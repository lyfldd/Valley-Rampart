# HH.225｜⑨/③ 焦点霸占治本批 · 通用「执行失败退避」（开工回执）

> 类型：**开工回执（协议核对闸）** · 状态：🟡**停手待策划确认**（未进入实施）
> 执行端（TraeCode·Unity 轨）· 2026-09-12 · 依据：**HH.224 任务书**（D670 签发／**D672 补登记**）· 来源实证 **HH.223 §3.2**（(C) 全链 `file:line`＋三样本同因）
> 取号：遵 D640 #10（水位线 HH.224 → **HH.225**，独立单行 commit `3a159d1`）

---

## 一、复述主修（逐条对照任务书 §一）

| 任务书条款 | 复述（我照此实施） |
|---|---|
| §一.2-1 通用「执行失败退避」 | `ExecuteBuildFocus` **两失败分支**（[KingdomBrain.cs:1190-1195](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1190-L1195) 选址无落位 ／ [`:1206-1208`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1201-L1208) `TryBuild=false`）写 **per-kingdom × per-action** 失败计数/冷却；评分面 `score` 乘退避因子 |
| §一.2-2 🔴 退避必须自愈 | 状态变化（目标变化／资源跨门槛／**新候选格出现**）即复位；**禁只做单向降权** |
| §一.2-3 保留分层 | `Feasible` **不做几何探测**（否决"把选址搬进评分面"，保 D525 §3.7 分层） |
| §一.2-4 数值走 SO | 退避因子／冷却天数／复位阈值**一律 SO 外置**（`so-data-driven`） |
| §1.3 先诊断后改 | 本回执＝诊断三件（镜像／在场性／回归影响）；**停手待确认后再动业务码** |
| §二-5 | 长局起跑前必复原容器档位 ⇒ **已于 `ecd5cd1` 复原，实读见 §六** |

**病灶链（已实证，非推断）**：`UtilityScorer.cs:227-232`（`WallGap` 纯状态函数·无失败记忆）→ `:133-142`（`score` 三因子可同时顶格，实测 k2 D6 `⑨ score=1.000`）→ `:467-475`（`Feasible` 只判资源＋存量，"选址/前置归执行门面"）→ `KingdomBrain.cs:1188-1195`（`TryPick` 无落位 ⇒ 日志＋`Bump(ok:false)`＋`return`，**无降权/无冷却**）⇒ 永久霸占（三样本 181/181、103/106、17 中 wall5）。

---

## 二、§1.3-1 镜像核查（红线① · L-33）⇒ **结论：不在镜像内，可直改，不走 sim-sync**

| 项 | 实读证据 |
|---|---|
| 检索域 | `Assets/_Game/Systems/AI.Core/` ＝ **34 个 `.cs`**（实读列全） |
| 检索符号 | `UtilityScorer`／`KingdomBrain`／`FocusController`／`PlacementScorer`／`UtilityAction`／`ExclusiveGap` |
| 结果 | **零命中**（34 文件全无） |
| **阳性对照（L-29）** | 同一模式在 `Assets/_Game` 全域命中 **31 个文件**（`AI/KingdomBrain/*` ＋消费面 `DayCycleSettlement`／`TerritorySystem`／`KingdomState` 等）⇒ **grep 通路有效，零命中是真零命中** |
| 待改 4 文件所在 | `Assets/_Game/Systems/AI/KingdomBrain/`（`UtilityScorer.cs`／`KingdomBrain.cs`／`PlacementScorer.cs`／`FocusController.cs`）——**域外** |

⇒ **结论**：`AI.Core`＝单位级战术决策核（Attention/L1/L2/L3/Tuning/Memory），**不含王国行动评分执行面** ⇒ **无需 `sim-sync`**（与 HH.195/HH.220 同判，本轮独立复验）。

---

## 三、§1.3-1′ 在场性核查（既有"失败记忆/冷却/退避"机制 ⇒ L-33）

