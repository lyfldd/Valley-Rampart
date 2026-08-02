// ============================================================================
//  M2 Headless 模拟器 - SimDamage 伤害时间轮（复刻 DamageSystem）
//  04_模拟器规格.md §三 保真度契约（DamageSystem.cs 逐行对照）：
//    - 伤害公式：DamageSystem.cs:273-279 atk×(1−def/(def+armorK))，RoundToInt，保底 1
//    - 攻击 CD：DamageSystem.cs:121-122 Ceil(cd/tick)×tick，至少一个 tick（0.1s）
//    - 首次立即攻击：DamageSystem.cs:137 注册成功立刻打一发（开局不僵）
//    - overkill：DamageSystem.cs:113-114 近战锁定数 ≥2 拒绝注册（overkillLimit=2）
//    - 射程判定：DamageSystem.cs:222-228 Vector2.Distance vs range×cellSize，超程不打
//    - 远程 hitscan：04 §四 直线 hitscan + 固定 1 tick 延迟（Unity 抛物线飞行简化）
//    - 死亡清理：DamageSystem.cs:294-324 三表清理（注册表/过度杀伤/节流）
//  同步击杀竞态（决策点 3）：默认 Unity 语义（注册即结算）；twoPhaseResolution=true 时
//  两相结算（步骤4注册入队/步骤5结算，首发立即攻击保留=注册后下 tick 结算步打）。
//  确定性：结算顺序 = 注册顺序（_order 列表，非 Dictionary 枚举），满足 04 §七。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 伤害管线（攻击判定 + 伤害计算 + 受击事件 + 死亡清理）。
/// 每 tick 由 SimWorld 调 Tick()：时间轮收集 CD 到点攻击 -> 分片结算；再结算远程延迟命中。
/// </summary>
public sealed class SimDamage : ISimAttackPort
{
    private readonly SimConfig _config;
    private readonly SimClock _clock;
    private readonly SimEventBus _events;
    private readonly SimHeat _heat;

    private float ArmorK => _config.armorK;
    private float TickInterval => _config.damageTickInterval;
    private int MaxAttacksPerFrame => _config.maxAttacksPerFrame;
    private int OverkillLimit => _config.overkillLimit;
    private float EventThrottle => _config.eventThrottle;

    // ===== 注册表（三张表，对应 DamageSystem）=====

    private struct AttackRegistration
    {
        public SimUnit target;
        public SimAttackProfile profile;
        public float cd;
        public float nextAttackTime;
    }

    /// <summary>attacker -> 注册信息。</summary>
    private readonly Dictionary<SimUnit, AttackRegistration> _registrations = new();

    /// <summary>attacker 注册顺序（确定性关键：结算按注册序，非 Dictionary 枚举序）。</summary>
    private readonly List<SimUnit> _order = new();

    /// <summary>target -> 近战锁定数（过度杀伤计数，仅近战计入）。</summary>
    private readonly Dictionary<SimUnit, int> _overkillCount = new();

    /// <summary>victim -> 上次发 UnitDamagedEvent 时间（节流字典）。</summary>
    private readonly Dictionary<SimUnit, float> _lastEventTime = new();

    // ===== 时间轮 =====

    private float _tickTimer;
    private readonly List<SimUnit> _pendingAttacks = new();

    // ===== 远程 hitscan 延迟命中（04 §四：直线 hitscan + 固定 1 tick 延迟）=====

    private struct ScheduledHit
    {
        public SimUnit attacker;
        public SimUnit target;
        public int attack;
    }
    private readonly List<ScheduledHit> _scheduledHits = new();

    // ===== 统一结算缓冲（阶段1 锁定 -> 阶段2 统一生效，消除结算顺序先手）=====

    private struct AppliedHit
    {
        public SimUnit target;
        public int total;
        public SimUnit source;
    }
    private readonly List<AppliedHit> _applied = new();

    public SimDamage(SimConfig config, SimClock clock, SimEventBus events, SimHeat heat)
    {
        _config = config;
        _clock = clock;
        _events = events;
        _heat = heat;
    }

