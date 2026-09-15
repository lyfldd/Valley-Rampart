# HH.286 `G3-1` per-kingdom 视野 · sim 侧先行批 任务书（**口径设计稿先行**）

> 类型：施工任务书（**分两步·设计稿先行**）｜状态：🟢 **已签发·待接单**
> 日期：2026-09-15 · 签发端：主策划端 · **Gate：`G3-1`（per-kingdom 视野）**
> 关联：`3.1 §三 G3-1` 门卡片／`§六` 队列行／`§七` 问题台（本批需求 **`P-007`**）／**`§八⑥ 冻结例外`**／`D677`（立项）／台账 **`DZ-143`**／`2_24 §六`／`15_训练侧harness与Unity端差距文档.md`（sim-sync 纪律）
> 取号：HH.286（账本水位线 285→286）
> 由来＝用户 2026-09-15 问「有什么活可以给训练端做」⇒ 主策划端实盘核出本批＝**训练端可独立开工且直接推进王国 AI 主线**的首选（`3.1 §四` 依赖矩阵：**`G3-2`/`G3-3` ← `G3-1`**）。
> **部署合法性**：`3.1 §三 G3-1` 门卡片原文已定「**sim 情形A：sim 真源 → Unity 后适配**」⇒ **训练端先行是设计指定分工**；且落在 `§八⑥` **冻结例外**内（sim 侧实现义务不受冻结限制）。

---

## 〇、一句话目标

把训练仓 sim 的视野/迷雾从 **per-`Faction`** 提升到 **per-`kingdomId`**，产出 **sim 真源**；Unity 侧（`VisionSystem` per-kingdom）留作**后适配**。

**完成标志**：`3.1 §三 G3-1` 判据「探索集 per-kingdom；k1/k2 交集空／AI 不读玩家集／`enabled=false` 基线不破」在 **sim 侧**成立。

---

## 一、策划端已取证（执行端复核·勿重推翻）

### 1.1 现状实读（sim 侧已有 per-Faction 迷雾，但**无 kingdomId 维度**）

| # | 落点 | 实读 | 含义 |
|---|---|---|---|
| 1 | `harness/Sim/SimWorld.cs:220-221` | `private readonly HashSet<long>[] _fogExplored` | 探索集**已是数组分桶**，但索引＝`Faction` |
| 2 | `harness/Sim/SimWorld.cs:240-248` | `FogEnabled => _config.enableFog`；`IsExplored(Faction f, Vector2X p)`；`if (!_config.enableFog) return true` | **按 Faction 查询**·`enableFog=false` 恒可见（零回归基线） |
| 3 | `harness/Sim/SimWorld.cs:254-271` | `RevealCircle/FogCellIndex`；`RevealUnitVision(u) => RevealCircle(u.Faction, u.Position, u.Profession.perceptionRadius * cellSize)` | 视野标记按 Faction·半径＝`perceptionRadius × cellSize` ✅ 已对齐设计 |
| 4 | `harness/Sim/SimWorld.cs:908-910` | `if (enableFog) IterateUnits(RevealUnitVision); IterateUnits(UpdatePerception)` | 先标视野再刷感知 |
| 5 | `harness/Sim/SimBrain.cs:375-377` | `if (_worldSim.FogEnabled && ...) _nearbyEnemies.RemoveAll(e => !_worldSim.IsExplored(myFaction, e.Position))` | 雾过滤**按 Faction** |
| 6 | **`harness/Sim/SimUnit.cs:58`** | `public Faction Faction => _prof.faction;` | 🔴 **sim 单位只有 `Faction`，无 `kingdomId`** |
| 7 | `harness/Sim/ScenarioGenV2.cs:323/773`、`CardPool.cs:905` | 场景构造只写 `faction`，无 kingdomId | 场景侧亦无该维度 |
| 8 | `harness/Sim/SimConfig.cs:42-45` | `enableFog`（默认 **false**＝过雾不生效）＋注释「**当前 suite 全同屏，开启后需配合"分区域大战场"场景才产生决策增益（专项）**」 | ⚠️ **关键前提：现役 suite 全同屏** ⇒ 开雾需配套场景 |

### 1.2 差距（本批要闭的洞）

