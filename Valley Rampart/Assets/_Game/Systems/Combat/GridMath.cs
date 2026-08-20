using UnityEngine;

/// <summary>
/// 全库共用格空间距离/方向原语（改造计划 doc 1 §1.6 实现体，2_5/2_7/2_8 同源）。
/// 核心：把世界坐标（向量）统一除以格尺寸分量，得到"格单位"各向同性度量。
///   cellW=1.28 / cellH=0.64（GridConfig.cellSize），禁止任何公式把 cellSize 当标量用（R5）。
/// 对比旧 1D：旧距离开区间用 Vector2.Distance(world) + range×cellSize 标量；
/// 新：全程 GridMath.DistCells（格单位），射程字段语义统一为格单位，不再 ×cellSize。
/// </summary>
public static class GridMath
{
    // 格尺寸分量（世界单位/格）。与 GridConfig.cellSize 一致；静态工具不走资源加载，常量化。
    public const float CellW = 1.28f;
    public const float CellH = 0.64f;

    /// <summary>
    /// 格单位分量归一化欧氏距离：dist = √((Δx/1.28)² + (Δy/0.64)²)。
    /// 各向同性：横 3 格 = 纵 1.5 格 = 对角 √(3²+1.5²)/?（见 doc 1 §1.6，同"曼哈顿化正交"）。
    /// </summary>
    public static float DistCells(Vector2 a, Vector2 b)
    {
        float dx = (a.x - b.x) / CellW;
        float dy = (a.y - b.y) / CellH;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>格空间归一化方向（先除分量再 normalize，保证 360° 各向同性）。零向量回退指向右。</summary>
    public static Vector2 DirCells(Vector2 from, Vector2 to)
    {
        Vector2 d = new Vector2((to.x - from.x) / CellW, (to.y - from.y) / CellH);
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }
}