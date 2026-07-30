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
}
