# `HH.332` `M4-C` 引用核对 交付报告（**编辑器侧 · 只读批**）

- **编号**：**`HH.332`**（对 `_编号登记.md` 在途行另取 · 先登记后落盘 · 水位线 331→332）
- **依据**：承 **`D865`**（`HH.331` 实核后裁）
- **范围**：**只四栋** —— `Assets/Resources/Buildings/rift.asset`／`portal.asset`／`ruins.asset`／`treasure_box.asset`。⛔ `FoodWorkshop`／`Ranch` **不在本批**（`D865` 已划出）。
- **性质**：只读（⛔ 零生产码 · ⛔ 零资产删除 · ⛔ 不改 `08` · ⛔ 不改《中层执行计划》 · ⛔ 未 commit／未 push）
- **基线**：`e7bb8696`
- **日期**：2026-09-25
- **⛔ 本报告不写「删／不删」**（停手 1 未触发：未出删除方案）。

---

## 〇、用的编辑器命令 ＋ 读数总表

**主判据（法一·引用数据库反向扫描）**：`mcp_unity-bridge` → `execute_csharp_script`（桥内、编辑器内执行，`Unity=2022.3.62t7`·`isPlaying=False`）：
`AssetDatabase.GetAllAssetPaths()` × 逐个 `AssetDatabase.GetDependencies(path, false)`，命中判定＝依赖路径 ∈ 四目标。
- **分母**：候选 **9816**（其中 `Assets/` 面 **1228**＝实扫 **1224** ＋ 四目标自身 4）／跳过 **8592**＝非 `Assets/` 面 8588 ＋ 四目标 4 ⇒ **`Assets/` 面零异常跳过，全扫**；依赖边合计 978。
- 第二法（同一次桥内）：开放场景 `SceneManager.GetSceneAt` × `EditorUtility.CollectDependencies` —— `GameScene`：roots=51／组件=140／依赖=353／**目标命中 0**。

**对拍（法二·真身 guid ＋ 非 `.meta` 文本扫描）**：`AssetDatabase.AssetPathToGUID()` 取编辑器权威 guid，再在 **非 `.meta`** 文本类文件（`.asset`/`.unity`/`.prefab`/`.mat`/`.json`/`.cs`/… 共 **677 文件 · 8.04 M 字符**）中逐字搜 4 个 guid。
- 命中 **3** 处，全部落在 `BuildingMappingTable.asset`（`:33` rift／`:31` ruins／`:29` treasure_box）；**`portal` guid（`52463a62…`）零命中**。

**两法结论一致**：

| 目标资产（编辑器真身 guid） | 法一 引用方 | 法二 命中 | 场景／预制体／代码 |
|---|---|---|---|
| `rift.asset`　`3625478e6de9ba740b90db1bf0111f24` | **1**：`BuildingMappingTable.asset` | 1（同表 `:33`） | **0** |
| `portal.asset`　`52463a62a124bf148b737cb3e7d5decb` | **0** | 0 | **0** |
| `ruins.asset`　`5f0f3cef7dde9984cbbfc8305b0ef942` | **1**：`BuildingMappingTable.asset` | 1（同表 `:31`） | **0** |
| `treasure_box.asset`　`4cf205a222b91c34d9a404b969ce423b` | **1**：`BuildingMappingTable.asset` | 1（同表 `:29`） | **0** |

**读数落盘**：`Logs/hh332_refscan.log`（法一全量）／`Logs/hh332_xcheck.log`（法二全量）。

⚠️ **工具面事实（登记）**：`mcp_unityMCP` 探活正常（`/health` 200 · v10.2.0；`read_console` 返回正常），但其 `execute_code` 单次调用返回 `{"success":false,"message":null,"data":null}`（**未取得读数**）⇒ 按 skill `unity-mcp-first` §3 走 bridge `execute_csharp_script`（**仍是编辑器内**），⛔ 未改用 `.meta` grep 充数。

---

## 一、`rift.asset`

