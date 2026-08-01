using UnityEngine;

// ============================================================================
//  3.0.1_4 多因子决策 - 漫游刺激源提供者
//  详见 3.0.1_4_多因子决策与威胁溯源设计.md §6.3
//  每 tick 算 Wander 强度 + 更新漫游中心（HomePoint），写入池化 WanderStimulus 实例。
//  强度 wanderIntensity(0.05) 恒低于 Safety 未到达最低值(~0.10) -> 回城优先；
//  Safety 到达 HomePoint 后强度压 0 -> Wander 自然浮出（城内漫游）。
// ============================================================================

/// <summary>
/// WanderStimulus 提供者（§6.3）。
/// 每 tick 更新漫游中心 + 强度，写入池化 WanderStimulus 实例。
/// NPCBrain 在 ② 阶段调用 GetOrUpdate 入 L1 评分池。
/// </summary>
public class WanderStimulusProvider
{
    private readonly WanderStimulus _stimulus = new WanderStimulus();

    /// <summary>池化 WanderStimulus 实例（复用不 new）</summary>
    public WanderStimulus Stimulus => _stimulus;

    /// <summary>每 tick 更新漫游中心 + 强度，返回池化实例。</summary>
    public WanderStimulus GetOrUpdate(in FactorContext ctx)
    {
        _stimulus.Position = ctx.HomePoint;
        _stimulus.Intensity = ctx.Config != null ? ctx.Config.wanderIntensity : 0.05f;
        return _stimulus;
    }

    public void Reset() { /* WanderStimulus 无状态，无需重置 */ }
}
