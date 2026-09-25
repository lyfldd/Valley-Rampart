# `HH.335` `M4-C` 收尾小批 · 两空类摘除 交付报告

- **编号**：**`HH.335`**（对 `_编号登记.md` 在途行另取 · 先登记后落盘 · 水位线 334→335）
- **依据**：承 **`D867`**（`HH.334` 已验收）后签发 · 本批任务四条（用户口径）
- **性质**：施工（改动面**恰三文件**：`BuildingComponents.cs`／`Valley_HH329_M4AProbe.cs`／`BuildingMappingTable.cs`）
- **基线**：`6f1da5e9`（含 `21adc832`／`D867` 验收 `HH.334`：四栋资产与映射表三行**已入基**）
- **日期**：2026-09-25
- **⛔ 未 commit／未 push · ⛔ 未动 `GameScene.unity`**

---

## 一、改动清单（file:line ＋ numstat）

### 1) `Assets/_Game/Systems/Building/BuildingComponents.cs` —— **`3 +/18 -`**

| 动作 | 原锚点 | 现文 |
|---|---|---|
| **删 `SpawnerComponent` 类**（含类注释） | 原 `:35-39`（`// ===== 留接口空壳组件 =====` 段内） | 段内现只剩 `PickupComponent`（`:29-33`）＋`CombatComponent`（`:35+`） |
| **删 `RiftComponent` 类**（含类注释） | 原 `:161-165` | `CombatComponent` 之后直接接 `CastleCoreComponent`（`:155-168` 一带） |
| **删键常量 `Rift = "comp.rift"`** | 原 `:202` | 键表 **9 → 8**：`:183-190`＝`storage`／`producer`／`blacksmith`／`siege_workshop`／`mine_byproduct`／`combat`／`pickup`／`castle_core` |
| **删 `Register(Rift, …)`（2 行）** | 原 `:220-221` | 注册表 **9 → 8**：`:196-207`（7 条直挂 ＋ `CastleCore` 来源守卫） |
| **改注释**「⚠️ **以下两个键**保「来源守卫」…改前**这两项**的判定…这两条路径…」 | 原 `:216-219` | ⭐ 现为**只描述 `castle_core`**：「⚠️ **本键**保「来源守卫」…其判定读的是 `b.sourceType`…」（`:203-206`） |

### 2) `Assets/Editor/Smoke/Valley_HH329_M4AProbe.cs` —— **`1 +/5 -`**（⛔ 探针文件**未删**）

| 动作 | 位置（现行） |
|---|---|
| 键→类名映射表删 `{ "comp.rift", "RiftComponent" }` 行 | 现 `:33-34`＝`comp.pickup`／`comp.castle_core`（8 行） |
| `OldTypes`（改前 9 处 `if` 复刻）删 `if (src == BuildingType.Rift) l.Add("RiftComponent");` | 现 `:191` 即 `CastleCore` 分支（Rift 分支已去） |
| `RowTypes` 删 `if (k == BuildingComponentRegistry.Rift && src != BuildingType.Rift) continue;`（该常量已删） | 现 `:204` 即 `CastleCore` 守卫（唯一条） |
| `RunOldChain` 删 `if (src == BuildingType.Rift) b.gameObject.AddComponent<RiftComponent>()?.Init(b);` | 现 `:236` 即 `CastleCore` 实挂 |
| 注释「（＋ **两个**来源守卫）」→「（＋ 来源守卫）」 | 现 `:195` |

（探针其余面未动：`CastleCore` 分支·`PickupComponent` 分支·`KeyType.Count` ⇄ `BuildingComponentRegistry.Count` 对拍行均在。）

### 3) `Assets/_Game/Data/BuildingMappingTable.cs` —— **`2 +/2 -`**

- `:11` 文档注释：「**11 种** `BuildingType` 各对应一个 `BuildingDef`…」→「**8 条** …」
- `:16` `[Tooltip]`：「**11 种** `BuildingType` → `BuildingDef` 映射…」→「**8 条** …」

---

## 二、判据 1~5 逐条读数

