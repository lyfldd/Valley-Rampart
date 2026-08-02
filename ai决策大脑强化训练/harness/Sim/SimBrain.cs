// ============================================================================
//  M2 Headless 模拟器 - SimBrain 个体决策脑（复刻 NPCBrain 三层裁决管线）
//  04_模拟器规格.md §三 think 节奏：NPCBrain.cs:85 10Hz；记忆组件 dt 用真实间隔
//  （sim 全活跃 10Hz，dt=0.1；04 §四 LOD 降频差异：sim 全活跃，测的是脑子不是调度）。
//  管线复刻（NPCBrain.Think() ⓪-⑤，04 §二 第 3 步）：
//    ⓪ BuildBaseContext -> ① 记忆组件 Tick -> ② FillContext+动态刺激源入池
//    -> ③ L1->L2->L3 纯管线 ->（第 4 步攻击注册 UpdateCombatRegistration 由 SimWorld 调）
//    -> ⑤ 缓存 cmd
//  感知复刻（NPCBrain.UpdatePerception，04 §二 第 2 步，每 2 tick=0.2s）：
//    格子粗筛+距离精判 -> 威胁刺激标定 -> 受击溯源 -> 热点支援（LOD 第二层）。
//  动态刺激源（Safety/Follow/Wander）内联 Provider 逻辑（壳 Provider 属 Assembly-CSharp，
//  harness 不引用壳，逻辑逐行复刻）。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 攻击注册端口（P4 SimDamage 实现；对应壳 DamageSystem 的 RegisterAttack/Unregister）。
/// </summary>
public interface ISimAttackPort
{
    bool RegisterAttack(SimUnit attacker, SimUnit target, in SimAttackProfile profile);
    void Unregister(SimUnit attacker);
}

/// <summary>攻击配置（复刻壳 DamageSystem.AttackProfile，NPCBrain 构造传入）。</summary>
public struct SimAttackProfile
{
    public int attack;
    public float range;           // 攻击范围（格数）
    public float cd;              // 攻击冷却（秒，内部取整到 tick 倍数）
    public bool isRanged;
    public float projectileSpeed; // 弹速（sim v0 直线 hitscan，不消费）
}

/// <summary>
/// IWorldQuery 适配器（接缝 3 的 sim 侧落地：包装 SimGrid + SimHeat）。
/// 供 AttentionSystem.SetWorldQuery（FollowStimulus 槽位化算 cellSize）与 SimBrain 世界查询。
/// </summary>
public sealed class SimWorldQueryAdapter : IWorldQuery
{
    private readonly SimGrid _grid;
    private readonly SimHeat _heat;
    private readonly SimClock _clock;

    public SimWorldQueryAdapter(SimGrid grid, SimHeat heat, SimClock clock)
    {
        _grid = grid;
        _heat = heat;
        _clock = clock;
    }

    public float CellSize => _grid.CellSizeForQuery;
    public float GetHeatAt(Vector2X pos) => _heat.GetHeatAt(pos.x);
    public bool TryGetHotspot(Vector2X pos, float maxAge, out Vector2X hotspot)
        => _heat.TryGetCombatHotspot(pos.x, maxAge, _clock.Now, out hotspot);
    public void QueryUnitsInCell(int cx, int cy, List<IUnitHandle> results)
        => _grid.QueryUnitsInCell(cx, results);   // cy 恒 0（1D 压平）
}

/// <summary>
/// 个体决策脑（复刻 NPCBrain 的核内管线调用）。
/// 决策核唯一真身 AI.Core：L1/L2/L3/记忆组件/注意力/公式全部复用核内静态类，
/// 本类只做壳侧装配与数据组装（BuildBaseContext/UpdatePerception/UpdateCombatRegistration）。
/// </summary>
public sealed class SimBrain
{
    private readonly SimUnit _self;
    private readonly TuningSnapshot _config;
    private readonly IWorldQuery _world;
    private readonly SimClock _clock;
    private ISimAttackPort _damage;

