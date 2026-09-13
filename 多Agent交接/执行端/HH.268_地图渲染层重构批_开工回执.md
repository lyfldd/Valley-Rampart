# HH.268 地图渲染层重构批 · **开工回执（报裁）**

> 类型：开工回执（**含前置报审点 ⇒ 请裁后施工**）｜状态：⏳ **待裁决**
> 日期：2026-09-13 · 提交端：**执行端（TraeCode）** · Gate：`G3-1`
> 关联：`多Agent交接/策划端/HH.268_地图渲染层重构批_任务书.md`（**权威**）· `HH.267_美术接入收口批_交付报告_GECD段.md` §E1~E6/§列报（**病根与量测来源**）· `改造计划/0.6_审查决策记录.md` §二百四十一（D712）
> 取号：本回执占 **HH.268**（任务书已用号·D640 #10 **禁预留**）⇒ 交付报告取 **HH.269**
> 证据载体：探针 [Valley_HH268_MapProbe.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH268_MapProbe.cs) · 读数 `Valley Rampart/Logs/hh268_map_baseline.log` · 截图 `Valley Rampart/Logs/hh268_baseline_overview.png` · 正门 `TestHarnessApi.EnterTestRun`（seed 21107）

---

## 〇、一句话结论

**①锚点归一的机制已实证**（`tileAnchor (0.5,0.5)→(0,0)` 使 `GetCellCenterWorld − GridToIso` 四点**全部 →0**）；**②新候选 (e)「单 Tilemap ZAsY ＋逐格 z」＝实测不成立**（ZAsY 同层 z 序的**硬前提是 `Mode=Individual`**，实测 draw call **79 → 475（Δ+396）／两层 Individual → 650（Δ+571）**，直接违反验收线 5「DrawCall 不恶化」）；**④ 的"完全达成"与"DrawCall 不恶化"存在硬冲突**（三条路径见 §四）⇒ **②④ 一并报裁**。③⑤ 无依赖，**已在回执后直接施工**。

---

## 一、只读取证（A–G · 全为运行中实读 · **本轮未改任何 `Assets/**` 业务文件**）

### A. 层序／模式（E4 复核 · 复现 D712 实读）

```
Grid 'MapRender'      cellSize=(1.28, 0.64, 1.00)  layout=IsometricZAsY  swizzle=XYZ
Tilemap_Ground        tileAnchor=(0.5,0.5,0)  order=0   mode=Chunk  sortOrder=BottomLeft  layer=Default
Tilemap_Feature       tileAnchor=(0.5,0.5,0)  order=10  mode=Chunk  sortOrder=BottomLeft  layer=Default
Camera 'Main Camera'  ortho=True size=8.64  sortMode=CustomAxis  sortAxis=(0.00, 1.00, 0.00)
```

### B. ① 判据基线（**本批"先量现值"**）

| cell | tileCenter | GridToIso | Δ |
|---|---|---|---|
| (0,0) | (0, 0.32) | (0, 0) | **(0, +0.32)** |
| (1,0) | (0.64, 0.64) | (0.64, 0.32) | **(0, +0.32)** |
| (0,1) | (−0.64, 0.64) | (−0.64, 0.32) | **(0, +0.32)** |
| (3,5) | (−1.28, 2.88) | (−1.28, 2.56) | **(0, +0.32)** |

**maxAbs = 0.32 世界单位**（＝D712「0.40」中的 0.32 分量；另 0.08 分量＝地皮 pivot 0.50 vs 顶面菱形中心 0.540）。

**★ ① 机制实证（编辑器内实测，测毕还原、`sceneDirty=False`）**：

```
tileAnchor=(0.5,0.5) → Δ = [(0,0.32)] ×4
tileAnchor=(0.0,0.0) → Δ = [(0,0.00)] ×4     ← 偏差归零
还原后 tileAnchor=(0.5,0.5)，场景未脏（未落盘）
```

⇒ **① 的落点＝`Tilemap.tileAnchor`（场景对象属性），Δ draw call ＝ 0，且不触 `cellSize`** —— 与任务书 §二① 约束逐条相符。余下 0.08 分量由「地皮 pivot 对齐顶面菱形中心」收口（§四·①）。

### C. M1 现值快照（几何／导入设置）

| 资产 | tex | sprite pivot | 归一 | importer pivot/align | mesh | filter | PPU | mode |
|---|---|---|---|---|---|---|---|---|
| `ground_temperate.png` | 172×202 | (86,101) | **(0.50, 0.50)** | (0.50,0.50) / align=9(Custom) | FullRect | Bilinear | 100 | Single |
| `feat_mine.png` | 254×248 | (127,108) | (0.50, 0.4355) | (0.50,0.44) / align=9 | FullRect | Bilinear | 100 | Single |
| `feat_tree_temperate_1.png` | 70×104 | (35,37) | (0.50, 0.3558) | (0.50,0.36) / align=9 | FullRect | Bilinear | 100 | Single |

**culling bounds 现行有效值**：Ground=**(0.36, 0.52, 0)** ／ Feature=**(0.77, 0.90, 0)**（场景序列化 `m_DetectChunkCullingBounds: 0`＝**Auto** ⇒ 运行时按 tile sprite 自动算，序列化里的 `(0.14,0,0)` 是占位、非生效值）。
**sortOrder 现 = `BottomLeft`**（Ground/Feature 同）。

