using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 2_10 步骤13 领土染色覆盖层（D443+D448~D452，设计 §5.10 视口分级修订版）。
/// 铁律：只渲染不数据——领土真源=TerritorySystem.Ledger（2_17 步骤12，D342），
/// 旗色=KingdomState.bannerColor（2_16 步骤5，D303 玩家 id=0 同染）；本类零写入上述数据源。
///
/// 渲染结构（D443）：MapRender 第六层 Tilemap_Territory（Iso ZAsY 同参数）；单一白菱形 tile 常驻，
/// 颜色经 per-cell SetColor（tint=派生色×alpha）；sortingOrder=5（Ground 0 之上、Feature/实体之下）；
/// 无主地透明不铺。染色粒度=中区块（Ledger key=mid）：每 mid 展开 midChunkSize² 格同色同 alpha；
/// 边界检测在 mid 级 8 邻域异主/无主（D450 边界恒浓：边界 mid 整块取档位表 boundaryAlpha）。
///
/// 视口分级（D448/D449/D451）：染色强度随 CameraRig 档位（zoomLods 对齐 zoomLevels 下标，超表取末档）；
/// 近景档整层 SetActive(false) 零 overdraw；跨档 fadeDurationSeconds 平滑插值；
/// SetTile 不随缩放发生（tile 常驻有主格，缩放只动 SetColor）。
///
/// 刷新四触发源（D445 三路+D448 档位）：
///   ① TerritoryChangedEvent 增量：Added 逐 mid 着色+±1 圈邻域边界重算
///   ② chunk 铺设钩子：MapRenderService.OnChunkRendered → chunk 范围查 Ledger 补染（防事件早于 chunk 竞态）
///   ③ 全量重染：GameLoadedEvent 读档成功后（LoadState 不广播事件，唯一可靠全染挂点；
///     时序实测：读档链 MapGeneratedEvent（Global 段建图）早于 GameLoadedEvent 收尾）；
///     新游戏=RebuildInitial/ClaimInitial 事件天然覆盖（EnterPlayingGate 无事件，语义由①③并集达成）
///   ④ 档位跨档：Update 轮询 CameraRig.ZoomIndex → 全部已染色 mid 按新档表重定 alpha（Ledger 内存直查）
///
/// 灭国渐隐（D446/D379）：FadeOutKingdom(kid) kingdomFadeDurationSeconds 内 alpha→0 后清 tile——
/// 纯渲染不占数据状态（领土数据当帧已全无主）；调用点=2_19 八步管线实施批（事件 Removed 语义扩展时接线），
/// 本批不预改 2_17 已落代码。
///
/// 2_13 接口（UI 消费归 2_13 实施批）：SetVisible(bool)/HighlightKingdom(int)（D452 临时中景浓度+聚焦）。
/// </summary>
public class TerritoryOverlay : Singleton<TerritoryOverlay>
{
    [Header("渲染层（场景 MapRender 下第六层 Tilemap_Territory）")]
    [SerializeField] private Tilemap territoryTilemap;

    [Header("配置（Resources/Config/TerritoryOverlayConfig 懒加载）")]
    [SerializeField] private TerritoryOverlayConfig config;

    // ===== 渲染层缓存（Ledger 镜像；真源始终在 TerritorySystem，本类只读）=====
    private readonly Dictionary<Vector2Int, int> _painted = new Dictionary<Vector2Int, int>();      // mid → kingdomId（已铺 tile）
    private readonly Dictionary<Vector2Int, bool> _boundary = new Dictionary<Vector2Int, bool>();   // mid → 是否边界 mid
    private readonly Dictionary<Vector2Int, float> _alphaTarget = new Dictionary<Vector2Int, float>(); // mid → 目标 alpha（当前档位/边界/高亮/渐隐）
    private readonly Dictionary<Vector2Int, float> _alpha = new Dictionary<Vector2Int, float>();    // mid → 当前显示 alpha
    private readonly Dictionary<int, Color> _colorCache = new Dictionary<int, Color>();             // kingdomId → 派生色 RGB（D447）
    private readonly Dictionary<int, float> _fadeMul = new Dictionary<int, float>();                // kingdomId → 灭国渐隐乘数（1→0）
    private readonly Dictionary<int, float> _fadeElapsed = new Dictionary<int, float>();            // kingdomId → 渐隐已历时秒
    private readonly Dictionary<Vector2Int, float> _alphaFrom = new Dictionary<Vector2Int, float>();// 跨档过渡起点快照

