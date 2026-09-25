# `HH.331` `M4-C` 开片前实核 交付报告（只读批）

- **编号**：**`HH.331`**（对 `_编号登记.md` 在途行另取 · 先登记后落盘 · 水位线 330→331）
- **依据**：`D864` 已验收 `M4-A` ｜ 中层执行计划 §4.4 的 `M4-C` ｜ `08` #34／#36／#39／§九-6
- **性质**：**只读实核**（⛔ 零生产码 · ⛔ 零资产删除 · ⛔ 不改 `08` · ⛔ 不改《中层执行计划》 · ⛔ **不出删除方案**）
- **基线**：`e7bb8696`（`M4-A` 已由 `c43f9fff`／`D864` 收编入基；本批读数即**已入库现状**）
- **日期**：2026-09-25
- **⚠️ 为什么先读不删**：`M4-A` 后 `Pickup`／`Rift`／`CastleCore` **不再由 `BuildingFactory` 的 `if` 挂上**，改走「数据行 `BuildingDef.components` ＋ `BuildingComponentRegistry` 键表」（`BuildingComponents.cs:189-258`）—— `HH.328` 报的 `BuildingFactory.cs:280` 一带**已不存在**（该文件现 `:237-255` 只有遍历数据行）。

---

## 〇、一句话 ＋ 停手

四个类的引用面**已全部换成新形态**：`SpawnerComponent` 全库仅类定义 1 处（仍零挂载）；`PickupComponent`／`RiftComponent` **已由数据行挂上**（各 4 栋／2 栋）；`CastleCoreComponent` 活（`castle` 数据行 ＋ 来源守卫）。六栋资产 **6/6 在场**。
⚠️ **停手 3 命中**（见 §五）：`FoodWorkshop`／`Ranch` **`isPlayerBuilt: 1` ＋ 已进 `Module_Production.asset` tier2 `unlockBuildings`** ⇒ **玩家建造菜单可达**（创建链已写）⇒ 按停手纪律**停下列报**。
⛔ 停手 1 **未触发**（本批一行未改、未出删除方案）；⛔ 停手 2 **未命中**（三类 `Init` 全空 ＋ 全库对三类**无 Tick／战斗／刷怪调用点**）。
**缺口是否还在**：**仍在**，但形状已变 —— 见 §六。

---

## 一、件 A · 四个类现在谁还引用

### A-1 逐类引用表（四列：生产码／数据行／探针／同名活物）

