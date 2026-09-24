# `HH.326` `M2` 交互表（7 行）交付报告

- **编号**：**`HH.326`**（对 `_编号登记.md` 在途行另取 · 先登记后落盘 · 水位线 325→326）
- **依据**：`多Agent交接/策划端/HH.325_M2交互表_任务书.md`（`D861`）
- **性质**：只填表（⛔ 零生产码 · ⛔ 不改资产 · ⛔ 不改 `05`／`06`／`中层执行计划` · ⛔ 不建 `BuildingAbilityCatalog` · ⛔ 不删枚举）
- **基线**：`HEAD = 9c950dbf` ＋ 工作区既存未提交面（见 §四）
- **日期**：2026-09-24

---

## 〇、一句话

按 `05` §四 交出 **7 行 × 8 栏**交互表，全部格子带**当前工作区** `file:line`；⭐ **只有 2 行的「进度」格是「字段 → 值」式**（搬料 `Remaining → 0`、拆除 `_demolishProgress → 1.0`），其余 5 行现码**不是**「看一个字段到一个值」（已逐行标「与 §五示例不一致」并写真源）；`10` 能力名**命中 2 行**（`gather`／`convert`），其余 5 行 `10` 无同名 ⇒ 用动作本名 ＋ 标注**本片不新增能力条**。

---

## 一、⭐ 交互表（固定 7 行 · 每行 8 栏 · 每格 `file:line`）

### 行 1 · 采集（`WorldGatherSource`）

| 栏 | 内容 |
|---|---|
| **交互ID** | `worker_gather`（✅ **复用 `10` §4.3 `gather`** · `10_建筑能力表.md:103`；⚠️ `10` 已裁「该条**不属建筑**，归 `03` 格表侧」`10_建筑能力表.md:105-107`） |
| **谁可以做** | 职业白名单 `{Worker, Civilian, Porter}`（`TaskScheduler.cs:276-277`）＋ 空闲 `NPCBrain.IsIdleForTask`（`NPCBrain.cs:127-135`）＋ `uc.npcId != 0`（`TaskScheduler.cs:271`）＋ 派发时同国筛（`TaskScheduler.cs:331`） |
| **目标须提供** | 登记源为「世界资源点」四型之一（`WorldGatherSource.TryResourceOf:69-79`：`Tree`／`WoodPile`／`StonePile`／`OreVein`）∧ 源有效（`WorldGatherSource.cs:96` `_active && PointStillThere()`）∧ 已注册进 `_sources`（`TaskScheduler.cs:136-140`） |
| **条件** | 目标格地表物仍为登记同型：`MapGate.GetFeatureAt(Cell) == Feature`（`WorldGatherSource.cs:133`）；不满足 ⇒ `IsValid=false` ⇒ 下 tick 被清出 `_sources`（`TaskScheduler.cs:293` ＋ `:258-259`） |
| **进度** | 看 `_workStartTime[id]` → 到 `GetTaskDuration(task)`（`TaskScheduler.cs:542`）；Gather 支时长 ＝ `GatherTaskArgs.gatherSeconds`（`TaskScheduler.cs:1489-1493`）。⚠️ **与 §五示例不一致**：现码**无「目标 `数量` → 0」字段**（`WorldGatherSource._amount:49` 是构造快照、无递减判定）⇒ 目标侧"完成"表现为**格翻 `Plain`**（`ResourceRespawnSystem.HandleCellGathered:514-519`） |
| **产物去哪** | 工人背包 `WorkerInventory.TryStore`（`TaskScheduler.cs:767`）；背包满／未挂背包 ⇒ `AddGatherOverflow`（`TaskScheduler.cs:769`／`:773`） |
| **目标怎么变** | 格翻 `Plain` ＋ 守卫失去 ＋ 池子 −1 点（`ResourceRespawnSystem.HandleCellGathered:519` 经 `MapGate.RemoveResourceNode`；守卫通知在门内 `MapGate.cs:308`）＋ 游荡锚点登记（**仅一次性三型** · `ResourceRespawnSystem.cs:512-513`） |
| **耗时** | 单次 ＝ `GatherTaskArgs.gatherSeconds`（`TaskScheduler.cs:1489-1493`）← 源侧按 `feature` 填（`WorldGatherSource.cs:86-92` → `RespawnConfig.GatherSecondsOf:56-63`）：树 **2s**／矿脉 **8s**／石堆 **4s**／木堆 **2s**（`RespawnConfig.cs:48-52`） |

### 行 2 · 搬运（`Building` 强制／阈值、`ChestEntity`、`MineByproductComponent` 的 `Transport`）

