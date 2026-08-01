using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 新增刺激源
//  详见 3.0.1_2_输入输出决定层设计.md §3
//  SafetyStimulus(归巢兜底) / FollowStimulus(动态锚点) / HoldPositionStimulus(驻留)
//  全部 class 池化，防 struct 装箱（§9 零 GC 纪律）
// ============================================================================

/// <summary>
/// 归巢吸引刺激源（§3.1，任务层 D 级 pseudo-task）。
/// NPC 自带、无需调度中心下发，住任务层最底部，只在无更高活跃项时兜底成焦点。
/// 让"没事干就回城墙里待着"成为涌现行为。
/// </summary>
public sealed class SafetyStimulus : IStimulus
{
    public AttentionLayer Layer => AttentionLayer.Task;
    /// <summary>目标位置 = HomePoint（安全点）</summary>
    public Vector2 Position { get; set; }
    /// <summary>safetyUrge 强度（每 tick 由 Provider 重算）</summary>
    public float Intensity { get; set; }
    /// <summary>NPC 自带，无外部源</summary>
    public object Source => null;
    /// <summary>永不过期，每 tick 重算强度</summary>
    public float Expiry => float.MaxValue;
    /// <summary>驻留类位置，到达后 Idle</summary>
    public FocusType FocusType => FocusType.HomePosition;

    /// <summary>任务优先级（C 级兜底，低于 B 级生产/A 级建设/S 级军令）</summary>
    public TaskPriority Priority => TaskPriority.C;
}

/// <summary>
/// 跟随锚点刺激源（§3.2，任务层）。
/// 调度中心/军令下发，目标位置动态绑定锚点（部队队长 UnitController），每 tick 刷新。
/// 让"工人跟上部队"成为涌现行为。
///
/// 3.0.1_3 扩展（审计 D3 复用路径）：加 SlotOffset 字段承载编队槽位偏移。
/// 槽位化跟随时：目标位置 = 锚点位置 + SlotOffset × cellSize（cell 吸附）。
/// 非编队跟随（工人随军）SlotOffset = Vector2.zero，退化为原松散跟随语义。
/// </summary>
public sealed class FollowStimulus : IStimulus
{
    public AttentionLayer Layer => AttentionLayer.Task;
    /// <summary>锚点（部队队长/将军），位置每 tick 随锚点刷新</summary>
    public UnitController Anchor;
    /// <summary>跟随优先级（跟随工程=B级，随军抢修=A级，编队军令=S级）</summary>
    public TaskPriority Priority;
    /// <summary>槽位偏移（cell 单位，3.0.1_3 编队用；非编队跟随=zero）</summary>
    public Vector2Int SlotOffset = Vector2Int.zero;
    /// <summary>是否编队槽位化跟随（SlotOffset 非 zero 即编队成员）</summary>
    public bool IsFormationSlot => SlotOffset.x != 0 || SlotOffset.y != 0;
    public Vector2 Position => Anchor != null ? (Vector2)Anchor.transform.position : Vector2.zero;
    public float Intensity { get; set; }
    public object Source => Anchor;
    public float Expiry => float.MaxValue;
    /// <summary>锚点型，永不到达，L2 选 FollowAnchor 模块</summary>
    public FocusType FocusType => FocusType.Anchor;
}

/// <summary>
/// 驻留刺激源（§3.3 / §13.3，任务层，由受击冷却状态机在 Caution 态注入）。
/// 目标 = 当前位置（恒已到达 -> 谱系 0 + 位置型已到达 -> Idle），强度中等。
/// 与被打折的任务类刺激（× stateTaskDiscount）在任务层内排序胜出 -> "原地不动"涌现。
/// </summary>
public sealed class HoldPositionStimulus : IStimulus
{
    public AttentionLayer Layer => AttentionLayer.Task;
    /// <summary>注入时快照 NPC 当前位置</summary>
    public Vector2 Position { get; set; }
    /// <summary>holdPositionIntensity（中等，需 > 折后任务强度）</summary>
    public float Intensity { get; set; }
    public object Source => null;
    public float Expiry => float.MaxValue;
    /// <summary>复用 Position 类型，目标=自身位置 -> 恒已到达 -> Idle</summary>
    public FocusType FocusType => FocusType.Position;

    /// <summary>任务优先级（与 TaskStimulus 同层竞争，靠 Intensity 胜出而非 Priority）</summary>
    public TaskPriority Priority => TaskPriority.C;
}
