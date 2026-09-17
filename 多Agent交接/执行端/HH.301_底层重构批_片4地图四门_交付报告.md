# HH.301 · 底层重构批 · 片 4「地图四门」交付报告（待策划验收）

> **端**：执行端　｜　**批次**：`HH.294` 底层重构批 · 片 4（承 `HH.300` 片3 / `D770` 验收销号）
> **口径源**：`最高优先级文档/03_地图即数据库.md`（§六~§九）＋ `04_上层接入指南.md`（§六 五条不许）
> **施工详规**：`最高优先级文档/底层执行计划.md` §二 批 2
> **交付时间**：2026-09-17　｜　**行尾**：本报告 LF
> **探针**：`Valley Rampart/Logs/hh294_slice4_probe.log`（PASS=39 / FAIL=0）

---

## 〇、一句话结论

片 4 九项（4-A~4-H）＋ 三项搭车（R1/R2/R3）**全部落地**；探针 **PASS=39 / FAIL=0**；编译 **0 error CS**（25 warning 全为本片前既存，逐条列于 §五）；确定性同 seed 逐格一致；`AI.Core` 命中 **0**；改动文件全 LF（`GameEvents.cs` 因预存 CRLF 走 python 二进制改，改后仍 CRLF，未污染）。

---

## 一、判据逐条读数

### 判据 1 · 4-A ⭐ 生产码 `features[]` 写点（逐文件计数 改前 → 改后）

**能力句**：**除 `MapGate.WriteRaw` 一处写内核外，生产码（`Assets/_Game/**`）无任何代码直接写 `features[]`。**

**复现命令**：

```bash
# 改后（含未跟踪 MapGate.cs；逐文件计数）
git grep -c -E "\.features\s*\[.*\]\s*=[^=]" -- 'Valley Rampart/Assets/_Game/**/*.cs'
Get-ChildItem -Recurse -Path "Valley Rampart/Assets/_Game" -Filter *.cs |
  Select-String -Pattern "\.features\s*\[.*\]\s*=[^=]"

# 改前（HEAD）
git grep -c -E "\.features\s*\[.*\]\s*=[^=]" HEAD -- 'Valley Rampart/Assets/_Game/**/*.cs'
```

| 作用域 | 改前（HEAD）逐文件计数 | 改后逐文件计数 |
|---|---|---|
| **生产码 `_Game/**`** | `MapGenRules.cs 9`／`MapValidator.cs 1`／`ResourceRespawnSystem.cs 1`／`WorldManager.cs 1` ⇒ **共 12 处 / 4 文件** | `MapGate.cs 1`（唯一写内核 `WriteRaw`）⇒ **共 1 处 / 1 文件** |
| **Editor 探针** | `Valley2_13_Smoke_AB.cs 2`／`Valley_HH140_Probe.cs 3` ⇒ **5 处 / 2 文件** | **不变**（本片不动，见下） |
| **第三方 / 其他** | `git grep -l "\.features\[" -- '*.cs'` 排除前两域 ⇒ **0 文件** | **0 文件** |

⭐ **作用域限定声明（判据 5 规矩）**：
- **生产码 `_Game/**`**：**12 → 1**（唯一命中＝`MapGate.cs:33 WriteRaw`）；**能力句在此作用域成立**。
- **Editor 探针面**：5 处写点**保留**（Editor-only，不在本片收口范围；见判据 1 附注）。
- **第三方**：0 命中。

**⚠️ 改前读数与提示词/任务书旧数不同**（提示词点出「63 处 / 10 文件」为 `D764` 旧读数已漂移；实测改前写点 **17 处 / 6 文件**，其中生产码 **12 / 4**，含 `MapValidator` 那处旧正则（`[^]]*`）会漏掉的 `cells[k]` 嵌套写法 —— 本报告用宽正则 `.*` 复核，两式结果已对齐）。
> **复算命令**（宽/窄两式并置，防正则漏计）：
> ```bash
> git grep -c -E "\.features\[.*\]\s*=[^=]" HEAD -- 'Valley Rampart/Assets/**/*.cs'
> git grep -c -E "\.features\s*\[[^]]*\]\s*=[^=]" HEAD -- 'Valley Rampart/Assets/**/*.cs'
> ```

### 判据 2 · 4-A 裸读面（逐文件计数 改前 → 改后）

**复现命令**：

```bash
git grep -c "\.features\[" HEAD -- 'Valley Rampart/Assets/_Game/**/*.cs'   # 改前
git grep -c "\.features\[" -- 'Valley Rampart/Assets/_Game/**/*.cs'        # 改后（已跟踪）
Get-ChildItem -Recurse -Path "Valley Rampart/Assets/_Game" -Filter *.cs |
  Select-String -Pattern "\.features\[" | Group-Object Path   # 含未跟踪 MapGate.cs
```

