using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// ============================================================================
//  HH.240 断链校验台 · R4 AI 行动声明可达（Editor-only 静态校验器）
//  规格真源：HH.240 任务书 T6 ＋ 断链校验台_设计稿 §三 R4
//
//  静态三层绑定（每条 action · UtilityActionConfig.asset 实读）：
//   ① NeedKind 绑定      —— asset `need` 值须为已定义 NeedKind（UtilityScorer.cs:55 枚举）
//   ② UtilityScorer 评分 —— NeedScore 须有 `case NeedKind.X:` 分支（UtilityScorer.NeedScore L206 起）
//   ③ KingdomBrain 执行 —— ExecuteFocus 须有 `case UtilityAction.Y:` 分支（KingdomBrain.cs L584 起）
//  缺一 ⇒ 幽灵行动 🔴。
//
//  静态"必要不充分条件"（设计稿 §三 R4 第二段）：
//    建造类行动 `Feasible` 含国库门槛（costGold/costStone/costWood/costFood）；
//    DZ-135 实证：③建产能 `costGold:50` vs AI 早期国库 3~8 ⇒ **当前值域内恒不可行**。
//    本器以 `EarlyTreasuryGoldUpperBound`（口径源＝设计稿 §三 R4 所引 DZ-135 证据值域 3~8）
//    作为**必要不充分**信号：costGold > 上界 ⇒ 🟡 列报（**非硬拦**——国库随时间增长，
//    静态证不了"永不可行"；真判据属行为层 ChainProbeFacade）。
//  🔴 静态证不了：「入口永不可达」（DZ-148 ㉗ 定义全齐、逻辑自洽、跑局零触发）⇒ 交 ChainProbeFacade。
//
//  自证：`RunSelfTest` 注入「有 action 声明、缺评分/执行分支」⇒ 须报幽灵行动 🔴（DZ-043 类）。
// ============================================================================
public static class R4_ActionReach
{
    public const string Tag = "ChainAudit.R4";

    private const string ScorerRel = "Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs";
    private const string BrainRel = "Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs";

    // 设计稿 §三 R4 所引 DZ-135 证据值域（AI 早期国库 3~8）——必要不充分上界。
    public const int EarlyTreasuryGoldUpperBound = 8;

    private static readonly ChainAuditCore.ExemptionTable Exempt
        = new ChainAuditCore.ExemptionTable("R4 行动可达豁免", null, new Dictionary<string, string>());

    /// <summary>豁免表条目数（报告汇总用；变更须策划端确认 D561）。</summary>
    public static int ExemptCount => Exempt.Count;

    /// <summary>最近一次自证结果（由 AppendReport 更新；供聚合判定行）。</summary>
    public static bool SelfTestOk { get; private set; }

    public sealed class Row
    {
        public UtilityActionDef Def;
        public int NeedValue;
        public bool NeedKindValid;
        public bool ScoringBranch;
        public bool ExecBranch;
        public bool Ghost => !(NeedKindValid && ScoringBranch && ExecBranch);
    }

    public sealed class Result
    {
        public readonly List<ChainAuditCore.Violation> Violations = new List<ChainAuditCore.Violation>();
        public readonly List<Row> Rows = new List<Row>();
        public int ActionTotal;
        public bool ScorerFound;
        public bool BrainFound;
    }

