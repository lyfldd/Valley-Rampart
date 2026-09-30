# HH.346 小源⑤ `SiegeWorkshopBuilding` 独立只读预检报告（事务端）

> **性质**：独立只读预检（非施工许可、非源级判绿、非验收）。
> **任务书**：`多Agent交接/策划端/HH.341_M5-D乙加_小源⑤_SiegeWorkshopBuilding预检任务书.md`（签发 `D924`／任务书占位号 `HH.345`）。
> **开工基线**：`HEAD = b88983c112d48fc65d40c61d1becd5c634204b99`（短 `b88983c1`）。
> **当前源（锁定）**：`Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs`（278 行 · 无尾换行 · 12,744 B · `sha256=B83C2DDC…A33C3E`）。
> **证据前缀**：`Valley Rampart/Logs/hh341_small_siege_*`。
> **写入面**：本报告 ＋ 上述 `Logs` 前缀（3 个文件）。**源码／资产／场景／协议／账本零写入**（见 §九）。
> **报告版本**：**首版**（本文件；尚无补记版）。
> **观察项编号**：自 `V14` 起（第 4 源用至 `V13`）。

---

## 〇、结论摘要（只给「取得／未取得／停手待裁」）

| 预检项 | 判定 |
|---|---|
| ① 生产入口、初始化、三子仓注册、`ProductionSystem→ITickable→Tick`、轮产、原料与容量门、旧档迁移、清零、幂等、clamp、`OnDestroy` | **取得** |
| ② `TaskScheduler` 引用逐条映射（注册／收集／去重／派发／位移／工作／完成／放弃）；`TS_REF` 本源专用 `0→0→0` | **取得** |
| ③ 工人四能力（可接取／可移动／可完成／可放弃）**能力句** | **取得**（四句齐全，见 §三） |
| ④ 卡片适用性 | **任务卡源**（判定见 §四；⚠️ 但卡机制**未接入本源**，见 §四-3） |
| 五行集三数（`L-97`） | **取得**（见 §五；含 1 处 pattern 缺陷勘正） |
| `D/A/B/E/C` 五类接缝 | **五类均 0**（只登记，不补码） |
| `sim-sync` 义务 | **0**（未触 `AI.Core`／训练仓／champion／factor registry／`harness/Core`） |
| `V14`~`V19+` 观察项 | **登记完成**（7 项；观察窗统一标「不适用」） |
| 基线与零改动证明 | **取得**（`_Game` 零 diff；场景脏点 4/5 为既有挂账） |
| 四步落盘闸门 | **取得**（见 §十） |
| 停手待裁点 | **3 项**（见 §十一；含「账本占号登记」与「本源无自持存档口」） |

> ⛔ 本报告不写「施工完成」「源级通过」「判绿」。本源状态 ＝ **`SiegeWorkshopBuilding` 未开始 · 预检待裁**。

---

## 一、预检项① 生产入口、调用链与状态变化

### 1.1 初始化与三子仓注册（可复核链）

| 环节 | 位置 | 实读事实 |
|---|---|---|
| 组件挂载 | `BuildingComponentRegistry`（`BuildingComponents.cs:187/200`） | 键 `comp.siege_workshop` → `Add<SiegeWorkshopBuilding>`；`Add<T>`（`:235-243`）同类型已挂则跳过并 `c.Init(b)` |
| 数据行 | `SiegeWorkshop.asset:16-17` | `components: [comp.siege_workshop]` 唯一元素 |
| 挂载调用点 | `BuildingFactory.AttachComponents:243-255`（`CreateBuildingInstance:181` / `SpawnFromSave:319`） | 遍历 `def.components` → `TryAttach` |
| `Init` | `SiegeWorkshopBuilding.cs:38-49` | ①`_building = building` ②`Resources.Load<SiegeProductionConfig>("Config/SiegeProductionConfig")` ③`CreateSubStores(building)` ④消费旧档迁移桥并**置零**（`:44-48`） |
| `CreateSubStores` | `:53-71` | 逐类（`_cycleOrder`）`new GameObject("Ammo_" + type)` → `SetParent(building.transform, false)` → `AddComponent<StorageComponent>()` → `SetDeclaredPaths(new[]{ ResourceCatalog.PrimaryPathOf(type) })` → `capacity = Capacity()` → `WarehouseRegistry.Register(sc)`；**不调 `StorageComponent.Init`**（注释：防被 `def.warehousePaths` 覆盖专属声明）；末尾 `_accumulator = 0f` |
| 容量 | `Capacity():87-94` | `def.producer.capacity(30) × _building.LevelScale()`，`>0` 取 `Max(1, RoundToInt(...))`，否则回退 `100` |

三子仓声明路径（`ResourceCatalog.cs:52-54`）：`StoneAmmo→res_ammo.stone`／`FireballAmmo→res_ammo.fireball`／`MagicAmmo→res_ammo.magic`，**体积均为 1** ⇒ 容量 30 ＝ 每仓 30 发。

> ⚠️ `SiegeWorkshop.asset:38` 的 `warehousePaths: []` 为空 ⇒ 本源注释所述「会被覆盖」在当前数据下**不成立**（防御性写法，无实害）。

### 1.2 `ProductionSystem → ITickable → Tick`

- `ProductionSystem.cs:14` 单例，`_tickInterval = 1f`（`:13`），`Update:18-27` 累计后 `TickAll()`；
- `TickAll:32-53`：遍历 `BuildingRegistry.Instance.All` → 每栋 `GetComponents<ITickable>(_tickBuf)`（成员持有缓冲，`Clear()` 后取，稳态零分配）→ 逐项 `t.Tick()`（`:50`，含假 null 兜底 `:49`）。
- ⭐ 该遍历**不点名具体组件类**（`M4-A`／`HH.329`）⇒ 本源只需实现 `ITickable`（`:19`）即被驱动，**无需改 `ProductionSystem`**。

