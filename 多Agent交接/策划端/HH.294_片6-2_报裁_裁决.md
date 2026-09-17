# HH.294 片 6-2「双写收敛」· 口径报裁裁决

> **端**：策划端（主策划·砚）｜**日期**：2026-09-17｜**裁决号**：`D778`
> **报裁来源**：`多Agent交接/执行端/HH.306_HH294_片6-2_双写收敛_口径报裁.md`（`fef82cdc`·1 文件·未 push）
> **口径源**：`03` §6.7／§7／§8｜`04` 五条不许｜台账 §四十八（`D777` 片 6-2 派工）
> **纪律**：本裁决**未采信任何自述** —— `B-1`~`B-5` 五项**全部由本端实读复核**（资产文本／`git grep -n`／源码原文）。

---

## 一、总判

**执行端停手正确；4 项报裁（`B-1`~`B-4`）＋1 项报备（`B-5`）全部成立。**
其中 **`B-1`／`B-2` 是我方提示词的缺陷 —— 错在签发侧（本端）**，不是执行端的问题：

| 项 | 定性 | 我的错在哪 |
|---|---|---|
| `B-1` | ⭐ **我方口径自相矛盾** | 提示词写「`nodeGatherSeconds` 单值」＋「默认值＝改前实盘值」——而三型实盘值**互异**（8／4／2），**单值不可能同时等于三个数** |
| `B-2` | ⭐ **我方误判消费面** | 我未实读 `KingdomBrain:460` 的**过滤口径**就断言"两处受影响"；也未读 `WanderStimulusProvider:181-183` 的 **D617 已裁口径** |
| `B-3` | 我方把**基线表**当成了**豁免表** | 我写「`:51`／`:124` 未使用白名单」——两者语义完全不同 |
| `B-4` | 我方未追**连带面** | 我只写"退役 `Building` 采集面"，**没列其调用方**；而且执行端自己也有 4 处未列（见 §二 B-4） |
| `B-5` | 报备成立 | 执行端读得比我准（`GameBootstrap:40` 只是保实例） |

**⇒ 结论：本批可开工**，按 §二 逐项裁决施工；`B-4` 的改动面**按 §二-B4 的完整清单**（比报裁列的多 4 处）。

---

## 二、逐项裁决

### `B-1` · 采集耗时口径 ⇒ **取「甲」：逐型三字段（零行为变更）**

**实读复核**（三资产 `:65`）：`ore_vein = 8` ／ `stone_pile = 4` ／ `wood_pile = 2`；
旁证 `TaskScheduler.cs:1130-1131` 注释逐字「以 def 为准（资源点资产已配 **2s/4s/8s**）」；入包量侧 `TaskScheduler.cs:49 gatherAmount = 5`（**单值·无冲突**）。⇒ 报裁所述**全部成立**。

**裁决＝甲（逐型保留原值）**：
- `RespawnConfig` **新增三个具名字段**：`oreVeinGatherSeconds = 8` ／ `stonePileGatherSeconds = 4` ／ `woodPileGatherSeconds = 2`
  ＋ 一个查表 helper（按 `feature` 取，供 `WorldGatherSource` 调用）
  ⇒ ⭐ **零行为变更**（逐型值与改前**逐位相同**）；默认值有实盘出处，**非猜**
- **否决「单值」（乙）**：任何单值都会改 ≥2 型耗时 ⇒ 那是**平衡调整**，本批是「**双写收敛**」⇒ ⛔ 不得顺带改行为（会污染验收面与玩家手感）
- **否决「按类数组」（丙）**：需与 `MapGenRules` 的 kind 序耦合，多一层隐式约束而无收益；若要改数组形态 ⇒ **停下报裁**
- ⚠️ `RespawnConfig` **既有树字段不动**（`treeGatherSeconds/treeGatherAmount`）；改动须报 SO **逐字段 diff 面**

### `B-2` · 两处 AI 消费面 ⇒ **`B-2a` 撤回（不动）／`B-2b` 保语义（只 `OreVein`）**

