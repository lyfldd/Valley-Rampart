using UnityEngine;

/// <summary>
/// 昼夜双环配置（2_8 §三 / §5.4，D232~D234，SO 可配）。
/// 白天经济环 / 夜晚战斗环的时长参数；保留四季（D233）：冬季产量↓、怪物强度↑均可调。
/// 注：与 WorldConfig 里序列化的 <see cref="TimeConfigData"/>（1D 直迁的日/季节结构）区分——
/// 本类是 2_8 昼夜双环独立 SO，由 TimeManager 读取并暴露给 DayCycleSettlement / WaveDirector 消费。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/TimeConfig", fileName = "TimeConfig")]
public class TimeConfig : ScriptableObject
{
    [Header("昼夜时长（D232）")]
    [Tooltip("白天时长（游戏时间，秒）")]
    public float dayDurationSeconds = 60f;

    [Tooltip("夜晚时长（游戏时间，秒）")]
    public float nightDurationSeconds = 30f;

    [Header("四季（D233）")]
    [Tooltip("四季开关（false 时冻结季节循环，锁定默认春季）")]
    public bool seasonEnabled = true;

    [Tooltip("冬季产量系数（<1 = 减产）")]
    public float winterProductionScale = 0.8f;

    [Tooltip("冬季怪物强度系数（>1 = 强化）")]
    public float winterMonsterScale = 1.2f;
}