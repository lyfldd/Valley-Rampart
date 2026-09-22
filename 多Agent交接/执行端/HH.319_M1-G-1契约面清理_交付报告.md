# `HH.319` · `M1-G-1`（A 批 · 契约面清理 ＋ `U-15` 根除）· 交付报告（⭐ **部分完成 · 停手待续**）

- **日期**：2026-09-22 ｜ **端**：执行端（TraeCode）｜ **依据**：`D824`（§一 裁定 ／ §二 施工件 ／ §三 判据 ／ §四 红线）
- **状态**：⚠️ **件 2／3（主体）／4／6 已落码 · 编译 0 error** ｜ ⛔ **件 1（`#42`）／件 5（`Q1` 接线）未落** ｜ ⛔ **探针未扩展 · ⛔ 未跑局 · ⛔ 回归未取** ⇒ ⭐ **判据一条未验 ⇒ 本批 ⛔ 不判绿**（如实停手 · 剩余见 §四）

---

## 一 · 已落码（4 件 · 编译 0 error）

| 件 | 内容 | 关键落点 |
|---|---|---|
| **件 2** · `#41` 删「满则入国库兜底」 | ⭐ **先问后拿**：`UnloadInventory` 改 `bool` 返回；`best == null` / `CanAccept <= 0` ⇒ **留背包**（`return false`）；否则 `n = Min(best.CanAccept(type), inv.carriedAmount)` ⇒ `inv.Take` ⇒ `best.Add` ＋ 余量竞态退回背包 | `TaskScheduler.UnloadInventory`（原 `:870-910`）；⛔ **两处 `AddGatherOverflow` 兜底已删** |
| | ⭐ **`WorkerInventory.Take` 修为尽力档**（去 `CanTake` 前置 ⇒ 同类型尽力取 ≤ 存量） | `WorkerInventory.cs`（原 `:81-87`） |
| | ⭐ **`AbandonReason` 7→8** 新增 **`DestFull`** ＋ `:68` 注释同改（"日志契约取值域（8 值）"） | `TaskScheduler` |
| | ⭐ 到达分支：`UnloadInventory` 失败 ⇒ **`Abandon(DestFull)`**（⛔ 不 `Complete` ⇒ 防"货滞留背包 ＋ 任务假成功"） | `TaskScheduler` 到达段 |
| **件 3** · `#40` 删直通国库旁路 | ⛔ 删 **`StorageComponent.HarvestCarry()`** ＋ 无参 **`GetCarryAmount()`**；⭐ 两处调用面改走 `Harvest()`（`BehaviorExecutor` 落点统一）＋ Editor 探针 3 处同步（`HH314_M1B`／`HH319_U15Probe`／`HH319_WaterAccount`） | `StorageComponent.cs` ／ `BehaviorExecutor.cs:172-177` |
| | ⭐ 删 `ExecuteCompletion` 的 `Transport` 无背包兜底（原 `st.HarvestCarry()`） | `TaskScheduler`（原 `:713-716`） |
| | ⭐ **两道守卫语义迁移论证**（⛔ 不无声消失）→ 见 §三-④ | `StorageComponent` 原位墓碑注 |
| **件 4** · `U-15` 根除（双链合一） | ⛔ 删 **`ScheduleCenterStub.DispatchTransport()` 整段** ＋ `IsTransporting()` ＋ `Update` 里的调用；⭐ 迁移论证（链 A 有阈值覆盖）写为墓碑注 | `ScheduleCenterStub.cs` |
| | `BuildingPanel` 原读 `IsTransporting` ⇒ 改读 **`TaskScheduler.HasWorkerAssigned(_target)`** | `BuildingPanel.cs`（原 `:183`） |
| **件 6** · `Q2` 出口 | ⭐ `LoadInventoryFromSource` **去掉 `is ChestEntity` 限定** ⇒ **对全部源**先卸空旧货 ⇒ 「留背包」有仓可收即**自愈** | `TaskScheduler`（原 `:848`） |

⚠️ **未做**：`TaskScheduler:722` 墓碑注中仍含旧方法名（申辩见 §四-1）；`StorageComponent` 内 `:269/:276/:309/:341` 等描述面**部分未改**。

## 二 · ⛔ 未落码（2 件 · 停手原因）

| 件 | 状态 | 说明 |
|---|---|---|
| **件 1** · `#42` 转职补写入 | ⛔ **未落** | ⭐ 已实读：**无既有转职事件**（`GameEvents.cs` 只有 `UnitSpawned/Died/HpChanged/AttributeChanged/Command/DataLoaded`）⇒ 按裁定**可新增 `UnitOccupationChangedEvent`**；⚠️ 但需逐处标注 9 处语义（含"成长/招募"3 处 ≠ 训练转职）⇒ **未动** |
| **件 5** · `Q1` 接线 | ⛔ **未落** | 形态已裁（`Building` 一次性标记 ＋ 入口读后即清 ＋ `RequestHaulNow(StorageComponent)` 重载 ＋ `BuildingPanel` 文案）⇒ **未动** |
| 件 7 · 探针扩展 | ⛔ **未落** | 判据 1/2/4/5/9/10 的读数面**均未建** |

