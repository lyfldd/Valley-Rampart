# `HH.324` `M2`（`05` 交互层）**开片前实核** 交付报告

- **签发**：策划端（砚）｜**`D860`**｜**任务书**：`多Agent交接/策划端/HH.324_M2开片前实核_任务书.md`
- **性质**：⭐ **只读实核**（⛔ 未写生产码／未改资产／未出设计方案）
- **基线**：`HEAD = 4bb0bcf2`（`HH.324` 任务书签发提交）＋ 工作区**既存未提交脏点**（非本批产物，见 §七）
- **读数口径**：全部读数为**工作区**（含既存脏点）实读；`file:line` 均按**当前工作区**行号
- **日期**：2026-09-24

---

## 〇、一句话

⭐ **`05` §十 五行的现状**：**2 行仍成立**（第 3、4 行）· **1 行计数漂移**（第 5 行）· **2 行已变**（第 1 行 7→8 实体源／第 2 行 12→**11** 项）· 节末「真跑通交互只有一种」**整条不成立**（其唯一例证 `HarvestCarry()` 已删）⇒ ⚠️ **停手条件 ① 命中**（照实报，⛔ 未自行改文档）。

⭐ **件 B 结论（最关键）**：现存**配对载体 12 类**、**第二处独立配对载体**（`ScheduleCenterStub._crewAssignments`）；**「配对零引用」判据 4 ＝ 未达标**（`KingdomTask.source` ＋ 工人侧 `TaskStimulus.Source/Issuer` 双向持引用）；`M2-B` 若开工，**改动面与 `M5`（`06`／`TaskScheduler`）正面重叠 8 项**（见 §二.4）。

---

## 一、件 A（主）· `05` §十 **逐行对账表**

> 原文出处：`最高优先级文档/05_交互层.md:197-207`（§十 共 5 行 ＋ 节末一句）。
> 计数口径：`Assets/**/*.cs`（含 `Editor/**`），`rg` 默认模式；**注释行单列**（`^\s*///?` 行不计入"代码面"）。

### A-1 `ITaskSource`（**7 个各自为政**）

| 项 | 内容 |
|---|---|
| **原文** | `ITaskSource`（**7 个各自为政**）⇒ 对象的能力声明 |
| **实盘** | **实现者 ＝ 9 个**（**8 个实体源** ＋ 1 个私有调试源） |

逐个列出：

| # | 类型 | `file:line` | 形态 | 广告的任务类型 |
|---|---|---|---|---|
| 1 | `Building` | `_Game/Systems/Building/Building.cs:29` | MonoBehaviour | `Build`(拆除)／`Transport`(阈值＋强制)／`WaterHaul`／`Production`（4 类 · 分支序 `:1527/1537/1550/1578/1603/1615`） |
| 2 | `UnitController` | `_Game/Systems/Unit/UnitController.cs:24` | MonoBehaviour | `AmmoReload` |
| 3 | `ChestEntity` | `_Game/Systems/World/ChestEntity.cs:18` | MonoBehaviour | `Transport` |
| 4 | `WorldGatherSource` | `_Game/Systems/World/WorldGatherSource.cs:36` | 纯 class（非 Component） | `Gather` |
| 5 | `ConstructionSiteStore` | `_Game/Systems/Building/ConstructionSiteStore.cs:23` | MonoBehaviour | `Build`（`HaulToSiteArgs` 搬料） |
| 6 | `MineByproductComponent` | `_Game/Systems/Building/MineByproductComponent.cs:30` | MonoBehaviour | `Production`／`Transport` |
| 7 | `BlacksmithBuilding` | `_Game/Systems/Kingdom/BlacksmithBuilding.cs:12` | MonoBehaviour | `Production` |
| 8 | `SiegeWorkshopBuilding` | `_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs:19` | MonoBehaviour | `Production` |
| 9 | `WorkerTask.DebugTaskSource` | `_Game/Systems/Unit/WorkerTask.cs:58`（`private class`） | 纯 class（调试） | ⛔ 无（`TryAdvertiseTask` 恒 `false`，`:64`） |

| 判 | ⚠️ **已变（写明变成什么）** |
|---|---|
| 依据 | 原文「7 个」＝ 上表 **1~7**（写作时集合）；**增量 = `ChestEntity`**（`HH.316` 件2 一箱一源 · `D798` 裁 ④）＋ `WorkerTask.DebugTaskSource`（调试源，非实体）。⇒ 现 **8 个实体源** ＋ 1 调试源。 |

### A-2 `KingdomTaskType`（**12 项枚举**）

| 项 | 内容 |
|---|---|
| **原文** | `KingdomTaskType`（**12 项枚举**）⇒ 取消（能力名 ＋ 交互表替代） |
| **实盘** | ⭐ **11 项**（`_Game/Data/TaskPriorityConfig.cs:32-47`，唯一定义点） |

逐项列出（按声明序）：

| # | 项 | `:行` | 生产码里被**构造**? | 备注 |
|---|---|---|---|---|
| 1 | `Repair` | `:34` | ⛔ **无**（仅 Editor 冒烟 `Smoke_2_23RB.cs:155/215`） | 死值 |
| 2 | `Build` | `:35` | ✅ `Building.cs:1529`(拆除)／`ConstructionSiteStore.cs:187`(搬料) | 一值两义（靠 `args` 区分） |
| 3 | `Produce` | `:36` | ⛔ **无** | 死值（与 `Production` 语义重叠） |
| 4 | `Transport` | `:37` | ✅ `ChestEntity:138`／`Building:1553/1621`／`MineByproduct:205`／`WorkerTask:47` | — |
| 5 | `Rancher` | `:38` | ⛔ **无**（仅 `ResourceBiasConfig:64` 兜底表） | 死值 |
| 6 | `WaterCarry` | `:39` | ⛔ **无**（仅 `ResourceBiasConfig:65` `enabled=false`） | 死值 |
| 7 | `GoldMine` | `:40` | ⛔ **无**（仅 `ResourceBiasConfig:63`） | 死值 |
| 8 | `Production` | `:42` | ✅ `Building:1608`／`Blacksmith:92`／`Siege:268`／`MineByproduct:182` | — |
| 9 | `WaterHaul` | `:43` | ✅ `Building:1586` | — |
| 10 | `Gather` | `:44` | ✅ `WorldGatherSource:109`／`WorkerTask:47` | — |
| 11 | `AmmoReload` | `:46` | ✅ `UnitController:1029` | — |

