using UnityEngine;

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
public class UnitController : MonoBehaviour, ISaveable, IDamageable, IUnitHandle
{
    // ===== ISaveable =====

    /// <summary>全局唯一存档 ID。由 Initialize 分配 GUID，读档时由 OverrideSaveId 覆盖为存档里的值。</summary>
    public string SaveId { get; private set; }
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Scene;

    // ===== 运行时数据 =====

    public UnitData Data { get; private set; }
    public int CurrentHp { get; private set; }

    // ===== 3.5 P0 步骤4：运行时职业覆盖（转职用，不污染共享 UnitData SO）=====
    private int _runtimeOccupation = -1;   // -1 = 未设置，回退 Data.occupation
    /// <summary>有效职业（优先运行时覆盖，否则 Data.occupation）。</summary>
    public Occupation EffectiveOccupation => _runtimeOccupation >= 0 ? (Occupation)_runtimeOccupation : (Data != null ? Data.occupation : Occupation.Civilian);
    /// <summary>设置运行时职业（TrainingSystem 转职用；随 UnitSaveData.occupation 持久化）。</summary>
    public void SetOccupation(Occupation occ) { _runtimeOccupation = (int)occ; }

    // ===== 3.5 P1 生活状态：饱食 / 幸福 / 装备（随 UnitSaveData v2 持久化）=====
    /// <summary>个体饱食度（0-100）。由 SatietySystem 每日结算，0 扣血 / 80+ 回血。</summary>
    public int Satiety;
    /// <summary>个体幸福度（0-100）。由 HappinessSystem 多因素结算，整体幸福 = 全体平均。</summary>
    public int IndividualHappiness = 50;
    /// <summary>已穿戴装备 id（null=无；穿戴后属性修正 + 职业转变，见 EquipmentSystem）。</summary>
    public string EquipId;

    /// <summary>初始化生活状态（新建单位调用；SatietySystem/HappinessSystem/存档读档共用）。</summary>
    public void InitLifeState(int satiety, int happiness, string equipId)
    {
        Satiety = Mathf.Clamp(satiety, 0, 100);
        IndividualHappiness = Mathf.Clamp(happiness, 0, 100);
        EquipId = string.IsNullOrEmpty(equipId) ? null : equipId;
    }

    // ===== 工事（3.6 §4.4：墙/门/拒马/塔对象配此引用，越墙判定/移动阻挡用）=====
    // 瞬态配置字段，不入存档（用户约束：存档不做 AI/NPC 相关）
    [Tooltip("工事配置（墙/门/拒马/塔对象配此引用；非工事单位留空）")]
    public FortificationDef fortification;

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

    // ===== 空间分区追踪（3.0.1 感知广播用）=====
    private GridCoord _lastGridCoord;
    private bool _gridRegistered;

    // ===== IDamageable 实现 =====

    /// <summary>世界坐标位置（空间分区查目标/投射物到达检测用）。</summary>
    public Vector2 GetPosition() => transform.position;

