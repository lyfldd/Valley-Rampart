# HH.30 · 2_17 步骤11 改造计划（per-kingdom 九系统全量归属）· 已裁决·三批实施收官

> 类型：策划报告请求（Gate 前置——先报装配计划再动代码，HH.24 之裁，设计正源 D289/D330 第二步）
> 状态：✅已裁决（§四）+ 三批制实施收官（§六，2026-08-28）+ **步骤11 收官已确认（§七，2026-08-28）**
> 日期：2026-08-28 · 发起端：执行端 · 关联：HH.29（P0+步骤10 收官）/ HH.24（步骤8 设计报告"/裁后动工"铁律 / HH.28（Faction 收编于步骤10 已收口）
> 前置：P0 / 步骤10 已收官（HH.29 §九 收盘）；per-kingdom 第一步（归属地基 5 系统）已落地（2_17 步骤3/4/5/2b）；AIEconomySettlement（第6段）已于步骤2b 落地

---

## 〇、执行端勘察声明（重读代码，非凭记忆）

本报告全部结论基于对以下文件的实际重读（绝对路径核实，行号标注）：
`HappinessSystem/SatietySystem/TaxSystem/TradeSystem/RanchSystem/SiegeProductionSystem/DayCycleSettlement/CastleUnlockTable/KingdomManager/WaterNetwork/ProducerComponent/AIEconomySettlement/KingdomState/KingdomRegistry/KingdomBrain.ExecuteTech/UtilityActionConfig`。勘察证实的每处守卫、每个入账点、每个字段均有行号注明。

---

## 一、必答四问

### 问① 桶0语义保持点逐系统标注（玩家=桶0 零回归如何保持 + 与步骤4 守卫关系 + Tax/Trade 财政面单标）

**统一方法论（D329 门面模式）**：九系统均为 `Singleton<T>` 门面单例，内部状态以 `Dictionary<int, ...>` 按 `kingdomId` 分桶（玩家=0）；**公开门面签名可能微调（加 kingdomId 参数）或保留受控过载，玩家现有调用点（DayCycleSettlement 玩家段 / UI / RulerController 交互）读到 id=0 桶结果 = 与现状逐字节一致**。逢外出参数默认填 0，玩家调用零改动。

