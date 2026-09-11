# HH.180｜2_23 资源 P0 批C 完成报告（R-C1~R-C5）

> 执行端（Unity 轨）· 2026-09-11
> 状态：⏳ 待策划验收（批 commit 已随本报告落盘，未 push）
> 锚点：D638（0.6 §一百六十七，2026-09-11）=批B 验收成立+批C 解锁；D639（0.6 §一百六十八，2026-09-11）=批C 开工准+列报1~8 逐裁（通道B 信号=`Flow.In(r)/pop`、铁不参与触发）
> 编号：**HH.177 撞号顺延 HH.180**（美术端 09-10 先落盘占用 HH.177 且 D641 已裁决；HH.178=训练轨 F26、HH.179=美术端；D639 顺延裁定漏见此号，用户拍板顺延 HH.180——撞号纪要见 §八）

## 一、五件任务完成描述

| 任务 | 交付物 | 要点 |
|------|--------|------|
| **R-C1** ⑤屯粮根因三通道分诊 | [KingdomBrain.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs)（`ExecuteGrainTriage` L1226 / `ResolveTriageResource` L1287 / `DecideTriage` L1265 / `TriageDecision` 枚举 L1386） | 触发（粮=底线日告警既有；金/石/木=净流量负+水位<基线，单日判定 D639 列报1/2）→ 严重度 粮→金→石→木 取首发（**铁不参与** D639 列报3）→ A→B→C 固定序命中即止、单通道不叠加（列报7）。通道A=无对应产能建筑+反查表有 def→建（列报4）；通道B=实际日产出/人口低（`Flow.In(r)/pop`<阈值，D639 列报5 改裁）→ 偏向批B 生效⑤不建（Bump ok）；通道C=仓储占用溢出→粮建 Granary/非粮建 Warehouse（列报6）。`ExecuteFocus` Grain 分支路由（L573）。纯函数无持久态（D630 A+ 口径）。 |
| **R-C2** ③建产能种类诊断化 | `ExecuteFocus` BuildCapacity 分支→`ResolveTriageDefId`（L568/L1329）+ `ExecuteBuildFocus(kingdom, cfg, overrideBuildingId)`（L1174） | 不再恒 quarry：按快照产能缺口（Count/pop 升序）解最缺资源 → `KingdomDiagnosisConfig.triageCapacityDefs` 反查表（SO，列报8）得 def 动态建；无缺口/表缺省 → null=沿用 SO buildingId。`CapacityGap` 改读快照 `CapacityBuildingCount`（2_22 P0 批A A4 承接）。 |
| **R-C3** MustHave 断链注入 | [UtilityScorer.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs) `NeedScore` 前置块（L151-172） | 断链缺失（批A 比对进快照）→ 对应建造行动 NeedScore=1.0 拉满（kind0=Farm→④/farm、kind1=Warehouse→②、kind2=Fort→⑨）；经 ScoreTop argmax 自然选中，**不设第五底线级**（常设底线三级序 粮→人口→被攻 不动=FocusController 红线复核）。负探针=招工行动不受注入。 |
| **R-C4** Smoke_2_23RP0 冒烟容器 | [Smoke_2_23RP0.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Smoke_2_23RP0.cs)（新建，Editor-only，自含禁挂 SmokeApi） | P1 正门进局（EnterTestRun）+反射面；P2 触发判定 5 探针；P3 三通道固定序 6 探针（A→B→C→非粮 C→固定序→确定性复算）；P4 R-C3 注入 5 探针（三断链 need=1.0+招工负探针）；P5 集成：通道A 建 farm 落地（E-B1）+正常态 no-op 同帧差分；P5-pre 清场前置；ExitTestRun 收尾+L-22 双跑。 |
| **R-C5** sim 镜像 S-B1 | 契约内嵌本报告 §六 | 执行端未开 AI.Core（红线）；对拍契约供训练仓侧实施；15_账本 S-B1 登记属训练仓不代提。 |

## 二、门禁证据

1. **静态门禁（纯 Shell/grep）**：
   - ① `git status --short -- "Valley Rampart/Assets/_Game/Systems/AI.Core"` ＝**空**
   - ② 变更面＝**3 改**（KingdomBrain.cs +176/−、UtilityScorer.cs +35/−、KingdomDiagnosisConfig.cs +33/−）＋**1 新**（Smoke_2_23RP0.cs + .meta）；diff 总额 3 文件 +235/−9
   - ③ 在场性 grep：`ExecuteGrainTriage` 调用点（KingdomBrain.cs L573）／`FindTriageDef` def 在场（KingdomDiagnosisConfig.cs L56，`triageCapacityDefs` L49：Food→farm/Stone→quarry）＋`Resources/Buildings/farm.asset`·`quarry.asset` 双 asset 在场／R-C3 注入点（UtilityScorer.cs L151-172 前置块）
   - ④ 常设底线序列 grep **无第五级**：FocusController 三级序（粮 L98→人口 L103→被攻 L131）全文未动，断链只走评分注入
   - ⑤ 死表红线：`TaskPriorityConfig` / `TaskScheduler.cs` **零改动**（不在变更面）
