# HH.28 P1 步骤10 路线① Faction 收编 sim-sync 报备（待策划裁）

> 类型：待决策（sim-sync 报备先行）
> 状态：✅已裁决·收编验收成立（HH.29 决策②确认；四问全裁 HH.28 §五 + 决策①人口底线判修见 HH.29 §七）
> 日期：2026-08-27 · 发起端：执行端 · 关联：HH.27（2_17 P0 收官成立）/ 2_17_AI王国脑与自主成长（D331/D338/D339）+ 实施计划步骤10 / sim-sync skill / 15_差距账本
> 前置：2_17 P0 收官成立（HH.27 终版）→ P1 步骤10 开工；路线① Faction 收编 sim-sync 报备先行

---

## 〇、一句话

**P1 步骤10 路线①报备：拟新增 `Faction.AiKingdom` 阵营值（不复用 Orc/Dwarf/Elf 预留槽，理由见 §二）；已查证 harness 侧唯一相关点=双份镜像 `Faction.cs`（两处逐字节一致）+ sim 消费全为现有枚举值（SimConfig/SimDamage/DayNightTransition 都只引用 None/Human_Player/Undead），**新增值不触任何既有引用、sim 侧零代码改动、无双端行为差账**；训练仓镜像只需同 commit 同步双份 `Faction.cs`。迁移清单=实施计划步骤4 分类表①+②（9 行 kingdomId==0 守卫逐行折 faction 判定），MonsterAI L192 收窄放开暂保守保留待裁。请策划裁：①新增 Orc/Dwarf/Elf 之外的 `AiKingdom` vs 复用预留槽；②9 行守卫迁移序与 L192 放开是否延后。**

---

## 一、背景

2_17 P0 收官后，AI 王国已有战士兵力（步骤10 ⑦招战士）。实施计划步骤10 为 Faction 完整收编点（此前步骤4/5 曾重钉到步骤10）。P0 期间 AI 单位用 **kingdomId>0 + Human_Player 阵营的"冒充态"** 过渡（四族预留槽 Orc/Dwarf/Elf 语义不符未用）。现需正式给 AI 王国一个**阵营态**，把 9 行 `kingdomId==0` 守卫迁移为 faction 判定 + kingdomId 路由双层。

**北极星红线（sim-sync）触发**：改 `AI.Core/Ports/Faction.cs` 属于双份拷贝区，动前必须先报、同步训练仓镜像、登记 15_差距账本。本文即报备。

---

## 二、报备四件

### ① 枚举追加项：新增 `Faction.AiKingdom`（不复用预留槽）

**建议**：在 `Undead` 后、预留区前插入 `AiKingdom`（或置于预留区上方），语义="AI 控制的王国单位（人类种族，非玩家阵营）"。

**为何不复用预留槽 Orc/Dwarf/Elf**（已查证）：
- 预留槽语义错位：三者均 `*_Player` 后缀，注释明言"玩家种族阵营（未来）"。AI 王国是 **AI 而非玩家**，复用=误导未来种族设计。
- 二选一的对照：**A=新增 AiKingdom**（语义正、与现有玩家/亡灵并立，"AI 王国=玩家对立营"敌我关系可复现现有 Human_Player↔Undead 对立模式）；**B=复用 Undead**（Undead=亡灵已语死亡灵语义，AI 王国是人类冒充亡灵，污染 Undead 判定面如 GridSystem~332 怪敌可视化）。
- **推荐 A**：大幅改动不可逆，语义清晰、敌我判定可与现有族复用同一套。

**影响面（不扩散）**：枚举加值不改任何既有枚举值的语义/序（C# 枚举社交性安全）；双份镜像同步即可。

### ② 训练仓镜像同步方案：sim 侧零代码改动，仅同步双份 `Faction.cs`

**已查证（实盘证据）**：
- 双份拷贝：`Valley Rampart/Assets/_Game/Systems/AI.Core/Ports/Faction.cs` 与 `ai决策大脑强化训练/harness/Core/Ports/Faction.cs` **逐字节一致**（已逐行核对）。
- sim 消费点：SimConfig（职业初始栈 faction）、SimDamage（友方过滤/承伤统计 Faction.Human_Player）、DayNightTransition（跳过失落职业）——**全部只引用现有枚举值 None/Human_Player/Undead**；全仓 grep 对 Orc/Dwarf/Elf/AiKingdom 零引用。

