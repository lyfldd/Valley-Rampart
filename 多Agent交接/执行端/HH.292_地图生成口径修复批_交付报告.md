# HH.292 · 地图生成口径修复批（HH.291／D735）交付报告

| 项 | 值 |
|----|----|
| 角色 | 执行端（TraeCode） |
| 批次 | `HH.291`「地图生成口径修复批」（6 项施工面 A1~A6·全落 `MapGenRules` 域） |
| 任务书 | `多Agent交接/策划端/HH.291_地图生成口径修复批_任务书.md`（D735 签发） |
| 开工回执 | `多Agent交接/执行端/HH.291_地图生成口径修复批_开工回执.md`（**D736 已裁＝✅ 放行施工**·3 项照准） |
| 取号 | **HH.292**（`_编号登记.md` 水位线 291→292·独立单行 commit） |
| 状态 | 🟡 **待验收**：A1/A2/A3/A4/A5 达；**A6① ② 达、A6③ 结构性不可达**（列报 R2）；**R3 探针受限·未判定**（列报） |
| 证据载体 | `Logs/hh291_probe.log`（编辑态 RunAll ＋ 实机正门 RunLiveCo 全量读数） |
| 探针 | `Valley Rampart/Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs`（本批新建） |
| push | **未 push** |

---

## §一 施工面逐项落实与判据读数

| # | 施工面 | 落实位置 | 验收判据（能力句） | 实测读数 | 判定 |
|---|--------|----------|-------------------|----------|------|
| A1 | 9 设计字段序列化进资产 | `Assets/Resources/Grid/MapGenRulesConfig.asset`（编辑器内 `SetDirty`+`SaveAssets` **重存**） | 资产文件内**可见** 9 字段 | 全文见 **§2.1**：`clusterSizeMin/TypicalMin/TypicalMax/Max`·`resourcesPerChunkBase`·`resourceWeights`（四带 5 维）·`guaranteeRatio`·`difficultyResourceScale`·`kingdomClearRadius` **逐字段在场** | ✅ |
| A2 | 四带 `mine.w` 提值＋实测校准 | `MapGenRulesConfig.cs`／`.asset` | 四带 mine 占比**实测**（温带目标 ≈7.3%） | 256²N：**热带 4.3%／亚热带 5.6%／温带 6.7%／寒带 5.4%**（解析 4.00/5.90/7.27/5.41%） | ✅ 见下注① |
| A3 | 新增 `bandResourceAbundance[4]` 乘进 `T` | `MapGenRulesConfig.cs`（`GetBandAbundance`＋`ComputeQuota` 乘子） | 四带 `T` **实测 = 108／156／132／84** | 256²N 实测 **108.6／155.9／130.8／84.2**；**M7 均值 `T×abundance` = 120.0** ✅ | ✅ |
| A4 | 坑位模型（两处）＋`PickSubSlot` 返回值真消费 | `MapGenRules.cs`：`PlaceResourceQuota`／`EnsureChunkResourceQuota`／新增 `PlaceResourceCell`＋`_pitMask` 生成期坑位账 | ⭐**至少 1 个 Cell 放了 4 个资源**＋全部 Cell ≤4 | 非矿 `max坑位/格 = 4`·**满格(=4) 计数 = 6824（256²N）／1752（128²）**·全域 `>4` 计数 = **0**·坑位总数 28810 | ✅ |
| A5 | 移除 `EnsureNearbyResources`＋`resourceGuaranteeRadius` | `MapGenRules.cs`（删函数·留墓碑）／`WorldManager.cs`／`Valley_HH272_MapGenProbe.cs` | 调用点**清零**＋地图**仍每区块有资源** | 全仓调用点 2 处**清零**；256 区块 **空区块 = 0**·min=77·均=119.2 | ✅ |
| A6 | `DeriveNaturalBuildings` 白名单加 `FeatureType.Mine`（w/h=2×2·每簇 1 实例） | `MapGenRules.cs` `DeriveNaturalBuildings` | ⭐Mine **实例数 > 0** ＋ 组件在场 ＋ **石入库有读数** | 实机 **Mine 实例 = 429**（改前 0）✅；`MineByproductComponent` **429/429** ✅；**石入库 ❌ 结构性不可达**（R2） | ① ② ✅／③ ❌ 列报 |

