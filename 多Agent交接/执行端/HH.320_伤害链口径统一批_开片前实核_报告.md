# HH.320 · 伤害链口径统一批（批 1）· 开片前实核报告

- **执行端**：TraeCode｜**日期**：2026-09-23｜**性质**：只读实核 ＋ 报选（⛔ `Assets/**` 零写：无 Editor 探针、无场景/资产改动）
- **基线**：`git log --oneline -3` ⇒ `d5c56651`（HH.320 任务书签发 ＋ 口径修订）← `ef7c72a6`（M1 全片闭合）← `0c75b0a0`（第十五笔）
- **依据**：任务书 `多Agent交接/策划端/HH.320_伤害链口径统一批_任务书.md`（D843）／口径真源 `最高优先级文档/07_伤害层.md` §二 · §七 判据 10~12／`改造计划/2_5_伤害管线与战斗.md` 头部 L2 修订块
- **取证方式**：`git --no-pager grep`／`git log -S`／字段实读（`.asset` 原文）／`file:line`；行首 `§N` 与任务书条目一一对应

---

## §0 · 首段结论（先答最优先项）

**1️⃣ 相机：M8 前提【成立】——正交 ＋ 零旋转**（证据见 §1）⇒ 世界空间欧氏圆 ≡ 屏幕圆，本项**无「停手报裁」触发**。

⚠️ 但本轮实核发现 **2 条口径与实盘冲突（§3 目标选择视线链不存在／§4 阻挡位与挡箭不同源）**，另有 **4 条判据/边界问题**——共 **6 条待裁**，集中在文末 **§待裁**。其余 7 项已照核。

---

## §1 · 相机投影＋旋转（最优先 · 前提性）

| 项 | 实盘读数 | 位置 |
|---|---|---|
| 唯一相机 | 场景内 `Camera` 组件 **1 个**（`git grep -c "^--- !u!20 &"` = 1） | `Assets/Scenes/GameScene.unity:885` |
| 宿主 | `m_Name: Main Camera`／`m_TagString: MainCamera`（go `&519420028`） | `:871`／`:872` |
| **正交** | `orthographic: 1` ／ `orthographic size: 6.065553` | `:919`／`:920` |
| **旋转** | `m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}`（恒等） | `:946` |
| 位置 | `m_LocalPosition: {x: 0, y: 0, z: -10}`；`m_Father: {fileID: 0}`（根物件，无父链旋转） | `:947`／`:951` |
| 运行时改写 | 全库 `transform.rotation = Quaternion.Euler` **仅 1 处**：`BuildingComponents.cs:135`（建筑瞄准） | `git grep` 全库 |
| 相机脚本 | 场景 `m_Script guid: e941692a…`（字段名 `config`＋`inputEnabled` ＝ `CameraRig` 序列化面）；`CameraRig` 只写 `position`（`:101/139/164`）、`orthographicSize`（`:172`）、`_cam.orthographic = true`（`:42`）⇒ **无旋转写** | `GameScene.unity:962`／`CameraRig.cs` |

**影响面**：正交 ＋ 零旋转 ＋ 方形像素 ⇒ 世界 XY 平面到屏幕 = **均匀缩放 ＋ 平移**（`aspect` 只改可见范围，不改比例）⇒ 世界欧氏圆在屏幕上仍是圆 ✅。
**建议**：M8 无异议，不触发停手。

⭐ **附注（影响判据 2，非相机问题）**：等轴 2:1 下「格」有三种计数口径 —— ①网格轴向步长 **0.7155** 世界单位/格（＝菱形边＝半对角线）②屏幕横格 **1.28** 世界 x / 格 ③屏幕纵格 **0.64** 世界 y / 格。同一世界圆在三口径下折合格数**不同**（R=6 例：轴向 6 格／屏幕横 3.354 格／屏幕纵 6.708 格）⇒ 判据 2「横向 R 格内可打」须先定计数方向（见 §9 与 §待裁 6）。

---

## §2 · `GetCellSize()` 全调用面分类

### 2.1 四处同名实现（均读 `Config.cellSize.x`，**回退值不一致**）

| 位置 | 实现体 | 回退值 |
|---|---|---|
| `UnitController.cs:1103-1107` | `Config.cellSize.x` | **1f** |
| `NPCBrain.cs:1805-1809` | `Config.cellSize.x` | 2.26f |
| `TaskScheduler.cs:1501-1505` | `Config.cellSize.x` | 2.26f |
| `WanderAnchorPool.cs:132-136` | `Config.cellSize.x` | 2.26f |

