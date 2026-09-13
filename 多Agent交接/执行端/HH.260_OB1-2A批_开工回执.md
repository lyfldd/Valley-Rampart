# HH.260「OB1-2 A 批：三处产能组件自持 `Production` 广告 ＋ 落岗门」开工回执

> 类型：开工回执（含 🔴 施工前只读预判门 · **停手待裁**）
> 状态：🟡 **预判 PASS ⇒ 回执落盘后停手等裁**（策划端确认后方可施工）
> 日期：2026-09-13 · 发起端：执行端 · Gate：`G1-2`
> 关联：**HH.257 任务书（D703）＋尾部「⚠️ 勘正（D704）」块**（勘正块优先）／**D704（`0.6 §二百三十三`）**／HH.258 §九／HH.259 §六／台账 `DZ-123`（加注）/`DZ-135`（加注）/`DZ-145`（✅已清）
> 取号：HH.260（账本水位线 259→260·独立单行 commit `1e4f70d`）

---

## 一、作业依据（M2 判据三直读 · 执行端复核）

- **①设计稿逐字**：HH.257 全文＋尾部 D704 勘正块（**与正文不一致时以勘正块为准**）；`2_24` §8.1 定案表/§8.2/§6.x V5 行；`0.6 §二百三十三`（D704）；HH.258 §九·HH.259 §六。
- **②代码落点实读**（本轮）：
  - 三处现状无门：`BlacksmithBuilding.cs:40-54`／`SiegeWorkshopBuilding.cs:106-126`／`MineByproductComponent.cs:84-92`；`ProductionSystem.cs:27-47` 逐秒无条件 Tick（四组件）。
  - 判定口/范式：`TaskScheduler.cs:138-149`；`ProducerComponent.cs:24/79`。
  - **组件源机制（D704 可行性基石）**：`TaskScheduler.cs:1096-1107` `SourceKingdom`（`Building→kingdomId`／`Component→GetComponentInParent<Building>().kingdomId`）／`:846-848` `ResolveDest(None)→destPos=SourcePos`／`:577-583` `ExecuteCompletion(Production)` 取 `comp.GetComponent<ProducerComponent>()`⇒三处建筑本体**无**该组件⇒**no-op**／`:1123-1132` `GetTaskDuration`（非 Gather⇒`workDuration`）／`:437` Working 计时到即 Complete／`:259` 同源+同类型去重（每源至多 1 工人）／`:51/:280` `maxWorkersPerTask=8`（Transport 规模派工）。
  - 资产：`mine.asset:52-64/73`（`outputResource=1`·`isResourceNode=1`·`isConsumable=0`·`isMineByproduct=1`）；`UtilityActionConfig.asset:320-338`（id17 BuildBlacksmith）／`:472-490`（id25 BuildSiegeWorkshop）。
- **③字段/配置直读**：`KingdomFoundry.cs:33-34/68-75/151-190`（tier→预置与工人）；`KingdomFoundry.cs:99-126`（工人直出）；`KingdomFoundingConfig.asset:31-76`（tier：`buildingCount` 5/6/7·`workerCount` 4/6/8）；`Kingdom_IronHoof.asset:32-39`（`baseBuildingDefIds` 第 5 项＝`mine`；六模板同构）。

---

## 二、sim-sync 结论复述（D702 依据 · **零义务** · 不重审）

**复述 D702（V5+ 裁定）**：本批 sim-sync 义务＝**零**。①三组件与 `ProducerComponent`/`HasWorkerAssigned` 在训练仓 harness **全库零镜像**（阳性对照＝`SimWorld`/`SimTask` 的 `Working` 系**搬运任务态**，非产能门）；②`SimEconomy`（`harness/Economy/`）属**独立经济近似**（非决策核镜像），其产能本就带 `WorkersAssigned>0` 门；③K 线王国训练**已冻结**（D681）⇒ 无「回灌失真」对象；④**Unity 侧照 D667/D672 最严档落门**（不因 V5 延期）；甲（sim 补 Working）/乙（Unity 采 `assigned`）**均已否决**。承接账＝`15_账本` 记 sim 协议差异（**HH.259 半程已落「一·补二十九」**）。

