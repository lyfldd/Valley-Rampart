using System.Collections.Generic;

// ============================================================================
//  地块与地图生成数据结构（3.2 第 7.3 节 + 3.2.1 第 6.2 节）
//  本文件只放数据结构，不含 ScriptableObject 配置（见 Data/ 目录）。
//
//  改造计划 doc 1：
//    - GridCoord 2D 化 + layer 预留 + 强哈希（§5.1）
//    - WalkFlags 五位位标记（§5.1）
//    - GridCell 瘦身：occupant/isObstacle/terrain 下沉到 GridSystem 稠密数组，
//      此处保留 Obsolete 过渡属性供兼容期读取（改造完毕删除）
//    - 删除 BigTerrain / MapZone / Region / BuildingPlaceholder（1D 概念，生成归 2_1）
// ============================================================================

/// <summary>2D 平面格坐标。x=横轴，y=纵轴（语义变更：老 y=层已废，改动见改造计划 doc 1 §1.2）。</summary>
public struct GridCoord
{
    public int x;
    public int y;          // 纵轴格号（0..H-1）
    public int layer;      // 预留：0=地面（空中层冻结，用户决策 2026-08-12）

    public GridCoord(int x, int y, int layer = 0) { this.x = x; this.y = y; this.layer = layer; }

    public static bool operator ==(GridCoord a, GridCoord b)
        => a.x == b.x && a.y == b.y && a.layer == b.layer;
    public static bool operator !=(GridCoord a, GridCoord b) => !(a == b);
    public override bool Equals(object obj) => obj is GridCoord c && this == c;
    public override int GetHashCode() => x * 73856093 ^ y * 19349663 ^ layer * 83492791;
    public override string ToString() => $"(x={x}, y={y}, layer={layer})";
}

