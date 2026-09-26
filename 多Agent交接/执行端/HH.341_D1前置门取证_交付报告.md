# HH.341 · D1 前置门 —— §九-5「读档重验」可行性取证 · 交付报告

> 执行端 ｜ 阶段：**D1 前置门（只读取证）** ｜ 日期 2026-09-26
> 性质：⭐ **只读取证**。⛔ 零代码改动、零资产改动、零配置改动（本批未改任何 `.cs`/`.asset`/`.prefab`/`.unity`）。
> 沿用 `HH.341`，⛔ 未取新号、未写 D 号。

---

## 〇、开工回执（四项）

1. **阶段**：D1 前置门（§九-5「读档重验」可行性取证）。
2. **本批确认零改动**：
   - `git diff --name-only -- Assets/_Game Assets/Editor Assets/Resources Assets/ProjectSettings` ⇒ **空**；
   - `Assembly-CSharp.dll` mtime ＝ **2026-09-25 23:58:01（未变）** ⇒ **生产运行时程序集字节未变**；
   - `GameScene.unity` mtime ＝ **2026-09-15 17:17:07**（远早于本会话）⇒ 本批未写场景；
   - 会话起始即存在的既有脏点（`GameScene.unity`、`pixel-forge/*`）⛔ 非本批引入。
3. **五问初步结论**（详见 §一~§六）：**`TaskCard` 现状确实无法入档**——6 个协议文件 `[Serializable]` 命中 0；存档模块 55 个中任务相关模块 0 个、存档原文任务关键字 0 命中；`ITaskRestoreValidator` 0 实现 0 调用；旧链任务态同样零入档 ⇒ **§九-5 的"逐条重验"目前无数据可验**。
4. **进局取证方案（已执行）**：MCP `manage_editor play`（单独下发）→ 等 `ready_for_tools` → `execute_menu_item` 触发临时探针 → 探针内 `TestHarnessApi.EnterTestRun(seed=31418/Small/difficulty=2)` 正门起局 → `SaveManager.Save` → `SaveManager.Load` → `TestHarnessApi.ExitTestRun()` → `EditorApplication.ExitPlaymode()`；Console 按 **`L-95` 三段法**（Play 内读 → 退 Play 后读 → 空白对照复跑）。

---

## 一、结论速览（五问答案）

| # | 问题 | 结论 | 标级 |
|---|---|---|---|
| 1 | `TaskCard` 及字段类型要被序列化，缺什么？ | 缺 **13 处 `[Serializable]`**（`TaskCard` ＋ 12 个值类型）；另有两项非硬阻断的格式项（`+∞` 哨兵 / `long`）与两项结构性缺口（无入档容器、无重验实现） | `[实读]`＋`[推断]` |
| 2 | 现有存档承载哪些任务状态？ | **零**：无 `KingdomTask`、无 `_npcTaskMap`、无 `TaskState`、无 `TaskScheduler` 模块；`TaskScheduler` 非 `ISaveable`；`UnitController.NPCBrain`/`Building` 存档字段中无任务字段 | `[实读]` |
| 3 | `ITaskRestoreValidator` 现状？ | 定义于 `TaskCardProtocol.cs:559-563`，全仓仅另 1 处**注释**提及 ⇒ **实现 0、调用 0**；卡片侧 `EnterRestore/RestoreValidated/RejectRestore` 亦 **0 生产调用点** | `[实读]` |
| 4 | 若要做，代价清单？ | 见 §五（**8 个文件／10 项改动**，含 3 项"新增载体"） | `[推断]`（清单本身） |
| 5 | 旧链任务态能否作 §九-5 的替代验证载体？ | **不能**——存档里没有任务数据，读档后也不存在"待重验的进行中任务"；实测读档后任务全部由**新一轮广告/派发重建**（npcId 集合与内容均变），非恢复 | `[实读]`＋`[推断]` |

---

## 二、Q1：可序列化现状（逐项）

### 2.1 硬缺口：`[Serializable]` 命中 = 0
扫描：`rg -n 'Serializable' Assets/_Game/Systems/AI/TaskScheduling`（6 个协议文件：`TaskCardProtocol.cs`／`TaskLifecycleRules.cs`／`TaskBindingTypes.cs`／`TaskBindingManager.cs`／`TaskProtocolRuntime.cs`／`TaskProtocolIssuer.cs`）
⇒ **0 命中** `[实读]`。存档走的 `JsonUtility`（`SaveManager.cs:281/322`）**只序列化带 `[Serializable]` 的类型** ⇒ 现状下新协议**整族不可入档**。

