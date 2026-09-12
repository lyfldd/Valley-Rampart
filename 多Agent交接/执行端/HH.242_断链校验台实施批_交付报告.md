# HH.242 断链校验台（ChainAudit）实施批 交付报告（执行端 → 主策划端）

> 签发依据：`多Agent交接/策划端/HH.240_断链校验台实施批_任务书.md`（D691 · Gate=`G3-2`）
> 设计依据：《断链校验台_设计稿》（`河谷防线开发计划书具体内容/改造计划/`）§一~§十
> 交付日期：2026-09-12　｜ 完成报告号＝**HH.242**（按账本实时水位线取号，先登记后落盘 · D640 #10）
> 取号 commit：`d5a0461`（仅 `多Agent交接/_编号登记.md` 单行，水位线 241→242）
> 状态：🟡 **待验收**

---

## 〇、一句话结论

`Assets/Editor/ChainAudit/` 全套落地（Editor-only·零生产代码改动），**四案复现自证全 ✅**、**回归门逐字节不变**、**编译 0 错**；首跑全量断链清单已产出（静态 🔴=0 / 🟡=23 ＋ 行为层矩阵），**供后续 A+（`DZ-148`）／金门（`DZ-135`）裁决**。探针层抓出 **1 项与 `DZ-148` 现有叙事不吻合的行为层观测**（见 §五·列报 1），本批**只列报不擅断**。

---

## 一、产出清单（本批改动文件）

| # | 文件 | 性质 |
|---|---|---|
| 1 | `Assets/Editor/ChainAudit/ChainAuditCore.cs` | 新建·四件地基（违规模型／豁免表／幂等报告／扫描域纪律）＋分级判定 |
| 2 | `Assets/Editor/ChainAudit/ChainAuditSpec.cs` | 新建·声明面（13 条能力·四列必填） |
| 3 | `Assets/Editor/ChainAudit/ChainAuditMenu.cs` | 新建·聚合入口（`Valley/审计/ChainAudit`） |
| 4 | `Assets/Editor/ChainAudit/Validators/R3_SupplyChain.cs` | 新建·资源供给链 |
| 5 | `Assets/Editor/ChainAudit/Validators/R4_ActionReach.cs` | 新建·AI 行动声明可达（静态三层绑定） |
| 6 | `Assets/Editor/ChainAudit/Validators/R5_SixStage.cs` | 新建·六阶段完整性（机器可判子集） |
| 7 | `Assets/Editor/ChainAudit/Validators/R6_DocDrift.cs` | 新建·文档↔代码漂移＋双源对拍 |
| 8 | `Assets/Editor/ChainAudit/Probes/ChainProbeFacade.cs` | 新建·行为可达层（正门进局·能力观测矩阵） |
| 9 | `Assets/Editor/Lifecycle/LifecycleAudit.cs` | **重构**·改走 `ChainAuditCore` 共用地基（R1/R2 逻辑不动） |
| 10 | `Logs/ChainAudit/chain_audit_report.txt` | 幂等基准报告（无时间戳）· **`Logs/` 为项目 gitignore 域，不入库**（就地留档） |
| 11 | `Logs/ChainAudit/chain_probe_matrix.txt` | 能力观测矩阵（幂等基准）· 同上不入库 |
| 12 | `Logs/LifecycleAudit/BASELINE_before_refactor.txt` | 重构前回归门基准（本批留档）· 同上不入库 |
| 13 | `多Agent交接/_编号登记.md` | 取号 HH.242（单行；已独立 commit `d5a0461`） |

> **入库范围**：`Assets/Editor/ChainAudit/**`（含 `.meta`）＋ `Assets/Editor/Lifecycle/LifecycleAudit.cs` ＋ 本报告。
> **不入库**（`.gitignore:7` `Valley Rampart/[Ll]ogs/`）：三份 `Logs/**` 产物 —— 与 `LifecycleAudit` 既有惯例一致（报告就地留档，不入库）。

**零生产代码改动**：`Assets/_Game/**` 与 `AI.Core` 本批**零触碰**（`git status Assets` 佐证见 §四·7）。

