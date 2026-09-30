# HH.343 · 第 4 源 `BlacksmithBuilding` 独立只读预检窗口 · 交付报告

- 执行端交付 · 2026-09-30 ｜ 任务书唯一正文＝`多Agent交接/策划端/HH.341_M5-D乙加_小源④_BlacksmithBuilding预检任务书.md`（commit `ddc57869` 签发落账，占位号 `HH.343` 由事务端 `4989c00e` 锁定）
- 证据前缀＝`Valley Rampart/Logs/hh341_small_blacksmith_*`（独立前缀，⛔ 未沿用第 1～3 源／D1 证据，⛔ 零覆盖）
- 本轮性质＝**独立只读预检窗口**（任务书 §〇-1 仅准预检）⇒ **生产面写动作计数＝0**，写动作全部落在证据面＋本报告
- **结果状态＝`BlacksmithBuilding` 未开始 · 预检待裁**（⛔ 不写施工完成／源级通过／判绿）

---

## 1. 开工回执

| 项 | 值 |
|---|---|
| 当前源 | `Valley Rampart/Assets/_Game/Systems/Kingdom/BlacksmithBuilding.cs`（盘值：splitlines 109 ／ `wc -l` 108 ／末行无换行符 ／ 5192 B ／ sha256 `5279E36CF4472AF127BF05147AE2D3BE4DC33E35D5F948EE3570B8039A63EE13`） |
| 顺序位置 | **第 4 源**（阶段任务书 §2.2 默认顺序表第 4 行；任务书 §〇-2 钉死本源，⛔ 未并序、⛔ 未进入第 5 源 `SiegeWorkshopBuilding`） |
| 门槛依据 | 任务书 §〇-1（Q1＝准，第 3 源已在 `D921` 判绿 ⇒ 逐源串行门开启；本裁定只解锁预检不解锁施工） |
| 前置基线 | **开工 `git rev-parse HEAD` 实测**＝`4989c00e416869c0c8253e21f3f8d682ca2361d1`（分支 `main`；证据登记于 gate 文件 `## S BASE`，非推算） |
| 独立回退点 | 见 §2 |
| 写动作计数 | 生产代码／资产／场景／Prefab／协议／政策值＝**0**；证据面＝**4 件**（统计器＋证据＋闸门脚本＋闸门输出，全在允许前缀内）；交付报告＝**1 件** |
| 只读预检结论 | 任务书 §三 四项（3.1／3.2／3.3／3.4）**全部取得**，每项带 `file:line` ⇒ 详见 §3、§4 |
| 卡片适用性判定 | **任务卡源**（§5.1 分支）⇒ 本轮不施工，【请裁】① 提请另开施工窗口 |
| 停手门 | 未命中任务书 §2.2 任一禁止面；`git diff -- _Game` 空输出 ⇒ 未触发「发现代码/资产 diff 非零即停手」条款 |

## 2. 独立回退点（§2.1 末条＋§七-1 先例）

1. **`HEAD`（开工实测）**＝`4989c00e416869c0c8253e21f3f8d682ca2361d1`。
2. **工作树状态**＝`git status --short --untracked-files=all` 全量逐行照录于 gate 文件（`## S STATUS total=241 modified=5 untracked=236`＋`## S STATUS_LINE` 全录）。其中：
   - `M "Valley Rampart/Assets/Scenes/GameScene.unity"`＝挂账 `O-14` 载体，**继续挂账**（§2.2-5，只登记 hash）；
   - 其余 modified（`Packages/packages-lock.json`、`pixel-forge/*` ×3、`最高优先级文档/*` ×2）与全部 untracked 均为本轮开工前既有差异 ⇒ 回退点＝**开工快照**，非本源净面（如实含之）。
   - ⚠️ 本轮 4 件证据产物**不在 `git status` 内**：`Logs/` 被 `.gitignore:89` 整体忽略 ⇒ 证据天然不入提交（与 §九-4「报告 commit 只含本报告 1 文件」同向）。
3. **允许面／禁止面逐条标注**（任务书 §2.1／§2.2，闸门脚本逐文件实测）：

| §2.2 禁止面条目 | 路径 | 本轮 | 零改动实测 |
|---|---|---|---|
| 当前源 | `Systems/Kingdom/BlacksmithBuilding.cs` | 未触碰（只读） | `diff_lines=0` ∧ `blob_identical=TRUE` |
| 调度器 | `Systems/AI/TaskScheduling/TaskScheduler.cs` | 未触碰（只读） | 同上 |
| 协议六文件 | `TaskCardProtocol`／`TaskLifecycleRules`／`TaskBindingTypes`／`TaskBindingManager`／`TaskProtocolRuntime`／`TaskProtocolIssuer`.cs | 未触碰 | 六件各 `diff_lines=0` ∧ `blob_identical=TRUE` |
| 接口／箱管 | `ITaskScheduler.cs`／`ChestManager.cs` | 未触碰 | 同上 |
| 只读链上文件 | `ProductionSystem.cs`／`Building.cs`／`BuildingComponents.cs`／`BuildingFactory.cs`／`StorageComponent.cs`／`BuildingDef.cs` | 未触碰 | 同上 |
| 数据行 | `Resources/Buildings/Blacksmith.asset`／`Resources/Config/BlacksmithDef.asset` | 未触碰 | 同上 |
| 后续源 | `Systems/Kingdom/SiegeWorkshopBuilding.cs` | 未触碰 | 同上 |
| 目录面 | `Systems/AI/Core`／`Assets/Scenes`／`Resources/Buildings`／`Resources/Config` | 未触碰 | `git diff --name-only` 各目录 `changed_files=0`（`Scenes/GameScene.unity` 的脏点＝`O-14` 既有挂账，见第 4 项） |
| 其余 | 训练仓／两巨兽／`HH.342` 废止草稿 | 未读取未写入 | ⛔ 未触碰 |

4. **`GameScene.unity`（`O-14` 载体）**＝只登记：磁盘 sha256 `9BB5AAF7CB9C81BC915E197648C1CA7DA64AAB28B853D9A439ABC6AF385F0C1E`，`git diff` 显示该文件相对 `HEAD` 为 modified（工作树 ≠ `HEAD` 属既有挂账）⇒ **继续挂账，⛔ 未回滚、⛔ 不问回滚**。
5. **回退手段表述**＝`git show <rev>:<path>` 取副本覆盖还原（⛔ 未写 `git checkout --`、⛔ 未整文件 `restore`、⛔ 未 `git add -A`；本轮零生产改动 ⇒ 实际未执行任何回滚类命令）。

## 3. 只读预检四项（任务书 §三 · 全带 `file:line`，路径相对仓库根）

### 3.1 生产触发入口 / 真实调用链 / 源状态变化

**（1）诞生与挂接（数据行 → 组件）**

