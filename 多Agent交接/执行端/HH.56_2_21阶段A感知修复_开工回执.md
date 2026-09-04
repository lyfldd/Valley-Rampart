# HH.56 2_21 阶段A（感知修复）开工回执「范围+锚点」

> 类型：开工回执 · 状态：⏳实施中 · 日期：2026-09-04 · 发起端：执行端
> 关联：策划端插队指令（58f13bc）、2_21 总纲 §三（D485）、0.6 §五十（D484~D488=2_21 链）、Q10 批1 已终验收入库（8a2aa17/a2de588）

## 一、范围回执

**做**（仅阶段A）：
1. `PerceptionSystem.QueryNearby` 1D 残留 2D 化=单遍过滤法（UnitRegistry 单遍+阵营过滤+世界坐标欧氏圆；GetUnitsInCell 方格遍历弃用；FallbackQuery 分支合并删除；签名/调用方零改动）
2. 行为级探针 P-A1~A6 硬性（纵向发现/无敌误报/保护恢复/治疗恢复/友军因子/回归零退化）——新建冒烟容器 `Valley2_21A_Smoke.cs`（仿 2_20 冒烟模式），遵守 09-03 新纪律（用户 MainMenu 正常进局后触发）
3. 阶段A 完成报告（P-A1~A6 逐项证据+回归声明+sim 义务评估）落 执行端/

**不做**（红线确认）：阶段B~E 不动；不触 AI.Core/Decision（单遍过滤法=壳层实现，AI.Core 零改动）；sim 侧不代做（义务如实列报）；设计文档正文只读；不 commit（策划端验收后代执）。

## 二、锚点（已实读）

- **病灶实锤**：[PerceptionSystem.cs](Valley Rampart/Assets/_Game/Systems/AI/PerceptionSystem.cs) L45-47 `for dx × for y ∈ {0,1}`——仅扫最南两行，2.5D 纵向失明；FallbackQuery（L71-96）语义=目标态。
- **连带恢复确认**：_nearestDist=NPCBrain.cs L519-532 感知循环内基于 QueryNearby results 计算→L928 写 FactorContext.NearestEnemyDist——**QueryNearby 修复后零改动自动恢复** ✓
- **探针断言点已定位**：P-A1=UpdatePerception L514→L521-543 ThreatStimulus 注入（_nearbyEnemies 反射计数）；P-A3=brain.HasProtection（public，SumNearbyProtectPower L984-990 遍历 _nearbyAllies）；P-A4=TryHealAlly L1329-1358（_nearbyAllies 选血最少低血友军→Heal，行为级=血量回升）；P-A5=_nearbyAllies/NearbyAllyCount；材料=AIDebugSpawnController.Spawn（PlayerShieldGuard/PlayerHealer/EnemyWarrior 全有）。
- P-A6 回归对象：Smoke_14/三冒烟/编队冒烟（具体 MenuItem 实施时 grep 确认）。

## 三、分批计划

- 批A-1：QueryNearby 改造+编译 0 error
- 批A-2：冒烟容器 Valley2_21A_Smoke（P-A1~A5 正负双侧）+编译
- 批A-3：用户进局跑冒烟（P-A1~A5）+P-A6 既有冒烟回归跑批
- HH.57 阶段A 完成报告 → 策划端验收 → Q10 批2（M3+M5）解锁

## 四、风险/预披露

- P-A3 HasProtection 断言依赖决策核 Update 周期填充 _lastCtx（需等待数帧）；protectPower 取值随 Profession（盾卫 0.2 基线？）vs protectThreshold（AttentionTuningConfig）——若基线配置不满足阈值，探针材料改用将军（protectPower 高配）或调参口径请策划批注，实施时实证。
- P-A6 既有冒烟可能有收工叫停/环境依赖历史（如 2_17 系列在真实世界跑）——回归口径=改后重跑结果对照改前记录，若历史记录缺失则声明"编译通过+本次实跑结果留档"。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| （无待决策项，风险两条为实施中自证或再上报） | | |
