using UnityEngine;

/// <summary>
/// 注意力系统全局调参 SO（3.0.1 第九节占位数值表）。
/// 全局节奏参数住此 SO，职业差异参数住 NpcProfessionDef。
/// Play 模式拖滑块实时看 500 NPC 反应，无需重编译。
///
/// 加载方式：Resources.Load&lt;AttentionTuningConfig&gt;("Config/AttentionTuningConfig")
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/AttentionTuningConfig", fileName = "AttentionTuningConfig")]
public class AttentionTuningConfig : ScriptableObject
{
    [Header("任务优先级权重（甲层评分用）")]
    [Tooltip("S 级任务刺激基础强度")]
    public float priorityWeightS = 4f;
    [Tooltip("A 级任务刺激基础强度")]
    public float priorityWeightA = 3f;
    [Tooltip("B 级任务刺激基础强度")]
    public float priorityWeightB = 2f;
    [Tooltip("C 级任务刺激基础强度")]
    public float priorityWeightC = 1f;

    [Header("撤退阈值（乙层）")]
    [Tooltip("基础撤退阈值（威胁 2 级触发撤退判定）")]
    public float retreatThresholdBase = 2f;

    [Header("威胁滞回（6.4 输入侧滞回）")]
    [Tooltip("威胁升级持续确认时间（秒）")]
    public float threatUpgradeConfirmTime = 0.3f;
    [Tooltip("威胁降级持续确认时间（秒）")]
    public float threatDowngradeConfirmTime = 0.5f;

    [Header("保护因子（6.4）")]
    [Tooltip("友军数达标进入保护（≥此值）")]
    public int protectionFriendThreshold = 3;
    [Tooltip("友军数失效退出保护（<此值）")]
    public int protectionLossThreshold = 1;

    [Header("威胁衰减")]
    [Tooltip("敌人离开后威胁线性衰减到 0 的时间（秒）")]
    public float threatDecayTime = 3f;

    [Header("调度（7.2 中心化任务调度）")]
    [Tooltip("招工请求重试间隔（秒）")]
    public float scheduleRetryInterval = 3f;
    [Tooltip("招募半径（格数）")]
    public int scheduleRecruitRadiusCells = 3;
    [Tooltip("调度分片组数（500 AI 分 N 组轮询）")]
    public int scheduleShardCount = 5;
    [Tooltip("调度分片间隔（秒）")]
    public float scheduleShardInterval = 0.1f;

    [Header("感知广播（7.1）")]
    [Tooltip("感知广播更新间隔（秒）")]
    public float perceptionUpdateInterval = 0.2f;

    [Header("归巢吸引（3.0.1_2 §3.1）")]
    [Tooltip("基础归巢倾向（D 级强度）")]
    public float baseSafetyPull = 0.5f;
    [Tooltip("夜晚归巢放大系数")]
    public float nightPullWeight = 2.0f;
    [Tooltip("受伤归巢放大系数")]
    public float woundPullWeight = 1.0f;

    [Header("跟随（3.0.1_2 §5.2）")]
    [Tooltip("基础跟随格数")]
    public int baseFollowCells = 2;
    [Tooltip("威胁跟随松散度：威胁越高跟得越松")]
    public float followScatterWeight = 0.5f;

    [Header("受击冷却状态机（3.0.1_2 §13.3）")]
    [Tooltip("驻留刺激源强度（Caution 态注入，需 > 折后任务强度。按层内强度 [0,1] 标定；若母文档 0-100 标定需同步放大，P1 统一时再校）")]
    public float holdPositionIntensity = 0.6f;
    [Tooltip("Caution 态任务类刺激折扣（强度 ×此值，让 HoldPosition 胜出）")]
    public float stateTaskDiscount = 0.3f;
    [Tooltip("Caution 态威胁加成（叠加到 rawFactor，目标落威胁 1 区间 [0.25,0.5)，不负责原地）")]
    public float stateThreatBias = 0.3f;
    [Tooltip("基础警戒时长（秒，3.0.1_1 §6.4）")]
    public float baseCautionTime = 5f;
    [Tooltip("基础恢复时长（秒，3.0.1_1 §6.5）")]
    public float baseRecoveryTime = 10f;
    [Tooltip("Probe 态敏感度放大系数")]
    public float probeSensitivityBoost = 1.5f;

