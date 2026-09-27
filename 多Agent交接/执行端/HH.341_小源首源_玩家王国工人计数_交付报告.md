# HH.341 · 小源首源 —— **`D899` 复议 `Q4` 玩家王国工人只读计数** 交付报告

> 执行端 ｜ 2026-09-27 ｜ 只读计数 ＋ 三处机械整改 ｜ ⛔ 零生产码／资产／政策值 ｜ ⛔ 未 push ｜ 挂 `D899`（⛔ 未取新号）
> 依据：`D899` 复议裁定（`Q4` 已放行）｜ 台账 §二百〇八 ｜ `L-97`（含 `D897` 补条）＋ `L-98`（含 `D899` 补条）
> 基线 **HEAD = `21feaa0c`**（收工 `git rev-parse HEAD` 重核一致，全哈希见 §⑤）
> ⛔ 未改 `taskTimeout`／`Complete` ｜ ⛔ 未并 `DZ-7` ｜ ⛔ 未申请下一个源 ｜ ⛔ 未代写策划端账本（`D767`）

**判据句（两臂相同）**：`K0_TOTAL=4` 且 `K0_ONBOOK=0` ⇒ **该时刻玩家王国工人均未在册**。

---

## 一、`L-97` 三件套 ＋ `L-98` 折算

### ① 观测范围

建局与暖机沿用 `{R15x,C1x}_rerun2.cs`：`seed=424242`、`WorldSize.Small`、`difficulty=2`、王国名 `hh341r2`。R 声明暖机 20 游戏秒、速度用配置；C 声明暖机 30 游戏秒、`speedOverride=1`。存档槽改为 `hh341k0R15x`／`hh341k0C1x`，避免 `Delete` 掉上轮槽。暖机结束即停：未选工投料、未调用 `BeginMaterialPhase`、未起算零点、未开观测窗。

`K0_TOTAL` 的全集是 `FindObjectsOfType<NPCBrain>()`（不含未激活物体）里 `UnitController.kingdomId==0` 且职业 ∈ {`Worker`,`Civilian`,`Porter`}。`K0_ONBOOK` 只在这个全集里数 `_npcTaskMap[npcId]` 的值非 null。另有一条 `includeInactive=true` 的 `BRAIN_ROW` 普查，分母单独标明，不改写 `K0_TOTAL`。

| 臂 | `t_warmup_start` | `t_warmup_end` | 建局+暖机（游戏秒） | `WaitForSeconds` 游戏秒 | `WaitForSeconds` 墙钟 | `timeScale` | `K0_ROW` | `MAP_ROW` | `BRAIN_ROW` |
|---|---|---|---|---|---|---|---|---|---|
| **R15x** | 0.7 | 21.9 | **21.1** | **20.8** | **0.63 s** | **15.0** | **4** | **12** | **32** |
| **C1x** | 0.7 | 31.1 | **30.3** | **30.0** | **29.36 s** | **1.0** | **4** | **12** | **32** |

活跃脑与含未激活脑都是 32，本快照没有多出来的未激活 `NPCBrain`。

### ② `L-98` 折算栏

`WaitForSeconds(n)` 的游戏秒就是 n。`timeScale` 只把游戏秒折成墙钟时做除法。本栏的「匀速折算」是 `n / timeScale`，不是 `n × timeScale`。

| 臂 | 声明暖机 | 实测 `WaitForSeconds` 游戏秒 | 实测墙钟 | 匀速折算 | 建局+暖机墙钟 |
|---|---|---|---|---|---|
| **R15x** | 20 游戏秒 | **20.8** | **0.63 s** | 20/15 ≈ 1.33 s | **3.4 s** |
| **C1x** | 30 游戏秒 | **30.0** | **29.36 s** | 30/1 = 30 s | **30.5 s** |

两臂都没有进入观测窗，这里不构成 `L-98` 意义上的 1x 对 15x 行为比较。

### ③ 逐字段三数（分母 = 该字段最终有效数）

