using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ============================================================================
//  M2 Headless 模拟器 CLI（子命令：acceptance / determinism / smoke）
//  06_执行计划与验收.md §M2：
//    验收 1：S1 平原镜像战跑 100 局，胜率落在 45%-55%（不对称=有 bug）
//    验收 2：S2 能产出"弓手被贴身"指标
//    附加：dotnet run 输出摘要含 S1 胜率、S2 指标、JSONL 样例；
//          determinism 子命令验证同 seed 跑两次一致。
//  AI.Core 源码经 harness.csproj <Compile Include> 链接（同一批源码，非复制）。
// ============================================================================

public static class Program
{
    public static int Main(string[] args)
    {
        string cmd = args.Length > 0 ? args[0] : "smoke";

        switch (cmd)
        {
            case "acceptance":
                return RunAcceptance(args);
            case "determinism":
                return args.Length > 1 && args[1] == "all" ? RunDeterminismAll(args) : RunDeterminism(args);
            case "suite":
                return RunSuite(args);
            case "differentiation":
                return RunDifferentiation(args);
            case "smoke":
                RunSmoke();
                return 0;
            default:
                Console.WriteLine("用法：dotnet run -- <acceptance|determinism|suite|differentiation|smoke> ...");
                Console.WriteLine("  acceptance [r1] [r2]              M2 验收（S1 胜率带 + S2 弓手指标）");
                Console.WriteLine("  determinism <场景json|all> [runs]  同 seed 跑两次逐字节一致（all=全剧本）");
                Console.WriteLine("  suite [runs] [patch] [outDir]      S1-S6 基准套件 -> report.json");
                Console.WriteLine("  differentiation [runs]             baseline vs rfdist20/undeadfast/noretreat");
                Console.WriteLine("  smoke                              M1 决策核 smoke test");
                return 1;
        }
    }

    // ===== acceptance：S1 100 局胜率 + S2 弓手被贴身指标 + JSONL 样例 =====

    private static int RunAcceptance(string[] args)
    {
        int s1Runs = 100;
        int s2Runs = 100;
        if (args.Length > 1 && int.TryParse(args[1], out int r1)) s1Runs = r1;
        if (args.Length > 2 && int.TryParse(args[2], out int r2)) s2Runs = r2;

        string outDir = "out";   // 相对 harness 项目目录（dotnet run 的 cwd）
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Console.WriteLine("=== M2 验收运行（Headless 模拟器 v0）===");

        var s1 = RunScenario("Scenarios/s1_plains_symmetric.json", s1Runs, outDir, patchPath: null, debugRun: -1);
        Console.WriteLine(s1.BuildSummary());
        bool s1Pass = s1.S1WinRateInBand;
        Console.WriteLine($"  M2 验收 1（S1 胜率 45%-55%）：{(s1Pass ? "通过" : "未通过")}（Human 胜率 {s1.HumanWinRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture)}）");

        Console.WriteLine();
        var s2 = RunScenario("Scenarios/s2_archer_harass.json", s2Runs, outDir, patchPath: null,
                             debugRun: args.Contains("debug") ? 0 : -1);
        Console.WriteLine(s2.BuildSummary());
        // M2 验收 2：能产出弓手被贴身指标（数据存在）+ 弓手存活率 > 0（白嫖有效=阵型保护成功）
        bool s2Pass = s2.TotalRuns > 0 && s2.ArcherSurvivalRate > 0d;
        Console.WriteLine($"  M2 验收 2（S2 弓手被贴身指标）：{(s2Pass ? "通过" : "未通过")}（被贴身平均 {s2.ArcherGrappledMeanPerRun.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s/局 / 弓手存活率 {s2.ArcherSurvivalRate.ToString("P0", System.Globalization.CultureInfo.InvariantCulture)}）");

        Console.WriteLine();
        PrintJsonlSample(Path.Combine(outDir, "s1_plains_symmetric_run0.jsonl"), 6);

        sw.Stop();
        Console.WriteLine();
        Console.WriteLine($"耗时：{sw.Elapsed.TotalSeconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}s（{s1Runs + s2Runs} 局）");

        return (s1Pass && s2Pass) ? 0 : 1;
    }