2. **行为探针**（Unity MCP 驱动，L-22 退 Play 重进）：
   - `Smoke_2_23RP0` run1 **PASS 22 / FAIL 0**（`Logs/P1/smoke_2_23rp0_run1.log`，13:05:26）
   - `Smoke_2_23RP0` run2 **PASS 22 / FAIL 0**（`smoke_2_23rp0_run2.log`，13:06:23）＝归一化（tag/时间戳剔除）**逐行一致＝确定性**
   - 既有基线 `Smoke_2_22P0` run1 **PASS 32 / FAIL 0**（`smoke_2_22p0_run1.log`，12:34:53，2422B）
3. 编译：编辑器全量编译 error CS=0、零新增告警（四文件零 error）。

## 三、容器迭代纪要（初跑 14/7 → 收口 22/0；产品代码零改动）

初跑 7 项 FAIL 全为**探针夹具/时序缺陷**，逐项根因与修正：

| FAIL | 根因 | 容器修正 |
|------|------|----------|
| P2b/c/d 金/木/铁触发 | 夹具未设 `GrainReserveDays`，粮严重序最高抢先触发 | 三夹具补 `GrainReserveDays=9` 隔离粮 |
| P3c/d 通道C | `StorageOccupancy` 是诊断块 `BuildEconomyBlock` 算好的**字段**（EconomyDiagnosis.cs L180/L305），夹具直建 EconomyBlock 时默认 0 | 夹具显式设 `StorageOccupancy=0.9/0.95` |
| P5a 建 farm（0→0） | 三层原因：①`Object.Destroy` 帧末生效（清场后需等帧）②`ExecuteBuildFocus` 首行按 `kingdom.focus` 反查行动 def（空 buildingId 直接返回；容器绕过 ExecuteFocus 路由直调须先置 focus=BoostHarvest 满足下游守卫）③farm 须建在**农田资源点**（ResourceNodeMapping/PlacementValidator 硬约束）+AI 国库须付得起造价（farm cost gold=50，开局国库不足→`CanAfford=N`）且半径 8 内无农田（`[WARN] 选址失败`） | 清场后 `yield return null`；置 focus=BoostHarvest+还原；国库注资（`k1.AddResources`）+选址半径 48（探针临时、跑后还原）；DBG/WARN 观测口埋入容器 |
| P5b no-op（0→1） | 跨帧比数捕捉到 **AI 王国帧内自行演化**（探针窗口内 AI 自行落 Warehouse——DBG 实测 `wh=1`；KingdomBrain 为纯类不可 disable；`SetGameSpeed(0)` 在考跑档被吸附 0.5x 非真冻结） | 改**同帧 before/after 差分**：两次计数间零 yield，只捕获分诊自身副作用，确定性更强 |

排雷回报：L-01（探针场景即时清理——容器自身即探针，结束后 ExitTestRun 清场无残留）／L-17（正门 EnterTestRun+ExitTestRun 收尾，无裸局）／L-02（并行写纪律——本次共享文件改动见 §七，写完随 commit 同串关窗）。

## 四、列报（待裁 3 项）

1. **`triageCapacityDefs` 出厂只配 Food→farm / Stone→quarry**（Wood/Gold/Metal 无实体产能建筑）——R-C2 验收「缺木建 lumber」在无 def 现实下**改按实际**：缺石→quarry 探针（D639 列报8 意义=「缺什么建什么」由 SO 表驱动，缺省资源自然落到通道B/C）。若需 Wood 产能（lumber）请策划端另行立项。
2. **`CountProductionOf` 沿用批A `MapProduceToEco` 口径**（含带 producer 的仓储类 Granary/Warehouse 计入产能）——可能使**通道A 难触发**（有 Granary 也计产能）。如实列报请裁：批C 维持批A 既有口径（零行为漂移）或另立窄口径（仅 farm/quarry 类）。
3. **R-C4 P5 集成探针用临时删建筑构造**（AI 王国建筑 Destroy 属探针清场，E-B1 集成需 1) 存在农田资源点 2) 国库可支付，容器以注资+扩半径整备构造可行性）——探针构造性动作不隐含生产行为假设，请知悉。

## 五、边界声明

- **AI.Core 零直改**（git status 验空；R-C5 sim 镜像走跨仓协同，执行端不开 AI.Core）
- **死表零动**（`TaskPriorityConfig`/S/A/B/C 未触碰，TaskScheduler.cs 未触碰）
- **常设底线机制不动**（三级序未改，R-C3 仅评分注入）
- **训练仓不代提**：15_账本 S-B1 登记、factor_registry 相关属训练仓，未代提
- **确定性**：同 seed（21140）同信号同输出；双 run 归一化逐行一致
- so-data-driven：触发/通道判据全走 `KingdomDiagnosisConfig` SO（缺 asset 回退占位实例）

