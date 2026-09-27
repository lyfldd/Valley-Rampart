# HH.341 · 小源首源 —— **D896「口径修正后 R 重跑 ＋ 臂 C 补跑」** 交付报告

> 执行端 ｜ 2026-09-27 ｜ 沿用 `HH.341`（⛔ 未取新号）｜ 裁定 `D896` · 落账 commit **`0a33d65b`** ｜ 新规 **`L-97`** 首轮执行
> 性质：只读补证（⛔ 零生产码／资产／政策值）｜ ⛔ 未提交、未 push ｜ ⛔ 不含判定字样

---

## 〇、上一轮失真 3 条的自我纠正（事务端已核实 · 本批已消除根因）

| 上轮失真 | 实测（本批） |
|---|---|
| 「全程 `wState=None 在册=无`／任务段未开始」 | **假**。上轮证据全量域实为 `wState`：`None`×277、**`MovingToSource`×23**；`Transport`×23 ⇒ 段**存在**，我漏读（只看了前 14 行） |
| 「`WaitForSeconds` 窗口压缩 15×／仅 ≈20 游戏秒」 | **假**。实测 **839.2 游戏秒**（t 1646.91→2486.10）；"20 秒"系**按名义周期推算**（`L-97` 明禁） |
| 「段内 5 项未取得」 | **不准确**。段内 ①②③④ 已在盘，仅 ⑤（首次 `Following→Repathing` 快照）确未取得 |

⇒ 根因：**未读完全量证据即下结论**。本批全程改为「**全量取值域 ＋ 计数**」取证（见 §二）。

---

## 一、本批两臂的 L-97 三件套（①观测范围 ②筛选条件 ③全量取值域）

### 臂 R（`MODE="R15x"` · 口径修正版：**取消 `source is ConstructionSiteStore` 筛选**）
- **① 观测范围**：采样行 **300**；`first_t=21394.38` → `last_t=22178.14`；**实测覆盖 = 783.8 游戏秒**（⛔ 非名义推算）
- **② 筛选条件**：对照工人＝① `IsIdleForTask ∧ wState==None` 优先 ② 兜底任意「起始微格可走」工人；**任务段标记改为 `wState == "MovingToSource"`**（⛔ 不再以 `source` 类型排除）
- **③ 全量取值域 ＋ 计数**：
  | 字段 | 取值域（计数） |
  |---|---|
  | `wState` | **`MovingToSource`×258**、`None`×63 |
  | `在册`（类型） | **`Transport`×258** |
  | `PF.State` | `Idle`×260、`Following`×40（`Failed`×**0**、`Repathing`×**0**） |
  | `status` | `obj=null`×260、`Ready`×70、`Partial`×19（`Unreachable`×**0**） |
  | `wpLen` | `<未取得>`×260、**2**×32、93×14、3×13、126×11、88×5… （**无 `1`**） |
  | `wp0.IsSubWalkable` | `True`×40（**全 True**） |
  | `wp0 == 单位微格` | `False`×34、`True`×6 |
  | 单位微格 `IsSubWalkable` | `True`×339、`False`×**1** |
  | `_wpIndex` | `0`×260、1…89 |
  | `fails` | **`0`×300（零递增）** |
  | `reachedExact` | `True`×22、`False`×18 |

### 臂 C（`MODE="C1x"` · 1x 对照 · 上限 900 tick）
- **① 观测范围**：采样行 **90**；`first_t=22874.63` → `last_t=22977.92`；**实测覆盖 = 103.3 游戏秒**
- **② 筛选条件**：同 R（含兜底选工）
- **③ 全量取值域 ＋ 计数**：`wState`：**`None`×90（全量）** ⇒ **该臂全程未进入任务段**；`PF.State`：`Idle`×76、`Following`×14；`status`：`obj=null`×76、`Ready`×14；`wpLen`：17/14/25/23/31/27/12/15/8（**无 `1`**）；单位微格 `IsSubWalkable`：`True`×104（无 False）；`fails`：`0`×91；**`Repathing`：0→0**
- ⇒ 按 `D896` 规格登记：**「该臂未出现 `Repathing`」**（⛔ 未无限等待，已按上限收工）

### 对照：上轮同一 `MODE="R15x"` 证据（`micro_R15x.txt`，保存原样，⛔ 未覆盖）
- ①观测 300 行 / **839.2 游戏秒**；③全量域：`PF.State` **`Failed`×240**、`Idle`×31、`Following`×29；`status` **`Unreachable`×10**、`Partial`×8、`Ready`×23；`wpLen` **`1`×10**；单位微格 `IsSubWalkable` **`False`×259**、`True`×79；`wp0.IsSubWalkable` `False`×**9**；`wp0.格级IsWalkable` **`False`×38**；`fails` 递增至 **2961**；卡 **`id=2 st=Aborted abort=RetryExhausted`**；**`Repathing`：0→0**

