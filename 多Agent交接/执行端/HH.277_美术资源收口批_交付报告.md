# HH.277 美术资源收口批 交付报告（SpriteAtlas 合批 ＋ 补山地图 ＋ 单位缺态清单）

> 类型：交付报告｜状态：🟡 待验收
> 日期：2026-09-14 · 执行端 · Gate：`G3-1`
> 取号：**HH.277**（账本水位线 **276→277** · `D640 #10` 禁预留）· 回执占 HH.276
> HEAD 基线：`349959c9` · **未 push**
> 依据：`HH.276_美术资源收口批_任务书.md`（D722）＋ `HH.276_美术资源收口批_开工回执.md`（本批）＋ D723 派工总纲

---

## 〇、一句话结论

**件1 成立**：`GroundFeature.spriteatlas` 落地并**实机合批生效**，DrawCall **663 → 204（3.25×）**、默认视域 **638 → 194（3.29×）**、近景 **129 → 52（2.48×）**；**但未达「80 量级」目标**，根因**已被隔离实证**＝**缺山地图**（`Mountain`/`SnowMountain` 899 格占位块各持**运行时生成贴图**，与图集页交替 ⇒ 打断合批；把这些格改挂图集内 sprite 后 **167 → 15**）。
**件2 阻塞**：仓库内不存在山地图素材 ⇒ **未自行生成替代素材**（红线 M5），线 4 ❌。
**件3 交付清单＋估算**，并**勘正任务书三处口径**：`run`／`loot` **非欠账**，`death` 为**唯一真欠账**（0/49）。

---

## 一、施工结果

| # | 落地 | 证据 |
|---|---|---|
| **件1** | 新建 `Valley Rampart/Assets/Resources/Config/Art/GroundFeature.spriteatlas`（＋`.meta`）＝**本批唯一新增文件（2 个）** | `git status` 仅此 2 项 |
| 件2 | **未落地**（无素材） | 见 §二 线 4 |
| 件3 | 清单＋估算（回执 §四）；**未出图** | — |

**零改动面**：`MapGenRules.cs`／`MapRenderService.cs`／`GameScene.unity`／`UnitsFrames.spriteatlas`／任何 `.png`（PPU／pivot／压缩／尺寸）／`AI.Core` —— 全部零触碰。

---

## 二、验收线 7 条逐项

### 线 1｜图集在场 ✅

- 资产在位：`Assets/Resources/Config/Art/GroundFeature.spriteatlas`（`AssetDatabase.LoadAssetAtPath<SpriteAtlas>` 非空，`spriteCount = 22`）
- `packables = 2`（文件夹口径，照 `UnitsFrames` 先例）：`Assets/_Game/Art/Ground` ＋ `Assets/_Game/Art/Buildings/neutral/features`
- **Ground 6 张全部入包实证**（`m_PackedSpriteNamesToIndex` 逐名）：`ground_temperate`／`ground_tropical`／`ground_cold`／`ground_subtropical`／`ground_river`／`ground_ocean` ✅ ＋ 特征物 16 张

### 线 2｜合批效果（核心）⚠️ 成立但未达目标值

**口径**：正门 `TestHarnessApi.EnterTestRun`（seed **21107**／Medium／diff **2**／**1x**）· 同一探针结构 · `Time.timeScale=0` 冻结 · `UnityStats.drawCalls`（同源 `UnityStats`，`L-35`）。

| 观测位 | 改前（图集临时移出 `Assets/`） | 改后 | 降幅 |
|---|---|---|---|
| **默认视域** ortho 8.64 | **638** | **194** | **3.29×** |
| **主城 home(128,128)** ortho 8.64 | **663** | **204** | **3.25×** |
| **近景**（贴最近单位）ortho 2.5 | **129** | **52** | **2.48×** |

> 改前基线独立复现三次：`638/639/628`、`663/664/654`、`129/130/133`（离散 ≤11），与 `HH.275` 记载 `652`／`HH.273` 记载 `≈664` 一致。

**载入面一致性（配对成立的关键）**：两次跑的 tile 集合**逐数相同** —— 地皮 **5184**（改前 5 张分列 `2116+68+896+1344+760`；改后 **全部归并 `sactx-0-1024x512-DXT5|BC3-GroundFeature-…` = 5184**）、特征物 **2034**（改后 `图集 1038 ＋ 占位 899 ＋ OffsetTile 97`）⇒ **差异可唯一归因于图集**。