| 环节 | 锚点 |
|---|---|
| 数据行声明 | `Valley Rampart/Assets/Resources/Buildings/Blacksmith.asset:15` `id: Blacksmith`；`:16-18` `components: [comp.storage, comp.blacksmith]`（⭐ **无 `comp.producer`**） |
| 数据行产能字段 | `Blacksmith.asset:40-43` `producer {kind: 0, rate: 0.5, capacity: 250}`；`:58` `statScale: 1.2`；`:76-79` `gradeScale [0.7, 1, 1.5]`；`:62` `outputResource: 9`；`:64` `isBlacksmith: 1`；`:60` `crewRequired: 0`；`:66` `concurrentWorkers: 0`；`:75` `gatherSeconds: 2`；`:37-38` `warehousePaths: [res_material.metal]`；`:25-28` 建造成本（type0/50＋type1/60）；`:54-57` 升级成本（75/90）；`:29` `footprint {2,2}` |
| 比例 SO | `Valley Rampart/Assets/Resources/Config/BlacksmithDef.asset:13` `m_Name: BlacksmithDef`；`:15` `oreToMetalRatio: 2` |
| 键常量／键表 | `Systems/Building/BuildingComponents.cs:186` `const string Blacksmith = "comp.blacksmith"`；`:199` `Register(Blacksmith, Add<BlacksmithBuilding>)` |
| 挂载入口 A（工厂：预置／读档） | `Systems/Building/BuildingFactory.cs:181` → `AttachComponents :243-255`（`:246` 读 `def.components`、`:252` `TryAttach`）→ `BuildingComponents.cs:220-226 TryAttach` → `Add<T> :235-243`（`:238` 已挂即跳、`:239` `AddComponent`、`:241` `c.Init(b)`） |
| 挂载入口 B（玩家建造） | `Systems/Building/BuildController.cs:329`（同 `AttachComponents`） |
| 组件 Init | `Systems/Kingdom/BlacksmithBuilding.cs:24-31`（`:27` 取宿主 `StorageComponent`、`:28` `Resources.Load<BlacksmithDef>("Config/BlacksmithDef")`、`:29` 累计器清零、`:30` `RefreshRate()`） |
| 产率公式 | `BlacksmithBuilding.cs:34-40`：`_rate = def.producer.rate × def.GetGradeScale(grade) × building.LevelScale()`（`LevelScale()`＝`Building.cs:479-487`，`levels[i].statScale` 连乘） |

**（2）更新（谁驱动 `Tick`）与真实产出面**

| 环节 | 锚点 |
|---|---|
| 集中驱动 | `Systems/Building/ProductionSystem.cs:18-27`（`Update` 每 1 秒：`:21` 累积 `Time.deltaTime`、`:22-25` 到 `tickInterval=1f`（`:13`）⇒ `TickAll`）→ `:32-53`（`:44` `b.GetComponents<ITickable>(_tickBuf)` → `:50` `t.Tick()`；⭐ `M4-A` 遍历接口不点名组件类） |
| 本源 `Tick` | `BlacksmithBuilding.cs:43-61`：`:45` 宿主非 Active ⇒ 退；`:46` `LazyRegister()`；`:47` 储满／无仓／无 SO ⇒ 退；`:48` `_rate<=0` ⇒ 退；`:51` **在岗门** `HasWorkerOnDuty` ⇒ 无在岗停产；`:53-55` 累计器加 `_rate`，不足 1 整数 Metal 不加工；`:57` `oreNeeded = metal × oreToMetalRatio`；`:58` `_storage.Transform(Ore→Metal, oreNeeded)`；`:59-60` 实产扣累计器 |
| 就地加工 | `Systems/Building/StorageComponent.cs:245-265 Transform`：`:247` **仅** `Ore→Metal`（其余组合返 0）；`:249-250` 本仓余量门；`:252-256` 比例取 `BlacksmithDef.oreToMetalRatio`（兜底 2）；`:258` `metal = min(max(1, amt/ratio), room)`；`:260-261` **原料取自国库** `RulerController.Ore < oreNeeded ⇒ return 0`（整批不产、累计器保留）；`:263` 扣国库 Ore；`:264` `Add(Metal)` 入本仓 |
| 容量线 | `StorageComponent.cs:95-101 RefreshCapacity`（`def.producer.capacity>0 ? round(capacity×LevelScale) : 100`）；`:133 FreeSpace`；`:136 IsFull`（`UsedSpace>=capacity`）；`:139 IsFullFor` |

**（3）广告与注册（如何进 `TaskScheduler` 源集合）**

| 环节 | 锚点 |
|---|---|
| 懒注册 | `BlacksmithBuilding.cs:97-102 LazyRegister`（`:99` `!HasInstance ⇒ return`、`:100` `Register(this)`、`:101` 置 `_registered`）；调用点在 `Tick :46`（**在岗门之前**，注释 `:46` 自陈理由＝无在岗时须仍能自举） |
| 注册落点 | `Systems/AI/TaskScheduling/TaskScheduler.cs:147-151 Register`（`:150` `_sources.Add(source)` ∧ `source.OnRegister()`）；本源 `OnRegister/OnUnregister`＝空实现（`BlacksmithBuilding.cs:81-82`） |
| 有效性／坐标 | `BlacksmithBuilding.cs:77 IsValid`（`this != null ∧ _building != null ∧ _building.IsValid`；`Building.IsValid`＝`Building.cs:1489`）；`:79 SourcePos`＝宿主建筑坐标 |
| 广告实现 | `BlacksmithBuilding.cs:85-95 TryAdvertiseTask`：`:88` 宿主失效 ⇒ false；`:89` 本仓满 ⇒ false；`:91` 已在岗（组件源）⇒ false；`:92` `new KingdomTask(KingdomTaskType.Production, this)`（**源＝组件**）；`:93` `destType = None`（原地劳作·常驻广告） |
| 调度器收集 | `TaskScheduler.cs:319-330`（`:321` `s.IsValid` 门、`:322` `TryAdvertiseTask`、`:323-325` Transport 名额门、`:327` 独占去重 `HasAssignedTaskForSourceType`（定义 `:1398-1406`，键＝`advertiser ?? source`＋类型；本源未置 `advertiser` ⇒ 键＝组件源本身）、`:328` `ResolveDest`（定义 `:1317-1336`，`:1321-1322` `None ⇒ destPos=SourcePos`）） |
| 排序与路由 | `TaskScheduler.cs:339 jobs.Sort(CompareByEffectivePriority)`（定义 `:1448`）；`:346 SourceKingdom`（定义 `:1580-1592`：本源命中 `:1586-1590` `is Component ⇒ GetComponentInParent<Building>().kingdomId`）；`:348 slots`（非 Transport ⇒ 1）；`:359` 池隔离；`:360` 格距；`:365 Dispatch` |

**（4）派发 → 到岗 → 完成 → 复位（三阶段严格区分）**

