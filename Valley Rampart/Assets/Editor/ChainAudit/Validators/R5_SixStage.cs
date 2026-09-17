using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// ============================================================================
//  HH.240 断链校验台 · R5 六阶段完整性（Editor-only 静态校验器 · **只做机器可判子集**）
//  规格真源：HH.240 任务书 T7 ＋ 断链校验台_设计稿 §三 R5
//
//  复用登记表六阶段定义，对每个 def 查**机器可判**锚点：
//   ① 生成入口存在性（至少一条生成路径：玩家菜单 / 地图自然 / AI 预置 / AI 行动 / 动态立国 / 代码驱动）
//   ② 存档 ISaveable（对应实体类是否实现）
//   ③ 注册注销**成对**（订阅↔退订；统一以源码 file 为单位比对同事件计数）
//
//  **语义面不自动化**（设计稿 §三 R5 硬条款）：如「gate 的 GateStateChangedEvent 零订阅算不算断」
//  机器判不了 ⇒ **本器不报**（否则误报会淹没真信号，需海量豁免表维护）。
//
//  【误报抑噪·设计稿 §十】「无任何生成入口」不得一律拔高 🔴（会硬拦交付并淹没真信号）：
//    · 代码驱动生成（`FindDefById("id")` / `*_DEF_ID = "id"`）⇒ **有入口**（如 VagrantCamp 营地锚）
//    · 同名实体类走独立管线（如 `Portal` ∉ 建筑六阶段语义）⇒ 🟡 单列
//    · 声明面（登记表 §1）**已登**死资产/退役/废值 ⇒ 🟡 已知声明态（不硬拦；本器只读声明面）
//    ⇒ 仅「**未声明 ∧ 无任何入口**」才判 🔴（真·新断链）。校验器**不回写**登记表（L-15）。
//
//  自证：`RunSelfTest` 注入「def 无任何生成入口 ∧ 未声明」⇒ 须报生成入口缺失 🔴（P1 幽灵引用 / DZ-049 类）。
// ============================================================================
public static class R5_SixStage
{
    public const string Tag = "ChainAudit.R5";

    private static readonly ChainAuditCore.ExemptionTable Exempt
        = new ChainAuditCore.ExemptionTable("R5 六阶段豁免", null, new Dictionary<string, string>());

    /// <summary>豁免表条目数（报告汇总用；变更须策划端确认 D561）。</summary>
    public static int ExemptCount => Exempt.Count;

    /// <summary>最近一次自证结果（由 AppendReport 更新；供聚合判定行）。</summary>
    public static bool SelfTestOk { get; private set; }

    /// <summary>声明面（登记表 §1）路径（R6 同源·只读）。</summary>
    private const string RegistryRel = "河谷防线开发计划书具体内容/改造计划/生命周期登记表.md";

    /// <summary>⭐【HH.294 片 6-2 收尾·口径同步（`D779` 残余 `S1`）】**格表资源点（非实体出口）**：
    /// 经「格表 ＋ 门」（`MapGate`）进世界、采集走数据寻址 —— 旧口径「与 `BuildingFactory.FeatureToBuildingType`
    /// 同源」已随该函数删除失效（实体派生路径已清场·`naturalBuildings` 恒空）。
    /// 纳入 <see cref="Row.HasAny"/> ⇒ 如实记为「**有出口（非 Building 路径）**」。
    /// ⛔ 不含 `Mine`（锚点·T6 转型面 ⇒ 单列 <see cref="GridAnchorTypes"/>）。</summary>
    private static readonly HashSet<BuildingType> GridTableResourceTypes = new HashSet<BuildingType>
    {
        BuildingType.Tree, BuildingType.OreVein, BuildingType.WoodPile, BuildingType.StonePile
    };

    /// <summary>格表锚点（`Mine`·T6 转型面）：`HH.293 B1` 起不派生实体；**单列声明**、不混入「资源点」列，
    /// 也不纳入 <see cref="Row.HasAny"/> —— 锚点是世界特征，非建筑生成路径（`mine` 建筑实例走 AI 预置，
    /// 实锚见 `KingdomDef.baseBuildingDefIds`）。列报期为人工审读保留该事实。</summary>
    private static readonly HashSet<BuildingType> GridAnchorTypes = new HashSet<BuildingType>
    {
        BuildingType.Mine
    };

    /// <summary>动态立国同批预置（KingdomFoundry.PlaceCampCastle/PlaceCampWell 独立于 baseBuildingDefIds 链）。</summary>
    private static readonly HashSet<string> DynamicFoundingIds = new HashSet<string>(StringComparer.Ordinal) { "castle", "Well" };

