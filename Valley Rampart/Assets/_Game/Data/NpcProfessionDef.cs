using UnityEngine;

/// <summary>
/// NPC 职业属性配置（SO，3.4 第九节三轨数值之一）。
/// 继承 UnitData 拿基础三件套（attack/defense/maxHp），扩展战斗/注意力/装备字段。
///
/// 三轨数值分住：
///   - NPC 职业属性 -> NpcProfessionDef（本类，继承 UnitData）
///   - 建筑战斗属性 -> BuildingDef.combat（已有 CombatConfig）
///   - 全局伤害规则 -> DamageConfig（独立 SO）
///
/// 详见 3.4_伤害管线设计.md 第九节、决策 27、第十节占位数值表。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/NpcProfessionDef", fileName = "NpcProfessionDef")]
public class NpcProfessionDef : UnitData
{
    [Header("战斗参数")]
    [Tooltip("攻击范围（格数）。近战 1 格，远程 5 格")]
    public float attackRange = 1f;

    [Tooltip("攻击冷却（秒）。内部取整到 tick 倍数")]
    public float attackCD = 1f;

    [Tooltip("是否远程攻击")]
    public bool isRanged = false;

    [Tooltip("弹速（世界单位/秒，远程用）。联调后值 25/s")]
    public float projectileSpeed = 25f;

    [Header("注意力系统（3.0.1）")]
    [Tooltip("感知半径（格数）。士兵远 / 农民近")]
    public float perceptionRadius = 5f;

    [Tooltip("威胁敏感度（越高越容易升级威胁等级）。农民 +x / 士兵 -x")]
    public float threatSensitivity = 1f;

    [Tooltip("勇气值（0-100，基础 50）。高勇气更敢冒险")]
    [Range(0, 100)] public int courage = 50;

    [Tooltip("服从度（0-100，基础 50）。高服从更少抗命")]
    [Range(0, 100)] public int obedience = 50;

    [Tooltip("职业撤退阈值偏移。士兵敢扛 +x / 农民怯 -x")]
    public float retreatThresholdOffset = 0f;

    [Header("输入输出决定层（3.0.1_2）")]
    [Tooltip("战略撤退触发阈值：受击次数达此值后无条件回家。工人 3 / 士兵 99（几乎不触发）")]
    public int maxHitCount = 3;

    [Tooltip("归巢职业系数：工人 1.0（爱回城）/ 士兵 0.2（守城不轻易回）")]
    public float professionPullScale = 1.0f;

    [Header("装备槽（预留，首版空）")]
    [Tooltip("装备槽数量（头盔/护甲/武器，首版 0）")]
    public int equipmentSlotCount = 0;

    [Header("漫游（3.0.1_4 §6.3）")]
    [Tooltip("漫游安全半径（格数，HomePoint 周围随机取点范围）。工人晃悠大 / 士兵小")]
    public float wanderRadiusCells = 2f;

    [Header("静态单位（M8 工事/静止机器）")]
    [Tooltip("静态单位（塔/墙/拒马/弩炮）：不参与 AI 决策、不移动、不逃逸；有攻击值的按 CD 攻击射程内敌人，attack=0 纯阻挡")]
    public bool isStatic = false;

    [Header("弹药（3.6 §三 介质层，AmmoDef 独立资产）")]
    [Tooltip("弹药行为模板（穿透/AOE/弹道/效果）。null=近战无弹药。ToSnapshot 拉平进快照")]
    public AmmoDef ammo;

    [Header("韧性（3.6 §4.2）")]
    [Tooltip("职业基础韧性（弓手 5 / 战士 40 / 骑兵 50）。最终韧性 = base + defense × scale")]
    public float baseToughness = 10f;

    [Tooltip("防御力 → 韧性系数（SO 配置，训练校准）")]
    public float toughnessDefenseScale = 0.2f;

    [Header("免伤（3.6 §5.2 免伤词条体系）")]
    [Tooltip("职业基础免伤（0-1，默认 0）。实际免伤 = Σ 各因子（+ 冲锋免伤 70% 等），clamp 到上限")]
    public float baseDamageReduce = 0f;

