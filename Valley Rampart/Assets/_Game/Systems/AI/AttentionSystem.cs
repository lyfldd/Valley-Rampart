using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 机制甲·五层注意力系统（3.0.1 第三节）。
///
/// 职责：
///   - 维护各层刺激源列表
///   - 持续评分排序，维护强度排行榜
///   - 跨层层压制：高层任何活跃项 > 低层所有项
///   - 输出第一名作为候选焦点
///   - 仅当第一名变化时才通知机制乙
///
/// 每层内部按 intensity 排序，层间按 AttentionLayer 枚举顺序压制。
/// </summary>
public class AttentionSystem
{
    private readonly List<ThreatStimulus> _threatStimuli = new List<ThreatStimulus>();
    private readonly List<TaskStimulus> _taskStimuli = new List<TaskStimulus>();
    private readonly List<PerceptionStimulus> _perceptionStimuli = new List<PerceptionStimulus>();

    private Focus _currentFocus;
    private bool _focusChanged;

    /// <summary>当前焦点（排行榜第一名）</summary>
    public Focus CurrentFocus => _currentFocus;

    /// <summary>本轮更新焦点是否变化（机制乙据此决定切换判断 or 强度调节）</summary>
    public bool FocusChanged => _focusChanged;

    // ===== 添加刺激源 =====

    public void AddStimulus(ThreatStimulus s) => _threatStimuli.Add(s);
    public void AddStimulus(TaskStimulus s) => _taskStimuli.Add(s);
    public void AddStimulus(PerceptionStimulus s) => _perceptionStimuli.Add(s);

    // ===== 移除刺激源 =====