    [Header("撤退公式（3.0.1_1 §6）")]
    [Tooltip("基础撤退格数")]
    public int baseRetreatCells = 2;
    [Tooltip("每次受击递增撤退格数")]
    public float stepRetreatCells = 1.5f;

    [Header("滞回量化器（3.0.1_2 §10）")]
    [Tooltip("威胁升阈数组：[0.25,0.5,0.75] -> level 0-3")]
    public float[] threatUpThresholds = { 0.25f, 0.5f, 0.75f };
    [Tooltip("威胁降阈数组：[0.15,0.4,0.65]（升阈>降阈=滞回带）")]
    public float[] threatDownThresholds = { 0.15f, 0.4f, 0.65f };
    [Tooltip("保护升阈数组：[3]（友军数达标进入保护）")]
    public int[] protectionUpThresholds = { 3 };
    [Tooltip("保护降阈数组：[1]（友军数失效退出保护）")]
    public int[] protectionDownThresholds = { 1 };

    [Header("到达判定（3.0.1_2 §4.2）")]
    [Tooltip("到达判定距离（格数）")]
    public float arrivalThreshold = 0.3f;

    [Header("tick 调度（3.0.1_2 §10 / 决策12）")]
    [Tooltip("调度分片组数（500 AI 分 N 组轮询，决策12 P0 内容）")]
    public int thinkShardCount = 5;

    [Header("编队（3.0.1_3 §4.1 / §5.3 / §9）")]
    [Tooltip("守阵追击限制（格数，§4.1：威胁层压制切 MoveTowards 时，目标钳制在槽位 ± 此值内，占位 2 cell）")]
    public float formationChaseRangeCells = 2f;
    [Tooltip("阵型切换确认时长（秒，§5.3 将军决策防抖，占位 1s）")]
    public float formationSwitchDebounce = 1f;
    [Tooltip("减员重排防抖（秒，§15.3 即时触发+防抖，占位 1s）")]
    public float formationCasualtyDebounce = 1f;

    [Header("破阵博弈（3.0.1_4 §4.3）")]
    [Tooltip("破阵升阈：breakScore 持续 > 此值进入破阵（威胁层胜出）")]
    public float breakThreshold = 1.0f;
    [Tooltip("破阵降阈：breakScore 持续 < 此值归队（滞回带内保持当前状态）")]
    public float breakReleaseThreshold = 0.7f;
    [Tooltip("威胁等级加成权重（levelWeight）：1+threatLevel×此值，默认 0.5")]
    public float breakLevelWeight = 0.5f;
    [Tooltip("破阵升级确认（秒，持续超过升阈的时间）")]
    public float breakConfirmUp = 0.3f;
    [Tooltip("破阵降级确认（秒，持续低于降阈的时间）")]
    public float breakConfirmDown = 0.5f;

    [Header("受击溯源（3.0.1_4 §2.3）")]
    [Tooltip("溯源基础强度：单次受击的溯源威胁强度")]
    public float traceBaseIntensity = 40f;
    [Tooltip("溯源递增斜率：每次追加的强度")]
    public float traceStepIntensity = 10f;
    [Tooltip("溯源强度上限（必须 < 贴脸近战强度下限 ~90，保证近战优先）")]
    public float traceMaxIntensity = 60f;
    [Tooltip("溯源指数衰减常数（秒，exp(-Δt/此值)，与 threatDecayTime 对齐）")]
    public float traceDecayTime = 3f;
    [Tooltip("溯源刺激有效期（秒，无受击超时移除）")]
    public float traceExpiry = 5f;

    [Header("漫游（3.0.1_4 §6.3）")]
    [Tooltip("Wander 刺激强度（必须 < Safety 未到达最低值 ~0.10，回城优先）")]
    public float wanderIntensity = 0.05f;
    [Tooltip("漫游到点停留时长（秒，走走停停节奏）")]
    public float wanderStayTime = 1.5f;

