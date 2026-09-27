# HH.341 · 小源首源 —— **`D902` 复议 `Q3(c)` 派工迁移记录 ＋ 源选择取证** 交付报告

> 执行端 ｜ 2026-09-27 ｜ **只读取证（A 段先记派工迁移 → B 段再查源选择）** ｜ ⛔ 零生产码／资产／政策值 ｜ ⛔ 未 push ｜ 挂 `D902`（⛔ 未取新号）
> 依据：`D902` 复议裁定（台账 **§二百一十三**）｜ `L-97`（含 `D897` 补条）｜ `L-98`（已解除「待复核」＋ `D902 Q6` 补条）｜ `L-99`
> 基线：**开工实测 `HEAD = c68d10c1` ＝ 收工实测 `c68d10c1`**（无漂移；相对本端上轮交付提交 `08ef1b34`，其间有 **2 笔非本端 docs commit**（`90a9d80e` D902 核实、`c68d10c1` D902 复议落账）——如实列报）
> ⛔ 未触碰 `Valley Rampart/Assets/Scenes/GameScene.unity`（`L-99`：**只登记 hash**，⛔ 未还原／未 `checkout`／未 `restore`）｜ ⛔ 未投料／未起算零点／未选工／未改 `taskTimeout`／`Complete` 判据 ｜ ⛔ 未并 `DZ-7`／未申请下一源／未代写策划端账本（`D767`）
> ⛔ **不硬编码 `npc22`**：本轮按 §一 条件重新定位 ⇒ 锚点＝**`npcId=20`**（`kingdomId==0 ∧ Worker ∧ 在册`；⛔ 上局实例 `npc22` 本轮未在册）

**一句话证据链**：池 4 人（19/20/21/22）→ `t=65.94` 游戏秒 **npc20＋npc21 同帧被派 `Transport`（源 `ChestEntity`@(8.32,66.24)）** → `t=96.13` **两人均 `Abandon Transport reason=Timeout`**（间隔 **30.19 游戏秒**，日志与轮询双证）⇒ 链＝`None → MovingToSource → Aborted(Timeout)`（**未达 `Working`／`MovingToDest`**）。

---

## 一、`Q6` 观察窗声明（7 项·强制）＋ `L-97` 三件套

### ① `Q6` 7 项（实测，原样见 `…_dispatch.txt` 第 55–61 行）

| # | 项 | 实测值 |
|---|---|---|
| ① | 暖机起止及游戏秒 | `t_start=22.35`（建局前）→ `t_end=65.94`（锚点派发）＝ **43.6 游戏秒**（含建局 0.33） |
| ② | 轮询起止及游戏秒 | `t_start=22.68`（入局）→ `t_end=96.13`（收工）＝ **73.4 游戏秒**（每帧轮询；心跳 3 行） |
| ③ | 触发或任务零点 | **`t0=65.94`（锚点=20）**（`None→MovingToSource` 首次在册帧；`Assigned` 同帧瞬态，见 §六） |
| ④ | 观测窗起止及游戏秒 | `22.68 → 96.13` ＝ **73.4 游戏秒**（＝轮询窗） |
| ⑤ | 墙钟与折算关系 | 墙钟＝**5.27 s**（`Stopwatch`）；**折算常数＝`timeScale=15.0`**（⛔ 未引「短窗倍率」；⛔ 禁 `n × timeScale`） |
| ⑥ | 收工条件 | 声明＝「锚点链回未在册（`chainEnd`）或锚点派发后 300 游戏秒上限（`capPost`）」；**实际触发＝`chainEnd`** |
| ⑦ | 是否在任务段内 | **是**（自锚点派发 65.94 跨任务段至其终止 96.13；`EXIT_OK` 前收工） |

### ② `L-97` 三件套