| 类（现行类定义） | 生产码 | 数据行 | 探针 | 同名活物（⛔ 均非本类） |
|---|---|---|---|---|
| **`SpawnerComponent`**<br>`BuildingComponents.cs:36-39`（`Init` 空 `:38`） | **仅类定义 `:36`** —— 全库**无 `AddComponent`／`GetComponent`／键表项**（`BuildingComponentRegistry` 9 键内**无 spawner 键**·见 `:195-203`） | **无**（零键） | **无**（`Valley_HH329_M4AProbe.cs` 零命中） | `MonsterSpawner`（`Disaster/MonsterController.cs:113` · 怪物生成桥·活）；`WaveDirector`（`AI/WaveDirector.cs` · 出怪调度·活） |
| **`PickupComponent`**<br>`:30-33`（`Init` 空 `:32`） | 类定义 `:30` ＋ **键表** `:215`（`Register(Pickup, Add<PickupComponent>)`） | **4 栋**：`ore_vein.asset:16-17`／`stone_pile.asset:16-17`／`wood_pile.asset:16-17`／`treasure_box.asset:16-17`（`comp.pickup`） | `Valley_HH329_M4AProbe.cs:33`（键→类映射）／`:191`（旧 if 链**推导复刻**）／`:238`（旧链**实挂复刻**） | `Pickup`（**箱子的"拾取"能力**·`World/ChestManager.cs:21-22` 注「`Pickup` 已退役 `D798`」）；`MapGate` 拾取命中链（`MapGate.cs:617-623` · 候选＝`ChestEntity`） |
| **`RiftComponent`**<br>`:162-165`（`Init` 空 `:164`） | 类定义 `:162` ＋ **键表＋来源守卫** `:220-221`（`b.sourceType == BuildingType.Rift` 才挂） | **2 栋**：`rift.asset:16-17`／`portal.asset:16-17`（`comp.rift`） | `Valley_HH329_M4AProbe.cs:34`／`:192`／`:239`；`Valley_HH294_Slice6PickProbe.cs:463`（放置扫描**排除** `rift`/`portal`） | `BuildingType.Rift`（枚举项·`GridTypes.cs` 一带）；`riftCellX`（地图 JSON 字段·**C# 侧 0 命中**）；⭐ 真出怪链走 **`Portal` 实体**（`Disaster/Portal.cs:8`）＋ `WaveDirector.SpawnPortalEntity:246`／`PortalDisasterTrigger.cs:11` —— ⛔ **不是 `RiftComponent`** |
| **`CastleCoreComponent`**<br>`:168-181`（`Init` **非空** `:170-180`） | 类定义 `:168` ＋ **键表＋来源守卫** `:222-223`；`Init` 内挂 `ThroneAnchor`（`:174-175`·**仅当** `ThroneAnchor.Instance == null`）＋ `TreasureVault`（`:178-179`） | **1 栋**：`castle.asset:16-18`（`comp.combat` ＋ `comp.castle_core`） | `Valley_HH329_M4AProbe.cs:35`／`:193`／`:240` | `BuildingType.CastleCore`（枚举）；`ThroneAnchor`（`Kingdom/ThroneAnchor.cs:9` · 全局单例）；`TreasureVault`（`Kingdom/TreasureVault.cs:21` · `IBuildingComponent`） |

### A-2 ⭐ `castle` 的来源守卫（任务指名项）

**会跳过。** `BuildingComponents.cs:222-223` 实读：

```222:223:Valley Rampart/Assets/_Game/Systems/Building/BuildingComponents.cs
        Register(CastleCore,    (go, b) => b != null && b.sourceType == BuildingType.CastleCore
                                            ? Add<CastleCoreComponent>(go, b) : true);
```

- `sourceType == None`（玩家建造 `Building.Init:431` 置 `None`／调试 `AIDebugSpawnController.cs:512` 传 `None`）⇒ **三元走 `true` 分支**：⛔ 不挂组件、⛔ 也不触发「键未登记」告警（返回 `true` ⇒ `AttachComponents` 不 `LogWarning`）。
- 与改前等价性的读数在案：`HH.330` 探针 Part 1b「`castle` src=None 改前＝改后＝`[CombatComponent]`」（`Logs/hh329_m4a_probe.log`）。
- ⚠️ 反之：**只有**以 `BuildingType.CastleCore` 建实例（`BuildingFactory.InstantiateMapPresetBuildings:69-80`）才会挂上 `CastleCoreComponent` ⇒ `ThroneAnchor`／`TreasureVault` 只在该路径出现。

### A-3 `portal.asset` 的连带事实（`08` #36 所述"误挂"在 `M4-A` 后**依然成立**）

`portal.asset` 数据行 `comp.rift`（`:16-17`）＋ 其 `sourceType: 10`（＝`Rift`）⇒ 若以 `def.sourceType` 创建，守卫**放行** ⇒ **会挂 `RiftComponent`**（改前同款行为；因 `portal` 现无创建路径，属**潜伏**）。

---

## 二、件 B · 六栋资产是否还在 ＋ 谁按 id／文件名点到它们

### B-1 在场性（实读 `Assets/Resources/Buildings/`）

`rift.asset`（1.31 KB）／`portal.asset`（1.39 KB）／`ruins.asset`（1.28 KB）／`treasure_box.asset`（1.32 KB）／`FoodWorkshop.asset`（1.38 KB）／`Ranch.asset`（1.35 KB）—— **6/6 在场**，且**各自 `.meta` 6/6 在场**（`portal.asset.meta` 206 B，其余 213 B）。

