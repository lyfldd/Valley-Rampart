using UnityEngine;

/// <summary>
/// 幸福度系统（3.5 §五 / 实施计划 P1 步骤2；Singleton）。
///
/// 规则（§五）：
///   - 平均分配：幸福度计算到每个 NPC 个体（UnitController.IndividualHappiness），整体幸福 = 全体平均。
///   - 多因素：饱食满足 / 有房住 / 教堂 / 医院 / 税负 / 食品品质（权重全在 KingdomConfig 可调）。
///   - 三层惩罚（整体幸福低时）：
///       1. 税收减少（GetTaxCoefficient：幸福系数 × 税率，幸福100%全额收、幸福0收0.5倍）
///       2. 人口增长减少（GetPopulationGrowthFactor，PopulationSystem 消费）
///       3. 士气低（GetRetreatThresholdModifier，个体撤退阈值降低；AI 撤退逻辑后置，暴露 API 供接入）
///
/// 接入：PopulationSystem.AvgHappiness 占位常量由本系统替代（PopulationSystem 读本系统 OverallHappiness）。
/// 每日结算挂 DayCycleSettlement（不自行 Update）。
/// </summary>
public class HappinessSystem : Singleton<HappinessSystem>
{
    private KingdomConfig _config;

    /// <summary>整体幸福（0-100，全体 NPC 平均）。由 RecomputeHappiness 每日刷新。</summary>
    public float OverallHappiness { get; private set; } = 50f;

    /// <summary>上一日税负水平（0-1，供幸福税负因子计算；由 TaxSystem 每日写入）。</summary>
    public float TaxBurdenLastDay { get; set; }

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        // 3.5 P0-1：订阅单位死亡事件，NPC 阵亡 → 整体幸福扣减（防雪崩）
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        base.OnDestroy();
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    private KingdomConfig Cfg()
    {
        if (_config == null) _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        return _config;
    }

    // ===== 3.5 P0-1：NPC 死亡 → 整体幸福扣减（防雪崩）=====

    /// <summary>
    /// 单位死亡事件处理器。我方 NPC（非工事/非君主）阵亡 → 整体幸福按公式扣减：
    /// avgHappiness ×= (1 - K/当前人口)，K=deathHappinessK（SO 可调，默认 0.5）。
    /// 连死按当前人口实时重算（分母随人口递减），避免一次性/雪崩式扣光。
    /// </summary>
    private void OnUnitDied(UnitDiedEvent evt)
    {
        // 2_17 步骤11 批1 守卫升格吸收：本死亡幸福扣减仅服务玩家(Human_Player)/幸福桶0——守卫将在批2 被 per-kingdom 幸福分桶吸收，
        // 本批不动分桶，仅标注（AI 王国阵亡幸福扣减归批2），玩家行为不变。
        if (evt.Faction != Faction.Human_Player) return;
        var uc = evt.Unit as UnitController;
        if (uc == null || uc.Data == null) return;
        if (!SatietySystem.IsNpc(uc.EffectiveOccupation)) return;   // 非 NPC（工事/君主）不扣幸福

        var cfg = Cfg();
        if (cfg == null) return;
        int population = PopulationSystem.Instance != null ? PopulationSystem.Instance.PopulationCount : 0;
        if (population <= 0) return;   // 无人口基数，无从扣减

        float k = cfg.deathHappinessK > 0f ? cfg.deathHappinessK : 0.5f;
        OverallHappiness *= (1f - k / population);
        OverallHappiness = Mathf.Clamp(OverallHappiness, 0f, 100f);
        Debug.Log($"[HappinessSystem] NPC 阵亡，整体幸福 ×= (1 - {k}/{population}) → {OverallHappiness:F1}");
    }

    /// <summary>
    /// 每日幸福结算（DayCycleSettlement 统一入口调用）。
    /// 重算每个 NPC 个体幸福（多因素加权）→ 更新整体平均。
    /// </summary>
    public void OnNewDay()
    {
        var cfg = Cfg();
        if (cfg == null || UnitRegistry.Instance == null) return;

        int npcCount = 0;
        int happinessSum = 0;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.GetFaction() != Faction.Human_Player) continue;
            // 2_17 步骤4 关账扫描：仅玩家桶0——AI 工人不稀释玩家幸福；收编后 GetFaction=AiKingdom 首条件已排除（此双条件保留兼容存量过渡态）。
            // 2_17 步骤11 批1 守卫升格吸收：此 kingdomId!=0 内联守卫不动分桶（分桶属批2），本批仅标注——守卫将在批2 被 per-kingdom 幸福分桶吸收，玩家行为不变。
            if (unit.kingdomId != 0) continue;
            if (!SatietySystem.IsNpc(unit.EffectiveOccupation)) continue;

