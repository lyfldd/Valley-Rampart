# `HH.336` `M4-B` 首片 · 两个死字段摘除 交付报告

- **编号**：**`HH.336`**（按 `_编号登记.md` 水位线取号 · 水位线 `HH.335`→`HH.336`）
- **依据**：**`D868`**（`M4-B` 首片：删 `BuildingDef.interactableType`／`enum InteractableType`／`isMineByproduct` 三处，并迁移两处消费者）
- **性质**：施工（改动面＝清单 4 项 ＋ 注释项）
- **基线**：`6f1da5e9`（含 `D867` 验收 `HH.334`）
- **日期**：2026-09-25
- **⛔ 未 commit／未 push** · **⛔ 未改策划端账本**（`_编号登记`／`_任务队列`／`_当前快照`／台账一律未动——应登记项见 §六）
- ⛔ 未碰任何 `.asset`／`.prefab`／`.unity`（含 37 栋资产里的 `isMineByproduct:`／`interactableType:` 旧序列化行·Unity 忽略·未手改未重存）

---

## 一、改动清单（file:line ＋ numstat）

| # | 文件 | numstat | 内容 |
|---|---|---|---|
| 1 | `Assets/_Game/Data/BuildingDef.cs` | **`0 +/6 -`** | ① 删 `:100 public InteractableType interactableType;`（保留其上 `[Header("交互与生命周期")]` ⇒ 现 `:99` Header ＋ `:100 isPlayerBuilt`／`:101 isDestructible`／`:102-104 maxHp`）② 删 `isMineByproduct` 整块（`[Header("矿洞副产…")]`＋`[Tooltip(…)]`＋字段·3 行）③ 删 `enum InteractableType { Own, Enemy, Resource }`（原 `:179`） |
| 2 | `Assets/Editor/ChainAudit/Validators/R3_SupplyChain.cs` | **`12 +/2 -`** | 判据块改为数据行判定：`if (HasComponent(def, BuildingComponentRegistry.MineByproduct))`（原 `if (def.isMineByproduct)`）＋注释同步「判定改读数据行 `def.components` 含 `comp.mine_byproduct`——原布尔已删；恒产 Crystal/FireOil/Ore 三条内容不变」；**新增私有助手 `HasComponent(BuildingDef, string)`**（`:346-352` 一带） |
| 3 | `Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs` | **`5 +/1 -`** | `:383` 日志由 `isMineByproduct=…` 改为 `components含comp.mine_byproduct={(mineHasByprodKey ? "true" : "false")}`（就地遍历 `mineDef.components` 比对 `BuildingComponentRegistry.MineByproduct`） |
| 4 | `Assets/Editor/Smoke/Valley_HH329_M4AProbe.cs` ＋ `.meta` | **`D`×2** | 编辑器侧 `AssetDatabase.DeleteAsset` 删除（`DeleteAsset=True`·删后 asset/meta 双 `False`·guid `405caae81085f4c4cb3ac98190894e30`）⇒ 全 Assets `.cs` 对该类名引用 **0**。`Logs/hh329_*.log` **保留未动** |
| 5 | `Assets/_Game/Systems/Building/BuildingComponents.cs` | **`2 +/1 -`** | `:10` 文件头「留接口空壳：Pickup/**Spawner**/Combat/**Rift**/CastleCore」→ 现状：「留接口空壳：Pickup…；**Spawner/Rift 两空壳已删（`HH.335`／`D867`）**；Combat/CastleCore 已实现」 |
| 6 | `Assets/_Game/Systems/Grid/GridTypes.cs` | **`1 +/1 -`** | `:143` `BuildingType.Rift` 注释 → **「1D 残留术语·废弃（组件与资产已删，D868）；保留 int 位，勿删项」** |

**行尾**：六文件全部 `i/lf w/lf`（保持 LF）。

---

## 二、判据 1~6 读数

