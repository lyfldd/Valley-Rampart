# HH.141 王国AI P0 批C+批D 完成报告（姿态层∥选址打分器+B8 搭车整改令）

> 日期：2026-09-10 · 持有端：执行端（TraeCode·Unity 轨）
> 上游：HH.140 开工回执（D594 批B 销号+整改令解锁）· 任务真源=2_22 P0 清单 §三批C/§四批D
> 验收：策划端（实盘复核纪律）；红线=AI.Core 零直改+同 seed 确定性+玩家侧零改动+评分语义补全非放宽

---

## 一、交付构成（git 面 10 改 473+/45- + 7 组新增，另 2 资产）

### 批C 姿态层（C1~C6）

| 类 | 文件 | 内容 |
|----|------|------|
| 新增 | `Data/Kingdoms/MilitaryPostureConfig.cs` + asset | C2：alertMinThreatWarriors=1/alertPatrolCount=2/hysteresisDays=2/mobilizeGuardSquads=1（SO 四参全可调） |
| 新增 | `Systems/AI/KingdomBrain/MilitaryPostureController.cs` | C1：三档 MilitaryPosture 枚举（None/Alert/Mobilized）+PostureHub 静态槽（SituationHub 同型）+Evaluate 档位转移表+**双层滞回**（危机线/恢复线双线带+hysteresisDays 天数窗，升降均受窗约束） |
| 改 | `Systems/AI/KingdomBrain/KingdomBrain.cs` | C3：ExecuteAlertPatrol（补巡逻 npcId 升序+ResolveMainThreatDirection 威胁朝向）；C4：ExecuteMobilize→TrySpawnGarrisonSquad（AddComponent+InitGarrison+RecruitStandard，锚=本国工事 IsFortification 回退主城）；C5：StopAllOwnPatrols+CountOwnGarrisonSquads；Tick 子步⑥ Fine 态执行+Unsubscribe 随 PostureHub.Remove |
| 改 | `Systems/AI/KingdomBrain/FocusController.cs` | C6：InterruptReplan（_interruptPending 置位→Update 消费→防抖旁路一次）；KingdomBrain 三事件 handler（OnSituationAttacked/OnGeneralDied/OnFormationDisbanded）即时打断；军覆判定=CountGenerals==0&&CountOwnFormations==0（实时查询与快照解耦） |
| 改 | `Systems/AI/PatrolTaskSystem.cs` (+12) | C3 配套：IsPatrolling(brain) 纯新增查询（零行为变化） |

### 批D 选址打分器（D1~D5）

| 类 | 文件 | 内容 |
|----|------|------|
| 新增 | `Data/Kingdoms/BuildingPlacementConfig.cs` + asset | D1：PlacementRule 嵌套（buildingId/w1ThreatFront/w2LinkBuilding/w3CastleCompact/linkBuildingId/sameTypeDensityPenalty）+8 规则（军事五类 w1=1+密度 0.5；farm→Granary/w2=1；mine→Warehouse/w2=1）+densityRadiusCells=5 |
| 新增 | `Systems/AI/KingdomBrain/PlacementScorer.cs` | D2/D3：TryPick（主城锚定切比雪夫环带全量收集不做前 K 截断+PlacementValidator 九项全继承+argmax 严格大于平局取首=固定环带序）；三特征 F1 距威胁锚（快照 Threats 首个非零兵国主城；威胁≈0 置 0）/F2 距最近关联建筑/F3 距主城紧凑−同类密度惩罚；**r3 坐标系统一**（候选=sub 域、锚点 cell→sub 域转换、密度统计 spot 转 cell 域格距、归一分母 maxR×div） |
| 改 | `KingdomBrain.ExecuteBuildFocus`（KingdomBrain.cs 内） | D3：FindAIBuildSpot 改接 PlacementScorer.TryPick+打分分解日志；FindAIBuildSpot 转兼容壳（批B HH.137 探针反射依赖保留） |
| 改 | `Data/MapGenRulesConfig.cs`+`Systems/World/MapGenRules.cs` (+86) | D5：KingdomPreferredFeature 四参 SO（featureScanRadius=8/森林 0.10/矿 0.05/开阔 0.60）+PickSpawnForTemplate 两轮结构（带内特征匹配→回退忽略特征+日志）+MatchesPreferredFeature/ChunkFeatureRatio（RiverAdjacent=半径水格；其余=16×16 大区块占比达阈值） |

### B8 搭车（D594 整改令硬条款）

| 类 | 文件 | 内容 |
|----|------|------|
| 改 | `Systems/AI/KingdomBrain/UtilityScorer.cs` | **D594 整改令兑现**：①ProduceMachine case 三重门控=厂前置+上限镜像+**prefab 预检（本族可造机器逐台 MachinePanel.IsPrefabMissing 全缺→Feasible=false，D582② 同源）**；②批B 遗留缺口补齐=Feasible 缺 TrainGeneral/BuildBarracks/BuildTrainingCamp/BuildSiegeWorkshop/ProduceMachine 五 case→default:false 评分侧全挡死（批B 探针反射直调绕过评分未暴露）——搭车补齐属语义补全非行为放宽（详见列报1）；③MachineDemand 姿态域细化（None 档+无威胁→0 分，PostureHub 消费）；④TrainGeneral case 金成本镜像 |

