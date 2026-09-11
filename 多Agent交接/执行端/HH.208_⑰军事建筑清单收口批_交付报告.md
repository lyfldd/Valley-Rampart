# HH.208｜⑰ 军事建筑清单收口批 · 交付报告

> 执行端（TraeCode·Unity 轨）· 2026-09-11 · 状态：🟡**已交付待验收（含 1 项验收句未达成 ⇒ §五请裁）**
> 锚点：**HH.204 任务书（D657／0.6 §一百八十六）** ＋ **D658 裁 A′**（放行实施）
> 取号：遵 D640 #10（账本水位线 HH.207→**HH.208**，独立单行 commit `dd839ef`；完成报告号禁预留）
> 顺序：本批验收成立 ⇒ **HH.203 七考重验**起跑

---

## 一、实施（3 件，改动面 4 文件）

| # | 内容 | 实盘锚点 |
|---|---|---|
| 1 | **asset 三行 `need: 17 → 22`** | `UtilityActionConfig.asset` id18 `WarAcademy`（need **L344**）／id19 `WarCamp`（**L363**）／id21 `ArcheryRange`（**L401**）；`LeyForge`(id20) 与 `SiegeWorkshop`(id25) **未动**（D658 裁③） |
| 2 | **死码 `ExecuteRecruitWarrior` 全量删除** | `KingdomBrain.cs` **853-881**（summary ＋ 墓碑注释块 ＋ 方法体；保留 :852 空行） |
| 3 | **A′ 探针与夹具**（D658 授权） | ①`Valley_DiagMilitary.cs`：**+3 DumpAction**（⑰c 战争学院／⑰d 兽人战营／⑰e 精灵射箭场）＋`DumpNow(string tag)` 同步快照重载<br>②`Valley_HH80_DiagRun.cs`：`SLOT=p1_diag3`＋**`INJECT_FROM_DAY=51` 阶段注入取证段**（同步窗内 注入→DumpNow→还原 `scriptPhase`）＋**L-32 收尾**（`Time.timeScale=0` 真暂停＋`EditorApplication.ExitPlaymode()`） |

**未动**：`UtilityScorer.cs`（case 语义/枚举序一字未动）／`UtilityActionConfig.cs` 回退表（D658 裁② 只改 asset）／`LeyForge`／任何数值（stageWeight·needA·cost·buildTargetCap 全保留）。

## 二、门禁（全过）

| 门禁 | 结果 |
|---|---|
| 编译 | **0 错**（新 DLL 落盘 19:19:56/19:19:59；`read_console error`＝0） |
| 静态核验（Unity 反射实读） | asset：id18/19/21 `need=22` ✅／id20=17 ✅／id23/24=22／id25=17；`ExecuteRecruitWarrior=**False**`＋阳性对照 `ExecuteRecruitArmy=**True**`；`DumpNow` 重载在场；`SLOT=p1_diag3`／`INJECT_FROM_DAY=51`；`Resources.Load` 非 null |
| 冒烟零退化 | `Smoke_2_22P0` **run1/run2 各 32/0**（2422B，归一化仅 tag 行差异）＝**与 HH.202 基线同字节**；`Smoke_2_23RP0` **run1/run2 各 27/0**（2440B＝HH.183 基线） |
| 工作区 | 仅本批 4 文件（1 asset＋1 业务＋2 观测域）；**AI.Core 零触碰** |
| L-32 | 容器收尾**真暂停**＋**退 Play**（实测 `isPlaying=False`）⇒ 观察者所见段无 1x 余留世界 |

**测试段倍率 ＝ 15x**（进局后实测 `Time.timeScale=15`）；**观察者所见段倍率 ＝ 已退 Play（无余留世界）**。

## 三、A′ 必过项逐项（正门 `EnterTestRun`＋15x，seed 48903，槽 `p1_diag3`，D60 窗口，23.5 分钟）