    public sealed class Result
    {
        public readonly List<ChainAuditCore.Violation> Violations = new List<ChainAuditCore.Violation>();
        public readonly List<Row> Rows = new List<Row>();
        public readonly List<string> PairIssues = new List<string>();
        public bool BuildingSaveable;
        public bool PortalSaveable;
        public int DefTotal;
        public int DeclaredCount;        // 声明面（登记表 §1）解析出的 def 名数
        public int CodeSpawnCount;       // 代码驱动生成命中数（误报抑制证据）
    }

    public sealed class Row
    {
        public string Id;
        public bool PlayerMenu;     // isPlayerBuilt
        public bool GridTable;      // ⭐ 格表资源点（非实体出口·Tree/OreVein/WoodPile/StonePile）
        public bool GridAnchor;     // ⭐ 格表锚点（Mine·T6 转型面·单列声明·不纳入 HasAny）
        public bool AiPreset;       // ∈ KingdomDef.baseBuildingDefIds 并集
        public bool AiAction;       // ∈ UtilityActionConfig buildingId 集
        public bool DynamicFound;   // ∈ 动态立国集
        public bool CodeSpawn;      // 代码驱动生成（FindDefById("id") / 常量 *_DEF_ID = "id"）
        public bool EntityClass;    // 同名实体类在场（走独立管线，非 Building 路径）⇒ 单列
        public bool Declared;       // ∈ 登记表 §1（声明面已登 ⇒ 已知态，不硬拦）
        public bool HasAny => PlayerMenu || GridTable || AiPreset || AiAction || DynamicFound || CodeSpawn;

        /// <summary>判定分级：🔴＝未声明 ∧ 无任何入口（真·新断链）；🟡＝已声明 or 独立管线实体。</summary>
        public bool IsStructuralBreak => !HasAny && !Declared && !EntityClass;
    }

    // ========================================================================
    //  入口
    // ========================================================================
    public static Result Run(string dataPath = null)
    {
        if (string.IsNullOrEmpty(dataPath)) dataPath = Application.dataPath;
        var res = new Result();

        // ── 生成入口信号源 ──────────────────────────────────────────────────
        var presetIds = CollectPresetIds();
        var actionIds = CollectActionBuildingIds();
        var codeSpawnIds = CollectCodeSpawnIds(dataPath);
        var entityIds = CollectEntityClassIds(dataPath);
        var declaredIds = CollectDeclaredIds(dataPath);
        res.DeclaredCount = declaredIds.Count;

        var defs = Resources.LoadAll<BuildingDef>("Buildings");
        if (defs != null)
        {
            res.DefTotal = defs.Length;
            foreach (var d in defs)
            {
                if (d == null) continue;
                bool codeSpawn = codeSpawnIds.Contains(d.id);
                if (codeSpawn) res.CodeSpawnCount++;
                var row = new Row
                {
                    Id = d.id,
                    PlayerMenu = d.isPlayerBuilt,
                    GridTable = GridTableResourceTypes.Contains(d.sourceType),
                    GridAnchor = GridAnchorTypes.Contains(d.sourceType),
                    AiPreset = presetIds.Contains(d.id),
                    AiAction = actionIds.Contains(d.id),
                    DynamicFound = DynamicFoundingIds.Contains(d.id),
                    CodeSpawn = codeSpawn,
                    EntityClass = entityIds.Contains(d.id),
                    Declared = declaredIds.Contains(d.id),
                };
                res.Rows.Add(row);
            }
        }
        res.Rows.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

        // ② 存档 ISaveable（实体类级·全局事实）
        res.BuildingSaveable = typeof(ISaveable).IsAssignableFrom(typeof(Building));
        res.PortalSaveable = typeof(ISaveable).IsAssignableFrom(typeof(Portal));

        // ③ 注册注销成对（源码 file 级：同事件 Subscribe 与 Unsubscribe 计数）
        CollectPairIssues(dataPath, res);

        // ── 判据 ────────────────────────────────────────────────────────────
        foreach (var row in res.Rows)
        {
            if (Exempt.Contains(row.Id)) continue;
            if (row.HasAny) continue;

            string matrix = "（玩家菜单✗ 格表资源✗ 格表锚点✗ AI预置✗ AI行动✗ 动态立国✗ 代码驱动✗）";
            if (row.IsStructuralBreak)
                res.Violations.Add(new ChainAuditCore.Violation("R5.生成入口缺失", ChainAuditCore.Severity.Red,
                    "BuildingDef " + row.Id + matrix,
                    "P1 幽灵引用/死资产：补生成入口 或 提策划端退役（不自行处置）"));
            else
                res.Violations.Add(new ChainAuditCore.Violation(
                    row.EntityClass ? "R5.独立管线实体" : "R5.声明态无入口", ChainAuditCore.Severity.Yellow,
                    "BuildingDef " + row.Id + matrix
                    + (row.EntityClass ? " 独立管线实体类在场" : "") + (row.Declared ? " 登记表 §1 已声明" : ""),
                    row.EntityClass
                        ? "非建筑六阶段语义（走独立实体管线，如 Portal/Chest）⇒ 列报不拦；归 2_14/2_10 域"
                        : "声明面已登（死资产/退役/废值）⇒ 已知态列报不拦；退役执行归策划端裁剪批（本器不回写）"));
        }
        if (!res.BuildingSaveable)
            res.Violations.Add(new ChainAuditCore.Violation("R5.实体存档缺席", ChainAuditCore.Severity.Red,
                "Building 未实现 ISaveable", "公共链存档断裂 ⇒ 立即排查"));
        if (!res.PortalSaveable)
            res.Violations.Add(new ChainAuditCore.Violation("R5.实体存档缺席", ChainAuditCore.Severity.Red,
                "Portal 未实现 ISaveable", "2_14 传送门存档断裂 ⇒ 排查"));

        foreach (var p in res.PairIssues)
            res.Violations.Add(new ChainAuditCore.Violation("R5.注册注销不成对", ChainAuditCore.Severity.Red,
                p, "缺退订/注销（有生无死）⇒ 补配对入口；语义面（零订阅是否算断）不自动判"));

        ChainAuditCore.SortViolations(res.Violations);
        return res;
    }

