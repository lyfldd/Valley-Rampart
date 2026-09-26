# `HH.338` `M5-A` 任务卡片与生命周期数据结构 交付报告

- **编号**：**`HH.338`**。取号口径＝⚠️ 账本 **水位线 `HH.335`** ＋ **在途实存 `HH.336`／`HH.337`**（两批已落盘但**未登记**入账本）⇒ 顺延取 **338**；落盘前全仓 `HH.338` 预扫 **0 命中**（见 §八-[3]）。⚠️ **顺序偏差如实登记**：本批为「**先落盘后补登记**」（正确顺序是登记即占号）—— 因任务书要求号从账本水位线取，实读与落盘同批完成；若他端已占 338，按 `vr-id-ledger` **先落盘者保留**处置（本报告持有时间戳证据）。
- **依据**：本批任务书（`M5-A`：任务卡片与生命周期数据结构）
- **性质**：**施工批**（新增**纯数据 ＋ 状态机**载体）· ⛔ **未接入旧生产链** · ⛔ 未 commit／未 push。⚠️ **账本登记已补写**（`多Agent交接/_编号登记.md`：水位线行 `HH.335→HH.338` ＋ 本行 ＋ 缺行注记 ＝ `+3/−1`）—— 先落盘后补登记，见 §八-[9]
- ⭐ **收口（2026-09-25，承策划端裁决）**：commit **`ea6ad129c60a0bb5d44910c812f781f1746e9eb5`**（`4 files changed, 703 insertions(+)` ＝ 仅本报告 §一 的 4 个物理路径；⛔ 未带账本／报告／其他脏点；⛔ 未 push）。⚠️ 列报：`git diff --check` 在两个 Unity 生成的 `.meta` 上报 12 行 **既有格式**行尾空白（全仓 `.meta` 抽样 400/400 同型）⇒ 未擅自"修"，见 `HH.339` 报告 §十-12。证据 `Logs/hh338_commit.log`／`hh338_check_ws.log`／`hh338_meta_ws.log`。
- **基线**：`76452c01`（`HH.336` 收口提交）—— 开工时 `git log -1` ＝ `76452c01`（§八-[7]）
- **设计依据**：`最高优先级文档/06_任务层.md` §三 `:59-70`（五组栏）／§四 `:73-112`（八阶段）／§五 `:113-124`（异常对齐）／§六 `:126-134`（配对同生共死）／§九 `:156-165`（7 判据）；`05_交互层.md` §四 `:77-103`（交互表 ＋ 现码 7 行）／§五 `:107-125`（进度硬编码）／§六 `:129-155`（配对零引用）
- **日期**：2026-09-25
- **硬停核对**：三条硬停**全未命中**（§十）；**四个待裁政策项一律未填值**（§九）

---

## 一、改动面（恰 2 个逻辑项 · 4 个物理路径 · 零改旧文件）

| # | 路径 | 性质 | 行数/字节 |
|---|---|---|---|
| 1 | `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskCardProtocol.cs` | **新增** | 498 行 · 26594 B |
| 2 | `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskCardProtocol.cs.meta` | **新增**（Unity 导入生成 · 16:16:47） | 267 B |
| 3 | `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskLifecycleRules.cs` | **新增** | 185 行 · 10203 B |
| 4 | `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskLifecycleRules.cs.meta` | **新增**（Unity 导入生成 · 16:16:47） | 267 B |

⭐ **零改旧文件（机械证据）**：本路径下**已跟踪件改动数 ＝ 0**（`git status --porcelain -- <该目录>` 仅 4 行 `??`，见 §八-[2]/[4]）；⛔ 未改 `TaskScheduler.cs`／`ITaskScheduler.cs`／`KingdomTask.cs`／`TaskState.cs`／`TaskPriorityConfig.cs`；⛔ 未删任何枚举项、广告源、调用点；⛔ 未建 `BuildingAbilityCatalog`；⛔ 零资产改动（`Resources` diff 空）。

**行尾/编码**：两文件均 **bare-LF（CRLF=0）· 无 BOM**（§五-[1]）；与同目录既有文件一致。

---

## 二、新增类型与字段 `file:line` 清单

> 文件前缀：`A` ＝ `.../TaskScheduling/TaskCardProtocol.cs`；`B` ＝ `.../TaskScheduling/TaskLifecycleRules.cs`。行号为**落盘现状**（见 `Logs/hh338_anchor_map.log`）。

### 2.1 卡片五组（`06 §三`）＋ 枚举/值类型（全部为 `struct` / `enum` / 基元）

