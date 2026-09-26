# `HH.337` `M5` 开工前实核报告（**只读批**）

- **编号**：**`HH.337`**（承 `HH.336` 收口后；⛔ 未写策划端账本 —— 应登记项见 §七）
- **依据**：`D868` 后签发 · 本批任务书（两阶段：HH.336 收口 ＋ `M5` 开工前只读实核）
- **性质**：**只读实核**（⛔ 零代码改动 · ⛔ 零资产改动 · ⛔ 未建 `BuildingAbilityCatalog` · ⛔ 未动 `TaskScheduler`）
- **基线**：`76452c01`（本批第一阶段提交：HH.336 收口）
- **日期**：2026-09-25
- **硬停遵守**：⛔ 不提出施工方案（§六只列事实与边界）· ⛔ 不采旧报告数字（下表全部**本次现读**，与文档记载的差异逐项对账）· ⛔ 未把 `InteractionKind` 计入任务枚举 · ⛔ 未把 M5 的配对/枚举删除/完成回调塞进 M2 · 契约与代码不一致处**停在报告，未自行修正**（§五-列报）。

---

## 一、设计原文摘录位置

| 文档 | 节 | 行号 | 要点（原文关键词） |
|---|---|---|---|
| `最高优先级文档/05_交互层.md` | §四 交互表（唯一权威） | `:77-105` | 表 7 栏（交互ID/谁可以做/目标须提供/条件/进度/产物去哪/目标怎么变/耗时）；`:93` 「**现码 7 行**（`HH.326` 验收·依据 `D861`）」；`:95-103` **7 行现码表**（`worker_gather`／`worker_haul`／`worker_waterhaul`／`worker_haulsite`／`worker_ammoreload`／`worker_convert`／`worker_demolish` ＋ 完成规则/耗时） |
| 同上 | §六 三层分工 | `:129-155` | 下层出能力交集（菜单）／任务管理器派项（点菜）／下层执行；⭐ `:139-153` **配对只在管理器（双方零引用）** `D758` 原话；⚠️ `:153` 「预定/配对**不是对象身上的字段** ⇒ **对象侧零字段**」 |
| 同上 | §八 多人协作 | `:177-195` | `可多人` 是字段（对象侧声明）；进度＝对象自身数据 ⇒ 不造进度条；⚠️ 原子性/阶段顺序**不在交互层** |
| 同上 | §十一 判据（8 条） | `:225-235` | ①无交互枚举（**`InteractionKind` 不计入**）②两端各声明 ③进度写死 ④配对零引用 ⑤选择归任务管理器 ⑥时间只有耗时 ⑦位置不参与交换 ⑧回调节口 |
| 同上 | §十 与现有代码的对齐 | `:211-221` | `ITaskSource` 8 实体源＋1 调试源／`KingdomTaskType` 11 项（5 项零构造）／`WorkerTask` 仍只有 `Gather`/`Transport`／`ITaskScheduler` 10 方法／类调用面 **54 处／15 文件** |
| `最高优先级文档/06_任务层.md` | §三 任务卡片（五组） | `:59-70` | 身份（`taskId`/`issuer`/`priority`）／⭐ 归属方（**属于哪个王国/玩家**）／做什么（`ability`/`targetRef`**不是引用**/`count`）／条件与进度（`precondition`/`progressSpec`/`duration`）／结果（`rewardTo`/`targetEffect`）／执行中（`state`/`workerId`/`workProgress`/`deadline`）／意外（`retry`/`onFail`） |
| 同上 | §四 全生命周期（八阶段） | `:73-112` | `[0]生→[1]待派→[2]已派→[3]执行中→[4]判定→[5]完成／[6]终止／[7]读档`；`:108-111` **同工任务仲裁（`D814`）**＋ 欠账 `U-19` |
| 同上 | §五 异常→处理对齐表 | `:113-124` | ✅已有 4（NPC死/建筑死/威胁抢占/外部撤回）；❌**新增 4**（目标变了／目标用尽／工人到不了⇒解绑＋**标记不可达**／读档重验） |
| 同上 | §六 配对与预定（同生共死） | `:126-134` | 配对表＝**唯一真源**；任务亡 ⇒ 预定**立刻释放** ＋ **预定带超时兜底** |
| 同上 | §七 扩展（单管理器＋分域） | `:136-143` | 分域＝**按大区块（或王国）分桶**；节流沿用 `tickInterval=1s`；⛔ 不要多管理器 |
| 同上 | §八 与现有代码的对齐（**迁移三步**） | `:144-154` | ①**并存**→②**切换**→③**清理**（`:154`）；参数沿用 `workDuration`/`taskExpiry`/`taskTimeout`/`maxWorkersPerTask` |
| 同上 | §九 判据（7 条） | `:156-165` | ①零引用 ②目标可判（下一 tick 内）③配对不悬空 ④不居中态 ⑤读档重验 ⑥单管理器 ⑦不碰地图数据 |
| 同上 | §十 待议 | `:168-177` | `-1` 已闭；**`-2` 优先级由谁定**／**`-3` `retry` 默认次数与"不可达"冷却**／**`-4` 分域粒度（大区块 vs 王国）**／**`-5` 统计/日志粒度** ← ＝本报告 §四四个待裁 |
| `最高优先级文档/中层执行计划.md` | §4.5 `M5` | `:259-274` | 子片 `M5-A` 卡片与生命周期／`M5-B` 配对与预定／`M5-C` 迁移三步／`M5-D` 枚举取消；交付物＝迁移三步逐步报告＋调用面改后分布＋四类异常处置读数；`:274` **待拍＝分域粒度／优先级归谁定** |
| 同上 | §3.2 施工片表 | `:132` | `M5` 依赖 **`M2`（交互表）**；风险中（记载 `HH.324`：生产码 54 处／15 文件） |

