using System.Collections.Generic;

// ============================================================================
//  地块与地图生成数据结构（3.2 第 7.3 节 + 3.2.1 第 6.2 节）
//  本文件只放数据结构，不含 ScriptableObject 配置（见 Data/ 目录）。
// ============================================================================

/// <summary>小区块坐标。x=横向列号，y=子层（0=地面，1=空中）。</summary>
public struct GridCoord
{
    public int x;
    public int y;

    public GridCoord(int x, int y) { this.x = x; this.y = y; }

    public static bool operator ==(GridCoord a, GridCoord b) => a.x == b.x && a.y == b.y;
    public static bool operator !=(GridCoord a, GridCoord b) => !(a == b);
    public override bool Equals(object obj) => obj is GridCoord c && this == c;
    public override int GetHashCode() => x * 31 + y;
}

/// <summary>NPC 类型分类（决定堆叠上限）。不划分防御类驻军——
/// NPC 白天打猎/种田、晚上防御是行为模式而非阵营分类。
/// 防御建筑是建筑本体（非 NPC 驻军）。</summary>
public enum UnitCategory
{
    Enemy,     // 敌人 NPC（怪物/王国兵，上限中）
    Civilian   // 普通 NPC（村民/工匠，无上限）
}

// ===== 地形类型 =====

/// <summary>小地形类型（对应大区块）。平原拆两态，肥沃降为子状态。</summary>
public enum TerrainType
{
    Plain,      // 平原（子状态见 PlainSubState）
    Wasteland,  // 荒地（内陆极端区出怪侧）
    Hills,      // 丘陵（万能缓冲 + 复合资源区）
    Forest,     // 林地（木来源）
    Quarry,     // 矿山（石来源）
    Snow,       // 雪山（仅内陆大山屏障侧）
    Coast       // 海岸（仅岛屿两端，出海口+造船厂位）
}

/// <summary>平原子状态（仅 terrain==Plain 时有效）。</summary>
public enum PlainSubState
{
    Normal,    // 普通平原（建造位为主）
    Fertile    // 肥沃（农田位，粮来源）
}

/// <summary>大地形类型（玩家/关卡决定，决定出怪方向）。</summary>
public enum BigTerrain
{
    Island,          // 岛屿（两边出怪）
    Inland,          // 内陆（一边出怪，另一边大山屏障）
    EndlessIsland    // 无限关卡固定岛屿（通关后）
}

/// <summary>5 区结构分区枚举（3.2.1 第二节）。</summary>
public enum MapZone
{
    LeftExtreme,    // 左极端区（出怪口/大山屏障）
    LeftResource,   // 左资源区
    Center,          // 中心区（主城）
    RightResource,  // 右资源区
    RightExtreme    // 右极端区（出怪口/造船厂位）
}

// ===== Building 体系（3.2.1 第 6.2 节）=====

/// <summary>Building 大类。所有资源点/特殊点/裂隙/主城/玩家建造都是 Building 子类。</summary>
public enum BuildingCategory
{
    ResourceProducer,  // 持续性资源（树/矿洞/农田）
    ResourcePickup,    // 一次性资源（石头堆/木头堆/矿脉）
    SpecialPoint,      // 特殊点（宝箱/遗迹/交互建筑）
    Rift,              // 裂隙（出怪口）
    CastleCore          // 主城
}

/// <summary>建筑玩法功能标签（3.3.1 P2 方案A）。与 BuildingCategory（来源分类）正交：
/// BuildingCategory 描述"这栋建筑从哪来/是什么大类"（地图生成侧用），
/// BuildingRole 描述"这栋建筑在玩法上起什么作用"（3.3 BuildingDef 用）。
/// 两套枚举不同名不同义，避免精神分裂。</summary>
public enum BuildingRole
{
    Defense,     // 防御建筑（箭塔/投石机/陷阱）
    Production,  // 产能建筑（农场/伐木场/矿场）
    Economy,     // 经济建筑（仓库/市场）
    Wall,        // 城墙/障碍
    Special      // 特殊（裂隙/主城/遗迹/宝箱）
}

