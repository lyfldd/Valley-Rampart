# HH.299 · `HH.294` 片 1／片 2 **验收补正批** · 交付报告

> 执行端：**执行端（本会话）**｜2026-09-16｜取号 **HH.299**（水位线 `HH.297` → 本批 `HH.298` 回执 ＋ `HH.299` 本报告）
> **唯一要求源**：`多Agent交接/策划端/HH.294_片1片2_验收报告.md`（`05bc807d`）＋ 补正提示词（`540b7e7c`，`D767`）
> **依据**：`最高优先级文档/底层执行计划.md`（批 0／批 1-B,1-C,1-F）／`02_空间与粒度.md` §六,§七 ／ `_策划教训库.md`（`L-15`/`L-24`/`L-26`/`L-30`/`L-37`/`L-40`）
> **口径声明（`L-35`）**：本报告每条读数均写**口径来源**与**排除项**；禁用形容词（`L-30`）；禁上限句（`L-37`）。
> ⚠️ **账本**：执行端**未改**策划端四档（`_任务队列.md`／`_当前快照.md`／`_编号登记.md`／`测试基线台账.md`）；应登记项见 §十。

---

## 〇、结论摘要（先给数字）

| 件 | 状态 | 判据达成（逐条） |
|---|---|---|
| **P1** 遍历点清单补全 ＋ 运行期全图扫处置 | ✅ **闭合** | 单次遍历 **147,456 → 13,064 格**；单次耗时 **1.099 → 0.374 ms**；⭐ 真实右键一次 **0.904 ms／26,128 格**（不触发全图遍历）；320 点取样**逐点一致 0 处不一致** |
| **P2** 连带改动**实际 diff 面**补声明 | ✅ **闭合** | 13 资产逐个给 `allowedTerrain` 改前/改后序列值 ＋ 新增字段名清单（计数）＋ 回填值＝C# 默认值逐字段行号对照；`mine.asset` 单独标注 |
| **P3** 4 个 `allowedTerrain=[]` 资产非玩家路径（⭐必闭） | ✅ **闭合** | AI 建造候选集 **15 个 def id**、立国预置 **7 个 def id** ⇒ 4 个 id **命中 0**；全文**未出现**「语义等价／无影响」字样 |
| **P4** `HH.293 B3` 三读数补落盘 | ✅ **闭合** | `Valley Rampart/Logs/hh294_hh293_probe.log`（两图关键三行原文见 §四） |
| **P5** 陈注释勘正 | ✅ **落地** | 三处改后原文行 ＋ 全库清点（`L-15`）清单外 **+1 处**同批勘正 |
| **C2** 片 3 判据口径确认 | ✅ **已确认·未施工** | 见 §六 |
| 编译／收尾 | ✅ | **0 error**；**0 新增 warning**（存量 17 条·均非本批改动行）；`isPlaying=False`（三态退 Play）；`AI.Core` 目录命中 **0** |

---

## 〇之二、双列对照总表（**改前 vs 改后**）

| 件 | 项 | **改前** | **改后** |
|---|---|---|---|
| P1 | 片 1 遍历点清单条数 | 9 项（**漏 #10**） | **10 项**（含 #10 `GuardDeploymentSystem`·右键派兵触发） |
| P1 | `FindNearestResourceNode` 单次遍历格数 | **147,456**（W×H） | **13,064**（索引条目数） |
| P1 | 单次耗时（中位·15 次） | **1.099 ms** | **0.374 ms** |
| P1 | 一次右键（两遍）遍历格数／耗时 | 294,912 格／未实测（同算法×2·保守 >2.2 ms） | **26,128 格／0.904 ms**（真实路径·守卫区域 0→1） |
| P2 | 报告披露面（`HH.297` §五） | **意图面**（3 smoke ＋ 1 探针） | **实际 diff 面**：13 资产 **+265/−9**（回填 256 行）＋ `ResourceGenConfig.asset` **+3/−18** ＋ 逐字段默认值对照 |
| P2 | `allowedTerrain` 序列值（13 资产） | 见 §2.1 左列 | 见 §2.1 右列（8 迁移／4 同值／`mine` 4→4 标注） |
| P3 | 4 资产非玩家路径 | **未核**（`HH.297` §六-2 自陈） | AI 建造候选集 **15** def id／立国预置 **7** def id ⇒ **命中 0** |
| P4 | `HH.293 B3` 三读数落盘 | 仅 `Debug.Log`（磁盘**不可复核**） | `Logs/hh294_hh293_probe.log`（**1,265 B**·两图三行齐） |
| P5 | 陈指引串（`IsOccupied`／`[Quarry]`／`_terrain`） | **4 处**（`GridTypes:83/190-191`、`Portal:6`、`GridTypes:163`） | **0 处**（残留命中全为「删除留痕注释」） |

---

## 一、P1（片 1）· 全图遍历点清单补全 ＋ 运行期全图扫处置 —— ✅ 闭合