    // 档位/过渡/高亮/开关状态
    private int _lod = -1;                 // 当前档位（-1=未落位，Update 首帧轮询落位）
    private bool _transitioning;           // 跨档过渡进行中
    private float _transitionElapsed;
    private bool _transitionToZero;        // 本次过渡目标=归零（回近景/SetVisible(false)）
    private bool _pendingHide;             // 过渡归零完成后待隐藏层
    private int _highlightKid = -1;        // D452 高亮王国（-1=无）
    private bool _visible = true;          // 染色总开关（2_13 SetVisible）
    private Tile _tile;                    // 单一白菱形 tile（运行时生成，常驻复用）

    // ===== 生命周期 =====

    protected override void Awake()
    {
        base.Awake();
        if (config == null) config = TerritoryOverlayConfig.Load();
        _visible = config != null && config.enableOnStart;
    }

    private void OnEnable()
    {
        EventBus.Subscribe<TerritoryChangedEvent>(OnTerritoryChanged);
        EventBus.Subscribe<MapGeneratedEvent>(OnMapGenerated);
        EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
        MapRenderService.OnChunkRendered += OnChunkRendered;
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<TerritoryChangedEvent>(OnTerritoryChanged);
        EventBus.Unsubscribe<MapGeneratedEvent>(OnMapGenerated);
        EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        MapRenderService.OnChunkRendered -= OnChunkRendered;
    }

    private void Start()
    {
        EnsureTilemap();
        if (!_visible && territoryTilemap != null) territoryTilemap.gameObject.SetActive(false);
    }

    // ===== 事件触发源 =====

    /// <summary>新图生成：清染色层+重置缓存防旧局残留（读档链本事件早于 GameLoadedEvent，清层不丢重染）。</summary>
    private void OnMapGenerated(MapGeneratedEvent evt)
    {
        ResetForNewMap();
    }

    /// <summary>读档成功（D445③ 全量重染唯一可靠挂点：LoadState 不广播事件）。</summary>
    private void OnGameLoaded(GameLoadedEvent evt)
    {
        if (!evt.IsSuccess) return;
        ReapplyAll();
    }

    /// <summary>① 事件增量（D445）：Added 逐 mid 着色+±1 圈邻域边界重算（D450）。</summary>
    private void OnTerritoryChanged(TerritoryChangedEvent evt)
    {
        if (evt.Added == null) return;
        for (int i = 0; i < evt.Added.Count; i++)
        {
            PaintMid(evt.Added[i], evt.KingdomId);
            RefreshBoundaryAround(evt.Added[i]);
        }
    }

    /// <summary>② chunk 铺设钩子补染（D445②）：该 chunk 范围内 mid 查 Ledger 真源补色。</summary>
    private void OnChunkRendered(int cx, int cy)
    {
        var ledger = TerritorySystem.Instance != null ? TerritorySystem.Instance.Ledger : null;
        if (ledger == null || ledger.Count == 0) return;
        int cs = MapRenderService.ChunkSize;
        if (cs <= 0) return;
        int ms = MidChunkSize;
        int midX0 = cx * cs / ms, midY0 = cy * cs / ms;
        int midX1 = (cx * cs + cs - 1) / ms, midY1 = (cy * cs + cs - 1) / ms;
        for (int my = midY0; my <= midY1; my++)
            for (int mx = midX0; mx <= midX1; mx++)
            {
                var mid = new Vector2Int(mx, my);
                if (_painted.ContainsKey(mid)) continue;                       // 已染不重复
                if (!ledger.TryGetValue(mid, out int owner)) continue;         // 无主不铺
                PaintMid(mid, owner);
                RefreshBoundaryAround(mid);
            }
    }

    // ===== Update：④ 档位轮询 + 过渡推进 + 灭国渐隐推进 =====

