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
                return RunDeterminism(args);
            case "smoke":
                RunSmoke();
                return 0;
            default:
                Console.WriteLine("用法：dotnet run -- <acceptance|determinism|smoke> [runs]");
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

        var s1 = RunScenario("Scenarios/s1_plains_symmetric.json", s1Runs, outDir, debugRun: -1);
        Console.WriteLine(s1.BuildSummary());
        bool s1Pass = s1.S1WinRateInBand;
        Console.WriteLine($"  M2 验收 1（S1 胜率 45%-55%）：{(s1Pass ? "通过" : "未通过")}（Human 胜率 {s1.HumanWinRate.ToString("P1", System.Globalization.CultureInfo.InvariantCulture)}）");

        Console.WriteLine();
        var s2 = RunScenario("Scenarios/s2_archer_harass.json", s2Runs, outDir,
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

    private static SimMetrics RunScenario(string scenarioPath, int runs, string outDir, int debugRun = -1)
    {
        var config = new SimConfig();
        var scenario = SimScenario.Load(scenarioPath, config);
        var metrics = new SimMetrics(scenario.Name);

        for (int run = 0; run < runs; run++)
        {
            string logPath = Path.Combine(outDir, $"{scenario.Name}_run{run}.jsonl");
            var world = new SimWorld(config, scenario, run, logPath);
            if (debugRun >= 0 && run == debugRun) world.DebugTrace = true;
            metrics.Add(world.Run());
        }
        return metrics;
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
