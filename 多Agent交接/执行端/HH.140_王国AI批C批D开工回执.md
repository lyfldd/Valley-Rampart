# HH.140 王国AI P0 批C+批D 开工回执（范围+锚点+搭车条款确认）

> 执行端（TraeCode·Unity 轨）→ 策划端
> 解锁依据：D594（HH.137/138 批B 验收成立销号+批C/批D 解锁+🔴B8 prefab 预检整改令=批C 搭车硬条款）
> 任务真源：`2_22_AI王国脑全景补全_P0实施清单.md` §三 批C（C1~C6）+ §四 批D（D1~D5）+ §八 门禁 + 2_22 主文档 §3.3/§3.7
> 取号：读 `_编号登记.md` 直取 max+1=**HH.140**（跳过 HH.139 训练师占用，D595 已销号）；完成报告=**HH.141**（同笔预留）
> 教训引用（钩子1）：L-02（落盘双验证）/L-13（编译双通道）/L-15（承接方在场性——本回执已直读）/L-17（容器正门 EnterTestRun）/L-19（摆位几何）/L-20（TryBuild=施工启动非竣工，探针涉建筑分型断言）

---

## 一、范围申报

**批C ∥ 批D 同串交付**（清单 §八：两批均只依赖批A2 威胁分布，C∥D 可并行执行端自定）。

### 批C：常设军事姿态层（C1~C6）

| 编号 | 任务 | 锚点（直读实锤） | 实施 |
|------|------|----------------|------|
| C1 | `MilitaryPostureController.cs` 三档状态机（无/警戒/动员）+态势触发升降+滞回防抖+不占焦点槽 | KingdomBrain L153-184 Tick 子步①态势重建→②剧本→③评分；姿态层挂子步③后（消费当日快照） | 纯 C# 类挂 KingdomBrain（同 FocusController 模式）；升档判据=警戒（邻接威胁非零）/动员（军力危机线 SituationConfig.crisisLine=2）；滞回双层=危机线升/recoveryLine(4)降的双线带+档位变更 hysteresisDays 天数窗 |
| C2 | `MilitaryPostureConfig` SO | SituationConfig.crisisLine/recoveryLine 批A 已落字段（L29/L32，批A 注释明示"批C 动员档判据"，全库零消费） | SO 字段=alertMinThreatWarriors（警戒触发阈值）/alertPatrolCount（警戒档巡逻目标数=频率表达）/hysteresisDays（滞回窗）/mobilizeGuardSquads（动员档守军编队目标）；危机线/恢复线**复用 SituationConfig 既有字段不重复造** |
| C3 | PatrolTaskSystem AI 侧驱动入口 | [PatrolTaskSystem.cs] StartPatrol(NPCBrain) 重载①②对任意 NPCBrain 可用；全库零调用方（玩家交互入口"归 2_13"未落地）；重载③ FindNearestPlayerBrain 硬编码 PlayerCamp 不动 | KingdomBrain 姿态执行面：警戒/动员档→对本国空闲战士（IsCombat+无编队槽，npcId 升序确定性）按主威胁方向 StartPatrol(brain, dir)；补 PatrolTaskSystem.IsPatrolling 静态查询（纯新增）；巡逻目标数=alertPatrolCount |
| C4 | GuardDeploymentSystem AI 侧自动派驻 | [GuardDeploymentSystem.cs] DeployGuardAt(GuardResourceNode) faction 可传非 PlayerCamp（L143 条件回退）；现驱动方=SelectionController 玩家右键（L167/171/175）零 AI 调用；[FormationController.cs] isGarrison+InitGarrison(wallAnchor) L153=真守城编队链，现唯一驱动方=AIDebugUIManager debug | 动员档→守城编队自动派驻：本国无 isGarrison 编队时建 FormationController（faction=Faction.AiKingdom+formationTable 既有资源）→InitGarrison(锚点=本国城墙 Building transform，无墙回退主城 transform)→RecruitStandard()（镜像 B7 将军成军 AddComponent 既有模式=L-06 生产链合规，士兵来自场景 FindIdleSoldiers 非裸构） |
| C5 | 动员档行为：集结守军+停止远程派遣+召回；被宣战硬触发器=2_18 挂点 | 集结守军=C4 链；停止远程派遣 P0 口径=StopPatrol 本国巡逻单位（⑪出征本为空桩无派遣面）；召回=巡逻兵停止后回归 AI 决策核常态 | 动员档进入时：StopPatrol 全部本国巡逻单位+建守军编队；**被宣战硬触发器=空挂点+注释指向 2_18（不实现）** |
| C6 | 危机打断两类：被攻+军覆——事件即时不等日 tick，焦点立即刷新 | [FocusController.cs] SetFocus private L152；KingdomAttackedEvent 只置 _attackedFlag 次日 Update 消费（L86-92）；军覆判定材料=GeneralDiedEvent(GameEvents L155)/FormationDisbandedEvent(L166)+GeneralCount/FormationCount 实时查询 | FocusController 新增 public `InterruptReplan(...)`（消费 _attackedFlag+跳过防抖强制重规划一次）；KingdomBrain 事件 handler（OnSituationAttacked/OnGeneralDied/OnFormationDisbanded）检测：被攻→即时打断；将军阵亡+无存活编队（军覆）→即时打断。断言口径=kingdom.focus 当日变化（执行面随次日 tick，决策当日完成） |

