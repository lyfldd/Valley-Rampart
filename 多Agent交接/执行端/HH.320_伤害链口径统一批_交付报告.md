# `HH.320` · 伤害链口径统一批（批 1）· 交付报告

- **执行端**：TraeCode｜**日期**：2026-09-23
- ⚠️ **本轮性质 ＝ 第一段 · 方案报裁（`S1`／`S2`／`S3`）** ⇒ ⛔ **停手待裁**：**未施工任何生产码**（第二段 7 件一行未写）。
- **依据**：任务书 §九（`D844`）＋ §9.4（件 3′ 形状）/ §9.3 范围｜口径真源 `最高优先级文档/07_伤害层.md` §二 ＋ §七 判据 10~12｜修订说明 `改造计划/2_5_伤害管线与战斗.md` 头部 L2 修订块（`:9-17`）｜前置报告 `多Agent交接/执行端/HH.320_伤害链口径统一批_开片前实核_报告.md`（`aaa67eb6`）
- **基线对齐**：`git log --oneline -3` ⇒ **`f09d7e8f`**（HH.320 批1 件3′ 拍板 `D845`）← `b9f12bcd` ← `aaa67eb6` ⇒ 与任务书「基线 `f09d7e8f`」一致；⛔ 未 rebase／未 reset／未 push。
- **本轮写入面**：**`Assets/**` 零写**（无脚本落盘、无场景/资产改动、**未进 Play**）；仅 1 次**编辑器内只读微基准**（合成字典，**不落盘、不改资产**，见 S2）；未 `git add`（工作区脏点 78 项与开片前同，见 §三）。
- **编号**：沿任务书给定路径命名（批内系列），**未取新号**。

---

## §〇 · 方案报裁（三项 · ⛔ 不得凭猜 → 全部实读）

### S1 · 山壁的 `FeatureType` 取值清单（属「地形硬阻挡」者）

**结论：地形硬阻挡 = `{ FeatureType.Mountain, FeatureType.SnowMountain }`（**2 值**）。**

#### S1-1 枚举真源（9 值全列）

| 值 | 枚举注释（逐字） | 派生可走位 | 属「地形硬阻挡」？ |
|---|---|---|---|
| `Plain` | 可走/可建 | `TerrainWalkable` | ⛔ 否 |
| `Tree` | 一次性木（可刷新） | `TerrainWalkable` | ⛔ 否 |
| `Mountain` | **阻挡**（HH.272 件④：山脉化生成） | `WalkFlags.None` | ⭐ **是** |
| `SnowMountain` | **阻挡**（同上） | `WalkFlags.None` | ⭐ **是** |
| `Mine` | 矿山锚点（地形；其上可建 mine／quarry） | `TerrainWalkable` | ⛔ 否 |
| `OreVein` / `StonePile` / `WoodPile` | 一次性资源（**可走**） | `TerrainWalkable` | ⛔ 否 |
| `River` / `Ocean` | **水（阻挡）** | `Water` | ⛔ 否（`D844` 明文：水不挡视线） |

**取证**：`Valley Rampart/Assets/_Game/Systems/Grid/GridTypes.cs:77-86`（枚举原文）。

#### S1-2 派生映射（唯一两处，同集合自证）

| 落点 | 内容 | 锚点 |
|---|---|---|
| `GridSystem.FeatureToWalkFlags` | 可走 6 值 → `TerrainWalkable`／水 2 值 → `Water`／**`default: WalkFlags.None`**（＝仅 `Mountain`/`SnowMountain`，`:123` 注释自陈"山地/雪山阻挡（无 `TerrainWalkable` 位）"） | `GridSystem.cs:112-126` |
| `MapGenRules.IsWalkableFeature` | 可走 6 值 → `true`；`default: false`（`:95` 注释逐字「`Mountain/SnowMountain/River/Ocean` 阻挡」） | `MapGenRules.cs:88-97` |

⇒ 两处**唯一命中的"非可走且非水"值集合 = 两山**（枚举 9 值全枚举后穷尽，无第三值）。

#### S1-3 地图生成写入面（生产码 `Mountain`/`SnowMountain` 全路径）

