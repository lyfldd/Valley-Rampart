# HH.307 · HH.294 底层重构批「片 6-2 · 双写收敛」交付报告

> **端**：执行端｜**日期**：2026-09-17｜**任务书**：`多Agent交接/策划端/HH.294_底层重构批_任务书.md` §二 片 6 ＋ 派工提示词（`D777`）＋ **报裁裁决 `D778`**
> **口径源**：`最高优先级文档/03_地图即数据库.md`（§6.7／§7／§8／§9）＋ `04_上层接入指南.md`（五条不许）＋ `底层执行计划.md` §二 批 5-A/5-B
> **证据落盘**：`Valley Rampart/Logs/hh294_slice6_2/hh294_slice6_2_probe.txt`（稳定名·**含第 1 段与第 2 段**：末次运行覆盖为第 2 段）＋ 各时间戳副本 ＋ `hh294_slice6_2_hash.txt`
> **正门**：`TestHarnessApi.EnterTestRun`（seed=29418／槽 `hh294_slice6_2`）→ 探针跑读 → 真暂停 `timeScale=0` ＋ `Save` ＋ `ExitTestRun` ＋ 退 Play（L-32 三态）；两段两次进局，均三态收尾（实测 `isPlaying=False`）
> **报裁闭环**：`HH.306`（`fef82cdc`）4 项必裁 ＋ 1 报备 → `D778` 全裁 ⇒ 本报告按裁施工（含 B-4 七处完整清单）

---

## 〇、改动清单（**24 文件** ＝ 22 改 ＋ 1 新建探针 ＋ 其 `.meta`；`+482/−281` ＋ 探针 `+834`）

