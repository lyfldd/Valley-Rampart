# HH.79 AI 人口再生批完成报告（AI 生育分支+Child→Worker 直生+流浪侧复通）

> 类型：完成报告（施工批·正式）
> 状态：⏳待策划端验收
> 日期：2026-09-05 · 发起端：执行端 · 任务书：策划端/HH.78_AI人口再生批_施工任务书.md（D540）
> 冒烟：**ALL PASS 6/6 双连庄（六轮+七轮）** + json 存档补证三方吻合

## 一、三件施工 diff

### 件1 AI 生育分支（面②，PopulationSystem +154 行主体）

- **挂点**：DayCycleSettlement L67 后插 AI 生育循环——`for regAll → if (!regAll[i].IsPlayer) pop.OnNewDayPerKingdom(regAll[i])`（+9）。
- **OnNewDayPerKingdom(KingdomState k)**（L328 起）：条件输入三处 per-kingdom 化——幸福 `hap.GetKingdomHappiness(k.id)`（HH.73 批3a 已备读口）/饱食 `sat.GetAverageSatiety(k.id)`/房容 `hap.GetHouseCapacityByKingdom(k.id)` vs `k.workerCount+k.warriorCount`；玩家轨 `OnNewDay` 逐位不动（零回归红线）。
- **配对池**：UnitRegistry 按 `kingdomId` 过滤，Worker/Porter/Resident 均可配对；**先收集 candidates 快照→npcId 固定序排序→种子 rng**（`seed^(day*7919)^(k.id*104729)`，R4 纪律，同 (seed,day,k.id) 恒复现）。
- **生成**：`SpawnUnit(Faction.PlayerCamp, Occupation.Child, birthPos, k.id)` + `raceId=GetKingdomRace(k.id)`（Foundry/Siege 现网组合先例）；出生落点=该国活动 House 旁（`GetKingdomBirthPosition`，无房兜底=国锚点）。
- **冷却**：`_aiBirthCooldowns` Dict per-kingdom；条件不满足重置冷却（对齐玩家 L273 语义）+条件诊断日志（幸福/饱食/房容 Bool+工人池规模——P0 调优观测口）；ResetState 清冷却（+跨轮清场）。
- **SO 参数**（KingdomConfig +8）：`aiBirthIntervalDays=10`、`birthHappinessThreshold`/`birthSatietyThreshold` 同表 60/50（占位，P0 调优回调），Header 注「AI 人口再生（HH.78/D540 混合双通道·C 路线）」。

### 件2 Child→Worker 直生（面③，TickChildGrowth per-kingdom 扩）

- **AI 段**（L491-516 新增）：UnitRegistry 按 `kingdomId>0+Child` 过滤（先收集 aiChildren 快照再遍历——雷区纪律），成长满 → `SetOccupation(Occupation.Worker)` 直生（AI 无 Resident 体系，绕 ⑥ 不占流浪供给——Gate 面③裁决）+「AI小孩长大」日志；成长耗粮走 Satiety per-kingdom 路由（D453 零新增）。
- **玩家段守卫**（L481）：`if (u.kingdomId > 0) continue;`——防 AI Child 被玩家段转 Resident（_entities 注册链时序 bug 既有面，见列报④；实测 D23「小孩长大→居民 Human_Player_Child」复现路径）。

### 件3 流浪侧复通（面④方案 A 四子件，VagrantCampSystem +86）

1. **营地实体复通**：WorldManager.PlaceVagrantCamps **全项目零命中=幽灵引用**（仅 VagrantCampSystem L6 注释提及，P1 两跑行为级 FindCamps 永空佐证断链）→ 选 **OnNewGameMapReady 补建**（恢复调用侧无挂点可复用）：锚点必建 `TryBuildCampAt(anchor)` + 种子 rng 散投组 `TryBuildCampAt(cell)`（CreateBuildingInstance kingdomId=-1，照 PlaceCampWell 模式）。
2. **自然增长刷点**：OnNewDay 尾插 `TryNaturalRespawn(cfg, rng)`——`vagrantRespawnIntervalDays=5`/`respawnGroupSize=2`（SO）；无主地判定 `ts.GetAllTerritory()` 不含目标 chunk + 避开 AI 领土（D469 纪律维持）+组内同族+随营补建+一组一地。
3. **族别映射回填**（Q10-M2 挂账清偿）：OnNewGameMapReady anchorRace/groupRace 硬编码 `GetKingdomRace(0)` 改 `racePool={0,1,2,3}`+rng 抽取——**「按族投放」结构不动，只改族值来源**（策划端补记口径）。
4. **参数 SO 化**：5 日/组 2 两笔入 KingdomConfig（零魔法数）。
- 观察器顺手修正：白名单 `[VagrantCamp]` → `[VagrantCamp`（无右括号——原带括号精确串漏匹配 [VagrantCampSystem] 前缀）。

## 二、冒烟证据链（Valley_HH78_Smoke，seed=20273，45 游戏日压场@5s/日×3x）

### 迭代史（七轮）