### B-2 引用表（四列）

| 资产 | 生产码（按 id 字符串） | 数据行 | 探针 | 同名活物 |
|---|---|---|---|---|
| `rift` | `BuildingVisual.cs:57`（`case "rift": → bld_portal`）；`BuildingComponents.cs:220`（守卫所比对的枚举，非 id） | `rift.asset:16-17`＝`comp.rift`；`sourceType: 10`；`isPlayerBuilt: 0` | `Valley_HH329_M4AProbe.cs:80`；`Valley_HH294_Slice6PickProbe.cs:463` | `bld_portal`／`BuildingType.Rift`／`riftCellX` |
| `portal` | `BuildingVisual.cs:56`（`case "portal"`） | `portal.asset:16-17`＝`comp.rift`；`sourceType: 10` | 同上两条 | `Portal.cs`／`PortalDisasterTrigger`／`bld_portal` |
| `ruins` | `BuildingVisual.cs:69`（`case "ruins"`） | ⛔ **无 `components` 栏**；`sourceType: 8` | 0 命中 | `bld_ruins`（损毁态表现）／`BuildingType.Ruins` |
| `treasure_box` | `BuildingVisual.cs:70` | `treasure_box.asset:16-17`＝`comp.pickup`；`:60` `isConsumable: 1` | `ChainAudit/Validators/R5_SixStage.cs:246`（`{"treasure_box","ChestEntity"}` 别名台账） | `ChestEntity`／`ChestManager`／`TreasureVault` |
| **`FoodWorkshop`** | `BuildingVisual.cs:61`（`→ bld_market`）＋ ⭐ **`Resources/Modules/Module_Production.asset:35`**（tier2 `unlockBuildings`） | ⛔ **无 `components` 栏**；`isPlayerBuilt: 1`；`moduleType: 1` | 0 命中 | `bld_market`／`market.asset`（**图面就近占位**，⛔ 非同一栋） |
| **`Ranch`** | `BuildingVisual.cs:51` ＋ ⭐ **`BuildingPanel.cs:492`**（`def.id == "Ranch"` ⇒ `RanchSystem.AnimalCount`／`Capacity()`）＋ ⭐ **`Module_Production.asset:36`** | ⛔ **无 `components` 栏**；`isPlayerBuilt: 1`；`moduleType: 1` | 0 命中 | `Rancher`（任务类型）／`RanchSystem`／`RanchConfig.asset` |

**生成／创建面（逐面实读，除菜单外均不创建六栋）**：

- **地图生成**：`MapGenRules.DeriveNaturalBuildings:1511-1513` ＝ `map.naturalBuildings.Clear()`（契约槽位·调用点 `WorldManager.cs:206`）⇒ ⛔ 不派生。
- **地图预置**：`BuildingFactory.InstantiateMapPresetBuildings:69-85` ⇒ **只建 `CastleCore`**（`:84` 日志「自然建筑自 `HH.294` 片 6-2 收尾起不再派生」）。
- **AI 立国预置**：`KingdomDef.baseBuildingDefIds`（6 模板实读，例 `Kingdom_RiverBay.asset:32-39`）＝`castle`／`House`／`farm`／`Well`／`mine`／`Warehouse`／`quarry` ⇒ ⛔ 六栋均不在。
- **AI 行动面**：`UtilityActionConfig` 的 `buildingId:` ⇒ 六栋 **0 命中**。
- **存档重建**：`BuildingFactory.cs:313`（按 `data.sourceType` 重建）⇒ 仅重建存档内曾存在的实例。
- ⚠️ **映射表**：`BuildingMappingTable.asset:15-37` 共 11 条 `type→def`（`type 1~8/10/11/12`）—— 逐字实读**不含**六栋任一 **id／文件名**（其 `type 7`／`8`／`10` 三条按 guid 指向**其他** def）。

