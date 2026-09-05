# HH.78 任务书：AI 人口再生批施工（混合双通道 D539——Gate 五面已裁决）

> 类型：任务书（施工批）
> 状态：⏳待执行端接单
> 日期：2026-09-05 · 发起端：策划端 · Gate 依据=执行端/AI人口再生批_Gate五面实施要点报告.md（§策划裁决已回写，五面全裁）
> 决策号：D540（0.6 §七十二）· 前置：HH.76 零碎包✅（HH.77/D540）

## 一、施工三件（Gate 裁决定稿，照单施工）

### 件1：AI 生育分支（Gate 面②）

- PopulationSystem 扩 `OnNewDayPerKingdom(KingdomState k)`（AI 轨新增，玩家轨 OnNewDay 主流程**逐位不动**）：
  - 条件输入 per-kingdom：幸福=HappinessSystem per-kingdom 桶（GetTaxCoefficient(kId) 同源读口）、饱食=SatietySystem.GetAverageSatiety(k.id)（已有）、房屋容量=该国 House 容量按国统计；
  - 配对池=遍历 UnitRegistry 按 kingdomId 过滤+固定遍历序（确定性），**Worker+Porter+Resident 均可配对**；
  - 生成：`SpawnUnit(Faction.PlayerCamp, Child, birthPos, k.id)`+`raceId=KingdomRace.GetKingdomRace(k.id)`（现网组合先例）；出生落点=该国 House 旁（无房=条件不满足自然不生育）；
  - Child 日常耗粮走 Satiety per-kingdom 国库路由（D453 已通，零新增）；
  - 参数 SO 化：AI 生育冷却=**10 日**、阈值复用玩家同表（幸福 60/饱食 50）——落 KingdomConfig 或新 SO（执行端按 so-data-driven 载体规范定，列报），终值 P0 调优批回调。

### 件2：AI Child 成长→Worker 直生（Gate 面③）

- TickChildGrowth per-kingdom 扩（UnitRegistry 按 kingdomId+Child 过滤遍历，固定序）：AI Child 成长天数满→`SetOccupation(Occupation.Worker)`（AI 无 Resident 体系，直生=绕 ⑥ 新生产力）；
- 成长期耗粮既有路由零新增；**SetOccupation 换职业无 Spawn 雷区**（既有机制）。

### 件3：流浪侧复通（Gate 面④ 方案 A，四子件）

1. **营地实体复通**：先定位 WorldManager.PlaceVagrantCamps 调用点断链处（L6 注释声称地图生成建营地，P1 两跑行为级证明 FindCamps 永空）——恢复调用或 OnNewGameMapReady 补建，**二选一列报**（FindCamps 复通=每日补员链自然激活）；
2. **自然增长刷点**：定期（SO 参数）在无主地（TerritorySystem 无主判定）按组 Spawn 流浪——**避开 AI 领土**（防野人入籍冲突，维持 D469 按族成群纪律）；
3. **族别映射回填**：OnNewGameMapReady anchorRace/groupRace 硬编码 `GetKingdomRace(0)`→按地图族分布/出生锚映射（Q10-M2 挂账同口清偿，含初始流民与刷点流浪两处）；
4. 参数 SO 化：vagrantRespawnIntervalDays=**5 日**/respawnGroupSize=**2**（初始占位，终值 P0 调优）。

## 二、冒烟容器（人口再生专测）

立国→fertility 条件满足→AI Child 诞生（归属国 raceId 正确）→成长→Worker 计数上升→存档回读（人口计数保持）；流浪侧：营地实体在场+补员激活+按族投放验证（非人类国可招）。

## 三、红线与纪律

1. **Spawn 不入 `foreach GetAllUnits` 遍历体内**（HH.76 件2 雷区纪律=硬性条目，施工后 20 处清单复核一次）。
2. 玩家轨繁殖路径逐位不动（零回归）；AI.Core/sim/champion/训练仓/RulerController 零触碰。
3. 参数全部 SO 化（so-data-driven），禁硬编码魔法数。
4. sim 义务：施工后列报人口再生语义 sim 侧有无（SimEconomy 人口段），策划端登记口径同 15_账本 #7。
5. 冒烟全绿才 commit（HH.53）；git diff HEAD 自查为交付前置（HH.42）。

## 四、交付物

1. 三件施工 diff+冒烟容器+探针证据。
2. HH.79 完成报告（含 PlaceVagrantCamps 断链定位结论/参数落点列报/sim 义务列报）。
3. **验收通过→P1 三考解锁**（队列行联动）。

## 五、策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 三件施工与冒烟验收 | | |
| PlaceVagrantCamps 断链定位结论 | | |
| 参数落点与 sim 义务列报 | | |
| P1 三考解锁确认 | | |
