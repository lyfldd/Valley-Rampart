using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.240 断链校验台 · 框架层（ChainAuditCore · Editor-only · 零运行时开销）
//  规格真源：多Agent交接/策划端/HH.240_断链校验台实施批_任务书.md §三 T1/T9
//           ＋ 河谷防线开发计划书具体内容/改造计划/断链校验台_设计稿.md §2.1/§五/§六
//
//  抽取四件地基（源自 LifecycleAudit HH.132 已验证设施 · 设计稿 §2.1）：
//   ① 违规模型   Violation{Kind, Severity, Anchor, Advice}
//   ② 豁免表机制 ExemptionTable（D561 硬红线：变更须策划端确认；本类不回写任何登记表）
//   ③ 幂等报告   稳定名（无时间戳＝幂等基准）＋ 时间戳副本
//   ④ 扫描域纪律 反射只扫 Assembly-CSharp · 源码只扫 Assets/_Game（Editor 程序集不入扫描，防自噪）
//  附加（T9）：分级门禁 🔴硬拦／🟡列报／豁免不计 ＋ 静态×行为 三角闭合判定行
//
//  接口纪律：本类**只读**声明面（登记表/映射表/资产），发现新违例只列报，**禁自行回写**（防双源 L-15）。
// ============================================================================
public static class ChainAuditCore
{
    // ── 扫描域纪律（④）────────────────────────────────────────────────────
    public const string ProductionAssemblyName = "Assembly-CSharp";
    public const string ProductionSourceFolder = "_Game";

    // ── 报告落点（③）──────────────────────────────────────────────────────
    public const string ReportRoot = "Logs/ChainAudit";
    public const string StableReportName = "chain_audit_report.txt";   // 幂等基准（无时间戳）

    // ── 菜单根（T3 聚合入口）───────────────────────────────────────────────
    public const string MenuRoot = "Valley/审计/ChainAudit";

    // ── 探针矩阵产物（行为层·观测角；由 ChainProbeFacade 落盘，静态层只读并入）──
    public const string ProbeMatrixName = "chain_probe_matrix.txt";

    // ========================================================================
    //  ① 违规模型
    // ========================================================================
    public enum Severity { Red = 0, Yellow = 1 }

    public struct Violation
    {
        public string Kind;          // 违例类（R3/R4/R5/R6 子类，如 "R3.零供给"）
        public Severity Level;       // 🔴 硬拦 / 🟡 列报
        public string Anchor;        // 证据锚点 file:line / 资产路径 / 文档行
        public string Advice;        // 处置建议（不自动执行）

        public Violation(string kind, Severity level, string anchor, string advice)
        {
            Kind = kind; Level = level; Anchor = anchor; Advice = advice;
        }

        public string LevelMark => Level == Severity.Red ? "🔴" : "🟡";

        public override string ToString()
            => LevelMark + " [" + Kind + "] 锚点=" + (string.IsNullOrEmpty(Anchor) ? "(无)" : Anchor)
               + " 建议=" + Advice;
    }

