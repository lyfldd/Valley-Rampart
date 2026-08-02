using UnityEngine;

// ============================================================================
//  3.0.1_3 AI 协作 - 编队枚举与数据结构
//  详见 3.0.1_3_AI协作.md §八（代码接口契约级）
//  锚点抽象（§1.1）：Anchor = 将军 NPC 或城墙预设点（静态），FormationController 不绑死将军
//  M1 决策核提取：TacticIntent / BattleLine 已迁入 AI.Core/Formation/FormationEnumsCore.cs
//  （FormationDecisionCore.DecideIntent 返回 TacticIntent，asmdef 边界要求）。
// ============================================================================

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
