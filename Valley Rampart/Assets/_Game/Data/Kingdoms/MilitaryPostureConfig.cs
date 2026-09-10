using UnityEngine;

// ============================================================================
//  军事姿态层配置 SO（2_22 P0 批C / C2，§八 SO 清单；§3.3 姿态三档 D518）。
//  资产路径：Resources/Config/Kingdoms/MilitaryPostureConfig.asset（so-data-driven 禁魔法数）。
//  消费方：MilitaryPostureController（C1 三档升降滞回）+KingdomBrain 姿态执行面
//  （C3 巡逻/C4 驻防/C5 动员）。
//  滞回判据复用 SituationConfig.crisisLine/recoveryLine（批A 已落字段，批A L28/L32
//  注释明示"批C 姿态层动员档消费"——不重复造字段）。
// ============================================================================

[CreateAssetMenu(menuName = "ValleyRampart/MilitaryPostureConfig", fileName = "MilitaryPostureConfig")]
public class MilitaryPostureConfig : ScriptableObject
{
    [Header("警戒档触发（D518 §3.3：邻接威胁非零/边境摩擦）")]
    [Tooltip("邻接威胁任一国战士数达到该值 → 警戒档（0=任何邻接威胁即触发）")]
    public int alertMinThreatWarriors = 1;

    [Header("警戒档行为：巡逻频率（C3 PatrolTaskSystem AI 侧驱动）")]
    [Tooltip("警戒档巡逻士兵目标数（当前巡逻数 < 目标 → 补发巡逻令，方向=主威胁方向；0=警戒档不发巡逻）")]
    public int alertPatrolCount = 2;

    [Header("滞回防抖（C1 验收：阈值附近抖动不切档）")]
    [Tooltip("档位变更滞回窗（日）：距上次档位变更不足该天数时维持现档（第二层滞回；第一层=危机线/恢复线双线带）")]
    public int hysteresisDays = 2;

    [Header("动员档行为：守军集结（C4/C5，FormationController isGarrison 链）")]
    [Tooltip("动员档守军编队目标数（无 isGarrison 守城编队时自动派驻）")]
    public int mobilizeGuardSquads = 1;

    /// <summary>载入军事姿态配置（缺 asset 时回退默认占位实例；对齐 SituationConfig.Load 先例）。</summary>
    public static MilitaryPostureConfig Load()
    {
        var cfg = Resources.Load<MilitaryPostureConfig>("Config/Kingdoms/MilitaryPostureConfig");
        return cfg != null ? cfg : ScriptableObject.CreateInstance<MilitaryPostureConfig>();
    }
}
