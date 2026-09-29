# HH.341 · 小源② `ChestEntity` —— **预检窗口**：独立回退点 ＋ 只读预检（§五 4 项）＋ 卡片适用性判定 交付报告

> 执行端 ｜ 2026-09-29 ｜ 单会话（**静态只读**）｜ ⛔ 零生产码／资产／场景改动 ｜ ⛔ 未 push ｜ 挂 `HH.341`（`M5-D乙加` · 第 2 源）
> 依据：**任务书**（`多Agent交接/策划端/HH.341_M5-D乙加_小源阶段任务书.md` · §五／§六／§七／§十／§十一）｜`D911` 终裁（台账 §二百二十六；任务书 §2.1 一次性例外已落）｜`L-97`（＋补条①行集/正则/三数 · ②计数口径标签 · ③行集来源 · ④同对象不同口径并列）｜`L-98`（＋补条②）｜`L-99`
> 硬边界照办：只读探针 —— ⛔ 未调 `TryAdvertiseTask`／`FindPickup`／任何源侧方法；⛔ **未改 `ChestEntity.cs` 与任何接缝文件**（§七-2「未判定不得写代码」）；⛔ 未碰 `GameScene.unity`（只登记 hash）；⛔ 未 `checkout --`／未 `restore`；⛔ 不改 `taskTimeout`／`Complete`；⛔ 未并 `DZ-7`；⛔ 未取新 D 号；⛔ 未在报告或落盘串写「判绿／判红」
> ⭐ **写动作计数＝0**（本轮**未进局**、⛔ 未调任何源侧方法、⛔ 未写任何生产/资产/场景文件；全部证据＝静态代码面 ＋ `git` 只读命令）

**三件事完成状态**：① 独立回退点 ✅ ② 只读预检 4 项 ✅ ③ 卡片适用性判定＝**任务卡源** ✅ —— 无未做项；⛔ 本轮未施工（§5.1 施工须另开一轮·见 §8-1）。

---

## 1. 开工回执

- **当前源**＝`ChestEntity`（`Valley Rampart/Assets/_Game/Systems/World/ChestEntity.cs`）
- **顺序位置**＝**第 2 源**（首源 `ConstructionSiteStore` 已按 `D911` 结项＝「结构性阻塞（玩家侧前置缺失：无非空配方承载者）· 非本源码缺陷」；⚠️ ⛔ ≠ 通过／判绿；本阶段**一次性显式例外**准申请 `ChestEntity` ⇒ 任务书 §2.1 例外条款）
- **前置基线**：任务书 §2.2 表 `ChestEntity` 行＝「`TaskScheduler.Instance|HasInstance` 命中行数 **1**｜`new KingdomTask(` 命中行数 **1**」⇒ 本轮实测复核**一致**（见 §3-4 行集 B；行口径）；§三 全量域基线扫描（59/16、14/9、9 类、11 项、3/2）属**施工轮** §七-8／§十一-6 基线包（本轮未做，如实登记）
- **独立回退点**＝见 §2
- ⭐ **写动作计数＝0**（⛔ 未进局；⛔ 未调任何源侧方法；⛔ 未写任何生产代码／资产／场景；本轮唯一落盘＝Logs 证据 1 个 ＋ 本报告）

## 2. 独立回退点（开工前实测 · 任务书 §七-1）

- **`HEAD`**（开工实测）＝ **`e96883ef713443463913f578c5d088e3a092ecac`**（相对本端上轮报告 `8eeaf0a5`，其间 **2 笔非本端 docs**：`f33dbfc6` `D909` 交付核实、`e96883ef` `D911` 终裁落账——如实列报）
- **工作树状态（`git status --short` 全量 · 逐行照录）**：

