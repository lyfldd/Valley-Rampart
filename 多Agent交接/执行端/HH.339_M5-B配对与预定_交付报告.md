# `HH.339` `M5-B` 配对与预定 交付报告（含 `HH.338` 收口）

- **编号**：**`HH.339`**。⭐ 取号口径＝**实时登记表水位线**：`多Agent交接/_编号登记.md` 实读 **HH 最大 ＝ `HH.338`**（distinct 230 个 / D 最大 `D868`）⇒ 顺延取 **339**；`HH.339` 全仓 `.md` **0 命中**（⛔ **未预留、未自行改四本策划账本** —— 登记行见 §十一，交策划端回填）。
- **前置（Part 一 · `HH.338` 收口）**：commit **`ea6ad129`**（`4 files changed, 703 insertions(+)` · ⛔ 未 push）。
- **依据**：本批任务书（一、`HH.338` 收口 ／ 二、`M5-B` 配对与预定 五条范围 ＋ 交付七项）。
- **性质**：**施工批**（新增**管理器侧配对／预定载体与释放规则**）· ⛔ **未接入旧生产链** · ⛔ 未改四账本。⭐ **已收口**：commit **`9432e4fc7a730b72766385ab17e0337e6f479edd`**（`4 files changed, 580 insertions(+)` ＝ 恰 4 路径 · ⛔ 未 push）—— 见 §十二；⛔ **未开 `M5-C`**（前置＝四项待决政策由策划端先裁 · §十二-12.3）。
- **基线**：`ea6ad129`（`HH.338` 收口提交）。
- **设计依据**：`06_任务层.md` §六 `:126-134`（配对与预定 · 同生共死）／§四 `:73-106`（八阶段）／§九 `:156-165`（判据）；`05_交互层.md` §六 `:139-153`（配对只在管理器 · 双方零引用）／§八 `:177-195`（「可多人」是对象侧字段）。
- **验收口径（按任务书）**：**「管理器侧配对／预定契约成立」** —— ⛔ **不是**「游戏任务系统已经可用」。
- **日期**：2026-09-25。

---

## 一、Part 一：`HH.338` 收口提交（恰 4 物理路径）

**commit ＝ `ea6ad129c60a0bb5d44910c812f781f1746e9eb5`**（`HH.338 M5-A：任务卡片与生命周期协议载体（只增不改·未接线）`）

| 要求项 | 读数 |
|---|---|
| `git diff --cached --name-status`（提交前） | **4 行 `A`**：`TaskCardProtocol.cs`／`TaskCardProtocol.cs.meta`／`TaskLifecycleRules.cs`／`TaskLifecycleRules.cs.meta`（⛔ 无其他路径） |
| `git diff --cached --stat`（提交前） | `4 files changed, 703 insertions(+)`（`.cs` 497＋184 行 · `.meta` 11＋11 行） |
| `git show --stat --oneline HEAD`（**提交前**） | `76452c01 HH.336 M4-B 首片：删两个死字段（D868）` · 7 files ＋20／−301 ⇒ 基线确认 |
| `git show --stat --oneline HEAD`（**提交后**） | `ea6ad129 HH.338 M5-A：任务卡片与生命周期协议载体（只增不改·未接线）` · **4 files changed, 703 insertions(+)** · 4 条 `create mode` |
| `git status --short`（提交前） | 暂存面恰 4 行 `A`；其余全部为**开工前既有脏点**（`.gitignore`／`GameScene.unity`／`Packages`／`pixel-forge`／美术 png／若干 `.md`） |
| `git status --short`（提交后） | 暂存面 **0 行**；`M5-A` 四文件已不在列表中（已入库） |
| `git diff --check`（**工作区**） | **exit=0 · 输出 0 行** ✅ |
| `git diff --check --cached`（暂存面） | exit=2 · **12 行**：**全部在两个 Unity 生成的 `.meta`**（`userData:`／`assetBundleName:`／`assetBundleVariant:` 行尾空格，各 3 行）。⚠️ **归属证明**：全仓 `.meta` 抽样 **400/400 含同型行尾空白**（既有 `.meta` 逐份 3/11 行）⇒ **Unity 生成格式的既有现象，非本批代码引入**；⛔ 未"修"（改则偏离 Unity 写入器且与全仓 99%+ 的 `.meta` 不一致）⇒ 列报见 §十-12 |
| pre-commit 钩子 | `[WARN] 疑似未提交交付件`：`HH.336`／`HH.337`／`HH.338` 三份交付报告未入提交（**不阻断**）；本批按指令只提交 4 路径 ⇒ **有意未带报告** |
| 红线核对 | ⛔ 未提交 `_编号登记.md`／`_任务队列.md`／`_当前快照.md`／`_测试基线台账.md`；⛔ 未提交旧调度器／旧任务对象／旧枚举／资产／Prefab／场景；⛔ 未 push |

> 证据产物：`Logs/hh338_commit.log`（含 [0]~[11] 全段）／`Logs/hh338_check_ws.log`（.meta 空白归属）／`Logs/hh338_meta_ws.log`（400/400 抽样）。

---

## 二、Part 二：`M5-B` 改动面（恰 2 逻辑项 · 4 物理路径 · ⛔ 零改既有文件）

| # | 路径 | 性质 | 行数/字节 |
|---|---|---|---|
| 1 | `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskBindingTypes.cs` | **新增** | **129 行** · 5692 B |
| 2 | `…/TaskBindingTypes.cs.meta` | 新增（Unity 导入生成 · 17:08:34） | 267 B |
| 3 | `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskBindingManager.cs` | **新增** | **431 行** · 22467 B |
| 4 | `…/TaskBindingManager.cs.meta` | 新增（同上） | 267 B |

