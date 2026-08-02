using System;

// ============================================================================
//  AI.Core Shim - MathfX 纯 C# 数学函数
//  详见 03_大脑提取与双适配工程.md §四 最小 shim（~80 行）
//  决策核零 UnityEngine 依赖：Mathf -> MathfX 机械替换。
//  仅实现决策核用到的函数：Clamp/Clamp01/Max/Min/Lerp/RoundToInt/CeilToInt/Exp/Sin/Abs。
// ============================================================================

/// <summary>
/// 数学函数 shim（替代 UnityEngine.Mathf，零引擎依赖）。
/// 语义与 Mathf 一致（RoundToInt 采用 Math.Round 的银行家舍入，与 Mathf 一致）。
/// </summary>
public static class MathfX
{
    public static float Clamp(float value, float min, float max)
        => value < min ? min : (value > max ? max : value);

    public static int Clamp(int value, int min, int max)
        => value < min ? min : (value > max ? max : value);

    public static float Clamp01(float value)
        => value < 0f ? 0f : (value > 1f ? 1f : value);

    public static float Max(float a, float b) => a > b ? a : b;
    public static float Min(float a, float b) => a < b ? a : b;
    public static int Max(int a, int b) => a > b ? a : b;
    public static int Min(int a, int b) => a < b ? a : b;

    /// <summary>线性插值（t 夹取 0-1，与 Mathf.Lerp 一致）</summary>
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

    public static int RoundToInt(float f) => (int)Math.Round(f);
    public static int CeilToInt(float f) => (int)Math.Ceiling(f);

    public static float Exp(float power) => (float)Math.Exp(power);
    public static float Sin(float f) => (float)Math.Sin(f);
    public static float Abs(float f) => Math.Abs(f);
    public static int Abs(int v) => Math.Abs(v);
}
