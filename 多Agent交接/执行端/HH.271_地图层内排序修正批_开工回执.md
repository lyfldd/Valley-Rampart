# HH.271 地图层内排序修正批 开工回执（Gate=G3-1 · 含施工完成与设施级回滚）

> 类型：开工回执（**占 HH.271 号·不另取号**）｜状态：🟡 **待验收**｜日期：2026-09-14｜发起端：执行端（TraeCode）
> 作业依据：`多Agent交接/策划端/HH.271_地图层内排序修正批_任务书.md`（D716 签发·Gate=`G3-1`）
> 入批 HEAD：`7e9ac9a6`（HH.271 取号＋任务书签发）
> ⚠️ **本回执落盘时施工已全部完成**（含线 3 判定与**线 5 停手回滚**）——M1~M7 自答与施工前实测均为**实测留痕**，非事后补写；完整七线证据见**交付报告 HH.273**。

---

## 〇、一句话结论

**线 1/2/6/7 ✅；线 3「改善」目视成立；线 5 ❌ 代价超 2× 阈值 ⇒ 按线 5 停手 ＋ 线 4 回滚模式，如实报告 Mode 非根因。**

| 线 | 结论 |
|---|---|
| 1 Mode 计数 | ✅ `m_Mode:1`＝**3**／`m_Mode:0`＝**0**（施工后实测） |
| 2 零夹带 | ✅ diff **恰 3 行**（`−m_Mode: 0` / `+m_Mode: 1`·L413/L1216/L1417） |
| 3 实机对照 | ✅ **改善成立**（草地菱形之间不再夹土色带） |
| 4 负例处理 | ✅ **已回滚**（Mode 非根因/不可承受 ⇒ 回 Chunk） |
| 5 DrawCall | ❌ **超 2× 阈值**（全 Individual **639.7** ≈ 基线 **7.8×**；最小档 Ground-only 亦 **256.8** ≈ **3.1×**）⇒ 停手列报 |
| 6 回归 | ✅ 编译 0 error／`AI.Core` 零触／`CanBindTo` **4/4**／回退链不破／三态 ✅ |
| 7 写后验 | ✅ 相关路径 `git status` 全空（场景已回滚至 HEAD 态） |

---

## 一、§五 排雷 M1~M7 逐条自答

| # | 雷 | 自答（实测） |
|---|---|---|
| **M1** | 枚举序风险 | ✅ **运行中反射实测**（非查文档）：`TilemapRenderer.Mode.Chunk＝0`／`Mode.Individual＝1` —— 与任务书一致；落值 `m_Mode: 1` 即 Individual。**Inspector 侧**：`m_Mode` 为序列化枚举，代码 `tr.mode` 读回 `Individual(1)` 双向自洽 |
| **M2** | SortOrder 误改 | ✅ 未动：三层 `m_SortOrder: 0` 逐字保持（diff 仅 3 行·无 `m_SortOrder` 行）；运行时实读 `sortOrder=BottomLeft` 不变 |
| **M3** | 性能 | ✅ 已先复述基线（HH.269 C 段 `84.48→81.88`）后给实测（§三）——**超阈值已停手列报**（未擅自留下改动） |
| **M4** | 他端并发改场景 | ✅ **改前重读磁盘**：`git status -- GameScene.unity` **空** ⇒ 无他端未提交改动；增量替换只动 3 行（未整文件覆盖） |
| **M5** | Territory 连带 | ✅ `Tilemap_Territory` 一并改；未出现新视觉问题（同批同向，见 §三）；已回滚同批还原 |
| **M6** | `L-29`/`L-30` | ✅ 判据均可实测（字段计数＋截图＋draw call），无结构性不可达；**线 5 超阈值时未为过线找补**（如实回滚） |
| **M7** | `m_Mode` 数量 | ✅ 施工前实测：`m_Mode:` 全库 **3** 处、全部 `: 0`（＝3，与 D716 一致）⇒ 无多余项 |

---

## 二、施工前实测与定位（M7 ＋ 定位纪律）

**施工前计数**：`m_Mode: 0` ＝ **3**；`m_Mode:`（全）＝ **3** ⇒ 与 D716 实测一致，无 >3 情形。

**三处落点（按「`m_Name: Tilemap_*` → 同 GameObject 的 `TilemapRenderer` 块 → `m_Mode`」逐块定位·非行号盲替）**：

| # | `m_Name` | `m_Mode` 行号 | 同块关键字段（未改·交叉核对） |
|---|---|---|---|
| 1 | `Tilemap_Territory` | **L413** | `m_ChunkSize:{32,32,32}`／`m_ChunkCullingBounds:{0,0,0}`／`m_SortOrder: 0`／`m_DetectChunkCullingBounds: 0` |
| 2 | `Tilemap_Feature` | **L1216** | `m_ChunkSize:{32,32,32}`／`m_ChunkCullingBounds:{x:0.13999999,…}`／`m_SortOrder: 0` |
| 3 | `Tilemap_Ground` | **L1417** | 同上（culling x=0.13999999） |

> 与任务书 D716 实测 L413／L1216／L1417 **逐位一致**。

**施工前运行时实名读数（编辑器侧反射·L-29 不靠文本 grep）**：

