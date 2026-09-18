# HH.312 · `F-15` 收尾小批 · 交付报告

> 执行端｜2026-09-18｜批次＝**`HH.294` 批外挂账「`F-15` 收尾小批」**（单列微批·**免开工回执**）
> 任务源＝**`HH.311` 报告 §八 残余 `R1`**（原文：「`LegacyUnconditionalRemove` 仍在生产码内（默认 `false`）⇒ 验收后随收尾批删除」；回退面＝ commit 历史 `0b817af1`）
> 唯一口径源＝`最高优先级文档/03_地图即数据库.md` §6.5（**只读遵守·契约一行未改** —— ⛔ 契约勘正归策划端，本报告只给 `F15-F` 行号读数）
> 先例＝**`HH.308`** 片 6-2 收尾批（旧开关 `SpawnResourceEntities` 同法：生产码删干净 ＋ 探针「对照段删·本体保留」）
> 施工 commit＝**`e1e77a1f`**（2 文件 `+33/−94`：`BuildingRegistry.cs` `+2/−7` ＋ 探针 `+31/−87`）
> 证据＝`Valley Rampart/Logs/hh310_f15/hh310_probe.txt`（稳定名·末次跑 2026-09-18 14:40:40）＋ `hh310_probe_20260918_144041.txt`（时间戳副本）＋ `hh310_hash.txt`
> ⚠️ **未 push**；四档账本**零 diff**（应登记项见 §末）

---

## 〇、取向执行确认（已裁·照做）

任务书取向＝**删开关 ＋ 探针摘段（保留探针本体）**。**未触发报裁**：实测摘段共牵动 **14 处**，全部为**行级删除／单段化改写**，无一处需重写逻辑 ⇒ 成本低于整删价值（§A/§B/§D 实机回归面完整保留）。⛔ 未整删探针（`.cs`/`.meta` 均在场）；⛔ 未「保留开关只加注释」。

## 一、施工面（逐处 `file:line`·改后行号）

### `F15-D` 生产码删开关 —— [BuildingRegistry.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingRegistry.cs)

| 位置（改前） | 位置（改后） | 内容 |
|---|---|---|
| `:25-28`（4 行） | **已删** | `【F-15 修法小批·F15-C】A/B 对照开关` 注释块 3 行 ＋ `public static bool LegacyUnconditionalRemove = false;` 字段行 |
| `:43` | **已删** | `if (LegacyUnconditionalRemove) { _byCoord.Remove(k); continue; }   // 改前语义（A 列对照）` |
| `:33` | `:29-30`（拆两行） | `F15-A` 注释末尾补勘正句：「【`HH.312` 收尾批】A/B 对照开关已于本批删除；改前读数落盘 `HH.311` 报告／`Logs/hh310_f15/`。」（中文·⛔ **不含标识符**） |

**改后 `Unregister` 全体（逐字）**：

```csharp
    /// <summary>注销建筑（按 b.coord + b.footprint 清除全部覆盖格）。
    /// 【`F-15` 修法小批·`F15-A`】**加归属判定**：仅当该格当前指向**自己**时才删 —— 改前为**无条件** `Remove`，
    /// 会把**后注册者**在该格的反查项一并抹掉（后写者"查不到"）。⛔ 未动 `_all.Remove(b)`（只删自己·已正确）
    /// 与 `Register`／`RegisterFootprintCells`（**后写者胜保持为现行语义**）。
    /// 【`HH.312` 收尾批】A/B 对照开关已于本批删除；改前读数落盘 `HH.311` 报告／`Logs/hh310_f15/`。</summary>
    public void Unregister(Building b)
    {
        if (b == null) return;
        _all.Remove(b);
        int w = Mathf.Max(1, b.footprint.x), h = Mathf.Max(1, b.footprint.y);
        for (int dy = 0; dy < h; dy++)
            for (int dx = 0; dx < w; dx++)
            {
                var k = new GridCoord(b.coord.x + dx, b.coord.y + dy);
                if (_byCoord.TryGetValue(k, out var cur) && cur == b) _byCoord.Remove(k);
            }
    }
```

