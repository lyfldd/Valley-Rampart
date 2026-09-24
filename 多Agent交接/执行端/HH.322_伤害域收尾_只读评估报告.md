# `HH.322`「伤害域收尾」**只读评估报告**（`D856` 任务书）

- **执行端**：TraeCode｜**日期**：2026-09-24｜**性质**：⭐ **只读评估**（⛔ 未写生产码、⛔ 未改资产）
- **依据**：`多Agent交接/策划端/HH.322_伤害域收尾_只读评估批_任务书.md`（`D856`）｜`07_伤害层.md` §四 ＋ §八 `#5`｜`D851` §3.5-⑤｜先例 `CombatRules.IsUnityNull`（**实读 `:159`**，任务书写 `:157` ⇒ 注释块起行 ⇒ 行号须以 **`:159`** 为准）
- **基线**：`1ea5e6a4`（`HH.322` 签发）｜⚠️ 行号取自**工作区实读**（`TaskScheduler.cs` 有并行会话未提交改动 ⇒ 行号以工作区为准，与 `HEAD` 可能有偏移）

## 〇、一句话 ＋ ⑤ 零代码改动声明

⭐ **件 A：22 处逐处过可达性 ⇒ 「可达（须修）」0 处／「不可达（登记不修）」20 处／「须补证」2 处**；⛔ **未做任何代码改动**；件 B 两处**行号已漂移**（机制仍在）；件 C ＝ **非双源（纯延后项）**。

**零代码改动证据**（`git status --porcelain -- "Valley Rampart/Assets"`）：
```
 M "Valley Rampart/Assets/Scenes/GameScene.unity"                          ← 并行会话既有（⛔ 未碰）
 M "Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs" ← 并行会话既有（⛔ 未碰）
?? .../Editor/Smoke/Valley_HH289_EcoProbe.cs(.meta)  ← 他批遗留（⛔ 未碰）
?? .../Editor/Smoke/Valley_HH290_GatherProbe.cs(.meta) ← 他批遗留（⛔ 未碰）
?? .../Editor/Smoke/Valley_HH319_U15Probe.cs.meta / Valley_HH319_WaterAccount.cs.meta ← 他批遗留（⛔ 未碰）
?? .../_Game/Art/Ground/*.prefab|*.asset(.meta)      ← 美术脏点（⛔ 未碰）
```
⇒ ⭐ **本批无一行生产码/资产改动**；本轮 scratch（MCP 调用器与测量片段）落 `Logs/_*`（**非 `Assets` 面** · 未入版本控制）。

---

## 一、件 A（主）：`DZ-4` 同族 22 处 ⇒ 三桶分级

### 1.1 方法（`L-86`/`L-88` 口径）
- **枚举式**：全库机械扫描（`Assets/_Game/**` ＋ `Assets/Editor/**`）「四接口类型标识符的 `== null`/`!= null`/`?.`」⇒ 30 命中（含 Editor 面）⇒ 逐处**回读声明**剔除同名异型（见 §1.3 假阳性）；四接口计数与任务书一致（`ITaskSource` 11／`ISaveable` 3／`IGridOccupant` 5／`IHomePointProvider` 3 ＝ **22**，其中 `IGridOccupant` 1 处为 Editor 探针面 ⇒ **生产面 4**）。
- **可达性判据（两问）**：① 编译期类型＝**接口** ✓（22 处全中）② **调用链是否可能在对象已销毁时触发** —— 逐处给出「注册/调用方 → 释放方」链 ＋ **后续调用是否触 native**。
- ⭐ **关键共性证据（决定多数结论）**：`ITaskSource` 的 6 个 `MonoBehaviour` 实现者 **`IsValid` 首项一律 `this != null`**（Unity 比较 ⇒ 销毁即 false）：`Building.cs:1489`／`MineByproductComponent.cs:166`／`BlacksmithBuilding.cs:77`／`SiegeWorkshopBuilding.cs:253`／`ChestEntity.cs:119`／`UnitController.cs:1017`；另 2 个实现者（`WorldGatherSource.cs:96` ／ `WorkerTask.cs:62 DebugTaskSource`）为**纯 C# 类 ⇒ 无假 null 语义** ⇒ ⭐ **「假 null 穿透守卫」在 `ITaskSource` 面被实现者约定抵消**。
- ⭐ **释放方证据**：`Building.ReleaseLayerOwnedState`（`Building.cs:1341-1361`）内 **`:1358 TaskScheduler.Instance.Unregister(this)`** ＋ `:1360 SaveManager.Instance.UnregisterSaveable(this)`（在 `Die()` 内、`Destroy` 前）⇒ 建筑族**销毁前主动注销**；调度器另有**每 tick 兜底清理** `:259`（`if (snapshot[i] == null || !snapshot[i].IsValid) _sources.Remove(...)`）。

