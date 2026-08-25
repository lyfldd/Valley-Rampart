# BUGFIX：读档建筑双份（双根因修复）修复记录

> 日期：2026-08-25
> 涉及文件：`SaveManager.cs`、`WorldManager.cs`、`BuildingFactory.cs`
> 验证工具：`Assets/Editor/Smoke/Valley2_16_Smoke_BuildDup.cs`、`Valley2_16_SmokeVerify.cs`

---

## 背景与定性

真实玩家读档链路（`SaveManager.Load`）在同一次 Load 内把除玩家建筑外的自然建筑/主城各重建了两份（翻倍）；真实读档还会把第一代 AI 王国再立一遍（叠格双份）。两者症状同族（读档重复生成实体）、同验证链路（三合一①不 PASS 卡收不了口）、拆卡无收益，故本卡由**单根因卡变为双根因卡**。

> 定性：根因一 2_14 既有；根因二 2_16 引入（读档路径、缺新游戏门控）。
> **两笔账，不混成"都是既有 bug"。**

---

## 根因一：A/B 双路径翻倍（2_14 既有）

### 现象

读档后自然建筑 + 主城数量翻倍。

### 根因链

```
SaveManager.Load（同一次 Load 内先后跑两条重建路径）
  ├─ 阶段1 Global → WorldManager.LoadState → GenerateWorld → InstantiateFromMap（路径 A）
  │      用「新随机 GUID + 默认 kingdomId」重建自然建筑 + 主城
  └─ 阶段1.5 → BuildingFactory.SpawnFromSave（路径 B）
         用「旧存档 GUID」重建存档里全部建筑（带正确血量/等级/kingdomId）

自然/城堡建筑被 A、B 各建一份 → 翻倍；玩家建筑只经 B，不翻倍。
```

- 路径 A 无 `HasSaveable`（存档侧去重）也无法救：同一栋建筑两路径 saveId 是**新随机 GUID vs 旧存档 GUID**，`HasSaveable(旧id)` 恒 false。
- git 考古：路径 A 引入 2026-07-29 `31673197`；路径 B 引入 2026-08-06 `5c6ef5e9`（王国经营 3.5）。均早于 2_16 → **独立于本卡原有流程，2_14 既有**。

### 方案裁决（策划批准）

- **主方案 A**：v2 读档跳过路径 A 的建筑实例化，路径 B 全权重建（带正确 kingdomId/血量/等级）。`GenerateWorld` 拆分保留「数据/网格」生成（tile/feature/种子派生，`System.Random(seed)` 确定性），仅 `InstantiateFromMap` 按门控跳过。
- **方案 C 否决**："网格已有 occupant 则保留现有" 会把显性 bug（可见双份）换成隐性数据腐坏（A 的新随机 GUID + 默认 kingdomId 被保留、存档里正确那份被静默丢弃——归属错 + GUID 错，复活 P0 传送门排除集污染）。
- **旧档版本门控**：`SaveManager.CurrentSaveVersion` 1 → 2。新存 v2 走新路径；历史 v1 旧档走 legacy（A+B 现状、可容忍）。宁保守：legacy=现状可容忍，新路径配不完备旧档=空世界（不可容忍）。
- **兜底意图不丢 → 响亮断言**（替代 C）：`SpawnFromSave` 实例化前查目标格 Building 占用，非空 → `LogError` 打双方 saveId，**不跳过、不吞**——把"存→读→再存→再读"复合腐坏链变成响亮报错。范围只查 Building（Portal/Chest 同为 IGridOccupant 不在此列，防误报）。

### 实现

1. `SaveManager.cs`：`CurrentSaveVersion` 1→2；新增 `LastLoadedSaveVersion`，Load 中在 Global 分发前记 `root.saveVersion`。
2. `WorldManager.cs`：`GenerateWorld` 增 `bool instantiateBuildings`(默认 true)；LoadState 按 `LastLoadedSaveVersion >= 2` → 新路径 `instantiateBuildings=false`。保留 `ClearAllBuildings`/`PopulateFromMap`/`EventBus.Publish(MapGeneratedEvent)`（不发=读档白屏回归，见 ⑤ 审计）。
3. `BuildingFactory.cs`：`SpawnFromSave` 在 `CreateBuildingInstance` 前加响亮断言。

---

## 根因二：FoundFirstGeneration 缺新游戏门控（2_16 引入）

### 现象

读档后出现 k4/k5/k6 三个本不存在的 AI 王国，其 castle/预置建筑叠回 k1/k2/k3 原格 → 同格双份、读档王国数漂移。

### 根因链

```
WorldManager.GenerateMap 末尾（2_16 步骤5 挂载，L177）
  └─ KingdomFoundry.FoundFirstGeneration(...)   ← 新游戏、读档都会执行
       读档时王国本应由 KingdomRegistry.LoadState 从存档恢复
       但 GenerateMap 每次重建（含 Load 的 GenerateWorld→GenerateMap）都再立 → k4/k5/k6
```

