# HH.256 HH.254「A+ ㉗ 触发式断路器」交付报告（执行端 → 主策划端）

> 取号：HH.256（账本水位线 HH.255 → 256；D640 #10 先登记后落盘）
> 作业依据：**HH.254 施工任务书**（D700 签发·Gate=`G1-1`）＋ **HH.255 开工回执**（本批已列报 M3/M4 与待确认 C-1~C-3，**用户裁＝按推荐口径直接施工**）
> 前置必读已完成：HH.251/252/253 全链 · HH.253 §十 裁决区（D700）· `FocusController.cs` 全文 · `ActionBackoff` · 台账 `DZ-148`
> 状态：**验收线 1 ✅ 命中（石场景落地首达 D7）**／验收线 2 ⚠️（判据口径未达·含报裁）／其余 ✅

---

## 〇、一句话结论

**㉗ 触发式断路器落地成功、石场景派发首达命中**：同 seed 复跑（seed 73621）**D7** 即出现 `k4 ㉗断路器触发：Stone → ㉗接管 → ㉗采集下发：Stone 新立案 6 个` 三段全链，全窗 **石派发 3 次／木派发 2 次**（矩阵判据面 `Stone=3 Wood=2`）——`DZ-148` 验收句（石场景 ㉗ **可派发**）**达成**。

**但验收线 2（one-shot 自限 ≤1 次/rt）字面未达**：`k4 Stone` 触发 **2 次**（D7、D11）。根因＝**首批 6 个石源被采尽后 `Prune` 剔除 ⇒ 条件⑤ 重新成立**（石堆为一次性可采实体）⇒ 属「**按需重采**」而非「振荡」（无「在册源>0 仍触发」）。任务书对 ⑤ 的预期（"注册一次后条件⑤不再成立"）**未预见源头消耗** ⇒ **判据口径 gap（L-30 家族）**，处置见 §八 报裁。

---

## 一、sim-sync 核查结论（红线项）

**结论＝本批零 `AI.Core` 义务、零训练仓义务**（HH.255 §一 已正式复述，本批施工后不变）：

| 核查项 | 证据 | 结论 |
|---|---|---|
| 改动对象 | `_Game/Systems/AI/KingdomBrain/FocusController.cs`、`_Game/Systems/World/WorldGatherRegistry.cs` | 属 `_Game` |
| `AI.Core/**` | `Grep FocusController\|WorldGatherRegistry\|Alarm\|Triage` ⇒ **零命中** | 无镜像 |
| 训练仓 `harness/**` | `Grep FocusController\|WorldGatherRegistry\|WorldGatherSource\|popAlarm\|grainAlarm` ⇒ **零命中** | 零同步义务 |

---

## 二、§0 L-30 全链门 gap 表（施工后逐环闭合）

| # | 门环 | 施工后证据 | 状态 |
|---|---|---|---|
| 1 | 条件成立日存在 | `k4 D7/D11`、`k2 D15` 三次触发（日志实读） | ✅闭 |
| 2 | 候选入池（旁证） | 触发路径不依赖入池（底线直写）；㉗ 评分 `top=2` 仍在 | ✅闭 |
| 3 | **焦点切换** | **三次触发即三次 `SetFocus(27)`**（当日 `ExecuteFocus` 路由到 `ExecuteWorldGatherFocus`——由同 tick 的接管/派发日志连号证明） | ✅**已闭（施工前 🔜 → 现已实证）** |
| 4 | 执行路由 | `KingdomBrain.cs:630-632` | ✅闭 |
| 5 | 接管分支放行 | 三次均打出 `㉗接管：通道A且③不可行（Stone）` | ✅闭 |
| 6 | Advertise 派发 | 三次 `㉗采集下发：Stone 新立案 6 个`（候选 7/8/7）；全窗 `Env 阻断=0` | ✅闭 |
| 7 | 观测口径 | `ChainProbeFacade` 三分径正则在场（新增 `㉗断路器触发` 未入矩阵口径，走日志实读） | ✅闭 |
| **8** | **登记容量 room>0**（HH.255 新识别） | **三次触发均 `room=6`（>0）**；注册后 `在册 6`（= `maxSourcesPerKingdom` 上限）；**未出现白焦点日**（`Env 阻断=0`）⇒ 该环在本跑次**非阻塞**、runtime 已闭 | ✅闭（本跑次） |
| **9** | **🆕 重触发环**（本报告新识别·任务书未含） | `k4 Stone` **触发 2 次**（D7/D11）：首批 6 源采尽 ⇒ `WorldGatherSource.IsValid=false` ⇒ `Prune` 剔除 ⇒ `CountOf(k4,Stone)=0` ⇒ 条件⑤ 重新成立 | ⚠️ **判据口径 gap**（见 §八 报裁） |

