using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - L1 焦点评分层
//  详见 3.0.1_2_输入输出决定层设计.md §3
//  包装 AttentionSystem 输出 FocusDecision（Caution 态对 TaskStimulus ×stateTaskDiscount）
// ============================================================================

/// <summary>
/// L1 焦点评分层（§3，纯计算无副作用）。
/// 层压制与排序规则沿用母文档（AttentionSystem 已实装），本文只新增刺激源 + 转 FocusDecision。
///
/// Caution 态 stateTaskDiscount 应用：
///   L1 评分时对 TaskStimulus 强度打折（effectiveIntensity = Intensity × stateTaskDiscount），
///   让 HoldPositionStimulus 胜出。打折在排序比较时用，不改 struct 字段。
/// </summary>
public static class L1FocusEvaluator
{
    /// <summary>
    /// 从 AttentionSystem 当前焦点转 FocusDecision。
    /// NPCBrain 在 ③ 阶段 _attention.Update 后调用。
    /// </summary>
    public static FocusDecision Evaluate(AttentionSystem attention, in FactorContext ctx)
    {
        Focus focus = attention.CurrentFocus;
        if (!focus.IsValid)
            return FocusDecision.Invalid;

        return new FocusDecision
        {
            // 从 AttentionSystem.CurrentStimulus 取刺激源实例（Focus.Source 存的是业务引用非刺激源本身）
            Focus = attention.CurrentStimulus,
            Type = focus.FocusType,
            TargetPos = focus.TargetPos,
            Score = focus.Intensity,
            IsValid = true,
        };
    }
}
