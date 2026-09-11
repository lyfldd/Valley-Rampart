# HH.215 任务书：军事期门槛映射层治本批（D348 兵力目标接入内源节拍 ＋ 资源链/焦点霸占先诊断）

> 签发：策划端 2026-09-11（**D663**）｜ 执行端：TraeCode ｜ 性质：**结构治本批**（限 `Systems/AI/KingdomBrain/` 效用层 ＋ `SituationConfig` SO）
> 依据：**D663 主阻断裁决**（HH.214 §三-1/§九-1）· **D589⑥⑧＋D590④**（内源节拍：阶段推进不得以外部袭扰为唯一源）· **D648/D649**（先诊断后参数，禁参数微调找补）· 缺陷台账 **DZ-068**（深化）
> 教训引用（钩子1）：**L-30**（判定线硬条件可达性前置，本次已勾选 §三.0）／**L-31**（同源 helper，禁另抄）／**L-25**（判据直读 gate 字段）／**L-15**（口径全部落点回查）／**L-29**（阳性对照）／**L-28**（枚举/字段直读）
> 完成报告：执行端按账本实时水位线取号（**D640 #10 禁预留**）
> 顺序：**本批（验收成立）→ 七考重验（长局 ≥2 AI，另批）**

## 〇、背景（HH.214 实证·非执行端缺陷）

七考重验（HH.214）判定线未 PASS，**主阻断被精确定位为结构性**：军事期迁移硬条件 `warriorCount >= expandToMilitary_warriorsMin(=4)` 在零/低威胁环境下**结构不可达**。

