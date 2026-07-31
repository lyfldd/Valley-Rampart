using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 威胁评定结果（3.0.1 第五节）。
/// 包含威胁等级 + 保护因子 + 调试用数据。
/// </summary>
public struct ThreatAssessmentResult
{
    /// <summary>最终威胁等级 0-3</summary>
    public ThreatLevel Level;
    /// <summary>原始威胁因子 X（0-1），调试用</summary>
    public float RawFactor;
    /// <summary>是否有友军保护（≥protectionFriendThreshold 个友军在附近）</summary>
    public bool HasProtection;
    /// <summary>附近敌人数</summary>
    public int NearbyEnemyCount;
    /// <summary>附近友军数</summary>
    public int NearbyAllyCount;
}

/// <summary>
/// 威胁评定函数（3.0.1 第五节）。
/// 多因子评定子系统，机制乙的核心计算。
///
/// 3.0.1_2 修订：滞回部分由 ThreatHysteresisComponent（HysteresisQuantizer）接管，
/// 本类仅保留 CalculateRawFactor 静态方法（rawFactor 计算，L3 复用）。
/// 原 Update/DetermineLevelWithHysteresis/ApplyHysteresis 已退役为兼容空壳。
/// </summary>
public class ThreatAssessor
{
    /// <summary>当前威胁等级（3.0.1_2 后由 ThreatHysteresisComponent 持有，本字段仅兼容旧调用）</summary>
    public ThreatLevel CurrentLevel => ThreatLevel.None;

    /// <summary>标记自身被攻击（3.0.1_2 后由 HitCooldownStateMachine 接管，空壳兼容）</summary>
    public void OnDamaged(float currentTime) { }

    /// <summary>更新（3.0.1_2 后退役为兼容空壳，威胁等级由 ThreatHysteresisComponent 计算）</summary>
    public ThreatAssessmentResult Update(
        float rawFactor, int nearbyEnemyCount, int nearbyAllyCount,
        float hpRatio, bool isNight, AttentionTuningConfig config,
        NpcProfessionDef profession, float currentTime)
    {
        bool hasProtection = nearbyAllyCount >= config.protectionFriendThreshold;
        return new ThreatAssessmentResult
        {
            Level = ThreatLevel.None, RawFactor = rawFactor, HasProtection = hasProtection,
            NearbyEnemyCount = nearbyEnemyCount, NearbyAllyCount = nearbyAllyCount
        };
    }

    /// <summary>重置状态（对象池复用时调，3.0.1_2 后空实现）</summary>
    public void Reset() { }

    /// <summary>
    /// 计算原始威胁因子 X（0-1）。3.0.1_2 保留复用，L3 调用。
    /// 因子清单：敌人距离 / 敌人数量 / 血量 / 友军保护 / 时间（昼夜）。
    /// </summary>
    public static float CalculateRawFactor(
        float nearestEnemyDist,
        int enemyCount,
        float hpRatio,
        int allyCount,
        bool isNight,
        NpcProfessionDef profession,
        AttentionTuningConfig config,
        float perceptionWorldRadius,
        float attackWorldRange)
    {
        if (enemyCount == 0 || nearestEnemyDist >= perceptionWorldRadius)
            return 0f;

        // 敌人距离因子（越近越高，0-1）
        float distFactor = 1f - Mathf.Clamp01(nearestEnemyDist / perceptionWorldRadius);

        // 敌人数量因子（越多越高，0-1，5 个满）
        float countFactor = Mathf.Clamp01(enemyCount / 5f);

        // 血量因子（越低越高，0-1）
        float hpFactor = 1f - Mathf.Clamp01(hpRatio);

        // 友军保护因子（越多友军越低，0-1）
        float allyFactor = 1f - Mathf.Clamp01((float)allyCount / config.protectionFriendThreshold);

        // 时间因子（夜晚 +0.1）
        float timeFactor = isNight ? 0.1f : 0f;

        // 加权合成
        float x = distFactor * 0.35f
                + countFactor * 0.15f
                + hpFactor * 0.2f
                + allyFactor * 0.2f
                + timeFactor * 0.1f;

        // 应用职业敏感度
        x *= profession.threatSensitivity;

        return Mathf.Clamp01(x);
    }
}