    private void Update()
    {
        if (config == null) config = TerritoryOverlayConfig.Load();
        float dt = Time.deltaTime;

        // ④ 档位轮询（D448）：CameraRig.ZoomIndex 跨档 → OnLodChanged
        int rigLod = 0;
        var rig = CameraRig.Instance;
        if (rig != null) rigLod = rig.ZoomIndex;
        if (_lod == -1)
        {
            _lod = rigLod;                                                     // 首次落位：无过渡直落
            ApplyLodDirect();
        }
        else if (rigLod != _lod && _visible)
        {
            OnLodChanged(rigLod);
        }

        // 跨档过渡推进（D451）
        if (_transitioning)
        {
            float dur = config.fadeDurationSeconds;
            _transitionElapsed += dt;
            float t = dur > 0.0001f ? Mathf.Clamp01(_transitionElapsed / dur) : 1f;
            RepaintAll(t);
            if (t >= 1f)
            {
                _transitioning = false;
                if (_pendingHide && territoryTilemap != null)
                {
                    territoryTilemap.gameObject.SetActive(false);              // 渐隐完毕再隐藏（D451）
                    _pendingHide = false;
                }
            }
        }

        // 灭国渐隐推进（D446/D379）：alpha 乘数 →0，完毕清 tile
        if (_fadeElapsed.Count > 0)
            TickFading(dt);
    }

    // ===== 档位切换编排 =====

    /// <summary>跨档（D448/D451）：近景渐出后隐藏；出近景先激活再渐显（from=0）；其余普通过渡。</summary>
    private void OnLodChanged(int newLod)
    {
        _lod = newLod;
        EnsureTilemap();
        if (territoryTilemap == null) return;

        if (newLod == 0)
        {
            BeginTransition(toZero: true);                                     // 回近景：渐隐至 0
            _pendingHide = true;
        }
        else
        {
            if (!territoryTilemap.gameObject.activeSelf)
            {
                territoryTilemap.gameObject.SetActive(true);                   // 出近景先激活再渐显（D451）
                BeginTransition(toZero: false, fromZero: true);                // from 全 0 渐显
            }
            else
            {
                BeginTransition(toZero: false);
            }
        }
    }

    /// <summary>首次落位：按当前档位直接着色（无过渡）；近景则层隐藏（D449/D451 直落）。</summary>
    private void ApplyLodDirect()
    {
        EnsureTilemap();
        if (territoryTilemap == null) return;
        if (!_visible) { territoryTilemap.gameObject.SetActive(false); return; }
        if (_lod > 0)
        {
            if (!territoryTilemap.gameObject.activeSelf)
                territoryTilemap.gameObject.SetActive(true);
        }
        else if (territoryTilemap.gameObject.activeSelf)
        {
            territoryTilemap.gameObject.SetActive(false);                      // 近景直落=层隐藏
        }
        RepaintAll(1f);                                                        // t=1 直落目标值
    }

    /// <summary>启动跨档过渡：快照当前 alpha 为起点（fromZero=true 时从 0 渐显，出近景/SetVisible(true) 用）。</summary>
    private void BeginTransition(bool toZero, bool fromZero = false)
    {
        _alphaFrom.Clear();
        if (fromZero)
        {
            foreach (var kv in _painted) _alphaFrom[kv.Key] = 0f;
        }
        else
        {
            foreach (var kv in _painted)
                _alphaFrom[kv.Key] = _alpha.TryGetValue(kv.Key, out var a) ? a : 0f;
        }
        _transitioning = true;
        _transitionElapsed = 0f;
        _transitionToZero = toZero;
        if (toZero && territoryTilemap != null && !territoryTilemap.gameObject.activeSelf)
            territoryTilemap.gameObject.SetActive(true);                       // 隐藏层渐出前先激活
    }

    /// <summary>过渡目标 alpha（D448 档位表+D450 边界+D452 高亮+D446 渐隐乘数）。</summary>
    private float ComputeTargetAlpha(int kid, bool boundary)
    {
        var lod = kid == _highlightKid ? config.MidLod : config.GetLod(_lod);  // 高亮无视档位（D452）
        float baseA = boundary ? lod.boundaryAlpha : lod.interiorAlpha;
        float mul = _fadeMul.TryGetValue(kid, out var m) ? m : 1f;
        return baseA * mul;
    }

