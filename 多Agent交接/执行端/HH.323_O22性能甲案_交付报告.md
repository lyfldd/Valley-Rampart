# `HH.323` `O-22` **性能甲案小批**（`QueryNearbyUnits` 单遍化）交付报告

- **执行端**：TraeCode｜**日期**：2026-09-24｜**性质**：施工（**单文件** · `D858` 任务书）
- **基线**：开工时 `1ea5e6a4`（`HH.322` 签发）｜提交时 `git log -1` ＝ **`7cfcba8d`**（`HH.323` 任务书签发 · ⭐ 仅新增任务书 ⇒ 与本批改动面**无交集**）| **产物**：`Logs/_o22_a*.json`／`Logs/_o22_b*.json`／`Logs/_o22_c*.json`（测量片段 `Logs/_o22_snippet*.cs` · scratch · ⛔ 非 `Assets` 面）
- **⑤ 未 push 声明**：⛔ 未 `push`、⛔ 未 rebase/reset；**改动面 ＝ 1 生产文件**（`git diff --stat` 见 §8）

---

## §0 · 结论（一句话）

⭐ 甲案落地：`QueryNearbyUnits` 由「**25 次微格窗全表扫 ＋ 25 次 `new List`**」改为「**单遍 `GetUnitsEnumerator()` ＋ 命中半径筛 ＋ 复用缓冲**」⇒ 逐发对照 **12 发「改前命中而改后漏」= 0**、耗时 **59.08 → 14.25 µs/次（4.1×）**、缓冲 **9000 次后 `Capacity` 仍 64**；编译 `error CS` 0 条。

---

## §1 · ⛔ 必做前置（**先报后改** · 已完成）

**① `candidates` 全部使用面（实读 `ProjectileManager.cs:213` 之后）**

| # | 位置 | 用法 | 是否依赖超集 |
|---|---|---|---|
| 1 | `:230-253`（命中主循环） | 逐候选过滤（`null`／`CurrentHp<=0`／野人敌意特判／己方与 `None` 跳过）⇒ `dist = GridMath.DistVisual(p.targetPos, unit.GetPosition())` ⇒ **`if (dist <= hitRadiusCells && dist < bestDist)`** 取最近 | ⛔ **不依赖**（消费者**自行重判 `dist ≤ hitRadiusCells`**） |
| 2 | `:275-281`（贯穿副循环） | 同上过滤 ⇒ **`if (dist <= hitRadiusCells && dist < throughDist)`** | ⛔ **不依赖**（同上自判） |
| 3 | 传往下游？ | ⛔ **无** —— `candidates` 仅此两处消费，不传参、不返回、不缓存（`git grep candidates` 逐条核） | —— |

**判**：⭐ **调用方不依赖超集** ⇒ 候选由「微格窗超集」变「命中半径精确集」**属等价或更严**（窗内但半径外者，两个消费点本就丢弃）⇒ **直接施工**（停手条件①未触发）。

**② 改前对照口径（实值）**：`hitRadiusCells = 0.25`（视觉格 · 调用点实参）｜`subCellDivisor = 4`（资产）｜`subRange = SubWindowForVisualRadius(0.25, 4) = 2` ⇒ **改前窗口数 = 25**。
**③ 调用点唯一性**：`QueryNearbyUnits` 生产调用点 **仅 `:213`**（另 2 处提及在 `Assets/Editor/Smoke/Valley_HH320_DamageProbe.cs:920/925` ⇒ **Editor 探针反射**，非第二调用点 ⇒ 见 §3-⑤签名兼容处理）。

---

## §2 · 改动清单（`file:line` · 单文件）

