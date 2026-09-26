# HH.341 · 小源首源 `ConstructionSiteStore` **施工**交付报告

> 执行端 ｜ 2026-09-26 ｜ 唯一正文：`HH.341_M5-D乙加_小源阶段任务书.md`（258 行 · 已全文直读）
> ⛔ 未取新号、未写新 D 号、未提交、未 push ｜ 证据目录 `Valley Rampart/Logs/hh341_small_construction_site_*`

---

## 一、开工回执（任务书 §十一-1 / §七-1）

| 项 | 内容 |
|---|---|
| 当前源 / 顺序 | `ConstructionSiteStore` ／ 第 **1**（⛔ 未换序） |
| **开工实际 HEAD** | **`73e58fb7`**（D885「预检核实并放行施工子批」） |
| **代码回退基线（预检时）** | **`7c21f1ba`**（D884 小源任务书入库） |
| 两者差异解释 | 预检→施工之间仅策划端**入库动作**（D885）；**允许面文件零改动**（开工时 `git diff --name-only -- Valley Rampart/Assets/_Game` ＝ **空**）⇒ ⛔ 不是"不一致"，可开工 |
| 允许面清单 | 本源 `ConstructionSiteStore.cs` ＋ 7 个接缝文件（实改 **2 个**）；⛔ `ITaskScheduler.cs` 未改（diff=0） |
| 只读预检 | 已核实通过（报告 `HH.341_小源首源_ConstructionSiteStore预检_交付报告.md`）· 判定 **任务卡源** ⇒ 进入施工（§5.1） |
| 回退点 | `git checkout -- "…/ConstructionSiteStore.cs" "…/TaskScheduler.cs"`（施工前 HEAD `73e58fb7`） |

---

## 二、逐文件改动（任务书 §十一-2）

| 文件 | 性质 | file:line 区间（**改后**行号 · `git diff -U0`） | 说明 |
|---|---|---|---|
| `Systems/Building/ConstructionSiteStore.cs` | 本源 · 新增协议接缝 | `+44,21` ／ `+222,8` ／ `+252,183` | ① 协议字段与只读口（`_card`/`_failAttempts`/`_protocolTerminal`/`_lastWorkerId`）② `OnUnregister()` 实现（源注销 ⇒ 终态封口）③ 协议编排块（`EnsureCard` 经 `TaskProtocolIssuer`／`OnProtocolDispatched`／`OnProtocolArrived`／**`OnProtocolTaskCompleted`**／`OnProtocolAbandoned`／`OnProtocolSourceInvalidated`／`FinalizeProtocol`／`MirrorCheck`／`MapUnassignReason`） |
| `Systems/AI/TaskScheduling/TaskScheduler.cs` | 兼容接缝 · 5 处同型回调 | `+276,3`（Tick 源失效 D′）／`+428,3`（Dispatch A′）／`+544,3`（到达 B′）／**`+710,5`（Complete E）**／`+757,3`（Abandon C′） | 每处仅 `if (task.source is ConstructionSiteStore …) …OnProtocol…(id/ProtocolTickNow);` |

**本源特有设计（与 D1 首片差异）**：本源在一次投料期内**可被多次派工**（逐资源搬料）⇒ 新增 **接缝 E**（`TaskScheduler.Complete` 后）区分：
- 一次搬料**正常完成**且仍有缺口 ⇒ `Executing→Resolving→Pending`（`Replan`）**回待派**，⛔ **不计失败尝试**（`_failAttempts` 只在失败路径递增 ⇒ 不与"最多 2 次尝试"政策冲突）；
- 料齐/源注销 ⇒ 终态 `Done`；失败累计 2 次 ⇒ `Aborted(RetryExhausted)` 封口。
⛔ 未改政策值；⛔ 未扩 `ITaskScheduler`；⛔ 未改调度器内核逻辑（仅同型条件回调）。

---

## 三、真实生产进局链路（任务书 §十一-3）

- 正门 `TestHarnessApi.EnterTestRun(seed=31418 / WorldSize.Small / difficulty=2)`，槽 `hh341cs`；探针 `hh341_small_construction_site_probe.cs`（bridge `execution_mode:"play"`，⛔ 未入 Assets）。
- **投料态触发**：反射调 `Building.BeginMaterialPhase(Stone5+Wood5)`（＝生产入口 `BuildController.Place`／修复路径的**同一方法**；⛔ 非造卡、⛔ 非影子面、⛔ 未改业务语义）。
  - 触发读数：`castle kingdom=0 awaiting=True siteStore=True satisfied=False` ⇒ **本源真实出现**（`本源数=1`）。
- 取料仓实测：玩家池 `k=0 仓数=1 Vault Stone=100 Wood=100`（国库容器）⇒ `FindPickup` 可解析。

