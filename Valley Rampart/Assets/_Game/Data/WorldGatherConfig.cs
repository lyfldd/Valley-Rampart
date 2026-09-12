using UnityEngine;

// ============================================================================
//  世界资源点采集配置 SO（HH.221 / D685 裁 A①；so-data-driven 禁魔法数）
//  资产路径：Resources/Config/WorldGatherConfig.asset
//  消费方：WorldGatherRegistry（AI 世界资源点采集**选点层**：单国在册上限 + 总开关）
//  边界注记：本 SO 与 `WorldGatherRegistry`/`WorldGatherSource` 同批（HH.221）；
//    `Systems/World/` + `Data/` 层对 `Assets/_Game/Systems/AI.Core/` **零命中**（HH.236 §四 sim-sync 核查）
//    ⇒ 不进 AI.Core、不走 sim-sync（同 `ActionBackoffConfig` 先例）。
//  ⚠️下方数值＝**出厂占位值**；执行端不据跑局结果调值（禁参数微调找补 D563③）。
// ============================================================================

/// <summary>世界资源点采集配置（AI 侧，D685 A①）。</summary>
[CreateAssetMenu(menuName = "ValleyRampart/WorldGatherConfig", fileName = "WorldGatherConfig")]
public class WorldGatherConfig : ScriptableObject
{
    [Header("总开关（false ⇒ AI 不立案任何世界资源点采集源=零行为差异；出厂 true）")]
    public bool enabled = true;

    [Header("单国**在册**世界资源点采集源上限（防洪泛：源 ≤ 本值；完成后自动腾位，非按日配额）")]
    public int maxSourcesPerKingdom = 6;

    /// <summary>载入（缺 asset 时回退默认占位实例 ⇒ 出厂零配置仍生效；对齐 ActionBackoffConfig.Load 先例）。</summary>
    public static WorldGatherConfig Load()
    {
        var cfg = Resources.Load<WorldGatherConfig>("Config/WorldGatherConfig");
        return cfg != null ? cfg : CreateInstance<WorldGatherConfig>();
    }
}
