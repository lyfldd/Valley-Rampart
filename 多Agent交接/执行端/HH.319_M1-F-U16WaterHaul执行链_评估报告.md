# HH.319 · `M1-F` · `U-16`「`WaterHaul` 派而不执行」· 只读评估报告 ＋ 报裁

- **日期**：2026-09-21 ｜ **端**：执行端（TraeCode）｜ **性质**：**只读评估**（⛔ `Assets/**` 零写 · ⛔ 未开工）
- **上游**：`D811`（U-15 止血批验收）⇒ `U-16` 立；现象载 `HH.319_M1-F-U15止血_交付报告.md:44-50 / :87`
- **结论速览**：⭐ **用户怀疑的"探针读数错位"（`stN` 与 `nearest` 不同源）不成立**；但另有**更致命的口径错位**（`nearest` ≠ 被派工者锚）＋ ⭐⭐ **按码推演锁定唯一自洽归因＝甲′「寻路失败 ⇒ 派发同帧被静默 `Abandon`」**；另坐实 **2 处 `F-1` 件4 引入的结构缺陷**（去重键错配／广告窗口被 `Production` 短路）⇒ **建议：`F-1` 可销号（判据 3 以 3c 硬证为准）＋ 另立 `U-16`（执行链）与 `U-16b`（广告/去重）**
- **红线自检**：`Assets/**` 零改动 ✅ ｜ ⛔ 未开工 ✅ ｜ ⛔ 未 push ✅ ｜ ⛔ 未代提交策划端四档账本 ✅ ｜ ⚠️ **探针口径未擅改**（等待 Q4 裁决）✅ ｜ 本报告自行 commit

---

## 一 · 复核：本端已取证的事实（逐条给判）

| # | 本端主张 | 复核判 | 依据 |
|---|---|---|---|
| 1 | 件5 的 `WaterHaul` 装载分支在位，与 `Transport` 同构 | ✅ 坐实 | `TaskScheduler.cs:525-542`（`LoadInventoryFromSource`⇒`MovingToDest`＋`InjectCarryStimulus`／失败⇒`Complete`）· 与 `:480-494` 逐行同构 |
| 2 | `AssignTask` 实为 `Dispatch`，两条路（刺激＋直走） | ✅ 坐实（命名勘正：方法名是 `Dispatch` 不是 `AssignTask`） | `TaskScheduler.cs:352-366`：`_npcStateMap[id]=Assigned`(`:358`) ＋ `InjectStimulus`(`:363`) ＋ `NavigateToSource`(`:364`) |
| 3 | `InjectStimulus`：`Remove` ＋ `Add`（`expiry = Time.time + 5`） | ✅ 坐实 | `TaskScheduler.cs:368-379`；`taskExpiry=5f`(`:41`) |
| 4 | `MovingToDest` 未到达续命 | ✅ 坐实 | `TaskScheduler.cs:575` |
| 5 | `task.source` = **水井**（非农场） | ✅ 坐实 | `Building.cs:1556` `new KingdomTask(KingdomTaskType.WaterHaul, well)` |

### ⭐ 一-A · 探针口径复核（用户要求"优先核"）—— **`stN` 与 `nearest` 同源，用户怀疑的那型错位不成立**

`Valley_HH319_F1LongRun.cs:142`：

```142:144:Valley Rampart/Assets/Editor/Smoke/Valley_HH319_F1LongRun.cs
                var stN = TaskScheduler.Instance != null ? TaskScheduler.Instance.GetWorkerState(nearest.npcId) : TaskState.None;
                var invN = nearest.GetComponent<WorkerInventory>();
                Debug.Log($"[HH319长局·{tag}·seq] +{rt:F1}s 最近工人 npc{nearest.npcId} st={stN} 距井={nearestD:F2} "
```

- `stN` 在 **:142 用 `nearest.npcId` 现场重取**，⛔ 不是循环里 `:112` 那个 per-`uc` 的 `st` ⇒ **无"循环中最后一个 uc"的错位**；`nearestD` 与 `nearest` 在 `:117` 配对赋值，亦同源。
- ⇒ **用户所列"口径疑点"判否**。

### ⭐⭐ 一-B · 但另有**两型真错位**（更要命）

