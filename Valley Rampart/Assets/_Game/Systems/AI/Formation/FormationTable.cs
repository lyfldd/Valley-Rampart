using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1_3 AI 协作 - 阵型查找表 SO（§3.6 候选表分级查表）
//  详见 3.0.1_3_AI协作.md §3.6 / §14.4
//  设计期烘焙 Top-N 候选（标安全向/进攻向），运行时按威胁因子分档权重候选内挑
//  P0：手配防守/进攻/守城各一条，直接按意图查表（无分档权重挑选）
//  P1：扩为 Top-N 候选 + ThreatWeightTier SO 分档映射
// ============================================================================

/// <summary>
/// 阵型查找表 SO（§3.6）。
/// key = (Intent, BattleLine, meleeCount, archerCount) → FormationDef
/// P0 简化为按意图直查（手配单阵型）；P1 扩为 Top-N 候选 + 分档权重。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/FormationTable", fileName = "FormationTable")]
public class FormationTable : ScriptableObject
{
    [Header("P0 手配阵型（按意图直查，残编自动压队尾）")]
    [Tooltip("防守意图阵型（满编 3 近 2 弓 + 将军）")]
    public FormationDef defenseFormation;
    [Tooltip("进攻意图阵型（将军带头，弓手殿后）")]
    public FormationDef chargeFormation;
    [Tooltip("撤退意图阵型（近战殿后，弓手先走）")]
    public FormationDef retreatFormation;
    [Tooltip("守城阵型（无将军，弓手上墙 + 近战堵口）")]
    public FormationDef garrisonFormation;

    /// <summary>
    /// 按意图查阵型（P0 简化：单阵型直查；P1 扩为 Top-N 候选 + 分档权重）。
    /// 守城编队（无将军）直接返回 garrisonFormation，不按意图查。
    /// </summary>
    public FormationDef Lookup(TacticIntent intent, BattleLine line, int meleeCount, int archerCount)
    {
        // P0：忽略 line/meleeCount/archerCount，按意图直查
        // 残编由 FormationController 在分配槽位时按 R2 残编紧凑规则压队尾
        switch (intent)
        {
            case TacticIntent.Charge: return chargeFormation;
            case TacticIntent.Retreat: return retreatFormation;
            case TacticIntent.Defense:
            default: return defenseFormation;
        }
    }

    /// <summary>守城编队专用查表（无将军，城墙锚点）</summary>
    public FormationDef LookupGarrison()
    {
        return garrisonFormation;
    }
}
