using UnityEngine;

/// <summary>
/// 全局伤害规则配置（SO，3.4 第九节三轨数值之一）。
/// 所有职业/建筑共享一套全局规则，不塞进每个 SO（冗余且易不一致）。
/// Play 模式拖滑块实时看反应，无需重编译。
///
/// 详见 3.4_伤害管线设计.md 第九节、决策 27。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/DamageConfig", fileName = "DamageConfig")]
public class DamageConfig : ScriptableObject
{
    [Header("伤害公式")]
    [Tooltip("护甲常数 K，减伤率 = 护甲/(护甲+K)。占位 100")]
    public float armorK = 100f;

    [Header("时间轮")]
    [Tooltip("tick 间隔（秒），CD 取整到此倍数")]
    public float tickInterval = 0.1f;

    [Header("分片")]
    [Tooltip("每帧攻击判定上限，超出推下帧")]
    public int maxAttacksPerFrame = 100;

    [Header("过度杀伤")]
    [Tooltip("同一目标最多被多少近战锁定，超出换次近")]
    public int overkillLimit = 2;

    [Header("受击事件节流")]
    [Tooltip("同一 victim 最少间隔秒数")]
    public float eventThrottle = 0.5f;

    [Header("投射物到达检测")]
    [Tooltip("到达时刻位置检测半径（格单位，2_5 决议 D1 占位 0.25，逻辑上等效微格级）")]
    public float hitRadiusCells = 0.25f;
    [Tooltip("首版 MaxHits=1（单发），AoE 留接口 P2")]
    public int maxHits = 1;

    [Header("投射物视觉")]
    [Tooltip("抛物线弧高（世界单位）")]
    public float arcHeight = 2f;

    [Header("弹道误差")]
    [Tooltip("弹道落点误差圆半径（世界单位）。0=精确命中，>0=落点在目标周围随机散布。后续可通过精度属性/科技升级缩小")]
    public float projectileErrorRadius = 0f;

    [Header("受击反馈")]
    [Tooltip("受击闪红时长（秒）")]
    public float hitFlashDuration = 0.1f;

    // ===== 2_5 2D 战斗（格单位）新增字段 =====

    [Header("2_5 目标选择（D83，2_7 消费）")]
    [Tooltip("目标滞回保持带：当前目标存活且 dist ≤ range×此值 → 不换目标，防抖")]
    public float stickinessFactor = 1.15f;
    [Tooltip("目标评分价值权重（D83）：score = valueWeight×目标价值 − distWeight×距离")]
    public float valueWeight = 1f;
    [Tooltip("目标评分距离权重（D83）")]
    public float distWeight = 1f;
    [Tooltip("远程视线开关：true=远程需 HasLineOfSight（微格 Bresenham），false=仅距离")]
    public bool losEnabled = true;

    [Header("2_5 投射物池")]
    [Tooltip("投射物池上限（溢出丢最旧）。原 const MaxPoolSize=200，改 SO 默认 128")]
    public int projectilePoolSize = 128;

    [Header("2_5 击飞（D69 归本篇）")]
    [Tooltip("击飞抛物线顶点高度（世界单位，占位 6）")]
    public float KnockbackArcHeight = 6f;
    [Tooltip("击飞力度系数（占位 6，与 CombatRules.KnockbackForceScale 对齐预留）")]
    public float KnockbackForceScale = 6f;
}
