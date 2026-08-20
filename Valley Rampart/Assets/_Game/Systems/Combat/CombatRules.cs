using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3.6 战斗规则工具（静态）：击飞模型（动能 + θ 随机路线）。
/// 全局常量暂内联（3.6 §7.3 不过度解耦：击飞参数训练校准前先固定，后续按需 SO 化）。
/// </summary>
public static class CombatRules
{
    // ===== 击飞模型全局参数（3.6 §5.4，占位可调）=====
    public const float KnockbackGravity = 9.8f;        // g（伪物理）
    public const float KnockbackThetaMin = 30f;        // 抛射角下限（度）
    public const float KnockbackThetaMax = 75f;        // 抛射角上限（度）
    public const float KnockbackForceScale = 6f;       // 冲击力基础（格）
    public const float ToughnessReduceScale = 0.02f;   // 韧性削减系数（高韧性 → v0 小 → 飞得近矮）

    /// <summary>
    /// 击飞结算（3.6 §5.4 定稿 + 2026-08-05 训练师解耦）：动能定最大限度 + θ 均匀随机 30°~75° 定路线。
    ///   v0    = 冲击力 × (1 - 韧性削减系数 × 韧性)
    ///   L_max = v0² / g；L = L_max × sin(2θ)
    /// 输出击飞距离（世界单位）与滞空时长。
    /// 改动③（对齐 sim ChargeSweep）：冲击力固定 6f（不再随 chargeDamage 缩放——chargeDamage 一参两用会
    ///   在伤害改 40 时把击飞也削到地板）；被撞「懵」滞空固定 1.2s（原 Clamp(0.3*dist,0.3,2.5)）。
    /// </summary>
    public static void ComputeKnockback(float chargeDamage, float toughness,
        out float distanceWorld, out float duration)
    {
        // 冲击力固定 6f（sim 2026-08-05 解耦：chargeDamage 只决定伤害，不决定击飞动能）
        float impactForce = KnockbackForceScale;
        if (impactForce < 0.1f) impactForce = 0.1f;

        float v0 = impactForce * Mathf.Max(0.1f, 1f - ToughnessReduceScale * toughness);
        float lMax = (v0 * v0) / KnockbackGravity;

        // θ 均匀随机 30°~75°：θ 大 → 高而近；θ 小 → 远而矮（"这次撞高、下次撞远"）
        float theta = Random.Range(KnockbackThetaMin, KnockbackThetaMax) * Mathf.Deg2Rad;
        float distFactor = Mathf.Sin(2f * theta);

        float distance = Mathf.Max(0.5f, lMax * distFactor);
        duration = 1.2f;                                             // 被撞「懵」固定 1.2s（改动③）
        distanceWorld = distance;
    }

    // ===== 2_5 目标选择 + 视线（D83/D3）=====

    /// <summary>
    /// 价值×距离目标评分（2_5 步骤4，D83）：score = valueWeight×目标价值 − distWeight×距离(格单位)，取最高。
    /// 近战：攻击范围内候选按此评分（无视线要求）；远程：射程圆内 + HasLineOfSight。
    /// </summary>
    public static float TargetScore(float targetValue, float distCells,
        float valueWeight, float distWeight)
        => valueWeight * targetValue - distWeight * distCells;

    /// <summary>
    /// 微格视线判定（2_5 步骤5，D3）：from→to 逐微格 Bresenham，任一非起点/终点微格不可走（非桥）→ false。
    /// 越界保守放行 true（不误拦）。
    /// </summary>
    public static bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        if (GridSystem.Instance == null) return true;
        var a = GridSystem.Instance.WorldToSubCoord(from);
        var b = GridSystem.Instance.WorldToSubCoord(to);
        if (a == null || b == null) return true; // 越界保守放行
        GridCoord sa = a.Value, sb = b.Value;
        foreach (var sub in SubBresenham(sa, sb))
        {
            if (sub == sa || sub == sb) continue; // 起点/终点所在微格豁免
            if (!GridSystem.Instance.IsSubWalkable(sub)) return false; // 桥可走 → 天然放行
        }
        return true;
    }

    /// <summary>微格 Bresenham 直线逐点枚举（含起终点，调用方自行豁免终点语义）。</summary>
    private static IEnumerable<GridCoord> SubBresenham(GridCoord a, GridCoord b)
    {
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            yield return new GridCoord(x0, y0);
            if (x0 == x1 && y0 == y1) yield break;
            int e2 = 2 * err;
            if (e2 >= dy) { err += dy; x0 += sx; }
            if (e2 <= dx) { err += dx; y0 += sy; }
        }
    }
}
