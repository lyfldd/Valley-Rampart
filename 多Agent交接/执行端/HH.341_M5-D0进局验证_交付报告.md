# HH.341 · M5-D「乙加」D0 进局验证 —— 交付报告

> 执行端 ｜ 阶段：**D0（只读进局验证门）** ｜ 日期：2026-09-26
> 任务书：`多Agent交接/策划端/HH.341_M5-D乙加_任务书.md`（215 行，已全文直读）
> 结论：**D0 达成**——真实游戏局内跑通完整任务链 1 条（另有异常回收链 1 条），逐步读数齐全，退 Play 成立，断言 **25/25，失败 0**，新增 error **0**。
> ⛔ 本批**未切任何源**、未进 D1、未取 D 号。

---

## 〇、开工回执（四项）

1. **阶段**：D0（只读进局验证）。D1 未开、未切源。
2. **最终改动文件清单**（收工态，见 §六）：新增临时探针 1 个＋`.meta`（**已删除**）；`Logs/` 日志 4 份＋归档 1 目录。
   ⇒ **`Assets/_Game/**` 零改动**（任务书允许面里的「验证适配文件」**未使用**：观测走只读反射＋订阅生产日志即可，无需生产侧适配文件 ⇒ 新协议零接线保持）。
3. **接缝设计一句话**：在 `TaskScheduler` 单例边界挂**只读观测接缝**——反射读其源集/在册任务表/状态表（⛔ 不回写）＋ 订阅 `Application.logMessageReceived` 捕生产日志，把每条真实任务的生命周期事件翻译成一张 M5-A `TaskCard` 并驱动 M5-B 配对/预定到终态释放。
4. **D0 入口与退 Play 方案**：MCP `manage_editor play`（单独下发）→ 等 `advice.ready_for_tools=true` → MCP `execute_menu_item`（ASCII 菜单路径）触发探针 → 探针内 `TestHarnessApi.EnterTestRun(cfg)` 走正门 → 链闭环 → `TestHarnessApi.ExitTestRun()` → `EditorApplication.ExitPlaymode()` → MCP 侧复核 `play_mode.is_playing=false`。

---

## 一、可复现参数（seed 与等价读数）

| 项 | 值 |
|---|---|
| 入口 | `TestHarnessApi.EnterTestRun`（正门 · `HH.92`） |
| seed | **31418**（`worldSeed = mapSeed = 31418`，difficulty=2，`WorldSize.Small`，raceId=0） |
| 存档槽 | `hh341_d0`（对照局 `hh341_ctrl`） |
| 起局读数 | ActiveMap=**128×128**、State=Playing、`Time.timeScale=15`（= `WorldConfig.time.testSpeedMultiplier`）、day=1、建筑=15、单位=25、EnterTestRun 用时 **0.9s** |
| 链#1 靶格 | (56,62) `Tree`（距王国锚点切比雪夫 8 格；扫描门读 264 次） |
| 链#2 靶格 | (73,61) `Tree`（距锚点 9 格；扫描门读 336 次） |
| 工人来源 | 玩家开局实体（`KingdomConfig.initialWorkerCount=4`）——⛔ 探针**未造任何单位/建筑** |
| 派发读数 | 链#1 → npcId 16（派发时距源 **2.1 格**）｜链#2 → npcId 13（距源 **9.2 格**） |

---

## 二、D0 判据对照（任务书 §5.1）

