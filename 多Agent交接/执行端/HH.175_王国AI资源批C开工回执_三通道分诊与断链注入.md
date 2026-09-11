# HH.175 王国AI P0 资源批C 开工回执（R-C1~R-C5）

> 日期：2026-09-11 · 持有端：执行端（TraeCode·Unity 轨）
> 上游：**D638**（0.6 §一百六十七：批B 验收成立＋列报七裁全认可＋**批C 解锁**）· D633/D634（§一百六十二/一百六十三）
> 任务真源＝`改造计划/2_23_AI王国脑总纲_资源P0实施清单.md` §三 批C（L47-55）＋§四 依赖矩阵＋§六 门禁＋§九 冒烟四件
> 设计稿＝`2_23_AI王国脑总纲_资源第一性架构.md` §2.2（L102-113 三通道）／§3.2（L135-143 常设底线域归属）／§3.3（L145-147 MustHave）／§4.3（L184-191 八格）／§八（L240-248 S-B1）
> 完成报告＝**HH.176**（落盘前读账本实时水位线复核）

## 一、判据三直读（动手前完成，逐项留证）

**① 设计稿全文直读**：§2.2 三通道原文（任一资源断裂触发→诊断层判根因**顺序固定 A→B→C**→多资源按**断供严重度 粮→金→石→木**；通道A 产能→建对应产能建筑／通道B 采集→派工偏向〔批B 生效〕／通道C 仓储→Granary/Warehouse 扩容；**单通道响应不叠加**）＋§3.2 常设底线域归属（一级粮走三通道、断链**不设第五底线级**＝诊断层注入 NeedScore 拉满）＋§3.3（MustHave 日 tick 比对→缺失清单进快照→缺口注入）＋§八 S-B1（诊断块进 AI.Core 镜像 sim 对称、P0a 同级）。

**② 代码 file:line 实读**：
| 锚点 | 实读结论 |
|------|---------|
| `KingdomBrain.ExecuteFocus` L545-600 | ⑤ Grain 路由→`ExecuteBuildFocus`（SO buildingId 通用通道）；②/③/⑨ 同走通用通道 |
| `KingdomBrain.ExecuteBuildFocus` L1160-1198 | 用 `focus` 对应的 `UtilityActionDef.buildingId` 找 def→`PlacementScorer.TryPick`→`BuildController.TryBuild`；**buildingId 静态**＝③恒 quarry 的根因落点 |
| `UtilityScorer.ScoreTop` L110-143 | 行动池循环：`need > 0`→`Feasible`→`axis*stageW`→`score=need×axis×stageW`→argmax |
| `UtilityScorer.NeedScore` L146-176 | ⑤ 屯粮＝`NeedKind.GrainGap`（`clamp01((needA−grainDays)/needA)`）；③建产能＝`NeedKind.CapacityGap`（`clamp01((needA−cap)/needA)`，**cap=CountActiveBuildings 总数不分种类**——R-C2 改造点）；②建仓＝`WarehouseGap`；⑨墙＝`WallGap` |
| `NeedKind` 枚举 L50-70 | HarvestGap=强化采集／GrainGap=屯粮（needA=底线日）|
| `Feasible` L370-443 | 建造类=成本镜像＋**buildTargetCap 上限守卫**（DZ-041 连轴根治）+族门禁+uniquePerKingdom |
| 批A `EconomyBlock`（SituationSnapshot L107） | 五元收支 Flow.Net(r)／产能盘点 Production(r).Count·RateSum·WorkersSum／FarmPerPop／GrainReserveDays／StorageOccupancy／MissingMustHave（RequiredKind 0=Farm 1=Warehouse 2=Fortification）——**三通道判据的全部信号已就位** |
| 批B `TaskScheduler` 有效优先级 | 通道B 派工偏向已生效（D638 验收成立） |