### 2.2 需加特性的类型清单（逐处）
| # | 类型 | 位置 | 性质 |
|---|---|---|---|
| 1 | `TaskCard`（sealed class，26 字段） | `TaskCardProtocol.cs:275` | 需 `[Serializable]` |
| 2 | `TaskIssuerRef` struct | `TaskCardProtocol.cs:30` | 同上 |
| 3 | `TaskTargetRef` struct | `TaskCardProtocol.cs:59` | 同上 |
| 4 | `TaskCountSpec` struct | `TaskCardProtocol.cs:76` | 同上 |
| 5 | `TaskPrecondition` struct | `TaskCardProtocol.cs:98` | 同上（`TaskPrecondition[]` 元素类型） |
| 6 | `TaskProgressSpec` struct | `TaskCardProtocol.cs:117` | 同上 |
| 7 | `TaskRewardSpec` struct | `TaskCardProtocol.cs:137` | 同上 |
| 8 | `TaskTargetEffect` struct | `TaskCardProtocol.cs:156` | 同上 |
| 9 | `TaskLifecycleStats` struct | `TaskCardProtocol.cs:220` | 同上 |
| 10 | `TaskTargetKey` readonly struct | `TaskBindingTypes.cs:48` | 同上（若 `binding` 亦入档） |
| 11 | `TaskReservation` struct | `TaskBindingTypes.cs:100` | 同上 |
| 12 | `TaskPairRecord` struct | `TaskBindingTypes.cs:114` | 同上 |
| 13 | `TaskSweepReport` struct | `TaskBindingTypes.cs:124` | 同上（可选） |
⇒ **共 13 处**（枚举无需特性：`JsonUtility` 按底层整型写读 `[实读]`：`BuildingSaveData.state/faction/grade` 皆以 `int` 化入档，`BuildingSaveData.cs:26-28/43`）。

### 2.3 非硬阻断但已实测的格式项
- **`+∞` / `NaN` 哨兵**：`TaskCard.deadline`（`:309`）默认 `float.PositiveInfinity`，`TaskReservation.deadline`（`TaskBindingTypes.cs:105`）同义。
  实测（用既有 `[Serializable]` 类型 `DifficultySaveData`，**⛔ 未新增/改任何项目类型**）：
  - `ToJson(+∞)` ＝ `{"currentDifficulty":2,"currentFactor":Infinity}`
  - `JsonUtility.FromJson` **能读回**（`IsInfinity=True`）；`NaN` 同理（`IsNaN=True`）
  ⇒ 结论 `[实读]`：**Unity 自身可往返**，但**写出的不是标准 JSON**（`Infinity`/`NaN` 非 JSON 字面量）⇒ 现状不阻断本仓（存档只由 Unity 读写），但**一旦引入外部解析/校验/迁移脚本即失败**。列为代价项（§五 E-2）。
- **`long`**：`TaskCard.taskId`（`:284`）／`unreachableBlockedUntilTick`（`:321`）／`TaskReservation.taskId`／`TaskPairRecord.taskId`。
  `[推断]` `JsonUtility` 支持 int64；**本仓无先例**——生产域 `public long` 仅 5 处（4 处即新协议，另 1 处 `MidChunkLodState.cs:37` 所在类**非 `[Serializable]`**）⇒ 建议实施时以一次往返试验钉死。

### 2.4 明确**不**缺的项（排除误判）
- **无接口字段、无对象引用字段**：`TaskCard` 26 字段反射扫描（`HH.341 D0` 批断言 A2b）⇒ 违规 0；字段类型全为基元／枚举／值 struct／`string`／值 struct 数组 `[实读]`。
- **无 `string[]`**：`preconditions` 是 **struct 数组**（`TaskPrecondition[]`），非 `string[]` ⇒ 可序列化（元素特性补齐后）。
- **非 MonoBehaviour／ScriptableObject**：`TaskCard` 是纯 C# 类 ⇒ 不参与 Unity 场景序列化，**只能**经 `ISaveable` 自序列化路径入档。

