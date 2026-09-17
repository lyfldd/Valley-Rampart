using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全资源刷新系统 —— **资源池模型**（`03_地图即数据库` §6.7 / §6.7.2 / §6.7.3 · `HH.294` 片 5）。
///
/// <b>本片（片 5）改了什么</b>（治 `F-12` 的 120× 偏差 ＋ 兑现 `D762` 资源池参数）：
/// <list type="bullet">
///   <item><b>4-A 删第二日历</b>：删 `RespawnConfig.daySeconds` 与 `_elapsed`／`_currentDay` —— 时间基准改
///         <b>游戏天</b>（`TimeManager.CurrentDay`），订阅 <see cref="TimeDayChangedEvent"/>，**每天推进一次**（非每帧/每秒）。</item>
///   <item><b>4-D／4-E 池子模型</b>：改前是「**逐格到期表**」（`_data[]`／`_entity[]`，各记 cell ＋ dueGameDay）；
///         现改为「**按大区块记池子**」（`_count[区块 × 类]` ＋ `_targetF[区块]`）——
///         与 `03` §6.7「户口＝锚点所在大区块；**重生记区块不记格**」一致。</item>
///   <item><b>单类上限 / 每日补量 / 分帧分摊</b>：见 <see cref="SettleDay"/>（每天一次·入队）＋ <see cref="FlushPending"/>（当天分帧落格）。</item>
///   <item><b>落点</b>：走落点器**随机落新位**（同区块内）＋ 兜底＝原位（<see cref="PickCell"/>）。</item>
/// </list>
///
/// <b>两条采集链</b>（承 `HH.10` 裁决三，未变）：
///   • 数据路径（Tree）：数据格，不建实体 —— 砍完格翻 Plain ⇒ <see cref="HandleTreeGathered"/> ⇒ 池子减 1 点。
///   • 实体路径（OreVein/WoodPile/StonePile）：一次性实体（Building）采集销毁 ⇒ <see cref="HandleEntityDepleted"/>
///      ⇒ 池子减 1 点；补量落格时重建实体（`BuildingFactory.ReSpawnNaturalBuilding`）。
///
/// <b>单位口径（本片落地式）</b>：池子计「**点**」—— 树／石堆／木堆／矿脉各 1 坑位 ＝ 1 点；
///   **矿洞 1 簇（2×2 Cell）＝ 1 点**（据 `Σ_i E_i = cap` 自洽 ＋ `03` §6.7.1「矿洞 2.2（≈2 簇）」）。
/// </summary>
public class ResourceRespawnSystem : Singleton<ResourceRespawnSystem>, ISaveable
{
    public string SaveId => "ResourceRespawnSystem";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    // ==========================================================================
    //  池子口径常量（`03` §6.7 系列）
    // ==========================================================================

    const int KindCount = MapGenRules.ResourceKindCount;
    const int ChunkSize = MapGenRules.ChunkSize;

    /// <summary>`03` §6.7.3：`base = 0.0124 天`（≈4.5 秒）是**连续模型的中间量**，`03` 明示「**不进参数表**」
    /// （「落地用『每天补几个』表述」）⇒ 落为**代码常量**，不作 SO 字段。</summary>
    const float IntervalBaseDays = 0.0124f;

    /// <summary>日补量闭式（`03` §6.7.2 曲线由此**自然涌现**）：`x ＝ 现有 ÷ 上限`、`d/dt (1+3x)³ = 9 / (base × 96)`
    /// ⇒ 相对曲线与 `cap` 无关，绝对补量按 `cap/96` 缩放（第 1 天 ≈ +14 → 第 7 天 ≈ 满）。</summary>
    const float DayCubeStep = 9f / (IntervalBaseDays * 96f);

    /// <summary>分帧分摊额度（**三重**）：每帧最多处理的**区块数**、最多**落格点数**（主控·防单帧尖峰）、
    /// 以及单帧**时间预算**（ms）。区块未补完时**不推进游标**、下一帧续做（可跨帧续）。
    /// 口径：全图 256 区块 × 每天 ≈8 点 ≈ **2100 点/天**，⛔ 禁「一天一帧全落」。</summary>
    const int MaxChunksPerFlush = 4;
    const int MaxPlacementsPerFlush = 16;
    const double FlushBudgetMs = 2.0;

