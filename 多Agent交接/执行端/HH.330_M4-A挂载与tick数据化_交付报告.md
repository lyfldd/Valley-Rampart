# `HH.330` `M4-A` 挂载与 tick 数据化 交付报告

- **编号**：**`HH.330`**（对 `_编号登记.md` 在途行另取 · 先登记后落盘 · 水位线 329→330）
- **依据**：`多Agent交接/策划端/HH.329_M4-A挂载与tick数据化_任务书.md`（`D863`）｜`HH.328` 实核｜`08` §3.3／§7.3
- **性质**：施工批（范围＝**只做 `M4-A`**；`M4-B`／`C`／`D`／`E` 未做；`M4-F` 已闭合未再动）
- **基线**：`HEAD = 0784327a`
- **日期**：2026-09-24

---

## 〇、一句话

两件事都落地：**挂载改读数据行**（`BuildingDef.components` ＋ `BuildingComponentRegistry` 9 键键表，原 **9 处 `if` 删除**）、**tick 改遍历 `ITickable`**（4 类实现接口，`ProductionSystem` 生产码**旧类名 0 个**）。⭐ **等价读数**：静态面 **40/40 def 集合＋挂载顺序逐栋相同**（未知键 0）、运行面（真实 `AddComponent`）**40/40 实挂集合相同**、`sourceType=None` 路径 **8/8 相同**。⭐ 判据 2：测试组件写进数据行后 **被 tick 到（0→1→2）**（⛔ 未改 `ProductionSystem`）；判据 3：`_tickInterval = 1`、Well 逐 Tick **+4 水**；判据 4：`TaskScheduler`／`TryAdvertiseTask` **diff = 0**。**停手条件 0 命中。**

---

## 一、改动清单（`file:line`）

### 1.1 生产码（8 文件 · ＋126／−58）

| 文件 | 行 | 改动 |
|---|---|---|
| `_Game/Data/BuildingDef.cs` | **`:69`** | 新增数据行字段 `public string[] components;`（§7.3 · 带 Header/Tooltip；空数组＝不挂行为组件） |
| `_Game/Systems/Building/BuildingComponents.cs` | **`:21-26`** | 新增 `public interface ITickable : IBuildingComponent { void Tick(); }`（`M4-A` 件 2 的接口） |
| 同上 | **`:189-258`**（文件尾） | 新增 `public static class BuildingComponentRegistry`：9 个键常量（`:195-203`）、静态构造登记 9 条绑定（`:209-223`）、`Register`（扩展口 `:227`）、`TryAttach`（`:234`）、`Count`／`Has`（自检读口）、`Add<T>`（`:249` · **同类型已挂即跳过**＝结构保「不挂两个 `StorageComponent`」） |
| `_Game/Systems/Building/BuildingFactory.cs` | **`:237-255`** | `AttachComponents` **重写**：原 9 处 `if`＋4 个 `AddComponent` 分支 **删除**（−43 行）⇒ 改为「遍历 `def.components`（`:246`）＋ 查键表 `TryAttach`（`:252`）」；未登记键 ⇒ `LogWarning`（死数据可见） |
| `_Game/Systems/Building/ProductionSystem.cs` | **`:3-4`** | 头注同步（原写"遍历所有 `ProducerComponent`" ⇒ 改「遍历 `ITickable`」） |
| 同上 | **`:30`** | 新增遍历缓冲 `private readonly List<ITickable> _tickBuf = new List<ITickable>(8);`（稳态零分配） |
| 同上 | **`:32-52`** | `TickAll` **重写**：删 4 组 `GetComponent<具体类>`＋`Tick()`（−12 行）⇒ `_tickBuf.Clear()`（`:43`）＋ `b.GetComponents<ITickable>(_tickBuf)`（`:44`）＋ 逐个 `t.Tick()`（`:45-51`，含接口假 null 兜底） |
| `_Game/Systems/Building/ProducerComponent.cs` | **`:8`** | `: MonoBehaviour, IBuildingComponent` ⇒ `: MonoBehaviour, ITickable` |
| `_Game/Systems/Kingdom/BlacksmithBuilding.cs` | **`:12`** | 同上（保留 `ITaskSource`） |
| `_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs` | **`:19`** | 同上（保留 `ITaskSource`） |
| `_Game/Systems/Building/MineByproductComponent.cs` | **`:30`** | 同上（保留 `ITaskSource`） |

