# HH.300 · `HH.294` 底层重构批 · **片 3（换算归一 ＋ 数组下移）** · 交付报告

> 执行端｜2026-09-16｜取号 **HH.300**（水位线 `HH.299` → `HH.300`）
> **依据**：`HH.294_片3开工提示词.md`（`D769` 派工）＋ `最高优先级文档/底层执行计划.md` §二 批 1（`1-A`／`1-E`）＋ `02_空间与粒度.md` §一／§2.3／§2.4／§五 ＋ `04_上层接入指南.md` §六 五条不许 ＋ `HH.294` 任务书 §二 片 3 ＋ `D768` §39.3（搭车 `R1`／`R2`）
> **口径声明（`L-35`）**：每条读数均写**口径来源**与**排除项**；禁用形容词（`L-30`）；**禁上限句**（`L-37`）——内存判据改为**逐值对照（B/格 × 格数，可复算）**。
> **施工 commit**：**`00782768`**（`HH.294 片3 换算归一+数组下移…＋搭车 R1/R2`）｜**未 push**｜21 files changed, **+825 / −216**
> ⚠️ **账本**：执行端**未改**策划端四档（`_任务队列.md`／`_当前快照.md`／`_编号登记.md`／`测试基线台账.md`）——应登记项见 §十。

---

## 〇、结论摘要（先给数字）

| 件 | 状态 | 逐条判据读数（摘要） |
|---|---|---|
| **3-A** 换算归一 | ✅ 落地 | 6 个换算函数**定义各仅 1 处**（grep 逐名实证）；⚠️「就地展开」算式片段 **17 → 0**（`* 0.5f * (grid\|g\|gs\|GridSystem)`）；`CoordToWorld` 命中 **68 → 52**（16 处改走内核）；行为等价：N=300 四点**不一致 0** |
| **3-B** 数组下移 | ✅ 落地 | 三派生数组域 **147,456 → 2,359,296**（×16）；全图 **2,359,296 子格**逐格一致**不一致 0**；同地块 `div²` 子格**非同值地块 0**；内存 **2.39 → 38.25 MiB（净增 35.86 MiB）** |
| **搭车 R1** | ✅ 落地 | `RebuildResourceIndex` 已移入 `GridSystem.Instance != null` 判定块内（改后原文见 §六） |
| **搭车 R2** | ✅ 落地 | `HH.293` 判据②「全图簇数<大区块数」**已撤下**，改判「`Mine nb = 0` 为达标」（改后原文见 §六） |
| 判据 4／7／8 | ✅ | 生成一图要素**逐值同 `HH.297`**；子格域 A* `Ready`/386 点；`GuardDeploymentSystem` 索引 **13,064 格／0.401 ms**（片1 基线 13,064／0.374 ms） |
| 编译／收尾 | ✅ | **0 `error CS`**；本片改动文件 **0 warning**；`isPlaying=False`（三态退 Play）；`AI.Core` 命中 **0** |

---

## 〇之二、双列对照总表（**改前 vs 改后**）

| 面 | **改前**（`00782768^`） | **改后** |
|---|---|---|
| 换算**算式**所在 | `GridSystem` 6 函数 ＋ `MapRenderService.GridToIso/IsoToCell/IsoDepth` 各一套 ＋ `FootprintCenterWorld` **4 处各写一套** ＋ 调用点**就地展开 17 处** | ⭐ **唯一内核**：`GridSystem.CellToWorldF`／`WorldToCellF`／`CellToWorld`／`WorldToCell`／`FootprintCenterWorld`；`MapRenderService` 三个函数**改一行转调**；就地展开 **0** |
| 换算**规则** | 无（40+ 处调用无判据） | `GridSystem.cs:145-173` 区块头**规则表**（三域 × 十种需求 → 用哪个）＋「⛔ 禁就地展开」反面教材注 |
| 派生数组**域** | `_walkFlags`／`_occupants`／`_cells` ＝ `W×H`（**地块级**，384² = 147,456） | ⭐ **小格子级** `SW×SH`（384² 大图 = **2,359,296**，面积 ×16） |
| `IPathGrid.Width/Height` | 已是小格子分辨率（`_w×div`）—— **与数组域不一致** | 与三数组域**恒等**（`Width/Height == _sw/_sh`，探针实证） |
| 占格写入粒度 | 1 地块 → 1 元素 | 1 地块 → **div² 子格**（＝`02` §2.3「建筑尺寸内部 ×16」） |
| 子格可走判定 | `IsSubWalkable` = `SubToCell` 后**按整地块近似** | ⭐ **精确到子格**（读该子格；`IsObstacleSub` 同粒度） |
| 新增子格读口 | 无 | `GetWalkFlagsSub`／`IsWalkableSub`／`GetOccupantSub`／`IsObstacleSub` |
| 三数组内存（384²） | **2.39 MiB**（片2 后口径；含两 int 数组的改前口径 = 3.52 MiB） | **38.25 MiB**（逐值复算与 GC 实测一致） |
| `WorldManager` 建图后 | `RebuildResourceIndex` 在 `null` 判定**块外** | **块内**（`R1`） |
| 探针判定行 | `HH.293` 判据② 打印 `不成立（相等⇒地板恐未拆）❌` | **该聚合判定行撤下**，改判「`Mine nb = 0`」 |

