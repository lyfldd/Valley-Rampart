using UnityEngine;

/// <summary>
/// 任务调度器契约（QQQ.3 B0-1）。由 TaskScheduler 单例实现。
/// 建筑（ITaskSource）经 Register/Unregister 接入调度；外部系统经查询/钩子读取任务态。
/// </summary>
// QQQ.2 T17 / QQQ.3 B0-1
public interface ITaskScheduler
{
    /// <summary>建筑注册（OnSpawn/OnConstructionComplete 调）。</summary>
    void Register(ITaskSource source);

    /// <summary>建筑注销（Die 调）。顺带清掉指向该源的在派任务。</summary>
    void Unregister(ITaskSource source);

    /// <summary>查某工人任务态（DR-19：仅 Working 算在场）。无任务返回 None。</summary>
    TaskState GetWorkerState(int npcId);

    /// <summary>该生产源当前是否有工人 Working（T9 用）。</summary>
    bool HasWorkerAssigned(ITaskSource producer);

    /// <summary>该源当前被派工人总数（2_8 步骤3 规模派工查询口，D95）。</summary>
    int CountAssignedWorkers(ITaskSource source);

    /// <summary>外部强制放弃某工人任务。</summary>
    void AbandonTask(int npcId);

    /// <summary>NPC 死亡钩子（清指派）。</summary>
    void OnNpcDied(int npcId);

    /// <summary>建筑死亡钩子（清指派）。</summary>
    void OnBuildingDied(ITaskSource source);

    /// <summary>威胁抢占挂起（记录挂起计时）。</summary>
    void OnThreatSuspended(int npcId);

    /// <summary>威胁恢复（清除挂起计时）。</summary>
    void OnThreatResumed(int npcId);
}