/// <summary>可行走位标记（byte，每格一份）。可走判定规则：TerrainWalkable 置位
/// 且 BuildingBlocked/Locked/Water 均未置位，或 Bridge 置位（桥面豁免水域阻挡，2_2 桥写入）。</summary>
[System.Flags]
public enum WalkFlags : byte
{
    None            = 0,
    TerrainWalkable = 1 << 0,  // 地形基础可走（地图生成写入）
    BuildingBlocked = 1 << 1,  // 建筑障碍（MarkOccupied 且 isObstacle 时置位）
    Locked          = 1 << 2,  // 资源点锁格（3.5 锁格机制延续，写入方 2_2/2_7）
    Water           = 1 << 3,  // 水域（2_1 河流/湖泊/海洋）
    Bridge          = 1 << 4,  // 桥面（2_2 桥建筑置位：桥可跨水，优先级高于 Water）
    // bit5~7 预留（空中层/特殊地形）
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

/// <summary>小地形类型（doc 1：语义从"每 Region"变"每格"）。</summary>
public enum TerrainType
{
    Plain,      // 平原（子状态见 PlainSubState）
    Wasteland,  // 荒地
    Hills,      // 丘陵（万能缓冲 + 复合资源区）
    Forest,     // 林地（木来源）
    Quarry,     // 矿山（石来源）
    Snow,       // 雪山
    Coast,      // 海岸
    Mountain,   // 山地（2_1 §5.1 映射：FeatureType.Mountain，阻挡）
    River,      // 河流（2_1 §5.1 映射：FeatureType.River，水域阻挡）
    Lake,       // 湖泊（2_1 §5.1 映射：FeatureType.Lake，水域阻挡）
    Ocean       // 海洋（2_1 §5.1 映射：FeatureType.Ocean，边缘环绕阻挡）
}

/// <summary>平原子状态（仅 terrain==Plain 时有效）。</summary>
public enum PlainSubState
{
    Normal,    // 普通平原（建造位为主）
    Fertile    // 肥沃（农田位，粮来源）
}

// ===== 2_1 地图生成契约（features 唯一功能源，doc 1 §5.5 扩展）=====

/// <summary>温度带（大区块 16×16 属性，散乱分布不按纬度，2_1 §3.2）。</summary>
public enum ClimateZone { Tropical, Subtropical, Temperate, Cold }

/// <summary>小区块特征物（功能层，2_1 §5.1 唯一功能源）。可走/阻挡由 GridSystem 派生 walkFlags。</summary>
public enum FeatureType
{
    Plain,             // 可走/可建
    Tree,              // 一次性木（可刷新，木无产能建筑 2_12）
    Mountain,          // 阻挡
    SnowMountain,      // 阻挡
    Mine,              // 矿洞（石，需争夺；可走，Locked 由 2_2/2_7 置）
    OreVein, StonePile, WoodPile,  // 一次性资源（可走）
    River, Lake, Ocean             // 水（阻挡）
}

/// <summary>自然建筑占位（features 派生的视觉层，供 2_2 实例化，不反向改可走，2_1 §5.1）。</summary>
[System.Serializable]
public class NaturalBuilding
{
    public int cellX, cellY;        // 落点（2×2 建筑取左上）
    public int w = 1, h = 1;        // 默认 1×1；仅 Mine/大型岩石等特殊特征物可升 2×2
    public FeatureType feature;     // 对应特征物
    public ClimateZone climate;     // 所属温度带（决定美术变体）
    public string artId;            // 美术资源 id（占位，以美术资源规范为准 D37）
}

// 注：BigTerrain / MapZone 已删除（doc 1 §2.2：单大陆 + 5 区是 1D 概念）

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
    Production,  // 产能建筑（农场/矿场/采石场）
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

// 注：BuildingPlaceholder 已删除（doc 1 §2.2：2D 化归 2_1 重新定义 NaturalBuilding）

// ===== 区块结构 =====

/// <summary>
/// 小区块：单位堆叠 + 建筑占用最小单元。
/// doc 1 §2.2 / §5.3：occupant/isObstacle/terrain 已下沉到 GridSystem 稠密数组
/// （_occupants/_walkFlags/_terrain），本 class 只承载稀疏懒分配的单位列表。
/// 旧字段保留为 Obsolete 过渡属性，从 GridSystem 数组读取；改造完毕删除。
/// </summary>
public class GridCell
{
    /// <summary>本格坐标（由 GridSystem 懒分配时写入）。</summary>
    public GridCoord Coord;

    /// <summary>本格单位列表（微格登记聚合到小区块后落在格级列表）。</summary>
    public readonly List<UnitController> Units = new List<UnitController>();

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

    // ===== 过渡 Obsolete 属性（已下沉 GridSystem 数组，2_2/2_4 适配完成后删除）=====

    /// <summary>[过渡已废弃] 占据此格的建筑。改用 GridSystem.IsOccupied / GetOccupant。</summary>
    [System.Obsolete("GridCell.occupant 已下沉 GridSystem._occupants，改用 GridSystem.IsOccupied/GetOccupant")]
    public Building occupant
    {
        get { var g = GridSystem.Instance; return g != null ? g.GetOccupant(Coord) : null; }
    }

    /// <summary>[过渡已废弃] 是否障碍格。改用 GridSystem.IsObstacle。</summary>
    [System.Obsolete("GridCell.isObstacle 已下沉，改用 GridSystem.IsObstacle")]
    public bool isObstacle
    {
        get { var g = GridSystem.Instance; return g != null && g.IsObstacle(Coord); }
    }

    /// <summary>[过渡已废弃] 此格地形。改用 GridSystem.GetTerrainAt。</summary>
    [System.Obsolete("GridCell.terrain 已下沉 GridSystem._terrain，改用 GridSystem.GetTerrainAt")]
    public TerrainType terrain
    {
        get { var g = GridSystem.Instance; return g != null ? g.GetTerrainAt(Coord) : TerrainType.Plain; }
    }
}