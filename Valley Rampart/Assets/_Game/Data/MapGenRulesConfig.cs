using System;
using UnityEngine;

/// <summary>
/// 地图生成约束规则配置（3.2.1 第八节）。
/// 5 区比例 + 邻接矩阵 + 资源保障下限 + 资源区地形权重。
/// 资产实例放在 Resources/Grid/MapGenRulesConfig.asset
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/MapGenRulesConfig", fileName = "MapGenRulesConfig")]
public class MapGenRulesConfig : ScriptableObject
{
    [Header("5 区分配比例")]
    [Range(0.2f, 0.6f)] public float centerRatio = 0.3f;     // 中心区占比
    [Range(0.1f, 0.3f)] public float extremeRatio = 0.25f;   // 单侧极端区占比

    [Header("资源区地形权重（内侧：靠中心区，中低风险）")]
    public TerrainWeight[] resourceInnerWeights;

    [Header("资源区地形权重（外侧：靠极端区，高风险）")]
    public TerrainWeight[] resourceOuterWeights;

    [Header("中心区边缘肥沃概率")]
    [Range(0f, 1f)] public float centerFertileChance = 0.3f;  // 中心区边缘变肥沃的概率

    [Header("邻接约束矩阵（7 种地形各一条）")]
    public AdjacencyEntry[] adjacencyMatrix;

    [Header("资源保障下限")]
    public int minForest = 1;   // 全图至少 N 个林地
    public int minStone = 1;    // 全图至少 N 个矿山或丘陵
    public int minFertile = 1;  // 全图至少 N 个肥沃

    // ===== 查表辅助 =====

    /// <summary>判断两种地形能否直接邻接（3.2.1 第 5.2 节）。</summary>
    /// <param name="strict">true=严格模式（△ 算违规，用于区交界）；false=宽松模式（△ 算合法，用于区内）</param>
    public bool CanAdjacency(TerrainType a, TerrainType b, bool strict = true)
    {
        if (adjacencyMatrix == null) return true;
        // 找 a 的条目
        for (int i = 0; i < adjacencyMatrix.Length; i++)
        {
            if (adjacencyMatrix[i].terrain == a)
            {
                // 在 allowed 里 = ✅
                var allowed = adjacencyMatrix[i].allowedNeighbors;
                if (allowed != null)
                    for (int j = 0; j < allowed.Length; j++)
                        if (allowed[j] == b) return true;
                // 宽松模式：检查 tolerable（△，仅区内允许）
                if (!strict)
                {
                    var tolerable = adjacencyMatrix[i].tolerableNeighbors;
                    if (tolerable != null)
                        for (int j = 0; j < tolerable.Length; j++)
                            if (tolerable[j] == b) return true;
                }
                return false;  // 不在 allowed/tolerable 里 = ❌
            }
        }
        return true;  // 没配 = 默认允许
    }

    /// <summary>判断地形是否可建造（玩家建筑放置判定）。</summary>
    public bool IsBuildableTerrain(TerrainType terrain)
    {
        // 平原（普通/肥沃）可建造；其余不可
        return terrain == TerrainType.Plain;
    }
}

/// <summary>地形权重项（用于资源区地形随机选择）。</summary>
[Serializable]
public struct TerrainWeight
{
    public TerrainType terrain;
    [Range(0f, 1f)] public float weight;
}

/// <summary>邻接约束项。</summary>
[Serializable]
public struct AdjacencyEntry
{
    public TerrainType terrain;
    [Tooltip("可直接邻接的地形（✅）")]
    public TerrainType[] allowedNeighbors;
    [Tooltip("勉强邻接（△，仅区内允许，区交界算违规）")]
    public TerrainType[] tolerableNeighbors;
    // 其余为 ❌（禁止，需缓冲地形隔开）
}