统计器先对空文件 `hh341_small_construction_site_k0count_empty.txt` 跑过（`K0.occupation`：原始 2 → 排除 2 → 最终 0，分母 0；`Worker`/`Civilian`/`Porter` 均为 0），再跑两臂实文件。锚是 `^## S ` 加行类型加字段全名。值捕获用 `([^\s|}]*)`。段标记、换源、汇总不进最终有效数。全文在 `hh341_small_construction_site_{R15x,C1x}_k0count_stat.txt`。

两臂非采样行相同：段标记 1、换源 0、汇总 67、其他 1。

| 字段 | 锚行类型 | R 原始 → 排除 → 最终 | R 分母 | C 原始 → 排除 → 最终 | C 分母 |
|---|---|---|---|---|---|
| `K0.kingdomId` | `K0_ROW` | 5 → 1 → **4** | 4 | 5 → 1 → **4** | 4 |
| `K0.npcId` | `K0_ROW` | 4 → 0 → **4** | 4 | 4 → 0 → **4** | 4 |
| `K0.occupation` | `K0_ROW` | 8 → 4 → **4** | 4 | 8 → 4 → **4** | 4 |
| `K0.wState` | `K0_ROW` | 5 → 1 → **4** | 4 | 5 → 1 → **4** | 4 |
| `K0.onBook` | `K0_ROW` | 5 → 1 → **4** | 4 | 5 → 1 → **4** | 4 |
| `K0.task.type` | `K0_ROW` | 5 → 1 → **4** | 4 | 5 → 1 → **4** | 4 |
| `K0.source.GetType()` | `K0_ROW` | 5 → 1 → **4** | 4 | 5 → 1 → **4** | 4 |
| `K0.卡ID` | `K0_ROW` | 5 → 1 → **4** | 4 | 5 → 1 → **4** | 4 |
| `MAP.kingdomId` | `MAP_ROW` | 12 → 0 → **12** | 12 | 12 → 0 → **12** | 12 |
| `MAP.npcId` | `MAP_ROW` | 12 → 0 → **12** | 12 | 12 → 0 → **12** | 12 |
| `MAP.occupation` | `MAP_ROW` | 12 → 0 → **12** | 12 | 12 → 0 → **12** | 12 |
| `MAP.wState` | `MAP_ROW` | 13 → 1 → **12** | 12 | 13 → 1 → **12** | 12 |
| `MAP.onBook` | `MAP_ROW` | 13 → 1 → **12** | 12 | 13 → 1 → **12** | 12 |
| `MAP.task.type` | `MAP_ROW` | 17 → 5 → **12** | 12 | 17 → 5 → **12** | 12 |
| `MAP.source.GetType()` | `MAP_ROW` | 17 → 5 → **12** | 12 | 17 → 5 → **12** | 12 |
| `MAP.卡ID` | `MAP_ROW` | 13 → 1 → **12** | 12 | 13 → 1 → **12** | 12 |
| `MAP.reason.noBrain` | `MAP_ROW` | 14 → 2 → **12** | 12 | 14 → 2 → **12** | 12 |
| `MAP.reason.occ` | `MAP_ROW` | 14 → 2 → **12** | 12 | 14 → 2 → **12** | 12 |
| `MAP.reason.kingdom` | `MAP_ROW` | 14 → 2 → **12** | 12 | 14 → 2 → **12** | 12 |
| `MAP.reason.inactive` | `MAP_ROW` | 14 → 2 → **12** | 12 | 14 → 2 → **12** | 12 |
| `MAP.primary` | `MAP_ROW` | 13 → 1 → **12** | 12 | 13 → 1 → **12** | 12 |
| `BRAIN.kingdomId` | `BRAIN_ROW` | 39 → 7 → **32** | 32 | 39 → 7 → **32** | 32 |
| `BRAIN.npcId` | `BRAIN_ROW` | 32 → 0 → **32** | 32 | 32 → 0 → **32** | 32 |
| `BRAIN.occupation` | `BRAIN_ROW` | 37 → 5 → **32** | 32 | 37 → 5 → **32** | 32 |
| `BRAIN.isAlive` | `BRAIN_ROW` | 33 → 1 → **32** | 32 | 33 → 1 → **32** | 32 |
| `BRAIN.occMatch` | `BRAIN_ROW` | 33 → 1 → **32** | 32 | 33 → 1 → **32** | 32 |
| `BRAIN.inK0Total` | `BRAIN_ROW` | 33 → 1 → **32** | 32 | 33 → 1 → **32** | 32 |

