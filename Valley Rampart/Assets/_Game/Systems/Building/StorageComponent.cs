using UnityEngine;

/// <summary>
/// 存储组件（3.3.4 批次5）。产能建筑的本地存储，满载停产出。
/// 实现 IHarvestable：玩家手动收取或未来工人搬运，资源转入国库。
/// 存档只需存 storedAmount（ProducerComponent 每秒重算无需存）。
/// </summary>
public class StorageComponent : MonoBehaviour, IBuildingComponent, IHarvestable
{
    public ResourceType resourceType;
    public int storedAmount;
    public int capacity = 100;

    /// <summary>存储变化事件（QQQ.2 §需求7 / DR-15：WarehousePanel 订阅实时刷新）。关闭退订避免泄漏。</summary>
    public event System.Action<StorageComponent> OnStorageChanged;

    public void Init(Building building)
    {
        if (building == null || building.def == null) return;
        resourceType = building.def.outputResource;
        RefreshCapacity();
    }

    /// <summary>
    /// 刷新存储容量：def.producer.capacity × 等级缩放（3.5.4 数据卡：仓库/粮仓 Lv2/Lv3 容量↑）。
    /// 建造/读档/升级后调用。
    /// </summary>
    public void RefreshCapacity()
    {
        var def = GetComponent<Building>() != null ? GetComponent<Building>().def : null;
        if (def == null) return;
        var b = GetComponent<Building>();
        capacity = def.producer.capacity > 0
            ? Mathf.Max(1, Mathf.RoundToInt(def.producer.capacity * b.LevelScale()))
            : 100;
    }

    public bool IsFull => storedAmount >= capacity;

    /// <summary>
    /// 仓库入货（QQQ.4 T12：工人背包卸货入口）。返回实际入货量；容量满拒绝（返回 0），调用方兜底入国库。
    /// </summary>
    public int Add(int amount)
    {
        if (amount <= 0) return 0;
        int room = capacity - storedAmount;
        if (room <= 0) return 0;
        int added = Mathf.Min(amount, room);
        storedAmount += added;
        OnStorageChanged?.Invoke(this);
        return added;
    }

    /// <summary>
    /// 从存储取走（≤ amount），返回实际取走量。工人搬运第一段入口（QQQ.4 T11：
    /// 背包已入货后从建筑扣减，保证资源不丢）。内部触发 OnStorageChanged 通知 UI 刷新。
    /// </summary>
    public int TakeOut(int amount)
    {
        if (amount <= 0) return 0;
        int taken = Mathf.Min(amount, storedAmount);
        if (taken <= 0) return 0;
        storedAmount -= taken;
        OnStorageChanged?.Invoke(this);
        return taken;
    }

    public bool IsReadyToHarvest() => storedAmount > 0;

    public int Harvest()
    {
        int amount = storedAmount;
        storedAmount = 0;
        if (amount > 0)
            RulerController.Instance?.ModifyResource(resourceType, true, amount);
        OnStorageChanged?.Invoke(this);
        return amount;
    }

    // ===== 搬运携带量（3.5.3 §3.1 / 3.5 前置缺口 §2.2；P1-8）=====

    private static ResourceCarryConfig _carryConfig;

    /// <summary>按本存储资源类型查一次搬运携带量（ResourceCarryConfig SO，数据驱动）。</summary>
    public int GetCarryAmount()
    {
        if (_carryConfig == null)
            _carryConfig = Resources.Load<ResourceCarryConfig>("Config/ResourceCarryConfig");
        return _carryConfig != null ? _carryConfig.GetCarryAmount(resourceType) : 10;
    }

    /// <summary>
    /// 搬运一次（≤携带量）入国库，返回实际搬走量；剩余留待下轮（分批搬运）。
    /// 供 BehaviorExecutor 搬运闭环替代全量 Harvest()：源建筑产出 &gt; 携带量时分批多次搬运。
    /// </summary>
    public int HarvestCarry()
    {
        int max = Mathf.Max(1, GetCarryAmount());
        int amount = Mathf.Min(storedAmount, max);
        if (amount <= 0) return 0;
        storedAmount -= amount;
        RulerController.Instance?.ModifyResource(resourceType, true, amount);
        OnStorageChanged?.Invoke(this);
        return amount;
    }
}