| 组 | 内容 | 类型定义 | 卡片字段 |
|---|---|---|---|
| ① 身份 | `taskId` ｜ `issuer` ｜ `priority` | `TaskIssuerKind` **A:20** ／ `TaskIssuerRef` **A:29** | `taskId` **A:229** ／ `issuer` **A:230** ／ `priority` **A:231** |
| ② 归属 | 王国/玩家 | （值口径：`kingdomId==0` ＝ 玩家 · `-1` ＝ 无国） | `kingdomId` **A:234** |
| ③ 做什么 | `ability` ｜ `targetRef` ｜ `count` | `TaskTargetKind` **A:43** ／ `TaskTargetRef` **A:58** ／ `TaskCountMode` **A:67** ／ `TaskCountSpec` **A:75** | `ability` **A:237** ／ `targetRef` **A:238** ／ `count` **A:239** |
| ④ 条件与进度 | `precondition` ｜ `progressSpec` ｜ `duration` | `TaskPreconditionMode` **A:84** ／ `TaskPrecondition` **A:97** ／ `TaskProgressMode` **A:108** ／ `TaskProgressSpec` **A:116** | `preconditions` **A:242** ／ `progressSpec` **A:243** ／ `duration` **A:244** |
| ⑤ 结果 | `rewardTo` ｜ `targetEffect` | `TaskRewardSink` **A:126** ／ `TaskRewardSpec` **A:136** ／ `TaskEffectKind` **A:144** ／ `TaskTargetEffect` **A:155** | `rewardTo` **A:247** ／ `targetEffect` **A:248** |
| ⑥ 执行中 | `state` ｜ `workerId` ｜ `workProgress` ｜ `deadline` | `TaskLifecycleState` **B:14** | `state` **A:251** ／ `workerId` **A:252** ／ `workProgress` **A:253** ／ `deadline` **A:254** |
| ⑦ 意外 | `retry` ｜ `onFail` | `TaskFailPolicy` **A:165** | `retryCount` **A:257** ／ `retryMax` **A:258** ／ `onFail` **A:259** |
| 附：原因码 | — | `TaskAbortReason` **A:179** ／ `TaskUnassignReason` **A:200** ／ `TaskSuspendReason` **A:210** | `abortReason` **A:263** ／ `lastUnassignReason` **A:264** ／ `suspendReason` **A:262** |
| 附：标记/上下文 | — | — | `unreachableStreak` **A:265** ／ `stateBeforeRestore` **A:266** ／ `IsSuspended` **A:269** ／ `IsTerminal` **A:272** |

- 卡片本体：`public sealed class TaskCard` **A:226**（哨兵：`retryMax` 默认 **-1** ＝ 未配置 · A:258；`deadline` 默认 **∞** ＝ 未设 · A:254；`priority` 默认 **0** ＝ 暂定占位 · A:231）
- 重验接口：`public interface ITaskRestoreValidator` **A:493**（⚠️ **无实现** · ⛔ 不接入存档系统）
- 规则面：`TaskLifecycleState` **B:14**（八阶段）／`TaskTransitionError` **B:27**（13 值）／`TaskLifecycleRules` **B:45**（`AllStates` **B:48** · `IsTerminal` **B:61** · `IsRestorable` **B:67** · `IsDefined` **B:74** · `IsLegalEdge` **B:88** · `Validate` **B:114** · `IsLegalAbort` **B:130** · `IsLegalUnassign` **B:168**）

### 2.2 字段形状说明（3 处与文档字面不同，逐条给依据）

| 项 | 文档字面 | 本片形状 | 依据 |
|---|---|---|---|
| `precondition` | 单数栏（`06 §三 :66`） | **`TaskPrecondition[]`（0..N 条 · 全成立才可做）** | `05 §三 :66` 有 **∧ 复合**（「等级 < 上限 ∧ 资源够」）⇒ 单条表达不了 |
| `progressSpec` | 「看哪个字段 → 到什么值」 | **三态**：`FieldReaches`／`TimerElapsed`／`ArrivalOnce` | 值集**照抄 `05 §四 :95-103` 现码 7 行**的完成规则形态（计时／到达并做完一次／字段到某值）；⛔ 未自造通用进度条（`05 §五 :111`） |
| `targetRef` 的「类型」段 | 「坐标 ＋ 类型」（`06 §三 :65`） | `kind`（5 值）＋ `typeKey`（int） | ⛔ 键编码口径**不属本片**（归 `M5-C` 切换期与能力名/交互表同批钉死）⇒ 本片只留**值槽位**，不裁编码 |

---

## 三、⭐ 八阶段状态转移表（`06 §四 [0]~[7]` ↔ 唯一入口 ↔ 拒绝码）

> **合法边 23 条**（机械枚举 · §四）+ **同阶段正交操作 3 个**（挂起/恢复/retry 消费）。`Done`／`Aborted` **出边 ＝ 0**。

