using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 2_10 渲染与摄像机 · 步骤1+2「渲染层结构 + 等轴铺格」。
///
/// 铁律：本篇只渲染、**不产生任何逻辑坐标副作用**；逻辑层一律正交格坐标（doc 1 §1.6），
/// 等轴投影只在本类作用于渲染层。
///
/// 等轴 2:1（菱形）约定（对齐 Unity Tilemap IsometricZAsY / Cell Size (1.28,0.64,1)）：
///   isoX = (gx - gy) * cellW * 0.5
///   isoY = (gx + gy) * cellH * 0.5
/// 即沿 +x 走一格 → ( +cellW/2, +cellH/2 )，沿 +y 走一格 → ( -cellW/2, +cellH/2 )。
/// cellSize 读 GridSystem.Config（PPU=100：128×64px → (1.28,0.64)），无网格时回退默认。
///
/// 本类交付：
///   - GridToIso / IsoToCell / IsoDepth：纯等轴投影（可选 GridSystem 对齐，空网格可算）。
///   - RenderMap / UpdateCell：遍历 MapData.features 铺到 Ground/Feature 两层 Tilemap（等轴占位菱形，
///     占位→正式=步骤10 纯资产替换；配色=调试分色，兼服务小剧场可读性）。
///   - 拾取（ScreenToGrid = Camera.ScreenToWorldPoint → IsoToCell）随步骤3 CameraRig 补全。
///
/// 占位 tile：运行时生成 128×64 等轴菱形 sprite（PPU100 → 世界 1.28×0.64），缓存复用。
/// Ground=地皮（特征→地皮基色，全覆盖无缝隙）；Feature=实体特征物（树/山/矿/一次性资源，与地皮分离）。
/// </summary>
public class MapRenderService : Singleton<MapRenderService>
{
    [Header("渲染层（场景 MapRender 下自动查找，亦可手动指定）")]
    [Tooltip("地皮层：全覆盖无缝隙")]
    [SerializeField] private Tilemap groundTilemap;
    [Tooltip("特征物层：树/山/矿/一次性资源")]
    [SerializeField] private Tilemap featureTilemap;

    /// <summary>1 小区块=128×64px @PPU100 → cellSize (1.28, 0.64)。</summary>
    public static readonly Vector2 DefaultCellSize = new Vector2(1.28f, 0.64f);

    // ===== 占位 tile 缓存（特征物 / 地皮）=====
    // G1（HH.264）：特征物缓存键改 string（artId / "ph:{FeatureType}"）——真图按 artId 分档（树＝4 气候×3 变体）
    private readonly Dictionary<string, Tile> _featureTiles = new Dictionary<string, Tile>();
    // HH.239 T10：地皮缓存键改 string（artId / "ph:{FeatureType}"）——地皮真图按温度带分档，同一 FeatureType 可多图
    private readonly Dictionary<string, Tile> _groundTiles = new Dictionary<string, Tile>();
    // G2（HH.264）：特征物缺真图一次性告警去重表（同 key 只报一次·L-05 禁刷屏）
    private readonly HashSet<string> _featureWarned = new HashSet<string>();
    private static readonly Color _fallback = new Color(0.6f, 0.6f, 0.6f);

    // ===== ③（HH.268）：多格占位特征「按占位只渲一次」=====
    // 合并 tile 缓存（键 = artId + "@{side}x{side}"）——带格位偏移，故与逐格 tile 分表
    private readonly Dictionary<string, TileBase> _mergedTiles = new Dictionary<string, TileBase>();
    // 块锚记忆表（键 = x<<20|y；1=锚 0=非锚）。每次铺格入口清空（防运行时 features 变更后失效）
    private readonly Dictionary<long, int> _blockAnchorMemo = new Dictionary<long, int>();