### D. ③ 判据基线（Mine 占位块·**精确分解**）

```
全图 65536 格：Tree=9593 Mountain=6183 SnowMountain=7233 OreVein=1137 StonePile=752 WoodPile=553
Mine 格=4176  ⇒  2×2 占位块=1044（贪婪读序精确分解）；残余非整块格=0
⇒ 逐格旧法渲 4176 张 vs 合并后应渲 1044 张（**4.0 张/簇**）
```

**运行中交叉实证**：已加载区 `feat_mine` 实名 tile = **390**（与 HH.267 §列报#4 同值）≈ 97~98 簇 × 4 ⇒ ③ 病根（逐格重复贴）**运行中坐实**。

### E/F. DrawCall 基线 ＋ **候选 (e) 代价实测**（运行中切 `TilemapRenderer.mode`，测毕还原 `Chunk/BottomLeft`）

| 配置 | drawCalls | Δ vs Chunk |
|---|---|---|
| Chunk（现状） | **79** | — |
| **Feature → Individual** | **475** | **+396** |
| **Ground ＋ Feature → Individual** | **650** | **+571** |
| Feature `order 10→1` | 82 | **+3** |
| 还原 | 82 | — |

> 同条件 E 段另次采样 = 96（视图/已加载 chunk 不同所致）；**对照须同采样条件**。

### G. 官方口径（唯一依据·逐字）

- **Chunk**：「handles Sprites on a Tilemap in batches and renders them together. They're treated as **a single sort item** when sorted in the 2D Transparent Queue. While this reduces draw calls, **other renderers can't render between any part of the Tilemap**, preventing other rendered Sprites from interweaving with Tilemap Sprites.」
- **Individual**：「sorts and renders the Sprites on a Tilemap **with consideration of other Renderers** in the Scene… allows a character sprite to go **in-between** obstacle sprites… **Using Individual Mode might reduce performance** as there is more overhead when rendering each sprite individually.」
- **ZAsY 排序**：「To sort and render tile sprites on an **Isometric Z as Y** Tilemap… 1. Set the Tilemap Renderer component **Mode to Individual**. 2. Transparency Sort Mode = **Custom Axis**. 3. Transparency Sort Axis Y = 1, **Z = –0.26**.」（本例 cellSize.y=0.5 的取值；按本项目 **cellSize.y=0.64** 换算 ⇒ **Z ≈ −0.33**）

---

## 二、§五 排雷 M1~M6 **逐条自答**

### M1（最要紧）改锚点＝动全局贴合 ⇒ **以"等价效果"为验收 · 先量现值再改**

**（a）现值已量**：见 §一-C（三型资产 tex/pivot/importer/mesh/filter/PPU/mode 逐项 ＋ culling bounds 现行有效值）。

**（b）"被盖掉的原值"已定位（本回执新增取证 · 关键）**：`git show '6c9cbf64^:…/GameScene.unity'` 中存在一个 **PrefabInstance**（源 guid `36f0d6c7255140a428f7071d04c09993`）；`git show --diff-filter=D --name-only 6c9cbf64` 确证该 guid ＝ **`Assets/_Game/Art/Ground/Palette1111.prefab`**，**已被 HH.263 的 `6c9cbf64` 连场景实例一并删除**（commit message 记为"60 条既有残留删除"之一）。该 prefab（＝用户手作的地块 Tile Palette，Grid ＋ Tilemap ＋ TilemapRenderer）逐项实读：

```
Grid            m_CellLayout: 3 (=IsometricZAsY)   m_CellSize: (1.28, 0.64, 1)
Tilemap         m_TileAnchor: (0.5, 0.5, 0)
TilemapRenderer m_Mode: 1 (=Individual)   m_SortOrder: 3 (=TopRight)
                m_ChunkCullingBounds: (0.36, 0.52, 0)   m_DetectChunkCullingBounds: 0 (Auto)
```

⇒ **结论三笔**：
1. **用户手调的是"渲染器配方"**（`Mode=Individual` ＋ `SortOrder=TopRight`），**不是 `tileAnchor`**（palette 内 `m_TileAnchor` 与现状逐位相同）⇒ ① 改 `tileAnchor` **不覆盖任何用户手调项**。
2. **culling bounds 无需恢复**：手调值 `(0.36, 0.52, 0)` 与**现行 Auto 读数 `Ground=(0.36,0.52,0)` 逐位相同** ⇒ 该手调项**已被等价复现**。
3. **手调配方里的 `Mode=Individual` 与 ② 的 (e) 候选同向**，但**本项目地图规模下实测 +396 draw call**（§一-F）⇒ 二者冲突（见 §四·报裁）。

**（c）导入侧"被覆盖前的值"在本仓 git 中不可得**：`ground_temperate.png.meta` 首次出现即 HH.263 `c7854336`（无更早版本）⇒ 只能以"**等价效果**"验收（与任务书 §三-1 口径一致）。