**还有哪里 switch／比较它**（生产码，逐处）：

| 文件 | 行 | 形态 |
|---|---|---|
| `TaskScheduler.cs` | `:295` `:320` | `task.type == KingdomTaskType.Transport`（分派/容量） |
| `TaskScheduler.cs` | `:542` `:556` `:570` `:584` | `Working` 分支链（`==` ＋ `args is …`） |
| `TaskScheduler.cs` | `:616` `:618` `:620` | `MovingToDest` 分支链 |
| `TaskScheduler.cs` | `:731-781` | `switch (task.type)` ＝ `ExecuteCompletion`（**完成回调唯一分派点**） |
| `TaskScheduler.cs` | `:1407-1422` | `switch (task.type)` ＝ `ResolveTaskBiasResource`（活权重映射） |
| `TaskScheduler.cs` | `:1267` `:1278` `:1306` | 形参类型 ＋ `kv.Value.type == type` |
| `TaskScheduler.cs` | `:1328` | `((int)a.type).CompareTo((int)b.type)`（排序次级键） |
| `TaskPriorityConfig.cs` | `:20-28` | `entries[i].taskType == type`（线性比较 → `TaskPriority`） |
| `ResourceBiasConfig.cs` | `:80-91` | `e.taskType == type`（线性比较 → `EcoResource`） |
| `TaskPriorityConfig.cs:53`／`ResourceBiasConfig.cs:33` | — | **结构体字段（可作字典/表键的形态）** |

| 判 | ⚠️ **已变（差 1 · 原文过时）** |
|---|---|
| 依据 | 唯一定义点实读 **11 项**，无第二处 `enum KingdomTaskType`；**11 ≠ 12**。另：**11 项里 5 项（#1/#3/#5/#6/#7）在生产码零构造点**（仅枚举声明 ＋ 兜底映射表 ＋ Editor 冒烟）⇒ 若 `M2` 要"取消枚举"，可判死面 = 5 项。 |

### A-3 `BehaviorModule.WorkAt`（自注"占位 · 位移即目的"）

| 项 | 内容 |
|---|---|
| **原文** | `BehaviorModule.WorkAt`（自注"砍/挖/建**占位**·首版『位移即目的』"）⇒ 执行一次交互的通用动作 |
| **实盘** | ⭐ **注释仍在**（**两处**）：<br>· `_Game/Systems/AI.Core/Decision/BehaviorModule.cs:17` → `/// <summary>工作循环：砍/挖/建占位，首版"位移即目的"</summary>`<br>· `_Game/Systems/AI.Core/Decision/L3CommandComputer.cs:78` → `cmd.Duration = 0f;  // P0 WorkAt 占位：位移即目的，无工作时长` |
| **执行体** | `_Game/Systems/AI/Execution/BehaviorExecutor.cs:157-184 ExecuteWorkAt`：到达阈值内 ⇒ 边沿触发一次 `Brake()` ＋ `cmd.HarvestTarget?.Harvest()`（`:175-176`）；否则 `NavigateTo` |
| 判 | ✅ **仍成立**（注释在）；⚠️ **但执行体已是死支**：`HarvestTarget` 唯一写入侧 ＝ `L3CommandComputer.cs:81`（`posture.Focus.Focus is TaskStimulus workTs && workTs.Source is IHarvestable`）；而 `IHarvestable` 全库唯一实现 ＝ `StorageComponent.cs:16`，**不是任何 `ITaskSource`**；`TaskStimulus.Source` 恒 ＝ `task.source`（`StimulusTypes.cs:136`）⇒ **生产链上该块恒不命中**（码面自陈 `BehaviorExecutor.cs:170-174`「生产路径上本块**已不可达**（`Inert`）」）。 |

### A-4 `WorkerTask`（只有 `Gather`/`Transport`）

| 项 | 内容 |
|---|---|
| **原文** | `WorkerTask`（只有 `Gather`/`Transport`）⇒ 一个交互实例 |
| **实盘** | ✅ **仍 2 种**：`WorkerTaskType { Gather, Transport }`（`_Game/Systems/Unit/WorkerTask.cs:15-19`）；`WorkerTask` 本体已退化为**静态工厂**（`:25`）：`GetCarryAmount(:30)`／`CreateTask(:42)` |
| **消费者** | `WorkerTask.CreateTask` ← `AIDebugSpawnController.cs:345`（`:340 AssignKingdomTask`／`:357 SpawnCivilianWithTask` · **唯一调用方**）；`WorkerTask.GetCarryAmount` ← `TaskScheduler.cs:1093`／`:1293` |
| 判 | ✅ **仍成立** |

### A-5 `TaskScheduler`（原文「10 方法 / 51 处 / 13 文件」）