### B-3 ⚠️ guid 面口径校准（勘正一条既有结论的适用边界）

- 六栋 `.meta` 记录的 guid（base64 形式）在**全库除自身 `.meta` 外 0 命中**（含 `Assets/**` 的 `.asset`／`.unity`／`.prefab`）；解出的 hex 形式亦 **0 命中**。
- ⚠️ **但本项目两套 guid 文本不同构**：`.asset` 内引用写 **32hex**，而 `Assets` 下 **1228 份 meta 的 `guid:` 为 base64**（实读；连**已知 live** 的 art 引用 `9e0706290c08e554aa2f2f3aaf6f907a`（`SpriteRefTable.asset:139`）也**不出现在任何 meta** 中）⇒ **不可用字符串法判定「谁引用谁」**。
- ⇒ 本批只报**字符串实读**结果；同时勘正 `河谷防线开发计划书具体内容/测试基线台账.md:4028-4033`「六栋 guid 零引用」的口径：**按 base64 形式实读确为 0 命中（成立）**，但**不可外推**为「无任何引用通道」。⛔ 本批不判、不修（要判须走引擎侧解析，另批）。

---

## 三、件 C · 同名活物（只登记 · 判定「不是这栋资产」）

| # | 同名／近名活物 | 现行 `file:line` | 判定 |
|---|---|---|---|
| 1 | `ChestEntity` | `World/ChestEntity.cs:18`（`IInteractable, ITaskSource`） | ⛔ **不是** `treasure_box.asset`（掉落箱＝独立管线·`ChestEntity.cs:8` 自认「不逃入 Building 建筑管线」） |
| 2 | `ChestManager` | `World/ChestManager.cs:24`（`Singleton`＋`ISaveable`；唯一落箱入口 `:70`） | ⛔ 不是 `treasure_box.asset` |
| 3 | `bld_ruins` | `Rendering/PlaceholderSprites.cs:78`；`BuildingVisual.cs:83`；`Building.cs:782`（废墟态占位渲染） | ⛔ **不是** `ruins.asset`（＝贴图 key／**损毁态表现**） |
| 4 | `bld_portal` | `PlaceholderSprites.cs:76`；`Resources/Config/Art/SpriteRefTable.asset:138`；`BuildingVisual.cs:56/57/84/95` | ⛔ 不是 `portal.asset`（＝贴图 key） |
| 5 | `Portal`（灾害实体） | `Disaster/Portal.cs:8`（`IDamageable, IGridOccupant, ISaveable`）；生成点 `WaveDirector.cs:246`／`AI/WaveDirector.cs:89`；触发 `PortalDisasterTrigger.cs:11` | ⛔ **不是** `portal.asset`（独立实体管线；`08` #36 已判「传送门不走建筑体系」） |
| 6 | `PortalDef`／`PortalSaveData`／`PortalDisasterConfig` | `Disaster/Data/PortalDef.cs:5`／`PortalSaveData.cs:6`／`PortalDisasterConfig.cs:5` | ⛔ 不是 `portal.asset`（SO／存档载荷） |
| 7 | `feat_treasure_box` | `BuildingVisual.cs:70/82` | ⛔ 不是 `treasure_box.asset`（artId；实际渲染 `ChestEntity`） |
| 8 | `TreasureVault` | `Kingdom/TreasureVault.cs:21`（`IBuildingComponent`·castle 挂载·国库真源） | ⛔ **不是** `treasure_box.asset` |
| 9 | `ThroneAnchor` | `Kingdom/ThroneAnchor.cs:9`（全局单例·王座/失败判定锚） | ⛔ 不是六栋任一（`CastleCoreComponent.Init:174-175` 挂载） |
| 10 | `BuildingState.Ruined`／`EnterRuined` | `Building.cs`（损毁态族·`HH.321` 件1 读数在案） | ⛔ 不是 `ruins.asset`（**状态**非资产） |
| 11 | `riftCellX` | `Resources/Debug/Maps/map_0_seed12345.json`（region 字段）；**C# 侧 0 命中** | ⛔ 不是 `rift.asset`（地图数据字段·疑残留） |
| 12 | `Rancher`（任务类型） | `Data/TaskPriorityConfig.cs:38`（`KingdomTaskType.Rancher`）；`Data/Kingdoms/ResourceBiasConfig.cs:64`（`Rancher→Food`） | ⛔ **不是** `Ranch.asset`（任务类型枚举·与建筑无代码耦合） |
| 13 | `RanchSystem`／`RanchConfig` | `Kingdom/RanchSystem.cs:14`；`Kingdom/RanchConfig.cs`；`Resources/Config/RanchConfig.asset`；预热 `GameBootstrap.cs:46`；日结 `DayCycleSettlement.cs:90`；重置 `WorldLifecycle.cs:39`／`TeardownManager.cs:128` | ⛔ 不是 `Ranch.asset`（**但为 `Ranch` 的活系统 ⇒ 见 §五停手 3**） |
| 14 | `MonsterSpawner` | `Disaster/MonsterController.cs:113` | ⛔ 不是 `SpawnerComponent`（怪物生成·与产兵组件无关） |
| 15 | `bld_ranch` | `BuildingVisual.cs:51`（artId） | ⛔ 不是 `Ranch.asset`（其贴图·关联非同一） |

