using UnityEngine;

/// <summary>
/// NPC 行为执行器接缝（3.5 实施计划 §2.7；王国经营 ↔ 3.0 AI 隔离层）。
/// 接口只定义"行为能做什么"（执行器），不定义"AI 怎么决定做什么"（决策器）。
/// AI 大脑重构只换决策器，执行器与王国玩法不动。
///
/// P0 不实现（纯建筑玩法，建筑自产出无需 NPC）；AI 重构稳定后由 AI 驱动填充。
/// 存档不涉及（P0 无状态）；指派关系若需存，随 AI 定一起设计。
/// </summary>
public interface IWorkerTaskExecutor
{
    /// <summary>指派任务：把工人指派到建筑（后续可换成 AI 自主分配）。</summary>
    void AssignTask(UnitController worker, Building building);

    /// <summary>当前任务描述（null=空闲）。</summary>
    object CurrentTask { get; }

    /// <summary>逐帧驱动执行（移动→站桩→触发产出）。</summary>
    void ExecuteTick(UnitController worker);

    /// <summary>取消当前任务（建筑拆除/工人转职时调）。</summary>
    void CancelTask(UnitController worker);
}