---

## 一、3-A · 换算归一

### 1.1 处置（**声明所选**）

- **① 定义收进一处**：新设**纯静态内核**（算式只此一份）：

```csharp
public static Vector2 CellToWorldF(float gx, float gy, Vector2 cellSize)   // 连续格号 → 世界
public static Vector2 CellToWorld(GridCoord g, Vector2 cellSize)           // 格号 → 世界（格点）
public static Vector2 WorldToCellF(Vector2 pos, Vector2 cellSize)          // 世界 → 连续格号
public static GridCoord WorldToCell(Vector2 pos, Vector2 cellSize)         // 世界 → 格号（floor）
public static Vector3 FootprintCenterWorld(GridCoord o, Vector2Int fp, Vector2 cellSize)
public static Vector3 FootprintCenterWorld(GridCoord o, Vector2Int fp, Vector3 fallback)  // 便利口（免各调用点判空）
```

- **② 规则写进代码注释**：`GridSystem.cs` 换算区块头 4 段——**三域**（世界／地块格号／小格子格号）＋**规则表**（十种需求 → 用哪个）＋**⛔ 禁就地展开**（点名两个反面教材）＋静态内核的适用场景。
- **③ 调用点按规则改过去**：见 §1.3／§1.4。
- ⛔ **未做**：把换算逻辑复制到调用方（本片的目标正是清除它）。

### 1.2 判据 ① · **定义唯一性**（grep 证据·逐名）

口径：`git grep -n -E "(public|private|internal|protected|static).*\b<名字>\s*\(" -- '*.cs'`，逐行人工判读「定义行 / 调用行」。

| 函数名 | 定义处（实读 `file:line`） | 定义数 |
|---|---|---|
| `WorldToCoord` | `GridSystem.cs:216` | **1** |
| `WorldToSubCoord` | `GridSystem.cs:228` | **1** |
| `SubToCell` | `GridSystem.cs:240` | **1** |
| `CellToSub` | `GridSystem.cs:247` | **1** |
| `CoordToWorld` | `GridSystem.cs:225` | **1** |
| `SubCoordToWorld` | `GridSystem.cs:237` | **1** |
| `CellToWorldF` | `GridSystem.cs:177` | 1 |
| `WorldToCellF` | `GridSystem.cs:184` | 1 |
| `CellToWorld` | `GridSystem.cs:181`（内核） | 1 |
| `WorldToCell` | `GridSystem.cs:192`（内核） | 1 |
| `FootprintCenterWorld` | `GridSystem.cs:200`／`:206`（**同处两个重载**） | 1 处／2 重载 |
| `GridToIso` | `MapRenderService.cs:91`（**一行转调**，无算式） | 1（转调口） |
| `IsoToCell` | `MapRenderService.cs:98`（**一行转调**） | 1（转调口） |
| `IsoDepth` | `MapRenderService.cs:102`（转调 `GridToIso`） | 1（转调口） |

⚠️ **排除项（`L-39`：零命中/命中只作线索）**：
- `CellToWorld` 另有 1 个**同名别名**：`VagrantCampSystem.cs:434`（`static Vector3 CellToWorld(Vector2Int)`）——其**算式已删除**，现为**一行转调** `GridSystem.FootprintCenterWorld(..., fallback)`。⇒ 「`CellToWorld` 名字 2 处，**算式 1 处**」，如实列明。
- grep 命中 `Valley2_17_Smoke_13.cs:158` 的 `CoordToWorld` 是**调用行**（`static Vector2 MidToWorld(...) => grid.CoordToWorld(...)`），**非定义**。

### 1.3 判据 ② · **调用点分布对照**（改前 → 改后）

口径：`git grep -n -E "\b<名字>\s*\(" -- '*.cs'` 命中**行数**（含定义行/注释行/探针），`HEAD` = 片3 前。