| 系统 | 现状（勘察） | 桶0保持点 | 与步骤4守卫关系 |
|---|---|---|---|
| **HappinessSystem** | 全局单例，`OverallHappiness`/`TaxBurdenLastDay` 单标量（L22/25）；OnNewDay/L67 用 `unit.kingdomId != 0` 内联守卫（L89） | 玩家走 id=0 桶 → 结果与现状一致 | **守卫被桶语义吸收**：现状「kingdomId!=0 排除 AI」将升格为「按 id 分桶计数」，AI 工人进自己桶；玩家桶不再需要排除判断。⚠️OnUnitDied（L56-72）缺 kingdomId 守卫，仅靠 Faction，分桶时一并补齐 |
| **SatietySystem** | 饱食数据存 `UnitController.Satiety`（个体），无字典；`GetAverageSatiety`/`OnNewDay` 用 `kingdomId!=0` 内联守卫（L74/95） | 玩家 id=0 桶均值 = 现状 | **守卫被桶语义吸收**：现状「AI 不吃玩家国库粮/拉低均饱食」升格为分桶均值；AI 走自己桶。SatietySystem 自身无 AI 国库概念（FeedUnit 只走当前 scope），改造主要把均值/逐日估值按桶拆 |
| **TaxSystem**（⚠️涉玩家财政） | 全玩家专属：`LastDayTax` 单标量（L19）、人头税读 `PopulationSystem.PopulationCount`（L50）、建筑税集全图 commercial（L57）、落账 `RulerController.ModifyResource(Gold)`（L78）、税负写 `HappinessSystem.TaxBurdenLastDay`（L86） | 玩家 id=0 桶：人头税=玩家人口、建筑税=kingdomId==0 的 market、税负落玩家桶 → 结果一致 | 无既有守卫（本就只服务玩家）；改造两端并发：a) 分桶计税（AI 人头税=KingdomState 人口、建筑税=AI 建筑）b) 落账路由（玩家→Ruler/TreasureVault，AI→KingdomState.resources）。**AI 是否开征、税率配 SO（挂 KingdomBrainConfig 或独立 TaxConfig）需策划顺裁** |
| **TradeSystem**（⚠️涉玩家财政） | `SellToGold`/`BuyWithGold` 四处 `RulerController.ModifyResource`（L100/101/130/131）、额度 `KingdomManager.TryConsumeTradeQuota`/单玩家数组（L37/305）、面板 `TradePanel` 纯玩家 UI 触发 | 玩家 id=0 桶：额度/落账走玩家真源（Gold→Ruler、非金→TreasureVault）→ 现状一致 | 改造核心：a) 额度改 per-kingdom（`Dictionary<int,int[]>TradeQuota` 或挂 KingdomState）b) 落账路由分国。**AI 是否接入贸易（Market AI 触发 + 模拟交易）是增量决策，建议本步仅做"结构分桶保玩家回归"，AI 主动贸易顺裁**（无 AI 市场 UI 消费面，见下） |
| **RanchSystem** | 单 `List<AnimalEntry> _animals`（L20）无 kingdom；`BuyCub`/`OnNewDay`/`Slaughter` 全读 `RulerController`（L67-72/85-99/129）；存档单 List（L195） | 玩家 id=0 桶 `_animals[0]` → 现状 | 无守卫（单桶=玩家桶）；改造 `Dictionary<int,List<AnimalEntry>>` + 存档 `Dictionary<int,List<...>>`（KingdomSaveData schema 拆 `kingdoms[]` 归 2_11 统一迁移，旧档展开单元素）+ AI 喂粮扣 `KingdomState.resources.Food` |
| **SiegeProductionSystem** | `GetPlacedMachineCount` 硬编码 `Faction.Human_Player`（L78）、`ProduceMachine` 扣 `RulerController`（L100/102）+ 固定 `SpawnUnit(Human_Player)`（L105）；无日结入口 | 玩家 id=0：`kingdomId==0` 计玩家机器、产机器喂玩家国库 → 现状 | 无守卫；改造 `kingdomId` 维度 + `SpawnUnit` 归属 AI 阵营分支。**注意：本类工程上已半退役（§13.7 弹药真源移 SiegeWorkshopBuilding）**，本步只做归属结构走通，是否还重建完整 AI 生产链建议与步骤13（SimModeManager）衔接时再顺裁 |
| **DayCycleSettlement** | 已有第6段 `AIEconomySettlement.Tick()`（L69，`kingdomId>0 跳过玩家`其样板）；其余段（Satiety/Happiness/Tax/Population/Ranch）玩家侧单调 | 保持玩家段逐字调用 → id=0 桶 | 已是 per-kingdom 遍历样板（TickKingdomBrains L91-106 + AIEconomySettlement L23-33 先例 `IsPlayer continue` + 固定排序 L87-95） |
| **CastleUnlockTable/KingdomManager** | CSUT 纯只读静态表（rows L14）；**解锁态实际在 KingdomManager**：`ModuleLevels[6]`/`ResearchLevels`/`CastleLevel` 全局单例（L34/46） | 玩家 id=0：`ModuleLevels[0]` → 现状 | 改造：解锁态 per-kingdom（拆到 KingdomState 或 KingdomManager 内部 `Dictionary<int,...>`），CastleUnlockTable 静态表保持共享（D330「配置 SO 全局共享不复制」）|
| **WaterNetwork** | 单例全局 `_stored`/`capacity`（L16-18）**无王国字典**；但**产水端守卫已在**（见问②，2_17 步骤3 已落） | 玩家 id=0 消费 Player.WaterNetwork → 现状 | 见问②问答 |

