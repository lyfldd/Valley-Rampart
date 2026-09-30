# HH.341_M5-D乙加：小源⑤ `SiegeWorkshopBuilding` 独立只读预检任务书

> **主策划签发｜2026-09-30｜D924**  
> 性质：**独立只读预检窗口**。本文件是五小源最后一源的正式派工正文，不是施工许可、不是源级判绿。  
> HH 占位号：**HH.345**（已登记；完成报告号由事务端届时按 `vr-id-ledger` 实时水位线取号）。

## 〇、裁定与开窗范围

1. **Q1＝准开**：`D923` 已确认第 4 源 `BlacksmithBuilding` 源级判绿，逐源串行门满足；准开第 5 源 `SiegeWorkshopBuilding` 独立只读预检窗口。
2. 本轮只解锁预检，不解锁施工、源码提交、资产改动或第 5 源判绿。第 5 源判绿后才算五小源阶段收口。
3. 当前源固定为 `Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs`。不得并源、换序或顺手重开前四源。
4. 预检结果只能是：四项取得/未取得、任务卡源/非任务卡源/停手待裁。报告不得写“施工完成”“源级通过/判绿”。

## 一、派工入场

- **执行角色**：执行端；事务端只做独立核实、正式提示词转发和落账，不改变本裁定。
- **必读 skill**：
  - `.codely-cli/skills/vr-role-brief/SKILL.md`（§一、§二末“派工指派”）
  - `.codely-cli/skills/vr-triage-flow/SKILL.md`（§6.1、§七）
  - `.codely-cli/skills/agent-handoff/SKILL.md`（§三、§六）
  - `.codely-cli/skills/vr-id-ledger/SKILL.md`
  - `.codely-cli/skills/vr-planner-leadership/SKILL.md`（§4.1、§4.2）
  - `.codely-cli/skills/execute-checklist/SKILL.md`
  - `.codely-cli/skills/sim-sync/SKILL.md`
- **必看文档**：
  - `多Agent交接/策划端/HH.341_M5-D乙加_小源阶段任务书.md`（§2.2、§五、§六、§七、§八、§十一）
  - `多Agent交接/策划端/D922_HH.343_BlacksmithBuilding施工窗口准开裁决与执行工单.md`
  - `多Agent交接/事务端/HH.344_BlacksmithBuilding施工窗口_核实记录.md`
  - `河谷防线开发计划书具体内容/测试基线台账.md`（§二百三十八～§二百四十）
  - `多Agent交接/策划端/HH.341_M5-D乙加_小源④_BlacksmithBuilding预检任务书.md`
  - `多Agent交接/_任务队列.md`、`多Agent交接/_当前快照.md`、`多Agent交接/策划端/_策划教训库.md`

## 二、只读范围与禁止面

### 2.1 只读核查面

必须直读本源及其实际依赖：

- `SiegeWorkshopBuilding.cs`、`TaskScheduler.cs`、`ProductionSystem.cs`、`BuildingFactory.cs`、`BuildingComponentRegistry`、`BuildingDef.cs`、`BuildingIds.cs`；
- `SiegeProductionSystem.cs`、`StorageComponent.cs`、`TreasureVault.cs`、`RulerController` 及实际读档入口；
- `Assets/Resources/Buildings/SiegeWorkshop.asset` 与 `Assets/Resources/Config/SiegeProductionConfig.asset`；
- 现有协议、报告、原始日志、生产域基线和 `GameScene.unity` hash（只登记，不回滚）。

本预检唯一写入面是本源独立报告与 `Valley Rampart/Logs/hh341_small_siege_*` 证据前缀；源码、资产和场景 diff 必须为零。

### 2.2 禁止面

- 不写 `SiegeWorkshopBuilding.cs`、`TaskScheduler.cs` 或任何生产/协议源码；
- 不写 `ITaskScheduler.cs`、`ChestManager.cs`、`Building.cs`、`ProductionSystem.cs`、`StorageComponent.cs`、`TreasureVault.cs`、`SiegeProductionSystem.cs`；
- 不写 `TaskCardProtocol.cs`、`TaskLifecycleRules.cs`、`TaskBindingTypes.cs`、`TaskBindingManager.cs`、`TaskProtocolRuntime.cs`、`TaskProtocolIssuer.cs`；
- 不写场景、Prefab、`Assets/Resources/**/*.asset` 旧键、`AI.Core`、训练仓、四本账本或其他四个小源；
- 不执行 `git add -A`、整目录覆盖、回滚或 push；任何必须扩大面的问题立即停手报裁。

`D923` 已定“循环常驻源接缝统一挂组件侧、`Building.cs` 保持禁止面”；本源预检直接沿用该口径，本体 `Transport` 不在本窗补接缝。

