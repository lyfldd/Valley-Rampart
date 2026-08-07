using System;

/// <summary>
/// 任务状态（QQQ.2 §10.4 任务执行）。任务态由 NPC 自身维护（npc.currentTask 引用 KingdomTask），
/// 调度器按此枚举查询工人是否"在场"（DR-19：仅 Working 算在场）。
/// </summary>
public enum TaskState
{
    None,          // 未分配任务
    Assigned,      // 已派发，尚未开始移动
    MovingToSource,// 正前往任务源
    Working,       // 在任务点执行（在场，DR-19 判定依据）
    MovingToDest,  // 任务完成，正搬运/前往终点
    Completed,     // 已完成
    Abandoned      // 已放弃（NPC 死亡/中断/被招募走）
}