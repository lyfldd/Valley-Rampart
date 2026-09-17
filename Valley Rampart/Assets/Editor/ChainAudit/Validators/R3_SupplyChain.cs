using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// ============================================================================
//  HH.240 断链校验台 · R3 资源供给链（Editor-only 静态校验器）
//  规格真源：HH.240 任务书 T4 ＋ 断链校验台_设计稿 §三 R3
//
//  扫描：ResourceType（GameEvents.cs:176）的**供给点** ↔ **消费点**
//   供给：ProducerComponent（outputResource/rate）· MineByproductComponent · Gather（【片 6-2】数据寻址链）
//        · StorageComponent.Transform 产出侧 · TaxSystem（金）· RanchSystem（肉）· WorldGatherSource（木·【片 6-2】数据寻址四型唯一源）
//   消费：BuildingDef.cost / BuildingDef.levels[].upgradeCost（建造/升级）· UtilityActionDef.costXxx（AI 行动）
//        · TrainingDef.costGold/costCrystal/costMetal（训练/转职）· StorageComponent.Transform 输入侧（矿）
//        · SiegeWorkshopBuilding 产弹原料（石/火油/水晶）· 源码硬扣 `ModifyResource(type,false,…)`
//
//  判据（设计稿 §三 R3）：`有消费 ∧ 供给==0 ⇒ 🔴`；`有供给 ∧ 消费==0 ⇒ 🟡`（蓄水）。
//  边界：只判"有没有"，不判"够不够"（后者属调优）。
//  自证：`RunSelfTest` 注入 HH.107 前的历史态（水晶/火油/矿零供给）⇒ 须产 🔴（复现 DZ-072 类）。
//
//  接口纪律：只读声明面（资产/登记表），只列报不修、不回写（防双源 L-15）。
// ============================================================================
public static class R3_SupplyChain
{
    public const string Tag = "ChainAudit.R3";

    // R3 豁免表（设计内例外）。【D561 硬红线】变更须策划端确认——本批**不自行扩张**，初始为空。
    private static readonly ChainAuditCore.ExemptionTable Exempt
        = new ChainAuditCore.ExemptionTable("R3 供给链豁免", null, new Dictionary<string, string>());

    /// <summary>豁免表条目数（报告汇总用；变更须策划端确认 D561）。</summary>
    public static int ExemptCount => Exempt.Count;

    /// <summary>最近一次自证结果（由 AppendReport 更新；供聚合判定行）。</summary>
    public static bool SelfTestOk { get; private set; }

    private const string GameEventsRel = "Assets/_Game/Core/GameEvents.cs";

    /// <summary>结果：违例 + 供给/消费矩阵（矩阵＝A+/金门裁决依据）。</summary>
    public sealed class Result
    {
        public readonly List<ChainAuditCore.Violation> Violations = new List<ChainAuditCore.Violation>();
        public readonly SortedDictionary<string, List<string>> Supply = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, List<string>> Consume = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        public readonly SortedDictionary<string, List<string>> SoftRead = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
    }

    // ========================================================================
    //  入口
    // ========================================================================
    public static Result Run(string dataPath = null)
    {
        if (string.IsNullOrEmpty(dataPath)) dataPath = Application.dataPath;
        var res = new Result();

        var allResources = Enum.GetNames(typeof(ResourceType));
        foreach (var r in allResources) { res.Supply[r] = new List<string>(); res.Consume[r] = new List<string>(); res.SoftRead[r] = new List<string>(); }

        CollectAssetSupplies(res);
        CollectConfigConsumes(res);
        CollectSourceFacts(dataPath, res);

        res.Violations.AddRange(Judge(res));
        return res;
    }

    private static string First(List<string> l)
    {
        var c = new List<string>(l);
        c.Sort(StringComparer.Ordinal);
        return c.Count > 0 ? c[0] : "(无)";
    }

