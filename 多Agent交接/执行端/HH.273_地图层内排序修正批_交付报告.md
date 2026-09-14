# HH.273 地图层内排序修正批 交付报告（Gate=G3-1 · 含线 5 停手与设施级回滚）

> 类型：交付报告｜状态：🟡 **待验收**｜日期：2026-09-14｜发起端：执行端（TraeCode）
> 取号：**HH.273**（账本水位线 **272** 已被策划端 HH.272「地图生成重构批」任务书占用〔D718·`8a053fad`〕⇒ 按 `vr-id-ledger`「先落盘者保留」**顺延 HH.273**·`D640 #10` 禁预留）
> 作业依据：[HH.271 任务书](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/策划端/HH.271_地图层内排序修正批_任务书.md)（D716·Gate=`G3-1`）＋ 开工回执 [HH.271_地图层内排序修正批_开工回执.md](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/执行端/HH.271_地图层内排序修正批_开工回执.md)
> 入批 HEAD：`7e9ac9a6`

---

## 〇、一句话结论

**Mode 是有效变量（改善目视成立），但代价不可承受（≈7.8× 基线），且无部分层折中解 ⇒ 按线 5 停手 · 线 4 回滚 · 如实报告 Mode 单独非根因。**

> 根因收窄：**Chunk→Individual 确实消除了地皮侧面在密铺下的错误露出**（线 3 成立），**故「合并 mesh 后无深度重排机会」这一机制判断成立**；但 Individual 的代价（逐格提交）在本项目地图规模下**结构性超标**——三层任一层改即 >2× 阈值。⇒ **正确解不在「Mode」这一个旋钮上**，需与「美术裁溢出量／分块策略」共同求解（与 D711 岔路①一致）。

---

## 一、施工结果（线 1 / 线 2）

**施工**：`GameScene.unity` 三层 `TilemapRenderer.m_Mode 0→1`（经编辑器 API ＋ `SaveScene`）。

**线 1（Mode 计数）**——施工后实测：

```
m_Mode: 1 = 3
m_Mode: 0 = 0
m_Mode: 总计 = 3
```

**线 2（零夹带）**——`git diff --unified=0` **逐行全文**（恰 3 行·无其他任何行）：

```diff
@@ -413 +413 @@ TilemapRenderer:
-  m_Mode: 0
+  m_Mode: 1
@@ -1216 +1216 @@ TilemapRenderer:
-  m_Mode: 0
+  m_Mode: 1
@@ -1417 +1417 @@ TilemapRenderer:
-  m_Mode: 0
+  m_Mode: 1
```

`git diff --stat` ＝ `1 file changed, 3 insertions(+), 3 deletions(-)`。
**§二② 不改清单逐项复核**：`m_SortOrder`（0×3）／`m_ChunkCullingBounds`（0 与 0.13999999）／`m_DetectChunkCullingBounds`（0×3）／`m_ChunkSize`（32³×3）／三层 `m_SortingOrder`（−1/0/1）／三层 `tileAnchor`（(0,0,0)）／Grid（cellSize/layout/gap）**逐字未变**（diff 中零出现）。

**定位（按纪律·非行号盲替）**：`m_Name: Tilemap_*` → 同 GameObject 的 `TilemapRenderer` 块 → `m_Mode`，落 **L413（Territory）／L1216（Feature）／L1417（Ground）**，与 D716 实测逐位一致。

---

## 二、线 3｜实机对照（**核心·改善成立**）

- 进局：正门 `TestHarnessApi.EnterTestRun`（**seed 21107／Medium／diff 2／1x**·无夹具·无特制场景）；收尾 `ExitTestRun` ＋ `QuitSmoke`。
- 截图：`screenshots/D716_TilemapMode/`（`.gitignore:60` 已忽略 ⇒ 不入库）——3 张主图 + `ab/` 6 张同机位逐层对照图。

| 文件名 | 观察（对照 `screenshots/D714_实机/` 同位置） |
|---|---|
| `D716_1_overview.png` | 默认视角：地皮密铺区**土色夹带明显减少** |
| `D716_2_player_buildings.png` | 主城周围草地：**草地菱形之间不再夹土色带**（D714 同位置可见的棕色侧壁条带消失） |
| `D716_3_units.png` | 单位区：草地菱形间**不夹土色带**；树/矿山仍正确压在地皮之上 |
| `ab/ab_chunk_B_field.png`（改前） | 草地菱形之间**可见土色带**（棕色横向条带成片） |
| `ab/ab_groundonly_B_field.png`（G 层 Individual） | 土色带**消失**，草地连成片 |
| `ab/ab_allindiv_B_field.png`（全 Individual） | 与上一张**字节≈相同**（2303354 vs 2302852）⇒ 视觉无进一步增益 |