**执行端复核**：本批标的均在 `_Game/Systems/**`，不触 `AI.Core`/sim/训练仓代码。

---

## 三、§0 L-30 全链门 gap 表（照 HH.257 §三 格式 · **按 D704 分两列**）

> D704 补硬性条款：gap 表须分列〔**前置条件在场性｜签发前必闭**〕与〔**施工后可达性｜可留施工后**〕。

| # | 门环 | 需要的证据 | 当前证据（实读） | 前置条件在场性 | 施工后可达性 |
|---|---|---|---|---|---|
| 1 | 三处组件在场 | 三类在职且被 `ProductionSystem` 调度 | `:40-54`／`:106-126`／`:84-92`；`ProductionSystem.cs:38-45` | ✅闭 | — |
| 2 | 现无在岗门 | 三处产出路径无 `HasWorkerAssigned`/`Working` 判定 | 同上（无该判定） | ✅闭 | — |
| 3 | 复用范式在场 | `ProducerComponent.cs:144` 判定可参照 | `ProducerComponent.cs:24/79`＋`TaskScheduler.cs:138-149` | ✅闭 | — |
| 4 | 门控后可达（有工人即产） | 有在岗 ⇒ 产出正常 | **HH.258 §四：三处均无 `Production` 广告源 ⇒ 不可达**；D704 R3 改判＝「🔴 结构性不可闭（**待派工链补全后重评**）」 | **🔴 结构性不可闭** | 见 §3.1（补全后全链已闭） |
| 5 | 门控后正确（无工人即停） | 无在岗 ⇒ 产出恒 0 | 判定口在场；门落即可判 | ✅闭 | 🔜施工后负探针 |
| 6 | AI 保底不破 | `stone in>0` 且 ⑦/⑰ 可进池 逐条核 | HH.221 达成（D701）／⑦⑰ 可进池（D684） | ✅闭 | 🔜**前段只读预判＝§四**；🔜后段落地后实测 |
| 7 | 死分支删除无残留 | 金矿/副产判据/`byproduct*` grep 无消费 | HH.259 已交付；`DZ-145` ✅已清（D704） | ✅闭 | — |
| 8 | 回归（五类全覆盖无侧漏） | 五类逐一对照 D672 定案表 | 定案表在场（2 新增门＋3 已合规/豁免/作废） | ✅闭 | — |

### 3.1 第 4 环「补全后重评」闭环证据链（本批施工形态 ⇒ 可达）

| 环 | 证据 | 判定 |
|---|---|---|
| a. 组件成为 `Production` 源 | 三处组件新增 `TryAdvertiseTask` 发 `KingdomTaskType.Production`（source＝组件；照 `MineByproductComponent` 既有 `ITaskSource` 先例） | 本批新增（施工项） |
| b. 池路由正确（AI 国工人只领本国任务） | `TaskScheduler.cs:1096-1107`（Component→父建筑 `kingdomId`）；`:278/:291` 池隔离 | ✅实读闭 |
| c. 落点可解析 | `:846-848` `ResolveDest(None) → destPos = SourcePos` | ✅实读闭 |
| d. 完成动作零副作用 | `:577-583` `ExecuteCompletion(Production)` 对三处建筑本体为 **no-op** | ✅实读闭 |
| e. Working 窗口成立 | `:1123-1132` 非 Gather 用 `workDuration`（默认 2s）；`:437` 计时到即 Complete；期间 `HasWorkerAssigned==true`（`:138-149`） | ✅实读闭 |
| f. 每源至多 1 工人 | `:259` 同源+同类型去重 | ✅实读闭 |

⇒ 第 4 环＝「**补全后可达性**」由 a~f 全链闭合（a 为本批施工项，b~f 为既有机制在场）。

---

## 四、🔴 施工前只读预判门（D704 新增硬门 · 施工前必过）

