# HH.302 · 底层重构批 · 片 4「补正小批」交付报告（待策划验收）

> **端**：执行端　｜　**批次**：`HH.294` 底层重构批 · **片 4 补正小批**（承 `HH.301` 片 4 / `D771` 验收）
> **要求源**：`多Agent交接/策划端/HH.294_片4_验收报告.md`（`D771`）§二 必改 3 条 ＋ §二末 微瑕 `M1`~`M9`
> **口径源**：`最高优先级文档/03_地图即数据库.md`（§六~§九）／`04_上层接入指南.md`（§六 五条不许）
> **交付时间**：2026-09-17　｜　**行尾**：本报告 LF　｜　**取号**：按水位线（HH.301 → **HH.302**）
> **探针**：`Valley Rampart/Logs/hh302_slice4fix_probe.log`（**PASS=10 / FAIL=0**）
> **回归**：`hh294_slice4_probe.log`（`PASS=39 / FAIL=0`）／`hh294_slice3_probe.log`（`PASS=14 / FAIL=1`·见 §三-1 归因）

---

## 〇、一句话结论

`R1`／`R2`／`M4`／`M7`／`M9` **已落地**；`R3` **四组代价读数已实测**，结论＝**不可接受（需报裁）**——根因是 `TryGetCell` 每格都付 `GetUnitCountInCell` 的 **O(N) 全表扫描**（`N=500` 时 2401 格窗口 **76.88 ms ＝ 463% 帧预算**）。
`R3` 所述「`GetOccupant` 慢路径扫 **div²−1 = 255**」**与实盘不符**：`SubDiv = 4` ⇒ 真实扫描量 **15**（该路径**不是**主要成本项，实测仅 +0.051 µs/格）。

---

## 一、判据逐条读数

### 判据 1 · `R1` 探针就地展开

