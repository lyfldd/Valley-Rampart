# HH.341 · 小源首源 —— **`D905` 复议裁定落地**（`Q2` 玩家王国主线 ＋ `Q1` 一次只读补证 ＋ `Q5` 源选择取证 ＋ `Q3` `PF_ROW` 配对专项 ＋ `Q4` 亚帧抓取）交付报告

> 执行端 ｜ 2026-09-27 ｜ 单会话 ｜ ⛔ 零生产码／资产／政策值 ｜ ⛔ 未 push ｜ 挂 `D905`
> 依据：`D905` 复议裁定（台账 **§二百一十九**）｜`D905` 核实（**§二百一十八**，含加注）｜`L-97`（＋补条①／补条②／**补条③本轮起强制**）｜`L-98`（＋补条②）｜`L-99`
> ⛔ 未碰 `GameScene.unity`（只登记 hash）｜⛔ 未 `checkout --`／未 `restore`｜⛔ 不改 `taskTimeout`／`Complete`｜⛔ 不申请下一源／不并 `DZ-7`／未取新号／未代写策划端账本（`D767`）｜⛔ 未在报告或落盘串写「判绿／判红」｜⛔ 未宣称「2 格差已解释」
> ⭐ **唯一写动作**：`Q5` 取证前置「对 AI 建筑调 `BeginMaterialPhase` 一次」（与 `D904` 同型；⛔ 未改 `state`／配方／政策值；触发前后对照已落盘）——核定未明文授权亦未禁止，**列【请裁】1 追认**。

**五件事完成状态**：`Q2` ✅（判定＝**未取得**）｜`Q1` ✅（7 字段同一行）｜`Q5` ✅（**首源在册 363.07 游戏秒零派工** ＋ 4 条前置逐条）｜`Q3` ✅（**div=0**，邻近区零覆盖）｜`Q4` ✅（日志 347／事件 811／帧级 21 窗 526 帧两栏分列）——无未做项。

---

## 1. 开工基线（实测）

- `HEAD`：开工＝收工＝ **`0935f3b16a66b1b24475154c52822b12f270df91`**（无漂移；相对本端上轮 `fe791ba6`，其间 **2 笔非本端 docs**：`64d06491` D904 复议核实、`0935f3b1` D905 复议裁定落账——如实列报）
- `Assets/_Game` diff＝**空**（实测输出见 §附）
- `GameScene.unity`：`git hash-object` 开跑前＝收工后＝ **`4a86f26f6a7c2aab3c440903ddfa80020ddec9a3`**（逐位一致；仍为挂账 `O-14` 载体 ⇒ 只登记）
- ⛔ **未执行任何回滚类命令**（无 `checkout --`／无 `restore`；`L-99` 连续第 4 轮遵守）
- ⭐ **两跑与归档**：run1（首跑，`Q4` 帧窗因 `Abandon→ClearNpc` 移除 `_npcBrainMap` 项导致数据全缺）已归档为 `…_d905_run1_archive.{txt,json,cs.runtime_resp.txt,logs.txt}`＋`…_run1_archive_stat.txt`；**正式证据＝run2（canonical 文件名）**，run1 的 `Q2`／`Q5`／`Q3` 读数与 run2 同型（一致性旁证）。

## 2. `Q2` 玩家王国判定（**第一优先**）

**Q5 标签**：口径＝三口径分列｜行集谓词＝`BLD_ROW` 全量／`Q2_ROW`（`kingdomId==0`）｜分母＝21／1｜三数＝原始 21 → 排除 0 → 有效 21（`BLD_ROW` N=21）；`Q2_ROW` 原始 1 → 排除 0 → 有效 1｜非采样行＝段标记／汇总行（`…_d905_stat.txt` §①）｜同字段多口径＝**是**（本表即分列）。

