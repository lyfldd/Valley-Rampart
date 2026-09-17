# HH.303 · `HH.294` 片 5（资源池落地）**5-0 参数映射表 ＋ 口径报裁**

> **端**：执行端｜**日期**：2026-09-17｜**依据**：`HH.294_底层重构批_任务书` §二 片 5 ＋ `03_地图即数据库` §6.7/§6.8 ＋ `底层执行计划` 批 4
> **状态**：⛔ **停手报裁** —— 5-0 硬性顺序要求「先报参数映射方案再施工」；实读发现 **4 处口径反例**（任务书自载「若实读发现反例 ⇒ 停下报裁，禁自行改口径」）⇒ **4-A~4-F 一行未写**。
> **本片已落**：仅 `R4`（`MapGate.QueryCells` 成本注释 · 纯注释零逻辑，任务书搭车项）。
> **范围排除声明**（`L-35`）：本批**未碰**片 6（双写收敛／`Collider2D`／`PickAt`）／**未碰**中层（`05`/`06`/`07`/`08`/`#14`）／**未碰** `F-14`（`BuildingFactory.cs:97/116/145` 三处 `grade: ResourceGrade.Normal` 原样）／**未碰** `AI.Core`（命中 0）。

---

## 〇、结论（一句话）

**5-0 映射表已出（§一 ＋ §四）；但 `03` §6.7.1 的「权重／占比」表与实盘 SO 资产不是同一套**（§二 · `P-1`）——
按实盘权重算，**判据 4「满池树 ≈36／矿洞 ≈2」不可达（实为 28.8／7.7）**，且任务书自载的「树上限 54／矿洞上限 3」亦然。
另 3 处须裁（§二 `P-2`~`P-4`）。⇒ **请裁决 P-1 后方可开工 4-A~4-F。**

---

## 一、5-0 交付物 ①：现行字段 → 新参数 映射表

> 口径源：`03_地图即数据库` §6.7（已定案 `D762`）｜实读源：`MapGenRules.cs`（2026-09-17 逐行实读）

| # | 现行（`file:line` · 实读） | 现行语义 | 新参数（`03` §6.7） | 映射动作 | 状态 |
|---|---|---|---|---|---|
| 1 | `MapGenRulesConfig.cs:42` `resourcesPerChunkBase = 120` | T 基准（**期望量**·坑位口径/大区块） | `poolCapBase = 96` | 语义由「期望量」→**「上限」** | ⚠️ **`P-2`** |
| 2 | `MapGenRules.cs:962` `T = Max(1, cfg.resourcesPerChunkBase)` | T 起点 | `cap(band) = 96 × GetBandAbundance(band)` | 基准改 96 | ✅ 可落 |
| 3 | `MapGenRules.cs:963` `T *= GetBandAbundance(band)` | 丰度**乘在期望**上 | 同上（**移入 cap**） | 语义迁移：期望 → 上限 | ✅ 可落 |
| 4 | `MapGenRules.cs:964-967` `T *= difficultyResourceScale[di]` | 难度**乘在期望**上 | **上限定案「不乘难度」** | ⚠️ 新模型**无消费者** ⇒ 悬空（`P-3`） | ⚠️ **`P-3`** |
| 5 | `MapGenRules.cs:968` `guaranteeRatio = 0.5` | `B_i = floor(E_i × 0.5)` | `B_i = floor(E_i^**当前目标** × ratio)` | 基准由「满池 E_i」→「当前目标」 | ✅ 可落（须改基准） |
| 6 | `MapGenRules.cs:975-978` `r_i = 1−w_i`；`p_i = r_i/Σr` | 占比 | `p_i` **复用**（单类上限 `cap × p_i × relax`） | **复用不重写** | ⚠️ **`P-1`** |
| 7 | `MapGenRules.cs:982` `e[i] = T × p_i` | 期望量 E_i | `E_i^open = cap × p_i × 0.4` | 基准换算（40% 开局） | ✅ 可落 |
| 8 | `MapGenRules.cs:983` `b[i] = floor(e[i] × ratio)` | 保底 B_i | 同左（基准换「当前目标」） | ✅ 可落 | ✅ |
| 9 | `MapGenRules.cs:986` `b[ResMine] = 0` | 矿洞退出保底（`HH.293 B3`） | **保留** | 不动 | ✅ |
| 10 | （**无**） | — | `relax`（松弛系数）= **1.5** | **新增字段**（本端裁·可调） | ✅ 新增 |
| 11 | `RespawnConfig.cs:28` `daySeconds = 3f` | **第二日历**（120× 偏差源） | **删** | 删字段 | ✅ 可落 |
| 12 | `ResourceRespawnSystem.cs:45` `_elapsed`／`:58`／`:75-76` | 自攒秒 → 自算天 | **删** | 删字段＋删自攒 | ✅ 可落 |
| 13 | `ResourceRespawnSystem.cs:32/40` `dueGameDay`（**float**） | 秒折算的游戏天到期点 | `dueDay`（**int**·绝对游戏天） | 类型＋语义变更 | ✅ 可落 |
| 14 | `ResourceRespawnSystem.cs:71` `Update()`（**每帧**） | 每帧推进倒计时 | **删** → `OnDayChanged(TimeDayChangedEvent)` | 频率：每帧 → 每天 1 次 | ✅ 可落 |
| 15 | `ResourceRespawnSystem.cs:96` `RebuildEntity(e.cell, e.feature)` | **原地**复活 | 落点器**随机新位**；兜底＝原位 | 改调用 | ✅ 可落 |
| 16 | `ResourceRespawnSystem.cs:43-44` `_data`／`_entity` 双列表（按**格**记） | 记格 | 记**区块**不记格 | ⚠️ 存档结构变更 | ⚠️ 需声明 |
| 17 | （**无**） | — | **池子模型 ＋ 单类上限 ＋ 每日补量 ＋ 分帧分摊** | **全新增** | ✅ 新增 |

