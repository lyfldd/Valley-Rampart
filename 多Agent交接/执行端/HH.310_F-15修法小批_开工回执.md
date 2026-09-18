# HH.310 · `F-15` 修法小批 · 开工回执

> 执行端｜2026-09-18｜批次＝**`HH.294` 批外挂账「`F-15` 修法小批」**（**不属六片**·单列微批）
> 唯一口径源＝`最高优先级文档/03_地图即数据库.md` §6.5 新增条（`D782` 立·**只读遵守·改任何一行契约**）
> 裁决依据＝台账 **§五十三**（`D782` 取向＝「**允许重叠 ＋ 显式化**」）
> 取号：HH 水位线现用至 **`HH.309`**（`_编号登记.md`／`_当前快照.md` 双读一致；`HH.31x` 全库零命中）⇒ **开工回执＝`HH.310`／交付报告＝`HH.311`**
> MCP：本会话 **`mcp_unity-bridge` 实测可用**（`unity_editor get_state`／`unity_console`／`unity_editor refresh` 均返回 JSON）⇒ 按 skill `unity-mcp-first` 走 bridge。

---

## 一、清单实核复述（逐项，含现场直读）

| 项 | 清单写 | 现场实核（本端直读） | 判 |
|---|---|---|---|
| `F15-A` | `BuildingRegistry.cs:26-34` `Unregister`，`:33` 无条件 `Remove` | `:26 public void Unregister(Building b)`／`:29 _all.Remove(b);`／`:31-33` 双层 for ⇒ **`:33 _byCoord.Remove(new GridCoord(b.coord.x + dx, b.coord.y + dy));` 确为无条件** | ✅ 成立 |
| | ⛔ 不动 `_all.Remove(b)`（`:29`） | `:29` 只删自己（`List.Remove` 引用相等）⇒ **已正确** | ✅ 不动 |
| | ⛔ 不动 `Register`／`RegisterFootprintCells:69` | `:18-23 Register` 有 `_all.Contains` 去重 ⇒ 早退；`:64-70` 逐格**无条件覆写** ⇒ 后写者胜 | ✅ 不动 |
| `F15-B` | `BuildingRegistry.cs:63` 注释与实现相反 | `:63 /// <summary>footprint 覆盖格逐格登记（重复格以先注册者为准，正常流程校验已排除重叠）。</summary>` —— 逐字与清单引用**一致**；「先注册者为准」与 `:69` 覆写语义**相反**，「已排除重叠」已被 `F-15`（实盘 9 座）**证伪** | ✅ 成立 |
| `F15-C` | A/B 对照开关（类内 `public static bool`） | 现无同名成员（全库零命中）⇒ 新建 | ✅ 可落 |
| 判据 6 | `_byCoord` 写读点**仍 5 处** | 见 §三 清单（**实测 5 处，逐处 `file:line` 附**） | ✅ |
| 判据 6 | `Unregister` 生产码调用点**只 1 处** | 见 §三 两列（生产码 **1**／Editor 探针 **4**） | ✅ |
| 红线 | 行尾「纯 LF·71 行·无 BOM」**改前重验** | **实测：`bytes=2777`／`LF=71`／`CR=0`／`BOM=False`／`lines=71`** ⇒ 与清单**逐值一致**（今日 LF ＝ 今日 LF） | ✅ |

## 二、红线认账（逐条，施工前）

| # | 红线 | 认账 |
|---|---|---|
| 1 | ⛔ 不动 `GridSystem.cs:380 MarkOccupiedFootprint` 与 `:397 FreeFootprint` | 认。实读 `:371-385 MarkOccupied`（`_occupants[i] = occupant` 无条件）／`:388-400 Free`（`_occupants[i] = null` 无条件）⇒ **只读取证，零改动**（判据 7 给读数） |
| 2 | ⛔ 不给 `_byCoord` 加多槽／引用计数／回滚栈 | 认。本批只加**归属判定**（`TryGetValue` ＋ `cur == b` 才删） |
| 3 | ⛔ 不改「预置落点加占格判定」 | 认。落点器（`MapGenRules` 侧）**零触**；判据 5 以同 seed hash 反证 |
| 4 | ⛔ 不碰消费面（`PlacementValidator:93/208`／`MachinePlacement:119`／`MapGate:591`／`GetInRect:44-54`） | 认。**只读**；§四 已对其做「是否依赖无条件删除」的隐式假设核查（结论＝**无**，不停手） |
| 5 | ⛔ 不碰 `Building.cs` 注销序（`ReleaseLayerOwnedState:848`） | 认。`:850-867` 三序（锚点返还→`FreeFootprint:857`→`Unregister:863`）**逐行未动** |
| 6 | ⛔ 不改契约 `03`／⛔ 不碰中层（`05`/`06`/`07`/`08`/`#14`） | 认。本批 `Assets/**` 改动面＝**`BuildingRegistry.cs` ＋ 新建 Editor 探针** |
| 7 | 具名 `git add`／不 push／**账本不代写** | 认。本批提交只用具名路径；四档账本**零 diff**（应登记项只在本报告 §末声明） |