    private static HashSet<string> CollectPresetIds()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var lib = Resources.Load<KingdomTemplateLibrary>("Config/Kingdoms/KingdomTemplateLibrary");
        var list = new List<KingdomDef>();
        if (lib != null && lib.templates != null) list.AddRange(lib.templates);
        // 兜底：直接全量加载 Kingdom_* 资产（模板库缺失时仍可判）
        if (list.Count == 0)
        {
            var all = Resources.LoadAll<KingdomDef>("Config/Kingdoms");
            if (all != null) list.AddRange(all);
        }
        foreach (var kd in list)
        {
            if (kd == null || kd.baseBuildingDefIds == null) continue;
            foreach (var id in kd.baseBuildingDefIds) if (!string.IsNullOrEmpty(id)) set.Add(id);
        }
        return set;
    }

    private static HashSet<string> CollectActionBuildingIds()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        var acfg = Resources.Load<UtilityActionConfig>("Config/Kingdoms/UtilityActionConfig");
        if (acfg != null && acfg.actions != null)
            foreach (var a in acfg.actions) if (!string.IsNullOrEmpty(a.buildingId)) set.Add(a.buildingId);
        return set;
    }

    // ========================================================================
    //  ①′ 代码驱动生成（误报抑噪：`FindDefById("id")` / 常量 `*_DEF_ID = "id"`）
    //  实锚：VagrantCampSystem.cs L15 `CAMP_DEF_ID = "VagrantCamp"` + L211 `FindDefById(CAMP_DEF_ID)`
    //        → L221 CreateBuildingInstance(isPlayerBuilt:false)（系统生成，非五路之一 ⇒ 旧版误报）
    //  口径：字面量直接命中 ＋ 常量传递（`X_DEF_ID = "v"` 后 `FindDefById(X...)` 视为同 id）
    // ========================================================================
    private static readonly Regex ReFindDefLiteral = new Regex(@"FindDefById\s*\(\s*""(\w+)""\s*\)", RegexOptions.Compiled);
    private static readonly Regex ReDefIdConst = new Regex(@"\b\w*_DEF_ID\s*=\s*""(\w+)""", RegexOptions.Compiled);
    private static readonly Regex ReFindDefVar = new Regex(@"FindDefById\s*\(\s*(\w+)\s*\)", RegexOptions.Compiled);

    private static HashSet<string> CollectCodeSpawnIds(string dataPath)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        string root = ChainAuditCore.ProductionSourceRoot(dataPath);
        if (!Directory.Exists(root)) return set;
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string text;
            try { text = File.ReadAllText(file); } catch { continue; }
            foreach (Match m in ReFindDefLiteral.Matches(text)) set.Add(m.Groups[1].Value);
            foreach (Match m in ReDefIdConst.Matches(text)) set.Add(m.Groups[1].Value);
            // 常量传递：`var def = BuildingFactory.FindDefById(CAMP_DEF_ID)` ⇒ 已由 ReDefIdConst 覆盖同 id
            foreach (Match m in ReFindDefVar.Matches(text))
            {
                var c = new Regex(@"\b" + Regex.Escape(m.Groups[1].Value) + @"\s*=\s*""(\w+)""", RegexOptions.Compiled).Match(text);
                if (c.Success) set.Add(c.Groups[1].Value);
            }
        }
        return set;
    }

    // ========================================================================
    //  ①″ 同名实体类在场（走独立管线，非 Building 六阶段路径）⇒ 🟡 单列不硬拦
    //  口径：`<id>` 去记法归一后匹配 `class <Name>`；并映射已知别名（treasure_box→ChestEntity）
    // ========================================================================
    private static readonly Dictionary<string, string> EntityClassAlias = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "treasure_box", "ChestEntity" },   // ChestEntity.cs L7 自认"不走 Building"，宝箱独立管线（DZ-074）
    };

    private static readonly Regex ReClassDecl = new Regex(@"\bclass\s+([A-Za-z_]\w*)", RegexOptions.Compiled);

    private static HashSet<string> CollectEntityClassIds(string dataPath)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        string root = ChainAuditCore.ProductionSourceRoot(dataPath);
        if (!Directory.Exists(root)) return set;

        var classes = new HashSet<string>(StringComparer.Ordinal);          // 原样类名
        var classesNorm = new HashSet<string>(StringComparer.Ordinal);      // 记法归一
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string text;
            try { text = File.ReadAllText(file); } catch { continue; }
            foreach (Match m in ReClassDecl.Matches(text))
            {
                classes.Add(m.Groups[1].Value);
                classesNorm.Add(ChainAuditCore.NormKey(m.Groups[1].Value));
            }
        }

        var defs = Resources.LoadAll<BuildingDef>("Buildings");
        if (defs != null)
            foreach (var d in defs)
            {
                if (d == null) continue;
                string alias;
                if (EntityClassAlias.TryGetValue(d.id, out alias) && classes.Contains(alias)) { set.Add(d.id); continue; }
                if (classes.Contains(d.id) || classesNorm.Contains(ChainAuditCore.NormKey(d.id))) set.Add(d.id);
            }
        return set;
    }

    // ========================================================================
    //  ①‴ 声明面（登记表 §1 def 表 · **只读**）——已声明 ⇒ 已知态，不硬拦（L-15：不回写）
    // ========================================================================
    private static readonly Regex ReDeclRow = new Regex(@"^\|\s*(\d+)\s*\|\s*([A-Za-z_][A-Za-z0-9_]*)\s*\|", RegexOptions.Compiled | RegexOptions.Multiline);

    private static HashSet<string> CollectDeclaredIds(string dataPath)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        string repo = ChainAuditCore.RepoRoot(dataPath);
        string abs = Path.Combine(repo, RegistryRel.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(abs)) return set;
        string text;
        try { text = File.ReadAllText(abs); } catch { return set; }

        int start = text.IndexOf("## §1", StringComparison.Ordinal);
        if (start < 0) return set;
        int end = text.IndexOf("## §2", start + 4, StringComparison.Ordinal);
        string seg = end > start ? text.Substring(start, end - start) : text.Substring(start);
        foreach (Match m in ReDeclRow.Matches(seg))
        {
            string name = m.Groups[2].Value;
            bool hasLower = false;
            for (int i = 0; i < name.Length; i++) if (char.IsLower(name[i])) { hasLower = true; break; }
            if (hasLower) set.Add(name);
        }
        return set;
    }

    // ========================================================================
    //  ③ 注册注销成对（file 级）
    // ========================================================================
    private static readonly Regex ReSub = new Regex(@"\bSubscribe\s*<\s*(\w+)\s*>", RegexOptions.Compiled);
    private static readonly Regex ReUnsub = new Regex(@"\bUnsubscribe\s*<\s*(\w+)\s*>", RegexOptions.Compiled);

    private static void CollectPairIssues(string dataPath, Result res)
    {
        string root = ChainAuditCore.ProductionSourceRoot(dataPath);
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string text;
            try { text = File.ReadAllText(file); } catch { continue; }
            string rel = ChainAuditCore.RelPath(file);

            var sub = new Dictionary<string, int>(StringComparer.Ordinal);
            var unsub = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Match m in ReSub.Matches(text)) Bump(sub, m.Groups[1].Value);
            foreach (Match m in ReUnsub.Matches(text)) Bump(unsub, m.Groups[1].Value);

            foreach (var kv in sub)
            {
                int u; unsub.TryGetValue(kv.Key, out u);
                if (kv.Value > 0 && u == 0)
                    res.PairIssues.Add(rel + " 订阅 " + kv.Key + " ×" + kv.Value + "／退订 ×0");
            }
        }
        res.PairIssues.Sort(StringComparer.Ordinal);
    }

    private static void Bump(Dictionary<string, int> map, string k)
    {
        int n; map.TryGetValue(k, out n); map[k] = n + 1;
    }

    // ========================================================================
    //  自证：注入「def 无任何生成入口 ∧ 未声明」⇒ 生成入口缺失 🔴
    // ========================================================================
    public static bool RunSelfTest(out string detail)
    {
        var dead = new Row { Id = "(注入)DeadAsset", Declared = false, EntityClass = false };
        var known = new Row { Id = "(注入)DeclaredAsset", Declared = true };
        var indep = new Row { Id = "(注入)EntityAsset", EntityClass = true };
        bool ok = dead.IsStructuralBreak                 // 未声明 ∧ 无入口 ⇒ 🔴
                  && !known.IsStructuralBreak            // 已声明 ⇒ 🟡（抑噪）
                  && !indep.IsStructuralBreak;           // 独立管线 ⇒ 🟡（抑噪）
        detail = ok
            ? "自证通过：注入「无入口 ∧ 未声明」⇒ 判生成入口缺失 🔴；已声明/独立管线 ⇒ 🟡（误报抑噪生效，P1 幽灵引用 / DZ-049 类可判）"
            : "自证失败：生成入口分级判据未命中";
        return ok;
    }

    // ========================================================================
    //  报告块
    // ========================================================================
    public static void AppendReport(StringBuilder sb, Result res)
    {
        sb.AppendLine("===== R5 六阶段完整性（静态·机器可判子集）=====");
        sb.AppendLine("  判据子集：①生成入口存在性 ②存档 ISaveable ③注册注销成对；**语义面不自动化**（不报）");
        sb.AppendLine("  生成入口六路＝玩家菜单/**格表资源（非实体出口·Tree/OreVein/WoodPile/StonePile）**/AI预置/AI行动/动态立国/**代码驱动**（FindDefById 实锚）；**格表锚点（Mine·T6 转型面）单列**、不计入口");
        sb.AppendLine("  误报抑噪：已声明（登记表 §1·只读）/ 独立管线实体 ⇒ 🟡列报；仅「无入口 ∧ 未声明」⇒ 🔴硬拦");
        sb.AppendLine("  BuildingDef 实盘=" + res.DefTotal + "；声明面已登=" + res.DeclaredCount
                      + "；代码驱动命中=" + res.CodeSpawnCount
                      + "；实体存档：Building ISaveable=" + (res.BuildingSaveable ? "✓" : "✗")
                      + " Portal ISaveable=" + (res.PortalSaveable ? "✓" : "✗"));
        sb.AppendLine();
        sb.AppendLine("  ── ① 生成入口矩阵 ──");
        foreach (var r in res.Rows)
            sb.AppendLine("   " + Pad(r.Id) + (r.PlayerMenu ? "玩家菜单✓ " : "玩家菜单✗ ")
                          + (r.GridTable ? "格表资源✓ " : "格表资源✗ ") + (r.GridAnchor ? "格表锚点✓ " : "格表锚点✗ ")
                          + (r.AiPreset ? "AI预置✓ " : "AI预置✗ ")
                          + (r.AiAction ? "AI行动✓ " : "AI行动✗ ") + (r.DynamicFound ? "动态立国✓ " : "动态立国✗ ")
                          + (r.CodeSpawn ? "代码驱动✓" : "代码驱动✗")
                          + (!r.HasAny ? (r.IsStructuralBreak ? "  🔴无任何入口" : "  🟡已知态无入口") : (r.GridAnchor ? "  (格表锚点·T6 转型面)" : "")));
        sb.AppendLine();
        sb.AppendLine("  ── ③ 注册注销成对（file 级）──");
        sb.AppendLine("   订阅无退订文件数=" + res.PairIssues.Count);
        foreach (var p in res.PairIssues) sb.AppendLine("    " + p);
        sb.AppendLine();

        ChainAuditCore.AppendViolations(sb, "R5 违例", res.Violations);

        bool self; string d;
        self = RunSelfTest(out d);
        SelfTestOk = self;
        sb.AppendLine("  ── 自证（P1 幽灵引用类）──");
        sb.AppendLine("   " + (self ? "✅ " : "❌ ") + d);
        sb.AppendLine();
    }

    private static string Pad(string s) => (s ?? "").PadRight(16);
}