**（d）本批处置**：① **只改证明必需的两项**（`tileAnchor`、地皮 `pivot` 规则）；② **不动** `cellSize`、**不动** culling bounds 模式、**不擅自套用** `Individual/TopRight`（除非 ② 裁决要求）；③ 改后以**双证**收口＝① 判据（偏差 →0）＋ **截图对照**（地块贴合/无白缝）；④ **不做第二次"统一导入设置"式批量覆盖**——地皮 pivot 仅按 `isGround` 分支重算，其余类型不重导。

### M2 ZAsY 单层可行性未知 ⇒ 先评估 · 回退"少量分层"并报裁

**评估结论＝不成立（实测否决）**，见 §四·②（含官方口径 ＋ 实测 +396/+571 ＋ z 抬升/方向两道结构耦合）。**回退预案**已列入 §四·④（"少量分层"三条路径 ＋ 代价）。

### M3 多格特征合并与深度序冲突 ⇒ 合并后整体须参与深度序

**处置**：③ 合并后**每簇只在占位块中心渲 1 张**，该 tile 仍落在 **Feature 层同一 Tilemap／同一 order**（现状 order 10；若 ④ 裁决改变层序，随之继承）⇒ **合并不改变参与深度序的载体与 order**，仅把"4 个格位各 1 张"改为"1 个格位 1 张（带 0.5,0.5 格位偏移）"。**合并后块的深度键取占位块中心**（＝原 4 格的中点），不引入新的深度失真。⇒ 风险已闭，实施后再以 ③ 运行中计数 + 截图复核。

### M4 别把"矿山太多"当渲染问题就完事 ⇒ 复看 · 若仍偏多 ⇒ 另立调参批（本批不动）

**处置**：③ 修完后**必做复看**：在同一 seed（21107）下对比修前/修后截图与**同屏可见 `feat_mine` 覆盖面积/簇数**。
- **判定分界**：修后"矿山叠起来"（同簇 4 张重叠）消失 ⇒ 属渲染面，本批收口；若**簇数本身**仍显偏多 ⇒ **属生成参数面**（`ResourceGenConfig.baseCounts` / `densityByDifficulty`，见 D712 §四⑤）⇒ **本批不动、另立调参批**。
- **本批红线遵守**：全程零改动 `MapGenRules` / `ResourceGenConfig` / `MapGenRulesConfig`。

### M5 图集已建（H6）⇒ 改渲染不得使 `UnitsFrames.spriteatlas` 失效（`CanBindTo` 4/4 须保持）

**处置**：本批 **不新建、不重打包任何图集**；①③ 只动 `tileAnchor` / 地皮 pivot / 特征铺格逻辑，**不触碰 `SpriteRefTable` 引用对象（仍直接引用源 sprite）** ⇒ `CanBindTo` 4/4 结构上不受影响。收尾**加跑一次 4 键 `CanBindTo` 复证**（含 monster）留证。

### M6 承 `L-29` / `L-30`

- **`L-29`（禁文本 grep 判无引用）**：本批所有"无引用/无消费方"判定一律走**编译期符号／运行中实名读数**，不用文本 grep 判无引用（如 ④ 改 `order` 后单位可见性以**运行中排序键/截图**取证，不以"谁引用了 order 10"判）。
- **`L-30`（gap 表两列）**：见 §三（按 **D704 补维度③** 口径＝〔**前置条件在场性｜签发前必闭**〕vs〔**施工后可达性｜可留施工后**〕**两列分列**）。

---

## 三、`L-30` gap 表（**两列** · 口径＝D704 补维度③）

| 环 | **① 前置条件在场性**（签发前必闭） | **② 施工后可达性**（可留施工后） |
|---|---|---|
| **① 锚点归一（tileAnchor）** | ✅ **场内闭**：`Tilemap.tileAnchor` 为可写属性、两 Tilemap 均在场景 ⇒ 编辑器实测 `(0,0)` ⇒ Δ **四点全 0**（§一-B） | 🔜 施工后实证：改场景属性后运行中 Δ →0 ＋ 截图（地皮/特征/实体对齐） |
| **① 锚点归一（地皮 pivot 顶面中心）** | ✅ **场内闭**：`ArtImportPipeline.ComputeSinglePivot` `isGround` 分支现成可改；`ground_temperate` 实测 顶面菱形中心 = 最上行(141) − cellH/2(32px) = **109px = 0.540**（与 E2 实测 109 逐位一致）⇒ **规则可确定性求得** | 🔜 施工后实证：重算后 `pivot.y=0.5396`，运行中 Δ →0（≤1px）＋ 贴合截图 |
| **② ZAsY 单层（e）** | ❌ **场内已判负**：官方明示 **`Mode=Individual` 为硬前提**；实测 Individual ⇒ **+396/+571 draw call**（§一-F）⇒ **前置条件不可达**（违反验收线 5）⇒ **未开跑即止**（守 `L-30` 补维度③正例） | ✗ 不适用（不施工） |
| **④ 层序修正（全达成路径）** | ⚠️ **部分不可闭**：Chunk 模式"整层为单一排序项"（官方逐字）＋实测 Individual 代价 ⇒ **"特征↔单位逐格交错"的前置条件在本项目规模下不成立**；仅 `order 10→1`（Δ+3）可达 ⇒ **须裁**（§四·④） | 🔜 施工后实证：按裁决路径取 单位/建筑可见性 的运行中排序键＋正/负截图 |
| **④ 层序修正（分层路径 ④-2/④-3）** | ⚠️ **量化未闭**：④-2 需 N 个带 Tilemap ＋ 单位/建筑 order 随深度带动态赋值（改造面含 `UnitController`/`Building` 排序写）；④-3 需 SR 池（对象数 +已加载区特征数 ≈2.3k）⇒ **两者对 DrawCall/CPU 的净影响须先算后开** | 🔜 若裁 ④-2/④-3：先出"改造面＋代价"细案再施工 |
| **③ 多格特征合并** | ✅ **场内闭**：`MineClusterSide=2` ＋ `ScatterMineClusters` 整簇盖章、簇间不重叠 ⇒ 精确分解得 **1044 块／残余非整块 0**（§一-D）；合并"1 块 1 张"数学闭合 | 🔜 施工后实证：运行中每块恰 1 张 `feat_mine` ＋ 计数＝1/块 ＋ 截图对照 |
| **⑤ NPC 名字字号** | ✅ **场内闭**：`IClickInteractable.cs:111-112` 两参数可改；TextMesh `characterSize/fontSize` 生效链在 | 🔜 施工后实证：截图对照（可读且不遮图） |
| **⑤ 回归（四族四轮／DrawCall）** | ✅ **场内闭**：同源容器 `Valley_HH239_ArtProbe` ＋ `UnityStats.drawCalls` 均在；基线已在 §一-E/F | 🔜 施工后实证：四族四轮不退化 ＋ DrawCall 前后对照 |
| **零改动面**（两 SO／`AI.Core`／LOD／间接层／回退链／图集） | ✅ **场内闭**：本批不新增字段、不触 `AI.Core`、不建 LOD、不改表与图集 | 🔜 施工后实证：`git diff --stat` 逐文件核对 ＋ `CanBindTo` 4/4 |