    // ===== 核内子系统（M1 迁移后的纯计算管线，复用）=====
    private readonly AttentionSystem _attention = new AttentionSystem();
    private readonly ThreatHysteresisComponent _threatHysteresis;
    private readonly ProtectionHysteresisComponent _protectionHysteresis;
    private readonly HitCooldownStateMachine _hitCooldown = new HitCooldownStateMachine();
    private IMemoryComponent[] _memoryComponents;

    // ===== 动态刺激源（池化单实例，对应壳三个 Provider 的逻辑）=====
    private readonly SafetyStimulus _safetyStimulus = new SafetyStimulus();
    private readonly FollowStimulus _followStimulus = new FollowStimulus();
    private readonly WanderStimulus _wanderStimulus = new WanderStimulus();

    // ===== 感知 =====
    private readonly List<IUnitHandle> _nearbyEnemies = new List<IUnitHandle>();
    private readonly List<IUnitHandle> _nearbyAllies = new List<IUnitHandle>();
    private float _nearestDist = float.MaxValue;
    private float _perceptionTimer = 999f;   // 首帧立即触发感知（NPCBrain.cs:83）

    // ===== 受击溯源（3.0.1_4 §2.3，聚合 O(1)）=====
    private IUnitHandle _lastAggressor;
    private int _recentHitCount;
    private float _lastHitTime;

    // ===== 追击状态（3.0.1_8 §六 放弃任务因子）=====
    private IUnitHandle _chaseTarget;
    private float _chaseStartTime;
    private float _lastChaseDist;

    // ===== 攻击链路（与 Executor 并行）=====
    private SimUnit _currentAttackTarget;

    // ===== 管线缓存 =====
    private float _lastRaw;
    private float _lastThinkTime;
    private FactorContext _lastCtx;
    public BehaviorCommand LastCmd;

    /// <summary>行为执行器（SimWorld 装配）。</summary>
    public SimExecutor Executor;

    /// <summary>是否绑定编队槽位（FormationController 状态清理用）。</summary>
    public bool HasFormationSlot => _followStimulus.IsFormationSlot;

    /// <summary>编队军令是否激活（有锚点绑定）。</summary>
    public bool FollowIsActive => _followStimulus.Anchor != null;

    /// <summary>上一帧完整 ctx（指标/日志用）。</summary>
    public FactorContext LastContext => _lastCtx;

    /// <summary>上一帧 rawFactor（指标用）。</summary>
    public float LastRaw => _lastRaw;

    /// <summary>当前谱系（日志 spectrum 事件用）。</summary>
    public BehaviorSpectrum CurrentSpectrum => _lastCtx.PostureDecision.Spectrum;

    /// <summary>是否处于追击中（放弃追击指标用）。</summary>
    public bool IsChasing => _chaseTarget != null;

    public SimBrain(SimUnit self, TuningSnapshot config, IWorldQuery world, SimClock clock,
                    ISimAttackPort damage)
    {
        _self = self;
        _config = config;
        _world = world;
        _clock = clock;
        _damage = damage;

        _threatHysteresis = new ThreatHysteresisComponent(config);
        _protectionHysteresis = new ProtectionHysteresisComponent(config);
        _memoryComponents = new IMemoryComponent[] { _threatHysteresis, _protectionHysteresis, _hitCooldown };

        _attention.SetConfig(config);
        _attention.SetWorldQuery(world);   // 接缝 3：世界查询注入
    }

    // ===== 感知（04 §二 第 2 步，NPCBrain 每 2 tick=0.2s 调）=====

