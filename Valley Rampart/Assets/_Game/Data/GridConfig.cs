using System;
using UnityEngine;

/// <summary>
/// 区块配置（3.2 第 7.1 节）。Inspector 可调，数据驱动。
/// 资产实例放在 Resources/Grid/GridConfig.asset
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/GridConfig", fileName = "GridConfig")]
public class GridConfig : ScriptableObject
{
    [Header("区块大小")]
    [Tooltip("一个小区块多宽（世界单位/像素）。审计勘误：资产 GridConfig.asset 实际值 2.26，类默认值对齐防误读")]
    public float cellSize = 2.26f;

    [Header("大区块")]
    [Tooltip("一个大区块含多少个小区块（固定）")]
    public int regionCellCount = 16;

    [Header("飞行层")]
    [Tooltip("y 值超过此阈值视为空中层")]
    public float flyHeightThreshold = 5f;
    [Tooltip("空中层固定 y 值")]
    public float flyHeight = 8f;

    [Header("堆叠上限（按 NPC 类型）")]
    [Tooltip("只配 Enemy + Civilian 两类（不划分防御类驻军）")]
    public StackLimitConfig[] stackLimits = new StackLimitConfig[2];

    /// <summary>按类型查堆叠上限。0=无上限。</summary>
    public int GetStackLimit(UnitCategory category)
    {
        for (int i = 0; i < stackLimits.Length; i++)
            if (stackLimits[i].category == category) return stackLimits[i].maxStack;
        return 0;
    }
}

/// <summary>堆叠上限配置项。</summary>
[Serializable]
public struct StackLimitConfig
{
    public UnitCategory category;
    [Tooltip("堆叠上限（0=无上限）")]
    public int maxStack;
}
