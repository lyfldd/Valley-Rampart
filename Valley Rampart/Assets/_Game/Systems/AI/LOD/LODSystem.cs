using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1_LOD 性能架构 - LODSystem 区块思考分区管理器
//  详见 3.0.1_LOD性能架构.md §1 / §3 / §六.P0
//  职责：region LOD 状态持有 / 活跃带判定（君主中心先行）/ 热度累积衰减 /
//        升降级防抖 / 事件覆盖（受击·敌人进格升整 region）/ 扩散（P1）
//  架构要点：判定成本 O(region 数) 而非 O(NPC 数)；升级即时、降级 30s 逐级
// ============================================================================

/// <summary>
/// LODSystem（§1.1，Singleton）。
/// 每个 region 持 LOD 状态，NPCBrain 从所在 region 读 Think 频率。
/// P0 仅君主中心（RulerController.Instance.MonarchUnit）驱动活跃带；
/// 军队锚点注册表留接口（§六.P0.2，FormationController 落地后注册）。
/// </summary>
public class LODSystem : Singleton<LODSystem>
{
    [Tooltip("全局调参 SO（自动加载 Resources/Config/AttentionTuningConfig）")]
    private AttentionTuningConfig _config;

    // region -> LOD 状态（索引连续，region 数 = MapCellCount / regionCellCount）
    private readonly List<RegionLodState> _regions = new List<RegionLodState>();

    // 中区块 -> 热度状态（3.0.1_5 §五：midRegionCellCount 小区块编组粒度，热度/热点按中区块聚合，热点跨编队可见）
    private readonly List<MidRegionHeat> _midHeats = new List<MidRegionHeat>();

    // 军队锚点注册表（§六.P0.2 留接口：P0 无注册方时仅君主中心运行）
    private readonly List<Transform> _armyCenters = new List<Transform>();

    /// <summary>region 数（按当前地图初始化）</summary>
    public int RegionCount => _regions.Count;

    protected override void Awake()
    {
        base.Awake();
        _config = Resources.Load<AttentionTuningConfig>("Config/AttentionTuningConfig");
        if (_config == null)
            Debug.LogError("[LODSystem] 未找到 AttentionTuningConfig！");
        EventBus.Subscribe<UnitDamagedEvent>(OnUnitDamaged);
        EventBus.Subscribe<EnemyEnteredRegionEvent>(OnEnemyEnteredRegion);
        EventBus.Subscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        base.OnDestroy();
        EventBus.Unsubscribe<UnitDamagedEvent>(OnUnitDamaged);
        EventBus.Unsubscribe<EnemyEnteredRegionEvent>(OnEnemyEnteredRegion);
        EventBus.Unsubscribe<MapGeneratedEvent>(OnMapGenerated);
    }

