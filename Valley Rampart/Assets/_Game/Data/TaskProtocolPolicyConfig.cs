using UnityEngine;

/// <summary>M5-C 新协议的分域策略。当前拍板值只有王国级。</summary>
public enum TaskProtocolDomain : byte
{
    Kingdom = 0,
}

/// <summary>M5-C 新协议的统计策略。当前拍板值为单卡统计。</summary>
public enum TaskProtocolStatistics : byte
{
    PerTask = 0,
}

/// <summary>
/// M5-C 并存运行链配置。通过 Resources.Load 读取，不使用 Inspector 拖引用。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/TaskProtocolPolicyConfig", fileName = "TaskProtocolPolicyConfig")]
public sealed class TaskProtocolPolicyConfig : ScriptableObject
{
    public int defaultRetryMax = 1;
    public float tickIntervalSeconds = 1f;
    public int unreachableCooldownTicks = 5;
    public TaskProtocolDomain domain = TaskProtocolDomain.Kingdom;
    public TaskProtocolStatistics statistics = TaskProtocolStatistics.PerTask;
}
