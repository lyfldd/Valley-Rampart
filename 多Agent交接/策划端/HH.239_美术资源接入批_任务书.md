# HH.239 美术资源接入批（HH.103 落地）任务书

> 签发：主策划端（团结 Cowork）｜2026-09-12｜**Gate = `G3-1`（美术接入）**
> 决策依据：**D689**（0.6 §二百一十八 · 《美术资源接入计划》定稿）· **D691**（断链校验台，本批不涉）
> 前置文档（执行端必读）：[美术资源接入计划](../河谷防线开发计划书具体内容/改造计划/美术资源接入计划.md)（§一~§十一）· [美术资源接入映射表](../../最高优先级文档/03_玩家分支/表现与资源接入/美术资源接入映射表.md)（§二~§十）· [美术资源规范_等轴立方体瓦片](../河谷防线开发计划书具体内容/改造计划/美术资源规范_等轴立方体瓦片.md)（D37/§一/§五/§八）· [3.6.1 弹药消费清单](../河谷防线开发计划书具体内容/3.6.1_弹药消费清单.md)（D686）
> 素材源：`美术资源文件夹/`（≈315 张·**仓库内未跟踪二进制**）
> **完成报告＝执行端届时按账本实时水位线取号**（`D640 #10` 禁预留）

---

## 〇、一句话目标

把 `美术资源文件夹/` **全量**（≈315 张）导入 `Assets/_Game/Art/`，**建立 `SpriteRefTable`（artId→Sprite）并接线**，使游戏运行后**建筑/资源/地皮/单位/弹药/特效显示真图**，**缺图处自动回退占位**（不崩不空白）；同时**清除 `Assets/_Game/Art/Ground` 残留**。**一次性全量、同批贯通**（S1~S8 为批内施工序，非多批）。

---

## 一、范围（D689 六定案，不得擅改）

| 项 | 定案 |
|---|---|
| 范围 | **一次性全量**（≈315 张·同批贯通） |
| 深度 | **入库＋接代码**（真图游戏内生效，非只入库） |
| 残留 | **删除＋清场景引用**（先摘 `GameScene` 引用→删 `Ground/`→清悬空 meta） |
| 动画 | **本批做·两向**（只画朝右＋运行时 `flipX` 补左） |
| 目标路径 | **`Assets/_Game/Art/`**（修正映射表 §八 的 `Assets/Art/`） |
| 地形 | **升级真图**（补 `ground_ocean`/`ground_river` 键） |

---

## 二、施工序（S1~S8·每序留证据）

```
S1 清残留 → S2 建 SpriteRefTable 骨架 + 素材入库 + 配导入设置
 → S3 建筑/资源线（①：键空间收敛＋补键＋接表）
 → S4 地皮线（②）
 → S5 主城/脚手架线（⑤）
 → S6 单位线（③：立绘＋Sheet 播放器两向＋机器）
 → S7 弹药/特效线（④）
 → S8 全量回归＋验收
```

---

## 三、实施清单（逐节扫·跨文档依赖·完整性校验）

### 3.1 任务总表

