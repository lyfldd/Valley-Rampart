# HH.342 ·「DZ-新」存档生成冲突修复批（A 批）· 交付报告

> 执行端 ｜ 日期：2026-09-26 ｜ 任务书：`多Agent交接/策划端/HH.342_DZ-新存档生成冲突修复批_任务书.md`
> 状态：**代码与验证已完成；⛔ 未提交、未 push；等待事务端逐文件逐行核实后入提交**。
> ⛔ 未改四本账本、未改最高优先级文档、未取 D 号、未碰 Building.cs／GridSystem.cs／场景／Prefab／旧资产键／AI.Core／HH.341 任务协议与生产源文件。

---

## 〇、开工回执（四项）

1. **计划改动文件**：`BuildingFactory.cs`（SpawnFromSave ＋ 同文件去重辅助）、以及允许但**未改动**的 `SaveManager.cs` / `WorldManager.cs`（说明见 §三.3）。
2. **当前 file:line 锚点（开工复核，实测）**：
   - `BuildingFactory.SpawnFromSave` = `:260-351`（任务书 `:260-326` 偏早）；冲突 LogError = `:307`（占格判据块 `:302-311`）；`CreateBuildingInstance` = `:89-204`；`ClearAllBuildings` = `:363-376`
   - `SaveManager.Load` = `:308-382`；spawner 循环 `:356-367`（`:363` 调 `SpawnFromSave`）；`RegisterSpawner` = `:164-167`；`DistributePayloads` = `:385-413`
   - `WorldManager.GenerateWorld` = `:102-148`（`ClearAllBuildings` `:105-106`；`PopulateFromMap` `:126`；`RebuildOccupancyFromRegistry` `:132`；实例化闸门 `:141-142`）；`LoadState` = `:328-364`（版本门控 `:348`；`foundKingdoms:false` `:350`）
3. **回退点**：HEAD = `d0b03782956457ff110e29e1121c20fd7529cf7b`（"L-96：写盘方向搞反"）；开工时 `_Game` 生产域 `git diff` 为空 ⇒ 可独立回退。
4. **复现参数**：`TestHarnessApi.EnterTestRun(seed=31418 / WorldSize.Small / difficulty=2)`；栈 `BuildingFactory.cs:307 ← SaveManager.cs:363`；四坐标 `(75,28)/(71,29)/(45,110)/(42,112)`，`defId=farm/Warehouse`。

---

## 一、结论速览

| # | 项 | 结论 | 标级 |
|---|---|---|---|
| 1 | 四坐标冲突 | **修复后三种场景（Play 内 / 退 Play 后 / 空白对照）全 0** | `[实读]` |
| 2 | 闭环 | 建局→保存→读档→再保存→再读档：Load#1 / Load#2 冲突均 **0**；两次保存 `Building_` 条目恒 **15**（无增长） | `[实读]` |
| 3 | 唯一占用 | 四坐标各 **1** 栋；`BuildingRegistry.Count=15`、`saveablesBuildingKeys=15`、唯一占格对象 **15** | `[实读]` |
| 4 | saveId/defId 对齐 | 四坐标 saveId 全部与存档条目一致、defId 一致 | `[实读]` |
| 5 | 编译 | `Assembly-CSharp.dll` mtime `2026-09-26 17:03:00`（重编译）；`filter_text=error CS` = **0** | `[实读]` |
| 6 | ⭐ **任务书前提勘正** | **根因不是「双路径双份」**，而是**冲突判据把 footprint 重叠误报为双份**（见 §二）⇒ **须事务端/策划端确认修复口径** | `[实读]` |
| 7 | 未归因相邻缺陷 | 读档 `WorldSize.Small(0)` 被当"缺字段" ⇒ **回退 Medium，网格 128→256**；⛔ 本批未自选边，报裁（见 §九） | `[实读]` |

---

## 二、根因定案（修复前基线实读铁证）

**手段**：`Logs/hh342_probe_02_baseline.cs`（bridge `execution_mode:"play"` 顶层脚本，⛔ 未入 Assets、⛔ 只读反射＋既有公开接口），
读数原文 `Logs/hh342_baseline/baseline_probe_prefix.txt`（修复前）。

**四条冲突的 occupied 真实身份**（与"疑似路径 A 新随机 GUID"的旧注释**不符**）：