**错位①（对象未锚定）**：`nearest`（`:109-117`）= 「**距井最近者**」，**与被派工者零绑定**。报告里"被派 npc34"是**事后从派发日志反推**的，seq 里的 npc34 只是"碰巧最近"。且 **跨帧 `nearest` 会换人**（15× 下 seq 每 1 真实秒＝15 游戏秒一行 ⇒ 换人在序列中不可见）⇒ ⛔ **"npc34 距井 5.25 恒定"严格讲不能推出"npc34 全程不动"**（除非每行 npcId 都是 34 —— 报告称是，暂采信但须留痕复核）。

**错位②（⭐ 决定性）**：`E2`（`:124`）才是"被派工者"的正确锚 —— 它**每帧**（`yield return null`）读 `_npcTaskMap`。而按码：

- `Dispatch` **同置** `_npcTaskMap[id]` 与 `_npcStateMap[id]`（`TaskScheduler.cs:357-358`）；
- `ClearNpc` **同删** 两者（`:646-647`）。

⇒ **`_npcTaskMap` 与 `_npcStateMap` 同生同灭** ⇒ ⛔ 「在 `_npcTaskMap` 里（`E2` 命中）」与「`GetWorkerState == None`」**不可能同时成立**。
⇒ 报告 §三-D 自陈"**`E2/E3` 均未捕获已发生的 `WaterHaul` 派发**"，**按码不可能**（若记录活到任一采样帧，`E2` 必命中）。
⇒ ⭐ **唯一解释：该 `WaterHaul` 记录在「派发帧内」即被清除，从未活到探针的下一帧。**

> ⚠️ 该推论依赖"E2 确实未命中"这一报告陈述的准确；若实为"命中但被 console 窗口淹没"，结论会翻转 ⇒ **必须落盘复核**（见 Q4/P3）。

### 一-C · 「距井 5.25」的旁证（强提示 · 非证明）

井位日志 `(-6.00, 40.96)`；探针 `Place(..., anchor + (-6f, 0f))`（`:60`）⇒ 反推 `anchor ≈ (0.00, 40.96)`。
`SpawnWorker(anchor + (-1f, 1.6f))`（`:62`）⇒ 出生点 `(-1.00, 42.56)` ⇒ 距井 `√(5² + 1.6²) = √27.56 = 5.2498 → **5.25**`（第二出生点 `(1.00, 39.36)` 距井 `7.18`，未出现）。
⇒ **强提示 npc34 ＝ 探针 1 号出生工人，且自出生起零位移**（"原地不动"得到旁证，并指向"站位/出发格不可走"）。

---

## 二 · 逐条结论 ＋ 依据

### 二-1 ⭐ 分性质：`st=None` 是「生产真无状态」还是「探针取错对象」？

**判：两者都不是——是「生产真无状态」，且无状态的**成因**是「记录在派发帧内被静默清除」（不是"读错人"）。**

`TaskState.None` 的语义：`GetWorkerState` 只在 `_npcStateMap` **查不到键**时返回 `None`（`TaskScheduler.cs:133-137`）⇒ 即"未派"或"已清"。已排除"未派"（有派发日志）⇒ **已清**。

**唯一能在派发帧内清除的路径（码链，逐环可验）**：

```352:366:Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
    private void Dispatch(NPCBrain brain, KingdomTask task)
    {
        ...
        _npcTaskMap[id] = task;          // :357
        _npcStateMap[id] = TaskState.Assigned;   // :358
        ...
        InjectStimulus(brain, task);     // :363
        NavigateToSource(brain, task);   // :364  ← ⭐ 在「派发」日志之前
        Debug.Log($"[TaskScheduler] 派发 {task.type} 任务 → npcId {id} @ {task.SourcePos}（优先级 {GetPriority(task.type)}）");  // :365
    }
```

`NavigateToSource`（`:386-396`）→ `PathFollower.SetDestination`（`:395`）→ `RequestPath()`（`PathFollower.cs:165-173`）→ **`PathfindingService.FindPathImmediate` ＝ 同步 A\***（`PathfindingService.cs:21-39`，`AStarSolver.Solve` 同步；`PathTypes.cs` 有 `Unreachable`）：

- 结果非 `Ready/Partial` ⇒ `SetPath`（`PathFollower.cs:78-81`）⇒ **`FailOnce()`（`:175-192`）**：
  - `_consecutiveFails < 3` ⇒ `_state = Idle`、**无路径 ⇒ 单位原地不动**，记录**仍在册**；
  - `_consecutiveFails ≥ 3`（`MovementConfig.maxConsecutiveFails = 3`）⇒ **`EventBus.Publish(new PathFailedEvent(...))`（`:183`）同步发出** ⇒ `TaskScheduler.OnPathFailed`（`:198-205`）⇒ `Abandon`（`:630-642`）⇒ **`ClearNpc`（`:644-652`）⇒ 记录在「派发」日志打印之前就没了**。

