using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 矿洞副产组件（DZ-072a，D562 / HH.107 件1）。
/// mine 双身份=A2 变体（已裁决策）：mine 保持 isResourceNode=1 石头采集点身份不动（WanderStimulusProvider/
/// GuardDeploymentSystem/Building 拆除守卫/BuildingPanel 全消费此 flag——M1 红线），另挂本组件产水晶/火油/矿石（**在岗才产**，D704 A 批）。
///
/// 结构（仿 SiegeWorkshopBuilding 厂级弹药仓）：
///   - 挂 **3** 个**专属仓**子 StorageComponent（Crystal/FireOil/**Ore**；T1.4/D609 加矿石伴生）——
///     ⭐ `M1-A` 起 StorageComponent 是多资源容器 ⇒ 三个子仓各用**专属仓声明**（`SetDeclaredPaths` 该资源的完整路径
///     ＝ `09` §3.2「专属仓」写法）表达"只收这一种"，⛔ 不再靠 `resourceType` 单资源字段。
///     容量取 KingdomConfig.byproductCrystalCapacity/byproductFireOilCapacity/byproductOreCapacity。
///     不注册 WarehouseRegistry（子仓=待运出缓冲非可存仓，见 CreateSubStore 注）。
///   - 产率：KingdomConfig.byproductCrystalRate/byproductFireOilRate/byproductOreRate（0.05/s=慢产保稀缺；
///     副产无等级门槛，原 ProducerComponent Lv2/Lv3 门槛随 mine levels=[] 不适用——已裁决策）。
///   - 在岗才产（D704 A 批·OB1-2）：本组件自持 ITaskSource 广告 Production（source＝组件·原地劳作）；无在岗（Working）⇒ 三槽停产。
///     在岗＝HasWorkerAssigned(本组件)（任意任务类型 Working 均算在场）；石头采集链的工人派工与本组件互不相干。
///   - 搬运：本组件实现 ITaskSource（随 `WorldGatherSource` 一脉的非 Building 任务源形态），子仓存量达
///     transportThreshold 时发 Transport（destType=NearestWarehouse；args 带子仓资源类型，
///     TaskScheduler.LoadInventoryFromSource 按 args 资源类型取子仓）。
///   - 由 ProductionSystem 逐秒调度（与 ProducerComponent/BlacksmithBuilding/SiegeWorkshopBuilding 并列）。
///
/// 已知限制（列报）：同矿水晶/火油两仓 Transport 广告按同 source 同 type 计数（CountAssignedForType），
/// 并发在派时互相挤占规模派工名额——效果为两仓错峰搬运（不断链），最小方案接受。
///
/// 存档：BuildingSaveData.byproductCrystalAmount/byproductFireOilAmount/**byproductOreAmount**（尾插，旧档缺→默认 0 零 bump），
/// Building.SaveState/LoadState 经 SaveByproductState/RestoreByproductState 读写。
/// ResetState：随建筑 GameObject 销毁自然清（组件随建筑域既有清场链），无需 WorldLifecycle 新编排（列报确认）。
/// </summary>
public class MineByproductComponent : MonoBehaviour, ITickable, ITaskSource
{
    const string SubStorePrefix = "Byproduct_";

    private Building _building;
    private StorageComponent _crystalStore;
    private StorageComponent _fireOilStore;
    private StorageComponent _oreStore;            // T1.4（D609）：矿石伴生子仓
    private float _crystalAccumulator;
    private float _fireOilAccumulator;
    private float _oreAccumulator;
    private bool _crystalFullLogged;   // 满仓停产分频：满时只记一次，消耗后复位（防逐秒刷屏）
    private bool _fireOilFullLogged;
    private bool _oreFullLogged;
    private bool _registered;          // TaskScheduler 懒注册（调度器未就绪时跳过，首 Tick 补挂——与采集源懒注册同语义）

    public void Init(Building building)
    {
        _building = building;
        _crystalAccumulator = 0f;
        _fireOilAccumulator = 0f;
        _oreAccumulator = 0f;
        _crystalFullLogged = false;
        _fireOilFullLogged = false;
        _oreFullLogged = false;
        _registered = false;
        CreateSubStores();
    }

    /// <summary>创建 3 个**专属仓**副产子仓（仿 SiegeWorkshopBuilding.CreateSubStores/TreasureVault 子物体聚合）。
    /// T1.4（D609）：第三仓=矿石（矿场伴生，Ore→Metal 链供给端）。</summary>
    void CreateSubStores()
    {
        if (_building == null) return;
        var config = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        _crystalStore = CreateSubStore(ResourceType.Crystal, config != null ? config.byproductCrystalCapacity : 20);
        _fireOilStore = CreateSubStore(ResourceType.FireOil, config != null ? config.byproductFireOilCapacity : 20);
        _oreStore = CreateSubStore(ResourceType.Ore, config != null ? config.byproductOreCapacity : 20);
        string defId = _building.def != null ? _building.def.id : "?";
        Debug.Log("[MineByproduct] 副产仓就绪（" + defId + "）：水晶仓 cap=" + _crystalStore.capacity + "，火油仓 cap=" + _fireOilStore.capacity + "，矿石仓 cap=" + _oreStore.capacity);
    }

