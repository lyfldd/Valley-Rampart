# HH.341 · 小源首源 —— **`D904` 复议裁定落地**（`Q4` 建筑构成核查 → `Q1` 非空配方触发 → `Q2`/`Q3` 只读专项 → `Q6` 登记）交付报告

> 执行端 ｜ 2026-09-27 ｜ 单会话只读 ＋ **一次授权投料触发** ｜ ⛔ 零生产码／资产／政策值 ｜ ⛔ 未 push ｜ 挂 `D904`
> 依据：`D904` 复议裁定（台账 **§二百一十七**）｜`D904` 核实（**§二百一十六**）｜`L-97`（＋补条①＋**补条②本轮新立**）｜`L-98`（＋补条②）｜`L-99`
> ⛔ 未碰 `GameScene.unity`（只登记 hash）｜⛔ 未 `checkout --`／未 `restore`｜⛔ 不改 `taskTimeout`／`Complete`｜⛔ 不申请下一源／不并 `DZ-7`／未取新号／未代写策划端账本（`D767`）｜⛔ 未在「未注册／无投料需求／源选择错误」间择一定性｜⛔ 未写「移速/帧量化根因」、未把 `Unreachable` 写成 `CSS` 专属｜⛔ 未写「判绿/判红」

**五件事完成状态**：`Q4` ✅ ｜ `Q1` ✅（**三合一成立**）｜ `Q2` ✅ ｜ `Q3` ✅（**9 快照**）｜ `Q6` ✅（只登记）——**无未做项**。

---

## 1. 开工基线（实测）

- `HEAD`：开工＝收工＝ **`d7823702ae253e833c52892888a1515ec032cdbe`**（无漂移；相对本端上轮 `0143d4c9`，其间 **2 笔非本端 docs**：`aae5f66f` D904 核实、`d7823702` D904 复议落账——如实列报）
- `Assets/_Game` diff＝**空**（实测输出见 §附）
- `GameScene.unity`：`git hash-object` 开跑前＝收工后＝ **`4a86f26f6a7c2aab3c440903ddfa80020ddec9a3`**（逐位一致；仍为挂账 `O-14` 载体）
- ⛔ **未执行任何回滚类命令**（无 `checkout --`／无 `restore`）

## 2. `Q4` 三口径读数（⛔ 三份分开）

**Q5 标签**：口径＝三口径分列（下三行）｜行集谓词＝`BLD_ROW` 全行（BuildingRegistry.All 无筛选 / ＋`kingdomId==0` / ＋`(costItems>0 ∨ upCostItems>0)`）｜分母＝21｜三数＝原始 21 → 排除 0 → 最终 21（`BLD_ROW` N=21）｜非采样行＝段标记 6／汇总 31／其他 1（`…_d904_stat.txt` §②）｜同字段多口径＝**是**（本表即三口径分列）。

| 口径 | 读数 |
|---|---|
| **玩家王国全量**（kingdomId==0） | **1 座**：`castle`／`Abandoned`／`(8.32,66.24)`／格 `(110,97)`／`inSources=False`／`siteStore=null`／`level=1`／`awaiting=False`／**`def.cost` 空（items 0）**／`levels[0].upgradeCost`＝`<无该级upgradeCost>`（原样见 `…_d904.txt` 第 34 行） |
| **全世界全量**（BuildingRegistry.All） | **21 座**：`Active=20｜Abandoned=1`；def 分布 `castle=4｜House=3｜farm=3｜Well=3｜mine=3｜Warehouse=3｜VagrantCamp=2`；王国分布 `k1=6｜k2=6｜k3=6｜k0=1｜k-1=2`（21 行原样见 txt 16–36 行） |
| **本次筛选集**（配方非空） | **12 座**＝k1/k2/k3 各 `House／farm／Well／Warehouse`（`castle`／`mine`／`VagrantCamp` 恒空配方 ⇒ 未入集）；12 行 `CAND_ROW` 原样见 txt 40–51 行 |

**王国清单（`KING_ROW` N=4）**：`0 hh341r2`｜`1 密林王国`｜`2 霜岩国`｜`3 石城国`。

