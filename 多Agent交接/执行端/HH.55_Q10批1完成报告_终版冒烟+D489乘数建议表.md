# HH.55 Q10 批1 完成报告（M1/M4/M2 数据层 · 含终版冒烟+D503 乘数建议表）

> 类型：交付报告（待策划验收；commit 由策划端代执）
> 状态：✅ 批1 全项完成（冒烟三轮演进：③真字段核心全绿；④c 重放波动挂账上报）
> 日期：2026-09-04 · 发起端：执行端 · 关联：HH.54 回执、策划端批1 收尾指令（D502 映射终裁/D503 乘数/D504 projDiag）、前半已入库 commit 33ca38a/14b95fc/34c22e1

## 一、交付总览

**编译基线：全部改动后 0 error**（read_console 实盘：error CS 0 命中，本批触碰文件 0 新增警告）。M1/M4 已由策划端验收入库（33ca38a），本报告后半=M2+projDiag 删除+冒烟终版。

### M1 ✅（已入库）
[RaceDef.cs](Valley Rampart/Assets/_Game/Data/Races/RaceDef.cs)+四资产 `Resources/Config/Races/`（五轴 D426 基准+军事 5 乘 trainCost/trainSpeed/meleeAtk/rangedAtk/moveSpeed+经济 6 乘 mine/lumber/farm/buildSpeed/buildingHp/carryCap+gatherBonusOnPreferred；两散点锚 Orc trainCostMul 0.85/Dwarf mineMul 1.30 实装；缺表乘数 1.0 中性=D503 口径）。正/负探针全过。

### M4 ✅（已入库）
KingdomPreferredFeature 尾插 MineralRich(3)/BarrenRich(4)；四资产 preferredFeature=0/2/3/4。

### M2 ✅（本批，全项）
| 项 | 证据 |
|----|------|
| KingdomDef.raceId 字段 | [KingdomDef.cs](Valley Rampart/Assets/_Game/Data/Kingdoms/KingdomDef.cs)（int，默认 RaceIds.Human，D502 映射注记） |
| KingdomState.raceId 真字段 | [KingdomState.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/KingdomState.cs)（国族构造性不变 D467） |
| 入档三处 | [KingdomRegistry.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/KingdomRegistry.cs)：KingdomEntryData 加字段+SaveState 写入+LoadState 恢复。**存档评估（2_11 纪律）=附加字段+JsonUtility 缺省解析旧档缺字段→0=Human（旧档全 Human 世界语义正确）→零迁移器改动、无需 bump**（CurrentSaveVersion 维持 3），申报知悉 |
| GetKingdomRace 单点回填 | [KingdomRace.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/KingdomRace.cs)：恒 Human 临时实现退役→读 `KingdomRegistry.Get(kingdomId).raceId`（Registry 缺失/查无→Human 兜底） |
| Foundry 两处写入 | [KingdomFoundry.cs](Valley Rampart/Assets/_Game/Systems/Kingdom/KingdomFoundry.cs)：①FoundFirstGeneration AI 建国 `state.raceId = tpl.raceId`（字段接通；保底席/随机池分配策略归 M3 不抢跑）②FoundFromCamp D471 `state.raceId = campRace`（**HH.53 §三.3 挂账清偿**） |
| 6 模板 D502 映射赋值 | YAML 实盘对账：Bedrock=2/SnowRock=2/DenseForest=1/GoldenWheat=0/RiverBay=0/IronHoof=3；git diff 证 6 资产各恰 +1 行 raceId 零其他改动 |

### projDiag 删除 ✅（D504，P3Diag 先例）
[ProjectileManager.cs](Valley Rampart/Assets/_Game/Systems/Combat/ProjectileManager.cs) 两块临时诊断删除（Spawn 处+到达判定处）；D500 弹道命中放行逻辑完整保留（14b95fc 已入库基线上仅删诊断）；删后编译 0 error。

## 二、终版冒烟（第三轮，用户 MainMenu 正常进局世界）

**探针③（本批核心新增）全绿**：
```
③ D471 插旗定族：立国 4→5=OK 定族日志=在(raceId=1)=OK 成员 raceId 保持(Elf)=OK
   真字段 state.raceId/helper(Elf)=OK(id=4)
```
即：新国 id=4 的 KingdomState.raceId=Elf=1（D471 显式写入生效）+ KingdomRace.GetKingdomRace(4)=Elf（helper 单点回填生效）——**M2 全链路（模板映射→AI 建国写入→真字段消费）行为级证实**。

