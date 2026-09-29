# HH.341 · 小源② `ChestEntity` **施工**交付报告

> 执行端 ｜ 2026-09-29 ｜ 唯一正文：`多Agent交接/策划端/HH.341_M5-D乙加_小源阶段任务书.md`（§七 九步 · §八 九条 · §十一 交付清单）
> 裁定依据：`D914`（台账 §二百二十八 · 准开施工窗口）＋预检核实（§二百二十七）＋`D911`（首源结转·一次性例外）
> ⛔ 未取新号、未写新 D 号、未 push ｜ 证据前缀 `Valley Rampart/Logs/hh341_small_chest_entity_*`（⛔ 未覆盖 CSS 首源证据）
> ⛔ 本报告**不写「判绿」**；源级结论为三选一（§十二），判绿由事务端核实后提交主策划。

---

## 一、开工回执（任务书 §十一-1 / §七-1）

| 项 | 内容 |
|---|---|
| 当前源 / 顺序 | `ChestEntity` ／ 第 **2** 源（首源 `ConstructionSiteStore` 已结项 · `D911`；本阶段一次性例外；⛔ 未换序） |
| **开工实际 HEAD** | **`fdfb6fd6`**（=`D914` 裁定落账提交） |
| 与 `D914` 原文「应为 `e96883ef`」的差异 | `e96883ef`（D911 落账）→ `91460941`（预检附）→ `c2c91b0a`（预检核实）→ **`fdfb6fd6`（D914 落账）** 共 3 个 **docs 提交**；**允许面文件零改动**（开工实测 `git diff --name-only -- Valley Rampart/Assets/_Game` ＝ **空**）⇒ ⛔ 不是不一致，可开工 |
| 独立回退点 | `git checkout -- "Valley Rampart/Assets/_Game/Systems/World/ChestEntity.cs" "Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs"`（回退到 `fdfb6fd6`）；⚠️ 回退点**不含其他源改动**（仅本源 2 文件） |
| 工作树（开工） | 既有挂账（⛔ 非本端）：`Scenes/GameScene.unity`（挂账 `O-14` · **只登记** `git hash-object`=**`4a86f26f6a7c2aab3c440903ddfa80020ddec9a3`**，⛔ 未还原/未 checkout）、`Packages/packages-lock.json`、`pixel-forge/*`×3、最高优先级文档×2、若干未跟踪文件 |
| 允许面清单（§六） | ① 本源 `ChestEntity.cs`（**已改**）② `TaskScheduler.cs`（**已改**）③ 协议 6 文件（`TaskCardProtocol`/`TaskLifecycleRules`/`TaskBindingTypes`/`TaskBindingManager`/`TaskProtocolRuntime`/`TaskProtocolIssuer`）——**逐文件核实：零改动（编译无需）** ④ 证据目录 `Logs/hh341_small_chest_entity_*`（**已用**）。⛔ `ITaskScheduler.cs` 未改（diff=0） |
| 只读预检 | 已交付（`HH.341_ChestEntity预检窗口_交付报告.md` · 提交 `475dafdb`＋闸门附 `91460941`）；判定＝**任务卡源** ⇒ 进入施工（§5.1）；⛔ 与代码现状核对一致（未发现不符） |

---

## 二、逐文件代码清单（任务书 §十一-2）