⭐ **零改既有文件（可机械核）**：
- 该路径下**已跟踪件改动数 ＝ 0**（`git status --porcelain -- <该目录>` 仅 4 行 `??`）。
- ⭐⭐ **`M5-A` 两文件未被本批触碰**：内容 blob 与 `HEAD` **逐位相同** —— `TaskCardProtocol.cs` `e4613adfab6e`＝`e4613adfab6e`；`TaskLifecycleRules.cs` `ae7aee83643a`＝`ae7aee83643a`。
- ⛔ 未动旧调度器／旧任务对象／旧枚举／9 类旧广告源／`M6` 面；⛔ 未清理旧 `.asset` 序列化键（另开资产清理批次）。

---

## 三、新增类型与 `file:line` 清单

> 前缀：`B` ＝ `TaskBindingTypes.cs`；`M` ＝ `TaskBindingManager.cs`（行号为落盘现状 · 见 `Logs/hh339_anchor_map.log`）。

### 3.1 值类型（`B`）

| 类型 | 行 | 字段（逐字段类型） |
|---|---|---|
| `TaskBindingResult`（12 值返回码） | `B:18` | `Ok/AlreadyReserved/AlreadyPaired/ReservationConflict/WorkerBusy/PairConflict/NoReservation/NotReserved/NotPaired/StateMismatch/InvalidTarget/WorkerRequired` |
| `TaskPairStatus` | `B:38` | `None`／`Bound`（⚠️ `⏸` 挂起**不改本表**） |
| `TaskTargetKey`（`readonly struct : IEquatable`） | `B:48` | `kind:byte`／`cellX:int`／`cellY:int`／`typeKey:int` ＋ `Of(TaskTargetRef)` `B:64` |
| `TaskReservation`（**预定行**） | `B:100` | `taskId:long`／`target:TaskTargetRef`／`ability:string`／`deadline:float`／`multiAllowed:bool`／`restorePending:bool` |
| `TaskPairRecord`（**配对行**） | `B:114` | `taskId:long`／`workerId:int`／`target:TaskTargetRef`／`ability:string`／`status:TaskPairStatus` |
| `TaskSweepReport` | `B:124` | `reservationCount:int`／`pairCount:int` |

### 3.2 管理器（`M`）—— 两张表 ＋ 索引 ＋ 释放口

| 面 | 行 | 说明 |
|---|---|---|
| 表 1 预定 `Dictionary<long,TaskReservation>` | `M:45` | 键＝**任务号**（⛔ 非卡片引用） |
| 目标索引 `Dictionary<TaskTargetKey,List<long>>` | `M:46` | 同目标冲突判定／回收 |
| 表 2 配对 `Dictionary<long,TaskPairRecord>` | `M:49` | 键＝**任务号** |
| 工人索引 `Dictionary<int,long>` | `M:50` | 一工人至多一配对（P4） |
| 回收缓冲 `List<long>` | `M:53` | ⛔ 非统计（免每次分配） |
| 读口 | `M:58` `ReservationCount`／`M:61` `PairCount`／`M:64` `TryGetReservation`／`M:70` `TryGetPair`／`M:76` `IsWorkerBusy`／`M:82` `ReservationCountForTarget` |
| 写口 | `M:98` `Reserve(card, allowsMultiple, deadline)`／`M:140` `ReleaseReservation(taskId)`／`M:159` `Pair(card, workerId)`／`M:194` `Unpair(card)`／`M:213` `ReleaseOnTerminal(card)`／`M:231` `EnterRestore(card)`／`M:253` `RestoreValidated(card)`／`M:274` `SweepExpired(now, list)` |
| 不变量自检 | `M:309` `VerifyInvariants(List<string>)` |
| 内部 | `M:364` `SameTarget`／`M:369` `HasOtherHolder`／`M:381` `HasExclusiveHolder`／`M:394` `IndexAdd`／`M:405` `IndexRemove`／`M:418` `RemoveReservation`／`M:424` `RemovePair` |

⭐ **对象侧零引用（结构面）**：管理器**不保存卡片引用**（表键／值全为 `long`／`int`／值结构）；入口以**卡片为参数做交叉校验**（阶段／工人号），⛔ 不入表。

---

## 四、⭐ 配对与预定状态转移表（事件 → 管理器入口 → 两表 → 返回码）

| 卡片侧事件（阶段） | 管理器入口 | **配对表** | **预定表** | 返回码（含拒绝） |
|---|---|---|---|---|
| `Accept()` → `[1] Pending` | `Reserve(card, allowsMulti, deadline)` `M:98` | — | **新增**（`taskId → 行`） | `Ok`／`AlreadyReserved`（同任务同目标）／`ReservationConflict`（独占目标已有他任务／**混占**／同任务换目标）／`InvalidTarget`／`StateMismatch` |
| `Assign(w)` → `[2] Assigned` | `Pair(card, w)` `M:159` | **新增**（＋工人索引） | 保留 | `Ok`／`AlreadyPaired`（同任务同工人）／`PairConflict`（同任务换工人）／`WorkerBusy`（P4）／`NoReservation`（**P1**）／`WorkerRequired`／`StateMismatch` |
| `Arrive()` → `[3] Executing` | （无） | 保留 | 保留 | — |
| `BeginResolve()` → `[4] Resolving` | （无） | 保留 | 保留 | — |
| `ContinueExecuting()` → `[3]` | （无） | 保留 | 保留 | — |
| `Unassign(r)` → `[1] Pending` | `Unpair(card)` `M:194` | **释放** | **保留**（任务在 ⇒ 预留在 · `06 §六 :132`） | `Ok`／`NotPaired`（幂等）／`StateMismatch`（卡未回待派） |
| `Complete()` → `[5] Done` | `ReleaseOnTerminal(card)` `M:213` | **释放** | **释放** | `Ok`／`NotReserved`（幂等）／`StateMismatch`（卡非终态） |
| `Abort(r)` → `[6] Aborted` | `ReleaseOnTerminal(card)` `M:213` | **释放** | **释放** | 同上 |
| `EnterRestore()` → `[7] Restore` | `EnterRestore(card)` `M:231` | **释放**（读档后工人绑定不可信） | **保留 ＋ `restorePending=true`** | `Ok`／`NotReserved`／`StateMismatch` |
| `RestoreValidated()` → 回原阶段 | `RestoreValidated(card)` `M:253` | 保留（**⛔ 不重建**） | 保留（清 `restorePending`） | `Ok`／`NotReserved`／`StateMismatch` |
| `RejectRestore(r)` → `[6] Aborted` | `ReleaseOnTerminal(card)` `M:213` | 释放（`EnterRestore` 已释） | **释放** | `Ok`／`NotReserved` |
| 目标变化等（需主动放目标） | `ReleaseReservation(taskId)` `M:140` | **级联释放**（**P1**） | **释放** | `Ok`／`NotReserved`（幂等） |
| 预定超时（管理器兜底 · `06 §六 :133`） | `SweepExpired(now, list)` `M:274` | **级联释放** | **释放**（`deadline ≤ now` 且 ≠ `+∞`） | `TaskSweepReport{reservationCount, pairCount}`（重复调用 0 变化） |
| `Suspend`／`Resume`（`⏸`） | （无） | **均不动本表**（`06 §四 :88`「挂起（**保留绑定**）」） | 保留 | — |

