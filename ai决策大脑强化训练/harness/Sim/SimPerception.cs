// ============================================================================
//  M2 Headless 模拟器 - SimPerception 空间分区感知查询（复刻 PerceptionSystem）
//  04_模拟器规格.md §三 感知：PerceptionSystem.cs:20-62 —— 格子粗筛+距离精判，敌=f!=mine && f!=None。
//  真身对照 PerceptionSystem.QueryNearby（Unity 版）：
//    - cellRange = Max(1, Ceil(radiusWorld / cellSize))（PerceptionSystem.cs:37）
//    - 扫描 (2n+1)×2 格；sim 无 y 层，只扫 cy=0（04 §一 压平）
//    - 敌人判定：f != myFaction && f != Faction.None；友军判定：f == myFaction（:50-54）
//    - 距离精判：Vector2.Distance <= radiusWorld 才入结果（:56-58）
//    - 结果顺序 = dx 扫描序 × 格内进入序（确定性关键，04 §七）
//  感知频率：NPCBrain 每 2 tick（0.2s）调一次（perceptionUpdateInterval=0.2）。
//  查询端口用 IWorldQuery（核内接口，接缝 3）——SimWorld 实现之，SimBrain 只依赖核 Ports。
// ============================================================================

using System.Collections.Generic;

/// <summary>
/// 空间分区感知查询（复刻 PerceptionSystem 的 QueryNearby，1D 版）。
/// 不是每帧全图扫描，而是只查感知半径覆盖的格子（查格数 n = ceil(R/cellSize)）。
/// </summary>
public static class SimPerception
{
    /// <summary>
    /// 查询指定位置周围的单位（空间分区）。
    /// findEnemies=true 查敌方，false 查友方。结果写入 results（先 Clear）。
    /// </summary>
    public static void QueryNearby(
        Vector2X position,
        float radiusWorld,
        Faction myFaction,
        bool findEnemies,
        IWorldQuery world,
        List<IUnitHandle> results)
    {
        results.Clear();

        float cellSize = world.CellSize;
        int centerCellX = (int)System.Math.Floor(position.x / cellSize);
        int cellRange = MathfX.Max(1, MathfX.CeilToInt(radiusWorld / cellSize));

        var buffer = new List<IUnitHandle>();
        for (int dx = -cellRange; dx <= cellRange; dx++)
        {
            world.QueryUnitsInCell(centerCellX + dx, 0, buffer);   // cy 恒 0（1D 压平）
            for (int i = 0; i < buffer.Count; i++)
            {
                var unit = buffer[i];
                if (unit == null || !unit.IsAlive) continue;

                Faction f = unit.Faction;
                bool isEnemy = f != myFaction && f != Faction.None;
                bool isAlly = f == myFaction;

                if (findEnemies && !isEnemy) continue;
                if (!findEnemies && !isAlly) continue;

                float dist = Vector2X.Distance(position, unit.Position);
                if (dist <= radiusWorld)
                    results.Add(unit);
            }
        }
    }
}
