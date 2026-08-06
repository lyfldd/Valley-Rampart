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

    public void Init(Building building)
    {
        if (building == null || building.def == null) return;
        resourceType = building.def.outputResource;
        capacity = building.def.producer.capacity > 0 ? building.def.producer.capacity : 100;
    }

    public bool IsFull => storedAmount >= capacity;

    public bool IsReadyToHarvest() => storedAmount > 0;

    public int Harvest()
    {
        int amount = storedAmount;
        storedAmount = 0;
        if (amount > 0)
            RulerController.Instance?.ModifyResource(resourceType, true, amount);
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
        return amount;
    }
}
