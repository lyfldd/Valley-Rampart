using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2_7 步骤8 迷雾视野（D262 本篇新建，Unity 侧）。
/// 决策核只读视野内目标：各单位每 tick 上报自我视野（并集，D167），维护探索标记（小区块 D173，
/// maxExploredCells 上限 128²=16384），感知查询前过滤视野外敌人（D169）。
/// 视野不阻塞寻路（D170）——本系统不做任何可通行性标记。渲染归 2_10。
///
/// ⚠️ sim 对称（D169）：sim 侧也需模拟单位视野使训练环境=真实环境（训练侧另行实现）。
/// </summary>
public static class VisionSystem
{
    private static readonly Dictionary<long, bool> _explored = new Dictionary<long, bool>(4096);
    private static bool _dirty;   // 供给 Reset/外界探测用，当前无渲染不消费

    /// <summary>上报自我视野（单位每帧，扩成单位视野并集）。self→圆覆盖的格标探索。</summary>
    public static void MarkExplored(Vector2 selfPos, float radiusWorld)
    {
        var cfg = VisionConfig.Instance;
        if (cfg == null || !cfg.enabled) return;   // 无 asset/关闭 = 迷雾禁用（回退基线）
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null) return;
        _dirty = true;

        float cs = Mathf.Max(0.01f, grid.Config.cellSize.x);
        int range = Mathf.CeilToInt(radiusWorld / cs);
        var center = grid.WorldToCoord(selfPos);
        if (center == null) return;

        int cx = center.Value.x, cy = center.Value.y;
        int maxCells = (cfg != null && cfg.maxExploredCells > 0) ? cfg.maxExploredCells : 16384;
        for (int dy = -range; dy <= range; dy++)
        {
            for (int dx = -range; dx <= range; dx++)
            {
                // 圆近似（方块内方形裁剪；粗粒度足够决策过滤，含角归不探索视觉差异可接受）
                if (dx * dx + dy * dy > range * range) continue;
                long key = Key(cx + dx, cy + dy);
                if (_explored.Count >= maxCells) return;   // D173 上限保护
                _explored[key] = true;
            }
        }
    }

    /// <summary>感知过滤：位置是否可见/已被探索。关闭（enabled=false）恒返回 true（全可见，回退基线）。</summary>
    public static bool IsExplored(Vector2 pos)
    {
        var cfg = VisionConfig.Instance;
        if (cfg == null || !cfg.enabled) return true;   // 无 asset/关闭 = 全可见（回退基线）
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null) return true;
        var c = grid.WorldToCoord(pos);
        if (c == null) return true;
        return _explored.TryGetValue(Key(c.Value.x, c.Value.y), out bool e) && e;
    }

    public static void Clear() { _explored.Clear(); _dirty = false; }

    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
}