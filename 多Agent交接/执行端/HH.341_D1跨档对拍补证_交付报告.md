# HH.341 · D1 首片子门 —— 跨档绑定面对拍 + `_npcTaskMap` 数字补证 + §九-5「重建重验」读数 · 交付报告

> 执行端 ｜ 阶段：**D1 首片子门（只读取证）** ｜ 日期 2026-09-26
> 性质：⭐ **只读取证**。⛔ 零代码改动、零资产改动、零配置改动（本批未改任何 `.cs`/`.asset`/`.prefab`/`.unity`）。
> 沿用 `HH.341`，⛔ 未取新号、未写 D 号；⛔ 未碰 `WorldGatherSource` 或任何源的生产接线。

---

## 〇、开工回执（四项）

1. **阶段**：D1 首片子门（跨档绑定面对拍补证）。
2. **确认零改动**：
   - `git diff --name-only -- Assets/_Game Assets/Editor Assets/Resources Assets/ProjectSettings` ⇒ **空**；
   - `Assembly-CSharp.dll` mtime ＝ **2026-09-25 23:58:01（未变）** ⇒ 生产运行时程序集字节未变；
   - `GameScene.unity` mtime ＝ **2026-09-15 17:17:07**（远早于本会话）⇒ 未写场景；
   - `git diff --check` ⇒ **空**；⛔ 未提交、未 push。
3. **五组对拍初步方向**：①`Building.currentWorkers` 全库**零使用**（读数常 0，须防"零悬空"被误读为"正确释放"）②`IsKingdomTaskWorker` 真值数 ＝ 在册任务数（`HH.319` 既有判据口径）③④新协议两表在**生产面零实例**（不可读）⇒ 另给**影子面**读数 ⑤跨档同键（`npcId` 数字复用）**必须按任务实例与 source InstanceID 核**，不可按键判。
4. **进局入口与退 Play 方案**：MCP `manage_editor play`（单独下发）→ 等 `ready_for_tools` → `execute_menu_item` 触发临时探针 → 探针内 `TestHarnessApi.EnterTestRun(seed=31418)` → `SaveManager.Save` → `Load` → 五组快照对拍 → `ExitTestRun()` → `EditorApplication.ExitPlaymode()`；Console 走 **`L-95` 三段法**。

---

## 一、结论速览

| 任务 | 结论 | 标级 |
|---|---|---|
| **1 跨档绑定面对拍** | ⭐ **读档后不存在悬空绑定**（判据 H1~H7 在「存档前／读档后·立即／读档后·4s」**三个时点全 0**）⇒ **无需停手报裁** | `[实读]` |
| **2 `_npcTaskMap` 数字** | **19 总 = 17 代码 + 2 注释**（代码 17 处**全在** `TaskScheduler.cs`；注释 2 处＝`TaskScheduler.cs:12` ＋ `UnitController.cs:37`）⇒ 与事务端复算**逐位一致**；⛔ 前版「16 处」作废（根因见 §三） | `[实读]` |
| **3 §九-5「重建重验」** | 三读数**全部成立**：①旧任务不悬空（H1~H7＝0）②源重新广告合法任务（读档后 10 条在册**全为新实例**、source **全为重建实例**、105 条派发日志）③配对与预定**重新闭合**（影子面：预定 1/配对 1 → 终态 **0/0**、不变量 0 违规；政策读数 `tickNow+5` 一致） | `[实读]`（③含影子面标注） |

---

## 二、任务 1：跨档绑定面对拍（五组 · 逐组判定）

**时点**：存档前（帧 8619）／读档后·立即 2 帧（帧 8621）／读档后·4s（帧 8720）。存活单位恒 25。

| # | 组 | 存档前 | 读档后·立即 | 读档后·4s | 判定 |
|---|---|---|---|---|---|
| ① | `Building.currentWorkers` | 建筑 0／成员 0／悬空 0 | 0／0／0 | 0／0／0 | **不悬空**（⚠️ 见下方口径注） |
| ② | `NPCBrain.IsKingdomTaskWorker` | true=**11**（不在册=0） | true=**9**（不在册=0） | true=**10**（不在册=0） | **不悬空** |
| ③ | 配对表 | 生产面**无实例可读**；旧链对应物 `_npcTaskMap`=**11** | 生产面无实例；旧链=**9** | 生产面无实例；旧链=**10** | **不悬空（旧链配对无孤儿）** |
| ④ | 预定表 | 同上（生产面无实例）；旧链对应物＝源独占去重（`HasAssignedTaskForSourceType`，由在册表派生） | 同③ | 同③ | **不悬空** |
| ⑤ | 重新广告的任务 × 旧工人占用 | 在册 npcId ＝ {3,9,15,13,14,16,…}（11 条） | 在册 npcId ＝ {5,4,11,10,12,1,…}（9 条） | 在册 npcId ＝ {1,9,5,2,8,6,…}（10 条）<br>**新任务实例=10/10｜source 为重建实例=10/10｜与存档前同 InstanceID=0** | **不悬空**（同键＝`npcId` 数字复用，非跨档残留；见 §五-②） |

