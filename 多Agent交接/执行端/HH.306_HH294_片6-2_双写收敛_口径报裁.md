# HH.306 · HH.294 片 6-2「双写收敛」口径报裁（停手）

> **端**：执行端｜**日期**：2026-09-17｜**任务书**：`多Agent交接/策划端/HH.294_底层重构批_任务书.md` §二 片 6 ＋ 本次派工提示词（`D777`）
> **口径源**：`最高优先级文档/03_地图即数据库.md`（§6.7／§7／§8／§9）＋ `04_上层接入指南.md`（五条不许）＋ `底层执行计划.md` §二 批 5-A
> **状态**：**停手报裁 —— `Valley Rampart/Assets/**` 一行未写**（含 Editor 探针；§四 附在场性证据）
> **依据**：提示词 §二 表头「（我实核过·照此施工；**发现反例 ⇒ 停下报裁**）」＋ §四 红线③「口径不明 ⇒ **停下报裁**，禁止『按理解实现』」
> **取号**：HH 水位线 `HH.305` → **`HH.306`**（本报告）；按 `D767`／`D776` 纪律**不代写策划端四档账本**，应登记项只在 §六 声明

---

## 一、冷启 ＋ 逐条复核（`D770` 规矩：行号一律当场取／直读原处）

### 1.1 冷启（指定 7 份全读完）

| # | 文件 | 读得 |
|---|---|---|
| 1 | `最高优先级文档/03_地图即数据库.md` | §6.7 池子／§7 删／§8 查（§8.6 `PickAt`／§8.7 判据 6·7）／§9 改 —— 本片口径唯一源 |
| 2 | `最高优先级文档/04_上层接入指南.md` | 五条不许（§六） |
| 3 | `最高优先级文档/底层执行计划.md` | 本片＝§二 批 5-A／5-B ＋ §五 回退（`useDataOnly` 属**待新增**非既有） |
| 4 | `多Agent交接/策划端/HH.294_片6-1_验收报告.md` | 残余 `R1`（窗口<sprite 溢出）／`R2`（判据自证）＋ 勘正我方 `2-4` 成立 |
| 5 | `多Agent交接/执行端/HH.305_…_交付报告.md` | 片 6-1 全部读数与双轨开关 |
| 6 | `多Agent交接/_当前快照.md` | 水位线 HH.305／`D777` 派工 9 项实核 |
| 7 | `多Agent交接/策划端/HH.294_底层重构批_任务书.md` §二 片 6 | 6-A~6-D 原目标 |

### 1.2 提示词 `file:line` 复核 —— **全部吻合（零漂移）**

以下每条均为本轮 `git grep -n` ＋ 直读原处所得（⛔ 未凭记忆／未反推）：