---

## 五、⭐ 全部终态与异常出口的**释放矩阵**

| 出口 | 配对（`worker↔target`） | 预定（`task→target`） | 机械读数（§六） |
|---|---|---|---|
| `[5] Done` | **释** | **释** | 残留＝0 · 工人释放 ✓ |
| `[6] Aborted`（含 `BuildingDied`／`ExternalAbandon`） | **释** | **释** | 残留＝0 · 工人释放 ✓ |
| `Unassign` ×｛`WorkerDied`／`Unreachable`／`Timeout`／`Replan`｝ | **释** | **留** | 配对＝0 · 预定＝1（`restorePending=false`）· 工人释放 ✓ |
| `[7] Restore` **进入** | **释** | **留（＋待复验）** | 配对＝0 · 预定＝1 · `restorePending=true` ✓ |
| `[7]` **重验成立** | 留空（**不重建**） | **留**（清标记） | `restorePending=false` · 配对＝0 ✓ |
| `[7]` **重验不成立**（→ Aborted） | 释（已释） | **释** | 残留＝0 ✓ |
| **超时回收**（`SweepExpired`） | **级联释** | **释** | 名单含被回收任务号 · 其配对级联释放 · 未到期者不受影响 ✓ |
| **显式放出目标**（`ReleaseReservation`） | **级联释** | **释** | P1 保持 0 违反 ✓ |
| `⏸` 挂起／恢复 | 留 | 留 | 表行不变 ✓ |
| **终态零残留（P3）** | — | — | 8 任务混合场景：4 终态**残留 0**、4 存活**预定在册** ✓ |

⭐ **不变量（探针每步自检）**：**P1** 配对 ⊆ 预定（违反 **0**）／**P2** 同目标占用模式一致（违反 **0**）／**P3** 终态零残留（残留 **0**）／**P4** 一工人至多一配对（违反 **0**）。

---

## 六、机械验证读数（探针 · 可复现）

> 载具：`Logs/_hh339_binding_probe.cs` ＋ payload `Logs/_hh339_binding_payload.json`；封存产物 **`Logs/hh339_binding.log`（79 行 · 封存时刻 2026-09-25 17:14:18）**；`Unity=2022.3.62t7 · isPlaying=False`（Edit 模式 · 纯数据无单例依赖）。
> ⭐ **总判据行：`== 结论：越界/断言不成立计数 = 0 ==`**

| 判据 | 读数（逐条） |
|---|---|
| **幂等** | `Reserve` 二次＝`AlreadyReserved`（行数 1→1）；`Pair` 二次＝`AlreadyPaired`（行数 1→1）；`Unpair` 二次＝`NotPaired`；`ReleaseOnTerminal` 二次＝`NotReserved`；`ReleaseReservation` 二次＝`NotReserved`；`SweepExpired` 同参二次回收 **0** |
| **同目标重复预定（冲突）** | 独占目标：第二个预定＝`ReservationConflict`（目标预定数恒 1 · 被拒者**零行**）；**混占①**（独占在先 ＋ 后到「可多人」）＝`ReservationConflict`；**混占②**（「可多人」在先 ＋ 后到独占）＝`ReservationConflict`；两行「可多人」并存＝2 行 ✓；同任务**换目标**＝`ReservationConflict`（⛔ 不隐式改目标） |
| **级联（P1）** | 已配对任务 `ReleaseReservation` ⇒ 预定 `False`／配对 `False`／**工人释放 `False`** ✓ |
| **配对前置（P1）／工人占用（P4）** | 无预定 ＋ 已派 ⇒ `NoReservation`；同工人第二配对 ⇒ `WorkerBusy`（被拒者零行）；卡侧工号与入参不符 ⇒ `StateMismatch`；`workerId=0` ⇒ `WorkerRequired`；`kind=None` ⇒ `InvalidTarget`；`Created` 阶段预定 ⇒ `StateMismatch` |
| **释放矩阵** | `(a) Done`／`(b) Aborted` ⇒ 预定 `False`／配对 `False`／工人释放；`(c) Unassign` ⇒ 配对 `False`／预定 `True`；`(d) EnterRestore` ⇒ 配对 `False`／预定 `True`／`restorePending=True`；`(e) RestoreValidated` ⇒ `Ok`／预定 `True`／标记清／配对不重建；`(f) Restore 拒绝` ⇒ `Aborted` ＋ 残留 0／工人释放 |
| **超时** | `(g1) Sweep(now=50)` ⇒ 回收预定 **1** ＋ 级联配对 **1** · 名单 `[35]` · 未到期（`deadline=100`）不受影响；`(g2)` 重复 Sweep ⇒ 回收 **0**；`(g3) Sweep(now=1000)` ⇒ 回收 2 · **预定行 0／配对行 0**；`(g4)` `deadline=+∞`（未设）者 `Sweep(1e9)` ⇒ 回收 **0**（不误杀） |
| **终态零残留总检（P3）** | 8 任务混合：4 终态（2 `Done`＋2 `Aborted`）⇒ **残留 0**；4 存活 ⇒ **预定在册 4**（配对 0 · 全为待派）；表规模 预定 4／配对 0；不变量违反 **0** |