⛔ **未改**：`TaskScheduler.cs`／4 处 `TryAdvertiseTask`（`Building.cs`／`BlacksmithBuilding`／`SiegeWorkshopBuilding`／`MineByproductComponent`）／`WorkAt`／`IsFortification`／`05`／`08`／`BuildingAbilityCatalog`（未建）。

### 1.2 数据行（20 栋 · ＋46 行 · 只加 `components:` 与键行，⛔ 未动其他字段）

| 资产 | `components`（顺序＝改前 if 链的挂载顺序） |
|---|---|
| `AdvancedStorage` | `comp.storage`, `comp.producer` |
| `Blacksmith` | `comp.storage`, `comp.blacksmith` |
| `farm` / `quarry` / `Well` | `comp.storage`, `comp.producer` |
| `Granary` / `Warehouse` | `comp.storage` |
| `SiegeWorkshop` | `comp.siege_workshop` |
| `mine` | `comp.mine_byproduct` |
| `arrow_tower`／`catapult`／`CrossbowTower`／`magic_tower`／`castle` | `comp.combat`（castle 另有 `comp.castle_core`） |
| `ore_vein`／`stone_pile`／`wood_pile`／`treasure_box` | `comp.pickup` |
| `rift`／`portal` | `comp.rift` |

⚠️ 其余 20 栋（`Church`／`House`／`market`／`Barracks`／`bridge`／`gate`／`wall`／`tree`／`farmland`／`ruins`／`VagrantCamp`…）改前**本来就不挂任何组件** ⇒ 数据行留空（＝不挂），⛔ 未新增键。

### 1.3 探针（1 个新增 · 非生产）

- `Assets/Editor/Smoke/Valley_HH329_M4AProbe.cs`（Editor-only · 菜单 `Valley/验证/HH329 M4-A 数据化等价 + tick`）——
  **Part 1 静态面**（纯逻辑对照 · 无副作用）／**Part 2 运行面**（真实挂载 ＋ `_tickInterval` ＋ 逐 Tick 产水）。
- ⚠️ 判据 2 的**测试组件**（`HH329TestTickComponent` ＋ 其一次性验证件 `Valley_HH329_TickTestProbe.cs`）**已按任务书删除**（含 `.meta`）；读数留档 `Logs/hh329_tick2_probe.log`。

---

## 二、判据 1 · 逐栋对照（改前集合 ＝ 改后集合）

### 2.1 静态面（**40/40 相同 · 含顺序 · 未知键 0**）

改前侧＝探针内**逐字复刻**的旧 9 处 `if` 推导；改后侧＝`def.components` 数据行 ＋ 键表推导。
（读数全文：`Logs/hh329_m4a_probe.log` Part 1，40 行逐栋；末行 `—— 合计 40 栋：相同 40 ／ 不同 0 ✅；顺序亦逐栋相同 ✅；未知键 0`）

**任务书 §三-1 要求的 8 类逐类抽证**（均"同 ✅"）：

| # | 要求类别 | 代表 def | 改前＝改后 |
|---|---|---|---|
| 1 | 经济仓 | `Warehouse`＝`[StorageComponent]`；`Granary` 同；`AdvancedStorage`＝`[StorageComponent, ProducerComponent]` | ✅ |
| 2 | 投掷机厂 | `SiegeWorkshop`＝`[SiegeWorkshopBuilding]`（⭐ **不含通用 `StorageComponent`**） | ✅ |
| 3 | 铁匠 | `Blacksmith`＝`[StorageComponent, BlacksmithBuilding]`（⭐ **不含 `ProducerComponent`**） | ✅ |
| 4 | 普通产能 | `farm`／`quarry`／`Well`＝`[StorageComponent, ProducerComponent]` | ✅ |
| 5 | 矿洞副产 | `mine`＝`[MineByproductComponent]`（⭐ 不含 `StorageComponent`） | ✅ |
| 6 | 有攻击 | `arrow_tower`／`catapult`／`CrossbowTower`／`magic_tower`／`castle`＝`[CombatComponent]`（castle 另含主城核） | ✅ |
| 7 | 可消耗物 | `ore_vein`／`stone_pile`／`wood_pile`／`treasure_box`＝`[PickupComponent]` | ✅ |
| 8 | 主城核 | `castle`＝`[CombatComponent, CastleCoreComponent]`（运行面两者均含 `TreasureVault` —— 由 `CastleCoreComponent.Init` 挂，两侧一致） | ✅ |