**⚠️ 未发现「`E_i`/`B_i` 被别处复用」**（任务书预警项）：`e[]`/`b[]` 均为 `PlaceResourceQuota:817-818` 与 `EnsureChunkResourceQuota:1047-1048` 的**函数内局部数组**；`ComputeQuota` 有 **2 个生产码调用点**（`:825`／`:1055`）——**同一函数被两处调**，改算式须两处同步（本端已核，非阻塞）。

---

## 二、⚠️ 口径反例 4 处（**停手报裁**）

### `P-1`（**最重·必裁**）· `03` §6.7.1 的「权重／占比」表 ≠ 实盘 SO 资产

**任务书原文自载**：「`p_i` ＝复用现行 `ComputeQuota` 的 `r_i/Σr` 式（它算出的树占比 **34.0%** 与 `03` §6.7.1 表**吻合**，已核）」
⇒ **实读否证**：该 34.0% **只在另一套权重下成立**，实盘算得 **27.3%**。

| 类 | `03` §6.7.1 表（权重／占比） | 实盘 `MapGenRulesConfig.asset`（温带·权重） | 实盘占比 | 差 |
|---|---|---|---|---|
| 树 | **0.20** ／ 34.0% | `.asset:36` `tree: 0.25` | **27.3%** | −6.7 pt |
| 石堆 | **0.50** ／ 21.3% | `:37` `stonePile: 0.40` | 21.8% | +0.5 |
| 木堆 | **0.30** ／ 29.8% | `:38` `woodPile: 0.40` | 21.8% | −8.0 |
| 矿脉 | **0.70** ／ 12.8% | `:39` `oreVein: 0.40` | 21.8% | +9.0 |
| 矿洞 | **0.95** ／ **2.1%** | `:40` `mine: 0.80` | **7.3%** | **+3.5×** |

**复现命令（本端自跑）**：
```
python -c "w=[0.20,0.50,0.30,0.70,0.95]; r=[1-x for x in w]; s=sum(r); print(r, s, 100*r[0]/s)"
⇒ [0.8, 0.5, 0.7, 0.3, 0.05] 2.35 34.04…
python -c "w=[0.25,0.40,0.40,0.40,0.80]; r=[1-x for x in w]; s=sum(r); print(r, s, 100*r[0]/s)"
⇒ [0.75, 0.6, 0.6, 0.6, 0.2] 2.75 27.27…
```
⇒ §6.7.1 表用的权重 `{0.20/0.50/0.30/0.70/0.95}` ＝ 台账 **§六 `R-03` 方案 A′ 的用户诉求权重**（矿山 95%／矿脉 70%／石头 50%／木头 30%），**从未落盘**；实盘是 `HH.291/292` **实测校准**过的一套（台账 §851）。

**后果（判据不可达）**：`03` §6.7.1 与任务书均以 **cap＝106（温带）** 列值。逐带实算（本端）：