**为什么"无声"（这条是本轮最关键的取证盲区）**：

| 环节 | 是否有日志 | 依据 |
|---|---|---|
| `Dispatch` | ✅ 有（`:365`） | — |
| `Complete` | ✅ 有（`:606` `[TaskScheduler] 完成 {type} 任务 → npcId {id}`） | — |
| **`Abandon`** | ⛔ **全程无日志** | `:630-642` |
| **`NPCBrain.OnPathFailed` 兜底日志** | ⛔ **对 Worker/Porter 直接 `return`，不打** | `NPCBrain.cs:362-380`（`:369 if (occ == Worker \|\| occ == Porter) return;`） |

⇒ ⭐⭐ **「派发 10+ 次 ／ 完成 0 次 ／ 状态恒 `None` ／ 单位原地不动 ／ 无任何兜底日志」五象，只有甲′能同时解释。**

⚠️ **放大器（甲″）**：`_consecutiveFails` **不会被 `SetDestination` 重置**（`PathFollower.cs:42-52` 无重置；仅 `SetPath` 成功 `:69/:75` 与 `Complete()` `:159` 重置）⇒ 注释 `:189`「由外部 `Stop`/`SetDestination` 重置」**与码不符** ⇒ 一旦累计 3 次失败，**此后每次重派都在派发帧内被 `Abandon`（永久空转）**。
⚠️ **无自愈**：`NavigateToSource` **只在 `Dispatch` 调一次**；`UpdateAssignedTasks` 的 `MovingToSource` 未到达分支只做 `InjectStimulus`（`:456`）——**只续命刺激，不重新寻路** ⇒ 首次寻路失败 ⇒ 该任务**必然**耗到 `taskTimeout=30f`（`:45`）才 `Abandon`，或被 `PathFailedEvent` 提前清。

> ⛔ **逐帧时序（`Assigned → ?`）本轮无法产出**：① `Logs/` 无 `hh319_u16` 留档（全库 grep `派发 WaterHaul`／`HH319长局` 仅命中探针源码本身）② 编辑器曾被用户关闭 ③ 本轮 `read_console` 两次失败（`Unity did not respond … 2.0s` / `session not ready`）⇒ **只能给出"按码推演的应有形态"**：`Assigned`（派发同帧·随即被清）→ `None`（永久）→ 下一 tick 重派 → … ⇒ 补证见 Q4。

### 二-2 ⭐ 生产日志全链（按码推演的应有形态 ＋ 判别式）

| 应有日志 | 甲′ 下的形态 | 判别力 |
|---|---|---|
| `派发 WaterHaul → npcId 34` | ✅ 反复出现（10+） | 已知 |
| `完成 WaterHaul 任务 → npcId …`（`:606`） | ⛔ **应为 0 条** | ⭐⭐ **决定性**：若有 ≥1 条 ⇒ 甲′ 被推翻（说明工人走到了井） |
| `[NPCBrain] PathFailed 兜底`（`:378`） | ⛔ 必为 0（工人被 `:369` 短路） | 佐证"无声" |
| `[EventBus] 事件 PathFailedEvent 无订阅者` | ⛔ 不应出现（`TaskScheduler`/`NPCBrain` 均已订阅 `:83`/`:313`） | — |
| `[调度中心] 派发搬运任务 @ (-6.00,40.96)` | ⛔ 应为 0（U-15 止血后 · 已验证） | 排除第三链 |

⇒ **重跑时的单一判别式：grep `完成 WaterHaul` 的条数。** 0 ⇒ 甲′ 坐实；>0 ⇒ 归因改走乙/戊。

### 二-3 候选归因逐条验