- 设计要 **`IsExplored(pos, kingdomId)`**（按**王国**）；实读是 **`IsExplored(Faction, pos)`**（按**阵营**）。
- **且桶长硬编码 2（1D 遗留）** ⇒ `AiKingdom(3)`/`Monster(4)` **越界** ⇒ `:247` `IsExplored` 恒 `return false`（不可见）＋ `:256` `RevealCircle` 直接 `return`（**从不标记**）⇒ **开雾实况＝「玩家有雾、AI/野怪全盲」**。
  - ⚠️ **勘正（`D731`）**：原稿此处误作「多 AI 王国共用 `Faction.AiKingdom` ⇒ 所有 AI 共享一张探索图」——系**漏读紧邻的 `SimWorld.cs:222` 长度行**（只读到 `:221` 声明头即下判·`L-24` 家族）。**方向（须补 `kingdomId`）不变，改造面更大**（须先解除桶长度钳制）。**勘正来源＝训练端第一步设计稿 `E1`（正向勘正·入正面样本）**。
- ⇒ **本批实质＝给 sim 补 `kingdomId` 维度 ＋ 解除桶长度钳制**（⚠️ 结构性改动·非小改）。**两者为与关系**：不解除钳制 ⇒ 任何 `kingdomId` 键都会越界 ⇒ 分桶改造无意义（此点为 `D731` 勘正新增施工面）。

### 1.3 与 Unity 侧的对照（后适配面·本批不动）

`3.1 §三 G3-1` 现状原文：「全局单探索集（无 per-kingdom 视野）」⇒ Unity 侧为**后适配方**，本批只产 sim 真源。

---

## 二、范围（**两步·设计稿先行**）

### 第一步（本批必做·产出＝**口径设计稿**·报主策划端裁）

**⚠️ 必须先出设计稿**：本批要碰 `ProfessionSnapshot`（`harness/Core/**` ＝**共享决策核**）⇒ 按 `sim-sync` 红线，**跨仓动 Core/AI.Core 语义须主策划端放行**。

设计稿须答（缺一不裁）：

| # | 待答项 | 要点 |
|---|---|---|
| ① | **`kingdomId` 的载体** | 加在哪：`ProfessionSnapshot`（**共享核·须放行**）／`SimUnitSpec`（场景输入）／`SimUnit`（运行时）？**三选一或组合，给理由** |
| ② | **来源与填充** | 场景构造（`SimScenario`/`CardPool`/`ScenarioGenV2`）怎么填？**现役 suite 全同屏** ⇒ 是否需要一个"多王国同屏"的**最小验证场景**？ |
| ③ | **双端同源对账** | Unity `NpcProfessionDef.ToSnapshot()` 是否需同步加字段？**双端 MD5 口径**怎么给？（`15_...` 判据） |
| ④ | **数据结构改造面** | `_fogExplored` 索引 `Faction → kingdomId`；`IsExplored` 签名；`RevealUnitVision`；`SimBrain` 雾过滤 ⇒ **逐点清单** |
| ⑤ | **零回归口径** | `enableFog=false` 基线不破（现默认 false）；**锚键集内逐共有场景** `subScore Δ≤0.0005`（门禁②）；**键数施工时实读 `report.json.score.subScores` 并贴出·不引用文档旧数**〔**勘正（`D731`）**：原稿「148/151」系策划端未核准即写——**151**＝池总数／**148**＝共有场景数／**144**＝`00 §8.3` 早于 T09/T10 两次重建锚的**旧值**〕 |
| ⑥ | **改造后可达性**（`L-30` 先验） | 「k1/k2 交集空」怎么在**现役场景**下可判？（若必须新场景 ⇒ 明确列出并评估是否本批引入） |

### 第二步（主策划端放行后施工）

按裁定口径施工 ＋ 门禁四件 ＋ 双端对账。

---

## 三、验收线

| # | 线 | 判据 |
|---|---|---|
| 1 | **设计稿先交** | `§二 第一步` 六项逐项作答；**未放行不得开工第二步** |
| 2 | **per-kingdom 成立（核心）** | 探索集按 `kingdomId` 分桶（**k1/k2 交集空**）；AI **不读**玩家集（正负探针） |
| 3 | **零回归** | `enableFog=false` 基线不破；`benchmark --suite v9 --battles 100` **读锚不写锚**（`D635` A′ 口径）**锚键集内逐共有场景** `Δ≤0.0005`（门禁②）；**前置＝锚在场**（`E5` 实读满足：`total 0.422`／100 局／2026-09-12 ⇒ 不落"首次运行＝建档写锚"分支） |
| 4 | **双端同源** | 共享核改动面**双端 MD5** 一致（或按裁定口径给"零义务"结论） |
| 5 | **构建与确定性** | `dotnet build harness` **0 警告 0 错误**；determinism 建锚 3/3 |
| 6 | **写后验** | 训练仓 + 主仓各笔 hash 已贴；**未 push**；本批相关路径 `git status` 全空 |

