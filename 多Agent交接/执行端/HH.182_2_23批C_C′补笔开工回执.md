# HH.182｜2_23 资源 P0 批C C′ 补笔 开工回执

> 执行端（Unity 轨）· 2026-09-11
> 状态：🟡开工中（D644 裁 A 放行）
> 锚点：**D644（0.6 §一百七十三，2026-09-11）**＝HH.181 三条勘正全成立＋**裁 A（收敛版）**＋姊妹项立 **DZ-089** 挂账；上游 D643（批C 验收成立销号）／HH.181 §六（裁决规格）
> 完成报告＝按账本实时水位线另取（**预计 HH.183**）

## 一、范围复述（按裁 A 逐字执行）

| 项 | 规格（D644） | 备注 |
|----|--------------|------|
| **R-C′1**（唯一代码改动） | [MapProduceToEco](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L492-L505) L494 加真产能守卫：`def == null \|\| def.producer.kind != ProduceKind.Resource \|\| def.producer.rate <= 0f \|\| def.isResourceNode` → `-1`；**口径对齐 [BuildingFactory.cs:295](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L295)**（`rate > 0f && kind == Resource && !isResourceNode`） | 其余一字不动 |
| **R-C′2**（探针） | [Smoke_2_23RP0.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Smoke_2_23RP0.cs) 加**正例**「有 Granary 无 Farm 时 通道A/R-C2 仍选建 farm」＋**负例**「farm/quarry 仍计产能；granary/warehouse/wood_pile/stone_pile/**Gold-默认族(~25)** 均不再计」 | 探针走正门（L-17） |
| **R-C′3**（契约同步） | HH.180 §六 契约 `Production` 计数口径改＝`rate>0 && kind==Resource && !isResourceNode` | 仅文档同步 |
| ❌ **不做** | `ProduceKind.None` 尾追／**任何**资产 `producer.kind` 改动／离屏 `AbstractEconomySettlement.cs:197` 改动（姊妹项＝**DZ-089** 挂账，独立裁决） | 触零漂移红线 |

## 二、预期收益（D644 §裁 A）

- 诊断侧只计**真产能建筑** ⇒ **解 通道A / R-C2 被仓储（Granary→Food、Warehouse→Wood）与非产能建筑（~25 个 Gold-默认族、wood_pile/stone_pile）掩蔽**。
- **零行为漂移**：`BuildingFactory` 本就未给 `rate=0` / `isResourceNode` 建筑挂 `ProducerComponent`（实际产出未变），本次仅收口**诊断口径**漂移。

## 三、红线自检（开工前）

- AI.Core **零直改**（本串不触 AI.Core；R-C5 镜像契约已交训练仓）
- 死表 `TaskPriorityConfig` / `TaskScheduler.cs` **零动**
- 常设底线三级序（粮→人口→被攻）**不动**
- 资产 **零改动**（D644 已撤回 kind 显式声明）
- 确定性：`MapProduceToEco` 为纯函数（输入 def → 输出 int），同 seed 同输出
- 排雷：L-29（检索必做阳性对照、优先 `Grep` 工具）／L-28（分类字段默认值＝有效值陷阱）／L-01（加守卫后必验消费端 live）／L-02（落盘即验）／L-12（单点串行）／L-17（探针走正门）／L-24·L-25（根因/档位带 `file:line` 直读）

## 四、门禁（交付前置）

1. 编译 0 警 0 错
2. `Smoke_2_23RP0` **22/0**（＋新正/负探针）
3. `Smoke_2_22P0` **32/0 零退化**（既有基线）
4. 确定性（同 seed 双跑归一化逐行一致）
5. `AI.Core` git status 空 ＋ 死表零动

## 五、批序与纪律

- 批序＝**开工回执（本信）→ 实施 → 交付报告 → 策划端验收**
- 写-改-commit 同串；**只提本串**、禁 `git add -A`、**不 push**、收尾**退 Play**
- 取号遵 D640 #10（**独立单行 commit `0b689bc`**）
- **C′ 落盘 → 策划端验收 → 「2_23 资源 P0」收官 → P1 七考放行**