    /// <summary>移除指定来源的所有威胁刺激源（敌人死亡/离开时调）。</summary>
    public void RemoveThreatStimuli(object source)
    {
        if (source == null) return;
        for (int i = _threatStimuli.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_threatStimuli[i].Source, source) ||
                ReferenceEquals(_threatStimuli[i].Enemy, source))
            {
                _threatStimuli.RemoveAt(i);
            }
        }
    }

    /// <summary>移除指定来源的所有任务刺激源。</summary>
    public void RemoveTaskStimuli(object source)
    {
        if (source == null) return;
        for (int i = _taskStimuli.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_taskStimuli[i].Source, source))
            {
                _taskStimuli.RemoveAt(i);
            }
        }
    }

    /// <summary>清空所有威胁刺激源。</summary>
    public void ClearThreats() => _threatStimuli.Clear();

    /// <summary>清空所有刺激源。</summary>
    public void ClearAll()
    {
        _threatStimuli.Clear();
        _taskStimuli.Clear();
        _perceptionStimuli.Clear();
        _currentFocus = Focus.Invalid;
    }

    // ===== 更新（持续评分）=====

    /// <summary>
    /// 更新注意力系统：清理过期刺激源，重新评分排序，输出焦点。
    /// 由 NPCBrain 每隔思考间隔调用。
    /// </summary>
    public void Update(float currentTime)
    {
        // 1. 清理过期刺激源
        RemoveExpired(_threatStimuli, currentTime);
        RemoveExpired(_taskStimuli, currentTime);
        RemoveExpired(_perceptionStimuli, currentTime);

        // 2. 评分排序，选出焦点
        Focus newFocus = SelectTopFocus();

        // 3. 检测焦点是否变化
        _focusChanged = !FocusEquals(_currentFocus, newFocus);
        _currentFocus = newFocus;
    }

    /// <summary>选出排行榜第一名（跨层层压制）。</summary>
    private Focus SelectTopFocus()
    {
        // 第 1 层：威胁（最高优先）
        if (_threatStimuli.Count > 0)
        {
            var top = GetTopThreat();
            return new Focus(AttentionLayer.Threat, top.Position, top.Intensity, top.Source);
        }

        // 第 2 层：仇恨（首版留壳，无刺激源）

        // 第 3 层：任务
        if (_taskStimuli.Count > 0)
        {
            var top = GetTopTask();
            return new Focus(AttentionLayer.Task, top.Position, top.Intensity, top.Source);
        }

        // 第 4 层：感知
        if (_perceptionStimuli.Count > 0)
        {
            var top = GetTopPerception();
            return new Focus(AttentionLayer.Perception, top.Position, top.Intensity, top.Source);
        }

        // 第 5 层：好奇（首版留壳，无刺激源）

        return Focus.Invalid;
    }

    private ThreatStimulus GetTopThreat()
    {
        int best = 0;
        for (int i = 1; i < _threatStimuli.Count; i++)
        {
            if (_threatStimuli[i].Intensity > _threatStimuli[best].Intensity)
                best = i;
        }
        return _threatStimuli[best];
    }

    private TaskStimulus GetTopTask()
    {
        int best = 0;
        for (int i = 1; i < _taskStimuli.Count; i++)
        {
            if (_taskStimuli[i].Intensity > _taskStimuli[best].Intensity)
                best = i;
        }
        return _taskStimuli[best];
    }

    private PerceptionStimulus GetTopPerception()
    {
        int best = 0;
        for (int i = 1; i < _perceptionStimuli.Count; i++)
        {
            if (_perceptionStimuli[i].Intensity > _perceptionStimuli[best].Intensity)
                best = i;
        }
        return _perceptionStimuli[best];
    }

    /// <summary>清理过期刺激源。</summary>
    private void RemoveExpired<T>(List<T> list, float currentTime) where T : IStimulus
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].Expiry > 0 && list[i].Expiry < currentTime)
            {
                list.RemoveAt(i);
            }
        }
    }

    /// <summary>比较两个焦点是否相同（同层 + 同来源）。</summary>
    private bool FocusEquals(Focus a, Focus b)
    {
        if (!a.IsValid && !b.IsValid) return true;
        if (a.IsValid != b.IsValid) return false;
        return a.Layer == b.Layer && ReferenceEquals(a.Source, b.Source);
    }

    // ===== 调试用 =====

    /// <summary>各层刺激源数量（调试用）。</summary>
    public int ThreatCount => _threatStimuli.Count;
    public int TaskCount => _taskStimuli.Count;
    public int PerceptionCount => _perceptionStimuli.Count;

    /// <summary>获取威胁层排行榜前 N 名（调试用）。</summary>
    public void GetTopThreats(List<ThreatStimulus> output, int maxCount)
    {
        output.Clear();
        // 简单复制后排序（调试用，不追求性能）
        var sorted = new List<ThreatStimulus>(_threatStimuli);
        sorted.Sort((a, b) => b.Intensity.CompareTo(a.Intensity));
        for (int i = 0; i < Mathf.Min(maxCount, sorted.Count); i++)
            output.Add(sorted[i]);
    }

    /// <summary>
    /// 收集所有层的刺激源，按强度降序排列，供 AI 调试面板展示（3.0.1_2）。
    /// UI 调 AIDebugController.GetSnapshot() -> NPCBrain.GetTopStimuli() -> 本方法。
    /// </summary>
    public void GetTopStimuliForDebug(List<StimulusDebugInfo> output, int maxCount)
    {
        output.Clear();
        Focus currentFocus = _currentFocus;

        // 威胁层
        foreach (var s in _threatStimuli)
        {
            output.Add(new StimulusDebugInfo(
                AttentionLayer.Threat, s.Intensity, s.Position,
                currentFocus.IsValid && currentFocus.Layer == AttentionLayer.Threat
            ));
        }
        // 其他层首版无刺激源，留壳

        // 按强度降序
        output.Sort((a, b) => b.Intensity.CompareTo(a.Intensity));

        // 截取前 maxCount
        if (output.Count > maxCount)
            output.RemoveRange(maxCount, output.Count - maxCount);
    }
}