| 提示词锚点 | 实读结果 | 判 |
|---|---|---|
| `ResourceRespawnSystem.ConfirmTreeGather` `:458-468` | `:458` `public bool ConfirmTreeGather(GridCoord cell)`，`:461` 仅认 `FeatureType.Tree` | ✅ |
| `HandleTreeGathered` `:471-480`／`HandleEntityDepleted` `:483-488` | `:471`／`:483`；`:477` 确含 `GuardDeploymentSystem.HandleResourceConsumed(cell)` | ✅ |
| 池子按 features 统计 `:195-196` | `:195` `MapGenRules.CountChunkPoints(map, cx, cy, _tmpHave)` | ✅ |
| 存档只存 `counts` `:555-570` | `:555` `SaveState()`；`ResourceRespawnSaveData`（`:627`）字段＝`counts/targets/cw/ch/lastSettleDay…`，**无逐格数据** | ✅ |
| `SpawnEntityFor`（重生附加实体） | `:417-422`：只对 `OreVein/WoodPile/StonePile` 调 `BuildingFactory.ReSpawnNaturalBuilding` | ✅（提示词写 `:381`＝其调用点 `PlaceOne` 内，吻合） |
| `MapGate.IsResourceNodeFeature` `:315`／`IsRemovableResourceFeature` `:321-323`／`RemoveResourceNode` `:296`（内含 `:308` 守卫通知）／`PickCellRadius` `:421` | 逐行实读一致；`IsRemovableResourceFeature` **已含三型** ✅ | ✅ |
| `WorldGatherSource.ForEntity` `:71-79`／`ForTree` `:62-68`／`_entity`·`_isTree` `:44-45`／`PointStillThere` `:125-132` | 逐行一致 | ✅ |
| `WorldGatherRegistry.TryMatchPoint` `:153-174`（②实体分支 `:165-172`）／`Advertise` `:105-120`（三目 `:111-113`） | 逐行一致；`Advertise` 实体分支传 `sched.gatherAmount` | ✅ |
| `MapGenRules.DeriveNaturalBuildings` `:1510-1535`（白名单 `:1526`） | `:1526` `if (f != OreVein && f != WoodPile && f != StonePile) continue;` | ✅ |
| `WorldManager.cs:202` 步骤11 | `:202` `MapGenRules.DeriveNaturalBuildings(map); // 步骤11` | ✅ |
| `BuildingFactory.FeatureToBuildingType` `:50-58`／`InstantiateFromMap` `:68`（自然建筑循环 `:79-102`）／`ReSpawnNaturalBuilding` `:130-148` | 逐行一致 | ✅ |
| Editor 两探针 `Valley_HH272_MapGenProbe.cs:54`／`Valley_HH291_MapGenProbe.cs:67` | 两行均为 `MapGenRules.DeriveNaturalBuildings(map);` | ✅ |
| `Building.cs` 采集面 `:158`／`:909`／`:923`／`:946`／`:1027-1028` | `:158 isBeingGathered`／`:909` 守卫／`:923` 调 `HandleEntityDepleted`／`:946 StartGather`／`:1028` 采集分支 | ✅ |
| `BuildingPanel.cs:478`（`def.isConsumable` 按钮） | `:478-484` 资源行＋采集按钮；`:588-594` `OnGatherClicked → _target.StartGather()` | ✅ |
| `KingdomBrain.cs:471`／`WanderStimulusProvider.cs:184` | `KingdomBrain.cs:471` 逐字；`WanderStimulusProvider` 实路径＝**`Assets/_Game/Systems/AI/Stimulus/WanderStimulusProvider.cs:184`** | ✅（路径补全，非漂移） |
| `SelectionController.cs:288-293`（D115 发布点已存在） | `:289 allWorkers && nearResource` → `:291` 守卫 → `:292 Publish(new PrioritizeHarvestCommand(alive, world))` | ✅ |
| `LifecycleAudit.cs:51`／`:124` | `:51` ＝ `ExemptEvents`（豁免表）内；`:124` ＝ `Baseline54`（基线）内 | ⚠️ **两处性质不同 ⇒ 见 `B-3`** |
| `AIDebugSpawnController.cs:502`／`BuildingDef.isConsumable` | `:502` 传 `def.isConsumable`；字段定义 `BuildingDef.cs:90` | ✅ |
| `TaskScheduler.gatherAmount` | `TaskScheduler.cs:49` `public int gatherAmount = 5;` | ✅ |

---

## 二、必裁项（**4 条**；`B-1`／`B-2` 为阻塞项）

### `B-1`（最重·阻塞）§2-2 `nodeGatherSeconds` **单一默认值**与「＝改前实盘值」**不可同时成立**

**实读（直读资产原处，逐字段）**：

| 处 | 值 | 说明 |
|---|---|---|
| `Assets/Resources/Buildings/ore_vein.asset:65` | `gatherSeconds: 8` | 矿脉 |
| `Assets/Resources/Buildings/stone_pile.asset:65` | `gatherSeconds: 4` | 石堆 |
| `Assets/Resources/Buildings/wood_pile.asset:65` | `gatherSeconds: 2` | 木堆 |
| `Assets/Resources/Buildings/tree.asset`（对照） | 树走数据路径 ⇒ `RespawnConfig.treeGatherSeconds` | 见下 |
| `Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs:49` | `gatherAmount = 5` | 入包量**全局单值** |
| `Assets/Resources/Config/RespawnConfig.asset:16-17` | `treeGatherSeconds: 2` / `treeGatherAmount: 5` | 现有树参数 |
| `Assets/_Game/Data/RespawnConfig.cs:33,35` | 同上（C# 默认值） | — |

**旁证（设计意图＝三型刻意不同，非数据噪声）**：`TaskScheduler.cs:1130-1131`

```csharp
if (task.source is Building b && b.def != null && b.def.gatherSeconds > 0f)
    secs = b.def.gatherSeconds;   // 以 def 为准（资源点资产已配 2s/4s/8s）
```

**冲突**：`nodeGatherSeconds`（**三型共用一套**·单值）**不可能**同时等于 `{8, 4, 2}` 三个不同的「改前实盘值」。
⇒ 任何取值都会**改变至少两型的采集耗时**（行为变更），而提示词明写「⛔ 禁凭猜」⇒ 按纪律停手。

