using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.294 片 6-2「双写收敛」验证探针（Editor-only）
//  口径真源：最高优先级文档/03_地图即数据库.md（§6.7／§7／§8／§9）
//           ＋ HH.294 底层重构批任务书 §二 片 6（6-A~6-E）＋ 报裁裁决 HH.294_片6-2_报裁_裁决.md（D778）
//
//  入口纪律（test-harness-first 铁律1）：**走正门** TestHarnessApi.EnterTestRun（禁裸跑）。
//  收尾纪律（L-32）：真暂停(Time.timeScale=0) → Save → ExitTestRun → 退 Play。
//  【HH.294 片 6-2 收尾（`D779` 残余 `S1`）】旧路径清场后**单段**＝目标态全量读数：
//    A/B 对照开关（`SpawnResourceEntities`）已删 ⇒ 不再跑对照段（改前读数已落盘 `HH.307` §一，不复须）。
//
//  判据面：
//    §A 判据 1  三型 Building 实例数 = 0（＋ Registry 总数 对照上批读数 21）                §A
//    §B 占格面  资源格不占格 ＋ 放置阻挡承接（压资源格 ⇒ Blocked）                        §A/B
//    §C 判据 3  玩家入口：消费者在场 ＋ 四型各 1 次「命令 → 派工」读数                     §C
//    §D 判据 4  采集完成 ⇒ 格翻 Plain ＋ 池子 −1（前后读数）＋ 删门/守卫通知幂等           §D
//    §E 判据 6  6-E：OreVein 格表来源候选数对照 ＋ 反证 ＋ 12s 缓存代价                    §E
//    §F 判据 7  R1：PickCellRadius 重推值 ＋ castle 上沿命中（存在性反证）＋ 代价          §F
//    §G 判据 8  R2′：独立渲染对照（像素隐藏法·三件套：身份可区分／背景对照／每例 ≥3 有效点）  §G
//    §H 判据 11 同 seed 跨跑次逐格一致（features+climateZones hash）
//  落盘：Logs/hh294_slice6_2/hh294_slice6_2_probe.txt（稳定名）＋ 时间戳副本
// ============================================================================
public static class HH294Slice62Probe
{
    public const string Tag = "HH294S62";
    private const string Menu = "Valley/审计/HH294片6-2/跑双写收敛探针（正门进局）";
    private const int PROBE_SEED = 29418;
    private const string PROBE_SLOT = "hh294_slice6_2";

    private static readonly StringBuilder Sb = new StringBuilder();
    private static bool _running;

