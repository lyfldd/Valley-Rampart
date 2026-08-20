using System;

// ============================================================================
//  2_6 P0a 微格 A*：格单位数学（纯 C#，确定性。doc 1 §1.6 距离约定）。
// ============================================================================

/// <summary>
/// 格单位距离/常量（doc 1 §1.6）。纯 C#（System.Math），仅 Int 坐标运算，确定性优先。
/// 距离约定：DistCells = √((Δx/cellW)² + (Δy/cellH)²)。寻路用 Octile 启发（准入该约定）。
/// </summary>
public static class GridMathCore
{
    /// <summary>微格 8 向邻居偏置，顺序固定 E/NE/N/NW/W/SW/S/SE（doc 1 §5.4，确定性/平局裁决依赖此序）。</summary>
    public static readonly GridCoord[] NeighborOffsets8 =
    {
        new GridCoord( 1,  0),   // E
        new GridCoord( 1,  1),   // NE
        new GridCoord( 0,  1),   // N
        new GridCoord(-1,  1),   // NW
        new GridCoord(-1,  0),   // W
        new GridCoord(-1, -1),   // SW
        new GridCoord( 0, -1),   // S
        new GridCoord( 1, -1),   // SE
    };

    /// <summary>√2 走常量（斜走代价，浮点顺序固定防止漂移）。</summary>
    public const float DiagonalCost = 1.41421356f;

    /// <summary>格单位分量归一化欧氏（doc 1 §1.6）：√((Δx/cellW)² + (Δy/cellH)²)。
    /// cellW/cellH 为小区块世界尺寸。P0a 用默认 1.28/0.64；调用方按 GridConfig 传入。</summary>
    public static float DistCells(int ax, int ay, int bx, int by, float cellW, float cellH)
    {
        float dx = (ax - bx) / cellW, dy = (ay - by) / cellH;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Octile 启发（可采纳）：max/√2 对角 + 直线余；与格单位一致（常量分量）。</summary>
    public static float Octile(int ax, int ay, int bx, int by)
    {
        int dx = Math.Abs(ax - bx), dy = Math.Abs(ay - by);
        int diag = Math.Min(dx, dy), straight = Math.Abs(dx - dy);
        return straight + DiagonalCost * diag;
    }
}