**「同一栋不挂两个 `StorageComponent`」**：改前两段条件互斥（`rate<=0` vs `rate>0`）＋ 改后 `Add<T>` 同类型已挂即跳过（`BuildingComponents.cs:249-255`）⇒ **结构双保**；40 栋实挂读数中**无任何栋出现两个 `StorageComponent`** ✅。

### 2.2 运行面（真实 `BuildingFactory.AttachComponents` 挂载 · **40/40 相同**）

（读数全文 `Logs/hh329_m4a_probe.log` Part 2；末行 `—— 合计 40 栋：相同 40 ／ 不同 0 ✅；顺序亦逐栋相同 ✅`）
⚠️ 运行面**必须 Play 模式**（Edit 模式下 `Singleton.Instance` 隐式创建会抛 `DontDestroyOnLoad`）⇒ 本批走 `manage_editor play` → 等 `advice.ready_for_tools=true` → 触发探针 → `stop`。

### 2.3 `sourceType = BuildingType.None` 路径（玩家建造 `Building.Init` / 调试入口 · **8/8 相同**）

| def | 改前 | 改后 |
|---|---|---|
| `castle` | `[CombatComponent]` | `[CombatComponent]` ✅ |
| `rift`／`portal` | `[]` | `[]` ✅ |
| `mine`／`Warehouse`／`SiegeWorkshop`／`Well`／`Blacksmith` | 各自同值 | 同 ✅ |

---

## 三、判据 2 · 测试组件被 tick 到（读数）＋ `ProductionSystem` 零旧类名

### 3.1 读数（`Logs/hh329_tick2_probe.log` · 一次性测试件，已删）

```
① 已登记测试键「comp.hh329_test」；键表计数 = 10
② 数据行（内存内改）= Warehouse.components = [comp.storage, comp.hh329_test]；该资产磁盘值仍 = [comp.storage]（⛔ 未写盘）
③ 挂载后：测试组件在场 = True；该 Go 的 IBuildingComponent = [StorageComponent, HH329TestTickComponent]
④ Tick 计数：0 → 1 → 2（两次 TickAll）⇒ 被 tick 到 ✅（未改 ProductionSystem）
⑤ 已还原 Warehouse.components = [comp.storage]；测试对象已销毁
```

⇒ ⭐ **「加一个带 `Tick` 的组件 ＋ 写进某一数据行 ⇒ 不改 `ProductionSystem` 也被 tick 到」成立**；⛔ 资产未被写（第二节②行 ＋ 事后 `git diff` 复核 `Warehouse.asset` 仅本批预期的 2 行数据行改动）。
测试组件与其登记件**已删除**（`Valley_HH329_TickTestProbe.cs` ＋ `.meta`）；主探针 `Valley_HH329_M4AProbe.cs` 保留（判据 1／3 可复跑，⛔ 不含测试组件）。

### 3.2 `ProductionSystem` 生产码零旧类名

```
$ Select-String -Path Assets/_Game/Systems/Building/ProductionSystem.cs -Pattern 'ProducerComponent|BlacksmithBuilding|SiegeWorkshopBuilding|MineByproductComponent' | Measure-Object
0
```

⇒ **0 命中** ✅（改前 4 组 `GetComponent<具体类>` 全删）。⚠️ 复编前有 **1 处头注**（`:5` 旧文"遍历所有 `ProducerComponent`"）残留 —— 已按真实行为同步改写（`:3-4`），**非生产码**改动，明列于此。

---

## 四、判据 3 · tick 间隔 1 秒 ＋ `ProducerComponent` 每秒加产量不变

| 项 | 读数 | 依据 |
|---|---|---|
| tick 间隔 | **`_tickInterval` = 1** | `ProductionSystem.cs:13`（字段值未改）＋ `:18-26` `Update`（`_timer >= _tickInterval` ⇒ `TickAll`）**未改** |
| 逐 Tick 产水 | Water：初始 **0** → 1×TickAll **4**（Δ4）→ 2×TickAll **8**（Δ4） | `Logs/hh329_m4a_probe.log` Part 2b · Well（`rate=4` · `仓容=100` · 免工自产） |
| `ProducerComponent` 本体 | 生产码 diff ＝ **1 行**（仅类声明行 `:8` 加 `ITickable`）；`Tick()` 与 `_mainAccumulator` 累加逻辑**逐字未改** | `git diff --numstat`＝`1 0`（`ProducerComponent.cs`） |

