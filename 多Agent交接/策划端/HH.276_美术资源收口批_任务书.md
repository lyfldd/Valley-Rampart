# HH.276 美术资源收口批 任务书（SpriteAtlas 合批 ＋ 补图）

> 类型：施工任务书｜状态：🟢 **已签发·待接单**
> 日期：2026-09-14 · 签发端：主策划端 · Gate：`G3-1`
> 关联：**`HH.275` 交付报告**（图集治本路径实证来源）／`HH.274`（原件4 Atlas **已挪入本批**）／`D715`（缺图定性）／`D721`／`D722`／`美术资源规范_等轴立方体瓦片.md`
> 取号：HH.276（账本水位线 275→276）
> **由来**＝用户 2026-09-14 18:58 构思「**能不能以此就把我们美术的这几个都做完了**」——把散落的美术欠账合成一批收口。

---

## 〇、一句话目标

把美术资源面的欠账**一次收口**：**Ground/Feature 入图集**（治 `HH.275` 遗留的 draw call 652）＋ **补两张山地图**＋**补单位缺态帧**。

---

## 一、作业依据（必读·逐字）

| # | 文档／物件 | 用处 |
|---|---|---|
| 1 | `多Agent交接/执行端/HH.275_地图区块边界遮挡治本_交付报告.md` | **图集治本路径的实证来源**：`DrawCall 80→652` 的根因＝**5 张地皮贴图交替打断合批**（地皮临时统一成 1 张贴图时 `Individual 58 ≈ Chunk 58`） |
| 2 | **`Valley Rampart/Assets/Resources/Config/Art/UnitsFrames.spriteatlas`** | **现成范例（照做）**：`maxTextureSize 2048`／`filterMode 1(Bilinear)`／`padding 4`／`m_IsVariant 0`／`packables` 收录跨多张 png 的数百 sprite |
| 3 | `Valley Rampart/Assets/Resources/Config/Art/SpriteRefTable.asset` | artId→sprite 映射（327 条：`unit_` 151／`bld_` 89／`portrait_` 46／`feat_` 16／`ammo_` 8／`machine_` 8／**`ground_` 6**／`fx_` 3） |
| 4 | `改造计划/美术资源规范_等轴立方体瓦片.md` | **素材规格权威源**（等轴菱形 2:1·PPU=100·1 格＝128×64px） |
| 5 | `改造计划/美术资源接入映射表.md` | 现有接入状态（§⑪ 地形/地皮） |

---

## 二、范围（三件）

### 件1｜Ground/Feature SpriteAtlas（**自 HH.274 件4 挪入**）

**目标**：把 `DrawCall 652` 收回 **80 量级**（`HH.275` 实测基线）。

**要求**：
1. **新建 `SpriteAtlas`**（建议命名 `GroundFeature.spriteatlas`，置于 `Assets/Resources/Config/Art/`，与 `UnitsFrames` 同目录），**照 `UnitsFrames` 的配置口径**（`maxTextureSize 2048`／`filterMode Bilinear`／`padding 4`／非 Variant）
2. **入包内容**：
   - **Ground 6 张**：`ground_temperate`／`ground_tropical`／`ground_cold`／`ground_subtropical`／`ground_river`／`ground_ocean`（**必入**——它们是 draw call 的主因）
   - **Feature**：`feat_*` 全部（`feat_mine`／`feat_orevein`／`feat_stone_pile`／`feat_deadwood`／12 张树）＋ **件2 新增的山地/雪山**（**按层分包**，Ground 一包、Feature 一包；若合批效果无差可同包）
3. **不改贴图本体**：导入设置与 `ArtImportPipeline` 口径一致（PPU/pivot/压缩不变）
4. **不得破坏**：`CanBindTo` **4/4**（`UnitsFrames` 单位图集）／`SpriteRefTable` 间接层／缺图回退链

**验证**：给 **DrawCall 改前/改后对照**（基线 **652**）；**须跑实机**（编辑器数字不作数）。

### 件2｜补 `feat_mountain` / `feat_snowmountain` 两张素材

**现状**：`MapRenderService.FeatureArtId`（`:530-540`）对二者 `default: return null` ⇒ **无真图**，回落占位色块（`FeatureToFeatureColor`＝Mountain `(0.45,0.40,0.35)`／SnowMountain `(0.85,0.88,0.92)`·`MapRenderService.cs:599/600`）⇒ **用户所见"散乱色块"**（`D715` 定性·屏占最高 **20.3%**）。

**要求**：
1. 按 `美术资源规范_等轴立方体瓦片.md` 出 **两张 1 格素材**（等轴菱形 2:1·PPU=100·128×64px）
2. 命名入表：`feat_mountain`／`feat_snowmountain`，写进 `SpriteRefTable.asset`
3. **并入件1 的 Feature 图集**
4. ⚠️ **素材是"单格"**；**"山脉成带"由生成算法负责**（`HH.272 §二④ 山脉化生成`）⇒ **本件只出图，不管形状**

### 件3｜补单位缺态帧（**范围待定·须先出欠账清单**）

**现状（实读 `SpriteRefTable`）**：`unit_*` 151 条，后缀分布＝`idle 48`／`walk 49`／`attack 45`／`icon 3`／**`run 3`**／**`loot 3`**／**`death 0`**。对照 `SpriteAnimator` **六态**（Idle/Walk/Attack/Run/Loot/Death，`SpriteAnimatorConfig.stateFallback`）：

- **`death` 全缺**（0 条）
- `run`／`loot` 仅 3 条（≈只 1 个职业有）
- `idle`／`walk`／`attack` **齐**

