# HH.169 2_23 资源 P0 批A 完成报告（经济诊断块真值 R-A1/R-A2/R-A3）

> 类型：交付报告（Gate 收口）
> 状态：✅已验收（D632，2026-09-11）
> 日期：2026-09-11 · 发起端：执行端 · 关联：`改造计划/2_23_AI王国脑总纲_资源P0实施清单.md` §〇/§一/§四/§六 · 0.6 §一百五十八（D629）/§一百五十九（D630）/§一百六十一（D632）· HH.167 §策划裁决 · 2_23 总纲 §2.1/§3.3/§六/§七/§八

---

## 一、做了什么（R-A1 / R-A2 / R-A3）

### R-A1 经济诊断块真值（新建 `Systems/AI/KingdomBrain/EconomyDiagnosis.cs`）

**新建**：
| 文件 | 内容 |
|------|------|
| `Assets/_Game/Systems/AI/KingdomBrain/EconomyDiagnosis.cs`（350 行，**零 `using UnityEngine`**，唯一 using＝`System`/`System.Collections.Generic`＝对齐 `AbstractEconomySettler` 纯 C# 先例） | 四件真值本体＋收支窗口累计＋DTO（`EcoResource`/`ResourceFlow`/`ProductionEntry`/`MustHaveMiss`/`MustHaveBaseline`/`EconomyInput`/`EconomyBlock`） |
| `Assets/_Game/Systems/AI/KingdomBrain/SituationSnapshot.cs`（改） | ②经济诊断块 **占位 → 真值**：`bool EconomyBlockPlaceholder` → `EconomyBlock Economy`；`SituationHub.Remove/Clear` 联动清收支窗口（八格 5/7） |
| `Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs`（改，+121 行） | 新增 Unity 侧适配层 `BuildEconomyBlock`（取数→DTO）+ `MapProduceToEco` + `QueryKingdomBuildingsSorted`（固定排序＝⑤-3 硬性 a 同款）；`BuildSituation` 尾部挂 `snap.Economy = BuildEconomyBlock(k, day)` |
| `Assets/_Game/Systems/Kingdom/KingdomState.cs`（改，+3 行） | `AddResources`/`Spend` 各加 1 行 `EconomyDiagnosis.RegisterFlow(...)`（入账/扣减登记，D630 A+ 口径） |

**四件真值（2_23 §2.1 逐项对位）**：
| # | 2_23 §2.1 要求 | 落地字段（`EconomyBlock`） |
|---|---------------|--------------------------|
| ① | 五元资源收支（日入/日出/净流量 per 资源＋劳力口径） | `Flow`（`goldIn/stoneIn/woodIn/foodIn/metalIn` × `…Out`）＋`In()/Out()/Net()` 取值口＋`StockGold~StockMetal`＋劳力口径 `Population/WorkerCount/WarriorCount/IncomePerWorker`（人均日入） |
| ② | 产能盘点（产能建筑×类型×产出率、farm/人口比、采集点饱和度） | `Production`（`EcoResource` 升序，每项 `Count/LevelSum/RateSum/WorkersSum`）＋`CapacityBuildingCount`＋`FarmCount/FarmPerPop`＋`GatherNodeCount/GatherSaturation` |
| ③ | 断链检测（基线 vs 现有→缺失清单） | `MissingMustHave`（`RequiredKind` 升序：0=Farm/1=Warehouse/2=Fortification；带 `Required/Actual`）＋`MustHaveBaselineCount` |
| ④ | 储备水位（粮裕日/仓储占用率） | `GrainReserveDays`＋`StorageUsed/StorageCapacity/StorageOccupancy` |