| # | 边（阶段对） | 唯一入口 | 文档箭头 | 载荷/前置 | 拒绝码（该入口可返回） |
|---|---|---|---|---|---|
| 1 | `[0] Created → [1] Pending` | `Accept()` **A:278** | `06 §四 :78` | — | `TerminalSource`／`IllegalEdge` |
| 2 | `[0] Created → [6] Aborted` | `Reject(reason)` **A:284** | `:77` 卡片不合法丢弃 | `reason != None` | `ReasonRequired` |
| 3 | `[1] Pending → [2] Assigned` | `Assign(workerId, deadline)` **A:294** | `:86` 匹配到工人 | `workerId != 0` | `WorkerUnbound` |
| 4 | `[1] Pending → [6] Aborted` | `Abort(reason)` **A:342** | `:82-84` 五类作废 | `IsLegalAbort(Pending, r)` | `IllegalReason`／`ReasonRequired` |
| 5 | `[2] Assigned → [3] Executing` | `Arrive()` **A:309** | `:90` 工人到达 | `workerId != 0` | `WorkerUnbound` |
| 6 | `[2] Assigned → [1] Pending` | `Unassign(WorkerDied‖Unreachable)` **A:353** | `:87-89` 工人死／到不了 | `IsLegalUnassign` | `IllegalReason` |
| 7 | `[2] Assigned → [6] Aborted` | `Abort(reason)` | `:88` 段后作废 | 白名单 | `IllegalReason` |
| 8 | `[3] Executing → [4] Resolving` | `BeginResolve()` **A:318** | `:96` 完成一次交互 | — | `IllegalEdge` |
| 9 | `[3] Executing → [1] Pending` | `Unassign(WorkerDied‖Unreachable‖Timeout)` | `:94-95` 挂起/解绑·超时回 [1] | 白名单 | `IllegalReason` |
| 10 | `[3] Executing → [6] Aborted` | `Abort(reason)` | `:92-93` 目标没了/变了 | 白名单 | `IllegalReason` |
| 11 | `[4] Resolving → [3] Executing` | `ContinueExecuting()` **A:324** | `:98` 未完成 ∧ 还要继续 | — | `IllegalEdge` |
| 12 | `[4] Resolving → [1] Pending` | `Unassign(Replan)` | `:98` 或回 [1] 重新派 | 白名单 | `IllegalReason` |
| 13 | `[4] Resolving → [5] Done` | `Complete()` **A:331** | `:101` 完成 | — | `IllegalEdge` |
| 14 | `[4] Resolving → [6] Aborted` | `Abort(ConditionUnmet‖…)` | `:99` 条件已不满足 | 白名单 | `IllegalReason` |
| 15 | `{进行中四阶段} → [7] Restore` | `EnterRestore()` **A:395** | `:103` 读档一律重验 | `IsRestorable(state)` | `NotRestorable` |
| 16 | `[7] Restore → {存档时阶段}` | `RestoreValidated()` **A:408** | `:104` 重验成立恢复 | `stateBeforeRestore` 可恢复 | `NoRestoreContext`／`NotRestorable` |
| 17 | `[7] Restore → [6] Aborted` | `RejectRestore(reason)` **A:422** | `:105` 不成立即作废（不恢复） | 在 Restore 态 | `NoRestoreContext`／`ReasonRequired` |

**同阶段正交操作（⛔ 不是阶段对 · 阶段不变）**：

| 操作 | 入口 | 文档 | 语义 |
|---|---|---|---|
| `⏸` 挂起（保留绑定） | `Suspend(ThreatPreempted)` **A:371** | `:88`／`:94` | 仅 `Assigned`／`Executing` 可挂；重复挂起 ⇒ `AlreadySuspended`；阶段与 `workerId` **不变** |
| `⏸` 恢复 | `Resume()` **A:384** | `06 §五 :119` | 未挂起时 ⇒ `NotSuspended` |
| retry 消费 | `TryConsumeRetry()` **A:437** | `06 §五 :123` | ⚠️ `retryMax < 0` ⇒ **`RetryPolicyUnconfigured`（拒绝消费）**；用尽 ⇒ `RetryExhausted` |

**唯一改状态点**：`Move(to, abortReason)` **A:474**（私有）—— 先 `Validate` 再写；非法 ⇒ **状态零变化**。终态守卫 `GuardLive()` **A:456** 与阶段预检 `Pre(to)` **A:466** 保证「阶段合法性先于载荷检查」（拒绝码语义可预期）。

---

## 四、验证读数（⭐ 可复现 · 探针 `Logs/_hh338_lifecycle_probe.cs` ＋ payload `Logs/_hh338_lifecycle_payload.json`）

> 封存产物：`Logs/hh338_lifecycle.log`（**长度 7877 · 封存时刻 2026-09-25 16:23:01**）· `Unity=2022.3.62t7 · isPlaying=False`（Edit 模式 · 纯数据无单例依赖）。

### 4.1 合法/非法**全矩阵**（8 阶段 × 8 阶段 = 64 格 · 每格用「新卡 → 合法入口驱到 from → 跑全部 25 个入口」实测）

| from | 静态合法边 | 实测可达（新状态） | 未覆盖 | 越界 |
|---|---|---|---|---|
| `Created` | 2 `[Pending,Aborted]` | 2 `[Pending,Aborted]` | 0 | 0 |
| `Pending` | 3 `[Assigned,Aborted,Restore]` | 3 `[Aborted,Assigned,Restore]` | 0 | 0 |
| `Assigned` | 4 `[Pending,Executing,Aborted,Restore]` | 4 `[Pending,Aborted,Executing,Restore]` | 0 | 0 |
| `Executing` | 4 `[Pending,Resolving,Aborted,Restore]` | 4 `[Pending,Aborted,Resolving,Restore]` | 0 | 0 |
| `Resolving` | 5 `[Pending,Executing,Done,Aborted,Restore]` | 5 `[Pending,Aborted,Executing,Done,Restore]` | 0 | 0 |
| `Done` | **0** | **0** | 0 | 0 |
| `Aborted` | **0** | **0** | 0 | 0 |
| `Restore` | 5 `[Pending,Assigned,Executing,Resolving,Aborted]` | 5 `[Pending,Aborted,Assigned,Resolving,Executing]` | 0 | 0 |