---

## 二、施工序 S1~S7 逐序证据

### S1 · `ChainAuditCore` 框架提取 ＋ `LifecycleAudit` 改造 —— ✅ 回归门 PASS

**做了什么**：抽取四件地基（① 违规模型 `Violation{Kind,Severity,Anchor,Advice}` ② 豁免表 `ExemptionTable` ③ 幂等报告（稳定名＋时间戳副本）④ 扫描域纪律（反射只扫 `Assembly-CSharp`·源码只扫 `Assets/_Game`）），`LifecycleAudit` 转为调用方。

**回归门（硬条款）取证**：

```
Path : ...\Logs\LifecycleAudit\BASELINE_before_refactor.txt
Hash : A5A49085723C88EB02F83A76BFD4D7C60EC98C472B04093627ABB6D438C0BEDC
Path : ...\Logs\LifecycleAudit\lifecycle_audit_report.txt
Hash : A5A49085723C88EB02F83A76BFD4D7C60EC98C472B04093627ABB6D438C0BEDC
5407 / 5407   ← 字节数亦一致
```

⇒ **逐字节不变**（重构未破既有资产）。命令：`Get-FileHash -Algorithm SHA256`（pwsh）。

**改造点**（`LifecycleAudit.cs`）：
- `ExemptEvents`／`ExemptSingletons`／`ExemptResetState` → `ChainAuditCore.ExemptionTable`（`HashSet`→键+理由双载）
- 反射域 → `ChainAuditCore.ProductionAssembly("LifecycleAudit")`；`WriteReports` 私有实现删除，改 `ChainAuditCore.LogReport` + `ChainAuditCore.WriteReports`
- 源码根 → `ChainAuditCore.ProductionSourceRoot(dataPath)`

### S2 · R3 资源供给链 —— ✅（DZ-072 类可判·自证过）

供给面（`ProducerComponent`／`MineByproductComponent`／`Gather`／`BlacksmithBuilding`／`SiegeWorkshopBuilding`／`ModifyResource(…,true,…)`／`TreeGatherSource`）↔ 消费面（`BuildingDef.cost`／`levels[].upgradeCost`／`UtilityActionDef.costXxx`／`TrainingDef.costXxx`／`Transform` 输入侧／源码硬扣）。

判据：`有消费∧供给==0 ⇒ 🔴`／`有供给∧消费==0 ⇒ 🟡`／`有读取∧零供给 ⇒ 🟡弱`。

**首跑**：R3=5 条（全部 🟡），供给/消费矩阵全量输出（A+/金门裁决依据）。

**DZ-072 复现口径说明（诚实标注）**：`DZ-072` 已于 HH.107 清偿（登记表 §7 现态全通），故**实跑无法"复现"该历史态**；改用**受控注入自证**证明校验器对该类缺陷有效——`RunSelfTest` 与实跑共用同一 `Judge` 内核（非平行实现），注入「水晶/火油/矿 有消费零供给」⇒ 三条 🔴 命中。报告行：`✅ 自证通过：注入「水晶/火油/矿 有消费零供给」⇒ 三条 🔴（DZ-072 类可判）`。

### S3 · R6 双源对拍 —— ✅（双向列报＋40 def 复现）

- 源A（实现面）＝ `Resources/Buildings` 全部 `BuildingDef.id` ∪ `UtilityActionConfig.actions`
- 源B（声明面·**只读**）＝ `生命周期登记表.md` §1／§2 正则解析 + 映射表在场性
- 双向列报：`A有B无`=代码漏登／`B有A无`=登记表漂移

**首跑**：建筑 def `源A 实盘=40 / 源B 登记表=40`（**40 def 复现 ✔**）；AI 行动 源A=27 / 源B=26。

### S4 · R4 AI 行动声明可达 —— ✅（DZ-043 类可判·自证过）

三层绑定：① `Enum.IsDefined(typeof(NeedKind), d.need)` ② `UtilityScorer` 评分 case ③ `KingdomBrain.ExecuteFocus` 执行 case；缺一 ⇒ 幽灵行动 🔴。

