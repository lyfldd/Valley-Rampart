# QQQ.5 附录B · golden replay 协议（T0.11）

> 日期：2026-08-10
> 状态：**初稿（P1 实施时细化）**
> 定位：sim 经济沙盘与 Unity 王国经营（3.5）的**输出对比标准**，保证 sim-to-real 不漂移（QQQ.5 点 9 工程保真 / 决策 2 sim 独立建模+golden replay）

---

## 一、目的

sim 经济模型是**独立建模**（不抽纯 C# 双编译），因此必须用 golden replay 对齐 Unity 真身，防止"sim 数值对、Unity 跑不通"或反向。协议定义：**同输入场景 → sim 与 Unity 各跑一遍 → 对比每日输出指标**，超出容差即判定漂移。

## 二、对比输入（同输入场景）

- 同一开局：初始资源（木/石/矿/粮/金/水晶）+ 建筑布局（类型/等级/工人）+ 初始人口（君主/居民/工人）+ 房屋容量 + 训练队列 + 流浪汉池
- 同一规则参数：产量/耗粮/训练成本/税率/生育条件/死亡扣幸福 K
- 同一天数（如 7 天）

## 三、对比输出指标（每日）

| 指标 | sim 字段 | Unity 对应 | 容差 |
|------|---------|-----------|------|
| 资源存量 | economy.report.resources | 仓库/粮仓独立存储 | 每资源 ±10% |
| 人口各职业数 | economy.report.population | PopulationSystem 实体计数 | ±2 人 |
| 平均饱食/幸福 | population.avgSatiety/avgHappiness | Satiety/Happiness 系统 | ±5 |
| 税收累计 | stats.taxCollected | TaxSystem | ±10% |
| 训练完成数 | stats.trainingsCompleted | TrainingSystem | 逐项一致 |
| 水晶副产 | stats.crystalProduced | 矿洞 Lv2 副产 | ±10% |
| 出生/流浪汉 | stats.births/vagrantsRecruited | 生育/招募 | 逐项一致 |

## 四、sim 专调参数差异标注（§12.6 洞察 3）

| 参数 | sim | Unity | 说明 |
|------|-----|-------|------|
| 生育冷却 | 3 天 | 10 天 | 7 天/局可触发 2 轮生育；**必须在协议中显式标注** |
| 单局天数 | 7 天 | 持续 | sim 定长对局 |
| 经济结算频率 | 每天 10 次 | 每秒 tick | sim 抽象加速 |

> 所有 sim 专调参数集中在 `EconomyConfig`（含 Unity 对比值注释），golden replay 对比时**排除**这些已知差异，只比"同参数下"的产出曲线。

## 五、执行方式

- **P1 起**：跑同一 econ 场景，sim 出 `results/econ/report.json`，Unity 侧在相同手工输入下记录每日资源/人口，脚本对比（`compare_economy.ps1` 待建）。
- 容差外 → 判定漂移 → 定位是 sim 建模还是 Unity 实现问题，修正后重跑。
- 确定性：sim 经济无 RNG（生育/招募固定判定），同 seed 天然逐字节一致；比对基准用 run0。

## 六、待细化（P1 实施时）

- Unity 侧"手工输入"标准化（如何注入同开局/同规则）
- 对比脚本实现（`compare_economy.ps1`）
- 容差阈值校准（首轮对跑后定）

## 版本历史

| 日期 | 变更 |
|------|------|
| 2026-08-10 | 初稿：对比输入/输出指标/容差 + sim 专调差异标注（生育冷却 3 vs 10）+ 执行方式 |