# `HH.334` `M4-C` 四栋空资产连映射行一起摘 交付报告

- **编号**：**`HH.334`**（对 `_编号登记.md` 在途行另取 · 先登记后落盘 · 水位线 333→334）
- **依据**：承 **`HH.333`／`D866`**（任务书 `多Agent交接/策划端/HH.333_M4-C四栋空资产_任务书.md`）
- **性质**：施工（改动面**仅**任务书列的三处：4 资产删除 ＋ 映射表三行 ＋ `BuildingVisual.cs` 四条 case）
- **基线**：`e7bb8696`（施工前 `Assets` 面唯一脏点＝前序 `GameScene.unity`）
- **日期**：2026-09-25
- **⛔ 未 commit／未 push**

---

## 一、改动清单（file:line ＋ 证据）

| # | 目标 | 动作 | 证据（读数载体） |
|---|---|---|---|
| 1 | `Assets/Resources/Buildings/rift.asset`＋`.meta`<br>`.../portal.asset`＋`.meta`<br>`.../ruins.asset`＋`.meta`<br>`.../treasure_box.asset`＋`.meta` | **删除**（编辑器侧 `AssetDatabase.DeleteAsset`，逐条含 `.meta`） | 逐条 `DeleteAsset=True`；删后 `asset=False meta=False`（`Logs/hh333_apply.log`）；`Resources/Buildings` 资产数 **41 → 37**；`git status` 显示 4 个 `D` ＋ 4 个 `.meta` `D` |
| 2 | `Assets/Resources/Buildings/BuildingMappingTable.asset` | **摘 `type 7`／`type 8`／`type 10` 三条**（`type 1–6、11、12` 保留） | `git diff --numstat`＝**`0 +/6 -`**；YAML 实读现 **8 条**：`type 1/2/3/4/5/6/11/12`，其中 **`type 11` → `castle.asset`**（guid `b0d1ffe0afa9a6644a4c18722efc0c67`）；`Resources.Load<BuildingMappingTable>` 复核 `True`·`entries=8`。**经 Unity 改**（`LoadAssetAtPath` → `entries` 重排 → `SetDirty` → `SaveAssets`·⛔ 未手改 YAML） |
| 3 | `Assets/_Game/Systems/Building/BuildingVisual.cs` | **删 def id switch 四条**：`:56 case "portal"`／`:57 case "rift"`／`:69 case "ruins"`／`:70 case "treasure_box"` | `git diff --numstat`＝**`0 +/4 -`**；行尾 `git ls-files --eol`＝`i/lf w/lf`（**保持 LF**）；现 def id switch 至 `:66 case "wood_pile"` 收尾；`BuildingType` switch 顺移为 `:78 TreasureBox`／`:79 Ruins`／`:80 Rift`（**保留**） |

---

## 二、判据 1~4 逐条读数

### 判据 1 —— 四路径不存在 ＋ 映射表不再出现四个 guid ✅
- 四路径：`rift.asset`／`portal.asset`／`ruins.asset`／`treasure_box.asset` 与各自 `.meta` **双 False**（磁盘 `Test-Path` 复核）。
- 映射表文本：`4cf205a2…`（treasure_box）＝**False**／`5f0f3cef…`（ruins）＝**False**／`3625478e…`（rift）＝**False**／`52463a62…`（portal）＝**False**；对照 `b0d1ffe0…`（castle）＝**True**。
- ⭐ 强判据：`Assets` 面 **非 `.meta` 文本扫描 673 文件 · 四 guid 命中 0**（`HH.332` 时为 3 命中，全部在映射表）。

### 判据 2 —— `type 11` 仍在 ＋ `BuildingFactory.cs:69` 不变且唯一生产调用 ✅
- 映射表 `entries[6] type=11 (CastleCore) → Assets/Resources/Buildings/castle.asset`（编辑器实读）。
- `BuildingFactory.cs:69` 仍为 `var castleDef = table.Get(BuildingType.CastleCore);`（`:58` `GetMappingTable()`·`:25/:31` `Resources.Load`）。
- 全库 `BuildingMappingTable` 检索命中 5 处：`BuildingFactory.cs:18/25/28-33/58/69`（生产）＋`BuildingMappingTable.cs:7-16`（类定义/注释）＋`BuildingDef.cs:9`（注释）⇒ **`.Get(...)` 生产调用唯一＝`:69`**。

### 判据 3 —— 不许动清单全在 ✅

| 项 | 读数 |
|---|---|
| `ChestEntity.cs` | 在场；`:6` 注释提及 `"treasure_box"` sprite（**未动**） |
| `ChestManager.cs`／`Portal.cs`／`TreasureVault.cs` | 在场（未动） |
| `Building.cs:782` 的 `bld_ruins` | 在场（未动） |
| `PlaceholderSprites` 的 `bld_ruins`／`bld_portal`／`feat_treasure_box` | 在场（未动） |
| `FoodWorkshop.asset`／`Ranch.asset`／`Module_Production.asset` | 在场（未动） |
| `SpriteRefTable.asset`／`GameScene.unity` | 在场（后者仍为**前序脏点**·本批未动） |
| `RiftComponent` 类 | `BuildingComponents.cs` `class RiftComponent` **1 处** |
| `PickupComponent`／`SpawnerComponent`／`BuildingComponentRegistry` | 在场（未动） |
| `ore_vein.asset` 的 `comp.pickup` | **1 处**（`stone_pile`／`wood_pile` 同未动） |

