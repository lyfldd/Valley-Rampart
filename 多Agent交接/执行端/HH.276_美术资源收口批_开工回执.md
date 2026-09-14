# HH.276 美术资源收口批 开工回执（SpriteAtlas 合批 ＋ 补山地图 ＋ 单位缺态清单）

> 类型：开工回执（**占 HH.276 号 · 不另取号**）｜状态：🟢 已开工
> 日期：2026-09-14 · 执行端 · Gate：`G3-1`
> 账本水位线：HH.276（**下一号 HH.277**）· HEAD = `349959c9` · **未 push**
> 依据：`多Agent交接/策划端/HH.276_美术资源收口批_任务书.md`（D722）＋ 用户 D723 派工总纲（先做 Atlas）

---

## 〇、一句话

**件1（图集）已落地并实机实测成立**（DrawCall 639→194）；**件2（补山地图）阻塞**——仓库内**不存在**山地图素材，按红线 M5 **未自行生成替代素材**；**件3 已出欠账清单＋工作量估算**，并勘正任务书三处口径（`run`／`loot` 非欠账、`death` 为唯一真欠账）。

---

## 一、施工面清单（file）

| # | 面 | 落点 | 状态 |
|---|---|---|---|
| 件1 | 新建 SpriteAtlas | `Valley Rampart/Assets/Resources/Config/Art/GroundFeature.spriteatlas`（+`.meta`） | ✅ 已落地 |
| 件2 | 补 `feat_mountain`/`feat_snowmountain` | — | 🔴 **阻塞（无素材）** |
| 件3 | 单位缺态欠账清单 | 本回执 §四 | ✅ 已出 |

**未触碰**：`MapGenRules.cs`／`MapRenderService.cs`／`GameScene.unity`／`UnitsFrames.spriteatlas`／任何 `.png` 导入设置（PPU／pivot／压缩／尺寸）——本批**零业务代码、零贴图本体改动**。

---

## 二、排雷 M1~M6 逐条自答

| # | 雷 | 自答（实测） |
|---|---|---|
| **M1** | 图集 V1/V2 模式差异 | ①`EditorSettings.spritePackerMode` 实测 = **`SpriteAtlasV2`（枚举值 5）**；②但脚本新建的 `SpriteAtlas` 默认 **`isAtlasV2 = 0`（legacy V1）**，与 `UnitsFrames` 逐字段同口径（`bindAsDefault 1`／`isAtlasV2 0`）；③**实测 V1 在本工程编辑器 Play 态确实生效**：`UnitsFrames` 旧图集运行时贴图名 = `sactx-0-2048x2048-DXT5\|BC3-UnitsFrames-bacae713`（单位 sprite 全部已入页） ⇒ **无需切 V2、无需 LateBinding、无需改 EditorSettings**。 |
| **M2** | 单张图集尺寸上限 | 实测产出页 = **`sactx-0-1024x512-DXT5\|BC3-GroundFeature-…`（1024×512）**（22 张入集：6 地皮 170~172×200~202 px ＋ 16 特征物 48×48 ~ 254×248 px），**远低于 `maxTextureSize 2048`** ⇒ **无需按层分包，同包反而更优**（单页 = 地皮与特征物同纹理 ⇒ 消除两者之间的贴图交替）。`maxTextureSize` 仍照先例设 2048（是**上限**，不是页尺寸）。 |
| **M3** | 补图后形状与生成算法不匹配 | 山地图**未到** ⇒ 实机仍为「散点占位色块」（运行期日志逐次复现 `[MapRenderService] 特征物无真图：ft=Mountain/SnowMountain`）⇒ **形状问题归 HH.272 §二④ 山脉化**，本批**未擅动生成算法**（如实声明，未找补）。 |
| **M4** | 单位缺态工作量未知 | 已出**逐族×逐职业×逐态**清单（§四）＋工作量估算；**报策划端定范围**，**未闷头全补**。 |
| **M5** | 补图属美术产出 | **图源确认＝无来源**（见 §五）⇒ **件2 列报阻塞，未自行生成替代素材**（含未动用 `pixel-forge`／未挪用 `Medieval Kingdom/` 参考包素材）。 |
| **M6** | 承 `L-29`/`L-30` | 判定线可达性自查：图集在场（资产 ＋ `packables` 计数）／DrawCall（正门 `EnterTestRun` ＋ `UnityStats` 同源）／`CanBindTo`（运行时）／取色命中率（截图＋占位 tile 计数）**均可实测**，无结构性不可达；**未为过线凑数**（线 4 判定为 ❌，见交付报告）。 |

