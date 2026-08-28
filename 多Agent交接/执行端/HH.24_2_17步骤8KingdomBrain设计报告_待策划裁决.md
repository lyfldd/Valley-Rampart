# HH.24 2_17 步骤8 KingdomBrain 设计报告（Gate：先报设计再动代码，待策划裁决）

> 类型：策划报告请求（步骤8 Gate 设计报告）
> 状态：✅已裁决（2026-08-26 策划端：A 准 + ④债归步骤12 准 + 六点全放行，附两条增补）
> 日期：2026-08-26 · 发起端：执行端 · 关联清单/文档：2_17_AI王国脑与自主成长.md（D317~D350）/ 2_17_实施计划.md §步骤8-9 / 2_15 / 2_19(D279) / 15_训练侧差距文档.md
> 前置：HH.23 已裁决（2b 收口成立 + 步骤7 放行）；步骤7 已落地（建造+招募 kingdomId 门面，五项冒烟全绿，69a0035/77569ef/c9f39b5）

---

## 〇、报告定位

策划端对步骤8（P0 最大架构步：**新系统 + 日 tick 权威序**）放 Gate **「先报设计再动代码」**。本报告必答裁决六点，全部给出执行端推荐方案 + 依据（贴代码事实），其中唯一真·决策点是 **②Brain.Tick 与日结入账先后序（选项A/B）**，请策划拍板；其余五点以确认+落地声明为主。

**步骤8 收口判据**：D347 五步权威序在 `DayCycleSettlement` 落地 + 剧本状态机（StatsM 推进）+ 常设底线/焦点/打断框架 + Brain 生命周期 + 玩家无 Brain；**不含**效用评分器（归步骤9）、SimMode 真实切换（P0 恒 Fine）、领土推进（归步骤12）。

---

## 一、D347 五步接线确认（含 Brain 位置决策 ⚠️需拍板）

### 1.1 落地形态：重构 `DayCycleSettlement.OnDayChanged` 为五步段

现状（[DayCycleSettlement.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/DayCycleSettlement.cs)）：`OnDayChanged` 现为线性序列——饱食(1)→幸福(2)→税收(3)→人口(4)→贸易冷却(5)→**AI段日结转账(6)**→牧场(7)→营地补员(8)→CampUpgrader(9)→DaySettledEvent。**无显式五步结构**。

Step8 重构为 D347 五步（对齐设计稿 §3.1.4 权威序）：

```
步骤0  TimeDayChangedEvent（日 tick 入口）
步骤1  SimMode 判定（SimModeManager.P0FixFine：恒 Fine 占位，SimModeConfig）
步骤2  王国脑/抽象结算：循环非玩家王国 → KingdomBrain.Tick（本步骨架）
步骤3  领土变更（P0 占位空跑，预留 TerritoryChangedEvent 插槽；推边界/建造纳土归12）
步骤4  CampUpgrader.TickAll（2_16 已有，作用序归位到③事件之后）
步骤5  其余日结算 = 现行 1~9 尾巴（饱食/幸福/税收/人口/贸易冷却/**AI段日结转账 AIEconomySettlement**/牧场/营地补员）+ DaySettledEvent
```

### 1.2 ⚠️ 决策点：Brain.Tick 在日结入账之前还是之后？

| 选项 | 语义 | 理由 |
|------|------|------|
| **A（推荐）Brain 在前**：步骤②跑，先于步骤⑤的 `AIEconomySettlement.Tick()` | 脑看到**昨日结算后的国库余额**（=day N 政令前已实现的 day N-1 结存） | 契合 15_账本登记 |
| B：Brain 搬到日结转账之后 | 脑看到**刚入账的今日余额**（当日储仓产出当日可花） | — |

**推荐 A（植入②，在日结入账之前）**。依据：