    /// <summary>每区块「最近空出的格」保留数（供**不原地复活**避让 ＋ 抽不到位置时**兜底原位**）。</summary>
    const int FreedKeep = 32;

    const int SaveVersion = 2;

    // ==========================================================================
    //  池子状态
    // ==========================================================================

    int _cw, _ch, _difficulty, _mapW, _mapH;
    int[] _count;          // [区块 × KindCount + 类] ＝ 点数
    float[] _targetF;      // [区块] ＝ 当天目标点数（浮点·整数部分即当天应达量）
    byte[] _chunkBand;     // [区块] ＝ 主导温度带（开局定，配额查表用）
    List<int>[] _freed;    // [区块] ＝ 最近空出的格号（FIFO·上限 FreedKeep）
    readonly List<int> _pendingChunks = new List<int>();
    int _pendingCursor;
    int _placedThisFrame;
    readonly List<int> _candFresh = new List<int>();   // 未空出过的空格（落点首选）
    readonly List<int> _candFreed = new List<int>();   // 空出过的空格（兜底原位）
    int _candFreshCursor, _candFreedCursor;
    System.Random _rng;    // 落点抽签（按 map.seed × 天 定种 ⇒ 同日可复现）
    bool _ready;
    int _lastSettleDay = -1;
    float[] _bandCap = new float[4];
    float[] _bandShare = new float[4 * KindCount];
    float[] _bandKindCap = new float[4 * KindCount];
    int[] _tmpHave = new int[KindCount];

    // ===== 读数口（判据 8／9 与探针）=====
    /// <summary>池子是否已按当前地图就绪。</summary>
    public bool PoolReady => _ready;
    public int PoolChunkW => _cw;
    public int PoolChunkH => _ch;
    public int PoolChunkCount => _cw * _ch;
    /// <summary>**每日结算被调用次数**（判据 8：一游戏天 ＝ 1）。</summary>
    public int DaySettleCount { get; private set; }
    /// <summary>最近一次已结算的游戏天（幂等游标）。</summary>
    public int LastSettleDay => _lastSettleDay;
    /// <summary>最近一次每日结算的**计划补量**（点）。</summary>
    public int LastDayPlannedAdditions { get; private set; }
    /// <summary>最近一次每日结算**实际落格**点数。</summary>
    public int LastDayPlacedPoints { get; private set; }
    /// <summary>最近一次分帧落格的**最大单帧耗时**（ms·判据 9）。</summary>
    public float LastFlushMaxFrameMs { get; private set; }
    /// <summary>最近一次每日结算落格**占用帧数**（判据 9）。</summary>
    public int LastFlushFrames { get; private set; }
    /// <summary>最近一次每日结算里走**兜底（用空出过的格）**的落格数。</summary>
    public int FreedFallbackPlacements { get; private set; }
    /// <summary>当天补量是否仍待落格。</summary>
    public bool HasPendingWork => _pendingCursor < _pendingChunks.Count;

    public static bool HasInstance => Instance != null;

    private RespawnConfig Cfg => RespawnConfig.Instance;

    static MapGenRulesConfig _mapCfgCache;
    static MapGenRulesConfig MapCfg
    {
        get
        {
            if (_mapCfgCache == null)
                _mapCfgCache = Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");
            return _mapCfgCache;
        }
    }

