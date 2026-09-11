# HH.210（原 HH.188）美术端：矿山锚点（feat_mine）口径确认与漏列补报

> **改号说明（2026-09-11，D660/TD-008）**：本件原取 **HH.188**，与训练轨 F26 round3 件（`ai决策大脑强化训练/cards/F26/HH.188_F26round3建档轮_TD007.md`）**撞号**；先落盘者（训练师件）保留，**本件顺延改号 HH.210**。

> 类型：口径确认+漏列补报（用户 2026-09-11 问「矿洞的建造锚点矿山是不是也 2×2」→ 核查发现美术清单未单列 + 文档口径冲突）
> 状态：✅已裁决（D660，2026-09-11 主策划端）
> 日期：2026-09-11 · 发起端：美术端 · 关联：美术资源规范_等轴立方体瓦片 §二 / 3.1.1 §8.2 / 3.1.3 §六（批3）/ 总表 v2 §1 自然·资源 / **D613（mine 2×2）** / D618·D621（矿山缺口件）/ **D641（资源点多态）** / Rendering/PlaceholderSprites.cs:43 / MapGenRules.cs:56

## 一、事由

用户问：矿洞的**建造锚点（矿山）**是不是也是 2×2。核查发现两件事：
1. **矿山锚点 `feat_mine` 是独立美术资产**（2×2），与「矿场建筑 `mine`/`bld_mine_b`（也是 2×2）」**是两个东西**——美术清单此前**未单列矿山锚点**（漏列）
2. **文档口径冲突**：美术规范说 2×2，3.1.1 §8.2 说「占地 1 格」（旧口径未清）

## 二、实盘核查（★两个 2×2 是两回事）