**贴图归并实名**：改后地皮层贴图**只剩 1 张**＝`sactx-0-1024x512-DXT5|BC3-GroundFeature-1a932aaa`（1024×512）；特征物层 1038 格同页。

**未达 80 量级的原因（已隔离实证）**：

| 步 | 观测 | 读值 |
|---|---|---|
| 现状（图集生效 · home ortho 8.64） | — | **167** |
| 把 Feature 层中 **899 个「运行时占位」格**改挂图集内 tile（**仅探针内存替换**，测后原样还原） | → | **15** |
| 还原后复测（防污染） | → | **167** |
| `Territory` 层置 `SetActive(false)` | → | **167**（**贡献 ≈ 0**） |

⇒ **剩余开销 100% 来自 `Mountain`/`SnowMountain` 缺真图**（`MapRenderService.cs:530-540` `FeatureArtId` 返 `null` ⇒ 回落 `FeatureToFeatureColor` 的**运行时生成贴图**，与图集页交替 ⇒ 每段交替一次 SetPass）。
⇒ **件2 一旦补图，同样条件预期落到 ~15 量级**（远优于「80 量级」目标线）。

### 线 3｜不破坏 ✅

| 检 | 读值 |
|---|---|
| `CanBindTo`（`UnitsFrames` 对单位 sprite） | **5/5** ✅：`unit_human_worker_idle`(8 帧)／`unit_elf_archer_walk`(16)／`unit_dwarf_general_attack`(16)／`unit_orc_warrior_idle`(8)／`unit_monster_raider_idle`(8)，其运行时贴图全 = `sactx-0-2048x2048-DXT5\|BC3-UnitsFrames-bacae713` |
| `SpriteRefTable` 间接层 | `TryGet("ground_temperate")` ✅／`TryGet("feat_tree_temperate_1")` ✅／`Contains()` 两者均 True ✅；键空间 175 单图 ＋ 152 帧**未变** |
| 缺图回退链 | `PlaceholderSprites.Get("bld_academy")` 非空 ✅（占位生成）；`Mountain`/`SnowMountain` 占位路径逐次复现告警 ✅（**回退链未被图集吞掉**） |
| 视觉保真（截图逐像素） | `before_01/02` vs `after_01/02`（1920×720）：通道差 **>64 仅 0.016%**／**>32 仅 0.16%**／>8 为 4.7%（昼夜色差底噪） ⇒ **无可见劣化／无压缩带状伪影** |

### 线 4｜山地图 ❌ **未达**（件2 阻塞）

- `feat_mountain`／`feat_snowmountain` **未入表、未入集**（素材不存在，见 §三 图源）
- 实机取色命中率（`after_*.png` 1920×720）：`Mountain (115,102,89)` = **27 / 38 / 28 px（0.002~0.003%）**；`SnowMountain (217,224,235)` = **0 / 0 / 1 px（≈0%）**
  - ⇒ **非 0**；但**主因是可视面积小**（home 视域内山体格很少），**结构性证据更强**：整图 **899 格占位块**仍持运行时贴图，运行期告警逐次复现 `[MapRenderService] 特征物无真图：ft=Mountain/SnowMountain`
- **判定：线 4 ❌ 未达**，根因＝**素材未到**（🔴 阻塞，非执行缺陷）；形状问题归 `HH.272 §二④ 山脉化`

### 线 5｜单位缺态 ✅（清单在场）／补图**未做**（范围待裁）

- 欠账清单在场（回执 §四）：`idle 48/49`／`walk 49/49`／`attack 45/49`（缺的 4 个＝四族幼儿，**预期**）／`run 3/3`／`loot 3/3`／**`death 0/49`**
- `death` 计数仍 **0**（≤ 期望 > 0）⇒ **如实说明：工作量估算已报，范围待策划端定（本批未补图）**
- **三处口径勘正**（详见回执 §四.2）：①`run` 仅坐骑使用（`SpriteAnimator.cs:184` `IsCharging`）⇒ 现役 3 条**即全量**；②`loot` 的 `NotifyLoot()` **全库零调用点** ⇒ 补帧零收益；③`idle` **非齐**（`unit_human_archer_idle` 文件在盘但被 `ArtImportPipeline.cs:35` **显式排除**）⇒ 属**切帧规格修复**，非补图

### 线 6｜回归 ✅