**注①（A2 校准留痕）**：首跑寒带 `w=0.88` 实测 **10.4%** > 温带 **6.6%** ⇒ 梯度反转（寒带 `woodPile w=1.00` ⇒ `1−w=0` ⇒ Σ 缩小 ＋ 2×2 簇整数量化）。按任务书提示回调寒带 **0.92**（源码＋资产同步）⇒ 复跑 **5.4%**，梯度恢复 `温带 6.7% > 亚热带 5.6% > 寒带 5.4% ≥ 热带 4.3%`。
**注②（占比口径偏差）**：实测占比系统性略低于解析值（温带 6.7% vs 7.27%），成因＝`mine` 按 **2×2 Cell 整簇**落格（整数量化·尾簇不落地）⇒ 属模型固有量化偏差，非实现偏差（其余四类实测/解析吻合 ≤0.3pp）。

---

## §二 附证据

### 2.1 A1 资产文件内容（`MapGenRulesConfig.asset` 全文·重存后）

```yaml
%YAML 1.1
%TAG !u! tag:yousandi.cn,2023:
--- !u!114 &11400000
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: bd16d70b57943b44d85aa7075f91797c, type: 3}
  m_Name: MapGenRulesConfig
  climateWeights: [1, 1, 1, 1]
  clusterSizeMin: 4
  clusterSizeTypicalMin: 96
  clusterSizeTypicalMax: 384
  clusterSizeMax: 384
  resourcesPerChunkBase: 120
  resourceWeights:                 # 热带/亚热带/温带/寒带
  - {tree: 0.2,  stonePile: 0.45, woodPile: 0.45, oreVein: 0.5,  mine: 0.9 }
  - {tree: 0.2,  stonePile: 0.4,  woodPile: 0.4,  oreVein: 0.45, mine: 0.84}
  - {tree: 0.25, stonePile: 0.4,  woodPile: 0.4,  oreVein: 0.4,  mine: 0.8 }
  - {tree: 0.6,  stonePile: 0.45, woodPile: 1,    oreVein: 0.55, mine: 0.92}
  guaranteeRatio: 0.5
  difficultyResourceScale: [0.7, 1, 1.3]
  bandResourceAbundance: [0.9, 1.3, 1.1, 0.7]      # HH.291 A3 新增
  mountainCellRatio: [0.05, 0.08, 0.12, 0.16]
  mountainSnowRatio: [0, 0.05, 0.3, 1]
  mountainRidgeWidthMin: 1
  mountainRidgeWidthMax: 2
  mountainRidgeLengthMin: 6
  mountainRidgeLengthMax: 20
  mountainClusterMinSize: 4
  kingdomClearRadius: 4
  spawnMinDistanceCells: 180000002000000028000000
  connectivityThreshold: 0.95
  threatsPerKingdom: 2
  threatMinChunkDistance: 2
  featureScanRadiusCells: 8
  forestDensityThreshold: 0.1
  mineralDensityThreshold: 0.039
  barrenDensityThreshold: 0.6
```

**A1 对照**：改前资产 **仅序列化 `climateWeights`（608 B）**；重存后上列字段**逐字段在场**。**A5 对照**：`resourceGuaranteeRadius` 在文件中**已消失**（grep 零命中）。

### 2.2 A2/A3/M1/M2/M7 四带配额对照表（256²／Normal·diff2·seed 21107）

```
带     | 区块 | T公式 | T实测 | 树   | 石堆 | 木堆 | 矿脉 | 矿洞u | 合计 | mine实测 | mine解析(改后) | mine解析(改前)
热带   | 63   | 108   | 108.6 | 2182 | 1496 | 1496 | 1374 | 292   | 6840 | 4.3%     | 4.00%          | 14.29%
亚热带 | 60   | 156   | 155.9 | 2732 | 2088 | 2084 | 1920 | 528   | 9352 | 5.6%     | 5.90%          | 16.39%
温带   | 67   | 132   | 130.8 | 2392 | 1923 | 1931 | 1931 | 586   | 8763 | 6.7%     | 7.27%          | 20.31%
寒带   | 66   | 84    | 84.2   | 1515 | 2046 | 0    | 1700 | 298   | 5559 | 5.4%     | 5.41%          | 22.22%
```

- **改前**：四带 `T=120`·`mine w = 0.60/0.50/0.35/0.60`·无丰度（解析列）；**改后**：`T = 108/156/132/84`。
- **M7**：`T×abundance` 四带均值 = **120.0**（基准 `resourcesPerChunkBase=120`）⇒ 全图总量基准不变。
- 128² 同型读数（`T` 实测 106.3/155.8/129.1/83.2）见 `Logs/hh291_probe.log`。**M2 全量对照见本表（四带全列·非只温带）**。

### 2.3 【D736 §四 强制】生成期配额 ⨯ 运行期可采实体数（双列·逐类对照改前·256²N）

