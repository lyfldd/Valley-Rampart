using System;
using UnityEngine;

// ============================================================================
//  2_6 P0a 寻路服务：同步微格 A* 入口（社区 GridSystem 实现 IPathGrid）。
//  提供 FindPathImmediate（调试/sim/P0a 冒烟 + PathFollower 阶段0 接线）。
//  异步分帧 / 票据 / 失效重寻（P0b 服务化）归阶段0 尾，本片仅同步入口。
// ============================================================================

/// <summary>寻路服务（2_6 P0a）。静态同步入口，接 GridSystem IPathGrid。</summary>
public static class PathfindingService
{
    private const int DEFAULT_MAX_EXPANSIONS = 4096;

    /// <summary>
    /// 同步微格 A*（推荐入口）。from/to 为世界坐标，先转微格再求解。
    /// 落点吸附微格只在调用时算（D73，由 PathFollower.SetDestination 每次调用触达）。
    /// </summary>
    public static PathResult FindPathImmediate(Vector2 fromWorld, Vector2 toWorld)
    {
        var grid = GridSystem.Instance;
        if (grid == null) return null;

        GridCoord? fromOpt = grid.WorldToSubCoord(fromWorld);
        GridCoord? toOpt = grid.WorldToSubCoord(toWorld);
        if (!fromOpt.HasValue || !toOpt.HasValue) return null;   // 越界

        int maxExp = GridSystemHasConfig() ? GridSystemMaxExpansions() : DEFAULT_MAX_EXPANSIONS;
        return AStarSolver.Solve(grid, fromOpt.Value, toOpt.Value, maxExp);
    }

    /// <summary>直接用微格坐标求解（内部用）。</summary>
    public static PathResult FindPathImmediate(GridCoord fromSub, GridCoord toSub)
    {
        var grid = GridSystem.Instance;
        if (grid == null) return null;
        int maxExp = GridSystemHasConfig() ? GridSystemMaxExpansions() : DEFAULT_MAX_EXPANSIONS;
        return AStarSolver.Solve(grid, fromSub, toSub, maxExp);
    }

    /// <summary>
    /// 2_6 P0b：异步分帧寻路入口（排队 + 每帧预算限流 + 票据回调）。
    /// PathFollower 主链仍用同步 FindPathImmediate（稳定）；此入口供编辑器/超大规模接入。
    /// 返回票据；未入队（越界/未就绪/无调度器）时 HasResult=false 且不回调。
    /// </summary>
    public static PathTicket RequestPath(Vector2 fromWorld, Vector2 toWorld, byte priority = 0,
        Action<PathResult> onComplete = null)
    {
        var scheduler = PathfindingScheduler.Instance;
        bool enqueued = false;
        var ticket = (scheduler != null)
            ? scheduler.RequestPath(fromWorld, toWorld, out enqueued, priority,
                t => onComplete?.Invoke(t.HasResult ? t.Result : null))
            : new PathTicket();
        if (!enqueued && onComplete != null) onComplete(null);   // 未入队 → 立即失败回调
        return ticket;
    }

    private static bool GridSystemHasConfig()
        => GridSystem.Instance != null && GridSystem.Instance.Config != null;

    // P0a 展开上限：写死 4096（PathfindingConfig SO 归 2_6 服务化阶段）。
    private static int GridSystemMaxExpansions() => DEFAULT_MAX_EXPANSIONS;
}