# HH.291 · 地图生成口径修复批 · 开工回执

> 类型：开工回执｜状态：🟡 **可施工**（含 1 项结构性缺口列报 **R1**，见 §五）
> 日期：2026-09-15｜执行端（TraeCode）
> 依据：`多Agent交接/策划端/HH.291_地图生成口径修复批_任务书.md`（D735 签发）
> 取号：**占 HH.291**（与任务书**同号**·`HH.272`／`HH.282` 先例）；交付报告届时按 `_编号登记.md` **实时水位线**取号（`D640 #10` 禁预留）
> 红线：`AI.Core` 零触／改动域限 `MapGenRules.cs`／`MapGenRulesConfig.cs`／`.asset`／不动已验收的 `EnsureChunkResourceQuota`（保留）·`MergeFragments`·`CapOversizedClusters`·山脉化／正门 `EnterTestRun`＋退 Play（`L-32`）／禁找补（`L-30`）／判据写口径来源与排除项（`L-35`）／具名 `git add`·不 push·写-改-commit 同串

---

## §〇 基线（开工前实测）

| 项 | 读数 | 取证 |
|---|---|---|
| 编辑器状态 | 非 Play（edit mode） | `mcp_unityMCP` 在线（`read_console` 返回 4 条 error） |
| Console **存量** error | **4 条**（`No Theme Style Sheet set to PanelSettings`／`The referenced script on this Behaviour (Game Object '') is missing!`／`233 node options failed to load and were skipped.`／`Some objects were not cleaned up when closing the scene…[SpriteAnimatorDriver]`） | 同上；**改后新增 error 须 = 0**（区分存量/新增） |
| 资产实况 | `Resources/Grid/MapGenRulesConfig.asset` = **608 B**，仅序列化 `climateWeights` ＋ 5 个旧字段（`spawnMinDistanceCells`／`resourceGuaranteeRadius`／`connectivityThreshold`／`threatsPerKingdom`／`threatMinChunkDistance`） | 资产文件逐行实读（本地 Read） |
| 脚本 GUID | 资产 `m_Script` guid `bd16d70b57943b44d85aa7075f91797c` **≡** `MapGenRulesConfig.cs.meta` 真身 → **脚本链接正常**（缺字段非"链接断"，是"从未重存"） | `.meta` 实读 |
| git | 分支 `main`；工作区有他端既有改动（`Valley_HH284_Probe.cs`／`GameScene.unity`／`Packages/*`／`pixel-forge/*` 等）＋未跟踪 `Valley_HH289_EcoProbe.cs`／`Valley_HH290_GatherProbe.cs` —— **本批一律不碰、不 add** | `git status --short` |

---

## §一 施工面复述（6 项·逐条钉 file:line）

