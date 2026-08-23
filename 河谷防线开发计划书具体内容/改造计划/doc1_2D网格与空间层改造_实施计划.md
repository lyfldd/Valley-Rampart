# doc1 2D 网格与空间层改造 实施计划

> 日期：2026-08-14
> 状态：**已落地（2026-08-19，编译 0 error，验收全过）**——原头部"待实施"为滞后标记（2026-08-23 对账修正）；HH.3 iso 统一裁决（2026-08-22）的迁移标记部分待落
> 前置：**[0_改造总计划](./0_改造总计划.md)**（三大核心决策 + 世界结构补充决策）
> 目标：GridCoord 2D 化 + GridSystem 稠密数组 + 坐标换算 + MapData 契约 + IPathGrid + 微格 API + WalkFlags 五位，全部旧数据定义迁移到新契约
> 边界：本片**不重写地图生成**（归 2_1）、**不重写渲染**（归 2_10）、**不重写移动**（归 2_3）；只完成空间层地基 + 全部消费方编译适配
> 影响面：全项目 28 个文件、约 236 处 `GridSystem` 引用（2026-08-12 全库实测）；`WorldToCoord` 签名 nullable 为 **P0 强制**（D2）

---

## 零、前置检查（不满足则停）

- [ ] 已阅读设计稿 [1_2D网格与空间层改造](./1_2D网格与空间层改造.md) 全文，确认 §5.1~§5.9 契约
- [ ] 旧代码基线确认：`Systems/Grid/GridSystem.cs`、`GridTypes.cs` 当前为 1D 实现（`_cells: Dictionary<GridCoord,GridCell>`、`originX`、`flyHeight`、`MapCellCount`）
- [ ] `GridConfig.asset` / `MapSizeConfig.asset` 资产存在（`Resources/Grid/` 下），改造后需同步改字段
- [ ] 确认 `WorldToCoord` 全部调用方（§2.7 总表 28 文件）已列出，本片一次性完成 nullable 签名适配（D2 P0）
- [ ] 存档兼容策略已定：旧档 schema 迁移归 2_11，改造期旧档可显式作废（总计划原则 5）
- [ ] 明确：本片只改空间层，`EnemyEnteredChunkEvent` 改名 + 订阅方适配归 2_7；`EnemyEnteredRegionEvent` 删除

---

## 一、实施步骤（按依赖序，每步可独立验收）

### 步骤 1：GridCoord 2D 化 + layer 字段 + 强哈希

**文件**：`Systems/Grid/GridTypes.cs`（改动）

**旧定义迁移**（旧 1D 语义作废）：
```csharp
// 旧（1D）：x=横轴格号，y=层（0=地面, 1=空中），GetHashCode=x*31+y
public struct GridCoord
{
    public int x;
    public int y;            // y = 层（0=地面,1=空中）—— 本字段语义作废
    public GridCoord(int x, int y) { this.x = x; this.y = y; }
    public override int GetHashCode() => x * 31 + y;
}
```

**新定义**（保留结构体名，避免 27 文件改名风暴）：
```csharp
/// <summary>2D 平面格坐标。x=横轴，y=纵轴（语义变更：老 y=层已废）。</summary>
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
}
```

**§1.2 全局核查清单（随本步骤清零，四种模式逐处人工确认）**：
- 模式 1：`coord.y == 0` / `coord.y == 1` / `coord.y > 0` —— 老层判断，全部作废
- 模式 2：`new GridCoord(x, 0)` 字面量构造 —— 语义恰好兼容（地面层 y=0 ↔ 纵轴 0），抽查确认
- 模式 3：`flyHeightThreshold` / `flyHeight` —— 删除
- 模式 4：`GetHashCode` —— 强哈希已替换

**验收**：编译通过；全库四种模式 grep 结果逐处确认清零（产出核查清单表格记录）；`GridCoord` 仍可作 Dictionary key。

### 步骤 2：WalkFlags 位标记（五位）

**文件**：`Systems/Grid/GridTypes.cs`（新增）

