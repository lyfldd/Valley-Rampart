# HH.348 第 5 源 `SiegeWorkshopBuilding` 受限施工 · 事务端独立核实记录

> **核实对象**：`多Agent交接/执行端/HH.348_SiegeWorkshopBuilding受限施工_交付报告.md`（commit `4c8be9a3`）
> **依据**：`HH.341_M5-D乙加_小源⑤_SiegeWorkshopBuilding施工窗口任务书.md`（`D925` / `HH.347`）、`HH.346` 预检报告、`L-96`／`L-97`／`L-100`
> **开工/收工 HEAD**：`4bca76c3`（施工轮零 commit；本端核实时 HEAD = `4c8be9a3`）
> **核实性质**：**事务端独立核实**。⛔ 不判绿、⛔ 不代表源级通过。
> **核实时间**：2026-09-30
> **本记录版本**：**首版**

---

## 一、落库核实（报告本体）

| 项 | 实测 | 判定 |
|---|---|---|
| commit | `4c8be9a3bb7192cbe5f4e8bac77280cfa5eef196`，`1 file changed, 127 insertions(+)` | ✅ 与回执一致 |
| blob 一致性 | `git rev-parse 4c8be9a3:<报告>` ＝ `git hash-object <工作区>` ＝ `9eca7080b0c1be0e9fc3e8805cd9899bce1067c8` | ✅ **字节一致** |
| 磁盘指纹 | `sha256=8e81708e…e1fc8`、14,649 B、127 行 | ✅ 与回执自验一致 |
| 命名/号位 | 文件名与标题号位 ＝ `HH.348` | ✅ 无空位、无错号 |
| 水位线 | `_编号登记.md` 水位线 `HH.199 → **HH.348**`，未被本批触碰 | ✅ |
| ahead | `main...origin/main [ahead 137]` | ✅ **未 push** |

## 二、改动面等集核实（`L-100`）

| 面 | 实测 | 判定 |
|---|---|---|
| 本批 commit 集合 | `git diff --name-only 4bca76c3 4c8be9a3` ⇒ `_编号登记.md`（本端取号 `25d9628c`）＋ `执行端/HH.348_…交付报告.md` | ✅ 无夹带 |
| `_Game` 源码（保留工作区） | `TaskScheduler.cs` **+17/−0**；`SiegeWorkshopBuilding.cs` **+272/−2** | ✅ 与回执逐位一致 |
| `_Game` 未跟踪（开工前既有） | `_Game/Art/Ground/New Palette.prefab`(＋meta)、`ground_tropical.asset`(＋meta) | ✅ 回执 §六 已列入并声明归属（本端复核：非本轮引入） |
| 行数 | 本源 548 ／ 调度器 1694 | ✅ 与报告一致 |

**改动面集合（含未跟踪）与报告清单等集** ⇒ ✅ 通过。

## 三、禁止面零越界核实

- `Building.cs`／`BuildingFactory.cs`／`StorageComponent.cs`／`BuildingSaveData.cs`／`ProductionSystem.cs`／`TreasureVault.cs`／`SiegeProductionSystem.cs` ⇒ `git status` **全空**（零触碰）；
- 协议六文件（`ITaskScheduler.cs`／`TaskCardProtocol.cs`／`TaskLifecycleRules.cs`／`TaskBindingTypes.cs`／`TaskBindingManager.cs`／`TaskProtocolRuntime.cs`／`TaskProtocolIssuer.cs`）⇒ `TaskScheduling/` 目录下**仅 `TaskScheduler.cs` 有改动**，六文件零触碰；
- `GameScene.unity` ⇒ 仍为**开工前既有脏点**（`O-14` 关联），⛔ 非本批引入、⛔ 未还原；
- `SiegeWorkshop.asset`（数据行）⇒ 零触碰。

⇒ **禁止面 12/12 零越界** ✅

## 四、源码实质核实

### 4.1 `TaskScheduler.cs`（+17/−0，仅 5 处接缝）

