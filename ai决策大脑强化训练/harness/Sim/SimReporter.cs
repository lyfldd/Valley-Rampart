// ============================================================================
//  M3 Headless 模拟器 - SimReporter 多剧本聚合报告（report.json 生成）
//  对齐 schemas/benchmark_report.example.json 结构（meta/score/scenarios/behavior）。
//  训练师读 score/scenarios/behavior 三块；M4 起追加 champion 对比（holdout/verdict）。
//  M3 决策点：
//    - D4 破阵指标（formationBreaksPerRun/formationBreakFirstTime）
//    - D5 S6 支援热点 block 标注 dependsOnV1:true（v0 脚本近似，v1 接 FormationBrain）
//    - D6 S4 全灭基线对比在 differentiation 子命令（同剧本 no_retreat patch 并排）
//  确定性：手拼 JSON（对齐 SimLogger 做法，不走 System.Text.Json float 序列化）；
//    scenarios 按固定顺序（S1-S6），deathsByProfession 用 SortedDictionary（key 序），
//    数字 InvariantCulture 定长；唯一非确定字段 = meta.timestamp/meta.durationMs（运行时元数据）。
// ============================================================================

using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>report 场景条目（SimReporter 输入）。</summary>
public sealed class ScenarioReportEntry
{
    public string Id;
    public string Name;
    public SimMetrics Metrics;
    public bool DependsOnV1;       // D5：S6 支援热点（v0 脚本近似）
    public string Result;          // pass / watch / info（对齐 example.result）
    public string Note;
}

/// <summary>report 元数据（meta 块）。</summary>
public sealed class ReportMeta
{
    public string ConfigName = "baseline";
    public string[] Patches = new string[0];
    public string Suite = "suite_v1";
    public int BattlesPerScenario = 100;
    public int Seed;
    public string Timestamp;       // 运行时元数据（非统计字段）
    public double DurationMs;
}

