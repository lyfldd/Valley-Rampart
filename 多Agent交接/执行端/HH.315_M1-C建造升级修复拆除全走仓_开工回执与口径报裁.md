# HH.315 · M1-C（建造／升级／修复／拆除 全走仓）开工回执 ＋ 开工前口径报裁

- 任务号：`HH.315`（`D792` 签发 · 承 `HH.314` M1-B 已验收销号 `D791`）
- 归属模块：中层 `09_资源与仓库.md` · 施工片 `M1-C`
- 上游依据：`中层执行计划` §4.1（`M1-C` 行）· `09` §十六（§16.1／16.2／16.3／16.3.1）· `09` §十二 差异 `#48`／`#53`／`#54`／`#55`／`#56`／`#64`
- 本端身份：**执行端**
- 状态：🟢 **已接单**；⚠️ **`Assets/**` 一行未写** —— 判据三直读发现 **5 条阻塞性口径歧义** ⇒ 按任务书 §五 红线「**先停手报裁**」⇒ 本文件 ＝ 回执 ＋ 报裁
- 取证纪律：**判据三直读** —— ①设计稿全文（`09` §十二 ＋ §十六 逐行实读）②代码 `file:line` 实读 ③档位字段直读（`_任务队列.md:65` `M1-C` 行／`_编号登记.md` `HH.315` 行／`测试基线台账.md` §九十七／`_当前快照.md`）

---

## §〇 前置门复核（本端独立取证 · ⛔ 非抄任务书）

| 门 | 任务书读数 | 本端独立取证 | 判 |
|---|---|---|---|
| 底层批 2（地图四门） | `HH.301` 片 4 · commit `12497dd6`（21 文件）· features 生产码写点 12→1 · 新建 `MapGate.cs` 394 行 | `git log --oneline -1 12497dd6` ⇒ 命中（HH.301 HH.294 片4 地图四门交付）；`MapGate.cs:33` `WriteRaw(MapData,int,FeatureType)` ＝ 唯一写口（方法头注自带 grep 判据，本端复核属实） | ✅ **已闭** |
| `0-B` 粒度代价实测（待议 #5） | `HH.296` 片 1 —— 内存净增 +34.73 MB/大图 · 单次全图遍历 1536² 1.75 ms · BFS 56.08 ms · ⛔ 无「每帧全图扫」 | 采信 `HH.296` 片 1 交付报告读数（本片不重跑全图基准；本片不引入每帧全图扫） | ✅ **已出** |

⇒ **两门皆开，本片无门阻塞**；阻塞来自**口径**（§三），非来自前置门。

⚠️ **勘正提示**：任务书开头「你（或你的上下文）若记着『底层施工未派工』—— 那是过期的」—— 本端**未持该过期记忆**，底层 6 片全闭与 `HH.301`／`HH.296` 读数均已按上表独立复核，**无需再问**。

---

## §一 判据三直读 —— 任务书 16 处 `file:line` 逐条实读对照

