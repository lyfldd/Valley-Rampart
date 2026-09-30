using UnityEngine;

/// <summary>
/// 铁匠铺（2_12 步骤8，D199~D201；D609/T1.1 原料由石改矿石）。矿石→Metal 就地加工。
/// Metal 为实体资源（可搬运/装箱/存储/交易），非加工中间物、非货币（D199）。
/// 生产公式（D200/D609）：消耗矿石 → 产出 Metal，转化率 SO 可配（2 矿 → 1 Metal 占位）。
/// 由 ProductionSystem 逐秒调度（与 ProducerComponent 并列，黑工铺不挂通用 Producer），
/// 每 tick 按 def.producer.rate 累积 Metal，达整数后用 StorageComponent.Transform 就地加工（D51）。
/// D704 A 批（OB1-2）：本组件自持 ITaskSource 广告 Production（source＝组件·原地劳作）；无在岗（Working）⇒ 停产。
/// 在岗判定＝HasWorkerAssigned(本组件) ‖ HasWorkerAssigned(建筑)（后者对建筑 ③搬运广告 source＝建筑，D704 §三-3-②）。
/// </summary>
public class BlacksmithBuilding : MonoBehaviour, ITickable, ITaskSource
{
    private Building _building;
    private StorageComponent _storage;
    private BlacksmithDef _def;
    private float _rate;
    private float _metalAccumulator;
    private bool _registered;   // TaskScheduler 懒注册（D704 A 批：组件自持 Production 广告源）

    /// <summary>当前每秒 Metal 产率（读档/升级后需 RefreshRate）。</summary>
    public float MetalRate => _rate;

    public void Init(Building building)
    {
        _building = building;
        _storage = building != null ? building.GetComponent<StorageComponent>() : null;
        _def = Resources.Load<BlacksmithDef>("Config/BlacksmithDef");
        _metalAccumulator = 0f;
        RefreshRate();
    }

    /// <summary>刷新产能 = def.producer.rate × 等级缩放（对齐 ProducerComponent.RefreshRate；升级后调用）。</summary>
    public void RefreshRate()
    {
        if (_building == null || _building.def == null) return;
        _rate = _building.def.producer.rate
                * _building.def.GetGradeScale(_building.grade)
                * _building.LevelScale();
    }

    /// <summary>逐秒 tick（ProductionSystem 调）。就地加工：矿石→Metal（StorageComponent.Transform，D200/T1.1）。</summary>
    public void Tick()
    {
        if (_building == null || !_building.IsActive) return;
        LazyRegister();   // 广告源注册须在岗门之前，否则无在岗时永不注册 ⇒ 永不自举
        if (_storage == null || _def == null || _storage.IsFull) return;
        if (_rate <= 0f) return;

        // D704 §三-3-②：在岗门——本组件自持广告（任务源＝组件）⇒ 读组件；铁匠铺另并计建筑（其 ③搬运广告 source＝建筑）。
        if (!HasWorkerOnDuty) return;

        _metalAccumulator += _rate;
        int metal = Mathf.FloorToInt(_metalAccumulator);
        if (metal <= 0) return;   // 低速率：未攒够整数 Metal 不加工（对齐金矿累计器，避免刷屏）

        int oreNeeded = metal * _def.oreToMetalRatio;
        int produced = _storage.Transform(ResourceType.Ore, ResourceType.Metal, oreNeeded);
        if (produced > 0)
            _metalAccumulator -= produced;   // 实际产出扣累计器；矿石不足/容量不足时整批不产，累计器保留待就绪
    }

    /// <summary>在岗判定（D704 §三-3-②）：组件源 ⇒ `HasWorkerAssigned(本组件)`；另并计 `HasWorkerAssigned(_building)`。</summary>
    bool HasWorkerOnDuty
    {
        get
        {
            var sched = TaskScheduler.Instance;
            if (sched == null) return false;
            return sched.HasWorkerAssigned(this)
                || (_building != null && sched.HasWorkerAssigned(_building));
        }
    }

