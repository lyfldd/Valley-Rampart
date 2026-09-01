using UnityEngine;

// ============================================================================
//  出生/目标落点就近可走吸附（寻路2 / HH.48，清偿 HH.47 §五-1a/1b）。
//  背景：出生环与游走目标可落在 flags=None 不可走格口袋（实测 seed=20260901 cell(126,119)，
//  npcId 29 困死：起点无可用邻居→任何目标 Unreachable→PathFailed→Idle 死循环）。
//
//  两个消费口：
//    a) 出生链吸附——PopulationSystem（玩家开局/繁殖 Child）、KingdomFoundry（AI 工人）、
//       VagrantCampSystem（流民）；机器工事/怪物链不接入（落建筑格为合法语义/域外）。
//    b) A* 目标格 snap——PathfindingService 目标不可走→snap 最近可走微格再求解（就近可达语义）。
//
//  确定性：环形扫描固定序（距离优先→同行 dy 外→内按 dx 升序）零随机——
//  同 seed 复现链（2_17 2b ③-a / Smoke_14 #3#12）两轮 snap 结果逐字节一致，不破坏。
//  半径有界（MaxCellRadius 防跨图吸附）；界内无可走格→告警+原样返回（调用方感知困死风险）。
// ============================================================================

/// <summary>落点就近可走吸附器（静态纯函数，仅消费 GridSystem IPathGrid 查询）。</summary>
public static class SpawnPosSnapper
{
    /// <summary>吸附扫描半径（宏格）。兜底防御参数（非玩法数值）暂 const；如需可调配位归 SO（待裁决）。</summary>
    public const int MaxCellRadius = 4;

    /// <summary>
    /// 出生/落点吸附（世界坐标入口）。本格可走→原样返回（零位移，负探针保障）；
    /// 不可走→最近可走宏格中心微格世界坐标；网格未就绪/越界→原样返回；
    /// 界内无可走格→告警+原样返回（单位有困死风险，日志暴露）。
    /// verbose=false 用于高频调用口（PathFollower.SetDestination 每 tick 触达），静默不刷日志。
    /// </summary>
    public static Vector2 SnapWorld(Vector2 worldPos, string context = null, bool verbose = true)
    {
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null) return worldPos;   // 网格未就绪：原样（锚点校验在调用方上游）
        var subOpt = grid.WorldToSubCoord(worldPos);
        if (!subOpt.HasValue) return worldPos;                       // 网格外：原样（不应发生，日志归调用方）
        if (grid.IsSubWalkable(subOpt.Value)) return worldPos;       // 本格可走：零吸附

        var snapped = SnapSubCore(grid, subOpt.Value);
        if (snapped.HasValue)
        {
            var w = grid.SubCoordToWorld(snapped.Value);
            if (verbose)
                Debug.Log($"[SpawnPosSnapper] 落点吸附{(context != null ? $"（{context}）" : "")}：" +
                          $"{worldPos} → {w}（原格不可走，吸附微格=({snapped.Value.x},{snapped.Value.y})）");
            return w;
        }
        if (verbose)
            Debug.LogWarning($"[SpawnPosSnapper] 吸附失败{(context != null ? $"（{context}）" : "")}：" +
                             $"{worldPos} 半径 {MaxCellRadius} 格内无可走格，原样落点（有困死风险）");
        return worldPos;
    }

    /// <summary>
    /// 微格吸附（A* 目标格入口）。本格可走→原样；不可走→最近可走微格；网格未就绪→null（调用方走原失败链）。
    /// </summary>
    public static GridCoord? SnapSub(GridCoord sub)
    {
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null) return null;
        if (grid.IsSubWalkable(sub)) return sub;
        return SnapSubCore(grid, sub);
    }

    /// <summary>宏格环形扫描最近可走格（距离优先→同行 dx 升序，确定性），返回该格中心微格。</summary>
    private static GridCoord? SnapSubCore(GridSystem grid, GridCoord sub)
    {
        int div = grid.Config.subCellDivisor > 0 ? grid.Config.subCellDivisor : 4;
        var cell = grid.SubToCell(sub);
        for (int r = 0; r <= MaxCellRadius; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;   // 环序（距离优先）
                    var c = new GridCoord(cell.x + dx, cell.y + dy);
                    if (!grid.IsInBounds(c)) continue;
                    // 与 IsSubWalkable 同语义（地形可走+非障碍）；扫描在宏格粒度（障碍即整格不可走，同 2_6 口径）
                    if (grid.IsWalkable(c) && !grid.IsObstacle(c))
                        return new GridCoord(c.x * div + div / 2, c.y * div + div / 2);
                }
            }
        }
        return null;
    }
}