| 子项 | 原文 | 实盘 | 判 |
|---|---|---|---|
| `ITaskScheduler` **方法数** | `10 方法` | ✅ **10**（`ITaskScheduler.cs:11,14,17,20,23,26,29,32,35,38`） | **仍成立** |
| `ITaskScheduler`（**接口名**）引用 | （任务书归此行） | ⚠️ **2 文件 / 3 处**：`ITaskScheduler.cs:8`（声明）＋`TaskScheduler.cs:32`（实现）＋`TaskScheduler.cs:134`（分区注释）⇒ **零外部调用方**（死抽象） | **已变·须补证**（原文未单列接口名计数） |
| `TaskScheduler`（**类名**）调用面 | `51 处 / 13 文件` | 生产码 `TaskScheduler.Instance\|HasInstance` ＝ **54 处 / 15 文件**（原文出处 ＝ `06_任务层.md:152` ＋ `测试基线台账.md:1951-1953`，指**类**调用面） | ⚠️ **已变（+3 处 / +2 文件）** |
| 同类标识符（含注释） | — | `\bTaskScheduler\b` ＝ **59 文件 / 321 处**（其中**注释行 106 处**、44 文件 ⇒「代码面」215 处） | 参考读数 |

分文件对照（生产码 `.Instance|.HasInstance`）：

| 文件 | 原文 | 实盘 |
|---|---|---|
| `Building.cs` | 13 | **14** |
| `SiegeWorkshopBuilding.cs` | 7 | **6** |
| `MineByproductComponent.cs` | 6 | **5** |
| `BlacksmithBuilding.cs` | 6 | **6** |
| `UnitController.cs` | 4 | **4** |
| （其余 10 文件） | （原文以「…」截断） | `ChestEntity 1`／`ChestManager 4`／`ResourceRespawnSystem 2`／`WorldGatherRegistry 1`／`VagrantCampSystem 2`／`KingdomFoundry 2`／`ProducerComponent 1`／`BuildingPanel 2`／`BuildingFactory 2`／`AIDebugSpawnController 2` |

| 判 | ⚠️ **已变**（分文件前三项与原文不符；上方 54/15 ＝ 生产面，Editor 面另 12 文件/95 处） |
|---|---|

### A-6 ⭐「真跑通的交互**只有一种**」

| 项 | 内容 |
|---|---|
| **原文** | 「真跑通的交互**只有一种** —— `WorkAt → ExecuteWorkAt → HarvestCarry()`（走过去 ＋ 搬走）」（`05_交互层.md:207`） |
| **实盘** | ⛔ **该链已不存在**：`HarvestCarry()` **已删**（`M1-G-1` `#40` · `D824` §一-4/件3；墓碑注 `StorageComponent.cs:352`；连带 `TaskScheduler.cs:740` 注明"原 `st.HarvestCarry()` 直接入国库 **已删**"）⇒ **原文例证整条失效**。 |

⭐ **现「在跑」的交互链（判据：有生产调用点 ＋ 有完成回调 ＋ 有消费方）**：

| # | 交互 | 广告/调用点 | 完成回调 | 消费方 |
|---|---|---|---|---|
| 1 | **采集** | `WorldGatherSource.TryAdvertiseTask:105` ← 玩家 `ResourceRespawnSystem.ConfirmResourceGather:475`／AI `WorldGatherRegistry.Advertise` | `TaskScheduler.ExecuteCompletion:754-780`（`inv.TryStore` ＋ `wg.OnGatherCompletion:124`） | 工人背包 → 就近仓/台账；格翻 `Plain`（`ResourceRespawnSystem.HandleCellGathered`） |
| 2 | **搬运** | `Building.TryAdvertiseTask:1553`(强制)/`:1621`(阈值)／`ChestEntity:131`／`MineByproductComponent:205` | `LoadInventoryFromSource:866` → `UnloadInventory:901` | 就近同国仓 `WarehouseRegistry.FindNearestAvailable` |
| 3 | **搬水** | `Building:1586`（农场广告 · 源＝水井） | `LoadInventoryFromSource` → `DepositWaterToFarm:1044` | 农场仓 |
| 4 | **搬料/建造** | `ConstructionSiteStore:168`（`HaulToSiteArgs`） | `LoadSiteMaterials:978` → `DepositToSite:1009` | 工地仓 `ConstructionSiteStore.Deposit` |
| 5 | **装填** | `UnitController:1023` | `LoadAmmoToBackpack:1073` → `UnloadAmmoToMagazine:1106` | 单位弹仓 `FillMagazine` |
| 6 | **生产** | `Building:1608`／`BlacksmithBuilding:92`／`SiegeWorkshopBuilding:268`／`MineByproductComponent:182` | `ExecuteCompletion:733`（`ProducerComponent.Tick()`） | 本建筑仓 |
| （7） | （拆除） | `Building:1529`（`DemolishTaskArgs`） | `else ⇒ Complete`（进度由建筑自推 `Building.DemolishDuration`） | `Building._demolishProgress` |

| 判 | ⛔ **已变（原文例证已删）** ＋ ⚠️ **「第二处真跑通交互」实际存在** |
|---|---|
| 依据 | ① 原文唯一例证 `HarvestCarry()` **已删**（§A-6 上文）；② 「`WorkAt → ExecuteWorkAt`」链本身**结构性惰性**（§A-3）；③ 现行在跑交互 **≥6 条**（上表，均走 `KingdomTask` ＋ `TaskScheduler` 完成回调）。<br>⚠️ **口径限制**：以上为**码面接线判定**（生产调用点＋回调＋消费方均在册）；「**活世界是否真跑**」本批**未取运行读数** ⇒ 该判据的**运行面**如实记「**须活世界读数**」（⛔ 不出方案）。 |

---

## 二、件 B（⭐ 最关键）· `M2` ／ `M5` **分界取证**

### B-1 ⭐ 现有**配对载体**逐个列出（`file:line` ＋ 一句职责）