| # | 判据 | 读数 | 结论 |
|---|---|---|---|
| 1 | `Assets/_Game` 生产码 `SpawnerComponent`／`RiftComponent` 均 0 命中；`Resources/Buildings` `comp.rift` 0 命中 | 全库 **`Assets`** 面（含 Editor）两类名 **0 命中**；`_Game` 面分别 0／0；`comp.rift` 在 `Resources/Buildings/*.asset` **0** | ✅ |
| 2 | `ore_vein`／`stone_pile`／`wood_pile` 仍各 1 行 `comp.pickup`；`PickupComponent` 类在 | 三者各 **1 行**；`class PickupComponent` **1 处**（`BuildingComponents.cs:30`）·`Register(Pickup, …)` 在（`:202`）·`const Pickup` 在（`:189`） | ✅ |
| 3 | `CastleCore` 的登记还在；`BuildingFactory.cs:69` 仍是 `Get(CastleCore)` | `const CastleCore`（`:190`）＋`Register(CastleCore, …)`（`:207`）俱在；`BuildingFactory.cs:69` ＝ `var castleDef = table.Get(BuildingType.CastleCore);` | ✅ |
| 4 | `BuildingMappingTable.cs` 不再写「11 种」 | 「11 种」**0 命中**；「8 条」**2 处** | ✅ |
| 5 | 编译 0 error | 编译读数 `errors=0`（`warnings=1`）；⭐ **编译产物晚于源码**：`Assembly-CSharp.dll` **10:09:43**／`Assembly-CSharp-Editor.dll` **10:09:47** ＞ 源码 mtime（`BuildingMappingTable.cs` 10:09:21／`BuildingComponents.cs`·探针 10:09:30）⇒ 本批改动**已被编译**；控制台 error 面仅 **1 条包噪声**（见列报 2） | ✅ |

**行尾**：三文件 `git ls-files --eol` 均 `i/lf w/lf`（保持 LF·未引入 CRLF）。

---

## 三、停手核对：**未命中**

- 本批开工前实核：`SpawnerComponent`／`RiftComponent` 的生产码引用**只在** `BuildingComponents.cs`（4 处：两处类定义＋常量＋注册）；`Assets/Editor` 侧仅 `Valley_HH329_M4AProbe.cs`（本批任务书**明示要改**）。
- ⇒ 「除 `BuildingComponents.cs` 与 `Valley_HH329_M4AProbe.cs` 外还有生产码引用」**不成立** ⇒ 未停手。

---

## 四、列报（2 条 · ⛔ 均未动）

| # | 事项 | 事实 |
|---|---|---|
| 1 | **文件头注释漂移**（`BuildingComponents.cs:10`） | 类头注释仍写「留接口空壳：`Pickup`/`Spawner`/`Combat`/`Rift`/`CastleCore`（定义类 + 挂载判断…）」—— `Spawner`／`Rift` **已删** ⇒ 该枚举成陈（⛔ 本批未授权改注释以外内容·未动·仅供下批一并校） |
| 2 | **编辑器包噪声 1 条**（⛔ 非本批代码面） | 控制台 1 条 `Exception`：`285 node options failed to load and were skipped.` —— 来源 `./Library/PackageCache/com.unity.visualscripting@1.9.4/Editor/VisualScripting.Flow/Options/UnitBase.cs:131`（**Visual Scripting 包自身**·推测由资产删除后的重导入触发·**非 CS 编译错误**）。读数存 `Logs/hh335_console.log` |
| 3 | **探针 id 表延续列报**（`Valley_HH329_M4AProbe.cs` Part 1b） | `:79` id 数组仍含 `"rift"`／`"portal"`（两 def 已于 `HH.334` 删除）⇒ 该段现走「未找到」分支（本批任务只要求"不引用已删类型"·该 id 表属字符串面·**未动**·延续 `HH.334` 列报） |

---

## 五、工具面 ＋ 零外溢声明

- **通道**：`mcp_unity-bridge` → `manage_editor request_compile`（编译·阻塞至完成）＋ `mcp_unityMCP` → `read_console`（控制台 error 面复核）。调用器：`Logs/_bridge_call.ps1`／`Logs/_mcp_call.ps1`。
- ⚠️ **schema 口径记**：`read_console` 的 `types` 只接受 `error`／`warning`／`log`／`all`（传 `exception` 被拒 ⇒ 该条 Exception 需在 `error` 档下取，实读已取到）。
- **`Assets` 面改动清单（本批）**：`M BuildingComponents.cs`／`M Valley_HH329_M4AProbe.cs`／`M BuildingMappingTable.cs`；**前序脏点**（非本批）＝`M GameScene.unity` ＋ 未跟踪美术/探针若干。
- ⛔ 未动：`PickupComponent`／`comp.pickup`／`Register(Pickup)`／三栋资源点 `comp.pickup`／`CastleCoreComponent`／`FoodWorkshop`／`Ranch`／`BuildingType` 枚举／`BuildingVisual.cs` 的 `BuildingType` 三 case／`ChestEntity`／`Portal.cs`／`bld_ruins`／`feat_treasure_box`／`GameScene.unity`。
- ⛔ 未 commit／未 push。
