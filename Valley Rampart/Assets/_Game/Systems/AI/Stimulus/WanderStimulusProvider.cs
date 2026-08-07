using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  QQQ.2 T8 / DR-21 - 漫游刺激源提供者（重写：动态锚点池驱动 + SafetyScore 门控）
//  详见 QQQ.2_NPC任务修正以及一些小问题.md §需求4.1/4.2
//  旧实现：_stimulus.Position = ctx.HomePoint 硬编码（所有空闲 NPC 聚城堡）。
//  新实现：
//   ① SafetyScore < wanderThreshold(0.4) → 不 Wander（不安全，交 Retreat/Safety）
//   ② 每 10-20s 随机间隔从 WanderAnchorPool 抽新锚点（近邻优先+随机抖动+最近N不重抽）
//   ③ 间隔内复用当前锚点（防每 tick 抖动）；抽失败回退 HomePoint 小半径
// ============================================================================

/// <summary>
/// WanderStimulus 提供者（§6.3 + QQQ.2 T8）。
/// 漫游中心 = 动态锚点池抽取的安全锚点（城堡/建筑/空地），取代硬编码 HomePoint。
/// </summary>
public class WanderStimulusProvider
{
    private readonly WanderStimulus _stimulus = new WanderStimulus();
    private readonly List<Vector2> _recent = new List<Vector2>();  // 本 NPC 最近用过的锚点（防重抽）

    private Vector2 _currentAnchor;
    private bool _hasAnchor;
    private float _lastRefreshTime = float.NegativeInfinity;
    private float _nextInterval = 12f;

    /// <summary>池化 WanderStimulus 实例（复用不 new）</summary>
    public WanderStimulus Stimulus => _stimulus;

    /// <summary>每 tick 更新漫游中心 + 强度，返回池化实例。</summary>
    public WanderStimulus GetOrUpdate(in FactorContext ctx)
    {
        _stimulus.Position = ctx.HomePoint;
        _stimulus.Intensity = 0f;

        // ① DR-21 门控：Score < wanderThreshold → 不 Wander（低分态交撤退/回城拉力）
        if (ctx.SafetyScore < ctx.Config.wanderThreshold) return _stimulus;

        // ② 锚点刷新（10-20s 随机间隔，间隔内复用当前锚点防抖动）
        float now = ctx.CurrentTime;
        if (now - _lastRefreshTime >= _nextInterval)
        {
            _lastRefreshTime = now;
            _nextInterval = Random.Range(
                ctx.Config.anchorRefreshIntervalMin, ctx.Config.anchorRefreshIntervalMax);

            var selfPos = new Vector2(ctx.SelfPos.x, ctx.SelfPos.y);
            var pool = WanderAnchorPool.Instance;
            if (pool.TryPickAnchor(selfPos, _recent, ctx.Config.anchorAvoidRecentCount, out var anchor))
            {
                _currentAnchor = anchor;
                _hasAnchor = true;
                _recent.Add(anchor);
                if (_recent.Count > Mathf.Max(1, ctx.Config.anchorAvoidRecentCount))
                    _recent.RemoveAt(0);
            }
            else
            {
                _hasAnchor = false;  // 无锚点（未初始化/空池）→ 回退 HomePoint
            }
        }

        // ③ 写回池化刺激
        _stimulus.Position = _hasAnchor ? Vector2XUnity.FromUnity(_currentAnchor) : ctx.HomePoint;
        _stimulus.Intensity = ctx.Config.wanderIntensity;
        return _stimulus;
    }

    public void Reset()
    {
        _recent.Clear();
        _currentAnchor = Vector2.zero;
        _hasAnchor = false;
        _lastRefreshTime = float.NegativeInfinity;
        _nextInterval = 12f;
    }
}
