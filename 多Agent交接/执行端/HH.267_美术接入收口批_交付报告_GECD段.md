# HH.267 美术接入收口批 交付报告（第②段：G / E / C / D 段）

> 类型：施工交付报告（分段交付·第②段）｜状态：🟡 **待验收**
> 日期：2026-09-13 · 提交端：**执行端（TraeCode）** · Gate：`G3-1`
> 关联：`多Agent交接/策划端/HH.264_美术接入收口批_任务书.md`（权威）·`改造计划/美术资源接入_SpriteAnimator播放器设计规格.md`（§7.1/§7.2/§7.3·唯一实现规格）·**`0.6 §二百三十九（D710）`／`§二百四十（D711）`**（E/G 段由来）·**本批第①段＝HH.266（A+B 段·另验收）**·`改造计划/美术资源接入映射表.md` §六/§八/§十.2
> 取号：**HH.267**（账本水位线 266→267·独立 commit `c0344e3f`）
> commit 串：`e7b2803e`（G 段）→ `6d0b9859`（C/D 段）→ `c0344e3f`（取号）→ 本报告 commit；**未 push**

---

## 〇、一句话结论 ＋ 验收线判定

**G 段（特征物接线）＝✅ 成立**（真图生效＋运行中实名＋截图）；**E 段（只读诊断＋E6 报审）＝✅ 产出**（E1~E4 全项＋E6 四候选与代价）；**D 段（图集）＝✅ 成立**（入集不失效＋三件不破＋DrawCall 对照）；**C 段（§7.1 预算）＝🔴 未达**（`LateUpdate` 段 **0.2647ms/帧** @392 单位 · Editor/Mono 口径 · 判据 <0.1ms）——**GC Alloc 0 B/帧 ✅ 达成**；C 段**含 1 项口径报裁**（见 §五-1）。

| # | 验收线（任务书 §三） | 判定 | 证据锚点 |
|---|---|---|---|
| 1 | A 段 F-04/F-09/F-13/F-15 逐项 file:line ＋ SO 实读 | ✅（HH.266 已交·本轮未回退） | HH.266 §一 |
| 2 | B 段 P1~P8 逐条读数 | ✅（HH.266 已交·11/11＋12/12） | HH.266 §二 |
| 3 | C 段 Profiler 读数 **<0.1ms** ＋ GC **0 B/帧** | 🔴 **未达**（0.2647ms）/ ✅（0 B/帧） | 本报告 §三 |
| 4 | D 段 DrawCall 对照 ＋ 入集后 sprite 不失效 | ✅ | 本报告 §四 |
| 5 | 回归：编译 0 error ＋ `ExitTestRun`＋`QuitSmoke`＋退 Play；四族四轮冒烟不退化 | ✅ | 本报告 §五-回归 |
| 6 | 零改动面：两 SO 零改 / `AI.Core` 零触 / 不另建 LOD | ✅ | 本报告 §六 |
| 7 | E 段（只读）：E1~E4 全项 ＋ E6 方案候选与代价 ＋ 原文引用 | ✅ | 本报告 §二 |
| 8 | 视觉客观化（运行中 sprite 实名含 artId 前缀 ＋ 截图·覆盖特征物层） | ✅ | 本报告 §一-G4 |
| 9 | G 段：`feat_*` 键与素材逐项对齐 ＋ 运行中 `Tile.sprite` 实名（`feat_` 前缀）＋截图 | ✅ | 本报告 §一-G1~G4 |

---

## 一、G 段 · 特征物接线（治"仍有色块"）

### G1/G2 施工（代码面）