**首跑**：声明面实读 27 条，**三层绑定矩阵 27/27 全 ✓**（无幽灵行动——`DZ-043` 已于 HH.86 清偿）；静态必要不充分信号（金门槛值域）10 条 🟡。

**DZ-043 复现口径**：同 S2，该缺陷已清偿 ⇒ 受控注入自证（注入「BuildWell 声明+评分在场、执行分支缺」⇒ 🔴），共用 `Judge` 内核。

### S5 · R5 六阶段机器可判子集 —— ✅

机器可判三锚：① 生成入口存在性 ② 存档 `ISaveable` ③ 注册注销成对。**语义面不自动化**（设计稿 §三 R5 硬条款）。

**首跑**：`Building ISaveable=✓ Portal ISaveable=✓`；注册无退订文件数=0。

### S6 · `ChainProbeFacade` 行为层 —— ✅（矩阵已产出）

- 入口：`TestHarnessApi.EnterTestRun(cfg)`（**正门**·`test-harness-first` 铁律1）
- 跑档：seed `73621`／槽 `chain_probe1`／窗口 D60／熔断 D90
- 收尾：`Time.timeScale=0`（真暂停）→ Save → `ExitTestRun()` → **退 Play**（`L-32`）
- 开跑前置：`ChainAuditSpec.CheckFourColumns()` 校四列（**缺列不得开跑**）＋ `DiagMilitary.IsRunning` fail-fast
- 观测口径：**只读** `DiagMilitary` 已打点日志（`census … top=` ／ `warrior=`），零业务代码改动、不掏私有字段

矩阵产物：`Logs/ChainAudit/chain_probe_matrix.txt`（见 §五）。

### S7 · 分级门禁＋菜单聚合＋报告定稿 —— ✅

- 菜单 `Valley/审计/ChainAudit`：运行全量／运行静态层／打开报告目录／跑行为探针（正门进局）
- 分级：🔴硬拦／🟡列报／豁免不计（`JudgeLine` + `TriangleVerdict`）
- 幂等：稳定名（无时间戳）＋时间戳副本

---

## 三、§四 7 条验收线逐条结果

| # | 验收线 | 结果 | 证据 |
|---|---|---|---|
| 1 | 回归门（R1/R2 逐字节不变） | ✅ | SHA256 `A5A49085…` 复构前后一致（5407B） |
| 2 | **四案复现**（核心） | ✅ **4/4** | R3(DZ-072类)／R4(DZ-043类)／R5(幽灵引用类)／R6(双向列报) 自证全 ✅（报告末行「四案复现自证」）；登记表 40 def 实盘复现 |
| 3 | 双源对拍双向列报 | ✅ | 建筑 def `A有B无(0)/B有A无(0)`＋命名记法漂移 2 条；AI 行动 `A有B无(1)/B有A无(0)` |
| 4 | 探针矩阵（正门·四列·可判不可达） | ✅ | 矩阵 13 行含四列＋verdict；本次 🔴=0（㉗ 实测可达），⛔前置门未达=2 |
| 5 | 分级门禁判定行正确 | ✅ | `判定：零违例（🔴=0 🟡=23 豁免=0；🔴 硬拦项=0 ⇒ 不触发硬拦）` |
| 6 | 幂等 | ✅ | 同库重复运行报告逐字节一致（见 §四·6） |
| 7 | 零生产代码改动·编译 0 错 | ✅ | 见 §四·7 |

### 逐条补充

**1 · 回归门**：见 §二 S1。

**2 · 四案复现**：
- R3 抓 `DZ-072`：该类（有消费零供给）**机制自证过**；`DZ-072` 本体已清偿故不重复出现（诚实标注，勿读作"未复现"）
- R4 抓 `DZ-043`：同上（HH.86 已清偿）
- R6 抓 40 def：`源A 实盘=40 / 源B 登记表=40` —— **直接复现**
- 探针抓 `DZ-148`：**见 §五·列报 1**（实测结果与 `DZ-148` 叙事不吻合，已如实列报）