---

## 四、§二 ①② **方案对比** ⇒ **报裁**

### ② 候选 (e)「单 `Tilemap`（ZAsY）＋逐格 z」—— **评估结论：不成立（实测否决）**

**可行性核验（三步）**：

1. **ZAsY 同层 z 分层机制 ＝ 成立**（实测）：`Grid.CellToWorld` 为 IsometricZAsY，`(0,0,0)→(0,0,0)`、`(0,0,1)→(0,0.32,1)`、`(0,0,2)→(0,0.64,2)` ⇒ 同一 `(x,y)` 可用不同 `z` 共存，**世界 +Δy = z × cellSize.y/2 = z × 0.32**。
2. **但"逐格 z 排序"的硬前提 ＝ `Mode=Individual`（官方明示）**，而 Individual 实测代价（同条件对照）：

   | 配置 | drawCalls | Δ |
   |---|---|---|
   | Chunk（现状） | 79 | — |
   | Feature = Individual | **475** | **+396** |
   | Ground ＋ Feature = Individual | **650** | **+571** |

   ⇒ **超 H6 基线（80.98）5.9~8.0 倍**，**直接违反验收线 5「DrawCall 不恶化」** ⇒ 不采。
3. **结构耦合两道**（即便忽略代价也不成立）：(i) **z 抬升**：z=1 ⇒ +0.32 世界，与 ① 的"锚点归零"**直接冲突**，须额外 per-tile transform 补偿；(ii) **z 序方向**：官方 ZAsY 偏置是"**z 大者先画（偏后）**"，而本项目要的是"特征（z 大）**后画（在前）**" ⇒ 符号相反，须改负 z 或反偏置，进一步放大 (i)。

### 与 E6 既有候选的并列比较（**逐项量化**）

| 候选 | draw call（实测/估） | 对象数 | `MapRenderService` 改造面 | 对 H5 预算影响 | 与既有回退链兼容 | 能否解 ④ |
|---|---|---|---|---|---|---|
| **(a) 行分层多 Tilemap** | 可见 ≈144 行 ⇒ **≈+144**（同图不跨行合批） | +168 Tilemap | 大（铺格/chunk/清理/层级全改） | 负向（144 Renderer 剔除＋提交） | 需重写并保 `ground_*`/占位回退 | ✅（行粒度） |
| **(b) 逐格 SpriteRenderer ＋等轴深度排序** | 图集下 **≈+2~5**（但需新图集） | **视域地皮 ≈5184 ＋特征 ~2.3k** | **最大**（弃 Tilemap 层 ＋对象池＋深度刷新） | **显著负向**（CPU 提交 0.3~0.6ms 量级·E6 估） | 需重建回退路径 | ✅（完全） |
| **(c) 分块烘焙** | 可见 chunk 9 ⇒ **≈+8~+27** | +49 chunk | 中（烘焙管线＋失效重烘，与 `UpdateCell` 增量语义冲突） | 内存↑、重烘卡顿 | 需把回退链前置到烘焙期 | ⚠️（chunk 粒度） |
| **(d) 锚点归一**（D712 第一优先） | **Δ ≈ 0**（实测 `tileAnchor` 改动 **不产生 draw call 增量**） | **Δ 0** | **极小**（场景属性 1 处 ＋ `ComputeSinglePivot` `isGround` 分支 1 处） | **零增量** | **天然保留** | ❌（不涉层序） |
| **(e) 单 Tilemap（ZAsY）＋逐格 z** | **实测 +396/+571** | **Δ −1 Tilemap** | 大（z 分层＋补偿＋单层重构） | 显著负向 | 可保回退链 | ❌（Chunk 下仍整层单排序项；须 Individual ⇒ 即上表代价） |
| **(e′) 保留双 Tilemap ＋ Feature `order 10→1`**（＝④-1） | **实测 +3** | Δ 0 | **1 行** | 零 | 天然保留 | ⚠️ **仅部分**（见下） |

