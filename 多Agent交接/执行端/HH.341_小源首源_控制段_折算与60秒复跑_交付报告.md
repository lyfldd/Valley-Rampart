# HH.341 · 小源首源 —— **`D900` 复议 `Q6` 折算补测 ＋ `Q1` 60 游戏秒只读复跑**（控制段）交付报告

> 执行端 ｜ 2026-09-27 ｜ 仪表校准 ＋ 只读计数 ｜ ⛔ 零生产码／资产／政策值 ｜ ⛔ 未 push ｜ 挂 `D900`（⛔ 未取新号）
> 依据：`D900` 复议裁定（`Q1`/`Q6`）｜ 台账 **§二百一十** ｜ `L-98`（含 `D899` 补条 · 已标「待复核」）｜ `L-99`
> 基线：任务书声明 `HEAD = 75b4e368`；**收工实测 `HEAD = aeaa03f0`**（＝`75b4e368` 之子提交，1 个 docs commit（策划端分工文档 +18/−2），非本端所出；列报见 §五）
> ⛔ 未触碰 `Valley Rampart/Assets/Scenes/GameScene.unity`（`L-99`：**只登记**，⛔ 未还原／未 `checkout`／未 `restore`）｜ ⛔ 未投料／⛔ 未调 `BeginMaterialPhase`／⛔ 未起算零点／⛔ 未开观测窗／⛔ 未选工 ｜ ⛔ 未改 `taskTimeout`／`Complete` 判据 ｜ ⛔ 未并 `DZ-7`／未申请下一源／未代写策划端账本（`D767`）

**判据句（两臂相同，原样）**：`== VERDICT_TEXT K0_TOTAL=4 K0_ONBOOK=1` ⇒ 本读数**不落入**「该时刻玩家王国工人均未在册」式；在册 1 人 = `npcId=22`（`Worker`／`wState=MovingToSource`／`task.type=Transport`／`source.GetType()=ChestEntity`）。

---

## 一、折算补测（`Q6` · 独立控制段 · 单独成表）

**口径声明**：游戏秒 ＝ `Time.time` 差（实测）；墙钟 ＝ `Stopwatch`（单独记录）；⛔ **未使用 `n × timeScale`**（游戏秒＝n；墙钟＝n ÷ timeScale 只作参考口径，本表一律给实测值）。本表数据**独立结算**，不与任何任务段数据混算（本轮无任务段）。

### 表 1-a 逐次实录（连续 3 × `WaitForSeconds(1f)`）

| 臂 | # | ① `Time.time` 增量（游戏秒） | ①′ 同值（ms） | ② 墙钟增量（`Stopwatch`） | ③ `Time.timeScale`（前→后） | ④ `Time.maximumDeltaTime`（前→后） | ⑤ 帧数 | ⑤ 实测帧率 | ⑤ 帧间隔均值 | 末帧 `dt`（scaled／unscaled） |
|---|---|---:|---:|---:|---|---|---:|---:|---:|---|
| **R15x** | 1 | **8.27** | 8268 | **0.04 s**（35 ms） | 15.0 → 15.0 | 1.00 → 1.00 | 1 | 28.6 | 35.00 ms | **8.27／0.55** |
| **R15x** | 2 | **1.13** | 1127 | **0.11 s**（108 ms） | 15.0 → 15.0 | 1.00 → 1.00 | 2 | 18.5 | 54.00 ms | 0.65／0.04 |
| **R15x** | 3 | **1.29** | 1292 | **0.04 s**（42 ms） | 15.0 → 15.0 | 1.00 → 1.00 | 2 | 47.6 | 21.00 ms | 0.32／0.02 |
| **C1x** | 1 | **1.04** | 1039 | **0.44 s**（435 ms） | 1.0 → 1.0 | 1.00 → 1.00 | 9 | 20.7 | 48.33 ms | 0.04／0.04 |
| **C1x** | 2 | **1.00** | 1001 | **1.00 s**（997 ms） | 1.0 → 1.0 | 1.00 → 1.00 | 27 | 27.1 | 36.93 ms | 0.03／0.03 |
| **C1x** | 3 | **1.04** | 1043 | **1.05 s**（1046 ms） | 1.0 → 1.0 | 1.00 → 1.00 | 34 | 32.5 | 30.76 ms | 0.04／0.04 |