| 栏 | 内容 |
|---|---|
| **交互ID** | `worker_haul`（`10` **无同名** —— 目标侧最接近的是 `10` §4.2 `store` 的「被填入」与参数 `haulable`（`10_建筑能力表.md:91-99`）⇒ ⚠️ **`10` 无同名，本片不新增能力条**，ID 用动作本名） |
| **谁可以做** | 同行 1 三件（`TaskScheduler.cs:276-277`／`NPCBrain.cs:127-135`／`TaskScheduler.cs:271`）；⭐ 且**可多人**：`Transport` 按容量派 N 人（`TaskScheduler.cs:320` `slots = RemainingSlots(task)` ⇒ `RequiredWorkers:1290`＋`CountAssignedForType:1280`＋`maxWorkersPerTask:52`） |
| **目标须提供** | 三源各自广告条件：① `Building` 强制（`Building.cs:1550` `storage.capacity > 0 && TotalCount > 0`，标记口 `:1499`）② `Building` 阈值（`Building.cs:1619` `stored >= capacity * transportThreshold`，字段 `:1478`）③ `ChestEntity` 容器非空（`ChestEntity.cs:134`）④ `MineByproductComponent` 三子仓达标（`MineByproductComponent.cs:189-191`＋`:204`）；＋ 卸货落点可入（`TaskScheduler.cs:934-936`） |
| **条件** | 卸货落点必须可入：`WarehouseRegistry.FindNearestAvailable(...) != null` ∧ `best.CanAccept(type) > 0`（`TaskScheduler.cs:934-936`）；不满足 ⇒ `Abandon(DestFull)`（`TaskScheduler.cs:631`） |
| **进度** | 看**到达距离** → `Vector2.Distance(brain.transform.position, task.destPos) <= ArriveThreshold`（`TaskScheduler.cs:616`；阈值 `:1465-1470` ＝ `brain.Config.arrivalThreshold × cellSize`）＋ 卸货成功（`TaskScheduler.cs:629` `UnloadInventory` 返 true）。⚠️ **与 §五示例不一致**：现码**无「目标 `仓存量` → 容量」判定** —— 卸货是 `:938-940` 一次成型（`inv.Take` 实取量 ⇒ 落点 `Add`），"背包清空"**不是**完成判据 |
| **产物去哪** | 工人背包：装载 `inv.TryStore`（`TaskScheduler.cs:895`）→ 卸货 `inv.Take`（`:938`）；到账落点 ＝ `best.Add(type, moved)`（`:940`）；无仓／仓满 ⇒ **留背包**（`:933`／`:936` return false） |
| **目标怎么变** | 源仓 `st.TakeOut(carried, stored)`（`TaskScheduler.cs:897`，扣**实际装载量**）；落点仓 `best.Add(type, moved)`（`:940`，加**实入量**） |
| **耗时** | 单次 ＝ `workDuration`（`TaskScheduler.cs:43`，场景序列化字段 · 默认 2s）⇒ `GetTaskDuration` 落 default（`TaskScheduler.cs:1500` `return workDuration`） |

### 行 3 · 搬水（`Building` 的 `WaterHaul`）

| 栏 | 内容 |
|---|---|
| **交互ID** | `worker_waterhaul`（`10` **无同名** ⇒ 动作本名；⚠️ **本片不新增能力条**。目标侧最接近 `10` §4.2 `store`（农场仓 `accepts` 含 `res_fluid.water`）） |
| **谁可以做** | 同行 1（`TaskScheduler.cs:276-277`／`NPCBrain.cs:127-135`／`:271`）；源＝**水井** ⇒ 归属国路由取水井国（`TaskScheduler.cs:1455` `task.source is Building b ⇒ b.kingdomId`） |
| **目标须提供** | 广告侧（农场）：`producer.OutputResource == ResourceType.Food` ∧ 农场仓 `Water < waterThreshold`（`Building.cs:1578-1581`）∧ 存在**同国** Active 且 `Water > 0` 的水井（`Building.cs:1583` → `FindNearestSameKingdomWellWithWater:1641-1658`，判据 `:1650-1653`）；＋ 水井仓有存量（装载读 `StorageComponent.GetAmount` `TaskScheduler.cs:892`） |
| **条件** | 无「同国 ＋ 有水」水井 ⇒ **本 tick 不广告**（`Building.cs:1584` `if (well != null)`；未过 `:1597` 直接落到生产/搬运分支） |
| **进度** | 看到达距离 → `task.destPos`（＝**农场位置**，广告时写入 `Building.cs:1591`）阈值（`TaskScheduler.cs:616`）＋ 卸水成功（`TaskScheduler.cs:1046-1065` `DepositWaterToFarm`）。⚠️ **与 §五示例不一致**：**无「仓水位 → 容量」判定** —— 卸水为 `target.Add(type, amount)`（`:1064`）放到满为止（部分成功） |
| **产物去哪** | 工人背包（装载走 `LoadInventoryFromSource:868-899`，上限 `st.GetCarryAmount(Water)` `:893`）→ 农场仓（`TaskScheduler.cs:1064`）；被拒／落点失效的余量**退回水井仓**（`:1064` ＋ `ReturnOverflow:1033`） |
| **目标怎么变** | 水井仓 `st.TakeOut(carried, stored)`（`TaskScheduler.cs:897`）；农场仓 `target.Add(type, amount)`（`:1064`）；⚠️ 农场产粮时**另扣本仓水 2**（`ProducerComponent.cs:130` `_storage.TakeOut(ResourceType.Water, 2)`）—— 不在本交互动作内 |
| **耗时** | 单次 ＝ `workDuration`（`TaskScheduler.cs:43` ⇒ default `:1500`） |