**口径注（⛔ 防误读）**：`Building.currentWorkers` 三时点恒 0 **不是**"正确释放"的证据 —— 该列表 `readonly`（`Building.cs:156`）且**生产域零写入**（`HH.324` 已记「零写入＝死载体」）⇒ 恒 0 ＝ **零使用**。本组判据因此只能给"**无内容可悬空**"，⛔ 不等于"跨档释放正确"。

**悬空判据 H1~H7 定义与读数**（机械口径，见 `cross_probe.txt §G`）：
- H1 `_npcTaskMap` 值 == null｜H2 值.`source` 假 null（`as UnityEngine.Object`）｜H3 值.`source.IsValid == false`｜H4 `_npcStateMap` 与 `_npcTaskMap` **键不一致**（双向）｜H5 `IsKingdomTaskWorker==true` 但 npcId ∉ `_npcTaskMap`｜H6 `_npcTaskMap` 键的 `_npcBrainMap` 值假 null（旧 npcId 孤儿）｜H7 键 ∉ 当前存活单位集合

| 时点 | H1 | H2 | H3 | H4 | H5 | H6 | H7 | 合计 |
|---|---|---|---|---|---|---|---|---|
| 存档前 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | **0** |
| 读档后·立即 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | **0** |
| 读档后·4s | 0 | 0 | 0 | 0 | 0 | 0 | 0 | **0** |

⇒ **任务 1 验收条件满足**（读档后不存在悬空绑定）⇒ ⛔ 未触发停手条件。

---

## 三、任务 2：`_npcTaskMap` 数字补证

**扫描域**：`Valley Rampart/Valley Rampart/Assets/_Game/**`（生产域）｜**计数单位**：**行**（一条命中行）与**文件**。
**命令**：`rg -n '_npcTaskMap' Assets/_Game --glob '*.cs'`（行级）＋ `rg -l …`（文件级）。

**读数：行级 = 19｜文件级 = 2**（`TaskScheduler.cs`、`UnitController.cs`）⇒ **19 总 ＝ 17 代码 ＋ 2 注释**。

### 3.1 代码命中 17 处（**全在** `TaskScheduler.cs`）
| # | 行 | 所在成员 | 性质 |
|---|---|---|---|
| 1 | `:56` | 字段声明 | `private readonly Dictionary<int, KingdomTask> _npcTaskMap = new …` |
| 2 | `:159` | `HasWorkerAssigned` | 遍历（判源+`Working`） |
| 3 | `:174` | `CountAssignedWorkers(source)` | 遍历计数 |
| 4 | `:188` | `CountAssignedWorkers(source, filter)` | 遍历计数（带过滤） |
| 5 | `:199` | `AbandonTask` | `TryGetValue` 读 |
| 6 | `:206` | `OnNpcDied` | `TryGetValue` 读 |
| 7 | `:219` | `OnPathFailed` | `TryGetValue` 读 |
| 8 | `:228` | `OnBuildingDied` | 遍历收集 stale |
| 9 | `:235` | `OnBuildingDied` | 索引取值 → `Abandon` |
| 10 | `:278` | `Tick`（idle 收集） | `ContainsKey` 幂等判（已占用不重派） |
| 11 | `:397` | `Dispatch` | ⭐ **唯一写入点** |
| 12 | `:466` | `UpdateAssignedTasks` | `Count == 0` 早退 |
| 13 | `:472` | `UpdateAssignedTasks` | 遍历快照（`new Dictionary<int,KingdomTask>(…)`） |
| 14 | `:658` | `UpdateAssignedTasks`（stale 收尾） | `TryGetValue` 读 |
| 15 | `:720` | `ClearNpc` | `Remove`（唯一移除点） |
| 16 | `:1271` | `HasAssignedTaskForSourceType` | 遍历（源独占去重判据） |
| 17 | `:1284` | `CountAssignedForType` | 遍历计数（规模派工） |