- 段合计（校准段）：R ＝ 10.7 游戏秒／1.28 s 墙钟（含建局后首帧跳变，见下）；C ＝ 3.1 游戏秒／3.55 s 墙钟。
- ⑤ 的「帧率」＝该次等待窗内 `Time.frameCount` 差 ÷ 墙钟；「帧间隔均值」＝墙钟 ÷ 帧数；「末帧 dt」＝等待恢复点当场读取的 `Time.deltaTime`／`Time.unscaledDeltaTime`。

### 表 1-b 折算复核（对照窗；同口径实测）

| 臂 | 对照窗 | 游戏秒（实测） | 墙钟（`Stopwatch`） | **实测倍率** | `timeScale` | 说明 |
|---|---|---:|---:|---:|---:|---|
| **R15x** | 短窗 1 游戏秒 ×3（逐次） | 8.27／1.13／1.29 | 0.035／0.108／0.042 s | **236.23×／10.44×／30.76×** | 15.0 | 离散极大 ⇒ 帧边界量化主导 |
| **R15x** | 长窗 60 游戏秒（暖机段） | **60.4** | **4.57 s** | **13.22×** | 15.0 | 低于 15（停摆帧被 `maximumDeltaTime` 截断） |
| **C1x** | 短窗 1 游戏秒 ×3（逐次） | 1.04／1.00／1.04 | 0.435／0.997／1.046 s | **2.39×／1.00×／1.00×** | 1.0 | 首窗受帧边界量化影响 |
| **C1x** | 长窗 60 游戏秒（暖机段） | **60.8** | **60.95 s** | **1.00×** | 1.0 | 收敛 |

⭐ **实物锚点（本轮取到，原样）**：单帧游戏推进 ＝ `unscaled 帧长 × timeScale`，单帧上限 ＝ `maximumDeltaTime(1.00) × timeScale`：
- R #1：`dtEnd=8.27` ＝ `unscaledDtEnd=0.55 × 15.0`（≈8.25，舍入一致）；
- R #2：`dtEnd=0.65` ≈ `0.04 × 15`；R #3：`dtEnd=0.32` ≈ `0.02 × 15`；
- ⇒ **短窗（1 游戏秒）实测倍率离散（含上轮「20.8 游戏秒 / 0.63 s ⇒ ≈33×」）属同族「帧边界量化 ＋ 单帧停摆」伪像**；折算常数仍为 `timeScale`。⛔ 本条仅作仪表口径复核，**不作机制结论**；`L-98` 折算栏「待复核」是否解除＝**请裁**（§七-1）。

---

## 二、60 游戏秒只读复跑（`Q1` · 派工前稳定性确认）

### ① 观测范围（三类时刻**分别实测**，⛔ 无名义值）

建局与上轮同配置：`seed=424242`、`WorldSize.Small`、`difficulty=2`、王国名 `hh341r2`。R 臂速度用配置默认、C 臂 `speedOverride=1`。存档槽改为 `hh341ctlR15x`／`hh341ctlC1x`（⛔ 未覆盖上轮 `hh341k0*` 槽）。暖机里程碑 ＝ `t_warmup_start` 起**实测 60 游戏秒**（`Time.time` 差计时，沿用上轮同口径；校准段在窗内执行、独立结算）。

| 臂 | `t_warmup_start`（建局前） | `t_enter` | `t_cal_end` | `t_warmup_end`（读到点） | **暖机（实测游戏秒）** | 暖机墙钟 | 步数（记录用） | 校准后静候（游戏秒） |
|---|---|---|---|---|---|---|---|---|
| **R15x** | 0.8 | 1.1 | 11.8 | 61.2 | **60.4** | **4.57 s** | 40 | 49.4 |
| **C1x** | 0.7 | 1.1 | 4.2 | 61.6 | **60.8** | **60.95 s** | 57 | 57.4 |