| 名字 | HEAD 命中行 | 现命中行 | 差值 | **去向归因** |
|---|---|---|---|---|
| `WorldToCoord` | 30 | 30 | 0 | 未动（原调用即合规：世界→地块，均需越界 null 语义） |
| `WorldToSubCoord` | 20 | 20 | 0 | 未动（原调用即合规：世界→小格子） |
| `SubToCell` | 12 | **11** | **−1** | `GridSystem.IsSubWalkable` 旧实现的 `SubToCell(sub)` 随「精确到子格」删除 |
| `CellToSub` | 7 | **16** | **+9** | 新增合规调用点：`PlacementScorer` **3**（原 `.x*div` 就地展开）／`SpawnPosSnapper` **1**（原 `.x*div+div/2`）／探针 **5**（`Smoke_2_22P0` 2・`HH140` 1・`HH144` 2，原均就地展开） |
| `CoordToWorld` | 68 | **52** | **−16** | **16 处就地展开改走内核**（明细见 §1.4） |
| `SubCoordToWorld` | 5 | 5 | 0 | 未动 |
| **合计** | **142** | **134** | **−8** | （另：`FootprintCenterWorld` **7 → 22**、`GridToIso` **17 → 18**、`CellToWorld` **5 → 9**，均为「转调口/便利口」的新增） |
| 引用**文件数**（6 名） | 61 | **59** | −2 | 两文件经收口后不再直接引用换算族 |

⚠️ **口径差异声明**：`底层执行计划` §二 批 1 记「6 个换算函数调用 **95 处 / 42 文件**」（`D764` 建立时实测）；本表按**命中行数**重数，得 `HEAD` **142 行 / 61 文件**（差异＝含定义行、注释行、`Assets/Editor/Smoke/**` 探针 与 `AI.Core` 注释行）。**两口径不混用**，请以本表口径复核。

### 1.4 ⛔「就地展开」**清零**证据（本片治的病）

| 检索串 | **HEAD** | **现在** |
|---|---|---|
| `\* 0\.5f \* (grid\|g\|gs\|GridSystem)`（footprint 中心点算式片段） | **17** | ⭐ **0** |
| `\.x \* div` ／ `\.y \* div`（地块→子格就地乘） | **10** | **1**（唯一残留 ＝ `GridSystem.cs:250`＝`CellToSub` **自身函数体**，正当） |

**16 处 `CoordToWorld` 就地展开的逐点去向**（`HEAD` 位置 → 改后）：

| # | 文件 | HEAD 位置 | 改后 |
|---|---|---|---|
| 1 | `BuildingFactory.cs` | `:151` 本地 `FootprintCenterWorld` **定义体** | **定义整段删除**；3 处调用点 `:95`／`:115`／`:144` → `GridSystem.FootprintCenterWorld(coord, fp, Vector3.zero)` |
| 2 | 同上 | `:359-360` 内联偏移 | → 同一内核（内核内一次算完；无网格兜底常量 2.26/1.13 逐字保留） |
| 3-5 | `KingdomFoundry.cs` | `:278` 本地**定义体**；`:479`／`:508` 内联 | **定义整段删除**；`PlaceAt:273`／`PlaceCampCastle`／`PlaceCampWell`／`PlaceAt`（第一代）共 **4 处** → 内核（兜底 `new Vector3(coord.x, coord.y, 0f)` 逐字保留） |
| 6-7 | `BuildController.cs` | `:178`（`GhostWorldPos`）／`:348`（`BuildingWorldPos`） | → 内核（兜底 `MapRenderService.DefaultCellSize`＝旧式 (1.28,0.64) 逐字保留） |
| 8-9 | `VagrantCampSystem.cs` | `:218` 内联／`:434` `CellToWorld` 定义体 | 定义体**算式删除**→转调；`:218` → 内核（兜底 `new Vector3(coord.x, coord.y, 0f)`） |
| 10-16 | 探针 7 处 | `TestFixtureApi:204`／`HH111:302`／`HH109:370`／`HH107:364`／`HH291:580`／`HH291:607`／`OB12:341` | → 内核（兜底与改前同） |

### 1.5 行为等价实证（探针 · N=300）

口径来源：`Logs/hh294_slice3_probe.log`（22:14:38 段）·正门 `EnterTestRun`·384²·seed 21107。