| 项 | 实读证据 |
|---|---|
| 检索域 | `Assets/_Game/Systems/AI/KingdomBrain/`（全部 .cs） |
| 检索符号 | `退避`／`冷却`／`cooldown`／`Cooldown`／`backoff`／`Backoff`／`failCount`／`失败计数`／`Retry`／`retry` |
| 结果 | **仅 1 命中，且为注释**：[KingdomBrain.cs:1086](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1086)「领土扩张引擎=…ExpandTick（D326 升序/**冷却5日**…）」＝**TerritorySystem 扩张节拍**，**非行动级退避** |
| **阳性对照** | 同模式在 `Assets/_Game` 全域命中 **20 处／8 文件**（`AttentionTuningConfig.cs`×5、`TuningSnapshot.cs`×3、`NpcProfessionDef.cs`×4…）⇒ grep 有效 |
| **明确排除项（L-33 要点）** | `AI.Core/Memory/HitCooldownStateMachine.cs`＋`HitCooldownState.cs` ＝ **NPC 受击冷却状态机**（`IMemoryComponent`，住 `NPCBrain`，三态 Normal/Caution/Probe）——**单位级，与"王国行动级退避"不同层**，**不作为"等价路径已实装"的论证对象**（正是 L-33 要防的"以他路等价"） |

⇒ **在场性结论：王国行动层「失败记忆／冷却／退避」＝零实装**（无等价路径）。⇒ 本批**不是**"以他路等价"的空头判断，属**真缺口**。
**附带正面先例（仅形态参照，非镜像）**：`HitCooldownStateMachine` 给了**"降权因子 ＋ 自愈复位"**的单位级模板（`:14-17` 三态＋双路径；"Caution 期间任务类刺激 ×`stateTaskDiscount`"＝降权形态）⇒ 王国行动级可**同构**实现，**但不共享代码**。

**既有可复用件（原地扩展，避免新造）**：
- [KingdomBrain.cs:67-86](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L67-L86)：`DispatchStat`＋`s_dispatch`（`private static Dictionary<int,…>`）＋`Bump(kid,train,ok)`＋`GetDispatch`＋`ResetDispatchStats`——**`Bump` 已是两失败分支的共同汇聚点**，天然是失败计数的写入位。
- ⚠️**但**：`s_dispatch` 现**只计数、不回流评分**（消费点仅 `Valley2_17_Smoke_P0.cs:269-274/332` 与 `ResetDispatchStats` 调用 `:179/:211`）⇒ **须新增"回流评分"这一步**，不可误认为已有。

---

## 四、§1.3-2 同 seed 对照方案（修前／修后）

### 4.1 **修前基线＝复用现存 `p1_fix1`，不重跑**（效率优先，D664 同世界同机制）

| 口径 | 旧值（`p1_fix1`，seed **64513**／65 日／正门 15x） | 证据来源 |
|---|---|---|
| ① `wall` 选址失败次数 | **103**（总失败 106；k1 39／k2 49／k3 3／k4 15） | 日志 `建造焦点选址失败：<id>` 计数 |
| ② census `top=` 分布 | `BuildWall` ＝ **k1 42／k2 52／k3 9／k4 52** 日（窗口 65 日） | `[DiagMilitary] … top=` 行 |
| ③ 建造落地数 | 逐 buildingId 计数（`建造焦点落地：<id>` 行）＋ `GetDispatch().buildOk` | 日志行／API |

### 4.2 修后跑（**待你确认后实施**）

- **同 seed 64513**＋**新槽 `p1_fix2`**（禁覆盖 `p1_fix1/1b`）＋正门 `EnterTestRun`＋15x；**对照窗口＝D1~D60**（两跑共同覆盖段；`p1_fix1` 因达标停于 D65）。
- **档位（依你 D670 后裁「独立常量」）**：新增 **`DIAG_CIRCUIT_DAY = 60`** 作**诊断/对照跑专用**窗口——**不回改** `CIRCUIT_BREAK_DAY`(120)／`MILITARY_STOP_COUNT`(2)（防 DZ-136 复发）；短窗跑只读 `DIAG_CIRCUIT_DAY`。
- **对照判据（改善/退化）**：① `wall` 失败次数 ↓；② `BuildWall` 登顶日占比 ↓（k1/k2/k4）；③ **建造落地数不降**（防"退避把正常建造也压掉"）。

### 4.3 需新增的读数（`L-35` 口径声明 · 否则判据无支撑量）

`[DiagMilitary]` 状态行拟增 **`avoid=<action:factor,…>`**（仅本次**非 1.0** 的项，最多 3 项，按因子升序）。
- **口径来源**：`UtilityScorer.ScoreTop` 内 `score` 实际乘入的退避因子快照（**同源实读，非另抄**）。
- **排除项**：**不含** `NeedScore`／`Feasible` 的返回值（二者保持纯函数，见 §五硬约束①）。

---

## 五、§1.3-3 回归影响评估（对 2_22／2_23 已验收批 · **实读断言面**）

| 已验收批 / 断言 | 会否受退避影响 | 实读依据 |
|---|---|---|
| `Smoke_2_22P0` **32/0** — P8a/P8b/P8c | **不影响** | 断言的是 `PlacementScorer.ComputeF1/F2` 纯函数方向性与 `TryPick` 双跑确定性（[Smoke_2_22P0.cs:435-471](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Smoke_2_22P0.cs#L435-L471)）——**退避不碰 `PlacementScorer`** |
| `Smoke_2_22P0` — **P8d 失败路径终止性** | **不影响** | 断 `半径0 ⇒ TryPick==false`（`:473-482`）；退避只改**分数**，不改 `TryPick` 返回值/异常面 |
| `Valley2_17_Smoke_P0` **B5 派遣双证** | **阈值型 ⇒ 耐受** | `b5build = r1.k1Build>0 && r2.k1Build>0`（[Valley2_17_Smoke_P0.cs:122](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_17_Smoke_P0.cs#L122)）——**>0 而非精确值** ⇒ 退避不致全压死则仍过 |
| `Valley2_17_Smoke_P0` **B4 轮间一致** | ⚠️**有风险 ⇒ 须硬约束②** | `b4Consist = r1.reachedExpand == r2.reachedExpand`（`:114-117`）；若退避状态**跨轮残留**⇒ 轮间不一致 ⇒ **32/0 滑落** |
| `Smoke_2_23RP0` **27/0** — **P4b/P4c/P4d/P4e** | ⚠️**有风险 ⇒ 须硬约束①** | 四条断言的是 **`NeedScore` 反射返回值**（`==1.0`／`<1f`）（[Smoke_2_23RP0.cs:223/229/235/243](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Smoke_2_23RP0.cs#L223-L245)） |
| `Smoke_2_23RP0` — P3a~P3f（分诊）/ P5a（通道A 建 farm）/ P6b~P6e（产能计数） | **不影响** | P3 走 `DecideTriage` 纯函数；P5a 容器**直调 `ExecuteGrainTriage`**（绕路由/绕评分，[`:272-293`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Smoke_2_23RP0.cs#L272-L293)）；P6 判 `CountProductionOf` |
| `Valley2_17_Smoke_9` **#3 性格分化**（好战 vs 经济 top 不同） | **低风险（中性态⇒等价）** | 同一王国同局面两次 `ScoreTop`（[Valley2_17_Smoke_9.cs:88-96](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_17_Smoke_9.cs#L88-L96)）——**乘同一正因子不改变两个 top 的不等关系**（除非并列）；仍受硬约束②约束（须中性起点） |
| `Valley2_17_Smoke_5`（**全 PASS** 基线） | **不影响** | 其 drive 正负例为 `MilitaryTarget` 纯函数面（HH.217 新增），退避不碰目标公式 |

### 🔴 两条**硬约束**（写入实施，防已验收批滑落）

1. **退避只乘 `score`，禁落进 `NeedScore`/`Feasible`** —— 否则 `Smoke_2_23RP0` P4b~P4e（`NeedScore==1.0`）**必 FAIL**，且违 D525 §3.7 分层。
2. **退避状态须有 `Reset` 入口，并与 `ResetDispatchStats` 同点清零**（`Valley2_17_Smoke_P0.cs:179/211` 已在调）⇒ 否则 `B4 轮间一致` 滑落；同一会话内其它 smoke 若共用 kingdom id 亦受此保护（对齐 `s_dispatch`「运行时态不入档」注释 [`KingdomBrain.cs:67`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L67)）。

**回归红线**：`Smoke_2_22P0` 32/0 ／ `Smoke_2_23RP0` 27/0 ／ `Valley2_17_Smoke_5` 全 PASS；**任一滑落 ⇒ 停工报裁，禁放宽断言**。

---

## 六、§二-5 DZ-136 档位实读（已复原 · `ecd5cd1`）

| 常量 | 实读值（`Valley_HH80_Run.cs`） |
|---|---|
| `CIRCUIT_BREAK_DAY` | [`:31`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L31) **`= 120`** |
| `MILITARY_STOP_COUNT` | [`:32`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L32) **`= 2`** |
| `JUDGE_FOCUS_KINGDOM` | [`:43`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L43) `= 3`（**换 seed 须同步改**） |
| `SEED`／`SLOT` | `:29` `64513`／`:30` `"p1_fix1b"`（**每批定案项**，本批对照拟用 `p1_fix2`） |

⇒ **长局档就绪**（120 日判定线不再被 90 截断）。本批短窗对照**另设 `DIAG_CIRCUIT_DAY`**，不动上表主档位。

---

## 七、在线判据表（`test-harness-first §八` **三列齐** · 照此实现与运行）

| # | 判据 | 可判定最早日 | 命中即停 | **服务哪条验收句** | **作用域** | **口径来源与排除项**（`L-35`） |
|---|---|---|---|---|---|---|
| **J6** | 退避**机制面生效**：同一 `(k,action)` 连续失败 ≥N 次后，该行动 `avoid<1` 出现（读数） | D10~D15（wall 失败 D6~D10 已现） | 机制已证 ⇒ **可停** | 验收句①（退避机制生效）＝**机制级** | **全批 any-国** | **来源**＝`ScoreTop` 内 `score` 实际乘入因子快照；**排除项**＝不含 `NeedScore`/`Feasible` 返回值 |
| **J7** | 退避**端到端**：`BuildWall` 登顶日**占比** ≤ 基线 −50%（或连续 ≥10 日无 wall 登顶） | D10~D30 | 判"霸占解除" ⇒ 可停 | 验收句①（wall 占比下降）＝**端到端级** | **指定国 k1/k2/k4**（霸占样本国） | **来源**＝census `top=` 行；**排除项**＝**不含 k3**（其 wall 占比本就低 9/65） |
| **J8** | 🔴**防退化（负向）**：建造落地数**连续 ≥M 日 < 基线同期** | D15 | 判"退避压掉正常建造"⇒**立即停手报裁** | 验收句①（落地数不降）＝端到端级 | **全批 any-国** | **来源**＝`建造焦点落地：<id>` 行计数；**排除项**＝单一建筑类型异常**不触发**（须总量口径） |
| — | **自愈（边界）**：构造"恒无落位"⇒退避生效；**状态变化后恢复** | 夹具（非长局） | 不进在线表，**单列边界探针 case** | 验收句②（自愈） | 指定国 | 来源＝`avoid` 复位读数；排除项＝非世界漂移所致 |

**同级/同作用域自检（§8.6）**：J6＝机制级⇒配机制级验收句 ✅；J7/J8＝端到端级⇒配端到端验收句 ✅（**未**给 J6 挂端到端句，**未**给 J7/J8 挂机制句）；J7 作用域＝指定国（**避免他国噪声切掉样本国证据**）。

---

## 八、红线勾选与请确认

| 红线 | 状态 |
|---|---|
| ① AI.Core 镜像 ⇒ sim-sync | ✅ **§二 已核：零命中（阳性对照过）⇒ 不跨仓** |
| ② 行为级（F 级）双门禁 | ⬜ 待实施：编译 0 警 0 错 ＋ `benchmark --suite v9 --battles 100` 读锚无退化 >5% ＋ holdout 不退 ＋ determinism 逐字节 —— **⚠️请确认**：本批是否确需跑训练仓门禁（Unity 侧王国脑不在训练仓镜像内，见 §二）；若你裁"仅 Unity 侧回归"，我按 `Smoke_2_22P0/2_23RP0/2_17_Smoke_5`＋同 seed 对照执行 |
| ③ 正门 15x ＋ 收工退 Play（L-32） | ✅ 已列（对照跑按此） |
| ④ 同 seed 干净局（L-22）／枚举尾插（L-28） | ✅ 已列（本批拟**不新增枚举**：退避键用既有 `UtilityAction` 值索引） |
| ⑤ 长局前复原档位（DZ-136） | ✅ **§六 已复原并实读** |
| ⑥ 只改 AI 决策/执行域、不碰 champion/训练仓禁改域 | ✅ 拟改面＝`AI/KingdomBrain/UtilityScorer.cs`＋`KingdomBrain.cs`（＋SO） |

**请确认（5 项 · 缺一不动业务码）**
1. **退避状态载体**：我倾向**静态字典**（对齐 `s_dispatch` 先例 `KingdomBrain.cs:69`，「运行时态不入档」＝读档后归零属已知边界）⇒ 是否同意？（替代＝入 `KingdomState` 存档，改动面更大）
2. **参数 SO 落点**：退避因子下限／失败 N 次门槛／冷却天数／复位阈值 ⇒ **新建 `ActionBackoffConfig.asset`** 还是并入既有 `SituationConfig`／`KingdomBrainConfig`？（`so-data-driven`）
3. **"通用"边界**：退避是否**统一适用于全部建造类行动**（②③④⑤⑨⑰a~e㉔㉕…，皆走 `ExecuteBuildFocus`）？是否也含非建造类（⑥⑦⑯）？
4. **对照跑档位命名**：`DIAG_CIRCUIT_DAY = 60`＋槽 `p1_fix2` 是否准？
5. **读数字段**：`[DiagMilitary]` 状态行新增 `avoid=<action:factor,…>`（口径§4.3）是否准？J6 依赖它。

---

*执行端 2026-09-12（HH.225 开工回执，取号 `3a159d1`）。本回执＝只读核查＋方案报审，**未改业务码、未跑局、未 push**；停手待策划确认。*

---

## 九、策划端裁决（D675，2026-09-12）

**结论：回执验收成立 → 准予实施。** 判据三直读已过（`KingdomBrain.cs:69/78/80-86/1190-1195/1200/1202`＋`UtilityScorer.cs:227-232`）。

### 逐项裁（5＋1）

| # | 请确认项 | 裁决 |
|---|---|---|
| 1 | 退避状态载体＝静态字典 | ✅ **准**（对齐 `s_dispatch` 先例「运行时态不入档」）。⚠️**勘正**：`Bump(id, train, ok)`（`:80`）**只有 train/build 二元维、无 action 维** ⇒ **不可直接复用** ⇒ 须建 **per-action 退避表**（`Dictionary<int,Dictionary<int,BackoffStat>>`，键＝`kingdomId × actionId`）；**禁 per-call-site 粒度**（过细会掩盖根因） |
| 2 | 参数 SO 落点 | **新建 `ActionBackoffConfig.asset`**（**不并入** `SituationConfig`/`KingdomBrainConfig`）——单职责＋`so-data-driven`＋`ResourceBiasConfig` 先例（R-B2） |
| 3 | "通用"边界 | **含非建造类 ⑥⑦⑯**（**是**统一适用全部行动）——退避是**机制级**问题（"选中但执行失败不降权"与行动类型无关）；只做建造类＝重犯 `DZ-123`/`D672`「改全局条律前未枚举全适用面」之误。**但实施前须先给"各行动失败落点清单"**（本回执只列了建造类） |
| 4 | `DIAG_CIRCUIT_DAY=60`＋槽 `p1_fix2` | ✅ **准**（**禁回改主档 120/2**） |
| 5 | `avoid=<action:factor,…>` | ✅ **准**（口径＝`ScoreTop` 实乘因子快照；**排除项＝不含 `NeedScore`/`Feasible`**，`L-35`） |
| ⑥ | 训练仓门禁（红线②） | ✅ **准"仅跑 Unity 侧回归"**（`Smoke_2_22P0` 32/0 ＋ `Smoke_2_23RP0` 27/0 ＋ `2_17_Smoke_5`）——**但交付报告须写明"本批未经 sim 门禁，因 `AI.Core` 零命中＋域分离"**；**若后续任何改动触及 `AI.Core` ⇒ 回补 sim-sync 规定序** |

### 🔴 判据三直读新增（执行端漏项）

**修三处，非两处**：
- `:1194`　选址无落位 → `Bump(false,false); return;`（回执已列）
- **`:1200`　`BuildController.Instance == null` → `Bump(false,false); return;`（回执漏列）** ⇒ **须补观测日志**（现为零日志静默失败；同 `HH.88 件3` 静默点观测口精神）
- `:1202`　`TryBuild=false`（回执已列）

### 硬约束（采信升硬＋我补一条）

- **①** 退避**只乘 `score`**、**禁**落进 `NeedScore`/`Feasible`（否则 `2_23RP0` P4 必 FAIL＋违 `D525 §3.7` 分层）。
- **②** 退避状态须有 **`Reset` 入口**并与 **`ResetDispatchStats` 同点清零**（`Valley2_17_Smoke_P0.cs:179/211` 已在调）；**并须核 `SituationSnapshot.Clear()` 同族先例**（`SituationSnapshot.cs:138` 自注"对齐 KingdomBrain.ResetDispatchStats 先例"）。
- **③（我补）** 退避须**自愈**（状态变化即复位）**且有上限**；达上限仍失败 ⇒ **写 `verdict` 升级报裁**——防"永久弃建"（把死循环改成静默放弃）。

### 下一步

按批序：**实施**（三处失败点＋per-action 退避表＋新 SO＋`avoid=` 读数）→ **同 seed 对照**（`p1_fix2`，D1~D60）→ **在线判据表 J6/J7/J8**（三列齐，照 §七）→ **交付报告**（按水位线取号；须含：三处落点清单／未经 sim 门禁声明／自愈与上限证据／回归三容器）。

**教训核查：无新增条目**（`L-33` 在场性核查做得扎实——含阳性对照＋明确排除 `HitCooldownStateMachine` 不作等价路径论证）；`L-24` 家族核查＝无新实例。
