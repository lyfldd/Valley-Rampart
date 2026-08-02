// ============================================================================
//  AI.Core Formation - 编队枚举（TacticIntent/BattleLine 从壳 FormationEnums.cs 迁入）
//  03_大脑提取与双适配工程.md 迁移 6 步·步5。
//  FormationDecisionCore.DecideIntent 返回 TacticIntent，asmdef 边界要求枚举入核。
//  SlotRole/SlotDef 留壳（SlotDef.cellOffset 用 UnityEngine.Vector2Int）。
// ============================================================================

/// <summary>
/// 战术意图（§3.6 三元组之一）。
/// 意图=评分权重集（IntentWeights SO），驱动阵型评分加权 + 行为参数（§14.1 IntentBehaviorProfile）。
/// P0 手配单阵型，不切换；P1 接 ThreatHeat 方向分布 + 君主军令切换。
/// </summary>
public enum TacticIntent
{
    Defense,    // 防守：将军居中、弓手靠后加分（默认，夜晚守城）
    Charge,     // 冲锋：将军靠前加分（进攻推进，将军带头）
    Retreat     // 撤退：近战殿后、弓手先走加分
}

/// <summary>
/// 战线形态（§3.6 三元组之一，由 ThreatHeat 方向分布判定）。
/// P0 单线手配；P1 接 RegionHeatChangedEvent 判定双线分兵。
/// </summary>
public enum BattleLine
{
    Single,     // 单线：全队一字横队
    Double      // 双线：分兵两侧，将军归威胁大边
}