| 检查 | 读数 |
|---|---|
| ① `grid.CoordToWorld(c) == MapRenderService.GridToIso(c)`（逐位 `!=` 计数） | **不一致 0** |
| ② `grid.WorldToCoord(w)` 与 `MapRenderService.IsoToCell(w)` 结果 | **不一致 0** |
| ③ `CellToSub`／`SubToCell`／`SubCoordToWorld`／`WorldToSubCoord` 往返 | **异常 0** |
| ④ `FootprintCenterWorld(c,(1,1),cs) == 格点` ∧ `(c,(2,2),cs) == 格点+(0.5,0.5)·cellSize` | **异常 0** |

⇒ 两套并行实现**合一后逐位相同**（`MapRenderService` 三个函数已成一行转调）。

---

## 二、3-B · 数组下移（可走／占格 → 小格子）

### 2.1 双列对照（存储与读写口）

| 项 | 改前 | 改后 |
|---|---|---|
| `_walkFlags`／`_occupants`／`_cells` 长度 | `W×H` = 147,456 | `SW×SH` = **2,359,296**（探针实读三数组 `.Length` 全等于该值） |
| 索引 | `ToIndex(x,y)=y*_w+x` | `ToSubIndex(sx,sy)=sy*_sw+sx`；**地块级代表位** `CellBaseIndex(x,y)=(y*div)*_sw+x*div` |
| 地块级读口 | 直读元素 | **读＝该地块首子格**（签名不变 ⇒ 消费端零改动） |
| 地块级写口 | 写 1 元素 | **整块 div² 子格同写** |
| `_cells` | 每占地块 1 个 `GridCell` 对象 | 数组 ×16，但**壳对象仍按地块首子格分配**（对象数与改前一致，不因 ×16 多分配小对象） |
| `RefreshCellFromFeature`／`PopulateFromMap` | 每地块 1 次写 | 每地块 div² 次写（**频次不变**：1 次/局 ／ 事件驱动） |

### 2.2 判据 ③ · **逐格一致**（**全图扫描**，非抽样）

| 检查 | 读数 | 判 |
|---|---|---|
| 扫描规模 | **2,359,296 子格**（全图） | — |
| ⭐ 派生态（`地表物 ⊕ 占格阻挡位`）vs `_walkFlags`：**掩 `Bridge` 位**（主口径） | **不一致 0** | ✅ |
| 同一口径：**不掩任何位** | **不一致 0** | ✅ |
| `Bridge` 位子格数／`Locked` 位子格数 | **0 / 0**（本局无桥、无锁格） | — |
| **同一地块 `div²` 子格非同值**的地块数 | **0**（＝地块级代表位成立的前提，全图 147,456 地块核过） | ✅ |
| 派生态取值分布 | `TerrainWalkable=2,078,144`／`Water=64,832`／`None=216,176`／`TerrainWalkable\|BuildingBlocked=144` | — |

⚠️ **排除项 1**：`Bridge` 位由 `SetBridge` 独立写入（不属「地表物＋占格」派生式）⇒ 主口径为「掩 Bridge」；本局两口径同值（桥 0）。
⚠️ **排除项 2**：未做「精确子格**部分**占格」用例 —— 当前全库写入方皆**地块对齐**（地表物按地块／footprint 按地块／桥按地块），无此状态；该状态出现于片 4「落位到小格子」之后。

### 2.3 占格整块写取证（＋1 项观察）

| 检查 | 读数 |
|---|---|
| 抽样（`BuildingRegistry.All` 前 60 座） | 有登记 **36** ／ 全覆盖（footprint 全 `div²` 子格同引用）**36/36** ／ 部分命中 **0** ／ 全未命中 **24** |
| 覆盖判据（限定「网格上有登记」集合） | ✅ **36/36**（＝「建筑尺寸内部 ×16」占格已落到小格子） |

⚠️ **观察项（列报·**非**本片引入）**：24 座建筑在该图**网格上零登记**；诊断样本 `castle(359,20) fp3×3`／`House(357,22)`／`farm(361,22)`／`Well(361,18)`／`mine(357,18)`（均 `def.id` 实读），其特征为：footprint 内**非空子格 = 0** 且**地块级 `GetOccupant(coord)` 同为 `null`**。⇒ 与「数组下移」**无因果**（本片只改**存储地址/写入粒度**，不改写入路径；地块级读口与改前同式）。**列报另查，本片未处置**。

### 2.4 判据 ⑤ · **内存＝能力句 ＋ 逐值对照**（⛔ 非上限句）

**能力句**：384² 大图（小格子 1536² = **2,359,296** 格）下，`GridSystem` 三个派生数组的占用**可逐值复算**＝下表「C」列。

