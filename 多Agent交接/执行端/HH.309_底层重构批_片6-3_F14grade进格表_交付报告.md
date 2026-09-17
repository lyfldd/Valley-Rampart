# HH.309 底层重构批 · 片 6-3「F-14 · grade 进格表」交付报告

> **端**：执行端｜**日期**：2026-09-17｜**批**：`HH.294` 片 6-3（`D781` 派工｜口径源 `03_地图即数据库` §6.9）
> **范围**：格表 `grade`（存储）＋ 生成分配 pass ＋ 门读口 ＋ 回收/重生语义 ＋ 搭车 `C1`/`C3`
> ⛔ **不含**：消费端（产量/HP 乘 `gradeScale`）·`C2`（策划端文档）·`F-15`（独立批）
> **证据（完整路径）**：
> ① 探针报告（终态·步骤 11 落点）：`Valley Rampart/Logs/hh294_slice6_3/`（稳定名＋时间戳副本＋hash）
> ② 证据副本（仓库根）：`Logs/hh294_slice6_3/`
>   · `probe_run1_full_step11_222149.txt`／`probe_run2_compare_step11_222340.txt`／`grades_hash_step11_222149.txt`（终态两段）
>   · `probe_flip_terminal_2227.txt`／`probe_restore_terminal_2229.txt`（终态鉴别力 取反/复原）
>   · `probe_run1_full_220856.txt`／`probe_run2_compare_221050.txt`／`grades_hash_step9_220856.txt`（初版落点·备查）
>   · `probe_flip_run_221300.txt`／`probe_restore_run_221451.txt`（初版落点鉴别力·备查）
>   · `chain_audit_before_slice6_3.txt`／`chain_audit_after_slice6_3.txt`（`C3` 对照）
>   · `slice6_1_rerun_full_20260917_214824.txt`／`slice6_1_rerun_seg2_20260917_2150.txt`（`C1`）
> ③ ChainAudit：`Valley Rampart/Logs/ChainAudit/chain_audit_report.txt`（＋时间戳副本）
> ④ 片 6-1 重跑：`Valley Rampart/Logs/hh294_slice6/`
> **正门证据**：探针两段式（`EnterTestRun` 进局；`Time.timeScale=0`→`Save`→`ExitTestRun`→退 Play），每段跑后实测 `isPlaying=False`

---

## 〇、结论

⭐ 判据 **1~15 全给实测读数**，无 FAIL。**落点已按 `D781` 勘正①逐字对齐**（＝步骤 11 `DeriveNaturalBuildings` 之后·管线末尾）。三项对照/自证均**同一 build 内可复现**：
① **未打乱主流程**（最强判据）：`feat+climate` hash **仍 ＝ `9424A5D99D9C3543`**（与片 6-2 基线逐值不变）＋ **终态取反实测**（取反 ⇒ `D38794C0701150D9`／复原 ⇒ 回基线）；
② **grade 确定性**：同 seed 两次建局 ⇒ `grades` 独立 hash **`55E659207940B2DF` 双同**；
③ **A/B 开关**：`OFF ⇒ 全图 0 个非 Normal`／`ON 复原 ⇒ 逐格 diff=0`（代价 0.40 ms / 65536 格）。

**本批落地**：`MapData.grades`（W×H）＋ 生成期后处理 pass（四型掷 15/70/15·独立 rng 流）＋ 门读口（`ReadGradeAt`/`GetGradeAt`/`CellInfo.grade`/`CellFilter.Grade`）＋ 写口语义（唯一写内核/非四型复位/重生重掷）＋ 枚举注释勘正。**搭车**：`C1` 片 6-1 探针重跑（✅ 7／❌ 0）／`C3` ChainAudit 剔注释（**🔴 1→0**·逐条对照**无新增**）。

---

## 一、判据读数（1~15）

