# HH.227｜①-b 追加项（达上限强制让位＋两护栏／J7 同段基线／⑥ 分型标注）（交付报告）

> 类型：**交付报告** · 状态：🟡**待验收** · 作业依据：**HH.226 §裁决区（D678）**
> 执行端（TraeCode·Unity 轨）· 2026-09-12 · 取号：遵 D640 #10（水位线 HH.226 → **HH.227**，独立单行 commit `651b034`）
> 范围＝D678 §追加项范围 4 条（①-b 闭环 ＝ ②七考起跑前置）

---

## 一、实施（4 条逐条兑现）

| # | 追加项 | 落点与实现 |
|---|---|---|
| 1 | **达上限强制让位 ＋ 两护栏**（D678 破框裁） | [UtilityScorer.ScoreTop:118-172](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L118-L172)：**选择层**双 argmax——`best/top`（跳过被出池者）＋`bestAny/topAny`（含被出池者）；命中 `IsCapped` ⇒ `census.evicted++` 并出池。**兜底回池**＝出池后无任何入选候选 ⇒ 取 `topAny`（**不空转**）＋`census.evictedFallback=true`。**自愈回池**＝成功／need 变化／冷却（与 `Factor` 同口径）⇒ 条目清 ⇒ `IsCapped=false`。**非 `Feasible`**（守硬约束①）。判据口 [ActionBackoff.IsCapped](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ActionBackoff.cs#L77-L96) |
| 2 | **J7 基线改同段**（＋HH.203 120 档口径） | [Valley_HH80_Run.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L41-L60)：常量改为**修前逐日序列** `BASE_WALL_TOP_DAYS[国][日]`（源 `p1_fix1` 同日志源）；判定＝`修后同段占比 ≤ 0.5 × 修前同段占比`（**双方均截到当前日**）。J8 同步改同段（修前累计落地按日线性插值 `BaseBuildOkAt`）。**开关 `ENABLE_J7_WALLTOP`** ＋注记：**HH.203（120 档·另一 seed）无同段参照 ⇒ 须重取基线或置 false** |
| 3 | **⑥ 失败分型标注**（不改行为） | `ActionBackoff.FailKind{Self,Env}`＋条目 `lastEnv`；`ReportFail(...,kind=Self)`；`Readout` 输出 `BuildWall:0.25(Self)`／`RecruitWorker:0.25(Env)`。⑥ 无候选处标 **Env**（[KingdomBrain.cs:847](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L847)，HH.28 裁决① 环境让渡）。**退避计数与因子行为零改变**（D678 裁：不问归因） |
| 4 | **复验** | ①`census` 读数加 `ev=<出池数>`（`!`＝兜底回池触发）②`Smoke_5` case 扩 3 组断言（让位门／三条回池／分型）③同 seed **同段短窗对照跑**（槽 `p1_fix3`，D2~D60）见 §三 |

**②起跑前置兑现**：`USE_DIAG_WINDOW` **已置 false**（[`:38`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L38)，回**主档 120/2**；D678 勘正⑥ 兑现）；`MILITARY_STOP_COUNT=2`／`CIRCUIT_BREAK_DAY=120` 早前已复原。

---

## 二、门禁

| 项 | 结果 |
|---|---|
| 编译 | **0 错**；**本批改动文件零 warning**（回落到 6 条既有历史 warning；期间引入的 `base` 关键字误用编译错已即时修掉） |
| `Valley2_17_Smoke_5` | **ALL PASS**，新断言全绿：`退避正负例=OK(… /让位门=True/回池=True/分型True(BuildWall:0.75(Env)))` |

---

## 三、①-b 同段对照（**修前 `p1_fix1` vs 修后 `p1_fix3`，均 D2~D60 · 59 样本/国**）

| 判据 | 修前 | 修后 | 变化 |
|---|---|---|---|
| **census `top=BuildWall`** k1 | 37/59 = 62.7% | **26/59 = 44.1%** | **−29.7%** |
| k2 | 47/59 = 79.7% | 47/59 = 79.7% | 0% |
| k4 | 47/59 = 79.7% | **40/59 = 67.8%** | **−14.9%** |
| **合计（k1+k2+k4）** | 131/177 = **74.0%** | 113/177 = **63.8%** | **−13.7%** |
| （k3·排除项） | 9/59 | 10/59 | +1 |
| **建造落地总数** | **32** | **61** | **+90.6%** ✅ |
| **wall 选址失败**（k1/k2/k4） | 34/44/13 ＝ **91** | 12/27/7 ＝ **46** | **−49.5%** ✅ |

**机制读数（实跑）**：`ev>0`（让位出池）**31 次**；**兜底回池触发 21 次**；`avoid` 分型 **RecruitWorker(Env) 68 行 ／ BuildWall(Self) 60 行**；`BACKOFF_CAP` 出现。
**✅ 同段基线修正的正面证据**：本跑 **J7 未触发** ⇒ 窗口满 D60 收工（旧全窗口径会在此跑 D31 假阳性停 ⇒ 假阳性根因已消除）。

### 3.1 机理诊断（①-b 靶为何未达 −50%）

**「让位 ⟷ 冷却自愈」循环**：⑨ 达 12 次失败 ⇒ 出池（当轮不进 argmax）⇒ **出池期间不再产生新失败 ⇒ 冷却 5 日到期 ⇒ 条目清空 ⇒ 回池** ⇒ 重新尝试 ⇒ 再累计 12 次失败 ⇒ 再度出池。
⇒ ⑨ 呈"**出池 ~5 日 ／ 在场 ~12 日**"的周期 ⇒ 占比降幅有限；**但出池窗口内其他建造行动登顶并落地** ⇒ **落地 +90.6%**（真实收益）。**k2 占比 0% 变动**＝其 `⑨` 在窗口内未稳定进入"出池态"（故障集中在 k1/k4）。

---

## 四、验收句① 判定（①-b 后）

| # | 句 | 判定 |
|---|---|---|
| ①-a | `wall` 选址失败**显著下降** | ✅ **达成**（91→46，**−49.5%**；较前批 −33% 进一步改善） |
| ①-b | census `top=` 中 **wall 占比下降** | ⚠️ **部分达成**＝总体 **−13.7%**（k1 −29.7%／k4 −14.9%／k2 0%），**未达 −50% 靶** |
| ①-c | **建造落地数不降** | ✅✅ **达成**（32→**61**，**+90.6%**） |
| ② | 边界/负探针（退避生效＋可恢复） | ✅ 达成（`Smoke_5`：让位门/自愈回池三条/分型 全过；前批自愈循环实证在案） |
| ③ | 回归 | ✅ 达成（编译 0 错；`Smoke_5` ALL PASS；`2_22P0` 32/0·`2_23RP0` 27/0 见前批） |

**⇒ 结论：机制级（让位／兜底／自愈／分型）全项达成；端到端 `占比` 降幅显著但未达原靶。**

---

## 五、报裁项（3 项 ＋ 勘正认账）

1. 🔴 **①-b 靶值失真（请重定）**：原 −50% 靶建立在**"压 ⑨ 自身 `factor` 即让位"**这一**已被证伪**的前提上；实际机制是"让位⟷冷却"循环（§3.1）⇒ 占比降幅天然有限，而**落地 +90.6%** 才是真实收益。
   **建议口径**：(a) 改为「**落地数上升 ＋ 占比不升**（或按国分列）**不设固定靶**」；或 (b) 如需更大占比降幅 ⇒ **数值面**调 `hardCapFails`↓（提前出池）／`cooldownDays`↑（延长出池期）——**属数值面，执行端不擅动**（D563③）。
2. **J7 在 HH.203 的口径**：120 日档 ＋ 另一 seed ⇒ **无同段参照** ⇒ 该批须**重取基线**或将 `ENABLE_J7_WALLTOP` 置 false（机制已备，开关在案）。
3. **②起跑剩余前置（报备，执行端可直接执行）**：① `SEED/SLOT` 按 HH.203 任务书设（73621／p1_run8）；② **`JUDGE_FOCUS_KINGDOM` 须随 seed 重定**（换 seed ⇒ "唯一可至军事期国"可能变；现=3 是 **seed64513** 的定案）；③ `USE_DIAG_WINDOW=false` ✅已兑；④ `120/2` 档 ✅在位。
4. **D678 §勘正 7 处：全部认账**（见 [HH.226 报告 §勘正认账区](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/执行端/HH.226_焦点霸占治本批·执行失败退避_交付报告.md)），其中勘正①（`hardCapFails` 不参与因子计算＝升级报裁触发线＋让位门线）已在源码文档与本次实现中更正。

---

## 六、声明与红线自检

**声明**：本批**未经 sim 门禁**（D675 裁⑥延续；`AI.Core` 与训练仓对王国脑层双端零命中）；冒烟本轮只跑 `Smoke_5`（`2_22P0`/`2_23RP0` 见 HH.226 §三，本轮改动面＝纯选择层＋观测域 ⇒ 未重跑，**如需重跑请示下**）。
**红线**：设计文档／队列**零改动**｜业务码改动面＝AI 决策域（`UtilityScorer`/`ActionBackoff`/`KingdomBrain`）＋观测域 |**AI.Core 零触碰**｜**已退 Play 实测 `isPlaying:false`**（L-32）｜数值全 SO 外置、未做调参找补（D563③）｜**未 push**。

**证据**：`Logs/P1/p1_log_20260912_113943.log`（修后 p1_fix3 D2~D60）／`p1_log_20260911_231630.log`（修前 p1_fix1）／`hh80_run_status.log`（窗口满 D60 收工原文）／`Saves/p1_fix3_day*`。

---
*执行端 2026-09-12（HH.227 交付报告，取号 `651b034`）。停手待策划端验收。*