| 文件（生产码） | 改前 | 改后 | 处置 |
|---|---|---|---|
| `Rendering/MapRenderService.cs` | 5 | **0** | 5 处全改 `MapGate.ReadAt(map, x, y)` |
| `World/MapGenRules.cs` | 27 | **18** | 9 写点改 `GenesisWrite`；余 18 处为**生成期自身读**（见下注） |
| `World/MapValidator.cs` | 3 | **0** | 读走 `GenesisRead`／写走 `GenesisWrite` |
| `World/ResourceRespawnSystem.cs` | 3 | **0** | 幂等判定改 `MapGate.ReadAt`；`ConfirmTreeGather` 改 `GetFeatureAt` |
| `World/WorldManager.cs` | 3 | **0** | 删 `TryConsumeResourceNode` 整段；`IsResourceNodeAvailable` 改 `MapGate.GetFeatureAt` |
| `World/WorldGatherRegistry.cs` | 1 | **0** | 改 `MapGate.ReadAt` |
| `World/WorldGatherSource.cs` | 1 | **0** | 改 `MapGate.ReadAt` |
| `Grid/MapVisualizer.cs` | 1 | **0** | 改 `MapGate.ReadAt` |
| `Grid/GridSystem.cs` | 1 | **0** | `PopulateFromMap` 派生改 `MapGate.ReadAt` |
| `AI/Formation/GuardDeploymentSystem.cs` | 1 | **0** | 索引回读改 `MapGate.ReadAt` |
| `World/MapGate.cs`（**新建**） | — | **11** | 门自身（唯一读写内核所在） |
| **合计（真裸读）** | **46** | **29**（门内 11 ＋ 生成期 18） | ⚠️ 另 3 处命中为**注释行**（本片新增的说明文字，非真裸读：`MapRenderService.cs:181`／`ResourceRespawnSystem.cs:112`／`WorldManager.cs:261`） |

⭐ **能力句 + 限定**：「**除 `MapGate` 门自身与 `MapGenRules` 生成期（0→1 造世界）外，生产码无任何代码直接读 `map.features[]`**」。
- `MapGate.cs 11 处`＝门自身（读写唯一内核，**应收口在这里**）。
- `MapGenRules.cs 18 处`＝**生成期读**（步骤4~11 的规则判定，`map` 未挂进 `WorldManager._world` ⇒ 走 `GenesisRead` 更绕且无收益；按 `03` §6.1「造世界 0→1 不算增」属造世界口范围）。**⚠️ 未完成项：`MapGenRules` 生成期读尚未统一走 `GenesisRead`（18 处），本片按「生成期自身，报告单列」处理**（提示词原文：「其余为 MapGenRules 生成期 8 处 ＋ Editor/Smoke 探针 5 处（不在收口范围）」）。
- **非代码命中排除声明**：改后 `git grep -c "\.features\["` 若不经人工筛查，会命中 3 行**注释**（上表已注）＋新增探针 `Slice4Probe` 5 行**注释** ⇒ 报告所有「真裸读」数字均已手工剔除注释行。

**Editor 探针面（本片不动，单列）**：排除本片新建探针后，`Assets/Editor/**` ⇒ **10 文件 / 55 处**：

```
16  Valley_HH268_MapProbe.cs      10  Valley_HH272_MapGenProbe.cs
 6  Valley_HH291_MapGenProbe.cs    6  Valley_HH294_P1Probe.cs
 4  Valley_HH294_Slice2Probe.cs    3  Valley_HH264_FeatureProbe.cs
 3  Valley_HH294_HH293Probe.cs     3  Valley_HH140_Probe.cs
 2  Valley2_13_Smoke_AB.cs         2  Valley_HH294_Slice3Probe.cs
```
（另：本片新建 `Valley_HH294_Slice4Probe.cs` 自身 5 处命中**全为注释**，不计入。）

**上层改成走门的调用对照（节选）**：

| 文件 : 行 | 改前 | 改后 |
|---|---|---|
| `MapRenderService.cs:181` | `SetCell(x, y, map.features[y * map.width + x]);` | `SetCell(x, y, MapGate.ReadAt(map, x, y));` |
| `MapRenderService.cs:193` | `SetCell(cell.x, cell.y, map.features[cell.y * map.width + cell.x]);` | `SetCell(cell.x, cell.y, MapGate.ReadAt(map, cell.x, cell.y));` |
| `MapRenderService.cs:210` | `SetCell(nx, ny, map.features[ny * map.width + nx]);` | `SetCell(nx, ny, MapGate.ReadAt(map, nx, ny));` |
| `MapRenderService.cs:288` | `if (map.features[(y+dy)*map.width + (x+dx)] != ft)` | `if (MapGate.ReadAt(map, x + dx, y + dy) != ft)` |
| `MapRenderService.cs:363` | `SetCell(x, y, _map.features[y * _map.width + x]);` | `SetCell(x, y, MapGate.ReadAt(_map, x, y));` |
| `MapVisualizer.cs:57` | `GetFeatureColor(map.features[i])` | `GetFeatureColor(MapGate.ReadAt(map, x, y))` |
| `WorldGatherRegistry.cs:162-163` | `int i = ...; if (map.features[i] == Tree)` | `if (MapGate.ReadAt(map, cell.x, cell.y) == Tree)` |
| `WorldGatherSource.cs:131-132` | `int i = ...; return map.features[i] == Tree;` | `return MapGate.ReadAt(map, Cell.x, Cell.y) == Tree;` |
| `GuardDeploymentSystem.cs:341` | `FeatureType f = map.features[i];` | `FeatureType f = MapGate.ReadAt(map, i % width, i / width);` |
| `GridSystem.cs:86` | `WalkFlags wf = FeatureToWalkFlags(map.features[y*_w + x]);` | `WalkFlags wf = FeatureToWalkFlags(MapGate.ReadAt(map, x, y));` |
| `MapValidator.cs:67` | `!MapGenRules.IsWalkableFeature(m.features[i])` | `!MapGenRules.IsWalkableFeature(MapGate.GenesisRead(m, x, y))` |
| `ResourceRespawnSystem.cs:113` | 自建界判定 + `map.features[i] != Tree` | `MapGate.GetFeatureAt(cell) != Tree` |
| `WorldManager.cs:244` | `var f = map.features[coord.y*map.width + coord.x];` | `var f = MapGate.GetFeatureAt(coord);` |