### 3.2 注释命中 2 处
| # | 位置 | 原文性质 |
|---|---|---|
| 1 | `TaskScheduler.cs:12` | 类头文档注释第 5 条（「对在册 NPC 维护 `_npcTaskMap/_npcStateMap` 供查询…」） |
| 2 | `UnitController.cs:37` | 行注释（「===== QQQ.2 T17：NPC 唯一 ID（任务调度器 `_npcTaskMap` 键）+ 死亡事件 =====」） |

### 3.3 前版「16 处」的差异根因（⛔ 换工具前先说清口径）
- **根因 1（漏计）**：前版用 `rg -n …` 输出**目测清点**，未用 `Measure-Object -Line` 计数 ⇒ 把 18 行 `TaskScheduler.cs` 数成 15~16 ⇒ **少报 3 行**。
- **根因 2（未分类）**：未做**代码/注释分类** ⇒ `TaskScheduler.cs:12` 的文档注释被当代码、`UnitController.cs:37` 的注释被单独提及却未计入总数。
- **根因 3（措辞与读数自相矛盾）**：文件级读数恒为 **2 文件**，而前版措辞「16 处命中**全在** `TaskScheduler.cs`」与之冲突。
- ⇒ 本次以 `-n` 行级输出逐条分类，**19 总／17 代码／2 注释** 与事务端复算一致；**前版「16 处·全在 `TaskScheduler.cs`」作废**。

**非生产域对照（⛔ 不入生产基线）**：`Assets/Editor/**` 命中 **10 行 / 2 文件**（`Valley_HH319_F1LongRun.cs` ×8、`Valley_HH319_M1F_Probe.cs` ×2，均为反射只读探针）。

---

## 四、任务 3：§九-5「重建重验」正式读数

> 口径：核心裁决已把语义定为 **读档后重建重验**（⛔ 不验"逐卡恢复"；`TaskCard` 不入档＝`HH.338` 已裁契约，本批未做序列化/台账/`ISaveable` 包装）。

### 读数①【读档后旧任务不悬空】✅
＝ 判据 **H1~H7 三时点全 0**（§二表）。另附：存档前在册 11 → 读档后立即 9 → 4s 后 10，**无 null／无假 null source／无失效 source／无键不一致／无孤儿 brain／无不在存活集的键**。

### 读数②【源重新广告合法任务】✅
- 读档后 4s 在册 **10** 条：**新任务实例 = 10/10**（`GetHashCode` 与存档前集合无重合）｜**source 为重建后新实例 = 10/10**（`GetInstanceID()` 不在存档前集合）｜**与存档前同 InstanceID = 0**。
  例：`npcId=1 task=Transport state=MovingToDest src=Building srcInstId=-16780`（存档前同键为 ChestEntity/-16360）。
- 窗口内生产日志 **`[TaskScheduler] 派发 …` = 105 条**（读档后新一轮广告→收集→派发正常运转）。
- 源广告口（生产域唯一"只读无副作用"型 `WorldGatherSource.TryAdvertiseTask`）：在册该型 **0**（本局无采集立案 ⇒ 0 属预期，⛔ 不作正面证据；正面证据为上两条）。
⇒ 结论：**源在重建后重新广告并产出合法任务**，链条自洽。

### 读数③【配对与预定重新闭合】✅（生产面 + 影子面）
- **生产面**：`TaskProtocolRuntime`/`TaskBindingManager` 在活世界**无实例可枚举**（纯 C# 类 · ⛔ 非 MonoBehaviour/ScriptableObject · ⛔ 无单例 · ⛔ 非 `ISaveable`；生产域对这两个类型名引用命中 = 0，除 6 个协议文件内部）⇒ 生产面两表读数恒为"**无实例可读**"，本批**不给生产正面读数**；仅记「零实例 ⇒ 无悬空可言」。⚠️ 明确列报：该项**须 D1 接线后复验**。
- **影子面**（⛔ 探针自建实例、非生产接线 · 仅证机制在活世界**读档后**可用）：
  | 步 | 调用 | 卡 state | 预定 | 配对 | 不变量违规 |
  |---|---|---|---|---|---|
  | 建卡+提交 | `Submit(…, targetAllowsMultiple:false)` → `Ok` | `Pending` | **1** | 0 | — |
  | 政策（读档后资格重建） | `MarkUnreachable(card, tickNow=129)` | `Pending` | 1 | 0 | — |
  | 配对 | `Assign(9999)` → `None` | `Assigned` | **1**（行 multiAllowed=false） | **1**（workerId=9999） | **0** |
  | 闭合 | `Abort(ExternalAbandon)` → `None` | `Aborted` | **0** | **0** | **0** |