> ⭐ **归属判定行逐字节不变**（判据 2 给二进制比对）：改前 `:44` 与改后 `:39` 两行 `.encode()` **相等**。

### `F15-E` 探针摘开关 —— [Valley_HH310_F15Probe.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH310_F15Probe.cs)

| 原行号（建立时） | 处置 | 改后行号 |
|---|---|---|
| `:10-11` 口径源 | ＋ 一行「`HH.312` 收尾批（删 A/B 对照开关 ⇒ §C 段删·§D 单段·§F 单列）」 | `:13` |
| `:17-24` 判据面列表 | §C 行改「**已随 A/B 对照开关删除**」／§D 行「OFF/ON 两段」→「单段」／§F 行「改前/改后两列」→「单列」 | `:18-24` |
| `:52` 日志拼开关值 | 去掉该段，改打「归属判定＝唯一路径（A/B 对照开关已删·`HH.312` 收尾批）」 | `:52-53` |
| `:131` `Case_A_Literal` 开关行 | **删该行** | — |
| `:168` `Case_A_Offset` 段 1 开关行 | **删该行**；段名「段 1：OFF（新语义）」→「单段：现行语义（归属判定）」；文案「§A-② 段 OFF（新语义）：」→「§A-② 」 | `:168-171` |
| `:189-212` **§C 整段** | ⭐ **整段删**（段 2 ON ＋ 鉴别力结论 ＋ 复原）；原位留一行勘正注 | `:189` |
| `:222` `Case_B_Single` 开关行 | **删该行** | — |
| `:246` `Case_D` 开关行 | **删该行** | — |
| `:251-252` 两次 `RunOneCycle(..., false/true, ...)` | ⇒ **改单段**：`RunOneCycle(reg, def, freeSpot, s0, out var s1, out int d)`（`onCleared`/`s1on`/`dOn` 未使用变量已一并清） | `:226` |
| `:254-266` 两段文案 ＋ S1_OFF/S1_ON 互比 | 改**单段**文案；原「缺陷鉴别力由 §C 承担」**已改写**为：§C 已随开关删除 ⇒ 本批**无对照面** ⇒ ⛔ 不得据此升格声称缺陷判据 | `:229-233` |
| `:270-287` `RunOneCycle(..., bool legacy, ...)` | **去 `legacy` 形参**；删 `:273` / `:285` 两行赋值 | `:237-253` |
| `:325-335` `§F` **A 列（改前语义 ON）** | **整列删**；原位留一行勘正注 | `:300` |
| `:337` `// B 列：改后语义（OFF）` | 改 `// 单列：现行语义` | `:289` |
| `:346-350` 「两列**同一语义**」论述 | 改写为**单列**：`⇒ Unregister 不触 GridSystem（双写各自独立）⇒ 03 §6.5 ④ 已知限制照旧登记·不计 FAIL`；⛔ 已不再声称「两列比对」 | `:301-302` |
| `:362` `Case_F` 末尾开关行 | **删该行** | — |
| `:531` `Finish()` 复原行 | **删该行**（开关已不存在，残留防护无对象） | — |
| `:383-388` `CountEqual` 工具（仅 §C 用） | **删该函数**（摘段后成死码） | — |

> ⚠️ **`CountEqual` 为清单外补充项**（任务表未列）：其**唯一**调用点在 §C 段（`:196`/`:199`）⇒ §C 整段删后成为未使用私有方法。删它属「摘干净」必要动作。**如实列报**（§五 偏离 ①）。
> 探针**本体保留**：`§A`（构①／构②）／`§B`／`§D`／`§E`／`§F`／`VerifyCleanup`／`Make`／`Kill`／`Snapshot`／`FindFreeBlock`／`HashMap`／`WriteFile` 全在（实机归属判定回归面完整）。

---

## 二、判据 1 —— ⭐ 能力句「`LegacyUnconditionalRemove` 已不存在」＋ 全库零命中

**作用域显式**（`Assets`／`Packages`／`AI.Core`／Editor 全覆盖）：