```
 M "Valley Rampart/Assets/Scenes/GameScene.unity"
 M "Valley Rampart/Packages/packages-lock.json"
 M pixel-forge/index.html
 M pixel-forge/server/forge.mjs
 M pixel-forge/server/serve.mjs
 M "最高优先级文档/_索引.md"
 M "最高优先级文档/底层执行计划.md"
?? .codebuddy/
?? .codex/
?? .commandcode/
?? .cursor/
?? .oc_cache/
?? .oc_data/
?? AGENTS.md
?? Active
?? Logs/
?? "Medieval Kingdom/"
?? Output/
?? Temp/
?? "Valley Rampart/Assets/Editor/Smoke/Valley_HH289_EcoProbe.cs"
?? "Valley Rampart/Assets/Editor/Smoke/Valley_HH289_EcoProbe.cs.meta"
?? "Valley Rampart/Assets/Editor/Smoke/Valley_HH290_GatherProbe.cs"
?? "Valley Rampart/Assets/Editor/Smoke/Valley_HH290_GatherProbe.cs.meta"
?? "Valley Rampart/Assets/Editor/Smoke/Valley_HH319_U15Probe.cs.meta"
?? "Valley Rampart/Assets/Editor/Smoke/Valley_HH319_WaterAccount.cs.meta"
?? "Valley Rampart/Assets/_Game/Art/Ground/New Palette.prefab"
?? "Valley Rampart/Assets/_Game/Art/Ground/New Palette.prefab.meta"
?? "Valley Rampart/Assets/_Game/Art/Ground/ground_tropical.asset"
?? "Valley Rampart/Assets/_Game/Art/Ground/ground_tropical.asset.meta"
?? "Valley Rampart/pyrightconfig.json"
?? _hh314_compile.log
?? _mcp_probe3/
?? _tmp_mcp.ps1
?? _tmp_mcp2.ps1
?? pixel-forge/server/.tmp-archer-batch.mjs
?? pixel-forge/server/.tmp-archer-finish.mjs
?? pixel-forge/server/.tmp-archer-walk.mjs
?? pixel-forge/server/.tmp-batch-repair.mjs
?? pixel-forge/server/.tmp-check1.ps1
?? pixel-forge/server/.tmp-copy-special.mjs
?? pixel-forge/server/.tmp-cutout-analyze.mjs
?? pixel-forge/server/.tmp-cutout-test.mjs
?? pixel-forge/server/.tmp-cutout.mjs
?? pixel-forge/server/.tmp-diagnose.mjs
?? pixel-forge/server/.tmp-dwarf-brighten.mjs
?? pixel-forge/server/.tmp-elf-ai-gen.mjs
?? pixel-forge/server/.tmp-elf-ai-raw.png
?? pixel-forge/server/.tmp-elf-ai.jpg
?? pixel-forge/server/.tmp-elf-ai.png
?? pixel-forge/server/.tmp-elf-preview.png
?? pixel-forge/server/.tmp-elf-ruins.mjs
?? pixel-forge/server/.tmp-healer-diff.mjs
?? pixel-forge/server/.tmp-healer-fix.mjs
?? pixel-forge/server/.tmp-human-recolor.mjs
?? pixel-forge/server/.tmp-knight-verify.mjs
?? pixel-forge/server/.tmp-luma.mjs
?? pixel-forge/server/.tmp-probe.bin
?? pixel-forge/server/.tmp-repair.mjs
?? pixel-forge/server/.tmp-sheet-test.mjs
?? pixel-forge/server/.tmp-side-healer-analyze.mjs
?? pixel-forge/server/.tmp-side-healer-fill.mjs
?? pixel-forge/server/.tmp-walk-verify.mjs
?? pixel-forge/server/.tmp-warrior-verify.mjs
?? pixel-forge/workspace/misc/
?? pyrightconfig.json
?? "图片资源/四族风格锚点/"
?? "多Agent交接/策划端/HH.342_DZ-新存档生成冲突修复批_任务书.md"
?? "最高优先级文档/19_王国AI决策循环与打断契约.md"
?? "最高优先级文档/20_个体NPC决策与神经策略契约.md"
?? "最高优先级文档/底层生命週期契约优化审查.md"
?? "河谷防线开发计划书具体内容/3.6.1_弹药消费清单.md"
?? "河谷防线开发计划书具体内容/3.8.1_音效播放器设计规格.md"
?? "美术资源文件夹/Ammo/ammo_monster.png"
?? "美术资源文件夹/Ammo/ammo_musket.png"
?? "美术资源文件夹/Ground/ground_ocean.png"
?? "音乐资源文件夹/"
```