| 接缝 | 落位 | 形态 |
|---|---|---|
| D⁵ 源失效 | `:289-291` | `if (snapshot[i] is SiegeWorkshopBuilding swInvalid) swInvalid.OnProtocolSourceInvalidated(ProtocolTickNow);` |
| A⁵ 派发 | `:453-455` | `if (task.source is SiegeWorkshopBuilding swDispatch) swDispatch.OnProtocolDispatched(id, ProtocolTickNow);` |
| B⁵ 到达 | `:582-585` | `if (task.source is SiegeWorkshopBuilding swArrive) swArrive.OnProtocolArrived(id, ProtocolTickNow);` |
| E⁵ 完成 | `:764-767` | `if (task != null && task.source is SiegeWorkshopBuilding swDone) swDone.OnProtocolTaskCompleted(npcId, ProtocolTickNow);` |
| C⁵ 放弃 | `:822-824` | `if (task != null && task.source is SiegeWorkshopBuilding swAbandon) swAbandon.OnProtocolAbandoned(npcId, (int)reason, ProtocolTickNow);` |

- 形态与 `D923` 第 4 源（`bs` 分支）**逐型同构**，均插在既有 `bsXxx` 分支之后；
- ⛔ `taskTimeout`／`SourceKingdom`／`Complete` 判据／调度器内核**零触碰**（diff 仅上述 5 处 hunk）。

### 4.2 `SiegeWorkshopBuilding.cs`（+272/−2）

| 项 | 落位 | 判定 |
|---|---|---|
| 五类回调 | `:386 OnProtocolDispatched`／`:406 Arrived`／`:416 Abandoned`／`:452 SourceInvalidated`／`:462 TaskCompleted` | ✅ 五类齐 |
| 建卡出口 | `:350/:359` 经 `TaskProtocolIssuer`（⛔ 无手填 `new TaskCard()`） | ✅ 沿 `D879` ⑥ 先例 |
| 销毁兜底 | `SealAllOnDestroy()` 定义 `:485`，`OnDestroy:239` **首行调用**（先封卡再 `Unregister`） | ✅ |
| ⭐ `HasWorkerOnDuty` **双查统一** | `:254-263`：`sched.HasWorkerAssigned(this) || (_building != null && sched.HasWorkerAssigned(_building))` | ✅ 与 `BlacksmithBuilding.cs:70-71` 同型，注释明标 `D925` 统一口径 |
| 轮产逻辑未被改写 | `Tick` 在岗门仍 `:119`；`_accumulator` 累积 `:121`／取整 `:122`／轮序 `:126-127`／实产扣减 `:131` —— **行号与预检读数一致** | ✅ 节奏未被接缝改写 |
| `[SerializeField]` ×4 | `:318-321`（`_lastWorkerId`／`_totalCards`／`_doneCards`／`_abortedCards`），注释声明「验收期可见化、行为零参与」 | ⚠️ **超出 D923 最小形态 ⇒ 待裁**（见 §六 Q2） |

## 五、基线复算

| 指标 | 预检 | 报告称 | **本端实测** | 判定 |
|---|---|---|---|---|
| 本源 `SchedRef` | 6 | 11（+5） | **11** | ✅ |
| 本源 `NewKT` | 1 | 1（持平） | **1** | ✅ |
| `TaskScheduler.cs` 内 `SiegeWorkshop` | 0 | 10（5 接缝×2 行） | **10** | ✅ |
| 生产域 `SchedRef` | 81 | 86 | **86** | ✅ |
| 生产域 `NewKT` | 15 | 15 | **15** | ✅ |

**五接缝全源家数对拍**（`A/B/C/D` 各 6 家、`E` 5 家）⇒ 与预检 §五 SEAM 基线 **+1 吻合** ✅

## 六、未核实项与待裁（诚实声明）

### 6.1 本端未独立重跑 Unity 读数（**读数级复现标「未取得」**）

本端无 Unity／MCP 进局通道 ⇒ 报告 §四 的运行时读数（正门建局 ×3、三子仓就绪 ×3、`Produce` 外部入口、派发/放弃日志、世界重建面）**未经本端独立复现**，采信执行端原始日志与自述。⚠️ 该限制沿用 `HH.344` 先例口径，⛔ 不因此改变判据归属。