- 桥接响应（成功证据）：R `elapsed_ms=10460`；C `elapsed_ms=66354`（各自 `.cs.runtime_resp.txt`）。
- 到点**立即收工**：循环每次 `WaitForSeconds(1f)` 后检查 `Time.time` 差，命中 ≥60 即停（⛔ 未因「数没变」延长）；收工 `== EXIT_OK`，退 Play 实测 `playMode=stopped`。

### ② 复跑四件（到点只读）

| 臂 | ① `K0_TOTAL` | ② `K0_ONBOOK` | ③ 职业分布（分母＝`K0_TOTAL`＝4） | ④ 在册状态 |
|---|---:|---:|---|---|
| **R15x** | **4** | **1** | `Worker=4`｜`Civilian=0`｜`Porter=0` | `onBook=True=1`（npc22：`MovingToSource`／`Transport`／`ChestEntity`）；`onBook=False=3`（npc19／20／21：`None`／`<无task>`） |
| **C1x** | **4** | **1** | 同 R | 同 R（逐行原样见 txt 第 20–23 行） |

**判据句**：两臂 `== VERDICT_TEXT K0_TOTAL=4 K0_ONBOOK=1`（**未触发**「均未在册」句式；⛔ 本端不替改为「未在册」）。
⭐ **与上轮对照（同 seed/同配置，仅暖机时长不同）**：上轮（`hh341k0*`，暖机实测 21.1／30.3 游戏秒）为 `K0_TOTAL=4／K0_ONBOOK=0`；本轮 60 游戏秒后两臂均为 `K0_TOTAL=4／K0_ONBOOK=1` ⇒ **在册状态已发生变化**（0→1）。⛔ **不据此推「首源已派工」**（`source.GetType()=ChestEntity ≠ ConstructionSiteStore`；`ConstructionSiteStore` 在 K0 行集内 = **0**／分母 4）；⛔ **不下派工机制结论**（`Q1` 仅作稳定性确认）。

### ③ `L-97` 三件套

**统计基数**：`## S` 采样行（按行类型分）：两臂均 `CAL_ROW N=3`（行号 9–11）∪ `K0_ROW N=4`（行号 20–23）。
**非采样行**（不计入）：两臂均 段标记=4／汇总=29／其他=1。
**正则**：锚 `^## S <行类型> <字段全名>`；值捕获 `([^\s|}]*)`（停在空白／竖线／右花括号）；⛔ 无 `\S+` 泛匹配。脚本：`Valley Rampart\Logs\hh341_small_construction_site_ctl_stat.py`（`FIELDS` 表 ＋ `scan_field()`）；**已先空跑**（空输入：全字段 原始 0 → 排除 0 → 最终 0，分母 0）→ `…_ctl_stat_dry.txt`。

**逐字段三数（原始 → 排除 → 最终；分母＝最终有效数）** —— 两臂数值相同：