### 1.3 `Tick:108-132` 逐门核对

| 序 | 守卫 | 行 | 事实 |
|---|---|---|---|
| 1 | 宿主活性 | `:110` | `_building == null \|\| !_building.IsActive` ⇒ return（`Building.cs:293` `IsActive ⇒ state == Active`） |
| 2 | 懒注册 | `:111` | `LazyRegister()` **在岗门之前**（注释：否则无在岗时永不注册 ⇒ 永不自举） |
| 3 | 子仓就绪 | `:112` | `_stores.Count < 3` ⇒ return |
| 4 | 配置 | `:113` | `_config == null` ⇒ return |
| 5 | 速率 | `:115-116` | `RatePerSecond()` = `def.producer.rate(0.5) × def.GetGradeScale(grade) × LevelScale()`；`<=0` ⇒ return |
| 6 | 在岗门 | `:119` | `HasWorkerOnDuty`（`:251`）＝ `TaskScheduler.HasInstance && TaskScheduler.Instance.HasWorkerAssigned(this)`（**仅 `TaskState.Working` 计在场**，`TaskScheduler.cs:167-178`） |
| 7 | 累积/整数门 | `:121-123` | `_accumulator += rate`；`amount = FloorToInt(_accumulator)`；`<=0` ⇒ return（低速率未攒够不发） |
| 8 | 轮产取型 | `:126-127` | `type = _cycleOrder[_cycleIndex]` 后**立即** `_cycleIndex = (_cycleIndex+1) % 3`（⚠️ 索引推进**先于**产出，见 `V16`） |
| 9 | 产出 | `:129-131` | `Produce(type, amount)`；`produced > 0` 才 `_accumulator -= produced`（原料/容量不足则整批不产、累计器保留） |

`Produce:135-152` 逐门：`amt<=0`／仓内无该键 ⇒ 0；`store.IsFullFor(ammoType)`（`:139`）⇒ 0；`cost = AmmoCostFor`（`<=0` ⇒ 0）；`maxByRaw = AmountAvailable(raw) / cost`；`produce = Min(amt, maxByRaw, store.CanAccept(ammoType))`；`<=0` ⇒ 0；否则 `SpendRaw(raw, produce*cost)` → `store.Add(ammoType, produce)` → 返回 `added`。

原料映射（`:186-194`）：`FireballAmmo→FireOil`／`MagicAmmo→Crystal`／其余（`StoneAmmo`）`→Stone`；成本取 `SiegeProductionConfig`（`stoneAmmoCost=1`／`fireballAmmoCost=1`／`magicAmmoCost=1`）。

- `AmountAvailable:155-167`：先 `TreasureVault.Instance.GetAmount(raw)`（`>0` 即取），否则 `RulerController.Instance.GetResource(raw)`，否则 0。
- `SpendRaw:170-174`：`RulerController.Instance?.ModifyResource(raw, false, amt)` ⇒ **默认 `kingdomId=0`（玩家国库）**（`RulerController.cs:221`／`:230`），走国库统一入口（`TreasureVault.Get(0).Take`），**不双写**。

### 1.4 `LazyRegister → Register → TryAdvertiseTask`

| 环节 | 位置 | 事实 |
|---|---|---|
| `LazyRegister` | `:273-278` | `_registered \|\| !TaskScheduler.HasInstance` ⇒ return；否则 `TaskScheduler.Instance.Register(this)`；`_registered = true`（**幂等**） |
| `Register` | `TaskScheduler.cs:147-151` | `if (source == null) return; if (_sources.Add(source)) source.OnRegister();`（`HashSet<ITaskSource>` 天然去重） |
| `TryAdvertiseTask` | `:261-271` | ①`_building == null \|\| !IsValid` ⇒ false ②`_stores.Count < 3` ⇒ false ③`sched.HasWorkerAssigned(this)` ⇒ false（**重复派工门**）④`task = new KingdomTask(KingdomTaskType.Production, this)`；`task.destType = KingdomDestType.None`；返回 true |
| 字段 | `:253` `IsValid` = `this != null && _building != null && _building.IsValid`（`Building.cs:1489-1490`＝`state==Active \|\| _awaitingMaterials \|\| _demolishing`）｜`:255` `SourcePos` = `_building.transform.position` |
| 空体回调 | `:257-258` | `OnRegister()`／`OnUnregister()` 均**空实现** |

### 1.5 旧档迁移、清零、幂等、clamp 边界

| 环节 | 位置 | 事实 |
|---|---|---|
| 迁移桥声明 | `SiegeProductionSystem.cs:28-30` | `public static int LegacyStoneAmmo/LegacyFireballAmmo/LegacyMagicAmmo`（**静态**，跨对象存活） |
| 写入桥（读档） | `SiegeProductionSystem.cs:250-263` `LoadState` | `payload.typeName` 校验 → `data.ammoStock != null && Length >= 3` 时写入三桥并打日志 |
| 新档写出 | `SiegeProductionSystem.cs:239-248` `SaveState` | `saveDataVersion = 2`；**不赋值 `ammoStock`** ⇒ `version=2`、`ammoStock` 恒空（v2 空架） |
| 消费桥 | `SiegeWorkshopBuilding.cs:44-48`（`Init`） | `RestoreLegacyAmmo(三桥)` 后**立即三桥置 0** ⇒ **防重复消费**（幂等靠「消费即清零」而非状态位） |
| 落仓 | `:219-226` `RestoreLegacyAmmo` → `:228-235` `DepositDirect` | 逐仓 `s.Add(ammoType, amt)`；`added < amt` ⇒ `Debug.LogWarning("超容量 clamp {amt}→{added}")`（**不静默丢弃**，与 `StorageComponent.RestoreContents:375-376` 同型） |
| 全局清零 | `SiegeProductionSystem.cs:265-269` `ResetState` | 三桥置 0（世界重置时丢弃未消费缓存） |