| # | 文件 | 改前 → 改后 |
|---|---|---|
| 1 | `Assets/Resources/Config/RespawnConfig.asset` | `+3` ⇒ 新增 `oreVeinGatherSeconds: 8` / `stonePileGatherSeconds: 4` / `woodPileGatherSeconds: 2`（**逐字段 diff 见 §十二**） |
| 2 | `Assets/_Game/Data/RespawnConfig.cs` | `+31` ⇒ 三具名字段 ＋ **按 feature 查表 helper** `GatherSecondsOf(feature)`（B-1 裁「甲」） |
| 3 | `Assets/_Game/Systems/World/MapGenRules.cs` | `+13` ⇒ **对照开关** `SpawnResourceEntities = false` ＋ `DeriveNaturalBuildings` 门控（6-A） |
| 4 | `Assets/_Game/Systems/Building/BuildingFactory.cs`（**CRLF 445 行保持**） | `+21/−27` ⇒ 自然建筑循环受开关门控／`ReSpawnNaturalBuilding` 加守卫退役／三型映射标旧路径／**`ReturnBuildingToPool` ＋ `_pool` ＋ 无用 using 删除**（B-4⑤） |
| 5 | `Assets/_Game/Systems/World/ResourceRespawnSystem.cs` | `+68/−23` ⇒ `ConfirmTreeGather` → **`ConfirmResourceGather`**（四型）＋ **`PrioritizeHarvestCommand` 消费者**（Awake 订阅／OnDestroy 退订）＋ `HandleTreeGathered`/`HandleEntityDepleted` → **`HandleCellGathered`**（两链合一·含游荡锚点承接）＋ `SpawnEntityFor` 门控 |
| 6 | `Assets/_Game/Systems/World/WorldGatherSource.cs` | `+59/−58` ⇒ `ForEntity`/`ForTree` → **`ForCell`**（四型同源）＋ **`TryResourceOf` 单一映射**（L-31）＋ 字段简化（`_feature` 替代 `_entity`/`_isTree`） |
| 7 | `Assets/_Game/Systems/World/WorldGatherRegistry.cs` | `+27/−31` ⇒ `TryMatchPoint` **只按格表**（实体分支删）＋ `Advertise` 走 `ForCell` ＋ `Entry.IsTree` 维度消除 |
| 8 | `Assets/_Game/Systems/World/TreeGatherSource.cs` | `+3/−1` ⇒ 完成回调改 `HandleCellGathered`（类本体**保留**·现无生产调用方·见 §十四残余） |
| 9 | `Assets/_Game/Systems/Building/Building.cs` | `+23/−73` ⇒ **采集面退役**：`isBeingGathered`／`OnGatherCompleted`／`FeatureOf`／`StartGather`／`TryAdvertiseTask` 采集分支 |
| 10 | `Assets/_Game/Systems/Building/BuildingPanel.cs` | `+9/−21` ⇒ `def.isConsumable` 采集按钮分支（成死码）＋ `GatherAmount()` ＋ `OnGatherClicked` 退役 |
| 11 | `Assets/_Game/Systems/Building/PlacementValidator.cs` | `+13` ⇒ ⭐ **占格承接**（8th·§十三明列）：三型一次性资源格 ⇒ 普通建筑 `Blocked`（改前由实体占格承担） |
| 12 | `Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs` | `+16/−9` ⇒ **B-4①** `isBeingGathered` 复位删；**B-4②** `comp is Building` Gather 分支删；注释口径同步 |
| 13 | `Assets/_Game/Systems/AI/Stimulus/WanderStimulusProvider.cs` | `+48/−7` ⇒ **6-E/B-2b**：`OreVein` 格表来源（**只 OreVein**·静态共享 12s 缓存·非每帧） |
| 14 | `Assets/_Game/Systems/AI/WanderAnchorPool.cs` | `+3/−2` ⇒ 锚点来源注释承接改写 |
| 15 | `Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs` | `+3/−1` ⇒ ㉗ 门槛注释口径勘正（6-B 同步） |
| 16 | `Assets/_Game/Systems/World/MapGate.cs` | `+79/−3` ⇒ **搭车 R1**：`PickCellRadius` 由 `const 1` → **按运行时 sprite 半高重推**（懒算＋缓存＋上限） |
| 17 | `Assets/Editor/Lifecycle/LifecycleAudit.cs` | `+7/−2` ⇒ **B-3**：`PrioritizeHarvestCommand` **移出豁免表 `:51`**；`:124`（54 事件基线）**保留** |
| 18 | `Assets/Editor/ChainAudit/Validators/R3_SupplyChain.cs` | `+13/−5` ⇒ **B-4⑥**：供给口径同步（一次性资源 Gather 路径 ＋ Wood 供给点） |
| 19 | `Assets/Editor/Smoke/Valley2_17_Smoke_FixCard.cs` | `+32/−15` ⇒ **B-4⑦**：α 段**改走数据路径**（格表落 OreVein ＋ `ConfirmResourceGather`） |
| 20 | `Assets/Editor/Smoke/Valley_HH294_Slice5Probe.cs` | `+6/−3` ⇒ ⭐ **清单外第 8 个调用点**（片 5 探针调 `HandleTreeGathered`/`HandleEntityDepleted` ⇒ 改 `HandleCellGathered`；否则编译失败） |
| 21 | `Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs` | `+2` ⇒ 探针同步注（`DeriveNaturalBuildings` 语义） |
| 22 | `Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs` | `+3` ⇒ 同上（`SameMap`/`nb` 判读语义不变说明） |
| 23 | `Assets/Editor/Smoke/Valley_HH294_Slice6_2Probe.cs`（**新建·834 行**） | 本批验证探针（§A~§H·两段式） |
| 24 | `…Slice6_2Probe.cs.meta` | Unity 生成 |

> ⛔ **未碰**：`AI.Core`（零命中）／中层五项（`05`/`06`/`07`/`08`/`#14`）／片 6-1 已闭项（`Collider2D`／`PickAt` 改道／掩码／框选）／`F-14`（单列片 6-3）／并行会话改动（美术 `Ground/*`／`pixel-forge`／`GameScene.unity`／`Packages/*`／`3.6`·`3.8` doc／`Valley_HH284_Probe.cs`／`Valley_HH289_*`／`Valley_HH290_*`）。

---

## 一、判据 1（⭐ 生成一张图 ⇒ 三型 `Building` 实例数 = 0）

**A/B 两读数（`SpawnResourceEntities` 对照开关·同一 build 内跑两段）**：

| 段 | 开关 | `BuildingRegistry` 总数 | OreVein | StonePile | WoodPile | 三型合计 |
|---|---|---|---|---|---|---|
| 第 1 段（**目标态**） | false（默认） | **21** | 0 | 0 | 0 | **0** ✅ |
| 第 2 段（**对照段·改前旧路径**） | true | **1854** | **438** | **697** | **698** | **1833** |

