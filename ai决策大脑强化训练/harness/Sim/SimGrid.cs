// ============================================================================
//  M2 Headless 模拟器 - SimGrid 空间分区（1D 复刻 GridSystem）
//  04_模拟器规格.md §一：SimGrid：格子字典（复刻 GridSystem 的 TryEnter/GetUnitsInCell，
//  1D 只需 x 向）。无 y 层：Unity y=0/1 双层压平为地面层（y 恒 0）。
//  保真要点：
//    - WorldToCoord：x = floor(pos.x / cellSize)（GridSystem.cs:44）
//    - 中区块索引：cellX / midRegionCellCount（GridSystem.cs:69-74）
//    - 堆叠上限：GridConfig.stackLimits（category0=12）
//    - 格子内单位保持"进入顺序"（Unity GridCell.Units 为 List 插入序）——
//      确定性关键：感知结果顺序 = dx 扫描序 × 格内进入序，保证同 seed 同结果（04 §七）。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 1D 空间分区（复刻 GridSystem 的运行时区块管理）。
/// 只做进入/退出/查询，无状态索引（与 GridSystem 一致：无存档，重启后 TryEnter 自恢复）。
/// </summary>
public sealed class SimGrid
{
    private readonly float _cellSize;
    private readonly int _midRegionCellCount;
    private readonly int _stackLimit;

    // cellX -> 格内单位（保持进入序；y 压平恒 0，键只存 x）
    private readonly Dictionary<int, List<IUnitHandle>> _cells = new Dictionary<int, List<IUnitHandle>>();
    // unit -> cellX（退出用）
    private readonly Dictionary<IUnitHandle, int> _unitCells = new Dictionary<IUnitHandle, int>();

    public SimGrid(float cellSize, int midRegionCellCount, int stackLimit)
    {
        _cellSize = cellSize > 0f ? cellSize : 2.26f;
        _midRegionCellCount = midRegionCellCount > 0 ? midRegionCellCount : 4;
        _stackLimit = stackLimit;
    }

    /// <summary>格大小（SimPerception 计算 cellRange 用）。</summary>
    public float CellSizeForQuery => _cellSize;

    /// <summary>世界坐标 x -> 小区块 cellX（GridSystem.WorldToCoord 1D：floor(x/cellSize)）。
    /// MathfX 核内无 FloorToInt，此处内联 System.Math.Floor（不修改核 shim）。</summary>
    public int WorldToCellX(float worldX)
    {
        return (int)System.Math.Floor(worldX / _cellSize);
    }

    /// <summary>世界坐标 -> 中区块索引（GridSystem.CellToMidRegionIndex：cellX / midRegionCellCount）。</summary>
    public int WorldToMidRegion(float worldX)
    {
        return WorldToCellX(worldX) / _midRegionCellCount;
    }

    /// <summary>cellX -> 中区块索引。</summary>
    public int CellToMidRegion(int cellX)
    {
        return cellX / _midRegionCellCount;
    }

    /// <summary>
    /// 单位尝试进入区块（复刻 GridSystem.TryEnter 的堆叠上限语义）。
    /// 堆叠满返回 false（排队等待，行为与 Unity 一致；战斗推进中基本不触发）。
    /// </summary>
    public bool TryEnter(IUnitHandle unit, float worldX)
    {
        int cellX = WorldToCellX(worldX);

        // 先退出旧格（GridSystem.TryEnter 内 ExitCurrentCell）
        Exit(unit);

        if (!_cells.TryGetValue(cellX, out var list))
        {
            list = new List<IUnitHandle>();
            _cells[cellX] = list;
        }
        if (_stackLimit > 0 && list.Count >= _stackLimit)
            return false;

        list.Add(unit);
        _unitCells[unit] = cellX;
        return true;
    }

    /// <summary>单位退出当前区块（GridSystem.ExitCurrentCell）。</summary>
    public void Exit(IUnitHandle unit)
    {
        if (_unitCells.TryGetValue(unit, out int cellX))
        {
            if (_cells.TryGetValue(cellX, out var list))
                list.Remove(unit);
            _unitCells.Remove(unit);
        }
    }

    /// <summary>获取区块内所有单位（副本，保持进入序；对应 GridSystem.GetUnitsInCell 返回副本）。</summary>
    public List<IUnitHandle> GetUnitsInCell(int cellX)
    {
        return _cells.TryGetValue(cellX, out var list)
            ? new List<IUnitHandle>(list)
            : new List<IUnitHandle>();
    }

    /// <summary>查询指定格内的单位（IWorldQuery 端口实现，感知粗筛用）。</summary>
    public void QueryUnitsInCell(int cellX, List<IUnitHandle> results)
    {
        results.Clear();
        if (_cells.TryGetValue(cellX, out var list))
            results.AddRange(list);
    }

    /// <summary>清空所有区块。</summary>
    public void ClearAll()
    {
        _cells.Clear();
        _unitCells.Clear();
    }
}