| 口径 | 读数 |
|---|---|
| **玩家王国全量**（`kingdomId==0`） | **1 座**：`castle`／`Abandoned`／世界 `(8.32,66.24)`／格 `(110,97)`／`inSources=False`／`siteStore=null`／`awaiting=False`／**`def.cost` 空（0 项）**／**`levels[0].upgradeCost` 无**（原样见 `Q2_ROW`，`…_d905.txt` **L31**） |
| **全世界全量**（`BuildingRegistry.All`） | **21 座**：`Active=20｜Abandoned=1`；def `castle=4｜House=3｜farm=3｜Well=3｜mine=3｜Warehouse=3｜VagrantCamp=2`；kingdom `1=6｜2=6｜3=6｜0=1｜-1=2` |
| **本次筛选集**（配方非空） | **12 座**＝k1/k2/k3 各 `House／farm／Well／Warehouse`（`Q5_CANDS n=12`） |

**判定结果（照裁办四项）**：
1. ⭐ **记录「玩家王国可投料承载者未取得」**（独立小节，本行即之）：`Q2_VERDICT` 原样——`玩家王国（kingdomId==0）可投料承载者（配方非空 ∧ 状态可施工·口径A）=<未取得：玩家王国可投料承载者未取得>（对象数=1）`（`…_d905.txt` **L35**）
2. ⛔ 未以 AI 建筑结果替代「玩家王国承载者」结论（AI 结果仅作**控制样本**，见下）
3. ⛔ 未强行修改建筑状态或配方（本会话对玩家王国 0 次写动作）
4. **首源继续保持未通过**（§10）

**判据与依据（如实列出）**：
- **「配方非空」硬判据**＝`def.cost` 项数 >0 **或** `levels[level-1].upgradeCost` 项数 >0（读取路径见探针 `Q2_ROW` 行；玩家王国 castle 两项均为 0 ⇒ `recipeNonEmpty=False`）。
- **「状态可触发投料」双口径并列**（裁定原文说「非 `Abandoned`／`Ruined` 等不可施工态（⚠️ 请如实列出你用的具体判据与依据）」——如实列两口径）：
  - 口径A（本端采用）：`state ∈ {Active, Abandoned, Ruined, Constructing}` ⇒ castle `constructA=True`。依据：枚举注释 `Building.cs:6-14`（`Abandoned`＝「可修复但不产出」、`Ruined`＝「修复=同建造」）＋ 重建入口 `Building.cs:514-521`（`Ruined` 可进投料流程）＋ 修复入口 `BuildingPanel.cs:83-85`（`Abandoned` 有修复按钮）。
  - 口径B（裁定原文表述）：`state ∉ {Abandoned, Ruined}` ⇒ castle `constructB=False`。
  - ⚠️ **两口径对结论无影响**：castle 因「配方非空」必要条件未满足而不 eligible（`eligibleA=False`）。
- **AI 控制样本（显式标注）**：`k1 Warehouse@19.20,49.92`（`def.cost=Gold:4+Stone:4`）→ `D904` 轮「触发后 `awaiting F→T`、`siteStore null→非null`、postA `css=1`⇒三合一成立」——**口径来源＝HH.341 D904 轮 `Q1_SEL/Q1_TRIGGER/Q1_OBS` 原样落盘**；⛔ 仅作控制样本、⛔ 不替代玩家王国结论。

## 3. `Q1` 补证行（7 字段同一行 ＋ 逐字段来源）

**Q5 标签**：口径＝玩家王国全量 ∪ 格命中（点集）｜谓词＝`kingdomId==0` ∨ 格∈{(108,97),(110,97)} ∨ 世界(108,97)±5｜分母＝1｜三数＝原始 1→排除 0→有效 1｜非采样行同上｜多口径＝**是**（同对象双坐标口径并列，见下行）。

**原样一行**（`…_d905.txt` **L39**，字段来源随行标注）：

| 字段 | 读数 | 来源（file:line） |
|---|---|---|
| ① `Building.coord` | **`108,97`** | `Building.cs:83`（`public GridCoord coord; // footprint 左上格（2D）`） |
| ② `def.footprint` | **`3,3`** | `BuildingDef.cs:27`（另附对象字段 `Building.footprint=3,3`，`Building.cs:84`） |
| ③ 世界位置 | **`8.32,66.24`** | `transform.position` |
| ④ `WorldToCell(world,cellSize)` | **`110,97`** | `GridSystem.cs:228`（静态） |
| ⑤ `WorldToSubCoord(world)` 微格 | **`440,388`**（÷4＝`110,97`） | `GridSystem.cs:264`（实例） |
| ⑥ `def`／`kingdomId` | `castle`／`0` | `Building.def`／`Building.kingdomId`（`Building.cs:128`） |
| ⑦ 唯一对象标识 | `SaveId=Building_869916110c1040a18022c3a730d749aa`｜`instId=-938292` | `Building.cs:60`／`GetInstanceID()` |