⇒ 「产量仍由组件自己在 `Tick` 里累加」＋「每秒一次」**均保持** ✅。

---

## 五、判据 4 · `TryAdvertiseTask`／`TaskScheduler` diff 为 0

```
$ git --no-pager diff --numstat -- "Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs"
（空输出）
$ git --no-pager diff -- "Valley Rampart/Assets" | Select-String '^\+\+\+|^@@'
（仅列出 20 个 Buildings 资产 ＋ GameScene.unity ＋ 8 个生产文件；⛔ 无 TaskScheduler／TryAdvertiseTask 承载文件）
```

- `TaskScheduler.cs`：**diff = 0** ✅（本批未打开该文件做写操作）
- `TryAdvertiseTask`（4 处承载：`Building.cs`／`BlacksmithBuilding.cs`／`SiegeWorkshopBuilding.cs`／`MineByproductComponent.cs`）：三处组件文件的本批 diff **仅类声明 1 行**（`ITickable`），`TryAdvertiseTask` 方法体**逐字未动**；`Building.cs` **不在本批改动面** ✅
- `05_交互层.md`（7 行完成规则）：**未改** ✅；在岗计时仍在 `TaskScheduler`（未动）✅

---

## 六、判据 5 · 禁止项四项

| 项 | 读数 |
|---|---|
| 未新增 `BuildingAbilityCatalog` | 全库命中 **0** ✅ |
| 未删 `InteractableType` | `BuildingDef.cs` 定义仍在（enum ＋ 字段 2 处命中）✅ |
| 未删 6 栋空资产 | `rift`／`portal`／`ruins`／`treasure_box`／`FoodWorkshop`／`Ranch` **6/6 仍在** ✅ |
| 未删 `SpawnerComponent` | `BuildingComponents.cs` 类仍在 ✅ |

---

## 七、红线与停手检查

| 项 | 结果 |
|---|---|
| ⛔ 不改 `TryAdvertiseTask`／`TaskScheduler`／`WorkAt` | ✅ 未改（§五） |
| ⛔ 不改 `05` §四 7 行完成规则／在岗计时仍在 `TaskScheduler`／产量仍由组件 `Tick` 累加 | ✅（§四） |
| ⛔ 不把 `IsFortification` 改读能力表／不建能力表 | ✅ 未动 `IsFortification`（`Building.cs:1271-1274` 逐字未改） |
| ⛔ 不删资产／不删 `CastleCoreComponent` | ✅（§六） |
| ⛔ 不碰既有脏文件（`GameScene.unity`／`Packages`／美术未跟踪） | ✅ 场景 diff 仍为**前序的 `5/1`**（本批前即如此）＋ 场景内 **0 个** `HH329*` 对象 ✅ |
| 不 commit／不 push | ✅ 均未执行 |
| **停手 ①** 保持集合不变必须改广告面／`TaskScheduler` | ⛔ **未命中**（集合零差异 ⇒ 无需动） |
| **停手 ②** 某栋改前改后对不上且说不清漏了哪个 `if` | ⛔ **未命中**（40/40 全同；改前侧为**逐字复刻**，无"说不清"） |
| **停手 ③** 发现 `ITickable` 已有实现者 | ⛔ **未命中**（开工前实读全库 **0 命中**） |

---

## 八、零改动声明（`git status`）

**本批 Assets 面改动面 ＝ 29 文件 ＋177／−60**，其中：

```
本批生产码 8 文件      BuildingDef +5 ／ BuildingComponents +86 ／ BuildingFactory +13−43
                       ProductionSystem +18−12 ／ MineByproduct 1−1 ／ Producer 1−1 ／
                       Blacksmith 1−1 ／ SiegeWorkshop 1−1   ⇒ 合计 +126／−58
本批数据行 20 资产     ＋46 行（每栋只加 `components:` ＋ 键行）
本批新增探针 1 个      Assets/Editor/Smoke/Valley_HH329_M4AProbe.cs（＋Unity 自生成 .meta）
⛔ 非本批（前序脏点）  GameScene.unity  5/1  ← 与本批开工前一致（未增未减）
```

