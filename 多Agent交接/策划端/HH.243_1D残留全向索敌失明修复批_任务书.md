# HH.243 1D 残留·全向索敌失明修复批 任务书

> 签发：主策划端（团结 Cowork）｜2026-09-12｜**Gate = 无（正确性修复·非王国AI域）**
> 决策依据：**D693**（0.6 §二百二十二 · 拆单裁决）
> 来源报告：[全工程性能热点静态审计_2026-09-12](../../河谷防线开发计划书具体内容/报告/设计审查/全工程性能热点静态审计_2026-09-12.md) §〇H2／§一H2
> 修法先例（**必读**）：[PerceptionSystem.cs](../../Valley%20Rampart/Assets/_Game/Systems/AI/PerceptionSystem.cs)（`D485` 单遍过滤法）
> **完成报告＝执行端届时按账本实时水位线取号**（`D640 #10` 禁预留）

---

## 〇、一句话目标

清除全库 **5 处** `y∈{0,1}` 1D 残留——该残留致**单位索敌/统计只扫最南两行**（其余行单位"看不见"），修法**照抄 `D485` 单遍过滤法**，不引入方格遍历。

---

## 一、根因与定性（D693 已裁·不得擅改）

**定性＝正确性缺陷（功能失明级）·非性能问题。**

实读锚点＝`GridTypes.cs:15-20`：`GridCoord.y` 注释原文「**`y=纵轴（语义变更：老 y=层已废）`**」＋层语义已迁 `layer` 字段（`L20`「`0=地面`」）。⇒ 代码中 `for(y=0;y<=1;y++)` 的**行内注释自称"y=地面+飞行两层"是错的**——`y` 现在是**地图行号**，`y∈{0,1}` ＝ **只扫最南两行**，其余行单位完全不可见。

**为何纯逻辑冒烟掩盖**：`PerceptionSystem` 走 `FallbackQuery` 已修，但本 5 处走 `GridSystem.GetUnitsInCell` 真实路径，冒烟未覆盖。

---

## 二、修复范围（全库 5 处·本端已全库 grep 实读）