⭐ **声明**：同一行一次性给全（⛔ 未分散多表）；本轮**只声称「同对象」**（同 `seed` ＋ 玩家王国**唯一** castle ⇒ 不依赖坐标即可判同对象）；⛔ **未宣称「2 格差已解释」**（现状＝两口径具名：`coord=108,97` vs `WorldToCell=110,97`；`footprint 3×3` 只能解释 ≤1 格）。
⭐ 补充：`saveId` 为每局新分配 GUID（run1＝`Building_5a6d…`、run2＝`Building_8699…`，跨局不同属预期）；而 `coord`／`cell`／`world`／`footprint` **两跑逐位一致**（口径读数稳定复现）。

## 4. `Q5` 源选择取证（4 组读数 ＋ 4 条前置逐条）

**Q5 标签**：口径＝全量派发（所有 NPC）｜行集谓词＝`Q5_DISPATCH`（新在册事件）／`SRC_ROW phase=q5dispN`（快照）｜分母＝97 派发／25 源（每快照）｜三数＝见 `…_d905_stat.txt` §④（`Q5_DISPATCH` 原始 97→0→97；`SRC_ROW` N=2425＝97×25）｜非采样行＝段标记／汇总｜多口径＝是（池内/全量/筛选分列）。
⭐ **分域声明**：候选**按王国分域取**（`TaskScheduler.cs:340-353` 池隔离；`SourceKingdom`＝`TaskScheduler.cs:1550-1562`）——已作为**排除原因**之一记录（下方②）。

**前置触发（唯一写动作，触发前后对照）**：`Q5_SEL`（**L54**）`kingdomId=1 def=Warehouse pos=19.20,49.92 state=Active inSources=True siteStore=null 配方字段=def.cost 物料项=Gold:4+Stone:4 触发前css=0`｜`Q5_TRIGGER`（**L55**）`结果=ok awaiting False→True siteStore null→非null`｜`Q5_CSSWAIT`（**L56**）`cssSeen=True cssNow=1`。

**① 是否同时进入候选集（各自计数与分母）**：**是**——97 张快照全部：`css=1/25`、`chest=1/25`（同快照并存；分母 25＝`SRC_ROW` 行集 2425/97，亦见 `Q5_OBS sources=25`）。

**② 各自可用状态、需求状态、排除原因（判据＋来源）**：
| 源 | 可用状态 | 需求/内容状态 | 排除原因（可复核判据＋来源） |
|---|---|---|---|
| `CSS` | `valid=True`（`ConstructionSiteStore.cs:184`）｜`terminal=False`｜`failAtt=0`｜`lastW=0`｜`card=<null>` | `awaiting=True`｜`satisfied=False`｜`remaining=4`｜**`SourcePos=0.00,0.00`（97/97，取料仓从未解析）** | 候选排除候选（仅列证据，⛔ 不定性）：①**分域**＝`SourceKingdom=1`（Component→parent Building，`TaskScheduler.cs:1556-1560`）⇒ k0/它国工人**不可领**；②**广告前置未满足**＝取料仓解析 `FindPickup==null ⇒ 不广告`（`ConstructionSiteStore.cs:189-204`）与 `pos=0.00,0.00`（从未成功过）一致；⚠️「本 tick 广告成功与否」**未取得**（不调源侧方法，见 §7） |
| `ChestEntity` | `valid=True`（`ChestEntity.cs:119`） | `empty=False`（有货，`Store.TotalCount>0`） | 被选中 31 次；分域＝`-1`（无主源·先到先得，`TaskScheduler.cs:1553`） |

