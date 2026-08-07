# QQQ.2 执行清单

> 配套文档：QQQ.2_NPC任务修正以及一些小问题.md
> 生成于 2026-08-07

## 任务总表

| 编号 | 任务 | 需求# | 类型 | 涉及文件 | 验收标准 | 状态 |
|------|------|-------|------|---------|---------|------|
| T1 | OverheadSpeech 改为每单位复用气泡（DR-6：新说话先销毁旧气泡再显示新的，不叠加） | 需求1 | 架构 | IClickInteractable.cs（OverheadSpeech L85-111） | 快速连点 NPC 只显示一条气泡，不叠加 | ⬜ |
| T2 | NPC 空闲自动说话（DR-10）：IsIdleForTask+SafetyScore>0.6 时 15-30s 随机计时器；视野裁剪+同时气泡≤6 轮转队列+冷却 8s；新 OverheadSpeechManager 管控 | 需求1 | 架构 | UnitController.cs, NPCBrain.cs, 新 OverheadSpeechManager | 闲逛每隔 15-30s 冒一句；100-200 人场景视野内同时气泡≤6；战斗/任务/Caution 态不说 | ⬜ |
| T3 | 从建造菜单/BuildingMappingTable 移除 Armory 建筑 | 需求2 | 清理 | BuildingMappingTable.asset | 建造菜单不再出现装备厂 | ⬜ |
| T4 | 删除 EquipmentPanel 及 BuildingPanel 中的 Armory 装备入口 | 需求2 | 清理 | BuildingPanel.cs:391-392,482, EquipmentPanel.cs | 点建筑无装备面板，编译 0 error | ⬜ |
| T5 | 彻底移除 EquipmentDef/EquipmentSystem/EquipmentPanel 代码+资产（grep 确认 UnitController/BuildingFactory/SaveSystem/UnitData 无 equipId 残留；存档兼容性兜底） | 需求2 | 清理 | EquipmentDef.cs, EquipmentSystem.cs, EquipmentPanel.cs, *.asset | 编译 0 error，旧存档加载不崩，无残留引用 | ⬜ |
| T6 | TrainingPanel 改为显示「可训练人数 + 训练队列（职业名×数量）+ 正在训练人数」 | 需求3 | 架构 | TrainingPanel.cs | 面板三块信息，队列显示「工人×2/士兵×1」格式，不列具体 NPC | ⬜ |
| T7 | TrainingSystem 暴露可训练居民数/队列清单/进行中数量；TrainingConfig 暴露 supportedOccupations + trainDuration（DR-12：按职业分级 居民→工人 1天/→士兵 2天/→高阶 3天）；点击训练弹职业选择后从居民池自动入队 | 需求3 | 架构 | TrainingSystem.cs, TrainingConfig.cs | 点训练弹职业选择→自动取居民入队（带职业+对应时长），队满置灰 | ⬜ |
| T8 | 完整重构 NPC 空闲分布：合并 SafetyStimulus/ThreatHysteresis/Caution 为统一 SafetyScore；新增动态 WanderAnchorPool；新增 RetreatToSafeAnchor 撤退行为谱系；SafetyScore 决定 Wander 阈值/半径/回城拉力；城墙内 wallFactor +X；加模拟器验证场景 | 需求4 | 架构 | WanderStimulusProvider.cs, SafetyStimulusProvider.cs, ThreatHysteresisComponent.cs, HitCooldownStateMachine.cs, BehaviorExecutor.cs, NPCBrain.cs, GridSystem.cs, 新 WanderAnchorPool, 新 RetreatToSafeAnchorBehavior | 空闲 NPC 从动态锚点池按 SafetyScore 抽取闲逛点；边界遇敌往最近安全锚点撤退；城墙内/无城墙场景均正常 | ⬜ |
| T9 | ProducerComponent 暴露 HasWorkerAssigned，基于 TaskScheduler 派发记录判定（不写空间查询）；调度器在 NPC 中断/死亡/被招募走时清除指派（兜底见 QQQ.3） | 需求5/10 | 架构 | ProducerComponent.cs, TaskScheduler.cs | 无工人不产粮；NPC 异常退出时建筑状态正确 | ⬜ |
| T10 | farm.asset producer.rate 从 15 校准到 2 粮/秒（DR-13） | 需求5 | 参数 | farm.asset | 产出速率=2 粮/秒 | ⬜ |
| T11 | 流浪汉 HomePoint 按 IsVagrantRecruited 判定（DR-7）：未招募→营地坐标，已招募→王国锚点；VagrantCampSystem 记录 BirthCampPos 字段并持久化 | 需求6 | 架构 | SceneHomePointProvider.cs, VagrantCampSystem.cs, UnitController.cs, UnitData.cs | 未招募流浪汉在营地游荡；招募后走回王国；BirthCampPos 持久化 | ⬜ |
| T12 | 新增 WarehousePanel：顶部按钮入口+订阅刷新（DR-15）；汇总各 StorageComponent 按资源类型显示 | 需求7 | 架构 | 新 WarehousePanel, 新 WarehousePanelButton, StorageComponent.cs | 顶部"仓库"按钮打开；实时显示各资源仓库量；面板关闭退订 | ⬜ |
| T13 | Well 从民生模块 tier2 移到 tier1（一级主城可建水井） | 需求8 | 参数 | Module_Livelihood.asset, CastleUnlockTable | 一级主城可建水井 | ⬜ |
| T14 | 新增 WaterNetwork（DR-8：单例 MonoBehaviour + ISaveable，容量 100）；well.asset rate=4 水/秒（DR-14）；UI 隐藏 | 需求9/10 | 架构 | 新 WaterNetwork, well.asset | 水井 4 水/秒入网；容量上限 100 超出停产；UI 不显示水 | ⬜ |
| T15 | 农场生产条件（DR-9+DR-18）：WaterNetwork.ConsumeWater(2) 返回 true + HasWorkerAssigned=true（仅 Working 算在场 DR-19）才产；1s/tick 离散产出事件；缺水停产+头顶冒"缺水"图标 | 需求9 | 架构 | ProducerComponent.cs, farm.asset, WaterNetwork | 有水网+工人产粮（2粮/秒耗2水）；缺水停产+提示；无工人不产 | ⬜ |
| T16 | 新增 KingdomTask/ITaskSource 抽象（type/source/destType/destPos 动态解析）；ITaskSource 扩展 OnRegister/OnUnregister（DR-16） | 需求10 | 架构 | 新 KingdomTask, ITaskSource | 任务带 destType 不硬编码终点；建筑生命周期挂钩注册 | ⬜ |
| T17 | 扩展调度器（DR-17：1s/tick+引用占用+距离升序）：收集 ITaskSource 任务、按优先级+距离分派 idle NPC、动态解析终点、任务幂等靠 NPC.currentTask 引用占用 | 需求10 | 架构 | ScheduleCenterStub.cs 或新 TaskScheduler | 生产/搬运/搬水/采集任务被正确分派；同优先级按距离升序 | ⬜ |
| T18 | WorkerTask 内化为 TaskStimulus 工厂（不做独立状态机），调度器构造 TaskStimulus 扔给 NPCBrain+BehaviorExecutor 消费；ThreatStimulus 抢占沿用现有挂起/恢复；Working 占位动作态 | 需求10 | 架构 | WorkerTask.cs, TaskScheduler.cs, BehaviorExecutor.cs | NPC 走到任务点停留执行占位动作；遇敌时任务挂起、威胁解除恢复 | ⬜ |
| T19 | 一次性资源点采集生命周期（DR-11）：确认 UI→发布 Gather 任务→按资源量耗时（WoodPile 2s/StonePile 4s/OreVein 8s）→入国库→释放网格+BuildingFactory 对象池 Despawn；支持多资源点并行采集 | 需求10 | 架构 | Building.cs, BuildingFactory.cs, GridSystem.cs | 采集后资源点消失不留贴图；多资源点并行；走对象池不直接 Destroy | ⬜ |
| T20 | 定义 `enum TaskState { Assigned, MovingToSource, Working, MovingToDest, Completed, Abandoned }`（A5 缺口，§10.4 提及） | 需求10 | 枚举 | 新 TaskState.cs 或并入 KingdomTask.cs | 枚举定义编译通过；任务态字段统一引用此枚举 | ⬜ |
| T21 | TrainingConfig 字段结构定义：`supportedOccupations: List<Occupation>` + `trainDuration: Dictionary<Occupation,float>`（A4 缺口，DR-3/DR-12 落地） | 需求3 | 架构 | TrainingConfig.cs | 字段定义编译通过；TrainingPanel 可读 supportedOccupations 和 trainDuration | ⬜ |
| T22 | 生产链路端到端联调验证场景（R2 缺口）：农场有工人+水→产粮→搬运入仓→仓库面板显示 | 需求5/9/10 | 验证 | 新验证场景/CombatTestSpawner 扩展 | 农场有工人+水时产粮；产出经搬运入仓；WarehousePanel 实时显示 | ⬜ |

