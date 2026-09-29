using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 掉落箱子实体（2_12 步骤7C + 步骤11 / D269 统一资源容器 D142）。
/// 落地资源统一用箱子承载：生成→落格可拾、限期消失、命中破碎洒内容、任意阵营可拾。
/// 本类为**数据容器 + 交互面**，详见 ChestManager（唯一归属者）。渲染归 2_10（"treasure_box" sprite），
/// 移动/拾取动画归 2_3，搬运调度归 2_8（`HH.316` 起实现 `ITaskSource`），存档归 2_11（ISaveable）。
/// 继承 MonoBehaviour，不逃入 Building 建筑管线（无造价/无 FSM/不产出的轻量可拾物）。
///
/// ⭐ `HH.316` · `U-2`（`D798` 裁 A-1「箱＝真仓」＋ 裁 ④「一箱一源」）：
///   · **内容物唯一真源 ＝ 容器**（`StorageComponent` 挂**本体** · 声明通用 `res` · 容量＝**实际掉落量** ·
///     ⛔ 不 `Register` · ⛔ 不调 `Init`）⇒ <see cref="contents"/> 改**转发读口**（⛔ 无独立字段 ⇒ 禁双写）；
///   · 实现 `ITaskSource`（一箱一源）：广告 `Transport` 任务 ⇒ 工人（**任何王国** · 无主先到先得）取货搬回；
///   · `Interact`（玩家手点）＝ **立案一个搬运任务**（`09` §9.8 :390）⇒ ⛔ 不提供「捡」的能力／⛔ 无独立入账口。
///   · 「不可掉落」（§九 :358 护栏 ⇒ ⛔ 防"箱再掉箱"递归）：容器**不入注册表** ＋ 本类/`ChestManager`
///     **无任何「容器 → 新箱」转换路径**（消亡＝实体销毁 · ⛔ 不洒落 ⇒ 无递归面）。
/// </summary>
public class ChestEntity : MonoBehaviour, IInteractable, ITaskSource
{
    /// <summary>挂格坐标（楼层=l0 微格）。</summary>
    public GridCoord cell;

    /// <summary>生成的绝对天数，过期 = 生成天 + ChestConfig.expireDays（D148）。</summary>
    public float bornDay;

    /// <summary>⚠️ `M1-D`/#57（`D802` `Q7`）：**语义已废**（新箱恒 `None` · `SpawnChest` 已去 `faction` 参数）——
    /// 字段**保留仅存档保真**（`ChestSaveEntry.ownerFaction` 读写不动 · ⛔ 不改存档格式）。</summary>
    public Faction ownerFaction = Faction.None;

    /// <summary>HP=1 一击碎（D247），破碎后内容物返回地面可再拾。</summary>
    public int hp = 1;

    /// <summary>箱＝仓（`D798` 裁 A-1）：内容物容器（挂**本体** ⇒ `LoadInventoryFromSource` 的
    /// `comp.GetComponent&lt;StorageComponent&gt;()` 直接命中 ⇒ 装载段零改）。</summary>
    private StorageComponent _store;

    /// <summary>箱位置快照（广告/派发用 —— ⛔ 不每读探 `transform` ⇒ 实体销毁后仍可安全读）。</summary>
    private Vector2 _pos;

    private SpriteRenderer _renderer;
    private bool _initialized;

    /// <summary>
    /// 内容物读口（⭐ **转发容器** · ⛔ 无独立字段 ⇒ 存档/搬运/过期只有一处真源；
    /// 形制照 `TreasureVault.Contents`）。未初始化 ⇒ 空。
    /// </summary>
    public ResourceList contents => _store != null ? _store.Contents : ResourceList.Empty;

    /// <summary>内容物容器（箱＝仓 · 未初始化 ⇒ null）。供 `ChestManager`／探针读。</summary>
    public StorageComponent Store => _store;

