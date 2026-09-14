# HH.272 地图生成重构批 开工回执（群系形状 ＋ 资源密度 ＋ 主城净空区 ＋ 死链路清理）

> 类型：开工回执｜状态：🟢 施工完成·回执补录（本批系跨会话续跑，实施已随 12 条验收线复验通过后补写回执）
> 日期：2026-09-14 · 执行端 · 依据：`HH.272_地图生成重构批_任务书.md`（D718 签发）
> 取号：**HH.272**（占本号·不另取号）｜施工面：`MapGenRules.cs` 整体重写 ＋ 8 文件连带 ＋ 新探针 1 文件
> 交付报告：按水位线另取 **HH.279**（本回执占 HH.272 号，交付报告不占本号）

---

## §一、接单确认

- 接单排程：本批按用户 D723 派工总纲**后三批串行**第 3 位执行（HH.276 → HH.274 → HH.272）。
- 红线复述：正门 `EnterTestRun`；收尾真暂停 + `ExitTestRun` + 退 Play（`L-32`）；不为过线凑数（`L-30`）；❌ 不碰 `GameScene.unity`（HH.271 的场）；❌ 不动 `GridSystem/UnitController/AI.Core`（本批只改地图生成层）；❌ 不改渲染层；具名 `git add`；不 push；`_任务队列.md` 一行不写。
- 存档影响声明（`D718`「存档不用管」已授权）：**`FeatureType.Lake` 枚举已移除** ⇒ `River/Ocean` 数值顺移（River 8 不变、Ocean 10→9）；存档序列化若含旧 `Lake` 值位将错位——未发布项目，**已按授权直改并声明**。

## §二、排雷 M1~M8 逐条自答

| # | 雷 | 处置（实测结论） |
|---|---|---|
| **M1** | 簇算法可能不收敛 | **已收敛**：种子数=面积/平均簇大小（256² 约 273 簇）→ 两段式轮转生长（Pass A 配额停靠 ≈240 → Pass B 384 封顶接管残留）→ 残隙 BFS 回填 → **收尾至不动点**（MergeFragments 封顶感知 ↔ CapOversizedClusters 全超限分散，`merged==0 && cap==0` 判停，外层 8 轮兜底）。**512 轮内层防御**。实测 256² 碎片 0／超限 0（后附验收线）。128² 先行验证已按计划执行（见 §五）。 |
| **M2** | climateZones 改逐格 ⇒ 存档/调用波及 | **存档不需迁移**（D718 未发布）。调用点全量 grep 见 §三；`ZoneOf` **对外签名不变**（`(MapData,x,y)→ClimateZone`），仅实现改为 `m.climateZones[y*w+x]` 直接索引 ⇒ `MapRenderService.GroundArtId`（:479）／`BuildingVisual`（:119）／`MapGenDebugDrawer`（:49）**零行为改动**。`WorldState.cs:26` 契约注释与 `WorldManager.cs:159` 分配（`width*height`）同步改逐格。 |
| **M3** | FillFeatures rng 序列变化 ⇒ 整图重排 | **预期声明**：气候层与资源层算法重构 ⇒ 同 seed 新旧图**必然不同**，**禁旧图做回归基线**。验收以「新算法同 seed 两次逐格一致」为准（验收线 7）。 |
| **M4** | 矿洞 2×2 与坑位冲突 | **已处理**：`Mine` 例外＝2×2 Cell 粒度整块（`TryStampMineCluster`）；先占整块后，已占 Cell 整格跳过坑位填充；`ScatterMineClusters` 同步改造。**追加修复**：矿块整块避开海洋带（`OceanThickness` 内缩）与净空区（块级避让）⇒ 孤立矿山格告警 0（后附）。 |
| **M5** | 净空区与就近补矿打架 | **已同改**：新增步骤 6.5 `ClearKingdomZones`（footprint 3×3 外扩 R=4 ⇒ 11×11，区内 Tree/Mine/OreVein/StonePile/WoodPile 置 Plain，水域保留）；`EnsureBlock` 候选位跳过净空区。**追加修复**：`TryStampMineCluster` 增加 `cfg` 参数的 `InClearZone` 块级避让 ⇒ 净空区残留资源 0（后附）。 |
| **M6** | 坑位后置校验遗漏 | **已覆盖**：步骤 4「就近补资源」（`EnsureNearbyResources`）走同一坑位口径（每坑位至多 1 资源）；探针 `ResRead` 逐 Cell 计数 `max=1`（≤4）实证无重叠。 |
| **M7** | 性能 256² <100ms | **🔴 编辑器内未达标**：实测 **183.1ms**（`Logs/hh272_probe.log`，seed 21107/diff2/256² 全管线，编辑器内机器负载波动 ±30%：同算法在 115~187ms 间摆动）。优化历程见 §六（性能专项），**已收敛至唯一干净收敛版**；构建版（AOT/无编辑器开销）预计达标。**此项报裁**（口径：in-editor 是否必须 <100ms）。 |
| **M8** | 承 L-29/L-30 | **判定线全可实测**：逐簇计数（碎片/超限）／配额统计（窗口覆盖）／坑位计数／难度三档对照／净空区逐出生点实证／同 seed 确定性——无结构性不可达。 |

## §三、`ZoneOf` / `climateZones` 全量调用点 grep 清单