| 冲突坐标 | occupied 实际是哪一栋 | 为何命中该格 | occupied saveId | 存档侧 saveId / defId |
|---|---|---|---|---|
| (75,28) | `castle@(73,26)` 3×3 k1 | 3×3 覆盖 (73..75, 26..28) ⊇ (75,28) | `Building_dfa916b2e48f496791bf4283e0d01f66` | `Building_7e4a6336…` / farm |
| (71,29) | `House@(71,28)` 2×2 k1 | 2×2 覆盖 (71..72, 28..29) ⊇ (71,29) | `Building_b7f94a5618fd45079371bd601ae7ae17` | `Building_cc4121e4…` / Warehouse |
| (45,110) | `castle@(44,109)` 3×3 k2 | 3×3 覆盖 (44..46, 109..111) ⊇ (45,110) | `Building_25e7c54e8ce14811b0e64d99d787bb8a` | `Building_6d6298f2…` / farm |
| (42,112) | `House@(42,111)` 2×2 k2 | 2×2 覆盖 (42..43, 111..112) ⊇ (42,112) | `Building_574a2bb7bb9a49de9c3b0ffa169a5f63` | `Building_80641926…` / Warehouse |

**判据链**：`SpawnFromSave` 原判据 ＝ `GridSystem.Instance.GetOccupant(coord) as Building`——`GetOccupant` 回答的是「**这一格上有没有占格物**」（footprint 覆盖格语义），
而读档重建顺序为**存档条目序**（castle → House → farm → …）⇒ 重建 `farm@(75,28)` 时，其主格 (75,28) 已被**先重建的邻接 castle** 覆盖 ⇒ 报"冲突"。
实际创建完成后，`MarkOccupiedFootprint` 按「后写者胜」把 (75,28) 归属回 farm（与建局路径一致）⇒ **读档结果正确，冲突是假 alarm**。

**"预置落点 footprint 允许重叠"是已认账事实**（`F-15`／`03 §6.5`：实盘 9 座建筑 footprint 相互重叠 ⇒「允许重叠 ＋ 显式化」）⇒ 本例四条全部落在该已认账面内。

**反证（证明不是双份/腐坏）**：
- 四坐标 occupied 的 `saveId`/`defId` 与存档条目**不同** ⇒ 不是同一条目被生成两次；
- 修复前 `BuildingRegistry.Count=15`、`saveablesBuildingKeys=15`、唯一占格对象 **15**（无 16/30）；
- 修复前 **Save#2 存档 `Building_` 条目 = 15**（与 Save#1 相同）⇒ 无复合腐坏增长。

---

## 三、修复设计与逐文件 file:line 改动区间

### 3.1 改动清单（唯一生产文件）

| 文件 | 改动区间（**改后**行号） | 性质 |
|---|---|---|
| `Valley Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs` | **267-268**（新增 2 行） | `SpawnFromSave` 入口重建计数日志（使「调用数＝条目数」可复算） |
| 同上 | **299-316**（替换原 `:297-311`） | 双份判据勘正：`GetOccupant(格)` ⇒ `FindRegisteredAtOrigin(coord)`（同主坐标） |
| 同上 | **359-376**（新增 18 行） | 同文件去重辅助 `FindRegisteredAtOrigin(GridCoord)`（private static） |

`git --no-pager diff -U0` 原样 hunk 头：`@@ -266,0 +267,2 @@` ／ `@@ -297,6 +299,13 @@` ／ `@@ -304,7 +313,4 @@` ／ `@@ -352,0 +359,18 @@`

### 3.2 判据勘正（新旧对照）

**旧**：目标格上**存在任意 Building** ⇒ `LogError`（假 alarm；且继续创建，不处置）
**新**：
- 真双份判据 ＝ **`BuildingRegistry` 中存在同主坐标（`coord`）的 Building**（同一条目被二次生成时主格必然相同）；
- 命中 ⇒ 保持 `LogError`（响亮）＋ **`return` 不创建第二份**（不产生第二份注册/占格/存档对象）；
- 未命中 ⇒ 正常重建（footprint 重叠按现行「后写者胜」语义，与建局路径一致，**不报**）。

**⛔ 未采用**（任务书 §四.2 明令禁止）：吞错、静默跳过、改成 Warning/删文案、删除四坐标数据、改存档键、"格被占就跳过但不恢复"。
**⚠️ 须核实点**：该分支在 v2 读档路径下**当前不可达**（`WorldManager.LoadState:348` 已门控 `instantiateBuildings=false`）⇒ 属**防御性分支**（防未来 A/B 双路径回归）；跳过分支不会导致建筑丢失（同主坐标已有实例）。

