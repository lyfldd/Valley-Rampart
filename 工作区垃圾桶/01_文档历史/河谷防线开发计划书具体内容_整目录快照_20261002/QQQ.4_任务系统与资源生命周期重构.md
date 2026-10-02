# QQQ.4 任务系统与资源生命周期重构

> 散乱细节优化文档 · 创建于 2026-08-07
> 本文档收集 5 个关联小需求（AI 行为智能/任务系统脆弱性/资源生命周期），配套执行清单见 QQQ.4_执行清单.md

## 概述

本批需求源于用户实测反馈：①农场作为最复杂的生产任务，无法同时"取水+耕作"；②流浪汉持续往主城走而非营地徘徊；③任务系统脆弱——流浪汉路过 2 秒就完成了玩家点击的采集任务；④NPC 闲逛时有时聚在主城、有时分散，规则不可控；⑤资源生命周期无"工人背包→仓库"环节，采集/搬运直接入国库。核心是任务系统的**职业过滤缺失**、**每源一任务的并发限制**、**流浪汉安全模型错误**、**资源流无中间载体**四大问题。

---

## 需求 1：农场双任务并行（取水+耕作）

### 问题重现
1. 建水井 + 农场 + 仓库，给农场配 2 名以上工人。
2. 观察：农场只有一个工人在耕作（或一个工人在挑水），另一个工人空闲游走，**取水与耕作从未同时发生**。
3. 水网水位持续下降时农场头顶反复冒"缺水"，产粮断断续续。

### 根本原因
[TaskScheduler.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L516) `HasAssignedTaskForSource(s)` 在 `Tick()` 第③步对**每个任务源**做整体去重：只要该源已有一个任务在派（无论类型），该源本 tick 全部跳过。而农场（Building）是唯一任务源：
- ② Production（耕作）：`producer != null && !HasWorkerAssigned`（有工人 Working 则停发）
- ④ WaterHaul（挑水）：`OutputResource==Food && 水网 Stored<20`

时序死锁：水网有水 → ④ 不满足 → 发 ② Production → 工人 Working 耕作 → 每秒耗 2 水 → 水位跌破 20 → ④ 满足，但 ② 因 `HasWorkerAssigned=true` 停发、且 `HasAssignedTaskForSource=true` 导致本 tick 跳过 → **WaterHaul 永远发不出** → 水网持续缺水 → 农场缺水停产。

### 解决方案（用户确认：农场双任务并行）
1. **调度器按"源 + 任务类型"去重**：`HasAssignedTaskForSource` 改为 `HasAssignedTaskForSourceType(source, type)`。Production 与 WaterHaul 属不同类型 → 农场可同时拥有"1 工人耕作 + 1 工人挑水"。
2. **挑水目标指向水井**：`ResolveDest` 的 `KingdomDestType.WaterNetwork` 分支改为查询**最近 Well 建筑**位置（无水井回退源坐标），不再指向 `WaterNetwork.transform`（Singleton 自动创建位置恒 (0,0)，工人会走到原点挑水——现状 bug）。完成动作仍 `AddWater(waterCarryAmount)` 入网（水是隐藏全局资源，无需真走到水网）。
3. **防呆**：WaterHaul 完成后水网仍可能 <20 → 下 tick 自然重发，无需额外状态。

### 影响面
- [TaskScheduler.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L516)：`HasAssignedTaskForSource` → 按类型去重；`ResolveDest` WaterNetwork 分支 → 最近水井
- Building.cs ④ 挑水条件不变（`producer.OutputResource==Food && WaterNetwork.Stored<waterThreshold`）
- 其他建筑不受影响（非农场无 ④，行为与现状一致）

### 验收
- 农场配 2 工人 + 水网缺水：同时看到 1 人耕作、1 人走向水井挑水
- 水网水位 <20 时农场仍能产粮（挑水补充）；水位充足时只耕作
- 工人挑水目标 = 水井坐标（非 (0,0)）

---

## 需求 2：任务职业过滤（仅工人可执行任务）

