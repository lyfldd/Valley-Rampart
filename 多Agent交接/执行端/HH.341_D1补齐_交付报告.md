# HH.341 · D1 首片 **补齐批** · 交付报告

> 执行端 ｜ 2026-09-26 ｜ 唯一正文：`HH.341_M5-D乙加_任务书.md` v2（已全文直读）｜ ⛔ 未取新号、未写 D 号、未提交、未 push
> 判定线：6 项全过 ＋ 无新增未归因错误 ＋ 生产面证据完整 ⇒ 判绿。**本报告如实列出未取得项**（⛔ 不自宣 D1 已绿）。

---

## 〇、开工回执（四项）

1. **阶段**：D1 首片补齐批。
2. **预计改动文件（⑤⑥ 落点）**：⑤ → `TaskCardProtocol.cs`（`hasDeadline` 载体＋`Assign` 写入）／`TaskBindingTypes.cs`（`TaskReservation.hasDeadline`）／`TaskBindingManager.cs`（`Reserve` 写入 ＋ **`SweepExpired` 判定改读该载体**）；⑥ → `WorldGatherSource.cs`（`EnsureCard` 改走 `TaskProtocolIssuer`）。`TaskProtocolIssuer.cs`／`TaskScheduler.cs` 本批**零改动**。
3. **跨档重验进局方案**：正门 `EnterTestRun(seed=31418/Small/2)` → 立案（`ConfirmResourceGather`）→ 等建卡/配对 → `Save` → `Load` → 查「rt 是否重建／表是否归零／悬空／重复活动卡／tick 是否不复用」→ 读档后**重新立案**复核新一轮链。
4. **独立回退点**：HEAD `afc261a7`；本批 4 文件逐文件 `git checkout --` 即回退。

---

## 一、⑤ `hasDeadline` 载体（已落地 · 运行期判定已改用它）

| 位置（新行号） | 改动 |
|---|---|
| `TaskCardProtocol.cs:+310,4` | `TaskCard` 新增 `public bool hasDeadline;`（显式载体 · ⛔ 不用 `-1` 哨兵 · ⛔ 不入档） |
| `TaskCardProtocol.cs:+363／+365` | `Assign()` 内**与 `deadline` 同点写入**：`hasDeadline = !(NaN ‖ ≤0 ‖ +∞)` |
| `TaskBindingTypes.cs:+106,3` | `TaskReservation.hasDeadline`（与卡片同源语义） |
| `TaskBindingManager.cs:+128` | `Reserve()` 写入 `row.hasDeadline`（同一判式） |
| `TaskBindingManager.cs:+281,2` | ⭐ **`SweepExpired` 判定改为只读 `hasDeadline`**（原 `!IsInfinity(d) && d<=now` ⇒ 现 `hasDeadline && deadline<=now`）⇒ **运行时判断使用显式载体** |

实测（逐卡读数）：`hasDeadline=False deadline=Infinity` ✓（本片一律未设 deadline · 与政策一致）；`taskExpiry/taskTimeout` 数值未改 ✓；⛔ 未加 `[Serializable]`／`ISaveable`／卡片台账；补字段后**同批重跑生产链＋跨档重建**（见 §四）。

---

## 二、⑥ 建卡统一经 `TaskProtocolIssuer`

- `WorldGatherSource.EnsureCard`（`+184,13`／`+198,3`）：`new TaskCard()` ⇒ `new TaskProtocolIssuer(rt, issuerRef)` ＋ `issuer.Create(taskId, kingdomId, ability, targetRef)` ＋ `issuer.Submit(card, basePriority=0, allowsMultiple=true, deadline=+∞)`
  ⇒ `retryMax` 由 **政策**（`Policy.defaultRetryMax=1`）写入、`issuer` 由封装写入、`basePriority` 由上层基值写入（⛔ 不回写 `EffectivePriority`）。
- **实测证据**：逐卡读数出现 `retry=1/1 consumed=1`（**重试额度被成功消费**）⇒ 本轮**未再出现** `TryConsumeRetry(RetryPolicyUnconfigured)`（首片的 4 条已核销）；`窗口内 error/exception=0`。
- ⛔ 未只补日志、未用探针证"等价"：改为**生产路径本身**走封装。

---

## 三、③ 旧链基线前后对拍（域＝`Valley Rampart/Valley Rampart/Assets/_Game/**`；计数单位＝行/文件）