```csharp
/// <summary>可行走位标记（byte，每格一份）。</summary>
[Flags]
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
```

> 可走判定规则（内联在 GridSystem，寻路经 `IPathGrid` 读取）：`TerrainWalkable` 置位 **且** `BuildingBlocked/Locked/Water` 均未置位，**或** `Bridge` 置位（桥面豁免水域阻挡，由 2_2 桥建筑写入）。

**旧定义迁移**：旧 `GridCell` 用 `bool isObstacle` + `occupant` 表达占用；新以 `WalkFlags` 位标记为**唯一功能源**，`isObstacle` 退化为缓存（见步骤 5）。

**验收**：编译通过；位运算按规则表逐位核对。

### 步骤 3：GridConfig 字段改造 + SO 资产同步

**文件**：`Data/GridConfig.cs`（改动）+ `Resources/Grid/GridConfig.asset`（同步改）

**旧字段迁移**（旧值 → 新处置）：
| 旧字段 | 旧默认 | 处置 |
|--------|--------|------|
| `cellSize` | 2.26（float 标量） | **改 `Vector2 (1.28, 0.64)`**（=Tile 全尺寸，轴向步长取半尺寸 0.64/0.32，2026-08-22 裁决 HH.3）；**双分量，距离公式见 §1.6，禁标量化** |
| `regionCellCount` | 16 | **改名 `chunkSize = 16`**（语义变边长） |
| `midRegionCellCount` | 4 | **改名 `midChunkSize = 4`** |
| `originX` | 0 | **删除**（改地图中心原点，§5.2） |
| `flyHeightThreshold` / `flyHeight` | 5 / 8 | **删除** |
| `stackLimits` | — | 保留（上限数值重审归 2_5） |
| 新增 | — | `subCellDivisor = 4`（小区块÷4=微格）、`mapOriginPolicy`（固定 Center）、`drawGizmos = true` |

```csharp
public class GridConfig : ScriptableObject
{
    public Vector2 cellSize = new Vector2(1.28f, 0.64f); // 世界单位/格，双分量，禁标量化
    public int subCellDivisor = 4;   // 小区块 ÷ 4 = 微格（寻路粒度）
    public int chunkSize = 16;       // Chunk 边长（格）
    public int midChunkSize = 4;     // MidChunk 边长（格）
    public StackLimitConfig[] stackLimits = new StackLimitConfig[2];
    public bool drawGizmos = true;
    // 删除：originX / flyHeightThreshold / flyHeight
}
```

**验收**：`GridConfig.asset` 字段与类一致；旧 `originX` 相关引用 grep 清零。

### 步骤 4：MapSizeConfig 2D 档位 + WorldState/MapData 契约改造

**文件**：`Data/MapSizeConfig.cs`（改动）+ `Systems/World/WorldState.cs`（改动）+ `Resources/Grid/MapSizeConfig.asset`（同步改）

**旧定义迁移**（`MapSizeConfig`）：
```csharp
// 旧：regionCount = 大区块数 M（Small=10/Medium=15/Large=24）
[Serializable]
public struct MapSizeEntry
{
    public WorldSize size;
    public int regionCount;   // 大区块数 M —— 改为 2D 宽高
}

// 新：width / height（格数）
[Serializable]
public struct MapSizeEntry
{
    public WorldSize size;
    public int width;         // 格数（推荐 Small=128 / Medium=256 / Large=384，D1）
    public int height;        // 格数（= width，方形）
}
```
- `GetEnemyMapBase(int difficulty)` 语义从"敌方王国地图数"变"同图 AI 王国数"，逻辑保留，数值重审归 2_1/2_8
- `enemyByDifficulty` 字段保留