**③ 实际选中的来源类型**（`Q5_DISPATCH` sel 域，分母 97）：**`ConstructionSiteStore=0`**｜`ChestEntity=31`｜`Building=43`｜`MineByproductComponent=23`｜`WorldGatherSource=0`。
**④ 工人 ID、任务类型、卡 ID**：npcId 集合＝`1,2,3,4,5,6,8,9,10,11,12,14,15,18,19,20,21,22,28`（含池 19–22）；task 域＝`Transport=68｜Production=26｜WaterHaul=3`；**卡 ID＝97/97 `<未取得:协议运行时未初始化>`**（读 `_protocolRuntime` **字段**＝null；⛔ 未触发懒初始化；按裁定登记「未取得」）。

⭐⭐ **4 条前置逐条判定（缺一不得认定「源选择异常」）**：
- ① `CSS` 在候选集内 ✅（`css=1/25` 且 `valid=True`）。
- ② `CSS` 当时具备可选资格 ⚠️ **未取得**（⛔ 不调 `TryAdvertiseTask` ⇒ 无法复核「本 tick 广告成功」；可复核替代证据＝`SourcePos=0.00,0.00` 从未解析取料仓、`lastW=0`、`failAtt=0`）。
- ③ 实际选择为 `ChestEntity` ✅（31 次选中 CHEST、0 次 CSS）。
- ④ `CSS` 被排除原因可复核，或不存在更高优先级规则 ⚠️ **部分**（分域/去重/容量三规则可复核来源见②；但「本 tick 是否广告成功」未取得）。
⇒ **4 条未全部满足 ⇒ ⛔ 不进入「源选择异常」候选、不认定「选择错误」**。
⭐ **首源未见派工**：`cssDispatched=0`（分母＝97 全量派发）；`CSS` 在册窗口＝**363.07 游戏秒**（`Q5_NOTE`，**L3739**）——期间 CSS 恒在册（末帧 `css=1`）且恒 `valid/awaiting`，CHEST 同期被选 31 次。

## 5. `Q3` 两到达判据配对（证据源＝`PAIR_ROW`，逐事件两列表）

**Q5 标签**：口径＝池内（`kingdomId==0 ∧ Worker`，本会话池＝**19/20/21/22**）｜行集谓词＝`PAIR_ROW`（PF 状态变化 ∪ 在册且 `worldDist≤1.5` 每 0.5s 节流）｜分母＝284｜三数＝原始 284→排除 133（两侧不同时有效）→有效 151｜非采样行同上｜多口径＝是（PF 侧/调度器侧同表分列）。

| 侧 | 必记字段 | 读数（两侧同刻有效行 N=151） |
|---|---|---|
| `PathFollower` | 目标／当前位置／`GridDist`／`IsArrived` | `PF.dest=8.32,65.92`（吸附点）｜`PF.pos` 与 `PF.rbPos` 同值（样例 `-0.64,40.48`）｜`PF.gridDist=40.36`（口径复刻 `UnitController.cs:1324-1331`）｜**`PF.isArrived=False=151／True=0`** |
| 调度器 | `SourcePos`／世界距离／`ArrivalThreshold` | `SCH.srcPos=8.32,66.24`（箱源位）｜`worldDist min=18.88 max=28.72`｜`SCH.thr=0.38`（＝`0.3×1.28`，`TaskScheduler.cs:1564-1569`）｜**`SCH.arrived=False=151／True=0`** |
| 公共 | `PF.State`／`_wpIndex`／`waypoints.Length`／`wp0`／`wState`／任务来源／终态 | `PF.state` 域 `Following=273｜Repathing=11`；样例（**L556**）`npc19 wState=MovingToSource srcType=ChestEntity wpIndex=1 wpLen=28 wp0=(x_251,_y_255,_layer_0) pStat=Partial`；终态＝`<待日志裁定>`＋日志核对（见下） |

⭐ **是否登记「两判据分歧」：未登记**。理由（须给相反结论证据）：151 行两侧结论**全部同向**（双方均 `False`），无一行出现相反到达结论；且 **邻近区（`worldDist≤1.5`）行 N=0、`≤0.5` 行 N=0** ⇒ 该区域**未观测到**（「未观测到」≠「不存在」）。
⭐ **池内任务链（逐次，日志核对）**：`t=34.02` 池内 4 人同时领 `ChestEntity/Transport`（`MIG_ROW` L555/557/559/561）→ `t=66.30` 4 人同时出册（L691/693/695/697）＝**`reason=Timeout`**（日志离线核对：npc19/20/21/22 全 `Timeout`；起止差 32.28 游戏秒 ≳ `taskTimeout=30.00`，超时分支 `TaskScheduler.cs:548`）——后续重派循环同型。
⭐ 附带观察（⛔ 不定性、⛔ 未写成移速/帧量化根因）：待机段 npc19 `wpIndex 1→2` 间隔 12.10 游戏秒且该窗位置未变（原样行 L556→L567）。

