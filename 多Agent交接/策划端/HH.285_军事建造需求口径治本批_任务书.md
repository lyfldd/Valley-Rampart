# HH.285 军事建造需求口径治本批 任务书（`DZ-105` ㉔建厂 ＋ `DZ-156` ㉑地脉熔炉）

> 类型：施工任务书（**纯数据层·零代码**）｜状态：🟢 **已签发·待接单**
> 日期：2026-09-15 · 签发端：主策划端 · **Gate：`G2-2`（建军链全绿）**
> 关联：`3.1 §六 G2-2`／`0.6 §二百五十六`（`D728`）／台账 **`DZ-105`**（🔴 坐实）· **`DZ-156`**（🔴 新立）／`HH.284`（本批为其第二步的**前置**）
> 取号：HH.285（账本水位线 284→285）
> 由来＝`D728` **裁 3 A 案**（用户 2026-09-15 点选）：先修 `DZ-105` 再跑 `HH.284` 第二步读数。

---

## 〇、一句话目标

把**两条"需求口径错配"**一次改到位（**只改 `UtilityActionConfig.asset` 两个字段，不碰任何 `.cs`**），使：
- **㉔ 建投掷机厂** 的需求分由「恒定占位 0.5」改为**态势驱动**（口径同 ㉕ `MachineDemand`）⇒ 军事期可竞争；
- **㉑ 建地脉熔炉**（矮人族专属）的需求口径与 id18/19/21 三条族专属建筑**对齐**（`MilitaryBuildingGap`）。

**⇒ 完成后放行 `HH.284` 第二步**（㉕机器实产读数）。

---

## 一、作业依据（必读·逐字）

| # | 文档／实物 | 用处 |
|---|---|---|
| 1 | `Assets/_Game/Resources/Config/Kingdoms/UtilityActionConfig.asset`（**27 条**） | 本批唯一改动面 |
| 2 | `UtilityScorer.cs:304-315`（`MachineDemand` 评分） | 件1 目标口径（含 `needA` 用法 `:313`） |
| 3 | `UtilityScorer.cs:285-303`（`ExclusiveGap` / `MilitaryBuildingGap`） | 件2 目标口径（`needA` 用法 `:301-302`） |
| 4 | `UtilityScorer.cs:563-580`（建造类 `Feasible` 三守卫共用 case 组） | **互斥链依据**（㉔ 与 ㉑ 同组：上限／族门禁／限建） |
| 5 | 台账 `DZ-105`（2026-09-11 立） | **已给定正确口径**：态势驱动（同 id26）；明示**不可**用 `MilitaryBuildingGap`（＝troop-gap，会把"缺将军"错接到"建机器厂"） |
| 6 | 台账 `DZ-156`（2026-09-15 立） | 件2 立账（`D656` 族专属建筑修复漏项） |
| 7 | `NeedKind` 枚举（`UtilityScorer.cs:55-94`） | 值对位：**17=`ExclusiveGap`／21=`MachineDemand`／22=`MilitaryBuildingGap`** |

---

## 二、策划端已取证（执行端复核·勿重推翻）

### 2.1 现状实读（`UtilityActionConfig.asset`）

| id | 名称 | minStage | axis | **need** | needA | stageWeight | buildingId | buildTargetCap |
|---|---|---|---|---|---|---|---|---|
| **20** | 建地脉熔炉 | 2(Expand) | 1(经济) | **17 = `ExclusiveGap`** ⚠️ | 1 | `[0,0.5,1,1]` | `LeyForge` | 0 |
| **25** | 建投掷机厂 | 3(Military) | 0(好战) | **17 = `ExclusiveGap`** ⚠️ | 1 | `[0,0,0.5,1]` | `SiegeWorkshop` | **1** |
| 26 | 造战争机器 | 3 | 0 | 21 = `MachineDemand` ✅ | 2 | `[0,0,0.5,1]` | `SiegeWorkshop` | 0 |
| 18/19/21 | 战争学院/兽人战营/射箭场 | 3 | 0 | 22 = `MilitaryBuildingGap` ✅ | 1 | `[0,0,0.5,1]` | 各族专属 | 0 |