- **政策读数**（⛔ 只调用、不重新定值）：`unreachableBlockedUntilTick = tickNow + 5`（129→134）｜当前 tick 可派＝**False**｜`tickNow+5` 可派＝**True** ⇒ **与既定政策一致**（「读档后不复用绝对 tick，按当前 tick + 既定 5 tick 重建资格」）。
  ⚠️ `tickNow` 由探针按 `Time.time / tickInterval` 换算（探针侧换算口径，非生产 tick 源），如实声明。

---

## 五、进局读数 / Console（`L-95` 三段法）/ seed

- **入口**：`TestHarnessApi.EnterTestRun`（正门）；**seed = 31418**（worldSeed=mapSeed=31418，difficulty=2，`WorldSize.Small`）；槽 `hh341_face`。
- 入口读数：ActiveMap=**128×128**、State=Playing、`timeScale=15`、day=1；等「进行中任务 ≥1」命中帧 **8619**（在册 11）。
- 存档：`SaveManager.Save("hh341_face")=**True**`（saveVersion=3、模块 55）。
- 读档：`SaveManager.Load("hh341_face")=**True**`。
- 退出：`ExitTestRun` 已调用（`maximumDeltaTime=0.33`／`shadows=All`／`vSync=1` 复原）＋ `EditorApplication.ExitPlaymode()` ⇒ MCP 复核 `play_mode.is_playing=**false**`。

| `L-95` 段 | 读数 | 归因 |
|---|---|---|
| ① Play 内（探针下发前） | **3** 条：`ruler 缺脚本`／`No Theme Style Sheet`／`287 node options` | 常驻既有 |
| ② 退 Play 后（对拍会话） | 13 条 = 实错误 **8** 条：①②③ ＋ **4×`[BuildingFactory] SpawnFromSave 冲突`** ＋ `not cleaned up …[SpriteAnimatorDriver]`（另 4 条为探针 §I 回显＝**Log**，被文本过滤命中） | ④~⑦ 由 `SaveManager.Load` 触发；⑧ 建局+退局既有 |
| ③ 空白对照复跑（只进 Play、不建局、不读档） | **3** 条＝①②③ | ⇒ ④~⑧ 与空白 Play 无关 |

**④~⑦ 逐字复现**：坐标 `(75,28)／(71,29)／(45,110)／(42,112)`，`defId=farm/Warehouse`，栈 `BuildingFactory.cs:307 ← SaveManager.cs:363`。
⭐ 与 `HH.314`（2026-09-20）**同 4 坐标**（仅随机 GUID 不同）⇒ 本次为**第 3 次逐字复现**；该现象已登记为挂账 **`DZ-新`**（`多Agent交接/_任务队列.md:107`）⇒ ⛔ 非本批引入，本批**只登记不处置**。

---

## 六、未取得项 / 观察登记（⛔ 非判定）

1. **未取得**：新协议两表（配对/预定）的**生产面**正面读数 —— 因新协议在生产域**零接线、零实例**（`HH.338`/`D873` 已裁不入档、不接线）⇒ 该两项在 D1 接线后**必须复验**，本批影子面读数⛔ 不得替代。
2. **观察**：`npcId` **不入档**（`UnitSaveData` 无 npcId 字段）⇒ 读档后单位以同序列重建，`_npcTaskMap` 出现 **6 个跨档同键**；本批按**实例身份**核验为「全部新实例、source 全为重建实例」⇒ 判"非残留"。⛔ 但若未来出现"按键判残留"的判据，会**假阳性** —— 建议 D1 任务书明确"按实例身份判"的口径。
3. **观察**：`Building.currentWorkers` 三时点恒 0（零写入死载体，`HH.324` 已记）⇒ 该组判据**不具备鉴别力**，建议 D1 不复用该载体。

---

## 七、零改动 / 探针去向 / 产物