**③ 档位/字段直读**：`UtilityActionConfig.asset` 行动表（⑤Grain/③BuildCapacity/②BuildWarehouse/⑨BuildWall need+buildingId+axisWeight+cap）；`KingdomDiagnosisConfig`（grainReserveDaysFloor=2/reserveTargetDaysOther=3/storageOccupancyThreshold=0.9/channelBOutputPerPop=1/capacityPerPeopleBaseline=5）；`MustHaveConfig`（tier3 档）。**既有常设底线序列＝四级**（粮/人口/被攻击窗口/军力底线）＋断链注入＝**无第五级**（§3.2 L143 原文）——施工后 grep 复核无第五级残留。

## 二、编号报备

| 编号 | 用途 | 状态 |
|------|------|------|
| HH.175 | 本开工回执 | 🟡 占用（账本已登；HH.174 在途最大顺延） |
| HH.176 | 批C 完成报告 | 🔵 预留（落盘前读水位线复核） |

## 三、🗂 单写者声明（agent-handoff 并发写纪律）

本会话**只**写以下文件，其余不动：
```
Valley Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs        （改：ExecuteFocus/⑤分诊 + ExecuteBuildFocus 种类化）
Valley Rampart/Assets/_Game/Systems/AI/KingdomBrain/UtilityScorer.cs        （改：③CapacityGap 改读快照 + R-C3 NeedScore 注入；禁改 NeedScore 签名）
Valley Rampart/Assets/_Game/Systems/AI/KingdomBrain/SituationSnapshot.cs    （改：R-C1 分诊输出注记/新字段若需——**倾向零新增**，见 §五列报）
Valley Rampart/Assets/_Game/Data/Kingdoms/KingdomDiagnosisConfig.cs/.asset  （改：R-C1 通道判据参数入 SO——so-data-driven）
Assets/Editor/Smoke/Smoke_2_23RP0.cs（+.meta）                              （新建：R-C4 冒烟容器）
多Agent交接/执行端/HH.175_*.md、HH.176_*.md                                 （本回执/完成报告）
多Agent交接/_编号登记.md · _交接索引.md · 河谷防线_开发计划书.md              （增量回写，禁整文件覆盖）
```
**不改**：`AI.Core/**`（红线上游；R-C5 镜像走跨仓）、`TaskPriorityConfig*`、`TaskScheduler.cs`（批B 已交付，本批只读其偏向）、`EconomyDiagnosis.cs`（批A 只读）、`MustHaveConfig`（批A 只读，本批消费 MissingMustHave）、训练仓任何文件。

## 四、五件任务复述（R-C1~R-C5 逐件口径）

| 编号 | 任务 | 本批落地形态（口径） |
|------|------|--------------------|
| **R-C1** | ⑤屯粮执行改根因**三通道分诊**（扩全资源 D531①） | `ExecuteFocus` 的 Grain 分支：触发断→**动态判根因 A→B→C 固定序**→决定"建什么/不建"（不再恒 Granary）＋多资源按**断供严重度 粮→金→石→木**取首发严重者；判据信号全部读批A EconomyBlock（零新增跨日状态） |
| **R-C2** | ③建产能种类诊断化（不再恒 quarry） | 双面：①`NeedScore.CapacityGap` 改读**快照产能缺口**（A4 承接）——按「产出 r 的产能建筑数/pop」缺口分种类定 need；②`ExecuteBuildFocus` 对 BuildCapacity（⑤通道A 复用）执行时**动态解出缺的是哪种产能**（farm/quarry/mine），替代静态 SO buildingId |
| **R-C3** | 断链缺口注入（MustHave 缺失→对应建造行动 NeedScore 拉满） | `NeedScore` 对②建仓/③建产能/⑨修工事检查 `EconomyBlock.MissingMustHave`（Farm/Warehouse/Fortification 三类）→ 缺失对应行动 need=1.0（拉满）；**不设第五底线级**（注入走评分被焦点自然选中，不动 ExecuteFocus 底线序列） |
| **R-C4** | 新建 `Smoke_2_23RP0` 冒烟容器 | `Assets/Editor/Smoke/Smoke_2_23RP0.cs` **自含容器、禁挂 SmokeApi**（走 TestHarnessApi 正门）：粮三通道正负对照（产能型饿→建 farm非粮仓／采集型饿→偏向生效断言／仓储型→建粮仓）＋派工偏向行为级（缺石世界采石优先级↑＋采石场边工人密度对比）＋断链自愈 E-B4（删 farm→数日内重建）＋全资源负探针（仅粮触发时金/石/木不误触发）＋确定性同 seed 双跑＋既有冒烟零退化 |
| **R-C5** | sim 镜像 S-B1（跨仓协同训练师） | 执行端本批**不开 AI.Core 直改**（红线）：产出「经济诊断块对拍契约」（输入 DTO=EconomyInput 五元口径定义＋公式=缺口率/权重/三通道判据的 sim 镜像规格）作为训练仓侧实现参照＋`15_账本 S-B1` 登记行**属训练仓不代提**（训练师随下笔提交）——本批交付=契约文档＋Unity 侧实现对拍锚（`EconomyDiagnosis` 纯 C# 已是可移植参照） |