| 文件 | 性质 | file:line 区间（**改后**行号 · `git diff -U0`） | 说明 |
|---|---|---|---|
| `Systems/World/ChestEntity.cs` | 本源 · 新增协议接缝 | `+1`（using）；`+182,260`（协议块） | ① `ProtocolSlot` 卡池（**一箱多卡**——对齐旧链 `Transport` 规模派工 `D95` 允许**一箱并发多工人**；失败计次上限 2＝初次1＋重试1）② 只读诊断口×5（`ProtocolLiveCardCount`/`TotalCards`/`DoneCards`/`AbortedCards`/`LastWorkerId`）③ `Rt()`/`LogProtocolOnce`/`CreateSlot`（**经 `TaskProtocolIssuer`**；`kingdomId=-1` 无主源口径；ability=`ChestEntity_Haul`；`retryMax` 由政策写入）④ 接缝 A `OnProtocolDispatched(workerId,tick)`（本工人绑定卡→回待派卡→新建；镜像对拍）⑤ 接缝 B `OnProtocolArrived(workerId,tick)`（一箱多卡 ⇒ **带 workerId**）⑥ 接缝 C `OnProtocolAbandoned(workerId,legacyReason,tick)`（源可用⇒回待派计次；源失效/注销⇒**不回待派·封口**）⑦ 接缝 D `OnProtocolSourceInvalidated(tick)`（**在途卡保留** · 对齐调度器 `:520` 箱源例外）⑧ 接缝 E `OnProtocolTaskCompleted(workerId,tick)`（收敛 `Done`）⑨ `OnDestroy` 兜底收口（`Remove`/`ClearAll` 销毁 ⇒ ⛔ 不留预定残留）⑩ `FinalizeSlot`（`TargetRemoved` 对进行中四阶段均合法 ⇒ 免补边）/ `IsInTransit` / `FindSlotForWorker` / `FindPendingSlot` / `MirrorCheck` / `MapUnassignReason` |
| `Systems/AI/TaskScheduling/TaskScheduler.cs` | 兼容接缝 · 5 处同型条件回调 | `+279,3`（Tick① 源失效 **D″**）；`+434,3`（`Dispatch` **A″**）；`+553,3`（到达 **B″**）；`+724,3`（`Complete` **E′**）；`+772,3`（`Abandon` **C″**） | 每处仅 `if (task.source is ChestEntity …) …OnProtocol…(id/ProtocolTickNow);`（一行注释＋一行 if＋一行调用）；⛔ 未改调度语义、⛔ 未扩 `ITaskScheduler`、⛔ 未动 `taskTimeout`/`Complete` 判据/源选择规则/-1 路由 |

**合计：+276 行（TaskScheduler +15 · ChestEntity +261），0 删除。** 提交面证明见 §十（闸门）。

---

## 三、基线口径 4 项（`D914` 逐项 3 · 本轮特批）

1. **`CSS` 结项状态＝「结构性阻塞（玩家侧前置缺失：无非空配方承载者）· 非本源码缺陷」**（`D911` 终裁）——⛔ **非通过/非绿**；本报告如实转写，⛔ **未改写为绿态**（全篇无「判绿/判红」字样）。
2. **本轮基线来自当前 `HEAD`（`fdfb6fd6` 开工实测）**：`TaskScheduler.Instance|HasInstance`＝**64 行/17 文件**；`new KingdomTask(`＝15/10（剔注释 14/9）；`ITaskSource` 实现者＝**9 类**；`ITaskScheduler`＝5/2（剔注释 2/2）；全 `Assets` 对照＝160/30。
3. **施工前后逐项对拍**（同命令同域 · 见 §七）：唯一增量＝`TaskScheduler.Instance|HasInstance` **+7 行**（全在 `ChestEntity.cs`），其余项 **0 变化** ⇒ **无未解释漂移**。
4. ⛔ **`CSS` 的非绿状态未被改写**；`ChestEntity` 源级结论独立给出（§十二），⛔ 未借用首源结论。

---

## 四、生产进局报告（任务书 §十一-3 · §七-5）

**入口**：HH.92 正门 `TestHarnessApi.EnterTestRun`（slot=`hh341chest`、seed=**424242**、`WorldSize.Small`、difficulty=2）；桥=bridge `exec_runtime_script`（⛔ 探针未入 Assets）；`timeScale` 实测＝**15.0**；`WaitForSeconds(n)` ⇒ 游戏秒=n。

**箱体触发（生产 API · ⛔ 非造卡）**：`ChestManager.Instance.SpawnChest(WorldToCoord(位置), pack)`——**与生产调用方 `DamageSystem.cs:689-692` 逐字同口径**（`WorldToCoord` 地块格号 → `SpawnChest`）；移除＝`ChestManager.Remove`（生产入口）。**全部 `TaskCard` 由调度器真实派发链产生**（⛔ 无人工造卡/⛔ 无 Edit 探针/⛔ 无影子面替代）。

