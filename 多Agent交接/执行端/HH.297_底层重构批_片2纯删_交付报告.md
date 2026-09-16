# HH.297 · 底层重构批 · **片 2 纯删** 交付报告

> 执行端｜2026-09-16｜依据 `HH.294` 任务书（D764）§二 片 2 ＋ `底层执行计划` 批 1-B/1-C/1-F｜取号 **HH.297**
> commit：**`919580e3`**（片 2：24 files ＋573／−151）｜**未 push**
> 证据：`Valley Rampart/Logs/hh294_slice2_probe.log`（正门实机 8/8 PASS）｜编译 `read_console` 零 error／零 warning

---

## 〇、双列对照（改前 vs 改后）

| 面 | 改前 | 改后 |
|---|---|---|
| `TerrainType` 枚举 | 11 项（`GridTypes.cs:66`） | **已删**（留迁移注释块） |
| `PlainSubState` 枚举 | 2 项（`GridTypes.cs:82`） | **已删** |
| `GridSystem._terrain` | `TerrainType[n]` 派生数组 | **已删** |
| `GridSystem._plainSub` | `PlainSubState[n]` 派生数组 | **已删** |
| 地块属性读口 | `GetTerrainAt(c)` → `TerrainType` | **`GetFeatureAt(c)` → `FeatureType`**（直读 `MapData.features` 同引用） |
| 派生写口 | `SetTerrain(c,t,sub)` | **已删**（派生只余 `RefreshCellFromFeature(c,f)` → walkFlags） |
| `GridCell.terrain` | `[Obsolete]` 属性 → `GetTerrainAt` | **已删** |
| 死接口 | `IsOccupied(c)`（零调用）／`GetPlainSubStateAt(c)`（零调用） | **两者均删**（见 §三 声明） |
| `BuildingDef.allowedTerrain` | `TerrainType[]` | **`FeatureType[]`** |
| `ResourceCountEntry` 键 | `terrain`(TerrainType)＋`plainSubState` | **`feature`(FeatureType)** |
| 引用规模 | 任务书称 26 处/4 文件 | 实读 **30 行命中**（含注释/资产），代码位 **约 24 处 / 5 文件**（多出 `Valley2_17_Smoke_12/13` 反射取 `_terrain` 的 3 处 ＋ `Valley2_20B_Smoke_M7` 的 `IsOccupied` 1 处） |

---

## 一、片 2-A · `TerrainType` 整层删（含 3 处消费端改读地表物）

| 消费端 | 改法 |
|---|---|
| `BuildingDef.allowedTerrain` | 类型 `TerrainType[]` → **`FeatureType[]`**（字段名保留，Tooltip 更新说明） |
| `PlacementValidator.ValidatePlacement` | `grid.GetTerrainAt(coord)` → **`grid.GetFeatureAt(coord)`**，比较对象同为地表物；`PlacementFailReason.Terrain` 保留 |
| `ResourceGenConfig.GetProducerCount/GetPickupCount` | 参数 `(TerrainType, PlainSubState)` → **`(FeatureType)`**；`ResourceCountEntry` 两键合一为 `feature` |

⚠️ **诚实标注（新发现死字段）**：实读 `GetProducerCount`／`GetPickupCount`／`baseCounts` **全库零外部调用**（仅自身定义），且 `ResourceGenConfig` 类 **零 `Resources.Load` 消费**、资产 guid **零引用** ⇒ 该件整体属**孤儿资产**（`L-01` 家族）。**因此上述迁移与"删 2 条"（Hills 条／Plain+Fertile 条）为零行为影响**；但删条目仍属我方单方判断，**列报请裁（§六 #1）**。

**资产侧迁移（13 个 BuildingDef，Unity 侧 `AssetDatabase` 写回，非手改 YAML）**：

| 资产 | 改前（TerrainType 值） | 改后（FeatureType） | 依据 |
|---|---|---|---|
| `tree` / `wood_pile` | `Forest(3)` | **`[Tree]`** | Forest→Tree |
| `mine` / `stone_pile` | `Quarry(4)`［stone_pile 另有 `Hills(2)`］ | **`[Mine]`** | Quarry→Mine；`Hills` **无对应地表物** ⇒ 删 |
| `ore_vein` | `Quarry(4)`＋`Snow(5)` | **`[Mine, SnowMountain]`** | Quarry→Mine；Snow→SnowMountain |
| `wall` / `farmland` / `castle` / `arrow_tower` | `Plain(0)` | **`[Plain]`** | 同值 |
| `treasure_box` / `portal` / `rift` / `ruins` | `Wasteland(1)`／`Coast(6)`／`Hills(2)` | **`[]`（空＝不校验）** | 三型**无对应地表物**（`FeatureToTerrain` 派生表**永不产生**）⇒ 原检查**恒 false**；见 §六 #2 观察项 |
| `ResourceGenConfig.baseCounts` | 5 条（含 `Hills`／`Plain+Fertile`） | **3 条**（`Tree`／`Mine`／`Plain`） | 死字段（见上），零行为影响 |

