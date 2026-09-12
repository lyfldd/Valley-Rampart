using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.240 断链校验台 · 行为可达层（ChainProbeFacade · Editor-only）
//  规格真源：HH.240 任务书 T8 ＋ 断链校验台_设计稿 §四
//
//  职责：每个"声明能力"（ChainAuditSpec）配一探针，跑局观测**是否被触发 ≥1 次**，
//        产出「能力观测矩阵」（补三角的"观测"角）。
//
//  入口纪律（`test-harness-first` 铁律1）：**走正门** `TestHarnessApi.EnterTestRun`（禁裸跑）。
//  收尾纪律（`L-32`）：真暂停(Time.timeScale=0) → Save → ExitTestRun → **退 Play**（禁留 1x 余留世界）。
//  四列必填（§8.6 硬条款）：开跑前校 `ChainAuditSpec.CheckFourColumns()`，**缺列不得开跑**。
//  观测口径：只读 `DiagMilitary` 已打点日志（`census … top=` ／ `warrior=`）——
//            零业务代码改动、不掏私有字段（接口纪律）。
//  落盘：`Logs/ChainAudit/chain_probe_matrix.txt`（幂等：无时间戳；ChainAuditMenu 只读并入）。
// ============================================================================
public static class ChainProbeFacade
{
    public const string Tag = "ChainAudit.Probe";
    public const string ProbeMenu = ChainAuditCore.MenuRoot + "/跑行为探针（正门进局）";

    // ── 探针跑档（本批专用；观测容器级配置，非游戏机制参数——D563③ 不适用）──
    private const int PROBE_SEED = 73621;          // 与 HH.230 七考同 seed（结构已侦察；本批只作观测窗口）
    private const string PROBE_SLOT = "chain_probe1";  // 探针专用槽（禁覆盖 p1_run6/6b/7/8/9/p1_diag*/p1_fix*）
    private const int PROBE_DAYS = 60;             // 观测窗口：≥ 各能力「可判定最早日」最大值（20）＋ 冗余
    private const int HARD_STOP_DAYS = 90;         // 熔断（防意外长跑）

    private static bool _running;
    private static readonly Dictionary<string, int> _topCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> _finalReadout = new Dictionary<string, int>(StringComparer.Ordinal);
    private static int _lastDay = -1;
    private static bool _probeDiagSeen;
    private static bool _militaryStageSeen;   // 前置阶段观测：本次跑局是否曾进入军事期（minStage=3 能力的前置门）

    /// <summary>矩阵产物（静态层只读并入用）。</summary>
    public sealed class MatrixArtifact
    {
        public bool HasMatrix;
        public string Path;
        public int RedCount;
        public readonly List<string> Lines = new List<string>();
    }

    /// <summary>只认汇总行的「🔴入口不可达=N」（防把各能力「命中即停」正文里的字样误计为违例）。</summary>
    private static readonly Regex ReRedSummary = new Regex(@"🔴入口不可达=(\d+)", RegexOptions.Compiled);

