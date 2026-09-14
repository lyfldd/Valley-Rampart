# HH.271 地图层内排序修正批 任务书（TilemapRenderer Mode：Chunk → Individual）

> 类型：施工任务书｜状态：🟢 **已签发·待接单**
> 日期：2026-09-14 · 签发端：主策划端 · Gate：`G3-1`
> 关联：**用户实机对照（D716）**／`HH.268` 开工回执 §A（`_交接索引.md:297`·已实读记「Ground/Feature 均 **Chunk**」）／`0.6 §二百四十四`（D716）／`HH.269` 交付报告（层序 ④-1′ 与锚点 ① 已收口）
> 取号：HH.271（账本水位线 270→271）
> **由来**＝用户 2026-09-14 实机对照：**手动建的 `New Palette.prefab`（Mode=Individual）不露侧面；游戏 `GameScene.unity`（Mode=Chunk）露** ⇒ 策划端按「链路逐步排除法」把两条链逐环节比对，锁定**唯一差异＝`TilemapRenderer.Mode`**。

---

## 〇、一句话目标

把 `GameScene.unity` 三层 TilemapRenderer 的渲染模式由 **Chunk** 改为 **Individual**，让「图形超出 cell」的地皮 tile 能被逐格按深度正确排序，消除地皮厚度（侧面）在等轴密铺下的错误露出。

---

## 一、作业依据（必读·逐字）

| # | 文档／物件 | 用处 |
|---|---|---|
| 1 | `Valley Rampart/Assets/_Game/Art/Ground/New Palette.prefab`（用户提供） | **对照样本**：其 TilemapRenderer `m_Mode: 1`（Individual）／`m_SortOrder: 3`／`tileAnchor (0.5,0.5,0)`；用户实测**不露侧面** |
| 2 | `Valley Rampart/Assets/Scenes/GameScene.unity` 三层 TilemapRenderer（现实现） | **本批唯一改动面**（`m_Mode: 0` ×3） |
| 3 | `多Agent交接/_交接索引.md:297`（HH.268 开工回执 §A） | **先例证据**：该回执**已实读记录**「Grid `IsometricZAsY (1.28,0.64,1)`；Ground `order 0`／Feature `order 10` 均 **Chunk** ＋ `tileAnchor (0.5,0.5)`」⇒ 本批＝**该已知点的处置**（当时未列为施工项） |
| 4 | `多Agent交接/执行端/HH.269_地图渲染层重构批_交付报告.md` §一／§十一（D714） | 上游基线：三处 `tileAnchor` 已归一 `(0,0,0)`、层序 ④-1′（Ground −1／Territory 0／Feature 1）已修、DrawCall C 段 `84.48→81.88` |
| 5 | `Valley Rampart/Assets/_Game/Systems/Rendering/MapRenderService.cs`（**只读**） | 铺格顺序＝`for y { for x }`（`RenderChunk`）⇒ 即 chunk mesh 内三角形顺序的来源 |

### 1.1 病根定论（策划端 D716·排除法·供执行端复核，勿重推翻）

| 链路步 | 手动那套 | 游戏（程序化） | 判定 |
|---|---|---|---|
| ① PNG → sprite | guid `a9dd31fe…`、fileID `21300000` | 同一文件 | **同一张图** |
| ② 登记映射 | `ground_tropical.asset:15` 引用它 | `SpriteRefTable.asset:258-259`（`artId: ground_tropical`）引用它 | **同一引用** |
| ③ 建 Tile | Tile 资产（sprite ＋ 单位矩阵 ＋ `LockColor`） | `CreateSpriteTile()` ＝ `CreateInstance<Tile>()` ＋ 赋同一 sprite | **语义等价** |
| ④ 落到格位 | 手刷的格 | `SetTile(x, y)` | **同一格语义** |
| **⑤ 谁把它画出来** | **Mode = Individual** | **Mode = Chunk** | **❌ 唯一差异** |

