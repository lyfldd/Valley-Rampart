# HH.252 HH.251「A+ 施工批·㉗接管降级通道」开工回执（执行端 → 主策划端）

> 取号：HH.252（账本水位线 HH.250/251 → 252；D640 #10 先登记后落盘）
> 作业依据：**HH.251 施工任务书**（D699 签发·Gate=`G1-1`）
> 恢复四连已完成：河谷防线_开发计划书.md 顶部工作日志 → `_交接索引.md` → `_任务队列.md` → HH.251 任务书（逐字读完）
> 前置必读已完成：HH.249 §九 验收区 + HH.249 探针结论（`Wood=2／Stone=0`）· `DZ-148` 台账 · D699（0.6 §二百二十八）· D690（0.6 §二百一十九）
> 状态：🔴 **停手待策划确认**（发现**决定性结构阻塞**——任务书指定修复点不可达，详见 §二）

---

## 〇、一句话结论

**任务书 §二 指定的修复点（`ExecuteWorldGatherFocus` 让位分支）在石场景下不可达**：㉗ 在通道A（`DecideTriage != NoOp`）时 `NeedScore` 恒返 **0f**（`UtilityScorer.cs:329`）⇒ `ScoreTop` 的 `need<=0.0001f ⇒ continue`（`UtilityScorer.cs:143`）⇒ **㉗ 永远不会成为焦点** ⇒ `ExecuteFocus` 不路由到 `ExecuteWorldGatherFocus` ⇒ **在让位分支内加 `Feasible(③)==false` 判定＝写入不可达死代码**，验收线 1（石场景 ㉗ 接管派发首达）**结构性不可能达成**。

**根因**：任务书把修复点定在**执行侧（让位分支）**，但真正的门在**评分侧（`NeedScore` 的通道B 限制）**——而任务书 §三 又明令「❌ 不修 `UtilityScorer`/`census top=` 评分面」。**二者互斥**：不改评分面 ⇒ 修复点不可达；要修复 ⇒ 必须突破 §三 红线。需策划端裁决（见 §四 报裁）。

---

## 一、sim-sync 核查结论（红线项·先出结论）

**结论＝本批零 `AI.Core` 义务；但若按报裁口径扩到评分面，须复查 `DecideTriage` 同源副本。**

| 核查项 | 结论 |
|---|---|
| 改动对象位置 | `Assets/_Game/Systems/AI/KingdomBrain/`（`KingdomBrain.cs`／`UtilityScorer.cs`）——**属 `_Game`，非 `AI.Core`** |
| `AI.Core` 镜像副本 | 全库 `AI.Core/**` **无** `KingdomBrain`/`UtilityScorer`/`DecideTriage`/`FocusController`/`TriageDecision`/`NeedKind`/`GatherShortageGap` 任一符号（`LS` 目录实读＋`Grep` 零命中） |
| 训练仓镜像 | `ai决策大脑强化训练/harness/`：`Grep` `DecideTriage|ExecuteWorldGatherFocus|GatherShortageGap|class UtilityScorer|class FocusController` **零命中**；仅 `harness/KingdomBrain/SituationSnapshot.cs:104` **注释**提及 `UtilityScorer`（非代码副本）；`harness/Economy/SimEconomy.cs` 有 `QuarryDaily` 等**经济域同名近似**（`采石场 DailyRate`），但**无三通道分诊/`DecideTriage`/㉗ 采集**语义 |
| 判定 | 标的=Unity 侧经济决策出口 → **镜像无对应逻辑 ⇒ sim 零同步义务**（HH.236 §四 同口径；15_账本留痕） |
| ⚠️ 附加 | 若策划端采纳报裁 b/c（需改 `NeedScore`/`Feasible`），`sim-sync` 义务**仍为零**（`NeedScore`/`Feasible` 亦无 harness 镜像），但须在交付报告显式复述本结论 |

---

## 二、🔴 决定性结构阻塞（本批停手根因·附 file:line 证据链）

### 2.1 调用链实读（四环全闭合）