### 批D：AI 建筑选址打分器（D1~D5）

| 编号 | 任务 | 锚点（直读实锤） | 实施 |
|------|------|----------------|------|
| D1 | `BuildingPlacementConfig` SO | UtilityActionDef.buildingId=string（对齐 BuildingDef.id，大小写敏感：farm/Granary/Warehouse/mine） | per 建筑类型规则=buildingId+三权重 w1/w2/w3+linkBuildingId（F2 关联表）+密度惩罚参数；出厂占位 1.0；未配置 def 走 default 权重（全 1=紧凑等价现状） |
| D2 | `PlacementScorer.cs` | FindAIBuildSpot（KingdomBrain L784-809）=切比雪夫环带扫描**首格即用**；PlacementValidator.ValidatePlacement 九项校验（越界/水域/占用/资源点/地形/障碍/城门/桥/国库门） | 候选收集=环带序全量收集合法微格（**不做前 K 截断**，Validator 全继承）；score=Σwᵢ×fᵢ；argmax 严格大于（首者保留=平局固定环带序）；无候选→null（退回现行 Bump fail 明日再试路径） |
| D3 | FindAIBuildSpot 改接打分器 | ExecuteBuildFocus L764 唯一调用点；aiBuildRadius=KingdomBrainConfig L79（=8） | ExecuteBuildFocus 改调 PlacementScorer.Pick；F1 距主威胁锚（快照 Threats 升序首个非零兵国主城=方向锚实现口径，威胁≈0→F1 置 0）/F2 距最近关联建筑/F3 距主城紧凑+同类密度惩罚；原 FindAIBuildSpot 收编进 scorer 后删除（唯一调用方消亡，避免死代码） |
| D4 | 边界注记 | 打分器住 KingdomBrain 域零 AI.Core 引用；sim 无空间概念 | 15_账本登记「Unity 单侧消费」双形态注记（DZ-031 同型）+玩家建造入口/UI/校验 grep 零改动自查（报告附 grep 结果） |
| D5 | 立国选址特征匹配（D316 悬空转正，D571 裁并入） | MapGenRules.PickSpawnForTemplate L129-142 只命中 preferredClimates；KingdomPreferredFeature 枚举在场（None/RiverAdjacent/ForestDense/MineralRich/BarrenRich）；KingdomDef.preferredFeature 资产 RiverBay=1/DenseForest=2 已配、矮人（Bedrock/SnowRock）/兽人（IronHoof）=0 未配；RaceDef 已配 Elf=2/Dwarf=3/Orc=4 | 偏好带命中后补 preferredFeature 真实过滤：RiverAdjacent=候选半径内水格（River/Lake）/ForestDense=区块林木密度阈值/MineralRich=区块矿密度阈值/BarrenRich=区块开阔地（Plain）密度阈值；全失败→忽略 feature 带内回退+日志（D292 模式复用）；判定参数进 MapGenRulesConfig（SO 化禁魔法数）；四模板 KingdomDef 资产回填（矮人=MineralRich/兽人=BarrenRich，实施时按资产实名定）；**分层注记=立国选址（地图生成期）≠D1~D3 建筑选址（运行时），同批不同链** |