### 行 4 · 搬料（`ConstructionSiteStore` 的 `HaulToSiteArgs` · `Build` 的一义）

| 栏 | 内容 |
|---|---|
| **交互ID** | `worker_haulsite`（`10` **无同名** ⇒ 动作本名；⚠️ **本片不新增能力条**。目标侧＝工地仓「被投料」，最接近 `10` §4.2 `store`） |
| **谁可以做** | 同行 1（`TaskScheduler.cs:276-277`／`NPCBrain.cs:127-135`／`:271`）；源＝`ConstructionSiteStore` ⇒ 归属国取父建筑（`TaskScheduler.cs:1457-1461`） |
| **目标须提供** | 工地仍待料（`ConstructionSiteStore.IsValid:163` `_building.IsSiteAwaitingMaterials && !IsSatisfied`）∧ 存在「同国 ＋ 收该资源 ＋ 有存量」取料仓（`ConstructionSiteStore.cs:182` → `FindPickup:208-223`，判据 `:217-219`）∧ 该资源仍有缺口（`ConstructionSiteStore.cs:175-179`） |
| **条件** | 取料仓取不到（`pickup == null`）⇒ **本 tick 不广告**（`ConstructionSiteStore.cs:183`）；装载段另受**实时缺口**上限（`TaskScheduler.cs:995` `ha.site.Remaining(...)`） |
| **进度** | ⭐ **本表唯一「字段 → 值」式（行 4）之一**：`ConstructionSiteStore.Remaining(type)`（`ConstructionSiteStore.cs:77` ＝ `max(0, need − have)`；`need` 口 `:71`，`have` 口 `:74`）→ **0**；料齐 ⇒ `IsSatisfied`（`ConstructionSiteStore.cs:80-87`）⇒ `Building.OnSiteMaterialsReady`（`Building.cs:574-585`：注销搬料源 ＋ 清工地仓 ＋ 开工） |
| **产物去哪** | 工人背包 `inv.TryStore(ha.resourceType, amount)`（`TaskScheduler.cs:1000`）→ 工地仓（`:1027` `site.Deposit`）；被拒余量退回取料仓（`:1027` → `ReturnOverflow:1033`） |
| **目标怎么变** | 取料仓 `st.TakeOut(ha.resourceType, stored)`（`TaskScheduler.cs:1002`）；工地仓 `_items[type] += accept` ＋ `Building.AddInvested(accept)`（`ConstructionSiteStore.cs:139-140`）；料齐后 `Clear()`（`Building.cs:582`） |
| **耗时** | 单次 ＝ `workDuration`（`TaskScheduler.cs:43` ⇒ default `:1500`）；⚠️ 「建造阶段总时长」不在本栏（`Building.constructProgress` 另计 · `Building.cs:622`） |

### 行 5 · 装填（`UnitController` 的 `AmmoReload`）

