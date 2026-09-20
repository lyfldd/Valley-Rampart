# HH.315 · M1-C · U-1 只读评估 ＋ 判据 3′ 补证 —— 交付报告

- 任务号：HH.315 · U-1（承 `cf206746` D794 验收「⚠️ 有条件通过 · ⛔ 不销号」）
- 本轮性质：**只读评估 ＋ 报选**（⛔ `Assets/**` **一行未动**；⛔ 未开工）
- 日期：2026-09-20
- 取证手段：Unity MCP `mcp_unityMCP.execute_code`（**内存态执行，⛔ 不落任何脚本文件**）＋ `AssetDatabase` 直读真资产 ＋ `git grep`
- 交付面：本报告 1 文件（⛔ 无代码、⛔ 无资产、⛔ 无探针文件）

---

## 〇 结论速览

| 件 | 本端结论 |
|---|---|
| **U-1 复核** | ✅ **成立**（落点 1／落点 2 皆坐实）；⭐ 本端**精确复现**验收端全部机械读数（含升级 12/28、均值 **7.28×**、峰值 **16.75×**@TrainingCamp、最低 **1.29×**@gate）；⚠️ **1 处勘正**（House 满级「应为」Wood **38** 非 54）＋ **2 处口径补强**（总量守恒／根因是 D162 的**标量近似**） |
| **件1 三路线** | ⛔ **甲、乙 皆不可用**（二者数学上**是同一修法**，且退化为恒等式 ⇒ 变成"系统性少退"，张冠李戴照旧）；✅ **丙 方向正确但需加投料中态**⇒ 本端报 **丙′**（＝丙 ＋ 已完成阶段／在投阶段分流） |
| **件2 判据 3′** | ✅ 已出读数（A/B/C 三组 · 身份可区分 · 含空对照）：**A 未升级 {Wood:4} ✅ ｜ B L1 现码退 {Wood:20}（应 {Wood:14,Stone:6}）❌ ｜ C 空对照 {Wood:4}**；另补 B2 满级、D 投料中、E/F Warehouse 多类型四组 |
| **件3 L-45 回扫** | 同族算式全库**仅 2 处**（即 U-1 两落点）；⭐ 但回扫出 **U-2（新 · 阻塞候选）**：**金退还落箱后无仓可收 ＋ 箱无工人搬运链**（09#48 × 09#55 复合下游） |

---

## 一 · U-1 复核（落点 1 ＋ 落点 2）

### 1.1 落点实读（现行行号）

| 落点 | 现行行 | 算式 |
|---|---|---|
| 落点 1 `BuildRefundPack()` | [Building.cs:878-892](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L878-L892) | `costSum = def.cost.TotalCount`（[:881](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L881)）／`invested = totalInvested>0 ? totalInvested : costSum`（[:882](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L882)）／`amount = floor(invested × e.amount / baseSum)`（[:888](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L888)） |
| 落点 2 `GetRepairCost()` | [Building.cs:1055-1075](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1055-L1075) | `costSum = def.cost.TotalCount`（[:1058](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1058)）／`invested = totalInvested>0 ? … : costSum`（[:1059](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1059)）／`amount = round(invested×ratio × e.amount / baseSum)`（[:1071](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1071)） |

⭐ **共同结构**：**分子 ＝ 累计投入（含升级）／分母 ＝ 仅基础造价**，且**遍历集 ＝ 仅 `def.cost.items`** ⇒ 升级投入被"折算"进基础造价的资源类型里。验收端陈述**成立**。

### 1.2 机械复算 —— 与验收端读数逐值吻合

复算手段：`AssetDatabase` 直读 40 个 `BuildingDef` 真资产（⛔ 非解析 YAML），逐栋按现行算式复算。

| 验收端读数 | 本端复算 | 是否吻合 |
|---|---|---|
| 有 cost 资产 **28 栋** | **28** | ✅ |
| 含升级 **12 栋（43%）** | **12（42.9%）** | ✅ |
| 每栋最大倍率 **均值 7.28×** | **7.28×** | ✅ |
| **峰值 16.75×**（TrainingCamp） | **16.75×@TrainingCamp** | ✅ |
| **最低 1.29×**（gate） | **1.29×@gate** | ✅ |

⚠️ **口径补强（须写清，否则倍率会被误读为"总量超发"）**：倍率的分母是**基础造价里该类型量**，⛔ 不是"应为量"。
⭐ **总量层面其实守恒**：`Σ floor(invested × e.amount / baseSum) ≈ invested`（因 `Σ e.amount = baseSum`）⇒ 全 12 栋**总量倍率 0.99~1.00×**。⇒ 本缺陷的准确名称是 **「构成全错（张冠李戴）＋ 逐类型超发/短缺」**，⛔ 不是"凭空多退总量"。

### 1.3 12 栋逐栋读数（满级口径）

