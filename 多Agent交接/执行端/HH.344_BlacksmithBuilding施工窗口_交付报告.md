# HH.344 · 第 4 源 `BlacksmithBuilding` 最小任务协议接缝 · 施工窗口交付报告

- 签发依据：`D922` 裁决（准开施工 · ⛔ 非判绿）＋《M5-D 五小源阶段任务书》§七 施工顺序 ＋ HH.344 工单
- 角色：**执行端**（实施工 · 不自评通过 · 判绿权在主策划）
- 日期：2026-09-30
- 源级结论（口径上限）：**施工完成 · 待事务端核实**（⛔ 不宣称判绿、⛔ 不进入第 5 源 `SiegeWorkshopBuilding`）

---

## 1. 回退点与开工基线（独立登记）

| 项 | 读数 | 来源 |
|---|---|---|
| 开工 HEAD | `1a9e9797269f0428e9316dd1429da366e5f76b23`（main） | `hh341_small_blacksmith_cx_gate_pre.txt` `BASE step=rev-parse` |
| 开工工作区 | total=241 modified=5 untracked=236 | 同上 `STATUS` |
| 两允许面文件起点 | `BlacksmithBuilding.cs` / `TaskScheduler.cs` 均 `diff_files=0 blob_identical=TRUE` | 同上 `ALLOWED` |
| O-14（`GameScene.unity`） | 开工即挂账（M 态），本轮 ⛔ 未回滚 ⛔ 未问回滚，仅登记 | `O14` 行 |
| 挂账池 | `L-99` 口径：本轮未做任何 `checkout --`/整文件 `restore` | — |

---

## 2. 施工内容（只有两处，全在允许面）

| 文件 | 增/删 | 施工前 blob | 施工后 blob | 现盘行数 |
|---|---|---|---|---|
| `Valley Rampart/Assets/_Game/Systems/Kingdom/BlacksmithBuilding.cs` | **+257 / −0** | `0fc4227bd314c07136c94a43254d912dc8578abb` | `86fc5033b86045431b3eb4e7245530c2cd374ddc` | 366 |
| `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs` | **+18 / −0** | `3e8efea34c8443a5dc383f969ef835a6dfc60fac` | `93ae7bbde3e624540d2fbc333601dc14555fbeaf` | 1678 |

接缝形态＝`D/A/B/C` 四类回调 ＋ `E` 单趟完成接缝（`D922` ①），五类判定点各增一列 `BlacksmithBuilding` 支，⛔ 不改调度语义/源选择/去重键/排序路由：

| 类 | 语义 | `TaskScheduler.cs` 增支行 | 同型先例（既有） |
|---|---|---|---|
| D | 源失效清扫前 | **:288** | `:275` wg / `:278` site / `:281` chest / `:284` mb |
| A | 派发成功 | **:449** | `:437/440/443/446` |
| B | 到达 | **:575** | `:562/565/568/571` |
| E | 单趟完成 | **:753** | `:743/746/749` |
| C | 放弃（`ClearNpc` 后） | **:807** | `:795/798/801/804` |

`E` 类先例为 4 支（`:740` 为 Mine 侧的 `WorldGather` 空位口径），本源补第 5 支 ⇒ 闸门读数 `SEAMCX E hitline=4` 是**判定支数**口径（不含 `:740` 之外的注释行），与 `pre` 阶段 `E3` 的差值＝本源新增 1 支，逐项可解释。

本源侧实现要点（承 `D922` ②③）：
- `ProtocolSlot{card, failAttempts}` ＋ 只读诊断口 `ProtocolLiveCardCount/TotalCards/DoneCards/AbortedCards/LastWorkerId`；
- 建卡统一经 `TaskProtocolIssuer.Create/Submit`（`issuer = TaskIssuerRef{Kingdom, kingdomId}`，`ability="BlacksmithBuilding_Task"`，`targetRef{Building, coord.x, coord.y, 0}`，`deadline=+∞` ⇒ 显式载体 `hasDeadline=false`）；卡 ID 取自 `TaskScheduler.NextProtocolTaskId()`；
- `Done` **只封口一张卡、不封源**（循环型常驻源）；仅建筑/组件失效或销毁时 D/`OnDestroy` 把剩余非终态卡封 `TargetRemoved`；
- 「暂时无矿／满仓／本轮无产出」**不**走 D（源继续广告）；
- `ExecuteCompletion`（`TaskScheduler.cs:858-867`）对本源为**空操作**（本源数据行无 `comp.producer`）⇒ ⛔ 未冒充产出、⛔ 未为本源新增专用副作用分支；
- `Building.currentWorkers` 排除规则未动；`SourceKingdom` 路由未动。

---

