// ============================================================================
//  M2 Headless 模拟器 - SimMetrics 指标聚合（打分态，harness 聚合）
//  04_模拟器规格.md §八 指标：
//    - 结果类：胜率 / 战损比 / 平均时长 / 全灭率
//    - 行为类：弓手被贴身总时长（白嫖指标）/ 槽位偏差均值 / 放弃追击次数
//  M2 验收（06 §M2）：S1 胜率 45%-55%；S2 能产出弓手被贴身指标。
// ============================================================================

using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 多局指标聚合器（acceptance 子命令消费）。
/// 每局 Add(SimRunResult)，最后 BuildSummary() 出报告。
/// </summary>
public sealed class SimMetrics
{
    private readonly List<SimRunResult> _results = new List<SimRunResult>();
    private readonly string _scenarioName;

    public SimMetrics(string scenarioName)
    {
        _scenarioName = scenarioName;
    }

    public void Add(SimRunResult r) => _results.Add(r);

    public int TotalRuns => _results.Count;
    public int HumanWins => CountWins("Human");
    public int UndeadWins => CountWins("Undead");
    public int Draws => CountWins("Draw");

    /// <summary>Human 胜率（0-1）。</summary>
    public double HumanWinRate => TotalRuns > 0 ? HumanWins / (double)TotalRuns : 0d;

    /// <summary>平均时长（秒）。</summary>
    public double AvgDuration
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            double sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].Duration;
            return sum / TotalRuns;
        }
    }

    /// <summary>全灭率（未拖到 maxDuration 的局占比 = 有胜者的局）。</summary>
    public double AnnihilationRate
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            int decisive = 0;
            for (int i = 0; i < _results.Count; i++)
                if (_results[i].Winner != "Draw") decisive++;
            return decisive / (double)TotalRuns;
        }
    }

    /// <summary>Human 平均战损比（杀敌/阵亡；满员 6，死亡 = 6 - 存活）。</summary>
    public double AvgKdRatio
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            double sum = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                int humanDead = 6 - r.AliveHuman;
                sum += humanDead > 0 ? r.KillsHuman / (double)humanDead : 0d;
            }
            return sum / TotalRuns;
        }
    }

    /// <summary>弓手被贴身平均每局时长（秒，S2 白嫖指标）。</summary>
    public double ArcherGrappledMeanPerRun
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            double sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].ArcherGrappledTime;
            return sum / TotalRuns;
        }
    }

    /// <summary>弓手平均存活率（局末存活弓手/满编 2，S2 白嫖有效性；1.0=从没被近战摸到）。</summary>
    public double ArcherSurvivalRate
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            double sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].ArcherAlive;
            return sum / (TotalRuns * 2d);
        }
    }

    /// <summary>槽位偏差均值（世界单位，跨局）。</summary>
    public double SlotDevMeanOverall
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            double sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].SlotDevMean;
            return sum / TotalRuns;
        }
    }

    /// <summary>放弃追击总次数（跨局）。</summary>
    public int AbandonChaseTotal
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].AbandonChaseCount;
            return sum;
        }
    }

    private int CountWins(string winner)
    {
        int count = 0;
        for (int i = 0; i < _results.Count; i++)
            if (_results[i].Winner == winner) count++;
        return count;
    }

    /// <summary>验收判据：S1 胜率是否落在 45%-55%（不对称=有 bug，06 §M2）。</summary>
    public bool S1WinRateInBand => HumanWinRate >= 0.45d && HumanWinRate <= 0.55d;

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>报告（P8 dotnet run 摘要用）。</summary>
    public string BuildSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"== {_scenarioName}（{TotalRuns} 局）==");
        sb.AppendLine($"  胜率：Human {HumanWinRate.ToString("P1", Inv)} / Undead {(UndeadWins / (double)System.Math.Max(1, TotalRuns)).ToString("P1", Inv)} / 平局 {Draws}");
        sb.AppendLine($"  平均时长：{AvgDuration.ToString("F1", Inv)}s / 全灭率：{AnnihilationRate.ToString("P1", Inv)} / Human 战损比：{AvgKdRatio.ToString("F2", Inv)}");
        sb.AppendLine($"  弓手被贴身（S2 指标）：平均 {ArcherGrappledMeanPerRun.ToString("F1", Inv)}s/局 / 弓手存活率 {ArcherSurvivalRate.ToString("P0", Inv)}");
        sb.AppendLine($"  槽位偏差均值：{SlotDevMeanOverall.ToString("F2", Inv)}（世界单位）/ 放弃追击总次数：{AbandonChaseTotal}");
        return sb.ToString();
    }
}
