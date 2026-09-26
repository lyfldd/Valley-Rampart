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

    // ===== ⭐【HH.341 D1 首片】新任务协议生产面（并存链 · 真卡；旧 KingdomTask 链不动）=====
    //   权威：工人占用 ＝ `TaskBindingManager` 配对表（`TaskScheduler._npcTaskMap` 仅作旧内核镜像，逐次对拍）。
    //   形态：一源一卡（`_card`）· 同 taskId 聚合重派 · 最多 2 次尝试（初次 1 ＋ 重试 1）· 终态封口。
    private TaskCard _card;
    private int _protocolAttempts;
    private bool _protocolTerminal;
    private int _lastWorkerId;
    private bool _protocolErrorLogged;

    /// <summary>⭐ 协议卡（诊断/验收只读口；未接或未建卡 ⇒ null）。</summary>
    public TaskCard ProtocolCard => _card;
    /// <summary>⭐ 终态封口标记（Done／Aborted 后恒 true ⇒ 不再有任何协议动作）。</summary>
    public bool ProtocolTerminal => _protocolTerminal;
    /// <summary>⭐ 尝试数（按 taskId 聚合；上限 2）。</summary>
    public int ProtocolAttempts => _protocolAttempts;
    /// <summary>⭐ 最近一次绑定的工人（值标识）。</summary>
    public int ProtocolLastWorkerId => _lastWorkerId;

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
        // ⭐【HH.341 D1】新协议终态：按当前阶段收敛到 Done（Resolving→Done ⇒ 释放配对/预定；零残留）。
        FinalizeProtocol(TaskAbortReason.TargetExhausted, "采集完成");
        if (ResourceRespawnSystem.HasInstance)
            ResourceRespawnSystem.Instance.HandleCellGathered(Cell);
    }

    /// <summary>目标格是否仍可采（格表唯一功能源：地表物仍为登记时的同型）。</summary>
    private bool PointStillThere() => MapGate.GetFeatureAt(Cell) == Feature;

    // ============================================================================
    //  ⭐【HH.341 D1 首片】新协议生产接缝（全部由 `TaskScheduler` 侧回调驱动 · 源自身零轮询）
    //    ⛔ 本块不复制调度器内核职责：只做「本源 ↔ 本卡」的协议编排（建卡/提交/推进/释放）。
    //    ⛔ 不新增全局主表；卡片引用只住本源（统计按单卡 · 政策）。
    // ============================================================================

    private TaskProtocolRuntime Rt()
    {
        if (!TaskScheduler.HasInstance) return null;
        return TaskScheduler.Instance.ProtocolRuntime;
    }

    private void LogProtocolOnce(string what, System.Exception ex)
    {
        if (_protocolErrorLogged) return;
        _protocolErrorLogged = true;
        Debug.LogError($"[WorldGatherSource] 新协议接缝异常（{what}）cell=({Cell.x},{Cell.y}) kingdom={KingdomId}："
                       + (ex != null ? ex.ToString() : "见上一步返回码"));
    }

    /// <summary>建卡并提交（幂等 · 仅一次）：`Created→Pending` 后写入 M5-B 预定表与王国分桶。
    /// `basePriority` 本片写 0（未填 · 口径待 M5-C 裁）；`deadline` 传 `+∞`（未设 · `hasDeadline` 载体缺失已登记）。</summary>
    private void EnsureCard()
    {
        if (_card != null || _protocolTerminal) return;
        var rt = Rt();
        if (rt == null) return;
        try
        {
            // ⭐【补齐批 ⑥】建卡**统一经 `TaskProtocolIssuer` 封装**（⛔ 不再自行 `new TaskCard()`）：
            //   `Create` 写 `issuer` 与 `retryMax = Policy.defaultRetryMax`（根治 `RetryPolicyUnconfigured`）；
            //   `Submit` 写 `basePriority` 并走 `Accept()`＋`Runtime.Submit`（预定表 ＋ 王国分桶）。
            var issuerRef = new TaskIssuerRef { kind = TaskIssuerKind.Kingdom, issuerId = KingdomId };
            var issuer = new TaskProtocolIssuer(rt, issuerRef);
            var card = issuer.Create(TaskScheduler.Instance.NextProtocolTaskId(), KingdomId,
                "WorldGatherSource_Gather", new TaskTargetRef
                {
                    kind = TaskTargetKind.WorldResource,
                    cellX = Cell.x,
                    cellY = Cell.y,
                    typeKey = (int)Feature
                });
            card.duration = _gatherSeconds;
            // `basePriority` 由上层写（政策：上层写基值 · 运行时只读算有效值）；本片写 0（未填）。
            // `deadline` 传 `+∞` ⇒ ⑤ 的显式载体记 `hasDeadline=false`（未设 · ⛔ 不写 Infinity 入档）。
            var submitted = issuer.Submit(card, 0, true, float.PositiveInfinity);
            if (submitted != TaskBindingResult.Ok && submitted != TaskBindingResult.AlreadyReserved)
            {
                LogProtocolOnce("Submit(" + submitted + ")", null);
                return;
            }
            _card = card;
        }
        catch (System.Exception ex) { LogProtocolOnce("建卡/提交", ex); }
    }

    /// <summary>接缝 A：调度器派发成功（旧链 `Dispatch` 之后）⇒ 建立 M5-B 配对（权威占用写入）。
    /// 换人场景先解绑（`Assigned/Executing` 只接受 `WorkerDied/Unreachable/Timeout`）。</summary>
    public void OnProtocolDispatched(int workerId, long tick)
    {
        if (_protocolTerminal) return;
        EnsureCard();
        if (_card == null) return;
        var rt = Rt();
        if (rt == null) return;

        _protocolAttempts++;
        if (_protocolAttempts > 2)
        {
            FinalizeProtocol(TaskAbortReason.RetryExhausted, "尝试数超上限");
            return;
        }
        try
        {
            if (_card.workerId != 0 && _card.workerId != workerId)
                rt.Unassign(_card, TaskUnassignReason.Unreachable, tick);

            var err = rt.Assign(_card, workerId, float.PositiveInfinity);
            if (err != TaskTransitionError.None) LogProtocolOnce("Assign(" + err + ")", null);
            else _lastWorkerId = workerId;
        }
        catch (System.Exception ex) { LogProtocolOnce("配对建立", ex); }
        MirrorCheck(workerId);
    }

    /// <summary>接缝 B：调度器侧到达（旧链 `MovingToSource→Working`）⇒ `Assigned→Executing`。</summary>
    public void OnProtocolArrived(long tick)
    {
        if (_card == null || _protocolTerminal) return;
        if (_card.state != TaskLifecycleState.Assigned) return;
        var e = _card.Arrive();
        if (e != TaskTransitionError.None) LogProtocolOnce("Arrive(" + e + ")", null);
    }

    /// <summary>接缝 C：调度器侧放弃（旧链 `Abandon` · `ClearNpc` 之后）⇒ 解绑回待派（可重派）；
    /// 尝试数达上限 2 ⇒ 终态封口（`RetryExhausted` · 不产生重复活动卡）。</summary>
    public void OnProtocolAbandoned(int workerId, int legacyReason, long tick)
    {
        if (_protocolTerminal || _card == null) return;
        if (_card.workerId != workerId) return;      // 非本卡工人 ⇒ 不动作（防误清他卡配对）
        var rt = Rt();
        if (rt == null) return;
        try
        {
            var e = rt.Unassign(_card, MapUnassignReason(legacyReason, _card.state), tick);
            if (e != TaskTransitionError.None)
            {
                // 回待派不被允许（阶段/原因不匹配）⇒ 收敛到终态，⛔ 不留配对残留（响亮报错一次 + 释放）。
                LogProtocolOnce("Unassign(" + e + ")", null);
                FinalizeProtocol(TaskAbortReason.TargetRemoved, "回待派非法 ⇒ 封口");
                return;
            }
        }
        catch (System.Exception ex) { LogProtocolOnce("释放配对", ex); return; }

        if (_protocolAttempts >= 2) FinalizeProtocol(TaskAbortReason.RetryExhausted, "尝试数达上限");
        else
        {
            var re = _card.TryConsumeRetry();        // 重试额度（retryConsumedCount 随卡片统计）
            if (re != TaskTransitionError.None) LogProtocolOnce("TryConsumeRetry(" + re + ")", null);
        }
        MirrorCheck(workerId);
    }

    /// <summary>接缝 D：源失效（调度器 `Tick` 清理无效源之前）⇒ 未终态则封口 `TargetRemoved`（释放配对/预定）。</summary>
    public void OnProtocolSourceInvalidated(long tick)
    {
        if (_protocolTerminal) return;
        if (_card == null) { _protocolTerminal = true; return; }
        FinalizeProtocol(TaskAbortReason.TargetRemoved, "源失效");
    }

    /// <summary>按当前阶段收敛到终态（先补齐合法边 `Assigned→Executing→Resolving`，再 `Complete`／`Abort`）。</summary>
    private void FinalizeProtocol(TaskAbortReason abortReason, string why)
    {
        if (_protocolTerminal) return;
        _protocolTerminal = true;
        if (_card == null) return;
        var rt = Rt();
        if (rt == null) return;
        try
        {
            if (_card.state == TaskLifecycleState.Assigned) _card.Arrive();
            if (_card.state == TaskLifecycleState.Executing) _card.BeginResolve();
            if (_card.state == TaskLifecycleState.Resolving)
            {
                var e = rt.Complete(_card);
                if (e != TaskTransitionError.None) LogProtocolOnce("Complete(" + e + ")", null);
            }
            else if (!TaskLifecycleRules.IsTerminal(_card.state))
            {
                var e = rt.Abort(_card, abortReason);
                if (e != TaskTransitionError.None) LogProtocolOnce("Abort(" + e + ") " + why, null);
            }
        }
        catch (System.Exception ex) { LogProtocolOnce("终态封口 " + why, ex); }
    }

    /// <summary>过渡镜像逐次对拍（D875 §3）：旧 `_npcTaskMap` 与配对表对本卡/本工人是否一致。</summary>
    private void MirrorCheck(int workerId)
    {
        if (_card == null || !TaskScheduler.HasInstance) return;
        if (TaskScheduler.Instance.ProtocolMirrorConsistent(workerId, _card.taskId)) return;
        LogProtocolOnce("过渡镜像不一致 workerId=" + workerId + " taskId=" + _card.taskId, null);
    }

    /// <summary>旧放弃原因 → 新协议回待派原因（**按卡当前阶段取合法值** · `TaskLifecycleRules.IsLegalUnassign`）：
    /// `Assigned` 只接受 `WorkerDied/Unreachable`；`Executing` 额外接受 `Timeout`；`Resolving` 只接受 `Replan`。
    /// ⚠️ 实读教训（首跑 4 条 `Unassign(IllegalReason)`）：旧链 `Timeout` 发生在 `MovingToSource`（＝`Assigned`）
    ///   ⇒ 直接映射 `Timeout` 非法 ⇒ 该阶段降级为 `Unreachable`（语义仍成立：到不了）。</summary>
    private static TaskUnassignReason MapUnassignReason(int legacyReason, TaskLifecycleState st)
    {
        // 旧 AbandonReason：0=External 1=Dead 2=Unreachable 3=SourceInvalid 4=Timeout 5=BrainLost 6=DestFull 7=Unknown
        if (legacyReason == 4) return st == TaskLifecycleState.Executing ? TaskUnassignReason.Timeout : TaskUnassignReason.Unreachable;
        if (legacyReason == 1) return TaskUnassignReason.WorkerDied;
        return TaskUnassignReason.Unreachable;
    }
}