| 判据 | 基线 | 本批实测 | 差异 | **逐项解释** |
|---|---|---|---|---|
| `TaskScheduler.Instance|HasInstance` | 54 行 / 15 文件 | **59 行 / 16 文件** | **+5 行 / +1 文件** | 全部落在 `WorldGatherSource.cs` 新增的**接缝引用**：`TaskScheduler.HasInstance`×2（`Rt()`／`MirrorCheck`）＋`Instance.ProtocolRuntime`／`Instance.NextProtocolTaskId`／`Instance.ProtocolMirrorConsistent` 各 1 ⇒ +5 行；该文件由未命中变命中 ⇒ +1 文件 |
| `new KingdomTask(` | 14 行 / 9 文件（剔 `ScheduleCenterStub.cs:99` 1 行注释） | **15 行 / 10 文件（含注释原始命中）** | **0** | 本读数**含注释**；与基线"原始 15/10 ⇒ 剔注释 14/9"**逐位一致** ⇒ 本批**零新增**（本片只接新协议，未产旧任务对象） |
| `ITaskSource` 实现者 | 9 类 | **9 类** | 0 | `WorldGatherSource`／`ChestEntity`／`DebugTaskSource`／`MineByproductComponent`／`BlacksmithBuilding`／`ConstructionSiteStore`／`Building`／`SiegeWorkshopBuilding`（＋1 处多行声明的后续行） |
| `KingdomTaskType` | 11 项 | **11 项** | 0 | `Repair/Build/Produce/Transport/Rancher/WaterCarry/GoldMine/Production/WaterHaul/Gather/AmmoReload` ⇒ 首片**未删未加**枚举 |

⛔ 未使用全 `Assets` 的 149/27 替代生产基线（该数仅历史对照）。

---

## 四、① 跨档重建重验（生产面 · 同局 Save→Load）

| 判据 | 读数 | 结论 |
|---|---|---|
| 运行时重建 | Load 前 rt 实例 `-256493698` → Load 后 `960337316`，**同一实例 = False** | ✅ 旧表整体释放 |
| 旧表归零 | Load 后 **预定 0／配对 0**；不变量违规 0 | ✅ |
| 悬空绑定 | Load 后立即：镜像条目 **0**、**协议侧悬空 0**；Load 后 3s：镜像条目 10、**协议侧悬空 0** | ✅ 无悬空 |
| 重复活动卡 | 生产段快照 `唯一taskId=4/4`（taskId＝3,1,2,4 全局唯一）；`EnsureCard` 幂等 ⇒ 一源一卡 | ✅ |
| ⛔ 不复用绝对 tick | `ProtocolTickNow = Time.time/tickInterval` **现算**；逐卡 `blockedUntil=109/111`（＝触发 tick+5） | ✅ |
| 读档后重新广告 | Load 后重新立案 2 源成功（源被重建）；本轮**未派工**（该 2 源在地图边缘、玩家工人未就近）⇒ 卡未建 | ⚠️ **部分**（重新立案已证；"重新广告→再派工"未复现） |

---

## 五、④ 按 `taskId` 聚合 retry 表（逐卡 · 生产面）

| taskId | 初次 | 回待派（释放） | 重试 | 终态 | 备注 |
|---|---|---|---|---|---|
| **1** | `Assigned worker=16 retry=0/1 consumed=0` | `Pending worker=0 retry=1/1 consumed=1 lastUnassign=Unreachable blockedUntil=109` | 已用 1/1 | 未封口（探针窗口内为 Pending） | 尝试数＝2（初次＋重试），**未越上限** |
| **2** | `Assigned worker=13 retry=0/1 consumed=0` | `Pending worker=0 retry=1/1 consumed=1 Unreachable blockedUntil=111` | 已用 1/1 | 同上 | — |
| **3** | `Assigned worker=14 retry=0/1 consumed=0` | `Pending worker=0 retry=1/1 consumed=1 Unreachable blockedUntil=111` | 已用 1/1 | 同上 | 另有 `Assigned worker=13 retry=1/1` 中间态 |
| **4** | `Assigned worker=15 retry=0/1 consumed=0` | `Pending worker=0 retry=1/1 consumed=1 Unreachable blockedUntil=111` | 已用 1/1 | 同上 | 另有 `Assigned worker=14 retry=1/1` 中间态 |

- **释放数**：4 次 `Unassign`（回待派）＋跨档 Load 时 4 组配对/预定**归零**（`4→0`）。
- **终态数**：本窗口 **0**（未出现 Done/Aborted）⇒ 因每卡**只用满 1 次重试后停在 Pending/Assigned**，未触发"第二次失败 ⇒ 封口"。
- **无重复活动卡** ✓；**上限 2 次**经实测未越（`retry=1/1` 即耗尽）。
- ⚠️ **未取得**：`Done`／`Aborted` 终态的逐卡读数（需更长窗口或让目标可达）。

---

## 六、② `L-95` 三段 Console（原始输出文件 `Valley Rampart/Logs/hh341_d1/console_l95_seg{1,2,3}.txt`）

