using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// ============================================================================
//  HH.240 断链校验台 · R6 文档↔代码漂移 ＋ 双源对拍（Editor-only 静态校验器）
//  规格真源：HH.240 任务书 T5 ＋ 断链校验台_设计稿 §三 R6
//
//  双源对拍：
//   源A（实现面）= 代码反射/SO/资产自动提取：
//      ① Resources/Buildings 全部 BuildingDef.id（建筑 def 全集）
//      ② Resources/Config/Kingdoms/UtilityActionConfig 全部 action（AI 行动全集）
//   源B（声明面）= 文档/声明清单（**只读**）：
//      ① 改造计划/生命周期登记表.md §1（建筑域 def 表）· §2（AI 行动池表）
//      ② ChainAuditSpec.Capabilities（能力声明面）
//  双向列报（设计稿 §三 R6）：
//     A有B无 = 代码漏登（新内容没登记）；B有A无 = 登记表漂移 / 实现缺失。
//  解析方式：Markdown 表格 ⇒ 正则提取名字集合；**只比对名字集合**（不比语义），保证鲁棒。
//
//  【L-15 双源纪律】校验器**只读**声明面，发现漂移只列报，**禁自行回写**登记表。
//  自证：`RunSelfTest` 用受控注入的小样本证明双向列报正确（A有B无 / B有A无 均能列）。
// ============================================================================
public static class R6_DocDrift
{
    public const string Tag = "ChainAudit.R6";

    private const string RegistryRel = "河谷防线开发计划书具体内容/改造计划/生命周期登记表.md";
    private const string MappingTableRel = "河谷防线开发计划书具体内容/改造计划/美术资源接入映射表.md";

    private static readonly ChainAuditCore.ExemptionTable Exempt
        = new ChainAuditCore.ExemptionTable("R6 漂移豁免", null, new Dictionary<string, string>());

    /// <summary>豁免表条目数（报告汇总用；变更须策划端确认 D561）。</summary>
    public static int ExemptCount => Exempt.Count;

    /// <summary>最近一次自证结果（由 AppendReport 更新；供聚合判定行）。</summary>
    public static bool SelfTestOk { get; private set; }

    public sealed class Result
    {
        public readonly List<ChainAuditCore.Violation> Violations = new List<ChainAuditCore.Violation>();
        public List<string> ABuilding = new List<string>();   // 源A 建筑 def
        public List<string> BBuilding = new List<string>();   // 源B 登记表 §1 def
        public List<string> AAction = new List<string>();     // 源A AI 行动
        public List<string> BAction = new List<string>();     // 源B 登记表 §2 行动
        public bool DocFound;
        public bool MappingTableFound;
        public string DocPath = RegistryRel;
        public string MappingTablePath = MappingTableRel;
    }

    // ========================================================================
    //  入口
    // ========================================================================
    public static Result Run(string dataPath = null)
    {
        if (string.IsNullOrEmpty(dataPath)) dataPath = Application.dataPath;
        var res = new Result();

        // ── 源A：实现面 ─────────────────────────────────────────────────────
        var defs = Resources.LoadAll<BuildingDef>("Buildings");
        if (defs != null)
            foreach (var d in defs) if (d != null && !string.IsNullOrEmpty(d.id)) res.ABuilding.Add(d.id);
        res.ABuilding.Sort(StringComparer.Ordinal);

        var acfg = Resources.Load<UtilityActionConfig>("Config/Kingdoms/UtilityActionConfig");
        if (acfg != null && acfg.actions != null)
            foreach (var a in acfg.actions) res.AAction.Add(a.id.ToString());
        res.AAction.Sort(StringComparer.Ordinal);

        // ── 源B：声明面（文档，只读）────────────────────────────────────────
        string repo = ChainAuditCore.RepoRoot(dataPath);
        string docAbs = Path.Combine(repo, RegistryRel.Replace('/', Path.DirectorySeparatorChar));
        res.DocFound = File.Exists(docAbs);
        if (res.DocFound)
        {
            string text;
            try { text = File.ReadAllText(docAbs); } catch { text = null; }
            if (text != null)
            {
                res.BBuilding = ParseRegistrySection(text, "## §1", "## §2");
                res.BAction = ParseRegistrySection(text, "## §2", "## §3");
            }
        }
        string mapAbs = Path.Combine(repo, MappingTableRel.Replace('/', Path.DirectorySeparatorChar));
        res.MappingTableFound = File.Exists(mapAbs);

        if (!res.DocFound)
        {
            res.Violations.Add(new ChainAuditCore.Violation("R6.声明面缺席", ChainAuditCore.Severity.Yellow,
                RegistryRel, "登记表不可达（源B 缺席）⇒ 双向对拍降级；请确认仓库根/路径"));
        }

        Diff(res, "建筑 def", res.ABuilding, res.BBuilding, RegistryRel + " §1");
        Diff(res, "AI 行动", res.AAction, res.BAction, RegistryRel + " §2");

        // ── 源B′：ChainAuditSpec 能力声明面（三源对拍的声明侧；本批只列报能力数与出处）──
        //  说明：Spec 能力 id（㉗/③/②…）与登记表行动名（Action 枚举名）为不同命名空间，
        //  不做字符串对拍（避免误报）；其一致性由 R4 三层绑定 + 探针矩阵独立覆盖。

        ChainAuditCore.SortViolations(res.Violations);
        return res;
    }