---

## 二、片 2-B · `PlainSubState` 删

- 枚举删；`GridSystem._plainSub` 数组删；`PopulateFromMap` / `RefreshCellFromFeature` 内的子状态写入行删。
- 读取端唯一处 `GetPlainSubStateAt` 零调用 ⇒ 随删（片 2-C 一并处置）。
- `ResourceCountEntry.plainSubState` 键删（见片 2-A）。

## 三、片 2-C · 死接口「删或接」—— ⭐ **本报告声明所选：两者均「删」**

| 接口 | 实测调用数 | 所选 | 理由 |
|---|---|---|---|
| `GridSystem.IsOccupied(c)` | **0**（改前全库仅自身定义 ＋ `IsFootprintClear` 内 1 处自用） | **删** | 与 `GetOccupant(c) != null` **完全等价**（纯重复接口）；自用处已改 `GetOccupant(c) != null`；探针 `Valley2_20B_Smoke_M7:445` 同步改 |
| `GridSystem.GetPlainSubStateAt(c)` | **0** | **删** | 其返回类型 `PlainSubState` 随片 2-B 整层删 ⇒ 物理上无法保留 |

⚠️ **另发现（列报·未处理）**：`GridSystem.MapCellCount`（`GridSystem.cs:28`·注释自标"过渡…2_4 重写后移除"）经 grep **零消费**，属同族死接口 —— **不在本片 2-C 名单**，未处理，备片 4 处置。

---

## 四、判据逐条实测（HH.294 §四 片 2）

| 判据 | 读数 | 判 |
|---|---|---|
| `TerrainType` **全库零引用**（grep 证据） | 严格语义位检索（`TerrainType.`／`PlainSubState.`／`enum …`／`…[]` 类型位）：**1 命中，且为探针内注释行**（`Valley_HH294_GrainProbe.cs:179`）｜其余命中均为字符串字面量（Tooltip 文本）与说明注释 | ✅ |
| `PlainSubState` **全库零引用** | 同上（`terrain:`／`plainSubState:` 字段键在 `.asset/.prefab/.unity` 侧 **零命中**） | ✅ |
| 旧 API 调用清零 | `GetTerrainAt`／`SetTerrain`／`GetPlainSubStateAt`／`IsOccupied(` 代码位 **零命中** | ✅ |
| **编译 0 error** | `read_console(types=[error])` ⇒ **空数组** | ✅ |
| **0 新增 warning** | `read_console(types=[warning])` ⇒ **空数组** | ✅ |
| **生成一张图正常** | 384²·seed 21107：总格 147456＝Plain 109090／Tree 5178／Mine 3760／山 13511／水 4052／一次性 11865；`climateZones` 长度 147456 | ✅ |
| **能走通** | GridSystem 可走层 BFS：spawn0 → 其余 4 个出生点 **[1]=True [2]=True [3]=True [4]=True** | ✅ |
| 派生一致性（附） | 抽样 400 格：`GetWalkFlags` 与"由地表物独立推导"**不一致 0** | ✅ |
| ⭐ **`allowedTerrain=[矿山]` 语义保住**（正反例实证） | ① `mine.asset` 载入 `allowedTerrain=[Mine]`、`footprint=(2,2)`；② 采样点：矿山 2×2 簇 =`(41,2)`、平地 2×2 =`(2,2)`；③ **在矿山**上放 mine ⇒ `ok=True reason=None`；④ **在平地**上放 mine ⇒ **`ok=False reason=Terrain`** | ✅ **正反例俱成立** |
| 确定性（同 seed 逐格一致·含 climateZones） | 片 1 探针复验：**逐格一致**（features＋climateZones＋spawns＋nb） | ✅ |

**正门／收尾**：探针经 `TestHarnessApi.EnterTestRun`（`WorldSize=Large`·seed 21107·1x）进入，自收尾 `ExitTestRun`＋`QuitSmoke`；实测 `isPlaying=False`（三态退 Play ✅）。日志汇总 `PASS=9 FAIL=0`。

