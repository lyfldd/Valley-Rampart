using System;
using UnityEngine;

/// <summary>
/// 模块树（3.5 §2.1 解耦：等级不限数量）。
/// 主城（树根）→ 模块（6 棵独立树）→ 等级节点（不限数量）→ 建筑解锁。
/// 加档 = 给 tiers 加节点，不牵连其他模块/文档（解耦原则 1）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/ModuleDef", fileName = "ModuleDef")]
public class ModuleDef : ScriptableObject
{
    [Tooltip("模块标识：土木/生产/民生/军事/商业/科技")]
    public string moduleId;

    [Tooltip("等级节点树（初始 3 节点，可自由扩展）")]
    public ModuleTierDef[] tiers;
}

/// <summary>模块等级节点（3.5 §2.1）。</summary>
[Serializable]
public class ModuleTierDef
{
    [Tooltip("等级（1/2/3/...）")]
    public int tier;

    [Tooltip("主城解锁门槛（每节点独立，可跨级）")]
    public int requiredCastleLevel;

    [Tooltip("该级可升级到的基础建筑 id（建筑等级 ≤ 节点级）")]
    public string[] upgradeBuildings;

    [Tooltip("该级新解锁的特殊建筑 id")]
    public string[] unlockBuildings;
}