---

## 四、件 D · `isConsumable` 的生产码消费点

> `BuildingDef.cs:111` ＝ `public bool isConsumable = false;`（字段定义）｜⭐ 任务指名：**`Building.cs:1563` 是注释**（原文「① 采集：一次性资源点（`isConsumable`）被玩家确认采集 → Gather 任务 —— 【片 6-2·6-D】已删（见方法头注）」）—— **注明**。

### D-1 生产码面（`Assets/_Game/**`）

| `file:line` | 性质 |
|---|---|
| `BuildingDef.cs:111` | **字段定义**（非消费） |
| `AI/AIDebugSpawnController.cs:513` | ⭐ **全库唯一生产码读 `def.isConsumable`**（`CreateBuildingInstance(..., def.isConsumable, ...)`）—— **调试面**（`AIDebugSpawnController`） |
| `Building/BuildingFactory.cs:90` | `CreateBuildingInstance(..., bool isConsumable, ...)` **形参**；⚠️ **体内零使用** ⇒ **死参**（本文件唯一调用点 `:80` 传字面量 `isConsumable: false`） |
| `Kingdom/VagrantCampSystem.cs:220` | **具名传参** `isConsumable: false`（⛔ 不读 def 字段） |
| `Kingdom/KingdomFoundry.cs:179`／`:274`／`:472`／`:498` | **具名传参** `isConsumable: false`（4 处） |
| 注释面（5 处·⛔ 非消费） | `Building.cs:1563`（任务指名）｜`BuildingPanel.cs:501`／`:503`｜`WorldGatherRegistry.cs:159`｜`UtilityScorer.cs:641`｜`BuildingMappingTable.cs:7`（指**地图占位层**同名字段） |

### D-2 数据行面

- **`BuildingDef` 数据行**：`isConsumable: 1` ＝ **4 栋**（`ore_vein.asset:60`／`stone_pile.asset:60`／`wood_pile.asset:60`／`treasure_box.asset:60`）；其余 def＝0（40 栋资产全带该栏）。
- **另一层同名数据**（⛔ 不同层）：`Resources/Debug/Maps/map_0_seed12345.json` 的逐资源条目 `isConsumable`（地图占位层；`BuildingMappingTable.cs:7` 所述 `type+grade+isConsumable`）。

### D-3 探针面（**单列 · 不混计**）

`Assets/Editor/**` 共 **21 文件 27 处**（本行不计入生产码）：

