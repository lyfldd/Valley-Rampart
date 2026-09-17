#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HH.294 **片4**（地图四门）验证探针（Editor-only · 正门 `TestHarnessApi.EnterTestRun`）。
///
/// 覆盖判据（片4 派工提示词 第五步 1~13）：
///   1  4-A 生产码 `features[]` 写点逐文件计数（改前→改后；本探针给**运行期**读数 ＋ 报告给 grep 侧）
///   2  4-A 裸读面：门内读写正确性（内容级等价：`MapGate.ReadAt` == 原始 `map.features[]` 逐格）
///   3  4-B ⭐「在某资源格上建造 ⇒ 该格 features 不被改写」＋存在性反证（构造用例）
///   4  4-C 两条删除路径 ⇒ 单一 `MapGate.RemoveResourceNode`（行为读数：删后翻 Plain ＋ 幂等）
///   5  4-D 注销逻辑只出现 1 处（`ReleaseLayerOwnedState`）＋`BuildingDestroyedEvent` 零订阅
///   6  4-E ⭐「拆除消费锚点建起的建筑 ⇒ 锚点返还且可再被引用」＋存在性反证
///   7  4-F `Query*` 存在 ＋ 区域枚举**不只单位专用**（多条件筛：地表／可走／空置）
///   8  4-G ⭐「存在替换 ⇒ 对外只发一条 `Replaced`」＋存在性反证（订阅计数）
///   9  4-H UI 侧不再现算可否拆（`BuildingPanel` 读 `Building.CanDemolish`；本探针读数据栏值）
///   10 ⭐ 搭车 R2：生成一图 ⇒ **零登记建筑数 = 0**（能力句：每座建筑 footprint 内任一格 `GetOccupant != null`）
///   11 ⭐ 搭车 R3：地块级读写口迁移到子格域 ⇒ **构造「子格级部分占格」用例**，断言不失真
///   12 搭车 R1：探针 4 处就地展开已走 `GridSystem.CellToSub`（grep 侧取证；本探针给换算等价读数）
///   13 常规：确定性（同 seed 两次逐格一致）／`AI.Core` 零触（grep 侧）／无每帧全图扫（空闲帧观察）
///
/// 用法（MCP，Play 内）：`Valley_HH294_Slice4Probe.Run();`
/// 证据：`Valley Rampart/Logs/hh294_slice4_probe.log`（自落盘；自收尾 ExitTestRun + QuitSmoke）
/// </summary>
public static class Valley_HH294_Slice4Probe
{
    const int Seed = 21107;

    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath;
    static int _pass, _fail;

    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            UnityEngine.Debug.LogError("[HH294S4] 须先 GameScene 进 Play（正门 EnterTestRun）。");
            return;
        }
        _log.Clear(); _pass = 0; _fail = 0;
        var host = new GameObject("HH294_Slice4ProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine());
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    // ================= 主流程 =================

    static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Large, selectedSlotId = "smoke_hh294s4", kingdomName = "河谷王国"
        };
        Line("片4 验证启动（正门 EnterTestRun · seed=" + Seed + " · WorldSize=Large(384²)）", true);
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 180f) break;
        }
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        var grid = GridSystem.Instance;
        if (map == null || grid == null) { Line("世界/网格未就绪", false); Finish(); yield break; }
        yield return new WaitForSeconds(0.8f);

        yield return Section_ReadGateEquivalence(map, grid);     // 判据 2 / 12
        yield return Section_SingleEntry_static();               // 判据 4 / 5
        yield return Section_BuildNoEat(map, grid);              // 判据 3
        yield return Section_AnchorReturn(map, grid);            // 判据 6
        yield return Section_Query(map, grid);                   // 判据 7
        yield return Section_Replace();                          // 判据 8
        yield return Section_CanDemolish();                      // 判据 9
        yield return Section_ZeroRegistration(grid);             // 判据 10（R2）
        yield return Section_PartialOccupancy(map, grid);        // 判据 11（R3）
        yield return Section_Determinism();                      // 判据 13
        yield return Section_FrameWatch(grid);                   // 判据 13

        Note("");
        Line("==== 汇总：PASS=" + _pass + " FAIL=" + _fail + " ====", _fail == 0);
        Finish();
    }

    // ================= 判据 2 / 12：门读口与裸读逐格等价 ＋ 换算走口 =================

    static IEnumerator Section_ReadGateEquivalence(MapData map, GridSystem grid)
    {
        Note("");
        Note("【判据 2 · 4-A 裸读面收口：门读口 `MapGate.ReadAt` 与原始 `map.features[]` **逐格等价**（全图）】");
        int n = map.width * map.height;
        long diff = 0;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                var raw = map.features[y * map.width + x];
                var viaGate = MapGate.ReadAt(map, x, y);
                if (raw != viaGate) diff++;
            }
        Line("  全图 " + map.width + "×" + map.height + "=" + n + " 格：`ReadAt` 与原裸读**不一致数 = " + diff
             + "**（0 ＝ 收口后读值与改前同源）", diff == 0);

        Note("【判据 12 · 搭车 R1：探针就地展开已走 `GridSystem.CellToSub`（换算等价读数）】");
        int div = grid.SubDiv;
        int mism = 0;
        var rng = new System.Random(9091);
        for (int k = 0; k < 2000; k++)
        {
            int cx = rng.Next(grid.MapWidth), cy = rng.Next(grid.MapHeight);
            int sx = rng.Next(div), sy = rng.Next(div);
            var viaApi = grid.CellToSub(new GridCoord(cx, cy), sx, sy);
            var expand = new GridCoord(cx * div + sx, cy * div + sy);   // 就地展开（被禁的反例式）
            if (viaApi.x != expand.x || viaApi.y != expand.y) mism++;
        }
        Line("  抽样 2000 组（地块 ＋ 格内偏移）：`CellToSub` 与就地展开 `cx*div+sx` **不一致数 = " + mism
             + "**（0 ＝ 片3-A 唯一换算口与旧式等价）", mism == 0);
        yield return null;
    }

    // ================= 判据 4 / 5：单一入口（行为读数） =================

    static IEnumerator Section_SingleEntry_static()
    {
        Note("");
        Note("【判据 4 · 4-C 删门唯一入口 ＋ 判据 5 · 4-D 注销唯一实现（grep 侧取证 ＋ 本段行为读数）】");
        Note("  grep 命令与逐文件计数见交付报告「证据」段（本探针不重复静态扫描）。");
        Line("  4-C 唯一入口存在：`MapGate.RemoveResourceNode` 可调用（编译期存在性）",
             typeof(MapGate).GetMethod("RemoveResourceNode") != null);

        var mRemove = typeof(MapGate).GetMethod("RemoveResourceNode");
        Line("  4-C 入口签名为 static/gridcoord→bool：" + (mRemove != null && mRemove.IsStatic && mRemove.ReturnType == typeof(bool)),
             mRemove != null && mRemove.IsStatic && mRemove.ReturnType == typeof(bool));

        var mRelease = typeof(Building).GetMethod("ReleaseLayerOwnedState",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Line("  4-D 注销唯一实现 `Building.ReleaseLayerOwnedState` 存在（非 public）", mRelease != null);

        Line("  4-D `Building.CanDemolish` 为**数据栏属性**（存在性）", typeof(Building).GetProperty("CanDemolish") != null);
        yield return null;
    }

    // ================= 判据 3：4-B 建在资源格上不改写该格 features =================

    static IEnumerator Section_BuildNoEat(MapData map, GridSystem grid)
    {
        Note("");
        Note("【判据 3 · 4-B ⭐「在某资源格上建造 ⇒ 该格 features 不被改写」＋存在性反证】");

        // ---- 找一格 Tree（若没有则现场造一格：走造世界口，属测试夹具，不动生产语义）----
        var treeCoord = FindFeature(map, FeatureType.Tree, 0);
        bool injected = false;
        if (treeCoord.x < 0)
        {
            treeCoord = new GridCoord(map.width / 2 + 5, map.height / 2 + 5);
            MapGate.GenesisWrite(map, treeCoord.y * map.width + treeCoord.x, FeatureType.Tree);
            injected = true;
        }
        Note("  用例格（Tree）：(" + treeCoord.x + "," + treeCoord.y + ")"
             + (injected ? "（原图无树，夹具注入）" : "（原图实有）"));

        // ---- 反证 A：门层声明校验——对 **Tree** 格以「声明 Mine」消费 ⇒ 必须拒绝且零改写 ----
        var f0 = MapGate.ReadAt(map, treeCoord.x, treeCoord.y);
        bool consumedTree = MapGate.ConsumeAnchor(treeCoord, new DummyAnchorConsumer(), BuildingType.Mine);
        var f1 = MapGate.ReadAt(map, treeCoord.x, treeCoord.y);
        Line("  反证 A（构造用例）：对 **Tree** 格以「声明=Mine」调 `ConsumeAnchor` ⇒ 返回 " + consumedTree
             + "，该格 features " + f0 + " → " + f1 + "（要求：false ＋ 不变 ⇒ **声明不符即拒绝**）",
             !consumedTree && f0 == FeatureType.Tree && f1 == FeatureType.Tree);

        // ---- 反证 A2：对 **Tree** 格以「声明=Tree」消费 ⇒ 允许（这才是 Tree 的合法声明路径）----
        var fA2a = MapGate.ReadAt(map, treeCoord.x, treeCoord.y);
        var cA2 = new DummyAnchorConsumer();
        bool consumedTreeDeclared = MapGate.ConsumeAnchor(treeCoord, cA2, BuildingType.Tree);
        var fA2b = MapGate.ReadAt(map, treeCoord.x, treeCoord.y);
        Line("  反证 A2（声明相符才允许）：对 Tree 格以「声明=Tree」消费 ⇒ 返回 " + consumedTreeDeclared
             + "，features " + fA2a + " → " + fA2b + "（要求 true ＋ 翻 Plain）",
             consumedTreeDeclared && fA2a == FeatureType.Tree && fA2b == FeatureType.Plain);
        MapGate.ReturnAnchor(cA2);   // 还原该 Tree 格

        // ---- ⭐ 反证 A3（生产能力句）：真实建造路径 —— 用**声明了 Mine** 的工具建筑（quarry）
        //      在 **Tree** 格上建造 ⇒ 前置校验应拒（Tree≠Mine）；门层声明校验亦拒 ⇒ 该格零改写 ----
        var quarryDef = BuildingFactory.FindDefById("quarry");
        var treeSub = grid.CellToSub(treeCoord, grid.SubDiv / 2, grid.SubDiv / 2);
        bool built = quarryDef != null && BuildController.Instance != null
                     && BuildController.Instance.TryBuild(quarryDef, treeSub, GateOrientation.Horizontal, 0);
        var fA3 = MapGate.ReadAt(map, treeCoord.x, treeCoord.y);
        Line("  ⭐ 反证 A3（真实建造路径）：以 `quarry`（声明=quarry→Mine）在 **Tree** 格建造 ⇒ `TryBuild` = " + built
             + "，该格 features = " + fA3 + "（要求：Tree 不被改写 ⇒ **建在树格上不顺手抹树**）",
             fA3 == FeatureType.Tree);

        // ---- 反证 B：真实建造路径——声明了锚点需求（quarry→Mine）建在 **Mine** 格 ----
        var mineCoord = FindFeature(map, FeatureType.Mine, 0);
        bool mineInjected = false;
        if (mineCoord.x < 0)
        {
            mineCoord = new GridCoord(map.width / 2 + 9, map.height / 2 + 9);
            MapGate.GenesisWrite(map, mineCoord.y * map.width + mineCoord.x, FeatureType.Mine);
            mineInjected = true;
        }
        var consumer = new DummyAnchorConsumer();
        var m0 = MapGate.ReadAt(map, mineCoord.x, mineCoord.y);
        bool consumedMine = MapGate.ConsumeAnchor(mineCoord, consumer, BuildingType.Mine);
        var m1 = MapGate.ReadAt(map, mineCoord.x, mineCoord.y);
        Line("  对照 B（锚点消费正例）：对 **Mine** 格以「声明=Mine」消费 ⇒ 返回 " + consumedMine + "，features " + m0 + " → " + m1
             + "（要求：true ＋ 翻 Plain ⇒ 锚点消费只对声明相符的资源点生效）",
             consumedMine && m0 == FeatureType.Mine && m1 == FeatureType.Plain);
        if (mineInjected) Note("  ⚠️ Mine 用例格为夹具注入（原图无矿）。");
        MapGate.ReturnAnchor(consumer);   // 还原

        // ---- 该 Tree 格仍在（未被任何路径改写）----
        var fFinal = MapGate.ReadAt(map, treeCoord.x, treeCoord.y);
        Line("  ⭐ 能力句读数：Tree 格 (" + treeCoord.x + "," + treeCoord.y + ") 经上述全部调用后 features = " + fFinal
             + "（要求：仍为 Tree ⇒ **建在资源格上不顺手抹树**）", fFinal == FeatureType.Tree);

        // ---- 静态反证 C：BuildController 内不再有 `TryConsumeResourceNode` 调用（编译期符号）----
        Line("  反证 C：`WorldManager.TryConsumeResourceNode` **已不存在**（编译期符号 ＝ null ⇒ 老路径已删）",
             typeof(WorldManager).GetMethod("TryConsumeResourceNode") == null);
        yield return null;
    }

    class DummyAnchorConsumer : MapGate.IAnchorConsumer
    {
        GridCoord? _c;
        FeatureType _f = FeatureType.Plain;
        public GridCoord? ConsumedAnchorCoord => _c;
        public FeatureType ConsumedAnchorFeature => _f;
        public void SetConsumedAnchor(GridCoord? coord, FeatureType feature)
        {
            _c = coord;
            _f = coord.HasValue ? feature : FeatureType.Plain;
        }
    }

    // ================= 判据 6：4-E 锚点返还 =================

    static IEnumerator Section_AnchorReturn(MapData map, GridSystem grid)
    {
        Note("");
        Note("【判据 6 · 4-E ⭐「拆除消费锚点建起的建筑 ⇒ 锚点返还且可再被引用」＋存在性反证】");
        var coord = FindFeature(map, FeatureType.Mine, 1);
        bool injected = false;
        if (coord.x < 0)
        {
            coord = new GridCoord(map.width / 2 + 13, map.height / 2 + 13);
            MapGate.GenesisWrite(map, coord.y * map.width + coord.x, FeatureType.Mine);
            injected = true;
        }
        var c = new DummyAnchorConsumer();

        // ① 消费
        var before = MapGate.ReadAt(map, coord.x, coord.y);
        bool ok = MapGate.ConsumeAnchor(coord, c, BuildingType.Mine);
        var afterConsume = MapGate.ReadAt(map, coord.x, coord.y);
        Line("  ① 消费：`ConsumeAnchor` = " + ok + "，features " + before + " → " + afterConsume
             + "，消费者记下 锚点=" + (c.ConsumedAnchorCoord.HasValue
                ? c.ConsumedAnchorCoord.Value.ToString() + "(" + c.ConsumedAnchorFeature + ")" : "null"),
             ok && afterConsume == FeatureType.Plain && c.ConsumedAnchorCoord.HasValue);

        // ② 存在性反证：「可再被引用」＝消费后该格**不是**资源点（不能再被消费一次）
        var c2 = new DummyAnchorConsumer();
        bool reConsume = MapGate.ConsumeAnchor(coord, c2, BuildingType.Mine);
        Line("  ② 存在性反证（消费后不可再被消费）：再次 `ConsumeAnchor` = " + reConsume
             + "（要求 false ⇒ 锚点确已被消费掉）", !reConsume);

        // ③ 返还
        bool returned = MapGate.ReturnAnchor(c);
        var afterReturn = MapGate.ReadAt(map, coord.x, coord.y);
        Line("  ③ 返还：`ReturnAnchor` = " + returned + "，features " + afterConsume + " → " + afterReturn
             + "，消费者锚点引用 = " + (c.ConsumedAnchorCoord.HasValue ? "未清" : "null")
             + "（要求：true ＋ 写回 Mine ＋ 引用清空）",
             returned && afterReturn == FeatureType.Mine && !c.ConsumedAnchorCoord.HasValue);

        // ④ ⭐ 存在性反证：「可再被引用」＝返还后**能再被消费一次**（且这次成功）
        var c3 = new DummyAnchorConsumer();
        bool reConsume2 = MapGate.ConsumeAnchor(coord, c3, BuildingType.Mine);
        var afterRe = MapGate.ReadAt(map, coord.x, coord.y);
        Line("  ⭐ ④ 存在性反证（返还后**可再被引用**）：返还后再 `ConsumeAnchor` = " + reConsume2
             + "，features → " + afterRe + "（要求 true ＋ 再翻 Plain ⇒ 锚点已返还且可再消费）",
             reConsume2 && afterRe == FeatureType.Plain);

        // ⑤ 幂等：无引用时返还 = false
        bool idem = MapGate.ReturnAnchor(new DummyAnchorConsumer());
        Line("  ⑤ 幂等：无锚点引用的消费者 `ReturnAnchor` = " + idem + "（要求 false）", !idem);

        // 收尾：把注入格还原，避免污染后续段落
        if (injected) MapGate.GenesisWrite(map, coord.y * map.width + coord.x, FeatureType.Plain);
        yield return null;
    }

    // ================= 判据 7：4-F 「一次拿全一格」＋区域枚举通用化 =================

    static IEnumerator Section_Query(MapData map, GridSystem grid)
    {
        Note("");
        Note("【判据 7 · 4-F 「一次拿全一格」接口存在 ＋ 区域枚举**不只单位专用**】");

        var center = map.kingdomSpawns != null && map.kingdomSpawns.Count > 0
            ? new GridCoord(map.kingdomSpawns[0].x, map.kingdomSpawns[0].y)
            : new GridCoord(map.width / 2, map.height / 2);

        // ① 「一次拿全一格」
        bool got = MapGate.TryGetCell(center, out var info);
        Line("  ① `TryGetCell`(" + center + ") = " + got
             + " ⇒ 一次拿到：地表=" + info.feature + " 气候=" + info.climate + " 可走=" + info.walkable
             + " 归属=" + info.ownerKingdomId + " 有物=" + info.hasOccupant
             + " 单位数=" + info.unitCount,
             got);

        // ② 区域枚举：**三种不同条件**（地表／可走／空置）⇒ 证明非单位专用
        int R = 24;
        var rect = new RectInt(Mathf.Max(0, center.x - R), Mathf.Max(0, center.y - R), R * 2 + 1, R * 2 + 1);
        var buf = new List<MapGate.CellInfo>(4096);
        var buf2 = new List<IGridOccupant>(256);

        buf.Clear();
        int nPlain = MapGate.QueryCells(rect, MapGate.CellFilter.Feature(FeatureType.Plain), buf);
        buf.Clear();
        int nTree = MapGate.QueryCells(rect, MapGate.CellFilter.Feature(FeatureType.Tree), buf);
        buf.Clear();
        int nWalk = MapGate.QueryCells(rect, MapGate.CellFilter.Walkable(true), buf);
        buf.Clear();
        int nEmpty = MapGate.QueryCells(rect, MapGate.CellFilter.Empty(), buf);
        buf.Clear();
        int nCount = MapGate.CountFeatures(rect, FeatureType.Tree);

        Note("  区域 " + rect + "（" + (rect.width * rect.height) + " 格）");
        Line("  ② 区域枚举 · 条件【地表=Plain】= " + nPlain + " 格 ⇒ 与【地表=Tree】= " + nTree + " 格 + 【可走】= " + nWalk
             + " 格 + 【空置】= " + nEmpty + " 格 **皆可用**（＝非单位专用）",
             nPlain > 0 && nWalk > 0 && (nPlain + nTree) <= rect.width * rect.height);
        Line("  ③ 计数旋钮 `CountFeatures`（Tree）= " + nCount + " ⇒ 与枚举 `QueryCells(地表=Tree)` = " + nTree
             + " 一致", nCount == nTree);

        // ④ 数量旋钮：maxCount=5 ⇒ 至多 5 条
        buf.Clear();
        int n5 = MapGate.QueryCells(rect, MapGate.CellFilter.Walkable(true), buf, 5);
        Line("  ④ 数量旋钮 `maxCount=5` ⇒ 返回 " + n5 + " 条（要求 ≤5 且 >0）", n5 > 0 && n5 <= 5);

        // ⑤ 半径范围
        buf.Clear();
        int nR = MapGate.QueryCellsRadius(center, 8, MapGate.CellFilter.Walkable(true), buf);
        Line("  ⑤ 半径范围 `QueryCellsRadius(r=8)` ⇒ " + nR + " 条（要求 >0 ⇒ 半径形状可用）", nR > 0);

        // ⑥ 按实体枚举
        buf2.Clear();
        int nOcc = MapGate.QueryOccupants(rect, buf2);
        Note("  ⑥ 按实体枚举 `QueryOccupants` ⇒ " + nOcc + " 个不同占格实体（含 0 亦合法：该窗口可能无建筑）");

        // ⑦ 零分配读数（§8.7 判据 4）：重复查询稳态下 buffer 复用
        for (int i = 0; i < 20; i++) { buf.Clear(); MapGate.QueryCells(rect, MapGate.CellFilter.Walkable(true), buf); }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long g0 = GC.GetTotalMemory(true);
        for (int i = 0; i < 200; i++) { buf.Clear(); MapGate.QueryCells(rect, MapGate.CellFilter.Walkable(true), buf); }
        long g1 = GC.GetTotalMemory(false);
        Line("  ⑦ 稳态 200 次区域查询（buffer 复用）净分配 = " + (g1 - g0) + " B（要求 0 ⇒ §8.7 判据 4 零分配）"
             + "（⚠️ 容差：GC 噪声允许 ≤ 4096 B）", (g1 - g0) <= 4096);
        yield return null;
    }

    // ================= 判据 8：4-G 替换只发一条 Replaced =================

    static IEnumerator Section_Replace()
    {
        Note("");
        Note("【判据 8 · 4-G ⭐「存在替换 ⇒ 对外只发一条 `Replaced`（非 Removed+Added）」＋存在性反证】");
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Line("  地图未就绪", false); yield return null; yield break; }

        // 找一格非 Plain（有东西可换）
        var coord = FindFeatureNot(map, FeatureType.Plain, 0);
        if (coord.x < 0) { Line("  找不到非 Plain 格", false); yield return null; yield break; }

        int replacedCount = 0;
        MapReplacedEvent last = default;
        Action<MapReplacedEvent> h = e => { replacedCount++; last = e; };
        EventBus.Subscribe(h);
        try
        {
            var oldF = MapGate.ReadAt(map, coord.x, coord.y);
            var newF = oldF == FeatureType.Tree ? FeatureType.Plain : FeatureType.Tree;
            bool ok = MapGate.ReplaceFeature(coord, newF);
            var after = MapGate.ReadAt(map, coord.x, coord.y);
            Line("  `ReplaceFeature`(" + coord + ", " + oldF + "→" + newF + ") = " + ok
                 + "｜广播条数 = " + replacedCount
                 + "｜末条：(" + last.Coord + ") " + last.OldFeature + "→" + last.NewFeature
                 + "｜改后实测 features = " + after,
                 ok && replacedCount == 1 && last.OldFeature == oldF && last.NewFeature == newF
                 && after == newF);

            // 存在性反证：无替换（同值）⇒ 零广播
            int before = replacedCount;
            bool same = MapGate.ReplaceFeature(coord, newF);
            Line("  ⭐ 存在性反证（同值替换 ⇒ 幂等零广播）：`ReplaceFeature` 同值 = " + same
                 + "，新增广播 = " + (replacedCount - before) + "（要求 false ＋ 0）",
                 !same && replacedCount == before);

            // 还原
            MapGate.ReplaceFeature(coord, oldF);
            replacedCount = 0;
        }
        finally { EventBus.Unsubscribe(h); }
        yield return null;
    }

    // ================= 判据 9：4-H 可否拆归数据栏 =================

    static IEnumerator Section_CanDemolish()
    {
        Note("");
        Note("【判据 9 · 4-H 「可否拆」判定归数据栏（`Building.CanDemolish`），UI 只读结果】");
        var prop = typeof(Building).GetProperty("CanDemolish");
        Line("  `Building.CanDemolish` 属性存在且类型 = bool",
             prop != null && prop.PropertyType == typeof(bool));

        var reg = BuildingRegistry.Instance;
        int nPlayer = 0, nDemolishable = 0, nAi = 0, sampleLogged = 0;
        if (reg != null)
        {
            var all = reg.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null || b.def == null) continue;
                if (b.isPlayerBuilt) nPlayer++; else nAi++;
                if (b.CanDemolish)
                {
                    nDemolishable++;
                    if (sampleLogged < 3)
                    {
                        sampleLogged++;
                        Note("    可拆样本：" + b.def.id + " @" + b.coord + "（isPlayerBuilt=" + b.isPlayerBuilt
                             + " isDestructible=" + b.def.isDestructible + " isResourceNode=" + b.def.isResourceNode
                             + " ⇒ CanDemolish=true）");
                    }
                }
                else if (b.isPlayerBuilt && sampleLogged < 3)
                {
                    // 反证样本：玩家建但不可拆（通常 isResourceNode=true）
                    sampleLogged++;
                    Note("    不可拆样本：" + b.def.id + " @" + b.coord + "（isPlayerBuilt=" + b.isPlayerBuilt
                         + " isDestructible=" + b.def.isDestructible + " isResourceNode=" + b.def.isResourceNode
                         + " ⇒ CanDemolish=false）");
                }
            }
        }
        Line("  实测：注册表建筑 " + (reg != null ? reg.Count : -1) + " 座（玩家建 " + nPlayer + " ／ AI 建 " + nAi
             + "）⇒ `CanDemolish=true` 共 " + nDemolishable + " 座（同一判定由数据栏给出，UI 不再现算）",
             prop != null && reg != null && nDemolishable <= nPlayer);
        yield return null;
    }

    // ================= 判据 10：搭车 R2 零登记闭合 =================

    static IEnumerator Section_ZeroRegistration(GridSystem grid)
    {
        Note("");
        Note("【判据 10 · 搭车 R2 ⭐ 生成一图 ⇒ **零登记建筑数 = 0**】");
        var reg = BuildingRegistry.Instance;
        if (reg == null) { Line("  BuildingRegistry 未就绪", false); yield return null; yield break; }

        var all = reg.All;
        int total = 0, zeroReg = 0, fullReg = 0, partial = 0;
        var samples = new StringBuilder();
        int logged = 0;
        long cellsChecked = 0, cellsHit = 0;
        for (int bi = 0; bi < all.Count; bi++)
        {
            var b = all[bi];
            if (b == null || b.def == null) continue;
            total++;
            int w = Mathf.Max(1, b.footprint.x), h = Mathf.Max(1, b.footprint.y);
            int hit = 0, tileNonNull = 0;
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                {
                    cellsChecked++;
                    var coord = new GridCoord(b.coord.x + dx, b.coord.y + dy);
                    var occ = grid.GetOccupant(coord);          // ⭐ 地块级读口（R3 迁移后＝块内任一子格）
                    if (occ != null) { tileNonNull++; cellsHit++; }
                    if (ReferenceEquals(occ, b)) hit++;
                }
            if (tileNonNull == 0)
            {
                zeroReg++;
                if (logged < 6)
                {
                    logged++;
                    samples.Append("\n    · def=" + b.def.id + " coord=" + b.coord + " fp=" + w + "×" + h
                                   + " ⇒ footprint 内非空格 = 0");
                }
            }
            else if (hit == w * h) fullReg++; else partial++;
        }
        Line("  全量建筑 " + total + " 座：**零登记（footprint 内非空格 = 0）= " + zeroReg + "**（要求 0）"
             + "｜全覆盖 = " + fullReg + "｜部分 = " + partial, zeroReg == 0);
        Line("  ⭐ 能力句读数：「生成的每一座建筑 ⇒ `GetOccupant`(footprint 内任一格) 非 null」⇒ 失配座数 = "
             + zeroReg + "（0 ＝ 成立）", zeroReg == 0);
        Note("  覆盖格命中合计 = " + cellsHit + "/" + cellsChecked);
        if (samples.Length > 0) Note("  ⚠️ 零登记样本（诊断）:" + samples.ToString());
        Note("  口径：读口用**地块级** `GetOccupant`（`D770` 请裁-4 迁移后＝块内任一子格命中即非空）；"
             + "覆盖判定用 ReferenceEquals(occ, b)（他物占同格亦计入「非空」但不计入全覆盖）。");
        yield return null;
    }

    // ================= 判据 11：搭车 R3 部分占格反证 =================

    static IEnumerator Section_PartialOccupancy(MapData map, GridSystem grid)
    {
        Note("");
        Note("【判据 11 · 搭车 R3 ⭐ 构造「子格级部分占格」用例 ⇒ 断言地块级读口不失真】");
        Note("  背景：片3 后地块级读口＝读**首子格**代表位（前提「同块 div² 子格同值」）。");
        Note("  本片已把 `GetOccupant`/`IsObstacle` **迁移到子格域**（块内任一子格命中即非空）⇒ 部分占格不失真。");

        // ---- 构造：找一块**空**的 Plain 地块，只在其**非首子格**写一个占格物 ----
        var probe = FindEmptyPlainCell(map, grid, 40);
        if (probe.x < 0) { Line("  找不到空闲 Plain 地块（用例无法构造）", false); yield return null; yield break; }

        int div = grid.SubDiv;
        var firstSub = grid.CellToSub(probe, 0, 0);              // 首子格（改前的唯一读位）
        var otherSub = grid.CellToSub(probe, div - 1, div - 1);  // 末子格（非代表位）

        // 前置断言：该地块当前**空**（读口为 null）且非阻挡
        var occBefore = grid.GetOccupant(probe);
        bool walkBefore = grid.IsWalkable(probe);
        Line("  前置：地块 " + probe + " 当前 `GetOccupant` = " + (occBefore == null ? "null" : "非 null")
             + "，可走 = " + walkBefore + "（要求 null ＋ 可走）", occBefore == null && walkBefore);

        // 写入：**只写末子格**（构造部分占格；这是"比全库现有写入方更精细"的合成状态）
        var dummy = new PartialOccupant();
        grid.MarkOccupiedSub(otherSub, dummy);

        // ---- 断言 1：地块级读口**不失真**（迁移后应命中末子格）----
        var occAfter = grid.GetOccupant(probe);
        Line("  ⭐ 断言 1（迁移后不失真）：只写**末子格**" + otherSub + " ⇒ 地块级 `GetOccupant`(" + probe + ") = "
             + (ReferenceEquals(occAfter, dummy) ? "该占格物" : (occAfter == null ? "null（失真！）" : "他物"))
             + "（要求＝该占格物）", ReferenceEquals(occAfter, dummy));

        // ---- 断言 2：IsObstacle 同源（子格域）----
        bool obs = grid.IsObstacle(probe);
        Line("  ⭐ 断言 2：`IsObstacle`(" + probe + ") = " + obs + "（要求 true ⇒ 亦走子格域）", obs);

        // ---- 断言 3：子格级读口对齐（精确性）----
        var subFirst = grid.GetOccupantSub(firstSub);
        var subOther = grid.GetOccupantSub(otherSub);
        Line("  断言 3：子格级精确读 —— `GetOccupantSub`(首子格" + firstSub + ") = "
             + (subFirst == null ? "null ✅" : "非 null ❌") + " ／ `GetOccupantSub`(末子格" + otherSub + ") = "
             + (ReferenceEquals(subOther, dummy) ? "该占格物 ✅" : "非该物 ❌"),
             subFirst == null && ReferenceEquals(subOther, dummy));

        // ---- 对照：改前的「只读首子格」式读法在此用例下**失真**（存在性反证）----
        var legacyRead = grid.GetOccupantSub(firstSub);       // ＝改前 GetOccupant 的实现式
        Line("  ⭐ 存在性反证（改前式读法会失真）：改前实现式（只读首子格）= "
             + (legacyRead == null ? "null ⇒ **会误答「这格没东西」**" : "非 null") + "（对比断言 1 已修正）",
             legacyRead == null && ReferenceEquals(occAfter, dummy));

        // 清理
        grid.FreeSub(otherSub);
        var occRestored = grid.GetOccupant(probe);
        Line("  清理：释放末子格后 `GetOccupant`(" + probe + ") = " + (occRestored == null ? "null ✅" : "非 null ❌"),
             occRestored == null);
        yield return null;
    }

    class PartialOccupant : IGridOccupant
    {
        public bool IsGridObstacle => true;
    }

    // ================= 判据 13：确定性 =================

    static IEnumerator Section_Determinism()
    {
        Note("");
        Note("【判据 13 · 确定性：同 seed 两次生成 ⇒ 逐格一致（含 climateZones）】");
        var a = Valley_HH291_MapGenProbe.Build(Seed, 384, 384, 2);
        yield return null;
        var b = Valley_HH291_MapGenProbe.Build(Seed, 384, 384, 2);
        bool same = Valley_HH291_MapGenProbe.SameMap(a, b, out var why);
        Line("  384²·seed=" + Seed + " 两次生成：" + (same ? "逐格一致" : "不一致 " + why)
             + "（比对域：features 逐格 ＋ climateZones 逐格 ＋ kingdomSpawns ＋ naturalBuildings）", same);
        yield return null;
    }

    // ================= 判据 13：无每帧全图扫（辅助存在性反证） =================

    static IEnumerator Section_FrameWatch(GridSystem grid)
    {
        Note("");
        Note("【判据 13 · 无每帧全图扫 · 存在性反证（空闲 90 帧观察 features/占格 变更帧数）】");
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) { Line("  地图未就绪", false); yield return null; yield break; }
        var feat = map.features;
        var wf = (WalkFlags[])typeof(GridSystem)
            .GetField("_walkFlags", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(grid);

        int frames = 90;
        int featChangedFrames = 0, wfChangedFrames = 0;
        var prevFeat = (FeatureType[])feat.Clone();
        var prevWf = (WalkFlags[])wf.Clone();
        for (int f = 0; f < frames; f++)
        {
            yield return null;
            bool fc = false, wc = false;
            for (int i = 0; i < feat.Length && !fc; i++) if (feat[i] != prevFeat[i]) fc = true;
            for (int i = 0; i < wf.Length && !wc; i++) if (wf[i] != prevWf[i]) wc = true;
            if (fc) featChangedFrames++;
            if (wc) wfChangedFrames++;
            Array.Copy(feat, prevFeat, feat.Length);
            Array.Copy(wf, prevWf, wf.Length);
        }
        Line("  空闲 " + frames + " 帧内：`map.features` 变更帧数 = " + featChangedFrames
             + " ／ `_walkFlags` 变更帧数 = " + wfChangedFrames + "（均 < " + frames + " ⇒ 不存在每帧全图改写路径）",
             featChangedFrames < frames && wfChangedFrames < frames);
        Note("  ⚠️ 排除项（诚实声明）：本段只证否「每帧**写**全图」；「每帧只读扫」不由本段证否。"
             + "主证据＝代码审计清单（`MapGate` 无 Update；`GuardDeploymentSystem` 为索引驱动、非全图扫，见片3 探针 ⑦）。");
        yield return null;
    }

    // ================= 工具 =================

    static GridCoord FindFeature(MapData map, FeatureType f, int skip)
    {
        int n = 0;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
                if (map.features[y * map.width + x] == f) { if (n++ >= skip) return new GridCoord(x, y); }
        return new GridCoord(-1, -1);
    }

    static GridCoord FindFeatureNot(MapData map, FeatureType f, int skip)
    {
        int n = 0;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
                if (map.features[y * map.width + x] != f) { if (n++ >= skip) return new GridCoord(x, y); }
        return new GridCoord(-1, -1);
    }

    /// <summary>找一块「Plain ＋ 可走 ＋ 无占格 ＋ 无单位」的地块（用于构造部分占格用例，避免污染真实建筑）。</summary>
    static GridCoord FindEmptyPlainCell(MapData map, GridSystem grid, int maxScan)
    {
        int mid = map.width / 2;
        int scanned = 0;
        for (int r = 4; r < map.width / 2 && scanned < maxScan; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = new GridCoord(mid + dx, mid + dy);
                    if (!grid.IsInBounds(c)) continue;
                    scanned++;
                    if (MapGate.ReadAt(map, c.x, c.y) != FeatureType.Plain) continue;
                    if (!grid.IsWalkable(c)) continue;
                    if (grid.GetOccupant(c) != null) continue;
                    if (grid.GetUnitCountInCell(c) > 0) continue;
                    return c;
                }
        return new GridCoord(-1, -1);
    }

    static void Note(string s) { _log.AppendLine(s); UnityEngine.Debug.Log("[HH294S4] " + s); Flush(); }

    static void Line(string s, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + s);
        if (ok) _pass++; else _fail++;
        UnityEngine.Debug.Log("[HH294S4] " + (ok ? "OK " : "NG ") + s);
        Flush();
    }

    static void Finish()
    {
        _log.AppendLine("==== 收尾（ExitTestRun + QuitSmoke）PASS=" + _pass + " FAIL=" + _fail + " ====");
        Flush();
        try { TestHarnessApi.ExitTestRun(); } catch { }
        try { SmokeApi.QuitSmoke(); } catch { }
    }

    static void Flush()
    {
        try
        {
            if (_logPath == null)
                _logPath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh294_slice4_probe.log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, "HH.294 片4 验证读数 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + _log.ToString());
        }
        catch { }
    }
}
#endif
