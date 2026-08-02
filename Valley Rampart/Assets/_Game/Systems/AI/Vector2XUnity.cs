using UnityEngine;

// ============================================================================
//  AI 壳 - Vector2X <-> UnityEngine.Vector2 转换助手（M1 决策核提取）
//  核内（AI.Core）零引擎依赖，位置一律 Vector2X；壳（MonoBehaviour 侧）消费时转回 Vector2。
//  机械转换，无行为差异。
// ============================================================================

/// <summary>
/// 核内外向量互转助手（壳专用，含 UnityEngine 依赖）。
/// </summary>
public static class Vector2XUnity
{
    /// <summary>核内 Vector2X -> UnityEngine.Vector2</summary>
    public static Vector2 ToUnity(Vector2X v) => new Vector2(v.x, v.y);

    /// <summary>UnityEngine.Vector2 -> 核内 Vector2X</summary>
    public static Vector2X FromUnity(Vector2 v) => new Vector2X(v.x, v.y);
}