**统计基数**（`## S` 采样行）：`POOL_ROW N=4`（行 11–14）｜`POLL_ROW N=3`（行 16–50）｜`MIG_ROW N=4`（行 18–52）｜`SRC_ROW N=24`（行 22–45）。
**非采样行**（不计入）：段标记＝4｜汇总＝24｜其他＝1。
**正则**：锚 `^## S <行类型> <字段全名>`；值捕获 `([^\s|}]*)`（停在空白／竖线／右花括号）；⛔ 无 `\S+`。**统计器先空跑**（空输入：全字段 0→0→0，分母 0）→ `…_dispatch_stat_dry.txt`；实跑 → `…_dispatch_stat.txt`。

**字段三数（原始 → 排除 → 最终；分母＝最终有效数）**——全量 47 字段见 `…_dispatch_stat.txt` §③，下方为核心 33 行（⚠ `BLD.*／CHEST.*／COMP.*／CSS.*` 为子集字段，分母小于 `SRC_ROW` 行数属预期）：

| 字段 | 锚行类型 | 原始 → 排除 → 最终 | 分母 |
|---|---|---|---|
| `POOL.npcId` | POOL_ROW | 4 → 0 → **4** | 4 |
| `POOL.occupation` | POOL_ROW | 4 → 0 → **4** | 4 |
| `POOL.wState0` | POOL_ROW | 4 → 0 → **4** | 4 |
| `POOL.onBook0` | POOL_ROW | 4 → 0 → **4** | 4 |
| `POLL.t` | POLL_ROW | 3 → 0 → **3** | 3 |
| `POLL.anchor` | POLL_ROW | 3 → 0 → **3** | 3 |
| `POLL.anchor.onBook` | POLL_ROW | 3 → 0 → **3** | 3 |
| `POLL.anchor.wState` | POLL_ROW | 3 → 0 → **3** | 3 |
| `POLL.poolOnBookN` | POLL_ROW | 3 → 0 → **3** | 3 |
| `MIG.no` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.t` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.npcId` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.kingdomId` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.occ` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.onBook.from`／`MIG.onBook.to` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.wState.from`／`MIG.wState.to` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.task.type` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.source.GetType()` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.mapEntry` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.卡ID` | MIG_ROW | 4 → 0 → **4** | 4 |
| `MIG.ended.task` | MIG_ROW | 2 → 0 → **2** | 2（子集：仅终止行） |
| `MIG.anchor` | MIG_ROW | 4 → 0 → **4** | 4 |
| `SRC.type` | SRC_ROW | 24 → 0 → **24** | 24 |
| `SRC.kingdom` | SRC_ROW | 24 → 0 → **24** | 24 |
| `SRC.kingdomNote` | SRC_ROW | 24 → 0 → **24** | 24 |
| `SRC.valid` | SRC_ROW | 24 → 0 → **24** | 24 |
| `SRC.pos` | SRC_ROW | 24 → 0 → **24** | 24 |
| `SRC.inFlight` | SRC_ROW | 24 → 0 → **24** | 24 |
| `SRC.anchorSrc` | SRC_ROW | 24 → 0 → **24** | 24 |
| `BLD.def`／`BLD.state`／`BLD.awaiting` | SRC_ROW | 20 → 0 → **20** | 20（建筑子集） |
| `CHEST.total`／`CHEST.empty` | SRC_ROW | 1 → 0 → **1** | 1（箱源子集） |
| `CSS.awaiting`／`CSS.satisfied`／`CSS.remaining`／`CSS.fail`／`CSS.want`／`CSS.pickupRecon`／`CSS.card`／`CSS.ownerDef` | SRC_ROW | 0 → 0 → **0** | **0（css 源数=0 ⇒ 结构性空）** |
| `COMP.parent` | SRC_ROW | 3 → 0 → **3** | 3（副产组件子集） |

