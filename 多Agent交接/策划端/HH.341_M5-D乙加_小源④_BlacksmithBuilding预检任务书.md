# HH.341_M5-D乙加：小源④ `BlacksmithBuilding` 独立只读预检任务书

> **主策划签发｜2026-09-29**
> 性质：**独立只读预检窗口**。本文件是第 4 源的正式派工正文，不是施工许可、不是源级判绿。
> HH 交付占位号：**由事务端按 `vr-id-ledger` 先登记后落盘锁定**；本文件不自取 HH 号、不取新 D 号。

## 〇、裁定与开窗范围

1. **Q1＝准**：准开第 4 源 `BlacksmithBuilding` 独立只读预检窗口。第 3 源已在 `D921` 判绿，逐源串行门已开启；本裁定只解锁预检，不解锁施工。
2. 当前源固定为 `Valley Rampart/Assets/_Game/Systems/Kingdom/BlacksmithBuilding.cs`，顺序第 4；执行端不得改序、并源或顺手进入第 5 源 `SiegeWorkshopBuilding`。
3. 本轮写动作只有两类：本源专属**交付报告**与**证据文件**。生产代码、协议代码、资产、场景、Prefab、`AI.Core` 写入数必须为 **0**。
4. 预检结论只能是「四项取得/未取得」与「任务卡源/非任务卡源/停手待裁」；不得在本轮写施工结论、判绿号或源码提交结论。

## 一、派工入场（四项强制）

- **你的角色：执行端**；事务端只做独立核实、占位 HH 锁定和落账，不改本任务书的判定内容。
- **角色卡：**`.trae/执行端入场卡.md`
- **必读 skill（按路径直读）：**
  - `.codely-cli/skills/vr-role-brief/SKILL.md`（§一、§二末“派工指派”）
  - `.codely-cli/skills/vr-tool-topology/SKILL.md`
  - `.codely-cli/skills/vr-triage-flow/SKILL.md`
  - `.codely-cli/skills/agent-handoff/SKILL.md`（§六并发写纪律）
  - `.codely-cli/skills/vr-id-ledger/SKILL.md`（§7.0、§七轮换闸门）
  - `.codely-cli/skills/vr-planner-leadership/SKILL.md`（§4.1判据三直读）
  - `.codely-cli/skills/execute-checklist/SKILL.md`
  - `.codely-cli/skills/sim-sync/SKILL.md`
- **必看文档：**
  - 本任务书全文；`多Agent交接/策划端/HH.341_M5-D乙加_小源阶段任务书.md`（§2.1、§2.2、§三、§五、§六、§七、§八、§十、§十一）
  - `河谷防线开发计划书具体内容/测试基线台账.md`（§二百三十四、§二百三十五；只作边界与先例，不重述已裁内容）
  - `多Agent交接/执行端/HH.341_MineByproduct预检窗口_交付报告.md`（5 行集、五类接缝、预检四项和四步闸门先例）
  - `多Agent交接/执行端/HH.341_小源首源_ConstructionSiteStore预检_交付报告.md`（任务卡适用性先例）
  - `多Agent交接/_任务队列.md`、`多Agent交接/_当前快照.md`
  - `多Agent交接/策划端/_策划教训库.md`（`L-97`、`L-98`、`L-100`）
  - 代码/数据直读：`BlacksmithBuilding.cs`、`TaskScheduler.cs`、`ProductionSystem.cs`、`BuildingComponents.cs`、`BuildingFactory.cs`、`BuildingDef.cs`、`Blacksmith.asset`、`BlacksmithDef.asset`

## 二、允许面与禁止面

### 2.1 允许面（仅只读）

- 读取当前源、调度器既有生产/状态推进分支、`ProductionSystem` 的 `ITickable` 驱动、组件注册/挂载、建筑数据行和本源 SO 字段。
- 读取既有报告、台账、队列、快照和教训库，形成 `file:line` 证据。
- 只在 `Valley Rampart/Logs/` 写本源独立前缀：`hh341_small_blacksmith_*`；不得覆盖第 1～3 源或 D1 证据。
- 报告须记录开工 `HEAD`、工作树状态、允许面清单、`GameScene.unity` 的 hash（只登记，不回滚）。

