using UnityEngine;

// 职业枚举。决定单位在战斗中的角色定位和可用行为。
// Ruler 为君主专属职业，由 RulerController 管理；其余职业由 AI 或玩家指令驱动。
public enum Occupation
{
    Ruler,          // 君主：玩家控制的统治者单位，阵亡则 GameOver
    General,        // 将军：特殊近战 NPC，挂 FormationController 统帅编队（3.0.1_3 §1.1）
    Archer,         // 弓箭手：远程攻击单位
    Warrior,        // 战士：近战攻击单位
    Civilian,       // 平民：非战斗单位，从事资源采集/建造等
    // ===== M8 新职业（2026-08-03 追加，序号保持前 5 个不变；对应 UnitDataManager 的 faction_occupation key）=====
    Mage,           // 法师：远程高伤
    Healer,         // 治疗师：人类侧治疗 / 亡灵侧攻击语义
    Crossbowman,    // 弩手：远程点杀
    HeavyWarrior,   // 重装战士：高防近战
    Bishop,         // 主教：远程治疗（人类）
    ShieldGuard,    // 盾卫：高防抗线
    Archmage,       // 大法师：远程高伤（更远射程）
    // ===== 3.6 新增（末尾追加，保持现有枚举 int 值稳定，防资产 occupation 错位）=====
    Cavalry,        // 骑兵：冲锋 + 击飞（3.6 §五，血与战争机器同级、韧性 50）
    // ===== 3.7 机器/工事（独立 occupation 防 UnitDataManager faction_occupation key 撞 Ruler）=====
    SiegeMachine,   // 投掷机（攻城机器）
    Ballista,       // 弩炮
    Tower,          // 防御塔（3.7 废弃共用值：三塔拆独立枚举防 UnitDataManager key 去重丢资产，见下）
    Barricade,      // 拒马
    Wall,           // 城墙
    Gate,           // 城门
    // ===== 3.7 P1.6 三塔拆独立（Tower 共用导致三塔资产 key 冲突/UnitDataManager 去重丢塔，末尾追加防 int 错位）=====
    ArrowTower,     // 箭塔（ammo=Arrow，独立 key Human_Player_ArrowTower）
    CrossbowTower,  // 弩塔（ammo=Bolt）
    MagicTower,     // 魔法塔（ammo=Magic）
}

// 注意：Faction 枚举已迁入 AI.Core/Ports/Faction.cs（M1 决策核提取，asmdef 边界要求），
// 全局命名空间不变，本文件及全部壳代码的 Faction 引用自动解析到核内类型。

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