    /// <summary>全量按当前状态着色（transitionT&lt;0 直落；≥0 按过渡插值）。</summary>
    private void RepaintAll(float transitionT)
    {
        if (_painted.Count == 0) return;
        float dur = config != null ? config.fadeDurationSeconds : 0.3f;
        foreach (var kv in _painted)
            SetMidColor(kv.Key, kv.Value, transitionT, dur);
    }

    /// <summary>单 mid 着色（16 格同色同 alpha；tile 常驻只动 SetColor）。</summary>
    private void SetMidColor(Vector2Int mid, int kid, float transitionT, float dur)
    {
        EnsureTilemap();
        if (territoryTilemap == null) return;
        bool boundary = _boundary.TryGetValue(mid, out var b) && b;
        float target = ComputeTargetAlpha(kid, boundary);
        float alpha;
        if (transitionT >= 0f)
        {
            float from = _alphaFrom.TryGetValue(mid, out var f) ? f : 0f;
            alpha = Mathf.Lerp(from, target, dur > 0.0001f ? transitionT : 1f);
        }
        else
        {
            alpha = target;
        }
        _alphaTarget[mid] = target;
        _alpha[mid] = alpha;
        if (alpha <= 0.0001f) return;                                          // 0 透明不写色（tile 留待隐藏/清）
        Color c = DerivedColor(kid);
        c.a = alpha;
        SetColorMid(mid, c);
    }

    // ===== 铺 tile/清 tile =====

    /// <summary>铺一个 mid（16 格 SetTile+按当前档位着色）。幂等：重复调用只重写。</summary>
    private void PaintMid(Vector2Int mid, int kid)
    {
        EnsureTilemap();
        EnsureTile();
        if (territoryTilemap == null || _tile == null) return;
        _painted[mid] = kid;
        int ms = MidChunkSize;
        var pos = new Vector3Int();
        for (int dy = 0; dy < ms; dy++)
            for (int dx = 0; dx < ms; dx++)
            {
                pos.x = mid.x * ms + dx;
                pos.y = mid.y * ms + dy;
                pos.z = 0;
                if (territoryTilemap.GetTile(pos) == null)
                    territoryTilemap.SetTile(pos, _tile);
            }
        RecalcBoundary(mid);
        SetMidColor(mid, kid, -1f, 0f);
    }

