# `HH.328` `M4`（`08` 建筑功能层）开片前实核 交付报告

- **编号**：**`HH.328`**（对 `_编号登记.md` 在途行另取 · 先登记后落盘 · 水位线 327→328）
- **依据**：`多Agent交接/策划端/HH.327_M4开片前实核_任务书.md`（`D862`）｜`中层执行计划.md` §4.4｜`08_建筑功能层.md`
- **性质**：只读实核（⛔ 未写生产码／未改资产／未出方案／未改 `08`／`05`／`06`／`中层执行计划`）
- **基线**：`HEAD = 0784327a`（「HH.324/325/326/327 收口」·含裁4还原的 `TaskScheduler.cs` ＋ `HH.326` 报告）；工作区脏面见 §零代码声明
- **日期**：2026-09-24

---

## 〇、一句话

⭐ **五病灶：4 条「仍成立」＋ 1 条「计数口径须裁」**（病灶四「7 个布尔」与 `08` 自身 §2.5 的 **9** 个／§4.1 的 **10** 个三处不一致）；⭐ **六子片：`M4-F` 缺口已闭合（片 6-2 已收），其余 5 片缺口仍在**；⛔ **三个停手条件命中 2 个**（`M4-E` 必动 `TryAdvertiseTask`；`M4-B`／`M4-D` 必须先有能力表）＋ `M4-F` 闭合 ⇒ **均只列报、未出方案**。

---

## 一、件 A：五个病灶对账（原文／实盘／判）

### A-1 `AttachComponents` 硬编码 if 链

| 项 | 内容 |
|---|---|
| **原文** | 「`AttachComponents` 硬编码 if 链（**7 条件**）」（`中层执行计划.md:240`）｜`08` §2.2 引 `BuildingFactory.cs:281-328`，称 7 条件嵌套 |
| **实盘** | `BuildingFactory.cs:238-285` `AttachComponents(Building, BuildingDef)`；**`if` 语句实测 9 处**：`econStorageOnly` 判 `:248`／产能分支 `:252`（3 子条件）／`isSiegeWorkshop` `:256`／嵌套 `else` 内 `isBlacksmith` `:264`／`isMineByproduct` `:273`／`combat.attack > 0` `:277`／`isConsumable` `:279`／`sourceType == Rift` `:281`／`sourceType == CastleCore` `:283`；⭐ `StorageComponent` **仍在两分支各挂一次**（`:250` 与 `:262`） |
| **判** | ⚠️ **仍成立**（形态未变：仍是逐条 `if` ＋ 具体组件类名；`08` §2.2 自记的「`StorageComponent` 两处重叠」**仍在**）<br>⚠️ **两处对不上照实写**：① **行号漂移** `281-328` ⇒ 现值 **`238-285`**；② **条件数** 原文「7」⇒ 实读 **9 个 `if`**（其中 `:256`／`:264` 为嵌套二分 ⇒ 也非纯线性 7 条） |
| 补充 | ⛔ 无 `components` 数据栏（`BuildingDef.cs` 内 `components`／`List<` 组件列表 **0 命中**）⇒ 未数据化 |

### A-2 `ProductionSystem` 点名的组件 tick

| 项 | 内容 |
|---|---|
| **原文** | 「`ProductionSystem` 硬编码 **4 个**组件 tick」（`中层执行计划.md:240`）｜`08` §2.3 引 `ProductionSystem.cs:35-45` |
| **实盘** | `ProductionSystem.cs:27-47` `TickAll()`；点名 **4 个具体类**：`ProducerComponent` `:35-36`／`BlacksmithBuilding` `:38-39`／`SiegeWorkshopBuilding` `:41-42`／`MineByproductComponent` `:44-45`；外层 `for` 遍历 `BuildingRegistry.Instance.All`（`:30-34`），**每秒一次**（`_tickInterval = 1f` `:11`／`:20-23`） |
| **判** | ✅ **仍成立**（行号也与 `08` 记载一致：`35-45` ⇒ 现 `35-45` 逐行相同） |

### A-3 五套「建筑是什么」的枚举