| 候选 | 判 | 依据／理由 |
|---|---|---|
| **甲 `NavigateToSource` 失败** | ⭐ **成立（首要）** | 见二-1 码链。细分：甲′＝同步 `FailOnce`⇒`PathFailedEvent`⇒同帧 `Abandon`；甲″＝失败后无重寻/无自愈。⚠️ 需补证"为何不可达"：疑似**出发格不可走**（`FindPathImmediate` 注释 `PathfindingService.cs:30`「**起点不 snap**」——起点必须＝单位实际位置），配合一-C「npc34＝出生点零位移」 |
| **乙 刺激冲突（C 档被抢）** | ❌ **排除为主因** | ① 用户自陈"那应表现为在动"，与"不动"不符 ✔ 本端认同；② **刺激冲突不会清除 `_npcStateMap` 条目**（`ClearNpc` 只由 `Complete`/`Abandon` 调）⇒ 与 `st=None` **结构性不符**。但乙仍是**次生风险**：`InjectStimulus` 的 C 档刺激若被 B 档压住，即使 PathFollower 有路径也会被 Executor 拉走 ⇒ 修完甲后须复测 |
| **丙 任务在同 tick 内作废** | ⚠️ **机理勘正 · 部分成立** | ⛔ **"农场不再缺水 ⇒ 任务 `IsValid` 假 ⇒ 被 Tick 清理"不成立**：`UpdateAssignedTasks:423` 只查 **`task.source`** 的 `IsValid`，而 `task.source`＝**水井**（`Building.cs:1556`），与农场水位**无关**；农场水位只影响"是否**再广告**"。<br>✅ 但丙的**广告侧**版本成立：`井仓 ≥80%` ⇒ 井自广告 `Transport`（B，`Building.cs:1524-1538`）直达 farm ⇒ 农场水达 `waterThreshold=20` ⇒ `:1551` 不再广告 ⇒ **`WaterHaul` 被"双链竞争"饿死**。⇒ 丙改判为 **丙′：广告侧抑制（非任务作废）** |
| **丁 工人不可用** | ❌ **排除** | `Tick:252` 要求 `n.IsAlive`；`:258-260` 限 `Worker/Civilian/Porter`；`:261` 排除已在册 ⇒ 被派者必是"活着、职业合规、当前无任务"的工人 |
| **戊 `args` 非 `HaulWaterArgs` ⇒ 落 `else⇒Complete`** | ❌ **排除** | `Building.cs:1559-1562` 明确 `task.args = new HaulWaterArgs { target = storage }`；且走 `else⇒Complete`（`:543-547`）会打 `[TaskScheduler] 完成 WaterHaul`（`:606`）⇒ 与"完成 0 条"不符 |

### 二-4 两档（1× / 15×）是否同因 —— ⚠️ **发现一处与"每 tick 重派"预测不符，须报裁**

- 15× 下 `Time.deltaTime` 钳到 1.0/帧 ≥ `tickInterval=1f` ⇒ **`Tick` 每帧跑**（`:111-114`）⇒ 每帧一次派发机会；1× 下 dt≈0.16 ⇒ 约 **6 帧一次**。
- ⇒ 若只由"每 tick 重派"驱动，应有 **15× ≫ 1×**。但报告称**两档皆 "10+"（同量级）** ⇒ ⚠️ **与预测不符** ⇒ 说明真正的节流阀不是 tick，而是 **D-2 的广告窗口**（下条）。
- **判：两档同因（甲′ ＋ D-2 窗口）**，倍率只影响"窗口内的派发密度"，不影响"是否派"。⇒ ⛔ 本端**不能**给出两档的逐帧 state 时序对照（无留档），只能给"机制一致性"结论，**须复跑证实**。

### ⭐ 二-5 新坐实：`F-1` 件4 引入的 2 处结构缺陷（根因＝「广告源＝农场 ≠ 任务源＝水井」）

**D-1「去重键错配 ⇒ `WaterHaul` 每 tick 无条件重派」**

```266:279:Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
        //    农场可派 Production（耕作）＋搬水任务（⭐ `M1-F` 件4 起源＝**水井** ⇒ 去重键＝「水井+WaterHaul」
        //    ⚠️ 同 tick 内同一水井只服务一个农场（串行）· 完成后再广告下一个）
        ...
            else if (HasAssignedTaskForSourceType(s, task.type)) continue;   // :279  s ＝ 广告源 ＝ **农场**
```
`HasAssignedTaskForSourceType`（`:1130-1135`）比的是 `kv.Value.source` ＝ **水井**，而 `s` ＝ **农场** ⇒ **键恒不匹配 ⇒ 去重永久失效** ⇒ 注释 `:266-268` 自称的"同一水井串行"**与码不符**。⇒ 这是"10+/12+ 次派发"的**直接放大机理**（无去重 ⇒ 每次广告窗口都派新的）。

**D-2「广告窗口被 `Production` 短路 ⇒ `WaterHaul` 结构性饥饿」**