**`git status --short`（`Valley Rampart` 面）**：`M`＝上面 29 文件；`??`＝本批探针 2 项（`.cs`＋`.meta`）＋ **既有历史未跟踪**（`Valley_HH289_EcoProbe`／`HH290_GatherProbe` ＋ `.meta`、`HH319_U15Probe.meta`、`HH319_WaterAccount.meta`、`Art/Ground` 4 项）—— 后 6 类与本批**无关**，⛔ 未碰。

⚠️ **另有两份文档在改动面但 ⛔ 非本批**（本端对 `最高优先级文档/**` **零写操作** · 疑为策划端并行会话产物，如实列报）：
`最高优先级文档/08_建筑功能层.md`（`9/9`）／`最高优先级文档/中层执行计划.md`（`2/2`，实读改动＝`M4-F` 行改为「⭐**已闭合（`HH.328`）**…不再做降级方案」⇒ 正是策划端采纳 `HH.328` 结论的动作）。
**本批落盘物（非 Assets 面）**：报告 1 份（本文件）＋ 账本 `HH.330` 行 1 行 ＋ 读数 2 份（`Logs/hh329_m4a_probe.log`／`Logs/hh329_tick2_probe.log`）＋ 工具脚本 2 份（`Logs/_hh329_splice_attach.py`＝CRLF 保行尾的 `BuildingFactory` 改法；`Logs/_hh329_migrate_components.py`＝20 栋数据行迁移）。

---

## 九、列报 / §待裁（5 条）

| # | 事项 | 事实 |
|---|---|---|
| 1 | ⚠️ **两项键带「来源守卫」** | `comp.rift`／`comp.castle_core` 的绑定器内含 `b.sourceType` 比对（`BuildingComponents.cs:220-223`）。**理由＝保等价**：改前这两项的判据读的是 **运行时 `b.sourceType`**，而玩家建造路径（`Building.Init:431` 置 `None`）与调试入口（`AIDebugSpawnController:512` 传 `None`）都不是 def 的 sourceType ⇒ 若纯按数据行挂，这两条路径会**多挂**组件（以 `castle` 为例）。读数已验：`castle@None` 两侧同为 `[CombatComponent]`（§2.3）。⇒ 若策划端要求「**纯数据行**、无来源守卫」，去掉守卫的代价＝**上述两条路径行为变化**（`castle` 调试实例会开始挂主城核）——请裁。 |
| 2 | ⚠️ **`BuildingMappingTable.asset` 不是 def** | `Resources.Buildings` 内 41 资产中 40 个是 `BuildingDef`（`Resources.LoadAll<BuildingDef>` 实读 **40**），`BuildingMappingTable` 是映射表 ⇒ 未加 `components`（正确）。 |
| 3 | ⚠️ **`magic_tower.asset` 的 def id ＝ `MagicTower`**（与文件名大小写不同） | 迁移脚本按 `id:` 行插入（不按文件名），已核；仅提示后续按文件名找 def 时注意。 |
| 4 | ⚠️ **探针分「静态面／运行面」两段** | 运行面需 Play 模式（Edit 模式 `Singleton.Instance` 隐式创建会抛 `DontDestroyOnLoad`）⇒ 探针在 Edit 模式自动跳过运行面并写明原因；本批已跑 Play 面（读数在案）。 |
| 5 | ⚠️ **账本登记未提交** | 遵红线 ⛔ 不 commit ⇒ `HH.330` 行 ＋ 本报告留工作区（取号已占号）。 |

---

## 十、零方案声明

本报告只含：改动清单（`file:line`）＋ 判据 1 逐栋对照＋ 判据 2 读数 ＋ 判据 3／4／5 读数 ＋ 红线／停手检查 ＋ 零改动声明 ＋ 列报。⛔ 未做 `M4-B`／`C`／`D`／`E`、⛔ 未出后续方案、⛔ 未 commit／push。

## 十一、策划裁决（D864）

验收通过。来源守卫留下，不另立项。全文：`多Agent交接/策划端/HH.330_M4-A_验收裁决.md`。
