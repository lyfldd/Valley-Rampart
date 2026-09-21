using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 国库仓库（2_12 步骤8.4；⭐ `M1-A`／`09#38` 重塑）。
///
/// ⭐ **本片变化**：
///   ① **9 个子仓 ⇒ 1 个多资源容器**（`09` §5.3 表：`TreasureVault`「挂 9 个子仓」⇒「**塌成 1 个容器**」）；
///      容器**收什么**由**标签**表达 ＝ `res_material` ＋ `res_food`（＝原 `Managed` 9 项：石/木/矿/金属/水晶/火油 ＋ 粮/特食/肉），
///      ⛔ 不再是"每资源一个仓"（`09` §5.2 硬规则 3：不做单一资源仓）；
///   ② **`Instance` 改按国查**（`09` §4.3 A 组：原「最后创建者胜」多国互相覆盖）⇒ `Get(kingdomId)`；
///      `Instance` 保留为**玩家国（id=0）国库**的兼容读口（现有调用方全在玩家侧）。
///
/// ⚠️ **容器仍挂子物体**（不挂主城本体）：主城本体若带 `StorageComponent` 会被 `Building.TryAdvertiseTask` ③
/// 当作**搬运源**（国库内容被工人搬去普通仓）⇒ 保持"主城本体无仓"的结构不变。
/// ⇒ 国库内容**随建筑存档显式存取**（`BuildingSaveData.treasuryContents` · 判据 6）。
///
/// ⭐ **`M1-E`（`09#47`）已做**：金降为普通资源 ⇒ 本容器**扩收金**（`res_currency` 族 · 体积 0 ⇒ 不占容量）；
/// `RulerController.Gold` 字段退役 ⇒ 只读门面，真源即本容器（`09` §4.3 A 组 `B1~B3`）。
/// </summary>
public class TreasureVault : MonoBehaviour, IBuildingComponent
{
    /// <summary>国库声明（**收什么** · `09` §三）：材料族 ＋ 粮族 ＋ ⭐ 币族（`M1-E`）⇒ 与原 `Managed` 9 项逐项等价
    /// （Stone/Wood/Ore/Metal/Crystal/FireOil ＋ Food/SpecialFood/Meat）＋ 金（体积 0 ⇒ 不占容量）；
    /// ⛔ 不含弹药（HH.19 口径 2）。</summary>
    static readonly string[] VaultPaths = { "res_material", "res_food", "res_currency" };

    /// <summary>按国查（⭐ `09` §4.3 A：多国不再互相覆盖）。</summary>
    static readonly Dictionary<int, TreasureVault> _byKingdom = new Dictionary<int, TreasureVault>();

    /// <summary>玩家国（`kingdomId=0`）国库 —— 兼容旧单例读口（⛔ 不再"最后创建者胜"）。</summary>
    public static TreasureVault Instance => Get(0);

    /// <summary>某国国库（未就绪 ⇒ null）。</summary>
    public static TreasureVault Get(int kingdomId)
        => _byKingdom.TryGetValue(kingdomId, out var v) ? v : null;

    public Building Castle { get; private set; }

    /// <summary>所属王国 id（0=玩家 / &gt;0=AI）。</summary>
    public int KingdomId { get; private set; }

    /// <summary>基础容量（主城 def.producer.capacity；0 则回退 250）。</summary>
    public int BaseCapacity { get; private set; } = 250;

    /// <summary>国库的唯一容器（多资源）。</summary>
    private StorageComponent _container;

    public void Init(Building building)
    {
        if (building == null) return;
        Castle = building;
        KingdomId = building.kingdomId;
        _byKingdom[KingdomId] = this;   // ⭐ 按国查（同国重建覆盖为最新，跨国不再互踩）

        var def = building.def;
        if (def != null && def.producer.capacity > 0) BaseCapacity = def.producer.capacity;

        // 1 个容器（替换原 9 个子仓）：挂子物体，主城本体保持"无仓"（见类注释警告）。
        var go = new GameObject("Vault");
        go.transform.SetParent(building.transform, false);
        _container = go.AddComponent<StorageComponent>();
        _container.SetDeclaredPaths(VaultPaths);   // 收什么＝标签（09 §三）
        _container.capacity = Capacity();
        // 不调 StorageComponent.Init（否则会被 def.warehousePaths 覆盖本声明）；手动注册以并入凑单
        WarehouseRegistry.Register(_container);

        Debug.Log($"[TreasureVault] 国库就绪：k{KingdomId} 单容器（收 res_material + res_food + res_currency[金·体积0]），BaseCapacity={BaseCapacity}");
    }

