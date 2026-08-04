// ============================================================================
//  AI.Core Config - ProfessionSnapshot 职业属性快照（接缝 4 的落地）
//  详见 03_大脑提取与双适配工程.md §三 配置快照方案
//  核内不引用 NpcProfessionDef（SO，Unity 侧资产），改吃本 POCO。
//  Unity 侧：NpcProfessionDef.ToSnapshot() 生成（机械拷贝，见壳 Data/NpcProfessionDef.cs）。
//  harness 侧：System.Text.Json 从 JSON 反序列化。
//  说明：Occupation 字段（壳 UnitData 定义，非核契约）不入快照；核内决策逻辑不消费职业枚举。
// ============================================================================

/// <summary>
/// 职业角色族（B4 构成驱动非职业驱动，00 B2 定案）。
/// 目标选择/治疗/保命等行为按族走；细分 7 族对齐 sim 配方（snipe/aoe/support/tank/machine）。
/// </summary>
public enum RoleFamily
{
    None,       // 工人/工事等无战斗角色（纯被保护者/阻挡物）
    Tank,       // 盾卫/重装：顶住守位
    Sniper,     // 弓手/弩手：点杀残血/脆皮
    Aoe,        // 法师/大法师：密度人群（AOE 收益）
    Support,    // 治疗/主教：低血友军治疗
    Mobility,   // 骑兵：冲锋切入
    Machine,    // 投掷机/弩炮：远程重火力 + 弹药经济（被贴身保命）
}

/// <summary>
/// 职业属性快照（纯数据，零引擎依赖）。
/// 字段与 NpcProfessionDef（含 UnitData 基础三件套扩展）一一对应，除 Occupation（壳类型）。
/// </summary>
public struct ProfessionSnapshot
{
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

    // ===== 静态单位（M8 工事/静止机器：塔/墙/拒马等）=====
    // 静态单位不参与 AI 决策、不移动、不逃逸；有攻击值的按 CD 攻击射程内敌人，attack=0 纯阻挡。
    public bool isStatic;

    // ===== 弹药（3.6 §三：AmmoDef 资产 → 快照拉平，sim 直读）=====
    // Unity 侧由 NpcProfessionDef.ammo（AmmoDef 引用）在 ToSnapshot 拷贝；harness 侧 JSON 直接配置。
    public ProjectileType projectileType;   // 弹药类型
    public int pierceLevel;                 // 穿透等级（int，不设上限；< 工事防御等级 → 不造成伤害）
    public float aoeRadiusCells;            // 溅射半径（格，0=单体）
    public float aoeFalloff;                // 溅射衰减 0-1（0=均匀，1=线性衰减到边缘0）
    public BallisticType ballisticType;     // 弹道类型（弧高 vs 工事高度 → 越墙判定）
    public float arcHeightCells;            // 弹道弧高（格）
    public GroundEffectType effectType;     // 命中后地面效果类型（None=无）
    public float effectRadiusCells;         // 效果区域半径（格）
    public float effectDuration;            // 效果持续时长（秒）
    public float effectTickInterval;        // 效果结算间隔（秒）
    public float effectPower;               // Burn=每tick伤害 / Slow=减速系数 / Heal=每tick治疗
    public int effectMaxTargets;            // Heal 有限个：区域内最多奶 N 个

    // ===== 韧性（3.6 §4.2）=====
    // 最终韧性 = baseToughness + defense × toughnessDefenseScale（两端同公式）
    public float baseToughness;             // 职业基础韧性（弓手 5 / 战士 40 / 骑兵 50）
    public float toughnessDefenseScale;     // 防御力 → 韧性系数（SO 配置，训练校准）

    // ===== 免伤（3.6 §5.2：免伤词条体系，职业基础因子）=====
    // 实际免伤 = Σ 各因子（职业基础 + 冲锋免伤 70% + 未来扩展），clamp 到上限
    public float baseDamageReduce;          // 职业基础免伤（0-1，默认 0）

    // ===== 保护者权重（3.7 保护力加权和，可训练）=====
    // 保护 = 身边友军 protectPower 加权和 ≥ protectThreshold。训练师学"谁能当保护者"。
    // 肉盾（盾卫/重装/战士）高，脆皮（弓手/法师/治疗）低；默认 0.2 保证任何兵种都有一定保护力，
    // 避免缺席某兵种就完全无保护（不硬编码兵种对兵种关系）。
    public float protectPower;              // 保护者权重（0-1，可训练）
    /// <summary>重甲标记（3.7）：重装/盾卫等重甲单位，评分统计用（替代职业名字符串硬编码）</summary>
    public bool isHeavyArmor;