> **注**：E6 原表 (a)(b)(c) 为**估算**；本表 (d)(e)(e′) 为**本轮实测**，(a)(b)(c) 沿用 E6 估算（其量级与本轮实测的 Individual 代价互证：**逐格独立排序在本项目规模下均不可承受**）。

### **⇒ 报裁点 1**：② 裁决

- **推荐裁**：**(e) 不采；② 归回 (d) 锚点归一**（Δ≈0、可逆、零 DrawCall 增量），并以 **(e′)** 承担"特征↔单位"的层序诉求（见 ④）。
- **备选裁**：若坚持"必须用上 ZAsY"，则须同时接受 **DrawCall 由 79 → 475+** ⇒ 与验收线 5 冲突，**建议否决**。
- **请裁**：**是否采纳「(e) 否决 ＋ ②归 (d)＋(e′)」**？（若否，请指定候选与可接受的 DrawCall 上限。）

### **⇒ 报裁点 2**：④ 层序 —— **"完全达成"与"DrawCall 不恶化"硬冲突**

**根因（官方逐字）**：Chunk 模式下整张 Tilemap 是"**a single sort item**"，**其他 Renderer 无法插入其间** ⇒ 只要 Feature 层保持 Chunk，"特征与单位/建筑**逐格**决定前后"**在本项目规模下无零成本解**。

| 路径 | 机制 | 达成度 | Δ draw call | 改造面 |
|---|---|---|---|---|
| **④-1（推荐·本批）** Feature `order 10 → 1`（与单位/建筑同序；两 Tilemap 均为 Chunk ⇒ 该整层作为单一排序项落在其 transform 原点(0,0＝深度 0)，单位/建筑深度均 >0 ⇒ 单位/建筑恒在特征**之前**） | 1 行 | **正向达成 ✅**（单位/建筑站在特征前方**可见**）；**负例不达 ❌**（位在特征**后方**的单位**不会**被遮挡） | **+3**（实测） | 极小 |
| **④-2** 特征层按**等轴深度分 N 带多 Tilemap**（"少量分层"）＋ 单位/建筑 `sortingOrder` 随深度带动态赋值 | 中 | **全达成 ✅**（误差 ≤1 带高） | **≈ +N×(1~3)**（N=4~8 ⇒ **+4~24**；若按行细分则 ≈+144） | 中（`MapRenderService` 按 y 分派 ＋ `UnitController`/`Building` 排序写） |
| **④-3** 可遮挡特征改 **SpriteRenderer 池 ＋ Y-sort**（E6 (b) 轻量版） | 大 | **全达成 ✅** | ≈ +2~5（需配套图集） | **大**（新池＋深度刷新＋回退链重建） |

> **⚠️ 冲突陈述**：验收线 2 要求"单位/建筑站在特征前方**可见**（**负例＝被特征遮挡应消失**）"＝要求**双向交错**；而验收线 5 要求"**DrawCall 不恶化**"。**逐格交错 ↔ 零 DrawCall 增量二者不可兼得**（Chunk 单排序项为硬约束，已由官方口径 ＋ 实测双证）。
> - **若优先 DrawCall** ⇒ 裁 **④-1**（+3），并**接受"负例不达"**（建议同步把验收线 2 的负例口径改为"**降级为可接受**"或改为 ④-2 的下批目标）。
> - **若优先正确性** ⇒ 裁 **④-2**（+4~24）或 **④-3**，并**同步修订验收线 5 的 DrawCall 口径**（明示允许的增量上限）。
> - **请裁**：**④ 取哪条路径？以及 DrawCall 口径是否随之修订？**

### ① 锚点归一（**推荐直接按 (d) 施工 · 但归属 ①② 报裁点**）

- **落地＝两处**：① `Tilemap_Ground`/`Tilemap_Feature.tileAnchor`：`(0.5,0.5) → (0,0)`（**场景属性**，Δ draw call ＝0，实测 Δ→0）；② `ArtImportPipeline.ComputeSinglePivot` 的 `isGround` 分支：由"最宽行"改为 **`pivotY = (最上行不透明像素 − cellH/2 px) / H`**（本项目 cellH/2 = 32px @PPU100；`ground_temperate` ⇒ **109/202 = 0.5396**，与 E2 实测顶面菱形中心 109px 逐位吻合）。
- **验收**：Δ →0（≤1px 量级）＋ 对齐截图 ＋ **地块贴合（无白缝）**截图 ＋ DrawCall 前后对照。
- **M1 纪律**：仅重导 `Assets/_Game/Art/Ground/**`（其余类型不重导，避免第二次批量覆盖）。

---

## 五、施工顺序（**已在回执后直接开工**）