| 名称 | artId | 性质 | 占地 | 归属 | 证据 |
|------|-------|------|------|------|------|
| **矿山锚点/矿洞入口** | `feat_mine` | 地图自然资源点（玩家在其上建矿场） | **2×2** | 批3 自然建筑包 | [Rendering/PlaceholderSprites.cs:43](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Rendering/PlaceholderSprites.cs#L43) `(2,2,1)`；美术规范 §二 |
| **矿场建筑** | `bld_mine_b` / mine.asset | 建在锚点上的产能建筑 | **2×2** | 普通批 A | [Rendering/PlaceholderSprites.cs:59](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Rendering/PlaceholderSprites.cs#L59) |

**代码已按 2×2 落地**：[MapGenRules.cs:56](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGenRules.cs#L56) `MineClusterSide = 2`（注释「对齐 mine.footprint 2×2，D613」）——矿山锚点**成 2×2 簇生成**，与 feat_mine 2×2 一致。

## 三、文档冲突（须策划端钉）

| 文档 | 口径 | 冲突 |
|------|------|------|
| 美术规范 §二（[L96](file:///c:/Users/trs/Desktop/Valley%20Rampart/河谷防线开发计划书具体内容/改造计划/美术资源规范_等轴立方体瓦片.md#L96)） | `feat_mine` 矿洞 **2×2**（两格地皮+32px） | ← 与右冲突 |
| **3.1.1 §8.2**（[L335-336](file:///c:/Users/trs/Desktop/Valley%20Rampart/河谷防线开发计划书具体内容/3.1.1_美术资源清单.md#L335-L336)） | 矿洞入口「**占地：1格**」 | **旧口径**（未随 D613 2×2 同步） |
| 3.1.3 §六 批3（[L16](file:///c:/Users/trs/Desktop/Valley%20Rampart/河谷防线开发计划书具体内容/3.1.3_美术资源生产排期.md#L16)） | `feat_ 10（山/雪山/4树/矿洞/矿脉/石堆/木堆）`——**未标占地** | 无冲突但口径不明 |

## 四、矿山锚点多态需求（3.1.1 §8.2，与 D641 同族但未覆盖）

3.1.1 §8.2 列矿山锚点状态：**贫瘠（更小碎石多）/ 普通（山体侧面洞穴入口）/ 富有（洞口矿晶光芒 4 帧闪）/ 损坏态（木支撑断裂塌方）/ 修复中态（脚手架）**。

- 这与 D641 裁定的「资源点多态」同族（该裁覆盖树/矿脉/石堆/木堆，**未显式覆盖 feat_mine**）
- 请注意：**损坏/修复中态**属建筑生命周期语义（D534 单态口径），而锚点当前是**纯数据格**（不派生 Building 实体）——多态口径需明确

## 五、美术侧影响

- **补列**：美术清单「自然/资源」层补 `feat_mine` 矿山锚点 1 项（2×2，独立于矿场建筑）
- **张数**：随多态口径定档（若照 §8.2 全态，约 3~5 张；若随 D641 单/多态，按同口径）
- **画布**：2×2 基面 256×128（与矿场建筑同尺寸，但内容不同：锚点=山体+洞口，建筑=矿场设施）

## 六、待决策（请策划端裁）

| # | 决策项 | 选项 | 美术端建议 |
|---|--------|------|-----------|
| 1 | 确认 `feat_mine` 2×2 | 确认 / 改 1×1 | 确认（美术规范+代码 MineClusterSide 均 2×2） |
| 2 | 3.1.1 §8.2「占地 1 格」勘正 | 改 2×2 / 维持 | 改 2×2（对齐 D613/美术规范） |
| 3 | 矿山锚点是否纳入多态（照 §8.2 贫瘠/普通/富有/损坏/修复） | 纳入 / 随 D641 同口径 / 单张 | 请裁（注意损坏/修复中态=建筑生命周期 vs 锚点纯数据格） |
| 4 | 归属批确认 | 批3 自然建筑包 / 普通批 | 批3（3.1.3 §六 批3 已含「矿洞」） |

## 七、证据（实盘）

- `feat_mine` 定义 2×2：[Rendering/PlaceholderSprites.cs:43](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Rendering/PlaceholderSprites.cs#L43) / 美术规范 §二 L96
- MineClusterSide=2：[MapGenRules.cs:56](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGenRules.cs#L56)
- FeatureType.Mine 纯数据格（不派生实体）：[MapGenRules.cs:545-564](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGenRules.cs#L545-L564)（DeriveNaturalBuildings 仅 OreVein/WoodPile/StonePile）
- 3.1.1 §8.2 多态需求：L335-344
- D613（mine 2×2）/ D618·D621（矿山缺口件）/ D641（资源点多态）

---

## 八、追加核查：资源点等级/生长/树桩（用户 2026-09-11 指令，以用户口径为准）

> **用户口径（权威，2026-09-11）**：①树**不要生长阶段、不要树桩**，直接长出来（采完即消失/整体重生，不做生长帧）②树**不分贫瘠/富有档**，**每气候 3 张**（共 4 气候×3=12 张）③矿脉/石堆/木堆——**先查有无等级规定，有等级才做，没有就不做**④「木堆」改名**「枯木」**（更形象、更像自然生成资源）。

### 8.1 「资源点等级」核查结论：**字段存在，但地图不生成等级、视觉换图零实现 = 纸面有实际无**

| 层 | 实盘 | 结论 |
|----|------|------|
| 枚举 | `ResourceGrade { Barren(×0.5), Normal(×1.0), Rich(×2.0) }`（[GridTypes.cs:165-171](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Grid/GridTypes.cs#L165-L171)） | 定义存在 |
| 资产 | tree/ore_vein/stone_pile/wood_pile 均有 `gradeScale [0.7, 1, 1.5]`（BuildingDef 字段） | 数值字段存在 |
| **消费** | `gradeScale` **只缩放 producer.rate + maxHp**（[BuildingDef.cs:91-92/114-118](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/BuildingDef.cs#L114-L118)），**无换图/选 artId 逻辑** | **视觉变体零实现** |
| **生成** | 自然建筑派生一律 `grade: ResourceGrade.Normal`（[BuildingFactory.cs:145](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L145)/[KingdomFoundry](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomFoundry.cs)）；地图从不赋 Barren/Rich | **等级从不生成** |
| 树的 artId | `feat_tree_*` 全仓零消费（见 HH.177） | **不做换图** |

→ **结论（支持用户口径）**：资源点等级**仅是缩放系数的纸面字段，玩家永远看到的是「普通」**；视觉上没有贫瘠/富有档可换。**故矿脉/石堆/木堆不该做 3 档**——按用户口径「没有等级就不做」＝**各 1 张**。

### 8.2 「树木生长/树桩」核查结论：**生长帧无实现，树桩无实现（用户砍掉正确）**

| 项 | 实盘 | 结论 |
|----|------|------|
| 树木重生 | `ResourceRespawnSystem` + `TreeGatherSource`（[TreeGatherSource.cs:55-60](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/TreeGatherSource.cs#L55-L60) / [RespawnConfig.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/RespawnConfig.cs)）：采完 feature→Plain，隔 `treeRespawnDays` **整棵重生** | 有「整树重生」，**无生长阶段** |
| 生长阶段贴图（树苗→小树→成树） | 3.1.1 §8.1 列了（3.2.1 §6.9 出处），但**代码无 growStage/树苗/小树实现**，且 3.2.1 已归档 `_archive/` | **设计未落地，砍掉正确** |
| 树桩 | 3.1.1 §8.1 列「被砍倒后残留」，**代码零实现**（树采完直接 feature→Plain，不建树桩） | **设计未落地，砍掉正确** |

→ **结论（支持用户口径）**：树直接「长出来/采完消失/整树重生」＝**无生长帧、无树桩**，与代码现状一致。

### 8.3 冲突清单（文档 vs 用户口径，以用户为准）

| # | 文档规定（出处） | 用户口径 | 处置（以用户为准） |
|---|-----------------|---------|-------------------|
| 1 | 3.1.1 §8.1：树「自恢复=树苗→小树→成树 生长阶段贴图」+「树桩」 | 不要生长、不要树桩 | **文档勘误**：删 3.1.1 §8.1 这两行 |
| 2 | 3.1.1 §八 通用规则：「三档等级 贫瘠/普通/富有」；§8.1/§8.4/§8.5/§8.6 均列 3 档 | 树不分档（每气候 3 张）；矿脉/石堆/木堆无等级→各 1 张 | **文档勘误**：§八 三档规则对资源点作废（等级无生成+无换图，见 8.1） |
| 3 | 3.1.1 §8.5「木头堆」 | 改名「**枯木**」 | **文档勘误**：§8.5 改名枯木 |
| 4 | D641 裁「资源点全多态」（树12+树桩4+矿脉3+石堆3+木堆3+营地1=26） | 树12（无树桩）+矿脉1+石堆1+枯木1+营地1=**16** | **D641 部分修订**：去掉树桩4、矿脉/石堆/木堆降为各 1 张 |

### 8.4 修正后张数（以用户口径）

| 件 | 张数 | 说明 |
|----|------|------|
| 树 tree | **12** | 4 气候（棕榈/阔叶/针叶/寒带矮树）×3 张（外观变化，非等级档） |
| 矿脉 ore_vein | **1** | 无等级 |
| 石堆 stone_pile | **1** | 无等级 |
| 枯木（原木堆） | **1** | 无等级；改名 |
| 营地 VagrantCamp | 1 | 不变 |
| **自然/资源小计** | **16** | （D641 原 26 → 16，−10） |
| ~树桩~ | **删** | 用户砍 |
| ~生长阶段~ | **删** | 用户砍 |

### 8.5 待确认（用户口径下的一个开放点）
- 树「每气候 3 张」的 **3 张是什么关系**？用户说「不分富有程度」——故 3 张应为**同气候下的外观变体/朝向/大小差异**（非贫瘠/普通/富有）。请确认 3 张定义（外观微变 / 大小 / 随机池）。

---

## 策划裁决（策划端回写）

> **D660**（2026-09-11，主策划端）。**判据三直读已过**：①设计稿＝美术规范 §二 L96＋`3.1.1 §八`（§8.1~§8.6 原文）＋`3.1.3 §六 批3`；②代码 file:line＝`Rendering/PlaceholderSprites.cs:43`（feat_mine `(2,2,1)`）／`:59`（bld_mine_b `(2,2,1)`）／`MapGenRules.cs:56`（`MineClusterSide=2`）＋`:142/152-162/383`（Mine 成簇撒布）＋`:545`（DeriveNaturalBuildings）／`GridTypes.cs:165-171`（`ResourceGrade`）／`BuildingDef.cs:114-118`（gradeScale 只缩放 rate/maxHp）／`BuildingFactory.cs:145`（恒 Normal）／`TreeGatherSource.cs:55-60`＋`ResourceRespawnSystem`（整树重生）；③字段＝footprint/占地直读。

| 项 | 裁决 |
|----|------|
| 决策 1：feat_mine 2×2 | **确认 2×2**。三源一致：美术规范 §二 L96（2×2）＋`PlaceholderSprites.cs:43` `(2,2,1)`＋`MapGenRules.cs:56` `MineClusterSide=2`（注释「对齐 mine.footprint 2×2，D613」＋成簇撒布）。**锚点 `feat_mine` ≠ 矿场建筑 `bld_mine_b`**（`:59`，亦 2×2）——是两件事（HH.188 §二 结论成立）。 |
| 决策 2：3.1.1 §8.2 勘正 | **改 2×2**（原「**占地：1 格**」＝旧口径，未随 D613 同步）。策划端已勘正 `3.1.1 §8.2`。 |
| 决策 3：是否多态 | **裁：单张（不纳入多态）**。〔**破框点**：三选项「纳入／随 D641／单张」**共同前提＝「锚点应有多态差异」——前提已破**：①**等级（贫瘠/富有）**：`ResourceGrade` 仅缩放 rate/maxHp、**无换图逻辑**，地图恒 `Normal`（`BuildingDef.cs:114-118`／`BuildingFactory.cs:145`）⇒ **无等级可换**（同 §8.1 结论，合用户「没等级就不做」）②**损坏/修复中态**：属**建筑生命周期语义**（D534 单态），而 `FeatureType.Mine` 是**纯数据格、不派生 Building 实体**（`MapGenRules.cs:545` DeriveNaturalBuildings 仅 OreVein/WoodPile/StonePile）⇒ **锚点无生命周期**〕⇒ **单张**（普通：山体侧面洞穴入口）。`3.1.1 §8.2` 五态表**勘正为单张**。 |
| 决策 4：归属批 | **确认 批3 自然建筑包**（`3.1.3 §六 批3` 已含「矿洞」）。 |
| **决策 5（追加）：资源点等级/生长/树桩/枯木 按用户口径（§八）** | **采纳用户口径（§八），文档勘误 4 笔**（§8.1/8.2 实盘支撑，非纸面） |

**§8.5 开放点裁决（树「每气候 3 张」的关系）**：**＝随机外观变体池**——同气候下 3 张为**外观微变体**（**非**贫瘠/普通/富有、**非**大小档、**零逻辑差异**），供接入时**随机/按坐标哈希选一**，纯表现。**待办**＝**变体选择机制**（接入批新增轻量逻辑；映射表 §十.2 已标「变体选择机制待定」）。

**决策 5 的 4 笔文档勘误（策划端已落）**：①`3.1.1 §8.1` 删「自恢复（树苗→小树→成树 生长阶段贴图）」＋「树桩」两行（实盘无生长帧/树桩，仅整树重生）②`3.1.1 §八` L320 三档等级规则**对资源点作废**（等级纸面有实际无）③`3.1.1 §8.5` 木头堆 → **枯木**（改名）④**张数修正**：自然/资源 **D641 原 26 → 16**（树12〔4 气候×3〕+矿脉1+石堆1+枯木1+营地1；**去树桩 4**、矿脉/石堆/枯木各 1 张）；**§8.4 石堆／§8.6 矿脉 3 档 → 各单张**。

**附带勘正（本笔顺带，非原决策项）**：`3.1.1 §9.1 流浪汉营地「占地 4 格（4×1）」` 与 def `VagrantCamp footprint:{x:2,y:2}`（2×2，同 4 格、形状不同）冲突 ⇒ 勘正为 **2×2**（对齐代码；承 D659 营地=同 `feat_mine` 预设生成物口径）。

### 衍生产物
- **更新文档**：`3.1.1 §八`（§8.1 删两行／§8.2 五态→单张＋占地 2×2／§8.4·§8.6 3 档→单张／§8.5 改名枯木／L320 三档作废／§9.1 营地 4×1→2×2）
- **更新文档**：映射表 §十.2（⑦ 自然/资源 16 张；树 3 变体机制待办）
- **勘正**：D641「资源点全多态 26」→ **16**
- **待办（执行端/接入批）**：树 3 变体选择机制
