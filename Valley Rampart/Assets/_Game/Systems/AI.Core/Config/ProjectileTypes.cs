// ============================================================================
//  AI.Core Config - 战斗介质枚举（3.6 军事统一管理 §三/§五）
//  ProjectileType 弹药类型 / BallisticType 弹道类型 / GroundEffectType 地面效果类型。
//  Unity 侧 AmmoDef/GroundEffectDef 引用；harness 侧 JSON 反序列化（同源契约）。
// ============================================================================

/// <summary>
/// 弹药类型（3.6 §3.2 弹药表）。穿透等级/AOE/弹道/效果由 AmmoDef 资产配置，本枚举只是类型标识。
/// </summary>
public enum ProjectileType
{
    Arrow,      // 弓手箭（Lv1 穿透，无 AOE）——弓装备
    Bolt,       // 手持弩箭（Lv1 穿透，无 AOE）——弩装备
    HeavyBolt,  // 贯穿弩箭（Lv3 穿透，单体高伤）——弩炮
    Stone,      // 投石（Lv3 穿透，单段 AOE）——投掷机
    Fireball,   // 火弹（大 AOE + 灼烧场）——投掷机/法师（阉割版）
    Magic,      // 魔弹（中 AOE + 减速场）——弩炮/法师（阉割版）
}

/// <summary>
/// 弹道类型（3.6 §5 抛物线体系）。弧高 vs 工事高度决定越墙判定：
///   弧高 &gt; 工事高度 → 越墙；弧高 ≤ 工事高度 → 被挡（穿透等级决定对工事伤害）。
/// </summary>
public enum BallisticType
{
    Straight,   // 直线（低抛，现有 hitscan 语义，被墙挡）
    Lob,        // 低抛弧线（弓手/弩手/法师：弧低，被墙挡）
    HighArc,    // 高抛弧线（投石/火弹/魔弹/弩炮：弧高，越墙）
}

/// <summary>
/// 地面效果类型（3.6 §3.4 GroundEffectDef）。
/// </summary>
public enum GroundEffectType
{
    None,
    Burn,       // 灼烧：每 tick 持续伤害
    Slow,       // 减速：区域减速系数
    Heal,       // 治疗：范围内有限个目标（maxTargets）
}