**判定：✅ 改善成立**（Mode 确为相关变量）。

---

## 三、线 5｜DrawCall 代价（**❌ 超 2× 阈值 ⇒ 停手**）

**方法**：**同局内** A/B 隔离测（`Logs/hh271_ab.log`）——同一局、同一视域、逐档切换 `TilemapRenderer.mode` 后各稳定 10 帧、采 30 帧取平均。**排除了"换局/换环境造成差异"的解释**。

| 档 | G / F / T mode | drawCall avg | 相对基线（83.33） |
|---|---|---|---|
| (b) **改前原态** | Chunk / Chunk / Chunk | **83.33** | **1.00×** |
| (c) 最小可行面 | **Individual** / Chunk / Chunk | **256.8** | **3.08×** |
| (d) | Individual / Individual / Chunk | 640.7 | 7.69× |
| (a) **本次施工态** | Individual / Individual / Individual | **639.7** | **7.68×** |

- **基线复述（先复述后实测·M3）**：HH.269 C 段（360 单位四族混编）`84.48→81.88`；本批同局全 Chunk ＝ **83.33**（同量级）。
- **判定**：**任一层改 Individual 即超 2× 阈值**（最小档亦 3.08×）⇒ **无部分层可行解**；按线 5「**停手列报**」。
- **附带证据**：(c) 与 (a) 同机位图**字节几乎相同** ⇒ **Feature / Territory 改 Individual 无视觉增益、纯代价**（建议若日后重提，先裁 G 层单层）。

---

## 四、线 4｜负例处理（**已回滚·非"未改善"而是"不可承受"**）

> 线 4 原文触发条件为「若线 3 **未改善**」；本批**线 3 已改善**，但**线 5 超阈值**（线 5 自身即规定「超 2× ⇒ 停手列报」）⇒ 按线 5 停手 ＋ 按「不留下不可承受改动」精神**回滚**。

**回滚方式（非破坏性）**：经编辑器 API 将三层 `mode` 复原为 `Chunk` ＋ `SaveScene`；**未使用** `git checkout/restore/reset` 等破坏性命令。

**回滚后实测**：`Tilemap_Ground/Feature/Territory: Individual(1) -> Chunk(0)`，`reverted=3 SaveScene=True sceneDirty=False`；计数 `m_Mode:1 = 0`／`m_Mode:0 = 3`；场景 `git status` **空**（与 HEAD 逐字一致）。

**结论表述（`L-30` 不找补）**：**Mode 单独不是可落地的根因解**。机制判断「Chunk 合并后无深度重排机会」**已由线 3 证实**，超标原因为 Individual 逐格提交的固有代价；**正确解须与「贴图溢出量（美术裁切）」或「分块/局部策略」共同求解**（候选见 §七 请裁）。

---

## 五、线 6｜回归（**零退化**）

| 项 | 结果 |
|---|---|
| 编译 | **0 error**（console 仅 3 条既有存量：`ruler` missing script／Theme Style Sheet／`233 node options`——**均非本批引入**，与 HH.269 同） |
| `AI.Core` | **零触**（本批仅改场景 3 字段） |
| `SpriteRefTable` 间接层 | ✅ 不破：`CanBindTo` **4/4**（同源读法＝HH.264 D 段：先 `TryGetFrames` 再 `TryGet`）⇒ `unit_human_warrior_idle` / `unit_orc_worker_attack` / `unit_elf_archer_walk` / `unit_monster_raider_idle` 全部命中 |
| 缺图回退链 | ✅ 不破：`PlaceholderSprites.Get("bld_academy")` nonNull=True |
| 四族四轮回归 | ✅ **零退化**（既有基线容器 `Valley_HH239_ArtProbe`，`Logs/hh271_fourrace.log`）：建筑真图率 **2463/2463、2468/2468、2463/2463、2456/2456**（100%）；主城按族按级不串族；地皮真图 4~5 种；特征物 15 种；②FALLBACK 语义与 HH.269 逐项一致 |
| 三态（`L-32`） | ✅ `isPlaying=False`／`timeScale=1`／`sceneDirty=False`（跑批后实测；`hh271_abshot` 收尾亦为 `ExitTestRun`＋`QuitSmoke`） |

---

## 六、`L-34` 在线判据表（**五列**）＋ `L-35` 口径

> 本批**非长局跑批**（跑批段＝**建局即探针**，单轮 <2 分钟·无「跑到底」风险），按 `L-34` 五列给在场判据表（口径同 `test-harness-first §八`）：

