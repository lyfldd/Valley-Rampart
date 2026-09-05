# AI 人口再生批——Gate 五面实施要点报告（报策划端裁决，勿施工）

> 类型：Gate 报告（实施要点，D539 裁定「人口结构牵 2_23 人口=第五资源设计域，比供水批多一轮 Gate」）
> 状态：⏳待策划端裁决（裁决后签发施工任务书）
> 日期：2026-09-05 · 发起端：执行端 · 依据：HH.75 再熔断报告 §六（发现 1/2 号）+HH.76 件2 同模式排查（§二）
> 红线：本报告纯只读调研，零代码改动

## 面① SpawnUnit 多 kingdomId 化影响面

### 签名现状（好消息：工厂通道已参数化）

- `UnitFactory.SpawnUnit(Faction, Occupation, Vector2, int kingdomId = 0)`（L131）+`SpawnUnit(UnitData, Vector2, int kingdomId = 0)`（L57）——**kingdomId 参数已存在且默认 0**；SiegeProductionSystem L208（AI 机器）/KingdomFoundry L113（AI 预置工人）已在用 kingdomId 传参。
- **faction 与 kingdomId 已是分离语义**：调用方传 `Faction.PlayerCamp, ..., kingdomId>0` 的组合已是现网事实（Foundry/Siege AI 路径）——AI 单位 faction 层标记与归属国 kingdomId 独立，`GetFaction()`（Satiety L163 消费）按现网组合工作正常。

### PlayerCamp 硬编码调用方全集（grep 级）

| # | 调用点 | 现状 | per-kingdom 化改动 |
|---|---|---|---|
| 1 | **PopulationSystem L312**（繁殖 Child） | `SpawnUnit(Faction.PlayerCamp, Occupation.Child, birthPos)`（无 kingdomId=玩家专属） | 主目标：AI 分支传 kingdomId+按国族 raceId 抄写 |
| 2 | SiegeProductionSystem L165 | 玩家机器（PlayerCamp+默认 0） | **不动**（玩家路径） |
| 3 | SiegeProductionSystem L208 | AI 机器（已带 kingdomId） | 不动 |
| 4 | KingdomFoundry L113 | AI 预置工人（PlayerCamp+kingdomId） | 不动 |
| 5 | AIDebugSpawnController L663 | 调试工具 | 不动 |

### 玩家侧零回归面

- kingdomId 默认值 0 保留→现有调用不传参=玩家语义逐位不变。
- 玩家繁殖路径（PopulationSystem.OnNewDay 玩家分支）建议**逐位保留**（Faction.PlayerCamp+GetKingdomRace(0)+玩家侧幸福/饱食输入）——AI 走新增 per-kingdom 分支，双轨并存（见面②）。
- 雷区提示（HH.76 件2 同模式排查产出）：**SpawnUnit 不得进入 `foreach GetAllUnits` 遍历体内**（GetAllUnits 返回内部 List 引用，遍历中 Spawn=枚举失效同款雷）——现状安全（Spawn 都在遍历外/先收集后处理），per-kingdom 施工须维持此纪律。

## 面② 配对池扩 Worker 设计

### 现状三重锁（AI 无配对资格的完整机制链）

1. **配对池职业门槛**：PopulationSystem.OnNewDay L284 `EffectiveOccupation != Occupation.Resident → continue`——AI 纯 Worker 结构连配对资格都没有（D539 策划端补充发现）。
2. **_entities 桶 0 专属**：PopulationSystem._entities 只收玩家桶 0 实体（L95 注释「IsPopulationEntity 守卫 kingdomId>0 排除」）——**AI 单位根本不在配对循环的数据源里**（扩职业前先解决数据源）。
3. **全局条件读玩家侧**：L246-249 AvgHappiness/AvgSatiety 读玩家系统全局值；L263 houseCapacity=玩家房屋容量——AI 条件评估无独立输入。

### 设计提案（双轨口径，供裁决）

- **玩家轨（逐位不动）**：现有 OnNewDay 主流程原样（Resident 配对池+玩家侧条件+Faction.PlayerCamp+GetKingdomRace(0)）。
- **AI 轨（新增 per-kingdom 分支）**：`OnNewDayPerKingdom(KingdomState k)`：
  - 条件输入按国：幸福=HappinessSystem._overallHappiness[k.id]（已有 per-kingdom 桶 L168）、饱食=SatietySystem.GetAverageSatiety(k.id)（已有 per-kingdom L103）、房屋=该国 House 容量（GetTotalHouseCapacity 需扩 kingdomId 参数或按国统计）；
  - 配对池=该国 Adult 池：**Worker+Porter+Resident 均可配对**（AI 纯 Worker 结构下 Worker×Worker；池来源=直接遍历 UnitRegistry 按 kingdomId 过滤[只读安全]，或 per-kingdom 实体桶[施工量大]——建议前者+固定遍历序=确定性）；
  - 冷却/概率参数落 SO：KingdomConfig 增 aiBirthIntervalDays/aiBirthHappinessThreshold/aiBirthSatietyThreshold（或复用 birth* 参数+per-kingdom 冷却字典）——**SO 化纪律**（so-data-driven）；
  - 生成：SpawnUnit(Faction.PlayerCamp, Child, birthPos, kingdomId)（现网组合先例）+childUc.raceId=KingdomRace.GetKingdomRace(k.id)（抄国族）；
  - 耗粮：Child 日常耗粮走 Satiety 现有 per-kingdom 国库路由（D453 已通）——零新增。
