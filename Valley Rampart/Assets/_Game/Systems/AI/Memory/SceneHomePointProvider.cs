using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - HomePoint 依赖倒置 P0 占位实现
//  详见 3.0.1_2_输入输出决定层设计.md §9 / 决策11
//  P0: 场景空 Transform 实现；P1: 城墙内驻留点计算器实现
// ============================================================================

/// <summary>
/// HomePoint 依赖倒置 P0 占位实现（§9，决策11）。
/// 场景空 Transform 标记城墙内驻留点，Inspector 拖引用。
/// P1 替换为城墙内驻留点计算器（接建造系统数据，自动算最近驻留点）。
/// </summary>
public class SceneHomePointProvider : MonoBehaviour, IHomePointProvider
{
    [Tooltip("P0 占位：城墙内驻留点空 Transform。P1 替换为城墙计算器")]
    public Transform homePointAnchor;

    public Vector2 GetHomePoint(NPCBrain npc)
    {
        return homePointAnchor != null
            ? (Vector2)homePointAnchor.position
            : Vector2.zero;
    }
}