### 2.2 调用点清单 ＋ 分类（22 调用点 = 本批 3 ＋ 挂账 18 ＋ 边界 1）

**A. 属本批（伤害链 · 件 2 表列）— 3 处**

| # | 位置 | 用途 |
|---|---|---|
| 1 | `UnitController.cs:1112` | 静态单位索敌射程 `attackRange × GetCellSize()`（`FindNearestEnemyInRange:1110-1113`） |
| 2 | `UnitController.cs:1154` | 乘员机器**开火**射程（`CrewMachineThinkCore`） |
| 3 | `UnitController.cs:1162` | 乘员机器开火前距离复核（同函数） |

**B. 边界项（战斗邻接 · 建议裁）— 1 处**

| 位置 | 用途 | 交裁理由 |
|---|---|---|
| `NPCBrain.cs:710` | 野性流浪汉攻击射程注入（`useGrid ? range : range × cs`；`UseWildAttackRange:705-712`） | 它是 **AI 决策核的 `AttackWorldRange`**（决定"想不想打"），与件 2 的"能不能打"是同一条战斗口径 ⇒ 若不同改，会出现"AI 认为在射程内、`DamageSystem` 判定在射程外"（反向亦然）。⚠️ 开关 `AIDistConfig.useGridUnits` 实读 = **1（true）**（`Resources/Config/AIDistConfig.asset:15`） |

**C. 非本批（同族挂账）— 18 处**

| 域 | 位置 | 用途 |
|---|---|---|
| 感知 | `NPCBrain.cs:536`／`:1036`／`UnitController.cs:1155`／`:1231` | 感知半径、归巢感知、乘员机器 reposition |
| 感知（野性） | `NPCBrain.cs:651` | `wildAggroRadiusCells × cs` |
| 编队/安全 | `NPCBrain.cs:888`／`:891`／`:1142` | `armyRadiusCells`、距王国锚点格数、编队槽位世界坐标 |
| 撤退 | `NPCBrain.cs:933` | `baseRetreatCells × cs0`（`EscapePointSampler` 半径） |
| AI 量纲 | `NPCBrain.cs:944` | `useGridUnits` 距离字段量纲 |
| 冲锋 | `NPCBrain.cs:1590`／`:1676` | `chargeRangeCells × cellSize`（终点/推进） |
| 移动 | `NPCBrain.cs:505` | `_executor.Execute(in cmd, dt, GetCellSize())` |
| 目标价值 | `UnitController.cs:1072` | AOE 密集判据 `aoeRadiusCells × 2 × cs`（`CountNearbyHostiles` 邻域） |
| 邻域查询 | `UnitController.cs:1196` | 乘员机器工人统计 `crewRadiusCells × cs` |
| 派工 | `TaskScheduler.cs:468` | `ArrivalThreshold(brain, cellSize)`（`:1463`；`brain.Config.arrivalThreshold × cellSize`） |
| 漫游 | `WanderAnchorPool.cs:78`／`:194` | 城堡锚点偏移 `offsets × cs`／`jitter = cs × 4` |

### 2.3 另：件 2 表内的"非 `GetCellSize`"落点（直读 `Config.cellSize.x`）

`BuildingComponents.cs:75`（`def.combat.range × cellSize.x`）／`:100`（再读一次）／`:104`（`ceil(rangeWorld / cellSize)`）；`ProjectileManager.cs:303`（`cellSize.x × 0.5f` 弹道带半宽）。

### 2.4 勘正（不影响施工，须改口径）

- ⚠️ 任务书 §件 2「同族待治」清单 `UnitController.cs:536/651/710/888/891/933/944/1036/1142/1590/1676` **实为 `NPCBrain.cs` 行号**（`UnitController.cs` 的 `GetCellSize` 行号是 `1072/1112/1154/1155/1162/1196/1231`）。
- ⚠️ `UnitController.GetCellSize()` 回退 **1f**，其余三处 **2.26f** ⇒ 无 `GridSystem` 时同一"格"含义不同（实战必就绪 ⇒ 无实害，登记备裁）。

---

## §3 · 视线／遮挡全调用面 ＋ 开关实际值（⚠️ 与任务书口径冲突）

### 3.1 调用点清单（实盘）

