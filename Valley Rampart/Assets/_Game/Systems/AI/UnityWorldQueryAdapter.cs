using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  AI 壳 - IWorldQuery 的 Unity 实现（M1 决策核提取，接缝 3）
//  核内不直取单例，壳包一层 GridSystem + LODSystem 传进核（AttentionSystem 等）。
// ============================================================================

/// <summary>
/// IWorldQuery 的 Unity 适配器（接缝 3：单例直取 -> 注入）。
/// CellSize = GridSystem.Config.cellSize；GetHeatAt/TryGetHotspot = LODSystem。
/// </summary>
public class UnityWorldQueryAdapter : IWorldQuery
{
    public float CellSize =>
        GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x
            : 0f;

    public float GetHeatAt(Vector2X pos)
        => LODSystem.Instance != null ? LODSystem.Instance.GetHeatAt(Vector2XUnity.ToUnity(pos)) : 0f;

    public bool TryGetHotspot(Vector2X pos, float maxAge, out Vector2X hotspot)
    {
        // M1：核内无消费方（支撑逻辑走壳 LODSystem 直调），searchRadius 传 0 保守
        if (LODSystem.Instance != null
            && LODSystem.Instance.TryGetNearestCombatHotspot(Vector2XUnity.ToUnity(pos), maxAge, 0f, out var hs))
        {
            hotspot = Vector2XUnity.FromUnity(hs);
            return true;
        }
        hotspot = Vector2X.zero;
        return false;
    }

    public void QueryUnitsInCell(int cx, int cy, List<IUnitHandle> results)
    {
        // M1：核内暂无消费方（P1 接 GridSystem 空间分区查询），留空壳
    }
}