口径来源：`Logs/hh294_slice3_probe.log`（在线数组 `.Length` 反射实读 ＋ `GC.GetTotalMemory` 差 3 次取最小交叉核对）。

| 数组 | **B/格** | × 格数（2,359,296） | ＝占用 | 地块级同口径（×147,456） |
|---|---|---|---|---|
| `_walkFlags`（`WalkFlags : byte`） | **1** | × 2,359,296 | **2,359,296 B** | 147,456 B |
| `_occupants`（`IGridOccupant[]` 引用） | **8** | × 2,359,296 | **18,874,368 B** | 1,179,648 B |
| `_cells`（`GridCell[]` 引用） | **8** | × 2,359,296 | **18,874,368 B** | 1,179,648 B |
| **三数组合计** | — | — | **40,108,032 B = 38.25 MiB** | 2,506,752 B = 2.39 MiB |

**同口径并置（让「完全没做」与「做满」可区分）**：

| 口径 | 组成 | 读数 |
|---|---|---|
| **A** 改前 5 数组（**地块级** 147,456 格） | 可走(1B)＋占格(8B)＋壳(8B)＋地形 int(4B)＋子状态 int(4B) | **3,691,776 B = 3.52 MiB** |
| **B** 片2 后 3 数组（**地块级**） | 可走＋占格＋壳 | **2,506,752 B = 2.39 MiB** |
| **C** 本片后 3 数组（**小格子级**） | 可走(1B)＋占格(8B)＋壳(8B) | ⭐ **40,108,032 B = 38.25 MiB** |

⇒ **本片净增量（B→C）= +35.86 MiB**；**对改前口径（A→C）= +34.73 MiB**（与片 1-B 实测 +34.73 MB 逐值吻合）。

**GC 实分配交叉核对（`MinBytes`·3 次取最小）**：`new WalkFlags[n]` **2,363,392 B**／`new IGridOccupant[n]` **18,878,464 B**／`new GridCell[n]` **18,878,464 B** ⇒ 合计 **38.26 MiB**。
逐值差 = **4,096 B ×3**（口径：`GC.GetTotalMemory` 以 **4 KiB 页**计量 ⇒ 差值应落在 `[0,4096]`）⇒ 逐值复算与实测**相符**。

⚠️ **排除项**：`_cells` 仅计**引用数组本身**（`GridCell` **对象**按地块首子格懒分配，数量与改前同）⇒ 上表非「含对象的全量占用」。

### 2.5 判据 ④ · **生成一张图 ⇒ 不报错、能走通**

| 检查 | 读数 | 对照 |
|---|---|---|
| 地图要素（384²·seed 21107·diff2） | 总格 **147,456**｜`Plain=109,090`｜`Tree=5,178`｜`Mine=3,760`｜`山=13,511`｜`水=4,052`｜`一次性=11,865`｜`climateZones=147,456` | ⭐ **与 `HH.297` §四 逐值相同**（零退化） |
| 子格域 flood-fill（1536²=2,359,296 节点，源＝spawn0） | 可达 **2,076,081** 子格／单次 **154.10 ms** | — |
| 走通：spawn0 → 其余 4 出生点 | `[1]=True [2]=True [3]=True [4]=True` | ✅ |
| ⭐ 生产子格域 A*（`IPathGrid` → `IsSubWalkable`） | `status=Ready`／`waypoints=386`／`reachedExactGoal=True`／**78.5 ms** | ✅ 能走通 |
| 编译 | **0 `error CS`**（`read_console(types=[error])`） | ✅ |

---

## 三、判据 ⑥ · **无每帧全图遍历**（逐点核过·清单）

口径：`git grep` 全库数组访问 ＋ `GridSystem` 内数组迭代点逐行核（三数组为 `private`，全库**无**外部直接访问）。