---

## 二、八项实读（每项：结论 ＋ `file:line`；★＝与文档记载对账）

### 2.1 `ITaskSource` 实际实现者数量、引用面、能力来源 ★

- **实现者数量 = 9 个类**（现读 pattern=`class\s+\w+[^\n]*ITaskSource`，域=`Assets`，命中 9 行/9 文件；`Editor` 内 0 个）＝ **8 实体源 ＋ 1 调试源** ★与 `05 §十 :215`「8 实体源＋1 调试源」**现读吻合**。
- 接口定义 `..._Game/Systems/AI/Tasks/KingdomTask.cs:60`；成员 5：`IsValid:63`／`SourcePos:66`／`TryAdvertiseTask:69`／`OnRegister:72`／`OnUnregister:75`。
- 逐个实现者 ＋ **能力来源**（＝其 `TryAdvertiseTask` 内产出的 `KingdomTaskType` 与读的字段）：

| # | 实现者 | 定义 | 广告产出（构造点） | 能力来源（读什么） |
|---|---|---|---|---|
| 1 | `Building` | `Systems/Building/Building.cs:29` | Build(拆除)`:1529`／Transport(强制)`:1553`／WaterHaul`:1586`／Production`:1608`／Transport(阈值)`:1621` | 本体 `ProducerComponent`/`StorageComponent`（`:1543-1544`）＋同国水井全扫（`:1641-1658`） |
| 2 | `ConstructionSiteStore` | `Systems/Building/ConstructionSiteStore.cs:23` | Build(搬料·`HaulToSiteArgs`)`:187` | 工地缺口 `Remaining()`＋同国取料仓 `FindPickup:208` |
| 3 | `WorldGatherSource` | `Systems/World/WorldGatherSource.cs:36` | Gather`:109`（`destType=Treasury:110`） | 数据格＋地表物 `PointStillThere():133`；per-命令 `KingdomId:39` |
| 4 | `ChestEntity` | `Systems/World/ChestEntity.cs:18` | Transport`:138`（`destType=None:139`） | 箱容器 `_store`（一箱一源） |
| 5 | `UnitController` | `Systems/Unit/UnitController.cs:24` | AmmoReload`:1029`（`destType=UnitMagazine:1030`） | 自身弹仓缺口 `HasMagazineSpace():1002`／`PickReloadPlan():1043` |
| 6 | `SiegeWorkshopBuilding` | `Systems/Kingdom/SiegeWorkshopBuilding.cs:19` | Production`:268` | 组件自持 `_stores`（≥3 子仓） |
| 7 | `BlacksmithBuilding` | `Systems/Kingdom/BlacksmithBuilding.cs:12` | Production`:92` | 组件自持 `_storage` |
| 8 | `MineByproductComponent` | `Systems/Building/MineByproductComponent.cs:30` | Production`:182`／Transport`:205` | 副产三仓 `:189-191` |
| 9 | `WorkerTask.DebugTaskSource`（**私有嵌套类**） | `Systems/Unit/WorkerTask.cs:58` | **无**（恒 `false`）`:64` | 仅调试坐标；**从不注册**（唯一构造 `WorkerTask.cs:45`，入口 `AIDebugSpawnController.cs:345`） |

- **引用面（谁持有/注册）**：唯一注册表 ＝ `TaskScheduler._sources`（`HashSet<ITaskSource>`）`TaskScheduler.cs:55`；写 `:139`/`:145`/`:259`；读 `:113`/`:254`/`:291`/`:367`。注册调用点 19 行/12 文件（域=`_Game`），代表：`BuildingFactory.cs:191`、`Building.cs:559`/`:1676`/`:1687`、`ChestManager.cs:92`、`UnitController.cs:435`、`WorldGatherRegistry.cs:119`、`ResourceRespawnSystem.cs:487`、`SiegeWorkshopBuilding.cs:276`、`BlacksmithBuilding.cs:100`、`MineByproductComponent.cs:248`。旁路持有：`WorldGatherRegistry._byKingdom` `WorldGatherRegistry.cs:30`。