**纯函数/无持久态（D630 A+ 口径落地）**：
- **日入＝增量和／日出＝消耗和／净流量＝入−出**；取数＝`EconomyDiagnosis.RegisterFlow` 在**入账/扣减时登记**的窗口累计；`Build` 时 `TakeFlow` **读取即清零**（`KingdomBrain.cs` L462），窗口字典随 `SituationHub.Remove/Clear` 清空 ⇒ 无持久态、不入档。
- 窗口语义（如实注记）：快照重建发生于 `DayCycleSettlement` **步骤②**（早于步骤⑤ 日结入账）⇒ 窗口中累计的是「**自上次重建以来**」的流量＝脑中「昨日结存」口径（与 `UtilityScorer.NeedScore` 缺口口径一致）。
- **零 2_12 资源链改动**：未改任何资源链/仓储/搬运/造价语义；`AddResources/Spend` 只在原有赋值之后**追加一行只读登记**（不改字段值、不改返回值、不改分支）。

### R-A2 `KingdomDiagnosisConfig` SO（新建）

`Assets/_Game/Data/Kingdoms/KingdomDiagnosisConfig.cs`＋资产 `Assets/_Game/Resources/Config/KingdomDiagnosisConfig.asset`（`Resources.Load("Config/KingdomDiagnosisConfig")` 实测可载 ✅）。字段与 2_23 §七 职责逐项一致：**产能基线**（`capacityPerPeopleBaseline`）／**水位阈值**（`grainReserveDaysFloor`＝对齐 `KingdomBrainConfig` 既有口径、`storageOccupancyThreshold`）／**三通道判据**（`channelBOutputPerPop`＋`gatherNodePerPopBaseline`＋`shortageSeverityOrder`＝粮→金→石→木 前置）／**储备目标**（`reserveTargetDaysGrain/Other`）＋粮耗口径 `grainConsumptionPerPop`。

### R-A3 `MustHaveConfig` SO（新建）

`Assets/_Game/Data/Kingdoms/MustHaveConfig.cs`＋资产 `Assets/_Game/Resources/Config/MustHaveConfig.asset`（实测可载 ✅，`tiers=3`）。结构：`TierRule[]`（`minPopulation` 人口规模档主轴 × `farmPerPeople`／`minWarehouse`／`wallTargetCount`[⑨既有] × `staggerTierMin` 预留）＋`ResolveTierIndex(pop)`（确定性：取 `minPopulation ≤ pop` 的最大档）＋`ResolveBaseline(pop)` → `MustHaveBaseline`（farm 需求＝`ceil(pop/farmPerPeople)`）。诊断层日 tick 比对 → 缺失清单进快照 ✅。

---

## 二、门禁与自查（逐条留证）

| 门禁 | 结果 | 证据 |
|------|------|------|
| 编译 0 警 0 错 | ✅ | `refresh_unity(compile=request)` 后 `read_console`：**error 0 条**；warning 15 条**全为既有基线**（ChestManager/ToastManager/IUIPanel/ResourceRespawnSystem/BuildingMenuPanel/ProjectileManager/NPCBrain/FormationPanel/CameraSetup/VisionSystem/PathfindingScheduler）——**本批 6 文件零告警**；Editor.log（`%LOCALAPPDATA%\Tuanjie\Editor\Editor.log`）尾段 4MB 内 `error CS` **= 0** |
| 四件真值可产出（静态/反射探活） | ✅ | Unity Editor 反射探活实录：`①net(g/s/w/f/m)=20/8/6/15/2 pop=12 inPerWorker=11.63; ②prodEntries=2 capTotal=5 farm=3 farmPerPop=0.250 gatherSat=1.000(nodes=3); ③missing=2/3 [kind1 req1 act0][kind2 req2 act1]; ④grainDays=2.50 storageOcc=0.600(120/200)`；类型在场（`unity_reflect` 命中 `EconomyDiagnosis`∈Assembly-CSharp） |
| 同 seed 确定性（纯函数） | ✅ | 探活：**同输入复算逐项全等=True**；窗口 `in=5 out=2 net=3 → 读后清零=True` |
| **既有冒烟零退化** | ✅ | **`Smoke_2_22P0` run1（正门 `TestHarnessApi.EnterTestRun`，seed 21140，15x）= PASS 32 / FAIL 0**；落盘 `Valley Rampart/Logs/P1/smoke_2_22p0_run1.log`（逐条 P1a~P9f 全 PASS，含 P7 读档恢复三段/P8 选址确定性/P9 机器双行动） |
| 交付前 `git diff HEAD` 全量 | ✅ | 3 改（KingdomBrain +121/‑/ SituationSnapshot / KingdomState）+ 6 新（3 `.cs`＋3 `.asset`，各带 `.meta`）＝**批外零混入** |
| 逐项在场性 grep 双自查 | ✅ | ①`EconomyDiagnosis.RegisterFlow` 消费点＝`KingdomState.cs:156`(Spend)/`:179`(AddResources) 恰 2 处 ②`BuildEconomyBlock`＝`KingdomBrain.cs:397`(调用)/`:423`(定义) ③旧占位 `EconomyBlockPlaceholder` **全库 0 残留** ④`AI.Core` 目录 `git status` **空**（红线①亲验） |
| 红线②零改动声明 | ✅ | 本批 diff 面仅 6 文件；**未触碰**效用四因子（`NeedScore` 逻辑不动）／焦点防抖／常设底线机制／2_12 资源链／训练系统结构／玩家建造入口 |
| 红线③ TaskScheduler 死表 | ✅ | 本批**未触碰** `TaskScheduler.cs`（排序键升级＝批B R-B1） |
| 红线④确定性 | ✅ | 见上（纯函数＋固定排序遍历＋无随机/无时间源） |