- 编译 **0 error**（`unity_editor.get_state`：`isCompiling=false`、`console.lastErrors=[]`）
- 正门 `EnterTestRun` ＋ `ExitTestRun` ＋ 收尾三态：`isPlaying=false`／`isCompiling=false`／`sceneDirty=false` ✅（`L-32`）
- **四族四轮冒烟零退化**（`Valley_HH239_ArtProbe.RunRound` × 4，seed 21107）：

| 轮 | 建筑真图率 | 单位帧解析 | 地皮 distinct | 特征物层 real/distinct | 新增/丢失贴图 |
|---|---|---|---|---|---|
| race0 human | **2463 / 2463（100%）** | 34 / 34 | 5 | 135 / 15 | 无 |
| race1 elf | **2468 / 2468（100%）** | 66 / 66 | 4 | 137 / 15 | 无 |
| race2 dwarf | **2463 / 2463（100%）** | 98 / 98 | 5 | 135 / 15 | 无 |
| race3 orc | **2456 / 2456（100%）** | 130 / 130 | 5 | 135 / 15 | 无 |

> 逐数与 `HH.273` 记载（`2463/2468/2463/2456`）**完全吻合**、`HH.267` G 段口径（`135~137 / 15`）**一致** ⇒ 零退化。

### 线 7｜写后验 ✅

- 本批相关路径 `git status` = **仅 2 个新增文件**（`GroundFeature.spriteatlas` ＋ `.meta`）；`GameScene.unity`／`MapRenderService.cs`／`MapGenRules.cs`／`UnitsFrames.spriteatlas` **全空**
- `screenshots/` 0 行（`.gitignore:60` 已忽略）
- **未 push**

---

## 三、图源确认（件2 阻塞证据）

全库检索 `*mountain*`：**唯一命中** `Medieval Kingdom/Sprites/Environment/mountains.png`（**外部参考包 · untracked · 非本工程 artId 体系**）；`美术资源文件夹/Buildings/neutral/features/`、`Assets/_Game/Art/Buildings/neutral/features/`、`pixel-forge/workspace/` 均**无山地图**。
⇒ 按红线 M5「图本身由美术（或用户 pixel-forge）产出 ⇒ 若图未到 ⇒ 如实列报阻塞，**不自行生成替代素材**」⇒ **件2 列 🔴 阻塞**。
**待产规格**（照 `美术资源规范_等轴立方体瓦片.md`）：`feat_mountain.png`／`feat_snowmountain.png`，等轴菱形 2:1 · PPU 100 · 1 格（可含高度层，占位现值 `layers=3`），落 `Assets/_Game/Art/Buildings/neutral/features/`（该夹**已是 packable ⇒ 入图集零操作**）。接入另需 ①`SpriteRefTable.asset` 补 2 键 ②`MapRenderService.FeatureArtId` 补 2 个 `case`（**业务代码·须另签发**）。

---

## 四、`L-34` 在线判据表（五列）

| 观测项 | 可判定最早时点 | 命中即停条件 | 本次读数 | 结论 |
|---|---|---|---|---|
| 图集是否接管贴图 | 进局后首帧（`MapRenderService` 铺格完成） | 地皮层贴图名以 `sactx` 开头 | ✅ `sactx-0-1024x512-…-GroundFeature-…` 单一页 | 生效 |
| DrawCall 改善 | 进局 +2s、相机冻结 20 帧后 | 同机位读值 < 基线 0.6× | 638→194 / 663→204 / 129→52 | 改善成立 |
| 是否达 80 量级 | 同上 | 读值 ≤ 80 | 194 / 204 / 52 | **未达** ⇒ 转隔离探针 |
| 剩余开销归属 | 隔离探针（占位格换图集 tile） | 换后读值 ≤ 80 | **167 → 15** | 归属＝缺山地图，**立即停**（不再追其它假设） |
| 单位/建筑真图率 | 四族四轮每轮探针末 | 任一 < 100% | 4 轮全 100% | 零退化 |

> 无「跑完再看」：四项均为**机制读数定论**（贴图名／tile 计数／帧解析计数），未挂在世界过程上。

---

## 五、列报