---

## 三、Q2：存档系统现状（承载了哪些任务状态）

### 3.1 存档机制（实读）
- `SaveManager`（`SaveManager.cs:11`，Singleton）收集全部 `ISaveable`：`SaveManager.cs:262-279` 遍历注册表逐个调 `SaveState()` ⇒ 组装 `GameSaveRoot` ⇒ `JsonUtility.ToJson` 落盘（`:281-288`）；读档对称（`:352-370`：Global 分发 → `SpawnFromSave` → Scene 分发）。
- `GameSaveRoot{ saveVersion, saveTime, slotName, isFinished, summary, modules }`（`SaveRoot.cs:22-37`）；`ModuleSaveEntry{ saveId, typeName, json, version, phase }`（`SaveableContracts.cs:36-43`）。
- ⭐ **每个模块自带 JSON 字符串**：`SavePayload{ typeName, json, version }`（`SaveableContracts.cs:19-30`）——`SaveManager` **不关心内容**（`:16-17` 注释）。

### 3.2 任务状态承载 = 0（四重证据）
| # | 证据 | 读数 | 标级 |
|---|---|---|---|
| ① | 存档模块清单机械扫描（本次存档 55 模块）：saveId/typeName 含 `task`/`card`/`binding`/`schedule` 的模块数 | **0** | `[实读]` |
| ② | 存档**原文**关键字命中（`save_hh341_evi.json`，41 760 B） | `KingdomTask=0`｜`TaskCard=0`｜`TaskState=0`｜`_npcTaskMap=0`｜`TaskBinding=0`｜`npcId=0`｜`TaskScheduler=0` | `[实读]` |
| ③ | `TaskScheduler` 是否 `ISaveable` | 否（`TaskScheduler.cs:32` ＝ `Singleton<TaskScheduler>, ITaskScheduler`；全仓 22 处 `SaveId`/`SaveIdPrefix` 取值点**无任务相关**） | `[实读]` |
| ④ | 在册表住所 | `_npcTaskMap` 全库 **16 处命中全在 `TaskScheduler.cs`**；`UnitController.cs:37` 仅**注释**提及 | `[实读]` |
补充（逐类字段面）：
- `UnitController.SaveState`（`:474-515`）＝ `UnitSaveData` v6，字段：faction／occupation／hp／maxHp／attack／defense／walk-run／posX,Y／satiety／happiness／lastBirthDay／childGrowthDays／isVagrantRecruit／birthCamp／carriedType,Amount／kingdomId／raceId／personalitySnapshot ⇒ **无任务字段** `[实读]`。
- `NPCBrain`（`NPCBrain.cs:49`）实现 `IAIDebugInfoExtended, IExecutorEventReceiver, IAIDebugInfoV3` ⇒ **非 `ISaveable`**；其 `IsKingdomTaskWorker`（`:191`）是 public 字段但**不入档** `[实读]`。
- `Building.SaveState`（`:1100-1142`）／`BuildingSaveData`（`BuildingSaveData.cs:16-74`，33 字段）⇒ **无任务/工人指派字段**；其中 `awaitingMaterials`／`demolishing`／`demolishProgress` 是**建造/拆除进度态**，⛔ 不是"谁在做" `[实读]`。
- `Building.currentWorkers`（`Building.cs:156`）＝ 运行时 `List<UnitController>`，零入档（且 `HH.324` 已记其**零写入＝死载体**）`[实读]`。

⇒ **总结论**：现有存档是「**世界 + 单位 + 建筑 + 系统台账**」的快照；**任务（无论旧链还是新协议）完全不在存档面**。存档不承载"进行中任务"这一概念。

---

## 四、Q3：`ITaskRestoreValidator` 现状

