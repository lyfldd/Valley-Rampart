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
    /// <summary>
    /// 解析撤退目标：
    ///   高分（≥ wanderThreshold）→ HomePoint（正常归巢/漫游语义）；
    ///   低分（&lt; wanderThreshold）→ WanderAnchorPool 最近安全锚点（池空/未就绪回退 HomePoint）。
    /// </summary>
    public static Vector2 ResolveRetreatTarget(Vector2 selfPos, float safetyScore, float wanderThreshold, Vector2 homePoint)
    {
        if (safetyScore >= wanderThreshold) return homePoint;

        // 低分：最近安全锚点（边界遇敌往内撤，不硬回城堡中心）
        var anchor = WanderAnchorPool.Instance.PickSafeAnchor(selfPos);
        if (anchor != Vector2.zero) return anchor;
        return homePoint;
    }
}
