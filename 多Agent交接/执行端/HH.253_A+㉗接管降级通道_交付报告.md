# HH.253 HH.251「A+ 施工批·㉗接管降级通道」交付报告（执行端 → 主策划端）

> 取号：HH.253（账本水位线 HH.252 → 253；D640 #10 先登记后落盘）
> 作业依据：**HH.251 施工任务书**（D699 签发·Gate=`G1-1`）＋ **D699**（0.6 §二百二十八）＋ **HH.252 开工回执**（本批预检报裁 R1~R4）
> **授权注记**：HH.252 报裁 R1~R4 经**用户裁决＝按「口径 A」施工**（授权最小面破 §三：改 `NeedScore` 入口判据 ＋ 放宽 `Feasible` 可见性；执行侧让位分支保留并加接管）。本报告即为该授权下的施工交付。
> 前置必读已完成：HH.249 §九 验收区 ＋ HH.249 探针结论 · `DZ-148` · D690（0.6 §二百一十九）· HH.244 任务书
> 状态：🔴 **验收线 1 ❌（接管派发未发生）· 含报裁**；其余 4 条 ✅

---

## 〇、一句话结论

**㉗ 在石场景的「入口」已修复且实证生效**（同 seed 三跑次对照：`D7/D8/D13 k4` ㉗ `need` 由恒 `0.000` → `1.000`）——即 `DZ-148` 论证的"永久让位"在**评分入口**层面被打开。**但「接管派发」未发生**：㉗ 虽已入池（本批 census `top=㉗` **3 次**），却在焦点竞争中**未夺魁/未落地**，全程 **0 条 `㉗接管` 日志**、**0 次石派发**（矩阵 `实际派发＝Wood=2`，与 HH.249 同 seed 读数一致）⇒ **验收线 1 ❌**，需策划端裁补救口径（§七 报裁）。

---

## 一、sim-sync 核查结论（红线项）

**结论＝本批零 `AI.Core` 义务。**

| 核查项 | 结论 |
|---|---|
| 改动对象 | `Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs` ＋ `UtilityScorer.cs`——**属 `_Game`，非 `AI.Core`** |
| `AI.Core/**` | 全符号零命中（`KingdomBrain`/`UtilityScorer`/`DecideTriage`/`NeedKind`/`GatherShortageGap`/`FocusController`） |
| 训练仓镜像 | `ai决策大脑强化训练/harness/`：`DecideTriage|ExecuteWorldGatherFocus|GatherShortageGap|class UtilityScorer|class FocusController` **零命中**；仅 `harness/KingdomBrain/SituationSnapshot.cs:104` **注释**提及 `UtilityScorer`；`harness/Economy/SimEconomy.cs` 有 `QuarryDaily` 等经济域近似但**无三通道分诊/㉗ 采集语义** |
| 判定 | 镜像无对应逻辑 ⇒ **sim 零同步义务**（同 HH.236 §四/HH.252 §一 口径；15_账本留痕） |
| 本批改动面无新增 | `UtilityScorer.Feasible` 仅放宽**可见性**（`private`→`internal`）、`NeedScore.GatherShortageGap` 仅改建**入口判据**——两者在 harness 均无副本 |

---

## 二、实施落点（3 处·逐处）

### 2.1 `UtilityScorer.cs`（评分侧＝入口）
| 处 | 位置 | 改动 |
|---|---|---|
| a | `Feasible(...)`（原 `private static`） | 放宽为 **`internal static`** ＋注释（HH.251/D699 授权；同 `ResolveTriageResource` 于 HH.221/D685 先例；**语义逐字不变**，禁另造第二套可行性判据 L-31） |
| b | `NeedScore` 的 `case NeedKind.GatherShortageGap` | 入口由「**仅通道B**」放宽为「**通道B 或（通道A 且 ③建产能 `Feasible==false`）**」：`decG != NoOp` 时——通道C ⇒ 让位 ⑤；通道A ⇒ 取 `UtilityActionConfig.Find(BuildCapacity)` 判 **同源 `Feasible(③)`**，`③ 可行/不可判 ⇒ 让位 ③`，不可行 ⇒ 落入下方缺口计算（㉗ 入池） |

### 2.2 `KingdomBrain.cs`（执行侧＝派发）
| 处 | 位置 | 改动 |
|---|---|---|
| c | `ExecuteWorldGatherFocus` 让位分支（原 `:1348-1352`） | 让位条件由「`DecideTriage != NoOp` ⇒ 让位」改为「**通道C／通道A 且 ③ 可行 ⇒ 让位**；**通道A 且 ③ `Feasible==false` ⇒ ㉗ 接管**（继续走下方 `Advertise` 派发）」；新增**只读观测打点** `[KingdomBrain] kX ㉗接管：通道A且③不可行（{rt}）——降级通道派发`（HH.251 §三 授权，用于区分「接管入径」与「通道B 常态派发」） |

