# HH.53 种族人口属性实施批（HH.51 任务书三批）交付报告

> 类型：进度同步（含待决策）
> 状态：✅**14/14 ALL PASS（D485/D486 已实施落地，②b/②c/②d 全绿）——待策划端终验收（D487）+隔离代执** + sim-sync 义务清单（自卫交火层语义补行）
> 日期：2026-09-02 · 发起端：执行端 · 关联清单/文档：HH.51 任务书、2_20 §十一.5/§十二、0.6 §四十七/§四十九

## 一、做了什么（执行端填，带证据）

**编译基线：三批+冒烟全部完成后 0 error**（read_console 实盘，仅存量 1 条 node options 噪音与代码无关）。commit 随批策划端代执，本端未 commit。

### 批A · 个体 raceId 数据层 + 五轴快照（7 文件）

1. **新建 [KingdomRace.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/KingdomRace.cs)**：`RaceIds` 常量（Human=0/Elf=1/Dwarf=2/Orc=3，对齐 2_13 M10 选族索引）；`GetKingdomRace(kingdomId)` 国族解析唯一入口（现阶段恒 Human，Q10-M2 落字段后单点回填，防散落硬编码）；`ResolveGroupRace(memberIds, rng, out tie)` 成员组多数派解析（D471/D308/D306 共用，平票同 seed 确定随机+tie=true 告警）。
2. **[UnitController.cs](Valley Rampart/Assets/_Game/Systems/Unit/UnitController.cs)**：`raceId` 终身字段（默认 Human）+ `originPersonality` 五轴快照字段；`ApplyVagrantization(originKingdom, race, fiveAxisSnapshot)` 流民化打标 API（**2_19 供给侧挂账**：现库无流民化事件打标点，API 管道先行）；ResetForReuse 复位三字段；SaveState **v5→v6**（raceId+personalitySnapshot 入档）；LoadState v6 兼容，旧档<6 兜底 Human+`BumpDefaultedRaceSaveCount()`+日志。
3. **[UnitFactory.cs](Valley Rampart/Assets/_Game/Systems/Unit/UnitFactory.cs)**：`DefaultedRaceSaveCount` 属性 + `BumpDefaultedRaceSaveCount()`（CS0272 修复：set 越权改方法递增）。
4. **[SaveManager.cs](Valley Rampart/Assets/_Game/Systems/Save/SaveManager.cs)**：Load 时 `ResetDefaultedRaceSaveCount()`（对齐既有 ResetFilteredSaveUnitCount 位）。
5. **[PopulationSystem.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/PopulationSystem.cs)**：繁殖 Child 生成处 `childUc.raceId = KingdomRace.GetKingdomRace(0)`（子女=国族，D467）。
6. **[KingdomFoundry.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/KingdomFoundry.cs)** BlendPersonality：D475 五轴快照优先（`originPersonality != null` 直接用）→ 缺失回退 Registry 来源国 → 再缺失 0.5 中性（三层回退链）。
7. **[KingdomBrain.cs](Valley Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs)** **D476 对拍实锤小改**（解禁注增量）：走查发现原 Tick L76-77=Abstract 期整 tick 短路（互斥实现），与 D476「脑照跑+经济执行分叉」不符 → 重构为 mode 同步（原恒写 Fine 会覆写 Abstract 态）+ `mode == SimMode.Fine` 才 `ExecuteFocus`。

### 批B · 同族锁定执行面（5 文件）