### 2.2 为什么这是缺陷（`DZ-105` 坐实）

- `ExclusiveGap`（`UtilityScorer.cs:285`）语义＝「本国**无族专属建筑** ⇒ **占位底分 0.5**」⇒ **恒定 0.5**，与"是否需要投掷机厂"**无关**。
- 而同轴（`axis:0`）同权重（`axisWeight:1`）同 stageWeight 的对手（id18/19/21/23/24）用**真实缺口**（`MilitaryBuildingGap`＝缺将军/编队/兵种 max）⇒ **可达 1.0**。
- **⇒ ㉔ 在军事期结构性竞争劣势**（0.5 恒定 vs 对手 1.0）⇒ `HH.284` 跑局**预期零触发**。

### 2.3 互斥链（件1 的安全性依据·实读）

- **无厂时**：`Feasible(ProduceMachine)` ⇒ `UtilityScorer.cs:616` ①厂前置 `<1 ⇒ false` ⇒ **㉕ 不可行**；**㉔ 可行** ⇒ 只有建厂可选。
- **有厂后**：㉔ `buildTargetCap:1` ⇒ `:577` 上限守卫 ⇒ **㉔ 不可行**；㉕ 可行（厂前置满足）。
- ⇒ **㉔/㉕ 天然互斥，共用 `MachineDemand` 不产生评分冲突** ✅

---

## 三、范围（两件·**纯 asset 两处单字段改**）

### 件1｜`DZ-105` — id25 `BuildSiegeWorkshop` 需求口径改态势驱动

```
- id: 25  （建投掷机厂）
    need: 17   →   need: 21          # ExclusiveGap → MachineDemand（同 id26）
```
- **`needA` 保持 `1` 不动**（`MachineDemand` 用 `wantM = Mathf.Max(1, needA)`·`:313` ⇒ 军事期 `MachineCount=0` 时得分 `1/(1+0)×0.8 = 0.8`，已足够高）。
- **其余字段一律不动**（`minStage:3`／`axis:0`／`axisWeight:1`／`stageWeight:[0,0,0.5,1]`／`buildingId`／三项 cost／`buildTargetCap:1`）。

### 件2｜`DZ-156` — id20 `BuildLeyForge` 需求口径对齐族专属建筑

```
- id: 20  （建地脉熔炉·矮人族专属）
    need: 17   →   need: 22          # ExclusiveGap → MilitaryBuildingGap（同 id18/19/21）
```
- **`needA` 保持 `1` 不动**（与 id18/19/21 一致；`MilitaryBuildingGap` 用 `needA` 作 `FormationGapScore(k, max(1,needA))`·`:301-302`）。
- **其余字段一律不动**（`minStage:2`／`axis:1`／`stageWeight:[0,0.5,1,1]`／`buildingId:LeyForge`／cost／`buildTargetCap:0`）。

---

## 四、验收线

| # | 线 | 判据 |
|---|---|---|
| **1** | **改动面最小（核心）** | `git diff` **恰 2 行**（每行 1 删 1 增）＝id20 的 `need:` ＋ id25 的 `need:`；**给逐行 diff 证据** |
| **2** | **静态直读** | 资产实读：id20 `need: 22`、id25 `need: 21`；**其余 25 条行动逐条未变**（给 grep/遍历证据） |
| **3** | **行为探针（核心·正门）** | ⓐ **㉔ 评分 > 0**：军事期 + 态势满足（`threatM` 或 `postureM`）⇒ `BuildSiegeWorkshop` 评分由"恒定 0.5"变为**态势驱动值**（给前后对照读数）<br>ⓑ **㉑ 进池**：矮人局 + 军事缺口存在 ⇒ id20 走 `MilitaryBuildingGap`（缺口 > 0）<br>ⓒ **互斥链**：无厂 ⇒ ㉔ 可行 / ㉕ 不可行；建厂后 ⇒ ㉔ 不可行（cap=1）/ ㉕ 可行 |
| **4** | **零代码** | `git status` **无任何 `.cs` 改动**（`UtilityScorer.cs`／`KingdomBrain.cs`／`UtilityActionConfig.cs` 逐字未动）——**本批唯一改动面＝1 个 `.asset`** |
| **5** | **sim-sync** | 出核查结论（**预期＝零 `AI.Core` 义务**·纯数据层；仍须给结论，纪律） |
| **6** | **回归** | 编译 **0 error**；四族四轮冒烟不退化（`2_20B_M7` 6/6 ＋ `2_20_Smoke_Race` ALL PASS）；收尾三态（`L-32`） |
| **7** | **写后验** | 本批相关路径 `git status` 全空；各笔 hash 已贴；**未 push** |