> 依据＝`2_24` §8.1 第 5 条 AI 最小可玩保底 ＋ D704 §三-2；防 **HH.221 血案**（「A 落地后才知判定线不可达」）重演。

### 4.1 AI 工人占用账（供给 vs 需求）

| 侧 | 项 | 值 / 来源（实读） |
|---|---|---|
| 供给 | 立国直出工人 | `tier.workerCount`＝**4/6/8**（`KingdomFoundingConfig.asset:34/49/64`；`KingdomFoundry.cs:68-69/99-126`，D1 立国即出） |
| 供给 | ⑥招工人目标 | `needA=10`（`UtilityActionConfig.asset:112-117`）；**观测值：worker=8（五考）／8~11（七考 k4@D78·k1@D94，HH.231）** |
| 供给 | 生育 | Child→Resident（**不增 `workerCount`**；`PopulationSystem.cs:435`＋口径 `:116-117`） |
| 需求 | `Production` 常驻占用 | 挂 `ProducerComponent` 条件＝`rate>0 && !isResourceNode && !isBlacksmith && !isSiegeWorkshop`（`BuildingFactory.cs:295-311`）⇒ 全资产扫描＝**{farm(2.0/s)·quarry(5.0/s)·AdvancedStorage}**；**AI 可达＝{farm, quarry}**（无任何行动指向 AdvancedStorage） |
| 需求 | 其他瞬时占用 | Transport（`Building.cs:963-975`＋`MineByproductComponent.cs:167-196`；规模派工上限 8）／WaterHaul（AI 水桶恒 0 ⇒ AI farm 常驻广告，`Building.cs:977-986`）／Gather（`WorldGatherSource`） |

**AI 国常态账**：立国工人 4~8（⑥ 目标 10）；`Production` 常驻占用 ≈ **1**（模板含 `farm`；`quarry` 仅要塞档预置或 ③ 建造，而 ③ 受 50 金门阻断＝`DZ-135` 未闭）⇒ **常态空闲工人 ≳ 3**。

### 4.2 三产面建筑数（AI 侧）

| 组件 | AI 侧建筑数 | 依据（实读） |
|---|---|---|
| **`MineByproductComponent`（mine）** | **每 AI 国 ≥1 座（必预置）** | `KingdomFoundry.PlaceBuildings`（`:151-190`）取 `tpl.baseBuildingDefIds` 前 `tier.buildingCount` 项；六模板 `baseBuildingDefIds` 第 **5** 项＝`mine`（`Kingdom_IronHoof.asset:32-39` 等）；`buildingCount`＝**5/6/7** ⇒ **各档都含 mine**；`kingdomId=state.id>0`（`:179-180`）。地图生成只产 `FeatureType.Mine` 数据格**不产建筑实体**（`MapGenRules.DeriveNaturalBuildings` 仅派生 OreVein/WoodPile/StonePile）；玩家不可建（`mine.asset:60 isPlayerBuilt:0`） |
| **`BlacksmithBuilding`** | **0 座**（本批时点） | AI 唯一获取路径＝id17 BuildBlacksmith（`UtilityActionConfig.asset:320-338`：`costGold=50`·`costStone=60`·`minStage=Develop`）；`DZ-135` 实测 AI 国库 `gold=3~20 ≪ 50` ⇒ `Feasible=false` |
| **`SiegeWorkshopBuilding`** | **0 座**（本批时点） | AI 唯一获取路径＝id25 BuildSiegeWorkshop（`:472-490`：`costGold=30`·`minStage=Military`·`buildTargetCap=1`）；G2-1 已通后仍受 30 金门 |

### 4.3 L-30 式 gap 表（**值 gap ＋ 时间 gap**）

| 产面 | 建筑数（需求工人） | 常态空闲工人 | **值 gap** | 建筑出生日 | 工人出生日 | **时间 gap** | 判定 |
|---|---|---|---|---|---|---|---|
| **mine（副产）** | 1 | ≳3（6~8 − 常驻≈1 − 瞬时） | **闭**（1 ≪ ≳3） | D1（立国预置） | D1（立国直出） | **0** | ✅ **PASS** |
| Blacksmith | 0 | — | **0 需求**（无建筑） | — | — | — | ✅ 不受影响 |
| SiegeWorkshop | 0 | — | **0 需求**（无建筑） | — | — | — | ✅ 不受影响 |