`base`＝`def.cost.TotalCount`；`up`＝全部 `levels[].upgradeCost.TotalCount`；「现退」＝现行算式逐类型结果；「丢失」＝升级里出现但基础造价**没有**的类型（⇒ 被折成基础类型）。

| def | base | up | 投入 | 现退（逐类型） | 基础倍率 | 丢失类型 |
|---|---|---|---|---|---|---|
| `arrow_tower` | 30 | 114 | 144 | Gold 20→**96** ／ Stone 10→**48** | 4.80× | Wood 52 · Metal 20 |
| `Blacksmith` | 110 | 165 | 275 | Gold 50→**125** ／ Stone 60→**150** | 2.50× | — |
| `farm` | 50 | 42 | 92 | Gold 50→**92** | 1.84× | Wood 28 · Food 14 |
| `gate` | 130 | 38 | 168 | Gold 80→**103** ／ Wood 50→**64** | **1.29×** | Stone 12 · Metal 10 |
| `Granary` | 4 | 56 | 60 | Wood 4→**60** | 15.00× | Stone 22 |
| `House` | 4 | 56 | 60 | Wood 4→**60** | 15.00× | Stone 22 |
| `market` | 80 | 110 | 190 | Gold 80→**190** | 2.38× | Stone 40 · Wood 70 |
| `quarry` | 50 | 76 | 126 | Gold 50→**126** | 2.52× | Stone 34 · Wood 42 |
| `TrainingCamp` | 8 | 126 | 134 | Wood 8→**134** | **16.75×** | Stone 56 |
| `TrainingGround` | 3 | 14 | 17 | Wood 3→**17** | 5.67× | Stone 6 |
| `wall` | 10 | 96 | 106 | Stone 10→**106** | 10.60× | Wood 42 · Metal 20 |
| `Warehouse` | 8 | 64 | 72 | Gold 4→**36** ／ Stone 4→**36** | 9.00× | Wood 34 |

**12/12 栋皆「丢失类型」或「逐类型错配」**；唯一无损者 `Blacksmith`（基础 金+石 ／ 升级 亦 金+石 ⇒ 恰好同型，仅量错配）。

### 1.4 ⚠️ 勘正 1 处（验收端算术）

验收端 House 逐值样本「满级 应为 {Wood:54, Stone:22}」⇒ **实为 {Wood:38, Stone:22}**。

- 逐阶段实读（[House.asset](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Resources/Buildings/House.asset)）：`cost = {Wood:4}`；`levels[0].upgradeCost = {Stone:6, Wood:10}`；`levels[1].upgradeCost = {Stone:16, Wood:24}`。
- Wood ＝ 4 ＋ 10 ＋ 24 ＝ **38**；Stone ＝ 6 ＋ 16 ＝ **22**；Σ ＝ 60 ＝ `invested`（自洽）。
- 验收端 54 ＋ 22 ＝ 76 ≠ 60 ⇒ **该行内部不自洽**（疑把 `levels[1]` 的 Stone 16 计入 Wood）。
- ⚠️ 该勘正**不改变 U-1 成立**（L1 行 {Wood:14, Stone:6} 验收端正确），仅改「应为」基数。

### 1.5 根因归属 —— 三层（验收端判定**方向正确，需下沉一层**）

| 层 | 内容 | 证据 |
|---|---|---|
| L1 直接因 | 分子含升级、分母只含基础造价，且遍历集只覆盖 `def.cost.items` | §1.1 |
| L2 归属 | ⛔ **非本片新引入**：`7c8a7f1f^:658-668` 结构与现码**逐字一致**（`costSum = SumCostOf(def.cost,false)` ／ `amount = invested × e.amount / baseSum`） | `git show 7c8a7f1f^:…Building.cs` 实读 |
| L3 ⭐ 真根因 | **`totalInvested` 自 D162 起就是「标量近似」** —— [Building.cs:142-143](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L142-L143) 原注释明写：「用**单一近似总量**（resourcepack 求和）**而非四资源分账**——返还/修复**粗糙按比例缩放同一 pack**」 | 字段注释（D162 已知近似） |

⚠️ **对验收端「折扣把超发盖住」的一处细化**：`hp/maxHp` 折扣**只在受损时掩盖**；`Refund(pack, ratio)` 在 `ratio=1`（满血）时**不掩盖** ⇒ **满血拆除的 U-1 在改前即已存在**（例：改前满血 House L1 亦退 {Wood:20}）。09#54 的真实作用是**让受损建筑也暴露**，⛔ 不是"首次引入"。

---

## 二 · 件1 · 三路线只读评估 ＋ 报选

### 2.0 ⭐ 先说一个结构性发现：**甲 与 乙 在数学上是同一个修法**