- ⚠️ **回退点纯净性声明**：已跟踪 ` M` 项＝`GameScene.unity`（挂账 `O-14` 载体）／`packages-lock.json`／`pixel-forge`×3／最高优先级文档×2 —— 均**非本端改动**（历轮同态、逐轮列报）；本轮**零写** ⇒ 回退点**不含任何其他源的本端改动** ✓（§七-1）
- **允许面清单（任务书 §六 全部路径 · 逐条标注「本轮是否触碰」）**：

| # | 路径（§六 原文分项） | 本轮是否触碰 |
|---|---|---|
| 1 | `Valley Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs`（首源·已结项） | ⛔ **未触碰**（只读历史） |
| 2 | `Valley Rampart/Assets/_Game/Systems/World/ChestEntity.cs`（**本轮当前源**） | ⛔ **未触碰**（只读预检；⛔ 未写代码） |
| 3 | `…/Systems/Building/MineByproductComponent.cs` | ⛔ 未触碰（后续源） |
| 4 | `…/Systems/Kingdom/BlacksmithBuilding.cs` | ⛔ 未触碰（后续源） |
| 5 | `…/Systems/Kingdom/SiegeWorkshopBuilding.cs` | ⛔ 未触碰（后续源） |
| 6 | 接缝 7 文件：`TaskScheduler.cs`／`TaskCardProtocol.cs`／`TaskLifecycleRules.cs`／`TaskBindingTypes.cs`／`TaskBindingManager.cs`／`TaskProtocolRuntime.cs`／`TaskProtocolIssuer.cs` | ⛔ **未触碰**（仅只读引用；施工轮若需逐文件报批） |
| 7 | `ITaskScheduler.cs`（本阶段默认禁止改） | ⛔ 未触碰 |
| 8 | 证据目录 `Logs/hh341_small_*` | ✅ **写入 1 个证据文件**：`Logs/hh341_small_chest_entity_precheck_evidence.txt`（ChestEntity 专属前缀；⛔ 未覆盖 D1／CSS 证据） |
| 9 | 明确不在改动面：`Building.cs`／`UnitController.cs`／`ScheduleCenterStub*`／`currentWorkers`／场景／Prefab／旧资产键／`AI.Core`／最高优先级文档／四本账本 | ⛔ 全部未触碰 |

- **`GameScene.unity` `git hash-object`** ＝ **`4a86f26f6a7c2aab3c440903ddfa80020ddec9a3`**（挂账 `O-14` 载体 ⇒ **只登记**）
- ⛔ **未执行任何回滚类命令**（无 `checkout --`／无 `restore`；`L-99` 照办；开工前已查挂账池＋`git status`＋工作区差异见上）

## 3. 只读预检 4 项（任务书 §五 · 逐项交付）

**行集来源**：三项证据的原样输出见 `Logs/hh341_small_chest_entity_precheck_evidence.txt`（[B]／[C]／[D] 节）；本报告引用其 `file:line` 锚点。

### 3-1 生产触发入口、真实调用链与源状态变化锚点（`file:line`）

**向上（谁创建／注册）**：