```
$ git grep -n "LegacyUnconditionalRemove" -- "Valley Rampart/Assets"
（无输出 ⇒ 0 命中）

$ git grep -n "LegacyUnconditionalRemove" -- "Valley Rampart/Packages"
（无输出 ⇒ 0 命中）

$ git grep -n "LegacyUnconditionalRemove" -- "*AI.Core*"
（无输出 ⇒ 0 命中）

$ git grep -n "LegacyUnconditionalRemove" -- .
（仅 3 行 ⇒ 全为**历史归档文书**，非代码面：
  多Agent交接/执行端/HH.310_…_开工回执.md:92
  多Agent交接/执行端/HH.311_…_交付报告.md:19
  多Agent交接/执行端/HH.311_…_交付报告.md:173）
```

**逐文件命中计数表（改前 vs 改后）**：

| 文件 | 改前 | 改后 |
|---|---|---|
| `Valley Rampart/Assets/_Game/Systems/Building/BuildingRegistry.cs` | **2**（`:28` 字段 ＋ `:43` 分支） | **0** ✅ |
| `Valley Rampart/Assets/Editor/Smoke/Valley_HH310_F15Probe.cs` | **14**（`:52/131/168/191/192/212/222/246/273/285/326/338/362/531`） | **0** ✅ |
| 其余代码面（全库 `Assets`＋`Packages`＋`AI.Core`） | 0 | **0** ✅ |
| 合计（代码面） | **16** | **0** ✅ |

> ⚠️ **注释内不含标识符**：`F15-D` 补句写「A/B 对照开关已于本批删除」，探针勘正注写「已随 A/B 开关删除」—— 均**中文描述**，**零标识符**（故 `git grep` 全库代码面 0 命中如实成立）。

## 三、判据 2 —— 归属判定唯一路径（`_byCoord.Remove` 生产码命中 = 1）

```
$ git grep -n "_byCoord.Remove" -- "Valley Rampart/Assets/_Game"
Valley Rampart/Assets/_Game/Systems/Building/BuildingRegistry.cs:39:                if (_byCoord.TryGetValue(k, out var cur) && cur == b) _byCoord.Remove(k);
```

**改后原文逐字**（`:39`）：

```csharp
                if (_byCoord.TryGetValue(k, out var cur) && cur == b) _byCoord.Remove(k);
```

**逐字节未变自证**（`git show 6be5a057:<path>` 改前 vs 工作副本改后）：

```
OLD hits= 2
  OLD|                if (LegacyUnconditionalRemove) { _byCoord.Remove(k); continue; }   // 改前语义（A 列对照）|
  OLD|                if (_byCoord.TryGetValue(k, out var cur) && cur == b) _byCoord.Remove(k);|
NEW hits= 1
  NEW|                if (_byCoord.TryGetValue(k, out var cur) && cur == b) _byCoord.Remove(k);|
判定行逐字节相同 = True
bytes equal = True
```

## 四、判据 3 —— ⭐ 探针编译 0 error（本批最大风险点）

`Assets` 侧 14 处引用全摘后编译：

```json
{ "status": "completed", "pipeline_kind": "refresh",
  "console": { "entries": [], "truncated": false },
  "errors": 0, "hasErrors": false, "warnings": 0, "hasWarnings": false }
```

> 首轮 refresh（改前基线取数）＝ `0 error / 25 warnings`（见判据 7）；本轮 0 error。⚠️ 若 14 处有任一漏摘 ⇒ 必报 `CS0117`（成员不存在）⇒ **如实为 0**。

## 五、判据 4 —— ⭐ 功能不回归（探针重跑·改后）

**证据**：`Valley Rampart/Logs/hh310_f15/hh310_probe.txt`（末次跑 2026-09-18 14:40:40）＋ 时间戳副本 `hh310_probe_20260918_144041.txt`。

**入口**（`§E`）：
```
§E feat+climate=9424A5D99D9C3543 vs 基线 9424A5D99D9C3543 ⇒ 逐值不变 ✅
§E feat+climate+spawns=0A78DF92C7F30B81 vs 基线 0A78DF92C7F30B81 ⇒ 逐值不变 ✅
§E 对照：BuildingRegistry 总数=21（片 6-2/6-3 读数 21 ⇒ 逐值不变 ✅）
```

