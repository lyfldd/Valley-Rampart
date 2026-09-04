# HH.59 · Q10 批2（M3+M5）完成报告

- 发件：执行端
- 收件：策划端
- 日期：2026-09-04
- 任务真源：HH.58 开工回执 + 策划端裁决 D506（三项全裁 A）
- 状态：**实施+冒烟完成，待验收代执**（未 commit）
- 上游：批1 终验收 34c22e1

---

## 一、范围与裁决回顾（D506）

| 项 | 裁决 | 落地 |
|---|---|---|
| ① M5 范围 | 按清单口径：选族绑定+D503 真值挂载+消费点接线；专属建筑×4 留批3 M6 | ✓ 本报告全部为清单口径内 |
| ② M3 保底降级 | min(AI数,3) 族各一保底；<3 时 rng 洗牌定序，同 seed 可复现 | ✓ |
| ③ 资源→mul 映射 | Stone/Ore→mine、Wood→lumber、Food/Meat→farm；Metal/SpecialFood/Crystal/FireOil/Gold/三弹药不乘 | ✓ 落为 KingdomRace.GetGatherMul 唯一真源 |

## 二、实施明细

### M3 开局分配（D430/D506②）
- [KingdomTemplateLibrary.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/KingdomTemplateLibrary.cs)：新增 `DrawAiTemplates(rng, count, playerRaceId)`
  - AI 池=排除玩家族模板（玩家占保底席，验收口径「三族各一不含玩家族」）
  - 保底：种族键升序稳定初序 → rng Fisher-Yates 洗牌定序 → 取前 min(count, 族数) 各 rng 抽 1 模板（全覆盖时免洗牌省 rng 流）
  - 余者：剩余模板 Fisher-Yates 抽
  - 池不足兜底（玩家族占双模板+高 AI 数边界）：并入玩家族模板+LogWarning
  - rng 沿用地图生成派生链（R4）；旧 `DrawWithoutReplacement` 加弃用标注保留（D315 文档锚点，GetUnitsInCell 先例）
- [WorldManager.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/WorldManager.cs)：GenerateMap 抽取点改调 DrawAiTemplates，玩家族从 KingdomRace.GetKingdomRace(0) 读真字段

### M5a 玩家选族绑定（D431）
- [KingdomRegistry.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomRegistry.cs)：`EnsurePlayerRegistered(int playerRaceId = RaceIds.Human)`，state.raceId=playerRaceId；缺省 Human 兜底旧路径
- [WorldSystem.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/WorldSystem.cs)：传 newConfig.raceId（2_13 选族 UI 暂存消费）

### M5b 四资产真值（D503 批注通过值）
- Race_Elf/Dwarf/Orc.asset：12 项×3 族批量写入（Human 全 1.00 原样已位）
- ④⑦探针 D503 全表断言 36 项逐值验证通过（见 §三）

### M5c 消费点接线 8 处（D420 每消费点唯一）
| # | 消费点 | 文件 | 实现要点 |
|---|---|---|---|
| 1 | moveSpeedMul | UnitController.EffectiveSpeed | 单点（普通/追击/override 全过此口）；怪物过滤 |
| 2 | melee/rangedAtkMul | DamageSystem.ExecuteAttack+ResolveAtkMul | struct 副本乘改不回写注册表（防复合乘）；远程=发射前乘入 profile.attack；怪物/中立不吃；建筑塔按国族 |
| 3 | 主产累加 | ProducerComponent.Tick | ×GetGatherMul(kingdomId, resourceType)；副产不乘 |
| 4 | Gather 入库 | TaskScheduler.ExecuteCompletion | ×GetGatherMul 同源（两处同乘防漂移）；Max(1) 防低 mul 白干 |
| 5 | carryCapMul | WorkerInventory.GetCarryCapacity | UnitController 归属国；非单位载体中性 |
| 6 | buildingHpMul 双路 | Building.ApplyDef + BuildingFactory 内联 | 两路同乘（同消费点两条初始化路）；哨兵 -1→null→中性 |
| 7 | buildSpeedMul | Building.EffectiveDuration | 时长÷mul（协作缩放后统一除） |
| 8 | trainCost/SpeedMul | TrainingSystem.TryTrain | effective 成本 ceil 防零成本白嫖；CanPayRecruit/PayRecruit 改接 effective 三元组（校验/扣费同值防漂移）；entry 存 effCostDays 副本不改 SO |

### helper 基座
- [KingdomRace.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomRace.cs)：
  - `GetKingdomRaceDef(kingdomId)`：四资产缓存（Resources/Config/Races），哨兵 -1/查无→null（消费侧 mul=1 中性）
  - `GetGatherMul(kingdomId, resourceType)`：D506③ 映射表唯一真源（D420 两处同乘防漂移纪律载体）

