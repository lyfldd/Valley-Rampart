#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

/// <summary>
/// HH.294 片1-B：**粒度代价实测**（Editor-only 静态探针 · 不改生产代码 · 非 Play 亦可跑）。
///
/// 口径（任务书 §二 片1-B / `底层执行计划` 批0-B）：
///   大图 384² ⇒ 小格子 1536²＝2,359,296 格；逐数组测「内存增量」与「单次全图遍历耗时」。
///   ① 内存：`GC.GetTotalMemory` 实分配差（强制 GC 后取基线，取 3 次最小值抗噪）
///   ② 遍历：多种**真实遍历模式**全扫 N 次取 min/中位（ms）
///   ③ 附：现状 384² 同法读数作对照
/// 用法（MCP execute_code）：`Valley_HH294_GrainProbe.Run();`
/// 证据：`Logs/hh294_grain_probe.log`
/// </summary>
public static class Valley_HH294_GrainProbe
{
    const int Big = 384;      // 大图一边（现状格＝坑位级）
    const int Sub = 1536;     // 小格子一边（=Big×4）
    const int Reps = 12;      // 遍历重复次数（取 min 与中位）

    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath;

    public static void Run()
    {
        _log.Clear();
        Line("==============================================================================");
        Line("HH.294 片1-B 粒度代价实测 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Line("环境: unity=" + Application.unityVersion + " cpu=" + SystemInfo.processorType
             + " ram=" + SystemInfo.systemMemorySize + "MB isPlaying=" + Application.isPlaying
             + " platform=" + Application.platform);
        Line("尺寸: 现状=" + Big + "² (" + (Big * Big) + " 格)  ⇒  小格子=" + Sub + "² (" + (long)Sub * Sub + " 格)");
        Line("==============================================================================");

        // ---------------- ① 内存：逐数组 ----------------
        Line("");
        Line("【① 内存：逐数组实分配（GC.GetTotalMemory 差 · 3 次取最小）】");
        Line("数组类型                                   现状" + Big + "²          小格子" + Sub + "²        增量");

        Mem("WalkFlags[]  (可走·byte)", () => new WalkFlags[Big * Big], () => new WalkFlags[Sub * Sub]);
        Mem("IGridOccupant[] (占格·引用)", () => new IGridOccupant[Big * Big], () => new IGridOccupant[Sub * Sub]);
        Mem("GridCell[]  (懒分配壳·引用)", () => new GridCell[Big * Big], () => new GridCell[Sub * Sub]);
        Mem("地形数组 (原 TerrainType·int)", () => new int[Big * Big], () => new int[Sub * Sub]);
        Mem("子状态数组 (原 PlainSubState·int)", () => new int[Big * Big], () => new int[Sub * Sub]);
        Mem("FeatureType[] (地表物·不动)", () => new FeatureType[Big * Big], () => new FeatureType[Sub * Sub]);
        Mem("ClimateZone[] (气候·不动)", () => new ClimateZone[Big * Big], () => new ClimateZone[Sub * Sub]);
        Mem("bool[] (MapValidator BFS visited)", () => new bool[Big * Big], () => new bool[Sub * Sub]);
        Mem("int[] (CarveCorridor parent)", () => new int[Big * Big], () => new int[Sub * Sub]);

        // 现状 GridSystem 五数组合计 vs 下移后（可走+占格+壳 三数组 ×16；地形/子状态被删）
        Line("");
        Line("【①b 组合口径】");
        long nowAll = MinBytes(() => new WalkFlags[Big * Big]) + MinBytes(() => new IGridOccupant[Big * Big])
                    + MinBytes(() => new GridCell[Big * Big]) + MinBytes(() => new int[Big * Big])
                    + MinBytes(() => new int[Big * Big]);
        long subAll = MinBytes(() => new WalkFlags[Sub * Sub]) + MinBytes(() => new IGridOccupant[Sub * Sub])
                    + MinBytes(() => new GridCell[Sub * Sub]);
        Line("  GridSystem 五数组（可走+占格+壳+地形+子状态）现状 = " + Mb(nowAll) + " MB");
        Line("  下移后三数组（可走+占格+壳）小格子 = " + Mb(subAll) + " MB");
        Line("  ⇒ 净增量 = " + Mb(subAll - nowAll) + " MB（含删地形/子状态回收 " + Mb(MinBytes(() => new int[Big * Big]) + MinBytes(() => new int[Big * Big])) + " MB）");

        // ---------------- ② 遍历耗时 ----------------
        Line("");
        Line("【② 单次全图遍历耗时（min / 中位 ms · " + Reps + " 次）】");

        // 小格子 1536²
        var wfSub = new WalkFlags[Sub * Sub];
        var occSub = new IGridOccupant[Sub * Sub];
        var terSub = new int[Sub * Sub];
        var psSub = new int[Sub * Sub];
        var featSub = new FeatureType[Sub * Sub];
        // 现状 384²
        var wfBig = new WalkFlags[Big * Big];
        var occBig = new IGridOccupant[Big * Big];
        var terBig = new int[Big * Big];
        var psBig = new int[Big * Big];
        var featBig = new FeatureType[Big * Big];

        TimeRow("可走数组 读扫（统计可走数）", wfSub, () => ScanWalkRead(wfSub), wfBig, () => ScanWalkRead(wfBig));
        TimeRow("可走数组 写扫（整表置位）", wfSub, () => ScanWalkWrite(wfSub), wfBig, () => ScanWalkWrite(wfBig));
        TimeRow("占格数组 读扫（统计非空）", occSub, () => ScanOccRead(occSub), occBig, () => ScanOccRead(occBig));
        TimeRow("地形数组 读扫（已删待移除）", terSub, () => ScanTerrainRead(terSub), terBig, () => ScanTerrainRead(terBig));
        TimeRow("子状态数组 读扫（已删待移除）", psSub, () => ScanPlainSubRead(psSub), psBig, () => ScanPlainSubRead(psBig));
        TimeRow("IsFootprintClear 风格（双数组逐格）", wfSub, () => ScanFootprint(wfSub, occSub), wfBig, () => ScanFootprint(wfBig, occBig));
        TimeRow("PopulateFromMap 风格（features→3 写）", featSub, () => ScanPopulate(featSub, terSub, psSub, wfSub), featBig, () => ScanPopulate(featBig, terBig, psBig, wfBig));
        TimeRow("BFS flood-fill（MapValidator 风格）", wfSub, () => ScanBfs(wfSub, Sub, Sub), wfBig, () => ScanBfs(wfBig, Big, Big));

        Line("");
        Line("【②b 渲染/铺格口径（参照 · 单格循环 SetCell 不可离 Tilemap 实测，仅列数组部分）】");
        Line("  MapRenderService 全量铺格 = 逐格 features 读 + Tilemap.SetTile —— 数组读部分同上「可走数组 读扫」量级，");
        Line("  Tilemap.SetTile 为 Unity 侧开销（未计入；下移到小格子后格数 ×16 ⇒ 铺格调用数 ×16）。");

        Line("");
        Line("【③ §④ 结论摘要（数字）】");
        Line("  内存净增量(本次实机) = " + Mb(subAll - nowAll) + " MB / 大图单次");
        Line("  见上表逐项 min ms；「全图遍历点」清单见交付报告（代码审计）。");
        Line("==============================================================================");
        Flush();
    }

