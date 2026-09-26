# HH.341_M5-D乙加：小源阶段任务书

> 主策划端 B1（2026-09-26）
>
> 本文件是 HH.341 D1 首片通过后的五小源阶段唯一正文。原
> 多Agent交接/策划端/HH.341_M5-D乙加_任务书.md v2 只管 D1
> WorldGatherSource 首片，不被本文件改写；本文件只管五小源的后续分层切换。
> 两份正文不共用同一施工窗口、提交面或验收结论。本任务书不自取 D 号。

## 一、前置状态、性质与目标

- 前置门：HH.341 D1 首片已完成完整验收，五小源阶段可以开工。
- 性质：五小源分层迁移施工批；每个源是一个独立施工窗口、独立回退点、
  独立生产基线和独立进局验收。
- 目标：将五个小源逐个接入已经验证过的真实新任务协议接缝，完成源级
  生产面、占用、重派、终态、读档重建和旧链基线复验。
- 本阶段不继承 D1 的“一个首片窗口即可覆盖后续源”口径。一个源未绿，
  不得进入下一个源；五源不得一次切换、一次提交或一次合并验收。
- 两巨兽 Building、UnitController 后置另立任务；ScheduleCenterStub 的
  _crewAssignments、Building.currentWorkers 仍不属于本阶段。

## 二、切法裁定与施工顺序

### 2.1 切法

本阶段采用**逐源串行**：

1. 施工端一次只打开一个源文件的生产迁移窗口；
2. 当前源完成独立回归、基线复算和事务端核实并判绿后，才可申请下一个源；
3. 本文件是五源阶段总正文，但每个源必须有独立执行报告、独立证据目录、
   独立回退点和独立验收记录；
4. 任何源的失败、证据缺项或需扩大范围，均在该源窗口停手，不得顺手推进
   其他源。

### 2.2 默认顺序

按已取证的生产耦合度从低到高推进，执行端不得自行换序：