| 项 | 读数 | 标级 |
|---|---|---|
| 定义处 | `TaskCardProtocol.cs:559-563`：`public interface ITaskRestoreValidator { bool Revalidate(TaskCard card, out TaskAbortReason rejectReason); }` | `[实读]` |
| 全仓命中 | **2 处**：定义处 ＋ `TaskCardProtocol.cs:457` 的注释（「重验本体由 `ITaskRestoreValidator` 外部提供」） | `[实读]` |
| 实现者 | **0** | `[实读]` |
| 调用者 | **0** | `[实读]` |
| 卡片侧读档入口 | `EnterRestore()`（`:458`）／`RestoreValidated()`（`:471`）／`RejectRestore()`（`:485`）**已落码**，但**零生产调用点**（新协议整体零接线：TaskScheduling 外命中 0 行/0 文件） | `[实读]` |
| `M5-B` 读档口 | `EnterRestore`／`RestoreValidated`／`ReleaseOnTerminal`（`TaskBindingManager.cs:231/253/213`）同样**零调用点** | `[实读]` |

⇒ §九-5 的"重验本体"当前是**纯接口空壳 + 无实现 + 无接线**，与 `TaskCard` 无入档路径**互相独立地**都不成立。

---

## 五、Q4：代价清单（⛔ 只列清单，未动手）

> 必要性口径：**甲＝必须**（不做则该能力不可能成立）；**乙＝建议**（不做可用但有隐患）；**丙＝可选**。
> 风险口径：指**触碰已裁契约/政策**的风险等级。

| # | 文件 | 改动性质 | 必要性 | 风险 | 说明 |
|---|---|---|---|---|---|
| E-1 | `TaskCardProtocol.cs` | 加 `[Serializable]` × 9（`TaskCard` ＋ 8 个值类型，§2.2 表 #1~#9） | 甲 | ⚠️ 中（M5-A 已裁片） | 纯加特性，⛔ 不改字段/状态机；但 M5-A 属"已裁政策"文件，须任务书明确授权 |
| E-2 | `TaskCardProtocol.cs` | `deadline` 的 `+∞` 哨兵→改 `-1`/`hasDeadline` 或加 DTO 转换层 | 乙 | ⚠️ 中（改**卡语义**） | 现状 Unity 可往返（§2.3），但产出非标准 JSON；若走 DTO 层则零改卡语义（推荐） |
| E-3 | `TaskBindingTypes.cs` | 加 `[Serializable]` × 4（§2.2 表 #10~#13） | 甲（若 binding 入档） | 中 | §九-5 要求重验 `binding` ⇒ 需 `TaskReservation`/`TaskPairRecord` 可序列化 |
| E-4 | `TaskBindingManager.cs` | 新增**序列化容器**（两表＋两索引的 DTO）＋ `Export/Import` 口 | 甲 | ⚠️ 中高 | 管理器现为**非单例、不注册全局**（M5-B 明文）⇒ ⛔ 不宜直接让它 `ISaveable`；宜"外部容器读写其表" |
| E-5 | **新增**（建议置于 `Systems/AI/TaskScheduling/`） | **卡片台账 + `ISaveable` 包装器**（`SaveId` 如 `TaskProtocol`；Global 阶段） | 甲 | ⚠️ **高** | 新协议**当前没有任何生产持有者**：`TaskProtocolRuntime._pendingByKingdom` 只存"待派"卡片且**不记已派/终态卡** ⇒ 要入档必须**新建一个台账**（谁持有卡片是本批外的新结构决定） |
| E-6 | **新增** | `ITaskRestoreValidator` 的**实现类**（逐条重验 `taskId`/`targetRef`/`kingdomId`/`state`/`binding`） | 甲 | 中 | 重验"目标还在吗"需调下层门（如 `MapGate.GetFeatureAt`）⇒ 依赖目标解析口径（`M5-C`/`M6` 能力表未落地） |
| E-7 | `TaskProtocolRuntime.cs` | 新增 `SaveState/LoadState` 或由 E-5 代为收集（含 `unreachableBlockedUntilTick` 的 tick 基准换算） | 甲 | 中 | ⚠️ "tick" 是运行时计数 ⇒ 读档后 tick 归零会**全量误判过期**，须定义读档基准（政策项） |
| E-8 | 注册点（如 `CoreBootstrap`/`WorldSystem` 一侧） | 把 E-5 的模块 `RegisterSaveable` | 甲 | 低 | 现成模式（`SaveManager.RegisterSaveable`） |
| E-9 | `SaveRoot.cs`/`SaveManager.cs` | **无需改**（模块自带 JSON ⇒ 透明） | — | 低 | ✅ 存档框架本身**不需要动**（这是好消息：代价集中在任务层，不在存档层） |
| E-10 | 读档时序 | 卡片重验宜置于 `Scene` 阶段之后 ／ `GameLoadedEvent` 后 | 乙 | 中 | `SpawnFromSave`(阶段 1.5) 才重建目标 ⇒ 重验早跑必然误判"目标没了" |
⇒ **代价规模**：**8 个文件**（其中 3 项为**新增**：台账/包装器、重验实现、注册接线），**10 项改动**；⛔ 存档框架零改动。
⇒ **未决政策项 4 条**（须策划端裁，⛔ 执行端不自定）：①卡片是否入档（决定 §九-5 是"重验"还是"重建"）；②`+∞` 哨兵口径；③读档后 tick 基准；④卡片台账的持有者与 §九-6「单管理器」边界（新增第 3 个卡片持有者会**改变**单管理器判据的现状）。

