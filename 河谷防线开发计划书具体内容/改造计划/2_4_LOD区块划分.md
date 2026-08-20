# 2_4 LOD 区块划分

> 日期：2026-08-13
> 状态：设计定稿（2026-08-13 审计扩写至详细级）
> 更新：2026-08-14 审查决策同步——详见 [`0.6_审查决策记录.md`](./0.6_审查决策记录.md)。本篇相关：活跃带中心=玩家视角焦点+战斗热点（D77，不再固定绑主城）；价值冲突（矿洞抢矿）也产热注入（D78）；休眠档 NPC 仍每帧移动（D79）；中区块状态稀疏 Dictionary 存储（D80）
> 更新：2026-08-14 终审——验收补活跃带压测（D268）
> 前置：[1_2D网格与空间层改造](./1_2D网格与空间层改造.md)（中区块 4×4/CellToMidChunk）、[0.5_1D到2.5D迁移路线图](./0.5_1D到2.5D迁移路线图.md)
> 编号说明：2_ 系列第 4 篇，**前置地基④**；配套实施计划**滚动补写**（本篇实施前按 2_1 实施计划模板补出）
> 定位：**1D LOD 从 region（大区块）→ 2D 中区块**。LOD 是性能优化（NPC 数量大时降频），不阻塞功能但迁移必需

---

## 一、老系统与新系统的衔接

### 1.1 现状（1D LOD）

老 `LODSystem.cs`（3.0.1_LOD 架构）：
- LOD 区块 = **region（大区块）**
- 每 region 持有 `LodLevel(活跃/半活跃/休眠)` + `threatHeat` + `idleTimer`
- NPC 从所在 region 读 Think 频率（活跃 10Hz/半 2Hz/休眠 0.5Hz）
- 活跃带判定是**一维带状**（地图左右向）

### 1.2 衔接总图

```
老：LOD 区块 = region(大区块)，活跃带=1D 带状
新：LOD 区块 = 中区块(4×4 小区块)，活跃带=2D 多中心半径区块集
```

**关键**：2D 后活跃带从"一条带"变"以玩家/战斗为中心的区块集"；且 2D 战场可能**多点同时开打**，活跃中心是**列表**不是单点。LOD 分区从大区块降到**中区块**（更细粒度，符合四级区块分工：中区块=热度/LOD/成本场增量）。

### 1.3 接缝约定

- 距离按 doc 1 §1.6 格单位（活跃半径以中区块数计，1 中区块 = 4 格边长）
- 热度/成本场增量/编队槽位组统一中区块粒度（与 2_6/2_7 对齐）

---

## 二、老系统的改造（逐文件清单）

| 文件 | 处置 |
|------|------|
| `LODSystem.cs` | region → 中区块；活跃带 1D 带状 → 2D 多中心半径集；状态稀疏化（Dictionary） |
| `RegionLodState.cs` | 改 `MidChunkLodState`（中区块，§5.1 结构） |
| `NPCBrain.cs` | 从所在中区块读 Think 频率（`GetThinkHz`） |
| `FormationBrain.cs` | 热度输入从大区块 → 中区块（`GetHeatAt`） |
| `AIDebugSpawnController.cs` | 调试面板区块索引改 Vector2Int 中区块坐标 |

---

## 三、新系统的需求讨论

### 3.1 功能需求

| # | 需求 |
|---|------|
| F1 | LOD 区块 = 中区块（4×4 小区块） |
| F2 | 活跃带 = 2D **多中心**：主城/玩家焦点位置 + 全部战斗热点（heat > hotspotThreshold 的中区块） |
| F3 | 每中区块持有 LodLevel + threatHeat + idleTimer（稀疏存储） |
| F4 | NPC 从所在中区块读 Think 频率（活跃 10Hz/半 2Hz/休眠 0.5Hz） |
| F5 | 热度扩散（threatHeat 在中区块间扩散 + 衰减） |
| F6 | 跨中区块移动继承新状态（TryEnter 跨界时刷新） |
| F7 | Gizmos 热度/LOD 可视化（冒烟用） |

