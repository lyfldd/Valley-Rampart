// ============================================================================
//  M2 Headless 模拟器 - SimMetrics 指标聚合（打分态，harness 聚合）
//  04_模拟器规格.md §八 指标：
//    - 结果类：胜率 / 战损比 / 平均时长 / 全灭率
//    - 行为类：弓手被贴身总时长（白嫖指标）/ 槽位偏差均值 / 放弃追击次数 / 撤退时机 / 破阵次数
//  M3 归一决策点（06 §M3）：
//    - D1 战损比归一：kdNorm = Clamp01(kdRatio / kdReference)，kdRatio = ΣHuman杀敌/ΣHuman阵亡
//      （跨局聚合防除0，kdReference 在 ObjectiveFunction 可配）
//    - D2 弓手被贴身率 = Σ被贴身时长 / Σ(初始弓手数×局时长)；无弓手剧本 = 0（不惩罚）
//    - D3 槽位保持 = 1 - Clamp01(槽位偏差均值/(2×cellSize))；无编队剧本 = 1
//  确定性：聚合遍历按 Add() 顺序（局序），跨局累加无浮点乱序（04 §七）。
// ============================================================================

using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 多局指标聚合器（acceptance/suite/differentiation 子命令消费）。
/// 每局 Add(SimRunResult)，最后 BuildSummary() 出报告。
/// M3 构造注入剧本常量（初始人数/弓手数/cellSize/是否有编队），杜绝硬编码。
/// </summary>
public sealed class SimMetrics
{
    private readonly List<SimRunResult> _results = new List<SimRunResult>();
    private readonly string _scenarioName;

    // ===== M3：剧本常量（构造注入，替代 M2 硬编码 6 / 2）=====
    private readonly int _initialHumanCount;
    private readonly int _initialArcherCount;
    private readonly float _cellSize;
    private readonly bool _hasFormation;