| 环 | 位置 | 事实 |
|---|---|---|
| ① 唯一调用点 | `KingdomBrain.cs:630-632` | `ExecuteFocus` 的 `case UtilityAction.GatherWorldResource:` → `ExecuteWorldGatherFocus`。**全库唯一调用点**（`Grep ExecuteWorldGatherFocus` 命中 2 处＝定义 `:1334` ＋ 调用 `:631`） |
| ② 焦点来源 | `FocusController.cs:138,158,176` | `kingdom.focus` 唯一写点＝`SetFocus(kingdom, (int)top, day)`，`top`＝`UtilityScorer.ScoreTop(...)` 返回值（或三级常设底线 ⑤/⑥/⑭，**不含 ㉗**） |
| ③ 评分门 | `UtilityScorer.cs:142-144` | `float need = NeedScore(k, def); if (need <= 0.0001f) { census.noNeed++; continue; }`——**need==0 ⇒ 该候选直接不入池**（越不过 `best`） |
| ④ need 门 | `UtilityScorer.cs:317-335`（`case NeedKind.GatherShortageGap`） | **`:329` `if (KingdomBrain.DecideTriage(ecoG, dcfgG, erG) != TriageDecision.NoOp) return 0f;`**——即**通道A/C ⇒ ㉗ 的 need 恒 0** |

㉗ 的 `NeedKind`＝`GatherShortageGap`（`UtilityActionConfig.asset:510-515`：`id: 27`／`need: 23`；`NeedKind` 枚举 `UtilityScorer.cs:90` `GatherShortageGap`＝第 23 位）。

### 2.2 石场景推演（与 D690 全列报一致）

石场景＝`CountProductionOf(eco, Stone)==0` 且 `FindTriageDef(Stone)=="quarry"` 非空 ⇒ `DecideTriage` **恒返 `BuildCapacity`**（`KingdomBrain.cs:1383-1387`）。

⇒ 环④：㉗ `NeedScore` **恒 0f** ⇒ 环③：㉗ **恒不入池** ⇒ 环②：`top≠27` ⇒ 环①：`ExecuteWorldGatherFocus` **恒不被调用**。

**⇒ 任务书 §二.1 的落点（`:1348-1352` 让位分支）在石场景下 100% 不可达。** 在此处加 `Feasible(③)==false` 判定，只会在"㉗ 已成为焦点"的通道B 场景被求值——而通道B 下 `DecideTriage==NoOp`，`Feasible(③)` 与 ㉗ 交接**无因果**（③ 走 `BuildCapacity` 已不成立）⇒ **新分支对通道B 无副作用，对通道A 不可达 ⇒ 净变化为零（死代码）**。

### 2.3 实盘日志佐证（既有权威证据·非推断）

`Logs/P1/p1_log_20260912_184557.log:283,286`（D2 k1，四国同型）：

```
[DiagMilitary] D2 k1 ③建产能        needKind=CapacityGap      need=1.000 feasible=False ... score=0.462  buildingId=quarry
[DiagMilitary] D2 k1 ㉗采集世界资源点 needKind=GatherShortageGap need=0.000 feasible=False ... score=0.000
```

- ㉗ `need=0.000` **实证**（非推断）——正是 §2.1 环④ 的产物。
- **③ 亦 `feasible=False`**（金 50 不足）⇒ ③ 也被环③ 的 `Feasible` 门滤出（`UtilityScorer.cs:144`）⇒ 石场景下 **③ 与 ㉗ 双双不可选**（焦点落到其他行动）。

> 附带发现：**`Feasible(③)==false` 这一条件本身在石场景恒真**（③ 金/石/木/粮镜像不可能满足 `quarry` 造价）——D690 论证方向正确；但**判据放错层**（应放评分侧 need 门，而非执行侧让位分支）。

### 2.4 第二处连带缺口（供报裁参考·非本批必破）

- **可见性缺口**：`UtilityScorer.Feasible(...)` 为 **`private static`**（`UtilityScorer.cs:483`）⇒ `KingdomBrain` 侧**无法直接调用** `Feasible(③)`。若按任务书字面在 `ExecuteWorldGatherFocus` 内判 `Feasible(③)==false`，须先放宽可见性（`private`→`internal`，同 `ResolveTriageResource`／`DecideTriage` 于 HH.221/D685 的先例）。
- **判据落点替代**：修复的**正确层**＝`NeedScore` 的 `GatherShortageGap` 分支（`:317-335`）：把 `:329` 的「仅通道B」放宽为「通道B **或**（通道A 且 `Feasible(③)==false`）」，即 D690 原文「通道A 且 `Feasible(③)==false` ⇒ ㉗ 接管」。

---

## 三、验收线可达性复核（按任务书 §五 逐条预判）

