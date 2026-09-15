# HH.284 `G2-2` ㉕机器实产 · 门判定读数 开工回执（§0 全链门 gap 表 ＋ 读数方案）

> 类型：开工回执（第一步·只读静态）｜状态：🟡 **已交付·停手待策划端过目 gap 表后放行第二步**
> 日期：2026-09-15 · 执行端 · 依据：D727 后派工（批次＝「㉕机器实产 · 门判定读数」）· Gate=`G2-2`
> 取号：**HH.284**（账本水位线 283→284 · `D640 #10` 禁预留）
> 交付报告：按水位线另取（预计 HH.285）
> HEAD 基线：`74a07517` · 未 push

---

## 〇、一句话结论

**§0 全链门 gap 表＝五环逐环勾选：环2/环4 已闭（HH.282 补齐后），环1/环3/环5 有条件下可达（非结构性不可达），但识别出 3 项须在跑局前知悉的可达性条件与 2 项口径发现。** **无「结构性不可达」判定**，第二步跑局读数具备可达性前提。**停手待策划端过目本 gap 表后放行第二步。**

---

## §一、§0 全链门 gap 表（逐环勾选 · 含实读证据）

| 环 | 内容 | 判定 | 实读证据 |
|---|---|---|---|
| **环2** | 评分可达：厂在场时 `Feasible(ProduceMachine)`（`UtilityScorer.cs:605`）> 0 | ✅ **已闭** | `Feasible` 四守卫逐条实读：①厂前置 `CountActiveDef(k.id, SiegeWorkshop) < 1 ⇒ false`（`:616`）②per-kingdom 上限 `GetPlacedMachineCountByKingdom >= GetMachineLimit() ⇒ false`（`:617`）③**prefab 预检** `anyPrefabReady`＝本族机器任一台 `!MachinePanel.IsPrefabMissing`（`:627/630`，`MachinePanel.cs:316-325` 口径＝`UnitData.prefab != null`）④金成本 `k.resources.gold >= costK.gold`（`:632`）。**③ 已由 HH.282 闭合**（10 prefab 落地 ⇒ `prefab:{fileID:0}` 计数 0/35 ⇒ `IsPrefabMissing` 全 false）⇒ **厂在场时四守卫全通** |
| **环1** | 建筑在场：AI 建造清单是否含 `SiegeWorkshop` | ⚠️ **未闭（初始不在场·需自建）** | ①**初始预置无厂**：六个 `KingdomDef`（Bedrock/IronHoof/SnowRock/RiverBay/GoldenWheat/DenseForest）`baseBuildingDefIds` **逐字相同**＝`castle/House/farm/Well/mine/Warehouse/quarry`（7 项·**无 SiegeWorkshop**）⇒ 只取前 `tier.buildingCount` 项（`KingdomFoundry.cs:156-160`）⇒ **初始从不预置厂**。②**靠行动 ㉔ 自建**：`UtilityActionConfig.asset` id:25 `BuildSiegeWorkshop`（`minStage:3` 军事期·`stageWeight[3]=1`·cost 金30/石20/木20·`buildTargetCap:1`·`buildingId:SiegeWorkshop`）在场；`Feasible` 走建造类通用分支（`:563/571-581`）＝`SiegeWorkshop.asset` **无 `raceId`（默认 -1 ⇒ 无族门禁）且无 `uniquePerKingdom`（默认 false）**、`buildTargetCap=1` ⇒ 已建 1 座后挡。③**风险点（＝`DZ-105`）**：id25 的 `need: 17 = NeedKind.ExclusiveGap`（`UtilityScorer.cs:285-286`）＝`HasExclusiveBuilding ? 0 : 0.5` **恒定占位 0.5**，不与军事缺口挂钩 ⇒ 军事期同场竞争（⑦/⑯/⑰a~e/㉗）下**可能长期被压制**。⇒ **环1 判定＝可达但不保证（有自建行动·无结构性死锁）**，与 `DZ-105` 立账定性一致 |
| **环3** | 派发：`ExecuteProduceMachine` 是否会被选中调用 | ⚠️ **有条件下可达（3 项前置须同时成立）** | `NeedScore(MachineDemand)`（`UtilityScorer.cs:304-315`）实读**三个 AND 前置**：①`SituationHub` 有快照（`:308`）②**`k.scriptPhase == ScriptStage.Military`**（`:309`，军事期硬前置）③`threatM ‖ postureM`（`:310-312`）＝「**邻接威胁非空**」或「**姿态 ≥ Alert**」。**实读展开**：`threatM` 来自 `SituationSnapshot.Threats`，其填充口径（`KingdomBrain.cs:380-393`）＝ `TerritorySystem.GetAdjacentKingdoms(id)` 的邻国、**排除玩家**、**无战士数阈值**（`snap.Threats.Add` 无条件加）⇒ `threatM` **等价于「存在至少一个邻接 AI 王国」**；`postureM` 为 OR 的补充项（`PostureHub.Get >= Alert`，`MilitaryPostureController.cs:39`，其 `HasThreat` 需邻国战士数 ≥ `alertMinThreatWarriors`）。⇒ **环3 判定＝军事期 ∧ 至少一个邻接 AI 国 ⇒ need > 0 可入池**；**孤立 AI 国（无邻接）⇒ `MachineDemand ≡ 0` ⇒ 结构性不入池**（本局 4 AI 是否全部有邻接，留第二步读数验证）。执行侧通道已通＝`KingdomBrain.cs:624-625` `case ProduceMachine: ExecuteProduceMachine` → `:1034-1071` → `sps.ProduceMachine(pick, spawnPos, kingdomId)` |
| **环4** | 生成：`ProduceMachine(...,kingdomId)` 是否 return true 且生成带 `kingdomId` 的实体 | ✅ **已闭** | `SiegeProductionSystem.cs:186-231` 实读：AI overload 走 `kingdom.Spend(cost)` → `UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, type, spawnPos, kingdomId)` → 门面内 `kingdomId>0` 覆写 `Faction.AiKingdom`（`UnitFactory.cs:139-143`）⇒ **实体带 kingdomId**；`HH.282` 已补 null 校验＋退款（`:223-230`）。`GetPlacedMachineCountByKingdom(kingdomId)`（`:133-145`）按 `unit.kingdomId` 计数 ⇒ 可读 |
| **环5** | 闸门：per-kingdom 上限（2 台）与族白名单是否把候选卡死 | ✅ **已闭（白名单不卡）**＋⚠️**1 项口径发现** | ①**族白名单**（`SiegeProductionSystem.cs:93-104` `IsRaceAllowedMachine`）＝Human→`Ballista`／Dwarf→`Mortar`／Elf→`VineCatapult`／Orc→`Ram`（`SiegeMachine` 恒 false＝D496 退役共通槽）。**四族各有唯一可造机器 ⇒ 无「本族无可造机器」卡死**。②**上限**：`GetMachineLimit()`（`:61-68`）＝`siegeMachineLimitBase(2) + perLevel(2) × max(0, WorkshopLevel()-1)`；`WorkshopLevel()`（`:47-58`）遍历 `BuildingRegistry` 取**首个** `def.id=="SiegeWorkshop" && IsActive` 的 `level`。**⚠️ 口径发现（新识别·轻）**：`WorkshopLevel()` **无 `kingdomId` 过滤** ⇒ `GetMachineLimit()` 是**全局口径**（全世界首个厂等级）而非 per-kingdom；机器厂无升级链 ⇒ `level` 恒 1 ⇒ `limit` 恒 **2**，**当前不产生行为差异**，但属口径隐患（`DZ-105` 邻域）。③**评分/执行两侧上限同源**（`:617` 与 `:198` 均调同一 `GetMachineLimit()`）⇒ 无「评分可造→执行被拒」错位 |