**机制**：Chunk 模式把相邻格 tile 合并成 **1 个 mesh** 提交渲染，**mesh 内三角形的先后顺序在合并时即按 `SetTile` 写入顺序定死**，此后**不存在"按深度重排一次"的机会**。而地皮 sprite 的**有效图形高 96px ＝ 1.5 格**（远超 cell 的 64px），其"厚度（土色侧面）"必须靠**逐格按深度排序**才能被下一格正确盖住 ⇒ Chunk 模式缺的正是这一步。Individual 模式逐 tile 独立提交、按位置排序，故对照样本正常。

---

## 二、范围（一件 · 单变量）

### ① `TilemapRenderer.Mode`：Chunk → Individual（**唯一施工面**）

改动点三处，**均在** `Valley Rampart/Assets/Scenes/GameScene.unity` 的对应 `TilemapRenderer` 组件内：

| 对象（`m_Name`） | 字段 | 改动 |
|---|---|---|
| `Tilemap_Ground` | `m_Mode` | `0` → **`1`** |
| `Tilemap_Feature` | `m_Mode` | `0` → **`1`** |
| `Tilemap_Territory` | `m_Mode` | `0` → **`1`** |

> ⚠️ **定位纪律（必守）**：按「`m_Name: Tilemap_*` → 同 GameObject 的 `TilemapRenderer` 块 → `m_Mode`」逐块定位，**禁按行号盲替**（行号会随他端改动漂移）。策划端 D716 实测三处落于 L413／L1216／L1417，**仅供交叉核对**。
> ⚠️ **施工前必须实测**该场景 `m_Mode: 0` 的**出现次数**（D716 实测＝ **3**）。若 >3 ⇒ 逐个判归属后再动，**多余者列报不擅改**（见 §五 M7）。

### ② 明确不改（**保持原值**，以免引入多变量）

`m_SortOrder`（Chunk 专有参数·改 Mode 后即失效）／`m_ChunkCullingBounds`／`m_DetectChunkCullingBounds`／`m_ChunkSize`／三层 `m_SortingOrder`（Ground `−1`、Territory `0`、Feature `1`）／三层 `tileAnchor`（均已归一 `(0,0,0)`·D714）／Grid `m_CellSize`、`m_CellLayout`、`m_CellGap`。

---

## 三、验收线

| # | 线 | 判据 |
|---|---|---|
| **1** | **Mode 已改** | 场景内 `m_Mode: 1` 计数 ＝ **3**、`m_Mode: 0` 计数 ＝ **0**（给计数实证） |
| **2** | **零夹带** | §二② 所列字段**逐字未变**（给 `git diff` 逐行证据，只应出现 3 行 `-m_Mode: 0` / `+m_Mode: 1`） |
| **3** | **实机对照（核心）** | 正门 `EnterTestRun`（**同 seed 21107／Medium／diff 2／speed 1x**、无夹具、无特制场景）⇒ 同视角 **3 张截图**，与 `screenshots/D714_实机/D714_1|2|3` **同位置对照**：**草地菱形之间不再夹土色带**（逐张目视判读，给"改前/改后"并排结论） |
| **4** | **负例处理** | 若线 3 **未改善** ⇒ **回滚 Mode、如实报告**（说明 Mode 非根因）＋ 附新证据（嫌疑转向「贴图溢出量 vs chunk 边界」／其他排序路径）；**禁为过线找补**（`L-30`） |
| **5** | **DrawCall 代价** | 给**前后对照**（Individual 放弃 chunk 合并 ⇒ draw call 上升属**预期**）；基线＝`HH.269` C 段 `84.48→81.88`。**若上升超 2× 基线 ⇒ 停手列报**，转备选（美术裁溢出／分块策略） |
| **6** | **回归** | 编译 **0 error**；`ExitTestRun` ＋ `QuitSmoke` 三态 ✅；`AI.Core` 零触；`SpriteRefTable` 间接层／缺图回退链**不破**（`CanBindTo` 4/4 保持） |
| **7** | **写后验** | 本批相关路径 `git status` 全空；各笔 hash 已贴；**未 push** |

