using System.Collections.Generic;

// ============================================================================
//  3.0.1_2 输入输出决定层 - class 刺激源池
//  详见 3.0.1_2_输入输出决定层设计.md §9 零 GC 纪律
//  SafetyStimulus/HoldPositionStimulus 每 NPC 1 实例复用，不每 tick new
// ============================================================================

/// <summary>
/// class 刺激源池（§9 零 GC 落地纪律）。
/// 每个组件内部持有长期实例复用、返回缓存列表（不每 tick new）。
/// IStimulus struct 实现有装箱风险，建议 class 池化（本项目的动态刺激源均为 class）。
/// </summary>
public static class StimulusPool
{
    /// <summary>共享空列表，GetActiveStimuli 无注入时返回此（避免每 NPC 各持一份空列表）</summary>
    public static readonly IReadOnlyList<IStimulus> Empty = new IStimulus[0];
}