### 4.1 施工期内自引入缺陷与修复（如实登记 · 探针工装侧 · 共 5 轮）

| 轮 | 现象 | 根因 | 处理 |
|---|---|---|---|
| **v1** | 箱源 8 卡建立+配对，但**全部 30s 超时**、箱存量恒 `999`（零装载） | ⚠️ **探针落箱坐标口径错**：把 `WorldToSubCoord`（**微格**号）传给了按**地块格**换算的 `SpawnChest`/`WorldPosOf` ⇒ 箱落**地图外侧** ⇒ 工人走不到 | **v2 起修**：改用 `GridSystem.WorldToCoord`（与 `DamageSystem.cs:689` 生产口径逐字同）；落点校验 `CFG.distA=0.00`（箱与工人距离 0） |
| **v2** | 箱 B(999) 取得 装载(cnt −30)/MovingToDest/跨档重派；**箱 A(60) 全程饥饿**（同池竞争：B 先占 8 槽） | 探针双箱同池竞争设计缺陷（旧链『规模派工槽位 + jobs 排序（同优先级按 y→x）』的既有行为） | **v3 起改分阶段单箱** |
| **v3** | A(60) 前 3 次装载后停摆：工人在卸货段 **DestFull／MovingToDest 超时**循环（A 剩 30、`ab=35`、**A.done=0**） | **卸货段失败组合**（搬者国仓距离/落点组合）——**既有链路与地图空间约束**（⛔ 非本源缺陷）；曾登记为「AI 工人回己国仓太远」，v4 实测修正为**混合**（v3 的 A 搬运工含 k0 玩家工人 95/96/97） | **v4 改落点至 k1 王国区**绕开；v3 数据用于**跨档/镜像/重试**判据 |
| **v4** | **箱 D 取得 `done=1` + `doneLog=8`**（⑥ 成立）；箱 C（落在 `Building_Well_96_58` 水井格）未被搬 | C 的落点选择 fallback 取到**水井**（`AcceptsStone=False`）；派工/可达性波动 | 保留（C 的读数如实登记；⑥ 以 **D 箱**为证） |
| **v5** | 专测「自然搬空⇒D」：E(20) **181 秒未搬空**（零装载；ab=8） | 工人可达性/竞争波动（同格箱在 v4 可搬、v5 不可搬） | **如实登记「协议侧 D 回调未取得」**（§九-⑦） |

### 4.2 生产面关键证据链（原文摘录 · 分轮标注）

