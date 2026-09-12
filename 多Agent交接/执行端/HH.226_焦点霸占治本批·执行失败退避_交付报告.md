# HH.226｜⑨/③ 焦点霸占治本批 · 通用「执行失败退避」（交付报告）

> 类型：**交付报告** · 状态：🟡**待验收**
> 执行端（TraeCode·Unity 轨）· 2026-09-12 · 依据：**HH.224 任务书**（D670 签发／D672 补登记）· **D675 裁决（准予实施＋逐项裁 5+1＋抓漏 `:1200`）** · 来源实证 HH.223 §3.2
> 取号：遵 D640 #10（水位线 HH.225 → **HH.226**，独立单行 commit `a1f130e`）

---

## 一、实施（业务码改动 3 文件 ＋ 新增 3 文件 ＋ 观测域 3 文件）

| # | 落点 | 内容 |
|---|---|---|
| 1 | **新建** [ActionBackoff.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ActionBackoff.cs) | 通用退避表：**per-kingdom × per-action**（键＝`kingdomId<<16\|actionId`，**禁 per-call-site**——D675 裁①）；静态字典（对齐 `s_dispatch` 先例）；**三条自愈**（成功即复位／need 变化超容差 `0.05` 即复位／日推进冷却 `5` 日复位）；**硬上限** `12` ⇒ 因子锁下限 ＋ 打 **`BACKOFF_CAP`** 升级报裁标记（D675 补硬约束③）；`Reset()` 入口 |
| 2 | **新建** [ActionBackoffConfig.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/ActionBackoffConfig.cs) ＋ `Resources/Config/Kingdoms/ActionBackoffConfig.asset` | 单职责 SO（D675 裁②，`ResourceBiasConfig` R-B2 先例）：`enabled/threshold=3/stepPerFail=0.25/minFactor=0.25/cooldownDays=5/hardCapFails=12` ＋ per 行动覆盖表。**出厂占位值（待裁）** |
| 3 | [UtilityScorer.cs:142-146](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L142-L146) | `score *= ActionBackoff.Factor(k.id, def.id, need)`——**硬约束①：只乘 `score`，`NeedScore`/`Feasible` 零改动**（保 2_23RP0 P4b~P4e 与 D525 §3.7 分层） |
| 4 | [KingdomBrain.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs) | ①`ReportActionFail/ReportActionOk` 上报口；②**全行动真失败落点接线**（见 §二）；③`bc == null` **补观测日志**（D675 抓漏）；④`ResetDispatchStats` **同点清零**（硬约束②）；⑤`Tick` 调 `OnDayTick`（冷却自愈） |
| 5 | [Valley_DiagMilitary.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs) | 状态行 **`+avoid=<action:factor,…>`**（口径＝`ScoreTop` 实乘因子快照；**排除项＝不含 `NeedScore`/`Feasible`**，`L-35`）；`verdict=` **`+BACKOFF_CAP`** |
| 6 | [Valley_HH80_Run.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs) | `DIAG_CIRCUIT_DAY=60`（**独立常量·不回改主档 120/2**，D675 裁④）＋槽 **`p1_fix2`** ＋ **J6/J7/J8 在线判据**（基线常量取自 `p1_fix1` 同日志源）＋`ENABLE_J2_STONECOLD` 开关 |
| 7 | [Valley2_17_Smoke_5.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_17_Smoke_5.cs) | 新增 case **`ActionBackoffGuard`**（退避正负例：中性/未达门槛/达门槛/读数/三条自愈/硬上限/下限护栏） |

---

## 二、各行动失败落点清单（D675 裁③ 点名前置件 · 26 行动全枚举 ＋ **真失败／非失败分流**）

> **分流判据**：`Bump(ok:false)` **不等于**失败——「已达目标／正常态／防御性」三类**不得**上报退避，否则把达标误判为失败（自伤）。

