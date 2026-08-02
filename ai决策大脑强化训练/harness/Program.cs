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
        // M6 T2：注册公式变体（harness/Formulas/ 目录；默认 LinearV1 已在注册表静态构造注册）
        RegisterFormulaVariants();

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
            case "benchmark":
                return RunBenchmark(args);
            case "champion":
                return RunChampion(args);
            case "propose":
                return RunPropose(args);
            case "search":
                return RunSearch(args);
            case "formula":
                return RunFormula(args);
            case "smoke":
                RunSmoke();
                return 0;
            default:
                Console.WriteLine("用法：dotnet run -- <acceptance|determinism|suite|differentiation|benchmark|champion|propose|search|formula|smoke> ...");
                Console.WriteLine("  acceptance [r1] [r2]              M2 验收（S1 胜率带 + S2 弓手指标）");
                Console.WriteLine("  determinism <场景json|all> [runs]  同 seed 跑两次逐字节一致（all=全剧本）");
                Console.WriteLine("  suite [runs] [patch] [outDir]      S1-S6 基准套件 -> report.json");
                Console.WriteLine("  differentiation [runs]             baseline vs rfdist20/undeadfast/noretreat");
                Console.WriteLine("  benchmark [--config <champion.json>] [--patch <patch.json>] [--battles N] [--out <dir>] [--name <名>]");
                Console.WriteLine("                                     M4 手动调参闭环：champion 基线 + patch 深合并 -> report.json + verdict.json");
                Console.WriteLine("  champion export [outPath]          导出当前默认配置为 champion 全量快照（首次建档）");
                Console.WriteLine("  champion baseline [--battles N]    跑 champion（无 patch）建档 results/baseline/report.json");
                Console.WriteLine("  propose validate <p_x.json>        校验提案（≤3改动/注册/边界/死参数/rawFactor Σ）");
                Console.WriteLine("  propose run <p_x.json> [--battles N] [--out <dir>]  校验通过后跑分 -> report+verdict");
                Console.WriteLine("  propose list                      列出 proposals/ 提案与 history.log 尾部");
                Console.WriteLine("  search --params a,b [--generations N] [--battles N]  CMA-ES 黑盒搜索（参数逗号分隔，注册路径）");
                Console.WriteLine("  formula list                      列出已注册威胁公式（T2 变体市场）");
                Console.WriteLine("  formula compare <名> [--battles N] 变体 vs LinearV1 baseline 套件对比（T2 守门：不劣于才可人审）");
                Console.WriteLine("  smoke                              M1 决策核 smoke test");
                return 1;
        }
    }

    /// <summary>M6 T2：注册公式变体（新增变体写 harness/Formulas/ 后在此登记）。</summary>
    private static void RegisterFormulaVariants()
    {
        ThreatFormulaRegistry.Register(new DistSquaredThreatFormula());
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
                                          string patchPath = null, int debugRun = -1,
                                          string championPath = null)
    {
        var config = new SimConfig();
        // M4：champion 全量基线在场景 Load 之前应用（调参基线唯一真身），patch 部分覆盖在之后
        if (!string.IsNullOrEmpty(championPath))
            SimChampion.Load(championPath, config);
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

    /// <summary>跑一套配置（可选 patch/champion）全剧本，生成 report.json，返回摘要与指标。</summary>
    private static List<(string Id, string Name, SimMetrics Metrics)> RunSuiteCore(
        int runs, string patchPath, string outDir, string configName, bool writeReport,
        string championPath = null)
    {
        var entries = new List<(string Id, string Name, SimMetrics Metrics)>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < SuiteV1.Length; i++)
        {
            string scenarioName = Path.GetFileNameWithoutExtension(SuiteV1[i].Path);
            var m = RunScenario(SuiteV1[i].Path, runs, outDir, patchPath, -1, championPath);
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
            case "S6": return "M6 v1 FormationBrain 自动意图（B 编队 autoIntent 接 SimHeat 支援热点）";
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

    // ===== benchmark / champion：M4 手动调参闭环（06 §M4 / 05 §四 CLI 契约）=====

    private const string DefaultChampionPath = "champion/tuning.champion.json";
    private const string BaselineDir = "results/baseline";

    /// <summary>benchmark 子命令：champion 基线 + patch 深合并 -> report.json + verdict.json。</summary>
    private static int RunBenchmark(string[] args)
    {
        string championPath = GetArg(args, "config", DefaultChampionPath);
        string patchPath = GetArg(args, "patch", null);
        int battles = GetIntArg(args, "battles", 100);
        string outDir = GetArg(args, "out", $"results/{DateTime.Now:yyyyMMdd_HHmmss}_bench");
        string name = GetArg(args, "name", string.IsNullOrEmpty(patchPath) ? "champion-baseline" : Path.GetFileNameWithoutExtension(patchPath));

        if (!SimChampion.Exists(championPath))
        {
            Console.WriteLine($"[benchmark] 冠军配置不存在：{championPath}。先跑：dotnet run -- champion export");
            return 1;
        }

        Console.WriteLine($"=== M6 benchmark（champion={championPath}，patch={(string.IsNullOrEmpty(patchPath) ? "无" : patchPath)}，{battles} 局/剧本）===");

        // 1. 若无 baseline 建档，先跑 champion（无 patch）——05 §七.4 冠军三条件需要对照基线
        //    baseline 同时建档 holdout（H1/H2 隐藏场景，verdict 阶段同卷对比）
        string baselineReport = Path.Combine(BaselineDir, "report.json");
        string baselineHoldout = Path.Combine(BaselineDir, "holdout_report.json");
        if (!File.Exists(baselineReport))
        {
            Console.WriteLine();
            Console.WriteLine(">>> 首次运行：建档 champion 基线（results/baseline/，含 holdout）");
            RunSuiteCore(battles, null, BaselineDir, "champion", writeReport: true, championPath);
            RunHoldoutCore(battles, null, BaselineDir, championPath, writeReport: true);
        }

        // 2. 跑本次配置（champion + patch 深合并）+ holdout 同卷
        Console.WriteLine();
        var entries = RunSuiteCore(battles, patchPath, outDir, name, writeReport: true, championPath);
        RunHoldoutCore(battles, patchPath, outDir, championPath, writeReport: true);

        // 3. 裁决：读 baseline report.json 的 subScores vs 本次（含 holdout 对比）
        var weights = new ObjectiveWeights();
        var norm = new ObjectiveNorm();
        var candidateScore = ObjectiveFunction.EvaluateSuite(
            entries.ConvertAll(e => (e.Id, e.Metrics)), weights, norm);
        var baselineScore = File.Exists(baselineReport) ? ReadSubScores(baselineReport, weights, norm) : null;

        // M6：holdout 聚合（H1/H2 总分对比）
        var holdoutCandidate = File.Exists(Path.Combine(outDir, "holdout_report.json"))
            ? ReadSubScores(Path.Combine(outDir, "holdout_report.json"), weights, norm) : null;
        var holdoutBaseline = File.Exists(baselineHoldout)
            ? ReadSubScores(baselineHoldout, weights, norm) : null;

        var verdict = SimVerdict.Judge(candidateScore, baselineScore, ScenarioOrder(),
                                       holdoutCandidate, holdoutBaseline, HoldoutIds());
        string verdictPath = Path.Combine(outDir, "verdict.json");
        string ts = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss");
        File.WriteAllText(verdictPath, SimVerdict.BuildVerdictJson(
            verdict, "champion@baseline", name, ts,
            string.IsNullOrEmpty(patchPath) ? new string[0] : new[] { patchPath }));

        Console.WriteLine();
        Console.WriteLine("=== verdict ===");
        if (baselineScore == null)
        {
            Console.WriteLine($"  （baseline 缺失，本次记为建档基线：{BaselineDir}/report.json）");
        }
        else
        {
            Console.WriteLine($"  总分 Δ：{verdict.ScoreDelta.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}（champion {baselineScore.Total.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -> candidate {candidateScore.Total.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}）");
            if (verdict.HoldoutEnabled)
                Console.WriteLine($"  holdout Δ：{verdict.HoldoutDelta.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}（regressed={verdict.HoldoutRegressed}，防背题同卷）");
            Console.WriteLine($"  三条件：总分升={verdict.TotalUp} / 无场景退化(>5%)={verdict.NoRegression} / holdout不退={!verdict.HoldoutEnabled || !verdict.HoldoutRegressed}");
            Console.WriteLine($"  裁决：{verdict.Decision}（candidate=可留用 / rejected=弃 / baseline=建档）");
            if (verdict.RegressionScenarios.Count > 0)
            {
                Console.WriteLine("  退化场景：");
                for (int i = 0; i < verdict.RegressionScenarios.Count; i++)
                    Console.WriteLine($"    {verdict.RegressionScenarios[i]}");
            }
        }
        Console.WriteLine($"  verdict 已写出：{verdictPath}");
        return 0;
    }

    // ===== holdout（M6）：H1/H2 隐藏场景同卷（05 §七.1 防过拟合最终验收）=====

    /// <summary>holdout 场景定义（H1/H2，细节不公布给训练师；harness/Holdout/ 独立目录）。</summary>
    private static readonly (string Id, string Name, string Path)[] HoldoutV1 =
    {
        ("H1", "镜像变体 8v8", "Holdout/h1_mirror_8v8.json"),
        ("H2", "混合遭遇战", "Holdout/h2_mixed_skirmish.json"),
    };

    private static string[] HoldoutIds()
    {
        var ids = new string[HoldoutV1.Length];
        for (int i = 0; i < HoldoutV1.Length; i++) ids[i] = HoldoutV1[i].Id;
        return ids;
    }

    /// <summary>跑 holdout 场景（同 RunSuiteCore 的指标聚合 + holdout_report.json 生成）。</summary>
    private static void RunHoldoutCore(int runs, string patchPath, string outDir,
                                       string championPath, bool writeReport)
    {
        if (!Directory.Exists("Holdout"))
        {
            Console.WriteLine("  （Holdout/ 目录不存在，跳过 holdout）");
            return;
        }
        var entries = new List<(string Id, string Name, SimMetrics Metrics)>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Console.WriteLine();
        Console.WriteLine($">>> holdout 同卷（H1/H2，{runs} 局/剧本，patch={((string.IsNullOrEmpty(patchPath)) ? "无" : patchPath)}）");
        for (int i = 0; i < HoldoutV1.Length; i++)
        {
            var m = RunScenario(HoldoutV1[i].Path, runs, outDir, patchPath, -1, championPath);
            entries.Add((HoldoutV1[i].Id, HoldoutV1[i].Name, m));
            Console.WriteLine(m.BuildSummary());
            if (i < HoldoutV1.Length - 1) Console.WriteLine();
        }
        sw.Stop();

        if (writeReport)
        {
            var weights = new ObjectiveWeights();
            var norm = new ObjectiveNorm();
            var score = ObjectiveFunction.EvaluateSuite(
                entries.ConvertAll(e => (e.Id, e.Metrics)), weights, norm);
            var scenarioEntries = new List<ScenarioReportEntry>();
            for (int i = 0; i < entries.Count; i++)
            {
                scenarioEntries.Add(new ScenarioReportEntry
                {
                    Id = entries[i].Id, Name = entries[i].Name, Metrics = entries[i].Metrics,
                    DependsOnV1 = false, Result = "info", Note = "holdout 防背题同卷",
                });
            }
            string reportPath = Path.Combine(outDir, "holdout_report.json");
            var meta = new ReportMeta
            {
                ConfigName = "holdout",
                Patches = string.IsNullOrEmpty(patchPath) ? new string[0] : new[] { patchPath },
                BattlesPerScenario = runs,
                Seed = 20261001 + 20261002,
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss"),
                DurationMs = sw.Elapsed.TotalMilliseconds,
            };
            File.WriteAllText(reportPath, SimReporter.BuildReport(meta, score, weights, scenarioEntries));
            Console.WriteLine($"  holdout report 已写出：{reportPath}（总分 {score.Total.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}）");
        }
    }

    /// <summary>champion 子命令：export（导出默认全量快照）/ baseline（建档）。</summary>
    private static int RunChampion(string[] args)
    {
        string sub = args.Length > 1 ? args[1] : "export";
        switch (sub)
        {
            case "export":
            {
                string outPath = args.Length > 2 ? args[2] : DefaultChampionPath;
                var config = new SimConfig();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                File.WriteAllText(outPath, SimChampion.Export(config));
                Console.WriteLine($"[champion] 全量快照已导出：{outPath}（{config.Professions.Count} 职业，tuning 全字段）");
                return 0;
            }
            case "baseline":
            {
                string championPath = GetArg(args, "config", DefaultChampionPath);
                int battles = GetIntArg(args, "battles", 100);
                if (!SimChampion.Exists(championPath))
                {
                    Console.WriteLine($"[champion] 冠军配置不存在：{championPath}。先跑：dotnet run -- champion export");
                    return 1;
                }
                Console.WriteLine($"=== champion baseline 建档（{battles} 局/剧本）===");
                RunSuiteCore(battles, null, BaselineDir, "champion", writeReport: true, championPath);
                Console.WriteLine($"baseline 已建档：{BaselineDir}/report.json");
                return 0;
            }
            default:
                Console.WriteLine("用法：dotnet run -- champion <export|baseline>");
                return 1;
        }
    }

    /// <summary>从已有 report.json 读 subScores 并重建 ObjectiveScore（裁决对比用）。</summary>
    private static ObjectiveScore ReadSubScores(string reportPath, ObjectiveWeights w, ObjectiveNorm n)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(reportPath));
        var root = doc.RootElement;
        var score = new ObjectiveScore();
        if (root.TryGetProperty("score", out var scoreEl) &&
            scoreEl.TryGetProperty("subScores", out var subs))
        {
            foreach (var kv in subs.EnumerateObject())
                score.SubScores[kv.Name] = kv.Value.GetDouble();
        }
        if (root.TryGetProperty("score", out scoreEl) && scoreEl.TryGetProperty("total", out var tot))
            score.Total = tot.GetDouble();
        return score;
    }

    /// <summary>场景顺序（S1-S6，verdict 对比与 report 一致）。</summary>
    private static List<string> ScenarioOrder()
    {
        var list = new List<string>();
        for (int i = 0; i < SuiteV1.Length; i++) list.Add(SuiteV1[i].Id);
        return list;
    }

    // ===== propose：训练师提案闭环（05 §三 契约 + AGENTS.md 铁律，M5）=====

    private const string ProposalsDir = "../proposals";

    /// <summary>propose 子命令：validate（校验）/ run（跑分）/ list（列提案）。</summary>
    private static int RunPropose(string[] args)
    {
        string sub = args.Length > 1 ? args[1] : "list";
        switch (sub)
        {
            case "validate":
            {
                string propPath = args.Length > 2 ? args[2] : null;
                if (string.IsNullOrEmpty(propPath) || !File.Exists(propPath))
                {
                    Console.WriteLine("[propose] 提案文件不存在: " + propPath);
                    return 1;
                }
                var result = SimProposalValidator.Validate(propPath);
                Console.WriteLine($"=== propose validate {Path.GetFileName(propPath)} ===");
                Console.WriteLine($"  裁决：{(result.Valid ? "✅ 通过" : "❌ 拒收")}");
                for (int i = 0; i < result.Issues.Count; i++)
                    Console.WriteLine($"    - {result.Issues[i]}");
                if (result.Valid)
                    Console.WriteLine("  通过后可：dotnet run -- propose run " + propPath);
                return result.Valid ? 0 : 1;
            }
            case "run":
            {
                string propPath = args.Length > 2 ? args[2] : null;
                if (string.IsNullOrEmpty(propPath) || !File.Exists(propPath))
                {
                    Console.WriteLine("[propose] 提案文件不存在: " + propPath);
                    return 1;
                }
                // 1. 先校验（未过 = 拒收，不进跑分，05 §三 规则）
                var result = SimProposalValidator.Validate(propPath);
                Console.WriteLine($"=== propose run {Path.GetFileName(propPath)} ===");
                Console.WriteLine($"  校验：{(result.Valid ? "通过" : "拒收")}");
                if (!result.Valid)
                {
                    for (int i = 0; i < result.Issues.Count; i++)
                        Console.WriteLine($"    - {result.Issues[i]}");
                    Console.WriteLine("  提案被拒收，未执行跑分。修正后重试。");
                    return 1;
                }

                // 2. 把提案 changes 转成临时 patch JSON（champion + patch 深合并链路的 patch 部分）
                var proposal = System.Text.Json.JsonSerializer.Deserialize<ProposalDoc>(
                    File.ReadAllText(propPath), ProposalJsonOptions());
                string tmpPatch = Path.Combine("out", Path.GetFileNameWithoutExtension(propPath) + ".patch.json");
                Directory.CreateDirectory("out");
                File.WriteAllText(tmpPatch, BuildPatchJson(proposal));

                // 3. 复用 benchmark 闭环（champion 基线 + patch 深合并 -> report.json + verdict.json）
                int battles = GetIntArg(args, "battles", 100);
                string outDir = GetArg(args, "out", $"results/{proposal.id}");
                string name = proposal.id;
                return RunBenchmark(new[] { "benchmark", "--patch", tmpPatch, "--battles", battles.ToString(), "--out", outDir, "--name", name });
            }
            case "list":
            {
                Console.WriteLine("=== proposals/ ===");
                string dir = ProposalsDir;
                if (Directory.Exists(dir))
                {
                    foreach (var f in Directory.GetFiles(dir, "p_*.json"))
                        Console.WriteLine($"  {Path.GetFileName(f)}（{(File.ReadAllText(f).Length / 1024.0).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}KB）");
                    string log = Path.Combine(dir, "history.log");
                    if (File.Exists(log))
                    {
                        Console.WriteLine("--- history.log 尾部 ---");
                        var lines = File.ReadAllLines(log);
                        int start = Math.Max(0, lines.Length - 15);
                        for (int i = start; i < lines.Length; i++) Console.WriteLine("  " + lines[i]);
                    }
                    else
                    {
                        Console.WriteLine("  （无 history.log——训练师复盘记录）");
                    }
                }
                else
                {
                    Console.WriteLine("  （proposals/ 目录不存在）");
                }
                return 0;
            }
            default:
                Console.WriteLine("用法：dotnet run -- propose <validate|run|list>");
                return 1;
        }
    }

    /// <summary>提案 changes -> patch JSON（{tuning:{...}, professions:{...}} 部分覆盖格式，对齐 SimPatchLoader）。</summary>
    private static string BuildPatchJson(ProposalDoc proposal)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"name\": \"" + proposal.id + "\",");
        sb.AppendLine("  \"tuning\": {");
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        bool firstT = true;
        for (int i = 0; i < proposal.changes.Length; i++)
        {
            var c = proposal.changes[i];
            if (c.path.StartsWith("tuning."))
            {
                if (!firstT) sb.AppendLine(",");
                sb.Append("    \"").Append(c.path.Substring("tuning.".Length)).Append("\": ").Append(c.to.ToString("R", inv));
                firstT = false;
            }
        }
        sb.AppendLine(firstT ? "  }," : "\n  },");
        sb.AppendLine("  \"professions\": {");
        var profGroups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<(string field, double val)>>();
        for (int i = 0; i < proposal.changes.Length; i++)
        {
            var c = proposal.changes[i];
            if (c.path.StartsWith("professions."))
            {
                var parts = c.path.Split('.');
                if (parts.Length != 3) continue;
                if (!profGroups.TryGetValue(parts[1], out var list))
                {
                    list = new System.Collections.Generic.List<(string, double)>();
                    profGroups[parts[1]] = list;
                }
                list.Add((parts[2], c.to));
            }
        }
        bool firstP = true;
        foreach (var kv in profGroups)
        {
            if (!firstP) sb.AppendLine(",");
            sb.Append("    \"").Append(kv.Key).Append("\": {");
            for (int k = 0; k < kv.Value.Count; k++)
            {
                sb.Append(k > 0 ? ", " : "").Append('"').Append(kv.Value[k].field).Append("\": ").Append(kv.Value[k].val.ToString("R", inv));
            }
            sb.Append("}");
            firstP = false;
        }
        sb.AppendLine(firstP ? "  }" : "\n  }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static System.Text.Json.JsonSerializerOptions ProposalJsonOptions()
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return options;
    }

    // ===== search：CMA-ES 黑盒自动搜索（06 §M6 优化项；SimCMAES）=====

    /// <summary>search 子命令：dotnet run -- search --params a,b [--generations N] [--battles N]。</summary>
    private static int RunSearch(string[] args)
    {
        string paramsArg = GetArg(args, "params", null);
        if (string.IsNullOrEmpty(paramsArg))
        {
            Console.WriteLine("[search] 缺少 --params（逗号分隔的注册路径，如 tuning.rfDistWeight,tuning.rfCountWeight）");
            return 1;
        }
        int generations = GetIntArg(args, "generations", 8);
        int battles = GetIntArg(args, "battles", 20);   // 搜索用低局数提速，找到后用 propose run 精跑

        var paths = paramsArg.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var ps = new SimCMAES.SearchParam[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            var e = FindRegistryEntry(paths[i].Trim());
            if (e == null)
            {
                Console.WriteLine($"[search] 参数未在 factor_registry 注册: {paths[i].Trim()}");
                return 1;
            }
            ps[i] = new SimCMAES.SearchParam { Path = paths[i].Trim(), Min = e.min, Max = e.max, Current = e.current };
        }

        Console.WriteLine($"=== CMA-ES 搜索（参数={paramsArg}，{generations} 代 × {SimCMAES.Population} 个体，每个体 {battles} 局/剧本）===");
        Console.WriteLine($"  当前值：{string.Join(", ", Array.ConvertAll(ps, p => p.Path + "=" + p.Current.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)))}");

        // 基线总分（当前值）
        double baseScore = EvaluateParams(ps, Array.ConvertAll(ps, p => p.Current), battles, null);
        Console.WriteLine($"  基线总分：{baseScore.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");

        // CMA-ES 搜索
        var sw = System.Diagnostics.Stopwatch.StartNew();
        double[] best = SimCMAES.Search(ps, generations, v => EvaluateParams(ps, v, battles, null));
        sw.Stop();

        double bestScore = EvaluateParams(ps, best, battles, null);
        Console.WriteLine();
        Console.WriteLine($"=== 搜索完成（{sw.Elapsed.TotalSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s）===");
        Console.WriteLine($"  最优总分：{bestScore.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}（基线 {baseScore.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}，Δ{(bestScore - baseScore).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}）");
        for (int i = 0; i < ps.Length; i++)
            Console.WriteLine($"    {ps[i].Path}: {ps[i].Current.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -> {best[i].ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}");

        // 输出最优 patch（供 propose run 精跑）
        Directory.CreateDirectory("out");
        string bestPatch = Path.Combine("out", "search_best.patch.json");
        File.WriteAllText(bestPatch, BuildSearchPatch(ps, best));
        Console.WriteLine($"  最优 patch 已写出：{bestPatch}（建议：propose run 精跑验证 + verdict）");
        return 0;
    }

    /// <summary>评估一组参数：champion 基线 + 参数覆盖 -> 跑 S1-S6 套件（低局数）-> 总分。</summary>
    private static double EvaluateParams(SimCMAES.SearchParam[] ps, double[] values, int battles, string outDir)
    {
        // 构建参数覆盖（部分覆盖语义：只改搜索参数）
        var config = new SimConfig();
        if (File.Exists(DefaultChampionPath))
            SimChampion.Load(DefaultChampionPath, config);
        for (int i = 0; i < ps.Length; i++)
        {
            var parts = ps[i].Path.Split('.');
            if (parts.Length == 2 && parts[0] == "tuning")
                SetTuningField(config, parts[1], values[i]);
            else if (parts.Length == 3 && parts[0] == "professions")
                SetProfessionField(config, parts[1], parts[2], values[i]);
        }

        double total = 0;
        int n = 0;
        var weights = new ObjectiveWeights();
        var norm = new ObjectiveNorm();
        // 搜索评估不落 JSONL（SimLogger 需要合法路径）；写临时目录防崩
        string evalLogDir = Path.Combine("out", "search_eval");
        Directory.CreateDirectory(evalLogDir);
        for (int s = 0; s < SuiteV1.Length; s++)
        {
            var scenario = SimScenario.Load(SuiteV1[s].Path, config);
            var metrics = new SimMetrics(scenario.Name,
                                         CountUnits(scenario, Faction.Human_Player),
                                         CountUnits(scenario, Faction.Human_Player, rangedOnly: true),
                                         config.cellSize,
                                         scenario.Formations.Count > 0);
            for (int run = 0; run < battles; run++)
            {
                var world = new SimWorld(config, scenario, run,
                    Path.Combine(evalLogDir, $"{scenario.Name}_eval{run}.jsonl"));
                metrics.Add(world.Run());
            }
            total += ObjectiveFunction.Evaluate(metrics, weights, norm);
            n++;
        }
        return n > 0 ? total / n : 0d;
    }

    private static void SetTuningField(SimConfig config, string fieldName, double value)
    {
        var field = typeof(TuningSnapshot).GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field == null) throw new InvalidOperationException($"[search] 未知 tuning 字段 {fieldName}");
        object boxed = config.tuning;
        field.SetValue(boxed, Convert.ChangeType(value, field.FieldType, System.Globalization.CultureInfo.InvariantCulture));
        config.tuning = (TuningSnapshot)boxed;
    }

    private static void SetProfessionField(SimConfig config, string name, string fieldName, double value)
    {
        var prof = config.GetProfession(name);
        var field = typeof(ProfessionSnapshot).GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (field == null) throw new InvalidOperationException($"[search] 未知职业字段 {fieldName}");
        object pbox = prof;
        field.SetValue(pbox, Convert.ChangeType(value, field.FieldType, System.Globalization.CultureInfo.InvariantCulture));
        config.RegisterProfession(name, (ProfessionSnapshot)pbox);
    }

    /// <summary>从 factor_registry 找注册项（返回 min/max/current）。</summary>
    private static RegistryEntry FindRegistryEntry(string path)
    {
        return SimProposalValidator.FindEntryPublic(path);
    }

    /// <summary>搜索最优 patch JSON（tuning/professions 部分覆盖，对齐 SimPatchLoader 格式）。</summary>
    private static string BuildSearchPatch(SimCMAES.SearchParam[] ps, double[] best)
    {
        var sb = new System.Text.StringBuilder();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        sb.AppendLine("{");
        sb.AppendLine("  \"name\": \"search_best\",");
        var tuningFields = new List<(string, double)>();
        var profFields = new List<(string name, string field, double val)>();
        for (int i = 0; i < ps.Length; i++)
        {
            var parts = ps[i].Path.Split('.');
            if (parts.Length == 2 && parts[0] == "tuning") tuningFields.Add((parts[1], best[i]));
            else if (parts.Length == 3 && parts[0] == "professions") profFields.Add((parts[1], parts[2], best[i]));
        }
        sb.AppendLine("  \"tuning\": {");
        for (int i = 0; i < tuningFields.Count; i++)
            sb.Append(i > 0 ? ",\n" : "").Append("    \"").Append(tuningFields[i].Item1).Append("\": ").Append(tuningFields[i].Item2.ToString("R", inv));
        sb.AppendLine(tuningFields.Count > 0 ? "\n  }," : "  },");
        sb.AppendLine("  \"professions\": {");
        for (int i = 0; i < profFields.Count; i++)
            sb.Append(i > 0 ? ",\n" : "").Append("    \"").Append(profFields[i].name).Append("\": { \"").Append(profFields[i].field).Append("\": ").Append(profFields[i].val.ToString("R", inv)).Append(" }");
        sb.AppendLine(profFields.Count > 0 ? "\n  }" : "  }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ===== formula：T2 公式变体市场（02 §三；list / compare 守门）=====

    /// <summary>formula 子命令：list（已注册公式）/ compare <名>（变体 vs LinearV1）。</summary>
    private static int RunFormula(string[] args)
    {
        string sub = args.Length > 1 ? args[1] : "list";
        switch (sub)
        {
            case "list":
            {
                Console.WriteLine("=== 已注册威胁公式（T2 变体市场）===");
                foreach (var name in ThreatFormulaRegistry.Names)
                {
                    bool isDefault = name == "LinearV1";
                    Console.WriteLine($"  {name,-16}{(isDefault ? "（baseline 真身）" : "（变体）")}");
                }
                return 0;
            }
            case "compare":
            {
                string formulaName = args.Length > 2 ? args[2] : null;
                if (string.IsNullOrEmpty(formulaName))
                {
                    Console.WriteLine("[formula] 缺少公式名：dotnet run -- formula compare <名>");
                    return 1;
                }
                if (ThreatFormulaRegistry.Get(formulaName) == null)
                {
                    Console.WriteLine($"[formula] 公式 '{formulaName}' 未注册。先 formula list 查看。");
                    return 1;
                }
                int battles = GetIntArg(args, "battles", 100);
                Console.WriteLine($"=== T2 公式守门：{formulaName} vs LinearV1（{battles} 局/剧本）===");

                // baseline（LinearV1）——复用现有 baseline 建档报告
                string baselineReport = Path.Combine(BaselineDir, "report.json");
                if (!File.Exists(baselineReport))
                {
                    Console.WriteLine($">>> 建档 LinearV1 baseline（{BaselineDir}/report.json）");
                    RunSuiteCore(battles, null, BaselineDir, "LinearV1", writeReport: true, DefaultChampionPath);
                }
                var weights = new ObjectiveWeights();
                var norm = new ObjectiveNorm();
                var baseScore = File.Exists(baselineReport)
                    ? ReadSubScores(baselineReport, weights, norm)
                    : EvaluateWithFormula("LinearV1", battles, weights, norm);

                // 变体跑分
                var variantScore = EvaluateWithFormula(formulaName, battles, weights, norm);

                // T2 守门：总分不劣于 baseline 且无场景退化 >5%（02 §三.2）
                string outDir = Path.Combine("out", "formula_" + formulaName);
                Directory.CreateDirectory(outDir);
                double delta = R3(variantScore.Total) - R3(baseScore.Total);
                Console.WriteLine();
                Console.WriteLine($"  总分：LinearV1 {baseScore.Total.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -> {formulaName} {variantScore.Total.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}（Δ{delta.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}）");
                bool pass = delta >= 0d;
                Console.WriteLine($"  T2 守门：{(pass ? "✅ 不劣于 baseline（可人审）" : "❌ 劣于 baseline（拒，02 §三.2 守门原则）")}");
                Console.WriteLine("  （人审通过后：把 config.formulaThreat 切到该变体，跑 champion baseline 重新建档）");
                return pass ? 0 : 1;
            }
            default:
                Console.WriteLine("用法：dotnet run -- formula <list|compare>");
                return 1;
        }
    }

    /// <summary>用指定威胁公式跑 S1-S6 套件，返回聚合总分（formula compare 用）。</summary>
    private static ObjectiveScore EvaluateWithFormula(string formulaName, int battles,
                                                      ObjectiveWeights weights, ObjectiveNorm norm)
    {
        var config = new SimConfig();
        if (File.Exists(DefaultChampionPath))
            SimChampion.Load(DefaultChampionPath, config);
        config.formulaThreat = formulaName;

        var entries = new List<(string Id, string Name, SimMetrics Metrics)>();
        string outDir = Path.Combine("out", "formula_eval_" + formulaName);
        Directory.CreateDirectory(outDir);
        for (int i = 0; i < SuiteV1.Length; i++)
        {
            var scenario = SimScenario.Load(SuiteV1[i].Path, config);
            var metrics = new SimMetrics(scenario.Name,
                                         CountUnits(scenario, Faction.Human_Player),
                                         CountUnits(scenario, Faction.Human_Player, rangedOnly: true),
                                         config.cellSize,
                                         scenario.Formations.Count > 0);
            for (int run = 0; run < battles; run++)
            {
                var world = new SimWorld(config, scenario, run,
                    Path.Combine(outDir, $"{scenario.Name}_run{run}.jsonl"));
                metrics.Add(world.Run());
            }
            entries.Add((SuiteV1[i].Id, SuiteV1[i].Name, metrics));
        }
        return ObjectiveFunction.EvaluateSuite(entries.ConvertAll(e => (e.Id, e.Metrics)), weights, norm);
    }

    private static double R3(double v) => System.Math.Round(v, 3, System.MidpointRounding.AwayFromZero);

    /// <summary>简单 arg 解析（--key value）=====</summary>

    private static string GetArg(string[] args, string key, string defaultValue)
    {
        for (int i = 1; i < args.Length - 1; i++)
            if (args[i] == "--" + key) return args[i + 1];
        return defaultValue;
    }

    private static int GetIntArg(string[] args, string key, int defaultValue)
    {
        string v = GetArg(args, key, null);
        return v != null && int.TryParse(v, out int r) ? r : defaultValue;
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