### 3.2 已决/占位项

| # | 项 | 结论/占位 |
|---|------|------|
| D1 | 活跃带半径 | **占位**：活跃 = 距任一中心 ≤1 中区块（切比雪夫），半活跃 = ≤2，其余休眠；SO 可调。注：1 中区块半径≈以中心 5×5 中区块=20×20 格，覆盖一次中型战斗够用，实测不足时先调 R 再谈细分 |
| D2 | 热度扩散规则 | 沿用 1D heatHitGain/heatEnemyEnter/heatAllyRetreat/heatDecayRate，2D 四邻中区块扩散（§5.3） |
| D3 | 多中心上限 | **占位**：最多 8 个热点中心（按热度取前 8），防事件风暴 |

---

## 四、新系统可能遇到的问题

| # | 风险 | 对策 |
|---|------|------|
| R1 | 中区块数量大（256²=64×64=4096 个）→ 状态开销 | 稀疏存储：Dictionary 只持有"活跃带附近 + 有热度"的中区块；其余默认休眠不落状态 |
| R2 | 热度扩散性能 | 扩散 tick 1Hz（非每帧）；限扩散半径 2 圈；异步可选 |
| R3 | 与 2_6 成本场/2_7 决策粒度冲突 | 统一中区块为 LOD/热度/成本场增量粒度（总计划 §3.3） |
| R4 | 多中心活跃带合并开销 | 中心列表上限 8；区块级取 max（任一中心覆盖即升档），不逐单位算 |
| R5 | 休眠区 NPC 移动冻结 | 休眠档 = Think 0.5Hz，移动仍每帧（2_3 联动，降频插值可选） |
| R6 | 热点抖动导致档位抖动 | 升降档迟滞：升档即时，降档需 idleTimer ≥ 3s |

---

## 五、要开发的新系统（详细设计）

### 5.1 核心类型

```csharp
public class MidChunkLodState
{
    public Vector2Int midChunk;       // 中区块坐标 (x/4, y/4)
    public LodLevel level;            // Active / SemiActive / Dormant
    public float threatHeat;          // 0..heatMax
    public float idleTimer;           // 无活动累计（降档迟滞用）
    public long lastActivityTick;
}

public enum LodLevel { Active, SemiActive, Dormant }
```

### 5.2 LODSystem API

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

### 5.3 活跃带判定（2D 多中心）与热度算法

```
中心集 = { 主城/玩家焦点中区块（上帝视角锚点） } ∪ { heat > hotspotThreshold 的中区块 }（取热度前 8）
对每个中区块：
  d = 到最近中心的切比雪夫距离（中区块数）
  d ≤ activeRadius        → Active(10Hz)
  d ≤ semiActiveRadius    → SemiActive(2Hz)
  其余                    → Dormant(0.5Hz)
降档迟滞：idleTimer ≥ demoteDelay(3s) 才降；升档即时

热度：
  事件注入：RegisterHeatEvent → heat += heatHitGain/heatEnemyEnter/heatAllyRetreat（SO）
  扩散 tick(1Hz)：heat > heatSpreadThreshold 的中区块向 4 邻溢 spreadRatio×heat
  衰减 tick(1Hz)：heat -= heatDecayRate×dt，clamp [0, heatMax]
```

### 5.4 SO 配置（LodConfig）

| 字段 | 默认 | 说明 |
|------|------|------|
| activeRadiusMidChunks | 1 | 活跃半径（中区块） |
| semiActiveRadiusMidChunks | 2 | 半活跃半径 |
| activeHz / semiHz / dormantHz | 10 / 2 / 0.5 | Think 频率 |
| demoteDelaySeconds | 3.0 | 降档迟滞 |
| hotspotThreshold | 占位 0.3 | 成热点热度阈值（归一化） |
| maxCenters | 8 | 中心上限 |
| heatSpreadThreshold / spreadRatio | 沿用 1D | 扩散 |
| heatDecayRate / heatMax | 沿用 1D | 衰减/上限 |
| heatHitGain / heatEnemyEnter / heatAllyRetreat | 沿用 1D | 事件注入量 |

