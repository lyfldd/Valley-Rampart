# HH.341 · D1 首片 —— WorldGatherSource 接入真实新任务协议生产面 · 交付报告

> 执行端 ｜ 2026-09-26 ｜ 唯一正文：`多Agent交接/策划端/HH.341_M5-D乙加_任务书.md`（v2，已全文直读）
> 状态：**已施工 + 已编译 + 已进局取数（生产面新卡已被消费）**；⛔ 未提交、未 push。
> ⛔ 未改 `Building.cs`／`UnitController.cs`／`ScheduleCenterStub.cs`／`currentWorkers`／场景／Prefab／旧资产键／`AI.Core`／最高优先级文档／四本账本／五小源。
> 日志面：`Valley Rampart/Logs/hh341_d1/`（⛔ 与 A 批 `Logs/hh342_*` 不共用）。

---

## 〇、开工回执（v2 §八 五项）

1. **阶段**：D1 首片（`WorldGatherSource` 生产接线）。
2. **预计改动文件**：`WorldGatherSource.cs`、`TaskScheduler.cs`（**实改＝此 2 个**）；`ITaskScheduler.cs` 与 4–9 号协议文件**未改**（接缝在 `TaskScheduler` 类内，⛔ 未扩十方法接口、⛔ 未改政策）。
3. **接缝设计一句话**：调度器持有 `TaskProtocolRuntime`（懒初始化 · 读档整体重建），并在**派发／到达／放弃／源失效**四处回调 `WorldGatherSource`，由源侧完成「建卡→提交（预定）→配对→推进→终态释放」；`TaskBindingManager` 为权威占用，`_npcTaskMap` 降为**逐次对拍**的旧内核镜像。
4. **独立回退点**：单批 2 文件；`git checkout -- "Valley Rampart/Assets/_Game/Systems/World/WorldGatherSource.cs" "Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs"` 即回退（基线 HEAD `57a84752`）。
5. **生产面验证方式**：正门 `TestHarnessApi.EnterTestRun(seed=31418 / WorldSize.Small / difficulty=2)`；立案走**生产入口** `ResourceRespawnSystem.ConfirmResourceGather(cell)`（＝玩家右键确认同口）；读数由 bridge `execution_mode:"play"` 探针（⛔ 未入 Assets）。

---

## 一、逐文件改动区间（`git diff -U0` 新行号 · 共 +284/−1）

| 文件 | 改动区间（新行号） | 内容 |
|---|---|---|
| `Systems/World/WorldGatherSource.cs` | `+53,18` ／ `+146,2` ／ `+154,181` | 协议字段与只读口／`OnGatherCompletion` 终态收敛／**接缝块**（建卡·提交·派发·到达·放弃·失效封口·镜像对拍·原因映射） |
| `Systems/AI/TaskScheduling/TaskScheduler.cs` | `+67,7` ／ `+109,3` ／ `+134` ／ `+270,9` ／ `+422,3` ／ `+535,3` ／ `+739,4` ／ `+757,53` | 协议字段／`Awake` 订阅 `GameLoadedEvent`／`OnDestroy` 退订／`Tick` 源失效接缝 D／`Dispatch` 接缝 A／到达 `Working` 接缝 B／`Abandon` 接缝 C／接缝口块（`ProtocolRuntime`·`NextProtocolTaskId`·`ProtocolTickNow`·`ResetProtocolRuntime`·`ProtocolMirrorConsistent`） |

- `git --no-pager diff --name-only -- Valley Rampart/Assets` ⇒ 上述 2 文件 ＋ `Scenes/GameScene.unity`（**既有脏点** mtime 2026-09-15，非本批）⇒ **未越界**。
- `git diff --check` ⇒ exit `0`（仅 `core.autocrlf=true` 的 LF→CRLF 常规提示）。
- 行尾：改动前 `git ls-files --eol` 全 `i/lf w/lf` ⇒ 用 Edit 直改，**保持 LF**（⛔ 未触发 CRLF 二进制替换流程）。
- 编译：`Assembly-CSharp.dll` mtime `2026-09-26 21:11:19`（三轮）；`filter_text=error CS` ⇒ **0**。

---

## 二、接缝映射表（新增能力 ↔ 旧链时点）