⭐ **冲突作用域解释**（`(108,97)` vs 上一轮「唯一 castle」）：
- 上一轮「唯一 `castle` `Abandoned` @(8.32,66.24)」的读数作用域＝**建筑本体表 `BuildingRegistry.All`**（无注册面过滤、无筛选条件）——本轮同口径复测**一致**（k0＝1 座，`inSources=False`）。
- 本轮实测该 castle **世界 `(8.32,66.24)` ⇔ 格 `(110,97)`**（两条换算一致：`WorldToCell=(110,97)`；微格 `(440,388)/4=(110,97)`）。
- 与早期「主城 @**(108,97)**」相差 **2 格（x）** ⇒ **强候选解释＝同一对象、坐标口径/锚点换算差异**（非另一座主城）；⚠️ 本端**未逐条核对早期轮原文** ⇒ 列【请裁】1（⛔ 不自行改期）。
- ⛔ 依裁定：未把唯一 castle 当作主城或「玩家王国完整建筑世界」（三口径已分列）。

## 3. `Q1` 切换至非空配方建筑（4 项选取 ＋ 4 项观察 ＋ 三合一）

**Q5 标签**：口径＝筛选集（配方非空 12 座）｜谓词＝`kingdomId ∈ {1,2,3} ∧ def.cost 非空 ∪ levels[level-1].upgradeCost 非空`｜分母＝12｜三数＝原始 12 → 排除 0 → 最终 12｜非采样行同上｜多口径＝`CAND_ROW`/`SRC_ROW` 两张表并存（分列）。

**① 选取 4 项（原样 `Q1_SEL`）**：`kingdomId=1`｜`def=Warehouse`／`pos=19.20,49.92`／（格 `93,63`）／`state=Active`／`level=1`｜`inSources=True`／`siteStore=null`｜**配方字段＝`def.cost`**，物料项＝**`Gold:4+Stone:4`**（备选 `levels[0].upgradeCost=Stone:10+Wood:10`；确定序＝kingdom,pos，取 `def.cost` 优先）。

**② 触发（一次）**：反射调 `Building.BeginMaterialPhase(def.cost)` ⇒ `结果=ok(无异常)`；**前置值**：`awaiting=False`／`siteStore=null`（＋`inSources=True`）。

**③ 触发后 4 项观察（原样 Q1_OBS/Q1_TRIGGER）**：
1. `BLD.awaiting` **翻转**：`False → True` ✅
2. `siteStore` **创建**：`null → 非null` ✅
3. **`ConstructionSiteStore` 进入 `_sources`**：postA `css=1`（`sources=22`＝bld20＋chest1＋css1）✅；post2 同（`sources=25`，其间 3 组 `MineByproductComponent` 入册）
4. **候选集 `source.GetType()` 全量分布（含零计数）**：postA（分母 22）`Building=20｜ChestEntity=1｜ConstructionSiteStore=1｜WorldGatherSource=0｜MineByproductComponent=0｜Transport=0`；post2（分母 25）`Building=20｜MineByproductComponent=3｜ChestEntity=1｜ConstructionSiteStore=1`（SRC_ROW N=47 原样见 txt 55–102 行）

⭐ **成立条件（三合一）：非空配方 ✅ ＋ 触发已确认 ✅ ＋ 候选集出现 `CSS` ✅ ⇒ 「投料 → 注册链」成立**（⛔ 未直接判定具体根因；本轮**不适用**「注册链缺口」登记）。

⚠️ **自报（见 §8-1）**：JSON `cssPre=1` 为**触发后即时读数**（脚本测量点晚于触发）——真·触发前 CSS 计数未单独落盘；确认链改以「目标 `siteStore` null→非null ＋ postA `css=1`」为据。

## 4. `Q2` 两套计数（⚠️ 128 次仅为事件证据；⛔ 不判根因／⛔ 非 CSS 专属）

**Q5 标签**：口径＝**①全量日志（计数单位=日志行）与 ②池内帧级（计数单位=采样行）分列、⛔ 不混用**｜谓词①＝`[TaskScheduler]` 派发/Abandon/完成 行｜谓词②＝池 `kingdomId==0 ∧ Worker`（本会话池＝**51/52/53/54**）｜分母＝各表注明｜三数＝见 `…_d904_stat.txt` §③（MIG_ROW N=66／PF_ROW N=833／POLL_ROW N=88）｜非采样行＝段标记 6／汇总 31／其他 1｜多口径＝**是**。