### 2.2 禁止面（命中即停手报裁）

- ⛔ 不写任何生产代码；`BlacksmithBuilding.cs` 与所有接缝文件的源码 diff 必须为 0。
- ⛔ 不写 `ITaskScheduler.cs`、`ChestManager.cs`、协议六文件（`TaskCardProtocol.cs`、`TaskLifecycleRules.cs`、`TaskBindingTypes.cs`、`TaskBindingManager.cs`、`TaskProtocolRuntime.cs`、`TaskProtocolIssuer.cs`）；`TaskScheduler.cs` 本轮只读。
- ⛔ 不写 `Scenes/**`、Prefab、`Assets/Resources/Buildings/**/*.asset`、`Assets/Resources/Config/**/*.asset` 或其他资产。
- ⛔ 不触碰 `AI.Core`、训练仓、两巨兽、后续源 `SiegeWorkshopBuilding`、`HH.342_DZ-新存档生成冲突修复批_任务书_废止草稿_D877不补发.md`；`O-14` 继续挂账。
- ⛔ 不执行 `git add -A`、整目录覆盖、`git checkout --` 或整文件 `restore`；回滚类动作若未来需要，先查挂账池并只用 `git show <rev>:<path>` 取副本。
- ⛔ 本轮不提交源码、不 push、不取 D 号、不申请第 5 源。

## 三、预检四项（每项必须有代码原文与 `file:line`）

### 3.1 生产触发入口、真实调用链、源状态变化

至少直读并串成一条可复核链：

1. `BuildingFactory.AttachComponents` → `BuildingComponentRegistry` 的 `comp.blacksmith` 映射 → `BlacksmithBuilding.Init`；同时核对 `Blacksmith.asset:15-18`、`:40-43`、`:51-65`、`:76-79` 与 `BlacksmithDef.asset:13-15` 的实际字段。
2. `ProductionSystem.TickAll` 遍历 `ITickable` → `BlacksmithBuilding.Tick`；核对 `_rate`、等级缩放、累积器、`StorageComponent.Transform(Ore→Metal)`、容量/原料/在岗门。
3. `LazyRegister` → `TaskScheduler.Register` → 调度器每 tick 收集 `TryAdvertiseTask`；核对 `IsValid`、`SourcePos`、`KingdomTaskType.Production`、`destType=None`、重复派工门。
4. 调度器 `Dispatch`、`Assigned→MovingToSource→Working`、工作时长、`Complete`/`Abandon`/源失效清理和工人复位；明确区分「到岗」「`ProductionSystem→Tick()` 真实产出」「调度器 `Complete`」三个阶段。
5. 建筑/组件销毁与读档相关状态是否存在；没有就明确写“代码面未见该源自持存档方法”，不得用相邻源代替。

### 3.2 `KingdomTask` / `TaskScheduler` 引用与生产分支

逐条列出本源内的 `TaskScheduler.Instance|HasInstance`、`new KingdomTask(`、`ITaskSource` 和协议标识引用；每一条都要指向调度器对应的收集、去重、派发、位移、工作、完成或放弃分支。注释行、文档行必须单列排除，不得把静态命中写成运行时调用。

### 3.3 是否具备工人任务四能力

分别给出“可接取、可移动、可完成、可放弃”的结论或未取得状态，并给出各自 `file:line` 链。生产源的真实产出只能由源自身 `Tick()` 与存储变更证明；到岗 `Working`、`Complete`、`ExecuteCompletion` 不得互相替代。若只看到 `ProducerComponent` 的完成兜底而看不到 `BlacksmithBuilding` 专属调用，登记为观察事实，不能自行补语义。

### 3.4 卡片适用性判定（先判再谈施工）

必须在报告中二选一并说明证据：

- **任务卡源**：代码面同时证明真实任务对象、调度器真实收集/派发、工人可接取/移动/完成/放弃、源有明确有效性和收口边界；施工轮才可按 §5.1 要求真实 Play `TaskCard`。
- **非任务卡源**：不得造卡。停手提交真实生产事件、必要接缝、成功/失败/释放/幂等、读档重建、旧链影响等六类替代证据，待主策划另裁；不得因卡数为零直接判绿。