## 3. 静态检查（施工顺序 ④）

| 检查 | 结果 | 证据 |
|---|---|---|
| 编译 | **0 error**（本轮全部进局读数均直接引用 `BlacksmithBuilding.Protocol*` / 接缝方法 ⇒ 程序集已带改动编译通过；`execute_code` 反射可解析） | 两臂探针均正常运行 |
| `git diff --check` | **stdout 空 ∧ rc=0**（无空白错误、无冲突标记）；stderr 仅有 `LF will be replaced by CRLF` 转换提示（`core.autocrlf=true` 的既有配置行为，指向 `GameScene.unity`/`TaskScheduler.cs`/`BlacksmithBuilding.cs`/`packages-lock.json`/`pixel-forge` 三件，⛔ 非本轮引入的空白错误） | `post` 阶段 `DIFFCHECK` / `DIFFCHECK_HEAD` ＋ 报告落盘后复核 |
| 禁止面逐文件（16 项） | **违规 0**（16/16 `blob_identical=TRUE`；含 `ITaskScheduler.cs`/`Building.cs`/`ProductionSystem.cs`/`StorageComponent.cs`/`ChestManager.cs`/`BuildingFactory.cs`/`BuildController.cs`/`MineByproductComponent.cs`/`SiegeWorkshopBuilding.cs`/`BuildingDef.cs`/`Blacksmith.asset`/`BlacksmithDef.asset` 等） | `FORBID` 行＋`AGG rowset=FORBID raw=16 excl=0 valid=16` |
| 协议六文件 | 6/6 `diff_files=0 blob_identical=TRUE`（⛔ 未改契约） | `PROTOCOL6` |
| 目录面（5 项）＋四本账＋测试基线台账 | `Scenes` 唯一变化＝O-14 既有挂账；`Assets/Resources/Buildings`、`/Config`、`Prefabs`、`champion`、`Holdout`、`harness/Scenarios`、`_Game/Systems/AI/Core` 全空；`_编号登记`/`_任务队列`/`_交接索引`/`_当前快照`/`测试基线台账` 均 `diff_files=0` | `FORBID_DIR` / `BOOK` |
| 政策值实读（⛔ 未改） | `taskTimeout=30f`（`TaskScheduler.cs:45`）、`workDuration=2f`（`:43`）、`retryMax=1`、冷却 5 tick、`hasDeadline` 显式载体、`TaskCard` 不入档 ⇒ 全部维持 | `POLICY` 行 |
| sim-sync 义务 | **0**（`AI.Core` 对本源零命中：`SIMSYNC_AICORE rc=1 hitline=0`、`SIMSYNC_AICORE2 rc=1 hitline=0`） | 两行 |

---

## 4. 基线复算（前后逐项解释 · 语义口径为主）

| 行集 | HEAD | 工作区 | 差 | 逐项归因 |
|---|---|---|---|---|
| `SchedRef`（`TaskScheduler.(Instance\|HasInstance)`） | 76 行 / 17 文件 | **81** / 17 | **+5** | 本源新增 5 处：`Rt()` 2（`HasInstance` 判空＋`Instance.ProtocolRuntime`）、`MirrorCheck` 2、`CreateSlot` 1（`Instance.NextProtocolTaskId()`）；文件数不变 |
| `NewKT`（`new KingdomTask(`） | 15 / 10 | 15 / 10 | 0 | 本源未新建旧链任务对象 |
| `ITaskSrcDecl` 字面（`[^\n{]`） | 2 / 2 | 2 / 2 | 0 | ⚠️ 字面口径漏计含 `MonoBehaviour`/`ITickable` 的声明行（`D922` ⑤ 已裁：历史字面读数保留作对照，⛔ 不回写） |
| `ITaskSrcDecl` **语义**（`:.*ITaskSource`） | 9 / 9 | 9 / 9 | 0 | 本源已注册为源（不新增类声明）⇒ 不变为正确 |
| `ClsName_ALL`（`\bBlacksmithBuilding\b`） | 9 行 / 5 文件 | **20** / **6** | **+11 行 / +1 文件** | 逐文件直读：`TaskScheduler.cs` 0→**10**（+10 行、+1 文件＝5 条接缝判定行＋5 条相邻注释），`BlacksmithBuilding.cs` 1→**2**（+1 行＝区头注释）；其余 4 文件（`BuildingDef.cs` 2、`BuildingComponents.cs` 1、`MineByproductComponent.cs` 1、`SiegeWorkshopBuilding.cs` 4）**零变化** ⇒ ⛔ 无第三文件被触碰 |

