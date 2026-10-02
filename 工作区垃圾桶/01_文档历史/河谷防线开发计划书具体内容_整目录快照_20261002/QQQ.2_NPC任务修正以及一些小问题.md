# QQQ.2 NPC任务修正以及一些小问题

> 散乱细节优化文档 · 创建于 2026-08-07
> 本文档收集 10 个散乱小需求，配套执行清单见 QQQ.2_执行清单.md
> 核心是需求10（NPC 任务调度系统，含生产/搬水/搬运/采集/一次性资源点生命周期），其余为对话/UI/水井/流浪汉/空闲分布等小问题
> 2026-08-07 二次审查更新：新增 DR-18~DR-21（耗水时序/在场判定窗口/QQQ.3粒度/SafetyScore 4个数值）；SafetyScore 公式数值落地；§需求9 ConsumeWater(1)→(2)；§需求10.2 农场重复行加注；§10.4 提及 enum TaskState

## 概述

本批源于玩家实测反馈：NPC 大脑只战斗/闲逛，完全不会执行任务（生产/采集/搬运都没跑通）；农场无工人也能产粮（15粮/秒）但粮食没到国库；NPC 聚在城堡；流浪汉也往王国走；UI 里装备厂应移除、训练场 UI 应改、仓库应显示各资源；水井需二级主城不合理。核心矛盾是**任务调度源未实现**（生产/采集任务源是 P1 占位），`WorkerTask` 状态机是死代码，生产是纯计时器与工人解耦。

---

## 需求 1：NPC 对话交互优化（点击重叠 + 闲逛自动说话）

### 问题/现状

- 点击 NPC 太快时，前一个头顶气泡（2.5s）未消失，新气泡就叠上来，交互混乱。
- `OverheadSpeech.Show`（IClickInteractable.cs:85-111）每次点击 `new GameObject` + TextMesh，无队列/复用；快速连点 → 多个气泡叠一起。
- 自动说话机制不存在：全代码库 `OverheadSpeech.Show` 仅 7 处调用，全来自点击交互（UnitController.cs:1060-1101）和招募反馈/训练空提示。NPC 闲逛时从不自言自语。

### 方案

**核心设计：复用覆盖（DR-6）+ 多 NPC 数量管控（DR-10）。**

1. **气泡复用覆盖**（DR-6）：`OverheadSpeech` 改为每单位 1 个可复用的头顶气泡（挂到 unit.transform 下），新说话时先销毁/复用旧气泡再显示新的，不叠加。连点丢话可接受。
2. **闲逛自动说话 + 多 NPC 数量管控**（DR-10）：
   - **单 NPC 触发条件**：IsIdleForTask（无任务、无威胁、非战斗、非 Caution 态）+ SafetyScore > 0.6 时，加自动说话计时器（15~30s 随机），到点调 `PickTalkLine()` 冒泡；冷却 8s 防刷屏。
   - **多 NPC 数量管控**（解决 100-200 人同时冒字一片文字海）：
     - 视野裁剪：相机视野外的 NPC 不冒字
     - 同时上限：视野内同时存在的气泡 ≤ 6 个，超出的进入队列轮转（按"该 NPC 距上次说话时间"排序，最久没说的优先）
     - 队列轮转：当某气泡消失（2.5s 后），队列下一个补上
   - 用 `WanderStimulusProvider` 或 NPCBrain 空闲分支触发，不打断战斗/任务。

### 影响面

- `IClickInteractable.cs`（OverheadSpeech 改造为复用覆盖）
- `UnitController.cs`（暴露/PickTalkLine 复用 + 自动说话计时器）
- `NPCBrain.cs`（空闲分支加自动说话触发 + SafetyScore 检查）
- 新 `OverheadSpeechManager`（视野裁剪 + 同时上限 6 + 轮转队列）

### 验收

- 快速连点 NPC，气泡只显示一条（新的覆盖旧的），不叠加
- NPC 闲逛时每隔 15-30s 随机冒一句对话，战斗/任务/Caution 态不说话
- 100-200 NPC 场景中，视野内同时气泡数 ≤ 6，不出现"一片文字海"
- 相机视野外的 NPC 不冒字
- 对话仍 2.5s 自动消失

---

## 需求 2：彻底移除装备厂（装备已并入训练）

### 问题/现状

3.5 已决策"装备系统全量移除、装备并入训练"（3.5_王国经营体系.md §八 L219），但代码里装备厂还残留：
- `BuildingPanel.cs:392` 若 `def.id=="Armory"` 打开 `EquipmentPanel`
- `EquipmentPanel.cs` 装备厂装备管理面板（UIDocument）
- `EquipmentDef.cs` / `EquipmentSystem.cs` 装备数据与穿戴逻辑

玩家在 UI 看到"装备厂"和"训练场"并存，与"已合并"的设计矛盾。

### 方案

1. 从建造菜单/`BuildingMappingTable` 移除 `Armory` 建筑（不再可建造/不再出现在菜单）。
2. 删除 `EquipmentPanel` 装备管理入口（BuildingPanel.cs:391-392,482）与面板使用。
3. `EquipmentDef.cs`/`EquipmentSystem.cs`/`EquipmentPanel.cs` + 对应 `.asset` 文件：**彻底移除代码与资产**。grep 确认 `UnitController`/`BuildingFactory`/`SaveSystem`/`UnitData` 无 `equipId`/`EquipmentDef` 残留引用；存档兼容性兜底（旧存档含 equipId 字段时迁移或忽略，不抛异常）。决策见「§决策记录 DR-1」。

