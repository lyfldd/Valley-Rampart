using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  2_4 LOD 区块划分 - LODSystem 中区块思考分区管理器（2D 多中心活跃带）
//  详见 2_4_LOD区块划分.md §5 设计 / 实施计划步骤 1~6
//  替代旧 region 粒度的 1D LOD（3.0.1_LOD 架构）。
//
//  职责：中区块 LOD 状态持有 / 2D 多中心活跃带判定 / 热度累积衰减扩散 /
//        升降级防抖 / 事件覆盖（受击·敌人进中区块升档）/ 稀疏登记（D80）/ Gizmos
//  关键改造对比旧版：
//    - 区块粒度 region(大区块) → 中区块(4×4 小区块)
//    - 活跃带 1D 带状 → 2D 多中心（主城锚点 + 战斗热点，切比雪夫）
//    - 状态全量数组 → Dictionary<Vector2Int, MidChunkLodState> 稀疏（惰性登记）
//    - 调参 SO：AttentionTuningConfig → LodConfig 真源（旧字段保留保 sim 同源）
//  对外查询 API 保持 GetLevelAt(worldPos)/GetHeatAt(worldPos) 签名，
//  内部改为 2D 中区块查询，兼容 NPCBrain/FormationBrain 既有调用。
// ============================================================================

/// <summary>热度事件源（2_4 步骤 4：事件注入量按源取 SO）</summary>
public enum HeatSource { Hit, EnemyEnter, AllyRetreat, ValueConflict }

public class LODSystem : Singleton<LODSystem>
{
    [Tooltip("2D 中区块 LOD 调参 SO（自动加载 Resources/Config/LodConfig；缺失回退默认值）")]
    private LodConfig _config;

    // 中区块坐标 -> LOD+热度状态（稀疏：只登记活跃带附近 + 有热度的，D80）
    // 2_4③ 回收口径（D455，2_17 步骤13 定案）：保持现役稀疏登记不逐条回收——「MapGenerated 整体清空即回收」。
    // 登记有界（≤ 活跃带 + 历史热点，受地图中区块总量硬上限），逐条回收时间相关会威胁同 seed 确定性 + 丢热度记忆。
    private readonly Dictionary<Vector2Int, MidChunkLodState> _midStates = new Dictionary<Vector2Int, MidChunkLodState>();

    // 军队锚点注册表（拓宽保留：外部编队/守卫可注册，为中心候选）
    private readonly List<Transform> _armyCenters = new List<Transform>();

    // 玩家/上帝视角焦点中区块（2_8 提供；缺省用主城锚点）
    private Vector2Int? _focalMidChunk;

    // 活跃中心缓存（主城 + 热点，≤ LandConfig.maxCenters）
    private readonly List<Vector2Int> _activeCenters = new List<Vector2Int>();

    // 多中心活跃带覆盖集合（由 ActiveCenters 渲染，避免每帧 O(4096)）
    private readonly HashSet<Vector2Int> _activeBandSet = new HashSet<Vector2Int>();

    private float _heatTickAccum;      // 热度扩散/衰减 tick 累加（1Hz）
    private const float HeatTickHz = 1f;

    /// <summary>活跃中心列表（≤ maxCenters，供 2_6 活跃带范围/2_8 守卫联动）</summary>
    public IReadOnlyList<Vector2Int> ActiveCenters => _activeCenters;

    /// <summary>当前活跃带内登记的活跃中区块数（冒烟/评估稀疏性用）</summary>
    public int ActiveBandCount => _activeBandSet.Count;

