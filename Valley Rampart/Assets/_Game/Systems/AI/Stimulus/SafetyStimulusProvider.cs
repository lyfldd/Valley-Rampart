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
    /// </summary>
    public SafetyStimulus GetOrUpdate(in FactorContext ctx)
    {
        _stimulus.Position = ctx.HomePoint;
        _stimulus.Intensity = RetreatFormulas.SafetyUrge(
            ctx.Config.baseSafetyPull,
            ctx.NightFactor,
            ctx.Config.nightPullWeight,
            ctx.HpRatio,
            ctx.Config.woundPullWeight,
            ctx.Profession != null ? ctx.Profession.professionPullScale : 1f);
        return _stimulus;
    }

    public void Reset() { /* SafetyStimulus 无状态，无需重置 */ }
}