| 对象 | mode | sortingOrder | sortOrder | tileAnchor | cellSize/layout |
|---|---|---|---|---|---|
| `Tilemap_Territory` | **Chunk(0)** | 0 | BottomLeft | (0,0,0) | (1.28,0.64,1)/IsometricZAsY |
| `Tilemap_Feature` | **Chunk(0)** | 1 | BottomLeft | (0,0,0) | 同上 |
| `Tilemap_Ground` | **Chunk(0)** | −1 | BottomLeft | (0,0,0) | 同上 |

Grid：`cellSize (1.28,0.64,1)` / `layout IsometricZAsY` / `gap 0`；相机：`sortMode=CustomAxis` / `axis=(0,1,0)`。

**DrawCall 基线复述**：HH.269 C 段（360 单位四族混编）：`84.48 → 81.88`；本批同局实测全 Chunk ＝ **83.33**（同量级，口径一致）。

---

## 三、施工与判定（摘要·完整证据见 HH.273）

**施工**：三层 `m_Mode 0→1`（经编辑器 API ＋ `SaveScene` 落盘）。施工后计数 `m_Mode:1`＝3／`m_Mode:0`＝0；`git diff` **恰 3 行**。

**线 3 实机对照（核心）**：正门 `EnterTestRun`（seed 21107／Medium／diff 2／1x·无夹具）同视角 3 张 ⇒ 与 `screenshots/D714_实机/` 同位置对照：**草地菱形之间不再夹土色带**（改善目视成立）。

**线 5 DrawCall（同局 A/B 隔离测·关掉"是不是环境差异"的疑）**：

| 档 | G/F/T mode | drawCall avg |
|---|---|---|
| (b) 改前原态 | Chunk / Chunk / Chunk | **83.33** |
| (c) 最小可行面 | **Individual** / Chunk / Chunk | **256.8**（≈ **3.1×** 基线） |
| (d) | Individual / Individual / Chunk | 640.7 |
| (a) 本次施工态 | Individual / Individual / Individual | **639.7**（≈ **7.8×** 基线） |

⇒ **任一层改 Individual 即超 2× 阈值**，**无部分层可行解**；且 (c) 与 (a) 的同机位对照图**字节几乎相同**（2303354 vs 2302852）⇒ Feature/Territory 改 Individual **无视觉增益、纯代价**。

**线 4 处置**：按线 5「停手列报」＋线 4「未改善即回滚」精神 ⇒ **设施级回滚**三层回 `Chunk`（经编辑器 API 复原 ＋ `SaveScene`；**非 git 破坏性命令**），保留全部证据。回滚后实测：`m_Mode:1`＝0／`m_Mode:0`＝3；场景 `git status` **空**（与 HEAD 逐字一致）。

---

## 四、红线遵守与工作区声明

- 正门 `EnterTestRun` ✅；收尾 `ExitTestRun` ＋ `QuitSmoke` ＋ 三态（`isPlaying=False`／`timeScale=1`／`sceneDirty=False`）✅（`L-32`）；**未为过线找补**（`L-30`）。
- ❌ 业务代码／SO／贴图／Grid／相机 **零改** ✅；未动 `cellSize`／生成参数／`SpriteRefTable` 间接层／未另建 LOD ✅。
- 具名 `git add` · 禁 `-A`/`-u`/`.` · **不 push** ✅；`_任务队列.md` **一行未写** ✅；改前重读磁盘 · 增量替换 ✅。
- **工作区携带（非本批·未提交·请知悉）**：`Packages/*.json`、`pixel-forge/**`、`美术资源文件夹/Ground/*.png`、`音乐资源文件夹/`、策划端文档（`3.6`/`3.8`/`3.8.1`/`3.6.1`）、**用户手建**（`New Palette.prefab`／`New Scene.scene`／`ground_tropical.asset`）、既有 `_tmp_mcp*.ps1`／`_mcp_probe3/`／`.oc_cache/`／`Logs/`／`Output/`／`Medieval Kingdom/` —— **本批未纳入提交**。

---

## 五、请裁项（1 项）

**请裁：Mode 路线的下一步取向**（本批已实证「Mode 是有效变量但代价不可承受」）：

1. **选项 A（本端倾向）**：**接受现状 + 另立批**——Mode 保留 Chunk（回滚态），把「地皮侧面露出」并入**已立项的「地皮渲染重构」**（D711 岔路①·与美术裁溢出/分块策略一起评估）。
2. **选项 B**：**分级妥协**——仅 `Tilemap_Ground` 改 Individual（3.1× ⇒ 仍超 2× 阈值，**本端不推荐**；若策划端愿放宽阈值需明示新阈值）。
3. **选项 C**：**先裁代价预算**——由策划端给出「可接受的 draw call 上限」，再回测是否存在满足上限的折中（如仅对超格地皮 tile 单独走 SR 池）。

> 本端**不擅断**；请策划端定取向后再进入下一批（HH.272 排程为「待 HH.271 完成后启动」，本回执＋HH.273 落盘即视为 HH.271 可收口）。

---

> 执行端：TraeCode｜占 HH.271 号｜本回执落盘即待验收
