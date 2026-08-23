# 2_4 LOD 区块划分 实施计划

> 日期：2026-08-14
> 状态：**主体已落地（2026-08-20，caa8dd84）**——LODSystem 重写为 2D 中区块（4×4 稀疏 Dictionary）+ 多中心活跃带 + 热度扩散 + LodConfig SO；原头部"待实施"为滞后标记（2026-08-23 对账修正）；D77~D80 已随实现兑现；Play 冒烟（活跃带跟随/热点扩散/降档迟滞）待补
> 前置：**doc 1 必须已落地**——中区块索引（`CellToMidChunk` 返回 Vector2Int）、`EnemyEnteredChunkEvent`、Chunk 分区
> 目标：1D LOD 从 region（大区块）→ 2D 中区块（4×4 小区块）；活跃带 N×N 中区块多中心；热度体系（中区块扩散/衰减）；区块稀疏登记
> 边界：本片**不重写 AI 决策**（归 2_7/2_8）、**不重写成本场**（归 2_6）；只做 LOD 分区/热度/降频档位
> 旧数据源说明：任务指定 `Systems/World/ChunkManager` 在旧库**不存在**；旧 LOD 实现在 `Systems/AI/LOD/LODSystem.cs` + `RegionLodState.cs`，旧调参在 `Data/AttentionTuningConfig.cs`（LOD 区段 + heat 区段），据此迁移

---

## 零、前置检查（不满足则停）

- [ ] doc 1 已落地：`CellToMidChunk(GridCoord) → Vector2Int`（`coord / midChunkSize`）、`CellToChunk`、`EnemyEnteredChunkEvent`、`GetUnitsInCell` 微格聚合
- [ ] 旧 LOD 基线确认：`LODSystem.cs` 为 region 粒度（`_regions`/`_midHeats` 一维列表），`AttentionTuningConfig` 持有 lod/heat 字段
- [ ] `NPCBrain` 现从 `GetLevelAt(worldPos)/GetThinkHz` 读 Think 频率（改签名时同步适配）
- [ ] 确认活跃带中心来源：玩家视角焦点（上帝视角锚点，2_8 提供）+ 战斗热点（本片热度体系产生，D77）
- [ ] 明确：休眠档 NPC **移动仍每帧**（D79），LOD 只降 Think 频率，不冻结

---

## 一、实施步骤（按依赖序，每步可独立验收）

### 步骤 1：LOD 状态类型迁移（region → 中区块）

**文件**：`Systems/AI/LOD/RegionLodState.cs`（改造，或新建 `MidChunkLodState.cs`）

**旧定义迁移**（`RegionLodState.cs` 全部类型）：
```csharp
// 旧（region 粒度）：
public enum LodLevel { Active, SemiActive, Sleeping }   // Sleeping 改名 Dormant
public class RegionLodState
{
    public readonly int RegionIndex;
    public LodLevel Level = LodLevel.Sleeping;
    public float IdleTimer;
}
public class MidRegionHeat
{
    public readonly int MidIndex;
    public float ThreatHeat;
    public Vector2 CombatHotspot;
    public float HotspotTime;
}
```
**删除**：`RegionLodState`、`MidRegionHeat` 两个独立类（热度并入新状态）。

**新定义**（设计 §5.1，中区块粒度 + 热度合并）：
```csharp
public class MidChunkLodState
{
    public Vector2Int midChunk;       // 中区块坐标 (x/4, y/4)
    public LodLevel level;            // Active / SemiActive / Dormant
    public float threatHeat;          // 0..heatMax
    public float idleTimer;           // 无活动累计（降档迟滞用）
    public long lastActivityTick;
    // 旧 MidRegionHeat 的 CombatHotspot/HotspotTime 合并进本类（或保留字段）
    public Vector2 combatHotspot;     // 最近战斗热点（受击位置，供支援）
    public float hotspotTime;         // 热点时间戳（maxAge 失效）
}

public enum LodLevel { Active, SemiActive, Dormant }   // 旧 Sleeping → Dormant
```