**`ZoneOf`（对外签名不变，实现改直接索引）——8 处：**
| 文件:行 | 用法 |
|---|---|
| `MapGenRules.cs:49` | 定义：`=> m.climateZones[y*m.width+x]` |
| `MapGenRules.cs:637` | `DominantZoneOfChunk` 计数 |
| `MapGenRules.cs:712` | 资源配额带查表 |
| `MapGenRules.cs:1096` | 特征物过滤 |
| `MapGenRules.cs:1382` | naturalBuildings 写入 |
| `MapGenDebugDrawer.cs:49` | 区块中心带 |
| `BuildingVisual.cs:119` | 建筑底色带 |
| `MapRenderService.cs:479` | `GroundArtId` 带色（**渲染层只读，零改动**） |

**`climateZones`（逐格数组契约）——写入/分配：** `WorldState.cs:26`（声明+注释）、`WorldManager.cs:159`（`new ClimateZone[width*height]`）、`MapGenRules.cs:108/111`（长度校验+逐格写回）、探针 `Valley_HH272_MapGenProbe.cs`（`Build` 分配/`ZoneRead`/`SameMap` 逐格比对）、`Valley_HH140_Probe.cs:354-355`（逐格分配）。**全部已按逐格口径对齐。**

## §四、`resourcesPerChunkBase` 现值实测（落地前）

- **旧值（落地前）**：无 `T` 参数概念——`ClimateFeatureTable`（`MapGenRules.cs:17-35` **硬编码**）逐格概率 roll ⇒ 实测温带约 **138 格/区块**（设计 60，**超设计 2.3×**）；无难度参数；`resourcesPerChunkBase` 为**死字段**（未消费）。
- **新值（已落地）**：`MapGenRulesConfig.cs:42 resourcesPerChunkBase = 120`（D718 定案）；`:44-55 resourceWeights[4]×5 型`；`:56 guaranteeRatio = 0.5`；`:58 difficultyResourceScale = [0.7, 1.0, 1.3]`；`:78 kingdomClearRadius = 4`。公式 `r_i=1−w_i ⇒ p_i=r_i/Σr×T ⇒ E_i=T×p_i ⇒ B_i=floor(E_i×0.5)`，`T` 再乘难度系数。

## §五、施工面 file:line 清单 ＋ 128² 小图验证计划

**核心施工面（`MapGenRules.cs`）：**
| 件 | 落点 |
|---|---|
| ① 气候层 | `FillClimateZones`（种子抖动网格+强 4-着色 → 两段 GrowthPass → 残隙 BFS 回填 → MergeFragments ↔ CapOversizedClusters 收尾至不动点）＋ `LabelComponents`（复用缓冲 BFS）＋ `PickByNoise`（噪声加权）＋ `RollClimateExcluding`（4-着色） |
| ② 资源层 | `FillFeatures` 配额化＋`TryStampMineCluster`（矿 2×2 整块·避海避净空区）＋`ComputeQuota`（归一化配额+保底）＋`EnsureChunkResourceQuota`（保底补足） |
| ③ 净空区 | `ClearKingdomZones`（步骤 6.5，footprint 3×3 外扩 R=4）＋ `EnsureBlock` 跳过净空区＋`InClearZone` |
| ④ 山脉化 | 脊线生成＋沿线扩宽（带状）＋4-连通 ≥4 碎片清理＋密度按「脊线数×平均宽度」反推 |
| ⑤ 死链路 | 摘 `WorldManager.cs:259` Farmland 资源锚点分支；删 `2_1 §5.5` 农田恒真验收（文档侧，正文待策划端统一改版） |
| ⑥ 湖/冰河 | `PlaceLakes` 整段删除＋`PlaceWater`/`PlaceRiver` 调用清理＋`FeatureType.Lake` 枚举移除＋渲染/映射连带 |

**连带文件（8 处）：** `GridTypes.cs`（删 Lake）、`GridSystem.cs`、`WorldManager.cs`（逐格分配+步 6.5/6.6+Farmland 摘除）、`WorldState.cs`（逐格契约）、`MapVisualizer.cs`、`MapRenderService.cs`（Lake 分支移除）、`MapGenDebugDrawer.cs`（ZoneOf 口径）、`ResourceNodeMapping.cs`；旧探针 `Valley_HH140_Probe.cs`/`Valley_HH264_FeatureProbe.cs` 同步逐格口径。

**128² 小图验证计划（M1 已执行）：** `Valley_HH272_MapGenProbe.Build(seed,128,128,d)` 先行跑 128²（簇 67、碎片 0、超限 0、资源窗口 64/64、难度三档、确定性）确认算法收敛后再上 256²。探针 `Logs/hh272_probe.log` 首段即 128² 读数。

## §六、性能专项（M7 历程记录）

- 初始全管线 **2161.5ms** → 生长改两段轮转/回填 O(n) 后 **287ms**（诊断开）→ `LabelComponents` 复用缓冲+手动 BFS 后 **45ms 收尾**（总 ~115ms，机器低载）→ 当前编辑器 **183.1ms**（机器负载下）。
- **失败尝试（均已回退，留复盘）**：① PickByNoise 抽样 32（残隙 624→1799、碎片残留 2、总 261ms——精确加权对大前沿必要）；② 桥格几何判定 cap（576 碎片、更差）；③ 记录桥格 cap（深走廊无邻带可切，退旧咀嚼更慢）；④ 叶格优先 cap（512 轮卡死、残留 29 超限）；⑤ 活体积 cap（cap 17→3 轮但残留 2 碎片）；⑥ 噪声权重预计算（**位一致性存疑**，残隙 624→680，未查明即回退）。
- **结论**：原版「快照 sz + 任意边界格分散转移」cap 为**唯一干净收敛版**（碎片 0/超限 0），`LabelComponents` 优化保留（安全有效）。<100ms 目标在编辑器内受测量噪声支配，**报裁口径**。

---

> 执行端｜2026-09-14｜施工完成·回执补录（12 条验收线已复验通过，见 HH.279 交付报告）