| 角色 | 落点 | 说明 |
|---|---|---|
| ⭐ **唯一创建点** | `MapGenRules.StampMountain:759-766`（`:764` `GenesisWrite(..., rng < GetMountainSnowRatio(band) ? SnowMountain : Mountain)`） | 唯一 `Mountain`/`SnowMountain` 写点 |
| 唯一调用 | `MapGenRules.PlaceMountainRidges:747` | 脊线 ＋ 沿线扩宽（HH.272 件④「山脉化」） |
| 消减（生成期兜底） | `MapGenRules.PruneMountainSpecks:801`（4-连通簇 <`mountainClusterMinSize` ⇒ 回落 `Plain`）／`MapValidator.CarveCorridor:100-101`（连通兜底：非可走且非 `Ocean` ⇒ `Plain`，**含山体**）／`PlaceRiver:1455`（河**不覆盖**山/雪山） | 只减不增 |
| 生成期以外 | **零创建点** —— 生产码 `map.features[...] =` 唯一写内核 ＝ `MapGate.WriteRaw:33`（私有），其调用面枚举（`MapGate.GenesisWrite` / `SetFeature` / `ReplaceFeature` / `PlaceResourceNode` / `GenesisWriteGrade`）**无一处写两山** | `MapGate.cs:27-33／49-54／278-294／302-321／333-342`；生产码全扫 `.features[...]=` 仅 `MapGate.cs:33`（`:28-29` 自陈判据） |
| 运行期可用口（**判据 4 拆墙用**） | `MapGate.SetFeature:278`（通用字段变更 ＋ 派生重算） | ⇒ 判据 4「同位置拆掉山壁」走**正门**；⚠️ ⛔ 不照抄 `Valley_HH140_Probe.cs:132` 的裸写（Editor 探针旁路，生产码 0 命中） |

#### S1-4 建议判据式（两式今日等价，请裁取一）

| 式 | 写法 | 评价 |
|---|---|---|
| **(i) 显式枚举**（推荐） | `f == FeatureType.Mountain || f == FeatureType.SnowMountain` | 照 `MapGenRules.cs:775`／`:1455` 同款写法；**不依赖 `default` 兜底**；新增地形须显式入列（可审） |
| (ii) 派生位判定 | `(GetWalkFlagsSub(sub) & (WalkFlags.TerrainWalkable \| WalkFlags.Water)) == 0` | 今日**等价**；语义为"未知地形即挡" ⇒ 未来新增阻挡地形自动挡（双刃：也会自动挡"未定义"值） |

⇒ 本端建议 **(i) ＋ 开发期一致性哨兵**（断言两式同值），既不误挡水、也不静默漏挡。

---

### S2 · 工事沿路径查的实现形状（性能）

#### S2-1 ⚠️ 关键实读（令式前提与实盘不符）

任务书 §9.4-2 的性能硬约束写作「沿 Bresenham 逐微格查 `GridSystem.GetUnitsInSubCell`」⇒ 隐含**该口是 O(1) 反查**。**实盘不是**：

```500:506:Valley Rampart/Assets/_Game/Systems/Grid/GridSystem.cs
    public List<UnitController> GetUnitsInSubCell(GridCoord sub)
    {
        var result = new List<UnitController>();
        foreach (var kv in _unitSubCells)
            if (kv.Value == sub) result.Add(kv.Key);
        return result;
    }
```

⇒ 该口 = **全表枚举（`Dictionary<UnitController, GridCoord>`）＋ 每次 `new List`** ⇒ **O(N) ＋ 一次分配 / 次**，与"全库遍历"同量级（同族前例：`PerceptionSystem.cs:10-11` 记「`GetUnitsInCell` 每次调用 O(N)/格，方格遍历不可接受」；`MapGate.cs:190-196` 记「`GetUnitCountInCell` O(N)」）。
⇒ **令式若照字面落地，代价 ＝ O(N) × 路径微格数 L**，比"一次全库遍历 × 候选数"**更差 L 倍**。

**微基准（本轮实测 · Editor 内 · 合成表 N=350 · 20000 次）**：

