using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  QQQ.2 T8 / DR-21 - 动态多锚点池 WanderAnchorPool（取代硬编码 HomePoint 单锚点）
//  详见 QQQ.2_NPC任务修正以及一些小问题.md §需求4.2
//  锚点来源：
//    ├── 城堡中心 + 周边预设点（开局生成，15s 随城堡刷新自愈跨岛/新局）
//    ├── 已建成建筑附近（BuildingRegistry 扫描，5s 重建——摧毁自动消失）
//    ├── 资源点采集后空地（Building.OnGatherCompleted 显式注册，持久）
//    └── 道路节点/军队驻扎（地图生成/编队后可选接入，本版占位跳过）
//  查询：
//    TryPickAnchor  —— WanderStimulusProvider 闲逛抽点（近邻优先+随机抖动+最近N不重抽）
//    PickSafeAnchor —— RetreatToSafeAnchor 撤退目标（最近安全锚点，兜底城堡中心）
// ============================================================================

/// <summary>王国动态闲逛锚点池（单例，NPCBrain/WanderStimulusProvider 查询）。</summary>
public class WanderAnchorPool : Singleton<WanderAnchorPool>
{
    private readonly List<Vector2> _castleAnchors = new List<Vector2>();  // 城堡中心+预设点（15s 刷新）
    private readonly List<Vector2> _buildingAnchors = new List<Vector2>(); // 活跃建筑（5s 重建）
    private readonly List<Vector2> _freeSpots = new List<Vector2>();       // 采集空地（持久）
    private readonly List<Vector2> _anchors = new List<Vector2>();         // 合并缓存（查询用）
    private readonly List<byte> _anchorTypes = new List<byte>();           // 与 _anchors 并行：0=城堡 1=建筑 2=空地
    private readonly HashSet<int> _coordKeys = new HashSet<int>();     // 去重（按 x 格）
    private readonly List<int> _scratch = new List<int>();             // 复用缓冲（零 GC）
    private readonly List<int> _result = new List<int>();

    private bool _initialized;
    private float _buildingRebuildTimer;
    private float _extrasRefreshTimer;
    private const float BuildingRebuildInterval = 5f;
    private const float ExtrasRefreshInterval = 15f;
    private const int MaxFreeSpots = 48;    // 采集空地锚点上限（防无限增长）

    private Vector2 _castleAnchor = Vector2.zero;

    private void Update()
    {
        _buildingRebuildTimer += Time.deltaTime;
        _extrasRefreshTimer += Time.deltaTime;
        bool buildingDue = _buildingRebuildTimer >= BuildingRebuildInterval;
        bool extrasDue = _extrasRefreshTimer >= ExtrasRefreshInterval;
        if (buildingDue) _buildingRebuildTimer = 0f;
        if (extrasDue) _extrasRefreshTimer = 0f;
        if (!buildingDue && !extrasDue) return;
        Rebuild(buildingDue, extrasDue);
    }

