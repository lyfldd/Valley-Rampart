# HH.249 HH.244「A+ 口径修正探针微批」交付报告（执行端 → 主策划端）

> 取号：HH.249（账本水位线 248→249，D640 #10 先登记后落盘；HH.250 §四-3 已声明 HH.249 预留给本报告）
> 作业依据：**HH.244 任务书**（D694 签发·Gate=`G1-1`）
> 前置必读已完成：HH.240 任务书 · 《断链校验台_设计稿》§四 · HH.242 §九（9.3/9.4）· 缺陷台账 `DZ-148`（D690/D694 两行）· 0.6 §二百一十九（D690）/§二百二十三（D694）
> 会话接续：HH.247 开工回执〔含「附·会话中断接续指引」〕· HH.248／HH.250（MCP 中断与应急上报，本报告不涉）
> 性质：**探针口径修正批**（件1 观测面改动 ＋ 件2 据结果出结论）；**不施工 A+ 本体**

---

## 〇、一句话结论

**探针口径双修已落地并编译 0 error；同 seed 两跑次实测 ㉗ 实际派发 = `Wood=2`／`Stone=0`（D1~D60 全程）⇒ `DZ-148` 证成**——即「石场景 ㉗ 结构性不可达（永久让位）」经**真实派发锚点**复核坐实，木路径可派发（`DZ-148` 自述的「反证正确形态」同步复现）。⇒ **A+ 立批前提成立**（请策划端据 §六 结论签 A+ 施工任务书）。**两向均有效**：本批未出现「石场景派发 >0」的证伪信号，如实报证成。

---

## 一、验收线 §四 5 条（逐条结果）

| # | 验收线（HH.244 §四） | 结果 | 证据 |
|---|---|---|---|
| 1 | **口径修正到位**：㉗ 观测**分资源**（石/木至少）＋**判据＝实际派发**；矩阵表头/新增列已改 | ✅ | `ChainProbeFacade.cs:42-44`（`_dispatchByRes`/`_envBlockByRes`/`_triageCounts`）／`:199-202`（四正则）／`:234-259`（`Watch` 捕获 `㉗采集下发`＋Env 阻断＋DiagTriage）／`:276-277` 表头「评分 top 次数」＋新增「实际派发次数」／`:287` 判据分流（㉗ 走派发、其余维持评分 top）；实跑矩阵见 §二 |
| 2 | **四列齐**：`CheckFourColumns()`＝0 缺 | ✅ | MCP 实测 `CheckFourColumns 缺列数=0 \| 能力数=13`；`ChainAuditSpec.cs:56-61` ㉗ 行四列更新 |
| 3 | **结论明确**：`DZ-148` 证伪/证实之一，附**同 seed／同槽／同跑次**读数（标〔同段·同 seed·同档〕） | ✅ | **证成**：㉗ 实际派发 `Wood=2／Stone=0`（两跑次一致）——见 §二、§三 |
| 4 | **零业务改动＋编译 0 错**：`Assets/_Game/**` 无 diff；`Assets/Editor/**` 仅观测面；编译 0 error | ✅ | `git status --porcelain -- Assets/_Game` 空／`-- Assets/_Game/Systems/AI.Core` 空；`Assets/Editor` 仅本批两文件；编译唯一 error＝Visual Scripting 存量 |
| 5 | **正门·退 Play**：实测 `isPlaying=False` | ✅ | MCP 实测（两跑次收尾后）`isPlaying=False ts=1` |

---

## 二、跑档与跑次标注（D694 勘正②）

### 跑档（正门·L-32 收尾）
- 入口：`TestHarnessApi.EnterTestRun`（正门，`test-harness-first` 铁律1）；前置 `DiagMilitary.IsRunning` fail-fast。
- 档位：seed **73621**（任务书指定）／槽 **`chain_probe2`**／窗口 **D60**／熔断 D90。
- 收工：**观测窗口到期 @D60**（`AllHittableObserved()` 因 ㉗ 判据改用「实际派发」后 `Stone` 恒 0 ⇒ 未命中即停，跑满窗口）→ 真暂停（`Time.timeScale=0`）→ Save → `ExitTestRun()` → **退 Play**。
- 观测行采样：3306。

