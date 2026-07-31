using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 攻击配置（注册攻击时由调用方提供）。
/// NPC 数据住 NpcProfessionDef（批次3），建筑数据住 BuildingDef.combat。
/// 调用方（NPCBrain）从 SO 构造此结构传入 DamageSystem。
/// </summary>
public struct AttackProfile
{
    public int attack;            // 攻击力
    public float range;           // 攻击范围（格数）
    public float cd;              // 攻击冷却（秒，内部取整到 0.1s 倍数）
    public bool isRanged;         // 是否远程
    public float projectileSpeed; // 弹速（远程用，世界单位/秒）
}

/// <summary>
/// 伤害管线集中调度器（3.4 核心）。
///
/// 职责：攻击判定 + 伤害计算 + 受击事件 + 死亡清理。
/// 性能破局点：时间轮砍 CD 遍历、分片抹尖峰、空间分区查目标、对象池零 GC。
///
/// 核心流程：
///   NPCBrain -> RegisterAttack(attacker, target, profile)
///   时间轮 tick -> CD 到点进待攻击队列 -> 分片处理
///   分流：近战即时命中 / 远程委托 ProjectileManager
///   伤害计算 -> victim.TakeDamage -> 发布 UnitDamagedEvent（节流）
///
/// 详见 3.4_伤害管线设计.md 第三~五节。
/// </summary>
public class DamageSystem : Singleton<DamageSystem>
{
    // ===== 配置（从 DamageConfig SO 加载，Play 模式实时拖滑块调参）=====

    private DamageConfig _config;

    private float ArmorK => _config.armorK;
    private float TickInterval => _config.tickInterval;
    private int MaxAttacksPerFrame => _config.maxAttacksPerFrame;
    private int OverkillLimit => _config.overkillLimit;
    private float EventThrottle => _config.eventThrottle;

    // ===== 注册表（三张表，决策 24 死亡清理目标）=====

    /// <summary>attacker -> 注册信息（attacker->target 映射 + CD）。</summary>
    private readonly Dictionary<IDamageable, AttackRegistration> _registrations = new();

    /// <summary>target -> 近战锁定数（过度杀伤计数，仅近战计入）。</summary>
    private readonly Dictionary<IDamageable, int> _overkillCount = new();

    /// <summary>victim -> 上次发 UnitDamagedEvent 时间（节流字典）。</summary>
    private readonly Dictionary<IDamageable, float> _lastEventTime = new();

    /// <summary>注册信息。</summary>
    private struct AttackRegistration
    {
        public IDamageable target;
        public AttackProfile profile;
        public float cd;             // 取整后的 CD
        public float nextAttackTime; // 下次攻击时间
    }

    // ===== 时间轮 + 分片 =====

    private float _tickTimer;
    private readonly List<IDamageable> _pendingAttacks = new();

    // ===== 生命周期 =====