**旧定义迁移**（`MapData`/`WorldState`）：
```csharp
// 旧（1D）：regions/bigTerrain/isPlayerHome/isConquered
public class MapData
{
    public int mapId;               // 唯一 ID
    public int seed;
    public BigTerrain bigTerrain;   // —— 删除（单大陆）
    public List<Region> regions;    // —— 废弃（由 2D 生成产出物接管）
    public bool isPlayerHome;       // —— 冻结保留
    public bool isConquered;        // —— 冻结保留
}

// 新（2D 最小空间契约 §5.5）
public class MapData
{
    public int mapId;                    // 冻结（恒 0）
    public int seed;
    public int width;                    // 格数
    public int height;
    public TerrainType[]   terrain;      // W×H，生成产出
    public PlainSubState[] plainSub;     // W×H
    public List<Vector2Int> kingdomSpawns; // 王国出生点（0=玩家，1..N=AI 王国，2_1 生成）
    public List<SpawnDef>   threatSpawns;  // 敌人晚上刷点/威胁方向（2_1 写入、2_8 消费）
    // 资源点/裂隙/特殊点占位 → 2_1 重新定义（BuildingPlaceholder 2D 化）
    // 冻结遗留：isPlayerHome / isConquered / bigTerrain（删）
}

/// <summary>敌人威胁来源点（2D 360° 来袭的静态刷点位，2026-08-12 新增）。</summary>
public struct SpawnDef
{
    public Vector2Int coord;         // 刷点格坐标
    public Vector2 direction;        // 威胁来袭方向（360° 归一化，格空间归一化 §1.6）
    public int strength;             // 波次规模（2_8 细化）
    public Faction faction;          // 阵营（玩家王国 / AI 王国【预留】 / 怪物）
}
```
- `WorldState`：`maps` 壳保留（永远只有 1 张）、`activeMapId` 恒 0、`conqueredMapIds` 冻结
- 删除：`BigTerrain` 枚举、`MapZone` 枚举、`Region` 类（移出 GridTypes）

**验收**：`MapData` 可序列化（字段存在即可，存档归 2_11）；`SpawnDef` 可被 2_1 写入 / 2_8 消费。

### 步骤 5：GridSystem 稠密数组重写（核心）

**文件**：`Systems/Grid/GridSystem.cs`（重写，保留类名与单例）

**旧成员处置表**（逐项迁移）：
| 旧成员 | 处置 | 说明 |
|--------|------|------|
| `_cells: Dictionary<GridCoord, GridCell>` | **换稠密数组** | `GridCell[] _cells`（W×H），索引 `y*W+x`；GridCell 保留 class 承载单位列表（懒分配，R2） |
| `_unitCells: Dictionary<UnitController, GridCoord>` | 保留 | 语义不变（微格主表，D70） |
| `MapCellCount` | 换 `MapWidth / MapHeight` | 边界校验改矩形 |
| `WorldToCoord(Vector2)` | 重写 | 2D 平面换算 + 地图中心原点（§5.2）；**返回 `GridCoord?`（null=越界，D2 P0）**。2026-08-22 裁决(HH.3)：改等轴逆变换（§1.6），iso 契约代码待落 |
| `CoordToWorld(GridCoord)` | 重写 | 返回格中心；删除 `flyHeight`/`Baseline_y=-3` 特判。2026-08-22 裁决(HH.3)：改等轴嵌入（`wx=(i−j)*0.64`、`wy=(i+j)*0.32`），iso 契约代码待落 |
| `CellToRegionIndex(int cellX)` | 换 `CellToChunk(GridCoord)` | 返回 `Vector2Int` chunk 坐标（`coord / chunkSize`） |
| `CellToMidRegionIndex(int cellX)` | 换 `CellToMidChunk(GridCoord)` | `coord / midChunkSize` |
| `TryEnter(unit, coord)` | 保留签名 | 微格登记 + 堆叠上限检查 → 换格登记 → 跨 Chunk 检测 → 发 `EnemyEnteredChunkEvent` |
| `ExitCurrentCell / RemoveUnit / ClearAll` | 保留 | ClearAll 增加稠密数组复位 |
| `GetUnitsInCell / GetUnitsInCellByCategory / GetUnitCoord` | 保留 | L0 零改动迁移；新增 `FillUnitsInRect` 零 GC 版 |
| `IsOccupied / IsObstacle / GetOccupant / MarkOccupied / Free` | 保留 | 内部改数组索引；`MarkOccupied` 同步置 `BuildingBlocked` 位 |
| `MarkOccupiedFootprint(origin, cellWidth)` | **改签名** | `MarkOccupiedFootprint(GridCoord origin, int w, int h, Building)`；`FreeFootprint` 同步 |
| `IsInsideWall(Vector2)` | **移除** | 1D 语义作废；围合判定由 2_2 移除（D63），现消费方登记移交 2_7/2_8 |
| `GetTerrainAt / GetPlainSubStateAt` | 保留签名 | 内部从"region 查找"变 `_terrain[]` 直查，**O(1)** |
| `PopulateFromMap(MapData)` | 简化 | 单图初始化：读宽高建数组、灌地形层（2_1 后接入 features） |
| `OnDrawGizmos` 全部 | 重写 | 2D 网格/Chunk 边界/地形色块；5 区染色逻辑删除 |

