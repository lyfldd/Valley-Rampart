# HH.285 军事建造需求口径治本批 开工回执（D732 重做版·占 HH.285 号）

> 类型：开工回执（重做）｜状态：🟢 **已交付（与施工同串·回执即施工记录）**
> 日期：2026-09-15 · 执行端 · 依据：`D732` 打回令 ＋ HH.285 任务书（`D729` 签发＋`6865457a` §3.1 补注）
> 取号：**HH.285**（打回保留号·重做占号）｜交付报告：按水位线另取 **HH.288**（287→288）
> HEAD 基线：`6865457a` · 未 push

---

## 〇、对 D732 打回三项的认领

1. **改动面违规认领**：上批误把 HH.284 的跑局活当本批交付、治本零施工，且动了 2 个 `.cs`（任务书红线「不改任何 `.cs`」）——本轮**唯一改动面＝1 个 `.asset` 2 个字段**，容器改动全部为 Editor-only 观测域且系 D732 第一步明令。
2. **容器混乱清 3 条**（第一步·已全部完成，见 §一）。
3. **报告标题**：本回执及交付报告标题＝「编号＋任务书原名」＝**HH.285 军事建造需求口径治本批**。

---

## §一、第一步·容器清理（未清不进第二步——已清）

| # | 项 | 处置 | 证据 |
|---|---|---|---|
| ① | `Valley_DiagMilitary.cs` +27 行尾插回退 | **已回退并迁入独立容器**（D728 裁 1 准 B 遵守）：㉔/㉕ 三面实读＋R4/R1 存量行＋`CountSiegeWorkshop` helper 全部迁入 `Valley_HH284_Probe.cs` 的 `DumpMachineReadout`／`TickDay`；`Valley_DiagMilitary.cs` **逐字还原** | `git diff 69998be6^ -- ...Valley_DiagMilitary.cs`＝**空**（逐字还原）；工作区 diff 恰 `-27` 行 |
| ② | `Valley_HH285_Observe.cs` 改名 | **已改名 `Valley_HH284_Probe`**（它实为 HH.284 第二步读数容器）：文件名／`.meta`（guid 同步 `BnxO5COp…`）／类名／头注释（含改名沿革）／日志路径（`hh284_probe.log`/`hh284_status.log`/`hh284_live.png`）／槽位（改 `hh284_probe`·防占用已跑证据）同步；旧文件＋meta 已删 | `git status`：`D Valley_HH285_Observe.cs(.meta)`＋`?? Valley_HH284_Probe.cs(.meta)` |
| ③ | 报告标题 | 本回执＋交付报告均按「编号＋任务书原名」 | 文件名：`HH.285_军事建造需求口径治本批_开工回执.md`／`HH.288_军事建造需求口径治本批_交付报告.md` |

**不复议声明**：尾插回退本身无异议（D728 裁 1 准 B 是既裁，上批违反在先），不报复议。

---

## §二、现状实读复核（改前重读磁盘）

`UtilityActionConfig.asset`（27 条）改前：id20 `need: 17`（ExclusiveGap）、id25 `need: 17`（ExclusiveGap）——与任务书 §2.1 逐字吻合。目标值核对 `NeedKind` 枚举（`UtilityScorer.cs:55-94`）：**17=`ExclusiveGap`／21=`MachineDemand`／22=`MilitaryBuildingGap`** ✅。`LoadConfig()`（`UtilityActionConfig.cs:62-66`）优先加载资产路径 `Resources/Config/Kingdoms/UtilityActionConfig`（存在·27 条）⇒ 运行时以 asset 为真源（M2）。

---

## §三、施工内容（第二步·唯一改动面）

| 文件 | 改动 |
|---|---|
| `Assets/_Game/Resources/Config/Kingdoms/UtilityActionConfig.asset` | **恰 2 行**：id20 `need: 17 → 22`；id25 `need: 17 → 21`。两处 `needA: 1` 不动；其余字段（minStage/axis/axisWeight/stageWeight/buildingId/cost*/buildTargetCap）一律不动 |
| `.cs` | **零改动**（红线 1）——`UtilityScorer.cs`/`KingdomBrain.cs`/`UtilityActionConfig.cs` 默认数组（22 条）逐字未动（M2 双源纪律） |
| `DZ-157` 面 | 未触（红线 3） |

---

## §四、排雷 M1~M7 逐条自答