**步骤4 关账扫描②类守卫（Happiness L89 / Satiety L74/95 的 `kingdomId!=0`）** → **结论：升格吸收进分桶**，不留双轨。理由：分桶后「AI 工人不稀释玩家幸福 / 不吃玩家粮」由「AI 进自己桶」结构性达成，独立守卫成为过时死代码；且 OnNewDay 用单位遍历，天然可按 `unit.kingdomId` 归桶，守卫逻辑被循环替换。唯一须补：Happiness.OnUnitDied 破缺守卫按桶补齐（问①表内已标）。

---

### 问② WaterNetwork AI 行为语义表态（建议置信低，请策划核定）

**勘察事实（重读 ProducerComponent.cs L120-124）**：AI 井**当前已不产水**——`if (_isWell) { if (_building.kingdomId > 0) return; TickWaterToNetwork(); return; }` 是 2_17 步骤3 落的水井路径守卫（注释明言防王国脑/模板池/动态立国 castle 复用 Well def 时泄入玩家供水网）。即「AI 井不产水」**现状已实现**，且是「守卫拦截 + WaterNetwork 自身单桶只服务玩家」两层。

**执行端建议（请策划确认/反驳）**：
- **赞成策划初步判断：AI 井仍不产水**（无 AI 侧用水消费场景——农场耗水是玩家农业，AI 无粮食生产副链）。**因此从"否决守卫 + 行为语义"看：本步可只配置化不做行为重写**——WaterNetwork 保持玩家级桶（或可加 kingdomId 但 AI 桶恒空、产水仍拦），改造只做「把 WaterNetwork 按 D330 结构归桶以防未来 AI 有消费时扩」，**行为零变化**。
- **口径澄清**：策划原话「改造只做结构不做行为」——执行端完全同意，且要补充：**结构上 WaterNetwork 是否值得拆桶存疑**，因为当前只有玩家 TrueUser。执行端推荐**最简方案**：WaterNetwork 本步**不拆桶**，仅把已有守卫（ProducerComponent L124 / TaskScheduler WaterHaul）保留为守卫形态，把「AI 井不产水」作为既有行为写进验收清单。**若策划坚持拆桶（为将来 Abstract 口径铺路），则拆到 KingdomState 侧但 AI 桶产水端恒 return**。
- **请策划选一个**：A) 保持现状（守卫+单桶，最简） B) 结构拆桶+AI 恒空（为步骤13 铺路）。

---

### 问③ 回归基线声明

**每改一个系统跑一次「同 seed 玩家侧逐字节一致」冒烟，复用 Valley2_17_Smoke_P0 作常驻回归基线**——同意，并补充执行细节：
- P0 套件已含 `A3确定性逐字节` / `RD2-①轮间清点 b=2684/2684/2684`（HH.29 §一）——即 **P0 是现成的玩家侧同 seed 基线**，改造后玩家场景 b/u 计数必须逐字节∈与 P0 一致。
- 每系统改造后**单独跑 P0 子探针**（Smoke_P0 的 RD/A3 段），确认玩家侧零回归；九个系统逐系统 + 最终联调各跑一次。预期 P0 常驻 = 每系统一次 + 九系统合跑 = 共 10 轮。
- 关键约束：AIEconomySettlement、TerritorySystem 先例已证明「固定排序 + IsPlayer 跳过」跑得同 seed 逐字节一致；九系统沿用该模式，确保改造不引入随机/时序方差。
- **玩家侧逐字节一致的具体判据**：同 seed 下玩家建筑数/单位数/国库读数/幸福饱食均值/牧场动物数/科技解锁态/水网存量在前 P0 基线锚定值内不变。

---

### 问④ CastleUnlockTable 闭环点（解锁态 per-kingdom + 回接步骤10 ExecuteTech ⑧）