**探针读数（内容级等价）**：全图 384×384＝147456 格，`MapGate.ReadAt` 与原始裸读**不一致数 = 0**（改后读值与改前同源）。

### 判据 3 · 4-B ⭐「在某资源格上建造 ⇒ 该格 features 不被改写」

**能力句**：**在某资源格（Tree）上建造 ⇒ 该格 `features` 不被改写。**

| 反证 | 读数 |
|---|---|
| **A（门层声明校验 · 构造用例）**：对 Tree 格以「声明=Mine」调 `ConsumeAnchor` | 返回 **False**，features `Tree → Tree`（**声明不符即拒绝**） |
| **A2（声明相符才允许）**：对 Tree 格以「声明=Tree」消费 | 返回 **True**，features `Tree → Plain` |
| ⭐ **A3（真实建造路径）**：以 `quarry`（声明 quarry→Mine）在 Tree 格建造 | `TryBuild = False`，该格 features = **Tree**（未被改写） |
| **B（对照 · 锚点消费正例）**：对 Mine 格以「声明=Mine」消费 | 返回 **True**，features `Mine → Plain` |
| ⭐ **能力句读数**：Tree 格 (31,2) 经上述全部调用后 | features = **Tree** |
| **C（老路径已删）**：`WorldManager.TryConsumeResourceNode` | 编译期符号 = **null** |

**治本修法**：`MapGate.ConsumeAnchor` 增第 3 参 `declaredAnchor`（＝`ResourceNodeMapping.GetResourceNode(def.id)`），门内新增 `MatchesDeclaredAnchor(feature, declared)` 校验 ⇒ **即便放置路径判定被绕过，未被声明的资源格也永不改写**。
> ⚠️ **勘正**：首轮探针 FAIL=2 暴露初版 `ConsumeAnchor` **缺门内声明校验**（只依赖调用方自觉）—— 已治本，非找补。

### 判据 4 · 4-C 两条删除路径 ⇒ 单一入口

**复现命令**：

```bash
git grep -n "TryConsumeResourceNode" HEAD -- 'Valley Rampart/Assets/**/*.cs'   # 改为非注释命中 2 处
git grep -n "TryConsumeResourceNode" -- 'Valley Rampart/Assets/**/*.cs'        # 改后：仅注释 3 处（含引用说明）
git grep -n "RemoveResourceNode" -- 'Valley Rampart/Assets/**/*.cs'            # 唯一入口
```

- **改前**：`WorldManager.TryConsumeResourceNode`（定义 :240 ＋ `BuildController.cs:256` 调用）与 `ResourceRespawnSystem.SetFeature`（:172 直接写 `map.features[i] = target`）**各写一遍同样的「翻 Plain ＋ 刷派生 ＋ 刷渲染 ＋ 守卫失去」链** ⇒ 两条删除路径属实。
- **改后**：`WorldManager.TryConsumeResourceNode` **整段删除**（编译期符号 = null，探针已证）；两条路径合并为 **`MapGate.RemoveResourceNode`**（`03` §7.3 删门唯一入口）。
- **删门口径勘正**：`RemoveResourceNode` 存在性校验用新引入的 `IsRemovableResourceFeature`（`Tree/Mine` ＋ 一次性 `OreVein/WoodPile/StonePile`），**不是** `IsResourceNodeFeature`（`Tree/Mine`）—— 后者是**锚点消费口径**。改前 `ResourceRespawnSystem.SetFeature` 是「`target == Plain` 即可写」⇒ 凡资源类地表皆可删；两者口径**逐字对齐**，未引入回归（一次性实体采集链 `Building.OnGatherCompleted → HandleEntityDepleted → SetFeature(Plain)` 仍通）。

**探针读数**：`MapGate.RemoveResourceNode` 存在且签名为 `static(GridCoord) → bool`；单入口行为：删后翻 Plain ＋ 二次删幂等（探针 §4-E 段 ② 以「消费后不可再消费」间接取证，见判据 6）。

### 判据 5 · 4-D 注销逻辑只出现 1 处 ＋ `BuildingDestroyedEvent` 零订阅

**复现命令**：

