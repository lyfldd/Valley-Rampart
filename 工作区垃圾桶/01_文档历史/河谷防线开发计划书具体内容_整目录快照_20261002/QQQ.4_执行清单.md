# QQQ.4 执行清单

> 配套文档：QQQ.4_任务系统与资源生命周期重构.md
> 生成于 2026-08-07
> 完成于 2026-08-07（T1-T13 全部实施，Unity 编译 0 error）

## 任务总表

| 编号 | 任务 | 需求# | 类型 | 涉及文件 | 验收标准 | 状态 |
|------|------|-------|------|---------|---------|------|
| T1 | 调度器去重改为"源+任务类型"：`HasAssignedTaskForSource(s)` → `HasAssignedTaskForSourceType(s, type)`，Tick ③ 与 UpdateAssignedTasks 同步改 | 需求1 | 设计调整 | TaskScheduler.cs L516/L202 | 农场同时有 Production + WaterHaul 两个在派任务；非农场行为不变 | ✅ |
| T2 | 挑水目标指向最近水井：`ResolveDest` 的 `KingdomDestType.WaterNetwork` 分支改为扫描 `FindObjectsOfType<Building>()` 找最近 `def.id=="Well"` 且 Active 的坐标，无水井回退源坐标 | 需求1 | bug修复 | TaskScheduler.cs L474 | 挑水工人走向水井坐标（非 (0,0)）；无 Well 时回退源坐标 | ✅ |
| T3 | 任务派发职业过滤：`Tick()` 空闲 NPC 收集处加 `uc.EffectiveOccupation == Occupation.Worker`，跳过非工人 | 需求2 | bug修复 | TaskScheduler.cs L191 | 流浪汉/居民/君主永不被派任务；工人正常被派 | ✅ |
| T4 | 流浪汉豁免 Wander 门控：WanderStimulusProvider 把 `ctx.IsUnrecruitedVagrant` 分支移到第①步 `SafetyScore < wanderThreshold` 判断之前 | 需求3 | bug修复 | WanderStimulusProvider.cs L37 | 流浪汉无论 SafetyScore 高低都在营地 ±3 格徘徊 | ✅ |
| T5 | 流浪汉撤退目标=营地：NPCBrain BuildBaseContext 组装 safeAnchor 时 `unrecruitedVagrant ? homePoint : ResolveRetreatTarget(...)` | 需求3 | bug修复 | NPCBrain.cs L622 | 流浪汉低分撤退目标为营地，不抽城堡锚点 | ✅ |
| T6 | 锚点池分类：WanderAnchorPool 锚点分 Castle/Building/FreeSpot 三类，`TryPickAnchor(selfPos, recent, avoidCount, occupation, out anchor)` 按职业过滤（Worker 排除 Castle） | 需求4 | 设计调整 | WanderAnchorPool.cs | 池内锚点带类型；Worker 抽不到城堡预设点 | ✅ |
| T7 | 闲逛按职业抽锚点：WanderStimulusProvider ② 分支用 `ctx.Profession` 传职业参数调 `TryPickAnchor`（Worker 只抽 Building/FreeSpot） | 需求4 | 设计调整 | WanderStimulusProvider.cs L70 | 4 名空闲工人分散在建筑/空地锚点，不扎堆主城 | ✅ |
| T8 | 新增 WorkerInventory 组件：`carriedType/carriedAmount/carryCapacity`（取 ResourceCarryConfig），`TryStore/UnloadAll/IsFull/IsEmpty`；挂 Worker prefab | 需求5 | 新功能 | 新建 Systems/Unit/WorkerInventory.cs | 背包可存取；携带量=SO 配置（木=10） | ✅ |
| T9 | 背包存档：UnitSaveData 升 v5 存 carriedType/carriedAmount，UnitController Save/Load 门控恢复 | 需求5 | 新功能 | UnitController.cs | 读档后背包资源不丢 | ✅ |
| T10 | Gather 完成入背包：`ExecuteCompletion` Gather 分支从 `RulerController.ModifyResource` 改为 `GetComponent<WorkerInventory>().TryStore`，满则余量直接入国库兜底 | 需求5 | 新功能 | TaskScheduler.cs L449 | 采集后左上角资源不立即增加；工人背包有货 | ✅ |
| T11 | Transport 两段式：完成动作 `HarvestCarry` 改为入背包；状态机加 `CarryingToDest→Unloading`（背包非空移向 dest，到达卸货入仓库/国库清空背包→Complete） | 需求5 | 新功能 | TaskScheduler.cs L439/L332 | 工人从源搬货入背包→走往仓库→入货清空→任务完成 | ✅ |
| T12 | 仓库入货：StorageComponent 加 `Add(int amount)`（容量满返回剩余/拒绝），Unloading 卸货时调用，满则入国库兜底 | 需求5 | 新功能 | StorageComponent.cs | 仓库收货成功；满仓兜底入国库不丢 | ✅ |
| T13 | 端到端验证场景：AIDebugSpawnController 加 `SpawnLifecycleScenario()`（水井+农场+仓库+木头堆+3 工人），覆盖：双任务并行/背包搬运/流浪汉营地徘徊 | 需求1-5 | 测试场景 | AIDebugSpawnController.cs | Play 可一键复现全部 5 需求验证点 | ✅ |

## 跨需求依赖

| 任务 | 依赖 | 说明 |
|------|------|------|
| T10 | T8 | Gather 入背包需先有 WorkerInventory |
| T11 | T8, T12 | 两段式搬运需背包 + 仓库 Add |
| T7 | T6 | 职业抽锚点需先分类 |
| T13 | T1-T12 | 验证场景最后做 |

## 完整性校验

| 需求# | 文档章节 | 对应任务 | 状态 |
|-------|---------|---------|------|
| 需求1 | §需求1 农场双任务并行 | T1, T2 | ✅ |
| 需求2 | §需求2 任务职业过滤 | T3 | ✅ |
| 需求3 | §需求3 流浪汉完全游离 | T4, T5 | ✅ |
| 需求4 | §需求4 NPC 闲逛规则明确化 | T6, T7 | ✅ |
| 需求5 | §需求5 资源生命周期+工人背包 | T8, T9, T10, T11, T12 | ✅ |