| # | 判据 | 实测读数（读自 ①②） | 判 |
|---|---|---|---|
| 1 | `grades` 在场 | 类型 `ResourceGrade[]`｜长度 **65536 = W×H**（Medium 256²）｜null 容忍①夹具（无 grades）`ReadGradeAt(3,3)=Normal`／越界 `=Normal`／`map=null=Normal`｜null 容忍②真图临时置 null：`GetGradeAt(0,0)=Normal`·`TryGetCell.grade=Normal`（引用已成对复原） | ✅ |
| 2 | ⭐ **未打乱主流程** | `feat+climate` hash **`9424A5D99D9C3543` == 6-2 基线**｜三元素（`+spawns`）`0A78DF92C7F30B81`（新口径·双段同值）｜`BuildingRegistry` 总数 **21**（6-2 读数 21 逐值不变）｜存在性反证：四型格 **2792**·其中非 Normal **862 > 0**｜**鉴别力**（终态·§二）：取反 ⇒ `D38794C0701150D9`，复原 ⇒ 回 `9424A5D99D9C3543` | ✅ |
| 3 |  `grade` 确定性＋分布 | 同 seed 两次：`grades` 上次 `55E659207940B2DF` ＝ 本次 `55E659207940B2DF` ✅｜分布（四型格 2792）：**Barren 422＝15.1%／Normal 1930＝69.1%／Rich 440＝15.8%**（对照 15/70/15）｜存在性反证：Rich 样例 `(19,2)WoodPile`·`(143,3)Tree`·`(92,5)OreVein`；Barren 样例 `(16,2)Tree`·`(45,2)Tree`·`(243,2)WoodPile` ⇒ **皆四型格** | ✅ |
| 4 |  非资源格恒 Normal | 机械扫描 **65536** 格：非资源格 **62744**｜其中 `grade != Normal` 的格数 **= 0**（输出片段：`§D 机械扫描全图 65536 格：非资源格（含 Mine/水/山/平原）=62744｜其中 grade != Normal 的格数=0 ⇒ ✅ = 0`） | ✅ |
| 5 |  作用域正确 | `Mine` 格 **1024**｜其中非 Normal **0**（恒 Normal）｜四型逐型：`Tree` 959/非 Normal 313·`OreVein` 438/125·`WoodPile` 698/206·`StonePile` 697/218 ⇒ **逐型皆有等级事实** | ✅ |
| 6 | ⭐ 门读口 | 能力句「一格要的全部事实一次拿完 ⇒ `grade` 在其中」：`TryGetCell(16,2)` ⇒ `feature/climate/walkable/owner/hasOccupant/unitCount/**grade=Barren**`（与同格直读相等）｜`CellFilter.Grade` 矩形(16,0,16,16)：**门** Rich=5／Barren=1／Normal=250 vs **机械扫描（独立列）** Rich=5／Barren=1 ⇒ 两列一致｜越界 `GetGradeAt(-1,-1)`／`GetGradeAt(W,H)`／`ReadGradeAt(map,-5,3)` ⇒ 全 `Normal`（不抛） | ✅ |
| 7 |  回收/重生语义 | 采集：`Tree(Barren)` ⇒ `RemoveResourceNode` ⇒ `feature=Plain／grade=Normal` **复位** ✅（存在性反证：初值非 Normal）｜直调：`PlaceResourceNode(...,Rich)` ⇒ `grade=Rich` ✅｜**真实重生链**：`SettleDay(0)` 计划补量 **3411** 点 ⇒ 分帧 40 帧 ⇒ **新落点 1222** 个·其中非 Normal **359（29.4%≈30%）**｜`Mine` 重生后复扫非 Normal **0**（不受影响） | ✅ |
| 8 | ⭐ 写点唯一性 | 生产码 `\.grades\s*\[[^]]*\]\s*=[^=]` 命中 **1** 行（`MapGate.cs:41`·写内核 `WriteGradeRaw`）｜别名后门 `(var|ResourceGrade\[\]) \w+ = map\.grades` 命中 **0**｜另：数组级分配点 2（`WorldManager.cs:175` 建数组／`MapGenRules.cs:1549` 兜底重建·非元素写）｜`features` 写点不变量仍 **1**（`MapGate.cs:33`） | ✅ |
| 9 | 内存（禁上限句） | 逐值：`ResourceGrade` 底层 `Int32`（4 B）⇒ **4×65536 = 262144 B ＝ 0.2500 MiB**（本局 Medium 256²；**Large 384²=147456 格 ⇒ 589824 B ＝ 0.5625 MiB**）｜同口径对照：`features` 4 B×65536=262144 B｜`climateZones` 4 B×65536=262144 B（每列可复算） | ✅ |
| 10 | 数值口径 | 枚举注释**改后原文行**（`GridTypes.cs:149-156`）：`Barren // 贫瘠（×0.7）`／`Normal // 普通（×1.0）`／`Rich // 富有（×1.5）`＋ summary「数值口径**认字段**：`BuildingDef.gradeScale = {0.7, 1.0, 1.5}`」｜「底层不消费 grade」证据：`GetGradeScale` 调用点 5 处（`Building.cs:389`／`BuildingFactory.cs:145`／`ProducerComponent.cs:58`／`BlacksmithBuilding.cs:38`／`SiegeWorkshopBuilding.cs:102`）**全不在本批改动集**（零 diff）⇒ 消费点**未新增** | ✅ |
| 11 | A/B 开关 | 原文行 `MapGenRules.cs:1520`：`public static bool AssignGradesEnabled = true;`｜OFF 段（取反）：重跑 pass ⇒ 全图非 Normal **= 0**｜ON 复原：同 seed 派生 rng 重跑 ⇒ 与副本**逐格 diff=0**；耗时 **0.40 ms / 65536 格**（代价读数） | ✅ |
| 12 |  搭车 `C1` | 片 6-1 探针**正门重跑**（两段·seed 29417）：判定行 **✅ 7 ／ ❌ 0**；关键读数：G0-A「小 y 在前」✅／§C 两用例一致 ✅／§D 无 Collider 单位命中 **19**（宝箱可点 ✅）／§E 建筑 **27 座 Collider=0**／§F `PickAt` 0.0358／0.0120 ms·`PickCellRadius=7`／§G 框选新 **0.0029** vs 旧 0.0072 ms／§H 同 seed hash `C445B25A73022B44` 双同 ✅ | ✅ |
| 13 |  搭车 `C3` | 违例**改前→改后**：`R3=5 R4=10 R5=6→5 R6=0`｜**🔴 1→0**（`R5.注册注销不成对`：`GameEvents.cs` 订阅 `BuildingDestroyedEvent ×1／退订 ×0` **消失**）｜ 20→20｜「订阅无退订文件数」**1→0**｜逐条 diff：**只在 before 1 条（即该 🔴）／只在 after 0 条** ⇒ **修正误报·非放宽门禁**（真实订阅调用仍逐条计入；字符串/字面量内 `//` 不误剔） | ✅ |
| 14 | 常规 | 编译 **0 error**（console 唯一 error＝既有环境噪声「240 node options…」·非 CS）｜**0 新增 warning**：本次编译共 16 条 warning，**逐条对应文件全部不在本批改动集**（`GroundEffectManager`/`IUIPanel`/`ChestManager`/`ToastManager`/`BuildingMenuPanel`/`ProjectileManager`/`FormationPanel`/`NPCBrain`/`CameraSetup`/`PathfindingScheduler`/`VisionSystem`）⇒ 结构性论证：本批 diff 未触碰这些行（⚠️ 改前 warning 快照缺失·见 `O5`）｜同 seed 逐格一致（判据 2/3）｜`AI.Core` **零触**（`git diff --name-only` 无 `Systems/AI` 路径）｜行尾：9 文件**全 LF**（0 CRLF / 0 CR-only）｜正门三态：`EnterTestRun`（报告头）＋ `Time.timeScale=0`→`Save`→`ExitTestRun`→退 Play（探针 Finish 段）＋ 每段跑后实测 `isPlaying=False` | ✅ |
| 15 | 清单＋登记项 | 见 §五（逐文件 `+N/−M`）｜应登记项见 §七 | ✅ |

