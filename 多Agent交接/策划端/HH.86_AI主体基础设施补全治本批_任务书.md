# HH.86 任务书：AI 主体基础设施补全批（治本批·全量 16 条）

> 类型：施工任务书（执行端）
> 状态：⏳待执行端接单（HH.81 收尾后接）
> 日期：2026-09-06 · 发起端：审计策划（依 HH.85 审计产出起草，用户拍板「全量治本」）
> 依据：HH.84 片1~4 审计报告（四份，`报告/设计审查/`）+ 缺陷台账 DZ-040~065 + D545 主体对称原则（40982cb 提案态）
> **本批性质**：P1 大考逐层显形（粮→人口→招工/住房，两日三修）的**治本收束**——审计已把全部病灶定位到 文件:行号+量级，本批=按图施工，勿再排查。**P1 四考重跑排本批收口后**。
> **文档最小化**（用户指示）：本批只交三样——本任务书+完成报告+验收记录；正式文档（2_24 架构批细化/片5~9 审计/设计文档回写）全部排 P1 收口后。

---

## 一、批背景（为什么是这一批）

P1 三轮大考暴露的问题全部同族：底层模块按「玩家专用」写成，AI 每走到一处玩家特权假设即断（P7 单主体假设族）。逐个补丁已两日，审计完成全量病灶地图（26 条 DZ，其中 16 条入本批，10 条已修/注记/归后续片）。**本批修完后 P1 四考的预期=冲军事期，而非再暴露下一层**——下一层候选（Well 损耗无重建 DZ-043/军事统计地基 DZ-057/空壳耗资源 DZ-041）恰好全部被本批治掉。

## 二、施工件（四件套）

### 件1 小修集（7 条，半天）

| # | DZ | 位置 | 修法 | 探针 |
|---|----|------|------|------|
| 1a | DZ-042 | `Resources/Config/Kingdoms/Kingdom_RiverBay.asset` baseBuildingDefIds | [castle,farm,mine,Warehouse,quarry] → **[castle,House,farm,Well,mine,Warehouse,quarry]**（对齐另五模板；**若 HH.81 收尾已捎话修掉则标已修跳过**） | 冒烟 RiverBay 模板局：AI 国开局有房有井 |
| 1b | DZ-057 | PopulationSystem.AliveWarriorCount 职业清单 | 追加 2_20 M7 七职业：Berserker/WolfRider/Musqueteer/Bedrock/Ranger/Windwalker/DeerRider（**枚举 int 值以 Occupation.cs 实值为准**，参考偏移声明 Monster=27/28~37 尾插链）；HeavyWarrior 保留（枚举位铁律）；3 机器不入（口径对齐） | AI 训一个 Berserker → k.warriorCount +1 |
| 1c | DZ-047 | TrainingSystem.CanTrainGeneral | 加 kingdomId 参数按国计数（调用点=TryTrain effKingdom）；玩家桶 0 逐位不动 | AI 国训将军不受玩家将军数干扰（双向） |
| 1d | DZ-064 | TaskScheduler.Tick 空闲候选过滤（`occ != Worker && occ != Civilian continue`） | 追加 Porter 放行 | 训练一个 Porter → 能领任务 |
| 1e | DZ-059 | PopulationSystem.GetBirthPosition | BuildingRegistry 遍历加 `b.kingdomId == 0` 过滤（对齐 AI 轨 GetKingdomBirthPosition） | 玩家 Child 落玩家 House 旁 |
| 1f | DZ-060 | PopulationSystem.OnNewDay 玩家配对段（Random.value/Random.Range） | 种子化（对齐 AI 轨 R4 纪律：System.Random 种子 rng；种子源选择报备） | 同 seed 两轮配对选择一致 |
| 1g | DZ-065 | `Editor/Smoke/Valley_P1_Observer.cs` WhitelistTags | 补 6 tag：**[TaskScheduler]/[ProductionSystem]/[RulerController]/[SatietySystem]/[PopulationSystem]/[AIEconomySettlement]** | 四考日志镜像出现派发行 |

### 件2 主体对称清偿（4 条，1 天）

| # | DZ | 位置 | 修法 | 探针（行为级硬条目） |
|---|----|------|------|------|
| 2a | DZ-040 | BuildingFactory.CreateBuildingInstance（b.faction=def.faction）+ BuildController.TryBuild（b.Init 后） | **faction 按 kingdomId 派生**：0=PlayerCamp / >0=AiKingdom / -1=None——**照抄 UnitFactory.SpawnUnit 既有先例**（kingdomId>0 自动覆写 AiKingdom）。KingdomFoundry 预置链走 Factory 自动生效；读档 SpawnFromSave 同链 | ①AI 塔对玩家单位开火✓ ②AI 塔对本国单位**不开火**✓（负探针）③AI catapult 半径内本国工人→IsOperational✓ ④玩家点 AI 建筑不再开面板（Interact 拒绝非本阵营——表现层变化注记进报告） |
| 2b | DZ-046 | HappinessSystem.ComputeUnitHappiness 四因子 | ①houseFactor：本国房容（GetHouseCapacityByKingdom(kingdomId)）vs 本国人口（玩家=PopulationCount，AI=workerCount+warriorCount）②church/hospital：CountActiveBuildings 加 per-kingdom 过滤重载（b.kingdomId==kingdomId）③foodQuality：AI 读本国 KingdomState.resources（特食/肉），玩家读 Ruler 原逻辑 | 玩家建 Church 前后 **AI 幸福不变**（不再反向受益）✓；玩家桶 0 四因子逐位不动 |
| 2c | DZ-044 | Building.TryAdvertiseTask ④段 + TaskScheduler.ExecuteCompletion WaterHaul 段 | 广告条件改读 `WaterNetwork.GetStored(_building.kingdomId)`（本国桶）；完成改 `AddWater(waterCarryAmount, 工人kingdomId)`（双参） | AI 国农田缺水→AI 工人挑水→**AI 桶上升、玩家桶不变**✓ |
| 2d | DZ-045 | TaskScheduler.ExecuteCompletion Gather 段 overflow + UnloadInventory 兜底 | 按工人 kingdomId 分流：0→RulerController（原逻辑）；>0→KingdomRegistry.Get(k).AddResources | AI 工人采集溢出→入 AI 国库，玩家国库**分毫不动**✓ |