| 环节 | 锚点 | 行为 |
|---|---|---|
| 落箱入口 | `ChestManager.cs:70-86` `SpawnChest` | `EnforceCellLimit` → `new GameObject("Chest")` → `AddComponent<ChestEntity>` → `Init(cell,pack,born)` → `_chests.Add` → `RegisterSource` |
| 注册入册 | `ChestManager.cs:89-93` `RegisterSource` → `TaskScheduler.cs:147-151` `Register` | `_sources.Add(source)` ＋ `source.OnRegister()`（`ChestEntity.cs:148` 空实现） |
| 注销 | `ChestManager.cs:97-101` `UnregisterSource` → `TaskScheduler.cs:153-159` `Unregister` | `_sources.Remove` ＋ `OnUnregister`（`ChestEntity.cs:152` 空实现）＋ `OnBuildingDied(source)` 放弃在派任务 |
| 外部运行时调用点 | `Building.cs:916`／`:1047`／`:1067`／`:1085`；`DamageSystem.cs:692`；`TreasureVault.cs:134` | 拆除/掉箱/战利品/溢出 → 落箱 |
| 读档重建 | `ChestManager.cs:257-284`（内联 `:275-281`） | 先清后建（幂等）＋ `RegisterSource`（同形入册）`ChestManager.cs:281` |
| 清场 | `ChestManager.cs:220-229` `ClearAll`；调用点 `WorldLifecycle.cs:46` | 逐箱注销后销毁 |

**向下（如何进入 `_sources` 与候选评估）**：

| 环节 | 锚点 | 行为 |
|---|---|---|
| 广告出口 | `ChestEntity.cs:131-146` `TryAdvertiseTask` | `_store` 非空 ⇒ 表序首个非空资源 ⇒ `new KingdomTask(Transport, this)`（`args=ScaleTaskArgs`；`destType=None`） |
| 手点立案 | `ChestEntity.cs:166` `RequestHaulNow(this)` → `TaskScheduler.cs:386-391` | 未在册则 `Register`；随后**立即 `Tick()`** |
| 周期收集 | `TaskScheduler.cs:313-324`（Tick ③） | 逐源 `TryAdvertiseTask`；Transport 按 `RemainingSlots`；`ResolveDest` |
| 候选评估/派工 | `TaskScheduler.cs:336-361`（slots `:342`；池隔离例外 `:351-353`）→ `:414-440` `Dispatch` | `_npcTaskMap` 等在册写入（`:419-424`）；接缝回调仅 WorldGatherSource/ConstructionSiteStore（`:426-430`，**箱源无**）；`:436` `IsKingdomTaskWorker=true`；`:438` `NavigateToSource` |
| 清理无效源 | `TaskScheduler.cs:263-280`（Tick ①） | `IsValid=false` ⇒ `_sources.Remove`（⛔ 不回调 `OnUnregister`） |

**源状态变化（推进链 · `file:line`）**：`MovingToSource` `:534-556`（到达→`Working` `:536-546`；超时 `:548`）→ `Working/Transport` `:578-585`（`LoadInventoryFromSource` → `ResolveChestDest` `:583` → `EnterMovingToDest` `:584`）→ `MovingToDest` `:645-680`（到达→`UnloadInventory` `:663`；`DestFull` `:665`；`Complete` `:669`）｜装载 `:967-996`（箱＝仓命中 `:978-980`）｜卸货 `:1002-1046`｜落点解析 `:1057-1070`｜段预算重置 `:1278-1283`｜完成 `:701-716`｜放弃 `:741-762`（reason 域 `:79-89`）｜箱源失效例外 `:514-524`｜箱容器过滤 `:1360-1361`（使用点 `:1188`／`:1237`／`:1335`）｜无主池 `:1550-1562`（`:1553`）。

### 3-2 该源的 `KingdomTask`／`TaskScheduler` 引用 × 对应生产分支（逐处）

**源侧引用（`ChestEntity.cs`，共 2 行 · 见行集 B）**：

| 锚点 | 引用 | 对应生产分支（做什么） |
|---|---|---|
| `ChestEntity.cs:138` | `new KingdomTask(KingdomTaskType.Transport, this)` | **广告出口**：被 `TaskScheduler.Tick ③`（`:313-324`）收集 → 派工（`:336-361`）→ `Dispatch`（`:414-440`） |
| `ChestEntity.cs:166` | `TaskScheduler.HasInstance` ＋ `TaskScheduler.Instance.RequestHaulNow(this)` | **手点立案入口**：`RequestHaulNow`（`:386-391`）注册＋立即 `Tick`；到账只走链 A 卸货段（⛔ 无独立入账口） |

（注释提及 `TaskScheduler.Tick` 于 `ChestEntity.cs:150`——注释行，不计入行集。）