---

## 二、判据 2 鉴别力自证（终态·取反 ⇒ 变／复原 ⇒ 回）

**被测实现**＝「grade pass 用**独立 rng 流**（seed 派生）＋ 落点＝**步骤 11 之后（管线末尾）**」（＝终态代码）。
**取反**（临时·跑毕复原＝`git diff` 零残留）：把调用**移到步骤 4 `FillFeatures` 之后**并**共用主流程 `rng`**。

| 跑次（终态） | 配置 | `feat+climate` hash | 对照基线 `9424A5D99D9C3543` | 三元素 hash | `grades` hash |
|---|---|---|---|---|---|
| ① 正常（22:21:49 落盘） | 独立 rng·步骤 11 后 | **`9424A5D99D9C3543`** | **✅ 逐值不变** | `0A78DF92C7F30B81` | `55E659207940B2DF` |
| ② **取反**（22:26:22） | 共用主 rng·步骤 4 后 | **`D38794C0701150D9`** | ❌ 已变（整图变样·实证） | `7CCC31E9BE4CA531` | `A485C7670C9C0E88` |
| ③ **复原**（22:28:22） | 独立 rng·步骤 11 后 | **`9424A5D99D9C3543`** | **✅ 回到基线** | `0A78DF92C7F30B81` | `55E659207940B2DF` |

