# HH.341 · 小源首源 `ConstructionSiteStore` · **只读预检**交付报告

> 执行端 ｜ 2026-09-26 ｜ 唯一正文：`多Agent交接/策划端/HH.341_M5-D乙加_小源阶段任务书.md`（258 行 · 已全文直读 · 入库 `7c21f1ba`）
> 性质：⭐ **只读预检（零代码改动）** —— 按任务书 §五「未完成卡片适用性判定，不得写代码」
> ⛔ 未取新号、未写 D 号、未提交、未 push；⛔ 未动其余四源／两巨兽／`_crewAssignments`／`currentWorkers`／场景／资产／`AI.Core`／账本

---

## 〇、开工回执（任务书 §十一-1）

| 项 | 内容 |
|---|---|
| 当前源 / 顺序位置 | `ConstructionSiteStore` ／ 顺序 **第 1**（任务书 §2.2 表定，⛔ 未自行换序） |
| 前置基线 | D1 绿态：`TaskScheduler.Instance\|HasInstance` **59 行 / 16 文件**；`new KingdomTask(` **14 行 / 9 文件**（剔注释）；`ITaskSource` **9 类**；`KingdomTaskType` **11 项** |
| 独立回退点 | HEAD **`7c21f1ba`**；本子批**零改动**（见 §五） |
| 只读预检结论 | **四项全部取得**（§一） |
| ⭐ 卡片适用性判定 | **任务卡源**（§二） |

---

## 一、预检 4 项（逐项 `file:line` 锚点）

### 1. 生产触发入口 · 真实调用链 · 源状态变化 `[实读]`

| 环节 | 位置 | 说明 |
|---|---|---|
| **创建（惰性）** | `Building.cs:546-554`（`EnsureSiteStore`） | `new GameObject("SiteStore")` → `AddComponent<ConstructionSiteStore>()` → `Init(this, _siteNeed)`；子物体容器，⛔ 不挂 `StorageComponent` |
| **注册（入任务源表）** | `Building.cs:556-560`（`RegisterSiteStore`） | `TaskScheduler.Instance.Register(_siteStore)`（⚠️ **注册点在 `Building` 侧**，非本源文件） |
| 注册触发 | `Building.cs:528-543`（`BeginMaterialPhase`） | `:538` `EnsureSiteStore()` → `:539` `SetNeed` → `:540` `_awaitingMaterials = !IsSatisfied` → `:541` `if (_awaitingMaterials) RegisterSiteStore()`；另 `:1209-1213` 同族路径（读档重建） |
| **广告** | `ConstructionSiteStore.cs:168-198`（`TryAdvertiseTask`） | ① 按 `TableOrder` 取首个缺口资源（`:174-179`）② `FindPickup` 解析同国＋收该型＋有存量的最近仓（`:182-183`，`WarehouseRegistry.GatherActive`）③ 产出 `KingdomTask`（`:187`） |
| **失效** | `ConstructionSiteStore.cs:163`（`IsValid`） | `_building != null && _building.IsSiteAwaitingMaterials && !IsSatisfied` |
| **注销/收口（料齐）** | `Building.cs:574-585`（`OnSiteMaterialsReady`）→ `:581 UnregisterSiteStore()` → `:556-566 UnregisterSiteStore()` 调 `TaskScheduler.Instance.Unregister(_siteStore)` | 唯一触发点 ＝ `ConstructionSiteStore.Deposit:141`（`if (IsSatisfied) _building?.OnSiteMaterialsReady();`） |
| 投料（状态变化） | `ConstructionSiteStore.cs:129-143`（`Deposit`） | 阈值拦截（`:136-138`）→ `_items` 累加 → `_building.AddInvested`（`:140`）→ 料齐 ⇒ `OnSiteMaterialsReady`（`:141`） |
| 其他注销点 | `Building.cs:535`（无料可搬）／`:895`／`:1323`／`:1387` | 拆除/死亡/重建等生命周期收口 |

**调用链一句话**：`Building.BeginMaterialPhase` →（惰性建仓 + 注册）→ `TaskScheduler.Tick` 每 tick 收集 `_sources` → `ConstructionSiteStore.TryAdvertiseTask` → 派工 → 工人两段位移（取料仓 → 工地）→ 卸货 `Deposit` → 料齐 ⇒ `OnSiteMaterialsReady` 注销。

