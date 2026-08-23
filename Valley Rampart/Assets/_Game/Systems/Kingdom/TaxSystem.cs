using UnityEngine;

/// <summary>
/// 税收系统（3.5 §六 / 实施计划 P1 步骤3（税）；Singleton）。
///
/// 规则（§六）：
///   - 人头税：每人口每日 N 金（KingdomConfig.headTaxPerPerson）。
///   - 建筑税：商业建筑（市场/商店）按等级抽成（无交易额统计时用 commercialTaxPerLevel × 等级）。
///   - 幸福系数：整体幸福 × 税率（幸福 100% 全额收，幸福 0 收 lowHappinessTaxFloor 倍，见 HappinessSystem.GetTaxCoefficient）。
///
/// 每日结算挂 DayCycleSettlement（不自行 Update）。税额全进 RulerController.Gold。
/// 同时把税负水平写入 HappinessSystem.TaxBurdenLastDay 供幸福税负因子计算。
/// </summary>
public class TaxSystem : Singleton<TaxSystem>
{
    private KingdomConfig _config;

    /// <summary>上一日总税额（金），供 UI/调试。</summary>
    public int LastDayTax { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
    }

    private KingdomConfig Cfg()
    {
        if (_config == null) _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        return _config;
    }

    /// <summary>是否商业建筑（建筑税来源，§六）。D144：商业只留市场——商店/税务所已移除，仅市场（资产 id="market" 小写）。</summary>
    public static bool IsCommercialBuilding(string buildingId)
    {
        // 大小写修复：资产 id 为小写 "market"，旧写 "Market" 匹配不到导致市场商业税收不到
        return buildingId == "market";
    }

    /// <summary>
    /// 每日税收结算（DayCycleSettlement 统一入口调用）。
    /// 总税 = (人头税 + 建筑税) × 幸福系数；入库并记录税负供幸福反算。
    /// </summary>
    public void OnNewDay()
    {
        var cfg = Cfg();
        if (cfg == null || RulerController.Instance == null) return;

        int population = PopulationSystem.Instance != null ? PopulationSystem.Instance.PopulationCount : 0;

        // 人头税（每人口每日 N 金）
        int headTax = Mathf.RoundToInt(population * cfg.headTaxPerPerson);

        // 建筑税（商业建筑等级抽成）
        int buildingTax = 0;
        if (BuildingRegistry.Instance != null)
        {
            var all = BuildingRegistry.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null || b.def == null || !b.IsActive) continue;
                if (!IsCommercialBuilding(b.def.id)) continue;
                buildingTax += Mathf.RoundToInt(cfg.commercialTaxPerLevel * b.level);
            }
        }

        // 幸福系数缩放（§六：幸福100%全额收，幸福0收0.5倍）
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
                ? Mathf.Clamp01((headTax + buildingTax) / (float)(population * cfg.headTaxPerPerson))
                : 0f;
            HappinessSystem.Instance.TaxBurdenLastDay = burden;
        }

        Debug.Log($"[TaxSystem] 税收结算：人头税{headTax} + 建筑税{buildingTax}，幸福系数{coeff:F2} → 实收 {totalTax} 金（人口{population}）");
    }
}