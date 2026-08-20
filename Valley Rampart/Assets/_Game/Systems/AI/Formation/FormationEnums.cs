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
/// 2_8 步骤4 复核（SlotDef 2D y 语义）：y=0 平面层扩散/上墙位仅守城编队用；
/// 2D 化后 x 为沿阵线横向展开方向，y 为沿朝向纵深（前/后），由 FormationShapes
/// 形参 + FormationController.AssignSlots 生成，SlotDef.cellOffset 保留为兼容旧资产。
/// </summary>
[System.Serializable]
public struct SlotDef
{
    [Tooltip("相对锚点的 cell 偏移（x=沿阵线横向，y=沿朝向纵深 0 地面层 / 1 上墙位）")]
    public Vector2Int cellOffset;
    [Tooltip("槽位角色约束")]
    public SlotRole role;
}

/// <summary>
/// 编队 2D 槽位形状（2_8 步骤4，§5.2）。FormationDef.shape 声明用哪种形状，
/// FormShapes SO 提供对应形参。默认 Line 兼容旧资产（未配置字段时自动取枚举首值）。
/// </summary>
public enum FormationShape
{
    Line,    // 线阵：槽位沿垂直于朝向量直线展开，间距 lineSpacingCells（近战外沿）
    Circle,  // 圆阵：将军居中，半径 r=ceil(n/2π) 环布，弓手内环/近战外环
    Wedge,   // 楔形：朝向为轴，两翼逐排后撤 wedgeStepBackCells
}

// NOTE: FormationDef（ScriptableObject）已拆分到独立文件 FormationDef.cs。
// 根因：原定义在此文件中，Unity 仅生成 m_ClassName=SlotDef 的 MonoScript，
// 导致 FormationDef 资产 Resources.Load 失败（阵型查表失败）。2026-08-01 修复。
