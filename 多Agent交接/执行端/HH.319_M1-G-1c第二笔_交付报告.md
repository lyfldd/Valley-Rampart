# `HH.319` · `M1-G-1c`（收口 · 第二笔）· 交付报告（⭐ **部分完成 · 停手待续**）

- **日期**：2026-09-22 ｜ **端**：执行端（TraeCode）｜ **依据**：`D828`（A 补正 ＋ B 件D ＋ C 件E ＋ D 报告）
- **基线对齐** ✅：`git log --oneline -3` ＝ `e6248cf4`（策划落账）← `e120471f`（报告）← `92390c35`（代码）｜`git status --short` ⇒ **无本端之外的代码改动**（脏项仅既有 `GameScene.unity` ＋ untracked 探针 `.meta`／美术素材）
- **状态**：✅ **A 补正 4 项（A1~A4）＋ A3（`U15Probe` 改判在役）已落 · 编译 0 error**（commit `655b33b4`）｜ ⛔ **件 B（探针）／件 C（跑局＋回归）未落** ⇒ ⭐ **判据 1~8 一条未验 ⇒ 不判绿**
- **红线**：⛔ 零 `AI.Core` 改动 ✅ ｜ ⛔ 未 push ✅ ｜ ⛔ 未动 `GameScene.unity`（`O-14`）✅ ｜ ⛔ 未改 `taskTimeout` 值 · ⛔ 未新增 SO 字段 ✅ ｜ ⛔ 未碰 `NPCBrain`／`BehaviorExecutor`／`UnitFactory`／存档 schema／四档账本／美术／`pixel-forge`／`Packages` ✅

---

## 一 · A 补正 · 改动清单（逐 `文件:行` ＋ 改前/改后）

| 项 | 位置 | 改前 | 改后 |
|---|---|---|---|
| **A1** | `ScheduleCenterStub.cs:15-21`（类头 XML doc 职责列表） | `~~1. 搬运派发（3.3.5）：产能建筑 IsReadyToHarvest → 找空闲工人 → TaskStimulus 注入~~` ＋ `~~2. 防重复：_transporting 标记…~~`（`~~` 在 XML doc **不渲染** ⇒ 旧陈述句**仍作字面存在**） | ⭐ **旧句整行删除**（⛔ 不以 `~~` 保留）；职责列表**重编号**（原第 3 条 ⇒ **第 1 条**）；改为**说明段**：「已退役职责：…随 `U-15` 根除项删除…」＋「…随件 F-1 删除（字段 0 引用）」 |
| **A1 连带** | 同上 `:21` | 「后续扩展：砍树/建造/随军任务**统一走本中心派发**（P1）」 | ⭐ **承诺撤回**（见 A4：本中心自建派发职责已整段删除 ⇒ 派发归 `TaskScheduler`） |
| **A2** | `StorageComponent.cs:273` | 「旧列 `ScheduleCenterStub:100/:118` **施**已随」 | 「…**已**随」（错字删除） |
| **A3** | `StorageComponent.cs:343-344` | 「…链 A 落点（见 `StorageComponent` 墓碑注）」 | 「…（⭐ 墓碑注位置：**`StorageComponent.cs:351`** · 即原 `HarvestCarry()` 删除处）」⇒ **补 file:line**（`L-64`） |
| **A4** | `ScheduleCenterStub.cs:49-55` | `private TaskPriorityConfig _priorityConfig;` ＋ `Awake()` 读取 | ⭐ **一并删除**（＋说明依据三条）；⚠️ `using System.Collections.Generic` **保留**（`_crewAssignments` 仍需） |
| **A4** | 同上 `:66-68`（`Update` 内注释） | 「3.5 §8.3 优先级派发…届时**统一走 `DispatchByPriority`** 派发，本中心只按优先级排序…」 | ⭐ **承诺撤回**：「⛔ 本类**不再**承诺统一派发；优先级取值与派发**统一归 `TaskScheduler`**（同 SO `Config/TaskPriorityConfig` ＋ 同回退 `TaskPriority.B`）」 |
| **A4** | 同上 `:74-79` | `private TaskPriority GetPriority(KingdomTaskType type) { … }` | ⭐ **已删**（墓碑注：本类 **0 调用** · 全库仅 `TaskScheduler.GetPriority` 在役） |
| **A3（件D-10）** | `Valley_HH319_U15Probe.cs:8-16`（头注判据 4） | 「4 面板布尔（`IsReadyToHarvest`）：纯水井仓 ⇒ false（**灰**）· 混合仓 ⇒ true（可点）」 | ⭐ **改判为在役判据探针**：「布尔**值列照旧**，但「⇒ **面板灰**」**推论已失效**（`BuildingPanel` 判据已改 `TotalCount > 0` ⇒ 纯水井仓**可点**）」 |
| **A3** | 同上 `:92-94` | 「判据 1 · 止血后（**生产落点 `HarvestCarry`**）」＋「（期望 False **⇒ 面板灰**）」 | 「（⭐ **在役落点 ＝ `Harvest()` 手动收取口** · 原直通国库口已随 `#40` 删）」＋「（期望 False · ⚠️ 但**不再推导面板灰**）」 |
| **A3** | 同上 `:97` | log 标签「**`HarvestCarry` 返回**={carried}」（**错标签** —— 值来自 `:95 st.Harvest()`） | 「**`Harvest()` 返回**={carried}」＋ 注明「⭐ 标签勘正」 |