**调度侧箱源专用分支（`TaskScheduler.cs`，共 6 处代码锚）**：

| 锚点 | 分支 | 做什么 |
|---|---|---|
| `:520`（`:514-524` 块） | 源失效例外 | 箱源 `MovingToDest` 不因 `IsValid=false` 放弃（防"最后一批滞留背包 ⇒ 到账断链"） |
| `:583`（调用）＋`:1057-1070`（实现） | `ResolveChestDest` | 装载成功后按**搬运者国＋实载资源**解析第二段落点（最近同国可收仓；无仓回退国库锚点） |
| `:1360-1361`（定义）＋`:1188`／`:1237`／`:1335`（使用） | `IsChestStore` 过滤 | 箱容器⛔ 不作弹药来源／退弹落点／`ResolveWarehouse` 落点（"箱只进搬运链"） |
| `:1550-1562`（`:1553`） | `SourceKingdom` → **-1** | 箱＝**无主源** ⇒ 无主池先到先得（任何国工人可接） |

**通用分支（箱源作为 `ITaskSource` 一并消费 · 非箱专用）**：`Tick ③` `:313-324`／Transport 去重与容量 `:317-319`＋`:342`／池隔离路由 `:346-357`／`Dispatch` `:414-440`／状态推进 `:534-556`／`:645-680`／`Complete` `:701-716`／`Abandon` `:741-762`／`ExecuteCompletion` `:828-849`／`LoadInventoryFromSource` `:967-996`／`UnloadInventory` `:1002-1046`／`ClearNpc` `:764-772`。

### 3-3 分支是否代表「工人可接取、移动、完成或放弃」的任务（逐项判定）

| 判定项 | 结论 | 证据（`file:line`） |
|---|---|---|
| **可接取** | ✅ 是 | `ChestEntity.cs:131-146` 产出 Transport 任务 → `TaskScheduler.cs:313-324` 收集 → `:336-361` 派工（slots `:342`；无主池 `:351-353`＋`:1553`）→ `:414-440` `Dispatch`（在册＋刺激＋导航） |
| **可移动** | ✅ 是 | `:438` `NavigateToSource` → `:534-556` MovingToSource（到达 `:536-546`／超时 `:548`）→ `:578-585` Working→装载（`:967-996`，箱＝仓 `:978-980`）→ `ResolveChestDest`（`:583`→`:1057-1070`）→ `:1278-1283` 转段 → `:645-680` MovingToDest |
| **可完成** | ✅ 是 | `:645-680` 到达卸货（`:663` `UnloadInventory` `:1002-1046`）→ `:669` `Complete` → `:701-716`（清册/复位）→ `:828-849` `ExecuteCompletion`（Transport 兜底卸货 `:839-849`）⚠️ 箱源**无**新协议完成回调（对照 CSS `:713-714`） |
| **可放弃** | ✅ 是 | `:741-762` `Abandon`（reason 域 `:79-89` 8 值）；触发面：外部撤回 `:212`／死亡 `:219`／路径失败 `:232`／源失效 `:514-524`／超时 `:548`／`DestFull` `:665`；注销链 `ChestManager.cs:211-217` → `TaskScheduler.cs:153-159` → `OnBuildingDied` 放弃在派任务 |

### 3-4 口径（行集来源 ＋ 分母 ＋ 三数 ＋ 零计数项）