### 4.4 预判结论 ＋ 残余不确定性（诚实声明）

- **结论＝预判 PASS（不构成结构性不可达）**：AI 侧本批**唯一受影响面＝矿洞副产**（每国必预置 1 座），其所需 1 名在岗工人 ＜ 常态空闲工人（≳3），且建筑/工人**均 D1 同时在场**（时间 gap＝0）；黑匠/投掷机厂 AI 侧当前 **0 座**（受金门/阶段门，属 `DZ-135` 既有域）⇒ **本批不改变其可达性（不产生退化）**。
- **残余不确定性（1 项）**：Transport 为**规模派工**（上限 8，`TaskScheduler.cs:51/280`）＋ AI farm `WaterHaul` 常驻（AI 水桶恒 0）⇒ **峰值**占用可上升，`idle ≥ 1` 属「**常态成立、峰值待实证**」；但即便峰值无空闲工人，矿洞 `Production` 任务仅**延迟**（他任务完成后即被派），**非结构性不可达**。
- **可选加固（请策划端裁）**：如需把「峰值占用」实证化，可授权本端**新增只读探针**（Editor-only·`Assets/Editor/**`，**不入 `_Game` 7 cs 白名单**）跑短窗（正门 `EnterTestRun`·同 seed），输出「逐 AI 国 × `workerCount`／空闲工人／三产面建筑数」。**未获授权前本端不新增任何文件**。

---

## 五、施工口径确认（D704 §三-3 已钉死 · 逐条复述）

1. **范围**：三处产能组件**自持** `Production` 广告能力（`BlacksmithBuilding.cs`／`SiegeWorkshopBuilding.cs` 各自成为 `ITaskSource`；`MineByproductComponent.cs` 既有 `TryAdvertiseTask` 增 `Production` 分支）。**❌ 不动 `Building.cs`（含 `TryAdvertiseTask` 分支结构）**；**A 段 3 cs**；OB1-2 全批唯一 cs 面 **7**（4 删除面已交付＋3 门面）。
2. **门**：`HasWorkerAssigned(本组件)`（**任意任务类型 `Working` 均算在场**）；**铁匠铺另计**＝`HasWorkerAssigned(组件) ‖ HasWorkerAssigned(_building)`（其 ③搬运广告 source＝建筑）。
3. **广告序（矿洞副产）＝硬 if/else**：**无在岗 ⇒ 发 `Production`；有在岗 ⇒ 走既有三仓 `Transport` 判定**；**禁同 tick 双发**（`ITaskSource` 同 tick 只返一任务，`TaskScheduler.cs:254`）。
4. **复用既有判定口**：`TaskScheduler.HasWorkerAssigned`，**禁另造**（`L-31`）。
5. **❌ 不改**：`AI.Core`／sim／训练仓代码／数据 SO／`TaskScheduler` 主体路由／`ProducerComponent` 主产-副产门（保持）。

### M7 新旧输入集对照表（硬断言：**无在岗 ⇒ 产量恒 0**）

| 组件 | 工人数 | 门控前 | 门控后（本批） |
|---|---|---|---|
| 铁匠铺 | 0 | 逐秒产（无门） | **恒 0**（`HasWorkerAssigned(组件)‖(建筑)`＝false） |
| 铁匠铺 | >0 但**无人 Working** | 产 | **恒 0** |
| 铁匠铺 | >0 且有 Working | 产 | **产**（输入集不变：`_storage`/`_def`/`_rate`/矿石源同前） |
| 投掷机厂 | 0 / >0 无在岗 | 产弹 | **恒 0** |
| 投掷机厂 | >0 且有 Working | 产弹 | **产**（原料/容量/轮产序同前） |
| 矿洞副产 | 0 / >0 无在岗 | 三槽恒产 | **恒 0**（三槽累积前 `return`） |
| 矿洞副产 | >0 且有 Working | 三槽恒产 | **产**（三槽 rate/容量同前） |
| 对照 `ProducerComponent` 主产 | 0 | 恒 0 | 恒 0（**不动**） |

