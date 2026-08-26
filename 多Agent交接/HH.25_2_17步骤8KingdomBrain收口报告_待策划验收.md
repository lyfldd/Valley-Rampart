# HH.25 2_17 步骤8 KingdomBrain 王国脑骨架 — 收口报告（待策划验收）

> 类型：步骤收口报告（HH.24 六点放行 + 增补两条已实施完毕，请策划验收）
> 状态：⏳待策划验收（准后即开步骤9；④债触发时须落地 foundKingdoms 门控）
> 日期：2026-08-26 · 发起端：执行端 · 关联：HH.24 / 2_17_AI王国脑与自主成长.md(D317~D350) / 2_17_实施计划.md §步骤8-9
> 前置：HH.24 已裁决（53c9064）六点全放行 + 两条增补；步骤7 已落地（69a0035）

---

## 〇、一句话收口

**D347 五步权威日 tick 已落地 + 剧本状态机骨架 + 常设底线/焦点 + Brain 生命周期 + 玩家无脑——冒烟 `Valley2_17_Smoke_8` 六探针 + 增补①既有结算回归探针 ALL PASS，编译 0 error。报策划验收步骤8。**

---

## 一、实现摘要（对照 HH.24 六点 + 增补两条逐项落地）

| 决策点 | 裁决 | 落地 |
|--------|------|------|
| ① Brain 位置 A | 植入五步②、日结入账以前 | `DayCycleSettlement.OnDayChanged` 重构为 D347 五步（SimMode判定→Brain.Tick→领土占位空→CampUpgrader→尾巴）；`TickKingdomBrains()` 在 `AIEconomySettlement.Tick()`（步⑤尾巴内）**之前**跑——脑视图库=昨日结存（`KingdomState.resources` 未含当日在储仓） |
| ② ④债归步骤12 | 触发时必须落地，不可再顺延 | 本步**不改 TerritorySystem 一行**，步③领土占位空；债状态声明保持三处登记，步骤12/领土入档先到者时在 `RebuildInitial` 加 foundKingdoms 门控 |
| ③ P0 封顶扩张期 | 口径准 | `ScriptStageMachine` 扩→军事阈值含战士≥4/人口≥12/扩占区≥2(D349)；P0 行动子集①~⑥+⑬⑭ 无⑦/⑩→真实对局停扩张=设计一致；冒烟⑤注入解耦验证机器层四段全链 |
| ④ 被攻击挂 DamageSystem 命中层 | 准 | `DamageSystem.PublishDamagedEvent` 命中应用点对 `kingdomId>0` 发 `KingdomAttackedEvent`（只发 AI 国·增补2）；`FocusController` 订阅→次日强制防御姿态（D322）；不挂怪物/波次选目标层 |
| ⑤ 生命周期 | 准 | `KingdomBrainFactory.Create` 两创建钩子（FoundFirstGeneration ①/FoundFromCamp ②）；`KingdomBrainRegistry.Unregister` 销毁+成对退订（2_19 吊钩）；玩家 id=0 双短路 |
| ⑥ 六探针 | 准 | 全绿（见 §三） |

**增补两条**：① 五步=行为保持重构——尾巴 1~9（饱食/幸福/税收/人口/贸易冷却/AI段日结/牧场/营地补员）逐项次序不变，只 CampUpgrader 归位步④（设计重排）；冒烟补既有结算回归探针。② `KingdomAttackedEvent` 只发 AI 国（kingdomId>0），玩家被袭事件面归 2_13/2_18。

---

## 二、文件脚印

**新增 `Systems/AI/KingdomBrain/`**：`KingdomBrain.cs`（主脑，Tick 采快照→状态机→同步 scriptPhase→刷新焦点）/ `ScriptStageMachine.cs`（含 `ScriptStage` 枚举 + `ScriptStageContext` 探针快照；四阶段单向+最小停留+每日升一级 D317~D320/D349）/ `FocusController.cs`（焦点+常设底线粮/被攻击+防抖+打断 D322/D340）/ `KingdomBrainFactory.cs`+`KingdomBrainRegistry.cs`（D337 创建/销毁钩子+Registry 永不含 id=0）/ `SimModeManager.cs`（`SimMode` 枚举 + P0 恒 Fine）。

**新增** `Data/Kingdoms/KingdomBrainConfig.cs` + `Resources/Config/Kingdoms/KingdomBrainConfig.asset`（全阈值 SO 化，so-data-driven）。

**改**：`DayCycleSettlement.cs`（D347 五步重构）/ `KingdomFoundry.cs`（两创建钩子）/ `KingdomState.cs`（+`ScriptStage? scriptPhase`/`int focus`/`SimMode simMode`）/ `DamageSystem.cs`（KingdomAttackedEvent 命中层）/ `GameEvents.cs`（`KingdomBrainCreatedEvent`/`KingdomAttackedEvent`）。

**不动（声明）**：TerritorySystem（④债挂起）、AI.Core/Ports/factor（步骤8 纯 Unity 侧王国脑，**无 sim 债务**，不新增决策输入）。

**冒烟**：`Assets/Editor/Smoke/Valley2_17_Smoke_8.cs`。

---

## 三、冒烟证据（Valley2_17_Smoke_8，GameScene Play，seed=20260826 两轮）

`[2_17_8冒烟]===== ALL PASS`：

```
玩家无脑=OK  玩家无阶段=OK  粮底线屯粮=OK
scan=存活0;发育0;...;（15日王国脑日tick确定性扫描，两轮逐字节一致）
①存活D1不动=OK  ①存活D2升发育=OK  ①发育停3日=OK   （剧本最小停留：不早不晚）
②单日只升一级=OK  ②次日不连跳=OK   （D319 每日最多升一级）
⑤四段全链军事=OK  ⑤军事期不打回=OK （机器四段全链 + D318 单向不回退）
确定性两轮逐字节一致=OK
回归尾巴=OK（饱食/税收/AI段/牧场/营地全链走通 + DaySettledEvent 照发——增补①既有结算零回归）
```

> 已注销一项实测环境说明：⑥确定性原初因"两轮共享已注册王国脑实例状态机累积"误报 FAIL，改为每轮 new 临时王国脑探针保干净起点后返回 OK——非产品决策核非确定性，是测试隔离修复（见 `WorldScenario` 注释）。

---

## 四、待决策（请策划验收）

1. **验收步骤8 收口**：上述落地 + 冒烟全绿视为成立？
2. 准后 **步骤9 效用评分器**（不设 Gate 照做；验收须带性格分化可测断言，如冒烟 #19 ⑥招工人可执行、#3 性格分化、#4 常设底线）。
3. **④债**（领土入档 foundKingdoms 门控）：维持归步骤12/领土入档先到者；**触发条件成立时执行端必须落地门控+回写追记，不可再顺延**（HH.24 裁决②重申）。

---

## 五、无 sim 债务声明

步骤8 王国脑为 Unity 侧纯决策骨架上位（ScriptStageMachine/FocusController 均纯 C# 无 Unity 引用），未新增 AI.EEC 决策输入、未改 champion/factor_registry/SO、未动 sim harness——与 sim 无同步义务（HH.24 §七 已声明，此重申）。