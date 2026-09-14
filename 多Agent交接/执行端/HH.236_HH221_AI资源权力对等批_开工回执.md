# HH.236｜HH.221「AI 资源权力对等批」（Gate=`G1-1`）· 开工回执

> 类型：**开工回执（协议核对闸 ＋ 正式 `sim-sync` 核查）** · 状态：🟡**核查已落地·按 D685 批内序进入实施**
> 执行端（TraeCode·Unity 轨）· 2026-09-12 · 依据：**HH.221 任务书**（D665 签发）＋ **D685 开工许可**（6 项裁示全 A）
> 取号：遵 D640 #10（水位线 HH.235 → **HH.236**，独立单行 commit `87bb190`）
> 前置：**HH.220/HH.223 已收口**（`G2-1` 军事期门槛达成，HH.231 七考重验 PASS）

---

## 〇、D685 六项裁示复述（我照此实施，逐条不擅扩）

| # | 裁示 | 我的实施边界 |
|---|---|---|
| A① | **新建「世界资源点采集源」包装**（per-命令绑 `kingdomId`，照 `TreeGatherSource` 先例）；**玩家侧现状不动** | 新类落 `World/`；`TreeGatherSource`／`ConfirmTreeGather`／玩家 UI 路径**零改动** |
| A② | `UtilityAction` **尾插** `GatherWorldResource=27`（守 `L-28`）＋`NeedKind` 尾插缺口函数＋`UtilityActionConfig.asset` 新行；**单源引 `2_23` 通道B 分诊**（避 `DZ-118/DZ-128`） | 尾插不改中间位；不新造分诊体系，复用既有通道B no-op 语义 |
| B | `asset:57`（id3）`axis 0→1` ＋ `.cs:26` 回退表 **同改**（防 `L-15` 双源）；否 C（`DZ-104` 不搭车） | 仅改轴值，不动 `costGold`／`needA`／`buildingId`（禁参数微调找补 `D563③`） |
| `DZ-135` | **只诊断＋报裁**（给 `feasible=True` 缺口实证与候选路径），**本批不改成本门、不加金通道** | 出 `file:line`＋数值实证；**不碰** `Feasible`／`costGold` |
| 基线 | 复用 **seed 64513 ＋新槽**；窗口 **D5~D15**；档位用**独立常量**（**不回改主档 120/2**）；交付须显式标「同段／同 seed／同档」 | 容器新增独立常量，主档 `CIRCUIT_BREAK_DAY=120`／`MILITARY_STOP_COUNT=2` **逐字不动** |
| 报备 | 账本 HH 行首已勘正 `232→233`；他端未提交改动不卷；**本批不重跑七考、`P-005` 不在本批** | `_任务队列.md` 他端 T09 行**不并入**本串 commit |

---

## 一、§0 `L-30` 勾选清单复核（判定线硬条件 gap 表·五列）

| 硬条件 | 当前值（实读） | 目标 | 本批如何闭 | 预计达成 | 窗口 |
|---|---|---|---|---|---|
| AI 能取到世界一次性资源 | **0 通道** | ≥1 通道在产 | A① 落地 `ITaskSource` 源 | D1~D3 | 短窗／纯代码 |
| k1 型（无 quarry）石入库 | `prod=0/in=0` | `in>0` | A①＋A②（石堆／矿脉可选采） | D5~D15 | 短窗 |
| ③建产能可被经济型国选中 | 轴错配→不选 | 进池可竞争 | B 轴纠正 | 夹具即证 | 夹具 |
| `warrior≥4` 自主达成 | max3 | ≥4 | **归后续七考重验** | 长局 | 90~120 日 |
| ③建产能可达（D670 新增） | D6 实测 `feasible=False`（金 `3~20 < 50`） | `feasible=True` | **不闭**：A 之外第二层；挂 `DZ-135` 只诊断 | 须实测 | 短窗 |

> **本批闭 3 行**（前 3 行），第 4 行归七考，第 5 行只诊断报裁（`DZ-135` ＝ A 裁）。

## 二、§0b 在线判据表（`L-34`／`test-harness-first §八`）→ 照表挂容器

