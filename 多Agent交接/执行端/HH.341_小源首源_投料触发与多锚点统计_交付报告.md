# HH.341 · 小源首源 —— **`D903` 复议 `Q1`＋`Q3`＋`Q4`** 投料触发 × 候选集复测 ＋ 多锚点派工统计 交付报告

> 执行端 ｜ 2026-09-27 ｜ **A 段：一次授权投料触发＋候选集复测；B 段：多锚点派工＋到达率独立统计（只读轮询）** ｜ ⛔ 零生产码／资产／政策值 ｜ ⛔ 未 push ｜ 挂 `D903`（⛔ 未取新号）
> 依据：`D903` 复议裁定（台账 **§二百一十五**）｜ `L-97`（含 `D897` 补条）｜ `L-98`（已解除「待复核」＋ `D902 Q6` 补条）｜ `L-99`
> 基线：**开工实测 `HEAD = 9c52d35c` ＝ 收工实测 `9c52d35c`**（无漂移；相对本端上轮交付 `d1290da4`，其间 **2 笔非本端 docs commit**：`5dc08792`（D903 核实）、`9c52d35c`（D903 复议落账）——如实列报）
> ⛔ 未触碰 `Valley Rampart/Assets/Scenes/GameScene.unity`（`L-99`：**只登记 hash**，⛔ 未还原／未 `checkout`／未 `restore`）｜ ⛔ 未调任何源侧方法｜ ⛔ 未触发 `_protocolRuntime` 懒初始化（仅反射读字段）｜ ⛔ 未硬编码工人 id（按条件重定位＝**19/20/21/22**）｜ ⛔ 源选择取证本轮不做（`D903 Q5` 挂起）｜ ⛔ 不并 `DZ-7`／未申请下一源／未代写策划端账本（`D767`）

**一句话结论**：A 段 = **投料确认不成立**（目标建筑本体取到（玩家 `castle`@(8.32,66.24)，`state=Abandoned`），但其 **`def.cost(items_0)` 为空 ⇒ `BeginMaterialPhase` 走「无料可搬」分支**（`awaiting` 未翻转／`siteStore` 仍 `null`））⇒ **三分支之③：两种成因继续保持未区分**；B 段 = 帧级可追踪派工事件 **9**（19×4／22×5），**到达 `Working` = 0/9**，日志侧池内派发 **138**（含 `npc20` **128 次亚帧 `Unreachable` 循环**——帧级不可见，单列列报）。

---

## 一、`L-97` 三件套 ＋ `Q6` 观察窗声明（7 项）

### ① 观测范围与三数（基数＝`## S` 采样行）

| 行类型 | N | 行号 |
|---|---|---|
| `BLD_ROW`（触发前/后建筑全量） | **63** | 11–106 |
| `SRC_ROW`（触发后源快照 ×2） | **48** | 61–133 |
| `POOL_ROW` | **4** | 138–141 |
| `POLL_ROW`（心跳） | **7** | 142–163 |
| `MIG_ROW`（逐次迁移） | **17** | 143–165 |
| `EV_ROW`（事件表） | **9** | 168–176 |

**非采样行**：段标记＝5｜汇总＝47｜其他＝2（不计入最终有效数）。**正则**：锚 `^## S <行类型> <字段全名>`；值捕获 `([^\s|}]*)`；⛔ 无 `\S+`。**统计器先空跑**（空输入全 0）→ `…_trigger_stat_dry.txt`；实跑 → `…_trigger_stat.txt`（全量 67 字段三数在 §③；下方为核心摘录，均为「原始 → 排除 → 最终」）：