- 能力句：「生成一张图 ⇒ 三型 `Building` 实例数 = 0」✅（目标态段实测 0）。
- **存在性反证**：同 seed 同图、仅开关不同 ⇒ 对照段三型 **1833 个**（`Building_wood_pile_19_2` 等逐名抽样在场）⇒ 探针**能**检出实体（非"恒 0 假绿"）✅。
- 「改前/改后 `BuildingRegistry` 总数」＝ **1854 → 21**（Δ = **−1833**，与三型计数逐值吻合）✅。
- 类目对照：`mine` 建筑两段均 **3**（AI 立国预置·`mine` 不在本批范围）；主城/围墙/功能建筑仍在 ⇒ 差值**全部**来自三型。

## 二、判据 2（⭐ 资源格的存在性只由格表决定）

**探针读数**：三型资源格抽样 **20 格 ⇒ `GetOccupant != null` 的 = 0** ✅（资源格不占格）；可走面抽样 5 格 `IsWalkable` 全 true（feature 派生·与改前一致）；`isObstacle=0` ⇒ 改前亦不写 `BuildingBlocked` ⇒ **可走面零变化**。

**「资源格不占格」不引入新空洞（`D778` B-4 另注）—— 实测发现 1 处并已承接**：

- ⭐ **发现**：改前「普通建筑不可压在一次性资源格上」的阻挡**由实体占格承担**（`PlacementValidator:93-100` 的 `occupant != null ⇒ Blocked`）；多数建筑（`House`/`farm`/`Well`/`quarry`…）`allowedTerrain` **为空** ⇒ 地形校验不拦 ⇒ 6-A 后若不补判，则**可压在资源格上**（净新增能力＝新空洞）。
- ✅ **承接（本批第 8 处改动·明列）**：`PlacementValidator` 增**格表判定**（`OreVein`/`WoodPile`/`StonePile` 三型 ⇒ `Blocked`；`Tree`/`Mine` **不纳**＝改前即无实体/本就允许 ⇒ 零行为变更）。
- **A/B 实测**：目标态「House 压 OreVein 格 (4,9)」⇒ `ok=False reason=Blocked` ✅；对照段「House 压 ore_vein **实体**格 (183,2)」⇒ `ok=False reason=Blocked` ✅ ⇒ **两段同结果＝净零行为变更**。
- 另：**放置阻挡以外的占格释放面**（改前 `ReleaseLayerOwnedState(unregisterFromRegistry:true)`）——改后**零实体 ⇒ 零占用 ⇒ 无对象可释放**，故无残留；全图占格数 = 建筑数（§一 两段读数差 = 三型实体数，逐值吻合）⇒ **无孤立占格残留**。

**grep 证据（生产码零调用/零残留）**：

```
$ grep -n 'DeriveNaturalBuildings|InstantiateFromMap|ReSpawnNaturalBuilding|SpawnEntityFor' -r Assets --include=*.cs
Assets/_Game/Systems/World/WorldManager.cs:202:  MapGenRules.DeriveNaturalBuildings(map);   // 步骤11（生成管线自身·按设计保留）
Assets/_Game/Systems/World/WorldManager.cs:142:  BuildingFactory.Instance.InstantiateFromMap(playerMap);
Assets/_Game/Systems/World/ResourceRespawnSystem.cs:430:  BuildingFactory.Instance.ReSpawnNaturalBuilding(coord, feature);  ← 唯一调用点，其唯一调用方 SpawnEntityFor 已被开关守卫
Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs:56 / Valley_HH291_MapGenProbe.cs:70:  （Editor 探针·已同步加注）
（其余命中＝注释文本）
⇒ 自然建筑派生面**默认路径零触达**：DeriveNaturalBuildings 首行 return（`!SpawnResourceEntities`）、InstantiateFromMap 循环受同门控、ReSpawnNaturalBuilding 自带守卫。
```

## 三、判据 3（⭐ 6-C 玩家入口：全工人 + 右键资源格 ⇒ 该格进入采集）