### 两跑次产物（幂等基准稳定名 ＋ 时间戳副本）
| 跑次 | 稳定名（幂等基准） | 时间戳副本（报告引用） | 收工 | 采样 |
|---|---|---|---|---|
| 跑次 A | `Logs/ChainAudit/chain_probe_matrix.txt` | `chain_probe_matrix_20260913_135911.log` | 观测窗口到期 @D60 | 3306 |
| 跑次 B | `Logs/ChainAudit/chain_probe_matrix.txt`（覆盖） | `chain_probe_matrix_20260913_142851.log` | 观测窗口到期 @D60 | 3306 |

> ⚠️ **跑次互比纪律（D694 勘正②/L-35）**：报告引用读数一律标〔同段·同 seed·同档·同槽〕；**禁跨跑次混比**评分 top 面（两跑次评分 top 分布因世界模拟分支不同而不同，见 §三.3）。

### ㉗ 行判据读数（两跑次一致·同段·同 seed·同档）
| 跑次 | 「评分 top 次数」 | 「实际派发次数」 | verdict |
|---|---|---|---|
| A | 2 | **Wood=2**（Stone 无） | ✅ 可达（实际派发 2 次） |
| B | 2 | **Wood=2**（Stone 无） | ✅ 可达（实际派发 2 次） |

**㉗ 分诊支撑（`DiagTriage`）**：跑次 A `Stone=BuildCapacity:158 Stone=NoOp:78 Wood=NoOp:236`／跑次 B `Stone=BuildCapacity:101 Stone=NoOp:135 Wood=NoOp:236`。
**㉗ Env 阻断**：两跑次均 **0**（`㉗采集：本国领土内无可采 {rt} 资源点` 零命中 ⇒ 派发被阻断的「世界未提供」路径亦不在流）。

---

## 三、件2 结论（禁预设·两向都出）

### 3.1 判据（HH.244 §二 件2 表）

| 观测结果 | 结论 |
|---|---|
| **石场景 ㉗ 实际派发＝0（恒）** | `DZ-148` **成立**（结构性不可达坐实） ⇒ 回报，策划端签 A+ 施工任务书 |
| 石场景 ㉗ 实际派发 >0 | `DZ-148` **证伪**（叙事失实） ⇒ A+ 不立，转 `G1-1` 余项 |

### 3.2 实测判定

**⇒ `DZ-148` 证成（成立）**。依据：

1. **石**：㉗ 实际派发**全程 0**（D1~D60 两跑次均无 `㉗采集下发：Stone …` 日志行）。支撑面同向：`DiagTriage Stone=BuildCapacity`（跑次 A 158 次／跑次 B 101 次）＝`DecideTriage` 在石场景恒判**通道A**（无产能且 `FindTriageDef(Stone)="quarry"` 非空）⇒ `ExecuteWorldGatherFocus` 在 `:1348-1352` **正常让位**（`DecideTriage != NoOp` ⇒ `Bump ok:true` return，**不派发**）——与 `DZ-148` 论证的「石走通道A ⇒ ㉗ 永久让位」**逐步一致**。
2. **木（阳性对照）**：㉗ 实际派发 **`Wood=2`**（两跑次一致）＝`DZ-148` 自述「**木**：无产能 ⇒ `FindTriageDef(Wood)=null` ⇒ 落通道B ⇒ ㉗ 可通（**反证正确形态**）」**实测复现** ⇒ 探针通道本身有效（非「整体恒 0」的观测失效假象）。
3. **旧读数解释**：HH.242 首跑「评分 top=GatherWorldResource 2 次」恰是这 **2 次木派发**在评分面（census argmax）的投影 ⇒ 与 `DZ-148` **不矛盾**（D694 9.3 勘正①已定性「不同级」，本批实测进一步给出**分资源真相**：2 次全属木、石为 0）。

### 3.3 附带读数（支撑面·**不计判据**·L-35）

- 两跑次 census top 分布差异（跑次 A `BuildCapacity=21/BuildWall=74/Grain=41…` vs 跑次 B `BuildCapacity=29/BuildWall=50/Grain=18…`）＝世界模拟分支随机性所致，**与判据面（实际派发）无关**；仅作支撑，**禁跨跑次混比**。
- ⑯ `TrainGeneral`／㉕ `ProduceMachine`：两跑次均 `⛔ 前置门未达`（`军事期到达=✗`）⇒ 不可判，非入口不可达（同 HH.242 口径）。
- 弹药三条（`AMMO.S/F/M`）：`⚪ 未观测`（本批无观测通道）——同 HH.242 口径，未变。

