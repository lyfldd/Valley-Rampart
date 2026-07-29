using System;
using UnityEngine;

/// <summary>
/// 资源点生成配置（3.2.1 第 8.3 节）。二级约束：数量 + 难度密度 + 等级概率 + 位置偏移。
/// 资产实例放在 Resources/Grid/ResourceGenConfig.asset
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/ResourceGenConfig", fileName = "ResourceGenConfig")]
public class ResourceGenConfig : ScriptableObject
{
    [Header("资源点基础数量（每大区块 16 小区块内）")]
    public ResourceCountEntry[] baseCounts;

    [Header("难度密度因子（索引 0/1/2 对应 Easy/Normal/Hard）")]
    public float[] densityByDifficulty = new float[3] { 1.0f, 0.8f, 0.6f };

    [Header("一次性资源额外密度因子")]
    public float pickupDensityFactor = 0.8f;

    [Header("特殊点出现概率")]
    [Range(0f, 1f)] public float specialPointChance = 0.15f;

    [Header("资源点等级概率（按难度）")]
    public GradeProbEntry[] gradeProbByDifficulty;

    [Header("位置等级偏移")]
    public GradeOffset innerOffset;  // 内侧（低风险）：贫瘠+10%，富有-10%
    public GradeOffset outerOffset;  // 外侧（高风险）：贫瘠-10%，富有+10%

    // ===== 查表辅助 =====

    /// <summary>按难度查密度因子。</summary>
    public float GetDensity(int difficulty)
    {
        int idx = Mathf.Clamp(difficulty, 1, 3) - 1;
        return idx >= 0 && idx < densityByDifficulty.Length ? densityByDifficulty[idx] : 1.0f;
    }

    /// <summary>按地形查资源点基础数量范围。</summary>
    public (int min, int max) GetResourceCount(TerrainType terrain)
    {
        if (baseCounts == null) return (0, 0);
        for (int i = 0; i < baseCounts.Length; i++)
            if (baseCounts[i].terrain == terrain)
                return (baseCounts[i].minCount, baseCounts[i].maxCount);
        return (0, 0);
    }

    /// <summary>按难度 + 位置查最终等级概率。</summary>
    public GradeProbability GetGradeProb(int difficulty, bool isInner)
    {
        int idx = Mathf.Clamp(difficulty, 1, 3) - 1;
        GradeProbability baseProb = (idx >= 0 && idx < gradeProbByDifficulty.Length)
            ? gradeProbByDifficulty[idx].probability
            : new GradeProbability { barren = 0.35f, normal = 0.55f, rich = 0.10f };

        var offset = isInner ? innerOffset : outerOffset;
        return new GradeProbability
        {
            barren = Mathf.Clamp01(baseProb.barren + offset.barrenOffset),
            normal = Mathf.Clamp01(baseProb.normal + offset.normalOffset),
            rich   = Mathf.Clamp01(baseProb.rich + offset.richOffset)
        };
    }
}

/// <summary>资源点基础数量项。</summary>
[Serializable]
public struct ResourceCountEntry
{
    public TerrainType terrain;
    public int minCount;
    public int maxCount;
}

/// <summary>难度档位等级概率项。</summary>
[Serializable]
public struct GradeProbEntry
{
    public int difficulty;
    public GradeProbability probability;
}

/// <summary>三档等级概率（贫瘠/普通/富有，三者之和=1.0）。</summary>
[Serializable]
public struct GradeProbability
{
    [Range(0, 1)] public float barren;
    [Range(0, 1)] public float normal;
    [Range(0, 1)] public float rich;
}

/// <summary>位置等级偏移。</summary>
[Serializable]
public struct GradeOffset
{
    public float barrenOffset;
    public float normalOffset;
    public float richOffset;
}