### 1.1 清单补入（`HH.296` §2.3 已加 **#10**）

| # | 遍历点（`file:line`） | 遍历对象 | **触发频率** | 下移后 |
|---|---|---|---|---|
| **10** | `GuardDeploymentSystem.FindNearestResourceNode`（`GuardDeploymentSystem.cs:256-282`·改造前形态） | `map.features` 全图嵌套 `for y<height / x<width`，**每命中资源格另调 `CoordToWorld`** | ⭐ **每次玩家右键派兵**：`SelectionController.cs:270`（判 `nearResource`）＋ `:279` `DeployGuard` → `GuardDeploymentSystem.cs:120` **再查一次** ⇒ **一次右键走两遍** | 384²：147,456 格/遍 ⇒ 一次右键 **294,912 格** |

（`HH.296` §2.3a 已同步写明处置与读数；本报告 §1.3~§1.5 为完整版）

### 1.2 处置声明（**所选＝案 A**：改为「索引查询」，不再全图扫）

- 索引＝「**曾出现过可守卫资源点（Tree/Mine/OreVein）的格号** ＋ 预换算世界坐标」，**地块级**（与 `W×H` 解耦）；
- 查询逐条**回读实时 `features`** 校验（被消耗 ⇒ 跳过）；距离式 `dx*dx+dy*dy` 与改造前 `((Vector2)CoordToWorld(c)-pos).sqrMagnitude` **逐位同式**；
- ⭐ **超集不变量**（索引只增不减仍正确）：运行期对 `features` 的资源点写入**只有两处** —— `WorldManager.TryConsumeResourceNode:241`（→Plain·消耗，由回读兜住）与 `ResourceRespawnSystem.SetFeature:178`（重生写回**原格**，经 `NotifyFeatureWritten` 增量登记）⇒ 索引恒为「当前可守卫资源点格」的**超集**；
- 并列取**格号小者**胜＝改造前 `y*W+x` 扫描序先者胜（确定性口径不变）。

### 1.3 双列对照（改前 vs 改后·**同局同图同落点**）

| 项 | **改造前** | **改造后** |
|---|---|---|
| 实现 | 全图嵌套扫 `map.features`（147,456 格）＋每命中格调 `CoordToWorld` | 索引查询（遍历＝索引条目数）＋回读校验 |
| 单次遍历格数 | **147,456**（W×H） | **13,064**（索引条目数） |
| 单次耗时（中位·15 次） | **1.099 ms** | **0.374 ms**（**2.94×**） |
| 一次右键遍历格数 | **294,912**（两遍·全图） | **26,128**（两遍·索引） |
| 一次右键耗时（真实路径） | 未实测（同算法×2 保守 >2.2 ms） | **0.904 ms** |
| 装载期代价 | — | **1 次/图**全图扫（建索引·`WorldManager.cs:124`；含在既有生成耗时基线内） |
| 索引内存 | — | **≈204 KB**（13,064 条 ×（4B 格号 ＋ 4B×2 世界坐标）＋ 去重 `HashSet`） |

### 1.4 逐条判据实测读数（**口径来源 ＋ 排除项**）

- **口径来源**：`Valley Rampart/Logs/hh294_p1_probe_v2.log`（21:11:20）——正门 `TestHarnessApi.EnterTestRun`（`mapSeed=worldSeed=21107`·`difficulty=2`·`WorldSize.Large`=384²）＋ 15 次取中位；**「改造前」列＝同局 A 段老实现副本**（逐字复制 `git show 919580e3:...GuardDeploymentSystem.cs` 的 `FindNearestResourceNode` 主体；差异仅：去 `out` 形参、调用本探针同名辅助 `IsGuardFeature`/`FeatureDisplayName`，逻辑逐行等价。**改造前生产代码已不存在** ⇒ 无法再跑真身，故以逐字副本作基线，**此处如实声明**）。
- **排除项**：未驱动真实鼠标输入链（`InputManager` → `RightClickPressedEvent` → `ScreenToPaint` → `IssueRightClick`），而是**直调分派函数据入口** `SelectionController.IssueRightClick(world)`（＝右键分派的实函数）；`Physics2D.OverlapPoint` 的 **Follow 分支未触发**（落点不在己方单位上）；探针为把路径推进到 `:279 DeployGuard` 分支，**布了 1 个 Warrior 作测试载体**（该局 D1 玩家无士兵 —— 实测 14 单位/士兵 0）⇒ **已在日志显式标注**；读数取自**编辑器 Mono**（非真机/构建版）。

