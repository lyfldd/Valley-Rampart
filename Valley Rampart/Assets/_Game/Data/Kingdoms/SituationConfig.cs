using UnityEngine;

// ============================================================================
//  态势层配置 SO（2_22 P0 批A / A3，§八 SO 清单）。
//  资产路径：Resources/Config/SituationConfig.asset（so-data-driven：全参数 SO 化禁魔法数）。
//  消费方：KingdomBrain 态势层子步①（A2：威胁/统计窗口+脏标记 TTL）+批C 姿态层（C1：
//  危机线与恢复线=动员档升降判据，批A 只落字段）。
// ============================================================================

[CreateAssetMenu(menuName = "ValleyRampart/SituationConfig", fileName = "SituationConfig")]
public class SituationConfig : ScriptableObject
{
    [Header("态势窗口（D517 五件聚合）")]
    [Tooltip("威胁分布/边境接触面的兵力求和基准窗（日）。批A 日 tick 全量重建=即时时点值，本窗为 B5 表现统计与批C 姿态判定的时间基准预留")]
    public int threatWindowDays = 3;

    [Tooltip("兵种表现统计滚动窗口（日，D517⑤ 确定性窗口平均；批B B5 局内环自整定消费）")]
    public int perfWindowDays = 10;

    [Tooltip("损毁清单滚动窗口 TTL（日，D517④；过期条目日 tick 清除）")]
    public int lossTtlDays = 7;

    [Header("脏标记（A2：事件只置脏标记不即时改快照）")]
    [Tooltip("脏标记 TTL（日）：事件置位后超过该天数未被日 tick 消费则过期（正常节拍=次日 tick 即消费，TTL 为异常兜底）")]
    public int dirtyTtlDays = 1;

    [Header("危机线（批C 姿态层动员档消费，批A 只落字段 D518）")]
    [Tooltip("危机线：军力现状低于该值且威胁非零 → 动员档触发判据之一（C5 军力危机线）")]
    public int crisisLine = 2;

    [Tooltip("恢复线：动员档回落警戒档的滞回判据（>危机线防抖，C1 滞回窗）")]
    public int recoveryLine = 4;

    // ===== 内源节拍（D590 增补节/D589 列报2：阶段推进评分不得以外部袭扰为唯一源）=====
    [Header("内源势能（D590 增补节②③：连续权重函数禁硬表；数值禁区=初始权重 0.1 保守量级只接结构不调值，实值归 P0 调优后续轮；可训练标量预留）")]
    [Tooltip("内源势能总权重（军事行动 NeedScore 合成项乘数；0=关[负探针锚：置 0 退化六考死滞表型]）")]
    [Range(0f, 1f)] public float internalDriveWeight = 0.1f;
    [Tooltip("经济盈余率分量占比（三输入线性加权内份额，和=1；连续函数=最简线性起步）")]
    [Range(0f, 1f)] public float driveEconShare = 0.5f;
    [Tooltip("人口压力分量占比")]
    [Range(0f, 1f)] public float drivePopShare = 0.3f;
    [Tooltip("仓储水位分量占比")]
    [Range(0f, 1f)] public float driveStorageShare = 0.2f;

    /// <summary>载入态势配置（缺 asset 时回退默认占位实例；对齐 KingdomBrain.LoadConfig 先例）。</summary>
    public static SituationConfig Load()
    {
        var cfg = Resources.Load<SituationConfig>("Config/SituationConfig");
        return cfg != null ? cfg : ScriptableObject.CreateInstance<SituationConfig>();
    }
}