| # | 位置 | 改动 |
|---|---|---|
| 1 | `ProjectileManager.cs:212-216`（调用点） | `List<UnitController> candidates = QueryNearbyUnits(...)` ⇒ **`QueryNearbyUnitsInto(p.targetPos, hitRadiusCells, _hitQueryBuf);` ＋ `List<UnitController> candidates = _hitQueryBuf;`**（复用缓冲 ⇒ 该调用点**零中间分配**） |
| 2 | `ProjectileManager.cs:362-410`（函数体） | ⭐ **新 `QueryNearbyUnitsInto(Vector2, float, List<UnitController>)`**：`buffer.Clear()` → `UnitRegistry.Instance.GetUnitsEnumerator()` **结构枚举**（⛔ 不走 `GetAllUnits()`：接口 `foreach` 会装箱）→ `GridMath.DistVisual(worldPos, u.GetPosition()) <= radiusCells` 筛 → 写入缓冲；**体内无 `new`** |
| 3 | 同上 | **删除**原 25 次微格窗双层循环（`GetUnitsInSubCell` × `(2·subRange+1)²` ＋ `AddRange`）及 `subDiv`/`SubWindowForVisualRadius` 依赖 |
| 4 | 同上 | ⚠️ **保留兼容口** `private List<UnitController> QueryNearbyUnits(Vector2, float)`（原签名不变 ⇒ **`HH.320` 探针反射调用不受影响**）；本口**有 1 次 `List` 分配**，⚠️ **生产调用点 ⛔ 不走本口** |
| 5 | 同上 | 新增字段 `private readonly List<UnitController> _hitQueryBuf = new List<UnitController>(32);`（⚠️ 契约：仅 `OnProjectileArrived` 单帧同步使用 · ⛔ 不跨帧持有／不嵌套同名查询 ⇒ 注释已写明） |

**守卫简化说明（任务书 §三-2 要求）**：新实现**不再需要 `WorldToSubCoord`**；① `UnitRegistry.Instance == null`／`GridSystem.Instance == null`／`grid.Config == null` 守卫**保留**；② 原「`WorldToSubCoord` 越界 ⇒ 空集」改用 **`grid.WorldToCoord(worldPos).HasValue`** 同语义保留（越界 ⇒ 不写入 ⇒ 空集）；③ ⛔ 原 `Config.subCellDivisor`／`SubWindowForVisualRadius` 依赖**随微格窗一并删除**。
**假 null 注记**：枚举元素为**具体类型** `UnitController` ⇒ `u == null` 走 **Unity `==` 重载**（**有效**）——⚠️ 与 `HH.322` 记的「**接口类型**下 `== null` 失效」相反，此处**无需** `CombatRules.IsUnityNull`（注释已写明）。

---

## §3 · 判据 1：功能等价（核心）

**构造**：test-run（`seed=20321` · **1×** · `TestHarnessApi.EnterTestRun(cfg, 1f)`）⇒ 实世界 `units=33 buildings=21 timeScale=1`；另特造 3 单位 ⇒ **载荷 = 实盘 33 ＋ 特造 3 = 36**；落点 `T=(6.40, 44.48)`；改前侧 ＝ **影子实现**（逐字复刻原 25 窗：`WorldToSubCoord` ＋ `SubWindowForVisualRadius` ＋ `GetUnitsInSubCell`）｜改后侧 ＝ **生产口**（反射调 `QueryNearbyUnits`）。

**① 逐发对照（12 发 · 同构造 · 逐发可比）**

| 发 | 落点 | 改前（25 窗） | 改后（生产口） |
|---|---|---|---|
| 1 | (6.40, 44.48) | {A, B} | {C} |
| 2 | (6.44, 44.50) | {A, B} | {A, C} |
| 3 | (6.47, 44.52) | {A, B} | {A, C} |
| 4 | (6.51, 44.54) | {A, B} | {A, C} |
| 5 | (6.54, 44.57) | {A, B} | {A, C} |
| 6 | (6.58, 44.59) | {A, B} | {A} |
| 7 | (6.61, 44.61) | {A, B} | {A, B} |
| 8 | (6.65, 44.63) | {A, B} | {A, B} |
| 9 | (6.69, 44.65) | {A, B} | {B} |
| 10-12 | …（同上趋势） | {A, B} | {B} |

⇒ ⭐ **「改前命中（消费口径 `dist ≤ hitRadiusCells`）而改后漏」计数 = 0**（12 发）✅

**② 边界例（3 例特造 · 单点专测 · 落点 = T）**