| 字段（核心摘录） | 原始 → 排除 → 最终 | 分母 | 域（含零计数项·摘） |
|---|---|---|---|
| `BLD.phase`／`BLD.kingdom`／`BLD.def`／`BLD.state`／`BLD.awaiting`／`BLD.inSources`／`BLD.siteStore` | 63 → 0 → **63** | 63 | phase `pre=21｜post=21｜post2=21`；kingdom `1=18｜2=18｜3=18｜-1=6｜0=3`；def `castle=12｜House=9｜Warehouse=9｜Well=9｜farm=9｜mine=9｜VagrantCamp=6`；state `Active=60｜Abandoned=3`（其余 0）；awaiting **`False=63｜True=0`**；inSources `True=60｜False=3` |
| `SRC.phase`／`SRC.type`／`SRC.kingdom`／`SRC.valid`／`SRC.inFlight` | 48 → 0 → **48** | 48 | phase `postA=24｜post2=24`；type `Building=40｜MineByproductComponent=6｜ChestEntity=2`（**`ConstructionSiteStore=0｜WorldGatherSource=0｜Transport=0`**）；kingdom `1=14｜2=14｜3=14｜-1=6｜0=0`；valid `True=48｜False=0`；inFlight `True=7｜False=41` |
| `CSS.awaiting`／`CSS.pickupRecon`／`CSS.card` 等 | 0 → 0 → **0** | **0** | 结构性空（`css=0`） |
| `POOL.npcId`／`POOL.occupation`／`POOL.wState0`／`POOL.onBook0` | 4 → 0 → **4** | 4 | npcId `19/20/21/22`（各 1）；occupation `Worker=4`；wState0 `None=3｜MovingToSource=1`；onBook0 `False=3｜True=1` |
| `POLL.t`／`POLL.events`／`POLL.active`／`POLL.poolOnBookN`／`POLL.cssSrc` | 7 → 0 → **7** | 7 | cssSrc **`0=7`**（全程 0） |
| `MIG.ev`／`MIG.role`／`MIG.t`／`MIG.npcId`／`MIG.wState.from`／`MIG.wState.to`／`MIG.task.type`／`MIG.source.GetType()`／`MIG.mapEntry`／`MIG.卡ID` | 17 → 0 → **17** | 17 | role `start=9｜end=8`（`mid=0`）；wState `None=9｜MovingToSource=8`（from）／`MovingToSource=9｜None=8`（to）；task.type `Transport=9｜<无task>=8`（**`Build=0`**）；source `ChestEntity=9｜<无task>=8`（**`ConstructionSiteStore=0`**）；mapEntry `有=9｜无=8`；卡ID `<未取得:协议运行时未初始化>=17` |
| `EV.*`（16 字段） | 9 → 0 → **9** | 9 | 见 §三 |

**证据链**：`Valley Rampart/Logs/hh341_small_construction_site_trigger.txt`（行号见表）；辅助原始日志 `…_trigger.logs.txt`（1560 行筛选自桥接 `capture_logs`）；统计器 `…_trigger_stat.py`（`FIELDS`＋`DOMAINS`＋`scan_field()`＋`log_table()`）。**复现命令**：

```powershell
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_trigger_stat.py" --out "Valley Rampart\Logs\hh341_small_construction_site_trigger_stat_dry.txt" "Valley Rampart\Logs\hh341_small_construction_site_trigger_stat_dry_in.txt"
pwsh -NoProfile -File "Valley Rampart\Logs\hh341_small_construction_site_trigger_run.ps1" -CsName "hh341_small_construction_site_trigger.cs" -RequestId "hh341trigrt2" -TimeoutSec 300
pwsh -NoProfile -File "Logs\_bridge_call.ps1" -Payload "Valley Rampart\Logs\hh341_small_construction_site_trigger_stop.json" -TimeoutMs 60000
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_trigger_stat.py" --out "Valley Rampart\Logs\hh341_small_construction_site_trigger_stat.txt" --logs "Valley Rampart\Logs\hh341_small_construction_site_trigger.logs.txt" --pool 19,20,21,22 "Valley Rampart\Logs\hh341_small_construction_site_trigger.txt"
```

### ② `Q6` 7 项（实测；原样见 txt 第 186–192 行）

| # | 项 | 实测值 |
|---|---|---|
| ① | 暖机起止及游戏秒 | `36.68`（建局前）→ `37.02`（投料触发）＝ **0.3 游戏秒**（含建局） |
| ② | 轮询起止及游戏秒 | txt 原样 `37.02 → 277.40 ＝ 240.4`；⚠️ **精度注**：该 `t_start` 为视窗定义起点（`t_enter`），**B 段逐帧采样实际自 A 段 post2 后 ≈46.8 起** ⇒ 逐帧覆盖 ≈**230.6 游戏秒**（A 段为点采样 37.02／46.51）——见 §七-1 |
| ③ | 触发或任务零点 | 投料触发 `t=37.02`（确认=**False**）；B 段首个派工事件 `t0=80.08` |
| ④ | 观测窗（B 段）起止及游戏秒 | `37.02 → 277.40 ＝ 240.4`（＝触发起 240 游戏秒窗；同上精度注） |
| ⑤ | 墙钟与折算关系 | 墙钟＝**16.65 s**（`Stopwatch`）；折算常数＝**`timeScale=15.0`**（⛔ 未引短窗倍率；⛔ 禁 `n × timeScale`） |
| ⑥ | 收工条件 | 声明＝「B 段窗（触发起 240 游戏秒）或会话安全阀（`t_warmup_start` 起 480 游戏秒）」；**实际触发＝`window240`** |
| ⑦ | 是否在任务段内 | **是**（窗内存在派工事件段） |