`SEAMCX blacksmith_line=0` 口径说明：该字段统计「接缝调用行内是否出现字面类名」，而调用行形如 `bsDispatch.OnProtocolDispatched(...)`（类名只出现在上一行的 `is BlacksmithBuilding bsDispatch`）⇒ 恒为 0，与 `pre` 阶段同口径，⛔ 不是「本源未接」的反证。

---

## 5. 真实进局取证（施工顺序 ⑤⑥⑦ · 两臂互补）

入口一律为正门 `TestHarnessApi.EnterTestRun(cfg, null)`（HH.92 专用测试环境），⛔ 未裸跑 `GameScene`；`seed=424242`、`difficulty=2`、`worldSize=Small`、`timeScale=15`（实测）。驱动形态＝`execute_code` 方法体 ＋ `EditorApplication.update` 逐帧闭包（⛔ 不嵌套编译，桥端 CodeDom 类型重复定义已自报）。

### 5.1 两臂分工与可比性

| 臂 | 文件 | 结果 | 覆盖判据 |
|---|---|---|---|
| **v2 对照臂**（近＋远两处源、预投 Metal 至 0.8cap+5） | `hh341_small_blacksmith_cx_probe.{cs,txt,json}`（4055 行 / 664470 B / sha256 `FB5C5BBDA6E3D8D5F3A359C69E1E09DB27A943AF32653FC4DE0148FFAB1A00F2`） | `A ✓ C ✓ D ✓`、本体源 Transport 在册且有真实搬运、跨档后源重建再派工、`errs=0` | 失败/源移除/双源并存/跨档 |
| **v3 成功路径臂**（单处源、不预投 Metal、国库先腾空再投 Ore） | `hh341_small_blacksmith_cx_probe_v3.{cs,txt,json}`（563 行 / 530 条 `## S` / 84660 B / sha256 见 §9） | `A ✓ B ✓ E ✓ C ✓ 正产出 ✓ 再广告 ✓`、跨档后 `A2/B2/E2` 齐、`errs=0`、`VIO=0` | 到达/单趟完成/真实产出/再广告 |

两臂同 seed、同规模、同正门、同 `timeScale=15` ⇒ 暖机与对照可比（`L-98`）。v2 的三条能力句未被 v3 取代，v3 也不重复宣称 v2 的读数。

### 5.2 v3 关键读数（全部直读，⛔ 无「≈」）

- 零点：`CFG zero_t=3940.50`，`coord=(65,62)`（`PICK_ROW free=12172 bestDist2=0.00 workerPos=2.56,40.96`），`metal0=0 bank0=0 carried0=0 metalTotal0=0 ore0=60 k0W=4`；
- 起点世界既有：`REFLECT tick=3940 nextId=262 reserv=8 pairs=8 现存Blacksmith组件数=0`（⇒ 本世界原本没有铁匠铺，本源为测试落格）；
- 夹具链：`step=0 rcOre=0 国库 CanAccept(Ore)=0` ⇒ `step=1 Wood −60` ⇒ `step=2 Ore 0→60`（真源＝`RulerController.Ore`＝`TreasureVault` k0 单容器，容量 250 已被 Wood100/Stone100/Food34 占满 ⇒ 不腾空则 Ore 投不进，`Deposit` 受容量钳制）；
- 三段停止：P1 `A∧B∧done>=2∧正产出∧再广告∧>=90游戏秒`（**自行收工，未走窗口上限**）90.11 游戏秒；P2 `读档后按当前tick重建资格→再广告→再派工→再到达(>=60游戏秒)` 60.04 游戏秒；P3 `活卡归零∧预定≤Die前基线 持3游戏秒` 4.20 游戏秒；合计 154.35 游戏秒 / 墙钟 10.92 s；
- 单趟完成 **10 次**（arm=cur 6 次 ＋ arm=post 4 次），逐次行证（示例）：
  `SEAM_ROW A … id=262 … Pending→Assigned … worker=59 abil=BlacksmithBuilding_Task tgtKind=Building cell=65,62 issuer=Kingdom:0`
  `SEAM_ROW B … id=262 到达只推进执行态（Assigned→Executing）`
  `SEAM_ROW E … 单趟完成⇒Done 封口（done 计数 0→1，活卡=0，total=1，本仓 Metal=2，国库 Ore=56）`
