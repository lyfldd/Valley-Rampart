using System;
using System.Collections.Generic;
using System.Text;

// ============================================================================
//  HH.240 断链校验台 · 声明面（ChainAuditSpec · Editor-only）
//  规格真源：HH.240 任务书 T3/T8 ＋ 断链校验台_设计稿 §二/§四
//
//  职责：**声明面**（人工维护 ＋ 半自动提取）——供 R4（行动可达）/R6（双源对拍）/探针矩阵消费。
//    · 能力清单 = 「设计声明该有的行为能力」（行动/产出/建成），行为层据此逐项配探针；
//    · 与 R6 源B（登记表/映射表）并列，属"声明侧"（区别于源A 实现面）。
//  【D561 硬红线】本清单=声明面真源之一，变更须策划端确认；校验器**只读**本清单，禁自动回写。
// ============================================================================
public static class ChainAuditSpec
{
    public const string Tag = "ChainAudit.Spec";

    /// <summary>声明能力（行为层探针的"声明角"）。
    /// 四列必填（`test-harness-first` §8.6 硬条款 · 缺列不得开跑）：
    /// 〔可判定最早日＋命中即停〕EarliestDay/StopRule ／〔服务哪条验收句〕Judge ／〔作用域〕Scope ／
    /// 〔口径来源与排除项〕Caliber。</summary>
    public struct Capability
    {
        public string Id;          // 稳定 id（探针矩阵行键）
        public string Label;       // 显示名（对齐任务书圈号/行动名）
        public string Kind;        // Action（AI 行动）／Production（产出）／Build（建成）
        public string Source;      // 声明出处（文档/资产锚点）
        public string Judge;       // 列② 服务哪条验收句
        public string Scope;       // 列③ 作用域（全批 any-国 / 指定国）
        public int EarliestDay;    // 列① 可判定最早日
        public string StopRule;    // 列① 命中即停条件
        public string Caliber;     // 列④ 口径来源与排除项
        public bool RequiresMilitaryStage;   // 前置门：minStage=3（军事期未达 ⇒ 未命中不可归因"入口不可达"，D669 同级同作用域）

        public Capability(string id, string label, string kind, string source, string judge, string scope,
            int earliestDay, string stopRule, string caliber, bool requiresMilitaryStage = false)
        {
            Id = id; Label = label; Kind = kind; Source = source; Judge = judge; Scope = scope;
            EarliestDay = earliestDay; StopRule = stopRule; Caliber = caliber;
            RequiresMilitaryStage = requiresMilitaryStage;
        }
    }

    // ── 声明能力清单（人工维护；本批＝AI 行动池实盘 26 条＋弹药产出 3 条）────────
    //  来源：UtilityActionConfig.asset 实读（26 条 action，D637）；弹药=SiegeWorkshopBuilding 三子仓。
    //  注：本清单是**声明面**，不引资产/不反射（纯静态常量，保证报告幂等）。
    private static readonly Capability[] _caps = BuildCaps();

    public static IReadOnlyList<Capability> Capabilities => _caps;