| 编号 | 任务 | 类型 | 依赖 | 文档出处 | 验收标准 | 证据文件 |
|---|---|---|---|---|---|---|
| **T1** | 摘除 `GameScene.unity` 对残留 Ground Tile/`Palette1111.prefab` 的引用 | 架构 | — | 计划 §六 | `GameScene` 无 Missing 引用（Unity Console 零 missing） | `Assets/Scenes/GameScene.unity` |
| **T2** | 删除 `Assets/_Game/Art/Ground/` 整目录（含 `.meta`）＋清悬空 `Buildings.meta`/`Features.meta`＋处置 `Units/` 13 张匿名残图 | 架构 | T1 | 计划 §六 | 目录不存在；`git status` 无该目录 | `Assets/_Game/Art/Ground/` |
| **T3** | 全量素材入库至 `Assets/_Game/Art/{Portraits\|Units\|Buildings\|Machines\|Ammo\|Effects\|Ground}/` | 架构 | — | 计划 §一/§二 | ≈315 张按类落位；分类计数吻合（46/137/107/8/8/3/6） | `Assets/_Game/Art/` |
| **T4** | 配导入设置：PPU=100 / pivot=底面中心 / filter / compression / 排序（Custom Axis (0,1,0)＋Sort Point=Pivot） | 架构 | T3 | 计划 §七 · 美术规范 §一 | 抽检 `TextureImporter` 各字段＝规定值 | 各 `.png` importer |
| **T5** | **Sheet 批量切帧**：状态定网格（idle/loot=3×3·**播 8 帧**；walk/attack/run=4×4·16 帧；icon=单张） | 架构 | T4 | 计划 §七.1 | `TextureImporter.spritesheet.Length == cols*rows`（读回抽检）；**帧格≠有效帧**（idle 切 9 播 8） | `Assets/_Game/Art/Units/**/*.png` |
| **T6** | 新建 `SpriteRefTable` SO（artId→`Sprite` 直接引用）＋`TryGet(artId, out s)` | 架构 | T3 | 计划 §三 | SO 资产存在；`TryGet` 命中返回真图 | `Assets/Resources/Config/Art/SpriteRefTable.asset`（或同域） |
| **T7** | **键空间收敛**：`BuildingVisual.GetPlaceholderKey` 的 legacy key（`"tree"/"castle"/"wall"/"tower_arrow"/"war_academy"/"mine"/"farm"/"rift"/…`）→ artId（`bld_*`/`feat_*`） | 架构 | T6 | 计划 §五.3 · 差异 D5 | `GetPlaceholderKey` 不再返回 legacy key；grep 零 legacy 命中 | `BuildingVisual.cs:12-49` |
| **T8** | **补 artId 缺键**：普通建筑 12 项（`building_{barracks/blacksmith/hospital/church/siegeworkshop/ranch/well/crossbowtower/magictower/vagrant_camp/portal/scaffold_*}`）＋地皮 2 项（`ground_ocean`/`ground_river`）＋枯木改名（`feat_wood_pile`→`feat_deadwood`） | 架构 | T6 | 计划 §2.2 D1/D2/D3 | 新键在 `SpriteRefTable`／`PlaceholderSprites._defs` 均可解析；**禁虚挂**（`L-15`） | `Rendering/PlaceholderSprites.cs:28-68` |
| **T9** | 接线线①：`BuildingVisual.ApplyPlaceholder` 改「先查 `SpriteRefTable` → 命中真图 → 未命中回退 `PlaceholderSprites.Get(artId)`」 | 架构 | T6/T7/T8 | 计划 §四① | 建筑真图显示；缺图回退占位不崩 | `BuildingVisual.cs:52-60` |
| **T10** | 接线线②（地皮）：`MapRenderService.GroundTile` 改按 `FeatureType`→artId 查表，命中真图、未命中回退 `CreateIsoDiamondSprite` | 架构 | T6/T8 | 计划 §五.2 · 差异 D1 | 地皮显示真图；缺图回退生成菱形 | `MapRenderService.cs:303-309` |
| **T11** | 接线线⑤（主城/脚手架）：主城按 `(race, level)` 取图；脚手架按 footprint 三档选图（1×1 兼覆城门·D642） | 架构 | T6 | 计划 §四⑤ · 差异 D4 | 主城 `castle_{race}_lv0..6` 按级显示；脚手架按尺寸选图 | `BuildingDef.levels`／`bld_castle` 键 |
| **T12** | 接线线③（单位立绘）：`UnitData.prefab` SpriteRenderer 换真图（或经 artId 表） | 架构 | T3/T6 | 计划 §四③ | 单位立绘显示真图 | `Resources/UnitPrefabs/` |
| **T13** | **新建动画播放器**（`SpriteAnimator`·自研轻量·路线A）：持 `Sprite[]`＋fps＋状态名；`Update` 推进；`flipX` 处理朝向 | 架构 | T5/T12 | 计划 §七.2 | 单位 idle/walk/attack/run 循环播放；左右移动 `flipX` 正确 | 新建 `.cs`（`Assets/_Game/Systems/Units/` 或同域） |
| **T14** | 接线线④（弹药/特效）：`Resources/Ammo/*.asset`(AmmoDef) 按 3.6.1 弹种对位挂 sprite；`GroundEffect_*.asset` 对位 | 架构 | T6 | 计划 §四④ · 差异 D7/D8 | 弹种 sprite 对位；命中特效对位（fireball→Burn/magic→Slow/stone→?） | `Resources/Ammo/*.asset` |
| **T15** | 接线线③补（机器）：`machine_*` 按 `UnitData`/prefab 接线（`_strip`=机器动画） | 架构 | T6/T13 | 计划 §四③ · 差异 D6 | 战争机器显示真图/动画 | `UnitData.prefab` |
| **T16** | **1D 遗留删除**：迁走调用方（`BuildingVisual.cs:57`／`Building.cs:516/523`／`ChestEntity.cs:50`／`LoadManager.cs:61`／`BuildProgressBar.cs:62,72`）→ 删 `Core/PlaceholderSprites.cs`＋`Core/SpriteFactory.cs`（含 `.meta`） | 架构 | T9/T11 | 计划 §五.3 | 全库 grep `Core/PlaceholderSprites`/legacy key **零命中**；编译 0 错 | `Core/PlaceholderSprites.cs`·`Core/SpriteFactory.cs` |
| **T17** | 全量回归＋验收（§九 6 条） | 架构 | T1~T16 | 计划 §九 | 见 §四 验收线 | — |

