using System;

// ============================================================================
//  AI.Core Shim - Vector2X 纯 C# 二维向量
//  详见 03_大脑提取与双适配工程.md §四 最小 shim（~80 行）
//  决策核零 UnityEngine 依赖：Vector2 -> Vector2X 机械替换。
//  仅实现决策核需要的运算（加减乘除/长度/距离/归一化/Lerp/MoveTowards）。
// ============================================================================

/// <summary>
/// 二维向量 shim（替代 UnityEngine.Vector2，零引擎依赖）。
/// </summary>
public struct Vector2X : IEquatable<Vector2X>
{
    public float x;
    public float y;

    public Vector2X(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public static Vector2X zero => new Vector2X(0f, 0f);
    public static Vector2X one => new Vector2X(1f, 1f);

    /// <summary>长度</summary>
    public float magnitude => (float)Math.Sqrt(x * x + y * y);

    /// <summary>长度平方（比较用，免开方）</summary>
    public float sqrMagnitude => x * x + y * y;

    /// <summary>单位向量（零向量返回 zero）</summary>
    public Vector2X normalized
    {
        get
        {
            float m = magnitude;
            if (m > 1E-05f) return new Vector2X(x / m, y / m);
            return zero;
        }
    }

    /// <summary>两点距离</summary>
    public static float Distance(Vector2X a, Vector2X b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>长度平方</summary>
    public static float SqrMagnitude(Vector2X v) => v.x * v.x + v.y * v.y;

    /// <summary>线性插值（t 夹取 0-1，与 Mathf.Lerp 语义一致）</summary>
    public static Vector2X Lerp(Vector2X a, Vector2X b, float t)
    {
        t = MathfX.Clamp01(t);
        return new Vector2X(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
    }

    /// <summary>朝目标移动 maxDistanceDelta（与 Vector2.MoveTowards 语义一致）</summary>
    public static Vector2X MoveTowards(Vector2X current, Vector2X target, float maxDistanceDelta)
    {
        float dx = target.x - current.x;
        float dy = target.y - current.y;
        float sqrDist = dx * dx + dy * dy;
        if (sqrDist == 0f || (maxDistanceDelta >= 0f && sqrDist <= maxDistanceDelta * maxDistanceDelta))
            return target;
        float dist = (float)Math.Sqrt(sqrDist);
        return new Vector2X(current.x + dx / dist * maxDistanceDelta,
                            current.y + dy / dist * maxDistanceDelta);
    }

    // ===== 运算符 =====

    public static Vector2X operator +(Vector2X a, Vector2X b) => new Vector2X(a.x + b.x, a.y + b.y);
    public static Vector2X operator -(Vector2X a, Vector2X b) => new Vector2X(a.x - b.x, a.y - b.y);
    public static Vector2X operator *(Vector2X a, float d) => new Vector2X(a.x * d, a.y * d);
    public static Vector2X operator *(float d, Vector2X a) => new Vector2X(a.x * d, a.y * d);
    public static Vector2X operator /(Vector2X a, float d) => new Vector2X(a.x / d, a.y / d);
    public static Vector2X operator -(Vector2X a) => new Vector2X(-a.x, -a.y);

    public bool Equals(Vector2X other) => x.Equals(other.x) && y.Equals(other.y);
    public override bool Equals(object obj) => obj is Vector2X other && Equals(other);
    public override int GetHashCode() => (x.GetHashCode() * 397) ^ y.GetHashCode();
    public static bool operator ==(Vector2X a, Vector2X b) => a.Equals(b);
    public static bool operator !=(Vector2X a, Vector2X b) => !a.Equals(b);

    public override string ToString() => "(" + x + ", " + y + ")";
}