- 真实产出闭合：终态 `subMetal=16 subMetalGain=16`，`oreDelta=−32` ⇒ 与 `BlacksmithDef.oreToMetalRatio=2` **严格闭合**（16×2=32）；`metalTotal=16`（近仓＋k0 入库＋工人携行全域）；`rate=0.50`（`producer.rate 0.5 × gradeScale[1] × LevelScale(1)`）；
- 计数器闭合：存前 `total=7 done=6 live=1 ab=0` ⇒ 7=6+1 ✓；读档后新实例 `total=5 done=4 live=1 ab=0` ⇒ 5=4+1 ✓（⛔ 无重复活动卡、⛔ 无悬空绑定）；
- 绑定/预定释放：`RT_ROW` 全程 `invVio=0`；存前 `reserv=8→7 pairs=8→6`，Die 前 `7/6` → Die 后 `6/5` ⇒ 恰 −1/−1，对应本源那一张活卡；
- 跨档 ID 不复用：存前 `nextId=287 旧maxId=286` → 读档后新卡 `289/294/295/296/297`，终态 `nextId=309 maxSeenId=297` ⇒ 单调不复用 ✓；
- `rtHash` 存前 `496194750` → 读档后 `970917874` ⇒ 读档**重建了协议运行时实例**，配对/预定按当前 tick 重新建立（`inSources=True`、再广告、再派工、再完成为证）；
- 失败口径（v3 内天然命中）：`id=265` `Assigned→Pending lastUn=Unreachable retry 1/1 consumed=1` ⇒ 配对释放并回待派；随后该卡被重新派发并 `Done`（`done 2→3`）⇒ `retryMax=1`＝「初次＋重试」封口口径成立，且**未因一次失败封源**；
- 全域取值域（`L-97` 要求，本轮实测）：`subMetal ∈ {0,2,4,6,8,9,12,14,15,16}`、`subCap=250`（恒定）、`subFree ∈ {250,248,246,244,242,241,238,236,234}`、`subFull=False`（恒）、`ore ∈ {60,56,52,48,44,42,40,36,32,30,28}`、`rate=0.50`、`live ∈ {0,1}`、`onBookProd ∈ {0,1}`、`onBookTrans=0`、`reserv ∈ {6,7,8}`、`pairs ∈ {5,6,7,8}`、`invVio=0`、`tick=游戏秒读数`、`workerId ∈ {58,59,60}`、`state ∈ {Created,Pending,Assigned,Executing,Resolving,Done,Aborted}`、`lastUnassignReason ∈ {None,Unreachable}`、`abortReason ∈ {None}`（v3 未产生 `RetryExhausted`/`TargetRemoved` 卡面读数 ⇒ 由 v2 臂取，见 §5.3）；行集来源与三数口径＝`raw=530 / excl=0 / valid=530`（`## S` 行全量入表，⛔ 无排除项），分类计数见 §9。

### 5.3 v2 臂的不可替代读数（⛔ 不用 v3 冒充）

- **D（源失效⇒非终态卡封 `TargetRemoved`）**：v2 命中 `SEAM_ROW D … 源失效⇒非终态卡封 TargetRemoved（ab 计数 0→1，活卡=0）`，并在 `Building.Die(DeathCause.Killed)` 后活卡归零、全局预定/配对下降；v3 的 D 只取到**间接读数**（`liveAtDie=1` → Die 后组件随建筑销毁、`SUB.inst=<无>`、全局 `7/6→6/5` 恰 −1/−1），`[BRANCH] D=False` 的原因是封口计数器随对象销毁不可再读 ⇒ ⛔ 不据此判 D 失败，D 的直接证据以 v2 为准；
- **双源并存分列**：v2 的 `DISP_ROW` 实证本体源（`Building`）广告 `Transport` 且真搬货：`Transport/bld:Blacksmith#-4120/MovingToSource/Gold:0 → /Working → /MovingToDest/Metal:10 → rem`；组件源（`BlacksmithBuilding`）广告 `Production`；两者按**对象身份＋task.type** 分列，⛔ 未合并；
- **合法无产出域**：v2 全程 `ore=0`（国库投不进）⇒ `Transform` 返回 0 ⇒ 「本轮无产出但源仍广告、仍派工」为合法无产出样本。

### 5.4 跨档复验（施工顺序 ⑥）

`Save(hh344bs3)=True` → `Load=True` → 新实例（`SUB.inst=-15920 bld#-15916`，同坐标 `(65,62)`）→ `inSources=True` → 再广告 → 再派工（`A2/B2/E2` 齐，4 次 `Done`）→ `ab=0` ⇒ ⛔ 无绝对 tick 复用、⛔ 无悬空绑定、⛔ 无重复卡、新卡 ID 不复用旧卡。

### 5.5 `L-95` 三段（每段同工具尝试计数如实登记）

