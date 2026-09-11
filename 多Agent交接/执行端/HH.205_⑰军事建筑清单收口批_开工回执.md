# HH.205｜⑰ 军事建筑清单收口批 · 开工回执

> 执行端（TraeCode·Unity 轨）· 2026-09-11
> 状态：🟡开工中 —— **按批序①「开工回执 →（停手待策划确认）→ 实施」：本信落盘后停手，待策划确认再施工**
> 锚点：**HH.204 任务书（D657／0.6 §一百八十六）**为唯一权威 ｜ 依据＝D657 裁②④（HH.202 §四 实证）｜ 设计稿＝`2_22` **B3**/L106 ｜ 缺陷台账＝**DZ-103**
> 取号：遵 D640 #10 原子化（账本水位线 HH.204→**HH.205**，独立单行 commit `0fa7004`）；完成报告届时按水位线另取（**禁预留**）

---

## 一、复述范围（HH.204 §一，三件）＋ 死码行号勘正

| # | 内容 | 实盘锚点 |
|---|---|---|
| 1 | 三军事向专属营 asset `need: 17 → 22` | id18 `WarAcademy`（**need L344**）／id19 `WarCamp`（**need L363**）／id21 `ArcheryRange`（**need L401**）；同口径基线＝id23 `Barracks` L439=22、id24 `TrainingCamp` L458=22 |
| 2 | `LeyForge`（id20，经济向）**禁动** | asset L377-395，need **L382=17** 保持不变 |
| 3 | 死码 `ExecuteRecruitWarrior` **全量删除** | **🔧 行号勘正**：任务书写「KingdomBrain.cs:854」＝**墓碑注释首行**；实盘＝ **:853 summary ＋ :854-855 墓碑 ＋ :856 方法签名**，方法体至 **:881**（`}`）⇒ 删除区间 **853-881**（保留 :852 空行） |
| 4 | 修后附 asset 三行 `need` 直读 ＋ 枚举未重排 | 见 §二 |

---

## 二、实盘前置核实（逐项带证据；检索类均带**阳性对照** L-29）

| 核查项 | 实盘读数 | 判定 |
|---|---|---|
| **asset 三行 need** | id18 **17**（L344）／id19 **17**（L363）／id21 **17**（L401） | 待改 ✅ |
| **同口径基线** | id23 **22**（L439）／id24 **22**（L458）；id20 `LeyForge` **17**（L382，禁动）；⚠️ id25 `SiegeWorkshop` **17**（L477）→ 见 §五-3 | ✅ |
| **枚举序（L-28）** | [UtilityScorer.cs:51-85](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L51-L85) `NeedKind : byte` 从 0 顺排：`ExclusiveGap=17`(L71)／`GeneralGap=18`／`FormationGap=19`／`UnitTypeGap=20`／`MachineDemand=21`(L79)／**`MilitaryBuildingGap=22`**(L82 **尾插**) | **中间位未重排** ✅ |
| **need 口径载体（asset-only 是否成立）** | [UtilityScorer.cs:268-276](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L268-L276) `MilitaryBuildingGap` case＝`if (CountActiveDef(k.id, d.buildingId) >= 1) return 0f;` 否则 `max(GeneralGapScore, FormationGapScore(needA), UnitTypeGapScore)` ⇒ **按 `d.buildingId` 判在场**，换 asset 引用即生效 | **asset-only 成立** ✅（不必改 case） |
| **族门禁仍在** | [UtilityScorer.cs:482-516](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L482-L516) id18/19/21 落**共享建造 case**：①`buildTargetCap` 上限（三者 asset=0）②`bdef.raceId` 族门禁（WarAcademy=0／WarCamp=3／ArcheryRange=1）③`uniquePerKingdom` 镜像 | ✅ 未因换 need 而丢 |
| **咬合自洽（谁把 need 顶起来）** | `UnitTypeGapScore`（[L320-342](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L320-L342)）统计「可训域战斗兵种」**不过滤建筑在场** ⇒ 本族专属兵（需专属营训练）未拥有时 `UnitTypeGap>0` ⇒ need>0 ⇒ ⑰ 先行导向建营（与 ⑦ 建筑前置双向咬合） | ✅ 机理成立 |
| **死码零调用** | grep `ExecuteRecruitWarrior` 全 `Assets/` = **1 命中（仅定义处 KingdomBrain.cs:856）**；**阳性对照**同 pattern `ExecuteRecruitArmy` = **4 命中**（L582 调用／L854·885 注释／L925 定义）⇒ 检索有效 | **零引用** ✅（L-29） |
| **AI.Core 核查（sim-sync）** | `AI.Core/` grep `UtilityScorer\|ExclusiveGap\|MilitaryBuildingGap\|ExecuteRecruitWarrior\|NeedKind\|UtilityActionConfig` ⇒ **零命中**；**阳性对照**同 pattern 在 `Systems/AI/KingdomBrain/` 命中 ⇒ 检索有效。**不同程序集 ⇒ 不走 sim-sync**（同 D656 判） | ✅ |
| **工作区卫生** | HEAD=`40c0416`；`git status --short -- Assets` = **空**（零在飞改动，无并发冲突） | ✅ |