| # | 1 · 编辑器报出的引用方 | 2 · 按 id 字符串的生产码 | 3 · 同名活物 | 4 · 判定 |
|---|---|---|---|---|
| 1 | **`Assets/Resources/Buildings/BuildingMappingTable.asset`**（唯一引用方；该表 `:32-33`＝`type: 10` → 本资产 guid `3625478e…`） | — | — | **指向该 .asset**（映射表按 guid 引用·法一法二双证）。⚠️ 附事实：该表生产消费点只有一条 —— `BuildingFactory.cs:69` `table.Get(BuildingType.CastleCore)`（各表项声明齐备，运行时只读 `CastleCore` 条） |
| 2 | 场景／预制体／代码：**0**（`Assets` 面 1224 资产零命中；`GameScene` 命中 0） | — | — | — |
| 3 | — | `BuildingVisual.cs:57`（`case "rift": return "bld_portal"`）—— 该 switch 的入参是 **`def.id`**（`BuildingVisual.cs:19-24`·「Resources/Buildings/ 实盘 id」） | — | **指向该 .asset**（按 `def.id` 命中·非 guid 引用） |
| 4 | — | — | `riftCellX`（`Resources/Debug/Maps/map_0_seed12345.json` region 字段·C# 侧 0 命中）／`BuildingType.Rift`（`GridTypes.cs:143`）／`FeatureType.Rift`（`GridTypes.cs:109`）／`RiftComponent`（`BuildingComponents.cs:162`）／`bld_portal`（artId·`PlaceholderSprites.cs:76`） | **只是同名**（数据字段／枚举项／组件类／artId，均非本资产） |
| 5 | — | — | `BuildingComponents.cs:220-221`（`comp.rift` 挂载守卫按 `sourceType == BuildingType.Rift`） | **只是同名**（枚举面·非本资产引用） |

---

## 二、`portal.asset`

| # | 1 · 编辑器报出的引用方 | 2 · 按 id 字符串的生产码 | 3 · 同名活物 | 4 · 判定 |
|---|---|---|---|---|
| 1 | **0**（法一：`Assets` 面 1224 资产零命中；开放场景 `GameScene` 命中 0；法二：guid `52463a62…` 在 677 文件零命中） | — | — | — |
| 2 | — | `BuildingVisual.cs:56`（`case "portal": return "bld_portal"`） | — | **指向该 .asset**（按 `def.id` 命中·非 guid 引用） |
| 3 | — | — | **`Portal`**（`Disaster/Portal.cs:8`·`IDamageable + IGridOccupant + ISaveable`）／`PortalDisasterTrigger`（`PortalDisasterTrigger.cs:11`）／`PortalDef`（`Data/PortalDef.cs:5`）／`PortalSaveData`（`Data/PortalSaveData.cs:6`）／`PortalDisasterConfig`（`Data/PortalDisasterConfig.cs:5`）／`bld_portal`（`PlaceholderSprites.cs:76`、`SpriteRefTable.asset:138`） | **只是同名**（灾害传送门＝独立实体管线，非本资产） |
| 4 | — | — | `WaveDirector.SpawnPortalEntity`（`AI/WaveDirector.cs:246`）／`WaveDirector.cs:89` | **只是同名**（刷怪链用 `Portal` 实体） |
| 5 | 附事实（编辑器读数）：本资产 `sourceType: 10` ＝ `BuildingType.Rift`，与 `rift.asset` 同值；而映射表 `type 10 → rift.asset`（⛔ 非本资产） | | | 事实登记·⛔ 不判 |

---

## 三、`ruins.asset`

| # | 1 · 编辑器报出的引用方 | 2 · 按 id 字符串的生产码 | 3 · 同名活物 | 4 · 判定 |
|---|---|---|---|---|
| 1 | **`Assets/Resources/Buildings/BuildingMappingTable.asset`**（唯一引用方；该表 `:30-31`＝`type: 8` → 本资产 guid `5f0f3cef…`） | — | — | **指向该 .asset**（法一法二双证） |
| 2 | 场景／预制体／代码：**0** | — | — | — |
| 3 | — | `BuildingVisual.cs:69`（`case "ruins": return "bld_ruins"`） | — | **指向该 .asset**（按 `def.id` 命中） |
| 4 | — | — | `bld_ruins`（artId：`PlaceholderSprites.cs:78`／`BuildingVisual.cs:83`／`Building.cs:782` **废墟态占位渲染**）／`BuildingType.Ruins`（`GridTypes.cs:140`）／`BuildingState.Ruined`·`EnterRuined`（`Building.cs` 损毁态族） | **只是同名**（artId／枚举项／状态，均非本资产） |

---

## 四、`treasure_box.asset`

