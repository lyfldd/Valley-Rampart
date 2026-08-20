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
    public ResourcePack cost;          // 金/石/木/粮（走 RulerController.CanAfford/Spend）
    public Vector2Int footprint;       // 占用小区块尺寸 (w,h)，2D 全用
    public TerrainType[] allowedTerrain;

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

    [Header("行为标记")]
    public bool isObstacle;            // 是否阻挡移动/寻路（城墙=是；资源点=否）
    public ProducerConfig producer;    // 非空 = 生成物（产资源 or 产单位）
    public CombatConfig combat;        // 非空 = 防御建筑（攻击属性，交 3.5/3.4）
    public BuildingLevel[] levels;     // 升级档位

    [Header("战争机器乘员（改动②：投掷机需工人操作；对齐 NpcProfessionDef.crewRequired）")]
    [Tooltip("需几名工人操作（0=不需工人，恒可工作）。Catapult=2")]
    public int crewRequired = 0;
    [Tooltip("工人操作半径（格）。工人在此半径内即算操作机器（运作战争机器任务）")]
    public float crewRadiusCells = 0f;

    [Header("产能（3.3.4 批次5）")]
    [Tooltip("产出资源类型（producer.kind==Resource 时用）")]
    public ResourceType outputResource;
    [Tooltip("是否为资源点（原始矿洞/树林/农田）。true=自身不产出，仅作工具放置前置（批次6）")]
    public bool isResourceNode = false;

    [Header("产能并发与训练（3.5 P0-5：3.5.4 建筑数据卡 §8.2/§8.4）")]
    [Tooltip("产能建筑并发工人数（0=不限/默认，允许任意数量工人同时操作该建筑）")]
    public int concurrentWorkers = 0;
    [Tooltip("训练建筑训练槽位（Lv1=1 / Lv2=2 / Lv3=3，其他=0）。等级缩放由升级档位 phase 处理，P1 训练队列接入")]
    public int trainingSlots = 0;

    [Tooltip("解锁所需主城等级（1=修复后即可建，2/3=主城升级后解锁）")]
    public int unlockLevel = 1;

    [Header("交互与生命周期")]
    public InteractableType interactableType;
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
}

[Serializable]
public struct BuildingLevel
{
    public ResourcePack upgradeCost;
    public float statScale;       // 升级后属性乘数
    public string[] prerequisites;// 前置（科技/时代，接未来科技系统）
}

// ===== 枚举 =====

public enum ProduceKind { Resource, Unit }
public enum InteractableType { Own, Enemy, Resource }
public enum DamageType { Physical, Fire, Cold, Magic }