| 项 | 原文（`08` §2.4 行号／项数） | 实盘（当前工作区） | 判 |
|---|---|---|---|
| `BuildingType` | `GridTypes.cs:146` · **13** | `GridTypes.cs:127` · **13 项**（`None`／`Tree`／`Mine`／`Farmland`／`StonePile`／`WoodPile`／`OreVein`／`TreasureBox`／`Ruins`／`Interactable`／`Rift`／`CastleCore`／`VagrantCamp`） | 仍在 · 项数同 · ⚠️ 行号漂移 |
| `BuildingCategory` | `GridTypes.cs:123` · **5** | `GridTypes.cs:104` · **5 项**（`ResourceProducer`／`ResourcePickup`／`SpecialPoint`／`Rift`／`CastleCore`） | 仍在 · 项数同 · ⚠️ 行号漂移 |
| `BuildingRole` | `GridTypes.cs:136` · **5** | `GridTypes.cs:117` · **5 项**（`Defense`／`Production`／`Economy`／`Wall`／`Special`） | 仍在 · 项数同 · ⚠️ 行号漂移 |
| `ModuleType` | `ModuleType.cs:7` · **5** | `ModuleType.cs:7` · **5 项**（`Civil`／`Production`／`Livelihood`／`Military`／`Commerce`） | 仍在 · 行号一致 |
| `InteractableType` | `BuildingDef.cs:155` · **3** | `BuildingDef.cs:174` · **3 项**（`Own`／`Enemy`／`Resource`） | 仍在 · 项数同 · ⚠️ 行号漂移 |
| （`08` 自记的"精神分裂"注释） | `GridTypes.cs:132-135` | `GridTypes.cs:113-116`「两套枚举不同名不同义，避免精神分裂」**原文仍在** | 仍成立 |

| 判 | ✅ **仍成立**（五套俱在、项数 13/5/5/5/3 与 `08` 逐项一致；⚠️ 除 `ModuleType` 外行号全漂移） |
|---|---|

### A-4 「7 个布尔当类型」

| 项 | 内容 |
|---|---|
| **原文** | ⚠️ **三处口径不一致**（照实报）：① `中层执行计划.md:240` 写「**7 个**布尔当类型」；② `08` §2.5 表列 **9 行**；③ `08` §4.1 表列 **10 行**（多 `isConsumable`）；且 §六 判据 4 写「§4.1 那 **7** 个布尔」 |
| **实盘** | 定义处全在 `BuildingDef.cs`：`role:19`／`isBridge:38`／`isGate:40`／`moduleType:46`／`isObstacle:61`／`isResourceNode:79`／`isBlacksmith:81`／`isSiegeWorkshop:83`／`interactableType:95`／`isPlayerBuilt:96`／`isDestructible:97`／`sourceType:104`／`isConsumable:106`／`isMineByproduct:129`；<br>⭐ 另有 `Building` 侧同名字段：`Building.cs:85 isObstacle`／`:125 isPlayerBuilt` ＋ `Init(..., bool isPlayerBuilt, ...)`（`:419`／`:426`）⇒ 计数含之 |
| **判** | ⚠️ **仍成立（布尔未退役）** ＋ ⭐ **须补证/待裁**：病灶名里的数字「**7**」**与本层文档自身（9 行／10 行）对不上** ⇒ 见 §停手命中① |

### A-5 `IsFortification` 四源混判

| 项 | 内容 |
|---|---|
| **原文** | `08` §2.6 锚 `Building.cs:743-744`；四源＝「枚举 × 布尔 × 枚举 × 枚举」 |
| **实盘** | **`Building.cs:1271-1274`**（⚠️ **漂移 −528 行**）：<br>`=> def != null && (def.role == BuildingRole.Wall \|\| def.isGate \|\| def.isBridge \|\| (def.role == BuildingRole.Defense && sourceType != BuildingType.CastleCore));`<br>⇒ **逐字与 `08` §2.6 引文相同**（四源：`role`／`isGate`／`isBridge`／`role` ＋ `sourceType`） |
| 现消费点（实读） | `Building.cs:1288`（致死分支：工事 ⇒ `Die(Killed)`）／`MonsterAI.cs:198`（不打工事）／`UtilityScorer.cs:681`（统计工事座数）／`KingdomBrain.cs:470`（`fortCount`）＋ Editor 探针多处 |
| **判** | ✅ **仍成立**（形态与文案一致）；⚠️ **计划书锚点行号已失效**（`743` ⇒ `1271-1274`） |

---

## 二、件 B：六个子片的缺口