---

## 三、列报（请裁 / 请过目）

**列报 1（🔴 请裁）＝日出/日入 覆盖缺口：台账 API 存在直写旁路（未擅改链）**
实盘 grep：AI 五资源台账主 API＝`KingdomState.AddResources/Spend`（已挂钩，覆盖 `AIEconomySettlement:55`／`AbstractEconomySettlement:230`／`TaxSystem:162`／`TaskScheduler:658`／`BuildController:330`／`TrainingSystem:514`／`KingdomBrain:711/742/948`／`SiegeProductionSystem:213`），**但有 4 处直写绕过 API**（均 AI 侧 `id>0`）：
- `RanchSystem.cs:104` `k.resources.gold -= d.youngCost`（买幼崽）
- `RanchSystem.cs:181` `k.resources.food -= amount`（喂粮）/`:209` `k.resources.food += meat`（产肉）
- `SatietySystem.cs:220` `kingdom.resources.food -= dailyFoodCost`（AI 进食扣粮 D453）

⇒ 牧场/饱食的**金/粮增减暂不进收支窗口**（通道B「日产出低」判据与「日出」列在批C 消费时会低估消耗；④储备水位用**存量**算＝不受影响）。
**未擅自改链**（遵 D630③）。请裁：
- **A（推荐）**：授权本批**顺带收口 4 处直写**（`RanchSystem` 2 处 → `Spend`/`AddResources`；`SatietySystem` 1 处 → `Spend`；`:209` 产肉走 `AddResources`）——**语义等价**（同为五元台账增减）、量级小；**负探针**＝牧场/饱食行为字段前后逐位一致；
- **B**：另立微批（本批保持缺口注记，批C 消费前补齐）；
- **C**：接受缺口（声明「收支窗口只覆盖台账 API 面」——但批C 通道B 判据需相应降级口径）。

**列报 2（请过目）＝口径自拟三笔**（设计稿未逐字给出，执行端按最小可算口径落地并注记）：
1. **采集点饱和度** ＝ 本国 `def.isResourceNode` 资源节点数 ÷ max(1, 人口×`gatherNodePerPopBaseline`)（clamp01）。**未展开领土格级地物节点**（Tree/Mine 需中区块→格展开，成本与确定性风险高）——如需格级口径请裁。
2. **粮裕日** ＝ `StockFood ÷ max(1, 人口×grainConsumptionPerPop)`（对齐 `KingdomBrainConfig.grainConsumptionPerPop`／`UtilityScorer.PerPopGrain` 既有口径）；**未采用** Abstract 公式的 Life/Soldier/Elite 加权口径（`AbstractEconomySettler` 用 `1/2/3` 耗粮档）——两口径并存，本层选前者（与存活判据同源），如需统一请裁。
3. **仓储聚合** ＝ Σ 本国建筑 `StorageComponent.storedAmount/capacity`（含 farm 等一切带仓储组件建筑）；**Warehouse 计数** ＝ `def.id ∈ {"Warehouse","Granary"}`。