| # | 判据（验收报告 §② 1-B③ / 提示词 P1） | 期望 | **实测** | 判定 |
|---|---|---|---|---|
| 1 | ⭐「右键派兵一次**不触发全图遍历**」⇒ 给改造前/后遍历格数两个数字 | 改造前 147,456；改造后＝具体有限值 | 改造前 **147,456/遍**（一次右键 294,912）；改造后 **13,064/遍**（一次右键 **26,128**） | ✅ **成立** |
| 2 | 实测：「选中一个远离所有资源点的单位 → 右键」一次调用耗时（ms）＋遍历格数 | 数字 | 单位＝`Human_Player_Warrior` @ (244.6, 120.3)（距最近可守卫资源点 **4.91** 世界单位）；`IssueRightClick` 中位 **0.904 ms** ／ **26,128 格**；守卫区域数 **0 → 1**（⇒ `:279 DeployGuard` 分支**确已走到**，非空跑） | ✅ |
| 3 | 落点有效性（避免「探针自己没跑到」） | 有值 | 320 点取样全部有值（`有值点 320/320`） | ✅ |
| 4 | ⭐ 结果不漂移（改造前后取点一致） | 0 不一致 | 随机 240 点 ＋ 资源点贴脸 80 点 ⇒ **不一致 0** | ✅ |
| 5 | 片 3 后单次成本预算（ms） | 数字 | 见 §1.5 | ✅ |

### 1.5 片 3 后单次成本预算（⭐ 预算项·两种口径都给）

| 口径 | 单次（一遍） | 一次右键（两遍） | 依据 |
|---|---|---|---|
| **甲（现状契约）**：`MapData.features` 维持**地块级** `W×H`（`03`／`WorldState.cs:27` 契约；片 3 下移的是 `_walkFlags`/`_occupants`） | **0.374 ms**（13,064 格） | **0.904 ms**（26,128 格） | 实测（=`v2` 日志） |
| **乙（验收报告 §④-3 的 ×16 假设）**：`features` **本身**也下移小格子 | ≈ **5.98 ms**（≈209,024 格） | ≈ **11.97 ms** | **线性外推·非实测**（条目数与遍历成本同比例） |

⚠️ 无论甲乙：**「远离资源点右键」是该点的最坏情形**（全图扫时成本与位置无关；索引查询时该情形遍历全部候选）。若片 3 后需再降：备选＝**有界半径近似**（**改行为**：远处右键不再吸附 ⇒ 须策划端裁）或 **分块剪枝／kd 结构**（结构升级）——**本批未做，列报**。

### 1.6 实际 diff 面（连带改动·**非越界**声明）

| 文件 | 改动 | 行数 |
|---|---|---|
| `Valley Rampart/Assets/_Game/Systems/AI/Formation/GuardDeploymentSystem.cs` | 索引（字段＋`RebuildResourceIndex`＋`NotifyFeatureWritten`＋读口 `IndexedResourceCells`）＋ `FindNearestResourceNode` 查询重写 ＋ `Clear()` 复位索引 | **+87 / −15** |
| `Valley Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs` | `SetFeature` 内 **＋1 行**登记（`GuardDeploymentSystem.NotifyFeatureWritten(cell, target);`） | **+2 / −0**（含注释 1） |
| `Valley Rampart/Assets/_Game/Systems/World/WorldManager.cs` | 建图后 **＋1 行**预建（`GuardDeploymentSystem.RebuildResourceIndex(playerMap);`） | **+3 / −0**（含注释 2） |
| `Valley Rampart/Assets/Editor/Smoke/Valley_HH294_P1Probe.cs`（新） | 探针（Editor-only） | **+372**（新文件） |

⚠️ **层级声明**（防 `F-09` 同名字段跨层误读）：`Systems/AI/Formation/**` 是**行为规则层**，**不是** `Systems/AI.Core/**`（决策核）⇒ 本批 **`AI.Core` 命中 0**（`git diff --name-only | grep AI.Core` ＝ 0）。

### 1.7 排除项（未覆盖·如实列出）

1. 「改造前」耗时取自**逐字副本**而非改造前生产码（生产码已删）——理由与差异点见 §1.4 口径来源。
2. 未做**真机/构建版**读数（与 `F-10` 同挂账口径）。
3. 未驱动真实鼠标事件链（直调分派入口）；`Follow` 分支未触发。
4. 未验证**资源点被消耗后**索引回读路径的端到端行为（代码面成立＝回读 `features`；探针点集 320 点中含贴脸资源点，但**未做「先消耗一格再查」专项**）——列报，可并入片 4（`features` 收口）一并验。

---

## 二、P2（片 2）· 连带改动补声明 —— **报「实际 diff 面」** —— ✅ 闭合

### 2.1 逐资产 `allowedTerrain` 改前 → 改后（**原始行**，`git diff 919580e3^ 919580e3 -- <path>`）

