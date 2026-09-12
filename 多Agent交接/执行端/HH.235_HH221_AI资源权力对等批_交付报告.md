# HH.235｜HH.221「AI 资源权力对等批」（Gate=`G1-1`）· 交付报告

> 类型：**交付报告（实施 ＋ 门禁 ＋ 短窗实证）** · 状态：🔴**A 实施已落地／验证结论＝「链路在册但行动结构性不可达」需报裁**
> 执行端（TraeCode·Unity 轨）· 2026-09-12 · 依据：**HH.221 任务书**（D665）＋ **D685 开工许可（6 项裁示全 A）**
> 取号：遵 D640 #10——本号已在先序 commit `f50ed4f` 取号（原拟 HH.234 撞美术端 HH.234〔CreationTime 15:28:38 先落盘〕按 `vr-id-ledger §四` 顺延 **HH.235**）；本文件落盘**不再重复取号**
> 前置：HH.236 开工回执（含正式 `sim-sync` 核查）已落盘
> 验收句对齐：**`G1-1`**（判定线＝「AI 能取到世界一次性资源 ≥1 通道在产」）

---

## 〇、结论速览（先看这里）

| 轴 | 交付 | 状态 |
|---|---|---|
| **A①** 世界资源点采集源落地 | `WorldGatherSource` ＋ `WorldGatherRegistry` ＋ `WorldGatherConfig`（SO/asset）＋ `TaskScheduler` 两落点 ＋ `WorldLifecycle` 跨轮清场 | ✅**代码落地·编译 0 错** |
| **A②** 决策出口 | `UtilityAction` 尾插 `GatherWorldResource=27` ＋ `NeedKind` 尾插 `GatherShortageGap` ＋ `UtilityActionConfig.asset` 新行 ＋ `UtilityScorer` 三处 ＋ `KingdomBrain.ExecuteWorldGatherFocus` | ✅**代码落地·编译 0 错** |
| **B** ③轴纠正 | `.cs:26` ＋ `.asset:57`（id3）双源同改 `Belligerence→Economy` | ✅**双源一致** |
| **C** 诊断 | 木产能缺口／审计 L35 回写／枯木与存档 0 三件（只诊断报裁） | ✅**见 §四** |
| **门禁①** 编译 | `refresh_unity` ＋ `read_console` filter `error CS` | ✅**0 条** |
| **门禁②** 冒烟回归 | `Smoke_2_22P0` run1 **PASS=32 FAIL=0**；`Smoke_2_23RP0` run1 **PASS=27 FAIL=0**；`Valley2_17_Smoke_5` ALL PASS | ✅**零回归** |
| **短窗实证** | seed 64513／槽 `p1_gather1`／D5~D15 短窗档 | 🔴**D3 命中断损（详见 §五）** |

> **一句话**：A/B 的**代码与评分面全部到位**，但短窗跑实测 **㉗ 采集行动在自然跑局中结构性不可达**（入口三重门 ＋ ③建产能金门死锁，与 `DZ-135` 同根）⇒ `G1-1` 判定线**未 PASS**。已按"直接报裁"口径交付，不补夹具。

---

## 一、红线自检

