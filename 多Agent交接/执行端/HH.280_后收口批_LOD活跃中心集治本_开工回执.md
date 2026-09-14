# HH.280 D724 后收口批 开工回执（LOD 活跃中心集治本 ＋ 移速重标待裁 ＋ 探针勘正 ＋ 锚点核实）

> 类型：开工回执｜状态：🟡 已交付（件3 已施工 · 件1/件2 停手待裁 · 件4 已结论）
> 日期：2026-09-14 · 执行端 · 依据：用户 D724 后收口批派工（4 件）
> 取号：**HH.280**（占本号 · 账本水位线 279→280 · `D640 #10` 禁预留）
> 交付报告：按水位线另取（本回执占 HH.280 号，交付报告不占本号）
> HEAD 基线：`17fd176f`（D724 三批验收收口）· 未 push

---

## 〇、一句话结论

**件1（LOD 活跃中心集治本）＝根因链复证 + 施工方案就绪，停手待裁 2 项**（①覆盖半径口径 ②是否顺带修「上帝视角无君主锚回退」）。**件2（移速重标）＝基线复述 2.59 格/秒，停手待裁目标值**（红线「目标值未给前不施工」）。**件3（HH.239 探针勘正）＝已施工**（删 `ground_lake` 查项，编译 0 error）。**件4（EnemyHomePoint 引用核实）＝结论：保留**（Inspector 序列化引用，非名字锚点，删除会致敌方归巢回归）。

---

## §一、接单确认与红线复述

- 接单：D724 后收口批 4 件（件1 LOD 活跃中心集治本／件2 移速重标／件3 HH.239 探针勘正／件4 EnemyHomePoint 核实）。
- 授权（本批）：**允许改 `Systems/AI/LOD/` 与玩家视角接线**（HH.274「只排查不动手」红线本批解除）。
- 红线复述：具名 `git add`；禁 `-A/-u/.`；不 push；改前重读磁盘；`_任务队列.md` 一行不写；件2 目标值未定前停手；件4 只查不改；正门 `EnterTestRun`；收尾三态（`L-32`）；不为过线凑数（`L-30`）。
- 存档影响：本批拟改 `LodConfig.asset` 半径字段（件1 报裁① 若采 A）——仅运行时配置，**无存档影响**；`CameraRig.cs`/`LODSystem.cs` 为运行时行为改动，无序列化数据变更。**不改 `GameScene.unity`**。

---

## §二、件3 ｜ HH.239 探针勘正（已施工）

- **依据**：`git log -S "ground_lake"` 全库历史**零命中**（仅探针本身与既往报告提及；`SpriteRefTable` 从无该键，`R5` 收敛后水系走 `ground_ocean`/`ground_river`）＋ 本日「湖/冰河删除」后该键彻底作废（D724 裁定「须修探针·删该查项」，撞 `L-26` 家族：判据不可满足）。
- **改动**：`Valley_HH239_ArtProbe.cs` 删除 `fb2 = PlaceholderSprites.Get("ground_lake")` 查项及其日志拼接（保留 `bld_academy`/`orc_windwalker`/`bld_house_lv2` 三个有效负/正探针），留注释说明删因。
- **验证**：`unity_editor refresh` → **errors=0 / warnings=0**；`Assets/**` grep `ground_lake` 仅剩注释 1 处（无查项）。

---

## §三、件4 ｜ EnemyHomePoint 引用核实（只查 · 已结论）

**结论：保留。** 该对象**不是**「名字像 `GameObject.Find` 型代码锚点」——它按 **Inspector 序列化引用（fileID）** 绑定，C# 字符串 grep 零命中是**预期现象**。

| 证据 | 内容 |
|---|---|
| `GameScene.unity:1746` | `m_Name: EnemyHomePoint`（root 空对象，仅 `Transform`，active，位置 (10,−3,0)） |
| `GameScene.unity:291` | `SceneHomePointProvider` MonoBehaviour（script `638a175b…`）序列化字段 **`enemyHomePointAnchor: {fileID: 1222217275}`** ＝ EnemyHomePoint 的 Transform |
| `GameScene.unity:3908` | `- {fileID: 1222217275}`（场景 Transform 引用列表再计一处） |
| `SceneHomePointProvider.cs:26/44` | `enemyHomePointAnchor` 消费点：`Faction.Monster` 单位 `GetHomePoint` → 敌方侧锚点（3.0.1_6 §4.1）；null 时回退 `ResolveKingdomAnchor()`＝**玩家主城锚点** |
| `Assets/**` C# grep `EnemyHomePoint` | **0 命中**（字面串），但 `enemyHomePointAnchor` 字段名仅存在于 `SceneHomePointProvider` |