8. **[Camp.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/Camp.cs)**：`wildAnnexDeclinedFlag`（异族营吞并拒绝去重）。
9. **[VagrantCampSystem.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/VagrantCampSystem.cs)**：RecruitVagrant 开头同族校验（**拒绝在粮检之前**，异族零资源损耗+日志，D469 玩家侧）；OnNewGameMapReady **D308 按族投放**（保底同点同族+余数按 baseline 切块成组，组内同族同锚点）；SpawnVagrantAt 签名改 `(worldPos, rng, race)` 写 raceId；SpawnVagrantNear 补员按营族（ResolveGroupRace）。
10. **[KingdomBrain.cs](Valley Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs)** L323：FindRecruitableVagrant 同族过滤（D469 AI⑥：`u.raceId != GetKingdomRace(kingdomId) continue`）。
11. **[CampUpgrader.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/CampUpgrader.cs)**：TryAnnex **D306 异族分支**（campRace≠国族 → 不解散不转化+flag 去重日志+return true；同族走原转化）。
12. **[KingdomFoundry.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/KingdomFoundry.cs)** L314-321：FoundFromCamp **D471 插旗定族**（多数派+tie 同 seed 随机+告警，日志「定族 raceId=X（D471 国族=营族）」；KingdomState 无 raceId 字段=Q10-M2 域，定族显式写入挂账单点回填）。

### 批C · 野性敌意行为（7 文件 + 2 资产）

13. **新建 [WildnessConfig.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/WildnessConfig.cs)** SO：`enabled=true` / `wildAggroRadiusCells=8`（**D477 格单位，禁微格域**，解禁注红线）/ `wildStrengthRatio=0.6`；`Load()`/`Cached`/`IsActive`（全局守卫单一入口）；`ResolveWorkerBaseline()` 查 PlayerCamp_Worker 职业资产。
14. 资产 `Assets/Resources/Config/WildnessConfig.asset`（guid 786bf2c4d1ae296458f54bbad2204a62，值 8/0.6/enabled:1 已实盘确认）。
15. **[KingdomFoundingConfig.cs](Valley Rampart/Assets/_Game/Data/Kingdoms/KingdomFoundingConfig.cs)** + 资产：`gatherSameRaceWeight=0.8`（SO 补丁注入，行 80 实盘确认）。
16. **[VagrantGatherSiteEvaluator.cs](Valley Rampart/Assets/_Game/Systems/AI/Stimulus/VagrantGatherSiteEvaluator.cs)**：ScoreSite/PickWeighted 加 sameRaceSites 参数+同族聚集分（权重 0 跳过）。
17. **[WanderStimulusProvider.cs](Valley Rampart/Assets/_Game/Systems/AI/Stimulus/WanderStimulusProvider.cs)**：`SelfRaceId` 属性注入（**不经 FactorContext/AI.Core 决策核**，防触发 sim-sync 扩字段义务）+同族结伙缓冲。
18. **[NPCBrain.cs](Valley Rampart/Assets/_Game/Systems/AI/NPCBrain.cs)**：Init 注入 SelfRaceId；UpdatePerception 末尾挂 `UpdateWildnessThreats`（无国流浪汉=Vagrant+!IsVagrantRecruited+非 Monster 阵营，扫 wildAggroRadiusCells×cellSize 内异族→ThreatStimulus(threatLevel:1 贴脸满强度衰减)，Monster 不进矩阵 D428）；`TryGetWildCombatOverride`（Worker 基线×ratio 覆盖 attack/range/cd/isRanged 四字段，**Max 下限兜底** attack≥1/range≥1/cd≥0.5）；`UseWildAttackRange` 随 useGridUnits 量纲；BuildBaseContext 射程三元；UpdateCombatRegistration 四 effective 值覆盖。
19. **[AIDebugSpawnController.cs](Valley Rampart/Assets/_Game/Systems/AI/AIDebugSpawnController.cs)** L660+：`SpawnVagrantWithRace`/`DebugSetRace`/`RaceName` 种族域调试钩子（任务书 §三.1「异族个体可用调试钩子构造」口径）。

### 验收 · 冒烟已实跑（2026-09-03，Play 上下文，2 轮有效结果）