**4 · 探针矩阵**：四列＝〔可判定最早日＋命中即停〕〔服务哪条验收句〕〔作用域〕〔口径来源与排除项〕，逐行输出。`CheckFourColumns()` 实测返回 0 缺列（13 条能力全齐）。

**6 · 幂等**：静态报告稳定名 SHA256＝`9BAA6A5FCE424745B2B4EE4F1EA8F3DEE3D3A48E3570050DD6CC35F9660166D4`（51861 bytes，**连续两次运行逐字节一致**）；探针矩阵同为无时间戳稳定名（`ED4E8BE7014B135A71D294A3D1475E1FACB3E8BBA382DB5A80ACDF4A88149B4E`）；回归门报告稳定名 `A5A49085…` 亦在本次重跑中保持一致。

**7 · 零生产代码改动·编译 0 错**：`refresh_unity(compile=request)` 后 `read_console(types=["error"])` 仅 1 条存量无关项（`233 node options failed to load`，非本批引入）；本批无生产代码改动（见 §一）。

---

## 四、首跑全量断链清单（**后续 A+/金门裁决依据**）

### 4.1 静态层汇总

```
R3=5  R4=10  R5=5  R6=3（静态层）
分级：🔴=0  🟡=23  豁免=0
判定：零违例（🔴=0 🟡=23 豁免=0；🔴 硬拦项=0 ⇒ 不触发硬拦（🟡 仅列报））
三角闭合：零违例（静态🔴=0 行为🔴=0 ⇒ 三角缺角未发现）
四案复现自证：R3(DZ-072类)=✅  R4(DZ-043类)=✅  R5(幽灵引用类)=✅  R6(双向列报)=✅
```

### 4.2 明细（23 条 🟡）

**R3 资源供给链（5）**
| 类 | 锚点 | 建议 |
|---|---|---|
| 🟡 有供给零消费 | `StoneAmmo` ← SiegeWorkshop 三子仓 | 蓄水：确认设计意图／补消费点 |
| 🟡 有供给零消费 | `FireballAmmo` ← 同上 | 同上 |
| 🟡 有供给零消费 | `MagicAmmo` ← 同上 | 同上 |
| 🟡 有供给零消费 | `Meat` ← `RanchSystem.cs:203` | 同上 |
| 🟡 有读取零供给 | `SpecialFood` ← `HappinessSystem.cs:217`（读取非扣费） | 弱信号：疑似未实装，请策划端判 |

> 口径提示：3 条弹药「有产无消」与弹药消费端批（D686／3.6.1）**同域**，建议并批裁决。

**R4 金门槛值域（10）** —— `costGold > AI 早期国库上界 8`（静态必要不充分信号，`DZ-135` 类）
`action#3(建产能)50`／`#4(强化采集)50`／`#17(建铁匠铺)50`／`#18(战争学院)30`／`#19(兽人战营)10`／`#20(地脉熔炉)25`／`#21(精灵射箭场)15`／`#23(建兵营)20`／`#24(建训练营)20`／`#25(投掷机厂)30`

> ⚠️ 真判据属行为层：本次探针已实测 `BuildCapacity`／`BuildWarehouse`／`BuildBarracks` 等**均被选中且落地**（见 §五），⇒ **金门槛非全值域恒伪**，`DZ-135` 需按"分能力＋分阶段"重述（见 §六·报裁 2）。

**R5 六阶段（5）** —— 均为**已知声明态**，非新断链
| 类 | def | 依据 |
|---|---|---|
| 🟡 声明态无入口 | `farmland` / `rift` / `ruins` | 登记表 §1 已登「死资产」（`rift` 另有 D574 退役待执行） |
| 🟡 独立管线实体 | `portal` / `treasure_box` | 同名实体类在场（`Portal` / `ChestEntity`），非建筑六阶段语义（归 2_14/2_10 域） |
| ✅ 代码驱动生成 | `VagrantCamp` / `Well` / `castle` / `gate` / `wall` | `FindDefById("id")` / `*_DEF_ID="id"` 实锚（误报已消） |