```bash
git grep -n "ReleaseLayerOwnedState" -- 'Valley Rampart/Assets/**/*.cs'
Get-ChildItem -Recurse -Path "Valley Rampart/Assets" -Filter *.cs |
  Select-String -Pattern "Subscribe<BuildingDestroyedEvent>|new BuildingDestroyedEvent" |
  Where-Object { $_.Line.Trim() -notmatch '^(//|///|/\*)' }
Get-ChildItem -Recurse -Path "Valley Rampart/Assets" -Filter *.cs |
  Select-String -Pattern "struct BuildingDestroyedEvent" | Where-Object { $_.Line.Trim() -notmatch '^(//|///)' }
```

| 检查项 | 读数 |
|---|---|
| `ReleaseLayerOwnedState` 命中 | **4 行 / 1 文件**（`Building.cs:847` 定义 ＋ `:887` `Die` 调 ＋ `:915` `OnGatherCompleted` 调 ＋ `:905` 注释）⇒ **实现体只 1 处** |
| 改前「抄两遍」实证 | `HEAD` 版 `Building.cs:803-813`（`Die`）与 `:838-847`（`OnGatherCompleted`）各写一遍 `FreeFootprint`/`SetBridge`/`Registry.Unregister`/`TaskScheduler.Unregister`/`SaveManager.UnregisterSaveable` |
| `Subscribe<BuildingDestroyedEvent>`（排注释） | **0** |
| `new BuildingDestroyedEvent`（排注释） | **0** |
| `struct BuildingDestroyedEvent` 定义（排注释） | **0**（已删） |

⭐ **`BuildingDestroyedEvent` 残留定义已清理**（`GameEvents.cs` 原 :493-499 整段删除，替换为注释块记录「已删 ＋ 先决证据：0 订阅 0 发布」）。清理前先决证据：`Subscribe<T>` = 0、`new` = 0。
> ⚠️ **勘正**：任务书称「广播钩子」为改造目标；实况为 `BuildingDestroyedEvent` **已退役**（`Building.Die` 改发 `UnitDiedEvent`）。本片**未新建事件**，只做「抄两遍合一 ＋ 注销走 `ReleaseLayerOwnedState` 单实现」。
> ⚠️ **遗留**：`LifecycleAudit.cs:131` 的白名单字符串仍含 `"BuildingDestroyedEvent"`（审计基线名单，非引用）；**未清理**（不在本片范围，属 Editor 审计工具面）。

### 判据 6 · 4-E ⭐「拆除消费锚点建起的建筑 ⇒ 锚点返还且可再被引用」

**能力句**：**拆除「消费锚点建起来的建筑」⇒ 锚点返还，且该锚点可再被引用（可再被消费）。**

| 步骤 | 读数 |
|---|---|
| ① 消费 | `ConsumeAnchor` = **True**，features `Mine → Plain`，消费者记下 锚点 = (42,2)(Mine) |
| ② 存在性反证（消费后不可再消费） | 再次 `ConsumeAnchor` = **False** |
| ③ 返还 | `ReturnAnchor` = **True**，features `Plain → Mine`，消费者锚点引用 = **null** |
| ⭐ ④ 存在性反证（**返还后可再被引用**） | 返还后再 `ConsumeAnchor` = **True**，features → **Plain** |
| ⑤ 幂等 | 无锚点引用的消费者 `ReturnAnchor` = **False** |

**落地**：`MapGate.IAnchorConsumer`（`ConsumedAnchorCoord`/`ConsumedAnchorFeature`/`SetConsumedAnchor`）＋ `Building` 实现该接口（`anchorCoordX/anchorCoordY/anchorFeature`，入档 `BuildingSaveData` 三字段）；`Building.ReleaseLayerOwnedState` 第 2 步调 `MapGate.ReturnAnchor(this)`（`03` §7.3 删门七步之第 2 步）⇒ **`Die` 与 `OnGatherCompleted` 两条销毁路径皆返还**。**通用、非仅矿山**（接口按 feature 泛化）。

### 判据 7 · 4-F 「一次拿全一格」接口存在 ＋ 区域枚举不只单位专用

| 项 | 读数 |
|---|---|
| `TryGetCell`（一次拿全一格） | 存在；实测 `True` ⇒ 一次拿到：地表 / 气候 / 可走 / 归属 / 有物 / 单位数 **6 项** |
| 区域枚举 · 条件【地表=Plain】 | **1477** 格 |
| 区域枚举 · 条件【地表=Tree】 | **75** 格 |
| 区域枚举 · 条件【可走】 | **1779** 格 |
| 区域枚举 · 条件【空置】 | **1971** 格 |
| `CountFeatures`（计数旋钮 Tree） | **75** ⇒ 与 `QueryCells(地表=Tree)` **75 一致** |
| 数量旋钮 `maxCount=5` | 返回 **5** 条 |
| 半径范围 `QueryCellsRadius(r=8)` | **251** 条 |
| 按实体枚举 `QueryOccupants` | **177** 个不同占格实体 |
| 稳态 200 次查询（buffer 复用）净分配 | **0 B**（`03` §8.7 判据 4 零分配） |