---

## 三、实施落点（2 文件·逐处）

### 3.1 `FocusController.cs`（＋45 行）
| 处 | 内容 |
|---|---|
| a | `Update` 常设底线段**插入触发级**：`grainAlarm` 之后、`popAlarm` 之前 ⇒ `if (TryGatherBreaker(kingdom, day)) return;`（新序＝**粮 → ㉗断路器 → 人口 → 被攻**；**D322 序变更授权出处＝D700**，代码注释显式声明） |
| b | 新增 `private bool TryGatherBreaker(KingdomState, int day)`——条件**全 AND**（判据**全部同源复用**，L-31 禁另造）：①`KingdomBrain.ResolveTriageResource` 有断供 ②`WorldGatherRegistry.TryMapWorldResource`＝石/木 ③`KingdomBrain.DecideTriage == BuildCapacity`（通道A） ④`UtilityScorer.Feasible(kingdom, ③def) == false` ⑤`reg.CountOf(id, rt) == 0`（one-shot 自限） ⑥`WorldGatherRegistry.HasCandidate(id, rt)` ⑦`reg.RoomOf(id) > 0` ⑧`workerCount > 0` ⇒ `SetFocus(27)` ＋ 只读打点 |
| c | 触发打点（M8·三分径之一）：`[KingdomBrain] kX ㉗断路器触发：{rt}（通道A∧③不可行∧无在册源｜room=N｜worker=W）` |

### 3.2 `WorldGatherRegistry.cs`（＋33 行·**全为只读增量**）
| 处 | 内容 |
|---|---|
| d | 新增 `public int CountOf(int kingdomId, ResourceType resource)`——per-rt 在册源数（与 `Advertise` **同源** `TryMatchPoint` 过滤·零新状态·不触 `WorldGatherSource`） |
| e | 新增 `public int RoomOf(int kingdomId)`——登记余量（触发预检 ⑦ 用） |
| f | 新增 `private static int RoomOf(List<Entry>, WorldGatherConfig)`＝余量公式**单源**，`Advertise` 内联公式改为调用它（L-31 禁另造；`Advertise` 行为逐字不变） |

**零改**＝HH.253 两处（`NeedScore` 入口／`ExecuteWorldGatherFocus` 让位分支，**保留不动＝双通道并存**）／`DecideTriage` 纯函数／`Feasible` 语义／`census top=` 结构／③ 本体／数据 SO／sim／AI.Core。

---

## 四、新旧输入集一致性核验（红线·D695）

`FocusController.Update` 底线段新序＝**粮 → ㉗断路器 → 人口 → 被攻**；逐案穷举 ＋ **结构性保证**：

| 日型 | 触发条件 | 原行为 | 本批行为 | 判定 |
|---|---|---|---|---|
| 粮警日 | 任意 | ⑤ 强制 return | **⑤ 强制 return**（断路器位于其后，不可达） | ✅ 零变化 |
| 人口警日 | **不成立** | ⑥ 份额/让位日 | ⑥ | **✅ 零变化** |
| 人口警日 | 成立 | ⑥ | **㉗断路器**（序在人口前） | 授权序变更 |
| 被攻日 | **不成立** | ⑭ | ⑭ | **✅ 零变化** |
| 被攻日 | 成立 | ⑭ | **㉗断路器** | 授权序变更 |
| 普通日 | **不成立** | 评分 argmax ＋ 3 日防抖 | 同 | **✅ 零变化** |
| 普通日 | 成立 | 评分 argmax ＋ 防抖 | **㉗断路器强制（跳防抖）** | 本批目标行为 |

**结构性保证（关键）**：插入形式为 `if (TryGatherBreaker(...)) return;`——`TryGatherBreaker` **无任何副作用**，返回 `false` 时后续代码**逐位照旧**。本跑次 60 日中仅 **3 日**触发（余 57 日零变化由结构保证，非遗漏）。
**新增 guard 全为「前置排除」**（不会把原可执行路径判否）：⑤⑥⑦⑧ 皆在触发成立后才起作用，且**不改动**任何既有函数的返回值语义。

---

## 五、跑次标注与实证读数（D694②）

**跑档**：正门 `EnterTestRun`｜seed **73621**｜槽 **`chain_probe2`**｜观测窗 **D1~D60**｜收工＝**观测窗口到期 @D60**｜采样 **3315**｜收尾＝真暂停+Save+`ExitTestRun`+**退 Play**（实测 `isPlaying=False, ts=1`）。
**产物**：`chain_probe_matrix.txt`（SHA256 `3572D1DC2167E59DB5F3507A9D187AFB6562029D05F5CBD735612A2E8FEBEFD6`）＝副本 `chain_probe_matrix_20260913_170722.log`（同哈希）。