### 判据 4 —— def id switch 四 id 零残留 ＋ `BuildingType` 三 case 在 ✅
- def id switch：`case "portal"`／`"rift"`／`"ruins"`／`"treasure_box"` **全库生产码零命中**（`_Game/**` 仅 `ChestEntity.cs:6` 注释提及 sprite 名）。
- `BuildingType` switch：`case BuildingType.TreasureBox`（`:78`）／`Ruins`（`:79`）／`Rift`（`:80`）**俱在**；枚举 `GridTypes.cs:139/140/143` 未动。
- 附：全库 `BuildingType.(Rift|Ruins|TreasureBox)` 余 4 处生产引用（`BuildingVisual.cs:78-80` ＋ `BuildingComponents.cs:220` 的 `comp.rift` 来源守卫）—— 均在"不许动"面内。

### 编译
**0 error**（桥 `manage_editor request_compile` 阻塞至完成：`compile_observed=true`·`errors=0`·`warnings=15`）—— 15 条 warning 全为**既有**（`CS0114/CS0108/CS0252/CS0253/CS0414/CS8632`），**无一条落在本批改动面**。

---

## 三、停手核对：**未命中**（可施工）

删前编辑器复核（`Logs/hh333_pre_refscan.log`·法＝引用数据库反向扫描 `Assets` 面实扫 1224）：

| 目标 | 引用方 | 明细 |
|---|---|---|
| `rift.asset` | **1** | `Assets/Resources/Buildings/BuildingMappingTable.asset` |
| `portal.asset` | **0** | — |
| `ruins.asset` | **1** | `BuildingMappingTable.asset` |
| `treasure_box.asset` | **1** | `BuildingMappingTable.asset` |

`仅映射表引用 = True`（⛔ 无第三引用方）＋ 开放场景 `GameScene`（组件 140／依赖 353）**目标命中 0** ⇒ 停手条件（存在映射表以外的引用方）**不成立**。

---

## 四、列报（3 条 · ⛔ 均未动）

| # | 事项 | 事实 |
|---|---|---|
| 1 | **探针面残留 2 处**（`Assets/Editor/**`·⛔ 不在本批改动面） | `Valley_HH329_M4AProbe.cs:80`（id 数组含 `"rift"`／`"portal"` ⇒ 该探针现走 `FindDefById` 未找到分支）／`Valley_HH294_Slice6PickProbe.cs:463`（排除表含 `"rift"`／`"portal"` ⇒ 现恒不匹配；桥/门项仍在） |
| 2 | **注释／文案漂移 2 处**（`BuildingMappingTable.cs`·⛔ 未授权改） | `:11` 文档注释「**11 种** `BuildingType` 各对应一个 `BuildingDef`」、`:16` `[Tooltip]`「**11 种** …映射」—— 现为 **8 条** |
| 3 | **名字级别名未动**（`ChainAudit`） | `R5_SixStage.cs:246` `{ "treasure_box", "ChestEntity" }`（⛔ 指实体类·非本资产引用·未动） |

**附事实（登记·不判）**：摘除 `type 7/8/10` 后，`BuildingType.TreasureBox`／`Ruins`／`Rift` 在映射表内**无对应条目**；代码侧 `table.Get(...)` 生产调用只有 `CastleCore` 一条（判据 2 读数），枚举与 `BuildingType` switch 分支保留。

---

## 五、工具面 ＋ 零外溢声明

- **走的编辑器通道**：`mcp_unity-bridge` → `execute_csharp_script`（编辑器内 Roslyn·施工与复核）／`manage_editor request_compile`（编译）；`mcp_unityMCP` 用于 `read_console` 等价面（探活 `/health` 200 · v10.2.0）。调用器：`Logs/_bridge_call.ps1`、`Logs/_mcp_call.ps1`。
- **读数落盘**：`Logs/hh333_pre_refscan.log`（删前复核）／`Logs/hh333_apply.log`（施工＋复核）／`Logs/_hh333_*.cs`·`*_payload.json`（片段与载荷）。
- **`Assets` 面改动清单（11 项）**：`M BuildingMappingTable.asset`／`M BuildingVisual.cs`／`D`×4 资产／`D`×4 `.meta`／`M GameScene.unity`（**前序脏点·非本批**）。
- ⛔ 未动：`GameScene.unity`、`Packages`、美术未跟踪文件、其余任何生产码。
- ⛔ 未 commit／未 push。

## 策划裁决（D867）

验收通过。组件类未删，不另派。全文：`多Agent交接/策划端/HH.334_M4-C四栋空资产_验收裁决.md`。