### 判据 1 —— 能力：mine 仍挂副产组件（Play · 正门）✅
载体：`mcp_unity-bridge` → `execute_csharp_script`（`execution_mode:"play"`）＋ `TestHarnessApi.EnterTestRun` 正门；读数落 `Logs/hh336_mine_play.log`：

```
== HH.336 判据1 · mine 副产组件实挂核验（Play · 正门 EnterTestRun）==
脚本进入时 isPlaying=True · 2026-09-25 11:28:20
--- 正门 TestHarnessApi.EnterTestRun(cfg, 60f) 起 ---
EnterTestRun 返回 · day=1 · 11:28:22
① 真实链（AI 立国预置 mine → BuildingFactory.CreateBuildingInstance → AttachComponents）：mine 实例=3 · 其中挂 MineByproductComponent=3 · 抽样=k1@(94,102) | k2@(101,65) | k3@(69,103)
mine def 在场=True · components=comp.mine_byproduct
② 直挂一次（对 mine def 调 AttachComponents）：MineByproductComponent 在场=True · GO 组件数=3
③ 判定面：真实链 3/3 · 直挂=True
ExitTestRun 已调 · 11:28:23
```
旁证（进局日志）：`[MineByproduct] 副产仓就绪（mine）：水晶仓 cap=20，火油仓 cap=20，矿石仓 cap=20` × **3**（k1／k2／k3）。
已 `ExitTestRun` ＋ 已退 Play（`manage_editor stop` ⇒ 复核 `isPlaying=False`）✅

### 判据 2 —— R3 改前／改后：违例数相同 ＋ 三条供给都在 ✅

**改前（11:22:49·字段仍在）** `Logs/hh336_r3_before.log`：
```
== HH.336 M4-B 首片 · R3 供给链读数 ==
跑次=BEFORE（改前 · 字段仍在） · 时刻=2026-09-25 11:22:49 · isPlaying=False
违例总数 = 5
   [0] Kind=R3.有供给零消费 | Level=Yellow | Anchor=ResourceType.FireballAmmo 供给锚点=Resources/Buildings/SiegeWorkshop.asset SiegeWorkshopBuilding.CreateSubStores(L55-58) | Advice=蓄水（有产无消）：确认设计意图 / 补消费点 | LevelMark=🟡
   [1] Kind=R3.有供给零消费 | Level=Yellow | Anchor=ResourceType.MagicAmmo 供给锚点=Resources/Buildings/SiegeWorkshop.asset SiegeWorkshopBuilding.CreateSubStores(L55-58) | Advice=蓄水（有产无消）：确认设计意图 / 补消费点 | LevelMark=🟡
   [2] Kind=R3.有供给零消费 | Level=Yellow | Anchor=ResourceType.Meat 供给锚点=Assets/_Game/Systems/Kingdom/RanchSystem.cs:203 ModifyResource(…,true,…) | Advice=蓄水（有产无消）：确认设计意图 / 补消费点 | LevelMark=🟡
   [3] Kind=R3.有供给零消费 | Level=Yellow | Anchor=ResourceType.StoneAmmo 供给锚点=Resources/Buildings/SiegeWorkshop.asset SiegeWorkshopBuilding.CreateSubStores(L55-58) | Advice=蓄水（有产无消）：确认设计意图 / 补消费点 | LevelMark=🟡
   [4] Kind=R3.有读取零供给 | Level=Yellow | Anchor=ResourceType.SpecialFood 读取锚点=Assets/_Game/Systems/Kingdom/HappinessSystem.cs:217 读取(非扣费) | Advice=弱信号（有读取面、零供给、无硬消费）：疑似未实装，请策划端判 | LevelMark=🟡
供给[Crystal] 条数=1
      - Resources/Buildings/mine.asset MineByproductComponent(L62)
供给[FireOil] 条数=1
      - Resources/Buildings/mine.asset MineByproductComponent(L63)
供给[Ore] 条数=2
      - Resources/Buildings/mine.asset MineByproductComponent(L64)
      - Resources/Buildings/ore_vein.asset Gather(数据寻址·WorldGatherSource→HandleCellGathered，HH.294 片6-2)
供给非空键概览：Crystal=1, FireOil=1, FireballAmmo=1, Food=1, Gold=2, MagicAmmo=1, Meat=2, Metal=1, Ore=2, Stone=2, StoneAmmo=1, Wood=3
消费非空键概览：Crystal=9, FireOil=1, Food=5, Gold=51, Metal=6, Ore=2, Stone=44, Wood=42
豁免表条数=0 · SelfTestOk=False
```