| 段 | 手段 | 结果 | 尝试计数 |
|---|---|---|---|
| ① Play 内生产链 | 探针内 `Application.logMessageReceived` 全文捕获 | v2 `errs=0`；v3 `errs=0`（`errCount=0`）、`VIO_ROW=0`、`DRV_ERR=0`；v3 `warnCount=401`（列表截 60 条原文入证） | 装载尝试 1＝v1（缺陷见 §7，⛔ 无世界副作用）；尝试 2＝v2（跑通但四缺陷，降级为对照臂）；尝试 3＝v3（收工条件自洽）⇒ **3/3 用尽** |
| ② 退 Play 后 | 自建文件回调跨退出（`hh341_small_blacksmith_cx_probe_l95c.txt`）＋ `Editor.log` 字节区域 | 唯一 Error＝`Some objects were not cleaned up when closing the scene … [SpriteAnimatorDriver]`；`Exception=0`、`error CS=0` | 1（`read_console` 故障另计，见下） |
| ③ 只进 Play 不建局（空白对照） | 同 ② 的回调，20 秒 | 断言 `playing=True activeMap=False bs=0 gs=Ready day=1 tick=29 ts=1`，段内 `err=0 warn=0`；退出后复现**同一条** `SpriteAnimatorDriver` 文案 ⇒ 该文案与本源的接缝**零参与**（本臂从未建局、从未落格） | 1 |
| 既有性对照（追加） | 施工前一日 `Editor-prev.log` 尾部 4 MB | `not cleaned up when closing the scene` 命中 **1**、`Unable to find type 'SpawnerComponent'` 命中 **89** ⇒ 两条文案均早于本施工存在 ⇒ **非本源引入** | — |

⚠️ 仪器故障自报：`mcp__unitymcp__read_console` 在本轮 3 次调用均 `Unity did not respond within 2.0s`（同期 `execute_code` 正常 ⇒ 判为工具侧故障而非编辑器冻结）。⛔ 未据此降级结论，改以「探针自持回调＋Editor.log 字节区域」作原文来源，两种口径互不替代、并列留证。

---

## 6. 六条能力判据逐条自述（⛔ 不自评通过，仅陈述证据落点）

1. **卡经既定 issuer 建立并按源对象配对/预定，取得 D/A/B/C/E 状态转移** — A/B/C/E：v3 `SEAM_ROW A/B/E/C`（issuer `Kingdom:0`、target `Building@(65,62)`、`hasDL=False`）；D：v2 直接读数＋v3 间接读数（§5.3）。
2. **到达只推进执行态；Working 不写成产出或 Complete；组件源/本体源 worker 归属可分别复算** — v3 `B` 行文案显式声明语义边界；worker ∈ {58,59,60} 逐卡可读；双源分列见 v2 `DISP_ROW`（`comp#` vs `bld:`）。
3. **`ProductionSystem` tick 可观察到 Metal 正增量，或带全量取值域的合法无产出** — v3 `subMetalGain=+16` 且 `oreDelta=−32`（闭合 ratio=2）；合法无产出域＝v2（`ore=0` 全程）。
4. **单趟 `Complete` 后该卡 `Done`、绑定与预定释放、源仍可再广告** — v3 `Done` 10 次，`live` 归 0 后 `total` 继续增长（`再广告=True`），`RT_ROW reserv/pairs` 随之下降，`invVio=0`。
5. **源失效时非终态卡 `TargetRemoved`，不因暂时无矿/满仓/本轮无产出而封源** — v2 D 读数；v3 中 `ore` 一度趋紧、`full=False` 全程，源持续广告未封 ⇒ 反例侧同样成立。
6. **失败卡按 `retryMax=1` 计数（初次＋重试后封口）、无重复活动卡/悬空绑定/未释放预定** — v3 `id=265` 回待派→重试→完成；v2 `RetryExhausted/Aborted` 封口；计数器 `total=done+live+ab` 在两臂各快照均闭合。
7. **读档后按当前 tick 重建、新卡 ID 不复用旧卡、完成 `L-95` 三段与前后基线逐项对拍** — §5.4、§5.5、§4。

---

## 7. 探针缺陷与自纠（⛔ 掩盖，全部留痕）

