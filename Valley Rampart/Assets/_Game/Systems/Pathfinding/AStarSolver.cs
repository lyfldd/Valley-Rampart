using System.Collections.Generic;

// ============================================================================
//  2_6 P0a 微格 A*：8 向确定性求解器（纯 C#，接 IPathGrid）。
//  输入：IPathGrid（微格坐标）、from/to、maxExpansions。
//  输出：PathResult（status + waypoints 微格序列）。
//  平局/邻居序固定（GridMathCore.NeighborOffsets8），禁字典遍历序参与，确定性 R3。
// ============================================================================

public static class AStarSolver
{
    /// <summary>微格 8 向 A*（P0a 核心）。同步求解，供 FindPathImmediate / PathFollower 调用。</summary>
    public static PathResult Solve(IPathGrid grid, GridCoord from, GridCoord to, int maxExpansions)
    {
        var gScore = new Dictionary<GridCoord, float>();
        var cameFrom = new Dictionary<GridCoord, GridCoord>();

        var open = new BinaryHeap();
        gScore[from] = 0f;
        open.Push(from, H(from, to));

        GridCoord bestNode = from;      // 已发现格中 g 最小者（unreachable 就近兜底）
        float bestF = float.MaxValue;
        int expanded = 0;

        while (open.Count > 0)
        {
            GridCoord current; float curF;
            open.TryPop(out current, out curF);
            // 懒删除：若该条 f 已被更新（过期），丢弃
            if (!Equivalent(gScore[current] + H(current, to), curF)) continue;
            expanded++;

            if (expanded > maxExpansions)
                return MakeResult(PathStatus.Partial, cameFrom, current, reachedExact: false);

            if (current == to)
                return MakeResult(PathStatus.Ready, cameFrom, current, reachedExact: true);

            float currentG = gScore[current];
            if (currentG < bestF) { bestF = currentG; bestNode = current; }

            var offs = GridMathCore.NeighborOffsets8;
            for (int k = 0; k < offs.Length; k++)
            {
                GridCoord nb = new GridCoord(current.x + offs[k].x, current.y + offs[k].y, current.layer);
                if (!grid.IsWalkable(nb)) continue;
                bool diag = offs[k].x != 0 && offs[k].y != 0;
                if (diag && !grid.IsDiagonalMoveAllowed(current, nb)) continue;   // 防穿角（R4）
                float stepCost = grid.GetEnterCost(nb) * (diag ? GridMathCore.DiagonalCost : 1f);
                float tentative = currentG + stepCost;
                float nextG;
                if (gScore.TryGetValue(nb, out nextG) && tentative >= nextG) continue;   // 非改进
                gScore[nb] = tentative;
                cameFrom[nb] = current;
                open.Push(nb, tentative + H(nb, to));
            }
        }

        // 目标不可达：返回最近可达点前缀
        return MakeResult(PathStatus.Unreachable, cameFrom, bestNode, reachedExact: false);
    }

    private static bool Equivalent(float a, float b) => System.Math.Abs(a - b) < 1e-6f;

    private static float H(GridCoord a, GridCoord b)
        => GridMathCore.Octile(a.x, a.y, b.x, b.y);

    /// <summary>从 cameFrom 回溯构造 waypoints（reached 微格 → from，逆序翻转）。</summary>
    private static PathResult MakeResult(PathStatus status, Dictionary<GridCoord, GridCoord> cameFrom,
                                          GridCoord reached, bool reachedExact)
    {
        var path = new List<GridCoord>();
        GridCoord cur = reached;
        while (true)
        {
            path.Add(cur);
            GridCoord prev;
            if (!cameFrom.TryGetValue(cur, out prev)) break;
            cur = prev;
        }
        path.Reverse();   // from → reached
        return new PathResult
        {
            status = status,
            waypoints = path.ToArray(),
            reachedExactGoal = reachedExact,
        };
    }
}