- **零改动**（三重）：`_Game`/`Editor`/`Resources`/`ProjectSettings` git diff ⇒ **空**；`Assembly-CSharp.dll` mtime **未变**；`GameScene.unity` mtime 仍 `2026-09-15`；`git diff --check` ⇒ 空。
- **临时探针去向**：`Valley Rampart/Assets/Editor/Smoke/Valley_HH341B_CrossSaveProbe.cs` ＋ `…cs.meta` **均已删除**（残留检查 = 0）；日志保留。
- ⛔ 未提交、未 push；⛔ 未改 `最高优先级文档/`、四本账本、`AI.Core`；⛔ 未碰 `Building.cs`／`UnitController.cs`／任何源的生产接线（只读反射 + 既有 `SaveManager` 公开接口）。

**产物**：
```
多Agent交接/执行端/HH.341_D1跨档对拍补证_交付报告.md   ← 本文件
Valley Rampart/Logs/hh341_d1gate/
├─ cross_probe.txt / cross_probe_20260926_155256.txt   ← 五组对拍 + H1~H7 + §九-5 三读数全量日志
├─ cross_l95.txt                                        ← 本批 Console L-95 三段法原始读数
├─ restore_probe.txt（上批）／save_hh341_evi.json（上批）／console_l95.txt（上批）
```

---

## 【应登记项】

> 交策划端回填（⛔ 执行端未改任何账本）。

```
HH.341 | D1 首片子门·跨档绑定面对拍（只读） | 执行端 | 对拍完成：读档后**无悬空绑定**（H1~H7 三时点全 0）；五组逐组判定见报告 §二 | _Game/Editor/Resources/Scenes 零改动 · 运行时 dll 未变 · 临时探针已删 | ⛔ 未触发停手条件
HH.341 | 数字勘正·`_npcTaskMap` | 执行端 | 生产域 = **19 总（17 代码 + 2 注释）/ 2 文件**；代码 17 处**全在** `TaskScheduler.cs`；注释 = `TaskScheduler.cs:12`、`UnitController.cs:37` | ⛔ 前版「16 处·全在 `TaskScheduler.cs`」**作废**；差异根因＝目测漏计 3 行 + 未分类 + 措辞与文件级读数矛盾 | 待登记（含前一版勘正标记）
HH.341 | §九-5「重建重验」三读数 | 执行端 | ①旧任务不悬空 ✅ ②源重新广告合法任务 ✅（读档后 10/10 新实例、source 全为重建实例、105 条派发日志）③配对与预定重新闭合 ✅**仅影子面**（预定1/配对1→终态0/0、不变量0；政策 `tickNow+5` 一致）| ⚠️ ③**生产面零实例**⇒ 须 D1 接线后复验，影子面⛔ 不得替代 | 待裁决
HH.341 | 观察登记 | 执行端 | `npcId` 不入档 ⇒ 读档后出现 6 个跨档同键；本批按**实例身份**核为全新建 ⇒ 非残留 | 建议 D1 判据明确「按实例身份判，⛔ 不按键判」 | 待裁
HH.341 | 观察登记 | 执行端 | `Building.currentWorkers` 三时点恒 0（`Building.cs:156` readonly·生产域零写入·`HH.324` 已记）⇒ 该组判据**无鉴别力** | 建议 D1 不复用该载体 | 待裁
HH.341 | 既有挂账复现（第 3 次） | 执行端 | 读档期 4 条 `[BuildingFactory] SpawnFromSave 冲突`：坐标 (75,28)/(71,29)/(45,110)/(42,112) 恒定，栈 `BuildingFactory.cs:307 ← SaveManager.cs:363` | 对应挂账 `DZ-新`（`_任务队列.md:107`）·⛔ 非本批引入 | 待登记
```

---

## 附：本批扫描命令与原始读数（扫描域/计数单位逐条声明）

| 扫描 | 命令 | 扫描域 | 计数单位 | 读数 |
|---|---|---|---|---|
| `_npcTaskMap`（生产域） | `rg -n '_npcTaskMap' Assets/_Game --glob '*.cs'` | 生产域 | 行 / 文件 | **19 行 / 2 文件** |
| 同上（文件级） | `rg -l '_npcTaskMap' Assets/_Game --glob '*.cs'` | 生产域 | 文件 | **2**（`TaskScheduler.cs`、`UnitController.cs`） |
| `_npcTaskMap`（非生产对照） | `rg -n '_npcTaskMap' Assets/Editor --glob '*.cs'` | Editor/Smoke（⛔ 非生产基线） | 行 / 文件 | 10 行 / 2 文件 |
| 新协议类型（生产域·排除 6 协议文件） | `rg -n '<9 类型名>' Assets/_Game --glob '!**/TaskScheduling/**'` | 生产域 | 行 / 文件 | **0 / 0** |
| 全 Assets（`TaskScheduler.Instance\|HasInstance`） | `rg -n … Assets` | 全 Assets（含 `Editor/Smoke`） | 行 / 文件 | 149 / 27（⛔ **非生产基线**，仅历史对照） |