20. **新建 [Valley2_20_Smoke_Race.cs](Valley Rampart/Assets/Editor/Smoke/Valley2_20_Smoke_Race.cs)**（菜单「Valley/验证/2_20_种族域Play冒烟」，Play 上下文）：探针①~⑤ 正负对照全编排——⑤静态（Resources.Load 路径/IsActive 开关负向/反射 TryGetWildCombatOverride 下限数值）→①行为（Elf 流民贴站桩 Worker 6s 掉血正/Human 流民血不变负）→②行为（Archer 贴 brain-off 流民 10s 血不变=压制负；brain-on 流民先动手→Archer 反击=正）→④同步（玩家侧拒绝 false+粮不变+日志捕获「招募拒绝：异族」/同族放行粮扣；AI⑥ FindRecruitableVagrant 反射——异族被滤/同族选中/全异族 null）→③同步（13 Elf+1 Human 营立国→日志「定族 raceId=1（D471」+成员 raceId 终身保持）。布局纪律：等轴 x 间距 14 格（17.9 世界单位>野性 8 格半径 10.24），y 向步长减半不展开组距；营地 (9,40) 独立+10 格清场保险。

**验收结果（3 轮有效实跑一致：第1轮暴露并修复 2 个冒烟容器 bug → 第2/3 轮稳定 12/13 PASS，第3 轮=正式终验）：**

| 探针 | 结果 | 证据 |
|------|------|------|
| ⑤a 路径探针 | OK | Resources.Load 命中 WildnessConfig.asset |
| ⑤b IsActive 默认开 | OK | Cached 填充后判定（修冒烟容器 1：IsActive 依赖 _cached，⑤a 直读不填 → 改 Cached 先填充） |
| ⑤c 开关关→零野性 | OK | enabled=false → 无野性 |
| ⑤d 下限兜底 | OK | attack=1/range=1.0/cd=0.5（Worker 基线 0→Max 下限） |
| ①正 异族→Worker_A | **OK** | 100→76 / 100→65 掉血；wildDiag：Elf 开火 →#34(Worker) 伤害=1 |
| ①负 同族→Worker_B | OK | 100→100 血不变 |
| ②a 压制 Archer 不打 E4 | **OK** | E4 100→100（修冒烟容器 2：②b 清场漏排 e4 误杀 → 排除列表加 e4） |
| ②b 反击→E2 | **FAIL（第二拦截点，D485 ① 已生效）** | 诊断②铁证：agg=#45(E2 溯源成功 ✅) threatN=1(威胁列表有 E2 ✅) focus=Position(Caution 态 HoldPosition 驻留胜出焦点 ❌) → 路径1 要求 ThreatStimulus 焦点不满足 → 射程内持续攻击的异族野人不还手 |
| ②b2 负 同族野人伤国民→不还手 | **OK** | ②b2 新负探针（D485）：ApplyDamage 强制同族野人袭击 → Archer 不还手（hVagrant 100→100） |
| ④a 玩家侧异族拒绝 | OK | rej=True 粮 150→150 日志「招募拒绝：异族」在 |
| ④b 玩家侧同族放行 | OK | 粮 150→149 |
| ④c AI⑥同族过滤 | OK | 选中 #43 race=0 |
| ④d AI⑥全异族→null | OK | 负加固（DebugSetRace 改标后）null |
| ③ D471 插旗定族 | OK | 立国 4→5；定族日志 raceId=1；成员 raceId 保持 Elf |

**13/14 PASS**（②b2 为 D485 新增负探针），唯一 FAIL=②b 的第二拦截点（见 §三.4 更新）——D485 ① 受击溯源已实施生效，但 Caution 态 HoldPosition 焦点（3.0.1 既有谱系）让射程内持续攻击的异族野人不被还手，涉 AI.Core 决策核焦点竞争。

### 验收 · 关键链路核实（冒烟前置走查，实盘）

- 野性攻击全链**无阵营过滤**：UpdateWildnessThreats 注入 ThreatStimulus → NPCBrain 路径1 焦点在射程 → `DamageSystem.RegisterAttack`（无过滤）→ `ExecuteAttack` → `ApplyDamage` 单体（无过滤，[DamageSystem.cs](Valley Rampart/Assets/_Game/Systems/Combat/DamageSystem.cs) L280-298；同阵营过滤仅存在于溅射 ApplyImpact L327）→ PlayerCamp 内互攻链路通。
- 压制语义结构保证：国民 `_nearbyEnemies` 来自感知系统=阵营敌对驱动，raceId 不进 Faction（D428）→ 国民感知不产异族敌意；本批野性扫描仅挂无国者。

## 二、现状与阻塞

