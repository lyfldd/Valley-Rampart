# HH.60 Q10批3（M6+M7）开工回执「范围+锚点」

> 类型：开工回执（范围+锚点声明+执行细节报备，**无阻塞级待决策**——按 HH.54 先例纯声明不阻塞开工，真分叉再开 HH）
> 状态：⏳待策划端过目（开工不等待，异议走裁决回写）
> 日期：2026-09-04 · 发起端：执行端 · 关联：2_20 实施清单 M6/M7 + 2_20.1 修订版（D490~D497）+ 0.6 §五十一

## 一、恢复三连与开工前置

- L1 三件套已加载（vr-role-brief / execute-checklist / agent-handoff §二）；角色=执行端。
- **批2 已终验入库确认**：git log `1c61339 feat(Q10 批2)…（HH.59 终验，D510）` + `8209fda chore: codely bridge 包…`（Packages 拆分 commit）——验收代执已由策划端完成。索引 HH.59 行状态仍挂"⏳待验收"，系回写滞后，本端随本回执登记一并代更正（事实同步，非裁决）。
- 工作区 M 四件（_任务队列/开发计划书/2_20.1/2_20 总纲）=策划端文档域更新，本批不触碰；HH.59 信件 untracked，随批3 收尾一并入库。

## 二、任务真源层级与范围声明

真源序=①清单 M6/M7 → ②总纲 §三/§四 → ③2_20.1 §二/§四 → ④0.6 §五十一 D490~D497（冲突以④为准）。

### M6（专属建筑×4）

| 项 | 实施 |
|---|---|
| 4 栋 SO | `Resources/Buildings/` 新建 WarAcademy/WarCamp/LeyForge/ArcheryRange BuildingDef（2×2、moduleType=Military、成本按 2_20.1 §三：学院金30石20/战营木25金10/熔炉金25石30/射箭场木30金15） |
| 建造链 | BuildingMenuPanel `LoadAll("Buildings")` 自动入列；**新增 BuildingDef.raceId 种族门禁**（菜单过滤 + KingdomManager.IsBuildingUnlocked AI 侧 + 每族限建 1 Registry 查重）；美术占位=BuildingType 尾插+PlaceholderSprites 色块 |
| 效果 | 战争学院=训练时长全局-25%（TrainingSystem.TryTrain 叠乘点，**HH.59 疑点④挂账清偿**）+溃败补充（阵亡 30s 内该兵种补训成本-50% 一次性窗口，UnitDiedEvent 驱动）；战营=唯一入口+战利品价值+50%（挂点=战利品结算处 D493）；熔炉=采矿+40%（GetGatherMul 唯一真源同点叠加）；射箭场=纯兵营 |
| AI 建造 | 解锁链/合法性本批（exclusiveBuildingDef 引用驱动+per-kingdom 解锁态）；建造优先级倾向=M8（批4） |

### M7（专属兵 7 职业+战争机器 4 台+共通槽退役）

