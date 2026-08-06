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
    public Vector2Int footprint;       // 占用小区块尺寸 (w,h)，1D 地图 h 暂不用
    public TerrainType[] allowedTerrain;

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

    [Tooltip("解锁所需主城等级（1=修复后即可建，2/3=主城升级后解锁）")]
    public int unlockLevel = 1;

    [Header("交互与生命周期")]
    public InteractableType interactableType;
    public bool isPlayerBuilt = true;  // false = 地图预置（不可拆/不可移）
    public bool isDestructible = true;

    [Header("地图预置映射（3.3.1 P6）")]
    [Tooltip("能由哪种地图 BuildingPlaceholder 转换来。None=只能玩家建造")]
    public BuildingType sourceType = BuildingType.None;
    [Tooltip("一次性资源（用完消失）。true=WoodPile/StonePile/OreVein；false=Tree/Mine/Farmland")]
    public bool isConsumable = false;
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