    // ========================================================================
    //  读取矩阵产物（ChainAuditMenu 并入「观测角」）
    // ========================================================================
    public static MatrixArtifact ReadMatrixArtifact()
    {
        var a = new MatrixArtifact();
        string p = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), ChainAuditCore.ReportRoot),
            ChainAuditCore.ProbeMatrixName);
        a.Path = ChainAuditCore.ReportRoot + "/" + ChainAuditCore.ProbeMatrixName;
        if (!File.Exists(p)) return a;
        try
        {
            a.HasMatrix = true;
            foreach (var line in File.ReadAllLines(p))
            {
                a.Lines.Add(line);
                var m = ReRedSummary.Match(line);
                if (m.Success) a.RedCount = int.Parse(m.Groups[1].Value);
            }
        }
        catch { a.HasMatrix = false; }
        return a;
    }

    // ========================================================================
    //  入口（正门进局）
    // ========================================================================
    [MenuItem(ProbeMenu, priority = 110)]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[" + Tag + "] 须先 GameScene 进 Play（正门 EnterTestRun 在 Play 内协程）。已中止。");
            return;
        }
        if (_running) { Debug.LogWarning("[" + Tag + "] 探针已在跑（幂等守卫）。"); return; }

        // 四列必填硬条款（§8.6）：缺列不得开跑
        var missing = ChainAuditSpec.CheckFourColumns();
        if (missing.Count > 0)
        {
            Debug.LogError("[" + Tag + "] ✗ 声明能力缺判据列（" + missing.Count + " 项）⇒ **不得开跑**（test-harness-first §8.6）：\n  "
                           + string.Join("\n  ", missing.ToArray()));
            return;
        }
        // DiagMilitary 探针须在场（否则 census 证据全空）
        if (!DiagMilitary.IsRunning)
        {
            Debug.LogError("[" + Tag + "] ✗ DiagMilitary 探针未启用（census/top 证据为空）——请先点「Valley/诊断/启动建军链诊断」。已中止。");
            return;
        }

        _topCounts.Clear(); _finalReadout.Clear(); _lastDay = -1; _probeDiagSeen = false; _militaryStageSeen = false;
        _running = true;
        Application.logMessageReceived += Watch;
        new GameObject("ChainProbeHost").AddComponent<Host>().Go(RunCoroutine());
    }

    private class Host : MonoBehaviour
    {
        public void Go(IEnumerator r) => StartCoroutine(r);
    }

    private static IEnumerator RunCoroutine()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = PROBE_SEED, mapSeed = PROBE_SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = PROBE_SLOT, kingdomName = "河谷王国"
        };
        Debug.LogWarning("[" + Tag + "] 正门进局 seed=" + PROBE_SEED + " 槽=" + PROBE_SLOT + "（观测窗口 D" + PROBE_DAYS + "）");
        yield return TestHarnessApi.EnterTestRun(cfg);

        while (true)
        {
            yield return new WaitForSeconds(5f);
            int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            _lastDay = day;

            // ── 命中即停（四列①·§8.6）：全部**已达可判定最早日**的能力均已命中 ≥1 次
            //    ⇒ 可达性已证毕，无需继续（省跑局）；有 0 命中者则须跑满窗口才可判"不可达"。
            if (day >= MaxEarliestDay() && AllHittableObserved())
            { Finish("全能力命中即停 @D" + day); yield break; }
            if (day >= PROBE_DAYS) { Finish("观测窗口到期 @D" + day); yield break; }
            if (day >= HARD_STOP_DAYS) { Finish("熔断 @D" + day); yield break; }
        }
    }

    private static int MaxEarliestDay()
    {
        int m = 0;
        foreach (var c in ChainAuditSpec.Capabilities) if (c.EarliestDay > m) m = c.EarliestDay;
        return m;
    }

    /// <summary>全部「有观测通道」的能力均已命中 ≥1（无通道者不参与判定，防误判）。</summary>
    private static bool AllHittableObserved()
    {
        foreach (var c in ChainAuditSpec.Capabilities)
        {
            if (!Observable(c)) continue;
            if (CountHits(c) <= 0) return false;
        }
        return true;
    }

    private static void Finish(string why)
    {
        Application.logMessageReceived -= Watch;
        _running = false;

        // L-32 条文3：禁以 SetGameSpeed(0f) 当暂停；真暂停用 timeScale
        Time.timeScale = 0f;
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(PROBE_SLOT);
        TestHarnessApi.ExitTestRun();

        WriteMatrix(why);
        Debug.LogWarning("[" + Tag + "] ★ " + why + "——真暂停(TS=0)+封盘=" + saved + "；收尾退 Play（L-32 条文1）");
        // L-32 条文1：收工禁留「已恢复 1x」余留世界
        EditorApplication.ExitPlaymode();
    }

    // ========================================================================
    //  观测：只读 DiagMilitary 已打点日志
    // ========================================================================
    private static readonly Regex ReCensusTop = new Regex(@"\[DiagMilitary\][^\n]*\btop=(\w+)", RegexOptions.Compiled);
    private static readonly Regex ReWarrior = new Regex(@"\[DiagMilitary\]\s+\S+\s+k(\d+)\s+stage=\S+\s+worker=(\d+)\s+warrior=(\d+)", RegexOptions.Compiled);
    private static readonly Regex ReMilitaryStage = new Regex(@"\[DiagMilitary\][^\n]*\bstage=Military\b", RegexOptions.Compiled);

    private static void Watch(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(condition)) return;
        if (!condition.Contains("[DiagMilitary]")) return;
        _probeDiagSeen = true;
        if (ReMilitaryStage.IsMatch(condition)) _militaryStageSeen = true;   // 军事期前置门（⑯/㉕ 判据可比性）

        var m = ReCensusTop.Match(condition);
        if (m.Success)
        {
            string top = m.Groups[1].Value;
            int n; _topCounts.TryGetValue(top, out n); _topCounts[top] = n + 1;
        }
        var w = ReWarrior.Match(condition);
        if (w.Success)
        {
            int kid = int.Parse(w.Groups[1].Value);
            int war = int.Parse(w.Groups[3].Value);
            int prev; _finalReadout.TryGetValue("warrior.k" + kid, out prev);
            if (war > prev) _finalReadout["warrior.k" + kid] = war;   // 取峰值（存量读数）
        }
    }

    // ========================================================================
    //  能力观测矩阵（四列表头 ＋ verdict）
    // ========================================================================
    private static void WriteMatrix(string why)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# ChainProbeFacade 能力观测矩阵（HH.240 T8；行为层·观测角）");
        sb.AppendLine("# 入口=TestHarnessApi.EnterTestRun（正门·test-harness-first 铁律1）；收尾=真暂停+退 Play（L-32）");
        sb.AppendLine("# seed=" + PROBE_SEED + " 槽=" + PROBE_SLOT + " 收工=" + why);
        sb.AppendLine("# 口径：只读 DiagMilitary 已打点日志（census top= ／ warrior=）；零业务代码改动");
        sb.AppendLine("# 矩阵为幂等基准（不含时间戳）；ChainAuditMenu 只读并入「观测角」");
        sb.AppendLine();
        sb.AppendLine("  " + Pad("能力", 26) + Pad("被选中次数", 12) + Pad("观测读数", 22)
                          + Pad("可判定最早日", 14) + "verdict");
        sb.AppendLine("  " + Pad("服务哪条验收句 / 作用域 / 口径来源与排除项", 40));
        sb.AppendLine("  " + new string('-', 100));

        int red = 0, ok = 0, pending = 0, unobservable = 0, gated = 0;
        foreach (var c in ChainAuditSpec.Capabilities)
        {
            int hits = CountHits(c);
            string readout = ReadoutOf(c);
            string verdict;
            if (!Observable(c))
            {
                verdict = "⚪ 未观测（本批无观测通道·不判；须补只读打点⇒提策划端）";
                unobservable++;
            }
            else if (hits > 0) { verdict = "✅ 可达（被选中/产出 " + hits + " 次）"; ok++; }
            else if (c.RequiresMilitaryStage && !_militaryStageSeen)
            {
                // D669 同级＋同作用域：minStage=3 能力的前置门（军事期）本局未达
                //  ⇒ 未命中**不可归因**于"入口不可达"，只可判"前置门未达·本次不判"
                verdict = "⛔ 前置门未达（minStage=3 军事期本局未达 ⇒ 不可判，非入口不可达）";
                gated++;
            }
            else { verdict = "🔴 入口不可达（全程 0 次）"; red++; }
            if (Observable(c) && _lastDay >= 0 && _lastDay < c.EarliestDay) { pending++; verdict = "⏳ 未达可判定最早日（D" + c.EarliestDay + "）"; }

            sb.AppendLine("  " + Pad(c.Id + " " + c.Label, 26) + Pad(hits.ToString(), 12) + Pad(readout, 22)
                              + Pad("D" + c.EarliestDay, 14) + verdict);
            sb.AppendLine("    " + c.Judge + " ｜ 作用域=" + c.Scope);
            sb.AppendLine("    " + c.Caliber);
            sb.AppendLine("    命中即停=" + c.StopRule);
        }

        sb.AppendLine();
        sb.AppendLine("  ── 汇总 ──");
        sb.AppendLine("   ✅可达=" + ok + "  🔴入口不可达=" + red + "  ⛔前置门未达=" + gated + "  ⚪未观测=" + unobservable
                      + "  ⏳待定=" + pending + "  DiagMilitary 在场=" + (_probeDiagSeen ? "✓" : "✗")
                      + "  军事期到达=" + (_militaryStageSeen ? "✓" : "✗"));
        sb.AppendLine("   census top= 分布：" + TopDistribution());
        string matrix = sb.ToString();

        string dir = Path.Combine(Directory.GetCurrentDirectory(), ChainAuditCore.ReportRoot);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, ChainAuditCore.ProbeMatrixName), matrix, new UTF8Encoding(false));
        foreach (var line in matrix.Split('\n'))
            if (line.TrimEnd('\r').Length > 0) Debug.Log("[" + Tag + "] " + line.TrimEnd('\r'));
        Debug.Log("[" + Tag + "] 落盘：" + ChainAuditCore.ReportRoot + "/" + ChainAuditCore.ProbeMatrixName);
    }

    /// <summary>能力被选中/产出次数（口径＝census top= 计数；产出类走存量读数）。</summary>
    private static int CountHits(ChainAuditSpec.Capability c)
    {
        if (c.Kind == "Action")
        {
            string actionName = ActionNameOf(c.Id);
            int n; _topCounts.TryGetValue(actionName, out n);
            // ⑦ 需 warrior>0 才算真正落地（选中>0 但存量恒 0 ⇒ 空转）
            if (c.Id == "⑦")
            {
                int peak = 0;
                foreach (var kv in _finalReadout)
                    if (kv.Key.StartsWith("warrior.k", StringComparison.Ordinal) && kv.Value > peak) peak = kv.Value;
                return peak > 0 ? Mathf.Max(n, 1) : 0;
            }
            return n;
        }
        // Production 类：本批**无观测通道**（DiagMilitary 不察弹药子仓；不掏私有字段，守零业务改动）
        //  ⇒ 不判"不可达"（防误报），矩阵置 ⚪未观测，须后续补 SiemgeWorkshop 只读打点（提策划端）
        return 0;
    }

    /// <summary>本批是否有观测通道（无通道 ⇒ 不判 🔴，置 ⚪未观测，防误报）。</summary>
    private static bool Observable(ChainAuditSpec.Capability c) => c.Kind == "Action";

    private static string ReadoutOf(ChainAuditSpec.Capability c)
    {
        if (c.Id == "⑦") return "warrior峰值=" + PeakWarrior();
        return "(census)";
    }

    private static int PeakWarrior()
    {
        int peak = 0;
        foreach (var kv in _finalReadout)
            if (kv.Key.StartsWith("warrior.k", StringComparison.Ordinal) && kv.Value > peak) peak = kv.Value;
        return peak;
    }

    private static string ActionNameOf(string capId)
    {
        switch (capId)
        {
            case "㉗": return "GatherWorldResource";
            case "③": return "BuildCapacity";
            case "②": return "BuildWarehouse";
            case "⑦": return "RecruitWarrior";
            case "⑰a": return "BuildBarracks";
            case "⑯": return "TrainGeneral";
            case "㉕": return "ProduceMachine";
            case "⑨": return "BuildWall";
            case "⑤": return "Grain";
            case "⑥": return "RecruitWorker";
            default: return "";
        }
    }

    private static string TopDistribution()
    {
        if (_topCounts.Count == 0) return "(无 census 行)";
        var keys = new List<string>(_topCounts.Keys);
        keys.Sort(StringComparer.Ordinal);
        var parts = new List<string>();
        foreach (var k in keys) parts.Add(k + "=" + _topCounts[k]);
        return string.Join(" ", parts.ToArray());
    }

    private static string Pad(string s, int w) => (s ?? "").PadRight(w);
}