| 阶段 | 锚点 |
|---|---|
| 派发 | `TaskScheduler.cs:420-452 Dispatch`（`:425` 写镜像 `_npcTaskMap`、`:426` 态＝`Assigned`、`:428` `_taskStartTime`、`:448` `IsKingdomTaskWorker=true`、`:449` `InjectStimulus`、`:450` `NavigateToSource`（定义 `:472`））；接缝 A 四支 `:432-442` **不含本源** |
| 位移 | `TaskScheduler.cs:541-543`（`Assigned→MovingToSource`）；`:546-569`（`:548-549` 到达判据 `ArrivalThreshold`（定义 `:1594-1599`）＋`Vector2.Distance`、`:566-568` 超时 `taskTimeout=30f`（`:45`）） |
| ① 到岗 | `TaskScheduler.cs:551` 置 `TaskState.Working` ∧ `:552` `_workStartTime`；接缝 B 四支 `:554-564` **不含本源** |
| ② 真实产出 | **只**由 `ProductionSystem.cs:50 → BlacksmithBuilding.cs:43-61 → StorageComponent.cs:245-265` 承载（秒级 tick，`rate 0.5` ⇒ 2 游戏秒攒 1 Metal）；⛔ 不等于「到岗」也不等于「`Complete`」 |
| ③ 调度器完成 | `TaskScheduler.cs:594` 工时判据（`GetTaskDuration`＝`:1616-1630`，非 Gather/Demolish ⇒ `workDuration=2f`（`:43`））→ `:655-657` else 支 `Complete(id,task,brain)` → `:719-740`（`:721` `ExecuteCompletion`、`:724-725` 复位＋退刺激、`:727` `ClearNpc`、接缝 E `:731-738` **仅三源不含本源**）；`ExecuteCompletion`（`:858-867`）Production 支＝`comp.GetComponent<ProducerComponent>()?.Tick()` |
| 复位 | `TaskScheduler.cs:794-798 ClearNpc`（三镜像表移除）；完成/放弃后本源下 tick 重新广告 ⇒ **循环型常驻源**（⛔ 无「源终态」概念） |
| 注销 | `BlacksmithBuilding.cs:104-108 OnDestroy`（`:106` `HasInstance` 门、`:107` `Unregister(this)`）→ `TaskScheduler.cs:153-159`（`:156` `_sources.Remove`、`:158` `OnBuildingDied(source)` → `:235-248` 按 `ReferenceEquals(kv.Value.source, source)` 放弃在派）；本体侧另有 `Building.cs:1356`（死亡）／`:1317`（拆除）`Unregister(this)` |

**（5）持久化（任务书 §3.1-5 要求「没有就明确写没有」）**

- **代码面未见本源（`BlacksmithBuilding`）自持存档方法**：`ISaveable`／`SaveState`／`LoadState` 三标识在 `BlacksmithBuilding.cs` 内命中 **0**（`SRC_SELF` 的 `Saveable` pattern 5 行命中全部为 `_metalAccumulator`，逐行 `token=` 可核；⛔ 未用相邻源代替）。
- 累计器 `_metalAccumulator`（`:18`）不入档 ⇒ 读档后由 `Init :29` 归零重建。
- 仓内 Metal 存量随**宿主建筑**入档：`Building.cs:1100-…` `SaveState()` 取 `GetComponent<StorageComponent>()` 显式存取；读档侧 `Building.cs:1164 LoadState`。
- 组件在读档时的重建路径＝`BuildingFactory.cs:181 → AttachComponents`（同（1））⇒ 注册态由 `Tick` 内 `LazyRegister` 自举（⛔ 不依赖存档字段）。

**源状态变化汇总（可复核）**：`_building`／`_storage`／`_def`／`_rate` 于 `Init :26-30`；`_metalAccumulator` 于 `:29`（清零）／`:53`（累加）／`:60`（扣减）；`_registered` 于 `:19` 声明、`:101` 置位、`:106` 读取；`IsValid :77` 随宿主；`OnDestroy :104` 注销。

### 3.2 `KingdomTask` / `TaskScheduler` 引用与对应生产分支（逐处）

**本源文件内引用**（`SRC_SELF` 行集，10 pattern，全量求和 **原始 26 → 排除 4（注释） → 有效 22**；分母 N＝109 行 splitlines）：

| 引用点（代码行） | pattern | 对应调度器生产分支 |
|---|---|---|
| `:68` `TaskScheduler.Instance`（`HasWorkerOnDuty` 取句柄） | SchedRef | 在岗查询 `TaskScheduler.cs:167-178`（语义落点＝停产门 `BlacksmithBuilding.cs:51`） |
| `:90` `TaskScheduler.Instance`（广告内取句柄） | SchedRef | 同上（去重前置 `:91`） |
| `:99` `TaskScheduler.HasInstance` | SchedRef | 注册门（懒注册 `:97-102`） |
| `:100` `TaskScheduler.Instance.Register(this)` | SchedRef／RegisterOrUnregister | 注册分支 `TaskScheduler.cs:147-151` |
| `:106` `TaskScheduler.HasInstance` | SchedRef | 注销门 |
| `:107` `TaskScheduler.Instance.Unregister(this)` | SchedRef／RegisterOrUnregister | 注销分支 `TaskScheduler.cs:153-159` → `OnBuildingDied :235-248` |
| `:92` `new KingdomTask(KingdomTaskType.Production, this)` | NewKT | 广告收集 `:322` → 独占去重 `:327`／`:1398-1406` → 排序 `:339` → 路由 `:346` → 派发 `:365`／`:420-452` → 位移 `:541-569` → 到岗 `:551` → 工时 `:594` → **else 支完成 `:655-657`** → `Complete :719-740` → `ExecuteCompletion :858-867` |
| `:12` `class BlacksmithBuilding : MonoBehaviour, ITickable, ITaskSource` | ITaskSrcDecl／Tickable | 契约声明：`ITaskSource`（`Systems/AI/Tasks/KingdomTask.cs:60-76`）＋ `ITickable`（`ProductionSystem.cs:44/50` 遍历接口） |
| `:85` `public bool TryAdvertiseTask(out KingdomTask task)` | Advertise | `ITaskSource` 契约成员，被 `TaskScheduler.cs:322` 调用 |
| `:70` `sched.HasWorkerAssigned(this)` ／ `:71` `sched.HasWorkerAssigned(_building)` ／ `:91` 同 `:70`（广告侧） | HasWorkerAssigned | 查询体 `TaskScheduler.cs:167-178`（`ReferenceEquals(kv.Value.source, producer)` ∧ `st == Working`） |
| `:43` `public void Tick()` | Tickable | 驱动方 `ProductionSystem.cs:50` |
| `:104` `void OnDestroy()` | OnDestroy | 注销配对（见上） |
| `:18 :29 :53 :54 :60` `_metalAccumulator` | Saveable（混合 pattern，见 §5 口径注） | 无调度器对应分支＝源侧累计器；存盘标识子 token 零命中 |
| **注释行（排除，4 行）**：`:9 :75`（`ITaskSource`）、`:10 :63`（`HasWorkerAssigned`） | — | ⛔ 静态命中不写成运行时调用 |
| **协议标识 `ProtocolToken`**（`OnProtocol`／`TaskCard`／`ProtocolRuntime`／`hasDeadline`／`TaskBinding`／`TaskProtocolIssuer`／`FinalizeSlot`／`ProtocolSlot`） | — | **0 → 0 → 0（显式零计数，`zero_flag=1`）** ⇒ 本源不含任何新协议标识 |

**调度器侧对本源的标识行**（`TS_REF`，分母 N＝1660）：`BlacksmithBuilding` 类名在 `TaskScheduler.cs` 内 **0 → 0 → 0（`ClsName/zero_flag=1`）**；本源专用数据标识 `isBlacksmith` 命中 1 行＝`:1546`（`ResolveTaskBiasResource` 内，代码行）；Metal 相关 `ResourceType.Metal` 2 行＝`:948`、`:1565`；`EcoResource.Metal` 2 行＝`:1546`、`:1565`。

