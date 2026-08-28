# HH.26 2_17 步骤9 效用评分器收口 + P0 总验收清单（待策划裁决完整局）

> 类型：步骤收口报告 + P0 收官验收清单报批
> 状态：⏳待策划裁决（裁清单后一次性跑完整局 Play 批次）
> 日期：2026-08-26 · 发起端：执行端 · 关联：HH.24(53c9064)/HH.25 / 2_17_AI王国脑与自主成长.md(D311~D350) / 2_17_实施计划.md §步骤9-10
> 前置：HH.25 已验收步骤8；裁决③④债维持；裁决②步骤9 准开工（无 Gate）

---

## 〇、一句话收口

**四因子效用评分器（D323）+ 相位权重 + 可行性门控（D346）+ 常设底线覆盖（D322）已落地，P0 行动子集 ①~⑥+⑬⑭ + UtilityActionConfig SO 数据驱动——冒烟 `Valley2_17_Smoke_9` 三条验收 ALL PASS，编译 0 error。步骤9 收口。**

---

## 一、实现摘要（对照 HH.25 裁决② 三条验收）

| 裁决 | 要求 | 落地 |
|------|------|------|
| 步骤9 准开工（无 Gate） | 效用评分器 P0 子集 8 项（①~⑥+⑬⑭，D345/D346/D323）+ 阈值/权重落 SO | `UtilityActionConfig` 类+资产（8 项行动 def：minStage/axis/axisWeight/need/needA/needB/stageWeight 全 SO 化）；`UtilityScorer`（D323 四因子=需求强度×性格权重×可行性×阶段权重） |
| 验收① 性格分化可测 | 五轴不同权重走出不同焦点倾向（冒烟 #3） | `ScoreTop` 以 `personality[axis]×axisWeight` 线性乘入（D311）；冒烟 #3：好战模板→`BuildCapacity`，经济模板→`BuildHouse`，断言 `divergence` = **OK** |
| 验收② ⑥招工人可执行 | AI 存活期不卡死关键路径（冒烟 #19） | `ScoreTop` gap>0 且 `Feasible`(gold>0 且 workerCount<needA) 才可得候选；冒烟 #19：焦点非空=评分非空=⑥可开=**全 OK** |
| 验收③ 常设底线覆盖评分 | 底线触发时评分排序失效（冒烟 #4） | `FocusController.Update` 先判粮底线/grainAlarm→强制 `FocusGranary`、即时跳过防抖；再判被攻击防御；否则才评分。冒烟 #4：评分态焦点⑥被粮底线强制翻到屯粮⑤ = **OK** |

**焦点下发契约**：`KingdomBrain.Tick` 集成 `Focus.Update(kingdom,cfg,ucfg,day)`（昨日结存口径 Live）+ `ExecuteFocus` 下发骨架（⑥招工人→TryTrain、建造类→TryBuild 留 P0 完整局批次接派送、⑬⑭/None 空姿态）。真实派遣（建造选址/招募实体化）与王国脑行为级时间推进均归完整局批次——**执行端诚实声明，不虚报行为级完成**。

---

## 二、文件脚印

**新增** `Data/Kingdoms/UtilityActionConfig.cs` + `Resources/Config/Kingdoms/UtilityActionConfig.asset`（`UtilityActionDef` 8 项 + `PersonalityAxis` 五轴枚举 + `LoadConfig` 缺 asset 回退）。`Systems/AI/KingdomBrain/UtilityScorer.cs`（`UtilityAction` 枚举含 P0+P1 位、`NeedKind` 8 缺口、`ScoreTop`/`NeedsScore`/`Feasible`/`CountActiveBuildings`，口径=昨日结存 PerPopGrain=1）。

**改** `Systems/AI/KingdomBrain/FocusController.cs`（D322 完整焦点模型：常设底线→评分→防抖≥minDays→焦点=行动 id；`FocusGranary=5/FocusDefense=14` 对齐枚举；兼容 3 参重载防破坏步骤8 冒烟）。`KingdomBrain.cs`（Tick 集成 Focus.Update+ExecuteFocus）。