    private static void Diff(Result res, string label, List<string> a, List<string> b, string docAnchor)
    {
        var setA = new HashSet<string>(a, StringComparer.Ordinal);
        var setB = new HashSet<string>(b, StringComparer.Ordinal);

        // 记法归一二次判：命名**记法**漂移（magic_tower ↔ MagicTower）⇒ 🟡，非结构性 🔴。
        // 依据设计稿 §十「误报会淹没真信号」：只列报、分级不夸大。
        var normA = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in a) normA.Add(ChainAuditCore.NormKey(n));
        var normB = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in b) normB.Add(ChainAuditCore.NormKey(n));

        var aNotB = new List<string>();
        foreach (var n in a) if (!setB.Contains(n)) aNotB.Add(n);
        var bNotA = new List<string>();
        foreach (var n in b) if (!setA.Contains(n)) bNotA.Add(n);
        aNotB.Sort(StringComparer.Ordinal); bNotA.Sort(StringComparer.Ordinal);

        if (Exempt.Contains(label + ".A有B无")) aNotB.Clear();
        if (Exempt.Contains(label + ".B有A无")) bNotA.Clear();

        foreach (var n in aNotB)
        {
            bool notationOnly = normB.Contains(ChainAuditCore.NormKey(n));
            res.Violations.Add(new ChainAuditCore.Violation(
                "R6." + label + (notationOnly ? ".命名记法漂移" : ".A有B无"),
                ChainAuditCore.Severity.Yellow,
                docAnchor + " ← 实现面 " + n,
                notationOnly ? "仅记法差异（大小写/下划线；实现与声明同一实体）⇒ 提策划端统一记法即可"
                             : "代码漏登（新内容未登记）⇒ 提策划端补登，校验器不回写（L-15）"));
        }
        foreach (var n in bNotA)
        {
            bool notationOnly = normA.Contains(ChainAuditCore.NormKey(n));
            res.Violations.Add(new ChainAuditCore.Violation(
                "R6." + label + (notationOnly ? ".命名记法漂移" : ".B有A无"),
                notationOnly ? ChainAuditCore.Severity.Yellow : ChainAuditCore.Severity.Red,
                docAnchor + " ← 声明面 " + n,
                notationOnly ? "仅记法差异（大小写/下划线；实现与声明同一实体）⇒ 提策划端统一记法即可"
                             : "登记表漂移 或 实现缺失 ⇒ 提策划端核对（P1 幽灵引用家族）"));
        }
    }

    // ========================================================================
    //  登记表 §N 表格解析（只取 def 名集合；Markdown 表格第 1 列序号、第 2 列名）
    //  形如：`| 1 | farm | ✅… |`
    //        `| 22 | 训练将军（TrainGeneral） | … |`（D637 补登行＝中文名＋括注英文枚举名）
    //  ⇒ 名字提取取**并集**：纯英文名 与 括注英文名（中文名不参与对拍，避免漏报/误报）。
    // ========================================================================
    private static readonly Regex ReRow = new Regex(@"^\|\s*(\d+)\s*\|\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex ReRowParen = new Regex(@"^\|\s*\d+\s*\|[^|\r\n]*?[（(]\s*([A-Za-z_][A-Za-z0-9_]*)\s*[）)]", RegexOptions.Compiled | RegexOptions.Multiline);

    private static List<string> ParseRegistrySection(string text, string startHeader, string endHeader)
    {
        var names = new List<string>();
        int start = text.IndexOf(startHeader, StringComparison.Ordinal);
        if (start < 0) return names;
        int end = text.IndexOf(endHeader, start + startHeader.Length, StringComparison.Ordinal);
        string seg = end > start ? text.Substring(start, end - start) : text.Substring(start);

        // 明细任务表：第 2 列 ＝ def 名（纯英文名 / 中文名＋括注英文名）
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in ReRow.Matches(seg))
        {
            string name = m.Groups[2].Value;
            if (name.Length == 0 || IsNonDefWord(name)) continue;
            if (seen.Add(name)) names.Add(name);
        }
        foreach (Match m in ReRowParen.Matches(seg))
        {
            string name = m.Groups[1].Value;
            if (name.Length == 0 || IsNonDefWord(name)) continue;
            if (seen.Add(name)) names.Add(name);
        }
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>剔除解析噪声（表头/统计表里形如 `A5`/`HTML` 等非 def 词）。名单来源＝登记表实盘词表。</summary>
    private static readonly HashSet<string> NoiseWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "A", "B", "C", "D", "E", "F", "G",
    };

    private static bool IsNonDefWord(string w)
    {
        if (NoiseWords.Contains(w)) return true;
        // 全大写英文缩写（如 HTML/AI）排除；def 名实盘均含小写字母
        bool hasLower = false;
        for (int i = 0; i < w.Length; i++) if (char.IsLower(w[i])) { hasLower = true; break; }
        return !hasLower;
    }

    // ========================================================================
    //  自证：双向列报（A有B无 / B有A无）
    // ========================================================================
    public static bool RunSelfTest(out string detail)
    {
        var res = new Result();
        var a = new List<string> { "farm", "quarry", "NewDef" };      // 实现面
        var b = new List<string> { "farm", "quarry", "GhostDef" };    // 声明面
        Diff(res, "自证", a, b, "(注入)");
        bool hasAB = false, hasBA = false;
        foreach (var v in res.Violations)
        {
            if (v.Kind.EndsWith("A有B无", StringComparison.Ordinal) && v.Anchor.Contains("NewDef")) hasAB = true;
            if (v.Kind.EndsWith("B有A无", StringComparison.Ordinal) && v.Anchor.Contains("GhostDef")) hasBA = true;
        }
        detail = (hasAB && hasBA)
            ? "自证通过：NewDef 报 A有B无（代码漏登）、GhostDef 报 B有A无（登记表漂移）"
            : "自证失败：双向列报不完整";
        return hasAB && hasBA;
    }

    // ========================================================================
    //  报告块
    // ========================================================================
    public static void AppendReport(StringBuilder sb, Result res)
    {
        sb.AppendLine("===== R6 文档↔代码漂移 ＋双源对拍（静态）=====");
        sb.AppendLine("  源A（实现面）=Resources/Buildings BuildingDef.id ∪ UtilityActionConfig.actions");
        sb.AppendLine("  源B（声明面）=" + res.DocPath + "（§1 建筑 def / §2 AI 行动）"
                      + "；映射表=" + (res.MappingTableFound ? "在场" : "缺席"));
        sb.AppendLine("  对拍口径：只比对名字集合（不比语义）；双向列报 A有B无=代码漏登 · B有A无=登记表漂移");
        sb.AppendLine();
        sb.AppendLine("  ── 建筑 def 对拍 ──");
        sb.AppendLine("   源A 实盘=" + res.ABuilding.Count + "  源B 登记表=" + res.BBuilding.Count);
        sb.AppendLine("   A有B无(" + CountKind(res, "建筑 def.A有B无") + ") / B有A无(" + CountKind(res, "建筑 def.B有A无") + ")");
        sb.AppendLine("  ── AI 行动对拍 ──");
        sb.AppendLine("   源A 实盘=" + res.AAction.Count + "  源B 登记表=" + res.BAction.Count);
        sb.AppendLine("   A有B无(" + CountKind(res, "AI 行动.A有B无") + ") / B有A无(" + CountKind(res, "AI 行动.B有A无") + ")");
        sb.AppendLine();

        ChainAuditCore.AppendViolations(sb, "R6 违例", res.Violations);

        bool self; string d;
        self = RunSelfTest(out d);
        SelfTestOk = self;
        sb.AppendLine("  ── 自证（双向列报）──");
        sb.AppendLine("   " + (self ? "✅ " : "❌ ") + d);
        sb.AppendLine();
    }

    private static int CountKind(Result res, string suffix)
    {
        int n = 0;
        foreach (var v in res.Violations) if (v.Kind.EndsWith(suffix, StringComparison.Ordinal)) n++;
        return n;
    }
}