**`B-2a` · `KingdomBrain.cs:471` ⇒ 本批不动，提示词该半句撤回**
实读：`:460 var buildings = QueryKingdomBuildingsSorted(k.id);`（`:545` 定义）⇒ **按 `kingdomId` 过滤**；三型实体 `kingdomId = -1`（`BuildingFactory.cs:99` 哨兵三分）⇒ **本就不在该列** ⇒ **6-A 对它零影响**。
⇒ 裁决：**不动**（提示词「`KingdomBrain:471` 须迁移」一句**作废**）。

**`B-2b` · `WanderStimulusProvider.cs:184` ⇒ 取「甲」：保语义，但**目标集只 `OreVein` 单型**
实读 `:181-183` 的 **D617 已裁口径**逐字：「三资产值不齐（`ore_vein=1` 一次性探明矿点 vs `stone_pile/wood_pile=0` 开局过渡堆积）**＝语义差异非缺陷，不统一为 1**——本处消费即『成为 NPC 游荡锚点候选』（真行为），统一会造成批内行为漂移」。
⇒ 裁决：
- ⛔ **不得**把 `stone_pile`／`wood_pile` 纳入（会**撞 D617 已裁**）
- ✅ **只补 `OreVein`**：在 `WanderStimulusProvider` **新增一条格表来源**（附近 `FeatureType.OreVein` 的格进 `resourceSites`），保住 `ore_vein` 的游荡锚点语义
- 该刷新口间隔 **12s**（`Reset` 里 `_nextInterval = 12f`）⇒ **非每帧**，但仍须给**代价读数**（`03` §8.7 判据 7）
- 判据：**能力句**「数据化后 `ore_vein` 格**仍**成为游荡锚点候选」＋ 对照读数（改前/后候选数）＋ 存在性反证（附近有 `ore_vein` ⇒ 候选含该格）

### `B-3` · `LifecycleAudit` ⇒ **只移 `:51`；`:124` 保留**（**D561 确认即本条**）

实读：
- `:42-57` ＝ **豁免表 `ExemptEvents`**（`:44` 逐字「【**D561 硬红线**】豁免表变更需策划端确认」）⇒ `:51` 在此表内
- `:112-134` ＝ **54 事件基线**数组（`:17` 注释「与『生命周期登记表 §4』**54 事件基线对账**」）⇒ `:124` 在此表内

⇒ 裁决：
- ✅ **移出 `:51`（豁免表）** —— 接入消费者后它不再"零订阅"；留在豁免表会让审计**失去覆盖**（日后订阅被删也不会被抓）
- ⛔ **不得移 `:124`（基线）** —— 它是**全集对账基线**，移出会产生假 `+ extra`
- ⭐ **D561 要求的「策划端确认」＝本裁决即确认**（口径：`PrioritizeHarvestCommand` 自本批起**有订阅**，故退出"无守卫零订阅噪音"豁免组；其余 12 项不动）

### `B-4` · `Building` 采集面的调用方 ⇒ **允许退役**，但**改动面按下列完整清单**（报裁漏 4 处）

**红线判据**：`TaskScheduler` 属**底层**（`_Game/Systems/AI/TaskScheduling/`），**不在**红线②列的**中层五项**（`05`/`06`/`07`/`08`/`#14`）之内 ⇒ ✅ **允许**，且属 6-D 的**必要连带**。

实读 `Building.OnGatherCompleted`（`:907-930`）后，**必须逐条处置**（含报裁未列的 4 处 ⭐）：