证据链：采样行在 `Valley Rampart/Logs/hh341_small_construction_site_{R15x,C1x}_k0count.txt` 的 `## S K0_ROW`（43–46 行）、`## S MAP_ROW`（47–58 行）、`## S BRAIN_ROW`（11–42 行）。正则在 `hh341_small_construction_site_k0count_stat.py` 的 `FIELDS` 与 `scan_field()`。

复现：

```powershell
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_k0count_stat.py" "Valley Rampart\Logs\hh341_small_construction_site_k0count_empty.txt" "Valley Rampart\Logs\hh341_small_construction_site_k0count_stat_dry.txt"
pwsh -NoProfile -File "Valley Rampart\Logs\hh341_small_construction_site_k0count_run.ps1" -CsName "hh341_small_construction_site_R15x_k0count.cs" -RequestId "hh341k0R15xrt" -TimeoutSec 240
pwsh -NoProfile -File "Logs\_bridge_call.ps1" -Payload "Valley Rampart\Logs\hh341_small_construction_site_k0count_stop.json" -TimeoutMs 30000
pwsh -NoProfile -File "Valley Rampart\Logs\hh341_small_construction_site_k0count_run.ps1" -CsName "hh341_small_construction_site_C1x_k0count.cs" -RequestId "hh341k0C1xrt" -TimeoutSec 200
pwsh -NoProfile -File "Logs\_bridge_call.ps1" -Payload "Valley Rampart\Logs\hh341_small_construction_site_k0count_stop.json" -TimeoutMs 30000
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_k0count_stat.py" "Valley Rampart\Logs\hh341_small_construction_site_R15x_k0count.txt" "Valley Rampart\Logs\hh341_small_construction_site_R15x_k0count_stat.txt"
python -X utf8 "Valley Rampart\Logs\hh341_small_construction_site_k0count_stat.py" "Valley Rampart\Logs\hh341_small_construction_site_C1x_k0count.txt" "Valley Rampart\Logs\hh341_small_construction_site_C1x_k0count_stat.txt"
```

`execute_csharp_script` 且 `execution_mode=play` 的第一次调用被拒：编辑器当时不在播放。成功路径是 `exec_runtime_script`（由 `k0count_run.ps1` 组载荷）。两臂返回均为 `C# script executed successfully`（R `elapsed_ms=14290`，C `elapsed_ms=36496`）。

---

## 二、采信读数（原样）

### 分开落盘的两个数

| 臂 | `K0_TOTAL` | `K0_ONBOOK` | 判据句 |
|---|---|---|---|
| R15x | **4** | **0** | 该时刻玩家王国工人均未在册 |
| C1x | **4** | **0** | 该时刻玩家王国工人均未在册 |

职业分布（`K0_ROW`，分母 = `K0_TOTAL` = 4）两臂相同：`Worker=4`｜`Civilian=0`｜`Porter=0`。

职业匹配数两臂相同：`OCC_MATCH=4`。分母是活跃且 `kingdomId==0` 且有 `UnitController` 的 `NPCBrain`，等于 **9**（其中 4 名 `Worker` 计入 `K0_TOTAL`，另 5 名 `Resident` 不在职业集合里）。

四类未匹配是 `_npcTaskMap` 条目上的独立标记，分母 = `MAP_N` = 12。本快照独立计数与互斥主因数值相同。`NPCBrain==null` 会同时进入「无该 npcId」和「非活跃」；本快照没有这种重叠。