- 三批代码/资产完成+编译 0 error；**行为级冒烟已实跑：13/14 PASS**，唯一 FAIL=②b 第二拦截点（见 §三.4——D485 ① 受击溯源已生效，剩余拦截点在 Caution 态 HoldPosition 焦点竞争，涉决策核）。
- 冒烟容器磨合记录：① 首跑暴露 GetAllUnits 遍历中杀单位→`Collection was modified` 协程崩（改 `.ToList()` 快照）② 等待世界逻辑三版（2 帧过早→InitializeNewGame 异步未就绪 Spawn 全 null；等 Playing 判定错→直接 GameScene Play 无配置不进 Playing；终版=用户 MainMenu 进局复用世界+幂等跳过，跑通）。
- 本端未 commit（任务书：commit 随批策划端代执）。

## 三、待决策事项（每项：选项 + 推荐 + 影响）

1. **Worker 基线 attack=0 → 野性战力公式退化**——实盘：`Human_Player_Worker.asset` attack/attackRange/attackCD 全 0（和平职业正常值），「野人战力=同职工人 60%」=0×0.6=0，若照公式野性攻击永不发生，D468「无条件攻击」行为硬规则不落地。执行端已裁量：守卫只挡查表失败，数值走 Max 下限兜底（attack=1/range=1 格/cd=0.5s），SO 注释+代码注释均已标注缺口。这决定探针①通过判据与野人战力数值，需策划回调。
   - A（推荐）：**保留下限兜底待 Play 实测回调**——与条目15「占位待 Play 回调」语义吻合，零回归风险（不改工人职业本体），下会话跑冒烟拿手感数据后再定数值。
   - B：改 Human_Player_Worker 资产 attack>0——工人职业获得攻击力，`effectiveAttack = _profession.attack` 使工人本体变战斗单位（会主动攻击/参与战斗），回归风险大，不推荐。
   - C：WildnessConfig SO 增绝对基线字段（如 wildBaseAttack）——数据驱动最干净，但多一参数，等 Play 回调时一并定（可与 A 合流）。
2. **sim-sync 义务清单（如实列报，不代做）**——本批 AI 侧改动全部在壳层（NPCBrain/ Stimulus/WanderProvider 属性注入），**AI.Core 决策核零扩字段、FactorContext 零新增，无 sim 直改义务产生**；但 D468 野性敌意行为语义（无国×异族无条件攻击/同族结伙/有国压制）属 AI 行为层新增，进 sim 训练仓时需：
   - sim 侧对等实现野性敌意行为 + L1 AI.Core/sim 双份登记（15_账本）
   - factor_registry 草案 4 参数：wildAggroRadius（8 格）/ wildStrengthRatio（0.6）/ D470 跨族敌意减成 / 宣战门槛系数
   - WildnessConfig/ gatherSameRaceWeight SO 参数入 sim 三源同步（champion/factor_registry/Unity SO）
   - **D485 ① 国民自卫溯源语义（壳层已落地）**：NPCBrain.OnDamaged 对「无国野人异族（raceId≠自身）」放行受击溯源——Unity 壳层已实施并验证（诊断 agg=#45 溯源成功），sim 侧需对等登记「国民被异族野人攻击可自卫还手」语义
   - **②b 第二拦截点（若选 §三.4-B）**：Caution 态 Trace 威胁优先级调整涉 AI.Core（AttentionSystem 排序）→ sim 双份登记追加
   - 排期归策划端（属 2_18 外交批/sim 批内容，本批未做）。
