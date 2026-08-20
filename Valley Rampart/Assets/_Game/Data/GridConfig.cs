using System;
using UnityEngine;

/// <summary>
/// 区块配置（改造计划 doc 1 §2.3 / §5.6）。Inspector 可调，数据驱动。
/// 资产实例放在 Resources/Grid/GridConfig.asset
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/GridConfig", fileName = "GridConfig")]
public class GridConfig : ScriptableObject
{
    [Header("区块尺寸（doc 1 §1.6：逻辑正交，等轴投影归 2_10）")]
    [Tooltip("世界单位/格，双分量（1.28, 0.64）。禁止任何公式把 cellSize 当标量用；1 小区块 = 1 等轴 Tile = 128×64px @PPU100")]
    public Vector2 cellSize = new Vector2(1.28f, 0.64f);

    [Tooltip("小区块 ÷ 此数 = 微格（寻路粒度，固定 4）。每小区块 = 4×4 = 16 微格（0.32×0.16）")]
    public int subCellDivisor = 4;

    [Header("区块划分")]
    [Tooltip("大区块边长（小区块数）。Chunk 级事件 / 地图生成单位 / 温度带")]
    public int chunkSize = 16;

    [Tooltip("中区块边长（小区块数）。LOD / 热度聚合 / 编队槽位组")]
    public int midChunkSize = 4;

    [Header("堆叠上限（按 NPC 类型）")]
    [Tooltip("只配 Enemy + Civilian 两类（不划分防御类驻军）")]
    public StackLimitConfig[] stackLimits = new StackLimitConfig[2];

    [Header("调试")]
    [Tooltip("Gizmos 调试绘制总开关")]
    public bool drawGizmos = true;

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