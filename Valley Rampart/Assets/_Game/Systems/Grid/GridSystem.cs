using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时区块管理器（3.2 第 7.5 节）。
/// 无状态索引：不存档，单位存档后重新 TryEnter 状态自恢复。
/// 只管理当前活跃地图的区块。
/// </summary>
public class GridSystem : Singleton<GridSystem>
{
    [SerializeField] private GridConfig config;

    private readonly Dictionary<GridCoord, GridCell> _cells = new Dictionary<GridCoord, GridCell>();
    private readonly Dictionary<UnitController, GridCoord> _unitCells = new Dictionary<UnitController, GridCoord>();

    /// <summary>当前活跃地图（用于 Gizmos 可视化）。</summary>
    private MapData _activeMap;

    public GridConfig Config => config;

    /// <summary>当前地图总小区块数（大区块数 × regionCellCount）。用于边界校验。</summary>
    public int MapCellCount
    {
        get
        {
            if (_activeMap == null || config == null) return 0;
            return _activeMap.regions.Count * config.regionCellCount;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        if (config == null)
            config = Resources.Load<GridConfig>("Grid/GridConfig");
    }

    // ===== 坐标转换 =====

    /// <summary>世界坐标 → 小区块坐标。</summary>
    public GridCoord WorldToCoord(Vector2 pos)
    {
        if (config == null) return new GridCoord(0, 0);
        int x = Mathf.FloorToInt(pos.x / config.cellSize);
        int y = pos.y > config.flyHeightThreshold ? 1 : 0;
        return new GridCoord(x, y);
    }

    /// <summary>小区块坐标 → 世界坐标（中心点）。</summary>
    public Vector2 CoordToWorld(GridCoord coord)
    {
        if (config == null) return Vector2.zero;
        float x = (coord.x + 0.5f) * config.cellSize;
        float y = coord.y == 1 ? config.flyHeight : 0f;
        return new Vector2(x, y);
    }

    /// <summary>小区块全局 x → 大区块索引。</summary>
    public int CellToRegionIndex(int cellX)
    {
        if (config == null) return 0;
        return cellX / config.regionCellCount;
    }

    // ===== NPC 进出（堆叠上限）=====

    /// <summary>单位尝试进入区块。超过堆叠上限返回 false（排队等待）。</summary>
    public bool TryEnter(UnitController unit, GridCoord coord)
    {
        var cell = GetOrCreateCell(coord);
        int limit = config != null ? config.GetStackLimit(unit.GetCategory()) : 0;
        if (limit > 0 && cell.Count >= limit)
        {
            // 检查同类型是否超限
            if (cell.CountByCategory(unit.GetCategory()) >= limit)
                return false;
        }

        // 退出旧区块
        ExitCurrentCell(unit);

        cell.Add(unit);
        _unitCells[unit] = coord;
        return true;
    }

    /// <summary>退出当前区块。</summary>
    public void ExitCurrentCell(UnitController unit)
    {
        if (_unitCells.TryGetValue(unit, out var coord))
        {
            if (_cells.TryGetValue(coord, out var cell))
                cell.Remove(unit);
            _unitCells.Remove(unit);
        }
    }

    // ===== 查询（攻击命中用）=====

    /// <summary>获取区块内所有单位。</summary>
    public List<UnitController> GetUnitsInCell(GridCoord coord)
    {
        if (_cells.TryGetValue(coord, out var cell))
            return new List<UnitController>(cell.Units);
        return new List<UnitController>();
    }

    /// <summary>获取单位当前所在坐标。</summary>
    public GridCoord? GetUnitCoord(UnitController unit)
    {
        if (_unitCells.TryGetValue(unit, out var coord))
            return coord;
        return null;
    }

    /// <summary>获取区块内指定类型的单位。</summary>
    public List<UnitController> GetUnitsInCellByCategory(GridCoord coord, UnitCategory category)
    {
        var result = new List<UnitController>();
        if (_cells.TryGetValue(coord, out var cell))
        {
            for (int i = 0; i < cell.Units.Count; i++)
                if (cell.Units[i].GetCategory() == category)
                    result.Add(cell.Units[i]);
        }
        return result;
    }

    // ===== 清理 =====

    /// <summary>移除单位（死亡/销毁时调）。</summary>
    public void RemoveUnit(UnitController unit)
    {
        ExitCurrentCell(unit);
    }

    /// <summary>清空所有区块（跨岛切换/回主菜单时调）。</summary>
    public void ClearAll()
    {
        _cells.Clear();
        _unitCells.Clear();
    }

    // ===== 建筑占用层（3.3.1 P1）=====

    /// <summary>该格是否被建筑占用。</summary>
    public bool IsOccupied(GridCoord coord)
    {
        return _cells.TryGetValue(coord, out var cell) && cell.occupant != null;
    }

    /// <summary>该格是否为障碍（占用且 isObstacle=true）。</summary>
    public bool IsObstacle(GridCoord coord)
    {
        return _cells.TryGetValue(coord, out var cell) && cell.isObstacle;
    }

    /// <summary>获取占据该格的建筑（null=空）。</summary>
    public Building GetOccupant(GridCoord coord)
    {
        return _cells.TryGetValue(coord, out var cell) ? cell.occupant : null;
    }

    /// <summary>标记单格被建筑占用。footprint 多格建筑由调用方循环或用 MarkOccupiedFootprint。</summary>
    public void MarkOccupied(GridCoord coord, Building building)
    {
        var cell = GetOrCreateCell(coord);
        cell.occupant = building;
        cell.isObstacle = building != null && building.isObstacle;
    }

    /// <summary>便利方法：标记 cellWidth 格（从 origin 起 x 方向）被同一建筑占用。</summary>
    public void MarkOccupiedFootprint(GridCoord origin, int cellWidth, Building building)
    {
        for (int i = 0; i < cellWidth; i++)
        {
            MarkOccupied(new GridCoord(origin.x + i, origin.y), building);
        }
    }

    /// <summary>释放单格占用。</summary>
    public void Free(GridCoord coord)
    {
        if (_cells.TryGetValue(coord, out var cell))
        {
            cell.occupant = null;
            cell.isObstacle = false;
        }
    }

    /// <summary>便利方法：释放 cellWidth 格（从 origin 起 x 方向）。</summary>
    public void FreeFootprint(GridCoord origin, int cellWidth)
    {
        for (int i = 0; i < cellWidth; i++)
        {
            Free(new GridCoord(origin.x + i, origin.y));
        }
    }

    // ===== 地形查询（3.3.1 P1 / C4）=====

    /// <summary>查询某格地形。由 coord.x → 大区块索引 → Region.terrain。</summary>
    public TerrainType GetTerrainAt(GridCoord coord)
    {
        if (_activeMap == null) return TerrainType.Plain;
        int regionIdx = CellToRegionIndex(coord.x);
        if (regionIdx < 0 || regionIdx >= _activeMap.regions.Count) return TerrainType.Plain;
        var region = _activeMap.regions[regionIdx];

        // 缓存到 GridCell 供后续查询
        var cell = GetOrCreateCell(coord);
        cell.terrain = region.terrain;
        return region.terrain;
    }

    /// <summary>查询某格平原子状态（仅 terrain==Plain 时有效）。</summary>
    public PlainSubState GetPlainSubStateAt(GridCoord coord)
    {
        if (_activeMap == null) return PlainSubState.Normal;
        int regionIdx = CellToRegionIndex(coord.x);
        if (regionIdx < 0 || regionIdx >= _activeMap.regions.Count) return PlainSubState.Normal;
        return _activeMap.regions[regionIdx].plainSubState;
    }

    // ===== 按当前活跃地图填充 =====

    /// <summary>按地图数据填充区块（跨岛切换时调）。</summary>
    public void PopulateFromMap(MapData map)
    {
        ClearAll();
        _activeMap = map;
        // 区块按需懒创建（GetOrCreateCell），单位 TryEnter 时自动创建
        // 这里只记录活跃地图供 Gizmos 用
    }

    // ===== 内部辅助 =====

    GridCell GetOrCreateCell(GridCoord coord)
    {
        if (!_cells.TryGetValue(coord, out var cell))
        {
            cell = new GridCell { Coord = coord };
            _cells[coord] = cell;
        }
        return cell;
    }

    // ===== 调试 Gizmos（3.2 第十二节 + 3.2.1 第十节 5区可视化）=====

    private void OnDrawGizmos()
    {
        if (_activeMap == null || config == null) return;

        int M = _activeMap.regions.Count;
        if (M == 0) return;

        float cs = config.cellSize;
        int rpc = config.regionCellCount;

        // 画小区块边界（黄色细线）
        Gizmos.color = new Color(1, 1, 0, 0.15f);
        int totalCells = M * rpc;
        for (int x = 0; x <= totalCells; x++)
        {
            float wx = x * cs;
            Gizmos.DrawLine(new Vector3(wx, -2, 0), new Vector3(wx, 2, 0));
        }

        // 画大区块边界 + 地形颜色（5区不同色）
        for (int i = 0; i < M; i++)
        {
            var region = _activeMap.regions[i];
            float startX = region.cellStartX * cs;
            float endX = startX + region.cellCount * cs;

            // 区颜色
            Color zoneColor = GetZoneColor(region.zone);
            Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.3f);
            // 画区底色方块
            Vector3 center = new Vector3((startX + endX) / 2f, 0, 0);
            Vector3 size = new Vector3(endX - startX, 1.5f, 0.01f);
            Gizmos.DrawCube(center, size);

            // 区边界（粗线）
            Gizmos.color = new Color(zoneColor.r, zoneColor.g, zoneColor.b, 0.8f);
            Gizmos.DrawLine(new Vector3(startX, -2, 0), new Vector3(startX, 2, 0));
            if (i == M - 1)
                Gizmos.DrawLine(new Vector3(endX, -2, 0), new Vector3(endX, 2, 0));

            // 资源点标记
            if (region.resources != null)
            {
                foreach (var b in region.resources)
                {
                    float wx = (region.cellStartX + b.localCellX + 0.5f) * cs;
                    Color bc = GetBuildingColor(b.type);
                    Gizmos.color = bc;
                    Gizmos.DrawSphere(new Vector3(wx, 0.5f, 0), 0.3f);
                }
            }

            // 裂隙标记（红色 X）
            if (region.riftCellX >= 0)
            {
                float wx = (region.cellStartX + region.riftCellX + 0.5f) * cs;
                Gizmos.color = Color.red;
                float s = 0.5f;
                Gizmos.DrawLine(new Vector3(wx - s, 0.5f - s, 0), new Vector3(wx + s, 0.5f + s, 0));
                Gizmos.DrawLine(new Vector3(wx - s, 0.5f + s, 0), new Vector3(wx + s, 0.5f - s, 0));
            }
        }

        // 主城标记（金色方块）-- 3.2.1 第 2.4 节
        var (castleA, castleB) = MapGenRules.GetCastleRegionIndices(M);
        foreach (int ci in new[] { castleA, castleB })
        {
            if (ci < 0 || ci >= M) continue;
            var cr = _activeMap.regions[ci];
            float cwx = (cr.cellStartX + cr.cellCount / 2f) * cs;
            Gizmos.color = new Color(0.8f, 0.7f, 0.2f);
            Gizmos.DrawCube(new Vector3(cwx, 0.5f, 0), new Vector3(cs * 2, 0.8f, 0.8f));
        }
    }

    Color GetZoneColor(MapZone zone)
    {
        switch (zone)
        {
            case MapZone.LeftExtreme:
            case MapZone.RightExtreme:
                return new Color(0.6f, 0.3f, 0.3f); // 暗红（极端区）
            case MapZone.LeftResource:
            case MapZone.RightResource:
                return new Color(0.3f, 0.5f, 0.3f); // 深绿（资源区）
            case MapZone.Center:
                return new Color(0.8f, 0.7f, 0.2f);  // 金色（中心区）
            default:
                return Color.gray;
        }
    }

    Color GetBuildingColor(BuildingType type)
    {
        switch (type)
        {
            case BuildingType.Tree:
            case BuildingType.WoodPile:
                return Color.green;      // 木=绿
            case BuildingType.Mine:
            case BuildingType.StonePile:
            case BuildingType.OreVein:
                return Color.magenta;     // 石=紫
            case BuildingType.Farmland:
                return Color.yellow;      // 粮=黄
            case BuildingType.TreasureBox:
            case BuildingType.Ruins:
                return Color.cyan;       // 特殊=青
            default:
                return Color.white;
        }
    }
}
