# HH.76 任务书：零碎小批——双模板补井+EventBus 日 tick 枚举异常修复（P1 三考前置）

> 类型：任务书（零碎小批派单）
> 状态：⏳待执行端接单
> 日期：2026-09-05 · 发起端：策划端 · 前序：HH.75 P1 重跑再熔断报告（§策划裁决已回写，D539）
> 决策号：D539（0.6 §七十一）· 批性质：P1 三考前置零碎包（用户拍板立即插队）

## 一、施工两件

### 件1：SnowRock/GoldenWheat 双模板补 Well（一行资产×2）

- `Kingdom_SnowRock.asset` / `Kingdom_GoldenWheat.asset`：baseBuildingDefIds 在 farm 后插 `Well`（大写 W——资产 id 实值，HH.73 笔误教训），目标序=`castle, farm, Well, mine, Warehouse, quarry`。
- buildingCount 无需再调（HH.73 已调 4/5/6，对六模板池全局生效；SnowRock/GoldenWheat 新序村落档 4 取 castle/farm/Well/mine ✓）。
- 背景：HH.73 任务书只点三族=模板池六族认知遗漏（策划端认账，HH.75 §四.1）；首轮 P5 的 k2 现象经双矮人对照根因闭合=SnowRock 无井（派工域定性更正记档 D539）。
- **验证探针**：SnowRock 模板立国（或改 KingdomTemplateLibrary 抽取验证）→Well=1+AI 桶蓄水+farm 产粮非零。

### 件2：EventBus `TimeDayChangedEvent` 枚举异常修复（×4 实锤）

- 现象：`[EventBus] TimeDayChangedEvent 的处理器抛出异常: InvalidOperationException: Collection was modified`（P1_run2 D27/D36/D38/D39 ×4，时点与 k2 人口死亡吻合）。
- 根因方向：某订阅者日 tick 处理器内遍历集合（疑似 UnitRegistry/PopulationSystem._entities/订阅者列表之一）过程中发生增删（死亡/入册）。
- 修复口径：**订阅者侧修复优先**（遍历改快照副本/延迟增删），EventBus 本体广播机制不动（广播快照若既有实现则维持）；修复后该订阅者对其它事件的同类模式顺手排查（同模式病灶一次清）。
- **验证探针**：长跑（≥39 日等价 tick 量或定向复现）InvalidOperationException 零命中+k2 人口死亡时点路径回归正常。

## 二、红线与纪律

1. 零业务行为变更（补井=纯资产；异常修复=防御性，不改任何系统语义）。
2. AI.Core/sim/champion/训练仓零触碰；RulerController 零触碰。
3. 冒烟全绿才 commit（HH.53）；git diff HEAD 自查为交付前置（HH.42）。
4. 零碎批纪律：两件合一报告（HH.77），轻量汇报不硬凑节。

## 三、交付物

1. 两件施工 diff+探针证据。
2. HH.77 完成报告（含件2 订阅者定位结论与同模式排查面）。
3. 完工后 P1 三考解锁条件仅剩 AI 人口再生批（Gate 报告待出，见 HH.75 裁决）。

## 四、策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 两件施工与探针验收 | | |
| 件2 订阅者定位结论 | | |