## 三 · 四项特别读数（裁定要求）

| # | 项 | 状态 |
|---|---|---|
| ① | `Q1` 接线"派出类型" | ⛔ **未接线 ⇒ 无读数** |
| ② | `Q2`"留背包 → 自愈"时序 | ⛔ **未跑局 ⇒ 无读数**（代码已落：`UnloadInventory` 留包 ＋ `LoadInventoryFromSource` 先卸空） |
| ③ | ⭐ **`AbandonReason` 新值域 ＝ 8 值** | ✅ **已落**：`External`／`Dead`／`Unreachable`／`SourceInvalid`／`Timeout`／`BrainLost`／**`DestFull`（新）**／`Unknown` ⇒ ⭐ **探针/summary 若按关键词计数须同步**（`D815 §2.4` 曾钉 7 值契约 ⇒ 本批按 `D824` §一-3 扩为 8） |
| ④ | ⭐ **两道守卫语义迁移论证** | ✅ **已落为墓碑注 ＋ 本节声明**：<br>· 原 `CanRulerAccept`（**标签面**）⇒ 迁至 **`WarehouseRegistry.FindNearestAvailable` 的 `Accepts(type)`**（`UnloadInventory` 内，链 A 新落点）＋ `Harvest()`（玩家手动收取）自留；<br>· 原 `TreasuryCanAccept`（**容量面**）⇒ 迁至 **`CanAccept(type) <= 0`** 同处判据 ＋ `Harvest()` 自留；<br>· ⇒ ⭐ 即「**先问后拿**」（`L-60`）在同一处承载，且本批**同时删掉"满则入国库兜底"** ⇒ ⛔ 不再存在"绕过可收性写死国库"的支路 |

## 四 · ⚠️ 停手说明与须裁项

1. ⚠️ **判据 3 口径与红线冲突（须裁）**：判据 3 要求「`HarvestCarry` 全库命中 **0**（**含注释**）」—— 但：
   - `_Game/Systems/AI/NPCBrain.cs:728` 为**注释命中**，而红线 ⛔ **不动 `NPCBrain.cs`**（让位门保持 `4de46407` 现状）⇒ **两条要求直接冲突**；
   - `Editor/ChainAudit/ChainAuditSpec.cs:61` 为**判据说明字符串**内命中（⛔ 非本批面）；
   - 本批新增的"墓碑注"（说明**已删**）亦必然含该方法名。
   ⇒ ⭐ **本端建议口径**：判据 3 改判「**存活型引用 ＝ 0**」（即**调用点/在役描述**全清），墓碑注与红线面豁免 ⇒ 请裁。
2. ⛔ **未跑局原因**：本批预算用于"可编译收口"（`HarvestCarry` 删除导致 5 处编译断裂 ⇒ 必须先修调用面），探针与跑局**未及**。
3. ⛔ **件 1／件 5 未落** ⇒ `#42`（契约面）与 `Q1`（玩家手点接线）**欠账** ⇒ ⭐ 请续批（形态已裁，落码路径明确）。
4. ⚠️ **`ScheduleCenterStub` 遗留**：`_transporting` 等字段现为**未使用**（仅 warning）⇒ 建议下批一并清理（本批未删，避免与 `DispatchCrew` 面交叉）。
5. ⚠️ **未跑回归**（判据 6）：`HH315`／`HH316`／`HH317`／`M7` **均未取新档** ⇒ ⛔ 本批**无任何判据证据**。

## 五 · 红线自检

✅ 改动面：`StorageComponent`／`TaskScheduler`／`ScheduleCenterStub`／`BuildingPanel`／`WorkerInventory`／`BehaviorExecutor`（落点统一）＋ Editor 探针 3 处 ｜ ⛔ **`AI.Core` 未动**（`Q3` 甲案 · `HarvestTarget` 透传保留标 `Inert`）｜ ⛔ 未动 `NPCBrain.cs` 语义（让位门 `4de46407` 现状）／`KingdomTask.advertiser`／`MonsterAI`／`FormationBrain`／`SelectionController`／`UnitFactory`／存档 schema／`09` 契约本体／`O-14`／四档账本／美术／`GameScene`／`Packages` ｜ ⛔ 未改 `taskTimeout` 值 ｜ ⛔ 未新增 SO 字段 ｜ ⚠️ **行尾**：`UnitController.cs` ＝ `mixed` ⇒ **本批未改该文件**（件 1 未落）⇒ 下批须走 python 二进制。