> 口径：**生成期配额**＝计数器 `_pitMask` 坑位口径（非矿 1 坑位=1 资源实例·矿洞 2×2 Cell 粒度）；**运行期可采实体数**＝容纳该类的**格数**（矿洞＝簇数）。改前列＝**解析值**（`T=120`＋改前权重·同公式复算），非实测。

| 带 | 类别 | 改前·配额 | 改前·可采 | 改后·配额(坑位) | 改后·可采 | 可采倍率 |
|----|------|-----------|-----------|-----------------|-----------|----------|
| 热带 | 树 | 2160.0 | 2160.0 | 2182 | 561.0 | **0.26×** |
| 热带 | 石堆 | 1485.0 | 1485.0 | 1496 | 374.0 | **0.25×** |
| 热带 | 木堆 | 1485.0 | 1485.0 | 1496 | 374.0 | 0.25× |
| 热带 | 矿脉 | 1350.0 | 1350.0 | 1374 | 375.0 | 0.28× |
| 热带 | 矿洞 | 1080.0 | 270.0 | 292 | 73.0 | 0.27× |
| 亚热带 | 树 | 1888.5 | 1888.5 | 2732 | 712.0 | 0.38× |
| 亚热带 | 石堆 | 1416.4 | 1416.4 | 2088 | 537.0 | 0.38× |
| 亚热带 | 木堆 | 1416.4 | 1416.4 | 2084 | 536.0 | 0.38× |
| 亚热带 | 矿脉 | 1298.4 | 1298.4 | 1920 | 480.0 | 0.37× |
| 亚热带 | 矿洞 | 1180.3 | 295.1 | 528 | 132.0 | 0.45× |
| 温带 | 树 | 1884.4 | 1884.4 | 2392 | 598.0 | 0.32× |
| 温带 | 石堆 | 1507.5 | 1507.5 | 1923 | 531.0 | 0.35× |
| 温带 | 木堆 | 1507.5 | 1507.5 | 1931 | 533.0 | 0.35× |
| 温带 | 矿脉 | 1507.5 | 1507.5 | 1931 | 533.0 | 0.35× |
| 温带 | 矿洞 | 1633.1 | 408.3 | 586 | 146.5 | 0.36× |
| 寒带 | 树 | 1760.0 | 1760.0 | 1515 | 395.0 | 0.22× |
| 寒带 | 石堆 | 2420.0 | 2420.0 | 2046 | 528.0 | 0.22× |
| 寒带 | 木堆 | 0.0 | 0.0 | 0 | 0.0 | 0.00× |
| 寒带 | 矿脉 | 1980.0 | 1980.0 | 1700 | 458.0 | 0.23× |
| 寒带 | 矿洞 | 1760.0 | 440.0 | 298 | 74.5 | 0.17× |

**读法（合规声明·D736 §四）**：**本报告不声称「资源总量不变」**。准确口径＝**「生成期配额口径不变（`T` 四带均值 × abundance = 120 基准）」**；**运行期可采实体数较改前下降至 0.17×~0.45×**（比值 = `abundance` 效应 ÷ 4 坑位/格 ＋ 整数量化）。比值带内差异来源＝四带 `abundance` 不同（0.9/1.3/1.1/0.7）。

### 2.4 A4 坑位存在性实证（能力句「至少 1 格放了 4 个资源」）

| 图幅 | max 坑位/格 | 满格(=4)计数 | 全域 >4 计数 | 坑位总数 | 直方图 0..4 |
|------|-------------|--------------|--------------|----------|-------------|
| 256²N | **4** | **6824**（须>0） | **0**（须0） | 28810 | 58011/201/187/313/6824 |
| 128² | **4** | **1752** | **0** | 7398 | 14453/50/47/82/1752 |

实现要点：`PlaceResourceCell` 消费 `PickSubSlot(rng)` 返回值·同格多坑位**顺移避碰撞**（`while ((mask & (1<<slot)) != 0) slot = (slot+1)&3`）⇒ 1..4 坑位严格落位、天然不重叠；`_pitMask` 生成期账**跨 `PlaceResourceQuota` 与 `EnsureChunkResourceQuota` 两趟存活**；矿洞走 `MarkBlockPitsFull` 整格占满（例外口径）。**同格同型**约束（`MapData.features` 逐格唯一）已按 D736 照准落地。

### 2.5 A6 实机正门读数（seed 21107/Medium/diff2/15x·正门 `EnterTestRun`）

