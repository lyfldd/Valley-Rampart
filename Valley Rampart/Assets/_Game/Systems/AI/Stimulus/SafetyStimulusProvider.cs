using UnityEngine;

// ============================================================================
//  QQQ.2 T8 / DR-21 - SafetyStimulus 提供者（合并 SafetyScore 公式）
//  详见 QQQ.2_NPC任务修正以及一些小问题.md §需求4.1
//  旧实现：SafetyUrge 旧公式（basePull × 夜晚 × 受伤 × profScale），目标恒 HomePoint。
//  新实现（DR-21 合并 SafetyStimulus/ThreatHysteresis/Caution）：
//   ① SafetyScore < wanderThreshold → 目标 = 最近安全锚点（RetreatToSafeAnchor），强拉力撤入
//   ② 高分态 → 目标 = HomePoint（正常归巢）；Score 越低拉力越强（SafetyScoreFormulas.SafetyPull）
//   ③ 已到达目标 → 强度压 0（归巢驱力消失 → Wander 浮出，城内漫游）
// ============================================================================

/// <summary>
/// SafetyStimulus 提供者（§3.1 + QQQ.2 T8）。
/// 每 tick 算回城/撤退拉力强度 + 更新目标位置，写入池化 SafetyStimulus 实例。
/// NPCBrain 在 ② 阶段调用 GetOrUpdate 入 L1 评分池。
/// </summary>
public class SafetyStimulusProvider
{
    private readonly SafetyStimulus _stimulus = new SafetyStimulus();

    /// <summary>池化 SafetyStimulus 实例（复用不 new）</summary>
    public SafetyStimulus Stimulus => _stimulus;

    /// <summary>
    /// 每 tick 更新拉力强度 + 目标位置，返回池化实例。
    /// </summary>
    public SafetyStimulus GetOrUpdate(in FactorContext ctx)
    {
        bool lowSafety = ctx.SafetyScore < ctx.Config.wanderThreshold;

        // 低安全分 → 撤往最近安全锚点（RetreatToSafeAnchor）；高分 → 正常回城 HomePoint
        _stimulus.Position = (lowSafety && ctx.SafeAnchorPos != Vector2X.zero)
            ? ctx.SafeAnchorPos
            : ctx.HomePoint;

        // 到家/到锚点判定（用距离，不依赖焦点——到了归巢驱力消失）
        bool atTarget = Vector2X.Distance(_stimulus.Position, ctx.SelfPos)
                        <= ctx.Config.arrivalThreshold * ctx.CellSize;
        if (atTarget)
        {
            _stimulus.Intensity = 0f;
        }
        else if (lowSafety)
        {
            // 低分：强拉力撤往安全锚点（保底 0.15 > Wander 0.05，防低分态 Wander 误浮出）
            _stimulus.Intensity = Mathf.Max(0.15f, ctx.Config.baseSafetyPull)
                                  * ctx.Profession.professionPullScale;
        }
        else
        {
            // 高分态：Score 越低回城拉力越强（DR-21 扩展公式，替代旧 SafetyUrge）
            _stimulus.Intensity = SafetyScoreFormulas.SafetyPull(
                ctx.SafetyScore, ctx.Config.baseSafetyPull, ctx.Profession.professionPullScale);
        }
        return _stimulus;
    }

    public void Reset() { /* SafetyStimulus 无状态，无需重置 */ }
}