- 判据与评分侧**同源**（同一 `UtilityScorer.Feasible(③)`，L-31 禁另造）。
- `DecideTriage` **纯函数零改**（保 `R-C4` 确定性断言）；`census top=` 结构零改；`UtilityActionConfig`/`KingdomDiagnosisConfig` 数据零改；`BuildCapacity(③)` 本体零改；sim/训练仓零触碰。

---

## 三、新旧输入集一致性核验（红线·D695）

新增 guard 后，**原让位路径是否仍覆盖全部原行为**——逐案穷举（`ExecuteWorldGatherFocus`）：

| `DecideTriage` 结果 | 原行为 | 本批行为 | 判定 |
|---|---|---|---|
| `NoOp`（通道B） | 派发 | **派发** | ✅ 不变（矩阵 `Wood=2` 实证，见 §五-2） |
| `BuildGranary`/`BuildWarehouse`（通道C） | 让位 | **让位** | ✅ 不变（`decW != BuildCapacity` ⇒ `takeover=false`） |
| `BuildCapacity`（通道A）∧ `Feasible(③)==true` | 让位 | **让位** | ✅ 不变（`takeover=false`） |
| `BuildCapacity`（通道A）∧ `Feasible(③)==false` | 让位（空转） | **接管派发** | 🆕 新增（本批唯一行为变化点） |
| `capDef == null`（配置缺失） | — | 让位（不可判按让位处理，防误接管） | ✅ 保守 |

评分侧 `NeedScore` 同表（入口）：原「非 `NoOp` ⇒ 0」⇒ 新「通道C ⇒ 0；通道A ⇒ 依 `Feasible(③)`；其余（含 `NoOp`）⇒ 计缺口」。
**结论＝原行为全保留，仅新增「通道A 且 ③不可行」一条入径**（`Feasible` 语义/参数/极性逐字未动，仅可见性放宽）。

---

## 四、跑次标注与实证读数（D694 勘正②）

**跑档**：正门 `TestHarnessApi.EnterTestRun`（`test-harness-first` 铁律1）｜seed **73621**｜槽 **`chain_probe2`**｜观测窗 **D1~D60**｜收工＝**观测窗口到期 @D60**｜观测行采样 **3306**｜收尾＝真暂停+Save+`ExitTestRun`+**退 Play**（`isPlaying=False` 实测）。

**产物**：稳定名 `Logs/ChainAudit/chain_probe_matrix.txt`（SHA256 `6B2B79AF15C92729A509E9699B3D98F0E991FF5EC7D3B33AC60E6AE8A34E4288`）＝时间戳副本 `chain_probe_matrix_20260913_153652.log`（同哈希）。

### 4.1 判据面读数（本批跑次）
| 项 | 读数 |
|---|---|
| ㉗ 评分 top 次数 | **3**（HH.249 同 seed 同槽为 2 ⇒ **+1**，即本批新增入池日） |
| ㉗ **实际派发次数** | **Wood=2**（**Stone=0**） |
| ㉗ Env 阻断 | **0** |
| ㉗ 分诊支撑 | `Stone=BuildCapacity:154`／`Stone=NoOp:82`／`Wood=NoOp:236` |
| **`㉗接管` 日志（本批新增打点）** | **0 条**（全 `Editor.log` 零命中） |

### 4.2 入口修复的**同 seed 三跑次对照**（决定性·D694② 同段同 seed 同档）
`Editor.log` 内同 seed（73621）三跑次，`k4` ㉗ need 序列：

| 跑次 | 代码 | D7 k4 | D8 k4 | D13 k4 | ㉗ `feasible` |
|---|---|---|---|---|---|
| 跑次 A | 旧码 | `need=0.000` | `0.000` | `0.000` | False |
| 跑次 B（HH.249 同批） | 旧码 | `need=0.000` | `0.000` | `0.000` | **True**（有石候选点） |
| **跑次 C（本批）** | **本批码** | **`need=1.000`** | **`1.000`** | **`1.000`** | True |