    /// <summary>阵营（敌我识别/Faction 二元判定用）。</summary>
    public Faction GetFaction() => Data != null ? Data.faction : Faction.None;

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
        // Kinematic：不受物理力/重力影响，只受 MovePosition 控制，杜绝"停不下来"
        _rb.bodyType = RigidbodyType2D.Kinematic;
        // 冻结旋转：NPC 不会因物理碰撞翻倒
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
    }

    /// <summary>
    /// 由 UnitFactory 调用，注入配置数据并初始化运行时状态。
    /// 同时向 UnitRegistry 注册自己，并发布 UnitSpawnedEvent 通知外界。
    /// </summary>
    public virtual void Initialize(UnitData data)
    {
        Data = data;

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
        InitLifeState(startSatiety, 50, null);

        UnitRegistry.Instance.Register(this);

        // 分配唯一 SaveId 并注册为可存档对象
        SaveId = $"Unit_{data.faction}_{data.occupation}_{System.Guid.NewGuid():N}";
        SaveManager.Instance.RegisterSaveable(this);

        // 通知外界有新单位生成（UI/仇恨/存档可订阅）
        EventBus.Publish(new UnitSpawnedEvent(this));

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

    public SavePayload SaveState()
    {
        var data = new UnitSaveData
        {
            saveDataVersion = 2,
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
            // v2：饱食 / 幸福 / 装备（3.5 P1）
            satiety = Satiety,
            happiness = IndividualHappiness,
            equipId = EquipId
        };
        return new SavePayload
        {
            typeName = typeof(UnitSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 2
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

        // v2 兼容：v1 存档缺生活字段 → 给默认值（饱食=config 起始，幸福=50，无装备）
        if (data.saveDataVersion >= 2)
        {
            InitLifeState(data.satiety, data.happiness, data.equipId);
        }
        else
        {
            int startSatiety = 80;
            if (KingdomManager.Instance != null && KingdomManager.Instance.Config != null)
                startSatiety = KingdomManager.Instance.Config.satietyStart;
            InitLifeState(startSatiety, 50, null);
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

        // 再从注册中心注销
        UnitRegistry.Instance.Unregister(this);

        // 从空间分区注销（3.0.1 感知广播用）
        GridSystem.Instance?.RemoveUnit(this);
        // 重置网格注册标志（回池后位置移动缓存失效，防出池跳过首格登记）
        _gridRegistered = false;
        _lastGridCoord = default;

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
        // B1 弹药补给（对齐 sim SimWorld.TickAmmoResupply）：战争机器石弹缓慢自动恢复（模拟工人搬运往返）
        TickAmmoResupply();

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
        GridCoord centerCoord = GridSystem.Instance.WorldToCoord(centerPos);
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
            ? GridSystem.Instance.Config.cellSize : 1f;
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
        float cellSize = GridSystem.Instance.Config.cellSize;
        GridCoord center = GridSystem.Instance.WorldToCoord(_rb.position);
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
        GridCoord center = GridSystem.Instance.WorldToCoord(_rb.position);
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
    /// 按方向移动。run=true 使用 runSpeed，否则使用 walkSpeed。
    /// 击飞中屏蔽移动；冲锋流程中（ChargeState!=0）普通移动让位（穿透冲锋为瞬间位移）。
    /// </summary>
    public virtual void Move(Vector2 direction, bool run = false)
    {
        if (Data == null || !IsAlive || _knockbackActive) return;
        if (ChargeState != 0) return;   // 3.6 §5.3：冲锋流程中普通移动让位

        UpdateFacing(direction);

        float speed = EffectiveSpeed(run ? RunSpeed : WalkSpeed);
        Vector2 movement = direction.normalized * speed * Time.deltaTime;
        Vector2 newPos = _rb.position + movement;
        newPos.y = _rb.position.y;  // 固定 Y 轴，1D 横版不上下移动
        // 3.7 P1.4 近战挡墙：方向移动同样受工事阻挡（撞墙即停，与 MoveTowards 语义一致）
        if (IsBlockedByFortification(newPos)) return;
        _rb.MovePosition(newPos);
        UpdateGridPosition();
        // 3.7 P1.4 拒马减速：经过敌方拒马格减速（友方拒马不减速）
        ApplyBarricadeSlowIfNeeded();
    }

    /// <summary>
    /// 向指定目标位置移动一步。返回是否已到达。
    /// 击飞中视为已到达（不移动）。工事挡移动（3.6 §2.3）：终点有 blocksMovement 工事 → 停下。
    /// speedOverride（B3，3.6 §六）：>0 时用该速度（NPCBrain 追击提速 speedChaseBoost 经此生效），
    /// 否则按 run 选 runSpeed/walkSpeed；速度仍过 EffectiveSpeed（拒马/Slow 场减速）。
    /// </summary>
    public virtual bool MoveTowards(Vector2 destination, bool run = false, float speedOverride = 0f)
    {
        if (Data == null || !IsAlive) return true;
        if (_knockbackActive) return true;
        if (ChargeState != 0) return true;   // 3.6 §5.3：冲锋流程中普通移动让位（瞬间位移已处理）

        // 工事挡移动（3.6 §2.3：墙/拒马挡，城门 passable 不挡）
        if (IsBlockedByFortification(destination)) return true;

        float speed = speedOverride > 0f
            ? EffectiveSpeed(speedOverride)
            : EffectiveSpeed(run ? RunSpeed : WalkSpeed);
        float step = speed * Time.deltaTime;

        Vector2 current = _rb.position;
        Vector2 newPos = Vector2.MoveTowards(current, destination, step);
        newPos.y = current.y;  // 固定 Y 轴，1D 横版不上下移动

        UpdateFacing(newPos - current);

        _rb.MovePosition(newPos);
        UpdateGridPosition();
        // 3.7 P1.4 拒马减速：经过敌方拒马格减速（友方拒马不减速）
        ApplyBarricadeSlowIfNeeded();

        return Vector2.Distance(current, destination) < 0.01f;
    }

    /// <summary>
    /// 瞬间位移（3.6 §5.3 穿透冲锋用）：瞬移到 worldPos（Y 锁定当前 y），并同步网格注册。
    /// 冲锋位移与普通移动共用网格维护路径。
    /// </summary>
    public void Teleport(Vector2 worldPos)
    {
        if (Data == null || !IsAlive) return;
        _rb.MovePosition(new Vector2(worldPos.x, _rb.position.y));
        UpdateGridPosition();
    }

    /// <summary>
    /// 工事挡移动：目标格子内有 blocksMovement 工事（城墙/拒马）→ 挡；城门（passable）不挡。
    /// public 供 NPCBrain 冲锋路径检查（3.7 P1.5：冲锋撞墙即止）。
    /// </summary>
    public bool IsBlockedByFortification(Vector2 worldPos)
    {
        if (fortification != null && fortification.blocksMovement) return true; // 自身是墙
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return false;

        GridCoord coord = GridSystem.Instance.WorldToCoord(worldPos);
        var units = GridSystem.Instance.GetUnitsInCell(coord);
        foreach (var unit in units)
        {
            var uc = unit as UnitController;
            if (uc == null || uc == this || uc.fortification == null) continue;
            if (uc.fortification.blocksMovement && !uc.fortification.passable)
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

        GridCoord coord = GridSystem.Instance.WorldToCoord(_rb.position);
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
    /// 更新空间分区格子位置。仅在跨格时调 GridSystem.TryEnter，同格内零开销。
    /// 3.0.1 感知广播系统依赖此方法维护 GridCell 实体列表。
    /// </summary>
    private void UpdateGridPosition()
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return;

        GridCoord currentCoord = GridSystem.Instance.WorldToCoord(transform.position);
        if (_gridRegistered && currentCoord == _lastGridCoord) return;

        GridSystem.Instance.TryEnter(this, currentCoord);
        _lastGridCoord = currentCoord;
        _gridRegistered = true;
    }

    /// <summary>
    /// 根据移动方向翻转精灵。默认精灵朝右。
    /// 向左移动时 flipX=true，向右移动时 flipX=false。
    /// </summary>
    private void UpdateFacing(Vector2 direction)
    {
        if (_renderer == null) return;

        if (direction.x < -0.01f)
            _renderer.flipX = true;
        else if (direction.x > 0.01f)
            _renderer.flipX = false;
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
    // ===== v2（3.5 P1）：饱食 / 幸福 / 装备 =====
    public int satiety = 80;
    public int happiness = 50;
    public string equipId;
}
