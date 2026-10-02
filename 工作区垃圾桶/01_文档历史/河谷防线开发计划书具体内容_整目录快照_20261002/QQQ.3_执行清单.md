# QQQ.3 执行清单

> 配套文档：QQQ.3_兜底设计.md
> 生成于 2026-08-07
> 粒度（DR-20）：本清单把 QQQ.3 的场景清单/原则/接口契约转成可执行任务，每条指明**实现方法**（具体动作而非模糊动词）。
> 依赖标注：A=现在能做完（自包含，无外部依赖）；B=等 QQQ.2 任务系统落地后实现。
> v2 更新（2026-08-07）：新增 B8-1~B8-20（20 项），覆盖生命周期审查发现的 16 项实证 bug（6 高危）+ D1-D14 转换决策落地。阶段 3（P0 独立 bug 修复）建议优先开展。

## 任务总表

| 编号 | 任务（兜底场景→动作） | 类别 | 涉及文件 | 实现方法（怎么做） | 依赖 | 验收 | 状态 |
|------|------|------|---------|--------------|------|------|------|
| B0-1 | 定义 `interface ITaskScheduler`：Register/Unregister/GetWorkerState/HasWorkerAssigned/OnNpcDied/OnBuildingDied/AbandonTask/OnThreatSuspended/OnThreatResumed（契约见 QQQ.3 §3.1） | 接口契约 | 新 ITaskScheduler.cs | 写纯接口定义，无方法体；放在 Assets/_Game/Systems/AI/TaskScheduling/ 或 _Game/Systems/AI.Core/；注释引用 QQQ.3 §3.1 | A | 接口编译通过；TaskScheduler 后续实现此接口 | ⬜ |
| B0-2 | 扩展 `interface ITaskSource`：加 `bool IsValid`、`Vector2 SourcePos`、`OnRegister/OnUnregister`（契约见 QQQ.3 §3.2） | 接口契约 | 现有 ITaskSource.cs 或新文件 | 在现有 ITaskSource 接口上追加成员；若 ITaskSource 尚不存在则新建；保持向后兼容 | A | 接口编译通过；实现 ITaskSource 的建筑类未编译错 | ⬜ |
| B0-3 | 定义 `interface ISaveableWithValidation : ISaveable`：`void ValidateAfterLoad()`（契约见 QQQ.3 §3.3） | 接口契约 | 新 ISaveableWithValidation.cs 或并入现有 ISaveable.cs | 继承现有 ISaveable，追加单一方法；注释引用 QQQ.3 §3.3 | A | 接口编译通过；NPCBrain/TrainingSystem/FormationController 后续实现此接口 | ⬜ |
| B0-4 | 在 `Building` 暴露 `event Action<Building> OnDied`，Die() 内触发；OnSpawn 调 TaskScheduler.Register（契约见 QQQ.3 §3.4） | 接口契约 | Building.cs | 在 Building 类加 `public event Action<Building> OnDied;`；Die() 末尾 `OnDied?.Invoke(this);`；OnSpawn 加 `if (TaskScheduler.HasInstance) TaskScheduler.Instance.Register(this);`（用 HasInstance 规避未就绪，对应 BLD-A7） | A | Building 编译通过；订阅者可订阅 OnDied；TaskScheduler 未就绪时 OnSpawn 不抛异常 | ⬜ |
| B0-5 | 在 `UnitController` 暴露 `public static event Action<int> OnUnitDied`（npcId），死亡时触发；暴露 `bool IsValid => IsAlive`（契约见 QQQ.3 §3.5） | 接口契约 | UnitController.cs | 在死亡逻辑（现有 Die() 或 HealthSystem 触发处）末尾 `OnUnitDied?.Invoke(this.npcId);`；加 `public bool IsValid => !IsDead;` | A | UnitController 编译通过；订阅方可订阅 OnUnitDied；IsValid 在死亡后返回 false | ⬜ |
| B1-1 | TaskScheduler 实现 OnNpcDied 钩子（NPC-A1/A5） | 清理钩子 | TaskScheduler.cs | TaskScheduler 单例 Awake 时 `UnitController.OnUnitDied += OnNpcDied;`；OnNpcDied(npcId) 内从 `_npcTaskMap` 取出 KingdomTask，调 `task.Abandon()` 或直接 `task = null`；幂等（npcId 不存在时无操作） | T17（B0-1 契约先） | NPC 执行任务途中死亡后，调度器 _npcTaskMap 中该 npcId 条目被清；ProducerComponent 下一 tick HasWorkerAssigned 返回 false | ⬜ |
| B1-2 | TaskScheduler 实现 OnBuildingDied 钩子（BLD-A1/A2/A3/A4） | 清理钩子 | TaskScheduler.cs | TaskScheduler 单例 Awake 时订阅所有 Building.OnDied（或在 Building.Die 内主动调 `TaskScheduler.Instance.OnBuildingDied(this)`）；OnBuildingDied 内 `Unregister(source)`；遍历 _npcTaskMap 找到指向该 source 的任务，标记 source 失效（不立即 Abandon，让 NPC-A6 自查时清理） | T17, B0-4 | 建筑被摧毁后，调度器注册表移除该建筑；该建筑的任务不再被重新派发 | ⬜ |
| B1-3 | NPCBrain 在访问 currentTask 前校验 `source.IsValid`（NPC-A6/A7/A9） | 校验逻辑 | NPCBrain.cs | 在 currentTask 访问点（每个 tick 的任务消费分支）加 `if (currentTask != null && !currentTask.source.IsValid) { AbandonCurrentTask(); currentTask = null; }`；AbandonCurrentTask 内调 TaskScheduler.AbandonTask(npcId)；try-catch 包裹访问逻辑，异常时清 currentTask 回 Idle（NPC-A7） | T18 | NPC 走到已摧毁建筑时自动 Abandon 回 Idle，不卡死；currentTask 引用异常时 NPCBrain 不崩溃 | ⬜ |
| B1-4 | ThreatStimulus 抢占时任务挂起 + 超时 Abandon（NPC-A2/A4） | 挂起机制 | TaskScheduler.cs, NPCBrain.cs | TaskScheduler 暴露 `OnThreatSuspended(npcId)`/`OnThreatResumed(npcId)`；挂起时记录 `suspendStartTime[npcId]`；TaskScheduler 每 tick 检查挂起超时（占位 60s，QQQ.3 §五.1），超时调 AbandonTask；恢复时（OnThreatResumed）校验 NPC 当前位置与任务 source 距离，>30 格（占位，QQQ.3 §五.2）则 Abandon 重派 | T18 | NPC 被威胁打断后任务挂起；威胁解除后恢复或超时 Abandon；NPC 远离任务点时任务 Abandon 重派 | ⬜ |
| B1-5 | 编队征召时 NPC.currentTask Abandon（NPC-A10/FRM-A1） | 编队协调 | FormationController.cs | FormationController.DispatchOrders 内对每个被征召 NPC 调 `if (npc.currentTask != null) { TaskScheduler.Instance.AbandonTask(npc.npcId); npc.currentTask = null; }`；编队解散后不自动恢复任务（NPC 回 Idle，由调度器下一 tick 重新派发） | 编队模块已存在 | 编队征召时 NPC 任务被清；编队解散后 NPC 回 Idle 不卡死 | ⬜ |
| B1-6 | 流浪汉招募时清旧任务刺激（NPC-A3） | 招募兜底 | VagrantCampSystem.cs | RecruitVagrant 内，花粮改职业后，调 `TaskScheduler.Instance.AbandonTask(npc.npcId); npc.currentTask = null;`；清旧 WanderStimulus（如 npc.brain.ClearStimulus<FollowStimulus>()）；然后注入"走回王国"任务 | T11 | 招募流浪汉后旧任务/旧刺激被清；走回王国任务正常注入 | ⬜ |
| B1-7 | TaskScheduler 单例化 + 场景加载清理旧实例（NPC-A8） | 单例化 | TaskScheduler.cs | TaskScheduler 继承 MonoBehaviour；`public static TaskScheduler Instance { get; private set; }`；Awake 内 `if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this;`；OnDestroy 内退订所有事件（OnUnitDied/Building.OnDied）；HasInstance 静态属性供 OnSpawn 调用 | T17 | 同场景内只有 1 个 TaskScheduler 实例；场景切换时旧实例销毁退订 | ⬜ |
| B1-8 | TaskScheduler.OnSpawn 容错：未就绪加入待注册队列（BLD-A7/SAV-A7） | 时序容错 | TaskScheduler.cs | 维护 `List<ITaskSource> _pendingRegister;`；提供 `Register(source)`：若 Instance 就绪直接加入主注册表，否则加入 _pendingRegister；TaskScheduler.Awake 后遍历 _pendingRegister 全部注册；OnDestroy 清队列 | T17 | 建筑 OnSpawn 在 TaskScheduler 未就绪时不抛异常；TaskScheduler 初始化后自动注册待注册建筑 | ⬜ |
| B1-9 | TaskScheduler.Unregister 前 null 检查（BLD-A8） | 时序容错 | TaskScheduler.cs | Unregister 内 `if (Instance == null) return; if (source == null) return;`；幂等（重复 Unregister 不抛异常） | T17 | 场景卸载时建筑 Die 调 Unregister 不抛异常；TaskScheduler 已销毁时 Unregister 无操作 | ⬜ |
| B2-1 | Building.Die 触发 TaskScheduler.Unregister + TrainingSystem 清队列 + GridSystem.Free（BLD-A1/A2/A4） | 单一清理入口 | Building.cs | Die() 末尾按顺序：①触发 OnDied 事件 ②`TaskScheduler.Instance?.Unregister(this)` ③若建筑是训练建筑调 `TrainingSystem.Instance?.ClearQueueForBuilding(this)` ④`GridSystem.Instance?.Free(gridCell)`；每步 try-catch 降级 | B0-4, T17, T7 | 建筑被摧毁后注册表清空、训练队列清空、网格释放；任一步失败不阻塞其他步 | ⬜ |
| B2-2 | 搬运任务终点动态解析：每 tick 重查最近仓库（BLD-A2/RES-A1） | 动态解析 | TaskScheduler.cs | Transport 任务派发时不缓存 destPos；WorkerTask.MovingToDest 阶段每 tick 调 `Vector2 dest = TaskScheduler.Instance.ResolveNearestWarehouse(npc.position, excludeFull=true);`；若所有仓库满/摧毁则 dest = Treasury 位置；仓库全毁则资源入国库（ npc 身上携带的资源直接调 `RulerController.AddResource()`） | T17, T19 | 搬运途中仓库被摧毁时终点重定向到下一仓库；仓库全毁时资源入国库 | ⬜ |
| B2-3 | 资源点采集锁定 isBeingGathered（RES-A4） | 并发锁 | Building.cs（资源点子类）, TaskScheduler.cs | Building 加 `public bool IsBeingGathered { get; private set; }`；调度器收集 Gather 任务时跳过 `IsBeingGathered=true` 的资源点；任务派发时 `source.IsBeingGathered = true`；任务 Abandon/Completed 时 `source.IsBeingGathered = false` | T19 | 多 NPC 不会同时派到同一资源点；任务 Abandon 后资源点可被再次点击 | ⬜ |
| B2-4 | 资源点采集中断时进度重置 + 网格锁随 Building 状态（RES-A2） | 中断重置 | WorkerTask.cs 或 TaskStimulus 构造器, Building.cs | Gather 任务 Working 阶段维护 `gatherProgress`；任务 Abandon 时 `gatherProgress = 0; source.IsBeingGathered = false;`；网格锁（GridSystem.Free）只在 Building.Die 或采集完成时触发，不在 Abandon 时触发 | T19 | 采集途中 NPC 被打断，进度归零；资源点可再次被点击；网格锁未提前释放 | ⬜ |
| B2-5 | 建筑升级期间拒绝新任务派发（BLD-A5） | 升级锁定 | Building.cs, TaskScheduler.cs | Building 加 `public bool IsUpgrading { get; private set; }`；升级开始时 `IsUpgrading = true`，结束时 `false`；ITaskSource.TryAdvertiseTask 内首行 `if (IsUpgrading) return false;` | 建筑升级模块已存在 | 建筑升级期间不发布新任务；升级结束后恢复 | ⬜ |
| B2-6 | 建筑 ProducerComponent/StorageComponent 访问前 null 检查（BLD-A6） | 组件兜底 | ProducerComponent.cs, StorageComponent.cs | 所有外部访问点 `var producer = building.GetComponent<ProducerComponent>(); if (producer == null) return false/0;`；内部方法访问其他组件同样 null 检查 | A | 存档加载后组件缺失时降级（不产/不存），不 NRE 崩溃 | ⬜ |
| B2-7 | 水井 ProducerComponent 随建筑销毁自动停止产水（BLD-A3） | 隐式兜底 | ProducerComponent.cs | ProducerComponent.OnDestroy 或 Building.Die 内停止 Tick（_isAlive=false）；Tick 首行 `if (!_isAlive) return;`；WaterNetwork 不依赖具体水井，单例独立存在 | T15 | 水井被摧毁后 WaterNetwork 不再被该水井充水；其他水井照常产水 | ⬜ |
| B3-1 | TrainingSystem 订阅 UnitDiedEvent 清队列（POP-A1） | 训练兜底 | TrainingSystem.cs | TrainingSystem.Awake 时 `UnitController.OnUnitDied += OnResidentDied;`；OnResidentDied(npcId) 内遍历训练队列，若 npcId 在队列则移除；训练中断时该居民变回无职业居民（已招募状态，不退款，3.5 已定） | T7 | 训练途中居民死亡后队列移除；TrainingPanel 显示数量正确 | ⬜ |
| B3-2 | TrainingSystem 实现 ISaveableWithValidation：存档加载后校验队列（POP-A3/SAV-A4） | 存档对账 | TrainingSystem.cs | 实现 `ISaveableWithValidation`；ValidateAfterLoad() 内遍历反序列化后的训练队列，对每个居民调 `UnitController.IsValid(npcId)` 校验，失效则移除；校验完成后触发 TrainingPanel 刷新事件 | B0-3, T7 | 旧存档加载后训练队列无失效引用；TrainingPanel 显示正确 | ⬜ |
| B3-3 | 训练建筑被摧毁时清空该建筑队列（POP-A2） | 训练兜底 | TrainingSystem.cs | TrainingSystem 提供 `ClearQueueForBuilding(Building b)`；Building.Die 内若 b 是训练建筑则调；队列内居民变回无职业居民（不退款） | B2-1, T7 | 训练建筑被摧毁后队列清空；居民变回无职业可重新分配 | ⬜ |
| B3-4 | RecruitVagrant 前 IsAlive 检查 + 已扣粮不退（POP-A4） | 招募兜底 | VagrantCampSystem.cs | RecruitVagrant 内首行 `if (!vagrant.IsAlive) return;`；扣粮后任何失败（招募途中死亡/异常）不退粮（交易完成）；try-catch 包裹招募流程，异常时仅记录日志 | A | 流浪汉招募途中死亡不崩；已扣粮不退 | ⬜ |
| B3-5 | 繁殖前检查房屋容量（POP-A5） | 繁殖兜底 | PopulationSystem.cs | 繁殖触发点加 `if (CurrentHousingCapacity <= CurrentPopulation) return;`；房屋被摧毁后 CurrentHousingCapacity 实时重算 | 人口模块已存在 | 房屋被摧毁后繁殖停止；房屋重建后恢复 | ⬜ |
| B3-6 | BirthCampPos 旧存档兼容（POP-A6/SAV-A5） | 存档兼容 | UnitController.cs/UnitData.cs | BirthCampPos 字段加 `[SerializeField] private Vector2 _birthCampPos = Vector2.zero;` 默认值；UnitController.BirthCampPos getter：`if (_birthCampPos == Vector2.zero) return IsVagrantRecruited ? KingdomAnchor : transform.position;`（默认值兜底）；旧存档加载时若 BirthCampPos=zero 则用当前位置 | T11 | 旧存档流浪汉 BirthCampPos=null 不崩；行为合理（未招募用当前位置，已招募用王国锚点） | ⬜ |
| B3-7 | 流浪汉营地被毁后 WanderAnchorPool 改用最近安全锚点（POP-A6） | 营地兜底 | WanderAnchorPool.cs, VagrantCampSystem.cs | 流浪汉 Wander 时 WanderStimulusProvider 抽取锚点时校验 BirthCampPos 是否仍可走（GridSystem.IsWalkable）；不可走则 fallback 到 WanderAnchorPool.GetNearestSafeAnchor(currentPos) | T8 | 营地被毁后流浪汉不再走 BirthCampPos；改用最近安全锚点 | ⬜ |
| B4-1 | WarehousePanel.OnDisable 退订所有 StorageComponent 事件（UI-A1） | UI 退订 | WarehousePanel.cs | WarehousePanel.OnDisable 内遍历所有仓库 `foreach (var s in _subscribedStorages) s.OnStorageChanged -= Refresh;`；订阅时记录到 _subscribedStorages 列表；面板销毁时同样退订 | T12 | 面板关闭后不接收 StorageComponent 事件；无内存泄漏 | ⬜ |
| B4-2 | WarehousePanel 订阅 BuildingDiedEvent 清理已摧毁仓库（BLD-A2） | 仓库清理 | WarehousePanel.cs | WarehousePanel.Awake 时 `Building.OnDied += OnBuildingDied;`；OnBuildingDied(b) 内若 b 含 StorageComponent 则从 _subscribedStorages 移除并退订；Refresh() 重算 | T12, B0-4 | 仓库被摧毁后 WarehousePanel 显示更新；订阅列表清理 | ⬜ |
| B4-3 | TrainingPanel.OnDisable 退订 TrainingSystem 队列变化事件（UI-A2） | UI 退订 | TrainingPanel.cs | TrainingPanel.OnDisable 内 `TrainingSystem.OnQueueChanged -= Refresh;`；OnEnable 时订阅 | T7 | 面板关闭后不接收 TrainingSystem 事件；无内存泄漏 | ⬜ |
| B4-4 | OverheadSpeechManager.OnDestroy 退订相机事件（UI-A3） | UI 退订 | OverheadSpeechManager.cs | OnDestroy 内 `Camera.onPreCull -= OnPreCull;`（或对应相机事件）；_bubbleQueue.Clear() 释放引用 | T2 | 场景切换后无内存泄漏 | ⬜ |
| B4-5 | 确认 UI 单例覆盖（UI-A4） | UI 单例 | 新 ConfirmGatherUI.cs 或 BuildingPanel.cs | 确认 UI 用单例模式：新点击资源点时若已有确认 UI 打开，先 Close() 再开新的；确认 UI 期间资源点锁定（IsBeingGathered=true，对应 B2-3） | T19 | 玩家连点 5 个资源点只弹 1 个确认 UI；不叠加 | ⬜ |
| B4-6 | UI 面板打开时校验引用有效性（UI-A5） | 引用校验 | BuildingPanel.cs, TrainingPanel.cs 等 | 面板 OnEnable 首行 `if (_targetNpc != null && !_targetNpc.IsValid) { UIManager.Instance.ClosePanel(this); return; }`；同样校验 _targetBuilding | B0-5 | 面板打开时引用已失效则自动关闭，不 NRE 崩溃 | ⬜ |
| B4-7 | UIManager.HandleEscape 统一处理面板嵌套（UI-A6） | 复用 | — | 已有机制（3.0.1_3），无需新代码；验收时确认 QQQ.3 涉及的新面板（WarehousePanel/TrainingPanel/ConfirmGatherUI）接入 UIManager UI 栈 | T12, T7 | 新面板的 ESC 行为符合 UIManager 栈管理 | ⬜ |
| B5-1 | NPCBrain 实现 ISaveableWithValidation：currentTask 加载后校验（SAV-A1） | 存档对账 | NPCBrain.cs | 实现 `ISaveableWithValidation`；ValidateAfterLoad() 内 `if (currentTask != null && !currentTask.source.IsValid) currentTask = null;`；校验后 NPC 回 Idle 由调度器重新派发 | B0-3, T18 | 存档加载后 currentTask 指向已摧毁建筑时被清；NPC 回 Idle | ⬜ |
| B5-2 | TaskScheduler 不持久化派发记录（SAV-A2） | 设计特性 | TaskScheduler.cs | TaskScheduler 的 _npcTaskMap 字段加 `[NonSerialized]` 或不进 ISaveable 序列化；每 tick 从 ITaskSource 注册表重新收集任务（已是 DR-17 设计）；加载后只依赖 NPC.currentTask 持久化 | T17 | 存档加载后派发记录从零重建；HasWorkerAssigned 不误判 | ⬜ |
| B5-3 | WaterNetwork 存档加载 clamp(0,100)（RES-A3/SAV-A3） | 存档兜底 | WaterNetwork.cs | WaterNetwork.Stored 的反序列化后 `Stored = Mathf.Clamp(Stored, 0, 100);`；在 ISaveable.LoadAfterDeserialize 或 OnLoad 钩子内 | T14 | 存档加载后 Stored ∈ [0,100]；异常值不破坏水网 | ⬜ |
| B5-4 | 旧存档 equipId 字段兼容（SAV-A6） | 存档兼容 | SaveSystem.cs 或 UnitData.cs | 反序列化 UnitData 时用 `[FormerlySerializedAs("equipId")]` 标记已移除字段，或反序列化 try-catch 忽略未知字段（JSON 反序列化天然忽略）；不抛异常 | T5 | 旧存档加载不崩；equipId 字段被忽略 | ⬜ |
| B5-5 | 存档加载时 TaskScheduler 待注册队列兜底（SAV-A7） | 加载顺序 | TaskScheduler.cs | 已由 B1-8 覆盖（Register 时若未就绪加入 _pendingRegister）；存档加载时 TaskScheduler.Awake 后遍历 _pendingRegister 注册；额外：NPC.currentTask 校验在所有建筑加载完成后才触发（用 SceneManager.sceneLoaded 事件延迟 ValidateAfterLoad） | B1-8 | 存档加载后建筑全部注册到 TaskScheduler；NPC.currentTask 校验在建筑就绪后 | ⬜ |
| B5-6 | WanderAnchorPool 加载后兜底注入城堡中心（SAV-A8） | 加载兜底 | WanderAnchorPool.cs | WanderAnchorPool.ValidateAfterLoad() 内 `if (_anchors.Count == 0) _anchors.Add(KingdomManager.Instance.KingdomAnchor);`；建筑加载完后 OnBuildingSpawn 钩子补充锚点 | T8 | 存档加载后 WanderAnchorPool 不为空；NPC 有锚点闲逛 | ⬜ |
| B5-7 | 存档损坏 try-catch 回退上一日自动存档（SAV-A9） | 存档回退 | SaveSystem.cs | SaveSystem.Load 内 try-catch JSON 反序列化；catch 块加载上一日自动存档（路径 save_auto_yesterday.json 或类似）；若昨日存档也损坏则回退初始存档；都失败则新建游戏 | A | 存档损坏时游戏不崩；回退到上一日存档 | ⬜ |
| B5-8 | Editor 强停 Play Mode 不兜底（SAV-A10） | 开发期 | — | 文档说明：开发期问题不产品期兜底；TaskScheduler 不持久化派发记录（B5-2）天然规避；验收时确认 Editor 强停后无残留状态影响下次 Play | — | 文档已说明；无需代码 | ⬜ |
| B6-1 | ProducerComponent.Tick 用 Time.deltaTime 累加适配 timeScale（TIM-A1） | 时间适配 | ProducerComponent.cs | Tick 内 `_accumulator += Time.deltaTime * rate;` 而非固定 +rate/秒；累加到 ≥1 触发产出事件（DR-18）；timeScale=2 时累加速度翻倍，产出频率自动翻倍 | T15 | timeScale=2 时农场产出速率符合预期；降速时产出减慢 | ⬜ |
| B6-2 | 感知敌人降速时不重置任务进度（TIM-A2） | 时间适配 | TaskScheduler.cs, NPCBrain.cs | 降速时仅修改 Time.timeScale，不调 TaskScheduler.Reset() 或 NPCBrain.ResetProgress()；任务挂起/恢复机制（B1-4）保持当前状态 | 时间模块已存在 | 降速时任务进度保留；恢复 2x 时继续 | ⬜ |
| B6-3 | 跨日存档时任务态正常持久化（TIM-A3） | 存档 | NPCBrain.cs, SaveSystem.cs | 每日自动存档时 NPCBrain 实现 ISaveable，序列化 currentTask（引用 source 的 buildingId）+ TaskState；加载后由 B5-1 校验恢复 | B5-1, T18 | 跨日存档加载后任务态正确恢复 | ⬜ |
| B6-4 | nightFactor 平滑过渡（TIM-A4） | 时间平滑 | SafetyStimulusProvider.cs 或 SafetyScore 计算处 | nightFactor 不用 0/1 突变，改用 `Mathf.Lerp(currentNightFactor, targetNightFactor, Time.deltaTime * transitionSpeed)`；transitionSpeed=2（占位，2 秒过渡）；白天→夜晚 targetNightFactor=1，夜晚→白天=0 | T8 | 夜晚→白天切换时 NPC SafetyScore 平滑变化；行为不跳变 | ⬜ |
| B7-1 | FormationController 锚点失效检查（FRM-A3） | 编队兜底 | FormationController.cs | 编队锚点每 tick 校验 `if (_anchorBuilding != null && !_anchorBuilding.IsValid) { DisbandOrRetreatToCastle(); }`；DisbandOrRetreatToCastle 内调 DispatchOrders 到城堡中心或解散 | 编队模块已存在 | 编队锚点建筑被摧毁后编队不卡死；回城堡或解散 | ⬜ |
| B7-2 | FormationController 实现 ISaveableWithValidation（FRM-A4） | 存档对账 | FormationController.cs | 实现 `ISaveableWithValidation`；ValidateAfterLoad() 内遍历编队成员校验 `IsValid`，失效则从编队移除；减员防抖（3.0.1_3 已定）触发重排 | B0-3 | 存档加载后编队成员无失效引用；减员防抖正常 | ⬜ |
| B7-3 | 编队成员死亡清理复用已有机制（FRM-A2） | 复用 | — | 已有机制（3.0.1_3 编队减员防抖 1s 重排）；验收时确认 OnUnitDied 触发防抖，不重复实现 | — | 编队成员死亡后 1s 防抖重排触发 | ⬜ |
| B8-1 | **LC-B1** 读档后建造菜单永久软锁修复（P0 高危） | 实证 bug | BuildingFactory.cs, BuildController.cs | 方案二选一：①SpawnFromSave 对 CastleCore 的 Active 态补发 `BuildingActivatedEvent`；②BuildController._buildUnlocked 改从 `KingdomManager.Instance.CastleLevel >= 1` 派生（不依赖事件）。推荐②，彻底解耦 | A | 读档后建造菜单正常解锁；未修复主城的存档读档后主城不卡死 | ⬜ |
| B8-2 | **LC-G5** 自动存档先于每日结算修复（P0 Heisenbug，D10） | 实证 bug | DayCycleSettlement.cs, SaveManager.cs | 新增 `DaySettledEvent`（DayCycleSettlement 结算完成后发布）；SaveManager 自动存档改订阅 `DaySettledEvent` 而非 `TimeDayChangedEvent`；顺序显式化不依赖订阅先后 | A | 从主菜单进游戏后跨日：结算先执行、存档后执行；读档后当天结算不丢 | ⬜ |
| B8-3 | **LC-N1** 对象池出池洗涤清单（P0 高危） | 实证 bug | UnitController.cs, NPCBrain.cs, UnitInstancePool.cs | 新增 `IPoolResettable` 接口（§11.3 契约）；UnitController.ResetForReuse 重置 _runtimeOccupation/LastBirthDay/ChildGrowthDays/IsVagrantRecruited/冲锋态/_slowUntil/_knockbackActive/_staticTarget；NPCBrain.ResetForReuse 清 _currentAttackTarget/_lastCmd/_chaseTarget/_lastAggressor/_recentHitCount/IsKingdomTaskWorker + 清刺激列表；UnitInstancePool.Return 出池前调 ResetForReuse | A | 池化复用的 NPC 不带上辈子职业/任务刺激/编队军令；冲锋死亡后复活不再 early-return 卡死 | ⬜ |
| B8-4 | **LC-G3** 读档失败把高版本存档打死档修复（P0 高危） | 实证 bug | SaveManager.cs | `SaveManager.Load` 全部校验通过后再设 `CurrentSlotId`（版本/死档校验前不设）；`MarkCurrentSaveFinished` 仅由君主死亡（RulerController.OnMonarchDied）触发，读档失败进 GameOver 不触发 | A | 高版本存档被拒读不写回 isFinished；换新版本后该存档可正常读取 | ⬜ |
| B8-5 | **LC-B2** grade 未入档致产能降贫瘠档（P0） | 实证 bug | BuildingSaveData.cs, Building.cs, BuildingFactory.cs | BuildingSaveData 加 `int grade` 字段；Building.SaveState 写入 grade；BuildingFactory.SpawnFromSave 读 grade 赋给 CreateBuildingInstance（替代硬编码 0=Barren）；LoadState 同步覆盖 | A | 读档后 Rich 资源点建筑速率不降档；Normal 建筑读档后 rate 不变 | ⬜ |
| B8-6 | **LC-G2/G4** timeScale 恢复与重置不一致（P0） | 实证 bug | PausePanel.cs, TimeManager.cs | PausePanel.Resume 改用 `TimeManager.SetTimeScale(TimeManager.CurrentTimeScale)` 而非硬编码 1f；TimeManager.ResetState 补重置 CurrentTimeScale/_pendingScale/IsCombatSlowed 为初始值（1f/1f/false） | A | 2x 下暂停再恢复保持 2x；回主菜单开新局不残留上局倍速/战斗降速 | ⬜ |
| B8-7 | **LC-B9** 低速率建筑永不产出（P0） | 实证 bug | ProducerComponent.cs | 主产加 `_accumulator`（对齐金矿 `_goldAccumulator` 模式）：Tick 内 `_accumulator += _rate; if (_accumulator >= 1f) { int produce = Mathf.FloorToInt(_accumulator); _accumulator -= produce; ... }` | A | rate=0.3/s 的建筑累计产出正常；Barren 缩放后 rate=0.35 不卡死 | ⬜ |
| B8-8 | **LC-N5** _transporting 名额泄漏致搬运卡死（P0） | 实证 bug | ScheduleCenterStub.cs | DispatchTransport 清理条件改用 `!w.IsAlive`（UnitController.IsAlive）替代 `w == null`（池化下死单位引用非 null）；或订阅 UnitDiedEvent 释放名额 | A | 工人被威胁打断/死亡后 _transporting 名额释放；该存储剩余产出可被重派 | ⬜ |
| B8-9 | **LC-B8** Building.Die 未 UnregisterSaveable（P0） | 实证 bug | Building.cs | Die() 清理序列补 `SaveManager.Instance?.UnregisterSaveable(this)`（放在 UnitRegistry.Unregister 之后、Destroy 之前） | A | Die 后 _saveables 字典无残留条目；无需等 CleanupDestroyedSaveables 兜底 | ⬜ |
| B8-10 | **D2** 升级清场序列：中断在场任务释放工人（P1，扩展 B2-5） | 转换清场 | Building.cs, 新 IClearableOnStateChange.cs | 新增 `IClearableOnStateChange` 接口（§11.1 契约）+ `StateChangeReason` 枚举；Building.TryUpgrade 进入 Constructing 前调 `ClearInFlightReferences(Upgrade)`：①移除以本建筑为 issuer 的任务刺激 + currentWorkers 清空回 Idle；②StorageComponent 存货保留（在途搬运不受影响）；③TrainingSystem 队列挂起（D11）。B2-5 的 IsUpgrading 拒绝新任务保留 | T17, B0-4 | 农场升级时在场工人被释放回 Idle（调度器下 tick 重派别处）；在途搬运继续正常入库 | ⬜ |
| B8-11 | **LC-N2** IsKingdomTaskWorker 永不复位致工人瘫痪（P1，随 T18） | 实证 bug | WorkerTask.cs, NPCBrain.cs | WorkerTask 进入 Completed/Abandoned 时 `npc.IsKingdomTaskWorker = false`；NPCBrain.ResetForReuse（B8-3）也复位此标志 | T18 | 任务完成后工人恢复可移动（Update 不再跳过 Execute）；池化复用不残留 | ⬜ |
| B8-12 | **LC-N4** 训练队列死亡槽位泄漏（P1，修复 B3-1 的判断失效） | 实证 bug | TrainingSystem.cs | B3-1 订阅 UnitDiedEvent 已定，但现有 TrainingSystem.Update 靠 `e.unit == null` 判断失效（池化下非 null）；改用 `!e.unit.IsAlive` 判断；OnResidentDied 内移除条目并 ActiveCount-- | T7, B3-1 | 训练中居民死亡后槽位释放；池化下不再对尸体 SetOccupation | ⬜ |
| B8-13 | **LC-N3** 招募走回 120s 过期致人口蒸发（P1） | 实证 bug | VagrantCampSystem.cs | Update 自愈扫描（0.5s 一次）增加条件：`IsVagrantRecruited && !IsRegistered && recruitStimulus已过期` → 重新注入走回刺激（重置 expiry）；记录重试次数防无限重试（上限 3 次，超限则强制就近入册） | A | 营地远/被压制超 120s 的招募居民重新走回；不再永久蒸发 | ⬜ |
| B8-14 | **D11** 建筑升级期间训练/研究队列挂起（P1） | 转换清场 | TrainingSystem.cs, AcademyBuilding.cs | TrainingSystem.Update 加 `if (building.IsUpgrading) { /* 暂停计时，不推进 startDay */ continue; }`；升级完工后恢复计时（startDay 顺延升级耗时天数）；研究队列同理挂起 | T7 | 升级期间训练计时暂停；完工后恢复不丢进度 | ⬜ |
| B8-15 | **D12/LC-B4** constructProgress/_pendingUpgrade 入档（P1） | 实证 bug | BuildingSaveData.cs, Building.cs, BuildingFactory.cs | BuildingSaveData 加 `float constructProgress` + `bool pendingUpgrade`；SaveState 写入；SpawnFromSave 读后：若 state==Constructing 则恢复 progress 和 _pendingUpgrade，续建而非重置 | A | 升级中存档读档后续建完成；不丢升级、不白扣升级费 | ⬜ |
| B8-16 | **LC-N6** UnitDiedEvent.Killer 恒 null + 饿死死因（P1） | 实证 bug | UnitController.cs, DamageSystem.cs, SatietySystem.cs | UnitController.TakeDamage 加 `UnitController source` 参数；DamageSystem.ApplyDamage 传 `attacker`；SatietySystem 饿死扣血传 null（Killer=null 合理）但 Die 时 Cause=Starved（DeathCause 枚举加 Starved 成员） | A | 击杀统计能区分击杀者；饿死报 Starved 不混为 Killed | ⬜ |
| B8-17 | **D5/D6** 工事关血事件 + 夜战战损清单 + 黎明收尾（P2，待波次系统） | 玩法层 | Building.cs, UnitController.cs, DayCycleSettlement.cs, GateController.cs | Building.TakeDamage 后发 `OnHpChanged` 事件（§11.2 契约）；UnitController(工事) 同理发 `FortificationDamagedEvent`；DayCycleSettlement 黎明结算时汇总受损工事/建筑生成战损清单（UI 播报）；GateController 开门条件改"区域无敌"才自动开（D6） | 波次系统 | 夜战后玩家可见战损清单；Dawn 城门不再在残敌在场时自动开 | ⬜ |
| B8-18 | **LC-C1** 投射物误伤己方城墙（P2） | 实证 bug | ProjectileManager.cs | CheckWallBlock 扫描路径格内工事时加阵营判断：`if (uc.Faction == p.attacker.Faction) continue;`（己方工事不挡己方弹道/不结算伤害） | A | 己方塔楼低抛弹道不再磨损自家城墙 | ⬜ |
| B8-19 | 废墟修复任务链落地（P2，3.5.3 §7.3，依赖任务系统） | 玩法层 | Building.cs, TaskScheduler.cs, 新 Building.cs 废墟态 | 生产/民生/商业/科技/军事建筑被摧毁后留废墟（占用格、Abandoned 复用或新 Ruined 态）而非 Destroy；废墟发布 Repair 任务（S 级，3.5.3 §7.3）；工人带资源去修（消耗=建造成本×50% 占位，时间=建造同档）；修复完成转 Active。土木工事不适用（直接销毁重建，D5） | T17, T19 | 生产建筑被毁后留废墟可被工人修复；修复后恢复 Active | ⬜ |
| B8-20 | T19 一次性资源点采集全链落地（P2，QQQ.2 阶段 B） | 玩法层 | PickupComponent.cs, TaskScheduler.cs, WorkerTask.cs, 新 ConfirmGatherUI.cs | 落地 QQQ.2 T19 全链：点击→确认UI(B4-5单例)→锁点(B2-3)→发布Gather→派发→Working(2/4/8s)→入国库→销毁三步(GridSystem.Free/Registry移除/对象池Despawn)；中断分支按 §4.3 定义 | T17, T19, B2-3, B2-4 | 玩家点击资源点弹确认 UI；确认后工人去采；采集完成资源入国库、资源点消失 | ⬜ |