**列报 3（请知悉）＝错峰档运行时不可读 → 锚点主轴改用人口规模档**
实盘复核：`staggerTier` 仅在 `KingdomFoundry` 立国时由全局 `difficulty` 分解消费，**`KingdomState` 不存该字段**（无 per-kingdom 读口）。故 `MustHaveConfig` 以**人口规模档**为主锚（§3.3 原文「错峰档/人口规模」两者并列，取可读者），`staggerTierMin` 字段留结构待后续批。

**列报 4（请知悉）＝内源势能三输入本批未接替（防行为漂移，L-01 义务转批B/C）**
`KingdomBrain` 注释原写「R-A1 真值块落地后接替」`DriveEconomic/DrivePopPressure/DriveStorage`。本批**未接替**（保留现算口径）——理由：三输入进 `InternalDrive → NeedScore` ＝效用四因子行为面，接替＝行为漂移，与本批「零行为消费」冲突。**消费义务已就近注记**（`L-01 就位≠生效`）：接替与批B/C 消费扩展同批一次到位。

**列报 5（请知悉）＝本批无 sim 义务**
R-A1/R-A2/R-A3 均为 **Unity 侧纯新增**（诊断块 sim 镜像＝S-B1 归批C R-C5 跨仓协同）；未触碰 `AI.Core`、未改 `TuningSnapshot/champion/factor_registry` ⇒ **15_账本本批无登记义务**（S-B5 归批B R-B2）。

**列报 6（批外观察，非本批引入）**：`Smoke_2_22P0` 跑批期间 Console 出现 9 条 `[BuildingFactory] SpawnFromSave 冲突…`（Error 级）——查证＝**既有「响亮断言」设计**（`修复记录/BUGFIX_读档建筑双份.md` L44：故意 LogError 打双方 saveId，不跳过不吞）；P7a~P7d 读档探针**全 PASS**，本批 diff 未触碰 Building/存档面。

---

## 四、下一步建议

- 待策划端验收 HH.169 → 批A 收口 → 解锁 **批B（R-B1 派工活权重）**（依赖 `R-B1 ← R-A1` 已达成＝本批交付的信号源「断链检测/产能缺口」已就位）。
- 若列报 1 裁 A：本端即刻收口 4 处直写旁路（同批追加，补负探针）。
- 批 commit 未执行（待验收/授权；遵「只提本批文件、禁 `git add -A`、不 push」）。
- 队列「2_23资源P0批A」行状态更新归**策划端**（队列 Owner=策划端单点维护）。

---

## 策划裁决（策划端回写，裁决前保持空白）