| 判据 | 可判定最早日 / 时机 | 命中即停条件 | 服务哪条验收线 | 作用域 |
|---|---|---|---|---|
| Mode 计数（3/0） | 施工后**立即**（文本计数） | 读得即判 | 线 1 | 全批 |
| diff 行数（恰 3） | 施工后**立即** | 读得即判 | 线 2 | 全批 |
| 截图对照（不夹土色带） | 建局后**首帧**（世界就绪 +2s） | 截图落盘即判 | 线 3 | 3 张（同 D714 机位） |
| **drawCall 档位 ×4** | 建局后**首帧**；每档稳定 10 帧→采 30 帧 | **测得即判**（>2× 基线 ⇒ 当场停手） | 线 5 | 全批（同局 A/B） |
| 编译 / `CanBindTo` / 三态 | 跑批前 / 局内 / 收尾 | 立即 | 线 6 | 全批 |
| 四族四轮 | 回滚后（确认无副作用） | 4 轮读数齐即判 | 线 6 | 四族各一轮 |

- **支撑日志异常即时记录**：全部读数**同轮内**落盘（`Logs/hh271_mode.log`／`hh271_ab.log`／`hh271_abshot.log`／`hh271_fourrace.log`·每写即 `Flush`），**不依赖「跑完再看」**。
- **`L-35` 口径来源与排除项**：
  - `drawCall` 口径＝`UnityEditor.UnityStats.drawCalls`（Editor Play 内读数，**与 H6/HH.269 基线同源**）；**排除**跨局/跨视域比较（本批四档**同局同视域**取得）。
  - Mode 读数口径＝**运行中 `tr.mode` 反射实名 + `(int)` 值**（`L-29`：不以文本 grep 判运行时行为）；文本计数仅作落盘一致性交叉核对。
  - 截图判定口径＝**同机位同参数**（机位 A 主城 `ortho 4`／机位 B 草地 `ortho 5`；另 3 张沿用 D714 取景逻辑）；**排除**滤镜/缩放/标注。
  - **禁跨口径混比**：未以「截图像素差」推断 drawCall，亦未以「drawCall 低」推断视觉达标。

---

## 七、请裁项（1 项·同 HH.271 回执）

**请裁：Mode 路线下一步取向** —— 本批已实证「变量有效、代价不可承受、无部分层折中」：

| 选项 | 内容 | 本端评价 |
|---|---|---|
| **A（倾向）** | 接受现状（回滚态 Chunk）＋ 把「地皮侧面露出」并入**已立项的「地皮渲染重构」**（D711 岔路①），与**美术裁溢出量**／分块策略一起评估 | 与 D711 方向一致；治本 |
| **B** | 分级妥协：**仅 `Tilemap_Ground`** 改 Individual | 实测 **3.08× 仍超 2×** —— 若采此路**须策划端明示新阈值** |
| **C** | 先**裁代价预算**（可接受上限），再回测是否有满足上限的折中（如仅对超格 tile 走 SR 池） | 可判性最强，但需新预算口径 |

**列报 2 项（供策划端知悉）**：
1. **`New Palette.prefab` 的相机侧差异另计**：其 Palette Settings 用 `CustomAxis (0,1,−0.25)`，而游戏相机为 `(0,1,0)` —— 本批按红线 3 **不判、不改**（另立观察项）。
2. **本会话 Unity 工具面仍未注册双 MCP** ⇒ 全程走 `unity-mcp-first` **D698 §3 终端 TCP 直连 bridge**（`unity_port=64854`）；本轮新增教训：`exec_editor_script` 内**类型须全限定**（`UnityEngine.Tilemaps.Tilemap`／`System.Collections.IEnumerator`），否则 `CS0246/CS0305`。

---

## 八、红线遵守与工作区声明

- 正门 `EnterTestRun` ✅／收尾 `ExitTestRun`＋`QuitSmoke`＋三态 ✅（`L-32`）／**不为过线找补**（`L-30`）。
- ❌ 只改 `m_Mode` 三个字段：业务代码／SO／贴图／Grid／相机 **一律零改** ✅；不碰 `cellSize`／生成参数／`SpriteRefTable` 间接层／不另建 LOD ✅。
- 写-改-commit 同串 · **具名 `git add`** · 禁 `-A`/`-u`/`.` · **不 push** ✅；`_任务队列.md` **一行未写** ✅；改前重读磁盘 ✅。
- **工作区携带（非本批·未提交）**：`Packages/*.json`、`pixel-forge/**`、`美术资源文件夹/**`、`音乐资源文件夹/`、策划端文档（`3.6`/`3.6.1`/`3.8`/`3.8.1`）、**用户手建**（`New Palette.prefab`／`New Scene.scene`／`ground_tropical.asset`）、既有 `_tmp_mcp*.ps1`／`_mcp_probe3/`／`.oc_cache/`／`.oc_data/`／`Logs/`／`Output/`／`Medieval Kingdom/` —— **未纳入本批提交**。

---

> 执行端：TraeCode｜取号 HH.273（水位线 272→273·顺延自 HH.272 占用）｜本报告落盘即待验收