| # | 雷 | 自答 |
|---|---|---|
| **M1** | `MachineDemand` 用 `needA` 作 `wantM`（`:313`） | id25 `needA=1` **未动** ⇒ `wantM=1` ⇒ Alert 档理论分 `1/(1+0)×0.8=0.8`——**探针实测 0.800 精确命中**（验收线3ⓐ），未擅改 needA |
| **M2** | `.cs` 默认数组 vs `.asset` 双源 | 只改 `.asset`；`.cs` 22 条默认数组**逐字未动**（`git status` 无该文件）；运行时真源=asset（`LoadConfig` 优先资产·探针静态直读即从 `LoadConfig()` 取得 21/22 ⇒ 资产生效实证） |
| **M3** | 互斥链须实证 | **已探针逐条验**（验收线3ⓒ）：无厂 ⇒ ㉔ true／㉕ false（`:616`）；建厂后（`FindAIBuildSpot`+`TryBuild`+等 4 日 Active）⇒ ㉔ false（`:577` cap=1）／㉕ true ⇒ **共用 `MachineDemand` 不双高分** |
| **M4** | 件2 `needA` 语义 | id20 `needA=1` 保持（与 id18/19/21 一致·`FormationGapScore` 目标基线）；探针 ⓑ `GeneralCount=0` 注入下缺口 >0 成立 |
| **M5** | `ExclusiveGap` 残余消费者 | 枚举**保留未删**（`L-28` 尾插禁中间改）；改后资产内消费者=0（27 条遍历实证：无任何条目 need=17）——历史资产引用面不受影响（枚举值未变） |
| **M6** | 与 HH.284 关系 | 本批只动 asset；HH.284 读数面已迁入 `Valley_HH284_Probe.cs`（独立容器·只读），两批职责分离；`Valley_DiagMilitary.cs` 已还原为 HH.284 前状态 |
| **M7** | 承 L-29/L-30/L-31/L-35 | **L-29** 阳性对照=ⓐ 前后对照（恒 0.500→0/0.8）；**L-30** 判据可达性先验=注入法（军事期/态势/资源/缺口全部注入·结构性可达；矮人局实测 k2 在场）＋**防假通过铁律落实=一律真 def**（D732 §3.1①·两先例硬编码陷阱规避）；**L-31** 双向咬合=ⓒ 用评分侧同源 `Feasible` 直调＋建厂走执行侧 `TryBuild` 真链；**L-35** 口径来源与排除项=交付报告 L-34 五列逐条（排除项=玩家国 id=0／id26 未动对照真源） |

---

## §五、sim-sync 核查结论

**零义务（纯数据层·预判与复核一致）**：本批改动=Unity 侧 `UtilityActionConfig.asset` 两字段（AI 决策的**行动配置**），不触 `AI.Core`（决策核）、不触 `TuningSnapshot`/`ProfessionSnapshot`/`FactorContext`、不触 champion/factor_registry/professions.json。sim 侧 `harness/` 无 `UtilityActionConfig` 对应物（KingdomBrain 效用层为 Unity 侧 Kingdom AI，sim 战场层无此概念·与 HH.284 只读批同域）⇒ 双端 MD5 对比义务=0、三方同步=0、15 账本登记义务=0。**AI.Core 零触已由 git status 佐证**（`Assets/_Game/Systems/AI.Core/` 零改动）。

---

## §六、行为探针方案（已执行·8/8 PASS）

**载体**：独立容器 `Assets/Editor/Smoke/Valley_HH285_Probe.cs`（本批新自建·**未改 `Valley_DiagMilitary.cs`**）。
**防假通过铁律（D732 §3.1①）**：一律从 `UtilityActionConfig.LoadConfig().Find(...)` 取**资产真 def**，禁自造 `UtilityActionDef`（两先例的 need 硬编码陷阱已知悉并规避）。
**流程**：正门 `EnterTestRun`(seed 21107/新槽 `hh285_probe1`/15x) → 等就绪 → **真暂停**（`Time.timeScale=0`·deltaTime=0 ⇒ 日推进冻结 ⇒ 每日 tick 不覆写注入态）→ ⓐⓑⓒ-1 注入读数（注入全部复原：scriptPhase/SituationHub/PostureHub）→ 恢复倍速 → 建厂（`FindAIBuildSpot`+`BuildController.TryBuild`·先例 P9b 同法）→ `WaitDays(4)` 等 Active（L-20）→ ⓒ-2 → `ExitTestRun`+`QuitSmoke`+退 Play（L-32）。**不跑长局**（§3.1④·asset 的 NeedScore 容器内直调）。
**结果**：**8/8 PASS**（详见交付报告 HH.288 §三）。

---

## §七、红线复核

不改任何 `.cs`（业务零改·容器为 Editor-only 观测域且系明令）✅｜不顺手改其他 need/字段 ✅｜不动 `DZ-157` ✅｜正门＋真暂停＋ExitTestRun＋退 Play（L-32）✅｜AI.Core 零触 ✅｜禁为过线凑数（L-30·真 def 铁律）✅｜具名 git add·禁 -A/-u/.·不 push ✅｜改前重读磁盘 ✅｜`_任务队列.md` 一行未写 ✅

---

> 执行端｜2026-09-15｜HH.285 军事建造需求口径治本批（D732 重做版）：第一步容器清理 3 条全落＋第二步 asset 两字段治本＋探针 8/8 PASS——交付证据见 HH.288
