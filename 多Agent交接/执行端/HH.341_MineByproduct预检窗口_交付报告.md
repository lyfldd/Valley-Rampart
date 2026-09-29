# HH.341 小源③ `MineByproductComponent` 独立只读预检窗口 · 交付报告

- 执行端交付 · 2026-09-29 ｜ 任务书唯一正文＝`多Agent交接/策划端/HH.341_M5-D乙加_小源阶段任务书.md`
- 证据前缀＝`Valley Rampart/Logs/hh341_small_mine_byproduct_*`（独立前缀，未沿用 `chest_entity`／`construction_site`）
- 本轮性质＝**独立只读预检窗口**（`D919` 仅批准预检，不批准改码）⇒ **写动作计数＝2**（本报告＋证据文件），**生产面写动作＝0**

---

## 1. 开工回执

| 项 | 值 |
|---|---|
| 当前源 | `MineByproductComponent`（`Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs`，全文件 263 行） |
| 顺序位置 | **第 3 源**（任务书 §2.2 默认顺序表第 3 行） |
| 门槛依据 | §2.1 第 2 条（`:29`）：第 2 源 `ChestEntity` 已完成独立回归、基线复算、事务端核实且源级＝**通过**（`D918`/`D919`，当前 `HEAD` 即其落账提交）⇒ 本轮属门槛满足后的递进 |
| 前置基线 | **当前 `HEAD` 实测**＝`25d5ca6ecccc9e7639b68a99865cec57902ad664`（开工 `git rev-parse HEAD` 实测，非推算；收工复测见 §6 第 4 步附注） |
| 独立回退点 | 见 §2 |
| 写动作计数 | **0**（生产代码／资产／场景／政策值零改动；`GameScene.unity` 未触碰） |
| 只读预检结论 | §五 4 项全部完成 ⇒ **判定＝任务卡源**（详见 §4） |
| 卡片适用性判定 | **任务卡源**（§5.1 分支）⇒ 本轮不施工，【请裁】① 提请另开施工窗口 |

## 2. 独立回退点（§七-1）

1. **`HEAD`**（开工实测）＝`25d5ca6ecccc9e7639b68a99865cec57902ad664`（`25d5ca6e docs(D919…)`）
2. **工作树状态**（`git status --short` 全量逐行照录）＝见证据文件 `Valley Rampart/Logs/hh341_small_mine_byproduct_gate.txt` §一（modified 7 项＋untracked 72 项，逐行全录）：其中 `M "Valley Rampart/Assets/Scenes/GameScene.unity"` 为挂账 `O-14` 载体，**继续挂账**；`M "Valley Rampart/Packages/packages-lock.json"`、`M pixel-forge/*`（3）、`M 最高优先级文档/*`（2）为本轮开工前既有差异，不属于本源改动，回退点如实含之（回退点＝开工快照，非本源净面）。
3. **允许面清单**（§六 全部路径，逐条标注「本轮是否触碰」）：

| §六 | 路径 | 本轮是否触碰 |
|---|---|---|
| §六-1 | `Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs`（当前源） | **未触碰**（只读） |
| §六-2 | `Valley Rampart/Assets/_Game/Systems/World/ChestEntity.cs` | 未触碰 |
| §六-2 | `Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs` | 未触碰（同 §六-1 当前源） |
| §六-2 | `Valley Rampart/Assets/_Game/Systems/Kingdom/BlacksmithBuilding.cs` | 未触碰 |
| §六-2 | `Valley Rampart/Assets/_Game/Systems/Kingdom/SiegeWorkshopBuilding.cs` | 未触碰 |
| §六-3 | `…/AI/TaskScheduling/TaskScheduler.cs` | 未触碰（仅读） |
| §六-3 | `…/AI/TaskScheduling/TaskCardProtocol.cs` | 未触碰（仅读） |
| §六-3 | `…/AI/TaskScheduling/TaskLifecycleRules.cs` | 未触碰（仅读） |
| §六-3 | `…/AI/TaskScheduling/TaskBindingTypes.cs` | 未触碰（仅读） |
| §六-3 | `…/AI/TaskScheduling/TaskBindingManager.cs` | 未触碰（仅读） |
| §六-3 | `…/AI/TaskScheduling/TaskProtocolRuntime.cs` | 未触碰（仅读） |
| §六-3 | `…/AI/TaskScheduling/TaskProtocolIssuer.cs` | 未触碰（仅读） |
| §六-4 | `Valley Rampart/Logs/hh341_small_*`（证据目录） | **已写 1 新文件**：`Valley Rampart/Logs/hh341_small_mine_byproduct_gate.txt`（证据面·允许） |

