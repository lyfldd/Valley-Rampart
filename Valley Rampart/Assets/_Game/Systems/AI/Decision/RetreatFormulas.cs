using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 撤退/警戒/恢复/跟随/归巢 静态公式
//  详见 3.0.1_2_输入输出决定层设计.md §5 + 3.0.1_1 §6
//  全部公式集中可查，L3 参数计算层调用
//  ⚠️ 3.0.1_1 勘误：recoveryDuration sens 在分子（高敏感恢复更慢），按§5.1"以此为准"
// ============================================================================

/// <summary>
/// 撤退链路公式（3.0.1_1 §6 + 3.0.1_2 §5.2）。
/// 全部静态，L3CommandComputer 调用。courage/sens/hpRatio 全链路参与。
/// </summary>
public static class RetreatFormulas
{
    /// <summary>
    /// 撤退距离（3.0.1_1 §6.2：courage + sens + hitCount 阶梯）。
    /// hitCount 越高撤越远，courage 越低撤越远，sens 放大距离。
    /// </summary>
    public static float RetreatDistance(int courage, float sens, int hitCount,
                                        int baseCells, float stepCells, float cellSize)
    {
        // (baseCells + hitCount * stepCells) * (1 + (100-courage)/200) * sens * cellSize
        return (baseCells + hitCount * stepCells)
               * (1f + (100 - courage) / 200f)
               * sens
               * cellSize;
    }

    /// <summary>
    /// 撤退速度（3.0.1_1 §6.3：courage 越低跑越快）。
    /// courage=30 -> walkSpeed × 1.7；courage=70 -> walkSpeed × 1.3
    /// </summary>
    public static float RetreatSpeed(int courage, float walkSpeed)
    {
        return walkSpeed * (1f + (100 - courage) / 100f);
    }

    /// <summary>
    /// 警戒时长（3.0.1_1 §6.4：courage 越低越长 + 血量越低越长）。
    /// cautionDuration = base * (1 + (100-courage)/100) * (1 + (1-hpRatio)*0.5)
    /// </summary>
    public static float CautionDuration(int courage, float hpRatio, float baseTime)
    {
        return baseTime
               * (1f + (100 - courage) / 100f)
               * (1f + (1f - hpRatio) * 0.5f);
    }

    /// <summary>
    /// 恢复时长（3.0.1_1 §6.5，按§5.1"以此为准"的修正方向）。
    /// recoveryDuration = base * (50/courage) * threatSensitivity
    /// sens 在分子：高敏感恢复更慢（符合§5.1勘误②修正方向，勿用 /sens 错误公式）。
    /// </summary>
    public static float RecoveryDuration(int courage, float sens, float baseTime)
    {
        return baseTime
               * (50f / Mathf.Max(1, courage))
               * sens;
    }

    /// <summary>
    /// 跟随保持距离（3.0.1_2 §5.2）。
    /// 威胁越高跟得越松（给士兵让位、便于逃跑），威胁 0 时紧凑跟随。
    /// </summary>
    public static float FollowKeepDistance(int threatLevel, int baseCells,
                                           float scatterWeight, float cellSize)
    {
        return baseCells * cellSize * (1f + threatLevel * scatterWeight);
    }

    /// <summary>
    /// 归巢吸引强度（3.0.1_2 §3.1 / §5.2）。
    /// safetyUrge = base * (1 + nightFactor * nightWeight) * (1 + (1-hpRatio) * woundWeight) * profScale
    /// </summary>
    public static float SafetyUrge(float basePull, float nightFactor, float nightWeight,
                                   float hpRatio, float woundWeight, float profScale)
    {
        return basePull
               * (1f + nightFactor * nightWeight)
               * (1f + (1f - hpRatio) * woundWeight)
               * profScale;
    }
}
