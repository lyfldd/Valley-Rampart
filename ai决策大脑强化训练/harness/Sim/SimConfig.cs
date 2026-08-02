// ============================================================================
//  M2 Headless 模拟器 - SimConfig 全局配置（防硬编码魔法数值）
//  数值对齐 Unity 侧真值资产（单源真值在 Unity 资产，此处机械拷贝）：
//    - TuningSnapshot 默认值 = Resources/Config/AttentionTuningConfig.asset（字段逐一对应）
//    - Damage 参数        = Resources/Config/DamageConfig.asset（armorK=100/tickInterval=0.1/
//                           maxAttacksPerFrame=100/overkillLimit=2/eventThrottle=0.5）
//    - cellSize           = Resources/Grid/GridConfig.asset（2.26）
//    - 职业库             = Resources/UnitData/Human_Player_Warrior|Archer / Undead_Warrior|Archer
//  场景 JSON（Scenarios/*.json）可覆盖 / 增补，不改动本文件即可调参。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 模拟器全局配置（每局实例）。
/// 组合：决策核 TuningSnapshot + 伤害规则 + 世界常量 + 职业库。
/// </summary>
public sealed class SimConfig
{
    // ===== 世界常量（GridConfig.asset 真值）=====
    public float cellSize = 2.26f;
    public int regionCellCount = 16;      // 大区块 cell 数（SimGrid region 索引用）
    public int midRegionCellCount = 4;    // 中区块 cell 数（热度聚合粒度，LODSystem 真值）
    public int cellStackLimit = 12;       // 单位堆叠上限（GridConfig.stackLimits category0）

    // ===== 伤害规则（DamageConfig.asset 真值）=====
    public float armorK = 100f;           // 减伤率 = 护甲/(护甲+K)
    public float damageTickInterval = 0.1f; // 时间轮 tick（CD 取整倍数）
    public int maxAttacksPerFrame = 100;  // 每 tick 攻击判定上限（超出推下 tick）
    public int overkillLimit = 2;         // 同一目标最多被多少近战锁定
    public float eventThrottle = 0.5f;    // 受击事件节流（秒）
    public float projectileErrorRadius = 1.5f; // 弹道误差圆（DamageConfig.asset 实为 1.5；
                                            //   v0 hitscan 不消费，报告披露差异，见 04 §四）

    // ===== 同步击杀竞态（决策点 3）=====
    // 默认 Unity 语义：注册即结算（首次立即攻击 + CD 到点即时结算）。
    // S1 验收发现胜率偏离 45-55%（spawn 序遍历引入系统性先手）→ 启用两相结算：
    //   步骤4 注册（不立即结算）/ 步骤5 时间轮统一结算（首发立即攻击保留=注册后下一结算步打）。
    // 两相下"后注册方不丢出手"（目标在第 5 步才结算死亡），先手窗口收窄。
    public bool twoPhaseResolution = true;

    // ===== 决策核调参快照（AttentionTuningConfig.asset 真值，字段逐一对应）=====
    public TuningSnapshot tuning = DefaultTuning();

    // ===== 职业库（Resources/UnitData 资产真值，JSON 可覆盖/增补）=====
    private Dictionary<string, ProfessionSnapshot> _professions;

    /// <summary>职业库（场景 JSON 引用名 -> ProfessionSnapshot）。</summary>
    public IReadOnlyDictionary<string, ProfessionSnapshot> Professions => _professions;

    public SimConfig()
    {
        _professions = BuildDefaultProfessions();
    }

    /// <summary>取职业快照；不存在返回 default（调用方应校验）。</summary>
    public ProfessionSnapshot GetProfession(string name)
    {
        return _professions != null && _professions.TryGetValue(name, out var p) ? p : default;
    }

    /// <summary>注册/覆盖职业（场景 JSON 自定义职业走此入口）。</summary>
    public void RegisterProfession(string name, ProfessionSnapshot prof)
    {
        _professions[name] = prof;
    }

    // ===== 默认职业库（Unity 资产机械拷贝）=====

    private static Dictionary<string, ProfessionSnapshot> BuildDefaultProfessions()
    {
        return new Dictionary<string, ProfessionSnapshot>
        {
            ["Human_Player_Warrior"] = new ProfessionSnapshot
            {
                faction = Faction.Human_Player, walkSpeed = 3f, runSpeed = 6f,
                maxHp = 100, attack = 10, defense = 20,
                attackRange = 1f, attackCD = 1f, isRanged = false, projectileSpeed = 0f,
                perceptionRadius = 8f, threatSensitivity = 0.8f,
                courage = 70, obedience = 70, retreatThresholdOffset = 0.5f,
                maxHitCount = 99, professionPullScale = 0.2f,
                equipmentSlotCount = 0, wanderRadiusCells = 2f,
            },
            ["Human_Player_Archer"] = new ProfessionSnapshot
            {
                faction = Faction.Human_Player, walkSpeed = 3f, runSpeed = 6f,
                maxHp = 80, attack = 8, defense = 10,
                attackRange = 5f, attackCD = 1.5f, isRanged = true, projectileSpeed = 15f,
                perceptionRadius = 8f, threatSensitivity = 0.9f,
                courage = 60, obedience = 60, retreatThresholdOffset = 0.3f,
                maxHitCount = 99, professionPullScale = 0.2f,
                equipmentSlotCount = 0, wanderRadiusCells = 2f,
            },
            ["Undead_Warrior"] = new ProfessionSnapshot
            {
                faction = Faction.Undead, walkSpeed = 3f, runSpeed = 6f,
                maxHp = 80, attack = 13, defense = 20,
                attackRange = 1f, attackCD = 1f, isRanged = false, projectileSpeed = 0f,
                perceptionRadius = 8f, threatSensitivity = 0.6f,
                courage = 85, obedience = 70, retreatThresholdOffset = 1f,
                maxHitCount = 99, professionPullScale = 0.2f,
                equipmentSlotCount = 0, wanderRadiusCells = 2f,
            },
            ["Undead_Archer"] = new ProfessionSnapshot
            {
                faction = Faction.Undead, walkSpeed = 3f, runSpeed = 6f,
                maxHp = 60, attack = 8, defense = 10,
                attackRange = 6f, attackCD = 1.5f, isRanged = true, projectileSpeed = 15f,
                perceptionRadius = 10f, threatSensitivity = 1.2f,
                courage = 50, obedience = 60, retreatThresholdOffset = -0.2f,
                maxHitCount = 99, professionPullScale = 0.2f,
                equipmentSlotCount = 0, wanderRadiusCells = 2f,
            },
        };
    }

    /// <summary>默认调参快照（AttentionTuningConfig.asset 字段逐一对应）。</summary>
    public static TuningSnapshot DefaultTuning()
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
}