⭐ **附**：初版落点（步骤 9 后）亦做过同形取反/复原（`probe_flip_run_221300`／`probe_restore_run_221451`），读数与终态**逐值相同**（`D38794C0701150D9`）⇒ 两版落点对「共用 rng ⇒ 位移」的响应一致、可复现。

**三件套声明**：① 身份可区分＝两列 hash（`feat+climate`／`+spawns`）＋ `grades` 独立列，均有具名口径；
② **鉴别力声明**＝「若被测实现反向（共用主 rng）⇒ `feat+climate` 列必须变为非基线值」——**已实测**（②）；
③ 对照点有效性＝hash 覆盖全部 65536 格×2 数组（非抽样），且 ②③ 证明该列**可动**且**可回**。

### 2.1 勘正对齐记录（`D781` 勘正①）

- 契约（`f3ecf840`·21:48:43 落盘）精确化：pass 落点＝「**全部特征写入完成之后 ＝ 管线末尾（步骤 11 `DeriveNaturalBuildings` 之后）**」；「排在 6.6 之后」仅下界（其间仍有 features 写步：连通修复 `MapValidator.cs:101`／水域 `Ocean`·`River`／孤立矿清理）。
- 本端初版落点＝步骤 9（水域＋复跑连通）之后——**功能等价**（步骤 10/11 不改 features），但按勘正①**已对齐**到步骤 11 之后（终态）。
- **两版读数逐值一致**：三口径 hash / 分布 / 作用域 / 门读口/复位 均同（唯 §G-3 新落点 1222 个中非 Normal 359，vs 初版 1226/361——**分帧落格的帧时预算（2 ms）波动**所致，非确定性面；生成期确定性由 hash 双同证明）。

---

## 三、搭车 `C1`：片 6-1 探针重跑（清场后回归）

- **跑法**：两段式（第 1 段全量／第 2 段同 seed 对比局），各走正门 `EnterTestRun`；seed 29417。
- **判定行统计**：**✅ 7 ／ ❌ 0**（G0-A 符号·§C 两例·§D 宝箱·§E 两处 Collider·§H 同 seed）。
- **回归面读数**：`PickCellRadius` **7**（动态）／拾取命中 **19/39**（无 Collider 单位可点）／建筑 Collider **0/27 座**／框选耗时 **−60%**（0.0029 vs 0.0072 ms）。
- 旧 hash 文件已留档（`Logs/hh294_slice6_3/hh294_slice6_hash_before.txt`·`C445B25A73022B44`）后移除，以触发「全量段」重跑两段。

## 四、搭车 `C3`：ChainAudit 成对扫描**剔注释**

- **根因**（`D780` 验收 `M2` 已定性）：`CollectPairIssues` 全文正则匹配 ⇒ `GameEvents.cs:495` **注释**内反引号包着的 `Subscribe<BuildingDestroyedEvent>` 被计成真订阅。
- **改法**：`CollectPairIssues` 扫描前走 `StripComments`（行注/块注剔除；字符串/逐字串/字符字面量**原样保留**；注释等长空格替换保持行结构；⛔ 未触任何豁免表）。
- **对照读数**：见判据 13；逐条 diff 证明**只消掉那 1 条误报 🔴·未新增任何违例**。
- **口径声明**：本器是静态文本扫描（非 Roslyn），罕见形态（插值串内嵌注释）**只少剔不错剔**（保守：宁多计不漏计）。

---

## 五、改动清单（逐文件 `+N/−M`）＋ 接口调用面（函数级 × 调用面）

**生产码（`_Game`·7 文件 `+133/−5`）＋ Editor（`R5` `+86/−2`）＋ 新探针 `+586` ⇒ 8 文件 `+219/−7` ＋ 新文件 1**