    // ========================================================================
    //  入口
    // ========================================================================
    public static Result Run(string dataPath = null)
    {
        if (string.IsNullOrEmpty(dataPath)) dataPath = Application.dataPath;
        var res = new Result();

        var cfg = Resources.Load<UtilityActionConfig>("Config/Kingdoms/UtilityActionConfig");
        if (cfg == null || cfg.actions == null)
        {
            res.Violations.Add(new ChainAuditCore.Violation("R4.行动声明面缺席", ChainAuditCore.Severity.Yellow,
                "Resources/Config/Kingdoms/UtilityActionConfig.asset", "行动配置不可达 ⇒ R4 降级"));
            return res;
        }

        string scorerPath = Path.Combine(Directory.GetCurrentDirectory(), ScorerRel.Replace('/', Path.DirectorySeparatorChar));
        string brainPath = Path.Combine(Directory.GetCurrentDirectory(), BrainRel.Replace('/', Path.DirectorySeparatorChar));
        string scorer = SafeRead(scorerPath); string brain = SafeRead(brainPath);
        res.ScorerFound = scorer != null; res.BrainFound = brain != null;

        var scoringBranches = ExtractCases(scorer, "NeedKind");
        var execBranches = ExtractCases(brain, "UtilityAction");
        var needEnumNames = Enum.GetNames(typeof(NeedKind));

        res.ActionTotal = cfg.actions.Length;
        foreach (var d in cfg.actions)
        {
            if (d.id == UtilityAction.None) continue;
            var row = new Row { Def = d, NeedValue = (int)d.need };
            row.NeedKindValid = Enum.IsDefined(typeof(NeedKind), d.need);
            row.ScoringBranch = row.NeedKindValid && scoringBranches.Contains(needEnumNames[row.NeedValue]);
            row.ExecBranch = execBranches.Contains(d.id.ToString());
            res.Rows.Add(row);
        }

        res.Violations.AddRange(Judge(res.Rows));
        return res;
    }

    /// <summary>判据内核（Run 与 RunSelfTest 共用；保证自证与实跑同源）。</summary>
    private static List<ChainAuditCore.Violation> Judge(List<Row> rows)
    {
        var list = new List<ChainAuditCore.Violation>();
        foreach (var row in rows)
        {
            var d = row.Def;
            if (Exempt.Contains(d.id.ToString())) continue;
            if (row.Ghost)
            {
                var miss = new List<string>();
                if (!row.NeedKindValid) miss.Add("①NeedKind 未定义(" + (int)d.need + ")");
                if (!row.ScoringBranch) miss.Add("②UtilityScorer 评分分支缺");
                if (!row.ExecBranch) miss.Add("③KingdomBrain.ExecuteFocus 执行分支缺");
                list.Add(new ChainAuditCore.Violation("R4.幽灵行动", ChainAuditCore.Severity.Red,
                    "action#" + (int)d.id + "(" + d.name + ") 组" + d.id + " " + string.Join("/", miss),
                    "补齐缺失层（need 枚举 / 评分 case / 执行 case）——缺一即幽灵行动"));
            }
            else if (d.costGold > EarlyTreasuryGoldUpperBound && IsBuildAction(d.id))
            {
                list.Add(new ChainAuditCore.Violation("R4.金门槛值域", ChainAuditCore.Severity.Yellow,
                    "action#" + (int)d.id + "(" + d.name + ") costGold=" + d.costGold
                    + " > AI 早期国库上界 " + EarlyTreasuryGoldUpperBound,
                    "静态必要不充分信号（DZ-135 类）：早期值域内 Feasible 恒伪 ⇒ 请策划端判；真判据属行为层"));
            }
        }
        ChainAuditCore.SortViolations(list);
        return list;
    }

    private static string SafeRead(string p)
    {
        try { return File.Exists(p) ? File.ReadAllText(p) : null; } catch { return null; }
    }