    // ========================================================================
    //  ① 供给（资产面：BuildingDef 全量）
    // ========================================================================
    private static void CollectAssetSupplies(Result res)
    {
        var defs = Resources.LoadAll<BuildingDef>("Buildings");
        if (defs == null) return;
        foreach (var def in defs)
        {
            if (def == null) continue;
            string path = "Resources/Buildings/" + def.id + ".asset";

            // ProducerComponent（BuildingFactory.AttachComponents：rate>0 ∧ kind==Resource ∧ !isResourceNode）
            // 特例：Well 的 outputResource 为占位（真产水入隐藏桶，ProducerComponent.Init L62 特判）⇒ 不计为资源供给
            bool wellPlaceholder = string.Equals(def.id, "Well", StringComparison.Ordinal);
            bool producerEligible = def.producer.rate > 0f && def.producer.kind == ProduceKind.Resource
                                    && !def.isResourceNode && !def.isBlacksmith && !def.isSiegeWorkshop;
            if (producerEligible && !wellPlaceholder)
                Add(res.Supply, def.outputResource, path + " ProducerComponent(rate=" + def.producer.rate + ")");

            if (wellPlaceholder && def.producer.rate > 0f)
                Add(res.Supply, null, path + " ProducerComponent(水→隐藏桶 WaterNetwork；非 ResourceType 桶)");

            // MineByproductComponent（isMineByproduct：恒产 Crystal/FireOil/Ore）
            if (def.isMineByproduct)
            {
                Add(res.Supply, ResourceType.Crystal, path + " MineByproductComponent(L62)");
                Add(res.Supply, ResourceType.FireOil, path + " MineByproductComponent(L63)");
                Add(res.Supply, ResourceType.Ore, path + " MineByproductComponent(L64)");
            }

            // BlacksmithBuilding（Ore→Metal 产出侧，StorageComponent.Transform L116 Add(metal)）
            if (def.isBlacksmith)
                Add(res.Supply, ResourceType.Metal, path + " BlacksmithBuilding(StorageComponent.Transform 产出侧)");

            // SiegeWorkshopBuilding（三弹药子仓 L215-217）
            if (def.isSiegeWorkshop)
            {
                Add(res.Supply, ResourceType.StoneAmmo, path + " SiegeWorkshopBuilding.CreateSubStores(L55-58)");
                Add(res.Supply, ResourceType.FireballAmmo, path + " SiegeWorkshopBuilding.CreateSubStores(L55-58)");
                Add(res.Supply, ResourceType.MagicAmmo, path + " SiegeWorkshopBuilding.CreateSubStores(L55-58)");
            }

            // 【HH.294 片 6-2·6-D 口径同步】一次性资源点的**采集供给**已从「实体链」改为「**数据寻址**」：
            //   改前记 `Gather(OnGatherCompleted，Building.cs L948-951)`（实体销毁入账）；
            //   改后＝格表（`MapGate`）→ `WorldGatherSource`（`RespawnConfig.GatherSecondsOf` 逐型耗时）
            //          → 采集完成 `ResourceRespawnSystem.HandleCellGathered`（格翻 Plain＋池子减 1 点）。
            //   供给资源面不变（`def.outputResource`：OreVein=Ore／WoodPile=Wood／StonePile=Stone）⇒ 图不产生假断链。
            if (def.isConsumable)
                Add(res.Supply, def.outputResource,
                    path + " Gather(数据寻址·WorldGatherSource→HandleCellGathered，HH.294 片6-2)");
        }
    }