### 1.2 逐处分级表（22 处生产面）

| # | 位置 | 编译期类型＝接口？ | 调用链（证据） | 假 null 若发生的后果 | 桶 |
|---|---|---|---|---|---|
| 1 | `TaskScheduler.cs:138 Register(ITaskSource)` | ✓ | 注册方＝`Building.OnSpawn`／`ChestEntity` 落箱／`RequestHaulNow:367`（均**新建/存活**期） | `_sources.Add` ＋ `source.OnRegister()`（实现者体为空/置标志） | **不可达（登记不修）** |
| 2 | `TaskScheduler.cs:144 Unregister(ITaskSource)` | ✓ | 唯一主要调用方＝`Building.cs:1358`（`Die()` 内·`Destroy` 前 ⇒ **尚未销毁**） | `OnUnregister()` ＋ `OnBuildingDied(source)` | **不可达** |
| 3 | `TaskScheduler.cs:158 HasWorkerAssigned(ITaskSource)` | ✓ | `Building`／组件在岗判定（存活期） | 仅 `ReferenceEquals` 循环 ⇒ **不触 native** | **不可达（且无害）** |
| 4 | `TaskScheduler.cs:172 CountAssignedWorkers` | ✓ | 同上（规模派工查询） | 仅 `ReferenceEquals` ⇒ 无害 | **不可达（且无害）** |
| 5 | `TaskScheduler.cs:186 CountAssignedWorkers(+filter)` | ✓ | `Building.DemolishDuration()`（拆除期·源在册） | 仅 `ReferenceEquals` ＋ `filter(task)` ⇒ 无害 | **不可达（且无害）** |
| 6 | `TaskScheduler.cs:226 OnBuildingDied(ITaskSource)` | ✓ | 由 `:144`（源存活）与 `:147` 内部调用 | 循环＋`Abandon(...)`（不读源 native） | **不可达** |
| 7 | `TaskScheduler.cs:366 RequestHaulNow(ITaskSource)` | ✓ | `Building.RequestForceHaulOnce` 链／箱源注册（存活期） | `_sources.Contains` ＋ 必要时 `Register`（见 #1） | **不可达** |
| 8 | ⭐ `TaskScheduler.cs:485 task.source == null \|\| !task.source.IsValid`（**每 tick 热路径**） | ✓ | `Update → Tick`（`tickInterval` 周期）⇒ 迭代 `_npctaskMap`；源销毁 ⇒ `Die → 门 → :1358 Unregister` 同帧清；**且实现者 `IsValid` 首项 `this != null`** ⇒ 假 null 时 `!IsValid`＝true ⇒ **走放弃分支** | **不触 native**（判定即短路） | **不可达（登记不修）** ⭐ 结论＝「守卫虽失真，但下游 `IsValid` 约定兜住」 |
| 9 | ⭐ `TaskScheduler.cs:729 task == null \|\| task.source == null` → `task.source as Component` → `GetComponent<ProducerComponent>()` | ✓ | 完成动作（同 `Tick` 内的完成分支）；⚠️ 与 #8 同 tick，**#8 先行剔除失效源** | ⚠️ **若可达 ⇒ `MissingReferenceException`（对已销毁对象 `GetComponent`）** | **须补证**（见 §1.4-①） |
| 10 | `TaskScheduler.cs:1280 CountAssignedForType` | ✓ | 广告去重（源为新广告者） | 仅 `ReferenceEquals` ⇒ 无害 | **不可达（且无害）** |
| 11 | ⭐ `KingdomTask.cs:53 SourcePos => source != null ? source.SourcePos : Vector2.zero` | ✓ | 9 个调用点（`TaskScheduler.cs:332/409/419/435/507/1191/1217/1238/1325`）；⚠️ 实现侧 `Building.SourcePos => transform.position`（`Building.cs:1483`·**native**） | ⚠️ **若可达 ⇒ `MissingReferenceException`** | **须补证**（见 §1.4-②） |
| 12 | `SaveManager.cs:147 RegisterSaveable(ISaveable)` | ✓ | 注册方＝各系统 `Awake`／实体创建（存活期） | `saveable.SaveId`（实现者为字段/常量）＋ 重名分支字符串化（Unity `ToString` 对销毁对象安全） | **不可达（登记不修）** |
| 13 | `SaveManager.cs:159 UnregisterSaveable(ISaveable)` | ✓ | 调用方＝`Building.cs:1360`（销毁前）／`UnitController`「先注销再回池」（`:702` 注释） | `SaveId` 读字段 ⇒ 无害 | **不可达** |
| 14 | `SaveManager.cs:224 ChangeSaveId(..., ISaveable)` | ✓ | 改 ID（实体存活期） | 同上 | **不可达** |
| 15 | `GridSystem.cs:393 MarkOccupiedSub(sub, IGridOccupant)` | ✓ | 写入方＝建筑落成/子格精写（新对象） | `occupant.IsGridObstacle` ⇒ `Building.IsGridObstacle => isObstacle`（`:88` **纯字段**）⇒ 无害 | **不可达（且无害）** |
| 16 | `GridSystem.cs:417 MarkOccupied(c, IGridOccupant)` | ✓ | 同上 ＋ `RebuildOccupancyFromRegistry`（在册活物） | 同上（纯字段） | **不可达（且无害）** |
| 17 | `GridSystem.cs:420 MarkOccupied` 尾 `if (occupant != null) MarkCellOccupied(c)` | ✓ | 同上 | 仅写 `_walkFlags` ⇒ 无害 | **不可达（且无害）** |
| 18 | ⭐ `CombatRules.cs:186 ignore == null \|\| !ReferenceEquals(g.GetOccupantSub(sub), ignore)` | ✓ | 调用方＝`HasLineOfSight(from,to,IGridOccupant ignore)`（`CombatRules.cs:130`）⇒ **生产调用点 2**：`BuildingComponents.cs:134`、`MonsterAI.cs:199`（传**活建筑**） | 仅 `ReferenceEquals` ⇒ 无害 | **不可达（且无害）** ＋ ⚠️ **附带勘正**：`07` §四 写「`HasLineOfSight` 全库**零调用点**（死码）」**已过时**（现存 2 生产调用点 ⇒ `D848` 件 3′ 已接线） |
| 19 | `NPCBrain.cs:258 HomePointWorld => _homePointProvider != null ? … : Vector2.zero` | ✓ | `_homePointProvider = SceneHomePointProvider.Instance`（`:321-324` 初始化）＝**Singleton** ⇒ 场景级生命周期 | `GetHomePoint(this)`（实现者可能触 native） | **不可达（登记不修）**（仅场景卸载窗） |
| 20 | `NPCBrain.cs:321 if (_homePointProvider == null)`（Init 内） | ✓ | `Init` 一次性（生成期） | 同上 | **不可达** |
| 21 | `NPCBrain.cs:873`（同 #19 式 · `BuildBaseContext` 决策 tick） | ✓ | 决策 tick（同 #19 同一 Singleton） | 同 #19 | **不可达（登记不修）** |
| 22 | `KingdomTask.cs:27/42` 字段＋`advertiser` 空合并链（`TaskScheduler.cs:1271 var adv = kv.Value.advertiser ?? kv.Value.source;`） | ✓ | 广告去重链（源为新广告者） | `??` 为空判定 ⇒ 仅引用比较 ⇒ 无害 | **不可达（且无害）** |

