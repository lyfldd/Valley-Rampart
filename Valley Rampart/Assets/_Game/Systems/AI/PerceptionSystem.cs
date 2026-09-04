using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 感知查询系统（3.0.1 第 7.1 节；2_21 阶段A/D485 修订 2026-09-04）。
/// 单遍过滤法：遍历 UnitRegistry 全体存活单位一遍 → 阵营过滤 → 世界坐标欧氏圆判定。
///
/// 修订背景（2_21 §三 D485）：旧主路径按 GridSystem 格扫描 for dx × for y ∈ {0,1}——y 双行
/// =1D 横版"双层"残留，2.5D 全图单位仅最南两行可被发现（GameScene 实跑失明；纯逻辑冒烟走
/// FallbackQuery 故全绿掩盖）。方格遍历弃用原因：GetUnitsInCell 每次调用 O(N)/格，
/// (2n+1)² 平方级放大不可接受；单遍 O(N) 与 FallbackQuery/D468 野性扫描同构（现役验证模式）。
/// 性能账：活跃带 300~400 单位 × 感知节流（保底 0.5s+LOD 降频）量级可接受；
/// 空间哈希分桶列 P2 优化（非本批，见 2_21 §三.2）。
/// </summary>
public static class PerceptionSystem
{
    /// <summary>
    /// 查询指定位置周围的单位（单遍过滤法）。
    /// findEnemies=true 查敌方（异阵营且非 None），false 查友方（同阵营）。
    /// UnitRegistry 缺失 → 返回空（原 FallbackQuery 语义已合并，分支删除）。
    /// </summary>
    public static void QueryNearby(
        Vector2 position,
        float radiusWorld,
        Faction myFaction,
        bool findEnemies,
        List<IDamageable> results)
    {
        results.Clear();

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