- **判读**：跑次 B 是干净对照——`feasible=True`（石候选点在领土内）、同 personality（`axis=0.665`）、旧码 `need=0.000` ⇒ 说明**旧码下触发资源＝石且走通道A**（若为木则通道B 会给 `need>0`）；本批同条件下 `need=1.000` ⇒ **入口放宽确已生效于石场景**（非木路径投影）。
- **排除竞争解释**：`㉗ Env 阻断=0` ⇒ 排除「领土内无可采」；`Stone=BuildCapacity:154` ⇒ 通道A 恒判定（`DZ-148` 论证复现）。

### 4.3 只读实盘探针（本批新增观测·零写入）
跑局中（D41+）经 `execute_code` 反射直读（`ResolveTriageResource`/`DecideTriage`/`Feasible`/`NeedScore`）：`res=True dec=True fea=True`（三 internal 面反射可达）；当时 `k1:noShortage｜k2 er=Food dec=NoOp map=False need27=0.000｜k3 同型｜k4:noShortage` ⇒ 与「石短缺窗口在早段（D2~D13）」一致。

---

## 五、验收线 §五 逐条

| # | 线 | 裁决 | 依据 |
|---|---|---|---|
| 1 | **石场景可达**（㉗ 接管派发首达有日志） | 🔴 **未达** | `㉗接管` 日志 **0 条**；矩阵 `实际派发＝Wood=2／Stone=0`；㉗ `top=3` 但仅 2 次 Wood 派发 ⇒ **入池已成立、落地未发生**（根因见 §七） |
| 2 | 原行为回归（①通道B 可达 ②③可行让位） | ✅ **成立** | ①矩阵 `Wood=2` 与 HH.249 同 seed 读数**一致**（通道B 派发能力未变）；②全批 **无石派发**、③ 评分为 `top=24` 次（③ 可行时 ㉗ 未夺池）⇒ 让位语义保留 |
| 3 | 零改动面 | ⚠️ **按授权变更** | `Assets/_Game/Systems/**` 改动**仅 2 文件**（`KingdomBrain.cs` `+25/-…`／`UtilityScorer.cs` 含 `Feasible` 可见性＋入口判据；`git diff --stat` ＝ 2 files, +37/-8）；**AI.Core/sim 零触碰**；`census top=` 结构零改；**但** `UtilityScorer` 评分面（`NeedScore`）**经用户授权改动**（§三 红线原文为「❌ 不修 `UtilityScorer`」，HH.252 报裁 R2/R3 已列该冲突，用户裁＝口径 A 授权） |
| 4 | 编译 | ✅ 0 error | `refresh_unity(mode=force, compile=request)` → `read_console(types=[error])` 仅 1 条存量（`233 node options failed…`，Visual Scripting，非本批引入）；IDE `GetDiagnostics` 两文件**空** |
| 5 | 教训兑现（新旧输入集／跑次标注／四列齐） | ✅ 已出 | §三（D695）／§四（D694② 跑次标注）／四列齐见 §六 |

---

## 六、四列齐（§8.6·本批判据）

| 列 | 内容 |
|---|---|
| **可判定最早日＋命中即停** | D5 起跑；判定＝**石场景出现「无产能（通道A）＋③不可行」天** ⇒ 当日 `㉗接管` 派发日志首达即判；跑满 D60（任务书容 D90）未达＝**❌ 不成立** |
| **服务哪条验收句** | `DZ-148` 验收句：石场景（通道A 且 ③ 不可行）㉗ **可派发**（不再永久让位） |
| **作用域** | 全批 any-国（分资源石/木）；限 `KingdomBrain.ExecuteWorldGatherFocus` 让位分支 ＋ `UtilityScorer.NeedScore` ㉗ 入口 |
| **口径来源与排除项** | 来源＝`[KingdomBrain] kX ㉗接管：…`（本批接管入径）＋`㉗采集下发：{rt} 新立案 N 个`（派发落地）；排除项＝评分 `census top=`（仅支撑·L-35）／Env 阻断（世界给定物）／肉矿粮铁无世界通道（`TryMapWorldResource` 结构性排除）／`HarvestCarry` 无日志（入账口径知情未观测） |

---

## 七、报裁（1 项·根因＋候选）

### 7.1 根因（决定性）：**入口已开，落地未达——卡在焦点竞争/切转**