### 影响面

- `BuildingPanel.cs`、`EquipmentPanel.cs`、`BuildingMappingTable`（Armory 映射）、`EquipmentDef.cs`、`EquipmentSystem.cs`
- 可能涉及 `UnitController` 的 equipId 穿戴逻辑（需 grep 确认）

### 验收

- UI 建造菜单不再出现"装备厂"，点击无装备面板
- 训练行为不受影响（训练直接出职业，含装备能力）
- 编译 0 error

---

## 需求 3：训练场 UI 改造（可训练人数 + 训练队列 + 正在训练）

### 问题/现状

`TrainingPanel`（UI Toolkit）当前把"可训练的 NPC（居民）"逐个列出来，玩家要点具体某个 NPC。玩家要的是**不列出具体 NPC**，而是显示：
- 该训练建筑**可训练的人数**（还有多少居民可训）
- 该建筑的**训练队列**（正在/排队转的职业）
- 若有**训练时间**，显示**正在训练的人数**（正在进行中的数量）

### 方案

1. `TrainingPanel` 改为显示：
   - **可训练人数**：王国空闲居民数（`UnitRegistry`/`PopulationSystem` 中 `Resident` 且可训的数量）
   - **训练队列**：显示「职业名 × 数量」清单（如 `工人×2 / 士兵×1`），不列具体 NPC 实体
   - **正在训练人数**：若 `TrainingConfig` 有训练时长，显示正在进行的训练数（`TrainingSystem` 队列里 `inProgress` 的数量与剩余时间）
2. 点击"训练"**弹出职业选择**（建筑支持的职业按钮），从可训练居民池自动取一个入队，入队参数携带职业；队满则不可点（置灰）。
3. 训练时长：若配置有 `trainDuration`，队列显示进度/剩余时间；无时长则仍是天级结算（天数驱动）。
4. **TrainingConfig 暴露 `supportedOccupations`**：每类训练建筑可训练的职业白名单（如兵营→士兵/弓箭手，学院→法师）。决策见「§决策记录 DR-3」。

### 影响面

- `TrainingPanel.cs`（UI 重构）
- `TrainingSystem.cs`（暴露队列/进行中数量/从池取居民入队）
- `TrainingConfig.cs`（若加训练时长字段）

### 验收

- 训练面板显示"可训练人数 N"、"队列 [职业清单]"、"正在训练 M"
- 不列出具体 NPC；点训练从居民池自动入队
- 队满/无可训居民时按钮置灰

---

## 需求 4：NPC 空闲分布（不聚城堡）

### 问题/现状

空闲 NPC 的漫游中心 = `ctx.HomePoint` = 王国锚点（城堡中心，需求2 修复后）。`WanderStimulusProvider`（WanderStimulusProvider.cs:26）把 `_stimulus.Position = ctx.HomePoint`。无城墙/无任务时，所有空闲 NPC 都朝城堡漫游 → 聚成一团在城堡。

### 方案

**核心设计：SafetyScore 完全合并 + 动态多锚点池 WanderAnchorPool + 新增撤退行为谱系。** 决策见「§决策记录 DR-5」。

#### 4.1 统一 SafetyScore（合并 SafetyStimulus/ThreatHysteresis/Caution）

```
SafetyScore = baseSafety(0.5)
           + wallFactor × 0.3                           // 在城墙内（DR-21）
           + armyFactor × 0.2                           // 8格内≥3友军（复用 protectionUpThresholds[0]=3，DR-21）
           + kingdomDistanceFactor                      // max(0.1, 0.02/格衰减)，最低0.1不归零（DR-21）
           - threatFactor × 0.8 × rawThreatHeat         // heat=0-1（FactorContext.ThreatFactor），满威胁扣0.8（DR-21）
           - nightFactor × 0.1                          // 夜晚

// 三层梯度（DR-21）：
//   Score < 0.4        → 不 Wander + 触发 RetreatToSafeAnchor
//   0.4 ≤ Score < 0.6  → 可 Wander，半径小（4格下限）
//   Score ≥ 0.6        → 大半径 Wander（8格上限）+ 可自动说话
//   满威胁(heat=1)时最高 Score = 1.0 - 0.8 = 0.2 < 0.4 → 不 Wander ✓
```

**SafetyScore 决定 3 件事**：
- 是否允许 Wander（Score ≥ wanderThreshold=0.4 才 Wander，DR-21）
- Wander 半径（Score 越高半径越大，按职业 4-8 格上下限 clamp）
- SafetyStimulus 强度（Score 越低回城拉力越强，扩展 SafetyStimulusProvider 公式）

#### 4.2 动态多锚点池 WanderAnchorPool（取代硬编码锚点）

不用"工人靠工作建筑"硬编码。改为：

```
WanderAnchorPool（王国内部维护）
├── 城堡中心 + 周边预设点（开局生成）
├── 已建成建筑附近（建筑注册时自动加入）
├── 资源点采集后空地（采集生命周期结束时加入）
├── 道路节点/广场（地图生成时加入）
└── 军队驻扎点（编队 DispatchOrders 后临时加入）
```