**现状勘察**：`KingdomBrain.ExecuteTech`（KingdomBrain.cs L250-261）当前是**占位**——`cost = max(1, cfg.techUpgradeCostGold)`（SO 值=80，KingomBrainConfig L93）→ `kingdom.Spend(new ResourcePack{gold=cost})` + `Bump(...ok:true)` + Debug.Log「⑧科技升级落地（金-{cost}；per-kingdom 解锁态步骤11 接入）」；**不真正触达任何解锁态**。评分侧 `UtilityScorer.TechGap`（L127-128）纯按金存量正向（`金/needA`），未读解锁态。

**闭环位置（列明）**：
1. **解锁态载体迁址**：`KingdomManager.ModuleLevels`（全局）→ per-kingdom 载体（KingdomState 增 `moduleLevels`/`castleLevel`，或 KingdomManager 内部 `Dictionary<int,int[]>`）。CastleUnlockTable 静态表保持共享（只读）。
2. **ExecuteTech 真实化**（KingdomBrain.cs L250-261）：从占位「Spend 金 + Bump」升级为「校验目标模块**当前是否已解锁该级** → 未解锁才花 cost → `KingdomState.moduleLevels[module]++`（依 CastleUnlockTable.GetModuleLevel 增长）」。解锁后影响评分（TechGap 读解锁态 → 已满级减仓）。
3. **回接验收**：⑧可被选中 → 执行 → 空链路不再「花金但无解锁」（原占位只扣钱无效果），改为真实提升 `moduleLevels`，且两王国各自独立（K1 升 Civil 不影响 K2）。
4. **评分联动**：`UtilityScorer.TechGap` 改为「按 kingdom 读解锁态，目标解锁度越高分数越降（已满减仓）」，而非纯金存量——否则 AI 会反复砸钱科技（鹤无效果）。
5. **存档 schema**：KingdomSaveData 拆 `kingdoms[]` 时 `moduleLevels` 随王国入档（归 2_11 统一迁移，旧档展开单元素）。

---

## 二、逐系统验收序（建议改造顺序 + 独立验收点 + 文件脚印）

> 依赖序：先改「无依赖纯归属」→ 再改「跨系统消费」。共同依赖=DayCycleSettlement 段 + KingdomSaveData schema 拆 `kingdoms[]`（预留，不阻断）。