**要求**：
1. **先出欠账清单**：逐族 × 逐职业 × 逐态，标出缺哪些（表格式）
2. **按清单补图**（工作量由清单决定，**回执里报工作量估算**）
3. **优先级建议**：`death` > `run` > `loot`

> ⚠️ **本件不承诺修好 `HH.274` 件2「Game 窗口完全无动画」**——`idle/walk/attack` 是齐的，缺 `death` 只影响死亡动作 ⇒ **两个问题必须分开验**（`HH.274` 件2 的根因仍在驱动层/回退链）。

---

## 三、验收线

| # | 线 | 判据 |
|---|---|---|
| **1** | **图集在场** | `GroundFeature.spriteatlas` 资产存在；Ground 6 张 **全部入包**（给 `packables` 计数实证） |
| **2** | **合批效果（核心）** | **实机 DrawCall 前后对照**：基线 **652** → **目标 80 量级**；给 profiler/Frame Debugger 证据 |
| **3** | **不破坏** | `CanBindTo` **4/4** 保持；`SpriteRefTable` 间接层与缺图回退链不破 |
| **4** | **山地图** | `feat_mountain`／`feat_snowmountain` 入表且入图集；**实机截图中不再出现该两种占位色块**（`(115,102,89)`／`(217,224,235)` 取色命中率→0） |
| **5** | **单位缺态** | 欠账清单在场；补图后 `unit_*` 后缀分布中 `death` 计数 > 0（或如实说明未补及原因） |
| **6** | **回归** | 编译 **0 error**；`EnterTestRun`＋`ExitTestRun` 三态；四族四轮冒烟**不退化** |
| **7** | **写后验** | 本批相关路径 `git status` 全空；各笔 hash 已贴；**未 push** |

---

## 四、红线

1. 正门 `EnterTestRun`；收尾真暂停 ＋ `ExitTestRun` ＋ 退 Play（`L-32`）；**不为过线凑数**（`L-30`）。
2. ❌ **本批只做资产面**：新建图集／补图／登记 artId；**不动 `MapGenRules.cs` / `MapRenderService.cs` 等业务代码**（那属 `HH.272`）。
3. ❌ **不碰 `GameScene.unity`**（`HH.271`/`HH.275` 的场）。
4. ❌ **不改贴图本体**（PPU／pivot／压缩／尺寸一律不动）；仅做"入图集"与"新增素材"。
5. ❌ 不删 `UnitsFrames.spriteatlas`；若图集冲突 ⇒ **列报不擅动**。
6. 写-改-commit 同串 · **具名 `git add`** · 禁 `-A`/`-u`/`.` · **不 push**；改前重读磁盘。
7. `多Agent交接/_任务队列.md` **一行不写**。
8. 交付报告按账本**实时水位线取号**（**禁预留**·`D640 #10`）。

---

## 五、排雷

| # | 雷 | 处置 |
|---|---|---|
| **M1** | **图集 V1/V2 模式差异** | 团结/Unity 2022 的 `SpriteAtlas` 有 `V1/V2` 差异（`Include in Build` 行为不同）⇒ **必须实测"跑起来的 DrawCall"**，不看编辑器；若 V2 下 Tilemap 不合批 ⇒ 试 V1 或显式 `LateBinding` |
| **M2** | **单张图集尺寸上限** | Ground 6 张（172×202 级）＋Feature 17 张＋单位图集若同包 ⇒ 可能超 `maxTextureSize 2048` ⇒ **按层分包**（Ground／Feature 分开），必要时放宽 `maxTextureSize` 并**报实测** |
| **M3** | **补图后形状与生成算法不匹配** | 山地图是**单格素材**；若 `HH.272` 山脉化尚未落地 ⇒ 实机仍显"散点山" ⇒ **如实声明"形状问题归 HH.272"**，不擅自改生成 |
| **M4** | **单位缺态工作量未知** | 151 条现状中 idle/walk/attack 齐（约 48×职业数）⇒ 若按同口径补 death/run/loot ⇒ **工作量可能很大** ⇒ **先出清单+估算，报策划端定范围**，禁闷头全补 |
| **M5** | **补图属美术产出** | 本批执行端负责**接图/入表/入集/验证**；**图本身由美术（或用户 pixel-forge）产出** ⇒ 若图未到 ⇒ **如实列报阻塞**，不自行生成替代素材 |
| **M6** | **承 `L-29`/`L-30`** | 判定线可达性：本批判据均可实测（DrawCall／取色命中率／计数），无结构性不可达风险 |

---

## 六、产出

1. **开工回执（占 HH.276 号 · 不另取号）**：含 §五 排雷 **M1~M6 逐条自答** ＋ **图集分包方案与 DrawCall 基线复述** ＋ **单位缺态欠账清单（首版）＋工作量估算** ＋ 图源确认（图从哪来）。
2. **交付报告（按水位线另取号）**：§三 七条逐项证据（**DrawCall 前后／取色命中率／packables 计数／欠账清单／回归**）＋ `L-34` 五列 ＋ **实机截图对照**。
3. 截图落 `screenshots/HH276_美术收口/`（`.gitignore:60` 已忽略 ⇒ **不入库**）。

---

> 签发：主策划端｜2026-09-14｜**D722**｜依据＝用户 2026-09-14 18:58 构思（美术面合并收口）＋ `HH.275` 图集治本路径实证 ＋ `D715` 缺图定性 ＋ `SpriteRefTable` 实读清单。