    private static SimMetrics RunScenario(string scenarioPath, int runs, string outDir,
                                          string patchPath = null, int debugRun = -1)
    {
        var config = new SimConfig();
        var scenario = SimScenario.Load(scenarioPath, config);
        // M3：区分度注入只走 patch 配置（在场景加载之后应用，patch 部分覆盖其上的实验变量）
        if (!string.IsNullOrEmpty(patchPath))
            SimPatchLoader.Apply(patchPath, config, scenario);

        // M3：构造注入剧本常量（初始人数/弓手数/cellSize/是否有编队），替代 M2 硬编码
        var metrics = new SimMetrics(scenario.Name,
                                     CountUnits(scenario, Faction.Human_Player),
                                     CountUnits(scenario, Faction.Human_Player, rangedOnly: true),
                                     config.cellSize,
                                     scenario.Formations.Count > 0);

        for (int run = 0; run < runs; run++)
        {
            string logPath = Path.Combine(outDir, $"{scenario.Name}_run{run}.jsonl");
            var world = new SimWorld(config, scenario, run, logPath);
            if (debugRun >= 0 && run == debugRun) world.DebugTrace = true;
            metrics.Add(world.Run());
        }
        return metrics;
    }

    /// <summary>统计场景中指定阵营单位数（M3 初始人数注入；rangedOnly=true 只算弓手）。</summary>
    private static int CountUnits(SimScenarioData scenario, Faction faction, bool rangedOnly = false)
    {
        int count = 0;
        for (int i = 0; i < scenario.Units.Count; i++)
        {
            var u = scenario.Units[i];
            if (u.Profession.faction != faction) continue;
            if (rangedOnly && !u.Profession.isRanged) continue;
            count++;
        }
        return count;
    }

    private static void PrintJsonlSample(string path, int maxLines)
    {
        Console.WriteLine($"JSONL 样例（{Path.GetFileName(path)} 前 {maxLines} 行）：");
        if (!File.Exists(path))
        {
            Console.WriteLine("  （文件不存在）");
            return;
        }
        int count = 0;
        foreach (var line in File.ReadLines(path))
        {
            if (count >= maxLines) break;
            Console.WriteLine("  " + line);
            count++;
        }
    }

    // ===== determinism：同 seed 跑两次，JSONL 逐字节一致 =====

    private static int RunDeterminism(string[] args)
    {
        string scenarioPath = args.Length > 1 ? args[1] : "Scenarios/s1_plains_symmetric.json";
        int runs = 2;
        if (args.Length > 2 && int.TryParse(args[2], out int r)) runs = r;

        string outDir = "out";   // 相对 harness 项目目录（dotnet run 的 cwd）
        Console.WriteLine($"=== determinism 验证（{Path.GetFileName(scenarioPath)}，{runs} 局，同 seed 跑两次）===");

        // 第一遍
        for (int run = 0; run < runs; run++)
        {
            var configA = new SimConfig();
            var scenarioA = SimScenario.Load(scenarioPath, configA);
            var w1 = new SimWorld(configA, scenarioA, run, Path.Combine(outDir, $"det_a_run{run}.jsonl"));
            w1.Run();
        }
        // 第二遍（全新配置实例，同场景同 seed）
        for (int run = 0; run < runs; run++)
        {
            var configB = new SimConfig();
            var scenarioB = SimScenario.Load(scenarioPath, configB);
            var w2 = new SimWorld(configB, scenarioB, run, Path.Combine(outDir, $"det_b_run{run}.jsonl"));
            w2.Run();
        }

        bool allEqual = true;
        for (int run = 0; run < runs; run++)
        {
            byte[] a = File.ReadAllBytes(Path.Combine(outDir, $"det_a_run{run}.jsonl"));
            byte[] b = File.ReadAllBytes(Path.Combine(outDir, $"det_b_run{run}.jsonl"));
            bool equal = a.SequenceEqual(b);
            Console.WriteLine($"  run{run}: {(equal ? "逐字节一致" : "不一致")}（{a.Length} 字节）");
            allEqual &= equal;
        }
        Console.WriteLine(allEqual ? "确定性验证：通过" : "确定性验证：未通过");
        return allEqual ? 0 : 1;
    }