**改后（11:29:12·字段已删·判据改读 `components`）** `Logs/hh336_r3_after.log`：
```
== HH.336 M4-B 首片 · R3 供给链读数 ==
跑次=AFTER（改后 · 字段已删 · 判据改读 components） · 时刻=2026-09-25 11:29:12 · isPlaying=False
违例总数 = 5
   [0] Kind=R3.有供给零消费 | Level=Yellow | Anchor=ResourceType.FireballAmmo 供给锚点=Resources/Buildings/SiegeWorkshop.asset SiegeWorkshopBuilding.CreateSubStores(L55-58) | Advice=蓄水（有产无消）：确认设计意图 / 补消费点 | LevelMark=🟡
   [1] Kind=R3.有供给零消费 | Level=Yellow | Anchor=ResourceType.MagicAmmo 供给锚点=Resources/Buildings/SiegeWorkshop.asset SiegeWorkshopBuilding.CreateSubStores(L55-58) | Advice=蓄水（有产无消）：确认设计意图 / 补消费点 | LevelMark=🟡
   [2] Kind=R3.有供给零消费 | Level=Yellow | Anchor=ResourceType.Meat 供给锚点=Assets/_Game/Systems/Kingdom/RanchSystem.cs:203 ModifyResource(…,true,…) | Advice=蓄水（有产无消）：确认设计意图 / 补消费点 | LevelMark=🟡
   [3] Kind=R3.有供给零消费 | Level=Yellow | Anchor=ResourceType.StoneAmmo 供给锚点=Resources/Buildings/SiegeWorkshop.asset SiegeWorkshopBuilding.CreateSubStores(L55-58) | Advice=蓄水（有产无消）：确认设计意图 / 补消费点 | LevelMark=🟡
   [4] Kind=R3.有读取零供给 | Level=Yellow | Anchor=ResourceType.SpecialFood 读取锚点=Assets/_Game/Systems/Kingdom/HappinessSystem.cs:217 读取(非扣费) | Advice=弱信号（有读取面、零供给、无硬消费）：疑似未实装，请策划端判 | LevelMark=🟡
供给[Crystal] 条数=1
      - Resources/Buildings/mine.asset MineByproductComponent(L62)
供给[FireOil] 条数=1
      - Resources/Buildings/mine.asset MineByproductComponent(L63)
供给[Ore] 条数=2
      - Resources/Buildings/mine.asset MineByproductComponent(L64)
      - Resources/Buildings/ore_vein.asset Gather(数据寻址·WorldGatherSource→HandleCellGathered，HH.294 片6-2)
供给非空键概览：Crystal=1, FireOil=1, FireballAmmo=1, Food=1, Gold=2, MagicAmmo=1, Meat=2, Metal=1, Ore=2, Stone=2, StoneAmmo=1, Wood=3
消费非空键概览：Crystal=9, FireOil=1, Food=5, Gold=51, Metal=6, Ore=2, Stone=44, Wood=42
豁免表条数=0 · SelfTestOk=False
```
⭐ **逐字对拍**：两档各 18 行，`Compare-Object` 差异**仅第 2 行**（跑次/时刻）⇒ 违例 5＝5 逐条同、`Crystal`／`FireOil`／`Ore` 三条 mine 供给同（L62／L63／L64）。

### 判据 3 —— 存在性反证 ✅ 0 命中
`Assets` 下 `*.cs` 搜 `isMineByproduct`／`InteractableType`／`interactableType`／`Valley_HH329_M4AProbe` ⇒ **0 命中**。
（首次检索曾命中 3 处——**均为本次施工自己写的注释**（`HH291:382`／`R3:100`／`R3:346` 引用旧字段名）⇒ 已改写掉字样后复检归零；此处如实登记为过程项。）

