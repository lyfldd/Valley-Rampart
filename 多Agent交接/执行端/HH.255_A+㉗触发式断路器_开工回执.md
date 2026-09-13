# HH.255 HH.254「A+ ㉗ 触发式断路器」开工回执（执行端 → 主策划端）

> 取号：HH.255（账本水位线 HH.254 → 255；D640 #10 先登记后落盘）
> 作业依据：**HH.254 施工任务书**（D700 签发·Gate=`G1-1`）
> 恢复四连已完成：河谷防线_开发计划书.md 顶部工作日志 → `_交接索引.md` → `_任务队列.md` → HH.254 任务书（逐字读完）
> 前置必读已完成：HH.251 任务书 ＋ HH.252 回执 ＋ **HH.253 报告 §十 裁决区（D700）** 全链逐字 · `FocusController.cs` 全文 · `ActionBackoff`（Env 护栏口径） · 台账 `DZ-148` 两行（🟡施工半程）
> 状态：🟡 **可施工**（§0 门环全闭或已定施工后实证口径；M3/M4 已列报并给出方案；**新增门环 1 环已列报**见 §二）

---

## 〇、一句话结论

`FocusController.Update` 底线段插入**触发级断路器**的方案在代码上成立（触发判据三函数全部可调、位序与调用序已实读确认）；**M3 实况＝`WorldGatherRegistry` 无 per-rt 在册源查询**（需增 1 个只读方法，方案见 §四）；**M4 推荐 (a)**（复用既有 `HasCandidate` 预检，避免白焦点日）。

**🔴 附带新发现（任务书 §0 gap 表未含·本回执补第 8 环）＝「登记容量 room」环**：`Advertise` 的**单国在册源上限是全 rt 共享**（`maxSourcesPerKingdom=6`），条件⑤（per-rt 无在册源）成立但**总容量已满**时，`Advertise` 直接返 `(0,0)` ⇒ 落 `candidates<=0` ⇒ **Env 失败（白焦点日）**，且打出**措辞失真**日志「领土内无可采」（实为容量满）。详见 §二 第 8 环与 §五 方案。

---

## 一、sim-sync 核查结论（红线项·正式复述）

**结论＝本批零 `AI.Core` 义务、零训练仓义务。**

| 核查项 | 证据 | 结论 |
|---|---|---|
| 改动对象 | `Assets/_Game/Systems/AI/KingdomBrain/FocusController.cs`（＋视 M3 需在 `Assets/_Game/Systems/World/WorldGatherRegistry.cs` 增只读查询）——**均属 `_Game`** | 非 `AI.Core` |
| `AI.Core/**` | `Grep FocusController\|WorldGatherRegistry\|Alarm\|Triage` ⇒ **零命中**（目录实读：Config/Decision/Formation/Memory/Ports/Shim/Stimulus 八子域，无焦点/分诊/采集面） | 无镜像 |
| 训练仓 `harness/` | `Grep FocusController\|WorldGatherRegistry\|WorldGatherSource\|popAlarm\|grainAlarm` ⇒ **零命中**；`harness/KingdomBrain/` 仅 `SituationSnapshot.cs`（注释提及 `UtilityScorer`）/`MirrorProbe.cs`/`BattleLearnedWeights.cs`；`harness/Sim` 的 `Focus`/`FocusDecision` 系**战术注意力**（`L1FocusEvaluator`）与国策焦点**语义无关** | 无镜像 ⇒ **零同步义务** |
| 判定 | 镜像无对应逻辑 ⇒ 零义务（同 HH.236/HH.252 口径；15_账本留痕） | ✅ |

---

## 二、§0 L-30 全链门 gap 表（逐环勾选·本回执落盘）