### 2. `KingdomTask` / `TaskScheduler` 引用分支（实证核实事务端预扫）`[实读]`

证据：`Valley Rampart/Logs/hh341_small_construction_site_scan.txt`（口径＝**命中行数**）

| 判据 | 预扫 | **本批实证** | 解释 |
|---|---|---|---|
| `TaskScheduler.Instance\|HasInstance` | ≈0 | **0 行** | ✅ 一致。文件内两处 `TaskScheduler` 文本均为**注释**：`:21`（类头生命周期说明）、`:206`（`FindPickup` 频率说明）⇒ 非调用面 |
| `new KingdomTask(` | ≈1 | **1 行** | ✅ 一致。唯一构造点 `:187`：`task = new KingdomTask(KingdomTaskType.Build, this);` |

**该分支（`:187`）对应的生产分支**（任务书 §五-2 要求逐条锚点）`[实读]`：

| 调度器侧环节 | 位置（`TaskScheduler.cs`） |
|---|---|
| 装载（第一段） | `:597`（`task.type == KingdomTaskType.Build && task.args is HaulToSiteArgs`）→ `:572` `LoadInventoryFromSource`（`:950` 定义） |
| 转段 | `:603`（`:575/:589/:620` 同族）`EnterMovingToDest`（`:1261` 定义） |
| 卸货（第二段） | `:645`（`Build + HaulToSiteArgs` 分支）→ `:1064`（`LoadInventoryFromSource` 内 `HaulToSiteArgs ha`）/ `:1095`（`UnloadInventory` 内 `ha`） |
| 任务对象构造 | `ConstructionSiteStore.cs:187-196`：`destType=SpecificBuilding`、`destPos=工地位置`、`args=HaulToSiteArgs{site, resourceType, pickup, need}` |
| 位移动目标 | `SourcePos`（`:166`）＝**取料仓位置**（每次广告解析）⇒ 与 `Transport` 同形两段式 |

### 3. 该分支是否代表"工人可接取／移动／完成／放弃"的任务 `[实读]`

| 能力 | 证据 |
|---|---|
| **可接取** | 本源实现 `ITaskSource`（`:23`）；`IsValid`（`:163`）为真 ⇒ 被 `TaskScheduler.Tick` 收集；`TryAdvertiseTask` 返回真实 `KingdomTask`（`:187-197`） |
| **可移动** | `SourcePos`＝取料仓位置（`:166`，指向**另一实体**仓）＋ `destPos`＝工地（`:189`）⇒ **两段位移**，与 `Transport` 同形（类头 `:18-19` 自陈） |
| **可完成** | `:597/:645` 两段分支 ⇒ 装载 → 转段 → 卸货 `UnloadInventory` → `Deposit` ⇒ 达阈值即 `IsSatisfied` ⇒ `OnSiteMaterialsReady`（`:141`） |
| **可放弃** | `IsValid`（`:163`）转假（料齐／建筑销毁／投料态结束）⇒ 调度器既有 `!IsValid ⇒ Abandon(SourceInvalid)`；`UnregisterSiteStore`（`Building.cs:562-566`）亦清在派任务 |

### 4. ⭐ 卡片适用性判定 `[实读]`

> ## **判定：任务卡源（TaskCard 适用）**

**判定证据（4 条）**：
1. 本源**真实产出工人任务对象**（`KingdomTaskType.Build` ＋ `HaulToSiteArgs`，`ConstructionSiteStore.cs:187-196`），且该类型在调度器中**有完整两段式处理分支**（`TaskScheduler.cs:597/645/1064/1095`）——非死广告。
2. 任务**必须具备工人实体**：位移（取料仓→工地）、装载/卸货（`LoadInventoryFromSource`/`UnloadInventory`）⇒ 满足"D1 接缝"的适用前提（工人可接取/移动/完成/放弃）。
3. 源**有明确有效性生命周期**（`IsValid`：投料态 ∧ 未料齐）与**明确收口点**（`OnSiteMaterialsReady` → 注销）⇒ 与 `WorldGatherSource` 同形（"有效直至完成，重复广告由调度器去重拦下"）。
4. 该源**与 `_npcTaskMap` 无直接写权**（命中 0 行）⇒ 其工人占用完全由调度器代持 ⇒ **适合作为 `TaskBindingManager` 新权威的接入点**（与首片同型）。