| 必过项 | 结论 | 实盘证据 |
|---|---|---|
| **(b) need＝真实缺口** | ✅ | k1 ⑰e（精灵·族匹配）自然段 **D10/20/30/40/50 = 1.048/1.047/1.052/1.058/1.082**；注入段 D51-59 = **1.044~1.085**；**自然段全部 354 行 camp 明细 need 均 >1.0、逐日变化**、**无一行 = 0.5**（占位已弃） |
| **(b) 在场⇒0** | ✅（传递性） | 代码单源 [UtilityScorer.cs:273](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs#L273) `CountActiveDef≥1 ⇒ 0`；**同 case 兄弟实证**＝HH.202 前批 k3 Barracks 在场后 `⑰a need=0.000`（三份镜像 14/72/73 行）。⚠️诚实注记：**三营自身本跑未在场**（无族匹配可行/未达军事期）⇒ 未直接复验该支，靠同 case 单源推断 |
| **(c) 族门禁／buildTargetCap 仍在** | ✅ | k1（精灵）⑰e `feasible=**True**` 而 ⑰c/⑰d `False`；k2·k3 三营全 `False`（非人/兽/精灵）⇒ 门禁逐国精确；`buildTargetCap=0` 未变 |
| **阶段门控机理实证** | ✅ | **自然段 354/354 行 `stageGateBlocked=True`**（三营 60 日全程不可见）；注入段 →`False`（A′ 注入生效） |
| **(a) 族匹配专属营 census top≥1** | ❌ **0/27** | 注入段 9 日×3 国：k1 `top=Reinforce` **9/9**、k2 `top=BuildWall` 9/9、k3 `top=TrainGeneral` 9/9；**三营 0 次** |

**(a) 未达成的机理（逐条实证，非推测）**
1. **k2/k3 无族匹配** ⇒ 三营 `feasible=False` 被 `Feasible` 门控剔除 ⇒ 结构上不可能成为 top（族门禁正确工作的表现）。
2. **k1 是唯一族匹配国，但好战轴低**：`personalityAxis0=0.23` ⇒ ⑰e 得分 = need 1.05~1.09 × 0.234 × stageW 1.0 = **0.245~0.254**，低于当日常量 top（Reinforce）。
3. **口径方向正确并显著抬升**：对照修复前（`ExclusiveGap` 占位恒定 0.5）＝ 0.5 × 0.234 = **0.117** ⇒ 本批使该行动得分 **+110%~117%**；但**未跨过 argmax**。
4. **本 seed 覆盖缺口**：seed 48903 的 AI **无兽人/人类**（⑰d/⑰c 全程 `feasible=False`）⇒ 三营中**两族在本 seed 结构不可测**。

**对照（自然行为无漂移）**＝同 seed 60 日 k3 D60 `warrior=3`（**与 HH.202 同值**）、三国 D60 全 `stage=Expand`；三营自然段不可见 ⇒ 换 need 靶位对自然局零影响。

## 四、列报

1. **🔴 (a) 验收句未达成 ⇒ 请裁取证口径**（任务书 §三-2「census 被选中」）：
   - **A（推荐）＝夹具定向重跑**：`TestFixtureApi.PlaceKingdom(FixtureTier.Military, raceId=3 兽人/0 人类/1 精灵)`＋阶段注入（正门＋15x，观测域容器级），**一次覆盖三族**；成本≈数分钟（夹具跳过发育期）。
   - **B ＝换 seed 重跑**：挑含**兽人/人类 AI 且好战度高**的 seed（本 seed 两族缺位）；成本≈24 分钟。
   - **C ＝判据改判**：以「need 由恒定 0.5 → 真实缺口（1.04~1.09）＋得分 **+110%~117%**＋族门禁/限额不变」为口径生效证据；理由＝argmax 受个性轴×竞争行动（Reinforce/BuildWall/TrainGeneral）影响，**非 need 口径本身**。
   - **D ＝判 FAIL**（执行端不建议：(b)(c) 与机理均正向，(a) 系**seed 族构成 + 个性**所致）。
2. **观察项·夹具优先取证效率**（应问）：本批为保「同 seed 修复前后对照」把注入段放在 D51 ⇒ 24 分钟；若只为 (a)(b)(c)，注入段可 D5 起 ⇒ ≈5 分钟。建议下批由策划端定「夹具优先」口径（`PlaceKingdom` 成熟档＋注入，跳过发育期）。
3. **观察项·AI 数**：本跑注册 AI ＝ **k1~k3（k4 零行）**，与 HH.202 该 seed「4 AI」表述有差，请核（可能与长局事件级复现差异有关）。
4. **并发注记**：①取号 commit `dd839ef` 同文件携带了策划端 **D660** 在飞回写（HH.188/追加两行）——**内容未丢、已随本串落盘**。②D660 **账本行**写「美术端双占行顺延改号〔建议 **HH.208**〕」，而 **D660 工作日志行**已改指 **HH.209**（"HH.208 已被执行端 ⑰收口批 交付占用"）⇒ 本报告按 vr-id-ledger「先落盘者保留」占用 **HH.208**，美术端取 **HH.209**；**账本 HH.188 行内「HH.208」字样为陈旧残留，建议策划端随手勘正**（执行端不越权改他端行）。

## 五、证据文件

- 镜像日志 `Logs/P1/p1_log_20260911_200545.log`（32,537 行；含 `[DiagMilitary]` 必过项全量与注入段 tag）
- 收工档 `Logs/P1/hh80_diag_status.log`（`诊断窗口到期 @D60`／seed 48903／槽 p1_diag3／存盘 True／20:29:25）
- 检查点 `%LocalAppData%Low/DefaultCompany/Valley Rampart/Saves/`：`p1_diag3_day005~060.json` ×12＋`p1_diag3.json`（**观察器按槽写档，未覆盖 `p1_run6/6b/7`／`p1_diag/2`**）
- 冒烟 `Logs/P1/smoke_2_22p0_run1|2.log`（2422B×2）／`smoke_2_23rp0_run1|2.log`（2440B×2）
- 改前改后扫描 `results/…`（训练仓侧无涉）

## 六、红线自检

- ✅ 禁参数微调找补（D563③）：只换 `need` 枚举靶位（机制口径），**零数值改动**
- ✅ 未动 `LeyForge`／未改 `MilitaryBuildingGap` case 语义／未动枚举中间位（L-28）／未动 `.cs` 回退表（D658 裁②）
- ✅ AI.Core 零直改；确定性未破（冒烟双跑逐行一致）；玩家侧零改动
- ✅ 正门 `EnterTestRun`＋15x（禁直接设 timeScale，L-09）；L-29 检索带阳性对照；L-22 双跑；**L-32 收尾退 Play**
- ✅ 写-改-commit 同串、显式路径 add（禁 `git add -A`）、**不 push**

---

*交付：执行端 2026-09-11（HH.208）。请策划端验收 ＋ §四 第 1 项（取证口径）裁示 ⇒ 验收成立后 **HH.203 七考重验**起跑。*

---

## 策划裁决（策划端回写）

> **D661**（2026-09-11，主策划端）。**判据三直读（独立复核，非采信转述）**：①任务书＝HH.204（D657）／0.6 §一百八十六／D658 裁 A′；②代码 file:line **实读**＝`UtilityActionConfig.asset` **id18 `WarAcademy`（L352）L344 `need:22`** ／ **id19 `WarCamp`（L371）L363 `need:22`** ／ **id21 `ArcheryRange`（L409）L401 `need:22`** ／ id20 `LeyForge`（L379/L382）**=17 未动** ／ id25（L474/L477）**=17 未动**；`KingdomBrain.cs` **`ExecuteRecruitWarrior` grep 0 命中**（死码全量删除）＋阳性对照 `ExecuteRecruitArmy` **3 命中**（`:582` 调用／`:855` 文档／`:895` 定义）；③档位＝asset need 字面量直读（22）。

| 项 | 裁决 |
|----|------|
| ① asset 三行 `need=22` | **确认**（`LeyForge`/`SiegeWorkshop` 未动，守 D658 裁①③） |
| ② 死码 `ExecuteRecruitWarrior` 删除 | **确认**（0 命中，全量删除） |
| ③ (b) need＝真实缺口 | **过**（1.04~1.09、无一 0.5） |
| ④ (c) 族门禁/`buildTargetCap` | **过**（k1 精灵 ⑰e `feasible=True`、⑰c/⑰d `False`；k2/k3 全 `False`） |
| ⑤ **(a) census 被选中** | **未达成（0/27）⇒ 裁「改判为非硬门」**（见下） |

## (a) 判据改判（beyond-options）

〔**破框点**：A/B/C/D 四选项**共享前提＝「(a) 被选中＝need 口径生效的必要证据」——前提已破**〕
**实盘证明**：「被选中」由 **need × 好战个性轴 × argmax 竞争行动** 共同决定，**非 need 口径单变量**——k1 是唯一族匹配国，但好战轴仅 **0.23** ⇒ ⑰e 得分 0.245~0.254 < 当日常量 top（Reinforce）；且**低好战国不建军事建筑＝正确行为**（和平 AI），非缺陷。

**裁：**
1. **(a) 改判为非硬门**；本批「need 口径生效」证据以 **（b）真实缺口〔0.5→1.04~1.09〕＋得分显著抬升〔+110%~117%〕＋（c）门禁/限额不变** 为准。
2. **「被选中／落地」挂 HH.203 七考长局**（D658 已定：判定线≥2 AI 至军事期）。
3. **A（夹具定向重跑）＝采纳为可选补强**（`PlaceKingdom(FixtureTier.Military, raceId∈{3,0,1})`＋阶段注入，一次覆盖三族、≈5 分钟；直接给 (a) 证据，**但不替代**七考长局）。
4. **采纳 §四.2「夹具优先取证」口径**（下批注入段由 D5 起，24→≈5 分钟）。
5. **§四.3（AI 数 k1~k3 vs HH.202「4 AI」）＝列观察项**（与长局事件级复现差异待核，不阻断）。

**⇒ 本批验收成立**（施工范围与效果均达标）⇒ **HH.203 七考重验起跑**。

**验收三问**：①**为什么发生**＝**D658 我设计的 (a) 判据未排除「个性轴×竞争行动」混淆变量**（判据设计缺陷）②**单次失误 vs 流程漏洞**＝**策划侧流程漏洞**（判据设计未做 confounder 排查）③**缺条 vs 没查**＝**L-25 家族新变体**（判据未直读其依赖变量）⇒ `L-25` 加实例注记，不新立条目。
**嘉奖**＝执行端 (a) 未达成**不掩盖**、逐条实证机理（非推测）＋诚实注记「在场⇒0 传递性推断」＋并发注记指出账本陈旧残留（已由我 `b50cd19` 勘正）。
**遗留**＝①A 夹具定向重跑（可选）②执行端交付件（4 代码文件＋本报告）**未 commit**，请执行端按 #9 补 commit ③AI 数观察项。

### 衍生产物
- **七考重验（HH.203）＝前置达成，可起跑**
- **教训库**：`L-25` 实例注记（判据设计需排除 confounder）
- **后续口径**：夹具优先取证（D5 起注入）
