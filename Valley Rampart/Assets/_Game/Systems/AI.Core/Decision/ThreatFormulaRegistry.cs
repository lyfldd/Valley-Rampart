// ============================================================================
//  M6 T2 公式变体市场 - ThreatFormulaRegistry 注册表（02 §三.2）
//  harness 启动按 config.formulaThreat 查注册表选实现；未注册回退 LinearV1。
//  Unity 侧默认 LinearV1（核内真身），行为与 M0-M5 完全一致。
//  AI.Core 零 UnityEngine 引用（M1 硬约束）。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 威胁公式注册表（T2 变体市场）。
/// Register 由变体实现所在程序集启动时调用（harness 的 Formulas/ 目录注册变体）；
/// Get 按名查，未注册返回默认 LinearV1（行为不变兜底）。
/// </summary>
public static class ThreatFormulaRegistry
{
    private static readonly Dictionary<string, IThreatFormula> _formulas = new Dictionary<string, IThreatFormula>();

    static ThreatFormulaRegistry()
    {
        // 默认公式（baseline 真身，现 CalculateRawFactor 逻辑）
        Register(new LinearThreatFormula());
    }

    /// <summary>注册公式（重名覆盖；变体放 harness/Formulas/ 启动时调）。</summary>
    public static void Register(IThreatFormula formula)
    {
        if (formula == null || string.IsNullOrEmpty(formula.Name)) return;
        _formulas[formula.Name] = formula;
    }

    /// <summary>按名取公式；未注册回退默认 LinearV1（行为不变）。</summary>
    public static IThreatFormula Get(string name)
    {
        if (!string.IsNullOrEmpty(name) && _formulas.TryGetValue(name, out var f))
            return f;
        return _formulas.TryGetValue(LinearThreatFormula.DefaultName, out var def) ? def : null;
    }

    /// <summary>当前已注册公式名（诊断/CLI 列表用）。</summary>
    public static IEnumerable<string> Names => _formulas.Keys;
}