    // ===== 注册接口（SimBrain.UpdateCombatRegistration 调用，决策 2+8）=====

    public bool RegisterAttack(SimUnit attacker, SimUnit target, in SimAttackProfile profile)
    {
        if (attacker == null || target == null) return false;

        // 过度杀伤检查（仅近战；DamageSystem.cs:113-114）
        if (!profile.isRanged && GetOverkillCount(target) >= OverkillLimit)
            return false;

        // 如果已注册，先清理旧的（换目标场景；DamageSystem.cs:117-118）
        if (_registrations.ContainsKey(attacker))
            UnregisterInternal(attacker);

        // CD 取整到 tick 倍数，至少一个 tick（DamageSystem.cs:121-122；MathfX 无 float Ceil，内联 Math.Ceiling）
        float cd = (float)System.Math.Ceiling(profile.cd / TickInterval) * TickInterval;
        cd = MathfX.Max(TickInterval, cd);

        _registrations[attacker] = new AttackRegistration
        {
            target = target,
            profile = profile,
            cd = cd,
            nextAttackTime = _clock.Now + cd,
        };
        _order.Add(attacker);

        // 近战计入过度杀伤（DamageSystem.cs:133-134）
        if (!profile.isRanged)
            _overkillCount[target] = GetOverkillCount(target) + 1;

        // 首次攻击：Unity 语义注册即打一发（决策 18，战斗开局不僵；DamageSystem.cs:137）
        if (_config.twoPhaseResolution)
        {
            // 决策点 3 两相结算：注册入队，结算步打（首发立即攻击保留=下 tick 结算步）
            _pendingAttacks.Add(attacker);
        }
        else
        {
            ExecuteAttack(attacker);
        }

        return true;
    }

    /// <summary>撤销攻击（SimBrain.StopAttacking 调）。</summary>
    public void Unregister(SimUnit attacker)
    {
        UnregisterInternal(attacker);
    }

    private void UnregisterInternal(SimUnit attacker)
    {
        if (!_registrations.TryGetValue(attacker, out var reg)) return;

        if (!reg.profile.isRanged)
            DecrementOverkill(reg.target);

        _registrations.Remove(attacker);
        _order.Remove(attacker);
    }

    // ===== 时间轮 + 分片（DamageSystem.Update + CollectPendingAttacks + ProcessPendingAttacks）=====

    /// <summary>每 tick 调（04 §二 第 5 步）：时间轮收集 CD 到点 + 分片结算 + 远程延迟命中结算。</summary>
    public void Tick(float dt)
    {
        _tickTimer += dt;
        if (_tickTimer >= TickInterval)
        {
            _tickTimer -= TickInterval;
            CollectPendingAttacks();
        }
        ProcessPendingAttacks();
        ProcessScheduledHits();
    }

    private void CollectPendingAttacks()
    {
        // 按注册顺序遍历（确定性；DamageSystem 用 Dictionary 枚举，sim 固定为注册序）
        for (int i = 0; i < _order.Count; i++)
        {
            var attacker = _order[i];
            if (_registrations.TryGetValue(attacker, out var reg) && _clock.Now >= reg.nextAttackTime)
                _pendingAttacks.Add(attacker);
        }
    }