### 玩家侧零改动

建造/训练/机器入口、UI、TryBuild 玩家链均未触碰；FocusController 既有三级底线与防抖语义未变（InterruptReplan 为纯新增旁路，防抖段改造仅包一层 else-if）。

## 二、D594 整改令兑现声明

1. **评分侧门控**（整改令条款①）：本族机器 prefab 全缺失→ProduceMachine 不评/Feasible=false，IsPrefabMissing 同源（MachinePanel L316 同一判定面 UnitDataManager.GetData(Faction, occ)）——**探针 P6b 实锤**：精灵 VineCatapult 缺→不评=True+ScoreTop=BuildWall+全程零扣费。
2. **验收探针**（整改令条款②）：P6c 双向闭环=k2(矮人) 建厂 Active→内存 mock 借 Ballista prefab→Feasible 翻转 true→复原——判定面翻转行为级闭环。
3. P6a 厂前置镜像（无投掷机厂→不评）+P6d 守城需求域（None 档 0 分/Alert 档 0.800）——B8 面四探针全绿。

## 三、行为级探针实录（Logs/P1/hh140_probe.log；23/23 全绿）

- **r1=12/23**：现场诊断实锤（k1 精灵/k2 矮人/k3 兽人；Mortar/VineCatapult/Ram 全缺；k0 玩家城 def.id 非 "castle"）→SetWarriors 全集/P2 地形密度/P6c 借图翻转法/P6d scriptPhase 注入/P7 锚改 k2 修正。
- **r2=17/23**：**D594 整改令核心全绿**（P6a~d/P2a~d/P3/P4/P7c/P8）；定位两层残余——①探针 P1/P5 时序（day 间距 1<hy=2 被天数滞回挡=产品逻辑无 bug 探针时序错）；②**产品级真 bug=PlacementScorer 特征距离跨坐标系**（锚=cell 域 183,133 vs 候选=sub 域 740,528→F1/F2/F3 恒 0 打分器退化首格，P7c Score=0/投影-682 实锤）。
- **r3=21/23**：坐标系修复生效（P7b F2=0.75/P7c Score=0.75 不再退化/P1 全绿）；新暴露 **P5 产品级缺陷链**=AI_Garrison_k1×13 空壳堆积（members=0/kid=-1），链=RecruitStandard 落空→0 成员无将军→KingdomId 反推=-1→计数面永不满足→每次动员重复建壳（**与批B D594"计数面与创建面失配→无限循环"同型**）；P7a=F1 带外 clamp（k1↔k2 45 格≫归一化范围 32 sub→带内恒 0，特征退化不崩溃非坐标 bug）。
- **r4=22/23**：空壳即毁止堆积生效（Editor.log"招募落空→销毁空壳"路径实锤）；根因下探=①faction 硬设 AiKingdom vs AI 王国单位实测 faction=PlayerCamp（归属面=kingdomId 既有模型）→0 候选——修=镜像 B7 先例（TrainingSystem fc.faction=将军 Data.faction；守军无将军→ResolveKingdomFaction 取本国任一活体单位）；②显式归属 override（FormationController.SetKingdomIdOverride，向后兼容字段）——**实测抽玩家/兽人兵实锤（m=6 编队 kid=0/0/3）**。
- **r5=23/23 全绿**：P5 守军编队=1+空壳=0（三重修复闭环：faction 镜像 B7+显式归属+空壳即毁）；P7a 改纯函数级断言（ComputeF1 真实威胁锚近/远锚点 0.75>0.00）+带内 clamp 现象留痕列报；P7c 候选 122 Score=0.625 与 P7b F2=0.50 数值自洽。

关键断言摘录：P1a~f 三档+双层滞回全表；P2a~c D5 三特征正/负对照+P2d 回退日志；P3 军覆 focus 99→14 当日重规划；P6b D594 缺失族不评零扣费；P6c 借图翻转双向闭环；P7c 同 seed 双跑逐字段一致。

## 四、既有冒烟回归（六容器收口=含 D594 列报⑤ 补跑义务兑现）

| 容器 | 结果 | 备注 |
|------|------|------|
| Smoke_9（评分器） | 与批B 后形态一致（#19 存量红维持=HH.131 件3 待修；**无新增红**） | #4/#3 OK；LastTop=None 根因=popAlarm 相位路径不写 LastTop（探针断言与 D322 份额轮替交互，非评分器缺陷） |
| Smoke_5（D348） | **ALL PASS** | 军事评分域 |
| Smoke_14（抽象经济） | **ALL PASS**（P1~P6+#9/#12） | SimMode 切换账本无差+同 seed 逐字节一致 |
| 2_20B_M7（训练域六轮） | **6/6 ALL PASS** | TrainingSystem 被批B 触碰必跑项 |
| 2_20C_M8M9 批4 | **ALL PASS** | **D594 列报⑤ 补跑兑现** |
| 2_13_C（流程UI） | **ALL PASS**（P1~P7） | **D594 列报⑤ 补跑兑现**；UI 域零触碰正交验证 |

