# HH.313 M1-A（资源表 ＋ 仓库结构）开工前口径报裁

> 类型：**待决策**
> 状态：✅ **已裁决**（`D789` · 2026-09-19 · **全裁 A/A/A** ＋ **2 处勘正** ＋ **5 条施工补充口径**）
> 日期：2026-09-19 · 发起端：**执行端** · 关联清单/文档：`中层执行计划` §4.1（`M1-A`）／`09_资源与仓库.md` §4.1／§4.2／§4.3／§5.3／§七／§十五
> 关联任务书：**「任务书 · M1-A 中层施工片 —— 资源表 ＋ 仓库结构」**（用户 2026-09-19 对话直接给出，`D788` §2 派工）

---

## 一、做了什么（执行端填，带证据）

### 1.1 预读（三份真源，逐节读完）

- `最高优先级文档/09_资源与仓库.md`（§二 三条基本判据 · §三 标签＝路径 · §四 资源表 · §五 仓库 · §七 CRUD · §十五 NPC 自仓 · §十六 建造走仓）
- `最高优先级文档/中层执行计划.md` §4.1（`M1-A` 8 项差异 · 边界）＋ §七（`D788` 四条拍板：一次切到位／旧档作废／不写迁移脚本）
- `最高优先级文档/底层执行计划.md` §四（交付物统一格式：双列对照 ＋ 实测读数 ＋ 未完成项显式列出）

### 1.2 引用面机械扫描（本片为**全库级结构换代**，非局部改）

| 旧结构 | 命中 | 文件数 | 命令 |
|---|---|---|---|
| `ResourcePack` | **129 处** | **42 文件**（含 12 个 `Assets/Editor/**` 冒烟容器） | `rg -c ResourcePack Assets` |
| `IWarehouse`／`StorageComponent`／`storedAmount`／`resourceType` | **337 处** | **44 文件** | `rg -c 'IWarehouse\|StorageComponent\|storedAmount\|resourceType\b' Assets` |

### 1.3 实现者与调用点清单（任务书 §五 要求「先列出所有实现者与调用点」）

**`IWarehouse` 实现者 ＝ 恰好 2 个**（机械枚举 `class … IWarehouse`，全库仅两行）：

| # | 实现者 | 位置 | 现状 |
|---|---|---|---|
| 1 | `StorageComponent` | `Building/StorageComponent.cs:8`（`IBuildingComponent, IHarvestable, IWarehouse`） | 单资源 `resourceType`＋`storedAmount`＋`capacity` |
| 2 | `WorkerInventory` | `Unit/WorkerInventory.cs:9` | 单资源 `carriedType`＋`carriedAmount` |

**调用点（运行时主面，逐文件）**：`WarehouseRegistry`（`:19 _storages` 泛型即 `StorageComponent`；`:47 :67` 读 `storedAmount`／`resourceType`）／`WarehouseHelper`（`:29 :78 :87 :101 :118` 全走 `IWarehouse.Query/Take`）／`TaskScheduler`（**41 处**：搬运两段 `LoadInventoryFromSource:691`／`UnloadInventory:716`／`:891` 同型过滤）／`TreasureVault`（9 子仓 ＋ `GetAmount/Deposit/Take` 转发 ＋ `:62-75` 读 `KingdomManager.Treasury*`）／`BuildingFactory`（11 处挂载/装配）／`Building`（18 处）／`BuildingPanel`（11 处）／`WarehousePanel`（13 处）／`MineByproductComponent`（23 处）／`SiegeWorkshopBuilding`（14 处，含 `:205` 弹药仓 `IWarehouse` 出口）／`BlacksmithBuilding`／`ProducerComponent`／`ChestManager`／`ChestEntity`／`ChestSaveData`／`KingdomManager`／`KingdomState`／`RulerController`／`AIEconomySettlement`／`AbstractEconomySettlement`／`KingdomBrain`／`SiegeProductionSystem`

**SO 资产实际承载数据（不是"理论上会掉"，已实读）**：

