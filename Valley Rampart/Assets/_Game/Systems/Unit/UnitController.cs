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

    // ===== 骑兵冲锋（3.6 §5.3 状态：0=None 1=冲锋结算 3=第二击等待）=====
    // 穿透冲锋 = 瞬间位移 + 路径击飞（NPCBrain.ImpactCharge），无逐帧突进态
    public int ChargeState;
    public UnitController ChargeTarget;
    public float ChargeReadyTime;   // 组冷却截止
    public float ChargeSecondTime;  // 组内第二击时刻
    /// <summary>冲锋流程中（结算/第二击等待，免伤 70% 生效，DamageSystem 消费）。</summary>
    public bool IsCharging => ChargeState != 0;

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

        // 3.7 H4 修复：工事引用从职业配置拷贝（ProjectileManager 越墙判定 / NPCBrain 工事免疫依赖 uc.fortification）
        // 此前无赋值点，导致墙/门/拒马/塔的 fortification 永远 null，阻挡与免疫全部失效。
        if (profession != null && profession.fortification != null)
        {
            fortification = profession.fortification;
        }

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
            faction = (int)Data.faction,
            occupation = (int)Data.occupation,
            currentHp = CurrentHp,
            maxHp = MaxHp,
            attack = Attack,
            defense = Defense,
            walkSpeed = WalkSpeed,
            runSpeed = RunSpeed,
            posX = transform.position.x,
            posY = transform.position.y
        };
        return new SavePayload
        {
            typeName = typeof(UnitSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
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
        _knockbackStartX = _rb.position.x;
        _knockbackStartY = _rb.position.y;
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
        // 3.7 P1 静态单位攻击（塔/弩炮/投掷机）：射程内最近敌注册攻击，无目标停手。
        // 静态单位无 NPCBrain，本分支是唯一攻击驱动（isStatic 判定开销极小，仅静态 prefab 命中）。
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
            if (!ReferenceEquals(nearest, _staticTarget))
            {
                _staticTarget = nearest;
                var profile = BuildStaticProfile();
                DamageSystem.Instance.RegisterAttack(this, nearest, profile);
            }
        }
        else if (_staticTarget != null)
        {
            _staticTarget = null;
            DamageSystem.Instance.Unregister(this);
        }
    }

    /// <summary>静态单位攻击配置（从职业快照构造，弹药拉平；对齐 NPCBrain.UpdateCombatRegistration）。</summary>
    private AttackProfile BuildStaticProfile()
    {
        var p = _professionSnapshot;
        return new AttackProfile
        {
            attack = p.attack,
            range = p.attackRange,
            cd = p.attackCD,
            isRanged = p.isRanged,
            projectileSpeed = p.projectileSpeed,
            projectileType = p.projectileType,
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

    /// <summary>静态单位射程内最近敌对单位（GridSystem 邻近格扫描，y 地面+飞行两层）。</summary>
    private IDamageable FindNearestEnemyInRange()
    {
        float cellSize = GridSystem.Instance.Config.cellSize;
        float rangeWorld = _professionSnapshot.attackRange * cellSize;
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
}
