// ============================================================================
//  M3 Headless 模拟器 - ObjectiveFunction 目标函数 v0（打分态）
//  04_模拟器规格.md §八：
//    score = 胜率×0.4 + 战损比归一×0.3 − 弓手被贴身率×0.2 + 槽位保持×0.1
//  M3 归一决策点（06 §M3）：
//    - D1 kdNorm = Clamp01(kdRatio / kdReference)，kdRatio = ΣHuman杀敌/ΣHuman阵亡
//      （跨局聚合防除0；阵亡=0 无伤亡全胜 -> kdNorm=1）；kdReference 默认 1.0 可配
//    - D2 弓手被贴身率见 SimMetrics.ArcherGrappledRatio（无弓手剧本=0 不惩罚）
//    - D3 槽位保持见 SimMetrics.SlotHold（无编队剧本=1）
//  权重即 T1 参数（champion 机制管理，M4）：默认 0.4/0.3/0.2/0.1，ObjectiveWeights 可改。
//  总分（suite 聚合）= 各剧本 sub-score 均值（每剧本等权；S6 dependsOnV1 时计入但 report 标注）。
//  确定性：纯 double 聚合，无 RNG。
// ============================================================================

using System.Collections.Generic;

/// <summary>目标函数权重（T1 参数，M4 champion 管理；默认 04 §八 0.4/0.3/0.2/0.1）。</summary>
public sealed class ObjectiveWeights
{
    public float WinRate = 0.4f;
    public float KdRatio = 0.3f;
    public float GrappledPenalty = 0.2f;
    public float SlotHold = 0.1f;
}

/// <summary>目标函数归一参数（D1 kdReference 可配；其余分母是场景固有量）。</summary>
public sealed class ObjectiveNorm
{
    public float KdReference = 1.0f;
}

/// <summary>单剧本评分 + 总分（SimReporter 消费）。</summary>
public sealed class ObjectiveScore
{
    /// <summary>每剧本 sub-score（key = 场景 Id，顺序 = 传入顺序）。</summary>
    public Dictionary<string, double> SubScores = new Dictionary<string, double>();
    public double Total;
}

/// <summary>
/// 目标函数 v0：按 04 §八 公式计算单剧本 score，跨剧本总分 = 均值。
/// 不做归一化缩放（winRate/kdNorm/grappled/slotHold 均已在 0-1 量纲）。
/// </summary>
public static class ObjectiveFunction
{
    public static double Evaluate(SimMetrics m, ObjectiveWeights w, ObjectiveNorm n)
    {
        // D1：kdNorm = Clamp01(kdRatio / kdReference)；阵亡=0（无伤亡全胜）-> 1.0
        double kdNorm;
        if (m.HumanTotalDeaths <= 0)
        {
            kdNorm = 1d;
        }
        else
        {
            double kdRatio = m.HumanTotalKills / (double)m.HumanTotalDeaths;
            kdNorm = Clamp01(kdRatio / n.KdReference);
        }

        double score = m.HumanWinRate * w.WinRate
                     + kdNorm * w.KdRatio
                     - m.ArcherGrappledRatio * w.GrappledPenalty
                     + m.SlotHold * w.SlotHold;
        return Clamp01(score);
    }

    /// <summary>多剧本评分：sub-score 记入字典（场景 Id 序），总分 = 均值（每剧本等权）。</summary>
    public static ObjectiveScore EvaluateSuite(
        IReadOnlyList<(string Id, SimMetrics Metrics)> scenarios,
        ObjectiveWeights w, ObjectiveNorm n)
    {
        var score = new ObjectiveScore();
        double total = 0d;
        for (int i = 0; i < scenarios.Count; i++)
        {
            double sub = Evaluate(scenarios[i].Metrics, w, n);
            score.SubScores[scenarios[i].Id] = sub;
            total += sub;
        }
        score.Total = scenarios.Count > 0 ? total / scenarios.Count : 0d;
        return score;
    }

    public static double Clamp01(double v) => v < 0d ? 0d : (v > 1d ? 1d : v);
}