**合计：合法边 23 · 非法边 41 · 未覆盖 0 · 越界 0 · 非法转移后状态变动 0 次**
⇒ 判据 1（合法转移均有明确入口）＋ 判据 2（非法转移被拒且零变化）**机械成立**。

### 4.2 终态边界（判据 3）

| 终态 | 25 入口全跑 | 接受 | 状态变动 | 错误码集 |
|---|---|---|---|---|
| `Done` | 拒绝 **25/25** | 0 | 0 | `[TerminalSource]` |
| `Aborted` | 拒绝 **25/25** | 0 | 0 | `[TerminalSource]` |

### 4.3 `Restore` 边界（判据 4：只定义态与接口 · ⛔ 不接存档）

- 入边：`Created` ⇒ `NotRestorable`；`{Pending,Assigned,Executing,Resolving}` ⇒ `None`（`stateBeforeRestore` 逐条记录正确）；`Done`／`Aborted` ⇒ `TerminalSource`；`Restore` ⇒ `NotRestorable`。
- 出边：`RestoreValidated()` ⇒ **回存档时阶段**（四条逐条对拍 `err=None`）；`RejectRestore(RestoreRejected)` ⇒ `Aborted`（`abortReason=RestoreRejected`）。
- 对照拒绝：非 Restore 态调 `RestoreValidated`／`RejectRestore` ⇒ `NoRestoreContext`；Restore 态调 `Complete()` ⇒ `IllegalEdge`；Restore 态调 `Unassign(Replan)` ⇒ `IllegalReason`。

### 4.4 原因码白名单（`Unassign` 12 格 ＋ `Abort` 60 格 · 静态表 ↔ 实际入口**表差 0**）

- `Unassign` × `Assigned`：`WorkerDied`✔ `Unreachable`✔ ／ `Timeout`✘`Replan`✘（`IllegalReason`）
- `Unassign` × `Executing`：`WorkerDied`✔ `Unreachable`✔ `Timeout`✔ ／ `Replan`✘
- `Unassign` × `Resolving`：`Replan`✔ ／ 其余 ✘
- `Unassign` × `Pending`：四者皆 ✘（`IllegalEdge`）
- `Abort` × `Pending` 允许 8 ／ × `Assigned` 允许 6 ／ × `Executing` 允许 6 ／ × `Resolving` 允许 8 ／ × `Created` 允许 **0**（`CardInvalid` 只走 `Reject`／`RestoreRejected` 只走 `RejectRestore`）—— **五档表差全 0**

### 4.5 retry 口（待裁政策的「拒绝填值」证据）

| 读数 | 结果 |
|---|---|
| `retryMax` 默认值 | **-1**（未配置 · ⛔ 非「0 次」亦非「不限」） |
| `TryConsumeRetry()` @ `retryMax=-1` | `RetryPolicyUnconfigured` · `retryCount` **0（零变化）** |
| `retryMax=2` ⇒ #1/#2/#3 | `None`／`None`／`RetryExhausted` · `retryCount=2` |

### 4.6 不可达标记（`06 §五 :123`）

`Unassign(Unreachable)` ⇒ `Pending` ＋ `unreachableStreak=1`；第二次 ⇒ `2` 且 `workerId=0`；`ClearUnreachableMark()` ⇒ `0`（⚠️ 清标时机与冷却＝待裁 `06 §十-3`，本片**只给口 ⛔ 不自动调用**）。

---

## 五、`targetRef` 无对象引用的机械扫描（判据：值数据）

### 5.1 源码面（`Logs/_hh338_src_scan.ps1` 输出 · 定稿复跑 · `Logs/hh338_src_scan.log`）

- **[A] 行尾/编码（定稿读数）**：`TaskCardProtocol.cs` **498 行 · 26594 B** · CRLF=**0** · bareLF=497 ✓ · BOM=**False**；`TaskLifecycleRules.cs` **185 行 · 10203 B** · CRLF=**0** · bareLF=184 ✓ · BOM=**False**。
- **[B] 禁词扫描（新文件内）**：`ITaskSource`／`KingdomTask`／`WorkerTask`／`NPCBrain`／`TaskScheduler`／`UnityEngine`／`MonoBehaviour`／`BuildingAbilityCatalog` **全 0 命中**（⛔ 新文件正文与注释内均不写旧类型名 ⇒ 保「未变化」对照读数不被污染；含 `UnityEngine` 字面量亦已清零）。
- **[C] 扫描器有效性对拍（旧文件应命中）**：`TaskScheduler.cs` ＝ `ITaskSource×16 · KingdomTask×59 · WorkerTask×5 · NPCBrain×30 · TaskScheduler×16 · UnityEngine×1 · MonoBehaviour×1`；`ITaskScheduler.cs` ＝ `ITaskSource×6 · TaskScheduler×2 · UnityEngine×1` ⇒ **扫描器有效**（B 段的 0 非假阴性）。
- **[D] `targetRef` 结构体逐字**：`TaskTargetKind kind; int cellX; int cellY; int typeKey;`（**A:58-64**）。

