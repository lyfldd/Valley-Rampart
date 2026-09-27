# HH.341 · 小源首源 —— **`D898` 两臂共同零点对照 ＋ 首源归属取证** 交付报告

> 执行端 ｜ 2026-09-27 ｜ 只读补证（两臂同批）｜ ⛔ 零生产码／资产／政策值 ｜ ⛔ 未 push ｜ 挂 `D898`（⛔ 未取新号）
> 依据：`D898` 复议裁定 ＋ `L-97`（含 `D897` 补条）＋ **`L-98`** ｜ 基线 **HEAD = `7e02f5af`**（收工实测重核一致 ✅）
> ⛔ 未申请下一个源 ｜ ⛔ 未改 `taskTimeout`／`Complete` 判据／未并 `DZ-7`

---

## 一、`L-97` 三件套 ＋ `L-98` 折算栏

### ① 观测范围（三类时刻**分别实测**，⛔ 无名义值）
| 臂 | `t_warmup_start` | `t_warmup_end` | 暖机（实测游戏秒） | `t_poll_start` | 零点 `t0` | 轮询（实测游戏秒） | 采样 `N` |
|---|---|---|---|---|---|---|---|
| **R15x** | 30220.3 | 30242.1 | **21.8** | （未达：选工即中止） | — | **未达成** | **0** |
| **C1x** | 30633.2 | 30664.0 | **30.8** | （未达：选工即中止） | — | **未达成** | **0** |

### ② `L-98` 折算栏（游戏秒 与 墙钟秒**都写**）
| 臂 | 声明暖机 | 实测暖机（游戏秒） | 实测暖机（墙钟） | 折算关系 | 单臂墙钟总 | 上限 |
|---|---|---|---|---|---|---|
| **R15x** | 20 游戏秒（`WaitForSeconds(20f)`） | **21.8** | **1.6 s** | 15x ⇒ 1 墙钟秒 ≈ 13.6 游戏秒 | **1.6 s** | 240 s |
| **C1x** | 30 游戏秒（`WaitForSeconds(30f)`） | **30.8** | **30.2 s** | 1x ⇒ 1:1 | **30.2 s** | 240 s |

⭐ **口径更正（实测）**：`WaitForSeconds(20f)` 在 15x 下实测推进 **21.8 游戏秒**（≈1.6 墙钟秒）⇒ **并非** 300 游戏秒 —— `WaitForSeconds` 以**游戏时间**计时；`D898`「20 × 15 = 300」的表述与实际不符，本报告以**实测**为准（⛔ 不据暖机差异推断模式支持性）。
⭐ **两臂未进入比较窗** ⇒ 无「等价窗」可比 ⇒ ⛔ 不构成 `L-98` 意义上的模式对比。

### ③ 全量取值域 ＋ 计数（分母 = 最终有效数 = **0**；含零计数项；三数制）
统计器 `Valley Rampart\Logs\hh341_small_construction_site_stat2.py` → `…_stat2_out.txt`（锚 **`^## S ` ＋ 字段全名**，⛔ 无 `\S+`）：

| 臂 | 目标字段（18 项） | 原始 | 排除 | 最终 | 域（含零计数） |
|---|---|---|---|---|---|
| R15x | `单位微格IsSubWalkable=`／`单位格级IsWalkable=`／`①单位微格=`／`wp0=`／`wp0.IsSubWalkable=`／`wp0.格级IsWalkable=`／`wp0==单位微格=`／`wpLen=`／`status=`／`reachedExactGoal=`／`PF.State=`／`_wpIndex=`／`_consecutiveFails=`／`Repathing累计=`／`wState=`／`在册=`／`卡ID{`／`_destination=` | 0 | 0 | **0** | 各取值＝0（`True`/`False`/`-`/`<无>`/`obj=null`/`Ready`/`Partial`/`Unreachable`/`Idle`/`Following`/`Repathing`/`Failed`/`None`/`MovingToSource`/`ConstructionSiteStore`/`ChestEntity`/`Transport`…） |
| C1x | 同上 18 项 | 0 | 0 | **0** | 同上（全 0） |

