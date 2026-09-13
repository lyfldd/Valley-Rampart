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
public class BlacksmithBuilding : MonoBehaviour, IBuildingComponent, ITaskSource
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

    // ===== ITaskSource（D704 A 批：组件自持 Production 广告；源＝组件；`TreeGatherSource` 非 Building 任务源先例）=====

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
    }
}