    protected override void Awake()
    {
        base.Awake();
        _config = Resources.Load<LodConfig>("Config/LodConfig");
        if (_config == null)
            Debug.LogWarning("[LODSystem] 未找到 LodConfig，回退默认值（网格类字段用内置默认）");
        EventBus.Subscribe<UnitDamagedEvent>(OnUnitDamaged);
        EventBus.Subscribe<EnemyEnteredChunkEvent>(OnEnemyEnteredChunk);
        EventBus.Subscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        base.OnDestroy();
        EventBus.Unsubscribe<UnitDamagedEvent>(OnUnitDamaged);
        EventBus.Unsubscribe<EnemyEnteredChunkEvent>(OnEnemyEnteredChunk);
        EventBus.Unsubscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    // ===== 懒登记（D80）：查询/注入时按需创建中区块状态 =====

    private MidChunkLodState GetOrCreateState(Vector2Int mid)
    {
        if (!_midStates.TryGetValue(mid, out var s))
        {
            s = new MidChunkLodState(mid);
            _midStates[mid] = s;
        }
        return s;
    }

    private bool TryGetState(Vector2Int mid, out MidChunkLodState s) => _midStates.TryGetValue(mid, out s);

    /// <summary>世界坐标 → 中区块坐标（复用 GridSystem.CellToMidChunk）。</summary>
    private bool WorldToMidChunk(Vector2 worldPos, out Vector2Int mid)
    {
        mid = default;
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return false;
        var coord = GridSystem.Instance.WorldToCoord(worldPos);
        if (!coord.HasValue) return false;
        mid = GridSystem.Instance.CellToMidChunk(coord.Value);
        return true;
    }

    private Vector2 MidChunkToWorldCenter(Vector2Int mid)
    {
        // 中区块中心的格坐标 = mid*midChunkSize + midChunkSize/2
        int ms = Ms();
        var centerCell = new GridCoord(mid.x * ms + ms / 2, mid.y * ms + ms / 2);
        return GridSystem.Instance.CoordToWorld(centerCell);
    }

    private int Ms()
    {
        if (GridSystem.Instance != null && GridSystem.Instance.Config != null
            && GridSystem.Instance.Config.midChunkSize > 0)
            return GridSystem.Instance.Config.midChunkSize;
        return 4;
    }

    // ===== 每帧更新：热度 tick(1Hz) + 活跃带渲染 + 降档防抖 =====

    private void Update()
    {
        float dt = Time.deltaTime;

        // 热度衰减（每帧，简单可靠；衰减速率小无性能压力）——扩散在 1Hz tick 做
        // 衰减在 tick 做更符合 R2（扩散/衰减同 tick 1Hz）。这里衰减走 tick。
        _heatTickAccum += dt;
        if (_heatTickAccum >= 1f / HeatTickHz)
        {
            _heatTickAccum = 0f;
            UpdateHeatTick();
        }

        // 活跃带渲染（O(中心数×半径²)，非 O(4096)）
        RenderActiveBands();

        // 降档防抖：只扫已登记中区块（≤ 活跃带 + 历史热点，稀疏）
        ApplyDowngrade(dt);
    }

    private bool TryLoadFallback()
    {
        _config = Resources.Load<LodConfig>("Config/LodConfig");
        return _config != null;
    }

    /// <summary>热度 tick（1Hz）：扩散 + 衰减。</summary>
    private void UpdateHeatTick()
    {
        if (_midStates.Count == 0) return;
        float spreadTh = Cfg(C => C.heatSpreadThreshold, 0.6f);
        float spreadRatio = Cfg(C => C.spreadRatio, 0.4f);
        float decay = Cfg(C => C.heatDecayRate, 0.05f);
        float invHz = 1f / HeatTickHz;
        float heatMax = Cfg(C => C.heatMax, 1f);

        // 收集扩散量（遍历已登记，避免改 dict 时枚举）
        var spreads = new Dictionary<Vector2Int, float>();
        if (spreadTh > 0f && spreadRatio > 0f)
        {
            foreach (var kv in _midStates)
            {
                var s = kv.Value;
                if (s.threatHeat <= spreadTh) continue;
                float amount = s.threatHeat * spreadRatio;
                foreach (var nb in FourNeighbors(kv.Key))
                {
                    spreads[nb] = spreads.TryGetValue(nb, out var v) ? v + amount : amount;
                }
            }
        }

        // 衰减 + 注入扩散（spreads）→ 申请登记
        foreach (var kv in _midStates)
        {
            var s = kv.Value;
            s.threatHeat = Mathf.Max(0f, s.threatHeat - decay * invHz);  // 衰减
            if (spreads.TryGetValue(kv.Key, out var inflow))
                s.threatHeat = Mathf.Min(heatMax, s.threatHeat + inflow); // 扩散入
        }
        // 扩散产生的新中区块也登记（扩散只在已登记间进行，新块等下一次事件/lazily）
    }

    private static readonly Vector2Int[] _neighborOffset =
        { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

    private static IEnumerable<Vector2Int> FourNeighbors(Vector2Int mid)
    {
        for (int i = 0; i < 4; i++) yield return mid + _neighborOffset[i];
    }

    /// <summary>2D 多中心活跃带渲染：先算中心集，再从每个中心渲染带内中区块升档。</summary>
    private void RenderActiveBands()
    {
        ComputeActiveCenters();
        _activeBandSet.Clear();

        int activeR = Cfg(C => C.activeRadiusMidChunks, 1);
        int semiR = Cfg(C => C.semiActiveRadiusMidChunks, 2);

        // 无中心兜底：全部已登记保持活跃（测试场景/无锚点时不"变傻"）
        if (_activeCenters.Count == 0)
        {
            foreach (var s in _midStates.Values)
                if (s.Level != LodLevel.Active) { s.Level = LodLevel.Active; s.idleTimer = 0f; }
            return;
        }

        var touched = new HashSet<Vector2Int>();
        for (int c = 0; c < _activeCenters.Count; c++)
        {
            var cen = _activeCenters[c];
            for (int dy = -semiR; dy <= semiR; dy++)
            {
                for (int dx = -semiR; dx <= semiR; dx++)
                {
                    var m = new Vector2Int(cen.x + dx, cen.y + dy);
                    int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)); // 切比雪夫
                    if (d > semiR) continue;
                    var level = d <= activeR ? LodLevel.Active : LodLevel.SemiActive;
                    var s = GetOrCreateState(m);            // 登记入活跃带（稀疏扩展）
                    if (s.Level != level) s.Level = level;  // 只升不降（降走防抖）
                    s.idleTimer = 0f;                        // 带内持续在场，不因 idleTimer 降档
                    touched.Add(m);
                }
            }
        }
        _activeBandSet.UnionWith(touched);
    }