⇒ **桶计：可达 0 ／ 不可达 20 ／ 须补证 2**。

### 1.3 扫描假阳性（已剔除 · ⛔ 不计入 22）
`Building.cs:1578/1603`（`producer` ＝ `ProducerComponent`）｜`ProductionSystem.cs:36`（同）｜`PlacementValidator.cs:94`（`occupant` ＝ `Building`）｜`AttentionSystem.cs:115/129`（`source` ＝ **`object`** 形参）｜`NPCBrain.cs:350`（`evt.Source` ＝ **`IDamageable`**（`GameEvents.cs:229`）⇒ 属 `DZ-4` 已治面）｜`IPlayerActions` 生成代码（`GameInput.cs:746`）。

### 1.4 须补证 2 处（写明还缺什么读数）
1. ⭐ **`TaskScheduler.cs:729`（完成动作）**：缺 —— **一次运行期破坏性构造**：令某任务源**绕开 `Die()`** 直接 `Destroy`（或在 `Destroy` 后同帧强制推进 `Tick`），观察是否抛 `MissingReferenceException`；并记录「异常类型／是否每帧重复／是否中断该帧其余结算」。当前仅能证：`#8`（同 tick 先行）＋ 实现者约定使其**看起来不可达**。
2. ⭐ **`KingdomTask.cs:53 SourcePos`**：缺 —— 同上破坏性构造 ＋ **窗口量测**（源在「广告 → 派发」之间被销毁的概率面）。当前仅能证：9 个调用点在「活源」路径或受 `#8` 保护。

