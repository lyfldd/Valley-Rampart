using System;
using UnityEngine;

/// <summary>
/// 建筑配置（ScriptableObject）。所有建筑属性集中于此，Inspector 可调，数据驱动。
/// 运行时 Building 实例持有本配置引用 + 运行时状态（level/hp/存储）。
///
/// 3.3.1 P2: 用 BuildingRole（玩法功能标签）替代原设计的 BuildingCategory，避免与 GridTypes 的来源分类同名冲突。
/// 3.3.1 P6: 加 sourceType（地图预置映射）+ gradeScale（等级缩放），供 BuildingFactory 查 BuildingMappingTable。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/BuildingDef", fileName = "BuildingDef")]
public class BuildingDef : ScriptableObject
{
    [Header("基础")]
    public string id;
    public string displayName;
    [TextArea] public string description;
    public Faction faction;
    public BuildingRole role;          // 玩法功能标签（P2 方案A）

    [Header("造价与占位")]
    // ⭐ `M1-A`／`09#50`／`09#51`：造价 ＝ **「资源量列表」`ResourceList`**（与配方表同形 · §7.3）。
    // ✅ **资产迁移三步法已完成**（`D789` Q1=A）：①旧字段保留一版反序列化 ②一次性 Editor 工具读旧填新
    //    ③逐项核对表 88 行全一致 ⇒ **旧字段与 `ResourcePack` 类型已退役**（核对表：工程根 `M1A_资产迁移核对表.txt`）。
    [Tooltip("造价（资源量列表：资源＋量 · 09#50/09#51）。⛔ 非固定桶 ⇒ 资源表加项即自动可表达")]
    public ResourceList cost;
    public Vector2Int footprint;       // 占用小区块尺寸 (w,h)，2D 全用
    [Tooltip("允许建造的**地表物**（FeatureType；空=不校验）。HH.294 片2-A：原 TerrainType 整层删 ⇒ 改读地表物。"
             + "例：mine.asset=[Mine]（矿山锚点）——「矿洞只能建在矿山上」红线（D737）")]
    public FeatureType[] allowedTerrain;

    [Header("2D 空间（2_2 建筑与占格）")]
    [Tooltip("纯视觉层数（美术规范 §1.2），不参与逻辑，只影响 sprite 尺寸（2_10 渲染用）")]
    public int heightLayer = 0;
    [Tooltip("桥专属：true 时仅校验 Water 位（只能造在水上），其余 false")]
    public bool canPlaceOnWater = false;
    [Tooltip("语义标记：是否桥（工事区分，D62/D64）")]
    public bool isBridge = false;
    [Tooltip("语义标记：是否城门（工事区分，D62/D64）")]
    public bool isGate = false;
    [Tooltip("可否旋转（城门/桥 true，R 键切换朝向）")]
    public bool rotatable = false;

    [Header("模块归属（3.5 §2.2 归属原则）")]
    [Tooltip("所属王国模块。Civil=土木/Production=生产/Livelihood=民生/Military=军事/Commerce=商业/Science=科技。用于模块级解锁判定")]
    public ModuleType moduleType;       // 3.5：建筑归属模块（模块级解锁门槛依据）

    [Header("仓库（`09` §五 · ⭐ `M1-A` 新增）")]
    [Tooltip("**收什么**（仓库声明路径前缀 · `09` §三）：可多条；空 ⇒ 通用仓 `res`（全部资源）。\n" +
             "例：res＝全部／res_material＝全部材料／res_material_ore＝只收矿石。\n" +
             "⭐ 加一个仓／改它收什么 ＝ **只改这一行数据**（`09` §十一 判据 1）⇒ ⛔ 不改 StorageComponent/ProductionSystem。\n" +
             "⚠️ 本栏是「仓库标签」的**过渡数据源**；`10` 建筑能力表的 `store` 能力（`M6-F`）落地后迁入能力声明。")]
    public string[] warehousePaths;