3. **Q10-M2 挂账确认**：GetKingdomRace 恒 Human、KingdomState.raceId 字段未落（D471 定族显式写入挂账 KingdomFoundry L316-317 单点回填注）——待 Q10-M2 批接真模板映射后回填，本批口径已全走统一 helper 防散落。
4. **②b 第二拦截点：国民受击溯源已通（D485 ① 生效），但 Caution 态 HoldPosition 焦点挡还手**——D485 ① 已按裁决落地 [NPCBrain.cs](Valley Rampart/Assets/_Game/Systems/AI/NPCBrain.cs) OnDamaged：同阵营但「无国野人异族（raceId≠自身）」放行溯源。**重跑诊断②铁证**：`agg=#45(E2)` 溯源成功 ✅ + `threatN=1`（Trace 威胁已入列表 ✅）+ `focus=Position`（Caution 态 HoldPosition 驻留胜出焦点 ❌）→ NPCBrain.UpdateCombatRegistration 路径1 要求 `focus.Focus is ThreatStimulus` 不满足 → 射程内持续攻击的异族野人不还手。这是 **3.0.1 既有「受击→Caution→HoldPosition 驻留防追击」谱系**，非 D485 ① 可覆盖，**涉 AI.Core 决策核焦点/威胁优先级 → 触碰 sim-sync 义务**。执行端未擅改（按纪律上报）：
   - A：**Caution 驻留但射程内高威胁可还手**——UpdateCombatRegistration 路径1 放宽：威胁列表（threatN>0）中最近威胁在射程内时，即使焦点=Position 也攻击。壳层改动（不触 AI.Core）；但改变「驻留 vs 还手」行为语义，需策划确认。
   - B：**提高 Trace 威胁优先级**——Caution 态下让受击溯源 ThreatStimulus 优先于 HoldPosition。涉 AI.Core（AttentionSystem 排序）→ sim-sync 义务。
   - C：**维持现状（驻留不还手）**——②b 判 FAIL 验收，异族野人可持续白嫖国民，对等性缺失，不推荐。
   - 影响：A 壳层最小、不触 AI.Core，但属行为语义变更；B 涉决策核需 sim 双份；C 验收留 FAIL。

### D485 ③ 守卫走查结论（登记，不扩大）

- 走查对象：GuardDeploymentSystem（守卫部署，D116=玩家选中士兵右键高价值点派兵守卫）。**结论：同因**——守卫/国民单位受击溯源已随 D485 ① 修复（OnDamaged 种族维度放行，诊断 agg 生效）；守卫驻守/还手是否响应野人袭击，同受 ②b 第二拦截点影响（Caution 态 HoldPosition 焦点竞争），**随 §三.4 裁决一并处理**。
- 守卫部署是玩家主动指令链路（非自动告警链），野人袭击不被守卫「主动拦截」属同阵营不视为敌人的既有语义；若策划要求「守卫点自动拦截野人」，属新行为需求，另立决策，本批不扩大。

## 四、下一步建议（恢复执行入口）

1. 策划端裁决 §三.4（②b 第二拦截点 A/B/C）——D485 ① 已落地生效（agg 溯源成功），剩余拦截点在 Caution 态 HoldPosition 焦点；选 A 本端落 UpdateCombatRegistration 壳层放宽+重跑②b；选 B 涉 AI.Core（sim-sync）；选 C 按裁决验收口径。
2. 裁决 §三.1（Worker 基线缺口）→ D484=A 已记录，Play 回调批合流 C（wildBaseAttack）。
3. 冒烟 13/14 PASS 基线 + ②b 裁决后 → 策划端验收+随批 commit（三批+SO 资产+冒烟，共 12 文件+2 资产）。
4. sim-sync 义务（§三.2）排期进 sim 批/2_18 批。

---

## 附录 · 批A 步骤0 只读走查产物（任务书 §一.批A 要求落档）

### 七落点盘点结论（实盘 grep，2026-09-02）

| 落点 | 现状结论 |
|------|---------|
| VagrantCampSystem | SpawnVagrantAt/Near（营地投放）→ ForceCampScan 结营 → RecruitVagrant（玩家招募）；ISaveable 存档齐（D301/D313/D387） |
| PopulationSystem | 繁殖 Child 生成处=唯一「新生人口」点 → 批A 落子女=国族 |
| UnitFactory | SpawnUnit(Faction, Occupation, pos, kingdomId=0)；**kingdomId=0=玩家侧语义**；NPCBrain 挂 prefab，data 为 NpcProfessionDef 时 brain.Init |
| 玩家招募 RecruitVagrant | 原无族校验 → 批B 补 D469 |
| AI⑥ 强制招工 | FindRecruitableVagrant 原无族校验；池=**kingdomId<0**（流失人口 MigrateToNearestCamp 入营者），野外流民 kid=0 不进 AI⑥ 池 → 批B 补同族过滤 |
| CampUpgrader 插旗与吞并 | TryAnnex 原无异族分支 → 批B 补 D306；FoundFromCamp 原无定族 → 批B 补 D471 |
| Child 繁殖 | 同 PopulationSystem 行 |

