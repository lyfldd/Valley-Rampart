# HH.228｜①-b 补做项·补 `Env` 分型落点（交付报告）

> 类型：**交付报告** · 状态：🟡**待验收** · 作业依据：**HH.227 §裁决区（D680）补做项** ＋ **`0.6` D680 追记（补做项开工口径）**
> 执行端（TraeCode·Unity 轨）· 2026-09-12 · **所属 Gate＝`G2-1`（军事期可达）**（R2）
> 取号：遵 D640 #10（水位线 HH.227 → **HH.228**，独立单行 commit `4709998`）
> 范围＝**只做 ①**（补 `Env` 分型落点）；**②（含 `JUDGE_FOCUS_KINGDOM` 侦察）不进本批**

---

## 一、实施（做了什么）

### 1.1 Env 分型落点（生产代码 7 个调用点：5 处按批口径 ＋ 2 处实读后判）

| # | 落点 | 语义（实读） | 标注 |
|---|---|---|---|
| 1 | [KingdomBrain.cs:938](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L938) | ⑦ `KingdomRace.GetKingdomRaceDef(kingdomId)==null`＝**数据/资产缺失**（[KingdomRace.cs:50-66](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomRace.cs#L50-L66)：`kingdomId<0` 或 `Resources.LoadAll<RaceDef>("Config/Races")` 无该 raceId 资产） | **Self → `Env`（实读后判）** |
| 2 | [KingdomBrain.cs:1031](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1031) | ㉕ `SiegeProductionSystem.Instance==null`＝门面单例未就绪 | **Self → `Env`** |
| 3 | [KingdomBrain.cs:1047](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1047) | ㉕ `IsMachineAllowed(myRace,…)` 全否＝本族无可造机器（族属/配置给定） | **Self → `Env`** |
| 4 | [KingdomBrain.cs:1053](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1053) | ㉕ `MachinePanel.IsPrefabMissing`＝prefab **资产缺失** | **Self → `Env`** |
| 5 | [KingdomBrain.cs:1059](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1059) | ㉕ `FindCastleCell` 无值＝世界/门面未提供主城锚点 | **Self → `Env`** |
| 6 | [KingdomBrain.cs:1251](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1251) | 建造链④ `BuildController.Instance==null`＝门面未就绪 | **Self → `Env`** |

**実装方式**：仅把 `ReportActionFail(kingdom)` 改为 `ReportActionFail(kingdom, ActionBackoff.FailKind.Env)` ＋ 行内注释说明依据；**未触碰** `ReportActionFail` 主体（[:97-107](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L97-L107)）、`ActionBackoff.ReportFail/Factor/IsCapped/Readout` 与任何因子/计数逻辑（`kind` 只写 `State.lastEnv`，见 [ActionBackoff.cs:113](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ActionBackoff.cs#L113)）。

### 1.2 语义定义补注（判定原则落码，防逐批挤牙膏）

[ActionBackoff.cs:39-45](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ActionBackoff.cs#L39-L45) 在 `FailKind` docstring 内补：**判定原则（HH.228／D680 追记）**＝失败原因落在「AI **无论怎么决策都无法自消**」的世界/门面/资产/配置给定物上 ⇒ `Env`，否则 `Self`；**边界反例**＝含 AI 自身可调参数影响者（建造链③ 选址含 `aiBuildRadius`）判 `Self`。

### 1.3 实读后判（D680 追记点名两条）

- **`:938`＝`Env`**：`raceDef` 为 `RaceDef` 资产本体，判空唯一来源＝数据/资产缺失（上表依据），AI 不可自消 ⇒ 属「资产/配置给定物」。
- **`:1241` 选址无落位＝`Self`（维持）**：D680 追记明指本条「**含 AI 选址半径影响**」（半径＝`cfg.aiBuildRadius`，AI 自身可调）⇒ **不属**「无论怎么决策都无法自消」；另与 [ActionBackoff.cs:41](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ActionBackoff.cs#L41) 既有语义例「无合法落点＝Self」一致。已就地加注（`KingdomBrain.cs:1241`）。**可一行回退/翻转（行为零影响），如策划端另判请裁。**

---

## 二、21 个 `ReportActionFail(` 调用点 · 完整 `Env/Self` 分类表

> 口径：`Select-String -Pattern 'ReportActionFail(kingdom'` 全仓 `Assets/**/*.cs` **命中 22 行＝定义 1（:97）＋调用 21**（PowerShell 默认不区分大小写，定义行亦命中）；下表＝21 个**调用点**逐点实读判。**禁凭注释/默认值判**（本表每条附实读条件）。

| # | file:line | 行动 | 实读触发条件 | 判定 | 本批是否改 |
|---|---|---|---|---|---|
| 1 | `KingdomBrain.cs:848` | ⑥招工人 | `FindRecruitableVagrant()==null`（流浪池无同族可招） | **`Env`**（既有·HH.226） | 否（前批已标） |
| 2 | `:872` | ⑥ | 粮 < `aiRecruitFoodCost` | `Self` | 否 |
| 3 | `:882` | ⑥ | `KingdomFoundry.ConvertVagrantsToWorkers` 返回 0（成败上报 false 支） | `Self`（混合·列报①） | 否 |
| 4 | `:938` | ⑦招兵 | `GetKingdomRaceDef==null`（资产/数据缺失） | **`Env`** | **是** |
| 5 | `:945` | ⑦ | `CollectRecruitCandidates` 空（缺建筑/缺 def） | `Self`（混合·列报①） | 否 |
| 6 | `:972` | ⑦ | 候选全部不可负担（金/水晶/铁不足） | `Self` | 否 |
| 7 | `:990` | ⑦ | `TryTrainFromKingdomPool==false`（成败上报） | `Self`（混合·列报①） | 否 |
| 8 | `:1005` | ⑯训练将军 | 无 `Barracks` | `Self`（前置建筑可自建） | 否 |
| 9 | `:1018` | ⑯ | 训练 false（成败上报） | `Self`（混合·列报①） | 否 |
| 10 | `:1031` | ㉕造机器 | `SiegeProductionSystem.Instance==null` | **`Env`** | **是** |
| 11 | `:1035` | ㉕ | 无 `SiegeWorkshop` | `Self`（前置建筑可自建） | 否 |
| 12 | `:1047` | ㉕ | 本族 `IsMachineAllowed` 全否 | **`Env`** | **是** |
| 13 | `:1053` | ㉕ | `IsPrefabMissing`（prefab 资产缺失） | **`Env`** | **是** |
| 14 | `:1059` | ㉕ | `FindCastleCell` 无值（无主城锚点） | **`Env`** | **是** |
| 15 | `:1063` | ㉕ | `sps.ProduceMachine==false`（成败上报） | `Self`（混合·列报①） | 否 |
| 16 | `:1094` | ⑧科技 | 金 < `techUpgradeCostGold` | `Self` | 否 |
| 17 | `:1220` | 建造① | `UtilityActionConfig.Find(focus)==null` / `buildingId` 空（**SO 配置缺失**） | `Self`（**语义属 `Env`**·列报②请裁） | 否 |
| 18 | `:1228` | 建造② | `BuildingFactory.FindDefById(bid)==null`（**def/资产缺失**） | `Self`（**语义属 `Env`**·列报②请裁） | 否 |
| 19 | `:1241` | 建造③ | `PlacementScorer.TryPick==false`（半径内无合法落位） | **`Self`（实读后判·维持）** | 否（已加注） |
| 20 | `:1251` | 建造④ | `BuildController.Instance==null` | **`Env`** | **是** |
| 21 | `:1265` | 建造⑤ | `bc.TryBuild==false`（门面校验/扣费未过） | `Self`（混合·列报①） | 否 |

**统计**：`Env` ＝ **7 个调用点**（`848/938/1031/1047/1053/1059/1251`）；`Self` ＝ 14。改动前后对比：`Env` **1 → 7**。

**Select-String 列证（本批落点全量列举，`FailKind.Env`）**：

```
Valley2_17_Smoke_5.cs:222            （T 级夹具·构造调用）
ActionBackoff.cs:116                 （实现：s.lastEnv = kind == FailKind.Env）
KingdomBrain.cs:848 / 938 / 1031 / 1047 / 1053 / 1059 / 1251   （生产调用点 7 处）
```

---

## 三、门禁

| 项 | 结果 | 证据 |
|---|---|---|
| 编译 | **0 错**；**6 条 warning 全在既有无关文件**（`Valley2_17_Smoke_5.cs:91`／`Smoke_P0:225`／`2_21A_Smoke:202`／`HH128_Probe:75`／`2_20_Smoke_Race:701/875`＝与 HH.226/HH.227 基线同 6 条），**本批改动文件零 warning** | 编译管线返回 `errors:0, warnings:6` |
| `Valley2_17_Smoke_5` | **ALL PASS** | 见下行原文 |
| **T 级等价证据** | 夹断言直证**标注链路**：构造 `ReportFail(..., FailKind.Env)` → `Readout` 输出 `(Env)` | `[2_17_5冒烟] … 退避正负例=OK(…让位门=True/回池=True/**分型True(BuildWall:0.75(Env))**)` |
| 升级判定 | 本批**纯标注**（`kind` 只写 `lastEnv`；不进 `fails`/`Factor`/`IsCapped`/计数）⇒ **不触 F 级**（`sim-sync §六`） | 代码锚点：`ActionBackoff.cs:113`；本批未触 `AI.Core` |

---

## 四、短窗 `(Env)` **同段**实测（D680 追记·取证准 A）

**口径（`L-35` 声明）**：`Select-String` **行计数**；`avoid=.*\(Env\)`／`avoid=.*\(Self\)`；**同段＝D2~D31**（修后跑被 J7 于 D31 命中收工 ⇒ 双方均截到 D2~D31）；口径来源＝`[DiagMilitary] … avoid=` 状态行（[Valley_DiagMilitary.cs:204](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs#L204)），**排除项**＝非 `avoid=` 行（如 `census`/`DumpAction` 行）。

| 判据（D2~D31） | 修前 `p1_fix3` | 修后 `p1_fix4` | 说明 |
|---|---|---|---|
| `avoid=` 行（总） | **120** | **120** | 含空读数行 |
| 其中**空**读数 | 46 | 54 | 该国当日无退避条目 |
| `(Env)` 行 | **45**（全为 `RecruitWorker`） | **40**（全为 `RecruitWorker`） | ⑥ 通道既有 |
| `(Self)` 行 | **29**（全为 `BuildWall`） | **26**（全为 `BuildWall`） | 建造链③ |
| 新增 5＋1 落点（㉕×4／建造④／⑦RaceDef）触发 | — | **0 次** | 防御性守卫路径，本窗未进入 |

**实测样例（修后·原文）**：

```
[D4][Warning] [DiagMilitary] D4 k3 … chSrc=0/4 avoid=RecruitWorker:0.75(Env)
[D5][Warning] [DiagMilitary] D5 k1 … chSrc=0/4 avoid=RecruitWorker:0.75(Env)
```

**结论（正面可读）**：`avoid=` 读数的 `(Env)/(Self)` **分型列证正常可读**（逐行可读、按行动可归因），T 级夹具直证新标注通道；**但本批新增落点在本短窗内 0 次触发** ⇒ 「(Env) 计数变化」**不来自新增落点**（详见 §六 列报③，请策划端核定该判据是否满足，或指定触发式取证方案）。

**修后跑收工档（原文）**：

```
判据命中：J7 霸占解除（k4 同段 wallTop 0/30=0.00 ≤ 修前同段 18/30=0.60 的半值）@D31
终速=0 存盘 p1_fix4=True 时间=13:28:58
```

---

## 五、常量复原声明（硬）

**✅ 已复原 `USE_DIAG_WINDOW=false`**（短窗取证用毕即复原；防 D678 勘正⑥／`DZ-136` 复发）。实读列证：

| 常量 | 复原后值 | 锚点 |
|---|---|---|
| `USE_DIAG_WINDOW` | **`false`**（回主档 120/2） | `Valley_HH80_Run.cs:38` |
| `SLOT` | **`"p1_fix3"`**（临时取证槽 `p1_fix4` 用毕复原） | `Valley_HH80_Run.cs:30` |
| `SEED` | `64513`（未动） | `Valley_HH80_Run.cs:29` |
| `CIRCUIT_BREAK_DAY`/`MILITARY_STOP_COUNT` | `120`/`2`（未动·长局档在位） | `Valley_HH80_Run.cs:31-32` |
| `ENABLE_J7_WALLTOP` | `true`（未动；**②起跑前须置 false**） | `Valley_HH80_Run.cs:54` |
| `JUDGE_FOCUS_KINGDOM` | `3`（未动；**②须随新 seed 重定**） | `Valley_HH80_Run.cs:72` |

**复原后复核**：编译 **0 错**；`Valley2_17_Smoke_5` **复跑 ALL PASS**（含 `分型True(BuildWall:0.75(Env))`）。
**已退 Play 实测**：`isPlaying=False`、`timeScale=1`（`L-32`；容器 `Finish()` 末尾自退 Play 亦已实测）。

---

## 六、自曝缺陷与列报

1. **🔴 首跑作废（漏启诊断探针·流程缺陷）**：首次短窗（`p1_log_20260912_123745.log`，12:37~13:02）**漏执行 `Valley/诊断/启动建军链诊断`** ⇒ `[DiagMilitary]` 行 **0**、`avoid=` 行 **0**、收工档 `J6 退避机制面=False wallTop=0/0/0` ⇒ **无任何 `(Env)` 读数、证据无效**。已废弃该跑（同槽 `p1_fix4` 被有效跑覆盖），**未据其申报任何结论**；本报告全部读数取自有效跑 `p1_log_20260912_131641.log`（`[DiagMilitary]` 行 **1441**）。**过程教训**：HH80 正式跑容器**不 assert 探针在场**（仅 `HH80_诊断跑` 有 `DiagMilitary.IsRunning` fail-fast）⇒ 建议后续批起跑前显式核 `P1_Observer.IsRunning`/`DiagMilitary.IsRunning`。
2. **新增落点本窗未触发 ⇒ 判据请裁**：6 个新增 `Env` 落点（㉕×4／建造④／⑦RaceDef）在本短窗均未进入 ⇒ 「`(Env)` 计数变化正面可读」若按"新增落点须触发"口径**未达成**；按"分型读数可读＋T 级直证"口径**已达成**（§三/§四）。请策划端核定口径；如需触发式取证，请示下方案（夹具/定向构造）。
3. **`Self/Env` 语义归属请裁（防逐批挤牙膏）**：`建造① :1220`（`UtilityActionConfig` 无该 focus 条目）与 `建造② :1228`（`buildingId` 无 def）**实读语义同为配置/资产给定**，按本批「判定原则」**应属 `Env`**，但**不在本批授权范围（5＋2）**⇒ **未改**，列报请裁是否并入后续批。
4. **混合型落点（本批维持 `Self`）**：`⑥:882`／`⑦:945/990`／`⑯:1018`／`㉕:1063`／`建造⑤:1265` 为"门面/系统返回 false"或"缺建筑＋缺 def"混合语义，未达"世界/门面给定"单义门槛 ⇒ 维持 `Self`；如需细分请裁。
5. **`:1241` 判据可翻**：判 `Self` 之核心理由＝D680 追记「含 AI 选址半径影响」；若策划端按"世界（地形/占用）给定"另判 `Env`，一行可翻（**行为零影响**）。
6. **同段对照非严格受控**：同 seed 长局**非逐值确定**（HH.202 首发现；本轮两次跑收工日不同 D60/D31 亦为此性质的表现）⇒ §四 为**同段口径对照**，不构成因果受控实验；差异不得单独作为行为回归证据。
7. **顺带观测（交 ②/①-b 参考，本批不裁）**：本跑 J7 于 **D31 命中**——k4 同段 `wallTop 0/30`（修前同段 18/30）⇒ 与 ①-b「霸占解除」靶重定相关；**②起跑前须按 D680 关 J7**（`ENABLE_J7_WALLTOP=false`）以免与本批同因提前收工。
8. **环境列报（非产品缺陷）**：本批期间 Unity MCP 桥发生切换（原 `mcp_unity-bridge` 断连；实际连接对象为 **MCPForUnity v3.4.7 服务**，经其 HTTP 端点 `127.0.0.1:8080/mcp` 与 IDE 侧 `mcp_unityMCP` 完成菜单/编译/控制台操作）。**未改用编辑器脚本替代 MCP**；工具面差异（`execute_menu_item`/`read_console`/`manage_editor`）已在此列报。

---

## 七、红线自检

- **只做 ①**：②（含 `JUDGE_FOCUS_KINGDOM` 侦察）未做 ✅
- **设计文档／队列零改动**（只读）✅
- **`AI.Core` 零触碰**：本批未触及；王国脑层与 `sim` 双端零镜像（HH.225 §二 实测）⇒ 不涉 `sim-sync` **改动**；**本批未经 sim 门禁**（纯标注·T 级）
- **不改因子/计数行为**（D675 硬约束①）：`kind` 只写 `lastEnv` ✅
- **枚举未新增**（`FailKind` 未动 ⇒ 不涉 `L-28`）✅
- **禁参数微调找补**（D563③）：未调任何参数 ✅
- **正门＋15x＋退 Play**：`TestHarnessApi.EnterTestRun` 正门（容器内）＋15x；**已退 Play 实测** ✅
- **写-改-commit 同串、只提本串、不 push** ✅（取号独立单行 `4709998`；本报告随交付串 commit）

---

## 八、证据清单

| 证据 | 位置 |
|---|---|
| 修后短窗日志（有效） | `Valley Rampart/Logs/P1/p1_log_20260912_131641.log`（`[DiagMilitary]` 1441 行；`avoid=` 120 行；D2~D31） |
| 修前同段基线日志 | `Valley Rampart/Logs/P1/p1_log_20260912_113943.log`（`p1_fix3`） |
| 收工档（判据命中原文） | `Valley Rampart/Logs/P1/hh80_run_status.log` |
| 作废首跑日志（列报①） | `Valley Rampart/Logs/P1/p1_log_20260912_123745.log` |
| 检查点存档 | `Saves/p1_fix4_day*` |
| Env 落点全量列举 | §二 Select-String 输出块 |

---

*执行端 2026-09-12（HH.228 交付报告，取号 `4709998`）。停手待策划端验收。*

---

## 策划裁决（D682，2026-09-12，主策划端）

**裁决编号＝D682**（取号遵 D640 #10，独立单行 `ed0f5ca`）。**验收结论＝实施忠实＋机制成立＋T 级零行为改**（判据三直读已过），**但 ① 未完全闭环 ⇒ 列 2 项补遗令**。

### 一、验收依据（实读核对）

| 判据 | 实读结论 |
|---|---|
| 21 点逐点标注 | `KingdomBrain.cs` 全部 **21** 个 `ReportActionFail(` 调用点逐点标注（含语义原文），核对通过 |
| `Env` 落点 | **7 处**（`:848` ⑥无候选／`:938` ⑦`RaceDef` 资产缺失／`:1031` ㉕`sps==null`门面单例／`:1047` ㉕本族无可造机器／`:1053` ㉕prefab 资产缺失／`:1059` ㉕主城 anchor 缺失／`:1251` 建造链④门面未就绪）——**与声明一致** |
| docstring | `ActionBackoff.cs:42-44` **已补「判定原则＋边界反例」** |
| 行为零改 | 全仓 grep 已证：`FailKind` 只经 `ReportFail` 写入 `s.lastEnv`；`lastEnv` 仅 `Readout:180-182` 读取拼后缀，**未进** `fails`／`lastFailDay`／`snapNeed`／`Factor`／`IsCapped` |
| 常量复原 | `Valley_HH80_Run.cs`：`:38 USE_DIAG_WINDOW=false`／`:30 SLOT=p1_fix3`／`:31 CIRCUIT_BREAK_DAY=120`／`:32 MILITARY_STOP_COUNT=2` |
| 探针读数 | `Valley_DiagMilitary.cs:203 avoid=Readout`（含 `(Env)/(Self)`）／`:222-225 ev=` 在位 |
| 夹具 | `Smoke_5:202-231` 三组断言在位 |
| 门禁 | 编译 0 错（6 warning 全既有无关）＋`Smoke_5` ALL PASS＋**T 级判定成立**（实读证实零行为影响 ⇒ 不升 F 级） |

### 二、🔴 补遗令 2 项（①闭环前须补）

**补遗 A．`:1220`／`:1228` → `Env`（`Env` 落点 7→9 处）**
- 判定条件：`def0==null || buildingId 空`＝**配置给定物缺失**；`BuildingFactory.FindDefById==null`＝**资产缺失** ⇒ **严格落在 `D680 追记` 自订判定原则内**（执行端自判"语义属 Env"、因未授权未改而请示）。
- ⇒ **准补 ⇒ `Env` 落点 7→9 处**。
- **根因＝策划侧口径不完整**：`D680 追记` 给了原则却**未按原则穷尽 21 点**、只逐个点名 5 处 ⇒ **逐批挤牙膏——正犯了自己要防的病**。

**补遗 B．容器加「探针在场」fail-fast**
- 事实：`DiagMilitary.IsRunning` 属性存在（`Valley_DiagMilitary.cs:110` 注释明写"供跑局容器断言"），且 `Valley_HH80_DiagRun.cs:39-43` 已有 fail-fast，**但 `Valley_HH80_Run.cs` 无**（仅 P1Observer fail-fast `:112-118`）。
- 后果：**首跑漏启探针时容器静默空跑至窗口满**（本次首跑证据作废之根因）。
- ⇒ **准补**：在 `Valley_HH80_Run` 就绪检查处加 `if (!DiagMilitary.IsRunning) { LogError+return; }`（同 P1Observer 先例）。

### 三、验收三问（教训定性）

| 项 | ①原则与实况 | ②流程 | ③可补/我没查 | 定性 |
|---|---|---|---|---|
| 补遗 A | 不一致 | **策划侧流程漏洞（口径未穷尽套用）** | 可补、且我没查 | **`L-33` 家族实例＋1**（结论性声明须穷尽直读支撑） |
| 补遗 B | 容器缺探针在场断言 | **流程漏洞** | 可补未补、没查 | **`L-34` 家族实例＋1**（判据/证据前提未自检） |

### 四、其余 6 项自曝裁决（全部＝准）

| # | 自曝项 | 裁决 |
|---|---|---|
| ③ | 混合型落点维持 `Self` | **准** |
| ④ | 同段对照非严格受控 | **准**（`HH.202` 长局非逐值确定，同段比较为当前最优；②建议同 seed 严格对照） |
| ⑤ | MCP 桥环境列报 | **准**（信息） |
| ⑥ | 新增 6 落点本窗 0 次触发 | **准**（防御性守卫路径，短窗不触发属预期；证据由 `Smoke_5` 夹具承担） |
| ⑦ | 短窗 `(Env)45→40`／`(Self)29→26` 读数正面可读 | **准** |
| ⑧ | `J7 命中@D31` | **准·交 ② 参考** |

### 五、⚠️ 另核出：`ENABLE_J7_WALLTOP` 与 120 档语义冲突

`ENABLE_J7_WALLTOP=true` 与 `USE_DIAG_WINDOW=false`（120 档）**语义冲突**：`BASE_WALL_TOP_DAYS` 仅覆盖至 D65，跑过 D66 后同段关系失效 ⇒ **误停风险**——**印证 D680 裁「② 关 J7」**，列为 **② 前置**。本批短窗 `J7 命中@D31` **有效**（当时 `USE_DIAG_WINDOW=true` ⇒ 60 档、基线覆盖 D65 ⇒ 同段成立）。

### 六、闭环判据

补遗 **A＋B 完成** ＋ **复跑 `Smoke_5`** ＋ **更新本报告追记** ⇒ **① 闭环 ⇒ 方可起 ②**（守 D676 批间硬门禁）。

---

*主策划端 2026-09-12（D682 裁决，取号 `ed0f5ca`）。*