| 判据 | 可判定最早日 | 命中即停 | 本批容器实现位 |
|---|---|---|---|
| 世界资源点源注册数 ≥1（`ITaskSource`） | D1~D3 | 未注册 ⇒ 判「通道未落地」并停 | `Valley_DiagMilitary.CountGatherSources()`（现成，数类型名含 `Gather`）⇒ 新类命名**必须含 `Gather`** |
| k1 型 `Stone/Wood in > 0` | D5~D15 | 命中 ⇒ 资源对等达成（机制面可停） | `DumpKingdom` 状态行 `in=` 分项 + 新 `JudgeKind` |
| 采集通道全 0 且 `in` 连续 ≥10 日=0 | D10 | 判「通道僵死」并停（止损） | 新增判据（D5 起计数，D15 封顶） |
| ③（`axis=Economy`）进池可竞争 | 夹具 | 夹具即证 | 冒烟夹具（读 `ScoreTop` 候选池） |
| `drive=0` 且 `target` 全程 ≤3 | D5~D10 | 负探针定论并停 | 复用既有 J3/负探针通道 |

> **本节填毕即为开跑前置**（`L-34`：未规定「可判定最早日＋命中即停」不得开跑）。

---

## 三、A / B / C 根因取证（先诊断后修·D648/D649，全部 `file:line` 实读）

### 3.1 A 主修根因＝「资源获取」这条底层权力只实现了玩家侧