- 甲：分母改 `totalInvested` ⇒ `amount = floor(invested × e.amount / invested) ≡ e.amount`。
- 乙：分母改「建造造价 ＋ 已发生升级造价」。而 `totalInvested` **恰好就等于**这两个量之和（建造记账 ＋ 升级记账，见 §3 波及面 #6/#9/#10）⇒ **乙的数值 ≡ 甲**。
- ⇒ 两路线**都令 分子/分母 ＝ 1**，结果**退化为「退还恒等于 `def.cost` 原样」**（与投入、等级**完全无关**）。

⚠️ 因此「甲／乙／丙」不是三条并列路线，而是 **「分母修（甲≡乙）」vs「逐类型修（丙）」** 两类。

### 2.1 正确性

| 路线 | 落点 1 修好吗 | 落点 2 修好吗 | 解决「张冠李戴」吗 | 数学后果（实测口径） |
|---|---|---|---|---|
| **甲** | ❌ 换了个错 | ❌ 换了个错 | ❌ 不解决 | 退 ⇒ **恒 `def.cost`**。House L1 投 20 ⇒ 退 **{Wood:4}**（应 {Wood:14,Stone:6}）⇒ **少退 16**；满级投 60 ⇒ 退 **{Wood:4}** ⇒ **少退 56**；Warehouse L1 投 28 ⇒ 退 **{Gold:4,Stone:4}** ⇒ **少退 20**。修复费 ⇒ 恒 `def.cost × ratio`（与等级/投入无关） |
| **乙** | ❌ 同上（数值 ≡ 甲） | ❌ 同上 | ❌ 不解决 | 同上。唯一差别：乙是 **def 派生**（不读 `totalInvested` 字段）⇒ 对地图预置／旧档／AI 建筑也能算；但因**分母＝分子**，结果仍是 `e.amount` |
| **丙** | ✅ | ✅ | ✅ | 逐阶段累加 ⇒ House L1 退 **{Wood:14,Stone:6}**、满级 **{Wood:38,Stone:22}**、Warehouse L1 **{Gold:4,Stone:14,Wood:10}**；总量 ＝ 实际投入（守恒） |
| **丙′**（本端推荐） | ✅ | ✅ | ✅ | ＝ 丙 ＋ **投料中态分流**（否则超发，见 2.3） |

**逐条回答验收端「关键问题」**

