using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - HomePoint 依赖倒置 P0 占位实现
//  详见 3.0.1_2_输入输出决定层设计.md §9 / 决策11
//  P0: 场景空 Transform 实现；P1: 城墙内驻留点计算器实现
//  3.0.1_6 §4.1：HomePoint 分阵营——敌方（Undead）回敌方侧锚点，不再逃往人类城墙
// ============================================================================

/// <summary>
/// HomePoint 依赖倒置 P0 占位实现（§9，决策11）。
/// 场景空 Transform 标记城墙内驻留点，Inspector 拖引用。
/// P1 替换为城墙内驻留点计算器（接建造系统数据，自动算最近驻留点）。
/// </summary>
public class SceneHomePointProvider : MonoBehaviour, IHomePointProvider
{
    [Tooltip("P0 占位：我方（Human_Player）城墙内驻留点空 Transform。P1 替换为城墙计算器")]
    public Transform homePointAnchor;

    [Tooltip("3.0.1_6 §4.1：敌方（Undead）侧锚点，地图右侧敌方出生地。修敌逃往人类城墙/漫游人类城墙/归巢站人类城墙")]
    public Transform enemyHomePointAnchor;

    /// <summary>
    /// 按阵营分流返回 HomePoint（3.0.1_6 §4.1）：
    /// 我方（Human_Player/None）→ 城墙内驻留点；敌方（Undead）→ 敌方侧锚点。
    /// 敌方锚点未配置时回退到 homePointAnchor（行为退化到现状，不 NRE）。
    /// </summary>
    public Vector2 GetHomePoint(NPCBrain npc)
    {
        if (npc != null)
        {
            var unit = npc.GetComponent<UnitController>();
            bool isEnemy = unit != null && unit.Data != null && unit.Data.faction == Faction.Undead;
            var anchor = isEnemy ? enemyHomePointAnchor : homePointAnchor;
            return anchor != null ? (Vector2)anchor.position : Vector2.zero;
        }
        return homePointAnchor != null ? (Vector2)homePointAnchor.position : Vector2.zero;
    }
}