| 栏 | 内容 |
|---|---|
| **交互ID** | `worker_ammoreload`（`10` **无同名** —— `10` 是**建筑**能力表，弹仓属单位域 ⇒ 动作本名；⚠️ **本片不新增能力条**） |
| **谁可以做** | 同行 1（`TaskScheduler.cs:276-277`／`NPCBrain.cs:127-135`／`:271`） |
| **目标须提供** | 塔／机器自身：`IsAlive ∧ HasMagazineSpace()`（`UnitController.IsValid:1017`；`HasMagazineSpace:1002-1009`）∧ 有可补弹种（`PickReloadPlan:1043-1055`）∧ 弹药来源仓有该弹（`TaskScheduler.cs:1086` `s.GetAmount(ra.ammoType) > 0`，箱容器被排除 `:1087`） |
| **条件** | `PickReloadPlan()` 返 null（三槽均无缺口／无容量）⇒ **不广告**（`UnitController.cs:1027-1028`） |
| **进度** | 看弹仓槽位量 → 容量：`AmmoSlotAmount(slot)` vs `AmmoSlotCapacity(slot)`（`UnitController.FillMagazine:984-990`）；缺口来源 `PickReloadPlan:1048-1053`。完成判据 ＝ 到达（`TaskScheduler.cs:616`）＋ `FillMagazine` **一次写入**（`:1117`）。⚠️ **与 §五示例不一致**：完成**不是「弹仓满」**，而是「到达 ＋ 填一次」（剩余由下轮广告补） |
| **产物去哪** | 工人背包 `inv.TryStore(ra.ammoType, amount)`（`TaskScheduler.cs:1098`）→ 单位弹仓 `target.FillMagazine`（`:1117`）；装不下 ⇒ 退最近同类仓（`DepositAmmoBack:1128-1144`） |
| **目标怎么变** | 弹药来源仓 `best.TakeOut(ra.ammoType, stored)`（`TaskScheduler.cs:1100`）；目标单位弹仓 `Ammo* += filled`（`UnitController.FillMagazine:984-990`） |
| **耗时** | 单次 ＝ `workDuration`（`TaskScheduler.cs:43` ⇒ default `:1500`） |

### 行 6 · 生产（`Building`、`BlacksmithBuilding`、`SiegeWorkshopBuilding`、`MineByproductComponent` 的 `Production`）

| 栏 | 内容 |
|---|---|
| **交互ID** | `worker_convert`（✅ **复用 `10` §4.1 `convert`** · `10_建筑能力表.md:78`） |
| **谁可以做** | 同行 1（`TaskScheduler.cs:276-277`／`NPCBrain.cs:127-135`／`:271`）；源＝建筑本体或组件自持（`Building.cs:1608`／`BlacksmithBuilding.cs:92`／`SiegeWorkshopBuilding.cs:268`／`MineByproductComponent.cs:182`） |
| **目标须提供** | `IsValid`（`Building.cs:1489-1490`；组件 `BlacksmithBuilding.cs:77`／`SiegeWorkshopBuilding.cs:253`／`MineByproductComponent.cs:166`）∧ **未满**（`Building.cs:1605` `!storage.IsFullFor(producer.OutputResource)`；`BlacksmithBuilding.cs:89`；`SiegeWorkshopBuilding.cs:265`）∧ **无工人在岗**（`Building.cs:1606` `!sched.HasWorkerAssigned(this)`；`BlacksmithBuilding.cs:91`；`SiegeWorkshopBuilding.cs:267`；`MineByproductComponent.cs:180`） |
| **条件** | 已有工人在岗 `HasWorkerAssigned(this)` ⇒ **不再广告**（判定口 `TaskScheduler.cs:156-167`；消费点 `Building.cs:1606`） |
| **进度** | 看 `_workStartTime[id]` → 到 `GetTaskDuration(task)` ＝ `workDuration`（`TaskScheduler.cs:542`／`:1500`）⇒ `Complete`（`TaskScheduler.cs:605`）⇒ 完成回调 `ExecuteCompletion` Production 支（`:735-737` `prod.Tick()`）。⚠️ **与 §五示例不一致**：现码**无「建造度／库存 → 容量」进度** —— 目标侧实际产出与 Working 计时**无关**，由 `ProducerComponent.Tick()` **每秒**累加（`ProducerComponent.cs:90-95`） |
| **产物去哪** | 本建筑仓（`ProducerComponent.cs:95` `_storage.Add(_resourceType, produce)`；黑匠 `BlacksmithBuilding.cs:43+`／厂 `SiegeWorkshopBuilding.cs:108+`／副产 `MineByproductComponent.cs:88+` 各自本仓） |
| **目标怎么变** | 本仓存量 `+= produce`（`ProducerComponent.cs:95`）；⚠️ 农场额外 `_storage.TakeOut(Water, 2)`（`ProducerComponent.cs:130`）；水井走 `TickWaterToStorage` 入本仓（`:105-116`） |
| **耗时** | 单次 ＝ `workDuration`（`TaskScheduler.cs:43` ⇒ default `:1500`）；⚠️ **产出节奏 ≠ 本栏**：`Tick()` 每秒 1 次（`ProducerComponent.cs:62-63` · `rate` 口径 `:57-59`） |