| 项 | 施工内容 | 锚点（实读） |
|---|---|---|
| **A1** | 9 字段序列化进资产：`clusterSizeMin`／`clusterSizeTypicalMin|Max`／`clusterSizeMax`／`resourcesPerChunkBase`／`resourceWeights[4]`／`guaranteeRatio`／`difficultyResourceScale`／`kingdomClearRadius` ⇒ **编辑器内重存**（Unity 不自动补写）| `MapGenRulesConfig.cs:32-58`（全 `public`，无需补 `[SerializeField]`）／`MapGenRulesConfig.asset:15-24` |
| **A2** | `mine` 的 `w` 提值：热带 **0.90**／亚热带 **0.84**／温带 **0.80**／寒带 **0.88**（温带目标占比 ≈7.3%）；寒带因 `woodPile w=1.00` 使 `Σ` 变小 ⇒ **实测校准** | `MapGenRulesConfig.cs:44-54`（`resourceWeights[4]` 现值 mine=0.60/0.50/0.35/0.60） |
| **A3** | 新增 `bandResourceAbundance[4]`＝0.9/1.3/1.1/0.7（索引 0/1/2/3＝热带/亚热带/温带/寒带），乘在 `T` 上 | 新增字段＋`MapGenRules.cs:880`（`ComputeQuota` 唯一 `T` 解算点） |
| **A4** | 坑位模型**两处**：`PlaceResourceQuota:806-815` ＋ `EnsureChunkResourceQuota:1003-1011`；每 Cell ≤4 资源；`PickSubSlot:845` 返回值**真正参与落位** | `MapGenRules.cs:761-842`（落格）／`:958-1014`（补足） |
| **A5** | 移除 `EnsureNearbyResources:1203` ＋ `EnsureBlock:1221` ＋ 参数 `resourceGuaranteeRadius`（`MapGenRulesConfig.cs:86`）＋ **调用点清零**（`WorldManager.cs:182`＋`Valley_HH272_MapGenProbe.cs:46`）| grep 全仓调用点＝2 处（见 §六） |
| **A6** | `DeriveNaturalBuildings:1367` 白名单加 `FeatureType.Mine`，`w/h=2×2`；**每 2×2 簇只派生 1 个 `NaturalBuilding`**（对齐 `BuildingFactory.InstantiateFromMap` 按 nb 建 1 个实体）| `MapGenRules.cs:1367-1386`／`GridTypes.cs:109-116`（`NaturalBuilding.w/h` 已有字段） |

---

## §二 编号事项（回应任务书 §五.4）

- 任务书 §五.4「补登记 `HH.289`／`HH.290`」**已被用户 2026-09-15 裁决取代**：`_编号登记.md` 水位线行现记「**`HH.289`／`HH.290`＝废弃号**（探针文件 `Valley_HH289_EcoProbe.cs`／`Valley_HH290_GatherProbe.cs`；用户裁：**不再登记、作废**，待其亲自主持的全面审计时重排）」⇒ **不再补登记**（照裁决执行，不逆行）。
- 本批：任务书/开工回执＝**HH.291**（同号·`HH.272` 先例）；交付报告按实时水位线取号。

---

## §三 `sim-sync` 核查结论＝**零义务**（不默认，已取证）

- 全工作区 grep `MapGenRules|MapGenRulesConfig|bandResourceAbundance|resourcesPerChunkBase`（`*.cs`）⇒ **命中 19 文件，全部落 `Valley Rampart/Assets/{_Game/Systems/World, _Game/Systems/Rendering, _Game/Data, _Game/Systems/Building, _Game/Systems/Kingdom, Editor/**}`；`Assets/_Game/Systems/AI.Core/**` 命中数 = 0**。
- 语义侧：本批标的为**地形/资源落格（图表层）**，`AI.Core` 为**单位级战术决策核**（Attention/L1/L2/L3/Tuning），**无地图生成对应物**（sim 场景为固定构成 JSON）；`champion`／`factor_registry`／`TuningSnapshot` **零改**。
- ⇒ **零义务**（本批不写 sim 镜像、不动训练仓 harness、不进池、不切锚）。

---

## §四 排雷逐条自答（M1~M8）