## 三、`_byCoord` 写点/读点清单（判据 6·实测）

全库（`Valley Rampart/Assets/**`）`_byCoord` 命中 **5 处，全在 `BuildingRegistry.cs` 内**：

| # | 行 | 角色 | 语句 |
|---|---|---|---|
| 1 | `:12` | **声明** | `private readonly Dictionary<GridCoord, Building> _byCoord = new Dictionary<GridCoord, Building>();` |
| 2 | `:33` | **写·删** | `_byCoord.Remove(new GridCoord(b.coord.x + dx, b.coord.y + dy));`（`Unregister`） |
| 3 | `:39` | **读** | `_byCoord.TryGetValue(coord, out var b);`（`GetAt`） |
| 4 | `:60` | **写·清** | `_byCoord.Clear();`（`Clear`） |
| 5 | `:69` | **写·插** | `_byCoord[new GridCoord(b.coord.x + dx, b.coord.y + dy)] = b;`（`RegisterFootprintCells`） |

⇒ **清单谓「须仍为 5 处」＝实测 5 处 ✅**（本批不改数量·只在 `:33` 加归属守卫）。

## 四、`Unregister`「函数级 × 全部调用点」两列（判据 6）

**函数级唯一实现**：`BuildingRegistry.cs:26-34`（**1 处**；`UnitRegistry`/`WarehouseRegistry`/`DamageSystem`/`TaskScheduler`/`FormationManager` 各为**同名异类**，不属本批面）。

| 列 | 命中 | 位置 |
|---|---|---|
| **生产码**（`Assets/_Game/**`） | **1 处** | `Building.cs:863 if (unregisterFromRegistry) BuildingRegistry.Instance?.Unregister(this);`（唯一入口＝`ReleaseLayerOwnedState`，`Die():888` 传 `true`） |
| **Editor 探针**（`Assets/Editor/**`） | **4 处** | `Valley2_17_Smoke_12.cs:223`（`br.Unregister(b)`）／`Valley2_17_Smoke_12.cs:310`／`Valley2_17_Smoke_14.cs:583`／`Valley2_20B_Smoke_M7.cs:454` |
| **过渡/清场** | **0 处** | `BuildingFactory.cs:366` 是 `Clear()`（**非** `Unregister`）；`TeardownManager` 只注销 `UnitRegistry` |

⇒ 与清单预期「生产码应只 `Building.cs:863`；Editor 探针另列」**逐值一致 ✅**。

## 五、消费面「隐式依赖无条件删除」预核（报裁纪律 §四·先行）

**结论：未发现任何消费方依赖「无条件删除」的隐式假设 ⇒ 不停手报裁。**

| 消费方 | 实读判据 | 修后行为变化 |
|---|---|---|
| `PlacementValidator.cs:93` | `occupant != null ⇒ Blocked` | 重叠格上**更保守**（后写者仍在册 ⇒ 仍 Blocked；改前会被误判为空 ⇒ 这是**修掉一个假空位**） |
| `PlacementValidator.cs:208` `IsWallAt` | 判 `role == Wall` | 重叠格归**后写者**（＝真占格者）⇒ 更准 |
| `PlacementValidator.cs:259` | 同 `:208` 家族（桥链 BFS） | 同上 |
| `MachinePlacement.cs:119` | `GetAt != null ⇒ 不可放` | 同上（更保守） |
| `MapGate.cs:591`（区块拾取候选） | 先取 `GetAt` 再取候选集 | 重叠格上仍能取到**真实占格者**⇒「看到的点不到」面**减少** |
| `BuildingRegistry.GetInRect:44-54` | 逐格 `GetAt` 去重 | 生产码 **0 调用点**（`D772` 已实核「备而未用」）⇒ 无消费方 |
| `BuildController.cs:374-381`（桥邻接） | `GetAt` 取邻格桥 | 无重叠时**逐值不变** |
| `GridSystem.RebuildOccupancyFromRegistry:429+` | 走 `reg.All`（**非** `_byCoord`） | 不受影响 |