**验收**：编译通过；`LodLevel.Dormant` 替代 `Sleeping`；旧 `RegionLodState/MidRegionHeat` 无残留引用。

### 步骤 2：LODSystem 中区块化 + 稀疏登记（D80）

**文件**：`Systems/AI/LOD/LODSystem.cs`（重写）

**旧成员处置表**（逐项迁移）：
| 旧成员 | 处置 | 说明 |
|--------|------|------|
| `_regions: List<RegionLodState>` | **换 `Dictionary<Vector2Int, MidChunkLodState>`** | 稀疏存储（D80）：只持有"活跃带附近 + 有热度"的中区块；其余默认休眠不落状态 |
| `_midHeats: List<MidRegionHeat>` | 并入 `MidChunkLodState` | 热度字段合并 |
| `_armyCenters: List<Transform>` | 换中心列表 | `ActiveCenters`（多中心，≤8）；来源=玩家视角焦点 + 战斗热点（D77） |
| `RegionCount / MidRegionCount` | 换 `ActiveCenters` | 多中心列表只读 |
| `InitRegions(int regionCount)` | 换 `OnMapGenerated` 后懒登记 | 不再预建全量 4096 状态（R1）；中区块**按需登记**（TryEnter 跨界/热度注入时） |
| `GetMidCellCount()` | 删除 | 从 `GridConfig.midChunkSize` 读 |
| `ApplyActiveBands()` | 重写 | 2D 多中心活跃带（N×N 中区块，切比雪夫） |
| `ApplyCenterBand(int centerRegion)` | 重写 | 中心 = `Vector2Int` 中区块坐标 |
| `ApplyDowngrade()` | 改造 | 降档迟滞 3s（demoteDelaySeconds，原 30s） |
| `RegionTotalHeat` | 删除 | 中区块级直查热度 |
| `OnUnitDamaged` / `OnEnemyEnteredRegion` | 改造 | 事件改 `EnemyEnteredChunkEvent`；热度注入 `RegisterHeatEvent` |
| `UpgradeImmediate(midIdx, level)` | 改造 | 中区块坐标直升活跃 |
| `GetRegionOf / GetMidRegionOf(worldPos)` | 换 `GetLevel(cell)` | 由 `WorldToCoord → CellToMidChunk` |
| `GetHeatAt(worldPos)` | 保留签名 | 改 `GetHeatAt(cell)` |
| `TryGetCombatHotspot / TryGetNearestCombatHotspot` | 改造 | 中区块坐标查询，供 2_7 支援 |

**新 API（设计 §5.2）**：
```csharp
public class LODSystem : Singleton<LODSystem>
{
    public LodLevel GetLevel(GridCoord cell);        // 小区块 → 所在中区块的 LOD 级
    public float GetThinkHz(GridCoord cell);          // 读 Think 频率（10/2/0.5）
    public float GetHeatAt(GridCoord cell);           // 中区块热度
    public bool TryGetNearestHotspot(GridCoord cell, float maxAge, out Vector2Int hotspot);
    public IReadOnlyList<Vector2Int> ActiveCenters { get; }   // 多中心列表（≤8）
    void OnActivityCenterChanged();                   // 中心集更新（主城/焦点/战斗起止）
    void RegisterHeatEvent(GridCoord at, HeatSource src, float amount);  // 战斗/敌入/友撤
}
```

**验收**：256² 地图（64×64=4096 中区块）常驻状态数 = 活跃中区块数 + 热点数（**不建全量 4096**）；`GetLevel/GetThinkHz/GetHeatAt` 按中区块坐标查询正确。

### 步骤 3：活跃带判定（2D 多中心 N×N 中区块）

**文件**：`Systems/AI/LOD/LODSystem.cs`（实现）