    private void ProcessPendingAttacks()
    {
        if (_pendingAttacks.Count == 0) return;

        int count = MathfX.Min(_pendingAttacks.Count, MaxAttacksPerFrame);

        // 阶段1：锁定本 tick 要结算的攻击（注册序；两相首发 + CD 到点），
        // 射程判定用当前瞬时位置（DamageSystem.ExecuteAttack 语义），伤害先只累计不应用。
        _applied.Clear();
        for (int i = 0; i < count; i++)
        {
            var attacker = _pendingAttacks[i];
            if (!_registrations.TryGetValue(attacker, out var reg)) continue;

            var target = reg.target;
            if (target == null || !target.IsAlive || target.CurrentHp <= 0)
            {
                UnregisterInternal(attacker);
                continue;
            }

            // 射程判定（DamageSystem.cs:222-228：超程不打，保持注册等靠近）
            float distance = Vector2X.Distance(attacker.Position, target.Position);
            float rangeWorld = reg.profile.range * _config.cellSize;
            if (distance > rangeWorld) continue;

            if (reg.profile.isRanged)
            {
                // 远程：直线 hitscan 延迟 1 tick 结算
                _scheduledHits.Add(new ScheduledHit { attacker = attacker, target = target, attack = reg.profile.attack });
            }
            else
            {
                // 近战：伤害累计（同 tick 同目标多击合并一次应用）
                int dmg = CalculateDamage(reg.profile.attack, target.Defense);
                bool found = false;
                for (int a = 0; a < _applied.Count; a++)
                {
                    if (ReferenceEquals(_applied[a].target, target))
                    {
                        var hit = _applied[a];
                        hit.total += dmg;
                        _applied[a] = hit;
                        found = true;
                        break;
                    }
                }
                if (!found)
                    _applied.Add(new AppliedHit { target = target, total = dmg, source = attacker });
            }

            // 更新下次攻击时间（DamageSystem.cs:243-245）
            reg.nextAttackTime = _clock.Now + reg.cd;
            _registrations[attacker] = reg;
        }

        // 阶段2：统一生效（同 tick 伤害无先后——Unity 因 MonoBehaviour Update 顺序不定
        // 而无系统先手，sim 用"统一生效"模拟该对称性，消除 spawn 序先手；确定性与保真度取舍见报告）
        for (int i = 0; i < _applied.Count; i++)
        {
            var hit = _applied[i];
            ApplyDamageBatch(hit.source, hit.target, hit.total);
        }

        // 剩余推下 tick（DamageSystem.cs:199-202）
        if (count < _pendingAttacks.Count)
            _pendingAttacks.RemoveRange(0, count);
        else
            _pendingAttacks.Clear();
    }

    /// <summary>统一结算版 ApplyDamage（总伤害一次扣血；死亡/事件/热度/溯源语义与 ApplyDamage 一致）。</summary>
    private void ApplyDamageBatch(SimUnit source, SimUnit target, int finalDamage)
    {
        if (target == null || !target.IsAlive || target.CurrentHp <= 0) return;

        target.Damage(finalDamage);

        // 受击热度累积（LODSystem.OnUnitDamaged：中区块 +0.4 + 记录热点）
        _heat.AddHit(target.Position.x, _clock.Now);

        // 发布受击事件（节流；NPCBrain.OnDamaged 消费：HitCooldown + 受击溯源）
        PublishDamagedEvent(target, source, finalDamage);

        // 死亡结算
        if (target.CurrentHp <= 0)
        {
            target.MarkDead();
            OnUnitDied(target, source);
        }
    }

    /// <summary>远程延迟命中结算：上一 tick 注册的 hitscan 在此结算（固定 1 tick 延迟）。</summary>
    private void ProcessScheduledHits()
    {
        if (_scheduledHits.Count == 0) return;
        for (int i = 0; i < _scheduledHits.Count; i++)
        {
            var h = _scheduledHits[i];
            if (h.target != null && h.target.IsAlive && h.target.CurrentHp > 0)
                ApplyDamage(h.attacker, h.target, h.attack);
        }
        _scheduledHits.Clear();
    }

    // ===== 执行攻击（DamageSystem.ExecuteAttack）=====

    private void ExecuteAttack(SimUnit attacker)
    {
        if (!_registrations.TryGetValue(attacker, out var reg)) return;

        var target = reg.target;
        var profile = reg.profile;

        // 检查 target 有效性（DamageSystem.cs:214-219）
        if (target == null || !target.IsAlive || target.CurrentHp <= 0)
        {
            UnregisterInternal(attacker);
            return;
        }

        // 检查距离（target 是否还在范围内；DamageSystem.cs:222-228）
        float distance = Vector2X.Distance(attacker.Position, target.Position);
        float rangeWorld = profile.range * _config.cellSize;
        if (distance > rangeWorld)
        {
            // 目标超出范围，不攻击（等 NPCBrain 换目标或靠近）
            return;
        }

        // 分流（决策 9+12）：近战即时命中 / 远程直线 hitscan 延迟 1 tick（04 §四）
        if (profile.isRanged)
        {
            _scheduledHits.Add(new ScheduledHit { attacker = attacker, target = target, attack = profile.attack });
        }
        else
        {
            ApplyDamage(attacker, target, profile.attack);
        }

        // 更新下次攻击时间（DamageSystem.cs:243-245）
        reg.nextAttackTime = _clock.Now + reg.cd;
        _registrations[attacker] = reg;
    }

