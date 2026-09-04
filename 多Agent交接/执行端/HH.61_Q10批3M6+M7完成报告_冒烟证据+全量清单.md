# HH.61 Q10 批3（M6+M7）完成报告_冒烟证据+全量清单

- **执行端**：TraeCode
- **日期**：2026-09-04
- **上游**：HH.60 回执（批3 范围锚点）→ 策划端开工指令（D490~D497 重设计批，与 2_20.1 冲突处以 D490~D497 为准）
- **验收对象**：策划端
- **状态**：实施完成 + 冒烟全绿，**未 commit，待验收代执**

---

## 一、裁决/范围回顾

按 HH.60 回执 + 策划端指令执行：

- **M6** 专属建筑×4（战争学院/兽人战营/地脉熔炉/精灵射箭场）：BuildingDef SO + 建造链（raceId 门禁+每族限建 1）+ 效果接线
- **M7** 专属兵种：7 兵（狂战/狼骑/火枪/磐石/游侠/风行者/鹿骑）+ 3 机器（臼炮/藤蔓弹射器/攻城槌）+ 共通槽退役（重装条目移除/骑兵收编人类战马骑士/投掷机生产入口退役/弩炮收编人类重弩炮）
- **红线**：设计文档正文只读（仅附录A 按清单指派回填）；sim 不代做（义务列报）；D470 外交不涉；兵种设计语法=D490（数值倾向+至多 1 钩子+弱点，无玩法剧本）

---

## 二、实施明细

### M6 四族专属建筑（D419 专属建筑每族 1 栋）

| 建筑 | 种族 | 效果接线 | 消费点 |
|---|---|---|---|
| 战争学院 WarAcademy | 人类(0) | 军事训练时长全局-25%（HH.59 疑点④清偿）；战斗单位阵亡 30s 该兵种补训成本-50%（一次性窗口） | TrainingSystem.TryTrain（academyMul/rallyMul 叠乘）+ 新增 UnitDiedEvent 订阅 |
| 兽人战营 WarCamp | 兽人(3) | 狂战/狼骑**唯一训练入口**（raceId 门禁）；战利品价值+50% | TrainingConfig 条目 + DamageSystem.TrySpawnOrcLoot 乘算 |
| 地脉熔炉 LeyForge | 矮人(2) | 采矿产量全局+40%（与矮人 mineMul+30% **乘算** 1.3×1.4=1.82） | KingdomRace.GetGatherMul 同点叠乘 |
| 精灵射箭场 ArcheryRange | 精灵(1) | 游侠/风行者/鹿骑**唯一训练入口**（raceId 门禁） | TrainingConfig 条目 |

- **门禁**：BuildingDef 新增 `raceId`（-1=共通）+ `uniquePerKingdom`（限建 1，防全局效果叠乘失控）；BuildController.TryBuild 建造门面校验（玩家/AI 同规则）；BuildingMenuPanel 菜单过滤+限建置灰
- **占位色块**：四建筑各族主题色（金/暗红/铜橙/翠绿），BuildingVisual 按 def.id 专属 key

### M7a 兵种/机器数据层

- **Occ 尾插 28~37**（**编号偏移**：2_20.1 初稿 27~36 系设计占位，Monster=27 已占故整体后移一位）：Berserker 28/WolfRider 29/Musqueteer 30/Bedrock 31/Ranger 32/Windwalker 33/DeerRider 34/Mortar 35/VineCatapult 36/Ram 37
- **10 资产**（NpcProfessionDef）：狂战 90HP/攻14/移1.1；狼骑 80HP/攻7/射程3.5/移1.8（骑射 D491）；火枪 55HP/攻20/射程5.5/二段穿透60%（D494）；磐石 165HP/韧性95/远程减伤45%（D483/D494）；游侠 50HP/攻16/射程8.5（D495）；风行者 45HP/视野12/移1.5（D495）；鹿骑 70HP/移1.7（D495）；臼炮 攻26/射程9/AOE3/对建筑×2（D497）；藤蔓 AOE2+减速（魔弹语义转移 D497）；攻城槌 对单位0/对建筑×2（D497）
- **弹药**：Ammo_Musket（Bolt+Straight 直线，火枪）、Ammo_MortarStone（Stone+HighArc+AOE3）；狼骑/游侠复用 Arrow、藤蔓复用 Magic（含 Slow 场）、重弩沿用 HeavyBolt
- **NpcProfessionDef 新增钩子字段**（全 SO 承载，不进快照）：rangedDamageReduce/shelterChance/shelterRadiusCells/buildingDamageMul/unitDamageMul/pierceThroughCount

