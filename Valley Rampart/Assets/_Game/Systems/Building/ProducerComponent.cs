using UnityEngine;

/// <summary>
/// 产能组件（3.3.4 批次5）。每秒按 rate × gradeScale 产出资源写入本地 StorageComponent。
/// 由 ProductionSystem 集中调度（每秒遍历），不自己 Update。
/// 仅在 Building.state==Active 时产出；满载停产出。
/// </summary>
public class ProducerComponent : MonoBehaviour, IBuildingComponent
{
    private Building _building;
    private StorageComponent _storage;
    private float _rate;
    private ResourceType _resourceType;

    public void Init(Building building)
    {
        _building = building;
        if (building == null || building.def == null) return;
        _rate = building.def.producer.rate * building.def.GetGradeScale(building.grade);
        _resourceType = building.def.outputResource;
        _storage = building.GetComponent<StorageComponent>();
    }

    /// <summary>每秒 tick（由 ProductionSystem 调用）。</summary>
    public void Tick()
    {
        if (_building == null || !_building.IsActive) return;
        if (_storage == null || _storage.IsFull) return;
        int produce = Mathf.RoundToInt(_rate);
        if (produce <= 0) return;
        _storage.storedAmount = Mathf.Min(_storage.capacity, _storage.storedAmount + produce);
    }
}