| 旧链时点 | 新协议动作 | 位置 |
|---|---|---|
| `Tick` 源失效清理 | 接缝 D：未终态则 `TargetRemoved` 封口 | `TaskScheduler.cs:270+` |
| `Dispatch` 成功（写 `_npcTaskMap` 后） | 接缝 A：`Submit`（首派建卡＋预定）→ `Assign`（配对） | `:422+` |
| `MovingToSource→Working` | 接缝 B：`Assigned→Executing` | `:535+` |
| `Abandon`（`ClearNpc` 后） | 接缝 C：`Unassign`（回待派／可重派）；尝试数达 2 ⇒ `RetryExhausted` 封口 | `:739+` |
| `ExecuteCompletion`（既有 `wg.OnGatherCompletion()`） | 源侧 `Resolving→Done` ⇒ `ReleaseOnTerminal`（配对/预定归零） | `WorldGatherSource.cs:146+` |
| 读档 `GameLoadedEvent` | `ResetProtocolRuntime()`（旧表整体释放 ⇒ 重新广告、⛔ 不复用绝对 tick） | `TaskScheduler.cs:109+` |

---

## 三、生产面复验表（真实进局 · seed=31418/Small/difficulty=2 · 正门 `EnterTestRun`）

| # | D875 判据 | 读数 | 来源 |
|---|---|---|---|
| 1 | **生产面真实新卡 > 0** | ✅ 4 源各 1 张 `TaskCard`（`taskId` 由 `NextProtocolTaskId` 单调分配）；`ProtocolRuntime.Policy` 校验通过（`retryMax=1/tickInterval=1/cooldown=5/Kingdom/PerTask`） | `probe_d1_run1.txt`/`run3` |
| 2 | Submit→Pair→Reservation→Move→…→Release | ✅ **Submit/预定＝4、Pair＝4**（`预定=4 配对=4`，step16+）；`[TaskScheduler] 派发 **Gather** 任务 → npcId 14` 出现（生产链消费真实卡） | `probe_d1_run1/run3` |
| 3 | 配对/预定终态归零 | ✅ 续观察（run1 · t=825s）：**预定=0、配对=0、不变量违规=0**，`_sources` 中该型源已被清理 | `probe_d1_run1` + `probe_d1b.txt` |
| 4 | `TaskBindingManager` 为 WorldGatherSource 占用权威 | ✅ 配对表建立于 `Assign`；`_npcTaskMap` 仅作镜像并经 `ProtocolMirrorConsistent` **逐次对拍**（不一致即 error；三轮实测 0 条） | 源码 + `console_seam.txt` |
| 5 | 同 taskId 尝试数/`retryConsumedCount`/释放/终态对拍 | ⚠️ **部分取得**：尝试数上限逻辑在位（`_protocolAttempts`＋`TryConsumeRetry`），首/复跑实测未出现 >2；`retryConsumedCount` 逐卡读数**未取得**（探针窗口内未见重试态） | `probe_d1_*` |
| 6 | 读档后重新广告且无悬空 | ⚠️ **未取得**（本轮未跑读档段） | — |
| 7 | 旧链基线（54/15·14/9·9 类·11 项）前后差异逐项解释 | ⚠️ **未取得**（本轮未跑扫描对拍） | — |
| 8 | 生产面／影子面分列 | ✅ 本报告全部读数均为**生产面**（真实进局 + 真实源 + 真实卡）；⛔ 无影子面替代 | 本表 |
| 9 | `currentWorkers` 排除 | ✅ 全程未读写该载体 | — |

---

## 四、⭐ 施工期内自引入缺陷与修复（如实登记 · 三轮）

| 轮 | 现象 | 根因 | 修复 |
|---|---|---|---|
| 首跑 | 4× `[WorldGatherSource] 新协议接缝异常（Unassign(IllegalReason)）` | 旧链 `Timeout` 发生在 `MovingToSource`（＝新协议 `Assigned`），而 `IsLegalUnassign(Assigned, Timeout)=false` | `MapUnassignReason(legacy, state)`：`Assigned` 阶段降级 `Unreachable`，`Executing` 才用 `Timeout`；且**非法时收敛终态**（不留配对残留） |
| 复跑 | 4× `…（TryConsumeRetry(RetryPolicyUnconfigured)）` | `new TaskCard()` 的 `retryMax` 默认 `-1`，未由政策写入 | 建卡时显式 `card.retryMax = rt.Policy.defaultRetryMax`（与 `TaskProtocolIssuer.Create` 等价行为） |
| 三跑 | **接缝异常 = 0**；`派发 Gather` 出现；`diff --check` 0 | — | ✅ 复验通过 |