### 判据 4 —— 编译 0 error ✅
- 编译读数：`status=completed · errors=0 · warnings=10 · compile_observed=True`。
- **编译产物晚于改动源文件**：`Assembly-CSharp.dll` **11:24:08**（＞ `BuildingDef.cs` 11:23:33／`BuildingComponents.cs` 11:23:26／`GridTypes.cs` 11:23:27）；`Assembly-CSharp-Editor.dll` **11:25:11**（＞ `Valley_HH291_MapGenProbe.cs` 11:24:36／`R3_SupplyChain.cs` 11:24:37）。
- 控制台 10 条**逐条列出**（全为 Warning·**无 error**·无一条在改动面）：`Valley2_17_Smoke_5.cs(91,85) CS0162`｜`Valley2_17_Smoke_P0.cs(225,13) CS0219`｜`Valley2_21A_Smoke.cs(202,13) CS0162`｜`ArtImportPipeline.cs(267,9) CS0618`｜`Valley2_20_Smoke_Race.cs(701,25) CS0472`｜`Valley2_20_Smoke_Race.cs(875,13) CS0162`｜`Valley_HH128_Probe.cs(75,14) CS0219`｜`Valley_HH264_AnimProbe.cs(182,17) CS0219`｜`Valley_HH294_Slice6_2Probe.cs(875,13) CS0219`｜`Valley_HH80_Run.cs(301,13) CS0162`。

### 判据 5 —— 注释 ✅ ＋「已查」清单见 §四

### 判据 6 —— git diff 面 ✅
`Valley Rampart/Assets` 面改动＝**6 个逻辑项、7 个物理路径**（`HH.336` 收口提交口径）：`M R3_SupplyChain.cs`／`M Valley_HH291_MapGenProbe.cs`／`D Valley_HH329_M4AProbe.cs`／`D ...cs.meta`／`M BuildingDef.cs`／`M BuildingComponents.cs`／`M GridTypes.cs`（其中「删探针」一项＝`.cs`＋`.meta` 两物理路径）。
- **`.asset`／`.prefab`／`.unity` 零新增改动**：`Assets/Resources` 面 diff **空**；`GameScene.unity` 仍是**前序脏点 `5 +/1 -`**（与本批开工前逐值相同 ⇒ 未被 Unity 自动改写）。
- 其余 `??` 未跟踪项（`Valley_HH289_EcoProbe.cs`／`Valley_HH290_GatherProbe.cs`／`HH319_*_Probe.cs.meta`／`_Game/Art/Ground/*`）**均为前序既有·非本批**。

---

## 三、停手核对：**四条全未命中**

| 停手条件 | 状态 | 依据 |
|---|---|---|
| 清单外还有 `.cs` 读这两个字段 | ⛔ 未命中 | 开工前机械检索：`isMineByproduct` 读取点 = `BuildingDef:134`（定义）＋ `HH329` 探针（本批删）＋ `HH291:383`（本批改）＋ `R3:100`（本批改）；`InteractableType/interactableType` = 仅 `BuildingDef:100/179`（本批删）⇒ **零清单外** |
| ChainAudit 违例数改前改后不同 | ⛔ 未命中 | 5 ＝ 5（逐字对拍仅头部差异） |
| Unity 自动改写 `.asset`／`.prefab`／`.unity` 并进 diff | ⛔ 未命中 | `Resources` diff 空；`GameScene.unity` numstat 恒定 `5/1`（前序） |
| 编译出错且原因不在改动面 | ⛔ 未命中 | `errors=0`（10 warning 全既有） |

---

## 四、判据 5「已查」清单（逐文件 · 位置 · 结论）