    /// <summary>③ 多格占位特征：带格位偏移的 tile（把 sprite 锚点从「格中心」平移到「占位块中心」）。
    /// 偏移以**格**为单位（side=2 ⇒ (0.5,0.5) 格 ⇒ 等轴下世界 +（0, cellH/2）= +0.32）。</summary>
    private sealed class OffsetTile : TileBase
    {
        public Sprite sprite;
        public Vector3 cellOffset;

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.sprite = sprite;
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.Translate(cellOffset);
            tileData.flags = TileFlags.None;                       // 不锁 transform（LockTransform 会吞掉偏移）
            tileData.colliderType = Tile.ColliderType.None;
        }
    }

    // ===== 视域动态加载（chunk 化，2_10 落地附加）=====
    [Header("视域动态加载（chunk 化）")]
    [Tooltip("chunk 边长（格数）。地图按此切块，摄像机滑入时才铺对应 chunk，初装只铺主城锚点周边强加载+视域")]
    [SerializeField] private int chunkSize = 24;
    [Tooltip("视域外预加载环形边数（chunk），滑动进入更远处才铺")]
    [SerializeField] private int lookaheadChunks = 1;
    private readonly HashSet<long> _loadedChunks = new HashSet<long>();
    private MapData _map;
    private bool _chunkRendering;
    private Vector2Int _lastCamChunk = new Vector2Int(int.MaxValue, int.MaxValue);

    /// <summary>取逻辑网格 cellSize；Editor 空网格（GridSystem 未激活）回退默认，保证纯投影可算。</summary>
    private static Vector2 CellSize()
    {
        if (GridSystem.Instance != null && GridSystem.Instance.Config != null)
            return GridSystem.Instance.Config.cellSize;
        return DefaultCellSize;
    }

    /// <summary>逻辑格 → 等轴渲染世界坐标（仅渲染层用）。
    /// 【HH.294 片3-A】⭐ **转调** `GridSystem` 唯一换算内核（本类**不再自带算式** —— 片3-A 前此处与
    /// `GridSystem.CoordToWorld` 是两套并行实现）；无 GridSystem 实例时用 `CellSize()` 兜底默认格尺寸。</summary>
    public static Vector2 GridToIso(GridCoord cell) => GridSystem.CellToWorld(cell, CellSize());

    /// <summary>
    /// 等轴世界坐标 → 逻辑格（逆投影，ScreenToGrid 底座；floor 取含点所在的菱形格）。
    /// 纯数学逆变换不校验越界，调用方（步骤3 CameraRig/ScreenToGrid）自行 clamp。
    /// 【HH.294 片3-A】⭐ **转调** `GridSystem` 内核（本类不再自带算式）。
    /// </summary>
    public static GridCoord IsoToCell(Vector2 iso) => GridSystem.WorldToCell(iso, CellSize());

    /// <summary>垂直向量（世界码→世界屏幕用），供单位/悬浮物按等轴深度参与 Y-sort 的辅助（预留）。
    /// 【HH.294 片3-A】⭐ 转调 `GridToIso`（＝其 isoY 分量），不再自带算式。</summary>
    public static float IsoDepth(GridCoord cell) => GridToIso(cell).y;

    // ========================================================================
    //  MonoBehaviour 生命周期
    // ========================================================================

    protected override void Awake()
    {
        base.Awake();
        _chunkRendering = true; // 默认启用 chunk 视域动态加载（chunkSize=0 时退全量）
        if (groundTilemap == null || featureTilemap == null)
        {
            var all = FindObjectsOfType<Tilemap>(true);
            foreach (var t in all)
            {
                if (groundTilemap == null && t.name == "Tilemap_Ground") groundTilemap = t;
                else if (featureTilemap == null && t.name == "Tilemap_Feature") featureTilemap = t;
            }
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    private void OnMapGenerated(MapGeneratedEvent evt)
    {
        RenderMap(WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null);
    }

    // ========================================================================
    //  铺格
    // ========================================================================

    /// <summary>
    /// 全图/视域铺格。默认走 chunk 视域动态加载（chunkSize>0 时）：
    ///   只在摄像机视域 + 主城锚点周边 chunk 铺 tile，镜头滑入新 chunk 才填。
    /// chunkSize=0 则全图一次铺（2_10 验收/调试模式），不依赖摄像机。
    /// </summary>
    public void RenderMap(MapData map)
    {
        if (map == null || map.features == null || map.width <= 0 || map.height <= 0)
        {
            Debug.LogWarning("[MapRenderService] RenderMap 无有效地图数据，清空渲染层");
            ClearAllTiles();
            _map = null;
            _loadedChunks.Clear();
            return;
        }
        if (groundTilemap == null) groundTilemap = FindTilemap("Tilemap_Ground");
        if (featureTilemap == null) featureTilemap = FindTilemap("Tilemap_Feature");
        if (groundTilemap == null && featureTilemap == null)
        {
            Debug.LogWarning("[MapRenderService] 未找到 Tilemap_Ground/Feature，跳过铺格");
            return;
        }

        _map = map;
        ClearAllTiles();
        _loadedChunks.Clear();
        _lastCamChunk = new Vector2Int(int.MaxValue, int.MaxValue);

        if (_chunkRendering && chunkSize > 0)
        {
            // chunk 模式：初装只强加载主城锚点周边 + 当前视域（Update 持续补）
            EnsureStrongHomeArea();
            UpdateViewport();
            Debug.Log($"[MapRenderService] chunk 视域加载初始化完成 {map.width}x{map.height}, chunk={chunkSize}x{chunkSize}");
        }
        else
        {
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                    SetCell(x, y, MapGate.ReadAt(map, x, y));   // 【HH.294 片4·4-A】读走查门（原裸读 map.features[...]）
            Debug.Log($"[MapRenderService] RenderMap 全量铺格完成: {map.width}x{map.height}");
        }
    }

    /// <summary>单格增量刷新（建造/地形/树刷新/2_12 废墟态切换时调用）。只重铺该格，不重建全图。</summary>
    public void UpdateCell(GridCoord cell)
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || map.features == null) return;
        if (cell.x < 0 || cell.y < 0 || cell.x >= map.width || cell.y >= map.height) return;
        _blockAnchorMemo.Clear();    // ③：features 可能已变（采集/刷新）⇒ 块锚分解重算
        SetCell(cell.x, cell.y, MapGate.ReadAt(map, cell.x, cell.y));   // 【HH.294 片4·4-A】读走查门
        // ③：多格占位特征的整块随之刷新（锚格变动会影响同块 4 格的渲染）
        RefreshMultiCellNeighborhood(cell.x, cell.y, map);
    }

    /// <summary>③ 多格占位特征：重铺 (x,y) 所在占位块的全部格（保证「只渲一次」在增量刷新下仍自洽）。</summary>
    private void RefreshMultiCellNeighborhood(int x, int y, MapData map)
    {
        foreach (var ft in MultiCellFeatures)
        {
            int side = FootprintSide(ft);
            for (int oy = -(side - 1); oy <= 0; oy++)
                for (int ox = -(side - 1); ox <= 0; ox++)
                {
                    int nx = x + ox, ny = y + oy;
                    if (nx < 0 || ny < 0 || nx >= map.width || ny >= map.height) continue;
                    if (nx == x && ny == y) continue;
                    SetCell(nx, ny, MapGate.ReadAt(map, nx, ny));   // 【HH.294 片4·4-A】读走查门
                }
        }
    }

    /// <summary>③ 需按占位合并渲染的特征型清单（当前仅 `Mine`；`NaturalBuilding.w/h` 面留待有素材时扩）。</summary>
    private static readonly FeatureType[] MultiCellFeatures = new FeatureType[] { FeatureType.Mine };

    /// <summary>重铺指定区域的全部格（外部批量刷新入口，如建筑 footprint 变化）。</summary>
    public void RefreshRegion(int x0, int y0, int w, int h)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++)
                UpdateCell(new GridCoord(x, y));
    }

    // ========================================================================
    //  视域动态加载（chunk）
    // ========================================================================

    private void Update()
    {
        if (!_chunkRendering || chunkSize <= 0 || _map == null) return;
        UpdateViewport();
    }

    /// <summary>单格铺格（Ground+Feature）。占位 tile 缓存复用。
    /// ③（HH.268）：多格占位特征（≥2×2）**整块取一张图、按占位中心只渲一次**（禁逐格重复贴）。</summary>
    private void SetCell(int x, int y, FeatureType ft)
    {
        var pos = new Vector3Int(x, y, 0);
        if (groundTilemap != null) groundTilemap.SetTile(pos, GroundTile(ft, x, y));
        if (featureTilemap == null) return;

        if (IsMultiCellFeature(ft))
        {
            int side = FootprintSide(ft);
            if (IsBlockAnchor(x, y, ft, side))
            {
                var merged = MergedFeatureTile(ft, x, y, side);
                if (merged != null) { featureTilemap.SetTile(pos, merged); return; }
                // 缺真图 ⇒ 落回逐格占位（保缺图回退链语义）
            }
            else if (CoveredByBlockAnchor(x, y, ft, side))
            {
                featureTilemap.SetTile(pos, null);      // 同块非锚格：不重复贴
                return;
            }
            // 非完整占位块（runtime features 变更等）⇒ 逐格回退，避免空洞
        }
        featureTilemap.SetTile(pos, FeatureTileOrNull(ft, x, y));
    }

    /// <summary>③ 该特征型是否为「多格占位特征」（需按占位合并渲染）。</summary>
    private static bool IsMultiCellFeature(FeatureType ft) { return ft == FeatureType.Mine; }

    /// <summary>③ 该型的占位边长（格）。来源＝单一源 <see cref="MapGenRules.MineClusterSide"/>。</summary>
    private static int FootprintSide(FeatureType ft)
    {
        return ft == FeatureType.Mine ? MapGenRules.MineClusterSide : 1;
    }

    /// <summary>③ 块锚判据（**读序贪婪分解的闭式**）：(x,y) 是锚 ⇔ side×side 全为该特征
    /// **且未被更早（读序：y升x升）的锚覆盖**；可能覆盖它的更早锚只可能是左上 (side-1)² 邻域内的块锚。
    /// 与逐格旧法的差异＝同块只在**锚格**渲一张（`Mine` 2×2 ⇒ 4 张 → 1 张）。</summary>
    private bool IsBlockAnchor(int x, int y, FeatureType ft, int side)
    {
        var map = _map;
        if (map == null || map.features == null) return false;
        if (x < 0 || y < 0 || x + side > map.width || y + side > map.height) return false;

        long key = ((long)x << 20) | (uint)y;
        int memo;
        if (_blockAnchorMemo.TryGetValue(key, out memo)) return memo != 0;

        bool anchor = true;
        for (int dy = 0; dy < side && anchor; dy++)
            for (int dx = 0; dx < side; dx++)
                if (MapGate.ReadAt(map, x + dx, y + dy) != ft) { anchor = false; break; }   // 【HH.294 片4·4-A】读走查门

        if (anchor)
        {
            for (int oy = -(side - 1); oy <= 0 && anchor; oy++)
                for (int ox = -(side - 1); ox <= 0; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    if (IsBlockAnchor(x + ox, y + oy, ft, side)) { anchor = false; break; }
                }
        }
        _blockAnchorMemo[key] = anchor ? 1 : 0;
        return anchor;
    }

    /// <summary>③ (x,y) 是否已被某个块锚覆盖（含自身；调用前已排除自身为锚的情形）。</summary>
    private bool CoveredByBlockAnchor(int x, int y, FeatureType ft, int side)
    {
        for (int oy = -(side - 1); oy <= 0; oy++)
            for (int ox = -(side - 1); ox <= 0; ox++)
                if (IsBlockAnchor(x + ox, y + oy, ft, side)) return true;
        return false;
    }

    /// <summary>③ 多格占位特征的合并 tile：真图 ＋ 格位偏移把 sprite 锚点落到**占位块中心**。
    /// ⚠️ 偏移量须**扣掉 tileAnchor**：sprite 锚点本就落在 `CellToWorld(格 + tileAnchor)`
    /// （实测 `GetCellCenterWorld(0,0,0)=(0,0.32)`），故目标偏移（格）＝ `(side-1)/2 − tileAnchor`。
    /// 当前 tileAnchor=(0.5,0.5) ⇒ offset=(0,0)；若 ① 把 tileAnchor 归一为 (0,0) ⇒ offset=(0.5,0.5)。
    /// 缺真图 ⇒ 返回 null（调用方落回逐格占位）。</summary>
    private TileBase MergedFeatureTile(FeatureType ft, int x, int y, int side)
    {
        string artId = FeatureArtId(ft, x, y);
        if (artId == null) { WarnMissingFeature(ft, artId); return null; }

        Vector3 anchor = featureTilemap != null ? featureTilemap.tileAnchor : new Vector3(0.5f, 0.5f, 0f);
        Vector3 offset = new Vector3((side - 1) * 0.5f - anchor.x, (side - 1) * 0.5f - anchor.y, 0f);
        string cacheKey = artId + "@" + side + "x" + side + "|a" + anchor.x + "," + anchor.y;
        TileBase cached;
        if (_mergedTiles.TryGetValue(cacheKey, out cached)) return cached;

        Sprite realArt = null;
        var table = ValleyRampart.Rendering.SpriteRefTable.Instance;
        if (table != null) table.TryGet(artId, out realArt);
        if (realArt == null) { WarnMissingFeature(ft, artId); return null; }

        var tile = ScriptableObject.CreateInstance<OffsetTile>();
        tile.sprite = realArt;
        tile.cellOffset = offset;
        _mergedTiles[cacheKey] = tile;
        return tile;
    }

    /// <summary>chunk 坐标 → 索引（long 防 256² 大数）。</summary>
    private static long ChunkKey(int cx, int cy)
    {
        return (long)cx * 100000 + cy;
    }

    /// <summary>chunkSize 公共只读（2_10 步骤13 TerritoryOverlay chunk→中区块范围换算用；实例字段经 Instance 读取，缺省 24）。</summary>
    public static int ChunkSize => Instance != null ? Instance.chunkSize : 24;

    /// <summary>chunk 铺设完成钩子（2_10 步骤13 D445②）：TerritoryOverlay 订阅→查 Ledger 补染（防「事件早于 chunk 加载」竞态）。</summary>
    public static event System.Action<int, int> OnChunkRendered;

    /// <summary>把指定 chunk 范围（逻辑格矩形）全部铺格并登记 loaded。</summary>
    private void RenderChunk(int cx, int cy)
    {
        long key = ChunkKey(cx, cy);
        if (_loadedChunks.Contains(key) || _map == null) return;
        int x0 = cx * chunkSize, y0 = cy * chunkSize;
        int x1 = Mathf.Min(x0 + chunkSize, _map.width);
        int y1 = Mathf.Min(y0 + chunkSize, _map.height);
        if (x0 >= _map.width || y0 >= _map.height) return;
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                SetCell(x, y, MapGate.ReadAt(_map, x, y));   // 【HH.294 片4·4-A】读走查门
        _loadedChunks.Add(key);
        OnChunkRendered?.Invoke(cx, cy);   // 铺设完成钩子（D445②）
    }

    /// <summary>当前摄像机所在 chunk 及周边 lookahead 环形 chunk。摄像机用 CameraRig 世界坐标→IsoToCell→chunk。</summary>
    private void UpdateViewport()
    {
        if (_map == null) return;
        var rig = CameraRig.Instance;
        if (rig == null || cameraCenterOut(out var center)) return;
        GridCoord camCell = MapRenderService.IsoToCell(center);
        int ccx = camCell.x / chunkSize;
        int ccy = camCell.y / chunkSize;
        if (ccx == _lastCamChunk.x && ccy == _lastCamChunk.y) return;
        _lastCamChunk = new Vector2Int(ccx, ccy);
        for (int oy = -lookaheadChunks; oy <= lookaheadChunks; oy++)
        {
            for (int ox = -lookaheadChunks; ox <= lookaheadChunks; ox++)
            {
                int cx = ccx + ox, cy = ccy + oy;
                if (cx < 0 || cy < 0) continue;
                RenderChunk(cx, cy);
            }
        }
    }

    private bool cameraCenterOut(out Vector2 center)
    {
        center = Vector2.zero;
        var rig = CameraRig.Instance;
        if (rig == null || rig.transform == null) return true;
        center = (Vector2)rig.transform.position;
        return false;
    }

    /// <summary>初装强加载主城锚点周边 chunk（保证出生可见主城及城郊，不被视域初始位置偏移错过）。</summary>
    private void EnsureStrongHomeArea()
    {
        if (_map == null) return;
        var map = _map;
        int hcx = (map.width / 2) / chunkSize;
        int hcy = (map.height / 2) / chunkSize;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
                RenderChunk(hcx + ox, hcy + oy);
    }

    public void ClearAllTiles()
    {
        _blockAnchorMemo.Clear();    // ③：全量重铺 ⇒ 块锚分解失效，须重算
        if (groundTilemap != null) groundTilemap.ClearAllTiles();
        if (featureTilemap != null) featureTilemap.ClearAllTiles();
    }

    private static Tilemap FindTilemap(string name)
    {
        if (Instance != null)
        {
            var child = Instance.transform.Find(name);
            if (child != null) return child.GetComponent<Tilemap>();
        }
        foreach (var t in FindObjectsOfType<Tilemap>(true))
            if (t.name == name) return t;
        return null;
    }

    // ========================================================================
    //  占位 tile
    // ========================================================================

    /// <summary>地皮 tile（全覆盖无缝隙；特征→地皮基色）。HH.239 T10：先查 <c>ground_*</c> 真图（按温度带/水系），
    /// 未命中回退生成菱形（缺图不崩不空白）。</summary>
    private Tile GroundTile(FeatureType ft, int x, int y)
    {
        string artId = GroundArtId(ft, x, y);
        string cacheKey = artId ?? ("ph:" + ft);
        if (_groundTiles.TryGetValue(cacheKey, out var t)) return t;

        Sprite realArt = null;
        if (artId != null)
        {
            var table = ValleyRampart.Rendering.SpriteRefTable.Instance;
            if (table != null) table.TryGet(artId, out realArt);
        }
        t = realArt != null ? CreateSpriteTile(realArt) : CreateIsoTile(FeatureToGroundColor(ft));
        _groundTiles[cacheKey] = t;
        return t;
    }

    /// <summary>地皮 artId：水/海按水系，其余按格所在温度带（D689 口径变更＝升级真图）。
    /// HH.272 件⑥：`Lake`（湖/冰河）已删 ⇒ 原「复用河道真图」分支移除。</summary>
    private string GroundArtId(FeatureType ft, int x, int y)
    {
        switch (ft)
        {
            case FeatureType.Ocean: return "ground_ocean";
            case FeatureType.River: return "ground_river";
        }
        var map = _map;
        if (map == null || map.climateZones == null || map.climateZones.Length == 0) return "ground_temperate";
        switch (MapGenRules.ZoneOf(map, x, y))
        {
            case ClimateZone.Tropical:    return "ground_tropical";
            case ClimateZone.Subtropical: return "ground_subtropical";
            case ClimateZone.Cold:        return "ground_cold";
            default:                      return "ground_temperate";
        }
    }

    private static Tile CreateSpriteTile(Sprite sprite)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        return tile;
    }

    /// <summary>特征物 tile（非地皮实体才返回，水/平原返回 null 铺空）。
    /// G1（HH.264/D711）：先查 <c>SpriteRefTable</c> 的 <c>feat_*</c> 真图（照 T9 建筑接线模式·真图优先）；
    /// G2：未命中 ⇒ 保持占位色块 ＋ **一次性告警**（L-05 禁刷屏）。</summary>
    private Tile FeatureTileOrNull(FeatureType ft, int x, int y)
    {
        if (ft != FeatureType.Tree && ft != FeatureType.Mountain && ft != FeatureType.SnowMountain
            && ft != FeatureType.Mine && ft != FeatureType.OreVein
            && ft != FeatureType.StonePile && ft != FeatureType.WoodPile) return null;

        string artId = FeatureArtId(ft, x, y);
        string cacheKey = artId ?? ("ph:" + ft);
        if (_featureTiles.TryGetValue(cacheKey, out var t)) return t;

        Sprite realArt = null;
        if (artId != null)
        {
            var table = ValleyRampart.Rendering.SpriteRefTable.Instance;
            if (table != null) table.TryGet(artId, out realArt);
        }
        if (realArt != null)
        {
            t = CreateSpriteTile(realArt);
        }
        else
        {
            WarnMissingFeature(ft, artId);
            t = CreateIsoTile(FeatureToFeatureColor(ft));
        }
        _featureTiles[cacheKey] = t;
        return t;
    }

    /// <summary>特征物 artId（G1·D37 唯一源）：树＝温度带×3 变体（复用 H2 格坐标确定性哈希·单一源在
    /// <see cref="BuildingVisual.TreeArtId"/>）；矿洞/矿脉/石堆/木堆＝单键；山/雪山**映射表 §十.2 未定义
    /// `feat_*` 素材** ⇒ 返回 null（走占位并列报·G3 禁静默）。</summary>
    private static string FeatureArtId(FeatureType ft, int x, int y)
    {
        switch (ft)
        {
            case FeatureType.Tree:      return BuildingVisual.TreeArtId(new GridCoord(x, y));
            case FeatureType.Mine:      return "feat_mine";
            case FeatureType.OreVein:   return "feat_orevein";
            case FeatureType.StonePile: return "feat_stone_pile";
            case FeatureType.WoodPile:  return "feat_deadwood";
            default:                    return null;   // Mountain / SnowMountain（无素材·列报）
        }
    }

    /// <summary>G2：特征物缺真图一次性告警（同 key 只报一次·L-05）。</summary>
    private void WarnMissingFeature(FeatureType ft, string artId)
    {
        string key = artId ?? ("ph:" + ft);
        if (_featureWarned.Add(key))
            Debug.LogWarning($"[MapRenderService] 特征物无真图：ft={ft} artId={artId ?? "(未定义)"} ⇒ 保持占位色块");
    }

    private static Tile CreateIsoTile(Color color)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = CreateIsoDiamondSprite(color);
        return tile;
    }

    /// <summary>生成 128×64 等轴菱形占位 sprite（PPU100 → 世界 1.28×0.64，与 cellSize 对齐）。pivot=底面中心。
    /// public：2_10 步骤13 TerritoryOverlay 复用同款白菱形做染色 tile（纯可见性放开，行为不变）。</summary>
    public static Sprite CreateIsoDiamondSprite(Color color)
    {
        const int w = 128, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            // 菱形：中心横向半宽随 y 线性收窄
            int halfW = (int)(w * 0.5f * (1f - Mathf.Abs(y - (h - 1) * 0.5f) / ((h - 1) * 0.5f)));
            for (int x = 0; x < w; x++)
                px[y * w + x] = Mathf.Abs(x - w * 0.5f) <= halfW ? color : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    // ===== 调试配色（占位时段，兼服务小剧场可读性；正式资产替换时不改逻辑）=====

    private static Color FeatureToGroundColor(FeatureType ft)
    {
        switch (ft)
        {
            case FeatureType.Plain: return new Color(0.5f, 0.6f, 0.4f);          // 地皮绿
            case FeatureType.Tree: return new Color(0.45f, 0.55f, 0.35f);        // 林下地皮（Feature 叠树）
            case FeatureType.Mountain: case FeatureType.SnowMountain: return new Color(0.5f, 0.5f, 0.5f);
            case FeatureType.River: return new Color(0.3f, 0.5f, 0.7f);          // 河
            case FeatureType.Ocean: return new Color(0.15f, 0.35f, 0.6f);        // 海
            default: return new Color(0.4f, 0.45f, 0.4f);                        // 矿/一次性资源落可走地皮
        }
    }

    private static Color FeatureToFeatureColor(FeatureType ft)
    {
        switch (ft)
        {
            case FeatureType.Tree: return new Color(0.15f, 0.5f, 0.2f);          // 树绿
            case FeatureType.Mountain: return new Color(0.45f, 0.4f, 0.35f);     // 山褐
            case FeatureType.SnowMountain: return new Color(0.85f, 0.88f, 0.92f);// 雪山白
            case FeatureType.Mine: return new Color(0.55f, 0.4f, 0.55f);         // 矿紫
            case FeatureType.OreVein: return new Color(0.6f, 0.5f, 0.4f);        // 矿脉
            case FeatureType.StonePile: return new Color(0.6f, 0.6f, 0.6f);      // 石堆
            case FeatureType.WoodPile: return new Color(0.5f, 0.4f, 0.25f);      // 木堆
            default: return _fallback;
        }
    }
}