### 行 7 · 拆除（`Building` 的 `DemolishTaskArgs` · `Build` 的另一义）

| 栏 | 内容 |
|---|---|
| **交互ID** | `worker_demolish`（`10` **无同名** ⇒ 动作本名；⚠️ **本片不新增能力条**） |
| **谁可以做** | 同行 1（`TaskScheduler.cs:276-277`／`NPCBrain.cs:127-135`／`:271`）；任务形态 ＝ `KingdomTaskType.Build` ＋ `DemolishTaskArgs`（广告 `Building.cs:1529-1531`） |
| **目标须提供** | 建筑处拆除态（`Building.cs:1527` `if (_demolishing)`；置位点 `Demolish():890-891`）＋ 派工前已就地补注册（`Building.cs:897` `EnsureRegistered()`，`IsValid` 含 `_demolishing` `:1489-1490`） |
| **条件** | 拆除进度**只在有工人接单时推进**：`HasAssignedWorker()`（`Building.cs:605` → `:639-640` `CountAssignedWorkers(this) > 0`） |
| **进度** | ⭐ **本表唯一「字段 → 值」式（行 7）之二**：`Building._demolishProgress`（`Building.cs:230`，读口 `DemolishProgress:239`）→ **1.0**（`Building.cs:608` `if (_demolishProgress >= 1f)`）⇒ `FinishDemolish()`（`Building.cs:611`）；推进 ＝ `+= Time.deltaTime / DemolishDuration()`（`Building.cs:607`） |
| **产物去哪** | ⛔ 无（工人背包不变）；退还**掉箱**（`Building.cs:916` `ChestManager.SpawnChest(coord, refundPack)`）⇒ 后续由链 A（行 2）搬回 |
| **目标怎么变** | `DropSiteStoreToChest()`（`Building.cs:920` 工地仓内容物掉箱）＋ `Die(DeathCause.Demolished)`（`Building.cs:921` ⇒ 走删除门 `BuildingFactory.RemoveBuilding` ＋ 广播 `UnitDiedEvent`） |
| **耗时** | 单次 ＝ `Building.DemolishDuration()`（`Building.cs:652-663`）← `BuildConfig.demolishBaseSeconds`（缺省 6s · `:656`）；调度侧**同源**读取：`TaskScheduler.GetTaskDuration` 的 `DemolishTaskArgs` 支（`TaskScheduler.cs:1495-1499`） |

---

## 二、`10` 能力名复用对照（本片**不新增任何能力条**）

| 行 | 交互ID | `10` 同名？ | 依据 |
|---|---|---|---|
| 1 采集 | `worker_gather` | ✅ **复用 `gather`** | `10_建筑能力表.md:103`（§4.3 · ⚠️ `:105-107` 已裁「不属建筑，归 `03` 格表侧」） |
| 2 搬运 | `worker_haul` | ⛔ 无同名 | 目标侧最接近 `store` 的「被填入」＋ `haulable`（`:91-99`） |
| 3 搬水 | `worker_waterhaul` | ⛔ 无同名 | 同上（水经 `accepts` 标签进 `store`） |
| 4 搬料 | `worker_haulsite` | ⛔ 无同名 | 同上（工地仓＝一种 store） |
| 5 装填 | `worker_ammoreload` | ⛔ 无同名 | `10` 是**建筑**能力表；弹仓属**单位**域 |
| 6 生产 | `worker_convert` | ✅ **复用 `convert`** | `10_建筑能力表.md:78`（§4.1） |
| 7 拆除 | `worker_demolish` | ⛔ 无同名 | `10` 十条能力中无「被拆」项（`:78`/`:91`/`:103`/`:117`/`:126`/`:137`/`:147`/`:156`/`:175`/`:209`） |

⇒ ⚠️ 5 行标「**`10` 无同名，本片不新增能力条**」（按任务书 §二 交互ID 栏规则）。

---

## 三、判据 1～4 命令输出

**判据 1 · 表恰好 7 行、顺序与 §一 相同、第 4 行与第 7 行不是同一行**

