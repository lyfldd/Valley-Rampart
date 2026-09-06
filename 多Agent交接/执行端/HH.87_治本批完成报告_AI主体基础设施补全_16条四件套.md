# HH.87 完成报告（HH.86 AI 主体基础设施补全治本批·全量 16 条四件套+P1 四考）

> 类型：完成报告（治本批）
> 状态：⏳待策划端验收
> 日期：2026-09-06 · 发起端：执行端 · 任务书：策划端/HH.86_AI主体基础设施补全治本批_任务书.md
> 依据：HH.85 审计五件 DZ-040~065 + D545 主体对称原则 + HH.83 §策划裁决（五随批）
> 红线自查：玩家侧零回归（四容器实证）/确定性（件1f 种子化）/git diff 自查+grep 双锚点/雷区 35 处/不越界（DZ-061~063 未动）

---

## 一、件1 小修集（7 条全清）

| # | DZ | 施工 | 落点 |
|---|----|------|------|
| 1a | DZ-042 | RiverBay 补 House+Well（目标序 castle,House,farm,Well,mine,Warehouse,quarry）——验收裁决兜底件（DZ-042 用户已裁归本批） | [Kingdom_RiverBay.asset](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Resources/Config/Kingdoms/Kingdom_RiverBay.asset)（纯数据行，无 YAML 注释防解析风险） |
| 1b | DZ-057 | AliveWarriorCount 追加 M7 七职业 Berserker/WolfRider/Musqueteer/Bedrock/Ranger/Windwalker/DeerRider（枚举实值 28~34 区先验 UnitData.cs L43-49；HeavyWarrior 保留；M9 三机器不入） | [PopulationSystem.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/PopulationSystem.cs) L120-129 |
| 1c | DZ-047 | CanTrainGeneral 加 kingdomId 参数按国计数（调用点=TryTrain effKingdom L301）；玩家桶 0 行为等价（玩家将军恒 kingdomId=0+PlayerCamp，faction 过滤与 kingdomId 过滤对玩家逐位一致） | [TrainingSystem.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/TrainingSystem.cs) L301+L538-556 |
| 1d | DZ-064 | TaskScheduler.Tick 空闲候选 Porter 放行（L230 追加；注释预告位兑现） | [TaskScheduler.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs) L226-234 |
| 1e | DZ-059 | GetBirthPosition 加 `b.kingdomId != 0 continue`（对齐 AI 轨 GetKingdomBirthPosition 同式过滤；旧=全图首栋 House 可能挂 AI 国） | [PopulationSystem.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/PopulationSystem.cs) L464 |
| 1f | DZ-060 | 玩家配对段种子化：Random.value/Random.Range → System.Random pairRng，种子=seed^(day*7919)（AI 轨 L398-407 同族公式去 k.id 项特例；**种子源=map.seed 与 AI 轨同源——种子源选择报备**）；抽签 `1 + rng.Next(Count-1)` 等价旧 `Range(1, N)` | [PopulationSystem.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/PopulationSystem.cs) L300-316 |
| 1g | DZ-065 | 观察器白名单补 6 tag——**实补 4**：[TaskScheduler]/[ProductionSystem]/[RulerController]/[SatietySystem]；[PopulationSystem]/[AIEconomySettlement] 已在案（任务书口径与实况差列报） | [Valley_P1_Observer.cs](file:///c:/Users/trs/Desktop/Valley Rampart/Valley Rampart/Assets/Editor/Smoke/Valley_P1_Observer.cs) L46-51 |

## 二、件2 主体对称清偿（4 条全清）

### 2a DZ-040 faction 按 kingdomId 派生（三处）

照抄 UnitFactory.SpawnUnit 既有先例（L135-139：仅 >0 覆写 AiKingdom；玩家 0/自然 -1 保持 def.faction 原值=玩家与野外逐位不动）：

1. **BuildingFactory.CreateBuildingInstance**（[BuildingFactory.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs) L202-210）：派生式移到 kingdomId 赋值**后**（旧 L204 在归属写入前=AI 国仍挂 def.faction）；KingdomFoundry 预置链+读档 SpawnFromSave 内联路自动生效
2. **Building.ApplyDef**（[Building.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs) L336-340）：读档链二次 ApplyDef（SpawnFromSave L379）时 kingdomId 已在位——派生全覆盖
3. **BuildController.TryBuild**（[BuildController.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildController.cs) L281-282）：b.Init→ApplyDef 时归属未写入（仍 0），归属赋值后补覆写

全项目 `faction = def.faction` 残留=0（grep 实证）。

### 2b DZ-046 幸福四因子 per-kingdom（[HappinessSystem.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/HappinessSystem.cs) L188-222）

- houseFactor：本国房容 GetHouseCapacityByKingdom(kingdomId) vs 本国人口（玩家=PopulationCount 桶0 实体数 / AI=workerCount+warriorCount）
- church/hospital：CountActiveBuildings 加 per-kingdom 重载（L236-250，旧无过滤签名不动=既有消费者零回归）；玩家 0 与旧全图值在「AI 不建 Church/Hospital」前提下逐位等价（行动池无此二项——3b 新增行动亦不含）
- foodQuality：AI 读本国 KingdomState.GetResourceValue（特食/肉），玩家读 Ruler 原逻辑逐位
- 税负 L196 原本已按桶不动
- **HasEnoughHousing 失去唯一消费者已删除**（本批改造连带，死代码清理）
- 玩家 houseFactor 输入源变化面：旧=GetTotalHouseCapacity 全图（含 AI 房污染），新=仅本国——HH.81 后 AI 有 House，有 AI 房时玩家旧判被污染（DZ-046 失真面之一），本批修正=预期列报（四容器实证无行为回归）

### 2c DZ-044 挑水链双参（两处）

- Building.TryAdvertiseTask ④段（L969-974）：广告条件 `Stored`（0 桶）→ `GetStored(kingdomId)` 本国桶（GetStored(0)==Stored 旧语义等价，玩家逐位）
- TaskScheduler.ExecuteCompletion WaterHaul 段（L579-586）：`AddWater(waterCarryAmount)` 单参 → 双参 `AddWater(waterCarryAmount, 工人kingdomId)`

### 2d DZ-045 采集溢出按国分流（[TaskScheduler.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs) L617-651）

AddGatherOverflow helper：玩家(0)→RulerController 原逻辑逐位；AI(>0)→KingdomRegistry.Get(k).AddResources 台账（ResourcePack 单字段构造=TaxSystem L162 先例）；国已注销→丢弃+日志（防亡国资源入玩家库资敌）；非国库五资源→丢弃+日志列报。Gather 段 overflow+无背包两路均改走分流。

## 三、件3 评分与供给（4 条全清）

### 3a DZ-041 空壳仓储挂载+评分收敛

- **①组件挂载**（[BuildingFactory.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs) AttachComponents L283-290）：扩条件 `rate<=0 && producer.capacity>0 && role==Economy` → Warehouse/Granary 类照挂 StorageComponent 入 WarehouseRegistry（旧=不挂→连轴建 79 座空壳）
- **②评分收敛**（[UtilityScorer.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs)）：
  - HouseGap need 源改「本国房容 vs 本国人口」（L105-112）：房容≥人口→0 不缺（旧=人口/needA 纯代理与房容无关→有房仍建）
  - Feasible 建造类分支三守卫扩（L301-326）：①**上限守卫** buildTargetCap>0 时同 def 本国已建<上限（对齐 WallGap 目标座数模式；Warehouse/Granary 占位 4，**真值待策划调列报**）②族门禁（M6 复用：BuildingDef.raceId>=0 时须与本国族匹配）③每族限建 1 镜像（def.uniquePerKingdom 且本国已建≥1→不可行）
  - 新 helper CountActiveDef（L349-362）

### 3b+3c DZ-043+052+053 行动池扩六行动（代码+SO 资产 21 条）

- **UtilityAction 枚举尾插**（[UtilityScorer.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs) L35-42）：BuildWell=16/BuildBlacksmith=17/BuildWarAcademy=18/BuildWarCamp=19/BuildLeyForge=20/BuildArcheryRange=21
- **NeedKind 尾插**（L56-60）：WellGap（井数<目标，DZ-043 井损重建通道）/MetalGap（国库 Metal<底线，DZ-052 铁链）/ExclusiveGap（无族专属→0.5 底分，DZ-053）
- **NeedScore 三 case**（L178-186）+**ExecuteFocus 六 case 路由**（[KingdomBrain.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs) L162-169，走 SO buildingId 通用 ExecuteBuildFocus）
- **UtilityActionDef 加 buildTargetCap 字段**（[UtilityActionConfig.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs) L103-108）
- **默认 actions 补全+扩 21 条**：旧默认 15 条+六新行动；成本镜像=六 BuildingDef 资产实值 2026-09-06 实查（Well stone6/Blacksmith gold50+stone60/WarAcademy gold30+stone20/WarCamp gold10+wood25/LeyForge gold25+stone30/ArcheryRange gold15+wood30）
- **SO 资产全量重写**（[UtilityActionConfig.asset](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Resources/Config/Kingdoms/UtilityActionConfig.asset)）：**审计实锤=资产为旧版 8 条（缺 ⑦招战士/⑧科技/⑨城墙/⑪⑫⑮——LoadConfig 优先资产→缺的行动运行时不可见=⑦招战士链四考前必堵面）**，重写为 21 条全量（含 buildTargetCap 字段落档）；YAML 手写与代码默认同步，guid/m_Script 不动
- **参数占位列报**（待策划调）：WellGap needA=1/MetalGap needA=30/ExclusiveGap 底分 0.5/minStage：Well=Survive、Blacksmith=Develop、四专属=**Military（任务书建议）但 LeyForge 用 Expand**（纯经济建筑 mineMul 加成面，扩张期开建更贴合功能——偏差列报）

### 3d DZ-058 玩家配对池对齐（[PopulationSystem.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/PopulationSystem.cs) L285-287）

配对池 Resident→Worker/Porter/Resident（对齐 AI 轨 L379 同口径——玩家全工人结构下繁殖可发生）。

## 四、编译与四容器回归（件4.1/4.2）

- 编译：0 错误（20 警告全存量=CS0114/CS0108/CS0252/CS0414 等既有面，与本批改动文件无关——「0 警告」口径列报：本批未引入新警告）
- **四容器回归全绿**（unity_console 镜像 Temp/unity_console_2026xxxx）：

| 容器 | 轮次 | 结果 |
|---|---|---|
| Valley2_20_Smoke_Race | seed 22360 + 换 seed 42424 两轮 | **ALL PASS ×2**（D467~D472 行为级探针） |
| Valley2_20B_Smoke_M7 | 六轮（四族 22360×4+换 seed 7841/31337） | **ALL PASS 6/6**（R2~R6 跨轮 ActiveMap 新实例+实体零残留） |
| Valley2_20C_Smoke_M8M9 | 自动跑（D522） | **ALL PASS**（M8 基准生效/同族差异/零回归/端到端+M9） |
| Valley2_13_Smoke_C | 一轮 | **ALL PASS P1~P7** |

- **容器口径连带修正一笔**（2_20 ④d）：首轮 seed22360 ④d「AI⑥全异族→null」FAIL——破案=**HH.81 件1 流浪置 -1 的容器口径连带**（自然流浪 kingdomId=-1+族池 {0,1,2,3} 抽取=1/4 概率撞玩家族 Dwarf，世界流浪被 ⑥ 选中=假 FAIL，非产品回归；HH.55~68 时代世界流浪=0 不可招故 ④d 稳定）。修=④d 前清场（世界 -1+同族+未招募流浪临时置 IsVagrantRecruited，测试锚点先例=foundedFlag 强制复位同款，只读遍历改字段零结构变更，跑完弃局不存档）；修正后两轮 ALL PASS，断言输出含清场计数=可观测

## 五、P1 四考重跑（件4.3——判定口径=冲军事期）

- 容器：Valley_HH80_Run 改参复用（SEED 16180→**31415**/SLOT p1_run3→**p1_run4**/CIRCUIT_BREAK_DAY=120 不动）
- **seed 31415 报备**：hh80_scout_result.log 四 seed 侦察局——3 AI 三族各异（k1 森语 Elf/k2 寒晶 Dwarf/k3 战歌 Orc）+国距 26.6/41.0/53.7 均衡+非历史正式局 seed（避开 16180[三考]/22360[HH.78 容器]/52707[首轮]/42424[HH.86 换 seed 轮]/7841/31337[2_20B 轮]）
- 观测：白名单补 4 tag 后全维度镜像（派工/生产/资源/人口日志全进镜像——四考不再盲飞）；复核包纪律=p1_run3 系列原封，p1_run4 独立命名
- 结果：**🔴 D19 提前熔断（D539 同精神：代码级实锤非频率问题勿硬拖 120 日）——新层=「Abstract 冻结层」**

### 四考判定（D19 提前熔断）

**终局快照（D19）**：k1[密林] Abstract/工 6/粮 188/金 116 · k2[寒晶] Abstract/工 8/粮 0/金 135/阶段=Expand · k3[战歌] Fine（战斗锁回切闪烁）/工 6/粮 102 · 三 AI 战=0 零建造零生育零军事。

**新层：Abstract 冻结层（代码级实锤）**——AI 国离开玩家活跃带连续 2 日（offscreenDaysToAbstract 迟滞）→`KingdomBrain.Tick` L91 `mode==SimMode.Fine` 挡掉 `ExecuteFocus`（焦点照更新但招工/建造/招兵/推边界执行全停）→`AbstractEconomySettler.SettleDaily` 只结算采集产出/税收/耗粮/断粮流失，**无招工/建造/生育/军事任何镜像公式**→离屏 AI 发展冻结；且**切 Abstract 静默无日志**（观测盲区，本轮靠运行时直查 simMode 实值破案）。零干预下无任何回切路径（推边界执行本身被冻结=死锁闭环），唯二回切=野怪战斗锁（领土内战斗热点强制 Fine）或玩家视野覆盖——**三考（HH.80）全程 Fine 的隐含依赖=野怪日均杀工 3 的战斗热点持续强制 Fine**（D5 起野怪杀工事件在案）。即：AI 自主发展当前隐含依赖「被野怪打」或「被玩家看到」两类外部刺激，零干预自主到军事期被结构阻断。

**层叠显形链**：三考（HH.80）显形 Fine 态三卡死点（⑥守卫恒拒/房容 0 生育挡/野怪杀工）→HH.81+HH.86 治本修通→四考 Fine 态链路实证通畅（见下正向数据）→显形上游 Abstract 冻结层（大考逐层显形链第四层）。

**正向数据（治本链路实证）**：⑥招工落地 3 笔（k2 #28/#29+k1 #36，粮-2/笔）=三考零落地根治实证；k2 D6 进扩张期（工 8 门破，三考十六日恒 3 工）；k2 扩张期焦点=屯粮（评分合理）；D5/D10/D15 检查点+回存全 True（观测链完整）；同 seed 复现（裸跑局与新局事件序列 #28 一致）。

**复现与复核包**：seed=31415/槽=p1_run4；首跑 40 分钟为裸跑作废（观测器编辑态启动被进 Play domain reload 清空——**观测器须在 Play 态内启动**，三考 L34282「启动快照 day=1」=Play 内启动先例，本轮流程笔记列报）；复核包=p1_log_20260906_144512.log（907KB 全镜像）/p1_snap.csv（76KB）/p1_run4_day005~015 检查点/p1_run4_terminal_D19.json 封盘镜像/直查三笔（焦点·流浪池·simMode）/p1_run3 系列原封。

**判定：🔴 熔断（非 PASS）**——Abstract 冻结层归策划端裁决。修法候选（均 sim 域/设计域，执行端未动手）：①AbstractEconomySettler 补人口增长/建造/军事镜像公式（Abstract 挂机国公式化发展）②活跃带判定放宽（offscreenDaysToAbstract 调大/活跃带口径改「已探索」）③Abstract 期允许焦点实体执行（去掉 Tick mode 挡——但实体驱动语义与 Abstract 冲突需裁）④P1 考协议改「玩家视野周期轮巡三 AI」（玩家干预边界需裁）。

### 熔断条款执行记录

- 熔断触发：D19 手工提前（终速 0+封盘 p1_run4=2859 模块+观测器 Stop 封存）——HH80_Run 容器 D120/军事/灭绝三停条件未达（手工熔断，容器未参与终局）
- 观测器军事期计数=0（达标空）——符合熔断判定

## 六、列报汇总

1. **HH.42 复发第 8 笔（随批登记时段新实锤）**：随批①队列更新时，Read 工具视图显示 P1 行状态列「⏳ 四考待开工（前置=HH.81修复批）」——pwsh 磁盘实读为「⏳ 四考待开工（前置=HH.81）」（多「修复批」三字，HEX 字符码实证）——**Read 视图幻读第 8 笔变体**（第 7 笔=回显+视图双幻读「行在案实无」；本笔=视图多出磁盘没有的字段）。pwsh 真源+断言失败打印 HEX 上下文再修正候选=两笔同治的标准动作已入 hh83 脚本族
2. **1g 白名单实补 4 非 6**：[PopulationSystem]/[AIEconomySettlement] 已在案（任务书口径与实况差）
3. **SO 资产实况差**：任务书 3b「UtilityActionConfig.asset 现缺失」实为**内容缺失**（文件在、行动集旧版 8 条缺 6 条）——⑦招战士/⑧科技/⑨城墙/⑪⑫⑮运行时不可见=审计「AI 行动池仅 6 buildingId」的另一半病灶，本批全量重写 21 条堵死
4. **参数占位真值待调**：buildTargetCap（Warehouse/Granary=4）/WellGap needA=1/MetalGap needA=30/ExclusiveGap 底分 0.5/minStage（Blacksmith=Develop/LeyForge=Expand 偏差列报）
5. **2_20 ④d 容器口径连带**（§四）——HH.81 流浪置 -1 的容器面，清场修正+两轮实证
6. **件2b 玩家 houseFactor 输入源修正面**：旧全图房容（含 AI 房）→本国——AI 有房时旧判被污染（DZ-046 失真面），本批修正=预期，四容器实证无行为回归
7. **2d 非国库五资源溢出丢弃+日志**：ResourcePack 无 Ore/Crystal/FireOil 等桶，AI 采集溢出该类资源丢弃列报（旧=全入玩家库=DZ-045 病灶本体，丢弃=不资敌最小修）
8. **2_20C 首次调用被拒**（「须先进入 Play」提示日志在案）——流程笔记非缺陷
9. **【四考随跑观察→台账建议】⑥ Feasible 缺候选存在性判定**：Feasible(RecruitWorker) 只判粮+工人<目标，不判流浪池有无同族候选——k1/k3 流浪池耗尽（全异族）后 ⑥ 仍 Feasible=true→评分 top 连任→焦点锁死（ExecuteRecruitWorker 无候选 Bump fail 每日空转，建造/House 行动永不上位）。观察面：自然补员（TryNaturalRespawn 5 日/组 2+族池 1/4 同族）能否解锁；若四考熔断此列新层实锤（守卫面修法候选=Feasible 加「存在可招候选」判定——**未扩权现场修，归策划裁**）
10. **【四考随跑→新层列报】Abstract 冻结层**：详见 §五判定节——离屏 AI 焦点执行全停+Abstract 无人口/建造公式+切 Abstract 静默=零干预自主发展被结构阻断（本批 16 条之外的上游层，sim 域归策划裁）；关联观察：建造链两个静默点（FindAIBuildSpot 无落位 L374/TryBuild false L380 均无日志）建议随 Abstract 层修法一并补观测口
11. **观测器启动时序纪律（流程笔记）**：P1 观测器须在 **Play 态内**启动（编辑态启动会被进 Play domain reload 清空订阅与静态态=裸跑；三考先例 L34282「启动快照 day=1」即 Play 内启动）；HH.80 三考流程笔记「编辑态启动」口径据此勘正

## 七、DZ-040~065 修复状态建议回填表（16 条本批+10 条域外）

| DZ | 描述 | 本批处置 | 建议 |
|---|---|---|---|
| DZ-040 | AI 建筑 faction=PlayerCamp 断层 | ✅件2a 三处派生 | 转✅（验收后） |
| DZ-041 | Warehouse/Granary 空壳连轴建 | ✅件3a①挂载+②上限守卫 | 转✅ |
| DZ-042 | RiverBay 漏井漏房 | ✅件1a 兜底 | 转✅ |
| DZ-043 | Well 损毁无重建 | ✅件3b BuildWell 通道 | 转✅（真值待调） |
| DZ-044 | 挑水链错桶 | ✅件2c 双参两处 | 转✅ |
| DZ-045 | 采集溢出资敌 | ✅件2d 分流 | 转✅ |
| DZ-046 | 幸福四因子失真 | ✅件2b per-kingdom | 转✅ |
| DZ-047 | 将军计数全局 | ✅件1c per-kingdom | 转✅ |
| DZ-052 | AI 铁链断 | ✅件3b BuildBlacksmith | 转✅（真值待调） |
| DZ-053 | 四专属 AI 恒 0 | ✅件3c 四行动+族门禁 | 转✅ |
| DZ-057 | warriorCount 缺 M7 | ✅件1b 七职业 | 转✅ |
| DZ-058 | 玩家配对池反向窄轨 | ✅件3d 对齐 | 转✅ |
| DZ-059 | 玩家出生落点无过滤 | ✅件1e | 转✅ |
| DZ-060 | 玩家配对 Random 未种子化 | ✅件1f | 转✅ |
| DZ-064 | Porter 白训 | ✅件1d | 转✅ |
| DZ-065 | 观察器白名单缺 6 tag | ✅件1g 实补 4 | 转✅ |
| DZ-049 | 四死资产 | 域外（审计片1） | 维持台账 |
| DZ-054~056 | 招工派工片余项 | 域外 | 维持台账 |
| DZ-059~060 外余/061~063 | 资源经济域 | **不越界条款**：归 2_23 批B/P0 调优 | 维持台账 |

## 八、纪律自查

| 项 | 兑现 |
|---|---|
| 玩家侧零回归 | ✓ 四容器全绿（件2/3 全部 per-kingdom 改造玩家桶 0=原逻辑逐位；2d 分流玩家 0=Ruler 原路；2b 玩家四因子=Ruler/PopulationCount 原口径） |
| 确定性 | ✓ 件1f 种子化（种子源 map.seed 报备）；无新增未种子 Random |
| git diff 自查+grep 双锚点 | ✓ 件1~3 每件 pwsh Select-String 复验（HH.42 第 8 笔纪律：两处复验脚本期望值误判已用 pwsh 磁盘真源澄清——复验正则缩进漏配/行计数口径，文件本体无误） |
| 雷区 35 处 | ✓ 本批新增遍历（KingdomBrain 诊断日志/④d 清场/CountActiveDef）均只读或改字段零结构变更；Spawn 不入遍历体 |
| 不越界 | ✓ DZ-061/062/063 零触碰；顺手发现未扩权 |
| 冒烟写单位走生产 Prefab 实例化 | ✓ 本批零新增冒烟造单位（复用容器） |

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 件1~4 施工与探针验收 | ✅ 成立（D546，策划端实盘复核 2026-09-06，16/16 条抽查直验零失实） | 抽查覆盖：2a 派生三处+残留清零 grep / 2c L583 双参 / 2d L602/606/623 分流 / 3a 挂载 L283-285+守卫 L312-322 / 3b 枚举 16~21+NeedKind+NeedScore / 3c / asset 21 条 id1~21 无重复（unicode 名核） / 3d / 1b 七职业 L120-128 / 1c L301+L538 / 1d Porter L228-231 / 1e L474 / 1f pairRng L308-317 / 1g 白名单 L49 / 1a RiverBay 序列逐字吻合 |
| P1 四考结果（冲军事期/新层暴露） | 🔴 熔断认可（D19 提前，Abstract 冻结层定性采信） | 正向数据=治本链实证（⑥招工 3 笔+k2 D6 扩张+检查点全 True+同 seed 复现）；新层=离屏 AI 发展结构阻断（Tick L92-93 Fine 挡 ExecuteFocus+Settler 无发展公式+切 Abstract 静默）；D539 同精神勿硬拖；复核包四件在场验证（工程内 Logs/P1） |
| 评分收敛参数真值（限建上限 SO） | 占位放行，归 P0 调优批 | buildTargetCap=4 等真值待 P1 长局数据；与 P0_端到端调优_归拢清单 合流 |
| 行动池新行动参数（Well/Blacksmith/四专属 minStage） | 照准（LeyForge=Expand 偏差认可） | 纯经济建筑 mineMul 加成面，扩张期开建贴合功能；余占位随 P0 调优 |
| DZ-040~065 状态批量回填 | ✅ 批准 16 条转✅ | 台账已同步落档（D546）；DZ-049/054~056/061~063 维持域外 |