全表回归：⑤a/b/c/d OK（含开关负探针）｜①正/①负 OK｜②a/②b/②b2/②c/②d OK（含 D500 三负探针）｜④a 异族拒绝 OK（日志=在）｜④b 同族放行 OK｜④d 全异族→null OK。

**④c 重放波动 FAIL（挂账上报，不擅裁）**：AI⑥同族过滤选中 #-1。三轮实证链：①第 2 轮 e3 alive=False（野性敌意致死）致 ④a/④c 材料缺失→加"材料保鲜"（死亡原坐标补注）→③第 3 轮 ④a 转绿，④c 仍 null。已核 FindRecruitableVagrant（[KingdomBrain.cs](Valley Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs) L312-327）五条件过滤与 GetKingdomRace(0)=0（现场 execute_code 实证），代码 L323 **现消费 M2 真字段且值正确**；④c 代码本批零改动（种族1 已验收项 96b3a26）。疑点=材料补注入零等待窗口 vs 原版注入后 ~11 秒（②c/②d 等待段）的时序差异（UnitRegistry 注册/脑初始化/位置互扰），需专项轮实锤。**处置建议**：A（推荐）④c 挂账至批2（M3+M5）冒烟自然重放验证（补注入后加 5 帧 yield+放宽为"选中任一未招募 Human 流民"的语义变更请策划先批）；B=要求第四轮专项重跑。

**容器演进沉淀（三轮教训，均为容器缺陷≠玩法缺陷）**：
1. 用户新纪律（常设，09-03）：冒烟一律用户 MainMenu 正常进局后触发，MCP 自建世界禁作正式结果——已照办。
2. 探针③禁盲用 FindCamps()[0]：真实世界既有营地可能 foundedFlag 已置位/中心格有主，TickAll TryAnnex 静默吞并（实测 camps 5→0）→改为"未立国+无主自证复用，否则自建（账本反查预检+放置成功即选定）"。
3. 探针材料保鲜：④段 e3/h2/v4/h3 死亡即原坐标补注（野性敌意对调试流民真实生效=环境真实性，也带来材料易损）。

## 三、D503 各族乘数建议值表（供策划批注；M5/M8 真值挂载前终核）

现状=M2 资产缺表乘数 1.0 中性+两散点锚实装。下表为完整建议（锚点已实装加粗）：

| 乘数 | 人族 | 精灵 | 矮人 | 兽人 | 设计依据 |
|------|------|------|------|------|----------|
| trainCostMul | 1.00 | 1.05 | 1.10 | **0.85**（锚） | 兽人量大管饱/矮人精贵 |
| trainSpeedMul | 1.00 | 1.00 | 0.95 | 1.15 | 兽人速成 |
| meleeAtkMul | 1.00 | 0.90 | 1.15 | 1.15 | 矮人/兽人近战 |
| rangedAtkMul | 1.00 | 1.20 | 0.80 | 0.85 | 精灵射术 |
| moveSpeedMul | 1.00 | 1.15 | 0.85 | 1.05 | 精灵机动/矮人重装 |
| mineMul | 1.00 | 0.90 | **1.30**（锚） | 1.05 | 矮人矿业 |
| lumberMul | 1.00 | 1.25 | 0.90 | 0.85 | 精林木艺 |
| farmMul | 1.00 | 1.10 | 0.75 | 0.70 | 兽人游牧/矮人洞居 |
| buildSpeedMul | 1.00 | 0.90 | 1.20 | 0.80 | 矮人工程 |
| buildingHpMul | 1.00 | 0.90 | 1.25 | 0.85 | 矮人石工 |
| carryCapMul | 1.00 | 0.95 | 1.20 | 0.90 | 矮人负重 |
| gatherBonusOnPreferred | 0 | +0.15 | +0.15 | +0.10 | 人类 Any=0 语义 |

批注后由执行端随批2 M5 真值挂载（RaceDef 资产批量改值）。

## 四、git status 实盘全量清单对照（卫生指令，勿凭记忆）