| 字段 | 锚行类型 | 原始 → 排除 → 最终 | 分母 |
|---|---|---|---|
| `CAL.idx` | CAL_ROW | 4 → 1 → **3** | 3 |
| `CAL.gameDelta` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.gameMs` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.wallDelta` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.wallMs` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.timeScaleB` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.timeScaleA` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.maxDeltaB` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.maxDeltaA` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.frames` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.avgFps` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.avgFrameMs` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.dtEnd` | CAL_ROW | 3 → 0 → **3** | 3 |
| `CAL.unscaledDtEnd` | CAL_ROW | 3 → 0 → **3** | 3 |
| `K0.kingdomId` | K0_ROW | 5 → 1 → **4** | 4 |
| `K0.npcId` | K0_ROW | 4 → 0 → **4** | 4 |
| `K0.occupation` | K0_ROW | 8 → 4 → **4** | 4 |
| `K0.wState` | K0_ROW | 5 → 1 → **4** | 4 |
| `K0.onBook` | K0_ROW | 7 → 3 → **4** | 4 |
| `K0.task.type` | K0_ROW | 5 → 1 → **4** | 4 |
| `K0.source.GetType()` | K0_ROW | 5 → 1 → **4** | 4 |

**全量取值域（含零计数项）**：
- `CAL.timeScaleB/A`＝R `15.0=3`｜C `1.0=3`；`CAL.maxDeltaB/A`＝`1.00=3`（两臂）；`CAL.frames`＝R `1=1｜2=2`｜C `9=1｜27=1｜34=1`；`CAL.gameMs`＝R `1127｜1292｜8268`（各 1）｜C `1001｜1039｜1043`（各 1）；`CAL.wallMs`＝R `35｜42｜108`（各 1）｜C `435｜997｜1046`（各 1）。
- 其余 CAL 数值字段为开放数值（域＝观测值，不补未见值；`DOMAIN_NOTE` 已在 txt 声明）。
- `K0.kingdomId`＝`0=4`；`K0.npcId`＝`19=1｜20=1｜21=1｜22=1`（开放标识，域＝观测值）。
- `K0.occupation`＝`Worker=4｜Civilian=0｜Porter=0`；`K0.onBook`＝`True=1｜False=3`。
- `K0.wState`＝`None=3｜MovingToSource=1`（`Assigned`／`Working`／`MovingToDest`／`Completed`／`Abandoned`＝**0**，均列出）。
- `K0.task.type`＝`<无task>=3｜Transport=1`（其余枚举全部 **0**，均列出）。
- `K0.source.GetType()`＝`<无task>=3｜ChestEntity=1`（`ConstructionSiteStore`＝**0**｜`<null>`＝0｜`Building`＝0｜`MineByproductComponent`＝0｜`Transport`＝0｜`WorldGatherSource`＝0，均列出）。

**证据链**：`hh341_small_construction_site_{R15x,C1x}_ctl.txt`（CAL 行 9–11；K0 行 20–23）；结果 JSON `…_{R15x,C1x}_ctl.json`；统计器输出 `…_ctl_stat.txt`（含 ④ 折算复核 ⑤ 复跑读数复核）。**复现命令**：

```powershell
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_ctl_stat.py" --out "Valley Rampart\Logs\hh341_small_construction_site_ctl_stat_dry.txt" "Valley Rampart\Logs\hh341_small_construction_site_ctl_stat_dry_in.txt"
pwsh -NoProfile -File "Valley Rampart\Logs\hh341_small_construction_site_ctl_run.ps1" -CsName "hh341_small_construction_site_R15x_ctl.cs" -RequestId "hh341ctlR15xrt" -TimeoutSec 300
pwsh -NoProfile -File "Logs\_bridge_call.ps1" -Payload "Valley Rampart\Logs\hh341_small_construction_site_ctl_stop.json" -TimeoutMs 60000
pwsh -NoProfile -File "Valley Rampart\Logs\hh341_small_construction_site_ctl_run.ps1" -CsName "hh341_small_construction_site_C1x_ctl.cs" -RequestId "hh341ctlC1xrt" -TimeoutSec 300
pwsh -NoProfile -File "Logs\_bridge_call.ps1" -Payload "Valley Rampart\Logs\hh341_small_construction_site_ctl_stop.json" -TimeoutMs 60000
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_ctl_stat.py" --out "Valley Rampart\Logs\hh341_small_construction_site_ctl_stat.txt" "Valley Rampart\Logs\hh341_small_construction_site_R15x_ctl.txt" "Valley Rampart\Logs\hh341_small_construction_site_C1x_ctl.txt"
```

（技术路径：`exec_runtime_script` 自动进 Play ⇒ 正门 `TestHarnessApi.EnterTestRun` 建局 ⇒ 探针协程 ⇒ `ExitTestRun` ⇒ `manage_editor stop` 退 Play；两臂各自一次，未并行。）

---

## 三、采信读数（`K0_ROW` 原样）

### 臂 R15x（`hh341_small_construction_site_R15x_ctl.txt` 第 20–23 行）
```
## S K0_ROW K0.kingdomId=0 K0.npcId=22 K0.occupation=Worker K0.wState=MovingToSource K0.onBook=True K0.task.type=Transport K0.source.GetType()=ChestEntity
## S K0_ROW K0.kingdomId=0 K0.npcId=21 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task>
## S K0_ROW K0.kingdomId=0 K0.npcId=20 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task>
## S K0_ROW K0.kingdomId=0 K0.npcId=19 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task>
```

### 臂 C1x（`hh341_small_construction_site_C1x_ctl.txt` 第 20–23 行）
```
## S K0_ROW K0.kingdomId=0 K0.npcId=22 K0.occupation=Worker K0.wState=MovingToSource K0.onBook=True K0.task.type=Transport K0.source.GetType()=ChestEntity
## S K0_ROW K0.kingdomId=0 K0.npcId=21 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task>
## S K0_ROW K0.kingdomId=0 K0.npcId=20 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task>
## S K0_ROW K0.kingdomId=0 K0.npcId=19 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task>
```

两臂读数逐行一致（含 `npcId` 序列 19–22 与在册者 22）；本端 ⛔ 不作跨臂行为比较（`Q1` 不承担首源验收）。

---

## 四、已排 ／ 未取得

**已排（本轮不主张）**：
- ⛔ 未把「`K0_ONBOOK=1`」写成「首源已派工」或「派工触发链成立」：`source.GetType()=ChestEntity`（≠`ConstructionSiteStore`）；且未做按 tick 状态迁移记录（`Q8` 取证动作不在本批）。
- ⛔ 未把折算复核写成机制结论（仅仪表口径复核；短窗离散归因＝帧边界量化＋单帧停摆，有实物锚）。
- ⛔ 未改暖机之外的任何口径、未开观测窗、未起算零点、未触发投料、未选工、未读任务表做「顺带分析」。
- ⛔ 未触碰 `GameScene.unity`（只登记）、未并 `DZ-7`、未取新号、未申请下一源、未代写策划端账本。

**未取得**：本轮**四项（`K0_TOTAL`／`K0_ONBOOK`／职业分布／在册状态）全部取得，无「应取未取」项**。范围外未读两项：任务表全量分布（`MAP_ROW`）与卡 ID —— 不属本批「到点只读四项」，未读取（清单式声明，非「未取得」三件套对象）。

---

## 五、合规声明（实测输出）

```
## git rev-parse HEAD（收工实测）
aeaa03f026187fb2bdc73f2acae4aed4ab8f9065
## 任务书声明基线 75b4e368 与实测差异：75b4e368 为 aeaa03f0 之父（1 个 docs commit：
##   "docs(策划端): 主策划两端分工……（依用户 2026-09-27 17:54 拍板口径落地）"，+18/−2，非本端所出）
## _Game diff（须空）——实测输出：
git diff --name-only -- "Valley Rampart/Assets/_Game"
（无输出行）
## GameScene.unity —— 只登记不改（L-99）：
git status --short -- "Valley Rampart/Assets/Scenes/GameScene.unity"
 M "Valley Rampart/Assets/Scenes/GameScene.unity"