---

## 六、Q5：替代载体评估

### 6.1 旧链任务态在存档里的现状
- **完全不入档**（§3.2 四重证据）。
⇒ 读档后**不存在**任何"存档前进行中的任务"，自然也**没有"逐条重验"的对象**。

### 6.2 进局实测（读档行为）
正门起局（seed=31418/Small/difficulty=2/15x）→ 等任务在册 → 存档 → 读档：

| 时点 | 在册任务数 | 抽样（npcId/task/state） |
|---|---|---|
| 存档前 | **11** | 3/Production/MovingToSource、9/Production、15/Transport、13/Transport、14/Transport |
| 读档后立即（2 帧） | **5** | 5/Transport、4/Transport、11/Transport、1/WaterHaul、10/WaterHaul |
| 读档后 4s | **6** | 16/Transport/MovingToDest、5/Transport、13/Transport、14/Transport、15/Transport |

判读 `[实读]`+`[推断]`：
- 存档前的 `npcId 3/9`（Production）读档后**不存在**；读档后的条目**全是新一轮广告/派发**产生（`source` 亦变）；
- 与 ② 存档原文零任务字样 **逻辑自洽** ⇒ 读档后任务态＝**重建**，不是**恢复**；
- ⚠️ 更细的"读档后是否残留跨档悬空绑定"本批**未取得**（探针只读在册表；`Building.currentWorkers`/`NPCBrain.IsKingdomTaskWorker` 面未逐项对拍）⇒ 如实列报为**未取得**。

### 6.3 能否替代
- **不能**作为 §九-5「读档后逐条重验 taskId/targetRef/kingdomId/state/binding」的替代载体：**数据不存在**（没有卡片、没有任务表、没有绑定关系入档）⇒ 无可重验之物。
- 旧链现状只能支撑一条**更弱**的口径：「读档后任务不中断、不悬空 ⇒ 由广告链重建」。⛔ 这是**政策选择**（把 §九-5 从"重验"降级为"重建"），**不属执行端可自定**，须策划端裁决。
- 其余替代路径评估：
  - 用探针把 `TaskProtocolRuntime` 快照写进自定义文件 → ⛔ 非生产存档路径，且 Edit/探针级证据**不能**充当进局读档证据（红线）。**否决**。
  - 用 `Edit` 级往返试验验证"卡片可序列化性" → 只能回答 Q1（可序列化性），**不能**回答"读档重验"。仅在实施期作单元级辅助。
⇒ **建议**：§九-5 允许二选一，由策划端裁：
  - **甲（保守）**：D1 首片**不要求**卡片入档；§九-5 改写为"读档后**重建语义**不中断/不悬空（本轮 D0/D1 可验）"，卡片入档另立专项。
  - **乙（完整）**：按 §五 清单施工（8 文件/10 项），并在 D1 前先落"卡片台账 + 读档重验"两件。

---

## 七、进局取证读数（seed／进-退 Play／Console）

- **正门**：`TestHarnessApi.EnterTestRun`；**seed=31418**（worldSeed=mapSeed=31418，difficulty=2，Small）；槽 `hh341_evi`。
- 入口读数：ActiveMap=**128×128**、State=Playing、`Time.timeScale=15`、day=1；等「进行中任务 ≥1」命中帧 **9065**，在册 **11** 条。
- 存档：`SaveManager.Save("hh341_evi")` ＝ **True**；`saveVersion=3`、**模块 55**；文件 41 760 B。
- 读档：`SaveManager.Load("hh341_evi")` ＝ **True**。
- 退出：`TestHarnessApi.ExitTestRun()`（`maximumDeltaTime=0.33`、`shadows=All`、`vSync=1` 已复原）＋ `EditorApplication.ExitPlaymode()` ⇒ MCP 复核 `play_mode.is_playing=**false**`。