    /// <summary>抽取 `case EnumType.X:` 的 X 集合。</summary>
    private static HashSet<string> ExtractCases(string text, string enumType)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(text)) return set;
        var re = new Regex(@"\bcase\s+" + Regex.Escape(enumType) + @"\.(\w+)\s*:", RegexOptions.Compiled);
        foreach (Match m in re.Matches(text)) set.Add(m.Groups[1].Value);
        return set;
    }

    private static bool IsBuildAction(UtilityAction a)
    {
        switch (a)
        {
            case UtilityAction.BuildHouse:
            case UtilityAction.BuildWarehouse:
            case UtilityAction.BuildCapacity:
            case UtilityAction.BoostHarvest:
            case UtilityAction.Grain:
            case UtilityAction.BuildWall:
            case UtilityAction.BuildWell:
            case UtilityAction.BuildBlacksmith:
            case UtilityAction.BuildWarAcademy:
            case UtilityAction.BuildWarCamp:
            case UtilityAction.BuildLeyForge:
            case UtilityAction.BuildArcheryRange:
            case UtilityAction.BuildBarracks:
            case UtilityAction.BuildTrainingCamp:
            case UtilityAction.BuildSiegeWorkshop:
                return true;
            default: return false;
        }
    }

    // ========================================================================
    //  自证：注入「有声明、缺评分/执行分支」⇒ 幽灵行动 🔴（DZ-043 类）
    //  走同一 Judge 内核（非旁路布尔），保证自证与实跑同源。
    // ========================================================================
    public static bool RunSelfTest(out string detail)
    {
        var rows = new List<Row>
        {
            // ① 正常：三层齐
            new Row { Def = new UtilityActionDef { id = UtilityAction.BuildHouse, name = "注入-全通", need = NeedKind.HouseGap },
                      NeedKindValid = true, ScoringBranch = true, ExecBranch = true },
            // ② 注入：有声明、评分分支在场、**执行分支缺**（DZ-043 同型：行动池有定义但无执行通道）
            new Row { Def = new UtilityActionDef { id = UtilityAction.BuildWell, name = "注入-幽灵", need = NeedKind.WellGap },
                      NeedKindValid = true, ScoringBranch = true, ExecBranch = false },
        };
        var v = Judge(rows);
        bool ghost = false, allPassNotReported = true;
        foreach (var x in v)
        {
            if (x.Kind == "R4.幽灵行动" && x.Anchor.Contains("BuildWell")) ghost = true;
            if (x.Kind == "R4.幽灵行动" && x.Anchor.Contains("BuildHouse")) allPassNotReported = false;
        }
        bool ok = ghost && allPassNotReported;
        detail = ok
            ? "自证通过：注入「BuildWell 声明+评分在场、执行分支缺」⇒ 判幽灵行动 🔴（DZ-043 类可判）"
            : "自证失败：幽灵行动注入未产出预期 🔴";
        return ok;
    }

    // ========================================================================
    //  报告块
    // ========================================================================
    public static void AppendReport(StringBuilder sb, Result res)
    {
        sb.AppendLine("===== R4 AI 行动声明可达（静态三层绑定）=====");
        sb.AppendLine("  判定锚＝①NeedKind 枚举绑定 ②UtilityScorer.NeedScore 评分 case ③KingdomBrain.ExecuteFocus 执行 case");
        sb.AppendLine("  声明面 UtilityActionConfig.actions 实读=" + res.ActionTotal + " 条"
                      + "；评分源=" + (res.ScorerFound ? "在场" : "缺席") + " 执行源=" + (res.BrainFound ? "在场" : "缺席"));
        sb.AppendLine("  静态必要不充分：建造类 costGold > 早期国库上界(" + EarlyTreasuryGoldUpperBound + ") ⇒ 🟡 列报（口径源=设计稿 §三 R4 引 DZ-135）");
        sb.AppendLine();
        sb.AppendLine("  ── 三层绑定矩阵 ──");
        foreach (var r in res.Rows)
            sb.AppendLine("   #" + ((int)r.Def.id).ToString().PadLeft(2) + " " + Pad(r.Def.id.ToString())
                          + " need=" + ((int)r.Def.need).ToString().PadLeft(2) + " "
                          + (r.NeedKindValid ? "①✓" : "①✗") + " " + (r.ScoringBranch ? "②✓" : "②✗") + " " + (r.ExecBranch ? "③✓" : "③✗")
                          + (r.Ghost ? "  🔴幽灵" : ""));
        sb.AppendLine();

        ChainAuditCore.AppendViolations(sb, "R4 违例", res.Violations);

        bool self; string d;
        self = RunSelfTest(out d);
        SelfTestOk = self;
        sb.AppendLine("  ── 自证（DZ-043 类复现）──");
        sb.AppendLine("   " + (self ? "✅ " : "❌ ") + d);
        sb.AppendLine();
    }

    private static string Pad(string s) => (s ?? "").PadRight(22);
}