| 文件 | 看过的位置 | 结论 |
|---|---|---|
| `BuildingDef.cs` | 文件头 `<summary>`（`:4-10`）；全部 `[Header]`（`:14 基础`／`:21 造价与占位`／`:32 2D 空间`／`:44 模块归属`／`:48 仓库`／`:60 行为标记`／`:66 组件绑定`／`:71 战争机器乘员`／`:77 产能`／`:85 铁匠铺区`／`:98/99 交互与生命周期`／`:106 地图预置映射`／`:117 表现`／`:120 怪物目标`／`:126 专属种族`）；相关 `[Tooltip]` 全文 | 三处目标（`interactableType`／`InteractableType`／`isMineByproduct` 块）已删；**无任何残留句子描述这两字段**（token 检索 0）；`交互与生命周期` Header 现辖 `isPlayerBuilt`／`isDestructible`／`maxHp`（语义仍成立） |
| `R3_SupplyChain.cs` | 文件头规格块（`:8-24`：扫描/供给/消费/判据/边界/自证/接口纪律）；`① 供给` 分节（`:78-128`）；新助手（`:346-352`） | 头部供给清单写的是**组件名** `MineByproductComponent`（非字段）⇒ 现文仍准确；判据块与注释已改读数据行；无残留旧字段描述 |
| `Valley_HH291_MapGenProbe.cs` | 文件头（`:1-13`：用法/两入口）；A6 段（`:369-389`：`A6②` 组件在场判定 / `A6 资产面` 日志行 / 口径勘正块） | 资产面日志已改 `components含comp.mine_byproduct=…`；其余用 `GetComponent<MineByproductComponent>()`（运行时组件判定）与文本均不需改；无残留旧字段引用 |
| `BuildingComponents.cs` | 文件头 `<summary>`（`:4-11`）；`// ===== 留接口空壳组件 =====` 分节（`:26-38` 区）；键表常量（`:187-190`）＋ `Register` 列表（`:196-201`） | `:10` 头注释已改（Spawner/Rift 已删·Combat/CastleCore 已实现）；文件内 `Spawner`／`Rift` 命中 **0**；键表 `MineByproduct` 常量与注册保持 |
| `GridTypes.cs` | `:103` `BuildingCategory` 的 `<summary>`；`:109` `BuildingCategory.Rift`；`:126` `BuildingType` `<summary>`；`:141` `Interactable`；`:143` `Rift`（已改） | `:143` 已写「**1D 残留术语·废弃**…保留 int 位，勿删项」；`:109` `BuildingCategory.Rift` **按任务书明令不动**⇒ `:103` 该句（"…特殊点/裂隙/主城…"）描述的是**来源分类枚举**，与现状一致**未改**（已查·供复核）；`:141 Interactable` 是 `BuildingType` 成员（**与被删的 `InteractableType` 字段不同物**）⇒ 未动（已查） |

---

## 五、零外溢 ＋ 工具链

- ⛔ 未动：其余 8 布尔（`isBlacksmith`／`isSiegeWorkshop`／`isBridge`／`isGate`／`isObstacle`／`isPlayerBuilt`／`isResourceNode`／`isDestructible`）＋ `isConsumable`；`BuildingType`／`BuildingCategory`／`BuildingRole`／`ModuleType` 枚举（含 `GridTypes.cs:109`）＋ `IsFortification`；`PickupComponent`／`comp.pickup`／`CastleCoreComponent`／`BuildingComponentRegistry` 键表；`BuildingFactory`／`TaskScheduler`／`KingdomBrain`／`AbstractEconomySettlement`；任何 `.asset`／`.prefab`／`.unity`。
- **通道**：`mcp_unity-bridge`（`execute_csharp_script` `execution_mode:editor|play`、`manage_editor get_state/play/stop`）；`Logs/hh329_*.log` 保留。
- ⭐ **新工具面知识（本批首次跑通）**：桥的 `execute_csharp_script` 支持 `execution_mode:"play"`，且**脚本返回的 `IEnumerator` 会被桥当协程调度**（顶层禁 `yield`，须写在**局部迭代函数**里再 `return Routine();`）⇒ 免建探针文件即可在 Play 态跑正门进局流程。

