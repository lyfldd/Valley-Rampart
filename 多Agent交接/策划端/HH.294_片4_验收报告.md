# HH.294 底层重构批 · 片 4「地图四门」验收报告

> **端**：主策划端（砚）｜**裁决**：`D771`｜**日期**：2026-09-17
> **被验对象**：`12497dd6`（`HH.301` 片 4 交付报告 ＋ `hh294_slice4_probe.log`）
> **口径源**：`最高优先级文档/03_地图即数据库.md`（§六~§九）／`04_上层接入指南.md`（§六 五条不许）／`底层执行计划.md` §二 批 2
> **验收方式**：**独立复读**（不采信自述）—— `git show --numstat` 复算 ＋ 原件日志直读 ＋ 全库机械枚举 ＋ 手工核调用面

---

## 〇、结论

**销号成立（🟢）。** 九项（4-A~4-H）＋ 三项搭车（R1/R2/R3）全部落地，探针 `PASS=39 / FAIL=0`，读数逐条可复现。

**但查出 3 条必改残余**，其中一条是**能力句被证伪**，一条是**我上轮也漏的**，一条根因在**我方判据缺口**。

| 项 | 判 |
|---|---|
| P1~P13 判据主体 | ✅ 全闭（逐条独立复读见 §一） |
| **R1** 探针 `* div` 声称清零 → 实残留 4 处 | ⚠️ **必改** |
| **R2** 判据 2 能力句「生成期」限定不成立（4 处运行期可达） | ⚠️ **必改（最重）** |
| **R3** R3 迁移引入两个 O(非1) 兜底，未测耗时 | ⚠️ **必改** |
| `M1`~`M9` 微瑕 | 记录不阻断 |

---

## 一、判据逐条独立复读

| 判据 | **我独立读得**（与报告差异处标 ⚠️） | 判 |
|---|---|---|
| **1** 写点 | 改前（`12497dd6^`）生产码宽式 **12 处 / 4 文件**（`MapGenRules 9`／`MapValidator 1`／`ResourceRespawnSystem 1`／`WorldManager 1`）→ 改后 **1 处**（`MapGate.cs:33` `WriteRaw`，**private**）；另一命中 `:27` 为注释。能力句成立 | ✅ |
| **2** 裸读面 | 改前 **46**（逐文件与报告全同）→ 改后命中 **32 行** ＝ 门内 11（**8 代码 ＋ 3 注释**）＋ 生成期 18 ＋ 他文件注释 3 ⇒ **真裸读 25**（门内真读 7 ＋ 生成期 18）⚠️ 报告写 29（见 `M3`）⚠️ **能力句限定不成立**（见 `R2`） | ⚠️ |
| **3** 4-B 禁抹树 | 原件：Tree 格 (31,2) 经「声明不符消费／声明相符消费／真实 `quarry` 建造」三条路径后 **features 仍 = Tree**；`quarry` ⇒ `TryBuild = False`；门内声明校验真在（`MapGate.cs:349` ＋ `MatchesDeclaredAnchor:357-359`）；`BuildController:288` 改后**带声明传参** ✅ 治本 | ✅ |
| **4** 4-C 单一删门 | `TryConsumeResourceNode` **定义 ＋ 调用全删**（全库命中尽为注释／探针反射取证）；`MapGate.RemoveResourceNode:286` 唯一删入口；`SetFeature:225` 通用改口 | ✅ |
| **5** 4-D 注销合一 | `ReleaseLayerOwnedState` **实现 1 处**（`Building.cs:847`，private）＋ `Die:887`／`OnGatherCompleted:915` 各调一次（原抄两遍已合一）；`BuildingDestroyedEvent` 定义已删（`GameEvents.cs:494` 注释块留痕）＋ **0 订阅 0 发布**；`LifecycleAudit.cs:131` 白名单字符串残留（报告已诚实单列） | ✅ |
| **6** 4-E 锚点返还 | 原件 ①消费 `True` → ②消费后不可再消费 `False` → ③返还 `True` ＋ 引用清 `null` → ⭐④返还后**可再消费** `True` → ⑤幂等 `False`；`IAnchorConsumer:323`；`BuildingSaveData` 尾插 3 字段（`-1` 表无·零 bump） | ✅ |
| **7** 4-F 查询口 | `TryGetCell:125` 一次 6 项；4 种 filter 皆可用（1477／75／1779／1971）；`CountFeatures` 75 与枚举 **75 一致**；`maxCount`／半径 251／按实体 177 皆可；**稳态 200 次净分配 0 B** ⚠️ **只证分配、未证耗时**（见 `R3`） | ⚠️ |
| **8** 4-G `Replaced` | `ReplaceFeature:248` **只发一条** `MapReplacedEvent`（`:264-265`，非 Removed+Added）＋ 同值 `False` 零广播；定义 `GameEvents.cs:503` | ✅ |
| **9** 4-H 可否拆 | `Building.CanDemolish:647` 数据栏；`BuildingPanel` 内 `isDestructible`／`isResourceNode` **仅 `:169` 一处且为注释** ⇒ 活引用 **0** | ✅ |
| **10** R2 24 座零登记 | 根因链实读成立：`GridSystem.RebuildOccupancyFromRegistry`（`:416+`）确在 `PopulateFromMap` **之后**、且**在我上轮 `R1` 修的 `if (GridSystem.Instance != null)` 块内**；探针全量 **11892 座 ⇒ 零登记 0**；能力句失配 0 | ✅ |
| **11** R3 子格域迁移 | `GetOccupant:319-337` 真迁移（快路径代表位 ＋ 慢路径扫同块余 div²−1）；`IsObstacle` 转调；新增 `MarkOccupiedSub`／`FreeSub`；探针**构造出真的部分占格用例**（只写末子格 763,755）并给出「改前式读法会失真 = null」的存在性反证 ⚠️ **代价未测**（见 `R3`） | ⚠️ |
| **12** R1 探针改向 | `:246`／`:275`／`:286-287` 改后确走 `CellToSub` ⚠️ **但"`* div` 0 命中"为假**（见 `R1`） | ⚠️ |
| **13** 常规 | 22 改动文件行尾：**19 LF ＋ `GameEvents.cs` 工作区 CRLF（git blob 仍 LF ⇒ diff 面 16/6，无幽灵）**、**无 BOM**；`AI.Core` 0 命中（报告）；**编译／warning 面我无法自测**（不碰工程）⇒ 采信并声明 | ✅ |