## 三、预检四项（必须逐条给出原文与 `file:line`）

### 3.1 生产入口、调用链与状态变化

至少串成以下可复核链：

1. `Init:38-49` 读取配置、创建三子仓并消费 `SiegeProductionSystem` 的旧档迁移缓存；`CreateSubStores:53-70` 逐仓设置资源路径、容量与注册。
2. `ProductionSystem.cs` 的 `ITickable` 遍历 → `SiegeWorkshopBuilding.Tick:108-132`；核对 `_cycleOrder`、速率、累积器、在岗门、原料门和各子仓容量门。
3. `LazyRegister:273-278` → `TaskScheduler.Register:147-151` → 每 tick 的 `TryAdvertiseTask:260-270`；核对 `IsValid:253`、`SourcePos:255`、`KingdomTaskType.Production`、`destType=None` 和重复派工门。
4. 调度器收集/派发、到达、`Working`、完成和放弃必须分列；不得把到岗、真实产弹、调度器 `Complete` 或 `ExecuteCompletion` 互相替代。
5. `OnDestroy:237-246` 注销源和子仓；`RestoreLegacyAmmo:218-226` 与 `SiegeProductionSystem` 的旧档 `SaveState/LoadState` 必须说明迁移、清零、幂等与容量 clamp 边界。若未见本源自持存档方法，明确写“代码面未见”，不得用相邻源代替。

### 3.2 `KingdomTask` / `TaskScheduler` 引用与生产分支

逐条列出本源 `TaskScheduler.Instance|HasInstance`、`new KingdomTask(`、`ITaskSource` 与协议标识；每条都要指向调度器的注册、收集、去重、派发、位移、工作、完成或放弃分支。当前已知源专用接缝为零，必须把 `TaskScheduler.cs` 的 `SiegeWorkshop` 0 命中写成 `0→0→0`，不能把 grep 零命中直接写成“链路不存在”。

### 3.3 工人四能力

分别给出“可接取、可移动、可完成、可放弃”的**能力句**结论或「未取得」，并给出各自 `file:line` 链。真实产弹只能由本源 `Tick/Produce` 与三子仓变更证明；`Working`、`Complete`、`ExecuteCompletion` 不能互相替代。

### 3.4 卡片适用性

必须二选一并给证据：

- **任务卡源**：真实任务对象、调度器收集/派发、工人可接取/移动/完成/放弃、有效性和收口边界均可达；后续施工轮才可要求真实 Play `TaskCard`。
- **非任务卡源**：禁止造卡；改交真实生产事件、成功/失败/释放/幂等、读档重建、旧链影响六类替代证据，待主策划另裁。

## 四、五行集三数（`L-97` 固定格式）

证据文件必须用 `## S <行集名>` 分段，写明来源、谓词、筛选、口径标签、分母 `N`、完整取值域（含零计数项）、原始→排除→有效、非采样行分类和复现命令；不得写“约/≈”，不得混用代码行数和匹配次数。

| 行集 | 本源要求 |
|---|---|
| `SRC_SELF` | `SiegeWorkshopBuilding.cs` 内 `TaskScheduler.(Instance\|HasInstance)`、`new KingdomTask(`、`ITaskSource`/协议标识；代码行与注释行分列，当前实读值为 6/1/1 的基线候选。 |
| `TS_REF` | `TaskScheduler.cs` 内 `SiegeWorkshop` 或本源专用分支全量行；当前零命中必须写 `0→0→0`，并单列通用调度分支。 |
| `SEAM` | `TaskScheduler.cs` 的 D/A/B/E/C 协议接缝/类型判定行；本源五类专用接缝各自给数，零值不省略。 |
| `DEFDATA` | `SiegeWorkshop.asset` 的 `comp.siege_workshop`、`isSiegeWorkshop`、`producer` 字段及 `SiegeProductionConfig.asset` 成本；筛选集、字段值和零计数项完整列出。 |
| `ALLREF` | 全 `Valley Rampart/Assets/**` 对 `SiegeWorkshopBuilding` 的引用；生产域、Editor/Smoke、生成物/二进制、文档注释分列，不把全 Assets 对照数当生产调用面。 |

所有统计必须写明实际 pattern 原文并附口径标签；不得以“等价 pattern”替代。引用自身报告、提交 blob 或行数时，必须标注「首版／补记版」。

## 五、接缝现状盘点（只登记，不补码）

按 `D/A/B/E/C` 顺序报告：

- **D 源失效**：源失效清理、未派任务放弃；
- **A 派发**：协议配对与预定；
- **B 到达**：`MovingToSource→Working`；
- **E 完成**：`Complete` 后收口；
- **C 放弃**：`Abandon` 后回待派/封口。