**调用点对照**：`FillUnitsInRect`（改前唯一区域枚举口、**单位专用**、`git grep` 全库 **0 调用点**）⇒ 新增 `MapGate.QueryCells`（通用：地表/可走/空置/有物皆可筛）／`QueryCellsRadius`（半径形状）／`QueryOccupants`（按实体）／`CountFeatures`（计数）。**非单位专用**由上述 4 种不同条件皆可用证明。

### 判据 8 · 4-G ⭐「存在替换 ⇒ 对外只发一条 `Replaced`」

| 项 | 读数 |
|---|---|
| `ReplaceFeature`(Ocean→Tree) | **True** ｜ 广播条数 = **1** ｜ 末条 =(0,0) `Ocean→Tree` ｜ 改后 features = **Tree** |
| ⭐ 存在性反证（同值替换 ⇒ 幂等零广播） | **False**，新增广播 = **0** |

**落地**：`GameEvents.cs` 新增 `MapReplacedEvent`（struct：`Coord`/`OldFeature`/`NewFeature`）；`ReplaceFeature` 内 `HasSubscribers` 守卫后**只 Publish 一条**（**不是** `Removed` ＋ `Added`）。

### 判据 9 · 4-H UI 侧不再现算可否拆

**改后原文行**（[Building.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L647)）：

```csharp
public bool CanDemolish => isPlayerBuilt && def != null && def.isDestructible && !def.isResourceNode;
```

`BuildingPanel.cs`：

```csharp
// :169（原 `bool canDemolish = _target.isPlayerBuilt && def.isDestructible && !def.isResourceNode;`）
bool canDemolish = _target.CanDemolish;
// :383
if (!_target.CanDemolish) return;   // 【HH.294 片4·4-H】判据归数据栏（同源，禁 UI 复算）
```

**复现命令**：

```bash
Get-ChildItem "Valley Rampart/Assets/_Game/Systems/Building/BuildingPanel.cs" |
  Select-String -Pattern "isPlayerBuilt|isDestructible|isResourceNode"
```

⇒ 命中 3 行，**`isDestructible`/`isResourceNode` 在 UI 内 0 命中**（余 2 处 `isPlayerBuilt` 用于**升级槽判定** :131 与**升级条件** :355，与「可否拆」无关）。探针读数：`Building.CanDemolish` 属性存在且类型 `bool`。

### 判据 10 · ⭐ 搭车 R2：24 座零登记已定位并闭合

**能力句**：**生成的每一座建筑 ⇒ `GetOccupant`(footprint 内任一格) 非 null。**

| 项 | 读数 |
|---|---|
| 全量建筑 | **11892** 座 |
| **零登记（footprint 内非空格 = 0）** | **0** ✅ |
| 全覆盖（每格 `ReferenceEquals(occ, b)`） | **11883** |
| 部分 | **9** |
| 覆盖格命中合计 | **11986 / 11986** |
| ⭐ 能力句失配座数 | **0** |

**断链根因（先诊断后修，实读取证）**：
1. `WorldManager.GenerateWorld` 调 `GenerateMap(...)` ⇒ **步骤5** `KingdomFoundry.FoundFirstGeneration` 创建 AI 王国预置建筑（含 castle/House/farm/Well/mine）；
2. 其 `BuildingFactory.CreateBuildingInstance` 调 `GridSystem.MarkOccupiedFootprint` ⇒ 此刻 **`PopulateFromMap` 尚未执行**，`_w/_h = 0` ⇒ `InBounds` 恒 false ⇒ **一个占格都没写**；
3. 回到 `GenerateWorld` 才调 `GridSystem.Instance.PopulateFromMap(playerMap)` ⇒ `Initialize()` **重新 `new IGridOccupant[n]`** 分配 `_occupants`（即便前步写进旧数组也被抹）。

⇒ 与片3 观察项「抽样 60 座中 24 座 footprint 内非空子格 = 0，且地块级 `GetOccupant(coord)` 亦为 null」**完全吻合**。

**修法**：新增 `GridSystem.RebuildOccupancyFromRegistry()`（按 `BuildingRegistry` 重登记每座 footprint，幂等含桥面位），并在 `WorldManager.GenerateWorld` 的 `PopulateFromMap` **之后**调用（同一 `GridSystem != null` 块内）。

### 判据 11 · ⭐ 搭车 R3：地块级读写口迁移清单 ＋ 部分占格存在性反证

**迁移清单（`D770` 请裁-4「强制迁移」的落地）**：