---

## 四、红线

1. **设计稿先行**：未获主策划端放行，**不得动 `harness/Core/**` 与 `harness/KingdomBrain/**`**。
2. **不动禁改领域**：`champion/`／`Holdout/`／`AGENTS.md`／`FactorRegistry`／`harness.csproj` 结构——需注册的走 **`schemas/` 草案流程**（`factor_registry` 取号纪律）。
3. **门禁标准行**：`门禁②` 口径＝`benchmark --suite v9 --battles 100`（**读锚不写锚**·`D635` A′），**须与 `AGENTS.md` 铁律 6 一致**；若要改口径 ⇒ 先报。
4. **不跑批判读**（训练端策划：**不写 harness 代码、不跑批、不做执行判读**）；施工归训练师。
5. **不碰主仓**（Unity 侧 `VisionSystem` 属**后适配**·本批零触碰）。
6. **TD 号边界**：训练端内部裁决走 **`TD-xxx`**；跨域问题走 **`3.1 §七`**（本批需求号 `P-007`）。**禁与主仓 `D-xxx` 混用**。
7. 写-改-commit 同串；具名 `git add`；**不 push**。

---

## 五、排雷

| # | 雷 | 处置 |
|---|---|---|
| **M1** | **现役 suite 全同屏**（`SimConfig.cs:44` 注释实读） | 若"k1/k2 交集空"在现役场景不可判 ⇒ **设计稿 ⑥ 必须给出场景方案**（新增或改造），并评估是否本批引入；**禁**为过判据造特制场景蒙混（`L-30`） |
| **M2** | **`enableFog` 默认 false** | 所有新判据须**在 `enableFog=true` 下成立**，同时证 `false` 时零回归；两个门都要 |
| **M3** | **`ProfessionSnapshot` 是共享核** | 加字段 ⇒ 双端必须同源；**若 Unity 侧 `NpcProfessionDef` 无对应字段** ⇒ 按 `U1` 先例登记为「Unity 适配义务」，**不在本批擅自改 Unity** |
| **M4** | **Faction 与 kingdomId 双轨** | 建议保留 `Faction`（战役/敌对判定仍用它），**新增 `kingdomId` 只用于视野分桶**——**勿把 Faction 语义替换掉**（会连带影响感知/阵营过滤，超出本批） |
| **M5** | **`SimBrain` 雾过滤的作用域** | 改 `IsExplored` 签名会连带 `SimBrain:377` 一处；须全库 grep 调用点给清单（防漏改 ⇒ 编译期兜底优先） |
| **M6** | **锚与池** | 本批若引入新场景 ⇒ 须按 `D-013 §四` 五步纪律（生成→位移声明→构成冻结→重建锚→门禁②自洽），**禁**跳过位移声明 |
| **M7** | **承 `sim-sync` 北极星** | 决策核**只一份源码**原则；本批若动 `Core` ⇒ 双端编译同一源文件，**禁**各自拷贝 |
| **M8** | **承 `L-23`/`L-29`/`L-30`/`L-34`/`L-35`** | 构成自检／阳性对照／判定线可达性先验／五列判据／口径来源与排除项 |

---

## 六、产出

1. **开工回执**（占 HH.286 号）：含 `§五 排雷 M1~M8 逐条自答` ＋ **`§一 现状实读复核`**（可否证）＋ `sim-sync` 义务初判。
2. **口径设计稿**（第一步产物·报主策划端裁）：`§二 第一步` 六项 ＋ 影响面清单 ＋ 场景方案。
3. **施工交付报告**（放行后·按训练仓/主仓水位线取号）：`§三 六条验收线`逐项证据。
4. **本批为 `G3-1` 的 sim 侧**；Unity 侧后适配**另批**（`3.1 §六` 同步标注分工）。

---

> 签发：主策划端｜2026-09-15｜**D730**｜依据＝`3.1 §三/§六/§七/§八⑥`（`D679`/`D681`）＋ `D677` 立项 ＋ 台账 `DZ-143` ＋ 策划端实读（`SimWorld.cs:220-279`／`SimBrain.cs:375-377`／**`SimUnit.cs:58` 无 kingdomId**／`SimConfig.cs:42-45`）＋ 用户 2026-09-15 派工确认