**补充口径（⚠️ 不在 §四 五行集内，单列 `SUPP`，⛔ 未混入 `TS_REF`）**：本源专用标识在 `_Game` 生产域另有消费点 —— `isBlacksmith` 5 行（代码 4／注释 1，4 文件）＝`Data/BuildingDef.cs:86`（声明）、`Systems/AI/KingdomBrain/KingdomBrain.cs:532`、`Systems/AI/TaskScheduling/TaskScheduler.cs:1546`、`Systems/Kingdom/AbstractEconomySettlement.cs:202`（＋注释 `:161`）；`comp.blacksmith` 代码 1 行＝`BuildingComponents.cs:186`；`BlacksmithDef` 8 行（代码 6）；`oreToMetalRatio` 6 行（代码 5）；`MetalRate`（本源公开读口）1 行＝声明处 `BlacksmithBuilding.cs:22` ⇒ **生产域零消费者**；`res_material.metal` 代码 1 行＝`Data/ResourceCatalog.cs:44`。

### 3.3 工人任务四能力（Production 分支）

| 能力 | 判定 | `file:line` 链 |
|---|---|---|
| **可接取** | ✓ | 空闲候选 `TaskScheduler.cs:290-309`（`:291` `FindObjectsOfType<NPCBrain>`、`:297` `IsAlive ∧ IsIdleForTask`、`:299` `npcId!=0` 门、`:303-305` 职业门 Worker/Civilian/Porter、`:306` 幂等已占用不重派）＋广告 `:322`（源侧 `BlacksmithBuilding.cs:85-95`）＋去重 `:327`／`:1398-1406`＋排序 `:339`＋池隔离 `:346/:359`＋派发 `:365` |
| **可移动** | ✓ | `Dispatch :420-452` → `:450 NavigateToSource`（定义 `:472`）→ `:541-543 Assigned→MovingToSource` → 到达判据 `:548-549`（阈值 `ArrivalThreshold :1594-1599`）→ `:551` 置 `Working`；`destType=None ⇒ destPos=SourcePos`（`:1321-1322`）＝原地劳作，位移只到源 |
| **可完成** | ✓（**完成语义与产出语义分离**） | 工时 `:594`（`GetTaskDuration :1616-1630` ⇒ `workDuration=2f`）→ 非 Transport/Gather 落 else `:655-657 Complete` → `Complete :719-740`（`:721 ExecuteCompletion`、`:727 ClearNpc`）。⚠️ `ExecuteCompletion` 的 Production 支（`:864-867`）取 `ProducerComponent`，而本源数据行无 `comp.producer`（`Blacksmith.asset:16-18`）⇒ **该完成动作对本源为空操作**；本源「产 Metal」唯一承载＝`ProductionSystem.cs:50 → BlacksmithBuilding.cs:43-61 → StorageComponent.cs:245-265`。登记为观察项 `V4`，⛔ 未自行补语义、⛔ 未把 `Complete` 当产出证据 |
| **可放弃** | ✓ | `Abandon :765-792`；四类触发＝`:518 BrainLost`、`:523 Dead`、`:527/:534 SourceInvalid`（源 `IsValid=false`；`:532` 的在途豁免仅 `ChestEntity`，不含本源）、`:566-568 Timeout`（`taskTimeout=30f`）；另有 `Unregister → OnBuildingDied :235-248` 与 `Tick ① :264-288` 无效源清扫两条收口路径 |

**阶段区分声明**（任务书 §3.1-4）：本报告把 ① 「到岗 `Working`」（`:551`）、② 「`ProductionSystem→Tick` 真实产出」（`BlacksmithBuilding.cs:43-61`＋`StorageComponent.cs:245-265`）、③ 「调度器 `Complete`」（`:657`／`:719`）作三件事报，⛔ 未互相替代。

### 3.4 卡片适用性判定（§5.1／§5.2 二择一）

**判定＝任务卡源**（§5.1 分支）。四要件各有代码面证据：

1. **真实任务对象**＝`BlacksmithBuilding.cs:92 new KingdomTask(KingdomTaskType.Production, this)`（构造契约 `Systems/AI/Tasks/KingdomTask.cs:44-50`）；
2. **调度器真实收集／派发**＝`TaskScheduler.cs:322 → :327 → :339 → :365 → :420-452`；
3. **工人可接取／移动／完成／放弃**＝§3.3 四链全 ✓；
4. **源有明确有效性与收口边界**＝`IsValid BlacksmithBuilding.cs:77`（随宿主建筑）＋收口三条（`OnDestroy :104-108` 注销、`Unregister :153-159`＋`OnBuildingDied :235-248`、`Tick ① :264-288` 无效源清扫）。

⇒ 按 §5.1，本源施工时须在**真实 Play 进局的生产面**出现真实 `TaskCard`，⛔ 不得用影子面／Edit 探针／人工造卡替代；本轮不施工（施工窗口提请见【请裁】①）。
§5.2 六项替代证据**不触发**（判定非「非任务卡源」）；⛔ 未造卡、⛔ 未以「没有卡片」作任何结论。

**与前三源的同异（只登记事实，不引申裁断）**：本源是**循环型常驻广告源**（完成即复位、下 tick 再广告，无「源终态」），且为**双源并存**结构——组件源（`BlacksmithBuilding`）只广告 `Production`，宿主本体源（`Building`，注册点 `BuildingFactory.cs:191`／`Building.cs:1676,1687`）因数据行无 `comp.producer` 而使 `Building.cs:1603-1611` ③ Production 支不触发、仅走 `:1615-1630` ④ Transport（Metal 达阈值外运）；这正是 `HasWorkerOnDuty :70-71` 同时查询组件与建筑的原因（搬运工在场也算在岗）。

## 4. 五行集三数（任务书 §四 · `L-97` 固定格式）

> 全部行集逐行读数落盘于 `Valley Rampart/Logs/hh341_small_blacksmith_precheck_evidence.txt`（388 行／61880 B／sha256 `710E50E8701D702A78BEBF5E534A66E69B5899C42668E793A9AE3783EED80A53`／`## S` 计数行 371＝含末行 `EXIT_OK` 自身，而该末行自报值 370＝不含自身口径，两者并列登记 ⛔ 不择一冒充；由 `hh341_small_blacksmith_precheck_stat.py` 生成，⛔ 手工填数）。每行集均含：口径标签（池内／全量／筛选集）＋行集谓词＋分母 `N`＋字段完整取值域（含零计数项）＋**原始→排除→有效**＋非采样行分类＋复现命令。

