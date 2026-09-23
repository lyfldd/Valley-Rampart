using UnityEngine;

/// <summary>
/// 全库共用格空间距离/方向原语（改造计划 doc 1 §1.6 实现体，2_5/2_7/2_8 同源）。
/// 核心：把世界坐标（向量）统一除以格尺寸分量，得到"格单位"各向同性度量。
///   cellW=1.28 / cellH=0.64（GridConfig.cellSize），禁止任何公式把 cellSize 当标量用（R5）。
/// 对比旧 1D：旧距离开区间用 Vector2.Distance(world) + range×cellSize 标量；
/// 新：全程 GridMath.DistCells（格单位），射程字段语义统一为格单位，不再 ×cellSize。
///
/// 【HH.320 批 1 · 件 1（`D843`/`D846`）】⭐ 换算**分域**（新增 · 唯一口）：
///   ① **格单位域**（寻路／派工／邻近查询）＝ <see cref="DistCells"/> ⇒ ⛔ 本批不动
///      （其「分量归一化椭圆」语义对寻路仍成立 · `D843` 分域声明）。
///   ② **视觉格域**（伤害链：射程／索敌／命中／溅射／地面效果）＝ <see cref="DistVisual"/>
///      ⇒ 世界空间**欧氏圆**（`Vector2.Distance` ⇒ 屏幕呈正圆）；`DistVisual ≤ R` 等价
///      「**沿格轴向**射程恰为 `R` 格」（`R` 字段语义不变 · ⛔ 不改 `range`/`attackRange` 数值）。
///   ⚠️ 「格」在不同域不是同一个量 ⇒ 新增调用点先认域再选口（`HH.140` P7 混域＝静默错答）。
/// </summary>
public static class GridMath
{
    // 格尺寸分量（世界单位/格）。⭐ **真源 ＝ `GridConfig.cellSize`** —— 由 `GridSystem.Awake` 经
    // <see cref="Bind"/> 显式注入（`HH.320` 排雷 `M1`：消除「硬编码常量 vs 资产」双源）。
    // 下列默认值 ＝ `GridConfig.asset` 现值（**未绑定时的回退**，供无实例/编辑器纯投影场景可算）。
    public const float DefaultCellW = 1.28f;
    public const float DefaultCellH = 0.64f;

    static float _cellW = DefaultCellW, _cellH = DefaultCellH;
    static float _cellStep = ComputeCellStep(DefaultCellW, DefaultCellH);
    static bool _bound;

    /// <summary>格宽（世界单位/格 · 屏幕横向）。真源 ＝ `GridConfig.cellSize.x`。</summary>
    public static float CellW => _cellW;

    /// <summary>格高（世界单位/格 · 屏幕纵向）。真源 ＝ `GridConfig.cellSize.y`。</summary>
    public static float CellH => _cellH;

    /// <summary>是否已绑定真源（false ＝ 仍用编译期回退值 ⇒ 与配置可能漂移，哨兵可见）。</summary>
    public static bool Bound => _bound;

    /// <summary>⭐ **格步长**（世界单位）：沿**任一格轴**走 1 格的世界位移长度 ＝ `0.5×√(cellW²+cellH²)`。
    /// ⚠️ 语义 ＝ 「1 格的**轴向步长**」（等轴菱形边长）；⛔ **不要**按「格对角线之半」去推导射程
    /// （`D846` `P5` 命名勘正）——两者数值相同但推导口径不同，按对角线推会得出错误的格数换算。
    /// 现配置 ＝ **0.7155**。</summary>
    public static float CellStep => _cellStep;

    static float ComputeCellStep(float w, float h) => 0.5f * Mathf.Sqrt(w * w + h * h);

    /// <summary>⭐【`M1` 唯一绑定口】把格尺寸真源（`GridConfig.cellSize`）注入本静态工具。
    /// 由 `GridSystem.Awake` 调用（1 行）；未绑定 ⇒ 用编译期回退值（＝现值 ⇒ 行为不变）。
    /// 值发生漂移 ⇒ 记一条 Log 使漂移可见（**一致性哨兵**：常量与配置不再各说各话）。</summary>
    public static void Bind(Vector2 cellSize)
    {
        if (cellSize.x <= 0f || cellSize.y <= 0f) return;
        bool changed = !Mathf.Approximately(cellSize.x, _cellW) || !Mathf.Approximately(cellSize.y, _cellH);
        _cellW = cellSize.x;
        _cellH = cellSize.y;
        _cellStep = ComputeCellStep(_cellW, _cellH);
        if (!_bound || changed)
            Debug.Log($"[GridMath] 绑定 GridConfig.cellSize=({_cellW:F3}, {_cellH:F3}) ⇒ 格步长={_cellStep:F4}"
                      + (changed ? $"（编译期默认 ({DefaultCellW}, {DefaultCellH}) 已让位 · 原为双源）" : ""));
        _bound = true;
    }