    public void UpdatePerception(float dt)
    {
        _perceptionTimer += dt;
        if (_perceptionTimer < _config.perceptionUpdateInterval) return;
        _perceptionTimer = 0f;

        float perceptionWorld = _self.Profession.perceptionRadius * _world.CellSize;
        Vector2X myPos = _self.Position;
        Faction myFaction = _self.Faction;

        SimPerception.QueryNearby(myPos, perceptionWorld, myFaction, true, _world, _nearbyEnemies);
        SimPerception.QueryNearby(myPos, perceptionWorld, myFaction, false, _world, _nearbyAllies);

        _attention.ClearThreats();
        float currentTime = _clock.Now;
        _nearestDist = float.MaxValue;

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            var enemy = _nearbyEnemies[i];
            if (enemy == null || !enemy.IsAlive || enemy.CurrentHp <= 0) continue;

            float dist = Vector2X.Distance(myPos, enemy.Position);
            if (dist < _nearestDist) _nearestDist = dist;
            // 刺激强度标定 0-100（贴脸满强度，NPCBrain.cs:366）
            float intensity = MathfX.Max(1f, _config.threatIntensityMax * (1f - dist / perceptionWorld));

            var stimulus = new ThreatStimulus(
                enemy,
                threatLevel: (int)_threatHysteresis.CurrentLevel,
                intensity: intensity,
                expiry: currentTime + _config.threatDecayTime);
            _attention.AddStimulus(stimulus);
        }

        // 受击溯源（3.0.1_4 §2.3：感知范围外的攻击者也能被溯源到；NPCBrain.cs:380-394）
        if (_lastAggressor != null && _lastAggressor.IsAlive && _lastAggressor.CurrentHp > 0)
        {
            float delta = MathfX.Max(0f, currentTime - _lastHitTime);
            float baseIntensity = MathfX.Min(_config.traceMaxIntensity,
                _config.traceBaseIntensity + (_recentHitCount - 1) * _config.traceStepIntensity);
            float intensity = MathfX.Max(1f, baseIntensity * MathfX.Exp(-delta / _config.traceDecayTime));

            var trace = new ThreatStimulus(
                _lastAggressor,
                threatLevel: MathfX.Max((int)_threatHysteresis.CurrentLevel, 1),   // 受击至少警戒
                intensity: intensity,
                expiry: currentTime + _config.traceExpiry);
            _attention.AddStimulus(trace);
        }

        // LOD 第二层：感知范围外但有战斗热点 -> 朝热点移动支援（TaskStimulus；NPCBrain.cs:399-413）
        if (_nearbyEnemies.Count == 0 && _world.TryGetHotspot(myPos, _config.traceDecayTime, out var hotspot))
        {
            float dist = Vector2X.Distance(myPos, hotspot);
            if (dist > perceptionWorld)
            {
                _attention.AddStimulus(new TaskStimulus(
                    TaskPriority.C,
                    targetPos: hotspot,
                    intensity: _config.hotspotSupportIntensity,
                    expiry: currentTime + _config.traceDecayTime,
                    issuer: _world));
            }
        }

