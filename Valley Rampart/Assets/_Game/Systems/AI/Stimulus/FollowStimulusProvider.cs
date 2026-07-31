using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - FollowStimulus 提供者
//  详见 3.0.1_2_输入输出决定层设计.md §3.2
//  锚点绑定 + 位置每 tick 刷新（零 GC：复用单实例）
// ============================================================================

/// <summary>
/// FollowStimulus 提供者（§3.2）。
/// 外部 SetFollowAnchor 绑定锚点（部队队长），每 tick 刷新位置 + 算强度。
/// NPCBrain 在 ② 阶段调用 Refresh 入 L1 评分池。
/// </summary>
public class FollowStimulusProvider
{
    private readonly FollowStimulus _stimulus = new FollowStimulus();

    /// <summary>池化 FollowStimulus 实例（复用不 new）</summary>
    public FollowStimulus Stimulus => _stimulus;

    /// <summary>是否激活（有锚点绑定）</summary>
    public bool IsActive => _stimulus.Anchor != null;

    /// <summary>绑定跟随锚点（调度中心/军令下发时调）</summary>
    public void SetFollowAnchor(UnitController anchor, TaskPriority priority, float intensity)
    {
        _stimulus.Anchor = anchor;
        _stimulus.Priority = priority;
        _stimulus.Intensity = intensity;
    }

    /// <summary>清除跟随锚点（部队解散/任务完成时调）</summary>
    public void ClearAnchor()
    {
        _stimulus.Anchor = null;
    }

    /// <summary>
    /// 每 tick 刷新锚点位置（位置随锚点移动更新）。
    /// 强度不变（下发时定），位置每 tick 从 Anchor.transform.position 读。
    /// </summary>
    public FollowStimulus Refresh(in FactorContext ctx)
    {
        // FollowStimulus.Position 已通过 Anchor 属性动态返回，无需写 Position
        // 但 intensity 可按任务优先级权重刷新（保持与 AttentionSystem 评分一致）
        return _stimulus;
    }

    public void Reset()
    {
        _stimulus.Anchor = null;
    }
}
