using System;
using System.Collections.Generic;

// ============================================================================
//  M1 控制台 harness smoke test
//  详见 03_大脑提取与双适配工程.md §六 步6 + 06_执行计划与验收.md §M1
//  手喂 FactorContext（含快照字段）-> 调 L1/L2/L3 -> 打印输出。
//  AI.Core 源码经 harness.csproj <Compile Include> 链接（同一批源码，非复制）。
//  假单位/假世界为内联最小适配器（正式 SimUnit/SimWorld 于 M2 建）。
// ============================================================================

/// <summary>M1 smoke test 假单位（IUnitHandle 最小实现；正式 SimUnit 适配器 M2 建）</summary>
public sealed class FakeUnit : IUnitHandle
{
    private readonly ProfessionSnapshot _prof;
    public Vector2X Position { get; set; }
    public Faction Faction => Faction.Undead;
    public bool IsAlive => true;
    public int CurrentHp => 100;
    public int MaxHp => 100;
    public int Attack => 10;
    public int Defense => 0;
    public float WalkSpeed => 5f;
    public ProfessionSnapshot Profession => _prof;

    public FakeUnit(Vector2X pos, ProfessionSnapshot prof)
    {
        Position = pos;
        _prof = prof;
    }
}

/// <summary>M1 smoke test 假世界（IWorldQuery 最小实现；正式 SimWorld 适配器 M2 建）</summary>
public sealed class FakeWorld : IWorldQuery
{
    public float CellSize => 2.26f;
    public float GetHeatAt(Vector2X pos) => 0f;
    public bool TryGetHotspot(Vector2X pos, float maxAge, out Vector2X hotspot)
    {
        hotspot = Vector2X.zero;
        return false;
    }
    public void QueryUnitsInCell(int cx, int cy, List<IUnitHandle> results) { }
}

/// <summary>M1 smoke test 入口。</summary>
public static class Program
{
    public static void Main()
    {
        var config = BuildConfig();
        var prof = BuildProfession();

        Console.WriteLine("=== M1 决策核 smoke test ===");
        Console.WriteLine("快照字段：retreatThresholdBase=" + config.retreatThresholdBase
            + " courage=" + prof.courage
            + " l2FullPowerThreatGate=" + config.l2FullPowerThreatGate);
        ScenarioThreat(config, prof);
        ScenarioTask(config, prof);
        Console.WriteLine("=== smoke test 完成 ===");
    }

    // ===== 场景 A：威胁近身 -> 谱系 4 战术短撤 =====

    private static void ScenarioThreat(TuningSnapshot config, ProfessionSnapshot prof)
    {
        var attention = new AttentionSystem();
        attention.SetConfig(config);
        attention.SetWorldQuery(new FakeWorld());

        var enemy = new FakeUnit(new Vector2X(15f, -3f), prof);
        attention.AddStimulus(new ThreatStimulus(enemy, threatLevel: 1, intensity: 60f, expiry: float.MaxValue));
        attention.Update(currentTime: 10f, dt: 0.1f);

        var ctx = new FactorContext
        {
            Profession = prof,
            Config = config,
            SelfPos = new Vector2X(10f, -3f),
            HpRatio = 1f,
            IsNight = false,
            NightFactor = 0f,
            NearbyEnemyCount = 1,
            NearbyAllyCount = 0,
            NearestEnemyDist = 5f,
            PerceptionWorldRadius = 5f * 2.26f,
            AttackWorldRange = 1f * 2.26f,
            CellSize = 2.26f,
            CurrentTime = 10f,
            HomePoint = new Vector2X(0f, -3f),
            ArrivedAtFocus = false,
            RegionHeat = 0f,
            ThreatFactor = 0.7f,
            FormationFactor = 0f,
            SafetyFactor = 0f,
            AbandonTaskFactor = 0f,
            WorkFactor = 0f,
            CurrentState = HitCooldownState.Normal,
            HitCount = 0,
            EffectiveSensitivity = 1f,
            ThreatLevel = ThreatLevel.Alert,
            HasProtection = false,
            HasFormationSlot = false,
        };

        var fd = L1FocusEvaluator.Evaluate(attention.CurrentFocus, attention.CurrentStimulus, in ctx);
        ctx.FocusDecision = fd;
        var pd = L2PostureDecider.Decide(in ctx);
        ctx.PostureDecision = pd;
        var cmd = L3CommandComputer.Compute(in pd, in ctx);

        Console.WriteLine("[威胁场景] L1 focus=" + fd.Type + " valid=" + fd.IsValid + " score=" + fd.Score.ToString("F2"));
        Console.WriteLine("[威胁场景] L2 spectrum=" + pd.Spectrum + " module=" + pd.Module + " tactical=" + pd.IsTacticalRetreat);
        Console.WriteLine("[威胁场景] L3 module=" + cmd.Module
            + " dir=(" + cmd.Direction.x.ToString("F3") + "," + cmd.Direction.y.ToString("F3") + ")"
            + " dist=" + cmd.Distance.ToString("F2")
            + " speed=" + cmd.Speed.ToString("F2")
            + " target=(" + cmd.TargetPos.x.ToString("F2") + "," + cmd.TargetPos.y.ToString("F2") + ")");
    }

