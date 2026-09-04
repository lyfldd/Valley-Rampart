# HH.58 — Q10 批2（M3+M5）开工回执「范围+锚点」

> 执行端 → 策划端。批1 已终验收（34c22e1），批2 解锁。本回执=实施前锚点申报，含 **1 处范围差异**+**1 处语义缺口**+**1 组映射表**请过目后动工。

---

## 一、范围（含任务书↔清单差异上报）

### M3（KingdomFoundry 种族分配）
- **动态立国部分批1 已清偿**：D471 同族营→同族建国+GetKingdomRace 真字段回填已实施（34c22e1）；D433 混合随机池已退役（总纲 §十二.5）。
- **本批剩余=开局分配（D430）**：玩家种族占保底席+AI 保底其余三族各一+余者随机；不改 FoundingConfig 数量配置。
- 验收探针：Small 3 AI=三族各一（不含玩家族）；动态立国种族=来源国（批1 已有行为级证据，随批2 冒烟复证）。

### M5（玩家出生选族数据侧 D431+RaceDef 真值挂载）
- 玩家 Kingdom/raceId 绑定（NewGameConfig.raceId 消费）+保底联动（玩家占席→M3 分配读它）。
- **RaceDef 真值挂载**：D503 表（12 项×4 族，批注通过占位生效）批量写入四资产，P0 端到端调优后回调。
- **消费点接线 8 处**（M5 验收"玩家单位/建筑吃 RaceDef 修正"的落地前提）——见 §三。

### ⚠ 范围差异上报（不擅裁）
任务书写"M5（**专属建筑×4**+玩家选族绑定+RaceDef 真值挂载）"——但清单 M5=**玩家出生选族数据侧**，专属建筑×4=**M6**（批3 M6+M7，依赖 M1+P3；D419 唯一入口+训练归属在 M6 验收标准内）。
**执行端推荐 A：按清单口径执行**（本批=选族绑定+真值挂载+消费点接线；专属建筑×4 留批3 M6），请确认或改判提前并入。

## 二、语义缺口上报（M3 唯一口径问题）

**AI 数<3 时保底如何降级**：D430 保底=其余三族各一，但 D288 档位 Small=2~3 AI——2 AI 时"三族各一"放不下。
**执行端推荐 A：保底降级为 min(AI数,3) 族各一**（AI 数≥3 强制三族覆盖；<3 时按 rng 确定哪几族，保持随机公平）——B 或其他口径请裁。

## 三、消费点接线清单（D420 映射权威已逐点实读）

读取辅助统一走 `KingdomRace.GetKingdomRace(kingdomId)`（批1 真字段），**每 mul 消费点唯一**（D436 禁散落）：