| # | 1 · 编辑器报出的引用方 | 2 · 按 id 字符串的生产码 | 3 · 同名活物 | 4 · 判定 |
|---|---|---|---|---|
| 1 | **`Assets/Resources/Buildings/BuildingMappingTable.asset`**（唯一引用方；该表 `:28-29`＝`type: 7` → 本资产 guid `4cf205a2…`） | — | — | **指向该 .asset**（法一法二双证） |
| 2 | 场景／预制体／代码：**0** | — | — | — |
| 3 | — | `BuildingVisual.cs:70`（`case "treasure_box": return "feat_treasure_box"`）；另 `ChestEntity.cs:6` 为**注释**（提及 sprite 名·⛔ 非代码引用） | — | **指向该 .asset**（按 `def.id` 命中） |
| 4 | — | — | **`ChestEntity`**（`ChestEntity.cs:18`·`IInteractable + ITaskSource`）／**`ChestManager`**（`ChestManager.cs:24`·Singleton＋`ISaveable`；唯一落箱口 `:70`） | **只是同名**（掉落箱＝独立管线，`ChestEntity.cs:8` 自认「不逃入 Building 建筑管线」） |
| 5 | — | — | `feat_treasure_box`（artId：`BuildingVisual.cs:70/82`）／`TreasureVault`（`TreasureVault.cs:21`·国库·castle 挂载）／`ChainAudit/Validators/R5_SixStage.cs:246`（别名表 `{"treasure_box","ChestEntity"}`） | **只是同名**（artId／国库组件／审计台账的名字级映射，均非本资产引用） |

---

## 五、⭐ 口径勘正（`HH.331` 列报 #4 · 我上批错，本批以编辑器实证为准）

| 项 | 上批（`HH.331`）说法 | 本批编辑器实证 |
|---|---|---|
| `.asset` 内 32hex 与 meta 的关系 | 称「两者不逐字对应 ⇒ guid 面不可用字符串法判定」 | ⭐ **`.asset` 内的 32hex 就是真 guid**：编辑器 `AssetPathToGUID` 对映射表 11 条逐位吻合（`tree c4ee0b6f…`／`mine a77f5a89…`／`farmland dbd580c7…`／`stone_pile 4c708b15…`／`wood_pile 546202c9…`／`ore_vein 33eed976…`／`treasure_box 4cf205a2…`／`ruins 5f0f3cef…`／`rift 3625478e…`／`castle b0d1ffe0…`／`VagrantCamp 75da74d7…`） |
| `.meta` 的 41B base64 blob | 推为「16B guid ＋ 25B 哈希」 | ⛔ **该推断不成立**：blob 与真 guid **无关**（如 `rift.asset.meta` blob 解出前 16B ＝ `0c7c1db0…`，而真 guid ＝ `3625478e…`）⇒ **`.meta` guid 行不可用于判引用**（与任务书给的对照一致） |
| 「六栋 meta guid 全库 0 命中 ⇒ 未被引用」 | 按字符串实读成立、但结论面**不可外推** | ⭐ **结论面不成立**：`rift`／`ruins`／`treasure_box` **确有引用方**（`BuildingMappingTable.asset`）；实读 0 命中的只是**错的字符串形式**（base64） |
| 判引用的正确方法 | — | **编辑器侧**：`AssetDatabase.AssetPathToGUID()` ＋ `AssetDatabase.GetDependencies` 反向扫描（法一）；32hex guid 文本扫描可作法二对拍 |

（⛔ 仅勘正口径与取证方法；**未出删除方案**，不动任何文件。）

---

## 六、停手核对

| 停手 | 状态 | 说明 |
|---|---|---|
| 1 · 要回答就必须删文件／改资产 | ⛔ **未触发** | 本批一行未改；报告只给引用面，⛔ 无删／不删字样 |
| 2 · 编辑器引用查找不可用 | ⛔ **未命中** | 法一（引用数据库反向扫描）＋法二（真身 guid 文本扫描）**双法均取得读数**且互证一致；仅 `mcp_unityMCP execute_code` 单条工具返回 `success:false`（已按 §3 切换 bridge，非"引用查找不可用"） |

---

## 七、零改动声明

- ⛔ 未写任何 `.cs`／`.asset`／`.unity`；未进 Play；未改 `08`／《中层执行计划》；未 commit／未 push。
- 落盘物（均非 `Assets` 面）：本报告 1 份；`Logs/` 下 6 件（`hh332_refscan.log`、`hh332_xcheck.log`、2 个扫描片段、2 个 payload、1 个调用器 `_mcp_call.ps1`）；`多Agent交接/_编号登记.md` 2 处（水位线号 ＋ `HH.332` 在途行）。
- `git status` 复核：`Valley Rampart/Assets/**` 面唯一改动仍是前序脏点 `Scenes/GameScene.unity`（非本批）。

## 策划裁决（D866）

核对采信。四栋连映射表 type 7／8／10 一起摘。任务书：`多Agent交接/策划端/HH.333_M4-C四栋空资产_任务书.md`。