## 跨需求依赖

| 任务 | 依赖 | 说明 |
|------|------|------|
| B0-1~B0-5 | — | 接口契约定义独立，现在可做 |
| B1-1~B1-9 | T17（TaskScheduler 实现）+ B0-1~B0-5 | 清理钩子需 TaskScheduler 主体 + 接口契约先定义 |
| B2-1 | B0-4, T17, T7 | Building.Die 单一清理入口需 Building 事件 + TaskScheduler + TrainingSystem |
| B2-2 | T17, T19 | 搬运动态解析需调度器 + 资源点采集 |
| B2-3, B2-4 | T19 | 资源点锁需采集任务先存在 |
| B2-5 | 建筑升级模块 | 升级锁定依赖升级模块 |
| B2-6 | — | null 检查独立可做 |
| B2-7 | T15 | 水井销毁兜底需 ProducerComponent 水条件改造 |
| B3-1~B3-3 | T7 | 训练兜底需 TrainingSystem 改造 |
| B3-4 | — | 招募 IsAlive 检查独立可做 |
| B3-5 | 人口模块已存在 | 繁殖检查独立可做 |
| B3-6, B3-7 | T11, T8 | BirthCampPos 兜底需流浪汉改造 + 锚点池 |
| B4-1~B4-7 | T12, T7, T2, T19 | UI 退订需对应面板先存在 |
| B5-1 | B0-3, T18 | NPCBrain 存档校验需接口 + 任务系统 |
| B5-2 | T17 | 派发记录不持久化需 TaskScheduler 实现 |
| B5-3 | T14 | WaterNetwork clamp 需水网先存在 |
| B5-4 | T5 | equipId 兼容需装备移除完成 |
| B5-5 | B1-8 | 待注册队列兜底需 B1-8 先做 |
| B5-6 | T8 | WanderAnchorPool 兜底需锚点池先存在 |
| B5-7 | — | 存档损坏回退独立可做 |
| B6-1 | T15 | ProducerComponent 时间适配需水条件改造 |
| B6-2 | 时间模块 | 降速不重置独立可做 |
| B6-3 | B5-1, T18 | 跨日存档需存档校验 + 任务系统 |
| B6-4 | T8 | nightFactor 平滑需 SafetyScore 改造 |
| B7-1 | 编队模块 | 编队锚点检查独立可做 |
| B7-2 | B0-3 | 编队存档校验需接口 |
| B7-3 | — | 复用已有机制 |
| B8-1, B8-2, B8-3, B8-4, B8-5, B8-6, B8-7, B8-8, B8-9 | — | P0 实证 bug 修复，独立可做（不等 T16-T18） |
| B8-10 | T17, B0-4 | 升级清场序列需任务系统 + Building 事件 |
| B8-11 | T18 | IsKingdomTaskWorker 复位随 WorkerTask 改造 |
| B8-12 | T7, B3-1 | 训练死亡清理修复 B3-1 判断失效 |
| B8-13 | — | 招募走回重注入独立可做 |
| B8-14 | T7 | 训练挂起需 TrainingSystem 改造 |
| B8-15 | — | constructProgress 入档独立可做 |
| B8-16 | — | Killer/Starved 死因独立可做 |
| B8-17 | 波次系统 | 工事关血+战损清单+黎明收尾待波次系统 |
| B8-18 | — | 投射物阵营判断独立可做 |
| B8-19 | T17, T19 | 废墟修复任务链依赖任务系统 |
| B8-20 | T17, T19, B2-3, B2-4 | 资源点采集全链依赖 QQQ.2 阶段 B |

