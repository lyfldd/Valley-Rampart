using UnityEngine;

/// <summary>
/// HH.221（**A①**，D685 裁 A）「世界资源点采集源」＝面向 AI 的 `ITaskSource` 包装
/// （照既有数据格采集源先例，禁另起炉灶；任务书 §一.A.2）。
///
/// <b>【HH.294 片 6-2（`D778`）双写收敛后】本类＝世界资源点采集的**统一数据寻址源**</b>：
/// 源只记「**格 ＋ 地表物**」（不再持有 `Building` 实体）——
///   · 改前两工厂：`ForTree`（数据格树）／`ForEntity`（`OreVein`／`WoodPile`／`StonePile` 一次性实体）；
///     实体退役（6-A/6-B）⇒ 合并为 <see cref="ForCell"/>（**四型同源**），<see cref="ForEntity"/> 退役。
///   · 采集耗时按 <see cref="RespawnConfig.GatherSecondsOf"/>（**逐型保改前原值**·`B-1` 裁「甲」）；
///     入包量由调用方给（AI 侧＝调度器口径／树＝树字段，逐调用点保原口径）。
///   · 完成回调统一走 `ResourceRespawnSystem.HandleCellGathered`（两链合并·6-D）。
///
/// 覆盖（改后）：数据格 `Tree` ＋ 一次性 `OreVein`／`WoodPile`／`StonePile`——全部「格表 ＋ 门」判定，
/// 不再依赖 `BuildingRegistry` 实体（`03` §8「资源格的存在性只由格表决定」）。
///
/// **唯一性**（`HH.294` 片 6-2 收尾·`D779` 残余 `S1` 清场后）：本类＝**全库唯一**世界资源点采集源 ——
/// AI 四型（`WorldGatherRegistry.Advertise`）与玩家四型（`ResourceRespawnSystem.ConfirmResourceGather`）皆走本类；
/// 改前并存的旧数据格树源类**已删**（改前玩家树链本就是零调用死码·`D776` 事实 A；
/// 其「广告后即失效」形态与调度器 `!IsValid ⇒ Abandon` 有已知竞态）。
/// 形态＝有效直至采集完成，重复广告由调度器 `HasAssignedTaskForSourceType` 独占去重拦下。
///
/// **与玩家侧同源等价**（任务书 §一.A.3）：同样产 `KingdomTaskType.Gather` ＋ `GatherTaskArgs`，
/// 资源同样先入**工人背包**再按归属国分流（`TaskScheduler.ExecuteCompletion` 既有链），
/// **不提供免费/瞬时入账通道**（禁绕过"劳动"）。
///
/// 归属（**per-命令绑 `kingdomId`**）：构造即绑国；`TaskScheduler.SourceKingdom` 认本类型
/// ⇒ 池隔离路由至**本国**工人（玩家侧传 0 ＝ 玩家池）。
/// 可达/归属由登记侧（`WorldGatherRegistry`）在立案前以**本国领土**过滤（禁全局共享）。
///
/// 生命周期：登记（＝决策核采集意图落地 ／ 玩家右键确认）→ **每 tick 广告**（去重交
///          `HasAssignedTaskForSourceType`）→ 派工 → 工人到点 Working(gatherSeconds)
///          → `OnGatherCompletion` 置失效 → 下 tick 被 `TaskScheduler` 清理。
/// </summary>
public class WorldGatherSource : ITaskSource
{
    /// <summary>本采集命令的归属国（per-命令绑定；`TaskScheduler.SourceKingdom` 读本字段做池隔离路由；玩家侧＝0）。</summary>
    public int KingdomId { get; private set; }

    /// <summary>目标格（数据格；四型同源）。</summary>
    public GridCoord Cell { get; private set; }

    /// <summary>目标格的地表物（有效性回读的唯一依据·B-2 后不再有实体态）。</summary>
    public FeatureType Feature { get; private set; }

    private readonly Vector2 _pos;
    private readonly ResourceType _resource;
    private readonly int _amount;
    private readonly float _gatherSeconds;
    private bool _active = true;

