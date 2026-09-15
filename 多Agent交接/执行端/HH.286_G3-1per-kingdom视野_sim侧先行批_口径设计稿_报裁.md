# HH.286 「`G3-1` per-kingdom 视野 · sim 侧先行批」口径设计稿（**报主策划端裁**）

> 类型：口径设计稿（**第一步产物·缺一不裁**）｜状态：🟡 **待裁（未放行不得开工第二步）**
> 日期：2026-09-15 ｜ 出品：**训练端策划**（训练轨设计权威）｜裁决端：**主策划端**（跨域·`3.1 §七 P-007`）
> 取号：随 **HH.286**（不另取号）；配套＝[HH.286_..._开工回执.md](file:///c:/Users/trs/Desktop/Valley%20Rampart/多Agent交接/执行端/HH.286_G3-1per-kingdom视野_sim侧先行批_开工回执.md)（实读复核 E1~E7 在本稿中被反复引用）
> 门卡片原文（`3.1 §三 G3-1`）：「`VisionSystem` per-kingdom 分桶＋`IsExplored(pos,kingdomId)`＋AI 受迷雾（**sim 情形A**：sim 真源→Unity 后适配，半径对齐 `Profession.perceptionRadius`）｜**静态**：探索集 per-kingdom；**正负探针**：k1/k2 交集空／AI 不读玩家集／`enabled=false` 基线不破；**双端 MD5**」
> 教训引用：`L-23`／`L-29`／`L-30`／`L-34`／`L-35`（`M8`）＋`L-24`/`L-25`（判据三直读）

---

## 〇、摘要（结论速览）

1. **载体＝`SimUnitSpec` ＋ `SimUnit`（实例层），不入 `ProfessionSnapshot`（共享核）** ⇒ 双端同源义务＝**0**（门卡片"双端 MD5"判定线在"零差异"意义上满足），`NpcProfessionDef.ToSnapshot()` **无需同步**。
2. **`kingdomId` 不是新造概念**：`KingdomBrain` 镜像层已把它当核心键（`SituationSnapshot.KingdomId`），本批只是把它**贯通到 sim 战场视野层**。
3. **改造面主体必须多修一处**（回执 E1）：现状桶长硬编码 2 ⇒ `AiKingdom`/`Monster` 越界 ⇒ 开雾＝**AI 全盲**（非"AI 共享一张图"）⇒ 除"按 `kingdomId` 分桶"外**必须解除桶长度钳制**。
4. **判定线 ②③ 的前置在当前结构性缺失**（回执 E3/E4）：`enableFog` 无开关入口、现役池零多王国同屏 ⇒ **必须补"场景级开关 ＋ 多王国同屏探针场景 ＋ 只读读数出口"三件**，否则 `L-30` 违规（施工后才验＝必然白工）。
5. **本稿一律不动 `harness/Core/**` 与 `harness/KingdomBrain/**`**；改动面全在 `harness/Sim/**` ＋ 探针场景 ＋（新增）只读探针出口。
6. **请裁项 5 条**（§九 C-1~C-5），其中 **C-1 载体取向／C-2 场景方案** 是放行开关。

---

## 一、① `kingdomId` 的载体

### 1.1 推荐（**A 案**）：`SimUnitSpec`（场景输入）＋ `SimUnit`（运行时）

| 层 | 落点 | 形态 | 语义 |
|---|---|---|---|
| 场景输入 | `SimUnitSpec`（[SimScenario.cs:70-81](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L70-L81)） | 新增 `public int KingdomId = -1;` | `-1`＝未标注（走 §二 派生兜底） |
| 运行时 | `SimUnit`（[SimUnit.cs:13-58](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimUnit.cs#L13-L58)） | 新增 `public int KingdomId;`（构造自 `spec`） | `0`＝玩家国；`>0`＝AI 王国；`-1`＝未标注 |
| 视野分桶 | `SimWorld` 探索集 | 键类型由 `(int)Faction` → `kingdomId` | 见 §四 |
| 消费 | `SimBrain.UpdatePerception` | `IsExplored(_self.KingdomId, e.Position)` | 见 §四 |

**理由（四条）：**

- **a) 语义层级正确**：`kingdomId` 是**单位实例**属性（同一职业可属不同王国），而 `ProfessionSnapshot` 是**职业类**快照。虽 `faction` 因历史原因栖身其中并逐单位拷贝覆写（[SimScenario.cs:166-171](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L166-L171)），但**不应扩大这类混装**（`L-28` 家族：分类字段默认值/载体错位是沉默误判源头）。
- **b) 北极星成本最低**：入 `Core` ⇒ Unity 必须加同名字段，而 Unity `NpcProfessionDef` 是**职业级资产**（无王国归属）⇒ 只能造"伪同源字段"或改 Unity 资产模型 ⇒ 违 `sim-sync` 北极星（禁伪同源／禁各自拷贝）。
- **c) 判定线"双端 MD5"更强**：`Core` **零改动** ⇒ 双端镜像**天然全等**（可取证"改动前后一致"），比"新字段两端一致"更省且无后患。
- **d) 与场景侧先例同构**：`faction` 的**覆盖**发生在场景加载（同 a 锚点），`kingdomId` 同理属"场景→实例"层；`ScenarioGenV2`/`CardPool` 目前只写 `faction`（回执 §一 行 7），新增字段完全向后兼容。