---

## 四、实施落点（本批改动文件·逐条）

### 件1-a/b/c/d：`Assets/Editor/ChainAudit/Probes/ChainProbeFacade.cs`（225 行增删）
| 项 | 落点 |
|---|---|
| 槽切换 | `:34` `PROBE_SLOT` `chain_probe1` → **`chain_probe2`** |
| 分资源观测（a） | `:42-44` 新增 `_dispatchByRes`（㉗ 实际派发·石/木）／`_envBlockByRes`／`_triageCounts`；`ReDispatchGather`（`:199`）／`ReGatherEnvBlock`（`:200`）／`ReTriageDec`（`:201`）／`ReTriagePair`（`:202`） |
| 判据改实际派发（b） | `:234-259` `Watch` 捕获 `[KingdomBrain] kX ㉗采集下发：{rt} 新立案 N 个`（**真实派发锚点**＝`KingdomBrain.cs:1371` `ExecuteWorldGatherFocus` 走通全部 guard 到达 `Advertise` 落地出口的唯一日志）＋`[DiagTriage]` 分诊（支撑）＋Env 阻断（支撑）；`:287` 判据分流（㉗→派发；其余→评分 top）；`:296-306` verdict 分支（㉗ 评分 top>0 而派发 0 ⇒ 🔴入口不可达） |
| 表头勘正（c） | `:276-277` 「被选中次数」→「**评分 top 次数**」＋新增「**实际派发次数**」列；`:368-372` `DispatchStrOf`（㉗ 分资源串；其余 `—`） |
| 跑次标注（d） | `:270-271` 矩阵 header 新增「跑次标注（seed/槽/观测窗/收工/采样）」；`:335-336` 复用 `ChainAuditCore.WriteReports`（**稳定名幂等** ＋ 时间戳副本 `chain_probe_matrix_*.log`，L-02 惯例） |
| 汇总面 | `:326-332` 新增 `DispatchSummary`／`EnvBlockSummary`／`TriageSummary` |

### 件1 声明面：`Assets/Editor/ChainAudit/ChainAuditSpec.cs`（8 行增删）
`:56-61` ㉗ 行四列更新——判据「㉗ **实际派发** ≥1 次 ⇒ `DZ-148` 证伪／全程 0 ⇒ 证成」／作用域「全批 any-国·**分资源（石/木）**」／命中即停「`㉗采集下发` 日志分资源首达」／口径来源与排除项重述（含「肉矿粮铁无世界通道 ⇒ `TryMapWorldResource` 结构性排除」「`HarvestCarry` 无日志 ⇒ 入账口径知情未观测」「Env 阻断＝世界给定物，不计派发」）。

> 注：`ChainAuditSpec`＝`C561` 管辖声明面；本改动系 HH.244 件1 明确授权（改判据/表头/维度），非自行扩张。

---

## 五、红线自检

1. **正门进局/退 Play** ✅：`TestHarnessApi.EnterTestRun`；两跑次收尾实测 `isPlaying=False`（L-32）。
2. **零业务代码改动** ✅：`git status --porcelain -- Assets/_Game` **空**（含 `Assets/_Game/Systems/AI.Core` 空）；`Assets/Editor` 仅本批两文件。**核验方式＝git 全量 status 按路径过滤**（非目测，见 §一-4）。
3. **AI.Core 零触碰** ✅：同上级。**`sim-sync` 核查**：本批不触 `AI.Core`、无镜像同步义务（HH.247 §一 已给正式核查＋阳性对照；本批未新增任何 `AI.Core` 符号依赖，落点全在 `Assets/Editor/`）。
4. **不修 A+/金门本体** ✅：`DZ-148`/`DZ-135` 仅作探针对象与结论输出；本批无 `_Game/**` 改动。
5. **编译 0 error** ✅：`refresh_unity(mode=force, compile=request)` → `read_console(types=["error"])` 仅 1 条存量（`233 node options failed to load`，Visual Scripting，非本批引入）。
6. **不 push** ✅：保持未推送态（见 §七 commit 号）。
7. **写-改-commit 同串、只提本批文件** ✅：commit 仅含两个 `.cs` ＋ 本报告（见 §七）。