| # | 载体 | `file:line` | 职责 | 归属层 |
|---|---|---|---|---|
| 1 | `TaskScheduler._sources` | `TaskScheduler.cs:55` | 在册任务源集合（`HashSet<ITaskSource>`）＝广告面 | `06`／`M5` |
| 2 | `TaskScheduler._npcTaskMap` | `:56` | **配对表主体**：`npcId → KingdomTask` | `06`／`M5` |
| 3 | `TaskScheduler._npcStateMap` | `:57` | 工人任务态（`TaskState`，`GetWorkerState` 读口） | `06`／`M5` |
| 4 | `TaskScheduler._npcBrainMap` | `:58` | `npcId → NPCBrain`（Abandon/Complete 复位用） | `06`／`M5` |
| 5 | `TaskScheduler._suspendStartTime` | `:59` | 威胁挂起计时（T-K/T-R） | `06`／`M5` |
| 6 | `TaskScheduler._workStartTime` | `:60` | `Working` 段起点（`GetTaskDuration` 比） | `06`／`M5` |
| 7 | `TaskScheduler._taskStartTime` | `:61` | 段预算起点（`U-20(f)` 每段重置） | `06`／`M5` |
| 8 | `KingdomTask.source` | `KingdomTask.cs:27` | **任务持源引用**（`SourcePos` 依赖它） | `05`↔`06` 交界 |
| 9 | `KingdomTask.advertiser` | `:42` | **第二个源引用**（仅 `WaterHaul`；去重键 `D-1` 甲案） | 同 8 |
| 10 | `TaskStimulus.Issuer`／`.Source` | `AI.Core/Stimulus/StimulusTypes.cs:135-136` | **刺激持源引用**（`Source = issuer`）；载体 `AttentionSystem._taskStimuli:29` ⇒ **工人持源引用** | `05` 判据 4 关键 |
| 11 | `Building._forceHaulOnce` ＋ `RequestForceHaulOnce()` | `Building.cs:1497`／`:1499` | 管理器 → 建筑的**一次性"预定"标记**（玩家手点派搬运 · 入口读后即清 `:1522-1523`） | 双向越层（管理器写对象字段） |
| 12 | `Building.currentWorkers` | `Building.cs:156` | ⚠️ **字段在、零写入点**（全库仅 `RemoveAt` `:1447/:1448/:1469`，**无 `.Add`**）⇒ **死载体** | 名义配对、实为遗留 |
| 13 | ⭐ `ScheduleCenterStub._crewAssignments` | `AI/Schedule/ScheduleCenterStub.cs:56` | **第二处独立配对载体**：`机器(Unit/Building) → List<NPCBrain>`（战争机器乘员派发 · 续命/释放） | **不属于 `06`**（独立于 `TaskScheduler`） |
| 14 | `HaulToSiteArgs.site`／`.pickup` | `KingdomTask.cs:96`／`:98` | 任务 args 持**工地仓 ＋ 取料仓**引用（`ConstructionSiteStore`／`StorageComponent`） | `M1-C` 产物 |
| 15 | `HaulWaterArgs.target` | `KingdomTask.cs:117` | 任务 args 持**卸水落点仓**引用 | `M1-F` 产物 |
| 16 | `DemolishTaskArgs.target` | `KingdomTask.cs:127` | 任务 args 持**被拆建筑**引用 | `M1-C` 产物 |
| 17 | （反例）`NPCBrain.currentTask` | ⛔ **字段不存在**（全库仅注释／文档提及：`TaskState.cs:4`、`KingdomTask.cs:22`、`KingdomTask.cs:62`） | 工人**不持有 task 对象** | — |

### B-2 ⭐ **双方是否持有对方引用**（`05` 判据 4 核心）

| 方向 | 是否引用 | 证据 |
|---|---|---|
| **任务 → 源** | ✅ **持引用** | `KingdomTask.source`（`KingdomTask.cs:27`）＋ `advertiser`（`:42`）＋ 三个 `args` 内仓/建筑引用（`:96/:98/:117/:127`） |
| **工人 → 源** | ✅ **持引用** | `NPCBrain.AddTaskStimulus`（`:1849-1852`）→ `AttentionSystem._taskStimuli`（`:29`）持有 `TaskStimulus`（struct）；其 `Source`/`Issuer` ＝ 源对象（`StimulusTypes.cs:135-136`）；`TaskScheduler.InjectStimulus:417-422` 以 `issuer: task.source` 注入；`RemoveTaskStimulus` 按 `ReferenceEquals(_taskStimuli[i].Source, source)` 删除（`AttentionSystem.cs:132`） |
| **工人 → 任务对象** | ⛔ 不持 | 无 `currentTask` 字段；`KingdomTask` 仅存于管理器 `_npcTaskMap` |
| **源 → 工人** | ⛔ 不持 | `Building` 无 `worker/task` 字段；`currentWorkers` **零写入**（§B-1 #12）；`WorldGatherSource`/`ChestEntity` 只持坐标快照 |
| **管理器 → 双方** | ✅ 全持 | `_npcTaskMap`（task）＋ `_npcBrainMap`（brain）＋ `_sources`（源） |
| **第二处（乘员）** | ✅ **双向** | 机器 → 工人名单（`_crewAssignments:56`）＋ 工人 → 机器（`issuer: machine` `:192`/`:204`） |
| 判 | ⚠️ **判据 4「配对零引用」＝ 未达标**（`05` §六「双方零引用」现为**单向**：源不持工人，但**工人持源、任务持源**且 `WaterHaul` 持两个源） |  |

### B-3 同格／同源并发现在怎么处理（`05` §八「可多人」字段**有没有**）