    [Tooltip("**能否掉落**（`09` §九 · ⭐ `M1-D` 件4 新增 · 第二维）：本建筑（本体仓）生命周期结束时，仓内容是否转为掉落箱。\n" +
             "默认 true＝**默认可掉**（`D803` 裁）；须拦的仓（人口仓/水仓等）显式置 false。\n" +
             "⛔ 与 warehousePaths（收什么）正交 —— 不参与前缀匹配；⛔ 不入档（随资产重建恢复）。")]
    public bool droppable = true;

    [Header("行为标记")]
    public bool isObstacle;            // 是否阻挡移动/寻路（城墙=是；资源点=否）
    public ProducerConfig producer;    // 非空 = 生成物（产资源 or 产单位）
    public CombatConfig combat;        // 非空 = 防御建筑（攻击属性，交 3.5/3.4）
    public BuildingLevel[] levels;     // 升级档位

    [Header("⭐ 组件绑定（`M4-A` · `08` §7.3：数据行显式列表）")]
    [Tooltip("本栋挂哪些行为组件（键表＝`BuildingComponentRegistry`，形如 comp.storage／comp.producer／comp.combat…）。\n" +
             "空数组 ⇒ 不挂任何行为组件。⭐ 挂什么由本栏决定，`BuildingFactory.AttachComponents` 只遍历本栏查表。")]
    public string[] components;

    [Header("战争机器乘员（改动②：投掷机需工人操作；对齐 NpcProfessionDef.crewRequired）")]
    [Tooltip("需几名工人操作（0=不需工人，恒可工作）。Catapult=2")]
    public int crewRequired = 0;
    [Tooltip("工人操作半径（格）。工人在此半径内即算操作机器（运作战争机器任务）")]
    public float crewRadiusCells = 0f;

    [Header("产能（3.3.4 批次5）")]
    [Tooltip("产出资源类型（producer.kind==Resource 时用）")]
    public ResourceType outputResource;
    [Tooltip("是否为资源点（原始矿洞/树林/农田）。true=自身不产出，仅作工具放置前置（批次6）。" +
             "语义分两类（DZ-054/D617）：一次性探明矿点（ore_vein=1）vs 开局过渡堆积（stone_pile/wood_pile=0）——" +
             "三者值不齐=语义差异非缺陷，不统一为 1（统一会使 stone/wood_pile 经 WanderStimulusProvider 新增 NPC 游荡锚点=行为漂移）。" +
             "实盘 4 消费面=WanderStimulusProvider.IsResourceDef / BuildingPanel.canDemolish / Building.Demolish / BuildingFactory 产能分支")]
    public bool isResourceNode = false;
    [Tooltip("是否为铁匠铺（2_12 步骤8，D199~D201）。true=挂 BlacksmithBuilding（矿石→Metal 就地加工 D200/D609），跳过通用 ProducerComponent")]
    public bool isBlacksmith = false;
    [Tooltip("是否为投掷机厂（2_12 步骤9，D207~D212）。true=挂 SiegeWorkshopBuilding（弹药产出入厂级弹药仓，仿 BlacksmithBuilding 专属组件），跳过通用 ProducerComponent；弹仓容量取 producer.capacity（HH.19 A×4）")]
    public bool isSiegeWorkshop = false;

    [Header("产能并发与训练（3.5 P0-5：3.5.4 建筑数据卡 §8.2/§8.4）")]
    [Tooltip("产能建筑并发工人数（0=不限/默认，允许任意数量工人同时操作该建筑）")]
    public int concurrentWorkers = 0;
    [Tooltip("训练建筑训练槽位（Lv1=1 / Lv2=2 / Lv3=3，其他=0）。等级缩放由升级档位 phase 处理，P1 训练队列接入")]
    public int trainingSlots = 0;

    [Tooltip("解锁所需主城等级（1=修复后即可建，2/3=主城升级后解锁）")]
    public int unlockLevel = 1;