**排雷结论**：**无结构性不可达** ⇒ 不需按派工 §「若发现结构性不可达」停手。但**环1（id25 need 占位·`DZ-105`）与环3（邻接国前置）为可达性的两个真实条件**，第二步读数须按此设计排除项（见 §三）。

---

## §二、sim-sync 核查结论

**零义务。** 本批为**只读观察批**：不改业务代码（`Assets/_Game/**` 零改动）、不改判定/数值/分支；唯一代码面为 **Editor-only 观测域增量**（`Assets/Editor/**`，见 §三 读数方案），不入 `AI.Core`／训练仓 `harness/**`／`TuningSnapshot`/`ProfessionSnapshot`/`FactorContext`／champion/factor_registry 面。`AI.Core` 零触。

---

## §三、读数方案（第二步 · R1~R4 口径与判据）

### 3.1 观测载体（Editor-only 观测域增量 · 业务代码零改动）

**基线实读**：现有 `Editor/Smoke/Valley_DiagMilitary.cs`（HH.190/D651 授权观测域）`DumpAction` 清单**不含** ㉕与 ㉔（现列＝⑦/⑰a/⑰b/⑯/⑰c/⑰d/⑰e/③/⑨/⑩/㉗）⇒ **R1 无载体**，须增量：在既有 `DumpAction` 序列**尾插**两行（`ProduceMachine`＝㉕／`BuildSiegeWorkshop`＝㉔），并**新增一行只读读数**（各 AI 的 `SiegeWorkshop` 在场数＋已放置机器数＋`GetMachineLimit()`）。**性质**＝只读（反射/公有只读口），零业务写，与 HH.190 判例同型；**改动面＝`Assets/Editor/Smoke/Valley_DiagMilitary.cs` 1 文件（可选新增独立容器）**，**是否走既有文件由策划端定**（列报：走既有=最小面但有跨批耦合；独立容器=隔离清晰但多一文件）。