| 基准 | 实测 | 说明 |
|---|---|---|
| `GetUnitsInSubCell` 等效（全表枚举 ＋ `new List` ＋ `GridCoord` 比较） | **19,045.6 ns / 次**（≈ **19.05 µs**） | 与任务书担心的 `UnitRegistry` 全库遍历同量级 |
| 反查索引 `TryGetValue(GridCoord key)` | **300.2 ns / 次**（≈ **0.30 µs**） | 索引形状（键改 `int` 子格序号可再降） |

> ⚠️ 基准为**合成字典**（非实盘单位表，N 取 `PerceptionSystem.cs:12` 所记「活跃带 300~400 单位」），用于**量级判断**；正式施工后按判据 11 给实盘读数。

**路径长度 L 口径（轴向 `R` 格 ⇒ `L = R × subCellDivisor = 4R`；由 `GridSystem.cs:177-178` 等轴换算 ＋ 子格 (0.32,0.16) 推出）**：`R=6`（弓箭手）⇒ `L=24`；`R=9`（弩手／箭塔单位 7／魔法塔 8）⇒ `L=36`。

**单次索敌耗时估算（外推 · N=350 · L=24）**：

| 形状 | 单次视线判定 | 候选 1 | 候选 5 | 候选 10 |
|---|---|---|---|---|
| **A · 照令式（逐微格 `GetUnitsInSubCell`）** | 24 × 19.05 µs = **457.1 µs** | 0.46 ms | **2.29 ms** | **4.57 ms** |
| **B · 增量反查索引（推荐）** | 24 × 0.30 µs = **7.2 µs** | 7.2 µs | 36 µs | 72 µs |
| **C · 每 tick 一次收集（备选）** | ≈ **19 µs / tick**（一次 O(N) 收集，候选循环内复用） | — | — | — |

**对照读数（现役）**：`PerceptionSystem.QueryNearby` 单遍 O(N)（N=350）本身 ≈ 19 µs/次（同规模单遍）⇒ **形状 A 相当于把每次索敌放大 L≈24 倍**；形状 B 的视线开销与"现役感知一次单遍"同量级。

#### S2-2 推荐形状 B（增量反查索引）——具体形状

```text
① GridSystem 新增（唯一真源仍是既有 _unitSubCells）
   Dictionary<int, List<UnitController>> _unitSubIndex;      // 键 = ToSubIndex(sub)（int，避免 struct 键哈希）
   → TryEnter:481-491          ：旧格摘除 + 新格追加（跨微格才发生，UnitController.cs:1362-1374 已保证同格零调用）
   → ExitCurrentCell:493／RemoveUnit:495 ：摘除
   → ClearAll:128-137           ：清表
② 新增只读判据口（零分配）
   bool IsSightBlockedByFortUnit(GridCoord sub)
      → 命中列表内任一 unit.fortification != null ⇒ true
③ CombatRules.HasLineOfSight 内：逐微格 O(1) 三次位/表查询（山壁／建筑占格／工事单位）
```

- **改动面**：`GridSystem.cs` 4~5 处（字段 ＋ 3 个写口 ＋ `ClearAll`）＋ 新增 1 个只读口；⛔ 不动 `GridMath.DistCells`、⛔ 不动寻路/派工语义（`_unitSubCells` 保持真源，索引只是镜像）。
- **代价**：单次 LOS **7.2 µs**（实测 0.30 µs/次 × L=24）＋ 零 GC（判据 11 读数用）。

#### S2-3 备选形状 C（零结构改动 · 若策划端不接受动 `GridSystem`）

每 tick **一次** O(N) 收集「工事单位子格」到 `HashSet<int>`（19 µs，与现役感知单遍同量级）⇒ 候选循环内逐微格 O(1) 查。
⛔ **硬约束：不得"每候选一次"**（那会回到 `19 µs × 候选数`）；⛔ 不得"每次 LOS 一次"（= 457 µs 的另一种写法）。

#### S2-4 形状 A（照令照做）——仅兜底

若策划端判定"不得动 `GridSystem` 结构、且接受 2.29~4.57 ms/次索敌"，可按字面落地；本端 ⛔ **不建议**（同批另有 `PerceptionSystem` O(N) 单遍仍在 ⇒ 会叠成双倍全库扫）。

#### S2-5 阻挡集判定式（供件 3′／件 4 共用 · ⛔ 唯一口）

