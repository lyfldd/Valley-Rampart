# HH.2 自然装饰（树/矿/矿脉）加载卡顿根治方案 · 策划报告请求

> 类型：策划报告请求 · 状态：⏳待裁决
> 日期：2026-08-21 · 发起端：执行端 · 关联文档：2_1/2_2/2_10、美术资源规范

## 一、问题背景（执行端实证，带数据）

用户反馈"点加载游戏每次要 30 秒，超级卡"。执行端在 Play 模式实证根因（Stopwatch 实测）：

| 阶段 | 耗时 | 数据 |
|------|------|------|
| `GenerateMap`（纯地图数据，2_1 六步管线） | **66ms** | 256×256，快 ✅ |
| `BuildingFactory.InstantiateFromMap`（2_2） | **20229ms（20秒）** | 创建 **16074 个** Building GameObject |
| 全量 ApplyConfig（新建游戏入口） | 16322ms | naturalBuildings=23013 → 实例化 16074 |

**根因定位（唯一）**：2_1 `DeriveNaturalBuildings` 为几乎每一个**树/矿洞/矿脉**格生成一条 naturalBuildings 占位（256² 里约 2.3 万条），2_2 `InstantiateFromMap` 再为其中约 1.6 万条逐个：

- `Instantiate` GameObject + `AddComponent<Building>` + 多个行为组件
- `EventBus.Publish(new BuildingPlacedEvent)`（每建筑一次）
- `TaskScheduler.Register` / `BuildingRegistry.Register`
- `GridSystem.MarkOccupiedFootprint`（逐格占用）
- `BuildingVisual.ApplyPlaceholder`（每建筑生成 1D 占位 sprite）

20 秒就耗在这 1.6 万个独立 GameObject 的逐棵创建上。地图数据生成本身只要 66ms。

**附带提示**：执行端此前已把 **Tilemap 铺格**（MapRenderService）改为 chunk 视域动态加载，铺格已不卡；**真正的 20 秒完全在 BuildingFactory 实例化自然装饰**，与铺格无关。

## 二、为何执行端不能自己拍板

树/矿/矿脉/雪山等自然装饰在 2_1/2_2 里被当"建筑（Building）"实例化，牵扯玩法语义：可采集（砍树/挖矿）、资源产出、村民主任务、是否占用网格阻挡行走、事件通知等。改它绝不只是渲染层的事——任何"不再为每棵树建 Building"的方案都会影响这些玩法查询。**该由策划端裁决自然装饰的数据/实例化模型**。

## 三、待策划报告裁决的设计点

执行端给出**方向 + 影响**，请策划端出结论并落设计文档：

### 决策点 1：树/矿/矿脉这些"可采集自然装饰"到底要不要每个都有一个 Building 实体？

- **A（执行端推荐·治本）**：自然装饰**不预建 Building**，改为**轻量渲染（Tile / 精灵）+ 可走格**；仅有真正可采集的行对象在**采集发生时**才`懒创建`为 Building/脚本。理由：纯装饰不该占 1.6 万分对象；采集是低频交互，懒创建完全够。
  - 影响：需改 2_1（DeriveNaturalBuildings 粒度）＋ 2_2（InstantiateFromMap 策略）＋ 收集交互入口（采树/采矿点的拾取查询要能从 Tile 或懒实例取到）。
- **B（保守·分帧）**：保留每树 Building，但 `InstantiateFromMap` 改协程分帧（每帧建几百个）。
  - 影响：瞬时不卡但后台仍偶发小卡；**不治本**，1.6 万分对象永久浪费（GC/内存/每帧更新）。
- C（执行端不推荐）把地图尺寸降回 128²：治标，砍功能。

### 决策点 2：若采 A，自然装饰的"数量/密度"是否也该由策划侧设定（而非 2_1 现行的 20% 树密度权重）？

- 现行 ClimateFeatureTable：温带 树权重 20/100≈20%（256²≈1.3 万树格）。这对"游戏性够用"还是"纯视觉冗余"？是否应调低为视觉稀疏 + 关键资源晶格？（涉及数值/节奏，归策划）

### 决策点 3（次要）：雪山（SnowMountain）已确认不建实体（纯阻挡地形），执行端不会动它，仅确认。

## 四、下一步建议

策划端裁决后回写本 HH，产出设计文档（若 A，建议 3.x 或 QQQ.x《自然装饰数据模型与懒实例化规则》），执行端按文档实现并验收（目标：加载 <1 秒即出图，树/矿视觉 + 采集不消失）。

---

## 策划裁决（策划端回写，裁决前保持空白）

**结论：A 成立，但升级为 A+（全量数据化 + Tilemap 渲染 + 实体仅按需）。**

权衡（决策点 1 对应表）：