| 项 | 实盘 |
|---|---|
| **对象侧「可多人」字段** | ⛔ **不存在**（无 `allowMulti`／`multi` 类字段；`Building`／`ChestEntity`／`StorageComponent` 均无） |
| 现等价机制（**均在管理器侧**） | `TaskScheduler.maxWorkersPerTask`（`:52`，场景序列化字段 · 默认 8）＋ `RequiredWorkers`（`:1288`，`ceil(total/carry)` clamp）＋ `RemainingSlots`（`:1299`）＋ 独占去重 `HasAssignedTaskForSourceType:1267`（键＝`advertiser ?? source` ＋ 类型）＋ `CountAssignedForType:1278` |
| 在岗判定 | `HasWorkerAssigned(source)`（`:156`）＝ 源匹配 ∧ 状态 `== Working`（**秒级窗口**） |
| 同格并发（多 NPC 站同格/同源） | 无对象侧字段；`Transport` 走容量派 N 人（`Tick:320`），其余类型**独占 1 人**（`:320` `slots = 1`）；同 tick 双发由各源自限（`MineByproductComponent:179` 注） |
| 判 | ⚠️ **`05` §八 两字段（`可多人=false/true`）＝ 未落地**；现为**调度器全局阈值 + 类型判**，对象无声明位 |

### B-4 ⭐ **结论句**：`M2-B` 若开工，**哪些改动会落进 `06`／`TaskScheduler` 的域**（⛔ 不出方案 · 只列"会碰到什么"）

| # | 会碰到的面 | 落点（现状坐标） | 与 `M5`／`M6` 的关系 |
|---|---|---|---|
| 1 | **配对表**（`05` §六「配对只在管理器」） | `_npcTaskMap`／`_npcStateMap`／`_npcBrainMap`（`TaskScheduler.cs:56-58`）＋ `Dispatch:390-410`／`ClearNpc:716-724` | ⭐ **`06` §六 明文同物** ⇒ **正面重叠** |
| 2 | **「双方零引用」裁引用** | `KingdomTask.source`(`:27`)／`advertiser`(`:42`)／`TaskStimulus.Source=issuer`（`StimulusTypes.cs:136`）／`InjectStimulus`(`:417`)／`InjectCarryStimulus`(`:1156`) | ⭐ 同 1（`06` 判据 1「零引用」） ⇒ **同一函数必被两片改** |
| 3 | **执行＝一次交互的通用动作** | `BehaviorModule.WorkAt`／`L3CommandComputer.cs:75-83`／`BehaviorExecutor.ExecuteWorkAt:157` | ⚠️ 属 **`AI.Core`＋壳**（`M2-B` 域）：但 `AI.Core` 有 sim 镜像约束（`sim-sync`） |
| 4 | **「一个交互实例」** | `KingdomTask`（全类）／`WorkerTaskType`／`WorkerTask.CreateTask` | ⚠️ 与 1/2 同体（`KingdomTask` 即配对实体） |
| 5 | **完成回调** | `ExecuteCompletion:727-782`（`switch (task.type)`） | ⭐ 若取消 `KingdomTaskType` ⇒ 本 switch **必改**（`M5` 域） |
| 6 | **选择归管理器** | `Tick:283-343`（优先级排序 ＋ 池隔离 ＋ 容量）＋ `GetPriority:1306`／`ResolveBiasWeight:1357` | ⭐ `M5`（`06` §一「选择」） |
| 7 | **意外处置／超时／挂起** | `Abandon:700`／`AbandonReason:72-82`／`OnThreatSuspended:239`／`OnPathFailed:215` | ⭐ `M5`（`06` §五 异常表；原文 `ITaskScheduler` **保留并扩**） |
| 8 | **对象侧声明位**（`M2-A`） | 各实现者 `TryAdvertiseTask`（8 处）＋ `IsValid`（8 处） | ⚠️ 若声明位改形 ⇒ `TaskScheduler.Tick:294` 消费点同步 ⇒ 与 5/6 同函数族 |
| 9 | **第二处配对载体** | `ScheduleCenterStub._crewAssignments:56` ＋ `DispatchCrew:115-160` ＋ `AssignOrRenewCrew:179-208` | ⚠️ **游离于 `06`／`TaskScheduler` 之外**（`06` §七「⛔ 不要多个管理器」的现状违例） |

⛔ **本端不出方案**；上表仅列"会碰到什么"＋"与哪片重叠"。

---

## 三、件 C · **能力声明现状** ＋ 与 `10` 的边界

### C-1 现在「对象声明自己能做什么」的载体是什么

**载体现状 ＝ 每个实现者各自的 `TryAdvertiseTask(out KingdomTask)`（硬 if/else 分支序）**，无统一表、无能力名：

| # | 对象 | 声明口 | 声明了哪几种能力（现状语义） | 分支依据 |
|---|---|---|---|---|
| 1 | `Building` | `Building.cs:1516-1633` | ①能被拆 ②能生产 ③能被搬空 ④**缺水时能求水**（主动广告 `WaterHaul`） | `_demolishing` → `state` → `forceHaul` → ②搬水前置 → ③生产 → ④搬运（顺序硬编码，`D-2` 乙案后搬水前置） |
| 2 | `UnitController` | `UnitController.cs:1023-1038` | ①**能被装填**（弹仓有缺口） | `HasMagazineSpace` → `PickReloadPlan`（石弹→火弹→魔弹优先序） |
| 3 | `ChestEntity` | `ChestEntity.cs:131-146` | ①**能被搬空**（箱＝仓 · 无主先到先得） | `_store.PrimaryStoredType()`（资源表序） |
| 4 | `WorldGatherSource` | `WorldGatherSource.cs:105-118` | ①**能被采集**（四型同源：`Tree/WoodPile/StonePile/OreVein`） | `TryResourceOf` 映射（`:69-79`） |
| 5 | `ConstructionSiteStore` | `ConstructionSiteStore.cs:168-198` | ①**能被投料**（工地仓缺料） | 资源表序首个缺口 → `FindPickup`（同国收该资源最近仓） |
| 6 | `MineByproductComponent` | `MineByproductComponent.cs:173-217` | ①能生产（无在岗时）②**能被搬空**（三子仓达标） | `HasWorkerOnDuty` → 水晶/火油/矿石顺序 |
| 7 | `BlacksmithBuilding` | `BlacksmithBuilding.cs:~85-95` | ①能生产（无在岗时） | `_building.IsValid` ＋ `HasWorkerAssigned` |
| 8 | `SiegeWorkshopBuilding` | `SiegeWorkshopBuilding.cs:261-271` | ①能生产（无在岗时） | `_stores.Count < 3` 守卫 ＋ 在岗 |
| 9 | `WorkerTask.DebugTaskSource` | `WorkerTask.cs:64` | ⛔ 无（恒 false） | — |