1. **线 2 目标值修正建议**：任务书目标「652 → **80 量级**」是基于 `HH.275` E6（地皮统一为 1 图 ⇒ 58）；实测**地皮+特征物同页**后仍有 **204**，缺口全在**缺山地图**。建议把线 2 判据改为**「≤ 基线 0.35×」**（现值 0.31× 已过），并将「→80 量级」挂到件2 落地后复测。
2. **线 4 为「依赖阻塞型」判据**：本批结构性不可达（无素材），**非 `L-29`/`L-30` 违规**（已如实列报，未找补）。
3. **`HH.239` 美术探针负探针判据陈旧**：该探针查 `PlaceholderSprites.Get("ground_lake")` 期望非空，实测 `False`；`git log -S "ground_lake"` **零命中** ⇒ 全库历史从未有该键（`R5` 收敛后水系走 `ground_ocean`/`ground_river`）⇒ **探针期望值过时**（撞 `L-26` 家族：判据本身不可满足），**非本批引入**，建议随下轮修探针。
4. **`GroundFeature` 页为 DXT5 压缩**（与 `UnitsFrames` 页同格式 `DXT5|BC3`）：像素差实测 >64 仅 0.016% ⇒ 判定无可见劣化；若后续对地皮清晰度有更高要求，可把该图集 `textureCompression` 由 0 改 `CompressedHQ` 复测（另签发）。
5. **件2 二次接入成本极低**：图夹已是 packable、`SpriteRefTable` 为间接层 ⇒ 素材到位后仅「2 键入表 ＋ 2 个 `case`」，可并入 `HH.272 §二④ 山脉化` 同批验收（形状＋真图一次看）。
6. **本批工具面**：双 MCP（`mcp_unity-bridge`）本次**注册可用**，全程 `exec_editor_script`/`exec_runtime_script`，**未走终端 TCP 直连**（对照 `HH.270` 的 D698 §3 应急），可更新 `unity-mcp-first` 的可用性记录。

---

## 六、红线遵守

| 红线 | 落实 |
|---|---|
| 正门 `EnterTestRun`／收尾真暂停＋`ExitTestRun`＋退 Play | ✅ 全部探针走 `TestHarnessApi.EnterTestRun`；末态 `isPlaying=false` |
| **不为过线凑数**（`L-30`） | ✅ 线 2「未达」、线 4「❌」均**如实判负**，未改口径就范 |
| 只做资产面 | ✅ 零业务代码；`MapGenRules.cs`／`MapRenderService.cs` 零触碰 |
| **不碰 `GameScene.unity`** | ✅ `git status` 场景文件 0 行（仅「打开」用于探针，未保存、`sceneDirty=false`） |
| **不改贴图本体** | ✅ 无任何 `.png`／`.meta` 改动 |
| 不删 `UnitsFrames` | ✅ 且其实测 `CanBindTo 5/5` 仍有效 |
| 具名 `git add`／禁 `-A`·`-u`·`.`／**不 push** | ✅ 见 §七 |
| 改前重读磁盘／共享文档增量改 | ✅ |
| `_任务队列.md` 一行不写 | ✅ |

---

## 七、产出

**新增（本批唯一入库面）**
- `Valley Rampart/Assets/Resources/Config/Art/GroundFeature.spriteatlas` ＋ `.meta`
- `多Agent交接/执行端/HH.276_美术资源收口批_开工回执.md`
- `多Agent交接/执行端/HH.277_美术资源收口批_交付报告.md`（本文件）
- `多Agent交接/_编号登记.md`（水位线 276→277 ＋ HH.276/HH.277 行）
- `多Agent交接/_交接索引.md`（HH.277 行）

**截图（不入库 · `.gitignore` 已忽略）** `screenshots/HH276_美术收口/`
| 文件 | 一句话观察 |
|---|---|
| `before_01_default.png` | 默认视域 · 图集前 · 地皮由 5 张贴图交替渲染（DrawCall 638） |
| `after_01_default.png` | 同机位同刻 · 图集后 · 地皮归并单页（DrawCall 194） |
| `before_02_home.png` | 主城 home ortho8.64 · 图集前（663）；可见山体为灰色占位块 |
| `after_02_home.png` | 同机位 · 图集后（204）；**画面观感与前者一致**（差 >64 仅 0.016%）；山体仍为占位块（件2 未做） |
| `before_03_close.png` | 近景贴最近单位 ortho2.5 · 图集前（129） |
| `after_03_close.png` | 同机位 · 图集后（52） |

**原始读数载体（不入库）**：`Valley Rampart/Logs/hh276_dc_before.log`／`hh276_dc_after.log`／`hh276_decomp.log`／`hh276_shot_before.log`／`hh276_verify.log`／`hh276_4race.log`

---

> 执行端 · 2026-09-14 · 下串＝`HH.274 单位显示与移动面`（D723 派工总纲序 2）→ `HH.272 地图生成重构批`（序 3）