## 五、🔶 R-C1 三通道判据最小口径（设计稿未逐字给死，逐项列报＋落地方案，请裁决）

> 原则：全部用批A EconomyBlock 既有信号（零新增跨日状态，保无持久态＋确定性）；新可调数值全部入 SO（so-data-driven）。

| # | 判据缺口（原文未给死） | 本批复述落地方案 | 备选 |
|---|----------------------|-----------------|------|
| 列报1 | **触发条件·金/石/木**：「收支净流量持续为负且水位<基线」——「持续」「基线」未定义 | `Flow.Net(r) < 0`（当日窗口净负，快照"昨日结存"语义=近期为负的单日判定）**且** `Stock(r) < reserveTargetDaysOther × max(1, Flow.Out(r))`（水位<基线=不足 N 日支出，reserveTargetDaysOther=现有 SO 占位 3） | 若须"N 日窗口"判持续＝破无持久态（快照无跨日内存），**不建议**；单日判定挂 P0 调优评估 |
| 列报2 | **触发条件·粮** | 沿既有 `GrainReserveDays < grainReserveDaysFloor`（grainReserveDaysFloor=2，§2.2 原文「粮=底线日告警既有」）——**底线机制不动**（§3.2 红线） | — |
| 列报3 | **多资源同时断裂→按严重度取谁** | `严重度序 = {粮,金,石,木}`（D531① 原文四元）→ 取**序位最前且触发**的唯一资源走它三通道；**铁(Metal)不在 D531① 严重度序**（原文未列）——本批**不参与分诊触发**（铁能力缺口仍走 R-C2/R-C3 被动面），如需扩铁归策划端另裁 | 铁参与=把铁追加序尾（金→石→木→铁）；需裁 |
| 列报4 | **通道A 产能判据**（"对应产能建筑/人口比<基线"） | `对应产能建筑数(pop 产 r 的·EconomyBlock.Production[r].Count) == 0`（**无对应产能建筑**→建对应产能；避免 per 资源基线 SO 膨胀）→ 响应=建 r 产能（farm/quarry/mine…，复用 R-C2 种类解析） | per 资源基线=SO 加 5 字段（capacityPerPeopleBaseline per r）——膨胀，不推荐 |
| 列报5 | **通道B 采集判据**（"有产能但日产出/人口低"） | `Count(r)>0` **且** `Production[r].RateSum/pop < channelBOutputPerPop`（SO 占位 1）——劳力不足→派工偏向批B 即时生效 ⇒ **⑤ 响应=不建（把执行让给偏向层），Bump ok**（当日不空转重试） | 通道B no-op 的 Bump 语义需确认（ok=true=已正确处置 vs ok=false=重试）——**建议 ok=true** |
| 列报6 | **通道C 仓储判据**（"产出正常占用溢出"） | 通道A/B 均不命中 → `StorageOccupancy >= storageOccupancyThreshold`（SO 占位 0.9）⇒ 响应=建 **Warehouse**（五元综合仓；Grain 特例口径=建 Granary 保留——**⑤ 特判：被诊资源=粮 → 建 Granary，非粮 → 建 Warehouse**） | 粮/非粮双仓细节需确认 |
| 列报7 | **A→B→C 固定序＋单通道不叠加** | 命中即止（A→B→C 顺序判，首个命中执行唯一响应）；全部不命中 → ⑤ no-op（`Bump ok`） | — |
| 列报8 | **R-C2 种类解析的"最缺"定义** | 产能缺口排序按 `Production[r].Count/pop` 从低到高取最缺 r（缺=0 优先）→ 对应 def=r 的产能建筑（复用批A `MapProduceToEco` 反查：Food→farm、Stone→quarry、Wood→lumber/工坊、Gold→GoldMine「税务所」、Metal→Blacksmith）——**def 映射表入 SO**（so-data-driven，禁硬编码） | 反查表可在 `KingdomDiagnosisConfig` 扩 per-r buildingId（待裁） |

