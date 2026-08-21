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
    private readonly Dictionary<FeatureType, Tile> _featureTiles = new Dictionary<FeatureType, Tile>();
    private readonly Dictionary<FeatureType, Tile> _groundTiles = new Dictionary<FeatureType, Tile>();
    private static readonly Color _fallback = new Color(0.6f, 0.6f, 0.6f);

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

    /// <summary>逻辑格 → 等轴渲染世界坐标（仅渲染层用）。</summary>
    public static Vector2 GridToIso(GridCoord cell)
    {
        Vector2 cs = CellSize();
        float halfW = cs.x * 0.5f;
        float halfH = cs.y * 0.5f;
        return new Vector2((cell.x - cell.y) * halfW, (cell.x + cell.y) * halfH);
    }

    /// <summary>
    /// 等轴世界坐标 → 逻辑格（逆投影，ScreenToGrid 底座；floor 取含点所在的菱形格）。
    /// 纯数学逆变换不校验越界，调用方（步骤3 CameraRig/ScreenToGrid）自行 clamp。
    /// </summary>
    public static GridCoord IsoToCell(Vector2 iso)
    {
        Vector2 cs = CellSize();
        float halfW = cs.x * 0.5f;
        float halfH = cs.y * 0.5f;
        // 由 isoX=(x-y)*hw, isoY=(x+y)*hh 反解：
        float gx = iso.x / halfW * 0.5f + iso.y / halfH * 0.5f;
        float gy = iso.y / halfH * 0.5f - iso.x / halfW * 0.5f;
        return new GridCoord(Mathf.FloorToInt(gx), Mathf.FloorToInt(gy));
    }

    /// <summary>垂直向量（世界码→世界屏幕用），供单位/悬浮物按等轴深度参与 Y-sort 的辅助（预留）。</summary>
    public static float IsoDepth(GridCoord cell)
    {
        Vector2 cs = CellSize();
        return (cell.x + cell.y) * cs.y * 0.5f; // 同 GridToIso 的 isoY，随行增即深度增
    }

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
                    SetCell(x, y, map.features[y * map.width + x]);
            Debug.Log($"[MapRenderService] RenderMap 全量铺格完成: {map.width}x{map.height}");
        }
    }

    /// <summary>单格增量刷新（建造/地形/树刷新/2_12 废墟态切换时调用）。只重铺该格，不重建全图。</summary>
    public void UpdateCell(GridCoord cell)
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || map.features == null) return;
        if (cell.x < 0 || cell.y < 0 || cell.x >= map.width || cell.y >= map.height) return;
        SetCell(cell.x, cell.y, map.features[cell.y * map.width + cell.x]);
    }

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

    /// <summary>单格铺格（Ground+Feature）。占位 tile 缓存复用。</summary>
    private void SetCell(int x, int y, FeatureType ft)
    {
        var pos = new Vector3Int(x, y, 0);
        if (groundTilemap != null) groundTilemap.SetTile(pos, GroundTile(ft));
        if (featureTilemap != null) featureTilemap.SetTile(pos, FeatureTileOrNull(ft));
    }

    /// <summary>chunk 坐标 → 索引（long 防 256² 大数）。</summary>
    private static long ChunkKey(int cx, int cy)
    {
        return (long)cx * 100000 + cy;
    }

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
                SetCell(x, y, _map.features[y * _map.width + x]);
        _loadedChunks.Add(key);
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

    /// <summary>地皮 tile（全覆盖无缝隙；特征→地皮基色）。</summary>
    private Tile GroundTile(FeatureType ft)
    {
        if (_groundTiles.TryGetValue(ft, out var t)) return t;
        t = CreateIsoTile(FeatureToGroundColor(ft));
        _groundTiles[ft] = t;
        return t;
    }

    /// <summary>特征物 tile（非地皮实体才返回，水/平原返回 null 铺空）。</summary>
    private Tile FeatureTileOrNull(FeatureType ft)
    {
        if (ft != FeatureType.Tree && ft != FeatureType.Mountain && ft != FeatureType.SnowMountain
            && ft != FeatureType.Mine && ft != FeatureType.OreVein
            && ft != FeatureType.StonePile && ft != FeatureType.WoodPile) return null;
        if (_featureTiles.TryGetValue(ft, out var t)) return t;
        t = CreateIsoTile(FeatureToFeatureColor(ft));
        _featureTiles[ft] = t;
        return t;
    }

    private static Tile CreateIsoTile(Color color)
    {
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = CreateIsoDiamondSprite(color);
        return tile;
    }

    /// <summary>生成 128×64 等轴菱形占位 sprite（PPU100 → 世界 1.28×0.64，与 cellSize 对齐）。pivot=底面中心。</summary>
    private static Sprite CreateIsoDiamondSprite(Color color)
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
            case FeatureType.Lake: return new Color(0.25f, 0.45f, 0.65f);        // 湖
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