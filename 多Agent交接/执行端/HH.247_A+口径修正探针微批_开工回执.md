# HH.247 HH.244「A+ 口径修正探针微批」开工回执（执行端 → 主策划端）

> 取号：HH.247（账本水位线 246→247，D640 #10 先登记后落盘）
> 作业依据：HH.244 任务书（D694 签发·Gate=`G1-1`）
> 前置必读已完成：HH.240 任务书 · 《断链校验台_设计稿》§四 · HH.242 §九（9.3/9.4）· 缺陷台账 DZ-148（D690/D694 两行）· 0.6 §二百一十九（D690）/§二百二十三（D694）

---

## 〇、一句话回执

**先行承诺**：本批**只修探针口径**（件1）＋**据结果出结论**（件2），**不做 A+ 本体施工**（DZ-148 归属另裁）；**零业务代码改动**；结论**两向都出**（证伪/证实均有效），不以"保住 A+"为由调口径。

---

## 一、sim-sync 核查（开工回执必须给结论·红线）

**结论：本批不触 `AI.Core`，无镜像同步义务，可直改（无需 sim-sync 流程）。**

证据（阳性对照式）：
- 本批改两文件 `Assets/Editor/ChainAudit/Probes/ChainProbeFacade.cs`＋`Assets/Editor/ChainAudit/ChainAuditSpec.cs`（均 Editor 域）。
- grep `KingdomBrain`／`UtilityScorer`／`UtilityAction`／`NeedKind`／`Reflection`／`GetMethod`／`AI.Core`：
  - `ChainProbeFacade.cs`：**零命中**（Pure 日志正则观测：`Application.logMessageReceived` 读既有日志文本）。
  - `ChainAuditSpec.cs`：仅**字符串常量** `"UtilityActionConfig.asset"`（声明面文本，非 C# 符号引用）——零反射、零符号依赖。
- 阳性对照：既有观测器 `Valley_DiagMilitary.cs` 直接/反射调用 `UtilityScorer.ScoreTop/NeedScore/Feasible/DecideTriage`（HH.190/D651 授权观测域）——**本批不新增任何此类符号依赖**，与 `AI.Core`（`Assets/_Game/Systems/AI.Core/`）零交集。
- `Assets/_Game/**` 本批**只读**（`git status` 交付前核零 diff）。

---

## 二、件1 探针口径双修 · 实施落点（只加观测，不改逻辑）

### (a) 分资源维度
世界资源点采集通道**结构性只覆盖石(Stone)/木(Wood)**（`WorldGatherRegistry.TryMapWorldResource` 仅映射二者；肉/矿/粮/铁无世界通道 ⇒ **结构性排除项**，非观测缺失）。故实际派发观测**天然分石/木两轨**；"扩肉/矿"＝打点成本高且**无通道可派发**（纳入只会恒 0，无判别力）⇒ 声明为排除项，不扩装。

### (b) 判据由「评分 argmax」改「实际派发」
**真实派发锚点（既有日志·零业务改动）**：
- `KingdomBrain.ExecuteWorldGatherFocus`（:1334-1373）走通全部 guard、到达通道B落地出口的**唯一日志**＝
  `[KingdomBrain] k{kingdomId} ㉗采集下发：{rt} 新立案 {registered} 个（候选 {candidates}，在册 {count}）——选点/派工归 WorldGatherRegistry+TaskScheduler`（:1371，`Debug.Log`）。
  ⇒ **捕获该日志行＝ExecuteWorldGatherFocus 真实派发（成功落地分支）的观测**。分资源按 `{rt}`（Stone/Wood）计数。
- 支撑读数（**不作判据**，L-35）：
  - `[DiagMilitary] census top=GatherWorldResource` 行（评分 argmax）→ 表头改「评分 top 次数」；
  - `[DiagTriage]` 行分资源分诊（`Stone=BuildCapacity Wood=NoOp ...`，DecideTriage 结果，直接对应 DZ-148「石走通道A／木走通道B」论证）→ 支撑面；
  - Env 阻断日志 `[KingdomBrain] kX ㉗采集：本国领土内无可采 {rt} 资源点`（:1366）→ 支撑面（派发意图被世界给定物阻断）。

### (c) 表头勘正
「被选中次数」→ **「评分 top 次数」**，并新增 **「实际派发次数」** 列（㉗ 行填分资源：`Stone=x Wood=y`；其余 Action 无派发观测通道 ⇒ `—`，避免无观测列假装有值）。