- 闸门：[ScriptStageMachine.cs:96-101](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ScriptStageMachine.cs#L96-L101)（扩张→军事需 `warriorCount>=4`）；`KingdomBrainConfig.cs:47`（`=4`）。
- 目标：[UtilityScorer.cs:372-378](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L372-L378) `D348Target = clamp(2+⌈威胁×3⌉+阶段系数, 2, 2+工人)`；Expand 期阶段系数=+1 ⇒ **threat=0 时 target=3 < 门(4)**。
- 铁证：k3 建军链全通（⑰a/⑰b `need=0` 93/87 天＋⑦ `feasible` 14 天）**止步 `warrior=3`**。
- **根因认定**：⑯⑰ 军事缺口已接 `InternalDrive`（`UtilityScorer.cs:305/313/342`，2_22 P0 批B / D590），**但 ⑦ 的目标值来源 `D348Target` 仍纯威胁驱动、无内源项** ⇒ 已裁「内源节拍」在此处**漏落**（D590「无袭扰环境不恒死滞」未落到兵力目标公式）。

## 一、修什么

### A. 主修（结构·治本）＝ **`InternalDrive` 接入 `D348Target`**

1. 让**零/低威胁环境下 Expand 期基线目标可达门**（`target ≥ expandToMilitary_warriorsMin`）：把内源势能项纳入兵力目标（与 ⑯⑰ **同源 helper**，`L-31` 禁另抄）。
2. **量级＝数值禁区下沿**：`SituationConfig.internalDriveWeight` 初始 0.1（**只接结构不调值**）；执行端须**实证**零威胁下 `target` 可达门。若 0.1 经接线后仍不足（含 `⌈⌉` 边界），**报裁**再定是否 SO 侧微调——**禁擅调**（D563③ / D590 数值禁区）。
3. **备选（仅在 A+ 实证仍不足时启用）**：Expand 基线对齐门（`militaryTargetFloor 2→3` **或** `expandToMilitary_warriorsMin 4→3`，**二选一**）——**须回写设计稿**（`D324`/`D348`/`2_17` §3.1.1）并说明「无威胁不爆兵」语义如何保留。**优先 A+ 单点结构修**。

### B. 并诊断（先诊断后修）＝ **资源链断裂**

- k1 密林 `Stone prod=0`（存量 3）⇒ 建不了兵营（需 10 石）⇒ ⑦ 无建筑前置；k2/k4 粮链死（`grainDays≈0`）⇒ 早灭（D60/D48）。
- **只诊断不擅修**：出根因（生成 / triage / 派工面）＋ `file:line` 锚点；与 HH.193 §七-3 同项比对。**诊断出结构性根因方可同批修**，否则报裁另立批。

### C. 并诊断（先诊断后修）＝ **焦点霸占 `BuildWall`**

- census top 占顶：k1 **97/120**、k2 **115/120**、k4 **107/120** 天（k3 仅 8）。
- **与 HH.189「wall 选址失败 181 次」同族但机理待辨**：`D654` 曾裁 181＝几何特定非共因；本轮为「评分长期占顶」（另一形态）⇒ **须先诊断是否同因，禁预设**；出根因＋`file:line`。

## 二、禁（红线）

- **禁参数微调找补**（D563③）——本批是**结构接线**（内源节拍补落），不是调 K/权重。
- **禁擅改 `internalDriveWeight` 值**（数值禁区，需实证后报裁）。
- **AI.Core 零直改**：先核 sim-sync（同 D656 先例——`Systems/AI.Core/` 与 `Systems/AI/KingdomBrain/` 不同程序集；若 grep 命中须转 sim-sync 跨仓）。
- **正门唯一入口** `TestHarnessApi.EnterTestRun`＋守卫全开＋**15x**；短局即可；**收工退 Play**（L-32，实测 `isPlaying=False`）。
- 确定性：同 seed 短局双跑**干净局对称**（L-22，每轮退 Play 重进）；长局口径＝**关键事件级复现**（D657⑥，不承诺逐值）。
- 合法/资产：`git status Assets` 仅本批文件；写-改-commit 同串、只提本串、**不 push**。
- **本批不重跑七考**（短局机制级自证；长局 ≥2 AI 归后续七考重验批）。

## 三、必带证据（验收句）

### 0. 🔴 **L-30 勾选清单（本次签发强制·判定线硬条件 gap 表）**

签发侧已逐项直读证明：**gap 未闭 ⇒ 先修本批再跑**。

| 硬条件 | 当前值（HH.214 实盘） | 目标值 | gap |
|---|---|---|---|
| `warriorCount ≥ 4` | **max 3**（k3 恒 3） | ≥4 | **未闭（主阻断）** |
| `populationCount ≥ 12` | k1 工 12（达标）；余国早灭 | ≥12 | 未闭（受资源链 (B) 制约） |
| `expansionChunks ≥ 2` | 未见阻塞证据 | ≥2 | 待补证 |
| **≥2 AI 达军事期** | **0 / 4** | ≥2 | 未闭 |

### 1. A+ 正向实证（答主阻断）

- 同 seed 短局（正门＋15x，槽如 `p1_fix1`）：**零/低威胁段 `target ≥ 4` 实读**（`[DiagMilitary] militaryTarget=` 逐日）＋**「剧本阶段 → 军事」日志 ≥1**。
- 负探针：`internalDriveWeight=0` 时**死滞复现**（target 退回 ≤3，无 →军事）——对齐 D590 负探针锚。

### 2. (B) 资源链诊断报告

- k1 `Stone prod=0` 根因（生成/triage/派工）＋ k2/k4 粮链断根因；`file:line` 锚点；**是否结构性**结论。

### 3. (C) 焦点霸占诊断报告

- `BuildWall` 长期占顶根因；与 HH.189 wall181 **同因/异因**结论；`file:line`。

### 4. 冒烟零退化

- 既有 `Smoke_2_22P0` 32/0 ＋ `Smoke_2_23RP0` 27/0；**若 `D348Target` 改动影响既有 `#5` 兵力目标探针，须同步更新/新增 D348 探针并附正负例**（L-31 双向）。

### 5. 字段/枚举直读（若动 SO / 枚举）

- `SituationConfig` 相关字段实读值；枚举若有新增须**尾插**（L-28）。

## 四、批序

**开工回执（含 §三.0 L-30 勾选清单复核）→ 停手待策划确认 → 实施 → 交付报告（含 §三 全部证据）→ 策划端验收**；验收成立 ⇒ **七考重验（长局 ≥2 AI 自证 P1 终验收）**起跑。

---
*签发：策划端 2026-09-11（**D663**）。0.6 §一百九十二 已落档；`DZ-068` 深化；`DZ-106`/`DZ-107` 新增。*