| # | 判据锚 | 原文（截取） | 来源 |
|---|---|---|---|
| ① 卡片创建 | 真实卡 >0 且 `taskId` 可追踪 | `CARD.P1.add … box#-19664 id=395 state=Assigned worker=118 …`（v4，一箱多卡逐张） | `probe4.txt` |
| ② Transport 派发 | 旧链派发日志 | `[TaskScheduler] 派发 Transport 任务 → npcId 16 @ (10.24, 163.84)`（v1 箱源 8 连发）；`DISP.P1.add DISP.npcId=96 … Transport/chest#-17896/MovingToSource`（v3） | `probe.txt`/`probe3.txt` |
| ③ MovingToSource→Working | 旧链状态机 | `DISP.P1.chg npcId=96 from=…/MovingToSource/Gold:0 to=…/Working/Gold:0`（v3 t=1636.19） | `probe3.txt` |
| ④ 箱内容装载 | 箱存量扣减 = 装载 | `DISP.P1.chg npcId=96 … to=…/MovingToDest/Stone:10`（v3，背包携 Stone:10）；`EVT.P1.A.cnt 60->50->40->30`（v3）；`EVT.P1.D.cnt 60->50`（v4） | `probe3.txt`/`probe4.txt` |
| ⑤ MovingToDest | 第二段转段 | 同 ④（`…/MovingToDest/Stone:10`，v3 三人 96/95/97） | `probe3.txt` |
| ⑥ 卸货与 Complete | 卡层 `Done` + 旧链完整 | `[P1] 停止 reason=done1 … D=…cnt_50…done_1…`（v4）；`doneLog=8`（`完成 Transport → npcId 113/121/124/125/126`，v4 t=2268-2330） | `probe4.txt`/`probe4.json` |
| ⑦ 放弃路径 | 分布（实时计数·不受日志上限影响） | v4：`Unreachable 142／Timeout 28／SourceInvalid 8／BrainLost 9`；v3 另有 `Abandon Transport → npcId 96 reason=DestFull`；⛔ `Dead` **未观测到**（观察窗内无工人死亡事件） | 各 `probe*.txt` `ABANDON_ROW` |
| ⑧ 终态归零 | 配对/预定归零 + 不变量 | P3 `zero3s`（v3/v4/v5）；全程 `SNAP.invVio=0`（`VerifyInvariants`）；`Aborted(RetryExhausted)` 封口后 `预定=配对=0` | 各 `probe*.txt` |
| ⑨ 跨档重建 | 重建+再派工（**以 v3 为准**） | `Load后 rt同一=False ts同一=True nextId后=335 观测箱=… cnt_999 live_5`；`首卡 id=330（oldMaxId=328 · nextIdBefore=329）`；`EVT.P2.B2.cnt 969->959->949`（P2 继续装载 20） | `probe3.txt` |
| ⑩ 基线无漂移 | 见 §七 | 64/17 → 71/17（+7 全在本源） | `scan_resp.txt` |

### 4.3 配对·预定·占用权威·重派·终态（任务书 §八 逐条）

- **§八-2**：建卡**统一经 `TaskProtocolIssuer`**（Create 写 `issuer`/`retryMax`；Submit 写 `basePriority` 并准入）；**占用权威＝`TaskBindingManager`**；旧 `_npcTaskMap` **仅镜像对拍**（`ProtocolMirrorConsistent` 逐次；**全程 0 条不一致**、`invVio=0`）。
- **§八-3**：同帧配对以**前后状态快照**取证——v3 `t=48.18` **同帧 8 条 `CARD chg`**（`id=6..13 Assigned→Pending`）+ 下一快照 `预定=配对` 同帧读数（⛔ 未凭帧号推断）。
- **§八-4**：**同 taskId 聚合**实证——`id=6`：建卡(Assigned)→失败回待派(`retry 1/1 consumed 1 blocked 53 lastUn Unreachable`)→**重派**(`t=55.01 Pending→Assigned worker=21`)→再失败→**封口**（`t=86.05 rem`；箱 ab 计数 +1）⇒ **尝试数＝初次1＋重试1＝2**、释放后才重派、终态封口、**无重复活动卡**（卡池唯一性：`FindSlotForWorker`/`FindPendingSlot` 单例语义）。
- **§八-5**：**Complete**（v4 D 箱 `done=1` ⇒ 释放）／**Abort**（`RetryExhausted` 封口 ⇒ 释放；P3 `zero3s` 全零）／**超时**（`Unassign` ⇒ **配对释放、预定保留于回待派卡**（设计：任务仍在）；后续封口即全零；**无悬空**）／**读档重建**（`rt` 整体重建 + P3 全零）——四路径**均无悬空绑定**。
- **§八-6**：逐卡尝试数 = **1＋`retryConsumedCount`**（实证样例 `id=13: retry=1/1 consumed=1`；v3 8 卡同帧）且**不超过 2**；`retryMax=1` 由政策写入（Issuer）。
- **§八-7**：读档后**重新广告并再派工**（v3）；**新卡不复用旧卡 id**（首卡 330 > 旧最大 328）；**资格时间按当前 tick 重算**（`blockedUntil=53 ＝ tick(48)+cooldown(5)`；跨档新卡 `blocked=0` 起始）。
- **§八-9**：既定策略/hasDeadline/卡不入档/`currentWorkers` 排除 均**未变**：`hasDL=False` 实证（`+∞` ⇒ 显式未设）；零 `Serializable`/零存档面改动；`currentWorkers` 未触碰。

