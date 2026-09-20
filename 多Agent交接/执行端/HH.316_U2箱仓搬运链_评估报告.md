# HH.316 · U-2「箱 → 仓搬运链 ＋ 拾取入包」—— 只读评估报告

- 任务号：HH.316 · U-2（承 `D795` 立 · **阻塞级** · `M1-C` 销号唯一前置；`D797` 确认 U-1/U-3/U-4 已修并销号）
- 本轮性质：**只读评估 ＋ 报选**（⛔ `Assets/**` **一行未动**；⛔ 未开工；⛔ 未 push；⛔ 未代提交策划端账本）
- 日期：2026-09-20
- 取证手段：静态实读（Read／Grep／Glob）＋ 资产 YAML 直读 ＋ `git` 只读命令；⛔ 未跑 Unity、⛔ 未进局（运行时验证归施工批）
- 交付面：本报告 1 文件（⛔ 无代码、⛔ 无资产、⛔ 无探针文件）

---

## 〇 结论速览

| 件 | 本端结论 |
|---|---|
| **三段实读复核** | ✅ **三条全坐实**；⭐ 另**新增第 4 条同族销毁出口**（`EnforceCellLimit` 同格上限驱逐，任务书未列，见 §一 · R2） |
| **复用面复核** | ✅ 任务书清单 **8/8 逐条坐实**；⭐ **2 处补强**（兜底路径的"逐条目金/材料分流"已天然存在；`StorageComponent.Harvest` 是"点收即入账"的玩家侧现成先例形状）；⚠️ **1 处资产级附注**（`Well.asset` 声明收金，疑似误配 → R4） |
| **链 A（箱→仓）** | 现成可复用 **6 项**（含整条卸货段＋兜底分流）；须新建 **4 项**（源实现／注册注销／装载段／无主路由与第二段落点）；装载段 **A-1「箱＝真仓」** 与 **A-2「保留 contents」** 二选一（见 §三 3.4） |
| **链 B（拾取入包）** | ⛔ **阻塞歧义（唯一）**：`09` §9.8 明文「箱子不提供『捡』的能力 · 玩家手点＝调用搬运任务的一种形式」 vs 任务书链 B「在 `Interact` 内部消费 `Pickup` 返回值直接入账」⇒ **两口径不可同时成立，请先裁**（见 §四、§七） |
| **入账口共用性** | ✅ **是**——无论链 B 走哪条路线，最终都收敛到同一入账族：玩家侧 `RulerController.ModifyResource` 逐条目（金→`Gold` 字段／材料→国库仓）＋ AI 侧 `KingdomState` 台账（链 A 卸货段 `AddGatherOverflow` 已覆盖）。⛔ 不新增第二套真源 |
| **五点报裁** | ① 一箱一源（推荐）② 无主⇒先到先得·卸货按工人国 ③ **先裁走向**（§9.8 转译 vs 直接入账）④ 复用 `Transport`（⛔ 不新增枚举）⑤ 过期**不改**（到期＝消亡，`09` 明文） |
| **列报项** | R1 `ResolveWarehouse` 全扫（必列）＋ R2~R6 附加 5 项 —— 全部 ⛔ 不顺手改 |

---

## 一 · 三段实读复核（逐条）

### 1.1 ① 无工人搬运链 —— ✅ 坐实

