using UnityEngine;

// ============================================================================
//  3.0.1_3 AI 协作 - 阵型定义 SO（独立文件）
//  详见 3.0.1_3_AI协作.md §八（代码接口契约级）
//  ⚠️ 根因修复（2026-08-01）：原定义在 FormationEnums.cs 中，文件名与类名不匹配，
//     Unity 仅生成 m_ClassName=SlotDef 的 MonoScript，导致 FormationDef 资产在
//     Play Mode / Resources.Load 下无法反序列化（阵型查表失败）。
//     按 Unity 约定（ScriptableObject 类须放在同名文件）拆分到此文件修复。
//  锚点抽象（§1.1）：Anchor = 将军 NPC 或城墙预设点（静态），FormationController 不绑死将军
// ============================================================================

/// <summary>
/// 阵型定义 SO（§八 FormationDef）。
/// 一字横队槽位布局，6 槽满编（1 将军 + 3 近战 + 2 弓手）。
/// 残编时空槽压队尾（§3.2 R2 残编紧凑），按成员实际构成填槽。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/FormationDef", fileName = "FormationDef")]
public class FormationDef : ScriptableObject
{
    [Header("阵型元数据")]
    [Tooltip("阵型名称（调试用）")]
    public string displayName = "新阵型";

    [Tooltip("适配的战术意图（P0 手配单意图，P1 候选表多意图）")]
    public TacticIntent intent;

    [Tooltip("适配的战线形态")]
    public BattleLine battleLine;

    [Header("2_8 步骤4：编队 2D 槽位形状（默认 Line 兼容旧资产；形参在 FormationShapes SO）")]
    [Tooltip("槽位生成形状：线阵（垂直朝向直线展开）/ 圆阵（将军居中环布）/ 楔形（两翼逐排后撤）")]
    public FormationShape shape = FormationShape.Line;

    [Header("槽位布局（一字横队，索引 0=最左，5=最右；2D 化后 AssignSlots 按 shape 生成，此字段留作兼容旧资产）")]
    public SlotDef[] slots = new SlotDef[6];

    /// <summary>标准满编规模（1 将军 + 3 近战 + 2 弓手）</summary>
    public const int StandardSize = 6;
}