| # | 门环 | 需要的证据 | 实读证据（本回执复核） | 状态 |
|---|---|---|---|---|
| 1 | **条件成立日存在** | 断供∧通道A∧③不可行∧该 rt 无在册源的天 ≥1 | HH.253 实证 `k4 D7/D8/D13`（`need=1.000`）；「无在册源」在首断供日**必然成立**（尚未登记过） | ✅已闭 |
| 2 | **候选入池（旁证）** | ㉗ `need>0` 且世界有候选 | 跑次 C `need=1.000`（触发路径不依赖入池） | ✅已闭 |
| 3 | **焦点切换** | 触发日 `kingdom.focus==27` | 底线路径 `SetFocus` 直写跳防抖（`FocusController.cs:174-178`）；**施工后由打点证明** | 🔜施工后实证 |
| 4 | **执行路由** | `ExecuteFocus` `case 27` | `KingdomBrain.cs:630-632` 唯一调用点在场（本回执实读）；`:293-294` `mode==Fine ⇒ ExecuteFocus` | ✅已闭 |
| 5 | **接管分支放行** | 通道A∧③不可行 ⇒ 不 return | HH.253 已落（`ExecuteWorldGatherFocus` 让位分支 `takeover` 判定） | ✅已闭 |
| 6 | **Advertise 派发** | 领土内候选>0 | 三跑次 `Env 阻断=0`；日志「候选 77~89」 | ✅已闭 |
| 7 | **观测口径** | 「㉗采集下发：Stone」日志 | `ChainProbeFacade.ReDispatchGather` 正则在场（HH.249） | ✅已闭 |
| **8** | **🔴 登记容量 room>0（新增·任务书未含）** | 触发日该国**在册源总数 < `maxSourcesPerKingdom`** | `WorldGatherRegistry.cs:55-56`＝`room = maxSourcesPerKingdom - list.Count`、**`room<=0 ⇒ return (0,0)`**（**返 0 候选**）／`WorldGatherConfig.asset:16` `maxSourcesPerKingdom: 6`／跑次 C 实读 `㉗采集下发：Wood 新立案 6 个`（**一次填满上限**）⇒ **容量全 rt 共享**，木源占满后石**登记不进去** | 🔜**施工后实证（打点带读数）＋方案见 §五** |

**M1 判定**：8 环中 6 环已闭、2 环（第 3/8）为**施工后实证**（与任务书对第 3 环的处置同型）⇒ **无「未闭即施工」情形**；第 8 环为**新识别门环**，其影响面（是否挡住验收线 1）由触发打点当日读数定论，**不构成施工前置阻断**，但**请在裁决时知悉**。

---

## 三、M2 底线序授权复述 ＋ 文档漂移列报

- **复述**：`FocusController.Update` 常设底线段插入位序＝**粮警（`grainAlarm`）之后、人口（`popAlarm`）之前**；新序＝**粮 → ㉗断路器 → 人口 → 被攻**。此为 **D322 红线序（粮→人口→被攻）的变更**，**授权出处＝HH.254 任务书（D700）**；执行端据此施工，**不自行扩张**（不并入 ⑤/⑥/⑭ 任一级、不改三级语义）。
- **文档漂移列报（M2 要求·执行端不代改）**：`2_17 步骤9 焦点模型` 及相关文档对「常设底线序＝粮→人口→被攻」的描述将与本批施工后代码**不一致** ⇒ **列报**，修订归策划端/文档端（建议并入本批验收时的回写；不属执行端改动面）。

---

## 四、M3 per-rt 在册源查询 API 实况与方案

**实况（代码实读）**：

| 面 | 现状 |
|---|---|
| `WorldGatherRegistry.CountOf(int kingdomId)` | **只有总口径**（`Prune` 后 `list.Count`），**不分 rt** ⇒ 不能满足条件⑤ |
| `Entry`（私有嵌套） | 字段＝`Source`／`Cell`／`IsTree`——**不含 rt** |
| `WorldGatherSource._resource` | **`private readonly ResourceType`**，**无公开访问器**（`KingdomId`/`Cell`/`IsValid` 为公开） |
| 既有可复用件 | `public static bool HasCandidate(int kingdomId, ResourceType)`（HH.221 加·`Feasible` 同源用）＝**不立案只探测** |