**① 日志行为事件主计数（离线复算）**——
- **上一会话（`…_trigger.logs.txt`，复核）**：派发 **786**（`Transport 655｜Production 128｜WaterHaul 3`）｜`Abandon` **685**（`Timeout 59｜Unreachable 626`）｜完成 **87**。**`Unreachable` 626**＝`npc9 175`（@`10.88,38.72`×164）｜`npc15 167`（@`-53.12,38.08`×158）｜`npc3 156`（@`24.32,49.28`×89）｜`npc20 128`（@`8.32,66.24`×128）⇒ **与事务端补充数字逐项一致（复核通过）**。
- **本会话（`…_d904.logs.txt`）**：派发 **1137**（`Transport 992｜Production 144｜WaterHaul 1`）｜`Abandon` **1059**（`Unreachable 938｜Timeout 104｜BrainLost 11｜SourceInvalid 4｜DestFull 2`）｜完成 **78**。`Unreachable` 938＝`npc47 226`｜`npc35 223`｜`npc41 199`｜`npc44 144`｜`npc43 144`｜`npc63 2`（分属 3 个不同目标位置）。
⇒ ⭐ **跨会话对照：`Unreachable` 全族分布随局变化（4↔6 个 npcId、目标不同），但恒为「跨源多目标＋AI 工人」；本会话池（51–54）`Unreachable=0`**（池内该现象本会话未出现）。

**② 帧级亚帧记录（本会话原样）**：`POLL_ROW` 88 行（心跳每 15 游戏秒 ×4 人：在册／`wState`／在册条目／任务源／目标／PF 快照）＋`PF_ROW` 833 行（PF `State`／`_wpIndex`／`fails`／`dest`／`wpLen`／`wp0`／`pStatus`）＋`MIG_ROW` 66 行（逐次迁移含 PF 快照）。⚠️ **`npc20` 的 128 次（上一会话）只能作为事件证据，⛔ 不等同 128 次完整状态机循环**；⛔ 本专项未判根因。

## 5. `Q3` 到达失败点 6 组读数（**9 快照**）＋ `EV5–7` 复核

**Q5 标签**：口径＝池内帧级（Q3SNAP_ROW）｜谓词＝池内且源距 ≤1.5 且帧位移 <0.01 累计 ≥5 游戏秒｜分母＝9｜三数＝原始 9 → 排除 0 → 最终 9（行 922–1049）｜非采样行同上｜多口径＝是（与日志侧 `Timeout` 计数分列）。

**6 组读数（快照原样摘，逐行见 txt 922/932/959/988/989/990/1047/1048/1049）**：
1. **`PathFollower` 状态/目标/位置**：三种形态并存——`s_Idle_wp_0_f_0_dest_8.32,65.92_wpLen_null_pStat_null`（**Idle、无路径**）｜`s_Following_wp_10→17_f_0_dest_8.32,65.92_wpLen_26_pStat_Ready`（**慢跟随，wp 逐格推进**）｜`s_Repathing_wp_0_f_0_wpLen_20_pStat_Partial`（**Repathing × Partial 路径**）
2. **`GridDist` 与世界距离（两者都写）**：世界 `0.43–0.48`｜格距 `0.64–0.75`
3. **目标格可走性**：`subCoord=(440,388)` → `IsSubWalkable=False`、`IsWalkableSub=False`；`cellCoord=(110,97)` → `IsWalkable=False`（=箱源本体格）
4. **`waypoints.Length／wp0／_wpIndex`**：`26／(413,366,layer0)／10–17`（慢跟随者）｜`20／(245,235)／0`（Repathing·Partial）｜`<null>／<null>／0`（Idle）
5. **调度器阈值与超时（源码实读）**：`arrivalThreshold=0.38`（＝`AttentionTuningConfig.arrivalThreshold(0.3，AttentionTuningConfig.cs:105) × cellSize.x(1.28)`；计算式＝`TaskScheduler.cs:1564-1569`）｜`cellSize=1.28`（`GridSystem.config.cellSize` 实测）｜`taskTimeout=30.00`（`TaskScheduler.cs:45`）｜超时分支 `TaskScheduler.cs:548／:672`｜快照时 `taskElapsed=16.23–…`、`stallSecs=5.00`
6. **`wState`／任务来源／终态原因**：`wState=MovingToSource`｜源 `ChestEntity`（@`8.32,66.24`）｜**终态原因＝以日志行为据**（本会话池 4 人 `Timeout` 计数：`51×8｜52×8｜53×8｜54×7`）

