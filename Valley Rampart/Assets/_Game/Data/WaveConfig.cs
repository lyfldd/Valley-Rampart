using UnityEngine;

/// <summary>
/// 波次占位数值表（2_8 §三 / 2_8_AI应用层 §5.3，SO 可配）。
/// 波次参数复用自 2D 敌袭（D97）：少波次高强度，偶发传送门灾害（2_14）在此配灾害触发概率/保底。
/// 消费方：<see cref="WaveDirector"/>（Systems/AI/WaveDirector.cs）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/WaveConfig", fileName = "WaveConfig")]
public class WaveConfig : ScriptableObject
{
    [Header("波次数（D97 少波次）")]
    [Tooltip("基础波数")]
    public int baseWaves = 2;

    [Tooltip("每难度档位附加波数（Easy=1 → Easy 3 / Normal 4 / Hard 5）")]
    public int wavePerDifficulty = 1;

    [Header("波次规模（单波强度）")]
    [Tooltip("首波规模")]
    public int strengthBase = 5;

    [Tooltip("随天数增长的规模增幅")]
    public float strengthGrowthPerDay = 1.5f;

    [Tooltip("单波规模上限（高强度）")]
    public int strengthCap = 60;

    [Header("方向聚合（R1）")]
    [Tooltip("同波次刷点方向聚合成一组的最大夹角（度）")]
    public float directionMergeAngle = 45f;

    [Tooltip("组内单位错峰出生间隔（秒），取 x~y 区间")]
    public Vector2 spawnIntervalRange = new Vector2(2f, 5f);

    [Header("灾害触发（2_14 §2.1 偶发）")]
    [Tooltip("每晚灾害触发概率")]
    public float disasterProbPerNight = 0.3f;

    [Tooltip("天数保底触发：超过该天数未触发则强制灾害")]
    public int disasterEveryNDays = 3;

    [Tooltip("连续 N 天未触发强制（防长草）")]
    public int disasterGuaranteeNDays = 3;
}