# HH.216｜军事期门槛映射层治本批 · 开工回执（协议核对闸 ＋ §三.0 L-30 勾选清单）

> 执行端（TraeCode·Unity 轨）· 2026-09-11
> 状态：🟡**开工中·停手待策划确认**（HH.215 §四 批序：开工回执 → 停手待策划确认 → 实施）
> 锚点：**HH.215 任务书（D663）**为唯一权威 ｜ 依据 **D663 主阻断裁决（beyond-options 自造 A+）** ｜ 前置事实＝**HH.214 七考重验 FAIL 实证**
> 取号：遵 D640 #10（水位线 HH.215→**HH.216**，独立单行 commit `3c94e45`）；完成报告届时按实时水位线另取（**禁预留**）

---

## 〇、执行前预检（execute-checklist §步骤1）

| 项 | 实读 |
|---|---|
| 基线工作区 | `git status --short -- Assets` ＝ **空**（HEAD `26e47be`，前台 D663 裁决后干净基线） |
| 批范围（授权域） | `Assets/_Game/Systems/AI/KingdomBrain/`（效用层）＋ `Assets/_Game/Data/Kingdoms/SituationConfig.cs`/SO ＋ **观测域**（`Assets/Editor/Smoke/`，列报） |
| 依赖 | HH.214 已交付（FAIL 实证）✅；HH.208 ⑰收口批已验收（D661）✅；无未闭前置 |
| 基线编译 | 实施前先跑编译记基线（新增错误＝0 口径；见 §五-4） |

## 一、现状直读（代码锚点，全部实读非转述）