### 6.1 ⚠️ 本片自纠 2 处（**探针首跑抓到 · 已修 · 复跑全绿**）

| # | 现象 | 根因 | 修法 |
|---|---|---|---|
| 1 | 首跑报 **P1 违反「任务 22 有配对无预定」**且该违反**级联污染后续全部判据行** | `ReleaseReservation(taskId)` **只删预定、不碰配对** ⇒ 公开口可破坏 **P1** | 改为**级联释放配对**（`M:140-149`），并在释放矩阵新增该行 |
| 2 | 首跑报 **P2 违反「目标多行但存在不许多人行」（任务 11 ＋ 13）** | `Reserve` 的冲突判定**只看本次入参**（`!targetAllowsMultiple`）⇒ 独占行与多人行可**混占**同一目标（让「可多人」声明自相矛盾） | 冲突判定改为「本次不许多人 **或** 既有行含不许多人」（`M:120-126` ＋ `HasExclusiveHolder` `M:381`） |

---

## 七、对象引用机械扫描（反射 ＋ 源码双证）

| 检查 | 读数 |
|---|---|
| 管理器**字段泛型实参**（递归到值结构内部） | `Dictionary<Int64,TaskReservation>`／`Dictionary<TaskTargetKey,List<Int64>>`／`Dictionary<Int64,TaskPairRecord>`／`Dictionary<Int32,Int64>`／`List<Int64>` —— **全为值类型／string** |
| 表行结构体字段 | `TaskReservation{taskId:Int64; target:TaskTargetRef; ability:String; deadline:Single; multiAllowed:Boolean; restorePending:Boolean}`／`TaskPairRecord{taskId:Int64; workerId:Int32; target:TaskTargetRef; ability:String; status:TaskPairStatus}`／`TaskTargetKey{kind:Byte; cellX:Int32; cellY:Int32; typeKey:int}` |
| **引用类型命中**（递归扫） | **0** |
| **`UnityEngine.Object` 可赋值字段** | **False** |
| 源码禁词（`ITaskSource`／`KingdomTask`／`WorkerTask`／`NPCBrain`／`TaskScheduler`／`UnityEngine`／`MonoBehaviour`／`BuildingAbilityCatalog`） | M5-B 两文件 **全 0 命中**（⚠️ 首查曾命中 `MonoBehaviour×1` ＝ **我自己的注释**，改写归零 —— 同 `HH.338` 教训 ②） |
| 行尾/编码 | `TaskBindingTypes.cs` 129 行 · CRLF=0 · 无 BOM · 行尾空白 0；`TaskBindingManager.cs` 431 行 · CRLF=0 · 无 BOM · 行尾空白 0 |
| 对象侧（`source`／`worker`／`scheduler` 引用） | ⛔ 管理器**不存卡片引用**（表键值全值）；入口的卡片参数仅作**交叉校验**用，⛔ 不入表 |

---

## 八、全仓旧链调用面扫描（⛔ 未迁移／未删除的机械证据）

> 脚本 `Logs/_hh339_scan.ps1` → 产物 `Logs/hh339_scan.log`（行数口径＝**命中行数**，与 `HH.337`／`HH.338` 同口径）。

| 口径（域=`_Game`） | 本批现读 | 基线 | 判定 |
|---|---|---|---|
| `TaskScheduler.Instance\|HasInstance` | **54 行 / 15 文件** | 54／15 | ✅ 逐数相同 |
| `new KingdomTask(`（仅代码） | **14 行 / 9 文件** | 14／9 | ✅ 相同 |
| `TaskScheduler.`（含注释） | **96 行 / 27 文件** | 96／27 | ✅ 相同 |
| `ITaskScheduler`（含注释） | **3 行 / 2 文件** | 3／2 | ✅ 相同 |
| `WorkerTask` / `WorkerTaskType` | **26 行/10 文件** · **6 行/2 文件** | 同 | ✅ 相同 |
| `ITaskSource` 实现者（Assets 全域） | **9 处 / 9 文件** | 9 类／9 文件 | ✅ 相同（⛔ **一个未迁移**） |
| `KingdomTaskType` 项数 | **11 项** | 11 | ✅ 相同（⛔ 一个未删） |
| `ITaskScheduler` 方法数 | **10** | 10 | ✅ 相同 |
| **新协议零接线**：`TaskCard`／`TaskLifecycleState`／`TaskLifecycleRules`／`TaskBindingManager`／`TaskBindingResult`／`TaskReservation`／`TaskPairRecord`／`TaskTargetKey`／`TaskSweepReport`／`ITaskRestoreValidator` 在 `_Game`（排除 4 个协议文件自身） | **全 0 行** | — | ✅ **未接入旧生产链** |

---

## 九、编译／warning／git 证据