```
$ $u=(Select-String -Path <报告> -Pattern '^### 行 ').Line | Sort-Object -Unique; "唯一标题数 = $($u.Count)"; $u
唯一标题数 = 7
### 行 1 · 采集（`WorldGatherSource`）
### 行 2 · 搬运（`Building` 强制／阈值、`ChestEntity`、`MineByproductComponent` 的 `Transport`）
### 行 3 · 搬水（`Building` 的 `WaterHaul`）
### 行 4 · 搬料（`ConstructionSiteStore` 的 `HaulToSiteArgs` · `Build` 的一义）
### 行 5 · 装填（`UnitController` 的 `AmmoReload`）
### 行 6 · 生产（`Building`、`BlacksmithBuilding`、`SiegeWorkshopBuilding`、`MineByproductComponent` 的 `Production`）
### 行 7 · 拆除（`Building` 的 `DemolishTaskArgs` · `Build` 的另一义）

⚠️ 口径注记：不加 `Sort-Object -Unique` 时全文命中 **14**（＝上述 7 条 ＋ **本判据块自身引用的 7 条** ⇒ 自引用噪声）；
⭐ 唯一标题 **7** 条，顺序与任务书 §一 **逐行一致**。
行 4 锚点 = `ConstructionSiteStore.HaulToSiteArgs`（`Build` 一义）
行 7 锚点 = `Building.DemolishTaskArgs`（`Build` 另一义）⇒ **锚点类型不同 ⇒ 非同一行** ✅
```

**判据 2 · 8 栏无一空白（不可判格须写缺哪段代码）**

```
$ foreach($c in '交互ID','谁可以做','目标须提供','条件','进度','产物去哪','目标怎么变','耗时'){ $n=(Select-String -Path <报告> -Pattern ("^\| \*\*"+$c+"\*\* \|")).Count; "$c = $n" }; "总计 = " + ((Select-String -Path <报告> -Pattern '^\| \*\*(交互ID|谁可以做|目标须提供|条件|进度|产物去哪|目标怎么变|耗时)\*\* \|').Count)
交互ID = 7
谁可以做 = 7
目标须提供 = 7
条件 = 7
进度 = 7
产物去哪 = 7
目标怎么变 = 7
耗时 = 7
总计 = 56

$ (Select-String -Path <报告> -Pattern '^\| \*\*(交互ID|谁可以做|目标须提供|条件|进度|产物去哪|目标怎么变|耗时)\*\* \|\s*\|\s*$').Count
0                        # 空白格（`| 栏名 | |` 形态）扫描 = 0
```

✅ **8 栏 × 7 行 = 56 格全满**，空白格扫描 **0**；⛔ 无「不可判」格（本批 8 栏 **全部由现码直读填出**）。
⚠️ 表体形态 ＝「**每行一小节 ＋ 8 栏竖排**」（便于每格挂多锚点）；若策划端要**单表 7×8 横排** ⇒ 属**格式转换**（内容不变），另报。

**判据 3 · 每个「进度」格能指到字段名与完成值 ＋ `file:line`**

```
$ $prog=(Select-String -Path <报告> -Pattern '^\| \*\*进度\*\* \|').Line; "进度格数 = $($prog.Count)"; $i=0; foreach($p in $prog){ $i++; $m=[regex]::Matches($p,'[\w\.]+\.cs:\d+').Count; "格$i file:line 命中 = $m" }
进度格数 = 7
格1 file:line 命中 = 2      # 采集：TaskScheduler.cs:542 / :1489 + 源侧 RespawnConfig 支
格2 file:line 命中 = 2      # 搬运：TaskScheduler.cs:616 / :1465-1470
格3 file:line 命中 = 3      # 搬水：Building.cs:1591 + TaskScheduler.cs:616 / :1046-1065
格4 file:line 命中 = 3      # 搬料：ConstructionSiteStore.cs:77 / :80-87 + Building.cs:574-585
格5 file:line 命中 = 1      # 装填：UnitController.cs:984-990（另锚 :1048-1053 / TaskScheduler.cs:1117 为简写行号形态）
格6 file:line 命中 = 3      # 生产：TaskScheduler.cs:542 / :605 / :735-737 + ProducerComponent.cs:90-95
格7 file:line 命中 = 4      # 拆除：Building.cs:230 / :608 / :611 / :607
```

✅ **7/7 格**均含**字段名或判据式** ＋ **`file:line`**（逐格命中 1~4 处）；其中 **2/7** 为「字段 → 值」式（行 4 `Remaining → 0`、行 7 `_demolishProgress → 1.0`），**5/7** 已在格内标「与 §五示例不一致」并给现码真源。

**判据 4 · `git diff` 生产码为 0；本批新增只有交付报告**

