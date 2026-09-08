using UnityEngine;

/// <summary>
/// 矿洞副产组件（DZ-072a，D562 / HH.107 件1）。
/// mine 双身份=A2 变体（已裁决策）：mine 保持 isResourceNode=1 石头采集点身份不动（WanderStimulusProvider/
/// GuardDeploymentSystem/Building 拆除守卫/BuildingPanel 全消费此 flag——M1 红线），另挂本组件恒产水晶/火油。
///
/// 结构（仿 SiegeWorkshopBuilding 厂级弹药仓）：
///   - 挂 2 个单资源子 StorageComponent（Crystal/FireOil），容量取 KingdomConfig.byproductCrystalCapacity/
///     byproductFireOilCapacity。不注册 WarehouseRegistry（子仓=待运出缓冲非可存仓，见 CreateSubStore 注）。
///   - 产率：KingdomConfig.byproductCrystalRate/byproductFireOilRate（0.05/s=慢产保稀缺；副产无等级门槛，
///     原 ProducerComponent Lv2/Lv3 门槛随 mine levels=[] 不适用——已裁决策）。
///   - 恒产：不设工人门（任务书「恒产」口径；石头采集链的工人派工与本组件互不相干）。
///   - 搬运：本组件实现 ITaskSource（TreeGatherSource 非 Building 任务源先例），子仓存量达
///     transportThreshold 时发 Transport（destType=NearestWarehouse；args 带子仓资源类型，
///     TaskScheduler.LoadInventoryFromSource 按 args 资源类型取子仓）。
///   - 由 ProductionSystem 逐秒调度（与 ProducerComponent/BlacksmithBuilding/SiegeWorkshopBuilding 并列）。
///
/// 已知限制（列报）：同矿水晶/火油两仓 Transport 广告按同 source 同 type 计数（CountAssignedForType），
/// 并发在派时互相挤占规模派工名额——效果为两仓错峰搬运（不断链），最小方案接受。
///
/// 存档：BuildingSaveData.byproductCrystalAmount/byproductFireOilAmount（尾插，旧档缺→默认 0 零 bump），
/// Building.SaveState/LoadState 经 SaveByproductState/RestoreByproductState 读写。
/// ResetState：随建筑 GameObject 销毁自然清（组件随建筑域既有清场链），无需 WorldLifecycle 新编排（列报确认）。
/// </summary>
public class MineByproductComponent : MonoBehaviour, IBuildingComponent, ITaskSource
{
    const string SubStorePrefix = "Byproduct_";

    private Building _building;
    private StorageComponent _crystalStore;
    private StorageComponent _fireOilStore;
    private float _crystalAccumulator;
    private float _fireOilAccumulator;
    private bool _crystalFullLogged;   // 满仓停产分频：满时只记一次，消耗后复位（防逐秒刷屏）
    private bool _fireOilFullLogged;
    private bool _registered;          // TaskScheduler 懒注册（调度器未就绪时跳过，首 Tick 补挂——TreeGatherSource 懒注册同语义）

    public void Init(Building building)
    {
        _building = building;
        _crystalAccumulator = 0f;
        _fireOilAccumulator = 0f;
        _crystalFullLogged = false;
        _fireOilFullLogged = false;
        _registered = false;
        CreateSubStores();
    }

    /// <summary>创建 2 个单资源副产子仓（仿 SiegeWorkshopBuilding.CreateSubStores/TreasureVault 子物体聚合）。</summary>
    void CreateSubStores()
    {
        if (_building == null) return;
        var config = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        _crystalStore = CreateSubStore(ResourceType.Crystal, config != null ? config.byproductCrystalCapacity : 20);
        _fireOilStore = CreateSubStore(ResourceType.FireOil, config != null ? config.byproductFireOilCapacity : 20);
        string defId = _building.def != null ? _building.def.id : "?";
        Debug.Log("[MineByproduct] 副产仓就绪（" + defId + "）：水晶仓 cap=" + _crystalStore.capacity + "，火油仓 cap=" + _fireOilStore.capacity);
    }

    StorageComponent CreateSubStore(ResourceType type, int capacity)
    {
        var go = new GameObject(SubStorePrefix + type);
        go.transform.SetParent(_building.transform, false);
        var sc = go.AddComponent<StorageComponent>();
        sc.resourceType = type;
        sc.capacity = Mathf.Max(1, capacity);
        // 不调 StorageComponent.Init（避免 def.outputResource=Stone 覆盖类型）。
        // 不注册 WarehouseRegistry（DZ-072a 语义：副产子仓=待运出缓冲，非可存仓——若注册，
        // AI 卸货 FindNearestAvailable 会就近卸回本仓死循环，AI 台账永不得水晶；不注册则
        // AI 走 AddGatherOverflow 台账兜底、玩家直卸国库 Vault_Crystal，P2/P5 落库链稳定通）。
        return sc;
    }

    /// <summary>每秒 tick（ProductionSystem 调度）：水晶/火油双槽并行恒产，独立容量互不挤占。</summary>
    public void Tick()
    {
        if (_building == null || !_building.IsActive) return;
        LazyRegister();
        var config = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        TickStore(_crystalStore, config != null ? config.byproductCrystalRate : 0.05f, ref _crystalAccumulator, ref _crystalFullLogged);
        TickStore(_fireOilStore, config != null ? config.byproductFireOilRate : 0.05f, ref _fireOilAccumulator, ref _fireOilFullLogged);
    }