    /// <summary>计算活跃中心集（主城/焦点 + 战斗热点，取热度前 maxCenters，D77/D3）。</summary>
    private void ComputeActiveCenters()
    {
        _activeCenters.Clear();

        // 1) 主城/上帝视角焦点中区块
        Vector2Int? anchorMid = _focalMidChunk;
        if (anchorMid == null)
        {
            var monarch = RulerController.Instance != null ? RulerController.Instance.MonarchUnit : null;
            if (monarch != null && WorldToMidChunk(monarch.transform.position, out var mm))
                anchorMid = mm;
            else if (_armyCenters.Count > 0)
            {
                var c = _armyCenters[0];
                if (c != null && WorldToMidChunk(c.position, out var am))
                    anchorMid = am;
            }
        }
        if (anchorMid.HasValue) _activeCenters.Add(anchorMid.Value);

        // 2) 战斗热点：热度 > hotspotThreshold 的中区块，按热度降序取前 N
        int maxC = Mathf.Max(1, Cfg(C => C.maxCenters, 8));
        float hsTh = Cfg(C => C.hotspotThreshold, 0.3f);
        var hotspotList = new List<MidChunkLodState>();
        foreach (var kv in _midStates)
            if (kv.Value.threatHeat > hsTh)
                hotspotList.Add(kv.Value);
        if (hotspotList.Count > 0)
        {
            // 按热度降序（简单选择，量小）
            hotspotList.Sort((a, b) => b.threatHeat.CompareTo(a.threatHeat));
            int take = Mathf.Min(hotspotList.Count, maxC - _activeCenters.Count);
            for (int i = 0; i < take; i++)
                _activeCenters.Add(hotspotList[i].midChunk);
        }
    }