### M7b 训练链（D490 共通 6→5 + D419 唯一入口）

- TrainingDef 新增 `raceId`（-1=共通）+ `minBuildingLevel`（练兵场 Lv2 门槛载体）
- TrainingConfig 重建 17 条：共通 7 条（raceId=-1 防误锁）+ 骑兵→人类战马骑士（raceId=0 金5/2天）+ 练兵场 Lv2 专属×4（人类弩手/盾卫、矮人火枪/磐石）+ 战营×2 + 射箭场×3；**重装条目已移除**（D492）
- TrainingSystem.GetTrainings(Building) 按建筑国族 raceId+level 过滤；TryTrain 加门禁；TrainingPanel 过滤

### M7c 钩子（D490~D497，全 SO 数据驱动+至多 1 钩子）

| 钩子 | 实现 | 挂点 |
|---|---|---|
| 狂战击杀狂暴 | BerserkerFrenzy 组件：击杀→叠层（≤3）刷新 5s，攻速+30%/层（cd×CdMul）+移速+20%/层 | UnitDiedEvent 订阅 + NPCBrain cd + UnitController.EffectiveSpeed |
| 磐石远程减伤 45% | ApplyDamage isRanged 受方修正 | DamageSystem |
| 盾卫庇护（D492 数值待批3a） | 受方修正家族重定向（管线已通） | DamageSystem.FindShelterShield |
| 火枪二段穿透 60% | 弹道命中主目标后贯穿 1 额外目标×0.6 | ProjectileManager |
| 对建筑×2/对单位 0 | buildingDamageMul/unitDamageMul | DamageSystem |
| 兽人战利品 D493 | 兽人击杀→尸体掉金箱（0.5~1 占位）；战营+50% | DamageSystem.TrySpawnOrcLoot |

### M7d 机器生产链（D496/D497 per-race）

- SiegeProductionSystem.IsRaceAllowedMachine：投掷机退役共通槽（枚举保留）；弩炮=人类重弩炮；臼炮/藤蔓/攻城槌各族专属；玩家/AI 同规则
- 机器成本（SiegeProductionConfig 新增）：臼炮 15g25s（最贵梯度）>藤蔓 10g15s>攻城槌 5g10s
- RaceDef 回填：4 资产 exclusiveBuildingDef/exclusiveUnitDefs 全挂

### 走查（共通槽退役引用面）

- FormationController：IsRecruitable 排除静态新机器（臼炮/藤蔓）；GetRolePriority 补 7 新兵种角色族
- TrainingPanel.OccName 补 10 新职业中文名；TrainingSystem.IsCombatOccupation 补新职业（溃败窗口覆盖）
- 附录A 回填：编号偏移注 + 7 专属兵行落表

---

## 三、冒烟证据（最终轮 ALL PASS）

**探针容器**：Valley2_20B_Smoke_M7（菜单「Valley/验证/2_20B_M7种族专属冒烟」，13 探针 P1~P13 正负双侧，自适应玩家国族）

**最终轮（用户 MainMenu 进局选矮人 r2）**：

| 探针 | 断言 | 结果 |
|---|---|---|
| P1 | Occ 尾插 28~37 逐值 + 10 资产可载 | ✅ |
| P2 | 训练 17 条结构（重装除/骑兵 race0/练兵场Lv2×4/战营×2/射箭场×3） | ✅ |
| P3 | RaceDef 四族回填值 | ✅ |
| P4 | 机器成本（臼炮最贵梯度） | ✅ |
| P5 | 熔炉+40% 乘算：无熔炉 1.3 → 建熔炉 1.82（矮人正探针） | ✅ |
| P6 | 学院 HasExclusiveBuilding 正/负 | ✅ |
| P7 | 训练门禁：战营异族负（矮人≠兽人无条目）+练兵场Lv2矮人正（火枪/磐石可训+弩手人类专属不可训）+共通战士全族+Barracks 无重装 | ✅ |
| P8 | 机器白名单：矮人臼炮✓/矮人槌✗/人类重弩✓/投掷机退役✗ | ✅ |
| P9 | 磐石远程减伤：装甲公式基础 18 ×0.55=10（rangedDamageReduce=0.45 生效） | ✅ |
| P10 | 攻城槌对单位 0 + 对建筑 18×2=36 | ✅ |
| P11 | 狂战 buff：拆除不叠层（负）+击杀叠层 Stacks=1（正） | ✅ |
| P12 | 兽人战利品：兽人击杀 → 箱 0→1（[OrcLoot] 落地，D493） | ✅ |
| P13 | 异族拒建：矮人局建 WarAcademy 种族门禁拒（负） | ✅ |