**件F-3 清零核**：`GetPriority`／`_priorityConfig`／`TaskPriorityConfig` 在本文件**剩余命中全部为注释（说明已删）** ⇒ **代码面 0 引用** ✅

## 二 · ⛔ 未落项（如实标注 · 不判绿）

| 件 | 缺口（逐条） |
|---|---|
| **件 D · 探针** | ① `·farm]` **逐帧序列** ② `AbandonReason` **8 值**分桶（含新 `DestFull`）③ `O-17` `DestFull` 条数（正常 ≈0）④ ⭐⭐ **`O-18`「无空闲工人」场景点击**（**本批最关键缺口**）⑤ 判据 9 `Q1` 派出**任务类型**（须 `Transport`）⑥ 判据 10 `Q2`「留背包→**先卸空**→该趟装上」时序 ⑦ 判据 5 `#42` 9 处＋**反向列** ⑧ ⭐ **A1** `G-1` 鉴别力（`TotalCount>0` 纯水井仓可点 **vs** `IsReadyToHarvest=False` **两列差异**）⑨ ⭐ **A2** 拆除中点击（`_demolishing`）⑩ ⭐ **新增** `AddGatherOverflow` 玩家面触发计数（**四行分计** `:771/:775/:1038/:1145`，⛔ `:927` AI 专支不计） |
| **件 E · 跑局＋回归** | ⛔ **全未跑**：判据 1（三侧守恒＝**背包＋各仓＋台账**）／2／3（⭐ **存活型引用＝0 ＋ 在役源文件旧描述注＝0**；⚠️ ⛔ **`IsReadyToHarvest` 不套用此判据** —— 它是 `IHarvestable` **在役接口**）／4（`D811` 止血：井仓水量**逐帧**）／6（**四项回归** `HH315`／`HH316` 全段／`HH317`／`M7` 六轮）／7（存档逐值）／8（⛔ 不含 `U-17`/`U-18`/`U-19`/`DZ-7`/B 批） ⇒ ⚠️ **跑前须先归档**（`L-68`/`L-70`） |
| **报告 §D 要求项** | 判据 1~8 逐项读数 ＋ 归档路径 ⛔ **未取**（无跑局 ⇒ 无从给） |

## 三 · 裁定回执（§D-5 要求）

| 项 | 回执 |
|---|---|
| `GetPriority` | ✅ **已删**（`ScheduleCenterStub` · 连同 `_priorityConfig` ＋ `Awake()`）｜依据三条：① 本类 0 调用 ② ⭐ **承接方在场且等价**（`TaskScheduler.GetPriority` 同 SO ＋ 同回退）③ 「备而未用」依据**已被推翻**（原承诺"本中心统一派发" ⇒ 该职责已随 `U-15` 根除项整段删除 · `L-63`） |
| `AddGatherOverflow` | ✅ **归 `O-2`（B 批）** —— ⛔ 本批**未动**（探针读数亦未加，见 §二） |
| `U15Probe` | ✅ **改判为在役判据探针** ⇒ **从豁免名单移出**（`L-77`）；`:13/:92/:94/:97` 口径已同步（`:101` 已明写"旧" ⇒ 保留） |

## 四 · 豁免清单（更新版）

| 类别 | 逐条 | 理由 |
|---|---|---|
| 历史复现档 | `Valley_HH319_WaterAccount:9/:84/:85` | 刻意复现**旧落点**取证 ＋ 已在头注**声明为历史档** |
| 墓碑注 | `ScheduleCenterStub`（A4 新增 3 处）／`StorageComponent:349` 级／`BehaviorExecutor:171-172`／`TaskScheduler:742` 级 | **说明"已删/为何删"** ＋ 迁移论证 ⇒ ⛔ 非"按旧口径陈述现状" |
| 红线面 | `NPCBrain.cs:728` | ⛔ 红线「不动 `NPCBrain.cs`（让位门 `4de46407`）」 |
| 判据说明串 | `ChainAuditSpec.cs:61` | 判据**说明文本**内命中 · ⛔ 非可执行引用 |
| ~~在役探针~~ | ~~`Valley_HH319_U15Probe`~~ | ⭐ **已从豁免移除**（本批改判**在役判据探针** · `L-77`） |

⚠️ 判据 3 的**可判绿前提**（尚未成立）：**A1 已完成**（✅ 本轮）⇒ 该判据**现行可判**，但需**跑局取读数**方可判绿。