    [Header("保护者权重（3.7 保护力加权和，可训练）")]
    [Tooltip("保护者权重（0-1）：保护 = 身边友军 protectPower 之和 ≥ protectThreshold。肉盾高/脆皮低；缺兵种也有保护力（不硬编码兵种表）")]
    public float protectPower = 0.2f;
    [Tooltip("重甲标记（3.7）：重装/盾卫等重甲单位，评分统计用（替代职业名字符串硬编码）")]
    public bool isHeavyArmor = false;

    [Header("骑兵冲锋（3.6 §5.3，仅 Cavalry 用）")]
    [Tooltip("是否骑兵（冲锋能力开关）")]
    public bool isCavalry = false;

    [Tooltip("冲锋伤害（特殊技能，80）")]
    public float chargeDamage = 80f;

    [Tooltip("冲锋距离（格，4 格 = 中区块距离）")]
    public float chargeRangeCells = 4f;

    [Tooltip("冲锋突进速度（世界单位/秒，默认 25：4 格≈0.36s 冲完，快且过程连续）")]
    public float chargeSpeed = 25f;

    [Tooltip("组内两次间隔（秒，0.3）")]
    public float chargePairGap = 0.3f;

    [Tooltip("组间隔（秒，20）")]
    public float chargeGroupCooldown = 20f;

    [Tooltip("冲锋过程免伤（0-1，70%）")]
    public float chargeDamageReduce = 0.7f;

    [Header("工事（3.6 §4.4，墙/门/拒马/塔用）")]
    [Tooltip("工事配置（墙/门/拒马/塔资产配此引用；非工事留空）。ToSnapshot 拉平进快照")]
    public FortificationDef fortification;

    [Header("弹药储备/补给（3.7 战争机器火力经济学，仅投掷机/弩炮用）")]
    [Tooltip("弹容量（石弹槽位最大值，如投掷机 30）。弓手/弩手等兵种保持 0=无弹药模型")]
    public int ammoMax = 0;
    [Tooltip("石弹成本（1，最低，自动补给）")]
    public float ammoCostStone = 1f;
    [Tooltip("火弹成本（3，昂贵，有限储备不自动补）")]
    public float ammoCostFireball = 3f;
    [Tooltip("魔弹成本（5，昂贵，有限储备不自动补）")]
    public float ammoCostMagic = 5f;
    [Tooltip("补给延迟（秒，模拟工人从后方搬运往返）")]
    public float ammoResupplyDelay = 0f;
    [Tooltip("惜用权重 0-1（训练可调；弹药紧张时提高发射价值门槛）")]
    public float ammoConservationWeight = 0.5f;

    [Header("战争机器乘员（改动②：弩炮/投掷机需工人操作）")]
    [Tooltip("需几名工人操作（0=不需工人，恒可工作）。Catapult/Ballista=2")]
    public int crewRequired = 0;
    [Tooltip("工人操作半径（格）。工人在此半径内即算操作机器")]
    public float crewRadiusCells = 0f;

    [Header("角色族（B4 构成驱动非职业驱动，00 B2 定案）")]
    [Tooltip("角色族（Tank 顶住/Sniper 点杀/Aoe 密度/Support 治疗/Mobility 冲锋/Machine 重火力）。目标选择/治疗按族走，不按职业名硬编码")]
    public RoleFamily roleFamily = RoleFamily.None;

    [Header("专属兵种钩子（2_20 M7，D490~D497）")]
    [Tooltip("远程伤害减免 0-1（矮人磐石卫士 45%，D494）：受方修正家族——对远程单体直伤乘 (1-值)。不进快照（AI.Core 零直改，sim 对拍=账本登记）")]
    public float rangedDamageReduce = 0f;
    [Tooltip("庇护转移概率 0-1（人类盾卫 30%，D492）：1 宏格内友军受远程单体直伤时按此概率转移给本盾卫。最近 1 个盾卫承接防叠加；AOE 不转")]
    public float shelterChance = 0f;
    [Tooltip("庇护半径（宏格，盾卫 1）：本盾卫庇护 1 格内友军的判定半径")]
    public float shelterRadiusCells = 1f;
    [Tooltip("对建筑伤害倍率（臼炮/攻城槌×2=2、重弩炮×1.5=1.5、其余 1）：攻方单位对 Building 目标结算乘此倍率")]
    public float buildingDamageMul = 1f;
    [Tooltip("对单位伤害倍率（攻城槌 0=对单位零伤害纯拆墙 D497、其余 1）：攻方单位对 UnitController 目标结算乘此倍率")]
    public float unitDamageMul = 1f;
    [Tooltip("贯穿额外目标数（矮人火枪手 1，D494）：弹道命中主目标后继续贯穿后 N 个目标（60% 传递伤害）")]
    public int pierceThroughCount = 0;