| 方法 | 定义 | 调用点 |
|---|---|---|
| `HasLineOfSight` | `CombatRules.cs:58-71`（微格 Bresenham；`:68` 判据 = `IsSubWalkable`） | ⛔ **全库零调用点** |
| `CheckWallBlock` | `ProjectileManager.cs:291-331`（世界带状 ＋ 弧高） | **1 处**：`ProjectileManager.cs:203`（`OnProjectileArrived` 首句） |

**零调用的取证**：
- `git --no-pager grep -n "HasLineOfSight" -- .` ⇒ 仅 3 条，全为**定义/注释/文档**（`CombatRules.cs:48` 注释、`CombatRules.cs:58` 定义、`DamageConfig.cs:60` Tooltip）。
- `git --no-pager log -S "HasLineOfSight(" --oneline -- Assets` ⇒ 仅 `e1a6fdf2`（2_5 核心 2D 化引入提交）**一条** ⇒ 自引入起**从未出现调用点**，也从未被删。

### 3.2 开关 `DamageConfig.remoteNeedsLineOfSight` 的实际值

- ⛔ **该字段名不存在**。实盘字段名 = `losEnabled`（`DamageConfig.cs:60` Tooltip／`:61` 声明）。
- **`.asset` 实读值**：`Assets/Resources/Config/DamageConfig.asset:28` ⇒ `losEnabled: 1`（= **true**）。
- ⚠️ **全库零消费点**（`git grep -n "losEnabled"` 仅 `.asset:28` ＋ `DamageConfig.cs:61`）⇒ 该开关当前**不产生任何行为**。

### 3.3 影响面

- 任务书 §一 #10 与 `07_伤害层.md` §四 记的「**遮挡双链**（目标选择 `HasLineOfSight` ／到达判定 `CheckWallBlock`）」在实盘是 **单链**：只有到达判定在役。
- ⇒ **件 3「遮挡双链合一」**、**件 5「高抛在目标选择阶段亦豁免视线」** 在实盘**无可改对象**（无调用点，等于"给死码改语义"）。
- 附：`CombatRules` 全体消费面 —— 仅 `TargetScore`（`CombatRules.cs:50-52`）在役，唯一消费方 = `MonsterAI.cs:196`（怪物 Raiding 选建筑，其"格距离" = `MonsterAI.cs:203-206` `Vector2.Distance / cellSize.x`）；`ComputeKnockback` 消费方 = `NPCBrain.cs:1703`。`DamageConfig.stickinessFactor`（`DamageConfig.cs:55`／asset `:25`）**零消费点**。

---

## §4 · 工事（fortification）是否改变格可走性（⚠️ 与件 4 落地式不同源）

### 4.1 实体形态（`FortificationDef` 系）：**不改变**格可走性

| 证据 | 内容 |
|---|---|
| 属性消费面 | `FortificationDef.blocksMovement` 全库消费点 **仅 2 处**：`UnitController.cs:1339`（"自身是墙"）／`:1352`（目标格内工事），均在 `IsBlockedByFortification`（`:1337-1356`）内 |
| 谁调它 | `NPCBrain.cs:1634`（**移动后置拦截**，实体级；不是格查询） |
| 是否入占格 | `UnitController` **不实现 `IGridOccupant`**；实现类仅 `Building.cs:29`／`Portal.cs:8`（`git grep IGridOccupant`） |
| 占格写口 | `MarkOccupied*`／`Free*` 全调用面（见 §5.2）**零工事路径** |
| 资产 | `blocksMovement: 1` 仅 `Resources/Fortifications/Wall.asset:17`／`Gate.asset:17`（四塔 `= 0`） |

### 4.2 建筑形态：**改变**格可走性

`Resources/Buildings/wall.asset`（`role: 3`=Wall ＋ `isObstacle: 1`）／`gate.asset`（`role: 3`＋`isGate: 1`＋`isObstacle: 1`）／`bridge.asset`（`role: 3`＋`isObstacle: 0`） ⇒ 走 `MarkOccupiedFootprint` 置 `BuildingBlocked`（桥走 `SetBridge` 豁免水阻挡）。

### 4.3 现状"为什么还能挡箭"

`CheckWallBlock`（`ProjectileManager.cs:308-324`）遍历 **`UnitRegistry`**，取 `uc.fortification != null`（且 `arcHeightCells ≤ heightCells`）的**单位**，按弹道带（半宽 `cellSize.x × 0.5`，`:303`）取最靠射手者 ⇒ **挡箭靠"单位侧工事实体"，与格可走性无关**。

