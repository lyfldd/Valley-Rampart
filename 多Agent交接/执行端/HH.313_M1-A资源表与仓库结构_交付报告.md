# HH.313 · M1-A 交付报告 —— 资源表 ＋ 仓库结构

> 执行端：TraeCode｜依据：任务书（M1-A）＋ `09_资源与仓库.md` ＋ 中层执行计划 §4.1 ＋ 底层执行计划 §四
> 开工前报裁：`多Agent交接/执行端/HH.313_M1-A资源表与仓库结构_开工前口径报裁.md`（`D789` 三条全裁 A ＋ 补-1~补-5）
> 口径真源：`09` §二/§三/§四/§五/§七/§十一/§十五 ＋ `D788` §七四条 ＋ `D789` 五条落码口径

---

## 〇、结论速览

| 项 | 结果 |
|---|---|
| 编译 | **0 error**（Unity MCP `unity_editor.refresh`）／15 warning **全部既有**（`CS0114/0108/0252/0414/8632`，所在文件均非本片改动） |
| 8 项差异 | `09#35`／`#36`／`#37`／`#38`／`#39`／`#46`／`#50`／`#51` **全部落地** |
| 判据 1~6 | **逐条实测读数**见 §三（禁形容词，全部为数字/文本读数） |
| 资产迁移三步法 | 走完；**逐项核对表 88 行、数值不一致 0**；旧字段与 `ResourcePack` **已退役** |
| 旧结构引用清零 | `*.cs` 内 **0 命中**（原文见 §四）；49 个资产内**孤儿 YAML 键亦已由 Unity 序列化器清除** |
| ✅ 追加迁移已裁 | **1 处追加迁移**（`warehousePaths` —— 裁决未列但"不动行为"必需）§五-1 ⇒ **`D790` 判-1 裁 A（认可）**；同处两问归 **勘-2／勘-4／判-2** |
| ⚠️ 需知晓 | **2 处结构强制的行为变化**（非主动改行为）§五-2 |
| 未完成项 | 显式列出 §六（含归属子片） |
| 红线 | `GridTypes.cs` ⛔未碰／地图四门 ⛔未碰／存档迁移脚本 ⛔未写／双轨开关 ⛔未写／训练仓 ⛔未碰 |

---

## 一、改动清单（改前 ⇒ 改后 · 双列对照）

### 1.1 新建（2 个）

| 文件 | 内容 |
|---|---|
| `Assets/_Game/Data/ResourceCatalog.cs`（394 行） | `ResourceCatalog` 静态表（13 行 · 枚举当键 · `Paths`/`Volume`/`DisplayName` 四字段）＋ `WarehousePaths`（`All="res"` · `Normalize`）＋ `ResourceAmount`（type+amount）＋ `ResourceList`（copy-on-write struct） |
| `Assets/_Game/Data/RecipeCatalog.cs` | `RecipeOutput`／`RecipeDef`／`RecipeCatalog`（表体**有据留空** —— 数值真源是 `BlacksmithDef.oreToMetalRatio`，抄一份＝双真源） |

### 1.2 `09#35` 资源定义硬编码 ⇒ 资源表

| 改前 | 改后 |
|---|---|
| 13 项 `ResourceType` 语义散落 7 套口径（UI 中文名/搬运量表/造价桶名/…各写一遍） | `ResourceCatalog` 一处定义；7 套口径改为查表（`DisplayNameOf`/`VolumeOf`/`PrimaryPathOf`/`PathsOf`/`Accepts`） |
| `BuildingPanel.ResName(ResourceType)`（私有 10 分支中文名表） | **删除**（改用 `ResourceCatalog.DisplayNameOf`）—— `BuildingPanel.cs` 原 `:497-513` 已成空位 |
| `ResourceType` 枚举 | **未改**（保持 13 项 1:1；第 14 项 `medkit` 按 `D789` Q3=A **不落码**，归 M6） |

### 1.3 `09#36` `StorageComponent` 单资源 ⇒ 多资源容器