| # | 文件:行 | 方法 | 语义 | 修法要点 |
|---|---|---|---|---|
| 1 | [UnitController.cs:1069-1087](../../Valley%20Rampart/Assets/_Game/Systems/Unit/UnitController.cs#L1069-L1087) | `FindNearestEnemy` | 单位/塔/机器索敌 | 改单遍：`UnitRegistry.GetAllUnits()` → 阵营过滤（异阵营且非 None）→ 欧氏圆（`rangeWorld`）→ 取最近 |
| 2 | [UnitController.cs:1149-1165](../../Valley%20Rampart/Assets/_Game/Systems/Unit/UnitController.cs#L1149-L1165) | `CountCrewWorkers` | 机器操作工人统计 | 改单遍：同阵营 → `IsWorker` → 欧氏圆（`crewRadius`）→ 计数 |
| 3 | [MonsterController.cs:114-133](../../Valley%20Rampart/Assets/_Game/Systems/Disaster/MonsterController.cs#L114-L133) | `FindNearestHuman` | 野怪索敌 | 改单遍：`Faction.PlayerCamp` → **保留 `kingdomId==0` 守卫**（L127·语义不得丢）→ 欧氏圆 → 取最近 |
| 4 | [ProjectileManager.cs:280-297](../../Valley%20Rampart/Assets/_Game/Systems/Combat/ProjectileManager.cs#L280-L297) | 穿透挡墙扫描 | 投射物穿透 | 改单遍：沿弹道**取圆/矩形带内**单位 → `fortification` 过滤 → 弧高/穿透判定（**语义不变**，只换索敌方式） |
| 5 | [NPCBrain.cs:1696-1700](../../Valley%20Rampart/Assets/_Game/Systems/AI/NPCBrain.cs#L1696-L1700) | `QueryUnitsInRangeX` | 穿透冲锋路径 | 改单遍：按 `x1~x2` 范围取单位（**注意：本方法语义＝X 范围带查询**，改单遍后覆盖全 y，非只 `{0,1}`） |

> ⚠️ **汇总提示**：`#1/#2/#3` 是同一模式（欧氏圆半径索敌）——三处建议**共用一个 helper**（可抽取 `QueryUnitsInRadius` 于合适位置），避免第三份拷贝。`#4/#5` 是带状/路径查询，语义与圆索敌不同，**单独处理**。

---

## 三、🔴 修法铁律（防二次返工·不得违反）

1. **必须走 `D485` 单遍过滤法**：`UnitRegistry.Instance.GetAllUnits()` 全量单遍 → 阵营/条件过滤 → 距离判定。先例＝[PerceptionSystem.cs:22-49](../../Valley%20Rampart/Assets/_Game/Systems/AI/PerceptionSystem.cs#L22-L49)。
2. **🔴 严禁采信报告 §一「修复方向」建议的"重设计为全向邻近格"**——该路已由 `2_21 §3.2` 与 `0.6 D485` **明确否决**：`GetUnitsInCell` 每次调用 **O(N)**，方格遍历 `(2n+1)²` **平方级放大不可接受**。照报告建议施工＝用未裁决口径覆盖已裁 `D485`＝双错叠加。
3. **🔴 严禁直接改 `GridSystem.GetUnitsInCell` 本身**（那属性能批 DZ-150 的反向索引域；本批只改调用方）。
4. **可加 `PerceptionSystem.QueryNearby` 复用**：`#1/#3` 若语义匹配（阵营+半径+最近/枚举），优先复用既有 `QueryNearby`，避免第四份拷贝。

---

## 四、实施清单

| 编号 | 任务 | 依赖 | 验收标准 | 证据 |
|---|---|---|---|---|
| T1 | `FindNearestEnemy` 改单遍过滤法 | — | grep 该方法内无 `y<=1`；单位在任意行可被选中 | `UnitController.cs` |
| T2 | `CountCrewWorkers` 改单遍过滤法 | — | 同上；机器工人在任意行被正确统计 | `UnitController.cs` |
| T3 | `FindNearestHuman` 改单遍过滤法 | — | 同上；**`kingdomId==0` 守卫保留**（AI 工人不被怪当玩家目标） | `MonsterController.cs` |
| T4 | 投射穿透挡墙扫描改单遍/带状 | — | 穿透判定语义不变（弧高/穿透等级照旧） | `ProjectileManager.cs` |
| T5 | `QueryUnitsInRangeX` 改单遍/带状 | — | 冲锋路径查询覆盖全行 | `NPCBrain.cs` |
| T6 | 全库 grep 复验 | T1~T5 | `grep -n "y *<= *1"` 相关索敌/统计处 **0 命中** | 命令输出 |
| T7 | 回归验证 | T1~T6 | 见 §五 验收线 | — |

---

## 五、验收线（硬）

1. **全库清零**：`grep` 确认 5 处 `y∈{0,1}` 索敌/统计残留**全部消除**（幸存者须逐条说明理由，如确为合法用途）。
2. **跨行可见**：进局（走正门 `TestHarnessApi.EnterTestRun`·`test-harness-first`）实证——单位**放置于非最南两行**时，也能被塔/机器/野怪正确索敌（探针可读或行为观测）。
3. **零回归**：既有冒烟容器（`Smoke_2_22P0`／`Smoke_2_23RP0`／`Smoke_5` 等）**零退化**；编译 **0 警 0 错**。
4. **语义守恒**：`MonsterController` 的 `kingdomId==0` 守卫、`ProjectileManager` 的弧高/穿透等级判定**逐字保留**。
5. **不碰红线**：AI.Core **零改动**（5 处均不在 `AI.Core.asmdef` 内，已核）；`GridSystem.GetUnitsInCell` **未改**。

---

## 六、红线

- **`test-harness-first`**：进局测试一律走正门 `EnterTestRun`（禁裸跑 GameScene）＋收工 `ExitTestRun` 退 Play（`L-32`）。
- **`sim-sync`**：本批**不触 AI.Core**（5 处均在 `Assembly-CSharp`），无需 sim 镜像（如执行端发现触 AI.Core，**立即停手报裁**）。
- **并发写**：只改本批 5 文件，改完即 commit（`agent-handoff §六 #9`）。
- **排雷**：`L-15`（修复同类残留须全库清点——本批已列全 5 处）／`L-01`（就位≠生效）／`L-22`（确定性双跑）。

---

## 七、请裁项（执行端开工前须先报）

1. `#1/#2/#3` 是否**抽取公共 helper** 或**复用 `PerceptionSystem.QueryNearby`**？——建议抽 helper（三处同模式）。
2. `#4/#5` 的**带状/路径**索敌改法（矩形带 vs 沿弹道采样）——须报设计方案再施工。
3. 是否存在**报告未列的遗漏位点**（如 `GetUnitsInCell` 的其他消费方）？——开工前须全库复扫确认。

---

## 八、产出

- 代码改动（5 文件）＋ commit。
- 完成报告（执行端取号·含 grep 证据链＋探针/行为观测＋冒烟回归读数）。
- 若发现新遗漏位点或语义冲突 ⇒ **列报不擅改**。