| # | 任务书写法 | 本端实读（原文） | 判 |
|---|---|---|---|
| 1 | `BuildController.cs:247-249` | `:247` 注释「扣资源（门面：玩家→王国仓库凑单；AI→KingdomState.Spend 台账制，无事件）」／`:248 if (!CanPayBuild(kingdomId, def.cost)) return false;`／`:249 if (!PayBuild(kingdomId, def.cost)) return false;` | ✅ 1:1 |
| 2 | `BuildController.cs:281` | `b.StartConstructing();     // 建造走 Constructing 进度（玩家手工与 AI 同一条链）` | ✅ 1:1 |
| 3 | `BuildingPanel.cs:372` | `RulerController.Instance.Spend(lvCost);` | ✅ 1:1 |
| 4 | `BuildingPanel.cs:334` | `RulerController.Instance.Spend(repairCost);` | ✅ 1:1 |
| 5 | `Building.cs:414` | `public void StartConstructing()`（`:416 _pendingUpgrade = false;` `:417 state = Constructing;` `:418 constructProgress = 0f;`） | ✅ 1:1 |
| 6 | `Building.cs:204`／`:205` | `private bool _pendingUpgrade;` ／ `private bool _pendingRepair;` | ✅ 1:1 |
| 7 | `Building.cs:442` | `if (state == BuildingState.Constructing)`（`Update` 内 `:444 constructProgress += Time.deltaTime / Mathf.Max(0.01f, EffectiveDuration());` `:445` 满 1 转 `OnConstructionComplete()`） | ✅ 1:1 |
| 8 | `Building.cs:656` | `float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;` | ✅ 1:1 |
| 9 | `Building.cs:667` | `if (!IsRefundResource(e.type)) continue;   // 旧口径：只摊 金/石/木/粮` | ✅ 1:1 |
| 10 | `Building.cs:668` | `int amount = Mathf.FloorToInt((float)invested * e.amount / baseSum);` | ✅ 1:1 |
| 11 | `Building.cs:677-678` | `static bool IsRefundResource(ResourceType t) => t == Gold \|\| t == Stone \|\| t == Wood \|\| t == Food;` | ✅ 1:1 |
| 12 | `Building.cs:672` | `RulerController.Instance?.Refund(refundPack, ratio);` | ✅ 1:1 |
| 13 | `Building.cs:653` | `public void Demolish()`（`:654 if (!CanDemolish) return;` ⇒ 无耗时字段 · 瞬时） | ✅ 1:1 |
| 14 | `Building.cs:827` | `if (!IsRefundResource(e.type) && e.type != ResourceType.Metal) continue;   // 旧口径：金/石/木/粮/铁`（位于 `GetRepairCost` 内） | ✅ 1:1 |
| 15 | `Building.cs:651-652`／`:676` | `:651` 注释「`09#48`…归 `M1-C`；`09#54`「改全退」同归 `M1-C`」／`:676` 注释「返还口径资源（金/石/木/粮 · 旧行为；`M1-C` 起应改全部 `def.cost` 资源）」 | ✅ 1:1（接缝注释确实已留） |
| 16 | `RulerController.cs:295` | 实读路径 `Assets/_Game/Systems/**Ruler**/RulerController.cs:295` ⇒ `public void Refund(ResourceList cost, float ratio = 1.0f)`（`:301 ModifyResource(e.type, true, Mathf.RoundToInt(e.amount * ratio));`） | ✅ 行号 1:1（⚠️ 任务书**未写子目录** `Systems/Ruler/`，仅路径书写不完整） |

⇒ **结论：任务书「现状实读」13 处全部准确（16/16 行号 1:1 命中，0 处行号漂移）**；唯 1 处路径书写不完整（第 16 行）。

**接缝实读确认**：任务书称「⭐ 接缝已就位」**成立** —— 进度体系（`Constructing` 态 ＋ `_pendingUpgrade`／`_pendingRepair` ＋ `Update` 按 `deltaTime` 推进）确已存在，本片**不必新建进度机制**。

---

## §二 逐条勘正（本端实读新发现 · ⛔ 不改变任务书结论，只补事实）

### 勘-1 ⭐ `KingdomTaskType` 已有 `Build`／`Repair` 两值，且 `Build` **生产码零调用方**

- 实读 `TaskPriorityConfig.cs:32-47`：11 项 —— `Repair／Build／Produce／Transport／Rancher／WaterCarry／GoldMine／Production／WaterHaul／Gather／AmmoReload`
- 全库 grep：`KingdomTaskType.Build` ⇒ **0 命中**（生产码与 Editor 探针均无）；`KingdomTaskType.Repair` ⇒ 仅 `Editor/Smoke/Smoke_2_23RB.cs:155/157/183/215`（优先级探针）⇒ **生产码零消费**
- ⇒ 件 1 的「搬料任务」**不必新增枚举值**（候选：复用 `Transport` ＋ `KingdomDestType.SpecificBuilding`（已存在 · `KingdomTask.cs:12`），或启用死值 `Build`）—— 避免枚举膨胀 ＋ 免动 `TaskPriorityConfig` 映射表

### 勘-2 ⭐ `:667` 与 `:827` 同形但不同职责，且**两者在现有 40 栋资产上「改前／改后读数逐值相同」**

