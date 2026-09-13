# HH.262「美术资源接入批（HH.103 落地 · Gate=`G3-1`）」开工回执

> 类型：开工回执（含 🔴 停手待裁 · 实盘勘正 11 项）
> 状态：🔴 **停手待裁**（回执落盘后等策划端确认；**本阶段零施工动作**）
> 日期：2026-09-13 · 发起端：执行端 · Gate：`G3-1`
> 关联：**HH.239 任务书（D689 签发）＋§八 增补节（H1~H6）**／HH.103（D604·已被本批取代）／`改造计划/美术资源接入计划.md`（D689）／`改造计划/美术资源接入映射表.md`（§十/§十一）／`0.6 §二百一十八`（D689）／3.6.1（D686）
> 取号：HH.262（账本水位线 261→262·独立单行 commit `ab41249`）

---

## 一、作业依据（必读链逐字实读）

| # | 文档 | 实读结论 |
|---|---|---|
| 1 | `HH.239` 任务书全文＋§八 | 范围=一次性全量（≈328）／六定案／T1~T17／§四 验收 6 条／§五 红线／§六 请裁 3 项／**H1~H6 硬条款** |
| 2 | `美术资源接入计划.md` §一~§十一 | 5 条接线线／§五 口径缺口 5.1~5.4／§六 残留清理／§七 导入设置＋§七.1 切帧＋§七.2 播放器路线A |
| 3 | `美术资源接入映射表.md` §十/§十一 | artId 键表（①~⑫）／⬜待建项／两个命名空间警示（文件名 vs artId） |
| 4 | `0.6 §二百一十八`（D689） | 计划定稿裁定 |
| 5 | 美术规范（HH.206/D659）／3.6.1（D686） | 命名八类／弹种全集 |
| 6 | `_交接索引.md` HH.239 行 | 🟢 已签发·待接单；**无交付报告**（首接属实） |

---

## 二、§六「请裁项」逐条自答（**不自裁 · 给判据**）

**① gitignore 口径（素材夹 ≈315 未跟踪二进制）**

- **自答（实盘）**：**已过时**——`git ls-files 美术资源文件夹` = **325 条（已跟踪）**；`.gitignore` 中**无** `Art`/`美术` 相关规则（`Select-String .gitignore` 零命中）。
- 现状：`_Game/Art/` **328 png 中仅 66 条被跟踪**（`git ls-files` 计数），另有 **60 条 `D`（删除待提交）＋31 条 `??`（未跟踪）**。
- ⇒ **本项须改判为新口径**：不再是"素材夹是否入库"，而是「**`_Game/Art/` 的 328 图＋meta 是否全部纳入版本控制**」（见 §六 R1）。

**② 弹药弹种未定项（`musket`/`mage_barrage`/`monster`）**

- **自答**：按 **H3** 执行——只挂现役 `ProjectileType` 六值（**实读坐实**：`Systems/AI.Core/Config/ProjectileTypes.cs:10-17` ＝ `Arrow/Bolt/HeavyBolt/Stone/Fireball/Magic`，**无** musket/monster/mage_barrage）；三新弹种**入库不接线**、列报挂 3.6.1 转正批。**`ProjectileType` 扩展＝AI.Core 域＝禁触**（H3 红线）。
- 素材实况：`_Game/Art/Ammo/` 已含 `ammo_musket/ammo_monster/ammo_mage_barrage`（E4 坐实）。

**③ 弓箭手待机 96×102（差异 D10）**

- **自答**：本批**跳过**该张（归批5 重出）；切帧脚本须**显式排除白名单**该路径，防误切成 32×34 错格（§七.1 特例①）。

---

## 三、实盘对账（磁盘为准 · §8.1 · **11 项勘正**）

### 3.1 两套占位系统在场性（**D9 坐实**）

| 系统 | 路径 | 命名空间 | 键空间 | 接线状态 |
|---|---|---|---|---|
| **1D legacy（在用）** | `_Game/Core/PlaceholderSprites.cs` | **全局**（无 namespace） | legacy key ~31（`monarch/villager/castle/wall/farm/mine/tower_arrow/tree/farmland/treasure_box/ruins/ore_vein/stone_pile/wood_pile/scaffold/war_academy/war_camp/ley_forge/archery_range/rift/…`，`PreloadAll()` L24-33） | **在用**：`BuildingVisual.cs:57`／`Building.cs:516,523`／`ChestEntity.cs:50`／`LoadManager.cs:61` |
| **artId 等轴（保留）** | `_Game/Systems/Rendering/PlaceholderSprites.cs` | `ValleyRampart.Rendering` | **artId 31 键**（`_defs` L28-68：`ground_×4`／`feat_×14`／`bld_×12`／`mark_×1`） | **无外部调用者**（D9 坐实） |
| 生成器 | `Core/SpriteFactory.cs`（全局）／`Systems/Rendering/SpriteFactory.cs`（`ValleyRampart.Rendering`） | — | — | `BuildProgressBar.cs:62,72` 用**全局** `SpriteFactory.CreateSquare`（建造进度条） |