| 型 | 格 | 发布命令 | 工人状态 | 进入采集 |
|---|---|---|---|---|
| `Tree` | (179,5) | True | **MovingToSource** | ✅ |
| `OreVein` | (92,5) | True | **MovingToSource** | ✅ |
| `StonePile` | (95,5) | True | **MovingToSource** | ✅ |
| `WoodPile` | (14,5) | True | **MovingToSource** | ✅ |

- 走**生产同一条链**：探针按 `SelectionController.cs:288-296` 同形发布 `PrioritizeHarvestCommand`（含工人表）→ `ResourceRespawnSystem.OnPrioritizeHarvest` 消费 → `ConfirmResourceGather` 立案 → 调度器派工。
- **存在性反证**：§C 先读 `EventBus.HasSubscribers<PrioritizeHarvestCommand>()` —— 在场 **True**（改前＝false·零订阅死码）。
- ⚠️ 施工中修正两处（**探针侧**，非生产）：① 命令落点须用**格内点**（菱形中心），`CoordToWorld(cell)` 是「菱形**下顶点**」＝4 格共享角，`WorldToCoord` 的 floor 在浮点边界会落邻格（首跑实测 (179,5)→(179,4)）⇒ 真实玩家点击落在菱形内部，无此歧义；② 首跑 Tree 丢失系 `TreeGatherSource`「一发即失效」与调度器 `!IsValid ⇒ Abandon` 的**已知竞态** ⇒ 玩家入口**统一改走 `WorldGatherSource`**（源有效直至完成·玩家树链改前是零调用死码 ⇒ 无既有行为可回归）。
- ⚠️ **口径收紧（需验收确认）**：接受面＝**树 ＋ 一次性三型**（`WorldGatherSource.IsHarvestFeature`），**不含 `Mine` 锚点**——`Mine` 无产出资源映射，纳入会新增「玩家可采矿山锚点」行为漂移。

## 四、判据 4（⭐ 采集完成 ⇒ 格翻 `Plain` ＋ 池子 −1）

- 格 (93,6)：`OreVein` ⇒ **`Plain`** ✅；池子（区块 ci=5·类 `OreVein`）：**6 → 5**（Δ **−1**）✅（前后读数·`PointsOf` 生产读口）。
- **反证/幂等**：第二次 `HandleCellGathered` ⇒ 池子仍 5（无二次扣减）✅；删门存在性校验幂等 ✅。
- **守卫面幂等（`D778` 清洁项要求的证据）**：部署守卫 1 区域 → `HandleCellGathered` ⇒ 区域 0（Δ−1）＋ `GuardRegionLostEvent` **1 次**；再直调 `HandleResourceConsumed` ⇒ 区域**仍 0**、事件累计**仍 1** ⇒ **`HandleResourceConsumed` 幂等成立**（实现＝按 coord 查 `_nodes` 命中即删并 return；二次调用无匹配 ⇒ 零效果）。⇒ 改前 3 处调用中后两处为**纯重复调用**，删除**零行为影响**。

## 五、判据 5（`PrioritizeHarvestCommand` 消费者在场 ＋ `LifecycleAudit` 白名单移出）

- 消费者原文行（`ResourceRespawnSystem.cs`）：

```csharp
        EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);   // 4-A：时间基准＝游戏天·每天推进一次
        // 【HH.294 片 6-2·6-C（`D778` B-5 落点）】玩家采集入口消费者：…
        EventBus.Subscribe<PrioritizeHarvestCommand>(OnPrioritizeHarvest);
```
  退订对偶：`OnDestroy` → `EventBus.Unsubscribe<PrioritizeHarvestCommand>(OnPrioritizeHarvest);`（ 无 `Start()` 订阅）。
- `LifecycleAudit`：**已移出 `:51`（豁免表 `ExemptEvents`）**，**保留 `:124`（54 事件基线数组）**：

```
$ grep -n 'PrioritizeHarvestCommand' Assets/Editor/Lifecycle/LifecycleAudit.cs
（改后）0 命中豁免表；基线数组内仍在 ⇒ 与「扫描事件集」对账不产生假 +extra
```
- 运行时在场读数：§C `HasSubscribers = True` ✅（改前 false）。

## 六、判据 6（⭐ 6-E：两处 AI 消费面的**改动前/后对照读数** ＋ 语义保持声明）