## 6. `Q4` 亚帧抓取（**日志计数／帧级计数两栏分列**）

**Q5 标签**：口径＝①日志侧（全量·行集＝`[TaskScheduler]` 日志行）②事件侧（`PathFailedEvent`）③帧级（帧窗）——**三栏分列、⛔ 不合并**｜帧级分母＝`Q4FRAME_ROW` N=526｜三数＝窗口 21（原始 21→合并 0→有效 21）｜非采样行同上｜多口径＝**是**。

**① 计数（分栏）**：
| 栏 | 计数 | 明细 |
|---|---|---|
| 日志侧（离线全量） | **`Unreachable=347`** | `npc3=67｜npc9=58｜npc13=55｜npc16=55｜npc17=55｜npc15=45｜npc7=10｜npc31=2`（**8 个 npc**）；同窗 `Abandon` 总 424（`Timeout=77`）、派发 443、完成 7 |
| 日志侧（探针运行期 `logMessageReceived`） | **347**（与离线一致） | `Q4_BYLOG`（**L3743**） |
| 事件侧（`PathFailedEvent`） | **811** | `Q4_BYEVT`（**L3744**）；⛔ 不与日志侧合并 |
| 帧级（窗口） | **21 窗／526 帧** | `ch=log 14 窗／ch=evt 7 窗`；合并 0 |

**② 6 组记录**（win=1 原样 **L409**；`Q4TERM` 原样 L1206）：
1. **事件主计数**：`Q4F.win=1 Q4F.ch=log`（窗口号＋通道；开窗行 `Q4_WIN` L408 带 `evTask=Production evDest=4.96,40.88`）。
2. **当前帧及连续帧状态**：连续 30 帧逐帧（`Q4F.k=0..29`，`Q4F.t/frame`）；`npc9` 30 帧 `Q4F.pos=8.32,39.36`。
3. **`_wpIndex`·`fails`·路径状态**：`wpIndex=0 fails=3`（＝`maxConsecutiveFails`）｜`wpLen=<null> pStat=<null>`（路径已清）｜**`pfState=Failed`**（526 帧域：`Failed=399｜Idle=102｜Following=25`）。
4. **目标格可走性**：`destSub=271,240 destSubWalk=True`；`evDestSub=271,240 evDestSubWalk=True`（`evDestSubWalk` 域：`True=477`／未取得 49——见 §8-1）。
5. **在册条目·任务类型·来源类型**：`onBook=False`（**526 帧全 False**——`Abandon` 即时移除，`TaskScheduler.cs:764-772`）｜`evTask` 域：日志通道 `Transport=231｜Production=107`（338 行全解析）、事件通道 `<未取得:事件侧无任务类型>=188`。
6. **终态与耗时**：`Q4TERM … wState=None onBook=False taskType=<无>`；`elapsed=<未取得>`（`_taskStartTime` 随 `ClearNpc` 清除，`TaskScheduler.cs:770`——结构性）。
⭐ **机理注记（随交付上报，仅供裁）**：事件后 `PF` 停在 `Failed`（`fails=3`）＋路径清除＋任务即时移除；事件目的地微格**全部可行走**（477/477 有值行 `True`）。

## 7. 口径落实声明

**行集来源**：每张统计表头均已标（本报告各节「Q5 标签」行＋`…_d905_stat.txt` 各段 `head()` 行集来源）；⛔ **同一小节未混列不同 `## S` 行型**（`SRC_ROW` 小节只含 `SRC_ROW`；`PAIR_ROW` 小节只含 `PAIR_ROW`；`Q4FRAME_ROW` 小节只含 `Q4FRAME_ROW`）。

