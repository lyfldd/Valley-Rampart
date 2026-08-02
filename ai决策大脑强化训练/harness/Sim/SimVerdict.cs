// ============================================================================
//  M4 Headless 模拟器 - SimVerdict 冠军裁决（verdict.json 生成）
//  05 §七 防过拟合裁决逻辑（冠军双条件）：
//    1. 总分升（score.delta > 0）
//    2. 无场景退化 > 5%（退化红线：任何场景 subScore 降幅 >5% 直接拒，即使总分升）
//    holdout：H1/H2 细节不写进训练师可见文件，verdict 阶段由 harness 跑（M6 落地；M4 先占位）
//  输出 verdict.json（对齐 05 §四 CLI 契约输出三件套之一）：
//    {
//      "base": "champion@<ts>", "candidate": "proposals/p_0042.json",
//      "scoreDelta": +0.023, "decision": "candidate" | "rejected" | "baseline",
//      "regression": ["S4: -0.081 (8.1% > 5%)"],   // 退化场景列表（超 5% 红线）
//      "holdout": {"enabled": false, "note": "M6 落地"},  // M4 占位
//      "criteria": {"totalUp": true, "noRegression": false},  // 双条件逐项
//      "timestamp": "..."
//    }
//  确定性：除 timestamp 外全为纯比较，无 RNG。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// 冠军裁决器：candidate 报告 vs champion 基线报告，输出 verdict.json 字符串 + 判定结果。
/// 对比维度：总分 delta + 各场景 subScore 退化检查（05 §七.3 退化红线）。
/// </summary>
public static class SimVerdict
{
    public const double RegressionRedLine = 0.05d;   // 任何场景退化 >5% 直接拒（05 §七.3）

    /// <summary>裁决结果（Program 打印 + verdict.json 生成共用）。</summary>
    public sealed class Result
    {
        public double ScoreDelta;
        public bool TotalUp;                       // 条件 1：总分升
        public List<string> RegressionScenarios = new List<string>();  // 退化场景 "S4: -0.081 (-8.1%)"
        public bool NoRegression;                  // 条件 2：无场景退化超红线
        public string Decision;                    // candidate / rejected / baseline
    }

    /// <summary>
    /// 裁决：champion 基线 subScores vs candidate subScores（均含总分）。
    /// baseScore 为 null 时（无基线）返回 baseline 占位裁决（首跑建档）。
    /// </summary>
    public static Result Judge(ObjectiveScore candidate,
                               ObjectiveScore baseline,
                               IReadOnlyList<string> scenarioOrder)
    {
        var r = new Result();

        // 无基线：首跑建档，不算 candidate 也不算 rejected
        if (baseline == null)
        {
            r.Decision = "baseline";
            return r;
        }

        r.ScoreDelta = R3(candidate.Total) - R3(baseline.Total);
        r.TotalUp = r.ScoreDelta > 0d;

        for (int i = 0; i < scenarioOrder.Count; i++)
        {
            string id = scenarioOrder[i];
            double b = R3(baseline.SubScores.TryGetValue(id, out double bv) ? bv : 0d);
            double c = R3(candidate.SubScores.TryGetValue(id, out double cv) ? cv : 0d);
            double delta = c - b;
            double rel = b > 0d ? delta / b : 0d;
            if (b > 0d && rel < -RegressionRedLine)
            {
                r.RegressionScenarios.Add($"{id}: {delta.ToString("F3", CultureInfo.InvariantCulture)} ({(rel * 100).ToString("F1", CultureInfo.InvariantCulture)}% > 5%)");
            }
        }
        r.NoRegression = r.RegressionScenarios.Count == 0;

        // 冠军双条件（05 §七.4）：总分升 AND 无退化，缺一不可
        r.Decision = (r.TotalUp && r.NoRegression) ? "candidate" : "rejected";
        return r;
    }

    /// <summary>round 3（对齐 report.json 的 F3 精度，防精确值 vs 圆整值误判）。</summary>
    private static double R3(double v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);

    /// <summary>生成 verdict.json 字符串（对齐 05 §四 输出契约）。</summary>
    public static string BuildVerdictJson(Result r, string baseName, string candidateName,
                                          string timestamp, string[] patches)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine("{");
        sb.Append("  \"base\": \"").Append(baseName).Append("\"");
        sb.Append(", \"candidate\": \"").Append(candidateName).Append("\"");
        sb.Append(", \"patches\": [");
        for (int i = 0; i < patches.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(patches[i]).Append('"');
        }
        sb.Append("]");
        sb.Append(", \"scoreDelta\": ").Append(r.ScoreDelta.ToString("F3", inv));
        sb.Append(", \"decision\": \"").Append(r.Decision).Append("\"");
        sb.Append(", \"regression\": [");
        for (int i = 0; i < r.RegressionScenarios.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(r.RegressionScenarios[i]).Append('"');
        }
        sb.Append("]");
        sb.Append(", \"holdout\": {\"enabled\": false, \"note\": \"M6 落地（H1/H2 防过拟合最终验收）\"}");
        sb.Append(", \"criteria\": {\"totalUp\": ").Append(r.TotalUp ? "true" : "false")
          .Append(", \"noRegression\": ").Append(r.NoRegression ? "true" : "false").Append("}");
        sb.Append(", \"timestamp\": \"").Append(timestamp).Append("\"");
        sb.AppendLine();
        sb.AppendLine("}");
        return sb.ToString();
    }
}