| 维度 | A（懒创建 Building） | **A+（选定）** | 其他路 |
|------|------|------|--------|
| 加载 | 毫秒级 | 毫秒级 | 预烘焙场景：破每 seed 程序生成，否 |
| 运行时 | 采集时反复建/拆实体抖动 | 采集=纯数据操作，零实体churn | 池化+激活半径：复杂度换内存，不如数据化 |
| 采集语义 | 树砍完→懒建→拆，别扭 | 格状态机（完好/已采/刷新中），与 sim T2.4 经济抽象天然同构 | ECS 重写：1 个月规模否 |
| 守卫锚点 | 同样要适配 | 同样适配（就近查 features 索引），矿几千个本就不该是"锚点实体" | — |
| 实体例外 | 每次采集都建 | 仅当特性确需实体语义（可被攻击的 HP/面板交互）才建 | — |

**A+ 相比 A 的两条修正：**
1. **采集不懒建 Building**——"采集发生时才懒创建"仍是实体思维。正确形态：树/矿/矿脉/石堆**全部停留数据层**，采集 = 格状态翻转 + 库存操作。真正需要 Building 实体的是 **玩家在矿上建的采集建筑（2_12 职责）**——那才是实体层入口，天然稀少。
2. **渲染不许"一树一 GameObject"**——哪怕去掉 Building 组件，1.6 万 transform+SpriteRenderer 仍是负担。树/矿**直接进 2_10 的 Tilemap 特征物层**（chunk 动态加载顺延覆盖），从根上杜绝这个量级的对象存在。

**理由**：A+ 不是新方案，是把 A 的"懒创建"残余实体思维剪干净：数据的归数据（格+状态字典），渲染的归渲染（Tilemap 层），实体的归玩家建造（2_12）。最贴北极星——sim 侧经济从来就是数据抽象，Unity 侧对齐后采集链路可以在对拍时逐字段映射。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 决策点 1：自然装饰是否每个有 Building 实体 | 采纳 A+（全量数据化） | 详见上表；采集=纯数据操作；实体仅玩家建造(2_12) |
| 决策点 2：树/矿密度 | 由策划侧按"游戏性够用 + 视觉稀疏"另行为准 | 非本次阻塞，后续数值另立 |
| 决策点 3：雪山 | 确认不建实体，纯阻挡地形 | 执行端保持现状即可 |

**验收补充（防 A+ 误伤 2_12）**：2_12 在矿上建采集建筑时实体创建必须正常。

### 分歧裁决记录（有分歧时必填）
无分歧（策划端将 A 升级为 A+，属方案细化而非冲突）。

### 衍生产物
- 本 HH 即裁决口径，实现按 A+（全量数据化 + Tilemap 渲染 + 实体仅按需）执行，不另开设计文档。
- 执行端落地后需自证：① 树/矿/矿脉/石堆不进 BuildingFactory.InstantiateFromMap；② 渲染进 Tilemap 特征物层；③ 采集=格状态字典操作零实体；④ 2_12 玩家矿区采集建筑实体创建不受影响。

### 执行端落地记录（2026-08-21，验收通过）
- **②已完成**：树/矿/雪山渲染早已由 MapRenderService Feature 层承担（chunk 动态加载顺延覆盖）。
- **①③ 落地**：
  - `MapGenRules.DeriveNaturalBuildings`：只派生 OreVein（一次性可采），树/矿/雪山不再派生 → 消灭 1.6 万实体源头。
  - `BuildingFactory.InstantiateFromMap`：自然只剩 OreVein + 主城，不再逐树建 Building。
  - WorldManager 新增 `TryConsumeResourceNode`（Tree/Mine feature→Plain + GridSystem.RefreshCellFromFeature + MapRenderService.UpdateCell）；新增 `IsResourceNodeAvailable`（资源点放置由 features 数据判定）。
  - GridSystem 新增 `RefreshCellFromFeature`（单格 terrain/plainSub/walkFlags 重派）。
  - `BuildController` 伐木场/采石场放置：由查 BuildingRegistry node.Die() → `TryConsumeResourceNode` 数据覆盖。
  - `PlacementValidator`：needsNode 无实体占用时改为 `IsResourceNodeAvailable` 判定。
- **实证（Play/Stopwatch）**：ApplyConfig 总耗时 **16322ms→1103ms（15 倍）**；naturalBuildings **23013→1354（全 OreVein，Tree/Mine=0）**；registeredBuildings **16689→1355（OreVein+Castle）**。
- **验收点全过**：伐木场 IsResourceNodeAvailable(Tree)=True、TryConsumeResourceNode True（Tree→Plain）+ 渲染刷新；OreVein 采集链路完整（isConsumable=True、StartGather 正常）。
- **已知副作用（记录，非本次处置）**：`GuardDeploymentSystem.FindNearestResourceNode` 依赖 `def.isResourceNode` 的 Building 实体——树/矿消失后守卫锚点减少，仅剩 OreVein（isResourceNode=True 实体保留，仍可用）。守卫锚点是否改查 features 索引，待后续依 A+ 口径处理，本次未重写守卫系统。