| 臂 | 无该 npcId 的 NPCBrain | 职业不匹配 | `kingdomId != 0` | 非活跃（`NPCBrain==null` 或 `!IsAlive`） |
|---|---|---|---|---|
| R15x | **0** | **0** | **12** | **0** |
| C1x | **0** | **0** | **12** | **0** |

互斥主因（优先级：无该 npcId > 非活跃 > 职业不匹配 > `kingdomId!=0` > 可匹配）两臂都是：前三类 0，`kingdomId!=0` = 12，可匹配 = 0。

### 臂 R15x · `K0_ROW` 与 `MAP_ROW` 原样

```
## S K0_ROW K0.kingdomId=0 K0.npcId=22 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task> K0.卡ID=<未取得:协议运行时未初始化>
## S K0_ROW K0.kingdomId=0 K0.npcId=21 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task> K0.卡ID=<未取得:协议运行时未初始化>
## S K0_ROW K0.kingdomId=0 K0.npcId=20 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task> K0.卡ID=<未取得:协议运行时未初始化>
## S K0_ROW K0.kingdomId=0 K0.npcId=19 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task> K0.卡ID=<未取得:协议运行时未初始化>
## S MAP_ROW MAP.kingdomId=3 MAP.npcId=15 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Production MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=2 MAP.npcId=9 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Production MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=3 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Production MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=4 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=5 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=2 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=1 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=6 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=3 MAP.npcId=18 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Production MAP.source.GetType()=MineByproductComponent MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=2 MAP.npcId=12 MAP.occupation=Worker MAP.wState=Working MAP.onBook=True MAP.task.type=Production MAP.source.GetType()=MineByproductComponent MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=3 MAP.npcId=14 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=WaterHaul MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=2 MAP.npcId=8 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=WaterHaul MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
```

### 臂 C1x · `K0_ROW` 与 `MAP_ROW` 原样

```
## S K0_ROW K0.kingdomId=0 K0.npcId=22 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task> K0.卡ID=<未取得:协议运行时未初始化>
## S K0_ROW K0.kingdomId=0 K0.npcId=21 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task> K0.卡ID=<未取得:协议运行时未初始化>
## S K0_ROW K0.kingdomId=0 K0.npcId=20 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task> K0.卡ID=<未取得:协议运行时未初始化>
## S K0_ROW K0.kingdomId=0 K0.npcId=19 K0.occupation=Worker K0.wState=None K0.onBook=False K0.task.type=<无task> K0.source.GetType()=<无task> K0.卡ID=<未取得:协议运行时未初始化>
## S MAP_ROW MAP.kingdomId=3 MAP.npcId=17 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=3 MAP.npcId=13 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Production MAP.source.GetType()=MineByproductComponent MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=3 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Production MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=4 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=5 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=2 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=1 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=1 MAP.npcId=6 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=ChestEntity MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=3 MAP.npcId=14 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=WaterHaul MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=2 MAP.npcId=8 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Production MAP.source.GetType()=MineByproductComponent MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=3 MAP.npcId=16 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
## S MAP_ROW MAP.kingdomId=2 MAP.npcId=10 MAP.occupation=Worker MAP.wState=MovingToSource MAP.onBook=True MAP.task.type=Transport MAP.source.GetType()=Building MAP.卡ID=<未取得:协议运行时未初始化> MAP.reason.noBrain=0 MAP.reason.occ=0 MAP.reason.kingdom=1 MAP.reason.inactive=0 MAP.primary=kingdomId_ne_0 MAP.hitN=1
```

`BRAIN_ROW` 32 行原样在各自 txt 的 11–42 行。分布见下。

### 全量分布（含零计数；两栏分开）

`kingdomId` 分布，分母 = `BRAIN_ROW` 最终有效数 **32**。两臂相同：`-1=5`｜`0=9`｜`1=6`｜`2=6`｜`3=6`。域是本快照观测值并强制含 0。