**全量取值域（含零计数项）** 摘要：
- `POOL.occupation`＝`Worker=4｜Civilian=0｜Porter=0`；`POOL.wState0`＝`None=4`（其余 TaskState 全 0）；`POOL.onBook0`＝`False=4｜True=0`。
- `MIG.wState.from/to`＝`None=2｜MovingToSource=2`（`Assigned`／`Working`／`MovingToDest`／`Completed`／`Abandoned` 全 **0**）；`MIG.task.type`＝`<无task>=2｜Transport=2`（其余枚举全 0）；`MIG.source.GetType()`＝`<无task>=2｜ChestEntity=2`（**`ConstructionSiteStore`=0**｜其余 0）；`MIG.mapEntry`＝`有=2｜无=2`；`MIG.anchor`＝`1=2｜0=2`；`MIG.卡ID`＝`<未取得:协议运行时未初始化>=4`（分母 4）。
- `SRC.type`＝`Building=20｜MineByproductComponent=3｜ChestEntity=1`（**`ConstructionSiteStore=0｜WorldGatherSource=0｜Transport=0`**，分母 24）；`SRC.kingdom`＝`1=7｜2=7｜3=7｜-1=3｜0=0`（分母 24）；`SRC.valid`＝`True=24｜False=0`；`SRC.inFlight`＝`False=20｜True=4`；`SRC.anchorSrc`＝`1=1｜0=23`。
- `BLD.state`＝`Active=20`（`Placing`／`Constructing`／`Dead`／`Abandoned`／`Ruined` 全 0）；**`BLD.awaiting`＝`False=20｜True=0`**；`BLD.def`＝`House=3｜Warehouse=3｜Well=3｜castle=3｜farm=3｜mine=3｜VagrantCamp=2`；`CHEST.total`＝`100=1`、`CHEST.empty`＝`False=1｜True=0`；`COMP.parent`＝`mine=3`。

**证据链**：采样行在 `Valley Rampart/Logs/hh341_small_construction_site_dispatch.txt`（POOL 11–14｜POLL 16/50/（83.17 行 50）｜MIG 18/49/51/52｜SRC 22–45）；辅助原始日志 `…_dispatch.logs.txt`（238 行筛选自桥接 `capture_logs`，含锚点派发/Abandon 两行）；统计器 `…_dispatch_stat.py`（`FIELDS` ＋ `DOMAINS` 域表 ＋ `scan_field()`）。**复现命令**：

```powershell
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_dispatch_stat.py" --out "Valley Rampart\Logs\hh341_small_construction_site_dispatch_stat_dry.txt" "Valley Rampart\Logs\hh341_small_construction_site_dispatch_stat_dry_in.txt"
pwsh -NoProfile -File "Valley Rampart\Logs\hh341_small_construction_site_dispatch_run.ps1" -CsName "hh341_small_construction_site_dispatch.cs" -RequestId "hh341disprt2" -TimeoutSec 300
pwsh -NoProfile -File "Logs\_bridge_call.ps1" -Payload "Valley Rampart\Logs\hh341_small_construction_site_dispatch_stop.json" -TimeoutMs 60000
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_dispatch_stat.py" --out "Valley Rampart\Logs\hh341_small_construction_site_dispatch_stat.txt" "Valley Rampart\Logs\hh341_small_construction_site_dispatch.txt"
```

（路径：`exec_runtime_script` 自动进 Play ⇒ 正门 `TestHarnessApi.EnterTestRun`（seed=424242/Small/difficulty=2/槽 `hh341dispatch`）⇒ 每帧轮询＋派发帧源快照 ⇒ `ExitTestRun` ⇒ `manage_editor stop`；桥接响应 `elapsed_ms=6215`，`success=true`。）

---

## 二、A 段：派工迁移链（**原样**）

### `MIG_ROW` 全量（4 行，逐字）