| 红线 | 自检 |
|---|---|
| 正门唯一入口 `TestHarnessApi.EnterTestRun`＋野怪守卫＋15x（`L-09`） | ✅ 短窗跑全程走 `EnterTestRun`（日志 `[TimeManager] 考跑模式 ON：timeScale 直通 → 15x`） |
| 批次必退 Play（`L-32`） | ✅ 冒烟／短窗跑收工均 `ExitPlaymode`；本会话末态 `playing=False` |
| `AI.Core` 零直改 | ✅ 全程未碰 `Assets/_Game/Systems/AI.Core/`；HH.236 §四 已出正式 `sim-sync` 核查＝**不触红线·可直改**（含阳性对照） |
| 枚举新增**尾插**（`L-28`） | ✅ `GatherWorldResource=27`／`GatherShortageGap` 均尾插，中间位零改动 |
| 禁参数微调找补（`D563③`） | ✅ 未动 `costGold`／`needA`／`buildingId`；B 轴**仅改轴值**（A 裁原文） |
| 取号＝独立单行 commit（`D640 #10`） | ✅ 本号先序已原子取号（`f50ed4f`），本轮不再取号 |
| 写-改-commit 同串·只提本串·不 push | ✅ 见 §七 commit 清单；未 push |
| 改文件前必重读磁盘 | ✅ 每处 SearchReplace 前重读 |
| 共享文档**增量改** | ✅ 仅改 `_编号登记.md` 水位行（他人行未动） |
| 禁绕过"劳动"（任务书 §二） | ✅ A① 走 `ITaskSource`→`TaskScheduler` 派工链（同玩家）；**无**"领土覆盖即入账"免费通道 |
| 先诊断后修（D648/D649） | ✅ 根因取证见 HH.236 §三 |

---

## 二、实施清单（含 `file:line` 锚点）

### 2.1 A① 世界资源点采集源（新建 3 件 ＋ 改 3 件）