**NPC Wander 流程**：
1. 每 10-20s 随机间隔，从池中按"安全系数 + 距离"抽取一个新锚点
2. 池过滤：只保留 NPC 当前 SafetyScore ≥ 阈值的点
3. 抽到锚点 → 走过去 → 到达后停留 wanderStayTime(1.5s) → 再抽下一个
4. 同一锚点池多人抽：避免重复（最近 N 个不重抽）

#### 4.3 城墙判定（多段城墙/无城墙都支持）

- 多段城墙：检测 NPC 是否在任一段城墙的 footprint 内（GridSystem 已有 occupant 查询）
- 无城墙：wallFactor=0，靠 kingdomDistance + armyFactor + threatFactor 判定安全系数
- 城墙被摧毁：对应段落不再贡献 wallFactor
- 城墙**不挡感知**（决策已定 B）：NPC 仍能感知城外敌人，但城内 wallFactor 提高安全系数

#### 4.4 撤退行为谱系 RetreatToSafeAnchor（解决"边界遇敌不知往内走"）

```
当 SafetyScore 突降（如遇敌）：
1. NPC 不直接回城堡中心
2. 改为往"最近的安全锚点"撤退
   - 从 WanderAnchorPool 中筛 Score 最高的点
   - 通常是王国内部某个建筑附近/广场
3. 到达安全锚点后，若威胁解除→重新 Wander；若仍受威胁→继续往内撤
```

#### 4.5 模拟器验证场景

新增"闲逛遇敌撤退"验证场景，跑 SafetyScore 调参：
- 场景：边界有敌，王国内部 NPC 闲逛
- 验证：NPC 遇敌后正确往最近安全锚点撤退，不卡在敌人和城堡之间
- 调参：wanderThreshold / wallFactor / armyFactor / kingdomDistanceFactor

### 影响面

- `WanderStimulusProvider.cs`（重写：从 WanderAnchorPool 抽锚点，SafetyScore 过滤）
- `SafetyStimulusProvider.cs`（合并 SafetyScore 公式）
- `ThreatHysteresisComponent.cs`（合并到 SafetyScore）
- `HitCooldownStateMachine.cs`（Caution 态 HoldPosition 改为读 SafetyScore）
- 新 `WanderAnchorPool`（动态锚点池）
- 新 `RetreatToSafeAnchorBehavior`（撤退行为谱系）
- `BehaviorExecutor.cs`（新增 Retreat 分支）
- `NPCBrain.cs`（SafetyScore 计算注入）
- `GridSystem.cs`（暴露 InsideWall 查询）
- 模拟器：新增"闲逛遇敌撤退"验证场景

### 验收

- 空闲 NPC 分散在王国内部各处（动态锚点池抽取），不再聚在城堡一个点
- NPC 有任务时仍正常去任务点
- 城墙建成后 NPC 在城墙内闲逛；城墙被摧毁后 NPC 退缩到内层安全锚点
- NPC 在边界遇敌时往最近安全锚点撤退，不卡在敌人和城堡之间
- 无城墙时 NPC 仍能在王国锚点附近闲逛（靠 kingdomDistance + armyFactor 判定安全）

---

## 需求 5：生产速率与工人依赖（农场 15 粮/秒 过高 + 无工人也产）

### 问题/现状

- `ProducerComponent.Tick`（ProducerComponent.cs:110）纯计时累加到 `StorageComponent`，**无工人在场检查** → 农场无工人也产。
- `farm.asset` `producer.rate:15 capacity:100` → 15 粮/秒，过高。
- 玩家观察到"粮食没涨"：因为产出进 `StorageComponent`，搬运/入国库链路未跑通（见需求10）。

### 方案

1. **生产需工人**：`ProducerComponent` 暴露 `HasWorkerAssigned`，**基于 TaskScheduler 派发记录判定**（NPC.currentTask 指向本 Producer 的 Production 任务即视为在场），不写空间查询。NPC 中断/死亡/被招募走时调度器自动清除指派，避免建筑无限判定"有人"。无工人不产。决策见「§决策记录 DR-4」。兜底机制见 QQQ.3。
2. **速率校准**（DR-13）：`farm.asset` 的 `rate` 从 15 校准到 **2 粮/秒**，配合"需要工人+水"的节奏。
3. 产出仍进 `StorageComponent`，由需求10 的搬运任务送到仓库/国库。

### 影响面

- `ProducerComponent.cs`（加工人在场检查 HasWorkerAssigned）
- `TaskScheduler.cs`（维护 NPC↔Task 映射，NPC 异常退出时清除指派）
- `farm.asset`（rate: 15 → 2）
- 配合需求10 生产任务

### 验收

- 无工人时农场不产粮；有工人执行生产任务且水够时才产
- 产出速率符合设定（不再是 15/秒）
- 粮食能经搬运入国库（配合需求10）

---

## 需求 6：流浪汉不往王国走（空闲中心=营地，已招募才走回）

### 问题/现状

玩家观察到未招募的流浪汉也往王国走。根因：空闲 NPC 漫游中心 = `ctx.HomePoint` = 王国锚点（见需求4），流浪汉出生在营地，但其 HomePoint 也是王国锚点 → 未招募也朝城堡走。`VagrantCampSystem` 的走回任务只在 `RecruitVagrant`（花粮、改职业、标记）后注入（VagrantCampSystem.cs:101-109），与"未招募流浪汉走王国"矛盾（代码上没有，但 HomePoint 统一造成）。

### 方案

**核心设计：按"是否已招募"标志判定 HomePoint（DR-7）。**

