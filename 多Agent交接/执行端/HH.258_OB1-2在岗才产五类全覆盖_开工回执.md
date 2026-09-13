# HH.258「产能须在岗·五类全覆盖（OB1-2）」开工回执

> 类型：开工回执（含 🔴 结构性报裁 · 停手待裁）
> 状态：✅ **已裁决（D704·2026-09-13·主策划端）**——原状态＝🔴 **停手待裁**（§0 gap 表第 4/5 环＝结构性不可闭：三处组件均无 `Production` 派工链 ⇒ 在岗判定恒不可达）
> 日期：2026-09-13 · 发起端：执行端 · 关联：**HH.257 施工任务书**（D703）／D702（V5+）／D672/D673/D677／`2_24 实施计划` §五 OB1-2 行／台账 `DZ-123`/`DZ-124`/`DZ-145`
> 取号：HH.258（账本水位线 256→258；D640 #10 先登记后落盘，独立单行 commit `6598d0b`）

---

## 一、作业依据实读（M2 判据三直读 · 执行端复核）

- **①设计稿逐字**：`2_24_..._架构提案.md` §8.1 五类定案表／§8.2 性能策略／§6.x V5 行（v7）；`2_24_..._实施计划.md` §五 批1 OB1-2 行；任务书 HH.257 全文；D672/D673/D677/D702 原文（0.6 §二百/二百零一/二百零二/二百零六/二百三十一）。
- **②代码落点实读**（本轮）：
  - `BlacksmithBuilding.cs:10/40-54`（类在职 · Tick 无门）
  - `SiegeWorkshopBuilding.cs:19/106-126`（类在职 · 产弹结算无门）
  - `MineByproductComponent.cs:27/84-92`（类在职 · 三槽副产无门）
  - `ProducerComponent.cs:39/144`（在岗范式：`HasWorkerAssigned => TaskScheduler.Instance.HasWorkerAssigned(_building)`）
  - `TaskScheduler.cs:138-149`（`HasWorkerAssigned(ITaskSource)`：存在 NPC 其 `task.source` **ReferenceEquals** 该源 **且** 态＝`Working`）
  - `Building.cs:932-995`（`Building.TryAdvertiseTask` 四分支）；`BuildingFactory.cs:288-319`（组件挂载分支）
  - `ProductionSystem.cs:27-47`（逐秒无条件 Tick 四组件，无门控）
- **③字段/资产直读**：`Resources/Buildings/mine.asset:52-64/73`（`outputResource=1`／`isResourceNode=1`／`isConsumable=0`／`isMineByproduct=1`）；`Building.cs:914`（`transportThreshold=0.8`）；`TaskScheduler.cs:43`（`workDuration=2s`）。

---

## 二、sim-sync 结论复述（D702 依据 · **零义务** · 不重审）

**复述 D702（V5+ 裁定）**：本批 sim-sync 义务＝**零**——
1. 三组件（`BlacksmithBuilding`／`SiegeWorkshopBuilding`／`MineByproductComponent`）与 `ProducerComponent`／`HasWorkerAssigned` 在训练仓 harness **全库零镜像**（D702 已 grep 实证，阳性对照＝`SimWorld`/`SimTask` 的 `Working` 系搬运任务态非产能门）；
2. `SimEconomy.cs:104` 属**独立经济近似**（`harness/Economy/` 私有副本，非决策核镜像），且其产能**本就带 `WorkersAssigned>0` 门**；
3. K 线王国训练**已冻结**（D681）⇒ 无「回灌失真」对象；
4. **Unity 侧照 D667/D672 最严档落门**（不正因 V5 延期）；甲（sim 补 Working）＝对不存在镜像空转、乙（Unity 采 assigned）＝推翻最严档，**均已否决**。
5. **承接账**＝`15_账本` 记 sim 协议差异（sim 维持分配即产近似，K 线重启时再评估对齐路径）。

**执行端复核确认**：标的文件均在 `_Game/Systems/**`（非 `AI.Core/**`），本批不触 `AI.Core`；sim-sync 结论照 D702 **零义务**，无需重审。

---

## 三、§0 L-30 全链门 gap 表逐环勾选（强制 · 任务书 §三）

