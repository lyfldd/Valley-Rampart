using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - BehaviorExecutor 事件
//  详见 3.0.1_2_输入输出决定层设计.md §13.4
//  Executor 事件双层分发：Executor -> NPCBrain 本地(主) -> EventBus(辅)
//  全部 readonly struct（EventBus 泛型约束 where T:struct）
// ============================================================================

/// <summary>
/// Executor 事件接收者接口（§13.4 双层分发的本地主路径）。
/// NPCBrain 实现此接口，BehaviorExecutor 检测到事件时同步调用（同帧可靠不丢）。
/// NPCBrain 处理后再 Publish 到 EventBus（辅，供调度/调试/音效订阅）。
/// </summary>
public interface IExecutorEventReceiver
{
    void OnArrived(Vector2 position, BehaviorModule fromModule);
    void OnMoveComplete(Vector2 position);
    void OnAnchorLost();
}

/// <summary>Executor 到达焦点目标事件（MoveTowards/WorkAt 到达）</summary>
public readonly struct ExecutorArrivedEvent
{
    public readonly NPCBrain Brain;
    public readonly Vector2 Position;
    public readonly BehaviorModule Module;
    public ExecutorArrivedEvent(NPCBrain brain, Vector2 position, BehaviorModule module)
    { Brain = brain; Position = position; Module = module; }
}

/// <summary>Executor 移动完成事件（RetreatMove 撤完/Idle 时长满，触发 Caution 计时起点）</summary>
public readonly struct ExecutorMoveCompleteEvent
{
    public readonly NPCBrain Brain;
    public readonly Vector2 Position;
    public ExecutorMoveCompleteEvent(NPCBrain brain, Vector2 position)
    { Brain = brain; Position = position; }
}

/// <summary>Executor 锚点丢失事件（FollowAnchor 锚点死亡）</summary>
public readonly struct ExecutorAnchorLostEvent
{
    public readonly NPCBrain Brain;
    public ExecutorAnchorLostEvent(NPCBrain brain) { Brain = brain; }
}