| 带 | cap | 树·占比 | **树·满池** | **矿洞·满池** |
|---|---|---|---|---|
| 热 | 86.4 | 32.0% | 27.6 | 3.5 |
| 亚热 | 124.8 | 29.5% | 36.8 | 7.4 |
| **温** | **105.6** | **27.3%** | **28.8** | **7.7** |
| 寒 | 67.2 | 27.0% | 18.2 | 3.6 |

⇒ **判据 4「满池树 ≈36／矿洞 ≈2」按实盘权重不可达**（温带实为 **28.8／7.7**；四带均值 27.9／5.5）。
⇒ 任务书自载的「温带示例：树上限 54／矿洞上限 3」（＝`cap×p_i×1.5`）**同样只对 §6.7.1 权重成立**；实盘为 **树 43.2／矿洞 11.5**。

**⇒ 请裁（本端§未自选边）**：
**(A)** 把 `resourceWeights`（**4 带 × 5 型**）改成 §6.7.1 那套 `{0.20/0.50/0.30/0.70/0.95}`（**全带统一**？还是只改温带？§6.7.1 只给了温带一列）——**这是改 SO 数据面**，且会推翻 `HH.291 A2`／`HH.292` 的实测校准结论；
**(B)** 保留实盘权重，**判据 4 的期望值按实盘重算**（树 ≈28／矿洞 ≈8）——但这样「矿洞最少」的诉求（§6.7.1 论证的**唯一目的**）不成立（矿洞 7.7 个/区块 ≠ "≈2 簇"）。

> ⚠️ 二者互斥且都改判据 —— 本端**不自选**。**P-1 未裁 ⇒ 判据 2/4 的期望值无法钉死 ⇒ 4-A~4-F 不可施工。**

### `P-2` · `resourcesPerChunkBase` **并非「仅本处消费」** ⇒ 按任务书 ⑤ 须报裁

**任务书 ⑤ 原文**：「若**仅本处消费** ⇒ **删除**；若另有消费者 ⇒ **列出来报裁，禁擅自删**」

**实读消费者清单（`git grep` 输出片段）**：
```
$ git grep -n "resourcesPerChunkBase" -- "Valley Rampart/Assets/**/*.cs"
Valley Rampart/Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs:136:        float T = cfg != null ? cfg.resourcesPerChunkBase : 120f;
Valley Rampart/Assets/Editor/Smoke/Valley_HH272_MapGenProbe.cs:357:        sb.AppendLine($"配置：resourcesPerChunkBase={cfg.resourcesPerChunkBase} ...
Valley Rampart/Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs:75:        float T = cfg != null ? cfg.resourcesPerChunkBase : 120f;
Valley Rampart/Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs:145:        sb.AppendLine($"  [{label}] **M7**：T×abundance 四带均值 = ...
Valley Rampart/Assets/Editor/Smoke/Valley_HH291_MapGenProbe.cs:264:        _log.AppendLine($"配置：resourcesPerChunkBase={cfg.resourcesPerChunkBase} ...
Valley Rampart/Assets/_Game/Data/MapGenRulesConfig.cs:42:    public int resourcesPerChunkBase = 120;
Valley Rampart/Assets/_Game/Systems/World/MapGenRules.cs:697:    /// ② 资源按「坑位模型 + 权重表归一化配额」落位（T=`resourcesPerChunkBase` × 难度系数，保底 B_i）。</summary>
Valley Rampart/Assets/_Game/Systems/World/MapGenRules.cs:808:    /// <summary>资源配额落格（件②）。每大区块：T = `resourcesPerChunkBase` × **地带丰度** × 难度系数；
Valley Rampart/Assets/_Game/Systems/World/MapGenRules.cs:962:        float T = cfg != null ? Mathf.Max(1f, cfg.resourcesPerChunkBase) : 120f;
```
**生产码真消费 ＝ 1 处**（`MapGenRules.cs:962`）＋注释 2 处（`:697`／`:808`）；
**但另有 3 处 Editor 探针消费**（`HH272Probe:136`／`HH291Probe:75`／`HH291Probe:145,264`）——**删除会令两探针编译失败**。
⇒ 按任务书 ⑤「另有消费者 ⇒ 列出来报裁，**禁擅自删**」⇒ **本端不删，报裁**。
**⇒ 请裁**：**(A)** 删字段 ＋ 同步改两探针（探针属历史批次产物，改后其 M7 断言口径需重写）；**(B)** 保留字段但**改语义/改名**（如 `legacyResourcesPerChunkBase` ＋ 注释「已被上限口取代」）；**(C)** 上报为「探针过时」另批处理。