### 1.5 修法口径评估（⛔ 本批不施工）
- **收口到单一判据口**：与 `DZ-4` 同源 ⇒ 建议**复用 `CombatRules.IsUnityNull`**，但 ⚠️ 它**签名锁定 `IDamageable`**（`CombatRules.cs:159`）⇒ 22 处涉及 4 个接口，**不能直接复用**；两条路：
  - **甲案（推荐）**：上提**中性口** `static bool IsUnityNull(object o) => o is UnityEngine.Object uo && uo == null;`（放 `Core`/`GridMath` 类中性位置），`CombatRules.IsUnityNull(IDamageable)` 保留为**薄转发**（⛔ 不复制 22 份）；改动面＝**新增 1 文件 ＋ 改 1 行** ＋ 22 处接线（**8 文件**）。
  - **乙案**：⛔ 只对「须补证 2 处 ＋ 有 native 后续的 6 处」定点加守卫（`#1/#2/#6/#7/#8/#9/#11`）⇒ 改动面 **2 文件**（`TaskScheduler.cs`／`KingdomTask.cs`）· 但与 `DZ-4`「唯一口」纪律不符。
- ⭐ **本报告立场**：在 `#9`/`#11` 的破坏性读数取得前，**不建议**按甲案全量接线（`L-88`：载体不足的能力断言不入账）。

---

## 二、件 A 附加：**22 处之外的新同族面**（停手条件③ ⇒ 列报，⛔ 不扩范围）

同一机械扫描档（`Logs/hh321b_iface_null_scan.txt`）按接口分组计数（含 Editor 面）：

| 接口 | 命中数 | 处置建议 |
|---|---|---|
| `IDamageable` | 43 | ✅ **已由 `DZ-4` 处置**（唯一口 `CombatRules.cs:159`）⇒ 本批范围外 |
| `ITaskSource` / `IGridOccupant` / `ISaveable` / `IHomePointProvider` | 11 / 5 / 3 / 3 | 本批 22 处（§一） |
| ⭐ **`IUnitHandle`** | **8** | ⛔ **新同族**（列报）—— 例：`L2PostureDecider.cs:121/144`（注释自称「IsAlive 含伪 null 检测（接缝 2）」）、`NewStimuli.cs:53`、`StimulusTypes.cs:102` |
| ⭐ **`IAnchorConsumer`** | **2** | ⛔ **新同族**（列报）—— `MapGate.cs:418/441`（消费者＝建筑 ⇒ 假 null 可能） |
| `IAIDebugInfo` / `IUIPanel` / `IThreatFormula` / `IWorldQuery` / `IClickInteractable` / `IUIStackEntry` | 4 / 3 / 1 / 1 / 1 / 1 | ℹ️ 列报（调试/UI/注册表面 · 未评估） |

---

## 三、件 B：性能债成本实读（`O-22`／`O-23`）

### 3.1 ⭐ 实读确认：**两处行号均已漂移**（停手条件② ⇒ 列报）