    /// <summary>初始化（幂等，仅首次生效）。cell/contents/bornDay 由 ChestManager.SpawnChest 预先填。</summary>
    public void Init(GridCoord c, ResourceList pack, float day)
    {
        if (_initialized) return;
        cell = c;
        bornDay = day;
        _pos = transform.position;
        EnsureStore(pack);
        _initialized = true;
    }

    /// <summary>
    /// 建容器并装载内容物（`D798` 裁 A-1 四条红线）：
    /// ① 声明**通用仓** `res`（§9.8 :389「它**就是个仓**」）· ⛔ 不调 `StorageComponent.Init`
    ///    （依赖 `Building` 且会被 `def.warehousePaths` 覆盖本声明 · 照 `TreasureVault.cs:58-65` 先例）；
    /// ② ⛔ **不 `WarehouseRegistry.Register`**（防成为他人卸货落点 · 结构性隔离）；
    /// ③ **容量 ＝ 实际掉落量**（§九 :356「零配置 · 刚好装下」· Σ(量×体积) ⇒ ⛔ 不设模板常数）；
    /// ④ 装载走 `RestoreContents`（逐条目 `Add`）—— 唯一真源 ＝ 容器 ⇒ ⛔ 无 `contents` 字段双写。
    /// </summary>
    private void EnsureStore(ResourceList pack)
    {
        if (_store == null)
        {
            _store = gameObject.AddComponent<StorageComponent>();
            _store.SetDeclaredPaths(new[] { WarehousePaths.All });
        }
        _store.droppable = false;   // ⭐ `M1-D` 件4 护栏（`09` §九 :358「箱子的仓打『不可掉落』标签」⇒ ⛔ 防"箱再掉箱"递归）
        _store.capacity = SpaceOf(pack);
        _store.RestoreContents(pack);
    }

    /// <summary>容量＝实际掉落量（Σ 量×体积；体积 0 条目不占容量 ⇒ 纯金箱容量 0 但照装不误）。</summary>
    private static int SpaceOf(ResourceList pack)
    {
        int sum = 0;
        if (pack.items != null)
        {
            for (int i = 0; i < pack.items.Length; i++)
            {
                int amt = pack.items[i].amount;
                if (amt > 0) sum += amt * ResourceCatalog.VolumeOf(pack.items[i].type);
            }
        }
        return sum;
    }

    void Awake()
    {
        Render();
    }

    /// <summary>渲染箱子占位（2_10 替换成真实瓦片）。</summary>
    private void Render()
    {
        if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
        _renderer.sprite = ValleyRampart.Rendering.PlaceholderSprites.Get("feat_treasure_box");
        _renderer.sortingOrder = 5;
    }

    /// <summary>箱子是否已空（容器空 ＝ §九「生命结束触发②」）。</summary>
    public bool IsEmpty => _store == null || _store.TotalCount <= 0;

    // ===== ITaskSource（⭐ `HH.316` 件2 · 一箱一源 · `D798` 裁 ④）=====

    /// <summary>源有效 ＝ 实体在场 ＋ **容器非空**（有货可搬）。⚠️ 装箱走**最后一批**的工人（已入
    /// `MovingToDest`）不受本失效牵连 —— 见 `TaskScheduler.UpdateAssignedTasks` 箱源例外注
    /// （否则该批滞留背包 ⇒ 到账断链）。</summary>
    public bool IsValid => this != null && !IsEmpty;

    /// <summary>箱位置（第一段位移目标 · 快照 ⇒ 销毁后仍可安全读）。</summary>
    public Vector2 SourcePos => _pos;