---

## 三、🔴 阻塞级发现（必须上报）：三专属营 `minStage=3`（军事期）⇒ §三-1「落地实证」60 日窗口**结构性不可得**

**实盘**：id18/19/21 三者 asset `minStage: 3`（asset L341/L360/L398）＋ `stageWeight: [0, 0, 0.5, 1]`（军事期档）；代码默认表同（`UtilityActionConfig.cs:42/43/45`）。
**门控**：[UtilityScorer.cs:127](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L127) `if (stage < def.minStage) { stageFiltered++; continue; }`（D321 阶段可见性）⇒ **军事期之前，三个行动不可见、不被评分、不可能落地**。
**军事期本身的门槛**：[ScriptStageMachine.cs:96-101](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/ScriptStageMachine.cs#L96-L101) `warriorCount ≥ expandToMilitary_warriorsMin(=4)` ＋ `population ≥ 12` ＋ `expansionChunks ≥ 2` ＋ `expandMinDays`。
**实测对照**：HH.202 §② 同一容器 60 日（seed 48903）四 AI **warrior 峰值＝3**（k3 D60=3）⇒ **未达军事期** ⇒ **任务书 §三-1（三专属营落地 >0）在 60 日窗口内结构性不可得**；DZ-103 的「run2/run3 命中 0」亦**部分源于此门控**（非仅 need 口径）。

**请裁取证路径（§五-1，执行端建议 A）**。

---

## 四、实施设计（最小面，落盘后无二义）

1. `UtilityActionConfig.asset` **3 行** `need: 17 → 22`（按 id 段精确定位，**禁整文件覆盖**；`LeyForge` id20 段一字不动）。
2. `KingdomBrain.cs` **删 853-881**（summary ＋ 墓碑注释块 ＋ 方法体；保留 :852 空行、:883 起 `CollectRecruitCandidates` 不受影响）。
3. **不动** `UtilityScorer.cs`／不动 `NeedKind` 枚举／不动 `LeyForge`／不动任何数值（stageWeight/needA/cost 全保留）。

---

## 五、列报 / 请裁（4 项）

1. **🔴 §三-1 取证路径（阻断级，见 §三）**——本批 60 日短局内三专属营不可见（minStage=3）：
   - **A（执行端建议）＝阶段注入夹具短程探针**：容器内进局后对「族匹配国」注入 `k.scriptPhase = ScriptStage.Military`，再观测 census/need/落地。**先例**＝`Smoke_2_22P0.cs:215/497` 同法，且该文件 :220 已实证「**日 tick 会把阶段机覆回** ⇒ 须每轮重注入」。成本≈24 现实分钟；正门 `EnterTestRun`＋15x 不变。**代价＝观测域容器级改动（须列报，D647 裁 B′ 允许）＋注入态属夹具面（非自然长局）**。
   - **B＝长局窗口**（120 日熔断，≈48 现实分钟）等 AI 自达军事期。**风险**：HH.202 60 日 warrior 峰值仅 3，120 日未必达标 ⇒ 可能白跑；且与 HH.203 七考重验重复。
   - **C＝如实认账不可判读**（同 D563③ 兽人首训模式）：§三-1 落地实证改由「军事期注入面 need/Feasible/census 行为断言 ＋ asset 直读」承担，**落地实证挂 HH.203 七考长局观察**（七考判定线本就是「≥2 AI 至军事期」，届时三专属营才真正可达）。
   - 另：§三-2 census top 由既有 `[DiagMilitary]` census 行（[Valley_DiagMilitary.cs:110-114](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_DiagMilitary.cs#L110-L114) 输出 `top=`）**天然覆盖，无需改探针**；但**逐条 need/feasible 明细**现探针仅 ⑦/⑰a/⑰b/⑯ 四条（:104-107），要覆盖三专属营需**加 3 条 DumpAction**（观测域增量，请一并裁）。
2. **⚠️ 双源（asset ↔ 代码默认回退表）**：`UtilityActionConfig.cs:42/43/45` **同有** `need = NeedKind.ExclusiveGap`（`LoadConfig()` L58-62＝asset 缺失才回退）。**HH.202 先例只改 asset**（当时 id23/24 在回退表中**根本不存在** ⇒ 无双源）；本批三行动**两处都有** ⇒ 只改 asset 会留回退表陈旧（且该表已缺 id23~26 四条＝本身已部分陈旧）。请裁：
   - **A（推荐）＝只改 asset**（守 HH.204 §一 文件范围；回退表＝asset 缺失兜底，现役路径不生效；与 HH.202 先例同判）；
   - **B＝同步改 `.cs` 三行**（治本双源一致，但**超出 §一 授权文件**；若一并补 id23~26 缺失条目则进一步扩范围）；
   - **C＝列报不改**。
3. **⚠️ id25 `SiegeWorkshop`（建投掷机厂）`need: 17 = ExclusiveGap`**（asset L472-490，need **L477**；minStage=3）——不在 §一 三行清单内，但**同为军事向建筑**、且是**唯一建厂行动**（`buildTargetCap:1`），按 D657 精神（军事向走真实缺口）疑似同类。**不擅动**，请裁是否并入（并入即本批 **4 行**）。
4. **报备（不请裁）**：探针沿用 `Valley_HH80_DiagRun`（正门＋15x，L-09）；**seed 复用 `48903`**（HH.202 同 seed ⇒ 修复前后可对照；L-29 已验证非历史/冒烟局）；**槽改 `p1_diag3`**（新槽，禁覆盖 `p1_diag`/`p1_diag2`/`p1_run6`/`6b`/`7`）；窗口＝容器现值 `DIAG_DAYS=60`。**上述均为容器级观测配置（D647 裁 B′），随交付列报。**

---

## 六、红线自检

- ✅ **禁参数微调找补**（D563③）：只换 `need` 的**枚举靶位**（机制口径），**零数值改动**（stageWeight/needA/cost/buildTargetCap 全不动）
- ✅ **禁动 `LeyForge`**；**禁改 `MilitaryBuildingGap` case 语义**（只换 asset 引用）；**禁改 `NeedKind` 中间位**（L-28）
- ✅ **AI.Core 零直改**（§二 核查：不在镜像内；施工后仍验 `git status AI.Core`＝空）
- ✅ **L-29**：三处检索全带阳性对照（死码／AI.Core／枚举）
- ✅ **L-32**：收工退 Play（禁留 1x 余留世界）
- ✅ 确定性／玩家侧零改动；写-改-commit 同串、**显式路径 add（禁 `git add -A`）**、不 push

## 七、门禁（交付前置）

编译 **0 错 0 新警** ＋ `ExecuteRecruitWarrior` grep **零命中（含定义处）** ＋ asset 三行 `need=22` 直读 ＋ 枚举未重排 ＋ 零退化（`Smoke_2_22P0` **32/0** ＋ `Smoke_2_23RP0` **27/0**）＋ `git status AI.Core` 空 ＋ `git status Assets` 仅本批文件。

## 八、批序与停手声明

**开工回执（本信，HH.205）→ 停手待策划确认（§五 第 1/2/3 项）→ 实施 → 交付报告（按水位线取号，含 §三 证据）→ 策划端验收成立 → HH.203 七考重验起跑。**

**⇒ 本信落盘后停手，等策划端确认（尤其 §五-1 取证路径与 §五-2 双源口径）后再开工。**