4. **`GameScene.unity` 的 `git hash-object`**（挂账 `O-14` 载体 ⇒ 只登记）＝`4a86f26f6a7c2aab3c440903ddfa80020ddec9a3`（工作树实测；工作树 ≠ `HEAD` 属 `D919` 已知挂账，继续挂账）。
5. **回退手段表述**＝`git show <rev>:<path>` 覆盖还原（⛔ 未写 `git checkout --`；本轮零生产改动，实际未执行任何回滚类命令）。

## 3. 只读预检 4 项（§五 · 全带 `file:line`）

> 原始输出全量落盘于 `Valley Rampart/Logs/hh341_small_mine_byproduct_gate.txt`；本节列主链锚点，路径一律相对仓库根。

### 3.1 预检① 生产触发入口 / 真实调用链 / 源状态变化

**向上（谁创建／注册它）**：

| 环节 | 锚点 |
|---|---|
| 数据行（键挂载声明） | `Valley Rampart/Assets/Resources/Buildings/mine.asset:17` ＝ `comp.mine_byproduct` |
| 键常量／键表注册 | `Valley Rampart/Assets/_Game/Systems/Building/BuildingComponents.cs:188`／`:201` |
| 挂载入口 A（工厂建局＋读档重建） | `Valley Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs:181` → `AttachComponents` `:243-255`（`:252` `TryAttach`） |
| 挂载入口 B（玩家建造） | `Valley Rampart/Assets/_Game/Systems/Building/BuildController.cs:329` |
| 绑定器（AddComponent＋Init） | `Valley Rampart/Assets/_Game/Systems/Building/BuildingComponents.cs:235-243`（`:239`／`:241`） |
| 组件 Init＋三子仓创建 | `Valley Rampart/Assets/_Game/Systems/Building/MineByproductComponent.cs:46-57`（`:56`）→ `:61-85` |
| 逐秒驱动 | `Valley Rampart/Assets/_Game/Systems/Building/ProductionSystem.cs:18-27`（Update 每 1s）→ `:32-53`（`:44` `GetComponents<ITickable>` → `:50` `Tick()`）→ `MineByproductComponent.cs:88-98` |
| 调度器注册（懒） | `MineByproductComponent.cs:245-250`（`:248` `Register(this)`）→ `TaskScheduler.cs:147-151`（`:150` 入 `_sources`） |
| 注销／清场 | `MineByproductComponent.cs:252-262`（`:255` `Unregister`）→ `TaskScheduler.cs:153-159`（`:158` `OnBuildingDied` 清在派） |

**向下（如何进入 `TaskScheduler` 的源集合与候选评估）**：

| 环节 | 锚点 |
|---|---|
| 调度主循环 | `Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs:262` `Tick()`；① 无效源清扫 `:265-285`；② 空闲候选 `:288-306`（职业门 `:301-302` Worker/Civilian/Porter）；③ 广告收集 `:315-327`（`:319` `TryAdvertiseTask`、`:320-323` Transport 名额门、`:324` 独占去重、`:325` `ResolveDest`）；④ 排序 `:336` → 池隔离 `:343` → 派发 `:362` `Dispatch` |
| 广告实现（源侧两型） | `MineByproductComponent.cs:173-193`：`:180-185` 无在岗 ⇒ `:182` `new KingdomTask(Production, this)`＋`:183` `destType=None`（常驻·原地劳作）；`:187-192` 有在岗 ⇒ 三仓达标判定（`:189-191`）→ `:205` `new KingdomTask(Transport, this)`＋`:209` `destType=SpecificBuilding`＋`:210` `destPos=主城门口可走格`＋`:211-215` `ScaleTaskArgs` |
| 在岗查询（源→调度器） | `MineByproductComponent.cs:196` → `TaskScheduler.cs:167-178` `HasWorkerAssigned`（遍历 `_npcTaskMap` 按 source 引用＋Working 态） |

**源状态变化锚点**：`_registered` 懒注册旗标 `MineByproductComponent.cs:44`→`:249`；三子仓存量 `:100-122`（`:103` 满仓停产门、`:116` 入仓）；满仓分频旗标 `:41-43`/`:105-112`；存档 `Valley Rampart/Assets/_Game/Systems/Building/Building.cs:1105-1106` ↔ 源侧 `:136-139`；读档 `Building.cs:1228-1229` ↔ 源侧 `:142-162`；销毁清场 `:252-262`。

### 3.2 预检② KingdomTask／TaskScheduler 引用与对应生产分支（逐处）

**源文件内引用**（行集 `## S SRC_SELF`，7 行）：