### 2.2 `KingdomTaskType` 枚举项、生产构造点、零构造项 ★

- 定义 `_Game/Data/TaskPriorityConfig.cs:32`；**枚举项 = 11 项**（`:34 Repair`／`:35 Build`／`:36 Produce`／`:37 Transport`／`:38 Rancher`／`:39 WaterCarry`／`:40 GoldMine`／`:42 Production`／`:43 WaterHaul`／`:44 Gather`／`:46 AmmoReload`）★与 `05 §十 :216`「11 项」**现读吻合**。
- **生产码构造点 = 14 行／9 文件**（pattern=`new KingdomTask\(`，域=`_Game`；域=`Assets` 另 14 行/4 文件属 Editor 冒烟）：
  `Build` ← `ConstructionSiteStore.cs:187`／`Building.cs:1529`｜`Transport` ← `Building.cs:1553`／`Building.cs:1621`／`ChestEntity.cs:138`／`MineByproductComponent.cs:205`｜`Production` ← `Building.cs:1608`／`BlacksmithBuilding.cs:92`／`SiegeWorkshopBuilding.cs:268`／`MineByproductComponent.cs:182`｜`WaterHaul` ← `Building.cs:1586`｜`Gather` ← `WorldGatherSource.cs:109`｜`AmmoReload` ← `UnitController.cs:1029`。
- **零构造项（域=`_Game`）= 5 项**：**`Repair`／`Produce`／`Rancher`／`WaterCarry`／`GoldMine`** ★与 `05 §十 :216`「5 项生产码零构造」**现读吻合**。
  - `KingdomTaskType.Repair` 域=`_Game` 命中 **0**；`KingdomTaskType.Produce` 域=`_Game` 命中 **0**；`Rancher`/`WaterCarry`/`GoldMine` 各 1 行且均为 **SO 配置表条目**（`Data/Kingdoms/ResourceBiasConfig.cs:63/64/65`），**非任务构造**。
- ⚠️ 口径：`InteractionKind` **未计入**（本项统计只用 `enum KingdomTaskType` 成员；`05 §十一-1 :227` 亦明示「`InteractionKind` 不计入本条」）。

### 2.3 `ITaskScheduler` 方法数与生产调用面 ★

- 定义 `Systems/AI/TaskScheduling/ITaskScheduler.cs:8`；**方法数 = 10**（`:11 Register`／`:14 Unregister`／`:17 GetWorkerState`／`:20 HasWorkerAssigned`／`:23 CountAssignedWorkers`／`:26 AbandonTask`／`:29 OnNpcDied`／`:32 OnBuildingDied`／`:35 OnThreatSuspended`／`:38 OnThreatResumed`）★与记载「10 方法」吻合。
- 实现者唯一 ＝ `TaskScheduler`（`TaskScheduler.cs:32` `class TaskScheduler : Singleton<TaskScheduler>, ITaskScheduler`）；10 实现体 `:136/142/150/156/170/197/204/224/239/244`。
- ⭐ **`ITaskScheduler` 类型的调用点 = 0**（全仓命中 3 行/2 文件：定义 `ITaskScheduler.cs:8` ＋ 实现声明 `TaskScheduler.cs:32` ＋ 注释小标题 `TaskScheduler.cs:134`）⇒ **契约面零外部消费**；生产侧一律走具体类单例。
- **类调用面（现读）= `TaskScheduler.Instance|HasInstance` 54 行／15 文件**（域=`_Game`）★与 `05 §十 :219`／`中层执行计划 §4.5 :261` 记载「**54 处／15 文件**」**逐数吻合**；另 `TaskScheduler.`（含 XML 注释）96 行/27 文件。

### 2.4 配对表、预定表、任务生命周期实际由谁持有；双方是否持有对方引用