### `P-3` · `difficultyResourceScale` 在新模型下**悬空**

任务书 ① 明写「上限**不乘**难度系数（难度系数**保留给旧的落格口径**，见 ④）」；但 ④ 把落格目标改为 `E_i^open = cap × p_i × 0.4`（**cap 已不含难度**）⇒ **新模型内 `difficultyResourceScale` 无任何乘入点**。
实读其现有消费者：`MapGenRules.cs:965-967`（生产码 1 处）＋ 两探针（`HH272Probe:138-139`／`HH291Probe:78-79`）。
⇒ ⚠️ 「保留给旧落格口径」**与**「新模型取代落格口径」**自相矛盾** ⇒ **难度系数去哪**（继续乘 cap？乘期望？彻底退役？）**未定**。

### `P-4` · `EnsureChunkResourceQuota`（步骤 6.6）在新模型下的角色未定

现行步骤链（`WorldManager.cs:183-202` 实读）：步骤4 `PlaceResourceQuota`（铺 E_i＋保底）→ **6.5 `ClearKingdomZones`**（净空区清资源）→ **6.6 `EnsureChunkResourceQuota`**（按 `target = max(round(E_i), B_i)` **补回**）。
新模型下 6.6 的 `target` 应取什么（**当前目标**＝40% → 还是随池子日增？）——若沿用 `E_i`（满池），**会把 6.5 清掉的又补满 ⇒ 40% 开局被顶穿**（正是任务书 ④ 警告的同一类坑，但 ④ 只说保底，未说 6.6）。实读 `:1088` `int target = Mathf.Max(Mathf.RoundToInt(e[t]), b[t]);` ⇒ **确会顶穿**。

---

## 三、5-0 交付物 ②：`resourcesPerChunkBase` 消费者清单（**函数级 ＋ 调用面**两列）

| 消费者（`file:line`） | 面 | 调用面／触发 | 删字段影响 |
|---|---|---|---|
| `MapGenRules.cs:962` `ComputeQuota` | **生产码·函数级** | 被调 2 处：`:825`（`PlaceResourceQuota`）／`:1055`（`EnsureChunkResourceQuota`）；两者各被 `WorldManager.cs:184`／`:196` 生成期各调 1 次/图 | 须同步改算式（本片本就要改） |
| `Valley_HH272_MapGenProbe.cs:136` | Editor 探针 | 手动/冒烟触发（非运行期） | ❌ **编译失败** |
| `Valley_HH272_MapGenProbe.cs:357` | Editor 探针（打印） | 同上 | ❌ **编译失败** |
| `Valley_HH291_MapGenProbe.cs:75` | Editor 探针 | 同上 | ❌ **编译失败** |
| `Valley_HH291_MapGenProbe.cs:145,264` | Editor 探针（打印） | 同上 | ❌ **编译失败** |
| `MapGenRules.cs:697`／`:808` | 注释 2 处 | — | 措辞更新 |

**处置**：⛔ **本端不删**（任务书 ⑤：另有消费者 ⇒ 报裁）—— 见 `P-2`。

---

## 四、搭车 ＋ 取证（本片必做 · 只读）

### `R4` ✅ 已落 · `MapGate.QueryCells` 接口旁成本特征注释（**只加注释·零逻辑改动**）

改后原文（`MapGate.cs:145-154`）：
```
/// <summary>⭐<b>区域枚举</b>（§8.2 ③）：范围（矩形）＋ 条件 ＋ 输出 buffer（**禁分配**：buffer 由调用方复用）。
/// 返回写入条数。原 `FillUnitsInRect` 只服务单位 ⇒ 本口**通用**（地表／可走／空置／有物皆可筛）。
///
/// <b>⚠️ 成本特征（`03` §8.7 判据 7 · `D772` 实测 · 禁每帧调用）</b>：
/// 每格复合 **6 项**（地表／气候／可走／占格／归属／单位数）⇒ 成本 **O(格数 × 单位数)**；
/// 实测 **2401 格 · N=23 ⇒ ≈7.46 ms**（45% 帧预算）、N=500 ⇒ 76.88 ms（463%）。
/// 其中单位计数（`GetUnitCountInCell`·O(单位数)）占 ≈46%，余 ≈4 ms 来自归属查／占格查／组包
/// ⇒ ⚠️ **仅把单位计数治本为 O(1) 仍约需 4 ms（24% 帧预算）**。
/// ⛔ **禁在 `Update`／每帧路径调用**；**启用前须先治本**（块级单位计数 ⇒ O(1) → 按需取事实），
/// 治本批须给「正确性读数 ＋ 代价读数」两栏。</summary>
```
判据 11 满足：注释在场；**逻辑零改**（`git diff` 仅新增 8 行注释，无代码行变动）。