### 件3 评分与供给（4 条，1 天）

| # | DZ | 位置 | 修法 | 探针 |
|---|----|------|------|------|
| 3a | DZ-041 | ①BuildingFactory.AttachComponents ②UtilityScorer | **①组件挂载**：StorageComponent 挂载条件扩为「producer.rate>0 **或** (rate==0 且 producer.capacity>0 且 role==Economy)」——Warehouse/Granary 照挂入 WarehouseRegistry；**②评分收敛**（连轴根治）：HouseGap need 源改「本国房容>本国人口才不缺」；WarehouseGap/GrainGap Feasible 加「同 def 本国已建数 < 目标上限」守卫（对齐 WallGap 目标座数模式；上限参数 SO 化占位，**真值报告列报待策划调**） | AI 高存量局 N 日：Warehouse/Granary 新建数 ≤ 守卫上限（**连轴终止**）✓；Warehouse 建后入 WarehouseRegistry✓ |
| 3b | DZ-043+052 | UtilityActionConfig | 加两条建造行动：**BuildWell**（井损重建通道）/ **BuildBlacksmith**（AI 铁链打通）；**顺手落 SO 资产**（Config/Kingdoms/UtilityActionConfig.asset 现缺失，代码默认同步成资产）；minStage/axis/need 参数按既有行动模式占位+报告列报 | 摧毁 AI 井→数日内重建✓；AI 建 Blacksmith 后可训铁耗兵种（CanPayRecruit metal 分支通） |
| 3c | DZ-053 | UtilityActionConfig + 建造执行 | AI 建造行动接入 **WarAcademy/WarCamp/LeyForge/ArcheryRange 四专属**（按本国族：raceId 门禁已有[BuildController M6 门禁复用]，uniquePerKingdom 限建已有）；minStage 建议 Military（参数报告列报） | 对应族 AI 国建出专属建筑→HasExclusiveBuilding(k.id) 为真（族加成激活：如矮人 AI 熔炉后 mineMul 1.82 生效）✓ |
| 3d | DZ-058 | PopulationSystem.OnNewDay 玩家配对池 | 扩为 Worker/Porter/Resident（对齐 AI 轨口径——全量治本拍板） | 玩家全工人结构下繁殖可发生 |

### 件4 收口（半天）

1. 编译 0 警告 0 错误。
2. **四容器回归**：2_20 / 2_20B 六轮（含换 seed）/ 2_20C / 2_13_C——**玩家侧零回归**（件2/3 全部 per-kingdom 改造的玩家桶 0 必须逐位不变；件3 评分收敛会改变 AI 建造分布属预期，报告对照表列报）。
3. **P1 四考重跑**：HH.71 协议 + 新 seed + 白名单补齐后的全维度观测（派工/生产/资源/人口日志全进镜像——四考不再盲飞）。**判定口径=冲军事期**；若再熔断，新层将由全维度日志直接暴露（不再排查两天）。
4. 完成报告（编号顺延 HH.87）：逐件施工清单+探针结果+影响面列报（faction 派生的怪物索敌/交互面变化/AI 建造分布对照）+SO 化参数清单（待调真值）+DZ-040~065 修复状态建议回填表。

## 三、纪律

1. **玩家侧零回归**是硬红线：件2/3 所有 per-kingdom 改造玩家桶 0 = 原逻辑逐位一致；四容器回归为证。
2. **确定性**：不引入未种子 Random（件1f 本身就在修此病）；同 seed 两轮关键路径一致。
3. **交付前置**：git diff 自查（HH.42 家族第 6 笔教训）+ grep 双锚点复验注册类改动。
4. **冒烟写单位走生产 Prefab 实例化**（2_13 P4 教训）；EventBus GetAllUnits 遍历雷区 35 处清单（HH.79）随改随查。
5. **不越界**：DZ-061（铁豁免）/062/063 不在本批（资源经济域，随 2_23 批B/P0 调优）——**本批只做上表 16 条**，顺手发现的另登台账勿现场扩权修。
6. 台账状态：完成报告附 DZ 回填建议表，正式转✅归策划验收。

## 四、策划裁决（验收时回写）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 件1~4 施工与探针验收 | | |
| P1 四考结果（冲军事期/新层暴露） | | |
| 评分收敛参数真值（限建上限 SO） | | |
| 行动池新行动参数（Well/Blacksmith/四专属 minStage） | | |
| DZ-040~065 状态批量回填 | | |

---

_起草：审计策划（依 HH.85 审计产出+用户「全量治本」拍板）· HH.86 · 2026-09-06 · 执行端接单口径：HH.81 收尾后接，预计 1.5~2 天_