| 项 | 实施 |
|---|---|
| Occupation 尾插 | 10 新枚举尾插（见 §三.1 编号偏移）；重装(8)/骑兵(12)/投掷机(13) 枚举保留 |
| 资产 | NpcProfessionDef×7（faction=PlayerCamp 单资产双端共用——SpawnUnit(PlayerCamp,…,kingdomId) 门面覆写 AiKingdom，批2 已实证链路）+机器 UnitData×4（mirror SiegeMachine/Ballista 惯例）+AmmoDef 按需（**ProjectileType 枚举住 AI.Core 红线区，零新增**：狼骑=Arrow/火枪=Bolt 直线/臼炮=Stone+HighArc+AOE3/藤蔓=Magic+Slow 魔弹减速语义转移 D497/重弩=HeavyBolt） |
| 训练链 | TrainingDef 新增 `raceId`（-1=共通）+`minBuildingLevel`（练兵场 Lv2 门槛=新机制，附录A「练兵场 Lv2 直接训练」落地载体）；条目=战营×2（狂战金3/1天·狼骑金4/2天）+射箭场×3（游侠金5水晶1/3天·风行者金3/2天·鹿骑金4/2天）+练兵场Lv2 人类×2（弩手金5水晶1·盾卫金4水晶1/3天）+矮人×2（火枪金5水晶1·磐石金4水晶1/3天）；**重装条目删除（D492）**；骑兵条目→人类专属+金5/2天占位（§1.11）；TrainingSystem/UI/TryTrain 三消费点 race 过滤 |
| 钩子 | 狂战士击杀 buff（UnitDiedEvent Killer==self&&Killed→攻速+30%/移速+20% 5s 叠3层）；盾卫庇护（30%/1 宏格/最近 1 盾卫/AOE 不转——DamageSystem 受方修正家族加重定向分支）；磐石远程减伤 45%（NpcProfessionDef 新字段 rangedDamageReduce）；火枪二段穿透（贯穿 1 额外目标 60%）；对建筑 ×2/×1.5（profile 新字段 buildingDamageMul，臼炮/攻城槌×2 重弩×1.5）；攻城槌对单位 0 伤+只以建筑为目标（数值特性非剧本）；兽人战利品 D493（Killer.raceId==Orc&&Killed→ChestManager.SpawnChest D142 同构金0.5~1，谁拾取归谁；战营 ×1.5；AI 镜像同享） |
| 机器生产 | SiegeProductionSystem 白名单收窄 per-race：投掷机(13) 生产入口退役（枚举保留，存量自然消耗）、弩炮(14)→人类重弩炮强化转正、尾插 3 台；机器种族门禁+成本沿 catapult/ballista 量级占位 |
| 收编 | 战马骑士=Cavalry 现成冲锋/训练链/枚举全复用（§1.11）；弩手/盾卫现成资产转正 |
| 文档 | QQQ.5 附录A 回填三笔（骑兵注记/重装退役注记/鹿骑+机器新增行）——附录A L94 明文"实施批落表"+清单 M7 指派，执行端代笔（设计文档正文只读红线的清单指派例外，特此声明） |
| 走查 | FormationController.IsRecruitable 白名单/TrainingPanel OccName 显示名/UnitDataManager 键位/编队美术引用面——结果随批报告 |

## 三、执行细节声明（路径唯一/文档授权，报备即可不阻塞）

1. **Occ 编号偏移**：设计稿 27~33/34~36 不可达——Monster=27 已占（2_14 尾插），int 铁律只能尾插 → **实际 Berserker=28/WolfRider=29/Musqueteer=30/Bedrock=31/Ranger=32/Windwalker=33/DeerRider=34/Mortar=35/VineCatapult=36/Ram=37**。尾插序语义不变，2_20.1 编号系初稿占位。2_20.1 文档修正建议随 HH.61 列报（本端不改）。
2. **熔炉叠算口径**：2_20.1 §三明示"实施二选一登记"→选 **乘算 1.3×1.4=1.82**（§三主口径+§8.2 强度梯度"矮人最强"自洽），随批登记。
3. **视野承载**：VisionConfig 实为迷雾探索开关（无个体字段）→风行者"视野 12"承载=NpcProfessionDef.perceptionRadius（个体感知半径，语义=个体视野），探针按此断言。
4. **M6 验收行初稿口径差异**：清单 M6"练兵场不出专属兵"与修订版冲突（矮人专属=练兵场Lv2）→按 2_20.1 修订版执行：唯一入口探针=战营（兽×2）/射箭场（精×3）；练兵场Lv2 出人类/矮人专属且 race 门禁生效（矮人练兵场不出弩手=负探针）。
5. **盾卫/战马骑士条目调整**：盾卫现 Barracks(Warrior→金3铁5/4天)→按设计迁练兵场Lv2（Resident→金4水晶1/3天）；骑兵现金3铁8/5天→金5/2天占位（from=Warrior 维持，§1.11"现成训练链保留"）。文档标"现值"与资产实值不符处，以设计值为准。
6. **AI 训练水晶缺口（既有非本批）**：TrainingSystem L246 AI 国库无水晶→含水晶职业 AI 不可经训练链（Mage/Healer 既有同款）；AI 专属兵获得=直产链（KingdomBrain SetOccupation 先例）归 M8。走查发现，列报。
7. **机器惯例**：§8.1 未指定处（乘员/弹药储备/静态标记）沿用现网 SiegeMachine/Ballista 资产惯例。
8. **存档零 bump 预期**：新职业=int 尾插旧档无新值；四建筑=buildingId 字符串链；战利品箱=ChestEntity D269 既有容器——预期零 schema 变更，随批申报核实。
9. **机器成本缺值**：2_20.1 §8.1 无 4 台造价 → 沿用 catapultCost/ballistaCost 量级占位（臼炮最贵梯度），§6.1"全部数值占位"授权口径，随批报批注。