**§A 构①（字面：同 coord 同 footprint）**：
```
§A-① 建后登记事实（后写者胜）：(251,251)=B#-14534, (252,251)=B#-14534, (251,252)=B#-14534, (252,252)=B#-14534 ⇒ 全格 == B ✅ 后注册者胜
§A-①(a) GetAt(A 独占格)：**无 A 独占格**（同 coord 同 footprint ⇒ 全 4 格共享）⇒ 该列在本构造下不存在（如实列报·见构②）
§A-①(b) GetAt(共享格)=… （4 格全 B#-14534） ⇒ == B ✅
§A-①(c) GetAt(B 独占格)：**无 B 独占格**（同上）⇒ 该列在本构造下不存在（如实列报·见构②）
§A-① 旁注：A ∈ All=False（`_all.Remove(b)` 只删自己·未动）｜B ∈ All=True
§A-①(d) Unregister(B) 后 GetAt(共享格)=…（4 格全 null） ⇒ == null ✅（B 是最后写入者 ⇒ 应被清）
```
> ⚠️ `1(a)`/`1(c)` 在构① 下**结构性不存在**（同 coord 同 footprint ⇒ 全格皆共享）—— 属 `HH.310` 已如实列报的构造法口径限制，**非本批回归**。

**§A 构②（错位 1 格·三格类齐）**：
```
§A-② 建后：A 独占=[(251,251)=B#-14542, (251,252)=B#-14542]｜共享=[(252,251)=B#-14550, (252,252)=B#-14550]｜B 独占=[(253,251)=B#-14550, (253,252)=B#-14550] ⇒ ✅ 与「后写者胜」自洽
§A-②(a) Unregister(A) 后 GetAt(A 独占格)=[(251,251)=null, (251,252)=null] ⇒ 必须 == null ⇒ ✅
§A-②(b) GetAt(共享格)=[(252,251)=B#-14550, (252,252)=B#-14550] ⇒ 必须 == B ⇒ ✅ == B
§A-②(c) GetAt(B 独占格)=[(253,251)=B#-14550, (253,252)=B#-14550] ⇒ == B ✅
§A-②(d) 继 Unregister(B) 后 GetAt(共享格)=[(252,251)=null, (252,252)=null] ⇒ 必须 == null ⇒ ✅（B 是最后写入者）
```

**§B 判据2（无重叠·单座）**：
```
§B 建后：[(251,253)=B#-14558, (252,253)=B#-14558, (251,254)=B#-14558, (252,254)=B#-14558] ⇒ == 该座 ✅
§B Unregister 后：[(251,253)=null, (252,253)=null, (251,254)=null, (252,254)=null] ⇒ 全格 == null ✅（零行为变更）
```

**§D 判据4（单段）**：
```
§D S0（基线·在册 21 座）：条目=95｜格数=95｜指向本座=84｜指向他座=11｜空格=0
§D 单段（现行语义）：测试座注销后其 footprint 全格 == null ✅｜在册 21 座签名 diff(S0,S1)=0 ⇒ 0 ✅ 无附带清除
```
> 两段互比 diff **已随 §C／§D 单段化删除**（原「S1_OFF vs S1_ON」无对照面）—— **如实列报**（§五 偏离 ②）。

**§F 判据7（单列）**：
```
§F 建后（两座重叠·后写者胜）：GetOccupant(共享格)=occ:Building#-14574｜GetOccupant(A 独占格)=occ:Building#-14566
§F **单列（现行语义）**：Unregister(A) 后 GetOccupant(共享格)=occ:Building#-14574
§F ⇒ Unregister 不触 GridSystem（双写、各自独立） ⇒ 本批未改该面（`03` §6.5 ④「已知限制」照旧登记·不计 FAIL）
§F 已知限制实证（临时·随后复原）：FreeFootprint(A footprint) ⇒ GetOccupant(共享格)=null ⇒ **静默清掉后注册者的占格** ⇒ 坐实「已知限制·不可恢复」
§F 已复原：GetOccupant(共享格)=occ:Building#-14574
```

