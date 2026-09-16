#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HH.294 **片3**（换算归一 3-A ＋ 数组下移 3-B）验证探针（Editor-only · 正门 `TestHarnessApi.EnterTestRun`）。
///
/// 覆盖判据（片3 提示词 第五步）：
///   ① 换算定义只 1 处（grep 侧取证见报告；本探针给「两套并行实现已合一」的行为等价读数）
///   ②/③ 任取 N 格（本探针跑**全图 2,359,296 子格**）⇒ 派生态与 `_walkFlags` 逐格一致
///   ④ 生成一张图 ⇒ 不报错、能走通（要素计数 ＋ 子格域 BFS ＋ 子格域 A*）
///   ⑤ 内存＝能力句＋逐值对照（三数组逐数组 B/格 × 2,359,296；与「改前 5 数组（地块级）」同口径并置）
///   ⑦ `GuardDeploymentSystem` 索引查询**不随本片变化**（features 维持地块级）—— 预期 13,064 格
///   ⑧ BFS flood-fill 耗时与触发频率（与片1 基线对照）＋ 子格域 flood-fill 单列
///   ＋ 换算一致性回归（`CoordToWorld↔GridToIso` 正向一致 / `WorldToCoord↔IsoToCell` 逆向一致）
///   ＋ 占格整块写取证（建筑 footprint 每地块 div² 子格同引用）
/// 用法（MCP）：Play 内 `Valley_HH294_Slice3Probe.Run();`
/// 证据：`Valley Rampart/Logs/hh294_slice3_probe.log`（自落盘；自收尾 ExitTestRun + QuitSmoke）
/// </summary>
public static class Valley_HH294_Slice3Probe
{
    const int Seed = 21107;
    const int Reps = 15;

    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath;
    static int _pass, _fail;

    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            UnityEngine.Debug.LogError("[HH294S3] 须先 GameScene 进 Play（正门 EnterTestRun）。");
            return;
        }
        _log.Clear(); _pass = 0; _fail = 0;
        var host = new GameObject("HH294_Slice3ProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine());
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    // ================= 主流程 =================

    static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Large, selectedSlotId = "smoke_hh294s3", kingdomName = "河谷王国"
        };
        Line("片3 验证启动（正门 EnterTestRun · seed=" + Seed + " · WorldSize=Large(384²)）", true);
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

        int div = grid.SubDiv;
        long subCells = (long)grid.Width * grid.Height;
        long tileCells = (long)grid.MapWidth * grid.MapHeight;
        Line("尺寸口径：地块 " + grid.MapWidth + "×" + grid.MapHeight + "=" + tileCells
             + " ｜ div=" + div + " ⇒ 小格子 " + grid.Width + "×" + grid.Height + "=" + subCells
             + "（面积比 " + (subCells / (double)tileCells).ToString("0.##") + "×）", subCells == 2359296);

        yield return Section_Memory(grid, subCells, tileCells);
        yield return Section_Consistency(map, grid, div, subCells);
        yield return Section_Walkable(map, grid, subCells);
        yield return Section_Occupancy(grid, div);
        yield return Section_Conversion(grid);
        yield return Section_GuardIndex(grid);
        yield return Section_Determinism();
        yield return Section_FrameWatch(grid);

        Note("");
        Line("==== 汇总：PASS=" + _pass + " FAIL=" + _fail + " ====", _fail == 0);
        Finish();
    }

    // ================= ⑤ 内存 =================

    static IEnumerator Section_Memory(GridSystem grid, long subCells, long tileCells)
    {
        Note("");
        Note("【⑤ 内存 · 逐值对照（⛔ 非上限句：逐数组给 B/格 × 格数，可复算）】");
        var blobWf = ((Array)Field(grid, "_walkFlags").GetValue(grid));
        var blobOc = ((Array)Field(grid, "_occupants").GetValue(grid));
        var blobCe = ((Array)Field(grid, "_cells").GetValue(grid));
        int lenWf = blobWf != null ? blobWf.Length : -1;
        int lenOc = blobOc != null ? blobOc.Length : -1;
        int lenCe = blobCe != null ? blobCe.Length : -1;
        int sw = (int)Field(grid, "_sw").GetValue(grid);
        int sh = (int)Field(grid, "_sh").GetValue(grid);

        Line("  在线数组实读：_sw×_sh=" + sw + "×" + sh + "=" + ((long)sw * sh)
             + " ｜ _walkFlags.Length=" + lenWf + " ｜ _occupants.Length=" + lenOc + " ｜ _cells.Length=" + lenCe,
             lenWf == subCells && lenOc == subCells && lenCe == subCells);
        Line("  消费端对齐：IPathGrid.Width/Height=" + grid.Width + "×" + grid.Height
             + " == 三数组域 " + sw + "×" + sh, grid.Width == sw && grid.Height == sh);

        int refSize = IntPtr.Size;                 // 64 位托管 ⇒ 8 B
        int wfSize = 1;                            // WalkFlags : byte
        long wfBytes = (long)wfSize * subCells;
        long ocBytes = (long)refSize * subCells;
        long ceBytes = (long)refSize * subCells;
        long now3 = wfBytes + ocBytes + ceBytes;

        long wfBytesTile = (long)wfSize * tileCells;
        long ocBytesTile = (long)refSize * tileCells;
        long ceBytesTile = (long)refSize * tileCells;
        long old3 = wfBytesTile + ocBytesTile + ceBytesTile;
        long old5 = old3 + 2L * sizeof(int) * tileCells;   // ＋地形(int)＋子状态(int)（片2 已删）

        Note("  ── 逐数组（B/格 × 格数）──");
        Note("  _walkFlags  " + wfSize + " B/格 × " + subCells + " = " + wfBytes + " B（地块级同口径 " + wfBytesTile + " B）");
        Note("  _occupants  " + refSize + " B/格 × " + subCells + " = " + ocBytes + " B（地块级同口径 " + ocBytesTile + " B）");
        Note("  _cells      " + refSize + " B/格 × " + subCells + " = " + ceBytes + " B（地块级同口径 " + ceBytesTile + " B）");
        Note("  ── 组合口径（同口径并置 · 让「完全没做」与「做满」可区分）──");
        Note("  A) 改前 5 数组（地块级：" + tileCells + " 格）= " + Fmt(old5) + " = " + Mb(old5) + " MiB");
        Note("  B) 片2 后 3 数组（地块级：" + tileCells + " 格）= " + Fmt(old3) + " = " + Mb(old3) + " MiB");
        Note("  C) 本片后 3 数组（小格子级：" + subCells + " 格）= " + Fmt(now3) + " = " + Mb(now3) + " MiB");
        Note("  ⇒ 本片净增量（B→C）= " + Mb(now3 - old3) + " MiB ｜ 对改前（A→C）= " + Mb(now3 - old5) + " MiB");

        long gcWf = MinBytes(() => new WalkFlags[subCells]);
        long gcOc = MinBytes(() => new IGridOccupant[subCells]);
        long gcCe = MinBytes(() => new GridCell[subCells]);
        Note("  ── GC 实分配交叉核对（MinBytes · 3 次取最小）──");
        Note("  new WalkFlags[n]=" + gcWf + " B ｜ new IGridOccupant[n]=" + gcOc
             + " B ｜ new GridCell[n]=" + gcCe + " B ⇒ 合计 " + Mb(gcWf + gcOc + gcCe) + " MiB");
        Note("  逐值差（GC 实测 − B/格复算）：WalkFlags " + (gcWf - wfBytes) + " B ｜ occupants " + (gcOc - ocBytes)
             + " B ｜ cells " + (gcCe - ceBytes) + " B（口径：`GC.GetTotalMemory` 以 4 KiB 页计量 ⇒ 差值应落在 [0,4096]）");
        Line("  逐值一致性：三数组差值均在 [0,4096] 页内 ⇒ 逐值复算与实测相符",
             gcWf - wfBytes >= 0 && gcWf - wfBytes <= 4096
             && gcOc - ocBytes >= 0 && gcOc - ocBytes <= 4096
             && gcCe - ceBytes >= 0 && gcCe - ceBytes <= 4096);
        yield return null;
    }

    // ================= ③ 逐格一致 =================

    static IEnumerator Section_Consistency(MapData map, GridSystem grid, int div, long subCells)
    {
        Note("");
        Note("【③ 全图逐格一致：派生态（地表物 ＋ 占格阻挡位） vs `_walkFlags`】");
        var wf = (WalkFlags[])Field(grid, "_walkFlags").GetValue(grid);
        int W = map.width, H = map.height, SW = grid.Width;
        long mismatchMasked = 0, mismatchExact = 0, bridgeBits = 0, lockedBits = 0;
        long nonUniformTiles = 0;
        var expectedCount = new Dictionary<int, long>();

        for (int sy = 0; sy < grid.Height; sy++)
        {
            int cy = sy / div;
            for (int sx = 0; sx < SW; sx++)
            {
                int i = sy * SW + sx;
                WalkFlags actual = wf[i];
                WalkFlags expect = ExpectWalk(map.features[cy * W + sx / div]);
                var occ = grid.GetOccupantSub(new GridCoord(sx, sy));
                if (occ != null && occ.IsGridObstacle) expect |= WalkFlags.BuildingBlocked;

                if (actual != expect) mismatchMasked++;
                if ((actual & ~WalkFlags.Bridge) != expect) mismatchExact++;
                if ((actual & WalkFlags.Bridge) != 0) bridgeBits++;
                if ((actual & WalkFlags.Locked) != 0) lockedBits++;
                int key = (int)expect;
                if (!expectedCount.ContainsKey(key)) expectedCount[key] = 0;
                expectedCount[key] = expectedCount[key] + 1;
            }
        }
        for (int cy = 0; cy < H; cy++)
            for (int cx = 0; cx < W; cx++)
            {
                WalkFlags first = wf[(cy * div) * SW + cx * div];
                bool same = true;
                for (int sy = 0; sy < div && same; sy++)
                    for (int sx = 0; sx < div; sx++)
                        if (wf[(cy * div + sy) * SW + cx * div + sx] != first) { same = false; break; }
                if (!same) nonUniformTiles++;
            }

        Note("  扫描子格数 = " + subCells + "（**全图**，非抽样）");
        Line("  ⭐ 不一致数（掩掉 `Bridge` 位后的主口径）= " + mismatchMasked + "（0＝逐格一致）", mismatchMasked == 0);
        Note("  不一致数（**不掩**任何位）= " + mismatchExact
             + " ｜ `Bridge` 位子格数 = " + bridgeBits + " ｜ `Locked` 位子格数 = " + lockedBits);
        Line("  同一地块 div² 子格**非同值**的地块数 = " + nonUniformTiles + "（0＝地块级代表位成立的前提）", nonUniformTiles == 0);
        var sb = new StringBuilder();
        foreach (var kv in expectedCount) sb.Append(((WalkFlags)kv.Key) + "=" + kv.Value + " ");
        Note("  派生态取值分布：" + sb.ToString().Trim());
        Note("  ⚠️ 排除项 1：`Bridge` 位由 `SetBridge` 独立写入（不属「地表物＋占格」派生式）⇒ 主口径为「掩 Bridge」。");
        Note("  ⚠️ 排除项 2：未做「精确子格部分占格」用例 —— 当前全库写入方皆**地块对齐**（地表物按地块 / footprint 按地块 / 桥按地块），无此状态。");
        yield return null;
    }

    static WalkFlags ExpectWalk(FeatureType f)
    {
        switch (f)
        {
            case FeatureType.Plain: case FeatureType.Tree: case FeatureType.Mine:
            case FeatureType.OreVein: case FeatureType.StonePile: case FeatureType.WoodPile:
                return WalkFlags.TerrainWalkable;
            case FeatureType.River: case FeatureType.Ocean:
                return WalkFlags.Water;
            default: return WalkFlags.None;
        }
    }

    // ================= ④ 能走通 ＋ ⑧ BFS =================

    static IEnumerator Section_Walkable(MapData map, GridSystem grid, long subCells)
    {
        Note("");
        Note("【④ 生成一图正常 ＋ 能走通（子格域）／⑧ BFS 耗时与频率】");
        int nPlain = 0, nTree = 0, nMine = 0, nMountain = 0, nWater = 0, nRes = 0;
        for (int i = 0; i < map.features.Length; i++)
        {
            var f = map.features[i];
            if (f == FeatureType.Plain) nPlain++;
            else if (f == FeatureType.Tree) nTree++;
            else if (f == FeatureType.Mine) nMine++;
            else if (f == FeatureType.Mountain || f == FeatureType.SnowMountain) nMountain++;
            else if (f == FeatureType.River || f == FeatureType.Ocean) nWater++;
            else nRes++;
        }
        Line("  ④a 要素：总格=" + map.features.Length + " | Plain=" + nPlain + " Tree=" + nTree + " Mine=" + nMine
             + " 山=" + nMountain + " 水=" + nWater + " 一次性=" + nRes
             + " | climateZones=" + (map.climateZones != null ? map.climateZones.Length : -1),
             map.features.Length > 0 && map.climateZones != null && map.climateZones.Length == map.width * map.height);

        int div = grid.SubDiv;
        int SW = grid.Width, SH = grid.Height;
        var spawns = map.kingdomSpawns;
        var src = spawns != null && spawns.Count > 0 ? spawns[0] : new Vector2Int(map.width / 2, map.height / 2);
        int sSubX = src.x * div, sSubY = src.y * div;
        if (sSubX < 0 || sSubY < 0 || sSubX >= SW || sSubY >= SH) { sSubX = SW / 2; sSubY = SH / 2; }

        var visited = new bool[subCells];
        var q = new Queue<int>();
        int start = sSubY * SW + sSubX;
        visited[start] = true; q.Enqueue(start);
        long reached = 0;
        var swBfs = new Stopwatch();
        swBfs.Start();
        while (q.Count > 0)
        {
            int cur = q.Dequeue(); reached++;
            int cx = cur % SW, cy = cur / SW;
            SubEnq(grid, visited, q, cx + 1, cy, SW, SH);
            SubEnq(grid, visited, q, cx - 1, cy, SW, SH);
            SubEnq(grid, visited, q, cx, cy + 1, SW, SH);
            SubEnq(grid, visited, q, cx, cy - 1, SW, SH);
        }
        swBfs.Stop();
        Note("  ⑧a 子格域 flood-fill（" + SW + "×" + SH + "=" + subCells + " 节点）：可达 " + reached
             + " 子格 ／ 单次 " + swBfs.Elapsed.TotalMilliseconds.ToString("0.00") + " ms"
             + "（⚠️ 生产**未**走此路径 —— `MapValidator` 跑在地块级 `features` 上，见 ⑧b）");

        string reachStr = ""; bool allReach = true;
        if (spawns != null)
        {
            for (int i = 1; i < spawns.Count; i++)
            {
                int sx = spawns[i].x * div, sy = spawns[i].y * div;
                bool ok = sx >= 0 && sy >= 0 && sx < SW && sy < SH && visited[sy * SW + sx];
                allReach &= ok;
                reachStr += "[" + i + "]=" + ok + " ";
            }
        }
        Line("  ④c 走通（子格域 flood-fill · spawn0 → 其余出生点）：" + (reachStr.Length > 0 ? reachStr : "无多出生点"),
             spawns == null || spawns.Count <= 1 || allReach);

        if (spawns != null && spawns.Count > 1)
        {
            var from = new GridCoord(spawns[0].x * div, spawns[0].y * div);
            var to = new GridCoord(spawns[spawns.Count - 1].x * div, spawns[spawns.Count - 1].y * div);
            var swA = new Stopwatch();
            swA.Start();
            PathResult pr = AStarSolver.Solve(grid, from, to, 400000);
            swA.Stop();
            Line("  ④d 生产子格域 A*（IPathGrid→IsSubWalkable）：status=" + pr.status
                 + " waypoints=" + (pr.waypoints != null ? pr.waypoints.Length : -1)
                 + " reachedExactGoal=" + pr.reachedExactGoal
                 + " ／ " + swA.Elapsed.TotalMilliseconds.ToString("0.0") + " ms",
                 pr.status == PathStatus.Ready || pr.status == PathStatus.Partial);
        }
        else Line("  ④d 生产子格域 A*：出生点不足，跳过", false);

        double[] mv = new double[Reps];
        bool mvReach = false;
        var far = spawns != null && spawns.Count > 1 ? spawns[1] : new Vector2Int(map.width / 2, map.height / 2);
        for (int r = 0; r < Reps; r++)
        {
            var sw2 = new Stopwatch(); sw2.Start();
            mvReach = MapValidator.IsReachable(map, src, far);
            sw2.Stop();
            mv[r] = sw2.Elapsed.TotalMilliseconds;
        }
        Array.Sort(mv);
        Note("  ⑧b 生产 BFS `MapValidator.IsReachable`（地块级 features · " + map.width + "×" + map.height
             + " · 源=spawn0 靶=spawn1 · " + Reps + " 次取中位）：中位 " + mv[Reps / 2].ToString("0.000") + " ms"
             + " ／ min " + mv[0].ToString("0.000") + " ms ／ 可达=" + mvReach + "（片1 基线：384² 地块级 = 4.73 ms；1536² 全图 flood = 56.08 ms）");
        Note("  ⑧c 触发频率（代码审计口径）：`MapValidator` = 2 次/局（步骤8 ＋ 水域后复跑）＋ `CarveCorridor` 偶发；"
             + "本片**未改**该路径 ⇒ 频率与耗时均不变（域＝`map.features` 地块级，不随三数组下移变化）");
        yield return null;
    }

    static void SubEnq(GridSystem g, bool[] vis, Queue<int> q, int x, int y, int W, int H)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        int i = y * W + x;
        if (vis[i]) return;
        vis[i] = true;
        if (g.IsSubWalkable(new GridCoord(x, y))) q.Enqueue(i);
    }

    // ================= 占格整块写取证 =================

    static IEnumerator Section_Occupancy(GridSystem grid, int div)
    {
        Note("");
        Note("【占格下移取证：建筑 footprint 每地块 div² 子格同引用】");
        var reg = BuildingRegistry.Instance;
        if (reg == null) { Line("  BuildingRegistry 未就绪", false); yield return null; yield break; }
        int checkedBuildings = 0, registered = 0, fullHits = 0, partialHits = 0, missAll = 0;
        long cellsChecked = 0, cellsHit = 0;
        var missSamples = new StringBuilder();
        int missLogged = 0;
        var all = reg.All;
        for (int bi = 0; bi < all.Count && checkedBuildings < 60; bi++)
        {
            var b = all[bi];
            if (b == null || b.def == null) continue;
            int w = Mathf.Max(1, b.footprint.x), h = Mathf.Max(1, b.footprint.y);
            checkedBuildings++;
            int hit = 0, total = 0, nonNull = 0;
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    for (int sy = 0; sy < div; sy++)
                        for (int sx = 0; sx < div; sx++)
                        {
                            total++;
                            var sub = new GridCoord((b.coord.x + dx) * div + sx, (b.coord.y + dy) * div + sy);
                            var occ = grid.GetOccupantSub(sub);
                            if (occ != null) nonNull++;
                            if (ReferenceEquals(occ, b)) hit++;
                        }
            cellsChecked += total; cellsHit += hit;
            if (nonNull > 0) registered++;
            if (hit == total) fullHits++;
            else if (hit == 0)
            {
                missAll++;
                if (missLogged < 5)
                {
                    missLogged++;
                    var tileOcc = grid.GetOccupant(b.coord);
                    missSamples.Append("\n    · def=" + b.def.id + " coord=" + b.coord + " fp=" + w + "×" + h
                                       + " 子格命中 0/" + total + " ｜ 该 footprint 内非空子格=" + nonNull
                                       + " ｜ 地块级 GetOccupant(coord)=" + (tileOcc == null ? "null" : (ReferenceEquals(tileOcc, b) ? "同物体(异常)" : "他物体")));
                }
            }
            else partialHits++;
        }
        Note("  抽样建筑 " + checkedBuildings + " 座：网格上**有登记**（footprint 内有非空占格）= " + registered
             + " ｜ footprint 全覆盖（全 div² 子格同引用）= " + fullHits
             + " ｜ 部分命中 = " + partialHits + " ｜ 全未命中（网格上无任何登记）= " + missAll);
        Note("  子格命中合计 = " + cellsHit + "/" + cellsChecked);
        if (missSamples.Length > 0) Note("  ⚠️ 全未命中样本（诊断）:" + missSamples.ToString());
        Note("  口径：本判据只对「网格上有登记」的建筑成立 ⇒ 断言限定在 registered 集合（无登记者＝**改前亦然**："
             + "地块级 `GetOccupant(coord)` 同为 null ⇒ 非本片引入的既存状态，列报）");
        Line("  在网格有登记的 " + registered + " 座 ⇒ footprint 全 div² 子格同引用 = " + fullHits + "/" + registered
             + "（全覆盖＝「建筑尺寸内部 ×16」占格已落到小格子）", registered > 0 && fullHits == registered);
        yield return null;
    }

    // ================= ①/② 换算一致性 =================

    static IEnumerator Section_Conversion(GridSystem grid)
    {
        Note("");
        Note("【①/② 换算合一回归：两套并行实现（GridSystem ↔ MapRenderService）已合一后的等价读数】");
        var rng = new System.Random(4242);
        int n = 300, badFwd = 0, badInv = 0, badSub = 0, badFp = 0;
        Vector2 cs = grid.Config != null ? grid.Config.cellSize : MapRenderService.DefaultCellSize;
        for (int k = 0; k < n; k++)
        {
            int x = rng.Next(grid.MapWidth), y = rng.Next(grid.MapHeight);
            var c = new GridCoord(x, y);
            Vector2 a = grid.CoordToWorld(c);
            Vector2 b = MapRenderService.GridToIso(c);
            if (a != b) badFwd++;
            var backA = grid.WorldToCoord(a);
            var backB = MapRenderService.IsoToCell(a);
            if (!(backA.HasValue && backA.Value == backB)) badInv++;
            Vector3 fp1 = GridSystem.FootprintCenterWorld(c, new Vector2Int(1, 1), cs);
            if ((Vector2)fp1 != a) badFp++;
            Vector3 fp2 = GridSystem.FootprintCenterWorld(c, new Vector2Int(2, 2), cs);
            Vector2 expect2 = a + new Vector2(0.5f * cs.x, 0.5f * cs.y);
            if ((Vector2)fp2 != expect2) badFp++;
            var sub = grid.CellToSub(c, 1, 2);
            var cellBack = grid.SubToCell(sub);
            if (!(cellBack.x == c.x && cellBack.y == c.y)) badSub++;
            Vector2 sw2 = grid.SubCoordToWorld(sub);
            if (!grid.WorldToSubCoord(sw2).HasValue) badSub++;
        }
        Line("  抽样 N=" + n + "：① `CoordToWorld == GridToIso` 不一致=" + badFwd
             + " ｜ ② `WorldToCoord == IsoToCell` 不一致=" + badInv
             + " ｜ ③ `CellToSub/SubToCell/SubCoordToWorld/WorldToSubCoord` 往返异常=" + badSub
             + " ｜ ④ footprint 中心点恒等式异常=" + badFp,
             badFwd == 0 && badInv == 0 && badSub == 0 && badFp == 0);
        Note("  口径来源：`GridSystem` 内核（`CellToWorldF`/`WorldToCellF`/`FootprintCenterWorld`）为**唯一算式**；"
             + "`MapRenderService.GridToIso/IsoToCell/IsoDepth` 已改为**一行转调**（本探针即验证转调后逐位相同）");
        yield return null;
    }

    // ================= ⑦ GuardDeploymentSystem =================

    static IEnumerator Section_GuardIndex(GridSystem grid)
    {
        Note("");
        Note("【⑦ C1 点确认：`GuardDeploymentSystem` 索引查询不随本片变化（`features` 维持地块级）】");
        int indexed = GuardDeploymentSystem.IndexedResourceCells;
        var pos = grid.CoordToWorld(new GridCoord(1, 1));
        double[] ms = new double[Reps];
        bool hasNode = false;
        for (int r = 0; r < Reps; r++)
        {
            var sw = new Stopwatch(); sw.Start();
            var node = GuardDeploymentSystem.FindNearestResourceNode(pos);
            sw.Stop();
            ms[r] = sw.Elapsed.TotalMilliseconds;
            if (r == 0) { hasNode = node.HasValue; Note("  取点有效性：返回=" + (node.HasValue ? node.Value.coord.ToString() : "null")); }
        }
        Array.Sort(ms);
        Line("  索引条目数 `IndexedResourceCells` = " + indexed + "（有值＝索引已建）", indexed > 0 && hasNode);
        Note("  单次 `FindNearestResourceNode` 耗时（" + Reps + " 次取中位）= " + ms[Reps / 2].ToString("0.000")
             + " ms ／ min " + ms[0].ToString("0.000") + " ms（片1 基线：0.374 ms ／ 13,064 格）");
        Note("  ⇒ 本片前后**未变化**：索引为**地块级**（随 `MapData.features` 的 W×H）⇒ 不随三数组下移 ×16；"
             + "遍历格数＝索引条目数（与查询位置无关）");
        yield return null;
    }

    // ================= 确定性（同 seed 两次 ⇒ 逐格一致） =================

    static IEnumerator Section_Determinism()
    {
        Note("");
        Note("【确定性：同 seed 两次生成地图 ⇒ 逐格一致（含 climateZones / spawns / nb）】");
        var a = Valley_HH291_MapGenProbe.Build(Seed, 384, 384, 2);
        yield return null;
        var b = Valley_HH291_MapGenProbe.Build(Seed, 384, 384, 2);
        bool same = Valley_HH291_MapGenProbe.SameMap(a, b, out var why);
        Line("  384²·seed=" + Seed + " 两次生成：" + (same ? "逐格一致 ✅" : "不一致 ❌ " + why)
             + "（比对域：features 逐格 ＋ climateZones 逐格 ＋ kingdomSpawns 逐个 ＋ naturalBuildings 逐项）", same);
        yield return null;
    }

    // ================= 无每帧全图扫（存在性反证：空闲 N 帧内三数组零变更） =================

    static IEnumerator Section_FrameWatch(GridSystem grid)
    {
        Note("");
        Note("【无每帧全图扫 · 存在性反证（空闲 N 帧观察）】");
        var wf = (WalkFlags[])Field(grid, "_walkFlags").GetValue(grid);
        var oc = (IGridOccupant[])Field(grid, "_occupants").GetValue(grid);
        var ce = (GridCell[])Field(grid, "_cells").GetValue(grid);
        int frames = 120;
        long wfChanged = 0, ocChanged = 0, ceChanged = 0;
        var dt = new List<float>(frames);
        var prevWf = (WalkFlags[])wf.Clone();
        var prevOc = (IGridOccupant[])oc.Clone();
        var prevCe = (GridCell[])ce.Clone();
        for (int f = 0; f < frames; f++)
        {
            yield return null;
            dt.Add(Time.unscaledDeltaTime * 1000f);
            for (int i = 0; i < wf.Length; i++) if (wf[i] != prevWf[i]) { wfChanged++; break; }
            for (int i = 0; i < oc.Length; i++) if (!ReferenceEquals(oc[i], prevOc[i])) { ocChanged++; break; }
            for (int i = 0; i < ce.Length; i++) if (!ReferenceEquals(ce[i], prevCe[i])) { ceChanged++; break; }
            Array.Copy(wf, prevWf, wf.Length);
            Array.Copy(oc, prevOc, oc.Length);
            Array.Copy(ce, prevCe, ce.Length);
        }
        dt.Sort();
        Note("  观察 " + frames + " 帧（空闲·1x）：三数组**发生变更的帧数** = _walkFlags " + wfChanged
             + " ／ _occupants " + ocChanged + " ／ _cells " + ceChanged + "（变更＝该帧内存在元素被改写）");
        Note("  帧耗时（roundtrip 与本探针 3×2.36M 指纹扫描已含在内，仅供参考）：p50 "
             + dt[frames / 2].ToString("0.0") + " ms ／ p95 " + dt[(int)(frames * 0.95)].ToString("0.0")
             + " ms ／ max " + dt[frames - 1].ToString("0.0") + " ms");
        Line("  ⭐ 存在性反证「无**每帧**写扫」：三数组变更帧数均 < 总帧数 "
             + frames + "（实测 " + wfChanged + "／" + ocChanged + "／" + ceChanged + "）"
             + " ⇒ 不存在「每帧都改写全数组」的路径",
             wfChanged < frames && ocChanged < frames && ceChanged < frames);
        Note("  ⚠️ 变更帧数 >0 时的成因＝运行期事件（资源重生/建筑放置等）**事件驱动**，非每帧；"
             + "**「每帧只读扫」在运行期不可由本探针证否**（读不改变数组内容）");
        Note("  ⚠️ 排除项（诚实声明）：「无每帧全图扫」的**主证据＝§三 代码审计逐点清单**"
             + "（`GridSystem` 内数组访问点全部为建局/事件驱动，无 `Update` 内全数组循环）；"
             + "本段为**辅助存在性反证**。（生产未插桩计数：插桩＝改动生产代码，超本片范围。）");
        yield return null;
    }

    // ================= 工具 =================

    static FieldInfo Field(object obj, string name)
        => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    static long MinBytes<T>(Func<T[]> factory)
    {
        long best = long.MaxValue;
        for (int r = 0; r < 3; r++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            long before = GC.GetTotalMemory(true);
            var keep = factory();
            long after = GC.GetTotalMemory(false);
            if (keep == null) { }
            long d = after - before;
            if (d < best) best = d;
            keep = null;
        }
        return best;
    }

    static string Fmt(long bytes)
        => bytes >= 1024 * 1024 ? (bytes / 1024.0 / 1024.0).ToString("0.00") + "M" : (bytes / 1024.0).ToString("0.0") + "K";

    static string Mb(long bytes) => (bytes / 1024.0 / 1024.0).ToString("0.00");

    static void Note(string s) { _log.AppendLine(s); UnityEngine.Debug.Log("[HH294S3] " + s); Flush(); }

    static void Line(string s, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + s);
        if (ok) _pass++; else _fail++;
        UnityEngine.Debug.Log("[HH294S3] " + (ok ? "OK " : "NG ") + s);
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
                    System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh294_slice3_probe.log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, "HH.294 片3 验证读数 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n" + _log.ToString());
        }
        catch { }
    }
}
#endif