| # | 门环 | 需要的证据 | 当前证据（本轮实读） | 判定 |
|---|---|---|---|---|
| 1 | 三处组件在场 | 三类在职且被 `ProductionSystem` 调度 | `BlacksmithBuilding.cs:10`／`SiegeWorkshopBuilding.cs:19`／`MineByproductComponent.cs:27` 在职；`ProductionSystem.cs:38-45` 逐秒调三者 | ✅ **闭** |
| 2 | 现无在岗门 | 三处产出路径无 `HasWorkerAssigned`/`Working` 判定 | 三处 Tick 全文无该判定（`BlacksmithBuilding.cs:40-54`／`SiegeWorkshopBuilding.cs:106-126`／`MineByproductComponent.cs:84-92`） | ✅ **闭** |
| 3 | 复用范式在场 | `ProducerComponent.cs:144` 判定可参照 | `ProducerComponent.cs:39/144`＋`TaskScheduler.cs:138-149` 在位可复用 | ✅ **闭** |
| 4 | **门控后可达**（有工人即产） | 有工人在岗 ⇒ 产出正常（正探针可造） | **🔴 结构性不可闭**——三处均**无 `Production` 任务源**：`Building.TryAdvertiseTask` 分支②（`Building.cs:959-966`）硬要求 `GetComponent<ProducerComponent>() != null`，而三处按 `BuildingFactory.cs:299-318` 用**专属组件替代**了它 ⇒ 无 `Production` 广告 ⇒ 无工人被派往「在三处工作」 ⇒ 门恒不可达（详见 §四） | 🔴 **不可闭（结构性）** |
| 5 | **门控后正确**（无工人即停） | 无工人在岗 ⇒ 产出恒 0（负探针可造） | `HasWorkerAssigned` 恒 false ⇒ 量产恒 0 ⇒ **「停产」退化为「永久停」**（非设计语义的"无人在岗即停产"） | ⚠️ **可闭但语义退化为永久停** |
| 6 | AI 保底不破 | `stone in >0` 且 ⑦/⑰ 可进池 逐条核 | 见 §五（施工前预判：本批将使 AI 三产面结构性不可达） | 🔴 **触发 M5 报裁** |
| 7 | 死分支删除无残留 | 金矿/副产判据/`byproduct*` grep 无生产路径消费 | D677 已预裁；施工前全链 grep 待做（**与本批核心独立·不阻塞**） | 🔜 **施工后实证**（本环独立可做） |
| 8 | 回归（五类全覆盖无侧漏） | 五类组件逐一对照 D672 定案表 | 定案表在场（3 新增＋2 保持＋1 豁免＋1 作废） | ✅ **闭** |

> **M1 结论**：第 **4、5 环未闭（第 4 环结构性不可闭）** ⇒ 按任务书 M1「任何环未闭 → **先报裁再施工**」与 M5「报裁不停工」，本回执**停手待裁**，**零业务代码改动**。

---

## 四、M3 实读：在岗判定复用范式 与 派工链在场性（**决定性发现**）

### 4.1 范式接口（复述，无异议）
`TaskScheduler.HasWorkerAssigned(ITaskSource producer)`（`TaskScheduler.cs:138-149`）＝存在 NPC 满足 `ReferenceEquals(task.source, producer) && _npcStateMap[npc]==TaskState.Working`。`Working` 系状态机第三态（`TaskScheduler.cs:421-475`，Transport 时长＝`workDuration` 默认 2s）。

### 4.2 🔴 关键：三处**均无 `Production` 任务源**（派工链缺失）
`Building.TryAdvertiseTask`（`Building.cs:932-995`）四分支，对三处的可达性逐一实读：

| 建筑 | ①Gather | ②**Production** | ③Transport | ④WaterHaul |
|---|---|---|---|---|
| 铁匠铺 | ✗（`isConsumable=0`，`Building.cs:944` 需 `isBeingGathered`；`Building.cs:862` 守卫） | **✗**（`Building.cs:939/959` 需 `ProducerComponent != null`；`BuildingFactory.cs:307-308` 未挂） | 仅当**本建筑 Metal 仓 ≥80%**（`Building.cs:970-971`；`BuildingFactory.cs:305` 挂了本体 `StorageComponent`） | ✗（需 producer） |
| 投掷机厂 | ✗（`isConsumable=0`） | **✗**（同；`BuildingFactory.cs:299-302` 只挂 `SiegeWorkshopBuilding`，**不挂** `ProducerComponent`/本体 `StorageComponent`） | **✗**（本体 `GetComponent<StorageComponent>()`＝null；三子仓系**子物体** `SiegeWorkshopBuilding.cs:58-59`，`GetComponent` 不收子物体） | ✗ |
| 矿洞（副产） | ✗（`mine.asset:64` `isConsumable=0`） | **✗**（`mine` 被 `BuildingFactory.cs:295` 的 `!isResourceNode` 排除，本体无 `ProducerComponent`/`StorageComponent`） | **✗**（本体无 `StorageComponent`） | ✗ |