**边界（实读）**：
- `RestoreLegacyAmmo` 内容 `< = 0` 时 `DepositDirect` 直接 return（`:230`）⇒ 无副作用；
- `DepositDirect` 若 `_stores` 无该键（例如 `CreateSubStores` 因 `building == null` 早退 `:55`）⇒ **静默 return，无日志**；
- `CreateSubStores` 在 `RestoreLegacyAmmo` **之前**（`:42` 先于 `:44`）⇒ 正常路径下三仓已就绪；
- 容量 clamp 在 `Add` 内按 `CanAccept`（体积 1，容量 30）⇒ 单仓上限 30。

### 1.6 清零与 `OnDestroy`

| 方法 | 位置 | 事实 |
|---|---|---|
| `ResetAll` | `:210-214` | 逐仓 `kv.Value.Clear()`（发 `OnStorageChanged`），`_accumulator = 0f` |
| `OnDestroy` | `:237-246` | `_registered && TaskScheduler.HasInstance` ⇒ `TaskScheduler.Instance.Unregister(this)`；逐仓 `WarehouseRegistry.Unregister(kv.Value)`；`_stores.Clear()` |
| `Unregister` 连带 | `TaskScheduler.cs:153-159` | `_sources.Remove` + `OnUnregister()` + `OnBuildingDied(source)`（清该源在派任务） |
| 子仓 `OnDestroy` | `StorageComponent.cs:74-77` | 子仓自身 GameObject 随宿主销毁时亦 `WarehouseRegistry.Unregister(this)` ⇒ **双保险** |

### 1.7 存档面（任务书 §三.1-5 指定项）

- ⭐ **本源无自持存档方法**：`grep -nE 'ISaveable|SaveState|LoadState|SaveId|SavePayload'` 对本源 **rc=1（零命中）** ⇒ **代码面未见**。本源未实现 `ISaveable`，无 `SaveState/LoadState`。
- 宿主侧事实：`Building.SaveState`（`Building.cs:1100-1144`）仅收集四处仓内容 —— `storageContents`（`GetComponent<StorageComponent>()`，**本体**）／`treasuryContents`（`GetComponent<TreasureVault>()`）／`byproduct*Amount`（`GetComponent<MineByproductComponent>().SaveByproductState()`）／`siteContents`（`_siteStore`）。**无 `SiegeWorkshopBuilding` 专属字段**。
- 全库 `GetComponentsInChildren<StorageComponent>` **rc=1（零命中）** ⇒ 无子物体仓扫描。
- ⇒ 三弹药子仓挂**子 GameObject**（`:60-61`），其内容**不进入 `BuildingSaveData.storageContents`**。
- 唯一补偿＝`SiegeProductionSystem` 的**v1 旧档**迁移桥（§1.5）；v2 档 `SaveState` 恒写空架 ⇒ **新档无弹药持久化路径**。
- ⚠️ **本项只登记事实，是否构成缺口属主策划裁定**（列 §十一 待裁 R2）。

---

## 二、预检项② `TaskScheduler` 引用逐条映射

### 2.1 本源 `TaskScheduler` 引用逐条（共 6 代码行 / 7 匹配）

| # | 行 | 原文 | 映射到调度器哪一支 |
|---|---|---|---|
| 1 | `:239` | `if (_registered && TaskScheduler.HasInstance)` | **注销前置守卫**（`OnDestroy`）→ `Unregister` |
| 2 | `:240` | `TaskScheduler.Instance.Unregister(this);` | 注销（含 `OnBuildingDied` 清理） |
| 3 | `:251` | `bool HasWorkerOnDuty => TaskScheduler.HasInstance && TaskScheduler.Instance.HasWorkerAssigned(this);` | **在岗查询**（`HasWorkerAssigned`，仅 `Working` 计在场）—— 供 `Tick` 在岗门 |
| 4 | `:266` | `var sched = TaskScheduler.Instance;` | `TryAdvertiseTask` 内取单例，供 `:267` 重复派工门 |
| 5 | `:275` | `if (_registered \|\| !TaskScheduler.HasInstance) return;` | **懒注册前置守卫**（`LazyRegister`） |
| 6 | `:276` | `TaskScheduler.Instance.Register(this);` | **注册**（首次 `Tick` 自举） |

### 2.2 调度器侧七支对照（本源是否被覆盖）

| 调度器支 | 位置 | 本源覆盖情况 |
|---|---|---|
| 注册 | `TaskScheduler.cs:147-151` `Register` | ✅ 经 `:276` |
| 收集 | `:322-334`（`Tick` ③ 遍历 `_sources` 调 `TryAdvertiseTask`） | ✅ 源广告 `Production` |
| 去重 | `:331` `HasAssignedTaskForSourceType(s, task.type)`／`:1416-1424`（键＝`advertiser ?? source`） | ✅ 本源 `advertiser` 保持 `null` ⇒ 键＝`source`＝本源组件 |
| 派发 | `:424-459` `Dispatch` | ✅（经 `:369` `Dispatch(idle[best], task)`） |
| 位移 | `:457` `NavigateToSource` → `:479-487` → `:497-505` `EnsurePfAndSetDest` | ✅ 走向 `SourcePos` |
| 工作 | `:588-672` `TaskState.Working` 分支（占位劳作 + 计时） | ✅ 到岗后 `Working` |
| 完成 | `:730-755` `Complete` → `:876-931` `ExecuteCompletion` | ⚠️ **完成动作对本源为空操作**（见 §三-3） |
| 放弃 | `:780-810` `Abandon` | ✅（多 `reason`） |

### 2.3 `TS_REF`：本源专用分支 `0→0→0`

```
$ git grep -n 'SiegeWorkshop' -- 'Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs'
（rc=1，无输出）
```