| 判据 | 结果 | 原始读数（`hh341_d0_probe.txt`） |
|---|---|---|
| 入口成功且退 Play 成功 | ✅ | `EnterTestRun` 完成（§A）｜`ExitTestRun=1`（§H：maximumDeltaTime=0.33/shadows=All/vSync=1 已复原）｜MCP `play_mode.is_playing=false`（两轮均复核） |
| 完整链 ≥1 条 | ✅ | 链#1 全程闭环：广告→收集→配对→推进→终态（完成）→释放 |
| 逐步记录：广告 | ✅ | §C：命令层立案（`PrioritizeHarvestCommand` 有订阅者）⇒ 源进调度器在册集；广告口只读复现 = `type=Gather dest=Treasury res=Wood amount=5 gatherSeconds=2 srcPos=(-3.84,37.76)` |
| 逐步记录：收集 | ✅ | §D 帧 10290：`[TaskScheduler] 派发 Gather 任务 → npcId 16 @ (-3.84, 37.76)`（生产日志）＋ 在册表出现该源任务 |
| 逐步记录：配对 | ✅ | 旧侧：`_npcTaskMap[16] = 该任务`；新侧：`TaskCard#1` `Submit=Ok`（预定=1）→ `Assign(workerId=16)=None`（配对表 workerId=16、配对规模=1） |
| 逐步记录：推进 | ✅ | 帧 10290 `MovingToSource` → 帧 10301 `Working`（卡片同步 `Arrive()=None ⇒ Executing`） |
| 逐步记录：终态 | ✅ | 帧 10305 `[TaskScheduler] 完成 Gather 任务 → npcId 16` ⇒ 卡片 `BeginResolve=None → Complete=None ⇒ Done` |
| 逐步记录：释放 | ✅ | 本任务实例零残留（`_npcTaskMap` 该键已被**新任务**占据，按**实例身份**判否）｜M5-B 预定=0 配对=0｜不变量自检违规=0｜源 `IsValid=False` 且退出在册集 |
| 任务卡 ↔ 旧对象可追踪 | ✅ | 映射行：`npcId=16 旧任务(源格 56,62) ↔ 卡 1`；`npcId=13 旧任务(源格 73,61) ↔ 卡 2` |
| 无未归因中间悬挂 | ✅ | 两条链均达终态；链#2 为**已归因**的异常回收（旧链 `Abandon … reason=Timeout`，同局同类 13 条）⇒ 非悬挂、无配对泄漏 |
| 新增错误数 = 0 | ✅ | 窗口内 error=0；退 Play 后 4 条 error 经**空白对照复现**全部归因为进/退局既有噪声（§五） |

### 链#2（异常回收分支 · 覆盖「完成或异常回收」的另一支）
`派发@10290→MovingToSource` → 帧 10387 `[TaskScheduler] Abandon Gather → npcId 13 reason=Timeout`（30s 段预算；
15x 下 ≈2 真实秒）⇒ 卡片 `Unassign(Unreachable)：配对释放、预定保留（预定=1/配对=0）` → `TryConsumeRetry=None(1/1)`
→ 按 M5-C 政策收口 `Abort(RetryExhausted)` ⇒ `Aborted`、两表归零、目标未变（Tree→Tree、池子 Δ=0）、源仍有效。
⚠️ 口径声明：「retry 重派」读数是 **D1 首片判据（§5.2）**，本批**未验**、不据此声称已验。

### 结构面断言
- `TaskCard` 反射扫 26 个字段 ⇒ **零 Unity 对象/接口引用**（违例 0）＝「配对零引用（§九-1）」的载体侧机械证据。
- M5-C 政策资产在活世界可加载且值等于裁定：`retryMax=1 tick=1 cooldown=5 domain=Kingdom stats=PerTask`。

---

## 三、断言统计（任务书 §六）

- **断言总数 = 25｜执行 = 25｜失败 = 0**（逐条 PASS 见日志 §G；链#2 的 C6「推进」记 `[N/A]`——该链是异常回收支，不适用，不计失败亦不计通过）。
- 说明：首跑曾出现 2 条 FAIL（`C5 配对`、`C8 释放`），核因**探针判据缺陷**（非协议缺陷）：
  ① `C5` 在终态后读 `card.workerId`，而协议 `Complete()/Abort()` 会**清零卡内绑定** ⇒ 判据改为取**配对建立时刻**读数；
  ② `C8` 用 `npcId` 键判残留，而该工人在链结束后**立刻被再派新任务**（日志实证：`该工人当前在册任务=Transport 源=ChestEntity`）⇒ 判据改为按**任务实例身份**（`ReferenceEquals`）判。
  修后复跑即全绿；首跑日志按「同档重跑先归档」纪律存 `Logs/hh341_d0/run1_preassertfix/`，⛔ 不与本跑并列引用。

---

## 四、交付物

| 交付物 | 位置 |
|---|---|
| D0 探针日志（稳定名＋时间戳副本） | `Valley Rampart/Logs/hh341_d0/hh341_d0_probe.txt`（＋`_20260926_140725.txt`） |
| 首跑归档（判据缺陷版·仅供差异复算） | `Valley Rampart/Logs/hh341_d0/run1_preassertfix/` |
| Console 前后对照与归因 | `Valley Rampart/Logs/hh341_d0/console_control.txt` |
| 扫描与 git 证据 | `Valley Rampart/Logs/hh341_d0/evidence_scan_git.txt` |
| 临时探针 | `Assets/Editor/Smoke/Valley_HH341_D0Probe.cs` ＋ `.cs.meta`——**已删除**（见 §六） |

---

## 五、Console：完整堆栈 ＋ 进局前后对照 ＋ 归因

（原始读数见 `console_control.txt`）

