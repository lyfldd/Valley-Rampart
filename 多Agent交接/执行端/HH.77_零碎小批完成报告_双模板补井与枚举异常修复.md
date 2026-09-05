# HH.77 零碎小批完成报告（双模板补井+EventBus 枚举异常修复，两件合一）

> 类型：完成报告（零碎批·轻量）
> 状态：⏳待策划端验收
> 日期：2026-09-05 · 发起端：执行端 · 任务书：策划端/HH.76_零碎小批_双模板补井与EventBus异常_任务书.md（D539）

## 一、件1：SnowRock/GoldenWheat 双模板补 Well

- 施工：两资产 baseBuildingDefIds farm 后插 `- Well`（大写 W=id 实值，Well.asset L15 复验在先），目标序 `castle,farm,Well,mine,Warehouse,quarry` ✓；buildingCount 不动（HH.73 4/5/6 全局生效）。
- 落盘复验：grep 四模板（Bedrock/DenseForest/SnowRock/GoldenWheat）Well 全在场。
- **探针（Valley_HH76_Smoke，seed=52707 五国局）**：
  - P1' 结构：k1/k2/k3/k4 **四 AI 国 Well=1+AI 桶=12**（补井后全模板池覆盖）=True
  - P2' 行为：**霜岩国（SnowRock，k2）farmStorage 峰=100 满仓+国库粮峰=379**（首轮 0 产→本轮爆产，k2 现象根治实锤）=True

## 二、件2：EventBus TimeDayChangedEvent 枚举异常修复

### 订阅者定位结论（任务书交付项）

- **病灶唯一=SatietySystem.OnNewDay L157 主结算遍历**。链：`SettleUnit` 饥饿扣血（L221 饱食≤satietyHurtThreshold）→`TakeDamage(satietyHurtPerDay)`（UnitController L548）→血尽 `Die()`（L563）→`UnitRegistry.Unregister(this)`（L672）从 `_aliveUnits` 移除——而 `GetAllUnits()` 返回**内部 List 引用**（UnitRegistry L47-50，非快照）→foreach 枚举中集合被改→抛 `InvalidOperationException`。
- 时点吻合：P1_run2 D27/D36/D38/D39 ×4=k2 断粮饿死窗口（SnowRock 无井→粮 0→饱食衰减→扣血致死）。

### 修复（订阅者侧，防御性零语义变更）

- SatietySystem L157 主遍历+L148 唤醒拉平遍历改**快照副本**（`new List<UnitController>(...)`）：死亡移除不影响遍历，快照中已死元素由既有 IsAlive 守卫跳过。+13/-2。
- EventBus 广播本体不动 ✓。

### 同模式病灶排查（一次清，全项目 20 处 foreach GetAllUnits）

| 判定 | 处 | 位置 |
|---|---|---|
| **病灶（已修）** | 2 | SatietySystem L157（主结算，实锤）/L148（唤醒拉平，顺手防御同改） |
| 安全（只读统计/查找） | 14 | SatietySystem L90/L109（均值统计）/PopulationSystem L103（CountAliveByKingdom）/HappinessSystem L134/L158（幸福只写值）/KingdomBrain L245（FindOwnWorker）/L315（FindRecruitableVagrant）/SiegeProduction L75/L129（计数）/AbstractEconomySettlement L100（冻结计数）/NPCBrain L629/WanderStimulus L154/ThroneAnchor L38/L54/TimeManager L349（HasActiveEnemies） |
| 安全（先收集后处理） | 4 | TrainingSystem L407（pool 收集，TryTrain 在遍历外）/L453/L543/SiegeProduction 机器生产（SpawnUnit 在计数遍历之外调用） |

**关键洞察**：本轮未爆的其余链（繁殖/训练/机器/招工）皆因 P1 死锁压制未触发——AI 人口再生批上线后「遍历中 Spawn」的同款雷区将激活（PopulationSystem 繁殖 SpawnUnit 在 TickChildGrowth/配对循环之外 ✓ 当前安全，但 per-kingdom 化施工时须维持「Spawn 不在 GetAllUnits 遍历体内」纪律——已写入 Gate 报告⑤）。

### 探针

- P3'：**33 游戏日等价 tick 压场**（15s/日×3x——日 tick 全链真实走通，断粮国饥饿扣血→饿死→移除链压场）→**InvalidOperationException 零命中**=True。
- k2 人口死亡时点路径回归：P2' 霜岩国粮 379 爆产=饿死根因（断粮）已除，死亡路径自然归零。

## 三、红线自查

| 项 | 兑现 |
|---|---|
| 零业务行为变更 | 件1 纯资产+件2 快照防御（结算语义逐位：进食/衰减/扣血/回血逻辑未动） |
| AI.Core/sim/champion/训练仓/RulerController 零触碰 | ✓ |
| 冒烟全绿才 commit | ALL PASS 3/3 后才出报告（commit 待验收代执） |
| git diff 自查 | 本批 3M+1 新增（两资产各+1/SatietySystem +13-2/HH76 容器新）；域外=策划端并行文件（3.1.2/3.1.3/美术规范）排除 |

## 四、验收请求

1. 两件施工与探针验收（P1'/P2'/P3' 全绿）。
2. 件2 订阅者定位结论认可（§二）。
3. 验收通过→commit 代执（两资产+SatieteySystem+容器）→**AI 人口再生批 Gate 报告已随本报告同串提交**（独立文件：执行端/AI人口再生批_Gate五面实施要点报告.md），请裁决后签发施工任务书。

---

## 策划裁决（策划端回写，裁决前保持空白）

> 策划端实盘复核（2026-09-05）：两资产 diff（各+1 `- Well`）+SatietySystem diff（L148/L157 双遍历快照副本+根因链注释完整）逐字吻合；探针数据自洽（P1' 四国井=1 桶=12/P2' 霜岩 farmStorage 峰 100+国库粮峰 379 vs 首轮 0 产=根治对照/P3' 33 日等价 tick 异常零命中）。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 两件施工与探针验收 | **✅ 成立（D540）** | 件1 纯资产两行+全模板池覆盖探针（P1' 四 AI 国+P2' k2 根治对照）；件2 快照防御零语义变更（进食/衰减/扣血逻辑未动）；**k2 现象正式销案**（SnowRock 无井→爆产，P5 派工域定性更正链闭合） |
| 件2 订阅者定位结论 | **✅ 认可+嘉奖**——SatietySystem.OnNewDay 主结算遍历唯一病灶（GetAllUnits 返回内部 List 引用→饿死移除→枚举失效）链路实锤；**同模式 20 处全查+「P1 死锁压制其余链、AI 人口批上线即雷区」关键洞察=超额交付**（该纪律已入 Gate 报告⑤=把一次性排查转化为施工纪律，方法论嘉奖） | 定位链（SettleUnit→TakeDamage→Die→Unregister L672）与 ×4 时点吻合；快照防御为标准修法 |
| commit 代执 | **✅ 代执**（两资产+SatietySystem+HH76 冒烟容器+HH 域文档+Gate 报告同串） | 构成=§三 git 自查口径 |