| 项 | 状态 | 说明 |
|---|---|---|
| **③ 多格特征合并** | ✅ **已完成**（见 §六 · 运行中计数 95/95 恰 1 张） | 与 ①/② 无耦合；M3 处已声明协同口径 |
| **⑤ NPC 名字字号** | ✅ **已完成**（见 §六 · 0.12→0.04 · 实测定档） | 完全独立；量级以截图实测定 |
| **① 锚点归一** | ⏸ 待裁（本回执 §四 报裁点 1） | 机制已实证，等"采纳 (d)"的确认 |
| **② ZAsY 单层** | ⏸ 待裁（§四 报裁点 1） | 推荐否决 |
| **④ 层序修正** | ⏸ 待裁（§四 报裁点 2） | 三路径互斥，须先定 |

> 若策划端认"① 已在 D712 定为第一优先、机制无争议"，**可仅驳回执"①按 (d) 施工"即可**（无需重开方案讨论）。

---

## 六、施工进度（回执后已做 · commit `d0175e88`）

> 证据载体：`Valley Rampart/Logs/hh268_map_v3.log`（**PASS=8 / FAIL=1**，唯一 FAIL＝①「待裁」⇒ 符合预期）＋ 近景截图 `hh268_v3_closeup_mine.png` / `hh268_v3_closeup_speech.png` ＋ 总览 `hh268_v3_overview.png`。编译 **0 error**（仅存量警告）。

### ③ 多格特征合并渲染 —— ✅ **成立**

| 项 | 证据 |
|---|---|
| 落点 | [MapRenderService.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Rendering/MapRenderService.cs) `SetCell` ＋ `IsBlockAnchor`/`CoveredByBlockAnchor`/`MergedFeatureTile` ＋ 嵌套 `OffsetTile : TileBase` |
| 机制 | ①**块锚判定**＝读序（y升x升）贪婪 2×2 分解的**闭式**（含覆盖回溯＋记忆表）⇒ 与"逐格旧法"的唯一差异＝同块只在锚格渲一张；②**非锚格显式 `SetTile(null)`**（禁重复贴）；③**块位偏移**＝`(side−1)/2 − tileAnchor`（**⚠️ 已自查修正**：`tileAnchor` 本身已含半格，故当前 tileAnchor=(0.5,0.5) ⇒ 偏移＝**0**；若 ① 归一到 (0,0) ⇒ 自动变 (0.5,0.5)，无需二次改码）；④缺真图 ⇒ 落回逐格占位（回退链不破） |
| **运行中计数（主证据）** | 四格全加载的完整 2×2 占位块 **95 个 ⇒ 恰 1 张=95 ／多张=0 ／零张=0**（逐格旧法应为 380 张） |
| 缩放比 | 已加载区 `feat_mine` 实名 tile **390 → 97**（≈4.0×↓；97 与 95 之差＝2 个跨加载边界块，已注明） |
| **截图对照** | `hh268_v3_closeup_mine.png`：红菱＝按世界坐标独立算出的**占位块中心**标记，**与 merge sprite 锚点重合**（mine 底边落在 2×2 占地前角、锚点落在块心）⇒ 只渲一张且落位正确 |
| 设计面零改 | `MapGenRules` / `ResourceGenConfig` / `MapGenRulesConfig` **零改动**（M4 红线遵守） |

> **M4 复看（③ 后）**：同 seed 下"矿山叠起来"（同簇 4 张重叠）**已消失**（4 张 → 1 张）；"矿山太多"是否仍偏多＝**待与用户截图/实机复看**——若仍偏多即属生成参数面，**本批不动**、另立调参批。

### ⑤ NPC 头顶语句字号 —— ✅ **已改 · 但用户"缩小十倍"实测超调（请留意）**

| characterSize | 实测世界高（`MeshRenderer.bounds`） | ≈像素（1080p / 本机 GameView 437px 高） |
|---|---|---|
| 0.12（**原值**） | ≈0.644 | ≈40px / ≈16px |
| 0.012（＝用户提的"缩小十倍"） | **0.0644** | ≈4px ⇒ **不可读** ❌ |
| **0.04（本批取值）** | **0.2145** | **≈13px ⇒ 可读** ✅（≈1/3 格高＝不遮图） |

- **只缩 `characterSize`**（世界尺寸），**保留 `fontSize=48`**（不降字形分辨率 ⇒ 缩小后仍锐利）。
- 截图：`hh268_v3_closeup_speech.png`（近景 ortho=2 下"村民长名A1"清晰可读、不压地块）。
- ⚠️ **口径提示**：用户原话"起码得缩小十倍"，但**实测量化后 10× 会掉到 ≈4px（不可读）** ⇒ 本批取 **÷3（0.04）**；若用户仍觉偏大，下一档＝0.03（0.161）/0.024（0.129），**一档一截图**。

### 待办（待裁后）
- **① 锚点归一**（机制已实证）：裁后可立即落地（`tileAnchor→(0,0)` ＋ `ComputeSinglePivot` 地皮分支改"最上行 − cellH/2"）⇒ 改后 `MergedFeatureTile` 的偏移**自动**变为 (0.5,0.5)（键含 anchor，无需清缓存）。
- **② 候选 (e)**：建议否决（实测 +396 draw call）。
- **④ 层序**：三路径待裁。
- **收尾回归**：四族四轮冒烟 ＋ DrawCall 前后 ＋ `CanBindTo` 4/4 复证 ＋ `ExitTestRun`/`QuitSmoke`/退 Play（将在交付报告 HH.269 一并给）。