| # | SO 字段 | 位置 | 资产侧现值样例 |
|---|---|---|---|
| 1 | `BuildingDef.cost` | `Data/BuildingDef.cs:22` | **`Resources/Buildings/` 下 40 个 `.asset` 均有 `cost:`**（`House.asset:20-24` ＝ `wood: 4`） |
| 2 | `BuildingLevel.upgradeCost` | `Data/BuildingDef.cs:149` | 同上 40 个 `.asset` 的 `levels[].upgradeCost`（`House.asset:40-44` ＝ `stone: 6 / wood: 10`；`:47-51` ＝ `stone: 16 / wood: 24`） |
| 3 | `ChestSaveData.contents` | `World/ChestSaveData.cs:36` | **存档字段**（旧档可作废 ⇒ 不阻塞） |
| 4 | `KingdomConfig.castleUpgradeCosts` | `Kingdom/KingdomConfig.cs:13`（`ResourcePack[]`） | `Config/KingdomConfig.asset` |
| 5 | `SiegeProductionConfig` 5 字段 | `Kingdom/SiegeProductionConfig.cs:16/17/21/23/25` | `Config/SiegeProductionConfig.asset`（投掷机/弩炮/臼炮/藤蔓/攻城槌造价） |
| 6 | `KingdomDef.baseStockpile` | `Data/Kingdoms/KingdomDef.cs:78` | `Config/Kingdoms/Kingdom_*.asset` |
| 7 | `KingdomFoundingConfig.stockpile` | `Data/Kingdoms/KingdomFoundingConfig.cs:27` | `Config/Kingdoms/KingdomFoundingConfig.asset` |
| 8 | `KingdomRegistry` 过渡账本行 | `Kingdom/KingdomRegistry.cs:233` | `Config/Kingdoms/KingdomTemplateLibrary.asset` |

> ⚠️ **即：本片若按「切到位」改类型，上述 8 类 SO 的**现有数值**会在 Unity 反序列化时被静默清零**（不属于 `D788` §4 豁免的「旧存档」范畴）。

### 1.4 代码改动

**零**（`Assets/**` 一行未写）。本信仅预读 ＋ 机械扫描，待裁决后开工。

---

## 二、现状与阻塞

任务书 §一 的 8 项（`09#35/36/37/38/39/46/50/51`）在执行口径上**自洽**，但有 **3 处 09 未写清 / 会掉真实数据** 的岔口，按 `AGENT 准则`＋`execute-checklist` 铁律 4（**验收标准不许降**）与「09 没写清的回来问，不要自行发挥」**停手报裁**：

| # | 岔口 | 为什么执行端不能自行决定 |
|---|---|---|
| Q1 | 换类型会清零 8 类 SO 资产数值（§1.3 第 3 项） | `D788` §4 只豁免**旧存档**；SO 资产是**游戏数据**（40 栋建筑造价全免费＝中间期可玩性破损）。写不写一次性资产迁移工具＝**产物范围变更**（任务书 §四 交付物未列它） |
| Q2 | `WorkerInventory` 是否随 `IWarehouse` 新契约一起多资源化 | `09` §5.3 与 §15.1 **都写了要改**（§15.1「`WorkerInventory` 跟 `StorageComponent` 一起改」），但 §十五 整体挂在 **`M1-F`** 名下，而本片任务书 §二 **⛔ 明列不做 `M1-F`** ⇒ **两份权威文本互相矛盾**，且第二选项会牵出 `TaskScheduler` 41 处搬运路径＋`UnitController` v5 存档代理＝**行为变更**（本片禁用） |
| Q3 | 资源表要不要现在就加第 14 项 | `09` §4.3 A 组有「⭐14 回血包 `res_consumable.medkit`（`D786` 新立）」，任务书写「**13 项**清单见 §4.3」。现在只加表行＝**孤儿资源**（`09` §十-1「启动自检报孤儿资源」会直接报警），且其产出端（`Hospital` 重塑 `09#63`）属 **`M6`** |

**另报备 1 条（不阻塞，仅需知悉）**：

- **`09#37` 的注释义务已明确**：`IWarehouse.cs:13` 现写「与 sim harness/Core **签名逐字对齐** ⇒ 单侧改签名必须记 `HH` 回策划」，而 `09` §5.3 ＋ 任务书 #37 已裁【**用户授权不管 sim**】⇒ 本片施工时**必须同步改写该注释**（否则下一个人会误以为仍有对齐义务）。执行端按任务书写法处置，无需额外裁决。

