using UnityEngine;

/// <summary>
/// HH.221（**A①**，D685 裁 A）「世界资源点采集源」＝面向 AI 的 `ITaskSource` 包装
/// （照 `TreeGatherSource` 先例，禁另起炉灶；任务书 §一.A.2）。
///
/// 覆盖两类世界资源点（任务书 §一.A.1）：
///   · 数据格 tree（`FeatureType.Tree`，A+ 数据化不建实体）→ 完成走
///     `ResourceRespawnSystem.HandleTreeGathered`（与玩家砍树同链：格翻 Plain＋刷新＋守卫失去＋记重生）；
///   · 一次性实体 stone_pile／ore_vein／wood_pile（`Building.isConsumable=1`）→ 完成走
///     `Building.OnGatherCompleted()`（与玩家采集同链：网格释放＋注册表移除＋守卫失去＋实体重生记账＋对象池回收）。
///
/// **与玩家侧同源等价**（任务书 §一.A.3）：同样产 `KingdomTaskType.Gather` ＋ `GatherTaskArgs`，
/// 资源同样先入**工人背包**再按归属国分流（`TaskScheduler.ExecuteCompletion` 既有链），
/// **不提供免费/瞬时入账通道**（禁绕过"劳动"）。
///
/// 归属（**per-命令绑 `kingdomId`**）：构造即绑国；`TaskScheduler.SourceKingdom` 认本类型
/// ⇒ 池隔离路由至**本国**工人（不再像其它非 Building 源恒落 `return 0`＝玩家池）。
/// 可达/归属由登记侧（`WorldGatherRegistry`）在立案前以**本国领土**过滤（禁全局共享）。
///
/// 生命周期：登记（＝决策核采集意图落地）→ **每 tick 广告**（去重交 `HasAssignedTaskForSourceType`，
///          与 `Building.TryAdvertiseTask` 同模式）→ 派工 → 工人到点 Working(gatherSeconds)
///          → `OnGatherCompletion` 置失效 → 下 tick 被 `TaskScheduler` 清理。
///
/// ⚠️与 `TreeGatherSource` 的**唯一有意差异**（非漂移，理由在案）：
///   `TreeGatherSource` 在广告后立即 `IsValid=false`，而 `TaskScheduler.UpdateAssignedTasks:388`
///   有 `!task.source.IsValid ⇒ Abandon` ⇒ 该形态会**当场放弃刚派出的任务**（未被派工的那一 tick 更会
///   永久失去再广告机会）。本类**不复制该形态**：保持有效直至**采集完成**，重复广告由调度器既有
///   独占去重（`HasAssignedTaskForSourceType`）拦下。
///   **玩家侧 `TreeGatherSource` 现状逐字不动**（D685 A①「玩家侧现状不动」）。
/// </summary>
public class WorldGatherSource : ITaskSource
{
    /// <summary>本采集命令的归属国（per-命令绑定；`TaskScheduler.SourceKingdom` 读本字段做池隔离路由）。</summary>
    public int KingdomId { get; private set; }

    /// <summary>目标格（数据格树＝树格；一次性实体＝建筑主格）。</summary>
    public GridCoord Cell { get; private set; }

    private readonly Vector2 _pos;
    private readonly ResourceType _resource;
    private readonly int _amount;
    private readonly float _gatherSeconds;
    private readonly bool _isTree;      // true=数据格树路径；false=一次性实体路径
    private readonly Building _entity;  // 实体路径载体（树路径为 null）
    private bool _active = true;

    private WorldGatherSource(int kingdomId, GridCoord cell, Vector2 pos, ResourceType resource,
        int amount, float gatherSeconds, bool isTree, Building entity)
    {
        KingdomId = kingdomId;
        Cell = cell;
        _pos = pos;
        _resource = resource;
        _amount = amount;
        _gatherSeconds = gatherSeconds;
        _isTree = isTree;
        _entity = entity;
    }

    /// <summary>数据格树源（耗时/入包量取 `RespawnConfig`——与玩家砍树**同源参数**，禁另造数值）。</summary>
    public static WorldGatherSource ForTree(GridCoord cell, Vector2 pos, int kingdomId, RespawnConfig cfg)
    {
        return new WorldGatherSource(kingdomId, cell, pos, ResourceType.Wood,
            cfg != null ? cfg.treeGatherAmount : 5,
            cfg != null ? cfg.treeGatherSeconds : 2f,
            isTree: true, entity: null);
    }

    /// <summary>一次性实体源（耗时取 `BuildingDef.gatherSeconds`、入包量取调度器口径——与玩家采集**同源参数**）。</summary>
    public static WorldGatherSource ForEntity(Building entity, Vector2 pos, int kingdomId, int gatherAmount)
    {
        var def = entity != null ? entity.def : null;
        return new WorldGatherSource(kingdomId, entity != null ? entity.coord : default(GridCoord), pos,
            def != null ? def.outputResource : ResourceType.Stone,
            gatherAmount,
            def != null ? def.gatherSeconds : 2f,
            isTree: false, entity: entity);
    }

    /// <summary>有效性＝未完成 **且** 目标点仍在（树格 feature 仍是 Tree／实体仍 Active）。
    /// 后者兼作跨局/跨轮兜底：世界已清场 ⇒ 目标点判否 ⇒ 陈旧源自动失效（不依赖显式清场钩子）。</summary>
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
    /// 采集完成回调（由 `TaskScheduler.ExecuteCompletion` 在入包/台账后调用）。职责＝世界侧善后（与玩家同链）：
    /// 树 → 格翻 Plain＋记重生；一次性实体 → `Building.OnGatherCompleted()`（释放网格＋注销注册表＋守卫失去＋实体重生记账＋池回收）。
    /// </summary>
    public void OnGatherCompletion()
    {
        if (!_active) return;
        _active = false;
        if (_isTree)
        {
            if (ResourceRespawnSystem.HasInstance)
                ResourceRespawnSystem.Instance.HandleTreeGathered(Cell);
            return;
        }
        if (_entity != null) _entity.OnGatherCompleted();
    }

    /// <summary>目标点是否仍可采（树格恒查 features 唯一功能源；实体查 Building 存活态）。</summary>
    private bool PointStillThere()
    {
        if (!_isTree) return _entity != null && _entity.IsValid;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || map.features == null) return false;
        if (Cell.x < 0 || Cell.y < 0 || Cell.x >= map.width || Cell.y >= map.height) return false;
        int i = Cell.y * map.width + Cell.x;
        return map.features[i] == FeatureType.Tree;
    }
}