| # | 缺陷 | 影响 | 归因 | 处置 |
|---|---|---|---|---|
| 1 | v1 `handler` 只做「调用时自摘/自挂」，装载时从未 `EditorApplication.update += handler` | 驱动空转；正门协程只建句柄、一次未步进 ⇒ ⛔ 无建局/落格/投料等任何世界副作用 | 取数工具（编排缺陷） | 取证＝`update` 订阅列表 25 项中无本探针项且水位为空；v2 起改为装载时一次性登记＋`finished`/非 Play 自摘 |
| 2 | `DumpCards` 末尾 `prevC.Clear()` 把另一侧键一并清空 | v2 逐卡状态转移（A/B）被冲散成每帧 add；v2 降级为对照臂 | 取数工具 | v3 只删本侧失效键 |
| 3 | `sawTransportOnBook` 判定含 `nearBldId >= 0`，而 Unity 场景对象 `GetInstanceID()` 为**负** | v2「Transport 在册」恒假＝**假阴性读数**（实际在册，见 `DISP_ROW`） | 取数工具 | v3 哨兵改 `-99999`；⛔ 撤销 v2 的该项结论，改用其 `DISP_ROW` 原文 |
| 4 | 夹具 `ModifyResource(Ore,+,120)` 后 `Ore` 仍 0 且未回读验证 | v2 无法产出（真因＝国库单容器 250 被占满，`CanAccept(Ore)=0` ⇒ `Deposit` 钳到 0） | 取数工具（夹具设计）＋**世界既有约束**（见 §8-V13） | v3 先探 `CanAccept`，必要时 `Wood/Stone` 负向腾空再投，并逐步回读 `before/after` |
| 5 | 预投 Metal 至 0.8cap+5 使本体源 Transport 独占 4 名工人 | v2 组件源 Production 420 游戏秒仅获 1 次派发即失败 ⇒ B/E 无从观测 | 取数工具 | v3 成功路径臂 ⛔ 不预投 Metal、只落一处源 |
| 6 | `K0Bank` 声明 `Func<int,int>` 但带两参 | 一次**编译失败**（未执行 ⇒ ⛔ 无任何世界动作、不占 `L-95` 段尝试） | 取数工具 | 改 `Func<int,int,int>` 后装载成功 |
| 7 | 中文路径/`Path` 反斜杠作 git pathspec、`git status --short --cached` 非法选项等 | 预检期即自纠并已入 HH.343 报告 §11 | 工具口径 | 本轮闸门脚本沿用 blob id 双口径（`rev-parse HEAD:<path>` 与 `hash-object <path>`），⛔ 直比磁盘 sha256 与 `git show` sha256 |
| 8 | 在 `execute_code` 内嵌套 `Microsoft.CSharp.CSharpCodeProvider` 编译探针（去重引用集后仍报 `imported type defined multiple times`） | 本窗口早前一次**会话中断**的直接原因（用户问「怎么暂停了」即此）；⛔ 未产生任何世界动作 | 取数工具（桥端引用集自身类型重复 ⇒ 嵌套编译不可用） | 放弃嵌套编译，改「一次调用建立逐帧闭包驱动」；本项属 `L-97` 三分归因中的「工具问题」，⛔ 不作接缝代码缺陷 |

---

## 8. 观察项（另案登记建议，⛔ 本轮不动）

- **V4**（完成判据双层）：本窗口已按裁决实现——调度器侧 `Complete/Done` 证明任务收口，`ProductionSystem.cs:50 → BlacksmithBuilding.cs:43-61 → StorageComponent.cs:245-265` 证明真实产出；`ExecuteCompletion` 对本源空操作未冒充。
- **V8**（升级不刷 `_rate`）：`Building.cs:714-732 OnConstructionComplete` 只刷 `ProducerComponent.RefreshRate()` 与 storage 容量，本源 `_rate` 由 `RefreshRate()` 自持 ⇒ 升级后 `_rate` 是否即时更新为**未取证观察项**；本窗口 ⛔ 未改 `Building.cs`（禁止面）⇒ 建议另案。
- **V10**（`MetalRate` 生产域零消费者）：只读诊断口，除探针外无生产消费者 ⇒ 保留为验收口，登记「无消费」。
- **V13**（新发现 · 建议入需求/策划侧）：**国库单容器容量 250 在新开局即被 Wood100/Stone100/Food34 占至 `CanAccept(Ore)=0`** ⇒ 铁匠铺的原料门（`RulerController.Ore`）在默认开局下**天然不可满足**，铁匠铺生产链在真实玩法里可能永不自举。本轮以夹具（Wood −60）绕过取证，⛔ 未改任何政策值/资产。是否属玩法缺陷请主策划裁。
- 其他沿承观察项 V5–V12 无新增结论，维持预检窗口报告的登记。

---

## 9. 证据清单（前缀 `Valley Rampart/Logs/hh341_small_blacksmith_*`，`Logs/` 被 `.gitignore` 忽略 ⇒ ⛔ 不入提交）