### 4.4 影响面（⛔ 关键）

件 4 建议落地式「阻挡集合 ＝ 山壁（地形硬阻挡）＋ 建筑占格（`BuildingBlocked`）」**不含"单位侧工事"** ⇒ 落地后：
- 单位侧城墙/城门/塔（`UnitData/Wall|Gate|ArrowTower|CrossbowTower|MagicTower.asset` 挂 `FortificationDef`）**不再挡视线**，但**仍挡弹道**（`CheckWallBlock` 未变）⇒ 同一堵墙"挡箭不挡眼"。
- 反向：**非工事建筑**（isObstacle=1 的仓库/农场/主城等，见 §5.1）**会挡视线**，但**不挡弹道**（`CheckWallBlock` 只看工事）⇒ "挡眼不挡箭"。
⇒ 二链若不同源，会出现新的语义分裂（见 §待裁 2）。

---

## §5 · 建筑占格是否置 `BuildingBlocked`

### 5.1 置位条件

`GridSystem.cs:357-358`（`MarkOccupiedSub`）／`:381-382`（`MarkOccupied`）：`occupant != null && occupant.IsGridObstacle ⇒ |= BuildingBlocked`，否则清位。`IsGridObstacle` 真源：`Building.cs:88`（`=> isObstacle`，由 `def.isObstacle` 初始化 `:459`／`BuildingFactory.cs:132`）／`Portal.cs:77`（恒 true）。

### 5.2 写入面清单（全库）

| 类 | 位置 | 语义 |
|---|---|---|
| 置 | `BuildController.cs:319` | 建造落位 |
| 置 | `Building.cs:119`（`SetGateBlocking`） | 城门开/关**重标 footprint**（`:115-120`） |
| 置 | `Building.cs:1235` | 存档恢复兜底（幂等） |
| 置 | `BuildingFactory.cs:168` | 工厂生成 |
| 置 | `GridSystem.cs:440` | `PopulateFromMap` 按 `BuildingRegistry` 重建（装配期兜底） |
| 置 | `Portal.cs:160` | 传送门 2×2 |
| 清 | `Building.cs:1345`（`ReleaseLayerOwnedState`） | `FreeFootprint`（＋`:1347` 桥 `SetBridge(false)`） |
| 清 | `Portal.cs:166` | 传送门销毁 |
| 清（底层） | `GridSystem.cs:367`（`FreeSub`）／`:398`（`Free`） | 子格/地块释放 |

### 5.3 拆除时是否清位：**清**

`FinishDemolish`（`Building.cs:910-922`）→ `Die(DeathCause.Demolished)`（`:921`）→ `ReleaseLayerOwnedState`（`:1387`）→ `FreeFootprint`（`:1345`）✅。

⚠️ **例外（有意）**：`EnterRuined`（`Building.cs:1299-1328`）**保持占格**（D154「废墟不 Free · 可修复」）⇒ 废墟态**继续置 `BuildingBlocked`** ⇒ 按件 4 判据"废墟会挡视线"（见 §待裁 3）。

---

## §6 · 件 1 微格候选换算：方案 ＋ 超集证明（⛔ 不施工）

### 6.1 实盘读数

| 项 | 值 | 位置 |
|---|---|---|
| 格尺寸（资产） | `cellSize: {x: 1.28, y: 0.64}` | `Resources/Grid/GridConfig.asset:15` |
| **`subCellDivisor`（资产实读）** | **4** | `Resources/Grid/GridConfig.asset:16` |
| 子格世界尺寸 | `SubCellSizeOr() = CellSizeOr()/SubDiv` ⇒ **(0.32, 0.16)** | `GridSystem.cs:140`／`:143` |
| 子格连续坐标映射 | `gx = x/subW + y/subH`；`gy = y/subH − x/subW` | `GridSystem.cs:184-189`（`WorldToCellF`） |
| 现状换算 | `subRange = ceil(radiusCells * subDiv)` ⇒ 现配置 0.25 ⇒ **1** | `ProjectileManager.cs:343` |
| 同款拷贝（另 2 处） | `DamageSystem.cs:490`／`GroundEffectManager.cs:202`（同式同值） | — |
| 命中过滤（不变） | `dist = GridMath.DistCells(落点, 单位位) ≤ hitRadiusCells` | `ProjectileManager.cs:232`／`:256` |