| # | 遍历点 | 遍历对象 | **触发频率** | 本片后 |
|---|---|---|---|---|
| 1 | `GridSystem.PopulateFromMap` | features → `_walkFlags` 全图 | **1 次/局**（建局/读档） | 108 行内层 div² 写（**频率不变**；单次量 ×16，片1 实测该样式 4.15 ms@1536²） |
| 2 | `GridSystem.Initialize` | 三数组分配（2,359,296×3） | **1 次/局** | 新增：`_sw/_sh` 与三数组按小格子分配 |
| 3 | `GridSystem.RefreshCellFromFeature` | 单地块 div² | 事件驱动（资源点数据覆盖） | 地块级→div²（**频率不变**） |
| 4 | `MarkOccupied`/`Free`/`MarkOccupiedFootprint`/`FreeFootprint`/`SetBridge` | 单/多地块 div² | 事件驱动（放置/拆除/桥） | 地块级→div²（**频率不变**） |
| 5 | `MapGenRules` 生成管线 | features＋辅助数组 | **1 次/局**（生成期） | **未改** |
| 6 | `MapValidator.ValidateConnectivity`→`IsReachable`／`CarveCorridor` | `features`（`bool[]`／`int[]`＋BFS） | **2 次/局** ＋ 偶发 | **未改**（域＝地块级 `features`） |
| 7 | `MapRenderService.RenderMap` 全量分支 | features → Tilemap | **仅 `chunkSize=0` 调试态** | **未改** |
| 8 | `MapRenderService.Update`→`UpdateViewport` | 仅相机跨 chunk 时 24×24 格 | **每帧调用但 early-out**（`_lastCamChunk` 比对） | **未改** |
| 9 | `MapVisualizer.Visualize` | 全图 → Texture2D | **编辑器预览**（非 Play） | **未改** |
| 10 | `ResourceRespawnSystem.Update` | 仅**重生列表** | 每帧 O(列表) | **未改** |
| **11** | ⭐ **`GuardDeploymentSystem.FindNearestResourceNode`**（片 1 漏列·补正 `P1` 已闭） | `map.features` → **索引条目** | ⭐ **每次玩家右键派兵**（`SelectionController:270` ＋ `:279`→`:120` ⇒ 一次右键**两遍**） | **已索引化**（`D768` 销号）；本片**未改变**（见 §五） |

**答复**：**无每帧全图遍历**；本片**未引入**任何每帧全图/全数组扫（#8 仍 early-out）。**三数组为 `private`** ⇒ 除 `GridSystem` 自身外**全库零访问**。

---

## 四、判据 ⑧ · BFS flood-fill 耗时与触发频率（与片 1 基线对照）

| 口径 | 片 1 基线 | 本片实测 | 说明 |
|---|---|---|---|
| **生产** BFS（`MapValidator.IsReachable`·**地块级 features**·384²·源=spawn0 靶=spawn1·15 次中位） | 4.73 ms（384² 地块级） | **中位 3.197 ms ／ min 3.093 ms ／ 可达=True** | 域＝`features`（**本片未改**）⇒ 频率与耗时**不变**；中位差异属「靶点不同（spawn1 vs 角落）＋早退」 |
| 触发频率（代码审计） | 2 次/局＋偶发 | **2 次/局 ＋ `CarveCorridor` 偶发** | 无新增触发 |
| 子格域 flood-fill（**1536²**·新基数，本片新增读数） | 56.08 ms（片 1 探针同尺寸假设态） | **154.10 ms**（源＝spawn0 全图 flood·2,359,296 节点） | ⚠️ **生产未走此路径**；仅作「若将来寻路全图扫下移」的观测底座 |

⚠️ **单列报出（提示词要求）**：子格域 flood-fill **比片 1 假设值慢 2.75×**（154.10 vs 56.08 ms）。**但触发频率未上升**（生产 BFS 仍在地块级 `features`）⇒ **不构成新风险**，列为观测项。

---

## 五、判据 ⑦ · C1 点确认（`GuardDeploymentSystem` 不随本片变化）

| 项 | 片 1 补正基线 | 本片实测 | 差 |
|---|---|---|---|
| 索引条目数 `IndexedResourceCells` | **13,064** | **13,064** | **0** |
| 单次 `FindNearestResourceNode`（15 次中位） | 0.374 ms | **0.401 ms**（min 0.384 ms） | +0.027 ms（计时噪声量级） |
| 遍历格数 | ＝索引条目数（与位置无关） | 同 | — |

**口径**：索引为**地块级**（随 `MapData.features` 的 `W×H`）⇒ **不随三数组下移 ×16**（提示词「取甲」口径）；取点有效性：返回 `(x=3, y=7, layer=0)`。
⚠️ **复核项保留（不做）**：若将来 `03` 格表契约落地时把 `features` **本身**下移 ⇒ 该点转乙（线性外推 ≈5.98 ms／一次右键 ≈11.97 ms ⇒ **须重估**）；⛔ **有界半径近似＝不做**。

---

## 六、搭车微修（`D768` §39.3）

### R1 · `RebuildResourceIndex` 移入判定块内（`WorldManager.cs:124-133` 改后原文）