> ⚠️ 我自己的基线纪律失误：首次复核误用 `HEAD`（＝本片 commit）当"改前"，改正为 `12497dd6^` 后才得真读数。记此一笔。

---

## 二、我查出的问题

### 必改（3 条）

#### `R1` · 「`* div` 改后 0 命中」为假 — 实残留 4 处

`Valley_HH294_Slice3Probe.cs` 改后 `* div` 命中：

| 行 | 内容 | 定性 |
|---|---|---|
| **`:359`** | `new GridCoord((b.coord.x + dx) * div + sx, (b.coord.y + dy) * div + sy)` | ⭐ **完整坐标就地展开**（≡ `CellToSub(cell, sx, sy)`）—— **未改** |
| `:186` | `wf[(cy * div) * SW + cx * div]` | 数组索引算式（非坐标口）⇒ 可豁免，**须显式声明理由** |
| `:190` | `wf[(cy * div + sy) * SW + cx * div + sx]` | 同上 |
| `:246` | 新增的中文注释 | 非代码 |

⇒ 报告 §一 判据 12 的复现命令（`Select-String -Pattern "\* div"`）**必然命中** ⇒ 该断言**未跑即写**（或跑了未回填）。
⭐ **另记：`:359` 也是我上轮（`D770`）片 3 验收的漏项** —— 我当时用的正则 `.x * div` 不匹配 `(b.coord.x + dx) * div`。**双方各漏一次**，写进教训库。

#### `R2` · ⭐ 判据 2 能力句的「生成期」限定**不成立**（本条最重）

报告原句：「除 `MapGate` 门自身**与 `MapGenRules` 生成期（0→1 造世界）**外，生产码无任何代码直接读 `map.features[]`」。

**实测 18 处的函数归属 ＋ 调用面：**