| 引用点 | 对应生产分支 |
|---|---|
| `MineByproductComponent.cs:182` `new KingdomTask(Production,this)` | 调度器 Production 分支：广告收集 `TaskScheduler.cs:319` → 独占去重 `:324`/`:1383-1391` → 派发 `:362`/`:417-446` → 完成走 `:646-650` else → `Complete :648`/`:709-727` → `ExecuteCompletion :849-852` |
| `MineByproductComponent.cs:196` `HasWorkerAssigned(this)` | 在岗查询分支 `TaskScheduler.cs:167-178`（在岗门语义载体，源侧 `:93` 停产门） |
| `MineByproductComponent.cs:205` `new KingdomTask(Transport,this)` | 调度器 Transport 分支：名额门 `:320-323`/`:1415-1420` → 装载段 `:587-599`（`:590` `LoadInventoryFromSource`，本源子仓分支 `:999-1003`，`:1001` `GetComponent<MineByproductComponent>()` 唯一代码引用）→ 转段 `:593`/`:1293-1298` → 卸货段 `:654-690`（`:672` `UnloadInventory`→`:678` `Complete`）→ 兜底 `ExecuteCompletion :854-864` |
| `MineByproductComponent.cs:247-248` | 注册分支 `TaskScheduler.cs:147-151` |
| `MineByproductComponent.cs:254-255` | 注销分支 `TaskScheduler.cs:153-159` |

**调度器侧通用消费分支**（本源任务流经）：路由 `:1565-1577`（Component 分支 `:1571-1575` `GetComponentInParent<Building>` 按父建筑归属国；`:1557` 注释即写明本源）；源失效放弃 `:521-531`（`:526` ChestEntity 例外不含本源）；到达 `:542-545`；超时 `:557-559`/`:681-683`；DestFull `:669-676`；Dead/BrainLost `:510-518`。

### 3.3 预检③ 分支四态判定（可接取／可移动／可完成／可放弃）

**Production 分支**（`MineByproductComponent.cs:182`）：

- 可接取 ✓ `TaskScheduler.cs:288-306`（职业门 `:301-302`）＋`:319`＋`:362`；独占去重 `:324`/`:1383-1391`（本源未置 `advertiser` ⇒ 键＝source）
- 可移动 ✓ `:444` `NavigateToSource`（`:466-472`）＋`:535-538` Assigned→MovingToSource
- 可完成 ✓ `:542-545` 到达→Working；`:585` 计时→`:646-650` else→`Complete :648`（`ExecuteCompletion :849-852` 对本源为空操作——观察项 V-1）
- 可放弃 ✓ `:510-513` BrainLost／`:515-518` Dead／`:521-531` SourceInvalid／`:557-559` Timeout

**Transport 分支**（`MineByproductComponent.cs:205`）：

- 可接取 ✓ 同上＋规模名额 `:320-323`/`:1415-1420`（`:1404-1412` 用 `ScaleTaskArgs.totalResourceDemand`，源侧 `:211-215`）
- 可移动 ✓ 第一段 `:444`/`:535-565`；装载 `:590`（本源子仓分支 `:999-1003`）→ 第二段 `:593`/`:1293-1298` → `:654-690`
- 可完成 ✓ `:659` 到达→`:672` `UnloadInventory`→`:678` `Complete`；兜底 `:854-864`（`:862-863`）
- 可放弃 ✓ 四类同上＋DestFull `:669-676`＋第二段 Timeout `:681-683`

### 3.4 预检④ 判定与证据

**判定＝任务卡源**（代码面调用链判定；未以「历史上常被派工」当判据）。三段主链证据：
① 广告链：`ProductionSystem.cs:50` → `MineByproductComponent.cs:173-193` → `TaskScheduler.cs:319`（`TryAdvertiseTask` 为 `ITaskSource` 契约成员，接口定义 `Valley Rampart/Assets/_Game/Systems/AI/Tasks/KingdomTask.cs:60-76`，任务构造 `:44-50`）；
② 装载链：`TaskScheduler.cs:590` → `:999-1003`（`:1001` 唯一代码引用按 `args.resourceType` 取副产子仓，源侧 `GetStore :125-131`）；
③ 完成/放弃链：`:678`/`:648` Complete、`:521-531`/`:669-683` 放弃，均以 `task.source` 引用本源实例。

任务书 §2.2 表格自检：`TaskScheduler.Instance|HasInstance` 命中＝5 行（`:196/:247/:248/:254/:255`）、`new KingdomTask(` 命中＝2 行（`:182/:205`），与任务书表「5 / 2」逐项吻合（无上一轮 `:14`/`:13` 型引注偏差）。

## 4. 卡片适用性判定（§5.1／§5.2 二择一）