| 项 | 读数 |
|---|---|
| 编译 | `manage_editor request_compile` ⇒ **`Compilation completed successfully`** · **errors ＝ 0**（`read_console` error 面仅 1 条**非错误信息**：`287 node options failed to load and were skipped.`） |
| DLL 时间戳 | `Assembly-CSharp.dll` **17:13:35** ＞ 末次源码改动 `TaskBindingManager.cs` **17:13:13**（`TaskBindingTypes.cs` 17:07:44）⇒ 含本批 |
| **warning 数／归属** | **15 条 · 全部为既有项 · 无一条在本批改动面**（逐条归属）：`GroundEffectManager:95`／`ToastManager:47`／`ChestManager:40`（CS0114）＋ `IUIPanel:11/:14`（CS0108）＋ `BuildingMenuPanel:169/:186`／`ProjectileManager:280`／`FormationPanel:128/:139`（CS0252/53）＋ `NPCBrain:906`（CS8632）＋ `CameraSetup:30/:32`／`PathfindingScheduler:42`／`VisionSystem:15`（CS0414） |
| `git diff --check`（工作区） | **exit=0 · 0 行** ✅ |
| 暂存面 | **0 行**（⛔ 本批未 commit） |
| 本路径已跟踪件改动 | **0**（`TaskScheduling/` 恰 4 行 `??`：2 `.cs` ＋ 2 `.meta`） |
| `.meta` 在场 | `TaskBindingTypes.cs.meta`／`TaskBindingManager.cs.meta` **True**（各 267 B） |
| ⭐ **四本策划账本零触碰** | `_编号登记.md` **mtime 16:27:09**（＝`HH.338` 登记批次所留 · 早于本批开工 17:05）· `_任务队列.md` 11:14:46 · `_当前快照.md` 11:15:12 · `_测试基线台账.md`（该路径不存在）⇒ **本批未写、未改、未提交** |

---

## 十、未完成项 ／ 未接入项（**如实列出**）

1. **未接入旧生产链**：新协议 10 个类型名在 `_Game`（排除协议文件）**命中全 0** ⇒ 跑的世界里**零调用点**；⛔ **不得**据此声称游戏任务能力已改变。
2. **触发未接线**：管理器口的调用点（卡片各出口 → `Reserve`／`Pair`／`Unpair`／`ReleaseOnTerminal`／`EnterRestore`／`RestoreValidated`／`SweepExpired`）**一处未接**（归 `M5-C`）；本片只证明「照规则调用 ⇒ 契约成立」。
3. **旧 9 类广告源未迁移**（`ITaskSource` 9 处不变）· 旧枚举**一个未删**（`KingdomTaskType` 11 项）· `WorkerTask` 未换卡片。
4. **超时阈值／时基未落**：`06 §六 :133`「预定带超时」的**机制在场**（`deadline` 字段 ＋ `SweepExpired(now)`），但**秒数与时间源由调用方给**（⛔ 本片不落政策）；`deadline ≤ 0 / NaN ⇒ +∞（未设）`＝中性占位。
5. **被回收任务的卡片处置未编排**：管理器**不持卡片引用** ⇒ `SweepExpired` 只回名单，**不把卡片拉回待派／作废**（归 `M5-C`）。
6. **第二处配对未纳入**：`ScheduleCenterStub._crewAssignments`（乘员派发）仍是独立配对载体 —— 「单管理器」判据（`06 §九-6`）**未在本片处置**。
7. **能力名与目标键编码未定**：`ability` 字符串、`typeKey`／`fieldKey` 的编码 ↔ 现码对象字段的映射表**不存在**（归 `M5-C`）。
8. **同工任务仲裁（`D814`）未接**：`⏸` 只落「表行不变」，在册任务不得降权的**刺激层**规则不在本片。
9. **未跑进局**：纯数据结构（Edit 模式探针）⇒ 判据的**运行面**不在本片（⛔ 不判活世界行为）。
10. **未落政策**：优先级／`retry`／分域粒度／统计粒度 四项（`06 §十-2~-5`）**继续不落值**；本片 `TaskSweepReport` 只报回收计数（⛔ 非统计口径、不落历史）。
11. **旧 `.asset` 序列化键未清理**（`isMineByproduct:`／`interactableType:` 等）—— 按任务书**另开资产清理批次**。
12. **列报（非本片引入）**：`git diff --check` 在 **Unity 生成的 `.meta`** 上恒报 12 行（`userData:`／`assetBundleName:`／`assetBundleVariant:` 行尾空格）；全仓 `.meta` 抽样 **400/400** 同型 ⇒ 若要归零，需**全仓统一策略**（⛔ 本片不擅自动）。

---

## 十一、应登记项（⛔ 本批**未写账本** —— 供策划端回填）

```text
| **HH.339** | **`M5-B` 配对与预定 交付报告**（执行端 · 承 `HH.338` 收口 `ea6ad129` 后签发 · ⭐ 取号＝实时水位线 `HH.338`＋1，⛔ 未改四账本）：**只增载体**（`TaskBindingTypes.cs` 129 行 ＋ `TaskBindingManager.cs` 431 行 ＋ 两 `.meta`；该路径**已跟踪件改动 0**，`M5-A` 两文件 **blob 逐位未变**）。**契约**：配对表＋预定表（键全为 `taskId`／值标识 · 管理器⛔ 不持卡片引用）＋ 七出口释放口；**不变量 P1~P4 违反 0**；⭐ 探针全绿（`结论：越界/断言不成立计数 = 0` · 封存 `Logs/hh339_binding.log` 17:14:18）：幂等（Reserve/Pair/Unpair/Release×2 各幂等）· 冲突（独占第二预定 Conflict · **混占①②均 Conflict** · 同任务换目标 Conflict）· 释放矩阵（Done/Aborted 两表清 · Unassign 配对释预定留 · EnterRestore 配对释预定留+待复验 · RestoreValidated 留预定清标记 · Restore 拒绝零残留）· 超时（回收 1 + 级联配对 1 · 重复 Sweep 0 · `+∞` 不误杀）· 终态零残留 8 任务混合＝0。**对照读数逐数相同**（54/15 · 14/9 · 96/27 · 9 类 · 11 项 · 10 方法）＋**新协议命中全 0 ⇒ 未接入旧生产链**。编译 **errors=0**（warning 15 全既有）· `git diff --check` exit 0 · ⛔ 未 commit／未 push。⚠️ 自纠 2 处（`ReleaseReservation` 破坏 P1 ⇒ 改级联；同目标混占未拦 ⇒ 加 `HasExclusiveHolder`）由探针首跑抓到、修后复跑全绿。未完成 12 项（接线归 M5-C／旧面未迁移未删／超时阈值与时基未落／回收后卡片处置未编排／第二处配对未纳入／键编码未定／未跑进局／政策四项未落／`.asset` 键另批／`.meta` 空白列报）。 | 执行端 | 🟡 **已落盘·待策划端验收** | 2026-09-25 | 报告 `多Agent交接/执行端/HH.339_M5-B配对与预定_交付报告.md`；读数 `Logs/hh339_binding.log`／`hh339_scan.log`／`hh339_git.log`／`hh339_anchor_map.log` |
```