| 子片 | 要核的事 | 实盘 | 判 |
|---|---|---|---|
| **`M4-A`** | ① `AttachComponents` 是否遍历数据行 `components` ② `ProductionSystem` 是否遍历 `ITickable` | ① ⛔ **否** —— `AttachComponents` 仍为 9 个 `if`（`BuildingFactory.cs:238-285`）；`BuildingDef` **无 `components` 栏**（0 命中）② ⛔ **否** —— ⭐ **`ITickable` 全库 0 命中**；`IBuildingComponent` 只有 `Init(Building)`（`BuildingComponents.cs:12-15`） | **缺口仍在** |
| **`M4-B`** | 7 布尔／五套枚举／`InteractableType` 是否还在 | ① ✅ 布尔**仍在**（§A-4，10 个标识符实读计数见 §件 D）② ✅ 五套枚举**仍在**（§A-3）③ ✅ **`InteractableType` 仍在** —— 枚举定义 `BuildingDef.cs:174` ＋ 字段 `BuildingDef.cs:95`，⭐ **零消费**（全库该标识符仅此 2 处命中 ⇒ 与 `08` §4.3「零消费死字段」一致；原锚 `BuildingDef.cs:77` ⇒ 现值 `:95`） | **缺口仍在** |
| **`M4-C`** | 4 空壳组件是否还在、各自有无生产调用点；6 栋空资产是否还在；同名坑登记 | ① `PickupComponent` `BuildingComponents.cs:21` —— 生产挂载点 **1 处** `BuildingFactory.cs:280`（条件 `def.isConsumable`；实盘 `isConsumable: 1` 的 def ＝ `ore_vein`／`stone_pile`／`treasure_box`／`wood_pile` 4 栋，**均无运行时实体路径**）② `SpawnerComponent` `:27` —— ⛔ **`AddComponent<SpawnerComponent>` 全库 0 命中（零挂载 · 纯孤儿类）** ③ `RiftComponent` `:153` —— 挂载点 **1 处** `BuildingFactory.cs:282`（条件 `sourceType == Rift`＝10；实盘 `rift.asset` ＋ `portal.asset` 两栋，**均无创建路径**）④ `CastleCoreComponent` `:159` —— 挂载点 **1 处** `:284`（`castle.asset` `sourceType: 11` ⇒ **活跃**）<br>⑤ 6 栋空资产**全部仍在**（`Assets/Resources/Buildings/` 实读）：`rift.asset`／`portal.asset`／`ruins.asset`／`treasure_box.asset`／`FoodWorkshop.asset`／`Ranch.asset`（＋ 各自 `.meta`）<br>⑥ 同名坑**只登记未删**：`ChestEntity`（活 · 与 `treasure_box.asset` 无关）／`bld_ruins`（sprite · 损毁态表现）／`bld_portal`（sprite · 给 `Portal.cs` 渲染） | **缺口仍在**（三删一留未做 · 6 资产未删） |
| **`M4-D`** | 7 栋功能是否仍只写在 `description` 里（**只报有无代码载体**） | **asset 侧（逐栋实读）**：`Church`／`FoodWorkshop`／`Hospital`／`House`／`LeyForge`／`Ranch`／`WarAcademy` —— `rate: 0`／`capacity: 0`／`attack: 0`／`trainingSlots: 0` **全 0**，`description` 非空且承载功能文本（如 `House`「人口容量3」／`Church`「幸福」／`Hospital`「受伤恢复＋幸福」／`LeyForge`「矮人专属·纯经济：采矿产量全局+40%」／`WarAcademy`「人类专属·爆兵加速：军事训练时长全局-25%…」／`FoodWorkshop`「粮×2→特殊食物」／`Ranch`「喂粮养动物→肉」）⇒ ⭐ **asset 侧仍无结构化载体**（与 `08` §八#35 描述一致）<br>**代码侧（实读有无 id 硬分支载体）**：`House` ✅ `PopulationSystem.cs:458`／`:477`（`def.id != "House"` 生育落点）＋ `HappinessSystem.cs:279`／`:312`（房容）／`:203`（幸福因子）｜`Church` ✅ `HappinessSystem.cs:206`｜`Hospital` ✅ `HappinessSystem.cs:207` ＋ `SatietySystem.cs:241`（`HasBuilding("Hospital", kingdomId)` 回血加成）｜`LeyForge` ✅ `KingdomRace.cs:99`（`HasExclusiveBuilding(..., BuildingIds.LeyForge) ? 1.4f`）｜`WarAcademy` ✅ `TrainingSystem.cs:434-436`（`warAcademyTrainingSpeedMul`）｜`FoodWorkshop` ⛔ **仅 sprite 映射**（`BuildingVisual.cs:61` → `bld_market`）｜`Ranch` ⛔ 仅 sprite（`BuildingVisual.cs:51`）＋ UI 行（`BuildingPanel.cs:492`），功能在**王国级** `RanchSystem` | **仍成立**（**能力声明载体 = 0**；功能靠**硬编码 id** 分支承载）⇒ ⛔ **未往 `10` 加能力条** |
| **`M4-E`** | 四处是否仍各写转化；**标出会不会改到 `HH.326` 已冻结的生产完成规则** | ① `ProducerComponent` —— `Tick()` `ProducerComponent.cs:63-98`（每秒 `_mainAccumulator += _rate × mul` ⇒ `_storage.Add` `:90-95`）② `BlacksmithBuilding` —— `Tick()` `:43+`（矿石→Metal）③ `SiegeWorkshopBuilding` —— `Tick()` `:108+`（扣原料 → 弹药入子仓）④ `TrainingSystem` —— `PayRecruit` `:455`（扣金/水晶/铁）＋ 天数驱动 `:56-63` ＋ `SetOccupation` `:100` ⇒ ✅ **四处仍各写转化**<br>⭐ **与冻结面的关系**：冻结的是 **`05` §四 行 6「生产」完成规则** ＝ ① 在岗计时判定（`TaskScheduler` `Working` 段 · `:542`）② 产量由建筑自己按秒累加（`ProducerComponent.cs:90-95`）。⇒ **`M4-E` 若收成一处 `convert`**：<br>· **`ProducerComponent` 的每秒累加仍是 `M4-E` 的实现面** ⇒ 会碰到①的**产出侧**（计时判定侧不在 `M4-E` 域）；<br>· ⛔⛔ **必动 `TryAdvertiseTask`** —— `BlacksmithBuilding.cs:85`／`SiegeWorkshopBuilding.cs:261`／`MineByproductComponent.cs:173` **三处均为 `ITaskSource` 实现者并各自广告 `Production`**（`Building.cs:1516` 另有 1 处）⇒ **合并/删类即改 `TryAdvertiseTask`**（红线面）⇒ **停手条件 2 命中**（列报，⛔ 不出方案） | **缺口仍在** ＋ ⭐ **撞冻结面（停手 2）** |
| **`M4-F`** | 计划书说 6 栋自然物 `BuildingDef` 运行时不生成实体 —— 现在是否已成立 | ⭐ **已成立**：① `MapGenRules.DeriveNaturalBuildings`（`MapGenRules.cs:1511-1514`）＝ **`map.naturalBuildings.Clear()`**（自陈「目标态：不再派生实体（资源点＝格表）」）② `BuildingFactory.InstantiateFromMap`（`BuildingFactory.cs:55-86`）**只建主城**（`:69-82`，`CastleCore`），日志自陈 `:84`「主城·**自然建筑自 `HH.294` 片 6-2 收尾起不再派生**」③ 调用点仍在管线（`WorldManager.cs:206` 步骤 11）但语义＝清空（契约槽位） | ⭐ **缺口已闭合** ⇒ **停手条件 3 命中**（⛔ 未写降级方案） |