| 口 | 改前（代表位＝首子格） | 改后 | 形态 |
|---|---|---|---|
| `GridSystem.GetOccupant(GridCoord)` | `_occupants[CellBaseIndex(c)]`（只读首子格） | 快路径读代表位；**未命中则扫同块余 div²−1 子格** | **已迁移到子格域** |
| `GridSystem.IsObstacle(GridCoord)` | 同（只读首子格） | 转调 `GetOccupant(c)` | **已迁移到子格域** |
| `GridSystem.GetWalkFlags(GridCoord)` | `_walkFlags[CellBaseIndex(c)]` | 未迁移 ⇒ **显式声明近似前提**（代码注释已在 `GridSystem.cs` 类头 片3-B 段） | 近似声明 |
| `GridSystem.IsWalkable(GridCoord)` | 转调 `GetWalkFlags` | 同上 | 近似声明 |
| **新增** `MarkOccupiedSub(GridCoord, IGridOccupant)` / `FreeSub(GridCoord)` | 无 | 子格级写口（与既有读口 `GetOccupantSub` 对称） | 新增 |
| 写入类口（`MarkOccupied`/`Free`/`SetBridge`/`MarkOccupiedFootprint`/`FreeFootprint`） | 已按**地块块 div² 子格同写**（片3） | 不变 | 已对齐 |

⚠️ **未迁移项的近似前提声明**：`GetWalkFlags`/`IsWalkable` 仍读首子格。前提＝「同地块 div² 子格恒同值」——成立条件＝**全库写入方皆地块对齐**（地表物按地块派生 ✓ ／ footprint 按地块 ✓ ／ 桥按地块 ✓）。该状态（子格级部分占格）会在**有人调用新增的 `MarkOccupiedSub`／`FreeSub` 时**出现，届时 `GetWalkFlags` 会读到代表位而非全块真实态。

**存在性反证（构造「子格级部分占格」用例）**：

| 步骤 | 读数 |
|---|---|
| 前置 | 地块 (190,188) 当前 `GetOccupant` = **null**，可走 = **True** |
| 写入 | **只写末子格** (763,755)（非代表位） |
| ⭐ 断言 1（迁移后不失真） | 地块级 `GetOccupant`(190,188) = **该占格物** ✅ |
| ⭐ 断言 2 | `IsObstacle`(190,188) = **True** ✅ |
| 断言 3（子格级精确） | `GetOccupantSub`(首子格 760,752) = **null** ✅ ／ `GetOccupantSub`(末子格 763,755) = **该占格物** ✅ |
| ⭐ 存在性反证（改前式读法会失真） | 改前实现式（只读首子格）= **null** ⇒ **会误答「这格没东西」** |
| 清理 | 释放末子格后 `GetOccupant` = **null** ✅ |

⇒ **能力句成立**：构造「子格级部分占格」用例后，迁移后的地块级读口**不失真**；改前式读法在此用例下确实失真（反证有效）。

### 判据 12 · 搭车 R1：探针 4 处改后原文行

[Valley_HH294_Slice3Probe.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH294_Slice3Probe.cs) 改后原文：

```csharp
// :246（原 `int sSubX = src.x * div, sSubY = src.y * div;`）
// 【HH.294 片4·搭车 R1】原此处就地展开 `src.x * div` —— 改走 `GridSystem.CellToSub`（片3-A 规则表：⛔ 禁就地展开）
var srcSub = grid.CellToSub(new GridCoord(src.x, src.y), 0, 0);
int sSubX = srcSub.x, sSubY = srcSub.y;

// :275（原 `int sx = spawns[i].x * div, sy = spawns[i].y * div;`）
// 【HH.294 片4·搭车 R1】就地展开 ⇒ 改走 `GridSystem.CellToSub`
var sSub = grid.CellToSub(new GridCoord(spawns[i].x, spawns[i].y), 0, 0);
int sx = sSub.x, sy = sSub.y;

// :286-287（原 `new GridCoord(spawns[0].x * div, spawns[0].y * div)` 两行）
// 【HH.294 片4·搭车 R1】就地展开 ⇒ 改走 `GridSystem.CellToSub`
var from = grid.CellToSub(new GridCoord(spawns[0].x, spawns[0].y), 0, 0);
var to = grid.CellToSub(new GridCoord(spawns[spawns.Count - 1].x, spawns[spawns.Count - 1].y), 0, 0);
```

**等价读数**：抽样 2000 组（地块 ＋ 格内偏移），`CellToSub` 与就地展开 `cx*div+sx` **不一致数 = 0**。
**复现命令**：`Get-ChildItem "Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice3Probe.cs" | Select-String -Pattern "\* div"` ⇒ 改后 0 命中（原 4 处）。

### 判据 13 · 常规

| 项 | 读数 | 复现命令 |
|---|---|---|
| **编译** | **0 error CS**；warning **25** 条，**逐条核对全为本片前既存**（清单见 §五） | `refresh_unity` ＋ `read_console types=["error"]` |
| **确定性** | 384²·seed=21107 两次生成：`features` 逐格 ＋ `climateZones` 逐格 ＋ `kingdomSpawns` ＋ `naturalBuildings` **全一致** | 探针 §Determinism |
| **无每帧全图扫** | 空闲 90 帧内 `map.features` 变更帧数 = **0** ／ `_walkFlags` 变更帧数 = **0** | 探针 §FrameWatch |
| **`AI.Core` 零触** | `git grep -c "MapGate" -- 'Valley Rampart/Assets/_Game/Systems/AI.Core/**/*.cs'` = **0**；`git status` 无 `AI.Core` 改动 | 同左 |
| **改动文件行尾** | 本片改动/新建 18 个文件：**16 个 LF**；`GameEvents.cs`（**改，CRLF 663**，走 python 二进制，改后仍 CRLF）；`BuildingFactory.cs`（**未改**，CRLF 453，仅列读） | `python Temp/eol_check.py` |
| **正门读数** | 探针走 `TestHarnessApi.EnterTestRun`，三态退 Play（`ExitTestRun` ＋ `QuitSmoke`） | 探针首尾行 |
| **改动文件数** | `git diff --name-only HEAD` 中本片相关 **16 个**（另新建 2 个 .cs ＋ 各自 .meta） | `git diff --name-only HEAD` |

