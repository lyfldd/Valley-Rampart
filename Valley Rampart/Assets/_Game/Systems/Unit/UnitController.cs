using UnityEngine;

/// <summary>
/// 单位运行时控制器。挂在所有单位 Prefab 上（君主、NPC、敌人通用）。
/// 持有运行时状态，提供战斗（攻击/受击/死亡）和移动能力。
/// 由 UnitFactory 创建时注入 UnitData 配置。
/// 
/// 数据变化事件：
///   - 单位生成     → UnitSpawnedEvent
///   - 血量变化     → UnitHpChangedEvent（受伤/治疗统一）
///   - 属性变化     → UnitAttributeChangedEvent（MaxHp/Attack/Defense/速度，供 Buff/装备/升级系统）
///   - 受伤/攻击/死亡沿用既有 UnitDamagedEvent / UnitAttackEvent / UnitDiedEvent
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public class UnitController : MonoBehaviour, ISaveable
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

    protected SpriteRenderer _renderer;
    protected Rigidbody2D _rb;

    protected virtual void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
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

    // ===== 战斗系统 =====

    /// <summary>
    /// 攻击目标单位，造成基于自身攻击力的伤害。
    /// </summary>
    public virtual void AttackUnit(UnitController target)
    {
        if (target == null || !target.IsAlive) return;
        if (Data == null) return;

        // 发布攻击事件（供 UI/音效/仇恨系统订阅）
        EventBus.Publish(new UnitAttackEvent(this, target, Attack));

        // 实际造成伤害，source 传递给 TakeDamage 以便发布 UnitDamagedEvent
        target.TakeDamage(Attack, this);

        Debug.Log($"[UnitController] {Data.faction}_{Data.occupation} "
            + $"攻击 {target.Data.faction}_{target.Data.occupation}，"
            + $"造成 {Attack} 伤害");
    }

    /// <summary>
    /// 受到伤害，自动按防御力减免。最低承受 1 点伤害（防止无敌）。
    /// source 为伤害来源，用于事件追踪（可为 null，如环境伤害）。
    /// 同时发布 UnitHpChangedEvent 供血条 UI 刷新。
    /// </summary>
    public virtual void TakeDamage(int rawDamage, UnitController source = null)
    {
        if (Data == null || !IsAlive) return;

        int actualDamage = Mathf.Max(1, rawDamage - Defense);
        int oldHp = CurrentHp;
        CurrentHp = Mathf.Max(0, CurrentHp - actualDamage);

        // 发布受击事件（供 UI/仇恨系统订阅）
        EventBus.Publish(new UnitDamagedEvent(this, source, actualDamage));
        // 发布血量变化事件（供血条 UI 订阅，受伤/治疗统一走这里）
        EventBus.Publish(new UnitHpChangedEvent(this, oldHp, CurrentHp, MaxHp));

        Debug.Log($"[UnitController] {Data.faction}_{Data.occupation} "
            + $"受到 {rawDamage} 伤害，防御减免后 -{actualDamage}，"
            + $"剩余 HP: {CurrentHp}/{MaxHp}");

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
    /// 死亡处理：发布事件 → 注销注册 → 销毁对象。
    /// </summary>
    protected virtual void Die()
    {
        Debug.Log($"[UnitController] {Data?.faction}_{Data?.occupation} 死亡。");

        // 先注销 ISaveable，再销毁对象，防止 SaveManager 抓到已销毁实例
        SaveManager.Instance.UnregisterSaveable(this);

        // 先发布事件，订阅者仍可访问 this
        EventBus.Publish(new UnitDiedEvent(this));

        // 再从注册中心注销
        UnitRegistry.Instance.Unregister(this);

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
        _rb.MovePosition(_rb.position + movement);
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

        UpdateFacing(newPos - current);

        _rb.MovePosition(newPos);

        return Vector2.Distance(current, destination) < 0.01f;
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