    // ===== 角色族（B4 构成驱动非职业驱动，00 B2 定案）=====
    // 目标选择/治疗等行为按角色族走，不按职业名硬编码；新增兵种 = 标角色族 + 挂 protectPower。
    // 细分 7 族对齐 sim 配方（snipe/aoe/support/tank/machine）与 Unity 行为差异。
    public RoleFamily roleFamily;

    // ===== 骑兵冲锋（3.6 §5.3，职业级参数，训练可调）=====
    public bool isCavalry;                  // 是否骑兵（冲锋能力开关）
    public float chargeDamage;              // 冲锋伤害（特殊技能，80）
    public float chargeRangeCells;          // 冲锋距离（格，4 格 = 中区块距离）
    public float chargeSpeed;               // 冲锋突进速度（世界单位/秒，25：快且连续，可训练）
    public float chargePairGap;             // 组内两次间隔（秒，0.3）
    public float chargeGroupCooldown;       // 组间隔（秒，20）
    public float chargeDamageReduce;        // 冲锋过程免伤（0-1，70%）

    // ===== 工事（3.6 §4.4：墙/门/拒马/塔。Unity 侧 FortificationDef → 快照拉平，sim 场景 JSON 直接配）=====
    public bool isFortification;            // 是否工事
    public int fortDefenseLevel;            // 防御等级（int，不设上限）
    public float fortHeightCells;           // 工事高度（拒马矮 0.5 / 城墙高 2 / 塔高 3）
    public bool fortBlocksMovement;         // 挡移动（墙/拒马）
    public bool fortPassable;               // 可通行（城门开合）
    public float fortMeleeDamageReduce;     // 工事近战减免（3.6 §4.4：近战攻击工事时减免比例）

    // ===== 多目标攻击（2026-08-04 决策员：箭塔多级升级——每级多一个同时攻击点）=====
    // 静态塔（箭塔）按此数同时攻击多个目标（0=默认单目标）。Unity 侧 FortificationDef 对应字段需同步。
    public int multiTargetCount;            // 同时攻击目标数（Lv1=1 / Lv2=2 / Lv3=3）

    // ===== 弹药储备/补给（3.7 战争机器火力经济学）=====
    // 仅供战争机器（投掷机/弩炮）用；弓手/弩手等兵种 ammoMax=0 无弹药模型（各自无限弹药）。
    // 三弹型：Stone 石弹（成本最低，自动补给）/ Fireball 火弹 / Magic 魔弹（昂贵，有限储备不自动补）。
    // 发射评估：AI 弹药紧张时提高发射价值门槛（惜用），昂贵弹只对高价值目标（残血/重甲/密集）用。
    public int ammoMax;                     // 弹容量（石弹槽位最大值，如投掷机 30）
    public float ammoCostStone;             // 石弹成本（1，最低）
    public float ammoCostFireball;          // 火弹成本（3）
    public float ammoCostMagic;             // 魔弹成本（5）
    public float ammoResupplyDelay;         // 补给延迟（秒，模拟工人从后方搬运往返）
    public float ammoConservationWeight;    // 惜用权重 0-1（训练可调；弹药紧张时提高发射门槛）

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
        isStatic = false,
        projectileType = ProjectileType.Arrow,
        pierceLevel = 1,
        aoeRadiusCells = 0f,
        aoeFalloff = 0f,
        ballisticType = BallisticType.Lob,
        arcHeightCells = 0f,
        effectType = GroundEffectType.None,
        effectRadiusCells = 0f,
        effectDuration = 0f,
        effectTickInterval = 0f,
        effectPower = 0f,
        effectMaxTargets = 0,
        baseToughness = 10f,
        toughnessDefenseScale = 0.2f,
        baseDamageReduce = 0f,
        protectPower = 0.2f,
        isHeavyArmor = false,
        roleFamily = RoleFamily.None,
        isCavalry = false,
        chargeDamage = 80f,
        chargeRangeCells = 4f,
        chargeSpeed = 25f,
        chargePairGap = 0.3f,
        chargeGroupCooldown = 20f,
        chargeDamageReduce = 0.7f,
        isFortification = false,
        fortDefenseLevel = 0,
        fortHeightCells = 1f,
        fortBlocksMovement = false,
        fortPassable = false,
        fortMeleeDamageReduce = 0f,
        multiTargetCount = 0,
        ammoMax = 0,
        ammoCostStone = 1f,
        ammoCostFireball = 3f,
        ammoCostMagic = 5f,
        ammoResupplyDelay = 0f,
        ammoConservationWeight = 0.5f,
    };
}
