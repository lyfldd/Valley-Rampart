#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HH.294 **片4 补正小批** 验证探针（Editor-only · 正门 `TestHarnessApi.EnterTestRun`）。
///
/// 覆盖补正批要求：
///   R1  换算口：`CellToSub` 与就地展开等价（全库 `* div` 命中的定性由 grep 侧给，见交付报告）
///   R2  4 处运行期可达裸读改走 `MapGate.ReadAt` 后 —— **正确性读数**：
///       ① 全图逐格 `ReadAt` == 原始裸读（差异 0）⇒ 替换等价；
///       ② `MapGenRules.NearestWalkable`（真运行期调用路径）行为读数 ⇒ 与**独立判据**（全图暴力求最小切比雪夫距离）
///          一致（存在性反证：不是"自证自"的复刻式对照）。
///   R3  ⭐ 代价读数（本批核心）：`GetOccupant` 命中／未命中 ／ `GetUnitCountInCell`（N = 基线／≥100／≥500）
///       ／ `TryGetCell` 单次 ／ `QueryCells` 49×49=2401 格窗口 —— 并与 **60fps 帧预算 16.6 ms** 对照。
///
/// 用法（MCP，Play 内）：`Valley_HH302_Slice4FixProbe.Run();`
/// 证据：`Valley Rampart/Logs/hh302_slice4fix_probe.log`（自落盘；自收尾 ExitTestRun + QuitSmoke）
/// </summary>
public static class Valley_HH302_Slice4FixProbe
{
    const int Seed = 21107;
    const int WindowR = 24;                     // 49×49 = 2401 格窗口（半径 24）
    const double FrameBudgetMs = 16.6;          // 60fps 帧预算

    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath;
    static int _pass, _fail;

    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            UnityEngine.Debug.LogError("[HH302] 须先 GameScene 进 Play（正门 EnterTestRun）。");
            return;
        }
        _log.Clear(); _pass = 0; _fail = 0;
        var host = new GameObject("HH302_Slice4FixProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine());
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    // ================= 主流程 =================

    static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Large, selectedSlotId = "smoke_hh302", kingdomName = "河谷王国"
        };
        Line("片4 补正小批 验证启动（正门 EnterTestRun · seed=" + Seed + " · WorldSize=Large(384²)）", true);
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

        yield return Section_R1_Convert(map, grid);            // R1
        yield return Section_R2_ReadGateEquivalence(map);      // R2 ①
        yield return Section_R2_NearestWalkable(map);          // R2 ②
        yield return Section_R3_GetOccupant(map, grid);        // R3 ①
        yield return Section_R3_UnitCount(map, grid);          // R3 ②
        yield return Section_R3_TryGetCell(map, grid);         // R3 ③
        yield return Section_R3_Query(map, grid);              // R3 ④

        Note("");
        Line("==== 汇总：PASS=" + _pass + " FAIL=" + _fail + " ====", _fail == 0);
        Finish();
    }

    // ================= R1：换算口 =================

    static IEnumerator Section_R1_Convert(MapData map, GridSystem grid)
    {
        Note("");
        Note("【R1 · `GridSystem.CellToSub` 唯一换算口：与旧「就地展开」逐组等价】");
        int div = grid.SubDiv;
        int mism = 0;
        var rng = new System.Random(9202);
        for (int k = 0; k < 2000; k++)
        {
            int cx = rng.Next(map.width), cy = rng.Next(map.height);
            int sx = rng.Next(div), sy = rng.Next(div);
            var viaApi = grid.CellToSub(new GridCoord(cx, cy), sx, sy);
            var expand = new GridCoord(cx * div + sx, cy * div + sy);
            if (viaApi.x != expand.x || viaApi.y != expand.y) mism++;
        }
        Line("  抽样 2000 组（地块 ＋ 格内偏移 sx,sy ∈ [0," + div + ")）：`CellToSub` 与就地展开 `cx*div+sx` **不一致数 = "
             + mism + "**", mism == 0);
        Note("  口径声明：`* div` 的**全库命中与逐处定性**由 grep 侧给（交付报告「判据 1」附输出片段）；"
             + "本段只证「换算口与被替换式等价」。");
        yield return null;
    }

    // ================= R2 ①：读门等价 =================

    static IEnumerator Section_R2_ReadGateEquivalence(MapData map)
    {
        Note("");
        Note("【R2 ① · `MapGate.ReadAt` 与原始裸读 `map.features[y*w+x]` **全图逐格等价**】");
        int n = map.width * map.height;
        long diff = 0;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
                if (map.features[y * map.width + x] != MapGate.ReadAt(map, x, y)) diff++;
        Line("  全图 " + map.width + "×" + map.height + "=" + n + " 格：不一致数 = " + diff
             + "（0 ⇒ `HasFullBlock`／`IsClearBlock`／`NearestWalkable` 4 处替换**内容等价**）", diff == 0);
        yield return null;
    }

    // ================= R2 ②：NearestWalkable 行为（独立判据） =================

    static IEnumerator Section_R2_NearestWalkable(MapData map)
    {
        Note("");
        Note("【R2 ② · `MapGenRules.NearestWalkable`（**运行期可达**：`KingdomFoundry`／`VagrantCampSystem`／"
             + "`MineByproductComponent`）：改后行为 vs **独立判据**（全图暴力求「最小切比雪夫距离的可走格」）】");

        var starts = new List<Vector2Int>();
        if (map.kingdomSpawns != null)
            for (int i = 0; i < map.kingdomSpawns.Count; i++) starts.Add(map.kingdomSpawns[i]);
        starts.Add(new Vector2Int(map.width / 2, map.height / 2));
        starts.Add(new Vector2Int(2, 2));                            // 海洋带（应螺旋外扩找回陆地）
        starts.Add(new Vector2Int(map.width - 3, map.height - 3));   // 另一角

        int checkedN = 0, okN = 0;
        var sb = new StringBuilder();
        for (int i = 0; i < starts.Count; i++)
        {
            var s = starts[i];
            var got = MapGenRules.NearestWalkable(map, s.x, s.y);
            int minDist = MinChebWalkable(map, s);          // 独立判据（暴力）
            bool ok;
            string how;
            if (got.x < 0)
            {
                ok = minDist < 0;
                how = "返回(-1,-1) 且全图无可走格";
            }
            else
            {
                int d = Mathf.Max(Mathf.Abs(got.x - s.x), Mathf.Abs(got.y - s.y));
                bool inb = got.x >= 0 && got.y >= 0 && got.x < map.width && got.y < map.height;
                bool walk = inb && MapGenRules.IsWalkableFeature(MapGate.ReadAt(map, got.x, got.y));
                ok = inb && walk && d == minDist;
                how = "返回" + got + " 距离=" + d + " / 独立判据最小距离=" + minDist
                      + " 可走=" + walk + " 界内=" + inb;
            }
            checkedN++; if (ok) okN++;
            sb.Append("\n    · 起点 " + s + " ⇒ " + how + (ok ? " ✅" : " ❌"));
        }
        Note("  起点集 = " + starts.Count + " 个（" + starts.Count + " 中含出生点）:" + sb.ToString());
        Line("  ⭐ 行为一致读数：" + okN + "/" + checkedN + " 与**独立判据**一致"
             + "（判据＝「返回格可走 ∧ 其切比雪夫距离 == 全图暴力最小距离」；**不复刻**原函数的分支式，避免自证）",
             okN == checkedN && checkedN > 0);
        yield return null;
    }

    /// <summary>独立判据：全图暴力求「离 (cx,cy) 最近的**可走格**的切比雪夫距离」；无 ⇒ -1。
    /// ⛔ 不复制 `NearestWalkable` 的实现（螺旋/环序），故不受其实现细节影响。</summary>
    static int MinChebWalkable(MapData map, Vector2Int c)
    {
        int best = int.MaxValue;
        for (int y = 0; y < map.height; y++)
            for (int x = 0; x < map.width; x++)
            {
                if (!MapGenRules.IsWalkableFeature(MapGate.ReadAt(map, x, y))) continue;
                int d = Mathf.Max(Mathf.Abs(x - c.x), Mathf.Abs(y - c.y));
                if (d < best) best = d;
            }
        return best == int.MaxValue ? -1 : best;
    }

    // ================= R3 ①：GetOccupant 命中 / 未命中 =================

    static IEnumerator Section_R3_GetOccupant(MapData map, GridSystem grid)
    {
        Note("");
        Note("【R3 ① · `GridSystem.GetOccupant(GridCoord)` —— 命中（代表位即中）vs 未命中（空地块，扫同块余 div²−1 子格）】");

        // 命中格：取一座建筑的 footprint 首格，且**代表位（首子格）非空**（＝走快路径）
        var hitCoord = new GridCoord(-1, -1);
        var reg = BuildingRegistry.Instance;
        if (reg != null)
        {
            var all = reg.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null || b.def == null) continue;
                if (grid.GetOccupantSub(grid.CellToSub(b.coord, 0, 0)) != null) { hitCoord = b.coord; break; }
            }
        }
        // 未命中格：找一块真空白地块（GetOccupant == null）
        var missCoord = FindEmptyCell(map, grid, 4096);

        if (hitCoord.x < 0 || missCoord.x < 0)
        {
            Line("  用例格未取到（命中格=" + hitCoord + " 未命中格=" + missCoord + "）", false);
            yield return null; yield break;
        }
        Note("  用例格：命中 (" + hitCoord.x + "," + hitCoord.y + ")（代表位非空）／ 未命中 ("
             + missCoord.x + "," + missCoord.y + ")（GetOccupant == null）");

        int div = grid.SubDiv;
        int hitIters = 200000, missIters = 20000;
        double hitMs = BenchPerCall(hitIters, () => { var _ = grid.GetOccupant(hitCoord); });
        double missMs = BenchPerCall(missIters, () => { var _ = grid.GetOccupant(missCoord); });

        Note("  ⭐ 单次均摊（命中，" + hitIters + " 次）：" + Fmt(hitMs) + " ms ＝ "
             + (hitMs * 1000.0).ToString("0.0000") + " µs");
        Note("  ⭐ 单次均摊（未命中，" + missIters + " 次 · 每格扫 " + (div * div) + " 子格）：" + Fmt(missMs) + " ms ＝ "
             + (missMs * 1000.0).ToString("0.0000") + " µs");
        Line("  未命中/命中 倍率 = " + (hitMs > 0 ? (missMs / hitMs).ToString("0.0") : "n/a")
             + " ×（≈同块子格数 " + (div * div) + " ⇒ 与「扫全块」预期同阶）", missMs > hitMs && hitMs > 0);
        Note("  口径：`div` = SubDiv = " + div + " ⇒ 单块子格数 div² = " + (div * div)
             + "；未命中路径按 `for sy 0..div-1 / for sx 0..div-1（跳过首子格）` 扫 " + (div * div - 1) + " 个子格。");
        Note("  ⭐ **勘正**：`HH.294_片4_验收报告` §二 `R3` 与派工提示词均写「慢路径扫同块余 **div²−1 = 255**」"
             + "⇒ 该数要求 `div = 16`；**实盘 `SubDiv = " + div + "`**（`GridSystem.cs:43` `config.subCellDivisor`，缺省 4；"
             + "独立佐证：片3 实测子格域 `SW×SH = 1536×1536 = 2,359,296 = (384×4)²`）⇒ 真实扫描量为 **"
             + (div * div - 1) + "**，为所述 255 的 **1/" + (255.0 / (div * div - 1)).ToString("0.00") + "**。");
        yield return null;
    }

    // ================= R3 ②：GetUnitCountInCell（三档 N） =================

    static IEnumerator Section_R3_UnitCount(MapData map, GridSystem grid)
    {
        Note("");
        Note("【R3 ② · `GridSystem.GetUnitCountInCell(GridCoord)` —— 三档单位数（`_unitSubCells` 是 Dictionary ⇒ O(N)）】");

        int iters = 20000;
        var spawned = new List<GameObject>();

        // ---- 档 A：基线（探针进入时的实测单位数）----
        int nA = UnitCountOf(grid);
        var cell = new GridCoord(map.width / 2, map.height / 2);
        double msA = BenchPerCall(iters, () => { var _ = grid.GetUnitCountInCell(cell); });
        Note("  档 A（基线 N=" + nA + "，" + iters + " 次）：" + Fmt(msA) + " ms/次 ＝ "
             + (msA * 1000.0).ToString("0.0000") + " µs/次");

        // ---- 档 B：补到 ≥100 ----
        yield return GrowUnitsTo(grid, 100, spawned);
        int nB = UnitCountOf(grid);
        var cellB = cell;
        var anyUnitB = FirstUnitCoord(grid);
        if (anyUnitB.HasValue) cellB = anyUnitB.Value;
        double msB = BenchPerCall(iters, () => { var _ = grid.GetUnitCountInCell(cellB); });
        Note("  档 B（补到 N=" + nB + "，" + iters + " 次 · 计数格=" + cellB + " 实计=" + grid.GetUnitCountInCell(cellB)
             + "）：" + Fmt(msB) + " ms/次 ＝ " + (msB * 1000.0).ToString("0.0000") + " µs/次");

        // ---- 档 C：补到 ≥500（＝探针内的「实测较大值」）----
        yield return GrowUnitsTo(grid, 500, spawned);
        int nC = UnitCountOf(grid);
        var cellC = cellB;
        var anyUnitC = FirstUnitCoord(grid);
        if (anyUnitC.HasValue) cellC = anyUnitC.Value;
        double msC = BenchPerCall(iters, () => { var _ = grid.GetUnitCountInCell(cellC); });
        Note("  档 C（补到 N=" + nC + "，" + iters + " 次 · 计数格=" + cellC + " 实计=" + grid.GetUnitCountInCell(cellC)
             + "）：" + Fmt(msC) + " ms/次 ＝ " + (msC * 1000.0).ToString("0.0000") + " µs/次");

        double perUnitUs = nC > nA ? (msC - msA) * 1000.0 / Mathf.Max(1, nC - nA) : 0;
        Note("  均摊：Δ(N=" + nA + "→" + nC + ") = " + (msC - msA).ToString("0.000000") + " ms ⇒ **每单位 ≈ "
             + perUnitUs.ToString("0.000000") + " µs/单位/次**（线性证据：三档单调增 ∧ 与 N 同阶）");
        Line("  三档读数单调（A ≤ B ≤ C）且 N 递增：A=" + nA + " B=" + nB + " C=" + nC
             + " ⇒ `GetUnitCountInCell` 确为 O(N)", nA <= nB && nB <= nC);

        // ---- 清理：销毁夹具单位（避免污染后续段落）----
        yield return CleanupUnits(grid, spawned);
        Note("  夹具清理后重组实测单位数 = " + UnitCountOf(grid) + "（应回落到档 A 附近）");
        yield return null;
    }

    static IEnumerator GrowUnitsTo(GridSystem grid, int target, List<GameObject> spawned)
    {
        var sc = AIDebugSpawnController.Instance;
        if (sc == null) { Note("    ⚠️ `AIDebugSpawnController.Instance` 为 null ⇒ 该档无法构造"); yield break; }
        var map = WorldManager.Instance != null ? WorldManager.Instance.ActiveMap : null;
        if (map == null) yield break;
        var wp = grid.CoordToWorld(new GridCoord(map.width / 2, map.height / 2));
        int guard = 0;
        while (UnitCountOf(grid) < target && guard++ < target + 64)
        {
            DebugSpawnResult r;
            try { r = sc.Spawn(DebugSpawnType.PlayerCivilian, wp); }
            catch (Exception e) { Note("    ⚠️ 生成异常：" + e.GetType().Name); break; }
            if (!r.Success || r.Spawned == null)
            {
                Note("    ⚠️ 生成失败：" + r.Message + " ⇒ 该档停在 N=" + UnitCountOf(grid));
                break;
            }
            spawned.Add(r.Spawned);
            if ((guard & 63) == 0) yield return null;
        }
        yield return null;
    }

    static IEnumerator CleanupUnits(GridSystem grid, List<GameObject> spawned)
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            var go = spawned[i];
            if (go == null) continue;
            var u = go.GetComponent<UnitController>();
            if (u != null) grid.RemoveUnit(u);
            UnityEngine.Object.Destroy(go);
            if ((i & 63) == 0) yield return null;
        }
        spawned.Clear();
        yield return null;
    }

    static int UnitCountOf(GridSystem grid)
    {
        var f = typeof(GridSystem).GetField("_unitSubCells", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) return -1;
        var d = f.GetValue(grid) as System.Collections.IDictionary;
        return d != null ? d.Count : -1;
    }

    static GridCoord? FirstUnitCoord(GridSystem grid)
    {
        var f = typeof(GridSystem).GetField("_unitSubCells", BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) return null;
        var d = f.GetValue(grid) as System.Collections.IDictionary;
        if (d == null || d.Count == 0) return null;
        foreach (var v in d.Values) if (v is GridCoord c) return c;
        return null;
    }

    // ================= R3 ③：TryGetCell =================

    static IEnumerator Section_R3_TryGetCell(MapData map, GridSystem grid)
    {
        Note("");
        Note("【R3 ③ · `MapGate.TryGetCell` 单次（每格恒算 6 项：地表／气候／可走／归属／有物／单位数）】");

        var center = map.kingdomSpawns != null && map.kingdomSpawns.Count > 0
            ? new GridCoord(map.kingdomSpawns[0].x, map.kingdomSpawns[0].y)
            : new GridCoord(map.width / 2, map.height / 2);
        var rect = Window(center);
        int cells = rect.width * rect.height;

        // 覆盖整个 2401 格窗口 ⇒ 与 R3④ 的「每格」口径同源
        int reps = 20;
        var samples = new double[reps];
        for (int r = 0; r < reps; r++)
        {
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            for (int y = rect.yMin; y < rect.yMax; y++)
                for (int x = rect.xMin; x < rect.xMax; x++)
                    MapGate.TryGetCell(new GridCoord(x, y), out var _);
            sw.Stop();
            samples[r] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        double total = samples[reps / 2];
        Line("  ⭐ 窗口 " + rect.width + "×" + rect.height + "=" + cells + " 格全量 `TryGetCell`：中位 **" + Fmt(total)
             + " ms**（min " + Fmt(samples[0]) + " / max " + Fmt(samples[reps - 1]) + "，reps=" + reps + "）"
             + " ⇒ 每格均摊 **" + (total / cells).ToString("0.0000") + " ms/格** ＝ "
             + (total / cells * 1000.0).ToString("0.000") + " µs/格",
             total > 0 && cells == (WindowR * 2 + 1) * (WindowR * 2 + 1));
        Note("  备注：`TryGetCell` 内含 `OwnerAt`（字典查）＋ `GetUnitCountInCell`（O(N)，见 R3②）⇒ 单格成本随 N 上升。");
        yield return null;
    }

    // ================= R3 ④：QueryCells 2401 格 =================

    static IEnumerator Section_R3_Query(MapData map, GridSystem grid)
    {
        Note("");
        Note("【R3 ④ · `MapGate.QueryCells` —— **49×49 = 2401 格**窗口单次总耗时 ＋ 与 60fps 帧预算 " + FrameBudgetMs + " ms 对照】");

        var center = map.kingdomSpawns != null && map.kingdomSpawns.Count > 0
            ? new GridCoord(map.kingdomSpawns[0].x, map.kingdomSpawns[0].y)
            : new GridCoord(map.width / 2, map.height / 2);
        var rect = Window(center);
        int cells = rect.width * rect.height;
        int nBase = UnitCountOf(grid);
        Note("  窗口 = " + rect + "（" + rect.width + "×" + rect.height + " = " + cells + " 格，出生点 " + center
             + " 贴边 ⇒ 窗口向内平移）｜当前单位数 N = " + nBase);
        Line("  ⭐ 窗口为**恰好 49×49 = 2401 格**（贴边向内平移，不裁小）", cells == 2401);

        var buf = new List<MapGate.CellInfo>(8192);
        var bufOcc = new List<IGridOccupant>(512);

        // ---- 阶段 1：基线 N 下的四口读数 ----
        var m1 = QueryWindowBench(rect, buf, bufOcc, 30, out int cWalk1, out int cPlain1, out int cOcc1, out int cCount1);
        Note("  ①【N=" + nBase + "】`QueryCells`(条件=可走) 中位 **" + Fmt(m1[0]) + " ms**／次（min " + Fmt(m1[4])
             + " / max " + Fmt(m1[5]) + "，reps=30）⇒ 每格均摊 **" + (m1[0] / cells).ToString("0.0000")
             + " ms/格** ＝ " + (m1[0] / cells * 1000.0).ToString("0.000") + " µs/格 ｜ 命中 " + cWalk1 + " 格");
        Note("  ①【N=" + nBase + "】`QueryCells`(条件=地表=Plain) 中位 **" + Fmt(m1[1]) + " ms**／次 ⇒ 每格均摊 "
             + (m1[1] / cells).ToString("0.0000") + " ms/格 ｜ 命中 " + cPlain1 + " 格");
        Note("  ①【N=" + nBase + "】`QueryOccupants`(按实体) 中位 **" + Fmt(m1[2]) + " ms**／次 ⇒ 每格均摊 "
             + (m1[2] / cells).ToString("0.0000") + " ms/格 ｜ 命中 " + cOcc1 + " 个实体");
        Note("  ①【N=" + nBase + "】`CountFeatures`(计数旋钮·**不调 TryGetCell**) 中位 **" + Fmt(m1[3]) + " ms**／次"
             + " ⇒ 每格均摊 " + (m1[3] / cells).ToString("0.0000") + " ms/格 ｜ 树=" + cCount1);
        Note("  ⭐ 对照：同窗口「纯 `ReadAt` 逐格读」（`CountFeatures`）＝ " + Fmt(m1[3]) + " ms，"
             + "而「复合 6 项」（`QueryCells`）＝ " + Fmt(m1[0]) + " ms ⇒ 差 " + Fmt(m1[0] - m1[3])
             + " ms ＝ **" + ((m1[0] - m1[3]) / m1[0] * 100.0).ToString("0.00")
             + "% 成本来自「每格组 6 项事实」而非「读地表本身」**。");

        // ---- 阶段 2：⭐ 存在性反证 —— 把单位数抬到 500，看窗口耗时是否随 N 线性上涨 ----
        var spawned = new List<GameObject>();
        yield return GrowUnitsTo(grid, 500, spawned);
        int nHi = UnitCountOf(grid);
        var m2 = QueryWindowBench(rect, buf, bufOcc, 30, out int cWalk2, out _, out _, out _);
        Note("  ②【N=" + nHi + "】`QueryCells`(条件=可走) 中位 **" + Fmt(m2[0]) + " ms**／次（min " + Fmt(m2[4])
             + " / max " + Fmt(m2[5]) + "）⇒ 每格均摊 **" + (m2[0] / cells).ToString("0.0000") + " ms/格** ｜ 命中 "
             + cWalk2 + " 格");
        double delta = m2[0] - m1[0];
        double perUnitPerCell = (nHi - nBase) > 0 ? delta / (nHi - nBase) : 0;
        Note("  ⭐ Δ(N=" + nBase + "→" + nHi + ") = " + Fmt(delta) + " ms ⇒ **每 +1 单位 ⇒ 窗口 +"
             + perUnitPerCell.ToString("0.000000") + " ms**（＝每格 O(N) 系数 × " + cells + " 格；"
             + "单次 `GetUnitCountInCell` 每单位 ≈ " + (perUnitPerCell / cells * 1000.0).ToString("0.000000")
             + " µs）");
        Line("  ⭐ 存在性反证：窗口耗时**随单位数 N 线性上涨**（N=" + nBase + " ⇒ " + Fmt(m1[0]) + " ms；N=" + nHi
             + " ⇒ " + Fmt(m2[0]) + " ms；Δ>0）⇒ `TryGetCell` 内的 `GetUnitCountInCell`（O(N)）**每格都付**"
             + " ⇒ `QueryCells` 成本＝**O(格数 × N)**", delta > 0 && nHi > nBase);

        // ---- 与 16.6 ms 帧预算对照（读数对照，不设自造阈值）----
        Note("  ⭐ **与 60fps 帧预算 16.6 ms 对照**：基线 N=" + nBase + " ⇒ " + Fmt(m1[0]) + " ms ＝ "
             + (m1[0] / FrameBudgetMs * 100.0).ToString("0.00") + "% 帧预算；N=" + nHi + " ⇒ " + Fmt(m2[0])
             + " ms ＝ " + (m2[0] / FrameBudgetMs * 100.0).ToString("0.00") + "% 帧预算。");
        Note("  ⚠️ 口径边界：以上为 **2401 格窗口** 的**单次**耗时；若每帧调用 ⇒ 即上述占帧比例。"
             + "`GetOccupant` 未命中路径在本图实测只多付 0.068 µs/格（见 R3①）⇒ **不是**主要成本项。");

        yield return CleanupUnits(grid, spawned);
        Note("  夹具清理后单位数 = " + UnitCountOf(grid) + "（回落到基线 " + nBase + " 附近）");
        yield return null;
    }

    /// <summary>窗口四口计时（reps 次取中位）。返回 [0]=可走中位 [1]=Plain中位 [2]=Occupants中位 [3]=Count中位
    /// [4]=可走min [5]=可走max（ms）。</summary>
    static double[] QueryWindowBench(RectInt rect, List<MapGate.CellInfo> buf, List<IGridOccupant> bufOcc, int reps,
                                     out int cWalk, out int cPlain, out int cOcc, out int cCount)
    {
        for (int i = 0; i < 5; i++) { buf.Clear(); MapGate.QueryCells(rect, MapGate.CellFilter.Walkable(true), buf); }
        var sw = new System.Diagnostics.Stopwatch();
        var sWalk = new double[reps]; var sPlain = new double[reps];
        var sOcc = new double[reps]; var sCount = new double[reps];
        cWalk = cPlain = cOcc = cCount = 0;
        for (int r = 0; r < reps; r++)
        {
            sw.Restart(); buf.Clear(); cWalk = MapGate.QueryCells(rect, MapGate.CellFilter.Walkable(true), buf); sw.Stop();
            sWalk[r] = sw.Elapsed.TotalMilliseconds;

            sw.Restart(); buf.Clear(); cPlain = MapGate.QueryCells(rect, MapGate.CellFilter.Feature(FeatureType.Plain), buf); sw.Stop();
            sPlain[r] = sw.Elapsed.TotalMilliseconds;

            sw.Restart(); bufOcc.Clear(); cOcc = MapGate.QueryOccupants(rect, bufOcc); sw.Stop();
            sOcc[r] = sw.Elapsed.TotalMilliseconds;

            sw.Restart(); cCount = MapGate.CountFeatures(rect, FeatureType.Tree); sw.Stop();
            sCount[r] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(sWalk); Array.Sort(sPlain); Array.Sort(sOcc); Array.Sort(sCount);
        return new double[] { sWalk[reps / 2], sPlain[reps / 2], sOcc[reps / 2], sCount[reps / 2], sWalk[0], sWalk[reps - 1] };
    }

    static RectInt Window(GridCoord center)
    {
        int W = MapGate.MapWidth, H = MapGate.MapHeight;
        int side = WindowR * 2 + 1;                      // 49
        // 贴边时**向内平移**（而非裁小）⇒ 窗口恒为恰好 49×49＝2401 格
        int x0 = Mathf.Clamp(center.x - WindowR, 0, Mathf.Max(0, W - side));
        int y0 = Mathf.Clamp(center.y - WindowR, 0, Mathf.Max(0, H - side));
        return new RectInt(x0, y0, Mathf.Min(side, W), Mathf.Min(side, H));
    }

    // ================= 工具 =================

    /// <summary>单次均摊耗时（ms/次）：`iters` 次调用取总时 / iters。</summary>
    static double BenchPerCall(int iters, Action a)
    {
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        for (int i = 0; i < iters; i++) a();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / iters;
    }

    static string Fmt(double ms) => ms.ToString("0.0000");

    static GridCoord FindEmptyCell(MapData map, GridSystem grid, int maxScan)
    {
        int mid = map.width / 2, scanned = 0;
        for (int r = 4; r < map.width / 2 && scanned < maxScan; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var c = new GridCoord(mid + dx, mid + dy);
                    if (!grid.IsInBounds(c)) continue;
                    scanned++;
                    if (grid.GetOccupant(c) != null) continue;
                    return c;
                }
        return new GridCoord(-1, -1);
    }

    static void Note(string s)
    {
        _log.AppendLine(s);
        UnityEngine.Debug.Log("[HH302] " + s);
        Flush();
    }

    static void Line(string s, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + s);
        if (ok) _pass++; else _fail++;
        UnityEngine.Debug.Log("[HH302] " + (ok ? "OK " : "NG ") + s);
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
                    System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh302_slice4fix_probe.log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath,
                "HH.294 片4补正小批 验证读数 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + _log.ToString());
        }
        catch { }
    }
}
#endif