---

## 五、跨档报告（任务书 §十一-4）

**以 v3 为准**（v4 未复现重派 · 见 §十一 瑕疵 6）：

| 判据 | 读数（原文） |
|---|---|
| 实例是否重建 | `Load后 rt后Hash=-933862016 rt同一=False`（runtime 整体重建）；`tsHash后=34544 ts同一=True`（**调度器实例保活**） |
| 重新广告与再派工 | 箱 B 读档重建（`inst=-18472 cnt_999 live_5 total_5 inSrc_True`）⇒ **自动**重新广告并派工（⛔ 无需人工触发；v3 `兜底触发=False`） |
| 新卡 id 不复用 | `nextIdBefore=329 → nextIdAfter=335`；`oldMaxId=328`；**读档后首卡 id=330**（新卡 330-334 共 5 张）⇒ **无复用** ✓ |
| 资格时间按当前 tick | `blockedUntil` 逐次现算（`53`＝48+5 冷却；读档后新卡起始 `0`）；`ProtocolTickNow=(long)(Time.time/tickInterval)` ⇒ ⛔ 无绝对 tick 复用 |
| 悬空绑定 / 重复卡 | `SNAP.invVio=0`（`VerifyInvariants`）；`live` 与 `onBookChestSrc` 逐快照吻合；无重复活动卡 |

**登记（观察项）**：v3 读档后观测箱 `cell=(61,65)` 与落箱时 `cell=(62,66)` 差 1 格——**未根因**（不影响判据：箱按存档 `cell` 重建、注册、派工均正常）；v2 曾现「首卡 115 vs 旧 max 119」疑似重叠——**v3 复测未复现**（v2 未记录 `ts`/`nextId` 前后，疑为读取时序错位）⇒ **以 v3 为准**。

---

## 六、`L-95` 三段原始输出（任务书 §十一-5 · §七-7）

| 段 | 读数 | 归因 |
|---|---|---|
| **段1 Play 内**（v1-v5 生产链运行期 · 读数时点=探针收尾后仍在 Play） | `types=["error"]`＝**3 条**：①`The referenced script on this Behaviour (Game Object 'ruler') is missing!` ②`No Theme Style Sheet set to PanelSettings…` ③`287 node options failed to load and were skipped.`；`filter_text=HH341CHEST`（关键行，含 5 个 wrap 行） | 三条＝**常驻既有**（⛔ 不计本源新增） |
| **段2 退 Play 后** | `Exited play mode` → `types=["error"]`＝**3 条**（同上，逐条一致） | 与段1 同源 |
| **段3 空白对照**（只进 Play · ⛔ 不建局/不落箱/不读档） | `types=["error"]`＝**3 条**（同上，逐条一致） | **常驻既有基准** |

- **生产面（更硬）**：探针内实时统计 `errs`＝**0**（v3=0／v4=0／v5=0；`error/exception` 订阅计数）⇒ **零新增未归因错误**。
- **`O-14` 噪声单独归档**：`ruler`/`Theme`/`287 node` 三条＝**常驻既有**（三者本轮三段全同）；⚠️ **本轮无 `NullReferenceException`**（与 CSS 轮 4 条基准的差异＝该噪声本轮未出现，如实登记）。
- 每段 **attempt=1**（同一工具 `read_console`，⛔ 未换工具、⛔ 未降级）。原始响应存盘：`…_l95_seg{1,2,3}_err.txt`／`…_seg1_key.txt`／`…_seg{2,3}_stop.txt`。
- **影子面＝0 条**（⛔ 未使用影子面替代生产面）。

---

## 七、基线包（任务书 §十一-6 · §七-8）

**域**＝`Valley Rampart/Assets/_Game/**`（生产域；全 `Assets` 对照另列）；**命令**＝`git grep -nE <pattern> [HEAD] -- <域>`（脚本 `Logs/hh341_small_chest_entity_scan.ps1`）；**口径**＝命中行数/文件数（**剔注释**另列：行首 `//` 剔除）；**三数**＝原始→剔注释→文件。