| 行集 | 谓词与来源 | 口径 | 分母 N | 三数（原始→排除→有效） | 零计数项（显式） |
|---|---|---|---|---|---|
| **`SRC_SELF`** | `BlacksmithBuilding.cs` 全文件，10 个 pattern（谓词逐条见证据 `## S SRC_SELF ... pattern=`） | 池内（单文件全行） | 109（splitlines；`wc -l`＝108，末行无换行符 ⇒ 两口径并列） | 求和 **26 → 4（注释行） → 22**；分项：SchedRef 6→0→6、NewKT 1→0→1、ITaskSrcDecl 3→2→1、Advertise 1→0→1、RegisterOrUnregister 2→0→2、HasWorkerAssigned 5→2→3、Tickable 2→0→2、Saveable 5→0→5、OnDestroy 1→0→1、**ProtocolToken 0→0→0** | `ProtocolToken` 有效 0（`zero_flag=1`）；行型分布 FILEKIND 109＝code 75／comment 18／blank 16 |
| **`TS_REF`** | `TaskScheduler.cs` 内含 `BlacksmithBuilding`／本源专用标识（`isBlacksmith`／`ResourceType.Metal`／`EcoResource.Metal`） | 池内（单文件全行） | 1660（`wc -l` 1659） | ClsName **0→0→0**；DataFlag 1→0→1（`:1546`）；MetalToken 2→0→2（`:948 :1565`）；EcoMetal 2→0→2（`:1546 :1565`） | `ClsName`（类名 `BlacksmithBuilding`）0（`zero_flag=1`）⇒ 调度器不点名本源，本源全走通用分支 |
| **`SEAM`** | `TaskScheduler.cs` 五类接缝调用行，行区间筛选（D 262-292／A 420-452／B 504-570／E 719-742／C 765-795） | 筛选集（区间） | 1660 | D 4→0→4、A 4→0→4、B 4→0→4、E 3→0→3、C 4→0→4；**`SEAM_SUM` 19 → 0 → 19** | **每类 `BlacksmithBuilding:0`；合计本源命中 0（`zero_flag` 显式登记）**；E 类另有 `WorldGatherSource:0` |
| **`SEAM_TOKEN`**（`SEAM` 附列） | `TaskScheduler.cs` 整文件不限区间的协议词元 | 全量（整文件） | 1660 | `OnProtocolAny` 19→0→19；`ProtocolRuntimeAny` 17→3（注释）→14；`Issuer` 2→0→2（`:464 :1292` 具名实参 `issuer: task.source`）；`TaskCard` 0→0→0；`hasDeadline` 0→0→0 | `TaskCard`／`hasDeadline` 各 0（`zero_flag=1`）；「匹配次数」与「命中行数」两量纲分列（如 `ProtocolRuntimeAny` 匹配次数 21 ≠ 命中行数 17） |
| **`DEFDATA`** | `Assets/Resources/Buildings/*.asset` 含 `comp.blacksmith` 的行 ＋ `Blacksmith.asset`／`BlacksmithDef.asset` 字段行 | 筛选集（全资产枚举）＋池内（单资产） | 资产文件数 37；`Blacksmith.asset` 85 行；`BlacksmithDef.asset` 15 行 | 键行 **1 → 0 → 1**（`Blacksmith.asset:18`，命中文件 1／其余资产 36）；字段逐项：`id :15`、`components :17 :18`（storage＋blacksmith，⛔ 无 producer）、`footprint :29`、`producer :40-43`（kind 0／rate 0.5／capacity 250）、`warehousePaths :38`（`res_material.metal`）、`isBlacksmith :64`＝1、`outputResource :62`＝9、`crewRequired :60`＝0、`concurrentWorkers :66`＝0、`statScale :58`＝1.2、`upgradeCost/cost amount :25-28 :54-57`＝50/60/75/90、`gradeScale :77-79`＝0.7/1/1.5、`oreToMetalRatio :15`＝2 | 含 `comp.blacksmith` 键的资产文件＝1／37；`comp.producer` 键在 `Blacksmith.asset` **0 命中**（该资产 `components` 只有 2 项，`:17 :18` 全录可核） |
| **`ALLREF`** | 全 `Valley Rampart/Assets/**` 内含 `BlacksmithBuilding` 的行，按域三分 | 全量（递归·含生成物桶） | 命中文件 7 | 命中行数 **14 → 0 → 14**（匹配次数 688，两量纲并列）＝**生产域 `_Game` 9 行（代码 5／注释 4）＋ Editor 域 5 行（代码 3／注释 2）**；逐文件：`SiegeWorkshopBuilding.cs` 4、`Valley_OB12_InGate_Probe.cs` 3、`BuildingDef.cs` 2、`R3_SupplyChain.cs` 2、`BuildingComponents.cs` 1、`MineByproductComponent.cs` 1、`BlacksmithBuilding.cs` 1 | 生成物桶另列不并入调用面：`Assets/Unity.VisualScripting.Generated/VisualScripting.Flow/UnitOptions.db`（50544640 B > 20 MB ⇒ 只做字节级计数，byte 级命中 674，⛔ 未逐行归属、⛔ 未算作生产调用面）；不可读文件 0 |

**口径声明（`L-97` 补条）**：① 上表每行集均带口径标签＋谓词＋分母＋三数；② 注释行／生成物域行／Editor 域行**单列**，未混入生产调用面；③ 同字段多口径**并列分列**（行数 vs 匹配次数；`splitlines` vs `wc -l`；`_Game` 生产域 vs Editor 域 vs 生成物桶）；④ 零计数项一律显式写 `0→0→0` 并带 `zero_flag=1`，⛔ 未用「零接缝」之类四字替代明细；⑤ `Saveable` 为**混合 pattern**（`ISaveable|SaveState|LoadState|_metalAccumulator`），其 5 行命中逐行 `token=_metalAccumulator` 可核 ⇒ 三个存盘标识子 token 命中 0，该子零计数在此具名声明；⑥ 本窗口 ⛔ 无「约／≈」类估数，全部读数为行号与整数计数，自报读数一律整数。

## 5. 接缝现状盘点（任务书 §五 · 固定顺序 D／A／B／E／C · 只登记不补码）

| 类 | 语义 | 命中数（调用行） | 全部 `file:line`（判定行／调用行） | 本源命中 |
|---|---|---|---|---|
| **D 源失效** | 无效源清扫 ⇒ 未终态封口 | **4** | `TaskScheduler.cs:274/275`（`WorldGatherSource`）、`:277/278`（`ConstructionSiteStore`）、`:280/281`（`ChestEntity`）、`:283/284`（`MineByproductComponent`） | **0** |
| **A 派发** | `Dispatch` 建立配对／预定 | **4** | `:432/433`、`:435/436`、`:438/439`、`:441/442`（同上四源型） | **0** |
| **B 到达** | `MovingToSource→Working` 到达回调 | **4** | `:554/555`、`:557/558`、`:560/561`、`:563/564` | **0** |
| **E 完成** | `Complete` 后源侧收口 | **3** | `:731/732`（`ConstructionSiteStore`）、`:734/735`（`ChestEntity`）、`:737/738`（`MineByproductComponent`）；⭐ **E 类无 `WorldGatherSource` 支**（显式登记，非漏计） | **0** |
| **C 放弃** | `Abandon` 后回待派／封口 | **4** | `:779/780`、`:782/783`、`:785/786`、`:788/789` | **0** |