- 任务对象**真名 = `KingdomTask`**（`KingdomTask.cs:24`）；⚠️ `WorkerTask` **不是**任务对象，是 static 工厂（`WorkerTask.cs:25`）。
- `KingdomTask` 字段 7：`type:26`／`source:27`／`destType:28`／`destPos:29`／`args:30`／`intensity:31`／`advertiser:42`；只读属性 `SourcePos:53`；构造 `:44-50`。**无 `priority` 字段**（见 2.5）。
- **配对表/预定表实体**（全在 `TaskScheduler.cs`）：`_sources:55`／`_npcTaskMap:56`（`int→KingdomTask`）／`_npcStateMap:57`（`int→TaskState`）／`_npcBrainMap:58`（`int→NPCBrain`）／`_suspendStartTime:59`／`_workStartTime:60`／`_taskStartTime:61`；**无 `_pending*`**（任务域 0 命中）。
- **第二处配对**＝`ScheduleCenterStub._crewAssignments` `Systems/AI/Schedule/ScheduleCenterStub.cs:56`（`object→List<NPCBrain>`；在役 `DispatchCrew()` `:115`，`Update` 调用 `:82`）。
- 生命周期实际由 **`TaskScheduler`** 持有与推进：创建 `KingdomTask:44`／派发 `Dispatch:392`（写表 `:397-402`）／推进 `UpdateAssignedTasks:464`／完成 `Complete:667`→`ExecuteCompletion:729`／放弃 `Abandon:702`（8 值 `AbandonReason:72-82`）。
- ⭐ **双方是否持有对方引用 —— 是（与 `05 §六 :139-153`「双方零引用」相违）**：
  - **任务 → 源**：`KingdomTask.source`（`KingdomTask.cs:27`）＋ `advertiser`（`:42`）；
  - **管理器 → 双方**：`_npcTaskMap`（工人 id → 任务）＋ `_npcBrainMap`（工人 id → `NPCBrain` 引用）；
  - **工人 → 任务**：`KingdomTask.cs:33-40` 注释自陈「任务被 `NPC.currentTask` 引用即视为占用（`KingdomTask.cs:22` DR-17）」；`KingdomTask` 无 `[Serializable]`、全库无类以字段/集合长期持有它（唯一长期容器＝`TaskScheduler._npcTaskMap`）。
  ⇒ 结论：**"对象侧零字段"未达成**（任务对象持源引用）；设计要的是 `targetRef`（坐标＋类型·`06 §三 :65`）。

### 2.5 任务优先级实际来自上层还是由 `TaskScheduler` 计算

- **由 `TaskScheduler` 自己算**（非上层给定、非广告侧携带）：`GetPriority(KingdomTaskType)` `TaskScheduler.cs:1308`（体 `:1310` 读 `_priorityConfig.Get(type)`，缺配回退 `TaskPriority.B`）；配置资产 `Resources/Config/TaskPriorityConfig.asset`（条目 `:15-31`；⚠️ **缺 `7/8/9`＝`Production`/`WaterHaul`/`Gather` ⇒ 回退 B**）；加载 `TaskScheduler.cs:89`；枚举 `TaskPriority{S=4,A=3,B=2,C=1}` `Systems/AI.Core/Stimulus/StimulusTypes.cs:19-25`。
- 消费点：刺激注入 `:420`／`:1159`；排序 `jobs.Sort(CompareByEffectivePriority):311`（首键 `EffectivePriority:1339`，`:1341 GetPriority`）。
- **第二层乘子（仍在调度器内算）**：`ResourceBiasConfig`（`Data/Kingdoms/ResourceBiasConfig.cs:43`；加载 `TaskScheduler.cs:94`；计算 `:1359-1379 ResolveBiasWeight`／`:1385-1393 ShortageRate`／`:1407-1425`）。
- 广告侧**不参与定价**（`TryAdvertiseTask` 只填 `type/destType/destPos/args/intensity`；`new KingdomTask(` 14 处均无优先级实参）。

### 2.6 分域现状：实际可用的王国/大区块归属字段

- **王国级**（在役）：`Building.kingdomId` `Systems/Building/Building.cs:128`／`UnitController.kingdomId` `Systems/Unit/UnitController.cs:46`／`WorldGatherSource.KingdomId` `Systems/World/WorldGatherSource.cs:39`（构造绑定 `:56`）／`TreasureVault.KingdomId` `Systems/Kingdom/TreasureVault.cs:41`／`KingdomBrain.kingdomId` `Systems/AI/KingdomBrain/KingdomBrain.cs:24`；存档镜像 `BuildingSaveData.cs:48`／`UnitController.cs:1847`。
- **大区块级**（存在但**任务域未用**）：`TerritorySystem._territory` `Systems/Kingdom/TerritorySystem.cs:21` ＝ `Dictionary<Vector2Int(mid), int(kingdomId)>`（自陈"唯一真源"`:8`；出口 `:42/:45/:48/:60`；写点 `:159/:183/:267/:333`）；粒度 SO `GridConfig.midChunkSize` `Data/GridConfig.cs:23`（默认 4）。
- `TaskScheduler` **只按王国分域**：`SourceKingdom` `:1451-1463`（`ChestEntity⇒-1`／`Building⇒b.kingdomId`／`WorldGatherSource⇒wg.KingdomId`／`Component⇒父建筑`；其余 `⇒0`）；同国闸 `:318`＋`:331`；卸货/落仓同国 `:934`／`:1239`。
- ⭐ **无大区块/区域过滤**：`TaskScheduler.cs` 对 `chunk|Chunk|midChunk` 命中 **0**；中区块级只在**选点层**用（`WorldGatherRegistry.cs:98`＋`:94`）。

### 2.7 `retry`、不可达冷却、统计日志是否存在