    [Header("rawFactor 权重（3.0.1_LOD §3.2 统一因子表，入 SO 防硬编码）")]
    [Tooltip("敌人距离因子权重")]
    public float rfDistWeight = 0.35f;
    [Tooltip("敌人数量因子权重")]
    public float rfCountWeight = 0.15f;
    [Tooltip("血量因子权重")]
    public float rfHpWeight = 0.2f;
    [Tooltip("友军保护因子权重")]
    public float rfAllyWeight = 0.2f;
    [Tooltip("昼夜因子权重")]
    public float rfTimeWeight = 0.1f;
    [Tooltip("区块威胁热度因子权重（heatFactor，环境型威胁）。单次受击热度0.4×此值应 ≥0.25 威胁1阈值——危险一次受击就传开。原0.08远不够，0.3 仍偏低，取0.5）")]
    public float rfHeatWeight = 0.5f;

    [Header("归巢因子（3.0.1_8 §五，SafetyFactor 独立化）")]
    [Tooltip("离家权重：distFactor = clamp01(离家距离/(2×感知半径)) × 此值")]
    public float safetyDistWeight = 0.4f;
    [Tooltip("夜晚权重：NightFactor × 此值")]
    public float safetyNightWeight = 0.35f;
    [Tooltip("受伤权重：(1-hpRatio) × 此值")]
    public float safetyWoundWeight = 0.45f;
    [Tooltip("撤退归巢门控：编队成员需 SafetyFactor > 此值才允许撤退（AND 联合语义）")]
    public float safetyRetreatGate = 0.3f;

    [Header("放弃任务因子（3.0.1_8 §六，防风筝无谓追击）")]
    [Tooltip("放弃判定阈值：AbandonTaskFactor > 此值 → 放弃追击回归编队")]
    public float abandonThreshold = 0.5f;
    [Tooltip("可击杀收益：目标血量 < abandonKillHpGate 时 +此值（残血值得追）")]
    public float abandonBenefitKillable = 0.6f;
    [Tooltip("军令收益：FormationFactor × 此值（编队要求追击时降低放弃倾向）")]
    public float abandonBenefitOrder = 0.4f;
    [Tooltip("追击受伤成本：我方血量 < abandonWoundedHpGate 时 +此值")]
    public float abandonCostWounded = 0.4f;
    [Tooltip("孤军成本：周围友军 ≤ abandonAloneGate 时 +此值")]
    public float abandonCostAlone = 0.3f;
    [Tooltip("追击超时成本：持续追击超过 abandonTimeout 秒 +此值")]
    public float abandonCostTimeout = 0.4f;
    [Tooltip("距离拉大成本：当前距离 > 上帧 × abandonDistGrowRatio 时 +此值")]
    public float abandonCostDistance = 0.3f;
    [Tooltip("追击超时阈值（秒）")]
    public float abandonTimeout = 6f;
    [Tooltip("目标血量低于此值视为可击杀")]
    public float abandonKillHpGate = 0.5f;
    [Tooltip("我方血量低于此值视为受伤追击")]
    public float abandonWoundedHpGate = 0.6f;
    [Tooltip("我方友军数 ≤ 此值视为孤军")]
    public float abandonAloneGate = 0;
    [Tooltip("距离增长比：当前/上帧 > 此值视为目标拉大距离")]
    public float abandonDistGrowRatio = 1.2f;

    [Header("坚持任务因子（3.0.1_8 §6.6，收益侧扩充——放弃 vs 坚持的天平）")]
    [Tooltip("装备战力收益门槛：我方 attack ≥ 此值视为打得动（装备系统落地前用基础攻击间接表达）")]
    public float persistPowerAttackGate = 12f;
    [Tooltip("装备战力收益：我方 attack ≥ 门槛时 +此值")]
    public float persistBenefitPower = 0.3f;
    [Tooltip("敌情可击败余量：我方 attack − 目标 defense ≥ 此值视为打得动（相对比较，0=能打出有效伤害即可）")]
    public float persistDamageMargin = 0f;
    [Tooltip("敌情可击败收益：我方打得动目标时 +此值")]
    public float persistBenefitWeakDefense = 0.2f;
    [Tooltip("移速追得上收益：我方 walkSpeed > 敌方 × 此比值才给（真追得上才坚持）")]
    public float persistSpeedRatio = 1.1f;
    [Tooltip("移速追得上收益：满足速度条件时 +此值")]
    public float persistBenefitSpeed = 0.3f;