- 五类调用行合计 **19**（`SEAM_SUM`），本源 **0**（显式零计数）。
- B 类「判定行 5 ≠ 调用行 4」的差额已定位为**非接缝**行：`TaskScheduler.cs:532` `if (!(task.source is ChestEntity && st == TaskState.MovingToDest))`＝箱源在途豁免门，证据文件以 `## S SEAM class=B(到达) line=532 role=非接缝型判定` 单列（⛔ 未把它当接缝、⛔ 未静默丢弃计数差）。
- 本源当前**完全走旧 `KingdomTask` 链**（占用镜像 `_npcTaskMap`、无 `TaskCard`、无配对／预定）；`TaskScheduler.cs` 内 `BlacksmithBuilding` 类名命中 0（`TS_REF.ClsName`）⇒ 调度器不点名本源，其任务流经通用分支：路由 `:1580-1592`、源失效放弃 `:527-534`、到达 `:548-551`、超时 `:566-568`、完成 `:655-657 → :719-740`。
- ⛔ 未新增任何接缝、⛔ 未把接缝当「已存在」。是否需要接缝属后续施工裁定（任务书 §五 末条）。

## 6. `sim-sync` 义务声明（任务书 §六）

- **本窗口裁定＝不触 `AI.Core`，sim-sync 义务＝0**；本轮为只读预检，唯一相关动作＝只读扫描取证。
- 只读扫描证据：`git grep -n 'BlacksmithBuilding' -- 'Valley Rampart/Assets/_Game/Systems/AI/Core'` ⇒ **rc=1 且空输出**（`## S SIMSYNC_AICORE`）；另以 `ITaskSource|TaskScheduler` 同域复扫亦 rc=1 空输出 ⇒ `AI.Core` 对本源零命中，即「义务 0」的代码面依据。
- 同源同步面写入核验：`champion`／`Holdout`／`harness/Scenarios` 三路径 `git diff --name-only` 各 `changed=0`（`## S SIMSYNC`）；训练仓 `AGENTS.md`、`schemas/factor_registry.example.json` ⛔ 未读取未写入。
- **未出现「必须改 `AI.Core` 才能继续」的情形** ⇒ 未触发停手上报条款。

## 7. 观察项登记（`V4` 起，只登记事实不裁断）

| # | 事实 | `file:line` | 观测范围／筛选条件 | 状态 | 是否阻塞本预检 | 后续归属 |
|---|---|---|---|---|---|---|
| **V4** | 调度器完成动作对本源**为空操作**：Production 支取 `ProducerComponent` 才产出，而本源数据行 `components` 无 `comp.producer` | `TaskScheduler.cs:864-867`；`Blacksmith.asset:16-18` | 直读两文件＋`Building.cs:1603-1611` ③ 支门（`producer != null`） | 已取得 | 否（不影响四能力判定） | 施工轮：「完成」判据由何承载需主策划裁（在岗门＋`HasWorkerAssigned` 语义 vs 完成动作） |
| **V5** | 在岗判定与广告判定**不同源**：`HasWorkerOnDuty` 并计「组件 ∥ 宿主建筑」，而广告门只看组件 | `BlacksmithBuilding.cs:70-71`（双查询）／`:91`（只查 `this`） | `TaskScheduler.cs:167-178` `ReferenceEquals(kv.Value.source, producer)` 按引用比对 ⇒ 组件源与本体源是两个不同对象 | 已取得 | 否 | 施工轮判据设计需知悉：本体源 Transport 在派工 ≠ 组件源已在岗不重派 |
| **V6** | 注册时机在 `Tick` 内且在停产门**之前**，首次广告须等第一次 `ProductionSystem` tick | `BlacksmithBuilding.cs:46`（`LazyRegister` 位置，注释自陈理由）／`:97-102`／`ProductionSystem.cs:22-25,50` | 驱动周期 1 秒；`Tick` 门序 `:45 → :46 → :47 → :48 → :51` | 已取得 | 否 | 施工轮观察窗设计（首卡出现的最早世界时刻） |
| **V7** | 原料真源＝**国库 `RulerController.Ore`**，非本仓 Ore；不足时整批不产且累计器保留（合法无产出路径） | `StorageComponent.cs:260-263`；`BlacksmithBuilding.cs:57-60` | 全文件直读 `Transform`；比例 SO `BlacksmithDef.asset:15`＝2 | 已取得 | 否 | 施工轮判据须区分「合法无产出」与「卡死」 |
| **V8** | 升级路径不刷新本源产率：`OnConstructionComplete` 只喂 `ProducerComponent.RefreshRate` 与 `StorageComponent.RefreshCapacity`；本源 `RefreshRate` 仅由 `Init` 调用 ⇒ `LevelScale()`（`statScale 1.2`）不进入 `_rate`，但容量线会跟级 | `Building.cs:724-725`／`:726-730`；`BlacksmithBuilding.cs:30`／`:34-40`；`Blacksmith.asset:58` | `SUPP` 行集 `RefreshRate_src`：`_Game` 生产域 6 代码行／4 文件，无升级路径调本源（逐行 `## S SUPP token=RefreshRate_src`） | 已取得 | 否 | 归主策划裁（是否属需处理缺口） |
| **V9** | 本源累计器不入档：`ISaveable/SaveState/LoadState` 三标识在源文件内命中 0，`_metalAccumulator` 读档归零；Metal 存量随宿主建筑入档 | `BlacksmithBuilding.cs:18,29`；`Building.cs:1100-…`（storage 显式存取）／`:1164` | `SRC_SELF.Saveable` 5 行全部 token=`_metalAccumulator`；子零计数见 §4 口径⑤ | 已取得 | 否 | 施工轮读档重建证据口径 |
| **V10** | 公开读口 `MetalRate` 生产域零消费者 | `BlacksmithBuilding.cs:22` | `SUPP.MetalRate`＝1 行／1 文件＝声明处 | 已取得 | 否 | 是否保留由主策划裁 |
| **V11** | 本源收口有**两条并行路径且引用比对语义不同**：组件 `OnDestroy → Unregister(组件) → OnBuildingDied(组件)`（`ReferenceEquals` 按组件匹配）与 `Tick ①` 的 `IsValid` 清扫；本体 `Building.cs:1356` 的 `Unregister(本体)` **不**会释放组件源在派卡 | `BlacksmithBuilding.cs:104-108`；`TaskScheduler.cs:153-159`、`:235-248`（`:241`）、`:264-288`（`:271`） | 直读三条链 | 已取得 | 否 | 施工轮「放弃/清算」判据的观测点选择 |
| **V12** | 外部报告锚点漂移先例复现（非本源缺陷）：`TaskScheduler.cs:1300-1303` 注释内自陈的 `MovingToSource(:450)`／`MovingToDest(:571)` 与当前实际行号（`:546`／`:663`）不符 | `TaskScheduler.cs:1300-1303` vs `:546`、`:663` | 只读直读；第 3 源 N-A 同族（位移非内容改动） | 已取得 | 否（不改码） | 事务端裁：是否另案勘正 |

**新发现（方法论级，须请裁）**：任务书 §八 字面 `ITaskSrcDecl` 命令的字符类 `[^\n{]` 在 POSIX ERE 下实为「排除反斜杠／字母 `n`／左花括号」，凡类名与 `: ITaskSource` 之间含字母 `n`（`MonoBehaviour`、`ITickable`、`ConstructionSiteStore`）即漏配 ⇒ 字面命令 HEAD/WT 各 **2 行／2 文件**，而语义修正口径（`class X : .*ITaskSource`）为 **9 行／9 文件**，本源 `BlacksmithBuilding.cs:12` **只出现在语义口径**。若仅按字面命令读数，会得出「本源未声明 `ITaskSource`」的**假阴性**（`L-39`/`D735` 家族：grep 零命中不作结论）。两口径已并列落盘（`## S BASE_ITaskSrcDecl_*` 与 `## S BASE_ITaskSrcDeclSEM_*`），⛔ 未择一冒充、⛔ 未改任务书命令原文。

