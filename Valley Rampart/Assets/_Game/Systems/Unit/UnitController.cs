using UnityEngine;

/// <summary>8 向朝向（2_3 步骤5，D74）。与 doc 1 邻居顺序一致（E/NE/N/NW/W/SW/S/SE）。供 2_10 动画消费。</summary>
public enum Facing8 { E, NE, N, NW, W, SW, S, SE }

/// <summary>
/// 单位运行时控制器。挂在所有单位 Prefab 上（君主、NPC、敌人通用）。
/// 持有运行时状态，提供战斗（攻击/受击/死亡）和移动能力。
/// 由 UnitFactory 创建时注入 UnitData 配置。
///
/// 3.4 重构：实现 IDamageable 接口，TakeDamage 去掉 source 参数和内部公式（公式搬 DamageSystem），
/// AttackUnit 移除（改由 DamageSystem.RegisterAttack 驱动），Die 发改造后 UnitDiedEvent。
///
/// 数据变化事件：
///   - 单位生成     -> UnitSpawnedEvent
///   - 血量变化     -> UnitHpChangedEvent（受伤/治疗统一）
///   - 属性变化     -> UnitAttributeChangedEvent（MaxHp/Attack/Defense/速度，供 Buff/装备/升级系统）
///   - 受伤事件     -> UnitDamagedEvent（3.4 起由 DamageSystem 发布，不在 TakeDamage 内发）
///   - 死亡         -> UnitDiedEvent（3.4 改造：IDamageable + Faction + Position + Killer + Cause）
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class UnitController : MonoBehaviour, ISaveable, IDamageable, IUnitHandle, IClickInteractable, ITaskSource
{
    // ===== ISaveable =====

    /// <summary>全局唯一存档 ID。由 Initialize 分配 GUID，读档时由 OverrideSaveId 覆盖为存档里的值。</summary>
    public string SaveId { get; private set; }
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Scene;

    // ===== 运行时数据 =====

    public UnitData Data { get; private set; }
    public int CurrentHp { get; private set; }

    // ===== QQQ.2 T17：NPC 唯一 ID（任务调度器 _npcTaskMap 键）+ 死亡事件 =====
    /// <summary>NPC 唯一 ID（出池时由 Initialize 重新分配，每次生成唯一）。</summary>
    public int npcId;
    private static int s_nextNpcId = 1;
    /// <summary>NPC 死亡事件（传 npcId）。TaskScheduler 订阅用清指派。</summary>
    public static event System.Action<int> OnUnitDied;

    // ===== 2_16 步骤2：王国归属（D329 门面，默认 0=玩家；AI/动态王国由 Foundry/转化管线传入非 0 id）=====
    /// <summary>王国归属 id（0=玩家；AI/动态王国=KingdomRegistry 分配的 id）。2_16 步骤2，随 UnitSaveData 存档恢复。</summary>
    public int kingdomId;

    // ===== 2_16 步骤11 D295/D308：流民来源王国（动态立国性格混合 D295 / 2_19 灭亡流民带原国 id）=====
    /// <summary>来源王国 id（默认 -1=无来源：预置者/玩家招募者皆此；2_19 灭亡流民带原国 id，供动态立国性格混合）。不入 UnitSaveData（2_19 供给侧落地前占位）。</summary>
    public int originKingdomId = -1;

    // ===== 3.5 P0 步骤4：运行时职业覆盖（转职用，不污染共享 UnitData SO）=====
    private int _runtimeOccupation = -1;   // -1 = 未设置，回退 Data.occupation
    /// <summary>有效职业（优先运行时覆盖，否则 Data.occupation）。</summary>
    public Occupation EffectiveOccupation => _runtimeOccupation >= 0 ? (Occupation)_runtimeOccupation : (Data != null ? Data.occupation : Occupation.Civilian);
    /// <summary>设置运行时职业（TrainingSystem 转职用；随 UnitSaveData.occupation 持久化）。</summary>
    public void SetOccupation(Occupation occ) { _runtimeOccupation = (int)occ; }

    // ===== 2_17 步骤10 Faction 收编：AI 王国单位贯彻 AiKingdom（不污染共享 UnitData SO）=====
    private int _runtimeFaction = -1;   // -1 = 未设置，走派生判定；AI 王国单位走 _runtimeFaction≥0 显式覆写或派生兜底
    /// <summary>有效阵营（优先运行时显式覆写；否则 Data.faction==Human_Player 且 kingdomId>0 的 AI 王国单位派生为 AiKingdom）。
    /// 收编后 kingdomId>0 的 AI 单位无论走生成门面(SetFaction)还是读档路径(无 runtime 覆写)均稳定=AiKingdom，
    /// 消除存读回环里读档后回落 Human_Player 的阵营漂移（2_17 步骤10 收编目标一致性）。</summary>
    public Faction EffectiveFaction => _runtimeFaction >= 0 ? (Faction)_runtimeFaction
        : (Data != null && Data.faction == Faction.Human_Player && kingdomId > 0 ? Faction.AiKingdom
        : (Data != null ? Data.faction : Faction.None));
    /// <summary>设置运行时阵营（AI 王国单位生成后显式覆写为 AiKingdom；读档走派生兜底，无需逐单位覆写）。</summary>
    public void SetFaction(Faction faction) { _runtimeFaction = (int)faction; }

    // ===== 3.5 P1 生活状态：饱食 / 幸福 / 装备（随 UnitSaveData v2 持久化）=====
    /// <summary>个体饱食度（0-100）。由 SatietySystem 每日结算，0 扣血 / 80+ 回血。</summary>
    public int Satiety;
    /// <summary>个体幸福度（0-100）。由 HappinessSystem 多因素结算，整体幸福 = 全体平均。</summary>
    public int IndividualHappiness = 50;

    /// <summary>
    /// 上次生育天数（3.5 P0-1：个体生育冷却记录，-999=可生育）。
    /// 实体制下为随机配对个体冷却（lastBirthDay + birthPairCooldownDays <= 当前天 才可再配对，3.5.1 §4.2）。
    /// </summary>
    public int LastBirthDay = -999;

    /// <summary>
    /// 小孩成长天数事件计数（3.5.1 §4.2 E-S4：每日 +1，累积 childGrowthDayEvents 次自动长成居民）。
    /// </summary>
    public int ChildGrowthDays;

    /// <summary>
    /// 流浪汉招募标记（3.5.1 §4.1 E-S4：招募后职业已翻转为居民，正在走回王国途中，
    /// 抵达王国锚点附近才正式纳入人口注册表；途中遇敌有风险）。
    /// </summary>
    public bool IsVagrantRecruited;

    /// <summary>
    /// 出生营地坐标（QQQ.2 T11 / DR-7）：SpawnVagrantNear 写入出生营地位置。
    /// 未招募流浪汉的 HomePoint = 本值（在营地附近游荡，不朝王国走）；招募后切换王国锚点。
    /// </summary>
    public Vector2 BirthCampPos;

    /// <summary>初始化生活状态（新建单位调用；SatietySystem/HappinessSystem/存档读档共用）。</summary>
    public void InitLifeState(int satiety, int happiness)
    {
        Satiety = Mathf.Clamp(satiety, 0, 100);
        IndividualHappiness = Mathf.Clamp(happiness, 0, 100);
    }

    // ===== 工事（3.6 §4.4：墙/门/拒马/塔对象配此引用，越墙判定/移动阻挡用）=====
    // 瞬态配置字段，不入存档（用户约束：存档不做 AI/NPC 相关）
    [Tooltip("工事配置（墙/门/拒马/塔对象配此引用；非工事单位留空）")]
    public FortificationDef fortification;

    /// <summary>
    /// 3.5 P1-13 城门昼夜开关运行时覆盖（可空）。null=用共享 FortificationDef.passable；
    /// 有值=覆盖可通行状态（GateController 昼夜/玩家控制写入）。
    /// 因 FortificationDef 是共享 SO（改它污染所有城门），故用运行时覆盖，不污染资产。
    /// </summary>
    public bool? FortificationPassableOverride;

    /// <summary>最终韧性 = 职业基础韧性 + 防御 × 系数（3.6 §4.2，骑兵冲击反制）。</summary>
    public float Toughness
    {
        get
        {
            var prof = _professionSnapshot;
            return prof.baseToughness + Defense * prof.toughnessDefenseScale;
        }
    }

    // ===== 击飞状态（3.6 §5.4：非物理模拟，抛物线位移 + 打断攻击）=====
    // 瞬态，不入存档
    private Vector2 _knockbackDir;
    private float _knockbackStartX;   // 击飞起点 x（水平线性位移基准）
    private float _knockbackStartY;   // 击飞起点 y（弧线基准，回落回此值）
    private float _knockbackDistance;
    private float _knockbackDuration;
    private float _knockbackElapsed;
    private bool _knockbackActive;
    /// <summary>是否处于击飞中（移动/攻击被屏蔽）。</summary>
    public bool IsKnockedBack => _knockbackActive;
    /// <summary>击飞弧高（世界单位，数值抛物线峰值；3.6 §5.4，穿透冲锋撞飞更明显）。</summary>
    private const float KnockbackArcHeight = 6f;

    // ===== 减速（3.6 §3.4 Slow 场：区域减速，取最大系数）=====
    private float _slowFactor;
    private float _slowUntil;
    public void ApplySlow(float factor, float duration)
    {
        if (factor <= 0f) return;
        _slowFactor = Mathf.Max(_slowFactor, factor);
        _slowUntil = Mathf.Max(_slowUntil, Time.time + duration);
    }
    private float EffectiveSpeed(float baseSpeed)
    {
        return Time.time < _slowUntil ? baseSpeed * (1f - _slowFactor) : baseSpeed;
    }

    // ===== 骑兵冲锋（3.6 §5.3 状态：0=None 1=准备 2=突进① 3=停顿 4=突进②；双连击）=====
    // 穿透冲锋 = 分两段位移 + 路径击飞（NPCBrain.TickCharge），无物理碰撞
    public int ChargeState;
    public UnitController ChargeTarget;
    public float ChargeReadyTime;   // 组冷却截止
    public float ChargeSecondTime;  // 组内第二击时刻
    /// <summary>冲锋流程中（结算/第二击等待，免伤 70% 生效，DamageSystem 消费）。</summary>
    public bool IsCharging => ChargeState != 0;

    // ===== B1 弹药经济（3.7 战争机器火力经济学；对齐 sim SimUnit 弹药储备）=====
    // 仅供战争机器（投掷机/弩炮）用；弓手/弩手等兵种 ammoMax=0 无弹药模型（无限弹药）。
    // 三弹型：Stone 石弹（自动补给）/ Fireball 火弹 / Magic 魔弹（昂贵，有限储备不自动补）。
    public int AmmoStone;
    public int AmmoFireball;
    public int AmmoMagic;
    public float AmmoResupplyTimer;   // 石弹补给计时（到 0 补一发）
    /// <summary>是否弹药耗尽待补给（战争机器无弹停火）。</summary>
    public bool IsAmmoEmpty => AmmoStone <= 0 && AmmoFireball <= 0 && AmmoMagic <= 0;

    // ===== D3 清理轮：hv*/惜用阈值（默认 = champion 默认；NPCBrain.Init 从 AttentionTuningConfig 覆盖）=====
    public float HvKillHpGate = 0.3f;    // 高价值：残血阈值（tuning.hvKillHpGate，champion 0.3）
    public float HvDefenseGate = 33f;    // 高价值：重甲阈值（tuning.hvDefenseGate，champion 33）
    public float HvCrowdGate = 5.5f;     // 高价值：邻域敌数阈值（tuning.hvCrowdGate，champion 5.5；AOE 溅射收益）
    public float AmmoConserveRatio = 0.3f; // 惜用触发：石弹 < 此比例（tuning.ammoConserveRatio）

    // ===== 运行时可变属性 =====
    // 从 UnitData 初始化，可被 Buff/装备/升级系统修改；修改时发布 UnitAttributeChangedEvent。
    // 之前直接读 Data（只读 SO）无法支持运行时变化，故改为运行时副本。

    public int MaxHp { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public float WalkSpeed { get; private set; }
    public float RunSpeed { get; private set; }

    public bool IsAlive => CurrentHp > 0;

    // ===== IUnitHandle 实现（M1 决策核提取，AI.Core 端口；接缝 1/2）=====
    // 核内只认 IUnitHandle，不引用 UnitController/IDamageable。
    // IsAlive 显式实现 = 伪 null 检测（UnityEngine.Object 销毁后 this != null 为 false），
    // 与壳公共 IsAlive（CurrentHp>0）语义不同——核内"存活"=引用有效，壳内"存活"=有血。

    /// <summary>职业属性快照缓存（Initialize 时从 Data 生成，非 NpcProfessionDef 用默认值）</summary>
    private ProfessionSnapshot _professionSnapshot;

    // ===== 静态单位攻击（3.7 P1 审查修复：sim StaticThinkCore 的 Unity 等价物）=====
    // 塔/弩炮/投掷机是 isStatic 单位（无 NPCBrain），但 NpcProfessionDef.isStatic 语义要求
    // "有攻击值的按 CD 攻击射程内敌人"。Unity 侧此前无驱动 → 静态单位挂弹不发射。
    // 只在换目标时 RegisterAttack（DamageSystem tick 按 CD 驱动后续攻击，防每帧开火）。
    private IDamageable _staticTarget;

    Vector2X IUnitHandle.Position => new Vector2X(transform.position.x, transform.position.y);
    Faction IUnitHandle.Faction => Data != null ? Data.faction : Faction.None;
    bool IUnitHandle.IsAlive => this != null;  // 伪 null 检测（Unity 销毁对象）
    ProfessionSnapshot IUnitHandle.Profession => _professionSnapshot;

    // ===== 空间分区追踪（3.0.1 感知广播用；2_3 步骤2 改微格粒度 D70/D71）=====
    private GridCoord _lastGridSub;
    private bool _gridRegistered;

    /// <summary>8 向朝向（2_3 步骤5，D74；2_10 动画消费）。</summary>
    public Facing8 Facing { get; private set; } = Facing8.E;

    // ===== IDamageable 实现 =====

    /// <summary>世界坐标位置（空间分区查目标/投射物到达检测用）。</summary>
    public Vector2 GetPosition() => transform.position;

    /// <summary>阵营（敌我识别/Faction 二元判定用）。收编后优先运行时覆写（AI 王国单位=AiKingdom），否则 Data.faction。</summary>
    public Faction GetFaction() => EffectiveFaction;

    /// <summary>
    /// 按阵营映射堆叠类型（3.2 第 7.8 节）。
    /// 不划分防御类驻军——NPC 白天经济/晚上防御是行为模式，非阵营分类。
    /// </summary>
    public UnitCategory GetCategory()
    {
        if (Data == null) return UnitCategory.Civilian;
        return Data.faction switch
        {
            Faction.Human_Player => UnitCategory.Civilian,
            _ => UnitCategory.Enemy,
        };
    }

    protected SpriteRenderer _renderer;
    protected Rigidbody2D _rb;

    protected virtual void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        // D104 排序遮挡：单位与建筑同 sortingOrder 基底，靠全局 CustomAxis (0,1,0) Y-sort + spriteSortPoint=Pivot
        // 决定遮挡（单位在墙/建筑下方→单位在前；上方→建筑挡），而非 1D 时代的固定 order 盖层。
        _renderer.sortingOrder = 1;
        _renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        // Kinematic：不受物理力/重力影响，只受 MovePosition 控制，杜绝"停不下来"
        _rb.bodyType = RigidbodyType2D.Kinematic;
        // 冻结旋转：NPC 不会因物理碰撞翻倒
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    /// <summary>
    /// 出池洗涤（QQQ.3 B8-3 / LC-N1）：对象池复用前显式重置所有瞬态字段，
    /// 防止新生儿带着上辈子的职业/生育冷却/成长计数/招募标记/冲锋/减速/击飞/静态目标。
    /// 由 Initialize 首行调用（出池总是走 Initialize，无需在池层额外 Reset）。
    /// </summary>
    public virtual void ResetForReuse()
    {
        npcId = 0;                      // QQQ.2 T17：NPC ID 复位，Initialize 重新分配
        _runtimeOccupation = -1;        // 职业回退 Data.occupation
        LastBirthDay = -999;            // 生育冷却复位（可生育）
        ChildGrowthDays = 0;            // 成长计数清零
        IsVagrantRecruited = false;     // 招募标记清零
        BirthCampPos = Vector2.zero;    // QQQ.2 T11：出生营地坐标清零（池化复用防串）
        ChargeState = 0;                // 冲锋态复位
        ChargeTarget = null;
        ChargeReadyTime = 0f;
        ChargeSecondTime = 0f;
        _slowFactor = 0f;               // 减速复位
        _slowUntil = 0f;
        _knockbackActive = false;       // 击飞复位
        _knockbackDir = Vector2.zero;
        _staticTarget = null;           // 静态目标复位
        FortificationPassableOverride = null; // 城门昼夜覆盖复位
        AmmoStone = 0;                  // 弹药复位（由 Initialize 按职业重新装填）
        AmmoFireball = 0;
        AmmoMagic = 0;
        AmmoResupplyTimer = 0f;
        Satiety = 0;                    // 生活状态由 InitLifeState 重新初始化
        IndividualHappiness = 50;
    }

    /// <summary>
    /// 由 UnitFactory 调用，注入配置数据并初始化运行时状态。
    /// 同时向 UnitRegistry 注册自己，并发布 UnitSpawnedEvent 通知外界。
    /// </summary>
    public virtual void Initialize(UnitData data)
    {
        // QQQ.3 B8-3 / LC-N1：出池洗涤——重置所有瞬态字段，防止复用污染（职业/生育冷却/成长/招募/冲锋/减速/击飞/静态目标）
        ResetForReuse();

        Data = data;

        // QQQ.2 T17：分配唯一 NPC ID（任务调度器派发/查询用）
        npcId = s_nextNpcId++;

        // 从配置初始化运行时可变属性
        MaxHp = data.maxHp;
        Attack = data.attack;
        Defense = data.defense;
        WalkSpeed = data.walkSpeed;
        RunSpeed = data.runSpeed;

        CurrentHp = MaxHp;

        // M1 决策核提取：职业快照缓存（核内吃 ProfessionSnapshot 不吃 SO）
        var profession = data as NpcProfessionDef;
        _professionSnapshot = profession != null ? profession.ToSnapshot() : ProfessionSnapshot.Default;

        // B1 弹药经济初始化（对齐 sim SimWorld.Build）：石弹满槽（ammoMax），昂贵弹（火/魔）有限储备（各 1/4 槽）
        if (_professionSnapshot.ammoMax > 0)
        {
            AmmoStone = _professionSnapshot.ammoMax;
            AmmoFireball = Mathf.Max(0, _professionSnapshot.ammoMax / 4);
            AmmoMagic = Mathf.Max(0, _professionSnapshot.ammoMax / 4);
        }

        // 3.7 H4 修复：工事引用从职业配置拷贝（ProjectileManager 越墙判定 / NPCBrain 工事免疫依赖 uc.fortification）
        // 此前无赋值点，导致墙/门/拒马/塔的 fortification 永远 null，阻挡与免疫全部失效。
        if (profession != null && profession.fortification != null)
        {
            fortification = profession.fortification;
        }

        // 3.5 P1：初始化生活状态（饱食起始默认；幸福起始 50；无装备）
        int startSatiety = 80;
        if (KingdomManager.Instance != null && KingdomManager.Instance.Config != null)
            startSatiety = KingdomManager.Instance.Config.satietyStart;
        InitLifeState(startSatiety, 50);

        UnitRegistry.Instance.Register(this);

        // 2_12 步骤9：弹药单位（ammoMax>0 的战争机器/塔）注册为装填任务源——弹仓不足时发布 AmmoReload（HH.19 A×4）。
        if (_professionSnapshot.ammoMax > 0 && TaskScheduler.HasInstance)
            TaskScheduler.Instance.Register(this);

        // 分配唯一 SaveId 并注册为可存档对象
        SaveId = $"Unit_{data.faction}_{data.occupation}_{System.Guid.NewGuid():N}";
        SaveManager.Instance.RegisterSaveable(this);

        // 通知外界有新单位生成（UI/仇恨/存档可订阅）
        EventBus.Publish(new UnitSpawnedEvent(this));

        // 3.5 P0-2：出生即注册空间分区（静态工事/墙体无移动，不注册则永不入格，
        // 敌人在 GridSystem 感知不到墙体、IsBlockedByFortification 也查不到 → 城墙机制失效）。
        // 移动单位随后续移动 UpdateGridPosition 幂等覆盖，无副作用。
        UpdateGridPosition();

        Debug.Log($"[UnitController] 初始化: {data.faction}_{data.occupation} "
            + $"(HP: {CurrentHp}/{MaxHp}, ATK: {Attack}, DEF: {Defense})");
    }

    /// <summary>
    /// 用存档里的 SaveId 覆盖 Initialize 时分配的新 GUID。
    /// 由 UnitFactory.SpawnFromSave 在读档时调用。
    /// </summary>
    public void OverrideSaveId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        // R4: 使用 SaveManager.ChangeSaveId 原子化更换，避免中间窗口
        string oldId = SaveId;
        SaveId = id;
        SaveManager.Instance.ChangeSaveId(oldId, id, this);
    }

    // ===== ISaveable 实现 =====

    /// <summary>获取工人背包（缺失则补挂，QQQ.4 T8 兜底：Worker prefab 未挂 WorkerInventory 组件）。</summary>
    public WorkerInventory GetOrAddInventory()
    {
        var inv = GetComponent<WorkerInventory>();
        if (inv == null) inv = gameObject.AddComponent<WorkerInventory>();
        return inv;
    }

    public SavePayload SaveState()
    {
        var inv = GetOrAddInventory();   // QQQ.4 T8 兜底：Worker prefab 未挂组件则补挂
        var data = new UnitSaveData
        {
            saveDataVersion = 5,
            faction = (int)Data.faction,
            occupation = (int)EffectiveOccupation,
            currentHp = CurrentHp,
            maxHp = MaxHp,
            attack = Attack,
            defense = Defense,
            walkSpeed = WalkSpeed,
            runSpeed = RunSpeed,
            posX = transform.position.x,
            posY = transform.position.y,
            // v2：饱食 / 幸福（3.5 P1）
            satiety = Satiety,
            happiness = IndividualHappiness,
            // v3：个体上次生育天数（3.5 P0-1）+ 成长计数/招募标记（3.5.1 E-S4）
            lastBirthDay = LastBirthDay,
            childGrowthDays = ChildGrowthDays,
            isVagrantRecruit = IsVagrantRecruited,
            // v4：出生营地坐标（QQQ.2 T11 / DR-7：未招募流浪汉 HomePoint=营地）
            birthCampX = BirthCampPos.x,
            birthCampY = BirthCampPos.y,
            // v5：工人背包（QQQ.4 T9 / 需求5 资源生命周期）
            carriedType = (int)inv.carriedType,
            carriedAmount = inv.carriedAmount,
            // v6：王国归属（2_16 步骤2，D329 门面）
            kingdomId = kingdomId
        };
        return new SavePayload
        {
            typeName = typeof(UnitSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 5
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(UnitSaveData).AssemblyQualifiedName) return;

        var data = JsonUtility.FromJson<UnitSaveData>(payload.json);
        CurrentHp = data.currentHp;
        MaxHp = data.maxHp;
        Attack = data.attack;
        Defense = data.defense;
        WalkSpeed = data.walkSpeed;
        RunSpeed = data.runSpeed;
        // 位置已在 SpawnFromSave 时由 UnitFactory.SpawnUnit 设置

        // v2 兼容：v1 存档缺生活字段 → 给默认值（饱食=config 起始，幸福=50）
        if (data.saveDataVersion >= 2)
        {
            InitLifeState(data.satiety, data.happiness);
        }
        else
        {
            int startSatiety = 80;
            if (KingdomManager.Instance != null && KingdomManager.Instance.Config != null)
                startSatiety = KingdomManager.Instance.Config.satietyStart;
            InitLifeState(startSatiety, 50);
        }

        // v3 兼容：v3+ 存档恢复个体生育天数/成长计数/招募标记；旧档缺字段 → 默认值（JsonUtility 兜底）
        LastBirthDay = data.saveDataVersion >= 3 ? data.lastBirthDay : -999;
        ChildGrowthDays = data.saveDataVersion >= 3 ? data.childGrowthDays : 0;
        IsVagrantRecruited = data.saveDataVersion >= 3 && data.isVagrantRecruit;
        // v4 兼容：出生营地坐标（QQQ.2 T11 / DR-7）；旧档无 → zero（未招募流浪汉 HomePoint 回落王国锚点）
        BirthCampPos = data.saveDataVersion >= 4
            ? new Vector2(data.birthCampX, data.birthCampY)
            : Vector2.zero;
        // v5 兼容：工人背包（QQQ.4 T9 / 需求5 资源生命周期）；旧档无 → 空背包
        if (data.saveDataVersion >= 5)
        {
            var inv = GetOrAddInventory();
            inv.carriedType = (ResourceType)data.carriedType;
            inv.carriedAmount = Mathf.Max(0, data.carriedAmount);
        }
    }

    // ===== 战斗系统（3.4 重构）=====
    // AttackUnit 已移除：攻击改由 DamageSystem.RegisterAttack 驱动（NPCBrain 调注册接口）。
    // TakeDamage 退化为"收算好的伤害扣血"：公式搬 DamageSystem，去掉 source 参数。
    // UnitDamagedEvent 改由 DamageSystem 发布（含 source/position），不在 TakeDamage 内发。

    /// <summary>
    /// 受到伤害，只扣血。伤害已由 DamageSystem 算好+取整（百分比减伤+RoundToInt+保底1）。
    /// 血量≤0 触发 Die。发布 UnitHpChangedEvent 供血条 UI 刷新。
    /// </summary>
    public virtual void TakeDamage(int finalDamage)
    {
        if (Data == null || !IsAlive) return;

        int oldHp = CurrentHp;
        CurrentHp = Mathf.Max(0, CurrentHp - finalDamage);

        // 发布血量变化事件（供血条 UI 订阅，受伤/治疗统一走这里）
        EventBus.Publish(new UnitHpChangedEvent(this, oldHp, CurrentHp, MaxHp));

        Debug.Log($"[UnitController] {Data.faction}_{Data.occupation} "
            + $"受到 {finalDamage} 伤害，剩余 HP: {CurrentHp}/{MaxHp}");

        if (CurrentHp <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 恢复血量，不超过上限。发布 UnitHpChangedEvent 供血条 UI 刷新。
    /// </summary>
    public virtual void Heal(int amount)
    {
        if (Data == null || !IsAlive) return;

        int oldHp = CurrentHp;
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);

        if (oldHp != CurrentHp)
        {
            EventBus.Publish(new UnitHpChangedEvent(this, oldHp, CurrentHp, MaxHp));
        }

        Debug.Log($"[UnitController] {Data.faction}_{Data.occupation} 恢复 {amount} HP，当前: {CurrentHp}/{MaxHp}");
    }

    // ===== 属性修改（Buff/装备/升级系统调用）=====
    // 每次修改都会发布 UnitAttributeChangedEvent，UI 据此刷新对应显示。
    // MaxHp 变化时会自动夹取当前血量，并补发一次 UnitHpChangedEvent 让血条同步。

    public void SetMaxHp(int value)
    {
        if (Data == null) return;
        value = Mathf.Max(1, value);
        if (MaxHp == value) return;

        MaxHp = value;

        int oldHp = CurrentHp;
        if (CurrentHp > MaxHp)
        {
            CurrentHp = MaxHp;
        }

        EventBus.Publish(new UnitAttributeChangedEvent(this, UnitAttributeType.MaxHp));
        // 上限变化导致当前血量被夹取时，血条需要同步
        if (oldHp != CurrentHp)
        {
            EventBus.Publish(new UnitHpChangedEvent(this, oldHp, CurrentHp, MaxHp));
        }
    }

    public void SetAttack(int value)
    {
        if (Data == null) return;
        value = Mathf.Max(0, value);
        if (Attack == value) return;
        Attack = value;
        EventBus.Publish(new UnitAttributeChangedEvent(this, UnitAttributeType.Attack));
    }

    public void SetDefense(int value)
    {
        if (Data == null) return;
        value = Mathf.Max(0, value);
        if (Defense == value) return;
        Defense = value;
        EventBus.Publish(new UnitAttributeChangedEvent(this, UnitAttributeType.Defense));
    }

    public void SetWalkSpeed(float value)
    {
        if (Data == null) return;
        value = Mathf.Max(0f, value);
        if (Mathf.Approximately(WalkSpeed, value)) return;
        WalkSpeed = value;
        EventBus.Publish(new UnitAttributeChangedEvent(this, UnitAttributeType.WalkSpeed));
    }

    public void SetRunSpeed(float value)
    {
        if (Data == null) return;
        value = Mathf.Max(0f, value);
        if (Mathf.Approximately(RunSpeed, value)) return;
        RunSpeed = value;
        EventBus.Publish(new UnitAttributeChangedEvent(this, UnitAttributeType.RunSpeed));
    }

    /// <summary>
    /// 死亡处理：发布 UnitDiedEvent -> 注销注册 -> 回池（3.0.1 §7.4，替代原 Destroy）。
    /// 3.4 改造：UnitDiedEvent 扩为 IDamageable + Faction + Position + Killer + Cause。
    /// Killer 此处为 null（TakeDamage 无 source），DamageSystem 可在调用方补充击杀者信息。
    /// </summary>
    protected virtual void Die()
    {
        Debug.Log($"[UnitController] {Data?.faction}_{Data?.occupation} 死亡。");

        // 先注销 ISaveable，再回池，防止 SaveManager 抓到已回收实例
        SaveManager.Instance.UnregisterSaveable(this);

        // 先发布事件，订阅者仍可访问 this（RulerController/TopLeftHUD/DamageSystem/对象池）
        EventBus.Publish(new UnitDiedEvent(
            this,                          // Unit (IDamageable)
            Data != null ? Data.faction : Faction.None,  // Faction
            transform.position,            // Position
            null,                          // Killer（TakeDamage 无 source，此处为 null）
            DeathCause.Killed              // Cause（战斗致死）
        ));

        // QQQ.2 T17：通知任务调度器该 NPC 死亡（清其指派）
        if (npcId != 0) OnUnitDied?.Invoke(npcId);

        // 再从注册中心注销
        UnitRegistry.Instance.Unregister(this);

        // 2_12 步骤9：弹药单位注销装填任务源（弹仓需求不再发布；TaskScheduler.Unregister 亦清该源在派任务）
        if (_professionSnapshot.ammoMax > 0 && TaskScheduler.HasInstance)
            TaskScheduler.Instance.Unregister(this);

        // 从空间分区注销（3.0.1 感知广播用）
        GridSystem.Instance?.RemoveUnit(this);
        // 重置网格注册标志（回池后位置移动缓存失效，防出池跳过首格登记）
        _gridRegistered = false;
        _lastGridSub = default;

        // 3.0.1 §7.4 对象池回收（无工厂兜底销毁——如场景未挂 UnitFactory）
        if (UnitFactory.Instance != null)
            UnitFactory.Instance.ReturnUnitToPool(this);
        else
            Destroy(gameObject);
    }

    // ===== 移动系统（基于 Rigidbody2D 的 2D 移动）=====

    /// <summary>
    /// 击飞（3.6 §5.4）：按 direction 抛物线位移 distanceWorld，duration 内打断攻击 + 屏蔽移动。
    /// 高度表现：x 线性位移 + y 弧线（轻量视觉），逻辑位移只算 x（与 sim 对齐）。
    /// </summary>
    public void Knockback(Vector2 impactDir, float distanceWorld, float duration)
    {
        if (Data == null || !IsAlive || distanceWorld <= 0f) return;

        _knockbackDir = impactDir.normalized;
        // 地面基准 Y 只在首次受击时抓取：弧线中再被撞（二次击飞）不覆盖基准，
        // 保证抛物线最终落回地面基准线（y=-3），避免基准被抬高后悬空。
        if (!_knockbackActive)
        {
            _knockbackStartX = _rb.position.x;
            _knockbackStartY = _rb.position.y;
        }
        _knockbackDistance = distanceWorld;
        _knockbackDuration = Mathf.Max(0.1f, duration);
        _knockbackElapsed = 0f;
        _knockbackActive = true;

        // 打断攻击（3.6 §5.4）
        if (DamageSystem.Instance != null)
            DamageSystem.Instance.Unregister(this);
    }

    private void Update()
    {
        // 2_12 步骤9 / HH.19 决策3-A：退役假搬运定时器 TickAmmoResupply（石弹自动回充→弹药经济名义化），
        // 石弹与火/魔统一走"工人装填真实链"（SiegeWorkshopBuilding 厂仓/通用仓库 → 装填任务 → A* 弹仓）。
        // sim 侧行为差已登记 HH.15 台账，是否对齐由训练仓会话决策。

        // 3.7 P1 静态单位攻击（塔/弩炮/投掷机）：射程内最近敌注册攻击，无目标停手。
        // 静态单位无 NPCBrain，本分支是唯一攻击驱动（isStatic 判定开销极小，仅静态 prefab 命中）。
        // 改动②：crewRequired>0 的战争机器优先路由（先于 isStatic）——需工人操作才可发射/移动。
        if (_professionSnapshot.crewRequired > 0)
        {
            CrewMachineThinkCore();
            return;   // 乘员战争机器不走击飞（机器免疫，sim 同语义）
        }
        if (_professionSnapshot.isStatic)
        {
            StaticAttackThink();
            return;   // 静态单位不参与击飞（工事免疫击飞，sim 同语义）
        }

        if (!_knockbackActive) return;

        _knockbackElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_knockbackElapsed / _knockbackDuration);

        // 数值抛物线（3.6 §5.4，无物理）：
        //   水平 = 起点 x + 方向 × 距离 × t（线性，与 sim 对齐）
        //   垂直 = 起点 y + 弧高 × sin(π·t)（t=0.5 峰值，t=1 回落回起点——自然下落，无需平台碰撞）
        Vector2 pos = _rb.position;
        pos.x = _knockbackStartX + _knockbackDir.x * _knockbackDistance * t;
        pos.y = _knockbackStartY + KnockbackArcHeight * Mathf.Sin(Mathf.PI * t);
        _rb.MovePosition(pos);
        UpdateGridPosition();

        if (t >= 1f)
        {
            _knockbackActive = false;
            _rb.MovePosition(new Vector2(pos.x, _knockbackStartY)); // 落回起点高度
        }
    }

    // ===== 静态单位攻击（3.7 P1：sim StaticThinkCore 等价物，仅 isStatic 单位调用）=====

    /// <summary>
    /// 静态单位思考：射程内最近敌 → 注册攻击（换目标才 RegisterAttack，DamageSystem tick 按 CD 驱动）。
    /// B1：弹药评估（惜用省弹/耗尽停火/昂贵弹只对高价值目标，对齐 sim StaticThinkCore+SelectAmmo）。
    /// 无目标 → 注销停手。attack=0 的纯阻挡工事（墙/拒马/门）天然无目标注册。
    /// </summary>
    private void StaticAttackThink()
    {
        if (_professionSnapshot.attack <= 0) return;
        if (DamageSystem.Instance == null) return;
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return;

        IDamageable nearest = FindNearestEnemyInRange();
        if (nearest != null)
        {
            // B1：弹药评估——弹药耗尽停火等补给；惜用（弹药紧张且目标非高价值）省弹停火
            if (!SelectAmmo(nearest, out var ammoType)) return;
            if (!ReferenceEquals(nearest, _staticTarget))
            {
                _staticTarget = nearest;
                var profile = BuildStaticProfile(ammoType);
                if (DamageSystem.Instance.RegisterAttack(this, nearest, profile))
                    ConsumeAmmo(ammoType);   // B1：发射扣弹（对齐 sim SimDamage.ConsumeAmmo）
            }
        }
        else if (_staticTarget != null)
        {
            _staticTarget = null;
            DamageSystem.Instance.Unregister(this);
        }
    }

    /// <summary>静态单位攻击配置（从职业快照构造，弹药拉平；对齐 NPCBrain.UpdateCombatRegistration）。
    /// B1：ammoType 为 SelectAmmo 选中弹型（高价值目标可切昂贵弹）。</summary>
    private AttackProfile BuildStaticProfile(ProjectileType ammoType)
    {
        var p = _professionSnapshot;
        return new AttackProfile
        {
            attack = p.attack,
            range = p.attackRange,
            cd = p.attackCD,
            isRanged = p.isRanged,
            projectileSpeed = p.projectileSpeed,
            projectileType = ammoType,
            pierceLevel = p.pierceLevel,
            aoeRadiusCells = p.aoeRadiusCells,
            aoeFalloff = p.aoeFalloff,
            ballisticType = p.ballisticType,
            arcHeightCells = p.arcHeightCells,
            effectType = p.effectType,
            effectRadiusCells = p.effectRadiusCells,
            effectDuration = p.effectDuration,
            effectTickInterval = p.effectTickInterval,
            effectPower = p.effectPower,
            effectMaxTargets = p.effectMaxTargets,
        };
    }

    /// <summary>B1 弹药补给（对齐 sim SimWorld.TickAmmoResupply）：石弹缓慢自动恢复（模拟工人从后方搬运往返），
    /// 昂贵弹（火/魔）有限储备不自动补（需生产，AI 必须珍惜）。</summary>
    private void TickAmmoResupply()
    {
        if (_professionSnapshot.ammoMax <= 0) return;            // 非战争机器无弹药
        if (!IsAlive) return;
        if (_professionSnapshot.ammoResupplyDelay <= 0f) return; // 无补给线
        if (AmmoStone >= _professionSnapshot.ammoMax) return;    // 石弹满
        AmmoResupplyTimer += Time.deltaTime;
        if (AmmoResupplyTimer >= _professionSnapshot.ammoResupplyDelay)
        {
            AmmoResupplyTimer = 0f;
            AmmoStone++;
        }
    }

    /// <summary>B1 发射扣弹（对齐 sim SimDamage.ConsumeAmmo）。</summary>
    public void ConsumeAmmo(ProjectileType type)
    {
        switch (type)
        {
            case ProjectileType.Fireball:
                if (AmmoFireball > 0) AmmoFireball--;
                break;
            case ProjectileType.Magic:
                if (AmmoMagic > 0) AmmoMagic--;
                break;
            default:
                if (AmmoStone > 0) AmmoStone--;
                break;
        }
    }

    /// <summary>B1 弹药评估（对齐 sim SimBrain.SelectAmmo）：返回是否可发射并输出弹型。
    /// 非战争机器（ammoMax=0）恒可发射（职业默认弹型）；战争机器弹药耗尽停火；
    /// 惜用（ammoConservationWeight）时弹药紧张（石弹 &lt; AmmoConserveRatio）且目标非高价值 -> 省弹停火；
    /// 昂贵弹只对高价值目标用。D3 清理轮：阈值读字段（NPCBrain 注入 tuning）。</summary>
    public bool SelectAmmo(IDamageable target, out ProjectileType ammoType)
    {
        ammoType = _professionSnapshot.projectileType;
        if (_professionSnapshot.ammoMax <= 0) return true;
        if (IsAmmoEmpty) return false;                            // 弹药耗尽 -> 停火等补给
        float ammoRatio = _professionSnapshot.ammoMax > 0 ? (float)AmmoStone / _professionSnapshot.ammoMax : 1f;
        bool highValue = IsHighValueTarget(target);
        // 惜用：弹药紧张（石弹 < AmmoConserveRatio 槽）且目标非高价值 -> 省弹停火
        if (ammoRatio < AmmoConserveRatio && !highValue && _professionSnapshot.ammoConservationWeight > 0f)
            return false;
        // 弹型选择：高价值目标优先昂贵弹（Fireball/Magic 库存够才用，否则 Stone）
        if (highValue)
        {
            if (AmmoFireball > 0 && _professionSnapshot.effectType != GroundEffectType.None)
            { ammoType = ProjectileType.Fireball; return true; }
            if (AmmoMagic > 0)
            { ammoType = ProjectileType.Magic; return true; }
        }
        if (AmmoStone > 0) { ammoType = ProjectileType.Stone; return true; }
        return false;
    }

    // ===== 2_12 步骤9 装填真实链（D207~D212，HH.19 A×4）=====

    /// <summary>
    /// 装填写入：把对应 ResourceType 弹药填入弹仓（Ammo* 字段，对齐 sim SimUnit 弹仓储备）。
    /// 工人装填任务到达后调用（TaskScheduler.UnloadToMagazine）。弹仓满则拒收返回 0。
    /// </summary>
    public int AmmoTypeFor(ResourceType ammo)
    {
        switch (ammo)
        {
            case ResourceType.StoneAmmo: return 0;   // 0=Stone 槽位
            case ResourceType.FireballAmmo: return 1;
            case ResourceType.MagicAmmo: return 2;
            default: return -1;
        }
    }

    /// <summary>某弹药子仓当前余量（弹仓满停止装填判定）。</summary>
    public int AmmoSlotAmount(int slot)
    {
        switch (slot)
        {
            case 0: return AmmoStone;
            case 1: return AmmoFireball;
            default: return AmmoMagic;
        }
    }

    /// <summary>某弹药子仓容量上限（弹仓余量，非补贴决定装填量）。</summary>
    public int AmmoSlotCapacity(int slot)
    {
        int max = _professionSnapshot.ammoMax;
        if (max <= 0) return 0;   // 非弹药单位无弹仓
        switch (slot)
        {
            case 1: return Mathf.Max(0, max / 4);   // 昂贵弹（火/魔）各占 1/4 槽（对齐 Initialize）
            case 2: return Mathf.Max(0, max / 4);
            default: return max;
        }
    }

    /// <summary>
    /// 装填一单位 ResourceType 弹药入弹仓（≤余量），返回实际填入量（工人背陪伴扣减调用方保证）。
    /// 弹仓满/类型不符返回 0。
    /// </summary>
    public int FillMagazine(ResourceType ammo, int amt)
    {
        int slot = AmmoTypeFor(ammo);
        if (slot < 0 || amt <= 0) return 0;
        int cap = AmmoSlotCapacity(slot);
        int cur = AmmoSlotAmount(slot);
        if (cap <= 0 || cur >= cap) return 0;
        int fill = Mathf.Min(amt, cap - cur);
        switch (slot)
        {
            case 0: AmmoStone += fill; break;
            case 1: AmmoFireball += fill; break;
            default: AmmoMagic += fill; break;
        }
        return fill;
    }

    /// <summary>该单位是否有弹药缺口（任一弹仓未满，且缺该弹种可装填）。</summary>
    public bool HasMagazineSpace()
    {
        int max = _professionSnapshot.ammoMax;
        if (max <= 0) return false;
        for (int i = 0; i < 3; i++)
            if (AmmoSlotAmount(i) < AmmoSlotCapacity(i)) return true;
        return false;
    }

    // ===== ITaskSource 实现（2_12 步骤9：装填需求的源=弹仓需求的塔/机器自身）=====

    /// <summary>装填目标单位世界坐标（单位自身位置）。</summary>
    public Vector2 SourcePos => transform.position;

    /// <summary>装填任务源有效（存活且弹仓未满）。</summary>
    public bool IsValid => this != null && IsAlive && HasMagazineSpace();

    /// <summary>
    /// 发布装填任务：弹仓有缺口（且缺该弹种可补）→ 发 AmmoReload 任务（dest=UnitMagazine，自身位置）。
    /// 同类去重由调度器按源+类型处理；装填量按最小缺口附 args。
    /// </summary>
    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        if (!IsAlive || !HasMagazineSpace()) return false;
        var plan = PickReloadPlan();
        if (plan == null) return false;
        task = new KingdomTask(KingdomTaskType.AmmoReload, this, intensity: 1f);
        task.destType = KingdomDestType.UnitMagazine;
        task.destPos = transform.position;
        task.args = new ReloadAmmoArgs
        {
            ammoType = plan.Value.ammo,
            amount = plan.Value.amount
        };
        return true;
    }

    private struct AmmoNeed { public ResourceType ammo; public int amount; }

    /// <summary>选一个待装填弹药（最缺档位的弹种；昂贵弹只在其容量>0 且缺口时）。</summary>
    private AmmoNeed? PickReloadPlan()
    {
        int max = _professionSnapshot.ammoMax;
        if (max <= 0) return null;
        // 优先石弹（主力、容量=max）缺口；其次火/魔（昂贵弹各自 1/4 槽）
        int needStone = max - AmmoStone;
        if (needStone > 0) return new AmmoNeed { ammo = ResourceType.StoneAmmo, amount = needStone };
        int capF = Mathf.Max(0, max / 4), needF = capF - AmmoFireball;
        if (needF > 0) return new AmmoNeed { ammo = ResourceType.FireballAmmo, amount = needF };
        int capM = Mathf.Max(0, max / 4), needM = capM - AmmoMagic;
        if (needM > 0) return new AmmoNeed { ammo = ResourceType.MagicAmmo, amount = needM };
        return null;
    }

    /// <summary>装填任务源注册/注销（塔/机器出生/死亡时由 Initialize/OnDestroy 调）。</summary>
    public void OnRegister() { }
    public void OnUnregister() { }

    /// <summary>B1 目标价值评估（对齐 sim SimBrain.IsHighValueTarget）：残血 / 重甲 / 邻域密集。
    /// D3 清理轮：阈值读字段（NPCBrain 注入 tuning.hv*）；邻域密集用 GridSystem 邻近格扫描（对齐 sim AOE 溅射半径）。</summary>
    public bool IsHighValueTarget(IDamageable target)
    {
        if (target == null || target.CurrentHp <= 0) return false;
        float hpRatio = target.MaxHp > 0 ? (float)target.CurrentHp / target.MaxHp : 0f;
        if (hpRatio < HvKillHpGate) return true;      // 残血
        if (target.Defense >= HvDefenseGate) return true; // 重甲
        // 邻域密集（AOE 收益）：aoe 半径内 ≥ hvCrowdGate 个敌人（对齐 sim IsHighValueTarget：crowdRadius=aoeRadiusCells*2）
        if (HvCrowdGate > 0f && _professionSnapshot.aoeRadiusCells > 0f)
        {
            float aoeWorld = _professionSnapshot.aoeRadiusCells * 2f * GetCellSize();
            int crowd = CountNearbyHostiles(target, aoeWorld);
            if (crowd >= HvCrowdGate) return true;
        }
        return false;
    }

    /// <summary>统计目标邻域 aoeWorld 半径内的敌对单位数（GridSystem 邻近格扫描，对齐 sim crowd 计数）。</summary>
    private int CountNearbyHostiles(IDamageable center, float aoeWorld)
    {
        if (GridSystem.Instance == null || center == null) return 0;
        float cellSize = GetCellSize();
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(aoeWorld / cellSize));
        Vector2 centerPos = center.GetPosition();
        var centerCoordOpt = GridSystem.Instance.WorldToCoord(centerPos);
        if (!centerCoordOpt.HasValue) return 0;
        GridCoord centerCoord = centerCoordOpt.Value;
        int count = 0;
        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int dy = 0; dy <= 1; dy++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(centerCoord.x + dx, dy));
                foreach (var unit in units)
                {
                    var uc = unit as UnitController;
                    if (uc == null || !uc.IsAlive || uc.CurrentHp <= 0) continue;
                    if (uc.GetFaction() == GetFaction() || uc.GetFaction() == Faction.None) continue;
                    if (Vector2.Distance(centerPos, uc.transform.position) <= aoeWorld) count++;
                }
            }
        }
        return count;
    }

    /// <summary>网格 cellSize（GridSystem 未就绪时回退 1）。</summary>
    private float GetCellSize()
    {
        return (GridSystem.Instance != null && GridSystem.Instance.Config != null)
            ? GridSystem.Instance.Config.cellSize.x : 1f;
    }

    /// <summary>静态单位射程内最近敌对单位（GridSystem 邻近格扫描，y 地面+飞行两层）。</summary>
    private IDamageable FindNearestEnemyInRange()
    {
        return FindNearestEnemy(_professionSnapshot.attackRange * GetCellSize());
    }

    /// <summary>指定 rangeWorld 半径内最近敌对单位（GridSystem 邻近格扫描）。</summary>
    private IDamageable FindNearestEnemy(float rangeWorld)
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return null;
        float cellSize = GridSystem.Instance.Config.cellSize.x;
        var centerOpt = GridSystem.Instance.WorldToCoord(_rb.position);
        if (!centerOpt.HasValue) return null;
        GridCoord center = centerOpt.Value;
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(rangeWorld / cellSize));

        IDamageable nearest = null;
        float nearestDist = float.MaxValue;
        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, y));
                foreach (var unit in units)
                {
                    var uc = unit as UnitController;
                    if (uc == null || !uc.IsAlive || uc.CurrentHp <= 0) continue;
                    if (uc.GetFaction() == GetFaction() || uc.GetFaction() == Faction.None) continue;
                    float d = Vector2.Distance(_rb.position, uc.transform.position);
                    if (d < nearestDist)
                    {
                        nearestDist = d;
                        nearest = uc;
                    }
                }
            }
        }
        return nearest;
    }

    /// <summary>
    /// 乘员战争机器（改动②，对齐 sim SimBrain.CrewMachineThinkCore）：需 crewRequired 个友方工人
    /// （同阵营、存活、attack<=0 且 roleFamily==None）在 crewRadiusCells×cellSize 内才可发射 + 缓慢移动；
    /// 工人不足则停火停机。只在 crewRequired>0 时调用（先于 isStatic 路由）。
    /// </summary>
    private void CrewMachineThinkCore()
    {
        if (DamageSystem.Instance == null || GridSystem.Instance == null) return;

        int crew = CountCrewWorkers();
        bool crewed = crew >= _professionSnapshot.crewRequired;

        // 开火目标：射程内最近敌；reposition 目标：感知内最近敌（射程外也朝其推进，进射程再开火）
        IDamageable fireTarget = FindNearestEnemy(_professionSnapshot.attackRange * GetCellSize());
        IDamageable moveTarget = FindNearestEnemy(_professionSnapshot.perceptionRadius * GetCellSize());

        // 机器当作特殊建筑：必须有工人操作才可开火（改动②：工人门控发射）
        bool canFire = false;
        if (crewed && fireTarget != null && _professionSnapshot.attack > 0)
        {
            float d = Vector2.Distance(_rb.position, fireTarget.GetPosition());
            if (d <= _professionSnapshot.attackRange * GetCellSize() && SelectAmmo(fireTarget, out var ammoType))
            {
                canFire = true;
                if (!ReferenceEquals(fireTarget, _staticTarget))
                {
                    _staticTarget = fireTarget;
                    var profile = BuildStaticProfile(ammoType);
                    if (DamageSystem.Instance.RegisterAttack(this, fireTarget, profile))
                        ConsumeAmmo(ammoType);   // B1：发射扣弹
                }
            }
        }
        if (!canFire)
        {
            // 工人不足或射程外/耗弹：停火（不移动走下方 Idle 判定）
            if (_staticTarget != null)
            {
                _staticTarget = null;
                DamageSystem.Instance.Unregister(this);
            }
        }
        // 移动：有工人操作朝感知内最近敌缓慢推进（reposition，速度 = walkSpeed 缓慢，改动④）；工人不足则 Idle 停机
        if (crewed && moveTarget != null)
            MoveTowards(moveTarget.GetPosition(), speedOverride: _professionSnapshot.walkSpeed);
    }

    /// <summary>统计操作半径内可用工人数（同阵营、存活、attack<=0 且 roleFamily==None）。</summary>
    private int CountCrewWorkers()
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return 0;
        float cellSize = GetCellSize();
        float crewRadius = _professionSnapshot.crewRadiusCells * cellSize;
        var centerOpt = GridSystem.Instance.WorldToCoord(_rb.position);
        if (!centerOpt.HasValue) return 0;
        GridCoord center = centerOpt.Value;
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(crewRadius / cellSize));
        int count = 0;
        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var units = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, y));
                foreach (var unit in units)
                {
                    var uc = unit as UnitController;
                    if (uc == null || !uc.IsAlive || uc.CurrentHp <= 0) continue;
                    if (ReferenceEquals(uc, this)) continue;
                    if (uc.GetFaction() != GetFaction()) continue;   // 同阵营
                    if (!IsWorker(uc)) continue;                     // attack<=0 且 roleFamily==None
                    if (Vector2.Distance(_rb.position, uc.transform.position) > crewRadius) continue;
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>是否纯工人（attack&lt;=0 且 roleFamily==None，对齐 sim CrewMachineThinkCore 工人判定，复用 Civilian 工人）。</summary>
    private static bool IsWorker(UnitController uc)
    {
        var nd = uc.Data as NpcProfessionDef;
        if (nd != null) return nd.attack <= 0 && nd.roleFamily == RoleFamily.None;
        return uc.Attack <= 0;
    }

    /// <summary>是否需工人操作（改动②：crewRequired&gt;0 的战争机器单位）。供调度中心派工人用。</summary>
    public bool IsCrewMachine => _professionSnapshot.crewRequired > 0;

    /// <summary>缺几名工人（0=已满编/不需工人）。供调度中心（ScheduleCenterStub.DispatchCrew）按缺口派工人。</summary>
    public int CrewDeficit()
    {
        if (!IsCrewMachine) return 0;
        return Mathf.Max(0, _professionSnapshot.crewRequired - CountCrewWorkers());
    }

    /// <summary>附近是否有敌（有敌情才派工人操作，供调度中心做敌情门控，避免锁死工人）。</summary>
    public bool HasNearbyEnemy(float rangeWorld) => FindNearestEnemy(rangeWorld) != null;

    /// <summary>机器感知范围内是否有敌（敌情门控默认用感知半径）。</summary>
    public bool HasNearbyEnemy() => HasNearbyEnemy(_professionSnapshot.perceptionRadius * GetCellSize());

    /// <summary>
    /// 按方向移动（2_3 步骤1：2D 平面 8 向）。run=true 使用 runSpeed，否则 walkSpeed。
    /// 击飞中屏蔽移动；冲锋流程中（ChargeState!=0）普通移动让位（穿透冲锋为瞬间位移）。
    /// 普通移动不再撞墙停（D68）——规则性障碍规避走 2_6 寻路/PathFollower。
    /// </summary>
    public virtual void Move(Vector2 direction, bool run = false)
    {
        if (Data == null || !IsAlive || _knockbackActive) return;
        if (ChargeState != 0) return;   // 冲锋流程中普通移动让位（D69）

        UpdateFacing(direction);

        float speed = EffectiveSpeed(run ? RunSpeed : WalkSpeed);
        Vector2 movement = GridDistClampDir(direction) * speed * Time.deltaTime; // 格空间归一化 §1.6
        _rb.MovePosition(_rb.position + movement);   // 2D 平面：删 y 固定 / 删撞墙停（D68）
        UpdateGridPosition();
        // 拒马减速：经过敌方拒马格减速（友方拒马不减速）
        ApplyBarricadeSlowIfNeeded();
    }

    /// <summary>
    /// 向指定目标位置移动一步（2_3：2D 平面走向路径点，PathFollower 调用）。返回是否已到达。
    /// 击飞中视为已到达（不移动）。普通移动不再撞墙停（D68）——障碍规避由 2_6 寻路层负责。
    /// speedOverride（B3）：>0 时用该速度（NPCBrain 追击提速 speedChaseBoost 经此生效），
    /// 否则按 run 选 runSpeed/walkSpeed；速度仍过 EffectiveSpeed（拒马/Slow 场减速）。
    /// </summary>
    public virtual bool MoveTowards(Vector2 destination, bool run = false, float speedOverride = 0f)
    {
        if (Data == null || !IsAlive) return true;
        if (_knockbackActive) return true;
        if (ChargeState != 0) return true;   // 冲锋流程中普通移动让位（瞬间位移已处理）

        float speed = speedOverride > 0f
            ? EffectiveSpeed(speedOverride)
            : EffectiveSpeed(run ? RunSpeed : WalkSpeed);
        float step = speed * Time.deltaTime;

        Vector2 current = _rb.position;
        Vector2 newPos = Vector2.MoveTowards(current, destination, step); // 2D 平面（删 y 固定）

        UpdateFacing(newPos - current);

        _rb.MovePosition(newPos);
        UpdateGridPosition();
        // 拒马减速：经过敌方拒马格减速（友方拒马不减速）
        ApplyBarricadeSlowIfNeeded();

        return IsArrived(destination);   // 2_3 到达判定：格单位分量归一化距离（§1.6）
    }

    /// <summary>
    /// 瞬间位移（3.6 §5.3 穿透冲锋用）：瞬移到 worldPos（2D，删 y 锁定），并同步网格注册。
    /// 冲锋位移与普通移动共用网格维护路径。
    /// </summary>
    public void Teleport(Vector2 worldPos)
    {
        if (Data == null || !IsAlive) return;
        _rb.MovePosition(worldPos);   // 2D 瞬移（删 y 锁定）
        UpdateGridPosition();
    }

    // ===== 2_3 到达判定 + 格空间距离（steps 1/2，doc 1 §1.6）=====

    /// <summary>
    /// 是否已到达目标（2_3 步骤2，D3/D73）：格单位分量归一化距离 ≤ arriveRadiusCells。
    /// 落点吸附微格只在 SetDestination/到达时算（D73），此处仅判定。
    /// </summary>
    public bool IsArrived(Vector2 dest)
    {
        var cfg = GridSystem.Instance != null ? GridSystem.Instance.Config : null;
        if (cfg == null) return Vector2.Distance(_rb.position, dest) < 0.01f;
        return GridDist(_rb.position, dest) <= ArriveRadiusCells;
    }

    /// <summary>到达半径（格单位，§1.6）。来自 MovementConfig（2_3 步骤6 SO），缺配置兜底 0.3。</summary>
    float ArriveRadiusCells
    {
        get
        {
            var mc = MovementConfig.Instance;
            return mc != null && mc.arriveRadiusCells > 0f ? mc.arriveRadiusCells : 0.3f;
        }
    }

    /// <summary>格单位分量归一化欧氏距离（doc 1 §1.6）：√((Δx/cellW)² + (Δy/cellH)²)。</summary>
    float GridDist(Vector2 a, Vector2 b)
    {
        var cfg = GridSystem.Instance != null ? GridSystem.Instance.Config : null;
        float cw = cfg != null ? cfg.cellSize.x : 1.28f;
        float ch = cfg != null ? cfg.cellSize.y : 0.64f;
        float dx = (a.x - b.x) / cw, dy = (a.y - b.y) / ch;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>格空间方向归一化（doc 1 §1.6）：先除 cellSize 分量再归一化，使斜向与轴向速度一致。</summary>
    Vector2 GridDistClampDir(Vector2 dir)
    {
        var cfg = GridSystem.Instance != null ? GridSystem.Instance.Config : null;
        float cw = cfg != null ? cfg.cellSize.x : 1.28f;
        float ch = cfg != null ? cfg.cellSize.y : 0.64f;
        Vector2 scaled = new Vector2(dir.x / cw, dir.y / ch);
        return scaled.sqrMagnitude < 0.0001f ? Vector2.zero : scaled.normalized;
    }

    /// <summary>
    /// 工事挡移动：目标格子内有 blocksMovement 工事（城墙/拒马）→ 挡；城门（passable）不挡。
    /// public 供 NPCBrain 冲锋路径检查（3.7 P1.5：冲锋撞墙即止）。
    /// </summary>
    public bool IsBlockedByFortification(Vector2 worldPos)
    {
        if (fortification != null && fortification.blocksMovement) return true; // 自身是墙
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return false;

        var coordOpt = GridSystem.Instance.WorldToCoord(worldPos);
        if (!coordOpt.HasValue) return false;
        GridCoord coord = coordOpt.Value;
        var units = GridSystem.Instance.GetUnitsInCell(coord);
        foreach (var unit in units)
        {
            var uc = unit as UnitController;
            if (uc == null || uc == this || uc.fortification == null) continue;
            // 3.5 P1-13：城门 passable 用运行时覆盖（昼夜开关），否则用共享 SO 值
            bool passable = uc.FortificationPassableOverride ?? uc.fortification.passable;
            if (uc.fortification.blocksMovement && !passable)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 3.7 P1.4 拒马减速：移动后所在格有敌方拒马（Barricade 职业 + fortification）→ 施加减速。
    /// 拒马 = 减速带（不硬挡，blocksMovement=false），友方拒马不减速。值住 FortificationDef（SO 防硬编码）。
    /// </summary>
    private void ApplyBarricadeSlowIfNeeded()
    {
        if (Data == null || fortification != null) return;   // 自身是工事不自我减速
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return;

        var coordOpt = GridSystem.Instance.WorldToCoord(_rb.position);
        if (!coordOpt.HasValue) return;
        GridCoord coord = coordOpt.Value;
        var units = GridSystem.Instance.GetUnitsInCell(coord);
        foreach (var unit in units)
        {
            var uc = unit as UnitController;
            if (uc == null || uc == this || uc.fortification == null) continue;
            if (uc.Data == null || uc.Data.occupation != Occupation.Barricade) continue;
            if (uc.Data.faction == Data.faction) continue;   // 友方拒马不减速
            float factor = uc.fortification.barricadeSlowFactor;
            float duration = uc.fortification.barricadeSlowDuration;
            if (factor <= 0f) return;
            ApplySlow(factor, duration);
            return;
        }
    }

    /// <summary>
    /// 更新空间分区格子位置（2_3 步骤2：改微格粒度，D70/D71）。仅在跨微格时调 GridSystem.TryEnter，同微格内零开销。
    /// 3.0.1 感知广播系统依赖此方法维护 GridCell 实体列表。
    /// </summary>
    private void UpdateGridPosition()
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return;

        GridCoord? subOpt = GridSystem.Instance.WorldToSubCoord(_rb.position);
        if (!subOpt.HasValue) return;   // 越界（doc 1 D2 nullable）
        GridCoord sub = subOpt.Value;
        if (_gridRegistered && sub == _lastGridSub) return;   // 同微格零开销

        GridSystem.Instance.TryEnter(this, sub);
        _lastGridSub = sub;
        _gridRegistered = true;
    }

    /// <summary>
    /// 根据移动方向更新朝向（2_3 步骤5，D74）：速度方向量化 8 向供 2_10 动画；
    /// 渲染 flipX 暂保留（2_10 前过渡，按水平分量翻转）。
    /// </summary>
    private void UpdateFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return;   // 静止保持当前朝向

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        int octant = Mathf.RoundToInt(angle / 45f) & 7;   // 0=E 1=NE 2=N 3=NW 4=W 5=SW 6=S 7=SE
        Facing = (Facing8)octant;

        // 渲染 flipX 过渡（2_10 接管动画切换）：按水平分量翻转
        if (_renderer != null)
        {
            if (direction.x < -0.01f)
                _renderer.flipX = true;
            else if (direction.x > 0.01f)
                _renderer.flipX = false;
        }
    }

    // ========================================================================
    //  统一点击交互（3.5.1 §六，E-S8：IClickInteractable + 优先级子系统）
    // ========================================================================

    private InteractAction[] _interactActions;
    private int _interactActionsOcc = int.MinValue;

    /// <summary>
    /// 注册的交互行为列表（按职业缓存，保证 oneShot 状态跨点击有效）。
    /// §6.4 默认清单：流浪汉=招募100+对话10；居民=训练50+对话10；其余=对话。
    /// </summary>
    public InteractAction[] GetInteractActions()
    {
        int occ = (int)EffectiveOccupation;
        if (_interactActions == null || _interactActionsOcc != occ)
        {
            _interactActionsOcc = occ;
            _interactActions = BuildInteractActions();
        }
        return _interactActions;
    }

    InteractAction[] BuildInteractActions()
    {
        switch (EffectiveOccupation)
        {
            case Occupation.Vagrant:
                return new[]
                {
                    new InteractAction("recruit", InteractPriority.RecruitVagrant, oneShot: true,
                        canTrigger: () => IsAlive && EffectiveOccupation == Occupation.Vagrant
                                          && VagrantCampSystem.Instance != null && VagrantCampSystem.Instance.CanRecruit(),
                        execute: () =>
                        {
                            bool ok = VagrantCampSystem.Instance != null && VagrantCampSystem.Instance.RecruitVagrant(this);
                            OverheadSpeech.Show(transform, ok ? "谢谢您的食物！我这就去王国！" : "……");
                        }),
                    new InteractAction("talk", InteractPriority.Talk, oneShot: false,
                        canTrigger: () => IsAlive,
                        execute: () => OverheadSpeech.Show(transform, PickTalkLine()))
                };

            case Occupation.Resident:
                return new[]
                {
                    new InteractAction("train", InteractPriority.TrainResident, oneShot: false,
                        canTrigger: () => IsAlive && FindNearestTrainingBuilding() != null,
                        execute: () => OpenTrainingPanel()),
                    new InteractAction("talk", InteractPriority.Talk, oneShot: false,
                        canTrigger: () => IsAlive,
                        execute: () => OverheadSpeech.Show(transform, PickTalkLine()))
                };

            case Occupation.Child:
                return new[]
                {
                    new InteractAction("talk", InteractPriority.Talk, oneShot: false,
                        canTrigger: () => IsAlive,
                        execute: () => OverheadSpeech.Show(transform, PickTalkLine()))
                };

            case Occupation.Worker:
            case Occupation.Porter:
                return new[]
                {
                    new InteractAction("talk", InteractPriority.Talk, oneShot: false,
                        canTrigger: () => IsAlive,
                        execute: () => OverheadSpeech.Show(transform, PickTalkLine()))
                };

            default:
                // 士兵/将军/君主等：点击只出轻量对话（编队指挥走 E 键军令面板，§6.4 原则）
                return new[]
                {
                    new InteractAction("talk", InteractPriority.Talk, oneShot: false,
                        canTrigger: () => IsAlive,
                        execute: () => OverheadSpeech.Show(transform, PickTalkLine()))
                };
        }
    }

    /// <summary>
    /// 按当前状态从对话池随机抽取一句（QQQ.1 需求5）。
    /// 状态优先级：受伤(hp&lt;40%) &gt; 饥饿(satiety&lt;30) &gt; 正常。对应状态池为空时回退到正常池。
    /// public 供 NPCBrain 空闲自动说话（QQQ.2 T2 / DR-10）与点击对话共用。
    /// </summary>
    public string PickTalkLine()
    {
        bool hungry = Satiety < 30;
        bool injured = MaxHp > 0 && (float)CurrentHp / MaxHp < 0.4f;
        var lines = GetTalkLinesByOccupation(EffectiveOccupation, hungry, injured);
        return lines != null && lines.Length > 0 ? lines[Random.Range(0, lines.Length)] : "……";
    }

    /// <summary>返回某职业的对话池（按状态：正常/饥饿/受伤，池空回退正常）。</summary>
    string[] GetTalkLinesByOccupation(Occupation occ, bool hungry, bool injured)
    {
        string[] pool = null;
        if (injured) pool = GetTalkPool(occ, TalkState.Injured);
        if (pool == null || pool.Length == 0)
        {
            if (hungry) pool = GetTalkPool(occ, TalkState.Hungry);
        }
        if (pool == null || pool.Length == 0) pool = GetTalkPool(occ, TalkState.Normal);
        return pool;
    }

    enum TalkState { Normal, Hungry, Injured }

    /// <summary>按职业+状态返回对话文案池（QQQ.1 需求5 设计文案，17 职业）。</summary>
    string[] GetTalkPool(Occupation occ, TalkState state)
    {
        switch (occ)
        {
            case Occupation.Worker:
                switch (state)
                {
                    case TalkState.Hungry: return new[] { "肚子好饿……什么时候开饭。", "干不动了，想吃东西。", "粮仓是不是空了？" };
                    case TalkState.Injured: return new[] { "嘶……疼……还能撑住。", "轻伤不下火线。" };
                    default: return new[] { "正在干活呢。", "木头、石头、粮食，都得有人搬。", "今天也要努力工作。", "嘿咻……这活儿不轻。", "手艺不能丢，天天练。", "仓库快满了，加把劲。" };
                }
            case Occupation.Porter:
                switch (state)
                {
                    case TalkState.Hungry: return new[] { "扛不动了……没吃饭。", "饿得手抖。" };
                    case TalkState.Injured: return null;
                    default: return new[] { "搬运中，请让让。", "这批货挺沉的。", "往仓库送呢。", "别挡道，赶时间。", "一趟又一趟。", "运完了能歇会儿吗。" };
                }
            case Occupation.Resident:
                switch (state)
                {
                    case TalkState.Hungry: return new[] { "好饿啊……粮仓还有粮吗。", "肚子咕咕叫。" };
                    case TalkState.Injured: return null;
                    default: return new[] { "还没活干……想学门手艺。", "今天天气不错。", "什么时候能有活干呢。", "闲着也是闲着。", "希望能派上用场。", "你看起来很忙。" };
                }
            case Occupation.Child:
                switch (state)
                {
                    case TalkState.Hungry: return new[] { "饿饿……想吃东西。", "妈妈什么时候回来。" };
                    case TalkState.Injured: return null;
                    default: return new[] { "我很快就会长大啦！", "长大了我也要干活！", "嘿嘿，好好玩。", "大人都在忙呢。", "我以后要当英雄！", "你看我跑得快不快。" };
                }
            case Occupation.Vagrant:
                switch (state)
                {
                    case TalkState.Hungry: return new[] { "三天没吃东西了……", "饿得走不动了。" };
                    case TalkState.Injured: return null;
                    default: return new[] { "……又冷又饿……", "能给口吃的吗。", "我已经流浪好久了。", "求求你，收留我吧。", "外面的世界好危险。", "只要一口粮食就好。", "我也能干活的。" };
                }
            case Occupation.Ruler:
                switch (state)
                {
                    case TalkState.Hungry: return null;
                    case TalkState.Injured: return new[] { "我没事……还能指挥。", "保护王国要紧。" };
                    default: return new[] { "王国就托付给我吧。", "子民们需要我。", "建设王国，任重道远。", "今天的决策，明天的未来。", "王国的繁荣是我的责任。", "有什么事尽管说。", "吾乃一国之主。" };
                }
            case Occupation.General:
                switch (state)
                {
                    case TalkState.Hungry: return null;
                    case TalkState.Injured: return new[] { "将不退，兵不散。", "轻伤而已。" };
                    default: return new[] { "军令请走 E 键面板。", "士兵们随时待命。", "兵者，国之大事。", "布阵迎敌！", "令行禁止。", "战况如何？", "稳住阵脚。" };
                }
            case Occupation.Warrior:
                switch (state)
                {
                    case TalkState.Hungry: return new[] { "饿得挥不动剑……", "军粮还没到吗。" };
                    case TalkState.Injured: return new[] { "小伤，不碍事。", "还能战。" };
                    default: return new[] { "随时准备战斗！", "剑在手，不退缩。", "为了王国！", "训练不能停。", "敌人来了尽管上。", "保家卫国是本分。", "嘿嘿，手痒了。" };
                }
            case Occupation.Archer:
                switch (state)
                {
                    case TalkState.Hungry: return new[] { "拉弓没力气……", "饿了手会抖。" };
                    case TalkState.Injured: return null;
                    default: return new[] { "随时准备战斗！", "弓弦已上，随时放箭。", "百步穿杨。", "风向……差不多。", "箭囊还满着呢。", "远程压制交给我。" };
                }
            case Occupation.Crossbowman:
                switch (state)
                {
                    case TalkState.Hungry: return null;
                    case TalkState.Injured: return new[] { "还能再射几发。", "不退。" };
                    default: return new[] { "随时准备战斗！", "弩已上弦。", "穿透盔甲没问题。", "装填……好了。", "射程之内，皆是猎物。", "机械的力量。" };
                }
            case Occupation.HeavyWarrior:
                switch (state)
                {
                    case TalkState.Hungry: return null;
                    case TalkState.Injured: return new[] { "甲还没破，人还在。", "重装不退。" };
                    default: return new[] { "随时准备战斗！", "重甲在手，万夫莫开。", "我是铜墙铁壁。", "冲我来的都后悔。", "盾墙不可破。", "挡在前面是我的职责。" };
                }
            case Occupation.Cavalry:
                switch (state)
                {
                    case TalkState.Hungry: return new[] { "马也得吃东西啊……", "饿得跑不动。" };
                    case TalkState.Injured: return null;
                    default: return new[] { "随时准备战斗！", "冲锋号角何时响？", "马蹄之下，寸草不生。", "速度就是优势。", "绕后突袭，我的强项。", "马儿今天状态不错。" };
                }
            case Occupation.ShieldGuard:
                switch (state)
                {
                    case TalkState.Hungry: return null;
                    case TalkState.Injured: return null;
                    default: return new[] { "随时准备战斗！", "盾在人在。", "我守这里，谁都过不来。", "盾墙坚不可摧。", "后面的人放心输出。", "我的盾就是城墙。" };
                }
            case Occupation.Mage:
                switch (state)
                {
                    case TalkState.Hungry: return new[] { "魔力需要饱食支撑……", "饿得念不动咒。" };
                    case TalkState.Injured: return null;
                    default: return new[] { "随时准备战斗！", "魔力充盈。", "一个火球，一片敌军。", "元素听我号令。", "别打断我施法。", "魔法不是戏法。" };
                }
            case Occupation.Healer:
                switch (state)
                {
                    case TalkState.Hungry: return null;
                    case TalkState.Injured: return new[] { "我自己也得小心。", "还能撑住。" };
                    default: return new[] { "随时准备战斗！", "谁受伤了？我来。", "圣光护佑。", "别担心，有我在。", "治疗优先给前线。", "愿光明庇佑你们。" };
                }
            case Occupation.Bishop:
                switch (state)
                {
                    case TalkState.Hungry: return null;
                    case TalkState.Injured: return null;
                    default: return new[] { "随时准备战斗！", "信仰即是力量。", "圣言指引方向。", "黑暗退散。", "我为主传道。", "神眷不灭。" };
                }
            case Occupation.Archmage:
                switch (state)
                {
                    case TalkState.Hungry: return null;
                    case TalkState.Injured: return null;
                    default: return new[] { "随时准备战斗！", "奥术洪流蓄势待发。", "我已洞悉元素本质。", "别浪费我的法力。", "一念之间，天地变色。", "魔法之巅，不过如此。" };
                }
            default:
                return new[] { "……" };
        }
    }

    /// <summary>找最近的激活训练建筑（def.trainingSlots > 0）。无则 null。</summary>
    Building FindNearestTrainingBuilding()
    {
        if (BuildingRegistry.Instance == null) return null;
        Building best = null;
        float bestDist = float.MaxValue;
        foreach (var b in BuildingRegistry.Instance.All)
        {
            if (b == null || b.def == null || b.def.trainingSlots <= 0 || !b.IsActive) continue;
            float d = Mathf.Abs(b.transform.position.x - transform.position.x);
            if (d < bestDist) { bestDist = d; best = b; }
        }
        return best;
    }

    /// <summary>居民训练：找最近训练建筑 → SetTarget → TrainingPanel 入栈（复用 BuildingPanel 打开方式）。</summary>
    void OpenTrainingPanel()
    {
        var building = FindNearestTrainingBuilding();
        if (building == null)
        {
            OverheadSpeech.Show(transform, "还没有能训练的地方……");
            return;
        }
        var panel = FindObjectOfType<TrainingPanel>();
        if (panel == null)
        {
            Debug.LogWarning("[UnitController] 未找到 TrainingPanel（场景缺少挂载 TrainingPanel + UIDocument 的 GameObject）");
            return;
        }
        panel.SetTarget(building);
        UIManager.Instance?.Push(panel, new Interactor(Faction.Human_Player, transform.position));
    }
}

[System.Serializable]
public class UnitSaveData
{
    public int saveDataVersion = 1;
    public int faction;
    public int occupation;
    public int currentHp;
    public int maxHp;
    public int attack;
    public int defense;
    public float walkSpeed;
    public float runSpeed;
    public float posX;
    public float posY;
    // ===== v2（3.5 P1）：饱食 / 幸福 =====
    public int satiety = 80;
    public int happiness = 50;
    // ===== v3（3.5 P0-1 + 3.5.1 E-S4）：个体生育/成长/招募标记（旧档缺字段 JsonUtility 给默认值）=====
    public int lastBirthDay = -999;
    public int childGrowthDays;      // 小孩成长天数事件计数（E-S4）
    public bool isVagrantRecruit;    // 流浪汉招募走回标记（E-S4）
    // ===== v4（QQQ.2 T11 / DR-7）：出生营地坐标（未招募流浪汉 HomePoint=营地）=====
    public float birthCampX;
    public float birthCampY;
    // ===== v5（QQQ.4 T9 / 需求5 资源生命周期）：工人背包 =====
    public int carriedType;
    public int carriedAmount;
    // ===== v6（2_16 步骤2）：王国归属（D329 门面）。旧档缺字段→默认 0（玩家），向后兼容。=====
    public int kingdomId;
}