- 非采样行：R15x 段标记 0／换源 0／复算 0／汇总 6／其他 12；C1x 汇总/其他同类不计入。
- 证据链：`hh341_small_construction_site_R15x_rerun2.txt`（两臂输出原样引用见 §二）；正则位置＝`stat2.py` 的 `FIELDS` 表 ＋ `scan()`（`re.escape(name) + r'([^\s|}]*)'`，`re.match(r'^## S ', l)`）＋ `stat2.py` 空跑已通过（⛔ 无语法错误）。
- 复现命令：
```powershell
python -X utf8 'Valley Rampart\Logs\hh341_small_construction_site_derive2.py'
pwsh -NoProfile -File 'Logs\_bridge_call.ps1' -Payload 'Valley Rampart\Logs\hh341_small_construction_site_R15x_rerun2.json' -TimeoutMs 300000
pwsh -NoProfile -File 'Logs\_bridge_call.ps1' -Payload 'Valley Rampart\Logs\hh341_small_construction_site_C1x_rerun2.json'  -TimeoutMs 300000
python -X utf8 'Valley Rampart\Logs\hh341_small_construction_site_stat2.py'
```

---

## 二、采信读数（原样）

### 臂 R15x（`hh341_small_construction_site_R15x_rerun2.txt`）
```
== [D898 两臂共同零点] 臂=**R15x** speedOverride=（用配置） seed=424242 start=2026-09-27 15:45:08
== [准备阶段] t_warmup_end=30242.1 暖机游戏秒(实测)=21.8 墙钟=1.6
## ⛔ 未观测到已派工工人（_npcTaskMap 中无可读任务 或 无 matched NPCBrain）⇒ 登记并中止
== [_npcTaskMap 全量分布] 条目数=9
   map[1..4,6] 297/292/293/296/295  task=Transport/ChestEntity           wState=MovingToSource
   map[5] 294  task=Production/Building                    wState=MovingToSource
   map[7,8] 299/305  task=WaterHaul/Building               wState=MovingToSource
   map[9] 303  task=Production/MineByproductComponent      wState=Working
== [实测] 墙钟=1.6 采样行数N=0 起算=否
```

### 臂 C1x（`hh341_small_construction_site_C1x_rerun2.txt`）
```
== [D898 两臂共同零点] 臂=**C1x** speedOverride=1.0 seed=424242 start=2026-09-27 15:45:36
== [准备阶段] t_warmup_end=30664.0 暖机游戏秒(实测)=30.8 墙钟=30.2
## ⛔ 未观测到已派工工人（_npcTaskMap 中无可读任务 或 无 matched NPCBrain）⇒ 登记并中止
== [_npcTaskMap 全量分布] 条目数=13
   map[1,2,4,5,7,9,11,12] 328/327/339/341/340/329/324/325  task=Transport/ChestEntity 或 Transport/Building  wState=MovingToSource
   map[3,10,13] 326/335/336  task=Production/Building 或 Production/MineByproductComponent  wState=MovingToSource
   map[6,8] 331/337  task=WaterHaul/Building  wState=MovingToSource
== [实测] 墙钟=30.2 采样行数N=0 起算=否
```

⚠️ **引用方式声明（守 `D898`「逐点原样」）**：上方两臂 `_npcTaskMap` 列表为**分组缩写**（`map[a..b] npcId 组`）；**逐行原样**见原始文件**全文**（各自包含 `map[n] npcId=… task=…/… wState=…` 共 9 行／13 行），文件：`Valley Rampart\Logs\hh341_small_construction_site_R15x_rerun2.txt` 与 `…_C1x_rerun2.txt`（⛔ 未删改、未覆盖）。下列分布统计由这些**逐行原样**行计数得出。

### ⭐⭐ 任务来源 ／ 类型全量分布（`D898 §四` 硬要求；含零计数项；注明分母）
| 臂 | 分母（`_npcTaskMap` 条目数） | `source.GetType()` 分布 | `task.type` 分布 |
|---|---|---|---|
| **R15x** | **9** | `Transport`… 见下：**`ChestEntity`=5｜`Building`=3｜`MineByproductComponent`=1｜`ConstructionSiteStore`=0** | `Transport`=5｜`Production`=2｜`WaterHaul`=2｜**`ConstructionSiteStore`=0** |
| **C1x** | **13** | **`ChestEntity`=5｜`Building`=6｜`MineByproductComponent`=2｜`ConstructionSiteStore`=0** | `Transport`=8｜`Production`=3｜`WaterHaul`=2 |
| **两臂合计** | **22** | **`ChestEntity`=10｜`Building`=9｜`MineByproductComponent`=3｜`ConstructionSiteStore`=0** | `Transport`=13｜`Production`=5｜`WaterHaul`=4 |