    // ========================================================================
    //  ② 消费（资产面：建造成本 / 升级成本 / AI 行动成本 / 训练成本）
    // ========================================================================
    private static void CollectConfigConsumes(Result res)
    {
        // 建造 / 升级成本
        var defs = Resources.LoadAll<BuildingDef>("Buildings");
        if (defs != null)
        {
            foreach (var def in defs)
            {
                if (def == null) continue;
                string path = "Resources/Buildings/" + def.id + ".asset";
                AddPack(res.Consume, def.cost, path + " BuildingDef.cost");
                if (def.levels != null)
                    for (int i = 0; i < def.levels.Length; i++)
                        AddPack(res.Consume, def.levels[i].upgradeCost, path + " BuildingDef.levels[" + i + "].upgradeCost");
            }
        }

        // AI 行动成本镜像（UtilityActionDef.costGold/costStone/costWood/costFood）
        var acfg = Resources.Load<UtilityActionConfig>("Config/Kingdoms/UtilityActionConfig");
        if (acfg != null && acfg.actions != null)
        {
            for (int i = 0; i < acfg.actions.Length; i++)
            {
                var a = acfg.actions[i];
                string anchor = "Resources/Config/Kingdoms/UtilityActionConfig.asset action#" + a.id + "(" + a.name + ")";
                Add(res.Consume, ResourceType.Gold, a.costGold > 0 ? anchor + ".costGold" : null);
                Add(res.Consume, ResourceType.Stone, a.costStone > 0 ? anchor + ".costStone" : null);
                Add(res.Consume, ResourceType.Wood, a.costWood > 0 ? anchor + ".costWood" : null);
                Add(res.Consume, ResourceType.Food, a.costFood > 0 ? anchor + ".costFood" : null);
            }
        }

        // 训练 / 转职成本（TrainingDef.costGold/costCrystal/costMetal）
        var tcfg = Resources.Load<TrainingConfig>("Config/TrainingConfig");
        if (tcfg != null && tcfg.trainings != null)
        {
            for (int i = 0; i < tcfg.trainings.Length; i++)
            {
                var t = tcfg.trainings[i];
                string anchor = "Resources/Config/TrainingConfig.asset " + t.fromOccupation + "→" + t.toOccupation;
                Add(res.Consume, ResourceType.Gold, t.costGold > 0 ? anchor + ".costGold" : null);
                Add(res.Consume, ResourceType.Crystal, t.costCrystal > 0 ? anchor + ".costCrystal" : null);
                Add(res.Consume, ResourceType.Metal, t.costMetal > 0 ? anchor + ".costMetal" : null);
            }
        }
    }

    private static void AddPack(SortedDictionary<string, List<string>> map, ResourcePack pack, string anchor)
    {
        Add(map, ResourceType.Gold, pack.gold > 0 ? anchor + ".gold" : null);
        Add(map, ResourceType.Stone, pack.stone > 0 ? anchor + ".stone" : null);
        Add(map, ResourceType.Wood, pack.wood > 0 ? anchor + ".wood" : null);
        Add(map, ResourceType.Food, pack.food > 0 ? anchor + ".food" : null);
        Add(map, ResourceType.Metal, pack.metal > 0 ? anchor + ".metal" : null);
        Add(map, ResourceType.StoneAmmo, pack.stoneAmmo > 0 ? anchor + ".stoneAmmo" : null);
        Add(map, ResourceType.FireballAmmo, pack.fireballAmmo > 0 ? anchor + ".fireballAmmo" : null);
        Add(map, ResourceType.MagicAmmo, pack.magicAmmo > 0 ? anchor + ".magicAmmo" : null);
    }

    // ========================================================================
    //  ③ 源码事实（供给/消费/读取锚点扫描 · 只扫 _Game）
    // ========================================================================
    private static readonly Regex ReDeduct = new Regex(@"ModifyResource\s*\(\s*ResourceType\.(\w+)\s*,\s*false", RegexOptions.Compiled);
    private static readonly Regex ReSupply = new Regex(@"ModifyResource\s*\(\s*ResourceType\.(\w+)\s*,\s*true", RegexOptions.Compiled);
    private static readonly Regex ReRead = new Regex(@"GetResource(?:Value)?\s*\(\s*ResourceType\.(\w+)\s*\)", RegexOptions.Compiled);

    private static void CollectSourceFacts(string dataPath, Result res)
    {
        string root = ChainAuditCore.ProductionSourceRoot(dataPath);
        if (!Directory.Exists(root)) return;

        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string text;
            try { text = File.ReadAllText(file); } catch { continue; }
            string rel = ChainAuditCore.RelPath(file);

            foreach (Match m in ReDeduct.Matches(text))
                Add(res.Consume, m.Groups[1].Value, rel + ":" + ChainAuditCore.LineOf(text, m.Index) + " ModifyResource(…,false,…)");
            foreach (Match m in ReSupply.Matches(text))
                Add(res.Supply, m.Groups[1].Value, rel + ":" + ChainAuditCore.LineOf(text, m.Index) + " ModifyResource(…,true,…)");
            foreach (Match m in ReRead.Matches(text))
                Add(res.SoftRead, m.Groups[1].Value, rel + ":" + ChainAuditCore.LineOf(text, m.Index) + " 读取(非扣费)");
        }