### 方案裁决（策划批准）

- **显式门控，禁止启发式**：`GenerateMap`/`GenerateWorld` 增 `bool foundKingdoms`（默认 true）。ApplyConfig（新游戏）走默认 true；LoadState 传 `foundKingdoms:false`。
- 不用 `Registry.Count>1 则跳过` 类状态推断——依赖读档时序，且对"0 AI 王国存档"会误判复跑。
- 新游戏链路零改动（默认 true，rng 链位置不变）→ 复用 `Valley2_16_SmokeVerify` 9 组合×2 局 canonical dump 复验通过（读档 rng 截断无害，存档不存 rng 状态，无跨版本破档面）。

### 同段兄弟副作用审计

| 挂载点 | 是否中招 | 依据 |
|--------|----------|------|
| `FoundFirstGeneration`（步骤5，L177） | **中招** | GenerateMap 每次重建都跑 → 加门控 |
| 初始流民预置（D308 步骤6） | **不中招** | 挂在 `GameBootstrap.StartNewGame()` 新建入口（`OnNewGameMapReady`），未挂 GenerateMap；读档走 `ContinueFromSave→LoadSave` 不触发。初始裁决"大概率同病"之假设在执行端被证伪 |
| `ResourceRespawnSystem.ResetRespawns`（HH.10，L180） | 不中招（放过） | 纯数据清账、同 seed 幂等，无累积副作用 |

---

## ⑤ 审计结论

- **MapGeneratedEvent 订阅方**：MapVisualizer/LODSystem/CameraRig/MapRenderService 全为地图/tile 渲染与相机 LOD，读 map 数据，不依赖 Building 实例 → 跳过 A 后照常发安全。
- **ClearAllBuildings**：保留（读档重建前需清残留）。

---

## ④ 教训条款（纪律）

**P0 步骤8 存读冒烟报了"读档 Registry 4 国全对"却没抓到再立国，原因：冒烟 harness 的读档路径 ≠ 真实 `ContinueFromSave` 链路。**

> **冒烟纪律：读档类验证必须走真 `SaveManager.Load`（全阶段 Global→1.5→Scene）链路；构造式/半链路验证（如仅 `SpawnFromSave` 单条）不作为读档回归的通过依据。** 与步骤11"必须走真 `InitializeNewGame`"同一条教训的读档版。

本卡 `Valley2_16_Smoke_BuildDup` 即用真 `SaveManager.Save/Load` 全阶段链路验证，且由此抓到了根因二。

---

## Play 三合一验证结果（ALL PASS）

| 项 | 结果 |
|----|------|
| ① 读档建筑数=存档记录数 | 2844 = 2844 OK |
| ① 无同格双份（无新增） | OK（基线已存在的 FOUNDRY/自然叠格不被计入新增） |
| ① 被砍树不复活 | OK |
| ① 归属一致（自然=-1） | OK |
| ① lastFoundingDay 存读回环 | 7=7 OK |
| ① 增补：读档王国数=存档王国数（无 k4/5/6） | 4=4 OK |
| ② 响亮断言响（构造同格冲突） | OK |
| ③ 新游戏确定性回归 | 9 组合×2 局 canonical ALL PASS |

### 附加观察（登记不修）

- 读档过程中响亮断言对少数**新建即存在的 FOUNDRY/自然叠格**（如 `(112,55)` 王国城堡叠自然矿石）也会触发 `LogError`。这是断言作为兜底网的预期行为（存在同格双占警告），非路径 A 回归——修复后 count 恒等于存档记录、无爆炸。该叠格为新建时 Foundry 与自然派生的确定性交错，属既有表现面，超本卡范围，登记不修。
- 上限 staging（`maxKingdomsGlobal=8`）与吞并 `TryAnnex` 构造验证：吞并判定为 2_17 占位恒假（`ResolveOwnerCampCell` 恒 -1，接线归 2_17 步骤12），本片只能做语法/入口级验证（已覆盖）；上限 runtime staging 需临时改 SO 资产，按 Step11 既定静态复核结论留档，非读档双份卡范围。

---

## 修改文件汇总

| 文件 | 修改内容 |
|------|---------|
| `SaveManager.cs` | `CurrentSaveVersion` 1→2；新增 `LastLoadedSaveVersion`（Load 前置） |
| `WorldManager.cs` | `GenerateMap`/`GenerateWorld` 增 `foundKingdoms` 显式门控；`LoadState` 传 `instantiateBuildings`(v2 跳过 A)+`foundKingdoms:false` |
| `BuildingFactory.cs` | `SpawnFromSave` 增加响亮断言（目标格 Building 占用→Error 双方 saveId） |
| `Assets/Editor/Smoke/Valley2_16_Smoke_BuildDup.cs` | 新增 Play 三合一冒烟（真 Save/Load 链路） |