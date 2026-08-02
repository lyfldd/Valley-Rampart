// ============================================================================
//  M6 Headless 模拟器 - SimCMAES 本地黑盒自动搜索（纯 C#，无外部依赖）
//  06 §M6 优化项：手动/AI 提案进入平台期后，用 CMA-ES 本地自动搜索收敛。
//  原理（简化 CMA-ES 变体，保证确定性）：
//    - 种群 N=8，每代评估 N 个参数向量 -> 目标函数 -> 排序
//    - 下一代均值 = 前一半（top4）的加权平均，步长自适应（std 缩放）
//    - 边界截断到 registry [min,max]；确定性：SimRng 种子注入
//  用法：
//    dotnet run -- search --params tuning.rfDistWeight,tuning.rfCountWeight --generations 10 --battles 30
//    -> 输出每代最优 + 最终最优 patch JSON（out/search_best.patch.json）+ 总分对比
//  目标函数：复用 ObjectiveFunction（套件总分；battles 少=快搜，找到后 propose run 精跑）
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 简化 CMA-ES 黑盒搜索器：对一组注册参数做多代演化，找最大化套件总分的参数组合。
/// 每代评估 = RunSuiteCore（低局数提速）；输出最优 patch 供 propose run 精跑验证。
/// </summary>
public static class SimCMAES
{
    public const int Population = 8;       // 种群大小（每代评估次数）
    public const int Elite = 4;            // 精英数（前一半）
    public const double InitStd = 0.15;    // 初始步长（相对参数区间宽度）
    public const double MinStd = 0.01;     // 最小步长（收敛终止）

    /// <summary>参数搜索定义（由 --params 解析）。</summary>
    public sealed class SearchParam
    {
        public string Path;
        public double Min;
        public double Max;
        public double Current;
    }

    /// <summary>
    /// 运行搜索。paramPaths 为逗号分隔的注册路径（如 "tuning.rfDistWeight,tuning.rfCountWeight"）。
    /// evaluate 为评估回调（返回套件总分），由 Program 注入（RunSuiteCore + ObjectiveFunction）。
    /// 返回最优参数向量（与 paramPaths 同序）。
    /// </summary>
    public static double[] Search(SearchParam[] ps, int generations,
                                  Func<double[], double> evaluate)
    {
        var rng = new SimRng(20260802);    // 固定种子 -> 确定性搜索
        int dim = ps.Length;

        // 初始化：均值 = 当前值，步长 = 区间宽度 × InitStd
        double[] mu = new double[dim];
        double[] sigma = new double[dim];
        for (int d = 0; d < dim; d++)
        {
            mu[d] = ps[d].Current;
            sigma[d] = Math.Max(MinStd, (ps[d].Max - ps[d].Min) * InitStd);
        }

        double[] best = (double[])mu.Clone();
        double bestScore = double.NegativeInfinity;
        var inv = CultureInfo.InvariantCulture;

        for (int g = 0; g < generations; g++)
        {
            // 采样种群（确定性：rng 序固定）
            double[][] pop = new double[Population][];
            double[] scores = new double[Population];
            double[] popBest = null;
            double popBestScore = double.NegativeInfinity;

            for (int i = 0; i < Population; i++)
            {
                double[] v = new double[dim];
                for (int d = 0; d < dim; d++)
                {
                    double raw = mu[d] + Gaussian(rng) * sigma[d];
                    v[d] = Clamp(raw, ps[d].Min, ps[d].Max);
                }
                pop[i] = v;
                scores[i] = evaluate(v);
                if (scores[i] > popBestScore)
                {
                    popBestScore = scores[i];
                    popBest = v;
                }
            }

            // 精英平均 -> 下一代均值
            int[] order = new int[Population];
            for (int i = 0; i < Population; i++) order[i] = i;
            Array.Sort(order, (a, b) => scores[b].CompareTo(scores[a]));   // 降序

            double[] newMu = new double[dim];
            for (int d = 0; d < dim; d++)
            {
                double sum = 0;
                for (int e = 0; e < Elite; e++)
                    sum += pop[order[e]][d];
                newMu[d] = sum / Elite;
            }

            // 步长自适应：精英分散度大 -> 步长放大；收敛 -> 缩小（防早停）
            for (int d = 0; d < dim; d++)
            {
                double spread = 0;
                for (int e = 0; e < Elite; e++)
                    spread += Math.Abs(pop[order[e]][d] - newMu[d]);
                spread /= Elite;
                double ratio = sigma[d] > 0 ? spread / sigma[d] : 1.0;
                sigma[d] = Clamp(sigma[d] * (0.5 + ratio), MinStd, (ps[d].Max - ps[d].Min) * 0.5);
            }

            mu = newMu;

            if (popBestScore > bestScore)
            {
                bestScore = popBestScore;
                best = popBest;
            }

            Console.WriteLine($"  代 {g + 1}: 最优 {popBestScore.ToString("F3", inv)} @ " + FormatVec(popBest, inv));
        }

        return best;
    }

    /// <summary>标准正态采样（Box-Muller，确定性：消费 rng 两次）。</summary>
    private static double Gaussian(SimRng rng)
    {
        double u1 = Math.Max(rng.Range(0f, 1f), 1e-9);
        double u2 = rng.Range(0f, 1f);
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);

    private static string FormatVec(double[] v, CultureInfo inv)
    {
        var parts = new string[v.Length];
        for (int i = 0; i < v.Length; i++) parts[i] = v[i].ToString("F3", inv);
        return "[" + string.Join(", ", parts) + "]";
    }
}