| 项 | 开工前（HEAD `fdfb6fd6` 实测） | 施工后（工作区） | 差异 | 逐项归因 |
|---|---|---|---|---|
| `TaskScheduler.Instance\|HasInstance` | **64 行 / 17 文件**（剔注释同） | **71 行 / 17 文件** | **+7 行 / 0 文件** | ChestEntity 新增 7 处：`:230/:231`（`Rt()`）`:252`（`CreateSlot`）`:405/:406`（`IsInTransit`）`:429/:430`（`MirrorCheck`）；该文件原已命中（`:167` 既有）⇒ 文件数不变 |
| `new KingdomTask(` | 15/10（剔注释 **14/9**） | 15/10（14/9） | **0** | 零新增任务构造（本源只接新协议） |
| `ITaskSource` | 49/13（剔注释 30/12）；**实现类 9** | 同 | **0** | 未新增实现者（ChestEntity 早已实现） |
| `ITaskScheduler` | 5/2（剔注释 **2/2**） | 同 | **0** | ⛔ 未扩接口（`ITaskScheduler.cs` diff=0） |
| 全 `Assets` 对照（仅 SchedRef） | 160/30 | 167/30 | +7/0 | 同上（7 行全在 ChestEntity；⛔ 未当作生产调用面） |

**与任务书所列基线（59/16）的差异解释**：+5 行/+1 文件 ⇒ **全部为 CSS 首源接缝引用**（CSS 施工报告 §六已登记 59/16→64/17）⇒ ⛔ 无未解释项。

**越界零命中**（`git diff --name-only`）：`ITaskScheduler.cs`＝0；协议 6 文件＝0；`ChestManager.cs`＝0；`Scenes/GameScene.unity`（仅既有挂账，未触碰）；Prefab＝0；`AI.Core`＝0；`Resources/…/*.asset` 旧键＝0；四本账本＝0；其余四小源＝0；`Building.cs`/`UnitController.cs`/`ScheduleCenterStub*`/`_crewAssignments`/`currentWorkers`＝0。

---

## 八、编译与清洁检查（任务书 §十一-7）

- **编译**：`Assembly-CSharp.dll` mtime **`2026/9/29 10:39:11`**（本批重编译，晚于源码 10:33-10:34 改动）；`read_console types=["error"]` ⇒ **0 条**（两次读数）。
- **warning 归属**：共 **25 条**，**逐文件核实：⛔ 无 `ChestEntity.cs` / `TaskScheduler.cs` 条目**；均为既有（`IUIPanel` CS0108×2／`GroundEffectManager`·`ChestManager`·`ToastManager` CS0114／`NPCBrain` CS8632／Editor 探针若干）。
- `git diff --check` ⇒ **exit 0**（仅 `core.autocrlf` 的 LF→CRLF 常规提示）。
- **行尾**：两文件 **LF**（`git diff` 警告面一致）；**BOM/NUL 无变更**（Edit 直改＋Shell 逐条核验：行数/CRLF 计数/关键串计数）。
- **提交面**：`git diff --stat`＝2 文件 **+276**（15＋261，0 删除）；⛔ 报告 commit **只含本报告 1 文件**。

---

## 九、施工验收重点 10 项逐条回执（`D914`）