| 时点 | 生产面读数（原文） |
|---|---|
| 建卡 | `id=1`（`NextProtocolTaskId` 分配） |
| 预定/配对 | `预定=1 配对=1`（`ids=1 唯一=1/1`） |
| 派工 | `id=1 state=Assigned worker=16 retry=0/1 consumed=0` |
| 失败重试 | `id=1 state=Assigned worker=13 retry=1/1 consumed=1 fails=1 blockedUntil=102 lastUnassign=Unreachable` |
| **终态封口** | `id=1 state=Aborted worker=0 abort=RetryExhausted fails=2 terminal=True blockedUntil=134` |
| 终态归零 | `预定=0 配对=0`；`不变量=0`；`协议侧悬空=0` |
| **真实生产事件（旧链同局）** | `[TaskScheduler] Abandon Build → npcId 14 reason=SourceInvalid`；**`[Building] castle 料齐 ⇒ 开工（投入=10·进度开始推进）`** ⇒ 料齐达成 |

**主策划端/事务端注意（须裁）**：本轮新协议卡走 **Abort 路径**封口（工人连续 `Unreachable` ⇒ 2 次失败），而**搬料由后续工人完成**（料齐事件在场）⇒ ⭐ **`Complete` 路径未取得**（详见 §八）。

---

## 四、跨档重建 · 重新广告 · 再派工 · 资格时间（§十一-4）

| 判据 | 读数 |
|---|---|
| 运行时重建 | `Load前 rt=504413606` → `Load后 rt=-872451808`，**同一=False** |
| 读档后重新广告 | 建筑投料态随存档恢复 ⇒ 源**自动重现**（`Load后 本源数=1`）⇒ 无需人工重新触发（比 D1 首源更强） |
| 重新派工 | `## L+2s … ids=2`；`R1 … id=2 state=Assigned worker=16` ⇒ **新卡 id=2 未复用 id=1** ✓ |
| 终态归零 | `R52 … 预定=0 配对=0`（`id=2 state=Aborted abort=RetryExhausted fails=2 terminal=True`） |
| 无悬空 / 无重复卡 | 全程 `协议侧悬空=0`、`唯一=1/1`；`不变量=0` |
| ⭐ 资格时间（未复用绝对 tick） | `blockedUntil` **逐次现算**：`102`（首次失败）→ `134`（封口，≈129+5）／读档后 `170` → `202`（≈197+5）⇒ ⛔ 非同一常量、跨档新卡起始 `0` |

---

## 五、L-95 三段（§十一-5 · ⛔ 未换工具、每段 attempt=1 成功）

| 段 | 读数 | 归因 |
|---|---|---|
| **段1 Play 内**（生产链运行期） | `filter_text=ConstructionSiteStore` ＝ **2 条**：`[TaskScheduler] Abandon Build → npcId 14 reason=SourceInvalid`／`[Building] castle 料齐 ⇒ 开工（投入=10）`；`types=error` ＝ **4 条**（NRE／ruler／Theme／287 node） | 4 条与空白对照**逐条一致** ⇒ **零新增未归因 error** |
| **段2 退 Play 后** | `Exited play mode` ＋ `types=error` ＝ **4 条**（同上；本轮无 `not cleaned up`） | 与段1 同源 |
| **段3 空白对照**（只进 Play、不建局、不读档） | `types=error` ＝ **4 条**（NRE／ruler／Theme／287 node） | 常驻既有基准 |

**生产面／影子面／空白对照分列**：生产面＝§三~§四全部读数（真实进局＋真实源＋真实卡）；**影子面＝0 条**（⛔ 未使用）；空白对照＝段3 基准。
**O-14 噪声单独归档**：`NullReferenceException` 与 `ruler 缺脚本` ⇒ 归 `O-14`（⛔ 不计本源新增错误、⛔ 不作门槛）；`Theme Style Sheet`／`287 node options`＝常驻既有。

---

## 六、基线复算（§十一-6 · 域＝`Assets/_Game/**`；口径＝命中行数/文件数）

| 判据 | 开工前（D1 绿态） | 施工后 | 差异 | **逐项归因** |
|---|---|---|---|---|
| `TaskScheduler.Instance\|HasInstance` | **59 行 / 16 文件** | **64 行 / 17 文件** | **+5 行 / +1 文件** | 全部来自 `ConstructionSiteStore.cs` 新增的 **5 处**引用：`TaskScheduler.HasInstance`×2（`Rt()`／`MirrorCheck`）＋`Instance.ProtocolRuntime`／`Instance.NextProtocolTaskId`／`Instance.ProtocolMirrorConsistent` 各 1；该文件由 0 命中转为命中 ⇒ **+1 文件**。✅ 无其他文件增量 |
| 本源专项 | `TaskScheduler.Instance\|HasInstance` 0 行（`:21`/`:206` 为**注释**）／`new KingdomTask(` 1 行 | **5 行 ／ 1 行** | +5 ／ 0 | 5 行＝上述接缝引用；`new KingdomTask(` 仍 1 行（`:187` 旧链未动） |
| `new KingdomTask(`（生产域） | 14 行 / 9 文件（剔注释） | **15 行 / 10 文件**（**含注释**口径） | **0** | 与基线"原始 15/10 ⇒ 剔注释 14/9"**逐位一致** ⇒ 本源**零新增任务构造** |
| `ITaskSource` 实现者 | 9 类 | **9 类** | 0 | 本类早已实现（未新增） |
| `KingdomTaskType` | 11 项 | 未改该文件 | 0 | ⛔ 未加未删 |
| 全 `Assets` 149/27 | 仅对照 | — | — | ⛔ 未当生产调用面 |