**若删**：`enemyHomePointAnchor` 变 null ⇒ 敌方（Undead）归巢/游荡目标回退到**玩家主城锚点** ⇒ 复现「NPC 全往主城聚集」类回归（2026-08-07 同型修复）。**故保留，不删**（本批只查不改，未触碰场景）。

---

## §四、件1 ｜ LOD 活跃中心集治本（调研完成 · 施工方案就绪 · 停手待裁 2 项）

### 4.1 根因链复证（R1~R4，与 HH.278 结论一致，本批逐一实读）

| # | 事实 | 证据（本批实读） |
|---|---|---|
| R1 | `LODSystem.SetFocalCenter` 在 `Assets/_Game/**` 零调用点 | grep 全库：调用者仅 `Editor/Smoke/Valley2_17_Smoke_13.cs`（4 处）＋ `Valley_HH264_PerfProbe.cs`（2 处）＋ `Valley_HH264_AnimProbe.cs`（1 处）——全为 Editor 探针 |
| R2 | `_focalMidChunk` 运行时恒 `null` | 同上（无 `_Game` 写入点） |
| R3 | null 回退锚＝`RulerController.MonarchUnit`，上帝视角下恒 `null` | `RulerController.cs:42` `public UnitController MonarchUnit => null;`（HH.17 裁决·君主实体退役）；`LODSystem.cs:233-245` 回退链 `_focalMidChunk → monarch → _armyCenters[0]`；`_armyCenters` 仅 `FormationController` 将军成型后注册（`FormationController.cs:146`） |
| R4 | ⇒ 绝大多数中区块 `GetLevelAt` 返 `Dormant` | `LODSystem.cs:378-384`：未登记且不在 `_activeBandSet` ⇒ `Dormant` ⇒ `SpriteAnimatorDriver.cs:148/151` `lodFpsScale[2]=0` ⇒ 帧推进冻结（`scale=0` 停推进） |

### 4.2 接线方案（授权面内，待裁后施工）

**玩家视角接线点＝`CameraRig`（2_10）**——`LODSystem` 注释「2_8 提供」系空挂（`2_8_AI应用层.md` 全文无 LOD/焦点接线），实际玩家视角即 `CameraRig`。坐标空间已实读对齐：

- `CameraRig.transform.position`（Iso 世界中心）→ `GridSystem.WorldToCoord`（`GridSystem.cs:140-151` 同 `MapRenderService.IsoToCell` 同一 iso 映射，origin-free）→ `CellToMidChunk`（`GridSystem.cs:389`）。**同源，可直接喂 `SetFocalCenter(transform.position)`，无需换算。**
- 落点：`CameraRig.Update()` 在 `if (!inputEnabled) return;` **之前**推一次 `LODSystem.Instance?.SetFocalCenter(transform.position)`（确保输入锁定/`FocusOn`/`Pan` 后焦点仍跟随；单次 `WorldToMidChunk` 数条浮点运算，无性能压力）。

### 4.3 ⚠️ 报裁 ① ｜ 活跃中心集覆盖半径口径

**现状读数**（`LodConfig.asset` 实读）：`activeRadiusMidChunks = 1`、`semiActiveRadiusMidChunks = 2`。中区块＝4 格边长 ⇒ 切比雪夫带：
- **Active（d≤1）**：3×3 中区块 ＝ **12×12 格**
- **SemiActive（d≤2）**：5×5 中区块 ＝ **20×20 格**

**相机视域**（`CameraConfig.cs`）：1× 档横向 **24 格**（D19 策划拍板）⇒ **1× 下屏缘约 ±2 格超出 semi 带 ⇒ 可见单位被冻结（Dormant）**；2×（12 格）/4×（6 格）档在带内。

**设计依据**：`2_4_LOD区块划分.md` D1 明示「1 中区块半径≈20×20 格…**实测不足时先调 R 再谈细分**」；且 D344「活跃带与相机缩放无关」（⇒ 否决定档位动态半径）。

| 选项 | 内容 | 影响 |
|---|---|---|
| **A（推荐）** | `activeRadius 1→2`、`semiActiveRadius 2→3`（`LodConfig.asset` 两字段）⇒ Active 20×20 格 / Semi 28×28 格，**完全覆盖 1× 视域**（24 宽） | 带内动画单位增多（驱动 per-slot 换帧 ≈2.2µs，量级可控）；**两字段纯配置改动，零代码面** |
| B | 保持 1/2 不变（最小改动） | 1× 下屏缘约 ±2 格单位仍 Dormant 冻结（肉眼可见） |
| C | 按相机档位动态半径 | 违背 D344「与相机缩放无关」，不推荐 |

> 执行端建议 **A**（D1 授权「先调 R」，且两字段配置改动成本最低）。

### 4.4 ⚠️ 报裁 ② ｜ 是否顺带修「上帝视角无君主锚回退」

