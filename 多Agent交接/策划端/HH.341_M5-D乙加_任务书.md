# HH.341_M5-D乙加_任务书（v2）

> 主策划端 B1（2026-09-26）
>
> 本文件是原 多Agent交接/策划端/HH.341_M5-D乙加_任务书.md 的 v2 修订版，原文件不再与本版并存使用；所有 D1 施工与验收以本版为唯一正文。D 号由事务端按实存顺延，本任务书不自取 D 号。
>
> HH.342 是与本批分开的 DZ-新 存档冲突修复批。A 必须先修复并重新基线，B 才能进行 D1 首片的正式进局生产回归。两批不混改、不共用提交面。

## 一、性质、目标与现状基线

- 性质：施工切换批；沿用“乙加”分段方案。D0 进局证据已经完成，D1 首片只在前置门、HH.342 修复和本版条件全部满足后施工。
- 首片目标：仅将 WorldGatherSource 接入真实新任务协议生产面，建立可回退的最小兼容接缝，并证明活世界完整链路。
- 后续小源：首片验收后再按阶段处理 BlacksmithBuilding、ChestEntity、ConstructionSiteStore、MineByproductComponent、SiegeWorkshopBuilding；五源不与首片混改。
- 后置巨兽：Building、UnitController 单独立项；ScheduleCenterStub._crewAssignments 不在首片改动面。
- 非迁移对象：DebugTaskSource（WorkerTask.cs:58）从不注册，属于删除候选，不得当作迁移源。

### 1. 生产基线（扫描域和计数单位固定）