    private static Capability[] BuildCaps()
    {
        // 四列（test-harness-first §8.6）：〔可判定最早日＋命中即停〕〔服务哪条验收句〕〔作用域〕〔口径来源与排除项〕
        var l = new List<Capability>();
        // (id, label, kind, source, judge, scope, earliestDay, stopRule, caliber)
        l.Add(new Capability("㉗", "采集世界资源点 GatherWorldResource", "Action",
            "UtilityActionConfig.asset id27；HH.221/D685 A②", "㉗ 被选中 ≥1 次（入口可达·DZ-148 判据）", "全批 any-国",
            5, "census top=GatherWorldResource 首达 ⇒ 入口可达；全程 0 ⇒ 🔴入口不可达",
            "口径=DiagMilitary census `top=` 逐日；排除项=不数 chSrc（副产源灌水，HH.222 口径修订）"));
        l.Add(new Capability("③", "建产能 BuildCapacity", "Action",
            "UtilityActionConfig.asset id3", "③ 被选中 ≥1 次且建成 >0（DZ-148/DZ-135 判据）", "全批 any-国",
            5, "census top=BuildCapacity 首达或建成 >0 ⇒ 可达；选中>0 而建成=0 ⇒ 🔴feasible 恒伪",
            "口径=census `top=` ＋ 建筑落地计数；排除项=不并入他国读数"));
        l.Add(new Capability("②", "建仓库 BuildWarehouse", "Action",
            "UtilityActionConfig.asset id2", "② 被选中 ≥1 次且建成 >0", "全批 any-国",
            5, "census top=BuildWarehouse 首达或建成 >0 ⇒ 可达",
            "口径=census `top=`；排除项=不含预置 Warehouse"));
        l.Add(new Capability("⑦", "招战士 RecruitWarrior", "Action",
            "UtilityActionConfig.asset id7", "⑦ 被选中 ≥1 次且训练完成 >0", "全批 any-国",
            10, "census top=RecruitWarrior 首达且 warrior>0 ⇒ 可达；选中>0 而 warrior 恒 0 ⇒ 🔴空转",
            "口径=census `top=` ＋ `warrior=` 存量；排除项=不含初始 warrior"));
        l.Add(new Capability("⑰a", "建兵营 BuildBarracks", "Action",
            "UtilityActionConfig.asset id23", "⑰a 被选中 ≥1 次且落地 >0（DZ-043 判据）", "全批 any-国",
            10, "census top=BuildBarracks 首达且 Barracks 落地 >0 ⇒ 可达",
            "口径=census `top=` ＋ DiagChain 落地；排除项=不含预置 Barracks"));
        l.Add(new Capability("⑯", "训练将军 TrainGeneral", "Action",
            "UtilityActionConfig.asset id22", "⑯ 被选中 ≥1 次", "全批 any-国",
            20, "census top=TrainGeneral 首达 ⇒ 可达",
            "口径=census `top=`；排除项=军事期未达则不判（minStage=3）", true));
        l.Add(new Capability("㉕", "造战争机器 ProduceMachine", "Action",
            "UtilityActionConfig.asset id26", "㉕ 被选中 ≥1 次", "全批 any-国",
            20, "census top=ProduceMachine 首达 ⇒ 可达",
            "口径=census `top=`；排除项=prefab 缺席国不判（D594 硬条款）", true));
        l.Add(new Capability("⑨", "修工事城墙 BuildWall", "Action",
            "UtilityActionConfig.asset id9", "⑨ 被选中 ≥1 次", "全批 any-国",
            5, "census top=BuildWall 首达 ⇒ 可达",
            "口径=census `top=`；排除项=无"));
        l.Add(new Capability("⑤", "屯粮 Grain", "Action",
            "UtilityActionConfig.asset id5", "⑤ 被选中 ≥1 次", "全批 any-国",
            5, "census top=Grain 首达 ⇒ 可达",
            "口径=census `top=`；排除项=无"));
        l.Add(new Capability("⑥", "招工人 RecruitWorker", "Action",
            "UtilityActionConfig.asset id6", "⑥ 被选中 ≥1 次", "全批 any-国",
            3, "census top=RecruitWorker 首达 ⇒ 可达",
            "口径=census `top=`；排除项=无"));
        // 产出类（弹药三仓；服务 2_12 步骤9 弹药链）
        l.Add(new Capability("AMMO.S", "石头弹 StoneAmmo 产出", "Production",
            "SiegeWorkshopBuilding.CreateSubStores(L55-58)", "弹药产出 >0", "全批 any-国",
            15, "弹药子仓存量 >0 首达 ⇒ 产出可达",
            "口径=SiegeWorkshopBuilding 子仓；排除项=装填消耗不回冲存量（净存量口径）"));
        l.Add(new Capability("AMMO.F", "火弹 FireballAmmo 产出", "Production",
            "SiegeWorkshopBuilding.CreateSubStores(L55-58)", "弹药产出 >0", "全批 any-国",
            15, "弹药子仓存量 >0 首达 ⇒ 产出可达",
            "口径=同上；排除项=同上（火油供给依赖 DZ-072 链）"));
        l.Add(new Capability("AMMO.M", "魔弹 MagicAmmo 产出", "Production",
            "SiegeWorkshopBuilding.CreateSubStores(L55-58)", "弹药产出 >0", "全批 any-国",
            15, "弹药子仓存量 >0 首达 ⇒ 产出可达",
            "口径=同上；排除项=同上（水晶供给依赖 DZ-072 链）"));
        return l.ToArray();
    }

    /// <summary>声明面摘要（报告用；幂等）。</summary>
    public static void AppendReport(StringBuilder sb)
    {
        sb.AppendLine("===== ChainAuditSpec 声明面 =====");
        sb.AppendLine("  声明能力数=" + _caps.Length + "（声明面=人工维护＋半自动提取；D561 变更须策划端确认）");
        foreach (var c in _caps)
        {
            sb.AppendLine("   [" + c.Kind + "] " + c.Id + " " + c.Label);
            sb.AppendLine("     列①可判定最早日=D" + c.EarliestDay + " 命中即停=" + c.StopRule);
            sb.AppendLine("     列②服务句=" + c.Judge);
            sb.AppendLine("     列③作用域=" + c.Scope + "  列④口径来源与排除项=" + c.Caliber);
            sb.AppendLine("     声明出处=" + c.Source);
        }
        sb.AppendLine();
    }

    /// <summary>四列必填自检（test-harness-first §8.6 硬条款 · 缺列不得开跑）。返回缺列清单。</summary>
    public static List<string> CheckFourColumns()
    {
        var missing = new List<string>();
        foreach (var c in _caps)
        {
            if (c.EarliestDay <= 0) missing.Add(c.Id + ":缺〔可判定最早日〕");
            if (string.IsNullOrEmpty(c.StopRule)) missing.Add(c.Id + ":缺〔命中即停〕");
            if (string.IsNullOrEmpty(c.Judge)) missing.Add(c.Id + ":缺〔服务哪条验收句〕");
            if (string.IsNullOrEmpty(c.Scope)) missing.Add(c.Id + ":缺〔作用域〕");
            if (string.IsNullOrEmpty(c.Caliber)) missing.Add(c.Id + ":缺〔口径来源与排除项〕");
        }
        return missing;
    }
}