    void TickStore(StorageComponent store, float rate, ref float accumulator, ref bool fullLogged)
    {
        if (store == null || rate <= 0f) return;
        if (store.IsFull)
        {
            if (!fullLogged)   // 分频：满仓停产只记一次，防逐秒刷屏（对齐任务书口径）
            {
                fullLogged = true;
                Debug.Log($"[MineByproduct] {store.resourceType} 副产仓满（{store.storedAmount}/{store.capacity}）停产，等待搬运");
            }
            return;
        }
        fullLogged = false;
        accumulator += rate;
        int amount = Mathf.FloorToInt(accumulator);
        if (amount <= 0) return;   // 低速率：未攒够整数不产（对齐 SiegeWorkshopBuilding 累计器口径）
        int added = store.Add(amount);
        if (added > 0)
        {
            accumulator -= added;   // 实际入仓扣累计器（仓满中途截断时余量保留待下轮）
            Debug.Log($"[MineByproduct] 产 {store.resourceType} ×{added}（存量 {store.storedAmount}/{store.capacity}）");
        }
    }

    /// <summary>按资源类型取副产子仓（TaskScheduler.LoadInventoryFromSource 按 args 资源类型取货用；仿 SiegeWorkshopBuilding.GetStore）。</summary>
    public StorageComponent GetStore(ResourceType type)
    {
        if (type == ResourceType.Crystal) return _crystalStore;
        if (type == ResourceType.FireOil) return _fireOilStore;
        return null;
    }

    // ===== 存档（Building.SaveState/LoadState 调；BuildingSaveData 尾插字段）=====

    /// <summary>保存副产子仓存量（水晶量, 火油量）。</summary>
    public (int crystal, int fireOil) SaveByproductState()
        => (_crystalStore != null ? _crystalStore.storedAmount : 0,
            _fireOilStore != null ? _fireOilStore.storedAmount : 0);

    /// <summary>读档恢复副产子仓存量（超容量 clamp 不静默丢——对齐 SiegeWorkshopBuilding.RestoreLegacyAmmo 口径）。</summary>
    public void RestoreByproductState(int crystal, int fireOil)
    {
        if (crystal > 0 && _crystalStore != null)
        {
            int added = _crystalStore.Add(crystal);
            if (added < crystal)
                Debug.LogWarning($"[MineByproduct] 读档水晶超容量 clamp {crystal}→{added}");
        }
        if (fireOil > 0 && _fireOilStore != null)
        {
            int added = _fireOilStore.Add(fireOil);
            if (added < fireOil)
                Debug.LogWarning($"[MineByproduct] 读档火油超容量 clamp {fireOil}→{added}");
        }
    }

    // ===== ITaskSource（搬运广告；TreeGatherSource 非 Building 任务源先例）=====

    public bool IsValid => this != null && _building != null && _building.IsValid;

    public Vector2 SourcePos => _building != null ? (Vector2)_building.transform.position : Vector2.zero;

    public void OnRegister() { }
    public void OnUnregister() { }

    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        if (_building == null || !_building.IsValid) return false;
        float threshold = _building.transportThreshold;
        // 两仓分别判达标（存量≥capacity×threshold），一次只发先达标的一个（同 source 串行，互挤限制见类注释）
        if (TryAdvertiseStore(_crystalStore, threshold, out task)) return true;
        if (TryAdvertiseStore(_fireOilStore, threshold, out task)) return true;
        return false;
    }

    bool TryAdvertiseStore(StorageComponent store, float threshold, out KingdomTask task)
    {
        task = null;
        if (store == null || store.storedAmount <= 0) return false;
        if (store.capacity <= 0 || store.storedAmount < store.capacity * threshold) return false;
        task = new KingdomTask(KingdomTaskType.Transport, this);
        // destPos=归属国主城门口可走格（SpecificBuilding 类型派发侧不覆盖 destPos）。禁用 NearestWarehouse：
        // 其解析落 Vault 子仓物理中心=主城占格中心（isObstacle，AI 城另有围墙环）→工人永不可达=搬运死循环（HH.107 十轮实证）。
        // 卸货仍按 UnloadInventory 就近同国仓/台账分流，destPos 只需可达且近仓库。
        task.destType = KingdomDestType.SpecificBuilding;
        task.destPos = TreasuryGatePos();
        task.args = new ScaleTaskArgs
        {
            resourceType = store.resourceType,
            totalResourceDemand = store.storedAmount
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

    void LazyRegister()
    {
        if (_registered || !TaskScheduler.HasInstance) return;
        TaskScheduler.Instance.Register(this);
        _registered = true;
    }

    void OnDestroy()
    {
        if (_registered && TaskScheduler.HasInstance)
            TaskScheduler.Instance.Unregister(this);
        if (_crystalStore != null) WarehouseRegistry.Unregister(_crystalStore);
        if (_fireOilStore != null) WarehouseRegistry.Unregister(_fireOilStore);
        _crystalStore = null;
        _fireOilStore = null;
    }
}