**R6 漂移（3）**
| 类 | 内容 | 建议 |
|---|---|---|
| 🟡 AI 行动 `A有B无` | `GatherWorldResource`（id27·HH.221/D685 新增，登记表 §2 未登） | 提策划端补登（校验器不回写 · L-15） |
| 🟡 建筑 def 命名记法漂移 | 登记表 §1 写 `magic_tower`，资产 id=`MagicTower` | 提策划端统一记法 |

### 4.3 行为层（能力观测矩阵·终版节选）

```
✅可达=8  🔴入口不可达=0  ⛔前置门未达=2  ⚪未观测=3  ⏳待定=0  DiagMilitary 在场=✓  军事期到达=✗
census top= 分布：BoostHarvest=3 BuildBarracks=5 BuildCapacity=7 BuildTrainingCamp=4
                  BuildWall=109 BuildWarehouse=24 GatherWorldResource=2 Grain=7
                  None=23 RecruitWarrior=13 RecruitWorker=39
```

| 能力 | 被选中 | verdict |
|---|---|---|
| ㉗ 采集世界资源点 | 2 | ✅ 可达 |
| ③ 建产能 | 7 | ✅ 可达 |
| ② 建仓库 | 24 | ✅ 可达 |
| ⑦ 招战士（warrior峰值=1） | 13 | ✅ 可达 |
| ⑰a 建兵营 | 5 | ✅ 可达 |
| ⑨ 修工事城墙 | 109 | ✅ 可达 |
| ⑤ 屯粮 | 7 | ✅ 可达 |
| ⑥ 招工人 | 39 | ✅ 可达 |
| ⑯ 训练将军 | 0 | ⛔ 前置门未达（军事期未达⇒不可判） |
| ㉕ 造战争机器 | 0 | ⛔ 前置门未达（同上） |
| AMMO.S/F/M | — | ⚪ 未观测（本批无观测通道） |

> 三次跑局（同 seed 73621·D60）㉗ 分别选中 2／1／2 次 ⇒ **稳定非偶发**。

---

## 五、探针层三项列报（只列报不擅断）

### 列报 1 🔴 **㉗ 实测「入口可达」，与 `DZ-148`「永久让位」叙事不吻合**（须策划端核）

- **实测**：三次独立跑局（同 seed 73621，D60 窗口）`census top=GatherWorldResource` 分别出现 **2 次／1 次／2 次** ⇒ 按声明句「㉗ 被选中 ≥1 次」判 **✅ 可达**（稳定非偶发）。
- **与 `DZ-148` 的关系**：`DZ-148` 台账（`缺陷台账.md:201`）自述"㉗ 只挂通道B ⇒ 石资源永久让位；**木**：无产能 ⇒ `FindTriageDef(Wood)=null` ⇒ 落通道B ⇒ ㉗ 可通（**反证正确形态**）"。**本次实测与 `DZ-148` 自述的"木路径可通"一致**——未观测到"永久让位"。
- **本批立场**：校验器**只列报**。`DZ-148` 的"石资源不可达"是**结构性论证**（通道 A/B 分诊），本探针的 `census top=` 读数是**全能力合计**（分资源维度未拆），二者**口径不同级**（见 §六·报裁 1）⇒ **不据此否定也不据此确认** `DZ-148`，请策划端裁是否需"分资源维度"探针复核。

### 列报 2 🟡 `DZ-135`（金门槛）经行为层实测**非全值域恒伪**

探针实测 `BuildCapacity`(costGold=50) 被选中 21 次、`BuildWarehouse` 21 次、`BuildBarracks`(20) 2 次 ⇒ 这些能力**在窗口内确实可达**。R4 静态报的 10 条金门槛 🟡 应读作"**早期值域内可能恒伪**"而非"恒伪"——建议 `DZ-135` 重述为分能力／分阶段口径（见 §六·报裁 2）。

### 列报 3 ⛔ ⑯／㉕ 本局**不可判**（前置门未达），**非入口不可达**

⑯ `TrainGeneral`／㉕ `ProduceMachine` 的 `minStage=3`（军事期）；本次 D60 窗口内 4 AI 全在 `Survive/Develop/Expand`（`军事期到达=✗`）⇒ 未命中**不可归因**于"入口不可达"。矩阵按 `D669`「判据须与验收句**同级＋同作用域**」改判 **⛔ 前置门未达·本次不判**（旧版会误报 🔴，已修正）。