| # | 阻挡类 | 判据（实读来源） | 代价 |
|---|---|---|---|
| ① | **山壁** | `GetFeatureAt(SubToCell(sub)) ∈ {Mountain, SnowMountain}`（S1） | O(1) |
| ② | **建筑占格（含废墟）** | `(GetWalkFlagsSub(sub) & WalkFlags.BuildingBlocked) != 0`（`GridSystem.cs:286-290` 子格读口；废墟由 `Building.EnterRuined:1299-1328` 保留占格 ⇒ 天然含，`D844` §9.1-3） | O(1) |
| ③ | **工事单位** | 见 S2-2／S2-3 | O(1)（B/C） |
| ⛔ | 不挡 | `Water`（河/湖/海）／`Locked`（工地）／`Bridge` —— **本函数不读这三类** | — |

#### S2-6 请裁（S2 待确认 3 项）

1. **形状三选一**：B（推荐 · 改 `GridSystem` 4~5 处）／C（零结构改动 · 每 tick 一次全扫）／A（照令 · 2.29~4.57 ms/次）。
2. **③ 的判定式**：`fortification != null` 全含（含 `blocksMovement=0` 的**三塔**，实测 `ArrowTower/CrossbowTower/MagicTower` `heightCells=3`）—— 与 `D844`「工事单位」字面一致，本端按**全含**准备。
3. **城门开态**：`Gate.asset` `passable: 1`（可通行）⇒ ⭐ 本端按**照挡视线**处理（实体在场；与现役 `CheckWallBlock` 亦不看 `passable` 状态一致）；若策划端要求"开门不挡请显式裁"。

#### S2-7 附注（同族现役代价 · ⛔ 不在本批范围）

`ProjectileManager.QueryNearbyUnits:334-353`（命中检测）**已在用**"逐微格 `GetUnitsInSubCell`"：每次弹到达 `(2×1+1)²=9` 微格 ⇒ ≈ **171 µs/发**（同基准外推）。本批不改；**登记为性能观察项**，供策划端决定是否另批（同形状问题，⛔ 不夹带进本批）。

---

### S3 · 件 1 微格候选超集换算方案（`subRange`）

#### S3-1 现状实读（三份同款拷贝 · 精确行号）

| # | 文件 | `subDiv` 读取行 | `subRange` 计算行 | 现式 |
|---|---|---|---|---|
| 1 | `ProjectileManager.cs` | `:339` | **`:343`** | `subRange = Mathf.Max(0, Mathf.CeilToInt(radiusCells * subDiv));` |
| 2 | `DamageSystem.cs` | `:486` | **`:490`** | 同式（`QueryUnitsInRadius`；消费者 庇护 `:429`＝2f 预筛／溅射 `:463`=`aoeRadiusCells`） |
| 3 | `GroundEffectManager.cs` | `:198` | **`:202`** | 同式（`QueryUnitsInRadius`，消费者 `:212`） |

> 注：任务书所给 `ProjectileManager:339 / DamageSystem:486 / GroundEffectManager:198` ＝ **`subDiv` 读取行**；`subRange` 计算行分别为 `:343 / :490 / :202`（两处一并列，防锚点漂移）。

**实读参数**：`GridConfig.asset:15 cellSize = (1.28, 0.64)`；`:16 subCellDivisor = 4` ⇒ 子格 **`(subW, subH) = (0.32, 0.16)`**（`GridSystem.cs:140-143`）。
**现状取值**：`radiusCells = 0.25`（`DamageConfig.cs:35 hitRadiusCells = 0.25f`，Tooltip 自陈"格单位，2_5 决议 D1 占位 0.25，逻辑上等效微格级"）⇒ `subRange = ⌈0.25×4⌉ = 1`。

#### S3-2 新口径公式（通用式 ＋ 本配置闭式）

设判定半径的**视觉格**值为 `R_vis`（＝ `hitRadiusCells` / `aoeRadiusCells` / `effectRadiusCells` / `shelterRadiusCells` / `profile.range` 改走 `DistVisual` 后的同一个量）：

