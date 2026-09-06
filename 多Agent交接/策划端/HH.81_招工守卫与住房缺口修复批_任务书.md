# HH.81 任务书：⑥招工守卫+AI 住房缺口修复批（P1 四考前置）

> **编号勘正（2026-09-06 策划端）**：HH.82 已被策划端《建筑功能全量审计任务书》占用（先落盘者保留），本批施工完成报告**顺延 HH.83**。

> 类型：任务书（修复批·P1 四考前置）
> 状态：⏳待执行端接单
> 日期：2026-09-06 · 发起端：策划端 · 前序：HH.80 P1 三考报告（§策划裁决已回写，D542）
> 决策号：D542（0.6 §七十四）

## 一、施工三件（D542 裁决定稿）

### 件1：流浪生成链置 kingdomId=-1（⑥招工守卫语义正解）

- **三处 Spawn 点全置 -1**：VagrantCampSystem `SpawnVagrantAt`（L387）/`SpawnVagrantNear`（L358）/自然增长链（TryNaturalRespawn 内调用）——`SpawnUnit(Faction.PlayerCamp, Occupation.Vagrant, pos, -1)`。
- 语义依据：KingdomBrain L311 注释设计语义「未入籍=kingdomId<0」；D329 归属门面 -1=无主（Building 营地=-1 先例）。守卫改判定方案已裁否决（延续 Vagrant 挂玩家国 id=0 语义污染=注册链时序 bug 同族面）。
- **影响面排查列报**：kingdomId=-1 对既有守卫的兼容（IsPopulationEntity/_entities 桶/AIEconomySettlement/CountAliveByKingdom/野性敌意扫描/招募限同族 D469——预期各守卫本就排除非正数归属=零回归方向，逐处 grep 确认列报）。
- **静默双坑顺手修**：KingdomBrain `FindRecruitableVagrant` 返回 null 时一次诊断日志（ExecuteRecruitWorker L193-194 静默 return 处——含流浪池活体数+守卫拒绝计数，P0 调优观测口）。
- 观察器白名单若需补 tag 随批（Editor-only）。

### 件2：六模板预置插 House（AI 住房快解）

- 六族 KingdomDef（DenseForest/Bedrock/IronHoof/SnowRock/GoldenWheat/RiverBay？——**RiverBay 玩家模板不动**（同 HH.73 口径，玩家住房自建），实际五 AI 模板：DenseForest/Bedrock/IronHoof/SnowRock/GoldenWheat）：baseBuildingDefIds 在 **castle 后第二位**插 `House`（大写实值先查），目标序=`castle, House, farm, Well, mine, Warehouse, quarry`（House 缺位的 SnowRock/GoldenWheat 为 `castle, House, farm, mine, Warehouse, quarry`）。
- `KingdomFoundingConfig` buildingCount **4/5/6→5/6/7**（帐篷/村落/要塞；帐篷新取 castle,House,farm,Well——粮水房三要素齐全）。
- 注意资产 id 实值先查（HH.73 笔误教训：House vs house 大小写以 Building_House.asset 为准）。

### 件3：正解登记件（零代码）

- AI 建造评分人口压力驱动自建 House=**2_23 批B 域**（策划端已登记，本批零施工）；
- 野怪杀 Worker 损耗面=数值观察项（挂账池已立行，本批零施工）。

## 二、冒烟容器（HH.81 专测）

- P1 结构：六模板 House 在场+buildingCount 新档位取序正确（含 Well）；
- P2 行为正：AI 立国→生育分支触发（房容>0 过门槛）→AI Child 诞生（归属国 raceId 正确）→⑥招工落地>0（流浪 kingdomId=-1 被招募→Worker 入籍）；
- P3 行为负：玩家侧零回归（玩家招募链/繁殖链/人口计数逐位——流浪 -1 后玩家 CountAliveByKingdom(0) 等统计面不回归）；
- P4 存档回读：流浪/新 Worker 归属数据保持。

## 三、红线与纪律

1. Spawn 不入 GetAllUnits 遍历体（雷区纪律硬性条目）；玩家轨逐位不动；AI.Core/sim/champion/训练仓/RulerController 零触碰。
2. 冒烟全绿才 commit（HH.53）；git diff HEAD 自查（HH.42——grep 双锚点复验=注册类施工标准动作）。
3. 静默失败面排查件1 必列报（同模式病灶一次清精神）。

## 四、交付物

1. 三件施工 diff+冒烟容器+探针证据。
2. HH.82 完成报告（含件1 影响面排查列报+PlaceVagrantCamps 式幽灵引用顺手查）。
3. **验收通过→P1 四考解锁**。

## 五、策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 三件施工与冒烟验收 | | |
| 件1 影响面排查结论 | | |
| P1 四考解锁确认 | | |