⭐ **`EV5–7` 复核（上一会话实例，原样值）**：`EV5 path.len=0.32、源距 0.64→0.48`；`EV6 0.00、0.48→0.48`；`EV7 0.00、0.48→0.48`，**维持至 30 秒超时**——本会话**同型在池内复现**（9 快照：`pos (8.32,65.76)／(8.16,65.84)` vs 源 `(8.32,66.24)`；世界距 `0.43–0.48`；静止 5 游戏秒触发快照后持续至 `Timeout`）。
⚠️ **观察事实（⛔ 不定性）**：停滞点距源 `0.43–0.48` ＞ 调度器阈值 `0.38`；PF 自身目标（吸附点）`dest=(8.32,65.92)` 与源位相差 `0.32` ⇒ **「PF 判据（吸附点）× 调度器判据（源位）」两口径并存**（沿用 `D894` 线索，⛔ 本端**未**写成「移速/帧量化根因」）。

## 6. `Q6` `Unreachable` 全族登记（**只登记 · 并入既有登记项 · ⛔ 不另立编号 · ⛔ 不并 `DZ-7`**）

**可直接并入的登记文本（供策划端/事务端）**：
> 既有「跨源通用候选缺陷」登记项 · 追加证据（`D904` 轮）：`reason=Unreachable` 跨 **2 会话**复核——上一会话全量 **626**（`npc9 175 @10.88,38.72｜npc15 167 @-53.12,38.08｜npc3 156 @24.32,49.28｜npc20 128 @8.32,66.24`）；本会话全量 **938**（`npc47 226｜npc35 223｜npc41 199｜npc44 144｜npc43 144｜npc63 2`）。理由：①全量计数大 ②涉及 `Transport`／`Production`／`WaterHaul` ③涉及**多个目标位置**且**跨会话分布变化** ④池外/池内均出现**完成事件**（本会话完成 78；上一会话 87）⇒ 支持「**非 `CSS` 专属**」，但**不决定具体机制**。沿用 `D896` 候选方向：核查 **`Repathing`（本会话已取到 `s_Repathing_…pStat_Partial` 原样行）／失败计数（`fails` 全程 0）／重寻／终态转换** 是否**跨源一致**。

## 7. `L-97` 三件套（未取得项）＋ `Q5` 六标签落实声明

**未取得项（每项按固定格式）**：
| # | 未取得项 | ① 观测范围（基数 N） | ② 筛选条件（被排除取值＋计数） | ③ 全量取值域＋计数（分母） |
|---|---|---|---|---|
| 1 | **真·触发前 `CSS` 计数** | Q1 段点采样（触发前后各 1 次；`SRC_ROW` N=47 均为触发后） | 目标 `siteStore` 前置 `null`；⛔ 无被排除取值 | `css`：触发后 postA `=1`／post2 `=1`；**触发前值未单独落盘**（`cssPre` 字段为触发后即时值——见 §8-1） |
| 2 | **`npc20` 亚帧循环的逐事件位置/时长** | 上一会话帧级 `MIG/POLL/PF`（该会话帧级未采 PF 行）＋日志 128 行 | 帧级轮询每帧 1 次；亚帧（同帧完成）⇒ 无捕获帧 | 日志侧 `npc20 派发=128／Abandon=128`（分母=行集）；帧级 `=0` ⇒ **结构性不可得**（本会话已以 833 行 PF 行补帧级能力，池内未出现该现象） |
| 3 | **早期轮「主城 @(108,97)」原文口径** | —（**范围外**，非本轮 5 件事） | — | 本轮点检：世界 `(108,97)±5` 命中 `<无>`；格 `(108,97)` 命中 `<无>`；实测同对象格＝`(110,97)`（差 2 格）⇒ 列【请裁】1 |

**`Q5` 六标签**：已在 §2／§3／§4／§5 每张统计表头逐表标注（口径／行集谓词／分母／原始→排除→有效／非采样行分类／同字段多口径）；⛔ 全程**未混用池内与全量**（两口径一律分列标注）。

## 8. 自报瑕疵

1. **`cssPre` 字段误标**：脚本测量点晚于触发 ⇒ JSON `cssPre=1` 实为**触发后即时值**；真·触发前 CSS 计数未取（见 §7-1）。⛔ 未回写 JSON，按原样＋本注列报。
2. **首跑（run2）Q4/Q1 空转**：`BuildingRegistry/KingdomRegistry` 经泛型基类静态字段反射取不到 ⇒ `buildings=0`、`Q1 未触发`；该跑产物已**重跑前归档**为 `…_d904_run2_archive.{txt,json,runtime_resp.txt}`（符合「重跑前先归档」纪律），其 Q2/Q3 段（6 快照）与第三跑同型；正式证据＝第三跑（canonical 文件名）。
3. **`(108,97)` 差 2 格未定性**：仅给「同对象·口径差」强候选解释；⛔ 未逐条核对早期轮原文。
4. **Q3 快照为「静止 5 游戏秒」判定**，非「精确停滞起点」；快照后的终态由日志裁定（探针字段 `Q3.terminal=<待日志裁定>` 原样）。
5. **本会话池内无 `Unreachable`**（全 `Timeout` 家族）：Q2② 的「池内亚帧循环」本会话未复现；分布随局变化已列 §4。
6. 探针首次编译失败 2 次（`bool→string` 赋值；同类修正），执行记录在案，重跑后通过。

