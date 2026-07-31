// ============================================================================
//  3.0.1_2 输入输出决定层 - 受击冷却状态机枚举
//  详见 3.0.1_2_输入输出决定层设计.md §13.3
//  三态：Normal / Caution / Probe（完全恢复 = 回 Normal，无独立 Recovery 态）
// ============================================================================

/// <summary>
/// 受击冷却状态机三态（§13.3）。
/// 完全恢复 = 回 Normal，与 3.0.1_1 §6.1 链路一致，无独立 Recovery 态。
/// </summary>
public enum HitCooldownState
{
    /// <summary>正常：无受击记忆，任务类刺激全强度</summary>
    Normal,
    /// <summary>警戒：刚受击/撤完，注入 HoldPositionStimulus + 任务折扣 -> Idle 涌现（原地不动）</summary>
    Caution,
    /// <summary>试探：敏感度 ×1.5，任务类恢复，可跟上也可再被打</summary>
    Probe
}