> **命名解析实读**：`BuildingVisual`/`Building`/`ChestEntity`/`BuildProgressBar` 均在**全局命名空间** ⇒ 名字查找优先全局类型 ⇒ 现走 **1D legacy**（与 §8.1 记载一致）。
> **K6（H1 坐实）**：`.meta` 的 `guid:` 为**长 base64 密文**（实例：`unit_human_warrior_idle.png.meta:2` = `DH4bvC6kUXqlmeqfV1rEEJu94SY79KJx+o7Uz0e3PqoktXdfbF3NnCo=`，非 32-hex）⇒ **文本 grep 互映不可用**（H1 实证成立）。

### 3.2 映射表键空间现状（D1/D2/D3 对账）

| 项 | 实盘 | 缺口 |
|---|---|---|
| `Rendering/PlaceholderSprites._defs` | 31 键 | **缺**：⑥建筑 12（barracks/blacksmith/hospital/church/siegeworkshop/ranch/well/crossbowtower/magictower/vagrant_camp/portal/scaffold×3）＋地皮 2 |
| **D1 命名不一致（新识别）** | 表内为 `feat_water_ocean`／`feat_water_river`（L49-50） | 而 D1/§十一 要求键名＝**`ground_ocean`／`ground_river`** ⇒ **同物二名**（须裁：改名 or 新增，`L-15` 禁双源） |
| **D2 枯木改名** | 表内 `feat_wood_pile`（L46） | 待改 `feat_deadwood`（D659 决策2） |
| `SpriteRefTable` 资产 | **0 条**（全库 `-Filter SpriteRefTable*` 命中 0） | **T6 未建**（仅注释级引用：`Rendering/PlaceholderSprites.cs:16`） |

### 3.3 素材与存量状态（**K1/K2/K3/K4/K5/K7**）

| # | 实盘（命令输出为准） | 影响 |
|---|---|---|
| **K1** | **`_Game/Art/` 已含全量 328 png**，与素材夹**逐一吻合**：`Ammo 8／Buildings 105／Effects 3／Ground 6／Machines 8／Portraits 46／Units 152 = 328`；素材夹同为 `8/105/3/6/8/46/152 = 328` | **T3「全量入库」主体已在盘** ⇒ T3 实为「**核对＋提交**」（非"导入"）；E1 计数（46/152/105/8/8/3/6）**逐项吻合** |
| **K2** | `git status _Game/Art` = **91 条**（`D`×60＋`??`×31）；`D` 明细＝`Palette1111.prefab`＋13 张匿名残图（`01~13_image-*.png`）＋地块 `.png/.asset`（`2231/2232/227`）＋全 metas ⇒ **残留已在工作区删除、未提交**；`Art/Ground/` 现存＝**新 `ground_*` 6 张** | **T2「删残留」已（工作区）完成** ⇒ T2 实为「**提交删除＋处置悬空 meta**」 |
| **K3** | **GameScene 编辑器侧只读枚举**（`execute_code`·active scene＝GameScene）：`goTotal=51 SR_total=6 SR_nullSprite=4 nullComponents(missingScript)=1 tilemaps=3`；明细＝`[missingScript] ruler`／`[nullSprite] 参考图`／`ruler`／`平原`／`VFriendly`；3 Tilemap（`Tilemap_Ground/Feature/Territory`，父 `MapRender`）**tiles 全 0** | **T1 实为「清 5 处存量 Missing」**（非"先摘引用再删"）；**3 Tilemap 无 Tile 引用** ⇒ §六 担心的"残留 Tile 被场景引用"**未成立** |
| **K4** | 抽检 `unit_human_warrior_idle.png.meta`：`spritePixelsToUnits: 100`✅／`spriteMode: 1`（**Single·未切**）／`alignment: 0`（**Center**）／`spritePivot: {0.5,0.5}`（**非底面中心**）／`spritesheet.sprites: []` | **T4 未配、T5 未切**（PPU 已符；pivot/alignment/mode 待改） |
| **K5** | `SpriteRefTable` 资产 0 条 | **T6 未建** |
| **K7** | 素材夹 **已 tracked 325**；`.gitignore` 无相关规则 | **§六① 口径过时**（见 §二①） |