    // ===== ITaskSource（D704 A 批：组件自持 Production 广告；源＝组件；非 Building 任务源形态同 `WorldGatherSource` 一脉）=====

    public bool IsValid => this != null && _building != null && _building.IsValid;

    public Vector2 SourcePos => _building != null ? (Vector2)_building.transform.position : Vector2.zero;

    public void OnRegister() { }
    public void OnUnregister() { }

    /// <summary>无在岗（组件源）⇒ 发 Production（常驻广告·destType=None 原地劳作）。储满/已在岗则不派（对齐 Building ② 口径）。</summary>
    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        if (_building == null || !_building.IsValid) return false;
        if (_storage != null && _storage.IsFull) return false;
        var sched = TaskScheduler.Instance;
        if (sched != null && sched.HasWorkerAssigned(this)) return false;   // 已在岗 ⇒ 不重复派（调度器亦按 source+type 去重）
        task = new KingdomTask(KingdomTaskType.Production, this);
        task.destType = KingdomDestType.None;
        return true;
    }

    // ============================================================================
    //  ⭐【HH.344 小源④】新任务协议生产接缝（并存链 · 旧 `KingdomTask(Production)` 链不动）
    //   权威＝`TaskBindingManager` 配对/预定；`_npcTaskMap` 仅作逐次对拍镜像。
    //   形态＝**循环型常驻源 · 一源多卡**（`D922` ②）：完成或失败只收口**该卡**，⛔ 不封源；
    //     源仍逐 tick 广告（`TryAdvertiseTask`），下一轮派发复用「回待派」卡或新建卡。
    //     · A 派发 ⇒ 建卡＋配对（`Pending→Assigned`）；
    //     · B 到达 ⇒ `Assigned→Executing`（⛔ 不等于产出、⛔ 不等于 `Complete`）；
    //     · E 单趟完成（调度器 `Complete` 之后）⇒ 收敛 `Done` 并释放配对/预定，源继续广告；
    //     · C 放弃 ⇒ 源仍可用则回待派计一次失败（上限＝`retryMax`＋初次 ⇒ 达上限 `Aborted`）；
    //       源已失效/注销 ⇒ 封口 `TargetRemoved`（⛔ 无人再派却留预定）；
    //     · D 源失效（建筑死亡/废弃/注销，调度器清无效源之前）⇒ 全部非终态卡封口 `TargetRemoved`
    //       （⛔ 无在途豁免：旧链在途例外仅 `ChestEntity` 享有）；
    //     · 组件销毁兜底 ⇒ ⛔ 不在已销毁对象上留残留。
    //   ⚠️ 真实产出承载＝`ProductionSystem.cs:50 → Tick() → StorageComponent.Transform`
    //     （`D922` ③ 双层判据）；调度器 `ExecuteCompletion` 的 Production 支取 `ProducerComponent`，
    //     而本源数据行无 `comp.producer` ⇒ 对本源**空操作**，⛔ 不冒充产出、⛔ 不为本源新增专用副作用分支。
    //   ⚠️ 双源并存：本源＝组件源，只广告 `Production`；本体 `Building` 源广告 `Transport`
    //     （源对象不同 ⇒ 按对象身份＋`task.type` 分列取证，⛔ 不合并成一个源）。
    // ============================================================================

    /// <summary>一次派发的协议槽（一工人一派一卡 · 卡引用只住本源）。</summary>
    private sealed class ProtocolSlot
    {
        public TaskCard card;
        /// <summary>失败次数（⛔ 不含"正常完成"；达政策上限 ⇒ `RetryExhausted` 封口）。</summary>
        public int failAttempts;
    }

    private readonly System.Collections.Generic.List<ProtocolSlot> _slots = new System.Collections.Generic.List<ProtocolSlot>();
    /// <summary>源已失效/注销（接缝 D 置位）⇒ 放弃不再回待派（回待派将无人再派 ⇒ 预定残留）。</summary>
    private bool _sourceInvalidated;
    private int _lastWorkerId;
    private int _totalCards;
    private int _doneCards;
    private int _abortedCards;
    private bool _protocolErrorLogged;

    /// <summary>⭐ 协议活卡数（非终态 · 诊断/验收只读口）。</summary>
    public int ProtocolLiveCardCount => _slots.Count;
    /// <summary>⭐ 累计建卡数（诊断只读口）。</summary>
    public int ProtocolTotalCards => _totalCards;
    /// <summary>⭐ 累计 `Done` 卡数（诊断只读口）。</summary>
    public int ProtocolDoneCards => _doneCards;
    /// <summary>⭐ 累计 `Aborted` 卡数（诊断只读口）。</summary>
    public int ProtocolAbortedCards => _abortedCards;
    /// <summary>⭐ 最近绑定工人（值标识 · 诊断只读口）。</summary>
    public int ProtocolLastWorkerId => _lastWorkerId;

    private TaskProtocolRuntime Rt()
    {
        if (!TaskScheduler.HasInstance) return null;
        return TaskScheduler.Instance.ProtocolRuntime;
    }

    private void LogProtocolOnce(string what, System.Exception ex)
    {
        if (_protocolErrorLogged) return;
        _protocolErrorLogged = true;
        Debug.LogError($"[BlacksmithBuilding] 新协议接缝异常（{what}）owner={(_building != null && _building.def != null ? _building.def.id : "?")}"
                       + " kingdom=" + (_building != null ? _building.kingdomId : -99)
                       + "：" + (ex != null ? ex.ToString() : "见上一步返回码"));
    }

    /// <summary>建卡并提交（统一经 `TaskProtocolIssuer` · 每派一卡）。
    /// 归属＝父建筑 `kingdomId`（对齐 `SourceKingdom` 的 Component 分支路由）；`deadline=+∞` ⇒ 显式载体记 `hasDeadline=false`。</summary>
    private ProtocolSlot CreateSlot()
    {
        var rt = Rt();
        if (rt == null) return null;
        try
        {
            var issuerRef = new TaskIssuerRef { kind = TaskIssuerKind.Kingdom, issuerId = _building != null ? _building.kingdomId : 0 };
            var issuer = new TaskProtocolIssuer(rt, issuerRef);
            var card = issuer.Create(TaskScheduler.Instance.NextProtocolTaskId(),
                _building != null ? _building.kingdomId : 0,
                "BlacksmithBuilding_Task",
                new TaskTargetRef
                {
                    kind = TaskTargetKind.Building,
                    cellX = _building != null ? _building.coord.x : 0,
                    cellY = _building != null ? _building.coord.y : 0,
                    typeKey = 0
                });
            card.duration = 0f;
            var submitted = issuer.Submit(card, 0, true, float.PositiveInfinity);
            if (submitted != TaskBindingResult.Ok && submitted != TaskBindingResult.AlreadyReserved)
            {
                LogProtocolOnce("Submit(" + submitted + ")", null);
                return null;
            }
            var slot = new ProtocolSlot { card = card };
            _slots.Add(slot);
            _totalCards++;
            return slot;
        }
        catch (System.Exception ex) { LogProtocolOnce("建卡/提交", ex); return null; }
    }

    /// <summary>接缝 A：调度器派发成功 ⇒ 选卡（本工人绑定卡 → 回待派卡 → 新建）并建立配对。</summary>
    public void OnProtocolDispatched(int workerId, long tick)
    {
        if (_sourceInvalidated) return;
        if (FindSlotForWorker(workerId) != null) return;   // 幂等：本工人已有活动卡（旧链占用幂等应已拦下 · 防御）
        var slot = FindPendingSlot() ?? CreateSlot();
        if (slot == null) return;
        var rt = Rt();
        if (rt == null) return;
        try
        {
            var err = rt.Assign(slot.card, workerId, float.PositiveInfinity);
            if (err != TaskTransitionError.None) LogProtocolOnce("Assign(" + err + ")", null);
            else _lastWorkerId = workerId;
        }
        catch (System.Exception ex) { LogProtocolOnce("配对建立", ex); }
        MirrorCheck(workerId);
    }

    /// <summary>接缝 B：调度器侧到达 ⇒ `Assigned→Executing`（一源多卡 ⇒ 带 workerId 定位卡）。
    /// ⚠️ 本回调只推进生命周期阶段，⛔ 不代表产出（产出＝`Tick()`）、⛔ 不代表 `Complete`。</summary>
    public void OnProtocolArrived(int workerId, long tick)
    {
        var slot = FindSlotForWorker(workerId);
        if (slot == null || slot.card.state != TaskLifecycleState.Assigned) return;
        var e = slot.card.Arrive();
        if (e != TaskTransitionError.None) LogProtocolOnce("Arrive(" + e + ")", null);
    }

    /// <summary>接缝 C：调度器侧放弃（`ClearNpc` 后）——源仍可用 ⇒ 失败回待派（计一次 · 可重派）；
    /// 源已失效/注销 ⇒ **不回待派**（无人再派 ⇒ 封口 `TargetRemoved` · ⛔ 不留预定残留）。</summary>
    public void OnProtocolAbandoned(int workerId, int legacyReason, long tick)
    {
        var slot = FindSlotForWorker(workerId);
        if (slot == null) return;
        var rt = Rt();
        if (rt == null) return;
        if (_sourceInvalidated || !IsValid)
        {
            FinalizeSlot(slot, TaskAbortReason.TargetRemoved, "源失效");
            return;
        }
        slot.failAttempts++;
        try
        {
            var e = rt.Unassign(slot.card, MapUnassignReason(legacyReason, slot.card.state), tick);
            if (e != TaskTransitionError.None)
            {
                LogProtocolOnce("Unassign(" + e + ")", null);
                FinalizeSlot(slot, TaskAbortReason.TargetRemoved, "回待派非法 ⇒ 封口");
                return;
            }
        }
        catch (System.Exception ex) { LogProtocolOnce("释放配对", ex); return; }

        if (slot.failAttempts >= 2) FinalizeSlot(slot, TaskAbortReason.RetryExhausted, "失败达上限");
        else
        {
            var re = slot.card.TryConsumeRetry();
            if (re != TaskTransitionError.None) LogProtocolOnce("TryConsumeRetry(" + re + ")", null);
        }
        MirrorCheck(workerId);
    }

    /// <summary>接缝 D：源失效（调度器 `Tick` 清理无效源之前）⇒ **全部**非终态卡封口
    /// （⛔ 无在途豁免：旧链在途例外仅 `ChestEntity` 享有 ⇒ 协议侧同收口）。
    /// ⚠️ 「暂时没矿／满仓／本轮无产出」**不是**源失效 ⇒ 不走本接缝，源继续广告。</summary>
    public void OnProtocolSourceInvalidated(long tick)
    {
        if (_sourceInvalidated) return;
        _sourceInvalidated = true;
        for (int i = _slots.Count - 1; i >= 0; i--)
            FinalizeSlot(_slots[i], TaskAbortReason.TargetRemoved, "源失效");
    }

    /// <summary>接缝 E：一趟任务正常完成（调度器 `Complete` 后）⇒ 收敛 `Done` 并释放（配对/预定归零）。
    /// ⭐ 循环型常驻源语义（`D922` ②）＝**只封这一张卡**，⛔ 不封源 ⇒ 卡出池后源照旧逐 tick 广告。</summary>
    public void OnProtocolTaskCompleted(int workerId, long tick)
    {
        var slot = FindSlotForWorker(workerId);
        if (slot == null) return;
        var rt = Rt();
        if (rt == null) return;
        try
        {
            if (slot.card.state == TaskLifecycleState.Assigned) slot.card.Arrive();
            if (slot.card.state == TaskLifecycleState.Executing) slot.card.BeginResolve();
            if (slot.card.state == TaskLifecycleState.Resolving)
            {
                var e = rt.Complete(slot.card);
                if (e != TaskTransitionError.None) LogProtocolOnce("Complete(" + e + ")", null);
                else { _doneCards++; _slots.Remove(slot); }
            }
            else LogProtocolOnce("完成后阶段=" + slot.card.state, null);
        }
        catch (System.Exception ex) { LogProtocolOnce("完成收口", ex); }
    }

    /// <summary>组件销毁兜底：先置失效（随后 `Unregister` 触发的旧链放弃经接缝 C 直接封口），再收口全部未终态卡
    /// ⇒ ⛔ 不留预定残留在已销毁对象上。</summary>
    private void SealAllOnDestroy()
    {
        _sourceInvalidated = true;
        for (int i = _slots.Count - 1; i >= 0; i--)
            FinalizeSlot(_slots[i], TaskAbortReason.TargetRemoved, "实体销毁");
    }

    /// <summary>终态封口：`TargetRemoved` 对进行中四阶段均合法（`IsLegalAbort`）⇒ 无需补边，直接 `Abort`。</summary>
    private void FinalizeSlot(ProtocolSlot slot, TaskAbortReason abortReason, string why)
    {
        if (slot == null) return;
        if (slot.card == null) { _slots.Remove(slot); return; }
        if (TaskLifecycleRules.IsTerminal(slot.card.state)) { _slots.Remove(slot); return; }
        var rt = Rt();
        if (rt == null) return;
        try
        {
            var e = rt.Abort(slot.card, abortReason);
            if (e != TaskTransitionError.None) LogProtocolOnce("Abort(" + e + ") " + why, null);
            else { _abortedCards++; _slots.Remove(slot); }
        }
        catch (System.Exception ex) { LogProtocolOnce("终态封口 " + why, ex); }
    }

    private ProtocolSlot FindSlotForWorker(int workerId)
    {
        if (workerId == 0) return null;
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].card != null && _slots[i].card.workerId == workerId) return _slots[i];
        return null;
    }

    private ProtocolSlot FindPendingSlot()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i].card != null && _slots[i].card.workerId == 0
                && _slots[i].card.state == TaskLifecycleState.Pending) return _slots[i];
        return null;
    }

    /// <summary>过渡镜像逐次对拍：`_npcTaskMap` 与配对表对本卡/本工人是否同真同假。</summary>
    private void MirrorCheck(int workerId)
    {
        var slot = FindSlotForWorker(workerId);
        if (slot == null || !TaskScheduler.HasInstance) return;
        if (TaskScheduler.Instance.ProtocolMirrorConsistent(workerId, slot.card.taskId)) return;
        LogProtocolOnce("过渡镜像不一致 workerId=" + workerId + " taskId=" + slot.card.taskId, null);
    }

    /// <summary>旧放弃原因 → 回待派原因（按卡阶段取合法值 · `IsLegalUnassign`）：
    /// `Assigned` 只接受 `WorkerDied/Unreachable`；`Executing` 额外接受 `Timeout`。</summary>
    private static TaskUnassignReason MapUnassignReason(int legacyReason, TaskLifecycleState st)
    {
        if (legacyReason == 4) return st == TaskLifecycleState.Executing ? TaskUnassignReason.Timeout : TaskUnassignReason.Unreachable;
        if (legacyReason == 1) return TaskUnassignReason.WorkerDied;
        return TaskUnassignReason.Unreachable;
    }

    void LazyRegister()
    {
        if (_registered || !TaskScheduler.HasInstance) return;
        TaskScheduler.Instance.Register(this);
        _registered = true;
    }

    void OnDestroy()
    {
        SealAllOnDestroy();   // ⭐【HH.344 小源④】先置失效＋收口协议卡（随后 Unregister 触发的旧链放弃经接缝 C 直接封口）
        if (_registered && TaskScheduler.HasInstance)
            TaskScheduler.Instance.Unregister(this);
    }
}