1. **SceneHomePointProvider 检查 unit.IsVagrantRecruited**（DR-7）：
   - 未招募（IsVagrantRecruited=false）→ 返回出生营地坐标
   - 已招募（IsVagrantRecruited=true）→ 返回王国锚点（现有逻辑）
2. **VagrantCampSystem 记录出生营地坐标**：流浪汉 SpawnVagrantNear 时把 campPos 写入 unit.BirthCampPos（新字段）。
3. 招募后（`RecruitVagrant`）才注入"走回王国"任务（现有逻辑保留），同时清旧任务刺激（兜底见 QQQ.3）。
4. 配合需求4：所有空闲 NPC 用 WanderAnchorPool 抽取闲逛点，流浪汉未招募时锚点池=营地周边。

### 影响面

- `SceneHomePointProvider.cs`（按 IsVagrantRecruited 返回 HomePoint）
- `VagrantCampSystem.cs`（SpawnVagrantNear 写入 BirthCampPos 字段）
- `UnitController.cs`/`UnitData.cs`（新增 BirthCampPos 字段，持久化）
- `NPCBrain.cs`（HomePoint 解析）

### 验收

- 未招募流浪汉在营地附近游荡，不朝王国走
- 花粮招募后，该流浪汉走回王国并入册为居民
- 招募后旧任务刺激被清理（兜底见 QQQ.3）

---

## 需求 7：全局仓库仓储面板

### 问题/现状

当前只有单建筑面板显示该建筑自己的 `存储 {storedAmount}/{capacity}`（BuildingPanel.cs:362-382），**没有按资源类型汇总各仓库存储量的全局面板**。玩家建了仓库看不到各资源仓储情况，也看不到粮食是否入仓。

### 方案

**核心设计：顶部按钮入口 + 订阅刷新（DR-15）。**

1. 新增全局仓储面板 `WarehousePanel`（UI Toolkit）：汇总所有带 `StorageComponent` 的建筑，按资源类型（粮/木/石/矿…）显示总额外存储/容量。
2. 与国库（`RulerController` 资源）区分：国库是主资源，仓储面板显示各仓库建筑里的存量分布。
3. **入口**（DR-15）：顶部 HUD 加"仓库"按钮打开（不依赖点击仓库建筑）。
4. **刷新机制**（DR-15）：StorageComponent 暴露 `OnStorageChanged` 事件，WarehousePanel 订阅事件实时刷新；面板关闭时退订避免泄漏。

### 影响面

- 新 `WarehousePanel`（UI Toolkit）
- 新 `WarehousePanelButton`（顶部 HUD 入口）
- `StorageComponent.cs`（暴露 OnStorageChanged 事件 + 遍历所有仓库的接口）

### 验收

- 顶部"仓库"按钮打开面板
- 建多个仓库后，面板显示各资源类型的仓库总量/容量
- 粮食经搬运入仓库后，面板数值实时变化
- 面板关闭时事件退订（兜底见 QQQ.3）

---

## 需求 8：水井降至一级主城

### 问题/现状

水井需二级主城：`Module_Livelihood.asset` tier2 `requiredCastleLevel:2`，`unlockBuildings:[Well]`，经 `KingdomManager.IsBuildingUnlocked`（KingdomManager.cs:191-202）→ `CastleUnlockTable` 判定。玩家认为不合理（若农场生产需水，则一级就该有水井）。

### 方案

1. 把 `Well` 从 `Module_Livelihood` tier2 移到 tier1（`requiredCastleLevel:1`），一级主城即可建水井。
2. 同步 `Module_Livelihood.asset` 的 `unlockBuildings` 与 `CastleUnlockTable`。

### 影响面

- `Module_Livelihood.asset`、`CastleUnlockTable.cs`（或对应 SO）

### 验收

- 一级主城即可建造水井
- 水井产出水（内部水网，见需求9/10）

---

## 需求 9：农场生产条件（工人 + 全局水网）

### 问题/现状

农场现在无工人、无水也能产（ProducerComponent 纯计时）。玩家要求：农场生产需要"工人去农场执行生产任务" + "农场内部存储的水够"；水井是供水源头；水是内部循环资源，不显示在 UI。

### 方案

**核心设计：单例 MonoBehaviour WaterNetwork（DR-8）+ fixed 耗水+缺水停产+UI 提示（DR-9）。**

1. **全局隐藏水网** `WaterNetwork`（DR-8）：
   - 类型：单例 MonoBehaviour（挂场景 GameObject，可被 SaveManager 持久化）
   - 容量上限：100 水（超出 100 水井停产，避免浪费）
   - 接口：`AddWater(amount)`（水井产水入网）、`ConsumeWater(amount)→bool`（农场消耗，返回是否够）、`Stored`（当前存量）
   - UI 不显示水（隐藏资源）
2. **水井产水**（DR-14）：well.asset `rate=4 水/秒`，经 ProducerComponent 产出调 `WaterNetwork.Instance.AddWater(4)`。1 口井供 2 农场（农场 2 粮/秒耗 2 水）。
3. **农场生产条件**：`ProducerComponent`（农场）产出需同时满足——
   - `WaterNetwork.Instance.ConsumeWater(2)` 返回 true（每次产出耗 2 水，DR-9 + DR-18：1秒1次产出事件，每次+2粮耗2水）
   - `HasWorkerAssigned == true`（DR-4，仅 Working 算在场，DR-19）
   - 缺水时（ConsumeWater 返回 false）：停产 + 农场头顶冒"缺水"图标提示