**结论**：新增 `AiKingdom` 后，sim 侧**无任何编译/行为消费**，只需把双份镜像 `Faction.cs` 同步为同一内容（同一 commit 内加值即可）。**无双端行为差账** → 15_差距账本登记"无 sim 影响"账（结论而非负债）。

**双门禁**：执行端不动训练仓 harness/（禁改领域第 4 条），仅同步双份 `Faction.cs` 属**共享决策核双端外眉心**——按纪律仍走：commit 空档 → 改双份 → Unity 编译 0 错 + 冒烟 →（sim 侧 sign 为纯枚举加值，不经手 benchmark 但会注明）。训练仓镜像由执行端同 commit 一并提交 `harness/Core/Ports/Faction.cs`。

### ③ 15_差距账本登记项草案

在 `15_训练侧harness与Unity端差距文档.md` 增登记（§一·补 或 新 §一·三 节）：

| 改动 | 位置 | 影响 | Unity 侧现状 |
|------|------|------|--------------|
| Faction 枚举新增 AiKingdom（AI 王国阵营） | `AI.Core/Ports/Faction.cs`（双端镜像）+ `harness/Core/Ports/Faction.cs` | sim 消费全为现有值，新增值零引用 → **无 sim 行为影响账**（结论非负债）| Unity 侧步骤10 折 9 行守卫为 faction+kingdomId 双层；sim 侧不消费 AiKingdom（AI 王国战斗面步骤10 后仍走现有 Human_Player 敌我判定）|

> 注：AI 王国的战斗敌我判定在 P0 由 kingdomId 双条件承担；收编后仍**先保守保留 Human_Player 视为玩家/ AI 覆盖**，避免 sim 侧未接 AiKingdom 造成行为差。具体放开点见②话题④。

### ④ 迁移清单执行：步骤4 分类表①+②（9 行 kingdomId==0 守卫）逐行折

按实施计划步骤4 分类表（一表两用=步骤10 迁移清单）：

- **① 已守卫（退役为迁移）**：
  - `MonsterController.FindNearestHuman`（patch C, `kingdomId==0`）
  - `SelectionController` L152/L182
  - `PopulationSystem.IsPopulationEntity`（step3）
  - `ThroneAnchor.HasRemainingWorker + AliveWorkerCount`（step4）
- **② 裸奔→本次已补守卫（同模式 `kingdomId==0`）**：
  - `HappinessSystem.OnNewDay`（AI 工人稀释玩家幸福）
  - `SatietySystem.GetAverageSatiety + OnNewDay`（AI 工人吃玩家国库粮/拉低均饱食）
  - `TrainingSystem` 转职池构建 + GetTrainableCount（AI 工人入玩家转职池/可训练数）

**迁移目标形态**：`faction 判定 + kingdomId 路由` 双层——即"本 `Human_Player` 阵营且 kingdomId==0 才算玩家"收编为"阵营判定圈定人类 + kingdomId 路由分玩家/AI 王国"。③桶语义天然安全类不迁移。

**MonsterAI L192（"AI 防御就绪后放开" P0 承诺）**：建议**保守保留**（收窄放开解除需 AI 有战士且 sim 敌我判定就位，步骤10 刚有战士、sim 未接 AiKingdom，此刻放开=行为差）。→ **话题④：L192 放开本次不动，留待 AI 王国战斗面稳定后另报裁。**

---

## 三、请策划终裁（四项）

1. **枚举方案**：采纳新增 `Faction.AiKingdom`（✅推荐）？还是复用预留槽 Orc/Dwarf/Elf？还是复用 Undead？
2. **sim 影响**：确认"新增枚举值不触 sim 任何消费 → 记无 sim 行为影响账（结论）+ 双份镜像同 commit 同步"口径？
3. **迁移清单**：确认 9 行守卫（①+②）一次性折 faction+kingdomId 双层、③类不动、逐行核退役？
4. **L192 放开**：本次**保守保留不动**（推荐，待 AI 王国战斗面稳定另裁）？还是本次一并放开？

## 四、下一步

策划揽准后：按裁 **①Faction 收编代码 + 双份镜像 + 15账本登记 + 9 行守卫迁移**（独立一步，冒烟：AI 王国不再冒充玩家、玩家零回归、既有守卫面守恒）→ 批准后动工 **②路线② 效用补全 15 项**（⑦⑧⑨⑫落地+⑪⑮占位桩，验收带冒烟 #5 兵力目标随威胁上调）。

---

## 五、四问全裁 + P0 干净复跑收口（2026-08-28）

### 5.1 Faction 收编验收：成立（无回归）