## 五、列报（5 项，归策划端裁决/知悉）

1. **【批B 遗留缺口修复申报】**：UtilityScorer.Feasible switch 缺批B 五行动 case（TrainGeneral/BuildBarracks/BuildTrainingCamp/BuildSiegeWorkshop/ProduceMachine）→default:false→**真实运行时评分侧五行动全被挡死**（批B 探针反射直调绕过评分故未暴露；HH.138 七点复核构成验证未覆盖 switch 分支覆盖）。本批搭车补齐（开工回执已申报）——属"让批B 交付真实生效"的语义补全，非行为放宽。请复核定性。
2. **【守军编队跨国招兵=玩法破坏级观察】**：FindIdleSoldiers 按 faction=PlayerCamp 共享阵营粒度过滤（B7 将军成军同款既有模型），而 AI 王国单位 faction 实测=PlayerCamp（归属面=kingdomId）→招募池含玩家+全体 AI 国兵。**C4 实锤：AI_Garrison m=6 编队 kid=0/0/3（玩家兵/兽人兵入编）**——AI 动员档会抽走玩家战斗兵。归属面已由显式 override 修复（编队 KingdomId 恒=创建国），但成员国籍混杂仍在。修法请裁决（建议：FindIdleSoldiers 增 kingdomId 过滤参数，B7 链同享；或编队按 kingdomId 招募）。
3. **【F1 带外 clamp 口径】**：威胁锚距己城超出候选带归一化范围（aiBuildRadius×div=32 sub）时带内 F1 恒 0=特征退化（不崩溃，argmax 由 F2/F3 接管；本局 k1↔k2 相距 45 格实锤）。是否改归一口径（如分母=max(城-锚实际距离, 带径)使带内保有梯度）请裁决——改动影响权重语义，执行端不擅动。
4. **【FormationController.KingdomId override 注记】**：新增 SetKingdomIdOverride 显式归属覆盖字段（ResolveKingdomId 优先级头部插入；不设=旧推导语义零变化）。A6 归属推导语义扩展申报：将军→首成员→-1 链前加覆盖层。守军编队创建方显式传 kid=计数面/态势层 FormationCount 过滤面正确性根基。
5. **【D5 特征语义口径确认】**：BarrenRich=Plain 密度判定（开阔平原占比≥0.60）/MineralRich=Mine 密度（原设计仅枚举名无语义，执行端按名称推导实现）——请策划核口径是否符合设计意图（ForestDense=Tree 密度/RiverAdjacent=半径水格自明）。

## 六、教训核查

- **L-02（回显成功≠落盘）**：今日首跑 17/23 症状=昨日 r2 复刻（投影值一字不差）→磁盘 Read 验证 r3 修改在、Unity 域旧程序集——refresh_unity `refresh_triggered:false` 暴露"编译请求≠编译发生"；touch 文件时间戳+重刷后 disconnect/retry 路径=真实重编译信号。**执行端教训候选：Unity 文件监视器失焦/异步场景下 refresh_unity 可能假成功，新代码生效必须以"行为特征反射探活"或"日志文案差异"验证，不得以 refresh 返回 success 为准。**
- **L-13（编译双通道）**：全程 0 error CS+execute_code 反射探活（ComputeF1 参数数/SetKingdomIdOverride 在场=新旧代码判别器）。
- **L-20（TryBuild=施工启动非竣工）**：P6pre/P7b 建仓后 WaitDays(4) 等 Active 翻转——在册教训直接复用，零重犯。
- **菜单调用偶发丢失**：进 Play 早期 execute_menu_item 回显 success 但未执行（探针未启动）→重触发解决；**探针重入=两次触发协程打架早退（PASS=1 FAIL=2 假象）**——处置=退 Play 杀协程再单次触发。教训候选：容器触发前确认无历史协程存活，Play 态重入须全清。
- r1→r5 自纠链：族分布/prefab 面→坐标系跨域→空壳堆积→faction 粒度→归属漂移，每轮现场 execute_code/Editor.log 诊断实锤后改——方法论正面延续。

## 七、账本回执

- HH.140：🟡开工回执→义务清偿（本报告随附待验收）
- HH.141：🔵预留→🟡已落盘（本报告，待策划端验收销号）
- 15_训练侧harness 文档 D4 注记已落盘（目录 .gitignore 不入库，账本形式注记）：AI 建筑选址打分器=Unity 单侧消费（S-8/D4·D525·HH.140 登记），零镜像义务；E3 归批E。
- D 水位维持 D594（本批无新裁决需求，列报 5 项待批）

## 八、批E 前置状态

批C/批D 产物 sim 镜像义务=零（选址打分器 Unity 单侧消费已注记；姿态层/守军链为 Unity 侧行为层，训练侧不消费）。批E（存档+sim 镜像+冒烟收口+E3 factor_registry+B4 15_账本注记）待策划端验收本批后解锁。