---

## 二、三分栏

### ⛔ 已证
| # | 读数（原样） | 证据 |
|---|---|---|
| 1 | **生产任务段在 R 臂大量存在且为 `Transport`**：`wState=MovingToSource`×258、`Transport`×258（783.8 游戏秒内）⇒ `D896`「上轮筛选漏掉生产任务段」**成立** | `…_R15x.txt` |
| 2 | **三态 `Ready`／`Partial`／`Unreachable` 均已实测**：本批 `Ready`×70、`Partial`×19；上轮 `Unreachable`×10 | 两文件 |
| 3 | **`Unreachable` 与 `wpLen=1` 同数（各 10）**，并伴随 `Failed`×240、`fails` 递增至 2961、卡 `Aborted/RetryExhausted` | 上轮文件 |
| 4 | **A* 结果含起点 waypoint**：`wp0 == 单位微格` 实测 `True`（本批 ×6、上轮 ×14）；且 `wpLen=1` 时该唯一点即单位微格 | 两文件 |
| 5 | **格级 `IsWalkable` 与微格 `IsSubWalkable` 不是同一判定**：同点 `wp0.IsSubWalkable=True` ∧ `wp0.格级IsWalkable=False`（×38） | 上轮文件 |
| 6 | **`Repathing` 在三份文件全量域中均为 0**（`PF.State` 未出现 `Repathing` 取值） | 三文件 |

### ⭐ 已排
- 本批（15x 口径修正）**未出现**：`Failed`×0、`Unreachable`×0、`wpLen=1`×0、`fails` 零递增 ⇒ 该模态**非必然复现**（与所选单位是否处于不可走微格相关：上轮 `False`×259 vs 本批 `False`×1）。
- 臂 C（1x）**未出现**任务段与 `Repathing`（⛔ 因 `wState=None`×90 全量，**该臂对任务段模态无对照意义**，如实登记）。

### ⚠️ 未取得（**附 `L-97` 三件套**）
| # | 未取得项 | ① 观测范围 | ② 筛选条件（被排除取值＋计数） | ③ 全量取值域＋计数 |
|---|---|---|---|---|
| 1 | **臂 C 的任务段读数** | 采样 90 行 / 实测 **103.3 游戏秒**（22874.63→22977.92） | 段标记 `wState=="MovingToSource"`；**未被排除取值**：`None`×90（即全部采样均非任务段） | `wState`：`None`×**90**／`MovingToSource`×**0** |
| 2 | **`Repathing` 状态变化（三臂均无）** | R 783.8 s ／ C 103.3 s ／ 上轮 839.2 s | 状态取值全量列出，无任何取值被探针自定义条件排除 | `PF.State`：上轮 `Failed`×240／`Idle`×31／`Following`×29；本批 R `Idle`×260／`Following`×40；C `Idle`×76／`Following`×14；**`Repathing`×0** |
| 3 | **首次 `Following→Repathing` 快照（上轮缺项）** | 同上 | 同上（依赖 #2） | 同上（`Repathing` 未出现 ⇒ 快照不存在） |
| 4 | **调用次数**（`NavigateToSource`／`EnsurePfAndSetDest`／`BehaviorExecutor.NavigateTo`／`Brake`／`RequestPath`） | — | — | **结构性不可得**（`D896` 禁改生产方法／运行时补丁／埋点） |

### ⭐ 任务三：读数登记（**须带作用域**）
| 读数 | 状态 | **作用域限定（必须随读数引用）** |
|---|---|---|
| 格级 `IsWalkable` ≠ 微格 `IsSubWalkable` | 已证 | 证据来自 **`Transport`／非首源筛选段** ⇒ ⛔ 不能直接判定 `ConstructionSiteStore` 专属根因 |
| A* 结果包含起点 waypoint | 已证 | 同上 |
| `Ready`／`Partial`／`Unreachable` 三态均已实测 | 已证 | 同上 |
| 单位处于不可走微格时 `Unreachable` 模态可形成 `wpLen=1` 与 `FailOnce` 链（`fails`↑、卡 `RetryExhausted`） | 已证（上轮） | 同上；且**本批未复现** ⇒ 非必然 |
| **「静默重寻循环」（`Repathing` 全程序显）** | ⛔ **未实证** | 三份文件 `Repathing`×0 ⇒ ⛔ **不得写成已实证** |
| 首源四项验收门槛 | ⛔ **不变** | 本批读数**不能判绿** |