### 3.2 跨文档依赖矩阵

| 本任务 | 依赖文档 | 依赖事项 | 状态 |
|---|---|---|---|
| T8 弹药对位 | 3.6.1_弹药消费清单（D686） | 弹种全集（`musket`/`mage_barrage`/`monster` 待坐实） | ⚠️ HH.233/234 待裁决项 |
| T14 命中特效 | `GroundEffect_Burn/Slow.asset` | `stone` 特效归属未定 | ⚠️ 待坐实 |
| T3/T5 素材 | `美术资源文件夹/`（HH.209 遗留） | **仓库内未跟踪二进制**；gitignore 口径未定 | 🔴 **主权项·须报主端** |
| T13 播放器 | 无（项目**无现成动画系统**·实读） | 须新建 | ✅ 已定路线A |
| T5 切帧 | 差异 D10（弓箭手待机 96×102） | 归批5 重出 | ⚠️ 本批跳过该张 |

### 3.3 完整性校验表（§一~§十一 逐节核对）

| 计划章节 | 清单任务 | 状态 |
|---|---|---|
| §一 范围/深度/残留/动画/路径 | T1~T17 全表 | ✅ |
| §二 素材盘点＋差异 D1~D10 | T3/D1→T8/T10；D2→T8；D3→T8；D4→T11；D5→T7；D6→T15；D7→T14；D8→T14；D9→T16；D10→跳过 | ✅ |
| §三 加载架构（SpriteRefTable） | T6 | ✅ |
| §四 5 线接线点 | ①T9 ②T10 ③T12/T13/T15 ④T14 ⑤T11 | ✅ |
| §五 口径缺口（5.1~5.4） | 5.1→T3 路径；5.2→T10；5.3→T7/T8/T16；5.4→T5/T13 | ✅ |
| §六 残留清理 | T1/T2 | ✅ |
| §七 导入设置＋切帧＋播放 | T4/T5/T13 | ✅ |
| §八 施工序 S1~S8 | §二 施工序 | ✅ |
| §九 验收 6 条 | §四 验收线 | ✅ |
| §十 风险 | §五 风险表 | ✅ |
| §十一 任务书要点 | 本任务书 | ✅ |

**铁律自检**：架构层 17 项（无参数层淹没）· 跨文档依赖 5 条全标 · 无模糊动词 · 每条含"改哪/改什么样/怎么验收"· 生命周期闭环（T16 删除走完整调用方迁移）· 完整性校验表已出。

---

## 四、验收线（§九 6 条·硬）

1. **真图生效**：每类至少抽 1 实**在游戏内肉眼显示真图**（非仍占位）。
2. **缺图回退（负探针）**：临时移走某 artId 图 ⇒ **回退占位且不崩**（`SpriteRefTable` miss → `PlaceholderSprites`）。
3. **锚点/比例**：真图符合 PPU=100＋pivot=底面中心＋2:1 菱形（美术规范 §五 4 条）。
4. **两向动画**：左右移动动画正确（含 `flipX`）、无抖动/错帧；**idle 播 8 帧**（切 9 格）。
5. **残留清零**：`GameScene` 无 Missing 引用；`Ground/` 残留目录不存在；全库 grep `Core/PlaceholderSprites` 零命中。
6. **回归**：既有冒烟容器零退化（`Smoke_2_22P0`/`Smoke_2_23RP0`/`Valley2_17_Smoke_5`）。

---

## 五、红线

