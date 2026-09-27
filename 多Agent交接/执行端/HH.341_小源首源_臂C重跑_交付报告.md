# HH.341 · 小源首源 —— **`D897` 臂 `C1x` 重跑（Q2）** 交付报告

> 执行端 ｜ 2026-09-27 ｜ 只读补证 ｜ ⛔ 零生产码／资产／政策值 ｜ ⛔ 未 push ｜ 挂 `D897`（⛔ 未取新号）
> 依据：`D897` 复议裁定 ＋ `L-97`（含补条）｜ 基线 **HEAD = `bdacfa3d`**
> ⛔ 本轮**未跑 `R15x`**（其 15x 证据沿用 `Logs/hh341_small_construction_site_micro_R15x.txt`）｜ ⛔ 未申请下一个源

---

## 一、`L-97` 三件套

### ① 观测范围（⛔ 非名义推算）
| 项 | 实测 |
|---|---|
| 墙钟 | **92.4 秒**（`Stopwatch`：`SW.ElapsedMilliseconds`） |
| 起算轮询 | **240 次 × 0.25 游戏秒 = 60.0 游戏秒**（1x） |
| 采样行（`^## S `） | **N = 0**（行号区间 [-]） |
| 输出文件 | `Valley Rampart\Logs\hh341_small_construction_site_C1x_rerun.txt`（**8 行**） |
| 统计器 | `Valley Rampart\Logs\hh341_small_construction_site_C1x_stat.py` → 结果 `…_C1x_stat_out.txt` |

### ② 全量取值域 ＋ 计数（分母 = 最终有效数 = 0；⛔ 含零计数项）
统计口径＝`D897 §三`：正则锚 **`^## S ` ＋ 字段全名**（⛔ 无 `\S+`）；三数＝**原始匹配 → 排除 → 最终有效**。

| 字段（锚） | 原始 | 排除 | 最终有效 | 域（含零计数） |
|---|---|---|---|---|
| `单位微格IsSubWalkable=` | 0 | 0 | **0** | `True`=0｜`False`=0｜`-`=0 |
| `单位格级IsWalkable=` | 0 | 0 | **0** | `True`=0｜`False`=0｜`越界null`=0｜`<无>`=0｜`<越界>`=0 |
| `①单位微格=` | 0 | 0 | **0** | `越界null`=0 |
| `wp0=` / `wp0.IsSubWalkable=` / `wp0.格级IsWalkable=` / `wp0==单位微格=` | 0 | 0 | **0** | 各取值=0（`True`/`False`/`<无>`/`-`） |
| `wpLen=` | 0 | 0 | **0** | `-1`=0｜`<无>`=0 |
| `status=` | 0 | 0 | **0** | `Ready`=0｜`Partial`=0｜`Unreachable`=0｜`Cancelled`=0｜`Pending`=0｜`obj=null`=0 |
| `reachedExactGoal=` | 0 | 0 | **0** | `True`=0｜`False`=0｜`obj=null`=0 |
| `PF.State=` | 0 | 0 | **0** | `Idle`=0｜`Following`=0｜`Repathing`=0｜`Failed`=0 |
| `_wpIndex=` / `_consecutiveFails=` / `Repathing累计=` | 0 | 0 | **0** | — |
| `wState=` | **2** | **2** | **0** | `None`=0｜`MovingToSource`=0｜`MovingToTarget`=0｜`Working`=0｜`Returning`=0｜`Done`=0 |
| `在册=` | **2** | **2** | **0** | `无`=0｜`ConstructionSiteStore`=0｜`source=null`=0 |
| `卡标识{` / `_destination=` | 0 | 0 | **0** | — |

⭐ **口径有效性的实证**：`wState=` 原始匹配 2 → **排除 2** → 最终 0 —— 这 2 处正是**非采样行**（`## 对照工人…触发前wState=None`、`## ⛔ 起算未达成…wState=None`），按 `D897 §三③` 正确排除；若用旧口径（`\S+` 且不锚 `^## S `）即会污染计数。