    /// <summary>主城等级/容量刷新时重设容器容量（对齐"国库随主城升级"）。</summary>
    public void RefreshCapacity()
    {
        if (_container != null) _container.capacity = Capacity();
    }

    int Capacity()
    {
        float scale = Castle != null ? Castle.LevelScale() : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(BaseCapacity * scale));
    }

    // ===== 存档（随建筑存档显式存取 · 判据 6）=====

    /// <summary>国库内容快照（存档用）。</summary>
    public ResourceList Contents => _container != null ? _container.Contents : ResourceList.Empty;

    /// <summary>读档恢复国库内容。</summary>
    public void RestoreContents(ResourceList contents)
    {
        if (_container != null) _container.RestoreContents(contents);
    }

    // ===== 供 RulerController/KingdomState 中转的资源读写（⭐ M1-E 起含金 —— 金降普通资源，走本容器 CRUD）=====

    /// <summary>某资源存量（未声明/无容器 ⇒ 0）。</summary>
    public int GetAmount(ResourceType type)
        => _container != null ? _container.GetAmount(type) : 0;

    /// <summary>
    /// 入国库（步骤11 堵溢出黑洞，D222/D223"溢出装箱"）。先装库内容量，超容部分**装箱落主城格**（杜绝静默丢资源）。
    /// 返回实际入库量；装箱超额部分不走返回值（已落箱，不丢）。
    /// ⭐ `M1-A`：装箱改用「资源量列表」单条目承载 —— 原 8 桶结构下的**折损/无桶丢弃**（特食/肉按粮折算、
    /// 水晶/火油不入箱）**结构性消失**（`ResourcePack` 无桶所致，随类型退役 ⇒ 上报为结构强制的行为变化）。
    /// </summary>
    public int Deposit(ResourceType type, int amt)
    {
        if (_container == null) return 0;
        int added = _container.Add(type, amt);
        int overflow = amt - added;
        if (overflow > 0) SpillToChest(type, overflow);   // 国库满 → 溢出装箱（D222/D223）
        return added;
    }

    /// <summary>国库满溢 → 超额装箱落主城格，防资源静默丢失（步骤11 堵 ModifyResource 黑洞）。</summary>
    private void SpillToChest(ResourceType type, int amount)
    {
        if (amount <= 0 || ChestManager.HasInstance == false) return;
        var cell = Castle != null && GridSystem.Instance != null
            ? GridSystem.Instance.WorldToCoord(Castle.transform.position).GetValueOrDefault()
            : new GridCoord(0, 0);
        var pack = ResourceList.Of(new ResourceAmount(type, amount));
        ChestManager.Instance.SpawnChest(cell, pack);   // ⭐ `M1-D`/#57：去 faction 参数（箱无主 · `D802` `Q7`）
        Debug.Log($"[TreasureVault] 国库满 {type} 溢出 {amount} → 装箱落主城格 ({cell.x},{cell.y})（D223 不丢资源）");
    }

    /// <summary>出国库（≤存量），返回实际取走量。</summary>
    public int Take(ResourceType type, int amt)
        => _container != null ? _container.TakeOut(type, amt) : 0;

    public void ResetAll()
    {
        _container?.Clear();
    }

    void OnDestroy()
    {
        if (KingdomId >= 0)
        {
            if (_byKingdom.TryGetValue(KingdomId, out var v) && v == this) _byKingdom.Remove(KingdomId);
        }
        if (_container != null) WarehouseRegistry.Unregister(_container);
        _container = null;
    }
}
