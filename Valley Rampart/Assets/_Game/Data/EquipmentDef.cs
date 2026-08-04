using System;
using UnityEngine;

/// <summary>
/// 装备属性修正（3.6 §4.3：装备 = 加减修正模型）。
/// 职业提供基础值，装备做加减（重甲 +20血+10防-2攻；盾 +40血+25防-4攻；弩 +6攻+3程+0.9CD+2防）。
/// </summary>
[Serializable]
public struct StatModifier
{
    public int attack;
    public int defense;
    public int maxHp;
    [Tooltip("射程修正（格）")]
    public float attackRange;
    [Tooltip("攻击间隔修正（秒）")]
    public float attackCD;
}

/// <summary>
/// 装备变体（3.6 §4.3，独立资产）。生产建筑（装备厂）按 id 引用，改数值不牵连生产端（解耦原则 3）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/EquipmentDef", fileName = "EquipmentDef")]
public class EquipmentDef : ScriptableObject
{
    [Tooltip("装备 id（装备厂引用；轻剑/重甲/盾/弓/弩/骑枪/坐骑...）")]
    public string id;

    [Tooltip("适用职业（Occupation 精简 6 核心）")]
    public Occupation compatibleWith;

    [Tooltip("属性加减修正（叠加到职业基础值）")]
    public StatModifier modifiers;

    [Tooltip("决定用什么弹药（弓→Arrow，弩→Bolt，骑枪→null 近战冲锋）")]
    public AmmoDef ammo;

    [Tooltip("盾：可挡箭（被动减远程命中，预留）")]
    public bool isShield;
}
