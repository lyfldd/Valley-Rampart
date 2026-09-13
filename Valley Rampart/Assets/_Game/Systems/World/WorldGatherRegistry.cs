using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HH.221（**A①**，D685 裁 A）「世界资源点采集立案处」——面向 AI 的世界资源点采集**选点**层。
///
/// 分层（任务书 §一.A.5 / `2_24` §7.2 O-3 禁决策核直选目标）：
///   · **决策核**（`KingdomBrain.ExecuteFocus` 的 `GatherWorldResource` 分支）只出**粗意图**＝「缺 X → 下发采集」；
///   · **本层**＝把粗意图翻译成具体世界资源点（**选点**）并 `Register` 进 `TaskScheduler`；
///   · **TaskScheduler**＝派工＋距离排序（`Dispatch` 内置最近空闲工人匹配，本层不碰）。
///
/// 归属/可达（任务书 §一.A.4「禁全局共享」）＝**只立案本国领土内**的点
/// （`TerritorySystem.GetKingdomTerritory` 中区块口径，与领土系统同源；禁另造第二套可达体系）。
/// 与玩家侧同源等价：同样产 `KingdomTaskType.Gather` → 工人背包 → 归属国分流台账（无免费通道）。
///
/// 防洪泛：**单国在册源上限**（`WorldGatherConfig.maxSourcesPerKingdom`，SO 驱动）。
/// 已立案点其源在派工前**每 tick 重新广告**（`WorldGatherSource.TryAdvertiseTask`，
/// 同源去重由 `TaskScheduler.HasAssignedTaskForSourceType` 承担）；源失效（完成／目标消失）即从本表剔除，
/// 该点可被再次立案（资源重生后重采）。
/// </summary>
public class WorldGatherRegistry : Singleton<WorldGatherRegistry>
{
    private class Entry
    {
        public WorldGatherSource Source;
        public GridCoord Cell;
        public bool IsTree;
    }

    private readonly Dictionary<int, List<Entry>> _byKingdom = new Dictionary<int, List<Entry>>();

    /// <summary>某国当前在册的世界资源点采集源数（诊断/探针用）。</summary>
    public int CountOf(int kingdomId)
    {
        Prune(kingdomId, out var list);
        return list != null ? list.Count : 0;
    }