**新分层存储布局（§5.3）**：
```
GridSystem（单例，沿用 Singleton<GridSystem>）
├─ int _w, _h                          // 地图尺寸（小区块数）
├─ TerrainType[]   _terrain            // W×H，值类型（实际存 byte 压缩）
├─ PlainSubState[] _plainSub           // W×H byte
├─ WalkFlags[]     _walkFlags          // W×H byte（N2：65KB @256²）
├─ Building[]      _occupants          // W×H 引用（footprint 每格同引用，256² ≈ 512KB）
├─ GridCell[]      _cells              // W×H，懒分配 null 起步（R2）；承载微格 footprint 列表
└─ Dictionary<UnitController, GridCoord> _unitSubCells   // 微格主表（D70，每单位 1 条）
```

**新坐标换算 API（§5.2）**：
```csharp
public GridCoord? WorldToCoord(Vector2 worldPos)   // 地图中心原点，分量换算，越界 null
// cx = Floor((pos.x + W*cellW/2) / cellW)，cy = Floor((pos.y + H*cellH/2) / cellH)
public Vector2 CoordToWorld(GridCoord coord)       // 格中心正交坐标，wy 补齐
// wx = (x+0.5)*cellW - W*cellW/2，wy = (y+0.5)*cellH - H*cellH/2
public bool IsInBounds(GridCoord coord)            // 矩形边界校验
```

> 2026-08-22 裁决（HH.3）：上段 `WorldToCoord`/`CoordToWorld` 为**当前正交实现**，仅作现状留档、**不先行改写**（避免代码仍正交而文档说 iso 的误导，见 §1.6 / §5.2 iso 契约）；世界坐标将统一为等轴嵌入（`wx=(i−j)*0.64`、`wy=(i+j)*0.32`，逆变换 §1.6），**GridSystem 迁移待落**。

**验收**：空场景初始化 256×256 网格 < 100ms，网格层内存 < 2MB；`WorldToCoord → CoordToWorld` 往返误差 < 0.001；地图中心 = 世界 (0,0)；地形写入/直查 10 万次无 GC。

### 步骤 6：微格（SubCell）API

**文件**：`Systems/Grid/GridSystem.cs`（新增）

> 微格 = **小区块边长 ÷ 4** → 每小区块 = **4×4 = 16 微格**（0.32×0.16 = 32×16px @PPU100，**非面积÷4**，D16）。NPC 底座 = 1 微格（0.32×0.16，D29）。可走性**按需推导**（热点缓存实现归 2_6），footprint 用**矩形列表**查询覆盖。