| 改前 | 改后 |
|---|---|
| `public ResourceType resourceType;`（`Init` 由 `def.outputResource` 赋值） | `public string[] warehousePaths;`（`BuildingDef` 数据行 · `Init` 读 `def.warehousePaths` · [StorageComponent.cs:23](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L23)、[:40](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L40)） |
| `public int storedAmount;`（单值） | `private readonly Dictionary<ResourceType,int> _items`（[:29](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L29)）＋ `TotalCount`（[:83](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L83)）／`UsedSpace`（[:94](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L94)） |
| `capacity` 语义＝件数上限 | `capacity` ＝**一条容量线**（体积计）；占用＝Σ(数量×体积)；`CanAccept`＝`floor(剩余÷体积)`（[:117](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L117)） |
| 无标签概念 | `Accepts(type)`（前缀路径匹配 · [:61](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L61)）／`SumByPrefix`（[:129](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L129)） |
| `Add(int)`／`TakeOut(int)`（隐式单资源） | `Add(type,amount)`／`TakeOut(type,amount)`／`Deposit`／`CanTake`／`Take`（[:169](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L169)／[:182](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L182)／[:187](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L187)／[:192](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L192)／[:205](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L205)） |
| — | `Contents` 快照／`RestoreContents`（[:152](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L152)／[:305](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L305)）／`TrimToCapacity`（[:333](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L333)）／`PrimaryStoredType`（[:281](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L281)） |
| `Transform`（Ore→Metal 专属） | **行为不动**（[:217](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L217)；改走配方表归 M6-B） |
| `BuildingDef` 无仓库字段 | `[Header("仓库")] public string[] warehousePaths;`（[BuildingDef.cs:53](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/BuildingDef.cs#L53)） |

### 1.4 `09#37` `IWarehouse` 单资源契约 ⇒ 多资源

| 改前 | 改后 |
|---|---|
| `ResourceAmount Query()`（单资源量） | `List<ResourceAmount> Query()`（全部存量 · 空仓 ⇒ 空列表非 null）（[IWarehouse.cs:27](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/IWarehouse.cs#L27)） |
| `CanTake/Take/Deposit/Transform` | **签名不变**（按资源参数 · 补-1 推荐形状 ⇒ 调用面零牵动） |
| 类注释「与 sim 逐字对齐 · 改签名须记 HH」 | **该义务已废**（改为引用 `09` §5.3 用户授权；[IWarehouse.cs:15-20](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/IWarehouse.cs#L15-L20)） |
| 实现者 2 个 | 2 个（`StorageComponent`／`WorkerInventory`）—— **只改 `Query`** ＋ `WorkerInventory.Deposit` 返回值对齐 `int` |

### 1.5 `09#38` `TreasureVault` 9 子仓 ⇒ 1 容器

| 改前 | 改后 |
|---|---|
| 9 个 `StorageComponent` 子仓（金/石/木/粮/铁/特食/肉/水晶/火油 各一） | **1 个容器**，声明 `{ "res_material", "res_food" }`（[TreasureVault.cs:25](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L25)、[:62](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L62)） |
| `static TreasureVault Instance`（单例假定只有一座国库） | `static Dictionary<int,TreasureVault> _byKingdom` ＋ `Instance => Get(0)` ＋ `Get(int kingdomId)`（[:28](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L28)、[:35](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L35)、[:53](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L53)） |
| 9 个 `Treasury*` 读口（`KingdomManager`）＋ 存档 9 字段（`KingdomSaveData`） | **整段退役**；国库随建筑存档走 `BuildingSaveData.treasuryContents`（单源，消双真源） |
| `SpillToChest`（8 桶装箱） | `ResourceList.Of(...)` 单条目承载（[:121](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L121)） |

### 1.6 `09#39`／`#50`／`#51` `ResourcePack` 8 桶 ⇒ 资源量列表（造价 ≡ 配方同形）

| 持有者 | 改前 | 改后 |
|---|---|---|
| `BuildingDef.cost` | `ResourcePack` | `ResourceList`（[BuildingDef.cs:26](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/BuildingDef.cs#L26)） |
| `BuildingLevel.upgradeCost` | `ResourcePack` | `ResourceList`（[BuildingDef.cs:160](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/BuildingDef.cs#L160)） |
| `KingdomConfig.castleUpgradeCosts` | `ResourcePack[]` | `ResourceList[]`（[KingdomConfig.cs:14](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomConfig.cs#L14)） |
| `SiegeProductionConfig` 5 字段 | `ResourcePack` ×5 | `ResourceList` ×5（[SiegeProductionConfig.cs:16-29](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/SiegeProductionConfig.cs#L16-L29)） |
| `KingdomDef.baseStockpile`／`StaggerTier.stockpile` | `ResourcePack` | `ResourceList` |
| `KingdomState.resources` | `ResourcePack` | `ResourceList`（[KingdomState.cs:47](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomState.cs#L47)） |
| `ChestSaveData.contents`／`ChestEntity.contents` | `ResourcePack` | `ResourceList` |
| `BuildingSaveData.storedAmount:int` | 单值 | `storageContents` ＋ `treasuryContents`（[BuildingSaveData.cs:33-36](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingSaveData.cs#L33-L36)） |
| `RulerSaveData`（stone/wood/food/specialFood/meat） | 6 字段 | 删 5，只留 `rulerName`/`gold` |
| `WorldConfig.ResourcePack` 类型本体 | `struct ResourcePack` 44 行 | **已删**（`WorldConfig.cs` 尾部） |

### 1.7 调用面改写（生产码 · 38 文件）

`WarehouseRegistry`（`GatherActive` 判 `TotalCount>0` [:46](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/WarehouseRegistry.cs#L46)；`FindNearestAvailable` 判据改 `Accepts`＋`CanAccept` 签名不变 [:66](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/WarehouseRegistry.cs#L66) ／ `WarehouseHelper`（全方法 `ResourceList` ＋ 逐条目）／`Building`（`SumCostOf` 单源 [:683](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L683)、存档 `storageContents`/`treasuryContents`、`Demolish` 逐条目摊回）／`BuildingFactory`（`RestoreContents` ＋ `SumCostOf` 兜底）／`BuildingPanel`（造价/容量显示 + 删 `ResName`）／`BuildingMenuPanel`（造价行 4 处 `.Get`）／`WarehousePanel`（逐"本仓能装的资源"汇总）／`KingdomIntelPanel`／`RulerController`（删读档桥 4 项）／`KingdomManager`（删 9 个 `Treasury*`；`NextCastleUpgradeCost` 返回 `ResourceList`）／`KingdomState`（`CanAfford/Spend/Refund/AddResources` 逐条目 ＋ `GetResourceValue`）／`KingdomRegistry`／`KingdomFoundry`／`DifficultyManager`／`WorldConfig`／`AIEconomySettlement`（逐条目收五经济资源）／`AbstractEconomySettlement`／`SiegeProductionSystem`／`SiegeWorkshopBuilding`／`ProducerComponent`／`MineByproductComponent`／`BlacksmithBuilding`／`TrainingSystem`／`TaxSystem`／`SatietySystem`／`RanchSystem`／`KingdomBrain`／`UtilityScorer`／`TaskScheduler`／`ScheduleCenterStub`／`MonsterController`／`MonsterDef`／`DamageSystem`／`MachinePanel`／`WorkerInventory`／`ChestManager`／`ChestEntity`／`ChestSaveData`。

### 1.8 判据读数生成器（新增 · 保留供复跑）

`Assets/Editor/Smoke/Valley_HH313_M1A_WarehouseProbe.cs` —— Edit Mode 单元级探针（不起场景、不进 Play Mode），输出 `M1A_判据读数.txt`。

---

## 二、资产迁移三步法（`D789` Q1=A · 零丢失）

### 2.1 第 1 步：加新字段、保留旧字段

新字段 `ResourceList` ＋ 旧字段一版（旧字段持 `[FormerlySerializedAs("旧名")]`，令 Unity 自己把 `cost:`／`upgradeCost:` 等旧 YAML 键反序列化进旧字段）。**⛔ 全程未解析 YAML 文本。**

### 2.2 第 2 步：一次性 Editor 工具「读旧填新」

工具功能（跑完已按裁决**删除**，⛔ 不留生产码）：

1. 遍历 `Assets/Resources/Buildings/**` 全部 `BuildingDef`：`legacyCost ⇒ cost`；`levels[i].legacyUpgradeCost ⇒ levels[i].upgradeCost`；
2. 遍历 6 个 `KingdomDef`：`legacyBaseStockpile ⇒ baseStockpile`；
3. `KingdomConfig.legacyCastleUpgradeCosts ⇒ castleUpgradeCosts`（6 档）；
4. `SiegeProductionConfig` 5 组 `legacy*Cost ⇒ *Cost`；
5. `KingdomFoundingConfig.staggerTiers[i].legacyStockpile ⇒ stockpile`（3 档）；
6. 逐条写 `EditorUtility.SetDirty` ＋ `AssetDatabase.SaveAssets`；
7. 口径：**缺省 0 ⇒ 不产出条目**（`ResourceList.Add(type,0)` 为空操作）；**全 0 ⇒ 空列表**（`items == Array.Empty`，⛔ 非 null）；
8. 输出「迁移前（旧桶文本）→ 迁移后（资源量列表文本）→ 一致?」逐项核对表。

### 2.3 核对表（`M1A_资产迁移核对表.txt` · 工程根）

- **明细行数 = 88；数值不一致 = 0；仓库声明未填 = 0**；
- 覆盖：40 栋 × `cost` ＋ 各栋 `levels[]` 全档（含 `stone_piece/bridge/well` 等 0 档者）＋ `KingdomConfig` 6 档 ＋ `SiegeProductionConfig` 5 字段 ＋ 6 个 `KingdomDef.baseStockpile` ＋ `KingdomFoundingConfig` 3 档；
- 抽样原文（判据：旧桶非 0 项与新列表条目**逐值相同**）：

```
Resources/Buildings/Warehouse.asset   BuildingDef.cost       金4/石4            金4/石4            OK
Resources/Buildings/Warehouse.asset   levels[0].upgradeCost  石10/木10          石10/木10          OK
Resources/Buildings/Warehouse.asset   levels[1].upgradeCost  石20/木24          石20/木24          OK
Resources/Buildings/House.asset       BuildingDef.cost       木4                木4                OK
Resources/Buildings/House.asset       levels[0].upgradeCost  石6/木10           石6/木10           OK
Resources/Buildings/wall.asset        levels[1].upgradeCost  石24/木30/铁10     石24/木30/铁10     OK
Resources/Buildings/ore_vein.asset    BuildingDef.cost       (全 0)             (空列表)           OK
Resources/Config/KingdomConfig.asset  castleUpgradeCosts[0]  金2/石6/木10/粮6   金2/石6/木10/粮6   OK
Resources/Config/KingdomConfig.asset  castleUpgradeCosts[5]  金120/石140/木180/粮140  金120/石140/木180/粮140  OK
Resources/Config/SiegeProductionConfig.asset  mortarCost     金15/石25          金15/石25          OK
Resources/Config/Kingdoms/Kingdom_IronHoof.asset  baseStockpile  石25/木40/粮40  石25/木40/粮40     OK
Resources/Config/Kingdoms/KingdomFoundingConfig.asset  staggerTiers[2].stockpile  石40/木64/粮64  石40/木64/粮64  OK
（全表 88 行见 M1A_资产迁移核对表.txt）
```

落盘抽样（`House.asset`）：

```yaml
  cost:
    items:
    - type: 2        # Wood
      amount: 4
  warehousePaths: []
```

### 2.4 第 3 步：核对通过 ⇒ 删旧字段 ＋ 删 `ResourcePack`

- 已删：`BuildingDef.legacyCost`／`BuildingLevel.legacyUpgradeCost`／`KingdomConfig.legacyCastleUpgradeCosts`／`SiegeProductionConfig` 5 个 `legacy*Cost`／`KingdomDef.legacyBaseStockpile`／`StaggerTier.legacyStockpile`／`WorldConfig.ResourcePack` 类型本体 ＋ `BuildingDef.cs` 的 `using UnityEngine.Serialization;`；
- 资产内遗留的**孤儿 YAML 键**（`legacyCost:` 等 66 处）：用 Unity 自身序列化器重存（`SetDirty` ＋ `SaveAssets`，49 个资产）清除 —— **⛔ 未解析/改写 YAML 文本**；
- `ChestSaveData.contents` 随类型换代（`ResourceList`）；**⛔ 未写旧档兼容**（`D788` §4 游戏未发布 ⇒ 旧档可作废）。

---

## 三、判据 1~6 逐条实测读数

> 全量原文：工程根 `M1A_判据读数.txt`（可由 `ValleyRampart/探针/M1-A 仓库与资源表读数（HH.313）` 复跑）。以下为原文摘录。

### 判据 1 · 一栋建筑只改一行

```
声明行 A = res_material ⇒ Accepts(Stone)=True  Accepts(Wood)=True  Accepts(Food)=False  Accepts(Gold)=False
声明行 B = res_food     ⇒ Accepts(Stone)=False Accepts(Food)=True  Accepts(SpecialFood)=True Accepts(Meat)=True
实投：A.Add(Stone,7)=7；A.Add(Food,7)=0（标签不匹配 ⇒ 拒收）  B.Add(Stone,7)=0；B.Add(Food,7)=7
```
⇒ 收什么**完全由一行声明数据决定**；`StorageComponent` 代码零分支，`def.outputResource` 不参与容器类型。

### 判据 2 · 一仓装多资源、共占一条容量线

```
仓：声明 res_material、capacity=20
投入 Stone5/Wood4/Ore3 ⇒ 实际入仓 5/4/3
GetAmount(Stone)=5  GetAmount(Wood)=4  GetAmount(Ore)=3
TotalCount=12  UsedSpace=12  FreeSpace=8  capacity=20   ⇒ 三者共占同一条容量线
Query() 条目数=3 ⇒ Stone×5, Wood×4, Ore×3
```

### 判据 3 · 容量按体积算

```
容量 10 ＋ 石材体积=1 ⇒ Add(Stone,10)=10；再加 Add(Stone,1)=0（已满拒绝）；CanAccept(Stone)=0；UsedSpace=10
体积 0 资源（金币=体积 0）⇒ 满仓仍可入 Add(Gold,999)=999；UsedSpace 仍=10（不占容量）
口径：CanAccept = floor(剩余容量 ÷ 体积)；体积 0 ⇒ int.MaxValue
```
⚠️ **如实记局限**：资源表体积初值**全 1、仅金 0**（`09` §4.2 已定 · 数值批可调）⇒ 现网**无体积=3 的资源样本**，「容量 10 ÷ 体积 3 = 3 个」无法实测；上式已由 体积=1（10÷1=10）与 体积=0 两端点读数夹住。

### 判据 4 · 前缀可读

```
仓内 Stone5/Wood4/Ore3/Food6
SumByPrefix("res_material")=12   （＝石5＋木4＋矿3，一次拿到子树合计）
SumByPrefix("res")         =18   （全部）
SumByPrefix("res_food")    =6
GetAmount(Stone)=5  GetAmount(Ore)=3
段边界反例 SumByPrefix("res_mat")=0   （段未对齐 ⇒ 0，`09` §3.2）
```

### 判据 5 · 旧结构引用清零（grep 原文，`Assets/**`）

`grep -n "struct ResourcePack|ResourcePack\s+\w|ResourcePack\.Zero|legacyCost|legacyUpgradeCost|legacyCastleUpgradeCosts|legacyBaseStockpile|legacyStockpile|legacyCatapultCost|legacyBallistaCost|legacyMortarCost|legacyVineCatapultCost|legacyRamCost"`

```
（*.cs：0 命中代码；.asset：0 命中）
命中 3 行，全部为「历史叙述型注释」，无代码/无数据：
  Assets/Editor/ChainAudit/Validators/R3_SupplyChain.cs:182   // ⭐ M1-A 适配：ResourcePack 八桶读数 → ResourceList.Get(type)
  Assets/_Game/Data/ResourceCatalog.cs:278                    // ===== 旧 ResourcePack 的算子接班人（语义逐条对齐，保迁移期读数一致）=====
  Assets/_Game/Systems/Disaster/Data/MonsterDef.cs:26         [Tooltip("…旧 8 桶「未映射槽回退 Food」限制已随 ResourcePack 退役消失")]
```

`grep -n "storage\.(resourceType|storedAmount)|\.storedAmount|\.resourceType\b"`（`*.cs`）

```
Assets/_Game/Data/ResourceCarryConfig.cs:27     entries[i].resourceType   ← 另一类型（ResourceCarryConfig 条目字段），非 StorageComponent
Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs:617/623/625/629/701/703/706/890/948/1066
                                                ← 全部是 ScaleTaskArgs/GatherTaskArgs 的 resourceType 字段，非 StorageComponent
Assets/Editor/Smoke/Valley_HH107_Smoke_Byproduct.cs:182  ← ScaleTaskArgs.resourceType
Assets/Editor/Smoke/Valley2_17_Smoke_FixCard.cs:15       ← 注释里的旧字段名
⇒ StorageComponent.resourceType / .storedAmount 全库 0 命中
```

### 判据 6 · 新档能存能读

```
存档前 storageContents = 石材×7, 木材×3, 矿石×2, 石头弹×1
json = {"defId":"Warehouse",…,"storageContents":{"items":[{"type":1,"amount":7},{"type":2,"amount":3},{"type":4,"amount":2},{"type":10,"amount":1}]},
        "treasuryContents":{"items":[{"type":0,"amount":123},{"type":9,"amount":9},{"type":5,"amount":4}]},…}
读回 storageContents  = 石材×7, 木材×3, 矿石×2, 石头弹×1    逐项一致：storage=True
读回 treasuryContents = 金币×123, 金属×9, 水晶×4             逐项一致：treasury=True
读档回灌（RestoreContents）⇒ 条数=4  TotalCount=13  UsedSpace=13/50  文本=石材×7, 木材×3, 矿石×2, 石头弹×1
空仓往返：items==null？False；条数=0    ⇒ 全 0 ＝ 空列表（⛔ 非 null）
```

### 附 · 资源表落码

```
ResourceCatalog 落码行数 = 13（与 ResourceType 枚举现值 1:1；枚举项数 = 13，未落码 0 项）
Gold/res_currency.gold  Stone/res_material.stone  Wood/res_material.wood  Ore/res_material.ore  Metal/res_material.metal
Crystal/res_material.crystal  FireOil/res_material.fireoil  Food/res_food.grain
SpecialFood/res_food.special  Meat/res_food.meat  StoneAmmo/res_ammo.stone  FireballAmmo/res_ammo.fireball  MagicAmmo/res_ammo.magic
```

---

## 四、五条落码口径落实

| 口径 | 落实 |
|---|---|
| 补-1 `IWarehouse` 只需 `Query` 改 | ✅ 采纳（`CanTake/Take/Deposit/Transform` 签名一字未动） |
| 补-2 `FindNearestAvailable` 判据改 `CanAccept` | ✅ 已改，签名不变（`WarehouseRegistry.cs:66`） |
| 补-3 迁移按「缺省＝0」「全 0 ⇒ 空列表」 | ✅ 已按此写；核对表 0 不一致 |
| 补-4 交付物增列「资产迁移前后逐项核对表」 | ✅ §二-3（88 行 · `M1A_资产迁移核对表.txt`） |
| 补-5 `WorkerInventory` 过渡态显式标注 | ✅ 类注释已标；单资源语义假设点清单见 §五-3 |

### Q2 硬约束核对（`TaskScheduler` 纯读取点一行不动）

> ⚠️ **补正（`D790` 勘-1 · 2026-09-20）**：原文写「七处 `:733/:734/:737/:744/:749/:800/:806`」——**行号全偏移**（漂移因上文插注释）。磁盘实读 `inv.carriedType` 共 **8 处**：`:734/:736/:739/:742/:746/:751/:802/:808`；其中 **`:742` `best.Add(inv.carriedType, amount)` 改前为无参单资源版 `Add(amount)` ⇒ 属存储侧调用点、必然改签名**，不属 Q2 点名的「纯读取七处」。⇒ **采信实质不变：纯读取七处一行未动 ✔**（本条入台账作「引述行号须复读」实例）。

- `WorkerInventory` **保留** `carriedType`／`carriedAmount` 及自有 API（`UnloadAll`／`IsEmpty`／`IsFull`／`GetCarryCapacity`／`TryStore`）；
- **只改** `Query()` ⇒ `List<ResourceAmount>`（至多 1 条非空）＋ `Deposit` 返回值 `void ⇒ int`（= `TryStore` 返回值）；
- 策划端点名的七处**纯读取** `inv.carriedType`（`TaskScheduler.cs` · **勘正后行号 `:734/:736/:739/:746/:751/:802/:808`**）**一行未动** ✔；第 8 处 `:742` 属存储侧（见上注）；
- ⚠️ **另需说明**：`TaskScheduler` 内**非** `inv.carriedType` 的存储侧调用点（原 `:707/:709/:712/:771/:778/:782/:819/:891/:892`）因 `StorageComponent` 换签名**必须**改，已按最小面改写（详见 §五-3 的过渡读口注释）。这类点不在 Q2 点名的七处之列，未触发「停下报」条款。

---

## 五、需策划端知晓 / 确认

### 5-1 ⚠️ **追加迁移（裁决未列 · 「不动行为」必需）** —— `BuildingDef.warehousePaths`

> ✅ **补正（`D790` 判-1／勘-2／勘-4 · 2026-09-20）**：本处两问已裁 —— **判-1 裁 A（认可追加迁移）**；`Well` 归 **勘-2**（死仓 ⇒ 7 栋改述为「6 必需 ＋ 1 无害」）；`Well.outputResource=Gold` 归 **勘-4**（既有占位约定，**非**内容缺陷）；`Warehouse.outputResource=Wood` 归 **判-2**（**判为真内容缺陷**，归内容批改那一行数据）。

- **事实**：改前 `StorageComponent.Init` 由 `def.outputResource` 决定容器类型（例如 `quarry→Stone-only`、`Granary→Food-only`、`Warehouse→Wood-only`、`Well→Gold`）；改后由 `def.warehousePaths` 决定。新字段**旧值无同名载体**（`outputResource` 不是被改名，而是**语义被新字段接管**）。
- **风险**：若留空 ⇒ 全库建筑变通用仓 `res`（**行为漂移**：石仓也收弹药/肉等；`WarehouseRegistry` 就近卸货落点全变）。
- **本端处置**：在同一个一次性工具里按旧判据（**逐字复刻** `BuildingFactory.AttachComponents` 的挂仓条件）**读旧填新**：`warehousePaths = [PrimaryPathOf(def.outputResource)]` ⇒ **行为逐字不变**。实填 **7 栋**（**勘-2 勘正后口径 ＝ 6 栋行为必需 ＋ 1 栋无害死仓**）：`Warehouse→res_material.wood`、`Granary→res_food.grain`、`AdvancedStorage→res_material.wood`、`Blacksmith→res_material.metal`、`farm→res_food.grain`、`quarry→res_material.stone`（**以上 6 栋：不定行为即漂移**）、`Well→res_currency.gold`（**无害死仓**，见下勘-2 补注）（余下 33 栋按旧判据本就不挂 `StorageComponent`：`mine/ore_vein/farmland/tree` 是 `isResourceNode`、`SiegeWorkshop` 走专属弹药子仓，其余 `rate=0 且非 Economy 仓`）。
- ⚠️ **勘-2 补注（`Well` 是死仓 · 实读 `ProducerComponent.cs:68-75`）**：`_isWell`（`:39` ＝ `def.id == "Well"`）时 **`Tick()` 早返回 `TickWaterToNetwork`**（`:73-74`），**永不进入 `_storage.Add`**（`:96`）；且 `:44-47` 的 `outputResource==Gold` 产金分支已退役（`_rate = 0f`）。⇒ `Well` 仓**改前改后皆无写入**：填 `res_currency.gold` **无害**，但把它与另外 6 栋并列为「不定行为就会漂移」**定性偏高** ⇒ 已按 **6 必需 ＋ 1 无害死仓** 改述。
- **❗请裁决** → **✅ 判-1 裁 A（认可）**：(A) 认可「先按旧单资源语义落，内容批再放宽为分类仓」；或 (B) 本片直接把 `Warehouse→res_material`、`Granary→res_food` 等**内容级合理标签**一次给全（＝本片引入内容设计决策）。
  本端**按 (A) 执行**（保「只改结构」红线）；策划端独立复算**逐栋相符**，且 33 栋空声明按旧条件本就不挂仓 ⇒ **无行为漂移**。
- ⚠️ **顺带发现（旧数据怪点 · 本端原样保留未改）**：`Warehouse.asset` 的 `outputResource = Wood(2)`、`Well.asset` 的 `outputResource = Gold(0)` ⇒ 旧口径下「通用仓库只收木」「水井的仓只收金」。本片按"不动行为"原样迁成 `res_material.wood`／`res_currency.gold`。
  - **✅ 勘-4（`Well` 一问 · 已由既有注释解答）**：`ProducerComponent.cs:15`／`:38` 明载「well.asset outputResource=Gold 占位，实际产水入网」（QQQ.2 T15）⇒ **不是内容缺陷，是既有占位约定**；占位值清理挂**内容批**（`10` 能力表 `store` 能力落地时）。
  - **✅ 判-2（`Warehouse` 一问）**：**判为真内容缺陷** —— `Warehouse` 定位＝通用仓库，`outputResource=Wood` 使它变成「只收木」（`09` §5.3 目标态应为 `res_material`／`res`）⇒ 归**内容批**改那一行数据（**不牵动代码** —— 正是判据 1 的收益兑现）。

### 5-2 ⚠️ **结构强制的行为变化**（非主动改行为 · 依纪律上报）

| 点 | 改前 | 改后 | 性质 |
|---|---|---|---|
| `TreasureVault.SpillToChest`（[TreasureVault.cs:105-124](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L105-L124)） | 8 桶：特食/肉装箱**按粮折算**、水晶/火油**无桶 ⇒ 丢弃** | 单条目**原值原类型**落箱；水晶/火油亦可入箱 | 旧"折损/丢弃"**结构性消失**（`ResourcePack` 无桶所致） |
| `MonsterController.BuildLootPack`（[MonsterController.cs:73-77](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Disaster/MonsterController.cs#L73-L77)） | 8 桶：`Ore/Crystal/FireOil/特食/肉`**无承载槽 ⇒ 回退 Food**（有损映射） | 直接承载 `def.lootResource` 实际类型 | 有损映射**结构性消失** |
| `WarehousePanel` 资源行 | 每仓 1 行（单资源） | 逐「本仓能装的资源」1 行 ⇒ 通用仓 `res` 会展开 13 行 | 结构性 UI 噪点；内容批给仓储定位后自然收敛 |
| `KingdomIntelPanel`/`BuildingPanel` 存储文本 | `名称 数量/容量` | `Contents` 文本 ＋ `UsedSpace/capacity` | 多资源下单资源文本无法表达，被迫改写 |

### 5-3 「单资源语义假设点」清单（Q2 交付项 · 供 `M1-F`／`M1-G` 收口）

| # | 位置 | 假设 | 归属 |
|---|---|---|---|
| 1 | `StorageComponent.GetCarryAmount()`／`PrimaryStoredType()`／`HarvestCarry()` | 取「首个非空资源」代表整仓 | `M1-G`（删 `HarvestCarry` 直通） |
| 2 | `TaskScheduler.LoadInventoryFromSource`（[:703-715](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L703-L715)） | 标签判 `Accepts` ＋ `PrimaryStoredType()` 搬一次；`TakeOut(type,…)` | `M1-G` |
| 3 | `ScheduleCenterStub.Tick`（[:119-149](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/Schedule/ScheduleCenterStub.cs#L119-L149)） | 分批数用 `TotalCount`、携带量/日志用 `PrimaryStoredType()` | `M1-G` |
| 4 | `WarhousePanel.RebuildStorageList` | 逐「可装资源」汇总（单资源仓恰 1 行） | `M1-B` 账本读口后重评 |
| 5 | `Building.TryAdvertiseTask` ②③ | 满判 `IsFullFor(producer.OutputResource)`；取量 `PrimaryStoredType()` | `M1-G` |
| 6 | `WorkerInventory`（`carriedType`/`carriedAmount`） | 单资源背包（`Query()` 至多 1 条） | **`M1-F`** |
| 7 | `TaskScheduler` **8 处** `inv.carriedType`（勘-1 勘正：7 纯读取 ＋ 1 存储侧） | 单资源卸货路由 | **`M1-F`** |
| 8 | `UnitController` v5 存档代理（两标量） | 单资源存档 | **`M1-F`** |
| 9 | `MineByproductComponent` 三子仓 / `SiegeWorkshopBuilding` 弹药子仓 | 各以**专属单资源声明**表达"只收这一种"（合法写法，非缺陷） | 保持 |
| 10 | `TreasureVault.SpillToChest` | 单条目落箱 | `M1-D` |
| 11 | `MonsterDef.lootResource` 单值 | 单资源掉落 | `M1-D` |

---

## 六、未完成项（显式列出 · 含归属子片）

| # | 未完成项 | 归属 | 说明 |
|---|---|---|---|
| 1 | `WarehouseHelper` 金仍走 `RulerController` 直通（不落仓） | `M1-E` | 本片行为不变，仅类型换代 |
| 2 | `RulerController` 金字段/直通分支未退役；只读门面未降级 | `M1-E` | 同上 |
| 3 | `Transform` 未改走 `RecipeCatalog`（`RecipeCatalog.Table` 为空） | `M6-B` | 本片只建表（`09#46`） |
| 4 | 资源表第 14 项 `res_consumable.medkit` 未落码 | `M6`（`D789` Q3=A） | 避免孤儿资源告警 |
| 5 | 体积数值（全 1／金 0）未调 | 数值批 | `09` §4.2 已定初值 |
| 6 | `HarvestCarry`／`GetCarryAmount` 过渡读口未删 | `M1-G` | 删则牵动 `TaskScheduler:711` 等 |
| 7 | `WarehouseRegistry` 未 per-kingdom 化（仍按 `kingdomId` 参数过滤） | `M1-B` | 判据未要求 |
| 8 | 掉落箱 `Faction` 参数语义未改（箱应无主） | `M1-D` | `09#57` |
| 9 | 战利品 ×1.5 判据 / 随机金 / 日志判据 | `M1-D` | `09#58/#59/#60` |
| 10 | 升级/修复/拆除未全走仓 | `M1-C` | `09#64` ＋ `#53~#56` |
| 11 | `WorkerInventory` 多资源化 ＋ 卸货路由 ＋ v5 存档代理 | `M1-F` | `D789` Q2 明确划出 |
| 12 | 水按资源处理（普通仓＋整数化） | `M1-F` | `09#44` |
| 13 | **未跑 in-game 长局/冒烟** | — | 本片只改结构 ⇒ 判据 1~4/6 用 Edit Mode 单元级读数；未进 `GameScene`、未走 `TestHarnessApi`。**⇒ `D790` §五 已裁：本片不另派冒烟，合并到 `M1-B` 跑一次同局冒烟**（那时才有账本读口可验） |
| 14 | `Assets/Unity.VisualScripting.Generated/…/UnitOptions.db` 因 `ResourcePack` 重载退役而陈旧 ⇒ 已执行 `UnitBase.Rebuild()` 重建（105s），Console 已清 | 收尾 | 非代码问题 |

---

## 七、红线遵守自检

| 红线 | 状态 |
|---|---|
| ⛔ 不碰 `GridTypes.cs`（`HH.293` 正在改） | ✅ 未碰（`git status` 无该文件） |
| ⛔ 不碰地图四门（`03` Place/Remove/Replace/Set） | ✅ 未碰 |
| ⛔ 不做存档迁移脚本（旧档可作废） | ✅ 未写；`ChestSaveData`/`BuildingSaveData` 直接换代 |
| ⛔ 不写双轨开关（一次切到位） | ✅ 无任何 `useXxx` 开关；旧结构**已删净**（非保留） |
| ⛔ 不碰训练仓（`ai决策大脑强化训练/`） | ✅ 未碰 |
| 本片只改结构；遇「不动行为做不下去」⇒ 停下报 | ✅ 见 §五-1／§五-2（**已报，未自选**） |
| ⛔ 不许「既保留旧结构又新增新结构」 | ✅ 迁移三步法走完，旧字段/旧类型已删 |
| 账本 | ✅ 未碰四档账本（策划端落） |