## 开跑前 hash ＝ 4a86f26f6a7c2aab3c440903ddfa80020ddec9a3
## 收工後 hash ＝ 4a86f26f6a7c2aab3c440903ddfa80020ddec9a3（一致 ⇒ 本轮播放未再写脏该文件）
## ⛔ 未执行 git checkout -- / restore / 任何整文件回滚；该文件仍为挂账 O-14 载体（D900 Q5：保留挂账）
```

- ✅ 零生产码／资产／政策值改动；`Valley Rampart/Logs/` 交付件被 `.gitignore` 第 7 行忽略（实测 `git check-ignore` 命中）⇒ 提交只含本报告 1 文件。
- ✅ 两臂**脚本／JSON／输出三者分开**（`{R15x,C1x}_ctl.{cs,json,txt}`）；`:2` 标识行与实臂一致（R＝「速度用配置」／C＝`speedOverride=1`）。
- ✅ 落盘自报值全为实测（`Time.time` 差／`Stopwatch`）；格式串仅 `"0.0"`／`"0.00"`（⛔ 无 `"0.1"`）。
- ✅ `.py` 落文件执行（⛔ 未内联）；统计器**先空跑**后实跑；正则锚 `^## S `＋字段全名（⛔ 无 `\S+`）。
- ✅ 进局走正门 `TestHarnessApi.EnterTestRun`／收尾 `ExitTestRun`；两臂跑完均退 Play（`playMode=stopped` 实测）。
- 编辑器收工态（bridge 实测）：`isPlaying=false`／`isCompiling=false`／console `unreadCount=0`、`lastErrors=[]`。

