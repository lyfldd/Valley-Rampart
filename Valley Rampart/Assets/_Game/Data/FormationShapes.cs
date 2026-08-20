using UnityEngine;

// ============================================================================
//  2_8 AI 应用层 - 编队 2D 槽位形状参数 SO（步骤4，§5.2 形状定义）
//  详见 2_8_AI应用层_实施计划.md 步骤 4 / §三 FormationShapes 配置
//  形状（线/圆/楔形）的布局形参以格单位浮点承载，对象为同名 SO（ScriptableObject
//  类须放同名文件）。FormationController 按 FormationDef.shape 选择用哪个形参。
//  注：本 SO 只读形状参数；形状本身的"哪种形状"由 FormationDef.shape 决定。
//  2026-08-14 终审：formationMaxWorkersPerTask 已移除（统一归任务派工 SO）。
// ============================================================================

/// <summary>编队 2D 槽位形状参数（§5.2，格单位）。</summary>
[CreateAssetMenu(menuName = "ValleyRampart/FormationShapes", fileName = "FormationShapes")]
public class FormationShapes : ScriptableObject
{
    [Header("编队 2D 槽位形状（格单位）")]
    [Tooltip("线阵槽位间距（格）：槽位沿垂直于朝向量直线展开，近战外沿")]
    public float lineSpacingCells = 1.0f;

    [Tooltip("圆阵最小半径（格）：将军居中，半径 r=ceil(n/2π) 环布，弓内近外；低于此值用此值")]
    public float circleMinRadiusCells = 1.0f;

    [Tooltip("楔形两翼逐排后撤（格）：朝向为轴，两翼逐排后退该值")]
    public float wedgeStepBackCells = 0.7f;
}