4. 水井本身不产"可显示资源"，只充水网；农场不直接依赖"水井建筑"，而是依赖"水网有水"。

### 影响面

- 新 `WaterNetwork`（单例 MonoBehaviour + ISaveable，容量 100）
- `ProducerComponent.cs`（农场加"水网+工人在场+缺水提示"条件；Tick 改 1s/tick 离散事件 DR-18；HasWorkerAssigned 仅 Working 算在场 DR-19）
- `Well.asset`（rate=4）、`farm.asset`（rate=2）
- 配合需求10 的搬水任务（农场水网不足→工人从水井搬水充网）

### 验收

- 有水网+工人时农场产粮（2 粮/秒，耗 2 水/秒）
- 水网干了（ConsumeWater 返回 false）农场停产 + 头顶冒"缺水"图标
- 无工人不产（DR-4）
- 水井产水入水网，UI 不显示水
- 水网容量上限 100，超出后水井停产
- 一级主城可建水井（需求8）

---

## 需求 10：完整任务调度系统（核心，含生产/搬水/搬运/采集/一次性资源点生命周期）

### 问题/现状

- 调度中心 `ScheduleCenterStub` 只派"搬运+战争机器乘员"（ScheduleCenterStub.cs:54-68），注释明示"生产/建造/养殖/挑水任务源在 P1 任务调度扩展中接入"（L63-65）。
- `WorkerTask` 状态机（WorkerTask.cs）是**死代码**：正式调度从不调用 `WorkerTask.Assign`，唯一调用是调试入口 `AIDebugSpawnController.AssignKingdomTask`（AIDebugSpawnController.cs:342-347）。
- 生产是纯计时器（ProducerComponent），与工人解耦；搬运走 `TaskStimulus`→`HarvestCarry` 直接入国库（无仓库中转）。
- 一次性资源点（WoodPile/StonePile/OreVein）**无采集**：没有"君主点击→工人去采→资源入账→资源点消失"的链路，采集后贴图还留在原地。
- NPC 只战斗/闲逛（威胁刺激 `ThreatStimulus` 抢占，任务刺激弱），无任务可做。

### 方案（核心设计）

#### 10.1 任务抽象（不硬编码）

新增通用 `KingdomTask`：

```
KingdomTask {
    KingdomTaskType type;        // Production / Transport / WaterHaul / Gather / Build / Repair / Ranch
    ITaskSource source;          // 来源对象（建筑/资源点），提供 sourcePos
    DestType destType;           // None / Treasury / NearestWarehouse / WaterNetwork / SpecificBuilding
    Vector2 destPos;             // 派发时由调度器动态解析，非硬编码
    object args;                 // 任务参数（产出量、目标物等）
    float intensity;             // 刺激强度
}
enum KingdomDestType { None, Treasury, NearestWarehouse, WaterNetwork, SpecificBuilding }
```

- 建筑/资源点实现 `ITaskSource` 接口：`bool TryAdvertiseTask(out KingdomTask)`，按需"声明"任务。
- **搬运目的地不硬编码**：`Transport` 任务只带 `destType=NearestWarehouse`，调度器派发时实时解析终点 = 找 `StorageComponent` 中最近的可用仓库（capacity > stored），找不到则 `Treasury`（国库）。同理 `WaterHaul` 终点 = `WaterNetwork`。

#### 10.2 任务发布源（建筑声明需求）

| 任务源 | 发布条件 | destType |
|---|---|---|
| 生产建筑（农场/伐木/采石/矿洞） | 需要工人且条件满足（农场需水网有水） | None（原地劳作） |
| 存储建筑 | 存量 ≥ 阈值（如 capacity×80%） | NearestWarehouse / Treasury |
| 农场（注：农场既是生产建筑又是水网消费者，故双行；其他生产建筑不耗水） | 水网库存低于阈值 | WaterNetwork（先 WaterHaul 到水网） |
| 一次性资源点（君主点击） | 被点击采集 | Treasury |
| 持续性资源点（树/矿脉） | 有可采资源 | Treasury |

#### 10.3 任务调度器（扩展 ScheduleCenterStub）

- **调度频率**（DR-17）：1s/tick（与现有 ScheduleCenterStub 一致）
- **ITaskSource 注册机制**（DR-16）：建筑生命周期挂钩——`ITaskSource` 扩展 `OnRegister/OnUnregister`；`Building.OnSpawn` 调 `TaskScheduler.Register(this)`；`Building.Die` 调 `Unregister`。调度器维护注册表，避免每帧 `FindObjectsByType`。
- 每 tick 流程：
  1. 遍历注册表所有 `ITaskSource` 收集"可发布任务"列表。
  2. 遍历空闲 NPC（`IsIdleForTask`），按 **优先级（生产 > 搬运 > 搬水 > 采集）+ 距离升序**（DR-17）分配任务。
  3. 派发时**动态解析终点**（destPos per DestType）。
  4. **任务幂等**（DR-17）：靠"任务被 NPC.currentTask 引用即占用"，不维护"已派发"标记；NPC.currentTask != null 即视为该任务被占用。
  5. 被威胁/战斗打断时，任务可重派（沿用现有 `TaskStimulus` 挂起/恢复机制，兜底见 QQQ.3）。