| 函数 | 处数 | 行 | **调用面（实读）** | 定性 |
|---|---|---|---|---|
| `FillFeatures` | 4 | 773／833／857／882 | `WorldManager.cs:184` 步骤4 | 生成期 ✅ |
| `ClearKingdomZones` | 2 | 1016／1032 | `:194` | 生成期 ✅ |
| `EnsureChunkResourceQuota` | 2 | 1062／1090 | `:196` 步骤6.6 | 生成期 ✅ |
| `PlaceWater` | 2 | 1319／1348 | `:199` 步骤9 | 生成期 ✅ |
| `DeriveNaturalBuildings` | 1 | 1417 | `:202` 步骤11 | 生成期 ✅ |
| **`ZoneOf`** | **2** | **118／128** | ⛔ **运行期**：`BuildingVisual.cs:119`／`MapRenderService.cs:464`／`MapGenDebugDrawer.cs:49` | **运行期可达** |
| **`NearestWalkable`** | **2** | **1270／1279** | ⛔ **运行期**：`KingdomFoundry.cs:131,146,195,209,244,254,491`／`VagrantCampSystem.cs:185,413,417`／`MineByproductComponent.cs:234` | **运行期可达** |
| `MatchesPreferredFeature` | 1 | 1209 | 仅 `PickSpawnForTemplate`(@1142) ← 仅 `PlaceKingdomSpawns`(@1106) | 生成期 ✅（传递核过） |
| `ChunkFeatureRatio` | 1 | 1238 | 仅 `MatchesPreferredFeature` | 生成期 ✅ |
| `ReadPitStats` | 1 | 68 | 仅 `Editor/Smoke/Valley_HH291_MapGenProbe:171` | **Editor-only** |

⇒ **4 处（`ZoneOf`×2 ＋ `NearestWalkable`×2）在运行期被生产码调用**，此时 `ActiveMap` 非 null ⇒ **走门完全可行**；报告"走 `GenesisRead` 更绕且无收益"的理由**对这 4 处不成立**。
⇒ 能力句须**重写**，限定改为「除门自身与**纯生成期函数**（上表 5 个）外」，**且这 4 处必须走门**。
⚠️ 附带观察：`NearestWalkable` 是**运行期热点**（预置落点／流浪营／矿山副产品），内部 O(半径²) 窗口 ＋ 逐格 features 读 ⇒ 单列观察项。

#### `R3` · R3 迁移引入两个 O(非 1) 兜底，**未测代价**

- `GridSystem.GetOccupant` 慢路径（`:327-336`）：代表位未命中 ⇒ 扫同块余 **div²−1 = 255** 子格 ⇒ **空地块读 从 O(1) 退化到 O(256)**
- `GridSystem.GetUnitCountInCell`（`:520-526`）：`foreach (_unitSubCells)` ⇒ **O(单位数)**
- 而 **`TryGetCell`（`:125`）每格都调这两者** ⇒ `QueryCells` **每格 O(256 ＋ N)**
- 探针只给「200 次查询**净分配 0 B**」＋条数 ⇒ **未给耗时**，无法判断区域枚举可接受性（`QueryOccupants` 覆盖 2401 格 ⇒ 慢路径被大量触发）。

---

### 微瑕（`M1`~`M9`·记录不阻断）

| # | 内容 |
|---|---|
| `M1` | 判据 4「改后仅注释 3 处」：实测**生产码注释 8 行**（`GuardDeployment 3`／`BuildController 1`／`MapGate 2`／`ResourceRespawnSystem 1`／`WorldManager 1`）＋ Editor 3 ⇒ **数字错**（`L-02` 家族） |
| `M2` | 判据 1 宽窄正则漏计的**归因位置错**：§一 称漏的是 `MapValidator` 那处 `cells[k]`，实测漏的是 **`MapGenRules.cs:798`**（`map.features[cells[k]] = …`）；§四 勘正 4 又写 `:799`（实为 **`:798`**）⇒ 同一条勘正三个数字两处错 |
| `M3` | 判据 2「报告所有真裸读数字均已手工剔除注释行」与实测不符：**门内 3 行注释未剔** ⇒ "29" 应为 **25** |
| `M4` | `BuildController:288` 调 `MapGate.ConsumeAnchor(...)` **未检查返回值** ⇒ 声明不符／非锚点格时**静默**（无锚点、建造照常）⇒ 宜 `if (!…) { 回滚/告警 }` |
| `M5` | `ReleaseLayerOwnedState(bool unregisterFromRegistry)` 两处调用**恒传 `true`** ⇒ **死参数**（注释称"采集路径自行处理"，但采集路径同样传 true） |
| `M6` | `PlaceResourceNode:277` 直接 `=> SetFeature(coord, f)` ⇒ **未做「位置空闲」校验**（`03` §7.4 增门口径＝位置空闲）；落点非 Plain 时**直接覆写** |
| `M7` | `MapGate.CountFeatures:211` 无 `i < map.features.Length` 保护（同文件 `ReadAt:54`／`GetFeatureAt:69`／`TryGetCell:135` 皆有）⇒ **保护口径不统一** |
| `M8` | `TryGetCell` 恒算 6 项（含 `OwnerAt` 字典查），而 `CellFilter.Accept` 只用 3 项 ⇒ 区域枚举**冗余计算** |
| `M9` | `ReturnAnchor:370-373` **先清引用后判定** —— `IsResourceNodeFeature(f)` 为假则引用已清、锚点未返还（**静默丢锚点**）；引用由 `ConsumeAnchor` 写入时已过校验 ⇒ 现实不可达，但属**新增静默失效面**（与片 1 `R1` 同类） |