---

## 三、图集方案 ＋ DrawCall 基线复述

### 方案（照 `UnitsFrames.spriteatlas` 先例）

| 项 | 取值 | 与 `UnitsFrames` |
|---|---|---|
| 资产名／路径 | `GroundFeature.spriteatlas` @ `Assets/Resources/Config/Art/` | 同目录 |
| `packables` | **2 个文件夹**：`Assets/_Game/Art/Ground`（6 地皮）＋`Assets/_Game/Art/Buildings/neutral/features`（16 特征物） | 同为「文件夹入包」口径 |
| `maxTextureSize` | 2048 | 同 |
| `filterMode` | 1（Bilinear） | 同 |
| `padding` / `blockOffset` | 4 / 1 | 同 |
| `enableRotation` / `enableTightPacking` / `enableAlphaDilation` | 1 / 1 / 0 | 同 |
| `textureCompression` / `generateMipMaps` / `sRGB` / `readable` | 0 / 0 / 1 / 0 | 同 |
| `isAtlasV2` / `bindAsDefault` / `variantMultiplier` | 0 / 1 / 1 | 同 |

**入包实证**：`m_PackedSpriteNamesToIndex` 共 **22 项** = 地皮 6（`ground_temperate`/`_tropical`/`_cold`/`_subtropical`/`_river`/`_ocean`）＋特征物 16（`feat_mine`/`_orevein`/`_stone_pile`/`_deadwood`/12 张树）；运行时 `atlas.spriteCount = 22`。

### DrawCall 基线复述（本机 · 正门 · seed 21107 / Medium / diff2 / 1x · 同机位 · 同源 `UnityStats`）

| 观测位 | 本轮实测（三次） | HH.275 记载 |
|---|---|---|
| 默认视域（ortho 8.64） | 638 / 639 / 628 | 80→**652** |
| 主城 home(128,128) ortho 8.64 | 663 / 664 / 654 | — |
| 近景（贴最近单位 ortho 2.5） | 129 / 130 / 133 | 39→307 |

> 口径注（`L-35`）：三次「改前」跑分离散 ≤11（≈1.7%），故取**同结构探针**逐项对照，禁跨口径混比。

---

## 四、单位缺态欠账清单（首版）＋工作量估算

### 4.1 实测基线（实读 `SpriteRefTable.asset`）

- 单图 `entries` = **175**（`ammo 8`／`bld 89`／`feat 16`／`fx 3`／`ground 6`／`machine 4`／`portrait 46`／**`unit 3`（仅 3 张怪物 icon）**）
- 序列帧 `frameEntries` = **152**（`_idle 48`／`_walk 49`／`_attack 45`／`_run 3`／`_loot 3`／`_strip 4`／**`_death 0`**）
- 角色总数 = **49**：human 12／elf 12／dwarf 11／orc 11／monster 3

### 4.2 逐态欠账（**含口径勘正**）

| 态 | 现役覆盖 | 判定 | 依据 |
|---|---|---|---|
| `idle` | **48 / 49** | ⚠️ 欠 **1**（**非补图·属切帧口径修复**） | 缺 `unit_human_archer_idle`：**文件在盘**（`Art/Units/human/archer/unit_human_archer_idle.png`，**96×102 非正方**），被 `ArtImportPipeline.cs:35` **显式排除**（`Excluded` 清单） |
| `walk` | 49 / 49 | ✅ 齐 | — |
| `attack` | 45 / 49 | ✅ 齐（对口） | 缺的 4 个 = 四族 `child`（幼儿非战斗，**无 attack 属预期**） |
| `run` | **3 / 3** | ✅ **非欠账（勘正）** | `SpriteAnimator.cs:184`：**仅 `IsCharging`（坐骑）走 `StRun`**，且注释明写「非坐骑无 run 帧 ⇒ 回落 walk 并加速」；现役坐骑恰 3 个 = human `knight`／elf `deerrider`／orc `wolfrider` ⇒ **`3 条`正是全量，非缺失** |
| `loot` | **3 / 3** | ⏸ **非欠账（勘正）·且当前无收益** | 3 条恰为 monster×3；且 `SpriteAnimator.NotifyLoot()` **全库零调用点**（HH.266 已列报「Loot 触发方未接」）⇒ 触发方未接线前补帧**零收益** |
| `death` | **0 / 49** | 🔴 **真实欠账（P0）** | `SpriteAnimatorConfig.stateFallback[5] = -1`（Death 无回退）⇒ 走 F-09 链成静态立绘；且 `UnitController.cs:679` 调 `NotifyDeath()`、`:712` 用 `HasFramesFor(StDeath)` 判帧存在性 ⇒ **缺帧即整个死亡动画被跳过**，影响**全部 49 个角色** |