## 四、五行集三数（`L-97` 固定格式）

证据文件必须用 `## S <行集名>` 分段；每行集都写：行集来源（路径/谓词/筛选条件）、口径标签（池内/全量/筛选集）、分母 `N`、字段完整取值域（含零计数项）、**原始 → 排除 → 有效**、非采样行分类和复现命令。禁止“约/≈”，禁止混用代码行数与匹配次数。

| 行集 | 固定来源与要求 |
|---|---|
| `SRC_SELF` | `BlacksmithBuilding.cs` 内 `TaskScheduler\.(Instance\|HasInstance)`、`new KingdomTask\(` 及 `ITaskSource`/协议标识相关行；代码行与注释行分列，列出全部 `file:line`。 |
| `TS_REF` | `TaskScheduler.cs` 内出现 `BlacksmithBuilding` 或本源专用分支标识的全量行；注释/历史批注排除必须逐行列出，零命中也须写 `0→0→0`。 |
| `SEAM` | `TaskScheduler.cs` 的协议接缝调用/类型判定行（`OnProtocol`、`TaskCard`、`ProtocolRuntime`、`hasDeadline` 等）；必须另列本源在 D/A/B/E/C 五类的命中数。 |
| `DEFDATA` | `Assets/Resources/Buildings/*.asset` 中 `comp.blacksmith` 与本源 `Blacksmith` 数据行；注明筛选集、资产文件、零计数项及字段值。 |
| `ALLREF` | 全 `Valley Rampart/Assets/**` 对 `BlacksmithBuilding` 的引用；生产域、Editor/Smoke、生成物/二进制、文档注释必须分列，不得把全 Assets 对照数当生产调用面。 |

事务端核实时须逐行复算上述五集；执行端不得只给最终有效数。

## 五、接缝现状盘点（只登记，不补码）

按下列固定顺序报告 **D／A／B／E／C**：

- **D 源失效**：源失效清理、未派任务放弃；
- **A 派发**：`Dispatch` 建立协议配对/预定的本源类型分支；
- **B 到达**：`MovingToSource→Working` 的本源到达回调；
- **E 完成**：`Complete` 后的本源收口回调；
- **C 放弃**：`Abandon` 后的本源回待派/封口回调。

每类给“命中数＋所有 `file:line` 位置”；无命中写 **0**，不得用“零接缝”四字替代五类明细。预检不得新增任何接缝；是否需要接缝属于后续施工裁定。

## 六、`sim-sync` 义务声明

本预检窗口的裁定是：**不触 `AI.Core`，sim-sync 义务为 0**。允许只读扫描以证明 `BlacksmithBuilding` 在 `AI.Core` 零命中；不得写 Unity `AI.Core`、`harness/Core`、champion、factor registry 或训练仓。若执行中发现必须改动 `AI.Core` 才能继续，立即停手，列出同源同步面与触发文件，交回主策划另裁。

## 七、观察项登记

第 3 源已使用 `V1`～`V3`；本源观察项从 **`V4`** 起编号。每项必须有：事实描述、`file:line`、观测范围/筛选条件、状态标签（已取得／未观察到／未取得／不确定）、是否阻塞本预检、后续归属。建议优先检查并按实证决定是否登记：

- `V4`：`TaskScheduler.ExecuteCompletion` 的 `ProducerComponent` 兜底与 `BlacksmithBuilding.Tick` 实际产出面的分离；
- `V5`：`HasWorkerOnDuty` 同时查询本组件与宿主建筑，而广告源为本组件；
- `V6`：注册发生在 `Tick()` 内的 `LazyRegister`，与首次广告/首次在岗之间的时序；
- `V7+`：数据行的 Metal 仓路径、Ore→Metal 比例、等级缩放、容量和原料不足时的合法无产出条件。

以上只是观察登记候选，不是缺陷结论，也不改变本轮零代码边界。

## 八、基线复算与命令

以开工 `HEAD` 与工作树分别复算，输出命令、原始输出和行/文件口径：