现状：`_focalMidChunk == null` 时回退链 `monarch（恒 null）→ _armyCenters[0]（开局空）` ⇒ 活跃中心集近空；`RenderActiveBands` 的「无中心兜底」只把**已登记**区块置 Active，`GetLevelAt` 对**未登记**（绝大多数）区块仍返 `Dormant`。设计文档「缺省用主城锚点」**未落地**。

| 选项 | 内容 | 影响 |
|---|---|---|
| **A（推荐）** | `ComputeActiveCenters` 在 `_focalMidChunk==null` 且君主/军队锚均无时，回退 `WorldManager.GetKingdomAnchorWorld()` 的中区块（主城锚点兜底） | 落设计文档原意；CameraRig 就绪前首帧/无相机测试场景下主城单位不冻结；单点小改 |
| B | 不修（依赖 CameraRig 首帧即推焦点） | 正常进局无碍（CameraRig `OnMapGenerated` 后首帧即有焦点）；但无相机场景/极端时序下保持现状冻结 |

> 执行端建议 **A**（一行回退，落「缺省用主城锚点」设计原意；HH.274 诚实声明里「缺口＝活跃中心集口径」的一部分）。

---

## §五、件2 ｜ 移速重标（基线复述 · 停手待裁目标值）

### 5.1 基线（HH.278 已实测，本批复述）

- **实测净位移速中位 2.59 格/秒**（区间 2.508~3.358，34 单位中 28 个在移动，3.21s 窗口，`timeScale=1`；HH.278 线1 已落盘）。
- 运行时 `WalkSpeed=3.000`；实盘 `Data.walkSpeed/runSpeed = 3.000/6.000`（`UnitData.cs:79/82` 的 `5f/10f` 为代码默认，实盘资产覆盖）。
- 设计基准 `MovementConfig.npcSpeed = 0.716` 世界单位/秒 ＝ **1 格/秒** ⇒ 现值超基准 **≈2.6×**。

### 5.2 修法（已裁采 (b)）

`UnitController.cs:376`（`Initialize`）与 `:510`（复用初始化）改 `WalkSpeed/RunSpeed = MovementConfig.npcSpeed × 倍率`（倍率源 `RaceDef.moveSpeedMul`，`EffectiveSpeed` 已单点消费；激活死字段 `npcSpeed`）。**配套红线**：同核到达半径 3 处（`PathFollower.cs:99/178` ＋ `UnitController.cs:1286`）**禁「改速度捎带改半径」**——只动速度初始化，半径不动。

### 5.3 ⚠️ 报裁 ｜ 目标格/秒（红线：未给前不施工）

| 候选 | 内容 | 影响 |
|---|---|---|
| **1.0**（策划端 D724 建议） | 严格按基准 `npcSpeed=0.716` ＝ 1 格/秒（run 按其倍率） | 全局降速 **≈2.6×**，属玩法级变更（节奏/寻路/战斗拉扯全受影响） |
| **2.0** | 折中（`npcSpeed ≈ 1.43`） | 降速 ≈1.3×，手感变化温和 |
| 其它 | 策划端给定值 | —— |

> 执行端建议：若接受玩法级变更选 **1.0**（回归设计基准）；若求稳选 **2.0**。**未定前不施工。**

---

## §六、L-34 在线判据表（五列）

| 观测项 | 可判定最早时点 | 命中即停条件 | 本次读数 | 结论 |
|---|---|---|---|---|
| 件3 探针键陈旧 | 编辑态一次 grep | `ground_lake` 查项存在于探针 | 已删（仅注释残留） | **已施工** |
| 件4 锚点引用 | 编辑态一次查 | 找到 fileID 序列化引用 | `GameScene.unity:291` 坐实 | **保留·不删** |
| 件1 接线点存在性 | 编辑态 grep | `_Game/**` 内 `SetFocalCenter` 调用点 >0 | **0 处**（R1 复证） | 待裁后接线 |
| 件1 半径带覆盖 | 待施工后进局快照 | tier2 占比显著下降（对照基线 63%） | 待施工 | 报裁① |
| 件2 目标值 | 策划端给值 | 值给出即施工 | **未给** | **停手待裁** |

---

## §七、工作区声明与提交

- 本回执阶段**零业务代码改动待提交面**＝仅 `Valley_HH239_ArtProbe.cs`（件3·1 文件）；件1/件2 待裁后另 commit。
- 工作区其余未跟踪/改动（`Packages/manifest.json`、`pixel-forge/`、Ground 图集资产、`New Palette.prefab`、`Medieval Kingdom/`、`Output/` 等）**均非本批引入，不混入**。
- 台账/索引按水位线 279→280 追加；`_任务队列.md` 一行不写。

---

> 执行端｜2026-09-14｜件3 已施工、件4 已结论、件1/件2 停手待裁（报裁① ② 目标值共 3 项）