| # | 自答 |
|---|---|
| **M1** | A2 改 `w` 经 `Σ` 归一化**影响该带全部 5 类资源占比** ⇒ 对照表按**四带全量**出（改前/改后 × 5 类 × 占比＋绝对数），**不只给温带**。 |
| **M2** | A3 丰度乘 `T` 与 M1 **叠加** ⇒ 对照表**四带分列**（每带 `T`／`E_i`／实测占比／实测数量），并单列「`T × abundance` 四带均值」（M7 复核 ≈120）。 |
| **M3** | A4 改 `rng` 序列（`PickSubSlot` 返回值入链＋打包改变消耗序）⇒ **地图会与改前不同**（预期内）。本批**不把"与改前同图"当判据**；判据改判为「**同 seed 两次生成逐格一致**」（`SameMap` 首差比对，含 `climateZones`）＋ 异 seed 出异图。 |
| **M4** | A1 资产重存 **须在编辑器内实操**（`AssetDatabase` 载入 → `EditorUtility.SetDirty` → `SaveAssets`）⇒ 交付**贴出重存后资产文件全文**（字段在场＝文件内可见，非"能读"）。 |
| **M5** | A6 新增 ≈1536 GameObject ⇒ 出**加载时长＋帧率对照**（改前/改后同 seed 同档，正门进局）。⚠️ 本会话若发生改前基线已丢失（工作区已含他端改动），以**同批次内 A6 开关前后对照**（临时停派生 Mine 跑一次 vs 开启跑一次）取证，并在报告声明口径。 |
| **M6** | A5＋A6 交互（取消就近补 ＋ Mine 实体化）⇒ 需证「主城旁仍能在合理距离内找到矿」：出**每个 spawn 到最近 Mine 簇的切比雪夫距离**（改前/改后），并列 `EnsureChunkResourceQuota` 逐区块有资源实证（`min 资源数/区块 > 0`）。 |
| **M7** | 三项数值叠加后 `T × abundance` 四带均值 = (108+156+132+84)/4 = **120.0** ✅（与 `resourcesPerChunkBase` 一致 ⇒ 全图总量基准不变）；报告出实测复核行。 |
| **M8** | 承 `L-23`／`L-29`／`L-30`／`L-34`／`L-35`：判据写**口径来源＋排除项**；不用"文本 grep 零命中"当无引用判据（`L-39`）；**禁为过线调参/换 seed**；长局观测走 `L-34` 五列（本批为编辑态直调＋短局，无 >10 分钟长局）。 |

---

## §五 ⚠️ A4 口径声明 ＋ **结构性缺口列报（报裁 R1）**

### 5.1 实读约束（决定实现形态的唯一硬约束）

- `MapData.features` 为 **`FeatureType[]`·逐格唯一值**（`WorldState.cs:27`，注释："唯一功能源"）；`MapData` 内**无任何逐格资源计数字段**（`WorldState.cs:20-32` 全字段已读）。
- ⇒ **同一 Cell 的 4 个坑位必须同型**，否则 features 无法表达（混合型会把同格其余资源**静默丢弃**，撞 `L-01`「就位≠生效」）。
- 本批**改动域＝`MapGenRules.cs`／`MapGenRulesConfig.cs`／`.asset`**（红线 1）⇒ **不能给 `MapData` 加逐格坑位计数字段**（那还要连带采集/刷新/锁格/渲染四层，属越域）。

### 5.2 本批实现口径（已按此施工）

| 口径项 | 取值 |
|---|---|
| 坑位容量 | **每 Cell 2×2＝4 个坑位**（`PitsPerCell=4`），**同格同型**（§5.1 约束） |
| 坑位账存活期 | 生成期（`PlaceResourceQuota`→`EnsureChunkResourceQuota` 两趟共享，生成结束不复用）——**生成期计数器口径**（对齐 `2_1_R1 §二` 验收线 4「坑位不重叠（**计数器实证**）」） |
| `PickSubSlot` | 返回值＝**格内子坑位序号（0..3）**，**真正参与落位**：按该序号起找**未占用**子坑位并置位（不再丢弃返回值） |
| 矿洞例外 | 沿用现行「**2×2 Cell 整簇**、每簇按 4 单位计入配额」（与改前 `TryStampMineCluster` 同口径，不动） |
| 补足口径 | `EnsureChunkResourceQuota` 的 `have[t]` 改按**坑位**计（非格数）；`target=max(round(E_t),B_t)` 不变 |

### 5.3 🔴 结构性缺口（列报·请裁 R1）