```csharp
public GridCoord? WorldToSubCoord(Vector2 worldPos)   // 世界坐标 → 最近微格（含中心原点偏移）
public Vector2 SubCoordToWorld(GridCoord sub)         // 微格 → 世界（格中心）
public bool IsSubWalkable(GridCoord sub)              // 微格可走 = 小区块可走 && 未被 footprint 覆盖
public GridCoord SubToCell(GridCoord sub)             // 微格 → 小区块（sub/4，layer 透传）
public GridCoord CellToSub(GridCoord cell, int sx, int sy) // 小区块 → 微格（cell*4+(sx,sy)）
```
- `WorldToSubCoord` 公式：`sx = Floor((pos.x + W*cellW*subDiv/2) / subW)`（`subW = cellW/subDiv`，y 分量同理），越界 null
- `IsSubWalkable` 查询逻辑：
```
IsSubWalkable(sub):
  小区块 = SubToCell(sub)
  if 小区块不可走: return false
  for 该小区块 footprint 列表中的每个建筑(矩形):
    if 建筑矩形覆盖微格(sub): return false
  return true
```

**验收**：16 微格/小区块正确（4×4）；`WorldToSubCoord` 吸附最近微格起点正确；`IsSubWalkable` 对 footprint 覆盖格返回 false。

### 步骤 7：单位层微格登记 + Chunk 事件改名

**文件**：`Systems/Grid/GridSystem.cs`（改动）+ `Core/GameEvents.cs`（改动）

**旧事件迁移**：
```csharp
// 旧：EnemyEnteredRegionEvent(RegionIndex int, UnitController Enemy) —— 删除
// 新：EnemyEnteredChunkEvent(Vector2Int ChunkCoord, UnitController Enemy)
public readonly struct EnemyEnteredChunkEvent
{
    public readonly Vector2Int ChunkCoord;
    public readonly UnitController Enemy;
    public EnemyEnteredChunkEvent(Vector2Int chunkCoord, UnitController enemy)
    { ChunkCoord = chunkCoord; Enemy = enemy; }
}
```
- 发布点：`TryEnter` 跨界检测改 **Chunk 比较**（旧为 region 比较）；订阅方适配归 2_7
- `MapGeneratedEvent` 保留（载荷 MapData 契约变化，发布方归 2_1）

**单位层（微格主表）API**：
```csharp
public bool TryEnter(UnitController, GridCoord subCoord);      // 微格登记 + 堆叠上限 + 跨Chunk事件
public void ExitCurrentCell / RemoveUnit / ClearAll;           // 沿用
public List<UnitController> GetUnitsInSubCell(GridCoord sub);  // 微格查询（每单位 1 条）
public List<UnitController> GetUnitsInCell(GridCoord cell);    // 小区块聚合（聚微格）
public int FillUnitsInRect(RectInt subRect, List<UnitController> buffer); // 新增：矩形零 GC 版
public GridCoord? GetUnitCoord(UnitController);                // 微格坐标（沿用语义）
```

**验收**：单位 TryEnter/Exit/查询行为与 1D 时代一致（回归）；`EnemyEnteredChunkEvent` 编译通过、发布点在 TryEnter；跨 Chunk 事件风暴节流归 2_7。

### 步骤 8：footprint 矩形化 + IsFootprintClear

**文件**：`Systems/Grid/GridSystem.cs`（改动）

**旧定义迁移**：
```csharp
// 旧：public void MarkOccupiedFootprint(GridCoord origin, int cellWidth, Building building)  // x 方向一维
// 新：
public void MarkOccupiedFootprint(GridCoord origin, int w, int h, Building building)
    // 矩形遍历置 _occupants + BuildingBlocked 位（isObstacle 时）
public void FreeFootprint(GridCoord origin, int w, int h)
    // 矩形释放 + 清 BuildingBlocked 位
public bool IsFootprintClear(GridCoord origin, int w, int h)
    // 新增原语（R6）：w×h 矩形内无阻挡/占用，2_2 摆放校验用
```

**验收**：2×2 建筑 footprint 覆盖 4 格、每格 `GetOccupant` 同引用；释放后 `IsOccupied` 全 false；`IsFootprintClear` 对半压水格返回 false。

### 步骤 9：IPathGrid 契约实现（2_6 硬契约）