| 行动 | 执行入口 | **真失败落点（已接退避 · `KingdomBrain.cs` 实读行号）** | **非失败 `Bump(ok:false)`（已排除）** |
|---|---|---|---|
| ①House ②Warehouse ③Capacity ④BoostHarvest ⑨Wall ⑯Well ⑰Blacksmith ⑱~㉑三营+射箭场 ㉓Barracks ㉔TrainingCamp ㉕SiegeWorkshop | `ExecuteBuildFocus` | **建造链 5 点**：`:1219` focus def 缺失／`:1227` buildingId 无 def／`:1240` **选址无落位**（DZ-107 主病灶）／`:1250` **`bc==null`（D675 抓漏·已补观测日志）**／`:1264` `TryBuild=false`；成功 `:1258` | — |
| ⑤Grain | `ExecuteGrainTriage` → 转 `ExecuteBuildFocus` | 失败随建造链归属**⑤**（per-action 口径） | `:1227`〔⑯〕外：`:1229/:1236/:1251` 诊断块未就绪/无触发/NoOp（均 ok:true） |
| ⑥RecruitWorker | `ExecuteRecruitWorker` | `:847` 无候选／`:871` 粮不足／`:881` 转换 0 | — |
| ⑦RecruitWarrior | `ExecuteRecruitArmy` | `:937` raceDef 缺失／`:944` 候选 0／`:971` 无可负担候选／`:989` 训练失败 | **`:899` warrior≥target（已达目标）** |
| ⑧Tech | `ExecuteTech` | `:1093` 金不足 | **`:1107` 已达城堡上限（防御性）** |
| ⑯TrainGeneral | `ExecuteTrainGeneral` | `:1004` 无兵营／`:1017` 训练失败 | — |
| ㉕ProduceMachine | `ExecuteProduceMachine` | `:1030` sps==null／`:1034` 无厂前置／`:1046` 无本族机器／`:1052` prefab 缺失／`:1058` 无锚／`:1062` 产出失败 | — |
| ⑩Expand | `ExecuteExpand` | **无失败语义**（焦点一致性占位，实际扩张归日 tick） | — |
| ⑪Expedition ⑫Reinforce ⑮Diplomacy | `:602-607` | **未接线桩**（无实体指令） | — |
| ⑬Rebuild ⑭Defense 0 None | `:608-612` | 姿态/占位，无实体指令 | — |

**⇒ 覆盖**：建造类 15 ＋ 非建造类 ⑥⑦⑧⑯㉕（D675 裁③要求**含非建造类**）＝ **21 条行动已接**；余 5 条为占位/姿态（无失败面，非漏接）。

---

## 三、门禁（全过）

| 项 | 结果 |
|---|---|
| 编译 | **0 错**；**本批改动文件零 warning**（新增 15 条为**主程序集既有**历史 warning，非本批引入；期间我引入的 1 条 CS0162 已即时修掉） |
| `Smoke_2_22P0` **run1** | **32/0**（与基线一致） |
| `Smoke_2_23RP0` **run1** | **27/0**（**2440B，与基线同字节**） |
| `Valley2_17_Smoke_5` | **ALL PASS**，含新 case「退避正负例=OK(中性/未达门槛/达门槛 f=0.75≥0.25/读数/自愈=True/硬上限 f=0.25/下限护栏)」 |

**声明**：本批**未经 sim 门禁**（D675 裁⑥；依据＝`AI.Core`（34 文件）与训练仓 `harness/` 对王国脑层 6＋5 符号**双端零命中**，HH.225 §二 实测）⇒ 后续若触及 `AI.Core` 须回补 `sim-sync` 规定序。
**声明**：本次冒烟**只跑 run1**（未跑 run2 逐行一致比对；HH.216 次生项）。

---

## 四、同 seed 修前/修后对照（**同段 D2~D31** · 各 30 样本/国）

> 口径＝**日志源**，与修前基线同源（`L-35`）。修前＝`p1_fix1`（seed64513·15x·D65 收工）；修后＝`p1_fix2`（同 seed·15x）。