- 落地后**占格率**：T=120 坑位 → **≈30 Cell/区块**（与台账 F-04「46.9% → 11.7%」一致）。
- **但**：运行期**采集/刷新/锁格全部按"格"计**（`ResourceRespawnSystem.cs:115` 按格判 `Tree` 并 `RecordDepleted`；`WorldManager.TryConsumeResourceNode:232` 按格置 `Plain`）⇒ **一格只能被采 1 次** ⇒ 「坑位=资源实例」在**运行期不可见** ⇒ **可采资源总数 ≈ 占格数 ≈ T/4**。
- 影响链：木/石/矿脉/木堆**可采节点数 4× 下降** → 直接冲击 AI 经济（`HH.284/285` 已证四国资源链脆弱）。

**请裁（R1）**：

- **(A) 接受现状**：生成期坑位落地，运行期仍按格采 ⇒ 可采总数 = T/4（**不推荐**：与「T=120 保留不变 ⇒ 总量不变」的设计意图相悖）。
- **(B) 另批加「坑位消费层」（推荐·治本）**：`MapData` 增逐格坑位数/子坑位掩码 + 采集/刷新/锁格/渲染四层按坑位粒度 —— 属**跨域批**（超本批红线），本批只交付生成侧。
- **(C) 改判不实现坑位**：回到「1 资源 = 1 Cell」＋ `T=60` ⇒ 占格 23.4%（= 台账 F-04 备选，同样达成"视觉变稀"，且**总量与可采数一致**、无消费层缺口）。

> 本批**按任务书已裁口径继续施工**（A4 生成侧落地），R1 只决定**"坑位消费层"归属**，不阻断本批其余 5 项。

---

## §六 A5 调用点清单（全仓 grep 实证＝2 处）

| # | 位置 | 性质 | 处置 |
|---|---|---|---|
| 1 | `WorldManager.cs:182` | 业务管线（步骤 7） | **删该行**（任务书 §一 A5 明文授权"同步清理 `WorldManager.cs:187` 附近的调用"） |
| 2 | `Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs:46` | 观测/验收容器 | **删该行**（`EnsureNearbyResources` 删除后必须同步，否则编译失败；属**强制连带改动**，报告列报） |

`resourceGuaranteeRadius` 另有 2 处：`MapGenRulesConfig.cs:86`（字段定义，删）＋ `MapGenRules.EnsureNearbyResources:1205`（唯一消费点，随之删除）；`MapGenRulesConfig.asset:21`（序列化旧值，重存后自然消失）。

**保留**：`EnsureChunkResourceQuota:958`（逐区块补足）／`InClearZone:1257`（`TryStampMineCluster:859` 仍用）／`HasFullBlock`／`IsClearBlock`／`StampBlock`／`InFullBlock`。**随之删除**：`EnsureBlock:1221`（唯一调用者＝`EnsureNearbyResources`）＋ `BlockHasFeature:67`（唯一调用者＝`EnsureBlock`）。

---

## §七 施工序（本批一次串完）

1. `MapGenRulesConfig.cs`：A2 四带 `mine w` ＋ A3 `bandResourceAbundance[4]`（含查表辅助）＋ A5 删 `resourceGuaranteeRadius`
2. `MapGenRules.cs`：A3 `ComputeQuota` 乘丰度 ＋ A4 两处坑位 ＋ A5 删 `EnsureNearbyResources`/`EnsureBlock`/`BlockHasFeature` ＋ A6 `DeriveNaturalBuildings` 加 Mine
3. 连带：`WorldManager.cs:182` 删行 ＋ `Valley_HH272_MapGenProbe.cs:46` 删行
4. 编译（`refresh_unity compile=request` ＋ `read_console` ⇒ 新增 error = 0）
5. **A1 编辑器内重存资产** → 贴资产全文
6. 新探针 `Valley_HH291_MapGenProbe.cs`（观测域增量·列报）：四带对照表／T 实测／坑位存在性／Mine 实例数／主城↔矿距离
7. 正门 `EnterTestRun` 进局（`M5` 加载时长＋帧率；`A6` 石入库读数）＋ 退 Play
8. 收尾：交付报告（按水位线取号）＋ 具名 `git add`／`commit`（**不 push**）

---

> 本回执落盘即生效；施工按 §七 推进。
