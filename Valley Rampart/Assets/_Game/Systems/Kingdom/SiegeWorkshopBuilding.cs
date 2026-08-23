using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 投掷机厂专属组件（2_12 步骤9，D207~D212；HH.19 裁决 A×4）。
/// 弹药=可搬运资源（ResourceType.StoneAmmo/FireballAmmo/MagicAmmo），真源=本厂级弹药仓（3 个子 StorageComponent）+ 通用仓库建筑；
/// 不纳国库（GameEvents 前例同 Ore/Crystal/FireOil）。
///
/// 结构（仿 BlacksmithBuilding 专属组件 + TreasureVault 多子仓聚合）：
///   - 挂 3 个单资源子 StorageComponent（StoneAmmo/FireballAmmo/MagicAmmo），容量 = def.producer.capacity × LevelScale。
///   - 产丹：从国库扣原料（石→石弹 / 火油→火弹 / 水晶→魔弹，D128/D210，SO 成本取 SiegeProductionConfig），入对应子仓；
///     弹药品类切换（轮产/按需）由 Config 驱动（HH.19 口径4：执行细节保持 SO 驱动）。
///   - 由 ProductionSystem 逐秒调度（与 BlacksmithBuilding 并列，不挂通用 ProducerComponent）。
///
/// 退役（HH.19 裁决口径 2）：原本 SiegeProductionSystem._ammoStock 全局弹药账（ProjectileType 键）不再作为真源；
///   ProduceAmmo 改走本组件子仓；ResupplySiegeUnit 直填接口退役；旧档 ammoStock 读入时迁入本组件子仓（不丢档）。
/// </summary>
public class SiegeWorkshopBuilding : MonoBehaviour, IBuildingComponent
{
    /// <summary>厂内仓储的子物体前缀（仿 TreasureVault 的 Vault_ 命名）。</summary>
    const string SubStorePrefix = "Ammo_";

    private Building _building;
    private SiegeProductionConfig _config;
    private readonly Dictionary<ResourceType, StorageComponent> _stores =
        new Dictionary<ResourceType, StorageComponent>();

    // 左轮产：三弹种依次序（SO 驱动，交替产出分散原料压力；可按需改为配比）
    private readonly ResourceType[] _cycleOrder = { ResourceType.StoneAmmo, ResourceType.FireballAmmo, ResourceType.MagicAmmo };
    private int _cycleIndex = 0;
    private float _accumulator;

    /// <summary>是否弹药仓库就绪（3 子仓全部建立）。</summary>
    public bool IsReady => _stores.Count == 3;

    public void Init(Building building)
    {
        _building = building;
        _config = Resources.Load<SiegeProductionConfig>("Config/SiegeProductionConfig");
        CreateSubStores(building);
        // 旧档弹药账迁移（HH.19 裁决口径2）：厂仓就绪后消费 Global 迁移桥缓存并入，归零防重复。
        RestoreLegacyAmmo(SiegeProductionSystem.LegacyStoneAmmo,
            SiegeProductionSystem.LegacyFireballAmmo, SiegeProductionSystem.LegacyMagicAmmo);
        SiegeProductionSystem.LegacyStoneAmmo = 0;
        SiegeProductionSystem.LegacyFireballAmmo = 0;
        SiegeProductionSystem.LegacyMagicAmmo = 0;
    }

    /// <summary>创建 3 个单资源弹药子仓（仿 TreasureVault.Init 的子物体聚合，单仓不做多字典变体）。</summary>
    void CreateSubStores(Building building)
    {
        if (building == null) return;
        int capacity = Capacity();
        for (int i = 0; i < _cycleOrder.Length; i++)
        {
            var type = _cycleOrder[i];
            var go = new GameObject(SubStorePrefix + type);
            go.transform.SetParent(building.transform, false);
            var sc = go.AddComponent<StorageComponent>();
            sc.resourceType = type;
            sc.capacity = capacity;
            // 不调 StorageComponent.Init（避免 def.outputResource 覆盖类型）；手动注册以并入凑单/搬运
            WarehouseRegistry.Register(sc);
            _stores[type] = sc;
        }
        Debug.Log($"[SiegeWorkshop] 厂级弹药仓就绪：{_stores.Count} 个子仓，容量={capacity}（HH.19 A×4）");
        _accumulator = 0f;
    }

    /// <summary>弹仓容量 = def.producer.capacity × 等级缩放（对齐 ProducerComponent.RefreshRate/StorageComponent.RefreshCapacity）。</summary>
    public void RefreshCapacity()
    {
        int cap = Capacity();
        foreach (var kv in _stores) kv.Value.capacity = cap;
    }

    /// <summary>读档/升级后刷新容量（对齐 BlacksmithBuilding.RefreshRate）。</summary>
    public void RefreshRate()
    {
        RefreshCapacity();
        _accumulator = 0f;
    }

    int Capacity()
    {
        if (_building == null || _building.def == null) return 100;
        float scale = _building.LevelScale();
        return _building.def.producer.capacity > 0
            ? Mathf.Max(1, Mathf.RoundToInt(_building.def.producer.capacity * scale))
            : 100;
    }

    // ===== 产弹（D207/D210；ProductionSystem 逐秒调）=====

    /// <summary>当前产率（每秒，def.producer.rate × 等级缩放）。弹药为轮产，本值视为"单类弹药产率"。</summary>
    float RatePerSecond()
    {
        if (_building == null || _building.def == null) return 0f;
        return _building.def.producer.rate
               * _building.def.GetGradeScale(_building.grade)
               * _building.LevelScale();
    }