**文件**：`Systems/Grid/GridSystem.cs`（实现接口）+ 新建接口所在文件

```csharp
/// <summary>寻路系统读取网格的最小契约（2_6 唯一依赖口）。坐标 = 微格坐标。</summary>
public interface IPathGrid
{
    int Width { get; }                 // 微格数宽（= 小区块宽 × 4）
    int Height { get; }                // 微格数高
    bool IsWalkable(GridCoord subCoord);          // 微格可走（§5.1 规则；跨格地形逐微格判定）
    float GetEnterCost(GridCoord subCoord);       // 地形移动代价（Plain=1.0，Hills=1.5 等，SO 可配；格单位，§1.6）
    bool IsDiagonalMoveAllowed(GridCoord from, GridCoord to);  // 防穿角：两正交邻微格均可走才允许斜走
}
```
- GridSystem 实现 `IPathGrid`；2_6 的寻路器只依赖接口不依赖具体类（sim 训练侧可另起纯 C# 实现）
- 地形代价表走 SO（新增 `TerrainCostConfig`，字段归 2_6 细化）；空代价表先用全 1.0
- 8 向邻居顺序固定（E/NE/N/NW/W/SW/S/SE）；斜走代价 √2 按格单位计（§1.6）
- 微格 ↔ 小区块换算：`微格 = (小区块x*4+subx, 小区块y*4+suby)`；`小区块 = (微格x/4, 微格y/4)`

**验收**：`IPathGrid` 可被独立程序集引用（不依赖 UnityEngine 之外的项目类型），sim 侧可另实现；`GetEnterCost` 默认全 1.0。

### 步骤 10：D2 全调用方 nullable 适配（P0 强制，一次完成）

**文件**：§2.7 总表全部 28 个消费文件（L0 仅重编译；L1 逐处改调用）

> 这是本片改动量最大的步骤。`WorldToCoord` 返回 `GridCoord?` 后，所有 `coord.x` / 传入 `TryEnter` 等用点的调用方需处理 null。

**适配规则**：
- L0（只用 `TryEnter/GetUnitsInCell/GetUnitCoord` 等语义不变接口）：重新编译即用，行为回归验证
- L1（用了坐标换算/footprint/地形查询）：改调用不改逻辑，`null` 越界时沿用旧 clamp 语义或提前返回
- L2（强 1D 假设：IsInsideWall/区域事件/移动执行）：只提供新原语，改造归 2_2~2_9（本片不实现）

**§2.7 消费文件归属总表（2026-08-13 重编号，全 28 文件）**：
| 文件 | 引用数 | 级别 | 归属 |
|------|-------|------|------|
| UnitController.cs | 32 | L1 | 2_3 |
| Building.cs | 26 | L1 | 2_2 |
| LODSystem.cs | 22 | L1/L2 | 2_4 |
| AIDebugSpawnController.cs | 15 | L1 | 2_7 |
| BuildingFactory.cs | 14 | L1 | 2_2 |
| ProjectileManager.cs | 13 | L1 | 2_5 |
| DamageSystem.cs | 11 | L1 | 2_5 |
| NPCBrain.cs | 9 | L1 | 2_7 |
| GroundEffectManager.cs | 8 | L1 | 2_5 |
| BuildController.cs | 7 | L1 | 2_2 |
| WanderAnchorPool.cs | 6 | L1 | 2_7 |
| FormationManager.cs | 6 | L2 | 2_8 |
| WorldManager.cs | 6 | L2 | 2_1 |
| PerceptionSystem.cs | 5 | L1 | 2_7 |
| TaskScheduler.cs | ≤4 | L1 | 2_8 |
| UnityWorldQueryAdapter.cs | 6 | L1 | 2_7 |
| WorldState.cs | 5 | L0/L1 | 本文档契约适配 + 2_11 |
| PopulationSystem.cs | ≤4 | L1 | 2_8 |
| VagrantCampSystem.cs | ≤4 | L1 | 2_8 |
| GameEvents.cs | ≤4 | L0 | 本文档（EnemyEnteredChunkEvent 改名） |
| Ports.cs | ≤4 | L0/L1 | 2_7 |
| AttentionSystem.cs | ≤4 | L1 | 2_7（轴距离→格单位距离 §1.6） |
| BuildingRegistry.cs | ≤4 | L1 | 2_2 |
| PlacementValidator.cs | ≤4 | L1 | 2_2 |
| CameraSetup.cs | ≤4 | L2 | 2_10 |
| RulerController.cs | ≤4 | L1 | 2_3 |
| GridTypes.cs | — | L0 | 本文档 |
| MapVisualizer.cs | — | L0/L2 | 2_10（数据契约本文档保证） |