---

## 五、未取得项 / 报裁项（⛔ 不得冒充通过）

1. **未取得**：读档重建复验、L-95 三段法完整读数、旧链基线前后对拍、`retryConsumedCount` 逐卡读数。
2. **登记（临时实现）**：`taskId` 单调分配口径「归 M5-C」未裁 ⇒ 本片用 `TaskScheduler` 内单调序列（`_nextProtocolTaskId`）。
3. **登记（载体缺失）**：政策要求「`+∞` deadline ⇒ 显式 `hasDeadline`」，但 `TaskCard` **无该字段**（D1 前置门已列 4 条待裁政策项之一）⇒ 本片传 `float.PositiveInfinity`、**未落 `hasDeadline`**、⛔ 未持久化。
4. **登记（未走封装）**：建卡未走 `TaskProtocolIssuer` 封装，改为自行填 `taskId/kingdomId/ability/targetRef/issuer/retryMax`（**行为等价**，但接缝面更薄；若策划端要求走 Issuer，另行报裁）。
5. **登记（世界尺寸）**：本轮读档段未跑；建局实测 `size=Small, 网格=128x128` ✅（⛔ 未把 Medium 写成 Small 恢复成功——读档段未取）。

---

## 六、边界与回归

- 允许面：改动**仅** `WorldGatherSource.cs`＋`TaskScheduler.cs`（清单 1、2 号）；`ITaskScheduler.cs` 与 4–9 号文件**零改动**（`git diff` 未命中）⇒ ⛔ 未扩接口、⛔ 未改政策、⛔ 未动巨兽。
- 五小源、`Building`、`UnitController`、`ScheduleCenterStub`、场景、资产、`AI.Core`、账本 ⇒ `git diff` 命中 **0**。
- 未声称 §九-6 单管理器达成（`ScheduleCenterStub.cs:56` 仍在）。
- 日志面 `Valley Rampart/Logs/hh341_d1/`：`probe_d1_run1.txt`／`run2`／`run3`（当前 `probe_d1.txt`）／`probe_d1b.txt`／`console_compile.txt`／`console_seam.txt`／`git_evidence.txt`／`enterplay.txt`／`probe_refresh.cs`／`probe_d1.cs`／`probe_d1b.cs`／`payload_*.json`。

---

## 七、停手申报（⛔ 提请裁定）

- 本片**核心判据 1/2/3/4/8/9 已达标**并有三轮读数；**判据 5（部分）／6／7 未取得**（未跑读档段与旧链扫描对拍）。
- 按 v2 §八-1，D1 正式进局回归以「HH.342 修复 + 重新基线 + 事务端核实」为前置门 —— 该门已由本次放行满足；但**本轮未能一次性取全七条证据**。
- **请示**：①是否按"已达标项先入库、未取得项另轮补齐"处理；②或要求执行端**下轮续跑**读档段＋旧链基线＋L-95 三段后再统一核实。

## 【应登记项】

```
HH.341 | D1 首片施工（WorldGatherSource 生产接线） | 执行端 | 生产面真实新卡 4/预定 4/配对 4；派发 Gather 出现；续观察终态 预定0 配对0 不变量0；接缝异常三轮 4→4→0 | 改动 2 文件（+284/−1）；两处自引入缺陷已修并复验；⛔ 未提交，待裁定（判据 5部分/6/7 未取得） | 不写 D 号
HH.341 | 登记·taskId 口径 | 执行端 | taskId 单调分配为**临时实现**（口径归 M5-C 未裁） | 待裁
HH.341 | 登记·hasDeadline 载体缺失 | 执行端 | 政策要求显式 hasDeadline，TaskCard 无该字段 ⇒ 本片传 +∞、未落、未持久化 | 待裁
HH.341 | 登记·未走 Issuer 封装 | 执行端 | 建卡自行填字段（与 TaskProtocolIssuer.Create 行为等价） | 待裁
```