---

## 二、本片改动文件清单

**新建（2）**：
- `Valley Rampart/Assets/_Game/Systems/World/MapGate.cs`（＋`.meta`）
- `Valley Rampart/Assets/Editor/Smoke/Valley_HH294_Slice4Probe.cs`（＋`.meta`）

**修改（16）**：
| 文件 | 改动内容 |
|---|---|
| `_Game/Systems/World/MapGate.cs` | 新建：四门 ＋ 五件 ＋ 锚点接口（0→1 造世界口／唯一写内核／查门复合结构＋区域枚举／改门＋`ReplaceFeature`／增删门／锚点消费＋返还） |
| `_Game/Core/GameEvents.cs` | 删 `BuildingDestroyedEvent` 残留定义；新增 `MapReplacedEvent` |
| `_Game/Systems/World/MapValidator.cs` | 读走 `GenesisRead`；写走 `GenesisWrite`（校验器改只读/造世界） |
| `_Game/Systems/World/ResourceRespawnSystem.cs` | `SetFeature` 收口到增/删门；`ConfirmTreeGather` 走门读 |
| `_Game/Systems/World/WorldManager.cs` | 删 `TryConsumeResourceNode`；`IsResourceNodeAvailable` 走门；新增过渡 `GetFeatureAt`；新增 R2 占格重建调用 |
| `_Game/Systems/World/MapGenRules.cs` | 9 处写点改 `GenesisWrite`（造世界口） |
| `_Game/Systems/World/WorldGatherRegistry.cs` | 裸读走门 |
| `_Game/Systems/World/WorldGatherSource.cs` | 裸读走门 |
| `_Game/Systems/Grid/GridSystem.cs` | 派生改走门；`GetOccupant`/`IsObstacle` 迁移到子格域；新增 `MarkOccupiedSub`/`FreeSub`/`GetUnitCountInCell`/`RebuildOccupancyFromRegistry` |
| `_Game/Systems/Grid/MapVisualizer.cs` | 裸读走门 |
| `_Game/Systems/Rendering/MapRenderService.cs` | 5 处裸读走门 |
| `_Game/Systems/AI/Formation/GuardDeploymentSystem.cs` | 索引回读走门 |
| `_Game/Systems/Building/BuildController.cs` | **4-B 禁顺手抹树**（删 `TryConsumeResourceNode` 调用）＋ 锚点消费走门（带声明） |
| `_Game/Systems/Building/Building.cs` | `IAnchorConsumer` 实现；`ReleaseLayerOwnedState`（4-D 合一，含 4-E 返还）；`CanDemolish`（4-H）；存档读写锚点 |
| `_Game/Systems/Building/BuildingSaveData.cs` | 锚点 3 字段 |
| `_Game/Systems/Building/BuildingPanel.cs` | 可否拆读数据栏（4-H） |
| `Assets/Editor/Smoke/Valley_HH294_Slice3Probe.cs` | 搭车 R1（4 处就地展开改走 `CellToSub`） |

---

## 三、未完成项（显式列出）

| # | 项 | 状态 | 说明 |
|---|---|---|---|
| 1 | `MapGenRules` 生成期读（18 处）统一走 `GenesisRead` | **未做** | 按提示词「生成期自身，不在收口范围」处理，报告单列 |
| 2 | **Editor 探针面裸读 56 处 / 10 文件** | **未做** | 提示词「本片不动，但报告须单列」——已单列（判据 2） |
| 3 | Editor 探针面**写点 5 处 / 2 文件** | **未做** | 同上 |
| 4 | `GridSystem.GetWalkFlags`/`IsWalkable` 地块级口 | **未迁移** | 只做「显式声明近似前提」（代码注释）；`GetOccupant`/`IsObstacle` 已迁移 |
| 5 | `LifecycleAudit.cs:131` 白名单含 `"BuildingDestroyedEvent"` 字符串 | **未清理** | Editor 审计工具面，非本片范围 |
| 6 | 探针判据 5（4-D）「注销逻辑只 1 处」 | **静态取证，非行为取证** | 以 grep ＋ 编译期符号取证；未构造「真实拆一座建筑 ⇒ 观测注销 4 项各执行 1 次」的行为用例（`Building` 是 MonoBehaviour，构造真实用例需进局操作，超本片探针范围） |
| 7 | 4-F `FillUnitsInRect` 调用点改造 | **不适用** | `git grep` 全库 **0 调用点**（死接口，片2 已登记族）⇒ 只新增通用 `Query*`，未改既有调用方 |

---

## 四、勘正记录

