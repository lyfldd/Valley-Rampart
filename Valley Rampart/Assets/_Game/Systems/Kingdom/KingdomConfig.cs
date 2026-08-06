using System;
using UnityEngine;

/// <summary>
/// 王国经营全局数值配置（3.5 §21 占用表 / §13.14 数值平衡）。
/// 所有影响王国行为的可调数值全部集中于此 SO，禁止硬编码魔法数值（so-data-driven 铁律）。
/// 资产路径：Resources/Config/KingdomConfig.asset，Play Mode 用 Resources.Load 加载。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/KingdomConfig", fileName = "KingdomConfig")]
public class KingdomConfig : ScriptableObject
{
    [Header("主城升级消耗（索引 0=Lv1 修复，1=Lv2 ... 5=Lv6；§2.1 ÷5）")]
    public ResourcePack[] castleUpgradeCosts;

    [Header("商人贸易梯度额度（索引 = 资源等级-1；§7 贸易）")]
    public TradeQuotaDef[] merchantQuotas;

    [Header("人口系统（数据层先行，§13.5）")]
    public int initialPopulation = 10;       // 开局人口（1 君主 + 4 工人 + 5 废人）
    public int birthHappinessThreshold = 60;   // 整体幸福 > 此值才生育
    public int birthSatietyThreshold = 50;     // 平均饱食 > 此值才生育
    public int birthCouplesDivisor = 2;        // 人口/2 = 对数
    public int birthIntervalDays = 5;          // 每对每 5 天 +1 人（旧档占位，已被 birthPairCooldownDays 对齐取代）
    public int birthCooldownDefault = 5;       // 初始冷却天数（占位）
    [Tooltip("3.5 P0-1：单对生育冷却天数（文档 10 天单对冷却）。计数制下作为全局生育冷却倒计时对齐")]
    public int birthPairCooldownDays = 10;     // 3.5 P0-1：单对 10 天冷却
    [Tooltip("3.5 P0-1：NPC 死亡整体幸福跌幅系数 K（avgHappiness ×= (1 - K/当前人口)），防雪崩")]
    public float deathHappinessK = 0.5f;       // 3.5 P0-1：死亡扣幸福 K=0.5

    [Header("每日耗粮（饱食结算，占位；§10）")]
    public int unemployedDailyFood = 1;        // 无职业废人/工人日耗粮
    public int soldierDailyFood = 2;           // 士兵日耗粮
    public int generalDailyFood = 3;           // 将军日耗粮
    public int mageHealerDailyFood = 2;        // 法师/治疗师日耗粮（§10）
    public int eliteDailyFood = 3;             // 盾卫/大法师/主教日耗粮（§10）

    [Header("饱食度系统（§四/§10，占位可调）")]
    public int satietyStart = 80;              // 新建单位初始饱食
    public int satietyDecayPerDay = 15;        // 未进食每日饱食衰减
    public int satietyRegenThreshold = 80;     // 饱食 ≥ 此值缓慢回血
    public int satietyRegenPerDay = 8;         // 回血速度（/日）
    public int satietyHurtThreshold = 0;       // 饱食 ≤ 此值持续扣血
    public int satietyHurtPerDay = 6;          // 饥饿扣血（/日）
    public int hungerHappinessThreshold = 30;  // 饱食 < 此值 幸福下降
    public int hungerHappinessPenalty = 2;     // 长期不满足每日幸福下降
    public int feedSatietyThreshold = 80;      // 饱食 < 此值才进食（避免浪费）
    public int foodRestoreGrain = 5;           // 粮饱食恢复 +5
    public int foodRestoreSpecial = 8;         // 特殊食物饱食恢复 +8
    public int foodRestoreMeat = 20;           // 肉饱食恢复 +20
    public int foodHappinessGrain = 0;         // 粮幸福加成
    public int foodHappinessSpecial = 1;       // 特殊食物幸福 +1
    public int foodHappinessMeat = 3;          // 肉幸福 +3

    [Header("税收系统（§六，占位）")]
    public float headTaxPerPerson = 0.5f;      // 人头税（金/人口/日，§10 0.5）
    public float buildingTaxRate = 0.10f;      // 建筑税（商业建筑交易额 10%）
    public int commercialTaxPerLevel = 1;      // 商业建筑（市场/商店）每级每日建筑税基数（无交易额统计时的等级抽成）
    public float lowHappinessTaxFloor = 0.5f;  // 幸福 0 时税收保底系数（§六 幸福0收0.5倍）

    [Header("贸易系统（§七，不对称兑换）")]
    public int foodToGoldIn = 4;               // 4 粮 → 1 金
    public int goldToFoodOut = 3;              // 1 金 → 3 粮
    [Tooltip("各资源等级兑换率（索引=资源等级-1，9 档）：卖出时多少单位换 1 金（粮4，越高级越少）")]
    public int[] tradeSellUnitsPerGold;        // 卖出：单位/金
    [Tooltip("各资源等级兑换率（买入）：1 金换多少单位（粮3）")]
    public int[] tradeBuyUnitsPerGold;         // 买入：单位/金
    [Tooltip("市场等级可交易最高资源档位（索引=市场等级-1；§3.5 商业 市场 Lv1 粮↔金 / Lv2 解锁水晶火油 / Lv3 全开）")]
    public int[] marketMaxTradeLevel = { 4, 6, 9 };

