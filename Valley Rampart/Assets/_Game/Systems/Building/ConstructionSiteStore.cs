using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 工地仓（`09_资源与仓库.md` §16.1 ② · ⭐ `M1-C` 件1 · 裁决 1 **案 A**）。
///
/// 职责：**建造／升级／修复的投料容器** —— 工人把材料搬进来，**够阈值即停（⛔ 不多搬）**，
/// 料齐 ⇒ 通知 `Building` 开工（`09` §16.1 ①~③）。
///
/// ⛔ **不复用 `StorageComponent`**（裁决 1 加严）：工地仓与产出仓**结构性隔离** ——
///   · ⛔ **不入 `WarehouseRegistry`**（不作为凑单／卸货落点，防"工地料被别的工人搬去普通仓"）；
///   · ⛔ **不被 `Building.TryAdvertiseTask` 当搬运源**（完工后材料**转成建筑本体** · `09` §16.1-3）；
///   · ⛔ **无「仓库声明标签」**（收什么由**本次配方**决定，不是标签前缀匹配）；
///   · 容量 ＝ **配方量本身**（阈值拦截保证不超 ⇒ 无需独立容量线 · `09` §16.1-2）。
///
/// 任务源：本组件实现 `ITaskSource`，广告「**搬料任务**」
/// （`KingdomTaskType.Build` ＋ `HaulToSiteArgs` —— ⛔ **不新增枚举** · 裁决收口 1）。
/// `SourcePos` ＝ **取料仓位置**（每次广告时解析）⇒ 工人走「取料仓 → 工地」**两段位移**，
/// 与 `Transport` 同形（`LoadInventoryFromSource`／`UnloadInventory` 的两段式）。
///
/// ⚠️ 生命周期：由 `Building` 在「投料态」创建并注册到 `TaskScheduler`；料齐／死亡即注销。
/// </summary>
public class ConstructionSiteStore : MonoBehaviour, ITaskSource
{
    /// <summary>资源表序（确定性：缺口资源按表序取第一个 · 与调度排序的确定性红线同源）。</summary>
    private static readonly ResourceType[] TableOrder = BuildOrder();

    private static ResourceType[] BuildOrder()
    {
        var list = new List<ResourceType>(16);
        foreach (var t in ResourceCatalog.AllTypes) list.Add(t);
        return list.ToArray();
    }

    /// <summary>本次投料需求（配方量 ＝ 阈值 · `09` §16.1-2）。</summary>
    private readonly Dictionary<ResourceType, int> _need = new Dictionary<ResourceType, int>();

    /// <summary>已到料（≤ 需求）。</summary>
    private readonly Dictionary<ResourceType, int> _items = new Dictionary<ResourceType, int>();

    private Building _building;
    private Vector2 _pickupPos;

    /// <summary>所属建筑（工地）。</summary>
    public Building Owner => _building;

    /// <summary>初始化（由 `Building` 创建时调用）。</summary>
    public void Init(Building building, ResourceList need)
    {
        _building = building;
        SetNeed(need);
    }

    /// <summary>设定需求（＝本次配方；金已在下单时直扣 ⇒ 需求里**不含金** · 裁决 2 金-A）。</summary>
    public void SetNeed(ResourceList need)
    {
        _need.Clear();
        if (need.items != null)
        {
            for (int i = 0; i < need.items.Length; i++)
            {
                var e = need.items[i];
                if (e.amount > 0) _need[e.type] = e.amount;
            }
        }
    }

    // ===== 读口 =====

    /// <summary>该资源的需求量（阈值）。</summary>
    public int NeedOf(ResourceType type) => _need.TryGetValue(type, out var v) ? v : 0;

    /// <summary>该资源已到料量。</summary>
    public int GetAmount(ResourceType type) => _items.TryGetValue(type, out var v) ? v : 0;

    /// <summary>该资源还缺多少（＝阈值拦截的上限）。</summary>
    public int Remaining(ResourceType type) => Mathf.Max(0, NeedOf(type) - GetAmount(type));

    /// <summary>全部需求是否已满足（料齐 ⇒ 可开工 · `09` §16.1 ③）。</summary>
    public bool IsSatisfied
    {
        get
        {
            foreach (var kv in _need)
                if (Remaining(kv.Key) > 0) return false;
            return true;
        }
    }

    /// <summary>还缺的件数合计（0 ＝ 料齐）。</summary>
    public int TotalRemaining
    {
        get
        {
            int sum = 0;
            foreach (var kv in _need) sum += Remaining(kv.Key);
            return sum;
        }
    }

    /// <summary>已到料的件数合计（＝`Building.totalInvested` 的累加源）。</summary>
    public int TotalCount
    {
        get
        {
            int sum = 0;
            foreach (var kv in _items) sum += kv.Value;
            return sum;
        }
    }