**另实读**：`KingdomTaskType.Production` 全仓**唯一广告点＝`Building.cs:964`**（grep＝4 命中，余 3 为调度侧消费/调试）；`TaskScheduler._sources` 登记方仅 `Building`／`MineByproductComponent`／`TreeGatherSource`/`WorldGatherSource`／`UnitController`。⇒ **三处（含其专属组件）都不发 `Production` 任务**。

### 4.3 唯一「可为真」的路径均**自毁/循环**（逐一推演）
- **铁匠铺**：唯一途径＝分支③ Transport（source＝建筑），前置＝**本体 Metal 仓 ≥80%＝200**（容量 250，`Blacksmith.asset:41`）。而该仓只由 `BlacksmithBuilding.Tick` 的 `Transform(Ore→Metal)` 充填 ⇒ **须先产过 Metal 才有搬运工**。加门后：无搬运工 ⇒ 不产 ⇒ 仓恒 0 ⇒ 无 Transport 广告 ⇒ 无搬运工 ⇒ **永锁**。
- **投掷机厂**：四分支全灭 ⇒ `HasWorkerAssigned`（建筑/组件）**恒 false** ⇒ **永锁**。
- **矿洞副产**：`HasWorkerAssigned(_building)` **恒 false**；`HasWorkerAssigned(this 组件)` 唯一途径＝其自身 Transport 广告（`MineByproductComponent.cs:167-196`，`source=this`；前置＝子仓 ≥80%×`transportThreshold`）⇒ 同样**循环永锁**（须先产过才有搬运工）。

### 4.4 反面印证（既有文档/容器）
- `TaskScheduler.cs:21` 已把**同型失效**写进注释：「建筑不注册、任务永不派发、`ProducerComponent.HasWorkerAssigned` **恒 false 停产**」。
- 既有冒烟 `Valley_HH107_Smoke_Byproduct.cs:143-197`（P1）**在无任何工人的前提下断言矿洞副产子仓增长**；加门后该探针将 FAIL ⇒ 进一步印证「当前口径＝无门恒产、无派工链」。
- 全仓 Editor 容器**无任何**「给铁匠铺/投掷机厂派工」的用例（grep 实证）。

### 4.5 结论
**「复用 `ProducerComponent.cs:144` 范式」的判定口本身在场，但其**前置派工链**在三处**结构性缺失**。**三处要落地 §8.1「工人在岗 `Working` 才产」，除「补在岗门」外，还必须先补「`Production` 任务源（意图→派工→到场）」**——这正是 §8.1 条律「意图 → 派工 → 工人到场 → 执行 → 搬运入账」的**前半段**，任务书范围（「只补在岗门」）未含。

---

## 五、M5 AI 最小可玩保底核验（**施工前预判**）

- **保底判据**＝`stone in > 0` 且 ⑦招兵/⑰建军可进池（`2_24` §8.1 第 5 条 / D673）。
- **现状**：`HH.221` 已达成（D701 · G1-1 PASS）＋`DZ-135` 金门**未达成**（③`BuildCapacity` `cost gold=50` 不可行）。
- **本批影响预判**：三处（铁匠铺 Metal／投掷机厂 三弹药／矿洞副产 水晶·火油·矿石）在 AI 场景**由「恒产」直接变为「永久 0 产出」**（§四 4.3）。其中：
  - **矿洞副产矿石**（`MineByproductComponent` 第三槽，T1.4/D609）＝**矿石→Metal 链供给端**；AI 无此副产即**铁链断供** → 触 `DZ-135` 同域（③ 金门之外的第二层不可达）。
  - **投掷机厂**弹药＝AI 军事链消耗面（`3.6.1 弹药消费清单`）。
- ⇒ **命中 M5 触发条件「本批使 AI 某产面结构性不可达」** ⇒ 按任务书 M5 **报裁**（执行端当场列证据，正本回执即证据）。

