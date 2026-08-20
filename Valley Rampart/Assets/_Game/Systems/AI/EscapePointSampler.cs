using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2_7 §5.3 逃逸点采样（Unity 侧，新增决策输入）。
/// 撤退落点：利用「最近威胁方向 + 多敌分布」往敌稀疏/开口侧采样一个可走路程 R 的逃逸点，
/// 供 Executor 战略撤退经 PathFollower 寻路到达（第四节已闭环）。
///
/// ⚠️ sim 差距（北极星红线）：本算法输入「最近威胁方向 NearestEnemyDir」「多敌方向分布」与
/// 逃逸点评分（directionSectorWeight 扇区加权）是 ai决策大脑强化训练 侧没有的新决策输入，
/// 训练回灌 champion 参数语义会错位。已在 15_训练侧harness与Unity端差距 文档登记（附加条件②）。
/// </summary>
public static class EscapePointSampler
{
    /// <summary>
    /// 采样逃逸点。附近无敌 → 返回 false（调用方回退原安全锚点逻辑）。
    /// </summary>
    /// <param name="selfPos">自我位置（世界）</param>
    /// <param name="enemyPositions">感知内敌人位置（世界，用于多敌分布避让）</param>
    /// <param name="retreatRadiusWorld">理想撤退距离（世界 = baseRetreatCells×cellSize）</param>
    /// <param name="cfg">RetreatConfig（采样参数）</param>
    public static bool TryPick(Vector2 selfPos, IReadOnlyList<Vector2> enemyPositions,
        float retreatRadiusWorld, out Vector2 escapePoint)
    {
        escapePoint = selfPos;
        if (enemyPositions == null || enemyPositions.Count == 0) return false;

        var rc = RetreatConfig.Instance;
        if (rc == null) return false;

        // 期望逃逸方向 = 各敌「背离方向/距离」加权叠加（近敌权重大；三面包围时开口侧无贡献 → 质心偏向开口）
        Vector2 flee = Vector2.zero;
        for (int i = 0; i < enemyPositions.Count; i++)
        {
            Vector2 d = selfPos - enemyPositions[i];
            float dist = d.magnitude;
            if (dist < 0.01f) continue;
            flee += d / (dist * dist);   // 越近权重越强
        }
        if (flee.sqrMagnitude < 1e-4f)
        {
            // 对称围死（四面包围）：极罕见，强行用最近敌方向反而不逃就回退
            return false;
        }
        flee.Normalize();

        // 采样圈 × 方向（§5.3）：基础沿期望逃逸方向，weights 扇区加权 directionSectorWeight 放大期望扇区
        int rings = Mathf.Max(1, rc.sampleRings);
        int dirs = Mathf.Max(1, rc.directionsPerRing);
        float half = rc.sectorHalfAngleDeg * Mathf.Deg2Rad;
        float bestScore = float.PositiveInfinity;
        Vector2 best = selfPos;
        bool found = false;
        for (int r = 0; r < rings; r++)
        {
            float ringR = retreatRadiusWorld * (0.6f + 0.2f * r);  // 0.6/0.8/1.0
            for (int s = 0; s < dirs; s++)
            {
                float angle = (s / (float)dirs) * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // 扇区加权：与期望逃逸方向夹角越小越优先（directionSectorWeight 来自 CostBiasConfig，sim 无的新输入）
                float angDiff = Mathf.Abs(Mathf.Atan2(Vector2.Dot(perp(flee), dir),
                    Vector2.Dot(flee, dir)));
                float sectorW = 1f - SectorWeight * (angDiff / Mathf.PI);  // 期望扇区内权重最高
                sectorW = Mathf.Clamp(sectorW, 0.2f, 1f);

                Vector2 cand = selfPos + dir * ringR;
                if (!IsWalkable(cand)) continue;  // 不可走直接跳过
                // 该方向敌人威胁（避敌）：扇区内敌人越近得分越高（惩罚)
                float threat = 0f;
                for (int e = 0; e < enemyPositions.Count; e++)
                {
                    Vector2 toEnemy = cand - enemyPositions[e];
                    float ed = toEnemy.magnitude;
                    if (ed < 0.01f) continue;
                    Vector2 edir = toEnemy / ed;
                    if (Vector2.Dot(edir, dir) > 0.3f)   // 采样方向朝向某个敌人
                        threat += 1f / ed;               // 敌人惩罚
                }
                float distBias = Mathf.Abs(ringR - retreatRadiusWorld) * rc.distBiasWeight;
                float score = sectorW / Mathf.Max(0.01f, threat) + distBias;
                if (score < bestScore)
                {
                    bestScore = score; best = cand; found = true;
                }
            }
        }
        if (found) { escapePoint = best; return true; }
        // 无任何可走采样点：沿期望逃逸方向逐步缩半径找最近可走点
        for (float rr = retreatRadiusWorld; rr > 0.5f; rr -= 0.5f)
        {
            Vector2 cand = selfPos + flee * rr;
            if (IsWalkable(cand)) { escapePoint = cand; return true; }
        }
        return false;
    }

    private static Vector2 perp(Vector2 v) => new Vector2(-v.y, v.x);

    /// <summary>方向扇区加权（2_7 §三 CostBiasConfig 占位 1.0，2_9 入训后替换；sim 侧无此输入）。</summary>
    private static float SectorWeight
        => CostBiasConfig.Instance != null ? Mathf.Max(0f, CostBiasConfig.Instance.directionSectorWeight) : 1f;

    private static bool IsWalkable(Vector2 pos)
    {
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null) return true;
        var c = grid.WorldToCoord(pos);
        return c != null && grid.IsWalkable(c.Value);
    }
}