| 行集 | 谓词（行集来源） | 原始 → 排除 → 有效 | 分母 | 备注 |
|---|---|---|---|---|
| A：`ChestEntity` 全生产域出现 | token=`ChestEntity`；域=`Valley Rampart/Valley Rampart/Assets/_Game/**` | **27 次（25 行 / 5 文件）→ 排除 9 行（注释/文档行，9 次）→ 有效 16 行（18 次，代码行）** | 25 行 / 5 文件（行数与次数两口径并列 · ⛔ 不混用） | 分布：`MapGate.cs=3｜ChestSaveData.cs=2｜ChestManager.cs=14｜ChestEntity.cs=1｜TaskScheduler.cs=7`（次数）；2 行双出现（`ChestManager.cs:29`、`MapGate.cs:690`） |
| B：`ChestEntity.cs` 内 `KingdomTask`／`TaskScheduler` 引用 | `TaskScheduler\.(Instance|HasInstance)` ∪ `new KingdomTask\(`；域=该文件 | **2 行 → 0 → 2 行** | 2 行 | 与任务书 §2.2 表（1／1）复核**一致**；`ITaskSource` 微集：原始 4 次（:7/:14/:18/:114）→ 排除 3（注释）→ 有效 1（`:18` 类声明） |
| C：新协议接缝引用（**零计数项**） | `OnProtocol` ∪ `TaskCard` ∪ `ProtocolRuntime` ∪ `hasDeadline` ∪ `_card`；域=`ChestEntity.cs` | **0 命中**（原始 0 → 排除 0 → 有效 0） | 0 | ⭐ 证据：该源**尚无**新协议接缝（施工轮需补齐 · 见 §4／§8-4） |

⚠️ **「未取得／无该状态」前先出观测范围**：行集 A 的域＝生产域全量（`_Game/**`，5 文件命中）；筛选＝token 精确；全量取值域＝按文件计数（含零计数文件不列——域内命中文件 5/全体；逐文件计数已给）。

## 4. 卡片适用性判定：**任务卡源**（判定证据＝代码面调用链 · §五-4）

⭐ **判定：`ChestEntity` ＝ 「任务卡源」**。

**判定证据（代码面，⛔ 不以行为历史为判据）**：
1. `ChestEntity.cs:18` 声明实现 `ITaskSource`；
2. `ChestEntity.cs:131-146` `TryAdvertiseTask` 产出**真实工人任务** `new KingdomTask(KingdomTaskType.Transport, this)`（含 `args=ScaleTaskArgs` 规模派工 ⇒ 同箱可并发多工人各取一趟）；
3. 调度器**周期收集**（`TaskScheduler.cs:313-324`）并**派发**（`:336-361` → `:414-440`）；
4. 任务全链「可接取／可移动／可完成／可放弃」齐备（§3-3 逐项 `file:line`）；
5. 手点入口 `ChestEntity.cs:166` → `RequestHaulNow`（`:386-391`）同样立案搬运任务。
⚠️ 两点如实并列：① 该源**当前无新协议接缝**（§3-4 行集 C＝0 命中；`Dispatch` 接缝回调仅 WorldGatherSource／ConstructionSiteStore，`:426-430`）⇒ 施工轮需按 §四既定接缝补齐（照 CSS 先例）；② 「历史上 `ChestEntity` 常被派工」仅作旁证，⛔ **未作判据**（判据＝上列代码面调用链）。

**分支处理（§5.1）**：⛔ **本轮不施工** —— 仅交预检结论；**提请开施工窗口**（见 §8-1）。施工须按 §七 九步 ＋ §八 九条判据**另开一轮**（⛔ 不得在本轮顺手施工）。（§5.2 六项替代证据：**不适用**——判定为任务卡源，非卡源分支未触发。）

## 5. 口径落实声明

1. **行集来源**：§3-4 三行集均标谓词＋域＋计数口径；⛔ 未混用「命中行数」与「匹配次数」（任务书 §三 · 两口径并列）；同对象多口径并列（§3-4 备注列）照 `L-97` 补条④。
2. **分母/三数/零计数项**：§3-4 逐行集给出；**零计数项**（新协议接缝引用 0 · `ChestEntity.cs` 内）已如实列出。
3. **观察窗 7 项**：**不适用** —— 本轮**零进局、零运行时读数**（纯静态只读预检）；⛔ 不以静态阅读时间冒充观察窗（如实登记；运行时证据留待施工轮 §七-5/6）。
4. **`Time.time` 与墙钟分列**：**不适用**（无运行时测量）；本轮唯一时间量＝落盘核验的 `mtime`（§6）与 `git` 只读输出时刻，⛔ 不作折算。
5. **量纲**：`WaitForSeconds(n)` ⇒ 游戏秒＝n／单帧推进公式——本轮未涉及（无运行时）；⛔ 未引短窗倍率。