| 时点 | error 数 | 条目 |
|---|---|---|
| 进局前（非 Play） | 1 | ③ `287 node options failed to load`（常驻噪声①：陈旧节点名 `InteractableType`/`SpawnerComponent`/`RiftComponent`/`WaterNetwork`＝既往已删类型） |
| **探针窗口内**（订阅捕获） | **0** | error/exception=0（另 warning=16、log=921、`[TaskScheduler]` 生产日志 245 条） |
| 退 Play 后 | 4 | ① `The referenced script on this Behaviour (Game Object 'ruler') is missing!`　② `No Theme Style Sheet set to PanelSettings , UI will not render properly`　③（同上）　④ `Some objects were not cleaned up when closing the scene. … [SpriteAnimatorDriver]` |
| 空白对照 A（无探针·进→退 Play） | 0 | ⚠️ 受 **Clear-on-Play** 影响（进 Play 清空控制台）⇒ 该读数**不作为**归因依据 |
| 空白对照 B（无探针·Play 内读＋退局读） | 3 | ①②③ ⇒ 由「进 Play／建局」链路产生，与探针无关 |
| 空白对照 C（无探针·**只建局**同 seed 同正门链 → 退 Play） | 4 | ①②③④ ⇒ ④ 由「建局＋退 Play」本身产生；点名对象 `SpriteAnimatorDriver` ≠ 探针自建对象 `HH341D0Host` |

**归因结论：本批新增 error = 0。** ①②为既有场景/UI 配置（`GameScene.unity` 在**本会话开始前**即已 working-tree modified，本批零场景改动）；③为已列报常驻噪声；④由空白对照 C 逐字复现。

---

## 六、工作区、编译与临时探针去向

- `git diff --name-only`：`Valley Rampart/Assets/Scenes/GameScene.unity`、`pixel-forge/index.html`、`pixel-forge/server/forge.mjs`、`pixel-forge/server/serve.mjs` —— **全部为本会话开始前既有的脏点**，本批未触及。
  另：`git --no-pager diff --name-only -- Valley Rampart/Assets/_Game Valley Rampart/Assets/Editor` ⇒ **空**（生产域零改动）。
- `git diff --check` ⇒ **空**（无空白错误）。
- 编译证据（⛔「errors=0」不作口头断言，给机械读数）：
  - `Assembly-CSharp-Editor.dll` mtime **2026-09-26 14:09:33**（删除探针后重编），晚于源码末改；
  - `Assembly-CSharp.dll` mtime **2026-09-25 23:58:01**（**运行时程序集字节未变** ⇒ 生产码零改动的硬证据）；
  - 控制台无 `CS` 编译错误（`read_console(types=["error"], filter_text="CS")` 仅余常驻噪声①）；删除探针后控制台只剩 10 条**存量** CS 警告（全部来自既有 Smoke/Editor 文件）。
- **临时探针去向**：`Valley_HH341_D0Probe.cs` 与 `Valley_HH341_D0Probe.cs.meta` **均已删除**（`Get-ChildItem Assets\Editor\Smoke\Valley_HH341*` ⇒ 0 个）；删除后控制台 `filter_text="HH341"` 仅余一条对照局存档日志，**无残留言**。日志**保留**。
- ⛔ 未提交、未 push；⛔ 未改四本账本；⛔ 未改 `最高优先级文档/`；⛔ 未改场景/旧资产键/`AI.Core`。

---

## 七、扫描命令与原始读数（**扫描域逐条声明**）

> 扫描域①＝**生产域** `Valley Rampart/Valley Rampart/Assets/_Game/**`（命令：`rg -n <pat> Valley Rampart/Assets/_Game`）
> 扫描域②＝全 `Assets`（含 `Editor/Smoke`）＝**非生产域对照**，⛔ 不得替代生产域基线。