---

## 三、待决策事项（每项：选项 ＋ 推荐 ＋ 影响）

### Q1 — 8 类 SO 资产的旧数值怎么处置？

这决定**本片是否新增一个一次性 Editor 资产迁移工具**（任务书 §四 交付物未列）。

- **A（推荐）**：写**一次性 Editor 资产迁移工具**（解析 `Resources/**/*.asset` 的 YAML，把旧 `gold/stone/wood/food/metal/stoneAmmo/fireballAmmo/magicAmmo` 逐项映射到新「资源量列表」的 `type + amount`；跑完留档于 `Assets/Editor/` 或跑完即删）。理由：①**零数据丢失**；②映射是 1:1 稳定（枚举名 → 枚举值未变）；③**不属** `D788` §4 禁令范围（那禁令针对**存档**，此处是**资产**）。影响：本片 ＋1 个工具文件 ＋1 次跑批证据。
- **B**：**接受清零**，SO 数值归数值批重填。理由：结构一次切到位、产物最少。影响：**中间期 40 栋建筑全免费、第 5/6/7 类造价全 0**，在数值批回填前游戏经济面不可验证（后续子片 `M1-C/E` 的读数会失真）。
- **C**：**本片只改运行时，SO 字段暂不动**。理由：不动资产数据。影响：⛔ **与用户「一次切到位、不许既保留旧结构又新增新结构」红线直接冲突**（`ResourcePack` 仍须在场）⇒ 若采此案，等于本片放弃 `09#50`，需明示豁免。

### Q2 — `WorkerInventory` 本片怎么处置？

这决定**本片是否连带 `TaskScheduler` 41 处搬运路径 ＋ `UnitController` v5 存档代理**。

- **A（推荐）**：**只做签名适配** —— `WorkerInventory` 按新 `IWarehouse` 契约实现（内部仍单资源 `carriedType/carriedAmount`），**多资源化 ＋ 存档代理改造留给 `M1-F`**。理由：①任务书 §二 **⛔ 明列不做 `M1-F`**，而 §十五 属 `M1-F`；②本片纪律「只改结构，任何行为变化属后续子片」；③任务书 §三 判据 5 的「引用清零」清单**只列 `ResourcePack`／`KingdomManager.Treasury*`／`StorageComponent.resourceType`**，**未列** `carriedType/carriedAmount`。影响：本片边界最小；`WorkerInventory` 中间期为「同一契约下的退化实现」（一次只装一种）。
- **B**：**本片一起多资源化**（照 `09` §15.1 字面）。理由：口径文本字面满足、避免二次返工。影响：**范围显著扩大** —— `TaskScheduler:691/716/:891` 搬运两段＋`WorkerInventory` 存档（`UnitController` v5 两标量 → 字典）＋12 个 Editor 冒烟容器连带 ⇒ 属**行为变更**（≠ 本片"只改结构"），且与任务书 §二 的 ⛔ 冲突。

### Q3 — 资源表现是否就加第 14 项 `res_consumable.medkit`？

- **A（推荐）**：**不加**，`ResourceCatalog` 只落 `09` §4.3 A 组**已有 13 项**（枚举现值 13 项，1:1 对应）。理由：①其产出端 `Hospital` 重塑（`09#63`）属 `M6`；②现在加＝**孤儿资源**，会被 `09` §十-1 启动自检直接报警。影响：`M6` 时「枚举 ＋1 行、表 ＋1 行」（`09` §4.1 已定"加一个资源＝两处"）。
- **B**：**现在就加**（枚举 ＋1 ＋ 表 ＋1 行）。理由：`09` §4.3 已列、免二次动表。影响：中间期启动自检**常驻 1 条孤儿资源告警**（须在自检白名单显式豁免，或接受告警噪声）。

---

## 四、下一步建议