`occupation` 分布，分母 = `BRAIN_ROW` **32**。两臂相同：`Worker=22`｜`Resident=5`｜`Vagrant=5`，其余 `Occupation` 枚举值与 `<未取得:无UnitController>` 均为 0（名单在统计器输出）。

`K0.occupation` 分布，分母 = **4**：`Worker=4`｜`Civilian=0`｜`Porter=0`。

`source.GetType()` 与 `task.type` 分开计。分母都是 `MAP_ROW` **12**。`ConstructionSiteStore` 只出现在来源栏。

| 臂 | `source.GetType()` 分布（分母 12） | `task.type` 分布（分母 12） |
|---|---|---|
| **R15x** | `Building=5`｜`ChestEntity=5`｜`MineByproductComponent=2`｜`ConstructionSiteStore=0`｜`Transport=0`｜`WorldGatherSource=0`｜`<null>=0`｜`<无task>=0` | `Production=5`｜`Transport=5`｜`WaterHaul=2`｜`Repair=0`｜`Build=0`｜`Produce=0`｜`Rancher=0`｜`WaterCarry=0`｜`GoldMine=0`｜`Gather=0`｜`AmmoReload=0`｜`<无task>=0` |
| **C1x** | `Building=5`｜`ChestEntity=5`｜`MineByproductComponent=2`｜`ConstructionSiteStore=0`｜`Transport=0`｜`WorldGatherSource=0`｜`<null>=0`｜`<无task>=0` | `Transport=8`｜`Production=3`｜`WaterHaul=1`｜`Repair=0`｜`Build=0`｜`Produce=0`｜`Rancher=0`｜`WaterCarry=0`｜`GoldMine=0`｜`Gather=0`｜`AmmoReload=0`｜`<无task>=0` |

玩家王国 4 名工人自己的 `K0.task.type`／`K0.source.GetType()` 都是 `<无task>`（分母 4）。上表是在册 12 条的分布，不是这 4 人的任务分布。

---

## 三、已排

- 未把本快照写成「玩家王国没有工人」。两臂 `K0_TOTAL` 都是 4。
- 未把「在册 12 条的 `kingdomId` 都不是 0」写成玩家王国工人未在册的原因。两件事分开落盘，不合并、不做根因。
- 未与首源归因合并，未申请下一个源，未改 `taskTimeout`／`Complete`，未并入 `DZ-7`，未取新 D 号。
- 未改暖机时长、未开观测窗、未起算零点、未调用 `BeginMaterialPhase`。
- 未把 `ConstructionSiteStore` 放进 `task.type` 栏。来源栏的 0 与类型栏的 0 分开。
- 未改 `Valley Rampart/Assets/_Game` 已跟踪文件，未改策划端账本，未 push。
- 未调用 `TaskScheduler.ProtocolRuntime` 的懒初始化 getter。

## 四、未取得

**卡 ID。** 反射读 `_protocolRuntime`，本快照该字段为 null。为避免懒初始化改写运行时，没有调用 `ProtocolRuntime` getter，也就没有读 `_workerToTask`。每一条 `K0_ROW` 与 `MAP_ROW` 的卡 ID 都是 `<未取得:协议运行时未初始化>`。

| 臂 | 字段 | 原始 → 排除 → 最终 | 分母 | 域 |
|---|---|---|---|---|
| R15x | `K0.卡ID` | 5 → 1 → **4** | 4 | `<未取得:协议运行时未初始化>=4`；`BindingManager空`／`无_protocolRuntime字段`／`无_workerToTask`／`无配对` 均为 0 |
| R15x | `MAP.卡ID` | 13 → 1 → **12** | 12 | 同上，未初始化 = 12，其余未取得桶 = 0 |
| C1x | `K0.卡ID` | 5 → 1 → **4** | 4 | 同 R |
| C1x | `MAP.卡ID` | 13 → 1 → **12** | 12 | 同 R |

证据：`k0count.txt` 第 10 行 `CARD_MODE`，采样行上的 `卡ID=`，统计器 `scan_field()`。这不是「无配对」。