> **判决（D632，2026-09-11，策划端）＝验收成立 + 列报六项全裁 + 独立复核增补 1 项**。
> **判据三直读＝已过**（①设计稿全文：2_23 资源P0 清单 §〇/§一/§四/§五/§六 + D630 裁决原文 0.6 §一百五十九 逐字读；②代码 `file:line` 实读：`EconomyDiagnosis.cs` 全文／`SituationSnapshot` L105-107+L132-143／`KingdomBrain` L390-519／`KingdomState` L156/L179／四处旁路 file:line／两 SO 全文；③档位/字段直读：SO 出厂值＝保守占位，免反推）。
> **独立复核（零采信报告自述）全中**：新文件 git status=10 项（5 主体+5 meta）｜三改动 diff=KingdomBrain +121／KingdomState 实体 3 行（含注释 +9）／SituationSnapshot 28｜`git grep EconomyBlockPlaceholder`=**0 命中**｜`smoke_2_22p0_run1.log` 在场（2422B/33 行）｜`git diff --name-only` Ranch/Satiety=**空**（本批确未擅改链）⇒ **报告零失实，交付可信**。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 列报 1：4 处台账直写旁路处置（A 本批顺带收口 / B 另立微批 / C 接受缺口） | **裁 A′＝授权本批顺带收口，但范围扩至 5 处**（执行端列 4 处 + **策划端独立全库清点增补第 5 处 `KingdomState.Refund` L160-166**） | 判据三直读已过。**A 语义等价成立**（三处同为五元台账增减，`RanchSystem` 买崽/喂粮/产肉、`SatietySystem` 进食扣粮均落 `KingdomState.resources` 五元桶；改走 `Spend`/`AddResources` 只多一次 `RegisterFlow` 登记，不改值/不改返回/不改分支）。**否决 B**（另立微批＝徒增跨批协调成本，且缺口在批C 通道B 消费前必须清零，就地收口最省）。**否决 C**（接受缺口＝批C 通道B「日产出/人口低」判据输入失真，判据降级＝埋雷给批C）。**A′ 扩面依据**：`Refund` 亦为「绕台账 API 直写点」（L162-166 五行 `+=` 无 `RegisterFlow`），**D628 T4(b)「绕 API 落点须全库清点」纪律的台账版实例**——执行端列报 4 处但未做全库清点（漏 1 处），**此项按 D628 T4(b) 嘉奖执行端 4 处主列 + 策划端补全第 5 处**，A′ 一并收口。**验收（负探针）**：改后 `RanchSystem`/`SatietySystem`/`KingdomState.Refund` 三处行为字段（金/粮存量、返回值、分支）前后**逐位一致**；`RegisterFlow` 消费点＝`AddResources`/`Spend`/`Refund` 三处齐（grep 双锚点）。**注**：`Refund` 玩家侧（id=0）由 `RegisterFlow` 内部 `kingdomId<=0 return` 自动豁免，无副作用。**精度注记（判据②实读确定调用面）**：`KingdomState.Refund` 系**潜伏点**而非当前活跃缺口——AI 生产调用点=**0**（`Building.Demolish` 由 `isPlayerBuilt` 门控 L583→AI 建筑不走拆除；玩家拆除走 `RulerController.Refund` 独立玩家通道 L596，不入 AI 台账；唯一调用=smoke `Valley2_17_Smoke_Treasury2a.cs:49`）⇒ 收口它=**为一致性 + 防未来潜伏陷阱**（AI 拆除/退款链若在 2_19/2_23 P2 接线，缺登记即成流量缺口），**非更正当前活跃流量**；执行端列 4 处均为活跃 AI 流量（Ranch 买崽/喂粮/产肉、Satiety 进食），据此判定其列报**主列准确、仅覆盖面欠全库清点** |
| 列报 2：三笔口径自拟是否认可（采集点饱和度/粮裕日/仓储聚合） | **认可三笔口径，附 1 条修正** | ①**采集点饱和度**＝`isResourceNode` 节点数 ÷ max(1,人口×人均期望) clamp01：**认可**（格级展开成本高+确定性风险，**未展开领土格级地物**＝合理最小可算口径，**注记「实体型资源点口径」**；非 `def.isResourceNode` 的纯数据格 Tree/Mine 不入＝口径边界已明示）。②**粮裕日**＝`StockFood ÷ max(1,人口×grainConsumptionPerPop)`：**认可**（与 `KingdomBrainConfig.grainConsumptionPerPop`/`UtilityScorer.PerPopGrain` 同源＝与存活判据一致；**不采用 Abstract 的 Life/Soldier/Elite 1/2/3 加权口径**——两口径并存，**本层选存活判据同源者正确**，Abstract 加权口径归抽象期执行面；**须注记两口径并存**已如实）。③**仓储聚合**＝Σ `StorageComponent.storedAmount/capacity`（含 farm 等一切带仓储组件建筑）+Warehouse 计数＝`def.id ∈ {"Warehouse","Granary"}`：**认可** |
| 列报 3：错峰档不可读 → 人口规模档为主锚 是否认可 | **认可**（含 1 条后续批义务注记） | 独立复核 `staggerTier` 全库＝仅 `KingdomFoundry` L31-34 立国时消费、`KingdomState` 不存 ⇒ **无 per-kingdom 读口属实**；`§3.3` 原文「错峰档/人口规模」两者并列、取可读者＝**正确**。`staggerTierMin` 字段留结构＝合规（**批C/重构批接入义务注记**：若后续批能读错峰档，须回填该锚点，防悬空字段＝L-01 同族） |
| 列报 4：内源势能三输入接替延至批B/C 是否认可 | **认可（嘉奖守边界）** | `DriveEconomic/DrivePopPressure/DriveStorage` 进 `InternalDrive→NeedScore` ＝效用四因子行为面，本批接替＝行为漂移，**撞本批「零行为消费」＋红线2「效用四因子零触碰」** ⇒ **不接替正确**；L-01「就位≠生效」义务已就近注记、消费义务转批B/C＝**正确处置，嘉奖** |
| 列报 5：本批无 sim 义务/15_账本登记是否认可 | **认可** | R-A1/R-A2/R-A3 均 Unity 侧纯新增、未触 `AI.Core`（独立复核 AI.Core 目录 git status 空）、未改 `TuningSnapshot/champion/factor_registry` ⇒ 本批无登记义务；S-B1（诊断块 sim 镜像）归批C R-C5 跨仓协同＝**归口正确** |
| 批A 验收与批B 解锁 | **验收成立（HH.169 销号）+ 批B 解锁（附 1 前置）** | 三直读过+独立复核全中+列报处置毕 ⇒ **验收成立**。**批B（R-B1 派工活权重）解锁**——依赖 `R-B1←R-A1` 已达成（断链检测/产能缺口信号源就位）。**前置附条**＝批B 开工前须确认 A′ 五处收口已落盘（否则派工偏向信号仍缺牧场/饱食/退款三面流量） |