    /// <summary>
    /// 广告搬运任务（复用 `Transport` · ⛔ 不新增枚举；`args` 沿用 `ScaleTaskArgs` —— ⭐ 规模派工
    /// ⇒ 同一箱可**并发多工人**各取一趟（准 · 符合"先到先得"））。
    /// 取**资源表序首个非空**（确定性）⇒ 多资源箱**逐轮广告多轮搬运**（判据 4）。
    /// `destType = None`：箱**无主**（`SourceKingdom → -1`）⇒ 第二段落点须**装载成功后按
    /// 搬运者国 ＋ 实载资源**即时解析（件 4 · 广告时定不了）。
    /// </summary>
    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        if (_store == null || IsEmpty) return false;
        var type = _store.PrimaryStoredType();      // 资源表序首个非空（确定性）
        int have = _store.GetAmount(type);
        if (have <= 0) return false;
        task = new KingdomTask(KingdomTaskType.Transport, this);
        task.destType = KingdomDestType.None;
        task.args = new ScaleTaskArgs
        {
            resourceType = type,
            totalResourceDemand = have
        };
        return true;
    }

    public void OnRegister() { }

    /// <summary>⛔ 关键清算**勿放此处**：`TaskScheduler.Tick` ① 清无效源**不回调** `OnUnregister`（R5）
    /// ⇒ 注销/清算走 `ChestManager` 显式钩子（`Remove`／`ClearAll`）。照 `Building`／`ConstructionSiteStore` 先例留空。</summary>
    public void OnUnregister() { }

    // ===== IInteractable（⭐ `HH.316` 件6 · `D798` 裁 ①「链 B 并回链 A」）=====

    /// <summary>
    /// 玩家手点 ＝ **调用搬运任务的一种形式**（`09` §9.8 :390）⇒ **立案一个搬运任务**
    /// （立即触发一次调度 ⇒ 工人在场即来搬；无空闲工人则照常每 tick 广告、来日再来）。
    /// ⛔ **不提供「捡」的能力**（§9.8 :389「它**就是个仓**」）／⛔ **无独立入账口** —— 到账只走
    /// 链 A 卸货段（`UnloadInventory` → 就近同国仓；无仓 ⇒ `AddGatherOverflow` 兜底）。
    /// ⭐ `M1-E`：**金与材料同路**（原「金→`Gold` 字段」直通已退役 ⇒ 金进该国国库仓）。
    /// </summary>
    public InteractionResult Interact(Interactor ctx)
    {
        if (IsEmpty) return InteractionResult.None;   // 空箱 ⇒ 无货可搬（⛔ 不出任务）
        if (TaskScheduler.HasInstance) TaskScheduler.Instance.RequestHaulNow(this);
        return InteractionResult.None;                // ⛔ 不返回资源包（`D798` 已否决「Interact 内直接入账」）
    }

    // ===== 命中（D247）：HP=1 一击碎，内容物返回地面可再拾 =====

    /// <summary>命中（D247）：HP=1 一击碎 ⇒ 内容物原地重落箱（经 `ChestManager.ResetDrop` · 容器转发读口）。</summary>
    public void Strike()
    {
        if (--hp <= 0)
        {
            // 破碎：内容物原地重新落箱（可再拾取）
            if (ChestManager.HasInstance) ChestManager.Instance.ResetDrop(this);
        }
    }

    // ============================================================================
    //  ⭐【HH.341 小源②】新任务协议生产接缝（并存链 · 旧 KingdomTask(Transport) 链不动）
    //   权威 ＝ `TaskBindingManager` 配对/预定；`_npcTaskMap` 仅作逐次对拍镜像。
    //   形态 ＝ **一箱多卡**（`_slots`）：旧链 `Transport` 规模派工（`D95`）允许**一箱并发多工人**
    //     各取一趟 ⇒ **一趟一卡**（卡按 `taskId` 聚合；失败回待派可重派 · 上限 2 ＝ 初次 1 ＋ 重试 1）；
    //     · 一趟正常完成（卸货成功 / 装载失败兜底 `Complete`）⇒ 收敛 `Done`（配对与预定均释放）；
    //     · 失败（超时/不可达/死亡/卸货失败）且源仍可用 ⇒ 回待派计一次（`retryConsumedCount` 随卡）；达上限 ⇒ `Aborted`；
    //     · 源失效（箱空/注销）⇒ 非在途卡封口 `TargetRemoved`；**在途卡**（旧链 `MovingToDest`）保留，
    //       待「卸货完成 ⇒ Done」或「放弃 ⇒ 封口」收口 —— 对齐 `TaskScheduler.UpdateAssignedTasks` 箱源失效例外注；
    //     · 实体销毁（`ChestManager.Remove`/`ClearAll`）⇒ `OnDestroy` 兜底封口 ⇒ ⛔ 不留预定残留。
    // ============================================================================

    /// <summary>一趟搬运的协议槽（一工人一趟一卡 · 卡引用只住本源）。</summary>
    private sealed class ProtocolSlot
    {
        public TaskCard card;
        /// <summary>失败次数（⛔ 不含"正常完成"；上限 2 ＝ 初次 1 ＋ 重试 1）。</summary>
        public int failAttempts;
    }

    private readonly List<ProtocolSlot> _slots = new List<ProtocolSlot>();
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

    // ============================================================================
    //  协议编排（全部由 `TaskScheduler` 侧回调驱动 · 源自身零轮询）
    //   ⛔ 不复制调度器内核职责；⛔ 不新增全局主表；统计按单卡（政策）。
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
        Debug.LogError($"[ChestEntity] 新协议接缝异常（{what}）cell=({cell.x},{cell.y})："
                       + (ex != null ? ex.ToString() : "见上一步返回码"));
    }

    /// <summary>建卡并提交（统一经 `TaskProtocolIssuer` · 每趟一卡）。
    /// `kingdomId=-1`（箱＝无主源 · 对齐 `SourceKingdom` 路由口径）；`deadline=+∞` ⇒ 显式载体记 `hasDeadline=false`。</summary>
    private ProtocolSlot CreateSlot()
    {
        var rt = Rt();
        if (rt == null) return null;
        try
        {
            var issuerRef = new TaskIssuerRef { kind = TaskIssuerKind.Kingdom, issuerId = -1 };
            var issuer = new TaskProtocolIssuer(rt, issuerRef);
            var card = issuer.Create(TaskScheduler.Instance.NextProtocolTaskId(), -1,
                "ChestEntity_Haul",
                new TaskTargetRef
                {
                    kind = TaskTargetKind.WorldResource,
                    cellX = cell.x,
                    cellY = cell.y,
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

    /// <summary>接缝 B：调度器侧到达（取货段）⇒ `Assigned→Executing`（一箱多卡 ⇒ 带 workerId 定位卡）。</summary>
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

    /// <summary>接缝 D：源失效（调度器 `Tick` 清理无效源之前）——非在途卡封口；在途卡保留（待接缝 E/C 收口）。</summary>
    public void OnProtocolSourceInvalidated(long tick)
    {
        if (_sourceInvalidated) return;
        _sourceInvalidated = true;
        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            var slot = _slots[i];
            if (slot.card == null || TaskLifecycleRules.IsTerminal(slot.card.state)) { _slots.RemoveAt(i); continue; }
            if (IsInTransit(slot)) continue;
            FinalizeSlot(slot, TaskAbortReason.TargetRemoved, "源失效");
        }
    }

    /// <summary>接缝 E：一趟搬运正常完成（调度器 `Complete` 后）⇒ 收敛 `Done` 并释放（配对/预定归零）。</summary>
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

    /// <summary>实体销毁兜底（`ChestManager.Remove`/`ClearAll` ⇒ `Destroy`）：收口全部未终态卡
    /// （`Remove` 的旧链放弃只释放配对；回待派卡由此处封口 ⇒ ⛔ 不留预定残留在已销毁对象上）。</summary>
    private void OnDestroy()
    {
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

    /// <summary>该槽是否「已装载在途」（旧链 `MovingToDest`）——对齐调度器箱源失效例外
    /// （已装载批次不因源失效放弃 ⇒ 协议侧同样保留，待卸货完成/放弃收口）。</summary>
    private static bool IsInTransit(ProtocolSlot slot)
    {
        if (slot == null || slot.card == null || slot.card.workerId == 0) return false;
        if (!TaskScheduler.HasInstance) return false;
        return TaskScheduler.Instance.GetWorkerState(slot.card.workerId) == TaskState.MovingToDest;
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
}