⇒ **三数 `0(原始) → 0(排除) → 0(有效)`**：`TaskScheduler.cs` 全 1677 行内**零**本源专用标识/分支。

### 2.4 通用调度分支（单列，**不归因本源**）

`TaskScheduler.cs` 内被本源**间接消费**的通用支：`:147` Register／`:153` Unregister／`:167` HasWorkerAssigned／`:262` Tick 主循环（① 清无效源 ② 收集空闲 NPC ③ 收集可派任务 ④ 排序派工 ⑤ 推进态）／`:424` Dispatch／`:511` UpdateAssignedTasks／`:730` Complete／`:780` Abandon／`:876` ExecuteCompletion／`:1335` ResolveDest／`:1598` SourceKingdom。

- `SourceKingdom`（`:1598-1610`）对本源（`Component` 源、非 `Building`）取 `c.GetComponentInParent<Building>().kingdomId`（`:1604-1608`）⇒ 按宿主建筑归属国路由（池隔离）。
- ⚠️ 注意：本源**不是** `Building` 子类 ⇒ 不走 `:1602` 分支，走 `:1604` `Component` 分支。

---

## 三、预检项③ 工人四能力（能力句）

> 口径：真实产弹只能由本源 `Tick/Produce` 与三子仓变更证明；`Working`／`Complete`／`ExecuteCompletion` **互不替代**。

**① 可接取（能力句）**：`SiegeWorkshopBuilding` 作为 `ITaskSource` 在自身 `Tick:111` 经 `LazyRegister:273-278` 注册进 `TaskScheduler._sources`；调度器每 tick（`:322-334`）遍历源调 `TryAdvertiseTask:261-271`，在三子仓就绪（`:265`）且无在岗（`:267`）时返回 `KingdomTask(Production, this)` 且 `destType=None`；调度器随后 `ResolveDest:1335-1356`（`None` ⇒ `destPos = SourcePos`）、按「源＋类型」去重（`:331`）与池路由（`:350` `SourceKingdom`）后由 `:369` `Dispatch` 派给同国空闲工人 ⇒ **可接取：取得**。

**② 可移动（能力句）**：派发时 `Dispatch:457` 调 `NavigateToSource:479-487`，经 `GridSystem.WorldToSubCoord(SourcePos)` 吸附后由 `EnsurePfAndSetDest:497-505` 设 `PathFollower.SetDestination`；状态机在 `UpdateAssignedTasks:548-551` 将 `Assigned→MovingToSource` 并每 tick `InjectStimulus` 续命，`:556-558` 距离 ≤ `ArrivalThreshold` 时转 `Working` ⇒ **可移动：取得**。

**③ 可完成（能力句）**：到达后 `:558-559` 置 `Working` 并记 `_workStartTime`；`Working` 分支（`:588-672`）计时达 `GetTaskDuration:1634-1648`（本源非 `Gather`/`Demolish` ⇒ 返回 `workDuration = 2f`）后落入 `else` ⇒ `Complete:668` ＋ `stale.Add(Unknown)`（惰性）；`Complete:730-755` 调 `ExecuteCompletion:876-931`，其 `Production` 分支（`:882-885`）查 `comp.GetComponent<ProducerComponent>()`——⭐ **本源宿主 GameObject 未挂 `ProducerComponent`**（`SiegeWorkshop.asset` 数据行仅 `comp.siege_workshop`；本源类注释 `:14` 明载「不挂通用 `ProducerComponent`」）⇒ `prod == null` ⇒ **该分支对本源为空操作**；随后 `brain.IsKingdomTaskWorker = false`、`RemoveTaskStimulus`、`ClearNpc:812-820` 移除记录 ⇒ 工人释放、下 tick 可重派 ⇒ **可完成（任务收口）：取得**。

**④ 可放弃（能力句）**：`Abandon:780-810` 由 `AbandonTask`／`OnNpcDied`／`OnPathFailed`／`OnBuildingDied`，及状态机 `stale` 汇聚（`BrainLost`／`Dead`／`SourceInvalid`／`Timeout`／`DestFull`／`Unknown`）触发；本源任务 `destType=None` ⇒ 不进入 `MovingToDest` 段 ⇒ 实际可达 `reason` ＝ `External`／`Dead`／`Unreachable`／`SourceInvalid`／`Timeout`／`BrainLost`／`Unknown`（`DestFull` 属 `MovingToDest` 专属，本源不可达）；`Abandon` 复位工人 + `RemoveTaskStimulus` + `ClearNpc` ⇒ **可放弃：取得**。

### 3-3 ⭐ 真实产弹（不得与上列互替）

- **唯一产弹承载 ＝ `ProductionSystem → ITickable → SiegeWorkshopBuilding.Tick:108-132 → Produce:135-152 → store.Add`**，且**必须先过在岗门（`:119`，`HasWorkerOnDuty` ⇒ 工人处于 `TaskState.Working`）**。
- ⛔ `ExecuteCompletion` 的 `Production` 分支（`:882-885`）对本源**不产弹**（无 `ProducerComponent`）—— 即「`Complete` ≠ 产弹」在本源**实证**。
- ⇒ 真实产弹＝**「工人到岗 `Working` 期间，`ProductionSystem` 每秒驱动 `Tick`，按 `rate × 在岗秒数` 累加、`floor` 后扣原料入子仓」**。当前数据（`rate=0.5`／`workDuration=2s`／`tickInterval=1s`）下，一轮 `Working`（≈2 游戏秒）至多累加 ≈1.0 ⇒ 至多产 1 发/轮。
- ⚠️ 本预检为**只读**，**未进局实测**该链；上述为代码面推导，运行时读数须由施工／验证轮取得（见 §八 `V19+`）。

---

## 四、预检项④ 卡片适用性判定

### 4.1 判定：**任务卡源**