## 6. 落盘核验闸门（§十一-9 · 4 步逐条回执）

1. **`mtime` 前后变化核验**：写入前实测＝**本报告路径不存在**（09:50:25 记录）；初写后复查 `mtime=09/29/2026 09:53:10`、`size=23448` 字节 ⇒ **发生变化 ✓**；回执补录（末次写入）后复核 `mtime` **再次变化 ✓**（末次精确值以磁盘查询为准；⛔ 本报告不自含末次 mtime 与全文件哈希，避免自指）。
2. **磁盘内容重新读取**：以 `[IO.File]::ReadAllText` 从磁盘**重读全文**（⛔ 未复用内存缓冲）⇒ 通过 ✓。
3. **逐字比对（与写入意图逐项对照）**：对 **16 项**关键行执行包含性逐字核对 —— 9 节标题（§1..§9 共 9 项）＋ `<<GATE>>` 回执锚本 ＋ `HEAD=e96883ef713443463913f578c5d088e3a092ecac` ＋ `GameScene=4a86f26f6a7c2aab3c440903ddfa80020ddec9a3` ＋ `ChestEntity.cs:138` ＋ 锚点行 `:1553` ＋ 「写动作计数＝0」＋「未调 `TryAdvertiseTask`」—— **16/16 命中 ✓**（清单与实测结果另录证据文件附录）。
4. **已提交文件再与 `git show <commit>:<path>` 对比**：本次提交后实测 —— `git cat-file blob <commit>:<path>` 与工作区文件**逐字节一致**（size ＋ SHA256 双证）；实测数值见本节「步骤 4 附」。

**步骤 4 附（实测数值 · commit A）**：commit A ＝ `475dafdbae667b6017d4853e1cca388ed5673c45`（短号 `475dafdb`；**仅本报告 1 文件** · +257 行）；`git rev-parse 475dafdb:<path>` 得 blob ＝ `06674c0cdb985e0e9c70132354289db98f430463`（size **24670**）｜工作区文件 `git hash-object` ＝ **同值**、size ＝ **24670** ⇒ **逐字节一致 ✓**。
**步骤 3 清单（精确 16 项 · 实测 16/16 命中）**：9 节标题（§1..§9）＋ `e96883ef713443463913f578c5d088e3a092ecac` ＋ `4a86f26f6a7c2aab3c440903ddfa80020ddec9a3` ＋ `ChestEntity.cs:138` ＋ `:1553` ＋ `ChestEntity.cs:166` ＋「写动作计数＝0」＋「未调 `TryAdvertiseTask`」。（本回执为 **commit B** 补录；commit B 自身按同闸门复核：mtime 再变化 ✓／磁盘重读 ✓／全文件 blob 与工作区一致 ✓——末次数值另录证据文件附录。）
## 7. 自报瑕疵（⛔ 不软化、不省略）

1. **本轮为静态轮**：⛔ 未进局、⛔ 无运行时对拍——§五-1 的「源状态变化」以**代码路径锚点**交付（§3-1 末段）；运行时证据（真进局生产面）留待施工轮 §七-5/6。如实声明，⛔ 不冒充。
2. **`TaskScheduler.cs:975` 为历史批注**（`M1-G-1` 件6「去掉 `is ChestEntity` 限定」）：引用以**现存代码**为准；该历史改动本轮**不评价**（非本轮范围）。
3. **类头注释与实测差异**：`ChestManager.cs:11` 称「`SpawnChest` 全工程 **5 处**运行时调用方」——本轮实测**6 处**（`Building.cs`×4／`DamageSystem.cs`×1／`TreasureVault.cs`×1，均为运行时）。差异**如实登记**，⛔ 未改注释、⛔ 未定因（列【请裁】外，供事务端复核）。
4. **任务书 §六-1 措辞歧义**：其仍写「当前施工源文件（首源只能是）`ConstructionSiteStore.cs`」——本轮按 `D911` 一次性例外以 `ChestEntity.cs` 为当前源；⛔ 未改任务书（列 §8-2）。
5. **计数口径说明**：行集 A 的 27 次含 **2 行双出现**（`ChestManager.cs:29`、`MapGate.cs:690`）；「排除 9 行」＝注释/文档行，**非行为排除**。
6. **证据前缀**：新建 `Logs/hh341_small_chest_entity_precheck_evidence.txt`（ChestEntity 专属）；⛔ 未覆盖 `hh341_small_construction_site_*`（CSS）与 D1 证据。
7. **未做**（如实列）：§三 全量域基线扫描（59/16 等 5 项）、§七-3~9 各步——均属施工轮（本轮范围＝§七-1/2＋§五）。

