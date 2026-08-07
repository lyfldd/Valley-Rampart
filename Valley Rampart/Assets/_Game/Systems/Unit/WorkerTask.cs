using UnityEngine;

// ============================================================================
//  王国任务工厂 WorkerTask（QQQ.2 T18 / DR-2：内化为 TaskStimulus 工厂，无双轨）
//  权威规格：QQQ.2_NPC任务修正以及一些小问题.md §10.4 + DR-2
//  原独立状态机（Assigned→MovingToSource→Working→MovingToDest）已移除：
//    任务推进统一由 TaskScheduler 驱动（简化状态机 Assigned→MovingToSource→Working→Completed），
//    任务态由 TaskScheduler._npcStateMap（TaskState 枚举）维护。
//  本类退化为 KingdomTask 构造器 + 携带量查表（P1-8），供调试/外部入口构造任务。
//  威胁挂起（T-K/T-R）：移动抢占由注意力系统天然处理（Threat 层 > Task 层，NPC 去战斗）；
//    Working 计时冻结由 TaskScheduler.Working 分支按 ThreatFactor 实现。
// ============================================================================

/// <summary>任务类型：Gather=采集 / Transport=运输（调试/工厂构造用，对齐 SimTaskType）。</summary>
public enum WorkerTaskType
{
    Gather,
    Transport,
}

/// <summary>
/// 王国任务工厂（QQQ.2 T18 / DR-2）：WorkerTask 内化，退化为 TaskStimulus/KingdomTask 构造器。
/// 不再挂组件驱动状态机；构造 KingdomTask 后交 TaskScheduler.DispatchExternal 派发。
/// </summary>
public static class WorkerTask
{
    private static ResourceCarryConfig _carryConfig;

    /// <summary>按资源类型查一次搬运携带量（ResourceCarryConfig SO，P1-8）。</summary>
    public static int GetCarryAmount(ResourceType type)
    {
        if (_carryConfig == null)
            _carryConfig = Resources.Load<ResourceCarryConfig>("Config/ResourceCarryConfig");
        return _carryConfig != null ? _carryConfig.GetCarryAmount(type) : 10;
    }

    /// <summary>
    /// 构造调试用王国任务（AIDebugSpawnController 验证入口用）。
    /// Gather → 采集（完成时资源入国库）；Transport → 搬运到指定 destPos。
    /// workDuration 由调度器统一配置（TaskScheduler.workDuration），本参数仅兼容旧调试签名。
    /// </summary>
    public static KingdomTask CreateTask(WorkerTaskType type, Vector2 sourcePos, Vector2 destPos,
        float workDuration, ResourceType carryResource)
    {
        var src = new DebugTaskSource(sourcePos);
        var task = new KingdomTask(
            type == WorkerTaskType.Gather ? KingdomTaskType.Gather : KingdomTaskType.Transport,
            src, 1f);
        task.destType = type == WorkerTaskType.Gather
            ? KingdomDestType.Treasury
            : KingdomDestType.SpecificBuilding;
        task.destPos = destPos;
        task.args = new GatherTaskArgs { resourceType = carryResource, amount = GetCarryAmount(carryResource) };
        return task;
    }

    /// <summary>调试任务源（仅承载坐标，无真实建筑；供验证入口构造 KingdomTask）。</summary>
    private class DebugTaskSource : ITaskSource
    {
        private readonly Vector2 _pos;
        public DebugTaskSource(Vector2 pos) { _pos = pos; }
        public bool IsValid => true;
        public Vector2 SourcePos => _pos;
        public bool TryAdvertiseTask(out KingdomTask task) { task = null; return false; }
        public void OnRegister() { }
        public void OnUnregister() { }
    }
}