1. **精确契合 15_账本「一·补二」滞后语义**（[15_训练侧差距文档.md](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/15_训练侧harness与Unity端差距文档.md) L47-56）：sim 侧瞬时入账、Unity 日结两段式（储仓攒产品→日结搬运）**天然 1 日滞后**；L56 明确警告"若王国脑以国库余额做预算/造价决策，此滞后会造成周期末偏差"。**A=把这个滞后显式固化为脑的预算口径**（脑永远花"昨日已结存"，不含在途储仓），不引入第二段漂移。
2. **保守**：不透支未落国库的储仓产出，防超支/预算抖动；与"国策看昨日结存定今日计划"的经营直觉一致，也贴近玩家"日末结算"心智。
3. **确定性不受影响**：只要次序固定 + 同 seed，两轮逐字节一致均可保证；A 只是把口径定稳。
4. **步骤9 勾兑顺滑**：效用评分器/造价断言全部统一读 KingdomState.resources=「昨日余额」口径；粮储常设底线（<2日消耗）也同口径，阈值自洽。
5. **副作用可解**：若实测"1 日滞后致决策过钝"，解在 sim 侧镜像 storage 中介或缩短 Unity 结算粒度（15_账本已列待办），**不在步骤8 改 Brain 次序**。

> 影响：若策划选 B（脑见当日余额），需同步改 15_账本「1 日滞后」登记语义 + 步骤9 口径，且脑可花当日在途产出→预算超前；性价比低于 A，故执行端明确推荐 A。

---

## 二、Brain 生命周期（确认性落地）

- **创建钩子两处**（D337），统一经 `KingdomBrainFactory.Create(kingdomId)` → `KingdomBrainRegistry.Register`：
  - ①`KingdomFoundry.FoundFirstGeneration`（第一代，[WorldManager.GenerateMap](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomFoundry.cs) 末尾）——每 AI 王国一个；
  - ②`KingdomFoundry.FoundFromCamp`（动态立国，营地转正）——为该新王国建。
- **灭亡销毁**：D279 本体归 2_19；本步只在 `KingdomBrainRegistry` 留 `Unregister(kingdomId)`（OnDestroy 成对退订 EventBus，D340）供 2_19 灭亡管线调用。**P0 无灭国路径→钩子空转安全**；起钩子在 2_19 时接入。
- **玩家 id=0 无 Brain**（D338）：工厂/Registry 双短路 `IsPlayer`，Registry 永远不含 id=0；`KingdomState.scriptPhase` 对玩家=null。
- 日 tick 驱动方=DayCycleSettlement 步骤②（非自挂 Update），与 D337 一致。

> 补充：KingdomState 现字段仅 id/name/personality[5]/IsPlayer/resources（已核实），无剧本阶段/焦点/SimMode——本步加 `ScriptStage? scriptPhase`（AI 用，玩家 null）+ `int focus`（候选行动 id）+ `SimMode simMode`。

---

## 三、剧本状态机（确认性落地：阈值全 SO）

- `ScriptStageMachine` 四阶段：存活→发育→扩张→军事，**单向不回退**（D318）、统一存活起步（D319）、**每日最多升一级**（D319）。
- 阈值表 + 最小停留 **全部落 `KingdomBrainConfig` SO**（`Data/KingdomBrainConfig.cs` + asset `Resources/Config/Kingdoms/KingdomBrainConfig.asset`，so-data-driven 无魔法数；对齐实施计划 §三）：

| 字段 | 占位默认 | 依据 |
|------|------|------|
| surviveMinDays / developMinDays / expandMinDays | 2 / 3 / 3 | D317 最小停留 |
| survive→develop 阈值（人均有房/粮储≥3日/无失业，连续2日） | true / 3 / 0 | D317 |
| develop→expand（工人≥8/产能≥3/连续3日净流入正） | 8 / 3 / 3 | D317 |
| expand→military（战士≥4/人口≥12/扩张占区≥2中区块） | 4 / 12 / 2 | D317/D349 |
| maxStageUpPerDay | 1 | D319 |
| focusMinDurationDays | 3 | D322 防抖 |
| grainReserveDaysFloor | 2 | D322 常设底线 |

- **P0 阶段自然封顶在扩张期**：推进阈值依赖战士/扩张占区，而 P0 行动子集 ①~⑥+⑬⑭ **无⑦招战士/⑩推边界**（D345）→ 真实对局不可能产出战士/占区→扩张→军事不触发，P0 停扩张期=预期且不"停滞"（每日本国日 tick 有动作）。这是设计一致，非 bug。冒烟注入状态**单独验证机器层四段推进**（与 P0 产出约束解耦）。

---

## 四、常设底线 + 焦点 + 事件打断框架（确认性落地）