### B8 prefab 预检整改令搭车（D594 硬条款，本回执确认并入）

1. **评分侧门控**：`UtilityScorer.Feasible` 新增 `case UtilityAction.ProduceMachine`——本族可造机器（IsMachineAllowed 同源选型）的 **prefab 全缺失 → return false（不评）**；上限守卫镜像（GetPlacedMachineCountByKingdom < GetMachineLimit）；prefab 在场判定口径=SiegeProductionSystem 既有 prefab 探测面同源（实施时以该方法实名为准）。
2. **执行侧防御预检**：ExecuteProduceMachine 开头补同源预检（可选条款=一并落，双保险）。
3. **验收探针**：缺失族不评零扣费（精灵局 ProduceMachine 不入 ScoreTop、国库零变动）+在场族不受影响（人类局 Ballista prefab 在场可评）。
4. **施工顺带修复批B 遗留缺口（列报申报）**：实盘直读发现 `Feasible` switch（L367-452）**无 TrainGeneral/BuildBarracks/BuildTrainingCamp/BuildSiegeWorkshop/ProduceMachine 五 case → default:false → 真实运行时评分侧五行动全被挡死**（批B 探针反射直调绕过评分故 14/16 未暴露；评分域=门未开、执行域=链已通）。本批搭车一并补 case（TrainGeneral=金成本+将军上限镜像；建造三行动并入既有建造组 case 全继承三守卫），**属批B 语义补全非行为放宽**——列报请策划知悉。
5. **MachineDemand 姿态细化（L270-278 注释锚兑现）**：军事期 &&（邻接威胁非空 || 姿态≥警戒）→守城需求域（动员档无威胁也有守城需求）；需求分式不变（Clamp01(want/(want+count))×0.8）；档位经 MilitaryPostureController 静态槽消费（SituationHub 同型模式）。

---

## 二、红线承诺

1. **AI.Core 零直改**（Faction.cs 等只读引用）；
2. **玩家侧零改动**：PatrolTaskSystem/GuardDeploymentSystem/FormationController/PlacementValidator 仅纯新增查询方法或零触碰；玩家建造入口/UI/校验 grep 自查随报告；
3. **常设底线三级不动**：FocusController.Update 主流程不动（仅新增 InterruptReplan 入口+防抖旁路标志，底线判定序粮→人口→被攻不变）；
4. **同 seed 确定性**：PlacementScorer 环带固定序+argmax 严格大于+威胁锚升序+FindIdleSoldiers npcId 序；姿态档位由确定性快照派生；
5. **阶段机结构零触碰**（D590 增补节④同款）；
6. **容器纪律**：探针容器强制 TestHarnessApi.EnterTestRun 正门+ExitTestRun 收尾+协程异常捕获器（L-16/L-17）。

## 三、探针与回归计划

- 新建 `Assets/Editor/Smoke/Valley_HH140_Probe.cs`：C 面六探针（三档升降+滞回负探针/巡逻断言/驻防断言/动员召回/军覆当日重规划/B8 缺失族零扣费+五行动 Feasible 开闸）+D 面四探针（军事朝向性[Pick 返回格落威胁半平面]/经济邻近性/同 seed 选址确定性/D5 特征匹配+回退负探针）。
- 回归（含 D594 列报⑤ 补全量义务）：Smoke_5/Smoke_9/Smoke_14/2_20B + **2_20C/2_13_C 补跑**（批B 未跑正交项本批收口）。
- 已知行为变化预期：①五行动评分门打开=真局 AI 可能开始选 ⑯⑰㉔㉕（批B 语义补全，Smoke_9 断言分型挂 HH.131 件3 若现新红归因列报）；②选址从首格→打分最优格=AI 建筑坐标漂移（断言"在场"类不受影响）。

## 四、交付序

实施（C2→C1→C3~C6→B8 搭车→D1→D2→D3→D5→D4）→编译双通道（L-13）→探针容器→回归→HH.141 完成报告→commit+git-plan-sync。

执行端开工。