```csharp
        if (GridSystem.Instance != null)
        {
            GridSystem.Instance.PopulateFromMap(playerMap);
            // 【HH.294 补正 P1】守卫资源索引预建：装载期 1 次全图 features 扫（1 次/图），
            // 换掉「每次玩家右键派兵全图扫」（SelectionController:270 + DeployGuard:120 各一次）。
            // 【HH.294 片3 R1】⭐ 必须**在 `GridSystem.Instance != null` 判定块内**：原在块外调用 ⇒
            //   `grid == null` 时 `RebuildResourceIndex` 内 `CoordToWorld` 取不到 config ⇒ 索引世界坐标
            //   全为 `Vector2.zero` 且 `_idxBuilt = true` ⇒ 懒重建兜底失效 ⇒ 恒返格号最小的资源点（静默错答）。
            GuardDeploymentSystem.RebuildResourceIndex(playerMap);
        }
```

⇒ **所选＝「移至判定块内」**（提示词两选项之一）；`RebuildResourceIndex` 内部逻辑**未改**（未加早返回）。

### R2 · 探针判定行撤下（`Valley_HH294_HH293Probe.cs:81-84` 改后原文）

```csharp
            sb.AppendLine("  ⇒ B3 判据①「存在 0 簇区块」=" + (zeroChunks >= 1 ? "成立 ✅" : "不成立 ❌")
                          + " ｜ 达标口径（**D768 §39.3**：原判据②「全图簇数 < 大区块数」已判为**形式错·结构性不可达**"
                          + "〔实盘 `round(E/4) ≥ 1` 恒成立 ⇒ 全图簇数恒 > 大区块数〕，**已撤下、不再落盘**）："
                          + "「Mine nb = 0」=" + (nbMine == 0 ? "达标 ✅" : "不达标 ❌"));
```

＋ 文件头注释同步（原「判据②」描述改为「达标口径「Mine nb = 0」」＋ 撤下理由）。
⇒ **所选＝「改为 `nb=0` 为达标」**（提示词两选项之一）。

---

## 七、编译与收尾

| 项 | 读数 | 口径来源 |
|---|---|---|
| 编译 | **0 `error CS`** | `read_console(types=[error])` → `[]` |
| 非 C# 控制台条目 | **1 条**：`240 node options failed to load and were skipped.` | 首次读（**重编前**）即在场（ShaderGraph 节点库装载消息）⇒ **非本片编译产物** |
| warning | 本片**改动/新增文件 0 条**（实测 9 条全在未触碰文件：`ArtImportPipeline:267`／`Valley2_17_Smoke_5:91`／`Valley2_21A_Smoke:202`／`Valley2_17_Smoke_P0:225`／`Valley_HH128_Probe:75`／`Valley_HH80_Run:301`／`Valley_HH264_AnimProbe:182`／`Valley2_20_Smoke_Race:701,875`） | `read_console(types=[warning])`（重编后控制台自清 ⇒ 该 9 条＝最新一次编译全量） |
| ⚠️ 施工中自查修正 | 初次编译暴露 **2 条本片新增 warning**（`Valley_HH294_Slice3Probe` 两处 `IGridOccupant == Building` 引用比较 `CS0252`）⇒ 已改 `ReferenceEquals` 并复编清零 | `read_console` 前后对照 |
| 退 Play（`L-32`） | `Application.isPlaying=False`（探针收尾 `ExitTestRun ＋ QuitSmoke`） | `execute_code` 实读 ＋ 探针日志末行 |
| `AI.Core` | 目录命中 **0**（本片改动文件全在 `Systems/Grid|Rendering|Building|Kingdom|World|AI/KingdomBrain|Editor/Smoke`） | `git show --stat 00782768` 逐文件核 |
| 行尾 | 本片 21 个提交文件**全 LF**（无 CRLF／混合） | git 提交告警仅为 `core.autocrlf` 归一提示 |

---

## 八、证据索引

| 类型 | 路径 | 说明 |
|---|---|---|
| 原始读数 | `Valley Rampart/Logs/hh294_slice3_probe.log` | **本片主证据**（22:14:38·384²·seed 21107·正门 `EnterTestRun`·`PASS=13 FAIL=0`） |
| 探针 | `Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice3Probe.cs`（新） | ⑤内存／③逐格／④走通／⑧BFS／占格／①换算回归／⑦Guard 索引 |
| 探针 | `Valley Rampart/Assets/Editor/Smoke/Valley_HH294_HH293Probe.cs`（改） | `R2` 判定行撤下 |
| git | `git show --stat 00782768` | 21 files changed, **+825 / −216** |
| git | `git grep -n -E "\b<6 名>\s*\(" HEAD` vs 工作区 | §1.3 调用点分布对照 |
| 计数 | `git grep -c -E '\* 0\.5f \* (grid\|g\|gs\|GridSystem)'` | 17（HEAD）→ **0** |
| 探针（前批·复用） | `Logs/hh294_p1_probe_v2.log`／`hh294_grain_probe.log` | §五 ／ §2.4 对照基线 |