    /// <summary>
    /// 生成核内快照（M1 决策核提取，接缝 4）。
    /// 核内（AI.Core）只吃 ProfessionSnapshot，不引用本 SO。字段机械拷贝，改字段需同步 ProfessionSnapshot。
    /// 注：Occupation（壳类型）不入快照——核内决策逻辑不消费职业枚举。
    /// </summary>
    public ProfessionSnapshot ToSnapshot()
    {
        return new ProfessionSnapshot
        {
            faction = faction,
            walkSpeed = walkSpeed,
            runSpeed = runSpeed,
            maxHp = maxHp,
            attack = attack,
            defense = defense,
            attackRange = attackRange,
            attackCD = attackCD,
            isRanged = isRanged,
            projectileSpeed = projectileSpeed,
            perceptionRadius = perceptionRadius,
            threatSensitivity = threatSensitivity,
            courage = courage,
            obedience = obedience,
            retreatThresholdOffset = retreatThresholdOffset,
            maxHitCount = maxHitCount,
            professionPullScale = professionPullScale,
            equipmentSlotCount = equipmentSlotCount,
            wanderRadiusCells = wanderRadiusCells,
            isStatic = isStatic,
            // 弹药（AmmoDef → 快照拉平；null 兜底默认值）
            projectileType = ammo != null ? ammo.ammoType : ProjectileType.Arrow,
            pierceLevel = ammo != null ? ammo.pierceLevel : 1,
            aoeRadiusCells = ammo != null ? ammo.aoeRadiusCells : 0f,
            aoeFalloff = ammo != null ? ammo.aoeFalloff : 0f,
            ballisticType = ammo != null ? ammo.ballisticType : BallisticType.Lob,
            arcHeightCells = ammo != null ? ammo.arcHeightCells : 0f,
            effectType = ammo != null && ammo.effect != null ? ammo.effect.type : GroundEffectType.None,
            effectRadiusCells = ammo != null && ammo.effect != null ? ammo.effect.radiusCells : 0f,
            effectDuration = ammo != null && ammo.effect != null ? ammo.effect.duration : 0f,
            effectTickInterval = ammo != null && ammo.effect != null ? ammo.effect.tickInterval : 0f,
            effectPower = ammo != null && ammo.effect != null ? ammo.effect.power : 0f,
            effectMaxTargets = ammo != null && ammo.effect != null ? ammo.effect.maxTargets : 0,
            // 韧性 + 免伤
            baseToughness = baseToughness,
            toughnessDefenseScale = toughnessDefenseScale,
            baseDamageReduce = baseDamageReduce,
            // 保护者权重（3.7 保护力加权和）+ 重甲标记
            protectPower = protectPower,
            isHeavyArmor = isHeavyArmor,
            roleFamily = roleFamily,
            // 骑兵冲锋
            isCavalry = isCavalry,
            chargeDamage = chargeDamage,
            chargeRangeCells = chargeRangeCells,
            chargeSpeed = chargeSpeed,
            chargePairGap = chargePairGap,
            chargeGroupCooldown = chargeGroupCooldown,
            chargeDamageReduce = chargeDamageReduce,
            // 工事（FortificationDef → 快照拉平）
            isFortification = fortification != null,
            fortDefenseLevel = fortification != null ? fortification.defenseLevel : 0,
            fortHeightCells = fortification != null ? fortification.heightCells : 1f,
            fortBlocksMovement = fortification != null && fortification.blocksMovement,
            fortPassable = fortification != null && fortification.passable,
            fortMeleeDamageReduce = fortification != null ? fortification.meleeDamageReduce : 0f,
            // 弹药储备/补给（3.7 战争机器火力经济学）
            ammoMax = ammoMax,
            ammoCostStone = ammoCostStone,
            ammoCostFireball = ammoCostFireball,
            ammoCostMagic = ammoCostMagic,
            ammoResupplyDelay = ammoResupplyDelay,
            ammoConservationWeight = ammoConservationWeight,
            // 战争机器乘员（改动②）
            crewRequired = crewRequired,
            crewRadiusCells = crewRadiusCells,
        };
    }
}