**入包量侧无冲突**：`nodeGatherAmount ＝ TaskScheduler.gatherAmount ＝ 5` 单值，可直接落。

**请裁（取值取向；候选仅为呈报，非自选）**：

| 案 | 内容 | 后果 |
|---|---|---|
| **甲** | **保三型差异**：`RespawnConfig` 落**按型**参数（如 `oreVeinGatherSeconds 8` / `stonePileGatherSeconds 4` / `woodPileGatherSeconds 2`，树沿用 `treeGatherSeconds 2`） | 语义**零变更**；但与「三型共用一套」的字面口径不符，需改口径 |
| **乙** | **真共用一套**：单值 `nodeGatherSeconds`，**须指定取 2／4／8 中哪一个** | 至少两型耗时改变（如取 2 ⇒ 矿脉 8→2 快 4×）；属**行为变更**须追认 |
| **丙** | 单值 ＋ 按型可覆盖（默认取某值，三型各覆盖为 8/4/2） | 字段最多；等价甲 |

（请一并裁定：`nodeGatherSeconds` 与 `treeGatherSeconds` **是否合并**为同一字段——现状树 2s 与木堆 2s 同值。）

### `B-2`（阻塞）§6-E 两处 AI 消费面的**实际口径**与提示词不符 ⇒ 迁移面与「保语义」目标集须重裁

**提示词原文**：「现状两处按『实体型资源点』判定，数据化后**不再命中**」
**实读结论：两处各不相同，其中一处的前提不成立。**

**(a) `KingdomBrain.cs:471` `if (def.isResourceNode) gatherNodes++;` ⇒ ⛔ 不受本批影响**

- `QueryKingdomBuildingsSorted`（`KingdomBrain.cs:545-565`）**按 `kingdomId` 过滤**（`:553` `src[i].kingdomId == kingdomId`）；
- 三型自然实体**全部** `kingdomId=-1`（`BuildingFactory.cs:99`／`:147` 哨兵），**本来就进不了这个循环**；
- `stone_pile`/`wood_pile` 的 `def.isResourceNode` **本就＝0**（`stone_pile.asset:53`／`wood_pile.asset:53`）⇒ 本来也不计；
- **实际被计的是 `mine` 建筑**：`mine.asset:53 isResourceNode: 1`，且 AI 立国**每国预置 1 座**（`baseBuildingDefIds` 六模板逐字相同＝`[castle, House, farm, Well, mine, Warehouse, quarry]`，`mine` 为第 **5** 项；`KingdomFoundry.cs:156-160` 取前 `buildingCount`＝5/6/7 个）⇒ 现值 ≈ **1／国**；
- ⇒ 6-A 删三型实体后**该计数不变**（仍＝mine 数）⇒ **无需迁移、无静默变更**（提示词该半句为误判）。

**(b) `WanderStimulusProvider.cs:184` `IsResourceDef(def) => def.isResourceNode;` ⇒ ✅ 确有静默变更，但目标集须裁**

- 该处遍历 `BuildingRegistry.Instance.All`（`:167`）**无 kingdom 过滤**（仅 `:169` `b.IsActive`）⇒ 今天把 **`ore_vein` 实体**（kingdomId=-1）计入 `resourceSites`；
- 6-A 后 `ore_vein` 实体消失 ⇒ 该部分**静默归零**（确需迁移）；
- ⚠️ **但「保语义」的目标集 = `OreVein` 单型，不能是「三型」**：`stone_pile`/`wood_pile` 的 `isResourceNode=0` 是**已裁口径**——`BuildingDef.cs:59-62` 与 `WanderStimulusProvider.cs:179-183` 两处注释逐字记录：

  > 「三资产值不齐（`ore_vein`=1 一次性探明矿点 vs `stone_pile/wood_pile`=0 开局过渡堆积）＝**语义差异非缺陷，不统一为 1**——本处消费即『成为 NPC 游荡锚点候选』（**真行为**），统一会造成**批内行为漂移**」（`DZ-054`／`D617`）

  ⇒ 若按字面「改走特征层（三型）」，会把 stone/wood 两型**新增**为游荡锚点 ⇒ 恰是 `D617` 明令**不要**的漂移。

**请裁**：

| 案 | 内容 |
|---|---|
| **甲（保语义·本端读数支持）** | `WanderStimulusProvider` 迁到**特征层 `OreVein` 单型**（格中心位置；`KingdomBrain:471` **不动**、仅勘正注释）⇒ 计数与原集 1:1（原实体⇄特征同格） |
| **乙** | 迁三型（改口径，承认新增 stone/wood 锚点＝行为变更，须追认并覆盖 `D617` 注释） |
| **丙** | 两处都**不迁移**，接受游荡锚点减少（本端不推荐：属静默 AI 行为变更，违 §6-E 原意） |