#### 10.4 任务执行（接入 WorkerTask 状态机）

- **WorkerTask 内化为 TaskStimulus 工厂**（不做独立状态机，无双轨）：调度器构造 `TaskStimulus`（带 source/destPos/type）扔给 NPCBrain，由现有 `BehaviorExecutor` 消费。原 `WorkerTask.cs` 退化为 TaskStimulus 构造器/数据类。决策见「§决策记录 DR-2」。
- 任务态由 NPC 自身维护（`npc.currentTask` 引用 KingdomTask，状态走 `enum TaskState { Assigned, MovingToSource, Working, MovingToDest, Completed, Abandoned }`，新增枚举定义见执行清单 T20）。
- **ThreatStimulus 抢占时任务挂起**：威胁解除后任务可恢复（沿用现有 TaskStimulus 挂起/恢复机制）。
- **占位动作态**：`Working` 阶段 NPC 朝向任务点 + 停留 + 头顶冒"劳作"提示（占位，视觉动画后置）。
- 完成：`Production`→触发 ProducerComponent 当次产出；`Transport`→HarvestCarry 到终点；`WaterHaul`→充入水网；`Gather`→资源入国库 + 触发生命周期。

#### 10.5 水循环（全局隐藏水网）

- 新增 `WaterNetwork`（DR-8：单例 MonoBehaviour + ISaveable，容量 100，UI 隐藏）：水井 `ProducerComponent` 产水入网（rate=4 水/秒，DR-14）；农场生产消耗网内水（每次产出耗 1 水，DR-9）。
- `WaterHaul` 任务：农场水网库存低于阈值（如 20）→ 发布 `WaterHaul` 任务 → 工人从水井取水 → 充入水网。
- 水网容量达 100 时水井 ProducerComponent 停产（避免浪费）。

#### 10.6 一次性资源点采集生命周期

- 君主点击一次性资源点（`Building`，isConsumable=true）→ 弹**确认 UI**（DR-11："采集将花费 X 秒，派 1 个工人？"）→ 确认后发布 `Gather` 任务。
- 工人执行（耗时按资源量动态，DR-11：WoodPile 2s / StonePile 4s / OreVein 8s）→ 资源入国库 → 触发生命周期：
  1. 释放网格占用（`GridSystem.Free`）
  2. 从 `BuildingRegistry` 移除
  3. 走对象池 `BuildingFactory.Release/Despawn`（DR-11，不直接 Destroy，与 UnitFactory 一致）
- 多个资源点可**并行采集**（每点 1 NPC）。
- 采集后不留原地贴图。

#### 10.7 生产条件接入

- `ProducerComponent`：`Production` 建筑需"有工人在场执行生产任务"才产出（农场还额外需水网有水）。
- 移除"无工人也产"。

### 影响面

- 新 `KingdomTask`/`ITaskSource`/`WaterNetwork`/`TaskScheduler`（或扩展 `ScheduleCenterStub`）
- `WorkerTask.cs`（接入正式调度 + 占位动作态）
- `ProducerComponent.cs`（加工人在场 + 农场水条件）
- 农场/伐木/采石/矿洞/水井 def & 资源点（`Building`/`BuildingFactory`，采集生命周期）
- `NPCBrain.cs`（任务消费/优先级/空闲分布协调）
- `Building.cs`（采集/搬运接口）

### 验收

- 农场有工人+水网才产；无工人不产（需求9）
- 工人会去伐木/采石/矿洞执行生产任务并产出
- 君主点一次性资源点 → 工人去采 → 资源入国库 → 资源点消失（不留贴图）
- 农场水网不足 → 工人从水井搬水充网
- 搬运任务把产出就近搬到仓库，无仓库则入国库
- NPC 空闲时不再只聚城堡（配合需求4），有任务时去执行任务，遇敌才战斗

---

## 需求汇总表

| # | 需求 | 类型 | 涉及文件 | 优先级 |
|---|------|------|---------|--------|
| 1 | NPC 对话优化（点击重叠 + 闲逛自动说话） | 体验优化 | IClickInteractable.cs, UnitController.cs, NPCBrain.cs | P2 |
| 2 | 彻底移除装备厂（装备并入训练） | 清理 | BuildingPanel.cs, EquipmentPanel.cs, MappingTable, EquipmentPrivate | P1 |
| 3 | 训练场 UI（可训练人数+队列+正在训练） | 体验优化 | TrainingPanel.cs, TrainingSystem.cs | P1 |
| 4 | NPC 空闲分布（不聚城堡） | 设计调整 | WanderStimulusProvider.cs, NPCBrain.cs | P1 |
| 5 | 生产需工人 + 速率校准 | bug修复 | ProducerComponent.cs, farm.asset | P0 |
| 6 | 流浪汉不往王国走（空闲中心=营地） | 设计调整 | SceneHomePointProvider.cs, VagrantCampSystem.cs | P1 |
| 7 | 全局仓库仓储面板 | 体验优化 | WarehousePanel, StorageComponent.cs | P1 |
| 8 | 水井降一级主城 | 配置调整 | Module_Livelihood.asset, CastleUnlockTable | P1 |
| 9 | 农场生产条件（工人+全局水网） | 设计调整 | WaterNetwork, ProducerComponent, farm.asset | P0 |
| 10 | 完整任务调度系统（生产/搬水/搬运/采集/一次性资源点生命周期） | 架构 | TaskScheduler, WorkerTask, ITaskSource, WaterNetwork, ProducerComponent, NPCBrain | P0 |