---

## 五、连带改动（同批·非越界声明）

| 文件 | 改动 | 归类 |
|---|---|---|
| `Valley2_17_Smoke_12.cs` / `_13.cs` | 反射取 `"_terrain"` 判定"未装载" ⇒ 改取 `"_walkFlags"`；`SetTerrain(..., Plain)` ⇒ `RefreshCellFromFeature(..., Plain)`（3 处） | **编译连带**（不改则 0 error 判据不成立） |
| `Valley2_20B_Smoke_M7.cs` | `grid.IsOccupied(c)` ⇒ `grid.GetOccupant(c) != null` | **编译连带** |
| `Valley_HH294_GrainProbe.cs` | 原 `TerrainType[]`/`PlainSubState[]` 内存行 ⇒ `int[]` 等价复测（同为 4 B 底型，尺寸一致） | **本批探针** |

**范围声明**：以上均为"被删 API 的调用点/探针"，属**片 2 直达后果**，未触中层（建筑功能 08／交互 05／任务 06／伤害 07／建筑数据化）。

---

## 六、未完成项 / 列报请裁（显式列出）

1. **`ResourceGenConfig.baseCounts` 删 2 条（`Hills`／`Plain+Fertile`）** —— 该字段**全库零消费**（孤儿资产）⇒ 零行为影响；但"删哪两条"属我方判断，**请追认**（备选：整件保留原状／整件移交片 4 删或接）。
2. **4 个资产 `treasure_box`/`portal`/`rift`/`ruins` 由"三型地表物"改为空数组** —— 语义上：原三型（Wasteland/Coast/Hills）在 `FeatureToTerrain` 派生表中**永不产生** ⇒ 原 `allowedTerrain` 检查**恒 false**（即"永远建不成"）；改空后 = **跳过该校验**。
   - 已核**玩家路径无影响**：四者 `isPlayerBuilt=False`，而 `BuildingMenuPanel.cs:209` 按 `isPlayerBuilt` 过滤 ⇒ **不进玩家建造菜单**。
   - ⚠️ **AI／立国等非玩家路径未逐条核**（时间与范围所限）⇒ **如实列为观察项**；若策划端要求"保持恒拒"，改法＝把四者 `allowedTerrain` 置为**永不可能的地表物**或加"禁放置"标记（属功能改动，需另批）。
3. **`MapCellCount` 新发现死接口**（零消费）未处理（不属片 2-C 名单）。
4. **`Assets/Resources/Debug/Maps/map_0_seed12345.json`（8220 B·`31673197` 2026-07-29）为 1D 时代产物**，内含 `terrain:"Coast"`／`plainSubState`／`bigTerrain`／`regions` 等**已废字段**；全库 **零代码消费者**（`Resources/Debug` 零命中）⇒ 属**孤儿数据**，**未删**（删资产不在本片授权内），列报备处置。
5. 实机全局长跑（含 LLM/长局观测）**未跑** —— 本片为"纯删"，判据按任务书仅要求"生成一图＋能走通＋语义实证"，已达成；若需长局回归，请另行指派。

---

## 七、产出与证据索引

| 物 | 位置 |
|---|---|
| commit（片 2） | **`919580e3`**（24 files：10 `.cs` ＋ 13 `BuildingDef.asset` ＋ `ResourceGenConfig.asset`） |
| 探针 | `Assets/Editor/Smoke/Valley_HH294_Slice2Probe.cs`（正门·可重跑） |
| 日志 | `Valley Rampart/Logs/hh294_slice2_probe.log`（`Logs/` 在 .gitignore 域） |
| 资产现值普查 | 40 个 `BuildingDef`：`allowedTerrain` 非空 **9 个**（arrow_tower/wall=Plain；castle/farmland=Plain；mine/stone_pile=Mine；ore_vein=Mine+SnowMountain；tree/wood_pile=Tree） |

## 八、对片 3 的交接口径（供策划端判）

- 本片已把 `GridSystem` 的**派生数组从 5 个减到 3 个**（`_walkFlags`／`_occupants`／`_cells`）⇒ 片 3「数组下移」的乘数基数由 **5 arrays** 降为 **3 arrays**，与片 1-B 实测的"三数组 38.26 MB／净增 34.73 MB"口径一致。
- `TerrainType`/`PlainSubState` 已不在牌面上 ⇒ 片 3 无需再考虑这两个数组的下移或折中。