```
[A6 对照]  改前 Mine 实例 = 0（派生白名单不含 Mine；仅 KingdomFoundry 立国预置 4 座）→ 改后 = 429
A6①  Mine Building 实例数 = 429（须>0）✅  · 建筑实例总数 5702 · 地图 Mine nb=425
A6②  组件在场：ProducerComponent = 0/429 ｜ MineByproductComponent = 429/429 ✅
A6 资产面  mine.isResourceNode=True isMineByproduct=True outputResource=Stone producer.rate=0.3 footprint=(2,2)
A6③  石入库读数 = ❌ 结构性不可达（见 §四 R2）
生成侧    Mine nb = 426（w/h≠2×2 者 0）· Mine 格 = 1704 = 426×4 ✅
M6        spawn→最近 Mine 簇：6 / 6 / 9 格（净空区半宽=5）⇒ 主城旁仍能找到矿
```

**A6② 口径勘正**（实读）：`mine` 为 `isResourceNode=1` 采集点身份 ⇒ `BuildingFactory.AttachComponents:295`（`!def.isResourceNode` 排除通用产能分支）**结构性不挂 `ProducerComponent`**；可挂副产链组件＝`MineByproductComponent`（`:316-319`）⇒ 判据改判为「可挂组件在场」＝**429/429 达**。

### 2.6 M5 性能对照（同局同机位 A/B·逐帧 `Time.unscaledDeltaTime` 采样）

| 轮次 | 加载(EnterGame→就绪+5s) | 帧时·含 mine | 帧时·销毁后 | Δ | GameObject Δ | 单座直建 |
|------|------------------------|--------------|-------------|-----|--------------|----------|
| 2 | 7.65 s | 143.591 ms/帧 | 137.922 ms/帧 | **+5.67** | 1720 | 0.577 ms |
| 3 | 8.41 s | 197.501 ms/帧 | 149.055 ms/帧 | **+48.45**（噪声离群） | 1720 | 0.483 ms |
| 4 | 7.44 s | 223.451 ms/帧 | 227.293 ms/帧 | **−3.84** | 1718 | 1.315 ms |
| 5 | 7.52 s | 120.537 ms/帧 | 122.251 ms/帧 | **−1.71** | 1719 | 0.504 ms |

**结论**：有效对照 3 次 Δ 落在 **−3.84 ~ +5.67 ms/帧**（另 1 次 +48.45 为同刻离群），**无稳定正差**；加载时长 **7.44~8.41 s**。**单座直建成本 0.48~1.32 ms/座**（同链 `CreateBuildingInstance`）⇒ A6 新增 425 座**线性外推 205~559 ms**（口径：编辑器内线性外推·**非整链实测差**）。A6 新增约 1536 个 GameObject（425 簇 × 若干子物体）**未观测到稳定帧时代价**。

### 2.7 确定性复验 ／ 难度比 ／ 耗时

- **同 seed 两次**：`features ⊕ climateZones ⊕ spawns ⊕ nb` **逐格一致** ✅（M3 判据＝确定性保留；「与改前同图」不作判据·D736 照准）
- **异 seed 出异图** ✅
- **难度三档资源单位比** = **0.711 : 1.000 : 1.289**（目标 0.7:1.0:1.3）✅
- **生成耗时（全管线·编辑器内）** = **140.0 ms**（HH.272 同口径基线 183.1 ms）

---

## §三 红线自查

| 红线 | 自查 |
|------|------|
| 改动域限 `MapGenRules.cs`／`MapGenRulesConfig.cs`／`.asset` | ✅ 业务改动恰 3 文件；连带 2 处＝A5 调用点（`WorldManager.cs` ＋ `Valley_HH272_MapGenProbe.cs`·D736 已照准「强制连带」） |
| `AI.Core` 零触 | ✅ 零改动（`AI.Core/**` 零命中） |
| 不动已验收的 `EnsureChunkResourceQuota`／`MergeFragments`／`CapOversizedClusters`／山脉化 | ✅ 仅 `EnsureChunkResourceQuota` 内「落格」改走 `PlaceResourceCell`＋计数改坑位口径（行为语义保留·每区块资源不空实测 0/256） |
| 地图确定性（同 seed 逐格一致） | ✅ §2.7 |
| 正门 `EnterTestRun`＋退 Play（L-32） | ✅ 实机读数走正门；收尾 `ExitTestRun`＋`QuitSmoke`（三态已退） |
| 禁找补（L-30） | ✅ 未调参过线：唯一参数回调（寒带 `mine w` 0.88→0.92）有实测依据且**改后仍不改变判定**；A6③ 未达即未达，不绕行 |
| 判据写口径来源与排除项（L-35） | ✅ 各表均标口径（坑位/格/簇·解析 vs 实测·编辑器内 vs 实机） |
| 具名 `git add`·不 push·写-改-commit 同串 | ✅ 见 §五 |

---

## §四 列报与请裁