### 关键对拍实锤

1. **D476 失配实锤**：KingdomBrain.Tick 原 L76-77=Abstract 期整 tick 短路（互斥实现）≠ D476「脑照跑+经济执行分叉」→ 批A 小改重构（见 §一.7）。
2. **originKingdomId 无打标点**：全库 grep 仅 UnitController 定义+KingdomFoundry BlendPersonality 消费 6 处，**无写入点**——流民化事件打标属 2_19 待实施（D389 印记管线）；批A 落 ApplyVagrantization API 管道先行，供给侧挂账。
3. **D400 流失落点**：AbstractEconomySettlement.MigrateToNearestCamp（L112）=流民化实点（AI 国流失→最近营），2_19 接打标时此为供给侧首站。
4. **流浪汉 kingdomId 约定**：SpawnVagrantAt/Near 走 SpawnUnit kingdomId 默认 0（=玩家侧未入籍，非 AI 国籍）；AI⑥ 用 `kingdomId >= 0 → continue` 排除已入籍+野外流民——野性矩阵判「无国者」用 occupation+IsVagrantRecruited 双条件（不用 kingdomId），两套口径已对齐走查。
5. **Human_Player_Vagrant 资产**：attack=0/walkSpeed=3/perceptionRadius=4——野性战力必须靠 Worker 基线覆盖（TryGetWildCombatOverride 方案由来）。

---

## 策划裁决（2026-09-03 策划端，终验收成立 · D484~D487）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| §三.1 Worker 基线缺口 | **D484=A 落地确认** | Max 下限兜底（attack≥1/range≥1/cd≥0.5）保留、不改工人资产；Play 回调批合流 C（WildnessConfig 增 wildBaseAttack 绝对基线字段，下限值转正初始参数） |
| §三.2 sim-sync 排期 | **D486 附带确认** | 义务清单（野性敌意+自卫交火层语义+wildAggroRadius/wildStrengthRatio/D470 减成/宣战系数 4 参数）归 sim 批/2_18 P2；本批零 AI.Core 触碰、零 FactorContext 扩字段，执行端只列清单合规 |
| §三.4 ②b 第二拦截点 | **D486=A+ 常设自卫交火层，落地实锤** | 焦点层与被动自卫解耦=根修（驻留防追击与还手不冲突——HoldPosition 本意是不追出去，不是站着挨打）；受击不追抑制（驻守/移动受击追出 11.34 格→0.0）=真实功能缺口修复嘉奖；守卫同因自动受益 |
| 三批验收 + commit | **D487 终验收成立** | 静态 8/8 关键声明实锤+raceId 写入点全线合法（D467 红线安全：声明默认/池复位/ApplyVagrantization/出生投放/存档读取/调试钩，无改写存活个体路径）+14/14 ALL PASS 三轮一致+编译 0 error；commit 隔离代执（排除 D483 美术更名批 8 文档+图片资源，另挂账 0.6 补登） |

容器附带登记三项确认（探针基建怪癖，不影响验收）：②c 受击单位行为不稳定=换 Worker+钳制；④a e3 raceId 改写=对象池复用怪癖（写入点核查全线合法，低危挂账确认机制）；②a 偶发开火=射程外未打属实。

### 衍生产物
- 新建冒烟：Valley Rampart/Assets/Editor/Smoke/Valley2_20_Smoke_Race.cs（探针①~⑤）
- 新建 SO：WildnessConfig.cs + Resources/Config/WildnessConfig.asset
- 待办挂账：Q10-M2（KingdomDef.raceId 回填）、2_19（流民化打标接 ApplyVagrantization）、sim 批（§三.2 义务清单）