⇒ 按任务书 **§5.1**：**进入施工**（届时另派子批）；本预检报告即开工回执素材。
⇒ ⛔ 未进入施工（本子批只读）；⛔ 未造卡、未改码。

---

## 二、§5.2 替代证据（**不适用**）

判定为**任务卡源** ⇒ 任务书 §5.2 的 6 项替代证据**不适用**（该项仅针对"非任务卡源"）。⛔ 未自定迁移/删除口径。

---

## 三、基线复核（本子批不动码 ⇒ 应无变化）

| 判据 | 基线（D1 绿态） | 本批复核 | 结论 |
|---|---|---|---|
| `TaskScheduler.Instance\|HasInstance` | 59 行 / 16 文件 | 未复扫（本子批零改动，`_Game` diff 为空） | 必然不变 |
| `new KingdomTask(` | 14 行 / 9 文件（剔注释） | 同上 | 必然不变 |
| `ITaskSource` | 9 类 | 同上 | 必然不变 |
| `KingdomTaskType` | 11 项 | 同上 | 必然不变 |
| 本源命中 | `TaskScheduler.Instance\|HasInstance` 0 行｜`new KingdomTask(` 1 行 | **0 / 1**（实证一致） | ✅ 与任务书 §2.2 表逐位相符 |

---

## 四、证据目录（`Valley Rampart/Logs/` · 独立前缀）

| 文件 | 内容 |
|---|---|
| `hh341_small_construction_site_scan.txt` | 本源两项命中实证 + `TaskScheduler`/`new KingdomTask`/`ITaskSource` 命中原文（含行号） |
| `hh341_small_construction_site_callchain.txt` | 创建/注册/注销调用链、料齐钩子、调度器侧 `Build`/`HaulToSiteArgs` 分支锚点 |
| `hh341_small_construction_site_git.txt` | 零改动证明：`git diff --name-only -- Valley Rampart/Assets` ⇒ 命中 1 行＝`Scenes/GameScene.unity`（**既有脏点**，非本批）；未跟踪新增均为会话前既有探针残留（HH289/290/319） |

⛔ 未使用仓库根 `Logs/`；⛔ 未覆盖 D1（`Valley Rampart/Logs/hh341_d1/`）或其他源证据。

---

## 五、零改动与边界

- **零代码改动**：本子批仅执行 `rg` 扫描与文件读取；`Assets/_Game` diff **为空**（唯一 `Assets` 命中＝既有 `GameScene.unity`）⇒ 满足任务书 §五"未判定不得写代码"。
- ⛔ 未动其余四源（`ChestEntity`／`MineByproductComponent`／`BlacksmithBuilding`／`SiegeWorkshopBuilding`）／两巨兽／`ScheduleCenterStub._crewAssignments`／`currentWorkers`／场景／Prefab／旧资产键／`AI.Core`／`最高优先级文档`／四本账本。
- ⛔ 未声称 §九-6 单管理器达成；⛔ 未改政策值；⛔ 未扩 `ITaskScheduler`。

## 【应登记项】

```
HH.341 | 小源首源 ConstructionSiteStore 只读预检 | 执行端 | 四项全取得；判定＝**任务卡源**（真实 KingdomTaskType.Build+HaulToSiteArgs，调度器侧 597/645/1064/1095 完整两段式分支）；实证命中：TaskScheduler.Instance|HasInstance=0 行（两处均为注释）、new KingdomTask(=1 行(:187)） | ⛔ 零代码改动（_Game diff 空）；⛔ 未施工，待另派子批 | 不写 D 号
HH.341 | 首源开工回执素材 | 执行端 | 顺序第 1、回退点 HEAD=7c21f1ba、前置基线＝D1 绿态（59/16·14/9·9 类·11 项） | 供施工子批直接引用 | 待登记
```