    // ===== 场景 B：工作任务（B 级，未到达）-> 谱系 0 MoveTowards =====

    private static void ScenarioTask(TuningSnapshot config, ProfessionSnapshot prof)
    {
        var attention = new AttentionSystem();
        attention.SetConfig(config);
        attention.SetWorldQuery(new FakeWorld());

        attention.AddStimulus(new TaskStimulus(
            TaskPriority.B, new Vector2X(20f, -3f), intensity: 2f, expiry: float.MaxValue, issuer: null));
        attention.Update(currentTime: 10f, dt: 0.1f);

        var ctx = new FactorContext
        {
            Profession = prof,
            Config = config,
            SelfPos = new Vector2X(10f, -3f),
            HpRatio = 1f,
            IsNight = false,
            NightFactor = 0f,
            NearbyEnemyCount = 0,
            NearbyAllyCount = 0,
            NearestEnemyDist = float.MaxValue,
            PerceptionWorldRadius = 5f * 2.26f,
            AttackWorldRange = 1f * 2.26f,
            CellSize = 2.26f,
            CurrentTime = 10f,
            HomePoint = new Vector2X(0f, -3f),
            ArrivedAtFocus = false,
            RegionHeat = 0f,
            ThreatFactor = 0.1f,
            FormationFactor = 0f,
            SafetyFactor = 0f,
            AbandonTaskFactor = 0f,
            WorkFactor = 0.5f,
            CurrentState = HitCooldownState.Normal,
            HitCount = 0,
            EffectiveSensitivity = 1f,
            ThreatLevel = ThreatLevel.None,
            HasProtection = false,
            HasFormationSlot = false,
        };

        var fd = L1FocusEvaluator.Evaluate(attention.CurrentFocus, attention.CurrentStimulus, in ctx);
        ctx.FocusDecision = fd;
        var pd = L2PostureDecider.Decide(in ctx);
        ctx.PostureDecision = pd;
        var cmd = L3CommandComputer.Compute(in pd, in ctx);

        Console.WriteLine("[任务场景] L1 focus=" + fd.Type + " valid=" + fd.IsValid + " score=" + fd.Score.ToString("F2"));
        Console.WriteLine("[任务场景] L2 spectrum=" + pd.Spectrum + " module=" + pd.Module);
        Console.WriteLine("[任务场景] L3 module=" + cmd.Module
            + " target=(" + cmd.TargetPos.x.ToString("F2") + "," + cmd.TargetPos.y.ToString("F2") + ")"
            + " speed=" + cmd.Speed.ToString("F2"));
    }

    // ===== 快照构造：默认值对齐 AttentionTuningConfig / NpcProfessionDef（03 §三 一致性关键）=====