> **勘正**：任务书 §二件3 记「`idle`／`walk`／`attack` 齐」，实测 `idle` 欠 1；任务书把 `run`／`loot` 当欠账，实测**两者均为「天然只有 3 个」的设计结果**，补帧无意义（`run` 仅坐骑、`loot` 无触发方）。

### 4.3 工作量估算

| 优先 | 项 | 数量 | 单件口径 | 帧数 | 备注 |
|---|---|---|---|---|---|
| **P0** | `death` 序列图 | **49 张** | 1 态 = 1 张 sheet，切帧 8~16 帧（建议 8） | **392**（按 8 帧）／784（按 16 帧） | 四族 46 ＋ monster 3 |
| **P1** | `unit_human_archer_idle` | **1 张**（重出） | **重出为 114×114 正方**（与其余 idle 同规格） | 8 | **优先于 P0？否**——属「1 文件重置规格」，可随 P0 一并出 |
| — | `run` 补帧 | 0 | — | — | **不建议**（口径勘正：非欠账） |
| — | `loot` 补帧 | 46 | — | — | **不建议**（触发方未接线，补了看不到） |

**配套代码义务（须一并签发）**：`ArtImportPipeline.TryGrid`（`ArtImportPipeline.cs:207-215`）**无 `_death` 规则** ⇒ 补图前必须**先加** `_death` 的切帧规则（建议同 `_idle`＝3×3 切 9 播 8，或按美术定的帧数），否则新图不会进 `frameEntries`。

**范围建议（报策划端裁）**：本件**只补 P0 `death` × 49 ＋ P1 `human_archer_idle` × 1（重出规格）**＝50 张 sheet，合计约 **400 帧**；`run`／`loot` **建议裁不补**（非欠账／无触发方）。

---

## 五、图源确认（件2 阻塞证据）

| 查面 | 结果 |
|---|---|
| 全库 glob `*mountain*` | 仅命中 `Medieval Kingdom/Sprites/Environment/mountains.png`（**外部参考包·untracked·非本工程 artId 体系**） |
| `美术资源文件夹/Buildings/neutral/features/` | 仅既有 **16 张 `feat_*`**，**无** `feat_mountain`／`feat_snowmountain` |
| `Assets/_Game/Art/Buildings/neutral/features/` | 同上 16 张，**无山地图** |
| `pixel-forge/workspace` | 仅 `ground/`＋`misc/`，**无山地图** |
| 工程全库 `**/*mountain*` | 除上述参考包外**零命中** |

⇒ **结论：山地图素材在本仓库内不存在**。按红线 M5「补图属美术产出 ⇒ 若图未到 ⇒ 如实列报阻塞，不自行生成替代素材」，**件2 列 🔴 阻塞**。

**待美术/用户产出的规格（照 `美术资源规范_等轴立方体瓦片.md`）**：
- 命名：`feat_mountain.png`／`feat_snowmountain.png`
- 规格：**等轴菱形 2:1 · PPU 100 · 1 格**（`128×64` 逻辑格；可含高度层，占位现值 `layers=3`）
- 落点：`Assets/_Game/Art/Buildings/neutral/features/`
- 接入（素材到位后由执行端一次完成）：①入 `SpriteRefTable.asset` 真图键 ②自动入 `GroundFeature.spriteatlas`（该夹已是 packable）③`MapRenderService.FeatureArtId` 补 2 个 case（**属业务代码·须另签发**）

---

## 六、备注

1. 图集**不加** `GameScene.unity`／不改任何 sprite 引用；生效方式＝**packable 隐式接管**（运行时实测贴图已归并至图集页）。
2. 本批 Unity 工具面走 **`mcp_unity-bridge`（Codely 桥）**——双 MCP 已注册可用，`exec_editor_script` / `exec_runtime_script` 全程正常，**无需回退终端 TCP 直连**（对照 `HH.270` 的 D698 §3 应急）。
3. 为取「改前」实机截图，图集曾**临时移出 `Assets/`（→ `Temp/*.bak`）并在截后原地恢复**；恢复后 `spriteCount = 22` 复核在位。

> 执行端 · 2026-09-14 · 依据 HH.276 任务书 ＋ D723 派工总纲
