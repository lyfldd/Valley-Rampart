using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 国库仓库（2_12 步骤8.4 / HH.16 裁决 B：多仓库聚合，勿改 IWarehouse 单资源契约）。
/// 主城每纳管资源挂一个子物体 StorageComponent（禁单组件多字典变体），容量 = BaseCapacity × LevelScale() 随主城等级；
/// 超容 clamp（满则拒收），不做溢出箱子（D221~D223 归步骤11）。
///
/// 非金资源真源 = 这些子仓库；金(Gold)=货币直通保留 RulerController（HH.8）。
/// 落地后 RulerController 的非金写路径即时转发本国库（禁双写红线 HH.8 的物理落点）。
/// 子仓库在创建时 WarehouseRegistry.Register（复用阶段①注册表），凑单路径自动并入。
/// </summary>
public class TreasureVault : MonoBehaviour, IBuildingComponent
{
    /// <summary>国库纳管的非金实体资源（金直通不纳入）。</summary>
    static readonly ResourceType[] Managed =
    {
        ResourceType.Stone, ResourceType.Wood, ResourceType.Food,
        ResourceType.SpecialFood, ResourceType.Meat, ResourceType.Metal
    };

    /// <summary>全局访问（主城装配后可用；仅一处）。</summary>
    public static TreasureVault Instance { get; private set; }

    public Building Castle { get; private set; }

    /// <summary>每资源对应的子仓库（缓存引用，节后再按需）。</summary>
    readonly Dictionary<ResourceType, StorageComponent> _vaults =
        new Dictionary<ResourceType, StorageComponent>();

    /// <summary>基础容量（主城 def.producer.capacity；0 则回退 250）。</summary>
    public int BaseCapacity { get; private set; } = 250;

    public void Init(Building building)
    {
        if (building == null) return;
        Castle = building;
        Instance = this;

        var def = building.def;
        if (def != null && def.producer.capacity > 0) BaseCapacity = def.producer.capacity;

        for (int i = 0; i < Managed.Length; i++)
        {
            var type = Managed[i];
            var go = new GameObject("Vault_" + type);
            go.transform.SetParent(building.transform, false);
            var sc = go.AddComponent<StorageComponent>();
            sc.resourceType = type;
            sc.capacity = Capacity();
            // 不调 StorageComponent.Init（否则 def.outputResource 覆盖类型）；手动注册以并入凑单
            WarehouseRegistry.Register(sc);
            _vaults[type] = sc;
        }
        Debug.Log($"[TreasureVault] 国库就绪：主城创建 {_vaults.Count} 个单资源仓库，BaseCapacity={BaseCapacity}");

        // 读档时序：国库晚于 RulerController/KingdomManager(Global) 初始化 →
        // ① 从 KingdomManager 读档缓存恢复国库真源（含铁，修正2）；② 冲刷 Ruler 旧档非金迁移缓存（防回退）。
        var km = KingdomManager.Instance;
        if (km != null)
        {
            Deposit(ResourceType.Stone, km.TreasuryStone);
            Deposit(ResourceType.Wood, km.TreasuryWood);
            Deposit(ResourceType.Food, km.TreasuryFood);
            Deposit(ResourceType.SpecialFood, km.TreasurySpecialFood);
            Deposit(ResourceType.Meat, km.TreasuryMeat);
            Deposit(ResourceType.Metal, km.TreasuryMetal);
        }
        if (RulerController.Instance != null) RulerController.Instance.EnsureTreasuryMigration();
    }

    /// <summary>主城等级/容量刷新时重设各子仓库容量（对齐"国库随主城升级"）。</summary>
    public void RefreshCapacity()
    {
        int cap = Capacity();
        foreach (var kv in _vaults) kv.Value.capacity = cap;
    }

    int Capacity()
    {
        float scale = Castle != null ? Castle.LevelScale() : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(BaseCapacity * scale));
    }

    // ===== 供 RulerController 中转的非金资源读写（金走 Ruler 直通不调用本类）=====

    /// <summary>某资源存量（国库纳管则读子仓库；未纳管返回 0）。</summary>
    public int GetAmount(ResourceType type)
        => _vaults.TryGetValue(type, out var s) ? s.storedAmount : 0;

    /// <summary>
    /// 入国库（步骤11 堵溢出黑洞，D222/D223"溢出装箱"）。先装库内容量，超容部分**装箱落主城格**（杜绝静默丢资源）。
    /// 返回实际入库量；装箱超额部分不走返回值（已落箱，不丢）。
    /// </summary>
    public int Deposit(ResourceType type, int amt)
    {
        if (!_vaults.TryGetValue(type, out var s)) return 0;
        int added = s.Add(amt);
        int overflow = amt - added;
        if (overflow > 0) SpillToChest(type, overflow);   // 国库满 → 溢出装箱（D222/D223）
        return added;
    }

    /// <summary>国库满溢（或未纳管资源）→ 超额装箱落主城格，防资源静默丢失（步骤11 堵 ModifyResource 黑洞）。</summary>
    private void SpillToChest(ResourceType type, int amount)
    {
        if (amount <= 0 || ChestManager.HasInstance == false) return;
        var pack = new ResourcePack();
        switch (type)
        {
            case ResourceType.Stone: pack.stone = amount; break;
            case ResourceType.Wood: pack.wood = amount; break;
            case ResourceType.Food: pack.food = amount; break;
            case ResourceType.SpecialFood: pack.food = amount; break;
            case ResourceType.Meat: pack.food = amount; break;
            case ResourceType.Metal: pack.metal = amount; break;
            default: return; // 弹药不走国库（HH.19 口径2），其余类型无装箱语义
        }
        var cell = Castle != null && GridSystem.Instance != null
            ? GridSystem.Instance.WorldToCoord(Castle.transform.position).GetValueOrDefault()
            : new GridCoord(0, 0);
        ChestManager.Instance.SpawnChest(cell, pack, Faction.PlayerCamp);
        Debug.Log($"[TreasureVault] 国库满 {type} 溢出 {amount} → 装箱落主城格 ({cell.x},{cell.y})（D223 不丢资源）");
    }

    /// <summary>出国库（≤存量），返回实际取走量。</summary>
    public int Take(ResourceType type, int amt)
        => _vaults.TryGetValue(type, out var s) ? s.TakeOut(amt) : 0;

    public void ResetAll()
    {
        foreach (var kv in _vaults) kv.Value.storedAmount = 0;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var kv in _vaults)
        {
            if (kv.Value != null) WarehouseRegistry.Unregister(kv.Value);
        }
        _vaults.Clear();
    }
}