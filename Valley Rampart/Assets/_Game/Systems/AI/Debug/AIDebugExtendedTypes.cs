using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - AI 调试扩展类型（V3）
//  详见 3.0.1_2_输入输出决定层设计.md §11 P0 第9项 / 附录 B.6
//  三层中间结果 + 记忆组件状态，供调试面板实时显示
// ============================================================================

/// <summary>
/// AI 调试信息 V3 接口（3.0.1_2，扩展 IAIDebugInfoExtended）。
/// 暴露三层裁决管线中间结果 + 记忆组件状态，供调试面板"看着因果拖"调参（附录 B.6）。
/// </summary>
public interface IAIDebugInfoV3 : IAIDebugInfoExtended
{
    /// <summary>L1 焦点评分输出</summary>
    FocusDecision DebugFocusDecision { get; }
    /// <summary>L2 姿态裁决输出</summary>
    PostureDecision DebugPostureDecision { get; }
    /// <summary>L3 参数计算输出</summary>
    BehaviorCommand DebugCommand { get; }
    /// <summary>受击冷却状态机当前态</summary>
    HitCooldownState DebugHitCooldownState { get; }
    /// <summary>受击次数</summary>
    int DebugHitCount { get; }
    /// <summary>上一帧 rawFactor（量化器输入）</summary>
    float DebugLastRaw { get; }
    /// <summary>归巢吸引强度（SafetyStimulus.Intensity）</summary>
    float DebugSafetyUrge { get; }
    /// <summary>HomePoint 安全点位置</summary>
    Vector2 DebugHomePoint { get; }
}

/// <summary>
/// AI 调试快照扩展（3.0.1_2 V3 字段）。
/// AIDebugSnapshot 的 V3 扩展部分，AIDebugController 检测 IAIDebugInfoV3 后收集。
/// </summary>
public struct AIDebugSnapshotV3
{
    /// <summary>L1 焦点评分输出</summary>
    public FocusDecision FocusDecision;
    /// <summary>L2 姿态裁决输出</summary>
    public PostureDecision PostureDecision;
    /// <summary>L3 参数计算输出</summary>
    public BehaviorCommand Command;
    /// <summary>受击冷却状态机当前态</summary>
    public HitCooldownState HitCooldownState;
    /// <summary>受击次数</summary>
    public int HitCount;
    /// <summary>上一帧 rawFactor</summary>
    public float LastRaw;
    /// <summary>归巢吸引强度</summary>
    public float SafetyUrge;
    /// <summary>HomePoint 位置</summary>
    public Vector2 HomePoint;
    /// <summary>是否有效（NPCBrain 实现 IAIDebugInfoV3 时为 true）</summary>
    public bool IsValid;
}
