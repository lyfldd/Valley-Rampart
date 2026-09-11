# HH.195｜建军链修复批（⑦↔⑰ 死锁链修复）· 开工回执

> 执行端（TraeCode·Unity 轨）· 2026-09-11
> 状态：🟡开工中 —— **按批序①「开工回执 →（停手待策划确认）→ 实施」：本信落盘后停手，待策划确认再施工**
> 锚点：**HH.194 任务书（D655／0.6 §一百八十四）**为唯一权威 ｜ 根因＝HH.193 §十三（**D654**）｜ 设计稿＝`2_22` L36/L106
> 取号：遵 D640 #10 原子化（账本水位线 HH.194→**HH.195**，独立单行 commit `264cf5e`）；完成报告届时按水位线另取（禁预留）

---

## 一、🔴 sim-sync 核查结论（本批第一件事，任务书 §首）——**不在 sim 镜像内，可直改，不停手报裁**

| 核查项 | 证据 |
|---|---|
| **AI.Core 域内容** | `Assets/_Game/Systems/AI.Core/` 实盘目录＝**单位级战术决策核**：`Decision/`（AttentionSystem／L1FocusEvaluator／L2PostureDecider／L3CommandComputer／LinearThreatFormula…）＋`Formation/`＋`Memory/`＋`Ports/`＋`Shim/`＋`Stimulus/`＋`Config/`（TuningSnapshot／ProfessionSnapshot）——**无王国脑效用评分层** |
| **符号检索（L-29 阳性对照）** | 对 `AI.Core/` 全目录 grep `UtilityScorer\|ExclusiveGap\|RecruitArmy\|KingdomBrain\|NeedScore` ⇒ **零命中**；**阳性对照**＝同 pattern 在 `Systems/AI/KingdomBrain/` 命中（UtilityScorer.cs/KingdomBrain.cs 均在）⇒ 检索有效 |
| **域分离** | 本批目标文件＝`Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs`＋`KingdomBrain.cs`（**王国脑层**），与 `AI.Core`（asmdef 独立域）**不同程序集** |
| **先例** | 2_23 批C 直改 `KingdomBrain.cs`（ExecuteGrainTriage 等）同样以「`git status AI.Core`＝空」过门禁，D643/D645 验收通过 ⇒ 王国脑层从未属 sim 镜像同步面 |
| **sim-sync 边界注记** | sim-sync 义务面＝AI.Core 双份拷贝/champion/factor_registry/SO 三方同步＋「Unity **新增决策输入** 的 sim 补实现」。本批**不新增 AI.Core 决策输入**（改的是王国脑效用评分的 need/Feasible 与观测域槽位）；**R-C5 契约（HH.180 §六）为经济诊断块**（EconomyInput 五元＋三通道），不含军事评分公式 ⇒ 本次无契约同步义务。**若策划端认为军事评分未来需入训练仓 sim 镜像，请另立件（执行端不代提 15_账本）** |

**结论：主仓直改合法，不走 sim-sync 跨仓。**

## 二、修什么（复述 D654 三裁＋实现设计）

### 主修 1：⑰ need 改真实缺口（弃 `ExclusiveGap` 占位 0.5，对齐设计稿 L106「选型=种族＋快照缺口」）

**实现方案（候选 A／B，请裁，见 §四-1）**：

- **共同部分（两案同）**：`NeedKind` **尾部追加** `MilitaryBuildingGap`（**L-28 排雷：禁改中间位**——`ExclusiveGap` 等 enum 位置不动，SO 序列化安全）；`UtilityActionConfig.asset` 中 **id 23（建兵营）／id 24（建训练营）的 `need: 17 → 新枚举序号`**（**资产字段改动＝配置面，属机制修复载体，非"参数找补"**——need 类型是机制口径，非数值调优）。`UtilityActionDef` 无需扩字段（用既有 `d.buildingId` 判型）。
- **案 A（推荐）＝「被阻塞的军事缺口」驱动**：`MilitaryBuildingGap(k, d)` ＝ 若该 `buildingId` **已在本国在场（CountActiveDef≥1）→ 0**；否则 ＝ **该建筑解锁的训练域所对应的军事缺口最大值**（GeneralGap／FormationGap／UnitTypeGap 三缺口现值取 max，同源读 `SituationHub` 快照）＋ `InternalDrive(k)`（与既有三军事缺口 case 同构，D590 内源势能口径——无袭扰环境不恒死滞）。
  - 优点：**忠实 L106「快照缺口」**——有军事缺口且缺建筑 ⇒ ⑰ need→高（压过 wall）；无缺口 ⇒ ⑰=0 不乱建；与批B「两行动自然咬合」的设计意图**双向成立**（⑰ 为 ⑦/⑯ 的前置承接）。
  - 缺点：实现稍重（需 buildingId→训练域映射：Barracks→General/Warrior/Cavalry 域，TrainingCamp→Archer/Mage/Healer+本族专属域——从 `TrainingConfig` 现读，不加新 SO）。
- **案 B（简化）＝「缺建筑」二值缺口**：缺该建筑 → need=1.0（含 `InternalDrive`），在场 → 0。
  - 优点：最小机制。
  - 缺点：**无军事缺口时也会建兵营**（重建后闲置面），与「欲望与容量分离」（HH.32 裁2 A′ 同族原则）略有张力。
- **执行端建议＝案 A**（若裁 A，回执即含实现细节，无二义）。

### 辅修 2：⑦ `Feasible` 镜像建筑前置（策划端裁：须镜像）