> 注：`stone in>0` 与 ⑦/⑰ 可进池**本身**不直接依赖上述三产面（石经 OB1-1 采集通道、⑦/⑰ 走建军链），故**不构成 G1-1 回退**；但「矿洞副产矿石→Metal」与「弹药供给」确被本批抬至不可达。

---

## 六、报裁事项（每项：选项＋推荐＋影响）

### R1 **确认停手待裁**（本批核心机制不可达）
- **建议**：采纳。**推荐**——§四已证「只补门」在三处结构性不可达；若强行落门，三处在正常游玩下**永久停产**（与验收线 1「有在岗 ⇒ 正常产」、M5 保底直接冲突）。
- **影响**：本会话**零业务代码改动**，等裁决；`DZ-123`（🟡已裁·待落批）暂缓收口。

### R2 **派工链补全口径**（决定本批实现形态）
- **A（推荐）·补派工链＋落门**：在 `Building.TryAdvertiseTask` 增「专属产能组件也发 `Production`（source＝建筑）」分支（覆盖铁匠铺／投掷机厂）；`MineByproductComponent` 增 `Production` 广告（与其既存 `Transport` 广告合流或分帧，需定序）；随后三处落 `HasWorkerAssigned` 门。
  - **理由**：§8.1 条律要求**完整劳动链**（意图→派工→到场→执行）；本选项使「在岗才产」**真实可达且可正探针**，是设计忠实解。
  - **影响**：改动面**超任务书「三处补门」范围**（须动 `Building.cs`＋`MineByproductComponent.cs`＝新行为：工人被派往三建筑）；验收线 1~3 正探针方可成立；承接账/回归面相应扩大（需授权）。
- **B ·严格字面落门（不补派工链）**：`if (!HasWorkerAssigned(_building)) return;` 三处直加。
  - **影响**：**铁匠铺/矿洞副产永锁·投掷机厂恒停**（§四 4.3）；验收线 1~3 正探针**不可造**；`Valley_HH107_Smoke_Byproduct` P1 破。**不推荐**（除非策划端明确要「无派工链即停产」的语义）。
- **C ·拆批（缩范围）**：本批只做「死分支全链删除（D677）＋水井豁免落 `15_账本`＋承接账（旧『恒产』表述清残）」，三处在岗门**拆到「补派工链」独立批**（另立任务书）。
  - **影响**：零结构性风险、可即时施工；但本批主体（五类全覆盖）延后。
- **D ·另定口径**：由策划端另定「在岗」语义（如「该建筑被任意任务占用」等）——须给判据来源与排除项（L-35）。

### R3 §0 gap 表**第 4 环**实测结论回写
- **建议**：把「门控后可达」由「🔜施工后实证」改判为 **「🔴 结构性不可闭（待派工链补全后重评）」**，并纳入 `DZ-123` 备注（新识别「派工链在场性」为 OB1-2 的第 4 环缺口，同 `L-30`「判定线硬条件可达性未先证」/`L-21`「出口存在 ≠ 出口可达」家族）。

### R4 台账状态
- **`DZ-123`**（🟡已裁·待签署批）→ 建议加注「⚠️ 施工前发现派工链缺失（HH.258 §四），待 R2 裁决后重评落地形态」。
- **`DZ-124`**（✅已裁 D702）、**`DZ-145`**（随批·死分支删除）＝**不受影响**，`DZ-145` 可随 R2 定案后一并处理。

---

## 七、下一步建议

1. **策划端裁 R1~R4**（尤其 R2 口径 A/B/C）。
2. 若裁 **A**：本批范围扩为「补派工链（`Production` 广告）＋落三处在岗门＋死分支删除＋水井豁免＋承接账」；执行端据裁决**重出开工回执/勘正单**再施工（守 D640 #10 不预留号）。
3. 若裁 **C**：执行端**即时施工**（死分支删除＋水井落账＋承接账），三处在岗门另批。
4. 无论何裁，**改前必重读磁盘**（agent-handoff §六）；写-改-commit 同串、只提本批文件、不 push。

---

## 八、红线自检 / 零改动面声明