---

## 九、未完成项（**显式列出**·不打包）

1. **「就地展开」在 `Assets/Editor/Smoke/**` 之外的第三方面（`美术/pixel-forge`）未核**（不属仓库 `Assets/**`，且红线要求不碰）。
2. **未做「精确子格部分占格」用例**（当前无此写入方；属片 4「落位到小格子」的范围）。
3. **子格域 flood-fill 154.10 ms 未做优化**（生产未走该路径 ⇒ 按提示词「不自行加节流糊过去」**未处置**，列为观测项）。
4. **占格取证发现 24/60 座建筑在其图上网格零登记**（诊断 5 样本已附）——**非本片引入**（地块级读口同为 `null`），**本片未处置，列报另查**。
5. **未做真机／构建版读数**（与 `F-10` 同挂账口径；本片读数全取编辑器 Mono）。
6. **`03` 格表契约（一行一小格子）未落地** ⇒ 地块级读口「代表位＝首子格」的**退化前提**仍在：一旦出现子格级部分占格写入方，地块级读口将失真（**片 4 必须同步迁移**）——本片已在 `GridSystem` 类头写明。
7. **延续挂账（本片未动）**：`HH.297` §六-1 `ResourceGenConfig` 孤儿资产追认／§六-4 `MapCellCount` 新死接口／§六-5 `Resources/Debug/Maps/map_0_seed12345.json` 1D 孤儿数据／`F-10` 判据②（需 Profiler·构建版）／判据④-乙（派工打散）／`MapValidator` 与 `_Game/Art/Ground/*.asset` 等并行会话未提交件。

---

## 十、应登记项（账本·**执行端未落账**）

1. `_编号登记.md` 水位线：`HH.299` → **`HH.300`**（新增 `HH.300` ＝ 本报告）。
2. `_编号登记.md` 在途区 `HH.294` 片 3 行：`🟢 已派工·在飞` → **`🟡 已交付待验收（3-A 换算归一 ＋ 3-B 数组下移 ＋ R1/R2）`**。
3. `_任务队列.md` `HH.294` 批行：片 3 判据补注「**换算定义各 1 处（grep）+ 就地展开 17→0**」「**三数组 147,456→2,359,296（×16）· 全图逐格一致 0 不一致**」「**内存 2.39→38.25 MiB（净增 +35.86 MiB）**」。
4. `_当前快照.md` 在飞表 `0k` 行：片 3 → **「已交付·待验收」**；水位线行／最后更新行同步。
5. `测试基线台账.md`：追加「片 3 · 换算归一 ＋ 数组下移」读数段（§1.3 调用点分布／§2.2 逐格一致 0／§2.4 逐值内存表／§四 BFS 对照／§五 Guard 索引 13,064）。
6. `_策划教训库.md`：`L-15`＋1 实例（「就地展开」全库清点：17→0，含探针面）；`L-02`＋1 实例（子格域 flood-fill 观测值 154.10 ms 已落盘可复核）；`L-37`＋1 实例（内存判据改写为逐值对照表的示范）。
7. `_策划教训库.md`（新增观察）：占格取证发现「24 座建筑网格零登记」⇒ 建议作为**独立观察项**建档（非本片因果）。

---

## 十一、请裁项

1. **子格域 flood-fill 观测值 154.10 ms**（片 1 假设 56.08 ms 的 **2.75×**）：生产当前**未走**此路径（`MapValidator` 在地块级）⇒ 本片**未优化**。请裁「是否另立观测项／是否并入片 4 一并看」。
2. **「24 座建筑网格零登记」观察项**（§2.3）：是否需要另批（`KingdomFoundry` 预置链／`BuildingRegistry` 残留）定位？
3. **`CellToWorld` 同名别名保留**（`VagrantCampSystem.cs:434`，一行转调／无算式）：请裁「是否认可保留别名」或要求改名消除同名。
4. **片 4 前置提醒**：地块级读口「代表位＝首子格」在片 4「落位到小格子」后必然要迁移 ⇒ 请裁「片 4 是否**强制**同步迁移全部地块级读写口」（否则会出现子格级部分占格而地块级读口失真的窗口）。