- 参数建议（占位待裁）：AI 生育冷却 10~15 日（慢于玩家 5）、阈值与玩家同表（复用 birthHappinessThreshold=60/satiety=50）。

## 面③ Child 成长链归属

### 现状

- `TickChildGrowth`（L349-365）：遍历 **_entities（玩家桶 0 专属集合）**，Child 成长天数累积≥childGrowthDayEvents（默认 2）→`SetOccupation(Occupation.Resident)`——**同单位换职业，无 Spawn+Destroy（无增删雷）**；成长期耗粮=Satiety 按职业日耗（UnitData L39 Child 吃粮 1/日）走既有国库路由。
- **AI Child 成长后职业建议**：→ **Worker 直生**（AI 无 Resident 消费体系；Resident 在 AI 侧无意义[纯 Worker 结构]）——`SetOccupation(Occupation.Worker)`，直接进入 ⑥招工同口径的 workerCount 派生统计与产能——**绕过 ⑥（⑥是流浪汉转工，成长 Child 直接成 Worker=新生产力，不占流浪供给）**；
- 数据源：AI 分支需遍历 UnitRegistry（按 kingdomId+Child 过滤）——per-kingdom 化同面②数据源方案；
- 成长耗粮：零新增（Satiety 路由已按 kingdomId）。

## 面④ VagrantCamp 自然增长机制（混合双通道的流浪侧）

### 现状断点实锤（P1 两跑 39 日零流浪的机制解释）

- **补员机制在但永不触发**：OnNewDay（L114）每日补员（不满营地补 campDailyRefill 至 campMaxVagrants）→但 `FindCamps()`（L234）扫 `def.id=="VagrantCamp"` **建筑实体**——而 OnNewGameMapReady（L66-108）**只 Spawn 流浪汉实体，从不创建营地建筑**→FindCamps 永空→补员循环体零执行→**流浪供给=开局一次性池**（initialVagrantTotalMin~Max=4~6 人）。
- **族别错配双断**：初始流民全 Human（L88-89「族别来源挂账：Q10-M2 接真模板映射前全 Human」）+D469 招募限同族（KingdomBrain L322）→**非人类 AI 国连初始流浪也招不到**（异族流民=永久野人）——P1_run2 四 AI 中 3 个非人类国全中。
- **动态立国耦合**：结营阈值（L21 ≥3 未招募流浪→结营建国）依赖流浪聚集+营地——流浪池枯竭后动态立国亦断（P1 两跑零动态立国旁证）；foundingThreshold 语义不变，供给恢复后自然复通。

### 设计提案（供裁决）

- **方案 A（推荐）**：流浪侧只修「营地实体缺位」——自然增长刷点=定期（N 日 rng）在无主地按地图族分布 SpawnVagrantAt+可选建 VagrantCamp 建筑实体（让 FindCamps 补员链复通）；族别映射随 Q10-M2（模板映射已接）回填按族投放。参数 SO 化（vagrantRespawnIntervalDays/respawnGroupSize）。
- **方案 B**：AI 人口再生全走繁殖通道（面②③），流浪侧维持现状（一次性池+玩家侧营地手动机制）——AI 侧零改动，但「混合双通道」拍板（D539 用户 C）落空。
- 耦合提示：A 方案刷点须避开 AI 领土（无主地判定=TerritorySystem 无主）防野人入籍冲突；与野性敌意 D472 的交互（异族群互杀）维持 D469 按族成群纪律。

## 面⑤ EventBus 零回归面

- **件2 修复零订阅面变化**：快照副本遍历=纯消费侧防御，订阅/发布机制未触碰。
- **繁殖链新事件需求=零新增**（复用既有）：Child SpawnUnit→`UnitSpawnedEvent` 已有订阅链（PopulationSystem 注册表自动重建 L369 注释+UnitRegistry 注册）即自动生效；成长转职→`SetOccupation` 走 UnitAttributeChangedEvent 既有链。**可选增项**：繁殖成功播报（ToastManager/KingdomFoundedEvent 同款广播）——列设计可选项非必需。
- 风险提示复述：AI 繁殖上线后 UnitSpawnedEvent 发布频率上升——订阅者（PopulationSystem 入册/TaskScheduler/统计）均为非遍历内注册（安全），但 Gate 施工时按 HH.76 件2 同模式清单复核一次（20 处 foreach GetAllUnits 全只读确认过）。

## 附：施工切面预估（供任务书签发参考）

