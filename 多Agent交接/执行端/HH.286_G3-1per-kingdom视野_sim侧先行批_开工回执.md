# HH.286 「`G3-1` per-kingdom 视野 · sim 侧先行批」开工回执

> 类型：开工回执（**第一步·设计稿先行**）｜状态：🟡 **已交付·待主策划端裁（第二步停手）**
> 日期：2026-09-15 ｜ 端：**训练端**（训练端策划出设计稿 → 训练师施工）
> 取号：**占 HH.286 号·不另取号**（同号先例 `HH.272`／`HH.282`／`HH.284`；`D640 #10` 禁预留 ⇒ 本批**完成报告**＝训练师届时按账本实时水位线取号）
> 任务书：[HH.286_G3-1per-kingdom视野_sim侧先行批_任务书.md](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/策划端/HH.286_G3-1per-kingdom视野_sim侧先行批_任务书.md)（`D730`·Gate=`G3-1`）
> 配套件：[HH.286_G3-1per-kingdom视野_sim侧先行批_口径设计稿_报裁.md](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/执行端/HH.286_G3-1per-kingdom视野_sim侧先行批_口径设计稿_报裁.md)（本回执 §六 指向它）
> 关联：`3.1 §三 G3-1` 门卡片／`§六` 队列行／`§七 P-007`／`§八⑥` 冻结例外／`D677` 立项／台账 `DZ-143`／`2_24 §六`／训练仓 `AGENTS.md` 铁律6 ＋ 00 v2 §8.3／`15_训练侧harness与Unity端差距文档.md`
> 教训引用（签发侧已列）：`M8` 承 `L-23`/`L-29`/`L-30`/`L-34`/`L-35`；本回执另引 `L-24`/`L-25`（判据三直读）
> 裁决端：**主策划端**（跨域·`3.1 §七 P-007`）；训练端**内部**未取 TD（本批无训练轨独立裁决项；放行后若产生内部执行裁决，按 `_训练端决策登记.md` 取号，当前水位线 **TD-026**）

---

## 〇、状态宣告（边界与守线）

1. **两步制**：第一步（本回执 ＋ 口径设计稿）＝**已完成交付**；第二步（施工）＝**未开工**。
2. **本回执零施工动作**：未改任何 `harness/**` 代码、未改场景、未进局、未跑批、未触碰主仓（`Assets/**` 零改动）。全部动作为**只读实读 ＋ 文档落盘**。
3. **训练端策划边界**（角色卡）：本会话**不写 harness 代码、不跑批、不做执行判读**；施工归训练师（第二步·放行后）。
4. **红线复述**：①未放行**不动** `harness/Core/**` 与 `harness/KingdomBrain/**`（本回执仅只读）；②不动 `champion/`／`Holdout/`／`AGENTS.md`／`FactorRegistry`／`harness.csproj` 结构；③`门禁②` 口径与 `AGENTS.md` 铁律6／`00 v2 §8.3` 一致（见 §一 E2 勘正）；④不碰主仓；⑤`TD` 只记训练端内部裁决，跨域走 `3.1 §七`（`P-007`）。

---

## 一、现状实读复核（任务书 §一 逐行可否证）

> 手段：本项目 `Grep`／`Read` 工具直读磁盘（**非记忆、非报告转述**）；凡"零命中"结论均按 `L-29` 附阳性对照。

