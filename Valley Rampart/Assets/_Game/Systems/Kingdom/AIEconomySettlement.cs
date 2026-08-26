using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 王国经济日结（2_17 步骤2b：收入侧入账路由）。
/// 追记二 ⑤-3 裁 B —— 在 DayCycleSettlement 日结时，把每 AI 建筑 StorageComponent 累计产出
/// `AddResources` 入 KingdomState.resources 并清零本地仓储，复用 IsFull 容量语义
/// （ProducerComponent 满仓停产出，日结清零故次日可继续攒）。
///
/// 两条硬性（⑤-3）：
///   a) 建筑遍历**固定排序**（禁依赖注册顺序 / FindObjectsOfType 序）——同 seed 逐字节一致须覆盖 AI 段；
///   b) 差距账本登记（sim 瞬时入账无 Storage 中介 vs Unity 日结两段式）——见 15_ 差距账本。
///
/// 边界 / 零回归：
///   - 只处理 AI 王国（kingdomId&gt;0）；玩家(id=0)产出不走本路由（玩家物流走 WarehouseRegistry/TreasureVault），零回归。
///   - 国库只认五经济资源（Gold/Stone/Wood/Food/Metal，2a 语义）。非经济资源（Ore/Crystal/FireOil/
///     特殊食物/肉/弹药）不入 AI 国库，跳过保留在本地存储。
///   - 水井 kingidId&gt;0 已在 ProducerComponent 拦截不入水网，本路由天然不处理。
/// </summary>
public static class AIEconomySettlement
{
    /// <summary>日结 AI 段：把所有 AI 建筑产出的五经济资源路由入各自王国国库并清零本地仓储。</summary>
    public static void Tick()
    {
        var reg = KingdomRegistry.Instance;
        if (reg == null) return;

        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var kingdom = all[i];
            if (kingdom.IsPlayer) continue;   // 玩家国库不在此入账（零回归）
            SettleKingdom(kingdom);
        }
    }

    /// <summary>单王国结算：收集本王国活跃带 Storage 的建筑 → 固定排序 → 逐建筑入账清零。</summary>
    private static void SettleKingdom(KingdomState kingdom)
    {
        var buildings = QueryKingdomBuildings(kingdom.id);
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            var storage = b.GetComponent<StorageComponent>();
            if (storage == null || storage.storedAmount <= 0) continue;

            var pack = new ResourcePack();   // 五经济资源入账；非经济跳过
            if (!MapToPack(storage.resourceType, storage.storedAmount, ref pack)) continue;
            if (pack.IsZero) continue;

            kingdom.AddResources(pack);

            // 清零本地仓储（日结搬运语义；与 Harvest 不同，出货入 AI 国库而非玩家 Ruler）。
            // 走 storage.Take(本类型, 全量) → 内部 TakeOut 扣减并触发 OnStorageChanged（event 不可在外部 Invoke，
            // 用 IWarehouse.Take 接口保证 UI/仓库刷新与扣减原子一致）。
            int drained = storage.storedAmount;
            storage.Take(storage.resourceType, drained);
            if (drained > 0)
                Debug.Log($"[AIEconomySettlement] k{kingdom.id} {b.def?.id} @{b.coord.x},{b.coord.y} 日结入账 {storage.resourceType}×{drained} → 国库");
        }
    }

    /// <summary>
    /// 查询某王国全部建筑。用 BuildingRegistry（单例真源）；单例未物化时 FindObjectsOfType 兜底。
    /// 无论何种来源，返回前一律**固定排序**（核心：禁依赖注册序/FindObjectsOfType 序），
    /// 保证同 seed 两轮逐字节一致（⑤-3 硬性 a）。
    /// </summary>
    private static List<Building> QueryKingdomBuildings(int kingdomId)
    {
        List<Building> list = new List<Building>();
        var reg = BuildingRegistry.Instance;
        if (reg != null && reg.All != null)
        {
            var src = reg.All;
            for (int i = 0; i < src.Count; i++)
                if (src[i] != null && src[i].kingdomId == kingdomId && src[i].IsActive)
                    list.Add(src[i]);
        }
        else
        {
            foreach (var b in Object.FindObjectsByType<Building>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b != null && b.kingdomId == kingdomId && b.IsActive)
                    list.Add(b);
        }

        // 固定排序：主键=坐标（左上格），次键=def.id（String.CompareOrdinal），彻底丢掉收集序/注册序/对象序。
        list.Sort((a, b) =>
        {
            // 升序：全王国坐标唯一 → 主键即可排定；同格（理论不应）再比 def.id。
            if (a.coord.y != b.coord.y) return a.coord.y.CompareTo(b.coord.y);
            if (a.coord.x != b.coord.x) return a.coord.x.CompareTo(b.coord.x);
            var ad = a.def != null ? a.def.id : "";
            var bd = b.def != null ? b.def.id : "";
            return string.CompareOrdinal(ad, bd);
        });
        return list;
    }

    /// <summary>把存储类型按五经济资源映射进 ResourcePack；非经济资源返回 false（跳过，保留本地存储）。</summary>
    private static bool MapToPack(ResourceType type, int amount, ref ResourcePack pack)
    {
        switch (type)
        {
            case ResourceType.Gold: pack.gold += amount; return true;
            case ResourceType.Stone: pack.stone += amount; return true;
            case ResourceType.Wood: pack.wood += amount; return true;
            case ResourceType.Food: pack.food += amount; return true;
            case ResourceType.Metal: pack.metal += amount; return true;
            default: return false;   // Ore/Crystal/FireOil/特殊食物/肉/弹药：非经济资源不入 AI 国库
        }
    }
}