    /// <summary>HH.254/D700（A+ ㉗ 触发式断路器）只读查询：该国在册源中**指定资源**的条数（不立案·不改状态）。
    /// 与 <see cref="Advertise"/> 共用同一匹配函数 <see cref="TryMatchPoint"/>（L-31 同源，禁另造匹配逻辑）。
    /// 用途＝触发条件⑤「该 rt 本国无在册源」的 one-shot 自限判据。</summary>
    public int CountOf(int kingdomId, ResourceType resource)
    {
        if (kingdomId <= 0) return 0;
        Prune(kingdomId, out var list);
        if (list == null || list.Count == 0) return 0;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || map.features == null) return 0;
        int n = 0;
        for (int i = 0; i < list.Count; i++)
            if (TryMatchPoint(map, list[i].Cell, resource, out _, out _)) n++;
        return n;
    }

    /// <summary>HH.254/D700（A+ ㉗ 触发式断路器）只读查询：该国**登记余量**（`maxSourcesPerKingdom` 是全 rt 共享上限）。
    /// 与 <see cref="Advertise"/> 的 room 判据**同一公式**（<see cref="RoomOf"/>·L-31）；
    /// 用途＝触发预检排除「条件成立但容量已满」（此时 `Advertise` 会返 (0,0) ⇒ 白焦点日）。</summary>
    public int RoomOf(int kingdomId)
    {
        var cfg = WorldGatherConfig.Load();
        if (cfg == null || !cfg.enabled) return 0;
        Prune(kingdomId, out var list);
        return RoomOf(list, cfg);
    }

    /// <summary>登记余量**单源**（`Advertise` 与触发预检共用；L-31 禁另造公式）。</summary>
    private static int RoomOf(List<Entry> list, WorldGatherConfig cfg)
        => Mathf.Max(0, cfg != null ? cfg.maxSourcesPerKingdom - (list != null ? list.Count : 0) : 0);

    /// <summary>
    /// 立案：为本国领土内的目标资源点创建并注册 `WorldGatherSource`（**选点归本层**）。
    /// 返回 (新立案数 registered, 立案前所见候选点数 candidates)；`candidates==0` ⇒ 本国领土内无可采目标
    /// （决策核按"执行失败/让渡"处理，`KingdomBrain` 侧按 Env 报退避）。
    /// 同国在册源数受 SO 上限约束（防 `_sources` 洪泛）；已在册的点幂等跳过。
    /// 确定性：领土集已按 (x,y) 排序（`TerritorySystem.GetKingdomTerritory`）⇒ 遍历序与立案序固定。
    /// </summary>
    public (int registered, int candidates) Advertise(int kingdomId, ResourceType resource)
    {
        if (kingdomId <= 0) return (0, 0);                       // 玩家(0)不走本层（玩家侧现状不动）
        var cfg = WorldGatherConfig.Load();
        if (cfg == null || !cfg.enabled) return (0, 0);
        var sched = TaskScheduler.Instance;
        if (sched == null) return (0, 0);

        Prune(kingdomId, out var list);
        int room = RoomOf(list, cfg);                           // HH.254/D700：单源（触发预检 <see cref="RoomOf(int)"/> 共用本公式·L-31）
        if (room <= 0) return (0, 0);                           // 已在册满额（完成后腾位）

        var ts = TerritorySystem.Instance;
        var grid = GridSystem.Instance;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (ts == null || grid == null || map == null || map.features == null) return (0, 0);

        int ms = grid.Config != null && grid.Config.midChunkSize > 0 ? grid.Config.midChunkSize : 4;
        var resCfg = RespawnConfig.Instance;

        int candidates = 0, registered = 0;
        foreach (var mid in ts.GetKingdomTerritory(kingdomId))
        {
            for (int y = mid.y * ms; y < (mid.y + 1) * ms; y++)
            {
                for (int x = mid.x * ms; x < (mid.x + 1) * ms; x++)
                {
                    var cell = new GridCoord(x, y);
                    if (!TryMatchPoint(map, cell, resource, out bool isTree, out Building entity)) continue;
                    candidates++;
                    if (Contains(list, cell, isTree)) continue;     // 幂等：已在册点不重复立案
                    if (registered >= room) continue;               // 在册上限（仍有候选 ⇒ 明日/腾位后再立案）

                    var pos = grid.CoordToWorld(cell);
                    var src = isTree
                        ? WorldGatherSource.ForTree(cell, pos, kingdomId, resCfg)
                        : WorldGatherSource.ForEntity(entity, pos, kingdomId, sched.gatherAmount);
                    sched.Register(src);
                    list.Add(new Entry { Source = src, Cell = cell, IsTree = isTree });
                    registered++;
                }
            }
        }
        return (registered, candidates);
    }

    /// <summary>断供资源（诊断口径 `EcoResource`）→ 世界可采资源类型（世界资源点只覆盖石/木两类；
    /// 其余 → false）。**单源映射**（评分 `Feasible`／`NeedScore` 与选点层共用，禁散落 cast——两枚举值当前巧合相同，不得依赖）。</summary>
    public static bool TryMapWorldResource(EcoResource er, out ResourceType rt)
    {
        switch (er)
        {
            case EcoResource.Stone: rt = ResourceType.Stone; return true;
            case EcoResource.Wood: rt = ResourceType.Wood; return true;
            default: rt = ResourceType.Gold; return false;   // 粮/金/铁：无世界资源点通道（粮归农场产能、金铁归产能/加工）
        }
    }

    /// <summary>本国领土内是否存在可采该资源的点（**不立案、只探测**；`UtilityScorer.Feasible` 硬门槛用）。
    /// 与立案共用同一匹配函数 `TryMatchPoint`（L-31 同源，禁另抄）。</summary>
    public static bool HasCandidate(int kingdomId, ResourceType resource)
    {
        if (kingdomId <= 0) return false;
        var ts = TerritorySystem.Instance;
        var grid = GridSystem.Instance;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (ts == null || grid == null || map == null || map.features == null) return false;
        int ms = grid.Config != null && grid.Config.midChunkSize > 0 ? grid.Config.midChunkSize : 4;
        foreach (var mid in ts.GetKingdomTerritory(kingdomId))
            for (int y = mid.y * ms; y < (mid.y + 1) * ms; y++)
                for (int x = mid.x * ms; x < (mid.x + 1) * ms; x++)
                    if (TryMatchPoint(map, new GridCoord(x, y), resource, out _, out _)) return true;
        return false;
    }

    /// <summary>该格是否本国可采的目标资源点（树=数据格 features；一次性实体=Building 注册表 + `isConsumable` + 产出匹配）。</summary>
    private static bool TryMatchPoint(MapData map, GridCoord cell, ResourceType resource, out bool isTree, out Building entity)
    {
        isTree = false;
        entity = null;
        if (cell.x < 0 || cell.y < 0 || cell.x >= map.width || cell.y >= map.height) return false;

        // ① 数据格树（A+ 数据化，不建实体；木的唯一持续来源）
        if (resource == ResourceType.Wood)
        {
            int i = cell.y * map.width + cell.x;
            if (map.features[i] == FeatureType.Tree) { isTree = true; return true; }
        }

        // ② 一次性可采实体（stone_pile／ore_vein／wood_pile：isConsumable=1 且产出该资源）
        var reg = BuildingRegistry.Instance;
        var b = reg != null ? reg.GetAt(cell) : null;
        if (b != null && b.IsValid && b.def != null && b.def.isConsumable && b.def.outputResource == resource)
        {
            entity = b;
            return true;
        }
        return false;
    }

    private static bool Contains(List<Entry> list, GridCoord cell, bool isTree)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i].Cell == cell && list[i].IsTree == isTree) return true;
        return false;
    }

    /// <summary>剔除已失效源（采集完成／目标消失／跨局清场）⇒ 该点腾位（重生后可再立案）。</summary>
    private void Prune(int kingdomId, out List<Entry> list)
    {
        if (!_byKingdom.TryGetValue(kingdomId, out list))
        {
            list = new List<Entry>();
            _byKingdom[kingdomId] = list;
            return;
        }
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i].Source == null || !list[i].Source.IsValid) list.RemoveAt(i);
    }

    /// <summary>清空全部立案表（新开局/跨轮清场用；防跨轮污染）。</summary>
    public void ResetAll() => _byKingdom.Clear();
}