    [Header("工作因子（3.0.1_8 §八，任务投入抗打断）")]
    [Tooltip("工作抵抗系数：effectiveThreat 削减 = WorkFactor × 此值（半效 0.5，正在干关键活更抗打断）")]
    public float workResistScale = 0.5f;

    [Header("L2 连续仲裁常数（3.0.1_8 §五，防硬编码）")]
    [Tooltip("低威胁→FullPower 警戒线：ThreatFactor < 此值直接全力执行（原硬编码 0.3）")]
    public float l2FullPowerThreatGate = 0.3f;
    [Tooltip("编队抵抗基础系数：formation × (此值 + obedience/100)，原硬编码 0.5")]
    public float l2ResistBase = 0.5f;
    [Tooltip("编队抵抗权重：编队抵抗 × 此值（核心手感参数，原硬编码 0.4）")]
    public float l2FormationResistScale = 0.4f;
    [Tooltip("总抗性上限：编队+工作抵抗总和封顶（防负，原硬编码 0.95）")]
    public float l2ResistCap = 0.95f;
    [Tooltip("撤退阈值量纲除数：档位量纲(0-3) → 连续量纲(0-1) 除以此值（原硬编码 /3）")]
    public float thresholdScaleDivisor = 3f;
    [Tooltip("撤退阈值保底：换算后下限（防除零/过低，原硬编码 0.2）")]
    public float thresholdMinFloor = 0.2f;

    [Header("军队级任务价值（3.0.1_5 §4.2 FormationBrain，防硬编码）")]
    [Tooltip("守城编队任务价值基础值（原硬编码 0.5）")]
    public float taskValueGarrison = 0.5f;
    [Tooltip("攻城编队任务价值基础值（有推进目标，原硬编码 0.8）")]
    public float taskValueAttack = 0.8f;
    [Tooltip("巡逻编队任务价值基础值（无目标待命，原硬编码 0.2）")]
    public float taskValuePatrol = 0.2f;
    [Tooltip("冲锋价值门限：任务价值 > 此值且敌压近才冲锋（原硬编码 0.6）")]
    public float chargeValueGate = 0.6f;
    [Tooltip("敌压近升价值触发线：本地热度 > 此值则任务价值 + 增量（原硬编码 0.5）")]
    public float taskValueHeatBoostGate = 0.5f;
    [Tooltip("敌压近升价值增量（原硬编码 +0.2）")]
    public float taskValueHeatBoost = 0.2f;
    [Tooltip("残编降价值：存活率 < 残编门限时任务价值 − 此值（原硬编码 −0.3）")]
    public float taskValueSurvivalPenalty = 0.3f;

    [Header("威胁刺激标定（NPCBrain/ThreatAssessment，防硬编码）")]
    [Tooltip("威胁刺激强度标定上限（0-100 量纲，贴脸满强度，原硬编码 ×100）")]
    public float threatIntensityMax = 100f;
    [Tooltip("敌人数量因子满编数：enemyCount/此值 归一（原硬编码 /5）")]
    public float countFactorFullCount = 5f;
    [Tooltip("贴脸 rawFactor 保底：敌入攻击距离时 rawFactor 不低于此值（原硬编码 0.5）")]
    public float closeRangeMinRaw = 0.5f;
    [Tooltip("热点支援刺激强度：感知外战斗热点引导支援，>Safety 未到达 0.5 且 <Follow S 级 4.5（原硬编码 0.6）")]
    public float hotspotSupportIntensity = 0.6f;

    [Header("编队军令（3.0.1_3 §4.1 / 3.0.1_8 §七 FormationController，防硬编码）")]
    [Tooltip("军令强度基础（S 级军令，需 > 工作任务 B 级 + 安全归巢 D 级，原硬编码 4.5；同时兼作军令强度归一化基准）")]
    public float formationOrderIntensity = 4.5f;
    [Tooltip("阵型切换瞬时提强度：切阵型瞬间基础→此值保底 duration 秒（原硬编码 6.0）")]
    public float formationOrderBoost = 6f;
    [Tooltip("阵型切换瞬时提强度保底时长（秒，原硬编码 1s）")]
    public float formationOrderBoostDuration = 1f;