    protected override void Awake()
    {
        base.Awake();   // Singleton：自动创建实例 + DontDestroyOnLoad
        if (_instance != this) return;
        EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);   // 4-A：时间基准＝游戏天·每天推进一次
        if (SaveManager.Instance != null) SaveManager.Instance.RegisterSaveable(this);
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        EventBus.Unsubscribe<TimeDayChangedEvent>(OnDayChanged);
        base.OnDestroy();
    }

    // ==========================================================================
    //  1. 池子建立（新地图/读档 ⇒ 按当前地图重建）
    // ==========================================================================

    /// <summary>新地图/读档后**按当前地图重建池子**（由 `WorldManager.GenerateMap` 地图就绪时调）。
    /// ⚠️ **必须显式传 `map`／`difficulty`**：该调用点在 `_world.maps.Add(map)` **之前**，`ActiveMap` 仍为 null。</summary>
    public void ResetRespawns(MapData map, int difficulty)
    {
        _ready = false;
        DaySettleCount = 0;
        LastDayPlannedAdditions = 0;
        LastDayPlacedPoints = 0;
        LastFlushMaxFrameMs = 0f;
        LastFlushFrames = 0;
        FreedFallbackPlacements = 0;
        _lastSettleDay = -1;
        _pendingChunks.Clear();
        _pendingCursor = 0;
        if (map == null || map.features == null || map.climateZones == null) return;

        RespawnConfigBootstrap(map, difficulty);
    }

    void RespawnConfigBootstrap(MapData map, int difficulty)
    {
        var cfg = MapCfg;
        _cw = MapGenRules.ChunkW(map);
        _ch = MapGenRules.ChunkH(map);
        _mapW = map.width;
        _mapH = map.height;
        _difficulty = Mathf.Clamp(difficulty, 1, 3);

        int n = _cw * _ch;
        if (_count == null || _count.Length != n * KindCount) _count = new int[n * KindCount];
        else System.Array.Clear(_count, 0, _count.Length);
        if (_targetF == null || _targetF.Length != n) _targetF = new float[n];
        if (_chunkBand == null || _chunkBand.Length != n) _chunkBand = new byte[n];
        if (_freed == null || _freed.Length != n) _freed = new List<int>[n];

        // 每带口径缓存（`cap` / `p_i` / `capKind_i`）—— 生成期与运行期共用同一解算式（`MapGenRules.ResolvePoolQuota`）
        var share = new float[KindCount];
        var kindCap = new float[KindCount];
        for (int b = 0; b < 4; b++)
        {
            MapGenRules.ResolvePoolQuota(cfg, (ClimateZone)b, _difficulty, share, kindCap, out _bandCap[b]);
            for (int t = 0; t < KindCount; t++)
            {
                _bandShare[b * KindCount + t] = share[t];
                _bandKindCap[b * KindCount + t] = kindCap[t];
            }
        }

        int totalPoints = 0;
        for (int cy = 0; cy < _ch; cy++)
            for (int cx = 0; cx < _cw; cx++)
            {
                int ci = cy * _cw + cx;
                _chunkBand[ci] = (byte)MapGenRules.DominantZoneOfChunk(map, cx, cy);
                if (_freed[ci] == null) _freed[ci] = new List<int>(FreedKeep);
                else _freed[ci].Clear();
                // 开局池内容 ＝ **按锚点归属**统计当前地图实际点数（4-C：跨界不重算）
                int sum = MapGenRules.CountChunkPoints(map, cx, cy, _tmpHave);
                for (int t = 0; t < KindCount; t++) _count[ci * KindCount + t] = _tmpHave[t];
                _targetF[ci] = sum;      // 起手目标＝当前量 ⇒ 曲线以「开局 40%」为起点
                totalPoints += sum;
            }

        _rng = new System.Random(map.seed * 73856093 ^ 0x5bf03635);
        _ready = true;
        Debug.Log($"[ResourceRespawnSystem] 资源池建立（`03` §6.7）：{_cw}×{_ch} 区块，开局点数 **{totalPoints}**" +
                  $"（均值 {(_cw * _ch > 0 ? totalPoints / (float)(_cw * _ch) : 0f):0.0}/区块），难度 {_difficulty}，" +
                  $"cap 四带=[{_bandCap[0]:0.0},{_bandCap[1]:0.0},{_bandCap[2]:0.0},{_bandCap[3]:0.0}]");
    }

    // ==========================================================================
    //  2. 每日结算（4-E：每天一次 ⇒ 入队；落格分帧分摊）
    // ==========================================================================

    void OnDayChanged(TimeDayChangedEvent evt) => SettleDay(evt.NewDay);

    /// <summary>⭐ **每日结算（每天一次）**：逐区块按 `03` §6.7.2 曲线算当天目标 ⇒ 计划补量**入队**；
    /// 落格由 <see cref="FlushPending"/> **分帧分摊**（⛔ 不在本方法内全图落格）。
    /// 运行期唯一入口 ＝ <see cref="TimeDayChangedEvent"/>（探针可直接调用以快进）。</summary>
    public void SettleDay(int day)
    {
        if (!_ready) return;
        if (day <= _lastSettleDay) return;            // 幂等 ＋ 单调：同一游戏天只结算一次、回退天不重算
        _lastSettleDay = day;
        DaySettleCount++;

        var cfg = Cfg;
        if (cfg == null || !cfg.enabled) return;

        _rng = new System.Random(_mapW * 131 + _mapH * 137 + day * 19349663);
        int nc = _cw * _ch;
        for (int ci = 0; ci < nc; ci++)
        {
            float cap = _bandCap[_chunkBand[ci]];
            _targetF[ci] = DailyTarget(PointsOf(ci), cap);   // `03` §6.7.2 曲线（以**当前量**为 x 起点）
        }
        // 待落格队列**按目标重算**（而非清空重排）⇒ 上一天没落完的欠量不丢
        RebuildPendingFromTargets();
        int planned = 0;
        for (int i = 0; i < _pendingChunks.Count; i++)
        {
            int ci = _pendingChunks[i];
            planned += Mathf.Max(0, Mathf.FloorToInt(_targetF[ci]) - PointsOf(ci));
        }
        LastDayPlannedAdditions = planned;
        LastDayPlacedPoints = 0;
        LastFlushMaxFrameMs = 0f;
        LastFlushFrames = 0;
        FreedFallbackPlacements = 0;
    }

    /// <summary>`03` §6.7.2 曲线（闭式）：`x ＝ 现有 ÷ 上限`；`(1+3x)³` 每天 +`9/(base×96)` ⇒ 反解新 `x`。
    /// ⇒ 前快后慢（≈+14 → ≈+6）**自然涌现**，无需手调档位。</summary>
    public static float DailyTarget(float total, float cap)
    {
        if (cap <= 0f) return 0f;
        float x = Mathf.Clamp01(total / cap);
        float t = 1f + 3f * x;
        float u = t * t * t + DayCubeStep;            // 闭式推进（等价「间隔 = base×(1+3x)²」连续解）
        float xn = Mathf.Clamp01((Mathf.Pow(u, 1f / 3f) - 1f) / 3f);
        return xn * cap;
    }

    /// <summary>分帧落格（每帧由 `Update` 调；**无待落格时立即返回 ⇒ 不扫全图**）。
    /// 每帧至多 <see cref="MaxChunksPerFlush"/> 个区块 ＋ <see cref="FlushBudgetMs"/> 时间预算。</summary>
    public void FlushPending()
    {
        if (!HasPendingWork) return;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int chunks = 0;
        _placedThisFrame = 0;
        while (_pendingCursor < _pendingChunks.Count && chunks < MaxChunksPerFlush)
        {
            int ci = _pendingChunks[_pendingCursor];
            bool done = PlaceChunkFill(map, ci);   // false ⇒ 本帧额度用尽（区块未补完 ⇒ 游标不推进·下帧续做）
            if (!done) break;
            _pendingCursor++;
            chunks++;
            if (sw.Elapsed.TotalMilliseconds >= FlushBudgetMs) break;
        }
        sw.Stop();
        LastFlushFrames++;
        if (sw.Elapsed.TotalMilliseconds > LastFlushMaxFrameMs)
            LastFlushMaxFrameMs = (float)sw.Elapsed.TotalMilliseconds;
        if (!HasPendingWork) { _pendingChunks.Clear(); _pendingCursor = 0; }
    }

    private void Update()
    {
        // ⚠️ 本方法**不再**推进任何日历（4-A：删 `_elapsed`／`_currentDay`）；
        //    只是把「当天已入队的补量」分帧落格 —— 无待落格时零工作。
        FlushPending();
    }

    // ==========================================================================
    //  3. 落格（同区块内·随机新位＋兜底原位）
    // ==========================================================================

    /// <summary>补足单个区块至当天目标。返回 false ⇒ **本帧落格额度用尽**（区块未补完，下一帧从同一区块续做）。</summary>
    bool PlaceChunkFill(MapData map, int ci)
    {
        float cap = _bandCap[_chunkBand[ci]];
        int total = PointsOf(ci);
        int want = Mathf.FloorToInt(_targetF[ci]);
        if (want <= total) return true;

        BuildCandidates(map, ci);
        int mineBlocked = -1;
        while (total < want)
        {
            if (_placedThisFrame >= MaxPlacementsPerFlush) return false;   // 分帧分摊：额度用尽 ⇒ 下帧续
            int kind = PickKind(ci, total, cap, mineBlocked);
            if (kind < 0) break;
            if (!PlaceOne(map, ci, kind))
            {
                // 非矿类落不下 ⇒ 候选已尽；矿洞类落不下 ⇒ 本区块无 2×2 空位，**换类**继续（不吞掉当天补量）
                if (kind == MapGenRules.ResMine) { mineBlocked = kind; continue; }
                break;
            }
            total++;
            _placedThisFrame++;
            LastDayPlacedPoints++;
        }
        return true;
    }

    /// <summary>候选格分桶：**未空出过**的格（首选）／**空出过**的格（兜底原位）；均 shuffle ⇒ 随机落新位。</summary>
    void BuildCandidates(MapData map, int ci)
    {
        _candFresh.Clear();
        _candFreed.Clear();
        _candFreshCursor = 0;
        _candFreedCursor = 0;
        int cx = ci % _cw, cy = ci / _cw;
        int x0 = cx * ChunkSize, y0 = cy * ChunkSize;
        var cfg = MapCfg;
        for (int y = y0; y < y0 + ChunkSize && y < map.height; y++)
            for (int x = x0; x < x0 + ChunkSize && x < map.width; x++)
            {
                if (x < MapGenRules.OceanThickness || y < MapGenRules.OceanThickness
                    || x >= map.width - MapGenRules.OceanThickness || y >= map.height - MapGenRules.OceanThickness) continue;
                if (MapGate.ReadAt(map, x, y) != FeatureType.Plain) continue;
                if (MapGenRules.IsInKingdomClearZone(map, x, y, cfg)) continue;
                int i = MapGenRules.Idx(map, x, y);
                if (_freed[ci] != null && _freed[ci].Contains(i)) _candFreed.Add(i);
                else _candFresh.Add(i);
            }
        Shuffle(_rng, _candFresh);
        Shuffle(_rng, _candFreed);
    }

    /// <summary>⭐ 落点选取：`fresh`（未空出过的格）优先；`fresh` 抽尽 ⇒ **兜底＝原位**（`freed`：刚空出来的格，
    /// 必然合法）；都无 ⇒ -1（本区块不可落）。**产出＝「重生不在原位」的可判定读数**。</summary>
    public static int PickCell(List<int> fresh, ref int freshCursor, List<int> freed, ref int freedCursor)
    {
        if (fresh != null && freshCursor < fresh.Count) return fresh[freshCursor++];
        if (freed != null && freedCursor < freed.Count) return freed[freedCursor++];
        return -1;
    }

    bool PlaceOne(MapData map, int ci, int kind)
    {
        var feature = MapGenRules.ResourceKindFeature[kind];
        if (kind == MapGenRules.ResMine)
        {
            if (!TryPlaceMineCluster(map, ci, out _)) return false;   // 1 簇（2×2）＝ 1 点
            _count[ci * KindCount + kind]++;
            return true;
        }

        int freshBefore = _candFreshCursor;
        int cell = PickCell(_candFresh, ref _candFreshCursor, _candFreed, ref _candFreedCursor);
        if (cell < 0) return false;
        bool fallback = _candFreshCursor == freshBefore;   // fresh 未推进 ⇒ 取自 freed 桶（兜底＝原位）
        int x2 = cell % map.width, y2 = cell / map.width;
        var coord = new GridCoord(x2, y2);
        if (!MapGate.PlaceResourceNode(coord, feature)) return false;   // 增门（幂等：非空格 ⇒ false）
        _count[ci * KindCount + kind]++;
        if (fallback) FreedFallbackPlacements++;
        if (_freed[ci] != null) _freed[ci].Remove(cell);
        SpawnEntityFor(coord, feature);
        return true;
    }

    /// <summary>矿洞落点：同区块内侧向 2×2 整块（避开海洋带 / 主城净空区）⇒ **1 簇 ＝ 1 点**。</summary>
    bool TryPlaceMineCluster(MapData map, int ci, out GridCoord anchor)
    {
        anchor = default;
        int side = MapGenRules.MineClusterSide;
        int n = _candFresh.Count + _candFreed.Count;
        for (int k = 0; k < n; k++)
        {
            int cell = PickCell(_candFresh, ref _candFreshCursor, _candFreed, ref _candFreedCursor);
            if (cell < 0) break;
            int ox = cell % map.width, oy = cell / map.width;
            if (ox + side > map.width || oy + side > map.height) continue;
            if (ox < MapGenRules.OceanThickness || oy < MapGenRules.OceanThickness
                || ox + side > map.width - MapGenRules.OceanThickness
                || oy + side > map.height - MapGenRules.OceanThickness) continue;
            bool clear = true;
            for (int dy = 0; dy < side && clear; dy++)
                for (int dx = 0; dx < side && clear; dx++)
                    if (MapGate.ReadAt(map, ox + dx, oy + dy) != FeatureType.Plain) clear = false;
            if (!clear) continue;
            if (MapGenRules.IsInKingdomClearZone(map, ox, oy, MapCfg)
                || MapGenRules.IsInKingdomClearZone(map, ox + side - 1, oy + side - 1, MapCfg)) continue;
            for (int dy = 0; dy < side; dy++)
                for (int dx = 0; dx < side; dx++)
                    MapGate.PlaceResourceNode(new GridCoord(ox + dx, oy + dy), FeatureType.Mine);
            anchor = new GridCoord(ox, oy);
            return true;
        }
        return false;
    }

    /// <summary>实体类资源（OreVein/WoodPile/StonePile）落格后**重建 Building 实体**（承 `HH.10`：格存在但无实体 ⇒ 断供）。</summary>
    static void SpawnEntityFor(GridCoord coord, FeatureType feature)
    {
        if (feature != FeatureType.OreVein && feature != FeatureType.WoodPile && feature != FeatureType.StonePile) return;
        if (BuildingFactory.Instance != null)
            BuildingFactory.Instance.ReSpawnNaturalBuilding(coord, feature);
    }

    /// <summary>⭐ 重生条件（`03` §6.7 表）：① 总点数 < `cap` ② 该类点数 < 单类上限 `capKind_i`。
    /// 生产与判据**同一处谓词**（探针可直调构造反证）。</summary>
    public static bool KindAllowed(int kindPoints, float kindCap, int totalPoints, float cap)
        => totalPoints < cap && kindPoints < kindCap;

    /// <summary>按权重 `p_i` 抽一类补（**区块统一调度**）；该类已达单类上限或总点数已达 `cap` ⇒ 换类/停止。</summary>
    int PickKind(int ci, int total, float cap, int excludeKind = -1)
    {
        int band = _chunkBand[ci];
        int baseIdx = band * KindCount;
        float sum = 0f;
        for (int t = 0; t < KindCount; t++)
        {
            if (t == excludeKind) continue;
            if (!KindAllowed(_count[ci * KindCount + t], _bandKindCap[baseIdx + t], total, cap)) continue;
            sum += _bandShare[baseIdx + t];
        }
        if (sum <= 0f) return -1;
        double r = _rng.NextDouble() * sum;
        for (int t = 0; t < KindCount; t++)
        {
            if (t == excludeKind) continue;
            if (!KindAllowed(_count[ci * KindCount + t], _bandKindCap[baseIdx + t], total, cap)) continue;
            r -= _bandShare[baseIdx + t];
            if (r <= 0d) return t;
        }
        return -1;
    }

    // ==========================================================================
    //  4. 采集 ⇒ 池子减 1 点（两条采集链）
    // ==========================================================================

    /// <summary>确认采集一棵树（数据格）：校验该格是 Tree feature → 创建 TreeGatherSource 注册进调度器。</summary>
    public bool ConfirmTreeGather(GridCoord cell)
    {
        if (!Cfg || !Cfg.enabled) return false;
        if (MapGate.GetFeatureAt(cell) != FeatureType.Tree) return false;

        Vector2 pos = GridSystem.Instance != null ? GridSystem.Instance.CoordToWorld(cell) : Vector2.zero;
        var src = new TreeGatherSource(cell, pos, Cfg.treeGatherSeconds, Cfg.treeGatherAmount);
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.Register(src);
        else return false;
        return true;
    }

    /// <summary>树被砍完成（TreeGatherSource.OnGatherCompletion 调）：格翻 Plain ＋ 守卫失去 ＋ **池子减 1 点**。</summary>
    public void HandleTreeGathered(GridCoord cell)
    {
        if (Cfg == null || !Cfg.enabled) return;
        if (MapGate.RemoveResourceNode(cell))       // 删门（唯一入口·幂等）
        {
            // 守卫锚点语义（HH.3 §六 / HH.6）：高价值资源点（树）被采走/覆盖 ⇒ 守卫区域失覆盖 ⇒ LostEvent
            GuardDeploymentSystem.HandleResourceConsumed(cell);
            NotifyConsumed(cell, FeatureType.Tree);
        }
    }

    /// <summary>一次性实体被采集销毁（Building.OnGatherCompleted 调）：格翻 Plain ＋ **池子减 1 点**。</summary>
    public void HandleEntityDepleted(GridCoord cell, FeatureType feature)
    {
        if (Cfg == null || !Cfg.enabled) return;
        if (MapGate.RemoveResourceNode(cell))
            NotifyConsumed(cell, feature);
    }

    /// <summary>池子减 1 点 ＋ 记「该格刚空出」（供落点避让／兜底原位）。矿洞按簇计，单格消费不改点数（见交付报告）。</summary>
    void NotifyConsumed(GridCoord cell, FeatureType feature)
    {
        if (!_ready) return;
        if (cell.x < 0 || cell.y < 0 || cell.x >= _mapW || cell.y >= _mapH) return;
        int kind = KindOfFeature(feature);
        if (kind < 0) return;
        int ci = (cell.y / ChunkSize) * _cw + (cell.x / ChunkSize);   // `03` §6.8 A 类：户口＝锚点所在大区块
        if (ci < 0 || ci >= _cw * _ch) return;
        if (kind != MapGenRules.ResMine && _count[ci * KindCount + kind] > 0)
            _count[ci * KindCount + kind]--;
        if (_freed[ci] == null) _freed[ci] = new List<int>(FreedKeep);
        if (_freed[ci].Count >= FreedKeep) _freed[ci].RemoveAt(0);
        _freed[ci].Add(cell.y * _mapW + cell.x);
    }

    static int KindOfFeature(FeatureType f)
    {
        for (int t = 0; t < KindCount; t++)
            if (MapGenRules.ResourceKindFeature[t] == f) return t;
        return -1;
    }

    static void Shuffle<T>(System.Random rng, List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    // ==========================================================================
    //  5. 读数口（探针／HUD）
    // ==========================================================================

    public int PointsOf(int ci)
    {
        if (!_ready || ci < 0 || ci >= _cw * _ch) return 0;
        int s = 0;
        for (int t = 0; t < KindCount; t++) s += _count[ci * KindCount + t];
        return s;
    }

    public int PointsOf(int ci, int kind)
        => !_ready || ci < 0 || ci >= _cw * _ch || kind < 0 || kind >= KindCount ? 0 : _count[ci * KindCount + kind];

    public float CapOf(int ci)
        => !_ready || ci < 0 || ci >= _cw * _ch ? 0f : _bandCap[_chunkBand[ci]];

    public float KindCapOf(int ci, int kind)
        => !_ready || ci < 0 || ci >= _cw * _ch || kind < 0 || kind >= KindCount ? 0f : _bandKindCap[_chunkBand[ci] * KindCount + kind];

    public float ShareOf(int ci, int kind)
        => !_ready || ci < 0 || ci >= _cw * _ch || kind < 0 || kind >= KindCount ? 0f : _bandShare[_chunkBand[ci] * KindCount + kind];

    public int BandOf(int ci)
        => !_ready || ci < 0 || ci >= _cw * _ch ? -1 : _chunkBand[ci];

    // ==========================================================================
    //  6. 存档（结构变更：逐格到期表 ⇒ 区块池子表）
    // ==========================================================================

    public SavePayload SaveState()
    {
        var data = new ResourceRespawnSaveData
        {
            version = SaveVersion,
            cw = _cw,
            ch = _ch,
            mapW = _mapW,
            mapH = _mapH,
            difficulty = _difficulty,
            lastSettleDay = _lastSettleDay,
            daySettleCount = DaySettleCount,
            counts = _ready && _count != null ? (int[])_count.Clone() : new int[0],
            targets = _ready && _targetF != null ? (float[])_targetF.Clone() : new float[0]
        };
        return new SavePayload
        {
            typeName = typeof(ResourceRespawnSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = SaveVersion
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(ResourceRespawnSaveData).AssemblyQualifiedName) return;
        try
        {
            var data = JsonUtility.FromJson<ResourceRespawnSaveData>(payload.json);
            if (data == null || data.version != SaveVersion)
            {
                Debug.LogWarning($"[ResourceRespawnSystem] 存档资源池版本不符（{data?.version ?? 0} ≠ {SaveVersion}）⇒ 放弃池子记录，按当前地图重建。");
                return;
            }
            var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
            if (map == null || !_ready
                || data.cw != _cw || data.ch != _ch || data.mapW != _mapW || data.mapH != _mapH)
            {
                Debug.LogWarning("[ResourceRespawnSystem] 存档资源池与当前地图不符 ⇒ 放弃池子记录，按当前地图重建。");
                return;
            }
            if (data.counts != null && data.counts.Length == _count.Length)
                System.Array.Copy(data.counts, _count, _count.Length);
            if (data.targets != null && data.targets.Length == _targetF.Length)
                System.Array.Copy(data.targets, _targetF, _targetF.Length);
            _lastSettleDay = data.lastSettleDay;
            DaySettleCount = data.daySettleCount;
            RebuildPendingFromTargets();
            Debug.Log($"[ResourceRespawnSystem] 存档恢复：资源池 {_cw}×{_ch} 区块·末结算天 {_lastSettleDay}·待落格区块 {_pendingChunks.Count}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[ResourceRespawnSystem] 存档恢复失败，放弃池子记录：{ex.Message}");
        }
    }

    /// <summary>读档后**从目标重建待落格队列**（未落格的差值 ＝ 当天欠量）。</summary>
    void RebuildPendingFromTargets()
    {
        _pendingChunks.Clear();
        _pendingCursor = 0;
        int nc = _cw * _ch;
        for (int ci = 0; ci < nc; ci++)
        {
            int total = PointsOf(ci);
            if (Mathf.FloorToInt(_targetF[ci]) - total > 0) _pendingChunks.Add(ci);
        }
        if (_pendingChunks.Count > 0) _pendingCursor = 0;
    }
}

/// <summary>资源池存档（`HH.294` 片 5：**逐格到期表 ⇒ 区块池子表**）。
/// ⚠️ 结构随片 5 模型变更加版本号；旧版存档（`version ≠ 2`）**不迁移**，按当前地图重建池子。</summary>
[System.Serializable]
public class ResourceRespawnSaveData
{
    public int version;
    public int cw, ch;          // 区块尺寸（与当前地图不符 ⇒ 放弃）
    public int mapW, mapH;      // 地图格尺寸
    public int difficulty;
    public int lastSettleDay;   // 幂等：同一天不重复结算
    public int daySettleCount;
    public int[] counts;        // [区块 × 5] 点数
    public float[] targets;     // [区块] 当天目标
}