1. 本信裁决后，**执行端按裁决直接开工**（无需再出开工回执：本片无 sim 义务、无 `AI.Core` 面、不碰 `GridTypes.cs`／地图四门）。
2. 施工顺序（建议，按依赖拓扑）：`ResourceCatalog`（表 ＋ `ResourceStack/ResourceList`）→ 新 `IWarehouse` 契约（含 `:13` 注释改写）→ `StorageComponent` 多资源容器 ＋ `WarehouseRegistry`／`WarehouseHelper` 适配 → `TreasureVault` 9 仓塌 1（含 `Instance` 按国查）→ `ResourcePack` 全量替换（含 `BuildingDef.cost`／`upgradeCost`／箱子／搬运）→ 配方表建立（仅建表，`Transform` 改走表归 `M6-B`）。
3. 交付物照 `底层执行计划` §四：改动清单（`file:line`）＋ **判据 1~6 逐条实测读数**（禁形容词）＋ 旧结构引用清零 `grep` 原文＋未完成项显式列出。

### 应登记项声明（`D767` 纪律：执行端不代写策划端账本）

| # | 应登记位置 | 内容 |
|---|---|---|
| 1 | `多Agent交接/_编号登记.md` | 水位线 `HH.312 → HH.313`；在途行：`HH.313｜M1-A 开工前口径报裁｜执行端｜⏳待裁决｜2026-09-19` |
| 2 | `多Agent交接/_交接索引.md` | 同上登记行 ＋ 本文件路径 |

---

## 策划裁决（策划端回写 · `D789` · 2026-09-19）

> ⭐ **三条全裁 A**（与执行端推荐一致）—— 按「**判据三直读**」**独立复核**后才落笔，已实读：`House.asset:20-24／:40-44／:47-51`（资产确有值）／`09` §4.3 A 组（**标题 13 项 vs 表体 14 行**）／`09` §15.1 `:522`／`TaskScheduler.cs:730/732/735/742/747/798/804`（**真连带**，非推断）。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| Q1 SO 资产旧数值 | ⭐ **A：写一次性 Editor 资产迁移工具，且必须零丢失**。⚠️ **实现方式由本端指定**：**优先「保留旧字段一版 → 脚本读旧填新 → 逐项核对 → 再删旧字段」三步法**（走 Unity 自己的序列化）；⛔ **不要解析 YAML 文本**，仅当旧字段无法保留时才退到文本解析 | ① ⭐ **不只是"数据丢失"** —— `M1-C`（建造走仓）与 `M1-E`（金币）**都要读造价做判据** ⇒ 造价全 0 ⇒ **后续片读数失真、会掩盖 bug**（B 案不可取）② 资产是**设计数据**（项目源文件）⛔ **不是存档** ⇒ 不在 `D788` §4 豁免范围 ③ 映射 1:1 稳定（枚举名未变） |
| Q2 `WorkerInventory` | ⭐ **A：本片只做签名适配**（内部保留单资源）。⚠️ **硬约束**：**保留 `carriedType`／`carriedAmount` 字段及其自有 API**（`UnloadAll`／`IsEmpty` 等）⇒ **`TaskScheduler.cs:730/732/735/742/747/798/804` 七处一行不动**；多资源化 ＋ 卸货路由 ＋ `UnitController` v5 存档代理 ⇒ **归 `M1-F`** | ① `TaskScheduler` **直接读 `inv.carriedType`**（实读 **7 处**）⇒ "一起多资源化"是**真行为变更**，超出本片"只改结构" ② 任务书 §二 ⛔ 已明列不做 `M1-F` ③ 卸货路由（"按资源找最近仓"）是**玩法设计**、不是结构改造 |
| Q3 第 14 项回血包 | ⭐ **A：不加** —— `ResourceCatalog` 只落**已有 13 项**；⛔ **不落** `res_consumable.medkit` | ① 产出端 `Hospital` 重塑（`09#63`）属 **`M6`** ⇒ 现在加＝**孤儿资源**，会被 `09` §十-1 启动自检直接报警 ② 加一个资源 ＝ **两处**（`09` §4.1 已定）⇒ `M6` 时补，成本相同 ③ ⭐ **"13 项"本身是对的**（枚举现值 13、与 A 组**已有 13 项 1:1**）—— 疑问源自 `09` §4.3 **标题与表体自相矛盾**（见下） |

### ⭐ 本端两侧勘正（`D789` · 裁决过程中实读检出）