---

## 六、验收计划（A 落地后执行）

| # | 线 | 判法（口径·`L-35`） |
|---|---|---|
| 1~3 | 三处正负探针 | **负探针**：无在岗 ⇒ 产量 0（口径＝逐秒产出日志/子仓增量；排除项＝容量满·原料缺·未 Active）；**正探针**：有在岗 ⇒ 产；**同 seed 修前/修后对照**（M7 两态×有无在岗·硬断言「无在岗产量恒 0」）；**走正门 `EnterTestRun`** |
| 4 | 承接账 | `D562/HH.107`／`D609/D199~201`／`HH.19/D207~212` 下游文档＋`15_账本` **同批清残**；判据写「**误表述语义零残留**」＋给正确措辞正例（**禁纯关键词零命中**，`L-26`） |
| 5 | 零改动面 | 白名单 7 cs；「零改动面」明文＝`AI.Core`/sim/训练仓代码/数据 SO/`TaskScheduler` 主体路由/`Building.TryAdvertiseTask` 分支结构；禁 `git add -A` |
| 6 | 后段门（AI 保底） | 实测三处产量 >0 ＋ `stone in>0` ＋ ⑦/⑰ 可进池；**结构性不可达 ⇒ 报裁不停工** |
| 7 | 编译 | 0 error（存量分离）＋ 退 Play |
| — | 在线判据表 | 若跑局：附 **`L-34` 五列**（可判定最早日／命中即停／服务哪条验收句／作用域〔全批 any-国｜指定国〕／口径来源与排除项） |

**跑局档位（沿用既有纪律）**：同 seed（不试 seed／`L-30`）·正门 `EnterTestRun`·收尾真暂停+Save+`ExitTestRun`+退 Play（`L-32`）。

---

## 七、红线自检 / 零改动面声明

- **零业务代码改动**：本回执阶段**未改任何文件**（仅取号 commit `1e4f70d` 改账本水位线行）。
- **未进局 / 未跑局**：未触发 `EnterTestRun`。
- **白名单**：施工仅动 A 段 3 cs（`BlacksmithBuilding.cs`／`SiegeWorkshopBuilding.cs`／`MineByproductComponent.cs`）；**不动 `Building.cs`**。
- **写-改-commit 同串·只提本批文件·不 push**；改前必重读磁盘（`agent-handoff §六`）。
- **`_任务队列.md` 不写**（CRLF-blob·队列只读）。
- **口径**：本回执断言口径来源＝磁盘实读 `file:line`/资产直读；排除项＝未跑局（纯静态）。
- **教训**：`L-30`（全链门 gap／硬条件可达性）／`L-31`（同源判据禁另造）／`L-26`（清残判据禁纯关键词）／`L-34`／`L-35`／`L-24`/`L-25`（判据三直读）。

---

## 八、停手待裁

- 本回执落盘后**停手**，等策划端确认下列三点后施工：
  1. **§四 只读预判门结论（PASS）是否照准**（尤其 4.4 残余不确定性的处置：接受"常态成立、峰值待实证" ⇒ 直接施工；或授权新增只读探针先实证峰值）。
  2. **§三 第 4 环改判文案**（「🔴 结构性不可闭（待派工链补全后重评）」＋§3.1 闭环证据链）是否照准。
  3. **§五 施工口径 1~5 复述**是否与 D704 §三-3 逐条一致。
- 确认后本端即施工 → 线 1~3 探针（正门）→ 后段门 → 承接账清残 → 交付报告（按实时水位线取号）。

---

> 执行端（TraeCode）｜2026-09-13｜本回执＝A 批开工前置（含 D704 施工前只读预判门）。