    [Header("LOD 三区思考（3.0.1_LOD §1.5 / §五）")]
    [Tooltip("活跃区 Think 频率（Hz，现状 10）")]
    public float lodActiveThinkHz = 10f;
    [Tooltip("半活跃区 Think 频率（Hz）")]
    public float lodSemiThinkHz = 2f;
    [Tooltip("休眠区 Think 频率（Hz）")]
    public float lodSleepThinkHz = 0.5f;
    [Tooltip("活跃带宽度：中心±N region 为活跃")]
    public int lodActiveRadius = 1;
    [Tooltip("活跃带宽度：中心±N region 为半活跃")]
    public int lodSemiRadius = 2;
    [Tooltip("降级条件：热度归零且无事件满此秒数后逐级降")]
    public float lodDowngradeIdleTime = 30f;

    [Header("ThreatHeat 区块热度（3.0.1_LOD §3.1 / §3.3）")]
    [Tooltip("区块内任何 NPC 受击累积热度")]
    public float heatHitGain = 0.4f;
    [Tooltip("敌人进入本 region 累积热度")]
    public float heatEnemyEnterGain = 0.2f;
    [Tooltip("友军撤退经过累积热度")]
    public float heatAllyRetreatGain = 0.05f;
    [Tooltip("热度衰减速率（/秒）")]
    public float heatDecayRate = 0.05f;
    [Tooltip("热度扩散阈值：超过则点燃邻区")]
    public float heatSpreadThreshold = 0.6f;
    [Tooltip("热度扩散系数（邻区获得 热度×此值）")]
    public float heatSpreadRatio = 0.4f;

    /// <summary>按优先级获取权重。</summary>
    public float GetPriorityWeight(TaskPriority priority)
    {
        switch (priority)
        {
            case TaskPriority.S: return priorityWeightS;
            case TaskPriority.A: return priorityWeightA;
            case TaskPriority.B: return priorityWeightB;
            default: return priorityWeightC;
        }
    }

