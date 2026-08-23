using UnityEngine;

/// <summary>
/// 铁匠铺（2_12 步骤8，D199~D201）。石→Metal 就地加工。
/// Metal 为实体资源（可搬运/装箱/存储/交易），非加工中间物、非货币（D199）。
/// 生产公式（D200）：消耗石 → 产出 Metal，转化率 SO 可配（2 石 → 1 Metal 占位）。
/// 由 ProductionSystem 逐秒调度（与 ProducerComponent 并列，黑工铺不挂通用 Producer），
/// 每 tick 按 def.producer.rate 累积 Metal，达整数后用 StorageComponent.Transform 就地加工（D51）。
/// </summary>
public class BlacksmithBuilding : MonoBehaviour, IBuildingComponent
{
    private Building _building;
    private StorageComponent _storage;
    private BlacksmithDef _def;
    private float _rate;
    private float _metalAccumulator;

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

    /// <summary>逐秒 tick（ProductionSystem 调）。就地加工：石→Metal（StorageComponent.Transform，D200）。</summary>
    public void Tick()
    {
        if (_building == null || !_building.IsActive) return;
        if (_storage == null || _def == null || _storage.IsFull) return;
        if (_rate <= 0f) return;

        _metalAccumulator += _rate;
        int metal = Mathf.FloorToInt(_metalAccumulator);
        if (metal <= 0) return;   // 低速率：未攒够整数 Metal 不加工（对齐金矿累计器，避免刷屏）

        int stoneNeeded = metal * _def.stoneToMetalRatio;
        int produced = _storage.Transform(ResourceType.Stone, ResourceType.Metal, stoneNeeded);
        if (produced > 0)
            _metalAccumulator -= produced;   // 实际产出扣累计器；石不足/容量不足时整批不产，累计器保留待就绪
    }
}