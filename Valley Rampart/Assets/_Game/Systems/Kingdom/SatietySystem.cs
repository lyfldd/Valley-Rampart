using System.Collections.Generic;
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

    // ===== 2_17 步骤11 批2·平均饱食 per-kingdom 分桶（Singleton 门面 + 内部 Dictionary，玩家桶0=原语义 HH.30）=====
    // 玩家(id=0) 桶 = 原 GetAverageSatiety 整体均值；AI(id>0) 各王国独立桶（AI 均饱食供王国脑/评分消费）。
    // 无参 GetAverageSatiety 仍走玩家实时计算（逐位一致）；OnNewDay 结算时把每王国均值写入本桶供 AI 下游读。
    private readonly Dictionary<int, float> _avgSatiety = new Dictionary<int, float>();

    /// <summary>读某王国最近一次每日结算的均饱食缓存桶（未结算回退 50）。供 AI 下游（王国脑/评分）消费；玩家请走 GetAverageSatiety() 实时口径。</summary>
    public float GetAverageSatietyCached(int kingdomId)
    {
        if (_avgSatiety.TryGetValue(kingdomId, out var v)) return v;
        return 50f;
    }

    /// <summary>写某王国均饱食缓存桶（2_17 步骤14 批B：AbstractEconomySettler 抽象结算写入公式值；
    /// 唤醒拉平由 OnNewDay 消费）。玩家桶仍走实时口径，勿外部写。</summary>
    public void SetAverageSatietyCached(int kingdomId, float value)
    {
        _avgSatiety[kingdomId] = value;
    }

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
            case Occupation.ArrowTower:
            case Occupation.CrossbowTower:
            case Occupation.MagicTower:
            case Occupation.Tower:
            case Occupation.SiegeMachine:
            case Occupation.Ballista:
            case Occupation.Ruler:   // 3.5 P0-4：君主不耗粮
            case Occupation.Vagrant: // 3.5.1 §5.2（E-S1）：王国领域外流浪汉不耗国会粮，不参与王国平均
                return false;
            default:
                return true;
        }
    }

    /// <summary>全体我方 NPC 平均饱食（供 PopulationSystem 生育条件 / 幸福计算）。无 NPC 返回 50。
    /// 2_17 步骤11 批2：玩家桶0——返回值与现状逐位一致（仅玩家口径）。</summary>
    public float GetAverageSatiety()
    {
        if (UnitRegistry.Instance == null) return 50f;
        int count = 0;
        int sum = 0;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.GetFaction() != Faction.PlayerCamp) continue;
            // 2_17 步骤4 关账扫描：仅玩家桶0——AI 工人不吃玩家国库粮/不拉低均饱食（双条件保留兼容存量过渡态）。
            if (unit.kingdomId != 0) continue;   // GetAverageSatiety 平均饱食（玩家口径）
            if (!IsNpc(unit.EffectiveOccupation)) continue;
            sum += unit.Satiety;
            count++;
        }
        return count > 0 ? sum / (float)count : 50f;
    }

    /// <summary>某王国平均饱食（per-kingdom；0=玩家走 GetAverageSatiety() 实时、>0=AI 按 kingdomId 实时算）。</summary>
    public float GetAverageSatiety(int kingdomId)
    {
        if (kingdomId == 0) return GetAverageSatiety();
        if (UnitRegistry.Instance == null) return 50f;
        int count = 0, sum = 0;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.kingdomId != kingdomId) continue;
            if (!IsNpc(unit.EffectiveOccupation)) continue;
            sum += unit.Satiety;
            count++;
        }
        return count > 0 ? sum / (float)count : 50f;
    }

    /// <summary>
    /// 每日饱食结算（DayCycleSettlement 统一入口调用）。
    /// 对全体我方 NPC：尝试进食（消耗国库粮）→ 未进食则衰减 → 应用 0 扣血 / 80+ 回血 / 饥饿降幸福。
    /// 2_17 步骤14 批B（D453/D460）：玩家(id=0)原样零回归；AI Fine 王国逐实体进食（国库源=王国 resources.Food）；
    /// AI Abstract 王国实体冻结跳过逐位结（与 NPCBrain 冻结语义一致），进食由 AbstractEconomySettler 计数公式结算。
    /// </summary>
    public void OnNewDay()
    {
        var cfg = Cfg();
        if (cfg == null || UnitRegistry.Instance == null) return;

        // 收集王国 simMode 快照（结算中途不重查，确定性固定当日档）
        var modes = new Dictionary<int, SimMode>();
        if (KingdomRegistry.Instance != null)
        {
            var all = KingdomRegistry.Instance.GetAll();
            for (int i = 0; i < all.Count; i++) modes[all[i].id] = all[i].simMode;
        }

        // 唤醒拉平（D335/D460）：AI 王国刚从 Abstract 切回 Fine（lastAbstractAvgSatiety>=0）→
        // 首次日结把实体饱食统一拉平到抽象结算均值（确定性无跳变），然后重置标记。
        foreach (var kv in modes)
        {
            if (kv.Key == 0 || kv.Value != SimMode.Fine) continue;
            var k = KingdomRegistry.Instance.Get(kv.Key);
            if (k == null || k.lastAbstractAvgSatiety < 0f) continue;
            float target = k.lastAbstractAvgSatiety;
            k.lastAbstractAvgSatiety = -1f;
            foreach (var unit in UnitRegistry.Instance.GetAllUnits())
            {
                if (unit == null || !unit.IsAlive || unit.kingdomId != kv.Key) continue;
                if (!IsNpc(unit.EffectiveOccupation)) continue;
                unit.Satiety = Mathf.Clamp(Mathf.RoundToInt(target), 0, 100);
            }
            Debug.Log($"[SatietySystem] k{kv.Key} 唤醒拉平：实体饱食 ← 抽象均值 {target}（D335/D460）");
        }

        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            // 关账扫描（2_17 步骤4 + 步骤14 批B 修正）：PlayerCamp=玩家、AiKingdom=AI 王国两类受治平民
            // 都参与每日饱食结算；国库源按 kingdomId 路由（0→RulerController、>0→KingdomState，D453）。
            // 敌阵营/自由态（None/敌族）不参与。AI 单位此前被 PlayerCamp 过滤误跳过（批B 收口修正：D453 死代码）。
            var fac = unit.GetFaction();
            if (fac != Faction.PlayerCamp && fac != Faction.AiKingdom) continue;
            if (!IsNpc(unit.EffectiveOccupation)) continue;
            if (!unit.IsAlive) continue;

            int kid = unit.kingdomId;
            SimMode mode = modes.TryGetValue(kid, out var m) ? m : SimMode.Fine;
            if (mode == SimMode.Abstract) continue;   // Abstract 王国实体冻结，跳过逐位结

            // 国库源：玩家(id=0)=null → RulerController.Food；AI Fine → KingdomState.resources.Food（D453）
            KingdomState k = kid == 0 || KingdomRegistry.Instance == null
                ? null : KingdomRegistry.Instance.Get(kid);
            SettleUnit(unit, cfg, k);
        }

        // 2_17 步骤11 批2 + 步骤14 批B：每王国均饱食写入分桶——玩家桶=玩家实时均值；
        // AI Fine 桶=按 kingdomId 独立实时算；AI Abstract 桶不覆盖（保留 AbstractEconomySettler 写入的公式值）
        if (KingdomRegistry.Instance != null)
        {
            var all = KingdomRegistry.Instance.GetAll();
            for (int i = 0; i < all.Count; i++)
            {
                var k = all[i];
                if (k == null) continue;
                if (k.IsPlayer) { _avgSatiety[k.id] = GetAverageSatiety(); continue; }
                if (modes.TryGetValue(k.id, out var m) && m == SimMode.Abstract) continue;   // Abstract 桶归公式写
                _avgSatiety[k.id] = GetAverageSatiety(k.id);
            }
        }

        Debug.Log($"[SatietySystem] 每日饱食结算完成（>>> 见各单位日志）");
    }

    /// <summary>单个单位每日饱食结算。kingdom=null → 玩家国库源（RulerController）；非空 → AI 王国国库源（D460）。</summary>
    private void SettleUnit(UnitController unit, KingdomConfig cfg, KingdomState kingdom)
    {
        int dailyFoodCost = cfg.GetDailyFoodByOccupation(unit.EffectiveOccupation);

        // 1. 进食（数据层）：饱食不满阈值 且 国库粮足 → 消耗粮恢复饱食
        bool hasFood = kingdom == null
            ? (RulerController.Instance != null && RulerController.Instance.GetResource(ResourceType.Food) >= dailyFoodCost)
            : kingdom.resources.food >= dailyFoodCost;
        bool fed = false;
        if (unit.Satiety < cfg.feedSatietyThreshold && hasFood)
        {
            if (kingdom == null)
                RulerController.Instance.ModifyResource(ResourceType.Food, false, dailyFoodCost);
            else
                kingdom.resources.food -= dailyFoodCost;   // AI 扣本国国库（D453）
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