- `:667`（`Demolish` 退还分摊）与 `:827`（`GetRepairCost` 修复费分摊）都是「按 `def.cost` 占比分摊」，但职责不同：**退还** vs **成本**
- ⭐ 实读 `M1A_资产迁移核对表.txt`（88 明细行 ＝ 40 栋 `cost` ＋ 21 条 `upgradeCost` ＋ 7 仓声明 ＋ 20 配置）：**全部 `cost` 只含 `金/石/木/粮`，`Metal` 只出现在 `arrow_tower`／`wall`／`gate`／`Blacksmith` 的 `upgradeCost`**
- ⇒ 「按**全部 `def.cost` 资源**摊」与「按**四资源**摊」在当前资产上**产出逐值相同**（`Demolish` 用 `def.cost` 摊，而**无任何 `cost` 含 `Metal`**）
- ⇒ ⚠️ **判据 4（「`cost` 含 `Metal` 等 ⇒ 该资源也进退还／修复分摊」）在现有资产上鉴别力 ＝ 0**（反向读数「`Metal` 永不出现在退还列表」改前改后**都成立**）⇒ 验收必须**由探针临时构造含 `Metal` 的 `BuildingDef`**（见 §六）

### 勘-3 ⭐「工地仓」全库不存在，且 `Building` 在 `Constructing` 态**不是合法任务源**

- 全库 grep `工地|ConstructionSite` ⇒ **0 命中**（`Assets/_Game/**`）
- `Building.IsValid => this != null && state == BuildingState.Active`（`Building.cs:1007`）；任务源注册**只发生在转 Active 时**（`Building.cs:530 RegisterWithTaskScheduler()` 位于 `OnConstructionComplete` 内；`EnterRuined` 侧 `:874 TaskScheduler.Instance.Unregister(this)`）
- ⇒ 件 1 的「工地仓 ＋ 搬料任务」**无任何现成载体**。任务书「**只需**把『扣费即启动』改为『投料齐 ⇒ 启动』」**低估了工作量**：实际需新增 ①**容器**（工地仓）②**任务源**（Constructing 态的合法广告者）③**任务类型／终点解析** ④**阈值拦截**（搬料不多搬）⑤**存档字段**（工地仓内容物 ＋ 投料进度）

### 勘-4 ⭐ **第 4 条「扣费即生效」路径：主城升级**（任务书未列）

- 实读 `KingdomManager.cs:183-192 TryUpgradeCastle()`：`:187 CanAfford` → `:189 RulerController.Instance.Spend(cost);` → `:190 SetCastleLevel(CastleLevel + 1);` ⇒ **扣费即生效、无进度、无投料**
- 调用方：`BuildingPanel.cs:346`（主城 `CastleCore` 的 Lv2+ 升级分支，与普通建筑升级 `:365-377` **不是同一条链**）
- ⇒ `09#64`「建筑升级…全走仓」的字面范围**是否含主城升级**，任务书 §二 四件未列 ⇒ 见 §三 歧义-5

### 勘-5 `totalInvested` 当前在「**下单时点**」累加，与「投料」口径不兼容

- 实读 `Building.cs:635`（`TryUpgrade` 内、`StartConstructing` 之前）：`totalInvested += SumCostOf(uc, includeMetal: true);   // 玩家已在外层扣款`
- 建造侧**无** `totalInvested` 写入点（`BuildController.TryBuild` 只扣费不记账）
- `Demolish:658`（`int invested = totalInvested > 0 ? totalInvested : costSum;`）与 `GetRepairCost:813` 都以 `totalInvested` 为基准
- ⇒ 投料口径下「投入」应改为「**实际入工地仓的量**」（否则拆除全退会退"从未搬进去的料"）—— 属落码口径，见 §四 自陈-3

---

## §三 ⭐ 阻塞项（5 条 · 需裁后方可落码）

### 歧义-1｜「工地仓」的载体是什么？

**问题**：`09` §16.1 ② 只写「工人把材料搬进「**工地仓**」」，**全文未定义工地仓是什么**（勘-3：全库亦无该概念）。

**硬约束（来自 `09` 自身）**：
- §16.1-3「建成 ⇒ 材料**转成「建筑本体」**（不可搬出 · ⛔ **不占产出容量**）」⇒ 工地仓**必须与产出仓分离**（若复用产出 `StorageComponent`，完工后材料会占产出容量线 ⇒ 违 `09` §5.2 硬规则 1）
- §16.2「**仓内容 ⇒ 掉箱**」＋ §16.1-4「退还**随本体掉箱**」⇒ 需定「本体」容器的落点

