using UnityEngine;

/// <summary>
/// 地图生成约束规则工具类（3.2.1 第二节）。
/// 5 区分配 / 区索引映射 / 主城位置 / 内外侧判定 / 中心区边缘判定。
/// 由 WorldManager.Awake 时通过 SetConfig 注入 SO 配置。
/// 外部系统（建造/波次/战斗）可直接调用，无需依赖 WorldManager 实例。
/// </summary>
public static class MapGenRules
{
    static MapGenRulesConfig _config;

    /// <summary>注入 SO 配置（WorldManager.Awake 时调用）。</summary>
    public static void SetConfig(MapGenRulesConfig config)
    {
        _config = config;
    }

    // ========================================================================
    //  5 区分配（3.2.1 第 2.2 节）
    // ========================================================================

    /// <summary>计算 5 区分配数量。</summary>
    /// <remarks>
    /// total = extreme*2 + resource*2 + center 只能是 M 或 M+1：
    ///   - remaining 偶数 -> total = M（左右对称）
    ///   - remaining 奇数 -> total = M+1（GetZone 天然让 RightExtreme 少 1，左右不对称）
    /// </remarks>
    public static (int center, int extreme, int resource) CalcZoneCounts(int M)
    {
        float cRatio = _config != null ? _config.centerRatio : 0.3f;
        float eRatio = _config != null ? _config.extremeRatio : 0.25f;

        int center = Mathf.Max(2, EvenRound(M * cRatio));
        int extreme = Mathf.Max(1, Mathf.FloorToInt((M - center) * eRatio));
        // 向上取整：余数补到资源区而非丢弃
        int remaining = M - center - extreme * 2;
        int resource = Mathf.Max(1, Mathf.CeilToInt(remaining / 2f));
        return (center, extreme, resource);
    }

    /// <summary>四舍五入取偶数（banker's rounding：4.5->4，5.5->6）。</summary>
    static int EvenRound(float f)
    {
        int r = Mathf.RoundToInt(f);
        return r % 2 != 0 ? r + 1 : r;  // 奇数+1变偶数
    }

    // ========================================================================
    //  区索引映射（3.2.1 第 2.3 节）
    // ========================================================================

    /// <summary>大区块索引 -> 区分区（简写版，内部自动算 zone counts）。</summary>
    public static MapZone GetZone(int idx, int M)
    {
        var (center, extreme, resource) = CalcZoneCounts(M);
        return GetZone(idx, M, center, extreme, resource);
    }

    /// <summary>大区块索引 -> 区分区（带预计算 zone counts，避免重复算）。</summary>
    public static MapZone GetZone(int idx, int M, int center, int extreme, int resource)
    {
        int leftExtremeEnd = extreme;
        int leftResourceEnd = extreme + resource;
        int centerEnd = extreme + resource + center;

        if (idx < leftExtremeEnd) return MapZone.LeftExtreme;
        if (idx < leftResourceEnd) return MapZone.LeftResource;
        if (idx < centerEnd) return MapZone.Center;
        if (idx < centerEnd + resource) return MapZone.RightResource;
        return MapZone.RightExtreme;
    }

    // ========================================================================
    //  主城位置（3.2.1 第 2.4 节）
    // ========================================================================

    /// <summary>废弃城堡所在的大区块索引（2 个，对称，简写版）。</summary>
    public static (int castleA, int castleB) GetCastleRegionIndices(int M)
    {
        var (center, extreme, resource) = CalcZoneCounts(M);
        return GetCastleRegionIndices(M, center, extreme, resource);
    }

    /// <summary>废弃城堡所在的大区块索引（带预计算 zone counts）。</summary>
    public static (int castleA, int castleB) GetCastleRegionIndices(int M, int center, int extreme, int resource)
    {
        int centerStart = extreme + resource;
        int midOffset = center / 2;
        return (centerStart + midOffset - 1, centerStart + midOffset);
    }

    // ========================================================================
    //  内外侧 / 中心区边缘（3.2.1 第 2.3 节辅助）
    // ========================================================================

    /// <summary>资源区大区块是否内侧（靠中心区，低风险）。</summary>
    public static bool IsResourceInner(int idx, int M, MapZone zone)
    {
        var (center, extreme, resource) = CalcZoneCounts(M);
        int leftResourceEnd = extreme + resource;
        int centerEnd = extreme + resource + center;

        if (zone == MapZone.LeftResource)
        {
            int offsetFromCenter = leftResourceEnd - idx;  // 越小越靠中心
            return offsetFromCenter <= resource / 2;
        }
        if (zone == MapZone.RightResource)
        {
            int offsetFromCenter = idx - centerEnd;  // 越小越靠中心
            return offsetFromCenter < resource / 2;
        }
        return false;
    }

    /// <summary>是否中心区边缘（靠资源区侧，可能变肥沃）。</summary>
    public static bool IsCenterEdge(int idx, int M, int center, int extreme, int resource)
    {
        int centerStart = extreme + resource;
        int centerEnd = centerStart + center;
        return idx == centerStart || idx == centerEnd - 1;
    }
}