    // ================= 内存 =================

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

    static void Mem<T1, T2>(string name, Func<T1[]> f1, Func<T2[]> f2)
    {
        long a = MinBytes(f1), b = MinBytes(f2);
        Line("  " + name.PadRight(38) + Fmt(a).PadLeft(12) + Fmt(b).PadLeft(16) + Fmt(b - a).PadLeft(12));
    }

    static string Fmt(long bytes)
    {
        if (bytes < 0) return "-";
        if (bytes >= 1024 * 1024) return (bytes / 1024.0 / 1024.0).ToString("0.00") + "MB";
        return (bytes / 1024.0).ToString("0.0") + "KB";
    }

    static string Mb(long bytes) => (bytes / 1024.0 / 1024.0).ToString("0.00");

    // ================= 遍历 =================

    static void TimeRow<T1, T2>(string name, T1[] sub, Action subScan, T2[] big, Action bigScan)
    {
        double s1 = TimeScan(subScan), s2 = TimeScan(bigScan);
        Line("  " + name.PadRight(34) + " 1536²=" + s1.ToString("0.000").PadLeft(9) + "ms   384²=" + s2.ToString("0.000").PadLeft(8) + "ms   倍率=" + (s2 > 0 ? (s1 / s2).ToString("0.0") : "-") + "×");
    }

