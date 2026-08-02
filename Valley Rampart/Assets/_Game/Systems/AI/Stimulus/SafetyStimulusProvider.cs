using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - SafetyStimulus 提供者
//  详见 3.0.1_2_输入输出决定层设计.md §3.1
//  每 tick 算 safetyUrge 并更新池化 SafetyStimulus（零 GC：复用单实例）
// ============================================================================

/// <summary>
/// SafetyStimulus 提供者（§3.1）。
/// 每 tick 算 safetyUrge 强度 + 更新 HomePoint 位置，写入池化 SafetyStimulus 实例。
/// NPCBrain 在 ② 阶段调用 GetOrUpdate 入 L1 评分池。
/// </summary>
public class SafetyStimulusProvider
{
    private readonly SafetyStimulus _stimulus = new SafetyStimulus();

    /// <summary>池化 SafetyStimulus 实例（复用不 new）</summary>
    public SafetyStimulus Stimulus => _stimulus;

    /// <summary>
    /// 每 tick 更新 safetyUrge 强度 + HomePoint 位置，返回池化实例。
    /// 3.0.1_4 §6.3：已到达 HomePoint 时强度压 0（归巢驱力消失）-> Wander 浮出（城内漫游）。
    /// 未到达时强度照常算（夜晚/受伤回城驱力不受影响）。
    /// </summary>
    public SafetyStimulus GetOrUpdate(in FactorContext ctx)
    {
        _stimulus.Position = ctx.HomePoint;

        // 3.0.1_4 §6.3：到家判定用位置距离（不依赖焦点，语义正确——到家了归巢驱力消失）
        // M1 决策核提取：核内位置为 Vector2X，距离用 Vector2X.Distance
        bool atHome = Vector2X.Distance(_stimulus.Position, ctx.SelfPos)
                      <= ctx.Config.arrivalThreshold * ctx.CellSize;
        if (atHome)
        {
            _stimulus.Intensity = 0f;
        }
        else
        {
            _stimulus.Intensity = RetreatFormulas.SafetyUrge(
                ctx.Config.baseSafetyPull,
                ctx.NightFactor,
                ctx.Config.nightPullWeight,
                ctx.HpRatio,
                ctx.Config.woundPullWeight,
                ctx.Profession.professionPullScale);  // ProfessionSnapshot 结构体，恒有效
        }
        return _stimulus;
    }

    public void Reset() { /* SafetyStimulus 无状态，无需重置 */ }
}