## 六、依赖达成复述

- `R-C1 ← R-A1~A3 + R-B1` ✅（批A 经济块四真值在场＋批A A′ 台账收口＋批B 偏向已验收 D638）
- `R-C2 ← R-A1` ✅（快照产能盘点在场；2_22 A4 "改读快照"承接=本批从 `CountActiveBuildings` 切到 `EconomyBlock.Production`）
- `R-C3 ← R-A3` ✅（MustHaveConfig＋`EconomyBlock.MissingMustHave` 已在）
- `R-C4 ← 全批` ✅（A/B 已验收；C1~C3 本批）
- `R-C5 ← R-A1` ✅（对拍契约基座）
- §四 依赖矩阵其余行：`R-C1 粮特例判据←常设底线一级粮`（grainReserveDaysFloor 既有，✅）·`2_12 资源链`（✅已收官）·`2_22 P1 咬合条款`（✅已双端落档）——全达成。

## 七、红线逐条自查

| 红线 | 本批兑现 |
|------|---------|
| **AI.Core 零直改** | R-C5 镜像走跨仓（契约文档出入口），`git status AI.Core` 必空（收尾验）；本批改面仅 KingdomBrain/UtilityScorer/KingdomDiagnosisConfig（均 Assembly-CSharp）＋新建 Smoke 容器 |
| **死表保留** | `TaskPriorityConfig`/S/A/B/C 零改动（通道B 响应是**读**批B 偏向，不重写） |
| **确定性** | 分诊纯函数（读快照无随机）；A→B→C 固定序；多资源严重度固定序；so-data-driven 禁硬编码 |
| **so-data-driven** | 新判据参数（reserveTargetDaysOther 复用既有／channelBOutputPerPop 既有／storageOccupancyThreshold 既有）＋ R-C2 def 反查表入 SO（待裁新增）——**尽量零新增 SO 字段**（列报1/4/5/6 全用既有） |
| **常设底线机制不动** | 一级粮触发条件沿既有（列报2）；断链注入不建第五级（评分面注入，`need=1.0` 只影响 ScoreTop argmax，不动 ExecuteFocus 底线分支） |
| **训练仓不代提** | `15_账本`/`factor_registry` 属训练仓（R-C5 S-B1 登记行由训练师随下笔提交） |

## 八、门禁（缺一不可交付）

1. 编译 **0 警 0 错**（基线=既有 15 告警，新增须 0）
2. **`Smoke_2_23RP0` ALL PASS**（含正/负双侧：粮三通道正负对照＋全资源负探针「仅粮触发时金/石/木不误触发」＋派工偏向行为级＋断链自愈 E-B4＋A→B→C 固定序确定）
3. **确定性同 seed 双跑逐字节一致**（run1/run2 归一化比对）
4. **既有冒烟零退化**（`Smoke_2_22P0` run1 复跑=32/0 基线）
5. 交付前 `git diff HEAD` 全量＋逐项在场性 grep 双自查＋`AI.Core` git status 空＋**常设底线序列 grep 无第五级**
6. L-17 正门进局（TestHarnessApi.EnterTestRun）；L-01 消费点逐项列报；L-02 写后必验

## 九、排雷自查回报（L-01/L-17/L-02）