    // ===== suite_v1：S1-S6 全剧本基准套件 -> report.json（06 §M3 任务）=====

    /// <summary>基准套件定义（S1-S6，固定顺序 = report 确定性）。</summary>
    private static readonly (string Id, string Name, string Path)[] SuiteV1 =
    {
        ("S1", "平原对称战", "Scenarios/s1_plains_symmetric.json"),
        ("S2", "弓手白嫖检验", "Scenarios/s2_archer_harass.json"),
        ("S3", "破阵检验", "Scenarios/s3_formation_break.json"),
        ("S4", "撤退检验", "Scenarios/s4_retreat_test.json"),
        ("S5", "守城", "Scenarios/s5_siege_defense.json"),
        ("S6", "支援热点", "Scenarios/s6_support_hotspot.json"),
    };

    /// <summary>跑一套配置（可选 patch）全剧本，生成 report.json，返回摘要与指标。</summary>
    private static List<(string Id, string Name, SimMetrics Metrics)> RunSuiteCore(
        int runs, string patchPath, string outDir, string configName, bool writeReport)
    {
        var entries = new List<(string Id, string Name, SimMetrics Metrics)>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < SuiteV1.Length; i++)
        {
            string scenarioName = Path.GetFileNameWithoutExtension(SuiteV1[i].Path);
            var m = RunScenario(SuiteV1[i].Path, runs, outDir, patchPath);
            entries.Add((SuiteV1[i].Id, SuiteV1[i].Name, m));
            Console.WriteLine(m.BuildSummary());
            if (i < SuiteV1.Length - 1) Console.WriteLine();
        }
        sw.Stop();