/// <summary>
/// 多剧本聚合报告生成器：输入每剧本 SimMetrics -> 输出 report.json 字符串（对齐 example 结构）。
/// 数值全部 double 聚合 + InvariantCulture 定长手拼，保证同 seed 同配置输出确定。
/// </summary>
public static class SimReporter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string BuildReport(ReportMeta meta, ObjectiveScore score,
                                     ObjectiveWeights weights,
                                     IReadOnlyList<ScenarioReportEntry> scenarios)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        // ===== meta =====
        sb.Append("  \"meta\": {");
        sb.Append("\"config\": \"").Append(meta.ConfigName).Append("\"");
        sb.Append(", \"patches\": [");
        for (int i = 0; i < meta.Patches.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(Escape(meta.Patches[i])).Append('"');
        }
        sb.Append("], \"suite\": \"").Append(meta.Suite).Append("\"");
        sb.Append(", \"battlesPerScenario\": ").Append(meta.BattlesPerScenario);
        sb.Append(", \"seed\": ").Append(meta.Seed);
        sb.Append(", \"timestamp\": \"").Append(meta.Timestamp).Append("\"");
        sb.Append(", \"durationMs\": ").Append(meta.DurationMs.ToString("F0", Inv));
        sb.AppendLine(" },");

        // ===== score =====
        sb.Append("  \"score\": {");
        sb.Append("\"total\": ").Append(F3(score.Total));
        sb.Append(", \"subScores\": {");
        int idx = 0;
        foreach (var kv in score.SubScores)
        {
            if (idx++ > 0) sb.Append(", ");
            sb.Append('"').Append(kv.Key).Append("\": ").Append(F3(kv.Value));
        }
        sb.Append("}, \"formula\": \"胜率×")
          .Append(F2(weights.WinRate))
          .Append(" + 战损比归一×").Append(F2(weights.KdRatio))
          .Append(" − 弓手被贴身率×").Append(F2(weights.GrappledPenalty))
          .Append(" + 槽位保持×").Append(F2(weights.SlotHold)).Append('"');
        sb.AppendLine(" },");

        // ===== scenarios =====
        sb.AppendLine("  \"scenarios\": [");
        for (int s = 0; s < scenarios.Count; s++)
        {
            var e = scenarios[s];
            var m = e.Metrics;
            sb.Append("    {");
            sb.Append("\"id\": \"").Append(e.Id).Append("\"");
            sb.Append(", \"name\": \"").Append(e.Name).Append("\"");
            sb.Append(", \"winRate\": ").Append(F3(m.HumanWinRate));
            sb.Append(", \"avgDuration\": ").Append(F2(m.AvgDuration));
            sb.Append(", \"annihilationRate\": ").Append(F3(m.AnnihilationRate));
            sb.Append(", \"kdRatio\": ").Append(double.IsNaN(m.KdRatioOverall) ? "null" : F3(m.KdRatioOverall));
            sb.Append(", \"archerPinnedRatio\": ").Append(F3(m.ArcherGrappledRatio));
            sb.Append(", \"archerSurvivalRate\": ").Append(F3(m.ArcherSurvivalRate));
            sb.Append(", \"formationBreaksPerRun\": ").Append(F3(m.FormationBreaksPerRun));
            sb.Append(", \"formationBreakFirstTime\": ").Append(F2(m.FormationBreakFirstTimeMean));
            sb.Append(", \"slotDeviationMean\": ").Append(F3(m.SlotDevMeanOverall));
            sb.Append(", \"slotHold\": ").Append(F3(m.SlotHold));
            sb.Append(", \"retreats\": {")
              .Append("\"tactical\": ").Append(F3(m.RetreatTacticalPerRun))
              .Append(", \"strategic\": ").Append(F3(m.RetreatStrategicPerRun))
              .Append(", \"firstTime\": ").Append(F2(m.RetreatFirstTimeMean))
              .Append("}");
            sb.Append(", \"deathsByProfession\": {");
            int di = 0;
            foreach (var d in m.DeathsByProfessionOverall)
            {
                if (di++ > 0) sb.Append(", ");
                sb.Append('"').Append(d.Key).Append("\": ").Append(d.Value);
            }
            sb.Append("}");
            sb.Append(", \"subScore\": ").Append(F3(score.SubScores.TryGetValue(e.Id, out double sub) ? sub : 0d));
            sb.Append(", \"result\": \"").Append(e.Result).Append("\"");
            sb.Append(", \"note\": \"").Append(e.Note).Append("\"");
            if (e.DependsOnV1)
                sb.Append(", \"dependsOnV1\": true");   // D5：S6 支援热点 v0 脚本近似
            sb.AppendLine(s < scenarios.Count - 1 ? " }," : " }");
        }
        sb.AppendLine("  ],");

        // ===== behavior（跨剧本聚合：死亡职业分布全量 + 撤退次数汇总）=====
        sb.Append("  \"behavior\": {");
        sb.Append("\"deathsByProfession\": {");
        var agg = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
        for (int s = 0; s < scenarios.Count; s++)
        {
            foreach (var d in scenarios[s].Metrics.DeathsByProfessionOverall)
            {
                agg.TryGetValue(d.Key, out int c);
                agg[d.Key] = c + d.Value;
            }
        }
        int bi = 0;
        foreach (var kv in agg)
        {
            if (bi++ > 0) sb.Append(", ");
            sb.Append('"').Append(kv.Key).Append("\": ").Append(kv.Value);
        }
        sb.Append("}");
        sb.AppendLine(" }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string F3(double v) => v.ToString("F3", Inv);
    private static string F2(double v) => v.ToString("F2", Inv);
    private static string F2(float v) => v.ToString("F2", Inv);

    /// <summary>JSON 字符串转义（M6 修复：Windows 路径反斜杠未转义导致 report.json 非法）。</summary>
    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