- **`so-data-driven`**：`SpriteRefTable`/导入设置走 SO/资产，禁硬编码。
- **`unity-mcp-first`**：所有 Unity 操作（导入/切帧/删资产/接线）**走 MCP**，**禁**写 `[MenuItem]` 让用户手点；双 MCP 路由（CoplayDev 主／Codely 兜底）。
- **`sim-sync`**：本批**不触** `AI.Core`（纯表现层）；若意外触，**先核同源再动**。
- **`test-harness-first`**：若进局验证，走正门 `TestHarnessApi.EnterTestRun`；收工退 Play（`L-32`）。
- **零设计改动**：本批**只接表现**，不动建筑/单位/弹药的**设计语义**（AI 决策核零改）。
- **证据纪律**：验收句必须**可判定**（`file:line`／grep 零命中／真图生效截图），禁把"改了"当"改对了"。

---

## 六、请裁项（执行端开工前须先报）

1. **gitignore 口径**（`美术资源文件夹/` 未跟踪 ≈315 二进制）——**主权项**，执行端不自行决定，**开工前报主端**。
2. **弹药弹种未定项**（`musket`/`mage_barrage`/`monster`，HH.233/234）——按 3.6.1 已定项先做，未定项**列报挂账**，不擅自定。
3. **弓箭手待机 96×102**（差异 D10）——本批**跳过**，归批5 重出。

---

## 七、产出

- 真图生效（§四 6 条验收）＋ 残留清零 ＋ 回归零退化
- **交付报告**（含每序证据 `file:line`＋截图）＋ `sim-sync` 核查回执（若触）
- 回执须含：`L-30` gap 表（若进局）＋ 列报请裁项

---

> 版本：2026-09-12 签发（主策划端）。Gate=`G3-1`。完成报告＝按水位线取号。

---

## 八、增补节（子主策划·美术接入域 · 2026-09-12；三问已用户裁示）

> 归属：美术接入域策划（D689 授权代理）。本节为**域内增补/勘正**，不改 §〇~§七 既有条款；与既有记载冲突处**以本节为准（更新）**。

### 8.1 实盘勘正（对账时点 2026-09-12，磁盘为准）

| # | 项 | 磁盘实况 | 处置 |
|---|---|---|---|
| E1 | 素材总量 | **≈328**（四族 313＝Portraits 46＋Units 137＋Buildings 105＋Machines 8＋Ammo 8＋Effects 3＋Ground 6；＋monster 15） | T3 计数勘正（原 46/137/107/8/8/3/6 → 46/152/105/8/8/3/6＋monster 15） |
| E2 | neutral 建筑 | **57 张**（非 ~59） | 映射表头部/§十.1 已勘正 |
| E3 | **monster 15 并入**（用户 2026-09-12 裁示） | `Units/monster/{raider,slinger,brute}/{idle,walk,attack,loot,icon}.png` | 并入 T3 入库＋T5 切帧；**接线面**（怪物渲染链 `MonsterDef`/`MonsterController`）由执行端**开工回执侦察后列报**，侦察前不动接线 |
| E4 | 新弹种素材已在盘 | `ammo_musket/ammo_monster/ammo_mage_barrage.png`（HH.234「待产」记载过时） | **入库不接线**；接线挂 3.6.1 弹种转正批（见 H3） |
| E5 | gate_open 缺图（用户裁示：**待补美术**） | 素材仅 `building_gate_closed.png` | 本批 closed 真图接线；**开门态临时复用 closed 图**；映射表登记缺图面（open 补抽后增量接入替换） |
| E6 | T8 补键 def id 锚 | `Resources/Buildings/` **41 资产在场** | T8 十二项补键逐一挂实 def id（Barracks/Blacksmith/Church/CrossbowTower/Hospital/Ranch/SiegeWorkshop/VagrantCamp/portal/magic_tower/arrow_tower/Well＋scaffold 三档），**禁虚挂**（L-15） |
| E7 | 残留目录边界 | `Art/Markers/`、`Art/UI/` 亦为空目录 | **保留**（mark_*/UI 图标目标位）；仅清 `Buildings.meta`/`Features.meta` 悬空 meta |

### 8.2 硬条款（新增）