### 衍生产物
- **D632**（0.6 §一百六十一）＝本验收 + 列报六裁 + A′ 扩面。
- **A′ 收口任务**（执行端）：`RanchSystem.cs:104/181/209`→`Spend`/`AddResources`；`SatietySystem.cs:220`→`Spend`；**`KingdomState.cs:160-166 Refund`→`AddResources`**（或内部调用 `AddResources`）；附负探针（三处行为字段前后逐位一致）+ grep 双锚点（`RegisterFlow` 消费点＝AddResources/Spend/Refund 三处）+ 既有冒烟零退化；同批追加，随批B 开工前收口。
- **列报 2/3 注记义务**：两口径并存注记（粮裕日 vs Abstract 加权）已如实；`staggerTierMin` 悬空字段回填义务挂批C/重构批。
- **教训核查**：本串**新增 1 条教训候选**＝**「绕台账 API 直写点必须全库清点，禁凭主 API 面推断覆盖完整」**（执行端列 4 处、实为 5 处；与 **L-15「挂账≠承接」同族**——均为「凭单一视图/自陈推断完整性」的漏项）。**是否入册**：与 L-15 同族 ⇒ **不新立独立条目**（防库膨胀，`vr-planner-leadership` 反模式），**在 L-15 条下加实例注记**（Round 台账直写清点，D632）。

---

## 五、A′ 收口执行（D632 前置附条兑现，2026-09-11）

> 依据：D632 列报1 裁 **A′**（授权本批顺带收口，范围 4→5 处）＋前置附条「批B 开工前须确认 A′ 五处收口已落盘」。
> 执行端自认：原列 4 处系**凭 `\.resources\.` 前导点写法 grep** 推断覆盖，漏掉 `KingdomState` 内部无点写法 ⇒ 命中策划端点出的盲区（L-15 同族）。

**① 全库清点（先清后改，补课）**：`Resources\.(gold|stone|wood|food|metal)\s*(\+=|-=|=)` 全库扫（含无前导点）⇒ 生产面直写点＝**恰 5 处**：`RanchSystem.cs:104/181/209`、`SatietySystem.cs:220`、`KingdomState.cs:162-166`（`Refund` 五行）。策划端 5 处判定**穷尽、无第 6 处**（其余命中全在 `Assets/Editor/Smoke/*` 测试面，非生产路径）。