**验收**：全项目编译通过；`WorldToCoord` 全调用方 nullable 签名适配完成；§1.2 核查清单四项模式全部清零。

### 步骤 11：Gizmos 2D 基础版

**文件**：`Systems/Grid/GridSystem.cs`（重写 OnDrawGizmos）

- 黄色细线：格边界（可关，只画摄像机视野内格子，256² 全画会卡）
- 粗线+底色：Chunk 边界
- 地形色块：每格小方块按 TerrainType 取色（沿用 `GetZoneColor` 思路改 `GetTerrainColor`）
- 红色覆盖：BuildingBlocked/Locked 格
- 金色标记：kingdomSpawns 出生点
- 编辑器下非 Play 模式也可预览（PopulateFromMap 后）

**旧定义迁移**：`GetZoneColor(MapZone)` → 删除（5 区染色逻辑删除）；新增 `GetTerrainColor(TerrainType)`。

**验收**：空场景生成 256² 后 Gizmos 清晰；视野外格子不绘制。

---

## 二、涉及文件清单

| 文件 | 改动 |
|------|------|
| `Systems/Grid/GridTypes.cs` | 改造：GridCoord 2D 化+layer+强哈希、WalkFlags 五位、GridCell 瘦身（terrain/occupant/isObstacle 字段下沉过渡属性标 Obsolete）；删除：BigTerrain、MapZone、Region |
| `Systems/Grid/GridSystem.cs` | 重写：稠密数组、坐标换算（nullable）、微格 API、footprint 矩形化、IPathGrid 实现、TryEnter 微格登记+Chunk 事件、Gizmos 2D |
| `Systems/Grid/MapVisualizer.cs` | 移交 2_10（本文档定契约）；现有程序化方案留作 2_10 调试底图 |
| `Data/GridConfig.cs` | 改造：cellSize 双分量、subCellDivisor、chunkSize/midChunkSize 改名、删 originX/flyHeight |
| `Data/MapSizeConfig.cs` | 改造：MapSizeEntry.width/height（128/256/384）、GetEnemyMapBase 语义 |
| `Systems/World/WorldState.cs` | 适配：MapData 2D 契约（删 regions/bigTerrain）、WorldState 单图冻结 |
| `Core/GameEvents.cs` | 改名：EnemyEnteredRegionEvent → EnemyEnteredChunkEvent |
| `Resources/Grid/GridConfig.asset` | 同步改字段 |
| `Resources/Grid/MapSizeConfig.asset` | 同步改 2D 档位 |
| §2.7 总表 28 个消费文件 | L0 重编译 / L1 逐处 nullable 适配（按归属认领） |

---

## 三、SO 配置

**GridConfig（改造后字段）**

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| cellSize | Vector2 | (1.28, 0.64) | 世界单位/格（逻辑小区块=1 Tile，素材对齐）；**双分量，距离公式见 §1.6，禁标量化** |
| subCellDivisor | int | 4 | 小区块 ÷ 4 = 微格（寻路粒度） |
| chunkSize | int | 16 | Chunk 边长（格） |
| midChunkSize | int | 4 | MidChunk 边长（格） |
| stackLimits | StackLimitConfig[] | 沿用 | 数值重审归 2_5 |
| drawGizmos | bool | true | 调试绘制总开关 |
| ~~originX / flyHeightThreshold / flyHeight~~ | — | — | 删除 |

