using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 王国仓库凑单器（2_12 步骤3，D51 多仓库凑单）。结算时点专用工具，非每帧路径。
///
/// 语义：给定一笔资源成本，从多个王国仓库各取一点凑够；任一资源不足则**整笔回滚**
/// （已从其它仓库取走的全部退还），保证原子性，调用方据此判定"负担得起/负担不起"。
/// 所有取用走 IWarehouse.Take，仓库实现（StorageComponent 王国仓库侧 / WorkerInventory 移动仓库）语义为零增零减。
///
/// 金(Gold)=货币不占存储（D51），保留 RulerController 直通（瞬时即取即用，不参与仓库凑单）。
///
/// ⚠️ 调用频率红线：本类只许在**结算时点**调用（建造/训练点击、搬运卸货、升级确认），
///    禁止在 Update / 每帧 / 活跃逻辑路径里调用——FindObjectsOfType 是全场景扫描的过渡实现，
///    这是安全边界，不是性能理由。
/// </summary>
public static class WarehouseHelper
{
    /// <summary>
    /// 结算一次王国仓库资源成本（建造/升级/训练）。成功 true 并已从仓库扣减；失败 false 且**未做任何扣减**（整笔回滚）。
    /// </summary>
    public static bool TrySettle(ResourcePack cost)
    {
        if (cost.IsZero) return true;
        var warehouses = GatherWarehouses();
        var locked = new List<IWarehouse>();

        // 金：货币直通。其余三资源走仓库凑单。
        if (cost.gold > 0)
        {
            if (RulerController.Instance == null || !RulerController.Instance.CanAfford(new ResourcePack { gold = cost.gold }))
                return false;
        }

        // 预校验三资源是否凑得够（不足则直接失败，不动任何仓库）
        if (!TryCheckEnough(warehouses, ResourceType.Stone, cost.stone)) return false;
        if (!TryCheckEnough(warehouses, ResourceType.Wood, cost.wood)) return false;
        if (!TryCheckEnough(warehouses, ResourceType.Food, cost.food)) return false;

        // 真正扣减：先逐仓锁定实际可取的量（暂存不动），全部够才开始真正 Take
        int[] stoneTake = LockTakes(warehouses, ResourceType.Stone, cost.stone);
        int[] woodTake = LockTakes(warehouses, ResourceType.Wood, cost.wood);
        int[] foodTake = LockTakes(warehouses, ResourceType.Food, cost.food);

        // 执行减（先金，再仓库资源；仓库不减的成功不会被部分应用，因为已预校验足够）
        if (cost.gold > 0) RulerController.Instance.Spend(new ResourcePack { gold = cost.gold });
        ApplyTakes(warehouses, ResourceType.Stone, stoneTake);
        ApplyTakes(warehouses, ResourceType.Wood, woodTake);
        ApplyTakes(warehouses, ResourceType.Food, foodTake);
        return true;
    }

    /// <summary>是否从王国仓库+国库负担得起这笔成本（原子判定，不改动）。</summary>
    public static bool CanAfford(ResourcePack cost)
    {
        if (cost.IsZero) return true;
        if (cost.gold > 0)
            if (RulerController.Instance == null || !RulerController.Instance.CanAfford(new ResourcePack { gold = cost.gold }))
                return false;
        var warehouses = GatherWarehouses();
        return TryCheckEnough(warehouses, ResourceType.Stone, cost.stone)
            && TryCheckEnough(warehouses, ResourceType.Wood, cost.wood)
            && TryCheckEnough(warehouses, ResourceType.Food, cost.food);
    }

    // ===== 定位器（2_12 步骤8.4：仓库注册表替代 FindObjectsOfType 全场景扫描）=====
    // 调用频率红线：只许结算时点调用，禁入 Update/每帧路径（过渡实现安全边界）。
    private static List<IWarehouse> GatherWarehouses()
    {
        // 2_17 修复卡γ：王国凑单按"玩家王国(0)"匹配——玩家结算只凑玩家仓，绝不流入 AI 库。
        // （AI 王国结算由 2_17 步骤2b 日结转账路径以各自 kingdomId 调 GatherActive。）
        return WarehouseRegistry.GatherActive(0);
    }

    /// <summary>校验所有仓库对该资源累计可取量是否达标。</summary>
    private static bool TryCheckEnough(List<IWarehouse> warehouses, ResourceType type, int need)
    {
        if (need <= 0) return true;
        int sum = 0;
        for (int i = 0; i < warehouses.Count; i++)
        {
            var q = warehouses[i].Query();
            if (q.type == type) sum += q.amount;
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
            if (q.type != type) continue;
            int take = Mathf.Min(remaining, q.amount);
            if (take <= 0) continue;
            takes[i] = take;
            remaining -= take;
        }
        return takes;
    }

    private static void ApplyTakes(List<IWarehouse> warehouses, ResourceType type, int[] takes)
    {
        for (int i = 0; i < warehouses.Count && i < takes.Length; i++)
        {
            if (takes[i] > 0) warehouses[i].Take(type, takes[i]);
        }
    }
}