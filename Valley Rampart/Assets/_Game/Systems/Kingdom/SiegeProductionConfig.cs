using UnityEngine;

/// <summary>
/// 战争机器生产配置（3.5 §13.7 / 实施计划 P1 步骤7；查表类型 SO）。
/// 投掷机厂生产投掷机/弩炮 + 弹药（普通石/燃烧火油/魔法水晶）。
/// 资产路径：Resources/Config/SiegeProductionConfig.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/SiegeProductionConfig", fileName = "SiegeProductionConfig")]
public class SiegeProductionConfig : ScriptableObject
{
    [Header("投掷机上限（§13.7：上限2，投掷机厂每级+2）")]
    public int siegeMachineLimitBase = 2;      // 基础上限
    public int siegeMachineLimitPerLevel = 2;  // 每级 +2

    [Header("战争机器造价（占位可调）")]
    public ResourcePack catapultCost;   // 投掷机造价
    public ResourcePack ballistaCost;   // 弩炮造价

    [Header("弹药造价（§13.4/§10：普通石×1/燃烧火油×1/魔法水晶×1）")]
    public int stoneAmmoCost = 1;       // 普通弹耗石
    public int fireballAmmoCost = 1;    // 燃烧弹耗火油
    public int magicAmmoCost = 1;       // 魔法弹耗水晶
}