| 项 | 落点 | 说明 |
|---|---|---|
| 查表取真图 | [MapRenderService.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Rendering/MapRenderService.cs#L352-L405) `FeatureTileOrNull(ft,x,y)` | 由**硬编码 `CreateIsoTile(色块)`** 改为**先查 `SpriteRefTable`**（照 T9 建筑接线模式：真图优先 ⇒ miss 回退占位） |
| artId 求值 | 同文件 `FeatureArtId`（L387） | Tree ⇒ `BuildingVisual.TreeArtId(new GridCoord(x,y))`（**复用 H2 格坐标确定性哈希＋温度带名＝单一源**，禁另造）；Mine/OreVein/StonePile/WoodPile ⇒ 单键；Mountain/SnowMountain ⇒ **null**（映射表 §十.2 未定义素材） |
| 缓存键 | 同文件 L38-L43 | `_featureTiles` 由 `Dictionary<FeatureType,Tile>` 改 **`Dictionary<string,Tile>`**（artId／`ph:{ft}`）——树 4 气候×3 变体同型多图 |
| 铺格传格坐标 | 同文件 L210 `SetCell` | `FeatureTileOrNull(ft, x, y)`（温度带/变体需格坐标） |
| 一次性告警 | 同文件 `WarnMissingFeature`（L401） | miss ⇒ 保持占位色块 ＋ **同 key 只告警一次**（L-05 禁刷屏） |

### G3 键空间逐项对齐（实盘 vs 表）

```
素材实盘 16 个：feat_deadwood, feat_mine, feat_orevein, feat_stone_pile,
  feat_tree_{cold,subtropical,temperate,tropical}_{1,2,3}
表内 feat_* 键 16 个：同上（逐项一致）
仅实盘有（未落表）：无          仅表内有（素材缺）：无
```

> ⚠️ **列报（G3 禁静默）**：`Mountain`／`SnowMountain` **在映射表 §十.2 无 `feat_*` 定义、实盘亦无素材** ⇒ 本次按 G2 走占位＋一次性告警，**并报裁**（见 §五-2）。两型占位数实测恰为 496＋403＝899，与"占位总数"逐位吻合（＝无静默漏接）。

### G4 运行期实名（视觉客观化·**禁"表里有键"充数**）

容器：[Valley_HH264_FeatureProbe.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH264_FeatureProbe.cs)（正门 `TestHarnessApi.EnterTestRun`·seed 21107）；读数载体 `Logs/hh264_feature_probe.log`；**探针 10/10 ALL PASS**。

```
特征物层运行期抽样（Tilemap_Feature cellBounds=Position(0,0,0), Size(168,168,1)）
  tile 总数=2327   真图(feat_* 实名)=1428   占位(sprite 名空)=899
  distinct sprite 名=17 → feat_deadwood×47 feat_mine×390 feat_orevein×83 feat_stone_pile×69
    feat_tree_cold_1×13 _2×8 _3×8 / feat_tree_subtropical_1×131 _2×121 _3×137
    feat_tree_temperate_1×84 _2×71 _3×71 / feat_tree_tropical_1×62 _2×64 _3×69  ＋(空名×899)

G4 运行中实名（Tile.sprite.name）：
  Tree       → "feat_tree_subtropical_3"   n=839   ✅ feat_ 前缀
  Mine       → "feat_mine"                 n=390   ✅
  OreVein    → "feat_orevein"              n=83    ✅
  StonePile  → "feat_stone_pile"           n=69    ✅
  WoodPile   → "feat_deadwood"             n=47    ✅
  Mountain   → ""（占位）n=496 ／ SnowMountain → ""（占位）n=403   ← 无素材·列报
G2 一次性告警条数=2（＝Mountain/SnowMountain 各 1 条 ⇒ 禁刷屏 ✅）
```

**截图**：`Logs/hh264_g_visual_features.png`（1024×437·运行中 Game View）——图中**树/矿/石堆/木堆为真图**（可见树冠、矿洞橙点、矿脉/石堆/枯木贴图），山/雪山仍为灰白色平面占位菱形（＝上述列报项）；地皮为 `ground_*` 真图（立体块形态）。

---

## 二、E 段 · 渲染路径全景诊断 ＋ 几何量测（**只读 · 未改任何 `Assets/**`**）

> 读数来源：编辑器侧只读探针（`execute_code`·仅读资产/场景/纹理，未写盘、未改资产）＋代码实读。
> E5 定性已由 **D711 定**（立体块＝设计如此）⇒ 本段不含 E5。

### E1 渲染路径全景（`FeatureType` → 渲染载体 → 是否查表）

| FeatureType | 渲染载体 | 查 `SpriteRefTable`？ | 代码锚点 |
|---|---|---|---|
| 全部（地皮） | `Tilemap_Ground`（Tilemap·order 0） | ✅ **是**（`ground_*`·按温度带/水系） | [MapRenderService.cs#L303-L340](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Rendering/MapRenderService.cs#L303-L340) |
| Tree / Mountain / SnowMountain / Mine | `Tilemap_Feature`（order 10） | ❌→✅：**改前硬编码色块（零查表）**；本批 G1 已接 `feat_*` | [MapRenderService.cs#L352-L406](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Rendering/MapRenderService.cs#L352-L406) |
| OreVein / StonePile / WoodPile | ① `Tilemap_Feature`（同上·本批已接真图）**＋② `Building` 实体**（`BuildingFactory`←`naturalBuildings`） | ①✅（本批）②✅（`BuildingVisual.ApplyPlaceholder`→`PlaceholderSprites`→表） | [MapGenRules.cs#L541-L563](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGenRules.cs#L541-L563)／[BuildingVisual.cs#L132-L150](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingVisual.cs#L132-L150) |
| River / Lake / Ocean / Plain | 仅 `Tilemap_Ground`（Feature 层返回 null） | ✅（水按水系；湖复用 `ground_river`） | 同上 |

**原文件与证据**（`MapGenRules.DeriveNaturalBuildings` 注释原文）：「**树/矿/雪山不再派生 Building 实体**——它们归 2_10 Tilemap 特征层渲染 + features 数据承载（装饰持续节点）…**仅真正一次性可采集的 OreVein 保留 Building 实体**」⇒ 与 D710 判定"HH.263 的 5 线接线清单里根本没有特征物这一线"**完全一致**（Tree/Mountain/SnowMountain/Mine 只走 Tilemap ⇒ 该层不查表 ⇒ 全走色块）。
**改后运行期实测**：特征物层 tile 2327 中 **真图 1428**（占位 899 恰＝无素材的 Mountain/SnowMountain）；同格 Building 实体 total=2442（OreVein 1137／StonePile 752／WoodPile 553）⇒ **该三型存在"双渲染面"**（列报·§五-3）。

### E2 几何量测（真图 vs `cellSize` vs 占位菱形·**三方对照**）

| 对象 | 像素 | @PPU100 世界尺寸 | 不透明 bbox | 锚点(pivot) |
|---|---|---|---|---|
| `ground_temperate` | 172×202 | 1.72×2.02 | x[22..149] y[37..141]＝**128×105** | (86,101)px＝**归一 (0.50,0.50) 图像中心** |
| `ground_subtropical` | 170×200 | 1.70×2.00 | 128×96 | (85,100)＝0.50 |
| `ground_cold` | 172×200 | 1.72×2.00 | 128×96 | (86,100)＝0.50 |
| `ground_ocean` / `ground_river` | 170×202 | 1.70×2.02 | 128×96 | (85,101)＝0.50 |
| **占位菱形**（`CreateIsoDiamondSprite`） | 128×64 | **1.28×0.64** | 满幅 | (64,32)＝0.50 |
| **`cellSize`（实读 Grid 'MapRender'）** | — | **(1.28, 0.64, 1.00)**·`IsometricZAsY`·swizzle XYZ | — | — |

**结论**：①真图**宽度＝1.28 世界＝1 格**（128px）✅ 与 cellW 对齐；②真图**高度 2.00~2.02 世界 ＝ cell(0.64) 的 3.16×**；不透明轮廓纵向分解＝顶面菱形 64px（2:1·对齐 cell）＋**侧壁 40px（＝厚度感·D711 设计如此）**＋外发光/描边；③占位菱形 128×64 是**平面 2:1**、无侧壁 ⇒ **两者几何模型不同**（D710 判定坐实）。
④**锚点口径不一致**：真图实测**顶面菱形中心在 y≈109/202＝0.540**，而 `ArtImportPipeline` 写入的 pivot＝**0.50（图像中心）** ⇒ 差 **8px**。

### E3 tile 锚点与白缝来源

- **Tile 落点**：`Tilemap.tileAnchor=(0.5,0.5)`（三张 Tilemap 全同）⇒ sprite 的 pivot 点落在 **cell 中心**。
- **实体落点**：单位/建筑走 `GridSystem.CoordToWorld`＝`MapRenderService.GridToIso`（[GridSystem.cs#L154-L160](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Grid/GridSystem.cs#L154-L160)）＝`((x−y)·0.64, (x+y)·0.32)`。
- **实测量差（4 抽样点恒定）**：`Tilemap.GetCellCenterWorld(cell) − GridToIso(cell) = (0, +0.32)`（cell (0,0)/(1,0)/(0,1)/(3,5) 四点同值）。
  ⇒ **Tile 锚点比逻辑/实体锚点高 0.32 世界单位（＝半个 cell 高）**；再叠加 E2④ 的 **pivot 8px(0.08)** ⇒ 地皮可见顶面中心比"实体脚下"高 **≈0.40 世界单位 ≈ 40px ≈ 1.25 行**。
- **白缝候选机制（量测依据充分·像素级归因归重构批）**：真图纵向 2.0 世界 vs cell 0.64 ⇒ 相邻行地块**压盖 1.38 世界**；同时顶面锚点上移 0.08 ⇒ 相邻块顶面**不共面**（差 8px）＋真图外缘为**柔边/透明padding**（bbox 外仍有 22/23/37/61px 透明边）⇒ 相邻块顶面之间会露出 1~8px 的背景/缝隙。**本段给"候选机制＋量测"，像素级复核建议放重构批**（承 L-30 不夸口径）。

### E4 排序链现状（实读）

| 载体 | sortingLayer | sortingOrder | 备注 |
|---|---|---|---|
| `MapVisualizer` 底图 | Default | −100 | 代码 [MapVisualizer.cs#L77](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Grid/MapVisualizer.cs#L77) |
| `Tilemap_Ground` | Default | **0** | 场景实读 |
| GroundEffect（命中特效） | Default | 0 | 代码 |
| 单位 `SpriteRenderer` | Default | **1** | [UnitController.cs#L315](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Unit/UnitController.cs#L313-L316) |
| 建筑 `SpriteRenderer` | Default | **1** | [Building.cs#L518](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L518)／[BuildingVisual.cs#L148](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingVisual.cs#L148) |
| 调试探针 | Default | 2 | 仅调试 |
| `ChestEntity` | Default | 5 | 代码 |
| `Tilemap_Territory`（领地染色） | Default | **5** | 场景实读 |
| `BuildProgressBar` 底/填充 | Default | 10 / 11 | 代码 |
| **`Tilemap_Feature`（特征物）** | Default | **10** | 场景实读 |
| `IClickInteractable` 标记 | Default | 100 | 代码 |
| UI（面板/Toast） | — | 30000 / 32000 | 代码 |
| 相机 | — | `orthographic=True` size≈6.07 | **`transparencySortMode=CustomAxis`·`sortAxis=(0,1,0)`**（＝D104 约定 ✅） |

**链上问题（实读结论）**：`TransparencySortMode=CustomAxis` **仅在"同 sortingLayer ＋ 同 sortingOrder"内生效** ⇒
①**特征物层 order=10 恒压单位/建筑（1）与领地（5）**——树/山/矿永远盖在其"后方应被遮住"的单位/建筑之上（等轴深度语义失效）；
②地皮（0）→单位/建筑（1）同 order 内 Y-sort 正常，但地皮真图纵向溢出 3.16× ⇒ 溢出部分压在单位/建筑之上（立体块"吃"脚下单位）；
③全部载体 `sortingLayer` 均为 `Default`（未分层）。

### E6 地皮渲染方案候选与代价（**报审项 · 报裁后再定施工**）

> 实测基线（§三 C3/D 段）：地皮/特征物/领地三张 Tilemap（Chunk 模式）＋ UI ＋ 场景其余 ⇒ 整场 DrawCall **avg 84.48 / max 88**（含 360 单位四族混编；图集前后 Δ 仅 −3.5，见 §四）⇒ **DrawCall 主体在 Tilemap 层与 UI，不在单位**。地图 168×168（cellBounds 实读）；`chunkSize=24`＋`lookaheadChunks=1` ⇒ 视域装载 3×3 chunk＝**72×72 格/层**。

| 候选 | DrawCall 估算 | 对象数 | `MapRenderService` 改造面 | 对 H5 预算影响 | 与既有回退链兼容性 |
|---|---|---|---|---|---|
| **(d) 锚点归一（最小代价·建议先验）** 单 Tilemap 保持，**地皮 tile 锚点改「顶面菱形中心」并统一 tile/实体锚点**（消 0.32＋0.08 双偏） | **Δ ≈ 0**（仍 1~2 批/层） | **Δ 0** | **极小**：`CreateSpriteTile` 落 tile 时按"顶面菱形中心 → 格中心"换算 ＋ `ArtImportPipeline.ComputeSinglePivot` 地皮规则 1 行（0.50 → 顶面中心 0.540H 的稳健求法） | **零增量**（甚至因消抖略优） | **天然保留**（仍走 `ground_*` 查表＋占位菱形回退） |
| **(a) 行分层多 Tilemap** | 可见行 ≈（72+72）=**144** 行 ⇒ **≈144 批**（同图不跨行合批）⇒ **Δ ≈ +140** | **+168** 个 Tilemap/Renderer | **大**：铺格/chunk/清理/场景层级全改（每行一 GO） | **显著负向**（144 个 Renderer 的剔除＋提交） | 需在重写中保留 `ground_*`/占位回退 |
| **(b) 逐格 SpriteRenderer ＋ 等轴深度排序** | 同图集下可合 **1~10 批**，但 5184 个 SR 的逐帧 culling/排序开销 ≈ **0.3~0.6ms 量级** | 视域格数 **≈5184**（地皮）＋特征物（实战 ~1400） | **最大**：弃 Tilemap 层，新建对象池＋深度排序＋增量刷新 | **显著负向**（CPU 提交） | 需重建回退路径 |
| **(c) 分块烘焙** | 可见 chunk ≈ **9**（全图 49） ⇒ 每 chunk 1~3 批 ⇒ **Δ ≈ +8~+27** | **+49**（全图 chunk 数） | **中**：新增烘焙管线＋失效重烘触发（砍树/地形/废墟态 → 现 `UpdateCell` 增量语义冲突） | 内存↑、重烘卡顿 | 需把回退链前置到烘焙期 |
| **(d′) 其他（列报备选）** 顶面（2:1）走现平面 Tilemap ＋ 侧壁独立层（按行 order） | 顶面 1~2 批＋侧壁 ≈行数/2 批 | +1 层 | 中：需把地皮真图**离线裁成顶面/侧壁两片**（改图！与 D711"不是改图"冲突）⇒ **不建议** | 中 | 保留 |

> **建议（执行端视角，供裁）**：先验证 **(d) 锚点归一**（代价≈0、可逆、零 DrawCall 增量）能否消除"错位/白缝"；若"相邻块压盖"本身即设计（D711 的厚度感）⇒ **(d) 即可收口**；若压盖亦不可接受 ⇒ 再按 (a)/(c) 排序评估（(b) 对 H5 负向最大，最后考虑）。

---

## 三、C 段 · §7.1 预算（H5）——🔴 **未达（含口径报裁）**

容器：[Valley_HH264_PerfProbe.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH264_PerfProbe.cs)（**正门 `TestHarnessApi.EnterTestRun`**·seed 21107·1x）；读数载体 `Logs/hh264_perf_before.log`／`hh264_perf_after.log`。
口径：**段隔离**（`driver.enabled=false` ＋ `Delegate` 直调 `LateUpdate`）·`Stopwatch` 包夹 ＋ `GC.GetAllocatedBytesForCurrentThread()` 计数；warmup 60 帧／采样 180 帧。

```
C0 四族混编规模：spawn=360（四族各 90：Human/Elf/Dwarf/Orc）→ r0=90 r1=90 r2=90 r3=90
   driver 活跃=392（360＋世界生成既有 32）  tier Active=360 Semi=0 Dormant=32
   state idle=132 walk=140 attack=120（三态覆盖·由探针 NotifyMove/NotifyWork 驱动）
C1 `SpriteAnimatorDriver.LateUpdate` 段：avg=0.2647ms/帧  max=0.7091ms   ｜harness 底噪(空委托同法包夹)=0.0004ms
   ⇒ 净耗时≈0.2642ms/帧  —— 🔴 判据 <0.1ms **未达**
C2 GC Alloc：avg=0 B/帧  max单帧=0 B（180 帧合计 0 B）  —— ✅ 判据 0 B/帧 **达成**
C1b 零推进对照（timeScale=0 ⇒ dt=0：同遍历同调用·无换帧/无 sprite 写）：avg=**0.0952ms/帧**
C1c 成本构成差：0.2647 − 0.0952 = **0.1694ms** ⇒ 「帧推进 ＋ sprite 写」段
C3 采样期 DrawCall：avg=84.48  max=88（UnityStats.drawCalls）
```

**成本构成判读（供报裁）**：
- 「**每 slot 调用开销**」下限已 **0.0952ms/帧**（392 slot ⇒ ≈0.24µs/slot）——即**即使零推进也几乎吃满 0.1ms 预算**；
- 「帧推进＋sprite 写」0.1694ms：三态混合下换帧写 ≈ **78 次/帧**（392×12fps/60）⇒ **≈2.2µs/次**，即**原生 `SpriteRenderer.sprite` setter 单价**是主导项；
- 与规格 **§1.1 的设计预期「400 次 float 加法＋索引比较 ≈ 0.02~0.05ms」** 相差 **5~13×** ⇒ 规格成本模型**未计入**「每 slot 跨对象调用（EnsureSet/DesiredState/DesiredMode/DecayIntent/FpsOf/LodScaleOf/FacingLeft/SpeedScale ≈8~10 次/slot）」与「**原生 sprite 写单价**」两项。
- **口径要素**：本次为 **Tuanjie 1.8.5 Editor Play（Mono）** 读数；§7.1 判法原文＝「Profiler 截图（`SpriteAnimatorSystem.Update` 段）」，**未指定 Editor 还是构建包**。Editor/Mono 的调用与原生 setter 单价均高于 IL2CPP 发布态 ⇒ 本读数**偏悲观**，不可直接充当发布读数。
- **执行端不据此改代码**：①sprite 写是**动画必需**、无法靠微优化清除；②即便做「每 slot 调用收敛」，仅能把下限 0.095→~0.04ms，**总量仍在 0.15ms 量级** ⇒ 不改判定；③A/B 段（已交付）刚过验证，禁在同一批内叠加未验证的结构改动（**禁为过线凑数**·L-30）。⇒ 列 **报裁**（§五-1）。

---

## 四、D 段 · §7.3 图集（H6）

**载体择一（理由）**：**新建 `.spriteatlas` 资产** `Assets/Resources/Config/Art/UnitsFrames.spriteatlas`，生成入口并入既有流水线 `ArtImportPipeline.EnsureSpriteAtlas()`（`RunAll` 第三/四步）。
理由：①**文件夹 packable**（`Assets/_Game/Art/Units`）⇒ 新帧自动入集、**零逐文件维护**；②与导入设置/键表**同源同入口**（同一条流水线，不双源 L-15）；③不触碰 152 个 Sheet 的逐图 `.meta`（避免像改导入设置那样触发大面积重导）；④`SpriteAtlas` 为引擎原生机制，编辑器与构建内绑定语义一致。

```
图集：Assets/Resources/Config/Art/UnitsFrames.spriteatlas
  packables=1（Assets/_Game/Art/Units ⇒ 四族 unit_* ＋ monster/* 全覆盖）
  spriteCount=2015   includeInBuild=True   （打包：SpriteAtlasUtility.PackAllAtlases 强制落盘）
  注：本仓 EditorSettings.spritePackerMode 原值＝SpriteAtlasV2（**未改动**）

① 间接层不破（入集后引用不失效）：
   SpriteRefTable 逐键查表命中 4/4，且 atlasCanBind 全 True：
     unit_human_warrior_idle → unit_human_warrior_idle_0_0   (CanBindTo=True)
     unit_orc_worker_attack  → unit_orc_worker_attack_0_0    (CanBindTo=True)
     unit_elf_archer_walk    → unit_elf_archer_walk_0_0      (CanBindTo=True)
     unit_monster_raider_idle→ unit_monster_raider_idle_0_0  (CanBindTo=True)   ← **monster 入集实证**
   运行中单位 SpriteRenderer.sprite=unit_orc_worker_idle_1_0（表内同一 sprite 对象·引用有效）
② 缺图回退链不破：bld_academy → nonNull=True 且非表内真图 ⇒ 占位路径仍生效（不崩）
③ 统一 pivot / 排序稳定：unit_human_warrior_idle 8 帧逐帧同尺寸同 pivot
   pivot=(19.00,3.00) rectSize=(38.00,38.00)（before／after 同值 ⇒ 图集未改 pivot/尺寸）
DrawCall 前后对照（同容器·同 seed·同 360 单位四族混编）：
   图集前 avg=84.48 max=88   →   图集后 avg=80.98 max=85   （Δ avg −3.5 / max −3）
```

> 判读：图集**成立且安全**（三件不破✅＋monster✅），但对**整场** DrawCall 的收益有限（−3.5）——因场景 DrawCall 主体是**地皮/特征物/领地 Tilemap ＋ UI**（见 §二 E6 基线），单位贴图只占小头。此结论同时为 E6 的"改哪里才省 DrawCall"提供实测依据。

---

## 五、回归 · L-34 · L-35 · 列报

### 5.1 四族四轮回归对照（同源工具＝HH.263 基线 `Valley_HH239_ArtProbe`·未改入场路径）

| 轮 | 建筑 real/total | 主城 k0 | 单位帧集 | 地皮 distinct | **G 段特征物层 real/sampled** | 回退三项 |
|---|---|---|---|---|---|---|
| R0 human | **2463/2463** | `castle_human_lv1` | 34/34 | 5 | **167/268**（distinctFeat 15） | 如基线 ✅ |
| R1 elf | **2468/2468** | `castle_elf_lv1` | 34/34 | 4 | **169/270**（15） | 如基线 ✅ |
| R2 dwarf | **2463/2463** | `castle_dwarf_lv1` | 34/34 | 5 | **167/268**（15） | 如基线 ✅ |
| R3 orc | **2456/2456** | `castle_orc_lv1` | 34/34 | 5 | **167/268**（15） | 如基线 ✅ |

- **对 HH.263 基线不退化**：建筑 2456~2468 全真图（基线同区间）·主城不串族·单位帧集 34/34·地皮 4~5 种真图·回退负探针（`bld_academy` 占位非崩／`ground_lake`・`orc_windwalker` null 静默／`bld_house_lv2` 命中真图）逐项与基线一致；`SpriteRefTable` 键数 **175 单图＋152 帧**（与 HH.263 同，图集未改表）。
- **新增覆盖**：本轮在基线容器内**增列 G 段特征物层抽样行**（每轮 real 167~169/268 ⇒ 特征物线真图在四族轮内均成立）。
- **测试工具修复（列报）**：基线容器 `Valley_HH239_ArtProbe` 的 `EnsureSet` 反射原用 `BindingFlags.NonPublic`；HH.264 **A 段**已把 `SpriteAnimator.EnsureSet` 提升为 `public` ⇒ 反射取到 null 抛 NRE（**该工具自 A 段起即不可跑**，非本轮引入）。本轮修为 `Public|NonPublic` 并加回归行；**同时留证：`RunRound` 同步采样于建局当帧，单位 `sprite` 读数为 `null` 属采样时点伪读**——同会话内多帧后复采 **34/34 全非空**（含 tier=2 休眠档，`unit_human_worker_idle_*` 等），**非回退失效**。

### 5.2 L-34 在线判据表（五列）

| 判据 | 可判定最早时点 | 命中即停 | 作用域 | 口径来源 |
|---|---|---|---|---|
| G1/G4 特征物层真图生效 | 建局后 **chunk 铺格完成**（首帧末／探针 2s 窗） | 任一必需型 sprite 名**非 `feat_` 前缀**即判 FAIL 并停 | 特征物层 `Tilemap_Feature` 已加载格 | 运行中 `Tilemap.GetSprite(cell).name`（实名·含前缀） |
| G2 缺图一次性告警 | 同 G1（首轮铺格后） | 告警条数 **> 每缺键 1 条**即判刷屏 FAIL | `[MapRenderService] 特征物无真图` | `Application.logMessageReceived` 计数 |
| G3 键空间对齐 | **施工前**（纯资产/表只读） | 出现"仅实盘有"或"仅表内有"即 FAIL | `Art/Buildings/neutral/features/*.png` ↔ 表 `feat_*` | 编辑器枚举（**禁文本 grep**·L-29） |
| C1 段耗时 | 采样窗 **第 1 帧**（warmup 后） | 单点即判（累计 avg 供报告） | `SpriteAnimatorDriver.LateUpdate` 段（隔离直调） | `Stopwatch`＋`GC.GetAllocatedBytesForCurrentThread()`（Mono/Editor） |
| C2 GC 0 B/帧 | 同上 | 任一帧 **>0 B** 即 FAIL | 同上 | 同上 |
| D1 入集不失效 | 图集打包后**首次查表** | `TryGet` 空 或 `CanBindTo=False` 即 FAIL | `SpriteRefTable` 4 代表键（含 monster） | 运行中 `SpriteAtlas.CanBindTo` |
| D3 pivot/排序稳定 | 图集前后各一次 | 逐帧 pivot/rect 不一致即 FAIL | 同 sheet 8 帧 | `Sprite.pivot` / `Sprite.rect.size` 实读 |
| 四族回归不退化 | 每轮**建局后首帧** | 任一轮 real<total 或主城串族即 FAIL | 建筑/单位/地皮/回退 | 同源容器基线对照 |

### 5.3 L-35 口径来源与排除项

**采样条件**：Tuanjie **1.8.5**（Unity 2022.3.62t7）·Editor Play（**Mono**）·Windows·同机同场景·seed 21107·`EnterTestRun`（关阴影/VSync 由门面声明）·1x 速度。
**C 段**：段隔离＝`driver.enabled=false`＋`Delegate` 直调 `LateUpdate`（**已量化 harness 底噪 0.0004ms/帧**并单列，净耗时＝avg−底噪）；`timeScale=0` 为零推进对照（同遍历同调用·无换帧/无写）；GC 取**当前线程已分配字节**精确差值；DrawCall 取 `UnityEditor.UnityStats.drawCalls`（Game View 统计）。
**排除项**：①**构建包/IL2CPP 未采**（Editor/Mono 单价高于发布态 ⇒ 读数偏悲观，**不得当发布读数**）；②未开编辑器 Profiler 深采（未叠加采样开销）；③单位状态由探针驱动、`NPCBrain` 禁用 ⇒ **不含 AI 开销**，也不含 AI 引起的换帧分布差异；④32 个世界生成既有单位（远档 Dormant）计入遍历开销、未计入"360 混编"分母；⑤`UnityStats` 反映 Game View 统计，编辑器窗口尺寸/可见性会影响绝对值（前后对照同条件）。
**D 段**：图集绑定生效性以 `CanBindTo=True` 实证（编辑器内）；打包模式＝项目原值 `SpriteAtlasV2`（未改）；`PackAllAtlases` 为强制打包动作（不改资产语义）。
**回归**：沿用 HH.263 基线**同源容器**（入场路径未改＝`SmokeApi.EnterGame`＋`EnableTestHarness(15f)`，为保同源对照）——**本批新增/使用的 G、C/D 容器一律正门 `TestHarnessApi.EnterTestRun`**；每轮收尾 `ExitTestRun`＋`QuitSmoke`＋退 Play（本轮 4 轮＋G/C/D 容器均已退 Play·`isPlaying=False`·`timeScale=1`）。

### 5.4 列报（含报裁）

| # | 项 | 请求 |
|---|---|---|
| 1 | **C 段预算口径**（Editor/Mono 0.2647ms vs 判据 <0.1ms；构成＝零推进下限 0.0952＋换帧写 0.1694） | **报裁**：(a) 口径改「构建包/IL2CPP 采样」并复核；(b) 授权独立微批做「每 slot 调用收敛」（预期仅降下限，总量仍 ~0.15ms）；(c) 重定预算（如 Editor 口径 ≤0.3ms） |
| 2 | **Mountain/SnowMountain 无 `feat_*` 素材**（映射表 §十.2 未定义·实盘亦缺） | **报裁**：补图／补键／明确长期占位（现为占位＋一次性告警） |
| 3 | **一次性资源"双渲染面"**：OreVein/WoodPile/StonePile 既走特征物 Tile（本批已接真图）又走 Building 实体（真图·total 2442） | **报裁**：哪一面为准（建议实体面为准、Tile 层该三型不渲染） |
| 4 | **Mine 2×2 簇按格各铺一张 `feat_mine`**（该图 footprint=2×2）⇒ 同簇 4 张重叠（n=390 ≈ 97 簇） | **报裁**：是否需按簇合并为 1 张 |
| 5 | **E6 地皮渲染方案**（4 候选·含推荐先验 (d) 锚点归一） | **报审**：裁定方案后「地皮渲染重构」另立批（D711 已定其性质） |
| 6 | 基线测试工具修复（`Valley_HH239_ArtProbe` 反射＋回归行） | 知悉（工具级·非业务代码） |
| 7 | **DrawCall 主体定位**：图集收益 −3.5（整场 84.48→80.98）⇒ 省 DrawCall 的着力点在 Tilemap 层/UI | 供 E6 与后续渲染批参考 |
| 8 | **遗留**：`git-plan-sync`（主计划书工作日志＋系统迭代追踪表）自 HH.263 起挂账 | 与本批一并补或另派（未擅自改主计划书） |

---

## 六、零改动面自检

| 红线 | 自检 |
|---|---|
| ❌ 不给 `AmmoDef`/`GroundEffectDef` 加字段（D707 R4 继承） | ✅ 两 SO **零改动**（本批 3 次 commit 只含 3 个代码/资产文件＋1 个测试工具＋1 个图集资产） |
| ❌ 不另建 LOD 系统 | ✅ A4 接**既有** `LODSystem.GetLevelAt`；本批未新增 LOD 代码 |
| ❌ 不接 F-15 钩子到游戏逻辑 | ✅ `SubscribeFrame` 仍零业务调用方 |
| ❌ 不采规格 §八 否决清单（Animator／Burst／GPU 动画／分桶分片／上下半身分层） | ✅ 未采任何一项（图集属 §7.3 配套·非否决项） |
| ❌ **E 段只读**（不改 `Assets/**`） | ✅ E 段仅编辑器只读探针（读资产/场景/纹理）⇒ E1~E4/E6 全部为**读数＋代码引用** |
| 正门 `EnterTestRun`＋收尾真暂停＋`ExitTestRun`＋`QuitSmoke`＋退 Play（L-32） | ✅ G/C/D 容器均正门；每轮收尾已退 Play（末态 `isPlaying=False`） |
| 写-改-commit 同串·只提本批文件·禁 `git add -A`·不 push | ✅ 逐文件 `git add`；`Packages/manifest.json`（bridge 版本环境变更）等无关改动**未提**；**未 push** |
| `多Agent交接/_任务队列.md`（CRLF-blob）不写 | ✅ 一行未写 |
| 取号按实时水位线（D640 #10 禁预留） | ✅ HH.267（独立 commit `c0344e3f`） |

---

> 交付端：执行端（TraeCode）｜2026-09-13｜HH.267｜依据＝HH.264 任务书（G/E/C/D 段＋验收线 7/8/9）＋D710/D711＋规格 §7.1/§7.2/§7.3