| 例 | 构造 | `DistVisual(T, ·)` | 改前含 | 改后含 | 判读 |
|---|---|---|---|---|---|
| **A · 恰在半径** | 单位置于 `T + VisualToWorld(0.25)` 轴向 | **0.250** | ✅ | ⛔（浮点上略 > 0.25） | ⭐ **两侧同口径**：消费点 `:248` 用**同一 `DistVisual(T, unitPos)`** 判定 ⇒ 该浮点结果下**消费者同样不命中** ⇒ **行为等价**（⛔ 非漏命中） |
| **B · 窗外（超集面）** | 单位置于半径外 1.6 倍（≈窗内） | **0.566** | ✅ | ⛔ | ⭐ **预期收严**：半径外单位不再入候选（改前被消费点 `:248/:280` 丢弃 ⇒ **可见行为不变**） |
| **C · 登记滞后（半径内）** | 远处生成后**直接搬位**（不调登记口）⇒ `_unitSubCells` 仍指 `(298, 258)` 旧微格；实际 `DistVisual = 0.000` | **0.000** | ⛔ | ✅ | ⭐⭐ **改后修正漏命中**（原超集以「微格登记」为界 ⇒ 位移快于登记刷新者被漏）⇒ 属**更全**（非放宽：该单位确在半径内） |

⇒ **判**：命中集合**一致或更严**，且**无「改前命中、改后漏」**；唯一差异方向为「窗内半径外 ⇒ 收严（消费者本就丢弃）」与「半径内登记滞后 ⇒ 补回」⇒ ✅ **判据 1 成立**。

---

## §4 · 判据 2：零分配（⚠️ 四仪器校验后如实记）

⭐ **仪器校验（`L-93` · 先用已知量真值标定，⛔ 不采信首次读数）**：

| 仪器 | 校验读数 | 结论 |
|---|---|---|
| `GC.GetAllocatedBytesForCurrentThread()` | 对 **1000×`new byte[1024]`（真分配 ≈1.02 MB）报 `0 B`** | ⛔ **失效 ⇒ 弃用**（对本运行时全部读数作废） |
| `GC.GetTotalMemory(false)` | 对**保留** 1000×1KB ＝ **+1,396,736 B** ✅；对**瞬时垃圾**（3000 次 25 窗影子 ⇒ 78,000 个短命 List）＝ **0 B** | ⚠️ **仅能测"保留"分配 ⇒ 不可归属瞬时分配** |
| `GC.CollectionCount(0)` | 两路径 3,000 次循环期间均 **= 0** | ⛔ 无区分度 ⇒ 不可用 |
| Unity 原生 `ProfilerRecorder("GC Allocated In Frame")` | 校准帧（保留 1 MB）＝ **+3,060,395 B** ✅ 响应；但**空帧① 41,352 B／空帧② 3,894,263 B**（噪底 ≫ 信号）⇒ 改后 −35.76 B/次、改前 −20.12 B/次（**负值**） | ⛔ **整帧量不可归属被测函数** ⇒ 不采信 |

⇒ ⭐ **判据 2 结论（结构性 ＋ 可测部分）**：
- **生产路径 `QueryNearbyUnitsInto` 体内无任何 `new`**（码面逐行 · §2#2）⇒ 无自身分配；
- **复用缓冲 9,000 次调用后 `Capacity` 仍 = 64**（初值 64）⇒ **未扩容＝无二次分配** ✅（本构造下可测）；
- 生产调用点（`:212-216`）**不再 `new List`**（改前每发 **26 个**：25 窗各 1 ＋ 结果 1）⇒ **每发中间 `List` 分配 26 → 0**；
- ⚠️ **「0 B 分配」的仪器级实测在本运行时不可得**（四仪器口径与失效模式如上表）⇒ 记为 **结构性结论 ＋ 缓冲不扩容实测**，⛔ 不写成"仪器实测 0 B"。

---

## §5 · 判据 3：耗时双列（带测法 · ≥3 轮取 min · 同载荷）

| 口径 | 改前（25 窗 ＋ 每窗 `new List`） | 改后 · 兼容口（含 1 次 `List`） | 改后 · **生产路径**（缓冲 · 零反射） | 倍数（生产 vs 改前） |
|---|---|---|---|---|
| 第 1 组（3 轮 min） | **59.04 µs/次** | 16.75 µs/次 | —— | 3.5× |
| 第 2 组（3 轮 min · `Delegate.CreateDelegate` 去反射噪声） | **59.08 µs/次** | —— | **14.25 µs/次** | ⭐ **4.1×** |

⚠️ 口径声明：两组的「改前」值独立复现（59.04／59.08 ⇒ 稳定）；「改后」组间差（16.75 vs 14.25）＝**是否含反射 `Invoke`／1 次 `List` 分配**之別 ⇒ 生产路径取 **14.25 µs/次**。
⚠️ 与 `O-22` 原文 **≈171 µs/发** 的差异：原文计**整发**（含 `QueryNearbyUnits` 之外的发射/推进开销）｜本批只测**查询函数本体** ⇒ ⛔ 两者不可逐值对齐（本批给的是函数级双列）。

