// ============================================================================
//  AI.Core Decision - 焦点类型枚举（从壳 Decision/FocusType.cs 迁入）
//  03_大脑提取与双适配工程.md 迁移 6 步·步3。命名空间不变（全局）。
// ============================================================================

/// <summary>
/// IStimulus 焦点类型，供 L2 三维裁决表查表（§4.2）。
/// 决定"到达焦点目标后做什么"的分支依据。
/// </summary>
public enum FocusType
{
    /// <summary>锚点型（FollowStimulus）：永不到达，持续跟随</summary>
    Anchor,
    /// <summary>位置型（HoldPosition/通用）：到达后 Idle</summary>
    Position,
    /// <summary>工作类位置（TaskStimulus 砍树/挖矿）：到达后 WorkAt</summary>
    WorkPosition,
    /// <summary>驻留类位置（SafetyStimulus HomePoint）：到达后 Idle</summary>
    HomePosition,
    /// <summary>漫游型（WanderStimulus 3.0.1_4 §6.3）：L2 选 Wander 模块，Executor 持续取点循环</summary>
    Wander
}
