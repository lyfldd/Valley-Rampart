using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 空间分区感知查询系统（3.0.1 第 7.1 节）。
/// 复用 GridSystem 区块结构，提供高效的"按半径查单位"接口。
///
/// 核心原理：不是 NPC 每帧扫描全图敌人，而是通过空间分区只查感知半径覆盖的格子。
/// 查格数 n = ceil(R / cellSize)，查 (2n+1) × 2 格（1D 横版 + 双层）。
/// 无敌人移动时，感知开销为零。
///
/// 首版为 NPC 主动查询模式（NPCBrain 调 QueryNearby），未来可扩展为敌人主动广播模式。
/// </summary>
public static class PerceptionSystem
{
    /// <summary>
    /// 查询指定位置周围的单位（空间分区，复用 GridSystem）。
    /// findEnemies=true 查敌方，false 查友方。
    /// </summary>
    public static void QueryNearby(
        Vector2 position,
        float radiusWorld,
        Faction myFaction,
        bool findEnemies,
        List<IDamageable> results)
    {
        results.Clear();

        if (GridSystem.Instance == null || GridSystem.Instance.Config == null)
        {
            FallbackQuery(position, radiusWorld, myFaction, findEnemies, results);
            return;
        }

        float cellSize = GridSystem.Instance.Config.cellSize;
        GridCoord center = GridSystem.Instance.WorldToCoord(position);
        int cellRange = Mathf.Max(1, Mathf.CeilToInt(radiusWorld / cellSize));

        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            for (int y = 0; y <= 1; y++)
            {
                var cellUnits = GridSystem.Instance.GetUnitsInCell(new GridCoord(center.x + dx, y));
                for (int i = 0; i < cellUnits.Count; i++)
                {
                    var unit = cellUnits[i];
                    if (unit == null || !unit.IsAlive) continue;

                    Faction f = unit.GetFaction();
                    bool isEnemy = f != myFaction && f != Faction.None;
                    bool isAlly = f == myFaction;

                    if (findEnemies && !isEnemy) continue;
                    if (!findEnemies && !isAlly) continue;

                    float dist = Vector2.Distance(position, unit.GetPosition());
                    if (dist <= radiusWorld)
                        results.Add(unit);
                }
            }
        }
    }

    /// <summary>GridSystem 不可用时的线性扫描兜底。</summary>
    private static void FallbackQuery(
        Vector2 position,
        float radiusWorld,
        Faction myFaction,
        bool findEnemies,
        List<IDamageable> results)
    {
        if (UnitRegistry.Instance == null) return;

        var allUnits = UnitRegistry.Instance.GetAllUnits();
        foreach (var unit in allUnits)
        {
            if (unit == null || !unit.IsAlive) continue;

            Faction f = unit.GetFaction();
            bool isEnemy = f != myFaction && f != Faction.None;
            bool isAlly = f == myFaction;

            if (findEnemies && !isEnemy) continue;
            if (!findEnemies && !isAlly) continue;

            float dist = Vector2.Distance(position, unit.GetPosition());
            if (dist <= radiusWorld)
                results.Add(unit);
        }
    }
}