---

## 三、件 C：和已冻结面的边界（只列会碰到什么）

| # | 会碰到的面 | 事实（`file:line`） |
|---|---|---|
| 1 | ⛔⛔ **`TryAdvertiseTask`（红线面）** | `M4-E`「收成一个 `convert`」必然触及三个 `ITaskSource` 实现者的广告面：`BlacksmithBuilding.cs:85-95`／`SiegeWorkshopBuilding.cs:261-271`／`MineByproductComponent.cs:173-217`（＋ `Building.cs:1516` 的 `Production` 支）；⛔ 本批红线明令**不改 `TryAdvertiseTask`** ⇒ **两片不可同批** |
| 2 | ⚠️ **`TaskScheduler`** | 上述三类若合并/删除 ⇒ 其 `Register`／`Unregister`／`LazyRegister` 调用面（`BlacksmithBuilding.cs:97-108`／`SiegeWorkshopBuilding.cs:273-278`）连带变动 ⇒ 会碰 `TaskScheduler._sources` **注册面**（`TaskScheduler.cs:136-148`）；⛔ 若**只换内部 tick 入口而保留类与广告面** ⇒ 不碰 |
| 3 | ⭐ **`05` §四 那 7 行的完成规则** | 冻结句 ＝ **行 6「生产」**：在岗计时（`TaskScheduler.cs:542`）＋ 产量由建筑每秒自累加（`ProducerComponent.cs:90-95`）。⇒ **`M4-A`（`ProductionSystem` 改遍历 `ITickable`）不碰**完成判定，只要「每秒一次」不变；**`M4-E` 会碰产出侧实现**（同上）；**`M4-D`／`M4-B` 不直接碰**（只补声明／改读声明） |
| 4 | ⭐ **必须先有 `BuildingAbilityCatalog`** | ① `M4-B` 的一条目标是「`IsFortification` 改读**能力声明**」（`08 §4.2:240`／`中层执行计划:245`）② `M4-D` 的目标是「补进**能力表**」（`08 §八#35`）⇒ **两者都以"能力表载体"为前置**，而 `M6`（`10`）**必须最后**、本片 ⛔ 不建 ⇒ **停手条件 2 的后半命中**（列报） |
| 5 | ⭐ **`BuildingAbilityCatalog` 现状** | **全库 0 命中**（无该类／无能力表代码载体）⇒ 判据 3「能力可查」在 `M4` 片内**结构性不可达**（除非先建，而红线禁止） |
| 6 | `10_建筑能力表.md` | ⛔ 本批**未改**、⛔ **未加能力条**（`10` 是文档侧；其 §十 29 栋表仍是"批 3 施工时搬进代码"的待落状态） |