**辅助声明面**：

| 口 | 位置 | 声明了什么 |
|---|---|---|
| `IsValid` | 8 处（`Building:1489`／`UnitController:1017`／`ChestEntity:119`／`WorldGatherSource:96`／`ConstructionSiteStore:163`／`MineByproductComponent:166`／`Blacksmith:77`／`Siege:253`） | 「我此刻还是合法源」——其中 **6 处首项 `this != null`**（`DZ-4` 已列报面） |
| `OnRegister`／`OnUnregister` | 全部 8 处**空实现**（`{ }`） | 无声明语义（挂/卸钩子空转） |
| 自建任务广播点 | `Building.cs:1660-1677 RegisterWithTaskScheduler`／`EnsureRegistered`（`:1679+`）／`SiegeWorkshopBuilding.LazyRegister:273-278`／`ChestManager` 注册钩子 | 「把自己登记进 `_sources`」 |
| 玩家手点侧 | `ChestEntity.Interact:163`（⇒ `RequestHaulNow`）／`Building.Demolish:885`／`ResourceRespawnSystem.ConfirmResourceGather:475` | 「点击 ＝ 立案一个任务」（**非能力声明**） |

### C-2 ⭐ 与 `10_建筑能力表.md` 的边界（**只报事实**）

| 事实 | 读数 |
|---|---|
| `10` 是否已有**已落地的能力声明载体** | ⛔ **没有**。全库 `BuildingAbilityCatalog` **0 命中**；`ability`／`provides` 在生产码仅命中**无关文件**（`ResourceGenConfig.cs`／`Disaster/PortalDisaster*.cs`） |
| `10` 的落地形态现状 | 文档自陈「⏳ **能力表的落地形态**：C# 静态表（`BuildingAbilityCatalog`）—— **归执行端设计**」（`10_建筑能力表.md:269` §八-5）；§十 登记表为**文档表**，标注「批 3 施工时逐行搬进代码」 |
| `05` 的交互表是否已落地 | ⛔ **没有**（无交互表载体；§四 唯一权威表为文档表） |
| `M2-A` 计划写的「与 `08`／`10` **同一张表**」 | `中层执行计划.md:206`：`M2-A` 内容含「⭐ **与 `08`／`10` 同一张表**」 |
| `M6`（`10`）时点 | `中层执行计划.md:133`：`M6` 前置 ＝ **`M1`＋`M2`＋`M3`＋`M4` 全齐**；`:320` 风险栏「**必须最后**」 |
| ⇒ **事实结论** | ⭐ **`10` 现在没有任何代码载体可供 `M2-A` 挂靠** ⇒ `M2-A` 若"与 `10` 同一张表"，当下**无同表可挂**（⚠️ 这是**开片前必须拍的分界**，⛔ 本端只报事实、不出方案） |

---

## 四、件 D · **迁移面计数**（只报数 · 供切子片）

| 对象 | 原始匹配（含注释） | 注释行 | **代码面** | 生产码代码面 | Editor 代码面 |
|---|---|---|---|---|---|
| `ITaskSource`（含实现者声明句） | **18 文件 / 58 处** | 21 处（13 文件） | **17 文件 / 37 处** | **12 文件 / 30 处** | 5 文件 / 7 处 |
| `KingdomTaskType` | **20 文件 / 95 处** | 8 处（6 文件） | **18 文件 / 87 处** | **13 文件 / 44 处** | 5 文件 / 43 处 |
| `WorkerTask`（`\bWorkerTask\b`，⛔ 不含 `IWorkerTaskExecutor`） | 6 文件 / 16 处 | 12 处（6 文件） | **4 处**（`WorkerTask.cs:25` 类声明 ＋ `TaskScheduler.cs:1093/1293` ＋ `AIDebugSpawnController.cs:345`） | 3 处 | 1 处 |
| `WorkerTaskType` | 2 文件 / 6 处 | 0 | **2 文件 / 6 处**（`WorkerTask.cs:15/42/47/49` ＋ `AIDebugSpawnController.cs:340/357`） | 4 处 | 2 处 |
| `IWorkerTaskExecutor`（参考 · P0 未实现接口） | 4 文件（`IWorkerTaskExecutor.cs` ＋ `SatietySystem.cs:12`／`RanchSystem.cs:11`／`TrainingSystem.cs:9` **全为注释/文档**） | 4 | **1 文件 / 1 处**（接口声明 `IWorkerTaskExecutor.cs:11` · `CurrentTask:17`） | 1 | 0 |
| `TaskScheduler`（类 · 含注释） | **59 文件 / 321 处** | 106 处（44 文件） | **45 文件 / 215 处** | **25 文件 / 77 处** | 20 文件 / 138 处 |

⚠️ 口径：以上为 `rg` 标识符匹配（**含注释行已单列并扣除**）；`ITaskSource` 的"实现者声明句"与"接口引用"未再区分（实现者 9 处声明已列于 §A-1）。