---

## 七、风险与回退

1. **① 回退**：`tileAnchor` 为场景属性 ⇒ 一行还原；地皮 pivot 重导可由 `git checkout -- Assets/_Game/Art/Ground` 还原（先留 `git diff` 证据）。
2. **③ 回退**：铺格分支加"多格占位特征"判定 + 单张 tile ＋ 0.5/0.5 格位偏移；关闭该分支即回逐格旧法（同一函数内 switch）。
3. **拟合风险（自曝）**：`feat_mine` 图 footprint ＝2×2、pivot ＝(0.50,0.4355)（**非**地皮口径）⇒ ③ 合并后须**实证**"占位块中心 ↔ 图中心"对齐（若偏移，用 per-tile transform ＋ 实测像素纠正，**不改图、不改导入设置**）。
4. **零改动面自检**（红线）：`AmmoDef`/`GroundEffectDef` 零字段、`AI.Core` 零触、不建 LOD、`SpriteRefTable` 间接层＋回退链不破、`UnitsFrames.spriteatlas` 不动（收尾复证 `CanBindTo` 4/4）。

---

## 八、请求裁决（**两项**）

| # | 决策点 | 选项 | 执行端推荐 | 影响 |
|---|---|---|---|---|
| **1** | **② 候选 (e) 与 ① 定案** | A：(e) 否决 ＋ ① 按 (d) 施工（`tileAnchor→(0,0)` ＋ 地皮 pivot→顶面菱形中心）<br>B：坚持 ZAsY 单层（须接受 DrawCall 79→475+）<br>C：其他 | **A** | 决定 ① 能否立刻施工；决定验收线 5 是否被破 |
| **2** | **④ 层序路径** | A：**④-1**（`order 10→1`，Δ+3）＋ 验收线 2 负例降级<br>B：**④-2** 深度分带（Δ+4~24）＋ 验收线 5 口径修订<br>C：**④-3** SR 池（对象数 +2.3k）<br>D：其他 | **A**（本批零 DrawCall 增量；B/C 另立批） | 决定 ④ 的达成度与 DrawCall 口径 |

> **③⑤ 不待裁**，本回执之后立即施工并另出证据；①②④ 待裁示后施工。

---

## 九、策划端裁决区（**D713**，2026-09-13，主策划端）

> 判据三直读已过（实读非采信转述）：①设计稿全文＝本任务书＋`2_10_渲染与摄像机.md` §5.10（D443 染色「Ground 之上、**Feature/实体之下**」＋验收探针 8）＋D712 全文；②代码/场景落点实读＝`GameScene.unity` L407 `Tilemap_Territory:5`／L1210 `Tilemap_Feature:10`／L1411 `Tilemap_Ground:0`／L345·L1238·L1439 三处 `m_TileAnchor:(0.5,0.5,0)`；`UnitController.cs:315`＝1／`Building.cs:518,527`＝1／`MapRenderService.cs:338` 偏移式；③字段直读＝`IClickInteractable.cs:117`、`GameScene.unity:3668 m_CellLayout:3`。

### 裁点 1 —— ② 候选 (e) ＝ **❌ 否决**（维持执行端推荐 A）· **①(d) 放行施工**

- **(e) 否决**：官方逐字（Chunk＝「a single sort item」；逐格 z 序硬前提＝`Mode=Individual`）＋实测 `79→475（+396）／两层 650（+571）`＝**超 H6 基线 80.98 的 5.9~8.0 倍** ⇒ 违反验收线 5 系**结构性、非取舍**。
- **破框核查（beyond-options）**：执行端 A/B 两选项共享前提「必须用上 ZAsY 同层排序」。该前提**在本项目结构下无适用面**——ZAsY 同层 z 的价值＝"同层内**异类**元素按 z 交错"，而本项目地皮/特征是**两个语义层**（本就该分层，非同层异类）⇒ **前提不成立**，故选 A 不是"退而求其次"，是**问题消解**。②**销项**（留痕·不实施），其职能（对齐＋深度）归 ①(d)。
- **①(d) 放行 ✅**，但**落地面须扩**（见下）。

#### ⚠️ 裁点 1 附带（策划端实读新发现·**回执与任务书双漏**）：① 落地面＝**3 处** `tileAnchor`，非 2 处

三 Tilemap（`Tilemap_Ground`／`Tilemap_Feature`／`Tilemap_Territory`）**同属 `MapRender` Grid**，三处 `m_TileAnchor` 均 **(0.5,0.5,0)**（L1439／L1238／L345）。**只归一 Ground/Feature ⇒ `Tilemap_Territory` 的染色白菱形（运行时生成·`pivot(0.5,0.5)`）将与地皮顶面错位半格（0.32 世界单位）** ⇒ ① 须**3 处同批归一**（或对 Territory 给出显式"保持＋补偿"理由并出对齐截图实证）。

### 裁点 2 —— ④ 层序 ＝ **不采原文 ④-1**（`Feature 10→1`）· **改裁 ④-1′**（连带修正 Territory 层）

**策划端实读抓到结构性连带风险（回执与任务书均未识别）**：