    StorageComponent CreateSubStore(ResourceType type, int capacity)
    {
        var go = new GameObject(SubStorePrefix + type);
        go.transform.SetParent(_building.transform, false);
        var sc = go.AddComponent<StorageComponent>();
        // ⭐ 专属仓声明＝该资源的完整路径（09 §3.2 第三种写法）⇒ 只收这一种，其余前缀不匹配被拒。
        sc.SetDeclaredPaths(new[] { ResourceCatalog.PrimaryPathOf(type) });
        sc.capacity = Mathf.Max(1, capacity);
        // 不调 StorageComponent.Init（否则会被 def.warehousePaths 覆盖本专属仓声明）。
        // 不注册 WarehouseRegistry（DZ-072a 语义：副产子仓=待运出缓冲，非可存仓——若注册，
        // AI 卸货 FindNearestAvailable 会就近卸回本仓死循环，AI 台账永不得水晶；不注册则
        // AI 走 AddGatherOverflow 台账兜底、玩家直卸国库 Vault_Crystal，P2/P5 落库链稳定通）。
        return sc;
    }

    /// <summary>每秒 tick（ProductionSystem 调度）：水晶/火油/矿石三槽并行恒产，独立容量互不挤占。</summary>
    public void Tick()
    {
        if (_building == null || !_building.IsActive) return;
        LazyRegister();
        // D704 §三-3-⑤：在岗门与广告序同源——无在岗 ⇒ 不发 Production ⇒ 三槽停产（累积前 return）。
        if (!HasWorkerOnDuty) return;
        var config = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        TickStore(_crystalStore, ResourceType.Crystal, config != null ? config.byproductCrystalRate : 0.05f, ref _crystalAccumulator, ref _crystalFullLogged);
        TickStore(_fireOilStore, ResourceType.FireOil, config != null ? config.byproductFireOilRate : 0.05f, ref _fireOilAccumulator, ref _fireOilFullLogged);
        TickStore(_oreStore, ResourceType.Ore, config != null ? config.byproductOreRate : 0.05f, ref _oreAccumulator, ref _oreFullLogged);   // T1.4：矿石伴生
    }

    void TickStore(StorageComponent store, ResourceType type, float rate, ref float accumulator, ref bool fullLogged)
    {
        if (store == null || rate <= 0f) return;
        if (store.IsFullFor(type))
        {
            if (!fullLogged)   // 分频：满仓停产只记一次，防逐秒刷屏（对齐任务书口径）
            {
                fullLogged = true;
                Debug.Log($"[MineByproduct] {type} 副产仓满（{store.GetAmount(type)}/{store.capacity}）停产，等待搬运");
            }
            return;
        }
        fullLogged = false;
        accumulator += rate;
        int amount = Mathf.FloorToInt(accumulator);
        if (amount <= 0) return;   // 低速率：未攒够整数不产（对齐 SiegeWorkshopBuilding 累计器口径）
        int added = store.Add(type, amount);
        if (added > 0)
        {
            accumulator -= added;   // 实际入仓扣累计器（仓满中途截断时余量保留待下轮）
            Debug.Log($"[MineByproduct] 产 {type} ×{added}（存量 {store.GetAmount(type)}/{store.capacity}）");
        }
    }

    /// <summary>按资源类型取副产子仓（TaskScheduler.LoadInventoryFromSource 按 args 资源类型取货用；仿 SiegeWorkshopBuilding.GetStore）。</summary>
    public StorageComponent GetStore(ResourceType type)
    {
        if (type == ResourceType.Crystal) return _crystalStore;
        if (type == ResourceType.FireOil) return _fireOilStore;
        if (type == ResourceType.Ore) return _oreStore;   // T1.4（D609）
        return null;
    }

    // ===== 存档（Building.SaveState/LoadState 调；BuildingSaveData 尾插字段）=====

    /// <summary>保存副产子仓存量（水晶量, 火油量, 矿石量）。T1.4（D609）：第三元=矿石。</summary>
    public (int crystal, int fireOil, int ore) SaveByproductState()
        => (_crystalStore != null ? _crystalStore.GetAmount(ResourceType.Crystal) : 0,
            _fireOilStore != null ? _fireOilStore.GetAmount(ResourceType.FireOil) : 0,
            _oreStore != null ? _oreStore.GetAmount(ResourceType.Ore) : 0);