```
## S MIG_ROW MIG.no=1 MIG.t=65.94 MIG.npcId=20 MIG.kingdomId=0 MIG.occ=Worker MIG.onBook.from=False MIG.onBook.to=True MIG.wState.from=None MIG.wState.to=MovingToSource MIG.task.type=Transport MIG.source.GetType()=ChestEntity MIG.mapEntry=有 MIG.卡ID=<未取得:协议运行时未初始化> MIG.anchor=1
## S MIG_ROW MIG.no=2 MIG.t=65.94 MIG.npcId=21 MIG.kingdomId=0 MIG.occ=Worker MIG.onBook.from=False MIG.onBook.to=True MIG.wState.from=None MIG.wState.to=MovingToSource MIG.task.type=Transport MIG.source.GetType()=ChestEntity MIG.mapEntry=有 MIG.卡ID=<未取得:协议运行时未初始化> MIG.anchor=0
## S MIG_ROW MIG.no=3 MIG.t=96.13 MIG.npcId=20 MIG.kingdomId=0 MIG.occ=Worker MIG.onBook.from=True MIG.onBook.to=False MIG.wState.from=MovingToSource MIG.wState.to=None MIG.task.type=<无task> MIG.source.GetType()=<无task> MIG.mapEntry=无 MIG.卡ID=<未取得:协议运行时未初始化> MIG.ended.task=Transport/ChestEntity MIG.anchor=1
## S MIG_ROW MIG.no=4 MIG.t=96.13 MIG.npcId=21 MIG.kingdomId=0 MIG.occ=Worker MIG.onBook.from=True MIG.onBook.to=False MIG.wState.from=MovingToSource MIG.wState.to=None MIG.task.type=<无task> MIG.source.GetType()=<无task> MIG.mapEntry=无 MIG.卡ID=<未取得:协议运行时未初始化> MIG.ended.task=Transport/ChestEntity MIG.anchor=0
```

### 锚点行 ＋ 辅助原始日志（`…_dispatch.logs.txt` 内原样两行）

```
== ANCHOR_FOUND npcId=20 t0=65.94 定位条件=kingdomId==0∧Worker∧在册（EntryState=MovingToSource）
== ANCHOR_DETAIL task.type=Transport source.GetType()=ChestEntity
[Log] [TaskScheduler] 派发 Transport 任务 → npcId 20 @ (8.32, 66.24)（优先级 B）
[Log] [TaskScheduler] Abandon Transport → npcId 20 reason=Timeout
```

### 链摘要（锚点=`npc20`）

**`None` → `MovingToSource`（65.94，同帧 `Assigned` 瞬态）→ `Aborted`（96.13，`Abandon reason=Timeout`；派发→放弃间隔＝30.19 游戏秒）** ⇒ **未观测到 `Working`／`MovingToDest`／`Completed`**；`Abandoned` 亦不落 `_npcStateMap`（`ClearNpc` 移除 ⇒ 帧级只见「回未在册」）。npc21 与锚点**同帧同源**被派、同帧同因放弃（行 2/4，`anchor=0`，仅登记）。

---

## 三、A 段「5 项闭合」逐条判定（裁定原句：**「已有迁移、在册条目、任务类型、来源类型和工人 ID 全部闭合 ⇒ 派工触发链可由『弱支持』提升为『已支持』」**）

| # | 闭合项 | 判定 | 证据（逐条） |
|---|---|---|---|
| 1 | **已有迁移** | ✅ **闭合** | 锚点 2 次状态迁移逐次落盘（行 18：`None→MovingToSource`；行 51：`MovingToSource→None`）；日志双证（`派发`／`Abandon`）；⛔ 非只记首尾 |
| 2 | **在册条目** | ✅ **闭合** | 行 18 `MIG.mapEntry=有`（`_npcTaskMap[20]` 非 null，含 `task.type`／`source`）；行 51 `MIG.mapEntry=无`（终止后移除） |
| 3 | **任务类型** | ✅ **闭合** | `MIG.task.type=Transport`（行 18；域 `Transport=2`） |
| 4 | **来源类型** | ✅ **闭合** | `MIG.source.GetType()=ChestEntity`（行 18；且与 B 段 chest@(8.32,66.24) 对应：`SRC.anchorSrc=1`） |
| 5 | **工人 ID** | ✅ **闭合** | `MIG.npcId=20`（池成员：`kingdomId=0 ∧ Worker`）；日志两行均以 `npcId 20` 闭合同一 id |

⇒ **5 项全部闭合** ⇒ **达到裁定原句「可由『弱支持』提升为『已支持』」的判据条件**（本端如实列证，⛔ **不替事务端采信**；⛔ 同时**不得写成「首源已派工」**——`source=ChestEntity ≠ ConstructionSiteStore`；采信口径见【请裁】2）。

---

## 四、B 段：源选择取证（3 条成立判据逐条判定＋降级口径适用性）