依据（逐条可达）：
1. **真实任务对象**：`TryAdvertiseTask:261-271` 产出真实 `KingdomTask`（`:268`），非影子；
2. **调度器收集/派发**：`TaskScheduler.Tick` ③ 收集（`:322-334`）＋ ④ 派发（`:346-370`／`Dispatch:424-459`）；
3. **工人可接取/移动/完成/放弃**：§三 四能力句均「取得」；
4. **有效性与收口边界**：`IsValid:253` 绑宿主 `Building.IsValid`；任务收口走 `Complete:730`／`Abandon:780`（含 `OnDestroy:237-246` 注销时 `OnBuildingDied` 清在派任务）。

### 4.2 ⚠️ 与「造卡」相关的关键限制（预检事实，非裁定）

- 本源**零引用协议六文件**（`TaskCardProtocol`／`TaskLifecycleRules`／`TaskBindingTypes`／`TaskBindingManager`／`TaskProtocolRuntime`／`TaskProtocolIssuer`）—— `grep` 对本源**全部零命中**。
- 本源**未持 `TaskCard`**：无 `_card`／`slot`／`Issuer` 使用；`advertiser` 字段保持 `null`。
- ⇒ 本源当前处于「**任务卡源·但未接卡机制**」状态：与首片（`WorldGatherSource`，`D875` 放行条件含「生产面须出真实新协议实例」）及第 4 源（`BlacksmithBuilding`，`D922` 补 `D/A/B/C＋E` 最小接缝）**不同**——本源**尚无任何源侧接缝**。
- ⛔ **非任务卡源**判定**不成立**（本源确产真实 `KingdomTask`）；但**是否要求本源补六文件接缝／是否要求真实 Play `TaskCard`** ⇒ **属主策划后续施工轮裁定**（本窗口不补码）。

---

## 五、五行集三数（`L-97` 固定格式）

> 完整逐行与复现命令见 `Valley Rampart/Logs/hh341_small_siege_precheck_stat.txt`（**首版**）。下表为汇总。

### `SRC_SELF`（本源文件内）

| pattern 原文 | 口径标签 | 观测范围 | 原始→排除→有效 | 备注 |
|---|---|---|---|---|
| `TaskScheduler\.(Instance\|HasInstance)` | 代码行 | 本源 278 行 | **6 → 0 → 6** | 匹配总数 7（`:251` 单行双匹配） |
| `new KingdomTask\(` | 代码行 | 同上 | **1 → 0 → 1** | `:268` |
| `ITaskSource` | 代码/注释分列 | 同上 | **2 → 1 → 1** | 代码 `:19`；注释 `:248` |

复现：`grep -nE 'TaskScheduler\.(Instance|HasInstance)' <SRC>` 等三条。

### `TS_REF`（`TaskScheduler.cs` 内本源专用）

| pattern 原文 | 口径标签 | 观测范围 | 三数 |
|---|---|---|---|
| `SiegeWorkshop` | 代码行 | `TaskScheduler.cs` 全 1677 行 | **0 → 0 → 0** |

复现：`grep -n 'SiegeWorkshop' <TS>`（rc=1）。通用调度分支见 §2.4。

### `SEAM`（`TaskScheduler.cs` 内 D/A/B/E/C 逐类）

| 类 | pattern 原文 | 命中行数 | 其中本源 | 全量 `file:line` |
|---|---|---|---|---|
| D 源失效 | `OnProtocolSourceInvalidated` | 5 | **0** | `:275`(wg) `:278`(site) `:281`(chest) `:284`(mb) `:288`(bs) |
| A 派发 | `OnProtocolDispatched` | 5 | **0** | `:437` `:440` `:443` `:446` `:449` |
| B 到达 | `OnProtocolArrived` | 5 | **0** | `:562` `:565` `:568` `:571` `:575` |
| E 完成 | `OnProtocolTaskCompleted` | 4 | **0** | `:743`(site) `:746`(chest) `:749`(mb) `:753`(bs) |
| C 放弃 | `OnProtocolAbandoned` | 5 | **0** | `:795` `:798` `:801` `:804` `:807` |
| 类型判定 | `is (五源\|SiegeWorkshopBuilding)` | 30（29 代码 ＋ 1 注释） | **0** | 见 `_stat.txt`（`_stat` 首版逐行列全） |

> ⚠️ E 完成接缝在 `TaskScheduler.cs` 内**只有 4 家**（`site`/`chest`/`mb`/`bs`）；`WorldGatherSource` 的收口走 `ExecuteCompletion:928 wg.OnGatherCompletion()`，不在 `Complete` 内挂接缝 —— 该事实**不改变**「本源 E 接缝 ＝ 0」。

### `DEFDATA`（数据行）

| 项 | 位置 | 值 |
|---|---|---|
| `comp.siege_workshop` | `SiegeWorkshop.asset:16-17` | **1**（`components` 数组唯一元素） |
| `isSiegeWorkshop` | `:57` | **1** |
| `producer` | `:40-43` | `{kind:0(Resource), rate:0.5, capacity:30}` = **1 块** |
| `warehousePaths` | `:38` | **0**（空数组 `[]`） |
| `id` | `:15` | `SiegeWorkshop` |
| 互斥标记 | `:55-56`／`:77`／`:61` | `isResourceNode=0`／`isBlacksmith=0`／`isMineByproduct=0`／`isSiegeWorkshop=1` |
| `stoneAmmoCost` / `fireballAmmoCost` / `magicAmmoCost` | `SiegeProductionConfig.asset:47-49` | **1 / 1 / 1** |

复现：`grep -nE 'comp.siege_workshop|isSiegeWorkshop|producer|kind|rate|capacity' <ASSET>`。

### `ALLREF`（全 `Valley Rampart/Assets/**`）

