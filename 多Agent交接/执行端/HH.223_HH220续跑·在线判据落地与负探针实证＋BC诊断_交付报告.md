# HH.223｜HH.220 续跑 · 在线判据落地＋负探针实证＋(B)(C) 正式诊断（交付报告）

> 类型：**交付报告** · 状态：🟡**待验收**
> 执行端（TraeCode·Unity 轨）· 2026-09-12 · 锚点：**HH.222 开工回执 §五**｜**HH.222 裁决区（D669）**｜**HH.220 §五（D665/D666）**｜`test-harness-first §八`（**L-34**）
> 取号：遵 D640 #10（水位线 → **HH.223**，独立单行 commit `1afdf65`）

---

## 一、做了什么（4 项；业务码零改动 · AI.Core 零触碰）

### 1.1 🔴 作用域规则落地（D669 裁决① —— 本批核心追加项）

**规则原文**：判据须与其服务验收句「**同级 ＋ 同作用域**」。落点＝容器 `CheckJudges` 由「遍历所有 AI 国、任一命中即停全批」改为**逐判据声明作用域**。

| 落点 | 内容 |
|---|---|
| [Valley_HH80_Run.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L40-L51) | 新增 `struct JudgeCfg { Kind; OnlyKingdom; Kingdom }` ＋ `JUDGE_FOCUS_KINGDOM = 3`（**本 seed 64513 唯一可至军事期者**，HH.220 实证 D65）；`JUDGES` 改三条 `JudgeCfg`：**J1 Deadlock＝全批**／**J2 StoneCold＝指定国 k3**／**J3 NoIncome＝全批** |
| [Valley_HH80_Run.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L133-L160) | `CheckJudges`＝**外层遍历判据、内层遍历王国**，`if (cfg.OnlyKingdom && k.id != cfg.Kingdom) continue;` ⇒ 全批级「任一 AI 命中即停」／指定国级「仅该国命中才停」 |
| [Valley_DiagMilitary.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs#L51-L78) | `TryJudge` → **`TryJudgeOne(int kId, JudgeKind kind, int day, out detail)`**（单判据查询；**作用域归容器**，探针不做范围假设） |

**动机实证（策划端抓到，本批生效）**：k1/k2/k4 石链本就死（HH.220 §一-7 实证）⇒ 若 J2 仍为「全批任一」，正向批会在 **~D10 停掉全批、切掉 k3 的「→军事 @D65」证据**（D664 验收句②）。

### 1.2 `chSrc` 口径修正（判据③ 漏报治理）

`CountWorldResourceSources()` → **`CountGatherSources()`（只数类型名含 `Gather`）＋ `CountByproductSources()`（含 `Byproduct`）**（[Valley_DiagMilitary.cs:80-107](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs#L80-L107)）。状态行改打 **`chSrc=<Gather>/<Byproduct>`**。
**实测验证见 §二.3**——旧口径 `chSrc=4` 全部是**矿洞副产搬运源**，会把"AI 采集通道未落地"灌水成"有源"。

### 1.3 (B)(C) 取证件：③⑨⑩ 逐行动读数（HH.220 §4.6 探针①，接现成机制）

`DiagMilitary` 每日新增三行 `DumpAction`：**③建产能**（`UtilityAction.BuildCapacity`）／**⑨建城墙**（`BuildWall`）／**⑩强化采集**（`BoostHarvest`）（[Valley_DiagMilitary.cs:214-217](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs#L214-L217)）。
⇒ 补齐 §4.4 指出的「逐行动读数只覆盖 ⑯⑰、③⑨ 无逐行动行」缺口，使「③ 65 天未登顶」可区分**被拦**（`stageFiltered/noNeed/infeasible`）与**分不够**。

### 1.4 编译与门禁

| 项 | 结果 |
|---|---|
| 编译 | **0 错**（`unity_editor.start_compilation_pipeline` ⇒ `errors: 0`；6 条 warning 全在**既有无关文件**：`Valley2_17_Smoke_5/P0`、`Valley2_21A`、`Valley_HH128`、`Valley2_20_Smoke_Race`，本批两文件零 warning） |
| 红线 | 业务码**零改动**（仅 `Assets/Editor/Smoke/*` 两文件）／**AI.Core 零触碰**／未 push／枚举无新增（L-28 N/A） |

---

## 二、负探针实证（**最终代码** · 正门 15x · 槽 `p1_fix1b`）

### 2.1 结果：J1 命中、自动中断、**124 秒**

```
===== HH.80 三考收工 =====
判据命中：Deadlock（k1 drive≤0 且 target<门 连续5日）@D6
终速=0 存盘 p1_fix1b=True 时间=09:37:16
```
- 起（观测器）09:35:12 → 止 09:37:16 ＝ **124 秒**（对比 HH.220 白跑 51 日 ≈**21 分钟**）⇒ **机制 1+3 端到端有效**。
- **熔断 D90 未达**、`MILITARY_STOP_COUNT` 未达 ⇒ 停跑**确由判据命中触发**（非兜底）。

### 2.2 判据轨迹（逐日全量 · `verdict=` 打标）

| 日 | 四国 `deadlock` | `stoneCold` | `militaryTarget` | `drive` |
|---|---|---|---|---|
| D2 | 1 | 0（`noIncome:1`） | 2/2/2/2 | 0.0000 |
| D3 | 2 | 1 | 2/2/2/2 | 0.0000 |
| D4 | 3 | 2 | 2/2/2/2 | 0.0000 |
| D5 | 4 | 3 | 2/2/2/2 | 0.0000 |
| **D6** | **5 ⇒ 命中** | 4 | 2/**3**/2/2 | 0.0000 |

- **死滞复现**：`drive≡0.0000`、`target` 全程 **2~3 < 门 4** ⇒ 修前 HH.214 同型（目标自限）；`warrior=0` 全程。
- k2 D6 已达 `Expand`（其余 Develop）⇒ 阶段推进未受影响。
- 证据：`Logs/P1/p1_log_20260912_093512.log`／`hh80_run_status.log`。

### 2.3 `chSrc` 口径修正对照（实证）

| 代码版本 | 状态行 | 结论 |
|---|---|---|
| 旧（`092821` 跑） | `chSrc=4` | 混合口径 ⇒ 判据③**漏报**"采集通道未落地" |
| **新（本次）** | **`chSrc=0/4`** | **采集源＝0、副产源＝4** ⇒ 4 个源**全部是矿洞副产搬运源**（`MineByproductComponent`），修正必要性当场实证 |

### 2.4 收尾纪律与复原

| 项 | 状态 |
|---|---|
| SO 测试件 | `SituationConfig.asset` `internalDriveWeight` **0 → 复原 0.1**；`git status` 该文件**无 diff**（实测确认） |
| L-32 | 容器末尾 `ExitPlaymode`；`get_state` 实测 `isPlaying: false`（无 1x 余留世界） |

### 2.5 列报：两次负探针**非逐值复现**（不构成本批回归）

旧次（092821）与新次（093512）D6 同日读数：`k3 food 82→52`、`k4 food 126→59`（gold/stone/wood/target 一致）。
⇒ 属 **HH.202 已登记**「同 seed 双跑 D2 起逐行漂移」**同族现象**（**首次发现即在案**），非本批引入；**判据命中日两次一致（D6）**，结论不受影响。

---

## 三、(B)(C) 正式诊断

### 3.1 (B) 资源链（**D665 §5.2 已闭合**；本批补强证据，不重复裁断）

| 证据（本批新增，实读） | 值 |
|---|---|
| `chSrc=0/4`（§2.3） | **世界资源点采集源＝0** ⇒ 「AI 采集世界一次性资源」通道**未落地**（代码级结论 `HH.220 §4.3` 的事件级佐证） |
| ③建产能 逐行动读数（D2~D6 四国逐日） | `needKind=CapacityGap need=1.000`／**`feasible=False`**／`feasibleReachable=True`／`cost(g/s/w/f)=50/0/0/0`／`buildingId=quarry`／`stageGateBlocked=False` |
| `[DiagTriage] Stone=BuildCapacity`（四国逐日） | 分诊面**已识别**石缺口并指向 ③ ⇒ 缺口**被看见但补不上** |
| `[DiagCapacity] Stone:prod=0/in=0`（k1/k2/k4；k3 有 quarry 时 `prod=1`） | 与 HH.220 §4.2 口径一致（`prod`＝**在产产能建筑数**，非石头存量） |

⇒ **(B) 结论：`Stone:prod=0/in=0` 的根因＝「AI 无消费世界一次性资源的通道」＋「③建产能被 50 金成本门挡住」两层叠加**（本国库 D6 `gold=3~20 ≪ 50`）；**非"世界上没石头"**（存档 `stone_pile 780｜ore_vein 1188` 在册）。

### 3.2 (C) 焦点霸占（⑨建城墙）—— 全链 `file:line` ＋ **同因判定**

**（C-1）机理链（四处实读，环环相接）**

| # | 落点 | 事实 |
|---|---|---|
| ① | [UtilityScorer.cs:227-232](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L227-L232) | `WallGap need = clamp01((needA − have)/needA)`（`needA=8`）——**纯状态函数，无失败记忆／无退避** ⇒ `have` 停滞即 need 恒 ≥0.875（`have=0` ⇒ **恒 1.000**） |
| ② | [UtilityActionConfig.cs:32](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs#L32) | ⑨ `axis=Defense, axisWeight=1`；`stageWeight[Expand/Military]=1.0`；`minStage=Expand` |
| ③ | [UtilityScorer.cs:133-142](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L133-L142) | `score = need × (axisWeight×personality[Defense]) × stageW` ⇒ **三因子可同时顶格**（**实测 k2 D6 `⑨建城墙 score=1.000`＝满分**） |
| ④ | [UtilityScorer.cs:467-475](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L467-L475) | ⑨ `Feasible` **只判"资源够 ＋ 存量未达目标"**（注释原文「**选址/前置归执行门面**」）⇒ 实测 `feasible=True` ⇒ **评分面每天放行** |
| ⑤ | [KingdomBrain.cs:1188-1195](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1188-L1195) | 执行面 `PlacementScorer.TryPick` 无落位 ⇒ 打 `建造焦点选址失败：wall … 明日再试`；**`Bump(ok:false)` 但不降权** ⇒ **次日同分再来（死循环）** |

⇒ **「评分面恒可做（`feasible=True`）＋ 执行面恒做不成（无落位）＋ need 无退避」三者叠加 ＝ 永久霸占。**

**（C-2）三样本对照（同 seed 家族外推，`file:line` 同一日志点）**

| 样本 | seed／窗口 | 选址失败**总数** | **wall 占比** | wall 分布 |
|---|---|---|---|---|
| HH.189 七考（`142147`） | 73621／120 日 | **181** | **181/181 ＝ 100%** | k1 77、k4 104 |
| HH.220 正向短局 `p1_fix1`（`231630`） | 64513／65 日 | **106** | **103/106 ＝ 97%** | k1 39、k2 49、k3 3、k4 15 |
| HH.193 诊断局（`160131`） | 48903／30 日 | 17 | wall 5（**farm 6 最多**） | farm 6／wall 5／quarry 4／Warehouse 2 |

**（C-3）判定（禁预设，逐项取证）**

- **是否同因 ⇒ 同因**：三样本打的是**同一条日志**（`KingdomBrain.cs:1193`）、**同一 `buildingId=wall`**、**同一代码路径**（`WallGap` → `Feasible` → `TryPick`），且失败**不降权**（`Bump` 只计数）⇒ 与 HH.189「wall 选址失败 181 次」＝**同链同因**，差异仅在**窗口长度／种子地形**（181/120日 vs 103/65日，且同日密度相当）。
- **是否结构性 ⇒ 分层结论**：
  - **设计意图层＝非结构性**：该失败**不保证必发**——HH.193（seed 48903）wall 仅 5 次且**非最多**（farm 6）⇒ 属**几何/地形特定**，非"必然恒失败"。
  - **实现层＝结构性缺陷**：一旦地形不利，**无任何机制能退出该循环**（need 无退避 ＋ `Feasible` 与执行面口径不一致 ⇒ 评分面不识"做不成"）⇒ **不是数值调参能治**，根治须在「失败反馈/退避」或「`Feasible` 与选址面口径一致」层动手。**该修法超本批授权，报裁不擅动**。
- **对 P1 终验收的影响**：与 HH.189 §放大因子一致——⑨ 霸占吃掉焦点预算，**是"0 AI 至军事期"的放大器而非唯一因**（病根仍在 3.1 资源地基层）。

**（C-4）HH.220 §4.6 三探针落实状态（如实）**

| 探针 | 状态 |
|---|---|
| ①③⑨ 逐行动读数 | ✅ **本批完成**（§1.3，已随负探针跑出 D2~D6 逐日读数） |
| ② `Gather` 事件计数（派发／注册／入账） | 🟡 **部分**：**注册面**已落（`chSrc=<Gather>/<Byproduct>`）；**派发／入账面未做**（需新增事件级读数，未授权） |
| ③ k1 领土内 `stone_pile/ore_vein` 计数 | ⛔ **未做**（需世界实体 × 领土交集读数；本批未授权） |

---

## 四、残余缺口与列报（**1 项残余 ＋ 3 项列报**，均不改文档）

**🔴 残余①（须知悉）：`OnlyKingdom` 过滤分支尚未端到端跑过。**
本批两次负探针**均由 J1（全批级）在 D5/D6 先命中** ⇒ `if (cfg.OnlyKingdom && k.id != cfg.Kingdom) continue;` 这条**新增分支未被实跑覆盖**（仅静态成立＋编译过）。⇒ **建议**：下一次**正向批／七考重验批**即为该分支的首次端到端验证点（预期 J2 只认 k3、**不**在 ~D10 切掉全批）；若届时仍出现"非 k3 国触发 J2"，即为该分支失效的判定信号。

**列报①（文档措辞）**：`test-harness-first §8.6` 现文「容器 `CheckJudges` **常**遍历所有 AI 国、任一命中即停全批」为**修前描述**；本批已实现"逐判据声明作用域"。**勾稽一致（判据表两列已按 §8.6 要求逐行填）**，仅措辞请策划端酌改——skill 属策划端单写者，**执行端未动**。

**列报②**：⑦招战士四国 D6 实读 `feasible=False` 而 **`feasibleReachable=True`**（`cost 0/0/0/0`）⇒ 卡的是**建筑前置**（非资源），与 HH.202 §三「⑦ Feasible 镜像建筑前置」一致，**本批无退化**。

**列报③**：⑨ 的**定量极值**已到手——k2 D6 `⑨建城墙 score=1.000`（`need=1.000 × axis=1.000 × stageW=1.00`）＝**该行动理论满分**，当日本国 `census top=BuildWall` 对应。

---

## 五、红线自检

| 项 | 状态 |
|---|---|
| 设计文档 | **零改动**（仅 skill 侧为策划端已落，执行端未动） |
| 任务队列 | **只读** |
| 业务码／AI.Core | 业务码**零改动**（仅 `Assets/Editor/Smoke/*` 两文件）／**AI.Core 零触碰** |
| 共享文档 | `_编号登记.md` **仅改水位线行**（增量）；取号＝独立单行 commit `1afdf65` |
| 收工 | **已退 Play**（`isPlaying=False`，实测）；SO 已复原（无 diff） |
| commit | 本批文件**同串提交**、只提本串文件、**未 push** |

---

## 六、下一步（待验收后）

1. **验收成立** ⇒ 本批收口（HH.220 续跑闭环）。
2. **接 HH.221「AI 资源权力对等批」**（D665 签发；本批已按 §一 打完 `chSrc=` 通道读数，未越界实施）。
3. **七考重验批起跑前**须复原容器常量：`CIRCUIT_BREAK_DAY 90→120`／`MILITARY_STOP_COUNT 1→2`（**现为 `p1_fix1b` 短局档**）；并**首次端到端验证 §四-残余①**。

---
*执行端 2026-09-12（HH.223，取号 `1afdf65`）。证据：`Logs/P1/p1_log_20260912_093512.log`（负探针·最终代码）／`hh80_run_status.log`／`p1_log_20260912_092821.log`（旧口径对照）／`p1_log_20260911_231630.log`（正向·census/选址失败统计）／`p1_log_20260911_142147.log`（HH.189 对照）／`p1_log_20260911_160131.log`（HH.193 对照）。*

---

## 七、策划裁决区（主策划端已回写）

> **主策划端裁决（D670，2026-09-12）＝验收成立**（HH.220 续跑闭环）。**判据三直读（已过）**——见下"实测"栏；**报告逐项属实，含自曝残余，正面样本**。

### 7.1 判据三直读（实测 · 非采信转述）

| 报告声称 | 实测锚点 | 结论 |
|---|---|---|
| D669 裁决① 作用域落地 | `Valley_HH80_Run.cs:40` `struct JudgeCfg{Kind,OnlyKingdom,Kingdom}`／`:43` `JUDGE_FOCUS_KINGDOM=3`／`:45-50` `JUDGES`＝Deadlock 全批／StoneCold **指定国 k3**／NoIncome 全批／`:149` `if (cfg.OnlyKingdom && k.id != cfg.Kingdom) continue;` | ✅ **与裁决逐字一致** |
| `TryJudgeOne` 单判据查询 | `Valley_DiagMilitary.cs:53` 签名＋`:70` 判据③改用 `CountGatherSources()<=0`（detail 明写"副产源不计"） | ✅ |
| `chSrc` 口径分离 | `:83 CountGatherSources()=CountSourcesByKeyword("Gather")`／`:86 CountByproductSources()=…("Byproduct")`／`:203 chSrc=<Gather>/<Byproduct>`／`:215-217` ③⑨⑩ DumpAction | ✅ |
| (C) 机理链 | `UtilityScorer.cs:227-232` `WallGap`＝`Clamp01((needA−have)/needA)`（**纯状态函数，无失败记忆/退避**）／`:467-475` ⑨ `Feasible` 只判资源＋存量（注释"选址/前置归执行门面"）／`KingdomBrain.cs:1188-1195` `PlacementScorer.TryPick` 无落位 ⇒ 日志＋**`Bump(ok:false)`＋return（无降权/无冷却）** | ✅ **全链属实** |
| 业务码零改动／AI.Core 零触碰 | `git show --stat de42027` ＝ 仅 `Assets/Editor/Smoke/*` **两文件** ＋ 3 份文档 | ✅ **实测确认** |
| SO 复原／退 Play（L-32） | `git diff` `Assets/_Game/Resources/Config` ＝ **空**；`Assets/_Game` status ＝ **空**；报告 `isPlaying=False` | ✅ |

### 7.2 逐项裁决

1. **作用域规则落地＝准** —— 与 D669 裁决① **逐字一致**；**这是「策划端抓坑 → 执行端落地 → 留待正向批首验」完整闭环**。
2. **`chSrc` 口径分离＝准，且必要** —— 报告 §2.3 当场实证：旧 `chSrc=4` **全为矿洞副产搬运源** ⇒ 会把"采集通道未落地"**灌水成"有源"**。⇒ **嘉奖**（下见 7.5 教训，此笔归策划侧）。
3. **负探针实证＝采信** —— **J1 命中 @D6／124 秒 vs 白跑 51 日 ≈21 分钟**；且**熔断（90 日）与 `MILITARY_STOP_COUNT` 均未达 ⇒ 停跑确由判据触发**（排除兜底混淆）。⇒ **`L-34`／`test-harness-first §八` 首个端到端实证**（机制 1+3 有效）。
4. **(B) 补强＝采信，并采其新增第二层（本批最有价值增量）** —— `③ BuildCapacity feasible=False` 且 `cost gold=50`（D6 国库 `gold=3~20 ≪ 50`）＋ `DiagTriage Stone=BuildCapacity`（**缺口被看见、③ 被指到**）＋ `feasibleReachable=True` ⇒ **(B) 根因＝两层叠加**：①**AI 无采集世界一次性资源的通道**（`chSrc=0/4`；D665 已裁）②**③建产能被 50 金成本门挡住**。
   ⇒ **对 HH.221 的加硬条款**：**主修 A 只解①**；**②须作为 HH.221 §0 gap 表新增行**（`③ feasible=True` 可达性 — gold 门槛）——**否则补了采集通道，③ 仍不可行**。⇒ 记 **DZ-135**（挂 HH.221）。
5. **(C) 判定＝「同因 ＋ 分层」全部采信**（三样本 181/181／103/106／17 中 wall5；同一日志点 `KingdomBrain.cs:1193`／同一 `buildingId=wall`／同一代码路径；**不降权实测**）⇒ **同因成立**；**分层成立**（HH.193 seed 48903 wall 仅 5 次且非最多 ⇒ 几何特定，非"必发"）。
6. **残余①（`OnlyKingdom` 分支未端到端跑过）＝准（知悉）** ⇒ **加硬条件**：**下一正向批／七考重验批必带判定信号**——"J2 仅认 k3、**未**在 ~D10 误停全批"；**若出现非 k3 国触发 J2 ⇒ 判该分支失效、立即停手报裁**。
7. **列报①（skill §8.6 措辞滞后）＝准，策划端改**（skill 属策划端单写者）→ 已改「**默认**遍历所有 AI 国（实现须逐判据声明作用域）」＋**三端同步**。
8. **列报②③＝采信知悉**（⑦ `feasible=False` 因**建筑前置**非资源＝无退化；⑨ `score=1.000`＝该行动理论满分，与 census `top=BuildWall` 对应）。
9. **§2.5「同 seed 双跑非逐值复现」＝采信，并立须知悉项** —— 属 HH.202 已登记同族（D2 起逐行漂移）；**判据命中日两次一致（D6）**⇒ 结论不受影响。**须知悉**：**"命中日一致"≠"逐值可复现"** ⇒ **该判据用于止损足够，不得用作逐值判定**。
10. **🔴 加硬条件（执行端已自报，升为硬门）**：**下一长局批起跑前，容器常量必复原 `CIRCUIT_BREAK_DAY 90→120`／`MILITARY_STOP_COUNT 1→2`**（现为 `p1_fix1b` 短局档）；**未复原不得开长局**（否则 120 日判定线被 90 截断＝D585/D589「截断混淆」同族风险）。⇒ 记 **DZ-136**。

### 7.3 (C) 修法裁决（**超执行端授权部分，本次由策划端裁**）

**破框（beyond-options）**：执行端两方向——(a)「`Feasible` 与选址面口径一致」(b)「失败反馈/退避」——**共同前提「只修 ⑨ wall 这一条」不成立**：**"选中但执行失败却不退避"是通用病灶**（三样本里 `farm 6／quarry 4／Warehouse 2` 同样在失败）⇒ **只修 wall ＝ 打补丁**。
⇒ **自造 A+ ＝ 通用「执行失败退避」机制**（立 **HH.224**）：
- **落点**：`ExecuteBuildFocus` 两处失败分支（`KingdomBrain.cs:1194` 选址无落位／`:1208` `TryBuild=false`）**写 per-kingdom × per-action 失败计数/冷却**；评分面 `score` 乘退避因子（或失败达 N 次该行动临时降权）。
- **退避必须自愈**（防从"死循环"变"永久弃建"）：**状态变化即复位**（目标变化／资源跨门槛／新候选格出现）。
- **否 (a)**：**保留"选址归执行门面"分层**（`Feasible` 不做几何探测）——把选址搬进评分面＝违 D525 §3.7 分层且重。
- **先诊断后改**：退避会改 ⑨/③/⑦（皆建造类）的焦点分布 ⇒ 须先出对 2_22/2_23 已验收批的**回归影响**（同 seed 行为必变，须同 seed 对照判"改善/退化"）。
- **红线**：`UtilityScorer`/`KingdomBrain` **先核 AI.Core 镜像**（有则走 sim-sync 规定序）；**禁参数调参找补**（D563③）；改动＝**行为级（F 级）双门禁**。
- **DZ-107（焦点霸占 BuildWall）** 由"观察项"**升级为「实现层结构性缺陷」**。

### 7.4 验收三问（钩子2）

1. **为什么发生**：① `chSrc` 为**混合口径**（采集源＋副产源同计）⇒ 漏报"通道未落地"；② `OnlyKingdom` 新分支**结构上必未被覆盖**（探针先命中全批级 J1）。
2. **单次失误 or 流程漏洞**：① ＝ **策划侧流程漏洞**——D669 我只裁了"判据作用域"，**未裁"判据的支撑读数口径须与判据语义单一匹配"**；② ＝ **非失误**（结构性必然，且执行端**自曝并给出验证点**＝正面样本）。
3. **缺条目 or 有条没查**：① **触发二次红线**——**同族已发生一次**（**D639**：2_23 通道B 信号由 `RateSum`〔设计产出率〕改 `Flow.In(r)/pop`〔实际日产出〕＝**口径指错量**）；本次 `chSrc` 混口径＝**口径指错对象** ⇒ **同类第二次** ⇒ 按 §四 立 **`L-35`**（新条）＋**防范升为签发硬性检查项**；② 属"新增分支须有端到端验证点"，已在报告内自识别 ⇒ **不新立**。

### 7.5 教训沉淀

- **新立 `L-35`｜读数/判据口径须与其语义单一匹配（禁混合口径）** —— 现象：支撑读数把两类语义混为一类 ⇒ 漏报/误判；根因：口径选择未经"它代表什么、排除了什么"自证；**流程漏洞＝签发侧未要求"读数口径声明（含排除项）"**；**防范（升硬性检查项）＝签发任何"判据/读数/验收句"时，必须写明该读数的口径来源与排除项**；**案例＝D639（首次）→ D670（二次实证）**。
- `L-34` 维持 D669 两条补维度（作用域／同级），本条**独立成条**（域＝读数诚实性，非判据机件）。

### 7.6 裁决后果（分流）

- **本批收口**：HH.220 续跑闭环 ⇒ 队列「军事期门槛映射层治本批」行**销号**。
- **接续**：**HH.221（AI 资源权力对等批）可接单**（§0 增 DZ-135 一行）。
- **新立**：**HH.224「⑨/③ 焦点霸占治本批（执行失败退避）」**（7.3 全文条款）。
- **硬门**：长局批起跑前**必**复原容器常量（DZ-136）＋**必**验 `OnlyKingdom` 分支（残余①）。
- **知悉**：判据"命中日一致"≠"逐值可复现"。