**冒烟**：`Assets/Editor/Smoke/Valley2_17_Smoke_9.cs`（探针 #19/#4/#3，自包含单轮）。

**无 sim 债务**：UtilityScorer/FocusController 纯 C# 无 Unity 引用，未新增 AI.EEC 决策输入、未动 champion/factor_registry/SO、未动 sim harness（HH.24 §七 重申）。

---

## 三、冒烟证据（Valley2_17_Smoke_9，GameScene Play，seed=20260827）

```
[2_17_9冒烟] #19焦点非空=OK #19评分非空=OK #19⑥可招=OK #4底线覆盖评分=OK
[2_17_9冒烟] #3性格分化(好战BuildCapacityvs经济BuildHouse)=OK
[2_17_9冒烟] ===== ALL PASS（#19存活期可执行不卡死/#4底线覆盖评分/#3性格分化）=====
```

> 编译 0 error（首版曾 `gold=100f` 隐式 float→int CS0266，已修为 int 后全绿）。

---

## 四、④债状态重申（HH.24 裁决③绑定，不可再顺延）

维持：④债按**先到者**归 步骤12 / 领土入档（`RebuildInitial`），**触发条件成立时必须落地 `foundKingdoms` 门控 + 三处追记回写**（王国入库判定，不误圈自然建筑），HH 报告明确这一点，账不受步骤9 影响。

---

## 五、P0 总验收清单（请策划裁决后一次性跑完整局 Play 批次）

> 步骤9 为 P0 最后一步。其后 P0 整体验收 = 执行端多次承诺的「完整局 Play 批次」兑现时点。本清单合并此前各步累积的**残⚪**（结构前提已具备、未做行为级终验的项）一并清账。

**A. P0 总验收主线（单 AI 王国自主成长，四判据）**
1. 单 AI 王国自主成长至**扩张期不夭折**（剧本演进 存活→发育→扩张 全链走通）
2. **无 ≥10 日停滞**（王国脑每日推进，焦点不空转、不卡死）
3. **同 seed 逐字节一致**（完整局跑两遍，时间线快照逐字节相同）
4. **玩家零回归**（玩家王国全程自主/受控操作零干扰，结算尾巴零回归）

**B. 残⚪合并项（各步骤结构已就位、行为级终验归本批次）**
| 来源 | 残⚪ | 本批次终验点 |
|------|------|--------------|
| 步骤5 | 正向玩家招募 / 两国并行 | 玩家 TryTrain 招募实体化 + AI/玩家两王国各自队列独立并行 |
| 步骤3 | 行为级池隔离与供水 | AI 建筑任务源登记→池隔离路由→AI 工人真派到 AI 建筑（行为级，非注入） |
| 步骤4 | LoadState 存读回环 | 完整局中途 存档→LoadState→续玩，资源/人口/建筑/王国脑状态一致闭环 |
| 步骤8/9 | Brain 行为级（真实对局四段推进，非注入） | 王国脑真实随日 tick 走完四段 + 焦点随评分/底线真实切换（ExecuteFocus 派送/招募实体化） |
| 步骤9 | 效用评分→真实派遣 | ⑥招工人 TryTrain 实体化、建造类 TryBuild 选址落成（接执行骨架） |

**裁法**：策划审核 A/B 清单 → 若放行，执行端一次性跑完整局 Play（单票入口/自动推进/快照取证）并回填验收报告。

---

## 六、待决策（请策划裁决）

1. **验收步骤9 收口** + 上述三条冒烟全绿视为成立？
2. **P0 总验收清单（§五 A 四判据 + B 五残⚪合并项）**是否就是完整局 Play 批次的验收口径？准后一次性跑。
3. ④债门控（领土入档）确认「先到者」裁决维持，触发时执行端落地不回顺延？