| 分类 | 行数 | 说明 |
|---|---|---|
| 生产域 `_Game/*.cs` | **16** | 其中**编译期类型引用仅 1**：`BuildingComponents.cs:200 Register(SiegeWorkshop, Add<SiegeWorkshopBuilding>)`；自身声明 1（`:19`）；其余 **14 为注释/Tooltip 字符串**（`BuildingDef.cs:87` Tooltip／`UtilityScorer.cs:47`／`MineByproductComponent.cs:9/22/60/116/125/142`／`SiegeProductionSystem.cs:9/12/16/27/237`／`UnitController.cs:782`） |
| Editor/Smoke/ChainAudit | **19** | 编译期类型引用 4（`GetComponent<SiegeWorkshopBuilding>()`：`Valley_HH107_Smoke_Byproduct.cs:310`／`Valley_HH111_Smoke_MachineEntry.cs:96`／`Valley_OB12_InGate_Probe.cs:107/161`）；其余 15 为字符串/注释 |
| 生成物/二进制 | **1** | `Assets/Unity.VisualScripting.Generated/.../UnitOptions.db`（非源码面） |
| 文档 | 0 | — |

⇒ **原始 36 → 排除（二进制）1 → 有效**：生产域编译期引用 **1** ＋ Editor 引用 4。⛔ **不得**把全 `Assets` 对照数（36）当作生产调用面。

---

## 六、`D/A/B/E/C` 接缝现状盘点（只登记，**不补码**）

| 类 | 语义 | `TaskScheduler.cs` 内同型接缝 | 本源专用 |
|---|---|---|---|
| **D 源失效** | 源失效清理、未派任务放弃 | 5 家（`WorldGatherSource`／`ConstructionSiteStore`／`ChestEntity`／`MineByproductComponent`／`BlacksmithBuilding`），`:274-288` | **0** |
| **A 派发** | 协议配对与预定 | 5 家，`:436-449` | **0** |
| **B 到达** | `MovingToSource→Working` | 5 家，`:561-575` | **0** |
| **E 完成** | `Complete` 后收口 | 4 家（无 `WorldGatherSource`），`:742-753` | **0** |
| **C 放弃** | `Abandon` 后回待派/封口 | 5 家，`:794-807` | **0** |
| 类型判定 | `is <源类型>` | 30 行（29 代码＋1 注释） | **0** |

- 本源在 `TaskScheduler.cs` 内**五类接缝全为 0**；本源自身亦**零引用协议六文件**（§4.2）。
- 是否需要接缝、接缝落组件侧还是本体侧 ⇒ **留待另行施工裁定**（沿用 `D923`「循环常驻源接缝统一挂组件侧、`Building.cs` 保持禁止面」口径；本窗口 ⛔ 不补码）。
- ⚠️ 任务书 §五 要求「本源五类专用接缝预期均为 0」—— **实测吻合**。

---

## 七、`sim-sync` 义务

- 本窗口**未触** `AI.Core`／训练仓／`champion`／`factor_registry`／`harness/Core`。
- 只读扫描证明零命中：本源对以上任一无引用。
- ⇒ **`sim-sync` 义务 ＝ 0**。
- 若后续发现「必须改 `AI.Core`／训练仓／`champion`／`factor registry`／`harness/Core` 才能继续」⇒ 立即停手回呈影响面（本窗口未发生）。

---

## 八、观察项登记（`V14`~`V19+`）

> **观察窗**：⛔ 本只读预检**无 `L-98` 观察窗**（无进局、无对照臂）⇒ 全部 7 项观察窗**统一标「不适用」**。⛔ 不得伪造运行时读数。