## 跨需求依赖

| 任务 | 依赖 | 说明 |
|------|------|------|
| T9 | T16,T17,T18,T20,QQQ.3 | 加工人在场检查需生产任务先能分派 + TaskState 枚举先定义（DR-19）；DR-4 引出的兜底机制需 QQQ.3 设计先行 |
| T10 | — | 速率校准独立 |
| T14 | — | WaterNetwork 先建 |
| T15 | T14,T9,T17 | 农场水条件需水网+生产任务 |
| T17 | T16 | 调度器需 KingdomTask 先存在 |
| T18 | T17,QQQ.3 | WorkerTask 接入需调度器先分派；任务挂起/恢复兜底需 QQQ.3 设计先行 |
| T19 | T17,QQQ.3 | 一次性资源点采集需调度器分派 Gather；销毁/锁格释放兜底需 QQQ.3 设计先行 |
| T8 | T17 | 空闲分布需与任务消费协调 |
| T11 | T8 | 流浪汉就近锚点属空闲分布方案 |
| T2 | T8 | 自动说话依赖 SafetyScore（T8 实现） |
| T7 | T21 | TrainingPanel 需读 TrainingConfig.supportedOccupations/trainDuration |
| T20 | — | 枚举定义独立 |
| T21 | — | TrainingConfig 字段定义独立 |
| T22 | T9,T15,T17,T19 | 端到端联调需生产链路全通 |