### 6.2 问题：`radiusCells × subDiv` 不是超集（现状已可构造漏命中）

推导（码面推演）：世界位移 `(Δx,Δy)` ⇒ 子格连续坐标位移 `Δgx = Δx/subW + Δy/subH`、`Δgy = Δy/subH − Δx/subW`（两轴系数对称）。设判定半径 R（世界单位），则
`|Δg| ≤ R × √(1/subW² + 1/subH²)`，索引差 `≤ ⌈|Δg|⌉` ⇒ **必要子格范围 n = ⌈R × √(1/subW² + 1/subH²)⌉**。

- **现状（格单位 R=0.25）**：`n = ⌈0.25 × 4 × √(1/1.28² + 1/0.64²)⌉ = ⌈1.7469⌉ = 2`，而实码取 **1** ⇒ 不满足。
  可构造反例：取 `u=Δx/1.28=0.24`、`v=Δy/0.64=0.07`（`DistCells = √(0.0576+0.0049) = 0.25` **恰好达标**；世界位移 `(0.3072, 0.0448)`）⇒ `Δgx = 4(u+v) = 1.24 > 1` ⇒ 若落点子格小数部分 ≈0.99，则该单位子格索引差 = **2 > subRange 1** ⇒ **判定圈内却不进候选集 ⇒ 漏命中（假阴性）**。
- **新视觉口径（R=0.25 视觉格）**：`R_world = 0.25 × 0.7155 = 0.17891` ⇒ `n = ⌈0.17891 × 4 × 1.74693⌉ = ⌈1.2501⌉ = **2**`。

### 6.3 方案（推荐）＋ 超集证明

**方案 A（推荐 · 最简可证）**：`subRange = ⌈R_world × (1/subW + 1/subH)⌉`
现配置 ⇒ `⌈0.17891 × 9.375⌉ = ⌈1.677⌉ = 2`。
证明：① 子格索引差 ≤ ⌈|Δg|⌉；② `|Δgx| = |Δx/subW + Δy/subH| ≤ |Δx|/subW + |Δy|/subH ≤ R_world(1/subW + 1/subH)`（三角不等式；`Δgy` 同式）；③ 故取 `n = ⌈R_world(1/subW+1/subH)⌉` 必覆盖 ✅。
**方案 B（紧确 · 同值）**：`n = ⌈R_world × √(1/subW² + 1/subH²)⌉`（柯西-施瓦茨；⇒ 2，与 A 同值）。
**参数来源**：`subW/subH` 由 `Config.cellSize / Config.subCellDivisor` 现算（⛔ 不写死 0.32/0.16）；`R_world = R_vis × 0.7155`（与本批 `DistVisual` 的换算系数同源，须同源取用）。

**代价／风险**：候选窗 `3×3 → 5×5`（每发到达多 16 次 `GetUnitsInSubCell` 查表）；命中过滤（`:232/:256`）不变 ⇒ **无假阳性**（只扩大候选，最终仍按真实距离判）。**建议同时集中**三处同款拷贝（`ProjectileManager.cs:343`／`DamageSystem.cs:490`／`GroundEffectManager.cs:202`）到单一口，否则本批后会出现 3 份不同口径。

**顺带读数（命中窗口变化，供 M6）**：`hitRadiusCells` 语义若由"格单位"改"视觉格"，则世界窗口 `半轴 (0.32, 0.16)` ⇒ `半径 0.17891 圆`（横向 ×0.559／纵向 ×1.118）。

---

## §7 · `GridMath` 常量双源（M1）—— 处置方案二选一 ＋ 调用面代价

**实盘**：`GridMath.cs:13-14` `public const float CellW = 1.28f / CellH = 0.64f`（`:12` 注释自承"与 GridConfig.cellSize 一致；静态工具不走资源加载，常量化"）；`GridConfig.asset:15` = `(1.28, 0.64)` ⇒ **当前一致（未漂移）**。

**调用面代价（关键读数）**：`GridMath.CellW`／`GridMath.CellH` **全库零外部引用**（`git grep -n "GridMath.CellW\|GridMath.CellH"` 空）⇒ 两常量只被 `GridMath.DistCells`（`:20-25`）／`DirCells`（`:28-32`）内部消费；而这两方法的调用面 = **19 处/8 文件**（`TaskScheduler` 4／`MonsterAI` 6／`DamageSystem` 4／`ProjectileManager` 2／`Building` 1／`GroundEffectManager` 1／Editor 探针 1；`GridMath` 自身定义不计）⇒ **改源不动任何调用点签名与语义（代价 ≈ 0）**。