### 5.1 判据面（矩阵）
| 项 | 本批（跑次 D） | 对照：跑次 C（HH.253·无断路器） |
|---|---|---|
| ㉗ **实际派发** | **`Stone=3` ／ `Wood=2`**（共 5） | `Stone=0` ／ `Wood=2` |
| ㉗ 评分 top 次数 | 2 | 3 |
| ㉗ Env 阻断 | 0 | 0 |
| ㉗ 分诊支撑 | `Stone=BuildCapacity:118`／`Stone=NoOp:118`／`Wood=NoOp:236` | `Stone=BuildCapacity:154`／`Stone=NoOp:82`／`Wood=NoOp:236` |

### 5.2 三段全链（日志实读·触发日以「前一 DiagMilitary 日标」定位）
| # | 触发日 | 国 | 链条 |
|---|---|---|---|
| 1 | **D7** | k4 | `㉗断路器触发：Stone（…｜room=6｜worker=8）` → `㉗接管：通道A且③不可行（Stone）` → `㉗采集下发：Stone 新立案 6 个（候选 7，在册 6）` |
| 2 | **D11** | k4 | 同型（`候选 8，在册 6`） |
| 3 | **D15** | k2 | 同型（`候选 7，在册 6`） |
| （木·回归） | D38 | k4 | `㉗采集下发：Wood 新立案 6 个（候选 89，在册 6）` |
| （木·回归） | D51 | k1 | `㉗采集下发：Wood 新立案 6 个（候选 77，在册 6）` |

### 5.3 支撑读数（非判据·D694② 禁跨跑次混比）
- `D60 k4` 行：本批 `worldGatherIncome@D8`；跑次 C 同行为 `@D48` ⇒ **世界采集入库首达提前约 40 日**（**仅参考**：跨跑次世界演化分支不同，不作判据）。
- 第 8 环 runtime：三次触发 `room` 均 6（>0）⇒ 容量环**未成为阻塞**；注册后 `在册 6`（满额）。

---

## 六、验收线 §六 逐条

| # | 线 | 裁决 | 依据 |
|---|---|---|---|
| 1 | **石场景落地**（条件日 ⇒ 当日 `㉗采集下发：Stone` 首达） | ✅ **成立** | D7 首达（三段全链）；全窗 `Stone=3`；`㉗断路器触发` 打点佐证入径 |
| 2 | **one-shot 自限**（全窗每 rt 触发打点 ≤1 次） | ⚠️ **字面未达** | `k4 Stone` 触发 **2 次**（D7/D11）；根因＝源采尽腾位（§二 第 9 环）⇒ **报裁** |
| 3 | 原行为回归（三组） | ✅ **成立** | 见 §七 |
| 4 | 零改动面 | ✅ 成立 | `Assets/_Game/**` 改动**仅 2 文件**（`FocusController.cs` `+45`／`WorldGatherRegistry.cs` `+33/-…`）；**AI.Core/sim 零触碰**；HH.253 两处零再改 |
| 5 | 编译 | ✅ 0 error | `refresh_unity(force, compile=request)`→`read_console(error)` 仅 1 条存量（Visual Scripting）；IDE `GetDiagnostics` 两文件**空** |
| 6 | 教训兑现（§0 gap 表／新旧输入集／跑次标注／四列齐） | ✅ 已出 | §二（含第 8 环闭合＋第 9 环新识别）／§四／§五／四列齐见 §五.1 |

---

## 七、回归三组证据

| 组 | 判法 | 证据 | 结论 |
|---|---|---|---|
| ① 三底线日行为不变 | 粮/人口/被攻日 | **代码位序**：断路器在 `grainAlarm` **之后** return（粮警日不可达）；触发仅 3 日，其余 57 日 `TryGatherBreaker==false` ⇒ 无副作用 | ✅ |
| ② 触发条件不成立日 ＝ HH.253 态逐位不变 | 评分/防抖照旧 | **结构性**（`false` 即零副作用）＋ `census top=GatherWorldResource=2`（评分入口**仍生效**·双通道并存） | ✅ |
| ③ 通道B 木路径照旧 | 木派发在场 | `Wood=2`（k4 D38／k1 D51）与 HH.249/HH.253 同 seed 读数**一致** | ✅ |

---

## 八、报裁（1 项）

### 8.1 根因（第 9 环）：one-shot 自限的语义＝「**在册期**自限」，非「**全窗**一次」

