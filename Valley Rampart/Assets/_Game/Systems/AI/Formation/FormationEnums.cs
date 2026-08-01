using UnityEngine;

// ============================================================================
//  3.0.1_3 AI 协作 - 编队枚举与数据结构
//  详见 3.0.1_3_AI协作.md §八（代码接口契约级）
//  锚点抽象（§1.1）：Anchor = 将军 NPC 或城墙预设点（静态），FormationController 不绑死将军
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

/// <summary>槽位角色约束（§八 SlotDef.role）。</summary>
public enum SlotRole
{
    Any,            // 任意兵种
    MeleeOnly,      // 仅近战（将军 + Warrior）
    RangedOnly,     // 仅弓手（Archer）
    GeneralOnly,    // 仅将军（标识将军槽位，P0 不强制）
}

/// <summary>
/// 槽位定义（§八 SlotDef）。
/// 一字横队 6 槽，每槽相对锚点的 cell 偏移 + 角色约束。
/// </summary>
[System.Serializable]
public struct SlotDef
{
    [Tooltip("相对锚点的 cell 偏移（x=横向，y=0 地面层 / 1 上墙位）")]
    public Vector2Int cellOffset;
    [Tooltip("槽位角色约束")]
    public SlotRole role;
}

// NOTE: FormationDef（ScriptableObject）已拆分到独立文件 FormationDef.cs。
// 根因：原定义在此文件中，Unity 仅生成 m_ClassName=SlotDef 的 MonoScript，
// 导致 FormationDef 资产 Resources.Load 失败（阵型查表失败）。2026-08-01 修复。
