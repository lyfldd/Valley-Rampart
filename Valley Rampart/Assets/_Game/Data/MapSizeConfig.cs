using System;
using UnityEngine;

/// <summary>
/// 地图大小配置（3.2 第 7.2 节）。决定单张地图的大区块数 M。
/// 资产实例放在 Resources/Grid/MapSizeConfig.asset
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/MapSizeConfig", fileName = "MapSizeConfig")]
public class MapSizeConfig : ScriptableObject
{
    [Header("地图大小规格")]
    [Tooltip("大/中/小三档，决定大区块数 M")]
    public MapSizeEntry[] sizes = new MapSizeEntry[3];

    [Header("敌方王国数量（按难度决定，不按 worldSize）")]
    [Tooltip("索引 0/1/2 对应 Easy/Normal/Hard 的基础数")]
    public int[] enemyByDifficulty = new int[3] { 1, 2, 3 };

    /// <summary>按 WorldSize 查大区块数 M。</summary>
    public int GetRegionCount(WorldSize size)
    {
        for (int i = 0; i < sizes.Length; i++)
            if (sizes[i].size == size) return sizes[i].regionCount;
        return 15;  // 默认中
    }

    /// <summary>按难度查敌方王国基础数。实际数 = 基础 + worldSeed 随机加 0~2。</summary>
    public int GetEnemyMapBase(int difficulty)
    {
        int idx = Mathf.Clamp(difficulty, 1, 3) - 1;
        return idx >= 0 && idx < enemyByDifficulty.Length ? enemyByDifficulty[idx] : 2;
    }
}

/// <summary>单档地图大小规格。</summary>
[Serializable]
public struct MapSizeEntry
{
    public WorldSize size;
    public int regionCount;  // 大区块数 M
}

/// <summary>地图大小枚举（3.2 第 2.2 节）。</summary>
public enum WorldSize
{
    Small,    // 10 个大区块（节奏快，适合试玩）
    Medium,   // 15 个大区块（标准体验）
    Large     // 24 个大区块（长期经营 + 更多敌方王国）
}