    /// <summary>稳定排序（幂等）：按 级别→类→锚点→建议 全序。</summary>
    public static void SortViolations(List<Violation> list)
    {
        if (list == null) return;
        list.Sort((a, b) =>
        {
            int c = a.Level.CompareTo(b.Level);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Kind, b.Kind);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.Anchor, b.Anchor);
            if (c != 0) return c;
            return string.CompareOrdinal(a.Advice, b.Advice);
        });
    }

    public static int CountLevel(List<Violation> list, Severity level)
    {
        if (list == null) return 0;
        int n = 0;
        for (int i = 0; i < list.Count; i++) if (list[i].Level == level) n++;
        return n;
    }

    /// <summary>输出违例块（明细为主，L-11）。</summary>
    public static void AppendViolations(StringBuilder sb, string title, List<Violation> list)
    {
        sb.AppendLine("-- " + title + " = " + (list == null ? 0 : list.Count) + " 条 --");
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
            sb.AppendLine("   " + list[i]);
    }

    // ========================================================================
    //  ② 豁免表机制（设计内例外 · 代码内静态清单）
    //  【D561 硬红线】豁免表变更须策划端确认——本类只是载体，不改任何清单。
    // ========================================================================
    public sealed class ExemptionTable
    {
        private readonly HashSet<string> _keys;
        private readonly Dictionary<string, string> _reasons;
        public string Name { get; }
        public string Discipline { get; }

        public ExemptionTable(string name, IEnumerable<string> keys,
            IDictionary<string, string> reasons = null, string discipline = "D561：变更须策划端确认")
        {
            Name = name;
            Discipline = discipline;
            _keys = new HashSet<string>(StringComparer.Ordinal);
            _reasons = new Dictionary<string, string>(StringComparer.Ordinal);
            if (keys != null) foreach (var k in keys) if (k != null) _keys.Add(k);
            if (reasons != null) foreach (var kv in reasons) { _keys.Add(kv.Key); _reasons[kv.Key] = kv.Value; }
        }

        public int Count => _keys.Count;
        public bool Contains(string key) => !string.IsNullOrEmpty(key) && _keys.Contains(key);
        public string ReasonOf(string key)
        {
            string r;
            return (key != null && _reasons.TryGetValue(key, out r)) ? r : "";
        }

        /// <summary>枚举豁免键（稳定序）。</summary>
        public List<string> KeysSorted()
        {
            var keys = new List<string>(_keys);
            keys.Sort(StringComparer.Ordinal);
            return keys;
        }
    }

    // ========================================================================
    //  ④ 扫描域纪律
    // ========================================================================
    /// <summary>反射域＝生产程序集（Assembly-CSharp）。名不符 → 响亮告警（Editor 程序集不入扫描，防自噪）。</summary>
    public static System.Reflection.Assembly ProductionAssembly(string logTag)
    {
        var asm = typeof(EventBus).Assembly;
        if (asm.GetName().Name != ProductionAssemblyName)
            Debug.LogWarning("[" + logTag + "] 反射域=(EventBus).Assembly 名称=" + asm.GetName().Name
                             + "（预期 " + ProductionAssemblyName + "，HH.132 排雷 R2）。");
        return asm;
    }

    /// <summary>源码扫描根＝Assets/_Game（生产代码面；Editor 冒烟订阅不计入运行时接线）。</summary>
    public static string ProductionSourceRoot(string dataPath) => Path.Combine(dataPath, ProductionSourceFolder);

    /// <summary>仓库根：从 Assets 向上找到含「河谷防线开发计划书具体内容」或「多Agent交接」的目录（文档声明面定位）。</summary>
    public static string RepoRoot(string dataPath = null)
    {
        string start = string.IsNullOrEmpty(dataPath) ? Application.dataPath : dataPath;
        var dir = new DirectoryInfo(start);
        while (dir != null)
        {
            try
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "河谷防线开发计划书具体内容"))
                    || Directory.Exists(Path.Combine(dir.FullName, "多Agent交接")))
                    return dir.FullName;
            }
            catch { }
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    public static string RelPath(string abs)
    {
        string norm = abs.Replace('\\', '/');
        int i = norm.IndexOf("/Assets/", StringComparison.Ordinal);
        return i >= 0 ? norm.Substring(i + 1) : norm;
    }

    public static int LineOf(string text, int idx)
    {
        int line = 1;
        for (int i = 0; i < idx && i < text.Length; i++) if (text[i] == '\n') line++;
        return line;
    }

    /// <summary>
    /// 名字**记法归一**（只用于判"同一实体的不同写法"，不用于判等值）：
    /// 小写化 ＋ 去 `_`/`-` ⇒ `magic_tower` 与 `MagicTower` 同键。
    /// 依据：登记表 R6 对拍中出现 `magic_tower`(文档) vs `MagicTower`(资产 id) 的记法漂移，
    /// 属"同实体不同写法"⇒ 应降级 🟡 列报，不得拔高为结构性 🔴（设计稿 §十：误报淹没真信号）。
    /// </summary>
    public static string NormKey(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '_' || c == '-') continue;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    // ========================================================================
    //  ③ 幂等报告（稳定名 ＋ 时间戳副本）
    // ========================================================================
    public static void LogReport(string report, string logTag)
    {
        if (string.IsNullOrEmpty(report)) return;
        foreach (var line in report.Split('\n'))
            if (line.TrimEnd('\r').Length > 0) Debug.Log("[" + logTag + "] " + line.TrimEnd('\r'));
    }

    /// <summary>写报告：稳定名（无时间戳＝幂等基准）＋ 时间戳副本（随交付信台账快照口径 L-02）。</summary>
    public static void WriteReports(string report, string logDir, string stableReportName,
        string stampedPrefix, string logTag)
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), logDir);
        Directory.CreateDirectory(dir);

        string stablePath = Path.Combine(dir, stableReportName);
        File.WriteAllText(stablePath, report, new UTF8Encoding(false));

        string stamped = stampedPrefix + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
        File.WriteAllText(Path.Combine(dir, stamped), report, new UTF8Encoding(false));

        Debug.Log("[" + logTag + "] 落盘：" + stablePath + " + " + stamped);
    }

    /// <summary>校验台标准落盘（Logs/ChainAudit/chain_audit_report.txt ＋时间戳副本）。</summary>
    public static void WriteChainReport(string report, string logTag)
        => WriteReports(report, ReportRoot, StableReportName, "chain_audit_", logTag);

    // ========================================================================
    //  T9 分级门禁 ＋ 合并判定（静态报告 × 探针矩阵 = 三角闭合）
    // ========================================================================
    /// <summary>判定行：🔴 存在 ⇒ "有违例"（硬拦：交付/验收不通过）；否则按 🟡 列报；豁免不计入违例。</summary>
    public static string JudgeLine(int red, int yellow, int exempt)
    {
        string head = red > 0 ? "有违例" : "零违例";
        string tail = red > 0
            ? "；🔴 硬拦：报告判定=有违例 ⇒ 交付/验收不通过"
            : "；🔴 硬拦项=0 ⇒ 不触发硬拦（🟡 仅列报）";
        return "判定：" + head + "（🔴=" + red + " 🟡=" + yellow + " 豁免=" + exempt + tail + "）";
    }

    /// <summary>
    /// 三角闭合判定：声明↔实现（静态层）＋观测（行为层）三角缺角汇总。
    /// probeMatrixPresent=false ⇒ 观测角缺席，判定降级为"静态单层（观测角缺席）"。
    /// </summary>
    public static string TriangleVerdict(int staticRed, int staticYellow, int probeRed, bool probeMatrixPresent)
    {
        if (!probeMatrixPresent)
            return "三角闭合：⚠ 观测角缺席（探针矩阵未产出）⇒ 静态单层判定（可达性不可证）";
        int red = staticRed + probeRed;
        string head = red > 0 ? "有违例" : "零违例";
        return "三角闭合：" + head + "（静态🔴=" + staticRed + " 行为🔴=" + probeRed
               + " ⇒ 三角缺角" + (red > 0 ? "存在" : "未发现") + "）";
    }
}