    /// <summary>当前内容（存档／掉箱用 · `ResourceList` 同形）。</summary>
    public ResourceList Contents
    {
        get
        {
            var r = ResourceList.Empty;
            foreach (var kv in _items) if (kv.Value > 0) r = r.Set(kv.Key, kv.Value);
            return r;
        }
    }

    // ===== 写口（⭐ 阈值拦截）=====

    /// <summary>
    /// ⭐ **阈值拦截**（`09` §16.1-2）：只收「配方量 − 已到料量」为上限，⛔ **不多收**。
    /// 返回**实际接受量**（调用方据此把余量退回/留存）。到料量变化即累加进 `Building.totalInvested`。
    /// </summary>
    public int Deposit(ResourceType type, int amount)
    {
        if (amount <= 0) return 0;
        // ⭐ 工地已关（料齐后 `OnSiteMaterialsReady` 已清账／拆除中／已死亡）⇒ **拒收**。
        //   ⛔ 无此守卫时：料齐 ⇒ `Clear()` 清账 ⇒ `Remaining` 复归配方量 ⇒ 在途工人再卸一笔会被
        //   静默吞掉并**重复累加 `totalInvested`**（拆除全退会多退）。M1-C 冒烟 §C 实测暴露。
        if (_building != null && !_building.IsSiteAwaitingMaterials) return 0;
        int rem = Remaining(type);
        if (rem <= 0) return 0;                 // 已够阈值 ⇒ 拒收（⛔ 不多搬）
        int accept = Mathf.Min(amount, rem);
        _items[type] = GetAmount(type) + accept;
        _building?.AddInvested(accept);         // 累计投入（拆除全退／修复费基数 · 裁决自陈-3）
        if (IsSatisfied) _building?.OnSiteMaterialsReady();
        return accept;
    }

    /// <summary>清空（完工"材料转成本体"／死亡掉箱后）。</summary>
    public void Clear() => _items.Clear();

    /// <summary>读档恢复内容（`BuildingSaveData.siteContents`）。</summary>
    public void RestoreContents(ResourceList contents)
    {
        _items.Clear();
        if (contents.items == null) return;
        for (int i = 0; i < contents.items.Length; i++)
        {
            var e = contents.items[i];
            if (e.amount > 0) _items[e.type] = e.amount;   // 存档态即合法态（阈值内）
        }
    }

    // ===== ITaskSource（搬料任务广告）=====

    /// <summary>仍待料且建筑在场 ⇒ 有效；料齐／建筑销毁即失效（在派搬料任务由调度器自动放弃）。</summary>
    public bool IsValid => _building != null && _building.IsSiteAwaitingMaterials && !IsSatisfied;

    /// <summary>⭐ **取料仓位置**（每次 `TryAdvertiseTask` 解析后更新）—— 工人的第一段位移目标。</summary>
    public Vector2 SourcePos => _pickupPos;

    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        if (_building == null || !_building.IsSiteAwaitingMaterials) return false;

        // ① 取首个仍有缺口的资源（资源表序 ⇒ 确定性）
        ResourceType? want = null;
        for (int i = 0; i < TableOrder.Length; i++)
        {
            if (Remaining(TableOrder[i]) > 0) { want = TableOrder[i]; break; }
        }
        if (!want.HasValue) return false;   // 料齐（理论不可达：IsSatisfied 时 IsValid 已 false）

        // ② 解析取料仓：**同国 ＋ 收该资源 ＋ 有存量**的最近仓（含国库容器 · 走 WarehouseRegistry）
        var pickup = FindPickup(want.Value, _building.kingdomId, _building.transform.position);
        if (pickup == null) return false;   // 无仓有货 ⇒ 本 tick 不广告（等有货再搬）

        _pickupPos = pickup.transform.position;

        task = new KingdomTask(KingdomTaskType.Build, this);
        task.destType = KingdomDestType.SpecificBuilding;
        task.destPos = _building.transform.position;   // 卸料点 ＝ 工地（第二段位移目标）
        task.args = new HaulToSiteArgs
        {
            site = this,
            resourceType = want.Value,
            pickup = pickup,
            need = Remaining(want.Value)
        };
        return true;
    }

    public void OnRegister() { }
    public void OnUnregister() { }

    /// <summary>
    /// 取料仓解析：`WarehouseRegistry.GatherActive(kingdomId)`（同国 ＋ 有存量）中，
    /// **收该资源（标签）＋ 该资源存量 &gt; 0** 的最近者。返回 null ＝ 无仓有货。
    /// ⚠️ 频率：只在广告时点调用（`TaskScheduler.Tick` 周期），⛔ 不在每帧路径。
    /// </summary>
    private static StorageComponent FindPickup(ResourceType type, int kingdomId, Vector3 fromPos)
    {
        var list = WarehouseRegistry.GatherActive(kingdomId);
        StorageComponent best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i] as StorageComponent;
            if (s == null) continue;
            if (!s.Accepts(type)) continue;
            if (s.GetAmount(type) <= 0) continue;
            float d = (s.transform.position - fromPos).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = s; }
        }
        return best;
    }
}