- **零业务代码改动**：本会话 `Assets/_Game/**` **未改任何文件**；仅取号（`多Agent交接/_编号登记.md` 水位线行）＋本回执落盘。
- **未进局 / 未跑局**：未触发 `EnterTestRun`，无跑批（故 L-32/L-34 无关）。
- **sim-sync**：结论复述 D702＝零义务（§二），未触 `AI.Core`/sim/训练仓。
- **证据口径（L-35）**：本回执全部断言的"口径来源"＝磁盘实读 `file:line`（§一/§四列全）；排除项＝未跑局、未做运行时探针（纯静态直读判定）。
- **判据三直读（L-24/L-25）**：设计稿逐字＋代码落点实读＋字段/资产直读三项均已过（§一）。
- **检索自证（L-29）**：`KingdomTaskType.Production`／`HasWorkerAssigned` 等检索均含阳性对照（全仓命中 `file:line` 列全）。
- **教训引用**：`L-30`（第 4 环全链门 gap）／`L-31`（同源判据禁另造）／`L-21`（出口存在 ≠ 出口可达）／`L-35`（口径声明）／`L-24`/`L-25`（判据三直读）。

---

> 执行端（TraeCode）｜2026-09-13｜本回执＝**停手待裁**版开工回执；裁决后按 §七 续做。

---

## 九、策划裁决（策划端回写 · D704 · 2026-09-13）

> 判据三直读＝**已过**（①HH.257 任务书＋`2_24 §8.1/§8.2/§6.x V5`＋本回执逐字＋教训库；②代码落点实读：`Building.cs:933/953-961`／`BuildingFactory.cs:295-322`／`TaskScheduler.cs:138-149/254/846-848/577-583/1101-1105`／`ProducerComponent.cs:24/79`；③独立复核：删除面 grep 零命中／全 37 asset `outputResource` 逐值核）。**§四 结构性发现＝成立**（策划端独立复核与执行端一致，含三处自毁/循环路径逐一推演）。

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| **R1 确认停手待裁** | **采纳** | §四 实读坐实：三处均无 `Production` 广告源 ⇒ `HasWorkerAssigned` 恒不可达 ⇒「只补门」＝**永久停产**（非"无人在岗即停"）。守 M1「任何环未闭 → 先报裁再施工」，**零业务代码改动**正确。 |
| **R2 派工链补全口径** | **裁 A（补派工链＋落门）**；**实现形态口径勘正＝❌ 不授权动 `Building.cs`**（含 `TryAdvertiseTask` 分支结构）；授权「三处产能组件自身获得 `Production` 广告能力」。 | **B 否决**（＝把 AI 三产面推向结构性不可玩·违 §8.1 第 5 条与 M5）；**C 已由本批执行**（"先做独立部分"）；**D 由本裁给出语义**（见下 §三-3）。A 为设计忠实解（§8.1 条律前半段）。形态勘正理由见 `0.6 §二百三十三 §三-1`——**可行性已实读闭合**：`TaskScheduler.cs:1101-1105` 已内建 `Component→GetComponentInParent<Building>()` 归属路由（先例＝`MineByproductComponent`）；`ResolveDest(None)`→`destPos=SourcePos`（`:846-848`）／`ExecuteCompletion(Production)` 对非 `Producer` 源为 no-op（`:581-583`）／`LoadInventoryFromSource` 仅 Transport 走（`:690`）。 |
| **R3 §0 gap 表第 4 环回写** | **采纳** | 改判「🔴 结构性不可闭（待派工链补全后重评）」；`DZ-123` 加注（已落）。 |
| **R4 台账状态** | **采纳** | `DZ-123` 加注；**`DZ-145` ✅已清**（本批删除验收成立）；`DZ-124`（D702）不受影响。 |
| **§三`1` 范围/改动面** | 见下 §三 | 改动面**重定＝A 段 3 cs／OB1-2 全批 7 cs**（4 删除面＋3 门面）；**报告口径「6cs」算错**（漏计 `BlacksmithBuilding`/`SiegeWorkshopBuilding`）。 |
| **§三`2` 验收** | 见下 §三 | 线 7 重定＋线 1~3 探针＋**新增 §8.1 第 5 条 AI 保底两段门**。 |
| **§三`3` 矿洞副产派工语义** | **裁 (a) ＋两条钉死** | 见下 §三。 |
| **§三`4` 承接账时点** | **准**（A 落地后同批清残） | 见下 §三。 |

### 分歧裁决记录（`design-advisor` §分歧裁决专项 · 双方原文保留）