**收尾自检（两区）**：
```
3×3 区 构造区 5×5 邻域残留：登记表命中=0｜GridSystem 占格命中=0 ⇒ 0 / 0 ✅ 无残留
3×3 区 BuildingRegistry 总数（收尾）=21
2×2 区 构造区 5×5 邻域残留：登记表命中=0｜GridSystem 占格命中=0 ⇒ 0 / 0 ✅ 无残留
2×2 区 BuildingRegistry 总数（收尾）=21
```

## 六、判据 5 —— ⭐ 零地图变更（基线取 `6be5a057`·⛔ 未用 `HEAD`）

⚠️ 本批一提交 `HEAD` 即本批自己 ⇒ 基线**显式取 `6be5a057`**（验收销号批＝本批父提交），取数走 `git show <rev>:<path>`（⛔ 未用 `git checkout`／`restore`）：

```
$ git show 6be5a057:"Valley Rampart/Assets/_Game/Systems/Building/BuildingRegistry.cs" | …
HEAD blob bytes= 4507 LF= 89
```

**同 seed 双 hash（探针 `§E`·探针于建局即取 ⇒ 免受后续 tick 影响）**：

| 口径 | 读数 | 基线 | 判定 |
|---|---|---|---|
| `feat+climate` | `9424A5D99D9C3543` | `9424A5D99D9C3543` | **逐值不变 ✅** |
| `feat+climate+spawns` | `0A78DF92C7F30B81` | `0A78DF92C7F30B81` | **逐值不变 ✅** |

落盘：`Valley Rampart/Logs/hh310_f15/hh310_hash.txt` ⇒ `feat=9424A5D99D9C3543 all=0A78DF92C7F30B81 seed=29418 at 2026-09-18 14:40:41`

> 本批改动面**全在运行时反查登记逻辑 ＋ Editor 探针** ⇒ 生成期（`MapGenRules`／落点器）**零触** ⇒ hash 不变为**结构性预期**，此读数作**反证**用。

## 七、判据 6 —— ⭐ 行尾（本批头号陷阱）

⚠️ **本文件工作副本为全 CRLF**（`core.autocrlf=true` ⇒ 索引/HEAD blob 归一为 LF）⇒ ⛔ **禁 `Edit`/`Write`**，走 **python 二进制按行替换并保留原行尾**。

| 文件 | 改前（工作副本） | 改后（工作副本） | 判定 |
|---|---|---|---|
| `BuildingRegistry.cs` | `bytes=4596 / CRLF=89 / bareLF=0 / bytes` | `bytes=4166 / CRLF=84 / bareLF=0 / bareCR=0 / BOM=False` | **裸 LF = 0 ✅**（CRLF 计数 84 ＝ 行数 84） |
| `Valley_HH310_F15Probe.cs` | `bytes=32383 / CRLF=0 / bareLF=559 / BOM=False` | `bytes=28530 / CRLF=0 / bareLF=503 / bareCR=0 / BOM=False` | **纯 LF 保持 ✅**（改前重验：今日 LF 已当场复核） |

**`git diff --numstat` 不得整文件重写**：

```
$ git diff --numstat -- BuildingRegistry.cs Valley_HH310_F15Probe.cs
31      87      Valley Rampart/Assets/Editor/Smoke/Valley_HH310_F15Probe.cs
2       7       Valley Rampart/Assets/_Game/Systems/Building/BuildingRegistry.cs
```
> ① `BuildingRegistry.cs`：`2 / 7`（**远小于** 84 行 ⇒ 行尾未坏 ✅）；② 探针：`31 / 87`（远小于 503 ⇒ 行尾未坏 ✅）。**均未触发「numstat ≈ 全文件行数」停手条件**。

## 八、判据 7 —— 常规

| 项 | 读数 |
|---|---|
| 编译 | **0 error** ✅ |
| warning | **25 = 25·不新增** ✅（⚠️ 须**全量重编译**才准：见下） |
| 本批两文件 warning | **0**（25 条全为存量·集合逐条同改前） |
| `AI.Core` | **零触** ✅（`git status --porcelain -- "*AI.Core*"` 空） |
| `numstat` 逐值 | 探针 `+31/−87`／`BuildingRegistry.cs` `+2/−7` |
| 工艺读数在终态复测 | ✅（`M3` 教训：行尾/numstat 均在末次改动**之后**复测，见判据 6/7） |