### 本批执行端产物（M2 范围内）
- M `Assets/_Game/Data/Kingdoms/KingdomDef.cs`（raceId 字段）
- M `Assets/_Game/Systems/Kingdom/KingdomState.cs`（raceId 字段）
- M `Assets/_Game/Systems/Kingdom/KingdomRegistry.cs`（入档三处）
- M `Assets/_Game/Systems/Kingdom/KingdomRace.cs`（回填）
- M `Assets/_Game/Systems/Kingdom/KingdomFoundry.cs`（两处写入）
- M `Assets/Resources/Config/Kingdoms/Kingdom_{Bedrock,DenseForest,GoldenWheat,IronHoof,RiverBay,SnowRock}.asset`（6 模板 raceId）
- M `Assets/_Game/Systems/Combat/ProjectileManager.cs`（projDiag 删）
- M `Assets/Editor/Smoke/Valley2_20_Smoke_Race.cs`（探针③真字段断言+容器修正+静默开关）
- ?? `多Agent交接/执行端/HH.54_…回执.md`、`HH.55_…报告.md`（本文件）
- M `河谷防线开发计划书具体内容/改造计划/2_20_四族种族体系_实施清单.md`（回执区状态行，执行端职责内）

### 非本批工作区遗留（如实列报，处置请裁）
- M `Packages/manifest.json`+`packages-lock.json`——**非本批改动**（Unity 包管理自动变更？）执行端未触碰，来源待查
- ?? `图片资源/四族风格锚点/`——疑似美术/用户素材，非本批
- ?? `改造计划/2_21_AI涌现范式升级.md`+M `0.6/0_改造总计划/2_20.1/2_20_总纲/_目录/3.1.2/3.1.3/3.6/QQQ.5/_交接索引`——策划端/文档审查端文档域（34c22e1 已 commit 一部分，现 M=其后改动），执行端仅动 2_20_实施清单回执区与 HH 文件

### 台账卫生注记（更新）
~~指令 D488/D489/D490 与 0.6 编号重叠~~ → **修正（2026-09-03 策划端复核）**：34c22e1 未含 0.6（0.6 补登挂账随 D483 批）——0.6 现存 D484~D488/D490~D497 字样系**并行 2_21 涌现批/四族兵种重设计批**登记（非种族1 链），编号碰撞实锤；种族1 验收裁决链已重编（Worker 基线 D498/溯源放行 D499/自卫层 D500/终验 D501/映射 D502/乘数 D503/弹道 D504/M10 D505，D489 预留位注销），映射表随 0.6 补登落档

## 五、批1 收口状态与下一步

- **批1=M1+M4+M2 全项完成**；③真字段核心全绿；④c 挂账（处置建议 §二）。
- 策划端验收全绿后 **代执 commit**（建议拆两笔：①M2 代码+模板+projDiag 删 ②冒烟容器+文档）。
- 批2=M3+M5（AI 保底席/随机池分配 D430+玩家选族绑定 M5）——待批1 验收后开工；D503 建议表批注值随批2 M5 挂载。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| ④c 处置 | **A 挂账批2 重放**（D498 基线上探针修正预批：材料补注入后 5 帧 yield+断言放宽「选中任一未招募同族流民」——原特定个体断言=时序敏感假阴性非产品缺陷，代码零改动+GetKingdomRace(0)=0 现场实证）；批2 重放仍异常→升级专项 |
| D503 乘数建议表 | **批注通过（占位生效）**：12×4 与 2_20 §二 定位矩阵逐族吻合+D403 弱点必配（每族有 buff 有 debuff）；两处 P0 调优观察点=①矮人 meleeAtk 1.15 攻守双强（抗线定位观察近战占比）②兽人 farm 0.70+矮人 0.75 双低粮链压力（D401 收敛后 farm 语义）；终核=M5/M8 挂载后端到端调优 |
| D503 乘数建议表批注（12 项×4 族） | | |
| 存档评估申报 | **零 bump 知悉确认** | 附加字段+JsonUtility 缺省解析=批1 收尾指令预批的预期设计；CurrentSaveVersion 维持 3 正确；旧档缺字段→0=Human 全 Human 世界语义正确 |
| 非本批遗留处置 | **登记不阻塞**：Packages 双 json=Unity 包管理自动变更（Q9 bridge 先例）→下次 Unity 会话 diff 来源确认后登记或随批入库；图片资源/=美术批1 用户件（D483 域） |