| 消费面 | 改前来源 | 改前读数 | 改后来源 | 改后读数 | 处置 |
|---|---|---|---|---|---|
| `KingdomBrain.cs:471` `gatherNodes` | `def.isResourceNode && IsActive`（**按国过滤**·三型实体 `kingdomId=-1` **本就不计**） | 3（＝mine·两段同） | 不动 | 3 | **不动**（`D778` B-2a 撤回） |
| `WanderStimulusProvider.cs:184` `resourceSites` | `isResourceNode` 建筑（**无国过滤**·含 ore_vein 实体） | **441**（OreVein 438 ＋ mine 3） | **格表 `OreVein` 格** | **437**（该时点·已含 §D 消耗 1 格） | **新增格表来源**（B-2b） |

- **语义保持声明**：`ore_vein` 实体与 `OreVein` 格 **1:1**（改前每格派生 1 实体）⇒ 438 实体 ⇄ 438 格，格表来源**等量承接**；差值 1 系探针自身 §D 消耗（读数时序差）。
- ⛔ **只 `OreVein`**：`stone_pile`/`wood_pile`（`isResourceNode=0`）**不纳入**——`DZ-054`/`D617` 已裁「三资产值不齐＝语义差异非缺陷·统一会造成游荡锚点漂移」，纳入＝撞已裁。
- **存在性反证**：格 (4,9) 世界位 (−3.2, 4.2) **在候选中 = True** ✅。
- **代价读数（`03` §8.7 判据 7）**：生产实现（反射直调 `AppendOreVeinCells`）**首扫 0.284 ms**（全图 features 扫 → 438 候选）／**12s 缓存内第二扫 0.032 ms**（免重扫）⇒ **非每帧、非每 NPC**（静态共享缓存·换图即失效）。

## 七、判据 7（⭐ 搭车 `R1`：候选窗口半径重推）

- `MapGate.PickCellRadius` = **7**（格·改前 `const 1`）⇒ 候选窗口 **15×15**；上限 `PickCellRadiusMax=8`（成本上界 (2×8+1)²＝289 次 O(1) 字典查·写进接口旁注释）。
- **算法**：`ceil(maxSpriteHalf ÷ 半格高)`；取样＝**运行时实体** sprite（`BuildingRegistry` 的 `SpriteRenderer`·含 `lossyScale`）——⚠️ **不是** `BuildingDef.prefab`（真图经 `SpriteRefTable` 旁挂·多数 def `prefab` 为空 ⇒ 读 prefab 会得 0·**本批实测踩过并修正**）。懒算 ＋ 缓存（换图/`cellSize` 变即失效·10s 粘性重扫）。
- **存在性反证**：点 `castle` sprite **上沿** (−63.36, 79.79)（半高 2.19 世界单位·距其 footprint 主格 **7 格**）⇒ `PickAt` 命中 **`Building_castle_68_169`** ✅（改前 r=1 必落空 ⇒ 能力句原缺口坐实）。
- **代价读数**：单次 `PickAt`（castle 上沿点·r=7）＝ **0.0180 ms**（2000 次均值·命中 2000）＝≈**0.11%** 帧预算 @16.6ms；空白点 0.0187 ms；半高重扫 **0.038 ms/次（≥10s 一次）** ⇒ 摊销可忽略。

## 八、判据 8（⭐ 搭车 `R2`：独立渲染对照 ≥2 例）

**方法（`L-30`「反证须有鉴别力」＋`D776` 判据 6 证据要求）**：**RT 像素隐藏法** —— 在重叠点集上做 3 次 RT 采样（both／隐藏 A／隐藏 B），
「**隐藏谁 ⇒ 该点像素变**」者＝**像素实测最前者**（**外部观测**：真实渲染像素，不含任何拾取键/常量）；与 `PickAt` 返回者比对。