| 原文（`O-22`/`O-23`） | 实读（本批 · 工作区） | 结论 |
|---|---|---|
| `ProjectileManager.cs:334-353 QueryNearbyUnits` | **`ProjectileManager.cs:363-385`**（`:334-353` 现为 `FindBuildingAtLanding`） | ⚠️ 漂移 |
| `ProjectileManager.cs:500-506`（全表枚举 ＋ `new List`） | **已不在 PM**：现址 **`GridSystem.cs:536-542 GetUnitsInSubCell`**（`new List` ＋ `foreach (_unitSubCells)` 全表扫） | ⚠️ 漂移 |
| 候选窗 ＝ `radiusCells × subDiv` | ⭐ **已改**：`ProjectileManager.cs:374 int subRange = GridMath.SubWindowForVisualRadius(radiusCells, subDiv)`（`HH.320` 件18 · 超集口 · `GridMath.cs:108-113`） | ⚠️ **机制已被后续批改动** |
| `PerceptionSystem.cs:33`：`UnitRegistry.GetAllUnits()`（装箱） | ✅ **未漂移**：`PerceptionSystem.cs:33-34 var allUnits = UnitRegistry.Instance.GetAllUnits(); foreach (var unit in allUnits)`（路径＝`Assets/_Game/Systems/AI/PerceptionSystem.cs`，⛔ 非 `AI/Perception/`） | ✅ 成立 |
| 同款「全库约 30 处」 | 机械计数：`GetAllUnits()` **提及 48 处**；其中 **`foreach (… in …GetAllUnits())` 形态 ＝ 30 处** | ✅ 与「约 30 处」吻合 |

### 3.2 量化（⭐ 带测法 ＋ **仪器校验**）

**测法**：MCP `execute_code`（在 Editor 进程内执行 · ⛔ 不落任何文件）｜载荷 `HashSet<UnitController>` **N=33**（＝`HH.321` 复验产物 `单位在册=33` 实值）｜片段留档 `Logs/_perf_snippet{1,2,3}.cs`（scratch · 非 `Assets` 面）｜输出 `Logs/_perf_out{1,2,3}.json`。

⭐ **仪器校验（先做 · 结果决定哪些读数可用）**：
- `GC.GetAllocatedBytesForCurrentThread()`：对 **1000×`new byte[1024]`**（真分配 ≈1.02 MB）**报 0 B** ⇒ ⛔ **本运行时失效 ⇒ 弃用**（⚠️ 并据此**作废**首轮 ①②③ 的「0 B」读数）。
- `GC.GetTotalMemory(false)`：同量报 **+1,282,048 B** ⇒ ✅ **可用**。

| 读数 | 数值 | 载体/口径 |
|---|---|---|
| `subCellDivisor` | **4** | 资产实读 `Assets/Resources/Grid/GridConfig.asset`（`cellSize {1.28, 0.64}`） |
| ⭐ `subRange` 现值 | **2** | **运行期实调** `GridMath.SubWindowForVisualRadius(0.25f, 4)` ⇒ `⌈0.25×4×1.25⌉ = 2` |
| ⭐ **每发查询次数** | **25 次** `GridSystem.GetUnitsInSubCell`（`(2·2+1)²`） | 算术（由实测 `subRange` 派生）＋ 码面（`:376-380` 双层循环） |
| 接口枚举 vs 结构枚举（N=33） | **0.195–0.210 µs/次**（`GetAllUnits` 路径）／ **0.076–0.094 µs/次**（`GetUnitsEnumerator` 路径）⇒ **≈2.2–2.9×** | ⭐ **带测法**（两轮独立跑批一致；`Logs/_perf_out2/3.json`） |
| ⚠️ O-23「每次 1 次堆分配」 | **未复现**：200,000 次接口装箱 ⇒ `GetTotalMemory(false)` 增量 **0 B** ／ Gen0 **0 次** | ⚠️ **须补证**（⛔ 不断言"无分配"；建议改「带真实 `_aliveUnits` 载荷 ＋ 生产链」再测） |
| 单次全表枚举（**代理量**） | **3.66–4.16 µs/次**（33 条 · 3 轮 · `Dictionary<object,int>` 替代载荷） | ⚠️ **代理量**（⛔ 非生产链读数） |
| ⭐ 派生：每发候选窗总耗时 | **91.5–103.9 µs/发**（＝25 × 代理量） | ⚠️ 与 `O-22` 原文 **≈171 µs/发** 同量级（差 ≈1.6×：载荷不同＋未含 `AddRange`/坐标换算） |
| ⚠️ **未取得** | 生产链单发 µs（需活世界） | ⛔ 本批未进局 ⇒ 明示"未测" |

### 3.3 修法建议 ＋ 范围 ＋ **改动面文件数**