### 1.2 备选（**B 案**·不推荐）：入 `ProfessionSnapshot`（共享核）

- 落点：[harness/Core/Config/ProfessionSnapshot.cs:29-51](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Core/Config/ProfessionSnapshot.cs#L29-L51) 新增 `int kingdomId`。
- **代价链**：① 双端镜像 MD5 义务（`sim-sync §六` 铁律：`AI.Core` ↔ `harness/Core` 同源文件逐字一致）；② Unity `NpcProfessionDef.ToSnapshot()` 须同步（Unity 侧**本批零触碰** ⇒ 只能另批闭环，本批双端不可能自洽）；③ **填充来源缺失**：Unity 侧 `NpcProfessionDef` 无王国归属 ⇒ 新增字段只能恒填默认值 ⇒ 造"伪同源"；④ `15_账本` U 项登记义务。
- **倾向**：**不做**。若主策划端有"必须入核"的理由（如需 Unity 层同名映射），请回裁并同时裁定 Unity 侧同步批，本批转"入核前置待批"。

---

## 二、② 来源与填充 ＋ 最小验证场景

### 2.1 来源链（唯一链路·无第二真源）

```
场景 JSON units[].kingdomId（可选）
  → SimScenarioDoc.units[].kingdomId（新增可选字段）
  → SimUnitSpec.KingdomId（缺省 -1 时按 §2.2 派生）
  → SimUnit.KingdomId
  → SimBrain: IsExplored(_self.KingdomId, …) ／ SimWorld: RevealCircle(u.KingdomId, …)
```

### 2.2 缺省派生兜底（**保零回归 ＋ 保旧语义**）

| `Faction` | 缺省 `kingdomId` | 说明 |
|---|---|---|
| `PlayerCamp` | `0` | 对齐 Unity 约定（玩家国 id=0） |
| `AiKingdom` | `1` | 未标注 ⇒ 单国兜底（**等价于旧"per-Faction"语义，不更差**） |
| `Monster` | `-1` | 野怪/传送门怪物：非王国，单一兜底桶 |
| `None`／未知 | 不揭示·不可感知 | 保留现状语义（[SimWorld.cs:247](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L246-L248) 的 `return false` 分支语义不废） |

> **显式标注优先**；派生仅在字段缺省时生效。`enableFog=false`（默认）时派生**无行为影响**（短路返回）。

### 2.3 最小验证场景（**现役 suite 不可判**·回执 E4）

- **现役实况**：`harness/Scenarios/v9/` 63 场景全 `PlayerCamp` vs `Monster`；全仓 `AiKingdom` 仅 2 个探针场景、各 **1** 个 AI 王国 ⇒ **无 k1/k2** ⇒「k1/k2 交集空」结构性不可判（`L-30`/`M1`）。
- **结论**：**必须新增"多王国同屏"最小验证场景**（三变体·详见 §七）。这不是"为过判据特制场景"——门卡片判定线**本身要求 k1/k2 存在**，场景是**判据的必要前置**；且本稿配**两个阴性对照**防"只证想要的结果"。
- **放置与池的关系**：见 §九 **C-2**（推荐 A＝探针目录·不进池/不进 suite/不重建锚）。

---

## 三、③ 双端同源对账（Unity `NpcProfessionDef.ToSnapshot()` 是否需同步 · 双端 MD5 口径）

### 3.1 结论

- **按 A 案（推荐）**：`ToSnapshot()` **不需要同步**（`ProfessionSnapshot` 零改动）⇒ 双端同源义务＝**0**；门卡片"双端 MD5"判定线按「**改动前后逐文件全等**」满足。
- **按 B 案**：须同步 + `15_账本` U 项登记 + Unity 另批（见 §1.2）。

### 3.2 双端 MD5 口径（供施工取证·**训练师执行**）

| 项 | 口径 |
|---|---|
| 同源清单 | `harness/Core/**/*.cs` ↔ `Valley Rampart/Assets/_Game/Systems/AI.Core/**/*.cs`（**同相对路径同名文件**） |
| 工具/命令 | `pwsh`：`Get-FileHash -Algorithm MD5`（逐文件）；输出"相对路径 / 哈希 / 两侧是否相等" |
| 判据 | ① 两侧**文件集合一致**（无新增/删除）；② 逐文件哈希**全等**；③ 本批应给出**改动前/改动后两份表**并证明**两份一致**（A 案下这是必然结果，用作证伪手段：若不一致 ⇒ 说明误改了核） |
| 落点 | 交付报告 §证据 ＋ `15_训练侧harness与Unity端差距文档.md`（登记"本批 `Core` 零改动"一行） |

### 3.3 Unity 后适配义务（`sim-sync §三` 反向登记·归放行后施工报告）

施工报告须在 `15_账本`「一·补」**追加一条**，内容＝sim 侧先行语义真源：①分桶键＝`kingdomId`（0=玩家／>0=AI 国／-1=野怪，缺省派生表）②视野半径＝`Profession.perceptionRadius × cellSize`（**已对齐**，不动）③过滤规则＝「未探索区敌不可见」（只看已探索集）④开关语义＝场景级 `enableFog`（默认 false 恒可见）⑤Unity 适配点＝`VisionSystem` per-kingdom ＋ `kingdomId` 来源（UnitController／KingdomState）。同时更新该文档现有"迷雾视野（小区块探索标记）"行状态。

---

## 四、④ 数据结构改造面逐点清单（逐点 · 含级别）

> 级别按 `sim-sync §六` 判定；**主体按 F 级**（保守）⇒ 双门禁全跑。所有落点均在 `harness/Sim/**`（`Core`／`KingdomBrain` **零触碰**）。

| # | 落点（现状 file:line） | 目标改法 | 级别 | 备注 |
|---|---|---|---|---|
| 1 | `SimWorld._fogExplored`＝`HashSet<long>[2]`（[SimWorld.cs:220-222](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L220-L222)／构造 [:279](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L279)） | 改 **`Dictionary<int, HashSet<long>>`（键＝`kingdomId`，按需建桶）** | **F** | **必改**：现状桶长 2 ⇒ `AiKingdom(3)`/`Monster(4)` 越界（回执 E1）。亦可是 `List`＋按 `maxKingdomId` 扩容，但需保证无长度上界钳制 |
| 2 | `IsExplored(Faction f, Vector2X p)`（[:243-249](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L239-L249)） | 签名改 **`IsExplored(int kingdomId, Vector2X p)`**；保留 `!enableFog → true` | F | 保留"未知/无桶 ⇒ false"语义（`None` 不可感知） |
| 3 | `RevealCircle(Faction f, …)`（[:252-264](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L251-L264)） | 签名改 **`RevealCircle(int kingdomId, …)`**；保留 `!enableFog → return` | F | 半径公式**不动** |
| 4 | `RevealUnitVision(u)`（[:271](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L271)） | 改 **`RevealCircle(u.KingdomId, u.Position, u.Profession.perceptionRadius * _config.cellSize)`** | F | 半径已对齐设计（门卡片明文） |
| 5 | `SimWorld` 构造桶初始化（[:279](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L279)） | 随 #1 改造（Dictionary 无需预建；若预建以场景 `max(kingdomId)` 为准） | T | 纯结构 |
| 6 | `SimBrain.UpdatePerception` 雾过滤（[SimBrain.cs:370-377](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimBrain.cs#L370-L377)） | `IsExplored(myFaction, e.Position)` → **`IsExplored(_self.KingdomId, e.Position)`** | **F** | **`myFaction` 保留**用于 `QueryNearby` 敌对判定（`M4` 双轨） |
| 7 | `SimUnit`（[SimUnit.cs:13-58](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimUnit.cs#L13-L58)） | 新增 `public int KingdomId;` ＋ 构造自 `spec.KingdomId` | T | 纯新增字段 |
| 8 | `SimUnitSpec`（[SimScenario.cs:70-81](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L70-L81)） | 新增 `public int KingdomId = -1;` | T | 纯新增字段 |
| 9 | `SimScenarioDoc.units` 解析（[SimScenario.cs:157-186](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L157-L186)） | 新增可选 `kingdomId` 解析 ＋ **§2.2 派生兜底** | F | 派生只影响"开雾"路径（保守归 F） |
| 10 | `SimConfig.enableFog` **开关入口**（[SimConfig.cs:45](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimConfig.cs#L41-L45)；`SimScenario.Load` [:136-147](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimScenario.cs#L136-L147) 现仅覆盖 cellSize/professions/tuning） | 场景 JSON 顶层新增可选 `enableFog`（与 `worldModel` 同级先例），`Load` 内应用 | T | **回执 E3**：当前**零入口** ⇒ 判定线"`true` 门"结构性不可达。默认 false ⇒ 零回归 |
| 11 | **只读探针出口**（新增） | `SimWorld` 增只读访问器：`FogExploredCount(int kingdomId)`／`FogOverlapCount(int a, int b)`／`FogBucketKeys()`；新增 CLI 子命令 `fog-probe --scenario <p> [--ticks K]`（建局→跑 K tick→逐 tick 转储→停） | T | **判定线 ②③ 的读数可达性**（`L-30`）。**不新增 `report.json` 字段**（避免扰锚对照面） |
| 12 | 不动面（**确认零连带**） | `CheckEnd`／参与阵营集（[SimWorld.cs:217-218](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L217-L218)／[:1403](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L1403)）、`SimPerception.QueryNearby`、`ScenarioGenV2`／`ScenarioGenerator`／`CardPool` | — | 生成器本批**不改**（缺省派生兜底即可）；胜负/敌对仍按 `Faction` |

**调用点清单（`M5`·实测）**：`IsExplored` 1 处（`SimBrain.cs:377`）；`RevealCircle` 外部 0 处；`FogEnabled` 1 处（`SimBrain.cs:376`）；`_fogExplored` 5 处（全在 `SimWorld`）⇒ 签名变更由**编译器兜底**。

---

## 五、⑤ 零回归口径（**双门**）

### 5.1 `enableFog=false` 门（基线不破）

- **结构保证**：`IsExplored` 短路返 `true`（[:245](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L244-L245)）、`RevealCircle` 首个 `return`（[:254](file:///c:/Users/trs/Desktop/Valley%20Rampart/ai决策大脑强化训练/harness/Sim/SimWorld.cs#L253-L254)）；`kingdomId`／`enableFog` 缺省值**不进入任何公式/评分/决策路径**。
- **门禁四件（`00 v2 §8.3`·命令口径与 `AGENTS.md` 铁律6 一致）**：
  1. `dotnet build harness -warnaserror`：0 警告 0 错误；
  2. `determinism`：**2D 新场景**同 seed 逐字节一致（建锚 3/3）；
  3. **对照口径** `benchmark --suite v9 --battles 100`（**读锚不写锚**·`D635` A′）：判无场景退化；**执行前置＝锚在场**（已实读满足·回执 E5：`total 0.422`／100 局／`seed -1283596485`／`2026-09-12`）⇒ 不会落"首次运行＝建档写锚"分支；
  4. `holdout_v9` 全绿（≥4 场景）。
- **逐场景判据**：**锚键集内共有场景**逐场景 `subScore` **Δ≤0.0005**、无场景退化 >5%（计数表述见 5.3）。

### 5.2 `enableFog=true` 门（成立）

- **前置**＝§四 #10 场景级开关（当前零入口·回执 E3）＋§四 #11 读数出口。
- **判据**＝探针三变体读数（§七）：F1 交集空 ＋ AI 不读玩家集；F2/F3 阴性对照成立（证明读数有效）。

### 5.3 勘正（请裁 **C-5**）：门禁② 场景计数口径统一

- 任务书 §二⑤／§三 线3 写「**148/151** 锚」；`00 v2 §8.3` 原文写「**144** 共有场景」。
- 实况：**151**＝T09 重构成后池内场景总数；**148**＝`benchmark` 对照的**共有**场景数；`00 §8.3` 的 144 系早于 T09/T10 两次重建锚的**旧值**。
- **建议**：验收线表述统一为「**锚键集内逐共有场景** `subScore Δ≤0.0005`」，键数**施工时以 `report.json.score.subScores` 实读并贴出**，不引用文档旧数。

---

## 六、⑥ 改造后可达性先验（`L-30` 两列 gap 表）

> 两列口径＝`D704`／`L-30` 补维度③：〔**前置条件在场性**｜签发前必闭〕vs〔**施工后可达性**｜可留施工后实证〕。**前置未闭而留到施工后验 ⇒ 必然白工**。

| 判定线硬条件 | 前置条件在场性（签发前必闭） | 施工后可达性 | 结论 |
|---|---|---|---|
| ① 探索集 **per-kingdom** 分桶 | ✅ 结构可改（`SimWorld` 私有面·`Sim/` 训练师全权面） | 施工后实证（读 `FogBucketKeys()`＝场景内 `kingdomId` 键集） | **本轮可闭** |
| ② **k1/k2 交集空** | 🔴 **缺失**：现役池零多王国同屏（回执 E4） ⇒ 须先落 §七 探针场景 | 施工后（探针读数，前 K tick 逐 tick） | **须随本批补前置**（否则不可判） |
| ③ **AI 不读玩家集** | 🔴 **缺失**：`SimWorld` 探索集**无公开访问器**（当前 `private`） ⇒ 无读数出口 | 施工后（§四 #11 出口 ＋ §七 口径 3） | **须随本批补前置** |
| ④ `enabled=false` 基线不破 | ✅ 锚在场（E5·`report.json` 实读） | 施工后（门禁②读数） | **本轮可闭** |
| ⑤ `enableFog=true` 生效 | 🔴 **缺失**：`enableFog` **零设置入口**（E3） | 施工后（§四 #10 开关） | **须随本批补前置** |

**结论**：②③⑤ 三条的**前置在场性必须在施工前补**（＝本批施工面的三件：**探针场景 ＋ 场景级开关 ＋ 只读读数出口**），并在放行时一并授权，否则本批交付物空心化且触 `L-30`。

---

## 七、场景方案与读数口径（正负探针设计）

### 7.1 三变体（**正例 1 ＋ 阴性对照 2**）

| 变体 | 建议文件（探针目录） | 构成 | 期望读数（本批判定线） |
|---|---|---|---|
| **F1 远距三国（正例）** | `harness/Scenarios/v9_probe/probe_fog_k2_far.json` | `k0` 玩家×2（`PlayerCamp`,`kingdomId:0`）／`k1` 王国A×2（`AiKingdom`,`kingdomId:1`）／`k2` 王国B×2（`AiKingdom`,`kingdomId:2`）；三国间距 **> 各自视野半径之和**（半径＝`perceptionRadius × cellSize`，构造性保证初始不重叠） | ① 分桶键＝{0,1,2}；② `k1∩k2`＝**空**；③ `k1`/`k2` 的 `_nearbyEnemies` 中 `kingdomId==0` 计数＝**0**（玩家在 AI 未探索区） |
| **F2 近距重叠（阴性对照·读数有效性）** | `probe_fog_k2_overlap.json` | 同构成，但 `k1`/`k2` 拉到**视野重叠** | `k1∩k2` **非空** ⇒ 证明读数能报"非空"（`L-29` 阳性对照，防假阴性） |
| **F3 玩家入视（阴性对照·可见路径）** | `probe_fog_player_in_sight.json` | 同 F1，但把玩家单位放进 `k1` 视野内 | `k1._nearbyEnemies` 含 `kingdomId==0` ⇒ 证明"AI 读不到玩家"不是读数恒 0 |

> **F1 的"远距"是构造性的**（不靠运气）；**F2/F3 是探针有效性对照**，不是"再造一个想要的结果"。
> 若三国单位会相向移动，**交集空只在早期成立** ⇒ 判定窗口＝**前 K tick**（K 须 ≥ 覆盖 ≥1 次感知刷新；`perceptionUpdateInterval` 实读后取整，取值与理由须写入交付报告）。

### 7.2 读数口径与排除项（`L-35` 硬性检查项）

| # | 读数 | 口径来源 | **排除项** |
|---|---|---|---|
| R1 | 探索集分桶 | `FogBucketKeys()`（场景内出现过的 `kingdomId` 键集） | **不数 `Faction` 键**；不数未揭示过的空桶 |
| R2 | `k1∩k2` | `FogOverlapCount(1,2)`，**前 K tick 逐 tick 采样，取序列最大值** | **不含 K tick 之后**（单位相向移动致重叠不属机制问题）；不跨局数/跨 seed 混比（`L-35` 家族） |
| R3 | AI 可见性 | AI 单位**雾过滤后**的 `_nearbyEnemies` 中 `kingdomId==0` 计数 | **不数几何半径内总数**（被雾滤掉者不计入"可见"） |
| R4 | `enabled=false` 基线 | 锚键集共有场景逐场景 `subScore`（同 seed·同局数 `@100`） | 不与 `@30` 等其它局数比（`L-35` 家族：禁跨局数混比） |

### 7.3 探针跑批纪律

- 探针为**短程**（建局＋跑 K tick），**不涉长局** ⇒ `L-34` 在线判据表不适用；但**若**放行后改跑长局验证，须补"可判定最早 tick／命中即停"（红线）。
- 探针**不进 suite／不进池／不写锚**；读数落 stdout ＋（可选）`results/**`，**不新增 `report.json` 字段**。

---

## 八、施工序与门禁（**放行后·归训练师执行**）

1. 改前 `git commit` 存档（训练仓干净回滚点）→ 2. 按 §四 清单施工（`harness/Sim/**`）→ 3. 三变体探针场景落盘 → 4. `dotnet build harness -warnaserror` 0/0 → 5. `fog-probe` 三变体读数（判定线 ①②③＋阴性对照）→ 6. `benchmark --suite v9 --battles 100`（**读锚不写锚**）判共有场景退化 → 7. `determinism` 3/3（**含 2D 新场景**）→ 8. `holdout_v9` 全绿 ≥4 → 9. 双端 MD5 前后对照表（§3.2）→ 10. `15_账本`「一·补」追加 Unity 适配义务登记（§3.3）→ 11. 交付报告（按账本实时水位线取号）＋ 具名 `git add` ＋ **未 push** ＋ 本批路径 `git status` 空。

**验收线映射（任务书 §三）**：线2（per-kingdom 成立）←§四＋§七；线3（零回归）←§5.1；线4（双端同源）←§三；线5（构建与确定性）←步骤 4/7；线6（写后验）←步骤 11。

---

## 九、请裁项（**5 条**）

| # | 请裁 | 选项 | 训练端倾向 |
|---|---|---|---|
| **C-1** | **载体取向** | **A**＝`SimUnitSpec`＋`SimUnit`（`Core` 零改动·双端零义务·本批可自洽闭环）／**B**＝入 `ProfessionSnapshot`（须双端同源＋Unity `ToSnapshot()` 同步＋15 账本 U 项·**Unity 本批不得触碰 ⇒ 另批**） | **A**（§1.1 四条理由） |
| **C-2** | **场景方案** | **A**＝新增探针场景落**探针目录**、**不进池／不进 suite／不改锚**，出**显式"位移声明（不改锚·不进池）"**（五步中"构成冻结/重建锚"不适用）／**B**＝进 v9 池 ⇒ 走 `D-013 §四`**全套五步**（生成→位移声明→构成冻结→**重建锚**→门禁②自洽） | **A**（先例：`escape_eval/e01_*`"验证性质、不进现有 suite"；B 会动锚，与"零回归"验收线互相拉扯） |
| **C-3** | **`enableFog` 开关载体** | **A**＝场景 JSON 顶层可选字段（与 `worldModel` 同级先例·场景自描述·缺省 false）／**B**＝CLI 参数／**C**＝两者都加 | **A**（探针可复现性最好；B 亦可，但场景不自描述） |
| **C-4** | **只读读数出口** | **A**＝新增只读 CLI 子命令 `fog-probe` ＋ `SimWorld` 只读访问器，**不新增 `report.json` 字段**／**B**＝扩展现有 `smoke`／`determinism` 输出 | **A**（不扰动锚对照面；纯新增只读，零行为） |
| **C-5** | **门禁② 计数口径** | 统一为「**锚键集内逐共有场景** `subScore Δ≤0.0005`」＋施工时实读键数（处理任务书"148/151" vs `00 §8.3`"144" 的漂移） | 采纳本统一口径（§5.3） |

> 另请知悉（非请裁·事实勘正）：**回执 E1**（B 桶长 2 ⇒ 开雾＝AI 全盲，非"AI 共享一张探索图"）＋**E3/E4**（`enableFog` 零入口／现役零多王国同屏 ⇒ 判定线 ②③⑤ 前置缺失）。若不采纳 C-2/C-3/C-4 的补前置，则本批**只能交付判定线 ①④**，须在任务书层面下调验收范围（不建议）。

---

## 十、本批不做 / 顺带可接项

- **本批不做**：`harness/Core/**`／`harness/KingdomBrain/**`（红线）；`champion`／`Holdout`／`AGENTS.md`／`FactorRegistry`（禁改领域）；主仓 `Assets/**`（Unity 后适配·另批）；`ScenarioGenV2`/`CardPool`/`ScenarioGenerator`（本批不改·缺省派生兜底）。
- **顺带可接（**本批不做**·建议另挂账）**：`RunTrain` 硬编码 `BuildSuiteV8` ＋ `champion` json tuning 段缺**野性 9 参数**（`15_账本`"一·补十·在册欠账"，原文理由"防训练跑错套件"）。**理由不进本批**：①与 `G3-1` 无关；②涉 `RunTrain`／champion 旁区＋三方同步，串做会**污染门禁②归因**（`M2` 零回归判读需要单一变量）。

---

> 落盘：训练端策划｜2026-09-15｜随 **HH.286**｜**未 push**｜未取 TD（跨域报裁·`P-007`）

---

## 十一、主策划端裁决区（`D731`，2026-09-15，主策划端）

> 裁决文号 **D731**（`0.6 §二百五十九`）· Gate=`G3-1` · **第一步（口径设计稿）＝✅ 验收成立 · 5 项请裁全准 · 放行第二步（归训练师）**

### 11.1 设计稿验收（✅ 成立 · 质量嘉奖）

**判据三直读已过**：①**E1 策划端独立复读＝成立**（`SimWorld.cs:220-222` 实读 `new HashSet<long>[2]` ＋ 注释 `// 索引 = (int)Faction（Human/Undead）` ＝ **1D 时代两阵营遗留硬编码**；`AiKingdom(3)`/`Monster(4)` 越界 ⇒ `:247` 恒 `return false`〔不可见〕＋ `:256` `RevealCircle` 直接 `return`〔从不标记〕⇒ **开雾实况＝"玩家有雾、AI 全盲"**）；②`E3`/`E4`/`E6`/`E7` 抽验一致；③**`E2` ＝ 策划端自认口径不清**（见 11.2 C-5）。

> ⚠️ **策划端自曝（`L-24` 家族·本串又一实例）**：`HH.286` 任务书 §一 结论「多 AI 王国共用 `Faction.AiKingdom` ⇒ 退化为所有 AI 共享一张探索图」**系误判**——**漏读紧邻的 `SimWorld.cs:222` 长度行**（只读到 `:221` 声明头即下判）。**方向（须补 `kingdomId`）不变，但改造面更大**（必须先**解除桶长度钳制**）。**已勘正任务书 §一**（见 11.4）。**训练端 `E1` ＝ 正向勘正，入正面样本。**

### 11.2 5 项请裁（**全准**）

| # | 裁定 | 理由 |
|---|---|---|
| **C-1** | **准 A**：载体＝`SimUnitSpec` ＋ `SimUnit`，**不入 `ProfessionSnapshot`**（`Core` 零改动） | 四条理由全部成立，**其中 b) 是决定性的**：入 `Core` ⇒ Unity `NpcProfessionDef` 是**职业级资产**（无王国归属）⇒ 只能造**伪同源字段** ⇒ **违 `sim-sync` 北极星**「禁伪同源／禁各自拷贝」。**原任务书把 `ProfessionSnapshot` 列为选项之一 ＝ 策划端选项集设计不当**（已勘正）。 |
| **C-2** | **准 A**：探针场景落**探针目录** · **不进池／不进 suite／不改锚**；**仍须出显式位移声明**（写明"不改锚·不进池"及理由） | 与"零回归"验收线无拉扯（B 会动锚）。位移声明本身即"为何无需重建锚"的取证 ⇒ 符合 `D-013 §四` 精神（**该声明是必出项·非可选项**）。 |
| **C-3** | **准 A**：`enableFog` 走**场景 JSON 顶层可选字段** | 场景自描述·探针可复现。**附加约束**：①只读消费（`SimScenario.Load` 应用）②**不得影响 `enableFog=false` 默认路径** ③**须登记 `15_账本`**（sim-only 字段·Unity 无对应物 ⇒ 事实注记·零回灌义务）。 |
| **C-4** | **准 A**：新增只读 CLI `fog-probe` ＋ `SimWorld` 只读访问器，**不新增 `report.json` 字段** | `report.json` 是**锚对照面**，不得扰动。**附加约束**：该 CLI **不进 `RunTrain` 路径、不参与 `benchmark`**（纯独立子命令）。 |
| **C-5** | **准**：门禁②计数口径统一为「**锚键集内逐共有场景** `subScore Δ≤0.0005`」＋ 施工时**实读 `report.json.score.subScores` 键集并贴出**，**不引用文档旧数** | `E2` 成立：任务书「148/151」系策划端**未核准即写**（**151**＝池总数／**148**＝共有场景数／**144**＝`00 §8.3` 早于 T09/T10 两次重建锚的**旧值**）⇒ **已勘正任务书**（§二⑤／§三线3）。 |

> **`§九` 末尾"若不采纳补前置则本批只能交付判定线 ①④"＝判断正确。** 本次 **C-2/C-3/C-4 全准** ⇒ **「探针场景 ＋ 场景级开关 ＋ 只读读数出口」三件纳入本批施工面**，**不得下调验收范围**（否则交付物空心化且触 `L-30`）。

### 11.3 新立缺陷 `DZ-158`（**`DZ-149` 家族 · sim 侧新实例**）

- **`DZ-158`**：`harness/Sim/SimWorld.cs:220-222` `_fogExplored = new HashSet<long>[2]`（注释自陈「索引 = `(int)Faction`（**Human/Undead**）」）＝**1D 时代两阵营遗留**；2.5D 后 `Faction` 达 8 值，`AiKingdom(3)`/`Monster(4)` **越界** ⇒ 开雾时 **AI/野怪视野恒不可见（全盲）**。**性质＝`DZ-149`「1D 残留致全向功能失明」同族**（`DZ-149` 锚＝Unity 侧 `y∈{0,1}` 只扫最南两行；本条＝sim 侧桶长钳制）⇒ **本批（`HH.286`）修**。
- 🔴 **连带结论（族谱级·须记）**：`DZ-149` 立账时的「**全库**实读」**实为 `Assets/**` 全库，未含训练仓 `harness/**`** ⇒ **1D 残留普查存在仓级盲区**。⇒ **建议**：把「1D 残留」普查**扩到 `harness/**`**（另立扫描批，或随 `G3-1` 顺带扫），并在 `DZ-149` 条目**加注本盲区**。

### 11.4 任务书勘正（3 处·已改）

| # | 原表述 | 勘正后 |
|---|---|---|
| 1 | §一.2「多 AI 王国共用一个 `Faction.AiKingdom` ⇒ per-Faction 分桶在实战中退化为「所有 AI 共享一张探索图」」 | 改为「`_fogExplored` 桶长**硬编码 2**（1D 遗留）⇒ `AiKingdom(3)`/`Monster(4)` **越界** ⇒ `:247` 恒 `false`（不可见）＋`:256` 从不标记 ⇒ **开雾实况＝玩家有雾、AI 全盲**」；并**新增施工面**「**解除桶长度钳制**」（原任务书未含） |
| 2 | §二⑤「`suite_v9` 全 **148/151** 锚逐场景 `Δ≤0.0005`」 | 改为「**锚键集内逐共有场景** `subScore Δ≤0.0005`；键数**施工时实读 `report.json` 贴出**」 |
| 3 | §三线3「`benchmark --suite v9 --battles 100` … 逐场景 `Δ≤0.0005`（门禁②·含新场景不排除）」 | 同口径统一；并**补**「**前置＝锚在场**（`E5` 实读满足：`total 0.422`／100 局／2026-09-12）」 |

### 11.5 放行与后续

- **✅ 放行第二步**（施工 · 归**训练师**）：按 `§四` 12 点改造面 ＋ `§七` 三变体探针 ＋ **11.2 三件前置**（C-2 探针场景／C-3 场景级开关／C-4 读数出口）**一并施工**。
- **顺带项**（`RunTrain` 硬编码 `BuildSuiteV8` ＋ `champion` json tuning 段缺野性 9 参数）：**准 `§十` 判断——不进本批**（理由采纳：与 `G3-1` 无关 ＋ 会污染门禁②归因·`M2` 需单一变量）⇒ **主策划端另排挂账**。
- **Unity 后适配**：不属本批；`§3.3` 的 `15_账本`「一·补」登记为**施工报告必出项**。