| 资产 | 改前 | 改后 | 语义 |
|---|---|---|---|
| `tree.asset` | `03000000` | `01000000` | Forest(3) → **Tree(1)** |
| `wood_pile.asset` | `03000000` | `01000000` | Forest(3) → **Tree(1)** |
| `stone_pile.asset` | `0400000002000000` | `04000000` | 去 Hills(2)，留 **Mine(4)** |
| `ore_vein.asset` | `0400000005000000` | `0400000003000000` | Snow(5) → **SnowMountain(3)**，留 Mine(4) |
| `portal.asset` | `0100000006000000` | **（空）** | Wasteland(1)＋Coast(6) → `[]` |
| `rift.asset` | `0100000006000000` | **（空）** | Wasteland(1)＋Coast(6) → `[]` |
| `ruins.asset` | `0100000002000000` | **（空）** | Wasteland(1)＋Hills(2) → `[]` |
| `treasure_box.asset` | `010000000600000002000000` | **（空）** | Wasteland(1)＋Coast(6)＋Hills(2) → `[]` |
| `mine.asset` | `04000000` | `04000000` | ⭐ **数值未变（4→4）·仅语义重解释**（原＝`TerrainType.Quarry`，现＝`FeatureType.Mine`；读值同为 4 —— **单独标注·非迁移**） |
| `castle.asset` | `00000000` | `00000000` | 数值未变（空校验） |
| `farmland.asset` | `00000000` | `00000000` | 数值未变 |
| `arrow_tower.asset` | `00000000` | `00000000` | 数值未变 |
| `wall.asset` | `00000000` | `00000000` | 数值未变 |
| （另）`ResourceGenConfig.asset` | `terrain:`＋`plainSubState:` 两键（4 组） | `feature:` 单键（3 组） | 键收敛（＋3/−18 行） |

（十六进制＝`FeatureType[]` 序列化值序：`Plain 0 / Tree 1 / Mountain 2 / SnowMountain 3 / Mine 4 / OreVein 5 / StonePile 6 / WoodPile 7 / River 8 / Ocean 9`；括号内数为**旧 `TerrainType`**（`git show 919580e3^:...GridTypes.cs` 实读·11 项全列）值序：`Plain 0 / Wasteland 1 / Hills 2 / Forest 3 / Quarry 4 / Snow 5 / Coast 6 / Mountain 7 / River 8 / Lake 9 / Ocean 10`。⚠️ 两套值序**不同层**，不得混读——`F-09` 家族纪律。`mine` 的 4 在两套里分别是 `Quarry(4)` 与 `Mine(4)`，**数值巧合相同、语义不同**。）

### 2.2 每资产 **实际 diff 面**（文件／新增行数／新增字段名清单·计数）

> 口径：`git diff -U0 919580e3^ 919580e3 -- <path>` 的 `+` 行；`numstat` 为 `＋/－`。`allowedTerrain` 行与 `description` 行**单列**（不混入回填清单）。

| 资产 | ＋/− | `allowedTerrain` 行 | 回填字段名清单（计数） |
|---|---|---|---|
| `tree.asset` | +26 / −1 | 1（迁移） | metal, stoneAmmo, fireballAmmo, magicAmmo, heightLayer, canPlaceOnWater, isBridge, isGate, rotatable, moduleType, attackCooldown, crewRequired, crewRadiusCells, isBlacksmith, isSiegeWorkshop, concurrentWorkers, trainingSlots, unlockLevel, maxHp, gatherSeconds, monsterTargetValue, monsterIsHighValue, raceId, uniquePerKingdom, isMineByproduct（**25**） |
| `wood_pile.asset` | +25 / −1 | 1（迁移） | 同 `tree` 串（**24**＝25 行 − 1 行 `allowedTerrain`） |
| `stone_pile.asset` | +25 / −1 | 1（迁移） | 同 `tree` 串（**24**） |
| `ore_vein.asset` | +25 / −1 | 1（迁移） | 同 `tree` 串（**24**） |
| `portal.asset` | +29 / −2 | 1 ＋ description 1 | metal, stoneAmmo, fireballAmmo, magicAmmo, heightLayer, canPlaceOnWater, isBridge, isGate, rotatable, moduleType, attackCooldown, crewRequired, crewRadiusCells, outputResource, isResourceNode, isBlacksmith, isSiegeWorkshop, concurrentWorkers, trainingSlots, unlockLevel, maxHp, gatherSeconds, monsterTargetValue, monsterIsHighValue, raceId, uniquePerKingdom, isMineByproduct（**27**） |
| `rift.asset` | +28 / −1 | 1 | 同 `portal` 串除去 `description`（**27**） |
| `ruins.asset` | +19 / −1 | 1 | metal, stoneAmmo, fireballAmmo, magicAmmo, heightLayer, canPlaceOnWater, isBridge, isGate, rotatable, attackCooldown, isBlacksmith, isSiegeWorkshop, gatherSeconds, monsterTargetValue, monsterIsHighValue, raceId, uniquePerKingdom, isMineByproduct（**18**） |
| `treasure_box.asset` | +26 / −1 | 1 | 同 `tree` 串（**25**） |
| `mine.asset` | +2 / −0 | 0（**数值未变**） | raceId, uniquePerKingdom（**2**） |
| `castle.asset` | +3 / −0 | 0 | raceId, uniquePerKingdom, isMineByproduct（**3**） |
| `farmland.asset` | +25 / −0 | 0 | 同 `tree` 串（**25**） |
| `arrow_tower.asset` | +16 / −0 | 0 | stoneAmmo, fireballAmmo, magicAmmo（**×3 组**：`cost` ＋ `levels[].upgradeCost`×2）, attackCooldown, isSiegeWorkshop, monsterTargetValue, monsterIsHighValue, raceId, uniquePerKingdom, isMineByproduct（**16**） |
| `wall.asset` | +16 / −0 | 0 | 同 `arrow_tower` 串（**16**） |
| **合计** | **+265 / −9** | 迁移行 **8** ＋ 转义行 **1** | 回填字段行 **256**（＝265 − 8 − 1） |