| # | 任务书/提示词描述 | 实况 | 处置 |
|---|---|---|---|
| 1 | 任务书「`features` 裸写 63 处 / 10 文件」（`D764`） | 改前实测 **17 处 / 6 文件**（生产码 12/4） | 按提示词要求**自己重新全库清点**，以实测为准 |
| 2 | 任务书「`BuildController.cs:257`」 | 实为 **:256**（偏 1 行） | 以实读为准 |
| 3 | 任务书 4-D「广播钩子」 | `BuildingDestroyedEvent` **已退役**（`Die` 改发 `UnitDiedEvent`）；零订阅零发布 | **不新建事件**，只做「合一 ＋ 注销走钩子」；顺手清理残留定义 |
| 4 | 提示词「写点 16 处 / 6 文件·运行期 3 处」 | 生产码写点 **12 处 / 4 文件**（运行期 3 处＝`MapValidator`/`ResourceRespawnSystem`/`WorldManager` ✅ 吻合）；余 9 处在 `MapGenRules`（提示词记 8 处，**漏计 `:799` 嵌套 `cells[k]` 写法**——窄正则漏匹配） | 宽正则复核后修正为 9 |
| 5 | —— | `ResourceRespawnSystem.SetFeature` 改前是「`target == Plain` 即可写」⇒ 一次性资源（`OreVein/WoodPile/StonePile`）采集链也走它 | 删门口径用新 `IsRemovableResourceFeature`（5 型），**与改前逐字等价**，避免回归 |

---

## 五、编译 warning 清单（25 条，逐条核对＝本片前既存）

```
IUIPanel.cs(11,10) CS0108 / IUIPanel.cs(14,10) CS0108
GroundEffectManager.cs(95,18) CS0114 / ToastManager.cs(47,18) CS0114
ChestManager.cs(29,18) CS0114 / ResourceRespawnSystem.cs(23,18) CS0114
BuildingMenuPanel.cs(169,24) CS0252 / BuildingMenuPanel.cs(186,13) CS0252
ProjectileManager.cs(253,41) CS0253 / NPCBrain.cs(880,22) CS8632
FormationPanel.cs(128,13) CS0252 / FormationPanel.cs(139,43) CS0252
CameraSetup.cs(32,19) CS0414 / VisionSystem.cs(15,25) CS0414
CameraSetup.cs(30,18) CS0414 / PathfindingScheduler.cs(42,17) CS0414
Valley2_17_Smoke_5.cs(91,85) CS0162 / ArtImportPipeline.cs(267,9) CS0618
Valley2_17_Smoke_P0.cs(225,13) CS0219 / Valley2_21A_Smoke.cs(202,13) CS0162
Valley_HH128_Probe.cs(75,14) CS0219 / Valley_HH264_AnimProbe.cs(182,17) CS0219
Valley2_20_Smoke_Race.cs(701,25) CS0472 / Valley2_20_Smoke_Race.cs(875,13) CS0162
Valley_HH80_Run.cs(301,13) CS0162
```

**核对口径**：25 条**无一**指向本片改动的 15 个 .cs 文件（`ResourceRespawnSystem.cs(23,18)` 是 `Awake()` 隐藏基类成员的既存 warning，非本片引入 —— 本片未改 `Awake`）。⇒ **0 新增 warning**。

---

## 六、请示裁决（1 项）

### 请裁-1 · `MapGenRules` 生成期读（18 处）是否收口到 `GenesisRead`？

**背景**：本片把 `MapGenRules` 的 **9 处写点**全部改走 `GenesisWrite`（造世界口）；但其 **18 处生成期读**仍为裸读 `map.features[...]`。

**两难**：
- **不做**：能力句须加限定「除门自身**与 `MapGenRules` 生成期读**外」（本报告已按此限定）；判据 21「8 个文件裸读 `map.features[]`」的「收口」在生成期未彻底。
- **做**：`GenesisRead(map, x, y)` 每次多两次界判定 ＋ 一次 `y*width+x` 重算（生成期在 `W×H` 循环内高频调用）⇒ **需实测对生成耗时的回归**，本片未测。

**建议**：**留作片 5/6 一并**（造世界口是独立语义，宜与「资源池」批次同批设计），或在**下一片**补一个「生成期读走门 ＋ 生成耗时回归」小项。
**⚠️ 口径不明，停在此处报裁，未按理解实现。**

---

## 七、回退预案

按 `底层执行计划` §五：`MapGate` 为**新增层**，旧路径均已删除（非同轨双写）。**回退方式＝`git revert` 本片 commit**（14 个改动文件 ＋ 2 个新建文件，单一 commit，无跨批耦合）。已确认无「新旧双轨并存」的中间态遗留（`TryConsumeResourceNode` 已删、`BuildingDestroyedEvent` 已删、`features[]` 写点唯一）。

---

## 八、证据索引

- **探针日志**：`Valley Rampart/Logs/hh294_slice4_probe.log`（PASS=39 / FAIL=0）
- **grep 复现命令**：见 §一 各判据内嵌命令块
- **行尾核查脚本**：`Temp/eol_check.py`
- **本报告行尾**：LF