### 3.3 允许但**未改动**的两条路径（说明）

- `SaveManager.cs`：`Load` 顺序（Global→Spawner→Scene）与 spawner 匹配逻辑**经实读无缺陷**（§五计数可复算），改动只会扩大提交面 ⇒ **不改**。
- `WorldManager.cs`：`GenerateWorld` 与 `LoadState` 的 v2 门控**行为正确**；其相邻缺陷「尺寸漂移」涉及重新定值 ⇒ **不改并报裁**（§九）。A 面 `git diff` 因此只命中 1 文件。

---

## 四、四坐标零复现表（修复后）

计数单位：**坐标 × L-95 段**（过滤词＝完整前缀 `[BuildingFactory] SpawnFromSave 冲突`）

| 坐标 | Play 内冲突条数 | 退 Play 后冲突条数 | 空白对照冲突条数 | 唯一占用数 | 存档 saveId 对齐 | 结果 |
|---|---:|---:|---:|---:|---|---|
| (75,28) | 0 | 0 | 0 | 1 | 是 | PASS |
| (71,29) | 0 | 0 | 0 | 1 | 是 | PASS |
| (45,110) | 0 | 0 | 0 | 1 | 是 | PASS |
| (42,112) | 0 | 0 | 0 | 1 | 是 | PASS |

- Play 内另覆盖**两种进局面**：① `EnterTestRun` 建局→保存→读档（同会话闭环，2 轮）② 进 Play **不建局**直接读档（玩家「继续游戏」面）⇒ **均 0**。
- 修复后四坐标 saveId（会话 B）：`6a502e9b…` / `4ef8630b…` / `bf3026d0…` / `30a03bd6…`，defId `farm`/`Warehouse`/`farm`/`Warehouse`，kingdom `1/1/2/2`，逐项与存档条目相等。

---

## 五、读档链一致性计数（修复后，可复算）

| 判据 | 读数 | 口径/来源 |
|---|---|---|
| `_spawners` 数 | **3**（`UnitFactory`→`Unit_`／`BuildingFactory`→`Building_`／`WaveDirector`→`Portal_`） | 反射读 `SaveManager._spawners` |
| 建筑 Scene 条目数 | **15** | `GetSaveMeta` 逐条（`phase=1` 且 `saveId` 前缀 `Building_`） |
| `SpawnFromSave` 调用数 | **15**（单次 Load） | 新日志 `[BuildingFactory] SpawnFromSave 调用 …` 逐条计数 |
| 同一 ModuleSaveEntry 命中 spawner 数 | **1**（前缀互斥，`break`） | 源码 `SaveManager.cs:356-367` + 3 前缀互斥读数 |
| 重复 saveId 新增数 | **0**（Save#2 ＝ 15 条，与 Save#1 相同） | `Save#2 modules总数=55 Building_条目数=15` |
| `BuildingRegistry.Count` | **15**（建局/读档后一致） | 读数快照 S1~S5 |
| `_saveables` 中 `Building_` 键数 | **15** | 反射读 `SaveManager._saveables` |
| 网格唯一占格对象数 | **15**（`_occupants` 非空子格 960） | 反射读 `GridSystem._occupants` |
| 四坐标最终 LoadState 恢复对象 | 四坐标 occupant = 对应 farm/Warehouse（saveId 一致） | 读数快照 S2/S4 |

---

## 六、L-95 三段 Console 原始读数