/// <summary>Building 具体类型。</summary>
public enum BuildingType
{
    None,
    // 持续性
    Tree,         // 树（木来源）
    Mine,         // 矿洞（石来源）
    Farmland,     // 农田（粮来源）
    // 一次性
    StonePile,   // 石头堆
    WoodPile,    // 木头堆
    OreVein,     // 矿脉（高等级）
    // 特殊点
    TreasureBox, // 宝箱
    Ruins,       // 遗迹
    Interactable,// 交互建筑（未来）
    // 功能
    Rift,        // 裂隙（出怪口）
    CastleCore,  // 主城
    // ===== 3.5.1 实体化（E-S7，末尾追加保持 int 稳定）=====
    VagrantCamp  // 流浪汉营地（3.5.1 §4.1：开局 2-3 个，近王国区块必有 1 个，禁落核心区块）
}

/// <summary>资源点等级（影响产出/获得量）。</summary>
public enum ResourceGrade
{
    Barren,   // 贫瘠（×0.5）
    Normal,   // 普通（×1.0）
    Rich      // 富有（×2.0，高风险区倾向）
}

/// <summary>
/// 地图生成产出的 Building 占位。运行时由 BuildingFactory（建造系统⬜）转为 Building 实例。
/// 这是"生成结果"数据，不是运行时实例。
/// </summary>
public class BuildingPlaceholder
{
    public BuildingCategory category;   // 大类
    public BuildingType type;            // 具体类型
    public int localCellX;               // 在大区块内的小区块局部坐标
    public int cellWidth = 1;            // 占几个小区格（默认 1，城堡=2）
    public ResourceGrade grade;          // 等级（仅资源点有效）
    public bool isConsumable;            // 是否一次性（true=用完消失，false=持续产出）
}

// ===== 区块结构 =====

/// <summary>小区块：单位堆叠 + 建筑占用最小单元。</summary>
public class GridCell
{
    public GridCoord Coord;
    public readonly List<UnitController> Units = new List<UnitController>();

    // ===== 建筑占用层（3.3.1 P1）=====
    /// <summary>占据此格的建筑（null=空）。footprint 多格建筑会在每个格都存同一引用。</summary>
    public Building occupant;
    /// <summary>缓存 occupant.isObstacle，避免每次读 occupant 空检查。</summary>
    public bool isObstacle;
    /// <summary>此格地形（由 GridSystem.GetTerrainAt 懒填充）。</summary>
    public TerrainType terrain;

    public int Count => Units.Count;

    public int CountByCategory(UnitCategory category)
    {
        int c = 0;
        for (int i = 0; i < Units.Count; i++)
            if (Units[i].GetCategory() == category) c++;
        return c;
    }

    public void Add(UnitController unit) => Units.Add(unit);
    public bool Remove(UnitController unit) => Units.Remove(unit);
}

/// <summary>
/// 大区块：地形段，程序化生成基本单位。
/// 每个大区块对应一种小地形，内部含固定 regionCellCount 个小区块。
/// </summary>
public class Region
{
    public int regionIndex;              // 在地图内的索引（0..M-1）
    public TerrainType terrain;          // 小地形类型
    public PlainSubState plainSubState;  // 平原子状态（仅 terrain==Plain 时有效）
    public int cellStartX;               // 该大区块第一个小区块的全局 x 坐标
    public int cellCount;                // 含小区块数（= regionCellCount，固定）
    public List<BuildingPlaceholder> resources; // Building 占位列表（二级约束生成）
    public int riftCellX = -1;           // 裂隙所在小区块索引（-1=无裂隙）
    public bool isEnemyTerritory;        // 是否敌方领土（敌方王国地图标记）
    public MapZone zone;                 // 所属 5 区分区
    public bool isInner;                 // 资源区是否内侧（靠中心区=true，靠极端区=false）

    // ===== QQQ.1 需求1：资源保障占位标记（3.2.1 第四节，事前占位替代事后补丁）=====
    /// <summary>是否为资源保障占位区块（true 时 terrain 固定为 protectedTerrain，不被权重随机/邻接修正覆盖）。</summary>
    public bool isProtectedResource;
    /// <summary>保障区块固定地形（Forest=保障林地 / Quarry=保障矿山）。</summary>
    public TerrainType protectedTerrain = TerrainType.Plain;
}