    [Header("交互与生命周期")]
    public bool isPlayerBuilt = true;  // false = 地图预置（不可拆/不可移）
    public bool isDestructible = true;
    [Tooltip("建筑 HP 统一入口（3.5.1 E-S10）：所有建筑 HP 基准值（再乘 gradeScale）。" +
             "防御建筑 combat.maxHp 与本值同值（战斗属性仍归 combat）；非战斗建筑不再走默认100硬编码")]
    public int maxHp = 100;

    [Header("地图预置映射（3.3.1 P6）")]
    [Tooltip("能由哪种地图 BuildingPlaceholder 转换来。None=只能玩家建造")]
    public BuildingType sourceType = BuildingType.None;
    [Tooltip("一次性资源（用完消失）。true=WoodPile/StonePile/OreVein；false=Tree/Mine/Farmland")]
    public bool isConsumable = false;
    [Tooltip("一次性资源点采集耗时（秒，QQQ.2 T19 / DR-11：WoodPile 2s / StonePile 4s / OreVein 8s）")]
    public float gatherSeconds = 2f;
    [Tooltip("ResourceGrade 缩放系数：[0]=Barren, [1]=Normal, [2]=Rich。作用于 producer.rate 和 combat.maxHp")]
    public float[] gradeScale = new float[] { 0.7f, 1.0f, 1.5f };

    [Header("表现")]
    public GameObject prefab;

    [Header("怪物目标（2_14 步骤7：价值×距离选目标 D83，SO 化禁硬编码）")]
    [Tooltip("怪物目标选择价值分（0=怪物不选此目标；乘 MonsterDef.valueWeight 参与 CombatRules.TargetScore 价值×距离评分）")]
    public float monsterTargetValue = 1f;
    [Tooltip("高价值标志（主城/矿洞/仓库/铁匠铺等高价值目标；配合价值分决定怪物目标优先级）")]
    public bool monsterIsHighValue = false;

    [Header("专属种族（2_20 M6 四族专属建筑）")]
    [Tooltip("专属种族（RaceIds：0=Human 1=Elf 2=Dwarf 3=Orc）。-1=共通建筑（全族可建）。玩家建造门面/菜单按国族过滤；AI 同规则（D419 专属建筑每族 1 栋）")]
    public int raceId = -1;
    [Tooltip("每族限建 1（2_20.1 §三：全局效果防叠乘失控，Registry 查重）。true=同王国同 id 已建则拒建")]
    public bool uniquePerKingdom = false;

    /// <summary>按资源等级获取缩放系数。</summary>
    public float GetGradeScale(ResourceGrade grade)
    {
        int idx = (int)grade;
        if (gradeScale == null || idx < 0 || idx >= gradeScale.Length) return 1.0f;
        return gradeScale[idx];
    }
}

// ===== 配置子结构 =====

[Serializable]
public struct ProducerConfig
{
    public ProduceKind kind;   // Resource / Unit
    public float rate;         // 每秒产出
    public int capacity;       // 存储上限
}

[Serializable]
public struct CombatConfig
{
    public int attack;
    public int defense;
    public int maxHp;
    public float range;
    public DamageType damageType;
    [Tooltip("攻击冷却（秒）。防御建筑攻速 SO（2_12 步骤14 顺手项：步骤12 自记 AttackCD=0.5 占位迁入）；0=回退 0.5s")]
    public float attackCooldown;
}

[Serializable]
public struct BuildingLevel
{
    [Tooltip("升级造价（资源量列表：资源＋量 · 09#50/09#51）")]
    public ResourceList upgradeCost;
    public float statScale;       // 升级后属性乘数
    public string[] prerequisites;// 前置（科技/时代，接未来科技系统）
}

// ===== 枚举 =====

public enum ProduceKind { Resource, Unit }
public enum DamageType { Physical, Fire, Cold, Magic }