    /// <summary>重建锚点（building=true 重扫建筑；extras=true 刷新城堡+预设）。</summary>
    void Rebuild(bool building, bool extras)
    {
        if (building)
        {
            _buildingAnchors.Clear();
            if (BuildingRegistry.Instance != null)
            {
                var all = BuildingRegistry.Instance.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var b = all[i];
                    if (b == null || b.def == null || !b.IsActive) continue;
                    _buildingAnchors.Add(b.transform.position);
                }
            }
        }
        if (extras) RefreshCastleExtras();
        Compile();
    }

    /// <summary>城堡中心 + 周边预设点（±2/4/6 格），随当前城堡位置刷新（跨岛/新局自愈）。</summary>
    void RefreshCastleExtras()
    {
        _castleAnchors.Clear();
        _castleAnchor = ResolveCastleAnchor();
        if (_castleAnchor == Vector2.zero) return;  // WorldManager 未就绪：仅保留后续注册的空地锚点，下轮重试
        _castleAnchors.Add(_castleAnchor);
        float cs = GetCellSize();
        float[] offsets = { 2f, 4f, 6f };
        for (int i = 0; i < offsets.Length; i++)
        {
            _castleAnchors.Add(_castleAnchor + new Vector2(offsets[i] * cs, 0f));
            _castleAnchors.Add(_castleAnchor - new Vector2(offsets[i] * cs, 0f));
            // 2_7 步骤1：去 1D y 固定，补 y 方向点（2D 环绕城堡）
            _castleAnchors.Add(_castleAnchor + new Vector2(0f, offsets[i] * cs));
            _castleAnchors.Add(_castleAnchor - new Vector2(0f, offsets[i] * cs));
        }
    }

    Vector2 ResolveCastleAnchor()
    {
        if (WorldManager.Instance != null)
        {
            var p = WorldManager.Instance.GetKingdomAnchorWorld();
            if (p != Vector2.zero) return p;
        }
        return Vector2.zero;
    }

    void Compile()
    {
        _anchors.Clear();
        _coordKeys.Clear();
        _anchorTypes.Clear();
        AddUnique(_castleAnchors, 0);
        AddUnique(_buildingAnchors, 1);
        AddUnique(_freeSpots, 2);
    }

    void AddUnique(List<Vector2> list, byte type)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (_coordKeys.Add(RoundedKey(list[i])))
            {
                _anchors.Add(list[i]);
                _anchorTypes.Add(type);
            }
        }
    }

    static int RoundedKey(Vector2 pos)
    {
        float cs = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;
        // 2_7 步骤1：去 1D 按 x 去重，改 2D 联合 key（x+y 哈希去重）
        int hx = Mathf.RoundToInt(pos.x / cs);
        int hy = Mathf.RoundToInt(pos.y / cs);
        unchecked { return hx * 397 ^ hy; }
    }

    static float GetCellSize()
    {
        return GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;
    }

    /// <summary>资源点采集后空地锚点（持久；Building.OnGatherCompleted 调）。</summary>
    public void RegisterFreeSpot(Vector2 worldPos)
    {
        if (_freeSpots.Count >= MaxFreeSpots) return;
        _freeSpots.Add(worldPos);
        Compile();
    }

    /// <summary>当前锚点总数（调试用）。</summary>
    public int Count => _anchors.Count;

    /// <summary>城堡中心（回退兜底目标）。</summary>
    public Vector2 CastleAnchor => _castleAnchor;

    void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        RefreshCastleExtras();
        Rebuild(building: true, extras: false);
    }

    /// <summary>
    /// 抽闲逛锚点（WanderStimulusProvider 用，DR-21 流程）：
    /// 近邻优先 + 随机抖动（安全系数+距离权重）→ 候选取最近 K 个 → 排除最近 avoidCount 个已用锚点 → 随机抽 1。
    /// QQQ.4 T6/T7：allowCastle=false 时排除城堡中心+预设点（工人闲逛不扎堆主城，居民可抽城堡）。
    /// 返回 false 时调用方回退 HomePoint（无锚点/未初始化）。
    /// </summary>
    public bool TryPickAnchor(Vector2 selfPos, List<Vector2> recent, int avoidCount, bool allowCastle, out Vector2 anchor)
    {
        anchor = Vector2.zero;
        EnsureInitialized();
        if (_anchors.Count == 0) return false;

        // 排除最近 avoidCount 个已用锚点（位置 1 世界单位内视为同一锚点）+ 职业不允的城堡锚点
        _scratch.Clear();
        for (int i = 0; i < _anchors.Count; i++)
        {
            if (!allowCastle && _anchorTypes[i] == 0) continue;   // QQQ.4：工人不抽城堡锚点
            bool used = false;
            for (int r = 0; r < recent.Count && r < avoidCount; r++)
            {
                if ((recent[r] - _anchors[i]).sqrMagnitude < 1f) { used = true; break; }
            }
            if (!used) _scratch.Add(i);
        }
        if (_scratch.Count == 0)  // 全部用过（锚点太少）→ 放宽重抽
        {
            _scratch.Clear();
            for (int i = 0; i < _anchors.Count; i++) _scratch.Add(i);
        }

        // 取最近 K 个（含距离+随机抖动，避免全王国 NPC 挤同一片）
        int k = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(_anchors.Count)), 3, 8);
        _result.Clear();
        float jitter = GetCellSize() * 4f;
        for (int s = 0; s < k && _scratch.Count > 0; s++)
        {
            int best = 0;
            float bestScore = (selfPos - _anchors[_scratch[0]]).sqrMagnitude + Random.Range(0f, jitter);
            for (int i = 1; i < _scratch.Count; i++)
            {
                float score = (selfPos - _anchors[_scratch[i]]).sqrMagnitude + Random.Range(0f, jitter);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            _result.Add(_scratch[best]);
            _scratch.RemoveAt(best);
        }

        anchor = _anchors[_result[Random.Range(0, _result.Count)]];
        return true;
    }

    /// <summary>最近安全锚点（RetreatToSafeAnchor 撤退目标；无锚点回退城堡中心）。</summary>
    public Vector2 PickSafeAnchor(Vector2 selfPos)
    {
        EnsureInitialized();
        if (_anchors.Count == 0) return _castleAnchor;
        Vector2 best = _anchors[0];
        float bestDist = (selfPos - best).sqrMagnitude;
        for (int i = 1; i < _anchors.Count; i++)
        {
            float d = (selfPos - _anchors[i]).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = _anchors[i]; }
        }
        return best;
    }
}
