# HH.173 王国AI P0 资源批B 完成报告（派工活权重 R-B1/R-B2/R-B3）

> 类型：交付报告（Gate 收口）
> 状态：✅已验收（D638，2026-09-11 主策划端）
> 日期：2026-09-11 · 持有端：执行端（TraeCode·Unity 轨）
> 上游：D634（0.6 §一百六十三）开工准+三处口径裁 · D633（§一百六十二）批B 解锁
> 任务真源＝`改造计划/2_23_AI王国脑总纲_资源P0实施清单.md` §二（R-B1/R-B2/R-B3，L41-45）
> 设计稿＝`2_23_AI王国脑总纲_资源第一性架构.md` §2.3（L115-126）／§2.2（L102-113）／§七（L232）／§八 S-B4/S-B5（L247-248）
> 开工回执＝HH.172（本会话单写者声明，收尾解除）

## 〇、锚点声明（所依据文档日期/commit）

- **D634 裁决**＝0.6 §一百六十三（2026-09-11 策划端）——三处口径逐字执行（①缺口信号 ②线性公式 ③SO 映射表）。
- **设计稿**＝2_23 §2.3 L115-126／§2.2 L102-113（D529/D531① 定稿）。
- **实施清单**＝2_23 资源P0 清单 §二（批B 三件）＋§六 门禁＋§八 S-B4/S-B5。
- **commit 基线**＝HEAD `f9ce9cf`（批A+A′ 收口）；本批未含批外文件（git diff 见 §三）。
- **账本水位线**＝D635／HH.172（H 区直读 2026-09-11；完成报告顺延取 HH.173，未撞号）。

## 一、三件交付

### R-B1：TaskScheduler 排序键升级（`Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs`，改）

**落点**＝`Tick()` ④排序段（原 L255-256 单段死表 → 现 L269-275 三段式）。

**三段式**＝
```
有效优先级 = 死表值(S/A/B/C，TaskPriorityConfig 零改动)
           × 资源偏向活权重（ResourceBiasConfig SO × 诊断层缺口信号）
→ 距离（下方循环原样，DR-17）
```

**新增私有方法**（纯函数，无随机无时间）：
| 方法 | 职责 |
|------|------|
| `CompareByEffectivePriority(a,b)`（L965） | 有效优先级降序 → 确定性次级键（源坐标 y→x→任务类型）＝严格全序（补-5 加固） |
| `EffectivePriority(task)`（L985） | 死表值×权重；**S 级不乘权重**（保命红线)＋非 S 硬上界 = S−0.001（防配置越界架空死表，补-3） |
| `ResolveBiasWeight(task, out r)`（L1005） | 权重 = `clamp(bias[r]×(1+k×缺口率), minWeight, maxWeight)`（补-2 出厂 k=1/min=1/max=1.25）；玩家源/无映射/无缺口 → 1.0 |
| `ShortageRate(kingdomId, r)`（L1031） | 缺口率 = `clamp01(max(0,−Net(r)) / max(1, Out(r)))`（补-1 口径；读 SituationHub.Economy.Flow） |
| `ResolveTaskBiasResource(task)`（L1053） | 采集/生产活（Gather/Production）＝实参 `GatherTaskArgs.resourceType` 优先，缺参读源建筑产出（须 `producer.kind==Resource` 防 `outputResource` 默认 0 误判母鸡→金）；**Transport 不参与偏向**（§2.2 通道B 语义＝「采集活」，扩面归列报）；其余任务走 SO 兜底表 |
| `MapResourceType(ResourceType)` | 五元映射（非五元 Ore/Crystal/Meat/弹药 → −1） |

### R-B2：`ResourceBiasConfig` SO（新建）

- `Assets/_Game/Data/Kingdoms/ResourceBiasConfig.cs`（CS）
- `Assets/_Game/Resources/Config/ResourceBiasConfig.asset`（资产，`Resources.Load("Config/ResourceBiasConfig")` 实测可载 ✅）