            int h = ComputeUnitHappiness(unit, cfg);
            unit.IndividualHappiness = Mathf.Clamp(h, 0, 100);
            happinessSum += h;
            npcCount++;
        }

        OverallHappiness = npcCount > 0 ? happinessSum / (float)npcCount : 50f;
        Debug.Log($"[HappinessSystem] 整体幸福 = {OverallHappiness:F1}（{npcCount} 名 NPC）");
    }

    /// <summary>计算单个 NPC 的幸福（多因素加权，§五）。</summary>
    private int ComputeUnitHappiness(UnitController unit, KingdomConfig cfg)
    {
        int satietyFactor = 0;
        if (unit.Satiety >= cfg.feedSatietyThreshold)
            satietyFactor = cfg.happinessSatietyBonusMax;
        else
            satietyFactor = Mathf.RoundToInt(cfg.happinessSatietyBonusMax * (cfg.feedSatietyThreshold > 0 ? (float)unit.Satiety / cfg.feedSatietyThreshold : 0f));

        int houseFactor = HasEnoughHousing(cfg) ? Mathf.RoundToInt(cfg.happinessHouseWeight * 100f) : 0;
        // 教堂/医院按数量计幸福（3.5 P2：多建多加成，§13.3 教堂/医院幸福加成）
        int churchFactor = Mathf.Clamp(Mathf.RoundToInt(cfg.happinessChurchWeight * 100f * CountActiveBuildings("Church")), 0, 100);
        int hospitalFactor = Mathf.Clamp(Mathf.RoundToInt(cfg.happinessHospitalWeight * 100f * CountActiveBuildings("Hospital")), 0, 100);

        // 税负：税越重幸福越低（0-1 税负 → 0-最高惩罚）
        float taxBurden = Mathf.Clamp01(TaxBurdenLastDay);
        int taxPenalty = Mathf.RoundToInt(cfg.happinessTaxPenaltyMax * taxBurden);

        // 食品品质：王国产出高档食品（特殊食物/肉）时小幅加成
        int foodQualityFactor = 0;
        if (RulerController.Instance != null
            && (RulerController.Instance.GetResource(ResourceType.SpecialFood) > 0 || RulerController.Instance.GetResource(ResourceType.Meat) > 0))
            foodQualityFactor = Mathf.RoundToInt(cfg.happinessFoodQualityWeight * 100f);

        int h = cfg.happinessBase
            + satietyFactor
            + houseFactor
            + churchFactor
            + hospitalFactor
            + foodQualityFactor
            - taxPenalty;

        return Mathf.Clamp(h, 0, 100);
    }

    // ===== 基础设施判定（BuildingRegistry 按 BuildingDef.id 匹配）=====

    /// <summary>统计处于 Active 态的指定 id 建筑数量（教堂/医院等；3.5 P2 多建多加成）。</summary>
    public static int CountActiveBuildings(string buildingId)
    {
        int count = 0;
        if (BuildingRegistry.Instance == null) return count;
        var all = BuildingRegistry.Instance.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null || b.def == null) continue;
            if (b.def.id == buildingId && b.IsActive) count++;
        }
        return count;
    }

    /// <summary>是否存在处于 Active 态的指定 id 建筑（教堂/医院等）。</summary>
    public static bool HasBuilding(string buildingId) => CountActiveBuildings(buildingId) > 0;

    /// <summary>房屋容量是否足够容纳当前人口（§五 有房住）。房屋容量 = Σ房屋Lv容量（3/5/8，§13.14）。</summary>
    private bool HasEnoughHousing(KingdomConfig cfg)
    {
        int capacity = GetTotalHouseCapacity();
        int population = PopulationSystem.Instance != null ? PopulationSystem.Instance.PopulationCount : 0;
        return capacity >= population;
    }

    /// <summary>
    /// 王国房屋总容量（Σ活动房屋 Lv 容量 3/5/8，§13.14）。
    /// 3.5 P0-1：PopulationSystem 生育硬前置用——剩余容量 > 0 才允许出生（房屋满=禁止生育）。
    /// </summary>
    public int GetTotalHouseCapacity()
    {
        int capacity = 0;
        if (BuildingRegistry.Instance != null)
        {
            var all = BuildingRegistry.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null || b.def == null || !b.IsActive) continue;
                if (b.def.id != "House") continue;
                capacity += GetHouseCapacity(b.level);
            }
        }
        return capacity;
    }

    /// <summary>房屋 Lv 容量（§13.14：Lv1=3 / Lv2=5 / Lv3=8）。</summary>
    public static int GetHouseCapacity(int level) => level >= 3 ? 8 : level >= 2 ? 5 : 3;

    // ===== 三层惩罚接口 =====

    /// <summary>
    /// 税收幸福系数（§六）：幸福100%全额收，幸福0收0.5倍（lowHappinessTaxFloor）。
    /// 区间 [lowHappinessTaxFloor, 1.0]。TaxSystem 用它缩放应征税额。
    /// </summary>
    public float GetTaxCoefficient()
    {
        var cfg = Cfg();
        float floor = cfg != null ? cfg.lowHappinessTaxFloor : 0.5f;
        float t = Mathf.Clamp01(OverallHappiness / 100f);
        return Mathf.Lerp(floor, 1f, t);
    }

    /// <summary>
    /// 人口增长幸福因子（§五 惩罚2）：整体幸福低 → 人口增长减少。
    /// 0..1，PopulationSystem 生育判定时叠加（幸福越低增长率越低）。
    /// </summary>
    public float GetPopulationGrowthFactor() => Mathf.Clamp01(OverallHappiness / 100f);

    /// <summary>
    /// 士气修正（§五 惩罚3）：整体幸福低 → 个体撤退阈值降低（更容易撤退）。
    /// 返回 1 - 幸福系数（0..1 惩罚量），供 AI 撤退逻辑接入（当前暴露 API，AI 接入后置）。
    /// </summary>
    public float GetRetreatThresholdModifier() => 1f - Mathf.Clamp01(OverallHappiness / 100f);
}