- **焦点模型**（`FocusController`，D322）：每日 1 个国策焦点，无固定时长，被替换才结束；**切换防抖 ≥3 日**（focusMinDurationDays）。步骤8 无评分器→提供基线焦点 + 常设底线覆盖；步骤9 的 `UtilityScorer` 打分后经此闸输出。
- **常设底线（触发式，最高优先，不评分）**：粮储 <2 日消耗 → 强制"屯粮/采集"；本土被攻击 → 强制"防御姿态"。无视效用排序（D322）。
- **被攻击信号源挂伤害管线层（非怪物目标选择层）**：在 `DamageSystem`（[Systems/Combat/DamageSystem.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Combat/DamageSystem.cs)，伤害管线中枢）**命中结算应用处**，对"该王国(kingdomId>0)实体确实受击"发布 `KingdomAttackedEvent(kingdomId)`；FocusController 订阅→该王国次日强制防御姿态。**明确不挂 MonsterAI/WaveDirector 目标选择层**（那是意图非事实——被判定为袭击目标但未命中/被牵引会误报；只有伤害真实落地才算被攻击）。职责边界：被攻击=已发生的受击事实（伤害管线层），非仇恨/选目标意图。
- **事件打断框架（D340）**：EventBus 订阅宣战/灾害/主城被围→立即重规划（不等次日）；未落地源（2_18/2_14）接口占位、实现延后；`KingdomBrain.OnDestroy` 成对退订（配 D337 生命周文销毁）。

---

## 五、④债时点声明（明确裁决项）

**声明：领土不随步骤8 入档，④债归步骤12，本步骤8 不落地 foundKingdoms 门控。**

- 依据：步骤8 的步骤③领土变更是**占位空跑**（P0 无推边界/玩家纳土，TerritorySystem 账本零写入）；读档目前领土从建筑重推=正确（P0 领土=初始圈入，无扩张），`RebuildInitial foundKingdoms` 门控的意义（读档不重推、改走存档恢复）只有在"存档已含领土账本"或"存在扩张后领土"时才产生现值——**当前两者皆无**，此刻加门控=防御性空转，无实际收益。
- 触发条件（与步骤6 追记 §④债、施实计划 L174 口径一致）：**领土入档（2_11 存档 kingdoms[] 拆分）或领土推进/玩家纳土（步骤12 落地）先到者**，届时 `RebuildInitial` 加 foundKingdoms 门控（新游戏走 RebuildInitial、读档走存档恢复），并同步回写步骤6/7/8 追记④债状态。
- 影响：本步不改 TerritorySystem 任何一行；冒烟不涉领土。

---

## 六、冒烟计划（行为级探针，`Valley2_17_Smoke_8`）

GameScene Play 上下文（切 GameScene 驱动真实世界，呼应步骤7 教训：裸 MainMenu 无世界只出 noAI），seed=20260826 两轮确定性：

| 探针 | 注入 | 断言 |
|------|------|------|
| ① 剧本推进+最小停留 | 直接注入 KingdomState/人口/建筑满足 存活→发育→扩张 阈值 | 按 surviveMinDays/developMinDays 最小停留推进（不早不晚）；到扩张期 |
| ② 单日最多升一级 | 同日注入「同时满足发育+扩张」条件 | 当日只升 1 级；次日再升（D319） |
| ③ 常设底线（粮） | 粮储清零 + 强制推进 | 次日 FocusController 出「屯粮/采集」焦点，无视评分排序（D322） |
| ④ 玩家无 Brain（#13） | 普通新游戏 | KingdomBrainRegistry 无 id=0；玩家 scriptPhase==null |
| ⑤ 机器四段全链（解耦 P0 产出约束）| 额外注入 战士≥4+扩张占区≥2 | 扩张→军事 触发；军事期注入"被打残"阶段标签不动（D318 单向） |
| ⑥ 确定性 | 同 seed 两轮 | 检测字符串逐字节一致 |

> 附：每探针贴确定性产出；编译 0 error 门槛同前（Unity MCP refresh_unity + read_console）。

---

## 七、文件脚印（实施范围声明）