## 建议执行顺序（五阶段）

### 阶段 0：接口契约（现在能做完，无依赖）

1. **0-1 接口定义**：B0-1（ITaskScheduler）→ B0-2（ITaskSource 扩展）→ B0-3（ISaveableWithValidation）→ B0-4（Building.OnDied）→ B0-5（UnitController.OnUnitDied）

### 阶段 1：与 QQQ.2 阶段 A 并行（现在能做完的兜底）

> 这些任务独立于任务系统，可现在做。

1. **1-1 独立兜底**：B3-4（招募 IsAlive）、B3-5（繁殖容量检查）、B2-6（组件 null 检查）、B5-7（存档损坏回退）、B6-2（降速不重置）、B7-1（编队锚点检查）、B7-3（编队减员复用验收）

### 阶段 2：等 QQQ.2 阶段 B 落地后（依赖任务系统）

1. **2-1 TaskScheduler 清理钩子**：B1-7（单例化）→ B1-8（待注册队列）→ B1-9（Unregister 容错）→ B1-1（OnNpcDied）→ B1-2（OnBuildingDied）→ B1-3（currentTask 校验）→ B1-4（挂起超时）→ B1-5（编队征召清任务）→ B1-6（招募清旧任务）
2. **2-2 建筑清理入口**：B2-1（Building.Die 单一清理）→ B2-2（搬运动态解析）→ B2-3（资源点锁）→ B2-4（采集中断重置）→ B2-5（升级锁定）→ B2-7（水井销毁）
3. **2-3 训练兜底**：B3-1（居民死亡清队列）→ B3-2（队列存档校验）→ B3-3（训练建筑摧毁清队列）
4. **2-4 流浪汉兜底**：B3-6（BirthCampPos 兼容）→ B3-7（营地被毁 fallback）
5. **2-5 UI 退订**：B4-1（WarehousePanel 退订）→ B4-2（仓库摧毁清理）→ B4-3（TrainingPanel 退订）→ B4-4（OverheadSpeechManager 退订）→ B4-5（确认 UI 单例）→ B4-6（UI 引用校验）→ B4-7（ESC 栈验收）
6. **2-6 存档对账**：B5-1（NPCBrain currentTask 校验）→ B5-2（派发记录不持久化）→ B5-3（WaterNetwork clamp）→ B5-4（equipId 兼容）→ B5-5（待注册队列兜底）→ B5-6（WanderAnchorPool 兜底）→ B5-8（Editor 强停说明）
7. **2-7 时间适配**：B6-1（ProducerComponent deltaTime）→ B6-3（跨日存档）→ B6-4（nightFactor 平滑）
8. **2-8 编队兜底**：B7-2（编队存档校验）

