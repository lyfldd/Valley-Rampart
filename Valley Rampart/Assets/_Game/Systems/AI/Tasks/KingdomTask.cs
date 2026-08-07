using UnityEngine;

/// <summary>
/// 任务终点类型（QQQ.2 §10.1）。派发时由调度器动态解析，不硬编码坐标。
/// </summary>
public enum KingdomDestType
{
    None,             // 无终点（原地劳作）
    Treasury,         // 国库
    NearestWarehouse, // 最近可用仓库（无则回退国库）
    WaterNetwork,     // 水网
    SpecificBuilding  // 指定建筑
}

/// <summary>
/// 通用任务抽象（QQQ.2 §10.1，DR-16）。任务带 destType 不硬编码终点；
/// 派发时调度器按 destType 实时解析 destPos。
/// 任务被 NPC.currentTask 引用即视为占用（幂等，DR-17）。
/// </summary>
public class KingdomTask
{
    public KingdomTaskType type;      // 任务类型
    public ITaskSource source;        // 来源对象（建筑/资源点），提供 sourcePos
    public KingdomDestType destType;  // 终点类型
    public Vector2 destPos;           // 派发时由调度器动态解析，非硬编码
    public object args;               // 任务参数（产出量、目标物等）
    public float intensity;           // 刺激强度

    public KingdomTask(KingdomTaskType type, ITaskSource source, float intensity = 1f)
    {
        this.type = type;
        this.source = source;
        this.intensity = intensity;
        this.destType = KingdomDestType.None;
    }

    /// <summary>任务源世界坐标（无源返回 zero）。</summary>
    public Vector2 SourcePos => source != null ? source.SourcePos : Vector2.zero;
}

/// <summary>
/// 任务源接口（QQQ.2 §10.1/§10.3，DR-16）。建筑/资源点实现此接口按需"声明"任务。
/// 生命周期挂钩：OnRegister/OnUnregister 由调度器在注册表维护时回调。
/// </summary>
public interface ITaskSource
{
    /// <summary>任务源是否仍有效（尚未被摧毁/失效）。NPCBrain 访问 currentTask 前必须校验（QQQ.3 B1-3 / NPC-A6）。</summary>
    bool IsValid { get; }

    /// <summary>任务源世界坐标（调度器分派用距离排序）。</summary>
    Vector2 SourcePos { get; }

    /// <summary>尝试发布一个任务（按优先级/条件）。无条件发布返回 false。</summary>
    bool TryAdvertiseTask(out KingdomTask task);

    /// <summary>注册到调度器时回调（Building.OnSpawn 调 Register 时触发）。</summary>
    void OnRegister();

    /// <summary>从调度器注销时回调（Building.Die 调 Unregister 时触发）。</summary>
    void OnUnregister();
}