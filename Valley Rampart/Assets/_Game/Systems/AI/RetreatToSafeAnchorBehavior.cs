using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  QQQ.2 T8 / DR-21 - RetreatToSafeAnchor 撤退行为谱系
//  详见 QQQ.2_NPC任务修正以及一些小问题.md §需求4.4
//  解决"边界遇敌不知往内走"：SafetyScore 突降（遇敌）时，NPC 不硬回城堡中心，
//  改为撤往 WanderAnchorPool 中最近安全锚点（通常 = 王国内部建筑/广场/空地）。
//  到达后：威胁解除 → SafetyScore 回升 → 自然切回 Wander；仍受威胁 → 保持撤退/驻留。
//  集成点：NPCBrain.BuildBaseContext 解析 ctx.SafeAnchorPos（L3 战略撤退 + SafetyStimulus 低分拉力共用）。
// ============================================================================

/// <summary>
/// 撤退到最近安全锚点的目标解析（QQQ.2 T8 / DR-21）。
/// 纯静态，无状态；高分态回退 HomePoint（正常归巢语义，向后兼容编队撤退）。
/// </summary>
public static class RetreatToSafeAnchorBehavior
{
    /// <summary>取证日志节流：每单位最近一次已发出的撤退态（低分中 + 目标锚点）。仅状态/目标变化时才打 [ChainRetreat]。</summary>
    private static readonly Dictionary<int, (bool retreating, Vector2 anchor)> _lastEmit =
        new Dictionary<int, (bool, Vector2)>();

    /// <summary>
    /// 解析撤退目标：
    ///   高分（≥ wanderThreshold）→ HomePoint（正常归巢/漫游语义）；
    ///   低分（&lt; wanderThreshold）→ WanderAnchorPool 最近安全锚点（池空/未就绪回退 HomePoint）。
    /// unitId 用于取证日志按单位节流；非调试调用方传 0 即可（默认每次记录）。
    /// </summary>
    public static Vector2 ResolveRetreatTarget(Vector2 selfPos, float safetyScore, float wanderThreshold, Vector2 homePoint, int unitId = 0)
    {
        if (safetyScore >= wanderThreshold)
        {
            if (unitId > 0) _lastEmit.Remove(unitId);   // 退出撤退态，清节流态
            return homePoint;
        }

        // 低分：最近安全锚点（边界遇敌往内撤，不硬回城堡中心）
        var anchor = WanderAnchorPool.Instance.PickSafeAnchor(selfPos);
        if (anchor == Vector2.zero) return homePoint;

        // [取证/K7撤退] 仅状态变化（新进入撤退 / 目标锚点变化）时打一条，避免每帧刷屏
        bool isNewState = !_lastEmit.TryGetValue(unitId, out var prev) || !prev.retreating || prev.anchor != anchor;
        if (unitId > 0)
        {
            if (isNewState)
                Debug.Log($"[ChainRetreat] 撤退→安全锚点 {anchor} (self {selfPos}, safetyScore {safetyScore:F2}, unit {unitId})");
            _lastEmit[unitId] = (true, anchor);
        }
        else if (isNewState)   // 无单位键：退化为仅当状态变化才记录
        {
            Debug.Log($"[ChainRetreat] 撤退→安全锚点 {anchor} (self {selfPos}, safetyScore {safetyScore:F2})");
        }
        return anchor;
    }
}