        // ── 已知专属锚点（设计稿 §三 R3 明列·不在上述通用模式内）────────────────
        // 【HH.294 片 6-2·6-B/6-C 口径同步】玩家/AI 四型的采集源统一为 `WorldGatherSource`（树＋一次性三型·
            //   地表物→资源映射 `TryResourceOf`）；旧数据格树源类已随片 6-2 收尾批清场删除（其 Wood 口径由本行承接）。
            Add(res.Supply, ResourceType.Wood,
                "Assets/_Game/Systems/World/WorldGatherSource.cs（数据寻址四型·TryResourceOf→Wood）");
        Add(res.Supply, ResourceType.Meat, "Assets/_Game/Systems/Kingdom/RanchSystem.cs:203（屠宰→Meat）");
        Add(res.Consume, ResourceType.Ore, "Assets/_Game/Systems/Building/StorageComponent.cs:99/115（Transform 输入侧 Ore→Metal）");
        Add(res.Consume, ResourceType.Stone, "Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs:184-186（石弹原料→Stone）");
        Add(res.Consume, ResourceType.FireOil, "Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs:184（火弹原料→FireOil）");
        Add(res.Consume, ResourceType.Crystal, "Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs:185（魔弹原料→Crystal）");
    }

    private static void Add(SortedDictionary<string, List<string>> map, ResourceType type, string anchor)
    {
        if (string.IsNullOrEmpty(anchor)) return;
        Add(map, type.ToString(), anchor);
    }

    private static void Add(SortedDictionary<string, List<string>> map, string name, string anchor)
    {
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(anchor)) return;
        List<string> l;
        if (!map.TryGetValue(name, out l)) { l = new List<string>(); map[name] = l; }
        if (!l.Contains(anchor)) l.Add(anchor);
    }

    // ========================================================================
    //  自证：复现 DZ-072 类（HH.107 前历史态 = 水晶/火油/矿 零供给）
    //  设计稿 §八：每序验收句＝"能复现既有已登记断链"。DZ-072 已清偿（登记表 §7），
    //  ⇒ 用**受控注入**证明本校验器对该类缺陷有效（机制自证），而非依赖现状复现。
    // ========================================================================
    public static bool RunSelfTest(out string detail)
    {
        // 构造历史态小模型：水晶/火油/矿 有消费、零供给
        var res = new Result();
        foreach (var r in Enum.GetNames(typeof(ResourceType))) { res.Supply[r] = new List<string>(); res.Consume[r] = new List<string>(); res.SoftRead[r] = new List<string>(); }
        res.Consume["Crystal"].Add("(注入)训练 costCrystal");
        res.Consume["FireOil"].Add("(注入)火弹原料");
        res.Consume["Ore"].Add("(注入)Transform 输入侧");
        res.Supply["Stone"].Add("(注入)quarry");

        var v = Judge(res);
        bool ok = HasRed(v, "Crystal") && HasRed(v, "FireOil") && HasRed(v, "Ore") && HasRed(v, "Stone") == false;
        detail = ok
            ? "自证通过：注入「水晶/火油/矿 有消费零供给」⇒ 三条 🔴（DZ-072 类可判）"
            : "自证失败：注入态未产出预期 🔴";
        return ok;
    }

    private static bool HasRed(List<ChainAuditCore.Violation> v, string resource)
    {
        foreach (var x in v)
            if (x.Level == ChainAuditCore.Severity.Red && x.Kind.StartsWith("R3.", StringComparison.Ordinal)
                && x.Anchor != null && x.Anchor.Contains("ResourceType." + resource, StringComparison.Ordinal))
                return true;
        return false;
    }

    /// <summary>判据内核（Run 与 RunSelfTest 共用；保证自证与实跑同源）。</summary>
    private static List<ChainAuditCore.Violation> Judge(Result res)
    {
        var list = new List<ChainAuditCore.Violation>();
        foreach (var r in Enum.GetNames(typeof(ResourceType)))
        {
            if (Exempt.Contains(r)) continue;
            List<string> sup, con, soft;
            if (!res.Supply.TryGetValue(r, out sup)) sup = new List<string>();
            if (!res.Consume.TryGetValue(r, out con)) con = new List<string>();
            if (!res.SoftRead.TryGetValue(r, out soft)) soft = new List<string>();

            if (con.Count > 0 && sup.Count == 0)
                list.Add(new ChainAuditCore.Violation("R3.有消费零供给", ChainAuditCore.Severity.Red,
                    "ResourceType." + r + " 消费锚点=" + First(con), "补供给点 / 退役消费点"));
            else if (sup.Count > 0 && con.Count == 0)
                list.Add(new ChainAuditCore.Violation("R3.有供给零消费", ChainAuditCore.Severity.Yellow,
                    "ResourceType." + r + " 供给锚点=" + First(sup), "蓄水（有产无消）：确认设计意图 / 补消费点"));
            else if (sup.Count == 0 && soft.Count > 0)
                list.Add(new ChainAuditCore.Violation("R3.有读取零供给", ChainAuditCore.Severity.Yellow,
                    "ResourceType." + r + " 读取锚点=" + First(soft), "弱信号（有读取面、零供给、无硬消费）：疑似未实装，请策划端判"));
        }
        ChainAuditCore.SortViolations(list);
        return list;
    }

    // ========================================================================
    //  报告块
    // ========================================================================
    public static void AppendReport(StringBuilder sb, Result res)
    {
        sb.AppendLine("===== R3 资源供给链（静态）=====");
        sb.AppendLine("  判据：有消费∧供给==0 ⇒ 🔴 ／ 有供给∧消费==0 ⇒ 🟡 ／ 有读取∧零供给 ⇒ 🟡弱");
        sb.AppendLine("  Resources/Buildings 实盘 def=" + (Resources.LoadAll<BuildingDef>("Buildings")?.Length ?? 0));
        sb.AppendLine();

        sb.AppendLine("  ── 供给/消费矩阵（A+/金门裁决依据）──");
        sb.AppendLine("  " + Pad("ResourceType") + Pad("供给") + Pad("硬消费") + "读取");
        foreach (var r in Enum.GetNames(typeof(ResourceType)))
        {
            List<string> sup, con, soft;
            res.Supply.TryGetValue(r, out sup); res.Consume.TryGetValue(r, out con); res.SoftRead.TryGetValue(r, out soft);
            string flag = (con != null && con.Count > 0 && (sup == null || sup.Count == 0)) ? " 🔴"
                        : (sup != null && sup.Count > 0 && (con == null || con.Count == 0)) ? " 🟡" : "";
            sb.AppendLine("  " + Pad(r) + Pad((sup?.Count ?? 0).ToString()) + Pad((con?.Count ?? 0).ToString())
                          + (soft?.Count ?? 0) + flag);
        }
        sb.AppendLine();

        sb.AppendLine("  ── 锚点明细（供给）──");
        foreach (var kv in res.Supply)
            foreach (var a in Sorted(kv.Value)) sb.AppendLine("   [供] " + kv.Key + " ← " + a);
        sb.AppendLine("  ── 锚点明细（消费）──");
        foreach (var kv in res.Consume)
            foreach (var a in Sorted(kv.Value)) sb.AppendLine("   [消] " + kv.Key + " ← " + a);
        sb.AppendLine();

        ChainAuditCore.AppendViolations(sb, "R3 违例", res.Violations);

        bool self;
        string d;
        self = RunSelfTest(out d);
        SelfTestOk = self;
        sb.AppendLine("  ── 自证（DZ-072 类复现）──");
        sb.AppendLine("   " + (self ? "✅ " : "❌ ") + d);
        sb.AppendLine();
    }

    private static List<string> Sorted(List<string> l)
    {
        var c = new List<string>(l);
        c.Sort(StringComparer.Ordinal);
        return c;
    }

    private static string Pad(string s) => (s ?? "").PadRight(20);
}
