using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 运行时 2D 网格管理器（改造计划 doc 1 §5.2/§5.3）。
/// 稠密数组存储 + 中心原点坐标换算（WorldToCoord 返回 null=越界 D2）+ 微格 + footprint 矩形。
/// 无状态索引：不存档。实现 IPathGrid（2_6 寻路唯一依赖口）。
/// </summary>
public class GridSystem : Singleton<GridSystem>, IPathGrid
{
    [SerializeField] private GridConfig config;

    // ===== 分层存储布局（doc 1 §5.3）=====
    private int _w, _h;
    private TerrainType[]   _terrain;      // W×H
    private PlainSubState[] _plainSub;     // W×H
    private WalkFlags[]     _walkFlags;    // W×H
    private IGridOccupant[]  _occupants;   // W×H（footprint 每格同引用，2_14 A⁻ 泛化非 Building）
    private GridCell[]      _cells;        // W×H，懒分配 null 起步
    private readonly Dictionary<UnitController, GridCoord> _unitSubCells = new Dictionary<UnitController, GridCoord>();

    public GridConfig Config => config;

    public int MapWidth  => _w;
    public int MapHeight => _h;

    /// <summary>过渡：旧消费方（LOD 等）读总格数，2_4 重写后移除。</summary>
    public int MapCellCount => _w * _h;

    // ===== IPathGrid（微格坐标）=====
    public int Width  => _w * Config.subCellDivisor;
    public int Height => _h * Config.subCellDivisor;

    protected override void Awake()
    {
        base.Awake();
        if (config == null)
            config = Resources.Load<GridConfig>("Grid/GridConfig");
    }

    private int ToIndex(int x, int y) => y * _w + x;
    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;
    public bool IsInBounds(GridCoord c) => InBounds(c.x, c.y);

    // ===== 生命周期 =====
    public void Initialize(int w, int h)
    {
        _w = w; _h = h;
        int n = w * h;
        _terrain   = new TerrainType[n];
        _plainSub  = new PlainSubState[n];
        _walkFlags = new WalkFlags[n];
        _occupants = new Building[n];
        _cells     = new GridCell[n];
        _unitSubCells.Clear();
    }

    public void PopulateFromMap(MapData map)
    {
        Initialize(map.width, map.height);
        // 2_1 §1.3：features 为唯一功能源，terrain/plainSub/walkFlags 全部由此派生
        if (map.features != null && map.features.Length == _w * _h)
        {
            for (int i = 0; i < _terrain.Length; i++)
            {
                var f = map.features[i];
                _terrain[i] = FeatureToTerrain(f);
                _plainSub[i] = PlainSubState.Normal;
                _walkFlags[i] = FeatureToWalkFlags(f);
            }
        }
    }

    // ===== 2_1 §5.1 FeatureType→网格层派生映射（features 唯一功能源）=====
    private static TerrainType FeatureToTerrain(FeatureType f)
    {
        switch (f)
        {
            case FeatureType.Plain: return TerrainType.Plain;
            case FeatureType.Tree: return TerrainType.Forest;
            case FeatureType.Mountain: return TerrainType.Mountain;
            case FeatureType.SnowMountain: return TerrainType.Snow;
            case FeatureType.Mine: return TerrainType.Quarry;
            // 一次性资源坑位落在可走地皮上（对应地皮，视觉变体归 2_10）
            case FeatureType.OreVein: case FeatureType.StonePile: case FeatureType.WoodPile: return TerrainType.Plain;
            case FeatureType.River: return TerrainType.River;
            case FeatureType.Lake: return TerrainType.Lake;
            case FeatureType.Ocean: return TerrainType.Ocean;
            default: return TerrainType.Plain;
        }
    }

    /// <summary>A+ 资源节点数据覆盖：按 feature 重刷单格 terrain/plainSub/walkFlags（保留 occupant）。</summary>
    public void RefreshCellFromFeature(GridCoord coord, FeatureType f)
    {
        if (coord.x < 0 || coord.y < 0 || coord.x >= _w || coord.y >= _h) return;
        int i = coord.y * _w + coord.x;
        if (i < 0 || i >= _terrain.Length) return;
        _terrain[i] = FeatureToTerrain(f);
        if (i < _plainSub.Length) _plainSub[i] = PlainSubState.Normal;
        if (i < _walkFlags.Length) _walkFlags[i] = FeatureToWalkFlags(f);
    }

    private static WalkFlags FeatureToWalkFlags(FeatureType f)
    {
        switch (f)
        {
            // 可走：平原/树/矿洞/一次性资源（Locked 由 2_2/2_7 按需置位）
            case FeatureType.Plain: case FeatureType.Tree: case FeatureType.Mine:
            case FeatureType.OreVein: case FeatureType.StonePile: case FeatureType.WoodPile:
                return WalkFlags.TerrainWalkable;
            // 水域阻挡（桥由 2_2 置 Bridge 位覆盖）
            case FeatureType.River: case FeatureType.Lake: case FeatureType.Ocean:
                return WalkFlags.Water;
            // 山地/雪山阻挡（无 TerrainWalkable 位）
            default: return WalkFlags.None;
        }
    }