## 四、sim-sync 义务清单（如实列报，不代做）

- 职业库登记：7 职业+4 机器战斗参数（15_账本）。
- 战斗公式对拍：盾卫庇护/磐石远程减伤/火枪二段穿透/对建筑 ×2·×1.5/臼炮 AOE3/藤蔓减速。
- 经济口径：战利品=兽人击杀收益入国库（sim 账面直入 vs Unity 箱拾取=表现差登记）。
- 狂战士击杀 buff 语义登记。
- **AI.Core 零直改预期**（ProjectileType/ProfessionSnapshot 复用现有字段；新战斗语义=账本登记非核内代码）→ 无 T 级代码同步义务，落地后按 sim-sync T/F 分级复核。

## 五、探针与回归计划

- 容器：新建批3 专用冒烟容器（复用 2_20_Smoke_Race 基座纪律：MainMenu 进局/活局副作用审计/材料自建自动回收/Play 模式编译挂起教训——改 Editor 脚本先退 Play 编译）。
- 探针=2_20.1 §6.2 修订版 11 条+§8.3 机器 5 条（正负双侧：共通槽退役=兽/矮/精无骑兵重装条目+机器种族门禁）+M6 探针（四建筑可建/唯一入口/限建1/学院-25%·熔炉+40% 行为正探针）。
- smoke/econ 回归：Smoke_12+2_17 步骤14（P0 基线）不退。
- 用户进局纪律照旧（MainMenu 正常进局选族，编辑器操作 MCP 自理，弹窗 Computer Use 自理）。

## 六、分批节奏与开工声明

- 批3a=M6 → 编译+编辑器自测；批3b=M7 → 探针跑批（用户配合）→ git status 全量对照 → HH.61 完成报告 → 策划端验收代执；每 3~5 项汇报。
- **本会话将改**：代码=UnitData/TrainingSystem/TrainingDef/BuildingDef/BuildingMenuPanel/DamageSystem/ProjectileManager/NpcProfessionDef/UnitController/NPCBrain(挂点)/SiegeProductionSystem/SiegeProductionConfig+新钩子系统文件；资产=Buildings×4+UnitData×11+AmmoDef 按需+RaceDef 四资产 exclusive 回填+TrainingConfig.asset；文档=实施清单状态行+QQQ.5 附录A 注记+HH.60/61+索引。
- **不碰**：设计文档正文（附录A 注记除外）/sim/AI.Core/策划端工作区四件；不 commit（验收代执）。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| （无阻塞级待决策——§二/§三 九项执行细节声明如有异议请逐条批注，无异议视为默许） | **✅ 2026-09-04 策划端过目：九项细节声明（①Occ 编号偏移 28~37 ②熔炉乘算 1.82 ③风行者视野=perceptionRadius ④M6 验收按修订版 ⑤盾卫/骑兵条目调整 ⑥AI 水晶缺口列报归 M8 ⑦机器惯例沿现网 ⑧存档零 bump 预期 ⑨机器成本占位）逐条核读无异议全默许——批3（M6+M7）放行续跑**。同串用户拍板三项：批3 继续跑完不中断；2_10 染色批批3 后升队；2_22 P0 清单批3 收尾期并行签发 | ①Monster=27 已占尾插正确符合 int 铁律 ②乘算与 §8.2「矮人最强」梯度自洽 ③~⑨=清单/修订版口径内合理落点；真分叉随 HH.61 完成报告再裁。附录A 落表三笔=清单指派例外确认有效 |
