// ============================================================================
//  M6 T2 公式变体市场 - LinearThreatFormula 默认实现（baseline 真身）
//  现 ThreatAssessment.CalculateRawFactor 逻辑原样搬入（02 §三.2：现公式原样搬入，
//  作为 baseline 变体）。行为与 M0-M5 完全一致——T2 守门：新变体必须不劣于此 baseline。
//  AI.Core 零 UnityEngine 引用（M1 硬约束）。
// ============================================================================

/// <summary>
/// 默认威胁公式（LinearV1）：六因子线性加权 + 职业敏感度 + 贴脸保底（Clamp01）。
/// 因子清单：敌人距离 / 数量 / 血量 / 友军保护 / 时间（昼夜）/ 区块热度。
/// 与 ThreatAssessor.CalculateRawFactor 原实现逐行对应。
/// </summary>
public sealed class LinearThreatFormula : IThreatFormula
{
    public const string DefaultName = "LinearV1";

    public string Name => DefaultName;

    public float Compute(in ThreatInputs i, TuningSnapshot cfg)
    {
        // 敌人距离因子（越近越高，0-1；攻击距离内保底 1.0——弓手贴脸最高距离威胁）
        float distFactor;
        if (i.EnemyCount > 0 && i.NearestEnemyDist < i.PerceptionWorldRadius)
        {
            if (i.AttackWorldRange > 0f && i.NearestEnemyDist <= i.AttackWorldRange)
                distFactor = 1f;
            else
                distFactor = 1f - MathfX.Clamp01(i.NearestEnemyDist / i.PerceptionWorldRadius);
        }
        else
        {
            distFactor = 0f;
        }

        // 敌人数量因子（越多越高，0-1，满编数 config 可调）
        float countFactor = MathfX.Clamp01(i.EnemyCount / cfg.countFactorFullCount);

        // 血量因子（越低越高，0-1）
        float hpFactor = 1f - MathfX.Clamp01(i.HpRatio);

        // 友军保护因子（越多友军越低，0-1）
        float allyFactor = 1f - MathfX.Clamp01((float)i.AllyCount / cfg.protectionFriendThreshold);

        // 时间因子（夜晚 +0.1）
        float timeFactor = i.IsNight ? 0.1f : 0f;

        // 区块威胁热度因子（环境型威胁，与夜晚同构）
        float heatFactor = MathfX.Clamp01(i.RegionHeat);

        // 加权合成（权重入 SO，防硬编码）
        float x = distFactor * cfg.rfDistWeight
                + countFactor * cfg.rfCountWeight
                + hpFactor * cfg.rfHpWeight
                + allyFactor * cfg.rfAllyWeight
                + timeFactor * cfg.rfTimeWeight
                + heatFactor * cfg.rfHeatWeight;

        // 应用职业敏感度（原 CalculateRawFactor x *= profession.threatSensitivity）
        x *= i.ThreatSensitivity;

        // 攻击距离内保底：敌人进入自身攻击距离时，rawFactor 不低于 config 值（原 closeRangeMinRaw）
        if (i.AttackWorldRange > 0f && i.NearestEnemyDist <= i.AttackWorldRange)
            x = MathfX.Max(x, i.CloseRangeMinRaw);

        return MathfX.Clamp01(x);
    }
}