    /// <summary>降档防抖（R6）：仅扫已登记中区块，热度归零 + idleTimer ≥ demoteDelay → 降一级。Dormant 不再降。</summary>
    private void ApplyDowngrade(float dt)
    {
        float idleLimit = Cfg(C => C.demoteDelaySeconds, 3f);
        // 降档候选：不在活跃带内（被带外覆盖则保持，带内已置 idleTimer=0）
        var toDowngrade = new List<Vector2Int>();
        foreach (var kv in _midStates)
        {
            var s = kv.Value;
            if (s.Level == LodLevel.Dormant) continue;
            if (_activeBandSet.Contains(kv.Key)) continue;   // 活跃带内不降
            if (s.threatHeat > 0f || s.idleTimer < idleLimit) continue;
            toDowngrade.Add(kv.Key);
        }
        foreach (var mid in toDowngrade)
        {
            if (!_midStates.TryGetValue(mid, out var s)) continue;
            s.Level = s.Level == LodLevel.Active ? LodLevel.SemiActive : LodLevel.Dormant;
            s.idleTimer = 0f;
        }

        // 全部已登记中区块 idleTimer 递增（活跃带内已在 RenderActiveBands 置 0）
        foreach (var kv in _midStates)
            if (!_activeBandSet.Contains(kv.Key))
                kv.Value.idleTimer += dt;
    }

    // ===== 事件覆盖（升级不走路径 tick）=====

    /// <summary>受击：所在中区块升活跃 + 热度注入 + 记录战斗热点（§3.1 +0.4，用事件受击位置，兼容建筑/单位）。</summary>
    private void OnUnitDamaged(UnitDamagedEvent evt)
    {
        if (!WorldToMidChunk(evt.Position, out var mid)) return;
        var s = GetOrCreateState(mid);
        s.threatHeat = Mathf.Min(HeatMax(), s.threatHeat + Cfg(C => C.heatHitGain, 0.4f));
        s.combatHotspot = evt.Position;
        s.hotspotTime = Time.time;
        s.lastActivityTick = Time.frameCount;
        UpgradeImmediate(s);
    }

    /// <summary>敌入中区块：热度注入 + 升活跃（doc1 EnemyEnteredChunkEvent，chunk 粒度跨到 mid 用 CellToMidChunk）。</summary>
    private void OnEnemyEnteredChunk(EnemyEnteredChunkEvent evt)
    {
        if (evt.Enemy == null || GridSystem.Instance == null) return;
        // evt 给 chunk 坐标；转敌军世界位置取 midChunk
        if (!WorldToMidChunk(evt.Enemy.transform.position, out var mid)) return;
        var s = GetOrCreateState(mid);
        s.threatHeat = Mathf.Min(HeatMax(), s.threatHeat + Cfg(C => C.heatEnemyEnter, 0.2f));
        s.lastActivityTick = Time.frameCount;
        UpgradeImmediate(s);
    }

    private void OnMapGenerated(MapGeneratedEvent evt)
    {
        _midStates.Clear();
        _activeCenters.Clear();
        _activeBandSet.Clear();
        _focalMidChunk = null;
    }

    /// <summary>威胁类事件升档（即时 + 重置 idleTimer 防误降级）。</summary>
    private void UpgradeImmediate(MidChunkLodState s)
    {
        if (s.Level != LodLevel.Active)
        {
            s.Level = LodLevel.Active;
            s.idleTimer = 0f;
        }
        // 发布热度变化（兼容旧订阅者 RegionHeatChangedEvent；中区块坐标语义）
        if (EventBus.HasSubscribers<RegionHeatChangedEvent>())
            EventBus.Publish(new RegionHeatChangedEvent(s.threatHeat >= 0f ? 0 : 0, s.threatHeat, s.Level));
    }