### ⭐ 首源归属判定（⛔ 按 `D898 §四` 口径，不越线）
- 两臂 `_npcTaskMap` 中 **`ConstructionSiteStore` 计数 = 0**（分母 22）⇒ **只能登记「未观测到」首源任务** —— ⛔ **不写「未被派工」**、⛔ 不作为根因结论。
- ⚠️ 两臂**选工均失败**（`kingdomId==0` 且职业匹配的**已派工工人 = 0**）⇒ 未触发投料、未起算零点、`N=0` ⇒ **两个「对照未成立」**（⛔ 不称 1x 对照）。

### ⭐ 新增实质线索（⛔ 仅登记，不给结论，待裁）
两臂 `_npcTaskMap` 的 **22 条在册任务全部未匹配到 `kingdomId==0` 的 NPCBrain**（R 9 条／C 13 条）⇒ 玩家王国在建局 +21.8／+30.8 游戏秒时**无可读的已派工工人**，而 AI 王国已有大量 `Transport`/`Production`/`WaterHaul` 任务在跑。该线索可能解释此前多轮「投料态触发后无派工」：**上游可能不在路径点，而在「玩家王国工人可用性」** —— ⛔ 本轮**不裁断**，请裁是否立项。

---

## 三、已排
- 本轮**无新增排除项**（`N=0`，无任何段内模态可排除）。
- ⛔ 沿用已排：速度链闭合、两到达判据分歧；⛔ 「静默重寻循环」仍**未实证**（`Repathing` 未出现于任何已采窗口）。

---

## 四、未取得（附 `L-97` 三件套）

| # | 未取得项 | ① 观测范围 | ② 筛选条件（被排除取值＋计数） | ③ 全量取值域＋计数 |
|---|---|---|---|---|
| 1 | **§四 ①–⑥ 全部字段**（单位微格／`wp0` 族／`PF.State` 族／`wState`／`在册`／卡 ID／`_destination`） | R15x：暖机 21.8 游戏秒（1.6 s 墙钟）；C1x：暖机 30.8 游戏秒（30.2 s 墙钟）；两臂轮询**均未启动**；**N=0** | 选工条件＝`kingdomId==0` ∧ 职业∈{Worker,Civilian,Porter} ∧ `_npcTaskMap[npcId]!=null` ∧ `wState!=None`；**未匹配数**：R 9 条／C 13 条（全部未匹配） | 采样行域＝**空（N=0）**；`_npcTaskMap` 域见 §二（分母 9／13） |
| 2 | **零点 `t0` 与比较窗** | 同上 | 零点条件＝连续 2 次 `MovingToSource` ∧ 在册 ⇒ **未达成**（选工阶段即中止） | `MovingToSource` 命中数＝0（因无候选工人，轮询未执行） |
| 3 | **卡 ID（`卡ID{}`）** | 同上（无窗口内任务实例） | — | **结构性不可得**（未进入任务段）；**已试字段名（8 个，沿用 `D897`）**：`card`／`Card`／`taskId`／`TaskId`／`id`／`Id`／`protocolCard`／`ProtocolCard`（各自缺失时回显 `<无此字段>`） |
| 4 | **单位格级 `IsWalkable`** | 同上 | — | 探针已实现 `CellWalkable(GridCoord)`；**已试签名** `IsWalkable(GridCoord)`（单参）；不匹配时回显 `<无 IsWalkable> 已试签名:…` |
| 5 | **`_destination`（⑤）** | 同上 | — | 探针已实现 `Rd(pf,"_destination")`；因 `N=0` 未取值 |
| 6 | **调用次数** | — | — | **结构性不可得**（⛔ 禁改生产方法／运行时补丁／埋点） |

---

