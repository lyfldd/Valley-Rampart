// ============================================================================
//  寻路系统读取网格的最小契约（doc 1 §5.4，2_6 唯一依赖口）。
//  坐标 = 微格 SubCell 坐标（GridCoord，layer 透传）。sim 训练侧可另起纯 C# 实现。
// ============================================================================

/// <summary>
/// 寻路系统读取网格的最小契约。坐标 = 微格坐标。
/// GridSystem 实现本接口；2_6 的寻路器只依赖接口不依赖具体类。
/// </summary>
public interface IPathGrid
{
    /// <summary>微格数宽（= 小区块宽 × 4）。</summary>
    int Width { get; }

    /// <summary>微格数高。</summary>
    int Height { get; }

    /// <summary>微格可走（§5.1 规则；跨格地形逐微格判定，Bridge 豁免水域）。</summary>
    bool IsWalkable(GridCoord subCoord);

    /// <summary>地形移动代价（Plain=1.0，Hills=1.5 等，SO 可配；格单位，§1.6）。</summary>
    float GetEnterCost(GridCoord subCoord);

    /// <summary>防穿角：两正交邻微格均可走才允许斜走。</summary>
    bool IsDiagonalMoveAllowed(GridCoord from, GridCoord to);
}