    // ===== 外部注入/注册 =====

    /// <summary>注入热度事件（战斗受击/敌入/友撤/价值冲突 D78）。</summary>
    public void RegisterHeatEvent(GridCoord at, HeatSource src, float amount)
    {
        var mid = GridSystem.Instance.CellToMidChunk(at);
        var s = GetOrCreateState(mid);
        float gain = amount > 0f ? amount
            : (src == HeatSource.EnemyEnter ? Cfg(C => C.heatEnemyEnter, 0.2f)
             : src == HeatSource.Hit ? Cfg(C => C.heatHitGain, 0.4f)
             : src == HeatSource.AllyRetreat ? Cfg(C => C.heatAllyRetreat, 0.05f)
             : Cfg(C => C.hotspotThreshold, 0.3f)); // ValueConflict 用热点阈值量级
        s.threatHeat = Mathf.Min(HeatMax(), s.threatHeat + gain);
        s.lastActivityTick = Time.frameCount;
        UpgradeImmediate(s);
    }

    /// <summary>注册军队/守卫锚点（外部编队登记，为中心候选，D77 多中心来源之一）。</summary>
    public void RegisterArmyCenter(Transform anchor)
    {
        if (anchor != null && !_armyCenters.Contains(anchor)) _armyCenters.Add(anchor);
    }

    public void UnregisterArmyCenter(Transform anchor)
    {
        _armyCenters.Remove(anchor);
    }

    /// <summary>设上帝视角焦点中区块（2_8 玩家视角提供；null 回退主城锚点）。</summary>
    public void SetFocalCenter(Vector2 worldPos)
    {
        if (WorldToMidChunk(worldPos, out var mid)) _focalMidChunk = mid;
    }

    // ===== 对外查询（NPCBrain / FormationBrain 既有签名保持）=====

    /// <summary>查询所在中区块的 LOD 等级（默认 Active 行为不变兜底）。</summary>
    public LodLevel GetLevelAt(Vector2 worldPos)
    {
        if (!WorldToMidChunk(worldPos, out var mid)) return LodLevel.Active;
        return TryGetState(mid, out var s)
            ? s.Level
            : (_activeBandSet.Contains(mid) ? LodLevel.Active : LodLevel.Dormant);
    }

    /// <summary>查询所在中区块的威胁热度。</summary>
    public float GetHeatAt(Vector2 worldPos)
    {
        if (!WorldToMidChunk(worldPos, out var mid)) return 0f;
        return TryGetState(mid, out var s) ? s.threatHeat : 0f;
    }

    /// <summary>查询所在中区块的 Think 频率（活跃 10/半活跃 2/休眠 0.5，LodConfig 真源）。</summary>
    public float GetThinkHz(Vector2 worldPos)
    {
        if (!WorldToMidChunk(worldPos, out var mid)) return ActiveHz();
        var level = TryGetState(mid, out var s) ? s.Level
            : (_activeBandSet.Contains(mid) ? LodLevel.Active : LodLevel.Dormant);
        switch (level)
        {
            case LodLevel.SemiActive: return SemiHz();
            case LodLevel.Dormant: return DormantHz();
            default: return ActiveHz();
        }
    }

    /// <summary>查询中区块是否被活跃带覆盖（2_17 步骤13，D333/D344：SimMode「视野」=LOD 活跃带，D77 同源信号）。</summary>
    /// <remarks>活跃带 = 多中心（玩家视角焦点 + 战斗热点）固定半径区块集，与相机缩放无关（D344）——缩放全图 Fine 集不变。</remarks>
    public bool IsActivelyCovered(Vector2Int mid)
    {
        // 无中心兜底（与 RenderActiveBands 同哲学：测试场景/无锚点时不"变傻"，全部视为被覆盖）
        if (_activeCenters.Count == 0) return true;
        return _activeBandSet.Contains(mid);
    }

