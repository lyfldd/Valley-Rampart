using System;
using UnityEngine;

/// <summary>
/// 主城解锁表（3.5 §2.1：主城升级 → 按表跨级解锁模块等级）。
/// 每行 = 某个主城等级达到后新解锁的 [module, lv] 列表。
/// 跨级示例：科技 Lv1 在主城 Lv2 解锁（requiredCastleLevel=2），生产 Lv3 在主城 Lv4 解锁。
/// 资产路径：Resources/Config/CastleUnlockTable.asset。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/CastleUnlockTable", fileName = "CastleUnlockTable")]
public class CastleUnlockTable : ScriptableObject
{
    [Tooltip("主城达到对应等级后解锁的模块等级节点（可多行，覆盖 1..6）")]
    public CastleUnlockRow[] rows;

    /// <summary>统计给定主城等级下，某模块达到的最高等级（含跨级）。</summary>
    public int GetModuleLevel(ModuleType module, int castleLevel)
    {
        int max = 0;
        if (rows == null) return max;
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row.castleLevel <= 0 || row.castleLevel > castleLevel) continue;
            if (row.unlocks == null) continue;
            for (int j = 0; j < row.unlocks.Length; j++)
            {
                var u = row.unlocks[j];
                if (u.module == module && u.level > max) max = u.level;
            }
        }
        return max;
    }
}

/// <summary>主城解锁表行：castleLevel → 解锁的模块等级节点列表。</summary>
[Serializable]
public class CastleUnlockRow
{
    public int castleLevel;                 // 主城等级（1..6）
    public ModuleUnlockEntry[] unlocks;     // 该级解锁的模块节点
}

/// <summary>单个模块等级解锁条目。</summary>
[Serializable]
public struct ModuleUnlockEntry
{
    public ModuleType module;               // 模块
    public int level;                       // 解锁到的模块等级
}