⚠️ 纠偏说明：`mine.asset` 的 `allowedTerrain` **不在** `+` 行内（原行与新行**同值**，git 未产生该行 diff）⇒ 与 §2.1 的「数值未变·仅语义重解释」互相印证。

### 2.3 核验口径：**回填值 ＝ C# 字段默认值**（逐字段对照 `BuildingDef.cs` 行号）

| 回填字段（实测值） | C# 声明处 | 默认值 | 一致 |
|---|---|---|---|
| `metal: 0`／`stoneAmmo: 0`／`fireballAmmo: 0`／`magicAmmo: 0`（在 `cost` 与 `levels[].upgradeCost` 内） | `WorldConfig.cs:143-146`（`struct ResourcePack`） | int 默认 `0` | ✅ |
| `heightLayer: 0` | `BuildingDef.cs:30` | `0` | ✅ |
| `canPlaceOnWater: 0` | `BuildingDef.cs:32` | `false` | ✅ |
| `isBridge: 0`／`isGate: 0`／`rotatable: 0` | `BuildingDef.cs:34/36/38` | `false` | ✅ |
| `moduleType: 0` | `BuildingDef.cs:42` | 枚举默认 `0` | ✅ |
| `attackCooldown: 0`（在 `combat` 内） | `BuildingDef.cs:143`（`struct CombatConfig`） | `0` | ✅ |
| `crewRequired: 0`／`crewRadiusCells: 0` | `BuildingDef.cs:52/54` | `0`／`0f` | ✅ |
| `outputResource: 0`／`isResourceNode: 0` | `BuildingDef.cs:58/63` | 枚举 `0`／`false` | ✅ |
| `isBlacksmith: 0`／`isSiegeWorkshop: 0` | `BuildingDef.cs:65/67` | `false` | ✅ |
| `concurrentWorkers: 0`／`trainingSlots: 0`／`unlockLevel: 1` | `BuildingDef.cs:71/73/76` | `0`／`0`／`1` | ✅ |
| `maxHp: 100`／`gatherSeconds: 2` | `BuildingDef.cs:84/92` | `100`／`2f` | ✅ |
| `monsterTargetValue: 1`／`monsterIsHighValue: 0` | `BuildingDef.cs:101/103` | `1f`／`false` | ✅ |
| `raceId: -1`／`uniquePerKingdom: 0`／`isMineByproduct: 0` | `BuildingDef.cs:107/109/113` | `-1`／`false`／`false` | ✅ |
| `description`（`portal`·`\u00D7` → `\xD7`） | — | **非回填**：同一字符 U+00D7（×）的两种 YAML 转义写法 | ⚠️ 值同·写法异 |

⇒ **结论（口径化）**：13 资产的新增行**逐字段值为 C# 字段默认值**（27 个字段位全覆盖），故**回填＝Unity 资产重存副作用，非人手改值 ⇒ 零行为变化**；唯一需注意的落点是 `allowedTerrain` 的**语义重解释**（`mine` 同值）。**归入「连带改动·非越界」**。

### 2.4 `ResourceGenConfig.asset`（第 14 件）

`git diff -U0 919580e3^ 919580e3 -- .../ResourceGenConfig.asset` ＝ **+3 / −18**：4 组 `terrain:`＋`plainSubState:` 两键 → `feature:` 单键（键由两键收敛为「地表物」单键，删 6 条 `producerMin/Max`＋`pickupMin/Max` 中的 2 条重复组）。**归入「连带改动·非越界」**；⚠️ 该资产**经 `HH.297` §六-1 列报为孤儿资产**（零外部调用），**删除条目请追认**（未变）。

---

## 三、P3（片 2·⭐ 唯一必闭）· 4 个 `allowedTerrain=[]` 资产**非玩家路径**核查 —— ✅ 闭合

### 3.1 两个必查路径的读数（**含口径来源**）