---

## 四、红线

1. 正门 `EnterTestRun`；收尾真暂停 ＋ `ExitTestRun` ＋ 退 Play（`L-32`）；**不为过线凑数**（`L-30`）。
2. ❌ **只改 `m_Mode` 三个字段**；业务代码（`Assets/**/*.cs`）／SO／贴图／Grid／相机 **一律零改**。
3. ❌ **不动相机**（`m_TransparencySortMode`／`m_TransparencySortAxis`）—— 该点**另立观察项**：`New Palette.prefab` 的 Palette Settings 用 `CustomAxis (0,1,−0.25)`，但那是**编辑器预览**设置；游戏相机是否补设，**本批不判、不改**。
4. ❌ 不碰 `cellSize`／生成参数／`SpriteRefTable` 间接层／不另建 LOD。
5. 写-改-commit 同串 · **具名 `git add`** · 禁 `-A`/`-u`/`.` · **不 push**；**改前重读磁盘**（他端可能并发改场景）；**禁整文件覆盖**。
6. `多Agent交接/_任务队列.md` **一行不写**。
7. 交付报告按账本**实时水位线取号**（**禁预留**·`D640 #10`）。

---

## 五、排雷

| # | 雷 | 处置 |
|---|---|---|
| **M1** | **枚举序风险** | 团结 1.8.5（Unity 2022 基）`TilemapRenderer.Mode`：`Chunk＝0`／`Individual＝1`。**施工前在 Inspector 里核对 UI 文案**（防枚举序差异）⇒ **以 Inspector 显示为准**落 `m_Mode` 值，并在回执里写明核对结果 |
| **M2** | **SortOrder 误改** | `m_SortOrder` **仅 Chunk 模式生效**；改 Mode 后它失去作用。**保持原值不动**——避免"多变量并行"导致归因不清 |
| **M3** | **性能** | Individual 放弃 chunk 合并 ⇒ draw call 上升属预期。**先复述基线、后给实测**；超阈值停手列报（§三-5） |
| **M4** | **他端并发改场景** | `GameScene.unity` 是共享大文件 ⇒ **改前重读磁盘**、增量替换、**只动 3 行**；若发现他端未提交改动 ⇒ **先列报再动**（`L-02` 家族） |
| **M5** | **Territory 层连带** | `Tilemap_Territory` 的 tile 同样可能超出 cell ⇒ **一并改**；若 Territory 视觉出现新问题 ⇒ **列报、不擅自回退** |
| **M6** | **承 `L-29`/`L-30`** | 判定线可达性：本批判据**均为可实测**（字段计数 ＋ 截图 ＋ draw call），**无结构性不可达风险** |
| **M7** | **`m_Mode` 数量** | 施工前实测计数（D716＝3）；**>3 须逐个判归属** |

---

## 六、产出

1. **开工回执（占 HH.271 号 · 不另取号）**：含 §五 排雷 **M1~M7 逐条自答** ＋ 施工前 `m_Mode` 实测计数 ＋ draw call 基线复述 ＋ 定位到的三处 file:line ＋ Inspector 枚举文案核对结果。
2. **交付报告（按水位线另取号）**：§三 七条逐项证据（**字段计数实证／截图对照／draw call 前后／diff 逐行**）＋ `L-34` 五列 ＋ 四族回归。
3. **截图落** `screenshots/D716_TilemapMode/`（`.gitignore:60` 已忽略 ⇒ **不入库**）。

---

> 签发：主策划端｜2026-09-14｜**D716**｜依据＝用户实机对照（`New Palette.prefab` vs `GameScene.unity`）＋ 策划端链路排除法（D716）＋ `HH.268` 开工回执已记录而未处置的「均 Chunk」点。