| 问 | 答（实读） |
|---|---|
| 甲：`totalInvested` 是否恒 ≥ Σ投入？ | 恒**等于**「已记账投入」（`AddInvested` 逐笔累加，[Building.cs:244-247](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L244-L247)）。⚠️ 但它**不等于物理投入**：AI 建筑下单即一次记满全造价（[BuildController.cs:295](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildController.cs#L295)）而 AI **不走投料**；地图预置／旧档为 **0**（[Building.cs:437](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L437)）。 |
| 甲：读档兜底 `:1002-1004` 会使分母为 0 吗？ | **不会**（只要保留 `totalInvested > 0 ? … : costSum` 三元兜底 ⇒ 分母 ≥ `costSum` ≥ 1）。⚠️ 但**若删掉兜底**改为 `Mathf.Max(1, totalInvested)` ⇒ 地图预置／旧档 `totalInvested=0` ⇒ **全零退还**（新缺陷）。⇒ 兜底**必须保留**。 |
| 乙：「已发生升级造价」从哪来？能否拆开？ | **能，且⛔ 无需新增字段** —— `level` 即"已发生升级数 ＋ 1"（`OnConstructionComplete` 只 `level++`，全库**无降级路径** ⇒ 单调可靠）⇒ 已发生升级造价 ＝ `Σ_{i=0}^{level-2} levels[i].upgradeCost`。⚠️ 但**拆开了也没用** —— 乙只改分母，遍历集仍是 `def.cost.items` ⇒ 升级类型永不出现。 |
| 丙：能否逐类型算退？ | ✅ **能，而且不需要"分摊"** —— `def.cost` 与 `levels[i].upgradeCost` 本身**都是精确的逐类型量** ⇒ 直接**逐阶段累加**即可（比"按占比摊"更强、零舍入误差）。 |
| 丙：金／弹药是否外溢？ | **弹药不外溢**（实读 40 栋：`cost` 仅含 **Gold×19／Stone×15／Wood×14**；`upgradeCost` 仅含 **Stone×19／Wood×20／Metal×5／Gold×1／Food×2** ⇒ **全库造价零弹药／零水晶／零燃油／零矿**）。⚠️ **金会外溢且是真问题** —— 19/40 栋 `cost` 含金 ⇒ 金纳入退还 ⇒ 撞 **U-2**（见 §四）。 |

### 2.2 代价

| 路线 | 改动文件数 | 新增字段 | 触及存档格式 | 影响旧档 |
|---|---|---|---|---|
| 甲 | 1（`Building.cs` · 2 方法） | 0 | ⛔ 否 | 分母改用 `totalInvested` ⇒ 旧档（该字段在档）行为一致；⚠️ 但地图预置恒 0 ⇒ 走兜底 |
| 乙 | 1（`Building.cs` · 2 方法） | **0**（`level` 已在档） | ⛔ 否 | ✅ 完全不受旧档影响（纯 def 派生） |
| **丙′** | **1（`Building.cs` · 2 方法 ＋ 1 处注释）** | **0** | ⛔ **否** | ✅ 不受影响（**不再读 `totalInvested`**） |

⭐ **三路线都不需要新增字段、都不触及 `BuildingSaveData`** —— 因为"已发生升级"可由 `level` 派生、"在投部分"可由 `siteContents`（本片已入档）派生。

⚠️ **丙′ 的副作用（须一并裁）**：`totalInvested` 将**零读点**（只剩入档 `:949` ／日志 `:576`）⇒ 退化为"只写不读"。处置二选一：**(a)** 保留（作为台账/后续用）；**(b)** 随 `M1-D` 一并退役（⚠️ 退役 = 动存档格式）。

### 2.3 ⭐ 丙 的必要补丁：投料中态（本端实测坐实）

`Demolish()` **无状态守卫**（[Building.cs:845-847](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L845-L847) 只挡 `Dead/Placing`）⇒ **投料中的工地可被拆**（`Update` 亦把拆除分支排在建造分支之前）。

- **实测 D 组**：House 建造完工 → 升级只投 **3/16** ⇒ `totalInvested=7`、工地仓 `{Stone:3}`。
- 若按**纯丙（def 派生全额）** ⇒ 退 `{Wood:14, Stone:6}`，**同时** `DropSiteStoreToChest()` 又掉出 `{Stone:3}` ⇒ **双重退还**，其中 **{Wood:10, Stone:6} 是玩家从未搬进来的料**。
- ⇒ **丙′ 必须分流**：

```
已完成阶段（level-1 次升级 ＋ 建造）       ⇒ 逐阶段造价全额
在投阶段（_awaitingMaterials == true）      ⇒ 该阶段「金」（下单即扣）＋ 工地仓内容物 siteContents
不在投料中（Active）                        ⇒ 已完成阶段全额
```

对应实现要素（本轮**不落码**）：`level` ／ `_pendingUpgrade` ／ `_pendingRepair` ／ `_awaitingMaterials` ／ `SiteStore.Contents` ／ `Building.GoldOnlyOf` —— **全部已在场**。

### 2.4 ⭐ 报选

> **本端选「丙′」**（＝丙 ＋ 投料中态分流），并**否决甲／乙**。

理由（三条，均可证）：
1. **甲≡乙，且数学上退化为恒等式**（`amount ≡ e.amount`）⇒ 不修"张冠李戴"，只把"逐类型超发"换成"系统性少退"（House 满级少退 56／Warehouse L1 少退 20）。
2. **丙 是唯一能同时修好落点 1 ＋ 落点 2 ＋ 张冠李戴**的路线，且**零新增字段、零存档变更、零旧档影响**（因所需输入 `level` ／ `siteContents` 均已在场）。
3. **丙 必须叠加投料中态分流**（否则超发，D 组实测坐实）⇒ 故报 **丙′** 而非裸丙。

⚠️ **丙′ 附带的 2 项语义变化（请一并裁）**：
- **① 拆除退还**改为"按 `level` 累计造价全额"，⛔ 不再受 `totalInvested` 记账漂移影响（含修复历史）。
- **② 修复费不再复利** —— 现码 `GetRepairCost` 以 `totalInvested` 为基数，而**修复花费本身被 `AddInvested` 累加**（[BuildingPanel.cs:322](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingPanel.cs#L322)／[:340](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingPanel.cs#L340) ＋ 投料逐笔）⇒ **每次重建后修复费递增**（`ratio=0.5` 实读 [RepairConfig.asset](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Resources/Config/RepairConfig.asset)：C → 1.5C → 2.25C → 3.375C…）。丙′ 后固定为「累计造价 × ratio」。**此项独立于 U-1，建议单列**。

---

## 三 · 件1 波及面：`totalInvested` 全部读写点（逐点标注）

取证：`git grep -n "totalInvested|AddInvested"` 全库 ⇒ **37 命中**，去重后逐点定性。

| # | file:line | 读/写 | 说明 | 甲 | 乙 | 丙′ |
|---|---|---|---|---|---|---|
| 1 | [Building.cs:146](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L146) | 声明 | 字段（`public int`） | – | – | ⚠️ 见 §2.2 副作用 |
| 2 | [Building.cs:244-247](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L244-L247) | 写 | `AddInvested` 累加（唯一写口） | – | – | – |
| 3 | [Building.cs:437](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L437) | 写 | `Initialize` 置 0（⚠️ 上方 `:432` 注释已陈：说的"记入"做的"置 0"） | – | – | – |
| 4 | [Building.cs:1002-1004](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1002-L1004) | 写 | `LoadState` 恢复 ＋ 兜底 `def.cost.TotalCount` | ⚠️ 兜底须保留 | – | – |
| 5 | [BuildingFactory.cs:347-348](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L347-L348) | 写 | `SpawnFromSave` 恢复 ＋ 兜底（与 #4 同源） | ⚠️ 同上 | – | – |
| 6 | [BuildController.cs:295](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildController.cs#L295) | 写 | 下单记账（玩家＝金／AI＝全造价） | – | – | – |
| 7 | [BuildingPanel.cs:322](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingPanel.cs#L322) | 写 | 废墟重建记「金」 | – | – | – |
| 8 | [BuildingPanel.cs:340](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingPanel.cs#L340) | 写 | 废弃主城修复记「金」 | – | – | – |
| 9 | [BuildingPanel.cs:382](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingPanel.cs#L382) | 写 | 升级记「金」 | – | – | – |
| 10 | [ConstructionSiteStore.cs:140](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/ConstructionSiteStore.cs#L140) | 写 | 投料入仓逐笔记 | – | – | – |
| **11** | [Building.cs:882](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L882) | **读** | `BuildRefundPack` 分子（**U-1 落点 1**） | ✅ 必改 | ✅ 必改 | ✅ 必改 |
| **12** | [Building.cs:1059](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1059) | **读** | `GetRepairCost` 分子（**U-1 落点 2**） | ✅ 必改 | ✅ 必改 | ✅ 必改 |
| 13 | [Building.cs:949](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L949) | 读 | `SaveState` 入档 | – | – | – |
| 14 | [Building.cs:576](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L576) | 读 | 日志（料齐） | – | – | – |
| 15 | [BuildingSaveData.cs:45](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingSaveData.cs#L45) | 档字段 | 声明（注释亦陈：`D162 拆除返还基数`） | – | – | – |
| 16 | `Valley_HH315_M1C_Smoke.cs` ×7（:162/181/184/187/193/196/198/271/277/340/372/403） | 读 | **Editor-only 探针**（⛔ 非生产） | – | – | – |

**读点合计 2 处生产码（#11／#12）＋ 2 处非退还用途（#13／#14）**；⇒ **三路线改动面都极小**，差别只在"改得对不对"。

**旁证（未列 = 未触碰）**：`KingdomManager.TryUpgradeCastle` **零命中** ⇒ 主城升级**不记账**（勘-4 已登记，归后续片）。

---

## 四 · 件2 · 判据 3′（已升级建筑拆除退还）实测读数

### 4.1 取证方式（须先声明，涉"生产值 vs 取证值"）

| 项 | 说明 |
|---|---|
| 载体 | Unity MCP `execute_code`（**内存态 C# 执行 · ⛔ 不落任何脚本文件 · ⛔ 不动 `Assets/**`**） |
| 被测对象 | **生产码原方法**：`Building.BuildRefundPack()`（private · 经反射直调）／`Building.GetRepairCost()`（public） |
| B 组的升级链 | ⭐ **走真实生产链**（非直接置值）：`AddInvested(GoldOnlyOf(升级造价))`（＝`BuildingPanel:380-382` 真实下单步）→ `TryUpgrade()`（真实，`BeginMaterialPhase` 建工地仓）→ `SiteStore.Deposit(逐类型全额)`（＝真实投料，逐笔记 `AddInvested`）→ `OnConstructionComplete()`（真实提级 `level++`） |
| ⚠️ 未跑到的部分 | **未跑 `Demolish()` → `FinishDemolish()` → `SpawnChest` 全链**（需进局 ＋ 工人到场推进 ⇒ 属"开工"范畴，与硬约束冲突）。⚠️ 但 `FinishDemolish` 的掉箱入参**唯一来源就是 `BuildRefundPack()` 返回值**（[Building.cs:863-867](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L863-L867) 实读）⇒ **读数等价**；且上一轮判据 3／5 已在正门跑局中实测过该链（`{Wood:4}` 落箱） |
| ⚠️ 环境瑕疵（已隔离） | Edit-mode 下 `OnConstructionComplete → UpdateVisual → KingdomRace.GetKingdomRace` 触碰 `KingdomRegistry` 单例 ⇒ 抛 `DontDestroyOnLoad 仅限 Play 模式`。该异常发生在 **`level++` 与 `state=Active` 之后** ⇒ **不影响本判据的两个输入（`level` / `totalInvested`）**；已在脚本内 try/catch 并逐组标注 |
| ⛔ 生产值变更 | **零**（⛔ 未改任何 SO／代码／资产；⛔ 无临时开关；⛔ 无 `taskTimeout` 类取证放宽） |

### 4.2 ⭐ A/B/C 三组读数（判据 3′ 主体）

| 组 | 身份（独立 GameObject） | 入场 `level` | 入场 `totalInvested` | **现码退（实测）** | 应为（逐阶段累加） | 判定 |
|---|---|---|---|---|---|---|
| **A** 对照组（未升级） | `U1_Probe_A`（House · `isPlayerBuilt=true`） | **1** | **4** | **`{Wood:4}`** | `{Wood:4}` | ✅ 正确 |
| **B** 实验组（升到 L1） | `U1_Probe_B`（House · `isPlayerBuilt=true`） | **2** | **20** | **`{Wood:20}`** | **`{Wood:14, Stone:6}`** | ❌ **Wood 超发 6 ／ Stone 6 全丢** |
| **C** 空/零对照（地图预置） | `U1_Probe_C`（House · `isPlayerBuilt=false`） | 1 | **0** | **`{Wood:4}`** | `{Wood:4}`（走 `:882` 兜底 `costSum`） | ✅ 对照点有效（非零、可读、走兜底分支） |

**鉴别力声明（前置给出，后置命中）**：
> 「若分母口径仍错，B 列应读成 **`{Wood:20}`**（＝投入总量全折成基础类型 Wood）；若修好，应读成 **`{Wood:14, Stone:6}`**。」
> ⇒ **实测 B ＝ `{Wood:20}` ⇒ 口径仍错，鉴别力命中**。

**身份可区分**：A／B／C 为**三个独立 `GameObject`**，读数按**实例引用**取（⛔ 未按 `name` 或 `def` 比同类）。

### 4.3 追加四组（覆盖满级／投料中／多类型基础）

| 组 | 构造 | 入场 `level` / `totalInvested` | 现码退（实测） | 应为 | 判定 |
|---|---|---|---|---|---|
| **B2** House 满级 | 真实链升两次 | **3** / **60** | **`{Wood:60}`** | `{Wood:38, Stone:22}` | ❌ Wood 超发 22 ／ Stone 22 全丢 |
| **D** House 投料中（升级只投 3/16） | 建造完工 → 升级投 `Stone 3` | 1 / **7**（`awaiting=True` · 工地仓 `{Stone:3}`） | **`{Wood:7}`** | 按实投 `{Wood:4, Stone:3}` | ❌ **投料中态亦张冠李戴**（Stone 3 → 折成 Wood 3） |
| **E** Warehouse 未升级 | 真实链建造 | 1 / **8**（`cost={Gold:4,Stone:4}`） | **`{Gold:4, Stone:4}`**；修复费 **`{Gold:2, Stone:2}`** | `{Gold:4, Stone:4}`（修复费 `×0.5`） | ✅ 正确 |
| **F** Warehouse L1 | 真实链升一次 | **2** / **28**（升级造价 `{Stone:10,Wood:10}`） | **`{Gold:14, Stone:14}`**；修复费 **`{Gold:7, Stone:7}`** | **`{Gold:4, Stone:14, Wood:10}`** | ❌ **Wood 10 全丢 ＋ Gold 超发 10**（多类型基础下张冠李戴最直观） |

⭐ **F 组是"张冠李戴"最干净的证据**：玩家付了 `{Gold:4, Stone:14, Wood:10}`，退到手却是 `{Gold:14, Stone:14}` —— **Wood 凭空消失、Gold 凭空多出 10**。
⭐ **落点 2 亦同型**（E 组未升级时"碰巧正确"，F 组 `{Gold:7,Stone:7}` vs 应 `{Gold:2,Stone:7,Wood:5}`）。

### 4.4 判据 3′ 要求项对照

| 要求 | 达成 |
|---|---|
| 身份可区分（⛔ 禁按 name 比同类） | ✅ A/B/C/D/E/F 六独立实例，按实例引用读数 |
| 对照组 A＝未升级 ／ B＝升到 L1 及以上 | ✅ A(L1) / B(L2) / B2(L3) |
| 打印 `level` 与 `totalInvested` 现值入场 | ✅ 逐组打印（见 4.2／4.3 表） |
| 读数含逐资源明细 | ✅ 逐资源 `{type:amount}` |
| `SpawnChest` 内容 | ⚠️ 未跑（见 4.1 声明；入参等价性已证） |
| 鉴别力声明 | ✅ 4.2 前置声明 ＋ 命中 |
| 必须有「空/零」对照 | ✅ C 组（`totalInvested=0` 走兜底）＋ E 组（未升级，两落点皆正确） |

---

## 五 · 件3 · `09#54` 下游依赖回扫（L-45）

**回扫口径**：① 依赖「`hp/maxHp` 折扣」者 ② 依赖「`IsRefundResource` 四/五资源过滤」者 ③ 「按比例缩放同一 pack」同族算式（L-45 通则扩查）。

### 5.1 同族算式全库扫描（③）

`git grep` 全库「`e.amount / baseSum` ／ `amount / costSum` ／ `× ratio`」⇒ **生产码同族算式仅 2 处**：

| # | file:line | 算式 | 09#54 后是否已暴露 |
|---|---|---|---|
| 1 | [Building.cs:888](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L888) | `floor(invested × e.amount / baseSum)` | ⭐ **已暴露 ＝ U-1**（验收端已抓） |
| 2 | [Building.cs:1071](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L1071) | `round(invested×ratio × e.amount / baseSum)` | ⭐ **已暴露 ＝ U-1 落点 2**（本端 4.3 F 组实测坐实） |

⇒ **同族算式无"第三处未被发现者"** ✅（其余 `× ratio` 命中均为地图生成／波次配比／HP 条宽度等无关域）。

### 5.2 依赖「`hp/maxHp` 折扣」者（①）

| # | 位置 | 现状 | 判定 |
|---|---|---|---|
| 1 | [Building.cs:888](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L888)（旧 `:656` `ratio = hp/maxHp`） | 折扣已退役；算式残留 | ⭐ **U-1** |
| 2 | [RulerController.cs:295-303](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Ruler/RulerController.cs#L295-L303) `Refund(cost, ratio)` | 唯一生产调用方（旧 `Demolish:672`）已退役 ⇒ **生产码 `ratio<1` 调用点 ＝ 0**（仅 `SiegeProductionSystem.cs:177` 用默认 `1.0`） | ⚠️ **形参语义空转**（非缺陷 · 观察项）；⚠️ [:293](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Ruler/RulerController.cs#L293) 注释「拆除退款 ratio=0.5」**已成陈注释** |
| 3 | [KingdomState.cs:167-172](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/KingdomState.cs#L167-L172) `Refund(cost, ratio)`（AI 侧） | 同款；生产调用 `SiegeProductionSystem.cs:226` 用默认值 | ⚠️ 同上（陈注释「拆除退款 ratio=0.5 等」） |
| 4 | [Building.cs:142-143](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L142-L143) 字段注释 | 明写「单一近似总量 · **返还/修复粗糙按比例缩放同一 pack**」 | ⭐ **根因文档证据**（D162 已知近似）⇒ 丙′ 须同步改此注释 |
| 5 | [Building.cs:432](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L432) 注释 vs `:437` 实现 | 注释「累计投入（修复成本基数/拆除返还基数）」紧跟 `totalInvested = 0;` | ⚠️ 陈注释（微瑕） |
| 6 | [BuildingSaveData.cs:44](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingSaveData.cs#L44) 注释 | 「（修复成本基数 / 拆除返还基数）」 | ⚠️ 丙′ 后成陈注释 |
| 7 | [Building.cs:876](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L876) 注释 | 「按 `def.cost` 全部资源**占比摊**」 | ⚠️ **口径本身错**（应"逐阶段累加"）⇒ 丙′ 须勘正 |

### 5.3 依赖「`IsRefundResource` 过滤」者（②）

| # | 位置 | 09#54 后 | 判定 |
|---|---|---|---|
| 1 | 落点 1（旧 `:667` 四资源） | 过滤退役 ⇒ 含 Metal／Ore | ✅ **已暴露且已修**（上一轮判据 4 实测 Ore True） |
| 2 | 落点 2（旧 `:827` 五资源含 Metal 不含 Ore） | 同上 | ✅ 同上 |
| 3 | ⭐ **金纳入退还范围** | 19/40 栋 `cost` 含金 ⇒ 金进入退还包 | 🔴 **U-2（新）** —— 见 5.4 |
| 4 | 弹药 | 实读 40 栋造价**零弹药类型** | ✅ 不外溢（判据 4 的"弹药"担忧**不成立**） |

### 5.4 🔴 U-2（新 · 阻塞候选）：金退还落箱后**无仓可收 ＋ 无工人搬运链**

**链条（三段全实读）**：

| 段 | 事实 | 证据 |
|---|---|---|
| ① 金进入退还包 | 19/40 栋 `cost` 含 Gold（`IsRefundResource` 退役 ⇒ 金不再被过滤） | 本端机械扫描（`cost:Goldx19`） |
| ② 退还改走**无主箱** | 09#55（件3）退役 `RulerController.Refund` ⇒ 改 `ChestManager.SpawnChest(coord, pack, Faction.None)` | [Building.cs:865-870](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L865-L870) |
| ③ **无仓接受金** | 金路径 ＝ `res_currency.gold`；全库声明 `res_currency` 的仓**仅 `Well.asset:33`**，而 Well 是**死仓**（`ProducerComponent.cs:69-76` `_isWell` 早返回 ⇒ 不挂 `StorageComponent`）；国库 `TreasureVault.VaultPaths = {res_material, res_food}` **⛔ 不含金**（[TreasureVault.cs:19](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/TreasureVault.cs#L19) 注释明写「故声明只写材料族＋粮族，⛔ 不含 `res_currency`/`res_ammo`」） | `git grep res_currency` ⇒ 3 命中 |
| ④ **箱无工人搬运链** | `ChestEntity : MonoBehaviour, **IInteractable**`（⛔ 非 `ITaskSource`）；取回唯一路径 ＝ `ChestEntity.Interact → ChestManager.Pickup(chest, ctx)`（**玩家交互**）；`KingdomTaskType` 11 项**无"取箱"类**；`TaskScheduler` **零 `ChestManager` 引用** | [ChestEntity.cs:11](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestEntity.cs#L11)／[ChestManager.cs:130-136](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ChestManager.cs#L130-L136) |

⇒ **后果**：含金建筑（19/40 栋）被拆 ⇒ 退还的金落进**无主箱**，**没有任何仓能收**、**没有工人会搬** ⇒ 金**卡死在箱里**（需玩家手动点开拾取，且拾取后是否入账仍取决于玩家侧背包→国库链）。

⚠️ **与 09 文档的偏差**：`09` §16.1-4 明写「拆房后退的材料落在箱子里，**要工人搬回**」⇒ **"工人搬回"这条链当前不存在**（⛔ 不限于金：**任何**退还物都只能玩家手动拾取）。

⇒ **本端判定**：U-2 是 **09#48（金纳入）× 09#55（改掉箱）的复合下游**，且**同时暴露"工人搬回"整条链缺失**。**须裁**：① 是否本片修 ② 归 `M1-D`／`M1-E`（金走仓）③ "工人搬回"另立片。

---

## 六 · 请裁汇总（可一键回复）

| # | 争点 | 本端倾向 |
|---|---|---|
| **C-1** | U-1 修法选型 | ✅ **丙′**（丙 ＋ 投料中态分流）；⛔ 否决甲／乙（数学上同一修法且退化为恒等式） |
| **C-2** | 丙′ 附带的「修复费不再复利」 | ⚠️ 请确认是否一并纳入（现码 C → 1.5C → 2.25C… 递增） |
| **C-3** | 丙′ 后 `totalInvested` 零读点 | (a) 保留 ／ (b) 随 `M1-D` 退役（⚠️ (b) 动存档格式） |
| **C-4** | **U-2**（金退还无仓可收 ＋ 箱无工人搬运链） | ⚠️ 请裁归属（本片 ／ `M1-D`／`M1-E` ／ 另立片）；⭐ 本端倾向：**另立片**（"箱 → 仓"搬运链 ＋ 金走仓同批），本片只留挂账 |
| **C-5** | 陈注释 6 处（§5.2 #2/#3/#5/#6 ＋ §5.3 ＋ [Building.cs:876](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L876)） | ✅ 建议随 U-1 修法一并勘正（零行为） |
| **C-6** | 勘正 1 处（House 满级"应为" Wood **38** 非 54） | ✅ 请回写验收记录 |

---

## 七 · 本端状态声明

- ⛔ **本轮 `Assets/**` 一行未动**；⛔ 未开工；⛔ 未改任何 SO／代码／资产／探针。
- ⛔ 未碰 `WarehousePanel`／`TreasureVault`／四档账本／美术／pixel-forge／`GameScene`／`Packages`／`3.6`·`3.8` doc。
- ⛔ **未 push**；本报告单独 commit。
- ⛔ **未代提交策划端账本文档**；应登记项见下。

**应登记项（供策划端落账）**：
1. `_编号登记.md`：`HH.315` 行追加「U-1 评估已交付（本轮只读 · 无代码）」；
2. `测试基线台账.md`：追加 `§九十九`（U-1 复核 ＋ 三路线评估 ＋ 判据 3′ 六组读数 ＋ L-45 回扫 ＋ **U-2 新立**）；
3. `_策划教训库.md`：⭐ 建议新立 **L-46**（**"分母修"型修法须先验其是否退化为恒等式**：当候选分母与分子同源时，`× a / a ≡ 1` ⇒ 修法空转）＋ **L-45 实例入库**（09#54 ⇒ U-1）；
4. `09_资源与仓库.md`：§16.1-4「要工人搬回」⇒ 标注**当前未实现**（U-2）；§16.2 补「退还量 ＝ **逐阶段造价累加**（⛔ 非"按占比摊"）」；
5. `09` §十二：`#48`／`#54` 追加「⚠️ U-1 修复中（丙′）」；
6. 新挂账：**U-2**（箱→仓搬运链 ＋ 金走仓）＋ `totalInvested` 退役（C-3）。