    /// <summary>
    /// 格单位分量归一化欧氏距离：dist = √((Δx/cellW)² + (Δy/cellH)²)。
    /// ⚠️ **域 ＝ 寻路／派工／邻近查询**（⛔ 伤害链请用 <see cref="DistVisual"/>）。
    /// 各向同性：横 3 格 = 纵 1.5 格 = 对角 √(3²+1.5²)/?（见 doc 1 §1.6，同"曼哈顿化正交"）。
    /// </summary>
    public static float DistCells(Vector2 a, Vector2 b)
    {
        float dx = (a.x - b.x) / _cellW;
        float dy = (a.y - b.y) / _cellH;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>格空间归一化方向（先除分量再 normalize，保证 360° 各向同性）。零向量回退指向右。</summary>
    public static Vector2 DirCells(Vector2 from, Vector2 to)
    {
        Vector2 d = new Vector2((to.x - from.x) / _cellW, (to.y - from.y) / _cellH);
        return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    // ========================================================================
    //  【HH.320 批 1 · 件 1】视觉格域（伤害链唯一口径）
    // ========================================================================

    /// <summary>⭐【件 1 **唯一换算口**】世界空间欧氏距离 ⇒ **视觉格数** ＝ `Vector2.Distance(a,b) / 格步长`。
    /// 语义：`DistVisual ≤ R` ⇒ **沿格轴向射程恰为 `R` 格**（屏幕呈正圆；⚠️ 对角比轴向远 ≈11.8%，预期内）。
    /// ⛔ 域 ＝ **伤害链**（射程／索敌／命中／溅射／地面效果）；寻路/派工 ⛔ 不得改走本口（仍用 `DistCells`）。</summary>
    public static float DistVisual(Vector2 a, Vector2 b)
        => Vector2.Distance(a, b) / _cellStep;

    /// <summary>视觉格 ⇒ 世界长度（唯一换算口；⛔ 禁调用方自行写 `× 0.7155`／`× cellSize`）。</summary>
    public static float VisualToWorld(float rangeVisual) => rangeVisual * _cellStep;

    /// <summary>世界长度 ⇒ 视觉格（唯一换算口；供「入参仍为世界量」的既有口收口，如机器敌情门控）。</summary>
    public static float WorldToVisual(float lengthWorld) => lengthWorld / _cellStep;

    /// <summary>弹道/视线**带的半带宽**（世界单位）＝ 屏幕横向半格 ＝ `cellW/2`（现配置 0.64）。
    /// ⚠️ 与视觉格口径关系：`0.64 ÷ 格步长 0.7155 = 0.8944 视觉格`（≈0.894 格）。
    /// ⛔ 非「grid 标量乘算」—— 本口把该长度**收口为单一世界量**（`R5`：伤害链内不再出现 `cellSize.x * 0.5f`）。</summary>
    public static float PathBandHalf => _cellW * 0.5f;

    /// <summary>⭐【件 18】**微格候选窗半径（超集）**：覆盖「视觉格半径 `R` 的世界圆」所需的**子格索引差**上界。
    /// 推导（`D846` §10.3 采纳）：子格坐标 `gx = Δx/subW + Δy/subH`（`GridSystem.WorldToCellF` 同形）⇒
    /// `sup|Δgx| = R_world × √(1/subW² + 1/subH²)`（柯西–施瓦茨）⇒
    /// **`n = ⌈R_vis × subDiv × (cellW²+cellH²) / (2·cellW·cellH)⌉`**（现配置 ＝ `⌈5 × R_vis⌉`）。
    /// ⛔ 参数**现算**（`subW/subH = cellSize/subDiv`），⛔ 不写死 0.32/0.16。R_vis=0.25 ⇒ **2**（收口前 1 ⇒ 非超集）。</summary>
    public static int SubWindowForVisualRadius(float rangeVisual, int subDiv)
    {
        if (subDiv < 1) subDiv = 1;
        float k = (_cellW * _cellW + _cellH * _cellH) / (2f * _cellW * _cellH);
        return Mathf.Max(0, Mathf.CeilToInt(rangeVisual * subDiv * k));
    }

    /// <summary>⭐【同族超集口】覆盖「视觉格半径 `R` 的世界圆」所需的**地块格索引差**上界
    /// （＝ `⌈R × (cellW²+cellH²)/(2·cellW·cellH)⌉` ＝ 现配置 `⌈1.25 × R⌉`）。
    /// 供仍按**地块格**取窗的调用点使用；子格级取窗请用 <see cref="SubWindowForVisualRadius"/>。</summary>
    public static int CellWindowForVisualRadius(float rangeVisual)
    {
        float k = (_cellW * _cellW + _cellH * _cellH) / (2f * _cellW * _cellH);
        return Mathf.Max(1, Mathf.CeilToInt(rangeVisual * k));
    }
}