| 文件 | +N/−M | 内容 |
|---|---|---|
| `_Game/Systems/World/WorldState.cs` | **+3/−0** | `MapData.grades` 字段（契约注释） |
| `_Game/Data/MapGenRulesConfig.cs` | **+5/−0** | `gradeWeights {0.15,0.70,0.15}`（枚举序） |
| `_Game/Systems/World/MapGenRules.cs` | **+42/−0** | `AssignGradesEnabled`／`RollGrade`／`AssignResourceGrades`（步骤 12 段） |
| `_Game/Systems/World/MapGate.cs` | **+63/−1** | `WriteGradeRaw`（唯一写内核）／`GenesisWriteGrade`／`ReadGradeAt`／`GetGradeAt`×2／`IsGradeFeature`／`CellInfo.grade`／`CellFilter.Grade`／`SetFeature` 复位／`PlaceResourceNode` 重掷值／`RemoveResourceNode` 复位 |
| `_Game/Systems/World/WorldManager.cs` | **+9/−0** | 建数组＋显式填 `Normal`（走内核）＋**步骤 11 之后** pass 调用（独立 rng） |
| `_Game/Systems/World/ResourceRespawnSystem.cs` | **+7/−1** | `_gradeRng` 独立流＋`PlaceOne` 传重掷值 |
| `_Game/Systems/Grid/GridTypes.cs` | **+4/−3** | `ResourceGrade` 枚举注释勘正（×0.7/×1.0/×1.5·认字段） |
| `Editor/ChainAudit/Validators/R5_SixStage.cs` | **+86/−2** | `StripComments` ＋ `CollectPairIssues` 剔注释（`C3`） |
| `Editor/Smoke/Valley_HH294_Slice6_3Probe.cs`（新） | **+586** | 片 6-3 探针（判据 1/2/3/4/5/6/7/11 ＋ 两段式 hash） |

**接口调用面（函数级 × 调用面；0 调用 ⇒ 明写「备而未用」）**：

| 接口（函数级） | 生产码调用面 | 探针调用面 |
|---|---|---|
| `MapGate.WriteGradeRaw`（private·唯一写内核） | 门内 4 处（`GenesisWriteGrade`/`SetFeature`/`PlaceResourceNode`/`RemoveResourceNode`） | 0 |
| `MapGate.GenesisWriteGrade` | 2（`WorldManager.cs:182` 填 Normal·`MapGenRules.cs:1554` pass） | 1（§H 兜底回写） |
| `MapGate.ReadGradeAt` / `GetGradeAt`×2 | **0（备而未用·预建读口）** | §A/§F |
| `MapGate.IsGradeFeature` | 3（`MapGenRules.cs:1553`·`SetFeature`·`PlaceResourceNode`） | §B/§C/§D/§E/§G |
| `MapGate.CellInfo.grade` | **0（备而未用）** | §A/§F |
| `MapGate.CellFilter.Grade` | **0（备而未用）** | §F（3 次） |
| `MapGenRules.RollGrade` | 2（`AssignResourceGrades`·`ResourceRespawnSystem.cs:388`） | 0（间接） |
| `MapGenRules.AssignResourceGrades` | 1（`WorldManager.cs:211`） | 3（§H OFF/ON/复原） |
| `MapGenRules.AssignGradesEnabled` | 1（pass 内读） | 1（§H 取反） |

**代价读数**（红线⑦第二栏）：全图 pass **0.39~0.40 ms / 65536 格**（实测·Medium）；内存 **+262144 B**（本局）／**+589824 B**（Large）。

---

## 六、未完成项 / 偏离 / 观察项（显式）

| 号 | 项 | 性质 |
|---|---|---|
| `U1` | **消费端不接**（产量/HP 乘格表 grade） | **按设计·不在本片**（属经济/中层）；底层只保「事实可读」 |
| `U2` | `C2`（生命周期登记表 §0/§1 生成侧锚点过时） | **策划端文档**（本端未碰） |
| `U3` | `F-15`（9 座 footprint 重叠） | 独立待修批（本批未触） |
| `U4` | `O3`（`PrioritizeHarvestCommand.Workers` 未消费） | 留 `05` 交互层（本批未触） |
| `O1` | `ReplaceFeature` **未含 grade 口径**（生产码 0 调用·备而未用） | **观察项**：若未来启用「存在替换」涉及四型，须同步补 grade 语义 |
| `O2` | `AssignResourceGrades` 的「长度不符重建数组」分支 | 未在实盘触发（长度恒相等）·防御性代码·如实列报 |
| `O5` | **改前 warning 快照缺失** | 本批首次读 warning 时才取读数（16 条既有）；「0 新增」用**结构性论证**（逐条文件不在改动集）——下次批次应**开片即取**warning 基线 |
| `O6` | 探针首版 §H 位于 §G 之后（重生已改 features ⇒ 同 seed 复跑必然错位） | **已修正**（§H 前移·终态版）；首版读数留档 `probe_run1_prevOrder.txt` |
| `O7` | 封盘落点未追溯（`Save(SLOT)` 返回值在 console·Play 退出后未留存） | 证据＝探针 Finish 代码在场＋`isPlaying=False` 实测（与片 6-2 先例一致） |

