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

    [Header("人口系统（数据层先行，§13.5）")]
    public int initialPopulation = 9;       // 开局人口目标（HH.17 决策3 去君主：4 工人 + 5 居民 = 9，§3.3）
    [Header("3.5.1 §3.3 开局人口分布（实体化 E-S3：1 君主由 SpawnMonarch 生成，此处为其余实体）")]
    [Tooltip("开局工人数（基础生产各 1）")]
    public int initialWorkerCount = 4;
    [Tooltip("开局居民数（原废人改名）")]
    public int initialResidentCount = 5;
    [Tooltip("开局实体出生间距（小区块数，城堡两侧交替散布）")]
    public float initialSpawnGapCells = 1f;
    public int birthHappinessThreshold = 60;   // 整体幸福 > 此值才生育
    public int birthSatietyThreshold = 50;     // 平均饱食 > 此值才生育
    public int birthCouplesDivisor = 2;        // 人口/2 = 对数
    public int birthIntervalDays = 5;          // 每对每 5 天 +1 人（旧档占位，已被 birthPairCooldownDays 对齐取代）
    public int birthCooldownDefault = 5;       // 初始冷却天数（占位）
    [Tooltip("3.5 P0-1：单对生育冷却天数（文档 10 天单对冷却）。实体制下为个体配对冷却（lastBirthDay + N <= 当前天 才可再配对）")]
    public int birthPairCooldownDays = 10;     // 3.5 P0-1：单对 10 天冷却
    [Tooltip("3.5.1 §4.2（E-S6）：小孩成长所需天数事件次数（累积 N 次长成居民）")]
    public int childGrowthDayEvents = 2;       // 小孩长大：天数事件 2 次
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

    [Header("时间换算（产率）")]
    [Tooltip("1 天秒数（与 TimeManager 白天120/黄昏30/夜晚30 同步，用于 rate 换算）")]
    public int kingdomSecondsPerDay = 180;        // 1 天秒数

    [Header("卫戍营（§13.3 训练将军限量，可配置）")]
    [Tooltip("将军训练限量（§10 将军限量 2，可配置）")]
    public int generalLimit = 2;                  // 将军限量

    [Header("流浪汉营地（3.5.1 §4.1，E-S7：前期人口来源）")]
    [Tooltip("开局随机生成营地数下限")]
    public int vagrantCampMin = 2;
    [Tooltip("开局随机生成营地数上限")]
    public int vagrantCampMax = 3;
    [Tooltip("营地占位格数（小区块）")]
    public int vagrantCampFootprint = 4;
    [Tooltip("单营地流浪汉上限（刷满停补）")]
    public int campMaxVagrants = 3;
    [Tooltip("单营地初始流浪汉数")]
    public int campInitialVagrants = 2;
    [Tooltip("营地不满时每日补员数")]
    public int campDailyRefill = 2;
    [Tooltip("流浪汉与营地关联半径（格）：半径内计入该营地人口")]
    public float campVagrantRadiusCells = 4f;
    [Tooltip("招募流浪汉粮耗（决策13：花 1 粮招募即变居民）")]
    public int recruitFoodCost = 1;
    [Tooltip("招募走回抵达半径（格）：到王国锚点此范围内正式纳入人口")]
    public float recruitArriveRadiusCells = 3f;

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
}