---

## 二、A 段：投料触发 × 候选集复测（5 项原样 ＋ 三分支判定）

### ① 触发前后「建造源对象数量」（`_sources` 里 `Building` 源计数）

```
== A_COUNT_SRC phase=pre    ok=True buildingSrc=20 cssSrc=0 totalSrc=24
== A_COUNT_SRC phase=postA  buildingSrc=20 cssSrc=0 totalSrc=24
== A_COUNT_SRC phase=post2  buildingSrc=20 cssSrc=0 totalSrc=24
```

### ② `BLD.awaiting`（全量建筑逐条，含 `BLD.def`；63 行原样见 txt 第 11–106 行）

- pre：`== A_COUNT phase=pre buildings=21 kingdom0=1 awaiting=0`；post／post2 同（`awaiting=0`）。
- **玩家王国（kingdom=0）仅 1 座建筑**：`castle`／`state=Abandoned`／`awaiting=False`／`inSources=False`／`siteStore=null`／`pos=8.32,66.24`（⭐ 与箱源同格）；AI 三国 18 座＋`VagrantCamp` 2 座全 `Active`（原样行见 §② 表域）。
- ⭐ 关键单条（pre，原样）：`## S BLD_ROW BLD.phase=pre BLD.kingdom=0 BLD.def=castle BLD.state=Abandoned BLD.awaiting=False BLD.inSources=False BLD.siteStore=null BLD.pos=8.32,66.24`

### ③ `source.GetType()` 全量分布（触发后快照；含零计数项与分母）

`SRC.type`（分母＝48＝postA 24＋post2 24）：`Building=40｜MineByproductComponent=6｜ChestEntity=2`；**`ConstructionSiteStore=0｜WorldGatherSource=0｜Transport=0`**。`A_SRC_SUM` 原样：`phase=postA sources=24 valid=24 css=0 chest=1 bld=20 other=3`；`phase=post2` 同。

### ④ ⭐ 投料触发是否被确认（三者至少两项）

```
== A_TARGET found=True def=castle pos=8.32,66.24 选取口径=BuildingRegistry kingdomId==0 优先 castle
== A_TRIGGER call=BeginMaterialPhase target=castle 参数=def.cost(items_0) 结果=ok(无异常)
== A_CONFIRM awaiting.before=False awaiting.after=False siteStore.before=null siteStore.after=null
```

**判定：投料确认不成立（0/3 正向）** —— ①调用成功（无异常）但 ②`IsSiteAwaitingMaterials` **未翻转**（False→False）③`_siteStore` **仍 null**。**成因（只读源码引用）**：本例参数 `def.cost` 为空（`items_0`）⇒ `SiteNeedOf(cost).IsZero` ⇒ `Building.BeginMaterialPhase`（`Building.cs:528-543`）走「无料可搬」分支（`_awaitingMaterials=false; UnregisterSiteStore(); return;`）⇒ 不建工地仓、不注册源。⚠️ 该建筑同时为 `state=Abandoned`（原样列入 ②）。

### ⑤ 任务候选集是否出现 `ConstructionSiteStore`

**否**：`cssSrc=0`（pre/postA/post2 三时点一致）；`SRC.type` 中 CSS = 0（分母 48）；`CSS.*` 字段分母 0。原样：`== A_VERDICT_INPUT 触发确认=False awaiting翻转=False→False css进候选(postA)=False css进候选(post2)=False`。

### ⭐⭐ 三分支判定（照 `D903 Q1` 原文逐条）