| 件 | 锚点 | 说明 |
|---|---|---|
| 新建 SO | [WorldGatherConfig.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/WorldGatherConfig.cs) | `enabled`／`maxSourcesPerKingdom=6`（`so-data-driven`） |
| 新建 asset | [WorldGatherConfig.asset](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Resources/Config/WorldGatherConfig.asset) | 经 `AssetDatabase.CreateAsset` 生成（规避手工写 GUID 风险） |
| 新建源 | [WorldGatherSource.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/WorldGatherSource.cs) | 实装 `ITaskSource`；**per-命令绑 `KingdomId`**；两静态工厂 `ForTree`（取 `RespawnConfig` 同源参数）／`ForEntity`（取 `BuildingDef.gatherSeconds`）；完成回调分派 `HandleTreeGathered`／`Building.OnGatherCompleted` |
| 新建选点层 | [WorldGatherRegistry.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/WorldGatherRegistry.cs) | `Advertise`／`HasCandidate`／`TryMapWorldResource`／`TryMatchPoint`；领土集遍历=**确定性**（按 `(x,y)` 排序） |
| 改·归属路由 | [TaskScheduler.cs:1100](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L1100) | `SourceKingdom` 加非破坏性分支 `if (task.source is WorldGatherSource wg) return wg.KingdomId;`（防 AI 任务落玩家池僵死） |
| 改·完成回调 | [TaskScheduler.cs:634](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs#L634) | `ExecuteCompletion` Gather 分支加 `else if (task.source is WorldGatherSource wg) wg.OnGatherCompletion();` |
| 改·跨轮清场 | [WorldLifecycle.cs:53](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Loading/WorldLifecycle.cs#L53) | `ResetAll()`（防跨轮污染） |

**与 `TreeGatherSource` 唯一有意差异**：`TreeGatherSource` 广告后立即 `IsValid=false`，但 `TaskScheduler.UpdateAssignedTasks:388` 有 `!task.source.IsValid ⇒ Abandon` ⇒ 会当场自弃任务。本类**保持有效直至完成**，去重交调度器 `HasAssignedTaskForSourceType`。

### 2.2 A② 决策出口

| 件 | 锚点 | 说明 |
|---|---|---|
| 枚举尾插 | [UtilityScorer.cs:50](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L50) | `GatherWorldResource = 27` |
| NeedKind 尾插 | [UtilityScorer.cs:90](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L90) | `GatherShortageGap` |
| NeedScore | [UtilityScorer.cs:317](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L317) | **单源**调 `KingdomBrain.ResolveTriageResource`＋`DecideTriage`＋`WorldGatherRegistry.TryMapWorldResource`（仅通道B；避 `DZ-118`／`DZ-128` 双源） |
| Feasible | [UtilityScorer.cs:626](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L626) | 硬门槛＝`WorldGatherRegistry.HasCandidate(k.id, rtF)`（选点层同源单函数，禁另抄 `L-31`） |
| 可见性放宽 | [KingdomBrain.cs:1404](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1404) | `ResolveTriageResource` `private → internal static`（行为/语义逐字不变） |
| 执行出口 | [KingdomBrain.cs:1334-1373](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1334-L1373) | `ExecuteWorldGatherFocus`：只出粗意图→调 `WorldGatherRegistry.Advertise`；失败分型（正常态 `Bump ok`／`Env` 报退避） |
| 配置表（`.cs`） | [UtilityActionConfig.cs:49](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs#L49) | id27 行尾插 |
| 配置资产 | [UtilityActionConfig.asset:510-528](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Resources/Config/Kingdoms/UtilityActionConfig.asset#L510-L528) | `id: 27`／`need: 23`／`axis: 1`／`minStage: 0`／`stageWeight: 1,1,1,1` |

### 2.3 B ③轴纠正（双源同改，防 `L-15`）

| 源 | 锚点 | 改前 | 改后 |
|---|---|---|---|
| 回退表（`.cs`） | [UtilityActionConfig.cs:26](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/Kingdoms/UtilityActionConfig.cs#L26) | `Belligerence` | `Economy` |
| 序列化（`.asset`） | [UtilityActionConfig.asset:57](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Resources/Config/Kingdoms/UtilityActionConfig.asset#L57) | `axis: 0` | `axis: 1` |

> **否 C**（`DZ-104` 仍挂账，不搭车治本）＝D685 裁③原文。

### 2.4 判据挂容器（短窗档专属）

| 件 | 锚点 | 说明 |
|---|---|---|
| 新 `JudgeKind` | [Valley_DiagMilitary.cs:27](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs#L27) | `ChannelAbsent`／`GatherStall`／`WorldGatherIncome`（尾插） |
| 计数面 | [Valley_DiagMilitary.cs:98-101](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs#L98-L101) | `CountGatherSources()`（类型名含 `Gather`）／`CountByproductSources()`（口径分离） |
| 判据实现 | [Valley_DiagMilitary.cs:72-87](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs#L72-L87) | 三条 hard gate |
| 短窗档常量 | [Valley_HH80_Run.cs:45-52](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L45-L52) | `GATHER_SEED=64513`／`GATHER_SLOT="p1_gather1"`／`GATHER_CIRCUIT_DAY=15`；**主档 `CIRCUIT_BREAK_DAY=120`／`MILITARY_STOP_COUNT=2` 逐字不动** |
| 判据挂载 | [Valley_HH80_Run.cs:119-127](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH80_Run.cs#L119-L127) | `if (USE_GATHER_WINDOW)` 内挂三条（**不进主档**：修前 `chSrc` 恒 0 ⇒ 无条件启用会在 D3 停掉任何长局） |

---

## 三、门禁实测

### 3.1 门禁① 编译

- `refresh_unity` → `read_console(types=[error])` ⇒ 仅 1 条 `233 node options failed to load and were skipped.`（`com.unity.visualscripting` 环境噪声，与本批改动无关，全程存在）。
- `utility_reflect`／`execute_code` 验：`UtilityAction.GatherWorldResource=27`、`NeedKind.GatherShortageGap=23`、`WorldGatherSource`／`WorldGatherRegistry` 类型在册、`UtilityActionConfig n=27, id3.axis=1, id27.need=GatherShortageGap`。

### 3.2 门禁② 冒烟回归（零退化）

| 容器 | tag | 结果 | 收工时刻 |
|---|---|---|---|
| `Smoke_2_22P0`（王国AI P0 九项） | run1 | **PASS=32 FAIL=0** | 17:36:55 |
| `Smoke_2_23RP0`（资源P0 批C） | run1 | **PASS=27 FAIL=0** | 17:18:33 |
| `Valley2_17_Smoke_5` | — | ALL PASS | （早前） |

> **取证方式修正（自曝）**：`read_console` 的 `filter_text` 返回缓冲区**最早**匹配（非最新），初判时据此误以为"冒烟未收工"。后改**反射直读容器静态字段**（`_pass`／`_fail`／`_log` 尾段）取权威值。此坑已记，供后续批规避。
> 污染澄清：该误判期间我曾误调 `EnableTestHarness(15f)` 想"救进度"，属运行态误操作，已随 `ExitPlaymode` 清除，**未落盘**。

---

## 四、C 件诊断（只诊断＋报裁，D685 裁④）

### 4.1 木产能缺口（`DZ-110`）

| 锚点 | 事实 |
|---|---|
| [ResourceNodeMapping.cs:6](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ResourceNodeMapping.cs#L6) | 注释原文：「木已无产能建筑 2_12，=一次性树」；映射表只有 `quarry↔Mine`／`farm↔Farmland`，**无木** |
| [2_12_王国建筑系统迁移.md:39](file:///c:/Users/trs/Desktop/Valley%20Rampart/%E6%B2%B3%E8%B0%B7%E9%98%B2%E7%BA%BF%E5%BC%80%E5%8F%91%E8%AE%A1%E5%88%92%E4%B9%A6%E5%85%B7%E4%BD%93%E5%86%85%E5%AE%B9/%E6%94%B9%E9%80%A0%E8%AE%A1%E5%88%92/2_12_%E7%8E%8B%E5%9B%BD%E5%BB%BA%E7%AD%91%E7%B3%BB%E7%BB%9F%E8%BF%81%E7%A7%BB.md#L39) | 「**木头 = 只有一次性树木**……伐木场作废——从建筑清单/美术表/代码资产中移除，**无可持续木材产能建筑**」 |
| [2_12_王国建筑系统迁移.md:186](file:///c:/Users/trs/Desktop/Valley%20Rampart/%E6%B2%B3%E8%B0%B7%E9%98%B2%E7%BA%BF%E5%BC%80%E5%8F%91%E8%AE%A1%E5%88%92%E4%B9%A6%E5%85%B7%E4%BD%93%E5%86%85%E5%AE%B9/%E6%94%B9%E9%80%A0%E8%AE%A1%E5%88%92/2_12_%E7%8E%8B%E5%9B%BD%E5%BB%BA%E7%AD%91%E7%B3%BB%E7%BB%9F%E8%BF%81%E7%A7%BB.md#L186) | D2 决策行：「伐木场处置｜**已定**：作废。Tree 一次性可刷新替代（2_1）」 |
| [RespawnConfig.cs:32-36](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Data/RespawnConfig.cs#L32-L36) | `treeGatherSeconds=2f`／`treeGatherAmount=5`／`treeRespawnDays=7f`（树可刷新＝有再生长） |

**结论**：木「无产能建筑」是 **2_12 有意取舍（D2 已定）**，不是缺口——树＝**一次性可刷新**（`treeRespawnDays=7`），语义上等价于"可再生但需劳动采集"的资源。**故 `DZ-110` 的"缺口"定性可下调**；真正的缺口在** AI 无采集通道**（本批 A 轴），而非"没有伐木场"。建议报裁时同步修订 `DZ-110` 措辞。

### 4.2 审计 L35 回写 diff（`DZ-111`）

**文件**：[全模块覆盖审计_P1文档推导层_2026-09-06.md](file:///c:/Users/trs/Desktop/Valley%20Rampart/%E6%B2%B3%E8%B0%B7%E9%98%B2%E7%BA%BF%E5%BC%80%E5%8F%91%E8%AE%A1%E5%88%92%E4%B9%A6%E5%85%B7%E4%BD%93%E5%86%85%E5%AE%B9/%E6%8A%A5%E5%91%8A/%E8%AE%BE%E8%AE%A1%E5%AE%A1%E6%9F%A5/%E5%85%A8%E6%A8%A1%E5%9D%97%E8%A6%86%E7%9B%96%E5%AE%A1%E8%AE%A1_P1%E6%96%87%E6%A1%A3%E6%8E%A8%E5%AF%BC%E5%B1%82_2026-09-06.md#L35) 第 16 行

| | 内容 |
|---|---|
| 原文（L35） | `| 16 | 采集确认 | 一次性资源点交互（IClickInteractable 轻量通道） | ... | AI 采集走 TaskScheduler | 战略等价——不算缺口 | — |` |
| 问题 | 「AI 采集走 TaskScheduler」被当作**在场事实**做等价判定，但 **`Grep` 实读＝该通道零实装**（`TreeGatherSource` 仅 `ConfirmTreeGather` 创建，而后者全库 0 调用者）⇒ **以"他路等价"判"不算缺口"，未验他路在场** |
| 建议回写 | 「战略等价——不算缺口」→「**实装缺口**（AI 侧采集源零在场）⇒ 归 **HH.221** 落地」 |

> **本批只出 diff 建议，未直接改审计文档**（改设计/审计文档＝执行端红线外，归策划端 `doc-management` 流程，D685 裁④"只诊断＋报裁"）。

### 4.3 枯木／存档 0 核文档（`DZ-110` 邻）

| 疑点 | 取证 | 结论 |
|---|---|---|
| 「枯木」资产零命中 | Grep「枯木」仅命中 [3.1.1_美术资源清单.md:360-366](file:///c:/Users/trs/Desktop/Valley%20Rampart/%E6%B2%B3%E8%B0%B7%E9%98%B2%E7%BA%BF%E5%BC%80%E5%8F%91%E8%AE%A1%E5%88%92%E4%B9%A6%E5%85%B7%E4%BD%93%E5%86%85%E5%AE%B9/3.1.1_%E7%BE%8E%E6%9C%AF%E8%B5%84%E6%BA%90%E6%B8%85%E5%8D%95.md#L360-L366)＋[美术资源接入映射表.md:239](file:///c:/Users/trs/Desktop/Valley%20Rampart/%E6%B2%B3%E8%B0%B7%E9%98%B2%E7%BA%BF%E5%BC%80%E5%8F%91%E8%AE%A1%E5%88%92%E4%B9%A6%E5%85%B7%E4%BD%93%E5%86%85%E5%AE%B9/%E6%94%B9%E9%80%A0%E8%AE%A1%E5%88%92/%E7%BE%8E%E6%9C%AF%E8%B5%84%E6%BA%90%E6%8E%A5%E5%85%A5%E6%98%A0%E5%B0%84%E8%A1%A8.md#L239) | 文档在册（D659 决策2 改名 `feat_wood_pile`→`feat_deadwood`，**标 ⬜ 待接入**）⇒ **属美术接入批未兑现**，非丢失 |
| `tree`／`farmland`／`treasure_box` 存档 0 | `PlaceholderSprites.cs:31` 三者**全为占位 sprite key**；`BuildingDef.cs:87` `isConsumable` 工具注释明示「false=**Tree**/Mine/**Farmland**」；`Farmland/TreasureBox` 在 `GridTypes.cs:150/156` 为 **`FeatureType`/`BuildingType` 枚举**；`WorldManager.cs:248` 注释「A+ 下由 **features 数据判定**，非 Building 实体」 | ✅**属 feature（数据格），不入 building 存档＝非缺失**（`doc-management` 立账即可，非缺陷） |

---

## 五、短窗实证（`G1-1` 判定线）＝🔴**未 PASS**

### 5.1 跑局配置（基线三标注，`L-35` 升级版）

| 项 | 值 | 标注 |
|---|---|---|
| 段 | D5~D15（`GATHER_CIRCUIT_DAY=15`） | **同段 ✓**（与 HH.220／HH.224 对照段一致） |
| seed | **64513** | **同 seed ✓**（任务书裁⑤指定） |
| 档 | 独立常量 `GATHER_*`（主档 120/2 逐字不动） | **同档＝独立常量 ✓** |
| 槽 | `p1_gather1` | 新槽（裁⑤） |
| 入口 | `TestHarnessApi.EnterTestRun`＋15x | ✅ |

### 5.2 实测结果

**状态档** `Logs/P1/hh80_run_status.log`：

```
判据命中：ChannelAbsent（k1 世界资源点采集源注册数=0（通道未落地；副产源=4 不计））@D3
终速=0 存盘 p1_run9=True 时间=18:49:55
```

**逐日取证**（日志 `Logs/P1/p1_log_20260912_184557.log`）：

| 读数 | D2 | D3 | 判读 |
|---|---|---|---|
| `chSrc=0/4` | ✅ | ✅ | 采集源 **恒 0**（4 为矿洞副产源，口径已分离） |
| `Stone:prod=0/in=0` | ✅ | ✅ | k1~k4 **石链全死** |
| `㉗采集世界资源点 need=0.000` | ✅ | ✅ | **缺口语义值恒 0** |
| `㉗ ... feasible=False` | ✅ | ✅ | 硬门槛亦假 |
| `DiagTriage ... Stone=BuildCapacity` | ✅ | ✅ | 四国皆指③建产能（非㉗） |
| `③建产能 feasible=False`（cost 50 金 vs 国库 3~8） | ✅ | ✅ | **金门**（`DZ-135` 实证复现） |

### 5.3 根因链（`file:line`，结构性不可达）

```
㉗ need 唯一入口 = ResolveTriageResource（断供触发）+ DecideTriage==NoOp（通道B）
   ↓  KingdomBrain.cs:1342-1352
① ResolveTriageResource：各资源 Net≥0、粮储备充足 ⇒ 恒返回 -1
   ↓  KingdomBrain.cs:1404-1425
   ⇒ ㉗ need 恒 0（全批四国全部 D2/D3 实测 0.000）
② 即便触发石：CountProductionOf(Stone)==0 ⇒ DecideTriage 恒走通道A（BuildCapacity）
   ↓  KingdomBrain.cs:1383-1386
   ⇒ ㉗ 让位（ExecuteWorldGatherFocus 第 1348 行提前 return）
③ 通道A 出口 ③建产能 feasible=False（costGold=50 ≫ 国库 3~8）
   ↓  UtilityActionConfig.asset:68 + 日志 D2/D3 实测
   ⇒ 建不了 quarry ⇒ 石产能恒 0 ⇒ 通道A 永久成立 ⇒ 通道B 永不可达（**死锁闭环**）
```

**定性**：这不是实现缺陷（编译/评分面/硬门槛同源均在册，夹具必能点亮），而是 **㉗ 的入口条件在自然跑局中永不成立**。这条链把 **`D665`（AI 无采集通道）** 与 **`DZ-135`（金成本门）** 两层**串成了同一根因**：补了采集通道，若金起不来，③ 仍不可行（`DZ-135` 原文预判已成真）。

### 5.4 附带发现：§0b 判据1「可判定最早日 D1~D3」**口径错误**

| 项 | 内容 |
|---|---|
| 判据 | 「世界资源点源注册数 ≥1｜可判定最早日 **D1~D3**｜未注册 ⇒ 判『通道未落地』并停」（任务书 §0b 第 1 行） |
| 缺陷 | 该口径**默认㉗开局即活跃**；实测㉗开局**不可能活跃** ⇒ 该判据**以既有常态误停**，与 `D678` 记录过的 J2 口径缺陷（HH.226 第 2 项）**完全同族** |
| 建议 | ① 修正可判定最早日（须等断供触发，非固定 D1~D3）；② 或改为「**须先验入口可达**（③金门闭 ⇒ 本判据不适用）」，否则任何长局都会在 D3 被截断（`L-34` 要防的正是此） |

### 5.5 自曝缺陷

| # | 缺陷 | 状态 |
|---|---|---|
| 1 | **短窗档污染 ②七考重验定案槽**：`Finish()` 原写死 `SaveManager.Save(SLOT)`，`USE_GATHER_WINDOW=true` 时仍存 `p1_run9` ⇒ 实测 `p1_run9.json` 被覆盖（18:49:55，与 `p1_gather1` 同刻同大小） | ✅**已修**（改 `ActiveSlot`）；⚠️**`p1_run9.json` 已被覆盖**，`p1_run9_day080/085/090.json` 检查点未受影响（14:5x 时戳） |
| 2 | 误判冒烟"未收工"并误加 `EnableTestHarness(15f)` | ✅ 随 `ExitPlaymode` 清除，未落盘 |
| 3 | §0b 判据1 口径错误（§5.4） | ⏳报裁 |

---

## 六、验收句对齐 `G1-1`

| 任务书 §三 分项 | 状态 | 证据 |
|---|---|---|
| §1 ① `ITaskSource` 注册数（对照修前=0） | ❌**未达成** | 短窗实测 `chSrc=0`（与修前同位）；代码在册但入口不可达（§5.3） |
| §1 ② `Gather` 派发数 ≥1 | ❌**未达成** | 同上（源未立案 ⇒ 无派发） |
| §1 ③ `HarvestCarry` 入账数 | ❌**未达成** | `Stone in=0` |
| §1 ④ 短窗实读（D5~D15） | ⚠️**D3 止损** | 判据命中即停（§5.2） |
| §1 负探针（关开关/无领土资源 ⇒ 不入账） | ⏳**未跑**（按 D685 批内序应收尾，本批按"直接报裁"口径不补） |
| §2 B 实证（③ axis=Economy 实读 + 回归冒烟） | ✅**达成** | `asset:57 axis:1`＋`.cs:26` 双源一致；冒烟零退化（§3.2） |
| §3 C 证据 | ✅**达成** | §四（木产能／L35 diff／枯木存档） |

> **§0 `L-30` gap 表 5 行**：第 1~2 行（AI 取到世界资源／k1 型石入库）**未闭**；第 3 行（③进池可竞争）**已闭**（B 轴）；第 4 行归七考；第 5 行（③可达性）**实证复现 `feasible=False`**（`DZ-135` 证据加深）。

---

## 七、commit 清单（只提本串）

| commit | 内容 |
|---|---|
| `f50ed4f` | 取号 HH.235（撞号顺延处置）——**先序已提** |
| `87bb190` | 取号 HH.236（开工回执）——**先序已提** |
| （本轮待提） | A①／A②／B 实施 ＋ 判据挂容器 ＋ `ActiveSlot` 修复 |

> 未 push（红线）。

---

## 八、请裁项（4 项）

1. **`G1-1` 判定线口径**：A 的"通道在产"在自然跑局中不可达（§5.3 三重门）。是否接受「**能力/事件级夹具实证**」作为 `G1-1` 的替代判据（任务书 §三.1 标题本就写"能力/事件级"），还是判本批**结构性阻塞**、转 `DZ-135`／新批同解？
2. **`DZ-135` 金门**：实测复现 `feasible=False`（cost 50 vs 国库 3~8）；A 补了采集通道仍不够（金起不来 ⇒ ③ 仍不可行）⇒ 是否**须与 A 同批**解金门（如采集入账走石/木折金，或降 `costGold`）？
3. **§0b 判据1 口径**：建议修正"可判定最早日"或加"入口可达性前置"（§5.4）；请裁。
4. **`DZ-110` 定性**：木「无产能建筑」经实读＝2_12 有意取舍（D2 已定，树可刷新 7 日），非缺口 ⇒ 是否修订措辞？（§4.1）

---

## 九、后续归口

- **A 轴治本**：须先解"㉗ 入口可达"（含金门），归 **新批／`DZ-135` 批**。
- **C 件回写**：审计 L35（§4.2）＋`DZ-110` 措辞（§4.1）归 **策划端 `doc-management`**。
- **本批不重跑七考**、`P-005` 不在本批（D685 裁⑥）。

---

*执行端（TraeCode）2026-09-12 · 依据 D665／D685 · 门禁① ✅／门禁② ✅／短窗 🔴未达标 · 待策划端裁决*