    // ===== 伤害计算 + 应用（DamageSystem.ApplyDamage + CalculateDamage）=====

    /// <summary>伤害计算：atk×(1−def/(def+armorK))，RoundToInt + 保底 1（DamageSystem.cs:273-279）。</summary>
    public int CalculateDamage(int attack, int defense)
    {
        float reduction = defense / (defense + ArmorK);
        float rawDamage = attack * (1f - reduction);
        int finalDamage = MathfX.RoundToInt(rawDamage);
        return MathfX.Max(1, finalDamage);
    }

    private void ApplyDamage(SimUnit source, SimUnit target, int attack)
    {
        if (target == null || !target.IsAlive || target.CurrentHp <= 0) return;

        int finalDamage = CalculateDamage(attack, target.Defense);

        // 扣血
        target.Damage(finalDamage);

        // 受击热度累积（LODSystem.OnUnitDamaged：中区块 +0.4 + 记录热点）
        _heat.AddHit(target.Position.x, _clock.Now);

        // 发布受击事件（节流：同一 victim 每 EventThrottle 秒最多一次；DamageSystem.cs:282-290）
        // NPCBrain.OnDamaged 订阅此事件（HitCooldown + 受击溯源），节流语义与 Unity 一致
        PublishDamagedEvent(target, source, finalDamage);

        // 死亡结算（Unity 中 TakeDamage 后 UnitController 销毁发 UnitDiedEvent；DamageSystem.OnUnitDied 清三表）
        if (target.CurrentHp <= 0)
        {
            target.MarkDead();
            OnUnitDied(target, source);
        }
    }

    private void PublishDamagedEvent(SimUnit victim, SimUnit source, int damage)
    {
        if (_lastEventTime.TryGetValue(victim, out float lastTime))
        {
            if (_clock.Now - lastTime < EventThrottle) return;
        }
        _lastEventTime[victim] = _clock.Now;
        _events.Publish(new SimUnitDamagedEvent { Victim = victim, Source = source, Damage = damage });
    }

    // ===== 死亡清理（DamageSystem.OnUnitDied，决策 24）=====

    private void OnUnitDied(SimUnit victim, SimUnit killer)
    {
        // 1. victim 是 attacker -> 删除其注册项（含过度杀伤-1）
        if (_registrations.ContainsKey(victim))
            UnregisterInternal(victim);

        // 2. victim 是 target -> 删除所有以 victim 为 target 的注册项（不调 UnregisterInternal 防过度杀伤误减）
        for (int i = _order.Count - 1; i >= 0; i--)
        {
            var attackerKey = _order[i];
            if (_registrations.TryGetValue(attackerKey, out var reg) && ReferenceEquals(reg.target, victim))
            {
                _registrations.Remove(attackerKey);
                _order.RemoveAt(i);
            }
        }

        // 3. 清理过度杀伤计数
        _overkillCount.Remove(victim);

        // 4. 清理节流字典（防字典累积死者条目）
        _lastEventTime.Remove(victim);

        // 发布死亡事件（SimWorld 移除 + 编队减员 + 指标）
        _events.Publish(new SimUnitDiedEvent { Unit = victim, Killer = killer });
    }

    // ===== 公开查询 =====

    /// <summary>查目标当前被多少近战锁定（DamageSystem.GetOverkillCount）。</summary>
    public int GetOverkillCount(SimUnit target)
    {
        return _overkillCount.TryGetValue(target, out int count) ? count : 0;
    }

    private void DecrementOverkill(SimUnit target)
    {
        if (_overkillCount.TryGetValue(target, out int count))
        {
            if (count <= 1) _overkillCount.Remove(target);
            else _overkillCount[target] = count - 1;
        }
    }
}