**活跃带规则（设计 §5.3）**：
```
中心集 = { 主城/玩家焦点中区块（上帝视角锚点，D77） } ∪ { heat > hotspotThreshold 的中区块 }（取热度前 8，D3）
对每个中区块：
  d = 到最近中心的切比雪夫距离（中区块数，Chebyshev）
  d ≤ activeRadius        → Active(10Hz)
  d ≤ semiActiveRadius    → SemiActive(2Hz)
  其余                    → Dormant(0.5Hz)
降档迟滞：idleTimer ≥ demoteDelay(3s) 才降；升档即时
```
- 活跃带 = N×N 中区块：R=1 → 3×3 中区块 = 12×12 格；R=2 → 5×5 中区块 = 20×20 格（覆盖一次中型战斗，SO 可调，D1）
- 多中心合并：区块级取 max（任一中心覆盖即升档），不逐单位算（R4）
- 升档即时、降档迟滞（R6 防热点抖动档位抖动）
- 价值冲突（矿洞抢矿）也产热注入（D78）：`RegisterHeatEvent(at, HeatSource.ValueConflict, amount)`

**验收**：玩家焦点移动 → 活跃带跟随（Gizmos 绿区移动）；两处同时战斗 → 两个热点都活跃（多中心）；战斗结束 3s 后降档（迟滞）。

### 步骤 4：热度体系（中区块扩散/衰减）

**文件**：`Systems/AI/LOD/LODSystem.cs`（实现）

**旧字段迁移**（`AttentionTuningConfig` heat 区段 → 迁入新 `LodConfig`）：
| 旧字段（AttentionTuningConfig） | 旧默认 | 新处置（LodConfig） |
|--------|--------|------|
| `lodActiveThinkHz` | 10 | `activeHz = 10` |
| `lodSemiThinkHz` | 2 | `semiHz = 2` |
| `lodSleepThinkHz` | 0.5 | `dormantHz = 0.5` |
| `lodActiveRadius` | 1 | `activeRadiusMidChunks = 1`（中区块） |
| `lodSemiRadius` | 2 | `semiActiveRadiusMidChunks = 2`（中区块） |
| `lodDowngradeIdleTime` | 30 | `demoteDelaySeconds = 3.0` |
| `heatHitGain` | 0.4 | `heatHitGain = 0.4` |
| `heatEnemyEnterGain` | 0.2 | `heatEnemyEnter = 0.2` |
| `heatAllyRetreatGain` | 0.05 | `heatAllyRetreat = 0.05` |
| `heatDecayRate` | 0.05 | `heatDecayRate = 0.05` |
| `heatSpreadThreshold` | 0.6 | `heatSpreadThreshold = 0.6` |
| `heatSpreadRatio` | 0.4 | `spreadRatio = 0.4` |
| 新增 | — | `heatMax`（clamp 上限）、`hotspotThreshold = 0.3`、`maxCenters = 8` |

**热度算法（设计 §5.3）**：
```
事件注入：RegisterHeatEvent → heat += heatHitGain/heatEnemyEnter/heatAllyRetreat（SO）
扩散 tick(1Hz)：heat > heatSpreadThreshold 的中区块向 4 邻溢 spreadRatio×heat
衰减 tick(1Hz)：heat -= heatDecayRate×dt，clamp [0, heatMax]
```
- 热度扩散性能（R2）：扩散 tick 1Hz（非每帧）；限扩散半径 2 圈；异步可选
- 热度事件源：战斗受击（`EnemyEnteredChunkEvent`/受击事件）、敌入、友撤、价值冲突（D78）

**验收**：造一场战斗 → 热点中心出现，热度向邻中区块扩散可见（Gizmos 色块）；热度衰减 clamp [0, heatMax]；事件注入量符合 SO。

### 步骤 5：区块登记（TryEnter 跨界继承 + 懒登记）

**文件**：`Systems/AI/LOD/LODSystem.cs`（实现）+ `Systems/Grid/GridSystem.cs`（TryEnter 联动，改动）