**方案（择一·执行端建议 A）**：

- **方案 A（推荐·零新状态·不触第三方文件）**：在 `WorldGatherRegistry` 增只读方法
  `public int CountOf(int kingdomId, ResourceType resource)`——`Prune` 后对已登记 `Entry.Cell` 逐条用**同源** `TryMatchPoint(map, cell, resource, …)`（本类内既有 private 静态函数，L-31 禁另造）过滤计数。
  ⇒ **不新增字段、不碰 `WorldGatherSource`**，完全落在任务书 §四「✅ 在 `WorldGatherRegistry` 增**只读**查询」授权面内。
- **方案 B（备选）**：`Entry` 增 `ResourceType` 字段（`Advertise` 已有 `resource` 入参可直存）＋ `CountOf(kingdomId, rt)` 直查；或给 `WorldGatherSource` 加 `public ResourceType Resource => _resource;`。
  ⇒ 改动 2 文件（含 `WorldGatherSource`，**超出任务书 ✅ 清单**），非必要。

**执行端选＝方案 A**（最小面·守 §四范围）。

---

## 五、M4 Env 护栏二选一列报 ＋ 第 8 环方案

**列报（含新增第 8 环）**：触发级断路器（底线路径）**绕过评分与 `ActionBackoff` 乘子**，故「触发日无一事可做」的代价＝**白占当日焦点槽**（原评分路径会让位给其他行动）。需在触发条件内**前置排除**下述两类必败情形：

| 项 | 必败情形 | 判据（只读·同源） | 处置 |
|---|---|---|---|
| M4-① | **领土内无可采该 rt 的点** | `WorldGatherRegistry.HasCandidate(kingdomId, rt) == false`（既有公开 API） | **前置排除**（不触发） |
| **第 8 环** | **登记容量已满**（全 rt 共享上限 6） | 新增只读 `room`（`maxSourcesPerKingdom − CountOf(kingdomId)`；**单源**＝建议同置于 `WorldGatherRegistry`，与 `Advertise:55` 同公式，**禁在 `FocusController` 重写公式** L-31） | **前置排除**（不触发） |
| M4-② | **本国工人 = 0**（登记了也无人派工 ⇒ 空转） | `kingdom.workerCount > 0` | **前置排除**（不触发） |

**推荐＝M4 方案 (a) 的扩展版**：触发条件 = ①断供 ②`TryMapWorldResource`＝石/木 ③通道A ④`Feasible(③)==false` ⑤**该 rt 在册源 = 0** ⑥`HasCandidate(rt)` **∧ room>0** ⑦`workerCount>0`，**全 AND 成立**才 `SetFocus(27)`。
> 理由：M4(b)「容忍白焦点日 + 既有 Env 退避」在本路径**失效**——`ActionBackoff` 只乘评分 `score`，**不作用于底线触发** ⇒ 会**每日重复触发**（每日一次全领土扫描 + 误导性 Env 日志），既浪费又污染诊断。方案 (a) 保持「触发即有事可做」。

**附带列报（观测准确性·不擅自改）**：`ExecuteWorldGatherFocus` 的 `candidates<=0` 分支日志文案为「㉗采集：本国领土内无可采 {rt} 资源点」——在 **room=0** 时该措辞**失真**（领土内有点、只是容量满）。本批**不改文案**（越界），改以**触发打点带读数**消歧（§八 M8）。

---

## 六、M5 新旧输入集一致性（D695·逐案穷举表·施工后逐步实证）

`FocusController.Update` 底线段新序＝**粮 → ㉗断路器 → 人口 → 被攻**；穷举（日型 × 触发条件）：