- `retry`：**不存在** —— `TaskScheduler.cs` 对 `retry|Retry` 命中 **0**（域全量仅 3 行/2 文件且非任务域：`Data/AttentionTuningConfig.cs:46/47` 的 `scheduleRetryInterval`＝**招工请求**重试间隔、`AI.Core/Config/TuningSnapshot.cs:39`）。
- **不可达冷却**：**不存在** —— `OnPathFailed` `:215-222` 仅 `Abandon(Unreachable)`（`:221`），**无标记表/无冷却**；`TaskScheduler` 无按源/按工人的失败计数（`_consecutiveFails` 在 `TaskScheduler.cs` 命中 0）。底层寻路**有**自己的计数/重寻冷却（**任务域之外**）：`PathFollower._consecutiveFails:23`（写 `:69/75/159/177`、读 `:180`）＋重寻冷却 `PathFollower.cs:109`（`mc.repathCooldownSeconds` 默认 1f）。
- **统计日志**：**只有逐事件日志，无聚合统计** —— `Debug.Log`/`LogWarning` 在 `TaskScheduler.cs` **10 行**（`:91`/`:384`/`:411`/`:676`/`:715`/`:806`/`:841`/`:846`/`:851`/`:945`）；任务域**无计数器字段**（`CountAssignedWorkers:170/184` 为即时遍历查询）。
- 附带（`06 §四` 相关）：四类调度钟**存在且在用** —— `tickInterval=1f:39`（闸 `:129`）／`taskExpiry=5f:41`（`:423`/`:1162`）／`workDuration=2f:43`（`:1492-1500`）／`taskTimeout=30f:45`（`:514`/`:638`）／`maxWorkersPerTask=8:52`（`:1297`）。

### 2.8 当前 7 行交互表载体 ＋ 事件输入面

- **7 行交互表载体 = 文档 `05_交互层.md:93-103`**（＋ `HH.326` 交付报告承载 file:line）；⭐ **码面零载体**：`worker_gather|worker_haul|worker_waterhaul|worker_haulsite|worker_ammoreload|worker_convert|worker_demolish` 在 `Assets` **0 命中**；`InteractionTable|交互表` 在 `Assets`（`.cs`/`.asset`）**0 命中**。★与 `05 §四 :93`「现码 7 行」＋ `中层执行计划 :212`「若落码，只读、零消费方改动」一致（**未落码**）。
- **`TaskScheduler` 事件订阅面 = 1 个**：`EventBus.Subscribe<PathFailedEvent>(OnPathFailed)` `:100`（注销 `:123`）。⭐ **未订阅 `MapReplacedEvent`**（该事件存在于 `Core/GameEvents.cs:506`，广播点 `World/MapGate.cs:319`）⇒ `06 §五`「目标变了 ⇒ 重验」**当前无输入**。

---

## 三、迁移三步（并存／切换／清理）现状映射

| 步 | 设计（`06 §八 :154`） | 现状（本次实读） | 标记 |
|---|---|---|---|
| ① **并存** | 新协议跑新东西，老源不动 | **未开始**：码面**无"新协议"载体** —— 交互表 0（§2.8）／能力声明无载体（`BuildingAbilityCatalog` 属 `M6`·`中层执行计划 :289`）／`KingdomTask` 仍以 `KingdomTaskType` 为键（`:26`）⇒ 并存期**当前无物可跑** | **缺口仍在** |
| ② **切换** | 老源逐个改造成能力声明 | **0/9 源已改造**：9 个实现者仍全走 `TryAdvertiseTask` ＋ `KingdomTaskType` 硬编码（§2.1/§2.2） | **缺口仍在** |
| ③ **清理** | 删枚举、删老源 | **未开始**：`KingdomTaskType` **11 项仍在**（其中 **5 项零构造**＝清理期现成候选，§2.2）；`WorkerTaskType`（`WorkerTask.cs:15`·`Gather:17`/`Transport:18`）仍在 | **缺口仍在** |

> ⚠️ 三步的**前置事实**：`M5` 只依赖 `M2`（`中层执行计划 :132`）；`M2` 已交「7 行表」（`HH.326`）且 `D861` 改裁「本片只出表」（`:204`）⇒ ①的载体需要**新协议**，而非再出表。

---

## 四、四个待裁事项的事实证据（`06 §十-2~-5`）