### 5.2 反射面（探针 §八 · Edit 运行时）

| 检查 | 读数 |
|---|---|
| `TaskTargetRef` 字段 | 4 个（`kind`/`cellX`/`cellY`/`typeKey`）· **全为基元/枚举** · 违规 **0** |
| `TaskCard` 全部公开实例字段 | **24 个** · 违规 **0**（逐字段见日志 §8.2；判据：非基元/枚举/`string` 者须为**值结构体**；数组按**元素类型**递归判；另查 `UnityEngine.Object` 可赋值性与 9 条禁名单） |
| 协议内 7 个 struct 成员清单 | `TaskIssuerRef`／`TaskTargetRef`／`TaskCountSpec`／`TaskPrecondition`／`TaskProgressSpec`／`TaskRewardSpec`／`TaskTargetEffect` —— 成员**全为 int/float/enum**（日志 §8.3） |
| ⛔ `ITaskSource`／`NPCBrain`／`UnityEngine.Object`／`KingdomTask` 引用 | **0**（反射禁名单 ＋ 源码禁词双证） |

---

## 六、旧面未变化 · 对照读数（**本次现读 · 行数口径＝命中行数**）

> 脚本 `Logs/_hh338_callface.ps1` → 产物 `Logs/hh338_callface.log`。基线 ＝ `HH.337` 报告（同一 `76452c01` 树）。

| # | 口径（域 = `_Game`） | 本片现读 | `HH.337` 基线 | 判定 |
|---|---|---|---|---|
| 1 | `TaskScheduler.Instance\|HasInstance` | **54 行 / 15 文件** | 54 行／15 文件 | ✅ 逐数相同 |
| 2 | `new KingdomTask(`（仅代码 · 剔整行注释） | **14 行 / 9 文件** | 14 行／9 文件 | ✅ 相同（含注释口径＝15 行/10 文件，多出者为 `ScheduleCenterStub.cs:99` 注释行） |
| 3 | `TaskScheduler.`（含注释） | **96 行 / 27 文件** | 96 行／27 文件 | ✅ 相同 |
| 4 | `ITaskScheduler`（含注释） | **3 行 / 2 文件**（定义／实现声明／注释小标题） | 3 行／2 文件 · 类型调用点 0 | ✅ 相同 |
| 5 | `ITaskSource` 实现者（Assets 全域） | **9 处 / 9 文件** | 9 类／9 文件 | ✅ 相同 |
| 6 | `KingdomTaskType` 枚举项 | **11 项**（`Repair…AmmoReload` 逐项列出） | 11 项 | ✅ 相同 |
| 7 | `ITaskScheduler` 方法数 | **10** | 10 | ✅ 相同 |
| 8 | `WorkerTask` / `WorkerTaskType` | **26 行/10 文件** · **6 行/2 文件** | （`HH.337`：static 工厂 ＋ 2 值枚举） | ✅ 未动 |
| 9 | **新协议零接线**：`TaskCard`／`TaskLifecycleState`／`TaskLifecycleRules`／`TaskTransitionError`／`TaskTargetRef`／`ITaskRestoreValidator`／`TaskAbortReason`／`TaskUnassignReason`／`TaskSuspendReason` 在 `_Game`（**排除新文件自身**）命中 | **全 0 行** | — | ✅ **未接入旧生产链** |

⇒ ⛔ **不得**据此声称「游戏任务能力已改变」：本片**只增载体**，跑的世界里**零调用点**（`06 §八 :154` 迁移三步之①「并存」的**前置件**，接线归 `M5-C`）。

---

## 七、编译结果与 warning/error 明细

- **编译**：`manage_editor request_compile`（bridge）⇒ **`Compilation completed successfully`** · **errors ＝ 0**。
- **DLL 时间戳**：`Assembly-CSharp.dll` **16:22:42** ／ `Assembly-CSharp-Editor.dll` 16:18:08 —— 晚于末次源码改动（`TaskCardProtocol.cs` 16:22:32 · `TaskLifecycleRules.cs` 16:20:02）⇒ **含本批**。
- **warning ＝ 15（`Assembly-CSharp`）· 全为既有项 · 无一条在本片改动面**（逐条）：

```text
ChestManager.cs(40,18) CS0114 · ToastManager.cs(47,18) CS0114 · GroundEffectManager.cs(95,18) CS0114
IUIPanel.cs(11,10)/(14,10) CS0108 · BuildingMenuPanel.cs(169,24)/(186,13) CS0252
ProjectileManager.cs(280,41) CS0253 · NPCBrain.cs(906,22) CS8632
FormationPanel.cs(128,13)/(139,43) CS0252 · CameraSetup.cs(30,18)/(32,19) CS0414
PathfindingScheduler.cs(42,17) CS0414 · VisionSystem.cs(15,25) CS0414
```

- **Editor 程序集 warning 10**（`Smoke/*` ＋ `ArtImportPipeline.cs:267`）—— 与 `HH.335`／`HH.336` 记录同族；控制台末条为非警告信息（`287 node options failed to load and were skipped.`）。
- ⚠️ 编译期间控制台 error 面 **0**（`read_console` 复核）。

---