| 用例 | 实体对 | 像素读数列（节选） | 拾取返回者 | 像素实测最前者（外部观测） | 一致 |
|---|---|---|---|---|---|
| ① **`Pivot`**（单位-单位） | `Human_Player_Vagrant`(t=77.62) vs `Human_Player_Vagrant`(t=77.42) | `#0(-0.13,77.62) both=(0.05,0.10,0.01) hideA=(0.58,0.83,0.26)(变) hideB=(0.05,0.10,0.01)(不变) ★` | `Human_Player_Vagrant` | **`Human_Player_Vagrant`** | ✅ |
| ② **跨层带**（宝箱 5 ＞ 建筑 1） | `Chest`(order=5·layer=Default) vs `Building_castle_68_169`(order=1·layer=Default) | `#0(-0.43,78.98) both=(0.78,0.75,0.33) hideA=(0.41,0.55,0.41)(变) hideB=(0.78,0.75,0.33)(不变) ★` | `Chest` | **`Chest`** | ✅ |

- 两例**首点即结论明确**（★ 标记）⇒ 判据成立；两列**非同源**（外部列＝像素采样，被测列＝`PickAt`）。
- 施工中修正 2 处（探针侧）：① 实体须**冻结/同帧**采样（NPC 漂移会把 0.2 偏移放大到 0.53 ⇒ bounds 不交）；② 单点法有「中心恰为透明像素」坑 ⇒ 改 **3×3 多点扫描** ＋ 落盘逐点读数（可复算）。

## 九、判据 9（清洁项：守卫重复调用合一）

- **3 处落点**（改前）：① `MapGate.RemoveResourceNode:308`（门内）② `ResourceRespawnSystem.HandleTreeGathered:477` ③ `Building.OnGatherCompleted:920`。
- **改后原文（唯一处）**：

```csharp
        if (!SetFeature(coord, FeatureType.Plain)) return false;
        // 守卫锚点语义（HH.3 §六 / HH.6 裁决二）：资源点被采走/覆盖 ⇒ 守卫区域失去覆盖
        GuardDeploymentSystem.HandleResourceConsumed(coord);
        return true;
```
  ②③ 随各自路径退役（`HandleTreeGathered`/`OnGatherCompleted` 已删）⇒ **全库 `HandleResourceConsumed` 调用点 = 1（门内）**（另 2 处为探针/定义）。
- **幂等证据**：见 §四（双调 ⇒ 区域与事件数均不变）⇒ 删重复调用**零行为影响**。

## 十、判据 10（`isConsumable` 消费者清单 ＋ 逐条处置·改后 grep 片段）

```
$ grep -n 'isConsumable' -r Assets --include=*.cs（生产码摘录）
Assets/_Game/Data/BuildingDef.cs:90        字段定义（保留·资产仍标 1）——⛔ 未动
Assets/_Game/Systems/Building/BuildingFactory.cs:105  isConsumable: def.isConsumable（自然建筑循环·**受开关门控**＝旧路径）
Assets/_Game/Systems/Building/BuildingFactory.cs:156  isConsumable: true（ReSpawnNaturalBuilding·**已加开关守卫**）
Assets/_Game/Systems/Building/BuildingFactory.cs:320  if (def.isConsumable) AddComponent<PickupComponent>（组件为**空壳**·`BuildingComponents.cs:22 Init{}` ⇒ 零行为；随实体退役后仅死资产 treasure_box 可命中）
Assets/Editor/ChainAudit/Validators/R3_SupplyChain.cs:124 审计供给口径（**已同步**为数据寻址链）
（改前消费点已删：Building.cs 3 处／WorldGatherRegistry.cs:168／BuildingPanel.cs:478）
```
**逐条处置**：① 字段**保留**（`D778` 明示）；② 两处工厂传参＝旧路径（随开关/验收后删）；③ `PickupComponent` 挂载＝空壳（保留·零行为）；④ 审计口径＝已同步；⑤ `AIDebugSpawnController.cs:502`（调试面）**未动**（按裁决）——仅其 `:474` 注释「玩家点击 `StartGather` 触发」成**陈注释**（列残余）。

## 十一、判据 11（常规）