策划终判 **「② Faction 收编验收成立（无回归）」**。证据链：
- **前绿项全绿**：A3 确定性逐字节=OK、A4 玩家零回归=OK、B2 供水=OK、RD2-①②=OK（干净环境复跑，非 MCP 污染——三轮 post-init `b=2684/2684/2684`、`u=22/22/22` 一致）。
- **通道零 faction 依赖已核**：`ConvertVagrantsToWorkers`（KingdomFoundry L349）与 `FindRecruitableVagrant`（KingdomBrain）只判 occupation/kingdomId/recruited，**无 faction 引用**。
- **执行链完好佐证**：玩家侧 `RecruitVagrant=True`（B1 pTrue）+ 建造侧 `K1 build45>`（B5 build 通）全通。
- **bfd8f349 兜底确认为真抓虫**：`EffectiveFaction` 派生兜底（Data.faction==Human_Player && kingdomId>0 → AiKingdom）修读档回环阵营漂移，纯轮行为不变。

### 5.2 源码态澄清（策划裁决②质询应答）

**「复跑编译时 workdir 路线② 未提交改动在不在？」——在。**

刚才 P0 干净复跑的编译态 = **commit(bfd8f349 Faction收编 + 46c31e6/afd29d5 镜像) + workdir（路线② ExecuteRecruitWarrior / Valley2_17_Smoke_5 / UtilityActionConfig 补 def / UtilityScorer D348 / KingdomBrainConfig 系数）混合态**。即「无回归」证据覆盖的是 commit+workdir 混合源码，非纯 commit。因 A3/A4 逐字节探明了决策核行为不变，几何验证仍成立；**但此后一切回归复跑均须先声明源码态**（本报告即首次声明）。路线② 将与其 Faction 分笔 commit，届时混合态自动消解。

### 5.3 策划裁决①（让渡确认）落点——探针两行已补

- **探针①（D308 自然流民数）**：`VagrantCampSystem.OnNewGameMapReady` 现有日志 `D308 初始流民预置: {spawned}/{total}`（L102）已隐式覆盖，无需重复行。
- **探针②（B5 trainTry vs trainOk 分层）**：已补进 `Valley2_17_Smoke_P0`——RoundData 增 `k1TrainTry/k2TrainTry`，DispatchStat 四元组读 `trainTry`（B5 黄旗行输出分层：`try=0→焦点从未选⑥→评分问题` vs `try>0&ok=0→无候选→环境让渡坐实`）。
- **让渡归档状态**：探针已埋；**最终 trainTry 数值需手工 Play 干净 pump 终判**（MCP 长 pump 结算段拿不到终判字）。归因方向已由策划确认「train0=pump 环境让渡方向对」；EP 若显示 D308 真产流民却 trainTry=0 → 回填策划判真问题。

### 5.4 策划裁决③④落点（P0 断言口径改造 + B4 归因）

**③ 断言口径对齐已裁决验收形态**（同批已改 `Valley2_17_Smoke_P0.cs`）：
- 绿项硬断言不变：A3/A4/B2/RD2-①②；B1 玩家招募、B5 建造 >0 保持硬断。
- 黄项（B1 AI 侧/B4 reachedExpand/B5 train）改**证据记录 + 黄旗标注 + 断言换轮间一致性**（两轮同为 train0/SDDDD = 确定性证据，非 FAIL）。
- B3 输出「黄旗挂 2_11（独立卡）」**不计 FAIL**（HH.27 §二.3 独立卡定义）。
- verdict 稳定反映裁决态。

**④ B4 归因措辞更正**：由旧「无真经济」改为 **「工人不足」**——抽象结算已供资源 + build45 佐证非缺资源，卡点=`workerCount≥8` 演化需求不可达（初始 4 + 招工 0）。黄旗归属不变（招工→成长链），归因文本已按 HH.27 黄旗 2 追更新。

### 5.5 收口执行序对照
| 策划裁决 | 状态 |
|---------|------|
| ①探针两行补（D308 现成 + trainTry 分层已埋） | ✅ 已落，终判待手工 pump |
| ②源码态澄清一句 | ✅ 见 5.2 |
| ③断言口径改造 | ✅ 已改（黄旗+轮间一致+B3 挂卡） |
| ④B4 归因措辞 | ✅ 已改（工人不足） |
| Faction 收编收口独立 commit（15账本+HH.28 回写+训练仓镜像） | ⏳ 本报告回写即此；镜像已同步（afd29d5）；15账本登记进行中 |
| 路线②单独 commit（与 Faction 分笔） | ⏳ 待下发 |