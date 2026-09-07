# HH.92 · T16/T17 门面能力核对表（D549 件D-11/12）

> 日期：2026-09-07 · 产端：执行端
> 底册：`报告/设计审查/全模块覆盖审计_P1文档推导层_2026-09-06.md`（玩家入口 18 项）+ `Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs`（行动池 21 条实读，HH.86 后）。
> T16 格式（清单 §3.4）：行动 id | 触发方式 | 执行证据 | 断言/观测。证据分级：🟢=本批 THD 容器自然评分触发日志在案 / 🔵=执行链结构核（KingdomBrain.ExecuteFocus switch file:line）/ 🟡=占位桩（2_18 域）。
> **差集=从未被评分自然选中的行动（🟡+🔵-未触发者）本身就是 P0 调优批的评分参数输入。**

## 一、T16 行动池 21 条

| # | 行动 id（名称） | 执行通道（结构锚点） | 触发方式 | 证据等级 |
|---|---|---|---|---|
| 1 | BuildHouse 建住宅 | ExecuteBuildFocus→BuildController.TryBuild（KingdomBrain.cs L155-168） | 评分自然触发（HouseGap） | 🔵+THD 窗内观察 |
| 2 | BuildWarehouse 建仓库 | 同上（buildTargetCap=4） | 评分自然触发 | 🔵 |
| 3 | BuildCapacity 建产能(quarry) | 同上 | 评分自然触发 | 🔵 |
| 4 | BoostHarvest 强化采集(farm) | 同上 | 评分自然触发 | 🔵 |
| 5 | Grain 屯粮(Granary) | 同上（Develop 起） | 评分自然触发 | 🔵 |
| 6 | RecruitWorker 招工人 | ExecuteRecruitWorker（L194，流浪汉→Worker） | 评分自然触发 | 🔵（考跑无噪声=流浪池恒 0，自然触发需开噪声窗） |
| 7 | RecruitWarrior 招战士 | ExecuteRecruitWarrior（直转 Warrior） | 评分自然触发 | 🔵 |
| 8 | Tech 科技升级 | ExecuteTech（L173） | 评分自然触发（执行链未核=审计 #2 同口径） | 🔵 |
| 9 | BuildWall 修工事城墙 | ExecuteBuildFocus（Expand 起） | 评分自然触发 | 🔵 |
| 10 | Expand 推边界 | ExecuteExpand（L176） | 评分自然触发 | 🔵 |
| 11 | Expedition 组建出征军 | 占位桩（L179-184：仅置位无实体动作，2_18 接口） | 评分可选中·执行=桩 | 🟡 |
| 12 | Reinforce 边境增援 | 同上占位 | 同上 | 🟡 |
| 13 | Diplomacy 外交姿态 | 同上占位 | 同上 | 🟡 |
| 14 | Rebuild 重建 | 姿态无实体指令（L185-189） | 评分自然触发（姿态） | 🔵 |
| 15 | Defense 防御姿态 | 同上 | 同上 | 🔵 |
| 16 | BuildWell 建水井 | ExecuteBuildFocus（Survive 起） | 评分自然触发 | 🔵 |
| 17 | BuildBlacksmith 建铁匠铺 | 同上（Develop 起） | 评分自然触发 | 🔵 |
| 18 | BuildWarAcademy 建战争学院 | 同上（Military 起，ExclusiveGap） | 评分自然触发 | 🔵 |
| 19 | BuildWarCamp 建兽人战营 | 同上 | 评分自然触发 | 🔵 |
| 20 | BuildLeyForge 建地脉熔炉 | 同上（Expand 起） | 评分自然触发 | 🔵 |
| 21 | BuildArcheryRange 建精灵射箭场 | 同上 | 评分自然触发 | 🔵 |

**MVP 口径列报**：①21 条全部有结构核锚点（ExecuteFocus switch 全覆盖，无「评了没人执行」的悬空行动）；②THD 三轮观察窗内自然触发到的行动以 `[THD][CSV]`+`[KingdomBrain]` 日志为行为级正证据，未自然触发者（差集）标注于 HH.93 报告——评分参数差集归 P0 调优批，勿在本批修 AI；③逐条强制触发冒烟（直接置 focus+调 ExecuteFocus 的 Editor 探针）列为 P0 调优批扩展件（本批不阻塞 MVP）。

## 二、T17 玩家入口 18 项分类核对（考跑环境口径）

> 分类：**A=逻辑入口（API 可测，考跑可核）** / **B=UI 入口（考跑无 UI=列报不测，正式 UI 回归归四容器+人工）** / **C=战略等价（AI 决策通道替代，非缺口）**。

| # | 入口 | 分类 | 考跑环境处置 |
|---|---|---|---|
| 1 | 建造 | A（TryBuild 已 kingdomId 参数化） | AI 侧行动池 11 条建造行动=同门面核（T16）；玩家 UI 链=B |
| 2 | 升级 | A（TryUpgrade/TryUpgradeCastle） | AI 执行链未核（审计同口径）→列报 2_23 域；玩家 UI=B |
| 3 | 拆除 | A（Building.Demolish） | AI 无行动=缺口（审计在案）；考跑不测=列报 |
| 4 | 修复 | A（修复链） | 重建≠修复语义待裁决（审计 #4）→列报 |
| 5 | 训练 | A（TrainingSystem.TryTrain(kingdomId)） | AI 消费缺口在 2_22 P0；玩家 UI=B |
| 6 | 招募流浪汉 | A（RecruitVagrant） | AI 侧⑥=同族核；考跑噪声关闭时流浪恒 0（T11 口径） |
| 7 | 移动 | C | UX 专属不测 |
| 8 | 跟随 | B | 2_21B 未实施域，列报 |
| 9 | 优先采集 | C | 战略等价 |
| 10 | 守卫部署 | A（DeployGuard(kingdomId)） | AI 驱动入口=2_22 P0 需求③域；列报 |
| 11 | 巡逻 | B | 玩家入口未接线（审计实锤）+AI 侧 2_22 P0；列报 |
| 12 | 编队 | B | FormationPanel 待 P-2 定性；列报 |
| 13 | 贸易 | A（BuyWithGold/SellToGold） | AI 无行动=缺口（2_23/2_18 域）；列报 |
| 14 | 牧场·买幼崽 | A（BuyCub(kingdomId)） | 双缺（审计 #1）；列报 |
| 15 | 牧场·宰杀 | A（Slaughter/SlaughterAt） | 双缺；列报 |
| 16 | 采集确认 | C | 战略等价 |
| 17 | 外交 | B | 2_18 双占位；列报 |
| 18 | 出征/增援 | C | 玩家=手动指挥 UX 专属；AI=⑪⑫行动（占位） |

**结论**：18 项中 A 类 10（API 对称面已 kingdomId 参数化=测试环境可驱动）、B 类 4（考跑无 UI，列报不测）、C 类 4（战略等价）；A 类中 4 项（#2/3/4/13）AI 消费端缺口+2 项双缺（#14/15）均在审计在案，归属已列（2_22 P0/2_23/2_18/待裁决）——**测试环境侧本批不新增缺口施工**（防批膨胀，缺口修复归各归属批）。