---

## 四、件 D：计数

| 项 | 读数 |
|---|---|
| `AttachComponents` 的 **`if` 条件数** | **9**（`BuildingFactory.cs:248/252/256/264/273/277/279/281/283`）；其中 `:256`／`:264` 为嵌套二分（4 分支）⇒ 最深嵌套 **2 层**；⚠️ `08`／计划书写「7」 |
| `ProductionSystem` 点名组件类数 | **4**（`ProducerComponent`／`BlacksmithBuilding`／`SiegeWorkshopBuilding`／`MineByproductComponent` · `ProductionSystem.cs:35-45`） |
| 7 个布尔（口径 3 说 · 逐个给出） | 说明：**标识符级计数**（`rg` 全 `Assets/**/*.cs` · 含注释与 `Building` 侧同名字段／形参）<br>`isPlayerBuilt` **29 文件 / 59 处**｜`isConsumable` **30 / 41**｜`isResourceNode` **15 / 39**｜`isObstacle` **18 / 34**｜`isBridge` **6 / 11**｜`isGate` **6 / 8**｜`isBlacksmith` **6 / 8**｜`isSiegeWorkshop` **5 / 7**｜`isDestructible` **5 / 6**｜`isMineByproduct` **4 / 5**<br>⚠️ `08` §2.5 原记（27/8 · 10/5 · 10/5 · 9/7 · 7/6 · 5/5 · 4/4 · 4/3 · 2/2）**全部已增长**（代码与注释增多） |
| 五套枚举项数 | `BuildingType` **13**／`BuildingCategory` **5**／`BuildingRole` **5**／`ModuleType` **5**／`InteractableType` **3** |

---

## 五、件 E：`08` §六 6 条判据现状

