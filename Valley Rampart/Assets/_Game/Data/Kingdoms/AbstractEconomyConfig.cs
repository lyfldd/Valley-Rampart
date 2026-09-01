using UnityEngine;

// ============================================================================
//  抽象经济配置（2_17 步骤14 批B，D460 用户拍板：独立 SO 承载抽象经济参数）
//  与 sim harness/Economy/EconomyConfig.cs 同构对照（D460：经济公式参数不与
//  KingdomBrainConfig 脑参数混桶）；资产路径：_Game/Resources/Config/Kingdoms/
//  AbstractEconomyConfig.asset（对齐 SimModeConfig/KingdomBrainConfig 既有布局）。
//  服务：AbstractEconomySettlement 适配层 LoadParams → 填纯 C# AbstractEconomyParams
//  → AbstractEconomySettler（零 Unity 引用）消费。数值双落：.cs 默认值 + asset 序列化值。
//  字段域 = 镜像 sim EconomyConfig（DailyRate/HeadTaxGold/职业耗粮）+ D400 流失项
//  （居民无粮 N 日-1 转营地 / 战士断粮解散转流民）。
// ============================================================================

/// <summary>
/// 抽象经济配置（2_17 步骤14 批B，D460/D400）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/Kingdoms/AbstractEconomyConfig", fileName = "AbstractEconomyConfig")]
public class AbstractEconomyConfig : ScriptableObject
{
    [Header("每日产量（镜像 sim EconomyConfig：LumberjackDaily/QuarryDaily/MineDaily/FarmDaily）")]
    [Tooltip("伐木场日产量（sim LumberjackDaily=8；Unity 对齐量级可在资产内调）")]
    public float lumberjackDaily = 8f;
    [Tooltip("采石场日产量（sim QuarryDaily=6）")]
    public float quarryDaily = 6f;
    [Tooltip("矿洞日产量（sim MineDaily=4；Ore 非经济资源不入 AI 国库，乘点仍保留 D462）")]
    public float mineDaily = 4f;
    [Tooltip("农田日产量（sim FarmDaily=6）")]
    public float farmDaily = 6f;
    [Tooltip("铁匠铺日产量（石→Metal 就地加工 D200；sim 无对应建筑，Unity 对齐量级）")]
    public float blacksmithDaily = 4f;

    [Header("税收（镜像 sim DailySettle 人头税）")]
    [Tooltip("人头税（金/人口/日；sim HeadTaxGold=0.5）")]
    public float headTaxGold = 0.5f;

    [Header("职业日耗粮（镜像 sim DailyFoodNeed：生活×1 / 士兵×2 / 高耗×3）")]
    [Tooltip("生活职业日耗粮（居民/工人/搬运/小孩）")]
    public int lifeFoodPerDay = 1;
    [Tooltip("士兵日耗粮（Warrior/Archer/Crossbowman/Cavalry/Mage/Healer）")]
    public int soldierFoodPerDay = 2;
    [Tooltip("高耗日耗粮（HeavyWarrior/ShieldGuard/Archmage/Bishop/General）")]
    public int eliteFoodPerDay = 3;

    [Header("D400 抽象态全民流失（2_17 §3.3 D400 追写）")]
    [Tooltip("居民无粮连续 N 日（均饱食=0 且断粮）→ 计数-1 转最近营地流民")]
    public int residentUnfedDaysToLeave = 3;
    [Tooltip("战士断粮（王国无粮）连续 N 日 → 解散转流民计数")]
    public int warriorUnfedDaysToDesert = 5;

    /// <summary>转纯 C# 参数（引擎零 Unity 引用消费；数值=SO 序列化值）。</summary>
    public AbstractEconomyParams ToParams()
    {
        return new AbstractEconomyParams
        {
            LumberjackDaily = lumberjackDaily,
            QuarryDaily = quarryDaily,
            MineDaily = mineDaily,
            FarmDaily = farmDaily,
            BlacksmithDaily = blacksmithDaily,
            HeadTaxGold = headTaxGold,
            LifeFoodPerDay = lifeFoodPerDay,
            SoldierFoodPerDay = soldierFoodPerDay,
            EliteFoodPerDay = eliteFoodPerDay,
            ResidentUnfedDaysToLeave = residentUnfedDaysToLeave,
            WarriorUnfedDaysToDesert = warriorUnfedDaysToDesert
        };
    }

    /// <summary>Resources.Load 加载（Play 运行时统一入口；失败回退 .cs 默认值=数值双落验证口径）。</summary>
    public static AbstractEconomyConfig LoadConfig()
    {
        return Resources.Load<AbstractEconomyConfig>("Config/Kingdoms/AbstractEconomyConfig");
    }
}