    [MenuItem(Menu, priority = 211)]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[" + Tag + "] 须先 GameScene 进 Play（正门 EnterTestRun 在 Play 内协程）。已中止。");
            return;
        }
        if (_running) { Debug.LogWarning("[" + Tag + "] 探针已在跑（幂等守卫）。"); return; }
        _running = true;
        Sb.Length = 0;
        Sb.AppendLine("# HH.294 片 6-2 双写收敛探针【目标态全量·收尾清场后单段】（正门 EnterTestRun·seed=" + PROBE_SEED + " 槽=" + PROBE_SLOT + "）");
        Sb.AppendLine("# 跑次：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                      + "｜MapGate.DepthMajorYInFront=" + MapGate.DepthMajorYInFront);
        new GameObject("HH294S62ProbeHost").AddComponent<Host>().Go(Run());
    }

    private class Host : MonoBehaviour { public void Go(IEnumerator r) => StartCoroutine(r); }

    private static void Log(string line)
    {
        Sb.AppendLine(line);
        Debug.Log("[" + Tag + "] " + line);
    }

    private static IEnumerator Run()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = PROBE_SEED, mapSeed = PROBE_SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = PROBE_SLOT, kingdomName = "河谷王国"
        };
        Log("── 正门进局（单段·目标态）");
        yield return TestHarnessApi.EnterTestRun(cfg);
        yield return null; yield return null;

        long hash = HashMap();
        Log("── hash（建局即取）features+climateZones：" + hash.ToString("X16"));
        long prev = WriteHash(hash);

        Log("");
        Log("## §A 判据1 生成期不派生实体（三型 Building 实例数）");
        Case_NoEntities();

        Case_Occupancy();                    // §B 占格面（含放置阻挡承接）
        yield return Case_PlayerEntry();     // §C 判据 3
        Case_CellGathered();                 // §D 判据 4 ＋ 幂等
        Case_OreVeinSource();                // §E 判据 6（6-E）
        Case_PickRadius();                   // §F 判据 7（R1）
        yield return Case_RenderCompare();   // §G 判据 8（R2′）

        Log("");
        Log("## §H 判据11 同 seed 跨跑次建局对比（判据：逐格一致）");
        Log("§H 上次=" + prev.ToString("X16") + " 本次=" + hash.ToString("X16")
            + " ⇒ " + (prev == hash && prev != 0 ? "逐格一致（hash 双同）✅" : "（首跑无对照 or 不一致）"));

        Finish();
    }

    // ========================================================================
    //  §A：判据 1（三型 Building 实例数 ＋ Registry 总数 对照上批读数）
    // ========================================================================
    private static void Case_NoEntities()
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null) { Log("§A ❌ BuildingRegistry 不在场"); return; }
        int total = 0, ore = 0, stone = 0, wood = 0, mineT = 0, mineF = 0;
        var samples = new StringBuilder();
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b == null) continue;
            total++;
            switch (b.sourceType)
            {
                case BuildingType.OreVein: ore++; break;
                case BuildingType.StonePile: stone++; break;
                case BuildingType.WoodPile: wood++; break;
                case BuildingType.Mine: mineT++; break;
            }
            if (b.def != null && b.def.id == "mine") mineF++;
            if (samples.Length < 200 && (b.sourceType == BuildingType.OreVein || b.sourceType == BuildingType.StonePile || b.sourceType == BuildingType.WoodPile))
                samples.Append(b.name).Append(";");
        }
        Log("§A BuildingRegistry 总数 = **" + total + "**（清场后目标态；对照上批 `HH.307` 目标态读数 **21** ⇒ 须不变）");
        Log("§A 三型实例数：OreVein=" + ore + " StonePile=" + stone + " WoodPile=" + wood
            + " ⇒ 合计 = **" + (ore + stone + wood) + "**（目标态应 = 0）" + ((ore + stone + wood) == 0 ? " ✅" : " "));
        Log("§A 对照类目：mine 建筑（按 sourceType）=" + mineT + "（按 def.id）=" + mineF
            + " —— 判据1 只涉三型；mine＝AI 预置/T6 转型面，**不在本批**");
        if (samples.Length > 0) Log("§A 三型实例抽样：" + samples);
        else Log("§A 三型实例抽样：（空 ⇒ 目标态全图零三型实体）");
    }

    // ========================================================================
    //  §B：占格面 —— 资源格不占格 ＋ 放置阻挡承接（改前由实体占格承担的阻挡）
    // ========================================================================
    private static void Case_Occupancy()
    {
        Log("");
        Log("## §B 占格面：资源格不占格 ＋ 普通建筑压资源格 ⇒ 阻挡承接");
        var grid = GridSystem.Instance;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (grid == null || map == null) { Log("§B ❌ 网格/地图未就绪"); return; }

        // ① 抽 20 个三型资源格：occupant 应为 null（改前有实体 ⇒ 非 null）
        int checkedCells = 0, occupied = 0;
        var buf = new StringBuilder();
        for (int y = 2; y < map.height - 2 && checkedCells < 20; y += 7)
            for (int x = 2; x < map.width - 2 && checkedCells < 20; x += 5)
            {
                var f = MapGate.ReadAt(map, x, y);
                if (f != FeatureType.OreVein && f != FeatureType.WoodPile && f != FeatureType.StonePile) continue;
                var c = new GridCoord(x, y);
                var occ = grid.GetOccupant(c);
                checkedCells++;
                if (occ != null) { occupied++; if (buf.Length < 200) buf.Append("(" + x + "," + y + ")" + occ.GetType().Name + ";"); }
            }
        Log("§B 三型资源格抽样 " + checkedCells + " 格 ⇒ `GetOccupant != null` 的 = **" + occupied + "**"
            + (occupied == 0 ? " ✅（资源格不占格）" : " ❌ " + buf));
        Log("§B 对照读数（改前语义）：三型资源点有 Building 实体时 `MarkOccupiedFootprint` 会写占格（1×1）"
            + "——改后零实体 ⇒ 零占格；`isObstacle=0` ⇒ 改前亦**不写 BuildingBlocked** ⇒ 可走面零变化（见下）");
        // 可走面零变化佐证：三型格可走位与 feature 派生一致（随机抽 5 格，IsWalkable==true）
        int walkTrue = 0, walkN = 0;
        for (int y = 3; y < map.height - 3 && walkN < 5; y += 11)
            for (int x = 3; x < map.width - 3 && walkN < 5; x += 9)
            {
                var f = MapGate.ReadAt(map, x, y);
                if (f != FeatureType.OreVein && f != FeatureType.WoodPile && f != FeatureType.StonePile) continue;
                walkN++;
                if (grid.IsWalkable(new GridCoord(x, y))) walkTrue++;
            }
        Log("§B 三型格可走读数：抽样 " + walkN + " 格 ⇒ IsWalkable=true 的 = " + walkTrue + "（feature 派生·与改前一致）");

        // ② 放置阻挡承接：找一格 OreVein 特征，用普通建筑（House·allowedTerrain 为空——地形校验不拦）试放
        GridCoord? probeCell = null;
        for (int y = 4; y < map.height - 4 && probeCell == null; y += 5)
            for (int x = 4; x < map.width - 4; x += 5)
                if (MapGate.ReadAt(map, x, y) == FeatureType.OreVein) { probeCell = new GridCoord(x, y); break; }
        if (!probeCell.HasValue) { Log("§B ❌ 未找到 OreVein 格（放置承接用例跳过）"); return; }

        var houseDef = BuildingFactory.FindDefById("House");
        if (houseDef == null) { Log("§B ❌ 未找到 House def"); return; }
        var sub = grid.CellToSub(probeCell.Value, 0, 0);
        var res = PlacementValidator.ValidatePlacement(houseDef, sub, GateOrientation.Horizontal);
        Log("§B 放置承接（普通建筑 House 压 OreVein 格 (" + probeCell.Value.x + "," + probeCell.Value.y + ")）："
            + "ValidatePlacement.ok=**" + res.ok + "** reason=**" + res.reason + "**"
            + (res.ok ? " （新空洞：可压在资源格上）" : " ✅（承接成立：与改前同阻断）"));
        Log("§B 承接实现：`PlacementValidator` 新增格表判定（三型命中 ⇒ Blocked·`allowedTerrain` 为空的普通建筑也拦住）；"
            + " 只三型（Tree/Mine 改前即无实体 ⇒ 不纳 ⇒ 零行为变更）");
    }

    // ========================================================================
    //  §C：判据 3 玩家入口（PrioritizeHarvestCommand 消费者）＋ 四型各 1 次
    // ========================================================================
    private static IEnumerator Case_PlayerEntry()
    {
        Log("");
        Log("## §C 判据3 玩家入口：全工人 + 右键资源格 ⇒ 该格进入采集");
        var grid = GridSystem.Instance;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        var sched = TaskScheduler.HasInstance ? TaskScheduler.Instance : null;
        if (grid == null || map == null || sched == null) { Log("§C ❌ 依赖未就绪"); yield break; }

        bool hasSub = EventBus.HasSubscribers<PrioritizeHarvestCommand>();
        Log("§C 消费者在场：EventBus.HasSubscribers<PrioritizeHarvestCommand>() = **" + hasSub + "**"
            + (hasSub ? " ✅（改前＝false·零订阅死码；本批 ResourceRespawnSystem.Awake 接入）" : " ❌"));

        var savedTick = sched.tickInterval;
        sched.tickInterval = 0.05f;   // 探针提速（收尾复原）

        var types = new[] { FeatureType.Tree, FeatureType.OreVein, FeatureType.StonePile, FeatureType.WoodPile };
        var spawned = new List<UnitController>();
        foreach (var want in types)
        {
            // 找一格该型 feature（避开建筑占格）
            GridCoord? cell = null;
            for (int y = 5; y < map.height - 5 && cell == null; y += 3)
                for (int x = 5; x < map.width - 5; x += 3)
                {
                    if (MapGate.ReadAt(map, x, y) != want) continue;
                    if (grid.GetOccupant(new GridCoord(x, y)) != null) continue;
                    cell = new GridCoord(x, y); break;
                }
            if (!cell.HasValue) { Log("§C [" + want + "]  未找到自由格（跳过）"); continue; }

            // ⚠️ 命令落点＝**格内点**（菱形中心 `CellToWorldF(x+.5,y+.5)`），**不是** `CoordToWorld(cell)`
            //   —— 后者＝「等轴菱形**下顶点**」（换算内核约定）：顶点是 4 格共享角，`WorldToCoord` 的 floor
            //   在浮点边界会落到邻格（本探针首跑实测 (179,5)→(179,4)）。真实玩家点击落在菱形内部 ⇒ 无此歧义。
            Vector2 world = GridSystem.CellToWorldF(cell.Value.x + 0.5f, cell.Value.y + 0.5f,
                grid.Config != null ? grid.Config.cellSize : new Vector2(1.28f, 0.64f));
            var go = UnitFactory.Instance != null
                ? UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker, world + new Vector2(0.2f, 0f), 0)
                : null;
            var uc = go != null ? go.GetComponent<UnitController>() : null;
            if (uc == null) { Log("§C [" + want + "] ❌ 测试工人生成失败（跳过）"); continue; }
            spawned.Add(uc);
            yield return null;   // 注册帧

            bool published = false;
            if (EventBus.HasSubscribers<PrioritizeHarvestCommand>())
            {
                var workers = new List<UnitController> { uc };
                EventBus.Publish(new PrioritizeHarvestCommand(workers, world));
                published = true;
            }
            bool dispatched = false;
            string st = "None";
            for (int i = 0; i < 40 && !dispatched; i++)   // ≈0.7s（tickInterval 0.05）
            {
                yield return null;
                var s = sched.GetWorkerState(uc.npcId);
                st = s.ToString();
                if (s == TaskState.MovingToSource || s == TaskState.Working) dispatched = true;
            }
            // 诊断（失败时定位是「格」还是「链」）：feature 双读数 ＋ 落点回解格 ＋ 直接立案返回值
            string diag = "";
            if (!dispatched)
            {
                var fRead = MapGate.GetFeatureAt(cell.Value);
                var backOpt = grid.WorldToCoord(world);
                bool direct = rsConfirm(cell.Value);
                diag = " ｜诊断：GetFeatureAt=" + fRead + " 落点回解格=" + (backOpt.HasValue ? backOpt.Value.ToString() : "null")
                     + " 直接ConfirmResourceGather=" + direct;
            }
            Log("§C [" + want + "] 格(" + cell.Value.x + "," + cell.Value.y + ") 发布命令=" + published
                + " ⇒ 工人(npcId=" + uc.npcId + ") 状态=**" + st + "** ⇒ 进入采集=" + (dispatched ? "✅" : "❌") + diag);

            sched.AbandonTask(uc.npcId);   // 放弃（该格 feature 保留 ⇒ 不污染后续读数）
            Object.Destroy(go);
            yield return null;
        }
        sched.tickInterval = savedTick;
        Log("§C 四型读数完毕（测试工人已销毁·命令走生产同一条链：SelectionController 发布 → ResourceRespawnSystem 消费）");
        Log("§C ️ 接受面＝树＋一次性三型（`WorldGatherSource.IsHarvestFeature`）；**不含 Mine 锚点**"
            + "（无产出资源映射·纳入＝新增「玩家可采锚点」漂移 ⇒ 口径收紧·见报告）");
    }

    // ========================================================================
    //  §D：判据 4 采集完成 ⇒ 格翻 Plain ＋ 池子 −1（＋ 删门/守卫通知幂等）
    // ========================================================================
    private static void Case_CellGathered()
    {
        Log("");
        Log("## §D 判据4 采集完成：格翻 Plain ＋ 池子 −1（前后读数）＋ 幂等");
        var rs = ResourceRespawnSystem.HasInstance ? ResourceRespawnSystem.Instance : null;
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (rs == null || map == null) { Log("§D ❌ ResourceRespawnSystem/地图未就绪"); return; }

        // 找一格 OreVein（自由格）
        GridCoord? cell = null;
        var grid = GridSystem.Instance;
        for (int y = 6; y < map.height - 6 && cell == null; y += 3)
            for (int x = 6; x < map.width - 6; x += 3)
                if (MapGate.ReadAt(map, x, y) == FeatureType.OreVein && grid.GetOccupant(new GridCoord(x, y)) == null)
                { cell = new GridCoord(x, y); break; }
        if (!cell.HasValue) { Log("§D ❌ 未找到 OreVein 自由格"); return; }

        int kind = KindIndexOf(FeatureType.OreVein);
        int ci = (cell.Value.y / MapGenRules.ChunkSize) * rs.PoolChunkW + (cell.Value.x / MapGenRules.ChunkSize);
        int before = rs.PointsOf(ci, kind);
        var featBefore = MapGate.GetFeatureAt(cell.Value);

        // 守卫面：先把该格所属最近资源点部署守卫（幂等探针需要），再用删门
        // —— 简化：直接对最近资源点 DeployGuard（Tree/Mine/OreVein 命中即布）
        GuardDeploymentSystem.DeployGuard(grid.CoordToWorld(cell.Value));
        int guardBefore = GuardDeploymentSystem.Count;
        int lostEvents = 0;
        System.Action<GuardRegionLostEvent> onLost = _ => lostEvents++;
        EventBus.Subscribe(onLost);

        rs.HandleCellGathered(cell.Value);          // 采集完成（合并后唯一实现）
        int after = rs.PointsOf(ci, kind);
        var featAfter = MapGate.GetFeatureAt(cell.Value);
        int lostAfterFirst = lostEvents;
        int guardAfterFirst = GuardDeploymentSystem.Count;

        // 幂等：第二次调用（同一格·门内存在性校验 ⇒ 幂等跳过）
        rs.HandleCellGathered(cell.Value);
        int after2 = rs.PointsOf(ci, kind);
        int lostAfterSecond = lostEvents;
        // 幂等②：守卫通知直调（门内 `MapGate:308` 已调过一次 ⇒ 再来一次应为零效果）
        GuardDeploymentSystem.HandleResourceConsumed(cell.Value);
        int guardAfterSecond = GuardDeploymentSystem.Count;

        EventBus.Unsubscribe(onLost);

        Log("§D 格(" + cell.Value.x + "," + cell.Value.y + ") feature：改前=" + featBefore + " ⇒ 改后=**" + featAfter + "**"
            + (featAfter == FeatureType.Plain ? " ✅（翻 Plain）" : " "));
        Log("§D 池子读数（区块 ci=" + ci + " 类=OreVein）：改前=" + before + " ⇒ 改后=**" + after + "**（Δ=" + (after - before) + "·须 −1）");
        Log("§D 幂等（第二次 HandleCellGathered）：池子=" + after2 + "（与改后同 ⇒ " + (after2 == after ? "✅ 无二次扣减" : "❌") + "）");
        Log("§D 守卫面：部署后区域=" + guardBefore + " ⇒ 采集后=" + guardAfterFirst + "（Δ=" + (guardAfterFirst - guardBefore) + "）"
            + " GuardRegionLostEvent=" + lostAfterFirst + " 次；"
            + "再直调 HandleResourceConsumed 一次 ⇒ 区域=" + guardAfterSecond + " 事件累计=" + lostAfterSecond
            + " ⇒ **幂等=" + (guardAfterSecond == guardAfterFirst && lostAfterSecond == lostAfterFirst ? "成立（重复调用零效果）" : " 不幂等") + "**");
        Log("§D 清洁项证据：改前 3 处守卫失去通知（`MapGate:308`／`ResourceRespawnSystem.HandleTreeGathered:477`／"
            + "`Building.OnGatherCompleted:920`）⇒ 本批只留门内 `MapGate:308`（另两处随各路径退役）");
    }

    /// <summary>直接调生产入口（诊断用；隔离「格」与「链」）。</summary>
    private static bool rsConfirm(GridCoord cell)
        => ResourceRespawnSystem.HasInstance && ResourceRespawnSystem.Instance.ConfirmResourceGather(cell);

    private static int KindIndexOf(FeatureType f)
    {
        for (int t = 0; t < MapGenRules.ResourceKindCount; t++)
            if (MapGenRules.ResourceKindFeature[t] == f) return t;
        return -1;
    }

    // ========================================================================
    //  §E：判据 6（6-E）OreVein 格表来源：候选数对照 ＋ 反证 ＋ 12s 缓存代价
    // ========================================================================
    private static void Case_OreVeinSource()
    {
        Log("");
        Log("## §E 判据6（6-E）游荡锚点候选：OreVein 格表来源（只 OreVein·D617 口径）");
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Log("§E ❌ 地图未就绪"); return; }

        // 改前来源（本段世界＝目标态 ⇒ 无实体）：实体面计数（应 0）
        var reg = BuildingRegistry.Instance;
        int entitySrc = 0;
        if (reg != null)
            for (int i = 0; i < reg.All.Count; i++)
                if (reg.All[i] != null && reg.All[i].def != null && reg.All[i].def.isResourceNode && reg.All[i].IsActive) entitySrc++;
        // 改后来源：格表 OreVein 格数（＝缓存内容规模）
        int cellSrc = 0;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
                if (MapGate.ReadAt(map, x, y) == FeatureType.OreVein) cellSrc++;

        Log("§E 候选来源读数：实体面（`def.isResourceNode && IsActive`·改前来源）= **" + entitySrc + "**"
            + " ｜ 格表面 OreVein 格（改后来源）= **" + cellSrc + "**");
        Log("§E 对照口径：改前 `ore_vein` 实体 1:1 对应 OreVein 格 ⇒ 格表来源**保语义**（数量级一致）；"
            + " 未纳入 stone_pile/wood_pile（`isResourceNode=0` 系 DZ-054/D617 已裁口径）");

        // 反证：附近有 ore_vein ⇒ 候选应含该格（用反射调生产实现 `AppendOreVeinCells`）
        var mi = typeof(WanderStimulusProvider).GetMethod("AppendOreVeinCells",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (mi == null) { Log("§E ❌ 反射未命中 `AppendOreVeinCells`（实现改名？）"); return; }
        var list = new List<Vector2>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        mi.Invoke(null, new object[] { list });
        sw.Stop();
        double firstMs = sw.Elapsed.TotalMilliseconds;
        int firstCount = list.Count;
        var list2 = new List<Vector2>();
        sw.Restart();
        mi.Invoke(null, new object[] { list2 });   // 12s 内第二次 ⇒ 走缓存（不重扫）
        sw.Stop();
        double secondMs = sw.Elapsed.TotalMilliseconds;
        Log("§E 生产实现代价：首扫（全图 features 扫）" + firstMs.ToString("F3") + " ms → 候选 " + firstCount
            + " 个；12s 缓存内第二扫 **" + secondMs.ToString("F3") + " ms**（⇒ 免重扫·O(候选数) 复制）；"
            + "缓存 TTL=" + 12f + "s（静态共享·非每帧/非每 NPC）");

        // 反证：取一个已知 OreVein 格 ⇒ 其世界坐标应在候选中
        GridCoord? probe = null;
        for (int y = 4; y < map.height - 4 && probe == null; y += 5)
            for (int x = 4; x < map.width - 4; x += 5)
                if (MapGate.ReadAt(map, x, y) == FeatureType.OreVein) { probe = new GridCoord(x, y); break; }
        if (probe.HasValue)
        {
            var wp = GridSystem.Instance != null ? GridSystem.Instance.CoordToWorld(probe.Value) : Vector2.zero;
            bool hit = false;
            for (int i = 0; i < list.Count && !hit; i++)
                if ((list[i] - wp).sqrMagnitude < 0.0001f) hit = true;
            Log("§E 存在性反证：格(" + probe.Value.x + "," + probe.Value.y + ") 世界位 " + wp.ToString("F1")
                + " 在候选中 = **" + hit + "**" + (hit ? " ✅" : " ❌"));
        }
    }

    // ========================================================================
    //  §F：判据 7（R1）PickCellRadius 重推 ＋ castle 上沿命中 ＋ 代价
    // ========================================================================
    private static void Case_PickRadius()
    {
        Log("");
        Log("## §F 判据7（R1）拾取候选窗口半径重推 ＋ 存在性反证 ＋ 代价");
        // `PickCellRadius` 为 `internal`（Assembly-CSharp ⇒ Editor 不可见）⇒ 反射读（探针只读，不改可见性）
        var piR = typeof(MapGate).GetProperty("PickCellRadius", BindingFlags.NonPublic | BindingFlags.Static);
        int r = piR != null ? (int)piR.GetValue(null) : -1;
        Log("§F MapGate.PickCellRadius = **" + r + "**（格·反射读）⇒ 候选窗口 " + (2 * r + 1) + "×" + (2 * r + 1)
            + " 格；上限 PickCellRadiusMax=" + MapGate.PickCellRadiusMax
            + "（成本上界＝(2×8+1)²=289 次 O(1) 字典查）");

        // 找 castle（最大 sprite 样本）
        var reg = BuildingRegistry.Instance;
        Building castle = null;
        if (reg != null)
            for (int i = 0; i < reg.All.Count; i++)
            {
                var b = reg.All[i];
                if (b != null && b.def != null && b.def.id == "castle") { castle = b; break; }
            }
        if (castle == null) { Log("§F ⚠ 未找到 castle（改用最大 sprite 建筑）"); }
        var sr = castle != null ? castle.GetComponentInChildren<SpriteRenderer>() : null;
        if (sr == null) { Log("§F ❌ castle 无 SpriteRenderer（用例跳过）"); return; }
        var bb = sr.bounds;
        Log("§F castle sprite bounds：center=" + bb.center.ToString("F2") + " size=" + bb.size.ToString("F2")
            + "（半高 " + (bb.size.y * 0.5f).ToString("F2") + " 世界单位）");

        // 上沿可见点：x=中心、y=上沿内缩 0.1（在 sprite 内、远超 footprint）
        var pt = new Vector2(bb.center.x, bb.max.y - 0.1f);
        bool hitCastle = MapGate.PickAt(pt, out var h) && h.source == castle;
        Log("§F 存在性反证：点 sprite **上沿** " + pt.ToString("F2") + " ⇒ PickAt 命中="
            + (h.source != null ? h.source.name : "(null)") + " ⇒ 命中该 castle=**" + hitCastle + "**" + (hitCastle ? " ✅" : " "));

        // 旧窗口反证：该点所属格与 castle 主格的距离（> 1 ⇒ 改前 3×3 必落空）
        var grid = GridSystem.Instance;
        var cellOpt = grid != null ? grid.WorldToCoord(pt) : null;
        if (cellOpt.HasValue)
        {
            int d = Mathf.Max(Mathf.Abs(cellOpt.Value.x - castle.coord.x), Mathf.Abs(cellOpt.Value.y - castle.coord.y));
            Log("§F 旧窗口反证：该点格(" + cellOpt.Value.x + "," + cellOpt.Value.y + ") 距 castle 主格(" + castle.coord.x + "," + castle.coord.y
                + ") 切比雪夫距离 = **" + d + "** 格 ⇒ 改前 r=1（3×3）**落空**=" + (d > 1 ? "是（能力句原缺口坐实）" : "否"));
        }

        // 半高取样读数 ＋ 重扫代价（反射调私有 `ScanMaxSpriteHalf`）
        var miHalf = typeof(MapGate).GetMethod("ScanMaxSpriteHalf", BindingFlags.NonPublic | BindingFlags.Static);
        if (miHalf != null)
        {
            var swH = System.Diagnostics.Stopwatch.StartNew();
            float half = 0f;
            for (int i = 0; i < 20; i++) half = (float)miHalf.Invoke(null, null);
            swH.Stop();
            Log("§F 半高取样：maxSpriteHalf=" + half.ToString("F3") + " 世界单位（扫全体在册建筑 "
                + (BuildingRegistry.Instance != null ? BuildingRegistry.Instance.All.Count : -1) + " 座）；"
                + "重扫代价=" + (swH.Elapsed.TotalMilliseconds / 20).ToString("F3") + " ms/次（间隔 " + 10f + "s ⇒ 摊销可忽略）");
        }

        // 代价：单次 PickAt（命中点）＋ 候选计数
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 200; i++) MapGate.PickAt(pt, out _);   // 预热
        sw.Restart();
        int hits = 0;
        for (int i = 0; i < 2000; i++) if (MapGate.PickAt(pt, out _)) hits++;
        sw.Stop();
        Log("§F 代价：单次 PickAt（castle 上沿点·r=" + r + "）=" + (sw.Elapsed.TotalMilliseconds / 2000).ToString("F4")
            + " ms（2000 次均值·命中 " + hits + "）＝≈" + ((sw.Elapsed.TotalMilliseconds / 2000) / 16.6 * 100).ToString("F2") + "% 帧预算 @16.6ms");

        // 空白点对照
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++) MapGate.PickAt(new Vector2(bb.center.x, bb.center.y - 6f), out _);
        sw2.Stop();
        Log("§F 代价对照：单次 PickAt（空白点）=" + (sw2.Elapsed.TotalMilliseconds / 2000).ToString("F4") + " ms");
    }

    // ========================================================================
    //  §G：判据 8（R2）独立渲染对照 —— 像素隐藏法（外部观测列）
    //   方法：重叠点做 3 次 RT 采样（both / 隐藏A / 隐藏B）——
    //     「隐藏谁 ⇒ 像素变」者＝**像素实测最前者**（外部观测·非 PickAt 同源重算）；
    //     与 `PickAt` 返回者比对。覆盖 ① Pivot（单位-单位）② 跨层带（宝箱 5 ＞ 建筑/单位 1）。
    // ========================================================================
    private static IEnumerator Case_RenderCompare()
    {
        Log("");
        Log("## §G 判据8（R2）独立渲染对照（RT 像素隐藏法·外部观测列）");
        var cam = Camera.main;
        if (cam == null) { Log("§G ❌ Camera.main 不在场"); yield break; }
        var grid = GridSystem.Instance;
        if (grid == null) { Log("§G ❌ GridSystem 不在场"); yield break; }

        // ---- 用例① 单位-单位（Pivot 排序点）----
        var units = Object.FindObjectsOfType<UnitController>();
        UnitController ua = null, ub = null;
        for (int i = 0; i < units.Length; i++)
        {
            if (units[i] == null || units[i].GetComponent<SpriteRenderer>() == null) continue;
            if (units[i].GetFaction() != Faction.PlayerCamp) continue;
            if (ua == null) ua = units[i]; else { ub = units[i]; break; }
        }
        if (ua == null || ub == null) Log("§G 用例①（单位-单位）：❌ 玩家单位样本 <2（跳过）");
        else
        {
            var srA = ua.GetComponent<SpriteRenderer>();
            var srB = ub.GetComponent<SpriteRenderer>();
            var posA = ua.transform.position; var posB = ub.transform.position;
            var subA = grid.GetUnitCoord(ua); var subB = grid.GetUnitCoord(ub);
            // 冻结（**防 NPCBrain 位移把两点拉开** —— 首跑实测 0.2 偏移被漂移放大到 0.53 ⇒ bounds 不交）
            var sched0 = TaskScheduler.HasInstance ? TaskScheduler.Instance : null;
            var brainA = ua.GetComponent<NPCBrain>(); var brainB = ub.GetComponent<NPCBrain>();
            bool enA = brainA != null && brainA.enabled, enB = brainB != null && brainB.enabled;
            if (sched0 != null) { sched0.AbandonTask(ua.npcId); sched0.AbandonTask(ub.npcId); }
            if (brainA != null) brainA.enabled = false;
            if (brainB != null) brainB.enabled = false;
            // 拉到相机前近旁（G0-A 同款隔离位：不改变 renderer 参数，只移位）
            var center = cam.transform.position + cam.transform.forward * 10f;
            ua.transform.position = new Vector3(center.x, center.y + 0.02f, 0f);
            ub.transform.position = new Vector3(center.x, center.y - 0.18f, 0f);   // 小 y ⇒ 渲染更前（G0 结论）
            SyncUnitCell(grid, ua); SyncUnitCell(grid, ub);
            // ⚠️ **同帧**采样（不等帧）：NPCBrain 冻结已防主动移动，但同帧内完成「定位→取样→拾取→复原」
            //   可根除任何帧间漂移（首跑实测：等帧后两点被拉开 0.53 ⇒ bounds 不交）。
            bool overlap;
            var pts = GridPoints(srA, srB, 3, out overlap);
            var front = HideTestFrontMulti(cam, pts, srA, srB, out var ev);
            var pt = pts.Count > 0 ? pts[pts.Count / 2] : new Vector2(srA.bounds.center.x, srA.bounds.center.y);
            bool picked = MapGate.PickAt(pt, out var hit);
            Log("§G 用例①（Pivot·单位-单位）：A=" + ua.name + "(Pivot,t=" + ua.transform.position.y.ToString("F2")
                + ",bounds.c=" + srA.bounds.center.ToString("F2") + ") B=" + ub.name + "(t=" + ub.transform.position.y.ToString("F2")
                + ",bounds.c=" + srB.bounds.center.ToString("F2") + ") bounds相交=" + overlap + " 采样点=" + pt.ToString("F2"));
            Log("§G 用例① 像素读数列：" + ev);
            Log("§G 用例① 两列：**拾取返回者**=" + (picked && hit.source != null ? hit.source.name : "(null)")
                + " ｜ **像素实测最前者（外部观测）**=" + front
                + " ｜ 一致=" + (picked && hit.source != null && hit.source.name == front ? "✅" : "❌"));

            ua.transform.position = posA; ub.transform.position = posB;
            SyncUnitCell(grid, ua, subA); SyncUnitCell(grid, ub, subB);
            if (brainA != null) brainA.enabled = enA;
            if (brainB != null) brainB.enabled = enB;
            yield return null;
            Log("§G 用例① 实体位置与脑（enabled）已复原");
        }

        // ---- 用例② 跨层带（宝箱 sortingOrder=5 ＞ 建筑/单位 1）----
        var chestMgr = ChestManager.HasInstance ? ChestManager.Instance : null;
        var reg = BuildingRegistry.Instance;
        Building bld = null;
        if (reg != null)
            for (int i = 0; i < reg.All.Count; i++)
            {
                var b = reg.All[i];
                if (b != null && b.GetComponentInChildren<SpriteRenderer>() != null) { bld = b; break; }
            }
        if (chestMgr == null || bld == null) Log("§G 用例②（跨层带）： 宝箱管理器/建筑样本缺失（跳过）");
        else
        {
            var srB2 = bld.GetComponentInChildren<SpriteRenderer>();
            var bPos = bld.transform.position;
            // 建筑先移到**相机前隔离位**（G0-A 同款：避开地面 Tilemap 层干扰），宝箱再贴同一屏点
            var spot2 = cam.transform.position + cam.transform.forward * 10f;
            bld.transform.position = new Vector3(spot2.x, spot2.y, 0f);
            yield return null;
            var cellOpt = grid.WorldToCoord(spot2);
            if (cellOpt == null) Log("§G 用例②：❌ 隔离位越界（跳过）");
            else
            {
                var chest = chestMgr.SpawnChest(cellOpt.Value, new ResourcePack { wood = 1 }, Faction.PlayerCamp);
                yield return null;
                if (chest == null) Log("§G 用例②：❌ SpawnChest 返回 null（跳过）");
                else
                {
                    // 宝箱贴到建筑 sprite 中心（同一屏点；宝箱 order=5 ⇒ 层带应压制 order=1 的建筑）
                    chest.transform.position = new Vector3(srB2.bounds.center.x, srB2.bounds.center.y, 0f);
                    yield return null;
                    var srC = chest.GetComponent<SpriteRenderer>();
                    // 采样点集＝**宝箱 ∩ 建筑** AABB 内 3×3（多点：真图上"中心恰为透明像素"会让单点判不出）
                    bool ovC;
                    var ptsC = GridPoints(srC, srB2, 3, out ovC);
                    var frontC = HideTestFrontMulti(cam, ptsC, srC, srB2, out var evC);
                    var ptC = ptsC.Count > 0 ? ptsC[ptsC.Count / 2] : new Vector2(srC.bounds.center.x, srC.bounds.center.y);
                    bool pickedC = MapGate.PickAt(ptC, out var hitC);
                    Log("§G 用例②（跨层带·宝箱 vs 建筑）：宝箱 order=" + srC.sortingOrder + "（layer=" + srC.sortingLayerName + "）"
                        + " 建筑 order=" + srB2.sortingOrder + "（layer=" + srB2.sortingLayerName + "）"
                        + " bounds相交=" + ovC + " 采样点=" + ptC.ToString("F2")
                        + "（宝箱中心=" + srC.bounds.center.ToString("F2") + " 建筑中心=" + srB2.bounds.center.ToString("F2") + "）");
                    Log("§G 用例② 像素读数列：" + evC);
                    Log("§G 用例② 两列：**拾取返回者**=" + (pickedC && hitC.source != null ? hitC.source.name : "(null)")
                        + " ｜ **像素实测最前者（外部观测）**=" + frontC
                        + " ｜ 一致=" + (pickedC && hitC.source != null && hitC.source.name == frontC ? "✅" : "❌"));
                    chestMgr.Remove(chest);   // 用完即清（L-23 探针纪律）
                    Log("§G 用例② 宝箱已清理（Count=" + chestMgr.Count + "）");
                }
            }
            bld.transform.position = bPos;
        }
    }

    private static void SyncUnitCell(GridSystem grid, UnitController u)
    {
        grid.ExitCurrentCell(u);
        var sub = grid.WorldToSubCoord(u.transform.position);
        if (sub.HasValue) grid.TryEnter(u, sub.Value);
    }
    private static void SyncUnitCell(GridSystem grid, UnitController u, GridCoord? sub)
    {
        grid.ExitCurrentCell(u);
        if (sub.HasValue) grid.TryEnter(u, sub.Value);
    }

    /// <summary>A∩B 世界 AABB 内 **n×n 采样点集**（无交 ⇒ 空表 + false）。</summary>
    private static List<Vector2> GridPoints(SpriteRenderer a, SpriteRenderer b, int n, out bool overlap)
    {
        var list = new List<Vector2>();
        var mn = Vector3.Max(a.bounds.min, b.bounds.min);
        var mx = Vector3.Min(a.bounds.max, b.bounds.max);
        overlap = mx.x > mn.x && mx.y > mn.y;
        if (!overlap) return list;
        for (int iy = 0; iy < n; iy++)
            for (int ix = 0; ix < n; ix++)
                list.Add(new Vector2(Mathf.Lerp(mn.x, mx.x, (ix + 0.5f) / n),
                                     Mathf.Lerp(mn.y, mx.y, (iy + 0.5f) / n)));
        return list;
    }

    /// <summary>
    /// **多点像素隐藏法**（外部观测）：逐点做 3 次 RT 采样（both／隐藏 A／隐藏 B）；
    /// 「隐藏谁 ⇒ 该点像素变」者＝**像素实测最前者**；取**首个结论明确**的点定论（其余点读数一并落盘可复算）。
    /// 单点法在真图上有"中心恰为透明像素"的坑（本批实测：宝箱中心透明 ⇒ 单点判不出）⇒ 多点扫描。
    /// </summary>
    private static string HideTestFrontMulti(Camera cam, List<Vector2> pts, SpriteRenderer a, SpriteRenderer b, out string evidence)
    {
        var sb = new StringBuilder();
        string verdict = "不确定";
        for (int i = 0; i < pts.Count; i++)
        {
            var c0 = SampleAtWorld(cam, pts[i], 256);
            a.enabled = false; var cA = SampleAtWorld(cam, pts[i], 256); a.enabled = true;
            b.enabled = false; var cB = SampleAtWorld(cam, pts[i], 256); b.enabled = true;
            bool chA = ColorChanged(c0, cA), chB = ColorChanged(c0, cB);
            bool conclusive = chA ^ chB;
            sb.Append("#").Append(i).Append(pts[i].ToString("F2"))
              .Append(" both=").Append(Fmt(c0))
              .Append(" hideA=").Append(Fmt(cA)).Append("(").Append(chA ? "变" : "不变").Append(")")
              .Append(" hideB=").Append(Fmt(cB)).Append("(").Append(chB ? "变" : "不变").Append(")")
              .Append(conclusive ? " ★" : "").Append(" ; ");
            if (conclusive)
            {
                verdict = chA ? a.name : b.name;
                break;
            }
        }
        evidence = sb.ToString();
        return verdict;
    }

    private static bool ColorChanged(Color x, Color y)
        => Mathf.Abs(x.r - y.r) + Mathf.Abs(x.g - y.g) + Mathf.Abs(x.b - y.b) > 0.02f;
    private static string Fmt(Color c) => "(" + c.r.ToString("F2") + "," + c.g.ToString("F2") + "," + c.b.ToString("F2") + ")";

    /// <summary>把相机渲到 RT 并取「世界点投影处」像素（外部观测·不依赖任何拾取键）。</summary>
    private static Color SampleAtWorld(Camera cam, Vector3 world, int rtSize)
    {
        var rt = new RenderTexture(rtSize, rtSize, 24);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;
        var prevCulling = cam.cullingMask;
        cam.cullingMask = ~0;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rtSize, rtSize, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, rtSize, rtSize), 0, 0);
        tex.Apply();
        cam.targetTexture = prevTarget;
        cam.cullingMask = prevCulling;
        RenderTexture.active = prevActive;
        var vp = cam.WorldToViewportPoint(world);
        Color c = new Color(-1f, -1f, -1f, 1f);
        if (vp.z > 0f)
        {
            int px = Mathf.Clamp(Mathf.RoundToInt(vp.x * rtSize), 0, rtSize - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(vp.y * rtSize), 0, rtSize - 1);
            c = tex.GetPixel(px, py);
        }
        Object.Destroy(tex);
        rt.Release();
        Object.Destroy(rt);
        return c;
    }

    // ========================================================================
    //  §H：同 seed 逐格一致（features + climateZones hash ＋ 长度）
    // ========================================================================
    private static string HashFile() => Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh294_slice6_2", "hh294_slice6_2_hash.txt");

    /// <summary>hash 即时落盘；返回**上一段**的 hash（首段 ⇒ 0）。</summary>
    private static long WriteHash(long hash)
    {
        long prev = 0;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(HashFile()));
            if (File.Exists(HashFile()))
            {
                var line = File.ReadAllLines(HashFile())[0];
                int sp = line.IndexOf(' ');
                prev = System.Convert.ToInt64(sp > 0 ? line.Substring(0, sp) : line, 16);
            }
            File.WriteAllText(HashFile(), hash.ToString("X16") + " seed=" + PROBE_SEED + " at " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] hash 落盘失败：" + ex.Message); }
        return prev;
    }

    private static long HashMap()
    {
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null || map.features == null) return 0;
        unchecked
        {
            ulong h1 = 14695981039346656037UL;
            ulong h2 = 0;
            for (int i = 0; i < map.features.Length; i++)
            {
                h1 = (h1 ^ (ulong)(int)map.features[i]) * 1099511628211UL;
                h2 += (ulong)(int)map.features[i];
            }
            if (map.climateZones != null)
                for (int i = 0; i < map.climateZones.Length; i++)
                {
                    h1 = (h1 ^ ((ulong)(int)map.climateZones[i] + 7UL)) * 1099511628211UL;
                    h2 += (ulong)(int)map.climateZones[i] * 31UL;
                }
            return (long)(h1 ^ (h2 << 1));
        }
    }

    // ========================================================================
    //  收尾（L-32）＋ 落盘
    // ========================================================================
    private static void Finish()
    {
        Time.timeScale = 0f;
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(PROBE_SLOT);
        TestHarnessApi.ExitTestRun();

        WriteFile();
        Log("★ 收尾：真暂停(TS=0)+封盘=" + saved + "（槽=" + PROBE_SLOT + "）→ 退 Play（L-32 条文1）");
        _running = false;
        EditorApplication.ExitPlaymode();
    }

    private static void WriteFile()
    {
        try
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "hh294_slice6_2");
            Directory.CreateDirectory(dir);
            string stable = Path.Combine(dir, "hh294_slice6_2_probe.txt");
            File.WriteAllText(stable, Sb.ToString());
            string stamped = Path.Combine(dir, "hh294_slice6_2_probe_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(stamped, Sb.ToString());
            Debug.Log("[" + Tag + "] 落盘：" + stable + " ＋ 时间戳副本");
        }
        catch (System.Exception ex) { Debug.LogError("[" + Tag + "] 落盘失败：" + ex.Message); }
    }
}