    private static TuningSnapshot BuildConfig()
    {
        return new TuningSnapshot
        {
            priorityWeightS = 4f, priorityWeightA = 3f, priorityWeightB = 2f, priorityWeightC = 1f,
            retreatThresholdBase = 2f,
            threatUpgradeConfirmTime = 0.3f, threatDowngradeConfirmTime = 0.5f,
            protectionFriendThreshold = 3, protectionLossThreshold = 1,
            threatDecayTime = 3f,
            scheduleRetryInterval = 3f, scheduleRecruitRadiusCells = 3, scheduleShardCount = 5, scheduleShardInterval = 0.1f,
            perceptionUpdateInterval = 0.2f,
            baseSafetyPull = 0.5f, nightPullWeight = 2f, woundPullWeight = 1f,
            baseFollowCells = 2, followScatterWeight = 0.5f,
            holdPositionIntensity = 0.6f, stateTaskDiscount = 0.3f, stateThreatBias = 0.3f,
            baseCautionTime = 5f, baseRecoveryTime = 10f, probeSensitivityBoost = 1.5f,
            baseRetreatCells = 2, stepRetreatCells = 1.5f,
            threatUpThresholds = new float[] { 0.25f, 0.5f, 0.75f },
            threatDownThresholds = new float[] { 0.15f, 0.4f, 0.65f },
            protectionUpThresholds = new int[] { 3 },
            protectionDownThresholds = new int[] { 1 },
            arrivalThreshold = 0.3f,
            thinkShardCount = 5,
            formationChaseRangeCells = 2f, formationSwitchDebounce = 1f, formationCasualtyDebounce = 1f,
            breakThreshold = 1f, breakReleaseThreshold = 0.7f, breakLevelWeight = 0.5f, breakConfirmUp = 0.3f, breakConfirmDown = 0.5f,
            traceBaseIntensity = 40f, traceStepIntensity = 10f, traceMaxIntensity = 60f, traceDecayTime = 3f, traceExpiry = 5f,
            wanderIntensity = 0.05f, wanderStayTime = 1.5f,
            rfDistWeight = 0.35f, rfCountWeight = 0.15f, rfHpWeight = 0.2f, rfAllyWeight = 0.2f, rfTimeWeight = 0.1f, rfHeatWeight = 0.5f,
            safetyDistWeight = 0.4f, safetyNightWeight = 0.35f, safetyWoundWeight = 0.45f, safetyRetreatGate = 0.3f,
            abandonThreshold = 0.5f, abandonBenefitKillable = 0.6f, abandonBenefitOrder = 0.4f,
            abandonCostWounded = 0.4f, abandonCostAlone = 0.3f, abandonCostTimeout = 0.4f, abandonCostDistance = 0.3f,
            abandonTimeout = 6f, abandonKillHpGate = 0.5f, abandonWoundedHpGate = 0.6f, abandonAloneGate = 0f, abandonDistGrowRatio = 1.2f,
            persistPowerAttackGate = 12f, persistBenefitPower = 0.3f, persistDamageMargin = 0f, persistBenefitWeakDefense = 0.2f,
            persistSpeedRatio = 1.1f, persistBenefitSpeed = 0.3f,
            workResistScale = 0.5f,
            l2FullPowerThreatGate = 0.3f, l2ResistBase = 0.5f, l2FormationResistScale = 0.4f, l2ResistCap = 0.95f,
            thresholdScaleDivisor = 3f, thresholdMinFloor = 0.2f,
            taskValueGarrison = 0.5f, taskValueAttack = 0.8f, taskValuePatrol = 0.2f, chargeValueGate = 0.6f,
            taskValueHeatBoostGate = 0.5f, taskValueHeatBoost = 0.2f, taskValueSurvivalPenalty = 0.3f,
            threatIntensityMax = 100f, countFactorFullCount = 5f, closeRangeMinRaw = 0.5f, hotspotSupportIntensity = 0.6f,
            formationOrderIntensity = 4.5f, formationOrderBoost = 6f, formationOrderBoostDuration = 1f,
            lodActiveThinkHz = 10f, lodSemiThinkHz = 2f, lodSleepThinkHz = 0.5f, lodActiveRadius = 1, lodSemiRadius = 2, lodDowngradeIdleTime = 30f,
            heatHitGain = 0.4f, heatEnemyEnterGain = 0.2f, heatAllyRetreatGain = 0.05f, heatDecayRate = 0.05f,
            heatSpreadThreshold = 0.6f, heatSpreadRatio = 0.4f,
        };
    }

    private static ProfessionSnapshot BuildProfession()
    {
        return new ProfessionSnapshot
        {
            faction = Faction.Human_Player,
            walkSpeed = 5f, runSpeed = 10f, maxHp = 100, attack = 10, defense = 0,
            attackRange = 1f, attackCD = 1f, isRanged = false, projectileSpeed = 25f,
            perceptionRadius = 5f, threatSensitivity = 1f, courage = 50, obedience = 50,
            retreatThresholdOffset = 0f, maxHitCount = 3, professionPullScale = 1f,
            equipmentSlotCount = 0, wanderRadiusCells = 2f,
        };
    }
}