    /// <summary>逐秒产弹：扣原料（国库）→ 对应弹药入子仓。弹药不足/仓满则本 tick 跳过。</summary>
    public void Tick()
    {
        if (_building == null || !_building.IsActive) return;
        if (_stores.Count < 3) return;
        if (_config == null) return;

        float rate = RatePerSecond();
        if (rate <= 0f) return;

        _accumulator += rate;
        int amount = Mathf.FloorToInt(_accumulator);
        if (amount <= 0) return;   // 低速率：未攒够整数不发

        // 单 tick 只产一种（轮产，SO/config 驱动品类切换；amount 视为对当前品类的产量）
        ResourceType type = _cycleOrder[_cycleIndex];
        _cycleIndex = (_cycleIndex + 1) % _cycleOrder.Length;

        int produced = Produce(type, amount);
        if (produced > 0)
            _accumulator -= produced;   // 实际产出扣累计器；原料/容量不足则整批不产，累计器保留待就绪（对齐 BlacksmithBuilding）
    }

    /// <summary>外部产弹入口（原 SiegeProductionSystem.ProduceAmmo 迁移）：扣原料→入本仓。返回实际入仓量。</summary>
    public int Produce(ResourceType ammoType, int amt)
    {
        if (amt <= 0 || !_stores.ContainsKey(ammoType)) return 0;
        var store = _stores[ammoType];
        if (store.IsFull) return 0;

        int cost = AmmoCostFor(ammoType);
        ResourceType raw = RawTypeFor(ammoType);
        if (cost <= 0) return 0;
        int maxByRaw = AmountAvailable(raw) / cost;
        int produce = Mathf.Min(amt, maxByRaw, store.capacity - store.storedAmount);
        if (produce <= 0) return 0;

        SpendRaw(raw, produce * cost);
        int added = store.Add(produce);
        Debug.Log($"[SiegeWorkshop] 产 {ammoType} ×{added}（耗 {raw} {added * cost}）");
        return added;
    }

    /// <summary>原料可供应量（石/水晶/火油：国库或仓库，真源=StorageComponent，禁双写）。</summary>
    int AmountAvailable(ResourceType raw)
    {
        // 石/水晶/火油以王国仓库真源为准（国库 Stone / 仓库 Ore 类在存储）。金直接开外。
        var tv = TreasureVault.Instance;
        if (tv != null)
        {
            int t = tv.GetAmount(raw);
            if (t > 0) return t;
        }
        var ruler = RulerController.Instance;
        if (ruler != null) return ruler.GetResource(raw);
        return 0;
    }

    /// <summary>扣原料（真源剥一次，防双写：直接走 RulerController 公共入口即国库）。</summary>
    void SpendRaw(ResourceType raw, int amt)
    {
        if (amt <= 0) return;
        RulerController.Instance?.ModifyResource(raw, false, amt);
    }

    int AmmoCostFor(ResourceType ammoType)
    {
        switch (ammoType)
        {
            case ResourceType.FireballAmmo: return _config != null ? _config.fireballAmmoCost : 1;
            case ResourceType.MagicAmmo:    return _config != null ? _config.magicAmmoCost : 1;
            default:                        return _config != null ? _config.stoneAmmoCost : 1;
        }
    }

    ResourceType RawTypeFor(ResourceType ammoType)
    {
        switch (ammoType)
        {
            case ResourceType.FireballAmmo: return ResourceType.FireOil;
            case ResourceType.MagicAmmo:    return ResourceType.Crystal;
            default:                        return ResourceType.Stone;
        }
    }

    // ===== 弹仓查询/进出（对接 SiegeProductionSystem / 装填任务）=====

    /// <summary>某类弹药存量（厂仓顶部，供装填任务取）。</summary>
    public int GetAmmo(ResourceType ammoType)
        => _stores.TryGetValue(ammoType, out var s) ? s.storedAmount : 0;

    /// <summary>某类弹药可取量（≤存量，装填）。</summary>
    public int TakeAmmo(ResourceType ammoType, int amt)
        => _stores.TryGetValue(ammoType, out var s) ? s.TakeOut(amt) : 0;

    /// <summary>某类弹药的 IWarehouse（装卸/凑单用）。</summary>
    public StorageComponent GetStore(ResourceType ammoType)
        => _stores.TryGetValue(ammoType, out var s) ? s : null;

    public void ResetAll()
    {
        foreach (var kv in _stores) kv.Value.storedAmount = 0;
        _accumulator = 0f;
    }

    // ===== 旧档 ammoStock 迁移（HH.19 裁决口径 2：不丢档）=====

    /// <summary>把旧全局弹药账数值迁入本仓（旧档读数，迁移后清零）。</summary>
    public void RestoreLegacyAmmo(int stone, int fireball, int magic)
    {
        DepositDirect(ResourceType.StoneAmmo, stone);
        DepositDirect(ResourceType.FireballAmmo, fireball);
        DepositDirect(ResourceType.MagicAmmo, magic);
        if (stone + fireball + magic > 0)
            Debug.Log($"[SiegeWorkshop] 旧档弹药账迁入厂仓：石{stone}/火{fireball}/魔{magic}（不丢档）");
    }

    void DepositDirect(ResourceType ammoType, int amt)
    {
        if (amt <= 0 || !_stores.TryGetValue(ammoType, out var s)) return;
        int added = s.Add(amt);
        // 旧档存量可能超新容量，接受 clamp（不静默丢弃之外的上限提示）
        if (added < amt)
            Debug.LogWarning($"[SiegeWorkshop] 旧档弹药 {ammoType} 超容量 clamp {amt}→{added}");
    }

    void OnDestroy()
    {
        foreach (var kv in _stores)
        {
            if (kv.Value != null) WarehouseRegistry.Unregister(kv.Value);
        }
        _stores.Clear();
    }
}