| # | 线 | 当前预判 | 依据 |
|---|---|---|---|
| 1 | 石场景可达（㉗ 接管派发首达） | 🔴 **结构性不可达** | §2.1-2.2（㉗ need 恒 0 ⇒ 永不成为焦点） |
| 2 | 原行为回归（通道B 可采／③可行让位） | ⚪ 无法进入实证（取决于线 1 修复层） | 同上 |
| 3 | 零改动面 | ⚠️ 与修复层冲突 | 真修复须动 `UtilityScorer`（§三 红线禁止） |
| 4 | 编译 0 error | ✅ 预期可过（若采纳死代码方案） | — |
| 5 | 教训兑现（新旧输入集／跑次标注／四列齐） | ✅ 可写（但为死代码背书＝失真） | — |

**⇒ 按任务书原方案施工 ⇒ 线 1 必 ❌，且线 5 会产出「为不可达路径背书」的失真报告**——属 `L-30`（判定线硬条件结构性可达性未在执行前证）＋`L-21`（出口存在≠出口可达）**同型复现**。

---

## 四、报裁（请策划端定夺·4 项）

> 依 `beyond-options`：给定方案（改让位分支）经实读**不成立**，故不自造替代即属失职——以下含自造候选。

| 项 | 内容 | 建议 |
|---|---|---|
| **R1** | **确认本批停手**（不动 `_Game` 任何代码，避免产出死代码 + 失真报告） | ✅ 建议立即停手 |
| **R2** | **修复层改判**：修复点自「执行侧让位分支」上移到**评分侧 `NeedScore` 的 `GatherShortageGap` 分支**（`:329` 放宽为「通道B **或** 通道A∧`Feasible(③)==false`」），并放宽 `Feasible` 可见性（`private`→`internal`） | ✅ 建议（**唯一使验收线 1 可达的落点**） |
| **R3** | **§三 红线改写授权**：若采纳 R2，须授权改写任务书 §三「❌ 不修 `UtilityScorer`」为「✅ 修 `NeedScore.GatherShortageGap` 单分支（不动 `census top=` 结构／不动 `Feasible` 语义／不动 `DecideTriage` 纯函数）」 | ✅ 建议（限定最小面） |
| **R4** | **重签任务书 or 补发勘正单**：因改动作业面（文件/行/红线），建议**补发 HH.251-A 勘正单**（或重签任务书），本回执转为该单的开工前置 | ⚪ 请示 |

**自造候选（供选）**：
- **口径 A（最小面·推荐）**：仅改 `NeedScore:317-335` 一行判据 + `Feasible` 可见性；`ExecuteWorldGatherFocus` 内**保留** `DecideTriage != NoOp` 让位（因评分侧已保证 ㉗ 只在「通道B 或 通道A∧③不可行」入池，执行侧到达即应派发）。⇒ 改动 2 文件 2 处，红线面最小。
- **口径 B（任务书原文＋补判据）**：执行侧让位分支**也**加 `Feasible(③)==false` 直通（双保险），但**必须同时**做口径 A，否则 B 单独＝死代码。⇒ 改动多一处，语义冗余（不推荐，违反最小改动）。

---

## 五、红线自检（停手态）

| 红线 | 状态 |
|---|---|
| 先出 sim-sync 核查结论 | ✅ §一（零 `AI.Core` 义务） |
| 正门 `EnterTestRun`＋退 Play（L-32） | ⚪ 未进局（停手，无跑局） |
| 新旧输入集一致性核验（D695） | ⚪ 无可核（未改动） |
| 跑次标注（D694②） | ⚪ 无跑次 |
| **零业务代码改动** | ✅ `Assets/_Game/**` **未做任何修改**（本批仅探测/实读） |
| 不修 A+/金门本体 | ✅ 未触 |
| 写-改-commit 同串、只提本批、不 push | ✅ 见 §六 |

---

## 六、产物与 commit

| 文件 | 状态 |
|---|---|
| `多Agent交接/执行端/HH.252_A+㉗接管降级通道_开工回执.md` | 新建（本回执） |
| `多Agent交接/_编号登记.md` | 取号 HH.252（水位线行＋在途区行） |
| `多Agent交接/_交接索引.md` | HH.251 行状态更新 + HH.252 行 |
| `多Agent交接/_任务队列.md` | HH.251 行状态更新 |

- **commit**：`<见回报>`（只提上述文件，**不 push**）。
- **未做**：未改 `Assets/_Game/**` 任何文件；未进 Play；未跑局。

---

> **版本**：2026-09-13（执行端）。关联：HH.251 任务书（D699）／HH.249 §九／`DZ-148`／D690（§二百一十九）／`L-21`/`L-30`/`L-35`。
> **红线自检**：sim-sync 结论已出 ✅｜零业务代码改动 ✅｜不修 A+/金门本体 ✅｜未进局未跑局 ✅｜不 push ✅。