        if (_nearbyEnemies.Count == 0) _nearestDist = float.MaxValue;
    }

    // ===== Think 管线（04 §二 第 3 步，每 tick；v0 不分片）=====

    public void ThinkCore()
    {
        // 记忆组件 dt 用真实间隔（NPCBrain.cs:423-425；sim 全活跃 dt=0.1）
        float currentTime = _clock.Now;
        float dt = _lastThinkTime > 0f ? MathfX.Max(0f, currentTime - _lastThinkTime) : 0.1f;
        _lastThinkTime = currentTime;

        // ⓪ 组装 FactorContext
        FactorContext ctx = BuildBaseContext();
        ctx.LastRaw = _lastRaw;
        ctx.ArrivedAtFocus = Executor.ArrivedAtFocus;

        // ① 记忆组件 Tick（量化器读 ctx.LastRaw 上一帧缓存）
        for (int i = 0; i < _memoryComponents.Length; i++)
            _memoryComponents[i].Tick(dt, in ctx);

        // ② FillContext + 收集动态刺激源入 L1 评分池
        for (int i = 0; i < _memoryComponents.Length; i++)
            _memoryComponents[i].FillContext(ref ctx);

        _attention.ClearDynamicStimuli();
        _attention.AddStimulus(GetOrUpdateSafety(in ctx));
        if (FollowIsActive)
            _attention.AddStimulus(_followStimulus);
        _attention.AddStimulus(GetOrUpdateWander(in ctx));
        for (int i = 0; i < _memoryComponents.Length; i++)
        {
            var stimuli = _memoryComponents[i].GetActiveStimuli();
            for (int j = 0; j < stimuli.Count; j++)
                _attention.AddDynamicStimulus(stimuli[j]);
        }

        _attention.SetTaskDiscount(ctx.StateTaskDiscount);
        _attention.SetBreakContext(_self.Profession.courage, _self.Profession.obedience);

        // ③ 纯管线：L1 -> 因子补算 -> L2 -> rawFactor -> L3（NPCBrain.cs:465-484）
        _attention.Update(currentTime, dt);
        ctx.FocusDecision = L1FocusEvaluator.Evaluate(_attention.CurrentFocus, _attention.CurrentStimulus, in ctx);
        ctx.AbandonTaskFactor = ComputeAbandonTaskFactor(in ctx);
        ctx.WorkFactor = ComputeWorkFactor(in ctx);
        ctx.PostureDecision = L2PostureDecider.Decide(in ctx);

        ctx.RawFactor = ThreatAssessor.CalculateRawFactor(
            ctx.NearestEnemyDist, ctx.NearbyEnemyCount, ctx.HpRatio, ctx.NearbyAllyCount,
            ctx.IsNight, ctx.Profession, ctx.Config, ctx.PerceptionWorldRadius, ctx.AttackWorldRange,
            ctx.RegionHeat);
        _lastRaw = ctx.RawFactor;

        LastCmd = L3CommandComputer.Compute(in ctx.PostureDecision, in ctx);
        _lastCtx = ctx;
    }

    /// <summary>攻击注册/注销（04 §二 第 4 步，复刻 NPCBrain.UpdateCombatRegistration）。</summary>
    public void UpdateCombatRegistration()
    {
        FactorContext ctx = _lastCtx;
        FocusDecision focus = ctx.FocusDecision;
        bool shouldAttack = false;
        SimUnit targetEnemy = null;

        if (_self.Profession.attack > 0)   // 战斗单位才攻击
        {
            // 路径1：威胁焦点 + 在射程内
            if (focus.IsValid && focus.Focus is ThreatStimulus ts
                && ts.Enemy != null && ts.Enemy.IsAlive && ts.Enemy.CurrentHp > 0)
            {
                float dist = Vector2X.Distance(ctx.SelfPos, ts.Enemy.Position);
                if (dist <= ctx.AttackWorldRange)
                {
                    shouldAttack = true;
                    targetEnemy = ts.Enemy as SimUnit;
                }
            }
            // 路径2：编队跟随或无威胁焦点时，感知范围内最近敌人在射程内也开火
            if (!shouldAttack && _nearbyEnemies.Count > 0)
            {
                IUnitHandle nearest = null;
                float nearestDist = float.MaxValue;
                for (int i = 0; i < _nearbyEnemies.Count; i++)
                {
                    var e = _nearbyEnemies[i];
                    if (e == null || !e.IsAlive || e.CurrentHp <= 0) continue;
                    float d = Vector2X.Distance(ctx.SelfPos, e.Position);
                    if (d < nearestDist) { nearestDist = d; nearest = e; }
                }
                if (nearest != null && nearestDist <= ctx.AttackWorldRange)
                {
                    shouldAttack = true;
                    targetEnemy = nearest as SimUnit;
                }
            }
        }

        if (shouldAttack)
        {
            if (!ReferenceEquals(targetEnemy, _currentAttackTarget))
            {
                StopAttacking();
                var profile = new SimAttackProfile
                {
                    attack = _self.Profession.attack,
                    range = _self.Profession.attackRange,
                    cd = _self.Profession.attackCD,
                    isRanged = _self.Profession.isRanged,
                    projectileSpeed = _self.Profession.projectileSpeed,
                };
                if (_damage != null && _damage.RegisterAttack(_self, targetEnemy, in profile))
                    _currentAttackTarget = targetEnemy;
            }
        }
        else
        {
            StopAttacking();
        }
    }

    private void StopAttacking()
    {
        if (_currentAttackTarget != null)
        {
            _damage?.Unregister(_self);
            _currentAttackTarget = null;
        }
    }

    // ===== 受击事件（事件驱动路径 b：HitCooldown + 受击溯源；NPCBrain.OnDamaged）=====

    public void OnDamaged(SimUnit source)
    {
        var ctx = BuildBaseContext();
        _hitCooldown.OnDamaged(in ctx);

        // 受击溯源：敌对攻击者才记录（环境伤害/友军误伤只计数不记攻击者）
        if (source == null) return;
        if (source.Faction == Faction.None || source.Faction == _self.Faction) return;
        _lastAggressor = source;
        _recentHitCount++;
        _lastHitTime = _clock.Now;
    }

    // ===== Executor 事件回调（IExecutorEventReceiver 语义，NPCBrain.OnArrived/OnMoveComplete/OnAnchorLost）=====

    public void OnArrived(Vector2X position, BehaviorModule fromModule)
    {
        // sim 无 EventBus 辅助转发（壳 Publish ExecutorArrivedEvent 供调度/调试，sim 从简）
    }

    public void OnMoveComplete(Vector2X position)
    {
        // 撤退完成 -> HitCooldownStateMachine Caution 计时起点（§13.3 关键）
        _hitCooldown.OnMoveComplete();
    }

    public void OnAnchorLost()
    {
        // 跟随锚点死亡 -> 清除 FollowStimulus
        ClearFollowAnchor();
    }

    // ===== 军令接口（FormationController.DispatchOrders 调用）=====

    /// <summary>设置编队槽位（对应 NPCBrain.SetFormationSlot；锚点=将军 SimUnit）。</summary>
    public void SetFormationSlot(SimUnit anchor, TaskPriority priority, float intensity,
                                 Vector2IntX slotOffset, bool royalCommand = false)
    {
        _followStimulus.Anchor = anchor;
        _followStimulus.Priority = priority;
        _followStimulus.Intensity = intensity;
        _followStimulus.SlotOffset = slotOffset;
        _followStimulus.IsRoyalCommand = royalCommand;
    }

    /// <summary>设置跟随锚点（对应 NPCBrain.SetFollowAnchor）。</summary>
    public void SetFollowAnchor(SimUnit anchor, TaskPriority priority, float intensity)
    {
        _followStimulus.Anchor = anchor;
        _followStimulus.Priority = priority;
        _followStimulus.Intensity = intensity;
    }

    /// <summary>清除跟随锚点/编队槽位（对应 NPCBrain.ClearFollowAnchor/ClearFormationSlot）。</summary>
    public void ClearFollowAnchor()
    {
        _followStimulus.Anchor = null;
        _followStimulus.SlotOffset = Vector2IntX.zero;
    }

    // ===== FactorContext 组装（复刻 NPCBrain.BuildBaseContext）=====

    private FactorContext BuildBaseContext()
    {
        float hpRatio = _self.MaxHp > 0 ? (float)_self.CurrentHp / _self.MaxHp : 0f;
        float cellSize = _world.CellSize;
        return new FactorContext
        {
            Self = _self,
            Profession = _self.Profession,
            Config = _config,
            SelfPos = _self.Position,
            HpRatio = hpRatio,
            IsNight = false,                 // sim 恒白天（无 TimeManager）
            NightFactor = 0f,
            NearbyEnemyCount = _nearbyEnemies.Count,
            NearbyAllyCount = _nearbyAllies.Count,
            NearestEnemyDist = _nearestDist,
            PerceptionWorldRadius = _self.Profession.perceptionRadius * cellSize,
            AttackWorldRange = _self.Profession.attackRange * cellSize,
            CellSize = cellSize,
            CurrentTime = _clock.Now,
            HomePoint = _self.HomePoint,
            HasFormationSlot = HasFormationSlot,
            FormationSlotWorld = ResolveFormationSlotWorld(),
            RegionHeat = _world.GetHeatAt(_self.Position),
            ThreatFactor = _lastRaw,
            FormationFactor = FollowIsActive
                ? MathfX.Clamp01(_followStimulus.Intensity / _config.formationOrderIntensity)
                : 0f,
            SafetyFactor = ComputeSafetyFactor(hpRatio, _self.HomePoint),
        };
    }

    /// <summary>归巢因子（3.0.1_8 §五；sim 恒白天，nightFactor=0 项消掉）。</summary>
    private float ComputeSafetyFactor(float hpRatio, Vector2X homePoint)
    {
        float perceptionWorld = MathfX.Max(1f, _self.Profession.perceptionRadius * _world.CellSize);
        float distFactor = MathfX.Clamp01(Vector2X.Distance(_self.Position, homePoint) / (perceptionWorld * 2f));
        float wound = 1f - hpRatio;
        return MathfX.Clamp01(
            distFactor * _config.safetyDistWeight
            + wound * _config.safetyWoundWeight);
    }

    /// <summary>工作因子（3.0.1_8 §八；复刻 NPCBrain.ComputeWorkFactor）。</summary>
    private float ComputeWorkFactor(in FactorContext ctx)
    {
        if (!ctx.FocusDecision.IsValid || !(ctx.FocusDecision.Focus is TaskStimulus ts))
            return 0f;
        return MathfX.Clamp01(GetPriorityWeight(ts.Priority) / MathfX.Max(1f, _config.priorityWeightS));
    }

    private float GetPriorityWeight(TaskPriority p)
    {
        switch (p)
        {
            case TaskPriority.S: return _config.priorityWeightS;
            case TaskPriority.A: return _config.priorityWeightA;
            case TaskPriority.B: return _config.priorityWeightB;
            default: return _config.priorityWeightC;
        }
    }

    /// <summary>放弃任务因子（3.0.1_8 §六；复刻 NPCBrain.ComputeAbandonTaskFactor）。
    /// 非追击中恒 0；目标切换重置计时。persist 因子读 SimUnit.Profession 快照（壳读 UnitController.Data）。</summary>
    private float ComputeAbandonTaskFactor(in FactorContext ctx)
    {
        IUnitHandle target = null;
        if (_self.Profession.attack > 0
            && ctx.FocusDecision.IsValid && ctx.FocusDecision.Focus is ThreatStimulus ts
            && ts.Enemy != null && ts.Enemy.IsAlive)
        {
            target = ts.Enemy;
        }

        if (target == null)
        {
            _chaseTarget = null;
            _lastChaseDist = 0f;
            return 0f;
        }

        // 君主令：收益封顶，永不弃任务
        if (FollowIsActive && _followStimulus.IsRoyalCommand)
            return 0f;

        // 目标切换 -> 重置追击计时
        if (!ReferenceEquals(_chaseTarget, target))
        {
            _chaseTarget = target;
            _chaseStartTime = _clock.Now;
            _lastChaseDist = 0f;
        }

        float chaseTime = _clock.Now - _chaseStartTime;
        float distNow = Vector2X.Distance(ctx.SelfPos, target.Position);
        bool distGrow = _lastChaseDist > 0f && distNow > _lastChaseDist * _config.abandonDistGrowRatio;
        _lastChaseDist = distNow;

        float targetHpRatio = target.MaxHp > 0 ? (float)target.CurrentHp / target.MaxHp : 0f;

        // 收益（正分）
        float benefit = 0f;
        if (targetHpRatio < _config.abandonKillHpGate)
            benefit += _config.abandonBenefitKillable;
        benefit += ctx.FormationFactor * _config.abandonBenefitOrder;

        // 坚持任务因子（读目标职业快照）
        var targetUnit = target as SimUnit;
        if (_self.Profession.attack >= _config.persistPowerAttackGate)
            benefit += _config.persistBenefitPower;
        if (targetUnit != null
            && (_self.Profession.attack - targetUnit.Profession.defense) >= _config.persistDamageMargin)
            benefit += _config.persistBenefitWeakDefense;
        if (targetUnit != null
            && _self.Profession.walkSpeed > targetUnit.Profession.walkSpeed * _config.persistSpeedRatio)
            benefit += _config.persistBenefitSpeed;

        // 成本（负分）
        float cost = 0f;
        if (ctx.HpRatio < _config.abandonWoundedHpGate)
            cost += _config.abandonCostWounded;
        if (ctx.NearbyAllyCount <= _config.abandonAloneGate)
            cost += _config.abandonCostAlone;
        if (chaseTime > _config.abandonTimeout)
            cost += _config.abandonCostTimeout;
        if (distGrow)
            cost += _config.abandonCostDistance;

        return MathfX.Clamp01(cost - benefit);
    }

    /// <summary>解析当前编队槽位世界坐标（锚点位置 + SlotOffset × cellSize；NPCBrain.ResolveFormationSlotWorld）。</summary>
    public Vector2X ResolveFormationSlotWorld()
    {
        if (!FollowIsActive) return Vector2X.zero;
        if (!_followStimulus.IsFormationSlot || _followStimulus.Anchor == null) return Vector2X.zero;
        float cs = _world.CellSize;
        return _followStimulus.Anchor.Position
            + new Vector2X(_followStimulus.SlotOffset.x * cs, _followStimulus.SlotOffset.y * cs);
    }

    // ===== 动态刺激源 Provider（壳 Provider 逻辑内联复刻）=====

    private SafetyStimulus GetOrUpdateSafety(in FactorContext ctx)
    {
        _safetyStimulus.Position = ctx.HomePoint;
        // 到家判定：位置距离 <= arrivalThreshold × cellSize -> 强度压 0（Wander 浮出）
        bool atHome = Vector2X.Distance(_safetyStimulus.Position, ctx.SelfPos)
                      <= ctx.Config.arrivalThreshold * ctx.CellSize;
        if (atHome)
        {
            _safetyStimulus.Intensity = 0f;
        }
        else
        {
            _safetyStimulus.Intensity = RetreatFormulas.SafetyUrge(
                ctx.Config.baseSafetyPull,
                ctx.NightFactor,
                ctx.Config.nightPullWeight,
                ctx.HpRatio,
                ctx.Config.woundPullWeight,
                ctx.Profession.professionPullScale);
        }
        return _safetyStimulus;
    }

    private WanderStimulus GetOrUpdateWander(in FactorContext ctx)
    {
        _wanderStimulus.Position = ctx.HomePoint;
        _wanderStimulus.Intensity = ctx.Config.wanderIntensity;
        return _wanderStimulus;
    }

    // ===== 谱系切换历史（日志 spectrum 事件用；复刻 NPCBrain.RecordSwitchHistory 语义）=====

    private Focus _lastRecordedFocus;
    private BehaviorSpectrum _lastRecordedSpectrum;

    /// <summary>记录切换历史（焦点或谱系变化返回 true，供 SimWorld 发 spectrum 事件）。</summary>
    public bool RecordSwitchHistory()
    {
        Focus curFocus = _attention.CurrentFocus;
        BehaviorSpectrum curSpectrum = _lastCtx.PostureDecision.Spectrum;

        bool focusChanged = !FocusEquals(_lastRecordedFocus, curFocus);
        bool spectrumChanged = _lastRecordedSpectrum != curSpectrum;

        if (focusChanged || spectrumChanged)
        {
            _lastRecordedFocus = curFocus;
            _lastRecordedSpectrum = curSpectrum;
            return true;
        }
        return false;
    }

    private static bool FocusEquals(Focus a, Focus b)
    {
        if (!a.IsValid && !b.IsValid) return true;
        if (a.IsValid != b.IsValid) return false;
        return a.Layer == b.Layer && ReferenceEquals(a.Source, b.Source);
    }
}