**另注（面迁移的实现细节，需在裁决中一并确认）**：现锚点取自 `b.GetPosition()`（建筑世界位），改特征层后取**格中心**（`GridSystem.CoordToWorld(cell)`）——两者数值接近但不逐位相等，属口径变更需声明。
**⚠️ 禁每帧调用面**（`03` §8.7 判据 7）：该提供器是否走每帧路径须先实测；若每帧 ⇒ 走索引／区块缓存并给代价读数（本端施工时按此办，若与裁决取向冲突再报）。

### `B-3`（需确认）`LifecycleAudit` 的两处**性质不同**：`:124` 移出会造成**假报**

- `:51` ∈ `ExemptEvents`（「R1 事件豁免」＝**无守卫零订阅噪音**豁免表，`:175` `ExemptEvents.Contains(name)` 用于 R1 对拍）⇒ 接入消费者后**移出**＝正确（`D561`「豁免表变更须策划端确认」⇒ **本条即确认请求**）。
- `:124` ∈ `Baseline54`（`:109-133`「54 事件基线·**只读对账面**」）⇒ `:207-211` 用它做**双向在场对账**：

  ```csharp
  foreach (var n in scanSet) if (!Baseline54.Contains(n)) extra.Add(n);   // 扫描有·基线无
  foreach (var n in Baseline54) if (!scanSet.Contains(n)) missing.Add(n); // 基线有·扫描无
  ```

  **事件体不移除**（本次是**加**订阅者）⇒ 若把 `PrioritizeHarvestCommand` 从 `:124` 移出，审计将报**「+ PrioritizeHarvestCommand（扫描有·基线无）」假差异**。
- ⇒ **本端拟办**：只移 `:51`，**保留 `:124`**。请确认（若要求两处都移，请明示并提供替代对账口径）。

### `B-4`（需确认·红线②边界）6-D 退役 `Building` 采集面 ⇒ **必改 `TaskScheduler` 两行**，否则编译不过

`Building` 采集面的**唯一生产调用方**是 `TaskScheduler`（不在 §6-D 清单内）：

| 处 | 原文 | 退役后 |
|---|---|---|
| `TaskScheduler.cs:555-556` | `if (task != null && task.type == KingdomTaskType.Gather && task.source is Building gb) gb.isBeingGathered = false;` | `isBeingGathered` 退役 ⇒ 该行编译失败 |
| `TaskScheduler.cs:632` | `if (comp is Building b) b.OnGatherCompleted();` | `OnGatherCompleted` 退役 ⇒ 该行编译失败 |

**提示词** §6-D 只列 `Building.cs`／`BuildingPanel.cs:478`，而 §四 红线② 写「⛔ 不碰中层（`05`/`06`/`07`/`08`/`#14`）—— 只允许 §6-D 里那**一处** `BuildingPanel` 死码退役」⇒ **`TaskScheduler` 是否在允许面内需明示**。
（另：`WorldGatherSource.cs:121` 的 `_entity.OnGatherCompleted()` 属 6-B 自身退役面，无需另裁。）

### `B-5`（报备·不需裁）§2-0 订阅落点的「先例」措辞与实盘略有出入，本端按实盘落地

- `GameBootstrap.cs:40` 实为 **`_ = DayCycleSettlement.Instance;   // 每日结算统一入口（订阅 TimeDayChangedEvent）`** —— 即**引导层只保实例**，真正订阅在 `DayCycleSettlement.Awake`（`DayCycleSettlement.cs:17` `EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);`）。
- ⇒ 本端拟按同一先例：**消费者订阅落在系统自身 `Awake`**（`ResourceRespawnSystem.Awake` 已有订阅先例 `:123`），`GameBootstrap` 侧仅在需要时保实例（若 `ResourceRespawnSystem` 已常驻则**零改动**）。符合「⛔ 禁 `Start()` 里订阅」。

---

## 三、复核附注（非阻塞·供验收参考）

