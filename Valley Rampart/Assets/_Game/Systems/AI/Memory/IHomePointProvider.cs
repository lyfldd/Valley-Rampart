// ============================================================================
//  3.0.1_2 输入输出决定层 - HomePoint 依赖倒置接口
//  详见 3.0.1_2_输入输出决定层设计.md §9
//  P0: 场景空 Transform 实现；P1: 城墙内驻留点计算器实现
// ============================================================================

using UnityEngine;

/// <summary>
/// HomePoint 安全点依赖倒置（§9）。
/// P0 由场景空 Transform 实现（SceneHomePointProvider），P1 替换为城墙内驻留点计算器。
/// NPCBrain 依赖此接口而非具体实现，解除对建造系统的耦合。
/// </summary>
public interface IHomePointProvider
{
    /// <summary>获取指定 NPC 的安全点（城墙内驻留点）</summary>
    Vector2 GetHomePoint(NPCBrain npc);
}
