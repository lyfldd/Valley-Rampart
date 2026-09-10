using UnityEngine;

// ============================================================================
//  经济诊断配置 SO（2_23 资源 P0 批A / R-A2，2_23 §七 数据层 P0 行）
//  资产路径：Resources/Config/KingdomDiagnosisConfig.asset
//  职责（2_23 §七）：经济诊断参数——产能基线／水位阈值／三通道判据／储备目标。
//  消费方：KingdomBrain.BuildEconomyBlock（R-A1 诊断块取参数）→ 批C ⑤三通道分诊消费判据。
//  数值禁区（2_23 §〇 增补节③同型）：出厂值=保守占位，只接结构不调值；实值归 P0 调优/后续轮。
// ============================================================================

[CreateAssetMenu(menuName = "ValleyRampart/KingdomDiagnosisConfig", fileName = "KingdomDiagnosisConfig")]
public class KingdomDiagnosisConfig : ScriptableObject
{
    [Header("产能基线（通道A 判据：产能建筑/人口比 < 基线）")]
    [Tooltip("每 N 人 ≥1 产能建筑（farm/人口比基线；与 MustHaveConfig.farmPerPeople 同源口径，诊断块报告用）")]
    public float capacityPerPeopleBaseline = 5f;

    [Header("采集基线（通道B 判据：有产能但日产出/人口低）")]
    [Tooltip("人均资源节点期望（采集点饱和度分母；0=不判采集点饱和度）")]
    public float gatherNodePerPopBaseline = 0.05f;

    [Tooltip("日产出/人口 下限（通道B 判据阈值；日产出取诊断块①收支窗口）")]
    public float channelBOutputPerPop = 1f;

    [Header("水位阈值（通道C 判据 + 储备）")]
    [Tooltip("粮裕日底线（天）——对齐 KingdomBrainConfig.grainReserveDaysFloor 既有口径；⑤粮特例触发沿用（底线机制不动）")]
    public int grainReserveDaysFloor = 2;

    [Tooltip("仓储占用率溢出阈（0~1）：产出正常但占用率超阈 → 通道C 仓储扩容")]
    [Range(0f, 1f)] public float storageOccupancyThreshold = 0.9f;

    [Header("储备目标")]
    [Tooltip("粮储备目标（天）")]
    public int reserveTargetDaysGrain = 7;

    [Tooltip("金石木铁储备目标（天；日耗近似=人口×粮耗口径同源，出厂保守占位）")]
    public int reserveTargetDaysOther = 3;

    [Header("粮耗口径")]
    [Tooltip("每人口每日粮耗（对齐 KingdomBrainConfig.grainConsumptionPerPop / UtilityScorer.PerPopGrain）")]
    public int grainConsumptionPerPop = 1;

    [Header("严重度排序（批C 消费；本批只落字段，2_23 §2.2 多资源同时断裂按断供严重度排序）")]
    [Tooltip("断供严重度序（EcoResource int：0金 1石 2木 3粮 4铁）——出厂=粮→金→石→木 前置（铁尾随，D531① 原文未列铁）")]
    public int[] shortageSeverityOrder = new int[] { 3, 0, 1, 2, 4 };

    /// <summary>载入诊断配置（缺 asset 时回退默认占位实例；对齐 SituationConfig.Load 先例）。</summary>
    public static KingdomDiagnosisConfig Load()
    {
        var cfg = Resources.Load<KingdomDiagnosisConfig>("Config/KingdomDiagnosisConfig");
        return cfg != null ? cfg : CreateInstance<KingdomDiagnosisConfig>();
    }
}