```powershell
# 生产域固定为仓库根下的 Valley Rampart/Assets/_Game
git grep -nE 'TaskScheduler\.(Instance|HasInstance)' HEAD -- 'Valley Rampart/Assets/_Game'
git grep -nE 'TaskScheduler\.(Instance|HasInstance)' -- 'Valley Rampart/Assets/_Game'
git grep -nE 'new KingdomTask\(' HEAD -- 'Valley Rampart/Assets/_Game'
git grep -nE 'new KingdomTask\(' -- 'Valley Rampart/Assets/_Game'
git grep -nE 'class[[:space:]]+[A-Za-z0-9_]+[^\n{]*ITaskSource|struct[[:space:]]+[A-Za-z0-9_]+[^\n{]*ITaskSource' HEAD -- 'Valley Rampart/Assets/_Game'
git grep -nE 'class[[:space:]]+[A-Za-z0-9_]+[^\n{]*ITaskSource|struct[[:space:]]+[A-Za-z0-9_]+[^\n{]*ITaskSource' -- 'Valley Rampart/Assets/_Game'
git diff --name-only -- 'Valley Rampart/Assets/_Game'
git diff --numstat HEAD -- 'Valley Rampart/Assets/_Game'
git grep -n 'BlacksmithBuilding' -- 'Valley Rampart/Assets/_Game/Systems/AI/Core' ; if ($LASTEXITCODE -eq 1) { 'AI.Core BlacksmithBuilding = 0' }
```

报告必须给出 `SchedRef`、`NewKT`、`ITaskSrcDecl` 三行集的 HEAD/WT 数值、逐文件差异归因，以及本源 `SRC_SELF` 的命中行。`git diff` 若显示既有 `GameScene.unity` 等脏点，只登记为外部改动，不得纳入本源改动面。

## 九、四步落盘核验闸门

报告与证据每次写入都必须完成并回执：

1. 写入前后记录 `mtime`，确认发生变化；
2. 从磁盘重新读取全文，不复用内存缓冲；
3. 对磁盘内容做 `sha256` 或逐字比对，并登记长度/哈希；
4. 事务端提交报告后，用 `git show <rev>:<path>`（或 `git cat-file blob`）与磁盘 `git hash-object <path>` 做长度＋哈希＋字节一致性核对。

缺任一步不得交付、不得入账、不得进入施工申请。报告提交必须单文件，不得混入源码、资产、场景、既有证据或 O-14。

## 十、交付与停手

- 交付物：开工回执、四项预检、五行集三数、D/A/B/E/C 接缝表、sim-sync 声明、`V4+` 观察项、基线复算、零改动证明、四步闸门回执。
- 结果状态固定为：**`BlacksmithBuilding` 未开始·预检待裁**；本报告不得写“施工完成”“源级通过/判绿”。
- 任一缺口、无法给出三数/行集来源、无法直读设计全文＋代码 `file:line`＋档位字段、需要触碰禁止面、或发现代码/资产 diff 非零，立即停手报裁。
- 预检未获后续施工裁定前，⛔ 不申请第 5 源 `SiegeWorkshopBuilding`；⛔ 不取新 D 号。

## 十一、事务端后续动作（Q4）

本轮主策划已完成任务书签发；事务端在收到本文件后执行以下落地动作：

1. 按 `vr-id-ledger` 读取现行表，锁定本任务的 HH 占位号并“先登记后落盘”；不得把 `HH.342` 废止草稿重新启用，不得自取 D 号。
2. 在台账新增“第 4 源 `BlacksmithBuilding` 独立只读预检窗口”节，并同步 `_编号登记.md`、`_任务队列.md`、`_交接索引.md`、`_当前快照.md`；内容只记录本裁定、范围、状态＝预检待裁和占位号，不改判定、不提前判绿。
3. 以本文件原文生成并发给执行端的正式预检提示词；不得要求执行端“转述式”复述裁定。
4. 预检交付后独立核实报告/证据的 6 项交付标识、3 项统计闭合和四步闸门；核实通过后再回呈主策划，另轮裁定是否施工。

**签发结论：Q1 准开；Q3 沿用“占位号由事务端锁定”；Q4 需要事务端落账＋正式提示词＋独立核实，除此不新增本端代码/资产动作。**