**② 五处收口（全部改走台账 API）**：
| # | 位置 | 改法 |
|---|------|------|
| 1 | `RanchSystem.cs:104` 买幼崽扣金 | `k.resources.gold -= d.youngCost` → `k.Spend(new ResourcePack { gold = d.youngCost })` |
| 2 | `RanchSystem.cs:181` 喂粮扣粮 | `k.resources.food -= amount` → `k.Spend(new ResourcePack { food = amount })` |
| 3 | `RanchSystem.cs:209` 宰杀产肉入粮 | `k.resources.food += meat` → `k.AddResources(new ResourcePack { food = meat })` |
| 4 | `SatietySystem.cs:220` AI 进食扣粮（D453） | `kingdom.resources.food -= dailyFoodCost` → `kingdom.Spend(new ResourcePack { food = dailyFoodCost })` |
| 5 | `KingdomState.cs:160-166` `Refund` 五行 `+=` | 五行直写 → **内部改走 `AddResources`**（`Mathf.RoundToInt(cost.X * ratio)` 口径逐字保留；潜伏点：AI 生产调用点=0） |

**③ 验收证据（四项）**：
- **负探针（行为字段前后逐位一致）** ✅：Unity 反射探针实录 `[1]Spend 存量逐位一致=True A=(460,180,188,270,44) B=(460,180,188,270,44); [2]Refund(0.5) 逐位一致=True A.gold=480 B.gold=480; [3]AddResources 逐位一致=True food=292/292`；窗口登记实测 `B(新API)in=61 out=108`（＝Spend 出 108／Refund 入 54＋Add 入 7）；**玩家 id=0 豁免登记=True、p.gold=90**（`RegisterFlow` 内 `kingdomId<=0 return` 无副作用）。
- **grep 双锚点** ✅：①生产面 `resources.X` 直写点＝**0 残留**（收口后复扫）②`EconomyDiagnosis.RegisterFlow` 消费点＝`KingdomState.cs:156`(Spend)/`:184`(AddResources)；**精度注记**＝`Refund` 采用**经 `AddResources` 复用**实现（非第三处直接调用）⇒ 直接调用点实为 **2 处**、覆盖 **AddResources/Spend/Refund 三入口**（等价达成策划端「三处齐」要求，形态差异如实标注）。
- **既有冒烟零退化** ✅：`Smoke_2_22P0` run1（正门，seed 21140，15x）**PASS=32 / FAIL=0**，日志 `Logs/P1/smoke_2_22p0_run1.log`（2422B，与批A 首跑同字节数）。
- **编译 0 警 0 错** ✅（`refresh_unity(compile=request)` 后 error 0；新增告警 0）。

**④ diff 面（本批合计）**：5 改（`KingdomBrain` +121／`SituationSnapshot` 28／`KingdomState` +26·−／`RanchSystem` 6／`SatietySystem` 2）＋ 6 新（3 `.cs`＋3 `.asset` 含 meta）＝**161 insertions / 22 deletions**；`AI.Core` 目录 git status **空**；批外零混入。

**⑤ 口径注记（呼应 D632 精度注记）**：第 5 处 `Refund` 系**潜伏点**（AI 生产调用点=0；玩家拆除走 `RulerController.Refund` 独立通道，不入 AI 台账）——收口目的＝**一致性＋防未来潜伏陷阱**（AI 拆除/退款链 2_19/2_23 P2 若接线即成流量缺口），**非更正当前活跃流量**；前 4 处为活跃 AI 流量。

**⑥ 列报 2/3 注记义务兑现**：粮裕日（存活判据同源）vs Abstract 1/2/3 加权**两口径并存已在代码注释如实标注**（`EconomyDiagnosis.cs` 头部「粮裕日」段）；`staggerTierMin` 悬空字段**回填义务已注记于 `MustHaveConfig.cs` 字段 Tooltip**（「运行时无 per-kingdom 读口，-1=不使用（留结构待后续批）」，挂批C/重构批）。

### A′ 收口裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| A′ 五处收口是否验收（含负探针/grep 双锚点/冒烟零退化） |  |  |