| # | 判据（`08:267-272`） | 现状 | 依据 |
|---|---|---|---|
| 1 | **挂载数据化**：新增一栋建筑只改数据行 | ⛔ **未达标** | `BuildingFactory.AttachComponents:238-285` 仍逐条 `if` ＋ 具体类名；`BuildingDef` **无 `components` 栏** |
| 2 | **无类型 if**：`AttachComponents`／`ProductionSystem` **不再出现具体组件类名** | ⛔ **未达标** | 前者出现 `StorageComponent`／`SiegeWorkshopBuilding`／`BlacksmithBuilding`／`ProducerComponent`／`MineByproductComponent`／`CombatComponent`／`PickupComponent`／`RiftComponent`／`CastleCoreComponent`（`BuildingFactory.cs:250/258/262/265/267/275/278/280/282/284`）；后者出现 4 个（`ProductionSystem.cs:35/38/41/44`） |
| 3 | **能力可查**：任一建筑能列出 `provide` 能力清单 | ⛔ **未达标** | `BuildingAbilityCatalog` **全库 0 命中**；`10` 为文档侧；代码侧唯一"声明"是 `ITaskSource.TryAdvertiseTask`（任务面，非能力面） |
| 4 | **布尔归零**：§4.1 那 7 个布尔**全部退役** | ⛔ **未达标** | §A-4：布尔全在（10 个标识符实读计数在案）；⚠️ 判据自身写「7 个」而 §4.1 表列 10 行 ⇒ 口径待裁 |
| 5 | **死字段可见**：`InteractableType` 已删 ＋ **启动自检**能报"全库无人取"的栏 | ⛔ **未达标** | ① `InteractableType` **未删**（`BuildingDef.cs:174` 枚举 ＋ `:95` 字段 · **零消费**）② 未发现**启动期**自检；⚠️ 现有审计均为 **Editor 侧**（`Assets/Editor/Lifecycle/LifecycleAudit.cs`／`Assets/Editor/ChainAudit/`）⇒ 若 Editor 审计可折抵，须策划端裁口径 |
| 6 | **tick 可扩展**：新增带 `Tick` 的组件 ⇒ 不改 `ProductionSystem` 且确实被 tick 到 | ⛔ **未达标** | ⭐ **`ITickable` 全库 0 命中**；`ProductionSystem.TickAll` 硬编码 4 个类（`ProductionSystem.cs:35-45`）⇒ 新组件不会被 tick 到 |

---

## 六、停手命中情况

| 条件 | 命中 | 处置 |
|---|---|---|
| ① 某病灶的**计划书描述已不成立** | ⚠️ **部分命中** | 病灶四「**7 个**布尔」与本层文档自身（`08 §2.5` 9 行／`§4.1` 10 行／`§六 判据4` 又写 7）**三处不一致**；病灶一条件数「7」⇒ 实读 **9**，锚 `281-328` ⇒ 现 `238-285`；病灶五锚 `743` ⇒ 现 `1271-1274`。⇒ **照实报**（⛔ 未改 `08`／计划书，⛔ 未把该子片设计完） |
| ② 某子片一做就**改到冻结完成规则**／**必须先有能力表** | ⭐ **命中（两项）** | (a) `M4-E` 必动 **`TryAdvertiseTask`**（三处 `ITaskSource` 实现者）⇒ 与红线冲突；(b) `M4-B`（`IsFortification` 改读能力声明）／`M4-D`（补能力声明）**必须先有 `BuildingAbilityCatalog`**，而 `M6` 最后、本片不建。⇒ **列报即停**，⛔ 未给方案 |
| ③ `M4-F`「运行时不生成自然物实体」**已成立** | ⭐ **命中** | `DeriveNaturalBuildings:1511-1514` ＝ `Clear()`；`InstantiateFromMap:55-86` 只建主城（`:84` 自陈）⇒ **判闭合**，⛔ 未写降级方案 |

---

## 七、零代码改动声明（`git status`）

**本批（`HH.328`）本端动作 ＝ 只读取证 ＋ 写 1 份报告 ＋ 账本登记 1 行**：

- ⛔ 未写生产码 · ⛔ 未改资产（未删任何资产 · 6 栋空资产**原样**）· ⛔ 未建 `BuildingAbilityCatalog`
- ⛔ 未改 `08`／`05`／`06`／`中层执行计划`（发现过时只报）
- ⛔ 未改 `TaskScheduler`／`TryAdvertiseTask`／`WorkAt`（本批**未打开任何生产文件做写操作**）
- ⛔ 未碰工作区既有脏文件 · 未编译 · 未跑局 · 未 commit · 未 push

**`git status --short`（`Valley Rampart` 面 · 本批末）**：