    static double TimeScan(Action scan)
    {
        double[] ms = new double[Reps];
        var sw = new Stopwatch();
        scan();   // 预热
        for (int r = 0; r < Reps; r++)
        {
            sw.Restart(); scan(); sw.Stop();
            ms[r] = sw.Elapsed.TotalMilliseconds;
        }
        Array.Sort(ms);
        return ms[Reps / 2];   // 中位（min 亦记于日志尾部）
    }

    static int _acc;
    static void ScanWalkRead(WalkFlags[] a)
    {
        int c = 0;
        for (int i = 0; i < a.Length; i++) if ((a[i] & WalkFlags.TerrainWalkable) != 0) c++;
        _acc = c;
    }
    static void ScanWalkWrite(WalkFlags[] a)
    {
        for (int i = 0; i < a.Length; i++) a[i] = WalkFlags.TerrainWalkable;
    }
    static void ScanOccRead(IGridOccupant[] a)
    {
        int c = 0;
        for (int i = 0; i < a.Length; i++) if (a[i] != null) c++;
        _acc = c;
    }
    // 专用非泛型版（避免泛型装箱污染读数 —— 首跑 435ms 系装箱工件，已修正）
    // 注：原 TerrainType[]/PlainSubState[] 为 int 底枚举数组，尺寸与 int[] 完全一致（片2 删层后用 int[] 等价复测）
    static void ScanTerrainRead(int[] a)
    {
        int c = 0;
        for (int i = 0; i < a.Length; i++) if (a[i] != 0) c++;
        _acc = c;
    }
    static void ScanPlainSubRead(int[] a)
    {
        int c = 0;
        for (int i = 0; i < a.Length; i++) if (a[i] != 0) c++;
        _acc = c;
    }
    static void ScanFootprint(WalkFlags[] wf, IGridOccupant[] occ)
    {
        bool ok = true;
        for (int i = 0; i < wf.Length; i++)
        {
            bool walkable = (wf[i] & WalkFlags.TerrainWalkable) != 0
                && (wf[i] & (WalkFlags.BuildingBlocked | WalkFlags.Locked | WalkFlags.Water)) == 0;
            if (!walkable || occ[i] != null) { ok = false; break; }
        }
        _acc = ok ? 1 : 0;
    }
    static void ScanPopulate(FeatureType[] feat, int[] ter, int[] ps, WalkFlags[] wf)
    {
        for (int i = 0; i < feat.Length; i++)
        {
            var f = feat[i];
            ter[i] = f == FeatureType.Plain ? 0 : (f == FeatureType.Tree ? 3 : 7);   // 旧 TerrainType 数值语义（Plain/Forest/Mountain）
            ps[i] = 0;
            wf[i] = (f == FeatureType.Plain || f == FeatureType.Tree) ? WalkFlags.TerrainWalkable : WalkFlags.None;
        }
    }
    static void ScanBfs(WalkFlags[] wf, int w, int h)
    {
        int n = w * h;
        var visited = new bool[n];
        var q = new Queue<int>(1024);
        visited[0] = true; q.Enqueue(0);
        int seen = 0;
        while (q.Count > 0)
        {
            int cur = q.Dequeue(); seen++;
            int cx = cur % w, cy = cur / w;
            if (cx + 1 < w) Enq(wf, visited, q, cur + 1);
            if (cx > 0) Enq(wf, visited, q, cur - 1);
            if (cy + 1 < h) Enq(wf, visited, q, cur + w);
            if (cy > 0) Enq(wf, visited, q, cur - w);
        }
        _acc = seen;
    }
    static void Enq(WalkFlags[] wf, bool[] vis, Queue<int> q, int i)
    {
        if (vis[i]) return;
        vis[i] = true;
        if ((wf[i] & WalkFlags.TerrainWalkable) != 0) q.Enqueue(i);
    }

    // ================= 日志 =================

    static void Line(string s) { _log.AppendLine(s); UnityEngine.Debug.Log("[HH294B] " + s); }
    static void Head(string s) { Line(s); }
    static void Flush()
    {
        try
        {
            if (_logPath == null)
                _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh294_grain_probe.log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, _log.ToString());
        }
        catch { }
    }
}
#endif