**`L-97` 三件套（未取得项）**：
| # | 未取得项 | ① 观测范围（基数 N） | ② 筛选条件（被排除取值＋计数） | ③ 全量取值域＋计数（分母） |
|---|---|---|---|---|
| 1 | **「本 tick CSS 广告成功与否」** | `Q5` 上下文 97 次派发帧快照 | 限制＝不调源侧方法（`TryAdvertiseTask` 有 `_pickupPos` 写副作用）；⛔ 无被排除取值 | 替代可复核证据域：`CSS.SourcePos=0.00,0.00×97`｜`lastW=0×97`｜`failAtt=0×97`（分母 97）⇒ 直接读数**不可得** |
| 2 | **`Q5_PRE` 分母** | 97 行 `Q5_PRE` | 探针取数 `<未取得>`（`HashSet<T>` 无泛型 `ICollection` 转换） | 替代分母＝`SRC_ROW` 行集 2425/97＝**25**（与 `Q5_OBS sources=25` 一致）⇒ 有效分母 25（分母 97 为派发数） |
| 3 | **`Q4` 帧窗 `elapsed`** | 526 帧行 | `_taskStartTime` 随 `ClearNpc` 清除（`TaskScheduler.cs:770`）⇒ 结构性 | 526 行全 `<未取得>`（分母 526） |
| 4 | **`Q4` evt 通道任务类型** | evt 通道 188 帧行 | 事件结构无任务类型字段 ⇒ 结构性 | 188 行全 `<未取得:事件侧无任务类型>`（日志通道 338 行全解析成功）（分母 526） |

**`L-98` 观察窗 7 项（原样，`…_d905.txt` L3747-3753）**：①暖机 t=0.75→1.09（含建局）游戏秒 0.3｜②轮询 t=1.09→364.16 游戏秒 363.1（`Q2/Q1` 点采样、逐帧自触发起）｜③触发零点 t=1.09（确认=True，cssSeen 同刻）｜④观测窗 t=1.09→364.16＝**360.0 游戏秒窗**｜⑤墙钟 25.35 s、折算常数 `timeScale=15.0`（⛔ 不引短窗倍率、⛔ 禁 `n×timeScale`）｜⑥收工条件＝360 秒窗 或 会话安全阀 540 秒；实际＝`window360`｜⑦在任务段内＝是（窗内派发：池内 23、**全量 97**）。
**其他**：格式串仅 `"0.0"`／`"0.00"`；每次读数即时刷盘（含每行 Flush）；`git rev-parse HEAD` 开工＝收工各实测一次。

## 8. 自报瑕疵

1. **`Q4F.evDest` 语义＝「按 npcId 最近一次事件侧目的地」**（跨窗保留，非本窗专属）⇒ `ch=log` 的窗口可引用该 npc 先前的 evt 目的地（477 行有值／49 行未取得）；判读须结合 `Q4F.ch`。⛔ 未回改数据。
2. **本轮含一次写动作**（`Q5` 前置触发，与 `D904` 同型）：裁定未明文授权亦未禁止；若无此动作「`Q5` 判据①恒不成立」（挂起原因同 `D903`）⇒ 列【请裁】1。手法＝反射调 `BeginMaterialPhase(need)`，⛔ 未改 `state`／配方／政策值；触发前后对照落盘（L54-55）。
3. **run1 首跑 `Q4` 帧窗全失效**（`Abandon→ClearNpc` 移除 `_npcBrainMap` ⇒ 查不到单位）：已按纪律**重跑前归档**，修复（全场景扫描回退）后重跑；正式证据＝run2。
4. **`Q5_PRE` 分母 `<未取得>`**（见 §7-2）；`Q4` 帧窗 `elapsed`／evt 任务类型为结构性未取得（§7-3/4）。
5. **统计器 v1 正则遗漏**（`SRC.extra=` 前缀字段如 `CSS.awaiting` 未被捕获为「缺」）：已修正并重跑（v2），run1 统计产物留档为 v1。
6. **`Q5` 卡 ID 全量未取得**（`_protocolRuntime` 字段＝null）：按裁定登记、⛔ 未触发懒初始化。
7. **`Q3` 邻近区零覆盖**：`worldDist≤1.5` 行 N=0 ⇒ 「停滞区配对」与「两判据分歧」均**未观测到**（「未观测到」≠「不存在」）；本专项结论以 151 行同向样本为限。
8. 探针首次编译未失败（run1 一次通过）；两跑均 `playMode=stopped` 收尾（stop 载荷实测）。