### `F-15` ⭐ 取证结论（**只读·未改任何代码**）· 9 座建筑 footprint 重叠 ＝ **缺陷**，非允许语义

**结论**：**缺陷** —— 地图**生成期预置建筑路径绕过了 `03` §6.5「底层校验①不重叠」**。落点器只避「地形不可走」，**从不查占格**。

**证据链（逐处实读）**：

| # | 证据 | 说明 |
|---|---|---|
| 1 | `KingdomFoundry.cs:170` `Vector2Int cell = BuildingCell(map, spawn, k, buildCount);` → `:176-180` `CreateBuildingInstance(...)` **直建** | 预置建筑**不经** `PlacementValidator`（该走的是玩家/AI 建造路径） |
| 2 | `KingdomFoundry.cs:195/209` `BuildingCell` 尾行 `return MapGenRules.NearestWalkable(map, x, y);` | 取点＝「**地形可走**」**单条件** |
| 3 | `MapGenRules.cs:1270-1288` `NearestWalkable` | 全文只判 `IsWalkableFeature(...)`（`:1275`／`:1284`），**零占格判定**（不读 `_occupants`／`BuildingRegistry`） |
| 4 | `MapGenRules.cs:1277-1285` **环形外扩取最近** | ⭐ **两个不同意图点可吸附到同一可走格** ⇒ 直接产出重叠（生成期主要机制之一） |
| 5 | `KingdomFoundry.cs:227` `var placed = new List<Vector2Int>();` ＋ `:257` `if (placed.Contains(p)) return;` | ⭐ 围墙环**只对「墙」去重**（`placed` 仅收墙/门）⇒ **墙可落进建筑格**（次要机制） |
| 6 | `BuildingFactory.cs:232` `MarkOccupiedFootprint(...)` → `GridSystem.cs:380` `_occupants[i] = occupant;` | 写口**无条件覆写**，**零冲突检测** |
| 7 | `BuildingRegistry.cs:69` `_byCoord[...] = b;`（`RegisterFootprintCells`） | 注册表**后注册者覆盖**；`:63` 注释自认「**正常流程校验已排除重叠**」——而生成期流程**没有**该校验 ⇒ 注释的**前提在生成期不成立** |
| 8 | `PlacementValidator.cs:93-100` `occupant != null ⇒ Blocked` | 对比证明：**「不重叠」只在玩家/AI 建造路径存在**，生成期缺失 |

**⇒ 「重叠时占格归谁」**：**后写者胜**（`_occupants` 覆写 ＋ `_byCoord` 覆写），**静默**。
⚠️ **并附一条衍生缺陷**：`BuildingRegistry.cs:26-34` `Unregister` **无条件** `_byCoord.Remove(...)` 全部 footprint 格 ⇒ 重叠格上「先注册者的注销」会**抹掉后注册者的反查项**（B 仍站着，却查不到）⇒ 重叠格在任一建筑死亡后**索引错乱**。
**⇒ 建议**：本项**升格为待修缺陷**（归属＝生成期落点器缺「占格校验」），但**修复属另一批**（任务书 ⛔ 未授权本片改建图生成落点逻辑）。

**复现路径（供策划端复核）**：`Logs/hh294_slice4_probe.log:51`（`全覆盖 = 11883｜部分 = 9`）＋ `Logs/hh294_slice3_probe.log:40,43`（`抽样 60 座：有登记 60｜全覆盖 52｜部分 8`）。

### `R5` ⭐ 预算重估（子格域 `flood-fill` **143.54 ms** · 复跑 154.10）

