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
//  观测口径：只读既有日志（`[DiagMilitary]` census top= ／ warrior= ／ `[DiagTriage]` 分诊 ／
//            `[KingdomBrain]` ㉗采集下发/无可采）——零业务代码改动、不掏私有字段（接口纪律）。
//  HH.244（D694 勘正①）：㉗ 判据由「评分 argmax」改「实际派发」——
//    实际派发=`ExecuteWorldGatherFocus` 走通通道B落地出口的唯一日志（`㉗采集下发：{rt} 新立案 N 个`）；
//    评分 top 只作支撑读数（L-35）。矩阵表头「被选中次数」→「评分 top 次数」＋新增「实际派发次数」列；报告标注跑次（D694 勘正②）。
//  落盘：`Logs/ChainAudit/chain_probe_matrix.txt`（幂等：无时间戳；ChainAuditMenu 只读并入）。
// ============================================================================
public static class ChainProbeFacade
{
    public const string Tag = "ChainAudit.Probe";
    public const string ProbeMenu = ChainAuditCore.MenuRoot + "/跑行为探针（正门进局）";

    // ── 探针跑档（本批专用；观测容器级配置，非游戏机制参数——D563③ 不适用）──
    private const int PROBE_SEED = 73621;          // 与 HH.230 七考同 seed（结构已侦察；本批只作观测窗口；HH.244 任务书指定）
    private const string PROBE_SLOT = "chain_probe2";  // HH.244 探针专用槽（v2 跑次；禁覆盖 p1_run*/p1_diag*/p1_fix*/chain_probe1）
    private const int PROBE_DAYS = 60;             // 观测窗口：≥ 各能力「可判定最早日」最大值（20）＋ 冗余
    private const int HARD_STOP_DAYS = 90;         // 熔断（防意外长跑）

    private static bool _running;
    private static readonly Dictionary<string, int> _topCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> _finalReadout = new Dictionary<string, int>(StringComparer.Ordinal);
    // HH.244 件1（D694 勘正①/任务书 a~d）：分资源「实际派发」观测（支撑=评分 top / DiagTriage 分诊）
    private static readonly Dictionary<string, int> _dispatchByRes = new Dictionary<string, int>(StringComparer.Ordinal);      // ㉗ 实际派发（石/木）
    private static readonly Dictionary<string, int> _envBlockByRes = new Dictionary<string, int>(StringComparer.Ordinal);        // ㉗ Env 阻断（领土内无可采）
    private static readonly Dictionary<string, int> _triageCounts = new Dictionary<string, int>(StringComparer.Ordinal);         // DiagTriage 分资源分诊计数（支撑面）
    private static int _lastDay = -1;
    private static int _watchCount;   // 观测行计数（跑次标注·采样面）
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

        _topCounts.Clear(); _finalReadout.Clear(); _dispatchByRes.Clear(); _envBlockByRes.Clear(); _triageCounts.Clear();
        _lastDay = -1; _watchCount = 0; _probeDiagSeen = false; _militaryStageSeen = false;
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