**① `:359` 改后原文行**（[Valley_HH294_Slice3Probe.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH294_Slice3Probe.cs#L365-L366)）：

```csharp
// 【HH.294 片4补正·R1】原此处**就地展开** `(b.coord.x + dx) * div + sx` —— 改走唯一换算口 `GridSystem.CellToSub`
var sub = grid.CellToSub(new GridCoord(b.coord.x + dx, b.coord.y + dy), sx, sy);
```

**② `:186`/`:190` 处置 ＝ 选项②（保留 ＋ 显式豁免注释）**，原文（[同文件 :186-192](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH294_Slice3Probe.cs#L186-L192)）：

```csharp
// 【HH.294 片4补正·R1 豁免声明 ①／②】下面两行是**子格数组索引算式**，**非坐标换算口** ⇒ 不走 `CellToSub`：
//   `wf` ＝ `GridSystem._walkFlags`（`WalkFlags[]`，**小格子域**一维扁平数组，长度 `SW×SH`）；
//   `SW` ＝ `grid.Width` ＝ `map.width * div` ＝ **子格域每行的跨距（行主序 stride）**；
//   `wf[(cy*div+sy) * SW + (cx*div+sx)]` 是「第 (cy*div+sy) 行第 (cx*div+sx) 列」的直接下标，
//   它自己**不产出任何格坐标**（无 `GridCoord` 输出、无域转换语义）⇒ 属纯下标运算，豁免。
//   （对照：本文件 `:359` 那一处**产出 `GridCoord`** ⇒ 是真换算口，已改走 `CellToSub`。）
WalkFlags first = wf[(cy * div) * SW + cx * div];
```

**③ ⭐ 全库 `* div` 实测命中 ＝ 23 行 / 7 文件**（复现命令与**输出片段**见 §一末「判据 6」）。逐处定性：

| 定性 | 处数 | 位置 |
|---|---|---|
| **真·坐标就地展开（产出 `GridCoord`）· 生产码** | **0** | — |
| 真·坐标就地展开 · Editor（**刻意保留的反例式**，即判据夹具本身） | 2 | `Valley_HH294_Slice4Probe.cs:118`／`Valley_HH302_Slice4FixProbe.cs:98` |
| 子格数组**索引／步长**算式（非换算口） | 4 | `Slice3Probe:192/196`（**已加豁免声明**）／`GridSystem:66/87` |
| 唯一换算**内核本体**（`CellToSub` 实现式，定义即此处） | 1 | `GridSystem.cs:250` |
| 值域／半径／面积算式（`maxR * div`／`div * div` 作分母或计数） | 10 | `PlacementScorer:86/88/144/151`／`Smoke_2_22P0:431`／`Valley_HH144_Probe:225`／`HH302:225/228/229/230/234` |
| 注释／文档行 | 6 | `Slice3Probe:188/252/365`／`HH302:14/103` |

⇒ **`CellToSub` 与就地展开等价**（[HH302 探针 §R1]）：抽样 2000 组（地块 ＋ 格内偏移 `sx,sy ∈ [0,4)`）**不一致数 = 0**。

---

### 判据 2 · `R2` 4 处运行期可达裸读走门

**① 4 处改后原文行**（[MapGenRules.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGenRules.cs#L119)）：

| 改前行 | 函数（**实读勘正**，见 §四-1） | 改后原文行（当前行号） |
|---|---|---|
| `:118` | `HasFullBlock`（**非** `ZoneOf`） | `:119` `if (MapGate.ReadAt(map, ox + dx, oy + dy) != need) return false;` |
| `:128` | `IsClearBlock`（**非** `ZoneOf`） | `:130` `if (MapGate.ReadAt(map, ox + dx, oy + dy) != FeatureType.Plain) return false;` |
| `:1270` | `NearestWalkable` | `:1275` `if (InB(map, cx, cy) && IsWalkableFeature(MapGate.ReadAt(map, cx, cy))) return new Vector2Int(cx, cy);` |
| `:1279` | `NearestWalkable` | `:1284` `if (InB(map, x, y) && IsWalkableFeature(MapGate.ReadAt(map, x, y))) return new Vector2Int(x, y);` |

**② 界判定核验（改前必核项）**：4 处**原式皆已带界保护** —— `HasFullBlock`/`IsClearBlock` 首行 `if (ox<0||oy<0||ox+side>map.width||oy+side>map.height) return false;`；`NearestWalkable` 两处均 `InB(map,…) &&` 前缀。`ReadAt` 越界返 `Plain` 的**差异路径在此不可达** ⇒ **等价，无口径不明项，未触「停报裁」**。
实证：全图 384×384=**147456** 格 `MapGate.ReadAt` 与原始裸读**不一致数 = 0**（HH302 探针 §R2①）；`NearestWalkable` 以**独立判据**（全图暴力求最小切比雪夫距离）验证 **8/8 一致**（HH302 探针 §R2②）。

**③⭐ 全库裸读「逐函数 ＋ 调用面」两列（`Assets/_Game/**`，改后）**

| 函数 | 处数 | 行（改后） | **调用面（实读）** | 可达性定性 |
|---|---|---|---|---|
| `FillFeatures` → `PruneMountainSpecks` | 1 | `:775` | `FillFeatures`(`:705`) ← `WorldManager.cs:184`（步骤4）＋ 探针 `HH272:41`/`HH291:56` | **纯生成期** |
| `FillFeatures` → `PlaceResourceQuota` | 3 | `:835/859/884` | `FillFeatures`(`:706`) | **纯生成期** |
| `ClearKingdomZones` | 2 | `:1018/1034` | `WorldManager.cs:194`（步骤6.5）＋ 探针 `HH272:44`/`HH291:58` | **纯生成期** |
| `EnsureChunkResourceQuota` | 2 | `:1064/1092` | `WorldManager.cs:196`（步骤6.6）＋ 探针 `HH272:45`/`HH291:59` | **纯生成期** |
| `PlaceWater` → `PruneOrphanMineCells` | 1 | `:1324` | `PlaceWater`(`:1318`) ← `WorldManager.cs:199`（步骤9）＋ 探针 | **纯生成期** |
| `PlaceWater` → `PlaceRiver` | 1 | `:1353` | `PlaceWater`(`:1313`) | **纯生成期** |
| `DeriveNaturalBuildings` | 1 | `:1422` | `WorldManager.cs:202`（步骤11）＋ 探针 `HH272:54`/`HH291:67` | **纯生成期** |
| **小计（11 处 · 收敛于 5 个纯生成期入口）** | **11** | — | — | ✅ 可留 |
| `ReadPitStats` | 1 | `:68` | 仅 `Editor/Smoke/Valley_HH291_MapGenProbe.cs:171` | **Editor-only**（`D771` §四-3 裁「留」） |
| `MatchesPreferredFeature` | 1 | `:1211` | `PickSpawnForTemplate`(`:1187`) ← `PlaceKingdomSpawns` ← `WorldManager.cs:192`；另 Editor `Valley_HH140_Probe:136~143` | **生成期传递**（裁「留」） |
| `ChunkFeatureRatio` | 1 | `:1240` | 仅 `MatchesPreferredFeature`(`:1217/:1220/:1223`) | **生成期传递**（裁「留」） |
| **小计（3 处 · 已裁留）** | **3** | — | — | ⚠️ 保留裸读（红线① 明令不碰） |
| `MapGate` 门自身（读代码） | 6 | `:54/69/135/233/255/293` | 门内核（读写唯一收口处） | 门自身 |
| `MapGate.WriteRaw`（**唯一写内核**） | 1 | `:33` | 同上（`private`） | 门自身 |

**④ 改前／改后计数对照（逐项可复算）**

| 项 | 改前（`HEAD`＝`c8b5b6f1`） | 改后 | 复现命令（输出片段见 §一末） |
|---|---|---|---|
| `MapGate.cs` 命中行 | 11（**8 读代码** ＋ 3 注释） | 11（**7 读代码 ＋ 1 注** ＋ 3 注释） | `git grep -n "\.features\[" HEAD -- …/MapGate.cs` |
| `MapGenRules.cs` 命中行 | **18**（全代码） | **14**（全代码） | 同左（改后直接 grep 工作区） |
| **生产码真裸读合计** | **25**（门内真读 7 ＋ 生成期 18） | **20**（门内真读 6 ＋ MapGenRules 14） | 同左 |
| 其余生产码（`MapRenderService`/`ResourceRespawnSystem`/`WorldManager`） | 1＋1＋1（**皆注释行**） | 不变（皆注释行） | 同左 |
| Editor 探针面 | 12 文件 | **12 文件**（本批**未动**，仅新增本批探针 2 处注释性 `map.features[]` 字样） | `Select-String -Path Valley Rampart\Assets\Editor\**\*.cs` |

**⑤⭐ 能力句（重写后原文）**：

> **除 `MapGate` 门自身、`MapGenRules` 内 11 处纯生成期裸读（收敛于 `FillFeatures`／`ClearKingdomZones`／`EnsureChunkResourceQuota`／`PlaceWater`／`DeriveNaturalBuildings` 5 个入口）与 3 处已裁留裸读（`ReadPitStats`【Editor-only 调用】／`MatchesPreferredFeature`／`ChunkFeatureRatio`【生成期传递】）外，生产码（`Assets/_Game/**`）无任何代码直接读 `map.features[]`。**

⚠️ **与提示词给定字面的差异（列报，见 §六 请裁-2）**：提示词要求的能力句只列「5 个纯生成期函数」，**未含**上述 3 处已裁留 ⇒ 该字面式**不成立**（会与红线①「不碰已裁留项」冲突）。本批按「保留 3 处 ＋ 能力句显式补 3 例外」执行，并把 3 处逐项列进上表第二段。

---

### 判据 3 · `R3` 四组耗时读数 ＋ 帧预算对照 ＋ 结论

> 环境：正门 `EnterTestRun` · 384² · seed=21107 · 同一局内实测；`div = SubDiv = 4`。

**① `GetOccupant`：命中 vs 未命中**

| 项 | 单次均摊 | 备注 |
|---|---|---|
| 命中（代表位即中，快路径） | **0.0368 µs**（200000 次） | 用例格 (359,20) |
| **未命中**（空地块，慢路径） | **0.0878 µs**（20000 次） | 用例格 (189,188) |
| 倍率 | **2.4×**（Δ = +0.051 µs） | 慢路径扫同块余 **15** 子格（`div²−1`，**非 255**） |

⇒ **未命中惩罚对 2401 格窗口仅 +0.12 ms ≈ 不构成成本项。**

**② `GetUnitCountInCell`：三档单位数（`_unitSubCells` 为 `Dictionary` ⇒ O(N)）**

| 档 | 实测 N | 单次均摊 |
|---|---|---|
| A（基线） | **23** | **2.03 µs** |
| B（补到 ≥100） | **100** | **6.37 µs** |
| C（补到 ≥500） | **500** | **31.93 µs** |
| 均摊 | — | **≈ 0.0627 µs／单位／次**（三档单调增 ⇒ O(N) 成立） |

（夹具：`AIDebugSpawnController.Spawn(PlayerCivilian)` 真生成单位并 `TryEnter` 登记；测毕 `RemoveUnit` ＋ `Destroy` 回收，回收后实测 N 回落 **23**。）

**③ `TryGetCell` 单次（6 项复合）**：49×49=**2401 格**全量中位 **7.6387 ms** ⇒ 每格均摊 **3.181 µs/格**（min 7.3681／max 8.6780，reps=20，N=23）。

**④ ⭐ `QueryCells` 2401 格窗口 ＋ 与 60fps 帧预算（16.6 ms）对照**

| 口 | N=23（基线） | 每格均摊 | 占帧预算 |
|---|---|---|---|
| `QueryCells`（条件＝可走） | **7.4648 ms** | 3.109 µs/格 | **44.97%** |
| `QueryCells`（条件＝地表=Plain） | **7.2224 ms** | 3.008 µs/格 | 43.51% |
| `QueryOccupants`（按实体） | **0.2214 ms** | 0.092 µs/格 | 1.33% |
| `CountFeatures`（**不调 `TryGetCell`**） | **0.0112 ms** | 0.0047 µs/格 | 0.07% |
| `QueryCells`（条件＝可走）· **N=500** | **76.8796 ms** | 32.02 µs/格 | **463.13%** |

- ⭐ **成本归因（同窗口对照）**：纯逐格 `ReadAt`（`CountFeatures`）＝ **0.0112 ms**，复合 6 项（`QueryCells`）＝ **7.4648 ms** ⇒ **99.85% 的成本来自「每格组 6 项事实」，而非读地表本身**。
- ⭐ **存在性反证（随 N 线性上涨）**：N=23 ⇒ 7.4648 ms；N=500 ⇒ 76.8796 ms；**Δ = 69.41 ms**，每 +1 单位 ⇒ 窗口 **+0.1455 ms**。⇒ `TryGetCell` 内的 `GetUnitCountInCell`（O(N)）**每格都付** ⇒ `QueryCells` 成本 ＝ **O(格数 × N)**。

**⑤⭐ 可接受性结论 —— 不可接受（按调用频次限定）**

| 场景 | 读数 | 判 |
|---|---|---|
| `QueryCells` 2401 格 · **单次**（N=23） | 7.46 ms（45% 帧预算） | 偶发调用可容忍 |
| `QueryCells` 2401 格 · **每帧**（N=23） | 7.46 ms / 帧 | ⛔ **不可接受**（近半帧预算） |
| `QueryCells` 2401 格 · **每帧**（N=500） | **76.88 ms / 帧**（463%） | ⛔ **明确不可接受** |
| `GetOccupant` 未命中（原报「O(255)」担忧） | +0.051 µs/格 | ✅ 可接受（**且原报 255 不成立，实为 15**） |
| `QueryOccupants` / `CountFeatures` | 0.22 ms / 0.011 ms | ✅ 可接受 |

⇒ ⛔ **按派工提示词「若结论是不可接受 ⇒ 停下报裁」，`R3` 项裁决请求见 §六 请裁-1。本批⛔未加任何缓存／节流／语义改动（守「不许为凑读数糊过去」）。**

---

### 判据 4 · 常规读数

| 项 | 读数 | 复现 |
|---|---|---|
| **编译** | **0 error CS**；warning **9 条，逐条核对无一指向本批 5 个改动文件**（清单见 §五） | `refresh_unity{compile:request}` ＋ `read_console{types:[error]}` |
| **确定性** | 384²·seed=21107 两次生成：`features` 逐格 ＋ `climateZones` 逐格 ＋ `kingdomSpawns` ＋ `naturalBuildings` **全一致** | `hh294_slice4_probe.log` 判据 13 [PASS] |
| **无每帧全图扫** | 空闲 90 帧内 `map.features` 变更帧数 = **0** ／ `_walkFlags` 变更帧数 = **0** | 同上 判据 13 [PASS] |
| **`AI.Core` 零触** | `_Game/**/AI.Core/**` 内 `MapGate` 命中 = **0**；`git status` 无 `AI.Core` 改动 | §一末 判据 6 片段 [D] |
| **改动文件行尾** | 本批 5 个文件**全 LF**、无 BOM（`Slice3Probe 584 LF`／`HH302 553 LF`／`MapGenRules 1437 LF`／`MapGate 402 LF`／`BuildController 415 LF`） | §一末片段 [M] |
| **正门三态** | 探针走 `TestHarnessApi.EnterTestRun`；收尾 `ExitTestRun` ＋ `QuitSmoke`（三态退 Play） | 两个探针日志首尾行 |
| **回归（片4 全判据）** | `hh294_slice4_probe.log`：**PASS=39 / FAIL=0**（与片4 交付读数逐条一致） | 探针重跑 |
| **回归（片3 探针）** | `hh294_slice3_probe.log`：**PASS=14 / FAIL=1** ⚠️ 该 FAIL **非本批引入**（A/B 归因见 §三-1） | 探针重跑 |

---

### 判据 5 · 微瑕 `M4`／`M7`／`M9`（三条全做）

**`M4`**（[BuildController.cs:294-298](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildController.cs#L294-L298)）：

```csharp
            // 【HH.294 片4补正·M4】原为**不检查返回值**的裸调用 ⇒ 声明不符／非锚点格时**静默**（无锚点、建造照常）。
            //   现显式检查并告警。⛔ 本批**不做回滚**（回滚＝建造中途销毁已 `Init`＋`StartConstructing` 的半成品，
            //   会新引入一条撤链路径，属行为变更、超「微瑕」范围）；此分支现实不可达的证据：
            //   `PlacementValidator.cs:102-108` 对 `needsNode` 走 `WorldManager.IsResourceNodeAvailable(coord, requiredNode)`
            //   （同源口径）⇒ 声明不符的落点**在放置校验即被拒**，到不了本行。告警落此只为「若真发生 ⇒ 可观测」。
            if (!MapGate.ConsumeAnchor(coord, b, ResourceNodeMapping.GetResourceNode(def.id).Value))
                Debug.LogWarning($"[BuildController] 【HH.294 片4·4-B】锚点消费被拒：def={def.id} coord={coord} "
                                 + $"声明={ResourceNodeMapping.GetResourceNode(def.id).Value} "
                                 + $"实际地表={MapGate.GetFeatureAt(coord)} ⇒ 该建筑**未消费锚点**（不返还）。");
```
> ⚠️ **显式声明**：本批取「**检查 ＋ 告警**」，**❌ 未做回滚**（理由见上注释；回滚属行为变更、超微瑕范围）。

**`M7`**（[MapGate.cs:211-213](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGate.cs#L211-L213)）：

```csharp
                // 【HH.294 片4补正·M7】原为裸读 `map.features[y * map.width + x]`（**无** `i < Length` 保护，
                //   与同文件 `ReadAt:54`／`GetFeatureAt:69`／`TryGetCell:135` 口径不一）⇒ 改走本类 `ReadAt`（含界保护与长度保护）。
                if (ReadAt(map, x, y) == f) n++;
```
⇒ 口径不一已消除，且**顺带减去生产码 1 处裸读**（25→20 对照中的门内真读 7→6）。

**`M9`**（[MapGate.cs:374-382](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/MapGate.cs#L374-L382)）：

```csharp
        // 【HH.294 片4补正·M9】原序为「先清引用（`SetConsumedAnchor(null,…)`）**后**判定 `IsResourceNodeFeature(f)`」
        //   ⇒ 若判定为假则**引用已清、锚点未返还**（静默丢锚点）。现改为**判定在前**：
        //   ① 判定不通过 ⇒ 直接返回，引用原样保留（不存在「引用没了、锚点也没还」）；
        //   ② 判定通过 ⇒ 已决定返还，引用必清（防「返还后的悬垂引用」再次触发写回）；
        //   ③ 返回值＝**实际是否发生写回**（该格若已是同值 ⇒ 增门幂等返 false，引用仍清）。
        if (!IsResourceNodeFeature(f)) return false;
        bool wrote = PlaceResourceNode(coord, f);          // 锚点返还**走增门**
        consumer.SetConsumedAnchor(null, FeatureType.Plain);
        return wrote;
```
⇒ 静默失效面（引用已清 / 锚点未还）已消除；回归：片4 探针 §4-E 段 ①~⑤ 全 [PASS]（含「返还后可再被消费」的存在性反证）。

---

### 判据 6 · ⭐「清零类」断言的复现命令**输出片段**（红线⑥）

**[A] 全库 `* div` 命中（23 行 / 7 文件）**

```powershell
Get-ChildItem -Recurse -Path . -Filter *.cs -File | Where-Object { $_.FullName -notmatch '\\(Library|obj|Temp|\.git)\\' } | Select-String -Pattern '\* div' | ForEach-Object { "$($_.Path.Replace((Get-Location).Path + '\','')):$($_.LineNumber): $($_.Line.Trim())" }
```
输出片段（首尾各截；**判据 1 的逐处定性即基于此全量输出**）：
```
Valley Rampart\Assets\Editor\Smoke\Smoke_2_22P0.cs:431: int maxR = 8; int bandDenom = maxR * div;
Valley Rampart\Assets\Editor\Smoke\Valley_HH144_Probe.cs:225: int bandDenom = maxR * div;
Valley Rampart\Assets\Editor\Smoke\Valley_HH294_Slice3Probe.cs:192: WalkFlags first = wf[(cy * div) * SW + cx * div];
Valley Rampart\Assets\Editor\Smoke\Valley_HH294_Slice3Probe.cs:196: if (wf[(cy * div + sy) * SW + cx * div + sx] != first) { same = false; break; }
Valley Rampart\Assets\Editor\Smoke\Valley_HH294_Slice3Probe.cs:366: var sub = grid.CellToSub(new GridCoord(b.coord.x + dx, b.coord.y + dy), sx, sy);
Valley Rampart\Assets\Editor\Smoke\Valley_HH294_Slice4Probe.cs:118: var expand = new GridCoord(cx * div + sx, cy * div + sy);   // 就地展开（被禁的反例式）
Valley Rampart\Assets\Editor\Smoke\Valley_HH302_Slice4FixProbe.cs:98: var expand = new GridCoord(cx * div + sx, cy * div + sy);
Valley Rampart\Assets\_Game\Systems\AI\KingdomBrain\PlacementScorer.cs:86: int f1Denom = maxR * div;
Valley Rampart\Assets\_Game\Systems\Grid\GridSystem.cs:66: _sw = w * div; _sh = h * div;
Valley Rampart\Assets\_Game\Systems\Grid\GridSystem.cs:87: int baseIdx = (y * div) * _sw + x * div;
Valley Rampart\Assets\_Game\Systems\Grid\GridSystem.cs:250: return new GridCoord(cell.x * div + sx, cell.y * div + sy, cell.layer);
```
> ⚠️ **口径声明**：`:366` 那行**含** `div` 但命中的是注释里的 `* div` 字样（其代码体已走 `CellToSub`）——本行出现在片段中即证明「**该断言已按其原文跑过**」，不修饰。

**[B] 生产码 `.features[` 逐文件计数（改后）**

```
1  Valley Rampart\Assets\_Game\Systems\Rendering\MapRenderService.cs      ← 注释行
11 Valley Rampart\Assets\_Game\Systems\World\MapGate.cs
14 Valley Rampart\Assets\_Game\Systems\World\MapGenRules.cs
1  Valley Rampart\Assets\_Game\Systems\World\ResourceRespawnSystem.cs     ← 注释行
1  Valley Rampart\Assets\_Game\Systems\World\WorldManager.cs              ← 注释行
```

**[J'] `HEAD` 版 `MapGate.cs` 逐行（改前，用于 8→7 真读对照）**

```
HEAD:…/MapGate.cs:33:  private static void WriteRaw(MapData map, int index, FeatureType f) => map.features[index] = f;
HEAD:…/MapGate.cs:54:  return i >= 0 && i < map.features.Length ? map.features[i] : FeatureType.Plain;
HEAD:…/MapGate.cs:69:  return i >= 0 && i < map.features.Length ? map.features[i] : FeatureType.Plain;
HEAD:…/MapGate.cs:135: info.feature = map.features != null && i < map.features.Length ? map.features[i] : FeatureType.Plain;
HEAD:…/MapGate.cs:211: if (map.features[y * map.width + x] == f) n++;        ← M7 已改（本批）
HEAD:…/MapGate.cs:233: if (map.features[i] == f) return false;
HEAD:…/MapGate.cs:255: var old = map.features[i];
HEAD:…/MapGate.cs:293: var f = map.features[i];
（另 3 行 :18/:27/:48 为注释）
```

**[D] `AI.Core` 命中**

```
===== [D] AI.Core 命中 =====
0
```

**[M] 行尾**

```
Valley_HH294_Slice3Probe.cs    CRLF=0  LF-only=584  BOM=False  bytes=32264
Valley_HH302_Slice4FixProbe.cs CRLF=0  LF-only=553  BOM=False  bytes=28946
MapGenRules.cs                 CRLF=0  LF-only=1437 BOM=False  bytes=75933
MapGate.cs                     CRLF=0  LF-only=402  BOM=False  bytes=24274
BuildController.cs             CRLF=0  LF-only=415  BOM=False  bytes=20282
```

---

## 二、本批改动文件清单

| # | 文件 | 改动 |
|---|---|---|
| 1 | `Assets/Editor/Smoke/Valley_HH294_Slice3Probe.cs` | `R1`：`:359→366` 就地展开改走 `CellToSub`；`:186` 起补**豁免声明**（数组索引算式，含 `SW` 说明） |
| 2 | `Assets/_Game/Systems/World/MapGenRules.cs` | `R2`：4 处裸读改走 `MapGate.ReadAt`（`:119`／`:130`／`:1275`／`:1284`） |
| 3 | `Assets/_Game/Systems/World/MapGate.cs` | `M7`（`CountFeatures` 改走本类 `ReadAt`）＋ `M9`（`ReturnAnchor` 判定前移） |
| 4 | `Assets/_Game/Systems/Building/BuildController.cs` | `M4`（`ConsumeAnchor` 返回值检查 ＋ 告警） |
| 5 | `Assets/Editor/Smoke/Valley_HH302_Slice4FixProbe.cs` | **新建**：本批探针（`R1` 等价 ／ `R2` 等价与行为 ／ `R3` 四组耗时） |
| 6 | `Valley Rampart/Logs/hh302_slice4fix_probe.log` | 本批原始读数（Logs 在 gitignore 域·见 §八） |

**行尾**：以上 5 个 `.cs` **全 LF、无 BOM**（§一末片段 [M]）。

---

## 三、未完成项（显式列出）

| # | 项 | 状态 | 说明 |
|---|---|---|---|
| 1 | `hh294_slice3_probe.log` **1 项 FAIL**（「在网格有登记的 60 座 ⇒ 全覆盖 = **52/60**」） | **未修**（**非本批引入**，已 A/B 归因） | 片3 探针该断言写于「AI 预置建筑**零登记**」时期（当时 `registered ≈ 36`，断言 `fullHits == registered` 成立）；片4 的 `RebuildOccupancyFromRegistry` 把 119… 全量登记后，前 60 座里有 **8 座 footprint 相互重叠**（`片4 探针判据 10` 已载「全覆盖 11883／部分 **9**」）⇒ 断言口径**过时**。**A/B 实测归因**：把 `:359` 临时还原为改前「就地展开」式重跑，得**同一读数**（有登记 60／全覆盖 52／部分 8／子格命中 2048/2240／FAIL=1）⇒ **`R1` 未引入**。⚠️ 属**片3 探针判据口径问题**（应限定在「无重叠 footprint 子集」或改为只报读数不设 PASS/FAIL）⇒ **超本批范围，未改，列报**（见 §六 请裁-3）。 |
| 2 | `R3` 的**成本治本**（`GetUnitCountInCell` O(N) ⇒ O(1)） | **未做** | 提示词明令「若不可接受 ⇒ **停下报裁**」＋「不许为凑读数自行加缓存／节流／改语义糊过去」⇒ 只给读数与候选方案（§六 请裁-1），**未动一行优化代码**。 |
| 3 | Editor 探针面裸读（**12 文件**）与写点（2 文件） | **未做** | 本批范围外（与片4 同口径，单列于 §一 判据 2 表）。 |
| 4 | `MapGenRules` 3 处**已裁留**裸读（`ReadPitStats`／`MatchesPreferredFeature`／`ChunkFeatureRatio`） | **按裁保留·未动** | `D771` §四 裁「可留／Editor-only 留」；红线① 明令不碰。 |
| 5 | `M1`／`M2`／`M3`／`M5`／`M6`／`M8` 六条微瑕 | **未做** | 提示词只点 `M4`／`M7`／`M9`「建议随手收」；其余六条**未在授权范围**⇒ 显式声明**未做**。（其中 `M5` 死参数、`M6` `PlaceResourceNode` 无「位置空闲」校验、`M8` 冗余计算 3 项，均可在下一批或片5 一并处理。） |
| 6 | 本批**未**跑任何长局（`R3` 单位数上限用夹具构造） | **未做** | `R3` 要的是「三档 N 读数」，用 `AIDebugSpawnController` 真生成单位构造；长局（自然增长到 N 峰值）未跑 ⇒ 「N 的真实上限」未知，列报。 |

---

## 四、勘正记录

| # | 来源说法 | 实况（实读） | 处置 |
|---|---|---|---|
| 1 | `HH.294_片4_验收报告` §二 `R2` ＋ 派工提示词：`ZoneOf` :118／:128 **运行期可达**（← `BuildingVisual:119`／`MapRenderService:464`／`MapGenDebugDrawer:49`） | ⛔ **行号与函数归属皆错**：`ZoneOf` 在 **`:104-105`**，实现体只读 **`climateZones`**（**不读 `features`**）⇒ 那 3 个调用者与 features 裸读**无关**。`:118`＝**`HasFullBlock`**（`private static`，`:113`）、`:128`＝**`IsClearBlock`**（`private static`，`:124`）。二者调用面**全在生成期**：`HasFullBlock` ← `InFullBlock`(`:146`) ← `PruneOrphanMineCells`(`:1325`←`PlaceWater`) ／ 直接 ← `ClearKingdomZones`(`:1019`)；`IsClearBlock` ← `TryStampMineCluster`(`:929`) ← `PlaceResourceQuota`(`:848/:879`)／`EnsureChunkResourceQuota`(`:1083`) | **仍按提示词把 4 处全改**（不改则能力句更弱）；但**归属与可达性定性已在 §一 判据 2 勘正**。⚠️ 「运行期可达 4 处」实际为 **2 处**（`NearestWalkable`×2） |
| 2 | 提示词／验收报告 `R3`：「`GetOccupant` 慢路径扫同块余 **div²−1 = 255**」 | ⛔ **实盘 `div = SubDiv = 4`**（`GridSystem.cs:43`，缺省 4；独立佐证：片3 实测子格域 `SW×SH = 1536² = 2,359,296 = (384×4)²`）⇒ 真实扫描量 **15**，为所述 255 的 **1/17** | 已按 **15** 重报代价；该路径**非**主要成本项（+0.051 µs/格） |
| 3 | 提示词给定能力句：「除门自身与**五个纯生成期函数**外，生产码无任何代码直接读 `map.features[]`」 | 该字面式**与红线①不相容**：`ReadPitStats`／`MatchesPreferredFeature`／`ChunkFeatureRatio` **不在**这 5 个函数内，但被裁「保留裸读」⇒ 字面式必假 | 能力句**补 3 例外**（§一 判据 2 ⑤ 原文）＋ **列报冲突**（§六 请裁-2） |
| 4 | 片4 交付报告 §一 判据 12「`* div` 改后 0 命中」 | 现为 **23 行 / 7 文件**命中（其中**生产码真·坐标就地展开 = 0**） | 本报告改为**实测读数 ＋ 逐处定性**（§一 判据 1 ③ ＋ 片段 [A]），不再使用「0 命中」字样 |
| 5 | 片4 交付报告 §三-1「Editor 探针面裸读 55 处 / 10 文件」 | 本批实测为 **12 文件**（`HH302` 新增并含 2 处注释性字样） | 以本批实读为准（§一 判据 2 ④ 表） |

---

## 五、编译 warning 清单（9 条，逐条核对＝本批前既存）

```
Valley2_17_Smoke_5.cs(91,85)   CS0162 / ArtImportPipeline.cs(267,9)  CS0618
Valley2_21A_Smoke.cs(202,13)   CS0162 / Valley2_17_Smoke_P0.cs(225,13) CS0219
Valley_HH264_AnimProbe.cs(182,17) CS0219 / Valley_HH128_Probe.cs(75,14) CS0219
Valley_HH80_Run.cs(301,13)     CS0162 / Valley2_20_Smoke_Race.cs(701,25) CS0472
Valley2_20_Smoke_Race.cs(875,13) CS0162
```

**核对口径**：9 条**无一**指向本批 5 个改动文件（`Valley_HH294_Slice3Probe.cs`／`Valley_HH302_Slice4FixProbe.cs`／`MapGenRules.cs`／`MapGate.cs`／`BuildController.cs`）⇒ **0 新增 warning**；`read_console{types:[error]}` 仅 1 条非编译错误日志（`240 node options failed to load and were skipped.`，既存）。
> ⚠️ 与片4 报告「25 条」的差：控制台在 `refresh_unity` 时被清空，仅重新编译的程序集日志留存 ⇒ **本批按本次实测 9 条为准**（口径已声明）。

---

## 六、请示裁决（3 项）

### 请裁-1 ⭐ · `R3` 结论＝**不可接受** —— 是否立治本批？

**读数**：`TryGetCell` 每格恒调 `GetUnitCountInCell`（`foreach (_unitSubCells)` ⇒ **O(N)**）⇒ `QueryCells` ＝ **O(格数 × N)**；2401 格窗口：N=23 ⇒ **7.46 ms**（45% 帧预算）／N=500 ⇒ **76.88 ms**（**463%**）。对照：同窗口**纯逐格 `ReadAt`** 仅 **0.0112 ms**（**99.85% 成本来自 6 项组包**）。

**候选方案（只列，未实施）**：
- **甲·按需取事实**：把 `unitCount`／`ownerKingdomId` 从 `TryGetCell` 的**恒算集**拆出（改「格子属性」与「格上存在」两段，`CellFilter` 只声明需要的事实）⇒ 可去 99% 成本，但**改 `03` §8.2 的复合结构语义**。
- **乙·块级单位计数**：`GridSystem` 维护「地块 → 单位数」稀疏计数（`TryEnter`/`ExitCurrentCell` 处 ±1）⇒ `GetUnitCountInCell` 回 O(1)。**正确性读数 ＋ 代价读数两栏**（`L-37`）须同步给：跨格搬迁路径（`TryEnter` 改写 `_unitSubCells[unit]` 时旧格 −1）易漏。
- **丙·不改，限定调用面**：`QueryCells` 只允许**偶发**调用（禁进 `Update`），并在文档/注释写明「N 大时 O(格数×N)」。成本 0，但**不解决**大 N 下每帧不可用。

**⚠️ 口径不明，停在此处报裁，未按理解实现**（提示词原文：「若结论是不可接受 ⇒ 停下报裁」）。

### 请裁-2 · 能力句的 3 处「已裁留」例外是否照准？

`D771` 裁「`ReadPitStats`／`MatchesPreferredFeature`／`ChunkFeatureRatio` 可留」↔ 提示词要求的能力句字面「只剩 5 个纯生成期函数」——**二者不可同时为真**。本批按「**保留 3 处 ＋ 能力句补 3 例外**」（§一 判据 2 ⑤）执行。**若要求能力句严格照字面 ⇒ 须授权改这 3 处（`MatchesPreferredFeature`/`ChunkFeatureRatio` 属生成期传递、改门有微小代价但可行；`ReadPitStats` 为 Editor-only 调用面）**，请裁。

### 请裁-3 · 片3 探针的过时断言（`52/60` FAIL）如何处置？

**A/B 已证非本批引入**（§三-1）。三个候选：**甲**改断言口径（限定「footprint 无重叠」子集）；**乙**改为只报读数、不设 PASS/FAIL（`L-37` 家族口径）；**丙**不动，仅登记为既存残余。本批**未改**（超范围）。

---

## 七、回退预案

按 `底层执行计划` §五：本批为**同轨替换 ＋ 两处微瑕收口**（无新旧双写）。
- `MapGenRules` 4 处：还原为裸读即回退（`MapGate.ReadAt` 与裸读逐格等价 ⇒ 无状态遗留）。
- `MapGate` `M7`／`M9`、`BuildController` `M4`：局部还原即可。
- 探针 `HH302` 为**新增文件**，删除即回退（不影响生产）。
- 首选：`git revert <本批 commit>`（单一 commit）。

---

## 八、证据索引

- **本批探针日志**：`Valley Rampart/Logs/hh302_slice4fix_probe.log`（**PASS=10 / FAIL=0**）
- **回归日志**：`Valley Rampart/Logs/hh294_slice4_probe.log`（39/0）／`Valley Rampart/Logs/hh294_slice3_probe.log`（14/1，FAIL 已归因）
- **grep 复现命令与输出片段**：§一 判据 6（[A]~[M]）
- **本报告行尾**：LF
- ⚠️ `Logs/` 在 `.gitignore` 域 ⇒ 日志**不入 git**（与片3/片4 同口径），只作现场证据；如需入档请裁。

---

## 附 · 应登记项（按 `D767` 纪律：执行端**不代写**策划端账本，仅声明）

1. **编号**：`HH.302` ＝ 本报告（水位线 `HH.301 → HH.302`）。
2. **批次状态**：`HH.294` 片 4 补正小批 ＝ **执行端已交付**，待策划端验收；`R3` 请裁-1 未决。
3. **教训候选**：`L-02` 家族第 4 次命中（`* div` 断言未跑即写）；`L-30` 家族（「限定期函数」类能力句须核调用面**归属**——本次查出验收报告把 `HasFullBlock`/`IsClearBlock` 误记为 `ZoneOf`）；新增维度候选：**「代价读数须先核前提常数」**（`div²−1=255` 实为 `15`）。
