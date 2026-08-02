// ============================================================================
//  AI.Core Decision - L1 焦点评分层（从壳 Decision/L1FocusEvaluator.cs 迁入）
//  03_大脑提取与双适配工程.md 迁移 6 步·步3。
//  ⚠️ 适配说明：原签名 Evaluate(AttentionSystem, in FactorContext) 依赖壳 AttentionSystem，
//  asmdef 边界要求核不引用壳类型；且 AttentionSystem 按施工单步 4 才搬入。
//  改为纯函数签名 Evaluate(Focus, IStimulus, in FactorContext)（输入即注意力系统当前产物），
//  壳 NPCBrain 调用处传入 _attention.CurrentFocus / _attention.CurrentStimulus，行为不变。
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
    /// 从注意力系统当前焦点转 FocusDecision。
    /// NPCBrain 在 ③ 阶段 _attention.Update 后调用。
    /// </summary>
    public static FocusDecision Evaluate(Focus focus, IStimulus stimulus, in FactorContext ctx)
    {
        if (!focus.IsValid)
            return FocusDecision.Invalid;

        return new FocusDecision
        {
            // 从注意力系统 CurrentStimulus 取刺激源实例（Focus.Source 存的是业务引用非刺激源本身）
            Focus = stimulus,
            Type = focus.FocusType,
            TargetPos = focus.TargetPos,
            Score = focus.Intensity,
            IsValid = true,
        };
    }
}