| # | 项 | 成立 | 证据 |
|---|---|---|---|
| ① | 真实箱源卡片创建 | ✅ | v4：`box#-19664` 逐张 `CARD.add`（id=395-399 等）；v3：8 卡同帧建 |
| ② | `Transport` 派发 | ✅ | v1：`派发 Transport → npcId 16 @ (10.24,163.84)` 等 8 连发；v3/v4 `DISP.add` 带 `Transport/chest#…` |
| ③ | `MovingToSource → Working` | ✅ | v3：`DISP.chg npcId=96 …/MovingToSource → …/Working`（t=1636.19） |
| ④ | 箱内容装载 | ✅ | v3：`cnt 60→50→40→30`＋背包 `Stone:10`；v3 箱 B `999→969`；v4：`D` 箱 `60→50` |
| ⑤ | `MovingToDest` | ✅ | v3：三人 `…/Working → …/MovingToDest/Stone:10`（96/95/97） |
| ⑥ | 卸货与 `Complete` | ✅ | v4：箱 D `done=1`（卡层收敛 `Done`）＋ `doneLog=8`（`完成 Transport`×8）；v3 另证 `DestFull`／超时的对照路径 |
| ⑦ | 放弃路径（源失效/路径失败/超时/死亡等） | ⚠️ **部分** | 已观测：**Timeout／Unreachable／DestFull／BrainLost／SourceInvalid（5 种）**＋**源注销收口**（P3 `Remove` ⇒ `zero3s` 归零）；⛔ **未取得**：**「箱自然搬空 ⇒ 协议侧 D（Tick① 回调）」**（v3/v4/v5 三轮均未自然搬空——工人可达性/竞争波动）；⛔ **`Dead`（工人死亡）未观测到** |
| ⑧ | 新协议卡·绑定·预定·配对·终态归零 | ✅ | 全程 `invVio=0`＋镜像 0 不一致；`RetryExhausted` 封口后释放；P3 `zero3s`（三跑一致）；「超时 ⇒ 配对释放·预定保留（设计）」+ 终态全零 |
| ⑨ | 跨档重建后箱源重新广告与派工 | ✅ | v3：`rt同一=False`＋箱重建＋`首卡 330`＋`EVT.P2.B2.cnt 969→949`（重派后继续装载） |
| ⑩ | 施工前后生产基线无未解释漂移 | ✅ | §七：+7 行全在本源；其余 0 变化 |

**任务书 §八 九条判据**：1 ✅／2 ✅／3 ✅／4 ✅／5 ✅（口径见 §4.3）／6 ✅／7 ✅／8 ✅（`errs=0`+三段一致）／9 ✅。

---

## 十、落盘核验闸门 4 步回执（任务书 §十一-9 · 交付硬门槛）

| 步 | 回执 |
|---|---|
| 1 **`mtime` 前后变化** | 本报告写入前**不存在** → 写入后**存在**（`LastWriteTime` 见下）；另：两源码文件 mtime `10:33:49`（ChestEntity）/`10:34:34`（TaskScheduler）＝写入后变化实测 |
| 2 **磁盘内容重新读取** | ⛔ 未复用内存缓冲：`[IO.File]::ReadAllText` 逐文件重读（内容 hash 与关键串计数另存） |
| 3 **内容 hash / 逐字比对** | 重读内容 SHA256 ＋ 逐节对照（本文件 12 节齐、无截断）；详见 `Logs/hh341_small_chest_entity_gate.txt` |
| 4 **已提交文件再与 `git show <commit>:<path>` 对比** | commit 后执行：`git show <commit>:<path>` 与工作区**逐字节一致**（`git hash-object` 对比；见 `…_gate.txt`） |

> ⚠️ 报告 commit **只含本报告 1 文件**；`Logs/` 命中 `.gitignore`（⛔ 不入提交）。

---

## 十一、自报瑕疵（⛔ 不软化）＋【请裁】