### ③ 证据链（原始文件 ＋ 行号 ＋ 复现）
- 原始文件：`hh341_small_construction_site_C1x_rerun.txt`（**全文 8 行**，`split('\n')` 计数；含 2 个空行）——全文原样如下（⛔ 未删改）：
```
== [D897 臂 C 重跑] MODE=**C1x** speedOverride=1.0 seed=424242 start=2026-09-27 15:11:16
== 臂标识：脚本=hh341_small_construction_site_C1x_rerun.cs | MODE=C1x | TAG=hh341_small_construction_site_C1x_rerun
## 建局完成 timeScale=1.0 Time.time=28219.5 grid=True
## 对照工人=npc281 主城=(108,97) 触发前wState=None 触发前在册=无
## 触发投料态 awaiting=True t=28219.5
## ⛔ 起算未达成（轮询 240 次 / 60.0 游戏秒）wState=None 在册=无 ⇒ 结果登记：对照未成立（⛔ 不得称 1x 对照）
== [实测] 墙钟=92.4 秒 采样行数N=0 起算=否
```
（`== ` 前缀行＝汇总行，`## ⛔` 行＝中止行；两者按 `D897 §三③` **均不计入字段计数**。）
- 正则位置：统计器 `FIELDS` 表（字段全名锚）＋ `scan_field()`（`re.escape(name) + r'([^\s|}]*)'`，`re.match(r'^## S ', l)` 限行）
- 复现命令：
```powershell
pwsh -NoProfile -File 'Logs\_bridge_call.ps1' -Payload 'Valley Rampart\Logs\hh341_small_construction_site_C1x_rerun.json' -TimeoutMs 300000
python -X utf8 'Valley Rampart\Logs\hh341_small_construction_site_C1x_stat.py' > 'Valley Rampart\Logs\hh341_small_construction_site_C1x_stat_out.txt'
```

---

## 二、采信读数（原样，⛔ 无判定字样）

| # | 读数 | 出处 |
|---|---|---|
| 1 | `MODE=**C1x**`、`speedOverride=1.0`、`timeScale=1.0`、`seed=424242` | :1–2、:3 |
| 2 | `对照工人=npc281 主城=(108,97) 触发前wState=None 触发前在册=无` | :4 |
| 3 | `触发投料态 awaiting=True t=28219.5` | :5 |
| 4 | **`起算未达成（轮询 240 次 / 60.0 游戏秒）wState=None 在册=无`** | :6 |
| 5 | **`墙钟=92.4 秒 采样行数N=0 起算=否`** | :8 |

⇒ **结果登记（按 `D897` §一）：「对照未成立」** —— ⛔ 不称 1x 对照、⛔ 不写成「未出现 `Repathing`」，且 ⛔ 未无限等待（已按 240 次上限收工）。

---

## 三、已排
- 本轮**无新增排除项**（采样 N=0 ⇒ 无任何段内模态可排除）。
- ⛔ 沿用 `D895`／`D896` 已排：速度链闭合、两到达判据分歧；⛔ 仍**未实证**「静默重寻循环」。

---

## 四、未取得（附 `L-97` 三件套）

| # | 未取得项 | ① 观测范围 | ② 筛选条件（被排除取值＋计数） | ③ 全量取值域＋计数 |
|---|---|---|---|---|
| 1 | **§二 ①–⑥ 全部字段** | 轮询 **60.0 游戏秒**（240 次）／墙钟 92.4 s／采样行 **N=0** | 起算条件 `wState=="MovingToSource" ∧ _npcTaskMap[fid]!=null`；**未被探针排除的取值**：轮询期 `wState=None`（终值记录 1 次） | 采样行域：**空（N=0）**；轮询期：`wState` `MovingToSource`=0（⛔ 未逐次落盘 ⇒ 见自报瑕疵 1） |
| 2 | **卡标识** | 同上（未进入任务段 ⇒ 无 `KingdomTask` 实例可读） | — | ⛔ **结构性不可得**（当轮 `在册=无`）；探针已实现候选字段读取，**已试字段名**：`card`／`Card`／`taskId`／`TaskId`／`id`／`Id`／`protocolCard`／`ProtocolCard`（各字段读不到时回显 `<无此字段>`） |
| 3 | **单位格级 `IsWalkable`** | 同上 | — | 探针已实现 `CellWalkable(GridCoord)`（反射 `GridMethod.IsWalkable` 单参）；**已试签名**：`IsWalkable(GridCoord)`；未匹配时回显 `<无 IsWalkable> 已试签名:IsWalkable(GridCoord) 单参` 或 `<签名不匹配:n参>` |
| 4 | **`_destination`（⑤）** | 同上 | — | 探针已实现 `Rd(pf,"_destination")`；因 N=0 未取值 |
| 5 | **调用次数** | — | — | **结构性不可得**（⛔ `D897` 禁改生产方法／运行时补丁／埋点） |

