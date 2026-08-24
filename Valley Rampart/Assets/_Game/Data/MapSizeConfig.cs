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

    [Header("同图 AI 王国数（2_16 步骤3 起按 D288 档位，读 KingdomFoundingConfig；本数组退役保留兼容旧档序列化）")]
    [Tooltip("已退役（D288：AI 数改由 KingdomFoundingConfig.GetAiCountRange 按 worldSize 档位决定，难度取档内低/随机/高值）。保留字段避免旧 assets 反序列化告警。")]
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

    /// <summary>同图 AI 王国数（2_16 步骤3 / D288：按 worldSize 档位 + 难度，rng 种子化取档内值）。</summary>
    public int GetEnemyMapBase(WorldSize size, int difficulty, System.Random rng)
    {
        var fc = Resources.Load<KingdomFoundingConfig>("Config/Kingdoms/KingdomFoundingConfig");
        var range = fc != null ? fc.GetAiCountRange(size) : new Vector2Int(2, 4);
        int lo = Mathf.Max(1, range.x);
        int hi = Mathf.Max(lo, range.y);
        if (difficulty <= 1) return lo;          // Easy → 档内取低
        if (difficulty >= 3) return hi;          // Hard → 档内取高
        return (rng != null) ? rng.Next(lo, hi + 1) : lo;   // Normal → 档内随机
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