---

## 三、R/C 脚本与 JSON 一一对应清单（`D896` 必附）

| 臂 | 脚本 | 常量行（`MODE`／`SPD`／`TAG`） | 循环上限 | JSON | 输出 | 运行结果 |
|---|---|---|---|---|---|---|
| **R15x** | `hh341_small_construction_site_micro_R15x.cs`（150 行） | `:8 "R15x"`｜`:9 -1f`｜`:10 "hh341_small_construction_site_micro_R15x"` | `:77 tick<3000` | `…_R15x.json`（`request_id=hh341d896R15x`） | `…_R15x.txt`（**111,145 B**） | ✅ 成功（54 s） |
| **C1x** | `hh341_small_construction_site_micro_C1x.cs`（150 行，由 `…_derive_C.py` 从 R 派生，断言差异仅 3 常量 ＋ `tick<900`） | `:8 "C1x"`｜`:9 1f`｜`:10 "hh341_small_construction_site_micro_C1x"` | `:77 tick<900` | `…_C1x.json`（`request_id=hh341d896C1x`） | `…_C1x.txt`（**29,942 B**） | ✅ 成功（135 s） |
| 选工兜底（两臂同） | 同上 `:67` `if (focus == null) { … }`（D896 兜底：无 idle 工人时取任意可走微格工人） | — | — | — | — | — |

⭐ 落盘命名已按 `D896` 要求含规范前缀 `hh341_small_construction_site_`（上轮 `micro_R15x.txt` 瑕疵已修）。

**复现命令**：
```powershell
pwsh -NoProfile -File 'Logs\_bridge_call.ps1' -Payload 'Valley Rampart\Logs\hh341_small_construction_site_micro_R15x.json' -TimeoutMs 900000
pwsh -NoProfile -File 'Logs\_bridge_call.ps1' -Payload 'Valley Rampart\Logs\hh341_small_construction_site_micro_C1x.json'  -TimeoutMs 900000
```

## 四、合规
- `git diff --name-only -- Assets/_Game` ⇒ **hit_lines = 0**（空）；`Assets` 全域仅既有脏点 `Scenes/GameScene.unity`。
- **HEAD = `0a33d65b`**（＝ `D896` 落账 commit）。
- ⛔ 未改 `taskTimeout`／`Complete` 判据／未并 `DZ-7`／未取新号／未申请下一源／未代写策划端账本（`D767`）。
- ⚠️ `Repathing` 不计失败、可能形成跨源静默循环的候选缺陷 ⇒ **另行登记，本轮不修**（符合 `D896`）。

## 五、自报瑕疵（本批 4 条）
1. ⚠️ **PowerShell 内联 `python -c "…"` 连续 3 次因引号转义失败**（`\"` 被 PowerShell 破坏）⇒ 耗时 3 轮；**教训：一律写 `.py` 文件执行**（本轮最终以此成功）。
2. ⚠️ 臂 C 首次因「**C 脚本落后于 R**」（派生内联失败未生效，缺兜底选工）提前退出（375 B）⇒ 重派生后成功；**教训：派生脚本必须断言关键锚点（`if (focus == null)`）**。
3. ⚠️ 臂 C 首次循环上限 3000 tick（1x ≈ 300 s 现实）触发 bridge 超时 ⇒ 改为 900 tick；**教训：1x 臂须按现实秒折算循环上限**。
4. ⚠️ 控制台中文乱码 ⇒ 统计器改 **ASCII 标签**输出（见 `…_stat.py`）。

## 六、结论与状态
- 本批**闭合** `D896` 任务一（R 重跑）、任务二（C 补跑并登记「未出现」）、任务三（读数登记含作用域）。
- ⛔ **首源源级＝未通过 · 停手待裁**（四项门槛不变；本批读数不能判绿）。

## 【请裁】
1. 上轮 `Failed`×240／`Unreachable`×10／`wpLen=1`×10／单位不可走微格 `False`×259 的模态，是否**另立「不可走微格起点的失败链」专项**（与 `ConstructionSiteStore` 解耦）？
2. 臂 C 需否**改在「确认进入任务段后再起算 30 游戏秒」**重跑（本轮 C 全程 `wState=None`×90 ⇒ 对照空转）？
3. `L-97` 三件套是否**定为后续所有"未取得"条目的固定格式**？