    /// <summary>mid ±1 圈（含自身）边界重算（D450）：已染色者边界标志变化 → 重写 alpha。</summary>
    private void RefreshBoundaryAround(Vector2Int mid)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var n = new Vector2Int(mid.x + dx, mid.y + dy);
                if (!_painted.TryGetValue(n, out var owner)) continue;
                bool wasBoundary = _boundary.TryGetValue(n, out var b) && b;
                bool now = RecalcBoundary(n);
                if (wasBoundary != now)
                    SetMidColor(n, owner, -1f, 0f);                            // 边界重算瞬时刷新（D450）
            }
    }

    /// <summary>边界判定（mid 级 8 邻域异主/无主，D450）：任一邻 mid owner 不同（无主=不存在）即边界。写缓存并返回。</summary>
    private bool RecalcBoundary(Vector2Int mid)
    {
        bool boundary = false;
        var ledger = TerritorySystem.Instance != null ? TerritorySystem.Instance.Ledger : null;
        if (!_painted.TryGetValue(mid, out var owner))
        {
            _boundary[mid] = false;
            return false;
        }
        for (int dy = -1; dy <= 1 && !boundary; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var n = new Vector2Int(mid.x + dx, mid.y + dy);
                int nOwner = ledger != null && ledger.TryGetValue(n, out var o) ? o : int.MinValue;   // 无主=哨兵
                if (nOwner != owner) { boundary = true; break; }
            }
        _boundary[mid] = boundary;
        return boundary;
    }

    /// <summary>16 格 SetColor（同色）。</summary>
    private void SetColorMid(Vector2Int mid, Color c)
    {
        int ms = MidChunkSize;
        var pos = new Vector3Int();
        for (int dy = 0; dy < ms; dy++)
            for (int dx = 0; dx < ms; dx++)
            {
                pos.x = mid.x * ms + dx;
                pos.y = mid.y * ms + dy;
                pos.z = 0;
                territoryTilemap.SetColor(pos, c);
            }
    }

    /// <summary>清除一个 mid 的 16 格 tile。</summary>
    private void ClearMid(Vector2Int mid)
    {
        if (territoryTilemap == null) return;
        int ms = MidChunkSize;
        var pos = new Vector3Int();
        for (int dy = 0; dy < ms; dy++)
            for (int dx = 0; dx < ms; dx++)
            {
                pos.x = mid.x * ms + dx;
                pos.y = mid.y * ms + dy;
                pos.z = 0;
                territoryTilemap.SetTile(pos, null);
            }
    }

    // ===== 灭国渐隐（D446/D379 渲染侧就绪）=====

    /// <summary>
    /// 灭国清色：该 kingdomId 全部色块 kingdomFadeDurationSeconds 内 alpha→0 渐隐后清 tile。
    /// 纯渲染（领土数据当帧已全无主）；调用点=2_19 八步管线实施批（Removed 事件扩展时接线）。
    /// </summary>
    public void FadeOutKingdom(int kingdomId)
    {
        bool has = false;
        foreach (var kv in _painted)
            if (kv.Value == kingdomId) { has = true; break; }
        if (!has)
        {
            Debug.LogWarning($"[TerritoryOverlay] FadeOutKingdom({kingdomId}): 无该 kingdomId 染色格（可能已清/未染色），跳过。");
            return;
        }
        _fadeMul[kingdomId] = 1f;
        _fadeElapsed[kingdomId] = 0f;
    }

    private void TickFading(float dt)
    {
        float dur = config != null && config.kingdomFadeDurationSeconds > 0.0001f
            ? config.kingdomFadeDurationSeconds : 2.0f;
        List<int> finished = null;
        foreach (var key in new List<int>(_fadeElapsed.Keys))
        {
            float e = _fadeElapsed[key] + dt;
            _fadeElapsed[key] = e;
            float mul = 1f - Mathf.Clamp01(e / dur);
            _fadeMul[key] = mul;
            foreach (var kv in _painted)
                if (kv.Value == key)
                    SetMidColor(kv.Key, kv.Value, -1f, 0f);                    // 渐隐帧直落（乘数因子）
            if (e >= dur)
            {
                if (finished == null) finished = new List<int>();
                finished.Add(key);
            }
        }
        if (finished == null) return;
        for (int i = 0; i < finished.Count; i++)
        {
            RemoveKidPaint(finished[i]);
            _fadeMul.Remove(finished[i]);
            _fadeElapsed.Remove(finished[i]);
            Debug.Log($"[TerritoryOverlay] 灭国渐隐完成 kingdomId={finished[i]}，色块已清（D379/D446）。");
        }
    }

    /// <summary>清除某 kingdomId 全部染色（tile+缓存）。</summary>
    private void RemoveKidPaint(int kid)
    {
        List<Vector2Int> doomed = null;
        foreach (var kv in _painted)
            if (kv.Value == kid) { (doomed ??= new List<Vector2Int>()).Add(kv.Key); }
        if (doomed == null) return;
        for (int i = 0; i < doomed.Count; i++)
        {
            ClearMid(doomed[i]);
            _painted.Remove(doomed[i]);
            _alpha.Remove(doomed[i]);
            _alphaTarget.Remove(doomed[i]);
            _boundary.Remove(doomed[i]);
        }
    }

    // ===== 全量重染 / 清场重置 =====

    /// <summary>③ 全量重染（D445③）：清染色层后按 Ledger 真源全量铺色。读档完成后调用（GameLoadedEvent）。</summary>
    public void ReapplyAll()
    {
        var ledger = TerritorySystem.Instance != null ? TerritorySystem.Instance.Ledger : null;
        if (ledger == null) return;
        ClearPainted();
        foreach (var kv in ledger)
            PaintMid(kv.Key, kv.Value);
        // 铺完后统一重算一轮边界（跨 mid 邻属关系在逐个 Paint 时由 ±0 圈初算，异主邻接修正）
        foreach (var kv in _painted)
        {
            bool was = _boundary.TryGetValue(kv.Key, out var b) && b;
            bool now = RecalcBoundary(kv.Key);
            if (was != now) SetMidColor(kv.Key, kv.Value, -1f, 0f);
        }
        Debug.Log($"[TerritoryOverlay] 全量重染完成：{_painted.Count} 个中区块（Ledger={ledger.Count}）。");
    }

    /// <summary>清染色层（WorldLifecycle 清场编排调用）：tile+缓存全清，档位重置待下帧重落位。</summary>
    public void ClearOverlay()
    {
        ClearPainted();
        _fadeMul.Clear();
        _fadeElapsed.Clear();
        _highlightKid = -1;
        _transitioning = false;
        _pendingHide = false;
        _lod = -1;                                                             // 下帧 Update 按当前档位重落位
        if (territoryTilemap != null) territoryTilemap.gameObject.SetActive(true);
    }

    /// <summary>新图生成重置（OnMapGenerated）：语义同 ClearOverlay，另重置可见开关。</summary>
    private void ResetForNewMap()
    {
        ClearOverlay();
        _visible = config != null && config.enableOnStart;
        if (!_visible && territoryTilemap != null) territoryTilemap.gameObject.SetActive(false);
    }

    private void ClearPainted()
    {
        if (territoryTilemap != null) territoryTilemap.ClearAllTiles();
        _painted.Clear();
        _alpha.Clear();
        _alphaTarget.Clear();
        _alphaFrom.Clear();
        _boundary.Clear();
    }

    // ===== 2_13 接口（UI 消费归 2_13 实施批）=====

    /// <summary>染色总开关（2_13 设置页可消费）：false 渐隐至 0 后隐藏层；true 渐显回当前档位。</summary>
    public void SetVisible(bool value)
    {
        if (_visible == value) return;
        _visible = value;
        EnsureTilemap();
        if (territoryTilemap == null) return;
        if (value)
        {
            if (!territoryTilemap.gameObject.activeSelf)
            {
                territoryTilemap.gameObject.SetActive(true);
                BeginTransition(toZero: false, fromZero: true);                // from 全 0 渐显
            }
            else BeginTransition(toZero: false);
        }
        else
        {
            BeginTransition(toZero: true);
            _pendingHide = true;
        }
    }

    /// <summary>
    /// 列国名单高亮（D452）：该 kingdomId 全部色块**临时以中景浓度显色**（无视当前档位，近景下亦有反馈→临时激活层）
    /// +CameraRig.FocusOn 跳转领地质心。kid&lt;0 取消高亮回当前档位状态。UI 消费归 2_13 实施批。
    /// </summary>
    public void HighlightKingdom(int kingdomId)
    {
        if (kingdomId < 0)
        {
            if (_highlightKid < 0) return;
            int kid = _highlightKid;
            _highlightKid = -1;
            EnsureTilemap();
            if (territoryTilemap == null) return;
            if (!_visible) return;                                             // 总开关关着：保持隐藏
            foreach (var kv in _painted)
                if (kv.Value == kid) SetMidColor(kv.Key, kv.Value, -1f, 0f);   // 回当前档位
            if (_lod == 0) { BeginTransition(toZero: true); _pendingHide = true; }   // 近景回隐
            return;
        }

        bool has = false;
        foreach (var kv in _painted)
            if (kv.Value == kingdomId) { has = true; break; }
        if (!has)
        {
            Debug.LogWarning($"[TerritoryOverlay] HighlightKingdom({kingdomId}): 无该 kingdomId 染色格，仅聚焦跳过。");
            FocusOnKingdom(kingdomId);
            return;
        }

        _highlightKid = kingdomId;
        EnsureTilemap();
        if (territoryTilemap != null && !territoryTilemap.gameObject.activeSelf && _visible)
            territoryTilemap.gameObject.SetActive(true);                       // 近景下亦有反馈（D452）
        foreach (var kv in _painted)
            if (kv.Value == kingdomId) SetMidColor(kv.Key, kv.Value, -1f, 0f); // 临时中景浓度
        FocusOnKingdom(kingdomId);
    }

    /// <summary>领地质心聚焦（Iso 基准，CameraRig 同源）：该国 mid 平均格坐标 → GridToIso。</summary>
    private void FocusOnKingdom(int kingdomId)
    {
        var ledger = TerritorySystem.Instance != null ? TerritorySystem.Instance.Ledger : null;
        if (ledger == null || ledger.Count == 0) return;
        long sx = 0, sy = 0; int n = 0;
        foreach (var kv in ledger)
            if (kv.Value == kingdomId) { sx += kv.Key.x; sy += kv.Key.y; n++; }
        if (n == 0) return;
        int ms = MidChunkSize;
        var centerCell = new GridCoord((int)(sx / n) * ms + ms / 2, (int)(sy / n) * ms + ms / 2);
        var rig = CameraRig.Instance;
        if (rig != null) rig.FocusOn(MapRenderService.GridToIso(centerCell));
    }

    // ===== 底座 =====

    private Tilemap EnsureTilemap()
    {
        if (territoryTilemap != null) return territoryTilemap;
        var child = transform.Find("Tilemap_Territory");
        if (child != null) territoryTilemap = child.GetComponent<Tilemap>();
        if (territoryTilemap == null)
            foreach (var t in FindObjectsOfType<Tilemap>(true))
                if (t.name == "Tilemap_Territory") { territoryTilemap = t; break; }
        return territoryTilemap;
    }

    private void EnsureTile()
    {
        if (_tile != null) return;
        var sprite = MapRenderService.CreateIsoDiamondSprite(Color.white);     // 单一白菱形（D443），色由 SetColor tint
        _tile = ScriptableObject.CreateInstance<Tile>();
        _tile.sprite = sprite;
        // 运行时 Tile 默认 flags 含 LockColor——锁 per-cell SetColor（实测：SetColor 全部失效读回白）。
        // 染色 tint（D443）必须解锁 per-cell 着色。
        _tile.flags = UnityEngine.Tilemaps.TileFlags.None;
    }

    /// <summary>染色基色派生（D447）：bannerColor → HSV 饱和度×colorSaturation / 亮度×colorBrightness，旗色数据零污染。按 kingdomId 缓存。</summary>
    private Color DerivedColor(int kid)
    {
        if (_colorCache.TryGetValue(kid, out var c)) return c;
        Color baseC = new Color(0.5f, 0.5f, 0.5f);
        var reg = KingdomRegistry.Instance;
        var ks = reg != null ? reg.Get(kid) : null;
        if (ks != null) baseC = ks.bannerColor;
        Color.RGBToHSV(baseC, out var h, out var s, out var v);
        s = Mathf.Clamp01(s * (config != null ? config.colorSaturation : 1f));
        v = Mathf.Clamp01(v * (config != null ? config.colorBrightness : 1.1f));
        c = Color.HSVToRGB(h, s, v);
        _colorCache[kid] = c;
        return c;
    }

    /// <summary>midChunkSize（GridConfig，缺省 4）。</summary>
    private static int MidChunkSize =>
        GridSystem.Instance != null && GridSystem.Instance.Config != null && GridSystem.Instance.Config.midChunkSize > 0
            ? GridSystem.Instance.Config.midChunkSize : 4;

    // ===== 探针/调试只读口（冒烟容器用，不改内部状态）=====

    /// <summary>mid 当前显示 alpha（未染=-1）。冒烟探针用。</summary>
    public float GetMidAlpha(Vector2Int mid) => _alpha.TryGetValue(mid, out var a) ? a : -1f;

    /// <summary>mid 首格渲染色（tint 实值，含 alpha；无 tile=clear（区分铺格缺失 vs 色未写）；层隐藏时也可读）。冒烟探针用（色异断言）。</summary>
    public Color GetMidColorForProbe(Vector2Int mid)
    {
        EnsureTilemap();
        if (territoryTilemap == null) return Color.clear;
        var pos = new Vector3Int(mid.x * MidChunkSize, mid.y * MidChunkSize, 0);
        if (territoryTilemap.GetTile(pos) == null) return Color.clear;
        return territoryTilemap.GetColor(pos);
    }

    /// <summary>已染色 mid 数。冒烟探针用。</summary>
    public int PaintedCount => _painted.Count;

    /// <summary>已染色 kingdomId 去重集。冒烟探针用。</summary>
    public List<int> GetPaintedKingdoms()
    {
        var set = new HashSet<int>(_painted.Values);
        return new List<int>(set);
    }

    /// <summary>当前档位（探针用）。</summary>
    public int CurrentLodForProbe => _lod;

    /// <summary>层是否激活（探针用：近景隐藏断言）。</summary>
    public bool IsLayerActive => territoryTilemap != null && territoryTilemap.gameObject.activeSelf;
}