| 序 | 系统 | 独立验收点 | 文件脚印 |
|---|---|---|---|
| 0 | **KingdomSaveData schema** | kingdoms[] 拆桶旧档展开、moduleLevels/res/* 随王国入档 | KingdomState.cs / SaveManager.cs / KingdomRegistry.cs（预留） |
| 1 | **DayCycleSettlement 段序** | 段序重组后玩家侧 P0 逐字节一致（Ranch/Tax 改成 AIEconomySettlement 段调用） | DayCycleSettlement.cs |
| 2 | **HappinessSystem** | 玩家 id=0 桶=现状；AI 桶独立计数；OnUnitDied 破缺守卫补；两桶不串 | HappinessSystem.cs (+ KingdomState) |
| 3 | **SatietySystem** | 玩家均值不变；AI 桶按 Scope 均值；（SatietySystem 是否接入 AI 国库进食需厘清——见验收备注） | SatietySystem.cs |
| 4 | **TaxSystem** | 玩家税额不变；AI 人头/建筑税分桶落 `KingdomState.resources`；（税率 SO 顺裁） | TaxSystem.cs (+ KingdomBrainConfig/TaxConfig) |
| 5 | **TradeSystem** | 玩家额度+落账零回归；AI 贸易是否接入顺裁（默认结构分桶） | TradeSystem.cs / KingdomManager.cs（额度拆桶）|
| 6 | **RanchSystem** | 玩家 `_animals[0]`=现状；AI 牧场扣 `KingdomState.resources.Food`；存档 kingdoms[] 迁移 | RanchSystem.cs |
| 7 | **SiegeProductionSystem** | 玩家机器计数/产出零回归；AI 归属分支走通（SpawnUnit 归属）；与步骤13 衔接顺裁 | SiegeProductionSystem.cs |
| 8 | **CastleUnlockTable/KingdomManager 解锁态** | moduleLevels 拆 KingdomState；玩家 id=0 一致；两 AI 互不影响 | KingdomManager.cs / CastleUnlockTable.cs / KingdomState.cs |
| 9 | **WaterNetwork** | 玩家 TODO 按问②选 A/B 口径验收 | WaterNetwork.cs / ProducerComponent.cs（守卫保留或拆桶）|
| 10 | **回接 ExecuteTech ⑧** | ⑧可执行且真实提升 moduleLevels、两王国独立、评分 TechGap 读解锁态、P0 玩家侧零回归 | KingdomBrain.cs / UtilityScorer.cs |

**文件脚印预估**：核心改动 ≈ 11 个系统文件 + 2 个 SO 配置（Tax 税率 / Trade 额度若拆 SO）+ 存档 schema 预留 = **13~15 个文件**，沿用「逐个改造逐个冒烟」纪律（每系统 1 次 P0 基线 + 全量 1 次）。

---

## 三、待策划裁决（请拍板三点）

1. **TaxSystem AI 是否开征 + 税率载体**（独立 TaxConfig SO or 挂 KingdomBrainConfig）？
2. **TradeSystem AI 是否接入**（本步默认只结构分桶、AI 无主动贸易，顺裁其是否进入 AI 经济闭环）？
3. **WaterNetwork 问②裁决**：A) 保持现状单桶+守卫（最简，行为零变） / B) 结构拆桶+AI 桶恒空（为步骤13 铺路）？

> 另附：SiegeProductionSystem 完整 AI 生产链是否本步重建，建议到步骤13（SimModeManager）再顺裁，本步只走归属结构。

---

*已裁决动工，见 §五/§六。*

---

## 六、三批实施收官回写（2026-08-28 执行端，步骤11 收官待策划确认）

> 留痕：本节两次写入均被同工作区并行会话（HH.31 对账端）覆盖丢失，第三次改用脚本落盘并同命令 commit（多会话并行纪律：文档修改即写即提交）。

### 6.1 批次交付实录（commit 链）

| 批 | commit | 内容 | 验证结果 |
|---|---|---|---|
| 批1·结构零接触 | 4a6ee3e | TaxSystem AI 开征（独立 TaxConfig SO+asset，玩家分支逐位保留+AI 走 KingdomState 台账）；Trade 额度分桶（API 未动，AI 贸易留 TODO 归 P2）；Siege 归属结构 overload；Hap/Sat 守卫升格标注；序0 schema 预留（KingdomState.moduleLevels/castleLevel） | 0 错；P0 纯绿（b=2684×3，A3/A4 OK） |
| 批2·计算路径 | f0a1efb（含 using 修复 amend） | Happiness 分桶（getter 读桶0 逐位等价，OnUnitDied 按 kingdomId 分流补 AI 桶=§四破缺守卫清偿）；Satiety 均值分桶（FeedUnit 不动）；Ranch List→Dictionary（玩家桶0 扣 Ruler 原逻辑，存档 struct 未动归 2_11）；KingdomManager 解锁态镜像接线→KingdomState[0]；TaxSystem AI 幸福系数改读 per-kingdom 桶 | 首跑编译错（漏 using×2）修复 amend 后 0 error；P0 纯绿（A3/A4 OK） |
| 批3a·水网 B′ | 45171ae | WaterNetwork 拆桶（玩家桶0=_stored 逐位等价；AI 桶恒 0）；ProducerComponent.TryConsumeFarmWater L208 补 kingdomId 路由（AI 农田吃 AI 桶→缺水停产，堵吃玩家网水泄漏面=B′②）；AI 井恒不产水守卫已在（B′①） | 0 error |
| 批3b·科技闭环 | b15d1ca | ExecuteTech 真升 moduleLevels[target]+1（clamp 城堡1 上限+升满停防刷分）；TechGap 改读解锁态（升满=0=§四问④硬性）；AI 立国 castleLevel=1+moduleLevels 全0（RegisterNewKingdom，2_16 语义正源）；techTargetModule SO 字段（默认 Civil）；新增 Smoke_11 三探针 | 0 error；S11 探针 ALL PASS；P0 纯绿且 B5 build15/15 try30 与批2 逐位一致（行为零漂移） |

### 6.2 批3 前置阻塞与策划补裁（留痕）

批3b 动工前执行端报阻塞（AI castleLevel 无驱动源/moduleLevels 未初始化/目标模块来源），策划补裁 Q1→A（techTargetModule 禁选 Science——CastleUnlockTable.asset 实证 module5 城堡1 无解锁，选之闭环死路）/ Q2→A′（castleLevel=1 为 2_16 立国语义正源非占位；moduleLevels 全0 起步；AI 城堡升级记债挂账）/ Q3 确认（UtilityScorer 纯函数层玩家天然零接触）/ WaterNetwork 先行批准。批3 据此拆 3a/3b 双 commit。

### 6.3 §二 序0~10 对账

序0=批1 载体（kingdoms[] 拆分归 2_11）；序1=实施方式偏差（诚实对账）：未做段序重组，改为各系统 OnNewDay 内部 per-kingdom 分桶，达成序1 目的（AI 结算进日结链）且玩家段零改动，验收点由批1/批2 P0 纯绿达成；序2~10 分别落批1/2/3（Happiness/Satiety/Ranch/解锁=批2；Tax/Trade/Siege=批1；水网=批3a；ExecuteTech=批3b）✅。

### 6.4 探针证据（S11 ALL PASS 原文摘录）

S11-①AI桶初值=OK(AI王国×4)（k1~k4 castleLevel=1 moduleLevels=全0）/ S11-②水网B′=OK 玩家桶[注+10/扣2/存量8.0] AI扣99折=阻(缺水停产) 零染=OK / S11-③TechGap点环=OK 目标=Civil(cap=1) 当前Lv=0 需升=Y(闭环活) 升满后TechGap归零=OK / ===== ALL PASS ===== / [P0完整局] ===== ALL PASS(状态面) =====（A3/A4 OK；B5 build15/15 try30 与批2 逐位一致）

### 6.5 挂账清单（继承+新增）

- AI 城堡升级动作（批3b 新增记债）：恒 1 级直到真实玩法需求，随军事期一并议
- 存档 kingdoms[] 完整迁移（含 moduleLevels/Ranch AI 桶）→ 2_11
- Trade AI 主动贸易 → P2 训练侧（代码留 TODO）
- Siege 完整 AI 生产链 / Satiety AI 国库进食 → 步骤13/14 AbstractEconomySettler 一并裁
- 玩家死亡/GameOver 链路 → 独立回归（HH.27②）
- 细模拟经济闭环/工人真走真产 → P1 收官人工 Play 批（让渡照旧）

### 6.6 步骤11 收官判定（执行端自评，待策划确认）

九系统 per-kingdom 归属全量落地，三批 P0 基线全绿（玩家侧零回归逐位成立），S11 三探针闭环，四问裁决+三点顺裁+节奏补令全部执行到位，无破基线事件（批2 编译错为交付前拦截，未触达基线）。**步骤11 收口态成立，报策划确认收官。**

---


## 四、策划裁决回写（2026-08-28 已裁决，覆盖全文）

**四问裁决**：
- **问① 准**。统一方法论（Singleton 门面+Dictionary 分桶、逢外出参默认0）与守卫升格吸收照准——**Happiness.OnUnitDied 破缺守卫分桶时一并补齐**（报告自曝项列入该步验收）。SatietySystem「是否接 AI 国库进食」：**本步只拆均值/结算桶，FeedUnit 消费语义不动**（AI 进食语义归步骤13/14 与 AbstractEconomySettler 一并裁）。
- **问② 裁 B′（含耗水端补漏修正，非原 B）**。报告 B「拆桶+AI恒空」隐含「为步骤13 铺路」；但策划核码发现**真铺路面在农田耗水端**：AI 农田若入 AI 桶且未来产水，TryConsumeFarmWater 当前无守卫（L208 ConsumeWater(2f) 直连单例吃玩家网水）。裁定：**WaterNetwork 拆 per-kingdom 桶 + 双行为语义锁死**：①AI 井恒不产水（守卫保留升格为桶路由：AI 产水若将来有意义进 AI 桶）②AI 农田耗水走自己桶（本步补 kingdomId 路由——AI 农田产粮消耗 AI 桶水；AI 桶水恒 0=AI 农田停产，与 AI 无供水链既有语义自洽，堵住吃玩家水泄漏面）。**定性：这是把步骤3 只堵一半的洞堵全，非为步骤13 铺路。**
- **问③ 准**。10 轮（9 系统各1+全量1）+ P0 锚定值判据（b/u 计数∈基线）照裁。
- **问④ 准，五点闭环全收**（迁址 KingdomState/真实化/回接/评分联动/存档 kingdoms[] 归 2_11）。**TechGap 改读解锁态是硬性**——否则「花金无效果」变「花金刷分循环」，闭环不完整。

**三点顺裁**：
- **TaxSystem AI 开征：征，税率挂独立 TaxConfig SO**（不挂 KingdomBrainConfig——脑配置=行为参数域、税率=经济参数域，混挂将来调参互相污染；so-data-driven 分域铁律）。税率占位对人头/建筑各一值。AI 开征意义=国库金来源多样化（现状全靠 2b 日结+抽象结算）+ 让 AI 税负→AI 桶幸福（TaxBurdenLastDay 分桶）有真实数据流。
- **TradeSystem AI 接入：本步不接，只结构分桶**。AI 无市场 UI、无交易决策评分项（15 项无），接了无消费方。挂账：AI 主动贸易归 P2 训练侧（econ-train 才有意义），本步留 TODO 注释。
- **WaterNetwork：B′**（见问②，含耗水端补漏——对报告 A/B 之外修正案）。
- **SiegeProductionSystem 半退役定性准**：本步只走归属结构，完整 AI 生产链归步骤13 顺裁。

**执行序（策划令）**：本裁决回写 → 按 §二 序 0~10 实施（序0 schema 预留先行）→ 每系统 P0 基线跑 → 回接 ExecuteTech → 全量 1 轮 → 步骤11 收官报裁。**中途任何系统玩家侧基线破 → 停手报裁。**

---


## 五、实施节奏补令（2026-08-28：批1~批3 三批制，替代逐系统制）

**策划裁定：否决逐系统(A)/按序两批(B)/全量(C)，自造方案 D=风险三批**（按玩家侧风险分，不按序号均分）。单次验证成本≈几分钟（进 Play+新局+点菜单触发，pump 主体脚本化，非手玩 45 天）。

| 批 | 系统 | 风险性质 | 验证 |
|---|---|---|---|
| 批1·结构零接触 | Tax AI开征(独立TaxConfig SO)+Trade分桶不接+Siege归属结构+守卫升格吸收 | 玩家侧零/低语义 | P0 基线×1（预期纯绿）|
| 批2·计算路径 | Happiness+Satiety+Ranch+解锁接线 | 玩家桶须与原全局语义逐位一致 | P0 基线×1 |
| 批3·语义高风险 | 水网B'(拆桶+AI井恒不产水+TryConsumeFarmWater L208补kingdomId路由)+ExecuteTech五点闭环(TechGap改读解锁态) | 动玩家侧真实接触面 | 冒烟(执行端自跑,含正负探针)+P0基线×1 |

**依赖自检**：解锁(批2)→科技回接(批3) 不倒置 ✅；其余相互独立 ✅。

**配套纪律**：
- **commit 铁律**：每批单独 commit（批内按系统可多个）；破基线定位手段 = revert 单批重跑，非代码考古（HH.16「批次现在提交勿攒」同理）。
- **验证分工**：编译 0 错(GetDiagnostics)+grep玩家面+正/负探针冒烟=执行端自主；P0 基线=用户触发（进Play→新局→Valley/验证/2_17_P0_完整局验收），执行端交付报告贴 console 断言原文。
- **让渡口径照旧**：细模拟经济闭环/工人真走真产仍归 P1 收官人工 Play 批，本三批不重复。
- 批序(序号)仍按 §二 0~10 实施，验证节点插批边界。

---

## 七、策划确认（2026-08-28 策划端回写：步骤11 收官成立）

**抽查记录**（裁决前必抽查纪律）：
- 四 commit 实存（git log：4a6ee3e5 / f0a1efba / 45171ae0 / b15d1ca7）+ meta 补提交 1cb8c962 + §六第三次落盘 c20e53cf 在案；
- git show 文件构成核对：批3b=5 文件（Smoke_11/KingdomBrainConfig/KingdomBrain/UtilityScorer/KingdomRegistry）、批3a=2 文件（ProducerComponent/WaterNetwork）——与报告一致，无意外混入，执行端未混入并行会话半成品（对账端 _目录.md/缺陷台账）正确；
- S11 三探针=行为级（含负探针：AI 农田扣99折=阻/水网零染 OK）；P0 三轮纯绿 + B5 build15/15·try30 与批2 逐位一致=**行为零漂移成立**；
- 批3b 执行与本端补裁逐项吻合（techTargetModule 默认 Civil 禁选 Science / castleLevel=1 为 2_16 立国语义正源 / moduleLevels 全0 起步 / clamp 城堡1上限 / 升满停防刷分）；
- 序1 方式偏差（系统内 per-kingdom 分桶替代段序重组）=既定裁决方向内的实现细节自裁，同目的+更小侵入+玩家段零改动，验收点已达成，**准**。

**裁决：步骤11 收官成立。** 挂账清单（§6.5 六项）确认入账。

**插曲处置**：§六 L111 并行覆盖事件（两次写入被 HH.31 对账端覆盖）沉淀为新纪律 → `vr-triage-flow` §三：**并行会话写共享文件（HH/索引/台账/目录）写完立即 commit（写-改-commit 同串关闭窗口）**。

**下一站**：
1. **步骤12（领土推进+吞并接线，④债到期点）**——Gate 纪律照旧：先出设计报告报裁，再动工。引用 2_17/2_16 实施计划时注意 HH.31 对账的 🟡 账本滞后类漂移，以最新裁决为准。
2. **HH.31 对账报告（13 项漂移）**归策划端下一步逐条裁决，不阻塞执行端步骤12 设计报告。

---

## 策划裁决记录（HH.30 全程留痕索引）

| 事项 | 裁决 | 日期 |
|------|------|------|
| 四问+三点顺裁 | §四（B′ 补漏修正/独立 TaxConfig/Trade 不接/Siege 半退役定性准） | 2026-08-28 |
| 实施节奏 | §五（否决 A/B/C，自造风险三批制） | 2026-08-28 |
| 批3 前置阻塞 | §6.2（Q1→A 带禁选 Science 约束/Q2→A′ 语义正源/Q3 确认/WaterNetwork 先行批准） | 2026-08-28 |
| 步骤11 收官 | §七（收官成立，抽查全过） | 2026-08-28 |