| 顺序 | 当前源 | 文件 | TaskScheduler.Instance\|HasInstance 命中行数 | new KingdomTask( 命中行数 |
|---|---|---|---:|---:|
| 1 | ConstructionSiteStore | Valley Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs | 0 | 1 |
| 2 | ChestEntity | Valley Rampart/Assets/_Game/Systems/World/ChestEntity.cs | 1 | 1 |
| 3 | MineByproductComponent | Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs | 5 | 2 |
| 4 | BlacksmithBuilding | Valley Rampart/Assets/_Game/Systems/Kingdom/BlacksmithBuilding.cs | 6 | 1 |
| 5 | SiegeWorkshopBuilding | Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs | 6 | 1 |

首个施工源确定为 **ConstructionSiteStore.cs**。选择依据是当前生产域命中
耦合度最低，且仅有一处 KingdomTask 构造；这不是对后续源的完成承诺。
首源未绿前，ChestEntity 及其余三个源只能保持只读取证。

## 三、生产域、基线与计数口径

- 生产扫描域固定为：
  Valley Rampart/Valley Rampart/Assets/_Game/**
- 计数单位必须写明是代码行数/文件数；表中“命中行数”不等于匹配次数，
  不得混用两种口径。
- 本阶段起始基线取 D1 绿态之后的生产域读数：
  - TaskScheduler.Instance|HasInstance：59 行 / 16 文件；
  - new KingdomTask(：14 行 / 9 文件，剔除注释；
  - ITaskSource 实现者：9 类；
  - KingdomTaskType：11 项；
  - ITaskScheduler 消费面：3 行 / 2 文件，仍为空壳消费面；
  - 全 Assets 的 149 行 / 27 文件只能作对照，不得当作生产调用面。
- 每个源完成后都要以“上一源绿态”为本源基线，重新扫描上述项目。
  下降、持平或上升都必须逐项解释；不预设必须下降多少，也不得用总量
  变化掩盖单文件增量。
- AI.Core、场景、Prefab、旧资产键和四本账本的命中必须为零。

## 四、跨源复用的既定接缝与政策

以下内容是 D1 已验证的既定基线，本阶段只复用，不重新定值：

- 调度器持有 TaskProtocolRuntime；按既有懒初始化、GameLoadedEvent 重建和
  派发/到达/放弃/源失效四类回调接入；
- TaskBindingManager 是新协议占用权威；旧 TaskScheduler._npcTaskMap 只能
  作为逐次对拍镜像，不得重新成为生产写权；
- 建卡必须经 TaskProtocolIssuer.Create/Submit，不得直接 new TaskCard 后
  手填字段；
- deadline 必须使用显式 hasDeadline；不使用 -1 哨兵，不以 IsInfinity 猜测，
  不把 TaskCard 写入存档；
- priority 由上层写 basePriority，运行时只读计算 EffectivePriority，不回写；
- retryMax=1，表示初次一次加重试一次；按 taskId 聚合，释放后才可重派；
- 不可达冷却为既定 5 tick，tickInterval 为既定 1 秒；
- 分域为王国级，统计按单个 TaskCard，不建全局主表；
- 读档后按当前 tick 重建资格，不复用绝对 tick；
- Building.currentWorkers 是死载体，不启用、不填充、不作为验收证据；
- 不得新增 TaskCard 的 Serializable、ISaveable、卡片台账或持久化 Infinity。

## 五、每源开工前的卡片适用性门

执行端不得先写代码再判断某源是否产卡。当前源必须先交只读预检，至少包含：

1. 生产触发入口、真实调用链和源状态变化的 file:line 锚点；
2. 该源的 KingdomTask/TaskScheduler 引用各自对应的生产分支；
3. 该分支是否代表工人可接取、移动、完成或放弃的任务；
4. 判定为“任务卡源”或“非任务卡源”，并说明判定证据。

### 5.1 任务卡源

若预检证明该源确实产生工人任务，则必须在真实 Play 进局的生产面出现
真实 TaskCard；完整链路按第七节验收，不得用影子面、Edit 探针或人工造卡
替代。

### 5.2 非任务卡源

若预检证明该源不是工人任务源，禁止为了满足“TaskCard 数大于零”而造卡。
此时不得由执行端自定迁移或删除口径，必须先停手报裁，并提交以下替代证据
供主策划确认：

- 真实生产事件及其可复算结果；
- 新协议或必要兼容接缝是否应被调用，以及调用序；
- 源状态的完成、失败、释放和幂等边界；
- 若有存档状态，读档后的重建和无悬空证据；
- 旧 KingdomTask 路径是迁移、保留还是删除候选；
- 该源不产卡对旧链基线的影响。

在替代口径未获裁定前，非任务卡源不得进入施工，也不得以“没有卡片”
直接判绿。这样可以避免把首片判据错误套在不适用的源上。

## 六、允许改动文件路径清单

以下清单是本阶段总允许面；**当前源之外的四个小源文件在本窗口一律不得写**。
执行端需要未列路径时，立即停手报裁。

1. 当前施工源文件（首源只能是）：
   - Valley Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs
2. 后续按既定顺序各自单独开窗的源文件：
   - Valley Rampart/Assets/_Game/Systems/World/ChestEntity.cs
   - Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs
   - Valley Rampart/Assets/_Game/Systems/Kingdom/BlacksmithBuilding.cs
   - Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs
3. 兼容接缝文件：仅在当前源确实需要，且必须逐文件说明原因和
   file:line 锚点：
   - Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs
   - Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskCardProtocol.cs
   - Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskLifecycleRules.cs
   - Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskBindingTypes.cs
   - Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskBindingManager.cs
   - Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskProtocolRuntime.cs
   - Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskProtocolIssuer.cs
4. 证据目录：
   - Logs/hh341_small_*
   - 每个源必须使用独立前缀，例如
     Logs/hh341_small_construction_site_*；不得覆盖 D1 或其他源证据。

本阶段默认禁止改 ITaskScheduler.cs；若接缝确实无法在现有接口下完成，必须
停手报裁，不得自扩十方法接口或另立主入口。

明确不在本阶段改动面：Building.cs、UnitController.cs、
ScheduleCenterStub.cs、ScheduleCenterStub._crewAssignments、currentWorkers、
场景、Prefab、地图底层数组、Assets/Resources/Buildings/**/*.asset 旧键、
AI.Core、最高优先级文档、四本账本以及其他四个未轮到的小源。
不得声称 §九-6 单管理器已经达成。

## 七、每源施工与复验步骤

每一个源都必须完整执行以下步骤，不能把某一步借用上一源的报告：

1. **独立回退点**：记录当前源开工前 HEAD、工作树状态和允许面清单；
   回退点不得包含其他源的改动。
2. **只读预检**：完成第五节的调用链和卡片适用性判定；未判定不得写代码。
3. **最小施工**：只处理当前源和必要接缝；不顺手重构、不改变既定政策值。
4. **静态检查**：编译、git diff --check、生产域扫描、AI.Core/资产/场景/
   账本零命中检查。
5. **真实进局**：使用正门进入 Play，记录 seed/参数、入口/退出时刻和
   生产面证据；影子面只作辅助并与生产面分列。
6. **跨档复验**：执行当前源读档重建；按当前 tick 重建资格，重新广告并
   重新派工（适用于任务卡源），确认没有绝对 tick 复用、悬空绑定或重复卡。
7. **L-95 三段**：Play 内生产链、退 Play 后、只进 Play 不建局的空白对照。
   三段必须保留原文并分列生产面、影子面和空白对照。
8. **基线复算**：以本源开工前基线和施工后基线逐项对拍，并解释每一项差异。
9. **事务核实**：逐文件给出改动路径、file:line 区间、扫描命令和原始读数；
   未经事务端核实不得入提交、不得进入下一个源。

L-95 若遇暂时性 read_console 超时，只能重试同一工具；每段最多三次总尝试。
三次仍无原文读数时，该段保持未完成，不得用机械捕获的 error/exception=0
降级替代，也不得自行判绿。

## 八、任务卡源的可复算判据

以下判据只适用于第五节判定为任务卡源的当前源：

1. 生产面真实 TaskCard 实例数大于零，且每张卡按 taskId 可追踪：
   Submit → Pair → Reservation → Move → Complete/Abort → Release。
2. 建卡走 TaskProtocolIssuer；TaskBindingManager 是占用权威；旧
   _npcTaskMap 只作为镜像对拍。
3. 同帧配对以单调调用序或前后状态快照取证，禁止只凭帧号推断先后。
4. 同目标按 taskId 聚合：最多初次一次加重试一次；释放后重派；终态封口；
   不出现重复活动卡。
5. Complete、Abort、超时、读档重建四条路径的配对和预定均归零，且没有
   悬空绑定。
6. 逐卡报告尝试数、retryConsumedCount、释放数、终态数；尝试数等于
   初次一次加 retryConsumedCount，且不超过两次。
7. 读档后重新广告并再派工；新卡不复用旧卡 id，资格时间按当前 tick 重算。
8. 生产面无新增未归因错误；L-95 三段原文完整，既有 O-14 噪声须单独归档，
   不得算作本源新增错误。
9. 既定策略、hasDeadline 载体、TaskCard 不入档和 currentWorkers 排除规则
   均保持不变。

## 九、非任务卡源的暂定处理边界

非任务卡源未获单独裁定前不进入施工。若主策划后续确认其可以在本阶段处理，
验收必须至少覆盖：

- 真实生产事件从触发到结果的可复算序列；
- 源状态的成功、失败、释放、幂等和读档重建；
- 新协议接缝或兼容旁路的真实调用证据；
- 旧链引用和计数变化的逐项解释；
- L-95 三段、编译、扫描域和回退点。

不得用“TaskCard 数为零”直接证明通过，也不得为了取卡片读数改变源的业务
语义。没有这项单独裁定时，当前源状态为停手待裁。

## 十、停手条件

出现任一情况，当前源立即停手，不得自选边：

1. 未完成只读预检，或无法证明源的生产触发链和卡片适用性；
2. 试图一次切换两个以上小源，或在首源未绿前改动后续源；
3. 需要改 Building、UnitController、ScheduleCenterStub、_crewAssignments、
   currentWorkers、场景、Prefab、旧资产键、AI.Core、最高优先级文档或账本；
4. 需要扩 ITaskScheduler、重写 TaskScheduler 内核、增加全局任务主表或
   改动已定政策值；
5. 生产面没有真实任务链，只能依赖影子面、Edit 探针、人工造卡或静态命中；
6. _npcTaskMap 仍是生产占用权威，或配对/预定在任一终态或读档后不归零；
7. 重派超过两次、未释放即重派、终态后仍有活动卡或出现悬空绑定；
8. 读档复用绝对 tick、持久化 Infinity、TaskCard 入档或 currentWorkers 被
   作为通过证据；
9. L-95 原文缺失、换工具替代、三次同工具失败后强行降级，或出现新增未归因
   Console 错误；
10. 旧链基线变化无法逐项解释，或把 149/27 当生产调用面；
11. 需要自定下一个源的顺序、改变非任务卡源的判据，或声称 §九-6 单管理器达成。

## 十一、交付物清单

每个源分别交付，不得合并成五源总报告：

1. 开工回执：当前源、顺序位置、前置基线、独立回退点、只读预检结论和
   卡片适用性判定。
2. 逐文件代码清单：路径、改动性质、file:line 区间、提交面证明；
   事务端核实前不得入提交。
3. 生产进局报告：入口、参数、生产面链、配对/预定、占用权威、重派和终态。
4. 跨档报告：实例是否重建、重新广告与再派工、资格时间、悬空绑定和重复卡。
5. L-95 三段原始输出：生产面、影子面、空白对照分列；记录暂时性噪声归属。
6. 基线包：生产域命令、代码行/文件数口径、施工前后计数和逐项归因。
7. 编译与清洁检查：error、warning 归属、git diff --check、行尾、BOM/NUL。
8. 源级验收结论：通过、未通过或停手待裁；未绿源不得推进下一源。

## 【应登记项】

HH.341 | M5-D乙加五小源阶段 | 逐源串行，首源 ConstructionSiteStore；每源独立回退、生产基线、进局回归和事务核实；D1 v2 只管 WorldGatherSource 首片 | 不写 D 号

## 【本批写入与提交声明】

本文件是策划端新增的小源阶段任务书正文。执行端不得改写本文件、四本账本或
最高优先级文档；任何代码或日志写入必须逐文件列出 path 与 file:line 区间，
交事务端核实后方可入提交。本阶段与 D1 首片、HH.342 及后续巨兽批次保持
独立提交面。