| 分支 | 条件 | 本轮判定 |
|---|---|---|
| ① | 投料确认后 `BLD.awaiting=True` 且出现 `ConstructionSiteStore` ⇒ 支持「原先无投料需求」 | ⛔ **不成立**（awaiting 未翻转；CSS=0） |
| ② | 投料已确认但对象仍不出现 ⇒ 支持「未注册」候选 | ⛔ **不成立**（投料未确认） |
| ③ | **投料确认本身缺失** ⇒ 两种成因继续保持未区分（⛔ 不得择一） | ✅ **成立并采纳** |

⇒ 本轮**不得**在「未注册／无投料需求」间择一；⛔ 未判定为任何缺陷；⛔ 本端未自行改触发对象或配方源（见 §八【请裁】1）。

---

## 三、B 段：多锚点派工（6 项原样）＋ 到达率独立统计（7 项）

### （1）多锚点事件表（`EV_ROW` 9 行**原样**；txt 第 168–176 行）

```
## S EV_ROW EV.no=1 EV.npcId=19 EV.tStart=80.08 EV.tEnd=110.85 EV.durGame=30.77 EV.task.type=Transport EV.source.GetType()=ChestEntity EV.reachedWorking=0 EV.state.seq=None>MovingToSource>None EV.terminal=回未在册 EV.srcPos=8.32,66.24 EV.pos.start=3.84,43.20 EV.pos.end=2.24,49.92 EV.path.len=9.58 EV.srcDist.start=23.47 EV.srcDist.end=17.42
## S EV_ROW EV.no=2 EV.npcId=22 EV.tStart=89.06 EV.tEnd=120.02 EV.durGame=30.96 EV.task.type=Transport EV.source.GetType()=ChestEntity EV.reachedWorking=0 EV.state.seq=None>MovingToSource>None EV.terminal=回未在册 EV.srcPos=8.32,66.24 EV.pos.start=0.80,48.08 EV.pos.end=8.48,53.52 EV.path.len=10.03 EV.srcDist.start=19.66 EV.srcDist.end=12.72
## S EV_ROW EV.no=3 EV.npcId=19 EV.tStart=121.58 EV.tEnd=152.23 EV.durGame=30.65 EV.task.type=Transport EV.source.GetType()=ChestEntity EV.reachedWorking=0 EV.state.seq=None>MovingToSource>None EV.terminal=回未在册 EV.srcPos=8.32,66.24 EV.pos.start=4.48,51.04 EV.pos.end=7.84,59.92 EV.path.len=14.18 EV.srcDist.start=15.68 EV.srcDist.end=6.34
## S EV_ROW EV.no=4 EV.npcId=22 EV.tStart=121.58 EV.tEnd=152.23 EV.durGame=30.65 EV.task.type=Transport EV.source.GetType()=ChestEntity EV.reachedWorking=0 EV.state.seq=None>MovingToSource>None EV.terminal=回未在册 EV.srcPos=8.32,66.24 EV.pos.start=8.48,53.68 EV.pos.end=8.32,64.00 EV.path.len=13.62 EV.srcDist.start=12.56 EV.srcDist.end=2.24
## S EV_ROW EV.no=5 EV.npcId=22 EV.tStart=195.31 EV.tEnd=225.55 EV.durGame=30.24 EV.task.type=Transport EV.source.GetType()=ChestEntity EV.reachedWorking=0 EV.state.seq=None>MovingToSource>None EV.terminal=回未在册 EV.srcPos=8.32,66.24 EV.pos.start=8.32,65.60 EV.pos.end=8.32,65.76 EV.path.len=0.32 EV.srcDist.start=0.64 EV.srcDist.end=0.48
## S EV_ROW EV.no=6 EV.npcId=19 EV.tStart=199.91 EV.tEnd=229.92 EV.durGame=30.01 EV.task.type=Transport EV.source.GetType()=ChestEntity EV.reachedWorking=0 EV.state.seq=None>MovingToSource>None EV.terminal=回未在册 EV.srcPos=8.32,66.24 EV.pos.start=8.32,65.76 EV.pos.end=8.32,65.76 EV.path.len=0.00 EV.srcDist.start=0.48 EV.srcDist.end=0.48
## S EV_ROW EV.no=7 EV.npcId=22 EV.tStart=227.53 EV.tEnd=258.51 EV.durGame=30.98 EV.task.type=Transport EV.source.GetType()=ChestEntity EV.reachedWorking=0 EV.state.seq=None>MovingToSource>None EV.terminal=回未在册 EV.srcPos=8.32,66.24 EV.pos.start=8.32,65.76 EV.pos.end=8.32,65.76 EV.path.len=0.00 EV.srcDist.start=0.48 EV.srcDist.end=0.48
## S EV_ROW EV.no=8 EV.npcId=19 EV.tStart=247.70 EV.tEnd=277.40 EV.durGame=29.70 EV.task.type=Transport EV.source.GetType()=ChestEntity EV.reachedWorking=0 EV.state.seq=None>MovingToSource>（窗到未终态） EV.terminal=未终态（窗到） EV.srcPos=8.32,66.24 EV.pos.start=8.32,65.76 EV.pos.end=<窗到仍在途> EV.path.len=0.00 EV.srcDist.start=0.48 EV.srcDist.end=-1.00
## S EV_ROW EV.no=9 EV.npcId=22 EV.tStart=260.83 EV.tEnd=277.40 EV.durGame=16.57 EV.task.type=Transport EV.source.GetType()=ChestEntity EV.reachedWorking=0 EV.state.seq=None>MovingToSource>（窗到未终态） EV.terminal=未终态（窗到） EV.srcPos=8.32,66.24 EV.pos.start=8.32,65.60 EV.pos.end=<窗到仍在途> EV.path.len=0.32 EV.srcDist.start=0.64 EV.srcDist.end=-1.00
```

