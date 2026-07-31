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