| 项 | 读数 |
|---|---|
| 编译 | **0 error / 0 新增 warning**（`refresh_unity` ＋ `read_console` 逐条核：warning 面 24 条**全为存量**（IUIPanel／CameraSetup／…／`Valley2_17_Smoke_5` 等·改前同集）；期间自犯 1 条 `CS0219`（探针未用变量）**已当场清除**） |
| 反射坐实（编译通过的第二证据） | `GatherSecondsOf=True／ForCell=True／ForEntity=False／ConfirmResourceGather=True／ConfirmTreeGather=False／HandleCellGathered=True／HandleTreeGathered=False／Building.isBeingGathered=False／StartGather=False／OnGatherCompleted=False／ReturnBuildingToPool=False／SpawnResourceEntities=True` ＋ SO `oreVein=8 stonePile=4 woodPile=2` |
| 同 seed 逐格一致 | 两段两次独立建局 hash 均 **`9424A5D99D9C3543`**（`features`＋`climateZones` FNV 双混合·seed=29418）⇒ **逐格一致** ✅（且对照段开关不影响生成 ⇒ 影响面仅"派生实体"） |
| `AI.Core` 命中 | **0**（改动 24 文件全在 `Assets/_Game/**` 与 `Assets/Editor/**`；`AI.Core/**` 零触碰） |
| 改动文件行尾 | 22 改 + 1 新：**LF**（探针 `LF 833`）；`BuildingFactory.cs` **保持 CRLF 445 行**（改前 451 ⇒ 净 −6 行·裸 LF=0·python 二进制按行替换·未用 Edit/Write） |
| 正门三态 | 两段均 `EnterTestRun` → 真暂停 `Time.timeScale=0` ＋ `Save(hh294_slice6_2)` ＋ `ExitTestRun` ＋ 退 Play；实测 `isPlaying=False` ✅；开关复原 `SpawnResourceEntities=False` ✅ |

## 十二、判据 12（SO 资产逐字段 diff 面 ＋ 改动文件清单）

**`Resources/Config/RespawnConfig.asset` 逐字段（`git diff` 原文）**：

```
   enabled: 1
   treeGatherSeconds: 2          ← 未动
   treeGatherAmount: 5           ← 未动
+  oreVeinGatherSeconds: 8       ← 新增（＝改前 ore_vein.asset:65 gatherSeconds=8）
+  stonePileGatherSeconds: 4     ← 新增（＝改前 stone_pile.asset:65 gatherSeconds=4）
+  woodPileGatherSeconds: 2      ← 新增（＝改前 wood_pile.asset:65 gatherSeconds=2）
```
⇒ **`+3/−0`**（既有面无一处改动）；三值均**逐位取自改前实盘**（`D778` B-1 裁「甲」·非猜）。
**改动文件清单**：见 §〇（24 文件）；`git status` 显示的本批外文件（美术/pixel-forge/GameScene/Packages/doc/`Valley_HH284_Probe.cs`/`HH289`/`HH290`）**一律未碰**。

---

## 十三、偏离与自决项（须验收裁断·诚实列出）

| # | 项 | 说明 |
|---|---|---|
| 1 | ⭐ **占格承接（清单外第 8 处改动）** | `D778` B-4 另注要求「证明不引入新空洞」；实测**确实存在空洞**（普通建筑可压资源格）⇒ 按承接语义补 `PlacementValidator` 格表判定（§二）。**理由**：这正是 `D778` §四-3 教训「退役某成员必须含其语义承接方」的同族补全；效果＝**净零行为变更**（A/B 双段同 `Blocked`）。⚠️ 若验收认为超范围 ⇒ 3 行可回退（并须另立批次补）。 |
| 2 |  **清单外第 9 个调用点** | `Valley_HH294_Slice5Probe.cs:516/539` 调已删的 `HandleTreeGathered`/`HandleEntityDepleted` ⇒ **不改则编译失败** ⇒ 已改 `HandleCellGathered`（与 B-4⑦ 同性质） |
| 3 | **玩家入口接受面不含 `Mine`** | `D778`/提示词未明列；`IsRemovableResourceFeature` 含 `Mine` 但锚点无产出映射 ⇒ 纳入＝新增漂移 ⇒ **收紧**为树＋三型（§三） |
| 4 | **玩家树路径改走 `WorldGatherSource`** | 改前玩家树链＝零调用死码（无既有行为可回归）；`TreeGatherSource` 一发即失效有**已知竞态**（实测首跑丢失）⇒ 取鲁棒形态（§三） |
| 5 | **游荡锚点登记只对一次性三型** | 改前仅**实体链**登记（`Building.OnGatherCompleted:913`·唯一调用点）／树链不登记 ⇒ `HandleCellGathered` 内**逐型保原口径**（树不新增）⇒ 避免游荡行为漂移。若裁「统一注册（含树）」⇒ 1 行可改 |
| 6 | `R3_SupplyChain` 供给点改为 `WorldGatherSource` | 与 B-4⑥ 同性质的口径同步（Wood 供给点原锚 `TreeGatherSource.cs:44`·该类现无生产调用方） |