| 段 | 会话 | 读数 | 原始文件 |
|---|---|---|---|
| 段1 Play 内 | 修复后·同会话闭环 | `types=error` 全量＝**3**（`ruler` 缺脚本／`No Theme Style Sheet`／`287 node options`）；`filter_text=SpawnFromSave`＝**0**；`filter_text=冲突`＝**0** | `console_l95_seg1_play_postfix.txt` |
| 段2 退 Play 后 | 同上 | `types=error`＝**4**（3 常驻 ＋ `not cleaned up …[SpriteAnimatorDriver]`）；`filter_text=SpawnFromSave`＝**0** | `console_l95_seg2_afterstop_postfix.txt` |
| 段3 空白对照（只进 Play、不建局、不读档） | — | `types=error`＝**3**（常驻）；`filter_text=SpawnFromSave`＝**0**；`filter_text=error CS`＝**0** | `console_l95_seg3_blank.txt` |
| 补充：干净场景直接读档面 | 修复后·会话 A | `filter_text=SpawnFromSave`＝**0**；`types=error`＝**3** | `console_l95_crosssession.txt` |
| **修复前对照** | 修复前·同会话闭环 | 段1 `filter=SpawnFromSave`＝**98**（all 型）；段2 `types=error`＝**11**（含 8 条冲突＝2 轮×4）、`filter=SpawnFromSave`＝**8** | `console_l95_seg1_play.txt`／`console_l95_seg2_afterstop.txt` |

⚠️ 口径注：`read_console(types=["error"])` 实为**文本过滤**（消息含 "error" 字样即命中）⇒ 段1 修复前 98 条含 Log 回显，报告只采用 `filter_text=SpawnFromSave` 的精确计数。

---

## 七、复现包（命令 / 过滤词 / 计数单位）

- **进局正门**：`TestHarnessApi.EnterTestRun(cfg)`，`cfg{kingdomName=hh342probe, mapSeed=31418, worldSeed=31418, difficulty=2, worldSize=Small, selectedSlotId=hh342_base}`；槽位先 `SaveManager.Delete`。
- **闭环命令（探针内既有公开接口）**：`SaveManager.Save("hh342_base")` → `Load` → `Save` → `Load`，各步快照。
- **探针载体**：bridge `execute_csharp_script`（`execution_mode:"play"`），脚本 `Logs/hh342_probe_02_baseline.cs`／`hh342_probe_04_crosssession.cs`；⛔ **未在 `Assets/` 下创建任何文件**（无新增 `.cs`/`.meta`，见 §十）。
- **Console 过滤词**：`SpawnFromSave`（精确前缀 `[BuildingFactory] SpawnFromSave 冲突`）；**计数单位**：条（一条日志一行）。
- **存档**：`C:/Users/trs/AppData/LocalLow/DefaultCompany/Valley Rampart/Saves/hh342_base.json`（saveVersion=3，55 模块，15 条 `Building_`）。

---

## 八、回退包

| 项 | 内容 |
|---|---|
| 修复前基线 | `Logs/hh342_baseline/baseline_probe_prefix.txt`（S1~S5 快照 ＋ Load#1/#2 各 4 条冲突原文） |
| 修复后基线 | `Logs/hh342_baseline/postfix_probe.txt`（同结构，冲突 0/0）＋ `crosssession_baseline.txt`（干净直接读档 0） |
| 差异根因 | 判据语义错误：`GetOccupant(格)`（footprint 覆盖格）→ 应为「同主坐标重复」；非双路径双份（§二） |
| 独立回退点 | 单文件改动；`git checkout -- "Valley Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs"` 即回退（HEAD `d0b03782`） |
| 行尾纪律 | `git ls-files --eol` ＝ `i/lf w/crlf` ⇒ 全程二进制替换保持 CRLF（426 行 → 405 行） |

---

## 九、未归因项与观察（⛔ 本批未自选边，报裁）

1. **⭐ 读档世界尺寸漂移（相邻缺陷，建议另批）**：`WorldSize{Small=0,Medium=1,Large=2}`；`WorldManager.LoadState:339` 用 `data.worldSize > 0 ? … : WorldSize.Medium` 兜底 ⇒ **Small 存档读档后被当缺字段、回退 Medium**（实读：`size=Small,网格=128x128` 建局 → 读档 `size=Medium,网格=256x256`）。修法需区分"缺字段"与"Small=0"（如存档哨兵），**属重新定值/扩存档语义** ⇒ 停手报裁。
2. **观察：`ChangeSaveId: 目标 ID … 已存在，覆盖`**（读档期）——根因＝`ClearAllBuildings` 走 `Object.Destroy`（延迟）且 `Building` 无 `OnDestroy` 注销 ⇒ `_saveables` 暂留旧键。实测**不影响**本批判据（`Save` 跳过已销毁对象，条目数恒 15；`TryGetSaveable` 有幽灵守卫）⇒ 本批未改（避免扩大改动面）。跨档连续读档会累积幽灵键，建议列入 D2/清理批。
3. **说明**：四坐标冲突日志的旧文案「疑似路径 A 新随机 GUID+默认 kingdomId」**与实读不符**（occupied 实为邻接建筑），本批已在代码注释中勘正记录。