**候选**：
| 案 | 形态 | 代价 |
|---|---|---|
| **A（本端倾向）** | 新建 `ConstructionSiteStore`（挂建筑的**独立容器**，照 `TreasureVault` 先例：**子物体 ＋ 程序化 `StorageComponent` ＋ ⛔ 不入 `WarehouseRegistry`**） | 与产出仓结构性隔离；完工时清空（材料"转成本体"＝记账进 `totalInvested`，不占任何容量线）；拆除时按 `totalInvested` 掉箱 |
| B | 复用建筑 `StorageComponent` ＋ 加「工地模式」标志 | 改动小，但完工后需清账（否则违「不占产出容量」）；且完工瞬间会被 `Building.TryAdvertiseTask` ③ 当搬运源把料搬走 |
| C | 工地仓 ＝ 地面掉落箱（`ChestManager.SpawnChest`） | 复用箱子，但箱子有 1 游戏日倒计时（`09` §九）⇒ 搬料慢即过期 ⇒ **不推荐** |

**需裁**：**A／B／C**（＋确认「本体量」的持久化方式：由 `def.cost` ＋ 已升档 `upgradeCost` 派生，还是独立入档）

---

### 歧义-2｜⭐ **金（Gold）怎么投料？**（本片最大阻塞）

**实读证据**：
- `M1A_资产迁移核对表.txt` ⇒ **40 栋中 19 栋 `cost` 含金**（`Warehouse` 金4/石4 · `farm` 金50 · `quarry` 金50 · `market` 金80 · `gate` 金80/木50 · `catapult` 金100/石50 · `Blacksmith` 金50/石60 · `Barracks` 金16/石20 · `WarAcademy` 金30/石20 · `AdvancedStorage` 金6/石12 · `Hospital` 金6/石8/木10 · `Church` 金8/石10/木8 · `SiegeWorkshop` 金10/石20/木16 · `ArcheryRange` 金15/木30 · `LeyForge` 金25/石30 · `arrow_tower` 金20/石10 · `CrossbowTower` 金6/石16/木12 · `WarCamp` 金10/木25 · `magic_tower` 金10/石20）＋ 1 条 `upgradeCost` 含金（`Blacksmith` levels[0] 金75/石90）
- **金现状（`M1-E` 前）**：⛔ **不在任何仓里** —— `TreasureVault.cs:25 static readonly string[] VaultPaths = { "res_material", "res_food" };`（`:18-19` 注释明写「⛔ 不含 `res_currency`/`res_ammo`」，归 `M1-E`）；扣费走 `RulerController` **直通**（`WarehouseHelper.cs:39-46` 金判定分支 ＋ `:65-66` 实扣 `RulerController.Instance.Spend`）
- `09#47`（金币降普通资源 · 含「**扣费统一一个 API**」）**归 `M1-E`**，⛔ **不在本片范围**（`中层执行计划` §4.1 表）

**二难**：
- 若**金也投料** ⇒ 工人**无处搬金** ⇒ **19/40 栋建筑永久卡死**（工地仓永远凑不齐 ⇒ 永远不开工）
- 若**金仍直扣** ⇒ 与 `09` §16.1 ①「下单 ⇒ ⛔ **不是立即扣费**开工」**字面冲突**（部分豁免）

⭐ **本端推论（须一并确认）**：`farm`（金50）／`quarry`（金50）／`market`（金80）是**纯金造价** ⇒ 无论选哪条，它们都「**下单即开工**」（无料可搬）⇒ **判据 1 在这 3 栋上鉴别力为零**（改前／改后读数相同）⇒ 验收必须改用**含料建筑**（`House` 木4／`Granary` 木4）

**候选**：
| 案 | 内容 | 代价 |
|---|---|---|
| **金-A（本端倾向）** | 本片**金仍走「下单即扣」**（`WarehouseHelper` 金分支**一行不动**）＋ **非金资源走投料** ⇒ 建造 ＝「金即时扣 ＋ 料投齐 ⇒ 开工」 | `09` §16.1 ① 的「⛔ 不是立即扣费」在本片**对金豁免**（金实体化随 `M1-E`）；3 栋纯金建筑即时开工 |
| 金-B | 本片把金实体化（国库仓收 `res_currency` ＋ 工人搬金） | **越界到 `M1-E`**（连带 `RulerController` 只读门面化） |
| 金-C | 金不搬实体，但**扣费时点移到「料齐」**（账面划转） | 与 ① 一致；⚠️ 对纯金建筑读数与金-A **相同** |

**需裁**：**金-A／金-B／金-C**（＋确认「纯金建筑 3 栋即时开工」是否接受）

---

### 歧义-3｜**AI 国是否同走实体投料？**