- **懒登记**：中区块状态在 `TryEnter` 跨界 / `RegisterHeatEvent` 时才创建（D80 稀疏存储）
- **跨中区块移动继承新状态（F6）**：`GridSystem.TryEnter` 跨 Chunk 检测时，同步刷新单位所在中区块的 LOD 登记（读新状态；旧状态可留可回收）
- 事件发布：`EnemyEnteredChunkEvent`（doc 1 改名）由 TryEnter 发布 → LODSystem 订阅 → `RegisterHeatEvent` + 升档
- 登记表：`Dictionary<Vector2Int, MidChunkLodState>` + 活跃中心列表，交叉维护

**验收**：单位跨中区块移动后 `GetLevel` 返回新中区块状态；懒登记不建全量状态；`EnemyEnteredChunkEvent` 驱动热度注入。

### 步骤 6：Gizmos 热度/LOD 可视化

**文件**：`Systems/AI/LOD/LODSystem.cs`（新增 OnDrawGizmos）

- 中区块染色：活跃绿 / 半活跃黄 / 休眠灰（只画摄像机视野内）
- 热度叠加红色透明度；中心集画星标
- 调试开关走 `LodConfig.drawGizmos`

**验收**：活跃带绿区随焦点移动；热点中心星标 + 热度红色可见；视野外不绘制。

---

## 二、涉及文件清单

| 文件 | 改动 |
|------|------|
| `Systems/AI/LOD/RegionLodState.cs` | 改造：LodLevel（Sleeping→Dormant）、新增 MidChunkLodState（含热度字段）、删除 RegionLodState/MidRegionHeat |
| `Systems/AI/LOD/LODSystem.cs` | 重写：中区块化、稀疏 Dictionary 登记、多中心活跃带、热度扩散/衰减、区块懒登记、Gizmos |
| `Systems/Grid/GridSystem.cs` | 改动：TryEnter 跨 Chunk 时刷新中区块登记（F6） |
| `Systems/AI/NPCBrain.cs` | 适配：`GetLevelAt(worldPos)` → `GetThinkHz(cell)`（从所在中区块读频率） |
| `Systems/AI/Formation/FormationBrain.cs` | 适配：热度输入从大区块 → 中区块（`GetHeatAt(cell)`） |
| `Systems/AI/Debug/AIDebugSpawnController.cs` | 适配：调试面板区块索引改 Vector2Int 中区块坐标 |
| `Data/AttentionTuningConfig.cs` | 迁移：lod/heat 区段字段移除（迁入 LodConfig）；保留 speedChaseBoost 等其余字段 |
| `Data/LodConfig.cs` | **新增**：SO 字段（见三） |
| `Resources/Config/LodConfig.asset` | 新增资产 |

---

## 三、SO 配置

**LodConfig（新增，字段从 AttentionTuningConfig 迁移 + 新加）**

| 字段 | 类型 | 默认 | 说明 |
|------|------|------|------|
| activeRadiusMidChunks | int | 1 | 活跃半径（中区块，切比雪夫）→ 3×3 中区块 = 12×12 格 |
| semiActiveRadiusMidChunks | int | 2 | 半活跃半径（中区块）→ 5×5 中区块 = 20×20 格 |
| activeHz | float | 10 | 活跃 Think 频率（Hz） |
| semiHz | float | 2 | 半活跃 Think 频率 |
| dormantHz | float | 0.5 | 休眠 Think 频率（移动仍每帧，D79） |
| demoteDelaySeconds | float | 3.0 | 降档迟滞（原 30s 缩短，防档位抖动） |
| hotspotThreshold | float | 0.3 | 成热点热度阈值（归一化） |
| maxCenters | int | 8 | 中心上限（防事件风暴，D3） |
| heatSpreadThreshold | float | 0.6 | 扩散阈值（迁自旧 heatSpreadThreshold） |
| spreadRatio | float | 0.4 | 扩散系数（迁自旧 heatSpreadRatio） |
| heatDecayRate | float | 0.05 | 衰减速率（/秒，迁自旧 heatDecayRate） |
| heatMax | float | 1.0 | 热度上限（clamp） |
| heatHitGain | float | 0.4 | 受击热度（迁自旧 heatHitGain） |
| heatEnemyEnter | float | 0.2 | 敌入热度（迁自旧 heatEnemyEnterGain） |
| heatAllyRetreat | float | 0.05 | 友撤热度（迁自旧 heatAllyRetreatGain） |
| drawGizmos | bool | true | 调试绘制总开关 |

