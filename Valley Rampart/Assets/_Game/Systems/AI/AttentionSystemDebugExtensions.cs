using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  AI 壳 - AttentionSystem 调试扩展（M1 决策核提取）
//  原 AttentionSystem.GetTopStimuliForDebug 依赖壳 StimulusDebugInfo（AIDebug 系，明确不搬核），
//  迁出为扩展方法，行为与原实现一致（威胁层排行榜前 N，按强度降序）。
// ============================================================================

/// <summary>
/// AttentionSystem 的 AIDebug 扩展（壳侧，StimulusDebugInfo 属 AIDebug 系留壳）。
/// </summary>
public static class AttentionSystemDebugExtensions
{
    /// <summary>
    /// 收集威胁层刺激源，按强度降序排列，供 AI 调试面板展示（3.0.1_2）。
    /// UI 调 AIDebugController.GetSnapshot() -> NPCBrain.GetTopStimuli() -> 本方法。
    /// </summary>
    public static void GetTopStimuliForDebug(this AttentionSystem system, List<StimulusDebugInfo> output, int maxCount)
    {
        output.Clear();
        Focus currentFocus = system.CurrentFocus;

        // 威胁层（复用核内 GetTopThreats：按强度降序 + 截断，与原实现结果一致）
        var buffer = new List<ThreatStimulus>();
        system.GetTopThreats(buffer, maxCount);
        foreach (var s in buffer)
        {
            output.Add(new StimulusDebugInfo(
                AttentionLayer.Threat, s.Intensity, Vector2XUnity.ToUnity(s.Position),
                currentFocus.IsValid && currentFocus.Layer == AttentionLayer.Threat
            ));
        }

        // 按强度降序
        output.Sort((a, b) => b.Intensity.CompareTo(a.Intensity));

        // 截取前 maxCount
        if (output.Count > maxCount)
            output.RemoveRange(maxCount, output.Count - maxCount);
    }
}