| 文件 | 用途 |
|---|---|
| `hh341_small_blacksmith_cx_gate_pre.txt` | 开工回退点与基线（336 行（`wc -l` 口径）/ 34858 B / `dfd779aa20932b333bed62b200166d9d4283c216f1a4291f02f2b2163c99bc05` |
| `hh341_small_blacksmith_cx_gate_mid.txt` | 施工后改动收据（+257/+18、接缝五支、政策值、禁止面；345 行 / 35724 B / `d13cb0447a6c466e5f4ca7d63b329bfc74098bebdee7267915812e1da1b2afb7`） |
| `hh341_small_blacksmith_cx_gate_post.txt` | 静态检查与基线复算（344 行 / 36020 B / `E0A3FBA86E35CE68A1444AEF7BB3DB3624F2BED61CD918ADC69C8335892AAF4E`） |
| `hh341_small_blacksmith_cx_probe.cs/.txt/.json` | v2 对照臂（.txt 4055 行 / 664470 B / `FB5C5BBDA6E3D8D5F3A359C69E1E09DB27A943AF32653FC4DE0148FFAB1A00F2`；.cs 归档件 777 行 / 50486 B / `cc781d605e68e6b23cf16b43918dc28711f9317f74075469704c9920b6c13564`；.json 8 行 / 638 B / `d4bddaf96689c77d303a38f87f9f99347b2d47757a5493645d31dc290545722a`） |
| `hh341_small_blacksmith_cx_probe_v3.cs/.txt/.json` | v3 成功路径臂（.txt 563 行 / 84660 B / `e8cd6ea2ada5a9a267e4afb9744dc7077a0c9c2ae0ccc8abbe48723c05cc4fa3`；.json 680 B / `84f271547e660fd89ba935b22bc06de4076c845e0765227d9793783fd9d4c271`；.cs 归档件 753 行 / 47076 B / `c99ed274a0fbfe177a30c720110840e1b8ed052f207d3666b36e463b2a39b08d`）；分类计数 `SNAP=29 RT=30 CARD=35 DISP=204 SEAM=35 LOG=126 WARN=60 ERR=0 VIO=0 PICK=1 SPAWN=1 FILL=3 CFG=1 WARM=4 SEAL=1 DRV_ERR=0` |
| `hh341_small_blacksmith_cx_probe_l95b.log` | `L-95` 段② 的 `Editor.log` 字节区域原文（122931833→122935099，3266 B / 20 行 / `2f21cee5372fd250c3a0a79486e4d18bf4f80b17c3bbfc44f06597e22cb51c3d`） |
| `hh341_small_blacksmith_cx_probe_l95c.txt` | `L-95` 段③ 空白对照＋段② 退出期的自持回调原文（513 B / 8 行 / `b0959551132bffa5ed9c0d64202f4be538d1486c8a3d0ddb1eb7838628bc5cba`） |
| `hh341_small_blacksmith_cx_gate.py` | 闸门器（`--phase pre|mid|post`） |

写动作全清单（⛔ 未人工造卡、⛔ 未裸跑 GameScene、⛔ 未改任何生产政策值/资产/场景）：落格＝`BuildingFactory.CreateBuildingInstance`（k0·Active）；夹具＝`RulerController.ModifyResource`（v2 `Ore +`；v3 `Wood −60`＋`Ore +60`）、v2 另有 `StorageComponent.Add(Metal)` 预投料；跨档＝`SaveManager.Save/Load`（槽 `hh344bs`/`hh344bs3`，收尾已 `SaveManager.Delete`）；源移除＝`Building.Die(DeathCause.Killed)`；v2 失败样本走自然 `Unreachable/Timeout`（⛔ 未施加伤害，兜底夹具条件未命中）。

---

## 10. 【请裁】三项（按工单要求上报，⛔ 不自选边）

1. **本体源 `Transport` 无协议接缝**：本源为「组件源（`BlacksmithBuilding`，广告 `Production`）＋本体源（`Building`，广告 `Transport`）」双源并存。`Transport` 的源对象是 `Building`，要给它协议接缝必须改 `Building.cs`＝**禁止面**。现状＝本体源任务只有旧链、无镜像卡。请裁：(a) 维持本源窗口只做组件源接缝（本轮即此），本体源 `Transport` 的接缝另开窗口并先解除禁止面；或 (b) 认可「循环常驻源接缝只挂组件侧」为五小源的统一落点（`MineByproduct`/`Chest` 先例是否有同型分歧已一并核实：`MineByproductComponent` 同为先落组件侧）。
2. **`V4` 完成动作对本源的具体处置**：`ExecuteCompletion` 对本源空操作（数据行无 `comp.producer`）。本轮严格按裁决不冒充、不新增专用分支，产出证据全部来自 `Tick → StorageComponent.Transform` 链。请裁：是否长期维持「本源完成动作＝空操作，产出真源在 `Tick`」这一口径（若维持，建议写入文档；若改动则触碰 `TaskScheduler.cs` 的完成语义，超出最小接缝范围）。
3. **扩面请求（一条）**：`V13` 所指「国库容量使铁匠铺原料门天然不可满足」若判为玩法缺陷，修复面涉及 `Blacksmith.asset`/`BlacksmithDef.asset`/主城容量口径＝**本轮禁止面**。请裁是否另开工单处理；本轮仅取证与登记。