---

## 四、冒烟验证 checklist（照单执行）

- [ ] 玩家焦点移动（主城锚点/视角）→ 活跃带跟随（Gizmos 绿区移动）
- [ ] 造一场战斗 → 热点中心出现，热度向邻中区块扩散可见
- [ ] 远处 NPC Think 降频（日志统计 2Hz/0.5Hz），移动不冻结（D79）
- [ ] 战斗结束 3s 后降档（迟滞，demoteDelaySeconds）
- [ ] 多中心：两处同时战斗，两个热点都活跃（≤8 上限）
- [ ] 价值冲突（矿洞抢矿）产热注入（D78）生效
- [ ] 256² 地图（4096 中区块）常驻状态 < 活跃中区块数 + 热点数（稀疏，不建全量）
- [ ] 单位跨中区块移动后 `GetLevel` 返回新状态（F6）
- [ ] 活跃带 300~400 单位压测：帧率 ≥30fps（D268，可用占位单位工厂批量生成）（2026-08-14 终审修订，D268）

---

## 五、验收标准

1. 中区块 LOD 状态正确（活跃带内 10Hz，外降频）
2. 多中心：两处同时战斗，两个热点都活跃
3. 热度在中区块扩散可见（Gizmos 色块）
4. 跨中区块移动继承新状态
5. 远处 NPC 降频不"冻结"（移动仍每帧）
6. 稀疏存储：256² 地图常驻状态 < 活跃中区块数 + 热点数（不建全量 4096 状态）
7. 旧 `AttentionTuningConfig` lod/heat 字段迁移干净（grep 无残留）

---

## 六、风险与回滚

| 风险 | 对策 |
|------|------|
| R1 中区块数量大（256²=64×64=4096）→ 状态开销 | 稀疏存储：Dictionary 只持有"活跃带附近 + 有热度"的中区块；其余默认休眠不落状态（D80） |
| R2 热度扩散性能 | 扩散 tick 1Hz（非每帧）；限扩散半径 2 圈；异步可选 |
| R3 与 2_6 成本场/2_7 决策粒度冲突 | 统一中区块为 LOD/热度/成本场增量粒度（总计划 §3.3） |
| R4 多中心活跃带合并开销 | 中心列表上限 8；区块级取 max（任一中心覆盖即升档），不逐单位算 |
| R5 休眠区 NPC 移动冻结 | 休眠档 = Think 0.5Hz，移动仍每帧（2_3 联动，降频插值可选） |
| R6 热点抖动导致档位抖动 | 升降档迟滞：升档即时，降档需 idleTimer ≥ 3s |
| 旧 LOD 字段迁移遗漏 | grep 复核 `lodActiveThinkHz/lodSemiThinkHz/lodSleepThinkHz/lodActiveRadius/lodSemiRadius/lodDowngradeIdleTime/heatHitGain/heatEnemyEnterGain/heatAllyRetreatGain/heatDecayRate/heatSpreadThreshold/heatSpreadRatio` 无残留 |
| NPCBrain 消费签名 | 适配 `GetLevelAt(worldPos)` → `GetThinkHz(cell)`，FormationBrain `GetHeatAt` 同步 |
| 回滚 | LOD 属性能优化不阻塞功能；git revert LODSystem/RegionLodState 可回 region 粒度；LodConfig 与 AttentionTuningConfig 双份时以 LodConfig 优先 |

> 本片完成即进入 `2_6_寻路`（活跃带范围→细成本场/远处粗切换）、`2_7_AI感知`（热度查询消费）、`2_8_战斗编队`（编队槽位组/活跃带联动）。