---

## 十二、⭐ `HH.339` 收口提交 ＋ 补齐证据（逐项对账策划端清单）

### 12.1 提交（**只 4 路径** · 全证据 `Logs/hh339_commit.log`）

| 要求输出 | 读数 |
|---|---|
| `git diff --cached --name-status`（提交前） | **4 行 `A`**：`TaskBindingManager.cs`／`TaskBindingManager.cs.meta`／`TaskBindingTypes.cs`／`TaskBindingTypes.cs.meta`（⛔ 无第 5 路径） |
| `git diff --cached --stat`（提交前） | `4 files changed, **580 insertions(+)**`（`.cs` 430＋128 行 · `.meta` 11＋11 行） |
| `git show --stat --oneline HEAD`（提交后） | **`9432e4fc HH.339 M5-B：配对表与预定表（管理器侧契约·未接线）`** · 4 files, 580 insertions(+) · 4 条 `create mode` |
| 提交前 HEAD（基线） | **`ea6ad129`**（`HH.338`）✅ |
| `git status --short`（提交后） | 暂存面 **0 行**；列表＝**开工前既有脏点集合**（`.gitignore`／`GameScene.unity`／`Packages`／`pixel-forge`／美术 png／若干 `.md`）＋ 未跟踪件 ⇒ 本批新文件已不在列表中 |
| **commit hash（全文）** | **`9432e4fc7a730b72766385ab17e0337e6f479edd`** |
| ⛔ 未带 | 交付报告（`HH.336`／`337`／`338`／`339` 四份）· 四本策划账本 · 旧五类契约 · 任意资产／Prefab／场景 · ⛔ 未 push |
| ⭐ 四账本零触碰（提交后复核） | `_编号登记.md` **mtime 16:27:09** ／ `_任务队列.md` 11:14:46 ／ `_当前快照.md` 11:15:12 ⇒ **早于本批开工（≈20:40）** ⇒ 本批未写、未改、未提交 |
| pre-commit 钩子 | `[WARN] 疑似未提交交付件`：四份 HH 报告未入提交（**不阻断**；按指令只提交 4 路径 ⇒ 有意） |

### 12.2 补齐证据（逐项 · 按要求顺序）

**① `Expect` 调用点总数** —— ⚠️ **计数单位必须分列**（否则两个数都对不上）：

| 口径 | 读数 | 证据 |
|---|---|---|
| `Expect(` **字面量总数**（含 1 处**定义行** `void Expect(...)`） | **44** ← **＝策划端读数** | `Logs/hh339_inventory.log [1]` |
| **调用点**（剔除定义行） | **43** | 同上（逐行 43 条清单已打印，含行号） |
| 本轮**执行**数 | **43** | `Logs/hh339_binding.log:85` |

**② 失败断言数 ＝ `0`** —— 运行时 `expectFailed = 0`（`hh339_binding.log:85`）＋ 步内失败计数 `broken = 0`（同日志末行 `越界/断言不成立计数 = 0`）。
⚠️ 列报：**2 处「只打印未断言」的契约**（§三「阶段=待派 时 `Pair` ⇒ `StateMismatch`」、§四(e)「卡侧 `RestoreValidated` 返回值」）—— 本批**未补断言**（⛔ 不为了凑数改断言集）；如需纳入断言清单，请在验收裁决中示下。

**③ 释放／删除语义出口逐个列出**

| # | 出口 | 行 | 语义 | 两表动作 | 内部调用 |
|---|---|---|---|---|---|
| 1 | `ReleaseReservation(long taskId)` | `M:140` | 显式放出目标（目标变化等） | 预定**释** ＋ 配对**级联释** | `RemoveReservation` `M:144`／`RemovePair` `M:146` |
| 2 | `Unpair(TaskCard)` | `M:194` | `Unassign` 回待派 ⇒ 解绑 | 配对**释** ＋ 预定**留** | `RemovePair` `M:201` |
| 3 | `ReleaseOnTerminal(TaskCard)` | `M:213` | `[5] Done`／`[6] Aborted` 出口 | 配对**释** ＋ 预定**释** | `RemovePair` `M:220`／`RemoveReservation` `M:222` |
| 4 | `EnterRestore(TaskCard)` | `M:231` | `[7] Restore` 进入 | 配对**释** ＋ 预定**留（置 `restorePending`）** | `RemovePair` `M:238`（置位 `M:241-242`） |
| 5 | `SweepExpired(float now, List<long>)` | `M:274` | 超时兜底回收（`deadline ≤ now`） | 预定**释** ＋ 配对**级联释** | `RemoveReservation` `M:288`／`RemovePair` `M:294` |
| 6 | （内部）`RemoveReservation(long, TaskReservation)` | `M:418` | 表 1 删除唯一实现 | — | `IndexRemove` `M:420` ＋ `_reservations.Remove` `M:421` |
| 7 | （内部）`RemovePair(long, TaskPairRecord)` | `M:424` | 表 2 删除唯一实现 | — | `_workerToTask.Remove` `M:427` ＋ `_pairs.Remove` `M:428` |

⭐ **结论**：**全部释放语义出口 ＝ 5 个公开口**（1~5）＋ **2 个私有助手**（6~7）；⛔ 无旁路出口。