- **只读打印**：`Valley_HH310_F15Probe.cs:101`（`…isConsumable= + def.isConsumable`）
- **具名传参（读 def 字段再传）**：`Valley2_17_Smoke_FixCard.cs:282`（`isConsumable: def.isConsumable`）／`Valley2_17_Smoke_2b.cs:189`
- **审计判据读**：`ChainAudit/Validators/R3_SupplyChain.cs:124`（`if (def.isConsumable)` 供给口径）
- **旧链复刻**：`Valley_HH329_M4AProbe.cs:191`／`:238`
- 其余 ~16 文件为 `CreateBuildingInstance(..., isConsumable: false, ...)` 具名传参（`Valley_HH107/109/111/291/314/315/316/317/318/320/321/OB12/2_16×2/2_20/TestFixtureApi` 等）

### D-4 结论（缺口）

`isConsumable` 现为**孤儿字段**：唯一**生效**消费者（`BuildingFactory.AttachComponents` 的 `if (def.isConsumable)` 分支）**已随 `M4-A` 删除**（`HH.330` 读数：`ProductionSystem`／`BuildingFactory` 生产码零类名）；现存读点仅剩 **调试面 1 处**（`AIDebugSpawnController.cs:513`）＋ 键表已改由 `comp.pickup` 数据行承担挂载 —— ⛔ 本批不判去留（停手 1）。

---

## 五、⭐ 停手 3 命中 · 创建链（`FoodWorkshop`／`Ranch`）

**命中依据（实读）**：两栋资产 `isPlayerBuilt: 1`、`moduleType: 1`（＝`ModuleType.Production`·枚举 `Kingdom/ModuleType.cs:7-14`）＋ `Module_Production.asset:26-36`（tier2 `requiredCastleLevel: 2`、`unlockBuildings: [FoodWorkshop, Ranch]`）。

**创建链（菜单 → 放置 → 挂件）**：

1. `BuildingMenuPanel.EnsureDefsLoaded:204-211` —— `Resources.LoadAll<BuildingDef>("Buildings")` 中**筛 `def.isPlayerBuilt`** 装满 `_allBuildable`。
2. `BuildingMenuPanel.RebuildList:228-235` —— 逐 def 过三重门：`def.moduleType == _currentTab`（`:230`）／`def.raceId >= 0 && != playerRace`（`:231`）／`KingdomManager.Instance.IsBuildingUnlocked(def)`（`:233`）。
3. `KingdomManager.IsBuildingUnlocked:233-244` —— `ResolveModule(def):255`（读 `def.moduleType`）＋ `FindSpecialUnlockTier(def.id):278`（扫 `ModuleDef.tiers[].unlockBuildings`）⇒ 两栋为**特殊建筑** ⇒ 需 **Production 模块 ≥ tier 2**（`Module_Production.asset:26-27` 的 `requiredCastleLevel: 2` 为前置）。
4. `BuildingMenuPanel.BuildCard:267-273` —— 按钮 `build-{def.id}`；置灰＝`WarehouseHelper.CanAfford(def.cost) && !IsUniqueBuilt(def)`。
5. `BuildingMenuPanel.OnBuildClicked:392-411` —— 复检 `CanAfford` ⇒ `UIManager.Push(new BuildModeEntry(def, this), …)`（`:406`；`UIManager` 为 null 时兜底 `BuildController.Instance.EnterBuildMode(def)` `:410`）。
6. `BuildModeEntry.cs:8-17`（虚拟栈条目）→ `BuildController.EnterBuildMode:61` → 放置段 `BuildController.cs:266-329`（`:284` `b.Init(def, coord, true, fp)`；⭐ **`:329` `BuildingFactory.Instance.AttachComponents(b, def)`**）。
7. ⇒ 两栋因**无 `components` 栏** ⇒ `AttachComponents` 早退（`BuildingFactory.cs:246-247` 空数组 return）⇒ **不挂任何行为组件**（与改前一致：改前 9 处 `if` 对二者亦全不命中）。