**既有可用载体**（不需改）：`[DiagMilitary] … census … top=`（`Valley_DiagMilitary.cs:251-254`，**㉕ 是否成为 top 的唯一现成读数**）／`[KingdomBrain] kX ㉕造机器落地：{pick} @ {spawnPos}`（`KingdomBrain.cs:1070`）／`[SiegeProduction] kX 生产 {type}`（`SiegeProductionSystem.cs:227`）／`[SiegeProduction] kX 生产失败：…已退款`（`:226`，HH.282 新加）／`[MilitaryPosture] kX 姿态档 → {Current}`（`MilitaryPostureController.cs:115`）。

### 3.2 四组读数（判据 · L-34 五列）

| 组 | 读数 | 可判定最早时点 | 命中即停条件 | 服务验收句 | 作用域 | 口径来源／排除项 |
|---|---|---|---|---|---|---|
| **R1 ㉕** | census `top=ProduceMachine` 出现次数／`㉕造机器落地` 与 `生产失败` 计数／**生成机器实体数按 `kingdomId` 分列** | 首次 `top=` 出现后 | `top=ProduceMachine` 首达 **且** 该 `kingdomId` 机器实体数 > 0 ⇒ **门达标**；`top=` 首达而实体恒 0 ⇒ **🔴空转** | 「㉕战争机器 AI 实产」 | 全 AI 国（逐国分列） | 口径＝census `top=`（评分面）＋`㉕造机器落地`（派发面）＋`GetPlacedMachineCountByKingdom`（存量面）；**排除项**＝玩家国(id=0)／预置机器（无预置） |
| **R2 ⑦** | `SetOccupation` 落到专属兵职业（28/29/30/31/32/33/34）的实体数 vs 通用 `Warrior(1)` | 首日即可（存量） | 长局末按 `kingdomId` 分列计数即可（**非命中即停型**） | 7 专属兵落地读数（不影响门） | 全 AI 国 | 口径＝`UnitRegistry` 逐单位 `EffectiveOccupation` 计数；**排除项**＝玩家国／怪物 |
| **R3 军事期** | 各 AI `stage` ＋ `warriorCount` | 首日即可 | `stage=Military` 且 `warriorCount ≥ 4`（`expandToMilitary_warriorsMin`）⇒ 达门（**D684 复证**） | 军事期复证（门前置） | 全 AI 国 | 口径＝`k.scriptPhase`／`k.warriorCount`（既有 `[DiagMilitary]` 状态行已含）；**排除项**＝玩家 |
| **R4 厂前置** | 各 AI 的 `SiegeWorkshop` 在场数 | 首日即可 | 任一 AI 厂数 ≥ 1 ⇒ 环1 实机侧成立 | 环1 实机对照 | 全 AI 国 | 口径＝`CountActiveDef(k.id, SiegeWorkshop)`（与 `Feasible` ①同源）；**排除项**＝玩家国／非 Active 态 |

### 3.3 跑局协议（第二步执行口径）