    public SimMetrics(string scenarioName, int initialHumanCount, int initialArcherCount,
                      float cellSize, bool hasFormation)
    {
        _scenarioName = scenarioName;
        _initialHumanCount = initialHumanCount;
        _initialArcherCount = initialArcherCount;
        _cellSize = cellSize > 0f ? cellSize : 2.26f;
        _hasFormation = hasFormation;
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

    // ===== M3 D1：战损比（跨局聚合，防除 0）=====

    /// <summary>Human 总杀敌（跨局 ΣKillsHuman）。</summary>
    public int HumanTotalKills
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].KillsHuman;
            return sum;
        }
    }

    /// <summary>Human 总阵亡（跨局 Σ(初始人数 - 局末存活)）。</summary>
    public int HumanTotalDeaths
    {
        get
        {
            int sum = 0;
            for (int i = 0; i < _results.Count; i++)
                sum += System.Math.Max(0, _initialHumanCount - _results[i].AliveHuman);
            return sum;
        }
    }

    /// <summary>Human 平均战损比（每局杀敌/阵亡的均值；M2 指标保留，acceptance 用）。</summary>
    public double AvgKdRatio
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            double sum = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                int humanDead = _initialHumanCount - r.AliveHuman;
                sum += humanDead > 0 ? r.KillsHuman / (double)humanDead : 0d;
            }
            return sum / TotalRuns;
        }
    }

    /// <summary>跨局聚合战损比 = ΣHuman杀敌 / ΣHuman阵亡（D1；阵亡=0 返回 NaN，报告端输出 null）。</summary>
    public double KdRatioOverall
    {
        get
        {
            if (HumanTotalDeaths <= 0) return double.NaN;
            return HumanTotalKills / (double)HumanTotalDeaths;
        }
    }

    // ===== M2 弓手白嫖指标（分母改用构造注入的初始弓手数）=====

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

    /// <summary>弓手平均存活率（局末存活弓手/满编弓手数；无弓手剧本 = 1.0 不惩罚）。</summary>
    public double ArcherSurvivalRate
    {
        get
        {
            if (TotalRuns == 0 || _initialArcherCount <= 0) return 1d;
            double sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].ArcherAlive;
            return sum / (TotalRuns * _initialArcherCount);
        }
    }

    // ===== M3 D2：弓手被贴身率（目标函数输入；无弓手剧本 = 0 不惩罚）=====

    /// <summary>弓手被贴身率 = Σ被贴身时长 / Σ(初始弓手数×局时长)（0-1）。</summary>
    public double ArcherGrappledRatio
    {
        get
        {
            if (TotalRuns == 0 || _initialArcherCount <= 0) return 0d;
            double num = 0, den = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                num += _results[i].ArcherGrappledTime;
                den += _results[i].Duration;
            }
            den *= _initialArcherCount;
            return den > 0d ? num / den : 0d;
        }
    }

    // ===== 槽位 =====

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

    /// <summary>槽位保持（D3）= 1 - Clamp01(槽位偏差均值/(2×cellSize))；无编队剧本 = 1。</summary>
    public double SlotHold
    {
        get
        {
            if (!_hasFormation) return 1d;
            double dev = SlotDevMeanOverall;
            double denom = 2d * _cellSize;
            double ratio = denom > 0d ? dev / denom : 0d;
            return 1d - Clamp01(ratio);
        }
    }

    // ===== M3：破阵 / 撤退 / 死亡职业分布（04 §八 行为类）=====

    /// <summary>破阵次数平均每局（编队解散事件数；D4）。</summary>
    public double FormationBreaksPerRun
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            int sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].FormationBreakCount;
            return sum / (double)TotalRuns;
        }
    }

    /// <summary>首次破阵时刻均值（无破阵局不计入；全无破阵返回 0）。</summary>
    public double FormationBreakFirstTimeMean
    {
        get
        {
            double sum = 0;
            int n = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                if (r.FormationBreakCount > 0 && r.FormationBreakFirstTime >= 0f)
                {
                    sum += r.FormationBreakFirstTime;
                    n++;
                }
            }
            return n > 0 ? sum / n : 0d;
        }
    }

    /// <summary>战术短撤平均每局（谱系 4 tactical）。</summary>
    public double RetreatTacticalPerRun
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            int sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].RetreatTacticalCount;
            return sum / (double)TotalRuns;
        }
    }

    /// <summary>战略撤退平均每局（谱系 4 strategic）。</summary>
    public double RetreatStrategicPerRun
    {
        get
        {
            if (TotalRuns == 0) return 0d;
            int sum = 0;
            for (int i = 0; i < _results.Count; i++) sum += _results[i].RetreatStrategicCount;
            return sum / (double)TotalRuns;
        }
    }

    /// <summary>首次撤退时刻均值（无撤退局不计入；全无撤退返回 0）。</summary>
    public double RetreatFirstTimeMean
    {
        get
        {
            double sum = 0;
            int n = 0;
            for (int i = 0; i < _results.Count; i++)
            {
                var r = _results[i];
                if ((r.RetreatTacticalCount + r.RetreatStrategicCount) > 0 && r.RetreatFirstTime >= 0f)
                {
                    sum += r.RetreatFirstTime;
                    n++;
                }
            }
            return n > 0 ? sum / n : 0d;
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

    /// <summary>死亡职业分布（跨局聚合，SortedDictionary 保证 report 固定 key 序，确定性）。</summary>
    public IReadOnlyDictionary<string, int> DeathsByProfessionOverall
    {
        get
        {
            var map = new SortedDictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = 0; i < _results.Count; i++)
            {
                var d = _results[i].DeathsByProfession;
                if (d == null) continue;
                foreach (var kv in d)
                {
                    map.TryGetValue(kv.Key, out int c);
                    map[kv.Key] = c + kv.Value;
                }
            }
            return map;
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

    private static double Clamp01(double v) => v < 0d ? 0d : (v > 1d ? 1d : v);

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>报告（P8 dotnet run 摘要用）。</summary>
    public string BuildSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"== {_scenarioName}（{TotalRuns} 局）==");
        sb.AppendLine($"  胜率：Human {HumanWinRate.ToString("P1", Inv)} / Undead {(UndeadWins / (double)System.Math.Max(1, TotalRuns)).ToString("P1", Inv)} / 平局 {Draws}");
        sb.AppendLine($"  平均时长：{AvgDuration.ToString("F1", Inv)}s / 全灭率：{AnnihilationRate.ToString("P1", Inv)} / Human 战损比(局均)：{AvgKdRatio.ToString("F2", Inv)} / 战损比(跨局)：{(double.IsNaN(KdRatioOverall) ? "null" : KdRatioOverall.ToString("F2", Inv))}");
        sb.AppendLine($"  弓手被贴身（S2 指标）：平均 {ArcherGrappledMeanPerRun.ToString("F1", Inv)}s/局 / 弓手存活率 {ArcherSurvivalRate.ToString("P0", Inv)} / 被贴身率(D2) {ArcherGrappledRatio.ToString("F3", Inv)}");
        sb.AppendLine($"  槽位偏差均值：{SlotDevMeanOverall.ToString("F2", Inv)}（世界单位）/ 槽位保持(D3) {SlotHold.ToString("F3", Inv)} / 放弃追击总次数：{AbandonChaseTotal}");
        sb.AppendLine($"  撤退：战术 {RetreatTacticalPerRun.ToString("F2", Inv)}/局 战略 {RetreatStrategicPerRun.ToString("F2", Inv)}/局 首撤 {RetreatFirstTimeMean.ToString("F1", Inv)}s");
        sb.AppendLine($"  破阵：{FormationBreaksPerRun.ToString("F2", Inv)}次/局 首破 {FormationBreakFirstTimeMean.ToString("F1", Inv)}s");
        return sb.ToString();
    }
}