| 排雷 | 本批自查 |
|------|---------|
| **L-01** 字段就位≠消费端生效 | R-C1 分诊的真实消费点＝`ExecuteFocus` Grain 分支（分诊→ExecuteBuildFocus buildingId 动态化→TryBuild 落地）；R-C2 need 消费点＝`ScoreTop` argmax；R-C3 注入消费点＝`NeedScore→ScoreTop`。交付时逐项 grep 到调用处并断言行为翻转 |
| **L-17** 正门进局 | R-C4 容器一律 `TestHarnessApi.EnterTestRun`+`ExitTestRun`（自含，禁挂 SmokeApi） |
| **L-02** 写后必验 | 每处编辑后重编译＋磁盘回读＋git diff 构成清点；文档增量改禁整文件覆盖 |

## 十、停止条件（遇之停手＋开 HH 报策划）

需要改设计文档／与既有裁决冲突／探针 PASS-FAIL 判据本身需裁／列报 1~8 任一需清单外扩面——即停。

---

**待策划端裁决**：§五 列报 1~8（**尤其列报1「持续」单日判定、列报3 铁是否参与、列报5 通道B 的 Bump 语义、列报6 粮/非粮双仓、列报8 def 反查表入 SO**）。裁决后本端即开工（R-C1 分诊 → R-C2 种类化 → R-C3 注入 → R-C4 容器 → 门禁 → 交付报告 HH.176）。

---

## 十一、策划裁决（策划端回写，**D639**，2026-09-11）

> 状态：✅ **开工准（D639）**——列报 1~8 逐项裁；执行端即按裁开工。0.6 §一百六十八。

**判据三直读＝已过**：①**设计稿全文**＝§2.2 三通道（L102-113）＋§3.2 常设底线域归属（L135-144「断链不设第五底线级」）＋§3.3 MustHave（L145-148）＋§4 行动总表③（L156「缺什么产能建什么」）；②**代码 `file:line` 实读**＝`KingdomBrain.ExecuteFocus` L545-600（⑤ Grain→`ExecuteBuildFocus` 共用通道）／`ExecuteBuildFocus` L1159-1197（`UtilityActionDef.buildingId` **静态**＝③恒 quarry 根因）／`UtilityScorer.ScoreTop` L110-143（need→Feasible→`need×axis×stageW`→argmax）／`NeedScore` L146-176（**`CapacityGap` L165 用 `CountActiveBuildings` 总数不分种类**＝R-C2 改造点；`GrainGap` L169／`WarehouseGap` L161）／`EconomyDiagnosis.cs` L89-96（**`ProductionEntry.RateSum`＝Σ `def.producer.rate`「设计产出率」**）；③**档位/字段直读**＝`KingdomDiagnosisConfig` 出厂值逐项实读（`capacityPerPeopleBaseline=5`／`channelBOutputPerPop=1`／`grainReserveDaysFloor=2`／`storageOccupancyThreshold=0.9`／`reserveTargetDaysGrain=7`／`reserveTargetDaysOther=3`／`shortageSeverityOrder=[3,0,1,2,4]`=粮→金→石→木→铁）——**与 §一 申报逐值一致**。

