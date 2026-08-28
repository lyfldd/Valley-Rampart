using System.Collections.Generic;
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

    // ===== 2_17 步骤11 批2·per-kingdom 幸福分桶（Singleton 门面 + 内部 Dictionary，玩家桶0=原全局语义逐位一致 HH.30）=====
    // 玩家(id=0) 桶 = 原单标量 OverallHappiness/TaxBurdenLastDay；AI(id>0) 各王国独立桶（供 Tax/王国脑消费）。
    // 玩家无参 getter（OverallHappiness/TaxBurdenLastDay/GetTaxCoefficient/GetPopulationGrowthFactor/
    // GetRetreatThresholdModifier）读桶0，玩家现有调用点零改动。

    private readonly Dictionary<int, float> _overallHappiness = new Dictionary<int, float>();
    private readonly Dictionary<int, float> _taxBurdenLastDay = new Dictionary<int, float>();

    /// <summary>读某王国整体幸福（桶不存在回退默认 50，玩家/AI 一致占位）。</summary>
    private float GetOverallHappiness(int kingdomId)
    {
        if (_overallHappiness.TryGetValue(kingdomId, out var v)) return v;
        return 50f;
    }

    /// <summary>读某王国税负（未写入回退 0）。</summary>
    private float GetTaxBurden(int kingdomId)
    {
        if (_taxBurdenLastDay.TryGetValue(kingdomId, out var v)) return v;
        return 0f;
    }

    /// <summary>整体幸福（0-100，玩家桶0）。由 OnNewDay 每日刷新。玩家调用点读本 getter=桶0（=原全局语义）。</summary>
    public float OverallHappiness => GetOverallHappiness(0);

    /// <summary>上一日税负水平（0-1，玩家桶0；由 TaxSystem 每日写入玩家口径）。玩家调用点读本 getter=桶0。</summary>
    public float TaxBurdenLastDay { get => GetTaxBurden(0); set => _taxBurdenLastDay[0] = value; }

    /// <summary>写入某王国税负水平（AI 由 Tax 分王国写入；内部用，玩家走 TaxBurdenLastDay setter 等价桶0）。</summary>
    public void SetTaxBurden(int kingdomId, float v) => _taxBurdenLastDay[kingdomId] = v;

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
        // 2_17 步骤11 批2：per-kingdom 幸福分桶吸收批1 守卫——按 evt.Unit.kingdomId 分流（0=玩家，>0=AI 王国）。
        var uc = evt.Unit as UnitController;
        if (uc == null || uc.Data == null) return;
        if (!SatietySystem.IsNpc(uc.EffectiveOccupation)) return;   // 非 NPC（工事/君主）不扣幸福
        var cfg = Cfg();
        if (cfg == null) return;
        float k = cfg.deathHappinessK > 0f ? cfg.deathHappinessK : 0.5f;

        int kingdomId = uc.kingdomId;   // 分流：0=玩家，>0=AI 王国
        if (kingdomId == 0)
        {
            // 玩家桶0 原语义（HH.30 逐位一致）：保留原 Faction 守卫 + 玩家人口基数（PopulationSystem 玩家口径）
            if (evt.Faction != Faction.Human_Player) return;
            int population = PopulationSystem.Instance != null ? PopulationSystem.Instance.PopulationCount : 0;
            if (population <= 0) return;   // 无人口基数，无从扣减
            _overallHappiness[0] *= (1f - k / population);
            _overallHappiness[0] = Mathf.Clamp(_overallHappiness[0], 0f, 100f);
            Debug.Log($"[HappinessSystem] NPC 阵亡，整体幸福 ×= (1 - {k}/{population}) → {OverallHappiness:F1}");
        }
        else
        {
            // AI 王国桶：人口基数=该国工人+战士（KingdomState 派生人口口径）
            int pop = AiPopulation(kingdomId);
            if (pop <= 0) return;
            float cur = GetOverallHappiness(kingdomId);
            cur *= (1f - k / pop);
            _overallHappiness[kingdomId] = Mathf.Clamp(cur, 0f, 100f);
            Debug.Log($"[HappinessSystem] AI王国[{kingdomId}] 阵亡，幸福 ×= (1 - {k}/{pop}) → {_overallHappiness[kingdomId]:F1}");
        }
    }

    /// <summary>AI 王国人口基数（工人+战士；玩家走 PopulationSystem 玩家口径，AI 用 KingdomState 派生人口）。</summary>
    private static int AiPopulation(int kingdomId)
    {
        var k = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        return k != null ? k.workerCount + k.warriorCount : 0;
    }

    /// <summary>
    /// 每日幸福结算（DayCycleSettlement 统一入口调用）。
    /// 重算每个 NPC 个体幸福（多因素加权）→ 更新整体平均。
    /// </summary>
    public void OnNewDay()
    {
        var cfg = Cfg();
        if (cfg == null || UnitRegistry.Instance == null) return;

        // ===== 玩家桶0（2_17 步骤11 批2：原全局语义逐位一致，HH.30）=====
        int npcCount = 0;
        int happinessSum = 0;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.GetFaction() != Faction.Human_Player) continue;
            // 2_17 步骤4 关账扫描：仅玩家桶0——AI 工人不稀释玩家幸福（双条件保留兼容存量过渡态）。
            if (unit.kingdomId != 0) continue;
            if (!SatietySystem.IsNpc(unit.EffectiveOccupation)) continue;

            int h = ComputeUnitHappiness(unit, cfg, 0);
            unit.IndividualHappiness = Mathf.Clamp(h, 0, 100);
            happinessSum += h;
            npcCount++;
        }
        _overallHappiness[0] = npcCount > 0 ? happinessSum / (float)npcCount : 50f;

        // ===== AI 桶（kingdomId>0）：按王国各自算平均（供 Tax/王国评分消费）=====
        if (KingdomRegistry.Instance != null)
        {
            var all = KingdomRegistry.Instance.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var k = all[i];
                if (k == null || k.IsPlayer) continue;
                int c = 0, s = 0;
                foreach (var unit in UnitRegistry.Instance.GetAllUnits())
                {
                    if (unit == null || unit.Data == null) continue;
                    if (unit.kingdomId != k.id) continue;
                    if (!SatietySystem.IsNpc(unit.EffectiveOccupation)) continue;
                    int h = ComputeUnitHappiness(unit, cfg, k.id);
                    unit.IndividualHappiness = Mathf.Clamp(h, 0, 100);
                    s += h;
                    c++;
                }
                _overallHappiness[k.id] = c > 0 ? s / (float)c : 50f;
            }
        }

        Debug.Log($"[HappinessSystem] 整体幸福 = {OverallHappiness:F1}（玩家 {npcCount} 名 NPC）");
    }

    /// <summary>计算单个 NPC 的幸福（多因素加权，§五）。kingdomId 决定税负取桶（玩家桶0=原语义）。</summary>
    private int ComputeUnitHappiness(UnitController unit, KingdomConfig cfg, int kingdomId = 0)
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
        float taxBurden = Mathf.Clamp01(GetTaxBurden(kingdomId));
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
    public float GetTaxCoefficient() => GetTaxCoefficient(0);   // 玩家桶0（原语义，HH.30）

    /// <summary>税收幸福系数（per-kingdom）：幸福100%全额收，幸福0收0.5倍。AI Tax/评分消费。</summary>
    public float GetTaxCoefficient(int kingdomId)
    {
        var cfg = Cfg();
        float floor = cfg != null ? cfg.lowHappinessTaxFloor : 0.5f;
        float t = Mathf.Clamp01(GetOverallHappiness(kingdomId) / 100f);
        return Mathf.Lerp(floor, 1f, t);
    }

    /// <summary>
    /// 人口增长幸福因子（§五 惩罚2）：整体幸福低 → 人口增长减少。
    /// 0..1，PopulationSystem 生育判定时叠加（幸福越低增长率越低）。
    /// </summary>
    public float GetPopulationGrowthFactor() => GetPopulationGrowthFactor(0);   // 玩家桶0（原语义）

    /// <summary>人口增长幸福因子（per-kingdom）。</summary>
    public float GetPopulationGrowthFactor(int kingdomId) => Mathf.Clamp01(GetOverallHappiness(kingdomId) / 100f);

    /// <summary>
    /// 士气修正（§五 惩罚3）：整体幸福低 → 个体撤退阈值降低（更容易撤退）。
    /// 返回 1 - 幸福系数（0..1 惩罚量），供 AI 撤退逻辑接入（当前暴露 API，AI 接入后置）。
    /// </summary>
    public float GetRetreatThresholdModifier() => GetRetreatThresholdModifier(0);   // 玩家桶0（原语义）

    /// <summary>士气修正（per-kingdom）：AI 撤退逻辑供接口。</summary>
    public float GetRetreatThresholdModifier(int kingdomId) => 1f - Mathf.Clamp01(GetOverallHappiness(kingdomId) / 100f);
}