### Console（`L-95` 三段法）
| 段 | 读数 |
|---|---|
| ① Play 内（探针下发前） | **3** 条：`ruler 缺脚本`／`No Theme Style Sheet…`／`287 node options…` |
| ② 退 Play 后（取证会话） | 实错误 **8** 条＝①②③ ＋ **4×`[BuildingFactory] SpawnFromSave 冲突`** ＋ `not cleaned up …[SpriteAnimatorDriver]`（另 4 条 `[HH341R] §F` 是探针 **Log** 回显，被 `read_console` 的**文本**过滤命中，非错误） |
| ③ 空白对照复跑（只进 Play、不建局、不读档） | **3** 条＝①②③ |
⇒ 归因：①②③＝常驻既有；④~⑦＝**`SaveManager.Load` 触发**；⑧＝建局+退局既有。**本批无新增代码路径错误**（探针仅调用既有公开接口）。

### ④~⑦ 逐字复现（**既有已登记**）
4 条冲突坐标 `(75,28)／(71,29)／(45,110)／(42,112)`，`defId=farm/Warehouse`，栈＝`BuildingFactory.SpawnFromSave`(`BuildingFactory.cs:307`) ← `SaveManager.Load`(`SaveManager.cs:363`)。
⭐ **与 `HH.314`（2026-09-20）报告中的 4 条坐标完全一致**（仅随机 GUID 不同）⇒ 该现象已被登记为挂账 **`DZ-新`**（`多Agent交接/_任务队列.md:107`「读档期 4 条 `[BuildingFactory] SpawnFromSave 冲突`」· 归**读档重建路径专项**·⏳ 待派）；本次为**第 2 次独立复现**，可作该专项的复现证据。

---

## 八、纪律核对（红线逐条）

| 红线 | 状态 |
|---|---|
| ⛔ 给任何类型加 `[Serializable]`／新增入档路径／改契约文件 | **未违反**：本批零改动（`Assembly-CSharp.dll` mtime 未变）；§五 为清单，⛔ 未动手 |
| ⛔ 改 `最高优先级文档/`／四本账本／场景／旧资产键／`AI.Core` | **未违反**（未触碰任何一本；场景 mtime 仍 2026-09-15） |
| ⛔ 触碰 `WorldGatherSource` 或任何源的生产接线 | **未违反**：只经既有玩家入口概念读在册表；⛔ 未切源、未改源 |
| ⛔ 用 Edit 探针冒充进局证据 | **未违反**：全部行为读数取自 Play 内真实局（正门进局）；§2.3 的 JsonUtility 试验**仅为格式性试验**，⛔ 未作为"读档能力"证据 |
| ⚠️ 进局须走正门 + 退 Play + `L-95` 三段法 | **已遵守**（§七） |
| 临时探针落 `Editor/Smoke` 并收工删除 | **已执行**：`Valley_HH341R_RestoreProbe.cs` ＋ `.meta` 已删（残留检查 = 0）；日志保留 |
| 扫描基线声明扫描域 | 已声明（本节全部扫描域＝`Valley Rampart/Valley Rampart/Assets/_Game/**` 或全 `Assets`，逐条标注） |
| ⛔ 未提交、未 push | 已遵守（报告落盘即可，由事务端入库） |

---

## 九、产物清单

```
多Agent交接/执行端/HH.341_D1前置门取证_交付报告.md   ← 本文件
Valley Rampart/Logs/hh341_d1gate/
├─ restore_probe.txt / restore_probe_20260926_144206.txt   ← 进局取证全量日志（§A~§G）
├─ save_hh341_evi.json                                     ← 存档副本（41 818 B，可复算扫描②）
└─ console_l95.txt                                         ← L-95 三段法原始读数与 4 条冲突原文
```
（探针源与 `.meta` 已删除，去向＝本批临时取证工装；日志保留）