---

## 三、我方缺陷（判据缺口 · 非执行端过错）

1. **判据 7 未要求耗时** ⇒ 执行端只报分配不报耗时（`R3` 的直接后果）。
2. **判据 2 未要求"逐函数 ＋ 调用面"两列** ⇒ 它按**文件**分列，因而把运行期可达的 `ZoneOf`／`NearestWalkable` 混入"生成期"一栏（`R2` 的直接后果）。**缺口在"可达性"维度**。
3. **我上轮片 3 验收正则 `.x * div` 不够宽** ⇒ 漏 `(b.coord.x + dx) * div`（`R1` 的 `:359`）。

---

## 四、请裁-1 裁决（`MapGenRules` 生成期读是否收口）

**不采纳「留片 5/6 一并」。拆开处理：**

1. **`ZoneOf`×2 ＋ `NearestWalkable`×2（运行期可达）⇒ 必改走门**，并入**片 4 补正小批**（不拖到片 5）—— 这 4 处是能力句被证伪的直接原因；留着则片 5/6「裸读收口」的契约基础不稳。
2. **11 处纯生成期读**（`FillFeatures`4／`ClearKingdomZones`2／`EnsureChunkResourceQuota`2／`PlaceWater`2／`DeriveNaturalBuildings`1）⇒ **可留**（造世界 0→1 语义），**报告中限定句须重写准确**。
3. **`ReadPitStats` 1 处**（Editor-only 调用）⇒ 留，不进生产码收口面。

---

## 五、销号与残余

- **销号：成立** —— 九项 ＋ 三搭车全部落地，读数可复现，探针 `PASS=39 / FAIL=0`。
- **残余（打成「片 4 补正小批」，不另开片）**：

| 编号 | 内容 | 级别 |
|---|---|---|
| `R1` | 探针 `:359` 改走 `CellToSub` ＋ `:186/:190` 补豁免声明 | 微改 |
| `R2` | **4 处运行期可达裸读走门 ＋ 能力句重写准确** | **必改** |
| `R3` | 补 `QueryCells`／`GetOccupant` 未命中／`GetUnitCountInCell` 的**耗时读数** | **必测** |
| `M1`~`M9` | 九条微瑕（`M4`／`M7`／`M9` 建议随手收） | 记录 |

- **下游不变**：片 5（资源池 ＋ `F-12`）→ 片 6（双写收敛 ＋ 拾取坐标化）。

---

## 六、收口三问

**Ⅰ 根因** —— 功能面全闭（九项 ＋ 三搭车）。偏差集中在**三处"声称越过实盘"**：①「`* div` 0 命中」（未跑即写）②「18 处皆生成期」（未核调用面）③「注释已剔全」（未剔全）。共同点 ＝ **断言写成了"超出本次取证范围"的形式**。

**Ⅱ 流程漏洞** —— 惯例要求"给读数"，但**未要求每条"清零／全 X／均 X"类断言附「断言本身的复现命令输出片段」** ⇒ 缺口在**"断言自检"未入收尾动作**。与 `L-37`（上限句）同族但不同型（这是"断言未跑"，不是"判据区间不辨"）⇒ **`L-30` 加维度**。

**Ⅲ 有条目但没查** —— `L-02`（汇总数字须附可复现命令）本批**第 3 次**命中（`M1`／`M2`）⇒ 处置：**把"每条清零类断言附命令输出"从"报告要求"升格进派工提示词的「红线」段**，下一批即生效。

---

## 七、落账

- 本报告（`策划端/HH.294_片4_验收报告.md`）
- 台账 **§四十二**｜`_编号登记`（`D771`／`HH.301`）｜`_任务队列`（片 4 销号 ＋ 补正小批行）｜`_当前快照`（水位线／在途／§七／commit 链）｜教训库
- ⚠️ `_任务队列.md` 为 **MIXED（265 CRLF ＋ 5 LF ＋ 1 NUL）** ⇒ 走 python 二进制按行替换，保留原行尾
- ⚠️ `0.6_审查决策记录.md` 未写（自 `D739` 停更·按**实况口径**落台账 §N）