- **H1（T1 引用枚举）**：`.meta` guid 为**密文**（实测文本互映零命中，阳性对照已做）⇒ GameScene 对残留 Ground Tile/`Palette1111` 的引用枚举**必须走编辑器侧**（unity-mcp-first：场景引用查找/FindReferences）；**禁以文本 grep 零命中判「无引用」**（L-29）。
- **H2（树 3 变体选择机制·域内裁【美术接入】）**：`feat_tree_{climate}` 3 变体按**格坐标确定性哈希**选变体（`(gx*73856093 ^ gy*19349663) % 3` 级纯函数：同格恒定、同 seed 同局稳定、零 rng 流污染、零存档）；**禁接入地图生成 rng 流**。
- **H3（弹种接线边界）**：T14 只挂现役 `ProjectileType`（`AI.Core/Config/ProjectileTypes.cs` 六值）可消费素材（Arrow/Bolt/Stone/Fireball/Magic；`ammo_heavybolt` 保留入库不接线＝3.6.1「退役/另用」）；musket/monster/mage_barrage 三张入库待 3.6.1 批。**`ProjectileType` 扩展属 AI.Core 域＝sim-sync 红线，本批禁触**。
- **H4（T14 搭车）**：施工时顺带**编辑器坐实** 3.6.1 §一 的 4 组 guid 推断引用（弩炮/臼炮/法师/藤蔓），结果回写 3.6.1 取证边界（HH.234 余留待办①清偿）。

### 8.3 三问裁示记录（2026-09-12 用户）

① monster 15 **并入本批**；② gate_open **用户补抽**（本批 closed 接线＋开门态临时复用 closed）；③ HH.233/234 **域内按 D686 对齐回写**（无新裁决点，回写已落两信 §五/§六）。

### 8.4 动画架构定档与图集（2026-09-12 第二轮复审·用户两问全准）

**H5（T13 验收口径·全配档）**

- **架构**＝单管理器集中推进（ProjectileManager 同款）＋**结构体数组内联**（平铺 `struct{timer; frame; fps; spriteRef;}`——连续内存遍历、零虚调用、零装箱）＋**脏写跳过**（帧索引未变不写 sprite 属性）＋**不可见注销**（`OnBecameInvisible` 移出活跃表／`OnBecameVisible` 回表）；
- **LOD 动画分层**：对接既有 `LodLevel`（`Systems/AI/LOD/MidChunkLodState.cs:11`；`Systems/AI/LOD/LODSystem.cs:23`；NPCBrain thinkHz 分档先例 `NPCBrain.cs:502-506`）——近档 12fps 全速／远档降半频或冻结静态帧；**查本单位已有档位，禁另建 LOD 系统**；
- **状态注册表**：per `(race,occ,state)` 共享缓存 `Sprite[]`（400 单位共享 46 套 sheet，帧数组只建一份）；每状态 **playMode＝loop／once回落／once定格**（⚠️ 工人 attack＝工作循环、战斗 attack＝单次回落、death＝定格——三态必须分清）；**同状态重入＝打断重播**（第 0 帧重开）；**完成回调＝P0**（death 播完衔接既有死亡流程／attack 回落靠它）；**随机起始相位**（仅 loop 态进场随机帧，防全场齐步走）；**缺图回退静默化**（负查询缓存＋一次性告警，禁逐帧刷日志）；**icon 态排除**（UI/立绘用，不入运行时状态集）；fps per-state SO 可配＋局部 speedScale（非坐骑 run＝walk 加速）＋全局 `Time.deltaTime`（自动随倍速 0.5~3x 与 timeScale 冻结）；**零 GC／枚举状态／禁每帧字符串比较**；
- **O(1) 注册/注销**：swap-remove 数组、禁 `List.Remove`；OnDestroy 必注销（防泄漏，池复位纪律延伸）；
- **统一 pivot 双重收益**（切帧坑②延伸）：同 sheet 子帧同尺寸同 pivot ⇒ 换帧不改变 bounds ⇒ **排序稳定不重算**——写入验收理由；
- **三档否决**（防自由发挥）：分桶时间分片（收益 <0.01ms）／Burst·Jobs（属性写须回主线程）／GPU·Shader 动画（破坏 SpriteRefTable 间接层＋缺图回退链＋排序稳定三件既有资产；记**远期备选**，P1 帧率红线不达标才议）；
- **验收补充**：300~400 单位场景 **Profiler 证据**＝动画推进 <0.1ms 级＋混编四族帧率达标（对齐 0_总计划 §九⑥）。

**H6（SpriteAtlas 图集·本批纳入）**

- 建 Unity 内建 SpriteAtlas 收 **Units 全部帧**（含 monster）——防 300~400 混编单位换帧打断 dynamic batching（draw call 随可见纹理数膨胀）；随 T4/T5 落（切帧后入集）；`SpriteRefTable` 直接引用入集后 sprite **不失效**；
- 验收：四族混编容器（2_20B）同屏 DrawCall 计数不随族数线性上涨（入集前后对照或机制说明）。