| 项 | 生产域读数 | 对照/基线 | 判读 |
|---|---|---|---|
| `TaskScheduler\.(Instance\|HasInstance)` | **54 行 / 15 文件** | 任务书基线 54/15 | ✅ 未下降 |
| `new KingdomTask\(`（原始命中） | 15 行 / 10 文件 | — | ⚠️ 需逐行核验 |
| `new KingdomTask\(`（**剔除注释行后**） | **14 行 / 9 文件** | 基线 14/9 | ✅ 一致（唯一多出者＝`ScheduleCenterStub.cs:99` 的**注释行**；照 `HH.338` 教训，注释字面量会污染对照读数） |
| `ITaskSource` 实现者声明 | **9 类** | 基线 9 类（8 实体源＋调试源） | ✅ `WorldGatherSource`/`ChestEntity`/`Building`/`ConstructionSiteStore`/`DebugTaskSource`(WorkerTask.cs:58)/`MineByproductComponent`/`UnitController`/`BlacksmithBuilding`/`SiegeWorkshopBuilding`（另 1 行命中＝`KingdomTask.cs:44` 构造形参，非实现者） |
| `KingdomTaskType` | 11 项（D2 前不得删） | 未删 | ✅ 本批未动 |
| **新协议零接线**（`TaskCard\|TaskLifecycleState\|TaskLifecycleRules\|TaskBindingManager\|TaskBindingResult\|TaskProtocolRuntime\|TaskProtocolIssuer\|TaskTargetRef\|TaskTargetKey`，排除 6 个协议文件） | **0 行 / 0 文件** | — | ✅ 首片/小源之外零接线；本批探针为 **Editor-only 且已删**，⛔ 不属生产域 |
| AI.Core（`TaskCard\|TaskLifecycleState\|TaskBindingManager\|TaskProtocolRuntime\|TaskProtocolIssuer\|KingdomTask\|TaskScheduler`） | **0 行** | 0 改动 | ✅ |
| 全 `Assets` 对照 `TaskScheduler\.(Instance\|HasInstance)` | — | **149 行 / 27 文件** | 与任务书所列一致（**非生产基线**，仅对照） |

**计数单位声明**：行＝含匹配的**源码行数**（含注释）；文件＝含匹配的文件数；类＝类型声明行数。排除清单＝上表已逐项注明。

---

## 八、任务书 §5.4「§九七条判据」逐条（D0 面）

1. **零引用**：载体侧机械证据＝`TaskCard` 26 字段零 Unity 对象/接口引用（断言 A2b）；管理器侧＝`TaskBindingManager` 表内只存 `long/int/值 struct/string`（`M5-B` 已裁，本批未改）。⚠️ 旧链侧 `KingdomTask.source` 仍持 `ITaskSource` 引用（**旧链现状，非本批**）。
2. **目标可判**：链#1 完成即证据充分（目标处置在**同一 `Tick` 内**随完成动作落定：格 Tree→Plain、池子 12→11）。⚠️ 本批卡片侧的目标处置由探针编排（`M5-C` 编排面尚未接线）。
3. **配对不悬空**：两链终态后 M5-B **预定=0 配对=0**，`VerifyInvariants` 违规 **0**（P1/P2/P3 均过）。
4. **不居中态**：两链卡片均达终态（`Done`/`Aborted`），零卡片滞留非终态。
5. **读档重验**：**未取得**（D0 不含存档/读档；`EnterRestore/RestoreValidated/RejectRestore` 未在活世界跑过）⇒ 如实列报，不判成立亦不判不成立。归 D1/另批。
6. **单管理器**：**未达成且如实列报**——`ScheduleCenterStub._crewAssignments:56` 第二载体仍在（本批只读，未动）。
7. **不碰地图数据**：探针仅经门读 `MapGate.GetFeatureAt`（264＋336 次）＋ `ResourceRespawnSystem.PointsOf` 只读口；**零底层数组引用、零写入**（无 `MapGate.SetFeature*`/无 `map.features[...]=`）。

---

## 九、停手条件核对（任务书 §七 · 12 条）

| # | 条件 | 是否命中 | 说明 |
|---|---|---|---|
| 1 | D0 不能由 `EnterTestRun` 完成／无法退 Play | ❌ 未命中 | 正门成立（0.9s 起局）；`ExitTestRun=1`、Play 回 false |
| 2 | 未归因异常／悬挂／配对泄漏／目标失联 | ❌ 未命中 | 窗口 error=0；两链均终态；两表零残留；链#2 Timeout 已归因（旧链既有分支，同局 13 条） |
| 3 | 需同改 `Building` 与 `UnitController` | ❌ 未命中 | 本批零生产码改动 |
| 4 | 需扩 `ITaskScheduler` 十方法／主耦合不在单例消费面 | ❌ 未命中 | 观测全部落在单例边界（源集＋3 张表＋日志），未扩接口 |
| 5 | 旧链基线下降无法解释 | ❌ 未命中 | 54/15 未降；`new KingdomTask` 14/9 未降（多出 1 行为注释，已逐行核验） |
| 6 | 新协议政策被回写/改值/新增未裁政策 | ❌ 未命中 | 只调用：`LoadPolicy` 读值＝裁定值；`basePriority` 只读复算（B 档），⛔ 未回写 |
| 7 | 触碰 AI.Core/场景/地图数据/旧资产键/四本账本/最高优先级文档 | ❌ 未命中 | 全部零改动（生产域 git diff 为空；运行时 dll 未变） |
| 8 | 本批新增编译错误／无法归因的本批 Console 错误 | ❌ 未命中 | 编译无 CS 错误；4 条 error 全部经空白对照归因 |
| 9 | 隐瞒第二载体为「单管理器已完成」 | ❌ 未命中 | §八-6 明列**未达成** |
| 10 | 需删 `KingdomTaskType`/`KingdomTask`/旧源/旧资产键 | ❌ 未命中 | 一律未动 |
| 11 | 需同改两个巨兽或超允许面 | ❌ 未命中 | 两巨兽只读（本批未读改、未动） |
| 12 | 判据只能靠 Edit 探针而不能进局复现 | ❌ 未命中 | 本批**全部读数取自 Play 内真实局**；探针已删 |