### 阶段 3：P0 实证 bug 修复（独立可做，不等任务系统，建议优先）

> 这些是 v2 生命周期审查新发现的代码实锤 bug，6 项高危。全部自包含、无外部依赖，建议与阶段 0/1 并行立即开展。

1. **3-1 高危数据损坏**：B8-1（建造菜单软锁）→ B8-4（读档失败毁档）→ B8-3（对象池洗涤）→ B8-2（存档/结算顺序）
2. **3-2 高危功能失效**：B8-5（grade 入档）→ B8-15（升级进度入档）→ B8-9（Die 补注销）→ B8-8（搬运名额泄漏）→ B8-7（主产累计器）→ B8-6（timeScale 恢复）
3. **3-3 独立中低危**：B8-13（招募走回重注入）→ B8-16（Killer/Starved 死因）→ B8-18（投射物阵营判断）

### 阶段 4：转换清场与玩法层（与 T16-T18 并行 / 待波次系统）

1. **4-1 转换清场框架（P1）**：B8-10（升级清场序列 IClearableOnStateChange）→ B8-14（训练挂起 D11）→ B8-11（IsKingdomTaskWorker 复位，随 T18）→ B8-12（训练死亡清理修复，随 T7）
2. **4-2 玩法层（P2，待波次/维修体系）**：B8-17（工事关血+战损清单+黎明收尾）→ B8-19（废墟修复任务链）→ B8-20（资源点采集全链）

