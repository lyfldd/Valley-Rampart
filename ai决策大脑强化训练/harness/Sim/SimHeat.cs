// ============================================================================
//  M2 Headless 模拟器 - SimHeat 中区块热度表（复刻 LODSystem 热度部分）
//  04_模拟器规格.md §一：SimHeat：中区块热度表（复刻 LOD 热度累积/衰减/扩散——FormationBrain 的输入）。
//  真身对照 LODSystem.cs：
//    - 中区块粒度：midIdx = cellX / midRegionCellCount（GridConfig.midRegionCellCount=4）
//    - 受击累积：ThreatHeat = min(1, ThreatHeat + heatHitGain=0.4) + 记录战斗热点（LODSystem.cs:237-252）
//    - 敌人进格累积：+heatEnemyEnterGain=0.2（LODSystem.cs:255-265）
//    - 衰减：ThreatHeat = max(0, ThreatHeat - heatDecayRate=0.05 × dt)（LODSystem.cs:125-130）
//      —— 决策点 6：sim 按 0.1s tick 衰减（heat -= 0.05×0.1），tick 粒度近似，报告披露。
//    - 扩散（heatSpreadThreshold/heatSpreadRatio）：LODSystem 真身未实装（P1 项），SimHeat 与真身一致留空。
//    - LOD 三区/活跃带/降级（LODSystem §1）：sim 全活跃 10Hz（04 §四 LOD 降频差异），不实现。
// ============================================================================

using System.Collections.Generic;

/// <summary>中区块热度状态（对应 LODSystem.MidRegionHeat）。</summary>
public sealed class SimMidHeat
{
    public int Index;
    public float ThreatHeat;
    public Vector2X CombatHotspot;   // 战斗热点位置（受击位置，供"危险传开"）
    public float HotspotTime;        // 热点记录时间（<=0=无热点）
}

/// <summary>
/// 中区块热度表（FormationBrain 的输入 + NPCBrain RegionHeat 因子）。
/// 热度聚合粒度 = 中区块（midRegionCellCount 小区块编组），热点跨编队可见（3.0.1_5 §五）。
/// </summary>
public sealed class SimHeat
{
    private readonly float _cellSize;
    private readonly int _midRegionCellCount;
    private readonly float _heatHitGain;
    private readonly float _heatEnemyEnterGain;
    private readonly float _heatDecayRate;

    private readonly List<SimMidHeat> _midHeats = new List<SimMidHeat>();

    public SimHeat(float cellSize, int midRegionCellCount, float heatHitGain,
                   float heatEnemyEnterGain, float heatDecayRate)
    {
        _cellSize = cellSize > 0f ? cellSize : 2.26f;
        _midRegionCellCount = midRegionCellCount > 0 ? midRegionCellCount : 4;
        _heatHitGain = heatHitGain;
        _heatEnemyEnterGain = heatEnemyEnterGain;
        _heatDecayRate = heatDecayRate;
    }

    /// <summary>按场景覆盖的 x 范围初始化中区块数组（负半轴~正半轴），防索引越界。</summary>
    public void InitRange(float minWorldX, float maxWorldX)
    {
        int minMid = WorldToMidRegion(minWorldX);
        int maxMid = WorldToMidRegion(maxWorldX);
        _midHeats.Clear();
        for (int i = minMid; i <= maxMid; i++)
            _midHeats.Add(new SimMidHeat { Index = i });
    }

    /// <summary>世界坐标 x -> 中区块索引（GridSystem.CellToMidRegionIndex）。</summary>
    public int WorldToMidRegion(float worldX)
    {
        int cellX = (int)System.Math.Floor(worldX / _cellSize);
        return cellX / _midRegionCellCount;
    }

    /// <summary>受击事件：热度累积 + 记录战斗热点（复刻 LODSystem.OnUnitDamaged）。</summary>
    public void AddHit(float worldX, float currentTime)
    {
        var m = GetMidHeat(worldX);
        if (m == null) return;
        m.ThreatHeat = MathfX.Min(1f, m.ThreatHeat + _heatHitGain);
        m.CombatHotspot = new Vector2X(worldX, 0f);
        m.HotspotTime = currentTime;
    }

    /// <summary>敌人进格事件：热度累积（复刻 LODSystem.OnEnemyEnteredRegion）。</summary>
    public void AddEnemyEnter(float worldX)
    {
        var m = GetMidHeat(worldX);
        if (m == null) return;
        m.ThreatHeat = MathfX.Min(1f, m.ThreatHeat + _heatEnemyEnterGain);
    }

    /// <summary>每 tick 热度衰减（决策点 6：dt=tick 粒度 0.1s，heat -= 0.05×0.1）。</summary>
    public void Decay(float dt)
    {
        for (int i = 0; i < _midHeats.Count; i++)
        {
            var m = _midHeats[i];
            if (m.ThreatHeat > 0f)
                m.ThreatHeat = MathfX.Max(0f, m.ThreatHeat - _heatDecayRate * dt);
        }
    }

    /// <summary>查询所在中区块的威胁热度（LODSystem.GetHeatAt）。</summary>
    public float GetHeatAt(float worldX)
    {
        var m = GetMidHeat(worldX);
        return m != null ? m.ThreatHeat : 0f;
    }

    /// <summary>
    /// 查询所在中区块的最近战斗热点（LODSystem.TryGetCombatHotspot）。
    /// maxAge 秒内热点有效；无热点/过期返回 false。
    /// </summary>
    public bool TryGetCombatHotspot(float worldX, float maxAge, float currentTime, out Vector2X hotspot)
    {
        hotspot = Vector2X.zero;
        var m = GetMidHeat(worldX);
        if (m == null) return false;
        if (m.HotspotTime <= 0f || currentTime - m.HotspotTime > maxAge) return false;
        hotspot = m.CombatHotspot;
        return true;
    }

    /// <summary>
    /// 查询中心点 searchRadius 内最近的有效战斗热点（LODSystem.TryGetNearestCombatHotspot）。
    /// 跨中区块扫描（FormationBrain v1 支援机制数据源；v0 DecideAutoIntent 空壳暂未消费）。
    /// </summary>
    public bool TryGetNearestCombatHotspot(Vector2X center, float maxAge, float searchRadius,
                                           float currentTime, out Vector2X hotspot)
    {
        hotspot = Vector2X.zero;
        bool found = false;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _midHeats.Count; i++)
        {
            var m = _midHeats[i];
            if (m.HotspotTime <= 0f || currentTime - m.HotspotTime > maxAge) continue;
            float d = Vector2X.Distance(center, m.CombatHotspot);
            if (d <= searchRadius && d < bestDist)
            {
                bestDist = d;
                hotspot = m.CombatHotspot;
                found = true;
            }
        }
        return found;
    }

    private SimMidHeat GetMidHeat(float worldX)
    {
        int idx = WorldToMidRegion(worldX);
        for (int i = 0; i < _midHeats.Count; i++)
            if (_midHeats[i].Index == idx) return _midHeats[i];
        return null;
    }
}