**④ `RemoveReservation` / `RemovePair` 全部调用点清单**（定义 2 处 ＋ 调用 8 处）

| 被调 | 调用点（行：宿主出口） |
|---|---|
| `RemoveReservation` | `M:144`（`ReleaseReservation`）· `M:222`（`ReleaseOnTerminal`）· `M:288`（`SweepExpired`）· 定义 `M:418` |
| `RemovePair` | `M:146`（`ReleaseReservation` 级联）· `M:201`（`Unpair`）· `M:220`（`ReleaseOnTerminal`）· `M:238`（`EnterRestore`）· `M:294`（`SweepExpired` 级联）· 定义 `M:424` |

**⑤ 直接表删除点清单**（`_*.Remove(...)` 直删 · 防「绕过助手」）

| 行 | 语句 | 位置 | 可否被外部绕过 |
|---|---|---|---|
| `M:421` | `_reservations.Remove(taskId)` | `RemoveReservation` 体内 | ⛔ 不可能（唯一实现） |
| `M:428` | `_pairs.Remove(taskId)` | `RemovePair` 体内 | ⛔ 不可能（唯一实现） |
| `M:427` | `_workerToTask.Remove(row.workerId)` | `RemovePair` 体内（含 `holder == taskId` 归属判定） | ⛔ 不可能 |
| `M:415` | `_reservationsByTarget.Remove(key)`（列表空时） | `IndexRemove` 体内（仅被 `RemoveReservation` 调） | ⛔ 不可能 |
| `M:277` | `_sweepBuf.Clear()` | `SweepExpired` 体内 | ⚠️ 非表删除（**复用缓冲**） |

⇒ **直接表删除点 4 处，全部位于 2 个助手（＋其私有 `IndexRemove`）内部**；表外零直删点。

**⑥ `P1`~`P4` 复跑结果**（`hh339_binding.log §八` · 本轮全绿）

| 不变量 | 读数 | 驱动 |
|---|---|---|
| **P1** 配对 ⊆ 预定 | 违反 **0** | §二「显式释放级联」＋ §三「无预定不配对（`NoReservation`）」＋ 全步 `VerifyInvariants` |
| **P2** 同目标占用模式一致（⛔ 不混占） | 违反 **0** | §二 混占①（独占在先→多人被拒）／混占②（多人在先→独占被拒） |
| **P3** 终态零残留 | 残留 **0** | §五 8 任务混合（4 终态 ⇒ 两表零残留；4 存活 ⇒ 预定在册） |
| **P4** 一工人至多一配对 | 违反 **0** | §三 `WorkerBusy` ＋ 工人／目标索引一致性违反 = 0 |
| （汇总）不变量违反明细**总条数** | **0** | 同节 |

**⑦ 新协议旧链命中仍为 `0`**（`Logs/hh339_scan.log` · 本批复跑）

- **A 组 旧链面逐数相同**：`TaskScheduler.Instance|HasInstance` **54 行/15 文件** · `new KingdomTask(` 仅代码 **14 行/9 文件** · `TaskScheduler.` 含注释 **96 行/27 文件** · `ITaskScheduler` **3 行/2 文件** · `WorkerTask` 26 行/10 文件 · `WorkerTaskType` 6 行/2 文件 · `ITaskSource` 实现者 **9 处/9 文件** · `KingdomTaskType` **11 项** · `ITaskScheduler` **10 方法**。
- **B 组 新协议零接线**：`TaskCard`／`TaskLifecycleState`／`TaskLifecycleRules`／`TaskBindingManager`／`TaskBindingResult`／`TaskReservation`／`TaskPairRecord`／`TaskTargetKey`／`TaskSweepReport`／`ITaskRestoreValidator` 在 `_Game`（排除 4 个协议文件自身）**命中全 0**。
- **D 组 入库面完整性**：`M5-A` 两文件 blob ＝ `HEAD` 逐位相同（`e4613adfab6e`／`ae7aee83643a`）。

**⑧ 编译错误、warning 归属、`git diff --check`**

| 项 | 读数 |
|---|---|
| 编译 | 末次真实编译 **`Compilation completed successfully`**（`Assembly-CSharp.dll` **17:13:35** ＞ 末次源码改动 **17:13:13** ⇒ **含本批两文件**）；本批收口时再请求编译 ⇒ 回 `No compilation was observed`（**无源码变更** ⇒ 符合预期，⛔ 不是失败） |
| **warning 归属** | **15 条 · 全部为既有项 · 无一条在本批改动面**：`GroundEffectManager:95`／`ToastManager:47`／`ChestManager:40`（CS0114）· `IUIPanel:11/:14`（CS0108）· `BuildingMenuPanel:169/:186`／`ProjectileManager:280`／`FormationPanel:128/:139`（CS0252/53）· `NPCBrain:906`（CS8632）· `CameraSetup:30/:32`／`PathfindingScheduler:42`／`VisionSystem:15`（CS0414） |
| 控制台 **error 面 2 条 · 均非本批**（如实列报） | ① `com.unity.visualscripting@1.9.4/…/UnitBase.cs:131`「287 node options failed to load」—— ⭐ 陈旧节点名单中的类型 ＝ **`InteractableType`／`SpawnerComponent`／`RiftComponent`／`WaterNetwork`**（**全部是既往批次已删／已退役**的类型）＋ **不含本批任何类型**：`filter_text=TaskBinding` **0 条**／`filter_text=TaskCard` **0 条** ② `NullReferenceException`，栈＝`UnityEditor.PackageManager.UI.Internal.AssetStoreDownloadManager.OnBeforeSerialize` ⇒ **编辑器内部（包管理器 UI）**，与项目代码无关。⇒ ⛔ 两者均**不计入**本批改动面（前者为 `HH.335`／`HH.336` 已列报的同一条噪声） |
| `git diff --check`（工作区） | **exit=0 · 0 行** ✅ |
| `git diff --check --cached`（提交面） | exit=2 · **12 行** —— **全部在两个 Unity 生成的 `.meta`**（`userData:`／`assetBundleName:`／`assetBundleVariant:` 行尾空格）；全仓 `.meta` 抽样 **400/400 同型** ⇒ 既有格式、非本批引入、⛔ 未擅改（列报） |