| # | 待裁项（`06 §十`） | 事实证据（本次现读） |
|---|---|---|
| **-2**（`:173`） | **优先级**由谁定（上层给？任务管理器算？） | ⭐ **现状＝任务管理器算**：`TaskScheduler.GetPriority:1308` 读 SO `TaskPriorityConfig.asset`（资产条目 `:15-31`，**缺 7/8/9 三项 ⇒ 回退 B**）；排序 `:311`＋`EffectivePriority:1339`；第二层乘子 `ResourceBiasConfig`（`:1359-1379`，输入 `SituationHub.EconomyBlock.Flow.Net/Out` `:1385-1393`）。广告侧无定价字段（`KingdomTask` 无 `priority`）。⛔ 未判"应归谁" |
| **-3**（`:174`） | **`retry` 默认次数**与"不可达"的冷却 | ⭐ **两者当前均不存在**：`TaskScheduler.cs` `retry|Retry|cooldown|Cooldown|冷却|重试` **0 命中**；路径失败仅 `Abandon(Unreachable)`（`:221`）无标记/冷却；唯一"重试相关"结构在任务域外（`PathFollower._consecutiveFails:23`＋`repathCooldownSeconds:109`） |
| **-4**（`:175`） | **分域粒度**（大区块 vs 王国）最终选哪个 | ⭐ **两条路都已有字段可用**：王国级 5 处字段（§2.6 前段）＋ `TaskScheduler` 已按王国闸（`:318/331`）；大区块级 `TerritorySystem._territory:21`（`midChunkSize` `GridConfig.cs:23`）＋选点层已用（`WorldGatherRegistry.cs:98/94`），但 **`TaskScheduler` chunk 命中 0**。⛔ 未选 |
| **-5**（`:176`） | 任务**统计/日志**的粒度 | ⭐ 现状＝**逐事件 10 行日志**（`TaskScheduler.cs:91/384/411/676/715/806/841/846/851/945`），**无聚合计数**、无统计字段；可用的现成读口仅 `GetWorkerState:17`／`HasWorkerAssigned:20`／`CountAssignedWorkers:23`（即时查询） |

---

## 五、逐条标记（已满足／缺口仍在／文档已过时／当前不可判）

### 5.1 实核项 1~7

| 项 | 标记 | 依据 |
|---|---|---|
| 1 实现者数量/引用面/能力来源 | 数量与引用面 **已满足**；**能力来源＝各自为政 ⇒ 缺口仍在** | §2.1（9 类·`_sources:55`；设计要"能力声明"，现为 `TryAdvertiseTask` 硬编码类型） |
| 2 `KingdomTaskType` 项数/构造点/零构造 | **已满足**（11 项／14 构造点·9 文件／零构造 5 项，与 `05 §十` 吻合） | §2.2 |
| 3 `ITaskScheduler` 方法数与调用面 | **已满足**（10 方法；**类型调用点 0**；类调用面 54 行/15 文件） | §2.3 |
| 4 配对/预定/生命周期归属；双向引用 | 归属 **已满足**（单管理器持有）；⭐ **双向引用 ⇒ 缺口仍在**（任务持源·工人持任务） | §2.4（与 `05 §六 :153` 相违） |
| 5 优先级来源 | **当前不可判（待裁 `06 §十-2`）**；事实＝管理器算 | §2.5／§四 |
| 6 分域现状 | 王国级 **已满足**；大区块 **缺口仍在**（字段有·调度器未用） | §2.6 |
| 7 `retry`/不可达冷却/统计日志 | **缺口仍在**（三者皆缺"存在"面） | §2.7 |

### 5.2 `05 §十一` 8 条（M5 归属：`中层执行计划 :210` 明示 1／2／4／5／8 归 M5）

| # 判据 | 标记 | 依据 |
|---|---|---|
| 1 无交互枚举 | **缺口仍在** | `KingdomTaskType` 11 项（§2.2）＋ `WorkerTaskType` 2 项（`WorkerTask.cs:15-18`） |
| 2 两端各声明 | **缺口仍在** | 现为"源单边广告 ＋ 调度器派工"，无"被交互者声明"面（§2.1） |
| 3 进度写死 | 文档侧 **已满足**（7 行表在 `05 §四:95-103`）；码面 **缺口仍在**（完成规则散在 `TaskScheduler:541`／`Building._demolishProgress` 等） | §2.8 |
| 4 配对零引用 | **缺口仍在** | §2.4 |
| 5 选择归任务管理器 | **已满足** | 派发唯一口 `Dispatch:392` |
| 6 时间只有耗时 | **已满足（文档口径）**：四钟归属已裁（`中层执行计划 :210`「四类调度钟归 `06`」），交互行只留"耗时"（`05 §四:95-103` 耗时列） | §2.7 末段 |
| 7 位置不参与交换 | **已满足（静态）**：调度只解析 `destPos`，未写坐标；⛔ 未取运行读数 | §2.4／运行读数未取 |
| 8 回调节口 | **缺口仍在** | 完成回调在调度器内（`Complete:667`→`ExecuteCompletion:729`），源侧无"各自处理"契约面 |

### 5.3 `06 §九` 7 条

| # 判据 | 标记 | 依据 |
|---|---|---|
| 1 零引用 | **缺口仍在** | §2.4 |
| 2 目标可判（下一 tick 内） | **部分已满足**：`IsValid:63` ＋ `_sources` 每 tick 清无效 `:254-259` ＋ 显式钩子 `OnBuildingDied:224`；⭐ **缺口**：`MapReplacedEvent` 未订阅 ⇒"目标变了"无输入 | §2.8 |
| 3 配对不悬空 | **部分**：无"配对表"实体；以 `_npcTaskMap`＋`ClearNpc:718-725` 达成"终止即清"；**无超时兜底**（`06 §六 :133` 要的"预定带超时"不存在） | §2.4／§2.7 |
| 4 不居中态 | **已满足（静态）**：`AbandonReason` 8 值（`:72-82`）＋ `TaskState` 4 态；⛔ 未取运行读数 | §2.4 |
| 5 读档重验 | **当前不可判（口径待裁）**：任务**不入档**（`KingdomTask` 无 `[Serializable]`·无字段持有者；TaskScheduling 域 `ISaveable|LoadState|Restore` **0 命中**）⇒ "重验"对象不存在 | §2.8 附 |
| 6 单管理器 | **已满足** | `TaskScheduler.cs:32` 单例；第二处配对 `ScheduleCenterStub` 为乘员派发（`:115`）非任务管理器 |
| 7 不碰地图数据 | **已满足（静态）** | `TaskScheduler.cs` 对 `MapGate|map.features|SetFeature|GetOccupant|.map` **0 命中** |