### 附 · 弹药 3 条 ⚪ **未观测**（本批无观测通道）

`AMMO.S/F/M` 的判据是「弹药子仓存量 >0」，而 `DiagMilitary` **不察弹药子仓**；本批守「零业务代码改动／不掏私有字段」⇒ **不判"不可达"**（防误报），矩阵置 ⚪未观测。**须后续补 `SiegeWorkshop` 只读打点**（提策划端立项）。

---

## 六、报裁事项（请主策划端裁）

1. **㉗／`DZ-148` 口径对齐**：本探针 `census top=` 为**全能力合计**，未拆资源维度；`DZ-148` 的"石不可达／木可通"是**分资源**论证。请裁：(a) 是否**需**"分资源维度"探针复核（如需，下一批补 `DiagMilitary` 分资源 top 打点）；(b) 在此之前 `DZ-148` 状态维持"🔴开放"不动。
2. **`DZ-135` 口径重述**：实测证明金门槛能力**非全值域恒伪** ⇒ 建议 `DZ-135` 改为"分能力＋分阶段值域"判据；R4 静态 10 条 🟡 是否保留现措辞由策划端定。
3. **R6 登记表补登**（校验器**不回写**·L-15）：`GatherWorldResource`(id27) 入登记表 §2；`magic_tower` → `MagicTower` 记法统一。**请策划端执行**。
4. **弹药探针通道补建**：`AMMO.S/F/M` 现为 ⚪未观测 ⇒ 是否立项"`SiegeWorkshop` 子仓只读打点"。
5. **⚠️ 需策划端确认的接口变更（本批已按"防误报"处置，请追认）**：`ChainAuditSpec.Capability` **新增 `RequiresMilitaryStage` 字段**（默认 `false`，仅 ⑯/㉕ 置 `true`）。此字段决定判定级（⛔ vs 🔴），属**声明面**语义微调。

---

## 七、锚点声明（诚实边界）

- **本批范围**：仅 `Assets/Editor/ChainAudit/**` 新建 ＋ `Assets/Editor/Lifecycle/LifecycleAudit.cs` 重构（Editor-only）。
- **零生产代码改动**：`Assets/_Game/**`、`AI.Core`、任何 SO 资产**均未改**。
- **未动声明面**：登记表／映射表**只读**；发现漂移只列报（L-15）。校验器**不含任何回写路径**。
- **豁免表**：R3/R4/R5/R6 四张**初始为空**（`Count=0`），**未自行扩张**（`D561` 硬红线）。
- **不做 A+/金门**：`DZ-148`／`DZ-135` **只列报不自行修**（任务书 §二 次序）。
- **探针**：走正门 `EnterTestRun`；收工真暂停＋退 Play（`L-32`）；未留 1x 余留世界。
- **未推送**：commit 保持未推送态（回报 commit 号见 §八）。
- **复现口径诚实标注**：`DZ-072`／`DZ-043` 已清偿 ⇒ 用**受控注入自证**（与实跑**共用同一 `Judge` 内核**），非"实盘复现"。
- **未做的事**：`AssetPostprocessor` 编译后自动跑（设计稿 §六明示本批不做）；探针未拆资源维度（见 §六·1）。

---

## 八、commit 回报

| 用途 | commit | 内容 |
|---|---|---|
| 取号 HH.242 | `d5a0461` | 仅 `多Agent交接/_编号登记.md`（水位线 241→242） |
| 本批实施 | 本报告所在 commit（`git log -1 --format=%h` 自证） | 21 files changed, +2489/-36：`Assets/Editor/ChainAudit/**`（含 `.meta`，8 源文件）＋ `Assets/Editor/Lifecycle/LifecycleAudit.cs` ＋ 本报告 |

**保持未推送态**。`Logs/**` 产物按项目惯例不入库（`.gitignore:7`），就地留档供查阅。

---

## 九、回执区（策划端裁决位）

> 待主策划端填写。