- **判定＝任务卡源**（§5.1 分支）⇒ 按 §5.1，该源施工时须在真实 Play 进局的生产面出现真实 `TaskCard`，完整链路按 §七验收——**本轮不施工**，施工窗口提请见【请裁】①。
- §5.2 六项替代证据**不触发**（判定非非任务卡源）；⛔ 未造卡、⛔ 未以「没有卡片」作任何结论。
- **接缝现状登记**（§五预检附加事实，⛔ 不把接缝当「已存在」）：新协议五类接缝 A 派发（`TaskScheduler.cs:429-436`）、B 到达（`:548-555`）、C 放弃（`:766-774`）、D 源失效（`:274-281`）、E 完成（`:721-725`）当前**仅认** `WorldGatherSource`／`ConstructionSiteStore`／`ChestEntity` 三型 ⇒ **本源命中 0**（行集 `## S SEAM` 零计数项）；本源当前**完全走旧 `KingdomTask` 链**（占用镜像 `_npcTaskMap`、无 `TaskCard`、无配对/预定）。若准施工，接缝补齐属施工轮按 §七九步执行的事项，本预检只登记事实。

**观察项（只登记代码面事实，不裁断）**：
- V-1：`ExecuteCompletion` Production 分支（`TaskScheduler.cs:849-852`）经 `GetComponent<ProducerComponent>()` 取组件；`mine` 本体无 `ProducerComponent` ⇒ 该完成动作对本源为空操作；本源「在岗才产」语义由 `MineByproductComponent.cs:93`/`:196`＋`ProductionSystem` 驱动链承载。
- V-2：`UnloadInventory` 的 AI(>0) 台账特支（`TaskScheduler.cs:1038`）条件仅覆盖 `Crystal`/`FireOil`；T1.4（`D609`）新增的 `Ore` 未列入 ⇒ AI 工人搬 Ore 落通用卸货分支。是否需处理归策划端裁。
- V-3：`D909` 线索（`STO_ROW` `Byproduct_*`／`parentDef` 含 `mine`）与本轮代码面结论（`mine.asset:17` 数据行）方向一致；该线索未作判据。

## 5. 口径落实声明

- **行集来源／分母／三数**（原始 → 排除 → 有效，全部行集定义与逐行明细落盘 `Valley Rampart/Logs/hh341_small_mine_byproduct_gate.txt` §六）：
  - `## S SRC_SELF`（MineByproductComponent.cs 全文件代码行·池内·分母 N=263）：原始 7 → 排除 0 → 有效 7；零计数项：无
  - `## S TS_REF`（TaskScheduler.cs 内含 `MineByproductComponent` 的行·池内·分母 N=3）：原始 3 → 排除 2（`:981`/`:1557` 注释行）→ 有效 1（`:1001` 代码行）；零计数项：无
  - `## S SEAM`（TaskScheduler.cs 协议接缝回调调用行·池内·分母 N=14）：原始 14 → 排除 0 → 有效 14；**零计数项＝其中命中本源的行数 0**（显式登记）
  - `## S DEFDATA`（`Assets/Resources/Buildings/*.asset` 内含 `comp.mine_byproduct` 的行·筛选集·分母 N=1）：原始 1 → 排除 0 → 有效 1（`mine.asset:17`）；零计数项：无
  - `## S ALLREF`（全 `Assets` 内含 `MineByproductComponent` 的行·全量·分母 N=30）：原始 30 → 排除 0 → 有效 30；同对象多口径**并列分列**：生产域 9 行（代码 5／注释 4）＋ Editor 域 21 行（探针/审计面，不入生产调用面）；零计数项：无
- **计数口径标签 6 项**：上列各表均含口径（池内/全量/筛选集）、行集谓词、分母、三数、非采样行分类（注释行/Editor 域行单列）、同字段多口径声明（ALLREF 内生产域/Editor 域并列，未混列）。
- **静态轮不适用声明（显式，⛔ 不以静态阅读冒充观察窗）**：
  - 观察窗 7 项（`L-98`）：**不适用**——本轮无进局、无派工读数、无时间窗测量；
  - 量纲（游戏秒/墙钟折算、短窗倍率）：**不适用**——无 `Time.time` 差／`Stopwatch`／`WaitForSeconds` 读数；
  - 格式串纪律（仅 "0.0"/"0.00"）：**不适用**——本轮无自报实测浮点值，全部读数为行号/计数整数；
  - `L-97` 满仓补条（`UsedSpace`/`FreeSpace`/`IsFull`）：**不适用**——本轮未声称任何「满仓/超容/容量不足」结论；
  - `L-99` 回滚前置检查：**不适用**——本轮未执行任何回滚类命令（回退手段表述＝`git show <rev>:<path>` 覆盖还原）；
  - 每次读数即时刷盘：已执行（取证完成即落盘 gate 文件，见 §6）；`git rev-parse HEAD` 开工已实测（§1），收工复测见 §6 第 4 步附注。