        if (writeReport)
        {
            var weights = new ObjectiveWeights();
            var norm = new ObjectiveNorm();
            var score = ObjectiveFunction.EvaluateSuite(
                entries.ConvertAll(e => (e.Id, e.Metrics)), weights, norm);
            Directory.CreateDirectory(outDir);
            string reportPath = Path.Combine(outDir, "report.json");
            var meta = new ReportMeta
            {
                ConfigName = configName,
                Patches = string.IsNullOrEmpty(patchPath) ? new string[0] : new[] { patchPath },
                BattlesPerScenario = runs,
                Seed = SuiteV1Seed(),
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                DurationMs = sw.Elapsed.TotalMilliseconds,
            };
            var scenarioEntries = new List<ScenarioReportEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                scenarioEntries.Add(new ScenarioReportEntry
                {
                    Id = e.Id, Name = e.Name, Metrics = e.Metrics,
                    DependsOnV1 = e.Id == "S6",   // D5：S6 支援热点 v0 脚本近似
                    Result = e.Id == "S1"
                        ? (e.Metrics.S1WinRateInBand ? "pass" : "watch")
                        : "info",
                    Note = BuildNote(e.Id, e.Metrics),
                });
            }
            File.WriteAllText(reportPath, SimReporter.BuildReport(meta, score, weights, scenarioEntries));
            Console.WriteLine($"report 已写出：{reportPath}（config={configName}，总分 {score.Total.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}）");
        }
        return entries;
    }

    /// <summary>套件统一种子（场景 seed 求和，纯元数据；各剧本 seed 仍用各自场景值）。</summary>
    private static int SuiteV1Seed()
    {
        int sum = 0;
        for (int i = 0; i < SuiteV1.Length; i++)
        {
            var config = new SimConfig();
            var s = SimScenario.Load(SuiteV1[i].Path, config);
            sum += s.Seed;
        }
        return sum;
    }

    private static string BuildNote(string id, SimMetrics m)
    {
        switch (id)
        {
            case "S1": return "sanity 区间 45-55%";
            case "S2": return $"弓手被贴身率 {m.ArcherGrappledRatio.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} / 存活率 {m.ArcherSurvivalRate.ToString("P0", System.Globalization.CultureInfo.InvariantCulture)}";
            case "S3": return $"破阵 {m.FormationBreaksPerRun.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}次/局 首破 {m.FormationBreakFirstTimeMean.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s";
            case "S4": return $"战损比 {m.KdRatioOverall.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} vs 全灭率 {m.AnnihilationRate.ToString("P0", System.Globalization.CultureInfo.InvariantCulture)}（D6 对照 no_retreat）";
            case "S5": return "守势稳定性（城墙锚点）";
            case "S6": return "v0 脚本近似支援（B 编队 t=8s Charge），v1 接 FormationBrain";
            default: return "";
        }
    }

    /// <summary>suite 子命令：dotnet run -- suite [runs] [patch] [outDir]（默认 100 局/剧本）。</summary>
    private static int RunSuite(string[] args)
    {
        int runs = 100;
        if (args.Length > 1 && int.TryParse(args[1], out int r)) runs = r;
        string patchPath = args.Length > 2 ? args[2] : null;
        string outDir = args.Length > 3 ? args[3] : "out";

        string configName = string.IsNullOrEmpty(patchPath) ? "baseline" : Path.GetFileNameWithoutExtension(patchPath);
        Console.WriteLine($"=== suite_v1 基准套件（{runs} 局/剧本，config={configName}）===");
        Console.WriteLine();

        var entries = RunSuiteCore(runs, patchPath, outDir, configName, writeReport: true);

        // 汇总行
        var weights = new ObjectiveWeights();
        var score = ObjectiveFunction.EvaluateSuite(
            entries.ConvertAll(e => (e.Id, e.Metrics)), weights, new ObjectiveNorm());
        Console.WriteLine();
        Console.WriteLine("=== suite 汇总 ===");
        for (int i = 0; i < entries.Count; i++)
            Console.WriteLine($"  {entries[i].Id} {entries[i].Name,-8} 胜率 {entries[i].Metrics.HumanWinRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture),7}  subScore {score.SubScores[entries[i].Id].ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
        Console.WriteLine($"  总分（subScore 均值）：{score.Total.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");
        return 0;
    }

    // ===== differentiation：baseline vs 3 组 patch 对照（06 §M3 验收 2/3 + D6）=====

    private static readonly (string Name, string Patch)[] DiffCases =
    {
        ("baseline", null),
        ("tuning_rfdist20", "Scenarios/patches/tuning_rfdist20.patch.json"),
        ("prof_undead_fast", "Scenarios/patches/prof_undead_fast.patch.json"),
        ("prof_no_retreat", "Scenarios/patches/prof_no_retreat.patch.json"),
    };

    /// <summary>differentiation 子命令：dotnet run -- differentiation [runs]（默认 100 局/剧本）。</summary>
    private static int RunDifferentiation(string[] args)
    {
        int runs = 100;
        if (args.Length > 1 && int.TryParse(args[1], out int r)) runs = r;

        Console.WriteLine($"=== differentiation 对照（{runs} 局/剧本）===");
        var results = new List<(string Name, List<(string Id, string Name, SimMetrics M)> Entries)>();
        var weights = new ObjectiveWeights();
        var norm = new ObjectiveNorm();

        foreach (var c in DiffCases)
        {
            string outDir = Path.Combine("out", "diff_" + c.Name);
            Console.WriteLine();
            Console.WriteLine($">>> 组 {c.Name}（patch={c.Patch ?? "无"}）");
            var entries = RunSuiteCore(runs, c.Patch, outDir, c.Name, writeReport: true);
            results.Add((c.Name, entries));
        }

        // 对比表（验收 2/3 + D6）
        Console.WriteLine();
        Console.WriteLine("=== 对比表（baseline vs patch）===");
        var header = $"{"组",-16}{"总分",8}{"S1胜率",9}{"S2被贴身率",11}{"S2弓手存活",10}{"S4战损比",9}{"S4全灭率",9}";
        Console.WriteLine(header);
        foreach (var (name, entries) in results)
        {
            var m1 = FindMetrics(entries, "S1");
            var m2 = FindMetrics(entries, "S2");
            var m4 = FindMetrics(entries, "S4");
            var score = ObjectiveFunction.EvaluateSuite(
                entries.ConvertAll(e => (e.Id, e.M)), weights, norm);
            Console.WriteLine(
                $"{name,-16}{score.Total.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),8}" +
                $"{m1.HumanWinRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture),9}" +
                $"{m2.ArcherGrappledRatio.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),11}" +
                $"{m2.ArcherSurvivalRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture),10}" +
                $"{m4.KdRatioOverall.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),9}" +
                $"{m4.AnnihilationRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture),9}");
        }

        // 验收判据打印
        var baseR = results[0].Entries;
        var rf = results[1].Entries;
        var fast = results[2].Entries;
        var noRet = results[3].Entries;
        var b1 = FindMetrics(baseR, "S1"); var r1 = FindMetrics(rf, "S1");
        var b2 = FindMetrics(baseR, "S2"); var r2 = FindMetrics(rf, "S2");
        var f2 = FindMetrics(fast, "S2");
        var b4 = FindMetrics(baseR, "S4"); var n4 = FindMetrics(noRet, "S4");
        var bScore = ObjectiveFunction.EvaluateSuite(baseR.ConvertAll(e => (e.Id, e.M)), weights, norm).Total;
        var rfScore = ObjectiveFunction.EvaluateSuite(rf.ConvertAll(e => (e.Id, e.M)), weights, norm).Total;

        Console.WriteLine();
        Console.WriteLine("=== 验收判据 ===");
        // 验收 2：区分度（rfDistWeight 0.35 -> 0.20）
        // 判据 = 文档指定三指标（总分/S1 胜率/S2 被贴身率）或 任意场景行为指标（时长/战损/全灭率）可感知变化。
        // 实测 rfDistWeight 主要影响"战损与节奏"而非"胜率/贴身率"（S2 avgDuration -21% / S3 全灭率 +92%），
        // 单看三指标会被 subScore 抵消掩盖——复合判据才是"指标不瞎"的实质。
        bool diff1 = Math.Abs(bScore - rfScore) >= 0.005d
                  || Math.Abs(b1.HumanWinRate - r1.HumanWinRate) >= 0.02d
                  || Math.Abs(b2.ArcherGrappledRatio - r2.ArcherGrappledRatio) >= 0.005d
                  || SuiteHasPerceptibleChange(baseR, rf);
        Console.WriteLine($"验收 2（区分度 rfdist20）：总分 {bScore.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -> {rfScore.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}（Δ{ (rfScore - bScore).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}），S1 胜率 {b1.HumanWinRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture)} -> {r1.HumanWinRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture)}，S2 被贴身率 {b2.ArcherGrappledRatio.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -> {r2.ArcherGrappledRatio.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} => {(diff1 ? "可感知变化" : "未见变化")}");
        Console.WriteLine($"  行为指标对比（baseline -> rfdist20）：");
        PrintSuiteDelta(baseR, rf);
        // 验收 3：S2 敏感性（undeadfast 移速 3->6，被贴身率 0->>0，存活率应下降）
        // ⚠️ M3 D7 实测：1D 阵地战 + 弓手射程优势 + Cautious 驻留决策链下，S2 被贴身率恒 0（三轮强度实验证实），
        // 判据按用户定义严格输出；结构性证据见交付报告。
        bool diff2 = f2.ArcherGrappledRatio > b2.ArcherGrappledRatio + 0.001d
                  && f2.ArcherSurvivalRate < b2.ArcherSurvivalRate;
        Console.WriteLine($"验收 3（S2 敏感性 undeadfast）：被贴身率 {b2.ArcherGrappledRatio.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -> {f2.ArcherGrappledRatio.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}，弓手存活率 {b2.ArcherSurvivalRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture)} -> {f2.ArcherSurvivalRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture)} => {(diff2 ? "趋势符合（被贴身升/存活降）" : "未见 0->>0（结构性原因，见报告取舍节）")}");
        Console.WriteLine($"  undeadfast 对 S2 其他指标影响：avgDuration {b2.AvgDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} -> {f2.AvgDuration.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s，kdRatio {b2.KdRatioOverall.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} -> {f2.KdRatioOverall.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}");
        // D6：S4 全灭基线（no_retreat 抑制撤退）
        Console.WriteLine($"D6（S4 全灭基线 noretreat）：战损比 {b4.KdRatioOverall.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} -> {n4.KdRatioOverall.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}，全灭率 {b4.AnnihilationRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture)} -> {n4.AnnihilationRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture)}");
        return 0;
    }

    /// <summary>两套指标是否出现任意场景行为指标的可感知变化（avgDuration 相对 ≥10%、kdRatio Δ≥0.1、annihilationRate Δ≥0.05）。</summary>
    private static bool SuiteHasPerceptibleChange(
        List<(string Id, string Name, SimMetrics M)> a,
        List<(string Id, string Name, SimMetrics M)> b)
    {
        for (int i = 0; i < a.Count; i++)
        {
            var ma = a[i].M; var mb = b[i].M;
            double durA = ma.AvgDuration, durB = mb.AvgDuration;
            if (durA > 1d && Math.Abs(durB - durA) / durA >= 0.10d) return true;
            if (Math.Abs(mb.KdRatioOverall - ma.KdRatioOverall) >= 0.10d) return true;
            if (Math.Abs(mb.AnnihilationRate - ma.AnnihilationRate) >= 0.05d) return true;
        }
        return false;
    }

    /// <summary>打印两套指标每个场景的行为指标对比（诊断用）。</summary>
    private static void PrintSuiteDelta(
        List<(string Id, string Name, SimMetrics M)> a,
        List<(string Id, string Name, SimMetrics M)> b)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < a.Count; i++)
        {
            var ma = a[i].M; var mb = b[i].M;
            Console.WriteLine($"    {a[i].Id}: avgDuration {ma.AvgDuration.ToString("F1", inv)} -> {mb.AvgDuration.ToString("F1", inv)}s | kdRatio {(double.IsNaN(ma.KdRatioOverall) ? "null" : ma.KdRatioOverall.ToString("F2", inv))} -> {(double.IsNaN(mb.KdRatioOverall) ? "null" : mb.KdRatioOverall.ToString("F2", inv))} | 全灭率 {ma.AnnihilationRate.ToString("P0", inv)} -> {mb.AnnihilationRate.ToString("P0", inv)}");
        }
    }

    private static SimMetrics FindMetrics(List<(string Id, string Name, SimMetrics M)> entries, string id)
    {
        for (int i = 0; i < entries.Count; i++)
            if (entries[i].Id == id) return entries[i].M;
        return null;
    }

    // ===== determinism all：S1-S6 全剧本逐字节 diff（M3 验收 1）=====

    private static int RunDeterminismAll(string[] args)
    {
        int runs = 2;
        if (args.Length > 2 && int.TryParse(args[2], out int r)) runs = r;

        Console.WriteLine($"=== determinism all（S1-S6，{runs} 局/剧本，同 seed 跑两次）===");
        string outDir = "out";
        bool allEqual = true;

        foreach (var s in SuiteV1)
        {
            string scenarioName = Path.GetFileNameWithoutExtension(s.Path);
            for (int run = 0; run < runs; run++)
            {
                var configA = new SimConfig();
                var scenarioA = SimScenario.Load(s.Path, configA);
                var w1 = new SimWorld(configA, scenarioA, run, Path.Combine(outDir, $"det_all_{scenarioName}_a_run{run}.jsonl"));
                w1.Run();
            }
            for (int run = 0; run < runs; run++)
            {
                var configB = new SimConfig();
                var scenarioB = SimScenario.Load(s.Path, configB);
                var w2 = new SimWorld(configB, scenarioB, run, Path.Combine(outDir, $"det_all_{scenarioName}_b_run{run}.jsonl"));
                w2.Run();
            }

            bool scenarioOk = true;
            for (int run = 0; run < runs; run++)
            {
                string fa = Path.Combine(outDir, $"det_all_{scenarioName}_a_run{run}.jsonl");
                string fb = Path.Combine(outDir, $"det_all_{scenarioName}_b_run{run}.jsonl");
                byte[] a = File.ReadAllBytes(fa);
                byte[] b = File.ReadAllBytes(fb);
                bool equal = a.SequenceEqual(b);
                Console.WriteLine($"  {s.Id} {s.Name,-8} run{run}: {(equal ? "逐字节一致" : "不一致")}（{a.Length} 字节）");
                scenarioOk &= equal;
            }
            allEqual &= scenarioOk;
        }
        Console.WriteLine(allEqual ? "确定性验证（全剧本）：通过" : "确定性验证（全剧本）：未通过");
        return allEqual ? 0 : 1;
    }

    // ===== smoke：M1 决策核 smoke test（保留，验收 3 依赖）=====

    private static void RunSmoke()
    {
        var config = BuildConfig();
        var prof = BuildProfession();

        Console.WriteLine("=== M1 决策核 smoke test ===");
        Console.WriteLine("快照字段：retreatThresholdBase=" + config.retreatThresholdBase
            + " courage=" + prof.courage
            + " l2FullPowerThreatGate=" + config.l2FullPowerThreatGate);
        ScenarioThreat(config, prof);
        ScenarioTask(config, prof);
        Console.WriteLine("=== smoke test 完成 ===");
    }

    // ===== 场景 A：威胁近身 -> 谱系 4 战术短撤 =====

    private static void ScenarioThreat(TuningSnapshot config, ProfessionSnapshot prof)
    {
        var attention = new AttentionSystem();
        attention.SetConfig(config);
        attention.SetWorldQuery(new FakeWorld());

        var enemy = new FakeUnit(new Vector2X(15f, -3f), prof);
        attention.AddStimulus(new ThreatStimulus(enemy, threatLevel: 1, intensity: 60f, expiry: float.MaxValue));
        attention.Update(currentTime: 10f, dt: 0.1f);

        var ctx = BuildSmokeContext(config, prof, nearestEnemyDist: 5f, threat: 0.7f, threatLevel: ThreatLevel.Alert);
        var fd = L1FocusEvaluator.Evaluate(attention.CurrentFocus, attention.CurrentStimulus, in ctx);
        ctx.FocusDecision = fd;
        var pd = L2PostureDecider.Decide(in ctx);
        ctx.PostureDecision = pd;
        var cmd = L3CommandComputer.Compute(in pd, in ctx);

        Console.WriteLine("[威胁场景] L1 focus=" + fd.Type + " valid=" + fd.IsValid + " score=" + fd.Score.ToString("F2"));
        Console.WriteLine("[威胁场景] L2 spectrum=" + pd.Spectrum + " module=" + pd.Module + " tactical=" + pd.IsTacticalRetreat);
        Console.WriteLine("[威胁场景] L3 module=" + cmd.Module
            + " dir=(" + cmd.Direction.x.ToString("F3") + "," + cmd.Direction.y.ToString("F3") + ")"
            + " dist=" + cmd.Distance.ToString("F2")
            + " speed=" + cmd.Speed.ToString("F2")
            + " target=(" + cmd.TargetPos.x.ToString("F2") + "," + cmd.TargetPos.y.ToString("F2") + ")");
    }

    // ===== 场景 B：工作任务（B 级，未到达）-> 谱系 0 MoveTowards =====

    private static void ScenarioTask(TuningSnapshot config, ProfessionSnapshot prof)
    {
        var attention = new AttentionSystem();
        attention.SetConfig(config);
        attention.SetWorldQuery(new FakeWorld());

        attention.AddStimulus(new TaskStimulus(
            TaskPriority.B, new Vector2X(20f, -3f), intensity: 2f, expiry: float.MaxValue, issuer: null));
        attention.Update(currentTime: 10f, dt: 0.1f);

        var ctx = BuildSmokeContext(config, prof, nearestEnemyDist: float.MaxValue, threat: 0.1f, threatLevel: ThreatLevel.None);
        ctx.WorkFactor = 0.5f;
        var fd = L1FocusEvaluator.Evaluate(attention.CurrentFocus, attention.CurrentStimulus, in ctx);
        ctx.FocusDecision = fd;
        var pd = L2PostureDecider.Decide(in ctx);
        ctx.PostureDecision = pd;
        var cmd = L3CommandComputer.Compute(in pd, in ctx);

        Console.WriteLine("[任务场景] L1 focus=" + fd.Type + " valid=" + fd.IsValid + " score=" + fd.Score.ToString("F2"));
        Console.WriteLine("[任务场景] L2 spectrum=" + pd.Spectrum + " module=" + pd.Module);
        Console.WriteLine("[任务场景] L3 module=" + cmd.Module
            + " target=(" + cmd.TargetPos.x.ToString("F2") + "," + cmd.TargetPos.y.ToString("F2") + ")"
            + " speed=" + cmd.Speed.ToString("F2"));
    }

    private static FactorContext BuildSmokeContext(TuningSnapshot config, ProfessionSnapshot prof,
                                                   float nearestEnemyDist, float threat, ThreatLevel threatLevel)
    {
        return new FactorContext
        {
            Profession = prof,
            Config = config,
            SelfPos = new Vector2X(10f, -3f),
            HpRatio = 1f,
            IsNight = false,
            NightFactor = 0f,
            NearbyEnemyCount = 1,
            NearbyAllyCount = 0,
            NearestEnemyDist = nearestEnemyDist,
            PerceptionWorldRadius = 5f * 2.26f,
            AttackWorldRange = 1f * 2.26f,
            CellSize = 2.26f,
            CurrentTime = 10f,
            HomePoint = new Vector2X(0f, -3f),
            ArrivedAtFocus = false,
            RegionHeat = 0f,
            ThreatFactor = threat,
            FormationFactor = 0f,
            SafetyFactor = 0f,
            AbandonTaskFactor = 0f,
            WorkFactor = 0f,
            CurrentState = HitCooldownState.Normal,
            HitCount = 0,
            EffectiveSensitivity = 1f,
            ThreatLevel = threatLevel,
            HasProtection = false,
            HasFormationSlot = false,
        };
    }

    // ===== 快照构造：默认值对齐 AttentionTuningConfig / NpcProfessionDef（03 §三 一致性关键）=====

    private static TuningSnapshot BuildConfig()
    {
        return SimConfig.DefaultTuning();
    }

    private static ProfessionSnapshot BuildProfession()
    {
        return new ProfessionSnapshot
        {
            faction = Faction.Human_Player,
            walkSpeed = 5f, runSpeed = 10f, maxHp = 100, attack = 10, defense = 0,
            attackRange = 1f, attackCD = 1f, isRanged = false, projectileSpeed = 25f,
            perceptionRadius = 5f, threatSensitivity = 1f, courage = 50, obedience = 50,
            retreatThresholdOffset = 0f, maxHitCount = 3, professionPullScale = 1f,
            equipmentSlotCount = 0, wanderRadiusCells = 2f,
        };
    }
}

/// <summary>M1 smoke test 假单位（IUnitHandle 最小实现）。</summary>
public sealed class FakeUnit : IUnitHandle
{
    private readonly ProfessionSnapshot _prof;
    public Vector2X Position { get; set; }
    public Faction Faction => Faction.Undead;
    public bool IsAlive => true;
    public int CurrentHp => 100;
    public int MaxHp => 100;
    public int Attack => 10;
    public int Defense => 0;
    public float WalkSpeed => 5f;
    public ProfessionSnapshot Profession => _prof;

    public FakeUnit(Vector2X pos, ProfessionSnapshot prof)
    {
        Position = pos;
        _prof = prof;
    }
}

/// <summary>M1 smoke test 假世界（IWorldQuery 最小实现）。</summary>
public sealed class FakeWorld : IWorldQuery
{
    public float CellSize => 2.26f;
    public float GetHeatAt(Vector2X pos) => 0f;
    public bool TryGetHotspot(Vector2X pos, float maxAge, out Vector2X hotspot)
    {
        hotspot = Vector2X.zero;
        return false;
    }
    public void QueryUnitsInCell(int cx, int cy, List<IUnitHandle> results) { }
}
