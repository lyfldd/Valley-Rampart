# HH.341 · 小源首源 —— **D895「微格路径点失效与重寻无效」只读定位** 交付报告

> 执行端 ｜ 2026-09-27 ｜ 沿用 `HH.341`（⛔ 未取新号、⛔ 未写 D 号）｜ 裁定：`D895` · 落账 commit **`127a6ee9`**
> 性质：**只读定位 ＋ 臂对照**（⛔ 不判绿、⛔ 不出修法）｜ ⛔ 未提交、未 push
> ⚠️ **本报告不含判定字样**（⛔ 不写"已修复/已启用"），读数**逐点原样**

---

## 〇、三分栏（`D895` 指定格式）

### ⛔ 已证（本轮新增硬读数 · 逐点原样）
| # | 读数（原样） | 证据位置 |
|---|---|---|
| 1 | ⭐ **格级 `IsWalkable` 与微格级 `IsSubWalkable` 语义不同**：同一点 `wp0=(245,261)` ⇒ `wp0.IsSubWalkable=**True**` ∧ `wp0.格级IsWalkable=**False**`（多帧一致，样本 t=1646.91/1651.91/1655.82） | `micro_R15x.txt` |
| 2 | **`waypoints[0]` ＝ A* 求解时的起点微格，非"单位当前微格"**：`wp0=(245,261)` 恒定（另一段 `wp0=(217,224)`／`(221,221)`），而单位微格序列 `(231,247)→(222,238)→(221,228)→(220,222)→(229,217)` 持续变化，`_wpIndex` 同步 `15→25→35→5→10` | 同上 |
| 3 | **路径走完的合法终态签名**：`_path=null`（`status=obj=null`）∧ `State=Idle` ∧ `_wpIndex=0` ∧ 世界坐标多帧恒定 `(2.24,35.84)` ∧ `wState=None 在册=无` | 同上（t=1676.02 起） |
| 4 | **`waypoints.Length` 确已可直取**（证实 `D895` 修正）：取到 `wpLen=50`／`17`／`13`，`status=Ready`，`reachedExact=True` | 同上 |

> ⛔ 以上均为**非任务段**（`在册=无`）路径上的读数；**不用于**对"微格路径点失效/重寻无效"作任何结论。

### ⭐ 已排
- **本轮无新增排除项**。`D895` 已排除的两条（速度链闭合／两到达判据分歧）沿用，未再取证。
- ⚠️ 本轮窗口内 `Repathing` 累计 = **0** ⇒ **未复现停摆** ⇒ ⛔ 不对停摆作任何排除或确认。

### ⚠️ 未取得（单列 · 附原因；⛔ 不以「无」充数）
| # | 未取得项 | 原因（读数级） |
|---|---|---|
| 1 | **任务段内 5 项读数**（①微格+`IsSubWalkable` ②`wp0` 值与可走性/是否=① ③段内 `wpLen/status/reachedExact` ④`PathfindingService` 复算 ⑤首次 `Following→Repathing` 快照） | **触发投料态后工人未被派工**：全程 `wState=None 在册=无`；对照工人 `npc53` 另有非任务路径（`PF.State=Following`，`_wpIndex` 递增）⇒ **任务段未开始** ⇒ 无段内读数 |
| 2 | **臂 C（1x）全部读数** | **未执行**（本轮先跑臂 R；R 未取得任务段 ⇒ C 缺同构基准）⛔ 如实登记 |
| 3 | **调用次数**（`NavigateToSource`／`EnsurePfAndSetDest`／`BehaviorExecutor.NavigateTo`／`Brake`／`RequestPath`） | **结构性不可得**（`D895` 禁改生产方法／运行时打补丁／埋点；⛔ 不得用静态代码读数替代运行时计数） |

---

## 一、探针与 JSON 一一对应清单（`D895` 必附）

