using UnityEngine;

// 2_14 灾害触发生成配置 SO（设计 §2.1/§2.2 + D235~D237）
[CreateAssetMenu(menuName = "ValleyRampart/Disaster/PortalDisasterConfig")]
public class PortalDisasterConfig : ScriptableObject
{
    [Header("触发规则（§2.1）")]
    public float triggerProbability = 0.3f;      // 每晚触发概率
    public int minDaysBeforeFirst = 3;           // 首次触发最小天数（给玩家发展期）
    public int forceTriggerAfterDays = 7;        // 保底天数（连续 N 天未触发则强制触发）

    [Header("生成规则（§2.2）")]
    public int maxPortalPerNight = 1;            // 同夜最多同时存在传送门数
    public int recheckRadius = 10;               // 放置检测半径（格，N 格内无王国建筑）
    public int maxPlacementRetries = 5;          // 随机点不合法重试上限（失败当晚不生成）

    [Header("难度系数（D235~D237）")]
    public float easyTriggerMultiplier = 0.6f;   // Easy 灾害触发概率倍率（低）
    public float normalTriggerMultiplier = 1.0f; // Normal（基准）
    public float hardTriggerMultiplier = 1.4f;   // Hard 灾害触发概率倍率（高）

    [Header("强度曲线（§3.3 + D236/D237）")]
    public float growthRate = 0.02f;             // 天数增长系数（/天）
    public int strengthCap = 60;                 // 单波强度上限（D97）
    public float easyWaveCoefficient = 0.7f;     // Easy 波次强度系数（D236）
    public float normalWaveCoefficient = 1.0f;   // Normal（基准）
    public float hardWaveCoefficient = 1.3f;     // Hard 波次强度系数（D236）

    /// <summary>按难度取触发概率倍率（难度档 1=Easy/2=Normal/3=Hard，对齐 DifficultyManager）。</summary>
    public float GetTriggerMultiplier(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return easyTriggerMultiplier;
            case 3: return hardTriggerMultiplier;
            default: return normalTriggerMultiplier;
        }
    }

    /// <summary>按难度取波次强度系数（D236；难度档 1=Easy/2=Normal/3=Hard）。</summary>
    public float GetWaveCoefficient(int difficulty)
    {
        switch (difficulty)
        {
            case 1: return easyWaveCoefficient;
            case 3: return hardWaveCoefficient;
            default: return normalWaveCoefficient;
        }
    }
}