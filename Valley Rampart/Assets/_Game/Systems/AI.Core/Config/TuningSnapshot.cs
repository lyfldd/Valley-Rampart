// ============================================================================
//  AI.Core Config - TuningSnapshot 全局调参快照（接缝 4 的落地）
//  详见 03_大脑提取与双适配工程.md §三 配置快照方案
//  与 AttentionTuningConfig（SO）字段一一对应（机械拷贝）。核内不引用 SO。
//  Unity 侧：AttentionTuningConfig.ToSnapshot() 生成（壳 Data/AttentionTuningConfig.cs）。
//  harness 侧：System.Text.Json 从 tuning.base.json + patch.json 反序列化（M2/M4 落地）。
//  ⚠️ 一致性关键：Unity SO 默认值表 与 harness JSON 默认值表同源，改一处同步。
// ============================================================================

/// <summary>
/// 注意力系统全局调参快照（纯数据，零引擎依赖）。
/// 字段与 AttentionTuningConfig 一一对应（除 GetPriorityWeight 方法在 TaskPriority 入核后补齐）。
/// </summary>
public struct TuningSnapshot
{
    // ===== 任务优先级权重（甲层评分用）=====
    public float priorityWeightS;
    public float priorityWeightA;
    public float priorityWeightB;
    public float priorityWeightC;

    // ===== 撤退阈值（乙层）=====
    public float retreatThresholdBase;

    // ===== 威胁滞回（6.4 输入侧滞回）=====
    public float threatUpgradeConfirmTime;
    public float threatDowngradeConfirmTime;

    // ===== 保护因子（6.4）=====
    public int protectionFriendThreshold;
    public int protectionLossThreshold;

    // ===== 威胁衰减 =====
    public float threatDecayTime;

    // ===== 调度（7.2 中心化任务调度）=====
    public float scheduleRetryInterval;
    public int scheduleRecruitRadiusCells;
    public int scheduleShardCount;
    public float scheduleShardInterval;

    // ===== 感知广播（7.1）=====
    public float perceptionUpdateInterval;

    // ===== 归巢吸引（3.0.1_2 §3.1）=====
    public float baseSafetyPull;
    public float nightPullWeight;
    public float woundPullWeight;

    // ===== 跟随（3.0.1_2 §5.2）=====
    public int baseFollowCells;
    public float followScatterWeight;

    // ===== 受击冷却状态机（3.0.1_2 §13.3）=====
    public float holdPositionIntensity;
    public float stateTaskDiscount;
    public float stateThreatBias;
    public float baseCautionTime;
    public float baseRecoveryTime;
    public float probeSensitivityBoost;

    // ===== 撤退公式（3.0.1_1 §6）=====
    public int baseRetreatCells;
    public float stepRetreatCells;

    // ===== 滞回量化器（3.0.1_2 §10）=====
    public float[] threatUpThresholds;
    public float[] threatDownThresholds;
    public int[] protectionUpThresholds;
    public int[] protectionDownThresholds;

    // ===== 到达判定（3.0.1_2 §4.2）=====
    public float arrivalThreshold;

    // ===== tick 调度（3.0.1_2 §10 / 决策12）=====
    public int thinkShardCount;

    // ===== 编队（3.0.1_3 §4.1 / §5.3 / §9）=====
    public float formationChaseRangeCells;
    public float formationSwitchDebounce;
    public float formationCasualtyDebounce;

    // ===== 破阵博弈（3.0.1_4 §4.3）=====
    public float breakThreshold;
    public float breakReleaseThreshold;
    public float breakLevelWeight;
    public float breakConfirmUp;
    public float breakConfirmDown;

    // ===== 受击溯源（3.0.1_4 §2.3）=====
    public float traceBaseIntensity;
    public float traceStepIntensity;
    public float traceMaxIntensity;
    public float traceDecayTime;
    public float traceExpiry;