### 5.4 文档过时项（本次共检出 **1 条**，其余记载逐数吻合）

| 文件 | 位置 | 记载 | 现读 |
|---|---|---|---|
| `中层执行计划.md` | `:132`／`:261`「`ITaskSource` 实体源 **8** ＋ 调试源 1」 | 与现读一致 ✅ | — |
| 同上 | `:261`「`KingdomTaskType` 11 项」 | 一致 ✅ | — |
| 同上 | `:261`「`TaskScheduler` 54 处／15 文件」 | **54 行/15 文件** ✅（吻合） | — |
| `05_交互层.md` | `:219`「`ITaskScheduler` 10 方法」 | 一致 ✅ | — |
| ⚠️ **列报（唯一不一致，停在报告不修）** | `06 §八 :149` 写「`ITaskSource`（**7 个各自为政**）」；`06 §八 :150` 写「`KingdomTaskType`（**12 项枚举**）」 | 现读为 **9 类实现者**／**11 项** | ⛔ 未自行修正（`05 §十` 与 `中层执行计划` 均已勘正为 8＋1／11；`06 §八` 两处仍为旧数） |

---

## 六、下一张最小施工片建议（⛔ 只列事实与边界，不含方案）

**事实面**
1. `M5` 四子片见 `中层执行计划 :265-268`（A 卡片与生命周期／B 配对与预定／C 迁移三步／D 枚举取消）；交付物见 `:272`（三步逐步报告 ＋ 调用面改后分布 ＋ 四类异常处置读数）。
2. **迁移三步的 ① 并存需要"新协议载体"，而该载体当前不存在**（交互表码面 0／能力声明无载体；§三）。
3. `M5-D`（枚举取消）已有现成**清理期候选**：5 项零构造枚举（§2.2）；但"取消"的前置是能力名＋交互表（`05 §四` 文档侧已有 7 行，码面 0）。
4. `M5-B`（配对与预定）触碰点＝现码 `KingdomTask.source:27`／`advertiser:42`／`NPCBrain.currentTask`（`KingdomTask.cs:22/33-40`）＋第二处配对 `ScheduleCenterStub._crewAssignments:56`（`中层执行计划 :216` 明示留到 M5）。
5. `M5-A`（卡片五组／八阶段）是 B/D 的字段面前置（`06 §三 :59-70`／§四 `:73-112`）。
6. 四项待裁（§四）分别压在：优先级（A/C 的排序面）／retry-冷却（§五异常"工人到不了"）／分域粒度（§七内部结构）／统计粒度（交付物"处置读数"）。

**边界（硬约束，取自文档与任务书）**
- ⛔ 不动 `TaskScheduler` 的 **54 行/15 文件**调用面外溢（`任务书`：不改 `TaskScheduler`；`05 §十一-1/2/4/5/8` 由 M5 一次改完）。
- ⛔ 不把 M5 的**配对／枚举删除／完成回调**提前塞进 M2（`中层执行计划 :210`／`:216`）。
- ⛔ 不建 `BuildingAbilityCatalog`（归 `M6`·`:289`）。
- ⛔ 四类调度钟归 `06`，交互行只留"耗时"（`:210`）。
- ⛔ 本批未取任何**运行读数**（除 2.8/5.3 标注的"静态"外，判据 2/4/7 的运行面均标"未取"）。

**候选最小片（事实性列举，⛔ 不由本端裁）**
- 候选 ①：`M5-A` 的**数据结构落码**（新增卡片/状态类型，零老码改动 ⇒ 与 ① 并存相容）。
- 候选 ②：先补 **§四四项待裁的事实包**下钻（现状已在 §四给出；是否需要更细读数由策划端定）。
- 候选 ③：`06 §五` **四类异常中"可行而未被占"的一项**（当前仅"目标变了"有现成事件源 `MapReplacedEvent` 未接线；其余三项均缺载体）。

---

## 七、零改动声明 ＋ 应登记项

