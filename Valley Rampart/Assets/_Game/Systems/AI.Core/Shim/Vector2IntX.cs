using System;

// ============================================================================
//  AI.Core Shim - Vector2IntX 纯 C# 整数二维向量（最小 shim）
//  详见 03_大脑提取与双适配工程.md §四
//  决策核零 UnityEngine 依赖：Vector2Int -> Vector2IntX 机械替换。
//  仅承载槽位偏移（cell 单位）的 x/y 两个整数，不做多余运算。
// ============================================================================

/// <summary>
/// 整数二维向量 shim（替代 UnityEngine.Vector2Int，零引擎依赖）。
/// 编队槽位偏移（cell 单位）用。
/// </summary>
public struct Vector2IntX : IEquatable<Vector2IntX>
{
    public int x;
    public int y;

    public Vector2IntX(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static Vector2IntX zero => new Vector2IntX(0, 0);

    public bool Equals(Vector2IntX other) => x == other.x && y == other.y;
    public override bool Equals(object obj) => obj is Vector2IntX other && Equals(other);
    public override int GetHashCode() => (x * 397) ^ y;
    public static bool operator ==(Vector2IntX a, Vector2IntX b) => a.Equals(b);
    public static bool operator !=(Vector2IntX a, Vector2IntX b) => !a.Equals(b);

    public override string ToString() => "(" + x + ", " + y + ")";
}