---

## 决策记录（2026-08-07 审查产出）

| 编号 | 决策点 | 选择 | 连锁影响 |
|------|--------|------|---------|
| DR-1 | T5 装备系统去留 | 彻底移除代码+资产 | grep 确认 UnitController/BuildingFactory/SaveSystem/UnitData 无 equipId 残留；存档兼容性兜底（旧存档字段忽略） |
| DR-2 | T18 WorkerTask 集成 | 内化为 TaskStimulus 工厂，不做独立状态机 | WorkerTask.cs 退化为 TaskStimulus 构造器；任务态由 NPC.currentTask 维护；ThreatStimulus 抢占沿用现有挂起/恢复 |
| DR-3 | T6/T7 训练 UI 粒度 | 队列显示「职业名 × 数量」，点击训练弹职业选择 | TrainingConfig 暴露 supportedOccupations；入队参数携带职业 |
| DR-4 | T9 工人在场判定 | 基于 TaskScheduler 派发记录判定 HasWorkerAssigned | ProducerComponent 不写空间查询；调度器需在 NPC 中断/死亡/被招募走时自动清除指派 |
| DR-5 | T8 NPC 空闲分布/安全范围 | SafetyScore 完全合并 + WanderAnchorPool 动态生成 + 新增 RetreatToSafeAnchor 谱系 + 加模拟器验证场景 | 重构 SafetyStimulus/ThreatHysteresis/Caution 为统一 SafetyScore；城墙内 wallFactor +X；NPC 从动态锚点池按 Score 抽取闲逛点；遇敌撤退到最近安全锚点而非城堡中心 |
| DR-6 | T1 气泡方案 | 复用覆盖 | 每单位 1 个头顶气泡，新说话先销毁旧气泡再显示新的；连点丢话可接受 |
| DR-7 | T11 流浪汉 HomePoint 判定字段 | 按"是否已招募"标志 | SceneHomePointProvider 检查 unit.IsVagrantRecruited：未招募→营地坐标，已招募→王国锚点；VagrantCampSystem 记录出生营地坐标 |
| DR-8 | T14 WaterNetwork 类型与容量 | 单例 MonoBehaviour + 容量 100 水 | 挂场景 GameObject；可被 SaveManager 持久化；超出 100 水井停产 |
| DR-9 | T15 农场耗水策略 | 1秒1次产出事件，每次耗2水 + 缺水停产 + UI 缺水图标提示 | farm.rate=2 粮/秒 → 1秒1次产出+2粮、ConsumeWater(2) 耗2水（DR-18 澄清）；缺水时农场头顶冒"缺水"图标 |
| DR-10 | T2 自动说话（多 NPC 数量问题） | 视野裁剪 + 同时上限 6 + 轮转队列 | 相机视野外 NPC 不冒字；视野内同时气泡 ≤ 6 个超出的进队列轮转；单 NPC 间隔 15-30s+冷却 8s |
| DR-11 | T19 资源点采集 | 确认 UI + 按资源量动态耗时 + 并行 + 对象池 | WoodPile 2s / StonePile 4s / OreVein 8s；多个资源点可并行采集（每点 1 NPC）；销毁走 BuildingFactory.Release/Despawn 对象池 |
| DR-12 | T7 训练时长 default | 按职业分级 | 居民→工人 1 天；→士兵 2 天；→高阶职业 3 天；进 TrainingConfig.asset 的 trainDuration 字段 |
| DR-13 | T10 farm.rate | 2 粮/秒 | 从 15 校准到 2；与"需工人+水"配合节奏 |
| DR-14 | T13 well.rate | 4 水/秒 | 1 口井供 2 农场（农场 2 粮/秒耗 2 水） |
| DR-15 | T12 WarehousePanel 入口与刷新 | 顶部按钮 + 订阅刷新 | 顶部 HUD 加"仓库"按钮；面板订阅 StorageComponent 变化事件实时刷新 |
| DR-16 | T16 ITaskSource 注册机制 | 建筑生命周期挂钩 | ITaskSource 扩展 OnRegister/OnUnregister；Building.OnSpawn 调 TaskScheduler.Register；Building.Die 调 Unregister |
| DR-17 | T17 调度器参数 | 1s/tick + 引用占用 + 距离升序 | 任务幂等靠"任务被 NPC.currentTask 引用即占用"；同优先级内按距离升序排序 |
| DR-18 | N1 耗水时序 | 1秒离散触发 | ProducerComponent.Tick 改 1s/tick 离散事件；每次产出 +2粮、ConsumeWater(2)；缺水该秒不产+冒图标；DR-9 原文"fixed 1水/次"更新为"每次产出耗2水（1秒1次产出事件）" |
| DR-19 | A1 在场判定窗口 | 仅 Working 算在场 | TaskScheduler 需暴露 GetWorkerState(npcId)→TaskState 查询；Assigned/MovingToSource 不算在场；工人换班/死亡到新工人到达有空窗期停产（设计特性，非bug） |
| DR-20 | QQQ.3 粒度 | 场景清单+原则+接口契约 | 不写实现细节；T16-T18 落地后按契约补；QQQ.3 现在可写完，不占位 |
| DR-21 | SafetyScore 4 个数值 | 0.02/格最低0.1 / 8格3友军+0.2 / threatFactor=0.8 / wanderThreshold=0.4 | 三层梯度：<0.4撤退 / 0.4-0.6小半径Wander / ≥0.6大半径+自动说话；armyFactor 复用 protectionUpThresholds[0]=3；满威胁(heat=1)时最高 Score=1.0-0.8=0.2 < 0.4 不 Wander |