### 问题重现
1. 点击一个木头堆 → 采集。
2. 恰好 2 个流浪汉路过（采集点旁）→ 流浪汉被派发 Gather 任务 → 走到资源点 2 秒 → 完成采集 → 玩家资源增加但**工人没参与**。
3. 玩家："任务系统好脆弱，流浪汉路过就把建筑发布的任务做完了。"

### 根本原因
[TaskScheduler.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L185) `Tick()` 第②步收集空闲 NPC 候选**无职业过滤**：`IsAlive && IsIdleForTask && npcId!=0 && 未占用` 即入候选。流浪汉（Occupation.Vagrant）、居民（Resident）、君主（Ruler）都可能被派发生产/采集/搬运任务。派发策略是"距离最近优先"（DR-17），资源点旁的流浪汉天然最近 → 被选中。

### 解决方案（用户确认：只有工人能做任务）
在 `Tick()` 空闲 NPC 收集处加职业过滤：**仅 `uc.EffectiveOccupation == Occupation.Worker` 可被派发任务**。
- 流浪汉（Vagrant）、居民（Resident）、君主（Ruler）全部排除。
- Porter 资产虽存在（Human_Player_Porter.asset），但当前游戏只有工人职业、搬运并入工人职责，故不单独放行；未来启用搬运工职业时在过滤条件加 `|| == Occupation.Porter` 即可。

### 影响面
- [TaskScheduler.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L185)：Tick 候选过滤加 `EffectiveOccupation == Occupation.Worker`
- 行为变化：流浪汉/居民/君主永不被派任务 → 采集/生产/搬运/挑水全由工人执行

### 验收
- 资源点旁有流浪汉 + 远处有工人：点击采集 → 工人远道而来采集，流浪汉无视
- 流浪汉不执行任何生产/搬运任务（观察其只闲逛/营地里活动）

---

## 需求 3：流浪汉完全游离于王国体系

### 问题重现
1. 流浪汉营地生成（地图偏远区）。
2. 观察：流浪汉持续朝主城方向移动，到主城附近徘徊后再折返营地，反复往返。
3. 玩家："流浪汉为什么一直往主城跑？不是说了只会在营地徘徊吗？"