| 编号 | 事实 | `file:line` | 观测范围/筛选 | 状态 | 阻塞 | 归属 |
|---|---|---|---|---|---|---|
| **V14** | 三子仓**同容量**（每仓 = `def.producer.capacity(30) × LevelScale()`）；满仓判断**逐仓独立**（`IsFullFor`／`CanAccept` 按体积 1 算）；是否有「三仓分别归零」需求：`ResetAll` 已**三仓全清**＋累加器清零 | `:56-70` `:74-78` `:87-94` `:139` `:145-152` `:210-214` | 本源全 278 行 | 已取得（代码面）；「是否需分别归零」＝未取得（待裁） | 否 | 主策划裁定轮 |
| **V15** | `HasWorkerOnDuty:251` **只查本组件**（`HasWorkerAssigned(this)`）；**不并计**宿主建筑在岗。对照第 4 源 `BlacksmithBuilding.cs:70-71` 为 `HasWorkerAssigned(this) \|\| (_building != null && HasWorkerAssigned(_building))`（**双查**）⇒ 两源**口径不一致**：本源工人若被派给宿主建筑的 `Transport` 任务（`source=Building`），本源视为无在岗 ⇒ 停产 | `:251`；对照 `BlacksmithBuilding.cs:70-71` | 两源逐行 | 已取得（差异事实）；是否有语义问题＝未取得 | 否 | 主策划裁定轮 |
| **V16** | `_cycleOrder={Stone,Fireball,Magic}`；`Tick:126-127` **先取型、后立即推进索引**，再 `Produce` ⇒ 索引推进**与产出成败无关**（本 tick 产不出也换品种）⇒ 单原料可用时会出现「轮到缺料品种 ⇒ 空转一轮再回来」的节奏。合法无产出集：子仓<3／无配置／`rate<=0`／无在岗／`amount<=0`／`IsFullFor`／`cost<=0`／原料不足／`CanAccept<=0` | `:30-32` `:121-131` `:139-146` | 本源 | 已取得（代码面；⛔ 无运行时读数） | 否 | 主策划裁定轮 |
| **V17** | 旧档迁移桥链完整（`LoadState`→静态三桥→`Init` 消费→**消费即清零**）；v2 新档 `SaveState` 恒写空架。⚠️ **容量 clamp 时序**：`CreateSubStores` 用**建仓时**的 `LevelScale()`；读档路径 `BuildingFactory.SpawnFromSave` 在 `CreateBuildingInstance`（内含 `Init`）**之后**才恢复 `level`（`BuildingFactory.cs:319` 早于 `:339`）⇒ 高等级档**弹仓容量按 Lv1 计算**（`M1-B` 件4 对**本体仓**的同类修复**未覆盖子仓**）。⚠️ 当前 `SiegeWorkshop.asset:51 levels: []` ⇒ `LevelScale()` 恒 1 ⇒ **暂无实害**；且 `RefreshRate`／`RefreshCapacity` 在**生产域零调用面**（`Building.OnConstructionComplete:723-731` 只刷 `ProducerComponent`／本体 `StorageComponent`） | `:38-49` `:53-71` `:80-85`；`SiegeProductionSystem.cs:239-269`；`BuildingFactory.cs:319/339/342-348`；`Building.cs:714-733` | 本源＋宿主读档链 | 已取得（代码面） | 否 | 主策划裁定轮（附条件触发） |
| **V18** | 经济结算**跳过**投掷机厂：`AbstractEconomySettlement.cs:199 if (def.isSiegeWorkshop) continue;`（注释「投掷机厂产弹药，非经济资源不入国库」）。弹药**真源 ＝ 本源三子仓**，**不进国库**（`TreasureVault.VaultPaths={res_material,res_food,res_currency}`，`TreasureVault.cs:25-26` 明注「⛔ 不含弹药」。⚠️ 全库 `isSiegeWorkshop` 生产域仅 1 处消费（该行）＋ `BuildingDef.cs:88` 声明。弹药公共入口：本源 `GetAmmo/TakeAmmo/GetStore`（`:199-208`，**生产域零调用面**，仅 Editor 探针）＋ 通用装填链 `TaskScheduler.LoadAmmoToBackpack:1222-1249`（`FindObjectsOfType<StorageComponent>` 全扫，`IsChestStore` 只滤箱容器 ⇒ **本源子仓是合法取货点**，但经**通用仓面**而非本源专属口） | `AbstractEconomySettlement.cs:199`；`TreasureVault.cs:25-26`；本源 `:199-208`；`TaskScheduler.cs:1222-1249` | 全库 `isSiegeWorkshop` ＋ 弹药资源面 | 已取得 | 否 | 主策划裁定轮 |
| **V19** | ⭐ **本源弹药子仓内容不随建筑存档**：`Building.SaveState:1100-1144` 收集的仓面仅 4 处（本体 `storageContents`／`treasuryContents`／`byproduct*Amount`／`siteContents`），**无本源字段**；全库无 `GetComponentsInChildren<StorageComponent>` ⇒ 子物体仓**不被扫描**。⇒ v2 新档往返后三子仓重建为空；唯一补偿＝**v1 旧档**迁移桥（一次性、消费即清零） | `Building.cs:1100-1144`；`BuildingSaveData.cs:33-41`；本源 `:38-49`（无存档口） | 全库存档面 | **已取得（事实）**；是否构成缺口＝**未取得（待裁）** | **待裁** | 主策划裁定轮（见 §十一 R2） |
| **V19b** | 本源**公开口零生产调用面**：`Produce`（`:135`）／`GetAmmo`／`TakeAmmo`／`IsReady`（`:36`）在 `_Game` 生产域 **0 行**（仅 Editor 探针 `Valley_HH107_Smoke_Byproduct`／`Valley_HH111_Smoke_MachineEntry`／`Valley_OB12_InGate_Probe`）；`RefreshRate`／`RefreshCapacity`／`ResetAll` 生产域亦**无本源调用**。⇒ 本源对外仅「`ITaskSource` 广告 ＋ `ITickable` 被驱动」两条在役面 | 全库调用面（`_evidence.txt` §`META_CALLSITES`） | `_Game` ＋ `Editor` | 已取得 | 否 | 主策划裁定轮 |
| **V19c** | 真实产弹链**未进局验证**：`Tick→Produce→store.Add` 为代码面推导；⛔ 本窗口无进局、无 `L-98` 观察窗 ⇒ 无运行时读数 | `:108-152` | — | **未取得（工具/窗口不足）** | 否（待施工/验证轮） | 施工验证轮 |

---

## 九、基线复算与零改动证明

### 9.1 三行集 HEAD / 工作树（复算命令见任务书 §八）

| 行集 | pattern 原文 | HEAD 总行 | 工作树总行 | 逐文件差异归因 |
|---|---|---|---|---|
| `SchedRef` | `TaskScheduler\.(Instance\|HasInstance)` | 81 | 81 | **无差异**（工作树 17 文件，含本源 6 行） |
| `NewKT` | `new KingdomTask\(` | 15 | 15 | **无差异**（工作树 10 文件，含本源 1 行） |
| `ITaskSrcDecl` | `class[[:space:]]+[A-Za-z0-9_]+[^\n{]*ITaskSource\|struct…` | 2 | 2 | **无差异** |
| `ITaskSrcDeclFIX`（勘正口径） | `class[[:space:]]+[A-Za-z0-9_]+[^{]*ITaskSource\|struct…` | 9 | 9 | **无差异** |

⭐ **pattern 缺陷勘正（`L-97` 家族）**：任务书 §八 给定的 `ITaskSrcDecl` pattern 含字符类 `[^\n{]` —— 在 POSIX ERE 下 `[^\n{]` 等价「排除字面 `n` 与 `{`」⇒ **凡类型名或接口列表中含小写 `n` 者被漏掉**。实测：
- `ITaskSrcDecl`（原文）＝ **2** 命中（仅 `WorkerTask.DebugTaskSource`／`WorldGatherSource`）；
- ⚠️ **本源 `SiegeWorkshopBuilding.cs:19` 未被该 pattern 命中**（因其含 `MonoBehaviour` 的 `n`）；
- 修正口径（去 `\n`）＝ **9** 命中（`Building`／`ConstructionSiteStore`／`MineByproductComponent`／`BlacksmithBuilding`／`SiegeWorkshopBuilding`／`UnitController`／`DebugTaskSource`／`ChestEntity`／`WorldGatherSource`）。
⇒ ⛔ **不得**把原文 pattern 的「2」当作 `ITaskSource` 实现者全集（否则「未观测到」＝误读为「未覆盖到」）。两口径**总行数 HEAD 与工作树均相等** ⇒ 零改动结论不受影响。