| 臂 | 脚本 | `MODE` 常量行 | JSON | 输出 |
|---|---|---|---|---|
| **R15x** | `Valley Rampart/Logs/hh341_small_construction_site_micro_R15x.cs` | `:8 const string MODE = "R15x";` ＋ `:9 const float SPD = -1f;` ＋ `:10 const string TAG = "micro_R15x";` | `…_micro_R15x.json`（`request_id=hh341microR15x`） | `Valley Rampart/Logs/micro_R15x.txt`（99,695 B / 312 行） |
| **C1x** | `Valley Rampart/Logs/hh341_small_construction_site_micro_C1x.cs`（由 `…_micro_derive.py` 从 R 派生，断言差异仅 3 常量） | `:8 const string MODE = "C1x";` ＋ `:9 const float SPD = 1f;` ＋ `:10 const string TAG = "micro_C1x";` | `…_micro_C1x.json`（`request_id=hh341microC1x`） | ⚠️ **未生成**（未执行） |

⭐ 对 `D895` 所指硬伤的处置：`D894` 的 `…_idle_probe.cs:8` 现为 `MODE="C1x"`（与所存 `…_idle_R15x.txt` 不符）⇒ 本轮**不再使用该可变脚本**，改为 **R/C 各自独立 `.cs` ＋ 独立 `.json`**，常量逐臂写死（上表可逐项核对）。

**读数级复现命令**：
```powershell
pwsh -NoProfile -File 'Logs\_bridge_call.ps1' -Payload 'Valley Rampart\Logs\hh341_small_construction_site_micro_R15x.json' -TimeoutMs 900000
```

---

## 二、合规声明
- **生产码 diff ＝ 0**：`git diff --name-only -- Valley Rampart/Assets/_Game` ⇒ **命中行数 0**；`Assets` 全域仅既有脏点 `Scenes/GameScene.unity`（⛔ 非本批）。
- **HEAD ＝ `127a6ee9`**（＝ `D895` 落账 commit）。
- ⛔ 未改 `taskTimeout`／两段预算／`Complete` 判据；⛔ 未并 `DZ-7`；⛔ 未申请下一源；⛔ 未代写策划端账本（`D767`）。
- 证据落 `Valley Rampart/Logs/hh341_small_construction_site_micro_*` ＋ 输出 `Valley Rampart/Logs/micro_*.txt`（⛔ 非仓库根 `Logs/`）。

---

## 三、自报瑕疵（3 条 · 均为本轮实测发现）

1. ⚠️ **`WaitForSeconds` 受 `timeScale` 影响 ⇒ 窗口被压缩 15 倍**：`WaitForSeconds(0.1f)` 在 15x 下仅 ≈6.7 ms 现实时间 ⇒ 3000 次采样只覆盖 **≈20 游戏秒**（探针总耗时 58 s 含建局）⇒ 这是"未覆盖到任务段"的**直接技术原因**（下一轮应改 `WaitForSecondsRealtime` 或按 **tick 计数**驱动）。
2. ⚠️ **落盘命名未含规范前缀**：`TAG="micro_R15x"` ⇒ 实际文件 `Valley Rampart/Logs/micro_R15x.txt`（应为 `hh341_small_construction_site_micro_R15x.txt`）。本轮保留原名并在此声明，下轮修正。
3. ⚠️ **本轮未复现任务段** ⇒ `D895` 必补 5 项**未闭合**；报告不做任何根因判定。

## 四、源级结论
⛔ 首源仍为 **未通过 · 停手待裁**（`Complete` 四项未取得；本批只读定位未闭合 `D895` 必补 5 项）。

## 【请裁】
1. 是否按上述两条**技术原因**重跑臂 R —— 建议：①采样改 `WaitForSecondsRealtime`／tick 驱动以覆盖**至少 1 个完整 30 游戏秒任务段**；②触发后**轮询等待派工发生**（`wState=MovingToSource`）再起算窗口；③`TAG` 补全规范前缀；
2. 臂 C 本轮未执行 ⇒ 是否与重跑的 R **同批补跑**（`D895` 要求 C 只跑一个完整 30 游戏秒段 ＋ 2~3 次 `Repathing` 循环）；
3. 本轮新增已证读数（格级 vs 微格级语义差异、`wp0` 为求解时起点）是否**登记入账**供后续批引用。
