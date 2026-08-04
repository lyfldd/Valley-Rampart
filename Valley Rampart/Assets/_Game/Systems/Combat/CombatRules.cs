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
    /// 击飞结算（3.6 §5.4 定稿）：动能定最大限度 + θ 均匀随机 30°~75° 定路线。
    ///   v0    = 冲击力 × (1 - 韧性削减系数 × 韧性)
    ///   L_max = v0² / g；L = L_max × sin(2θ)；T = k × L
    /// 输出击飞距离（世界单位）与滞空时长（= 被打断时长）。
    /// </summary>
    public static void ComputeKnockback(float chargeDamage, float toughness,
        out float distanceWorld, out float duration)
    {
        // 冲击力随冲锋强度缩放（基础 6 格，chargeDamage=80 时为满额）
        float impactForce = KnockbackForceScale * Mathf.Clamp01(chargeDamage / 80f);
        if (impactForce < 0.1f) impactForce = 0.1f;

        float v0 = impactForce * Mathf.Max(0.1f, 1f - ToughnessReduceScale * toughness);
        float lMax = (v0 * v0) / KnockbackGravity;

        // θ 均匀随机 30°~75°：θ 大 → 高而近；θ 小 → 远而矮（"这次撞高、下次撞远"）
        float theta = Random.Range(KnockbackThetaMin, KnockbackThetaMax) * Mathf.Deg2Rad;
        float distFactor = Mathf.Sin(2f * theta);

        float distance = Mathf.Max(0.5f, lMax * distFactor);
        duration = Mathf.Clamp(0.3f * distance, 0.3f, 2.5f);   // T = k × L（k=0.3）
        distanceWorld = distance;
    }
}