**瑕疵（9 条）**：
1. **v1 探针落箱坐标口径错**（自引入 · 微格 vs 地块格）⇒ v1 关键链全部未取得；v2 起修正为 `WorldToCoord`（与 `DamageSystem.cs:689` 生产口径逐字同）。
2. **v2 读数疑点**：「首卡 115 vs 旧 max 119」疑似 id 重叠；v3 复测（含 `tsSame`/`nextId` 前后）未复现 ⇒ **以 v3 为准**，v2 该组读数列疑点。
3. **卸货段失败组合**（`DestFull`／`MovingToDest` 超时）在 v3 出现（既有链路×地图空间约束；⛔ 非本源缺陷）——v3 因此 `Complete=0`，v4 以 k1 区落点绕开并取得 ⑥。
4. **「箱自然搬空 ⇒ 协议侧 D」未取得**（v3/v4/v5 三轮均未自然搬空）——协议侧 D 回调**无直接读数**；**注销路径**（`Remove` ⇒ 收口归零）已取得。
5. **`Dead`（工人死亡）放弃路径未观测到**（观察窗内无死亡事件；低烈度测试环境）。
6. **v4 读档段未复现重派**（B 落在「水井格」——未被搬的格；30s 兜底 `RequestHaulNow` 亦无卡产生）⇒ **跨档判据以 v3 为准**；v4 的该读数如实登记。
7. **v1 日志行收集上限 500 截断**（v1 的 `Abandon` 分布统计不全）⇒ v2+ 改 1500/2000 并加**实时计数**；报告统计以实时计数为准。
8. **v3 读档后观测箱 `cell` 差 1 格**（(62,66)→(61,65)）——未根因（观察项）。
9. **`k0` 玩家工人接单情况在 5 轮间不一致**（v1 无、v3 有 95/96/97）——现象登记，未根因（非本源）。

**【请裁】（≤5 条）**：
1. **⑥ 的成立口径**：v4「箱 D `done=1`（卡层）+ `doneLog=8`（旧链层）」是否足够作为「卸货与 `Complete`」的验收证据？（该 `Done` 由真实装载→MovingToDest→卸货成功产生；如需逐卡 `CARD` 的完整 `Done` 链明细，可另轮补取。）
2. **⑦「源失效」的收口口径**：以「**注销路径（`Remove`/`ClearAll`）收口归零 ✓＋5 种放弃原因全观测**」替代「**自然搬空 ⇒ Tick① D（未取得）**」是否可接受？或需另设专项环境（限源/固定工位）重取。
3. **v2 疑似 id 重叠的定性**：以 v3 复测（`nextId 329→335`、首卡 330 > 328、`tsSame=True`）为准是否可接受？
4. **卸货段失败组合**（远仓/`DestFull`）是否作为**独立观察项**登记（影响箱源链路成功率·⛔ 不属本源）。
5. **`k0` 玩家工人接单不一致**（`IsIdleForTask` 口径）是否作为观察项上报（可能影响箱源在玩家区的接单率）。

---

## 十二、源级结论（任务书 §十一-8）

**结论文本：`ChestEntity` —— 通过（附 1 条未取得项：⑦ 的「箱自然搬空 ⇒ 协议侧 D 回调」；⛔ 不写判绿，判绿由事务端核实后提交主策划）。**

依据：任务卡源九条判据 **全部成立**（§4.3）；施工验收重点 10 项中 **9 项成立、⑦ 部分成立**（5 种放弃原因＋注销收口取得；自然搬空 D 与 `Dead` 未观测到）；基线**无未解释漂移**（+7 行全在本源）；`L-95` 三段一致、`errs=0`；编译 0 error、0 本源 warning；越界零命中。未取得项与口径已全量透明登记（§九/§十一），供事务端核实与主策划裁断。

## 【应登记项】

```
HH.341 | 小源② ChestEntity 施工（生产接线 · 第 2 源） | 执行端 | 改动 2 文件（本源 +261／接缝 +15）；生产面真卡全链：建卡→派发→到达→装载→MovingToDest→卸货 Done（v4 D 箱 done=1＋doneLog=8）；跨档 rt 重建+新卡 330>旧 328（无 id 复用）；P3 归零；L-95 三段 3/3/3 一致、探针 errs=0；基线 64/17→71/17（+7 全在本源） | ⚠️ 未取得：⑦ 的自然搬空⇒D（三轮尝试）；Dead 未观测 | ⛔ 未判绿 · 不写 D 号
```

## 【本批写入与提交声明】

本报告为执行端交付件；源码改动仅 `ChestEntity.cs`（+1／+182,260）与 `TaskScheduler.cs`（5×3 行）两文件，`file:line` 区间与扫描读数见 §二/§七；**未经事务端核实不得进入下一个源**；⛔ 未改本任务书、四本账本或最高优先级文档；⛔ 未 push。