| 路径 | **口径来源（读点）** | 候选集（实测） | 计数 | `treasure_box`/`portal`/`rift`/`ruins` 命中 |
|---|---|---|---|---|
| **AI 建造候选集** | ① `Assets/_Game/Resources/Config/Kingdoms/UtilityActionConfig.asset`（`KingdomBrain.ExecuteBuildFocus` 经 `UtilityActionConfig.LoadConfig()` 取 `buildingId`；实读文件 27 条 action、其中 **16 条含 `buildingId`**）② `KingdomDiagnosisConfig.cs:51-52`（分诊 → `farm`/`quarry`）③ `KingdomBrain.cs:1310/1313`（硬编码 `"Granary"`/`"Warehouse"`） | `{House, Warehouse, quarry, farm, Granary, wall, Well, Blacksmith, WarAcademy, WarCamp, LeyForge, ArcheryRange, Barracks, TrainingCamp, SiegeWorkshop}` | **15**（唯一 def id） | **0** |
| **立国预置路径** | 6 × `Assets/Resources/Config/Kingdoms/Kingdom_*.asset:32 baseBuildingDefIds`（`KingdomFoundry.cs:156-165` 逐 id `FindDefById`） | `{castle, House, farm, Well, mine, Warehouse, quarry}`（6 资产并集·**逐字相同**） | **7** | **0** |

### 3.2 全路径盘点（为什么这两条路径是「会受 `allowedTerrain` 影响」的全部）

- **过 `PlacementValidator.ValidatePlacement` 的调用点全集**（`git grep'ValidatePlacement('`）＝ 4 处：`BuildController.cs:123`（玩家预览）／`BuildController.cs:240`（玩家落地）／`PlacementScorer.cs:106`（**AI 建造**）／`PlacementValidator.cs:171`（`CanPlace` helper）。⇒ **`allowedTerrain` 只在「玩家菜单」与「AI 建造」两条链上被消费**；
- **非玩家但不过 `Validator` 的直建路径**（`CreateBuildingInstance`）：`BuildingFactory.InstantiateFromMap`（地图预置）／`BuildingFactory` 读档 `SpawnFromSave`／`KingdomFoundry.cs:162/215/216/472/496`（预置 castle/wall/gate/Well）／`VagrantCampSystem.cs:211` ⇒ **这些路径不读 `allowedTerrain`**，本次「由恒拒变跳过」的放宽在其中**不产生行为**；
- **全库字符串检索**（4 个 def id）：命中仅 `BuildingVisual.cs:56/57/69/70/82-84`（美术键）＋ `R5_SixStage.cs:232`（ChainAudit 命名别名映射表）＋ `ChestEntity.cs:7/50`（宝箱独立管线注释）⇒ **零额外建造调用点**；
- **玩家菜单**：`BuildingMenuPanel.cs:209`（`if (def != null && def.isPlayerBuilt)`）过滤；4 资产实读 `isPlayerBuilt: 0` ⇒ 不进玩家菜单（与 `HH.297` 一致）。

### 3.3 结论表述（⭐ 措辞纪律遵守声明）

> **经查：AI 建造候选集（15 个 def id）与立国预置路径（7 个 def id）均不含这 4 个 id（命中 0）⇒ 本次由「恒拒」变「跳过校验」的**放宽**，在这两条非玩家路径上**当前不可达**（无候选 ⇒ 永不进入 `ValidatePlacement`）。**
> ⚠️ **本报告全文未使用「语义等价」「无影响」** —— 契约层面 `[]` 与「恒拒」**并不相同**：一旦有朝一日把任一项加入候选集（AI SO 增 `buildingId` ／ `KingdomDef` 增预置 id ／ 新增调用点），行为即由「恒拒」变为「放行」。

### 3.4 改造案（**只报·本次不动**·另批）

| 案 | 内容 | 影响面 | 建议 |
|---|---|---|---|
| **①（窄·推荐）** | 新增显式声明位（如 `BuildingDef.noBuild`／复用语义明确的 `isPlayerBuilt`）＋ `PlacementValidator` 增一条「不可建列 ⇒ 拒」，把**恒拒写回契约层** | 4 资产 ＋ `Validator` 1 判定 | ⭐ 另批可带 |
| **②（宽·不建议本批）** | `PlacementValidator` 增「`def.isPlayerBuilt == false` ⇒ 任何方拒建」 | ⚠️ 波及**全部** `isPlayerBuilt=0` 资产 —— 实读 **12 个**（`castle, farmland, mine, ore_vein, portal, rift, ruins, stone_pile, treasure_box, tree, VagrantCamp, wood_pile`），须先做 AI／玩家／探针三面影响审计（例：`mine` 是 AI 第一代必预置物，虽走直建路径仍须核） | 不建议本批 |

---

## 四、P4（片 1）· `HH.293 B3` 三读数**补落盘** —— ✅ 闭合

- **文件**：`Valley Rampart/Logs/hh294_hh293_probe.log`（**1,265 B**·mtime **2026-09-16 21:08:30**）
- **改动**：`Valley_HH294_HH293Probe.cs` 加 `WriteLog(...)`（`Debug.Log` 之外写盘；沿用同批探针族 `Application.dataPath/../Logs/**` 口径）＋ 头部加环境/口径行；复跑取得。

**两张图·关键三行原文**（逐字摘录）：

