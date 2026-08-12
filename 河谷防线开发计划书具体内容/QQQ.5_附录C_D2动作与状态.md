# QQQ.5 附录C · D2 动作空间与状态表示（T2.1/T2.2）

> 日期：2026-08-10
> 状态：**初稿（对应落地实现）**
> 定位：经济训练（P2）的动作空间（AI 可决策什么）与状态表示（AI 看到什么）。对应 QQQ.5 §12.4 D2 动作/状态。

---

## 一、D2 动作空间（T2.1，AI 可决策参数）

经济训练用 CMA-ES 搜索这些连续参数（0-1），SimEconomy 按策略做 auto-player 决策。已落地为 `EconomyPolicy`（`harness/Economy/EconomyPolicy.cs`）。

| 动作 | 类型 | 范围 | 默认 | 语义 |
|------|------|------|------|------|
| `workerProdRatio` | 连续 | 0-1 | 0.6 | 新居民转生产工人的比例（高=全力生产，低=少生产） |
| `armyFocus` | 连续 | 0-1 | 0.5 | 训练士兵（Warrior）投入比例（高=暴兵，低=不训兵） |
| `foodReserveRatio` | 连续 | 0-0.8 | 0.3 | 粮保留比例（低于阈值×容量停训，防断粮崩盘） |
| `satietyHealthGate` | 连续 | 0-100 | 50 | 平均饱食低于该值停训（经济健康 gate） |

**动作维度 = 4**。当前 CMA-ES 训练只搜前 3 个（workerProdRatio/armyFocus/foodReserveRatio），satietyHealthGate 固定。

**trade-off 设计**：`armyFocus` 高 → 暴兵守城持久但耗粮/缺生产；低 → 生产稳定但兵力不足。`foodReserveRatio` 防"只暴兵不生产"导致断粮崩盘（经济健康 shaping 的早期防线）。

## 二、D2 状态表示（T2.2，AI 看到什么）

状态向量供报告/训练文档定义（当前评分只消费局末聚合，逐 tick 状态向量留 P3/后续）。维度：

| 分块 | 维度 | 内容 |
|------|------|------|
| 资源存量 | 7 | 木/石/矿/粮/金/水晶/火油（SimEconomy 资源字段） |
| 人口各职业数 | N | 君主/居民/工人/搬运/小孩 + 军事职业（PopulationPool） |
| 建筑列表摘要 | M | 各类型建筑等级/工人占用（BuildingState） |
| 对局进度 | 2 | 当前天数 / DaysPerRun、当前波次 |
| 威胁等级 | 1 | 夜晚波次敌人总战力（vs 我方士兵） |

## 三、评分目标（T2.5 全链路）

`EconomyObjective`（`harness/Economy/EconomyObjective.cs`）：

```
score = survival × 0.5        # 坚持天数 / DaysPerRun（主目标）
      + population × 0.2      # 终局人口健康（防人口暴跌）
      + economyHealth × 0.3   # (饱食≥50 + 主城未破) 平均（经济健康 shaping）
```

决策 17：坚持天数为主；shaping 防 AI 只暴兵不生产导致经济崩盘。

## 四、落地现状（2026-08-10）

- `EconomyPolicy`（可训因子）+ `SimConfig.economyPolicy`（champion/patch 可管理）
- `SimEconomy.AutoPlayerApply()`（auto-player 决策；`AutoPlayerEnabled` 开关，P2 训练专用，P0/P1 场景不启用不影响）
- `SimPatchLoader` 支持 `economyPolicy.x` 提案路径（T2.3）
- `econ-train` 命令：CMA-ES 搜索 3 个经济参数，目标 = EconomyObjective
- 训练场景 `econ_p2_train.json`（资源紧张，使生产-军事 trade-off 显著）

## 版本历史

| 日期 | 变更 |
|------|------|
| 2026-08-10 | 初稿：D2 动作空间（4 动作）+ 状态表示 + EconomyObjective 评分 + 落地现状 |