    private WorldGatherSource(int kingdomId, GridCoord cell, FeatureType feature, Vector2 pos,
        ResourceType resource, int amount, float gatherSeconds)
    {
        KingdomId = kingdomId;
        Cell = cell;
        Feature = feature;
        _pos = pos;
        _resource = resource;
        _amount = amount;
        _gatherSeconds = gatherSeconds;
    }

    /// <summary>⭐ <b>单一映射</b>：地表物 → 产出资源（`L-31` 同源·全库仅此一处）。
    /// 与改前口径逐位一致（取自退役的 `BuildingDef.outputResource`：`ore_vein=Ore(4)`／`stone_pile=Stone(1)`／
    /// `wood_pile=Wood(2)`；树＝Wood）。⛔ `Mine`（矿山锚点）**不在映射内** —— 锚点非采集对象。
    /// 返回 false ⇒ 该地表物不可采（调用方判否，不生源）。</summary>
    public static bool TryResourceOf(FeatureType feature, out ResourceType resource)
    {
        switch (feature)
        {
            case FeatureType.Tree:
            case FeatureType.WoodPile: resource = ResourceType.Wood; return true;
            case FeatureType.StonePile: resource = ResourceType.Stone; return true;
            case FeatureType.OreVein: resource = ResourceType.Ore; return true;
            default: resource = ResourceType.Gold; return false;
        }
    }

    /// <summary>该地表物是否「可采集资源格」（＝<see cref="TryResourceOf"/> 命中；含树·不含 `Mine`）。</summary>
    public static bool IsHarvestFeature(FeatureType feature) => TryResourceOf(feature, out _);

    /// <summary>数据格源（**四型统一**·HH.294 片 6-2）：耗时按 `feature` 取 `RespawnConfig`（逐型保原值），
    /// 入包量由调用方给（AI＝调度器口径／玩家三型＝调度器口径）。地表物不可采 ⇒ 返回 null（调用方零副作用）。</summary>
    public static WorldGatherSource ForCell(GridCoord cell, FeatureType feature, Vector2 pos,
        int kingdomId, RespawnConfig cfg, int gatherAmount)
    {
        if (!TryResourceOf(feature, out var rt)) return null;
        return new WorldGatherSource(kingdomId, cell, feature, pos, rt, gatherAmount,
            cfg != null ? cfg.GatherSecondsOf(feature) : 0f);
    }

    /// <summary>有效性＝未完成 **且** 目标格地表物仍在（同型）。兼作跨局/跨轮兜底：
    /// 世界已清场 ⇒ 目标格判否 ⇒ 陈旧源自动失效（不依赖显式清场钩子）。</summary>
    public bool IsValid => _active && PointStillThere();

    public Vector2 SourcePos => _pos;

    public void OnRegister() { }
    public void OnUnregister() { }

    /// <summary>广告采集任务（`destType=Treasury` 与玩家侧同口径）。每 tick 可重复调用：
    /// 同源同类型独占去重由 `TaskScheduler.HasAssignedTaskForSourceType` 承担（与 `Building` 同模式）。</summary>
    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        if (!IsValid) return false;
        task = new KingdomTask(KingdomTaskType.Gather, this);
        task.destType = KingdomDestType.Treasury;
        task.args = new GatherTaskArgs
        {
            resourceType = _resource,
            amount = _amount,
            gatherSeconds = _gatherSeconds
        };
        return true;
    }

    /// <summary>
    /// 采集完成回调（由 `TaskScheduler.ExecuteCompletion` 在入包/台账后调用）。职责＝世界侧善后（**两链合一**）：
    /// 格翻 Plain ＋ 守卫失去（门内）＋ 游荡锚点登记 ＋ 池子减 1 点（`ResourceRespawnSystem.HandleCellGathered`）。
    /// </summary>
    public void OnGatherCompletion()
    {
        if (!_active) return;
        _active = false;
        if (ResourceRespawnSystem.HasInstance)
            ResourceRespawnSystem.Instance.HandleCellGathered(Cell);
    }

    /// <summary>目标格是否仍可采（格表唯一功能源：地表物仍为登记时的同型）。</summary>
    private bool PointStillThere() => MapGate.GetFeatureAt(Cell) == Feature;
}