| 方案 | 内容 | 改动面 | 风险 |
|---|---|---|---|
| **甲（推荐 · 最小）** | `ProjectileManager.QueryNearbyUnits` 改**单遍** `UnitRegistry.GetUnitsEnumerator()` ＋ 直接以**命中半径**（`GridMath.DistVisual`）筛候选 ⇒ 每发 `O(N)` **1 遍**（替代 25 遍） | **1 文件**（`ProjectileManager.cs`）＋ 收敛口 `UnitRegistry.cs:57` **已备 · 无需改** | 低（候选**超集→精确集**，语义更严；⚠️ 须回归「漏命中」判据） |
| **乙（同族统一）** | 连同 `BuildingComponents.cs:117`／`DamageSystem.cs:524`／`GroundEffectManager.cs:203` 四链同改 | **4 文件** | 中（四条链口径须同时对齐） |
| **丙（结构级）** | `GridSystem` 增「子格 → 单位」**反向索引**（写口 `MarkUnitSub`/`ExitCurrentCell` 同步） | **1–2 文件**（但改数据结构） | 高（写口同步遗漏 ⇒ 幽灵占格） |

⭐ **判"值不值得"的量化锚**：每发 ≈0.1 ms ⇒ 甲案理论省 **≈24/25**（同载荷下 4 µs vs 100 µs）；⚠️ 绝对值取决于**并发在飞发数**（每帧 10 发 ⇒ ≈1 ms/帧 级）⇒ 建议**先做甲案**（1 文件）并在**并发高发**场景复测。

---

## 四、件 C：`07` §八 `#5`（战争机器是否已双真源）

⭐ **一句结论：⛔ 未双真源（仍是同一 `AttackProfile` 链）⇒ 属纯延后项（登记即可）。**
依据：全库 `new AttackProfile(...)` **仅 4 处** —— `UnitController.cs:858`（`BuildStaticProfile` ← 职业快照）／`BuildingComponents.cs:90`／`MonsterController.cs:69`／`NPCBrain.cs:1300`；投石机/弩炮族（`Resources/UnitData/Ballista.asset`、`Elf_VineCatapult.asset`）为**单位资产**，其开火经 `UnitController.BuildStaticProfile` → `DamageSystem.RegisterAttack` ⇒ **无与 `AttackProfile` 并行的独立攻击链路**（`SiegeWorkshopBuilding` 只做**弹药生产** `ITaskSource`，不另开攻击链）。

---

## 五、停手条件对照 ＋ §待裁 / 登记

| 停手条件 | 状态 |
|---|---|
| ① 同族**可达且后果严重**（每帧异常×主循环） | ⛔ **未触发** —— 22 处「可达」＝ **0**；两处"后果重"的（`#9`/`#11`）判 **须补证**（缺破坏性读数） |
| ② 件 B 原文**已过时** | ⚠️ **触发（列报）** —— `O-22` 两处行号漂移 ＋ 候选窗实现已被 `HH.320` 件18 改动（§3.1） |
| ③ **22 处之外**的新同族接口 | ⚠️ **触发（列报）** —— `IUnitHandle`(8)／`IAnchorConsumer`(2) 等（§二） |

**§待裁 / 登记（6 条）**
1. ⭐ **`#9`／`#11` 须补证读数**（破坏性构造：绕 `Die()` 直 `Destroy` 源 ⇒ 观测异常/是否每帧重复/是否中断该帧结算）⇒ 是否立项一批"运行期破坏性验证"。
2. ⭐ **修法口径采甲/乙案**（§1.5）＋ 是否上提中性 `IsUnityNull(object)` 口（⛔ 不复制 22 份）。
3. ⭐ **`O-22` 修法立项**（甲案 1 文件）＋ 「值不值得」锚（并发发数）。
4. ⚠️ **`O-22` 原文勘正**：行号（`:334-353` → `:363-385`；`:500-506` → `GridSystem.cs:536-542`）＋「候选窗」实现已改（`SubWindowForVisualRadius`）。
5. ⚠️ **`O-23` 分配面须补证**（本轮仪器校验后**未能复现**"每次 1 次堆分配"；⛔ 亦不断言无分配）。
6. ⚠️ **`07` §四 勘正**：「`HasLineOfSight` 全库零调用点（死码）」**已过时**（现存生产调用点 2：`BuildingComponents.cs:134`／`MonsterAI.cs:199`）。
