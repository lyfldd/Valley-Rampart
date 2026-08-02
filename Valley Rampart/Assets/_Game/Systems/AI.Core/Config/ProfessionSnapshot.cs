// ============================================================================
//  AI.Core Config - ProfessionSnapshot 职业属性快照（接缝 4 的落地）
//  详见 03_大脑提取与双适配工程.md §三 配置快照方案
//  核内不引用 NpcProfessionDef（SO，Unity 侧资产），改吃本 POCO。
//  Unity 侧：NpcProfessionDef.ToSnapshot() 生成（机械拷贝，见壳 Data/NpcProfessionDef.cs）。
//  harness 侧：System.Text.Json 从 JSON 反序列化。
//  说明：Occupation 字段（壳 UnitData 定义，非核契约）不入快照；核内决策逻辑不消费职业枚举。
// ============================================================================

/// <summary>
/// 职业属性快照（纯数据，零引擎依赖）。
/// 字段与 NpcProfessionDef（含 UnitData 基础三件套扩展）一一对应，除 Occupation（壳类型）。
/// </summary>
public struct ProfessionSnapshot
{
    // ===== UnitData 基础 =====
    public Faction faction;
    public float walkSpeed;
    public float runSpeed;
    public int maxHp;
    public int attack;
    public int defense;

    // ===== NpcProfessionDef 战斗参数 =====
    public float attackRange;
    public float attackCD;
    public bool isRanged;
    public float projectileSpeed;

    // ===== 注意力系统 =====
    public float perceptionRadius;
    public float threatSensitivity;
    public int courage;
    public int obedience;
    public float retreatThresholdOffset;

    // ===== 输入输出决定层 =====
    public int maxHitCount;
    public float professionPullScale;

    // ===== 装备槽（预留）=====
    public int equipmentSlotCount;

    // ===== 漫游 =====
    public float wanderRadiusCells;

    /// <summary>
    /// 默认值（非 NPC 职业单位用；数值对齐 NpcProfessionDef 字段默认值）。
    /// </summary>
    public static ProfessionSnapshot Default => new ProfessionSnapshot
    {
        faction = Faction.None,
        walkSpeed = 5f,
        runSpeed = 10f,
        maxHp = 100,
        attack = 5,
        defense = 0,
        attackRange = 1f,
        attackCD = 1f,
        isRanged = false,
        projectileSpeed = 25f,
        perceptionRadius = 5f,
        threatSensitivity = 1f,
        courage = 50,
        obedience = 50,
        retreatThresholdOffset = 0f,
        maxHitCount = 3,
        professionPullScale = 1f,
        equipmentSlotCount = 0,
        wanderRadiusCells = 2f,
    };
}