### 9.2 边界与场景

- `git diff --name-only -- 'Valley Rampart/Assets/_Game'` ⇒ **空**；
- `git diff --numstat HEAD -- 'Valley Rampart/Assets/_Game'` ⇒ **空**；
- ⇒ **`_Game` 域零改动**（源码／资产／探针）。
- `GameScene.unity` 工作树 `sha256=9BB5AAF7…F0C1E`（109,704 B）／blob `46db7cfebebc380514901d378562eeffee245ede`；`git diff --numstat HEAD` ⇒ **`4/5`** ⇒ **开工前既有脏点**（与挂账 `O-14` 同源），⛔ 不纳本源改动面、⛔ 不回滚、⛔ 不提交。
- ⚠️ **账本脏点声明**：开工首条 `git status --porcelain` 即已列出 `多Agent交接/_交接索引.md`／`_任务队列.md`／`_当前快照.md`／`_编号登记.md` 与 `河谷防线开发计划书具体内容/测试基线台账.md` **五本为 `M`（开工前既有）**。本窗口按 §2.2 禁止面**未写任何账本**；落盘后复验 `git status` ⇒ **无新增 `M`**，五本状态逐字未变（详见 §十一 `R1`）。
- **本窗口写入面清点（全量）**：①本报告；②`Valley Rampart/Logs/hh341_small_siege_precheck_stat.py`／`_stat.txt`／`_evidence.py`／`_evidence.txt`。**共 5 个新增文件**，无其它写入。

### 9.3 本源文件指纹

| 项 | 值 |
|---|---|
| 路径 | `Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs` |
| 行数 | **279 行文本 / `nl_count=278`（无尾换行）** |
| 字节 | 12,744 |
| `sha256` | `B83C2DDC2C102D0654C12489A6E42E89978E6B2D6D0D205EB36EAC05E1A33C3E` |
| `git show HEAD:<path>` vs 工作树 | **逐字节一致 = True** |

---

## 十、四步落盘核验闸门

| 步 | 执行 | 读数 |
|---|---|---|
| ① 写入前后 mtime | 脚本记录 | `_stat.py` mtime `1790758515`／`_stat.txt` `1790758523`／`_evidence.py` `1790758643` |
| ② 从磁盘重读全文 | 脚本已读回并逐段打印 | 见 `_stat.txt`、`_evidence.txt`（首版） |
| ③ `sha256` ＋ 登记长度 | 已登记 | 见 `_evidence.txt` §`META_GATE` 与 §9.3 |
| ④ 事务端字节一致性核对 | `git show HEAD:<SRC>` 与工作树比对 | **一致 = True**；新增证据文件走 `sha256` ＋ 磁盘重读（未入库 ⇒ 无 blob 可对） |

> ⚠️ 本批全部交付物**未 `git add`／未 `commit`／未 `push`**（预检窗口禁令）；四步闸门④对**已入库**文件用 `git show`／`git hash-object`，对**新增**文件用 `sha256` 双读。

---

## 十一、停手待裁 / 待裁点

| 编号 | 事项 | 为什么停手 |
|---|---|---|
| **R1** | **完成报告号的账本占号登记未执行** | 按 `vr-id-ledger` §二，取号＝「先登记后落盘、登记即占号」（须改 `_编号登记.md`）。而本预检窗口 §2.2 禁止面明列「不写四本账本」⇒ 两项纪律**正面冲突**。本报告按「水位线 `HH.345` ＋ 1」定量为 **`HH.346`** 并**以此命名落盘**，但**账本占号行未写** ⇒ 请主策划裁定：由裁定轮补登记，或改判号归属 |
| **R2** | **本源弹药子仓内容不随建筑存档（`V19`）** | 已取得事实（`Building.SaveState` 无本源字段／无子物体仓扫描），**是否构成缺口**（尤其对照 `MineByproductComponent` **有**专属存档口 `SaveByproductState()` 的第 3 源）⇒ 属主策划裁定；本窗口 ⛔ 不改码 |
| **R3** | **本源零协议六文件接缝 ＋ `V15` 在岗口径与第 4 源不一致 ＋ `V17` 容量 clamp 时序** | 三者为**施工面决策**（是否补接缝／是否统一在岗口径／是否补 `RefreshRate` 调用）⇒ 本窗口 ⛔ 不补码，报裁 |

> ⛔ 另：本窗口**不申请第 6 源**、**不自行施工**、**不改本源/调度器/协议/资产/场景/账本**。

---

## 十二、取号与后续

- 任务书占位号：`HH.345`（**已登记**，核对维持）。
- 本报告（完成报告）拟用号：**`HH.346`**（＝实时水位线 `HH.345` ＋ 1）——⚠️ 见 §十一 `R1`（账本占号未执行）。
- **回呈主策划**：请裁定 ①是否准开第 5 源施工窗口 ②`R1`~`R3` 三项处置 ③是否要求本源补协议接缝／真实 Play `TaskCard`。
- 五小源阶段**收口条件**：仅当第 5 源**判绿**后（本轮 ⛔ 未判）。

---

*报告完（首版）。证据：`Valley Rampart/Logs/hh341_small_siege_precheck_stat.py` / `_stat.txt` / `_evidence.py` / `_evidence.txt`。*
