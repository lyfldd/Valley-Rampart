using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  AI 调试数据类型（3.0.1 附录 A / 3.0.1_2）
//  供 IAIDebugInfo 接口返回，AIDebugController 收集，UI 面板消费。
//  纯数据结构，无逻辑。
// ============================================================================

/// <summary>
/// 焦点/谱系切换历史记录。每次焦点层或谱系变化时由 NPCBrain 记录。
/// UI 面板展示最近 N 次，带时间戳，排查时序用。
/// </summary>
public struct AISwitchRecord
{
    /// <summary>发生时间（Time.time）</summary>
    public readonly float Timestamp;
    /// <summary>切换前焦点</summary>
    public readonly Focus OldFocus;
    /// <summary>切换后焦点</summary>
    public readonly Focus NewFocus;
    /// <summary>切换前谱系</summary>
    public readonly BehaviorSpectrum OldSpectrum;
    /// <summary>切换后谱系</summary>
    public readonly BehaviorSpectrum NewSpectrum;
    /// <summary>变化类型描述（"焦点切换"/"谱系切换"/"焦点+谱系切换"）</summary>
    public readonly string Description;

    public AISwitchRecord(float timestamp, Focus oldFocus, Focus newFocus,
                          BehaviorSpectrum oldSpectrum, BehaviorSpectrum newSpectrum)
    {
        Timestamp = timestamp;
        OldFocus = oldFocus;
        NewFocus = newFocus;
        OldSpectrum = oldSpectrum;
        NewSpectrum = newSpectrum;

        bool focusChanged = !FocusEquals(oldFocus, newFocus);
        bool spectrumChanged = oldSpectrum != newSpectrum;

        if (focusChanged && spectrumChanged)
            Description = "焦点+谱系切换";
        else if (focusChanged)
            Description = "焦点切换";
        else if (spectrumChanged)
            Description = "谱系切换";
        else
            Description = "无变化";

        // 补充焦点详情
        if (focusChanged)
            Description += $": {LayerName(oldFocus.Layer)} -> {LayerName(newFocus.Layer)}";
    }

    public static bool FocusEquals(Focus a, Focus b)
    {
        if (!a.IsValid && !b.IsValid) return true;
        if (a.IsValid != b.IsValid) return false;
        return a.Layer == b.Layer;
    }

    public static string LayerName(AttentionLayer layer)
    {
        switch (layer)
        {
            case AttentionLayer.Threat: return "威胁";
            case AttentionLayer.Hate: return "仇恨";
            case AttentionLayer.Task: return "任务";
            case AttentionLayer.Perception: return "感知";
            case AttentionLayer.Curiosity: return "好奇";
            default: return "无";
        }
    }
}

/// <summary>
/// 刺激源调试信息。供 UI 面板展示注意力排行榜前 N 名。
/// </summary>
public struct StimulusDebugInfo
{
    /// <summary>所属层</summary>
    public readonly AttentionLayer Layer;
    /// <summary>强度（0-100）</summary>
    public readonly float Intensity;
    /// <summary>焦点对应位置</summary>
    public readonly Vector2 Position;
    /// <summary>是否为当前焦点（排行榜第一名）</summary>
    public readonly bool IsFocus;

    public StimulusDebugInfo(AttentionLayer layer, float intensity, Vector2 position, bool isFocus)
    {
        Layer = layer;
        Intensity = intensity;
        Position = position;
        IsFocus = isFocus;
    }
}

/// <summary>
/// AI 调试快照。AIDebugController 每帧收集选中 NPC 的全部 AI 状态，
/// UI 面板只需调用 AIDebugController.GetSnapshot() 一次即可获取所有数据。
/// </summary>
public struct AIDebugSnapshot
{
    /// <summary>是否有选中的 NPC</summary>
    public bool HasSelection;
    /// <summary>NPC 名称</summary>
    public string NPCName;
    /// <summary>NPC 世界位置</summary>
    public Vector2 NPCPosition;
    /// <summary>血量比例 (0-1)</summary>
    public float HPRatio;
    /// <summary>当前焦点</summary>
    public Focus CurrentFocus;
    /// <summary>当前谱系</summary>
    public BehaviorSpectrum CurrentSpectrum;
    /// <summary>当前威胁等级</summary>
    public ThreatLevel CurrentThreatLevel;
    /// <summary>附近敌人数</summary>
    public int NearbyEnemyCount;
    /// <summary>附近友军数</summary>
    public int NearbyAllyCount;
    /// <summary>是否有保护</summary>
    public bool HasProtection;
    /// <summary>是否在安全确认中</summary>
    public bool InSafetyConfirmation;
    /// <summary>是否在受击冷却中</summary>
    public bool IsInHitCooldown;
    /// <summary>刺激源排行榜（前 5）</summary>
    public List<StimulusDebugInfo> TopStimuli;
    /// <summary>最近切换历史（前 5）</summary>
    public List<AISwitchRecord> RecentSwitches;
}