### 6.2 `Complete ≠ 产弹` 证据分级

- **结构证据（本端已独立核实）**：`TaskScheduler.cs:882-885` `Production` 支唯一动作 ＝ `comp.GetComponent<ProducerComponent>()`；本源 `SiegeWorkshop.asset` 数据行仅 `comp.siege_workshop`；`BuildingComponents.cs:200` 该键仅 `Add<SiegeWorkshopBuilding>` ⇒ `prod == null` ⇒ 分支空操作。⇒ **成立** ✅
- **运行时佐证（未取得）**：工坊组件清单运行时直读 —— 同 §6.1。

### 6.3 待裁三项（转主策划）

| 编号 | 事项 | 本端说明 |
|---|---|---|
| **Q1** | 探针通道（下轮落一次性本源探针 vs 事务端代跑） | 属施工面/窗口授权 ⇒ **主策划裁定**；⛔ 本端不代裁。⚠️ 本端已如实登记"无 Unity 通道"（§6.1），故"事务端代跑"一项**本端目前不具备条件**。 |
| **Q2** | `[SerializeField]` ×4 去留 | 与 `D923` 最小形态先例存在偏差 ⇒ **主策划裁定**。 |
| **Q3（新）** | **源码提交时点** | 沿 `D921`／`D923` 先例＝**生产码保留工作区，至判绿时点由事务端具名提交**。⚠️ `HH.347` 任务书**未授权**本端提交源码 ⇒ 本端**已按先例不提交**，请主策划确认或另行授权。 |

### 6.4 未取得项（执行端已如实登记，本端复核认可）

在岗门内 `Tick` 产弹、E/C/D 接缝归因快照、跨档 Save/Load 往返、双查在岗的 `Transport` 互斥行为 ⇒ **均未取得**。⚠️ 这些正是能力句 2/3/4/6 的运行时面 ⇒ **阻塞判绿**，⛔ 不阻塞核实受理（本记录即为受理）。另：R2（子仓存档缺口）**未闭合前不得判第 5 源绿**（`D925` 硬门）。

## 七、核实结论

| 维度 | 判定 |
|---|---|
| 落库 | ✅ 通过（blob 字节一致、号位正确、未 push） |
| 改动面等集 | ✅ 通过（含未跟踪项声明，与报告清单等集） |
| 禁止面 | ✅ **12/12 零越界** |
| 接缝形态 | ✅ 五类同型、内核零动 |
| `V15` 双查统一 | ✅ 已落实（`:254-263`） |
| 基线复算 | ✅ 五指标全数吻合 |
| 未取得项 | ⚠️ 如实登记（4 组），**阻塞判绿** |
| 待裁 | ⚠️ 3 项（Q1／Q2／Q3）转主策划 |

⇒ **施工合规 · 核实受理通过**；本源状态仍为 **「施工完成 · 待进局/跨档/`L-95`/基线/事务核实闭环」**，⛔ **不判绿**。

---

*核实记录完（首版）。本端写入面：本文件。⛔ 未提交源码、未改账本水位线、未 push。*

---

## 裁定加注（2026-10-01 · 事务端按裁定补注）

> 本件原文保留不改；下列为后续裁定结果的加注。

- **裁定结果**：第 5 源 `SiegeWorkshopBuilding` **本轮不判绿** —— 结构、落库、边界、五接缝、`V15`、五项基线均支持；`R2` 缺口承认存在；4 组运行时读数未取得。
- **唯一下一动作**：下一授权轮立 **`R2` 三子仓存档兼容闭环 ＋ 一次性本源探针联合工单**（目标号 `HH.349`／`D926`；⚠️ 本轮不登记、不预留）。
- **本件状态**：施工合规 · 核实受理 ⇒ 队列项 **⏸阻塞**（`R2` 硬门 ＋ 4 组运行时读数未取得）。
- 台账详见 `测试基线台账.md` **§二百四十三**。