- **零改动**：⛔ 本阶段未改任何 `.cs`／`.asset`／`.prefab`／`.unity`；⛔ 未进 Play；⛔ 未开 `BuildingAbilityCatalog`；⛔ 未动 `TaskScheduler`；⛔ 未 commit／未 push。（第一阶段仅提交 `HH.336` 的 7 个物理路径，见下。）
- **第一阶段收口读数**：commit **`76452c013eac1b53e7321d26a61f1d71129261fb`**（`7 files changed, 20 insertions(+), 301 deletions(-)`）＝ 5 `M` ＋ 2 `D`（`Valley_HH329_M4AProbe.cs`／`.cs.meta`）；`git show --stat` 与 `git status --short` 已随执行回复给出；⭐ **未带 `GameScene.unity`／未跟踪件／资产／预制体**；⛔ 未顺手清理 `mine.asset` 旧序列化键。⚠️ pre-commit 钩子提示「疑似未提交交付件：`HH.336_…_交付报告.md`」—— 按本批指令（只提交 7 路径）该报告**有意未入本提交**，列报待策划端处置。
- **本报告勘正**：`HH.336` 报告已按指令把「Assets 面恰 6 项」改为「**6 个逻辑项、7 个物理路径**」（两处：§二判据 6 段 ＋ §六应登记项）。

### 应登记项（⛔ 本批未改账本 · 供策划端回填 `_编号登记.md`）

```text
| **HH.337** | **`M5` 开工前实核报告**（执行端 · 承 `HH.336` 收口后签发 · 取号 HH.337）：**只读批**（零代码/零资产/未建能力表/未动 TaskScheduler）。
读数（全部本次现读·不采旧报告数字）：`ITaskScheduler` 10 方法（`ITaskScheduler.cs:11-38`）·**类型调用点 0**·实现者唯一 `TaskScheduler.cs:32`；
`ITaskSource` **9 类实现者**（8 实体源＋1 调试源 `WorkerTask.cs:58`）·注册表 `_sources:55`；`KingdomTaskType` **11 项**（`TaskPriorityConfig.cs:34-46`）·生产构造 **14 点/9 文件**·**零构造 5 项**（Repair/Produce/Rancher/WaterCarry/GoldMine）；
`TaskScheduler.Instance|HasInstance` **54 行/15 文件**（与文档「54/15」吻合）；任务对象＝`KingdomTask`（7 字段 `:26-42`）；配对/预定实体 7 张（`:55-61`）＋第二处 `ScheduleCenterStub._crewAssignments:56`；
**⭐ 契约-代码不一致（停在报告未修）**：① 任务持源引用（`source:27`/`advertiser:42`）＋工人持任务 ⇒ 违 `05 §六:153`「对象侧零字段」② `06 §八:149/150` 仍写「7 个/12 项」旧数（应 9 类/11 项）；
缺口：`retry`/不可达冷却/聚合统计 **三皆缺**（`TaskScheduler.cs` 0 命中）·大区块分桶 **0**（chunk 0 命中·字段 `TerritorySystem._territory:21` 已有）·`MapReplacedEvent` 未订阅（事件源 `GameEvents.cs:506`／广播 `MapGate.cs:319`）；
7 行交互表**载体＝文档 `05 §四:93-103`**·码面 0（`worker_*` 0 命中）；迁移三步：①并存未开始（新协议载体不存在）②0/9 源已切换 ③清理未开始（5 项零构造为现成候选）。
待裁四项证据齐（`06 §十-2~-5`）。⛔ 未 commit／未 push／未改账本。 | 执行端 | 🟡 **已落盘·待策划端裁** | 2026-09-25 | 报告 `多Agent交接/执行端/HH.337_M5开工前实核_交付报告.md` |
```

## 八、主策划端补落终裁（HH.337，2026-09-26）

### 终裁：判绿

- [实读] Valley Rampart/Assets/_Game 机械复算：ITaskSource 9 类／9 文件；ITaskScheduler 方法 10；TaskScheduler.Instance|HasInstance 54 行／15 文件。
- [实读] Valley Rampart/Assets/_Game/Data/TaskPriorityConfig.cs:32-49 的 KingdomTaskType 为 11 项；最高优先级文档/06_任务层.md:149-152 当前仍显示 9 类/11 项，但 :152 仍写 51 处/13 文件。
- [实读] 复核输出为：ITaskSource=9 files=9、TaskSchedulerInstanceHas=54 files=15、ITaskSchedulerMethods=10；因此 06_任务层.md:152 应勘正为 54 行 / 15 文件引用。[推断] 这是本批只读实核的直接文档产出，不构成代码施工。
- [实读] 06 与现码的其它缺口（双方持有任务/源引用、retry 与不可达冷却尚未在旧链、MapReplacedEvent 未订阅、大区块分桶未接）均保留为事实列报；本批不替用户裁优先级、重试值、分域粒度或统计粒度。

准入结论：HH.337 实核可采信，允许 HH.338（M5-A）及后续 M5 载体批进入验收；不授权 06:154 ②切换或③清理。06:152 的勘正由事务端按本裁落账后再提交。

[未知] 四项待裁政策的最终定值仍不由本批决定。