---

## 五、件 E · `05` §十一 **判据 8 条现状表**（开片前基线）

| # | 判据 | 现状 | 依据 |
|---|---|---|---|
| 1 | **无交互枚举**（全库 grep） | ⛔ **未达标** | 存在**三个**枚举：`KingdomTaskType`（**11 项** · `TaskPriorityConfig.cs:32`）／`WorkerTaskType`（2 项 · `WorkerTask.cs:15`）／`InteractionKind`（4 项 · `IInteractable.cs:15`，⚠️ 语义域＝**玩家点击**派发）。⚠️ 是否把 `InteractionKind` 计入射程 ＝ **待裁**（§六-6） |
| 2 | **两端各声明**（下层**无第三份**） | ⛔ **未达标** | 现**只有源侧一份声明**（`TryAdvertiseTask` ×8）；工人侧无"能力声明"，仅有**职业白名单硬编码** `{Worker, Civilian, Porter}`（`TaskScheduler.cs:275-277`）＋ `IsIdleForTask`（`NPCBrain.cs:127-135`）；配对/能力交集**没有第三份统一表**（无交互表载体） |
| 3 | **进度写死**（在交互表里） | ⛔ **未达标** | **交互表本身不存在**（无表载体，§C-2）；"什么叫完成"散在代码：`TaskScheduler.GetTaskDuration:1485`（`GatherTaskArgs.gatherSeconds`／`DemolishTaskArgs`）＋ `ExecuteCompletion:727`（按类型分支） |
| 4 | **配对零引用**（一方消失不悬空） | ⛔ **未达标** | §B-2：**工人持源**（`TaskStimulus.Source=issuer` · `StimulusTypes.cs:136` · `AttentionSystem.cs:29`）＋ **任务持源**（`KingdomTask.source:27`／`advertiser:42`／`args.site/pickup/target`）；源**不**持工人（单向）⇒ 源消失时工人侧靠 `TaskStimulus.expiry(5s)` ＋ 管理器 `Abandon(SourceInvalid)` 兜，**非零引用** |
| 5 | **选择归任务管理器** | ⚠️ **部分达标（口径需裁）** | ✅ 管理器按优先级/池隔离**选任务**（`Tick:305-339`）；⛔ 但「一个对象的多项能力中**选哪一项**」＝ **源侧 `TryAdvertiseTask` 硬分支序**（`Building.cs:1527/1537/1550/1578/1603/1615`；`MineByproductComponent.cs:180-192`）⇒ 与判据字面「由任务管理器指定执行哪一项」**不符** |
| 6 | **时间只有耗时** | ⚠️ **部分达标／须裁** | 交互链内确无"时间数据"（无时间字段写到对象）；但管理器侧存在 **4 类时间量**：`tickInterval`(`:39`)／`taskExpiry`(`:41`)／`workDuration`(`:43`)／`taskTimeout`(`:45`)＋ `_suspendStartTime`／`_workStartTime`／`_taskStartTime`（`:59-61`）。⚠️ 这些属 **`06` 任务层域**（`06` §四 明文有 `deadline`／超时）⇒ 是否算"交互涉及的时间"＝ **待裁** |
| 7 | **位置不参与交换**（双方坐标不变） | ✅ **已达标（码面无违例）** | 全链路未发现任一交互写**对方**坐标：`UnloadInventory`／`DepositWaterToFarm`／`DepositToSite`／`UnloadAmmoToMagazine` 只写仓/弹仓数据；`KingdomTask.destPos` 写的是**任务自身**的落点字段（`ResolveChestDest:966`），非改他方坐标 |
| 8 | **回调节口**（双方各自处理自己的事） | ⛔ **未达标** | 完成回调 **唯一实现方 ＝ 管理器** `TaskScheduler.ExecuteCompletion:727-782`：由它 `switch` 任务类型**代劳**目标侧变化（`prod.Tick()`／`inv.TryStore`／`AddGatherOverflow` 分流国库·台账）。⭐ 唯一"目标侧自处理"实例 ＝ `WorldGatherSource.OnGatherCompletion:124`（格翻 Plain）⇒ 现状为**管理器代劳 ＋ 一点自处理**，非"双方各自处理" |

---

## 六、§待裁（**6 条**）

| # | 事项 | 本端实读事实（⛔ 不含方案） |
|---|---|---|
| 1 | ⭐ **`05` §十 三行已不成立／漂移** | 第 1 行（7→**8** 实体源）· 第 2 行（12→**11** 项）· 第 5 行（51/13→**54/15**）· 节末「真跑通交互只有一种」（例证 `HarvestCarry` **已删**，现 ≥6 条在跑）。⛔ **本端不改文档** ⇒ 修订归策划端 |
| 2 | ⭐ **`M2-B` 与 `M5`／`M6` 的域切分** | §B-4 列 **9 项**会碰面，其中 **第 1/2/5/6/7 项与 `06` 明文同物**（`06` §六 配对表 ＝ `_npcTaskMap`；`06` 判据 1 零引用；`06` §五 异常表 ＝ `AbandonReason` 族）⇒ **同一函数必被两片改**（编队/裁剪须策划端定序） |
| 3 | ⭐ **「第二处配对口」是否算第二处"真跑通交互"** | `ScheduleCenterStub.DispatchCrew`（`Update:82` → `:115`）＋ `_crewAssignments`（`:56`）＝ **独立于 `TaskScheduler` 的机器↔工人配对**（`issuer: machine` 双向引用 · `:192/:204`），且**在役**（`Update` 调用；`06` §七「⛔ 不要多个管理器」的现状违例）。⚠️ 但它**不交换数据**（无 读/改 对方数据）⇒ 本端**不判**它是否属"交互" |
| 4 | ⚠️ **工作区既存未提交改动**（非本批产物） | `TaskScheduler.cs:379-384` 相对 `HEAD` 有 **1 处未提交改动**：`RequestHaulNow(StorageComponent)` 无父建筑分支由「`LogWarning` 后返回」**回退为 `st.Harvest()`**（`git diff` 实读）；该形态与 `M1-G-1c` 件G-2（`D826` §五「⛔ 不落国库」）**相抵触** ⇒ 请策划端知悉/裁处（⛔ 本端未动） |
| 5 | ⚠️ **判据 1 的射程** | 全库另有 `InteractionKind`（`IInteractable.cs:15`，玩家点击派发）⇒ 若"交互枚举"含它，判据 1 的字面口径须写明（本端只报事实） |
| 6 | ⚠️ **`WorkerTask.DebugTaskSource` 与 AIDebug 入口** | 调试源（`WorkerTask.cs:58`）＋ 唯一调用方 `AIDebugSpawnController.cs:340/357` 属 `M2-B`（"一个交互实例"）射程内外未定 |