    // ===== 漫游（3.0.1_4 §6.3）=====
    public float wanderIntensity;
    public float wanderStayTime;

    // ===== rawFactor 权重（3.0.1_LOD §3.2 统一因子表）=====
    public float rfDistWeight;
    public float rfCountWeight;
    public float rfHpWeight;
    public float rfAllyWeight;
    public float rfTimeWeight;
    public float rfHeatWeight;

    // ===== 归巢因子（3.0.1_8 §五）=====
    public float safetyDistWeight;
    public float safetyNightWeight;
    public float safetyWoundWeight;
    public float safetyRetreatGate;

    // ===== 放弃任务因子（3.0.1_8 §六）=====
    public float abandonThreshold;
    public float abandonBenefitKillable;
    public float abandonBenefitOrder;
    public float abandonCostWounded;
    public float abandonCostAlone;
    public float abandonCostTimeout;
    public float abandonCostDistance;
    public float abandonTimeout;
    public float abandonKillHpGate;
    public float abandonWoundedHpGate;
    public float abandonAloneGate;
    public float abandonDistGrowRatio;

    // ===== 坚持任务因子（3.0.1_8 §6.6）=====
    public float persistPowerAttackGate;
    public float persistBenefitPower;
    public float persistDamageMargin;
    public float persistBenefitWeakDefense;
    public float persistSpeedRatio;
    public float persistBenefitSpeed;

    // ===== 工作因子（3.0.1_8 §八）=====
    public float workResistScale;

    // ===== L2 连续仲裁常数（3.0.1_8 §五）=====
    public float l2FullPowerThreatGate;
    public float l2ResistBase;
    public float l2FormationResistScale;
    public float l2ResistCap;
    public float thresholdScaleDivisor;
    public float thresholdMinFloor;

    // ===== 军队级任务价值（3.0.1_5 §4.2）=====
    public float taskValueGarrison;
    public float taskValueAttack;
    public float taskValuePatrol;
    public float chargeValueGate;
    public float taskValueHeatBoostGate;
    public float taskValueHeatBoost;
    public float taskValueSurvivalPenalty;

    // ===== 军队级自动意图阈值（M7：FormationBrain 内置大脑入训，原 Inspector 硬编码）=====
    public float fbDecisionInterval;        // 决策间隔（秒，原 FormationBrain.decisionInterval=1）
    public float fbHeatEngage;              // 本地热度≥此值=敌压近（防守/撤退判定线，原 0.3）
    public float fbHeatCharge;              // 本地热度≥此值且价值高=冲锋（原 0.6）
    public float fbSurvivalRetreatGate;     // 残编率<此值且被压=撤退（原 0.4）
    public float fbSupportSearchRadius;     // 支援热点搜索半径（世界单位，原 30 sim=500）
    public float fbHotspotMaxAge;           // 热点有效期（秒，原 5 sim=10）

    // ===== 威胁刺激标定（NPCBrain/ThreatAssessment）=====
    public float threatIntensityMax;
    public float countFactorFullCount;
    public float closeRangeMinRaw;
    public float hotspotSupportIntensity;

    // ===== 编队军令（3.0.1_3 §4.1 / 3.0.1_8 §七）=====
    public float formationOrderIntensity;
    public float formationOrderBoost;
    public float formationOrderBoostDuration;

    // ===== LOD 三区思考（3.0.1_LOD §1.5 / §五）=====
    public float lodActiveThinkHz;
    public float lodSemiThinkHz;
    public float lodSleepThinkHz;
    public int lodActiveRadius;
    public int lodSemiRadius;
    public float lodDowngradeIdleTime;

    // ===== ThreatHeat 区块热度（3.0.1_LOD §3.1 / §3.3）=====
    public float heatHitGain;
    public float heatEnemyEnterGain;
    public float heatAllyRetreatGain;
    public float heatDecayRate;
    public float heatSpreadThreshold;
    public float heatSpreadRatio;
}