- **正门** `TestHarnessApi.EnterTestRun`；收尾 `ExitTestRun` ＋ `SmokeApi.QuitSmoke` ＋ 退 Play（`L-32`）。
- **seed／槽**：建议复用 `HH.230` 正跑档 `SEED=73621`／`SLOT=p1_run9`（**同 seed 可与 D684 `HH.203` PASS 对照**）；或按策划端指定（列报待定）。**禁覆盖 `p1_run9` 既有证据**（如需另存槽，按 `HH.230` 纪律新槽）。
- **时长**：军事期是硬前置（环3），须跑到至少 1 个 AI 进军事期并持续观察；建议 **120 日**（`HH.203` 同档）或按策划端定。
- **在线判据（`L-34`）**：以 R1／R3 为命中即停主判据（㉕ 首达＋实体 >0 ⇒ 门达标即停；或 `top=ProduceMachine` 首达而实体恒 0 达 N 日 ⇒ 空转即停）。

---

## §四、对配套挂账的静态定性（可先给结论·读数补充）

| 挂账 | 本批静态可给的定性 | 待第二步读数补充 |
|---|---|---|
| **`DZ-103`**（三军事向专属营观察项） | ✅ **制度面已闭合**：`UtilityActionConfig.asset` **id:18/19/21 `need: 22`（＝`MilitaryBuildingGap`）实读确认已改**（`UtilityActionConfig.cs:42/43/45` 的默认表仍为 `ExclusiveGap`＝`DZ-104` 双源债，但**现役路径读 asset**，asset 命中即用）⇒ 与 D657 裁决一致 | 三营**实机落地数**（长局自证）；本批 R4 可顺带读 WarCamp/ArcheryRange/WarAcademy 在场数 |
| **`DZ-105`**（id25 SiegeWorkshop 态势驱动口径待裁） | ⚠️ **实读确认仍未改**：asset id:25 `need: 17 = ExclusiveGap` ⇒ **恒定 0.5 占位**（与 id26 `MachineDemand` 语义错配）。**本条正是本批环1 的脆弱根因** ⇒ 本批读数可给出**首个实机证据**（id25 是否被压制 ⇒ 厂是否落地） | **`census top=BuildSiegeWorkshop` 次数＋厂落地数**（本批 R4 直接给）⇒ 若长局「top 恒 0 且厂恒 0」，则 `DZ-105` 由观察项**升为实质阻断**（㉕ 永无前置） |

---

## §五、识别出的口径发现（列报 · 非阻断）

1. **`GetMachineLimit()` 全局口径**：`SiegeProductionSystem.WorkshopLevel()`（`:47-58`）**无 `kingdomId` 过滤** ⇒ 上限取「全世界首个 Active 厂」的等级，非 per-kingdom。当前厂无升级链 ⇒ `limit` 恒 2，**无行为差异**；属口径隐患（`DZ-105` 邻域），列报不扩围。
2. **`MachineDemand` 的 `threatM` 无阈值**：`snap.Threats` 填充（`KingdomBrain.cs:391`）对**每个**邻接 AI 国无条件加入 ⇒ `threatM` 实为「存在邻接 AI 国」而非「存在威胁达阈值」；`postureM` 才带阈值（`alertMinThreatWarriors`）。二者为 OR ⇒ **环3 门比字面宽松**（列报供策划端知悉口径）。

---

## §六、红线复核

- 只读取证、不改业务代码（`Assets/_Game/**` 零改动）✅｜正门 `EnterTestRun` ＋ 退 Play（`L-32`）✅｜`AI.Core` 零触 ✅
- 本批 **sim-sync 零义务**已出结论（§二）✅｜判据用 `L-34` 五列（§3.2）✅｜禁为过线找补（`L-30`）✅
- 具名 `git add`、不 push ✅｜未碰 `GameScene.unity`／`MapGenRules.cs`／`_任务队列.md` ✅

---

## §七、停手声明

按派工要求（「策划端看过 gap 表后放行第二步」），**本回执落盘后停手**，等策划端就以下两项裁定后进入第二步：

1. **观测载体取向**：R1 载体增量走「既有 `Valley_DiagMilitary.cs` 尾插」还是「新建独立容器」？（影响面：前者 1 文件最小面、后者隔离清晰）
2. **跑局档位**：seed／槽／日数（建议 73621／新槽／120 日，与 `HH.203`（D684）同 seed 可对照；**禁覆盖 `p1_run9`**）。

---

> 执行端｜2026-09-15｜第一步（只读静态）完成：五环 gap 表逐环勾选·无结构性不可达·识别 3 项可达性条件＋2 项口径发现·停手待放行第二步