**⇒ 12 条全未命中，不停手，D0 判为达成。**（D1 未开工：等待策划端绿色裁决。）

---

## 十、观察登记（**非判定**，供策划端分流）

1. **旧链 `Assigned` 无帧级窗口**：`Tick()` 内 `Dispatch` 与 `UpdateAssignedTasks` **同一次调用**完成 ⇒ `TaskState.Assigned` 恒不可被逐帧观测（本批两链的 `MovingToSource` 与派发同帧出现，实测帧号相同：10290/10290、10341/10341）。此结构事实对 D1「新卡配对建立点」的取证方式有影响，建议 D1 任务书预先裁定取证口径。
2. **同工人对同一目标「派发→不可达→放弃」连发**：窗口内 `[TaskScheduler] Abandon Transport → npcId 3 reason=Unreachable` 与紧随其后的 `派发 Transport 任务 → npcId 3 @ (32.64, 31.68)` **成对连发 8 次**（帧 10291→10306，约每 tick 一次）。窗口内 `Abandon` 分布：`reason=Timeout` **13 条**、`reason=Unreachable` **8 条**。⛔ 本批不作判定（属既有链行为，非本批引入），仅登记原始行供分流。
3. **15x 下 `taskTimeout=30s` 段预算的触达率**：13 条 Timeout 中 1 条落本批链#2（派发距源 9.2 格）。⛔ 不作平衡判定。

---

## 【应登记项】

> 以下为执行端应交策划端回填的文本块（⛔ 执行端未改任何账本）。

```
HH.341 | M5-D0 进局验证（只读） | 执行端 | D0 达成：1 条完整链（完成）＋1 条异常回收链；断言 25/25、失败 0 | 生产域零改动 · 临时探针已删除 · 未取 D 号 | 待策划端绿色裁决后另立 D1
HH.341 | 载体无新增 | 执行端 | _Game 零改动；新协议零接线维持 0/0 | 允许面内「验证适配文件」未使用（只读反射＋日志订阅已足） | 待登记
HH.341 | 基线口径勘正 | 执行端 | `new KingdomTask(` 生产域**原始命中 15 行/10 文件**，逐行核验后剔除 1 行注释（`ScheduleCenterStub.cs:99`）⇒ **真实构造点 14 行/9 文件**（＝基线） | 后续扫描须声明「是否含注释」 | 待登记
HH.341 | 观察登记① | 执行端 | 旧链 `Assigned` 与 `MovingToSource` 同 Tick 内完成 ⇒ 无帧级可观测窗口 | 影响 D1 配对建立点取证口径 | 待裁
HH.341 | 观察登记② | 执行端 | 窗口内 `Abandon … reason=Unreachable` 8 条＝同工人(npcId 3)对同一目标连发「派发→放弃」（帧 10291~10306）；同窗 `Timeout` 13 条 | 既有链行为，非本批；建议分流 | 待裁
```

---

## 附：日志与证据文件清单

```
Valley Rampart/Logs/hh341_d0/
├─ hh341_d0_probe.txt                     ← 本轮（判据修正版）探针全量日志
├─ hh341_d0_probe_20260926_140725.txt     ← 同上·时间戳副本
├─ console_control.txt                    ← Console 前后对照 ＋ 三组空白对照 ＋ 归因表
├─ evidence_scan_git.txt                  ← 生产域/全 Assets 扫描读数 ＋ 逐行核验 ＋ git 证据
└─ run1_preassertfix/                     ← 首跑归档（判据缺陷版·仅供差异复算）
   ├─ hh341_d0_probe_run1.txt
   └─ hh341_d0_probe_20260926_140404.txt
```