## 十六、主策划端补落终裁（HH.341 D1 首片，2026-09-26）

### 一、D1 首片放行结论：有条件放行

**结论：允许进入 WorldGatherSource 首片施工；不直接判首片验收绿。**

放行条件是硬条件，不是建议：

1. 施工只覆盖 WorldGatherSource 与必要的兼容接缝；
2. 生产面必须出现真实新协议实例，影子面只能作辅助诊断；
3. 工人占用必须从旧 TaskScheduler._npcTaskMap 迁到 TaskBindingManager 的配对/预定表；
4. 同帧配对必须用单调调用序或前后状态快照取证；
5. 同目标连发必须按 taskId 聚合，满足最多 2 次尝试、释放后重派、终态封口、无重复活动卡；
6. 首片切换后必须重跑生产面链路、旧链基线和跨档重建重验；
7. Building、UnitController、ScheduleCenterStub._crewAssignments 不得混入首片改动面。

### 二、首片施工后的生产复验

首片切换完成后，必须在真实 WorldGatherSource 生产面取得：

- 新 TaskCard 实例数大于 0；
- Submit、Pair、Reservation、Move、Complete/Abort、Release 全链可追踪；
- TaskBindingManager 配对与预定表在终态归零；
- WorldGatherSource 的工人占用不再以 _npcTaskMap 作为权威；
- 同一 taskId 的尝试数、retryConsumedCount、释放数和终态数可逐项对拍；
- 读档后重建重新广告，且跨档绑定不悬空；
- 旧生产源基线的变化逐项解释；
- 影子面与生产面分列，禁止互相替代。

### 三、Building.currentWorkers 定性

**结论：定性为死载体；D1 不启用、不修复、不作为验收证据。**

理由：

- [实读] Building.cs:156 声明 currentWorkers；
- [实读] Building.cs:1439、1444、1446-1448、1469 存在读写路径；
- [实读] 进局运行期恒为 0；
- [推断] 它没有承担当前工人占用的权威职责，空集对拍不具鉴别力。

D1 不得为了让该组读数“变得有意义”而临时填充 currentWorkers。后续清理批次可在完成引用审计后删除该死载体；本片不改它。

### 四、工人占用迁移：必须迁入 TaskBindingManager

**结论：必须迁移，不能保留无载体状态。**

对 WorldGatherSource：

- TaskBindingManager 配对/预定表是新权威载体；
- _npcTaskMap 不得再接收该源的新权威写入；
- 若为过渡兼容保留镜像，必须逐次断言与 TaskBindingManager 一致，并在终态同时清理；
- currentWorkers 不参与迁移，也不参与验收；
- 终态、超时、读档重建三条路径都必须验证占用释放。

这是首片必带项，不是可选项。

### 五、currentWorkers 组对拍处置

**结论：接受“无鉴别力”声明，不为该组重取释放结论。**

第①组只能作为死载体诊断记录；不能证明释放正确、不能证明不悬空。首片结论改由：

- TaskBindingManager 配对/预定生产读数；
- NPCBrain.IsKingdomTaskWorker；
- _npcTaskMap 的迁移前后对照；
- 终态与读档重建后的四组占用读数

共同承担。不得把第①组恒 0 写成通过证据。

### 六、DZ-新修复批时序

**结论：立即立修复批，首片正式进局回归前先修复并重新取基线。**

原因：

- SpawnFromSave 冲突已第 3 次逐字复现；
- 四个坐标与 HH.314 完全一致；
- 每次进局都会污染 Console 与存档对拍。

修复批与 D1 任务分开，不混改、不共用提交面；D1 只在修复后重新完成一次生产基线和 Console 三段对照。事务端负责另立修复登记，本裁不自取 D 号。

### 七、应登记项（供事务端落账）

HH.341 | D1 首片 | 有条件放行 WorldGatherSource 施工；必须将工人占用迁入 TaskBindingManager，currentWorkers 定性为死载体并排除验收；生产面接线后须重跑配对、重派、终态、读档重建与旧链基线；DZ-新 立即立修复批并先于首片正式回归 | 不写 D 号