- `09` §16.1 ① 明写「下单（**玩家/AI**）」；但**实读** `BuildController.cs:332-337 CanPayBuild`（`if (kingdomId <= 0) return WarehouseHelper.CanAfford(cost);` → AI 走 `ks.CanAfford`）与 `:339-346 PayBuild`（AI 走 `ks.Spend`）⇒ **AI 扣费走 `KingdomState` 台账**（抽象经济，**无实体仓参与扣费**）
- `09#52`「AI 国是第二本独立账 ⇒ 统一走仓」**不在本片范围**（表列归 `M1-B`；`HH.314` 实际未落该件 —— 实读 `_任务队列.md:61` 其交付内容为四件，非 `#52`）
- 若本片把 AI 改实体投料 ⇒ 需动 AI 扣费口径（＝`09#52` 范围）＋ AI 抽象日结（`AbstractEconomySettler`）连带

**候选**：
| 案 | 内容 | 代价 |
|---|---|---|
| **AI-A（本端倾向）** | 本片 **AI 保持台账直扣 ＋ 立即开工**（即 `09` §16.1 ① 的「玩家/AI」**对本片只对玩家生效**） | AI 与玩家行为分叉（AI 无搬料延迟）；AI 投料随 `09#52` 专项 |
| AI-B | AI 也走实体投料（**技术可行**：AI 建筑 `StorageComponent` 已在 `WarehouseRegistry`，按国过滤 `GatherActive(kingdomId)`；`TaskScheduler` 池隔离路由已就位） | **越界 `09#52`** |
| AI-C | AI 走「台账扣费 ＋ 虚拟耗时」（不搬料但等时间） | 折中，但引入第二套语义（**L-01 双真源风险**） |

**需裁**：**AI-A／AI-B／AI-C**

---

### 歧义-4｜`:827`（修复费分摊）是否同改「全部 `def.cost` 资源」？

- 任务书 §二 件 2 ⚠️ 要求「**须一并核**（同为旧口径）」
- 本端核：`GetRepairCost`（`Building.cs:810-833`）**不是退还**，是**修复费计算**（总额固定 ＝ `invested × RepairConfig.repairCostRatio`，分摊只决定"分到哪些资源"）；`09#48` 的差异项标题**只覆盖「拆除退还」只摊四资源**
- ⭐ **关键读数（勘-2）**：现有 40 栋 `cost` **只含 金/石/木/粮** ⇒ 改 `:827` 为「全部 `def.cost` 资源」**当前读数逐值零差异**
- 候选：**(a) 一并改**（口径统一 · 零读数差异 · 零风险 · 未来造价加资源即自动正确）／**(b) 保持五资源**（含铁 `Metal` 为 D131 刻意）
- **本端倾向 (a)**；但需裁 —— 因涉及「是否把 `09#48` 的口径**外推**到非退还路径」

---

### 歧义-5｜**主城升级**（`KingdomManager.TryUpgradeCastle`）是否纳入本片？

- 实读 `KingdomManager.cs:183-192`：`:187 CanAfford` → `:189 Spend(cost)` → `:190 SetCastleLevel(+1)` ⇒ **扣费即生效 · 无进度 · 无投料**（＝第 4 条「扣费即生效」路径 · 勘-4）
- `09#64` 字面为「**建筑升级**／修复／拆除不走仓 ⇒ 全走仓」；主城升级**是建筑升级的一种**（`CastleCore` 的 Lv2+），但与普通建筑升级**不同链**（普通走 `Building.TryUpgrade`＋`constructProgress`；主城走 `KingdomManager` 即时提级 ＋ 跨级解锁模块）
- 任务书 §二 四件的 `file:line` 清单**未列** `KingdomManager`
- **候选**：**(i) 纳入本片**（主城升级也改「投料 ⇒ 等时间」，代价：`CastleLevel` 是 `KingdomManager` 的独立状态机 ＋ 跨级模块解锁时点后移 ⇒ 连带面大）／**(ii) 不纳入**（本片只覆盖任务书已列的 4 条路径，主城升级另立）／**(iii) 纳入但只改扣费时点**
- **本端倾向 (ii)**（⛔ 不自行扩大解释；若纳入须单独立件）

---

## §四 本端自决落码口径自陈（⛔ 不阻塞 · 预先声明以免事后被判「自行扩大解释」）