### 根本原因（两层）
1. **SafetyScore 模型错误**：`SafetyScore` 含 `KingdomDistanceFactor`（距主城越远分越低，1-0.02/格）。流浪汉营地偏远 → SafetyScore 常 < `wanderThreshold(0.4)` → [WanderStimulusProvider.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/Stimulus/WanderStimulusProvider.cs#L38) 第①步门控拦截 → 不 Wander → 交撤退/安全逻辑。
2. **撤退锚点指向城堡**：[RetreatToSafeAnchorBehavior.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/RetreatToSafeAnchorBehavior.cs#L28) 低分时 `WanderAnchorPool.PickSafeAnchor(selfPos)` 取最近安全锚点——城堡中心/预设点/建筑都在王国，流浪汉被导向主城。且需求 2 修复前流浪汉还会被派任务走向任务点。

### 解决方案（用户确认：完全游离）
1. **Wander 豁免门控**：[WanderStimulusProvider.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/Stimulus/WanderStimulusProvider.cs#L38) 流浪汉分支（`ctx.IsUnrecruitedVagrant`）上移到第①步门控**之前**——流浪汉无论 SafetyScore 高低都执行营地徘徊（HomePoint=营地为中心 1-3 格小半径随机点）。
2. **撤退目标=营地**：[NPCBrain.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/NPCBrain.cs#L622) `BuildBaseContext` 组装 `safeAnchor` 时特判：`unrecruitedVagrant ? homePoint（营地） : ResolveRetreatTarget(...)`，流浪汉低分撤退目标固定为营地，不走城堡锚点。
3. **不参与任务**（需求 2 职业过滤已覆盖）。
4. **招募后恢复正常**：`RecruitVagrant` 职业翻转 Resident → `IsUnrecruitedVagrant` 变 false → 走向王国被纳入人口（现有逻辑，保持）。

### 影响面
- WanderStimulusProvider.cs（分支顺序调整）
- NPCBrain.cs BuildBaseContext（safeAnchor 特判）
- SceneHomePointProvider.cs（已返回营地坐标，无需改）

### 验收
- 流浪汉始终在营地 ±3 格内活动，不再朝主城移动
- 流浪汉不执行任务（需求 2）
- 点击招募 → 流浪汉走回王国，行为恢复正常 NPC

---

## 需求 4：NPC 闲逛规则明确化

### 问题重现
1. 工人空闲时：有时全部聚在主城附近，有时分散在地图各处闲逛，无规律。
2. 玩家："NPC 闲逛到底怎么闲逛？为什么有时候聚在主城有时候不聚？"

### 根本原因
[WanderStimulusProvider.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/Stimulus/WanderStimulusProvider.cs#L60) 所有 NPC 抽同一个 `WanderAnchorPool`：池含城堡中心 + 城堡±2/4/6 格预设点（7 个）+ 全部活跃建筑 + 采集空地。`TryPickAnchor` 近邻优先 + 随机抖动 + 最近 N 不重抽：
- 出生/聚集在主城附近的工人，抽到的近邻锚点大概率是城堡预设点 → **聚主城**
- 分布在远处的工人抽到远处建筑/空地锚点 → **分散**
- "聚/散不稳定"是锚点池无职业区分 + 近邻优先的随机结果

### 解决方案
1. **锚点池按来源分类 + 职业偏好**：[WanderAnchorPool.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/WanderAnchorPool.cs) 锚点分三类：`CastleAnchor`（城堡中心+预设点）、`BuildingAnchor`（活跃建筑）、`FreeSpot`（采集空地）。`TryPickAnchor` 增加职业参数：**Worker 只抽 BuildingAnchor+FreeSpot（不抽城堡）**；居民/流浪汉抽全部。
2. **工人闲逛=工作半径内**：工人闲逛锚点限定其 HomePoint（主城）一定半径外的建筑/空地——视觉上"工人散布在生产设施附近"，不再聚主城。
3. **规则文档化（写入本需求）**：
   - 有任务（Worker）→ 任务点（优先级最高，刺激 1.0 压过闲逛 0.05）
   - 无任务且 SafetyScore ≥ 0.4 → 闲逛：Worker 抽建筑/空地锚点、居民抽城堡/建筑锚点、流浪汉营地徘徊（需求 3）
   - 无任务且 SafetyScore < 0.4 → 撤退：Worker/居民撤往最近安全锚点、流浪汉撤往营地
4. **锚点刷新节奏不变**（10-20s 随机间隔，间隔内复用防抖动）。

### 影响面
- WanderAnchorPool.cs：锚点分类 + TryPickAnchor 职业参数
- WanderStimulusProvider.cs：按 `ctx.Profession` 职业传参抽锚点
- 行为变化：工人空闲时分布在工作半径内（生产建筑/仓库/空地附近），不再扎堆主城

### 验收
- 4 名空闲工人：各自分散在不同建筑/空地锚点附近（无 2 人以上重叠站立）
- 主城城堡预设点不再被工人占用（居民才用）
- 有任务时工人优先任务点（闲逛不干扰）

---

## 需求 5：资源生命周期 + 工人内置仓库（搬运闭环）

### 问题重现
1. 工人采集木头堆 → 木头**直接进国库**（左上角资源立刻 +5），资源点消失。
2. 农场产出粮食 → 仓库/建筑存量 → 搬运任务 → `HarvestCarry()` **直接入国库**。
3. 玩家："资源应该先存到工人的仓库里，再由工人搬运到仓库存储，而不是直接进国库。资源生命周期不完整。"

### 根本原因
资源流缺少"中间载体"：
- [TaskScheduler.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L449) Gather 完成 → `RulerController.ModifyResource(...)` 直接入国库 + 资源点销毁
- [TaskScheduler.cs](file:///d:/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L439) Transport 完成 → `StorageComponent.HarvestCarry()` 直接入国库（destType=NearestWarehouse 只解析了路径点，完成动作却跳过仓库直接入国库）
- 无"工人背包"概念：资源从产出到国库之间没有可观察、可中断、可归属的中间态

### 解决方案（用户确认：完整生命周期）
引入 **WorkerInventory（工人背包）**，资源流改为：
`采集/生产 → 工人背包 → 搬运 → 仓库(StorageComponent) → 玩家收取入国库`

#### 5.1 新增 WorkerInventory 组件
挂载 Worker prefab（UnitController 同级）：
- 字段：`ResourceType carriedType`、`int carriedAmount`、`int carryCapacity`（= `ResourceCarryConfig.GetCarryAmount(type)`，木/石/矿=10、粮=20、水晶/火油=5，SO 数据驱动）
- 方法：`TryStore(type, amount)`（背包满拒绝）、`int UnloadAll()`（清空返回）、`bool IsFull`、`bool IsEmpty`
- 存档：UnitSaveData 升级 v5 存背包（carriedType/carriedAmount）

#### 5.2 采集（Gather）改为入背包
`ExecuteCompletion` Gather 分支：`RulerController.ModifyResource` 改为 `workerInventory.TryStore(resourceType, amount)`；背包满则余量**暂不产出**（该资源点下次采集补上——改为资源点不立即销毁，改为"采空"状态待工人清空背包后重采？简化：本次 amount 全入背包，若背包满则多出部分直接入国库兜底）。
资源点销毁时机不变（采集完成即销毁）。

#### 5.3 搬运（Transport）重做——两段式
- **第一段 Working at source**：`HarvestCarry()` 改为入**工人背包**（`inventory.TryStore`），不再入国库
- **第二段 CarryingToDest → Unloading**：背包非空 → 工人移动至 dest（仓库/国库）→ 到达后 `storage.Add(storedAmount)` 或 `RulerController.ModifyResource` 入国库 → 清空背包 → Complete
- 状态机扩展：`Assigned→MovingToSource→Working→CarryingToDest→Unloading→Completed`（TaskScheduler `UpdateAssignedTasks` 增两个分支）
- dest 解析：`KingdomDestType.NearestWarehouse` 不变（找最近有空位的 StorageComponent）；仓库满则落国库

#### 5.4 仓库建筑
StorageComponent 已有（Harvest 收取入国库、OnStorageChanged 面板刷新）。仓库作为背包的卸货目标 + 玩家收取点。容量按等级缩放（已实现 `RefreshCapacity`）。

### 影响面
- 新增 `WorkerInventory.cs`（Assets/_Game/Systems/Unit/）
- UnitController.cs：背包存档 v5
- TaskScheduler.cs：Gather/Transport 完成动作改背包；状态机加 CarryingToDest/Unloading
- StorageComponent.cs：加 `Add(int)` 方法（仓库入货，满则拒/入国库兜底）
- 行为变化：采集后资源先入工人背包 → 工人搬去仓库 → 玩家点击仓库收取入国库

### 验收
- 点击采集 → 左上角资源**不立即增加**；工人背包图标/头顶提示"负重"；工人走回最近仓库 → 左上角资源增加
- 背包携带量符合 ResourceCarryConfig（木=10）
- 农场存量 ≥80% → 工人搬运入背包 → 仓库入货（非直接国库）
- 仓库被摧毁/满 → 兜底直接入国库，资源不丢失

---

## 需求汇总表

| # | 需求 | 类型 | 涉及文件 | 优先级 |
|---|------|------|---------|--------|
| 1 | 农场双任务并行（取水+耕作） | 设计调整 | TaskScheduler.cs | P0 |
| 2 | 任务职业过滤（仅工人） | bug修复 | TaskScheduler.cs | P0 |
| 3 | 流浪汉完全游离王国体系 | bug修复 | WanderStimulusProvider.cs / NPCBrain.cs | P0 |
| 4 | NPC 闲逛规则明确化 | 设计调整 | WanderAnchorPool.cs / WanderStimulusProvider.cs | P1 |
| 5 | 资源生命周期 + 工人内置仓库 | 新功能 | WorkerInventory.cs / TaskScheduler.cs / StorageComponent.cs / UnitController.cs | P1 |