---

## 五、合规声明（实测输出）
```
## D897 合规 2026-09-27 15:13:07 HEAD=bdacfa3db60671671626ba8968f3caa1e436b6df
## _Game diff（须空）hit_lines=0
（`git diff --name-only -- Valley Rampart/Assets/_Game` ⇒ 无输出行）
## Assets 全域
Valley Rampart/Assets/Scenes/GameScene.unity        ← 既有脏点，⛔ 非本批
```
- 基线 `HEAD = bdacfa3d` ✅（与 `D897` 声明一致）｜⚠️ 同目录另有 `…_C1x_rerun_git.txt` 为证据落盘
- ⛔ 未改 `taskTimeout`／`Complete` 判据／未并 `DZ-7`／未取新号／未申请下一源／未代写策划端账本（`D767`）
- ✅ 命名含规范前缀 ＋ 臂标识；R/C **脚本／JSON／输出三者分开**（本轮仅 C，未触碰 R 三件）
- ✅ 脚本 `:1` 注释＝「臂 C（1x 对照）重跑」与 `MODE="C1x"` 一致（上轮全角括号漏替根因已消除——本轮为**新建脚本**，非派生）
- ✅ 自报数值格式串**全用 `"0.0"`／`"0.00"`**（⛔ 无 `"0.1"`）⇒ 本轮实测值 `92.4`／`28219.5`／`60.0` 与实际一致

---

## 六、自报瑕疵
1. ⚠️ **轮询期 `wState` 未逐次落盘**（仅记终值 `None` 1 次）⇒ 不满足「全量取值域含计数」的完整要求；下一轮须在轮询循环内也落盘 `wState` 分布（`None`×N ／ `MovingToSource`×N）＋计数。
2. ⚠️ 统计器初次运行语法错误（多余 `)`）⇒ 修正后跑通；**教训：统计器写完须先空跑一次**。
3. ⚠️ 「起算未达成」分支无法读 `task.type`／`source.GetType()`／卡标识（当轮无任务实例）⇒ 已按 `D897` 要求**显式登记**并列出**已试字段名**（⛔ 未静默省略）。
4. ⚠️ `bridge` 墙钟按 **300 s** 设定（脚本内自限 ≈150 s；`D897` 要求 180 s）⇒ 已在上报中给出实际墙钟。

---

## 七、【请裁】（3 条）
1. **选工口径**：本轮 `npc281`（`触发前wState=None`）在 60 游戏秒内**未被派工** ⇒ 是否准用「**先定位已被派工的工人，再对其所在王国的建造源触发投料**」的口径重试一次？（⚠️ 需同时授权记录**任务类型全量分布**，以判定首源是否真的被派工）
2. **单位格级 `IsWalkable` 反射签名**：本轮未触发；探针已内置候选签名与回显 ⇒ 若下轮仍不匹配，请准按「**未取得 ＋ 列出已试签名**」登记（沿用 `D895` 复议已准的只读反射扩大）。
3. **墙钟预算**：本轮实测 **92.4 s**（建局 30 ＋ 轮询 60 ＋ 开销）；若下轮起算成功，需再 ＋30~60 游戏秒 ⇒ 预计 **150~160 s**，**180 s 偏紧** ⇒ 请裁：①放宽至 **240 s**，或 ②把起算轮询上限由 60 降至 **40 游戏秒**。

## 八、状态
⛔ **首源源级＝未通过 · 停手待裁**（四项门槛不变；本批 `对照未成立`，⛔ 不能判绿）。`Q1` 专项（不可走微格起点失败链）本轮仅采字段，证据由事务端单独登记（⛔ 未并入首源归因）。