- 任务书 §一.2 ⑤ 的预期："one-shot 自限——**注册一次后条件⑤不再成立**，天然退避"。
- 实际机制：石堆为**一次性可采实体**（`BuildingDef.isConsumable`）⇒ 采完即 `WorldGatherSource.OnGatherCompletion → _active=false` ⇒ `IsValid=false` ⇒ `WorldGatherRegistry.Prune` 剔除 ⇒ `CountOf(id, Stone)` 归 0 ⇒ **条件⑤ 重新成立** ⇒ 再次触发（D7 → D11，间隔 4 日；木路径 D38→D51 亦同型）。
- ⇒ **不是振荡**（无「在册源>0 仍触发」、无重复登记/重复派发），而是**按需重采**（每批 6 源用尽后补采）。

### 8.2 报裁候选

| 项 | 内容 | 评估 |
|---|---|---|
| **Q-1（推荐）** | **口径改判**：验收线 2 由「全窗每 rt ≤1 次」改为「**无重复触发**＝不存在『该 rt 在册源>0 时仍触发』」——即把 one-shot 语义明确为**在册期自限**（与 `WorldGatherRegistry` 既有「源失效即腾位、可再立案」设计一致） | ✅ 与机制现状自洽；「按需重采」正是"即时采集"应有之义；**判据可判定**（逐次触发均可核对当时 `CountOf(id,rt)==0`） |
| Q-2 | 加**冷却**（触发后 N 日不再触发） | ⚠️ 需跨日持久态（违「无持久态」倾向）；且会牺牲采尽后的补给 |
| Q-3 | 全窗每 rt 硬限 1 次 | ❌ 石采尽后不再补采 ⇒ 供给链断，与 A+ 目标（石持续可得）相悖 |

**执行端建议＝Q-1**（口径改判·无需改码）；若采 Q-2 需策划端明确 N 与状态载体。

### 8.3 附带列报（不涉结论）
- **文档漂移**（HH.255 §三 已列报·仍待策划端/文档端处置）：`2_17 步骤9 焦点模型` 的「常设底线序＝粮→人口→被攻」描述已与本批代码（粮→㉗→人口→被攻）不一致。
- **打点口径微差**（HH.255 C-2·已获准）：触发打点含 `room/worker`，`cand` 读数由紧随其后的 `㉗采集下发` 行（含 候选M/在册K）承担，避免重复全领土扫描。

---

## 九、红线自检

| 红线 | 状态 |
|---|---|
| sim-sync 核查结论先行 | ✅ §一（零 `AI.Core`／零训练仓义务） |
| 正门 `EnterTestRun`＋退 Play（L-32） | ✅ 正门起跑；收尾实测 `isPlaying=False, ts=1` |
| 新旧输入集一致性（D695） | ✅ §四（穷举＋结构性保证「false 即零副作用」） |
| 跑次标注（D694②）／§8.6 四列齐 | ✅ §五（含哈希/副本名/采样） |
| 零改动面 | ✅ `Assets/_Game/**` 仅 2 文件（本批）；AI.Core/sim 零触碰 |
| 不修 HH.253 态／③ 本体／数据 SO | ✅（M6/M10 守住） |
| 写-改-commit 同串、只提本批、**不 push** | ✅ §十 |

---

## 十、产物与 commit

| 文件 | 状态 |
|---|---|
| `Valley Rampart/Assets/_Game/Systems/AI/KingdomBrain/FocusController.cs` | 修改（底线插入＋断路器＋打点） |
| `Valley Rampart/Assets/_Game/Systems/World/WorldGatherRegistry.cs` | 修改（只读 `CountOf(id,rt)`／`RoomOf`＋room 单源化） |
| `多Agent交接/执行端/HH.256_A+㉗触发式断路器_交付报告.md` | 新建（本报告） |
| `多Agent交接/_编号登记.md`／`_交接索引.md`／`_任务队列.md`／河谷防线_开发计划书.md | 回写 |

- 矩阵产物 `Logs/ChainAudit/chain_probe_matrix*.log`＝**gitignore 域**，不入库（就地留档）。
- **commit**：`81aebe6`（本批串：两 `.cs` ＋ 本报告 ＋ 账本/索引/队列/主计划书回写）——**只提本批文件，不 push**。

---

> **版本**：2026-09-13（执行端）。关联：HH.254 任务书（D700）／HH.255 回执／HH.253 §十／`DZ-148`／`D531①`／`D322`／`L-21`/`L-30`/`L-31`/`L-35`。
> **红线自检**：sim-sync ✅｜正门/退 Play ✅｜新旧输入集 ✅｜跑次标注 ✅｜AI.Core 零触碰 ✅｜不 push ✅。
