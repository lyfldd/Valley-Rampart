# HH.275 地图区块边界遮挡治本 · 交付报告

- 取号：HH.275（水位线 274→275·`D640 #10` 单行独立 commit）
- Gate：`G3-1`｜发起端：执行端｜状态：🟡 待验收
- 由来：用户实机上报（HH.271/273 后残留）——「每格排序已好，但**一动就有**区块与区块之间的排序问题（看着像 16×16）」
- 本批施工面：`GameScene.unity` **2 行**（`Tilemap_Ground` / `Tilemap_Feature` 的 `m_Mode: 0→1`）
- commit：`0e855c9f`（施工）｜**未 push**

---

## 一、一句话结论

**根因＝`TilemapRenderer.Mode.Chunk` 的结构性限制**：Chunk 模式下**每个 chunk 是一个不可分割的排序项**，其内部三角形顺序只由 `m_SortOrder` 沿**单轴**（x 或 y）决定，**无法表达等轴深度（x+y）** ⇒ 必然产生沿垂直对角线的**长直缝**，且缝**恰在 chunk 边界**上。
**治本＝`Ground`/`Feature` 改 `Mode.Individual`（逐格深度排序）**；`Territory` 保持 Chunk（其菱形恰好一格、不溢出 ⇒ 零差异、零代价）。

---

## 二、先回答用户的问题：程序填入色块时，到底怎么排序？（完整链路）

这是本批必须先说清的一环，**排序不是由我们代码决定的，而是由 `TilemapRenderer` 的三个旋钮叠加决定的**：

| # | 旋钮 | 现值 | 作用 |
|---|---|---|---|
| 1 | `Grid.cellLayout` | `IsometricZAsY` | 等轴布局（Z 参与 Y 偏移） |
| 2 | `TilemapRenderer.m_Mode` | **原 Chunk** | **决定「以什么为单位排序」**（整块 or 逐格） |
| 3 | `TilemapRenderer.m_SortOrder` | `TopRight`(3) | Chunk 模式内**沿哪条轴、朝哪个方向**排 |
| — | 相机 `TransparencySortMode/Axis` | `CustomAxis (0,1,0)` | **仅 Individual 生效**：逐格按屏幕 Y 深度排 |

**我们的代码侧**（`MapRenderService.SetCell` / `RenderChunk`）只做两件事：①`Tilemap.SetTile(格, Tile)` 把图铺上去；②给 `TilemapRenderer.sortingOrder` 设层（Ground −1／Territory 0／单位·建筑·Feature 1）。
**它完全不参与「格与格之间谁在前」的决策**——那是引擎按上面 3 个旋钮算的。

因此：
- **Chunk（原状）**：9 个 24×24 的加载块 → 引擎把它们合并成 mesh；mesh 内**只有一次**排序机会（`m_SortOrder` 单轴）⇒ 单轴序在等轴地图上必然沿另一条对角线出错 ⇒ **长直缝**。
- **Individual（现状）**：每格是独立排序项 ⇒ 引擎按 `CustomAxis` 的 Y 深度**逐格**排 ⇒ 与等轴深度 `x+y` 一致 ⇒ 正确。

---

## 三、根因证据链（全部实测，非推断）

| 序 | 实验 | 读数 | 结论 |
|---|---|---|---|
| E1 | 稳定性复现 | 同机位两次截图字节 **2510670 = 2510670** | 排序**稳定**，非抖动；缝是**世界空间固定图案**，随镜头扫过视野（故用户感觉「一动就在排」） |
| E2 | 差分定位 | `Chunk` vs `Individual` 差 **2.83%**，差分图**恰为 2 条 45° 长直线** ＋ 散点 | 缝＝**区块边界**，形状与用户描述一致 |
| E3 | chunk 缩小能否解？ | **强制重建网格**后 `chunkSize` 32/8/4/2/1 → 差异 **2.83% → 3.79%**（不收敛） | **缩小 chunk 无解**（缝变小变多而已） |
| E4 | 逐层定位 | 仅 `Ground=Individual` → 差异 **0.72%**（**长直缝全部消失**，只剩特征物处小斑） | 缝由 **Ground 层**造成 |
| E5 | 最小正确组合 | `Ground+Feature=Individual`、`Territory=Chunk` → **与三层 Individual 像素差 0.00%** | **Territory 不必改** |
| E6 | 代价来源 | Individual：现状 651；**Feature 层清空** 240；**地皮统一为 1 张贴图** **58**；Chunk+同图亦 58 | 贵的原因是**贴图交替打断合批**，**不是 Individual 本身** |