**列报逐裁**：
1. **触发·金/石/木「持续为负且水位<基线」→ 裁 认可**（单日 `Flow.Net(r) < 0` **且** `Stock(r) < reserveTargetDaysOther × max(1, Flow.Out(r))`）；**否决 N 日窗口**（破 §一 L186「无持久态、纯函数聚合」红线——"持续"语义须先解决无持久态，属 P0 调优/后续轮另裁）。单日判定 + 水位基线 AND 已用「水位不足」阻尼单日噪声（防 flapping）；**R-C4 全资源负探针须实证不误触发**。
2. **触发·粮 → 裁 认可**（沿既有 `GrainReserveDays < grainReserveDaysFloor`；§3.2 底线机制不动）。
3. **严重度序 → 裁 认可**（粮→金→石→木＝D531①；取序位最前且触发者走其三通道）。**铁不参与分诊触发**（§2.2 触发枚举仅 粮/金/石/木 ＋ §2.2 L113「心跳四成员」）。**注记**：SO `shortageSeverityOrder` 的**铁尾随项＝预留不生效**，交付时加注释澄清（防后人误读为铁参与）；扩铁须先改设计稿单独立项。
4. **通道A 判据 → 裁 认可**（`Production[r].Count == 0`）——**保守口径**（触发面＝「完全无对应产能」；语义最清晰、零 SO 膨胀）。**附**：设计稿「产能建筑/人口比<基线」的**比率面本批不收**（粮比率基线 `farmPerPeople` 已由 §3.3 MustHave→R-C3 断链注入覆盖）；**复活条件**＝若 R-C4/实战显示 `Count>0` 但产能明显不足仍不治愈 ⇒ P0 调优批补比率面（届时 per-资源基线入 SO）。
5. **通道B 判据 → 🔴改裁**：信号源**改为 `Flow.In(r)/pop < channelBOutputPerPop`**（**实际日产出/人口**，对齐 §2.2 原文「**日产出**/人口低（劳力信号）」）；**否决**原提的 `Production[r].RateSum/pop`——`RateSum` 实为 `def.producer.rate`＝**设计产出率**（`EconomyDiagnosis.cs` L94 实读），属「产能/潜在」面（＝通道A 族），**不反映劳力**，与通道B 语义错位。响应＝不建（把执行让给批B 偏向层）、**`Bump ok=true` 认可**（已正确处置、当日不空转重试）。
6. **通道C 判据 → 裁 认可**（`StorageOccupancy >= storageOccupancyThreshold(0.9)`；粮→Granary、非粮→Warehouse）。**注记**：`StorageOccupancy` 为**全局值**（不分资源），故「建仓种类」按**被诊资源**分流（粮→Granary 否则 Warehouse），语义可接受。
7. **A→B→C 固定序 + 单通道不叠加 → 裁 认可**（命中即止、首个命中执行唯一响应、全不命中→⑤ no-op；＝§2.2 原文）。
8. **R-C2 种类解析「最缺」→ 裁 认可**（`Production[r].Count/pop` 升序取最缺 r → def 反查表**入 SO**）。**附**：反查表 def id 须以实盘在场 def 为准，**交付时逐项 grep 断言**（防 L-01）；③ `needA` 单一阈值语义须随种类化统一（避免悬空）。

**🔴 撞号处置（L-04）**：HH.176 **双占**——执行端本信 §二预留 HH.176＝批C 完成报告，而**训练端策划 `157c2f4` 已落账** HH.176＝F26 开训批次完成报告（训练轨）。按「**先落盘者保留**」⇒ **HH.176 → 训练师·F26 开训批次报告（保留）**；**执行端批C 完成报告顺延取 `HH.177`**（交付时按账本实时水位线复核，**禁跳号/复用**）。

**验收三问（钩子2，前置＝判据三直读已过）**：①**发生**＝列报5 信号源取 `RateSum`（设计产出率）而非 `Flow.In`（实际日产出）——执行端"有产能但产出低"处取近义字段、未回查 `RateSum` 定义（`EconomyDiagnosis.cs` L94）；②**定性＝执行端单次口径选择失误，非策划流程漏洞**（本回执其余六项落地方案均正确、判据三直读齐备）；③**教训核查＝无新增独立教训**——属 **L-01 家族**（消费端/信号源须直读）的**正向应用**（本次由**策划端判据三直读**抓出，非执行端回查），在 L-01 下加实例注记。

**嘉奖**：①判据三直读齐备且带 `file:line`（本批多数落地方案正确）②列报逐项**自带落地方案**非抛选择题③停止条件/红线/门禁/单写者声明齐（并发纪律自觉）。

**边界**＝策划端零代码/零资产动（仅裁决＋落档）。**执行端下串**＝按 D639 口径开工：R-C1 分诊（**列报1/3/4/5/6 口径按裁修正**）→ R-C2 种类化 → R-C3 注入 → R-C4 容器 → 门禁 → **HH.177** 交付报告。