## 9. 【请裁】（4 条）

1. **`(108,97)` 是否需原文核对**：是否准下一轮对早期轮原文做**逐条坐标口径核对**（世界/格/微格换算与 footprint 锚点），以把「同对象·口径差」从强候选升为已证？（⛔ 本轮范围外，未自行改期）
2. **玩家王国侧验证路径**：本轮已在 **AI 建筑**上证实「投料 → 注册链成立」；而**玩家王国全量仅 1 座 `castle`（恒空配方、`Abandoned`）** ⇒ 是否准下一轮改对**玩家王国可投料承载者**（先有非空配方建筑/放置流程）验证，还是维持跨王国验证口径？
3. **「两到达判据差」是否立只读专项**：停滞点观测到「PF 吸附点判据（`dest` 距 `0.32`）与调度器源位判据（阈值 `0.38`，实测站位 `0.43–0.48`）」并存 ⇒ 是否准列为下一轮只读专项（沿 `D894` 口径，⛔ 本端不定性）？
4. **亚帧循环复现触发条件**：本会话池内未复现（分布随局变化）⇒ 是否准下一轮把「日志侧 `Unreachable` 行出现即抓帧级窗口」定为触发条件（仍只读、⛔ 不改触发方式）？

## 10. 源级结论

⛔ **维持 `ConstructionSiteStore` 未通过 · 停手待裁**（`D904 Q6` 口径；⛔ 未写「判绿/判红」字样）。本轮新增事实：`Q1` 三合一**成立**（投料→注册链）、`Q4` 三口径基线建立、`Q2` 626 复核通过＋本会话 938 跨会话分布变化、`Q3` 9 快照（两判据差原样）。⛔ 未申请下一源 · ⛔ 未并 `DZ-7` · ⛔ 未取新号 · ⛔ 未代写策划端账本。

---

## 附：合规声明（实测输出）与交付件

```
git rev-parse HEAD（开工=收工）      d7823702ae253e833c52892888a1515ec032cdbe
git diff --name-only -- "Valley Rampart/Assets/_Game"        （无输出行）
git status --short -- "…/GameScene.unity"                    M "…/GameScene.unity"
开跑前 hash ＝ 收工后 hash ＝ 4a86f26f6a7c2aab3c440903ddfa80020ddec9a3（一致）
⛔ 未执行 git checkout -- / git restore / 整文件回滚
```

**交付件**（`Valley Rampart/Logs/`，gitignore 第 7 行忽略 ⇒ 提交只含本报告 1 文件）：`…_d904.{cs,json,txt}`｜`…_d904_stat.py → …_d904_stat.txt`（空跑先行：`…_d904_stat_dry.txt`）｜`…_d904.logs.txt`（本会话筛选日志）｜run2 归档 `…_d904_run2_archive.*`｜runner／stop／state 载荷。**桥接**：`success=true`，`elapsed_ms=25649`。**复现命令**：

```powershell
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_d904_stat.py" --out "Valley Rampart\Logs\hh341_small_construction_site_d904_stat_dry.txt" "Valley Rampart\Logs\hh341_small_construction_site_d904_stat_dry_in.txt"
pwsh -NoProfile -File "Valley Rampart\Logs\hh341_small_construction_site_d904_run.ps1" -CsName "hh341_small_construction_site_d904.cs" -RequestId "hh341d904rt3" -TimeoutSec 300
pwsh -NoProfile -File "Logs\_bridge_call.ps1" -Payload "Valley Rampart\Logs\hh341_small_construction_site_d904_stop.json" -TimeoutMs 60000
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_d904_stat.py" --out "Valley Rampart\Logs\hh341_small_construction_site_d904_stat.txt" "Valley Rampart\Logs\hh341_small_construction_site_d904.txt" --unreach "Valley Rampart\Logs\hh341_small_construction_site_trigger.logs.txt" --unreach "Valley Rampart\Logs\hh341_small_construction_site_d904.logs.txt"
```