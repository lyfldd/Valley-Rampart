// ============================================================================
//  AI.Core Ports - Faction 阵营枚举（从壳 UnitData.cs 迁入）
//  03_大脑提取与双适配工程.md §二：IUnitHandle.Faction 契约需要。
//  原定义于壳 Assembly-CSharp 的 UnitData.cs，asmdef 边界要求迁入核（全局命名空间不变，壳透明）。
// ============================================================================

/// <summary>
/// 阵营枚举。决定单位的敌我关系和所属势力。
/// None 用于未初始化/中立单位；Human_Player 和 Undead 互为敌对立营。
/// </summary>
public enum Faction
{
    None,           // 无阵营：未初始化或中立单位
    Human_Player,   // 玩家阵营：玩家控制的单位
    Undead          // 亡灵阵营：AI 控制的敌方单位
}
