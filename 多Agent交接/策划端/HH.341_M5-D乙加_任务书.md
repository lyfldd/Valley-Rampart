# HH.341_M5-D乙加_任务书

> **主策划端 B1 决策（2026-09-26）**
> 本批占位号：HH.341。D 号由事务端按实存顺延，本任务书不自取 D 号。
> 本批目标：为 最高优先级文档/06_任务层.md:154 的②切换建立可回退的分段施工入口；③清理只作为后置门槛，不在首片切换中执行。

## 〇、性质、状态与决策结论

- **性质**：施工（先做 D0 只读进局验证，再做 D1 分段切换）；不执行一次性清场。
- **状态**：待执行。
- **决策一**：M5-D0 必须先行。M5-A/B/C 都是 Edit 模式载体证据，尚未证明活世界完整链路。当前真实生产链有 TaskScheduler.Instance|HasInstance 54 行 / 15 文件、TaskScheduler.cs 1531 行、KingdomTask 75 行 / 15 文件消费面；在没有一条进局闭环前，不得直接改真实源。
- **决策二**：选乙加：
  1. 先用最小兼容接缝把新协议接到现有调度入口，接缝以真实单例消费面为边界；
  2. 以 WorldGatherSource 为首片样板，再按小源到巨兽分段切换；
  3. Building、UnitController 两个巨兽最后处理；
  4. DebugTaskSource（WorkerTask.cs:58）从不注册，属于删除候选，不属于迁移源；
  5. 所有源切换完成且 §九七条判据闭合后，才另取③清理裁决。
- **空壳应对**：ITaskScheduler 仅有 3 行 / 2 文件消费，接口本身不是本轮主耦合点；本批不先做十方法接口扩张。先在 TaskScheduler 单例边界建立最小兼容接缝，接口若确需修改只能服务于该接缝，并须给出新增调用点与旧调用面前后对照。
- **两个巨兽应对**：Building.cs、UnitController.cs 在前段只读盘点；不得与首片或小源同批改动。只有 D0、首片回归、小源阶段门槛全部通过，且每阶段可单独回退，才可立下一阶段任务。

## 一、现状证据与契约锚点