| 轮 | 结果 | 关键证据/事件 |
|---|---|---|
| 1 | 部分通 | 首跑；45 日大世界 Load 卡死 6min+ → P3 改 Save-only（列报⑤） |
| 2 | 部分通 | 件3 营地/刷点首证 |
| 3 | P1 FAIL | AI Child 未诞生 → 破案：**AI Child 进了 _entities 被玩家段转 Resident**（注册链时序 bug，列报④）→ 玩家段守卫 |
| 4 | P2 FAIL | 镜像实锤件3 全链：营地补建 D1×2/D2/D7/D12/D17+刷点每 5 日 +2（族 0/1/2/0 轮换）；件1 生育 D21/31/41/51 四胎 10 日节律精准（幸福 74~79/饱食 66~79 过门槛，raceId=1）；P2「AI小孩长大」**零命中** |
| 5 | P2 FAIL | 同上——守卫拦下 AI Child 后无人处理 → **grep 实锤件2 AI 段从未落盘**（HH.42 复发第 6 笔：SearchReplace 回显 diff 骗过验证，守卫 L481 在而 AI 段缺）|
| 6 | **ALL PASS 6/6** | AI 段补写（grep 双锚点复验）+P2 判定改双证口径（主证=镜像日志，弱判定 workerCount>6 废弃——AI 招募 Resident 入籍不计 workerCount）|
| 7 | **ALL PASS 6/6** | 稳定性复跑+P3 存档镜像拷贝（QuitSmoke 自清 smoke_ 槽——HH.66 防堆积纪律实锤，save=True 但文件被收尾删→Save 后立即拷贝到 Logs/P1）|

### 七轮终局（六轮/七轮双连庄）

- **P4a 结构 营地实体=2+流浪族别多样性 True（族集[3,1]）**
- **P1 生育 AI Child 诞生 k1 raceId=1（期望1✓）**
- **P2 成长 AI小孩长大日志×4 workerCount峰=10(基线6)**
- **P4b 流浪峰 131/123（六/七轮）**
- **P3 存档 save=True 快照[k1=10 k2=6 k3=6]**
- **P5 雷区 InvalidOperationException 命中=0**

### json 存档补证（七轮镜像 hh78_p3_save.json，1.73MB，统计脚本 Logs/P1/hh78_stat_units.ps1）

- UnitSaveData 总数 184；**`occ=23 kid=1 race=1 ×4`** = k1 四胎全直生 Worker（raceId=国族 Elf）——与镜像「AI小孩长大×4」+P3 快照 k1=10（=6 基线+4 直生）**三方吻合闭环**；
- k2/k3 各 6 原生（未达生育条件=冷却/门槛正常）、AI 侧零 Child 残留（成长链 2 日全消化）；
- 流浪 occ=25 ×123 四族分布（0=89/1=23/2=1/3=10）=四族轮换刷点实锤；
- 既有面旁证：k1 原生 6 Worker 全 race=0（Foundry 预置 raceId=0 同款，列报④关联）。

## 三、列报项（随本报告，策划端登记）

1. **PlaceVagrantCamps 断链定位结论**：WorldManager 无此方法，全项目零命中——L6 注释称「地图生成时由 WorldManager.PlaceVagrantCamps 建营地」=幽灵引用（从未实现或早期移除未清注释）；P1 两跑行为级 FindCamps 永空实锤。处置=OnNewGameMapReady 补建（方案 A）。
2. **参数落点**：全部入 KingdomConfig SO——aiBirthIntervalDays=10 / birthHappinessThreshold=60 / birthSatietyThreshold=50 / vagrantRespawnIntervalDays=5 / respawnGroupSize=2（占位值，P0 调优回调）。
3. **sim 人口再生语义**（训练仓 harness/Economy，只读）：**有生育无成长**——SimEconomy.TryBirth()（L213）有幸福/饱食门槛+冷却（与 Unity AI 生育分支同构）；但 sim 无 Child→Worker 成长转换段（Child 仅人口池计数+耗粮 L174 参与；Worker 来源=居民 workerProdRatio 比例转换 L354）。若 sim 要镜像完整人口再生，需训练仓侧自治补（不归本批）。策划端登记口径同 15_账本 #7。
4. **（新增）_entities 注册链时序 bug**：UnitSpawnedEvent 发布早于 kingdomId 写入 → AI 实体以 kingdomId=0 时序态入册（IsPopulationEntity L139 `kingdomId>0 return false` 不拦）→ 玩家段曾把 AI Child 转 Resident（件2 守卫已堵本面）；Foundry 预置工人 raceId=0（json 旁证）同款既有面。根治需改发布时序，涉注册链改动，待裁决另行立批。
5. **（新增）45 日大世界 Load 卡死**：SaveManager.Load 卡 6min+（HH.73 P4 33 日局 Load 成功=临界点问题）；P1 三考读档链若涉大世界 Load 需留意。冒烟 P3 已改 Save-only 规避。
6. **（新增）观察器白名单笔误修正**：`[VagrantCamp]`（带右括号）StartsWith 精确串漏匹配 `[VagrantCampSystem]` 前缀 → 修为 `[VagrantCamp`。

## 四、红线自查