**跨跑次口径纪律（D694 勘正②）**：§二/§三 所有读数均标〔跑次 + 槽 + 副本文件名〕；评分 top 面差异已声明**禁跨跑次混比**。
**参数极性/过滤方向/作用域复核（D695 教训）**：本批无公共 API 面改动，仅日志正则只读观测；四个新正则逐条核过（`㉗采集下发：(\w+) 新立案 (\d+) 个` / `㉗采集：本国领土内无可采 (\w+) 资源点` / `[DiagTriage] …` / `(?:\b(Stone|Wood)=(\w+))`）——**作用域限 `[KingdomBrain]`/`[DiagTriage]` 前缀**、**分资源分组明确**，与 `KingdomBrain.cs:1366/1371` 实测日志逐字比对通过（正例：本跑两跑次均正确捕获 `Wood=2`；负例：`Stone` 行零命中）。

---

## 六、报裁（1 项）

### 报裁 1 · `DZ-148` 判成 ⇒ 请签 A+ 施工任务书

依 HH.244 §二 件2 表：**石场景 ㉗ 实际派发恒 0 ⇒ `DZ-148` 成立** ⇒ 后续＝**策划端签 A+ 施工任务书**（㉗↔③ 双轨解耦：通道A 且 `Feasible(③)==false` ⇒ ㉗ 接管·降级通道）。

**本批未做的**（按任务书「只做把问题问对、不做 A+ 本体」）：
- 未改 `KingdomBrain.ExecuteWorldGatherFocus`／`DecideTriage` 本体（`_Game` 只读）；
- 未动 `DZ-135`（金门）本体（仅 `DiagTriage` 支撑面读数带出 `Stone=BuildCapacity` 频次，供 A+ 批参考）。

**A+ 批可用的新增读数**（本批副产品，供签发参考）：
- ㉗ Env 阻断两跑次 **0** ⇒ 「石场景 ㉗ 无派发」**不是**「领土内无可采资源点」所致（世界给定物不是根因），而是**入口条件（通道A 让位）结构性挡死**——与 `DZ-148` 论证一致，**排除了 Env 阻断这一竞争解释**。

---

## 七、产物与 commit

| 文件 | 状态 |
|---|---|
| `Valley Rampart/Assets/Editor/ChainAudit/Probes/ChainProbeFacade.cs` | 修改（件1 主体） |
| `Valley Rampart/Assets/Editor/ChainAudit/ChainAuditSpec.cs` | 修改（件1 声明面） |
| `多Agent交接/执行端/HH.249_A+口径修正探针微批_交付报告.md` | 新建（本报告） |
| `多Agent交接/_编号登记.md` | 取号 HH.249（独立行） |

- 矩阵产物 `Logs/ChainAudit/chain_probe_matrix*.log`＝**gitignore 域，不入库**（就地留档，同 HH.242 惯例）。
- **commit**：`767af98`（交付串：两 `.cs` ＋ 本报告 ＋ 取号行）／`67d30f2`（回写串：交接索引 ＋ 队列 ＋ 主计划书工作日志）——**只提本批文件，不 push**。

---

## 八、下一步建议

1. **策划端验收本批**（§一 5 条 ＋ §三结论）；验收成立 ⇒ 签 **A+ 施工任务书**（`DZ-148` 归属）。
2. `DZ-148` 台账状态：本批给出**判据面实证**（探针口径修正后复核）⇒ 建议从「🔴开放（探针不可判）」推进为「**✅判据面坐实·待施工**」（由策划端裁）。
3. HH.247（开工回执）随本报告一并归档；HH.250（MCP 应急上报）「HH.249 预留」已兑现。

---

> **版本**：2026-09-13（执行端）。关联：HH.244 任务书（D694）／HH.247 开工回执（含接续指引）／HH.242 §九／`DZ-148`／`L-32`/`L-34`/`L-35`。
> **红线自检**：正门进局/退 Play ✅｜零业务代码改动 ✅｜AI.Core 零触碰 ✅｜不修 A+/金门本体 ✅｜编译 0 error ✅｜不 push ✅。
