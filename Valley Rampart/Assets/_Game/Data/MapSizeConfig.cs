using System;
using UnityEngine;

/// <summary>
/// 地图大小配置（doc 1 §2.4 / §5.6）。决定单张地图的宽高（格数）。
/// 资产实例放在 Resources/Grid/MapSizeConfig.asset
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/MapSizeConfig", fileName = "MapSizeConfig")]
public class MapSizeConfig : ScriptableObject
{
    [Header("地图大小规格（2D 档位：128²/256²/384²，D19）")]
    public MapSizeEntry[] sizes = new MapSizeEntry[3];

    [Header("同图 AI 王国数（按难度决定，不按 worldSize；数值重审归 2_1/2_8）")]
    [Tooltip("索引 0/1/2 对应 Easy/Normal/Hard 的基础数")]
    public int[] enemyByDifficulty = new int[3] { 1, 2, 3 };

    /// <summary>按 WorldSize 查单档规格。</summary>
    public MapSizeEntry GetEntry(WorldSize size)
    {
        for (int i = 0; i < sizes.Length; i++)
            if (sizes[i].size == size) return sizes[i];
        return sizes.Length > 0 ? sizes[0] : default;
    }

    /// <summary>按 WorldSize 查地图宽（格数）。默认 256。</summary>
    public int GetWidth(WorldSize size)
    {
        var e = GetEntry(size);
        return e.width > 0 ? e.width : 256;
    }

    /// <summary>按 WorldSize 查地图高（格数）。默认 256。</summary>
    public int GetHeight(WorldSize size)
    {
        var e = GetEntry(size);
        return e.height > 0 ? e.height : 256;
    }

    /// <summary>按难度查同图 AI 王国基础数。</summary>
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
    public int width;   // 格数（推荐 Small=128 / Medium=256 / Large=384，D19）
    public int height;  // 格数（= width，方形）
}

/// <summary>地图大小枚举。</summary>
public enum WorldSize
{
    Small,    // 128×128
    Medium,   // 256×256（标准体验）
    Large     // 384×384
}