**其余四栋**：`rift`／`portal`／`ruins`／`treasure_box` 的 `isPlayerBuilt` 全 0 ⇒ ⛔ 不在菜单候选集；地图生成／AI 预置／AI 行动／存档四面实读均无创建路径（§B-2）。

---

## 六、缺口是否还在

**仍在**，但形状已变（三句）：

1. **"挂不上"的缺口已闭**：`PickupComponent`／`RiftComponent` 现可由**数据行**挂上（4 栋／2 栋），`CastleCoreComponent` 一直活（`castle` 数据行 ＋ 来源守卫）—— ⛔ **但 `Init` 仍全空**（`:32`／`:164`）⇒ **功能缺口仍在**（挂上＝零行为）。
2. **`SpawnerComponent` 仍零挂载**：既无数据行键（键表 9 键无 spawner），亦无生产码引用（全库仅类定义 1 处）。
3. **"六栋＝死资产"已不成立于其中两栋**：`FoodWorkshop`／`Ranch` 进了**模块解锁链 ＋ 建造菜单可达**（§五）；`rift`／`portal`／`ruins`／`treasure_box` 仍无创建路径（四面实读 0）。

---

## 七、零改动声明

- **本批只读**：⛔ 未写任何 `.cs`／`.asset`／`.unity`；⛔ 未跑探针／未进 Play；⛔ 未改 `08`／`中层执行计划`；⛔ 未 commit／未 push。
- **落盘物（非 Assets 面）**：本报告 1 份 ＋ `多Agent交接/_编号登记.md` **2 处**（水位线号 `330→331` ＋ `HH.331` 在途行）。
- **取证**：`git status` 复核 ⇒ `Valley Rampart/Assets/**` 面**仅 1 项**改动＝`Scenes/GameScene.unity`（**前序脏点 `5/1`·非本批**）；写本报告前 40 分钟内被写的文件仅 `.codebuddy/memory/MEMORY.md`（会话记忆维护）＋`多Agent交接/_编号登记.md`（本批取号）⇒ **零代码／零资产写**；本报告本身为**非 `Assets` 面**新增件。
- **基线**：`e7bb8696`（`c43f9fff`／`D864` 已把 `M4-A` 收编入基 ⇒ 本报告所有 `file:line` 即**已入库现状**）。

---

## 八、列报 / 待裁（4 条）

| # | 事项 | 事实（⛔ 不出方案） |
|---|---|---|
| 1 | ⭐ **停手 3 已命中** | `FoodWorkshop`／`Ranch` 可被玩家建造菜单创建（需 Production ≥ tier2）⇒ `M4-C` 若涉及这两栋，**须先接创建链**（§五）；⛔ 本批未出删除方案 |
| 2 | ⚠️ **`portal.asset` 的 `comp.rift` ＋ `sourceType: 10`** | 若将来给 `portal` 接创建路径且以 `def.sourceType` 建 ⇒ 会挂 `RiftComponent`（`08` #36 所述"误挂"在 `M4-A` 后仍成立·见 §A-3） |
| 3 | ⚠️ **`isConsumable` 已成孤儿** | 唯一生效消费者随 `M4-A` 删除；`CreateBuildingInstance` 的 `isConsumable` 形参 `BuildingFactory.cs:90` **体内零使用（死参）**；余读点仅调试面 `AIDebugSpawnController.cs:513`（§D-4） |
| 4 | ⚠️ **guid 引用面不可用字符串法判定** | 本项目 `.asset` 引用＝32hex／meta＝base64（1228 份实读）⇒ `测试基线台账.md:4028-4033`「guid 零引用」按 base64 实读成立，但**不可外推**（§B-3）；`BuildingMappingTable.asset` 11 条 `def` 引用的可解析性 **未核**（须引擎侧解析·另批） |

## 策划裁决（D865）

实核采信。不放行删除。食品工坊与牧场划出删除名单。全文：`多Agent交接/策划端/HH.331_M4-C开片前实核_裁决.md`。