---

## 【应登记项】

> 以下为执行端应交策划端回填的文本块（⛔ 执行端未改任何账本）。

```
HH.341 | D1 前置门·§九-5 读档重验可行性取证（只读） | 执行端 | 取证完成：新协议零 [Serializable]（13 处待补）· 存档零任务承载 · ITaskRestoreValidator 0 实现 0 调用 · 旧链任务态零入档 | _Game/Editor/Resources/Scenes 零改动 · 运行时 dll 未变 · 临时探针已删 | 待策划端裁 §九-5 口径（甲：降级为"重建语义"／乙：按 8 文件 10 项代价施工）
HH.341 | 待裁政策项 ×4 | 执行端 | ①卡片是否入档 ②`+∞` deadline 哨兵口径（实测 JsonUtility 可往返但写出非标准 JSON）③读档后 tick 基准（`unreachableBlockedUntilTick`）④卡片台账持有者与 §九-6「单管理器」边界 | 裁决后方可立 D1 首片施工 | 待裁
HH.341 | 观察登记·既有挂账复现 | 执行端 | 读档期 4 条 `[BuildingFactory] SpawnFromSave 冲突` 逐字复现（坐标 75,28／71,29／45,110／42,112 ＝ `HH.314` 同坐标） | 对应当前挂账 `DZ-新`（`_任务队列.md:107`）·本次为第 2 次独立复现 | 待登记（⛔ 不并入本批）
HH.341 | 未取得项 | 执行端 | 读档后跨档悬空绑定面（`Building.currentWorkers`／`NPCBrain.IsKingdomTaskWorker` 对拍）本批未取得 | 如需，另批取证 | 待裁
```

---

## 附：本批扫描命令与读数（扫描域逐条声明）

| 扫描 | 命令 | 扫描域 | 读数 |
|---|---|---|---|
| 新协议 `Serializable` | `rg -n 'Serializable' Assets/_Game/Systems/AI/TaskScheduling` | 生产域（6 协议文件） | **0** |
| 新协议外接线 | `rg -n '<9 类型名>' Assets/_Game --glob '!**/TaskScheduling/**'` | 生产域 | **0 行/0 文件** |
| `ITaskRestoreValidator` | `rg -n 'ITaskRestoreValidator' Assets` | 全 Assets | **2**（1 定义 ＋ 1 注释） |
| `_npcTaskMap` 住所 | `rg -n '_npcTaskMap' Assets/_Game` | 生产域 | **16 处（全在 `TaskScheduler.cs`）** ＋ `UnitController.cs:37` 注释 1 处 |
| 全仓 `SaveId`/`SaveIdPrefix` | `rg -n 'string SaveId\|SaveId =>\|SaveId \{' Assets/_Game` | 生产域 | **22 处取值点，任务相关 0** |
| 存档关键字 | 存档原文 `IndexOf` 计数（探针内） | 存档产物 | **全 0**（见 §3.2 ②） |


## 十一、主策划端补落终裁（HH.341 D1 前置门，2026-09-26）

### 一、核心裁决：采用第四案“重建重验”，不做卡片入档

**结论：TaskCard 不入档。沿用 HH.338 已裁的“卡片不入档”契约；本阶段把 §九-5 的语义改为“读档后重建重验”，不采用甲案的卡片序列化，也不把缺口简单顺延到 D2。**

理由：

- [实读] 13 处 Serializable 缺口、存档 JSON 任务承载 0 命中、读档后任务数 11→5→6，证明当前系统语义是重建，不是恢复。
- [实读] ITaskRestoreValidator 在 TaskCardProtocol.cs:559，当前实现 0、调用 0；若现在补序列化、ISaveable 和新台账，会直接扩大范围并推翻 HH.338 的既有契约。
- [推断] M5-D 应验证“读档后旧任务状态不悬空、源能重建合法卡片、配对和预定重新闭合”，而不是伪造逐卡恢复。

因此：

- 不补 13 处 Serializable；
- 不新增卡片持久化台账；
- 不新增 ISaveable 包装；
- 不把 §九-5 降为 D2 才处理的债务；
- 将 §九-5 改记为“读档后重建重验”。

### 二、D1 前置门当前状态

**结论：当前 D1 前置门为“口径成立、证据未闭”，暂不放行首片生产接线。**