- 生产域：Valley Rampart/Valley Rampart/Assets/_Game/**；计数单位为“代码行 / 文件数”，排除 Editor/Smoke 和协议文件外的非生产探针。
- TaskScheduler.Instance|HasInstance：54 行 / 15 文件。
- new KingdomTask(：14 行 / 9 文件（剔除 ScheduleCenterStub.cs:99 注释）。
- ITaskSource 实现者：9 类，含未注册的 DebugTaskSource；它是删除对象，不是迁移对象。
- KingdomTaskType：11 项。
- ITaskScheduler 消费面：3 行 / 2 文件，当前为空壳消费面；不得先扩成十方法新主入口。
- 全 Assets 对照可列 149 行 / 27 文件，但不得写成生产调用面。

## 二、原任务书关系与阶段门

本版继承原任务书的 D0、乙加分段、D2 后置清理和七条 §九判据框架，并以本版新增的 D875 硬条件为覆盖性约束。若旧文与本版冲突，以本版为准；不得保留“两份并存且不一致”的执行口径。

### D0：只读进局门（前置证据，不替代 D1）

- 入口必须是正门 TestHarnessApi.EnterTestRun，退出必须回到非 Play。
- 至少取得一条真实生产链的“源广告 → 调度器收集 → 工人接单/配对 → 移动/推进 → 完成或异常回收”逐步读数。
- 记录 seed/参数、入口/退出时刻、任务卡/旧对象、配对/预定、目标变化、终态和完整 Console 堆栈。
- Console 采用 L-95 三段法：Play 内读取、退 Play 后读取、空白对照复跑。
- D0 不能证明 D1 已接线；HH.342 修复后的重新基线仍是 D1 正式进局回归前置门。

### D1：首片 WorldGatherSource

首片只允许一个生产源和必要兼容接缝；每个阶段独立回退、独立基线、独立进局回归。首片未绿，不得进入五小源阶段。

### D2：后置清理

D2 不在本批执行。只有八个实体源完成切换、旧链零引用、DebugTaskSource 零注册证据、唯一管理器边界闭合且另获主策划终裁后，才可另立清理任务书。不得在 D1 顺手删除旧枚举、旧源、旧资产键或四本账本。

## 三、D875 七条放行硬条件（逐条执行）

1. 施工范围：只覆盖 WorldGatherSource 与必要的兼容接缝；不得扩展到其他源或巨兽。
2. 真实生产实例：生产面必须出现真实的新 TaskCard 协议实例并被生产链消费；影子面只能作辅助诊断，严禁替代生产验证。
3. 占用权威迁移：工人占用必须从旧 TaskScheduler._npcTaskMap 迁入 TaskBindingManager 的配对/预定表；首片必带。过渡镜像若保留，必须逐次与新表对拍并在终态、超时、读档重建同时清理。
4. 同帧取证：同帧配对先后只能用单调调用序或前后状态快照证明；禁用帧号推断先后。
5. 同目标重派：按 taskId 聚合；最多 2 次尝试（初次 1 + 重试 1），释放后重派，终态封口，活动卡不得重复。
6. 切换后重跑：必须重跑生产面链路、旧链基线、跨档重建重验；任何一项缺失都不放行。
7. 巨兽/第二载体排除：Building、UnitController、ScheduleCenterStub._crewAssignments 不得混入首片改动面；不得宣称 §九-6 单管理器已达成，ScheduleCenterStub.cs:56 仍在。

## 四、已定案政策（本批只调用，不重新定值）

- priority：上层写 basePriority；运行时只读计算 EffectivePriority，禁止回写。
- retryMax=1：初次 1 次 + 重试 1 次；按 taskId 聚合，第二次失败封口。
- 不可达冷却：5 tick；tickInterval=1s，不得用其他时基替代。
- 分域：王国级，不接 chunk。
- 统计：按单个 TaskCard，不建全局主表。
- 读档后：不复用绝对 tick；按当前 tick + 既定 5 tick 重建资格。
- +∞ deadline：显式 hasDeadline；不用 -1 哨兵，持久化不得写 Infinity。
- TaskCard 不入档（D873 第四案“重建重验”）：不得加 [Serializable]、卡片台账或 ISaveable。
- Building.currentWorkers 是死载体：不启用、不填充、不作验收证据；不得为制造读数临时修复。

## 五、首片允许改动文件路径清单（B 独立提交面）

除以下路径外命中即停手。meta 只随本批新增文件产生；不得把临时探针或报告混入生产提交。

1. Valley Rampart/Assets/_Game/Systems/World/WorldGatherSource.cs
   - 首片唯一生产源；允许接入真实 TaskCard 广告、提交、目标推进、完成/作废和释放。
2. Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
   - 仅允许最小兼容接缝、生产卡消费和 _npcTaskMap 占用迁移；不得重写调度器内核。
3. Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/ITaskScheduler.cs
   - 仅在接缝确需时改；不得先扩十方法接口或另立主入口。
4. Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskCardProtocol.cs
5. Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskLifecycleRules.cs
6. Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskBindingTypes.cs
7. Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskBindingManager.cs
8. Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskProtocolRuntime.cs
9. Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskProtocolIssuer.cs
   - 4–9 仅允许为首片生产调用、兼容接缝、重派聚合、终态/读档释放补足必要适配；不得入档 TaskCard 或改写已定政策。
10. Logs/hh341_d1_*
   - 扫描、编译、进局、L-95、生产/影子分列和跨档证据；不与 A 的 Logs/hh342_* 共用。

明确不在 B 面：Valley Rampart/Assets/_Game/Systems/Building/Building.cs、Valley Rampart/Assets/_Game/Systems/Unit/UnitController.cs、Valley Rampart/Assets/_Game/Systems/AI/Schedule/ScheduleCenterStub.cs；最高优先级文档/、四本账本、场景、Prefab、地图底层数组、Assets/Resources/Buildings/**/*.asset 旧键、AI.Core 及所有五小源文件。需要改未列路径，立即停手报裁。

## 六、首片生产面判据（可复算）

### 6.1 真实新协议链

生产面必须在真实 Play 进局中出现 TaskCard 实例数 >0，并逐个 taskId 追踪：Submit → Pair → Reservation → Move → Complete/Abort → Release。报告分列“生产面”和“影子面”，影子面任何读数都不能替代生产面正证。

### 6.2 占用、配对与重派

- TaskBindingManager 是 WorldGatherSource 工人占用的唯一生产权威；_npcTaskMap 对首片不再接收新权威写入。
- 配对表、预定表在 Complete、Abort、超时、读档重建四条终态路径均归零；无悬空绑定。
- 同目标按 taskId 聚合：每卡最多 2 次尝试；列出尝试数、retryConsumedCount、释放数、终态数，逐项相等且无重复活动卡。
- 同帧配对证据必须给单调调用序或前后状态快照；报告不得只给帧号。
- Building.currentWorkers 读数只能作为“死载体诊断”，不得计入通过数。

### 6.3 读档重建

TaskCard 不入档。读档后必须按当前 tick 重新广告并重建资格，绝不复用绝对 tick；重新广告后无悬空绑定、无重复活动卡，既有卡的终态/释放证据可追溯。hasDeadline 明确区分无期限，持久化中不得出现 Infinity。

### 6.4 旧链基线与计数

生产域扫描必须列命令、路径和计数单位：