## 八、git 证据（产物 `Logs/hh338_git_evidence.log`）

1. **`git status --short`（本片路径）**：4 行 `??`（2 `.cs` ＋ 2 `.cs.meta`）—— `TaskCardProtocol.cs`／`TaskCardProtocol.cs.meta`／`TaskLifecycleRules.cs`／`TaskLifecycleRules.cs.meta`。
2. **`git check-ignore`**：两文件 **exit=1（未被忽略）** ⇒ 会正常入库。
3. **`HH.338` 全仓预扫**：0 命中（取号时）；`HH.336`／`HH.337` 未登记入账本（实读）。
4. **本路径已跟踪件改动数 ＝ 0**（`git status --porcelain -- <该目录>` 中 `M` 计 0）。
5. **`git diff --check`**：**exit=0 · 输出行数 0**（无空白错误）。⚠️ 仅打印既有脏点的 `LF will be replaced by CRLF` 预警（未跟踪件不参与）。
6. **`git diff --stat`（工作区既有脏点·**非本片**）**：`19 files changed, 608 insertions(+), 278 deletions(-)` —— 全部为开工前既有脏点（`GameScene.unity`／`Packages`／`pixel-forge`／美术 png／若干 `.md`），**本片未增删其中任何一行**。
7. **`HEAD` ＝ `76452c01`**（开工与收尾同一提交 · ⛔ 本批**未 commit／未 push**）。
8. ⚠️ **本片对工作区脏点行数的影响**：开工 84 行 ⇒ 收尾 **88 行**，增量恰为本批 4 个新文件（含 `.meta`）。
9. ⚠️ **本片另有 1 个文档面改动（非代码）**：`多Agent交接/_编号登记.md` ⇒ `1 file changed, 3 insertions(+), 1 deletion(-)`（水位线行改写 ＋ `HH.338` 行 ＋ 缺行注记；**LF 保持**，`git status` 另计 1 行 `M`）。⛔ **未 commit**（策划端账本不自提交）。

---

## 九、未完成项 ＋ ⚠️ 四个待裁政策项（**⛔ 一律未填值**）

### 9.1 四个待裁政策（`06 §十-2~-5`）—— 本片**只留字段/口，不落值**

| # | 待裁项 | 本片处置 | 证据 |
|---|---|---|---|
| **-2** | **优先级**由谁定 | 只留 `priority` 字段（默认 **0** ＝ ⚠️**暂定占位**）；⛔ **不算、不读、不排序** | A:231 · §六-[9]（零接线） |
| **-3** | **`retry` 默认次数**与「不可达」冷却 | `retryMax` 默认 **-1 ＝ 未配置**；`TryConsumeRetry()` 在未配置时**拒绝消费**；`unreachableStreak` 只记标记与计数，⛔ 无冷却/无重置时机；`ClearUnreachableMark()` 只给口 | A:258／A:437／A:265／A:448 · §四-4.5/4.6 |
| **-4** | **分域粒度**（大区块 vs 王国） | 只留 `kingdomId`（值口径与现码一致）；⛔ 无分桶结构、⛔ 无 chunk 字段 | A:234 |
| **-5** | 任务**统计/日志**粒度 | ⛔ 本片**零日志、零计数器**（连 `Debug.Log` 都未引入）；`retryCount`／`unreachableStreak` 为**卡片自身**状态非统计口径 | §五-[B] 禁词扫描 · A:257/A:265 |

### 9.2 暂定占位清单（最小中性默认 · **⛔ 非政策裁定**）

| 字段 | 占位值 | 语义 | 何时可定 |
|---|---|---|---|
| `priority` | `0` | 未填（⛔ 不是「最低优先级」） | 待裁 `06 §十-2` 后由 `M5-C` 接排序 |
| `retryMax` | `-1` | **未配置**（⛔ 不是「0 次重试」） | 待裁 `06 §十-3` |
| `onFail` | `Unspecified` | 未指定（值集 3 项＝文档原词「换人／放弃／降级」） | `M5-C` 按交互表填 |
| `deadline` | `+∞` | 未设（`taskExpiry`／`taskTimeout` 预算由管理器给） | `M5-C` |
| `TaskRewardSpec.amount` | `0` | 未填（⛔ 不是「0 个」） | `M5-C` |
| `TaskTargetRef.typeKey` / `fieldKey` | `0` | 键编码未定 | `M5-C`（与能力名/交互表同批） |

### 9.3 未完成项（**如实列出 · 均非本片范围**）