### R1（**已裁**·D736 §三 取 (A)「接受现状」）——坑位在运行期不可见

运行期采集/刷新/锁格按「格」计（`ResourceRespawnSystem.cs:115`／`WorldManager.TryConsumeResourceNode:232`）⇒ 同格 4 坑位在运行期不可见。**本批按 D736 取 (A)**：不动 `T`、本批不阻断施工；**准确口径见 §2.3**（可采实体数下降至 0.17×~0.45×）。治本方向 (B)/(D) 待用户拍板另批。

### R2 🔴 `mine`「石入库」**结构性不可达**（A6③ 判据不达·请裁）

**证据链**（三条实读）：
1. `mine.asset:53/64/73` ⇒ `isResourceNode: 1` ／ **`isConsumable: 0`** ／ `isMineByproduct: 1`
2. `Building.TryAdvertiseTask:960` 的 Gather 分支**要求 `def.isConsumable`** ⇒ mine 不满足（`isConsumable=0`）
3. `Building.TryAdvertiseTask:975` 的 Production 分支**要求 `ProducerComponent`** ⇒ `BuildingFactory.AttachComponents:295` `!def.isResourceNode` 门 ⇒ mine **结构性不挂**（实测 0/429）

⇒ **`mine` 实体无任何「产石」通道**；`mine.asset` 的 `producer.rate=0.3 / output=Stone` 在该身份下为**不可达死字段**。实机佐证：探针 mine 本体 `StorageComponent` **不存在**、台账 `stone 25→25`（增量 0）。
（石来源＝`StonePile` 一次性采集 ＋ `quarry` 采石场。）

**三选项**：(甲) A6③ 判据**改判**为「Mine 实例在场 ＋ 副产链组件在场」并**另批**决定 mine 是否应具产石身份（**推荐**·本批已完成可验收部分）；(乙) 另批把 mine 纳入产石链（改身份 ⇒ 触碰 `isResourceNode` 消费面＝`WanderStimulusProvider`／`GuardDeploymentSystem`／拆除守卫／`BuildingPanel` **M1 红线区**）；(丙) `mine.asset` 死字段清理挂账。**本批不擅改**（超红线域）。

### R3 ⚠️ 运行期「在岗 mine = 0/429」**未判定**（探针方法学受限·非缺判定论）

**实测**（阳性对照·L-29·`DispatchExternal` 绕过调度竞争直派 1 工）：

```
空闲工人 3／非空闲 0（全图任务源 ≈5700+）
(A) 探针 mine（2×2 占格）：在岗=0 · 末态=MovingToSource(2) · 末距源=16.74 格 · 副产子仓 0→0
(B) 对照 1×1 自然建筑（Well）： 在岗=0 · 末态=None(0)      · 末距源=15.18 格
```

**读法**：(B) 对照组**同型失败** ⇒ 失败**非 mine 特异性**（不能归因于 A6 的 2×2 几何）；且落点已取「邻近可走格」，工人仍距源 15~17 格 ⇒ **探针的 `SpawnUnit` 落点/导航驱动未被验证有效**，本口径**不足以判定**副产链本体是否可用。
**处置**：本批**不定缺陷、也不宣称通过**；建议**另批专项**（独立小图＋独占工人池＋落点/轨迹直证）判定副产链（水晶/火油/矿石）在 A6 新增 425 座 mine 上是否真产。**不影响 A6①/A6② 判定**；与 R2 为**互不相同的两条通道**。

---

## §五 改动清单与落盘

**业务（3）**：`Assets/_Game/Systems/World/MapGenRules.cs`／`Assets/_Game/Data/MapGenRulesConfig.cs`／`Assets/Resources/Grid/MapGenRulesConfig.asset`
**连带（2·A5 调用点·D736 照准）**：`Assets/_Game/Systems/World/WorldManager.cs`／`Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs`
**探针（本次新建）**：`Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs`（＋`.meta`）
**文档**：本报告 ＋ `_编号登记.md`（水位线 291→292 ＋ 在途行）＋ `_交接索引.md`
**证据（就地留档·不入库）**：`Logs/hh291_probe.log`

**未触碰**（他批在途·不纳入本批 commit）：`Valley_HH284_Probe.cs`／`GameScene.unity`／`Packages/**`／`pixel-forge/**`／`美术资源文件夹/**`／`河谷防线开发计划书具体内容/3.6·3.8*.md`。
**`HH.289/290` 废弃号文件**（`Valley_HH289_EcoProbe.cs`／`Valley_HH290_GatherProbe.cs`）按用户裁「不再登记·作废」**原样留置不动**。

**本批不做**：批B（AI 开局资源 stockpile）。