**问**：本片是否改变该路径触发频率？
**答：不改变。** 依据（实读）：
- 子格域 flood-fill **唯一在场点**是 Editor 探针（`Valley_HH294_Slice3Probe.cs:336` `IsSubWalkable(...)`）；**生产码零调用**（`git grep -n "IsSubWalkable"` ⇒ 生产码命中均系**单点**判定：`FormationController:550`／`CombatRules:68`／`SpawnPosSnapper:36,60`／`PathFollower:145`／`PathfindingService:31` —— **无全图 flood**）。
- 生产全图 BFS ＝ `MapValidator.ValidateConnectivity`（**地块级**·`MapValidator.cs:17-34`），调用面**仅 2 处**：`WorldManager.cs:198`／`:200`（**每图生成期各 1 次**）。
- 本片 4-A~4-F 的改动面（配额/重生/时间基准）**均不触及上述两者**；新增的「每日补量」走**地块级 `features`**（非子格域）。
⇒ **本片不改变该路径触发频率 ⇒ `R5` 新读数 ＝ 不产生**（沿用 143.54／154.10 ms 观测底座，**仍属「生产未走」**）。

---

## 五、未完成项（**显式列出** · 任务书红线 ⑥）

| # | 项 | 状态 | 原因 |
|---|---|---|---|
| 1 | **4-A** 删第二日历（`daySeconds`／`_elapsed` → `TimeManager.CurrentDay`＋`TimeDayChangedEvent`） | ⛔ **未开工** | 停手报裁（`P-1`~`P-4`） |
| 2 | **4-B/4-C/4-F** 开局 40% ＋ 锚点统计 ＋ 上限 96×丰度 ＋ 单类上限 | ⛔ **未开工** | ⚠️ 依赖 `P-1` 裁决（单类上限＝`cap×p_i×1.5` 的值取决于权重集） |
| 3 | **4-D** 重生随机落新位（＋兜底原位） | ⛔ **未开工** | 停手报裁 |
| 4 | **4-E** 池子模型 ＋ 每日补量 ＋ 分帧分摊 | ⛔ **未开工** | ⚠️ 依赖 `P-4`（6.6 角色） |
| 5 | 判据 1~14 的**实测读数**（开局 38／第 7 天 96／单类上限／非原位重生／一天一次／分帧／锚点配额） | ⛔ **未产生** | 同上（**无代码改动 ⇒ 无读数可测**；本端**不伪造读数**） |
| 6 | 编译 0 error／同 seed 逐格一致／正门三态 | ⛔ **未执行** | 同上（唯一落地项 `R4` 为纯注释，**未变量为代码**） |
| ✅ | `R4`（`QueryCells` 成本注释） | ✅ **已落** | 纯注释·零逻辑 |
| ✅ | `F-15` 取证（只读结论 ＋ 8 条证据链） | ✅ **已交** | 见 §四 |
| ✅ | `R5` 预算重估 | ✅ **已交** | 见 §四（结论＝频率不变） |

---

## 六、本端自陈（诚实面）

- **无任何 4-A~4-F 代码改动**：`git status` 本片改动面 ＝ **1 文件**（`MapGate.cs`·8 行注释）＋ 本报告。
- **未删 `resourcesPerChunkBase`**（尽管任务书 ⑤ 的默认动作是删）——因实读发现探针消费者，按 ⑤ 后半句**报裁**。
- **未采样任何"看起来对"的数字**：判据 2/3 的总量口径（38／96）虽与权重无关（＝`cap×0.4`／`cap`），但**判据 4 的 `p_i` 强依赖权重** ⇒ 本端**不先斩后奏**。
- ⚠️ **本报告 §二 的算术均系本端实算**（命令与输出已在文中）；**权重值系直读 `.asset` 原处**（`:36-45`），非从文档转抄（遵 `L-40` 新维度）。

---

## 七、请裁汇总（**待策划端裁决后开工**）

| # | 请裁 | 本端倾向（**仅建议·不代裁**） |
|---|---|---|
| **`P-1`** | 权重表取 §6.7.1 那套（改 SO·推翻实测校准）**or** 判据 4 期望值按实盘重算（"矿洞最少"失守） | ⛔ **不自选**（两者都改判据，须口径方定） |
| **`P-2`** | `resourcesPerChunkBase` 删/改名/保留（探针消费者 3 处） | 建议 **(B) 改名保留**（零编译面破坏，语义标注清楚） |
| **`P-3`** | `difficultyResourceScale` 在新模型中的落点（继续乘 cap／乘期望／退役） | 建议 **继续乘 cap**（保难度曲线），但**须口径方确认**是否与「上限不乘难度」冲突 |
| **`P-4`** | 步骤 6.6 `EnsureChunkResourceQuota` 的 `target` 口径（当前目标 vs 满池） | 建议 **改用「当前目标」**（与 ④ 保底同基准，防 40% 被顶穿） |