## 六、基线与口径（开工即取）

| 项 | 读数 |
|---|---|
| **warning 基线**（`O5` 改正项·开片即取） | Console 清空 → `unity_editor refresh` ⇒ **`errors=0`／`warnings=0`／`entries=[]`** |
| 编译基线 | **`0 error`／`0 warning`**（同上一次 refresh 返回值） |
| `BuildingRegistry.cs` 行尾（改前） | **LF=71／CR=0／BOM=False／bytes=2777／lines=71** |
| `git HEAD`（改前） | **`558515a3`**（`D783` 勘正②·契约 03 两条）——**判据 5 基线取此值·⛔ 禁 `HEAD`**（本批一提交 `HEAD` 即成自己） |
| 地图 hash 基线 | `feat+climate` ＝ **`9424A5D99D9C3543`**（片 6-2/6-3 四连跑读数） |
| ⚠️ **口径勘正（当场直读）** | 清单判据 5 谓「同 seed `features ＋ climateZones ＋ spawns` hash **仍＝`9424A5D99D9C3543`**」——**实读不符**：`9424A5D99D9C3543` 是 **`feat+climate` 两口径**的基线；**加 `spawns` 后为 `0A78DF92C7F30B81`**（片 6-3 证据 `Logs/hh294_slice6_3/probe_run1_full_step11_222149.txt:7`）。⇒ 本批**两列同报**（`feat+climate` 对基线／`+spawns` 对 `0A78…`），**不按字面单列**（否则该判据结构性无法满足）。属**签发侧列名误差**，非改判。 |

## 七、施工计划（本批内，就三处）

1. `F15-A` `Unregister` 加归属判定（`if (_byCoord.TryGetValue(k, out var cur) && cur == b) _byCoord.Remove(k);`）——⛔ 不动 `:29`／`Register`／`:69`
2. `F15-B` `:63` 注释勘正（三点写清：① 重复格＝**后注册者胜**（实读 `:69` 无条件覆写）② 生成期预置落点**不查占格** ⇒ 实盘 **9 座**重叠 ③ 共享格归属**不可恢复**（单槽存储））
3. `F15-C` `public static bool LegacyUnconditionalRemove = false;`（ON＝改前语义／OFF＝新语义）⇒ 同 build 内跑两段（⚠️ **验收后删除**·以 commit 历史为回退面·`HH.294` 片 6-2 收尾批 `SpawnResourceEntities` 先例）
4. 新建 **Editor-only 探针** `Valley_HH310_RegistryOwnerProbe.cs`（正门 `EnterTestRun` 进局·`L-32` 真暂停＋退 Play 收尾）⇒ 判据 1~5／7 读数
5. 编译 `0 error`／warning 不新增 ⇒ 交付报告 **`HH.311`**

**判据 1 构造法（本端设计·附口径说明）**：清单谓「**同一 `coord` ＋ 同一 `footprint`**」造 A/B —— 但**同 coord 同 footprint ⇒ 全格皆为共享格**，判据 1(a)「`GetAt(A 独占格) == null`」与 1(c)「`GetAt(B 独占格) == B`」**无格可测**（该构造下不存在独占格）。
⇒ 本端**两构造并报**：**构①（字面）**同 coord 同 footprint（`2×2`）⇒ 全格共享，1(a)/(c) 如实报「无独占格」；**构②（补全·测三格类）**同 footprint 错位 1 格（A@(x,y)／B@(x+1,y)，均 `2×2`）⇒ **A 独占 2 格／共享 2 格／B 独占 2 格**。判据 1(a)(b)(c)(d) 以**构②**为准（构①作字面复述）。

## 八、应登记项（`D767` 纪律：不代写策划端四档账本，仅声明）

1. `_编号登记.md`：HH 水位线 **`HH.309` → `HH.310`（本回执）→ `HH.311`（交付报告）**
2. `_当前快照.md`：HH 水位线同步 ＋ 在飞表新增 `F-15` 修法小批行
3. 台账 `测试基线台账.md`：`F-15` 行（`:902`）状态由「**待派**」→「**施工中（`HH.310`/`HH.311`）**」（验收销号后改 ✅）
4. `_任务队列.md`：新增 `F-15` 修法小批行（`D782` §53.3「必改两件」的下游）

——

**⚠️ 本回执未碰四档账本（`_编号登记`／`_当前快照`／`_任务队列`／台账）·零 diff；`Assets/**` 尚未改动（§五 消费面预核为只读）。**