**观测窗、零点、投料结果。** 本轮范围是选工前。这些数没有取。三数不适用，因为没有对应采样行；不是算成 0。

**上轮 `npc118`／`npc247`／`npc281`。** 本轮是新槽上的新局，工人 id 是 19–22。本轮没有去对上轮那三个 id。

## 五、合规声明（实测）

```
git rev-parse HEAD
21feaa0ccaa9343f96f88ea1bd7854ec1ee2b244

git diff --name-only -- "Valley Rampart/Assets/_Game"
（无输出）

git status --short -- "Valley Rampart/Assets/_Game"
?? "Valley Rampart/Assets/_Game/Art/Ground/New Palette.prefab"
?? "Valley Rampart/Assets/_Game/Art/Ground/New Palette.prefab.meta"
?? "Valley Rampart/Assets/_Game/Art/Ground/ground_tropical.asset"
?? "Valley Rampart/Assets/_Game/Art/Ground/ground_tropical.asset.meta"
```

已跟踪的 `_Game` 差异为空。上面 4 个未跟踪美术文件是本轮之前就在的，本轮未改、未暂存。

播放结束后 `GameScene.unity` 出现过一处 ruler 激活位改动（`m_IsActive` 从 1 变为 0）。开跑前编辑器状态里该场景 `dirty=false`。已执行 `git checkout -- "Valley Rampart/Assets/Scenes/GameScene.unity"`。还原后 `git diff --name-only -- "Valley Rampart/Assets/Scenes/GameScene.unity"` 无输出。

`Valley Rampart/Logs/` 被 `.gitignore` 第 7 行 `Valley Rampart/[Ll]ogs/` 忽略。计数脚本、两臂 txt／json、统计器、空跑记录，以及 `R15x_rerun2.cs` 的暖机注释，都在该目录里落了盘，按仓库忽略规则不进提交。本次提交只含本报告。

`hh341_small_construction_site_R15x_rerun2.cs:11` 注释现为 `// R15x 暖机（游戏秒）（= 20；墙钟 ≈ 20/15）`。两臂探针与统计器里的格式串只有 `"0.0"` 和 `"0.00"`。跑完 C 臂后已退出播放：`playMode=stopped`。

## 六、自报瑕疵

1. `hh341_small_construction_site_C1x_rerun2.cs:11` 仍是 `// C1x 暖机（游戏秒）（×1 = 30）`。点名整改只写了 R15x 那一行，本轮没有改 C 的旧注释。新写的 C 计数脚本注释是 `= 30；墙钟 ≈ 30/1`。
2. R 臂 `WARMUP_WAIT_ONLY` 墙钟是 **0.63 s**，按 15 倍匀速，20/15 ≈ 1.33 s。两数都写在 §一。这里不解释差值。
3. `t_warmup_start` 打在 `EnterTestRun` 之前，所以「暖机游戏秒(实测)」21.1／30.3 含建局。纯等待是 `WARMUP_WAIT_ONLY` 的 20.8／30.0。
4. 本轮 npcId 是新槽里的 19–22，不是上轮中止分支里的 118／247／281。
5. 采样行上有附加字段 `MAP.hitN`（两臂 12 条都是 1）。统计器字段表没有单独给它三数。
6. 独立四类与互斥主因在本快照数值相同。重叠规则写在 txt 的 `MISMATCH_NOTE`，本快照没有重叠样本。
7. 播放把 `GameScene.unity` 里 ruler 的激活位写脏，已还原。探针与注释在被忽略的 `Logs/` 里，提交不含这些文件。

## 七、【请裁】

1. 两臂都是 `K0_TOTAL=4 > 0` 且 `K0_ONBOOK=0`。是否准**只读复跑一次**，暖机延长到 **60 游戏秒**后再看这两个数？其余不改：不改窗口、不投料、不起算零点、不开观测窗。
2. 「非活跃 `NPCBrain`」独立计数两臂都是 **0／12**，含未激活普查也是 32＝32。这条的前提没有出现，本轮**不提请**把「活跃性」加进下一轮选工前置。