---

## 11. 停手条件自查（7 条逐一）

| 条 | 判定 | 依据 |
|---|---|---|
| 1 接缝无法在现有接口下完成／需扩 `ITaskScheduler` 或新增全局主表 | **未命中** | 仅两处允许面文件，未改接口 |
| 2 需改 `Building.cs`/`ProductionSystem.cs`/`StorageComponent.cs`/场景/Prefab/旧资产键/AI.Core/账本 | **未命中**（本体源 `Transport` 的需求已转 §10-1 请裁，⛔ 未擅自扩面） | 16/16 禁止面 blob 全等 |
| 3 需改任一政策值 | **未命中** | `POLICY` 实读 30f/2f 未变 |
| 4 生产面无真实任务链，只能依赖影子面/探针/造卡/静态命中 | **未命中** | 全部卡片出自真实派发链；v3 有 10 次生产面 `Done`＋正产出闭合 |
| 5 `_npcTaskMap` 仍是占用权威，或配对/预定在终态/读档后不归零 | **未命中** | 权威＝`TaskBindingManager`；`_npcTaskMap` 仅逐次对拍；终态/读档后 `reserv/pairs` 与活卡数闭合，`invVio=0` |
| 6 `L-95` 原文缺失／换工具替代／三次失败强行降级／新增未归因 Console 错误 | **⚠️ 部分需事务端复核**：三段原文齐；段① 同工具 3 次已用尽（⛔ 未强行降级，v2 保留为对照臂而非判绿）；`read_console` 故障已自报并另立两种口径并列；段② 唯一 Error 已用空白对照＋施工前日志**双重排除**归因 | §5.5 |
| 7 旧链基线变化无法逐项解释／需自定下一源顺序／声称 §九-6 单管理器达成 | **未命中** | §4 逐项归因（+5、+11/+1）；⛔ 未申请第 5 源、⛔ 未自称单管理器达成 |

---

## 12. 提交面与边界声明

- 提交面＝**本报告单文件**（具名 `git add -- <本报告>`，⛔ `-A`，⛔ push）。
- 生产码改动（`BlacksmithBuilding.cs +257`、`TaskScheduler.cs +18`）**留在工作区不入库**，待事务端核实后按裁决入库；本轮 ⛔ 覆盖任何既有证据文件、⛔ 触碰第 1~3 源/D1 证据。
- ⛔ 未取 D 号（台账与 D 号归事务端管理）；⛔ 未写四本账；⛔ 未宣称判绿。源级结论止于：**施工完成 · 待事务端核实**。
- 交付报告号说明：工单 §证据与提交要求「完成报告号按账本实时水位线取号，先登记后落盘」，但同一工单 §边界要求执行端「⛔ 不取 D 号、⛔ 不碰四本账」。两条并读的执行解＝本报告以 `HH.344` 窗口号命名落盘，**D 号位留空由事务端登记**（本报告即自报这一口径歧义，请事务端确认）。

## 13. 四步闸门与提交记录

| 步 | 内容 | 读数 |
|---|---|---|
| 1 mtime 变化 | 报告落盘后 `mtime=2026-09-30 14:34:57`（写后变化确认） | ✓ |
| 2 磁盘重读 | 首行 `# HH.344 · 第 4 源 BlacksmithBuilding 最小任务协议接缝 · 施工窗口交付报告`／末行 `（执行端 · 2026-09-30）` 一致 | ✓ |
| 3 sha256 ＋ 长度 | 216 行 / 27851 B / `1a35a2dc127ee5a4e79efd37c6eb20cbed46c408ff5e275f9d10c98ad082138d` | ✓ |
| 4 提交 vs 磁盘（blob 口径，⛔ autocrlf 下直比 sha 会假差异） | 提交 `fad50855`；`git rev-parse HEAD:<报告>` ＝ `git hash-object <报告>` ＝ **`f661f398fdade6618c44e1dc9a2e90fe2bd273f7`** | ✓ |

- 提交面核验：`git diff --cached --name-only` 仅报告一件（`216 / 0`），⛔ 未 `-A`、⛔ 未 push；
- 生产码改动仍在工作区未入库（`TaskScheduler.cs`、`BlacksmithBuilding.cs` 为 `M` 态，另有开工前既挂账的 `GameScene.unity`(O-14)/`packages-lock.json`/`pixel-forge` 三件，本轮 ⛔ 未触碰）；
- 本文件补记第 13 节后另起一次单文件提交（同型先例＝HH.343 交付用三笔单文件提交），第 4 步对账以该笔提交的 blob 为准。

（执行端 · 2026-09-30）
