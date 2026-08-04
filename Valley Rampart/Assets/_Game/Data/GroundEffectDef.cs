using UnityEngine;

/// <summary>
/// 地面效果（3.6 §3.4 介质层）。
/// Burn 灼烧（每 tick 伤害）/ Slow 减速（区域系数）/ Heal 治疗（范围内有限个 maxTargets）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/GroundEffectDef", fileName = "GroundEffectDef")]
public class GroundEffectDef : ScriptableObject
{
    public GroundEffectType type;

    [Tooltip("区域半径（格）")]
    public float radiusCells = 2f;

    [Tooltip("持续时长（秒）")]
    public float duration = 5f;

    [Tooltip("结算间隔（秒，治疗/灼烧频率，训练调）")]
    public float tickInterval = 1f;

    [Tooltip("Burn=每tick伤害 / Slow=减速系数 / Heal=每tick治疗")]
    public float power = 1f;

    [Tooltip("Heal 有限个：区域内最多奶 N 个（0=不限）")]
    public int maxTargets = 0;
}