    protected override void Awake()
    {
        base.Awake();
        _config = Resources.Load<DamageConfig>("Config/DamageConfig");
        if (_config == null)
            Debug.LogError("[DamageSystem] 未找到 DamageConfig！请确保 Resources/Config/DamageConfig.asset 存在。");
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        base.OnDestroy();
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    private void Update()
    {
        // 时间轮：每 tick 收集 CD 到点的攻击
        _tickTimer += Time.deltaTime;
        if (_tickTimer >= TickInterval)
        {
            _tickTimer -= TickInterval;
            CollectPendingAttacks();
        }

        // 分片处理（每帧最多 MaxAttacksPerFrame 个）
        ProcessPendingAttacks();
    }

    // ===== 注册接口（NPCBrain 调用，决策 2+8）=====

    /// <summary>
    /// 注册攻击。首次注册立即打一次，然后进 CD。
    /// 近战过度杀伤检查：如果 target 已被 OverkillLimit 个近战锁定，返回 false。
    /// </summary>
    /// <returns>false=注册失败（过度杀伤已满），调用方应选别的目标。</returns>
    public bool RegisterAttack(IDamageable attacker, IDamageable target, AttackProfile profile)
    {
        if (attacker == null || target == null) return false;

        // 过度杀伤检查（仅近战）
        if (!profile.isRanged && GetOverkillCount(target) >= OverkillLimit)
            return false;

        // 如果已注册，先清理旧的（换目标场景）
        if (_registrations.ContainsKey(attacker))
            UnregisterInternal(attacker);

        // CD 取整到 tick 倍数（决策 18）
        float cd = Mathf.Ceil(profile.cd / TickInterval) * TickInterval;
        cd = Mathf.Max(TickInterval, cd); // 至少一个 tick

        _registrations[attacker] = new AttackRegistration
        {
            target = target,
            profile = profile,
            cd = cd,
            nextAttackTime = Time.time + cd
        };

        // 近战计入过度杀伤
        if (!profile.isRanged)
            _overkillCount[target] = GetOverkillCount(target) + 1;

        // 首次攻击：立即打一次（决策 18，战斗开局不僵）
        ExecuteAttack(attacker);

        return true;
    }

    /// <summary>换目标（NPCBrain 选了新目标时调）。</summary>
    public void UpdateRegistration(IDamageable attacker, IDamageable newTarget)
    {
        if (!_registrations.TryGetValue(attacker, out var reg)) return;
        if (newTarget == null) return;

        // 旧 target 过度杀伤-1（近战）
        if (!reg.profile.isRanged)
            DecrementOverkill(reg.target);

        // 新 target 过度杀伤+1（近战）
        if (!reg.profile.isRanged)
            _overkillCount[newTarget] = GetOverkillCount(newTarget) + 1;

        reg.target = newTarget;
        _registrations[attacker] = reg;
    }

    /// <summary>撤销攻击（NPCBrain 停止攻击/目标死亡时调）。</summary>
    public void Unregister(IDamageable attacker)
    {
        UnregisterInternal(attacker);
    }

    private void UnregisterInternal(IDamageable attacker)
    {
        if (!_registrations.TryGetValue(attacker, out var reg)) return;

        // 过度杀伤-1（近战）
        if (!reg.profile.isRanged)
            DecrementOverkill(reg.target);

        _registrations.Remove(attacker);
    }

    // ===== 时间轮 + 分片 =====

    private void CollectPendingAttacks()
    {
        foreach (var kvp in _registrations)
        {
            if (Time.time >= kvp.Value.nextAttackTime)
                _pendingAttacks.Add(kvp.Key);
        }
    }

    private void ProcessPendingAttacks()
    {
        if (_pendingAttacks.Count == 0) return;

        int count = Mathf.Min(_pendingAttacks.Count, MaxAttacksPerFrame);
        for (int i = 0; i < count; i++)
        {
            ExecuteAttack(_pendingAttacks[i]);
        }

        // 剩余推下帧
        if (count < _pendingAttacks.Count)
            _pendingAttacks.RemoveRange(0, count);
        else
            _pendingAttacks.Clear();
    }

    // ===== 执行攻击 =====

    private void ExecuteAttack(IDamageable attacker)
    {
        if (!_registrations.TryGetValue(attacker, out var reg)) return;

        var target = reg.target;
        var profile = reg.profile;

        // 检查 target 有效性
        if (target == null || target.CurrentHp <= 0)
        {
            UnregisterInternal(attacker);
            return;
        }

        // 检查距离（target 是否还在范围内）
        float distance = Vector2.Distance(attacker.GetPosition(), target.GetPosition());
        float rangeWorld = profile.range * GetCellSize();
        if (distance > rangeWorld)
        {
            // 目标超出范围，不攻击（等 NPCBrain 换目标或靠近）
            return;
        }

        // 分流（决策 9+12）
        if (profile.isRanged)
        {
            // 远程：委托 ProjectileManager 发射投射物（位置驱动，不追踪）
            if (ProjectileManager.Instance != null)
                ProjectileManager.Instance.SpawnProjectile(attacker, target, profile);
        }
        else
        {
            // 近战：即时命中
            ApplyDamage(attacker, target, profile.attack);
        }

        // 更新下次攻击时间
        reg.nextAttackTime = Time.time + reg.cd;
        _registrations[attacker] = reg;
    }

    // ===== 伤害计算 + 应用（近战/投射物到达共用）=====

    /// <summary>
    /// 伤害计算 + 扣血 + 发布受击事件。
    /// 百分比减伤：伤害 = 攻击力 × (1 - 护甲/(护甲+K))，RoundToInt + 保底 1。
    /// 节流：同一 victim 每 EventThrottle 秒最多发一次 UnitDamagedEvent。
    /// </summary>
    public void ApplyDamage(IDamageable source, IDamageable target, int attack)
    {
        if (target == null || target.CurrentHp <= 0) return;

        // 伤害计算（float 内部运算，对外 int，决策 21）
        int finalDamage = CalculateDamage(attack, target.Defense);

        // 扣血（TakeDamage 只扣血，公式已在此算好）
        target.TakeDamage(finalDamage);

        // 发布受击事件（节流，决策 7）
        PublishDamagedEvent(target, source, finalDamage);
    }

    /// <summary>
    /// 百分比减伤公式：伤害 = 攻击力 × (1 - 护甲/(护甲+K))。
    /// int 取整：RoundToInt + 保底 1（防取整为 0）。
    /// </summary>
    public int CalculateDamage(int attack, int defense)
    {
        float reduction = defense / (defense + ArmorK);
        float rawDamage = attack * (1f - reduction);
        int finalDamage = Mathf.RoundToInt(rawDamage);
        return Mathf.Max(1, finalDamage);
    }

    /// <summary>发布受击事件（节流：同一 victim 每 EventThrottle 秒最多一次）。</summary>
    private void PublishDamagedEvent(IDamageable victim, IDamageable source, int damage)
    {
        if (_lastEventTime.TryGetValue(victim, out float lastTime))
        {
            if (Time.time - lastTime < EventThrottle) return;
        }
        _lastEventTime[victim] = Time.time;
        EventBus.Publish(new UnitDamagedEvent(victim, source, damage, victim.GetPosition()));
    }

    // ===== 死亡清理（决策 24，订阅 UnitDiedEvent 自清三表）=====

    private void OnUnitDied(UnitDiedEvent evt)
    {
        var victim = evt.Unit;
        if (victim == null) return;

        // 1. victim 是 attacker -> 删除其注册项（含过度杀伤-1）
        if (_registrations.ContainsKey(victim))
            UnregisterInternal(victim);

        // 2. victim 是 target -> 删除所有以 victim 为 target 的注册项
        List<IDamageable> toRemove = null;
        foreach (var kvp in _registrations)
        {
            if (kvp.Value.target == victim)
                (toRemove ??= new List<IDamageable>()).Add(kvp.Key);
        }
        if (toRemove != null)
        {
            foreach (var attackerKey in toRemove)
            {
                // 不调 UnregisterInternal（会减过度杀伤），直接删
                _registrations.Remove(attackerKey);
            }
        }

        // 3. 清理过度杀伤计数
        _overkillCount.Remove(victim);

        // 4. 清理节流字典（防字典累积死者条目；对象池回池重置时 ClearThrottleForVictim 再保险）
        _lastEventTime.Remove(victim);
    }

    // ===== 公开查询（供 NPCBrain 选目标用）=====

    /// <summary>查目标当前被多少近战锁定（过度杀伤计数）。</summary>
    public int GetOverkillCount(IDamageable target)
    {
        return _overkillCount.TryGetValue(target, out int count) ? count : 0;
    }

    /// <summary>清理节流字典项（对象池回池重置时调用，防新 NPC 继承上辈子状态）。</summary>
    public void ClearThrottleForVictim(IDamageable victim)
    {
        _lastEventTime.Remove(victim);
    }

    // ===== 辅助 =====

    private void DecrementOverkill(IDamageable target)
    {
        if (_overkillCount.TryGetValue(target, out int count))
        {
            if (count <= 1) _overkillCount.Remove(target);
            else _overkillCount[target] = count - 1;
        }
    }

    private float GetCellSize()
    {
        return GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize : 2.26f;
    }
}