| # | 自陈 | 依据 |
|---|---|---|
| 自陈-1 | **直建路径（`BuildingFactory.CreateBuildingInstance`）不走投料** —— 该路径是 Editor 探针／冒烟／地图预置／读档重建的**公共落点**（`BuildController.TryBuild` 之外的唯一实例化口），本片保持其「直接落成」语义不变（否则探针与地图预置全断）。**投料门只加在** `TryBuild`（玩家/AI 门面）＋ `BuildingPanel` 三入口 | `BuildingFactory.cs:89`；`BuildController.TryBuild` 为玩家＋AI 唯一门面（`TryPlace:195`／`KingdomBrain.cs:1261`） |
| 自陈-2 | **拆除耗时的数据来源 ＝ 新增 SO 字段**（`so-data-driven`：⛔ 禁硬编码魔法数值）。候选：`BuildingDef` 加 `demolishSeconds`，或复用现有建造配置 SO 加一项；缺省值须使「拆除 ≠ 同帧」。⚠️ 若策划端要求「与建造同长」⇒ 改为复用 `EffectiveDuration()`（`Building.cs:180-201`） | `so-data-driven` skill；`09` §16.3-3「与建造对称」未给数值 |
| 自陈-3 | **`totalInvested` 记账时点改「投料入仓时累加」**（对齐 `Demolish:658`／`GetRepairCost:813` 的「投入」语义）；建造侧需补一处写入点（现无），与 `invested = totalInvested > 0 ? totalInvested : costSum` 兜底并存 | 勘-5 |
| 自陈-4 | **掉箱 `Faction` 参数传 `Faction.None`**（＝`09` §9.8「箱子**无主**」的现状实践；先例 `DamageSystem.cs:633 SpawnChest(..., Faction.None)`）。`09#57`「`Faction` 参数去留」归 `M1-D` ⇒ **本片不改签名** | `09` §9.8；`ChestManager.cs:58 SpawnChest(GridCoord, ResourceList, Faction)` |
| 自陈-5 | **拆除的「进度」复用 `constructProgress` 0→1（`Constructing` 态）**，非「耐久递减」（`09` §16.3.1 表写「耐久到 0」＝语义描述；现成进度体系只有 `constructProgress`，任务书亦明示「本片不必新建进度机制」） | 任务书 §二 件 1 ⭐ 接缝；`Building.cs:439-453` |
| 自陈-6 | **件 3「随本体掉箱」只掉「退还量」，不掉建筑仓内容物** —— `09#49`「建筑损毁**不处置仓内存量** ⇒ 仓内容统一掉箱」**归 `M1-D`**（`中层执行计划` §4.1 表），本片不越界 | `09` §十二 `#49` 归 `M1-D` |

---

## §五 影响面（实读 · 供裁决评估）

**调用面（生产码 · 全部实读命中）**

| 入口 | 位置 | 现状 |
|---|---|---|
| 建造（**玩家＋AI 唯一门面**） | `BuildController.TryBuild:210` | 被 `BuildController.TryPlace:195`（玩家）与 `KingdomBrain.cs:1261`（AI）调用 |
| 升级 | `BuildingPanel.OnUpgradeClicked:365-377` | `RulerController.Spend(lvCost)` → `TryUpgrade()` |
| 废墟重建 | `BuildingPanel:310-323` | `WarehouseHelper.TrySettle(rc)` → `StartRebuildFromRuins()` |
| 废弃主城修复 | `BuildingPanel:326-341` | `RulerController.Spend(repairCost)` → `StartConstructing()` |
| **主城升级**（独立链 · 见歧义-5） | `BuildingPanel:344-353` → `KingdomManager.TryUpgradeCastle:183-192` | `RulerController.Spend(cost)` → `SetCastleLevel(+1)` |
| 拆除 | `BuildingPanel.OnDemolishClicked:380-388` | `_target.Demolish()` |

**存档面**：`BuildingSaveData`（`storageContents`／`treasuryContents`／`level`／`hp`／`maxHp`／`state`／`totalInvested`／`kingdomId`／`anchor*`）⇒ 工地仓内容物 ＋ 投料进度需入档（判据 7）。⚠️ 用户 §七-4「旧档不管」⇒ 判据只要求「**新档能存能读**」。

**数据面**：40 栋 `BuildingDef`（`cost`／`levels[].upgradeCost`）＋ `RepairConfig`／`BuildConfig`；新增 SO 字段须走 `[FormerlySerializedAs]` 三步法（自陈-2）。