## 十四、残余与观察项（**显式列出·不隐瞒**）

| # | 项 | 状态 |
|---|---|---|
| 1 | **`TreeGatherSource` 死类** | 现**无生产调用方**（类本体未删·`TaskScheduler:635` 分支同）；建议随「验收后删旧路径」一并清（删类＋分支 2 处） |
| 2 | `TaskScheduler.GetTaskDuration` 的 `Building` 覆盖分支（`:1134`） | 结构性不可达（Gather 源已无 Building）·**零行为影响**·未在裁决清单内 ⇒ 保留＋注释，随旧路径清理 |
| 3 | `AIDebugSpawnController.cs:474` 陈注释（「玩家点击 `StartGather` 触发」） | 调试面按裁决**不动** ⇒ 注释已过时（同文件 `:502` 传参仍是旧语义） |
| 4 | `Smoke_2_23RB.cs:162` 构造 `TreeGatherSource` 作「玩家采集真实载体」探针 | 语义前提已过时（新载体＝`WorldGatherSource`）·编译与行为不受影响 ⇒ 列残余 |
| 5 | 片 6-1 探针文本（`Valley_HH294_Slice6PickProbe.cs:593`）含「`PickCellRadius=1`」字样 | 历史读数文本·⛔ 片 6-1 已闭项不碰 ⇒ 仅提示勿复引 |
| 6 | `RespawnConfig` 参数只有**探针构造读数**，未做「玩家手感」长局观测 | 8s/4s/2s 逐位保原值 ⇒ 手感不变（零行为变更）；长局验证归验收可选 |
| 7 | §G 用例样本 n=2 例（1 Pivot ＋ 1 跨层带） | 按 `D778` R2 要求「各 ≥1 例」满足；更多样本可扩（成本低） |
| 8 | 片 5/片 6-1 探针连带改（Slice5Probe）后**未重跑**其全量判据 | 只改调用名（同实现）＋ 文本；如需全量回归请指示（片 5 探针须正门进局跑批） |
| 9 | 探针 §C 会临时生成/销毁测试工人并立案（世界侧留下 1~4 个采集源/若干格被采） | 探针纪律：测毕销毁＋放弃任务；世界为探针槽（不入正式局） |

## 十五、应登记项（`D767` 纪律：不代写策划端账本，仅声明）

1. `_编号登记.md`：HH 水位线 **HH.305 → HH.306（报裁）→ HH.307（本报告）**
2. `_任务队列.md`：片 6-2 行 ⇒ 交付待验收（`HH.307`）
3. `_当前快照.md`：`D778` 裁决行 ＋ 片 6-2 交付行
4. 台账新增节（§四十九／§五十）：`D778` 裁决落账 ＋ 本批读数与**8/9 两处清单外承接**
5. 教训库候选：`L-15` 家族 ＋1（**「退役某成员」的调用点清单本批又漏 2 处**：片 5 探针／`R3_SupplyChain` 供给点；与 `D778` §四-3 同族）；`L-40` ＋1（**参数值须直读存储处**：`gatherSeconds` 在 `.asset`，本批 B-1 已按此办）；方法学 ＋1（**RT 像素隐藏法**＝可复用的"外部观测"拾取判据；单点须防"中心透明像素"）
6. 契约侧（请策划端落）：`03` §8.7 判据 6 证据规范可补「像素隐藏法」为推荐外部观测；`03` §6.2/§7 可补「资源格不占格 ⇒ 放置阻挡改由格表承担」一句（本批实锚）

## 十六、commit

本报告与施工改动**同串 commit**（具名 `git add`·逐文件列名；不含任何并行会话改动文件）；**不 push**。

**证据文件（gitignore 域就地留档·不入 commit）**：`Valley Rampart/Logs/hh294_slice6_2/hh294_slice6_2_probe.txt`（末次为第 2 段全量）＋ 各时间戳副本 ＋ `hh294_slice6_2_hash.txt`（`9424A5D99D9C3543`）。