**MapSizeConfig（改造后字段）**

| 字段 | 类型 | 说明 |
|------|------|------|
| sizes | MapSizeEntry[] | 每档 `{ size, width, height }`（推荐 128²/256²/384²，D1） |
| enemyByDifficulty | int[] | 语义变"同图 AI 王国数"（数值重审归 2_1/2_8） |

> 新增 `TerrainCostConfig`（地形代价表）为 2_6 细化项，本片暂不落地（IPathGrid 先用全 1.0 空代价表）。

---

## 四、冒烟验证 checklist（照单执行）

- [ ] 空场景初始化 256×256 网格 < 100ms，内存 < 2MB（网格层）
- [ ] `WorldToCoord → CoordToWorld` 往返误差 < 0.001；地图中心 = 世界 (0,0)
- [ ] 单位 TryEnter/Exit/查询行为与 1D 时代一致（回归）
- [ ] 地形写入/直查 10 万次无 GC 分配
- [ ] 2×2 footprint 覆盖 4 格，释放后清空；`IsFootprintClear` 半压水返回 false
- [ ] 微格吸附：`WorldToSubCoord` 落到最近微格起点；`IsSubWalkable` 对建筑覆盖格 false
- [ ] Gizmos：256² 只画视野内格子，Chunk 边界/地形色/出生点可见
- [ ] 全项目编译通过，`WorldToCoord` 全调用方 nullable 签名适配完成

---

## 五、验收标准

1. `GridCoord` 2D 化 + layer 预留 + 强哈希落地，§1.2 核查清单四项模式全部清零
2. `WalkFlags` 五位可走规则正确（TerrainWalkable && !Blocked/!Locked/!Water || Bridge）
3. `GridSystem` 稠密数组 + 坐标换算（nullable）+ 微格 API 全落地，N1~N4 达标
4. `MapData` 契约含 `threatSpawns`（SpawnDef），可被 2_1 写入 / 2_8 消费
5. `IPathGrid` 可被独立程序集引用，sim 侧可另实现
6. §2.7 总表 **28 文件**编译适配完成，`WorldToCoord` 全调用方 nullable 签名适配完成
7. `EnemyEnteredChunkEvent` 改名 + 发布点就绪（订阅方适配归 2_7）

---

## 六、风险与回滚

| 风险 | 对策 |
|------|------|
| R1 y 语义混淆长尾 bug（老代码把 coord.y 当层） | §1.2 四种搜索模式逐处核查 + 2_3~2_8 各认领文件回归测试兜底 |
| R2 稠密数组初始化 GC spike | GridCell 懒分配（null 起步，TryEnter/占用时才 new）；地形/walkFlags 值类型数组 |
| R3 单位列表 GC（GetUnitsInCell 每次 new List） | 高频方直接遍历 `cell.Units`；新接口 `FillUnitsInRect` 复用缓冲 |
| R5 原点政策变更破坏旧场景/存档坐标 | 存档 schema 迁移归 2_11；改造期旧档可显式作废 |
| R7 27 文件漏改某个 L1 消费方 | §2.7 总表即认领清单；每个子文档验收时 grep 复核 |
| R8 寻路接口预留不足/过度 | §5.4 只定最小契约（IsWalkable/GetCost），2_6 可扩不可改语义 |
| R9 超大地图（384²）Chunk 分区过粗 | chunkSize 走 SO 可调；不满足时 2_4 再细分 |
| 确定性漂移 | code review 禁 `UnityEngine.Random` 进生成管线 |
| 回滚 | GridSystem 保留类名/单例，git revert 单文件即可回 1D；`GridCoord` 结构体名保留避免改名风暴 |

> 本片完成即进入 `2_2_建筑与占格`（footprint 微格吸附 + 放置交互 + 桥）、`2_3_空间与移动`（PathFollower 接微格寻路）、`2_4_LOD区块划分`（Chunk 索引适配）。