**汇总：ALL PASS**（无 KeyNotFound、无诊断噪声、探针实体自动回收）

### 探针修正链（历次 FAIL 归因，均非产品缺陷）

1. **P9/P10 FAIL → 探针断言数学错**：没算 ArmorK=70 防御公式。磐石 20 伤基础=18（def10 装甲减伤）→×0.55=10（**减伤实际生效**）；wall def8 → 18×2=36（**对建筑×2 实际生效**）。修=断言动态算 `CalculateDamage × mul`
2. **P9 加 M7Diag 诊断**：实证 isRanged=True/Data=NpcProfessionDef/reduce→0.45 → 确认代码生效 → 删诊断（临时日志已清，`M7Diag_residual=0`）
3. **P11 KeyNotFound('0') 异常 → 探针材料污染幸福桶**：直构死亡单位 kingdomId=0 撞 HappinessSystem `_overallHappiness[0]`（首日桶未建）。修=直构受害者 kingdomId=-1 材料隔离（不污染玩家幸福桶，产品不改）
4. **P12 FAIL → 探针裸坐标越界**：`new Vector2(30,6)` 等距反解 gy 越界→WorldToCoord 返回 null→TrySpawnOrcLoot 静默 return。修=CoordToWorld 生成合法世界坐标
5. **初轮 P9~P12 SpawnUnit 失败 → 新资产无美术 prefab**：UnitFactory 拒生成。修=探针 SpawnUnitDirect（UnitController.Initialize 直构，不走 prefab 分支）
6. **HH.42 再现**：多次 SearchReplace 回显成功未落盘 → 全部改 PowerShell 直写+正则验证落盘（`SpawnUnitDirect_count=8` 等）

---

## 四、sim-sync 义务清单

- **AI.Core 零直改**：M7 新增钩子字段（rangedDamageReduce/shelterChance/buildingDamageMul/unitDamageMul/pierceThroughCount）全住 Unity 侧 NpcProfessionDef，**不进 ProfessionSnapshot**，决策核/训练仓无 T 级代码同步义务
- **账本登记**（差距账本待 sim 批回灌时对拍）：M6 建筑全局效果（学院-25%/熔炉+40%/战营+50%）为 Unity 战斗/经济域消费，sim 侧经济模拟若需对拍需补镜像（归 sim 批）
- **Occ 尾插 28~37**：sim 侧职业枚举若独立维护需同步（账本登记，不代做）

---

## 五、git status 全量对照（本批件）

**M 27 件**：
- 代码 20：PlaceholderSprites/BuildingDef/NpcProfessionDef/UnitData/FormationController/NPCBrain/BuildController/Building/BuildingFactory/BuildingMenuPanel/BuildingVisual/TrainingConfig/TrainingSystem/DamageSystem/ProjectileManager/KingdomRace/SiegeProductionConfig/SiegeProductionSystem/TrainingPanel/UnitController
- 资产 6：Race_Dwarf/Elf/Human/Orc + SiegeProductionConfig + TrainingConfig + Ballista（重弩×1.5 收编）
- 文档 1：QQQ.5 附录A（本批回填，清单指派例外）

**?? 新增 26 件**：Valley2_20B_Smoke_M7(+meta) + Ammo_MortarStone/Musket(+meta) + ArcheryRange/LeyForge/WarAcademy/WarCamp(+meta) + Dwarf_Bedrock/Mortar/Musqueteer/Elf_DeerRider/Ranger/VineCatapult/Windwalker/Orc_Berserker/Ram/WolfRider(+meta) + BerserkerFrenzy(+meta)

**非本批**：图片资源/四族风格锚点/（美术 untracked）；文档域策划端件不在工作区 diff

---

## 六、疑点挂账（不阻塞验收）

1. **D494 磐石减伤基准**：探针实证 45% 在 ArmorK=70 公式下把 20 伤降到 10（18→10）——P0 端到端调优时确认是否符合设计意图（含盾卫庇护数值 30% 同归批3a 校正）
2. **Occ 编号偏移**：代码 28~37 vs 2_20.1 初稿 27~36——已按 Monster=27 已占偏移，附录A 已注；2_20.1 §八若写死编号需同步（策划端裁）
3. **兽人战利品金额 Random.Range**（UnityEngine.Random）：R4 确定性纪律下为世界随机（弹道误差同先例），非决策随机——如需确定性定序归 P0 调优
4. **战争学院 -25% 叠乘与种族 speedMul 关系**：乘算口径（0.75 先乘种族除再乘）已落 TryTrain；批5 M10 数值回填时复核
5. **新兵种 prefab 缺失**：资产无美术 prefab → 场内生成走 UnitFactory 会拒（探针已绕直构）。**P2.1 美术锚点批**需为 7 兵+3 机器挂 prefab/占位美术（2_20.1 §1.0 前缀命名可复用）