- TaskScheduler.Instance|HasInstance 54/15 的前后差异逐项解释；
- new KingdomTask( 14/9 的前后差异逐项解释；
- ITaskSource 9 类实现者的迁移/删除/保留逐项解释；DebugTaskSource 只能列为删除候选；
- KingdomTaskType 11 项，首片不得删除；
- ITaskScheduler 消费面 3/2，不得因首片扩成未经裁定的十方法主入口；
- 全 Assets 149/27 只能作对照，不得替代生产基线；
- AI.Core 改动和新增命中均为 0。

## 七、首片后生产复验必取（D875 清单）

交付报告必须逐项给原始读数和计数单位：

1. 新 TaskCard 实例数 > 0；
2. Submit→Pair→Reservation→Move→Complete/Abort→Release 全链可追踪；
3. 配对与预定终态归零；
4. TaskBindingManager 成为 WorldGatherSource 的占用权威；
5. 同 taskId 的尝试数、retryConsumedCount、释放数、终态数逐项对拍；
6. 读档后重新广告，且无悬空绑定、无复用绝对 tick；
7. 旧链基线变化逐项解释；
8. 影子面与生产面分列，禁止互相替代；
9. currentWorkers 明确排除为验收证据；
10. L-95 三段 Console 读数中，A 修复冲突词保持 0，且不得把 A 的日志污染算入 B 的失败或通过。

## 八、停手条件（不得自选边）

1. HH.342 修复未完成、四坐标任一仍复现，或未取得修复后重新基线；
2. 生产面没有真实新 TaskCard，只能靠影子面或 Edit 探针证明；
3. _npcTaskMap 仍是 WorldGatherSource 的占用权威，或迁移后配对/预定无法在四条终态路径归零；
4. 同帧先后只能拿帧号解释，或同目标重派超过 2 次、未释放即重派、出现重复活动卡；
5. 需要改 Building、UnitController、ScheduleCenterStub._crewAssignments、currentWorkers、场景、旧资产键、AI.Core、最高优先级文档或四本账本；
6. 需要给 TaskCard 加 [Serializable]/ISaveable、新增卡片台账、持久化 Infinity 或复用绝对 tick；
7. 旧链基线下降无法逐项解释，或把 149/27、Edit 探针、影子读数冒充生产证据；
8. 编译新增 error、Console 出现无法归因错误、读档后悬空绑定或终态残留；
9. 需要先扩 ITaskScheduler 十方法接口、一次性切五小源或宣称 §九-6 单管理器已达成；
10. 任何新增文件、路径、参数定值超出本版，执行端不得自选边，须报裁。

## 九、交付物清单

1. 开工回执：A 修复后基线已核实；首片生产面、接缝、独立回退点、文件清单和当前行号锚点。
2. 代码交付：只含 B 允许面；提交前给逐文件 git diff --name-only、file:line 改动区间和 git diff --check。
3. 进局报告：正门入口、seed/参数、Play/退 Play 时刻、生产面完整链、L-95 三段 Console 原始输出。
4. 生产复验表：新卡数、六段链、配对/预定终态、占用权威、taskId 聚合重派、读档重建、旧链前后基线、影子/生产分列。
5. 编译与扫描包：errors、warnings 归属、生产域/全 Assets 对照命令和原始计数；不得混写扫描域。
6. 回退与事务核实：独立回退点、A/B 提交面不相交证明；事务端核实后方可入提交。本任务书不授权执行端改写本文件、四本账本或最高优先级文档。

## 十、§九判据边界复述

首片只报告本阶段已完成的零引用、目标可判、配对不悬空、不居中态、读档重建重验和不碰地图数据。ScheduleCenterStub.cs:56 的第二 _crewAssignments 仍在，因此不得声称 §九-6 单管理器已达成；该项是后续清理/迁移的独立门槛。

## 【应登记项】

HH.341 | M5-D乙加 D1 首片：WorldGatherSource 生产接线与重验（v2） | 主策划策划端 | 待执行 | 先完成 HH.342 修复并重新基线，再按 D875 七条硬条件施工和复验；五小源、巨兽与 D2 清理后置 | 不写 D 号

## 【本批写入与提交声明】

本文件只修订 B 的任务书正文。执行端每次写代码或日志，必须逐文件列出改动路径及 file:line 区间，交事务端核实后方可入提交；不得替用户重新定值、不得自取 D 号、不得改四本账本。A 与 B 各自独立提交面，不共用暂存路径。