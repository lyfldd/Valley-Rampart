using UnityEngine;

/// <summary>
/// NPC 职业属性配置（SO，3.4 第九节三轨数值之一）。
/// 继承 UnitData 拿基础三件套（attack/defense/maxHp），扩展战斗/注意力/装备字段。
///
/// 三轨数值分住：
///   - NPC 职业属性 -> NpcProfessionDef（本类，继承 UnitData）
///   - 建筑战斗属性 -> BuildingDef.combat（已有 CombatConfig）
///   - 全局伤害规则 -> DamageConfig（独立 SO）
///
/// 详见 3.4_伤害管线设计.md 第九节、决策 27、第十节占位数值表。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/NpcProfessionDef", fileName = "NpcProfessionDef")]
public class NpcProfessionDef : UnitData
{
    [Header("战斗参数")]
    [Tooltip("攻击范围（格数）。近战 1 格，远程 5 格")]
    public float attackRange = 1f;

    [Tooltip("攻击冷却（秒）。内部取整到 tick 倍数")]
    public float attackCD = 1f;

    [Tooltip("是否远程攻击")]
    public bool isRanged = false;

    [Tooltip("弹速（世界单位/秒，远程用）。联调后值 25/s")]
    public float projectileSpeed = 25f;

    [Header("注意力系统（3.0.1）")]
    [Tooltip("感知半径（格数）。士兵远 / 农民近")]
    public float perceptionRadius = 5f;

    [Tooltip("威胁敏感度（越高越容易升级威胁等级）。农民 +x / 士兵 -x")]
    public float threatSensitivity = 1f;

    [Tooltip("勇气值（0-100，基础 50）。高勇气更敢冒险")]
    [Range(0, 100)] public int courage = 50;

    [Tooltip("服从度（0-100，基础 50）。高服从更少抗命")]
    [Range(0, 100)] public int obedience = 50;

    [Tooltip("职业撤退阈值偏移。士兵敢扛 +x / 农民怯 -x")]
    public float retreatThresholdOffset = 0f;

    [Header("装备槽（预留，首版空）")]
    [Tooltip("装备槽数量（头盔/护甲/武器，首版 0）")]
    public int equipmentSlotCount = 0;
}