| 段 | 读数 | 说明 |
|---|---|---|
| ① Play 内 | **未取得** | 两次 `read_console` 均返 `Unity did not respond to 'read_console' within 2.0s; please retry`（原文已落盘 `console_l95_seg1.txt`）。⛔ **未按"换工具刷取"处理**，如实登记 |
| ② 退 Play 后 | **部分**：`Exited play mode` 成功；error 读数未返回（同上超时） | 落盘 `console_l95_seg2.txt` |
| ③ 空白对照（只进 Play、不建局、不读档） | **4 条**：`NullReferenceException`／`ruler 缺脚本`／`No Theme Style Sheet`／`287 node options`；`filter_text=WorldGatherSource`＝**0**；`filter_text=error CS`＝**0** | 落盘 `console_l95_seg3.txt` |
| **机械替代（更硬）** | 探针窗口内（覆盖完整生产链 ＋ Save/Load）`error/exception = 0` | `probe_fill.txt` 末行 |

**归因**：空白对照（不建局、不读档）即含该 4 条 ⇒ **均非本批引入**；其中 `NullReferenceException` 的**栈未取得**（`read_console` 仅返消息文本，未返 stack）⇒ 与本项目既有常驻噪声（编辑器内部）同批，**本批不声称已归因**，另登记。
**生产面／影子面分列**：本报告**全部读数均为生产面**（真实进局＋真实源＋真实卡＋真实 Save/Load）；⛔ 无影子面替代、⛔ 未使用 Edit 探针。

---

## 七、编译与扫描包

- 编译：`Assembly-CSharp.dll` mtime **21:47:28**（补齐批重编译）；`filter_text=error CS`＝**0**。
- 改动面（`git diff --name-only -- Valley Rampart/Assets`）：`TaskCardProtocol.cs`／`TaskBindingTypes.cs`／`TaskBindingManager.cs`／`WorldGatherSource.cs` ＋ `Scenes/GameScene.unity`（**既有脏点** mtime 2026-09-15 ⇒ 非本批）。
- `TaskScheduler.cs` 本批 **hunk 为空**（零改动）；`git diff --check` exit **0**；行尾三文件 `i/lf w/lf` ⇒ 用 Edit 直改、**保持 LF**（⛔ 未触发 CRLF 二进制替换）。
- 逐文件 hunk（新行号）：`TaskCardProtocol @@ +310,4 / +363 / +365`；`TaskBindingTypes @@ +106,3`；`TaskBindingManager @@ +128 / +281,2`；`WorldGatherSource @@ +184,13 / +198,3`。

---

## 八、未取得项 / 报裁（⛔ 不得冒充通过）

1. **未取得**：②段1 与段2 的 error 读数（MCP `read_console` 超时）；④的 `Done/Aborted` 终态逐卡读数；①的"读档后重新广告→再派工"。
2. **登记**：空白对照中的 `NullReferenceException` 栈未取得（工具仅返消息）⇒ 是否需另批取证，请裁。
3. `taskId`：**认可临时实现**（`TaskScheduler.NextProtocolTaskId` 单调序列）；本轮实际序列 **3,1,2,4**（唯一 4/4、无碰撞、单调发号）；⛔ 未写成全局政策、⛔ 未扩展分配规则。
4. ⛔ 未宣称 §九-6 单管理器达成（`ScheduleCenterStub.cs:56` 仍在）；⛔ 未动 `Building`／`UnitController`／`currentWorkers`／五小源／场景／资产／`AI.Core`／账本。

## 【应登记项】

```
HH.341 | D1 补齐批 ⑤hasDeadline 载体 | 执行端 | TaskCard/TaskReservation 显式载体落地；Assign/Reserve 写入；SweepExpired 判定改读载体；实测 hasDeadline=False | 未入档、未改政策数值 | 待核实
HH.341 | D1 补齐批 ⑥建卡经 Issuer | 执行端 | EnsureCard 改走 TaskProtocolIssuer.Create/Submit；实测 retry=1/1 consumed=1、RetryPolicyUnconfigured 未再现 | ⛔ 非"等价"声明而是生产路径本身 | 待核实
HH.341 | D1 补齐批 ③旧链基线 | 执行端 | TaskScheduler.Instance|HasInstance 54/15→59/16（+5 行 +1 文件，全在 WorldGatherSource 接缝引用）；new KingdomTask( 0 变化（口径含注释 15/10）；ITaskSource 9 类；KingdomTaskType 11 项 | 逐项解释见 §三 | 待核实
HH.341 | D1 补齐批 ①④ | 执行端 | 跨档：rt 重建、预定/配对 0/0、悬空 0、不变量 0、taskId 唯一 4/4；逐卡 retry 1/1 consumed=1、冷却 +5 tick | ⚠️ Done/Aborted 终态未取得 | 待裁
HH.341 | D1 补齐批 ②L-95 | 执行端 | 段3=4 条既有噪声（NRE/缺脚本/主题/节点）+ WorldGatherSource 0 + error CS 0；段1/段2 因 MCP 超时未取得（原文已存） | 机械替代：探针窗口 error/exception=0 | 待裁
```