    /// <summary>查询中区块是否为当前战斗热点（热度 &gt; 热点阈值；与 ComputeActiveCenters D77 同判据，供 SimMode 战斗锁 D333）。</summary>
    public bool HasActiveCombatHotspot(Vector2Int mid)
    {
        return _midStates.TryGetValue(mid, out var s)
            && s.threatHeat > Cfg(C => C.hotspotThreshold, 0.3f);
    }

    /// <summary>查询所在中区块的最近战斗热点（有效期内，供 NPCBrain 支援）。</summary>
    public bool TryGetCombatHotspot(Vector2 worldPos, float maxAge, out Vector2 hotspot)
    {
        hotspot = Vector2.zero;
        if (!WorldToMidChunk(worldPos, out var mid) || !TryGetState(mid, out var s)) return false;
        if (s.hotspotTime <= 0f || Time.time - s.hotspotTime > maxAge) return false;
        hotspot = s.combatHotspot;
        return true;
    }

    /// <summary>查询 center 半径内距离最近的**有效**战斗热点（跨中区块，FormationBrain 支援）。</summary>
    public bool TryGetNearestCombatHotspot(Vector2 center, float maxAge, float searchRadius, out Vector2 hotspot)
    {
        hotspot = Vector2.zero;
        if (_midStates.Count == 0) return false;
        bool found = false;
        float best = float.MaxValue;
        foreach (var s in _midStates.Values)
        {
            if (s.hotspotTime <= 0f || Time.time - s.hotspotTime > maxAge) continue;
            float d = Vector2.Distance(center, s.combatHotspot);
            if (d <= searchRadius && d < best) { best = d; hotspot = s.combatHotspot; found = true; }
        }
        return found;
    }

    // ===== Gizmos（步骤 6）=====

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        if (_config == null && !TryLoadFallback()) return;
        if (!_config.drawGizmos) return;
        if (GridSystem.Instance == null) return;

        foreach (var kv in _midStates)
        {
            var s = kv.Value;
            var center = MidChunkToWorldCenter(kv.Key);
            Vector3 sz = new Vector3(Ms(), Ms(), 0f) * 0.5f;
            var c = s.Level == LodLevel.Active ? Color.green
                  : s.Level == LodLevel.SemiActive ? Color.yellow
                  : Color.gray;
            DrawChunkRect(center, sz, c);
            if (s.threatHeat > 0f)
            {
                var heatCol = new Color(1f, 0.2f, 0.2f, Mathf.Clamp01(s.threatHeat) * 0.5f);
                DrawChunkRect(center, sz, heatCol);
            }
        }
        // 中心集星标
        Gizmos.color = Color.magenta;
        for (int i = 0; i < _activeCenters.Count; i++)
            Gizmos.DrawWireSphere(MidChunkToWorldCenter(_activeCenters[i]), Ms() * 0.6f);
    }

    private void DrawChunkRect(Vector2 center, Vector3 size, Color color)
    {
        Gizmos.color = color;
        Vector3 c = new Vector3(center.x, center.y, 0f);
        Gizmos.DrawWireCube(c, size);
    }

    // ===== 配置读取工具 =====

    private float Cfg(System.Func<LodConfig, float> get, float def)
        => _config != null ? get(_config) : def;
    private int Cfg(System.Func<LodConfig, int> get, int def)
        => _config != null ? get(_config) : def;

    private float HeatMax() => Cfg(C => C.heatMax, 1f);
    private float ActiveHz() => Cfg(C => C.activeHz, 10f);
    private float SemiHz() => Cfg(C => C.semiHz, 2f);
    private float DormantHz() => Cfg(C => C.dormantHz, 0.5f);
}