| 判据 | 修前 | 修后 | 变化 |
|---|---|---|---|
| **① `wall` 选址失败次数**（k1/k2/k4） | 5／15／10＝**30** | 5／12／3＝**20** | **−33.3% ✅** |
| **② census `top=BuildWall` 占比** | k1 8／k2 18／k3 5／k4 18 ＝ **49/120 = 40.8%** | k1 8／k2 17／k3 6／k4 17 ＝ **48/120 = 40.0%** | **−1.8%（远未达 −50%）❌** |
| **③ 建造落地数（不降）** | **22** | **23** | **+1 ✅** |

### 4.1 ✅ 机制面全项实跑成立（J6）

- **退避生效**：`avoid=BuildWall:0.25`（⑨ 达下限）／`avoid=RecruitWorker:0.25`（⑥）——全跑 `avoid` 非空 **75 行**（RecruitWorker 54／BuildWall 21），达下限 **48 行**。
- **硬上限升级报裁**：`BACKOFF_CAP` 实跑出现（k2 D31）。
- **✅ 自愈循环实证（硬约束③兑现）**：k1 ⑥ 序列 `D10~D13 =0.25` → **`D14~D15 avoid=空`（冷却复位）** → **`D16 =0.75`（重新累计）** ⇒ **"降权→定期重试→再降权"**，未变"永久弃建"。
- **✅ HH.223 残余①「`OnlyKingdom` 端到端首验」正面闭环**：首跑 D12 **k1 与 k3 同日 `stoneCold:10`**，命中报 **k3**（指定国）、**k1 未触发** ⇒ 过滤分支真跑生效。
- **口径护栏**：`NeedScore`/`Feasible` 零改动（`Smoke_2_23RP0` P4b~P4e 全过为证）。

---

## 五、🔴 本批新发现的两项**判据口径缺陷**（同一族：判据口径与其窗口/语义不匹配）

### 5.1 J2「石链僵死」**误停**（首跑 D12 止损；**非修后回归**）

- 修前同 seed 实测：**k3 D2~D14 `stone` 恒 25、`Stone prod=0/in=0`**（D30 才 `prod=1/in=100`）⇒ J2 在**修前同样**会 ~D11~D12 停。
- 根因：J2 的**"可判定最早日 D10"未排除"开局无产能期"常态**。
- **本批处置（依你"有一点影响就处理"）**：按 **D669「判据须与其服务验收句同级＋同作用域」**——J2 服务句＝HH.220 石链诊断（该批已销号），**不在本批启用集** ⇒ **本批关闭**，保留开关 `ENABLE_J2_STONECOLD`（后续批需要时置 true，但**须先解决"开局无产能期"口径**）。
- 影响评估（为何必须处理）：修前 wall 失败与登顶**集中在 D16 之后** ⇒ D12 止损等于三项对照**全拿不到**。

### 5.2 J7「霸占解除」**假阳性**（次跑 D31 触发停跑）

- 触发原文：`判据命中：J7 霸占解除（k1 wallTop 8/30=0.27 ≤ 基线半值 0.31）@D31`。
- **反证**：**修前同段 k1 亦为 8/30 = 0.267** ⇒ 占比**未变**，却判"解除"。
- 根因：**J7 的基线取"全窗 D1~D60 占比"（37/59=0.627）**，而判据在**早窗（D2~D31）**生效 ⇒ **不同段比较**（早窗天然低于全窗）⇒ 假阳性。
- ⇒ **报裁**：J7 基线须改**同段**（或改滑动基线），否则会以既有常态误判"解除"并提前停跑（本跑因此未取 D60 整窗）。

---

## 六、验收句判定（HH.224 §三）