    private static bool TerrainIsWalkable(TerrainType t)
    {
        switch (t)
        {
            case TerrainType.Plain: case TerrainType.Forest: case TerrainType.Quarry: return true;
            default: return false;
        }
    }

    public void ClearAll()
    {
        _w = _h = 0;
        _terrain = null;
        _plainSub = null;
        _walkFlags = null;
        _occupants = null;
        _cells = null;
        _unitSubCells.Clear();
    }

    // ===== 坐标换算（HH.3 裁决 2026-08-22 统一 iso：世界坐标=等轴嵌入，与 MapRenderService.GridToIso/IsoToCell 同一映射，doc 1 §1.6）=====
    public GridCoord? WorldToCoord(Vector2 pos)
    {
        if (config == null || _w <= 0 || _h <= 0) return null;
        float cellW = config.cellSize.x, cellH = config.cellSize.y;
        float halfW = cellW * 0.5f, halfH = cellH * 0.5f;
        // 由 wx=(gx-gy)*halfW, wy=(gx+gy)*halfH 反解（同 IsoToCell，origin-free）
        float gx = pos.x / halfW * 0.5f + pos.y / halfH * 0.5f;
        float gy = pos.y / halfH * 0.5f - pos.x / halfW * 0.5f;
        int x = Mathf.FloorToInt(gx);
        int y = Mathf.FloorToInt(gy);
        return InBounds(x, y) ? new GridCoord(x, y) : (GridCoord?)null;
    }

    public Vector2 CoordToWorld(GridCoord coord)
    {
        float cellW = config != null ? config.cellSize.x : MapRenderService.DefaultCellSize.x;
        float cellH = config != null ? config.cellSize.y : MapRenderService.DefaultCellSize.y;
        return new Vector2((coord.x - coord.y) * cellW * 0.5f,
                           (coord.x + coord.y) * cellH * 0.5f);
    }

    // ===== 微格 SubCell（HH.3 裁决 2026-08-22 统一 iso：与 CoordToWorld 同一条 iso 映射，doc 1 §1.6）=====
    public GridCoord? WorldToSubCoord(Vector2 pos)
    {
        if (config == null || _w <= 0 || _h <= 0) return null;
        int div = config.subCellDivisor > 0 ? config.subCellDivisor : 4;
        float subW = config.cellSize.x / div, subH = config.cellSize.y / div;
        float halfW = subW * 0.5f, halfH = subH * 0.5f;
        float gx = pos.x / halfW * 0.5f + pos.y / halfH * 0.5f;
        float gy = pos.y / halfH * 0.5f - pos.x / halfW * 0.5f;
        int subWCount = _w * div, subHCount = _h * div;
        int sx = Mathf.FloorToInt(gx);
        int sy = Mathf.FloorToInt(gy);
        return (sx >= 0 && sy >= 0 && sx < subWCount && sy < subHCount) ? new GridCoord(sx, sy) : (GridCoord?)null;
    }

    public Vector2 SubCoordToWorld(GridCoord sub)
    {
        float cellW = config != null ? config.cellSize.x : MapRenderService.DefaultCellSize.x;
        float cellH = config != null ? config.cellSize.y : MapRenderService.DefaultCellSize.y;
        int div = config != null && config.subCellDivisor > 0 ? config.subCellDivisor : 4;
        float subW = cellW / div, subH = cellH / div;
        return new Vector2((sub.x - sub.y) * subW * 0.5f,
                           (sub.x + sub.y) * subH * 0.5f);
    }

    public GridCoord SubToCell(GridCoord sub)
    {
        int div = config != null && config.subCellDivisor > 0 ? config.subCellDivisor : 4;
        return new GridCoord(sub.x / div, sub.y / div, sub.layer);
    }

    public GridCoord CellToSub(GridCoord cell, int sx, int sy)
    {
        int div = config != null && config.subCellDivisor > 0 ? config.subCellDivisor : 4;
        return new GridCoord(cell.x * div + sx, cell.y * div + sy, cell.layer);
    }

    public bool IsSubWalkable(GridCoord sub)
    {
        var cell = SubToCell(sub);
        if (!IsWalkable(cell)) return false;
        // 精确 footprint 覆盖推导归 2_6；此处分块粒度近似（建筑阻挡即整格不可走）
        return !IsObstacle(cell);
    }