---

## 七、编译 · 清洁检查（§十一-7）

- 编译：`Assembly-CSharp.dll` mtime **`2026-09-26 23:16:27`**（本批重编译）。
- `git diff --check -- Valley Rampart/Assets` ⇒ **exit 0**。
- 改动面：`ConstructionSiteStore.cs`（+213）／`TaskScheduler.cs`（+17）＋既有脏点 `Scenes/GameScene.unity`（mtime 2026-09-15，⛔ 非本批）。
- 行尾：两文件 **`i/lf w/lf`** ⇒ 用 Edit 直改、**保持 LF**（⛔ 未触发 CRLF 二进制替换流程）；⛔ 无 BOM/NUL 变更。
- **越界零命中**：`AI.Core` diff=**0**、`Resources`（资产）diff=**0**、四本账本 diff=**0**、`ITaskScheduler.cs` diff=**0**；场景/Prefab/其余四源/两巨兽/`_crewAssignments`/`currentWorkers` 均未触及。

---

## 八、源级结论：**未通过（停手待裁）** ⛔

**任务卡源判据逐项**（任务书 §八）：

| # | 判据 | 结果 |
|---|---|---|
| 1 | 真实 TaskCard > 0 且全链可追踪 | ✅ `id=1`／`id=2`；`Submit→Reservation→Pair→Assigned→（重试）→Aborted→Release` 完整 |
| 2 | 建卡走 `TaskProtocolIssuer`；`TaskBindingManager` 权威；镜像对拍 | ✅ 建卡经 Issuer；配对表建立于 `Assign`；镜像逐次对拍（窗口 `error=0`） |
| 3 | 同帧配对用快照取证 | ✅ 快照（`预定=1 配对=1` 与卡 `Assigned` 同帧读数） |
| 4 | 同 taskId 最多 1+1；释放后重派；终态封口；无重复卡 | ✅ `fails=2 ⇒ Aborted(RetryExhausted)`；`唯一=1/1` |
| 5 | **Complete／Abort／超时／读档四条路径配对预定均归零** | ⚠️ **三路取得**（Abort ✓／超时（`Unreachable` 失败路）✓／读档 ✓ 均归零）；⭐ **`Complete` 路未取得** |
| 6 | 逐卡尝试/`retryConsumedCount`/释放/终态 | ✅ `id=1`：fails=2、consumed=1、Aborted；`id=2`：同 ⇒ 等式 `尝试(2)=1+consumed(1)` 成立、≤2 |
| 7 | 读档后重新广告再派工；新卡不复用 id；资格按当前 tick | ✅ `id=2`；`blockedUntil` 102→134／170→202 |
| 8 | 无新增未归因错误；L-95 三段完整；O-14 单独归档 | ✅ 三段 attempt=1；error 4 条＝空白对照同源；O-14 已归档 |
| 9 | 策略/`hasDeadline`/不入档/`currentWorkers` 保持 | ✅ 全部保持（`hasDeadline=False deadline=Infinity` 读数在案） |

**未取得原因（当事读数）**：本局工人在搬料路上连续 `lastUnassign=Unreachable`（`fails` 达 2 ⇒ 新协议卡按政策**封口**），随后**旧链**由其他工人完成搬料 ⇒ `castle 料齐 ⇒ 开工（投入=10）` 生产事件在场，但**新协议侧 `Complete` 路未被触发**。

**⇒ 按纪律：⛔ 不自选边、⛔ 不判绿。请裁其一**：
1. **接受**"Abort／超时／读档三路 + 真实料齐生产事件"为源级通过（`Complete` 路另立补证）；
2. **要求复跑补证**：更换投料目标（避免不可达位形）以取得新协议 `Complete` 路读数（⛔ 零改码，仅换触发对象/seed）。

## 【应登记项】

```
HH.341 | 小源首源 ConstructionSiteStore 施工 | 执行端 | 改动 2 文件（本源 +213／接缝 +17）；生产面真卡 id=1/id=2、预定配对 1/1→0/0、Aborted(RetryExhausted) 封口、读档 rt 重建+重新派工、L-95 三段 attempt=1；基线 59/16→64/17（+5/+1 全在本源接缝引用）、new KingdomTask( 0 变化 | ⚠️ **Complete 路未取得** ⇒ 源级**未通过 · 停手待裁** | 不写 D 号
HH.341 | 本源接缝设计（新增接缝 E） | 执行端 | 本源源可多次派工 ⇒ 新增 `TaskScheduler.Complete` 回调（+710,5）；源侧自决"回待派(Replan·不计尝试)/终态(Done)"；失败才计 `_failAttempts` | 未改政策值 | 待登记
HH.341 | 本源实测阻塞点 | 执行端 | 工人在本局对搬料目标连续 `Unreachable`（2 次⇒封口），料齐由旧链完成；`Complete` 路未触发 | 供裁定复跑方案 | 待裁
```