- [实读] 最高优先级文档/06_任务层.md:146-154：旧接口、旧源、旧枚举、旧调度器与三步迁移路径。
- [实读] 最高优先级文档/06_任务层.md:156-164：零引用、目标可判、配对释放、不居中态、读档重验、单管理器、不碰地图数据七条判据。
- [实读] Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs:32-59：TaskScheduler 单例、HasInstance、旧源集合与旧任务表。
- [实读] Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/ITaskScheduler.cs:8-39：10 个契约方法；当前主要消费面机械复算仅 3 行 / 2 文件。
- [实读] Valley Rampart/Assets/_Game/Systems/World/WorldGatherSource.cs:36：首片样板 WorldGatherSource : ITaskSource。
- [实读] Valley Rampart/Assets/_Game/Systems/Unit/WorkerTask.cs:58：未注册的 DebugTaskSource。
- [实读] Valley Rampart/Assets/_Game/Systems/Building/Building.cs:29、Valley Rampart/Assets/_Game/Systems/Unit/UnitController.cs:24：两个巨兽源。
- [实读] 生产域 Valley Rampart/Valley Rampart/Assets/_Game/**：TaskScheduler.Instance|HasInstance 为 54 行 / 15 文件；全 Assets 的 149 / 27 含 Editor/Smoke，不得混列为生产基线。
- [实读] ScheduleCenterStub.cs:56 的第二 _crewAssignments 载体仍在；06 §九-6 的单管理器在迁移完成前不得宣称达成。

## 二、阶段与放行关系

### D0：进局验证门

D0 只读验证，不切换旧源。入口沿用正门 TestHarnessApi.EnterTestRun；可参考 HH.336 的正门链与 HH.314 的 seed=31418、15x 先例。执行端必须：

1. 进入 Play；
2. 在真实生产链内完成至少一条“源广告 → 调度器收集 → 工人接单/配对 → 推进 → 完成或异常回收”的完整链；
3. 记录任务卡、旧任务对象、配对/预定、目标变化和终态的逐步读数；
4. 调用退出入口并退回非 Play；
5. 交付报告附完整 Console 错误栈、入口/退出时刻、seed 或等价可复现参数。

D0 任一停手条件命中，立即停手报裁，不进入 D1。

### D1：首片与小源分层切换

- 首片：WorldGatherSource。
- 小源阶段：BlacksmithBuilding、ChestEntity、ConstructionSiteStore、MineByproductComponent、SiegeWorkshopBuilding；这 5 个与首片合计 6 个可迁移小源。
- 非迁移对象：DebugTaskSource，只在清理阶段按零注册证据处理。
- 巨兽阶段：Building、UnitController，单独立项，不能与首片/小源混改。

每一阶段都必须保留旧链基线快照、可独立回退点和进局回归结果。阶段之间未取得绿色裁决，不得跨段施工。

### D2：后置清理门

D2 不在本任务书中执行。只有以下条件同时成立，才可另取③清理任务书：

- 8 个实体源均完成切换并通过进局回归；
- DebugTaskSource 已证明零注册且有删除影响面清单；
- KingdomTaskType、旧 KingdomTask、旧源接口消费面均为 0，扫描域明确为 Assets/_Game 生产码；
- TaskScheduler.Instance|HasInstance 旧调用面归零，唯一管理器与 ScheduleCenterStub._crewAssignments 边界已闭合；
- §九七条判据和读档重验均有可复算证据；
- 另行取得主策划终裁。未达成前，不得删枚举、删旧源、删旧资产键或改四本账本。

## 三、允许改动文件路径清单

> 下面是执行端本批可施工面；新增文件须在开工回执中逐项列出。除清单外的路径命中即停手报裁。

### D0 允许面

- Valley Rampart/Assets/Editor/Smoke/：临时进局探针 1 个及其 .meta；探针只用于验证，收工删除并报告去向。
- Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/：仅为接入正门所需的验证适配文件；不得把探针逻辑写入生产链。
- Logs/：D0 运行日志、Console 归因日志、前后对照日志。

### D1 允许面

- Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs：最小兼容接缝；不得借机重写 1531 行内核。
- Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/ITaskScheduler.cs：仅在接缝确需时改；不得先行扩成新主入口。
- Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskCardProtocol.cs、TaskProtocolRuntime.cs、TaskProtocolIssuer.cs、TaskBindingManager.cs：仅允许为首片/小源调用或补必要适配；不得改变 M5-A/B/C 已裁政策与释放契约。
- Valley Rampart/Assets/_Game/Systems/World/WorldGatherSource.cs
- Valley Rampart/Assets/_Game/Systems/Kingdom/BlacksmithBuilding.cs
- Valley Rampart/Assets/_Game/Systems/World/ChestEntity.cs
- Valley Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs
- Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs
- Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs
- 以上新增/改动文件对应 .meta。
- Logs/：每阶段扫描、编译、进局回归与回退证据。

### 明确不在本批允许面

- Building.cs、UnitController.cs：只读盘点；巨兽改造另立阶段。
- 最高优先级文档/、多Agent交接/ 四本账本、任何策划文档。
- Assets/Resources/Buildings/**/*.asset 旧序列化键。
- *.prefab、*.unity、地图底层数组与场景。
- AI.Core。
- 任何与本批无关的生产码或测试基线。

## 四、五项政策与既有契约

M5-C 已落值，本批只调用，不重新定值：

1. priority：上层写 basePriority，运行时只读计算 EffectivePriority，禁止回写。
2. retry：retryMax=1，初次 1 次加重试 1 次。
3. 不可达冷却：5 tick，tickInterval=1s。
4. 分域：王国级，不接 chunk。
5. 统计：按单个 TaskCard，不建全局主表。

M5-A/B 的生命周期、配对、预定、终态释放契约保持不变；ScheduleCenterStub._crewAssignments 仍作为观察边界，不得宣称单管理器已完成。

## 五、验收判据

### 5.1 D0 进局链

- 入口成功且退 Play 成功：EnterTestRun=1、ExitTestRun=1、Play 状态回到 false。
- 完整链至少 1 条；逐步记录数不少于 1：广告、收集、配对、推进、终态、释放各有明确事件。
- 任务卡与旧对象的对应关系可追踪；不存在未归因的中间悬挂。
- 新增错误数为 0；若有 Console 异常，须给完整堆栈与前后对照归因。

### 5.2 D1 首片/小源

每个已切源分别给出：

- 源注册/注销各至少 1 次，且重复注册不增加记录；
- 新卡创建、提交、配对、完成或作废各至少 1 次；
- TaskBindingManager 释放结果成功；终态后配对与预定均为 0；
- 目标消失/变化在下一 tick 内完成处置；
- basePriority 前后相等，EffectivePriority 只读；
- retry 读数为首次成功、第二次 RetryExhausted；
- 不可达冷却 tick 100–104 不可派发、105 恢复；
- 两个 kingdom 分桶不混，两个 TaskCard 统计互不混淆；
- 进局回归至少 1x；每阶段报告 seed/参数与退 Play 证据。