### 3.4 接线点侦察（**K8/K9/K10**）

| # | 实读 | 结论 |
|---|---|---|
| **K8（§8.1 E3 monster 接线面·侦察列报）** | `MonsterController : UnitController`（`Systems/Disaster/MonsterController.cs:14`）；`MonsterDef`（`Systems/Disaster/Data/MonsterDef.cs:5`）；渲染源＝`UnitController._renderer`（`UnitController.cs:22 [RequireComponent(SpriteRenderer)]`／`:290/295`） | **monster 与单位同链**（无独立渲染入口）⇒ 接线归**单位线**（T12/T13/T15），**零新机制** |
| **K9** | `AmmoDef.cs`（`_Game/Data/AmmoDef.cs:9-33`）字段＝`ammoType/pierceLevel/aoeRadiusCells/aoeFalloff/ballisticType/arcHeightCells/effect`——**无 sprite 字段**；`GroundEffectDef.cs:8-26` 字段＝`type/radiusCells/duration/tickInterval/power/maxTargets`——**无 sprite 字段** | **T14 须新增"表现字段"**（sprite/帧数组）⇒ **请裁边界**：是否属"不改 SO 语义字段"红线之内（本端判断＝**表现层新增、非语义变更**，但须策划端确认） |
| **K10** | `ProjectileType` 六值（见 §二②） | **H3 坐实** |
| 其他接线点 | `BuildingVisual.ApplyPlaceholder`（`BuildingVisual.cs:52-57`）／`MapRenderService.GroundTile`（计划 §五.2）／`ProjectileManager.cs:110,377`（弹体 SpriteRenderer） | 与计划 §四 5 线一致 |

---

## 四、§3.3 完整性校验表初检结果（§一~§十一 逐节核对）

| 计划章节 | 任务书映射 | 初检 | 说明 |
|---|---|---|---|
| §一 范围/深度/残留/动画/路径 | T1~T17 | ⚠️ **需勘正** | 路径✅；**残留/入库两项实盘已部分完成**（K1/K2） |
| §二 素材盘点＋D1~D10 | T3/D1→T8/T10；D2→T8；D3→T8；D4→T11；D5→T7；D6→T15；D7→T14；D8→T14；D9→T16；D10→跳过 | ⚠️ | D1 键名与实盘**不一致**（`feat_water_*` vs `ground_*`）⇒ **须裁**；D2/D3 未做✅；D9 实读坐实✅ |
| §三 加载架构（SpriteRefTable） | T6 | ✅ | 未建（待施工） |
| §四 5 线接线点 | ①T9 ②T10 ③T12/T13/T15 ④T14 ⑤T11 | ✅ | 接线点实读在场；K9 为 ④ 前置 |
| §五 口径缺口（5.1~5.4） | 5.1→T3；5.2→T10；5.3→T7/T8/T16；5.4→T5/T13 | ✅ | 5.3 调用方 5 处实读在场（含 `BuildProgressBar` 单列） |
| §六 残留清理 | T1/T2 | ⚠️ **需勘正** | 见 K2/K3（T1 语义变更） |
| §七 导入设置＋切帧＋播放 | T4/T5/T13 | ✅ | 未做（待施工）；§七.1 特例须加"弓箭手排除" |
| §八 施工序 S1~S8 | §二 施工序 | ⚠️ **需勘正** | S1/S2 主体已（工作区）完成 ⇒ 序应改为「**S1 收口存量（提交删除＋清 Missing）→ S2 核对＋配置**」 |
| §九 验收 6 条 | §四 验收线 | ✅ | 见 §五 gap 表 |
| §十 风险 | §五 风险表 | ✅ | "素材夹未跟踪"风险**已消失**（K7） |
| §十一 任务书要点 | 本任务书 | ✅ | — |

---

## 五、L-30 式 gap 表（进局验收：四族四轮冒烟）

| 验收句 | 需要的证据 | 硬条件在场性 | 值 gap | 时间 gap | 判定 |
|---|---|---|---|---|---|
| 真图生效（每类抽 1） | 游戏内肉眼真图 | 素材在盘✅＋`SpriteRefTable` 建❌＋接线❌ | 素材满足；表/接线待建 | 施工后 | 🔜施工后（材料已备） |
| 缺图回退（负探针） | miss ⇒ 占位且不崩 | 占位表在场✅（双套） | — | — | 🔜施工后 |
| 锚点/比例（PPU100＋pivot底面中心＋2:1） | importer 读回 | PPU=100✅；pivot/alignment **未配**❌ | 1 项待配 | 施工后 | 🔜施工后 |
| 两向动画（含 flipX／idle 播 8） | 帧数组＋播放器 | 切帧❌＋播放器**项目无现成系统**❌ | 两项待建 | 施工后 | 🔜施工后 |
| 残留清零 | Missing 0／目录不存在／grep 零命中 | **现 5 处 Missing**（K3）＋`Core/PlaceholderSprites` 在用 | **存量已有欠账** | S1 | ⚠️**起点即不达标**（非本批引入） |
| 回归零退化（3 冒烟容器） | 容器 ALL PASS | 容器在场✅ | — | 施工后 | 🔜施工后 |

