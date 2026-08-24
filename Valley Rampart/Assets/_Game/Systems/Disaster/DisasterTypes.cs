// 2_14 敌怪与传送门灾害 —— 枚举与灾害状态数据结构
// 设计《2_14_敌怪与传送门灾害.md》§3.1/§2.3；数值与语义以实施计划步骤1 为准。

/// <summary>怪物种类（掠夺者型，D185：2-3 种基础，无 Boss）。</summary>
public enum MonsterType
{
    Raider,     // 近战（掠夺者）：主力肉搏，数量最多，攻建筑
    Slinger,    // 远程（投石者）：后排骚扰，优先打守卫
    Brute       // 精英（头目）：高 HP+高攻击，直冲高价值目标
}

/// <summary>传送门状态机（渲染归 2_10）。</summary>
public enum PortalState
{
    Spawning,       // 生成动画中（2_10 视觉）
    Active,         // 夜晚：可攻击、可召唤
    DayProtected,   // 白天：无敌（不可交互），不召唤
    Destroying      // 摧毁：崩塌动画（2_10 视觉）
}

/// <summary>怪物行为模式（守门+出击双目标 D183）。</summary>
public enum MonsterMode
{
    Raiding,    // 出击：价值×距离选目标，掠夺高价值建筑/资源点
    Guarding,   // 守门：传送门被打 → 回援/增援，攻击靠近传送门的玩家单位
    Retreating, // 撤退：低血量尝试退回传送门
    Looting     // 掠夺：到达资源点短暂停留携带资源回传送门
}

/// <summary>灾害触发状态（供存档 2_11）。</summary>
[System.Serializable]
public class DisasterState
{
    public int daysSinceLastTrigger;    // 已连续未触发天数
    public int daysUntilForcedTrigger;  // 距保底触发剩余天数
    public int totalTriggers;           // 累计触发次数
}