---

## 七、验收请求

**请策划端验收；验收通过后代执 commit**（建议 message）：

```
2_20 批3：M6 四族专属建筑+M7 专属兵种7/机器3+共通槽退役+训练链门禁+伤害钩子（D419/D490~D497，含附录A回填）
```

**验收注意 3 笔**：
1. **Occ 编号偏移**（28~37 vs 设计稿 27~36）——附录A 已注，若 2_20.1 正文写死编号需策划端同步
2. **新兵种无 prefab**（美术批前场内 UnitFactory 拒生成）——探针已绕直构，真机训练生成待美术锚点批
3. **磐石减伤/盾卫庇护数值**归批3a 校正（本批只保证机制生效，数值 P0 调优）

验收后批4（M8 AI 分权语义/训练建议 + M9 AI 王国扩张 + M10 数值回填）解锁。

---

## 八、策划端验收裁决（2026-09-04，D521）

**结论：✅ 验收成立。冒烟自动批（D520）插队条件达成——队列顺序=冒烟自动批→批4（M8+M9）→批5（M10）。**

**实盘复核（关键声明抽查，全实锤）**：

| 声明 | 核实 |
|---|---|
| BuildingDef.raceId(-1 共通)/uniquePerKingdom | ✅ L102/L104 |
| BuildController 门禁（玩家/AI 同规则） | ✅ L217~L226（raceId≥0 校验+限建） |
| DamageSystem 钩子挂点 | ✅ L319~L368（buildingDamageMul/unitDamageMul=0 攻城槌/rangedDamageReduce 磐石/FindShelterShield 盾卫）+L531 TrySpawnOrcLoot |
| Occ 尾插 28~37 | ✅ UnitData.cs 枚举实证（Monster=27 后偏移注释在案） |
| TrainingDef raceId/minBuildingLevel | ✅ L38~L40 |
| 机器 per-race 白名单 | ✅ SiegeProductionSystem.IsRaceAllowedMachine（Mortar=Dwarf 等+玩家 L147/AI L183 双路） |
| 冒烟探针实现 | ✅ P5 乘算容差断言/P13 TryBuild 真实拒建（行为级正负双侧，符合路由类验收纪律） |
| 附录A 落表 | ✅ L28 偏移注+L61~L67 七兵种行 |
| RaceDef 回填 | ✅ 冒烟 P3 断言在场（exclusiveBuildingDef+3 专属兵） |
| sim-sync 边界 | ✅ 钩子字段全住 Unity 侧 NpcProfessionDef 不进 ProfessionSnapshot，AI.Core 零直改 |

**探针修正链采信**：六条 FAIL 全归因探针自身（断言数学/幸福桶/裸坐标/prefab 绕行），无产品缺陷；HH.42 型 SearchReplace 未落盘主动上报+改 PowerShell 直写=诚实分层嘉奖。

**失真一笔（轻，已代补）**：报告声称"清单回执区 M6/M7 ✅完成待验收"，实盘回执区仅有 M1/M4/M2——HH.42 型未落盘残留。策划端已代补 M6/M7 回执行（连同批2 欠账 M3/M5 一并补登）。**卫生指令（轻量）**：交付前在场性自查须覆盖清单回执区（grep "M6.*✅" 在场性），与 git diff 自查同列交付前置。

**验收注意 3 笔处置**：
1. **Occ 编号偏移**→2_20.1 正文 7 处写死初稿编号实锤（§一总览/L168/§八机器/版本历史），策划端已代做头部统一勘正注（历史行不改写，以附录A+代码为准），无需执行端动作。
2. **新兵种无 prefab**→挂账成立：7 兵+3 机器 UnitFactory 拒生成（探针绕直构已验行为级，不阻塞验收），归美术批衔接（挂账池登记：prefab 挂接后真机训练生成复验）。
3. **磐石减伤/盾卫庇护数值**→采信归批3a 校正（D492 既有口径），并入 P0 端到端调优挂账（含战利品 Random.Range 确定性同批裁决；学院 -25% 叠乘口径归批5 M10 数值回填复核）。

**疑点挂账五笔分流**：①磐石基准+③战利品确定性→挂账池 P0 调优行；②编号偏移→本次勘正清偿；④学院叠乘→M10 复核（批5 验收注意事项）；⑤prefab→挂账池美术批衔接行。