| 方案 | 做法 | 代价 | 风险 |
|---|---|---|---|
| **A 改读配置** | 显式 `GridMath.Bind(Vector2 cellSize)`（由 `GridSystem.Initialize` 调 1 行）＋ 未绑定回退 1.28/0.64；或 lazy `Resources.Load<GridConfig>` | 1 行绑定 ＋ 1 处声明改动 | ⚠️ 静态 ctor/子线程 `Resources.Load` 不可靠 ⇒ 建议走**显式 Bind**；未绑定回退会静默用常量 |
| **B 一致性断言** | 保留 `const`，加启动/编辑器断言（`CellW != Config.cellSize.x ⇒ LogError`） | 1 处断言（需有调用时机） | 不解决"配置改了、断言没跑"的盲区 |

**建议**：**A ＋ B 并做**（A 保一致性、B 防手改）。⭐ 紧迫性上升理由：本批新增 `DistVisual` 若用 `CellDiagHalf = 0.5√(CellW²+CellH²)`，双源将**直接承担射程换算系数**——配置一改即"射程系数与寻路格单位各自漂移"，故建议本批一并处置（与任务书 M1 要求一致）。

---

## §8 · DZ-149 同款残留核实（M7）

**注释落点**：`BuildingComponents.cs:97` `/// <summary>射程圆内最近敌对单位（GridSystem 邻近格扫描，y 地面+飞行两层，欧氏距离）。</summary>` —— 与**同函数实现矛盾**：`:98-126` 为 `dx/dy` 全向格扫（`:109-124`）＋ `Vector2.Distance` 圆过滤（`:120-121`），**无 y 层循环** ⇒ 注释为漏网残留。

**全库清点（本轮实跑）**：

| 扫描 | 结果 |
|---|---|
| `for (int y = 0; y <= 1 …`／`for (int y = 0; y < 2 …` | `Assets/_Game` **0 命中**（`Assets/Editor` 亦 0） |
| `y <= 1` 命中项 | 均为 `-1..1` 邻域扫（`TerritorySystem.cs:149/203`／`WanderStimulusProvider.cs:125`／`MapRenderService.cs:406`／`TerritoryOverlay.cs:321/343`／`Building.cs:329/361/393`／`BuildingComponents.cs:109`／`MonsterAI` 等）⇒ 2D 邻域，非层语义 |
| `DZ-149/D693` 注释 | `UnitController.cs:1081/1117-1118/1190`／`NPCBrain.cs:1685/1716`／`ProjectileManager.cs:285`／`MonsterController.cs:81` —— 均为**已修点的历史说明**（"旧实现…已改"）⇒ 保留正确 |
| 其余"两层"命中 | 渲染 Tilemap 两图层（`MapRenderService.cs:19`）／配方条件分两层（`RecipeCatalog.cs:23`）⇒ 非同款 |

**结论**：**代码面零残留**（D693 的代码清点未见漏网）；**唯一漏网 = `BuildingComponents.cs:97` 注释**（1 行，须勘正为「以建筑为中心的方形邻格扫描 ＋ 欧氏圆过滤」）。

---

## §9 · 码面推演对照（供 M6／判据 10；⛔ 非实测）

以弓箭手 `attackRange = 6`（`Resources/UnitData/Human_Player_Archer.asset:23`；另：弩手 9／魔法塔 8／箭塔单位 7／箭塔建筑 `combat.range 5`／主城 2）为例，"可达格数"按**网格轴向步长 0.7155/格**计：

| 方向 | 改前有效可达（格） | 改后（格） | 变化 |
|---|---|---|---|
| 网格轴向 `gx`／`gy` | `√2R` = **8.485** | `R` = **6** | **−29.3%** |
| 网格对角 `(1,1)`（＝屏幕纵向） | `R` = **6** | `1.118R` = **6.708** | **+11.8%** |
| 网格反对角 `(1,−1)`（＝屏幕横向） | `R` = **6** | `0.559R` = **3.354** | **−44.1%** |