    [Header("幸福度系统（§五，多因素权重占位）")]
    public float happinessSatietyWeight = 0.4f;   // 饱食满足权重
    public float happinessHouseWeight = 0.2f;     // 有房住权重
    public float happinessChurchWeight = 0.15f;   // 教堂权重
    public float happinessHospitalWeight = 0.15f; // 医院权重
    public float happinessTaxWeight = 0.1f;       // 税负权重（税高幸福低）
    public float happinessFoodQualityWeight = 0.1f;// 食品品质权重
    public int happinessBase = 50;                // 基础幸福
    public int happinessSatietyBonusMax = 30;     // 饱食满足最高加成
    public int happinessTaxPenaltyMax = 20;       // 税负最高惩罚

    [Header("时间加速（步骤7，1x/2x/4x）")]
    public float[] timeScales;                 // 支持的倍速档位，如 {1,2,4}

    [Header("矿洞副产（步骤6，占位；§10 每5矿1）")]
    public int byproductOrePerUnit = 5;        // 每 N 矿副产 1
    public int byproductCrystalCapacity = 20;  // 水晶副产本地存储上限
    public int byproductFireOilCapacity = 20;  // 火油副产本地存储上限

    [Header("牧场养殖（§13.10，占位）")]
    public int ranchCapacity = 10;                // 牧场容量（动物总数上限）
    public int ranchFeedPerAnimal = 1;            // 每动物每日喂粮
    public int ranchMeatHappiness = 3;            // 肉幸福加成（复用 foodHappinessMeat）

    [Header("金矿（§13.3 直接产金入国库，SO 可调）")]
    [Tooltip("金矿每日产金数（金=货币不占存储，直接进国库 RulerController.Gold）")]
    public int goldMineGoldPerDay = 2;            // 金矿每日产金
    [Tooltip("1 天秒数（与 TimeManager 白天120/黄昏30/夜晚30 同步，用于 rate 换算）")]
    public int kingdomSecondsPerDay = 180;        // 1 天秒数

    [Header("卫戍营（§13.3 训练将军限量，可配置）")]
    [Tooltip("将军训练限量（§10 将军限量 2，可配置）")]
    public int generalLimit = 2;                  // 将军限量

    [Header("医院（§13.3 受伤恢复 + 幸福）")]
    [Tooltip("医院存在时，每日饱食回血额外加成（受伤单位恢复加快）")]
    public int hospitalRecoveryBonus = 5;         // 医院每日额外回血

    /// <summary>按职业获取日耗粮（§10 占位）。</summary>
    public int GetDailyFoodByOccupation(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.General: return generalDailyFood;
            case Occupation.Cavalry: return generalDailyFood;
            case Occupation.Warrior:
            case Occupation.Archer:
            case Occupation.Crossbowman:
            case Occupation.HeavyWarrior:
            case Occupation.SiegeMachine:
            case Occupation.Ballista:
            case Occupation.Tower:
            case Occupation.ArrowTower:
            case Occupation.CrossbowTower:
            case Occupation.MagicTower:
                return soldierDailyFood;
            case Occupation.Mage:
            case Occupation.Healer:
                return mageHealerDailyFood;
            case Occupation.ShieldGuard:
            case Occupation.Archmage:
            case Occupation.Bishop:
                return eliteDailyFood;
            default:
                return unemployedDailyFood;   // 废人/工人/搬运工/平民
        }
    }

    /// <summary>获取主城第 castleLevel 级升级消耗（1..6；越界返回 Zero）。</summary>
    public ResourcePack GetCastleUpgradeCost(int castleLevel)
    {
        if (castleLevel < 1 || castleLevel > 6) return ResourcePack.Zero;
        if (castleUpgradeCosts == null || castleLevel - 1 >= castleUpgradeCosts.Length) return ResourcePack.Zero;
        return castleUpgradeCosts[castleLevel - 1];
    }

    /// <summary>获取资源等级（1..9）对应的贸易额度配置。</summary>
    public TradeQuotaDef GetQuota(int resourceLevel)
    {
        if (resourceLevel < 1 || resourceLevel > 9) return TradeQuotaDef.Zero;
        if (merchantQuotas == null || resourceLevel - 1 >= merchantQuotas.Length) return TradeQuotaDef.Zero;
        return merchantQuotas[resourceLevel - 1];
    }

    /// <summary>卖出兑换率：资源等级 → 多少单位换 1 金（默认粮 4）。</summary>
    public int GetTradeSellRate(int resourceLevel)
    {
        if (tradeSellUnitsPerGold == null || resourceLevel < 1 || resourceLevel - 1 >= tradeSellUnitsPerGold.Length)
            return foodToGoldIn;
        return Mathf.Max(1, tradeSellUnitsPerGold[resourceLevel - 1]);
    }

    /// <summary>买入兑换率：资源等级 → 1 金换多少单位（默认粮 3）。</summary>
    public int GetTradeBuyRate(int resourceLevel)
    {
        if (tradeBuyUnitsPerGold == null || resourceLevel < 1 || resourceLevel - 1 >= tradeBuyUnitsPerGold.Length)
            return goldToFoodOut;
        return Mathf.Max(1, tradeBuyUnitsPerGold[resourceLevel - 1]);
    }
}

/// <summary>商人贸易额度条目（资源等级 → 每次额度 + 刷新周期天数）。</summary>
[Serializable]
public struct TradeQuotaDef
{
    public int resourceLevel;   // 1粮/2木/3石/4矿/5金/6水晶/7火油
    public int amountPerCycle;  // 每次额度
    public int refreshDays;     // 刷新周期（天）

    public static TradeQuotaDef Zero => new TradeQuotaDef { resourceLevel = 0, amountPerCycle = 0, refreshDays = 0 };
}