已有的 11→5→6 读档后重建读数可以采信为重建事实，但还缺跨档悬空绑定面对拍：

- Building.currentWorkers；
- NPCBrain.IsKingdomTaskWorker；
- 配对表与预定表；
- 读档后重新广告的 TaskCard 与旧工人占用状态。

该项并入 D1 首片判据，作为一个独立子门；不另立脱离 D1 的任务。补齐前允许继续做只读准备、探针设计和回退点准备，不得改 WorldGatherSource 的生产接线。

### 三、数字口径纠正

执行端报告中的 _npcTaskMap 16 处且“全在 TaskScheduler.cs”口径不成立，须补正后再作为正式数字引用：

- 事务端复算：19 处总命中；
- 代码命中 17 处；
- 注释命中 2 处，其中 UnitController.cs:37 为注释；
- 报告必须列出 17 个代码命中的文件与行号，不能继续写 16 处全在 TaskScheduler.cs。

该项不推翻“存档零任务承载”主结论，但属于数字纪律补证。

## 十二、四条待裁政策

### 1. 卡片是否入档

**判定：不入档。**

这是对 HH.338 既有契约的延续，不是新增定值。§九-5 按“重建重验”执行。

### 2. +∞ deadline 哨兵

**判定：持久化表示不得写 PositiveInfinity。**

运行时仍可把“无截止时间”解释为 PositiveInfinity，但未来若进入存档或跨工具交换，必须使用明确的无截止标志与有限数值；不接受把 Infinity 写进 JSON。具体采用显式 hasDeadline，还是既有字段上的 -1 哨兵，属于序列化格式细节；若要固定具体数值，列为 A1 待用户拍，本裁不替用户定 -1。

当前 M5-D 不做存档代码改造。

### 3. 读档后的 tick 基准

**判定：不复用绝对 tick。**

当前卡片不入档，读档后旧 cooldown 状态随卡片重建丢弃；新卡按当前 tick 和既定 5 tick 政策重新建立资格。若未来扩展卡片持久化，保存“剩余 tick”或等价相对时长，禁止保存跨会话复用的绝对 tick。

这不改变已经裁定的 5 tick 数值。

### 4. 卡片台账与单管理器

**判定：本批不新增生产卡片台账。**

如果未来确需台账，它必须归属于唯一任务管理器内部，并吸收或替代 ScheduleCenterStub.cs:56 的 _crewAssignments；不得新建第二个或第三个配对载体。TaskBindingManager 继续负责既有配对/预定释放契约，不另立并行管理器。

当前 D1 只允许日志级、探针级临时记录，不形成生产持久台账。§九-6 仍未达成。

## 十三、挂账升格：DZ-新

**判定：升格为“待修”，另立修复批。**

理由：

- [实读] BuildingFactory.cs:307 的 SpawnFromSave 冲突日志；
- [实读] SaveManager.cs:363 的调用链；
- [实读] 本批 4 个坐标与 HH.314 逐字一致：75,28／71,29／45,110／42,112；
- [推断] 同坐标、同文本、第二次独立复现，足以定性为确定性存档生成缺陷，不再按随机噪声挂账。

本批不修复、不改账本、不自取 D 号；由事务端另立修复登记。

## 十四、D1 首片最终准入口径

WorldGatherSource 生产接线必须同时满足：

1. §九-5 的“重建重验”口径已写入报告；
2. 跨档绑定面对拍通过，确认 currentWorkers、IsKingdomTaskWorker、配对和预定均不悬空；
3. _npcTaskMap 数字补证完成，17 个代码命中与 2 个注释命中分列；
4. 同帧配对采用调用序或状态快照取证；
5. 同目标连发按 taskId 聚合，并通过最多 2 次尝试、终态封口、无重复活动卡判据；
6. 生产域旧链基线无未解释下降；
7. Building、UnitController 不进入首片改动面。

## 十五、应登记项（供事务端落账）

HH.341 | D1 前置门 | §九-5 改为“读档后重建重验”，TaskCard 不入档；当前口径成立但跨档绑定面对拍与 _npcTaskMap 数字补证未闭，WorldGatherSource 生产接线暂不放行；DZ-新 升格待修另立批次 | 不写 D 号
