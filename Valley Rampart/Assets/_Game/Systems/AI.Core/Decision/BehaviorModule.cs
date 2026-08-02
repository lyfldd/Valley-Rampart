// ============================================================================
//  AI.Core Decision - 行为模块枚举（从壳 Decision/BehaviorModule.cs 迁入）
//  03_大脑提取与双适配工程.md 迁移 6 步·步3。命名空间不变（全局）。
// ============================================================================

/// <summary>
/// 5 个通用行为模块（§2 词汇表）。
/// 所有职业共用，职业差异通过 L3 参数体现，不新增模块。
/// 新增行为需求先问"能否用现有 5 模块 + 参数组合表达"，模块表扩张需文档级评审。
/// </summary>
public enum BehaviorModule
{
    /// <summary>常规位移：目标位置 + 速度</summary>
    MoveTowards,
    /// <summary>撤退位移：战略(目标位置) / 战术(方向+距离)</summary>
    RetreatMove,
    /// <summary>工作循环：砍/挖/建占位，首版"位移即目的"</summary>
    WorkAt,
    /// <summary>跟随动态锚点：每 tick 随锚点刷新</summary>
    FollowAnchor,
    /// <summary>原地待机：警戒驻留 / 无焦点兜底</summary>
    Idle,
    /// <summary>漫游（3.0.1_4 §6.3）：HomePoint 周围随机小幅走动，到点停一下再取新点</summary>
    Wander
}