每类给命中数和全部 `file:line`；当前本源专用接缝预期均为 0。预检不新增接缝，是否需要接缝留待另行施工裁定。

## 六、`sim-sync` 义务

本窗口不触 `AI.Core`，`sim-sync` 义务为 **0**。只读扫描可用于证明零命中；若发现必须改 `AI.Core`、训练仓、champion、factor registry 或 `harness/Core` 才能继续，立即停手并回呈影响面。

## 七、观察项登记

第 4 源已使用到 `V13`，本源从 **`V14`** 起编号。每项必须写事实、`file:line`、观测范围/筛选、状态标签（已取得／未观察到／未取得／不确定）、是否阻塞及后续归属：

- `V14`：三子仓分别容量与满仓判断，是否需要三仓分别归零；
- `V15`：`HasWorkerOnDuty:251` 只查组件，与宿主建筑在岗语义的差异；
- `V16`：`_cycleOrder:30-32` 轮产次序与“合法无产出”条件；
- `V17`：`SiegeProductionSystem` 旧档弹药迁移桥、清零和容量 clamp；
- `V18`：经济系统跳过 `isSiegeWorkshop` 的边界与弹药资源公共入口；
- `V19+`：真实生产调用、读档状态、三子仓装卸链的其他可证观察。

以上为观察项，不是缺陷结论；只读预检没有 `L-98` 观察窗，七项观察窗要求标为「不适用」，不得伪造运行时读数。

## 八、基线复算与零改动证明

以开工 `HEAD=b88983c1` 与工作树分别复算：

```powershell
git grep -nE 'TaskScheduler\.(Instance|HasInstance)' HEAD -- 'Valley Rampart/Assets/_Game'
git grep -nE 'TaskScheduler\.(Instance|HasInstance)' -- 'Valley Rampart/Assets/_Game'
git grep -nE 'new KingdomTask\(' HEAD -- 'Valley Rampart/Assets/_Game'
git grep -nE 'new KingdomTask\(' -- 'Valley Rampart/Assets/_Game'
git grep -nE 'class[[:space:]]+[A-Za-z0-9_]+[^\n{]*ITaskSource|struct[[:space:]]+[A-Za-z0-9_]+[^\n{]*ITaskSource' HEAD -- 'Valley Rampart/Assets/_Game'
git grep -nE 'class[[:space:]]+[A-Za-z0-9_]+[^\n{]*ITaskSource|struct[[:space:]]+[A-Za-z0-9_]+[^\n{]*ITaskSource' -- 'Valley Rampart/Assets/_Game'
git grep -n 'SiegeWorkshop' -- 'Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs' ; if ($LASTEXITCODE -eq 1) { 'TaskScheduler SiegeWorkshop = 0' }
git diff --name-only -- 'Valley Rampart/Assets/_Game'
git diff --numstat HEAD -- 'Valley Rampart/Assets/_Game'
```

报告必须给出 `SchedRef`、`NewKT`、`ITaskSrcDecl` HEAD/WT 三行集、本源 `SRC_SELF`、`TS_REF`、`SEAM` 的逐文件差异归因，以及本源文件长度/sha256。既有 `GameScene.unity` 等脏点只登记，不纳入本源改动面。

## 九、四步落盘核验闸门

报告与证据必须回执：①写入前后 mtime；②从磁盘重读全文；③sha256 或逐字比对并登记长度；④事务端用 `git show <rev>:<path>` 或 `git cat-file blob` 与 `git hash-object` 做字节一致性核对。缺一不得交付、入账或进入施工申请。

## 十、停手条件与下一关

- 任一预检项缺少原文锚点、五行集三数、四步闸门、边界声明或发现代码/资产 diff 非零，立即停手报裁。
- 预检报告只能落「`SiegeWorkshopBuilding` 未开始·预检待裁」；不能申请第 6 源，也不能自行施工。
- 预检完成并经事务端独立核实后，主策划另行裁定是否准开第 5 源施工；只有第 5 源判绿，五小源阶段才可收口。

## 十一、事务端后续动作

1. 按 `vr-id-ledger` 核对并维持 `HH.345` 占位登记；完成报告号不得预留。
2. 依据本文件正文向执行端发出单一正式只读预检提示词；不得要求执行端转述式复述。
3. 预检交付后独立核实 4 项预检、5 行集三数、D/A/B/E/C、`sim-sync`、`V14+`、基线与四步闸门；未核实不得进入施工裁定。
4. 本轮不改代码、资产、场景、协议、账本，不 push。

**签发结论：D924＝准开第 5 源 `SiegeWorkshopBuilding` 独立只读预检；HH.345 已登记；施工与判绿另裁。**
