using UnityEngine;

// ============================================================================
//  2_4 LOD 区块划分 - LODSystem 全局调参 SO（2D 中区块粒度）
//  字段从旧 AttentionTuningConfig 的 lod/heat 区段迁出 + 2D 新增项。
//  AttentionTuningConfig 同名旧字段保留（经 ToSnapshot 进 AI.Core 决策核，
//  北极星同源要求），本 SO 是 LODSystem 的运行时消费真源。
//  加载方式：Resources.Load<LodConfig>("Config/LodConfig")
// ============================================================================
[CreateAssetMenu(menuName = "ValleyRampart/LodConfig", fileName = "LodConfig")]
public class LodConfig : ScriptableObject
{
    [Header("活跃带半径（2D 中区块，切比雪夫）")]
    [Tooltip("活跃半径：距任一中心切比雪夫距离 ≤ 此值（中区块数）→ Active。默认 1 → 3×3 中区块 = 12×12 格")]
    public int activeRadiusMidChunks = 1;
    [Tooltip("半活跃半径：距任一中心切比雪夫距离 ≤ 此值 → SemiActive。默认 2 → 5×5 中区块 = 20×20 格")]
    public int semiActiveRadiusMidChunks = 2;

    [Header("Think 频率（Hz）")]
    [Tooltip("活跃档 Think 频率（Hz）；NPCBrain 活跃档现用硬编码 ThinkInterval，本字段为扩展预留")]
    public float activeHz = 10f;
    [Tooltip("半活跃档 Think 频率（Hz）")]
    public float semiHz = 2f;
    [Tooltip("休眠档 Think 频率（Hz）；移动仍每帧（D79），仅降思考频率")]
    public float dormantHz = 0.5f;

    [Tooltip("降档迟滞（秒）：热度归零 + 无事件累计 ≥ 此值才降档，防档位抖动（原 30s 缩短为 3s）")]
    public float demoteDelaySeconds = 3f;

    [Header("热点（多中心活跃带 D77）")]
    [Tooltip("成热点热度阈值（归一化）：热度 > 此值的中区块成为活跃中心")]
    public float hotspotThreshold = 0.3f;
    [Tooltip("多中心上限：取热度前 N 中区块为中心，防事件风暴（D3）")]
    public int maxCenters = 8;

    [Header("热度扩散 / 衰减")]
    [Tooltip("扩散阈值：热度超此值向 4 邻溢热度")]
    public float heatSpreadThreshold = 0.6f;
    [Tooltip("扩散系数：邻中区块获得 热度×此值")]
    public float spreadRatio = 0.4f;
    [Tooltip("衰减速率（/秒）")]
    public float heatDecayRate = 0.05f;
    [Tooltip("热度上限（clamp）")]
    public float heatMax = 1f;

    [Header("热度事件注入量")]
    [Tooltip("受击注入热度")]
    public float heatHitGain = 0.4f;
    [Tooltip("敌入注入热度")]
    public float heatEnemyEnter = 0.2f;
    [Tooltip("友撤注入热度")]
    public float heatAllyRetreat = 0.05f;

    [Header("调试")]
    [Tooltip("中区块热度/LOD Gizmos 总开关")]
    public bool drawGizmos = true;
}