using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  QQQ.2 T8 / DR-21 - 漫游刺激源提供者（重写：动态锚点池驱动 + SafetyScore 门控）
//  详见 QQQ.2_NPC任务修正以及一些小问题.md §需求4.1/4.2
//  旧实现：_stimulus.Position = ctx.HomePoint 硬编码（所有空闲 NPC 聚城堡）。
//  新实现：
//   ① SafetyScore < wanderThreshold(0.4) → 不 Wander（不安全，交 Retreat/Safety）
//   ② 每 10-20s 随机间隔从 WanderAnchorPool 抽新锚点（近邻优先+随机抖动+最近N不重抽）
//   ③ 间隔内复用当前锚点（防每 tick 抖动）；抽失败回退 HomePoint 小半径
// ============================================================================

/// <summary>
/// WanderStimulus 提供者（§6.3 + QQQ.2 T8）。
/// 漫游中心 = 动态锚点池抽取的安全锚点（城堡/建筑/空地），取代硬编码 HomePoint。
/// </summary>
public class WanderStimulusProvider
{
    private readonly WanderStimulus _stimulus = new WanderStimulus();
    private readonly List<Vector2> _recent = new List<Vector2>();  // 本 NPC 最近用过的锚点（防重抽）

    private Vector2 _currentAnchor;
    private bool _hasAnchor;
    private float _lastRefreshTime = float.NegativeInfinity;
    private float _nextInterval = 12f;

    /// <summary>池化 WanderStimulus 实例（复用不 new）</summary>
    public WanderStimulus Stimulus => _stimulus;

    /// <summary>
    /// 自身种族 id（D468 同族结伙，HH.51 批C）：NPCBrain.Init 注入，聚集地评分同族分数项用。
    /// 不经 FactorContext（防 AI.Core 决策核扩字段触发 sim-sync 义务）。
    /// </summary>
    public int SelfRaceId { get; set; } = RaceIds.Human;

    /// <summary>同族流浪汉聚集站点缓冲（RefreshVagrantAnchor 刷新时重扫，防 GC）。</summary>
    private readonly List<Vector2> _sameRaceSites = new List<Vector2>();

    /// <summary>每 tick 更新漫游中心 + 强度，返回池化实例。</summary>
    public WanderStimulus GetOrUpdate(in FactorContext ctx)
    {
        _stimulus.Position = ctx.HomePoint;
        _stimulus.Intensity = 0f;

        // ① QQQ.4 T4：未招募流浪汉豁免 SafetyScore 门控——无论分数高低都不抽全局锚点池
        // （全局池含城堡中心+预设点，流浪汉抽到会被拉到城堡附近 →"不停往主城走"）。
        // 2_16 步骤10：改为"聚集地评估"——按评分加权抽候选点（营地周边 + 无主富地候选集），
        // 流民自然聚到资源/食物富集处（后续结营/立国的地理前兆）；不抽全局锚点池（否则朝主城走）。
        if (ctx.IsUnrecruitedVagrant)
        {
            float nowVagrant = ctx.CurrentTime;
            if (nowVagrant - _lastRefreshTime >= _nextInterval)
            {
                _lastRefreshTime = nowVagrant;
                var fc = Resources.Load<KingdomFoundingConfig>("Config/Kingdoms/KingdomFoundingConfig");
                // 间隔：优先走 SO（步骤10 占位 15s，防每帧扫描；对齐 10~20s 锚点刷新惯例），
                // 缺配置回退原随机间隔。
                _nextInterval = fc != null && fc.gatherCandidateRefreshSeconds > 0f
                    ? fc.gatherCandidateRefreshSeconds
                    : Random.Range(ctx.Config.anchorRefreshIntervalMin, ctx.Config.anchorRefreshIntervalMax);
                RefreshVagrantAnchor(ctx, fc);
            }
            _stimulus.Position = _hasAnchor ? Vector2XUnity.FromUnity(_currentAnchor) : ctx.HomePoint;
            _stimulus.Intensity = ctx.Config.wanderIntensity;
            return _stimulus;
        }

        // ② DR-21 门控：Score < wanderThreshold → 不 Wander（低分态交撤退/回城拉力）
        if (ctx.SafetyScore < ctx.Config.wanderThreshold) return _stimulus;

        // ③ 锚点刷新（10-20s 随机间隔，间隔内复用当前锚点防抖动）
        float now = ctx.CurrentTime;
        if (now - _lastRefreshTime >= _nextInterval)
        {
            _lastRefreshTime = now;
            _nextInterval = Random.Range(
                ctx.Config.anchorRefreshIntervalMin, ctx.Config.anchorRefreshIntervalMax);

            var selfPos = new Vector2(ctx.SelfPos.x, ctx.SelfPos.y);
            var pool = WanderAnchorPool.Instance;
            // QQQ.4 T7：工人闲逛不抽城堡锚点（防扎堆主城，散布在建筑/空地锚点）；居民可抽城堡
            if (pool.TryPickAnchor(selfPos, _recent, ctx.Config.anchorAvoidRecentCount,
                allowCastle: !ctx.IsWorker, out var anchor))
            {
                _currentAnchor = anchor;
                _hasAnchor = true;
                _recent.Add(anchor);
                if (_recent.Count > Mathf.Max(1, ctx.Config.anchorAvoidRecentCount))
                    _recent.RemoveAt(0);
            }
            else
            {
                _hasAnchor = false;  // 无锚点（未初始化/空池）→ 回退 HomePoint
            }
        }

        // ③ 写回池化刺激
        _stimulus.Position = _hasAnchor ? Vector2XUnity.FromUnity(_currentAnchor) : ctx.HomePoint;
        _stimulus.Intensity = ctx.Config.wanderIntensity;
        return _stimulus;
    }