每事件 6 项对照：①工人 ID→`EV.npcId` ②状态迁移（逐次）→`EV.state.seq` ＋ `MIG_ROW` 17 行逐次（`MIG.ev` 关联）③任务类型与来源→`EV.task.type`／`EV.source.GetType()` ④在册条目→`MIG.mapEntry`（各事件 start 行 `有`、end 行 `无`）⑤起止时间→`EV.tStart/tEnd/durGame` ⑥终态与终止原因→`EV.terminal` ＋ **日志行**（见下表）。

### （2）独立观察线 7 项（⛔ 与首源归因解耦；⛔ 只统计不定性）

| # | 项 | 实测（两种计数口径**都写**，分母标明） |
|---|---|---|
| ① | 派工总数 | **帧级可追踪＝9**（19×4／22×5；分母＝派工事件）／**日志侧池内派发行＝138**（19×4｜**20×128**｜21×0｜22×6；分母＝日志行） |
| ② | 到达 `Working` 数 | **0／9**（帧级；日志侧池内 `完成`＝**0**） |
| ③ | `Abandon(Timeout)` 数 | 日志侧：npc19 **3**｜npc22 **5**（合计 8）；帧级对应「回未在册」8 行（2 行未终态为窗到截断） |
| ④ | 其他终态数 | 日志侧：npc20 **`Unreachable` 128**（帧级不可见，见 ①）；池内 `完成`＝0 |
| ⑤ | 派工至终态的游戏秒 | 帧级 9 事件：合计 **260.53**／均值 **28.95**；⚠️ 含 2 个窗到截断（#8 29.70／#9 16.57）⇒ **已终态 7 事件：合计 214.26／均值 30.61** |
| ⑥ | 任务来源类型 | 帧级：`ChestEntity=9`（全部 `@(8.32,66.24)` 同箱）；日志侧池内派发行同为 `Transport@(8.32,66.24)` |
| ⑦ | 位置推进与距离变化 | 逐事件 `EV.pos.start/pos.end/srcDist.start/srcDist.end/path.len`（原样见上表）；汇总：`path.len` 合计 **48.05**／均值 **5.34**（逐帧位移累加，采样口径）。⭐ 观测事实（⛔ 不定性）：EV1–4 有明显推进（path 9.58–14.18，源距 23.47→17.42／15.68→6.34／12.56→2.24）；**EV5–7 起止几乎零位移**（path 0.32／0.00／0.00），**源距 0.48–0.64 且维持至 30 秒超时**（参考：`D894` 给定到达阈值 ≈`0.3×1.28=0.384`，本轮未重读） |

⭐ 日志侧同批原样两行（池内最贴近判定的一对）：

```
[Log] [TaskScheduler] 派发 Transport 任务 → npcId 19 @ (8.32, 66.24)（优先级 B）
[Log] [TaskScheduler] Abandon Transport → npcId 19 reason=Timeout
```