## 8. 基线复算（任务书 §八 · 命令逐字执行 · `## S BASELINE`／`## S AGG rowset=BASE_*`）

生产域＝仓库根下 `Valley Rampart/Assets/_Game`；口径＝`git grep -nE` **命中行数**（非匹配次数）。

| 行集 | HEAD 值 | 工作树值 | 差异 | 逐文件归因 |
|---|---|---|---|---|
| `SchedRef`（`TaskScheduler\.(Instance\|HasInstance)`） | 76 行／17 文件（rc=0） | 76 行／17 文件（rc=0） | **0** | 逐文件行数 HEAD 与 WT 全等；`BlacksmithBuilding.cs`＝6 行（与本源 `SRC_SELF.SchedRef` 6 行逐项吻合）；`MineByproductComponent.cs` 10、`Building.cs` 14、`ChestEntity.cs` 8、`SiegeWorkshopBuilding.cs` 6、`ConstructionSiteStore.cs` 5、`WorldGatherSource.cs` 5、`UnitController.cs` 4、`ChestManager.cs` 4、其余 7 文件各 1～2 |
| `NewKT`（`new KingdomTask\(`） | 15 行／10 文件 | 15 行／10 文件 | **0** | `BlacksmithBuilding.cs`＝1 行（＝本源 `:92`）；`Building.cs` 5、`MineByproductComponent.cs` 2、`ScheduleCenterStub.cs`／`ConstructionSiteStore.cs`／`SiegeWorkshopBuilding.cs`／`UnitController.cs`／`WorkerTask.cs`／`ChestEntity.cs`／`WorldGatherSource.cs` 各 1 |
| `ITaskSrcDecl`（§八 字面命令） | 2 行／2 文件 | 2 行／2 文件 | **0** | `WorkerTask.cs:58`、`WorldGatherSource.cs:36`；⚠️ 字符类缺陷见 §7 末「新发现」 |
| `ITaskSrcDeclSEM`（语义补口径，同源并记） | 9 行／9 文件 | 9 行／9 文件 | **0** | 含本源 `BlacksmithBuilding.cs:12`；另 8 支＝`Building.cs:29`、`ConstructionSiteStore.cs:23`、`MineByproductComponent.cs:31`、`SiegeWorkshopBuilding.cs:19`、`UnitController.cs:24`、`WorkerTask.cs:58`、`ChestEntity.cs:19`、`WorldGatherSource.cs:36` |
| `BlacksmithInSrc`（`BlacksmithBuilding` 于 `_Game`） | 9 行／5 文件 | 9 行／5 文件 | **0** | `SiegeWorkshopBuilding.cs` 4、`BuildingDef.cs` 2、`BuildingComponents.cs` 1、`MineByproductComponent.cs` 1、`BlacksmithBuilding.cs` 1 |

**零改动证明**：`git diff --name-only HEAD -- 'Valley Rampart/Assets/_Game'` 与 `git diff --numstat HEAD -- …` **均为空输出（rc=0，0 行）** ⇒ HEAD/WT 全等；§2 表内 19 个禁止面文件逐一 `diff_lines=0` ∧ `blob_identical=TRUE`（`## S FORBID`／`## S AGG rowset=FORBID` denom 19 → raw 19 → valid 19）。
⚠️ **换行口径注**：`BuildingFactory.cs` 磁盘原始 sha256 ≠ `git show` 文本 sha256（`wt_crlf_pairs=406`），系 `core.autocrlf=true` 的检出期换行转换，**不属内容改动**——已用 git 同算法口径复核：`git rev-parse HEAD:<path>` 与 `git hash-object <path>` 的 blob id **相等**（该行 `blob_identical=TRUE`）。零改动判据以「`git diff` 空 ∧ blob id 等」为准，不采信原始字节 sha 直比。
既有脏点（`GameScene.unity`＝`O-14`、`packages-lock.json`、`pixel-forge/*`、`最高优先级文档/*`）登记为**外部改动**，⛔ 未纳入本源改动面。

## 9. 四步落盘核验闸门（任务书 §九 · 回执）

| 步 | 对象 | 实测 |
|---|---|---|
| **1 mtime 变化** | 证据文件 | 生成前＝不存在（新建）；写入后 `mtime=2026-09-30 11:15:01.105`（SUPP 段并入后终稿），前后有变化 ✓（本窗口内该文件被统计器写过 2 次：先 11:01:31.648 / 54013 B，后 11:15:01.105 / 61880 B，两次均由脚本回报并落 gate 记录） |
| | 本报告 | 写入前＝**不存在**（新建）；第 1 次落盘后 mtime/字节由脚本重读登记于 gate 文件 `## S GATE_FILE tag=REPORT`（终值随交付消息报出） |
| **2 磁盘重读** | 证据＋报告＋两件脚本 | 均以 `Path.read_bytes()/read_text()` 从磁盘重读（⛔ 未复用内存缓冲），行数／字符数／首尾锚点逐字登记（`## S GATE_FILE`／`## S GATE_REREAD`） |
| **3 sha256＋长度** | 证据文件 | 盘值 `61880 B`；sha256 **两口径一致**：脚本自算 `710E50E8…0A53`（大写）＝ `certutil -hashfile … SHA256` 独立复算 `710e50e8…0a53`（小写）✓；行数 388，`## S` 计数行 371（末行 `EXIT_OK` 自报 370＝不含自身口径，两值并列） |
| | 统计器／闸门脚本 | `hh341_small_blacksmith_precheck_stat.py`＝`24504 B / mtime 2026-09-30 11:12:17.425 / d86198547cb06118193fddc8b1054eb9e2d378fc213840cc5d523d33d2d96b70`（此后未再改，⇒ 与证据文件同代）；`hh341_small_blacksmith_precheck_gatecheck.py`＝**末次改后** `12412 B / mtime 2026-09-30 11:21:41.397 / 2a2431ba5e306626dce361501947695f408ef443ba197dc11ab449cd2c3904f0`（⚠️ 本窗口内该脚本因 §11-3、§11-6 两项自纠被改过，早期盘值 `12063 B / mtime 11:09:00.861 / 34cbdf60c4e02c221846daefbcacc7563cd719c97551f7f98154c24b37533986` 已被取代 ⇒ 复算以末次值为准；闸门输出 `hh341_small_blacksmith_precheck_gate.txt` 由末次脚本生成，其自身盘值不可自持，随交付消息报出） |
| **4 提交 vs 磁盘一致性** | 本报告（单文件 commit 后） | 具名 commit＝`ff4c1e2fdd4aa9e75cb1b08ecd57376f1831517f`（`1 file changed, 268 insertions(+)`）；实测 `ls_tree_oid=f87e4a3d03d4d4ede73a009bc81dce6b07169d09` ＝ `disk_recomputed_git_blob_id=f87e4a3d03d4d4ede73a009bc81dce6b07169d09` ⇒ **blob_identical=TRUE**（同算法同口径）；`files_in_commit=1` `single_file=TRUE` ⇒ ⛔ 未混入源码／资产／场景／既有证据／`O-14`；结果行登记于 gate 文件 `## S GATE_COMMIT`／`## S GATE_COMMIT_CLOSURE`／`## S GATE_COMMIT_SCOPE`（证据文件不入提交 ⇒ 不违反单文件提交约束）。**⚠️ 自指口径**：本行写定后报告自身字节必变 ⇒ 上列 oid 对应「本行写入前盘态」；闭合复算以定稿后末次 gate 值为准（末次值随交付消息报出，⛔ 不在正文写死自身终值） |