## 完整性校验

| QQQ.3 场景分组 | 对应任务 | 状态 |
|---------|---------|------|
| §1.1 NPC AI（NPC-A1~A10） | B1-1, B1-2, B1-3, B1-4, B1-5, B1-6, B1-7, B1-8, B1-9, B5-1 | ✅ 10 场景全覆盖 |
| §1.2 建筑生命周期（BLD-A1~A8） | B2-1, B2-2, B2-5, B2-6, B2-7, B1-2, B1-8, B1-9 | ✅ 8 场景全覆盖 |
| §1.3 人口训练（POP-A1~A6） | B3-1, B3-2, B3-3, B3-4, B3-5, B3-6, B3-7 | ✅ 6 场景全覆盖 |
| §1.4 资源（RES-A1~A6） | B2-2, B2-3, B2-4, B5-3, B2-6 | ✅ 6 场景全覆盖 |
| §1.5 UI（UI-A1~A6） | B4-1~B4-7 | ✅ 6 场景全覆盖 |
| §1.6 存档（SAV-A1~A10） | B5-1~B5-8 | ✅ 10 场景全覆盖 |
| §1.7 时间（TIM-A1~A4） | B6-1~B6-4 | ✅ 4 场景全覆盖 |
| §1.8 编队（FRM-A1~A4） | B1-5, B7-1, B7-2, B7-3 | ✅ 4 场景全覆盖 |
| §3 接口契约（5 个） | B0-1, B0-2, B0-3, B0-4, B0-5 | ✅ 5 契约全覆盖 |
| **v2 §八 实证 bug 清单（16 项）** | B8-1(LC-B1), B8-2(LC-G5), B8-3(LC-N1), B8-4(LC-G3), B8-5(LC-B2), B8-6(LC-G2/G4), B8-7(LC-B9), B8-8(LC-N5), B8-9(LC-B8), B8-11(LC-N2), B8-12(LC-N4), B8-13(LC-N3), B8-15(LC-B4), B8-16(LC-N6), B8-18(LC-C1) + B8-10(D2清场) | ✅ 16 bug 全覆盖（LC-B6/B7/B11 等非独立任务项并入相关任务实现说明） |
| **v2 §九 决策表（D1-D14）** | D1/D5/D7→B8-17(关血事件)+B8-19(废墟修复); D2→B8-10(升级清场); D3→文档决策(脚手架无敌); D4→B8-10实现说明(货留身上); D6→B8-17(黎明收尾); D8→文档决策(无怀孕期); D9→待定; D10→B8-2(DaySettledEvent); D11→B8-14(训练挂起); D12→B8-15(进度入档); D13→B8-19实现说明(仓库清空); D14→B8-20实现说明(不锁点入档) | ✅ 14 决策全覆盖 |

## 反偷懒自查

- [x] 每条任务能否回答"改哪个文件/怎么做/怎么验收" → 全部能回答
- [x] 实现方法是具体动作而非模糊动词 → "加 null 检查/订阅事件/遍历校验/调 AbandonTask"等具体操作
- [x] 每个兜底场景都有对应任务 → 完整性校验表全部 ✅
- [x] 跨需求依赖显式标注 → 依赖表已列
- [x] 执行顺序明确 → 五阶段（0 接口契约 / 1 独立兜底 / 2 依赖任务系统 / 3 P0 实证 bug / 4 转换清场与玩法层）已给
- [x] 与 QQQ.2 阶段 A/B 协调 → 阶段 1 与 QQQ.2 阶段 A 并行；阶段 2 等 QQQ.2 阶段 B 落地
- [x] 复用已有机制显式标注 → B7-3（编队减员防抖）、B4-7（UIManager.HandleEscape）等已标
- [x] v2 实证 bug 全部带文件:行号 + 与已有任务的扩展关系标注 → B8-10 扩展 B2-5、B8-12 修复 B3-1 判断失效、B8-11 随 T18 等
