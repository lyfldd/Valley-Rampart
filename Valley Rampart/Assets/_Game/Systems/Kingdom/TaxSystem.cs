using UnityEngine;

/// <summary>
/// 税收系统（3.5 §六 / 实施计划 P1 步骤3（税）；Singleton）。
///
/// 规则（§六）：
///   - 人头税：每人口每日 N 金（TaxConfig.headTaxPerPerson）。
///   - 建筑税：商业建筑（市场/商店）按等级抽成（无交易额统计时用 commercialTaxPerLevel × 等级）。
///   - 幸福系数：整体幸福 × 税率（幸福 100% 全额收，幸福 0 收 lowHappinessTaxFloor 倍，见 HappinessSystem.GetTaxCoefficient）。
///
/// 每日结算挂 DayCycleSettlement（不自行 Update）。税额全进 RulerController.Gold。
/// 同时把税负水平写入 HappinessSystem.TaxBurdenLastDay 供幸福税负因子计算。
///
/// ===== 2_17 步骤11 批1·AI 开征 + 独立 TaxConfig（玩家零回归改造）=====
///  - 税率字段从 KingdomConfig 迁移语义到 TaxConfig（HH.30 玩家零回归：KingdomConfig 字段保留不删，Happiness 仍读它；
///    玩家分支各字段读 TaxConfig，缺资产回退 KingdomConfig 现值 → 玩家结果逐位一致）。
///  - 玩家(id=0) 分支：人口税(PopulationSystem.PopulationCount) / 市场(kingdomId==0) / 幸福 GetTaxCoefficient /
///    入 Ruler.Gold / 写 TaxBurdenLastDay —— 逻辑与现状一致。
///  - AI(id>0) 分支：遍历 KingdomRegistry 跳过 IsPlayer，workerCount 作人头税人口 + kingdomId 匹配建筑收商业税，
///    税入 kingdom.AddResources({gold})。AI 幸福系数批1 暂按满额 1.0（AI 幸福桶批2 接入）。
/// </summary>
public class TaxSystem : Singleton<TaxSystem>
{
    private KingdomConfig _config;
    private TaxConfig _taxConfig;

    /// <summary>上一日玩家总税额（金），供 UI/调试。</summary>
    public int LastDayTax { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        _taxConfig = Resources.Load<TaxConfig>("Config/TaxConfig");
    }

    private KingdomConfig Cfg()
    {
        if (_config == null) _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        return _config;
    }

    private TaxConfig TaxCfg()
    {
        if (_taxConfig == null) _taxConfig = Resources.Load<TaxConfig>("Config/TaxConfig");
        return _taxConfig;
    }

    /// <summary>是否商业建筑（建筑税来源，§六）。D144：商业只留市场——商店/税务所已移除，仅市场（资产 id="market" 小写）。</summary>
    public static bool IsCommercialBuilding(string buildingId)
    {
        // 大小写修复：资产 id 为小写 "market"，旧写 "Market" 匹配不到导致市场商业税收不到
        return buildingId == "market";
    }

    /// <summary>人头税率（金/人口/日）。玩家(id=0) 取玩家税率；AI 取 AI 税率。TaxConfig 缺资产回退 KingdomConfig 现值保证玩家逐位等价。</summary>
    private float HeadTaxPerPerson(int kingdomId)
    {
        var t = TaxCfg();
        var c = Cfg();
        bool ai = kingdomId != 0;
        if (t != null) return ai ? t.headTaxPerPersonAI : t.headTaxPerPerson;
        return c != null ? c.headTaxPerPerson : 0.5f;
    }

    /// <summary>商业建筑每级每日建筑税基数。玩家/AI 分流同上。</summary>
    private int CommercialTaxPerLevel(int kingdomId)
    {
        var t = TaxCfg();
        var c = Cfg();
        bool ai = kingdomId != 0;
        if (t != null) return ai ? t.commercialTaxPerLevelAI : t.commercialTaxPerLevel;
        return c != null ? c.commercialTaxPerLevel : 1;
    }