- [UtilityScorer.cs:412-419](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L412-L419) 的 `RecruitWarrior` case 追加：**存在任一可训军事训练建筑**（`FindKingdomBuilding(k.id, …)` 命中 Barracks／TrainingCamp／本族专属营——与 `ExecuteRecruitArmy` [L913](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L913) 同源判定）⇒ true；全缺 ⇒ **false**（让位给 ⑰）。
- 口径注记：镜像后 ⑦ 在无建筑期**不再被选中**（census top 将转移到 ⑰/其它）＝**消除空转**（D654 裁意）；`ExecuteRecruitWarrior`（L854 死码）**本批顺带删除或保留？→ 建议保留不动**（死码清理非本批范围，避免扩大 diff 面——列报请裁）。

### 并入 3：观测器 `MainSlot` 参数化（修槽覆盖事故，禁再硬编码 `p1_run7`）

- **方案**：`P1Observer` 新增 `public static void SetMainSlot(string slot)`（含空值守卫）＋ `MainSlot` 改为**静态字段（非 const）**；`Checkpoint()`／回存全部改读字段。
- **各容器显式传槽**（观测器自身不再持有业务槽位缺省）：
  - `Valley_HH80_Run.Run()` 开头：`P1Observer.SetMainSlot(SLOT)`（七考容器既有 `SLOT="p1_run7"` 常量＝**容器级配置**，D647 裁 B′ 已允许）；
  - `Valley_HH80_DiagRun.Run()` 开头：`P1Observer.SetMainSlot(SLOT)`（`p1_diag`）；
  - **fail-fast 加固**：观测器 `Checkpoint()` 若 `MainSlot` 为空 ⇒ **告警跳过检查点**（不再误写未知槽）。
- 未设置时行为＝不写档＋`[P1观察]` 告警（**宁可缺检查点，不可覆盖错误槽**——本次事故的直接教训）。

## 三、回归探针设计（验收句兑现方案）

| 项 | 设计 |
|---|---|
| 探针载体 | 复用 `Valley_HH80_DiagRun` 容器（改 `SLOT="p1_diag2"` ＋ `SEED` 复用 **48903**——同 seed 对照诊断局基线＝「修复前：兵营 0／⑦落地 0／warrior 0」；**L-29**：seed 已验证非历史/冒烟局） |
| **判定句（L-31 双向）** | ① **⑰ 会被选中**：镜像日志 `[DiagMilitary]` census `top=BuildBarracks/BuildTrainingCamp` **≥1 次** ＋ `建造焦点落地：Barracks\|TrainingCamp` **>0**；② **⑦ 不空转**：`⑦招战士落地` **>0**；③ **warrior>0**：CSV 末行任一 AI `warrior≥1`。**三者全中＝PASS**（行为级，非"没报错"） |
| 窗口 | 30 日（⑰ minStage=Expand=D6 起；k3 石料富余 1294 ⇒ 成本门可达；k1 `Stone:prod=0` 属**另一结构问题**＝HH.193 §七-3 已列报，本批**不处理**——探针判定按**任一 AI** 聚合，避免石枯竭国误判） |
| 确定性 | 同 seed **双跑**（run1/run2，每轮 **L-22 退 Play 重进**）归一化逐行一致 |
| 零退化 | `Smoke_2_22P0`（32/0 基线）＋ `Smoke_2_23RP0`（27/0×2 基线）全过（后者的 P6 探针走 `CountProductionOf/DecideTriage`，与本次改动面正交但仍回归） |
| 正门 | `EnterTestRun`＋15x 缺省（禁直接设 timeScale，L-09）；观测器＋诊断探针双 fail-fast 在场 |

## 四、口径异议 / 请裁（2 项）

1. **⑰ need 案 A／案 B**（§二-主修1）：执行端建议 **案 A**（被阻塞的军事缺口驱动，忠实 L106）；若裁 B 请明示（实现更简但无缺口也会建）。
2. **`ExecuteRecruitWarrior`（L854 死码）处置**：建议**本批保留不动**（死码清理扩大 diff 面，另立散账）；若裁"顺带删"请明示。

## 五、红线自检

- ✅ **禁参数微调找补**（D563③）：不改 `warriorsMin`/stageWeight/axisWeight 等任何数值；need **枚举类型**与 asset `need:` 字段指向＝机制口径（§二 已论证，如策划端认为 asset 改动需另行定性请裁）
- ✅ **AI.Core 零直改**（§一 核查：不在镜像内；施工后仍验 `git status AI.Core`＝空）
- ✅ **L-31**：回归探针双向（⑰ 被选中 ＋ ⑦ 不空转）双验
- ✅ **L-29**：seed 与取号均带阳性对照；**L-17** 正门；**L-22** 双跑退 Play
- ✅ 观测域增量（MainSlot 参数化）**列报改动面**
- ✅ 写-改-commit 同串、**显式路径 add（禁 `git add -A`）**、不 push、收尾退 Play

## 六、门禁（交付前置）

编译 0 错＋零新警 ＋ 回归探针三句全中（双跑一致）＋ `Smoke_2_22P0` 32/0 ＋ `Smoke_2_23RP0` 27/0 ＋ `git status AI.Core` 空 ＋ `git status Assets` 仅本批文件。

## 七、批序与停手声明

**开工回执（本信，HH.195）→ 停手待策划确认（§四 两项）→ 实施 → 交付报告（按水位线取号，含回归探针证据）→ 策划端验收**。

**⇒ 本信落盘后停手，等策划端确认（尤其 §四-1 need 机制口径）后开工。**