**新增 `Systems/AI/KingdomBrain/`**：`KingdomBrain.cs`（日 tick 主脑）/`ScriptStageMachine.cs`（状态机）/`FocusController.cs`（焦点+底线+打断）/`KingdomBrainFactory.cs`+`KingdomBrainRegistry.cs`（创建钩子+Registry+2_19 销毁钩子）/`SimModeManager.cs`（P0 恒 Fine）。
**改**：`DayCycleSettlement.cs`（五步重构）/`KingdomFoundry.cs`（两处创建钩子）/`KingdomState.cs`（scriptPhase+focus+simMode）/`DamageSystem.cs`（KingdomAttackedEvent 挂伤害应用）/`GameEvents.cs`（新事件）。
**新增 SO**：`Data/KingdomBrainConfig.cs` + asset。
**不动（声明）**：TerritorySystem（④债挂起）、AI.Core/Ports/factor（步骤8 纯 Unity 侧王国脑，**无 sim 债务**，不新增决策输入）。

---

## 待决策事项（选项 + 推荐 + 影响）

| 决策点 | 推荐 | 备选 | 影响 |
|--------|------|------|------|
| ① Brain.Tick 位置 | **A 植入五步②，日结入账之前**（花昨日结存，保守，契合 15_账本 1 日滞后） | B 移到日结转账之后（花当日入账） | A：口径自洽、不稳超支、步骤9 顺畅；B：需改 15_账本语义、预算超前，性价比低 |
| ② ④债时点 | **本步不落地，归步骤12**（领土占位空、无现值） | 本步顺手加 foundKingdoms 门控 | 归12：不写防御性代码；本步加：纯防御空转、无现值、增长度 |
| 其余四点（二三四六） | 按报告方案执行 | — | 确认性落地，无实质分叉 |

> 备注：若策划对六点有增补/修订（尤其 Decision①若选 B、或对③P0 封顶扩张期的口径有异议），请在本 HH「策划裁决」节回写；执行端按裁决再开工步骤8。

---

## 策划裁决（2026-08-26 回写）

### 总裁决：六点全放行，按报告方案开工步骤8

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| ① Brain.Tick 位置 | **A 准**（植入五步②，日结入账之前） | 给定选项已最优：15_账本 L56 的滞后警告恰是 A 的依据——把滞后显式固化为脑预算口径（花昨日结存）比 B 消除滞后更诚实；B 需改账本语义+步骤9 口径双返工。A 的"若决策过钝解在 sim 侧"兜底路线也对——不在本步改次序 |
| ② ④债时点 | **归步骤12/领土入档先到者，准** | 门控加在零写入点上=防御性空转无现值；与步骤6 追记口径一致。"先到者"触发条件成立时**必须**落地门控+回写追记——此债已三处登记，届时不可再顺延 |
| ③ P0 封顶扩张期 | **口径准** | 行动子集无⑦⑩→军事期不可达=设计一致非停滞；冒烟⑤注入解耦验证机器层四段全链的处理正确（机器正确性 ≠ 对局可达性，分开验证是对的） |
| ④ 被攻击信号挂 DamageSystem 命中层 | **准** | "受击事实才算被攻击"的职责界定清晰；与此前 MonsterAI 解耦裁决完全一致 |
| ⑤ 生命周期/玩家无 Brain | **准** | 双短路+Registry 永不含 id=0+空转安全的销毁钩子，无分叉 |
| ⑥ 冒烟六探针 | **准** | 行为级探针覆盖此前教训（结构证据不算数）；探针⑤含 D318 单向性负探针，好 |

### 两条增补（实施时一并落）

1. **五步重构是行为保持重构**：现行 1~9 尾巴（饱食/幸福/税收/人口/贸易冷却/AI日结/牧场/营地补员）进⑤时**逐项次序保持不变**——这些是既有行为面，重构只做"包结构"，不做"重排序"。冒烟补一条既有结算回归探针（如饱食/税收断言），证明⑤尾巴零回归。
2. **KingdomAttackedEvent 只发 AI 国（kingdomId>0）**：玩家被袭事件面归 2_13/2_18（警报 UI/外交），本步在伤害管线层只对 AI 王国发——防玩家侧事件消费者未来误订。EventBus 洪泛教训（2_10 BuildingPlacedEvent）同族预防。

### 分歧裁决记录
- 无分歧。执行端 A 推荐 = 策划端结论；增补两条为验收面加强非方案分歧。