---

## 六、自报瑕疵

1. **校准段落在暖机时间窗内**：段序为「入局 → 校准（3×1s）→ 静候至 `t_warmup_start` 起 60 游戏秒 → 读数」；故暖机 60 秒中含校准段（R 10.7／C 3.1 游戏秒），校准后静候为 49.4／57.4。若评审要求「无仪器插入的纯净 60 秒窗」，请裁是否重跑（本端⛔ 不自行重跑、不自行改口径）。
2. **R 臂短窗 #1 出现单帧 8.27 游戏秒跳变**（`dtEnd=8.27`，`unscaledDtEnd=0.55`）⇒ 短窗倍率 236.23× 由该跳变主导。如实列，⛔ 未剔除、未平滑。
3. **判据句句式**：两臂均为「`K0_TOTAL=4 K0_ONBOOK=1`」（未命中「均未在册」句式）；⛔ 未替改措辞。
4. **基线漂移**：任务书声明 `75b4e368`，收工实测 `aeaa03f0`（差 1 个 docs commit，非本端）——已如实并列，未经本端推送或改写。
5. 本轮**未做 Console 三段法**（不在本批交付项内）；如需要，请裁是否补。
6. 校准段单次 `WaitForSeconds(1f)` 在低帧率下会**跨越多游戏秒**（R #1 一帧即完成等待）⇒ 「1 游戏秒等待」的实测增量在 R 臂天然 ≥1 且可能远超 1；表格按实测原样呈现。

---

## 七、【请裁】（2 条）

1. **折算补测是否追加 ＋ `L-98` 折算栏是否复核**：本批短窗倍率离散（R 236.23×／10.44×／30.76×，Σ 57.77×；C 2.39×／1.00×／1.00×，Σ 1.24×），长窗收敛（R **13.22×** 对 `timeScale=15`；C **1.00×** 对 `1`）；单帧推进＝`unscaled × timeScale` 已取到直接锚（`dtEnd` 对 `unscaledDtEnd` 逐行吻合）。⇒ 是否仍准**追加 3 × `WaitForSeconds(0.25f)`**（形式化验证「越短越离散」曲线）？或直接据本批复核 `L-98`「待复核」栏（解除/改写）？（请裁其一；⛔ 本端不自行开做）
2. **是否按 `D900 Q8` 进入派工触发取证**：本轮 60 游戏秒后两臂读数**已变化**（`K0_ONBOOK` 0→1，npc22＝`Transport`／`ChestEntity`；`ConstructionSiteStore` 在 K0 行集内=0，分母 4）⇒ 「与上轮完全相同」条件**未命中**。请裁：是否按 `Q8` 边界（探针只读、触发前记录 `K0_TOTAL`／`K0_ONBOOK`／职业／`wState`／任务表，触发后按 tick 记 `None→Assigned→MovingToSource→Working/Aborted`，卡 ID 优先 `_sources` 非懒初始化路径）进入下一轮？（⛔ 本轮不自行进入、不反复重跑）

---

## 八、状态

⛔ **首源源级＝未通过 · 停手待裁**（四项门槛不变）。本轮只完成**仪表校准（折算补测）＋ 只读复跑（60 游戏秒）**：两臂 `K0_TOTAL=4／K0_ONBOOK=1`（在册者 npc22／`Transport`／`ChestEntity`），⛔ 未申请下一源、⛔ 未并 `DZ-7`、⛔ 未取新号。