### DR-4 引出的兜底需求（→ QQQ.3）

任务指派判定引入了**双向状态耦合**风险：
- NPC 死亡时：调度器若不同步清除指派 → ProducerComponent 永远判定"有人"持续产出
- NPC 被玩家手动调走（如调去战斗）：派发记录残留 → 同上
- NPC 被招募（流浪汉转居民）：流浪汉旧任务记录残留 → 同上
- 调度器重启/存档加载：派发记录与建筑/NPC 状态如何对账
- 网络断线/Editor 强制停止 Play Mode：未保存的派发记录如何恢复

这些场景的兜底机制统一在 QQQ.3「流程破坏兜底设计」中详细设计。

### 笔误修正

- §10.1 `enum KingDoDestType` → `enum KingdomDestType`（漏 m）

---

## 实施记录

> 2026-08-07（提交 998a0b3）：T1-T22 全部落地，编译 0 error 0 warning。

| 任务 | 实现摘要 | 关键文件 |
|------|---------|---------|
| T1 气泡复用（DR-6） | 每单位 1 气泡，新说话销毁旧气泡 | IClickInteractable.cs（OverheadSpeech.Show） |
| T2 自动说话（DR-10） | SafetyScore>0.6+IsIdleForTask+非Caution，15-30s 随机；视野裁剪/上限6/轮转/冷却8s | 新 OverheadSpeechManager.cs、NPCBrain.TickAutoTalk、AttentionTuningConfig talk* 字段 |
| T3-T5 装备厂移除 | Armory/Equipment* 代码+资产全删，无 equipId 残留 | Armory.asset、EquipmentDef/System/Panel 删除、BuildingPanel 去装备入口 |
| T6/T7 训练 UI | 可训练人数+队列（职业×数量）+正在训练；TrainingSystem 暴露 + TrainingConfig 字段（T21） | TrainingPanel、TrainingSystem.cs、TrainingConfig.cs |
| T8 空闲分布（DR-5/DR-21） | SafetyScore 三路合并（公式见 §需求4）；WanderAnchorPool 动态锚点池（城堡+预设+建筑5s重建+采集空地）；RetreatToSafeAnchor 撤退谱系；GridSystem.IsInsideWall 双表示；Wander 半径按 Score 档位 4-8 格；Caution 低分不驻留 | 新 SafetyScoreFormulas.cs / WanderAnchorPool.cs / RetreatToSafeAnchorBehavior.cs；Wander/SafetyStimulusProvider、L3CommandComputer、HitCooldownStateMachine、NPCBrain、GridSystem |
| T9 工人在场（DR-4/DR-19） | ProducerComponent.HasWorkerAssigned 基于调度器派发记录；异常退出清指派（死亡/招募走/中断） | ProducerComponent.cs、TaskScheduler.cs、Building.cs、VagrantCampSystem.cs |
| T10 速率校准（DR-13） | farm.rate 15→2 粮/秒 | farm.asset |
| T11 流浪汉（DR-7） | BirthCampPos 字段+存档 v4；SceneHomePointProvider 未招募→营地/已招募→王国 | UnitController.cs（存档 v4）、SceneHomePointProvider.cs、VagrantCampSystem.cs |
| T12 仓库面板（DR-15） | 顶部入口+订阅刷新，汇总 StorageComponent 按资源类型 | 新 WarehousePanel（cs/uxml/uss） |
| T13 Well 降 tier1 | Module_Livelihood tier1 unlockBuildings 加 Well | Module_Livelihood.asset |
| T14 水网（DR-8/DR-14） | WaterNetwork 单例容量100；well.rate=4；UI 隐藏 | 新 WaterNetwork.cs、well.asset |
| T15 农场水条件（DR-9/DR-18） | ConsumeWater(2)+HasWorkerAssigned 才产；缺水停产+头顶"缺水" | ProducerComponent.cs、farm.asset |
| T16-T20 任务系统 | KingdomTask/ITaskSource（OnRegister/OnUnregister）/ITaskScheduler/TaskScheduler（1s/tick+距离升序+引用占用）/TaskState；WorkerTask 内化工厂（T18）；资源点采集生命周期（T19 gatherSeconds 2/4/8s+对象池+锁定复位） | 新 Tasks/、TaskScheduling/、WorkerTask.cs、Building.cs、BuildingFactory.cs、BuildingDef.cs、BuildingPanel.cs |
| T21 训练配置字段 | supportedOccupations + trainDuration | TrainingConfig.cs |
| T22 验证场景 | 生产链路端到端 + 闲逛遇敌撤退 一键生成 | AIDebugSpawnController.SpawnProductionChainScenario / SpawnWanderRetreatScenario |

>> 📌 **2026-08-30 漂移追记**（转录自 [文档对账报告_2026-08-23](./报告/落实对账/文档对账报告_2026-08-23.md) §三 及 _目录 状态列后续裁决；仅转录未重审代码，最后核对日期以 _目录 为准）：
> - T18"入国库"→两段式搬运（WorkerInventory）；T19→2_12 步骤6 双路径。