---

## 四、落地

`GameScene.unity`（场景序列化值，**零代码面**）：

| 对象 | 字段 | 改前 | 改后 |
|---|---|---|---|
| `Tilemap_Ground` | `TilemapRenderer.m_Mode` | `0`(Chunk) | **`1`(Individual)** |
| `Tilemap_Feature` | `TilemapRenderer.m_Mode` | `0`(Chunk) | **`1`(Individual)** |
| `Tilemap_Territory` | `m_Mode` | `0` | **不动**（零差异·零代价） |

`git diff` **恰 2 行**（`-m_Mode: 0` / `+m_Mode: 1` ×2）；不改清单（`m_SortOrder`／`m_ChunkSize`／`m_ChunkCullingBounds`／`m_DetectChunkCullingBounds`／三层 `m_SortingOrder`／三层 `m_TileAnchor`／Grid／相机／代码／SO／贴图）**逐项零出现**。

---

## 五、验收

| # | 项 | 结果 |
|---|---|---|
| 1 | 运行时实名读数 | `Ground mode=Individual/sortOrder=TopRight`、`Feature mode=Individual/…`、`Territory mode=Chunk/…` ✅ |
| 2 | 前后同机位对照 | 默认视域差 **2.26%**、近景差 **2.83%**（差分图＝长直缝）⇒ 修正即命中原病灶 ✅ |
| 3 | 视觉 | 默认视域与近景均**无侧壁带／无长缝**；草地连片 ✅ |
| 4 | 增长风险 | 大范围游历（3 个远点）后**同机位读数不变**（624 = 624）⇒ **不随已加载面积增长**（按视域裁剪） ✅ |
| 5 | 回归 | 0 error；`AI.Core` **零触**；`CanBindTo` **4/4**；`PlaceholderSprites.Get(bld_academy)` 非空（回退链不破）；收尾三态 `isPlaying=False`/`timeScale=1`/`sceneDirty=False` ✅ |
| 6 | 写后验 | 场景 `git status` 空；本批仅 1 文件 ✅ |

---

## 六、代价与治本路径（**请策划端定**）

实测 DrawCall（同局·同时刻切换 A/B，同源 `UnityStats`）：

| 取景 | Chunk（改前） | Individual（改后） | 倍数 |
|---|---|---|---|
| 默认视域 `ortho 8.64` | **80**（≈ H6 基线 80.98） | **652** | **8.2×** |
| 近景 `ortho 5` | 39 | 307 | 7.9× |
| 全图视域 `ortho 60` | — | 4854 | 极端视角 |

**代价根因已实证（E6）**：逐格深度排序 ⇒ 每格是独立排序项 ⇒ **5 张地皮贴图交替** ⇒ 合批被打断。
**治本路径（代价可忽略）**：把 `Ground`/`Feature` 的图打进**一张 SpriteAtlas**（同页可合批）⇒ 实测「地皮统一为单张贴图」时 Individual **58 ≈ Chunk 58**（即代价回落到基线）。
本会话**未实施**（属新增资产面施工项，须策划端签发）。

**备选（更省）**：仅 `Ground=Individual`（近景 129／3.3×），代价是**残留 0.72%**（特征物处小斑，无长缝）。

---

## 七、列报

1. **水位线**：274 → 275。
2. **建议另签发**「地图图集批」（把 `Ground`/`Feature` 入 Atlas）——本批留下的 DrawCall 增量由它收回。
3. **遗留观察**（非本批）：`Mountain`/`SnowMountain` 无真图 ⇒ 占位灰块（`FeatureArtId` 返 `null`），与排序无关。
4. 工作区存在**非本批**改动与若干**乱码名残留目录**（形如 `取号：HH.274…`／`日期：2026-09-14` 等，疑为历史命令误建）——**未纳入本批、未删除**，请策划端处置。
