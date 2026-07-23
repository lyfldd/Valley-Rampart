using UnityEngine;

// 职业枚举。决定单位在战斗中的角色定位和可用行为。
// Ruler 为君主专属职业，由 RulerController 管理；其余职业由 AI 或玩家指令驱动。
public enum Occupation
{
    Ruler,      // 君主：玩家控制的统治者单位，阵亡则 GameOver
    Archer,     // 弓箭手：远程攻击单位
    Warrior,    // 战士：近战攻击单位
    Civilian    // 平民：非战斗单位，从事资源采集/建造等
}

// 阵营枚举。决定单位的敌我关系和所属势力。
// None 用于未初始化/中立单位；Human_Player 和 Undead 互为敌对阵营。
public enum Faction
{
    None,           // 无阵营：未初始化或中立单位
    Human_Player,   // 玩家阵营：玩家控制的单位
    Undead          // 亡灵阵营：AI 控制的敌方单位
}

// 单位数据资产（ScriptableObject）
// 定义单位的基础属性模板，由 UnitController.Initialize 读取并应用到运行时实例。
// 作为静态配置由 LoadManager 在阶段1 加载，存档系统不直接保存 UnitData 引用
// （存档通过 faction+occupation 组合键从 UnitDataManager 查找对应资产）。
[CreateAssetMenu(menuName = "ValleyRampart/UnitData")]
public class UnitData : ScriptableObject
{
    // 身份设定：单位所属阵营，决定敌我关系
    [Header("身份设定")]
    public Faction faction;

    // 职业：决定单位的角色定位和可用行为
    [Header("职业")]
    public Occupation occupation;

    // 步行速度：单位常规移动速度（单位/秒）
    [Header("移动速度")]
    public float walkSpeed = 5f;

    // 跑步速度：单位加速移动速度（单位/秒），需按住跑步键触发
    public float runSpeed = 10f;

    // 最大血量：单位生命值上限，降为0时死亡
    [Header("基础数值属性")]
    public int maxHp;

    // 攻击力：单位造成伤害的基础值，实际伤害 = max(1, attack - target.defense)
    public int attack;

    // 防御力：单位减免伤害的基础值，被攻击时从原始伤害中扣除
    public int defense;
}