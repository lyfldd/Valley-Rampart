using UnityEngine;

// ============================================================================
//  3.0.1 注意力机制 - 枚举定义
//  详见 3.0.1注意力机制与刺激源.md
// ============================================================================

/// <summary>注意力五层（高->低）。跨层层压制：高层任何活跃项 > 低层所有项。</summary>
public enum AttentionLayer
{
    Threat,       // 1.威胁：敌人入范围、自身被攻击
    Hate,         // 2.仇恨：被特定目标攻击累积（首版留壳）
    Task,         // 3.任务：君主指令、调度中心派工
    Perception,   // 4.感知：发现宝箱、看到友军受伤
    Curiosity     // 5.好奇：闲逛探索（首版留壳）
}

/// <summary>任务优先级。甲层=刺激基础强度；乙层=撤退阈值加成。</summary>
public enum TaskPriority
{
    S = 4,   // 军令：守城、增援前线
    A = 3,   // 建设：危险区建防御塔
    B = 2,   // 生产：挖矿、种田
    C = 1    // 杂务：搬运、闲逛
}

/// <summary>威胁感知等级（渐变非突变，事件驱动）。</summary>
public enum ThreatLevel
{
    None = 0,     // 无威胁：周边无敌人，处于安全区
    Alert = 1,    // 警戒：远处有敌人或获知前方危险
    Danger = 2,   // 危险：敌人在攻击范围或友军正在交战
    Lethal = 3    // 致命：自身被攻击、血量过低或敌人数量碾压
}

/// <summary>行为妥协谱系。首版实现 0/2/4 三态，1/3 留接口。</summary>
public enum BehaviorSpectrum
{
    FullPower = 0,          // 全力执行（威胁 0）
    Vigilant = 1,           // 警惕执行（留口，威胁 1）
    Cautious = 2,           // 谨慎：维持工作，抑制低优先刺激（威胁 2 且有保护）
    RetreatWhileWorking = 3,// 边撤边做（留口，威胁刚超阈值）
    FullRetreat = 4         // 完全撤退（威胁 2 无保护 / 威胁 3）
}

/// <summary>感知类型（PerceptionStimulus 用）。</summary>
public enum PerceptionType
{
    EnemySighted,      // 发现敌人
    AllyDamaged,       // 友军受伤
    ResourceFound,     // 发现资源
    BuildingDestroyed  // 建筑被毁
}

// ============================================================================
//  刺激源接口与派生结构
//  按层分结构（非单一扁平），每层有自己的字段，共享基接口。
// ============================================================================

/// <summary>
/// 刺激源基接口。所有刺激源实现此接口，统一进入机制甲评分。
/// 详见 3.0.1 文档第 2.2 节。
/// </summary>
public interface IStimulus
{
    AttentionLayer Layer { get; }
    Vector2 Position { get; }
    float Intensity { get; }
    object Source { get; }
    float Expiry { get; }
}

/// <summary>
/// 威胁刺激源（第 1 层，最高优先）。
/// 敌人进入感知范围、自身被攻击时产生。
/// </summary>
public struct ThreatStimulus : IStimulus
{
    public AttentionLayer Layer => AttentionLayer.Threat;
    public Vector2 Position { get; }
    public float Intensity { get; }
    public object Source { get; }
    public float Expiry { get; }

    /// <summary>威胁等级 0-3</summary>
    public int ThreatLevel { get; }
    /// <summary>敌人引用</summary>
    public IDamageable Enemy { get; }

    public ThreatStimulus(IDamageable enemy, int threatLevel, float intensity,
                          float expiry, object source = null)
    {
        Enemy = enemy;
        ThreatLevel = threatLevel;
        Intensity = intensity;
        Expiry = expiry;
        Source = source ?? enemy;
        Position = enemy != null ? enemy.GetPosition() : Vector2.zero;
    }
}

/// <summary>
/// 任务刺激源（第 3 层）。
/// 君主指令、调度中心派工、生产点满了时产生。
/// </summary>
public struct TaskStimulus : IStimulus
{
    public AttentionLayer Layer => AttentionLayer.Task;
    public Vector2 Position { get; }
    public float Intensity { get; }
    public object Source { get; }
    public float Expiry { get; }

    /// <summary>任务优先级 S/A/B/C</summary>
    public TaskPriority Priority { get; }
    /// <summary>任务目标位置</summary>
    public Vector2 TargetPos { get; }
    /// <summary>发起方（君主/调度中心）</summary>
    public object Issuer { get; }

    public TaskStimulus(TaskPriority priority, Vector2 targetPos, float intensity,
                        float expiry, object issuer = null)
    {
        Priority = priority;
        TargetPos = targetPos;
        Position = targetPos;
        Intensity = intensity;
        Expiry = expiry;
        Issuer = issuer;
        Source = issuer;
    }
}

/// <summary>
/// 感知刺激源（第 4 层）。
/// 发现宝箱、看到友军受伤等。
/// </summary>
public struct PerceptionStimulus : IStimulus
{
    public AttentionLayer Layer => AttentionLayer.Perception;
    public Vector2 Position { get; }
    public float Intensity { get; }
    public object Source { get; }
    public float Expiry { get; }

    /// <summary>感知对象引用</summary>
    public object Perceived { get; }
    /// <summary>感知类型</summary>
    public PerceptionType Type { get; }

    public PerceptionStimulus(PerceptionType type, object perceived, Vector2 position,
                              float intensity, float expiry)
    {
        Type = type;
        Perceived = perceived;
        Position = position;
        Intensity = intensity;
        Expiry = expiry;
        Source = perceived;
    }
}

/// <summary>
/// 仇恨刺激源（第 2 层，首版留壳）。
/// 被特定目标攻击累积。结构已定义，逻辑待后续实装。
/// </summary>
public struct HateStimulus : IStimulus
{
    public AttentionLayer Layer => AttentionLayer.Hate;
    public Vector2 Position { get; }
    public float Intensity { get; }
    public object Source { get; }
    public float Expiry { get; }

    public HateStimulus(Vector2 position, float intensity, float expiry, object source = null)
    {
        Position = position;
        Intensity = intensity;
        Expiry = expiry;
        Source = source;
    }
}

/// <summary>
/// 好奇刺激源（第 5 层，首版留壳）。
/// 闲逛探索。结构已定义，逻辑待后续实装。
/// </summary>
public struct CuriosityStimulus : IStimulus
{
    public AttentionLayer Layer => AttentionLayer.Curiosity;
    public Vector2 Position { get; }
    public float Intensity { get; }
    public object Source { get; }
    public float Expiry { get; }

    public CuriosityStimulus(Vector2 position, float intensity, float expiry, object source = null)
    {
        Position = position;
        Intensity = intensity;
        Expiry = expiry;
        Source = source;
    }
}

/// <summary>
/// 焦点数据（注意力系统输出）。
/// 包含注意力系统选出的第一名刺激源的精简信息，供行为执行器使用。
/// </summary>
public struct Focus
{
    public readonly AttentionLayer Layer;
    public readonly Vector2 Position;
    public readonly float Intensity;
    public readonly object Source;
    public readonly bool IsValid;

    public Focus(AttentionLayer layer, Vector2 position, float intensity, object source)
    {
        Layer = layer;
        Position = position;
        Intensity = intensity;
        Source = source;
        IsValid = true;
    }

    public static Focus Invalid => default;

    public bool Is(AttentionLayer layer) => IsValid && Layer == layer;
}