1. **入口已开（已证）**：㉗ 在「通道A 且 ③不可行」入池（`need=1.000`），本批 `top=㉗` 出现 **3 次**（较 HH.249 +1）。
2. **未落地（实测）**：`㉗接管`＝0 条、石派发＝0 条 ⇒ `ExecuteWorldGatherFocus` **未以 rt=Stone 被执行**。
3. **竞争面（同段读数）**：石短缺窗口内 `k4` 的 **⑨建城墙** `score=1.000`（`WallGap need=1.000 × axis=1.000 × stageW=1.00`）恒高于 ㉗ 的 `0.665`（`need=1.000 × personality[Economy]=0.665`）⇒ 焦点被 ⑨ 占据（`census top=BuildWall`）。
4. **切转面（候选·未直证）**：`FocusController` 切焦需 `day ≥ 上次焦点设定 + focusMinDurationDays(=3)`，且**常设底线**（⑤屯粮/⑥招工人/⑭防御）会 `SetFocus` 刷新该基准——㉗ 的入池日（D7/D8/D13）未满足/被打断即错过窗口。㉗ **需连续或恰逢可达窗口**才落地。

> ⚠️ 本批**不做**「为过线而调 seed 试跑」——`L-30` 精神（判定线硬条件结构性可达性须机制层成立，非靠碰运气）。故如实报 ❌。

### 7.2 报裁候选（请策划端择一）

| 项 | 内容 | 可达性评估 |
|---|---|---|
| **R-1（推荐）** | **接管型优先**：在「通道A ∧ ③不可行 ∧ 本国领土有该资源候选」条件下，把 ㉗ 的接管视为**优先型**（豁免焦点防抖／或计入常设底线序列）——落点 `FocusController`（焦点模型） | 高：直接消除「窗口错过」；**须授权动 `FocusController`**（本批未动） |
| **R-2** | **改在 ⑤ 分诊路径接管**：`ExecuteGrainTriage` 通道A 建产能不可行 ⇒ ㉗ 接管派发——⑤ 焦点在本批 `census top=32` 次（可达性天然高） | 高：⑤ 频次高；语义＝「⑤ 断供分诊的降级出口」，与 D690「降级通道」措辞更贴合 |
| **R-3** | 换 seed 复跑（任务书容「新 seed」）——不改码，仅验证机制端到端 | 中：取决于该 seed 的 personality（Defense 轴低则 ㉗ 可胜） |
| **R-4** | 接受「入口已修（已证）」为**部分达成**，接管落地另立批 | — |

**执行端建议＝R-1 或 R-2**（皆属机制层可达性修复，守 `L-30`）；若采 R-2，**本批 §二 的两处改动仍为必要前置**（入口不开，⑤ 路径也无从「让位给 ㉗」）。

---

## 八、红线自检

| 红线 | 状态 |
|---|---|
| 先出 sim-sync 核查结论 | ✅ §一（零 `AI.Core` 义务） |
| 正门 `EnterTestRun`＋退 Play（L-32） | ✅ 正门起跑；收尾实测 `isPlaying=False` |
| 新旧输入集一致性核验（D695） | ✅ §三（逐案穷举＋回归实证） |
| 跑次标注（D694②） | ✅ §四（seed/槽/窗/收工/采样＋副本名＋哈希） |
| 零业务代码改动 | ⚠️ **经授权变更**（`Assets/_Game/Systems/**` 仅 2 文件；HH.252 报裁 R2/R3 ＋用户裁口径 A 授权） |
| AI.Core / sim 零触碰 | ✅ `git status` 仅 2 个 `_Game/Systems/AI/KingdomBrain/*.cs` |
| 不修 A+/金门（③）本体 | ✅ `DecideTriage` 纯函数／`BuildCapacity` 本体／数据 SO 零改 |
| 写-改-commit 同串、只提本批、**不 push** | ✅ §九 |

---

## 九、产物与 commit

| 文件 | 状态 |
|---|---|
| `Valley Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs` | 修改（`Feasible` 可见性 ＋ `NeedScore` 入口判据） |
| `Valley Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs` | 修改（让位分支接管 ＋ 只读打点） |
| `多Agent交接/执行端/HH.253_A+㉗接管降级通道_交付报告.md` | 新建（本报告） |
| `多Agent交接/_编号登记.md` / `_交接索引.md` / `_任务队列.md` / 主计划书工作日志 | 回写 |

- 矩阵产物 `Logs/ChainAudit/chain_probe_matrix*.log`＝**gitignore 域**，不入库（就地留档，同 HH.242/HH.249 惯例）。
- **commit**：`<见回报>`（只提本批文件，**不 push**）。

---

> **版本**：2026-09-13（执行端）。关联：HH.251 任务书（D699）／HH.252 开工回执（报裁 R1~R4）／HH.249 §九／`DZ-148`／`L-21`/`L-30`/`L-31`/`L-35`。
> **红线自检**：sim-sync ✅｜正门/退 Play ✅｜新旧输入集 ✅｜跑次标注 ✅｜AI.Core 零触碰 ✅｜不修 ③ 本体 ✅｜不 push ✅。