### 5.5 Gizmos 可视化

- 中区块染色：活跃绿 / 半活跃黄 / 休眠灰（只画摄像机视野内）
- 热度叠加红色透明度；中心集画星标
- 调试开关走 LodConfig.drawGizmos

### 5.6 冒烟验证

1. 玩家焦点移动（主城锚点/视角） → 活跃带跟随（Gizmos 绿区移动）
2. 造一场战斗 → 热点中心出现，热度向邻中区块扩散可见
3. 远处 NPC Think 降频（日志统计 2Hz/0.5Hz），移动不冻结
4. 战斗结束 3s 后降档（迟滞）

---

## 六、验收标准

1. 中区块 LOD 状态正确（活跃带内 10Hz，外降频）
2. 多中心：两处同时战斗，两个热点都活跃
3. 热度在中区块扩散可见（Gizmos 色块）
4. 跨中区块移动继承新状态
5. 远处 NPC 降频不"冻结"（移动仍每帧）
6. 稀疏存储：256² 地图常驻状态 < 活跃中区块数 + 热点数（不建全量 4096 状态）
7. **活跃带压测（D268）**：活跃带内 300~400 单位同时活动（活跃档 10Hz Think + 每帧移动），帧率 ≥30fps（占位目标，实机标定）——与 doc1 §3.4 密度表 Medium 档对齐（2026-08-14 终审修订，D268）

---

## 七、实施任务拆分

- **P0**：LODSystem 中区块化、多中心活跃带、NPCBrain 读 Think 频率
- **P1**：热度中区块扩散、热点查询、降档迟滞
- **P2**：性能优化（异步扩散）、Gizmos 完善

---

## 八、与其他子文档接口

| 文档 | 依赖/提供 |
|------|------|
| doc 1 | 依赖：中区块 4×4 索引（CellToMidChunk）、EnemyEnteredChunkEvent |
| 2_3 | 依赖：NPC 移动跨中区块（位置更新）；提供：降频档位 |
| 2_5 | 依赖：伤害/击杀事件（热度注入） |
| 2_6 | 提供：活跃带范围（细成本场/远处粗切换） |
| 2_7 | 提供：热度查询（FormationBrain 输入） |

---

## 版本历史

> **2026-08-20（主仓库 caa8dd84）实施落地**：
> - 新增：`LodConfig.cs`（SO 运行时真源，活跃半径/Think频率/热度扩散衰减/事件注入；注意旧 `AttentionTuningConfig` lod/heat 字段保留保 sim 同源不删）+ `Assets/Resources/Config/LodConfig.asset`
> - 新增：`MidChunkLodState.cs`（`LodLevel` 含 `Dormant`，替代旧 `RegionLodState.cs`，已删）
> - 重写：`LODSystem.cs`——1D region → 2D 中区块（4×4，`GridSystem.CellToMidChunk`）；状态稀疏 `Dictionary<Vector2Int,MidChunkLodState>`（D80）；2D 多中心活跃带（主城锚点+战斗热点，切比雪夫，`maxCenters` 上限 D3/D77）；热度扩散/衰减 tick 1Hz（`heatSpreadThreshold`/`spreadRatio`/`heatDecayRate` 4 邻）；升降级即时上、降档防抖（热度归零+`idleTimer≥demoteDelaySeconds`）；事件驱动升档（`UnitDamagedEvent.Position` 兼容建筑、`EnemyEnteredChunkEvent`）；Gizmos（活跃绿/半活跃黄/休眠灰+热度红+中心星标）
> - 改动：`NPCBrain.RefreshLodIntervals` 改读 `LODSystem.GetThinkHz`（不再直接用 config 频繁字段）；`GetLevelAt/GetHeatAt/TryGetCombatHotspot/TryGetNearestCombatHotspot` 签名保持，FormationBrain 等调用点兼容
> - 验证：编译 0 错误；`LodConfig.asset` `Resources.Load` 通过；⚠️ Play 冒烟验证待补（活跃带跟随/热点扩散/降档迟滞/稀疏性）