**快照时点**：锚点派发帧 `t=65.94`（单帧时点；⛔ 不外推全程）。**源池汇总（原样）**：`== B_SUM sources=24 valid=24 css=0 chest=1 bld=20 wgs=0 other=3`。

| # | 成立判据 | 判定 | 证据 |
|---|---|---|---|
| ① | 同一派发上下文中存在 `ConstructionSiteStore` 候选 | ❌ **不成立** | `_sources` 全量 24 中 `css=0`（`SRC.type` 域 `ConstructionSiteStore=0`，分母 24；`CSS.*` 8 字段分母全 0） |
| ② | 实际选择为 `ChestEntity` | ✅ **成立** | 锚点源＝`ChestEntity`（`SRC.anchorSrc=1`；`CHEST.total=100`／`CHEST.empty=False`；`SRC.inFlight=True`）；日志 `派发 Transport 任务 → npcId 20 @ (8.32, 66.24)` |
| ③ | 能取得可复核的排除条件或选择依据 | ⚠️ **部分**（对 CSS 不适用） | CSS：**无候选 ⇒ 排除条件无从暴露**；实际选择侧已录可见状态（箱：`valid=True`／`total=100`／`inFlight=True`；王国＝`-1` 无主池） |

⭐ **整体结论**：3 条**不成立**（判据①不满足）⇒ **适用降级口径①**：只能登记「**首源未进入候选范围**」；⛔ **不得直接判定为选择缺陷**（降级口径②对此亦不触发——无候选可比）。

⭐ **「未注册 vs 被前置条件排除」的可见面**（本批已顺带列全量候选源清单 24 行 = 请裁 2 的「追加」部分在本批即为既有交付）：
- **全量建筑源 `BLD.awaiting=False`＝20/20**（`True=0`）⇒ **无任何建筑处于投料态**；
- **`SRC.kingdom=0`（玩家王国）的建筑源＝0/24**；
- 依源码只读引用（`ConstructionSiteStore.cs:21` 生命周期注）：工地仓由**投料态** `Building` 创建并注册 ⇒ **本时点不存在任何 CSS 对象**（「未注册」呈现为「**承载对象尚未出现**」；更细成因链见【请裁】1）。
- 全量 `SRC_ROW` 24 行（类型／王国／可用／在册／锚点源标记／细节）在 `…_dispatch.txt` 第 22–45 行**原样**，含 20 建筑＋3 副产组件＋1 箱。

**④ 卡 ID（裁定第 ④ 小项）**：优先走 `_sources` 等非懒初始化字段 —— **CSS 侧**：无源（`CSS.card` 分母 0）；**工人侧**：反射读 `_protocolRuntime` **字段**（⛔ 未调用懒 getter）＝ **`null`（未初始化）** ⇒ 全链 `MIG.卡ID=<未取得:协议运行时未初始化>`（域 4/4）⇒ 登记「**未取得**」＋已试路径（见 §六-1）。

---

## 五、已排（本轮 ⛔ 不主张）

- ⛔ 未把「派工触发链 5 项闭合」写成「**首源已派工**」（`source=ChestEntity`；`ConstructionSiteStore` 全批 **0**）。
- ⛔ 未把 `Abandon(Timeout)` 写成机制结论／缺陷判定（链现状**原样**列报；`taskTimeout` 值本轮未读，超范围）。
- ⛔ 未调用任何源侧方法（`TryAdvertiseTask` 等**一律未调**）；⛔ 未触发 `_protocolRuntime` 懒初始化（仅字段反射）。
- ⛔ 未改暖机／未起算零点／未开观测窗／未投料／未选工／未改码／未碰 `GameScene.unity`／未并 `DZ-7`／未取新号／未申请下一源。
- ⛔ 未把 B 段单帧快照外推为全程状态（§八-3）。

## 六、未取得（`L-97` 三件套）

