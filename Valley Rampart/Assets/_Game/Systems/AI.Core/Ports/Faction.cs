using System;

// ============================================================================
//  AI.Core Ports - Faction 阵营枚举（从壳 UnitData.cs 迁入）
//  03_大脑提取与双适配工程.md §二：IUnitHandle.Faction 契约需要。
//  原定义于壳 Assembly-CSharp 的 UnitData.cs，asmdef 边界要求迁入核（全局命名空间不变，壳透明）。
// ============================================================================

/// <summary>
/// 阵营枚举。决定单位的敌我关系和所属势力。
/// None 用于未初始化/中立单位；PlayerCamp 和 Monster 互为敌对立营。
/// AiKingdom：2_17 步骤10 新增——AI 王国单位阵营（人类种族，非玩家阵营）。插在 Undead 后/Orc_Player 前
/// （净增枚举值=尾部追加，Faction 不直接入档；UnitData.faction 按 int 序列化，排此位不对既有资产错位）。
/// Monster：2_20 D427 新增——传送门怪物阵营（2_14 Raider/Slinger/Brute）。插 AiKingdom 后/四族预留区前
/// （int=4；四族预留 Orc/Dwarf/Elf 顺延 +1，当前无资产引用，安全）。
/// Undead：D422/D427 已退役——枚举值保留 + [Obsolete]（int 序列化/存档安全），不再使用。
/// 四族预留（M8 方案 A）：Orc/Dwarf/Elf 为未来种族阵营，当前无资产/玩法，纯枚举预留。
/// </summary>
public enum Faction
{
    None,           // 无阵营：未初始化或中立单位
    PlayerCamp,     // 玩家阵营：玩家控制的单位（玩家营地；2_20 D428 阵营与种族解耦，玩家不再=人类）
    [Obsolete("2_20 D422/D427 Undead 退役，用 Monster")]
    Undead,         // 亡灵阵营：AI 控制的敌方单位（已退役）
    AiKingdom,      // AI 王国阵营：AI 控制的王国单位（人类种族，非玩家；2_17 步骤10 Faction 收编）
    Monster,        // 传送门怪物阵营：2_14 Raider/Slinger/Brute（2_20 D427 新增）
    // ===== 四族预留（2026-08-03 方案 A 定案；harness 职业库未含，训练师暂不可引用）=====
    Orc_Player,     // 兽人玩家阵营（未来）
    Dwarf_Player,   // 矮人玩家阵营（未来）
    Elf_Player,     // 精灵玩家阵营（未来）
}