**结构（D519 模式预留 champion 所有权，对齐 BuildingPlacementConfig 先例）**：
- `bias[5]` per 资源偏向权重，**出厂全 1.0 占位**
- `k`/`minWeight`/`maxWeight`（出厂 1.0/1.0/**1.25**——见补-2 跨档安全论证）
- `taskResourceMap[]` **可配任务→资源映射表**（SO 数据驱动，禁硬编码映射）：预置 GoldMine→金、Rancher→粮 启用；WaterCarry/WaterHaul/AmmoReload 显式 disabled（水/弹药非五元）
- `BiasOf(r)`／`ResourceOfTaskType(type)`／`Load()`（缺 asset 回退占位实例=出厂零行为差异）

### R-B3：边界注记（代码级）

- `TaskScheduler.cs` 头部注释增注：**活权重住本类（Unity 侧执行层）零镜像、不进 AI.Core**——R-B3/2_23 §八 S-B4（D525 打分器同型）。
- **跨仓边界**：`15_账本`「Unity 单侧消费」S-B4 注记 + `factor_registry` 草案登记（S-B5）**属训练仓**（独立 git）＋`factor_registry` 为 AGENTS.md 禁改域 ⇒ **执行端不代提**（D634 明文），由训练师随下笔提交。

## 二、门禁（逐条留证）

| 门禁 | 结果 | 证据 |
|------|------|------|
| 编译 0 警 0 错 | ✅ | `refresh_unity(compile=request)` 后 error CS=0；warning 15 条**全为既有基线**（ChestManager/ToastManager/IUIPanel/ResourceRespawnSystem/BuildingMenuPanel/ProjectileManager/NPCBrain/FormationPanel/CameraSetup/VisionSystem/PathfindingScheduler），本批 4 文件零新增告警 |
| 行为级探针正/负双侧 | ✅ | **`Smoke_2_23RB` run1 = PASS 15 / FAIL 0**（`Logs/P1/smoke_2_23rb_run1.log`）——P3 E-B5 缺石采石 eff 2→2.5↑（正）｜P4 资源隔离（采木/采粮不动）｜P5 S 保命（Repair=4 不动）｜P6 玩家源（TreeGatherSource kingdomId=0 → eff=2 死表值）｜P7 跨档保护（maxWeight=100 → 3.999 < 4）｜P8 clamp 上界（==2.5=maxWeight×A）｜P9 排序确定性（打乱输入序序列一致）｜P10 失败路径终止性（economy=null 不异常）｜P11 L-21 计数面翻转（0→6） |
| 同 seed 确定性 | ✅ | **run2 = PASS 15 / FAIL 0**；run1/run2 归一化（tag/时间戳）后 **逐行完全一致**（Compare-Object 零差异）＝同 seed 同信号→同排序实证 |
| 既有冒烟零退化 | ✅ | **`Smoke_2_22P0` run1 = PASS 32 / FAIL 0**（`Logs/P1/smoke_2_22p0_run1.log`，2422B＝与批A 首跑同字节数，10:22:28 新跑覆盖） |
| diff/grep 双自查 | ✅ | 见 §三 |
| 排雷自查回报 | ✅ | 见 §四 |

## 三、diff 面 + 双自查留证

**本批文件面（7 项＝1 改 + 6 新）**：
```
 M  Valley Rampart/Assets/_Game/Systems/AI/TaskScheduling/TaskScheduler.cs   (+145/−2)
??  Valley Rampart/Assets/Editor/Smoke/Smoke_2_23RB.cs (+.meta)
??  Valley Rampart/Assets/_Game/Data/Kingdoms/ResourceBiasConfig.cs (+.meta)
??  Valley Rampart/Assets/_Game/Resources/Config/ResourceBiasConfig.asset (+.meta)
```

**双自查四锚（实测）**：
① **AI.Core 零改动**＝`git status --short -- "Valley Rampart/Assets/_Game/Systems/AI.Core"` **空** ✅
② **R-B1 消费点 grep**＝`_biasConfig` 字段 L64／加载 L76／`jobs.Sort(CompareByEffectivePriority)` L271／`EffectivePriority` L985／`ResolveBiasWeight` L1005／`ShortageRate` L1031／`ResolveTaskBiasResource` L1053——全链在场 ✅
③ **SO 在场**＝`ResourceBiasConfig.cs/.asset`＋双 meta 4 文件实存；`Resources.Load` 探活返回非 null ✅
④ **死表零改动**＝`TaskPriorityConfig.cs/.asset` git status **空** ✅（红线）

## 四、排雷自查回报（L-01/L-02/L-12/L-17/L-20/L-21）

| 排雷 | 本批自查 |
|------|---------|
| **L-01** 字段就位≠消费端生效 | ✅ 活权重的真实消费点＝`EffectivePriority → CompareByEffectivePriority → jobs.Sort`（grep 到调用处）；探针 P11 断言「偏向生效任务数 0→6」＝业务级消费证据（非仅字段在场） |
| **L-02** 写后必验 | ✅ 每处编辑后重编译（error CS=0）＋反射探活（`ResourceBiasConfig.BiasOf`/`ResourceOfTaskType` 返回值对照预期）＋git diff 构成清点；无整文件覆盖共享文档 |
| **L-12** 单线程串行 | ✅ TaskScheduler.cs 改动按「字段→Awake→排序段→方法体→映射收窄」逐处 SearchReplace 串行，每处后编译 |
| **L-17** 正门进局 | ✅ 探针走 `TestHarnessApi.EnterTestRun`+`ExitTestRun`（P1a 日志：`考跑模式 ON 15x`）；未裸跑 GameScene |
| **L-20** 建筑在场两态 | ✅ 本批**不涉建造**（无 TryBuild/竣工两态语义）＝不适用，如实注记；探针任务源用**既有 Active 建筑**（P1e 断言 IsActive） |
| **L-21** 计数面翻转+失败终止 | ✅ P11「偏向生效任务数 0→6」（计数面 0→1 真实翻转）；P10「economy=null → 返回死表值不异常」＋ P2「无快照＝死表值」（失败路径终止性） |

## 五、🔶 列报（请策划端过目/裁决；均为 D634 未逐字给出的最小可执行口径落地，非改裁决）

| # | 列报项 | 本批落地 | 请示 |
|---|--------|---------|------|
| 列报1 | **缺口率定义**（公式中的「缺口率」D634 未给算式） | `clamp01( max(0, −Net(r)) / max(1, Out(r)) )`＝支出未被收入覆盖比例；`Out=0 ⇒ 0`；`In=0 且 Out>0 ⇒ 1.0`（全额缺口） | 认可 / 需调整（给替代算式） |
| 列报2 | **k / min / max 出厂值**（补-2） | `k=1.0`、`min=1.0`、`max=1.25`（出厂保守占位只接结构） | 认可 / 调值（SO 改，零代码） |
| 列报3 | **跨档结构与 S 保命双保险**（补-3/补-4） | ①S 级不乘权重（死表原值）②非 S 硬上界=S−0.001 ③出厂 max=1.25 ⇒ 默认不跨档（A×1.25=3.75<4；B×1.25=2.5<3；C×1.25=1.25<2） | 认可 / 需跨档（调 maxWeight） |
| 列报4 | **Transport 不参与偏向** | §2.2 通道B 语义＝「对应资源**采集活**权重↑」⇒ 搬运（独立工作类，B 档）不参与 | 认可 / 扩面（加 Transport 映射行即生效，SO 化） |
| 列报5 | **任务→资源映射的「实参优先」实现** | 采集/生产活按 `GatherTaskArgs.resourceType`（真值）优先，缺参回退源建筑产出（须 producer.kind==Resource 防默认值 0 误判）；SO 表作兜底 | 认可 / 调整 |
| 列报6 | **R-B3 跨仓边界** | `15_账本` S-B4 注记＋`factor_registry` S-B5 草案登记属**训练仓**，**执行端不代提**（D634 明文），由训练师随下笔提交 | 知悉即可（无裁决点） |
| 列报7 | **确定性排序加固**（补-5） | 排序比较器加确定性次级键（源坐标 y→x→任务类型）＝严格全序，消除 `List.Sort` 不稳＋`_sources`(HashSet) 枚举序不可保证的潜在乱序 | 认可（既有冒烟零退化佐证无行为回归） |

## 六、下一步建议

- 待策划端验收 HH.173 → 批B 收口 → 解锁 **批C（R-C1~R-C5）**（依赖 `R-C1 ← R-B1` 已达成＝偏向信号已可被三通道分诊消费）。
- commit **未执行**（待验收/授权；遵「只提本批文件、禁 `git add -A`、不 push」——本会话单写者解除已生效）。
- 队列「2_23资源P0批B」行状态归策划端更新（队列 Owner=策划端）。

---

## 策划裁决（策划端回写，裁决前保持空白）

| 决策点 | 裁决 | 理由 |
|--------|------|------|
| 批B（R-B1/R-B2/R-B3）验收 | ✅**验收成立（HH.173 销号）** | 判据三直读已过＋独立复核全中（见下 §裁决展开） |
| 列报1：缺口率定义是否认可 | ✅**认可**（附**观察项**） | 算式自洽：`Out=0 ⇒ Net=In−0≥0 ⇒ 提前 return 0`（分母 `max(1,Out)` 双保险）；`In=0 且 Out>0 ⇒ Net=−Out ⇒ rate=1.0`＝全额缺口；语义「支出未被覆盖比例」贴 D634① 流量口径。**观察项**＝出厂 `k=1` × `max=1.25` ⇒ **rate > 0.25 即权重饱和于 1.25**，偏向区分度实际只在 `rate∈(0,0.25]`；不阻塞（出厂本就"只接结构、零占位"），挂 P0 调优批/批C 后评估调 `k` 或 `max`（SO 改，零代码） |
| 列报2：k/min/max 出厂值是否认可 | ✅**认可** | 保守占位、只接结构；`min=1.0 ⇒ 默认只升不降` ⇒ 出厂与既有排序零差异（P2 负探针实证）。调值零代码（SO 可配，D519 模式 champion 所有权已预留） |
| 列报3：跨档结构+S 保命双保险是否认可 | ✅**认可** | **死表值实读核验**（`StimulusTypes.cs` L21-24：`S=4/A=3/B=2/C=1`）⇒ 论证成立：`A×1.25=3.75<4`／`B×1.25=2.5<3`／`C×1.25=1.25<2` ⇒ **默认不跨档**真；`S 级不乘权重`（`baseP >= S → return baseP`）＋`非 S 硬上界 S−0.001`（防 maxWeight 配置过大架空死表）＝**保命双保险认可**（P5/P7 正负探针实证） |
| 列报4：Transport 不参与偏向是否认可 | ✅**认可窄读**（扩面不立项） | `2_23 §2.2` 通道B 语义＝「对应资源**采集活**权重↑」⇒ 搬运（独立工作类、已 B 档）不参与＝**忠于语义**，非静默漏项（已列报）。**扩面不单独立项**——SO 加映射行即生效，属调参域；且批B 扩面会引入行为漂移。⇒ 挂 **R-C4** 冒烟观察面（该条已含「派工偏向行为级」） |
| 列报5：映射「实参优先」实现是否认可 | ✅**认可** | `GatherTaskArgs.resourceType`（真值）优先 → 缺参回退源建筑产出，且**加 `producer.kind == ProduceKind.Resource` 守卫防 `outputResource` 默认值 0 把无产出建筑误判为产金** ⇒ 正是 L-01「字段就位≠生效」的正确姿势；口径对齐批A `EconomyDiagnosis.MapProduceToEco` |
| 列报6：R-B3 跨仓边界（15_账本属训练仓）是否认可 | ✅**认可（知悉）** | 与 D634 明文一致：`15_账本` S-B4 注记＋`factor_registry` S-B5 草案登记属训练仓（独立 git）＋AGENTS.md 禁改域 ⇒ **执行端不代提，训练师随下笔提交** |
| 列报7：确定性排序加固是否认可 | ✅**认可** | 原 `jobs.Sort((a,b) => GetPriority(b).CompareTo(GetPriority(a)))` 对**同优先级**顺序**本就未定义**（`List.Sort` 不稳定 + `_sources` 是 HashSet、枚举序无保证）⇒ 加确定性次级键（源坐标 y→x→任务类型）＝把非确定性**收敛**，**非行为回归**；既有冒烟 `Smoke_2_22P0` 32/0 佐证 |
| 批B 解锁批C | ✅**准予解锁** | 清单 §四 依赖矩阵**直读**：`R-C1 ← R-A1~A3 + R-B1` ✅ 全达成；`R-C2 同窗条件＝2_22 P0 批A（A4）` ✅ 已由 D608 收官；§四 行「R-C1 通道B 响应 ← 批B 活权重」原 **⛔ 批内顺序** ⇒ **解除**。`R-C3←R-A3` ✅／`R-C5←R-A1` ✅ |

### 裁决展开：判据三直读 + 独立复核（零采信报告自述）

**① 设计稿全文**：`2_23 §2.3`（派工活权重 L115-126）／`§2.2`（三通道）／`§七`／`§八 S-B4/S-B5`＋清单 §二（R-B1~R-B3）/§四（依赖矩阵）/§六（门禁）＋D634 裁决（0.6 §一百六十三）逐字读。

**② 代码/资产 `file:line` 实读（本端亲读，非转述）**：
- `TaskScheduler.cs` `git show 772ae25` **逐行 diff 直读**：`CompareByEffectivePriority`／`EffectivePriority`（`if (baseP >= (float)TaskPriority.S) return baseP;`＋`float cap = (float)TaskPriority.S - 0.001f;`）／`ResolveBiasWeight`（`kingdom <= 0 → 1f`；`raw = BiasOf(r) * (1f + k * shortage)`；`clamp(lo,hi)`）／`ShortageRate`（`flow.Net(r)`／`net >= 0 → 0f`／`(-net) / (float)Mathf.Max(1, outAmt)`／`Clamp01`）／`ResolveTaskBiasResource`（Gather/Production 实参优先＋`producer.kind == ProduceKind.Resource` 守卫＋`isBlacksmith → Metal`）／`MapResourceType`（仅五元，非五元 −1）。**逐条与 D634 三口径对上**。
- `ResourceBiasConfig.asset` **全文直读**：`bias: [1,1,1,1,1]`／`k: 1`／`minWeight: 1`／`maxWeight: 1.25`／`taskResourceMap` 5 行（taskType 6→res 0 on｜4→res 3 on｜5/8/10 disabled）——**与申报逐值一致**。
- `ResourceBiasConfig.cs`：`Load()` 缺 asset 回退占位实例（出厂零差异）／`BiasOf` 越界回 1f／`ResourceOfTaskType` 兜底表。
- `StimulusTypes.cs` L21-24：`TaskPriority` 死表值 `S=4/A=3/B=2/C=1`（列报3 论证的核验依据）。

**③ 档位/字段直读**：SO 出厂值＝**字面量直读**（非由行为反推）；死表值＝**枚举字面量直读**。**无任何分数/指标反推**。

**独立复核（零采信）全中**：
| 复核项 | 方法 | 结果 |
|---|---|---|
| commit 构成 | `git show --name-only 772ae25` | **12 文件**（7 代码＝TaskScheduler + Smoke_2_23RB{.cs,.meta} + ResourceBiasConfig{.cs,.meta} + ResourceBiasConfig.asset{.meta}；5 文档＝HH.172/HH.173/索引/账本/主计划书）＝申报一致 |
| **AI.Core 零改动** | `git show --name-only … \| grep -c "AI.Core"` | **0** ✅（红线守） |
| **死表零改动** | `\| grep -c "TaskPriorityConfig\|UnitData.cs\|KingdomTaskType"` | **0** ✅（红线守） |
| **行为级探针实证** | 亲读 `Logs/P1/smoke_2_23rb_run1.log` | **PASS=15 FAIL=0**；P3 E-B5 缺石采石 **eff 2→2.5↑**（**正探针**）｜P2 出厂等价｜P4 资源隔离（采木/粮不动）｜**P5 S 保命 Repair=4 不动**｜P6 玩家源 kingdomId=0 → 2｜**P7 maxWeight=100 时 eff=3.999<4**｜P8 clamp ==2.5｜P9 排序确定性｜P10 失败路径终止｜**P11 计数面 0→6 翻转** |
| **同 seed 确定性** | `sed 去时间戳后 diff run1 run2` | **DIFF=EMPTY（逐行一致）** ✅；run2 亦 PASS=15 FAIL=0 |
| **既有冒烟零退化** | 亲读 `smoke_2_22p0_run1.log` | **PASS=32 FAIL=0**，时间 10:22（**本批之后**新跑）✅ |
| **探针非平凡性**（抽查） | 亲读 `Smoke_2_23RB.cs` | 用**反射调私有方法真值**（`GetMethod("EffectivePriority", NonPublic\|Instance)` + `Invoke`）非桩；P7 **临时改 `maxWeight=100f` 后 `finally` 复原**＝真边界测试 ✅ |
| push 状态 | `git log origin/main..HEAD` | `772ae25` **未 push** ✅（符合"不 push"纪律） |

**勘正两笔（不影响验收）**：
1. **§六「commit 未执行（待验收/授权）」＋§〇「commit 基线＝HEAD `f9ce9cf`」＝过时表述**——实盘 **`772ae25` 已提交**（10:38；报告 mtime 10:36 ⇒ **报告写完后提交**）；§三 的 `M/??` 亦为提交前快照。**构成可核、无失实**，属工序性表述滞后。**建议（非硬性）**：交付报告 §〇 增一行「提交状态」，commit 后补记 commit 号/时间。
2. §〇「基线 HEAD `f9ce9cf`」严格应为「批A/A′ 收口 commit」；批B 实际开工时 HEAD 已是 `98d6455`（D637 补记）⇒ 属措辞，**diff 面判定不受影响**（本批 7 代码文件零批外混入已实证）。

**验收三问（钩子2，前置＝判据三直读已过）**：
1. **为什么会发生**（勘正1）＝执行端工序为「先写报告 → 后提交」，报告因此定格在提交前状态。
2. **单次失误还是策划流程漏洞**＝**单次表述滞后，非策划流程漏洞**（且**未造成失实**：12 文件构成与申报逐项可核）。可固化举措＝报告 §〇 增「提交状态」行（建议，非硬性）。
3. **教训库缺条目还是有条没查**＝**无新增独立教训**；属 **L-02 家族「申报时点错位」变体**（回显/申报 ≠ 磁盘真源），本件**无失实后果**⇒ 不新立条目（防膨胀），在 **L-02 条下加一行正向注记**。

**嘉奖**：①**列报七项全部自带落地方案＋请示口径**（不给选择题，符合 requirement-review 格式）②**L-01 消费端正确姿势**（实参优先＋`producer.kind` 守卫防默认值误判）③**P7/P11 正负双侧探针设计**（临时越界配置测硬上界 + 计数面 0→1 翻转）④**主动窄读并列报**（Transport 不参与，未静默扩面）⑤`f9ce9cf` 基线声明＋diff 面零批外混入。

**边界**＝策划端零代码/零资产动（仅裁决＋落档）。**执行端下串**＝**批C（R-C1~R-C5）开工回执**——取号前**必读 `_编号登记.md` 水位线**（当前 HH.173＝本报告已销号；**HH.174 已被训练轨 F26 报告（HH.174 三键补测）占用**）⇒ 若无新占用则**顺延取 HH.175**，**禁跳号/复用**（L-04）。 |