> **冒烟档位**：正门 `TestHarnessApi.EnterTestRun`＋收工 `ExitTestRun`＋退 Play（`L-32`）；四族四轮（human/orc/dwarf/elf）。

---

## 六、待裁事项（**R1~R6 · 决定施工形态**）

| # | 事项 | 选项（推荐在前） | 影响 |
|---|---|---|---|
| **R1** | **K7 口径改判**：`_Game/Art/` 328 图＋meta 的版本控制口径 | **(a·推荐)** 全量纳入版本控制（提交 328 图＋meta；素材夹 `美术资源文件夹/` 保留或另议）；(b) 仅提交接线所需（其余留 untracked）；(c) 全走 LFS（若仓库策略允许） | 决定 T3 的"入库"定义与交付体积 |
| **R2** | **K2 T2 语义**：残留删除已在工作区（60 条 `D`） | **(a·推荐)** 本批**打包提交该删除**＋清悬空 meta＋处置 13 匿名残图（即 T1/T2 收口）；(b) 由他端提交、本批仅核 | 决定 S1 是否含"提交既有删除" |
| **R3** | **K3 T1 语义**：GameScene **现存 5 处 Missing**（`参考图`/`ruler`×2/`平原`/`VFriendly`） | **(a·推荐)** 本批**照清**（去悬空引用/删空 GO）；(b) 仅清与残留相关的 2 处（`平原`/`VFriendly`），`参考图`/`ruler` 存量另立小批 | 决定 T1 范围与验收判据（存量 vs 本批引入） |
| **R4** | **K9 T14 边界**：`AmmoDef`/`GroundEffectDef` **无 sprite 字段** | **(a·推荐)** 允许**新增表现字段**（sprite/帧数组），视作非语义变更；(b) 改走旁挂映射（如 `SpriteRefTable` 的 `ammo_*`/`fx_*` 键），**不动这两个 SO** | 决定是否触"不改 SO"红线 |
| **R5** | **D1 键名二义（新识别）**：实盘表内 `feat_water_ocean/river` vs 要求 `ground_ocean/river` | **(a·推荐)** 收敛为 `ground_ocean/river`（改 `Rendering/PlaceholderSprites._defs` 键名＋地皮线按 `ground_*` 查表）；(b) 保留 `feat_water_*` 并同步改 D1 要求 | `L-15` 禁双源；影响 T8/T10 |
| **R6** | **施工序修正**：S1/S2 主体已（工作区）完成 | **(a·推荐)** 改为「S1 收口存量（提交删除＋清 5 处 Missing）→ S2 核对 328＋配导入设置」；(b) 维持原 S1~S8 文字（仅在报告中标注"部分已先行"） | 影响清单逐项勾选口径 |

---

## 七、红线自检 / 零改动面声明

- **本阶段零施工动作**：未改任何 `Assets/**`／未建资产／未配导入／未动场景（**仅只读枚举**：`execute_code` 读 active scene 组件与 Tilemap 计数，**未保存场景**）。
- 未进 Play／未跑局（`L-32` 无关）。
- **写-改-commit 同串·只提本批文件·未 push**；`_任务队列.md`（CRLF-blob）**未触碰**。
- **证据纪律**：本回执断言口径＝**命令实盘输出**（`git`/`Get-ChildItem`/`execute_code` 返回值）＋`file:line`；排除项＝未跑局、未配导入、未动场景。
- 教训引用：`L-29`（禁以文本 grep 判无引用→**H1 已实读坐实**）／`L-15`（禁双源）／`L-30`（gap 表）／`L-34`／`L-35`（口径声明）。

---

## 八、停手待裁

本回执落盘后**停手**，等策划端对 **§六 R1~R6** 逐条裁决（尤其 **R4 是否触 SO 红线**、**R1/R2/R3 存量收口范围**）后开工。

> 执行端（TraeCode）｜2026-09-13｜本回执＝HH.239 开工前置（含 §六请裁自答＋§3.3 初检＋实盘对账 11 项勘正）。
