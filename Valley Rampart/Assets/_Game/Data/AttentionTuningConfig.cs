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
    [Tooltip("S 级任务撤退阈值加成（更难被打断）")]
    public float retreatBonusS = 2f;
    [Tooltip("A 级任务撤退阈值加成")]
    public float retreatBonusA = 1f;
    [Tooltip("B 级任务撤退阈值加成")]
    public float retreatBonusB = 0.5f;
    [Tooltip("C 级任务撤退阈值加成")]
    public float retreatBonusC = 0f;

    [Header("威胁滞回（6.4 输入侧滞回）")]
    [Tooltip("威胁因子 X 升级阈值（1->2/2->3 升级：X 持续 > 此值）")]
    public float threatUpgradeThreshold = 0.6f;
    [Tooltip("威胁因子 X 降级阈值（2->1/3->2 降级：X 持续 < 此值）")]
    public float threatDowngradeThreshold = 0.4f;
    [Tooltip("威胁升级持续确认时间（秒）")]
    public float threatUpgradeConfirmTime = 0.3f;
    [Tooltip("威胁降级持续确认时间（秒）")]
    public float threatDowngradeConfirmTime = 0.5f;

    [Header("保护因子（6.4）")]
    [Tooltip("友军数达标进入保护（≥此值）")]
    public int protectionFriendThreshold = 3;
    [Tooltip("友军数失效退出保护（<此值）")]
    public int protectionLossThreshold = 1;

    [Header("谱系驻留（6.4 状态侧驻留）")]
    [Tooltip("谨慎态最小驻留时间（秒）")]
    public float cautiousMinDwell = 1.0f;
    [Tooltip("撤退态最小驻留时间（秒）")]
    public float retreatMinDwell = 1.5f;
    [Tooltip("撤退到安全区后停留确认时间（秒）")]
    public float safetyConfirmTime = 2.0f;

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

    [Header("姿态速度（3.0.1_2 §4.2 谱系 1 留口）")]
    [Tooltip("警惕态速度系数（谱系 1 留口，P0 未实装）")]
    public float alertSpeedScale = 0.9f;

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
    [Tooltip("军令强度归一化基准（S 级军令值，分母 formationIntensity/此值）")]
    public float breakFormationBase = 4.5f;

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

    /// <summary>按优先级获取撤退阈值加成。</summary>
    public float GetRetreatBonus(TaskPriority priority)
    {
        switch (priority)
        {
            case TaskPriority.S: return retreatBonusS;
            case TaskPriority.A: return retreatBonusA;
            case TaskPriority.B: return retreatBonusB;
            default: return retreatBonusC;
        }
    }
}