    // ===== 地形 / 可行走层 =====
    public TerrainType GetTerrainAt(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _terrain == null) return TerrainType.Plain;
        return _terrain[ToIndex(c.x, c.y)];
    }

    public PlainSubState GetPlainSubStateAt(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _plainSub == null) return PlainSubState.Normal;
        return _plainSub[ToIndex(c.x, c.y)];
    }

    public WalkFlags GetWalkFlags(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _walkFlags == null) return WalkFlags.None;
        return _walkFlags[ToIndex(c.x, c.y)];
    }

    public void SetTerrain(GridCoord c, TerrainType t, PlainSubState sub = PlainSubState.Normal)
    {
        if (!InBounds(c.x, c.y) || _terrain == null) return;
        int i = ToIndex(c.x, c.y);
        _terrain[i] = t;
        _plainSub[i] = sub;
        _walkFlags[i] = TerrainIsWalkable(t) ? WalkFlags.TerrainWalkable : WalkFlags.None;
    }

    public bool IsWalkable(GridCoord c)
    {
        var f = GetWalkFlags(c);
        return (f & WalkFlags.TerrainWalkable) != 0
            && (f & (WalkFlags.BuildingBlocked | WalkFlags.Locked | WalkFlags.Water)) == 0
            || (f & WalkFlags.Bridge) != 0;
    }

    // ===== 建筑占用层 =====
    public bool IsOccupied(GridCoord c) => InBounds(c.x, c.y) && _occupants != null && _occupants[ToIndex(c.x, c.y)] != null;

    public bool IsObstacle(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _occupants == null) return false;
        var o = _occupants[ToIndex(c.x, c.y)];
        return o != null && o.IsGridObstacle;
    }

    public IGridOccupant GetOccupant(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _occupants == null) return null;
        return _occupants[ToIndex(c.x, c.y)];
    }

    public void MarkOccupied(GridCoord c, IGridOccupant occupant)
    {
        if (!InBounds(c.x, c.y) || _occupants == null) return;
        int i = ToIndex(c.x, c.y);
        _occupants[i] = occupant;
        if (occupant != null)
        {
            if (occupant.IsGridObstacle) _walkFlags[i] |= WalkFlags.BuildingBlocked;
            else _walkFlags[i] &= ~WalkFlags.BuildingBlocked;
            MarkCellOccupied(c);
        }
        else
        {
            _walkFlags[i] &= ~WalkFlags.BuildingBlocked;
        }
    }

    public void Free(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _occupants == null) return;
        int i = ToIndex(c.x, c.y);
        _occupants[i] = null;
        _walkFlags[i] &= ~WalkFlags.BuildingBlocked;
    }

    public void MarkOccupiedFootprint(GridCoord origin, int w, int h, IGridOccupant occupant)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                MarkOccupied(new GridCoord(origin.x + dx, origin.y + dy, origin.layer), occupant);
    }

    public void FreeFootprint(GridCoord origin, int w, int h)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
                Free(new GridCoord(origin.x + dx, origin.y + dy, origin.layer));
    }

    /// <summary>置/清桥面位（2_2 桥放置/拆除）。Bridge 置位后 IsWalkable 豁免 Water 阻挡（doc 1 §5.1）。</summary>
    public void SetBridge(GridCoord origin, int w, int h, bool on)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                var c = new GridCoord(origin.x + dx, origin.y + dy, origin.layer);
                if (!InBounds(c.x, c.y) || _walkFlags == null) continue;
                int i = ToIndex(c.x, c.y);
                if (on) _walkFlags[i] |= WalkFlags.Bridge;
                else _walkFlags[i] &= ~WalkFlags.Bridge;
            }
    }

    public bool IsFootprintClear(GridCoord origin, int w, int h)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                var c = new GridCoord(origin.x + dx, origin.y + dy, origin.layer);
                // 阻挡（地形/建筑/水域）或已被占用均不可摆放（doc 1 R6）
                if (!IsWalkable(c) || IsOccupied(c)) return false;
            }
        return true;
    }

    // ===== 单位层（微格登记，doc 1 §5.2 单位层）=====
    public bool TryEnter(UnitController unit, GridCoord subCoord)
    {
        var prev = _unitSubCells.TryGetValue(unit, out var old) ? (GridCoord?)old : null;
        _unitSubCells[unit] = subCoord;

        // 跨界检测 → 发 EnemyEnteredChunkEvent（聚焦到 cell 级开始，精确微格事件归 2_7）
        bool crossedChunk = !prev.HasValue || CellToChunk(subCoord) != CellToChunk(prev.Value);
        if (crossedChunk && unit.GetFaction() == Faction.Undead)
            EventBus.Publish(new EnemyEnteredChunkEvent(new Vector2Int(CellToChunk(subCoord).x, CellToChunk(subCoord).y), unit));
        return true;
    }

    public void ExitCurrentCell(UnitController unit) { _unitSubCells.Remove(unit); }

    public void RemoveUnit(UnitController unit) { _unitSubCells.Remove(unit); }

    public GridCoord? GetUnitCoord(UnitController unit)
        => _unitSubCells.TryGetValue(unit, out var c) ? (GridCoord?)c : null;

    public List<UnitController> GetUnitsInSubCell(GridCoord sub)
    {
        var result = new List<UnitController>();
        foreach (var kv in _unitSubCells)
            if (kv.Value == sub) result.Add(kv.Key);
        return result;
    }

    public List<UnitController> GetUnitsInCell(GridCoord cell)
    {
        var result = new List<UnitController>();
        var cellIs = cell == default;
        foreach (var kv in _unitSubCells)
        {
            GridCoord s = kv.Value;
            if (SubToCell(s) == cell) result.Add(kv.Key);
        }
        return result;
    }

    public List<UnitController> GetUnitsInCellByCategory(GridCoord cell, UnitCategory category)
    {
        var result = new List<UnitController>();
        foreach (var kv in _unitSubCells)
        {
            if (SubToCell(kv.Value) != cell) continue;
            if (kv.Key.GetCategory() == category) result.Add(kv.Key);
        }
        return result;
    }

    public int FillUnitsInRect(RectInt subRect, List<UnitController> buffer)
    {
        int before = buffer.Count;
        foreach (var kv in _unitSubCells)
            if (subRect.Contains(new Vector2Int(kv.Value.x, kv.Value.y))) buffer.Add(kv.Key);
        return buffer.Count - before;
    }

    // ===== 分区 =====
    public Vector2Int CellToChunk(GridCoord c)
    {
        int cs = config != null && config.chunkSize > 0 ? config.chunkSize : 16;
        return new Vector2Int(c.x / cs, c.y / cs);
    }

    public Vector2Int CellToMidChunk(GridCoord c)
    {
        int ms = config != null && config.midChunkSize > 0 ? config.midChunkSize : 4;
        return new Vector2Int(c.x / ms, c.y / ms);
    }

    /// <summary>过渡：旧 1D 消费方按 x 取 region 索引，2_4/2_8 重写后移除。</summary>
    public int CellToRegionIndex(int cellX)
        => config != null && config.chunkSize > 0 ? cellX / config.chunkSize : 0;

    /// <summary>过渡：旧 1D 消费方按 x 取 midregion 索引。</summary>
    public int CellToMidRegionIndex(int cellX)
        => config != null && config.midChunkSize > 0 ? cellX / config.midChunkSize : 0;

    public IEnumerable<GridCoord> GetCellsInChunk(Vector2Int chunk)
    {
        int cs = config != null && config.chunkSize > 0 ? config.chunkSize : 16;
        for (int y = chunk.y * cs; y < (chunk.y + 1) * cs; y++)
            for (int x = chunk.x * cs; x < (chunk.x + 1) * cs; x++)
                if (InBounds(x, y)) yield return new GridCoord(x, y);
    }

    // ===== IPathGrid 实现 =====
    public bool IsDiagonalMoveAllowed(GridCoord from, GridCoord to)
    {
        int dx = Mathf.Abs(to.x - from.x), dy = Mathf.Abs(to.y - from.y);
        if (dx == 0 || dy == 0) return true;
        var a = new GridCoord(from.x + System.Math.Sign(to.x - from.x), from.y, from.layer);
        var b = new GridCoord(from.x, from.y + System.Math.Sign(to.y - from.y), from.layer);
        return IsWalkable(a) && IsWalkable(b);
    }

    public float GetEnterCost(GridCoord subCoord)
    {
        // 地形代价表归 2_6；此处统一 1.0
        return 1f;
    }

    // ===== 内部辅助：GridCell 懒分配（承载单位列表，兼容旧消费方）=====
    private void MarkCellOccupied(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _cells == null) return;
        int i = ToIndex(c.x, c.y);
        if (_cells[i] == null) _cells[i] = new GridCell { Coord = c };
    }

    // ===== 过渡兼容：IsInsideWall（围合判定由 2_2 移除 / 2_7 接管；此处保留保守实现避免破坏消费方编译）=====
    public bool IsInsideWall(Vector2 worldPos) => false;

    // ===== Gizmos 2D（doc 1 §5.7，简易版）=====
    private void OnDrawGizmos()
    {
        if (config == null || !config.drawGizmos || _w <= 0 || _h <= 0) return;
        float cellW = config.cellSize.x, cellH = config.cellSize.y;

        // 地形色块（只画一格，避免全图开销；实际可按键决定）
        Gizmos.color = new Color(0, 0, 0, 0.05f);
        Gizmos.DrawCube(CoordToWorld(new GridCoord(_w / 2, _h / 2)), new Vector3(cellW, cellH, 0.01f));
    }
}