**自持值物理限制声明**：闸门输出文件 `hh341_small_blacksmith_precheck_gate.txt` 无法在自己的正文里预知自己的终值 hash，故其**末次实测值只在交付消息与报告写作时点的盘值中出现**；本报告正文 ⛔ 未写死 gate 文件的 hash／行数（防自指漂移，承上一轮 `_r2` 实证教训）。

## 10. 静态轮不适用声明（⛔ 不以静态阅读冒充观察窗）

- 观察窗 7 项（`L-98`）：**不适用**——本轮无进局、无派工读数、无时间窗测量；
- 量纲（游戏秒／墙钟折算、短窗倍率）：**不适用**——无 `Time.time` 差／`Stopwatch`／`WaitForSeconds` 读数；凡本轮出现的秒值（`tickInterval=1f`、`workDuration=2f`、`taskTimeout=30f`、`gatherSeconds=2`、`rate=0.5`）一律标注为**数据行／字段声明值**，不是实测；
- 格式串纪律（仅 `0.0`／`0.00`）：**不适用**——本轮无自报实测浮点；
- `L-97` 满仓补条（`UsedSpace`/`FreeSpace`/`IsFull`）：**不适用**——本轮未声称任何「满仓／超容／容量不足」实测结论，只登记 `IsFull :136`／`FreeSpace :133` 的判据定义；
- `L-99` 回滚前置检查：**不适用**——本轮未执行任何回滚类命令；`O-14` 只登记 hash；
- `L-100` 改动面收据：由**实际变更集合反推**＝`git diff -- _Game` 空输出 ∧ 19 禁止面 blob 等 ⇒ 改动面＝∅（证据面 4 件＋报告 1 件另列，且证据面被 `.gitignore:89 Logs/` 排除于提交）。

## 11. 自报瑕疵

1. **统计器 `Issuer` pattern 曾疑过度匹配**：`\bissuer\b` 是否把注释词计入——复核后确认 2 处命中均为 C# 具名实参（`TaskScheduler.cs:464`、`:1292` 的 `issuer: task.source`），未收窄 pattern，但已在证据行带 `kind=code`＋原文可核。
2. **`SEAM` B 类「判定行 5 ≠ 调用行 4」首版未解释**：闸门/证据首版只给计数不给差额行 ⇒ 已补 `role=非接缝型判定` 单列行（`:532`），使五类计数闭合可复核。
3. **零改动判据首版用「磁盘原始 sha256 vs `git show` 文本 sha256」直比**，在 `core.autocrlf=true` 下把 `BuildingFactory.cs` 误示为 `identical=FALSE`（换行转换被当成疑似差异）⇒ 已改为「`git diff` 空 ∧ `git rev-parse HEAD:path` blob id ＝ `git hash-object path`」双口径，并保留 CRLF 计数（406）作解释性证据。**此为本窗口自身的方法缺陷自纠，非源码问题。**
4. **补充口径（`SUPP`）在报告写作前才追加**：导致证据文件在本窗口内被重写 2 次（末次 11:15:01.105）；未覆盖任何其他源或 D1 的证据文件（`L-68`），两次写盘均为本源自有产物的定稿过程，已如实登记 mtime 序列。
5. 报告写作过程中未出现写工具静默失败（本窗口所有落盘均经脚本回报＋磁盘重读双验），⛔ 未使用估数／「≈」。
6. **闸门第 4 步首版误报「提交不含报告」（工具层假阴性）**：以 `git show <rev>:<含中文路径>` 经 Windows `subprocess` 传参时，中文被 ANSI 代码页破坏 ⇒ `rc=128`，首版据此写出「该 commit 未含此文件」的**失实行**。已改为「`ls-tree` 取 oid ＋ 磁盘侧本地重算 git blob id ＋ `cat-file` 复核」全 ASCII 参数路径，并与 Shell 侧手工复算（同 oid `f87e4a3d…`）对撞定案；假阴性行已随 gate 复跑被真值行替换。**教训面**：跨工具端的「命令失败」须先排除**取数工具自身**故障，再判被测对象状态（`L-97` 第 5 项「未取得」三分归因中的**工具问题**实证）。
7. **`## S` 计数首版单值报出掩盖自指口径**：证据文件末行 `EXIT_OK` 自报 370（不含自身）而脚本落盘回报 371（含自身），首版正文只写 371 ⇒ 与 gate 文件首尾锚点读数不一致。已改为两值并列登记并标明口径差异（同上一轮「描述计数的行本身被下一次计数计入」的自指族）。

## 12. 【请裁】（≤5 条）

1. **准开第 4 源 `BlacksmithBuilding` 施工窗口**：预检四项全取得、判定＝任务卡源 ⇒ 按 §5.1＋阶段任务书 §七九步另开施工轮。是否准予，请主策划裁。
2. **接缝形态**：本源五类接缝命中全 0。若准施工，是否照第 2/3 源同型先例补齐 **D/A/B/C 四类回调＋E 完成接缝**（在类型判定处增列 `BlacksmithBuilding`），请裁。⚠️ 本源与前三源的结构性差异＝**循环型常驻源（无源终态）＋双源并存（组件源 Production／本体源 Transport）** ⇒ 「Done／封口」语义可否沿用同型先例，须主策划定，执行端不自定口径。
3. **完成判据归属（`V4`）**：`ExecuteCompletion` 的 Production 支对本源为空操作（无 `comp.producer`），施工验收的「完成」是否改以「在岗门＋`HasWorkerAssigned(组件)` 语义＋`ProductionSystem→Tick` 实产读数」承载，请裁。
4. **升级不刷产率（`V8`）**：`Building.cs:724-725` 升级路径只喂 `ProducerComponent`，本源 `_rate` 不随 `statScale` 更新（容量线却跟级）——是否属需处理缺口、归本源施工轮还是另案，请裁（本轮 ⛔ 未处理）。
5. **§八 字面命令的字符类缺陷**（见 §7 末）：后续源的基线复算命令是否统一改用语义口径（`class X : .*ITaskSource`），请裁；本轮两口径已并列落盘，⛔ 未擅改任务书原文。

## 13. 源级结论

**本轮未施工 ⇒ `BlacksmithBuilding` 源级＝未开始 · 预检待裁**（未判通过／未判未通过；未自行推进施工；未申请第 5 源 `SiegeWorkshopBuilding`；未取新 D 号；未 push）。

预检结论：**四项全部取得**；判定＝**任务卡源**；四能力（可接取／可移动／可完成／可放弃）在 Production 分支均有 `file:line` 调用链证据；接缝现状＝五类共 19 调用行、**本源 0**；sim-sync 义务＝0；`V4`～`V12` 九项观察登记；零改动证明成立。下一步唯一动作＝【请裁】① 待主策划裁定。