```
---- [256²/Normal(diff2) · seed=21107] ----
  B3：矿洞簇（连通分量）总数=412 ｜ 大区块数=256 ｜ 簇数=0 的区块数=3（判据须 ≥1） ｜ 每区块簇数 min=0 max=3 均值=1.61
---- [384²/Normal(diff2) · seed=21107] ----
  B3：矿洞簇（连通分量）总数=924 ｜ 大区块数=576 ｜ 簇数=0 的区块数=1（判据须 ≥1） ｜ 每区块簇数 min=0 max=3 均值=1.60
```

（另：两图 `Mine nb=0`、`Mine 格=1704／3764`；末尾 `[确定性] 同 seed 两次：逐格一致 ✅（features+climateZones+spawns+nb）`。与验收报告 §④-5 引用的 `412/256`、`924/576`、`3/1`、`min/max/均值 0/3/1.60` **逐值吻合** ⇒ 现有磁盘件可复核。）

---

## 五、P5 · 陈注释勘正（三处 ＋ 全库清点 +1） —— ✅ 落地

### 5.1 三处改后**原文行**

1. `Valley Rampart/Assets/_Game/Systems/Grid/GridTypes.cs:190-191` ⇒

```csharp
    /// <summary>[过渡已废弃] 占据此格的建筑。改用 GridSystem.GetOccupant。</summary>
    [System.Obsolete("GridCell.occupant 已下沉 GridSystem._occupants，改用 GridSystem.GetOccupant")]
```

   （**所选＝「改指 `GetOccupant`」**：属性体本就调 `GetOccupant`（`:194`），仅指引串陈；`.occupant` 全库**零消费者**（grep）⇒ 保留属性、不删（删属结构改动，另批可议）。）

2. `Valley Rampart/Assets/_Game/Systems/Disaster/Portal.cs:6` ⇒

```csharp
//   Portal 非 Building 却可被 GridSystem.IsObstacle/GetOccupant 查询。
```

3. `Valley Rampart/Valley Rampart/Assets/_Game/Systems/Grid/GridTypes.cs:83` ⇒

```csharp
    Mine,              // 矿山锚点（地形；其上可建 mine（矿洞·建筑）／quarry）——≠ mine.asset（建筑，2×2，allowedTerrain=[Mine]）
```

### 5.2 全库清点（`L-15`：凡称覆盖全 X 面必先全库清点）⇒ **清单外 +1 处**同批勘正

`GridTypes.cs:163-164`（`GridCell` 类头注释仍把已删的 `_terrain` 列为「已下沉数组」）⇒

```csharp
/// doc 1 §2.2 / §5.3：occupant/isObstacle 已下沉到 GridSystem 稠密数组
/// （_occupants/_walkFlags；`terrain` 已于 HH.294 片2 随 TerrainType 整层删除），本 class 只承载稀疏懒分配的单位列表。
```

### 5.3 残留命中分类（改后 `git grep -n -I -E <pat> -- '*.cs'`）

| 检索串 | 代码位命中 | 分类 |
|---|---|---|
| `IsOccupied` | 1（`GridSystem.cs:197`） | 迁移留痕注释 |
| `GetPlainSubStateAt`／`SetTerrain` | **0** | — |
| `GetTerrainAt` | 1（`GridSystem.cs:174`） | 迁移留痕注释 |
| `allowedTerrain=[Quarry]` | **0** | — |
| `_terrain` | 5（`Valley2_17_Smoke_12/13` 各留痕注释＋`GridSystem.cs:14`） | 迁移留痕注释 |
| `PlainSubState`／`TerrainType` | 全为**删除留痕注释**或 `Valley_HH294_GrainProbe` 的「原数组等价复测」注释 | 无活引用 |

⇒ **无任何活代码指向已删 API**；陈指引串清零。

---

## 六、C2 · 片 3 判据口径确认（**只确认·不施工**）

已读并确认：① 片 3 内存/耗时判据须写成**能力句 ＋ 逐值对照**、**禁上限句**（`L-37`）；② 沿用**本批同口径**＝**384²（大图）· seed 21107 ·「三数组」（可走＋占格＋壳）**；③ 判据须写明**口径来源与排除项**（`L-35`）。
⛔ **片 3 未开工**（开工时点**待用户拍**；本批 `HH.298`/`HH.299` 亦不动片 3~6）。

---

## 七、编译与收尾

| 项 | 读数 | 口径来源 |
|---|---|---|
| 编译 | **0 error** | Unity `read_console`（`types=[error]`·`Retrieved 0 log entries`） |
| warning | **0 新增**（存量 **17** 条：`CS0114`×4／`CS0108`×2／`CS0252/0253`×5／`CS0414`×4／`CS8632`×1／`CS0618`×1） | 逐条所在文件**非本批改动行**（唯一同文件者＝`ResourceRespawnSystem.cs:23` 存量 `Awake` 隐藏告警；本批改动在 `:178-179`） |
| 退 Play（`L-32`） | `Application.isPlaying = False`；探针收尾 `ExitTestRun ＋ QuitSmoke` | `execute_code` 实读 ＋ 探针日志末行 |
| `AI.Core` | 目录命中 **0**（`Systems/AI/Formation/**` ≠ `Systems/AI.Core/**`） | `git diff --name-only` 过滤 |
| 行尾 | 本批改动 6 个文本文件**全 LF**（无 CRLF／混合） | python 二进制计数 |