---

## 六、应登记项（⛔ 本批未改账本 · 供策划端回填 `_编号登记.md`）

```text
| **HH.336** | **`M4-B` 首片·两死字段摘除 交付报告**（执行端 · 承 `D868` · 取号 HH.336·水位线 335→336）：
① `BuildingDef.cs`（`0+/6-`）删 `interactableType` 字段＋`enum InteractableType`＋`isMineByproduct` 块（Header/Tooltip/字段）；
② `R3_SupplyChain.cs`（`12+/2-`）判据改「`def.components` 含 `BuildingComponentRegistry.MineByproduct`」＋新增助手 `HasComponent`；
③ `Valley_HH291_MapGenProbe.cs`（`5+/1-`）`:383` 日志改输出 `components含comp.mine_byproduct`；
④ 删 `Valley_HH329_M4AProbe.cs`＋`.meta`（`AssetDatabase.DeleteAsset`·全 Assets 该类名引用 0·`Logs/hh329_*.log` 保留）；
⑤ `BuildingComponents.cs:10` 头注释改现状（Spawner/Rift 已删）；⑥ `GridTypes.cs:143` `BuildingType.Rift` 注释改「1D 残留术语·废弃…勿删项」。
**判据 1~6 全达**：① Play 正门 `EnterTestRun` ⇒ mine 真实链 **3/3** 挂 `MineByproductComponent`＋直挂 True＋已 `ExitTestRun`＋退 Play；② R3 改前/改后违例 **5=5**（逐字对拍仅头部差异）·`Crystal`/`FireOil`/`Ore` 三条 mine 供给在；③ 四 token 在 `Assets/**/*.cs` **0 命中**；④ 编译 `errors=0`（DLL mtime 晚于改动源）·10 warning 全既有；⑤ 注释两处已改＋五文件「已查」清单在报告 §四；⑥ `Assets` 面 6 个逻辑项、7 个物理路径·`Resources` 零 diff·`GameScene.unity` 恒定前序 `5/1`。
**停手四条全未命中**。⛔ 未 commit／未 push／未改账本。 | 执行端 | 🟡 **已落盘·待策划端裁** | 2026-09-25 | 报告 `多Agent交接/执行端/HH.336_M4-B首片两死字段摘除_交付报告.md`；读数 `Logs/hh336_r3_before.log`／`Logs/hh336_r3_after.log`／`Logs/hh336_mine_play.log`／`Logs/hh336_delete.log` |
```

## 七、主策划端补落终裁（HH.336，2026-09-26）

### 终裁：判绿

- [实读] Valley Rampart/Assets/_Game/Data/BuildingDef.cs:99-110 当前交互段只剩 isPlayerBuilt、isDestructible、maxHp；interactableType、InteractableType、isMineByproduct 在 Valley Rampart/Assets/**/*.cs 机械扫描为 0 命中。
- [实读] Valley Rampart/Assets/Editor/ChainAudit/Validators/R3_SupplyChain.cs:99-105,346-352 已由 HasComponent(def, BuildingComponentRegistry.MineByproduct) 读取 def.components；Logs/hh336_r3_after.log:2-18 输出 违例总数 = 5，并保留 Crystal/FireOil/Ore 三条 mine 供给。
- [实读] Logs/hh336_mine_play.log:3-8 记录真实链 mine 实例 3、挂载 3；本裁只采信为本批探针载体读数，不外推为活世界长期行为。
- [实读] Valley Rampart/Assets/Resources/Buildings 中旧序列化键为 36 个文件／72 行；[推断] 依任务书明确的另开资产清理批次处理，不阻塞本批代码判据。

准入结论：HH.336 允许 HH.337 实核结果进入 M5 前置裁决；不构成 06:154 ②切换或③清理的准入，二者仍须另取迁移裁决。探针结论限于本批载体契约，不能表述为任务系统已在活世界可用。

[未知] 资产旧键何时由独立清理批次重存并清零，本裁不替该批定值。