| # | 任务书原述 | 复核结论 | 实读锚点 |
|---|---|---|---|
| 1 | `_fogExplored` 已数组分桶·索引＝`Faction` | ✅ **可证**（但见 E1 勘正） | [SimWorld.cs:221-222](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L220-L222) `new HashSet<long>[2]` |
| 2 | `IsExplored(Faction,p)`；`enableFog=false` 恒可见 | ✅ **可证**（签名/短路均实读一致） | [SimWorld.cs:239-249](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L239-L249) |
| 3 | `RevealCircle`／`RevealUnitVision` 半径＝`perceptionRadius × cellSize` ✅ 已对齐设计 | ✅ **可证** | [SimWorld.cs:251-271](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L251-L271) |
| 4 | 先标视野再刷感知 | ✅ **可证** | [SimWorld.cs:908-910](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L908-L910) |
| 5 | `SimBrain` 雾过滤按 `Faction` | ✅ **可证** | [SimBrain.cs:375-377](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimBrain.cs#L375-L377)（`myFaction = _self.Faction`，[:370](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimBrain.cs#L370)） |
| 6 | `SimUnit.cs:58` 仅 `Faction`、**无 `kingdomId`** | ✅ **可证** | [SimUnit.cs:58](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimUnit.cs#L58) |
| 7 | 场景构造只写 `faction`，无 `kingdomId` | ✅ **可证** | [SimScenario.cs:70-81](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L70-L81)（`SimUnitSpec`）／[:166-171](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L166-L171)（faction 覆盖）／[:339-356](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L339-L356)（`ParseFaction`） |
| 8 | `enableFog` 默认 false ＋注释"现役 suite 全同屏" | ✅ **可证** | [SimConfig.cs:41-45](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimConfig.cs#L41-L45) |

**复核结论：8/8 行"是否可证"＝可证；但其中 2 行需勘正（E1/E2），另有 5 项任务书未载的实读（E3~E7），它们直接决定设计稿口径。**

### 1.1 勘正点（**报主策划端知悉**）

**E1｜"AI 共享一张探索图"表述不成立——实况是"AI 全盲"。**
`_fogExplored` 长度**硬编码 2**（[SimWorld.cs:221-222](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L220-L222)，构造仅初始化 2 桶 [:279](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L279)，全仓无二次扩容），而 `Faction` 有 8 值（[Faction.cs:19-31](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Core/Ports/Faction.cs#L19-L31)：None=0／PlayerCamp=1／Undead=2／**AiKingdom=3／Monster=4**／Orc_Player=5／Dwarf_Player=6／Elf_Player=7）⇒ `idx >= _fogExplored.Length` 命中 [SimWorld.cs:247](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L246-L248) 的 `return false`，且 `RevealCircle` 于 [:256](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L255-L256) 直接 `return`。
⇒ `enableFog=true` 时的**实况语义**＝「只有 `PlayerCamp`(idx=1) 有桶 ⇒ 玩家有雾；`AiKingdom`/`Monster` **越界 ⇒ AI 恒盲、AI 也不揭示视野**」。
**本勘正不改变方向（仍须补 `kingdomId` 维度），但改变改造面**：除"按 `kingdomId` 分桶"外，**必须同时解除"桶长度＝2"的结构钳制**，否则 AI 仍全盲（详见设计稿 §四 第 1 点）。

**E2｜`门禁②` 场景计数口径不一致，须统一后再作验收线。**
任务书 §二⑤／§三 线3 写「`suite_v9` 全 **148/151** 锚逐场景 `Δ≤0.0005`」；但 `00_训练体系主文档.md §8.3` 第 3 条原文写「判无场景退化（**144** 共有场景逐场景 `subScore`）」（[00_训练体系主文档.md:218](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/00_训练体系主文档.md#L215-L219)）。
据 `HH.229`／`HH.196`／`HH.187` 串记录，"**151**＝T09 重构成后池内场景总数"、"**148**＝`benchmark` 对照的**共有**场景数"，`00 §8.3` 的 144 系**早于 T09/T10 两次重建锚**的旧值。
**建议口径（请裁·C-5）**：以 `00 §8.3` 的**命令口径**为准（`benchmark --suite v9 --battles 100`·读锚不写锚），计数表述改为「**锚键集内逐共有场景** `subScore Δ≤0.0005`」，具体键数**施工时以 `results/baseline/v9/report.json` 的 `score.subScores` 键集实读并贴出**，不引用任何文档旧数。

### 1.2 新增实读（任务书未载·设计稿依据）

| # | 实证 | 含义 | 锚点 |
|---|---|---|---|
| **E3** | **`enableFog` 无任何外部设置入口**：全 `harness/**` grep `enableFog` 命中**仅 3 个文件**（`SimWorld.cs`／`SimConfig.cs`／`SimBrain.cs`，全为代码声明与引用），**零 JSON／零 CLI／零 patch 设置**；`SimScenario.Load` 只覆盖 `cellSize`／`professions`／`tuning` | ⇒ `M2` 的 **"`enableFog=true` 成立"门当前结构性不可达**（`L-30`）⇒ 必须补开关载体 | [SimScenario.cs:136-147](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L136-L147)；[SimConfig.cs:45](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimConfig.cs#L41-L45) |
| **E4** | **现役 v9 池零多王国同屏**：`harness/Scenarios/v9/` 63 场景全 `PlayerCamp` vs `Monster`（样例 [W9_T01_20260907_0.json:8-15](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Scenarios/v9/W9_T01_20260907_0.json#L8-L15)）；全 `harness/Scenarios/**` grep `AiKingdom` 仅命中 **2 个探针场景**（`v9_probe/probe_selfdef.json`、`v9_probe/probe_kingdom_calm.json`），且**各只有 1 个 AI 王国单位** | ⇒ 判定线「**k1/k2 交集空**」「**AI 不读玩家集**」在现役场景**不可判**（`L-30`/`M1`）⇒ 须新增"多王国同屏"最小验证场景 | grep `AiKingdom`→仅 `SimChampion.cs:197`／`SimScenario.cs:348` ＋上述 2 场景；[professions.json:1451-1452](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Data/professions.json#L1451-L1452)（`Orc_Berserker` 默认 `PlayerCamp`＝混编玩家侧，非 AI 王国） |
| **E5** | **锚在场**：`results/baseline/v9/report.json`（`total 0.422`／`battlesPerScenario 100`／`seed -1283596485`／`timestamp 2026-09-12T09:54:23`／`determinism 3/3`＝`W9_T-K_0`✓`KF26_2_0`✓`H9_chokepoint`✓） | ⇒ `00 §8.3` 的「**执行前置**：先确认锚在场」**已满足**（不会落进"首次运行＝建档写锚"分支） | `results/baseline/v9/report.json:2`（`meta`） |
| **E6** | **改造面调用点极小**（`M5` 实测清单）：`IsExplored` 调用点 **1 处**（`SimBrain.cs:377`）／`RevealCircle` **外部调用 0 处**（仅 `SimWorld` 内 `RevealUnitVision:271`）／`FogEnabled` 消费 **1 处**（`SimBrain.cs:376`）／`_fogExplored` **5 处**（全在 `SimWorld` 内） | ⇒ 签名变更**编译期兜底充分**，无隐蔽漏改面 | 同上四处 `file:line` |
| **E7** | **王国维度在 `KingdomBrain` 镜像层已有、在 sim 战场层为零**：`harness/KingdomBrain/` 仅 3 文件（`SituationSnapshot.cs`／`BattleLearnedWeights.cs`／`MirrorProbe.cs`），其中 `KingdomId` 已是核心键（[:15](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/KingdomBrain/SituationSnapshot.cs#L15)／[:38](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/KingdomBrain/SituationSnapshot.cs#L38)／[:113-119](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/KingdomBrain/SituationSnapshot.cs#L113-L119)）；但 `harness/Sim/**` 战场层 grep `kingdom` 零命中 | ⇒ 本批**不是**引入"王国"概念，而是**把已有 `kingdomId` 口径贯通到战场视野层**（口径来源＝镜像层既有点，非新造） | 上述锚点 ＋ grep 结果（阳性对照：`Grep "kingdomId" harness/KingdomBrain` 命中，证明检索有效） |

---

## 二、排雷 `M1~M8` 逐条自答

| # | 雷 | 自答 |
|---|---|---|
| **M1** | 现役 suite 全同屏 ⇒ 可达性先验·禁造特制场景蒙混（`L-30`） | **实证成立**（E4）：现役零多王国同屏 ⇒「k1/k2 交集空」**结构性不可判**。处置＝**设计稿 ⑥ 给场景方案**（新增"多王国同屏"最小验证场景），并**论证其必要性**：门卡片判定线**本身要求 `k1`/`k2` 存在** ⇒ 场景是判据的**必要前置**，非"为过判据特制"；同时**必配阴性对照**（重叠态／玩家在视野内两个变体）防"只证想要的结果"（详见设计稿 §七）。**不做**：为迁就判据改动生产池构成。 |
| **M2** | 双门（`enableFog=true` 成立 ＋ `false` 零回归） | **`false` 门**＝结构保证（短路返回）＋门禁②实测（锚在场 E5）；**`true` 门当前不可达**（E3：无开关入口）⇒ 设计稿 §五 给"补场景级开关"口径（请裁 C-3）。两门**并列验收**，缺一不裁。 |
| **M3** | 共享核加字段须双端同源 | 设计稿 §一 给出**载体取向**：**推荐不入 `ProfessionSnapshot`（共享核）** ⇒ 双端同源义务＝**0**；若裁定入核，则须 Unity `NpcProfessionDef.ToSnapshot()` 同步（Unity 本批零触碰 ⇒ 只能另批闭环），**不推荐**（理由：`kingdomId` 是单位实例属性、`NpcProfessionDef` 是职业级资产，无自然填充来源 ⇒ 会造"伪同源字段"，违 `sim-sync` 北极星）。 |
| **M4** | `Faction` 与 `kingdomId` 双轨——保留 `Faction` 供战役判定，仅新增 `kingdomId` 供视野分桶 | **照办**。`Faction` 保留全部现有语义：`SimPerception.QueryNearby(..., myFaction, ...)`（[SimBrain.cs:372-373](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimBrain.cs#L372-L373)）、参与阵营集与胜负判定（[SimWorld.cs:217-218](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L217-L218)／[:1403](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L1403)）；**只把"视野分桶键"换成 `kingdomId`**，`Faction` 一个字节不改语义。 |
| **M5** | `SimBrain` 调用点全库 grep 清单 | **已给**（E6）：`IsExplored` **1 调用点**（`SimBrain.cs:377`）；`RevealCircle` 外部 **0**；`FogEnabled` **1**；`_fogExplored` **5（全在 `SimWorld`）**。设计稿 §四 逐点列改造面，签名变更后由**编译器**天然兜底（漏改即编译失败）。 |
| **M6** | 若引新场景走 `D-013 §四` 五步纪律（含位移声明） | 设计稿 §七 给**两案**请裁：**A（推荐）**＝新场景落**探针目录**、**不进池／不进 suite／不重建锚** ⇒ 五步中"生成→构成冻结→重建锚"**不适用**，但**仍出显式"位移声明（不改锚·不进池）"**以保留纪律精神；**B**＝进 v9 池 ⇒ 走**全套五步**（生成→位移声明→构成冻结→重建锚→门禁②自洽）。倾向 A：`HH.229` 已有"探针性质、不进现有 suite"先例（`harness/Scenarios/escape_eval/e01_envelop_left_heavy.json`，见 `15_账本` L45 行）。 |
| **M7** | 决策核只一份源码 | 按推荐口径（**不动 `harness/Core/**`**）⇒ 该原则恒成立（不存在第二份）。若裁定动 `Core` ⇒ 必须双端**同一源文件、同 commit**，禁各自拷贝。 |
| **M8** | 承 `L-23`/`L-29`/`L-30`/`L-34`/`L-35` | 逐条落点：**`L-23`（构成变体）**＝本批"阵营构成变体"清单化＝〔单国 AI／多国 AI／玩家+多国／无 AI〕四变体，至少正反各一实测（设计稿 §七 三变体）；**`L-29`（检索假阴性）**＝本回执全部"零命中"结论均用本项目 `Grep` 工具并附**阳性对照**（如 `AiKingdom` 检索用已知命中样本 `probe_selfdef.json` 验证有效）；**`L-30`（判定线可达性先验）**＝设计稿 §六 给**两列 gap 表**（〔前置条件在场性·签发前必闭〕vs〔施工后可达性〕，`D704` 口径）；**`L-34`（长局在线判据）**＝本批为**短程探针**（不涉长局）⇒ 不触；探针跑批若引用读数须给"可判定最早 tick／命中即停"，已在设计稿 §七 给列；**`L-35`（口径来源与排除项）**＝设计稿 §七 每条读数写明口径来源与排除项（如"AI 可见性"只数**雾过滤后**的感知列表，排除"几何半径内但未探索"者）。 |

---

## 三、`sim-sync` 义务初判

| 面 | 初判 | 依据 |
|---|---|---|
| **共享决策核（`AI.Core` ↔ `harness/Core`）** | **零改动 ⇒ 双端镜像 MD5＝全等（改动前后一致）** | 推荐口径下 `kingdomId` 不入 `ProfessionSnapshot`（设计稿 §一）；改动面全在 `harness/Sim/**`（`sim-sync §一` 表：sim 模拟层为训练侧专用，Unity 无对应文件） |
| **`harness/KingdomBrain/**`** | **零改动** | 本批不消费国情快照；`kingdomId` 口径来源复用其既有语义（E7），不新增字段 |
| **三方同步（champion／`factor_registry`／SO）** | **零义务** | 本批**不新增可调参数**（`kingdomId` 非可训参数、`enableFog` 为场景开关非训练参数）⇒ 无 champion／registry／SO 变更 |
| **Unity 后适配义务（北极星反向）** | **须登记**（归**放行后施工报告**，本回执先声明） | `sim-sync §三`：sim 侧先行的语义＝Unity 后适配规格 ⇒ 施工报告须在 `15_训练侧harness与Unity端差距文档.md`「一·补」**追加一条**（per-kingdom 视野真源语义：分桶键／视野半径／过滤规则／`enableFog` 开关语义；Unity 适配点＝`VisionSystem` per-kingdom ＋ `kingdomId` 来源），并更新该文档「一·补」现有"迷雾视野（小区块探索标记）"行的状态 |
| **若裁定 C-1=B（入共享核）** | 义务**升级**：双端同源 ＋ Unity `ToSnapshot()` 同步 ＋ `15_账本` U 项登记 | 见设计稿 §一 备选案 |

---

## 四、守线证据（本回执）

- **零业务代码改动**：未 Edit 任何 `.cs`／`.json`（`harness/**` 与 `Assets/**` 均未写）。
- **零跑批／零进局**：未执行 `dotnet`／`benchmark`／`determinism`／任何 Unity 进局。
- **只读手段**：`Read`／`Grep`／`Glob` ＋ 1 次文件行数统计（`Measure-Object`，只读）。
- **落盘物**：本回执 ＋ 口径设计稿（2 文件）；另按 `vr-triage-flow §九`／`vr-id-ledger` 追加 `_交接索引.md` 登记行。

---

## 五、下一步（依赖主策划端裁）

1. **请裁项 C-1~C-5**（见设计稿 §九）：载体取向／场景方案／`enableFog` 开关载体／读数出口／门禁②计数口径。
2. **放行后**（第二步·归**训练师**）：按裁定口径施工 ＋ 门禁四件 ＋ 双端对账 ＋ `15_账本` 登记，交付报告＝按账本实时水位线取号（`D640 #10`）。
3. **未放行**：不动 `harness/Core/**`／`harness/KingdomBrain/**`；本批停在设计稿态。

---

## 附、证据索引（本回执引用的全部 `file:line`）

| 对象 | 锚点 |
|---|---|
| 探索桶（长度 2） | [SimWorld.cs:220-222](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L220-L222)／[:279](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L279) |
| `IsExplored`／越界分支 | [SimWorld.cs:239-249](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L239-L249) |
| `RevealCircle`／`RevealUnitVision` | [SimWorld.cs:251-271](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L251-L271) |
| tick 序（先标视野再刷感知） | [SimWorld.cs:908-910](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L908-L910) |
| 雾过滤调用点 | [SimBrain.cs:370-377](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimBrain.cs#L370-L377) |
| `Faction` 枚举（8 值） | [Faction.cs:19-31](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Core/Ports/Faction.cs#L19-L31) |
| `SimUnit.Faction` | [SimUnit.cs:58](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimUnit.cs#L58) |
| `SimUnitSpec` | [SimScenario.cs:70-81](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L70-L81) |
| 场景覆盖面（仅 cellSize/professions/tuning） | [SimScenario.cs:136-147](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L136-L147) |
| `enableFog` 声明 | [SimConfig.cs:41-45](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimConfig.cs#L41-L45) |
| v9 场景阵营样例 | [W9_T01_20260907_0.json:8-15](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Scenarios/v9/W9_T01_20260907_0.json#L8-L15) |
| AiKingdom 探针场景 | [probe_selfdef.json:77-81](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Scenarios/v9_probe/probe_selfdef.json#L77-L81)／[probe_kingdom_calm.json:6-10](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Scenarios/v9_probe/probe_kingdom_calm.json#L6-L10) |
| 锚（在场） | `ai决策大脑强化训练/results/baseline/v9/report.json`（`meta`／`score.total`） |
| 门禁四件口径 | [00_训练体系主文档.md:215-219](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/00_训练体系主文档.md#L215-L219) |
| `KingdomBrain` 镜像层 kingdomId | [SituationSnapshot.cs:15](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/KingdomBrain/SituationSnapshot.cs#L15)／[:38](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/KingdomBrain/SituationSnapshot.cs#L38)／[:113-119](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/KingdomBrain/SituationSnapshot.cs#L113-L119) |
| 共享核职业快照（双端） | [harness/Core/Config/ProfessionSnapshot.cs:29-31](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Core/Config/ProfessionSnapshot.cs#L29-L31) ↔ [Assets/.../AI.Core/Config/ProfessionSnapshot.cs:29-31](file:///c:/Users/trs/Desktop/Valley%20Rampart/valley%20rampart/assets/_Game/Systems/AI.Core/Config/ProfessionSnapshot.cs#L29-L31) |

---

> 落盘：训练端｜2026-09-15｜**占 HH.286 号·不另取号**｜配套＝[口径设计稿（报裁）](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/执行端/HH.286_G3-1per-kingdom视野_sim侧先行批_口径设计稿_报裁.md)
> **未 push**；本批相关路径 `git status` 仅本回执＋设计稿＋索引登记行
