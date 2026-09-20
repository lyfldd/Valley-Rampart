using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 王国仓库凑单器（2_12 步骤3，D51 多仓库凑单）。结算时点专用工具，非每帧路径。
///
/// 语义：给定一笔资源成本，从多个王国仓库各取一点凑够；任一资源不足则**整笔回滚**
/// （已从其它仓库取走的全部退还），保证原子性，调用方据此判定"负担得起/负担不起"。
/// 所有取用走 IWarehouse.Take，仓库实现（StorageComponent 王国仓库侧 / WorkerInventory 移动仓库）语义为零增零减。
///
/// ⭐ `M1-A`（`09#50`／`#51`）：成本结构由固定 8 桶 `ResourcePack` ⇒ **「资源量列表」`ResourceList`**，
/// 本类**逐条目**处理（⛔ 不再是硬编码的 石/木/粮/铁 四连查）⇒ 造价加一项资源**无需改本类**。
/// 金(Gold)仍走 `RulerController` 直通（**行为不变** —— 「扣费统一一个 API」归 `M1-E`，`09#47`）。
///
/// ⚠️ 调用频率红线：本类只许在**结算时点**调用（建造/训练点击、搬运卸货、升级确认），
///    禁止在 Update / 每帧 / 活跃逻辑路径里调用。
/// </summary>
public static class WarehouseHelper
{
    /// <summary>
    /// 结算一次王国仓库资源成本（建造/升级/训练）。成功 true 并已从仓库扣减；失败 false 且**未做任何扣减**（整笔回滚）。
    /// </summary>
    public static bool TrySettle(ResourceList cost) => TrySettle(0, cost);

    /// <summary>B1-5（2_24 批1，D605）：带主体参数重载——凑单仓按 kingdomId 主体匹配（0=玩家 / &gt;0=AI）。</summary>
    public static bool TrySettle(int kingdomId, ResourceList cost)
    {
        if (cost.IsZero) return true;
        var entries = cost.items;
        if (entries == null || entries.Length == 0) return true;

        var warehouses = GatherWarehouses(kingdomId);

        // 预校验：逐条目判够不够（不足则直接失败，不动任何仓库）
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e.amount <= 0) continue;
            if (e.type == ResourceType.Gold)
            {
                // 金：货币直通（M1-E 前行为不变）
                if (RulerController.Instance == null
                    || !RulerController.Instance.CanAfford(ResourceList.Of(new ResourceAmount(ResourceType.Gold, e.amount))))
                    return false;
                continue;
            }
            if (!TryCheckEnough(warehouses, e.type, e.amount)) return false;
        }

        // 真正扣减：先逐条目锁定实际可取的量（暂存不动），全部够才开始真正 Take
        var takes = new int[entries.Length][];
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            takes[i] = e.amount > 0 && e.type != ResourceType.Gold
                ? LockTakes(warehouses, e.type, e.amount)
                : null;
        }

        // 执行减（先金，再仓库资源；已预校验足够 ⇒ 不会出现部分应用）
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e.amount <= 0) continue;
            if (e.type == ResourceType.Gold)
                RulerController.Instance.Spend(ResourceList.Of(new ResourceAmount(ResourceType.Gold, e.amount)));
            else
                ApplyTakes(warehouses, e.type, takes[i]);
        }
        return true;
    }

    /// <summary>是否从王国仓库+国库负担得起这笔成本（原子判定，不改动）。</summary>
    public static bool CanAfford(ResourceList cost) => CanAfford(0, cost);

    /// <summary>B1-5（2_24 批1，D605）：带主体参数重载（语义同 TrySettle）。</summary>
    public static bool CanAfford(int kingdomId, ResourceList cost)
    {
        if (cost.IsZero) return true;
        var entries = cost.items;
        if (entries == null || entries.Length == 0) return true;

        var warehouses = GatherWarehouses(kingdomId);
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e.amount <= 0) continue;
            if (e.type == ResourceType.Gold)
            {
                if (RulerController.Instance == null
                    || !RulerController.Instance.CanAfford(ResourceList.Of(new ResourceAmount(ResourceType.Gold, e.amount))))
                    return false;
                continue;
            }
            if (!TryCheckEnough(warehouses, e.type, e.amount)) return false;
        }
        return true;
    }

    // ===== 定位器（2_12 步骤8.4：仓库注册表替代 FindObjectsOfType 全场景扫描）=====
    // 调用频率红线：只许结算时点调用，禁入 Update/每帧路径（过渡实现安全边界）。
    private static List<IWarehouse> GatherWarehouses(int kingdomId)
    {
        // 2_17 修复卡γ：王国凑单按主体匹配——玩家(0)结算只凑玩家仓，绝不流入 AI 库。
        return WarehouseRegistry.GatherActive(kingdomId);
    }

    /// <summary>校验所有仓库对该资源累计可取量是否达标（多资源仓 ⇒ 逐条目求和）。</summary>
    private static bool TryCheckEnough(List<IWarehouse> warehouses, ResourceType type, int need)
    {
        if (need <= 0) return true;
        int sum = 0;
        for (int i = 0; i < warehouses.Count; i++)
        {
            var q = warehouses[i].Query();
            for (int j = 0; j < q.Count; j++)
                if (q[j].type == type) { sum += q[j].amount; break; }
            if (sum >= need) return true;
        }
        return false;
    }

    /// <summary>预锁定各仓库将要取走的量（0=该仓不参与），但不真正扣减。</summary>
    private static int[] LockTakes(List<IWarehouse> warehouses, ResourceType type, int need)
    {
        int[] takes = new int[warehouses.Count];
        if (need <= 0) return takes;
        int remaining = need;
        for (int i = 0; i < warehouses.Count && remaining > 0; i++)
        {
            var q = warehouses[i].Query();
            int have = 0;
            for (int j = 0; j < q.Count; j++)
                if (q[j].type == type) { have = q[j].amount; break; }
            int take = Mathf.Min(remaining, have);
            if (take <= 0) continue;
            takes[i] = take;
            remaining -= take;
        }
        return takes;
    }

    private static void ApplyTakes(List<IWarehouse> warehouses, ResourceType type, int[] takes)
    {
        if (takes == null) return;
        for (int i = 0; i < warehouses.Count && i < takes.Length; i++)
        {
            if (takes[i] > 0) warehouses[i].Take(type, takes[i]);
        }
    }
}