---

### 3.1 线3 行为探针·**验法指引**（`D732` 补注·**防假通过**）

> **①⛔ 禁自造 `UtilityActionDef`（本批最大陷阱）**
> 现成先例 `Smoke_2_22P0.cs:493-507`（P9a）与 `Valley_HH140_Probe.cs:251-265`（P6d）**都是自造 def**：
> ```csharp
> var defMD = new UtilityActionDef { id = ..., need = NeedKind.MachineDemand, needA = 2 };  // ← need 硬编码
> ```
> 那验的是 **`MachineDemand` 分支逻辑本身**，**与本批改的 `asset.need` 字段无关** ⇒ 照抄它会**通过但什么都没验到**。
> **本批必须**：从资产取**真 def** —— `UtilityActionConfig.LoadConfig().Find(UtilityAction.BuildSiegeWorkshop)` / `...Find(UtilityAction.BuildLeyForge)`，再跑 `UtilityScorer.NeedScore(k, def)`。
>
> **② 静态直读（资产级·必给）**：`Find(BuildSiegeWorkshop).need == 21` ／ `Find(BuildLeyForge).need == 22`（输出实测值）。
>
> **③ 行为前后对照**：**改前基线已现成**＝`HH.285` 报告实测「㉔ `score` **恒 0.500**」（＝`ExclusiveGap` 占位·已由 `D732` 收编为治本前基线）；**改后**＝`MachineDemand` 态势驱动（照先例注入法：`scriptPhase=Military` ＋ `SituationHub`/`PostureHub` 造 None/Alert 两档 ⇒ **期望 None 档 0 ／ Alert 档 > 0**）。
>
> **④ 本批不跑长局**：asset 改动的 `NeedScore` **可在容器内直接调**（两处先例即如此）⇒ **禁**为此再跑 120 日——**长局读数是 `HH.284` 第二步的事**。
>
> **⑤ ⓒ 互斥链**：用 `UtilityScorer.Feasible` 直调（`HH.282` 验收探针同法）——无厂 ⇒ `Feasible(㉕)=false`（`:616` 厂前置）＋`Feasible(㉔)=true`；建厂后 ⇒ `Feasible(㉔)=false`（`buildTargetCap:1`·`:577`）＋`Feasible(㉕)=true`。
>
> **⑥ 容器归属**：本批探针**自建独立容器**（**禁**改 `Valley_DiagMilitary.cs`——该文件已因 `HH.284` 违裁尾插待回退，见 `D732` 四）。

---

## 五、红线

1. ❌ **不改任何 `.cs`**（含 `UtilityScorer.cs`／`KingdomBrain.cs`／`UtilityActionConfig.cs` 的**默认数组**——注意 `.cs` 与 `.asset` 是**两份**，本批**只改 `.asset`**）。
   - ⚠️ **M2 提示**：`LoadConfig()`（`:62-66`）优先加载**资产**（`Assets/_Game/Resources/Config/Kingdoms/UtilityActionConfig.asset`·存在·27 条）⇒ **运行时以资产为准**；`.cs` 默认数组（22 条）仅作 asset 缺失兜底，**本批不动**。