1. **未接入旧生产链**（`06 §八 :154` 迁移三步之①）：本片＝载体，**跑的世界零调用点**（§六-[9] 全 0）⇒ ⛔ 不得声称任务能力已改变。
2. **配对表／预定（`06 §六 :126-134`）未建**：`Done`／`Aborted` 只清**卡内** `workerId`，**配对释放**归 `M5-B`；判据 3（配对不悬空）本片**不判**。
3. **准入校验未接**：`Accept()` 不做「能力名存在吗／目标有效吗」（依赖能力表 ⇒ 归 `M6`）⇒ `Reject(CardInvalid)` 由调用方判。
4. **`ITaskRestoreValidator` 无实现**：⛔ 不接入存档系统（卡片不入档）；重验本体归 `M5-C`／存档面。
5. **键编码未定**：`ability` 名字符串与 `fieldKey`／`typeKey` 的编码 ↔ 现码对象字段的映射表**不存在**（`05 §四` 7 行码面 0）⇒ `M5-C` 必补。
6. **`⏸` 挂起的仲裁语义未接**：`D814`「在册任务不得因重注降权／其它任务源不得抢焦点」是**管理器侧**规则（刺激列表），本片只有卡片侧挂起标记。
7. **`WorkerTask`／`WorkerTaskType`／`ITaskScheduler` 旧面一字未动**（迁移归 `M5-C`／`M5-D`）。
8. **未跑任何进局测试**：本片是纯数据结构 ⇒ 判据「运行面」不在本片（⛔ 不判「活世界行为」）。

---

## 十、停手核对 ＋ 列报

### 10.1 三条硬停（任务书）

| 硬停 | 命中？ | 证据 |
|---|---|---|
| 「必须改旧 `TaskScheduler` 才能完成本片」 | **未命中** | 本路径已跟踪件改动 0（§八-[4]）；新文件禁词扫描 0（§五-[B]） |
| 「必须决定四个待裁政策」 | **未命中** | §九-9.1 四项一律未落值；`retryMax=-1` 未配置时**拒绝消费**（§四-4.5） |
| 「`06`／`05` 与新协议字段仍有边界冲突」 | **未命中**（有 **3 处字段形状差异**，**已按文档原文给出依据**、非冲突） | §二-2.2：`precondition` 取数组（依据 `05 §三 :66` ∧ 条件）／`progressSpec` 三态（依据 `05 §四 :95-103` 七行）／`targetRef` 只留值槽位（键编码归 `M5-C`） |

### 10.2 本片自纠两处（探针抓到 · 已修 · 留痕）

| # | 现象 | 根因 | 修法 | 证据 |
|---|---|---|---|---|
| 1 | 探针首跑报「`Pending/Assigned/Executing/Resolving` → `Restore` **越界**」 | `EnterRestore()` **绕过了唯一改状态点**（直写 `state`），且 `[7]` 入边**未进合法边表** | 把 `{进行中四阶段} → Restore` 纳入 `IsLegalEdge`（`B:88-107`）并改为经 `Move()` 写入（`A:395-405`） | `Logs/hh338_lifecycle.log` §二（越界 0） |
| 2 | 终态错误码集散乱（`IllegalReason`／`WorkerUnbound`…） | 载荷检查（原因码/工人号）**先于**阶段合法性 | 新增 `GuardLive()`＋`Pre(to)`，**次序钉死**：终态 → 阶段对 → 载荷 | 同上 §三（错误码集 ＝ `[TerminalSource]`） |

### 10.3 列报（停在报告 · ⛔ 未自行处置）

1. **账本缺行**：`HH.336`／`HH.337` 已落盘但**未登记**入 `_编号登记.md`（本片取号据此按**实存**顺延）；回填归策划端。
2. **本片登记顺序偏差**：先落盘后补登记（§头注 · 时间戳证据在 §七/§八）。
3. **`06 §十` 四项待裁**：本片已把「拒绝填值」做成**可执行机制**（`retryMax=-1` 拒绝消费／`priority` 不读），供 `M5-C` 落值。
4. **能力表依赖**：`ability` 名字符串 ↔ 现码对象的映射缺口在 `M5-C` 会暴露（`05 §四` 码面 0）。
5. **经验候选（供策划端裁决是否沉淀）**：①「声明唯一改状态点 ⇒ 必须用机械探针验（**实测可达边集 == 静态合法边集**）」——本片首跑即抓到第二处写入点（§十-10.2 自纠 #1）；②「**只加不改**批的新文件内 ⛔ 不写旧类型名字面量」——否则「调用面未变化」的 grep 读数被**新文件注释**污染（本片 `UnityEngine` 字面量曾在注释里，已清零；`Logs/_hh338_src_scan.ps1` B 段对拍）。

---

## 十一、应登记项（⛔ 本批未提交账本；登记行见下 —— 供策划端回填／核对）