    /// <summary>地图生成后按 region 数初始化（GridSystem.PopulateFromMap 之后由事件触发）。</summary>
    private void OnMapGenerated(MapGeneratedEvent evt)
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return;
        int mapCellCount = GridSystem.Instance.MapCellCount;
        if (mapCellCount <= 0) return;
        InitRegions(mapCellCount / GridSystem.Instance.Config.regionCellCount);
    }

    // ===== 初始化 =====

    /// <summary>
    /// 按当前地图初始化 region + 中区块数组（世界/地图生成后调，GridSystem.PopulateFromMap 之后）。
    /// </summary>
    public void InitRegions(int regionCount)
    {
        if (regionCount <= 0) return;
        _regions.Clear();
        for (int i = 0; i < regionCount; i++)
            _regions.Add(new RegionLodState(i));

        // 中区块 = regionCount × midRegionCellCount（每 region 内 4 个中区块连续编号）
        int mrc = GetMidCellCount();
        _midHeats.Clear();
        for (int i = 0; i < regionCount * mrc; i++)
            _midHeats.Add(new MidRegionHeat(i));
        Debug.Log($"[LODSystem] 初始化 {regionCount} 个 region（{_midHeats.Count} 个中区块），初始全休眠。");
    }

    /// <summary>中区块数 = region 数 × 每 region 中区块数</summary>
    public int MidRegionCount => _midHeats.Count;

    private int GetMidCellCount()
    {
        if (GridSystem.Instance != null && GridSystem.Instance.Config != null
            && GridSystem.Instance.Config.midRegionCellCount > 0)
            return GridSystem.Instance.Config.midRegionCellCount;
        return 4;
    }

    // ===== 军队锚点注册表（§六.P0.2 留接口）=====

    /// <summary>注册军队锚点（FormationController 编队落地后调，P0 空实现由君主中心兜底）</summary>
    public void RegisterArmyCenter(Transform anchor)
    {
        if (anchor != null && !_armyCenters.Contains(anchor))
            _armyCenters.Add(anchor);
    }

    /// <summary>注销军队锚点（将军阵亡时调）</summary>
    public void UnregisterArmyCenter(Transform anchor)
    {
        _armyCenters.Remove(anchor);
    }

    // ===== 每帧更新：热度衰减 + 活跃带重算 + 降级防抖 =====

    private void Update()
    {
        // 懒初始化兜底（MapGeneratedEvent 未触发时：GridSystem 已填充即按当前地图初始化）
        if (_regions.Count == 0 && GridSystem.Instance != null && GridSystem.Instance.Config != null)
        {
            int mapCellCount = GridSystem.Instance.MapCellCount;
            if (mapCellCount > 0)
                InitRegions(mapCellCount / GridSystem.Instance.Config.regionCellCount);
        }

        if (_regions.Count == 0 || _config == null) return;

        // 1. 中区块热度衰减 + region 无事件计时累积（先衰减再判定降级）
        for (int i = 0; i < _midHeats.Count; i++)
        {
            var m = _midHeats[i];
            if (m.ThreatHeat > 0f)
                m.ThreatHeat = Mathf.Max(0f, m.ThreatHeat - _config.heatDecayRate * Time.deltaTime);
        }
        for (int i = 0; i < _regions.Count; i++)
            _regions[i].IdleTimer += Time.deltaTime;

        // 2. 活跃带重算（升级即时，基于君主/军队中心位置）
        ApplyActiveBands();

        // 3. 降级防抖（§1.4：热度归零且 idleTimer ≥ 30s → 逐级降，不跳级）
        ApplyDowngrade();
    }

    /// <summary>活跃带判定（§1.2）：每个中心点亮 [中心±1]=活跃 [中心±2]=半活跃，取并集。</summary>
    private void ApplyActiveBands()
    {
        if (_regions.Count == 0) return;

        // P0 仅君主中心 + 军队锚点（注册表留接口）
        var monarch = RulerController.Instance != null ? RulerController.Instance.MonarchUnit : null;
        int centerCount = 0;
        if (monarch != null)
        {
            ApplyCenterBand(GetRegionOf(monarch.transform.position));
            centerCount++;
        }

        for (int i = 0; i < _armyCenters.Count; i++)
        {
            var c = _armyCenters[i];
            if (c != null)
            {
                ApplyCenterBand(GetRegionOf(c.position));
                centerCount++;
            }
        }

        // 兜底：无君主且无军队锚点（测试场景/战斗验证），全部保持活跃——
        // 否则将军死后 region 无中心 30s 降级休眠，NPC 反应迟钝"变傻"。
        if (centerCount == 0)
        {
            for (int i = 0; i < _regions.Count; i++)
            {
                if (_regions[i].Level != LodLevel.Active)
                {
                    _regions[i].Level = LodLevel.Active;
                    _regions[i].IdleTimer = 0f;
                }
            }
        }
    }

    /// <summary>点亮以 centerRegion 为中心的活跃带（升级即时；带内 region 无条件重置 idleTimer——活跃带覆盖=持续存在，永不因 idleTimer 降级）。</summary>
    private void ApplyCenterBand(int centerRegion)
    {
        if (centerRegion < 0 || centerRegion >= _regions.Count) return;
        int activeR = _config.lodActiveRadius;
        int semiR = _config.lodSemiRadius;

        for (int d = -semiR; d <= semiR; d++)
        {
            int idx = centerRegion + d;
            if (idx < 0 || idx >= _regions.Count) continue;
            var r = _regions[idx];
            LodLevel target = Mathf.Abs(d) <= activeR ? LodLevel.Active : LodLevel.SemiActive;
            if ((int)r.Level > (int)target) continue; // 只升不降（降级走防抖）
            if (r.Level != target)
            {
                r.Level = target;
            }
            // 无条件重置无事件计时：君主/军队在带内 = 持续在场，热度归零也不该降级
            r.IdleTimer = 0f;
        }
    }

    /// <summary>降级防抖（§1.4）：region 下全部中区块热度归零 + idleTimer ≥ 30s → 降一级，不跳级。</summary>
    private void ApplyDowngrade()
    {
        float idleLimit = _config.lodDowngradeIdleTime;
        int mrc = GetMidCellCount();
        for (int i = 0; i < _regions.Count; i++)
        {
            var r = _regions[i];
            if (r.Level == LodLevel.Sleeping) continue;
            if (RegionTotalHeat(i, mrc) > 0f || r.IdleTimer < idleLimit) continue;
            // 逐级降：Active -> SemiActive -> Sleeping
            r.Level = r.Level == LodLevel.Active ? LodLevel.SemiActive : LodLevel.Sleeping;
            r.IdleTimer = 0f;
            Debug.Log($"[LODSystem] region {r.RegionIndex} 降级 -> {r.Level}（热度 0 + {idleLimit}s 无事件）");
        }
    }

    /// <summary>region 内全部中区块热度之和（降级判定用：任一中区块有热度即不降级）。</summary>
    private float RegionTotalHeat(int regionIdx, int mrc)
    {
        int baseIdx = regionIdx * mrc;
        float sum = 0f;
        for (int i = 0; i < mrc; i++)
        {
            int idx = baseIdx + i;
            if (idx >= 0 && idx < _midHeats.Count)
                sum += _midHeats[idx].ThreatHeat;
        }
        return sum;
    }

    // ===== 事件覆盖（§1.3 升级不走路径 tick）=====

    /// <summary>受击事件：被打 NPC 所在**中区块**即时升活跃 + 热度累积 + 记录战斗热点（§3.1 +0.4 / §3.2 危险传开，热点跨编队可见）。</summary>
    private void OnUnitDamaged(UnitDamagedEvent evt)
    {
        if (_midHeats.Count == 0 || _config == null) return;
        var uo = evt.Unit as UnityEngine.Object;
        if (uo == null) return;
        var unit = evt.Unit as UnitController;
        if (unit == null) return;
        int midIdx = GetMidRegionOf(unit.transform.position);
        if (midIdx < 0 || midIdx >= _midHeats.Count) return;
        var m = _midHeats[midIdx];
        m.ThreatHeat = Mathf.Min(1f, m.ThreatHeat + _config.heatHitGain);
        // 战斗热点：记录受击位置（供同中区块感知范围外 NPC 朝支援方向移动，§3.1 第二层位置载体）
        m.CombatHotspot = unit.transform.position;
        m.HotspotTime = Time.time;
        UpgradeImmediate(midIdx, LodLevel.Active);
    }

    /// <summary>敌人进格事件：敌人进入 region 即升活跃 + 热度累积（§3.1 +0.2，按敌人所在中区块聚合）。</summary>
    private void OnEnemyEnteredRegion(EnemyEnteredRegionEvent evt)
    {
        if (_midHeats.Count == 0 || _config == null) return;
        int midIdx = -1;
        if (evt.Enemy != null)
            midIdx = GetMidRegionOf(evt.Enemy.transform.position);
        if (midIdx < 0 || midIdx >= _midHeats.Count) return;
        var m = _midHeats[midIdx];
        m.ThreatHeat = Mathf.Min(1f, m.ThreatHeat + _config.heatEnemyEnterGain);
        UpgradeImmediate(midIdx, LodLevel.Active);
    }

    /// <summary>威胁类事件升级（即时，重置 idleTimer 防误降级）。事件带中区块索引（热度粒度=中区块）。</summary>
    private void UpgradeImmediate(int midIdx, LodLevel level)
    {
        // LOD 三区仍在 region 粒度：中区块 → 所属 region
        int regionIdx = midIdx / GetMidCellCount();
        if (regionIdx < 0 || regionIdx >= _regions.Count) return;
        var r = _regions[regionIdx];
        if (r.Level != level)
        {
            r.Level = level;
            r.IdleTimer = 0f;
        }
        // 发布热度变化事件（§3.5 协作层事件源，无订阅者守卫由 EventBus.HasSubscribers 判定；RegionIndex 参数=中区块索引）
        if (EventBus.HasSubscribers<RegionHeatChangedEvent>())
            EventBus.Publish(new RegionHeatChangedEvent(midIdx, _midHeats[midIdx].ThreatHeat, r.Level));
    }

    // ===== 对外查询（NPCBrain 调）=====

    /// <summary>查询所在 region 的 LOD 等级。</summary>
    public LodLevel GetLevelAt(Vector2 worldPos)
    {
        int idx = GetRegionOf(worldPos);
        if (idx < 0 || idx >= _regions.Count) return LodLevel.Active; // 未初始化兜底全活跃（行为不变）
        return _regions[idx].Level;
    }

    /// <summary>查询所在中区块的威胁热度（NPCBrain 喂 heatFactor，3.0.1_5 §五中区块粒度）。</summary>
    public float GetHeatAt(Vector2 worldPos)
    {
        int idx = GetMidRegionOf(worldPos);
        if (idx < 0 || idx >= _midHeats.Count) return 0f;
        return _midHeats[idx].ThreatHeat;
    }

    /// <summary>
    /// 查询所在中区块的最近战斗热点（§3.1 第二层"危险传开"的位置载体，中区块粒度热点跨编队可见）。
    /// 热点有效期：maxAge 秒内的热点有效；无热点/过期返回 false。
    /// 供 NPCBrain 在"感知范围内无敌人但中区块有战斗"时朝热点移动支援。
    /// </summary>
    public bool TryGetCombatHotspot(Vector2 worldPos, float maxAge, out Vector2 hotspot)
    {
        hotspot = Vector2.zero;
        int idx = GetMidRegionOf(worldPos);
        if (idx < 0 || idx >= _midHeats.Count) return false;
        var m = _midHeats[idx];
        if (m.HotspotTime <= 0f || Time.time - m.HotspotTime > maxAge) return false;
        hotspot = m.CombatHotspot;
        return true;
    }

    /// <summary>
    /// 查询中心点 searchRadius 内**最近**的有效战斗热点（3.0.1_5 §四 支援机制数据源）。
    /// 跨中区块扫描（编队 B 支援编队 A：A 受击在中区块 X，B 在中区块 Y 也能读到热点）。
    /// FormationBrain 秒级调用，O(中区块数) 可接受。
    /// </summary>
    public bool TryGetNearestCombatHotspot(Vector2 center, float maxAge, float searchRadius, out Vector2 hotspot)
    {
        hotspot = Vector2.zero;
        if (_midHeats.Count == 0) return false;
        bool found = false;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _midHeats.Count; i++)
        {
            var m = _midHeats[i];
            if (m.HotspotTime <= 0f || Time.time - m.HotspotTime > maxAge) continue;
            float d = Vector2.Distance(center, m.CombatHotspot);
            if (d <= searchRadius && d < bestDist)
            {
                bestDist = d;
                hotspot = m.CombatHotspot;
                found = true;
            }
        }
        return found;
    }

    /// <summary>世界坐标 → region 索引（复用 GridSystem 换算）。</summary>
    public int GetRegionOf(Vector2 worldPos)
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return -1;
        var coord = GridSystem.Instance.WorldToCoord(worldPos);
        return GridSystem.Instance.CellToRegionIndex(coord.x);
    }

    /// <summary>世界坐标 → 中区块索引（3.0.1_5 §五：midRegionCellCount 小区块编组，热度聚合粒度）。</summary>
    public int GetMidRegionOf(Vector2 worldPos)
    {
        if (GridSystem.Instance == null || GridSystem.Instance.Config == null) return -1;
        var coord = GridSystem.Instance.WorldToCoord(worldPos);
        return GridSystem.Instance.CellToMidRegionIndex(coord.x);
    }
}