⚠️ **两个计数口径的差额已闭合**：138（日志）− 9（帧级）＝ **129** ＝ ①`npc20` **128 次亚帧循环**（每次 `派发→Unreachable` 在同一帧内完成 ⇒ 帧边界不可见）＋ ②`npc22` 1 次「窗内已在进行中」事件（起点早于逐帧采样，仅捕获其终止行 `MIG.no=1`）。npc19 日志 4＝帧级 4 ✓；npc22 日志 6＝帧级 5＋partial 1 ✓；npc21 两侧均 0 ✓。

---

## 四、已排（本轮 ⛔ 不主张）

- ⛔ 未在「未注册／无投料需求」间择一（三分支③）；⛔ 未把「投料确认不成立」写成缺陷或机制结论。
- ⛔ 未把 B 段 ⑦ 的位置/时间事实写成「移速/帧量化是 Timeout 根因」（裁定：仍为线索）；⛔ 未把 `Unreachable` 循环写成根因。
- ⛔ 未调任何源侧方法；⛔ 未触发 `_protocolRuntime` 懒初始化（全链 `MIG.卡ID` 均「未初始化」）；⛔ 未触发第二个投料对象（⛔ 未自行改目标王国）。
- ⛔ 未改暖机/任务参数/判据；⛔ 未碰 `GameScene.unity`；⛔ 未取新号／未申请下一源／未并 `DZ-7`。

## 五、未取得（`L-97` 三件套）

| # | 未取得项 | ① 观测范围（N） | ② 筛选条件（被排除取值＋计数） | ③ 全量取值域＋计数（分母） |
|---|---|---|---|---|
| 1 | **投料确认（同 `A` 段④）** | BLD_ROW N=63（21 建筑×3 时点）；触发 1 次 | 目标＝`kingdomId==0`∧优先 `castle`（候选中仅 1 座）；参数＝`def.cost`（`items_0`） | `BLD.awaiting` `True=0`（分母 63）；`BLD.siteStore` `非null=0`；**已试路径＝本建筑本体＋其 `def.cost` 配方**（⛔ 未自行改对象/改配方源） |
| 2 | **`ConstructionSiteStore` 进候选** | SRC_ROW N=48（postA/post2 各 24） | 无被排除取值（枚举域含 CSS；计数 0） | `SRC.type` `ConstructionSiteStore=0`（分母 48）；`CSS.*` 分母 0 |
| 3 | **卡 ID（工人/源侧）** | MIG_ROW N=17；EV_ROW N=9 | 反射读 `_protocolRuntime` 字段（null ⇒ 未初始化）；⛔ 未调 getter | `MIG.卡ID` `<未取得:协议运行时未初始化>=17`（分母 17）；`工配:`／`源卡:`／`<无配对>`＝0 |
| 4 | **`npc20` 亚帧循环的逐事件时长/位置** | 日志侧 128 派发＋128 `Unreachable`（分母＝日志行）；**帧级采样不可见（0 行）** | 帧级轮询每帧一次；亚帧（同帧内完成）⇒ 无可捕获帧 | 帧级 `EV.npcId` 域 `19=4｜22=5｜20=0｜21=0`；⇒ 需下一轮专项（见 §八-2） |
| 5 | **到达 `Working`（≥1 例）** | EV_ROW N=9＋心跳 7 行（≈230.6 游戏秒逐帧覆盖） | 锚点池＝19/20/21/22；`Working` 未出现 | `EV.reachedWorking` `1=0`（分母 9）；`MIG.wState.to` `Working=0` |

---

## 六、合规声明（实测输出）

```
## git rev-parse HEAD（开工=收工，实测一致）
9c52d35c8d4a6ffb220a7066db738d1107ea555d
## 漂移列报：相对本端上轮交付 d1290da4，其间 2 笔非本端 docs commit：
##   5dc08792 docs(D903): Q3(c) 派工迁移记录+源选择取证 交付核实
##   9c52d35c docs(D903复议): 首源复议裁定落账（本轮任务书依据）
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

- ✅ 零生产码／资产／政策值改动；交付件 `…_trigger.{cs,json,txt}` 等落于 `Valley Rampart/Logs/`（`.gitignore` 第 7 行忽略，`git check-ignore` 命中）⇒ 提交只含本报告 1 文件。
- ✅ **A/B 分段落盘** ＋ **每次迁移即时刷盘**（`Flush()` 在每 MIG/EV/心跳/快照行后调用）；⛔ 非只记首尾。
- ✅ 落盘自报值全为实测（`Time.time` 差／`Stopwatch`）；格式串仅 `"0.0"`／`"0.00"`（⛔ 无 `"0.1"`）；**⛔ 未用 `n × timeScale`、未引短窗倍率**。
- ✅ `.py` 落文件执行；统计器**先空跑**后实跑（实跑含 `--logs` 派生表）；正则锚 `^## S `＋字段全名（⛔ 无 `\S+`）。
- ✅ 进局走正门 `TestHarnessApi.EnterTestRun`／`ExitTestRun`；收工退 Play（`playMode=stopped` 实测）；编辑器 `isCompiling=false`、console `unreadCount=0`。