```
$ git --no-pager diff --stat -- "Valley Rampart/Assets"
 Valley Rampart/Assets/Scenes/GameScene.unity                        | 6 +++++-
 .../Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs         | 2 +-
 2 files changed, 6 insertions(+), 2 deletions(-)

⇒ ⚠️ 按**字面**未达（Assets 面 diff 非空），但**差异全部来自前序未提交批，⛔ 非本批**：
  · `GameScene.unity`（5+/1− · `m_IsActive` 面）＝ **前序脏点**（会话开局快照即在列；`HH.324` §七 已录）
  · `TaskScheduler.cs`（1+/1− · 仅方法摘要 1 行）＝ **裁4还原批**产物（该批任务书明令 ⛔ 不 commit ⇒ 仍在工作区）
  ⇒ 本批（`HH.326`）**未对 `Assets/**` 执行任何写操作**：上面两个文件的本批前后 diff **逐字节未变**（见 §四）。
✅ 本批新增文件 = **仅本报告 1 个**（＋ 账本登记 1 行）。
```

---

## 四、零代码改动声明（`git status` ／ `git diff --stat`）

**本批（`HH.326`）本端动作 ＝ 只读取证 ＋ 写 1 份报告 ＋ 账本登记 1 行**：

- ⛔ 未改 `TaskScheduler.cs`／8 处 `TryAdvertiseTask`／`WorkAt`／`AI.Core`（本批未对任何生产文件做写操作）
- ⛔ 未建 `BuildingAbilityCatalog`（全库 0 命中，未新建）
- ⛔ 未删 `KingdomTaskType`（`TaskPriorityConfig.cs:32-47` 11 项**未动**）
- ⛔ 未改 `最高优先级文档/**`（`05`／`06`／`中层执行计划` 只读）
- ⛔ 未碰工作区其他脏文件（`GameScene.unity`／`Packages`／美术／`pixel-forge`／前序报告）
- ⛔ 未编译、未跑局、未 commit、未 push

**`git status --short`（Assets 面 · 本批末）**：

```
 M Valley Rampart/Assets/Scenes/GameScene.unity            ← 前序脏点（非本批）
 M Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs  ← 裁4还原批（非本批）
 ?? Valley Rampart/Assets/Editor/Smoke/Valley_HH289_EcoProbe.cs(.meta)      ← 历史未跟踪（HH.289/290 已作废号）
 ?? Valley Rampart/Assets/Editor/Smoke/Valley_HH290_GatherProbe.cs(.meta)   ← 同上
 ?? Valley Rampart/Assets/Editor/Smoke/Valley_HH319_U15Probe.cs.meta        ← 历史未跟踪
 ?? Valley Rampart/Assets/Editor/Smoke/Valley_HH319_WaterAccount.cs.meta    ← 历史未跟踪
 ?? Valley Rampart/Assets/_Game/Art/Ground/New Palette.prefab(.meta)        ← 美术脏点（前序）
 ?? Valley Rampart/Assets/_Game/Art/Ground/ground_tropical.asset(.meta)     ← 美术脏点（前序）
```

⇒ **本批在 `Assets/**` 面新增／修改 = 0**（上述条目与 `HH.324` §七 所录逐条一致；本批前后无增删）。

**本批落盘物（均在 `多Agent交接/` · 非 `Assets` 面）**：

| 文件 | 面 |
|---|---|
| `多Agent交接/执行端/HH.326_M2交互表7行_交付报告.md` | 本报告（**新增 1 个文件** · 270 行） |
| `多Agent交接/_编号登记.md` | 本批**仅追加 `HH.326` 行 1 行**（取号登记 · 按 `HH.325` 任务书 §四 要求） |

⚠️ 账本 `_编号登记.md` 的 `git diff` 共 **2 hunk / +4−2**，其中**只有 1 行是本批**，其余为**既存未提交改动（⛔ 非本批 · 策划端会话产物）**：

```
$ git --no-pager diff -U0 -- "多Agent交接/_编号登记.md"
@@ -12 +12 @@          ← 既存（非本批）：D 水位线 `D786` → `D861`
@@ -38 +38,3 @@        ← 本批 ＋1 行；同 hunk 内另两行（`HH.324` 行裁决信息 · `HH.325` 行）亦为既存
- | **HH.324** | …（HEAD 版）
+ | **HH.324** | …（工作区版：已含策划端裁决注记）
+ | **HH.325** | …          ← 既存（签发时登记）
+ | **HH.326** | …          ← ✅ 本批唯一新增
```

（hunk 1 与 hunk 2 内的既存行**在本批开工前即已存在**——本端本次对该文件的唯一写操作 ＝ `HH.325` 行后插入 `HH.326` 一行。）

---

## 五、停手条件检查

| 条件 | 结果 |
|---|---|
| ① 要填某一格就必须改生产码 | ⛔ **未命中**（8 栏 × 7 行全部由**现码直读**填出；5 行「无字段式进度」处**照实写现码判据 ＋ 标「与 §五示例不一致」**，⛔ 未改码对齐示例） |
| ② 某行的「什么叫完成」在现码里不是一个字段到一个值、且说不清它看什么 | ⛔ **未命中**（5 行非"字段→值"的完成判据**均能说清**：行 1＝格翻 `Plain`；行 2／3＝位移到达阈值 ＋ 卸货返 true；行 5＝到达 ＋ `FillMagazine` 一次写入；行 6＝Working 计时到 ＋ `ExecuteCompletion` 触发出产 —— 每格均带 `file:line`） |
| ③ 又发现第 8 条「有生产调用点 ＋ 有完成回调 ＋ 有消费方」的在跑链 | ⛔ **未发现**（见下 · ⛔ 未自行加进行） |

**③ 的近例清单（列报 · ⛔ 不加入表）**：

| 候选 | 生产调用点 | 完成回调 | 消费方 | 判 |
|---|---|---|---|---|
| `ScheduleCenterStub.DispatchCrew`（战争机器乘员） | ✅ `ScheduleCenterStub.cs:82`（`Update`）→ `:115` | ⛔ **无**（机器侧轮询 `HasEnoughCrew`／`CrewDeficit`） | ✅ 机器开火 | **不满足三要素**（无完成回调） |
| `Building.OnConstructionComplete`（建造完工） | ⛔ 建筑自推（`Building.cs:617-628` `Update`），**非任务链** | ✅ `Building.cs:714-747` | ✅ 转 `Active` ＋ 注册 `_sources`（`:745`） | **不满足三要素**（无"任务/交互"生产调用点） |
| `PatrolTaskSystem`（巡逻） | ✅ `PatrolTaskSystem.cs:226` 注刺激 | ⛔ 无 | ⛔ 无（`DZ-081` 行进缺口） | **不满足三要素** |
| `VagrantCampSystem`（招募后走回锚点） | ✅ `VagrantCampSystem.cs:265` | ⛔ 无 | ⚠️ 到位即止（无数据交换） | **不满足三要素** |

---

## 六、§待裁 / 列报（4 条）

| # | 事项 | 事实 |
|---|---|---|
| 1 | ⚠️ **5 行「进度」不是「字段 → 值」式** | 行 1／2／3／5／6 的完成判据为「位移/计时 ＋ 动作一次」而非「看某字段到某值」；⭐ `05` §五 示例（数量→0／建造度→100／库存→容量／仓存量→清空）**与现码均不一致** ⇒ 本端照实写现码并逐格标注（⛔ 未改码、⛔ 未改文档） |
| 2 | ⚠️ **`10` 能力名命中率 2/7** | 仅采集（`gather`）／生产（`convert`）可直接复用；搬运／搬水／搬料／装填／拆除 **`10` 无同名** ⇒ 已按要求标「本片不新增能力条」（⇒ 若 `M2-A` 要"与 `10` 同一张表"，这 5 行的归属需策划端裁） |
| 3 | ⚠️ **判据 4 字面未达** | `git diff` 的 `Assets` 面非空（`GameScene.unity` ＋ `TaskScheduler.cs`），**全部为前序未提交批**；本批零改动（§四逐条比对 `HH.324` §七 快照 ⇒ 同） |
| 4 | ⚠️ **本报告表体形态** | 采用「每行一小节 ＋ 8 栏竖排」（便于逐格挂 `file:line`）；若策划端要**单表 7×8 横排**，属格式转换（内容不变） |
| 5 | ⚠️ **账本登记未提交** | 本批红线 **⛔ 不 commit**，而 `vr-id-ledger`／`D640` 的常规做法为「取号 ＝ 只改本行 ＋ 独立 commit」⇒ 本端**遵本批红线**：`HH.326` 行已写入工作区、**⛔ 未提交**（请策划端随下次账本提交一并落，或另令本端补提交） |

---

## 七、零方案声明

本报告只含：7 行 × 8 栏交互表 ＋ `10` 复用对照 ＋ 判据 1～4 命令输出 ＋ 零改动声明 ＋ 停手检查 ＋ 待裁列报。⛔ 无方案、无建议、⛔ 未改任何生产码／文档。