---

## §6 · 判据 4：并发在飞发数（"值不值得"的锚）

| 读数 | 值 | 口径 |
|---|---|---|
| ⭐ **构造齐射峰值** | **peak = 60（第 5 帧）** | 60 名弓手同一帧 `RegisterAttack`（弹速 5 · 目标 34 格轴）⇒ 逐帧采样 `ProjectileManager._active.Count`（65 帧） |
| ⚠️ **实盘长局峰值** | **未测** | 需要：**长局观测**（≥30 游戏日 · 含多波次来袭/多塔齐射），并逐帧采样 `_active`（建议挂在既有考跑容器里做，⛔ 本批不扩范围） |

⇒ 「值不值得」的量级锚：**每发省 ≈44.8 µs（59.08→14.25）** ⇒ 10 发/帧 ≈ **0.45 ms/帧**；60 发/帧 ≈ **2.7 ms/帧**（构造齐射档）。

---

## §7 · 判据 5：编译／相邻面

- **编译**：`refresh_unity(force)` ⇒ `read_console{types:[error], filter_text:"error CS"}` ＝ **Retrieved 0 log entries**（`error CS` **0 条**）✅
- **相邻面**：建筑兜底链（`:216-262`「单位优先 → 建筑底座兜底 → 唯一早退点」）**逐行未动**（diff 仅 §2 所列 5 处）✅；⚠️ 兼容口签名保留 ⇒ **`HH.320` 探针（`Valley_HH320_DamageProbe.cs:925` 反射）不破** ✅
- ⭐ 附带：本批未改 `UnitRegistry`／`GridSystem`／`GridMath`／`CombatRules`（判据 6 同证）✅

---

## §8 · 判据 6：红线（`git diff` 证据 · 未 push）

```
$ git diff --numstat -- "Valley Rampart/Assets/_Game/Systems/Combat/ProjectileManager.cs"
47      17      Valley Rampart/Assets/_Game/Systems/Combat/ProjectileManager.cs   ← +47/−17

$ git diff -U0 -- …/ProjectileManager.cs | grep '^@@'
@@ -212,2 +212,5 @@      ← 调用点（缓冲化）
@@ -362 +365,8 @@
@@ -366 +376,3 @@
@@ -368,7 +380,26 @@     ← 函数体（删除 25 窗循环 · 新增单遍实现 ＋ 缓冲字段）
@@ -376 +407,2 @@
@@ -378,4 +410,3 @@
@@ -383 +413,0 @@
```
- ⭐ **本批改动 = 该 1 文件（7 处 hunk · 全部在 `ProjectileManager.cs`）**；工作区其余脏项（`.gitignore`／`GameScene.unity`／`TaskScheduler.cs`／`Packages/*`／`pixel-forge/*`／美术 PNG 等）**均为并行会话既有** —— 证据：本批开工前（`HH.322` 零改动核对档）`Assets` 面脏项清单**不含 `ProjectileManager.cs`**，而**含**上述诸项。
- ⛔ **未 `push`**（⛔ 未 rebase/reset；本批改动仅落本地工作区）。

---

## §9 · §待裁 / 登记

| # | 项 | 说明 |
|---|---|---|
| 1 | ⭐ **实盘长局并发峰值**（判据 4 缺口） | 需长局观测容器（⛔ 本批未做）⇒ 是否并入下一轮考跑观测 |
| 2 | ⚠️ **分配仪器缺口**（判据 2 缺口） | 本运行时 4 种仪器均无法归属**瞬时**分配（§4）⇒ 建议后续以「长局 `GC.Alloc` 分帧统计 ＋ 大样本对比」另立（⛔ 本批不扩） |
| 3 | ℹ️ **兼容口保留** | `QueryNearbyUnits(…)` 仍在盘（探针用 · 1 次 `List` 分配）⇒ 若后续探针迁移到 `QueryNearbyUnitsInto`，可删该口（本批 ⛔ 不动） |
| 4 | ℹ️ 边界浮点 | 「恰在半径」单位在 `DistVisual` 浮点上略 > 半径 ⇒ 与消费者**同口径**一致（§3-② A 例）⇒ 如需"含边缘"语义应改**判据常量**（⛔ 属口径决策 · 非本批） |