1. **判据 3「三型各 1 次 ＋ 树 1 次」可达性成立（静态判读）**：`nearResource` 闸（`SelectionController.cs:273`）取自 `GuardDeploymentSystem.FindNearestResourceNode`（`:322-353`），该查询**无距离上限**（全图索引取最近）⇒ 只要图上存在任一 `Tree/Mine/OreVein` 格即为真；而 `PrioritizeHarvestCommand.TargetPos`（`GameEvents.cs:48`）＝**玩家实际右键落点** ⇒ 消费者可由落点解析出 `stone_pile`/`wood_pile` 格并立案。⚠️ 该结论为静态读得，**建议施工探针给三型各 1 次运行时读数坐实**（届时若与实盘不符再报）。
2. **`or` 面口径提示**：`IsGuardResourceFeature`（`GuardDeploymentSystem.cs:301-302`）＝`Tree/Mine/OreVein`（守卫高价值口径），**与本批删门口径分列**，本批不触。
3. **6-D 清洁项证据已核**：`HandleTreeGathered:474` 的 `MapGate.RemoveResourceNode(cell)` **门内已含** `GuardDeploymentSystem.HandleResourceConsumed`（`MapGate.cs:308`）⇒ `:477` 确为**重复调用**（合一后删 `:477`；`HandleResourceConsumed` 幂等性本端未验——施工时按提示词要求给读数）。
4. **`treasure_box` 交叉**：`isConsumable=1` 的 def 共 **4 个**（`ore_vein`／`stone_pile`／`wood_pile`／**`treasure_box`**）。`treasure_box` 属「建筑体系重构批」**待退役死资产**（宝箱实际走 `ChestEntity` 独立管线，`R5_SixStage.cs:232` 注记），且无实例化路径 ⇒ `BuildingPanel:478` 退役后该分支**在实盘无活对象**。另：`AIDebugSpawnController.SpawnLifecycleScenario`（`:475`）仍会直接实例化 `wood_pile` 建筑（调试面·按裁决不动）⇒ 其 `:474` 注释「玩家点击 `StartGather` 触发」将成为**陈注释**（属微瑕，施工时随 §6-D 一并标注）。

---

## 四、在场性证据（**未动一行**）

```
$ git status --short -- Assets/_Game/Systems/World/MapGenRules.cs ... LifecycleAudit.cs ... GuardDeploymentSystem.cs
（空 ⇒ 本批目标文件零改动）

$ git status --short | Measure-Object -Line
68   ← 全部为并行会话既有改动（美术 Ground/*.png、pixel-forge、GameScene.unity、
       Packages/*、3.6/3.8 doc、Valley_HH284_Probe.cs 等），本端一律未碰

$ git log --oneline -1
986b973d D777 ⭐片6-2 双写收敛派工…（派工提交，本端未产生任何新 commit）

行尾核对（施工前基线，供后续编辑防幽灵 diff）：
  MapGenRules.cs          CRLF=0   LF=1536   → LF
  ResourceRespawnSystem.cs CRLF=0  LF=637    → LF
  Building.cs             CRLF=0   LF=1096   → LF
  BuildingFactory.cs      CRLF=451 LF=0      → **CRLF**（改时须走 python 二进制按行替换，禁 Edit/Write）
```

---

## 五、若裁「继续」的施工序（待裁决后执行；本轮不给读数）

1. 依 `B-1` 裁决落 `RespawnConfig` 参数 ＋ 报 SO 资产逐字段 diff
2. `B-2` 裁决后落 6-E（迁移／不迁移 ＋ 目标集）
3. `B-3`／`B-4` 确认后：6-A（含 `SpawnResourceEntities` 对照开关·默认 false）→ 6-B → 6-C → 6-D → 搭车 `R1`／`R2`／清洁项
4. 判据 1~12 逐条读数（含改前/改后 `BuildingRegistry` 总数、`isConsumable` 消费者清单、正门三态、同 seed 逐格一致）

---

## 六、取号与应登记项（`D767` 纪律：不代写策划端账本，仅声明）

1. `_编号登记.md`：HH 水位线 **HH.305 → HH.306**（本报告）
2. `_任务队列.md`：片 6-2 行状态 → **🔴 停手报裁（等 `B-1`／`B-2`／`B-3`／`B-4`）**
3. `_当前快照.md`：`D777` 派工行补「执行端已接单 ⇒ 首轮报裁 4 项」
4. 台账新增节（§四十九）：本报告四条报裁 ＋ 6-E 两处实读口径（`KingdomBrain:471` 不受影响／`WanderStimulusProvider:184` 仅含 `OreVein`）
5. 教训库候选：`L-40` 家族 ＋1（**「消费面判定」须连「过滤条件」一起直读**——`KingdomBrain` 的 `kingdomId` 过滤使「实体型资源点」前提落空）；`L-02` 家族 ＋1（`WanderStimulusProvider` 实路径含 `Stimulus/` 子目录）