| 日型 | 触发条件 | 原行为 | 新行为 | 硬断言 |
|---|---|---|---|---|
| 粮警日 | 成立 | ⑤ 强制即 return | **㉗断路器强制即 return**（先于 ⑤？否——粮警在断路器**之前**，故粮警日**仍先走 ⑤**） | ⚠️ 见下注 |
| 粮警日 | 不成立 | ⑤ 强制 | ⑤ 强制 | **零变化** |
| 人口警日 | 成立 | ⑥ 份额/让位日 | **㉗断路器**（序在人口前） | 授权序变更 |
| 人口警日 | 不成立 | ⑥ | ⑥ | **零变化** |
| 被攻日 | 成立 | ⑭（人口后） | **㉗断路器** | 授权序变更 |
| 被攻日 | 不成立 | ⑭ | ⑭ | **零变化** |
| 普通日 | **成立** | 评分 argmax ＋ 3 日防抖 | **㉗断路器强制（跳防抖）** | 本批目标行为 |
| 普通日 | 不成立 | 评分 argmax ＋ 防抖 | 同 | **零变化（硬断言）** |

> **注（位序语义澄清）**：任务书 §一.1 定「粮警**之后**」⇒ 粮警日**仍由 ⑤ 先行**（断路器不抢占粮底线），断路器只在「粮不警」时参与 ⇒ 与 D322 保命优先一致。此点施工时按字面落位，**回执显式声明**。

---

## 七、M7 时序复述（同 tick 生效）

`KingdomBrain` 日 tick 子步序实读（`KingdomBrain.cs:282-294`）：①态势重建 → ②剧本 → **③`Focus.Update(...)`（`:292`）** → **④`ExecuteFocus(...)`（`:294`，`mode==SimMode.Fine`）** ⇒ 底线触发 `SetFocus(27)` 与当日 `ExecuteFocus` **同 tick 同日生效**，无需跨日等待。**风险点登记**：Abstract 模式国不执行 `ExecuteFocus`（`:293`）⇒ 触发日仅置位不派发；因条件⑤持续成立会**每日重复置位**（无副作用，焦点槽在 Abstract 下本不驱动执行），**列报不改**。

---

## 八、排雷 M1~M10 逐条回应

| # | 任务书要求 | 本回执回应 |
|---|---|---|
| M1 | gap 表逐环勾选；未闭先报裁 | ✅ §二（8 环：6 闭／2 施工后实证；第 8 环新识别已列报） |
| M2 | 底线序授权出处声明＋文档漂移列报 | ✅ §三（授权＝D700；漂移列报，不代改） |
| M3 | per-rt API 实况＋方案 | ✅ §四（方案 A 推荐：`WorldGatherRegistry` 增只读 `CountOf(id, rt)`） |
| M4 | Env 护栏二选一列报 | ✅ §五（**推荐 (a) 扩展版**＋第 8 环 room 预检＋worker>0 口径） |
| M5 | 新旧输入集一致性 | ✅ §六（逐案穷举＋「触发不成立⇒零变化」硬断言；施工后实证） |
| M6 | HH.253 两处零再改；禁改面 | ✅ 承诺：`NeedScore` 入口／让位分支**不动**；`DecideTriage`/`Feasible` 语义/`census top=`/③ 本体/数据 SO/sim/AI.Core 零改 |
| M7 | 时序复述 | ✅ §七 |
| M8 | 打点三分径 | ✅ 计划：触发日加只读 `[KingdomBrain] kX ㉗断路器触发：{rt}（通道A∧③不可行∧无在册源｜room=N｜cand=M｜worker=W）`——**较任务书原文追加只读读数**（为闭第 8 环），与 `㉗接管`（接管入径）、`㉗采集下发`（落地）三分径 |
| M9 | 验收复跑（同 seed 优先） | ✅ 报备见下 |
| M10 | 禁改清单 | ✅ 承诺；champion/Holdout/harness 共享套件/AGENTS.md 零触碰 |