| 层 | 实读锚点 | 事实 |
|---|---|---|
| 玩家侧（完整） | [BuildingPanel.cs:590](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingPanel.cs#L590) | `_target.StartGather()` 唯一调用者＝玩家 UI |
| | [Building.cs:859-865](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L859-L865) | `StartGather()` 置 `isBeingGathered=true` 即返回（无国别参数） |
| | [ResourceRespawnSystem.cs:109-122](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs#L109-L122) | `ConfirmTreeGather` **全库 0 调用者**（`Grep` 命中仅定义行） |
| | [TreeGatherSource.cs:12](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/TreeGatherSource.cs#L12) | `ITaskSource` 实装；仅 `ConfirmTreeGather` 创建 |
| **AI 侧（缺）** | [TaskScheduler.cs:251-262](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L251-L262) | 派工链入口＝遍历 `_sources` 调 `TryAdvertiseTask`；**无任何面向世界资源点的源** |
| | [Building.cs:932-995](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L932-L995) | `Building.TryAdvertiseTask` ①采集分支前置条件＝`def.isConsumable && isBeingGathered`，而 `isBeingGathered` **只能由玩家 UI 置位** ⇒ AI 结构性够不到 |

> `ITaskSource` 全库实装面（`Grep` 实读）：`Building`／`MineByproductComponent`／`UnitController`／`TreeGatherSource`／`DebugTaskSource`——**零个面向世界资源点且面向 AI 的源**。

### 3.2 A 归属路由缺口（A① 实施必解·非推断）

[TaskScheduler.cs:1091-1101](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L1091-L1101)：

```csharp
private int SourceKingdom(KingdomTask task)
{
    if (task == null) return 0;
    if (task.source is Building b) return b.kingdomId;
    if (task.source is Component c) { var pb = c.GetComponentInParent<Building>(); if (pb != null) return pb.kingdomId; }
    return 0;   // ← 非 Building 源恒归玩家池
}
```

⇒ `TreeGatherSource`（非 `Building`）**恒落 `return 0`＝玩家池**。世界资源点若照抄其形态，AI 国的采集任务会被路由进**玩家池**（[TaskScheduler.cs:291](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L291) 池隔离：`tKingdom >= 0 && idleKingdom[i] != tKingdom ⇒ continue`）⇒ **AI 工人永不匹配**＝通道名义落地实则僵死。
**本批解法（D685 A① 已裁「per-命令绑 `kingdomId`」）**：新包装暴露 `kingdomId`，并给 `SourceKingdom` **增一条非破坏性分支**（只多认一个接口，不动既有分支与返回语义）——**须在交付报告给出「玩家侧逐位不动」证据**。

### 3.3 A 替代通道被锁（根因之二）

| 资源 | 实读锚点 | 事实 |
|---|---|---|
| **石** | [UtilityActionConfig.cs:26](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs#L26) | ③`BuildCapacity` ＝ `axis = Belligerence`，而 `2_17 §3.1` 归「经济」、①②④⑤ 皆 `Economy` ⇒ **唯③异常**；且 `buildingId="quarry"` 需建在**矿洞节点**（全图 `mine 4`） |
| **木** | [ResourceNodeMapping.cs:6](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ResourceNodeMapping.cs#L6) | 注释「木已无产能建筑 2_12，＝一次性树」＋ `_toolToNode` 仅 `quarry→Mine`／`farm→Farmland` ⇒ AI 木＝开局存量，**无再生成通道** |

### 3.4 B 根因（轴错配·双源落点实读）

| 落点 | 实读 |
|---|---|
| `.cs` 回退表 | [UtilityActionConfig.cs:26](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs#L26) `axis = (int)PersonalityAxis.Belligerence`（注释 [:7](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs#L7)「axis=0好战1经济…」为另一处口径说明） |
| 枚举定义 | [UtilityActionConfig.cs:115](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs#L115) `Belligerence = 0, Economy = 1` |
| `.asset` 序列化值 | `Resources/Config/Kingdoms/UtilityActionConfig.asset` id3 行 `axis: 0`（第 57 行） |
| 消费点 | [UtilityScorer.cs:139-140](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L139-L140) `axis *= Mathf.Clamp01(k.personality[def.axis])` |

⇒ 单改一处必被另一处覆盖（`L-15` 双源）⇒ **两处同改**。

### 3.5 C 根因

| 项 | 实读 |
|---|---|
| 木产能缺口 | `ResourceNodeMapping.cs:6`（见 3.3）＋ `BuildingDef` 侧木类产能建筑缺席 ⇒ **只诊断立账，禁擅加建筑** |
| 审计 L35 失实 | 《全模块覆盖审计_P1文档推导层_2026-09-06》L35「AI 采集走 TaskScheduler＝战略等价·不算缺口」——**等价路径零实装**（3.1 已证）⇒ 须回写标注（`L-33` 立条由来） |
| 枯木／存档 0 | `tree`／`farmland` 属 `FeatureType`／自然格（`MapData.features` 为唯一功能源，[WorldState.cs:27](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/WorldState.cs#L27)）⇒ 不入 building 存档＝**非缺失** ⇒ 归 `doc-management` 立账 |

---

## 四、🔴 正式 `sim-sync` 核查（D685 附加把关项·**改前必须落地此结论**）

### 4.1 结论（一句话）

> **A（`KingdomBrain`／`UtilityAction`／`UtilityActionConfig`／`NeedKind` 面）＝「不触红线·可直改」**——四符号均**不在** `AI.Core` 同源镜像面内，**无需 sim 侧补实现、无需跨仓对账**。
> 附带发现（**不属本批义务，据实列报**）：`AI.Core ↔ harness/Core` 现存 **8 个 DIFF ＋ 1 个 ONLY-UNITY** 文件级差异，其中 7 个（`ProfessionSnapshot`／`TuningSnapshot`／`AttentionSystem`／`DecisionStructs`／`L2PostureDecider`／`L3CommandComputer`／`FactorContext`／`HitCooldownStateMachine`）**未在 15_账本登记**＝历史在途差距；`SafetyScoreFormulas.cs` 仅 Unity 有。**本批不改训练仓**（训练仓为独立 git 仓，须走训练师身份/流程）⇒ 已列报待裁。

### 4.2 检索证据（含 `L-33` 阳性对照）

| 项 | 实读输出 |
|---|---|
| 检索域 | `Valley Rampart/Assets/_Game/Systems/AI.Core/` ＝ **35 个 `.cs`** |
| 目标符号命中 | `UtilityScorer` **0**／`UtilityAction` **0**／`UtilityActionConfig` **0**／`NeedKind` **0**／`PersonalityAxis` **0**／`KingdomBrain` **0**／`GatherWorldResource` **0**／`NeedScore` **0**／`ITaskSource` **0**／`isConsumable` **0** ⇒ **全零命中** |
| harness 侧同查 | 仅 `harness/KingdomBrain/SituationSnapshot.cs:94` 具 `public bool EconomyBlockPlaceholder;`＝**占位 bool**（非 Unity `EconomyBlock` 类）；`SituationSnapshot` 注释里的 `KingdomBrain`／`UtilityScorer`／`NeedScore` 字样＝**文档性引用，非代码依赖** |
| **阳性对照（`L-33`）** | 同一检索通路下 `SituationSnapshot`＝**8**／`MathfX`＝**118**／`ProfessionSnapshot`＝**90**／`class`＝**156** ⇒ **检索有效，零命中是真零命中** |
| MD5 逐字比对（按相对路径） | `IDENTICAL=26`／`DIFF=8`／`ONLY-UNITY=1`／ONLY-harness=0；8 个 DIFF 与上列文件一一对应 |
| 15_账本登记面 | 仅 1 条 `SituationSnapshot` 相关登记（`:424` S-1 行）⇒ 其余 DIFF **未登记**（`DZ` 级列报） |
| champion 面（B 轴改动的训练侧影响） | `champion/*.json` 对 `axis`／`Belligerence`／`BuildCapacity`／`UtilityAction`／`actionWeight`／`needA`／`gather` **全零命中**（阳性对照 `safetyNightWeight`=1／`hv`=3／`protectThreshold`=1 有命中）⇒ **B 轴改动不触 champion／factor_registry** |

### 4.3 判定依据（为何四符号不在同源面）

依据 [sim-sync SKILL.md §二](file:///c:/Users/trs/Desktop/Valley%20Rampart/.trae/skills/sim-sync/SKILL.md) 双源架构表：同源面＝**决策核 Core**（训练侧 `harness/Core/` 为真源的 **单位级战术决策核**：`Attention/L1/L2/L3/Tuning/Memory`）。
本批四符号全住 `_Game/Systems/AI/KingdomBrain/` 与 `_Game/Data/Kingdoms/`＝**王国行动评分执行面**，MD5 表 35 文件**零交集** ⇒ **域外**。与 `HH.195`／`HH.220`／`HH.225` 同判，本批**独立复验**。

### 4.4 连带义务（据实列报，不在本批范围）

- 8 个 DIFF 文件中 7 个未登记 15_账本 ⇒ 属 `sim-sync §差距账本维护` 义务，**须训练师身份在训练仓补登记**；本次**未动**训练仓任何文件。
- `SituationSnapshot.cs` 的 `EconomyBlockPlaceholder`（sim 占位）vs Unity `EconomyBlock`（已实装）＝**已知 F 级行为差**（HH.220 批A 遗留），已登记 `15_账本:424` S-1。

---

## 五、实施设计要点（据 D685 裁示落定，实施时逐条对照）

### 5.1 A① 新类形态（照 `TreeGatherSource` 先例，禁另起炉灶）

- **契约**：实现 `ITaskSource`（`IsValid`／`SourcePos`／`TryAdvertiseTask`／`OnRegister`／`OnUnregister`）；`TryAdvertiseTask` 产 `KingdomTaskType.Gather` ＋ `GatherTaskArgs`，`destType = Treasury`（与玩家侧同口径）。
- **命名**：**必须含 `Gather`** —— `Valley_DiagMilitary.CountGatherSources()` 按类型名含 `Gather` 计数（否则 §0b 判据 1 恒 0，探针失明）。
- **归属（per-命令绑 `kingdomId`）**：构造时绑国别；`SourceKingdom` 增**非破坏性**分支识别之。
- **可达/归属判定（任务书 §一.A.4＝禁全局共享）**：以**领土判定为准**（`territory.id` 命中国）——即「本国领土内的世界资源点」；不新造第二套可达体系。
- **完成回调**：树 → `ResourceRespawnSystem.HandleTreeGathered(cell)`；一次性实体 → `Building.OnGatherCompleted()`（内部已含 `HandleEntityDepleted` 重生记账）。
- **入账**：**完全复用** `TaskScheduler.ExecuteCompletion` 的 `AddGatherOverflow`／`UnloadInventory`（工人搬运同链）——**禁免费/瞬时入账**（任务书 §一.A.3 红线）。

### 5.2 A② 决策核只出粗意图（禁 O-3 直选目标）

- `UtilityAction` 尾插 `GatherWorldResource = 27`；`NeedKind` 尾插缺口函数（缺口＝本国该资源 `Stock` 低于底线，**纯状态函数**，照 `CapacityGap` 形态）。
- `KingdomBrain.ExecuteFocus` 增 `case UtilityAction.GatherWorldResource:` ⇒ 仅**下发采集意图**（发源注册/宣告），**选点/距离/派工一律归 TaskScheduler**。
- `UtilityActionConfig.asset` 增新行 ＋ `.cs` 回退表同步增行（双源同改，防 `L-15`）。
- 单源引 `2_23` 通道B 分诊语义（避 `DZ-118/DZ-128` 重造）。

### 5.3 B 双源同改（仅轴值）

- `.cs:26` `Belligerence → Economy`；`asset:57` `axis: 0 → 1`。
- **不动** `costGold=50`／`needA=8`／`buildingId="quarry"`（`D563③` 禁参数微调找补）。

### 5.4 跑局容器（独立常量·主档逐字不动）

- 复用 **seed 64513 ＋新槽**（避开 `p1_run9`／`p1_fix1`／`p1_run8`）；窗口 **D5~D15**。
- 档位＝**本批新增独立常量**；主档 `CIRCUIT_BREAK_DAY=120`／`MILITARY_STOP_COUNT=2` **逐字不动**。
- 交付报告须显式标注「**同段 ✓／同 seed=64513 ✓／同档=独立常量 ✓**」（`L-35` 升级版）。
- 批次收尾**必退 Play**（`L-32`）；正门唯一入口 `TestHarnessApi.EnterTestRun`＋守卫全开＋**15x**（`L-09`）。

---

## 六、红线自检（开工前）

| 红线 | 状态 |
|---|---|
| `AI.Core` 零直改（先核 `sim-sync`） | ✅ 已核，结论「不触红线·可直改」（§四） |
| 枚举新增**尾插**（`L-28`） | ✅ 尾插 `27`／`NeedKind` 尾插，不改中间位 |
| 禁参数微调找补（`D563③`） | ✅ B 只改轴；`DZ-135` 只诊断 |
| 取号＝独立单行 commit（`D640 #10`） | ✅ `87bb190` |
| 写-改-commit 同串·只提本串·**不 push** | ✅ 遵守；他端 T09 行不并入 |
| 改文件前必重读磁盘 | ✅ 已重读 `UtilityActionConfig.cs`／`UtilityScorer.cs`／`TaskScheduler.cs`／`Building.cs` |
| 共享文档**增量改** | ✅ 遵守 |
| 正门唯一入口＋15x＋退 Play | ✅ 照 `L-09`／`L-32` |
| `L-33` 断言带阳性对照 | ✅ §4.2 已附 |

---

## 七、待策划确认项（**不阻塞**·D685 已授权实施，下列仅备过目）

1. **A① 新类命名**拟 `WorldGatherSource`（含 `Gather`，探针可计）——如需沿用别称请示下，我按自决执行并报。
2. **可达判定口径**拟＝**本国领土内**（`TerritorySystem` 命中）——若须叠加「距主城 N 格」，请裁；未裁则按领土口径。
3. **§四连带发现**（8 个 DIFF 中 7 个未登记 15_账本）**不在本批范围**，已在 §4.4 列报，供后续排批。

---

*执行端 2026-09-12 · 取号 `87bb190`（HH.235→HH.236）· 下一步：按 D685 批内序进入实施（A→A②→B→C 诊断），随后冒烟 → 短窗正向/负探针 → 交付报告 `HH.235` 取号前再读水位线。*