**全量重编译过程（如实列报）**：增量 refresh 首读仅 **10 条**（纯 Editor 程序集）⇒ 判为**假象**（`_Game` 侧 15 条未重编）⇒ 用临时编译触发件（`__HH312Kick.cs`·**读毕即删**，`.cs`/`.meta` 均已清场）强制 `_Game` 程序集重编 ⇒ 读得 **25 条**：`_Game` 15 ＋ `Editor` 10，与本批改前基线**同集合**（删去/新增触发件未产生任何新条目）。

> ⚠️ 触发件为**临时工艺工具**，非交付面：已删 `.cs`＋`.meta`（`git status` 现无残留）。

## 九、判据 8 / `F15-F` —— 改后行号清单（供**策划端**勘正契约 `03` §6.5 ⑥ 引用）

### `BuildingRegistry.cs`（改后·共 84 行·CRLF）

| 锚点 | 改后行号 | 契约现引（需勘正） |
|---|---|---|
| `public class BuildingRegistry` | `:9` | — |
| `public void Register` | `:18` | — |
| **`public void Unregister`** | **`:30`** | — |
| `_all.Remove(b);` | `:34` | — |
| ⭐ **归属判定行** `if (_byCoord.TryGetValue(k, out var cur) && cur == b) _byCoord.Remove(k);` | **`:39`** | 契约引 `:43-44` ⇒ **应改 `:39`** |
| `public Building GetAt` | `:44` | — |
| `public List<Building> GetInRect` | `:51` | — |
| `public void Clear` | `:64` | — |
| **`private void RegisterFootprintCells`** | **`:76`** | — |
| ⭐ **`F15-B` 勘正注释块** | **`:69-75`** | 契约引 `:75-81` ⇒ **应改 `:69-75`** |
| 无条件覆写末行 `_byCoord[...] = b;` | **`:81`** | 契约引 `:87` ⇒ **应改 `:81`** |

### `Valley_HH310_F15Probe.cs`（改后·共 503 行·纯 LF）

| 锚点 | 改后行号 |
|---|---|
| 头注口径源（含 `HH.312` 行） | `:13` |
| 判据面列表（§C 已删标注／§D 单段／§F 单列） | `:18-24` |
| `RunFromMenu`（日志头改「归属判定＝唯一路径」） | `:41`（拼串在 `:52-53`） |
| `Run()` 主流程 | `:65` |
| `Case_A_Literal` | `:128` |
| `Case_A_Offset`（含 §C 勘正注 `:189`） | `:158` |
| `Case_B_Single` | `:195` |
| `Case_D_NoDisturb`（单段） | `:219` |
| `RunOneCycle`（**已去 `legacy` 形参**） | `:237` |
| `Case_F_GridKnownLimit`（单列） | `:281` |
| `Finish()` | `:474` |
| `WriteFile()` | `:487` |

> ⛔ **契约由策划端处置** —— 本表**只给读数**，本端未改 `03` 任何一行。

---

## 十、偏离与残余（如实列报）

**偏离 5 项**（均本端主动，非任务书清单内）：

| # | 项 | 说明 | 判定 |
|---|---|---|---|
| ① | **删 `CountEqual` 工具函数** | 任务表未列；其唯一调用点在 §C（`:196`/`:199`）⇒ 摘段后成死码 ⇒ 删 | 必要动作（若留 ⇒ 未使用私有方法告警风险） |
| ② | **§D 两段互比 diff 消失** | 原 `diff(S1_OFF, S1_ON)` 随单段化删除 ⇒ 该对照面**结构性不再存在** | 属取向必然后果·**非缺陷**；判据 4 其余读数全过 |
| ③ | 段名/文案改写范围 | `§A-② 段 OFF（新语义）：`→`§A-② `（段名去除 OFF 字样）；`§D 段 OFF`→`§D 单段（现行语义）` | 为免留「OFF」字样造成误读·未越清单范围 |
| ④ | **临时编译触发件** | `__HH312Kick.cs` 为取全量 warning 基线所建（读毕即删·`.cs`+`.meta` 双清） | 工艺工具·非交付面·`git status` 现无残留 |
| ⑤ | 首跑拼接瑕疵自发现 | 首跑日志出现 `全格=== null`（本端单段化时 `"…全格="` 与 `"== null"` 拼串重叠）⇒ 已修为 `"…全格 "` 并**重跑复测** | 终态已修正（判据 4 读数取自**修正后**跑次） |