    /// <summary>
    /// 生成核内快照（M1 决策核提取，接缝 4）。
    /// 核内（AI.Core）只吃 TuningSnapshot，不引用本 SO。字段机械拷贝，改字段需同步 TuningSnapshot。
    /// </summary>
    public TuningSnapshot ToSnapshot()
    {
        return new TuningSnapshot
        {
            priorityWeightS = priorityWeightS,
            priorityWeightA = priorityWeightA,
            priorityWeightB = priorityWeightB,
            priorityWeightC = priorityWeightC,
            retreatThresholdBase = retreatThresholdBase,
            threatUpgradeConfirmTime = threatUpgradeConfirmTime,
            threatDowngradeConfirmTime = threatDowngradeConfirmTime,
            protectionFriendThreshold = protectionFriendThreshold,
            protectionLossThreshold = protectionLossThreshold,
            threatDecayTime = threatDecayTime,
            scheduleRetryInterval = scheduleRetryInterval,
            scheduleRecruitRadiusCells = scheduleRecruitRadiusCells,
            scheduleShardCount = scheduleShardCount,
            scheduleShardInterval = scheduleShardInterval,
            perceptionUpdateInterval = perceptionUpdateInterval,
            baseSafetyPull = baseSafetyPull,
            nightPullWeight = nightPullWeight,
            woundPullWeight = woundPullWeight,
            baseFollowCells = baseFollowCells,
            followScatterWeight = followScatterWeight,
            holdPositionIntensity = holdPositionIntensity,
            stateTaskDiscount = stateTaskDiscount,
            stateThreatBias = stateThreatBias,
            baseCautionTime = baseCautionTime,
            baseRecoveryTime = baseRecoveryTime,
            probeSensitivityBoost = probeSensitivityBoost,
            baseRetreatCells = baseRetreatCells,
            stepRetreatCells = stepRetreatCells,
            threatUpThresholds = threatUpThresholds,
            threatDownThresholds = threatDownThresholds,
            protectionUpThresholds = protectionUpThresholds,
            protectionDownThresholds = protectionDownThresholds,
            arrivalThreshold = arrivalThreshold,
            thinkShardCount = thinkShardCount,
            formationChaseRangeCells = formationChaseRangeCells,
            formationSwitchDebounce = formationSwitchDebounce,
            formationCasualtyDebounce = formationCasualtyDebounce,
            breakThreshold = breakThreshold,
            breakReleaseThreshold = breakReleaseThreshold,
            breakLevelWeight = breakLevelWeight,
            breakConfirmUp = breakConfirmUp,
            breakConfirmDown = breakConfirmDown,
            traceBaseIntensity = traceBaseIntensity,
            traceStepIntensity = traceStepIntensity,
            traceMaxIntensity = traceMaxIntensity,
            traceDecayTime = traceDecayTime,
            traceExpiry = traceExpiry,
            wanderIntensity = wanderIntensity,
            wanderStayTime = wanderStayTime,
            rfDistWeight = rfDistWeight,
            rfCountWeight = rfCountWeight,
            rfHpWeight = rfHpWeight,
            rfAllyWeight = rfAllyWeight,
            rfTimeWeight = rfTimeWeight,
            rfHeatWeight = rfHeatWeight,
            safetyDistWeight = safetyDistWeight,
            safetyNightWeight = safetyNightWeight,
            safetyWoundWeight = safetyWoundWeight,
            safetyRetreatGate = safetyRetreatGate,
            abandonThreshold = abandonThreshold,
            abandonBenefitKillable = abandonBenefitKillable,
            abandonBenefitOrder = abandonBenefitOrder,
            abandonCostWounded = abandonCostWounded,
            abandonCostAlone = abandonCostAlone,
            abandonCostTimeout = abandonCostTimeout,
            abandonCostDistance = abandonCostDistance,
            abandonTimeout = abandonTimeout,
            abandonKillHpGate = abandonKillHpGate,
            abandonWoundedHpGate = abandonWoundedHpGate,
            abandonAloneGate = abandonAloneGate,
            abandonDistGrowRatio = abandonDistGrowRatio,
            persistPowerAttackGate = persistPowerAttackGate,
            persistBenefitPower = persistBenefitPower,
            persistDamageMargin = persistDamageMargin,
            persistBenefitWeakDefense = persistBenefitWeakDefense,
            persistSpeedRatio = persistSpeedRatio,
            persistBenefitSpeed = persistBenefitSpeed,
            workResistScale = workResistScale,
            l2FullPowerThreatGate = l2FullPowerThreatGate,
            l2ResistBase = l2ResistBase,
            l2FormationResistScale = l2FormationResistScale,
            l2ResistCap = l2ResistCap,
            thresholdScaleDivisor = thresholdScaleDivisor,
            thresholdMinFloor = thresholdMinFloor,
            taskValueGarrison = taskValueGarrison,
            taskValueAttack = taskValueAttack,
            taskValuePatrol = taskValuePatrol,
            chargeValueGate = chargeValueGate,
            taskValueHeatBoostGate = taskValueHeatBoostGate,
            taskValueHeatBoost = taskValueHeatBoost,
            taskValueSurvivalPenalty = taskValueSurvivalPenalty,
            threatIntensityMax = threatIntensityMax,
            countFactorFullCount = countFactorFullCount,
            closeRangeMinRaw = closeRangeMinRaw,
            hotspotSupportIntensity = hotspotSupportIntensity,
            formationOrderIntensity = formationOrderIntensity,
            formationOrderBoost = formationOrderBoost,
            formationOrderBoostDuration = formationOrderBoostDuration,
            lodActiveThinkHz = lodActiveThinkHz,
            lodSemiThinkHz = lodSemiThinkHz,
            lodSleepThinkHz = lodSleepThinkHz,
            lodActiveRadius = lodActiveRadius,
            lodSemiRadius = lodSemiRadius,
            lodDowngradeIdleTime = lodDowngradeIdleTime,
            heatHitGain = heatHitGain,
            heatEnemyEnterGain = heatEnemyEnterGain,
            heatAllyRetreatGain = heatAllyRetreatGain,
            heatDecayRate = heatDecayRate,
            heatSpreadThreshold = heatSpreadThreshold,
            heatSpreadRatio = heatSpreadRatio,
        };
    }
}