| 硬性条目 | 兑现 |
|---|---|
| Spawn 不入 foreach GetAllUnits 遍历体内 | 35 处调用点复核：**HH.78 新增 3 处全清**（L374 配对池快照收集/Spawn 在 L414 遍历外；L388 CountEligible 只读计数；L498 aiChildren 快照收集/SetOccupation 无增删）；件3 补员 Spawn 在 camp 遍历（BuildingRegistry 快照）内、GetAllUnits 只读遍历已关闭后（L127-133/L333/L531 只读）；存量 32 处=HH.76 已全查（病灶 2 修/只读 14/先收集 4），HH.78 未触碰。P5 运行时监听 45 日压场异常 0 命中佐证 |
| 玩家轨繁殖路径逐位不动 | OnNewDay/TickChildGrowth 玩家段逻辑零变更（AI 段守卫为纯防御 continue，玩家 Child kingdomId=0 路径逐位原样） |
| AI.Core/sim/champion/训练仓/RulerController 零触碰 | ✓（sim 侧仅只读排查列报③） |
| 参数全部 SO 化禁魔法数 | ✓ 5 笔入 KingdomConfig（racePool{0,1,2,3}=四族全池常量，随 6 模板池扩展再议） |
| 冒烟全绿才 commit | ALL PASS 6/6 双连庄后出报告；**commit 待验收代执**（HH.77 模式） |
| git diff 自查 | 本批=6 代码件（Observer/DayCycle/Happiness/KingdomConfig/Population/VagrantCamp +2/-1~+154）+新增容器（Valley_HH78_Smoke.cs+meta）；域外=策划端并行改动（3.1.2/3.1.3/美术规范 .md、图片资源/四族风格锚点/）**不裹入本批 commit** |

## 五、验收请求

1. 三件施工与探针验收（七轮证据链 §二）。
2. 列报六项（§三）登记口径确认——尤其 ④注册链时序 bug 是否另立批、⑤Load 卡死是否进 P1 三考风险清单。
3. 验收通过→commit 代执（6 代码件+容器+meta，排除域外文件）→**P1 三考解锁**。

---

## 策划裁决（策划端回写，裁决前保持空白）

> 策划端实盘复核（2026-09-05）：git stat 口径吻合（6 代码件+容器）+三处抽查实锤（PlaceVagrantCamps 幽灵引用 grep 验证[WorldManager 零实现仅注释提及]/件2 AI 段落盘[L342 OnNewDayPerKingdom+L514 日志在场——HH.42 第 6 笔补写实锤]/HappinessSystem 读口归属）。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 三件施工与探针验收 | **✅ 成立（D541）**——三件全实锤+七轮证据链（json 三方吻合：镜像日志×4+json occ=23 race=1×4+快照 k1=10=6 基线+4 直生闭环；流浪 occ=25×123 四族分布=刷点实锤）+P5 雷区 45 日压场 0 命中 | **HH.42 复发第 6 笔处置采信**：件2 未落盘（五轮 P2 恒 FAIL 根因）如实列报+grep 双锚点复验补救——「SearchReplace 回显 diff 骗过验证」为 HH.42 家族新变体（回显层有 diff≠磁盘层落盘），grep 双锚点复验升级为人口/注册类施工标准动作；P2 双证口径修正（镜像日志主证+workerCount 弱判定废弃）判定学正确 |
| 列报六项登记口径 | ①PlaceVagrantCamps 幽灵引用**认可**（OnNewGameMapReady 补建已施工；L6 幽灵注释更正归 P0 卫生包随手件）②参数落点 KingdomConfig**知悉**（5 笔占位，P0 调优回调）③sim 人口语义**登记 15_账本 #8**（有生育[TryBirth 同构]无成长[无 Child→Worker 段+Worker 来源=workerProdRatio 比例转换与 Unity 直生不同构]——策划端本串代登）④注册链时序 bug（UnitSpawnedEvent 早于 kingdomId 写入→AI 实体 kingdomId=0 时序态入册+Foundry raceId=0 同款）**挂账池立单**：件2 守卫已堵繁殖新面+两跑未爆=不阻塞 P1 三考；根治涉注册链发布时序，归独立小批（P1 后排）⑤45 日大世界 Load 卡死 6min+**进 P1 三考风险清单**（HH.73 33 日局成功=临界点；P1 三考 120 日+检查点回存读档链直接暴露——R2 预案常备+读档卡死超时即报告勿硬等；根因排查=独立诊断批挂账）⑥观察器笔误知悉 | 六项全部有主，零悬空 |
| commit 代执 | **✅ 代执**（6 代码件+HH78 容器+meta+HH 域+0.6 D541+队列；排除美术域 3.1.2/3.1.3/美术规范/图片资源） | 构成=§四 git 自查口径 |
| P1 三考解锁 | **✅ 解锁**——前置批（HH.76 零碎包+AI 人口再生批）全验收；P1 三考协议=HH.71 原样+新 seed+风险清单两项（⑤Load 卡死/R2 预案+④时序 bug 观察点） | 大考逐层显形：粮（一轮）→人口（二轮）→本轮人口再生批清偿后，下一候选暴露层=工 8 门后的扩张/军事行为层（2_22 P0 域已排） |