    /// <summary>
    /// 每日税收结算（DayCycleSettlement 统一入口调用）。
    /// 玩家(id=0)：总税 = (人头税 + 建筑税) × 幸福系数；入库并记录税负供幸福反算。
    /// AI(id>0)：人头税(工人) + 建筑税（kingdomId 匹配）→ 入王国台账。
    /// </summary>
    public void OnNewDay()
    {
        var cfg = Cfg();
        if (cfg == null || RulerController.Instance == null) return;

        // ===== 玩家(id=0) 分支（2_17 步骤11 批1·HH.30 零回归：与现状逐位一致，仅税率字段改读 TaxConfig）=====
        int population = PopulationSystem.Instance != null ? PopulationSystem.Instance.PopulationCount : 0;

        // 人头税（每人口每日 N 金）
        int headTax = Mathf.RoundToInt(population * HeadTaxPerPerson(0));

        // 建筑税（商业建筑等级抽成；仅玩家桶0 市场，AI 市场走下方 AI 分支）
        int buildingTax = 0;
        if (BuildingRegistry.Instance != null)
        {
            var all = BuildingRegistry.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null || b.def == null || !b.IsActive) continue;
                if (!IsCommercialBuilding(b.def.id)) continue;
                if (b.kingdomId != 0) continue;   // 2_17 步骤11 批1·玩家零回归：仅玩家桶0 市场收进 Ruler.Gold
                buildingTax += Mathf.RoundToInt(CommercialTaxPerLevel(0) * b.level);
            }
        }

        // 幸福系数缩放（§六：幸福100%全额收，幸福0收0.5倍；Happiness 仍读 KingdomConfig.lowHappinessTaxFloor）
        float coeff = HappinessSystem.Instance != null
            ? HappinessSystem.Instance.GetTaxCoefficient()
            : 1f;

        int totalTax = Mathf.RoundToInt((headTax + buildingTax) * coeff);
        LastDayTax = totalTax;

        if (totalTax > 0)
            RulerController.Instance.ModifyResource(ResourceType.Gold, true, totalTax);

        // 税负水平写入幸福系统（供幸福税负因子：税重幸福低）
        if (HappinessSystem.Instance != null)
        {
            float burden = population > 0
                ? Mathf.Clamp01((headTax + buildingTax) / (float)(population * HeadTaxPerPerson(0)))
                : 0f;
            HappinessSystem.Instance.TaxBurdenLastDay = burden;
        }

        Debug.Log($"[TaxSystem] 税收结算：人头税{headTax} + 建筑税{buildingTax}，幸福系数{coeff:F2} → 实收 {totalTax} 金（人口{population}）（玩家）");

        // ===== AI(id>0) 分支（2_17 步骤11 批1·AI 开征；批2 econ-train 可接 AI 幸福桶/调税率）=====
        if (KingdomRegistry.Instance == null) return;
        var kingdoms = KingdomRegistry.Instance.GetAll();
        foreach (var kingdom in kingdoms)
        {
            if (kingdom == null || kingdom.IsPlayer) continue;
            int kId = kingdom.id;

            // 人头税：用王国工人数（workerCount 派生自存活实体，AI 人口口径）
            int aiHead = Mathf.RoundToInt(kingdom.workerCount * HeadTaxPerPerson(kId));

            // 建筑税：kingdomId 匹配本王国的商业建筑
            int aiBuilding = 0;
            if (BuildingRegistry.Instance != null)
            {
                var all = BuildingRegistry.Instance.All;
                for (int i = 0; i < all.Count; i++)
                {
                    var b = all[i];
                    if (b == null || b.def == null || !b.IsActive) continue;
                    if (b.kingdomId != kId) continue;
                    if (!IsCommercialBuilding(b.def.id)) continue;
                    aiBuilding += Mathf.RoundToInt(CommercialTaxPerLevel(kId) * b.level);
                }
            }

            // 2_17 步骤11 批1 注释：AI 幸福系数暂按满额 1.0（AI 无幸福桶；批2 econ-train 接 HappinessSystem per-kingdom 幸福后再缩放）
            int aiTotal = Mathf.RoundToInt((aiHead + aiBuilding) * 1f);

            if (aiTotal > 0)
                kingdom.AddResources(new ResourcePack { gold = aiTotal });

            Debug.Log($"[TaxSystem] AI 税收结算：王国[{kId}]{kingdom.name} 人头税{aiHead} + 建筑税{aiBuilding} → 入账 {aiTotal} 金（工人{kingdom.workerCount}）");
        }
    }
}