---

## 七、⛔ **零代码改动声明**（`git status` 证据）

**本批（`HH.324`）本端动作 ＝ 纯只读 ＋ 新建本报告 1 个文件**：

- ⛔ 未写生产码 · ⛔ 未改资产（`.asset`／`.unity`／`.meta`）· ⛔ 未动 `GameScene.unity`／`TaskScheduler.cs`／`Packages`／美术脏点
- ⛔ 未改任何 `最高优先级文档/**`（含 `05`：发现过时亦**只报不改**）

**`git status --short`（读于 2026-09-24 · 与开局快照逐字一致）**：

- 既存 `M`（**前序/并行产物，非本批**）：`.gitignore`｜`Valley Rampart/Assets/Scenes/GameScene.unity`｜`Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs`｜`Valley Rampart/Packages/manifest.json`｜`Valley Rampart/Packages/packages-lock.json`｜`pixel-forge/index.html`｜`pixel-forge/server/forge.mjs`｜`pixel-forge/server/serve.mjs`｜前序报告 `多Agent交接/执行端/HH.314…`／`HH.315…`（4 件）｜`河谷防线开发计划书具体内容/3.6…`／`3.8…`｜`美术资源文件/.../*.png`（5 张）
- 既存 `??`（**历史未跟踪面**）：`.codebuddy/`｜`Logs/`｜`Active`｜`Temp/`｜`Output/`｜`pixel-forge/**`（`server/.tmp-*` 系列）｜`Editor/Smoke/Valley_HH289_EcoProbe.cs`／`HH290_GatherProbe.cs` 等
- **本批新增**：仅 `多Agent交接/执行端/HH.324_M2开片前实核_交付报告.md`（＋ 记忆文件，非仓内产物）
- ⚠️ 逐文件核对方法：`git status --short` ＋ `git --no-pager diff -- <file>`；⭐ 上表 `TaskScheduler.cs` 的改动**先于本批存在**（开局快照即在列），其内容见 §六-4

---

## 八、停手条件命中情况

| 条件 | 命中 | 处置 |
|---|---|---|
| ① `05` §十 **有行已不成立** | ✅ **命中** | **照实报**（§A-1／§A-2／§A-5／§A-6）· ⛔ **未自行改文档** |
| ② `M2-B` 与 `M5`／`M6` 域**切不开** | ⚠️ **部分命中** | §B-4 第 1/2/5/6/7 项与 `06` 明文同物（**同一函数必被两片改**）⇒ §六-2 列报（切分归策划端） |
| ③ 发现**第二处"真跑通交互"** | ⚠️ **命中（但须裁定义）** | §A-6 表：**≥6 条**在跑交互链（原"只有一种"已不成立）；另 §B-1 #13 `DispatchCrew` 为**第二处配对/派发**（非数据交换）⇒ §六-3 列报 |

---

## 九、附：本批读数索引（供复核）

| 主题 | 关键锚点 |
|---|---|
| `ITaskSource` 实现者 | `Building.cs:29`／`UnitController.cs:24`／`ChestEntity.cs:18`／`WorldGatherSource.cs:36`／`ConstructionSiteStore.cs:23`／`MineByproductComponent.cs:30`／`BlacksmithBuilding.cs:12`／`SiegeWorkshopBuilding.cs:19`／`WorkerTask.cs:58` |
| 枚举 | `TaskPriorityConfig.cs:32-47`（`KingdomTaskType` 11）／`WorkerTask.cs:15-19`（`WorkerTaskType` 2）／`IInteractable.cs:15`（`InteractionKind` 4）／`TaskState.cs:7-16`（`TaskState` 7） |
| 配对载体 | `TaskScheduler.cs:55-61`／`KingdomTask.cs:27,42,96,98,117,127`／`StimulusTypes.cs:135-136`／`AttentionSystem.cs:29,127-136`／`Building.cs:156,1497`／`ScheduleCenterStub.cs:56,115,179-208` |
| 完成回调 | `TaskScheduler.cs:727-782`（唯一）＋ `WorldGatherSource.cs:124`（目标侧自处理唯一实例） |
| `WorkAt` | `BehaviorModule.cs:17`／`L3CommandComputer.cs:75-83`／`BehaviorExecutor.cs:157-184` |
| `M2` 计划 | `中层执行计划.md:198-213`（§4.2）／`:129`（片表）／`:133`（`M6` 前置）／`:301`（交付物） |
| `10` 现状 | `10_建筑能力表.md:269`（落地形态待设计）／`:297-401`（§十 29 栋表）／`:416`（不许） |
| 原文过时例证 | `StorageComponent.cs:352`（`HarvestCarry` 墓碑）／`TaskScheduler.cs:740` |