| 落点 | 实读内容 |
|---|---|
| [UtilityScorer.cs:372-379](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L372-L379) | `D348Target(warrior, worker, neighborMilitary, stageFactor, cfg)`＝`clamp(floor + ⌈threat×scale⌉ + stageFactor, floor, floor+worker)`；**纯整数核、零世界耦合、无内源项** |
| [UtilityScorer.cs:349-350](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L349-L350) | `MilitaryTarget(k,cfg) => MilitaryTargetFromThreat(k, NeighborMilitary(k.id), cfg)` |
| [UtilityScorer.cs:361-366](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L361-L366) | `MilitaryTargetFromThreat` 自算 stageFactor（存活/发育 0／**扩张 +1**／军事 +stageFactor） |
| [UtilityScorer.cs:415-424](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L415-L424) | `InternalDrive(k)`＝`internalDriveWeight × clamp01(0.5·DriveEconomic + 0.3·DrivePopPressure + 0.2·DriveStorage)`，**≤ 0.1**；**已接 ⑯ ⑰**（[:305](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L305)/[:313](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L313)/[:342](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L342)） |
| [KingdomBrainConfig.cs:47](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/KingdomBrainConfig.cs#L47)／`:83`／`:87` | `expandToMilitary_warriorsMin=4`／`militaryTargetFloor=2`／`militaryExpandStageFactor=1` |
| [SituationConfig.cs:37](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/SituationConfig.cs#L37) | `internalDriveWeight = 0.1f`（SO 缺省；`Load()` 走 `Resources.Load("Config/SituationConfig")`，asset 路径＝`Assets/_Game/Resources/Config/SituationConfig.asset`） |
| [ScriptStageMachine.cs:96-101](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ScriptStageMachine.cs#L96-L101) | 扩张→军事三条件（`warrior≥4`＋人口≥12＋占区≥2） |
| 消费点（回归面） | ⑦ 需 [`UtilityScorer.cs:214/451`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L214)、⑰ 判 [`UtilityScorer.cs:242`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L242)、**执行闸** [`KingdomBrain.cs:897`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L897)；**冒烟** `Valley2_17_Smoke_5.cs`（**纯函数 5 参**，L40/53/56/59/73）＋ `Smoke_2_22P0.cs:224`（**世界耦合 `MilitaryTarget`**） |

**漏落认定复核（同意 D663）**：`InternalDrive` 已落 ⑯⑰ 评分面，**未落 ⑦ 的目标值来源 `D348Target`** ⇒ 零威胁 Expand 期 `2+⌈0⌉+1=3 < 4`。

## 二、修法 A+（结构接线·具体方案，请确认）

### 2.1 接线（3 处，最小面积；`InternalDrive` 同源 helper 不另抄，L-31）

| # | 落点 | 改法 |
|---|---|---|
| 1 | `D348Target(...)` | **尾插**参数 `float internalDrive = 0f`（L-28 尾插精神；默认 0 ⇒ 既有 5 参调用零退化）<br>公式：`target = floor + Mathf.CeilToInt(threat*scale + Mathf.Max(0f, internalDrive)) + stageFactor`，clamp 不变 |
| 2 | `MilitaryTargetFromThreat(k, neighborMilitary, cfg)` | **尾插** `float internalDrive = 0f` 并透传 |
| 3 | `MilitaryTarget(k, cfg)` | 改为 `MilitaryTargetFromThreat(k, NeighborMilitary(k.id), cfg, InternalDrive(k))` ← **唯一新增内源项来源＝同源 helper** |

> 语义：内源势能与威胁**同量纲相加**后再取整——威胁面数学不变（**不是调 K/权重**）；`internalDriveWeight` **保持 0.1 不动**（数值禁区）。

### 2.2 算术闭合证明（零威胁 Expand 期）

| 场景 | 代入 | 结果 | 判定 |
|---|---|---|---|
| **正向**（drive>0，实战常态） | `2 + ⌈0×3 + 0.05⌉ + 1` ＝ `2 + 1 + 1` | **4** | **≥ 门(4) ⇒ 闭合** ✅ |
| **负探针**（`internalDriveWeight=0` ⇒ drive≡0） | `2 + ⌈0⌉ + 1` | **3** | **< 4 ⇒ 死滞复现**（负探针锚对齐 D590）✅ |
| 威胁仍有效（结构未动） | 威胁↑ ⇒ `threat×3` 继续抬升 ⇒ 目标单调不降 | — | ✅ |

**关键判定依赖（请知悉/确认）**：正向成立条件＝**drive > 0**（`⌈x⌉=1` 对任意 `x∈(0,1]` 成立）。**实证支持＝HH.214 实盘**：k3 `⑯训练将军 need=1.051`（＝缺口 1.0＋drive **≈0.051**）、k4 `need=1.050`、`⑰d need=1.051` ⇒ **实战局 drive≈0.05>0 常态成立**（非理论假设）。若某国全部内源项为 0（"满足国"），目标仍为 3 ⇒ **语义即 2_17 §3.1.3「无威胁不爆兵」**，不视为回归。

### 2.3 若 A+（0.1）实证仍不足 ⇒ 停手报裁（禁擅调）

触发条件（预设）：短局实盘出现「drive>0 但 `target` 仍 <4」或「目标 ≥4 但 ⑦ 未招满至 4」。届时**停手写 HH 报裁**，按 HH.215 §一 A.3 备选（`militaryTargetFloor 2→3` 或 `expandToMilitary_warriorsMin 4→3`，**二选一＋回写设计稿**）请裁。

## 三、§三.0 L-30 勾选清单：判定线硬条件「当前值 vs 目标值」gap 表（**本批首次强制落地**）

| 硬条件 | 当前值（HH.214 实盘） | 目标值 | gap | 本批如何使其闭合 |
|---|---|---|---|---|
| **`warriorCount ≥ 4`** | **max 3**（k3 D60 后恒 3；k1 恒 0） | ≥4 | **未闭（主阻断）** | **A+ 结构闭**：零威胁 Expand 基线 3→**4**；⑦ 两道闸（评分 `warrior<MilitaryTarget` [`:451`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L451) ＋**执行闸** [`KingdomBrain.cs:897`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L897)）随之放开 ⇒ 可达 4。**闭合证据＝短局 `[DiagMilitary] militaryTarget=` ≥4 实读 ＋ k3 类王国 `warrior` 达 4 ＋「剧本阶段 → 军事」日志 ≥1** |
| **`populationCount ≥ 12`** | k1 工 12 ✅；**k3 D70~80 工 9＋战 3 ＝ 12 ✅**（招兵为工→战**直转**，人口总量不降）；k2/k4 早灭（D60/D48） | ≥12 | **部分闭** | k1/k3 **实测已达 12 ⇒ 该项对 k1/k3 已闭**；k2/k4 受 **(B) 资源链**制约（本批**只诊断**）⇒ **该项最终闭合与否取决于 (B) 诊断结论**（结构性则同批修，否则报裁另立批） |
| **`expansionChunks ≥ 2`** | k3 领土 mid **31**、k1 **32**（D120 CSV） | ≥2 | **已闭** | 实读已闭（短局以 `[DiagMilitary]` + 领土列复证） |
| **`≥2 AI 达军事期`** | **0 / 4** | ≥2 | 未闭 | **本批不跑长局**（HH.215 §二）：短局**机制级自证**（≥1 次「→军事」）；**长局 ≥2 归后续七考重验批** |

**⇒ 结论（L-30 硬性检查口径）**：主阻断项 `warrior≥4` **本批结构闭合**；`population/chunks` 中 chunks 已闭、population 对 k1/k3 已闭（余国挂 (B)）；末端「≥2 AI」属长局观察项，按任务书归后续批。**故本批可实施，无需先补前置。**

## 四、(B)(C) 诊断计划（先诊断后修；只出根因＋`file:line`，不预设）

- **(B) 资源链断裂**：k1 `Stone prod=0`（存量 3）／k2·k4 粮 `grainDays=0`。诊断面＝**生成面**（资源点/矿点生成与等级）→ **triage 面**（`[DiagTriage]` ⑤三通道）→ **派工面**（`[DiagBias]`/产能 `[DiagCapacity]`）；出「是否结构性」结论＋与 **HH.193 §七-3**、**DZ-106** 比对。**禁预设**（不假定＝生成缺陷）。
- **(C) 焦点霸占 `BuildWall`**：k1 97/120、k2 115/120、k4 107/120 天占 census top。诊断面＝⑨ `BuildWall` 的 need/axis/stageW 与 `CountForts` 上限（[`UtilityScorer.cs:460-468`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L460-L468)）＋ **D654「181＝几何特定非共因」复核**；出「与 HH.189 wall181 **同因/异因**」结论＋`file:line`。对应 **DZ-107**。

## 五、验收句兑现方案（HH.215 §三 → 执行方案）

| # | 验收句 | 执行方案 |
|---|---|---|
| 1 | **A 正向实证** | **正门 `TestHarnessApi.EnterTestRun`＋15x**，短局 **60 日窗口**（D320「进军事期≈20~30 日」＋HH.214 k3 达 warrior=3 于 D60 ⇒ 40 日窗口风险偏高，取 60 日余量；达标可提前收工）：<br>①`[DiagMilitary] … militaryTarget=` **逐日实读，零/低威胁段 ≥4**；②k3 类王国 `warrior` **达 4**；③**「剧本阶段 → 军事」日志 ≥1**（同时抓「军事期达标」行） |
| 2 | **负探针** | 同 seed、同槽族（`p1_fix1b`）：把 **`SituationConfig.asset` `internalDriveWeight` 临时置 0**（**测试配置件、非设计值改动**；跑后**复原并 git 校验**）⇒ 预期 `target` 退回 ≤3、**无 →军事**（死滞复现，对齐 D590 负探针锚）。**改动面列报＋复原证据随交付报告** |
| 3 | **(B)(C) 诊断报告** | §四；随交付报告出「根因＋file:line＋是否结构性」 |
| 4 | **冒烟零退化** | `Smoke_2_22P0` **32/0** ＋ `Smoke_2_23RP0` **27/0**（**双跑** L-22）；**并按 §三-4 要求同步更新/新增 D348 探针正负例** —— 既有 `Valley2_17_Smoke_5` 五参调用**默认 drive=0 零退化**，另**新增 2 case**：`drive=0.1 ⇒ target=4`（正）／`drive=0 ⇒ target=3`（负）＋`InternalDrive` 在场性断言 |
| 5 | **字段/枚举直读** | `internalDriveWeight` SO 实读＝**0.1**（未改）；本批**无枚举新增**（N/A，L-28 无触发） |
| 6 | **本批不重跑七考** | 短局机制级自证；长局 ≥2 AI 归后续批 ✅ |

**短局 seed 报备（请确认）**：**复用 HH.214 定案 seed `64513`** —— 理据＝**同世界修复前/后直读对照**（HH.214 实盘 target 2~3 → 本批短局 target ≥4），机制级短局不涉长局观测偏差；**若策划端要求换新 seed**，执行端即刻报备新候选（零命中＋阳性对照，L-29）。

**列报（本轮改动面）**：①业务代码 3 处（`UtilityScorer.cs` 内，见 §二）②观测域＝`Valley2_17_Smoke_5.cs` 新增正负例 ＋（如需）`Valley_DiagMilitary` 状态行**加 `drive=` 读数**（只读、零世界写入）③容器级＝`Valley_HH80_Run.cs` 常量（`SEED/SLOT/CIRCUIT_BREAK_DAY=60`，R10 澄清域内）④SO 测试件＝`SituationConfig.asset` 负探针临时置 0（**跑后复原**）。

## 六、sim-sync 核（D656 先例，先行）

| 检查 | 结果 |
|---|---|
| `Assets/_Game/Systems/AI.Core/` 阳性对照 | **35 个 .cs 在场**；`public class` 命中 **5 文件** ⇒ **检索有效**（L-29） |
| 本批 7 符号（`UtilityScorer`/`KingdomBrain`/`InternalDrive`/`NeedScore`/`MilitaryTarget`/`D348Target`/`SituationConfig`） | **全部 0 命中** ⇒ **不同程序集、不涉跨仓同步，无需转 sim-sync** ✅ |

## 七、红线自检

- ✅ **禁参数微调找补**（D563③）：本批＝结构接线（内源项补落）；**`internalDriveWeight` 保持 0.1 不动**（数值禁区）；威胁面数学不变
- ✅ **AI.Core 零直改**（§六 已核）；**同源 helper**（`InternalDrive`）不另抄（L-31）
- ✅ 正门 `EnterTestRun`＋守卫全开＋15x（L-09）；**收工退 Play**（L-32，实测 `isPlaying=False`）
- ✅ 同 seed 短局双跑干净局对称（L-22，每轮退 Play 重进）；**枚举无新增**（L-28 N/A）；检索带阳性对照（L-29）
- ✅ `git status Assets` 仅本批文件；写-改-commit 同串、只提本串、**不 push**；改前必重读磁盘（并发写红线）
- ✅ 若 A+ 实证不足 ⇒ **停手报裁不擅调**；若 (B)/(C) 非结构性 ⇒ **报裁另立批**（不顺手改）

## 八、批序声明与下一步

**按 HH.215 §四：本回执落盘（含 §三.0 gap 表）⇒ 停手待策划确认 ⇒ 实施。** 确认后执行序：

1. 改 `UtilityScorer.cs` 三处（§二）＋探针正负例（§五-4 观测域）→ 编译 **0 新增错误**
2. 冒烟双跑（`2_22P0` 32/0＋`2_23RP0` 27/0）→ 记录
3. 短局正向（正门 15x，seed 64513，槽 `p1_fix1`，60 日）→ 抓 target≥4 ＋ warrior 达 4 ＋「→军事」≥1
4. 短局负探针（SO `internalDriveWeight` 临时 0，槽 `p1_fix1b`）→ 死滞复现 → **SO 复原＋git 校验**
5. (B)(C) 诊断（复用短局 `[DiagCapacity]`/`[DiagChain]`/`[DiagTriage]` 数据＋代码直读）
6. 交付报告（按水位线取号）＋ 策划端验收 ⇒ 验收成立后 **七考重验（长局 ≥2 AI）** 起跑

**请策划端确认**：①§二 接线方案（尾插参数＋`⌈threat×scale + drive⌉` 形态）②§五 seed 复用 64513 ③短局窗口 60 日 —— 确认后我即实施。

---
*执行端 2026-09-11（HH.216）。*

---

## 九、策划端裁决（D664，2026-09-11，主策划端）

> **开工回执验收成立 → 放行实施**。本回执 §三.0 是 `L-30` 升硬性检查项后**首次落地**，gap 表逐项＋闭合路径**扎实**（正面样本）。

**判据三直读（已过·独立复核）**：①HH.215（D663）/0.6 §一百九十二；②代码实读＝`UtilityScorer.cs:372-379/349-350/361-366/415-424`（`InternalDrive≤0.1`）／`:305/313/342`（已接 ⑯⑰）／`:214/235/242/451`（`MilitaryTarget` 消费点）／`KingdomBrain.cs:897`（执行闸）／`KingdomBrainConfig.cs:47/83/87`／`ScriptStageMachine.cs:96-101`；**回归面**＝`Smoke_2_22P0.cs:224`（世界耦合）＋`Valley2_17_Smoke_5.cs`（纯函数 5 参）＋`Valley_DiagMilitary.cs:91`；③字段＝`SituationConfig.cs:37`＋`.asset:21`＝`0.1`。**sim-sync 独立复核**＝AI.Core 对 `InternalDrive/D348Target/MilitaryTarget/UtilityScorer/SituationConfig` **零命中** ⇒ 不跨仓、可直改 ✅。

**三项裁定**：
1. **接线形态＝准**。附**语义注记（须写入交付报告）**：`⌈威胁×scale + max(0,drive)⌉` 使 drive 成**阶跃项**（`⌈ε⌉=1` 对任意 `ε∈(0,1]`）⇒ **`internalDriveWeight` 数值量级对该项不敏感**，§2.3「0.1 若不足」实为 moot；**保留报裁通道，禁据此调权**。
2. **seed 复用 64513＝准**（**同世界修复前/后直读对照**＝机制级对照短局正解；「seed 换新」红线（D657）适用**判定长局**、不适用机制短局——同 HH.202 复用 48903 先例）。
3. **短局窗口＝改 90 日**（执行端取 **60 日不足**：HH.214 k3 `warrior` D40=1／D50=2／D60=3 ⇒ **~7~10 日/兵** ⇒ `warrior=4 ≈ D67~70` ⇒ 「→军事」端到端 **>60**；**90 日留余量**，**见「→军事」日志即提前收工**；`target≥4` 可于 D25~40 先行见证）。

**🔴 新增回归必查（我补·回执未列全）**：世界版 `MilitaryTarget` 在 Expand/Military 段因 drive 生效而 **+1**，其消费点（`UtilityScorer:214/235/242/451`＋`KingdomBrain:897`＋**`Smoke_2_22P0:224`**＋`Valley_DiagMilitary:91`）**行为随之移动** ⇒ **必维持 `Smoke_2_22P0` 32/0 ＋ `Smoke_2_23RP0` 27/0 ＋ `Valley2_17_Smoke_5` 绿**（Smoke_5 五参默认 drive=0 ⇒ 预期零退化；Smoke_2_22P0 世界耦合 ⇒ 须逐条核 target 影响面并列入交付报告）；**任一冒烟滑落 ⇒ 停手报裁，禁就地放宽断言**。

**(B)(C) 诊断计划＝准**（先诊断后修、禁预设、与 HH.193 §七-3／DZ-106／DZ-107 比对）。

**教训补强（`L-30` 实例·时间维度）**：本回执暴露 `L-30` 清单**只核「值 gap」未核「时间 gap」** ⇒ **`L-30` 补一条**：硬条件 gap 表**须含「预计达成日 / 窗口余量」核算**（长局/时间依赖型硬条件），禁只核值不核时（本回执窗口误选 60 即此缺口所致）。

**验收三问**：①发生（唯窗口修正）＝`L-30` 定义**只核值不核时** ②**策划侧流程漏洞**（定义不全，非执行端）③`L-30` 有条且已查（首次落地正面），属定义不完整 ⇒ 补实例、**不新立条目**。

**嘉奖**＝①§三.0 gap 表首次落地扎实 ②sim-sync 先行核带阳性对照 ③负探针设计（SO 临时置 0＋跑后复原＋git 校验）④算术闭合＋判定依赖诚实标注 ⑤主动请确认 3 项不擅动 ⑥消费/回归面列报。

**⇒ 放行实施**（改 3 处＋观测域探针正负例 → 冒烟双跑 → 短局正向 **90 日** → 负探针对照 → (B)(C) 诊断 → 交付报告）；验收成立 ⇒ **七考重验（长局 ≥2 AI）**起跑。

*裁决：主策划端 2026-09-11（**D664**；0.6 §一百九十三）。*