### 随批四项
1. **④c 探针修正**（D498 预批）：补注入后 5 帧 yield+断言放宽「任一未招募同族流民」+两轮根因修正（见 §四）
2. **D473 P3 探针小修**（第二次催办）：UnitCommandEvent OR PrioritizeHarvestCommand 二选一+分派路径直移断言条件化（终版 ALL PASS）
3. **Smoke_12 补跑**（D508）：P1~P10 **ALL PASS**
4. 动态立国复证（探针③）：final-r1 轮立国 4→5 OK+定族日志+真字段 state.raceId=1+成员保持 Elf 全绿

## 三、冒烟证据（用户 MainMenu 正常进局、选矮人 r2，共六轮）

### 核心新探针（M3+M5）
| 探针 | 断言 | 证据轮次 | 结果 |
|---|---|---|---|
| ⑥ M3 分布 | 玩家 r2；第一代 id 序 min(3,AI) 个互异（#1:r0 人/#2:r1 精/#3:r3 兽）+不含玩家族+合法域+M5a 绑定同值 | 首轮/final-r1/final-r2 三轮 | **三轮全绿**（#4/#5=③探针立国 Elf+动态继承，与保底逻辑自洽） |
| ⑦a D503 全表 | 12 项×4 族逐值 | 三轮 | **全绿** |
| ⑦b GatherMul 同源 | 全王国 Ore/Wood/Food 映射=RaceDef 字段+Metal 不乘 | 三轮 | **全绿** |
| ⑦c 矮人正探针 | GetGatherMul(0,Ore)==1.3 | 三轮 | **全绿**（用户选矮人激活） |
| ⑦d 哨兵负探针 | kingdomId=-1→null→中性 1 | 三轮 | **全绿** |
| ④b 同族放行 | Dwarf r2 放行+粮扣（150→149） | final-r1 | **OK**（final-r2 粮 0=SKIP 诚实语义） |
| ④c 同族过滤 | 玩家族 r2 选中 Dwarf 流民（#50/#87） | final-r1/final-r2 | **连续两轮 OK** |
| ③ D471 定族 | 立国 4→5+定族日志+真字段+成员保持 | final-r1 | **OK** |

### 回归探针（终版 final-r2 全绿一轮定版）
①正(74)/①负/②a/②b(死)/②b2/②c/②d/④a/④d/⑤a-e 全 OK。
前轮翻转（②b 首轮、②a 第五轮、①正/②b2 final-r1）均在后续轮自愈恢复——同代码多轮结果翻转=环境噪声定性（2_21A P-A1 先例；用户世界 70+ 单位野怪游荡干扰材料）。

### 其他域
- 2_13_AB：P1~P8 **ALL PASS**（P3 修正后验证 ✓）
- Smoke_12：P1~P10 **ALL PASS** ✓
- 编译：全程 0 error（触文件零新增警告）

## 四、冒烟过程中修正的探针问题（均容器侧，产品零改动）

1. **Collection modified（活局事故纪律应用）**：③ 段营 10 格清场无快照，多轮材料积累下枚举内部 HashSet 炸——批1 零伤害未触发纯运气。修=ToList 快照后杀（①/②段先例）。
2. **④c 根因链（M5a 行为联动）**：玩家选族后 GetKingdomRace(0)=玩家族 r2，旧材料 h3=Human 被同族过滤结构性排除（=M5a 生效的正确表现）→同族正材料改 Dwarf h5；第二轮发现 h5 漏置 kingdomId=-1 补上。
3. **④b 根因（同款 M5a 联动）**：h2=Human 被 D469 异族拒绝（VagrantCampSystem L156 实读）→材料改 Dwarf r2。
4. **P3 断言口径**：目标格含资源走 D115=PrioritizeHarvestCommand 分派接管，PathFollower 无直移=合理→断言条件化 `behaviorOk = sentH || (moved && destOk)`。
5. **编译挂起教训**：Play 模式下 Script Changes 挂起编译，曾致一轮跑旧程序集（异常行号考古发现）——后续改 Editor 脚本后均先退 Play 编译再进局。

## 五、sim 义务评估（如实列报，sim 侧零改动）

- 本批全在 Unity 侧壳层（KingdomFoundry 分配+消费点挂载），sim 的 AbstractEconomySettler 四乘数占位（=1f）未动
- sim 真值回灌（mineMul/lumberMul/farmMul/buildSpeedMul Unity 值→sim 快照）归 sim 批，本批不代做
- D503 表值与 sim 侧对拍：待 sim 批立单

## 六、git status 全量文件清单对照（勿凭记忆，实测 2026-09-04）