### 5.3 旧链基线

扫描域必须写在报告标题与命令旁：

- 生产域：Valley Rampart/Valley Rampart/Assets/_Game/**。
- 全 Assets 对照：可另列，但不得替代生产域。

生产域前后必须保持：

- TaskScheduler.Instance|HasInstance：54 行 / 15 文件，直到对应源切换阶段才按阶段列出下降原因；未解释的下降即失败。
- new KingdomTask( 调用：14 行 / 9 文件，同上。
- ITaskSource：9 类实现者基线；每切一源，列出减少项与新能力声明项。
- KingdomTaskType：11 项，D2 前不得删除。
- 新协议零接线扫描：首片/小源之外不得出现未授权接线；排除清单必须完整。
- AI.Core：0 改动、0 新命中。

### 5.4 §九七条判据

每个阶段交付报告必须分别给出：

1. 零引用：源与工人不持对方旧对象引用；
2. 目标可判：下一 tick 内处置；
3. 配对不悬空：终态释放；
4. 不居中态：异常回待派或作废；
5. 读档重验：进行中任务逐条重验；
6. 单管理器：阶段内只有一个管理器入口；_crewAssignments 未闭合时明确列为未达成；
7. 不碰地图数据：无地图底层数组读写。

## 六、交付物与复算要求

- 开工回执：阶段、最终改动文件清单、接缝设计一句话、D0 入口与退 Play 方案。
- 交付报告：git diff --name-only、git diff --check、编译 errors=0、Console 完整堆栈、进局前后对照、扫描命令与原始读数、断言总数/执行数/失败数。
- 临时探针删除后，报告列出源与 .meta 是否删除；日志保留。
- 所有“换工具直到测出期望值”必须给出差异根因、原始输出与复算输出。
- 所有基线必须声明扫描域、计数单位和排除清单。

## 七、停手条件

命中任一条立即停手报裁，执行端不得自选边：

1. D0 不能由 TestHarnessApi.EnterTestRun 完成，或无法正常退 Play；
2. 活世界链出现未归因异常、任务悬挂、配对泄漏或目标失联；
3. 需要同时改 Building 与 UnitController 才能让首片成立；
4. 需要把 ITaskScheduler 十方法扩张作为前置，或发现真实主耦合不在单例消费面；
5. 旧链基线下降但无法逐项解释；
6. 新协议政策被回写、改值或新增未裁定政策；
7. 触碰 AI.Core、场景、地图数据、旧资产键、四本账本或最高优先级文档/；
8. 编译出现本批新增错误，或 Console 出现无法归因的本批错误；
9. 需要把 ScheduleCenterStub._crewAssignments 的第二载体隐瞒为单管理器已完成；
10. 需要删 KingdomTaskType、KingdomTask、旧源或旧资产键才能继续；
11. 需要一次性改动两个巨兽或超过当前阶段允许面；
12. 任何判据只能靠 Edit 探针而不能在 D0/阶段进局复现。

## 八、交付数量

- D0：1 个进局入口、1 条完整活链、1 份前后 Console 对照、1 份可复现日志包。
- D1 首片：1 个源 WorldGatherSource，1 个独立回退点，至少 1x 进局回归。
- D1 小源：5 个小源分 5 个阶段；每阶段 1 份基线、1 份进局回归、1 份失败/停手判定。
- 巨兽：0 个进入本批施工；各保留 1 份只读盘点表。
- D2 清理：0 个本批执行项，另取任务书。

## 九、红线复述

- 不改四本策划账本，不改最高优先级文档/，不改场景与旧资产键。
- 不把 Edit 模式探针写成活世界验收。
- 不把 149/27 写成生产调用面。
- 不把 ITaskScheduler 空壳改造成未经裁定的主入口。
- 不把两个巨兽提前并入首片。
- 不自取 D 号；本批只使用 HH.341 占位。

---

## 【应登记项】

HH.341 | M5-D乙加：D0进局验证＋小源分层切换任务书 | 主策划决策端 | 待执行 | ②切换分段放行；③清理后置另裁 | 不写D号

## 【后台登记说明】

- HH.341：由事务端按实存锁定并落账。
- 事务端核实本任务书的允许改动面、判据、停手条件与实际执行报告后，方可入提交。
- 本任务书未授权执行端改写本文件、四本账本或最高优先级文档/。