## 8. 【请裁】（5 条 · 只列需裁定的）

1. **开施工窗口**：判定＝**任务卡源** ⇒ 依 §5.1／§七-2，请**开 `ChestEntity` 施工窗口**（第 2 源）；施工范围＝`ChestEntity.cs` ＋ 必要接缝（§六-3 **逐文件**说明原因与 `file:line`）；⛔ 本轮未施工、⛔ 未写一行代码。
2. **任务书 §六-1 加注**：其仍写「首源只能是 `ConstructionSiteStore.cs`」；`D911` 例外是否需在任务书/台账**加注**以消歧义？（⛔ 本轮未改任务书·非本端权限）
3. **「上一源绿态」基线口径**：`CSS` 结项＝「结构性阻塞·**非绿**」⇒ §三 要求「**以上一源绿态**为本源基线」——第 2 源是否以**当前 `HEAD` 实测**（`e96883ef`）作为本源开工基线？（影响施工轮 §七-8／§十一-6 基线包口径）
4. **接缝范围预期**：`ChestEntity` 现**无协议回调**（§3-4 行集 C＝0）⇒ 施工轮预计需在 `TaskScheduler.cs` 增**箱源接缝调用**（照 `:426-430` CSS 先例）＋ `ChestEntity.cs` 增四类接缝回调（派发/到达/放弃/源失效）；是否准在 §六-3 接缝文件内实施（施工轮**逐文件报批**）？
5. **例外覆盖面**：`D911`「一次性显式例外」是否仅覆盖 `ChestEntity`（第 2 源）窗口；**第 3 源起**是否恢复「当前源绿后才行进」？（⛔ 本轮未自定下一源顺序）

## 9. 源级结论

⛔ **本轮未施工 ⇒ `ChestEntity` 源级 ＝ 未开始 · 预检待裁**（⛔ 不得判绿／判红；⛔ 未自行推进施工、⛔ 未自定下一源顺序、⛔ 未改非任务卡源判据、⛔ 未声称 §九-6 单管理器达成）。
本轮新增事实：**独立回退点已立**（`HEAD=e96883ef` · 工作树全量照录 · 允许面逐条未触碰 · `GameScene.unity` hash 只登记）｜**只读预检 4 项齐备**（`file:line` 锚点见 §3；行集/分母/三数见 §3-4）｜**卡片适用性判定＝任务卡源**（代码面五证 · §4）⇒ 施工窗口待裁。

---

## 附：合规声明与交付件

```
HEAD（开工实测）                        e96883ef713443463913f578c5d088e3a092ecac
GameScene.unity git hash-object         4a86f26f6a7c2aab3c440903ddfa80020ddec9a3（只登记）
回滚类命令                               未执行（checkout -- / restore 均无）
写动作计数（游戏实体）                    0（未进局；未调任何源侧方法；未写生产/资产/场景）
本报告 commit                            仅本报告 1 文件（Logs/ 命中 .gitignore）
```

**交付件**：本报告（`多Agent交接/执行端/HH.341_ChestEntity预检窗口_交付报告.md`）＋ 证据文件 `Valley Rampart/Logs/hh341_small_chest_entity_precheck_evidence.txt`（原始输出 · gitignored）。
（`HH.341` 报告文件＝本文件；⛔ 未 push。）