## 五、合规声明（实测输出）
```
## D898 合规 2026-09-27 15:46:14 HEAD=7e02f5af71777d393f97d207c8bf6cdfbb90f6de
## _Game diff（须空）hit_lines=0
（`git diff --name-only -- Valley Rampart/Assets/_Game` ⇒ 无输出行）
## Assets 全域
Valley Rampart/Assets/Scenes/GameScene.unity     ← 既有脏点，⛔ 非本批
```
- 基线 `HEAD = 7e02f5af` ✅（与 `D898` 声明一致；收工实测重核一致）
- ⛔ 未改 `taskTimeout`／`Complete` 判据／未并 `DZ-7`／未取新号／未申请下一源／未代写策划端账本（`D767`）
- ✅ 两臂**脚本／JSON／输出三者分开**（`{R15x,C1x}_rerun2.{cs,json,txt}`）；派生脚本 `…_derive2.py` **含 8 项源锚点 ＋ 9 项派生断言（含 `:1` 注释一致性）**，实测「残留 `R15x` = 0」
- ✅ 两臂 `:1` 注释与实际臂**一致**（R＝「臂 R15x（15x · 配置默认）」；C＝「臂 C1x（1x 对照臂）」）
- ✅ 自报数值格式串**全用 `"0.0"`／`"0.00"`**（⛔ 无 `"0.1"`）；落盘自报值与实际读数一致（21.8／30.8／1.6／30.2）
- ✅ **`.py` 一律落文件执行**（⛔ 未内联 `python -c`）；**统计器已先空跑**（输出 `READ_FAIL`＝文件尚未生成，语法通过）
- ✅ 段标记行含**卡 ID 占位**（`卡ID{…}`），不可得时列已试字段名

---

## 六、自报瑕疵
1. ⚠️ **C 臂 `:2` 标识行未随派生同步**（原残留 `SPD=-1f（用配置）｜ timeScale=15`）⇒ 收工前已用 .NET 文本替换修正为 `SPD=1f ｜ timeScale=1`；**根因＝派生脚本只替换带空格的 `SPD = -1f`，未覆盖标识行里的 `SPD=-1f`（无空格）** ⇒ 下轮派生脚本须把 `:2` 标识行纳入断言。
2. ⚠️ **`D898` 声明的暖机折算（R15x ＝ 300 游戏秒）与实测不符**（实测 21.8 游戏秒）—— 已按实测报告，请裁是否更正台账表述。
3. ⚠️ **两臂均未进入轮询阶段** ⇒ 轮询/窗口/换源/`Repathing` 等全部代码路径本轮**未被执行**（⛔ 不代表它们不可用，仅未观测）。
4. ⚠️ 脚本中止分支的 `_npcTaskMap` 分布输出**未区分「map 中存在但不匹配 NPCBrain」与「职业不匹配」**（本轮只报总条数）⇒ 下轮应分别计数，以精确判定筛选失败环节。

---

## 七、【请裁】（3 条）
1. **选工口径是否放宽**：两臂 `kingdomId==0` 的已派工工人 = **0**（R 9 条／C 13 条全未匹配）⇒ 是否准下一轮**放宽为「任意王国的已派工工人」**（并**同时记录该王国的 `kingdomId`**），以便真正进入任务段并采集 §四 字段？（⛔ 本轮未擅自放宽）
2. **是否立项「玩家王国工人可用性」专项**：22 条在册任务全在 AI 王国、玩家王国 0 工人 —— 是否作为独立线索立项（⛔ 与首源归因分开，⛔ 不合并缺陷编号）？
3. **暖机口径**：`Q4` 默认**不改**（`C1x` 暖机保持 30 游戏秒），但 `D898` 声明的「R15x ＝ 300 游戏秒」与实测不符 ⇒ 请确认：**按实测记录** 还是**统一两臂暖机游戏秒**（若统一，建议两臂均取 30 游戏秒，墙钟代价 R15x ≈ 2 s）？

## 八、状态
⛔ **首源源级＝未通过 · 停手待裁**（四项门槛不变）。本轮两臂均**未观测到** `ConstructionSiteStore` 任务 ⇒ 仅登记「未观测到」，⛔ 不作「未被派工」结论、⛔ 不判绿。