## 七、自报瑕疵

1. **`Q6` ②/④ 的 `t_start` 标注精度**：txt 原样标的是视窗定义起点（`t_enter`／`t_trigger`＝37.02），但 B 段**逐帧采样实际自 A 段 post2 后 ≈46.8 起**（A 段为两个点采样 37.02／46.51）⇒ 逐帧覆盖 ≈230.6 游戏秒。⛔ 未回写 txt，按原样＋本注列报。
2. **`npc20` 亚帧循环未被帧级捕获**（日志 128 次 vs 帧级 0）——已单列于 §三-①④与 §五-4，⛔ 未替改成「未发生」；两口径差额 129 已逐项闭合。
3. **⑤ 均值含 2 个窗到截断**（#8/#9 未达终态即收工）——另给已终态 7 事件的均值 30.61。
4. **B 段起点前已在进行中的事件**（npc22 首个，起点未捕获，仅存终止行 `MIG.no=1`）——⛔ 未计入 9 分母。
5. **`EV.pos.end` 对窗到事件为 `<窗到仍在途>`**（未取值，非 0）。
6. **投料触发参数取 `def.cost`**（建筑本体施工配方）；该建筑配方为空 ⇒ 走「无料」分支。⛔ 未自行改用其他配方源（如升级费）或改目标王国。
7. 本轮探针首次编译失败 2 次（`ReferenceEquals` 未全限定；`BuildingRegistry.HasInstance` 不存在 ⇒ 改反射 `_instance` 字段），均在执行记录中，重跑后通过。
8. **域声明形制**：域表内嵌于统计器（`DOMAINS`），txt 未内嵌 `== DOMAIN` 行——沿用上轮形制说明，如实列报。

## 八、【请裁】（3 条）

1. **触发目标/配方源是否切换**：本轮**玩家王国建筑本体取到了**（`castle`@(8.32,66.24)，`state=Abandoned`），但其 **`def.cost` 为空 ⇒ 投料确认不成立**（分支③）。请裁：是否准下一轮改对「**recipe（`def.cost` 或 `levels[].upgradeCost`）非空的任意王国建筑**」触发并**联注王国 id**（以验证「投料 ⇒ CSS 进候选」链本身）？（⛔ 本轮未自行改对象）
2. **`npc20` 亚帧 `Unreachable` 循环是否立项**：日志侧 128 次 `派发→Unreachable`（帧级 0 可见；同箱同源）。请裁：是否准下一轮以「**日志行为事件主计数**＋亚帧事件专项（只读）」推进 B 线计数口径？（⛔ 本端不定性其成因）
3. **到达失败点（距源 <1 单位停滞）是否专项**：EV5–7 观测到源距 **0.48–0.64**、`path.len≈0`、维持至 30 秒超时（EV4 曾达 2.24）。请裁：是否准把「**到达失败点**」列为下一轮只读专项（如读 `PathFollower` 状态／目标格可走性，沿用 `D894` 口径）？（⛔ 本线不定性；⛔ 不写「移速/帧量化根因」）

## 九、状态

⛔ **首源源级＝未通过 · 停手待裁**（`D903 Q6` 维持；四项门槛不变）。本轮：A 段三分支③（投料确认不成立 ⇒ 两成因未区分）；B 段帧级 9 事件／日志 138 派发、到达 `Working`＝0、终态多为 `Timeout(30s)` 与 `Unreachable`。⛔ 未申请下一源 · ⛔ 未并 `DZ-7` · ⛔ 未取新号 · ⛔ 未代写策划端账本。