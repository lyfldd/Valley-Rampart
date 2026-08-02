// ============================================================================
//  M6 T2 公式变体市场 - DistSquaredV1 示例变体（harness/Formulas/ 目录）
//  02 §三.1 候选公式 1：CalculateRawFactor 非线性变体——距离因子改平方衰减。
//  T2 守门（02 §三.2）：新变体必须不劣于 baseline（LinearV1）才可人审——
//  用 `dotnet run -- formula compare DistSquaredV1` 对比总分 + holdout。
//  变体注册：本文件被 harness 编译（harness.csproj 默认包含项目目录），
//  需在 Program.Main 启动时调 ThreatFormulaRegistry.Register(new DistSquaredThreatFormula())。
//  修改公式 = 只改这里，AI.Core 零改动（决策核唯一真身不动）。
// ============================================================================

/// <summary>
/// 距离平方衰减变体：distFactor = (1 - clamp01(dist/radius))²，
/// 近敌威胁更尖锐（贴脸更快拉满），远敌更快衰减到 0。
/// 其余因子（数量/血量/友军/时间/热度）与 LinearV1 一致。
/// </summary>
public sealed class DistSquaredThreatFormula : IThreatFormula
{
    public string Name => "DistSquaredV1";

    public float Compute(in ThreatInputs i, TuningSnapshot cfg)
    {
        // 距离因子平方衰减（唯一差异点）
        float distFactor;
        if (i.EnemyCount > 0 && i.NearestEnemyDist < i.PerceptionWorldRadius)
        {
            if (i.AttackWorldRange > 0f && i.NearestEnemyDist <= i.AttackWorldRange)
                distFactor = 1f;
            else
            {
                float t = 1f - MathfX.Clamp01(i.NearestEnemyDist / i.PerceptionWorldRadius);
                distFactor = t * t;   // 平方：近敌威胁更尖锐
            }
        }
        else
        {
            distFactor = 0f;
        }

        float countFactor = MathfX.Clamp01(i.EnemyCount / cfg.countFactorFullCount);
        float hpFactor = 1f - MathfX.Clamp01(i.HpRatio);
        float allyFactor = 1f - MathfX.Clamp01((float)i.AllyCount / cfg.protectionFriendThreshold);
        float timeFactor = i.IsNight ? 0.1f : 0f;
        float heatFactor = MathfX.Clamp01(i.RegionHeat);

        float x = distFactor * cfg.rfDistWeight
                + countFactor * cfg.rfCountWeight
                + hpFactor * cfg.rfHpWeight
                + allyFactor * cfg.rfAllyWeight
                + timeFactor * cfg.rfTimeWeight
                + heatFactor * cfg.rfHeatWeight;

        x *= i.ThreatSensitivity;
        if (i.AttackWorldRange > 0f && i.NearestEnemyDist <= i.AttackWorldRange)
            x = MathfX.Max(x, i.CloseRangeMinRaw);

        return MathfX.Clamp01(x);
    }
}