| # | mul | 挂载点（实读行号） | 方式 |
|---|-----|-------------------|------|
| 1 | moveSpeedMul | [UnitController.EffectiveSpeed](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/UnitController.cs#L178-L181) | 单点乘（普通/追击/override 全过此口） |
| 2 | meleeAtkMul/rangedAtkMul | [DamageSystem.ExecuteAttack](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Combat/DamageSystem.cs#L252-L263) isRanged 分流处 | 攻方 kingdomId→mul 乘 profile.attack（AttackProfile=struct 副本安全，注册表不污染） |
| 3 | mineMul/lumberMul/farmMul（Production 侧） | [ProducerComponent.Tick](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ProducerComponent.cs#L146) 主产累加 | ×资源映射 mul（副产 Crystal/FireOil 不乘） |
| 4 | mineMul/lumberMul/farmMul（Gather 侧） | [TaskScheduler.ExecuteCompletion](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L582-L604) 采集完成入库 | 两处同乘防漂移（D420 铁律）；工人 kingdomId 取 UnitController（L648 同款） |
| 5 | carryCapMul | [WorkerInventory.GetCarryCapacity](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/WorkerInventory.cs#L24-L28) | 容量×mul（组件内查 UnitController.kingdomId） |
| 6 | buildingHpMul | [Building.ApplyDef](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L327-L341) + [BuildingFactory 内联](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L206-L220) | **HP 双路同乘**（Factory 内联路径不经 ApplyDef；两处 kingdomId 均先于 HP 计算赋值已核实）；已建成不追溯自然满足 |
| 7 | buildSpeedMul | [Building.EffectiveDuration](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L135-L156) | 时长÷mul（2_12 建造链唯一时长出口） |
| 8 | trainCostMul/trainSpeedMul | [TrainingSystem.TryTrain](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/TrainingSystem.cs#L165-L215)（CanPay/Pay L218~248+entry costDays L56） | 扣费=effective 成本（ceil 取整防零成本白嫖）；时长=entry 存 effDays 副本不改 SO；**战争学院-25% 随 M6 在同点叠乘**（现无实现，挂账） |

### 资源类型→mul 映射表（请策划过目，实施即按此）

| mul | 适用 ResourceType | 说明 |
|-----|-------------------|------|
| mineMul | Stone、Ore | 自然采集+矿洞主产 |
| lumberMul | Wood | 伐木链 |
| farmMul | Food、Meat | 农田/牧场（farm=食物原料语义） |
| **不乘** | Metal、SpecialFood、Crystal、FireOil、Gold、三弹药 | 加工品/副产/货币不乘（保守口径，防中间加工重复加成） |

## 四、随批并入四项（已裁事项）

1. **D503 真值挂载**：表值已从 HH.55 §三提取（12 项×4 族，两锚在列）——M5b 批量写入四资产。
2. **④c 探针修正**：[Valley2_20_Smoke_Race.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_20_Smoke_Race.cs) 材料补注入后 5 帧 yield+断言放宽「任一未招募同族流民」——随批2 冒烟重放。
3. **D473 P3 小修**（第二次催办）：[Valley2_13_Smoke_AB.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_13_Smoke_AB.cs) P3 断言改「UnitCommandEvent **OR** PrioritizeHarvestCommand 二选一」——本批顺手落地。
4. **Smoke_12 补跑**（D508）：Valley2_17_Smoke_12 随批2 冒烟周期执行留档。

## 五、红线与 sim 义务

- 红线确认：设计文档正文只读（实施清单状态行除外）/sim 不代做/D470 不在本批/projDiag 不复活（14b95fc 基线）。
- **sim 义务列报**：本批消费点全在 Unity 侧壳层（TaskScheduler/Building/Unit/Training/Damage），不触 AI.Core 决策核；sim 侧 [AbstractEconomySettler](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/AbstractEconomySettler.cs#L20-L35) 已有 mineMul/lumberMul/farmMul/buildSpeedMul 字段占位（步骤14），**真值回灌+SimMode.Abstract 挂载归 sim 批**，如实列报不代做。
- 存档影响：零新增字段（raceId 批1 已入档；真值挂载=资产改值非字段变更）→ **预期零 bump**，随完成报告申报。

## 六、实施顺序与冒烟计划

1. M5b 资产真值批量写入（D503 表）→ M3 分配层（TemplateLibrary.DrawWithRaceQuota 或调用处过滤层，rng 同链确定性）→ M5a 玩家绑定 → M5c 消费点 8 处 → ④c/D473 探针修正
2. 冒烟（用户 MainMenu 正常进局，遵守既有纪律）：M3 开局分布探针（Small 3 AI=三族各一不含玩家族）+M5 选族探针（矮人采矿加成正/未选族负）+④c 重放+P3 二选一验证+Smoke_12 补跑+动态立国复证
3. 完成报告（git status 全量对照）→ 策划验收代执

**请裁决三项后动工**：①范围差异（A 按清单/专属建筑并入）②AI<3 保底降级口径 ③资源→mul 映射表确认。

— 执行端 · Q10 批2 · 2026-09-04