| # | 未取得项 | ① 观测范围（N） | ② 筛选条件（被排除取值＋计数） | ③ 全量取值域＋计数（分母） |
|---|---|---|---|---|
| 1 | **卡 ID（工人侧）** | MIG_ROW N=4（轮询窗 22.68→96.13 游戏秒） | 反射读 `_protocolRuntime` **字段**（⛔ 未调 getter）；`null` ⇒ 回显未初始化；**被排除取值 0** | `MIG.卡ID` 域：`<未取得:协议运行时未初始化>`=**4**（分母 4）；`工配:`／`源卡:`／`<无配对>`／`<未取得:无_workerToTask>`＝**0**。**已试路径**：`_protocolRuntime` 字段；后续路径（`BindingManager`→`_workerToTask`／CSS `ProtocolCard`）因运行时未初始化或 css=0 未达 |
| 2 | **源侧卡 ID（CSS）** | SRC_ROW N=24（快照帧） | —（无 CSS 行） | `CSS.card` 分母 **0**（结构性不适用：css=0） |
| 3 | **`Assigned` 帧级观测** | MIG_ROW N=4＋POLL N=3 | 每帧读口（`_npcStateMap`）；帧边界未捕获 | `MIG.wState.from/to` 域 `Assigned`＝**0**（两向）；已观测 `None`／`MovingToSource`。说明：同帧 `Assigned→MovingToSource`（源码只读引用：`Tick` ④派发后 ⑤`UpdateAssignedTasks` 同帧推进）——⛔ 不据此推断，列报供裁 |
| 4 | **`Working`／`MovingToDest`／`Completed`／`Abandoned` 状态** | MIG_ROW N=4＋心跳 3 行（窗 73.4 游戏秒） | 锚点（npc20）链；**链止于 `Abandon(Timeout)`** ⇒ 未达 | `MIG.wState.to` 域：`Working=0｜MovingToDest=0｜Completed=0｜Abandoned=0`；已取得＝**终止事件类型**（`Abandon reason=Timeout`，日志行实证；`Abandoned` 状态按 `ClearNpc` 移除不落 map ⇒ 帧级不可见） |
| 5 | **玩家王国建筑全量（含非 Active／未注册）与「未注册成因链」** | —（**范围外**，非本批 A/B 取证项） | — | `SRC.kingdom=0`＝**0/24**（⛔ 不等价「玩家王国无建筑」——仅注册面为 0）⇒ 列【请裁】1 |

---

## 七、合规声明（实测输出）

```
## git rev-parse HEAD（开工=收工，实测一致）
c68d10c1aa5ce5d94dd974faca022ff44f7c37f9
## 漂移列报：相对本端上轮交付 08ef1b34，其间 2 笔非本端 docs commit：
##   90a9d80e docs(D902): 控制段核实（全采信＋折算常数确证＋K0_ONBOOK 0->1 命中 Q2 分支②）
##   c68d10c1 docs(D902复议): 首源复议裁定落账（Q3 选(c) 等）——本轮任务书依据
## _Game diff（须空）——实测输出：
git diff --name-only -- "Valley Rampart/Assets/_Game"
（无输出行）
## GameScene.unity —— 只登记 hash（L-99）：
git status --short -- "Valley Rampart/Assets/Scenes/GameScene.unity"
 M "Valley Rampart/Assets/Scenes/GameScene.unity"
开跑前 hash ＝ 4a86f26f6a7c2aab3c440903ddfa80020ddec9a3
收工后 hash ＝ 4a86f26f6a7c2aab3c440903ddfa80020ddec9a3（一致 ⇒ 本轮播放未再写脏；仍为挂账 O-14 载体）
（⛔ 未执行 git checkout -- / restore / 任何整文件回滚）
```