## 9. 【请裁】（5 条）

1. **`Q5` 前置触发是否追认**：本轮为满足 `Q5` 判据①（「挂起条件已满足」＝`CSS` 须在场）复用 `D904` 同型手法对 AI 建筑触发一次投料（⛔ 未改 `state`／配方／政策值）。若否 ⇒ `Q5` 判据①恒不成立；若准 ⇒ 是否将该手法固定为 `Q5` 取证的标准前置？
2. **「本 tick 广告成功与否」复核方式**：是否准下一轮**调用** `CSS.TryAdvertiseTask`（会写 `_pickupPos`，属源侧写副作用）或提供不动源侧方法的替代判据（如新增只读查询口）？
3. **`Q3` 邻近区覆盖路径**：本轮池内未进入邻近区（N=0）⇒ 是否（a）保持现口径继续待到邻近区样本，或（b）把 `Q4` 式帧窗机制扩展到池内邻近区（事件驱动抓取）？——(b) 属新机制，需裁。
4. **`Q4` 是否升级「事件前环形缓冲」**：现机制只能取事件后连续帧（事件后 `PF=Failed`＋任务已清）；若要拿「失败前最后状态」，需常驻环形缓冲（行为面外的新机制）⇒ 是否准立？
5. **`Q4F.evDest` 跨窗语义处置**：采信为「按 npcId 最近一次事件目的地」（§8-1 如实标注），还是要求下一轮改为窗口隔离（每窗快照后清）？

## 10. 源级结论

⛔ **维持 `ConstructionSiteStore` 未通过 · 停手待裁**（`D905` 口径；⛔ 未写「判绿/判红」字样）。
本轮新增事实：`Q2` 玩家王国可投料承载者**未取得**（castle 配方空，`eligibleA=False`）｜`Q1` 双口径具名落盘（`coord=108,97` vs `WorldToCell=110,97`，⛔ 未宣称 2 格差已解释）｜`Q5` 触发成立后 **CSS 在册 363.07 游戏秒、候选集恒在（1/25）、恒 `valid`，全量 97 次派发 0 次选中 CSS；4 条前置未全满足 ⇒ 不认定「源选择异常」**｜`Q3` 两侧同刻有效 151 行为**同向**（`div=0`），邻近区零覆盖 ⇒ **未登记「两判据分歧」**｜`Q4` 三栏分列（日志 347＝8 npc／事件 811／帧级 21 窗 526 帧），事件后 `PF=Failed(fails=3)`＋路径清＋任务即时移除＋事件目的地微格全可行走。
⛔ 未申请下一源 · ⛔ 未并 `DZ-7` · ⛔ 未取新号 · ⛔ 未代写策划端账本。

---

## 附：合规声明（实测输出）与交付件

```
git rev-parse HEAD（开工=收工）        0935f3b16a66b1b24475154c52822b12f270df91
git diff --name-only -- Valley Rampart/Assets/_Game     （空）
git hash-object GameScene.unity（开跑前=收工后）        4a86f26f6a7c2aab3c440903ddfa80020ddec9a3
回滚类命令                              未执行（checkout -- / restore 均无）
探针 run2（正式）                        success=true elapsed_ms=33257 墙钟=25.35s
探针 run1（归档）                        success=true elapsed_ms=34577（Q4 帧窗缺数据 ⇒ 已归档重跑）
playMode 收尾                            stopped（stop 载荷实测）
```

**交付件（`Valley Rampart/Logs/`，均命中 `.gitignore`）**：`hh341_small_construction_site_d905.{cs,json,txt}`＋`…_d905_run.ps1`＋`…_d905_state.json`／`…_d905_stop.json`＋`…_d905.cs.runtime_resp.txt`＋`…_d905.logs.txt`＋`…_d905_stat.py → …_d905_stat.txt`（空跑 `…_stat_dry.txt`）＋run1 归档 5 件（`…_d905_run1_archive.*`）。
（`HH.341` 报告文件＝本文件；⛔ 未 push。）