## 六、R-C5 经济诊断块 sim 镜像对拍契约（供训练仓侧实施）

> 训练仓施行 sim-sync 纪律（先 commit→改→双门禁）；本契约仅执行端出规格，不实现 AI.Core。

**① EconomyInput 五元口径（sim 侧需与 Unity 侧逐元对齐）**
- 五资源：金 Gold=0 / 石 Stone=1 / 木 Wood=2 / 粮 Food=3 / 铁 Metal=4（EcoResource int）
- 输入元：`In(r)` 日入（当日窗口累计入账）、`Out(r)` 日出（当日窗口累计消耗）、`Net(r)=In−Out`、`Population`、`Production.List<{Resource,Count,LevelSum,RateSum,WorkersSum}>`（产能建筑按资源聚合计数）、`GrainReserveDays=粮储备/日耗（口=人口×grainConsumptionPerPop）`、`Stock(r)` 国库存量、`StorageUsed/StorageCapacity/StorageOccupancy=Used/Capacity clamp01`
- 日入/日出口径＝日结路由当日窗口累计、每日重建清零、无持久态（D630 A+）

**② 触发判定规格（ResolveTriageResource，纯函数）**
- 严重度序（SO `shortageSeverityOrder` 出厂=粮→金→石→木，**铁跳过不参与触发**）
- 粮：`GrainReserveDays < grainReserveDaysFloor(=2)`
- 金/石/木：`Net(r)<0 且 Stock(r) < reserveTargetDaysOther(=3)×max(1,Out(r))`（单日判定）
- 全部不中 → 无触发（-1）

**③ 通道决策规格（DecideTriage，A→B→C 固定序、命中即止、单通道不叠加）**
- 通道A：`Count(Production[r])==0 且 FindTriageDef(r)!=null`（SO 反查表，Unity 出厂 Food→farm/Stone→quarry）→ 建对应产能
- 通道B：`Flow.In(r)/pop < channelBOutputPerPop(=1.0)` → NoOp（偏向派工活权重生效，⑤ 不建不空转）
- 通道C：`StorageOccupancy ≥ storageOccupancyThreshold(=0.9)` → 粮→Granary／非粮→Warehouse
- 全不命中 → NoOp
- 联动批B：`ShortageRate(r)=clamp01(max(0,−Net)/max(1,Out))`（活权重缺口率信号源）

**③ 验收面**：sim 侧实现等价纯函数后，同 seed 同输入逐元对拍（Unity 侧 R-C4 探针断言同输入同输出为锚）；判据参数走 SO 化对齐；15_账本 S-B1 登记+factor_registry 相关属训练仓侧落地。

## 七、附：HH.175 开工回执列报 1~8 的 D639 逐裁落实对照

列报1 单日判定→**已按**（Trigger 单日净流量为负）；列报2 粮沿 `grainReserveDaysFloor`→**已按**（FloorController 底线机制不动）；列报3 严重度序铁不参与→**已按**（L1296 continue）；列报4 通道A= `Production[r].Count==0`→**已按**；列报5 **改裁 `Flow.In(r)/pop`**→**已按**（L1275）；列报6 通道C 粮 Granary/非粮 Warehouse→**已按**；列报7 A→B→C 固定序→**已按**（switch 顺序）；列报8 def 反查入 SO→**已按**（`FindTriageDef`）。

## 八、撞号纪要

- D639 裁定批C 完成报告顺延 HH.177，但 **HH.177 已被美术端口径申报 09-10 先落盘占用**（D641 已裁决销案）；HH.178=训练轨 F26、HH.179=美术端均占用
- 执行端停手按 ⑦ 上报（未自改队列「完成报告=HH.177」口径），**用户拍板顺延取 HH.180**（先落盘者保留，后取者顺延）
- 本报告即 HH.180；账本/索引/队列三处陈旧口径随本串回写勘正

## 九、回写与 commit

- 回写：`_编号登记.md`（水位线 HH→HH.180＋HH.175 行状态→已交付待验收＋新增 HH.180 行）／`_交接索引.md`（HH.175 行完成报告指控 HH.176→HH.180＋新增 HH.180 行）／`_任务队列.md`（2_23 资源 P0 批C 行「完成报告=HH.177」→HH.180＋状态→🚧交付待验收）／`河谷防线_开发计划书.md` 工作日志插行（本行）
- commit 范围（只提本批）：KingdomBrain.cs / UtilityScorer.cs / KingdomDiagnosisConfig.cs / Smoke_2_23RP0.cs（+.meta）＋本报告＋四回写文件；禁 `git add -A`；**未 push**