- ✅ 零生产码／资产／政策值改动；交付件 `…_dispatch.{cs,json,txt}` 与统计器落于 `Valley Rampart/Logs/`（`.gitignore` 第 7 行忽略，实测 `git check-ignore` 命中）⇒ 提交只含本报告 1 文件。
- ✅ **A/B 两段落盘分开**（同一 txt 内以 `## ⭐[段 A …]`／`## ⭐[段 B …]` 分段；JSON 分栏 `anchor/mig_rows/wrap`）；每次迁移**即时刷盘**（⛔ 非只记首尾）。
- ✅ 落盘自报值全为实测（`Time.time` 差／`Stopwatch`）；格式串仅 `"0.0"`／`"0.00"`（⛔ 无 `"0.1"`）；**⛔ 未用 `n × timeScale`、未引短窗倍率**。
- ✅ `.py` 落文件执行（⛔ 未内联）；统计器**先空跑**后实跑；正则锚 `^## S `＋字段全名（⛔ 无 `\S+`）。
- ✅ 进局走正门 `TestHarnessApi.EnterTestRun`／`ExitTestRun`；收工退 Play（`playMode=stopped` 实测）；编辑器收工态 `isCompiling=false`、console `unreadCount=0`。

## 八、自报瑕疵

1. **域声明形制变更**：本轮探针 txt **未内嵌 `== DOMAIN` 行**，域表改由统计器 `DOMAINS`（源码枚举名单）承担并输出零计数项——与上轮 txt 内嵌形制不同，如实列报（含本次 `hh341dispatch` 探针首次编译失败：`ReferenceEquals` 未全限定 ⇒ 已改 `object.ReferenceEquals` 后重跑，两次调用记录在案）。
2. `…_dispatch_stat.txt` 含 6 条「⚠ 最终有效数≠行数」提示——均为**子集字段**（`MIG.ended.task`／`BLD.*`／`CHEST.*`／`CSS.*`／`COMP.parent`／`WGS.detail`）的预期提示，⛔ 非计数错误。
3. B 段为**单帧时点**快照（派发帧 65.94）；`css=0` 是该时点事实，⛔ 不外推至全程（全程证据仅 73.4 游戏秒窗）。
4. `SRC.kingdom=0` 计 0 仅代表**注册面**为 0；⛔ 未读玩家王国建筑全量（含非 Active／未注册），⛔ 不据此写「玩家王国无建筑」。
5. `Assigned` 帧级未观测（同帧瞬态）与 `Abandoned` 状态不落 map——均如实列入 §六，未替改成"未发生"。
6. `t_warmup_start` 打在 `EnterTestRun` 前（沿用先例）⇒ 暖机 43.6 游戏秒含建局（0.33）。
7. 折算栏仅录 `timeScale=15.0`（⛔ 未重算任何窗口倍率；`L-98` 已解除「待复核」，本批不涉折算结论）。

## 九、【请裁】（3 条）

1. **「未注册」成因是否追加取证**：B 段已发现 `ConstructionSiteStore` **未进入候选范围**（css=0／24）＋已列**全量候选源清单 24 行**（含类型/王国/可用/在册）＋附「全量建筑 `BLD.awaiting=False`=20/20、`kingdom=0` 建筑源=0」⇒ 降级口径①登记「首源未进入候选范围」。请裁：**是否准下一轮追加「玩家王国建筑全量（含非 Active/未注册）＋投料态判定链」专项**以坐实「承载对象尚未出现」的成因，还是以本批登记收口？（⛔ 本端不自行开做）
2. **「已支持」采信口径**：A 段 5 项闭合**全部满足**（⇒ 依裁定原句达「可由『弱支持』提升为『已支持』」条件）；但链止于 `Abandon(reason=Timeout)`、**未达 `Working`**。请裁：采信是否受「未达 Working」影响？（本端按裁定原句记 5 项闭合＋链现状原样，⛔ 不自行升降档）
3. **同帧多派工口径**：定位时点＝52.84 游戏秒心跳仍 0/4、**65.94 游戏秒 npc20＋npc21 同帧被派**（本轮按首个迁移定锚点＝20，21 仅登记）。请裁：下一轮是否需**同时记录同帧全部派工链**（多锚点），还是维持单锚点＋余者登记？

## 十、状态

⛔ **首源源级＝未通过 · 停手待裁**（`D902 Q5` 维持；四项门槛不变）。本轮为**只读取证**：A 段 5 项闭合（供采信）／B 段「首源未进入候选范围」（降级口径①）／无 `ConstructionSiteStore` 样本。⛔ 未申请下一源 · ⛔ 未并 `DZ-7` · ⛔ 未取新号 · ⛔ 未代写策划端账本。