```
 M Valley Rampart/Assets/Scenes/GameScene.unity                  ← 前序脏点（非本批）
 M Valley Rampart/Packages/manifest.json                         ← 前序脏点（非本批）
 M Valley Rampart/Packages/packages-lock.json                    ← 前序脏点（非本批）
 ?? Valley Rampart/Assets/Editor/Smoke/Valley_HH289_EcoProbe.cs(.meta)      ← 历史未跟踪
 ?? Valley Rampart/Assets/Editor/Smoke/Valley_HH290_GatherProbe.cs(.meta)   ← 历史未跟踪
 ?? Valley Rampart/Assets/Editor/Smoke/Valley_HH319_U15Probe.cs.meta        ← 历史未跟踪
 ?? Valley Rampart/Assets/Editor/Smoke/Valley_HH319_WaterAccount.cs.meta    ← 历史未跟踪
 ?? Valley Rampart/Assets/_Game/Art/Ground/New Palette.prefab(.meta)        ← 美术脏点（前序）
 ?? Valley Rampart/Assets/_Game/Art/Ground/ground_tropical.asset(.meta)     ← 美术脏点（前序）
```

**`git --no-pager diff --stat -- "Valley Rampart/Assets"`**：

```
 Valley Rampart/Assets/Scenes/GameScene.unity | 6 +++++-
 1 file changed, 5 insertions(+), 1 deletion(-)
```

⇒ **本批在 `Assets/**` 面新增／修改 = 0**（该 `GameScene.unity` 改动＝前序脏点；`TaskScheduler.cs`（裁4还原）已随 `0784327a` **收口提交**，故本轮 diff 中不再出现）。

**本批落盘物（均在 `多Agent交接/` · 非 `Assets` 面）**：

| 文件 | 面 |
|---|---|
| `多Agent交接/执行端/HH.328_M4开片前实核_交付报告.md` | 本报告（新增 1 个文件） |
| `多Agent交接/_编号登记.md` | 在途区**追加 `HH.328` 行 1 行**（取号登记 · 按 `HH.327` 任务书 §六 要求） |

⚠️ 遵本批红线 **⛔ 不 commit／不 push** ⇒ 账本 `HH.328` 行与报告均**留在工作区**（取号已占号；常规做法「取号＝只改本行＋独立 commit」见 §待裁-5）。

---

## 八、§待裁 / 列报（5 条）

| # | 事项 | 事实 |
|---|---|---|
| 1 | ⚠️ **病灶计数三处不一致** | 「7 个布尔」出现在 `中层执行计划.md:240` 与 `08 §六 判据 4`，而 `08 §2.5` 列 **9** 行、`§4.1` 列 **10** 行 ⇒ 请裁以哪一版为口径（本端 ⛔ 未改文档） |
| 2 | ⚠️ **`08`／计划书行号多处失效** | 病灶一 `281-328 → 238-285`；病灶三 4/5 处漂移；病灶五 `743 → 1271-1274`；`08 §4.3` 死字段锚 `BuildingDef.cs:77 → :95`；`08 §2.4` `InteractableType` 锚 `:155 → :174` ⇒ 修订归策划端 |
| 3 | ⭐ **`M4-F` 判闭合** | 闭环证据：`MapGenRules.cs:1511-1514`（`Clear()`）＋ `BuildingFactory.cs:84` 自陈 ⇒ 建议在 `08`／计划书侧标「已闭合」（⛔ 本端未改） |
| 4 | ⚠️ **`M4-D` 的"只在 description 里"须精确化** | 实读结果：asset 侧确无结构化载体（`rate/capacity/attack/trainingSlots` 全 0）；但**代码侧 5 栋已有硬编码 id 载体**（`House`／`Church`／`Hospital`／`LeyForge`／`WarAcademy`），`FoodWorkshop`／`Ranch` **仅 sprite（Ranch 另有 UI 行）** ⇒ `08 §八#35` 原句宜分「asset 侧／代码侧」两写 |
| 5 | ⚠️ **账本登记未提交** | 遵本批红线未 commit（常规做法见 `D640`「取号＝只改本行＋独立 commit」）⇒ 请策划端随下次账本提交落，或另令补提交 |

---

## 九、零方案声明

本报告只含：件 A 五病灶对账 ＋ 件 B 六子片缺口 ＋ 件 C 碰界面清单 ＋ 件 D 计数 ＋ 件 E 判据现状 ＋ 停手命中 ＋ 零代码声明 ＋ 待裁列报。⛔ 无方案、无建议、⛔ 未改任何生产码／资产／文档。