`Building.TryAdvertiseTask` 是**单选返回**：
- ② `Production`（`Building.cs:1512-1520`）：`producer != null && !IsWell && !storage.IsFullFor(Food) && !sched.HasWorkerAssigned(this)` ⇒ **`return true`**；
- ④ `WaterHaul`（`:1548-1566`）在 ②③ 之后 ⇒ **只有 ② 不成立时才走得到**。

而 `HasWorkerAssigned(farm)`（`:139-150`）要求「`task.source == 农场` ∧ `state == Working`」——**`WaterHaul` 的 `task.source` 是水井 ⇒ 永不为农场成真**。
⇒ ⭐ **农场越缺水，越只广告 `Production`（B 档）；`WaterHaul`（C）只在「农场已有工人处于 `Working`（`workDuration=2f` 游戏秒窗口）」或「粮已满」的窄窗内才广告** —— 与"农场缺水就该挑水"的语义**正好相反**。

### 二-6 是否阻塞 `F-1` 销号 —— **判：不应阻塞；但须另立两项，⛔ 不可留白**

| 项 | 判 | 理由 |
|---|---|---|
| `F-1` 销号 | ✅ **建议放行** | 甲′ 的缺陷在**寻路/站位层**，⛔ 不在搬水链逻辑；件5 装载/卸货段在位（`TaskScheduler.cs:525-542`、`:562-563`）；**判据 3c 单元级硬证已过**（井仓 100→90 · 装载 10 · 卸货背包 0 · 农场仓 0→10，见 `HH.319_M1-F-F1水仓化与真搬运_交付报告.md`）⇒ 判据 3 建议**以 3c 为准**，字面"3b 走调度器端到端"改为**环境项** |
| 必改的 `F-1` 件 | ⚠️ **件4**（`Building.cs:1556` 把 `source` 改水井）**连带引入 D-1／D-2** ⇒ 若裁决"F-1 销号则件4 收口"，D-1/D-2 **必须同时收**；否则另立 `U-16b` |
| `U-16` 定性 | ⭐ 建议**另立为「寻路失败静默放弃」类缺陷**（影响面 ⛔ 不限于 `WaterHaul`：**任何任务**派给"出发格不可走/寻路失败"的工人都会静默空转）⇒ 严重度**高于** `WaterHaul` 本身 |

### 二-7 可用／不可用读数清单（用户要求明确）

**可用**：派发日志（`TaskScheduler.cs:365`）· 农场仓水序列（0→10→20→14）· 背包=10 · `E4→E6=1.42s` · 判据 1/2/4/5/8 · 判据 3c 单元级硬证 · `HH316` §A~§H。
**不可用**：① seq 的 `st`/`距井`（对象未锚定 · 一-B①）② 该探针的**水账**（"井仓压制"调 `TakeOut`，`:99`）③ **`E2/E3` 的"未捕获"本身是强证据而非"不可靠"**（一-B②，机理上不可能）④ 任何"完成/放弃计数"（**console 无留档**）⑤ `HH315` §B（工人 11→3 衰减 · 环境扰动）。

---

## 三 · 报裁（Q1~Q4）

### Q1 归因落点（已排序）

1. ⭐ **甲′ 寻路失败 ⇒ `PathFailedEvent` 同步 ⇒ 派发同帧静默 `Abandon`**（唯一能同时解释五象）
2. ⭐ **甲″ 失败后无自愈**：`NavigateToSource` 只在 `Dispatch` 调一次；`MovingToSource` 只续命不重寻（`:456`）；`_consecutiveFails` 不被 `SetDestination` 重置（`PathFollower.cs:42-52` vs 注释 `:189`）
3. ⭐ **D-1 去重键错配 ⇒ 每 tick 重派**（放大"10+ 次"）
4. ⭐ **D-2 广告窗口被 `Production` 短路 ⇒ `WaterHaul` 结构性饥饿**（决定"何时派"· 解释两档同量级）
5. **丙′ 广告侧双链竞争**（`Transport` 先达 ⇒ 农场不再广告）
6. ~~乙／丁／戊~~ —— 排除（见二-3）

### Q2 若为"双链竞争"（丙′）⇒ 处置方向 —— **建议：不要只在"提优先级"上打转**

理由：丙′ 只**抑制新广告**，⛔ 不会作废在派任务（二-3），故**提优先级治不了本轮主因**。