**⑨ ⭐ 禁词扫描（代码面 / 注释面 **分列** —— 承「⛔ 不得用改注释冒充代码清零」）**

> 扫描器：`Logs/_hh339_code_vs_comment.py`（C# 极小状态机：`//`／`/* */`／`"字符串"`／`@"逐字串"` 逐一分离）→ 产物 `Logs/hh339_code_vs_comment.log`。

| 组 | 文件 | **代码命中** | **注释命中** | 逐词明细 |
|---|---|---|---|---|
| A 协议·`M5-A`（已入库） | `TaskCardProtocol.cs` | **0** | **0** | 代码: 0 ｜ 注释: 0 |
| A 协议·`M5-A`（已入库） | `TaskLifecycleRules.cs` | **0** | **0** | 代码: 0 ｜ 注释: 0 |
| B 协议·`M5-B`（本批） | `TaskBindingTypes.cs` | **0** | **0** | 代码: 0 ｜ 注释: 0 |
| B 协议·`M5-B`（本批） | `TaskBindingManager.cs` | **0** | **0** | 代码: 0 ｜ 注释: 0 |
| C 验证脚本（`Logs`·不入库） | `_hh339_binding_probe.cs` / `_hh338_lifecycle_probe.cs` | 13 / 13 | 0 / 0 | 探针需指名旧类型做对拍 ⇒ 预期 |
| **D 对拍·旧文件**（应有代码命中） | `TaskScheduler.cs` | **113** | **15** | 证明扫描器**能检出代码命中**（非假阴性） |

⇒ **判定口径**：**代码清零以「代码命中」列为准**；注释面单列，⛔ **不得当代码证据**。本批两文件**代码面 0／注释面 0**。

### 12.3 ⛔ 未开 `M5-C`（遵守指令）

- 本批**到此为止**：`M5-C` 未开工、未提案、未改任何 `M5-C` 面。
- ⭐ **`M5-C` 前置（待策划端裁决）**：**四项待决政策** —— ① `priority`（由谁定 · `06 §十-2`）② `retry` 默认次数与「不可达」冷却（`06 §十-3`）③ 分域粒度（大区块 vs 王国 · `06 §十-4`）④ 统计／日志粒度（`06 §十-5`）。
- 本片已把三处「拒绝填值」做成**可执行机制**（`priority` 不读不排序／`retryMax=-1` 拒绝消费／`deadline` 与 `SweepExpired(now)` 由调用方给值 · 零统计零日志）⇒ 政策落值后 `M5-C` 只需接线，无需回改契约。

---

## 十三、零改声明 ／ 附：复现命令

- **零改声明**：⛔ 本批（`M5-B`）未改任何既有 `.cs`／`.asset`／`.prefab`／`.unity`／`.meta`；⛔ 未 commit／未 push；⛔ 未改四本策划账本；⛔ 未进 Play；⛔ 未清 `.asset` 序列化键。（Part 一 的 `HH.338` 提交＝**只** 4 个既定路径，§一 已给全证据。）

```powershell
# ① 编译（bridge）
pwsh -NoProfile -File 'Logs/_bridge_call.ps1' -Payload 'Logs/_hh338_compile_payload.json'
# ①′ 代码面/注释面分列 禁词扫描（⛔ 不得用改注释冒充代码清零）
python Logs/_hh339_code_vs_comment.py
# ①″ 断言/删除点清点
pwsh -NoProfile -File 'Logs/_hh339_inventory.ps1'
# ② 配对/预定机械验证（Edit · 只读 · 落盘 Logs/hh339_binding.log）
pwsh -NoProfile -File 'Logs/_bridge_call.ps1' -Payload 'Logs/_hh339_binding_payload.json'
# ③ 旧链调用面 ＋ 新协议零接线 ＋ 源码面扫描
pwsh -NoProfile -File 'Logs/_hh339_scan.ps1'
# ④ git 证据（含四账本零触碰核对）
pwsh -NoProfile -File 'Logs/_hh339_git.ps1'
```

## 十四、主策划端补落终裁（HH.339，2026-09-26）

### 终裁：判绿（带观察项）

- [实读] Logs/hh339_binding.log:1-18 的幂等、冲突、释放、Restore、超时读数全部归零；:80-83 明列 P1/P2/P3/P4 违反 0；:85 明列调用点 43、执行 43、失败 0。计数单位已区分：Expect( 字面量总数 44（含定义行），调用点 43。
- [实读] Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskBindingManager.cs:159-187,194-202,213-263,274-294,418-428 是五个公开出口、Restore/超时释放及两个私有清理助手；TaskBindingTypes.cs:48-76,106-107 的键和行结构为值数据。
- [实读] Valley Rampart/Assets/_Game/Systems/AI/Schedule/ScheduleCenterStub.cs:56,118-132,181-182 仍有第二处 _crewAssignments 配对载体。[推断] 因此 06 §九-6 的“全库单管理器”在本批不判达成，作为明确边界挂账，不反向否定本批管理器侧契约。
- [实读] B-7 两处（待派阶段 Pair、卡侧 RestoreValidated）代码入口已有返回值守卫，探针仅未将其计入 Expect 断言；本裁接受列报，要求后续探针把两处转成正式断言，作为观察项，不要求为本批返工改码。
- [未知] 第二处配对载体何时并入唯一管理器、以及两处观察断言何时补入后续探针，不在本批替用户定时点。

准入结论：HH.339 的 M5-B 管理器侧契约成立，可与 HH.340 的 M5-C 并存接线结论共同作为“并存”阶段证据；不构成 06:154 ②切换或③清理准入，单管理器与旧链收敛须另取裁决。