```text
| **HH.338** | **`M5-A` 任务卡片与生命周期数据结构 交付报告**（执行端 · 取号 HH.338：账本水位线 HH.335 ＋ 在途实存 HH.336／HH.337（未登记）顺延；⛔ 未 commit／未 push／未改账本其他行）：**施工批 ＝ 只增载体**。
改动面恰 2 逻辑项／4 物理路径（`TaskCardProtocol.cs` 498 行 ＋ `TaskLifecycleRules.cs` 185 行 ＋ 各自 `.meta`，均 Unity 导入生成）；**本路径已跟踪件改动 ＝ 0**（⛔ 未动调度器/旧任务对象/旧枚举/资产）。
① 卡片 `06 §三` 七组栏位全落（`taskId`/`issuer`/`priority`/`kingdomId`/`ability`/`targetRef`/`count`/`preconditions`/`progressSpec`/`duration`/`rewardTo`/`targetEffect`/`state`/`workerId`/`workProgress`/`deadline`/`retryCount`/`retryMax`/`onFail`）；`targetRef` ＝ 纯值（4 字段全基元/枚举 · 反射违规 0）。
② 八阶段状态机：合法边 **23**／非法边 **41**（全矩阵实测**未覆盖 0 · 越界 0 · 拒绝后状态变动 0**）；`Done`/`Aborted` 25/25 入口全拒且码集 ＝ `[TerminalSource]`；`Restore` 入边限「进行中四阶段」、出边＝回原阶段/作废；`Unassign`×3 阶段、`Abort`×5 阶段**白名单表差 0**。
③ **四个待裁政策（`06 §十-2~-5`）一律未落值**：`priority` 默认 0／`retryMax` 默认 **-1（未配置 ⇒ `TryConsumeRetry` 拒绝消费）**／只留 `kingdomId` 无分桶／零统计零日志。
④ 对照读数**逐数相同**：`TaskScheduler.Instance|HasInstance` 54 行/15 文件 · `new KingdomTask(` 14 行/9 文件 · `ITaskSource` 9 类 · `KingdomTaskType` **11 项** · `ITaskScheduler` 10 方法；**新协议在 `_Game`（排除新文件）命中全 0 ⇒ 未接入旧生产链**。
⑤ 编译 **errors=0**（DLL 16:22:42 晚于源码）；warning 15 全既有无一条在改动面；`git diff --check` exit 0。
⑥ ⚠️ 自纠 2 处（`EnterRestore` 绕过唯一改状态点 → 已纳入合法边表并经 `Move`；终态错误码次序 → `GuardLive()`/`Pre()` 钉死）—— 由探针首跑抓到，修后复跑全绿。
⑦ 未完成如实列 8 项（未接线／配对归 M5-B／准入校验依赖 M6／Restore 无实现／键编码未定／挂起仲裁归管理器／旧面未动／未跑进局）。
读数产物：`Logs/hh338_lifecycle.log`（16:23:01）／`hh338_callface.log`／`hh338_src_scan`／`hh338_git_evidence.log`／`hh338_anchor_map.log`。 | 执行端 | 🟡 **已落盘·待策划端验收** | 2026-09-25 | 报告 `多Agent交接/执行端/HH.338_M5-A卡片与生命周期_交付报告.md`；设计依据 `06 §三/§四/§五/§九`·`05 §四/§五/§六` |
```

---

## 附：复现命令（4 条）

```powershell
# ① 编译（bridge · Codely unity-bridge TCP）
pwsh -NoProfile -File 'Logs/_bridge_call.ps1' -Payload 'Logs/_hh338_compile_payload.json'
# ② 状态机全矩阵验证（Edit · 只读 · 落盘 Logs/hh338_lifecycle.log）
pwsh -NoProfile -File 'Logs/_bridge_call.ps1' -Payload 'Logs/_hh338_lifecycle_payload.json'
# ③ 调用面对照读数 ＋ 新协议零接线
pwsh -NoProfile -File 'Logs/_hh338_callface.ps1'
# ④ 源码禁词/行尾扫描 ＋ 锚点清单
pwsh -NoProfile -File 'Logs/_hh338_src_scan.ps1'; pwsh -NoProfile -File 'Logs/_hh338_anchor_map.ps1'
```

## 十二、主策划端补落终裁（HH.338，2026-09-26）

### 终裁：判绿

- [实读] Logs/hh338_lifecycle.log:13-23 给出八阶段矩阵：合法边 23、非法边 41、未覆盖 0、越界 0、非法转移后状态变化 0；:26-29 给出 Done/Aborted 各 25/25 拒绝、错误码 [TerminalSource]。
- [实读] Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskLifecycleRules.cs:130-180 的 IsLegalAbort/IsLegalUnassign 与 Logs/hh338_lifecycle.log:42-50 白名单表差 0；TaskCardProtocol.cs:59-65 的 TaskTargetRef 只有枚举与 Int32 四字段，日志反射违规 0。
- [实读] 三处字面差异有现行文档依据：最高优先级文档/06_任务层.md:65-66 允许条件与进度字段语义；最高优先级文档/05_交互层.md:97-103 明列三种完成规则。故 preconditions 数组、三态 progressSpec、targetRef 纯值槽位属于实现细化，不是契约冲突。
- [实读] A-5 接缝已闭合：TaskCardProtocol.cs:313 仍保留 retryMax=-1 哨兵，:500-508 在未配置时返回 RetryPolicyUnconfigured；M5-C 入口 TaskProtocolIssuer.cs:29-43,47-60 从配置写入，配置 Valley Rampart/Assets/Resources/Config/TaskProtocolPolicyConfig.asset:14-18 为 defaultRetryMax: 1。因此“未配置即拒绝”与“造卡后写入 1”不冲突。
- [实读] Logs/hh338_lifecycle.log:64-83 反射读数只用于 TaskTargetRef 的纯值判据；本裁不把 TaskCard 中的字符串/数组数据字段偷换成“全字段无托管引用”的强断言。

准入结论：HH.338 的 M5-A 载体契约成立，允许 HH.339（M5-B）验收结果作为独立批次继续；不授权 06:154 ②切换或③清理，也不声称任务系统已在活世界验证。

[未知] M5-D 枚举取消与旧链清理仍未由本批裁定。