```
现状排序链（场景实读）：MapVisualizer −100 ｜ Ground 0 ｜ 单位/建筑 1 ｜ Territory 5 ｜ Feature 10
```

1. **④-1 破 D443 已落不变量**：`Feature→1` 后 `Feature(1) < Territory(5)` ⇒ **染色块压到特征物（树/山/矿）之上** ⇒ 直接违反 2_10 §5.10「染色不盖特征物/建筑/单位」＋验收探针 8。
2. **现状 `Territory 5` 本就在单位/建筑(1) 之上**（与 `TerritoryOverlay.cs:11` 设计文本相悖）＝**既有漂移**，仅因近景档整层隐藏（D451）未暴露 ⇒ ④ 须一并收口。

**⇒ 裁 ④-1′（全为场景序列化值·零代码面）**：`Feature 10→1` ＋ **`Territory 5→0`** ＋ **`Ground 0→−1`**

| 新链 | 保证 |
|---|---|
| `Ground −1 ｜ Territory 0 ｜ 单位/建筑 1 ｜ Feature 1`（同序靠深度） | Territory(0)<Feature(1) ⇒ **染色不盖特征物 ✅（恢复 D443）**／Territory(0)<单位(1) ⇒ **染色不盖单位 ✅（顺带修既有漂移）**／Feature(1) 与单位(1) 同序＋Chunk 单排序项落深度 0 ⇒ **单位恒在特征前 ✅**／Ground(−1)<Territory(0) ⇒ **染色仍在地皮之上 ✅** |

- **备选 ④-1″**（若判"改场景层级值"越界）＝改代码 `UnitController.cs:315`／`Building.cs:518,527` 单位/建筑 `1→6` ＋ `Feature 10→6`（不触 Ground）。
- **落地后双证**＝①层序负探针（**染色不盖特征物/单位**·D443 探针 8）②单位在特征**前**可见（正向）。

**DrawCall 口径 ＝ 不修订**（维持验收线 5「不恶化·H6 80.98」）：④-1′ Δ≈+3 属噪声；②(e)/④-2/④-3 的超额增量＝**用正确性换代价**，另立项独立评估，**不在本批松绑验收线**。

**验收线 2 负例**（单位在特征**后方**应被遮挡）：④-1′ 结构上**不达**（Chunk 单排序项·零 DrawCall 增量下无解）⇒ 裁＝「**本批有条件下不达**」，并**作为 ④-2/④-3 另立项的立项依据**（**禁**改判为"完全放弃验收线 2"）。

### ③⑤ 追认（不待裁·已施工）

- **③ 多格特征合并 ✅ 成立**：完整 2×2 占位块 95 个 ⇒ 恰 1 张=95／多张=0／零张=0（逐格旧法应 380 张）；`feat_mine` tile **390→97**；用**运行中计数**而非截图充数 ✅。**附条件**＝偏移式 `(side−1)/2 − anchor` 随 ① 归一自动 `0→0.5` ⇒ **① 落地后须复跑 ③ 计数实证**。
- **⑤ 字号 ✅ 追认 ÷3（0.12→0.04）**：用户"缩小十倍"经实测量化 ÷10＝0.064≈4px **不可读** ⇒ 执行端改判 ÷3（0.644→0.2145≈13px·≈1/3 格高）＝**正确处置（准·非越权）**；留一步＝用户若仍觉大 ⇒ 下一档 0.03/0.024 **一档一截图**（最终定档待用户目视）。

### 嘉奖

①**官方口径逐字＋实测代价双证**（把"能不能用 ZAsY"从主张变可判）②**主动自查纠错一笔**（`tileAnchor` 半格 ⇒ 偏移式修正）③**M1 三步取证扎实**（`Palette1111.prefab` 定位用户手调配方·确认 ① 改 anchor 不覆盖手调项·culling 手调值已被 Auto 等价复现）④**③ 运行中计数** ⑤**守 `L-32` 三态**（`isPlaying=False`／`timeScale=1`／`sceneDirty=False`）＋零业务代码外溢＋只提 3 文件＋不 push＋队列不写。

### 教训

**`L-15` 家族＋1**：**改「排序链/层级/顺序」类参数，必须对该链上全部层逐层做连带扫描**——含**未列入改动清单的层**＋该层挂载的**既有不变量**（本次＝④-1 未扫 Territory 层 ⇒ 险些破 D443；① 未扫第 3 个 Tilemap ⇒ 险些破染色对齐）。防范动作＝签发/裁决此类改动前**先画全排序链图并逐层标注"不变量"**。

### 下游

执行端落地 **①（3 处 anchor）＋④-1′** ⇒ 出 **HH.269** 交付报告（五项逐项证据＋四族四轮回归＋DrawCall 前后＋`CanBindTo` 4/4＋`L-34` 五列＋`L-35` 口径，**含 ③ 复证与层序双证**）；**④-2/④-3 另立项**（待 HH.269 后按"正确性优先"评估）。

---

> 提交端：执行端（TraeCode）｜2026-09-13｜HH.268 开工回执｜依据＝HH.268 任务书 §五/§六＋D712＋HH.267 §E/§列报＋本轮只读取证（`hh268_map_baseline.log`）
> 裁决端：主策划端｜2026-09-13｜**D713**｜§九 裁决区
