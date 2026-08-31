using UnityEngine;

// ============================================================================
//  模拟粒度切换（SimMode）判定配置（2_17 步骤13，D333/D344；so-data-driven 铁律，禁魔法数）
//  资产路径：Resources/Config/Kingdoms/SimModeConfig.asset
//  字段对齐 2_17 实施计划 §三 SimModeConfig 表（仅两字段；「视野」信号源=LODSystem 活跃带，
//  D344 无额外参数）。
// ============================================================================

/// <summary>SimMode 判定配置（2_17 步骤13）。</summary>
[CreateAssetMenu(menuName = "ValleyRampart/Kingdoms/SimModeConfig", fileName = "SimModeConfig")]
public class SimModeConfig : ScriptableObject
{
    [Header("SimMode 判定（D333）")]
    [Tooltip("连续 N 日未被 LOD 活跃带覆盖 → 切 Abstract（迟滞防边界抖动，默认 2 日）")]
    public int offscreenDaysToAbstract = 2;
    [Tooltip("领土内出现战斗热点 → 强制切 Fine（战斗锁：军队打到家门口，工人逃跑/救火必须真跑）")]
    public bool combatHotspotForceFine = true;
}