**残余**：

| # | 项 | 说明 |
|---|---|---|
| `R1'` | **§F 对照面永久缺失** | 改前语义不可再构造 ⇒ 「Unregister 不触 GridSystem」的**两列对照**在本 build 内**不可复现**；改前读数仅在 `HH.311` 报告 §三／`Logs/hh310_f15/` 历史日志中。⚠️ ⛔ 不得据此在后续批次**升格**声称缺陷判据 |
| `R2` | 探针 `§C` 已删 | 鉴别力自证**不可重跑**；如后续需复建 ⇒ 回退面＝ commit `e1e77a1f` 的父提交（`6be5a057` 内 `0b817af1` 分支） |

## 十一、结论

1. ⭐ **`LegacyUnconditionalRemove` 全库代码面零命中**（16 → 0；作用域 `Assets`／`Packages`／`AI.Core`／Editor 全覆盖）；注释内**零标识符**（中文描述）。
2. ⭐ **归属判定唯一路径**：`_byCoord.Remove` 生产码命中 = 1，且该行**逐字节未变**（二进制比对 `True`）。
3. ⭐ **探针编译 0 error**（14 处引用全摘·首轮 `0/25` ＋ 末轮 `0/0`）。
4. ⭐ **功能不回归**：§A-②(a)-(d) 全 ✅／§B 全 `null` ✅／§D 单段 `diff=0` ✅／在册 **21 座** ✅／§F 单列「不触 GridSystem」✅。
5. ⭐ **零地图变更**：双 hash 逐值同（基线显式取 `6be5a057`·⛔ 未用 `HEAD`·⛔ 未 `checkout`）。
6. ⭐ **行尾**：`BuildingRegistry.cs` 全 CRLF·**裸 LF=0**（88→84 行）；探针**纯 LF 保持**（559→503 行）；两文件 `numstat` 均**未触发**整文件重写停手条件。
7. **常规**：0 error／**不新增 warning（25=25·全量重编译实证）**／`AI.Core` 零触／工艺读数**终态复测**。
8. `F15-F` 改后行号清单已给（§九）⇒ **契约 `03` 勘正归策划端**。

## 十二、应登记项（**四档账本由策划端处置·本端不代写**）

1. **`_编号登记.md`**：HH 水位线 **`HH.311` → `HH.312`（本报告）**
2. **`_当前快照.md`**：`F-15` 修法小批**全链闭合**（回执 `HH.310` ＋ 报告 `HH.311` ＋ **收尾批 `HH.312`**）⇒ 残余 `R1` **销项**
3. **`测试基线台账.md`**：`F-15` 行（`:902`）⇒ 收尾批已落（`e1e77a1f`·开关已删·本批零地图变更双 hash 同）
4. **`_任务队列.md`**：`F-15 收尾小批`行 ⇒ 交付待验收（`HH.312`）
5. **`03_地图即数据库.md` §6.5**：⛔ **本端一行未改**。⚠️ **需策划端勘正三处引用行号**（⑥(b)）：`BuildingRegistry.cs:75-81` ⇒ **`:69-75`**；`Unregister:43-44` ⇒ **`:39`**（⑥(a)）；若正文另有引 `RegisterFootprintCells:87` ⇒ 应改 **`:81`**。另建议 ⑥ 追加一句「A/B 对照开关已于 `HH.312` 收尾批删除（改前读数落盘 `HH.311` 报告／`Logs/hh310_f15/`）」
6. **证据落盘**：`Valley Rampart/Logs/hh310_f15/`（`hh310_probe.txt` ＋ 时间戳副本 `hh310_probe_20260918_144041.txt` ＋ `hh310_hash.txt`）