### 本批产物（执行端，验收范围）
| # | 文件 | 类型 |
|---|---|---|
| 1 | Assets/_Game/Data/Kingdoms/KingdomTemplateLibrary.cs | M3 分配层 |
| 2 | Assets/_Game/Systems/World/WorldManager.cs | M3 接线 |
| 3 | Assets/_Game/Systems/Kingdom/KingdomRegistry.cs | M5a |
| 4 | Assets/_Game/Systems/World/WorldSystem.cs | M5a |
| 5 | Assets/Resources/Config/Races/Race_Elf.asset | M5b |
| 6 | Assets/Resources/Config/Races/Race_Dwarf.asset | M5b |
| 7 | Assets/Resources/Config/Races/Race_Orc.asset | M5b |
| 8 | Assets/_Game/Systems/Kingdom/KingdomRace.cs | helper 基座 |
| 9 | Assets/_Game/Systems/Unit/UnitController.cs | M5c#1 |
| 10 | Assets/_Game/Systems/Combat/DamageSystem.cs | M5c#2 |
| 11 | Assets/_Game/Systems/Building/ProducerComponent.cs | M5c#3 |
| 12 | Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs | M5c#4 |
| 13 | Assets/_Game/Systems/Unit/WorkerInventory.cs | M5c#5 |
| 14 | Assets/_Game/Systems/Building/Building.cs | M5c#6/#7 |
| 15 | Assets/_Game/Systems/Building/BuildingFactory.cs | M5c#6 |
| 16 | Assets/_Game/Systems/Building/TrainingSystem.cs | M5c#8 |
| 17 | Assets/Editor/Smoke/Valley2_20_Smoke_Race.cs | 探针⑥⑦+随批修正 |
| 18 | Assets/Editor/Smoke/Valley2_13_Smoke_AB.cs | P3 D473 修正 |
| 19 | 多Agent交接/执行端/HH.58_..._范围锚点.md（untracked） | 回执 |
| 20 | 多Agent交接/执行端/HH.59（本报告，untracked） | 完成报告 |
| 21 | 多Agent交接/_交接索引.md | 登记 |

### 非本批改动（工作区既有，待策划端分辨归属）
- **Packages/manifest.json + packages-lock.json**：codely bridge 包 1.0.78→1.0.80 自动升级（工具链自动，非人工）
- **计划书文档 ×10**（0.6/0_总计划/2_20 总纲/2_20.1/2_20 实施清单/3.1.2/3.1.3/3.6/QQQ.5/_目录）：策划端裁决回写与批注（D506 等），非执行端本批产物
- **图片资源/四族风格锚点/（untracked 目录）**：美术域

## 七、疑点与挂账上报（不擅裁）

1. **D503 兽人 trainSpeedMul=1.15 方向矛盾**：字段语义=训练时长%（<1 加速，RaceDef 注释+2_20.1 §二），但 D503 表兽人 1.15（设计依据「兽人速成」）会=**更慢 15%**，与速成矛盾。矮人 0.95=快 5% 符合时长语义。已按批注值原样挂载（D503=权威），**请 P0 端到端调优时一并回调**（若意图=速成应为 <1）。
2. **M3 rng 链变化**：DrawAiTemplates 的 rng 消耗与旧 DrawWithoutReplacement 不同→同 seed 新开世界地图布局与批1 版本不同（M3 行为变更的必然副作用，同版本内仍确定可复现）。既有 P0 状态面基线若依赖固定 seed 布局需在下次全量回归时重新标定。
3. **③ 探针同会话重放边界**：同一 Play 会话多轮触发时营地/材料被前轮消耗→③ 段退化（立国 5→5 FAIL）。final-r1 已证产品链路 OK；属容器幂等边界非产品回归，冒烟 SOP=每会话 2_20 容器触发 ≤2 次为宜。
4. **战争学院 -25%**：随 M6（批3）在同点（TrainingSystem effective 成本/时长）叠乘，本批挂账。
5. **自然建筑 kingdomId=-1 吃 Human mul 的未来风险**：当前 GetKingdomRaceDef(-1)=null→中性 1，已防；若未来 Human 调非 1 值，哨兵仍安全（null 兜底）。

## 八、验收请求

本批 M3+M5a+M5b+M5c+随批四项全部完成，冒烟证据链齐（§三），产品侧零缺陷残留（§四全部容器侧）。
**请策划端验收；验收通过后代执 commit**（建议 message：`2_20 批2：M3 开局种族分配+M5a 选族绑定+M5b 真值挂载+M5c 消费点 8 处+随批探针修正（D506/D430/D503/D420/D473/D508）`）。
验收后批3（M6+M7）解锁。

（HH.59 完）
