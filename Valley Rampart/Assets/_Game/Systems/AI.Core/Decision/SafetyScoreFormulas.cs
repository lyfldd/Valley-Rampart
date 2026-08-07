// ============================================================================
//  AI.Core Decision - 统一安全系数 SafetyScore 静态公式（QQQ.2 T8 / DR-21）
//  纯计算零引擎依赖（MathfX），供 NPCBrain（⓪ 组装）与 L3CommandComputer 调用。
//  合并 SafetyStimulus/ThreatHysteresis/Caution 三路为空闲分布统一安全系数。
// ============================================================================

/// <summary>
/// SafetyScore 公式族（QQQ.2 §需求4 / DR-21）。
///
/// SafetyScore = baseSafety(0.5)
///            + wallFactor × wallWeight(0.3)              // 在城墙内（多段/无城墙都支持）
///            + armyFactor × armyWeight(0.2)              // 8格内友军 ≥ protectionUpThresholds[0]=3
///            + kingdomDistanceFactor                      // max(kingdomDistMin, 1 - perCell×格数)，近家=1
///            - ThreatFactor × threatPenaltyScale(0.8)    // 满威胁扣 0.8 → 高分档必跌破 0.4 不 Wander
///            - NightFactor × nightPenaltyScale(0.1)      // 夜晚
///
/// 三层梯度（DR-21）：
///   Score < 0.4   → 不 Wander + 触发 RetreatToSafeAnchor
///   0.4 ≤ S < 0.6 → 可 Wander，小半径（wanderRadiusMinCells 档）
///   S ≥ 0.6       → 大半径 Wander（wanderRadiusMaxCells 档）+ 可自动说话（T2 复用）
/// </summary>
public static class SafetyScoreFormulas
{
    /// <summary>统一安全系数合成（各分项由调用方 NPCBrain 算好传入，公式集中可查）。</summary>
    public static float ComputeSafetyScore(
        float baseSafety, bool insideWall, float wallWeight,
        bool hasArmy, float armyWeight, float kingdomDistFactor,
        float threatFactor, float threatPenaltyScale,
        float nightFactor, float nightPenaltyScale)
    {
        float score = baseSafety
            + (insideWall ? wallWeight : 0f)
            + (hasArmy ? armyWeight : 0f)
            + kingdomDistFactor
            - threatFactor * threatPenaltyScale
            - nightFactor * nightPenaltyScale;
        return score;
    }

    /// <summary>距王国锚点安全因子：近家=1，每格衰减 perCell（0.02），最低不归零（kingdomDistMin=0.1）。</summary>
    public static float KingdomDistanceFactor(float distCells, float minValue, float perCellDecay)
    {
        return MathfX.Max(minValue, 1f - perCellDecay * distCells);
    }

    /// <summary>
    /// Wander 半径（格）：Score 越高半径越大，阈值→下限、阈值+0.2→上限线性插值（DR-21：4-8 格）。
    /// Score=0.4 → minCells(4)，Score=0.6 → maxCells(8)。
    /// </summary>
    public static float WanderRadiusCells(float score, float threshold, float minCells, float maxCells)
    {
        float t = MathfX.Clamp01((score - threshold) / 0.2f);
        return MathfX.Lerp(minCells, maxCells, t);
    }

    /// <summary>
    /// SafetyStimulus 回城拉力（扩展 SafetyStimulusProvider 公式，DR-21）：
    /// Score 越低拉力越强：pull = (1 - clamp01(score)) × baseSafetyPull × profScale。
    /// Score≥1 → 0（很安全不拉回城）；Score=0 → 满拉力。
    /// </summary>
    public static float SafetyPull(float score, float basePull, float profScale)
    {
        return (1f - MathfX.Clamp01(score)) * basePull * profScale;
    }
}