**跑档报备（M9·§8.6 四列齐）**：

| 列 | 内容 |
|---|---|
| 可判定最早日＋命中即停 | **D5 起跑**（条件日最早 D7 已由 HH.253 实证）；判定＝**`㉗采集下发：Stone` 首达即停**；**D90 熔断** |
| 服务哪条验收句 | `DZ-148` 验收句（石场景 ㉗ 可派发） |
| 作用域 | 全批 any-国；`FocusController.Update` 底线段 ＋ `WorldGatherRegistry` 只读查询 ＋ 只读打点 1 条 |
| 口径来源与排除项 | 入径＝`㉗断路器触发`；接管＝`㉗接管`；落地＝`㉗采集下发：{rt}`；排除项＝评分 `census top=`（支撑面·L-35）／Env 阻断（世界给定物）／肉矿粮铁无世界通道 |
| 跑档 | **同 seed 73621 优先**（D520/D556 隔离变量先例）｜槽 `chain_probe2`（沿用 HH.249/HH.253 同槽，时间戳副本留档）｜收尾＝真暂停+Save+`ExitTestRun`+退 Play（L-32）；三跑次对照基线＝HH.253 跑次 C |

---

## 九、待确认项（请策划端知悉/准驳）

| # | 项 | 执行端建议 |
|---|---|---|
| C-1 | **第 8 环（登记容量 room）预检**是否纳入触发条件 | ✅ 建议纳入（否则容量满期每日白焦点＋误导日志） |
| C-2 | **M8 打点追加只读读数**（room/cand/worker） | ✅ 建议采纳（闭第 8 环所需·纯观测） |
| C-3 | **文档漂移**（底线序描述）修订归属 | 列报，归策划端/文档端 |

> 三项均为**加法/列报**，不改变方案主体；**若策划端无异议，执行端按上述方案施工**（§四 方案 A ＋ §五 推荐版 ＋ §八 M8 扩展打点）。

---

## 十、红线自检

| 红线 | 状态 |
|---|---|
| sim-sync 核查结论先行 | ✅ §一（零 `AI.Core`／零训练仓义务） |
| 正门 `EnterTestRun`＋退 Play（L-32） | ⚪ 未进局（回执阶段；施工后执行） |
| 新旧输入集一致性（D695） | ✅ §六（穷举表＋硬断言；施工后逐步实证） |
| 跑次标注（D694②）／§8.6 四列齐 | ✅ §八（报备） |
| 零业务代码改动（回执阶段） | ✅ `Assets/_Game/**` **未做任何修改**（本回执仅实读） |
| 不修 HH.253 已落两处／③ 本体／数据 SO | ✅ 承诺（M6/M10） |
| 写-改-commit 同串、只提本批、不 push | ✅ §十一 |

---

## 十一、产物与 commit

| 文件 | 状态 |
|---|---|
| `多Agent交接/执行端/HH.255_A+㉗触发式断路器_开工回执.md` | 新建（本回执） |
| `多Agent交接/_编号登记.md` | 取号 HH.255（水位线 254→255＋在途区行） |
| `多Agent交接/_交接索引.md` | HH.254 行状态更新 ＋ HH.255 新行 |
| `多Agent交接/_任务队列.md` | HH.254 行状态更新 |
| `河谷防线_开发计划书.md` | 工作日志插行 |

- **commit**：`<见回报>`（只提上述文件，**不 push**）。
- **未做**：未改 `Assets/_Game/**`；未进 Play；未跑局。

---

> **版本**：2026-09-13（执行端）。关联：HH.254 任务书（D700）／HH.253 §十 裁决区／`DZ-148`／`L-21`/`L-29`/`L-30`/`L-31`/`L-35`／`D531①`／`D322`。
> **红线自检**：sim-sync ✅｜零业务代码改动 ✅｜不修 HH.253 态/③ 本体 ✅｜未进局未跑局 ✅｜不 push ✅。
