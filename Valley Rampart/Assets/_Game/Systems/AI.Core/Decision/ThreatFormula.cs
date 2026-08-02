// ============================================================================
//  M6 T2 公式变体市场 - IThreatFormula 接口 + ThreatInputs（02 §三.2 设计预留）
//  每个可换公式一个接口 + 默认实现（LinearV1 现公式原样搬入，作为 baseline 变体）。
//  harness 启动按 config.formulaThreat 选实现；Unity 侧默认 LinearV1 行为不变。
//  AI.Core 零 UnityEngine 引用（M1 硬约束）。
// ============================================================================

/// <summary>威胁公式输入（CalculateRawFactor 全部入参，变体实现的唯一输入面）。</summary>
public struct ThreatInputs
{
    public float NearestEnemyDist;
    public int EnemyCount;
    public float HpRatio;
    public int AllyCount;
    public bool IsNight;
    public float PerceptionWorldRadius;
    public float AttackWorldRange;
    public float RegionHeat;
    public float ThreatSensitivity;   // 职业敏感度（原 CalculateRawFactor x *= threatSensitivity）
    public float CloseRangeMinRaw;    // 贴脸保底（原 closeRangeMinRaw）
}

/// <summary>
/// T2 威胁公式接口（02 §三.2）：输入 ThreatInputs + 调参快照 -> rawFactor（0-1）。
/// 变体实现放 harness/Formulas/（训练师写），默认 LinearV1 在 AI.Core（baseline 真身）。
/// </summary>
public interface IThreatFormula
{
    /// <summary>公式名（注册表 key，config.formulaThreat 用）。</summary>
    string Name { get; }

    /// <summary>计算 rawFactor（0-1）。</summary>
    float Compute(in ThreatInputs i, TuningSnapshot cfg);
}