2. ❌ **不顺手改** id26 或任何其他 `need`；**不调** `stageWeight`／`axis`／`axisWeight`／`cost*`／`minStage`／`buildTargetCap`。
3. ❌ **不动** `DZ-157` 面（`SiegeProductionSystem.cs` 上限口径·独立挂账）。
4. 若施工中发现**必须改代码**才能达标 ⇒ **停手列报**（`sim-sync` 义务判定），禁自行扩面。
5. 正门 `EnterTestRun` ＋ 真暂停 ＋ `ExitTestRun` ＋ 退 Play（`L-32`）；**不为过线凑数**（`L-30`）。
6. 具名 `git add`、禁 `-A/-u/.`、**不 push**；改前重读磁盘；写-改-commit 同串。
7. `_任务队列.md` 一行不写（王国AI 专向队列 `3.1 §六` 由策划端更新）。
8. 交付报告按账本**实时水位线取号**（禁预留·`D640 #10`）。

---

## 六、排雷

| # | 雷 | 处置 |
|---|---|---|
| **M1** | **`MachineDemand` 用 `needA` 作 `wantM`**（`:313` `Mathf.Max(1, d.needA)`） | id25 `needA:1` ⇒ `wantM=1` ⇒ 军事期 `MachineCount=0` 时 **0.8 分** ✅ 足够；**勿为"提高分数"擅改 needA** |
| **M2** | **`.cs` 默认数组 vs `.asset` 双源** | 运行时读 **`.asset`**（`LoadConfig` 优先资产且资产存在）⇒ **只改 `.asset`**；`.cs` 数组保持原样（防误改后"双源漂移"） |
| **M3** | **互斥链须实证** | ㉔/㉕ 共用 `MachineDemand` 后**不会**双高分：出厂前 ㉕ `Feasible=false`（`:616`）／出厂后 ㉔ 被 `buildTargetCap` 拦（`:577`）⇒ **须探针逐条验**（验收线3ⓒ） |
| **M4** | **件2 的 `needA` 语义** | `MilitaryBuildingGap` 用 `needA` 作 `FormationGapScore` 的编队目标基线 ⇒ 保持 **1**（与 id18/19/21 一致），**勿改成别的** |
| **M5** | **改 need 后 `ExclusiveGap` 是否还有消费者** | 改后 id20/id25 不再用它；**仍保留枚举**（`NeedKind` 尾插禁中间改·`L-28`）；**勿删枚举**（id18/19/21 曾用、历史资产可能引用） |
| **M6** | **本批与 `HH.284` 的关系** | 本批是 `HH.284` **第二步的前置**；**两批改动分开 commit**（本批只动 asset；`HH.284` 只读不动业务） |
| **M7** | **承 `L-29`/`L-30`/`L-31`/`L-35`** | 阳性对照（㉔ 前后评分对照）／判定线可达性先验（军事期 + 态势条件须先证可达）／**双向咬合**（评分侧 `Feasible` 与执行侧 `ExecuteBuildSiegeWorkshop` 同源·`L-31`）／口径来源与排除项（`L-35`） |

---

## 七、产出

1. **开工回执（占 HH.285 号）**：含 §六 排雷 **M1~M7 逐条自答** ＋ 现状实读复核 ＋ `sim-sync` 核查结论 ＋ 行为探针方案。
2. **交付报告（按水位线另取号）**：§四 七条逐项证据（**2 行 diff 逐字**／静态遍历／三组行为探针读数／零代码声明／回归／写后验）＋ `L-34` 五列。
3. **完成后由策划端放行 `HH.284` 第二步**（㉕机器实产读数）。

---

> 签发：主策划端｜2026-09-15｜**D729**｜依据＝`D728` 裁 3 A 案（用户点选）＋ 台账 `DZ-105`（既定口径）＋`DZ-156`（新立）＋ 策划端实读（`UtilityActionConfig.asset` 27 条全表／`MachineDemand` 与 `MilitaryBuildingGap` 分支／`Feasible` 三守卫共用 case 组）