| 证据 | 实读 |
|---|---|
| `ChestEntity` 未实现 `ITaskSource` | [ChestEntity.cs:11](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestEntity.cs#L11)：`public class ChestEntity : MonoBehaviour, IInteractable` |
| `TaskScheduler` 零 `ChestManager` 引用 | `ChestManager` 全库命中 14 文件（Building／DamageSystem／MonsterController／TreasureVault／WorldLifecycle／MapGate／4 个 Editor 冒烟探针等），**`AI/TaskScheduling/` 目录零命中** |

⇒ 无搬运链成立（`09` §16.1-4「要工人搬回」契约未实现）。

### 1.2 ② `Pickup` 返回值全库零消费 —— ✅ 坐实

| 证据 | 实读 |
|---|---|
| 返回内容物 | [ChestManager.cs:130-136](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestManager.cs#L130-L136)：`var got = chest.contents; Remove(chest); return got;` |
| 唯一生产调用点丢弃 | [ChestEntity.cs:61-62](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestEntity.cs#L61-L62)：`ChestManager.Instance.Pickup(this, ctx);` → 无接收 → `return InteractionResult.None;`（恒 None） |
| 无旁路调用 | `\.Pickup\(` 全库 grep ⇒ **仅此 1 处**；Editor 侧（`Valley_HH294_Slice6PickProbe`／`Valley_HH315_M1C_Smoke`／`Valley_HH109_Smoke`）只用 `Count`／`CountAt`／`FillChestsInCellRect`，⛔ 非拾取调用 |

⇒ 无任何「入背包」路径成立。

### 1.3 ③ 过期 `Remove` 不洒落 —— ✅ 坐实

[ChestManager.cs:42-47](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestManager.cs#L42-L47)（:46 `Remove(c)`）→ [Remove:150-155](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestManager.cs#L150-L155) 仅 `_chests.Remove` ＋ `Destroy`，**无任何洒落/转移**。

### 1.4 ⭐ 本端新增：第 4 条同族销毁出口（任务书未列）

| # | 出口 | 位置 | 语义 |
|---|---|---|---|
| ④ | **同格上限驱逐** | [EnforceCellLimit:101-117](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestManager.cs#L101-L117) → `Remove(oldest)` | 落新箱时该格已达上限（`ChestConfig.chestMaxPerCell=4` · [资产实读](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Resources/Config/ChestConfig.asset)）⇒ **驱逐最早箱**，同样**不洒落** ⇒ 内容一并销毁 |

⚠️ 与 ③ 同病，且叠加面更长：`TreasureVault.SpillToChest`（国库溢出再落主城格）会把新箱堆到同一格 ⇒ 该格触顶即驱逐旧箱（详见 R2）。

**不计入缺陷的两条"清场"**：`WorldLifecycle.cs:45 ClearAll`（跨局清场）／`ChestManager.LoadState:200 ClearAll`（读档先清后建，存档态覆盖）——语义正确，⛔ 不属 U-2。

---

## 二 · 复用面复核（任务书清单逐条）

| # | 任务书条目 | 本端实读 | 判定 |
|---|---|---|---|
| 1 | `WarehouseRegistry.FindNearestAvailable` | [WarehouseRegistry.cs:58-71](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/WarehouseRegistry.cs#L58-L71)：同国(`KingdomOf` :74-78) ＋ `Accepts` ＋ `CanAccept>0` ＋ `sqrMagnitude` 最近；无可用 ⇒ null | ✅ 坐实 |
| 2 | `TaskScheduler.UnloadInventory` | [:736-770](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L736-L770)：就近同国仓 → 满/无仓 ⇒ `AddGatherOverflow` 按国分流（**资源不丢**） | ✅ 坐实 |
| 3 | `DepositToSite` ＋ `ReturnOverflow` ＋ `LoadSiteMaterials` | [:810-829](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L810-L829) ／ [:832-838](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L832-L838) ／ [:779-803](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L779-L803) | ✅ 坐实（形状可照抄） |
| 4 | `ConstructionSiteStore` 形制 | [组件实现 ITaskSource:23](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs#L23) ＋ [TryAdvertiseTask:168-198](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs#L168-L198) ＋ 创建/注册形制 [Building.cs:546-567](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L546-L567) | ✅ 坐实 |
| 5 | `KingdomTaskType` 11 项 | [TaskPriorityConfig.cs:32-47](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/TaskPriorityConfig.cs#L32-L47)：Repair/Build/Produce/Transport/Rancher/WaterCarry/GoldMine/Production/WaterHaul/Gather/AmmoReload ＝ **11 项**；`Transport=B`，未配置兜底 B（:20-28） | ✅ 坐实 |
| 6 | `ITaskSource` 5 成员 | [KingdomTask.cs:47-63](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/Tasks/KingdomTask.cs#L47-L63)：`IsValid`／`SourcePos`／`TryAdvertiseTask`／`OnRegister`／`OnUnregister` | ✅ 坐实 |
| 7 | `Interactor` 无资源口 ／ `InteractionResult` 无资源口 | [IInteractable.cs:15-34](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Interaction/IInteractable.cs#L15-L34)（4 kind）／ [:37-47](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Interaction/IInteractable.cs#L37-L47)（faction ＋ position） | ✅ 坐实 |
| 8 | 候选入账口 `RulerController.Refund` ／ `KingdomState.Refund` | [RulerController.cs:298-306](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Ruler/RulerController.cs#L298-L306)（逐条目 `ModifyResource`）；[KingdomState.cs:173-176](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomState.cs#L173-L176)；生产码唯一调用方 [SiegeProductionSystem.cs:177](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/SiegeProductionSystem.cs#L177)／[:226](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/SiegeProductionSystem.cs#L226)（**均缺省 ratio**） | ✅ 坐实 |

### 2.1 ⭐ 补强 1：链 A 卸货段的"金/材料分流"**已天然存在**（任务书未点明）

`UnloadInventory` 的**兜底路径**（无可用仓／仓满溢出）→ `AddGatherOverflow`（[:663-695](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L663-L695)）：

- 玩家（`uc.kingdomId<=0`）→ `RulerController.ModifyResource` **逐条目** → [金→`Gold` 字段直通 :233-241](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Ruler/RulerController.cs#L233-L241) ／ 非金→`TreasureVault.Deposit` [:244-254](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Ruler/RulerController.cs#L244-L254)；
- AI（`>0`）→ `KingdomState` 台账桶（[:672-694](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L672-L694)），国已注销 ⇒ 丢弃＋日志（防资敌）。

⭐ ⇒ **箱内容物（任意混合 ResourceList）经此路径入账时，逐条目分流已经正确**——这是链 A 对"混合内容物"接近零新建的关键（前提：卸货段不整包塞给某一个仓）。

### 2.2 ⭐ 补强 2：玩家侧"点收即入账"的现成先例形状

`StorageComponent.Harvest`（[:263-276](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L263-L276)，`IHarvestable`「玩家手动收取转入国库」）＝ **逐条目 `ModifyResource` → 清仓**。⇒ 若链 B 裁"直接入账"，其代码形状有现成先例可照；但注意它已被标注为 `M1-G` 待改两段式（`09#40`，注释 :257／:284-287 在案）——**"点收直入账"是正在退役中的形状**。

### 2.3 ⚠️ 附注：仓库标签命中强依赖资产声明（抽查）

| 资产 | 声明路径 | 后果 |
|---|---|---|
| `Warehouse.asset:34-35` | `res_material.wood` | 只收木（不收石/其它） |
| `AdvancedStorage.asset:34-35` | `res_material.wood` | 同上 |
| `Well.asset:32-33` | `res_currency.gold` | ⚠️ **水井声明收金** ⇒ 该井 `StorageComponent` 会进入"就近可收金"命中集 —— **疑似 M1-A 标签迁移误配，列报待裁（R4）** |
| `TreasureVault`（`VaultPaths`） | `res_material` ＋ `res_food` | ⛔ 不收金（`res_currency`）／不收弹药（`res_ammo`）· [TreasureVault.cs:19-25](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L19-L25) |

⇒ 链 A 的"就近仓"判定是**标签驱动**的：命不命中取决于资产声明；命不中就走 §2.1 兜底（分流正确）。

---

## 三 · 链 A：箱 → 工人 → 仓 落点清单

### 3.1 现成（零新建）

| # | 部件 | 位置 | 链 A 复用方式 |
|---|---|---|---|
| A1 | 任务类型 `Transport`（B 档） | 枚举 :41／优先级映射 SO | 直接用作箱搬运类型（见报裁 4） |
| A2 | 两段式状态机 | `UpdateAssignedTasks` [:368-533](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L368-L533)（Assigned→MovingToSource→Working→MovingToDest） | 零改：箱＝MovingToSource 目标点 |
| A3 | 装载段主路径 | `LoadInventoryFromSource` [:709-734](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L709-L734) | **仅当走 A-1「箱＝真仓」时零改**（见 3.4） |
| A4 | 卸货段（含分流兜底） | `UnloadInventory` [:736-770](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L736-L770) | **零改直接复用**（含 §2.1 的金/材料分流） |
| A5 | 仓库定位 | `WarehouseRegistry.FindNearestAvailable`／`GatherActive` | 零改 |
| A6 | 无主源"先到先得池"机制 | 派工循环 [:289-291](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L289-L291)（`tKingdom >= 0` 才做同国过滤） | 机制在场；**但须箱源被路由为 `-1` 才生效**（N4） |

### 3.2 须新建（4 项）

| # | 部件 | 落点 | 形制参照 |
|---|---|---|---|
| N1 | **源实现**：`ChestEntity` 实现 `ITaskSource`（或等价物） | `IsValid`＝`!contents.IsZero && 未销毁`；`SourcePos`＝箱世界位；`TryAdvertiseTask`＝`Transport` ＋ 表序取首个非空资源（确定性）；`OnRegister/OnUnregister` 留空 | `ConstructionSiteStore`／`Building` |
| N2 | **注册/注销挂钩**（4 处）：`SpawnChest`(:58-73)／`Remove`(:150-155)／`ClearAll`(:158-164)／`LoadState` 内联重建(:202-216) | 每处 `Register`/`Unregister` | [Building 的 RegisterSiteStore/UnregisterSiteStore:556-567](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L556-L567) |
| N3 | **装载段**（二选一，见 3.4） | A-1：箱挂 `StorageComponent` 容器（照 `TreasureVault` 子容器先例，**⛔ 不调 `Init`／⛔ 不 `Register`**，声明通用 `res`）→ 零改调度器；A-2：照 `LoadAmmoToBackpack`(:846-872) 新增装载分支 ＋ 箱侧取走 API | `TreasureVault`／`AmmoReload` |
| N4 | **无主源路由 ＋ 第二段落点** | `SourceKingdom`(:1186-1197) 现对非 Building 组件源 `return 0`（＝玩家池）⇒ 按 §9.8 须为箱源返回 `-1`；且 `NearestWarehouse` 在广告时按源国解析（`ResolveWarehouse` 对 `-1` 全跳过 → 回退国库锚点 [:982-987](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L982-L987)）⇒ 需**装载成功后按搬运者国＋carriedType 即时解析 `destPos`**（`InjectCarryStimulus` 前），或退而用 `destType=None` | ⚠️ 与"建筑 Transport 广告时解析"的**形制差异点** |

⚠️ **N2 附注（生命周期缝）**：`Tick` ①（[:217-224](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L217-L224)）清理无效源时**不回调 `OnUnregister`** ⇒ 箱源的关键清算**不要放 `OnUnregister`**（照 `Building`／`ConstructionSiteStore` 先例留空；注销走显式 `Remove`／`ClearAll` 钩子）。

### 3.3 与 `ConstructionSiteStore` 形制的差异

| 维度 | `ConstructionSiteStore`（现成先例） | 箱源（拟） |
|---|---|---|
| 第一段位移点 | `SourcePos`＝**取料仓位置**（去仓取料）→ 第二段＝工地 | `SourcePos`＝**箱位置**（去箱取货）→ 第二段＝仓/国库 |
| 货物载体 | 取料仓 `StorageComponent`（工人从仓取） | 箱 `contents`（或"箱＝真仓"容器 · 见 3.4） |
| 阈值 | `Remaining()` 缺口阈值拦截（够阈值即停） | ⛔ 无阈值（有啥搬啥；`IsEmpty` 即失效） |
| 多资源 | 表序取**首个缺口**，逐轮广告 | 表序取**首个非空**，逐轮广告（同形） |
| 归属国 | `_building.kingdomId`（确定） | **无主** → 需 `-1` 先到先得（N4）；卸载按工人国 |
| 创建/注册 | Building 惰性建 ＋ 投料态注册／料齐注销 | ChestManager 落箱注册 ＋ `Remove`/清场注销 |
| 目的地解析 | `destType=SpecificBuilding`（工地本体，广告时定） | `NearestWarehouse`（须装载后定 · N4） |
| 失效条件 | `!IsSiteAwaitingMaterials \|\| IsSatisfied` | `IsEmpty \|\| 箱销毁` |
| 容量 | 配方量＝容量（阈值保证恰好） | 容量＝实际掉落量（§九） |

### 3.4 ⭐ 装载段两走法（A-1 / A-2）对比

| 维度 | **A-1 箱＝真仓**（给箱挂 `StorageComponent` 容器） | **A-2 保留 `contents`**（新增装载分支） |
|---|---|---|
| 装载段 | `LoadInventoryFromSource` **零改**（要求容器 `Accepts` 广告类型 ⇒ 声明通用 `res`；容量＝Σ(量×体积)，金体积 0 不受限） | 调度器新增分支 ＋ 箱侧“取走”API（`ResourceList.Set` 减量） |
| 单源红线 | ⚠️ 需把 `contents` 字段**迁移进容器**（禁双写）：`SpawnChest`／`Pickup`／`ResetDrop`／`SaveState`／`LoadState`／`IsEmpty` 连改；`ChestSaveEntry.contents` 存档形状可保留（装载进容器） | `contents` 保持唯一写点，⛔ 不触存档形状 |
| `09` 对齐度 | ✅ §9.8「它**就是个仓**」＋ §九「对象生命周期结束 ⇒ **它的仓**变成掉落箱」——目标模型 | ⚠️ 保留"箱≠仓"偏差（§九 判据只做了半截） |
| 已知风险 | 容器**⛔ 勿 `Register`**（防成为他人卸货落点；`KingdomOf` 无父建筑＝-1 本就挡，仍建议照工地仓"结构性隔离"精神不注册）；容量须＝实际掉落量（§九，防容量泄露） | 分支与 Transport 主路径分叉，M1-F/M1-G 收口时需再并 |
| 改动面 | **中**（ChestManager ＋ ChestEntity ＋ 冒烟） | **小**（TaskScheduler ＋ ChestEntity） |

**本端倾向**：A-1（与 `09` 目标模型同向、零改装载段）；若以"最小闭合 U-2"为优先，A-2 亦可接受。**请策划裁**（属方案面，非阻塞）。

---

## 四 · 链 B：玩家点箱 ⇒ 内容入账 落点清单

### 4.1 现成链（调用路径）

`InteractionManager.HandleClick` [:82-113](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Interaction/InteractionManager.cs#L82-L113)（箱经 `MapGate.PickAt` 候选③可达）→ [:97-98](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Interaction/InteractionManager.cs#L97-L98) `Interact(new Interactor(Faction.PlayerCamp, worldPos))` → `ChestEntity.Interact` [:58-63](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestEntity.cs#L58-L63)。

⭐ 注：`ctx.faction` **恒 `PlayerCamp`**（AI 无点击入口）⇒ "AI 拾取入账"在本链**不存在**（AI 侧入账只可能走链 A）。

### 4.2 须新建（若走"直接入账"）

| # | 部件 | 落点 |
|---|---|---|
| B1 | 消费 `Pickup` 返回值 | 唯一丢点 [ChestEntity.cs:61](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestEntity.cs#L61) ⇒ 改为接收 `pack` 并**逐条目**入账 |
| B2 | 入账调用 | `RulerController.ModifyResource(逐条目)`（或等价 `Refund(pack)`，ratio 缺省 1.0） |

### 4.3 ⛔ 关键发现（阻塞歧义）：`09` §9.8 与任务书链 B **取向冲突**

[`09_资源与仓库.md` §9.8（2026-09-18 · 用户定）](file:///c:/Users/trs/Desktop/Valley%20Rampart/最高优先级文档/09_资源与仓库.md#L383-L392) 明文：

> ⭐ **掉落箱自身不提供"捡"的能力** —— 它**就是个仓** —— ⛔ 不在箱子上加"拾取"接口
> ⭐ **怎么捡 ＝ 上层的事**：**玩家手点 ＝ 调用搬运任务的一种形式**；**AI 走同一套形式** ⇒ 落点归 `05` 交互／`06` 任务
> ⭐ **谁能搬**：**任何王国的工人都能搬**（无主 ⇒ **先到先得**）—— ⚠️ 玩法后果：**AI 会来抢你的战利品**

而现码 `D146`／`D246`（`ChestEntity` 拾取 ＋ `ChestManager.Pickup` 返回内容物「供调用方入背包」）是**旧口径**；且"玩家背包"**当前无载体**（全库无玩家背包系统；`WorkerInventory` 是**工人内置仓** · [类注释 :5-8](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/WorkerInventory.cs#L5-L8)）。

⇒ 两条路线**不可同时成立**，请策划**先裁**（§七）：

| 路线 | 内容 | 与 `09` §9.8 | 与任务书链 B |
|---|---|---|---|
| **B-§9.8 转译（本端推荐）** | 点箱 ⇒ **立案一个搬运任务**（或给箱源高优插队），工人来搬回 ⇒ **链 B 并回链 A**，无独立入账口 | ✅ 合规 | ⚠️ 与「消费 `Pickup` 直接入账」相反 |
| **B-直接入账** | 点箱 ⇒ `Pickup` ⇒ **逐条目** `ModifyResource`（金→`Gold` 字段／材料→国库仓） | ⚠️ 与「不在箱子上加拾取接口」相抵（但形制上可做） | ✅ 合规 |

### 4.4 入账口共用性（回答任务书"是否共用入账口"）

| 路线 | 玩家侧入账口 | AI 侧入账口 | 是否共用 |
|---|---|---|---|
| B-§9.8 转译 | 链 A 卸货段（`UnloadInventory` → `AddGatherOverflow` → `ModifyResource`） | 链 A 卸货段（`AddGatherOverflow` → `KingdomState` 台账） | ✅ 完全共用（链 B 无独立入口） |
| B-直接入账 | `RulerController.ModifyResource`（逐条目）＝ 与链 A 兜底**同一入口** | —（AI 无点击入口） | ✅ 共用同一入口；`KingdomState.Refund` 在"点箱"链**无触点**（仅未来 AI 自主搬运时经链 A 走台账） |

⇒ ⛔ **不新增第二套真源**：无论哪条路线，最终都收敛到 `RulerController.ModifyResource`（玩家）／`KingdomState`（AI）。

---

## 五 · 五点报裁

### 5.1 载体：一箱一源 vs `ChestManager` 统一广告 → **推荐「一箱一源」**（ChestEntity : `ITaskSource`）

**「统一广告」不成立的三条结构性理由**（均实读）：

1. `ITaskSource` 契约＝**每源每 tick 一任务**（`TryAdvertiseTask` 单出口 · [:254](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L254)）⇒ 单源只能广告**一个箱**/tick，多箱需自建轮转状态；
2. 调度器全程以 **`source` 身份为键**：`IsValid` 校验（[:388](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L388)）／`RemoveTaskStimulus(source)`／刺激 issuer（[:337-344](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L337-L344)）⇒ `ChestManager` 单例的 `SourcePos` **单点**无法表达多箱位置（工人寻路/续命刺激全取 `task.SourcePos`）；
3. Transport 去重＝按源容量（`RemainingSlots` :1039-1044 ⇒ `CountAssignedForType(task.source)`）⇒ 若 `task.source` 仍是箱（未注册源），等于**绕开注册制**、与 `Tick` ①的失效源清理脱节（悬挂引用风险）。

**⭐ U-4 双箱评估（任务书点名）**：投料中态拆除 ⇒ `FinishDemolish` 的 refund 箱 ＋ `DropSiteStoreToChest` 的工地仓箱（[Building.cs:870-876](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L870-L876)，**同 coord 两箱**）——在"一箱一源"下＝**两个独立源、两个独立任务**：`contents` 各自独立、`source` 身份独立、失效各自判定 ⇒ **无合并/覆盖/双计**；这正是期望行为（两堆料都要搬回，分别成箱本就是 U-4 修复的目标）。反而是"统一广告"才需要额外处理同 coord 双箱的轮转与配额。⇒ **一箱一源无碍，推荐**。

⚠️ 附注（不否定本方案）：同 coord 两箱叠加同格上限（4）后，若再有新箱落同格 ⇒ 驱逐最早箱（R2）。

### 5.2 无主箱（`Faction.None`）归属规则 → **按 `09` §9.8：无主 ⇒ 先到先得；卸货按搬运者国**

- **派工侧**：§9.8「任何王国的工人都能搬（无主 ⇒ 先到先得）」⇒ 需箱源被路由为 **`-1`**（派工循环 [:289-291](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L289-L291) 对 `-1` 已支持"任何国工人都可匹配"）。
- **卸货侧**：`UnloadInventory` 现码即按 **`uc.kingdomId`**（搬运者国）分流（[:746-747](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L746-L747)）⇒ **"谁搬回，归谁国"** 天然自洽（玩法后果＝AI 来抢，§9.8 明文）。
- ⚠️ **落地缺口**：`SourceKingdom`（[:1186-1197](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L1186-L1197)）现对非 `Building` 组件源 **`return 0`（玩家池）** ⇒ 若不加分支，箱源**只派玩家工人**（AI 永不抢），与 §9.8 不符。
- `ownerFaction` 字段（`SpawnChest` 记录来源阵营）**不参与路由**（仅存档/`ResetDrop` 沿用）⇒ ⛔ 建议**维持现状不动**；其去留归 §9.8 挂的 `09#57`（[:488](file:///c:/Users/trs/Desktop/Valley%20Rampart/最高优先级文档/09_资源与仓库.md#L488)），⛔ 不属 U-2。

**两个选项**：(a) 箱源 `-1` 先到先得（＝§9.8 合规，本端推荐）；(b) 暂留 `0` 玩家池（零改 `SourceKingdom`，但 AI 永不抢、与 §9.8 相抵）。**请裁 (a)/(b)**。

### 5.3 玩家拾取入账落点（国库仓 vs 材料仓 vs 玩家背包）→ **先裁走向（§4.3），再定落点**

设问逐条作答：

| 问 | 答（实读） |
|---|---|
| "入玩家背包"可行吗？ | ⛔ **无载体**：全库无玩家背包系统；`Interactor` 无资源口；`WorkerInventory` 是工人内置仓。选它＝新建系统（超 U-2 范围） |
| "整包入国库仓"可行吗？ | ⛔ **违标签**：`TreasureVault` 只声明 `res_material`＋`res_food`（[:19-25](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L19-L25)）⇒ 金（`res_currency.gold`）与弹药（`res_ammo`）会被 `Accepts` **拒收**；且 `Vault.Deposit` 拒收部分会走 `SpillToChest` **落回主城格重新装箱**（[:105-124](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L105-L124)）⇒ "箱→国库→溢出→落箱→再点"**存在环路面** |
| 正确落点？ | ✅ **逐条目分流**（＝`RulerController.ModifyResource` 逐条目 `Refund` 形制）：**金→`Gold` 字段**（货币直通 · `09` §4.3 B 组「国库金仓」为 M1-E 目标）／**材料·粮→国库仓**／**弹药→无国库位**（Vault 不收 ⇒ 需另有落点或依赖既有弹药仓；⚠️ 若走链 A 卸货段则由 `FindNearestAvailable` 兜住，若走直接入账需单列处理） |
| 选哪个落点？ | **取决于 §4.3 的路线裁**：走 B-§9.8 ⇒ 落点问题消解（并回链 A）；走 B-直接入账 ⇒ 采「`ModifyResource` 逐条目」（⛔ 非"整包入 Vault"） |

**本端推荐 B-§9.8 转译**：① `09` §9.8 是最新用户口径明文；② 复用链 A 全链（零新增入账路径）；③ AI 同形（同一套形式）；④ 避免"点击即到手"与"工人搬运"两套语义并存。若策划要"点一下立刻到手"的体感 ⇒ 退 B-直接入账（逐条目）。

### 5.4 任务类型：复用 `Transport` vs 新增枚举 → **复用 `Transport`（⛔ 不新增枚举）**

- 先例＝`M1-C` 件1 裁决收口 1 明文：「⛔ **不新增 `KingdomTaskType` 枚举**——靠 `args` 类型区分语义」（`HaulToSiteArgs`／`DemolishTaskArgs` 同法）。
- `Transport`（B 档）语义已覆盖"搬运"；箱搬运是"搬运"的子形态 ⇒ 靠 `args` 或源类型即可区分装载分支。
- 新增枚举代价：`KingdomTaskType` 是 `TaskPriorityConfig` SO 的序列化键（`TaskPriorityEntry.taskType`，[TaskPriorityConfig.cs:50-54](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/TaskPriorityConfig.cs#L50-L54)）＋ SO 资产条目 ⇒ 虽然末尾追加稳定（工程先例），但**无必要**。
- ⭐ 若采 **A-1「箱＝真仓」**：连 `args` 都可沿用既有 `ScaleTaskArgs`（`resourceType`＝表序首个非空／`totalResourceDemand`＝存量，照 [Building.TryAdvertiseTask:1402-1416](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1402-L1416)）⇒ **零新增类型、零新增 args**。
- 附注（小裁点）：Transport 的规模派工＝`ceil(total/carry)`（[:1028-1036](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L1028-L1036)）——用 `ScaleTaskArgs` ⇒ **同一箱可并发多工人**（各取一趟）；若要"单工依次搬完"则 `args` 不给规模语义（默认 1）。二者皆可，报裁一并定。

### 5.5 过期箱是否改「过期即洒落到箱」→ **⛔ 建议不改**（到期＝消亡，是 `09` 明文设计）

依据（三条，均文档实读）：

1. §九「生命结束三触发：**① 倒计时结束** ② 仓为空 ③（未来）性能清理」（[:351](file:///c:/Users/trs/Desktop/Valley%20Rampart/最高优先级文档/09_资源与仓库.md#L351)）⇒ 到期消失＝**设计内**；
2. §七 语义四条：「⛔ 『删』≠『消亡』—— **消亡是连同内容消失**（**掉落箱到期**、粮被吃掉）⇒ 消亡是独立的动作，**必须单列并记账**」（[:287](file:///c:/Users/trs/Desktop/Valley%20Rampart/最高优先级文档/09_资源与仓库.md#L287)）⇒ 到期销毁**合法**；
3. §九 护栏：「**箱子自己不能再掉箱子（递归）**」（[:357](file:///c:/Users/trs/Desktop/Valley%20Rampart/最高优先级文档/09_资源与仓库.md#L357)）＋ 三触发①⇒ "过期即再落箱"会 `bornDay` 重置 ⇒ **无限续命**，直接违护栏。

⇒ "内容仍丢"的**真根因是回收通道缺失（①无搬运链／②拾取丢弃）**，修链 A（＋链 B 或并回链 A）后，到期消亡回到"惩罚玩家＋回收实体"的设计内。**请裁"维持不改"**（本端推荐）；若裁改，则需一并改 §九 护栏与消亡口径（文档级裁决，非执行端可自定）。

⚠️ 两条附注：① §七「消亡必须单列并记账」**当前未落地**（列报 R3）；② 到期初值：§九 定「初值＝**1 个游戏日**」，现资产 `expireDays=3`（列报 R6）。

---

## 六 · 列报项（⛔ 不得顺手改 · 本端仅列报）

| # | 项 | 证据 | 定性 |
|---|---|---|---|
| **R1** | ★ 任务书必列：`TaskScheduler.ResolveWarehouse` 仍 `FindObjectsOfType<StorageComponent>()` 全场景扫描 | [:969-988](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L969-L988)（:971 全扫）vs `UnloadInventory` 的注册表路径（:756）；注释 :967 称"对齐既有语义"——实为**过滤口径**（同国＋同型）对齐，**定位方式未齐** | ⛔ 不属 U-2 范围，仅列报（本端另记观察项） |
| R2 | 第 4 条同族销毁出口：`EnforceCellLimit` 上限驱逐不洒落 | [:101-117](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestManager.cs#L101-L117)；叠加 `TreasureVault.SpillToChest` 同格堆积面 | 同族缺陷（§一 1.4），⛔ 本批不动 |
| R3 | §七「消亡必须单列并记账」未落地：过期／上限驱逐／清场均无账本登记（含 AI 侧） | §七 :287 义务 vs 全库无登记调用 | 记账缺口，⛔ 本批不动 |
| R4 | 资产标签疑似误配：`Well.asset` 声明 `res_currency.gold`（水井收金）⇒ 井的 `StorageComponent` 进入"就近可收金"命中集 | [Well.asset:32-33](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Resources/Buildings/Well.asset#L32-L33) ＋ 工厂挂载条件（[BuildingFactory.cs:214-220](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L214-L220)） | 疑似 M1-A 迁移误配；影响"金"的就近落点 ⇒ 列报待裁 |
| R5 | `Tick` ① 清无效源**不回调 `OnUnregister`**（与 AGENTS 铁律二"有注册必有注销"有缝） | [:217-224](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L217-L224) | 设计约束（箱源清算勿放 `OnUnregister`），列报 |
| R6 | 到期初值偏差：`ChestConfig.expireDays=3`（资产）vs §九「初值＝1 个游戏日」 | [ChestConfig.asset](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Resources/Config/ChestConfig.asset) vs §九 :354 | 数值批口径，列报 |

**R1 同族两处（一并列报，同不属 U-2）**：`LoadAmmoToBackpack` :855、`DepositAmmoBack :903` 亦 `FindObjectsOfType`（全扫同病）。

---

## 七 · 阻塞歧义与停手点

| # | 歧义 | 影响 | 处置 |
|---|---|---|---|
| 1 | **链 B 走向**：`09` §9.8「玩家手点＝调用搬运任务的一种形式」 vs 任务书链 B「`Interact` 内消费 `Pickup` 直接入账」——两口径不可同时成立（§4.3） | 决定链 B 是否实施 B1/B2、是否共用链 A | ⛔ **先停手报裁**（本端不擅自下钻；本轮本就是只读） |
| 2 | 装载段 A-1/A-2（§3.4） | 决定改动面与 `09` 对齐度 | 方案面，随本轮报裁一并定（不阻塞评估） |
| 3 | 无主源路由 `-1` / `0`（§5.2） | 决定 AI 是否抢战利品（§9.8 明文"会抢"） | 报裁 |

其余（5.4 枚举复用、5.5 过期不改）**不构成阻塞**，本端已给建议与依据。

**若获裁后的建议实施顺序**（本轮**不开工**，仅列序）：①裁 §七 三歧义 → ②链 A（N1/N2/N3 ＋ N4）＋ 编译门禁 0 警告 0 错误 → ③进局冒烟须走 **`TestHarnessApi.EnterTestRun` 正门**（⛔ 禁裸跑），靶例＝「投料中态拆除 ⇒ 同 coord 两箱 ⇒ 工人搬回 ⇒ 逐条目到账（金→`Gold`／材料→国库仓）」→ ④链 B 视裁。

---

## 八 · 红线与在场性自查

| 红线 | 自查 |
|---|---|
| ⛔ `Assets/**` 一行不动 | ✅ 本轮**零写操作**（仅 Read／Grep／Glob／git 只读命令）；工作区既存的 `M` 项（`GameScene.unity`／`manifest.json` 等）**非本端所出**（本端未对其做任何写/提交动作） |
| ⛔ 不开工 | ✅ 未写代码、未建探针、未进局、未跑批 |
| ⛔ 不 push | ✅ 无任何 git 写命令 |
| ⛔ 不代提交策划端账本 | ✅ 未触碰 `_编号登记.md`／`_任务队列.md`／任何策划端文档 |
| 交付面 | 本报告 1 文件（`多Agent交接/执行端/HH.316_U2箱仓搬运链_评估报告.md`） |

**未覆盖项（诚实标注）**：① 未跑运行时验证（链 A/B 的实际行为读数归施工批 ＋ 冒烟）；② `Well` 标签（R4）仅静态证据，未验"井收金"是否已实际发生（进局可查）；③ 未核 HL/训练仓侧是否存在"箱＝仓"的同源约束（U-2 不触 `AI.Core` 决策核，未展开）。

---

*本端：执行端（TraeCode）· 2026-09-20 · 只读评估完毕，待策划端五点回裁*