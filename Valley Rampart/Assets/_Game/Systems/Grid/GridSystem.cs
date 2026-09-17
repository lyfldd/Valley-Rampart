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
    // 【HH.294 片2-A/B】`_terrain`(TerrainType) 与 `_plainSub`(PlainSubState) 两派生数组**已删**
    //   （`02_空间与粒度` §六：整层删）——地块属性一律**直读地表物** features（`_features` 引用，非拷贝）。
    // 【HH.294 片3-B】⭐ 三个派生数组**已下移到小格子**（`02_空间与粒度` §2.4「逻辑最小单位＝小格子」）：
    //   改前：`W×H`（**地块级**；384² 大图＝147,456 格）；改后：`SW×SH`（**小格子级**；384² 大图＝1536²＝2,359,296 格，面积 ×16）。
    //   地块级读写口**签名不变**（消费端零改动），代表位＝「该地块的**首子格**」（sx=x*div, sy=y*div）；
    //   写入类口（MarkOccupied/Free/SetBridge/MarkOccupiedFootprint/FreeFootprint）按**地块块**整块写（div² 子格 ⇒ 建筑尺寸「内部 ×16」）。
    //   当前全库写入方皆**地块对齐**（地表物按地块派生／footprint 按地块／桥按地块）⇒ 同一地块的 div² 个子格**恒同值**
    //   ⇒ 地块级口与改前**逐格等价**（探针全图 2,359,296 子格扫描 `0` 不一致为证）。
    private int _w, _h;                    // 地块级（W×H）
    private int _sw, _sh;                  // 小格子级（SW×SH = _w×div, _h×div）
    private FeatureType[]  _features;      // 地块级 W×H（= MapData.features 同引用；唯一功能源，只读）
    private WalkFlags[]     _walkFlags;    // ⭐ 小格子级 SW×SH（派生缓存：地表物 + 占格 + 桥）
    private IGridOccupant[]  _occupants;   // ⭐ 小格子级 SW×SH（footprint 每子格同引用，2_14 A⁻ 泛化非 Building）
    private GridCell[]      _cells;        // ⭐ 小格子级 SW×SH，懒分配 null 起步（**按地块首子格**分配 ⇒ 壳对象数与改前一致）
    private readonly Dictionary<UnitController, GridCoord> _unitSubCells = new Dictionary<UnitController, GridCoord>();

    public GridConfig Config => config;

    public int MapWidth  => _w;
    public int MapHeight => _h;

    /// <summary>过渡：旧消费方（LOD 等）读总格数，2_4 重写后移除。（**地块级**总格数 W×H）</summary>
    public int MapCellCount => _w * _h;

    // ===== IPathGrid（微格坐标）=====
    public int Width  => _w * SubDiv;
    public int Height => _h * SubDiv;

    /// <summary>1 地块边长的小格子数（＝子格/地块换算的**唯一除数**；缺配置兜底 4）。</summary>
    public int SubDiv => config != null && config.subCellDivisor > 0 ? config.subCellDivisor : 4;

    protected override void Awake()
    {
        base.Awake();
        if (config == null)
            config = Resources.Load<GridConfig>("Grid/GridConfig");
    }

    // ===== 索引与界（片3-B：地块级 / 小格子级**两套**，别混用）=====
    private int ToIndex(int x, int y) => y * _w + x;                       // 地块级索引（features 用）
    private int ToSubIndex(int sx, int sy) => sy * _sw + sx;               // 小格子级索引（三派生数组用）
    /// <summary>地块 (x,y) 的**首子格**线性索引＝地块级读写口的代表位。</summary>
    private int CellBaseIndex(int x, int y) => (y * SubDiv) * _sw + x * SubDiv;
    private bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < _w && y < _h;
    private bool InSubBounds(int sx, int sy) => sx >= 0 && sy >= 0 && sx < _sw && sy < _sh;
    public bool IsInBounds(GridCoord c) => InBounds(c.x, c.y);

    // ===== 生命周期 =====
    public void Initialize(int w, int h)
    {
        _w = w; _h = h;
        int div = SubDiv;
        _sw = w * div; _sh = h * div;
        int n = _sw * _sh;                    // ⭐ 小格子级（片3-B；384² 大图 ⇒ 2,359,296）
        _walkFlags = new WalkFlags[n];
        _occupants = new IGridOccupant[n];   // 2_14 A⁻：分配类型与声明 IGridOccupant[] 对齐（修 Portal 占格崩）
        _cells     = new GridCell[n];
        _unitSubCells.Clear();
    }

    public void PopulateFromMap(MapData map)
    {
        Initialize(map.width, map.height);
        // 2_1 §1.3：features 为唯一功能源 —— walkFlags 由此派生（terrain/plainSub 派生数组已删，HH.294 片2）
        _features = map.features;
        if (map.features != null && map.features.Length == _w * _h)
        {
            // 片3-B：地块级 features → 小格子级 walkFlags（每地块 div² 子格同写）
            int div = SubDiv;
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    WalkFlags wf = FeatureToWalkFlags(MapGate.ReadAt(map, x, y));   // 【HH.294 片4·4-A】读走查门
                    int baseIdx = (y * div) * _sw + x * div;
                    for (int sy = 0; sy < div; sy++)
                        for (int sx = 0; sx < div; sx++)
                            _walkFlags[baseIdx + sy * _sw + sx] = wf;
                }
        }
    }

    // ===== 2_1 §5.1 FeatureType→网格层派生映射（features 唯一功能源）=====

    /// <summary>A+ 资源节点数据覆盖：按 feature 重刷单格 walkFlags（保留 occupant）。片3-B：整块 div² 子格同写。</summary>
    public void RefreshCellFromFeature(GridCoord coord, FeatureType f)
    {
        if (!InBounds(coord.x, coord.y) || _walkFlags == null) return;
        int div = SubDiv;
        WalkFlags wf = FeatureToWalkFlags(f);
        int baseIdx = CellBaseIndex(coord.x, coord.y);
        for (int sy = 0; sy < div; sy++)
            for (int sx = 0; sx < div; sx++)
            {
                int i = baseIdx + sy * _sw + sx;
                if (i >= 0 && i < _walkFlags.Length) _walkFlags[i] = wf;
            }
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
            case FeatureType.River: case FeatureType.Ocean:
                return WalkFlags.Water;
            // 山地/雪山阻挡（无 TerrainWalkable 位）
            default: return WalkFlags.None;
        }
    }

    public void ClearAll()
    {
        _w = _h = 0;
        _sw = _sh = 0;
        _features = null;
        _walkFlags = null;
        _occupants = null;
        _cells = null;
        _unitSubCells.Clear();
    }

    /// <summary>渲染/无实例场景用的格尺寸（config 缺失时回退渲染层默认，与改前一致）。</summary>
    private Vector2 CellSizeOr() => config != null ? config.cellSize : MapRenderService.DefaultCellSize;

    /// <summary>一格小格子的世界尺寸（＝cellSize ÷ div）。</summary>
    private Vector2 SubCellSizeOr() => CellSizeOr() / SubDiv;

    // ============================================================================
    //  【HH.294 片3-A】⭐ 坐标换算 —— **全库唯一入口**（`02` §一：换算「×16/÷4 全库只允许一处」）
    //
    //  ⚠️ 调用前先认清**域**：同一格号在不同域指的不是同一个东西，混域＝静默错答（HH.140 P7 实锤）。
    //     域 ①**世界坐标**（Vector2，等轴嵌入）：渲染 / 物理 / 单位 transform
    //     域 ②**地块格号**（GridCoord 整数，0..W-1）：地皮瓦片 / `features` / footprint / 建筑 coord
    //     域 ③**小格子格号**（GridCoord 整数，0..SW-1）：寻路 / 可走 / 占格 / 落位 / 采集寻址
    //
    //  ⭐ 何时用哪个（规则表 —— 新增调用点照此选，别自己推）：
    //  ┌──────────────────────────────────────────────┬────────────────────────┬─────────┐
    //  │ 你要什么                                      │ 用哪个                  │ 返回域   │
    //  ├──────────────────────────────────────────────┼────────────────────────┼─────────┤
    //  │ 世界 → 地块格号（越界 ⇒ null）                 │ `WorldToCoord`          │ 地块     │
    //  │ 世界 → 小格子格号（越界 ⇒ null）                │ `WorldToSubCoord`       │ 小格子   │
    //  │ 世界 → 格号（**不校验越界**，调用方自己 clamp）  │ `WorldToCell`（静态）   │ 地块     │
    //  │ 地块格号 → 世界（格点＝等轴菱形下顶点）          │ `CoordToWorld`          │ 世界     │
    //  │ 小格子格号 → 世界（格点）                       │ `SubCoordToWorld`       │ 世界     │
    //  │ 小格子格号 → 地块格号                          │ `SubToCell`             │ 跨域     │
    //  │ 地块格号 → 小格子格号（＋格内偏移 sx,sy）        │ `CellToSub`             │ 跨域     │
    //  │ 地块格号 ＋ footprint(地块) → **世界中心点**     │ `FootprintCenterWorld`  │ 世界     │
    //  │ 连续格号（小数）→ 世界                          │ `CellToWorldF`（静态）  │ 世界     │
    //  │ 世界 → 连续格号（小数，不 floor）                │ `WorldToCellF`（静态）  │ 跨域     │
    //  └──────────────────────────────────────────────┴────────────────────────┴─────────┘
    //
    //  ⛔ **禁**把算式抄进调用方「就地展开」（`(x−y)*halfW, (x+y)*halfH` ／ `(fp−1)*0.5*cellSize`）：
    //     反面教材＝`FootprintCenterWorld` 曾在 4 处各写一套、`MapRenderService.GridToIso/IsoToCell`
    //     曾与 `CoordToWorld/WorldToCoord` 两套并行 —— 片3-A 后二者**只做转调**，算式只此一处。
    //  ✅ 静态内核（`CellToWorldF`/`CellToWorld`/`WorldToCellF`/`WorldToCell`）供**无 GridSystem 实例**的场景
    //     （编辑器预览 / 渲染层纯投影）复用；有实例时一律走上表的实例口。
    // ============================================================================

    /// <summary>【换算内核·唯一】连续格号（可小数）→ 世界坐标。`cellSize`＝一格的世界尺寸。</summary>
    public static Vector2 CellToWorldF(float gx, float gy, Vector2 cellSize)
        => new Vector2((gx - gy) * cellSize.x * 0.5f, (gx + gy) * cellSize.y * 0.5f);

    /// <summary>【换算内核·唯一】格号 → 世界坐标（格点＝等轴菱形下顶点）。</summary>
    public static Vector2 CellToWorld(GridCoord g, Vector2 cellSize) => CellToWorldF(g.x, g.y, cellSize);

    /// <summary>【换算内核·唯一】世界坐标 → 连续格号（可小数；**不 floor、不校验越界**）。</summary>
    public static Vector2 WorldToCellF(Vector2 pos, Vector2 cellSize)
    {
        float halfW = cellSize.x * 0.5f, halfH = cellSize.y * 0.5f;
        return new Vector2(pos.x / halfW * 0.5f + pos.y / halfH * 0.5f,
                           pos.y / halfH * 0.5f - pos.x / halfW * 0.5f);
    }

    /// <summary>【换算内核·唯一】世界坐标 → 格号（floor；**不校验越界**，调用方 clamp）。</summary>
    public static GridCoord WorldToCell(Vector2 pos, Vector2 cellSize)
    {
        Vector2 g = WorldToCellF(pos, cellSize);
        return new GridCoord(Mathf.FloorToInt(g.x), Mathf.FloorToInt(g.y));
    }

    /// <summary>【换算内核·唯一】地块格号 ＋ footprint(地块) → **世界中心点**（origin 格点 ＋ (fp−1)/2 格偏移）。
    /// ⚠️ 该式＝**视觉居中**约定，别处不得再抄（片3-A 前曾在 4 处各写一套）。</summary>
    public static Vector3 FootprintCenterWorld(GridCoord origin, Vector2Int fp, Vector2 cellSize)
        => CellToWorld(origin, cellSize) + new Vector2((fp.x - 1) * 0.5f * cellSize.x,
                                                       (fp.y - 1) * 0.5f * cellSize.y);

    /// <summary>【便捷】用**当前单例配置**算 footprint 世界中心点；无单例 / 无配置 ⇒ 返回 `fallback`
    /// （各调用点兜底值不同，故由调用方传入）。调用方不必再各自判空。</summary>
    public static Vector3 FootprintCenterWorld(GridCoord origin, Vector2Int fp, Vector3 fallback)
        => Instance != null && Instance.Config != null
           ? FootprintCenterWorld(origin, fp, Instance.Config.cellSize)
           : fallback;

    // ========================================================================
    //  以下为实例口（＝上表；算式一律转调内核，本类内也无第二份）
    // ========================================================================

    /// <summary>世界 → 地块格号；越界返回 null（doc 1 D2）。</summary>
    public GridCoord? WorldToCoord(Vector2 pos)
    {
        if (config == null || _w <= 0 || _h <= 0) return null;
        Vector2 g = WorldToCellF(pos, CellSizeOr());
        int x = Mathf.FloorToInt(g.x), y = Mathf.FloorToInt(g.y);
        return InBounds(x, y) ? new GridCoord(x, y) : (GridCoord?)null;
    }

    /// <summary>地块格号 → 世界坐标（格点）。</summary>
    public Vector2 CoordToWorld(GridCoord coord) => CellToWorld(coord, CellSizeOr());

    /// <summary>世界 → 小格子格号；越界返回 null。</summary>
    public GridCoord? WorldToSubCoord(Vector2 pos)
    {
        if (config == null || _w <= 0 || _h <= 0) return null;
        Vector2 g = WorldToCellF(pos, SubCellSizeOr());
        int sx = Mathf.FloorToInt(g.x), sy = Mathf.FloorToInt(g.y);
        return InSubBounds(sx, sy) ? new GridCoord(sx, sy) : (GridCoord?)null;
    }

    /// <summary>小格子格号 → 世界坐标（格点）。</summary>
    public Vector2 SubCoordToWorld(GridCoord sub) => CellToWorldF(sub.x, sub.y, SubCellSizeOr());

    /// <summary>小格子格号 → 地块格号（整数除法；子格域恒非负 ⇒ 与 floor 同）。</summary>
    public GridCoord SubToCell(GridCoord sub)
    {
        int div = SubDiv;
        return new GridCoord(sub.x / div, sub.y / div, sub.layer);
    }

    /// <summary>地块格号 → 小格子格号（＋格内偏移 sx,sy；建筑落位 / 放置校验统一走此）。</summary>
    public GridCoord CellToSub(GridCoord cell, int sx, int sy)
    {
        int div = SubDiv;
        return new GridCoord(cell.x * div + sx, cell.y * div + sy, cell.layer);
    }

    /// <summary>小格子级可走（**精确到子格**；片3-A 前为「按地块近似」＝整格一体判定）。</summary>
    public bool IsSubWalkable(GridCoord sub)
    {
        if (_walkFlags == null || !InSubBounds(sub.x, sub.y)) return false;
        if (!WalkableOf(_walkFlags[ToSubIndex(sub.x, sub.y)])) return false;
        return !IsObstacleSub(sub);
    }

    // ===== 地块属性（地表物）/ 可行走层 =====
    // 【HH.294 片2-A】原 `GetTerrainAt`(TerrainType) 已随整层删除 —— 地块属性唯一读口＝**地表物**（features）。
    // 【HH.294 片3-B】派生数组已降到**小格子**；下列**地块级读口**签名不变（消费端零改动），
    //   代表位＝该地块**首子格**（`CellBaseIndex`）。当前写入方皆地块对齐 ⇒ 同地块 div² 子格恒同值 ⇒ 与改前等价。
    /// <summary>该格的地表物（FeatureType；越界/未装载返回 Plain）。唯一功能源＝MapData.features 同引用。</summary>
    public FeatureType GetFeatureAt(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _features == null) return FeatureType.Plain;
        return _features[ToIndex(c.x, c.y)];
    }

    /// <summary>可走判定（纯位测试；`TerrainWalkable` 且无 阻挡/锁/水 ，或 `Bridge`）。</summary>
    private static bool WalkableOf(WalkFlags f)
        => ((f & WalkFlags.TerrainWalkable) != 0
            && (f & (WalkFlags.BuildingBlocked | WalkFlags.Locked | WalkFlags.Water)) == 0)
           || (f & WalkFlags.Bridge) != 0;

    /// <summary>【地块级便捷口】该地块的可走位（读＝首子格；见类头 片3-B 声明）。</summary>
    public WalkFlags GetWalkFlags(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _walkFlags == null) return WalkFlags.None;
        return _walkFlags[CellBaseIndex(c.x, c.y)];
    }

    /// <summary>⭐【小格子级·精确】该子格的可走位（片3-B 新增读口）。</summary>
    public WalkFlags GetWalkFlagsSub(GridCoord sub)
    {
        if (!InSubBounds(sub.x, sub.y) || _walkFlags == null) return WalkFlags.None;
        return _walkFlags[ToSubIndex(sub.x, sub.y)];
    }

    /// <summary>【地块级便捷口】该地块是否可走（读＝首子格）。</summary>
    public bool IsWalkable(GridCoord c) => WalkableOf(GetWalkFlags(c));

    /// <summary>⭐【小格子级·精确】该子格是否可走。</summary>
    public bool IsWalkableSub(GridCoord sub) => WalkableOf(GetWalkFlagsSub(sub));

    // ===== 建筑占用层 =====
    // 【HH.294 片2-C】原 `IsOccupied` 已删（与 `GetOccupant(c) != null` 完全等价、重复接口）—— 占用判据统一走 GetOccupant。
    // 【HH.294 片3-B】占格数组已降到**小格子**；写入类口按**地块块**整块写（div² 子格）。

    /// <summary>【地块级便捷口】该地块是否有阻挡占格物。
    /// 【HH.294 片4·搭车 R3（`D770` 请裁-4）】**已迁移到子格域**：块内**任一**子格阻挡 ⇒ true
    /// （改前＝只读**首子格**代表位 ⇒ 「子格级部分占格」会**失真**为 false）。
    /// 快路径：代表位命中即返回（常见情形 1 次读）；否则扫同块其余 div²−1 子格。</summary>
    public bool IsObstacle(GridCoord c)
    {
        var o = GetOccupant(c);
        return o != null && o.IsGridObstacle;
    }

    /// <summary>⭐【小格子级·精确】该子格是否有阻挡占格物。</summary>
    public bool IsObstacleSub(GridCoord sub)
    {
        var o = GetOccupantSub(sub);
        return o != null && o.IsGridObstacle;
    }

    /// <summary>【地块级便捷口】该地块的占格物。
    /// 【HH.294 片4·搭车 R3（`D770` 请裁-4）】**已迁移到子格域**：块内**任一**子格非空即返回该占格物
    /// （改前＝只读**首子格**代表位，前提「同块 div² 子格同值」；一旦出现「子格级部分占格」，
    /// 代表位落在空子格 ⇒ 静默错答 null）。快路径：代表位命中即返回（常见情形 1 次读）；
    /// 否则扫同块其余 div²−1 子格 ⇒ **部分占格下不失真**。
    /// ⚠️ 跨格建筑的「哪一格是主格」语义不变（本口只回答「这块上有没有东西」）。</summary>
    public IGridOccupant GetOccupant(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _occupants == null) return null;
        int baseIdx = CellBaseIndex(c.x, c.y);
        var o = _occupants[baseIdx];
        if (o != null) return o;                       // 快路径：代表位命中
        int div = SubDiv;
        for (int sy = 0; sy < div; sy++)               // 慢路径：同块其余子格（部分占格兜底）
            for (int sx = 0; sx < div; sx++)
            {
                if (sx == 0 && sy == 0) continue;
                var cand = _occupants[baseIdx + sy * _sw + sx];
                if (cand != null) return cand;
            }
        return null;
    }

    /// <summary>⭐【小格子级·精确】该子格的占格物（片3-B 新增读口）。</summary>
    public IGridOccupant GetOccupantSub(GridCoord sub)
    {
        if (!InSubBounds(sub.x, sub.y) || _occupants == null) return null;
        return _occupants[ToSubIndex(sub.x, sub.y)];
    }

    /// <summary>⭐【小格子级·精确】登记/清空**单个子格**占格（片3-B 读口 `GetOccupantSub` 的写对偶；
    /// 【HH.294 片4·搭车 R3】地块级口已迁到子格域 ⇒ 子格级写口需对称存在，供「子格级部分占格」这类精细写入）。
    /// 不影响同块其他子格；`BuildingBlocked` 位按 `IsGridObstacle` 同置同清。</summary>
    public void MarkOccupiedSub(GridCoord sub, IGridOccupant occupant)
    {
        if (!InSubBounds(sub.x, sub.y) || _occupants == null) return;
        int i = ToSubIndex(sub.x, sub.y);
        _occupants[i] = occupant;
        if (occupant != null && occupant.IsGridObstacle) _walkFlags[i] |= WalkFlags.BuildingBlocked;
        else _walkFlags[i] &= ~WalkFlags.BuildingBlocked;
    }

    /// <summary>⭐【小格子级·精确】释放**单个子格**占格（与 <see cref="MarkOccupiedSub"/> 对偶）。</summary>
    public void FreeSub(GridCoord sub)
    {
        if (!InSubBounds(sub.x, sub.y) || _occupants == null) return;
        int i = ToSubIndex(sub.x, sub.y);
        _occupants[i] = null;
        _walkFlags[i] &= ~WalkFlags.BuildingBlocked;
    }

    /// <summary>【地块级写口】登记/清空该地块占格（**整块 div² 子格同写**；壳对象按首子格分配）。</summary>
    public void MarkOccupied(GridCoord c, IGridOccupant occupant)
    {
        if (!InBounds(c.x, c.y) || _occupants == null) return;
        int div = SubDiv;
        int baseIdx = CellBaseIndex(c.x, c.y);
        for (int sy = 0; sy < div; sy++)
            for (int sx = 0; sx < div; sx++)
            {
                int i = baseIdx + sy * _sw + sx;
                _occupants[i] = occupant;
                if (occupant != null && occupant.IsGridObstacle) _walkFlags[i] |= WalkFlags.BuildingBlocked;
                else _walkFlags[i] &= ~WalkFlags.BuildingBlocked;
            }
        if (occupant != null) MarkCellOccupied(c);
    }

    /// <summary>【地块级写口】释放该地块占格（**整块 div² 子格同写**）。</summary>
    public void Free(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _occupants == null) return;
        int div = SubDiv;
        int baseIdx = CellBaseIndex(c.x, c.y);
        for (int sy = 0; sy < div; sy++)
            for (int sx = 0; sx < div; sx++)
            {
                int i = baseIdx + sy * _sw + sx;
                _occupants[i] = null;
                _walkFlags[i] &= ~WalkFlags.BuildingBlocked;
            }
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

    /// <summary>
    /// ⭐【HH.294 片4·搭车 R2】**按 `BuildingRegistry` 重建占格**（装配期兜底口）。
    ///
    /// <b>断链根因（实读取证）</b>：`KingdomFoundry.FoundFirstGeneration` 在 `WorldManager.GenerateMap`
    /// **步骤5**创建 AI 王国预置建筑 ⇒ 其 `BuildingFactory.CreateBuildingInstance` 调
    /// <see cref="MarkOccupiedFootprint"/> 时，<see cref="PopulateFromMap"/> **尚未执行**
    /// （`_w/_h = 0` ⇒ <see cref="InBounds"/> 为 false ⇒ 一个占格都没写）；随后
    /// `PopulateFromMap → `Initialize`` 又 `new` 掉 `_occupants`/`_walkFlags` ⇒ 即便写进旧数组也被抹。
    /// 症状＝片3 观察项：抽样的 AI 预置建筑「footprint 内非空子格 = 0」且「地块级 `GetOccupant` = null」。
    ///
    /// <b>本口</b>：在装配顺序的**网格就绪之后**调用（`WorldManager.GenerateWorld` 紧接 `PopulateFromMap`），
    /// 把注册表里每座建筑的 footprint 重登记一遍（幂等；含桥面位）。返回重登记座数。
    /// </summary>
    public int RebuildOccupancyFromRegistry()
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || _occupants == null) return 0;
        var all = reg.All;
        int n = 0;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null) continue;
            int w = Mathf.Max(1, b.footprint.x), h = Mathf.Max(1, b.footprint.y);
            MarkOccupiedFootprint(b.coord, w, h, b);
            if (b.def != null && b.def.isBridge) SetBridge(b.coord, w, h, true);
            n++;
        }
        return n;
    }

    /// <summary>置/清桥面位（2_2 桥放置/拆除）。Bridge 置位后 IsWalkable 豁免 Water 阻挡（doc 1 §5.1）。
    /// 片3-B：按**地块块**整块写（div² 子格）。</summary>
    public void SetBridge(GridCoord origin, int w, int h, bool on)
    {
        int div = SubDiv;
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                var c = new GridCoord(origin.x + dx, origin.y + dy, origin.layer);
                if (!InBounds(c.x, c.y) || _walkFlags == null) continue;
                int baseIdx = CellBaseIndex(c.x, c.y);
                for (int sy = 0; sy < div; sy++)
                    for (int sx = 0; sx < div; sx++)
                    {
                        int i = baseIdx + sy * _sw + sx;
                        if (on) _walkFlags[i] |= WalkFlags.Bridge;
                        else _walkFlags[i] &= ~WalkFlags.Bridge;
                    }
            }
    }

    public bool IsFootprintClear(GridCoord origin, int w, int h)
    {
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                var c = new GridCoord(origin.x + dx, origin.y + dy, origin.layer);
                // 阻挡（地形/建筑/水域）或已被占用均不可摆放（doc 1 R6）
                if (!IsWalkable(c) || GetOccupant(c) != null) return false;
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
        if (crossedChunk && unit.GetFaction() == Faction.Monster)
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

    /// <summary>【地块级·**无分配**读口】该地块上的单位数（不 `new List`）。
    /// 【HH.294 片4·4-F】供 `MapGate` 区域枚举每格取「有没有单位」用 —— `03` §8.7 判据 4「区域查询稳态零分配」。</summary>
    public int GetUnitCountInCell(GridCoord cell)
    {
        int n = 0;
        foreach (var kv in _unitSubCells)
            if (SubToCell(kv.Value) == cell) n++;
        return n;
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

    // ===== IPathGrid 实现（微格坐标语义，HH.47 寻路1 修复）=====
    // 修（寻路1 / HH.47）：IPathGrid.IsWalkable 契约=微格可走（IPathGrid.cs：跨格地形逐微格判定），
    // 原隐式实现把微格坐标直接当宏格查表——微格域 _w×4=1024 远超宏格域 _w=256，
    // sub≥256 的查询全部 InBounds 失败 → WalkFlags.None → 不可走，A* 除 sub<256（左下 64×64 格）
    // 外全图不可达。症状：首帧起 NPC 游走 PathFailed→Idle、右键 MoveTo 位移 0.00（destination 送达但无路）。
    // 显式接口实现改走 IsSubWalkable（sub→cell 映射 + 障碍判定），与 PathFollower.FollowNext 的
    // 动态阻挡判定同一语义；宏格 IsWalkable 保留原语义供建造/校验等 cell 级消费方（IsFootprintClear 等）。
    bool IPathGrid.IsWalkable(GridCoord subCoord) => IsSubWalkable(subCoord);

    public bool IsDiagonalMoveAllowed(GridCoord from, GridCoord to)
    {
        int dx = Mathf.Abs(to.x - from.x), dy = Mathf.Abs(to.y - from.y);
        if (dx == 0 || dy == 0) return true;
        var a = new GridCoord(from.x + System.Math.Sign(to.x - from.x), from.y, from.layer);
        var b = new GridCoord(from.x, from.y + System.Math.Sign(to.y - from.y), from.layer);
        // 修（HH.47）：入参为微格坐标，防穿角须按微格判定（原 IsWalkable 宏格查表同上越界问题）
        return IsSubWalkable(a) && IsSubWalkable(b);
    }

    public float GetEnterCost(GridCoord subCoord)
    {
        // 地形代价表归 2_6；此处统一 1.0
        return 1f;
    }

    // ===== 内部辅助：GridCell 懒分配（承载单位列表，兼容旧消费方）=====
    // 片3-B：`_cells` 数组本身已降到小格子（计入"三数组"内存），但**壳对象按地块首子格分配一件** ⇒
    //   `GridCell` 实例数与改前一致（不因 ×16 而多分配小对象）；`GridCell.Coord` 仍为**地块坐标**。
    private void MarkCellOccupied(GridCoord c)
    {
        if (!InBounds(c.x, c.y) || _cells == null) return;
        int i = CellBaseIndex(c.x, c.y);
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