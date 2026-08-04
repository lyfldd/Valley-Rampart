using UnityEngine;

/// <summary>
/// 弹药行为模板（3.6 §三 介质层，独立资产）。
/// 职业/装备引用；穿透/AOE/弹道/效果全部数据驱动，等级不设上限（解耦原则 1）。
/// ToSnapshot 时由 NpcProfessionDef 拉平进 ProfessionSnapshot。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/AmmoDef", fileName = "AmmoDef")]
public class AmmoDef : ScriptableObject
{
    [Header("基础")]
    public ProjectileType ammoType;

    [Header("穿透（3.6 §5.1）")]
    [Tooltip("穿透等级（int，不设上限）。< 工事防御等级 → 不造成伤害（被挡）")]
    public int pierceLevel = 1;

    [Header("溅射（3.6 §3.3 单段 AOE）")]
    [Tooltip("溅射半径（格）。0=单体")]
    public float aoeRadiusCells = 0f;
    [Tooltip("溅射衰减 0-1：0=均匀满伤，1=线性衰减到边缘0")]
    public float aoeFalloff = 0f;

    [Header("弹道（3.6 §5 抛物线体系）")]
    [Tooltip("弹道类型：弧高 vs 工事高度决定越墙判定")]
    public BallisticType ballisticType = BallisticType.Straight;
    [Tooltip("弹道弧高（格）。> 工事高度 → 越墙命中墙后目标")]
    public float arcHeightCells = 0f;

    [Header("命中效果（可空）")]
    [Tooltip("命中后生成的地面效果（火弹灼烧场/魔弹减速场）")]
    public GroundEffectDef effect;
}