**挂账 `DZ-1`**（面板容量跨行合计虚增 · `WarehousePanel.cs:109-159`）：`_任务队列.md:62` 归 `M1-C`；⚠️ `HH.314` 裁决 §二 **裁 A「本片不动」**＋ 该缺陷**当前不可达**（7 栋挂仓全专属路径）⇒ 见 §七 待裁 6。

---

## §六 验收取证方案（A/B 对照 · 改前／改后）

- 口径 #1：**A/B 开关仅供验收取证**（⛔ 不作长期双轨 · 用户 §七-3）⇒ 探针内以「旧直扣路径」取**改前**读数、以「投料路径」取**改后**读数，取证后**删开关**
- ⭐ **判据鉴别力补强（本端勘正 · 必做）**：
  - **判据 1**：必须用**含料建筑**（`House` 木4 ／ `Granary` 木4 ／ `Warehouse` 金4石4）—— ⛔ **不得用** `farm`／`quarry`／`market`（纯金造价 ⇒ 改前改后皆即时开工 ⇒ **零鉴别力**）
  - **判据 4**：必须由探针**临时构造含 `Metal` 的 `BuildingDef`**（`ScriptableObject.CreateInstance<BuildingDef>()` ＋ `cost` 加 `Metal` 条目）—— 现有 40 栋 `cost` **全无 `Metal`** ⇒ 否则**零鉴别力**（勘-2）
  - **判据 5**：`Faction.None` 掉箱 ⇒ 读「**国库是否即时增加**」（`TreasureVault.GetAmount`／`RulerController.Gold`）＋「**原地是否出现 `ChestEntity`**」
- **进局纪律**：走 `HH.92` 正门 `TestHarnessApi.EnterTestRun(cfg, speedOverride?)` ＋ 收尾 `ExitTestRun()`；L-32（真暂停 ＋ `EditorApplication.ExitPlaymode()`）；读数落 `Logs/`。

---

## §七 待裁汇总（可一键回复）

| # | 待裁 | 候选 | 本端倾向 |
|---|---|---|---|
| 1 | **工地仓载体** | A 独立 `ConstructionSiteStore` 子物体容器／B 复用产出仓加工地模式／C 地面掉落箱 | **A** |
| 2 | ⭐ **金投料** | 金-A 金仍下单即扣 ＋ 非金投料／金-B 金实体化（越界 `M1-E`）／金-C 金账面延扣 | **金-A** |
| 3 | **AI 路径** | AI-A 保持台账直扣 ＋ 立即开工／AI-B AI 实体投料（越界 `09#52`）／AI-C 台账 ＋ 虚拟耗时 | **AI-A** |
| 4 | `:827` 修复费分摊 | (a) 一并改全资源／(b) 保持五资源 | **(a)** |
| 5 | **主城升级** | (i) 纳入本片／(ii) 不纳入另立／(iii) 只改扣费时点 | **(ii)** |
| 6 | `DZ-1` 是否搭车 | 本片修／维持裁 A 不动 | **维持不动**（不可达；「容量列语义」宜与 `M1-C` 通用仓口径一并定） |
| 7 | 自陈-2 拆除耗时来源 | 新增 SO 字段／复用建造时长 | **新增 SO 字段** |
| 8 | 自陈 1／3／4／5／6 | 见 §四 | **照自陈** |

> ⚠️ 若策划端认可上表**全部倾向** ⇒ 回复「**全按倾向**」即可，本端随即开工。
> 预计改动面（文件级）：`BuildController`／`BuildingPanel`／`Building`／`BuildingFactory`／`StorageComponent`／`TaskScheduler`／`KingdomTask`／`TaskPriorityConfig` ＋ 新容器组件（歧义-1 案 A）＋ SO 字段（自陈-2）＋ 冒烟容器。

---

## §八 本端状态声明

- ✅ **已接单**（`D792` `HH.315` · 前置门两门已独立复核通过）
- ⛔ **`Assets/**` 一行未写**（含 `Assets/Editor/**`）—— 按任务书 §五 红线「先停手报裁」
- ✅ 本文件为**唯一**本端产出（`多Agent交接/执行端/HH.315_M1-C建造升级修复拆除全走仓_开工回执与口径报裁.md`）
- ⛔ 未碰：美术／pixel-forge／`GameScene`／`Packages`／`3.6`·`3.8` doc／`WarehousePanel`／`TreasureVault`／账本（`_任务队列`／`_编号登记`／`_当前快照`／`测试基线台账` 本端**未动** —— 派工已由 `D792` 落账）
- ⛔ 未 push
