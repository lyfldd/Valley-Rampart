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
public class UnitController : MonoBehaviour, ISaveable, IDamageable
{
    // ===== ISaveable =====

    /// <summary>全局唯一存档 ID。由 Initialize 分配 GUID，读档时由 OverrideSaveId 覆盖为存档里的值。</summary>
    public string SaveId { get; private set; }
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Scene;

    // ===== 运行时数据 =====

    public UnitData Data { get; private set; }
    public int CurrentHp { get; private set; }

    // ===== 运行时可变属性 =====
    // 从 UnitData 初始化，可被 Buff/装备/升级系统修改；修改时发布 UnitAttributeChangedEvent。
    // 之前直接读 Data（只读 SO）无法支持运行时变化，故改为运行时副本。

    public int MaxHp { get; private set; }
    public int Attack { get; private set; }
    public int Defense { get; private set; }
    public float WalkSpeed { get; private set; }
    public float RunSpeed { get; private set; }

    public bool IsAlive => CurrentHp > 0;

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
    /// 死亡处理：发布 UnitDiedEvent -> 注销注册 -> 销毁对象。
    /// 3.4 改造：UnitDiedEvent 扩为 IDamageable + Faction + Position + Killer + Cause。
    /// Killer 此处为 null（TakeDamage 无 source），DamageSystem 可在调用方补充击杀者信息。
    /// </summary>
    protected virtual void Die()
    {
        Debug.Log($"[UnitController] {Data?.faction}_{Data?.occupation} 死亡。");

        // 先注销 ISaveable，再销毁对象，防止 SaveManager 抓到已销毁实例
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

        Destroy(gameObject);
    }

    // ===== 移动系统（基于 Rigidbody2D 的 2D 移动）=====

    /// <summary>
    /// 按方向移动。run=true 使用 runSpeed，否则使用 walkSpeed。
    /// 由玩家输入或 AI 每帧调用。
    /// </summary>
    public virtual void Move(Vector2 direction, bool run = false)
    {
        if (Data == null || !IsAlive) return;

        UpdateFacing(direction);

        float speed = run ? RunSpeed : WalkSpeed;
        Vector2 movement = direction.normalized * speed * Time.deltaTime;
        Vector2 newPos = _rb.position + movement;
        newPos.y = _rb.position.y;  // 固定 Y 轴，1D 横版不上下移动
        _rb.MovePosition(newPos);
        UpdateGridPosition();
    }

    /// <summary>
    /// 向指定目标位置移动一步。返回是否已到达。
    /// </summary>
    public virtual bool MoveTowards(Vector2 destination, bool run = false)
    {
        if (Data == null || !IsAlive) return true;

        float speed = run ? RunSpeed : WalkSpeed;
        float step = speed * Time.deltaTime;

        Vector2 current = _rb.position;
        Vector2 newPos = Vector2.MoveTowards(current, destination, step);
        newPos.y = current.y;  // 固定 Y 轴，1D 横版不上下移动

        UpdateFacing(newPos - current);

        _rb.MovePosition(newPos);
        UpdateGridPosition();

        return Vector2.Distance(current, destination) < 0.01f;
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