    /// <summary>读档恢复副产子仓存量（超容量 clamp 不静默丢——对齐 SiegeWorkshopBuilding.RestoreLegacyAmmo 口径）。T1.4：第三参=矿石（旧档缺→0）。</summary>
    public void RestoreByproductState(int crystal, int fireOil, int ore)
    {
        if (crystal > 0 && _crystalStore != null)
        {
            int added = _crystalStore.Add(ResourceType.Crystal, crystal);
            if (added < crystal)
                Debug.LogWarning($"[MineByproduct] 读档水晶超容量 clamp {crystal}→{added}");
        }
        if (fireOil > 0 && _fireOilStore != null)
        {
            int added = _fireOilStore.Add(ResourceType.FireOil, fireOil);
            if (added < fireOil)
                Debug.LogWarning($"[MineByproduct] 读档火油超容量 clamp {fireOil}→{added}");
        }
        if (ore > 0 && _oreStore != null)
        {
            int added = _oreStore.Add(ResourceType.Ore, ore);
            if (added < ore)
                Debug.LogWarning($"[MineByproduct] 读档矿石超容量 clamp {ore}→{added}");
        }
    }

    // ===== ITaskSource（搬运广告；非 Building 任务源形态，同 `WorldGatherSource` 一脉）=====

    public bool IsValid => this != null && _building != null && _building.IsValid;

    public Vector2 SourcePos => _building != null ? (Vector2)_building.transform.position : Vector2.zero;

    public void OnRegister() { }
    public void OnUnregister() { }

    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        if (_building == null || !_building.IsValid) return false;

        // D704 §三-3-③：广告序＝硬 if/else。无在岗 ⇒ 发 Production（常驻广告·destType=None 原地劳作）；
        // 有在岗 ⇒ 走既有三仓 Transport 判定。禁同 tick 双发（ITaskSource 同 tick 只返一任务）。
        if (!HasWorkerOnDuty)
        {
            task = new KingdomTask(KingdomTaskType.Production, this);
            task.destType = KingdomDestType.None;
            return true;
        }