    // ===== 2_16 步骤10：未招募流浪汉聚集地锚点（评分加权抽候选点）=====

    const int VagrantMaxCandidates = 16;   // 候选集规模占位可调（防候选过多扫分开销）

    /// <summary>
    /// 采样候选集并按评估分数加权抽 1 个聚集锚点（步骤10）。
    /// 候选 = 营地周边环（HomePoint=出生营地，恒非空保底） + 无主富地（资源/食物建筑点位）。
    /// 评估分数高 → 权重高 → 流民更常聚到潜在建国点。
    /// </summary>
    void RefreshVagrantAnchor(in FactorContext ctx, KingdomFoundingConfig fc)
    {
        float cs = Mathf.Max(1f, ctx.CellSize);

        var resourceSites = new List<Vector2>();
        var foodSites = new List<Vector2>();
        CollectResourceFoodSites(resourceSites, foodSites);

        var candidates = new List<Vector2>(VagrantMaxCandidates);
        // 营地周边环（HomePoint=出生营地；无论富贫恒有保底候选，老行为的大半径随机被此环覆盖）
        float ring = (fc != null ? Mathf.Clamp(fc.gatherInfluenceRadiusCells, 1f, 6f) : 3f) * cs;
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            candidates.Add(new Vector2(ctx.HomePoint.x, ctx.HomePoint.y) + new Vector2(dx * ring, dy * ring));
        }
        // 无主富地候选（资源/食物建筑点位；2_17 TerritorySystem 落地前恒无主，占位）
        for (int i = 0; i < resourceSites.Count && candidates.Count < VagrantMaxCandidates; i++)
            candidates.Add(resourceSites[i]);
        for (int i = 0; i < foodSites.Count && candidates.Count < VagrantMaxCandidates; i++)
            candidates.Add(foodSites[i]);

        // 评分加权抽点（eval 为纯函数，点位在此传入）
        // D468 同族结伙（HH.51 批C）：收集同族未招募流浪汉位置作聚集分输入（无国×同族→结伙偏好；
        // 异族站点不进集合——野性敌意下异族聚集地不可共处）
        CollectSameRaceVagrantSites();
        Vector2 fallback = new Vector2(ctx.HomePoint.x, ctx.HomePoint.y);
        _currentAnchor = fc != null
            ? VagrantGatherSiteEvaluator.PickWeighted(candidates, resourceSites, foodSites, fc, cs, _sameRaceSites)
            : fallback;
        _hasAnchor = fc != null;
    }

    /// <summary>收集同族未招募流浪汉位置（D468 同族结伙聚集分输入；开关权重 0 时跳过扫描省开销）。</summary>
    void CollectSameRaceVagrantSites()
    {
        _sameRaceSites.Clear();
        var fc = Resources.Load<KingdomFoundingConfig>("Config/Kingdoms/KingdomFoundingConfig");
        if (fc == null || fc.gatherSameRaceWeight <= 0f) return;
        if (UnitRegistry.Instance == null) return;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive) continue;
            if (u.EffectiveOccupation != Occupation.Vagrant || u.IsVagrantRecruited) continue;
            if (u.raceId != SelfRaceId) continue;   // 仅同族（异族聚集地不可共处 D468）
            _sameRaceSites.Add(u.GetPosition());
        }
    }

    /// <summary>从 BuildingRegistry 分拣资源点（废墟/采集点）与食物点（浆果/农田）世界坐标。</summary>
    void CollectResourceFoodSites(List<Vector2> resourceSites, List<Vector2> foodSites)
    {
        if (BuildingRegistry.Instance == null) return;
        foreach (var b in BuildingRegistry.Instance.All)
        {
            if (b == null || b.def == null || !b.IsActive) continue;
            bool food = IsFoodDef(b.def);
            if (food) foodSites.Add((Vector2)b.GetPosition());
            else if (IsResourceDef(b.def)) resourceSites.Add((Vector2)b.GetPosition());
        }
    }

    static bool IsFoodDef(BuildingDef def) =>
        def.outputResource == ResourceType.Food || def.outputResource == ResourceType.Meat;

    // 资源点（废墟/采集点）= 原始资源节点标记（矿洞/树林/农田）；def.producer 为 struct 不可判空，
    // 不能靠 producer.kind 判断（其默认枚举值恰是 ProduceKind.Resource），故只用 isResourceNode 可靠标记。
    static bool IsResourceDef(BuildingDef def) => def.isResourceNode;

    public void Reset()
    {
        _recent.Clear();
        _currentAnchor = Vector2.zero;
        _hasAnchor = false;
        _lastRefreshTime = float.NegativeInfinity;
        _nextInterval = 12f;
    }
}