---

## 七、应登记项（供策划端落账·⛔ 本端未代写账本）

1. **取号**：`HH.309`（本报告；水位线查得 HH 已用至 `HH.308`）。
2. `_当前快照.md`：片 6-3 状态更新（`F-14` 落地）＋ 残余移交。
3. `_任务队列.md`：`HH.294` 片 6-3 行（已交付待验收·含证据路径）。
4. 台账（`测试基线台账.md` §五）：**`F-14` 消项候选**（「富矿/贫矿被硬编码 Normal 掐死」⇒ `D777` 改判「grade 进格表」·本批落地：改判后语义已兑现，落地证据本报告§一）。
5. 台账新发现候选：无新增 F 号（`O1` 为观察项·不立号）。
6. `03` §6.9 已含全部口径（`D781` 立）——本端核过与实现**逐条一致**（存储/作用域/比例/时机/rng/重生/消费端边界；含 `D781` 勘正①落点）。

---

## 八、纪律自检（红线 1~11 ＋ 补充）

| # | 项 | 自检 |
|---|---|---|
| ① | 只做格表 grade＋分配＋读口＋回收重生＋`C1`/`C3` | ✅ 未碰中层（`05`/`06`/`07`/`08`/`#14`）·未碰消费端·未碰 `C2` |
| ② | 不碰并行会话改动·具名 `git add` | ✅ 并行文件（美术/`pixel-forge`/`GameScene`/`Packages`/`3.6`·`3.8` doc/`Valley_HH284_Probe.cs`）零触；commit 用**具名**（禁 `-A`/`-u`/`.`） |
| ③ | 不 push·写-改-commit 同串 | ✅ 三个 commit（生产码／Editor／报告）·未 push |
| ④ | 判据＝实测读数·未完成项显式 | ✅（§一~§六） |
| ⑤ | 「清零／N→0」类断言附输出片段 | ✅ 判据 4/5/8/13 均附片段 |
| ⑥ | 能力句含限定词 ⇒ 核「受限语境外可否调用」 | ✅ `GenesisWriteGrade`「仅生成期使用」——运行期无调用（调用面见 §五）；`PlaceResourceNode` 的 grade 参数运行期唯一消费＝`ResourceRespawnSystem.cs:388` |
| ⑦ | 新接口 ⇒ 正确性＋代价两栏＋调用面 | ✅ §五（代价 0.39~0.40 ms／0 调用者明写「备而未用」） |
| ⑧ | `file:line`／常数／参数值**直读原处** | ✅ 全部 `git grep` 当场取；数值读定义处（`BuildingDef.cs:94` `{0.7,1.0,1.5}`／`MapSizeConfig.asset` 128/256/384 实读） |
|  | 对照类判据三件套 | ✅ §二（身份可区分/鉴别力声明+终态实测/对照点有效性） |
| ⑩ | 退役/替换/新增事实 ⇒ 保留 A/B 开关 | ✅ `AssignGradesEnabled`（默认 true·OFF 读数同 build 实测） |
| ⑪ | 能力句＋存在性反证·禁上限句·禁只给一列 | ✅ 存在性反证 4 处（判据 2/3/7/11）；内存逐值（判据 9） |
| 补充 | 确定性（含 `grades`）／编译 0 error／正门三态／`AI.Core` 零触／行尾／不代写账本 | ✅ 判据 14（warning 见 `O5`） |
| 补充 | 落点逐字对齐 `D781` 勘正①（步骤 11 之后） | ✅ §2.1（两版读数逐值一致） |