---

## 八、证据索引

| 类型 | 路径 | 说明 |
|---|---|---|
| 原始读数 | `Valley Rampart/Logs/hh294_p1_probe_v2.log` | **P1 终版**（21:11:20·384²·seed 21107·15 次中位） |
| 原始读数 | `Valley Rampart/Logs/hh294_p1_probe_after.log` | P1 中间版（21:06:30·索引化 v1：B＝0.805 ms·未预换算世界坐标）——留痕迭代 |
| 原始读数 | `Valley Rampart/Logs/hh294_hh293_probe.log` | **P4**（21:08:30·256²／384² 两图＋确定性） |
| git | `git diff --stat`（本批） | `GuardDeploymentSystem +96/−16`／`ResourceRespawnSystem +2`／`WorldManager +3`／`GridTypes ±3(×2 处)`／`Portal ±1`／`HH.296 +27`／新增 `Valley_HH294_P1Probe.cs` |
| git | `git show 919580e3:"…/GuardDeploymentSystem.cs"` | P1「改造前」逐字基线源（副本比对用） |
| git | `git diff 919580e3^ 919580e3 -- <asset>` | P2 逐资产原始行 |
| 计数 | `git diff --numstat 919580e3^ 919580e3` | 13 资产 ＋3/−18 等（§2.2 合计 **+265/−9**） |

---

## 九、未完成项（**显式列出**·不打包）

1. **P1 ·「改造前」耗时取自逐字副本**（改造前生产码已删）——非「跑改造前真身」所得；差异点已在 §1.4 声明。
2. **P1 · 资源点被消耗后的索引回读路径未做专项端到端验证**（代码面成立；可并入片 4 `features` 收口批）。
3. **P1 · 未做真机/构建版读数**（与 `F-10` 同挂账口径）。
4. **P1 · 未驱动真实鼠标事件链**（直调分派入口；`Follow` 分支未触发）；探针为推进 `:279` 分支布了 1 个 Warrior（**测试载体**·已标注）。
5. **P3 · 改造案未施工**（按提示词「只报改造案·本次不动」）。
6. **延续挂账（本批未动）**：`HH.297` §六-1 `ResourceGenConfig` 孤儿资产追认／§六-4 `MapCellCount` 新死接口／§六-5 `Resources/Debug/Maps/map_0_seed12345.json` 1D 孤儿数据／`F-10` 判据②（需 Profiler·构建版）／判据④-乙（派工打散）；`HH.296` §五 其余列报项。

---

## 十、应登记项（同 `HH.298` §四·**共 9 条**·此处按落账文件归并为 7 项·执行端未落账）

1. `_编号登记.md` 水位线：`HH.297` → **`HH.299`**（新增 `HH.298` 回执／`HH.299` 本报告）。
2. `_编号登记.md` 在途区 `HH.294-补正` 行：`🟢 已签发·待接单` → **`🟡 已交付待验收（P1/P2/P3/P4 闭合·P5 落地）`**。
3. `_任务队列.md` `HH.294` 批行：片 1 判据「全图遍历点清单」补注「**含 #10 `GuardDeploymentSystem`（右键派兵触发）**」；片 2 判据补注「**实际 diff 面已声明（P2）**」「**非玩家路径已核（P3）**」。
4. `_任务队列.md`：新增 `HH.294-补正` 状态行（🟡 已交付待验收）。
5. `_当前快照.md` 在飞表 `0k` 行：片 1／片 2 → **「补正已交·待验收销号」**；水位线行／最后更新行同步。
6. `测试基线台账.md`：追加「P1 运行期全图扫处置」读数段（147,456→13,064／1.099→0.374 ms／右键一次 0.904 ms／320 点 0 不一致）。
7. `_策划教训库.md`：`L-15`＋1 实例（遍历点清单补正侧取证）／`L-02`＋1 实例（P4 读数落盘缺口）。

## 十一、请裁项

1. **P1 处置口径确认**：所选＝**案 A（索引查询）**，且改动含**跨 3 文件各 +1~2 行接缝**（`WorldManager` 预建／`ResourceRespawnSystem` 登记）——请裁「是否认可该接缝面」。
2. **片 3 预算口径**（§1.5 甲/乙）：`features` 是否随片 3 下移小格子 ⇒ 决定该点预算取 **0.374 ms** 还是 **≈5.98 ms**；若取乙口径，是否需要**有界半径近似**（＝改行为，需授权）。
3. **P3 改造案**（§3.4）：是否另批立「把恒拒写回契约层」（推荐案①）；案②须先做 12 资产影响审计。
4. **P1 §1.7-4**（消耗后回读专项）是否并入片 4。