## 6. 落盘核验闸门 4 步（§十一-9 · 交付硬门槛）

1. **`mtime` 前后变化核验**（实测值）：
   - 证据文件 `Valley Rampart/Logs/hh341_small_mine_byproduct_gate.txt`：第 1 次写入后 `mtime=2026-09-29 12:55:25.784`（len=17054）；补全 git status 全录后 `mtime=2026-09-29 12:57:26.241`（len=18672，207 行）——前后有变化 ✓（该值为本报告写作时点的证据文件实测态；提交后按 §6-4 追记复验行时 mtime/hash 会再变，变化由 §十 自记）
   - 本报告 `多Agent交接/执行端/HH.341_MineByproduct预检窗口_交付报告.md`：**写入前＝不存在**（新建）；第 1 次落盘后实测 `mtime_pass1=2026-09-29 12:59:33.414`（len=18848）；终版经 Shell 复制覆盖落盘后 `mtime_final` 实测值登记于证据文件 §十——前后有变化 ✓（终版 mtime 物理上无法于写入前预知，故登记于证据文件终验行）
2. **磁盘内容重新读取**（⛔ 不复用内存缓冲）：两文件均以 `Get-Content -Raw` 从磁盘重读 ✓
3. **内容 hash／逐字比对**：证据文件（截至本报告写作时点）`sha256=1DA70323E83DD2EC71AC03DA1C8D2CFFBAF5D014C32115288B14EEC5CD2D64FF`，重读头部/尾部锚点逐字一致 ✓；报告终版 `sha256` 于落盘后实测、登记于证据文件 §十（自持值物理上无法于写入前预知），重读逐字比对 ✓
4. **已提交文件与 `git show <commit>:<path>` 对比**：于具名 commit 落地后执行 `git show` 取出与磁盘逐字比对；比对结果与 commit hash、收工 `git rev-parse HEAD` 复测值登记于证据文件 `Valley Rampart/Logs/hh341_small_mine_byproduct_gate.txt` §十（提交后追记——证据文件不入提交，故不违反「报告 commit 只含本报告 1 文件」）。

## 7. 自报瑕疵

1. 证据文件首版曾将 untracked 明细压缩为「约 26 项」，与「全量逐行照录」口径不符——已当场修正为 72 项逐行全录并重走核验（mtime/hash 以修正后值为准）。
2. **报告写入过程发生 3 次写工具静默失败**：对报告的 2 次 Edit 与 1 次 Write 均回报成功但磁盘内容与 mtime 未变（12:59:33.414 复测坐实），系 `L-96` 同族「报成功但未落盘」问题再现；已改走「临时文件＋Shell 复制＋逐项磁盘复验」完成终版（闸门 1-3 步以终版实测为准）。无其他已知瑕疵。

## 8. 【请裁】（≤5 条）

1. **准开第 3 源 `MineByproductComponent` 施工窗口**：预检 4 项完成、判定＝任务卡源 ⇒ 按 §5.1／§七九步＋§八九条**另开施工轮**；是否准予，请主策划裁。
2. **接缝形态**：本源当前零协议接缝（五类接缝命中 0）。若准施工，接缝补齐应按第 2 源 `ChestEntity` 同型先例（A/B/C/D 四类回调＋完成接缝 E，类型判定处增列本源）办理，是否照此执行请裁。
3. **观察项 V-1**：Production 完成动作对本源为空操作（`TaskScheduler.cs:849-852` 取不到 `ProducerComponent`）——施工验收时「完成」判据是否改以「在岗门＋`HasWorkerAssigned` 语义」承载，请裁。
4. **观察项 V-2**：AI(>0) 搬 `Ore` 落通用卸货分支（`TaskScheduler.cs:1038` 特支未含 Ore）——是否属需处理缺口，请裁（本轮不处理）。
5. 无其余待裁事项；⛔ 未自定下一源顺序、未动判据、未声称单管理器达成。

## 9. 源级结论

**本轮未施工 ⇒ `MineByproductComponent` 源级＝未开始 · 预检待裁**（未判通过／未判未通过；未自行推进施工；未自定下一源）。
预检结论：**任务卡源**，四态（可接取／可移动／可完成／可放弃）在 Production／Transport 两分支均有代码面调用链证据；下一步唯一动作＝【请裁】① 待裁定。