- **执行端意见（原文保留·未采纳）**：「A（推荐）·补派工链＋落门：在 `Building.TryAdvertiseTask` 增『专属产能组件也发 `Production`（source＝建筑）』分支（覆盖铁匠铺／投掷机厂）；`MineByproductComponent` 增 `Production` 广告」；影响自陈＝「改动面超任务书『三处补门』范围（须动 `Building.cs`＋`MineByproductComponent.cs`）⇒ 需授权」。
- **策划端意见**：**不动共用派工枢纽**——`Building.TryAdvertiseTask` 是**全建筑（玩家/AI/自然）共用**的派工枢纽，在其上加专属组件类型判别＝把 `Building` 耦合到三个专属组件，且副作用波及全库建筑；改由三处产能组件**自持**广告（source＝组件），复用既有 `Component→父建筑` 归属路由，**零新增机制**（先例 `MineByproductComponent`），与 2_24「去特判/单轨」主旨一致。
- **裁决＝采策划端**。**依据（坐标系）**：范围纪律（共用枢纽 blast radius ＞ 组件自持）／兼容风险（`Building.cs` 副作用波及全库建筑）／AI 北极星（两形态对 AI 等价·不冲突）／开发成本（组件自持复用既有路由）。
- **落败方意见保留存档＝本条**（不删除·裁决可能错·留作复盘依据）。

### §三 勘正四项（要点·全文见 `0.6 §二百三十三`）

1. **范围**：授权「三处产能组件自身获得 `Production` 广告能力」；❌ 不动 `Building.cs`。**白名单＝本批唯一 cs 面 7**（`ProducerComponent`/`BuildingSaveData`/`Building`/`BuildingFactory`［已交付删除面］＋`BlacksmithBuilding`/`SiegeWorkshopBuilding`/`MineByproductComponent`［A 段］）。**禁 `git add -A`**，只提本批清单。
2. **验收**：线 7 重定（白名单 7 cs＋「零改动面」明文＝`AI.Core`/sim/训练仓代码/数据 SO/`TaskScheduler` 主体路由/`Building.TryAdvertiseTask` 分支结构）；线 1~3＝负探针＋正探针＋同 seed 修前/修后对照（M7 两态×有无在岗·**硬断言无在岗产量恒 0**）＋`L-34` 五列＋`L-35` 口径·**走正门**；**新增硬验收＝§8.1 第 5 条 AI 保底两段门**（**前段·施工前只读**「AI 工人占用账＋三产面可达性预判」·L-30 式 gap 表含**值 gap＋时间 gap**·预判不过⇒**报裁不得施工**；**后段·落地后**实测三处产量 >0＋`stone in>0`＋⑦/⑰ 可进池·结构性不可达⇒报裁不停工）。
3. **矿洞副产派工语义＝裁 (a)（两条钉死）**：**(a-1)** 门＝`HasWorkerAssigned(本组件)`（任意任务类型 `Working` 均算在场；与 (b) 门同、仅广告序不同）；**(a-2)** 广告序＝**硬 if/else**（无在岗⇒发 `Production`；有在岗⇒走既有三仓 `Transport` 判定），**禁同 tick 双发**（`TaskScheduler.cs:254`）。**铁匠铺另计**：因 ③搬运广告 source=建筑，门须 `HasWorkerAssigned(组件) ‖ HasWorkerAssigned(_building)`。
4. **承接账时点＝准**（A 落地后同批清残 `D562/HH.107`、`D609/D199~201`、`HH.19/D207~212`＋`15_账本`）；清残判据须写「**误表述语义零残留**」并给正确措辞正例（`L-26`）。

### 衍生产物

- 新建设计文档：**无**（裁决即口径）。
- 新建清单任务：**无新签任务书**——A 批以 **HH.257 尾部勘正块**为准；完成报告按账本实时水位线取号。
- 教训：**`L-30` 家族＋1**（并入硬性检查项，不新立条目）；伴生认账＝HH.257 验收线 7「上限 4 cs」与 §一 面数自相矛盾（签发侧）。

> 策划端（主策划端）｜2026-09-13｜回写五处：本区 · `0.6 §二百三十三` · 账本（D704） · `_任务队列.md` · `缺陷台账.md`（外＋`_交接索引.md`／`_策划教训库.md`／HH.257 勘正块）。