- 改前有效值 = **索敌门**（`UnitController.cs:1112` 世界圆半径 `R × 1.28 = 7.68`）∩ **判定门**（`DamageSystem.cs:301` `DistCells ≤ R`，世界椭圆 半轴 `(1.28R, 0.64R) = (7.68, 3.84)`）的**较小者**。
- 改后世界半径 = `R × 0.7155 = 4.293`（世界圆）。
- ⚠️ 判读提示：改后"正轴向恰 R 格"成立，但**屏幕横向仅 0.559R 格**、**屏幕纵向 1.118R 格** —— 同一世界圆在等轴 2:1 下的必然结果，判据 2 的"横向/纵向 R 格"必须先定计数口径（§待裁 6）。

---

## §待裁（6 条）

| # | 级别 | 事项 | 建议 |
|---|---|---|---|
| **1** | 🔴 口径 vs 实盘冲突 | **目标选择侧视线链不存在**：`HasLineOfSight` 零调用（自 `e1a6fdf2` 引入即无调用点）＋ `losEnabled` 零消费 ⇒ 任务书 #10／`07_伤害层` §四 的「遮挡双链」实为**单链**。件 3「双链合一」与件 5「高抛在目标选择阶段豁免」**无可改对象**。 | 请裁：(a) 本批只收口到达判定（＝现状）＋ 件 3/件 5 降级为"登记/接线待批"；或 (b) 本批**新接线**目标选择视线（＝新增功能，非"合一"，须另立判据）。⛔ 本端不自行择一 |
| **2** | 🔴 落地式与实盘不同源 | 件 4 阻挡集「山壁 ＋ `BuildingBlocked`」**不含"单位侧工事"**（`UnitData/Wall|Gate|四塔` 挂 `FortificationDef`，其挡箭走 `CheckWallBlock` 实体扫 §4.3）⇒ 落地后"工事挡弹道不挡视线"＋"非工事建筑挡视线不挡弹道"。 | 请裁：① 阻挡集是否并入"工事实体"？② 视线链与弹道链是否共用同一阻挡集（含"建筑挡不挡弹道"）？ |
| **3** | 🟡 | **废墟占格**：`EnterRuined` 保留占格（`Building.cs:1299-1328`，D154 有意）⇒ 废墟继续置 `BuildingBlocked` ⇒ 按件 4 判据"废墟挡视线"（但挡不了弹道）。 | 请裁：废墟是否计入"建筑阻挡位"？ |
| **4** | 🟡 判据字面不可达成 | 判据 8「`GridMath.DistCells` 调用面与读数**不变**」：`DamageSystem.cs:301`（射程判定）＋ `ProjectileManager.cs:232/256`（命中判定）若改走 `DistVisual`，调用面必然 **−3**（件 2 表亦未列这 3 处）。 | 请裁：判据 8 改述为「**非伤害链** DistCells 调用面不变」（推荐）＋ 明确这 3 处是否纳入本批改写 |
| **5** | 🟡 同批边界 | 伤害链内另有 3 处"格单位半径"未列入件 2：`DamageSystem.cs:436`（盾卫庇护）／`:471-472`（溅射 AOE）／`GroundEffectManager.cs:212`（地面效果半径）；另 `MonsterAI.cs:196/203-206`（怪物选目标"格距离"实为 `世界距离/标量 cellSize.x`）。不同改 ⇒ "命中按世界圆、溅射按格椭圆"分裂。 | 请裁本批边界（建议：与命中同源者一并收口，怪物选目标登记挂账） |
| **6** | 🔴 判据 2 计数口径 | 「横向 R 格内可打」的"格"有三种计数（轴向 0.7155／屏幕横 1.28／屏幕纵 0.64）⇒ 同一世界圆分别折 R／0.559R／1.118R 格（§9）。若按"屏幕横向格宽"字面取点，**改后判据必不通过**。 | 请裁：①"格"取哪一口径；②判据 2 是否改述为「**同世界距离**两向同结果 ＋ 各向可达格数表」 |

---

## 附 · 本轮红线遵守

- ⛔ `Assets/**` 一行未写（无探针、无场景/资产改动、无 `AI.Core`／训练仓触碰）；本轮动作全为只读（`git grep`／`git log -S`／读文件／读 `.asset`）。
- ⛔ 未 push；⛔ 未取 HH／D 号（报告以任务书 `HH.320` 命名，无新编号）；⛔ 未改 `GameScene.unity`／`Packages`／美术等既有脏点。
- 未施工任何生产码；§6 方案与 §7 处置**仅报选**。