| 批内件 | 触点 | 量级 |
|---|---|---|
| 面② AI 生育分支 | PopulationSystem.OnNewDay 扩 OnNewDayPerKingdom+KingdomConfig SO 参数+房屋容量按国化 | 中 |
| 面③ AI Child 成长 | TickChildGrowth per-kingdom 扩（UnitRegistry 遍历版）+成长→Worker | 小 |
| 面④ 流浪侧（若选 A） | VagrantCampSystem 自然增长刷点+族别映射回填（Q10-M2 挂账同口清偿） | 中 |
| sim 义务 | 经济/人口镜像若涉人口再生公式→sim SimEconomy 人口段同步（15_账本前瞻） | 待裁 |
| 冒烟 | 人口再生冒烟容器（立国→ fertility 条件→Child 诞生→成长→Worker 计数上升+存档回读） | 中 |

## 策划裁决（策划端回写，裁决前保持空白）

> 策划端实盘复核（2026-09-05）：三处关键声明抽查实锤（SpawnUnit L57/L131 kingdomId=0 参数化+现网 Foundry/Siege AI 路径已用四参/_entities 桶 0 守卫注释 L97/VagrantCampSystem OnNewGameMapReady 零营地建筑创建+FindCamps 永空+族值硬编码 GetKingdomRace(0)——**面④画像策划端补一笔记：L88/L94/L103 实读确认「按族投放」结构已实装[D469 同族成群]但族值全 Human，L110 日志「按族投放」指结构非映射**）。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 面① 影响面认可 | **✅ 认可** | kingdomId 通道现网事实（Foundry/Siege 先例）+faction 与 kingdomId 分离语义成立；PlayerCamp 5 处全集=1 处主目标 4 处不动（玩家路径/调试工具）；雷区纪律（Spawn 不入 GetAllUnits 遍历体）列为施工硬性条目 |
| 面② 双轨口径（AI 分支/参数 SO 化/池方案） | **✅ 采纳**：玩家轨逐位不动+AI 轨 OnNewDayPerKingdom；条件输入三处 per-kingdom 化（幸福桶/饱食缓存已有读口，房屋容量按国统计）；**池方案=直接遍历 UnitRegistry 按 kingdomId 过滤+固定遍历序**（确定性，施工量小优先）；配对池=Worker+Porter+Resident 均可；参数 SO 化纪律兑现，**初始占位裁决：AI 生育冷却=10 日、阈值复用玩家同表（60/50）**，终值归 P0 端到端调优批 | 用户已拍板路线 C；per-kingdom 读口基建批3a 已备（与供水链同款「接收端在位」模式） |
| 面③ AI Child 成长→Worker 直生 | **✅ 采纳** | 绕 ⑥ 不占流浪供给=新生产力直生；Resident 在 AI 侧无意义符合纯 Worker 结构；SetOccupation 换职业无 Spawn 雷区；成长耗粮 Satiety 路由已通零新增 |
| 面④ 流浪侧方案 A/B | **✅ 方案 A**（用户 D539 拍板 C 混合双通道，B 落空）。施工四件：①营地实体复通（WorldManager.PlaceVagrantCamps 链核查——L6 注释声称地图生成建营地但 P1 两跑行为级证明 FindCamps 永空，先定位 PlaceVagrantCamps 调用点断链处，再选恢复调用或 OnNewGameMapReady 补建，二选一列报）②自然增长刷点（定期 N 日无主地[TerritorySystem 无主判定]按组 Spawn，**避开 AI 领土**防野人入籍冲突维持 D469 纪律）③**族别映射回填**（anchorRace/groupRace 硬编码 GetKingdomRace(0)→按地图族分布/出生锚映射——Q10-M2 挂账同口清偿，含初始流民与刷点流浪两处）④参数 SO 化（vagrantRespawnIntervalDays 初始 5 日/respawnGroupSize 初始 2，终值 P0 调优） | 断点双实锤（营地缺位+族值 Human 双断）；刷点避开领土+按族成群=野性敌意 D468/D469 交互面保持 |
| 面⑤ 事件面（复用既有/可选播报） | **✅ 复用既有；可选播报=不做**（YAGNI——播报面归 2_13 域 D305 挂账，AI 繁殖播报若 2_13 批要再加） | UnitSpawnedEvent/SetOccupation 既有链自动生效；Gate 施工按 HH.76 件2 20 处清单复核一次 |
| 施工任务书签发（切面预估参考） | **✅ 签发 HH.78**（面②AI 生育分支+面③成长链+面④流浪侧三件+人口再生冒烟容器[立国→fertility 条件→Child 诞生→成长→Worker 计数上升+存档回读]；sim 义务=施工后列报人口再生语义 sim 侧有无，策划端登记口径同 15_账本 #7） | 切面预估量级合理；冒烟容器含存档回读=aiBuckets additive 教训沿用 |

**验收前置条目（施工任务书同款红线）**：AI.Core/sim 零触碰/玩家轨逐位不动/Spawn 不入 GetAllUnits 遍历体（硬性条目）/冒烟全绿才 commit/git diff 自查。**P1 三考解锁=本批验收后。**