| # | 验收句 | 判定 |
|---|---|---|
| ①-a | `wall` 选址失败次数**显著下降** | ✅ **达成**（30→20，−33%） |
| ①-b | census `top=` 中 **wall 占比下降** | ❌ **未达**（40.8%→40.0%，−1.8%） |
| ①-c | **建造落地数不降** | ✅ **达成**（22→23） |
| ② | 边界/负探针：恒无可落位 ⇒ 退避生效 **且状态变化可恢复** | ✅ **达成**（`Smoke_5` 退避正负例：达门槛生效／need 变化复位／成功复位／冷却复位；＋实跑自愈循环 §4.1） |
| ③ | 回归：32/0 · 27/0 · Smoke_5 全 PASS | ✅ **达成** |
| ④ | 在线判据表三列 | ✅ 已填（J6/J7/J8 各列齐） |

**⇒ 结论：机制治本成立（机制面 100% 实跑）、②③④ 达成；① 分项 2/3 达成、`占比` 项未达。**
**机理诊断**：⑨ 退避**已锁下限 0.25** 仍居 census 首位 ⇒ 说明其 `score` 原值（1.000＝满分）**远超竞争者**（0.25×1.0=0.25 仍 > 多数候选）⇒ **属数值/口径面**（非结构缺陷）：可选 (a) 降 `minFactor`；(b) 降门槛 3→2；(c) 加"**达硬上限后强制让位**（该行动当轮出池）"口径。**三者皆数值/口径面 ⇒ 归策划端裁**（执行端**不擅自调值**，D563③）。

---

## 七、报裁项（**4 项 ＋ 2 声明**）

1. **J2 口径缺陷**（§5.1）：本批已关；后续批启用前须解决"开局无产能期"口径（建议：`quarry`/任何 Stone 产能未在场期间不计 streak）。
2. **J7 基线须同段**（§5.2）：否则假阳性停跑。
3. **验收句①-b 未达**（§六）：数值/口径三选（`minFactor`／门槛／"达上限强制让位"）。
4. **⑥ 环境让渡型失败是否进退避计数**（你指"让策划评估"）：本跑 ⑥「无候选」（HH.28 裁决①＝**环境让渡**）已触发退避（54 行）；现靠 5 日冷却自愈"定期重试"。**是否应把环境让渡型失败排除出退避**（只保留资源/建筑前置类）＝待裁。
   **附**：出厂参数 `th3/step0.25/min0.25/cd5/cap12` 全为**出厂占位**，请一并裁。

**声明 A**：本批**未经 sim 门禁**（D675 裁⑥）。
**声明 B**：冒烟只跑 run1；对照窗口为 **D2~D31（同段）**——D60 整窗因 J7 假阳性截断未取（§5.2）；如需整窗，须先修 J7 基线口径。

---

## 八、红线自检

| 项 | 状态 |
|---|---|
| 设计文档／队列 | **零改动**（队列只读） |
| 业务码改动面 | 仅 AI 决策/执行域（`AI/KingdomBrain/*`＋`Data/Kingdoms/*`＋新 SO）；**champion／训练仓禁改域零触碰** |
| AI.Core | **零触碰**（镜像核查双端零命中，HH.225 §二） |
| 收工 | **已退 Play 实测 `isPlaying:false`**（两跑均在案；L-32） |
| 采样/阈值/退避参数 | **全 SO 外置**（`so-data-driven`）；无硬编码魔法数 |
| commit | **同串提交**（`e4d8357` 实施 ＋ 本串 判据开关＋报告）；**未 push** |
| 禁用手段 | 未做参数调参找补（D563③）；未放宽任何断言（冒烟零滑落） |

**证据**：`Logs/P1/p1_log_20260912_105407.log`（对照跑·J7@D31）／`p1_log_20260912_104437.log`（首跑·J2@D12）／`hh80_run_status.log`／`p1_log_20260911_231630.log`（修前基线）／`smoke_2_22p0_run1.log`／`smoke_2_23rp0_run1.log`／`Saves/p1_fix2_day005~030`。

---
*执行端 2026-09-12（HH.226 交付报告，取号 `a1f130e`）。停手待策划端验收。*