        float threshold = _building.transportThreshold;
        // 三仓分别判达标（存量≥capacity×threshold），一次只发先达标的一个（同 source 串行，互挤限制见类注释）
        if (TryAdvertiseStore(_crystalStore, ResourceType.Crystal, threshold, out task)) return true;
        if (TryAdvertiseStore(_fireOilStore, ResourceType.FireOil, threshold, out task)) return true;
        if (TryAdvertiseStore(_oreStore, ResourceType.Ore, threshold, out task)) return true;   // T1.4（D609）：矿石伴生搬运
        return false;
    }

    /// <summary>在岗判定（D704 §三-3-②/⑤）：本组件为任务源 ⇒ 读 `HasWorkerAssigned(本组件)`（任意任务类型 Working 均算在场）。</summary>
    bool HasWorkerOnDuty => TaskScheduler.HasInstance && TaskScheduler.Instance.HasWorkerAssigned(this);

    bool TryAdvertiseStore(StorageComponent store, ResourceType type, float threshold, out KingdomTask task)
    {
        task = null;
        if (store == null) return false;
        int stored = store.GetAmount(type);
        if (stored <= 0) return false;
        if (store.capacity <= 0 || stored < store.capacity * threshold) return false;
        task = new KingdomTask(KingdomTaskType.Transport, this);
        // destPos=归属国主城门口可走格（SpecificBuilding 类型派发侧不覆盖 destPos）。禁用 NearestWarehouse：
        // 其解析落 Vault 子仓物理中心=主城占格中心（isObstacle，AI 城另有围墙环）→工人永不可达=搬运死循环（HH.107 十轮实证）。
        // 卸货仍按 UnloadInventory 就近同国仓/台账分流，destPos 只需可达且近仓库。
        task.destType = KingdomDestType.SpecificBuilding;
        task.destPos = TreasuryGatePos();
        task.args = new ScaleTaskArgs
        {
            resourceType = type,
            totalResourceDemand = stored
        };
        return true;
    }

    /// <summary>归属国主城旁可走格世界坐标（卸货集合点；取不到时回退本矿位置）。</summary>
    Vector2 TreasuryGatePos()
    {
        int kid = _building != null ? _building.kingdomId : 0;
        Building castle = null;
        var buildings = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.All : null;
        if (buildings != null)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b == null || b.kingdomId != kid || b.def == null || b.def.id != "castle") continue;
                castle = b;
                break;
            }
        }
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        var grid = GridSystem.Instance;
        if (castle != null && map != null && grid != null && grid.Config != null)
        {
            var gate = MapGenRules.NearestWalkable(map, castle.coord.x + 1, castle.coord.y);
            return grid.CoordToWorld(new GridCoord(gate.x, gate.y));
        }
        return SourcePos;
    }

    // ============================================================================
    //  ⭐【HH.341 小源③】新任务协议生产接缝（并存链 · 旧 KingdomTask(Production/Transport) 链不动）
    //   权威 ＝ `TaskBindingManager` 配对/预定；`_npcTaskMap` 仅作逐次对拍镜像。
    //   形态 ＝ **一源多卡**（`_slots`）：旧链 Production（独占）＋ Transport（规模派工 `D95`）都经
    //     调度器真实派发 ⇒ **一派一卡**（卡按 `taskId` 聚合；失败回待派可重派 · 上限 2 ＝ 初次 1 ＋ 重试 1）；
    //     · 一趟正常完成（卸货成功 / 装载失败兜底 `Complete` / Production 工作时长结束 `:585→:648`）⇒
    //       收敛 `Done`（配对与预定均释放）；
    //     · 失败（超时/不可达/死亡/卸货失败）且源仍可用 ⇒ 回待派计一次（`retryConsumedCount` 随卡）；
    //       达上限 ⇒ `Aborted`；
    //     · 源失效（建筑死亡/废弃/注销）⇒ **全部**非终态卡封口 `TargetRemoved`（⛔ 无在途豁免 —— 旧链
    //       `TaskScheduler.UpdateAssignedTasks` 的箱源在途例外仅 `ChestEntity` 享有，本源在途任务照旧
    //       被旧链放弃 ⇒ 协议侧同收口）；
    //     · 组件销毁（`OnDestroy`）⇒ 兜底封口 ⇒ ⛔ 不留预定残留。
    //   ⚠️ 卡不区分 Production/Transport 类型（接缝签名同型限制 · `D920`）：类型 ground truth ＝ 旧链
    //     `KingdomTask.type` 逐次对拍（探针读 `_npcTaskMap`），卡池只承载配对/预定生命周期。
    // ============================================================================

    /// <summary>一次派发的协议槽（一工人一派一卡 · 卡引用只住本源）。</summary>
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
        Debug.LogError($"[MineByproduct] 新协议接缝异常（{what}）owner={(_building != null && _building.def != null ? _building.def.id : "?")}"
                       + " kingdom=" + (_building != null ? _building.kingdomId : -99)
                       + "：" + (ex != null ? ex.ToString() : "见上一步返回码"));
    }

    /// <summary>建卡并提交（统一经 `TaskProtocolIssuer` · 每派一卡）。
    /// 归属 ＝ 父建筑 `kingdomId`（对齐 `SourceKingdom` Component 分支路由）；`deadline=+∞` ⇒ 显式载体记 `hasDeadline=false`。</summary>
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
                "MineByproductComponent_Task",
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

    /// <summary>接缝 B：调度器侧到达（到岗/取货段）⇒ `Assigned→Executing`（一源多卡 ⇒ 带 workerId 定位卡）。</summary>
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
    /// （⛔ 无在途豁免：旧链箱源在途例外仅 `ChestEntity` 享有，本源在途任务照旧被旧链放弃 ⇒ 协议侧同收口）。</summary>
    public void OnProtocolSourceInvalidated(long tick)
    {
        if (_sourceInvalidated) return;
        _sourceInvalidated = true;
        for (int i = _slots.Count - 1; i >= 0; i--)
            FinalizeSlot(_slots[i], TaskAbortReason.TargetRemoved, "源失效");
    }

    /// <summary>接缝 E：一趟任务正常完成（调度器 `Complete` 后）⇒ 收敛 `Done` 并释放（配对/预定归零）。
    /// Production 与 Transport 同口（Q3 分层：到岗＝接缝 B、真实产出＝`ProductionSystem→Tick()`、
    /// 此处＝调度器工作时长结束后的 `Complete` 收口）。</summary>
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
        SealAllOnDestroy();   // ⭐【HH.341 小源③】先置失效＋收口协议卡（随后 Unregister 触发的旧链放弃经接缝 C 直接封口）
        if (_registered && TaskScheduler.HasInstance)
            TaskScheduler.Instance.Unregister(this);
        if (_crystalStore != null) WarehouseRegistry.Unregister(_crystalStore);
        if (_fireOilStore != null) WarehouseRegistry.Unregister(_fireOilStore);
        if (_oreStore != null) WarehouseRegistry.Unregister(_oreStore);   // T1.4（D609）：矿石子仓同清
        _crystalStore = null;
        _fireOilStore = null;
        _oreStore = null;
    }
}
