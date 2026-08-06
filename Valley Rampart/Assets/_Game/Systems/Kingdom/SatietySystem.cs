using UnityEngine;

/// <summary>
/// 饱食度系统（3.5 §四 / 实施计划 P1 步骤1，数据层先行；Singleton）。
///
/// 核心规则（§四）：
///   - 每个 NPC 个体饱食 0-100（UnitController.Satiety，随 UnitSaveData v2 持久化）。
///   - 每日结算：按职业日耗粮（KingdomConfig.GetDailyFoodByOccupation）→ 饱食恢复 or 衰减。
///   - 饱食 0 → 持续扣血；80+ → 缓慢回血；长期不满足 → 个体幸福降低。阈值/速度全 SO 可调。
///   - 进食：数据层 = 饱食不满时消耗国库粮（RulerController.Food）恢复饱食；
///     NPC 移动至粮仓/食堂/搬运的表演走 IWorkerTaskExecutor 接口后置（等 AI 稳定）。
///
/// 食品等级（§10，FoodQuality）：粮+5 / 特殊食物+8(幸福+1) / 肉+20(幸福+3)。
/// 每日统一结算挂 DayCycleSettlement（不自行 Update，避免各系统散乱结算点）。
/// </summary>
public class SatietySystem : Singleton<SatietySystem>
{
    private KingdomConfig _config;

    /// <summary>食品等级（§10 粮/特殊食物/肉）。</summary>
    public enum FoodQuality
    {
        Grain,      // 粮：饱食 +5，幸福 +0
        Special,    // 特殊食物：饱食 +8，幸福 +1
        Meat        // 肉：饱食 +20，幸福 +3
    }

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

    /// <summary>是否属于需要饱食的 NPC（排除纯工事/战争机器/君主，它们非受治平民）。
    /// 3.5 P0-4：君主不参与饱食/不吃粮（仅指挥移动，不耗粮）。</summary>
    public static bool IsNpc(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.Wall:
            case Occupation.Gate:
            case Occupation.Barricade:
            case Occupation.ArrowTower:
            case Occupation.CrossbowTower:
            case Occupation.MagicTower:
            case Occupation.Tower:
            case Occupation.SiegeMachine:
            case Occupation.Ballista:
            case Occupation.Ruler:   // 3.5 P0-4：君主不耗粮
                return false;
            default:
                return true;
        }
    }

    /// <summary>全体我方 NPC 平均饱食（供 PopulationSystem 生育条件 / 幸福计算）。无 NPC 返回 50。</summary>
    public float GetAverageSatiety()
    {
        if (UnitRegistry.Instance == null) return 50f;
        int count = 0;
        int sum = 0;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.faction != Faction.Human_Player) continue;
            if (!IsNpc(unit.EffectiveOccupation)) continue;
            sum += unit.Satiety;
            count++;
        }
        return count > 0 ? sum / (float)count : 50f;
    }

    /// <summary>
    /// 每日饱食结算（DayCycleSettlement 统一入口调用）。
    /// 对全体我方 NPC：尝试进食（消耗国库粮）→ 未进食则衰减 → 应用 0 扣血 / 80+ 回血 / 饥饿降幸福。
    /// </summary>
    public void OnNewDay()
    {
        var cfg = Cfg();
        if (cfg == null || UnitRegistry.Instance == null) return;

        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.faction != Faction.Human_Player) continue;
            if (!IsNpc(unit.EffectiveOccupation)) continue;
            if (!unit.IsAlive) continue;

            SettleUnit(unit, cfg);
        }

        Debug.Log($"[SatietySystem] 每日饱食结算完成（>>> 见各单位日志）");
    }

    /// <summary>单个单位每日饱食结算。</summary>
    private void SettleUnit(UnitController unit, KingdomConfig cfg)
    {
        int dailyFoodCost = cfg.GetDailyFoodByOccupation(unit.EffectiveOccupation);

        // 1. 进食（数据层）：饱食不满阈值 且 国库粮足 → 消耗粮恢复饱食
        bool fed = false;
        if (unit.Satiety < cfg.feedSatietyThreshold && RulerController.Instance != null
            && RulerController.Instance.Food >= dailyFoodCost)
        {
            RulerController.Instance.ModifyResource(ResourceType.Food, false, dailyFoodCost);
            unit.Satiety = Mathf.Clamp(unit.Satiety + cfg.foodRestoreGrain, 0, 100);
            fed = true;
        }

        // 2. 饱食变化：进食 +foodRestoreGrain；未进食 -satietyDecayPerDay
        if (!fed)
            unit.Satiety = Mathf.Clamp(unit.Satiety - cfg.satietyDecayPerDay, 0, 100);

        // 3. 阈值表现：0 扣血 / 80+ 回血
        if (unit.Satiety <= cfg.satietyHurtThreshold)
        {
            if (cfg.satietyHurtPerDay > 0)
                unit.TakeDamage(cfg.satietyHurtPerDay);
            Debug.Log($"[SatietySystem] {unit.Data.occupation} 饥饿（饱食{unit.Satiety}）扣血 {cfg.satietyHurtPerDay}");
        }
        else if (unit.Satiety >= cfg.satietyRegenThreshold)
        {
            int heal = cfg.satietyRegenPerDay;
            // 医院存在加速受伤恢复（3.5 P2，§13.3 医院：恢复 + 幸福）
            if (HappinessSystem.HasBuilding("Hospital"))
                heal += cfg.hospitalRecoveryBonus;
            if (heal > 0)
                unit.Heal(heal);
            Debug.Log($"[SatietySystem] {unit.Data.occupation} 温饱（饱食{unit.Satiety}）回血 {heal}");
        }

        // 4. 长期不满足 → 个体幸福降低
        if (unit.Satiety < cfg.hungerHappinessThreshold)
        {
            unit.IndividualHappiness = Mathf.Clamp(
                unit.IndividualHappiness - cfg.hungerHappinessPenalty, 0, 100);
            Debug.Log($"[SatietySystem] {unit.Data.occupation} 长期饥饿，幸福 -{cfg.hungerHappinessPenalty} → {unit.IndividualHappiness}");
        }
    }

    /// <summary>
    /// 进食（按食品等级，高等食品额外加幸福；§10）。
    /// 由饱食进食/牧场宰肉/加工食品消费调用。返回是否成功进食。
    /// </summary>
    public bool FeedUnit(UnitController unit, FoodQuality quality)
    {
        if (unit == null || !unit.IsAlive) return false;
        var cfg = Cfg();
        if (cfg == null) return false;

        int restore = quality switch
        {
            FoodQuality.Special => cfg.foodRestoreSpecial,
            FoodQuality.Meat => cfg.foodRestoreMeat,
            _ => cfg.foodRestoreGrain
        };
        int happinessBonus = quality switch
        {
            FoodQuality.Special => cfg.foodHappinessSpecial,
            FoodQuality.Meat => cfg.foodHappinessMeat,
            _ => cfg.foodHappinessGrain
        };

        unit.Satiety = Mathf.Clamp(unit.Satiety + restore, 0, 100);
        if (happinessBonus > 0)
            unit.IndividualHappiness = Mathf.Clamp(unit.IndividualHappiness + happinessBonus, 0, 100);

        Debug.Log($"[SatietySystem] {unit.Data?.occupation} 食用[{quality}] 饱食+{restore}(→{unit.Satiety}) 幸福+{happinessBonus}");
        return true;
    }
}