| # | 落点 | 处置 | 报裁是否列 |
|---|---|---|---|
| 1 | `TaskScheduler.cs:556` `gb.isBeingGathered = false;` | 随 `isBeingGathered` 退役删除 | ✅ |
| 2 | `TaskScheduler.cs:632` `if (comp is Building b) b.OnGatherCompleted();` | 删（`WorldGatherSource` 分支 `:636` 已覆盖全部世界资源点） | ✅ |
| 3 | `WorldGatherSource.cs:121` `_entity.OnGatherCompleted()` | 随 `ForEntity` 退役 ⇒ 统一走 `HandleCellGathered` | （在口径内） |
| 4 | ⭐ `Building.cs:913 WanderAnchorPool.Instance.RegisterFreeSpot(...)`（**唯一调用点**·`WanderAnchorPool.cs:139`） | **必须搬进 `HandleCellGathered`** —— 否则**游荡锚点来源丢失**（`WanderAnchorPool.cs:10` 注释明写此来源） | ❌ **未列** |
| 5 | ⭐ `BuildingFactory.cs:434 ReturnBuildingToPool`（`Building.OnGatherCompleted` 调） | 无实体可回收 ⇒ 随实体退役（给 grep 证据） | ❌ **未列** |
| 6 | ⭐ `Editor/ChainAudit/Validators/R3_SupplyChain.cs:119-121`（把 `Gather(OnGatherCompleted)` 记为**供给路径**） | **同步改口径**（否则 ChainAudit 报供给断链·假警报） | ❌ **未列** |
| 7 | ⭐ `Editor/Smoke/Valley2_17_Smoke_FixCard.cs:173`（`O.isBeingGathered = true` 驱动"自然矿采集"） | 该探针语义**失效** ⇒ 改走数据路径或**标废弃**（禁静默留红） | ❌ **未列** |

**另：占格面须确认**（报告里给读数）—— 6-A 后资源点无实体 ⇒ **无网格占格/无注册表条目**；须证「资源格不占格」不引入新空洞（改前 `ReleaseLayerOwnedState(unregisterFromRegistry:true)` 承担的释放面，现在**没有对象可释放**）。

### `B-5` · 订阅落点 ⇒ **确认执行端的先例判断**（且发现更优落点）

实读：`GameBootstrap.cs:40` ＝ `_ = DayCycleSettlement.Instance;`（**只是保实例**）；真订阅在 `DayCycleSettlement.Awake:17`。
⭐ **更优落点**：`ResourceRespawnSystem` **自己已有 `Awake`**（`:119-127`，且 `:123` **已订阅** `TimeDayChangedEvent` —— 片 5 的 4-A 留的）
⇒ 裁决：**玩家入口消费者落在 `ResourceRespawnSystem.Awake` 内**（紧随 `:123`），照同文件既有写法（`_instance != this` 守卫 ＋ `OnDestroy` 退订）；⛔ **禁 `Start()` 订阅**。

---

## 三、清洁项修订（报裁提的"重复"实为 **3 处**）

实读守卫失去通知的**三个落点**：① `MapGate.RemoveResourceNode:308`（门内）② `ResourceRespawnSystem.HandleTreeGathered:477` ③ `Building.cs:920`（一次性资源点路径）。
⇒ 裁决：**统一后只保留门内 `:308` 一处**；另两处随各自路径退役/删除。报告须给「`HandleResourceConsumed` 是否幂等」的证据（若非幂等 ⇒ 说明现网 3 次调用下的实际效果）。

---

## 四、本端缺陷（3 条）

1. **`B-1`**：口径**自相矛盾**（单值 vs 逐型实盘值）—— 签发时**未实读**三资产的 `gatherSeconds`（只写"先实读"，把矛盾留给了执行端）。
2. **`B-2`**：**未实读消费面的过滤口径**（`KingdomBrain:460` 的 `kingdomId` 过滤）＋**未读已裁注记**（`WanderStimulusProvider:181-183` 的 D617）就断言"两处须迁移"。
3. **`B-4`**：写"退役 X"却**未列 X 的调用方** ⇒ 把连带面留给执行端发现（本次靠它停手才没漏）。

⇒ **归入教训**：`L-15` 家族＋新维度（**「退役某成员」的清单必须含其全部调用点与语义承接方**）；`L-40` 家族＋1（**参数值须直读存储处·本端同一课第 5 次**：`gatherSeconds` 在 `.asset` 而非 `.cs`）。**落点**＝派工提示词的"退役类"红线（下一批起生效）。

---

## 五、落账

- 本裁决（`多Agent交接/策划端/HH.294_片6-2_报裁_裁决.md`）
- 台账 **§四十九**（`D778`）｜`_编号登记.md`（`D778`）｜`_任务队列.md`（片 6-2 裁决行）｜`_当前快照.md`
- ⚠️ **`0.6_审查决策记录.md` 未写**（自 `D739` 停更·按**实况口径**）