### (d) 跑次标注（D694 勘正②）
矩阵**稳定名** `chain_probe_matrix.txt` 保持幂等（不含时间戳）；**新增时间戳副本** `chain_probe_matrix_yyyyMMdd_HHmmss.log`（沿用 `ChainAuditCore.WriteReports` 惯例 L-02 口径）供报告引用。矩阵 header 加「跑次标注」行（槽/seed/观测窗/收工原因）。**报告引用读数一律标槽＋副本文件名，禁跨跑次混比**。

### ChainAuditSpec.cs ㉗ 行四列更新（D694 授权内·声明面变更）
| 列 | 现值 | 改后 |
|---|---|---|
| ① 可判定最早日＋命中即停 | D5，census top 首达 | **D5，`㉗采集下发`日志分资源首达 ⇒ 可达；全程 0 ⇒ 🔴入口不可达** |
| ② 服务句 | ㉗ 被选中 ≥1 次（入口可达·DZ-148 判据） | **㉗ 实际派发 ≥1 次 ⇒ `DZ-148` 证伪（石场景可派发）；全程 0 ⇒ `DZ-148` 证成（结构性不可达）** |
| ③ 作用域 | 全批 any-国 | **全批 any-国·分资源（石/木）** |
| ④ 口径来源与排除项 | census top=… | **口径=`[KingdomBrain] ㉗采集下发`日志（ExecuteWorldGatherFocus 实际派发落地唯一出口）；支撑=census top=评分面＋DiagTriage 分诊面；排除项=评分 top 不作判据（L-35）／肉矿粮铁无世界通道（TryMapWorldResource 结构性排除）／HarvestCarry 无日志⇒入账口径知情未观测** |

> 注：ChainAuditSpec=C561 管辖声明面；本改动系 HH.244 件1 明确授权（改判据/表头/维度），非自行扩张。

---

## 三、件2 判据四列（修正后探针·CheckFourColumns 0 缺）

| 能力㉗ | 四列 |
|---|---|
| 〔可判定最早日＋命中即停〕 | D5；`㉗采集下发` 分资源日志首达 ⇒ 命中即停（可达性已证毕）；全程 0 ⇒ 跑满 D60 窗口判不可达 |
| 〔服务哪条验收句〕 | ㉗ 实际派发 ≥1 ⇒ `DZ-148` **证伪**；全程 0 ⇒ `DZ-148` **证成**（HH.244 件2 表） |
| 〔作用域〕 | 全批 any-国（4 AI）· 分资源（石/木） |
| 〔口径来源与排除项〕 | 来源=`[KingdomBrain] ㉗采集下发：{rt} 新立案 N 个`（真实派发）；排除=`census top=` 评分面（支撑不计判据）、肉/矿/粮/铁无世界通道、`HarvestCarry` 无日志（知情未观测）、Env 阻断（世界给定物，不计派发） |

---

## 四、跑档（正门·L-32 收尾）

- 入口：`TestHarnessApi.EnterTestRun`（正门，铁律1）；`DiagMilitary.IsRunning` fail-fast。
- 档位：seed **73621**（任务书指定·同 HH.230/HH.242）／槽 **`chain_probe2`**（本批新槽·禁覆盖 p1_* / chain_probe1）／窗口 D60／熔断 D90。
- 收尾：真暂停（`Time.timeScale=0`）→ Save → `ExitTestRun()` → **退 Play**（L-32·禁留 1x 余留世界）。
- 零业务代码改动：`Assets/_Game/**` 只读；`Assets/Editor/**` 仅探针文件。

---

## 五、立场声明（禁预设）

- 石场景 ㉗ 实际派发＝恒 0 ⇒ `DZ-148` **成立** ⇒ 回报→策划端签 A+ 施工任务书；
- 石场景 ㉗ 实际派发 >0 ⇒ `DZ-148` **证伪** ⇒ 回报→A+ 不立→转 G1-1 余项（DZ-135 金门/木产能立账）。
- **两向均为有效结论**；若实测与 HH.242 旧读（木可通·census 2 次）有出入，如实列报。

---

## 六、承诺与红线

1. 探针走正门·四列齐·收工退 Play（test-harness-first 铁律1/L-32）。
2. 零业务代码改动·编译 0 error（0 warning 目标）。
3. 不修 A+/金门本体（只探针+结论）。
4. 取号：开工回执 HH.247（本笔）／交付报告按水位线另取（预计 HH.248）；**写-改-commit 同串、只提本串文件、不 push**。
5. 复用公共 API 处逐调用点核〔参数极性·过滤方向·作用域〕（D695 教训）——本批只读日志/正则，无公共 API 面改动（极性核验陈述随交付报告附）。

> 无请裁项：方案完全落在 HH.244 任务书授权内（D694 已裁），开工回执存档即施工。

---