---

## 十、边界与回归

- `git --no-pager diff --name-only -- Valley Rampart/Assets` ⇒ `BuildingFactory.cs` ＋ `Scenes/GameScene.unity`。
  ⚠️ **`GameScene.unity` 为会话前既有脏点**（mtime `2026-09-15 17:17:07`，远早于本会话；diff `5 insertions/1 deletion`）⇒ ⛔ 非本批改动、本批未触碰场景。
- **B 面命中 = 0**：`WorldGatherSource.cs`／`Assets/_Game/Systems/AI/**` 在 A 提交面 `git diff` 命中 **0 行**（反向亦成立，B 批未开工）。
- **A 面之外的生产域改动 = 0**（`_Game` 下除 `BuildingFactory.cs` 外无命中）。
- `git diff --check` ⇒ exitCode **0**（仅一条既有 `GameScene.unity` 的 LF→CRLF 提示）。
- `Assets/` 未跟踪新增 = **10**（`Valley_HH289_*`／`Valley_HH290_*`／`Valley_HH319_*` 探针残留与美术资源，**均为会话前既有**；本批零新增）。
- 编译：`Assembly-CSharp.dll` mtime `2026-09-26 17:03:00`；`filter_text=error CS`＝**0**；本批新增 Console error＝**0**（既有 3 常驻已分列）。

---

## 十一、交付物清单 ／ 事务端核实请求

1. 本报告：`多Agent交接/执行端/HH.342_DZ-新存档冲突修复_交付报告.md`
2. 台账（允许面）：`Logs/hh342_baseline/`（`baseline_probe_prefix.txt`／`postfix_probe.txt`／`crosssession_baseline.txt`／`console_l95_*`×6／`git_evidence*.txt`／`bridge_state.txt`）
3. 探针与调用面：`Logs/hh342_probe_01…05*.cs`、`Logs/hh342_payload_01…05.json`、`Logs/hh342_patch.py`、`Logs/hh342_wait_play.ps1`、`Logs/hh342_git_evidence.ps1`
4. **事务端核实请求（⛔ 核实前不得入提交）**：
   - 逐文件：确认生产改动**仅** `BuildingFactory.cs`（3 处区间见 §3.1）；
   - 逐行：确认 `267-268`／`299-316`／`359-376` 与 diff 一致、行尾 CRLF 未破、无越界；
   - **确认口径**：本批根因＝判据误报（非双路径双份）；修复采用"同主坐标"防御性判据；§九-1 尺寸漂移**未修**是否接受、是否另立批。

---

## 【应登记项】

> 交策划端/事务端回填（⛔ 执行端未改任何账本、未取 D 号）。

```
HH.342 | DZ-新 存档冲突修复 A 批 | 执行端 | 四坐标 (75,28)/(71,29)/(45,110)/(42,112) 修复后 L-95 三段全 0；同会话闭环 Load#1/#2 各 0；干净直接读档 0；Save#2=15 条无增长；编译 error CS=0 | 根因＝判据误报（GetOccupant 覆盖格语义 vs 邻接建筑 footprint 重叠），非双路径双份；改动仅 BuildingFactory.cs 3 区间；⛔ 待事务端核实后入提交 | 不写 D 号
HH.342 | 观察·读档世界尺寸漂移 | 执行端 | WorldSize.Small=0 被 LoadState `data.worldSize > 0` 当缺字段 ⇒ 读档回退 Medium（128→256 网格） | 属重新定值/扩存档语义 ⇒ ⛔ 本批未自选边，请裁是否另立批
HH.342 | 观察·ChangeSaveId 覆盖告警 | 执行端 | ClearAllBuildings 延迟销毁 + Building 无 OnDestroy 注销 ⇒ `_saveables` 暂留旧键（读档期覆盖告警） | 不影响本批判据（Save 跳过已销毁对象）；建议列入清理批
HH.342 | 数字勘正 | 执行端 | 四坐标冲突文案「疑似路径 A 新随机 GUID+默认 kingdomId」与实读不符：occupied 实为邻接建筑 castle@(73,26)/House@(71,28)/castle@(44,109)/House@(42,111) | 已在代码注释勘正；对应 `DZ-新`（`_任务队列.md:107`）
```