| 案 | 内容 | 代价 | 本端建议 |
|---|---|---|---|
| **A（治本）** | 修 **D-1**：`Tick:279` 的去重键改按 `task.source`（水井）判定，或为"广告源＋类型"单独立一张在册表 | 1~3 行 · 限于 `TaskScheduler`｜⚠️ 会改变"同井串行"为"同井串行（真正生效）" ⇒ 派发次数骤降，须同步复核判据 3 | ⭐ **首选** |
| **B（治标·对应 `D808` 判据 9）** | 井仓侧**不给 Water 打 `Transport`**：`Building.TryAdvertiseTask` ③（`:1524-1538`）对 `PrimaryStoredType()==Water` 跳过，或井的 `transportThreshold` 单独抬高 | 1~2 行｜⚠️ 缩小 `Transport` 职责面，须评估"井水外运"是否另有诉求 | ⭐ **次选（建议与 A 同批）** |
| **C** | `WaterHaul` 由 C 提到 B | 0~1 行｜⛔ **不建议单用**：会与 `Production`(B) 平级抢人，且本轮主因是寻路 ⇒ 提档只增加派发次数、不增加成功率 | ⛔ |
| **D（另案·待裁）** | `Abandon` 加一条带原因的日志（生产 1 行） | 日志量↑ | ⭐ **强烈建议**：本轮"派而不执行"**长期无法定位**的根因就是 `Abandon` 全程无声 ＋ `NPCBrain.OnPathFailed` 对工人静默（`:369`） |

### Q3 `U-14` 与 `U-16` 的关系 —— **判：根因不同，⛔ 不合并；判据面合并**

- `U-14` 定性＝「`WaterHaul` 被更高优先刺激**拉偏**」（抢占 · 表现为**在动**）；`U-16` ＝「**派而不执行**」（寻路失败静默放弃 · 表现为**不动＋`st=None`**）⇒ **机理互斥**（二-3 乙）。
- 但两者**共用同一个判据面**（"`WaterHaul` 是否完成往返"）⇒ 建议：**`U-14` 保留编号、降级为"待 `U-16` 修完再复测"；`U-16` 立为阻塞项**；`U-14` 的"刺激竞争"面与 **D-2** 同源（优先级/广告次序），可在 `U-16b` 一并处置。

### Q4 是否需先修 `F1LongRun` 探针再复跑 —— ⭐ **是，且必须先修；但修的不是用户怀疑的那处**

⚠️ **用户怀疑的 `stN` 错位不成立**（一-A）⇒ 若只改那处，⛔ 修不到点上。真须修的是三处，**全在 Editor 探针内，`Assets/_Game` 零改动**：

| 编号 | 修法 | 目的 |
|---|---|---|
| **P1** | seq 改为**按「被派 npcId」锚定逐帧打点**（订阅/轮询时记录 `Dispatch` 的 npcId，或每帧扫 `_npcTaskMap` 全量打印），⛔ 不用"距井最近者" | 消灭错位①（对象未锚定） |
| **P2** | 反射**只读**读 `PathFollower._state`／`_consecutiveFails`／`_destination`（`PathFollower.cs:19-23`）＋ `GridSystem.IsSubWalkable(单位所在微格)` | 直接证甲′（不可达 vs 被抢占） |
| **P3** | 每帧/每 tick 计数 `PathFailedEvent`（Editor 侧 `EventBus.Subscribe` 只读订阅）＋ **把 console 全量落盘到 `Logs/hh319_u16/`** | ⭐ 消灭本轮最大取证缺口：**console 无留档** ⇒ 二-1/二-2/二-4 无法复核 |

**顺序建议**：**先修探针（P1~P3）＋ 复跑取证 ⇒ 再定生产改法**。⛔ 若裁决"先改生产"，风险是修在不是根因的地方（甲′ vs 丙′ 尚未由落盘日志钉死）。

---

## 四 · 未完成项与风险

1. ⛔ **二-1 的逐帧时序、二-2 的生产日志全链、二-4 的两档对照 —— 本轮均无法产出**（无留档 ＋ 编辑器不可用）⇒ 结论为**按码推演**，强度标注为"唯一自洽解"而非"已证"。
2. ⚠️ 二-1 的关键推论依赖"`E2` 确实未命中"这一报告陈述；若复核发现 `E2` 曾命中 ⇒ 归因须改走乙/戊 ⇒ **P3 落盘是第一优先**。
3. ⚠️ 甲′ 若坐实 ⇒ **影响面远超 `WaterHaul`**（任何任务派给"寻路失败"工人都静默空转）⇒ 建议单独立项评估严重度。
4. ⚠️ `HH315`／`HH317`／`M7` 回归仍未跑（承 `U-15`）；本轮未新增回归。