**状态**：🟡 开工回执落盘（2026-09-13）→ 随即施工件1 → 件2 出结论 → 交付报告（HH.248）。

---

## 附 · 会话中断接续指引（本会话 MCP 失效·新会话续跑必读）

> 触发：本会话 Unity MCP（`mcp_unityMCP`/`mcp_unity-bridge`）因会话 ID 失效无法注入（目录/配置均为最新，schema 齐全，纯注入失败；重启无效，须新会话）。**代码改动已全部落盘在磁盘**（未 commit）。

### 已完成的（磁盘在场·勿重做）
1. **账本取号 HH.247**（`_编号登记.md` 水位线→247＋在途行；commit `52a1094`）。
2. **开工回执 HH.247**（sim-sync 核查结论＝不触 `AI.Core` 可直改；判据四列；跑档 seed73621/槽 `chain_probe2`/D60；立场=两向出结论）。
3. **件1 代码改动（两个文件）**：
   - `Assets/Editor/ChainAudit/Probes/ChainProbeFacade.cs`：
     - 槽 `chain_probe1`→`chain_probe2`；
     - 新增观测：`_dispatchByRes`（㉗ 实际派发）/`_envBlockByRes`/`_triageCounts`＋正则 `ReDispatchGather`/`ReGatherEnvBlock`/`ReTriageDec`/`ReTriagePair`；
     - `Watch()` 捕获 `[KingdomBrain] kX ㉗采集下发：{rt} 新立案 N 个`（真实派发·判据面）＋`[DiagTriage]` 分诊（支撑面）＋Env 阻断（支撑面）；
     - 矩阵表头「被选中次数」→「评分 top 次数」＋新增「实际派发次数」列；矩阵新增跑次标注（seed/槽/观测窗/采样）+ `DispatchSummary`/`EnvBlockSummary`/`TriageSummary`；
     - 落盘改 `ChainAuditCore.WriteReports`（稳定名＋时间戳副本 `chain_probe_matrix_*.log`，L-02）；
     - 判据：㉗ 走「实际派发」、其余维持「评分 top」（HH.242 口径）；⑦ 复合逻辑保留。
   - `Assets/Editor/ChainAudit/ChainAuditSpec.cs`：㉗ 行四列更新（判据改实际派发、作用域分资源、口径来源/排除项重述）。
4. **未 commit**：上述两个 .cs 改动仍为工作区未提交（严禁 `git add -A`；只提这两个文件＋交付报告）。

### 新会话接续步骤（严格按序）
1. **编译验证（MCP 正门）**：`refresh_unity(mode=force, scope=all, compile=request)` → `read_console(types=["error"])` 确认 **0 error**；有错按报错修。
2. **跑修正后探针（件2）**：Unity 内先点「Valley/诊断/启动建军链诊断」（`DiagMilitary` fail-fast 前置）→ Play GameScene → 菜单「Valley/审计/ChainAudit/跑行为探针（正门进局）」→ 自动正门 `EnterTestRun` seed73621 槽 `chain_probe2` D60 → 收尾自动真暂停+Save+ExitTestRun+退 Play。
3. **读矩阵产物**：`Logs/ChainAudit/chain_probe_matrix.txt`（稳定名）＋同目录 `chain_probe_matrix_*.log`（时间戳副本·报告引用标注跑次）。**㉗ 行判读**：`实际派发次数` 列 `Stone=x`（x>0⇒`DZ-148` 证伪；x=0 全程⇒证成）；结合 `分诊支撑`/`Env 阻断` 交叉核验。
4. **出结论（件2·禁预设）**：石场景派发恒 0⇒`DZ-148` 成立→回报策划端签 A+ 施工任务书；石场景派发>0⇒`DZ-148` 证伪→A+ 不立→转 G1-1 余项。
5. **交付报告 HH.248**（账本水位线先取号）：验收线 §四 5 条＋跑次标注（槽 `chain_probe2`/副本文件名）＋零业务改动 diff 核验＋`isPlaying=False` 实测＋commit（只提本批文件：两个 .cs＋HH.248 报告）＋**不 push**。
6. **回写**：`_交接索引.md`（HH.247 状态→施工中/完成后补 HH.248 行）＋`_任务队列.md`（HH.244 行状态更新）＋主计划书工作日志。红线：写-改-commit 同串、只提本串文件。

### 需新会话自证项
- 编译 0 error（MCP 实测）；
- `ChainProbeFacade` 重构后**幂等**（重复跑矩阵稳定名逐字节一致·可跑一次验证）；
- 复现口径（HH.242 勘正②）：报告引用读数必须标注槽＋时间戳副本文件名，禁跨跑次混比。