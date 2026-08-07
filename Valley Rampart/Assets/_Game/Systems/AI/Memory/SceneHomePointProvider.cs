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
    /// 按阵营分流返回 HomePoint（3.0.1_6 §4.1 + QQQ.1 需求2+3）：
    /// 我方（Human_Player/None）→ 主城坐标（WorldManager.GetKingdomAnchorWorld，城堡中心）；
    /// 敌方（Undead）→ 敌方侧锚点。
    /// 修复：不再读场景手配 homePointAnchor（与主城脱节），也杜绝往世界 (0,0) 乱走。
    /// </summary>
    public Vector2 GetHomePoint(NPCBrain npc)
    {
        if (npc != null)
        {
            var unit = npc.GetComponent<UnitController>();
            bool isEnemy = unit != null && unit.Data != null && unit.Data.faction == Faction.Undead;
            if (isEnemy)
                return enemyHomePointAnchor != null ? (Vector2)enemyHomePointAnchor.position : ResolveKingdomAnchor();
            return ResolveKingdomAnchor();
        }
        return ResolveKingdomAnchor();
    }

    /// <summary>
    /// 人类阵营 HomePoint = 主城（废弃城堡中心）。QQQ.1 需求3：避免 Vector2.zero 哨兵被当真坐标。
    /// WorldManager 未就绪时 Debug.LogError 暴露初始化时序问题（而非静默退化为 0,0）。
    /// </summary>
    Vector2 ResolveKingdomAnchor()
    {
        if (WorldManager.Instance != null)
        {
            var p = WorldManager.Instance.GetKingdomAnchorWorld();
            if (p != Vector2.zero) return p;
            Debug.LogError("[SceneHomePointProvider] WorldManager 未初始化，GetKingdomAnchorWorld 返回 (0,0)，HomePoint 将为 0,0（时序问题需排查）");
            return p;
        }
        Debug.LogError("[SceneHomePointProvider] WorldManager 未初始化，无法解析 HomePoint");
        return Vector2.zero;
    }
}