```
R_world   = R_vis × CellDiagHalf                     // CellDiagHalf = 0.5·√(CellW²+CellH²) = 0.7155（件 1 唯一口）
subW      = CellW / subDiv ,  subH = CellH / subDiv   // 现配置 (0.32, 0.16)
n         = ⌈ R_world × √( 1/subW² + 1/subH² ) ⌉      // 通用式（柯西–施瓦茨，紧确）
          = ⌈ R_vis × subDiv × (CellW²+CellH²) / (2·CellW·CellH) ⌉
          = ⌈ 5 × R_vis ⌉                              // ★ 本配置闭式（subDiv=4 · 2:1 等轴）
```

- **本批取值**：`R_vis = 0.25` ⇒ `n = ⌈5 × 0.25⌉ = ⌈1.25⌉ = **2**`（现状 `1` ⇒ ⭐ **由 1 改 2**）✅ 与任务书 §9.3 预期一致。
- **更松的同值变体（三角不等式）**：`n' = ⌈R_world × (1/subW + 1/subH)⌉ = ⌈0.178875 × 9.375⌉ = ⌈1.677⌉ = 2`（同值，可作交叉校验）。
- **参数来源**：`subW/subH` 由 `Config.cellSize / Config.subCellDivisor` **现算**（⛔ 不写死 `0.32/0.16`）；`CellDiagHalf` 与 `DistVisual` **同源取用**（⛔ 不得第二份常数）。

#### S3-3 超集证明

**目标**：保证"任何满足 `DistVisual(落点, 单位) ≤ R_vis` 的单位"必落在 `(2n+1)²` 候选窗内（否则漏命中 ＝ 假阴性）。

1. `DistVisual ≤ R_vis` ⇒ 世界距离 `|Δ| ≤ R_world`（`DistVisual` 定义，件 1）。
2. 子格连续坐标（`GridSystem.cs:184-189`）：`gx = Δx/subW + Δy/subH`、`gy = Δy/subH − Δx/subW`（两轴系数对称）。
3. 对 `gx` 取上确界（柯西–施瓦茨，**线性式在圆盘上的最大＝半径×系数范数**）：
   `sup |Δgx| = R_world × √(1/subW² + 1/subH²)` = `0.178875 × 6.9877 = 1.25007`；`gy` 同式（系数集相同）。
4. 子格**索引差** ≤ `⌈ sup |Δg| ⌉` = `⌈1.25007⌉` = **2** ⇒ 取 `n = 2` 必覆盖 ✅（每轴索引差 ∈ {0,1,2}）。
5. **反例自证（现状为何不是超集）**：现状 `n=1`；取极值方向（位移平行于系数向量 `(1/subW, 1/subH)`）⇒ `|Δgx| → 1.25 > 1` ⇒ 当落点子格小数部分 ≈ 0.99 时单位子格索引差可达 **2 > 1** ⇒ **圈内却不进候选 ⇒ 漏命中**（旧口径下同理：`sup|Δgx|(格单位椭圆) = ⌈1.414⌉ = 2`，现状 1 亦不足）。
6. **无假阳性**：候选窗只扩大候选集，最终命中仍按 `DistVisual ≤ R_vis` 过滤（`ProjectileManager.cs:232/256`、`DamageSystem.cs:301/471-472`、`GroundEffectManager.cs:212`）⇒ 仅"多查 16 个微格"（3×3→5×5），不改判定结果。

#### S3-4 施工建议（随件 1/2 同批）

- 三处拷贝**收口到单一口**（建议 `GridMath.SubRangeForVisualRadius(float rVis)` 或 `GridSystem` 侧静态口，内部读活配置）⇒ 否则本批后会出现 3 份不同口径。
- ⚠️ **各调用点半径语义须同步**：`hitRadiusCells`（0.25）改视觉格；建塔/单位 `range`/`attackRange` 数值**不变**（件 1 铁律）但**量纲解释**随 `DistVisual` 变；`DamageSystem` 的庇护**预筛窗 `:429` 传 `2f`**（＝2 视觉格 ＝ 1.43 世界）**仍 ≥ 判定半径 `shelterRadiusCells`（1 视觉格 ＝ 0.7155 世界）** ⇒ 预筛超集性保持 ✅（须在报告给出该比值）。
- **代价**：每发到达候选微格 `9 → 25`（多 16 次 O(1) 表查；⚠️ 若按 S2-1 的现役 `GetUnitsInSubCell` 实现（O(N)＋分配），则为多 16 × 19.05 µs ≈ 305 µs/发 ⇒ **这是 S2 形状选择的连带影响，须与 S2 同裁**）。