| # | 勘正 | 依据 |
|---|---|---|
| **勘-1** | ⭐ **`09` §4.3 A 组标题错** —— 原写「已有 **13** 项」，而**表体 14 行**（第 14 行＝⭐回血包）⇒ 改为「**已有 13 项 ＋ ⭐ 表内已列第 14 项（回血包 · ⚠️ 落码归 `M6`）**」；⚠️ 并记：**编号 14 在 A 组（回血包）与 B 组（居民）重复**（⛔ 本夹"不主张重编"⇒ 引用须带组名） | 实读 `09`:140~157 |
| **勘-2** | ⭐ **`09` §15.1 `:522` 表述有歧义** —— 原写「`WorkerInventory` **跟 `StorageComponent` 一起改**」会被读成"同片同批" ⇒ 补**切分**：**签名适配**（本片 `M1-A`）／**多资源化 ＋ 存档代理**（`M1-F`） | 实读 `09`:522 |
| **勘-3** | ⚠️ **执行端引述一处不准确（不影响结论）** —— §三 `Q2` 写"`09` §5.3／§15.1 **都**说改"，而实读 **§5.3 表只有 3 行**（`StorageComponent`／`IWarehouse`／`TreasureVault`），**未列 `WorkerInventory`**；"一起改"是 **§15.1 `:522`** 的话 ⇒ 引述以 §15.1 为准 | 实读 `09`:229~233 |

### ⭐ 施工补充口径（针对 `Q1` A 案落地 · 5 条）

| # | 口径 |
|---|---|
| **补-1** | ⭐ **`IWarehouse` 新签名的推荐形状** —— `CanTake`／`Take`／`Deposit`／`Transform` **保持「按资源参数」**（多资源容器天然支持），**只需 `Query` 改为「返回该仓全部存量」（条目列表）** ⇒ 两个实现者**只改 `Query`** ⇒ `WorkerInventory` 的"签名适配"成本极小。⚠️ **建议非强制**（实现细节归执行端），但 ⭐ **"只改 `Query`"是最不牵动调用面的形状** |
| **补-2** | ⭐ **`WarehouseRegistry` 的"按资源找仓"** —— 现 `FindNearestAvailable(type, …)` 走单资源假设；多资源仓后**判据须改 `CanAccept`**（⚠️ **签名可不变**） |
| **补-3** | ⚠️ **迁移必须处理"缺省＝0"** —— 实读 `House.asset` 的 `cost` **只列非 0 字段**（8 字段里只出现 4 个）⇒ 按"**缺省 0**"处理；且 **全 0 的 `cost` 也要产出空列表（⛔ 非 `null`）** |
| **补-4** | ⭐ **交付物增列：资产迁移前后逐项核对表**（**40 栋 × `cost` ＋ `levels[].upgradeCost`** ＋ `KingdomConfig.castleUpgradeCosts` ＋ `SiegeProductionConfig` 5 字段 ＋ `KingdomDef.baseStockpile` ＋ `KingdomFoundingConfig.stockpile` ＋ `KingdomTemplateLibrary`）—— ⛔ **禁只报"迁移完成"** |
| **补-5** | ⏭️ **`WorkerInventory` 的过渡态须显式标注** —— 交付时**列出"单资源语义的所有假设点"**（供 `M1-F` 收口） |

### 分歧裁决记录

- 执行端意见：见 §三（`Q1`→A／`Q2`→A／`Q3`→A） · 策划端意见：**三条同裁 A**
- 裁决：**无分歧**（执行端三处推荐**全部采纳**） · 依据：本端独立复核（上表"理由"列）
- ⚠️ **本端自陈一处缺陷**：`M1-A` 开工提示词原写「**13 项**清单见 §4.3」—— 是**照 `09` 标题抄的**、未数表体（与本日 `L-40` 第 6 次扩维**同族复发**）⇒ 已勘正 `09` 标题并记入台账 **§九十四**

### 衍生产物

- 新建设计文档：**无**（勘正在 `09` 原处）
- 新建清单任务：**无**（本片任务书已签发；⛔ 不新增批次）
- ⭐ **本片新增一个子片内产物**：**一次性 Editor 资产迁移工具**（`Q1` A 案）—— ⚠️ **跑完即删或标"一次性"**，⛔ **不留在生产码里**