    /// <summary>全部「有观测通道」的能力均已命中 ≥1（无通道者不参与判定，防误判）。
    /// 判据（HH.244/L-35）：㉗＝实际派发≥1；其余＝评分 top≥1（HH.242 口径）。</summary>
    private static bool AllHittableObserved()
    {
        foreach (var c in ChainAuditSpec.Capabilities)
        {
            if (!Observable(c)) continue;
            int hits = c.Id == "㉗" ? DispatchHitsOf(c) : TopHitsOf(c);
            if (hits <= 0) return false;
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
    //  观测：只读既有日志（ChainProbeFacade 零业务改动——不掏私有字段·接口纪律）
    //   HH.244（D694 勘正①/L-35/L-21）：判据由「评分 argmax」改「实际派发」——
    //     · 实际派发＝`[KingdomBrain] kX ㉗采集下发：{rt} 新立案 N 个` 日志行
    //       （`ExecuteWorldGatherFocus` 走通通道B全部 guard、到达 Advertise 落地出口的**唯一日志** = 真实派发）；
    //     · 评分 top＝`[DiagMilitary] census top=`（**只作支撑读数，不作判据**）；
    //     · 分诊支撑＝`[DiagTriage]` 分资源 DecideTriage 结果（直接对应 DZ-148「石走通道A/木走通道B」论证）；
    //     · Env 阻断＝`[KingdomBrain] kX ㉗采集：本国领土内无可采 {rt}`（派发被世界给定物阻断，不计派发）。
    // ========================================================================
    private static readonly Regex ReCensusTop = new Regex(@"\[DiagMilitary\][^\n]*\btop=(\w+)", RegexOptions.Compiled);
    private static readonly Regex ReWarrior = new Regex(@"\[DiagMilitary\]\s+\S+\s+k(\d+)\s+stage=\S+\s+worker=(\d+)\s+warrior=(\d+)", RegexOptions.Compiled);
    private static readonly Regex ReMilitaryStage = new Regex(@"\[DiagMilitary\][^\n]*\bstage=Military\b", RegexOptions.Compiled);
    private static readonly Regex ReDispatchGather = new Regex(@"\[KingdomBrain\] k\d+ ㉗采集下发：(\w+) 新立案 (\d+) 个", RegexOptions.Compiled);
    private static readonly Regex ReGatherEnvBlock = new Regex(@"\[KingdomBrain\] k\d+ ㉗采集：本国领土内无可采 (\w+) 资源点", RegexOptions.Compiled);
    private static readonly Regex ReTriageDec = new Regex(@"\[DiagTriage\]\s+\S+\s+k\d+\s*(.*)$", RegexOptions.Compiled);
    private static readonly Regex ReTriagePair = new Regex(@"(?:\b(Stone|Wood)=(\w+))", RegexOptions.Compiled);

    private static void Watch(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(condition)) return;
        bool isDiag = condition.Contains("[DiagMilitary]");
        bool isTriage = condition.Contains("[DiagTriage]");
        bool isGatherLog = condition.Contains("[KingdomBrain]") && condition.Contains("㉗");
        if (!isDiag && !isTriage && !isGatherLog) return;
        _watchCount++;

        if (isDiag)
        {
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

        // ㉗ 实际派发（分资源·判据面）：`[KingdomBrain] ... ㉗采集下发：{rt} 新立案 {N} 个`
        var d = ReDispatchGather.Match(condition);
        if (d.Success)
        {
            string res = d.Groups[1].Value;
            int n; _dispatchByRes.TryGetValue(res, out n); _dispatchByRes[res] = n + 1;
        }
        // ㉗ Env 阻断（支撑面）：`㉗采集：本国领土内无可采 {rt}`
        var e = ReGatherEnvBlock.Match(condition);
        if (e.Success)
        {
            string res = e.Groups[1].Value;
            int n; _envBlockByRes.TryGetValue(res, out n); _envBlockByRes[res] = n + 1;
        }
        // 分诊支撑（支撑面）：`[DiagTriage] {tag} k{id} {r}=<Decision> ...` → 分资源分诊计数
        var t = ReTriageDec.Match(condition);
        if (t.Success)
        {
            foreach (Match p in ReTriagePair.Matches(t.Groups[1].Value))
            {
                string res = p.Groups[1].Value;   // Stone/Wood
                string dec = p.Groups[2].Value;   // 分诊值（BuildCapacity/NoOp/...）
                string key = res + "=" + dec;
                int n; _triageCounts.TryGetValue(key, out n); _triageCounts[key] = n + 1;
            }
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
        sb.AppendLine("# 跑次标注（HH.244·D694 勘正②）：seed=" + PROBE_SEED + " 槽=" + PROBE_SLOT + " 观测窗=D1~D"
                      + PROBE_DAYS + " 收工=" + why + " 观测行采样=" + _watchCount);
        sb.AppendLine("# 口径：只读既有日志（[DiagMilitary] census top=／[DiagTriage] 分诊／[KingdomBrain] ㉗采集下发）；零业务代码改动");
        sb.AppendLine("# 表头口径（HH.244/L-35）：「评分 top 次数」=评分 argmax（支撑读数）｜「实际派发次数」=㉗ 派发落地（判据面）");
        sb.AppendLine("# 矩阵为幂等基准（无时间戳）＋同目录时间戳副本（chain_probe_matrix_*.log·报告引用随跑次标注）；ChainAuditMenu 只读并入「观测角」");
        sb.AppendLine();
        sb.AppendLine("  " + Pad("能力", 26) + Pad("评分 top 次数", 14) + Pad("实际派发次数", 16) + Pad("观测读数", 22)
                          + Pad("可判定最早日", 14) + "verdict");
        sb.AppendLine("  " + Pad("服务哪条验收句 / 作用域 / 口径来源与排除项", 40));
        sb.AppendLine("  " + new string('-', 110));

        int red = 0, ok = 0, pending = 0, unobservable = 0, gated = 0;
        foreach (var c in ChainAuditSpec.Capabilities)
        {
            int topHits = TopHitsOf(c);                 // 评分 top 次数（支撑面）
            int dispatch = DispatchHitsOf(c);           // 实际派发次数（判据面·仅㉗）
            // HH.244/L-35：判据＝㉗ 走「实际派发」，其余能力维持「评分 top」（HH.242 既有口径，仅表头/读数措辞校正）
            int hits = c.Id == "㉗" ? dispatch : topHits;
            string dispatchStr = DispatchStrOf(c);      // 分资源串或 "—"
            string readout = ReadoutOf(c);
            string verdict;
            if (!Observable(c))
            {
                verdict = "⚪ 未观测（本批无观测通道·不判；须补只读打点⇒提策划端）";
                unobservable++;
            }
            else if (hits > 0)
            {
                verdict = c.Id == "㉗" ? "✅ 可达（实际派发 " + hits + " 次）" : "✅ 可达（评分 top " + hits + " 次）";
                ok++;
            }
            else if (c.Id == "㉗" && topHits > 0)
            {
                // HH.244/L-35/DDZ-148：㉗ 评分 top 命中但实际派发 0 ⇒ 判据面（实际派发）为准——证「入口结构性不可达」
                verdict = "🔴 入口不可达（评分 top " + topHits + " 次，但实际派发 0 次）";
                red++;
            }
            else if (c.RequiresMilitaryStage && !_militaryStageSeen)
            {
                // D669 同级＋同作用域：minStage=3 能力的前置门（军事期）本局未达
                //  ⇒ 未命中**不可归因**于"入口不可达"，只可判"前置门未达·本次不判"
                verdict = "⛔ 前置门未达（minStage=3 军事期本局未达 ⇒ 不可判，非入口不可达）";
                gated++;
            }
            else { verdict = "🔴 入口不可达（全程 0 次）"; red++; }
            if (Observable(c) && _lastDay >= 0 && _lastDay < c.EarliestDay) { pending++; verdict = "⏳ 未达可判定最早日（D" + c.EarliestDay + "）"; }

            sb.AppendLine("  " + Pad(c.Id + " " + c.Label, 26) + Pad(topHits.ToString(), 14) + Pad(dispatchStr, 16)
                              + Pad(readout, 22) + Pad("D" + c.EarliestDay, 14) + verdict);
            sb.AppendLine("    " + c.Judge + " ｜ 作用域=" + c.Scope);
            sb.AppendLine("    " + c.Caliber);
            sb.AppendLine("    命中即停=" + c.StopRule);
        }

        sb.AppendLine();
        sb.AppendLine("  ── 汇总 ──");
        sb.AppendLine("   ✅可达=" + ok + "  🔴入口不可达=" + red + "  ⛔前置门未达=" + gated + "  ⚪未观测=" + unobservable
                      + "  ⏳待定=" + pending + "  DiagMilitary 在场=" + (_probeDiagSeen ? "✓" : "✗")
                      + "  军事期到达=" + (_militaryStageSeen ? "✓" : "✗"));
        sb.AppendLine("   census top= 分布（评分面·支撑）：" + TopDistribution());
        sb.AppendLine("   ㉗ 实际派发（判据面·分资源）：" + DispatchSummary());
        sb.AppendLine("   ㉗ Env 阻断（支撑）：" + EnvBlockSummary());
        sb.AppendLine("   ㉗ 分诊支撑（DiagTriage·分资源）：" + TriageSummary());
        string matrix = sb.ToString();

        // HH.244 (d)/L-02：稳定名（幂等基准）＋时间戳副本（报告引用随跑次标注）
        ChainAuditCore.WriteReports(matrix, ChainAuditCore.ReportRoot, ChainAuditCore.ProbeMatrixName, "chain_probe_matrix_", Tag);
        foreach (var line in matrix.Split('\n'))
            if (line.TrimEnd('\r').Length > 0) Debug.Log("[" + Tag + "] " + line.TrimEnd('\r'));
    }

    /// <summary>评分 top 次数（支撑面·L-35：不作判据；㉗ 判据走实际派发）。
    /// 口径=census top= 计数；⑦ 需 warrior>0 才算真正落地（选中>0 但存量恒 0 ⇒ 空转，HH.242 既有口径）。</summary>
    private static int TopHitsOf(ChainAuditSpec.Capability c)
    {
        if (c.Kind != "Action") return 0;
        string actionName = ActionNameOf(c.Id);
        int n; _topCounts.TryGetValue(actionName, out n);
        if (c.Id == "⑦")
        {
            int peak = 0;
            foreach (var kv in _finalReadout)
                if (kv.Key.StartsWith("warrior.k", StringComparison.Ordinal) && kv.Value > peak) peak = kv.Value;
            return peak > 0 ? Mathf.Max(n, 1) : 0;
        }
        return n;
    }

    /// <summary>实际派发次数（判据面·HH.244）。仅 ㉗ 有观测通道（`[KingdomBrain] ㉗采集下发`）；其余 Action 无派发观测 ⇒ 0。</summary>
    private static int DispatchHitsOf(ChainAuditSpec.Capability c)
    {
        if (c.Id != "㉗") return 0;
        int total = 0;
        foreach (var kv in _dispatchByRes) total += kv.Value;
        return total;
    }

    /// <summary>㉗ 行「实际派发次数」列展示串（分资源）；其余能力 ⇒ "—"（无派发观测通道）。</summary>
    private static string DispatchStrOf(ChainAuditSpec.Capability c)
    {
        if (c.Id != "㉗") return "—";
        return DispatchSummary();
    }

    private static string DispatchSummary()
    {
        if (_dispatchByRes.Count == 0) return "0（全程无派发）";
        var parts = new List<string>();
        foreach (var kv in SortedDict(_dispatchByRes)) parts.Add(kv.Key + "=" + kv.Value);
        return string.Join(" ", parts.ToArray());
    }

    private static string EnvBlockSummary()
    {
        if (_envBlockByRes.Count == 0) return "0";
        var parts = new List<string>();
        foreach (var kv in SortedDict(_envBlockByRes)) parts.Add(kv.Key + "=" + kv.Value);
        return string.Join(" ", parts.ToArray());
    }

    /// <summary>DiagTriage 分资源分诊计数（支撑面）：如 Stone=BuildCapacity:5 Wood=NoOp:12。</summary>
    private static string TriageSummary()
    {
        if (_triageCounts.Count == 0) return "(无 DiagTriage 行)";
        var parts = new List<string>();
        foreach (var kv in SortedDict(_triageCounts)) parts.Add(kv.Key + ":" + kv.Value);
        return string.Join(" ", parts.ToArray());
    }

    private static IEnumerable<KeyValuePair<string, int>> SortedDict(Dictionary<string, int> d)
    {
        var keys = new List<string>(d.Keys);
        keys.Sort(StringComparer.Ordinal);
        foreach (var k0 in keys) yield return new KeyValuePair<string, int>(k0, d[k0]);
    }

    /// <summary>本批是否有观测通道（无通道 ⇒ 不判 🔴，置 ⚪未观测，防误报）。</summary>
    private static bool Observable(ChainAuditSpec.Capability c) => c.Kind == "Action";

    private static string ReadoutOf(ChainAuditSpec.Capability c)
    {
        if (c.Id == "⑦") return "warrior峰值=" + PeakWarrior();
        if (c.Id == "㉗")
        {
            int top = 0; _topCounts.TryGetValue("GatherWorldResource", out top);
            return "评分top=" + top + " 派发=" + DispatchHitsOf(c);
        }
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