---

## §一 · 本轮动作清单（全只读 ＋ 1 次编辑器内微基准）

| # | 动作 | 产物/证据 |
|---|---|---|
| 1 | 任务书／口径真源／修订块／前置报告 全读（含 §九 `D844` ＋ §9.4 `D845`） | 本报告 §〇 |
| 2 | `FeatureType` 枚举 ＋ 两处派生映射 ＋ 生成写入面全扫（S1） | `GridTypes.cs:77-86`／`GridSystem.cs:112-126`／`MapGenRules.cs:88-97/759-766`／`MapGate.cs:27-33` |
| 3 | `GetUnitsInSubCell`／`QueryUnitsInRadius` 三份／`PerceptionSystem` 实现实读（S2/S3） | `GridSystem.cs:500-506`／`ProjectileManager.cs:334-353`／`DamageSystem.cs:481-501`／`GroundEffectManager.cs:193-218` |
| 4 | 编辑器内微基准（合成字典 N=350 × 20000 次 · **不进 Play · 不落盘 · 不改资产**） | 读数见 S2-1（19,045.6 ns ／ 300.2 ns） |
| 5 | 工作区脏点核对（⛔ 不碰 `GameScene.unity`／`Packages`／`TaskScheduler.cs`／美术等既有脏点） | 见 §三 |

---

## §二 · 待放行（第二段 · 7 件 · ⛔ 未动一行）

件 1 换算分域（`GridMath.DistVisual` ＋ `CellDiagHalf`）｜件 2 换算统一 13 处｜**件 3′ 目标选择视线接线**（落点 4 处 ＋ 判据 8 条）｜件 4 阻挡集（唯一口 ＝ `CombatRules.HasLineOfSight` 改造后）｜件 6 菱形底座｜件 7 随机源（`R4`）＋ 判据 1~17。
⇒ **等策划端对 §〇 `S1`／`S2`（含 3 项待确认）／`S3` 的裁定后开工。**

---

## §三 · 红线遵守（本轮）

- ⛔ **`Assets/**` 零写**（无新脚本、无 `.meta`、无场景/资产改动、无 Play 进入）；⛔ 未动 `AI.Core`／训练仓；⛔ 未碰 `GameScene.unity`／`Packages`／`pixel-forge`／美术资源等既有脏点。
- ⛔ 未 `git add`、未 commit、未 push、未 rebase／reset。
- ⛔ 未改 `GridMath.DistCells`（仅读）；⛔ 未改 `range`／`attackRange` 数值；⛔ 未碰 `M3` 本体三项与 `DZ-4`。
- 工作区 `git status --porcelain` 条目数 **78**（＝开片前既有脏点，含 `GameScene.unity`／`TaskScheduler.cs`／`Packages`／美术 8 图／文档若干 ＋ 未跟踪目录）——**本轮零新增**。

---

## §四 · 供策划端裁定时参考的两条口径问题（本端实读所得）

1. **`DistCells` 在伤害链的调用点实数 ＝ 6，非 3**：`DamageSystem.cs:301／:436／:471` ＋ `ProjectileManager.cs:232／:256` ＋ `GroundEffectManager.cs:212`（`MonsterAI.cs:203-206` 为**自建私有同名方法**，不计入 `GridMath.DistCells`）。
   ⇒ 若按 §9.1-5（庇护/溅射/地面效果一并纳入）＋ 判据 8 全改，则 **`GridMath.DistCells` 减 6**（§9.1-4 记"−3"）。**请裁**：判据 8 的预期减量口径按 **−6** 记录（本端建议），还是只改 3 处。
2. **判据 11「单次索敌耗时」的判据载体**：建议明确为「LOS 判定耗时（µs）＋ 候选数 ＋ 路径微格数 L」三列（本报告 S2-1 已给估算基线；施工后给实盘读数）。