## 建议执行顺序（按"现在能做完 vs 占位等以后"两阶段）

### 阶段 A：现在能做完（无外部依赖，自包含）

> 用户诉求：优先做"现在就能做完"的任务，不占位等以后实现。

1. **A1 装备厂清理**：T3 → T4 → T5（独立，先清遗留）
2. **A2 枚举/配置定义**：T20（TaskState 枚举）→ T21（TrainingConfig 字段）
3. **A3 速率/等级校准**：T10（farm.rate）、T13（Well tier1）
4. **A4 独立新系统**：T14（WaterNetwork 单例）、T16（KingdomTask/ITaskSource 抽象定义）
5. **A5 UI 改造（自包含）**：T1（气泡复用）、T6（TrainingPanel UI）、T7（TrainingSystem，依赖 T21）、T12（WarehousePanel）
6. **A6 QQQ.3 兜底设计文档**（DR-20：场景清单+原则+接口契约粒度，不写实现细节，现在可写完）

### 阶段 B：占位等以后实现（有依赖链）

> 等 QQQ.3 设计 + 任务系统核心落地后再做。

1. **B1 任务系统核心**：T17（调度器，依赖 T16）→ T18（WorkerTask 接入，依赖 T17+QQQ.3）
2. **B2 生产链路**：T9（HasWorkerAssigned，依赖 T16/T17/T18/T20/QQQ.3）→ T15（农场水条件，依赖 T14/T9/T17）
3. **B3 资源点采集**：T19（依赖 T17+QQQ.3）
4. **B4 空闲分布/流浪汉**：T8（依赖 T17）→ T11（依赖 T8）→ T2（依赖 T8）
5. **B5 端到端验证**：T22（依赖 T9/T15/T17/T19）

## 完整性校验

| 需求# | 文档章节 | 对应任务 | 状态 |
|-------|---------|---------|------|
| 需求1 | §需求1 对话优化 | T1, T2 | ✅ 2任务覆盖 |
| 需求2 | §需求2 移除装备厂 | T3, T4, T5 | ✅ 3任务覆盖 |
| 需求3 | §需求3 训练UI | T6, T7, T21 | ✅ 3任务覆盖（含 TrainingConfig 字段定义） |
| 需求4 | §需求4 空闲分布 | T8 | ✅ 1任务覆盖 |
| 需求5 | §需求5 生产速率/工人 | T9, T10 | ✅ 2任务覆盖 |
| 需求6 | §需求6 流浪汉 | T11 | ✅ 1任务覆盖 |
| 需求7 | §需求7 仓库面板 | T12 | ✅ 1任务覆盖 |
| 需求8 | §需求8 水井等级 | T13 | ✅ 1任务覆盖 |
| 需求9 | §需求9 农场条件 | T14, T15 | ✅ 2任务覆盖 |
| 需求10 | §需求10 任务调度 | T16, T17, T18, T19, T20 | ✅ 5任务覆盖（含 TaskState 枚举） |
| 验证 | 跨需求端到端 | T22 | ✅ 1验证任务覆盖生产链路 |

> 注：T20/T21/T22 为二次审查缺口转化的架构/验证任务（A5/A4/R2 缺口），分别补强需求10（TaskState 枚举）、需求3（TrainingConfig 字段）、需求5/9/10（端到端验证）。

## 反偷懒自查

- [x] 每条任务能否回答"改哪个文件/改成什么样/怎么验收" → 全部能回答
- [x] 模糊动词清零 → 无"完善/实现/对接/优化"，全改具体动作
- [x] 每个需求都有对应任务 → 完整性校验表全部 ✅
- [x] 跨需求依赖显式标注 → 依赖表已列（含 T20/T21/T22）
- [x] 执行顺序明确 → 两阶段（A 现在可做 / B 占位等以后）已给
- [x] 批次分类明确 → 阶段A 13 任务现在可做；阶段B 9 任务等依赖
- [x] 审查缺口补全 → A1/A4/A5/R2 缺口已转 T20/T21/T22 + DR-18~DR-21 决策