# HH.220｜军事期门槛映射层治本批 · 进度同步（会话收工前落盘）

> 类型：**进度同步（半程）** · 状态：🟡**未完工**（用户指令：先保存，明日续）
> 执行端（TraeCode·Unity 轨）· 2026-09-11 夜 · 锚点：**HH.215 任务书（D663）**｜**HH.216 开工回执（D664 放行实施）**
> 取号：遵 D640 #10（水位线 → **HH.220**，独立单行 commit `636caed`）

---

## 一、已完成（带证据）

### 1. 实施 3 处（业务：`UtilityScorer.cs`）
| # | 落点 | 内容 |
|---|---|---|
| 1 | `D348Target(...)` | **尾插** `float internalDrive = 0f`；公式＝`floor + ⌈威胁×scale + max(0,drive)⌉ + stageFactor`（威胁面数学不变） |
| 2 | `MilitaryTargetFromThreat(...)` | **尾插** `internalDrive = 0f` 透传（纯探针默认 0 ⇒ 既有 5 参调用零退化） |
| 3 | `MilitaryTarget(k,cfg)` | 传**同源 helper** `InternalDrive(k)`（L-31 禁另抄） |

`internalDriveWeight` **保持 0.1 未动**（数值禁区）；含 D664 语义注记（drive＝阶跃项、权重不敏感、禁据此调权）。

### 2. 观测域（列报）
- `Valley_DiagMilitary.cs`：状态行 **+`drive=` 实读**（只读反射）。
- `Valley2_17_Smoke_5.cs`：**新增 `InternalDriveToGate` 正负例**（drive0.1⇒4≥门 / drive1e-4⇒4 / drive0⇒3<门 / 威胁面回归）。

### 3. 容器级（列报，R10 域内）
`Valley_HH80_Run.cs`：`SEED=64513`（复用 HH.214 定案）／`SLOT="p1_fix1"→"p1_fix1b"`／`CIRCUIT_BREAK_DAY=90`／**新增 `MILITARY_STOP_COUNT=1`**（短局机制自证 ⇒ 见 1 军事实达即收工；**⚠️七考重验批须复原 120/2**）。

### 4. 门禁（全绿）
| 项 | 结果 |
|---|---|
| 编译 | **0 错**（新 DLL 22:01:49/50） |
| `Smoke_2_22P0` | **run1/run2 = 32/0 ×2**（逐行一致，仅 tag/时间差） |
| `Smoke_2_23RP0` | **run1/run2 = 27/0 ×2**（逐行一致） |
| `Valley2_17_Smoke_5` | **ALL PASS**（含新 case：`内源势能接入=OK(…drive0.1=4≥4/drive1e-4=4/drive0=3<门/威胁面回归)`） |
| L-22／L-32 | 每轮退 Play 重进；两次收工均实测 `isPlaying=False` |

### 5. ✅ **正向短局实证成立**（seed 64513，槽 `p1_fix1`，15x，D65 提前收工，26.6 分钟）
- **`[KingdomBrain] k3 剧本阶段 → 军事 (Day 65)`**（**修前同 seed 该行 0 条**）；
- 收工档：`达标收工：≥1 AI 军事期（3）@D65`；
- **`militaryTarget` 修后常态 ＝ 4**（逐国分布：k1 3×22/4×42；k2 3×12/4×52；k3 3×22/4×41/**5×1**；k4 2×17/3×12/4×35）——**修前 HH.214 同 seed 全程 2~3、从未 ≥4**；
- 首达门时点＝**D14**（k2/k4，`drive=0.0500`）；k3 跨门窗：D64 `warrior=3,target=4` → **D65 `stage=Military, warrior=4, target=5`**；
- `drive` 实测 **0.0440~0.0880 >0**（判定依赖实证）。

### 6. 负探针（**半程·待续**）
SO `SituationConfig.asset` `internalDriveWeight` 临时置 **0** ⇒ 槽 `p1_fix1b`：**D1~D50 实读 `drive=0.0000`、`target` 全程 2~3（从未 ≥4＝死滞复现）**；因收工于 D50/90 停止 ⇒ **「无 →军事」整窗腿未跑满（待明日补）**。

### 7. (B)(C) 诊断（**初步取证，正式报告待明日**）
- **(B) 资源链**：`[DiagCapacity] D30` ⇒ **k3 `Stone:prod=1/in=100`** 而 **k1/k2/k4 `Stone:prod=0/in=0`**（Wood 亦 prod=0）；k1 census **③BuildCapacity ＝ 0 次**（从未选建产能）⇒ 石产能缺口不自愈。saves 直读（`p1_fix1.json`，正则统计 58 条建筑）：**k1 ＝ castle/farm/House/mine×1/Warehouse×4/Well/wall×1（无 quarry）** vs **k3 ＝ Barracks×2/quarry×3/TrainingCamp×3/wall×9** ⇒ 对照鲜明（k3 有石产能→建军链通；k1 无→⑦ 无建筑前置）。
- **(C) 焦点霸占**：⑨ asset 实读 `need=WallGap`／**`needA=8`**／`costStone=2、costWood=2`／`buildTargetCap=0`；`WallGap need = clamp01((needA−have)/needA)`（**存量缺口型单调·无失败退避**）＋[KingdomBrain.cs:1193](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1193) 选址失败仅「明日再试」⇒ k1 wall 仅 1 座（have≪8 ⇒ need 长期 ≈0.875~1.0）；census top `BuildWall` k1 **42**/k2 **52**/k4 **52** 天（k3 仅 9）。⇒ 与 HH.189「选址失败 181 次」**同链**（占顶＝need 单调 × 几何特定失败 × 无退避）；**是否「同因」待明日以失败计数/运行数据正式定论（禁预设，D663 明令）**。

## 二、明日待办（按序）

1. **负探针补齐**：SO `internalDriveWeight` 置 0 → 槽 `p1_fix1b` 跑满 90 日 ⇒ 取「`target` 全程 ≤3 ＋ **无 →军事**」整窗证据 → **SO 复原＋git 校验**。
2. **(B)(C) 正式诊断报告**（根因＋`file:line`＋「是否结构性」结论；(C) 明确「同因/异因」）。
3. **交付报告**（按账本实时水位线取号）＋ 回写（账本/索引/工作日志/队列）⇒ 策划端验收 ⇒ 验收成立后 **七考重验（长局 ≥2 AI）**。

## 三、当前状态与环境（明日恢复入口）

| 项 | 状态 |
|---|---|
| Play | **已退**（`isPlaying=False`；L-32 兑现） |
| SO 测试件 | **已复原 `internalDriveWeight: 0.1`**（`git status` 该文件**无 diff**） |
| 容器常量 | `SEED=64513`／`SLOT="p1_fix1b"`／`CIRCUIT_BREAK_DAY=90`／`MILITARY_STOP_COUNT=1`（**七考重验批须复原 120/2**） |
| 未提交改动 | 本批 **4 文件**（`UtilityScorer.cs`／`Valley_DiagMilitary.cs`／`Valley2_17_Smoke_5.cs`／`Valley_HH80_Run.cs`）——随本进度同步**同串 commit**（D640 #9 保护半成品） |
| 证据位置 | `Logs/P1/p1_log_20260911_231630.log`（正向·D65）／`p1_log_20260911_234416.log`（负探针·至 D50）／`hh80_run_status.log`／`smoke_2_22p0_run1\|2.log`／`smoke_2_23rp0_run1\|2.log`；`Saves/p1_fix1_day005~065`×13＋`p1_fix1.json`／`p1_fix1b_day005~050`×10 |
| 红线自检 | 业务改动仅 `UtilityScorer.cs`（3 处·结构接线非调参）；AI.Core 零触碰；未 push；SO/枚举无新增（L-28 N/A） |

---

## 四、会话追加实证与报裁项（用户追问「王国 AI 到底能不能消费一次性资源」引发 · **全部代码级取证**）

> 定位：本节由收工后一次**纯代码/存档取证**产生（**未跑局、未改任何业务代码**），含 **1 项报裁**。承 §二-2（(B)(C) 正式诊断）并入明日交付报告。

### 4.1 一次性资源**已实装且大量生成**（更正我先前口误）
- **资产层**（`Assets/Resources/Buildings/`）：`stone_pile`（石堆，`isConsumable: 1`）｜`ore_vein`（矿脉，`isConsumable: 1` **＋** `isResourceNode: 1`）｜`wood_pile`（木材堆，`isConsumable: 1`）｜`treasure_box`（宝箱，`isConsumable: 1`）｜`tree`（树木，`isResourceNode: 1`）｜`farmland`（农田，`isResourceNode: 1`）｜`mine`（**矿洞**，`isResourceNode: 1` **＋ `isMineByproduct: 1`**，`outputResource: 1`(Stone)，`producer.rate 0.3`）
- **存档层**（`Saves/p1_fix1.json` 全图 defId 计数，实测）：**`stone_pile` 780｜`ore_vein` 1188｜`wood_pile` 644**；`mine` 4｜`quarry` 3｜`farm` 5｜`wall` 12｜`Barracks` 2｜`TrainingCamp` 3；**`tree` 0｜`farmland` 0｜`treasure_box` 0**（→ §4.5 落差）
- **矿洞副产实装**：开局日志 `[MineByproduct] 副产仓就绪（mine）：水晶仓/火油仓/矿石仓 cap=20`
- ⚠️**更正（HH.214 §(B) 口误）**：我引 `ResourceNodeMapping`（`quarry→矿洞`／`farm→农田`，注释「资源点自身不产出，工具建筑激活后才产出」）并表述为「**石材唯一产出路径＝在矿点上盖 quarry**」⇒ **该表述不成立**。现行**石源 ≥4 条**：①石堆（一次性）②矿脉（一次性＋资源点）③矿洞（锚点主产＋副产）④采石场（工具建筑·建在矿洞上）；**木**＝树木/木材堆（**设计上无产能建筑**）；**粮**＝农田节点＋农场（工具建筑）

### 4.2 `Stone:prod=0/in=0` 的**精确口径**（防误读）
- `[DiagCapacity]` 的 `prod` ＝ `KingdomBrain.CountProductionOf`（[KingdomBrain.cs:1313-1320](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/KingdomBrain/KingdomBrain.cs#L1313-L1320)）→ `eco.Production[i].Count` ⇒ **「在产产能建筑数」**，**不是石头总量**；`in` 才是入库
- ⇒ k1 的真命题 ＝ **「无在产采石场」＋「零石入库」**，**≠「世界上没石头」**（石堆 780／矿脉 1188 在册）

### 4.3 🔴 **硬证据：AI 目前无法消费世界一次性资源（能力缺口）**
| 入口 | 调用者实读 | 结论 |
|---|---|---|
| [`Building.StartGather()`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/Building.cs#L856-L866)（摘要原文：「**玩家确认采集**一次性资源点（BuildingPanel 采集按钮调）」） | **全库唯一调用者 ＝ [BuildingPanel.cs:590](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingPanel.cs#L590)**（UI 按钮回调） | **零 AI 调用者** |
| [`ResourceRespawnSystem.ConfirmTreeGather()`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/ResourceRespawnSystem.cs#L104-L121)（摘要原文：「**玩家点击地图树格**（2_13 交互入口）经此调用」） | **全库零调用者**（仅定义处） | **连 UI 都未接**（现仅测试/程序化可触发） |
| [`TreeGatherSource`](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/World/TreeGatherSource.cs#L6-L9)（「懒注册——**仅玩家确认**砍某棵树时才创建」） | 冒烟亦标注「**玩家源**…玩家采集真实载体」（`Smoke_2_23RB.cs:161-166`） | 玩家通道 |
- **AI 可自动消费的仅两条**：①自建建筑产能（`StorageComponent`→搬运闭环 `IHarvestable`/`HarvestCarry`）②**矿洞副产**（`MineByproductComponent` 实现 `ITaskSource` 自动广告搬运，`:158`）
- **对照实证**：k3 `quarry×3`＋`Stone prod=1/in=100`（走"建筑产能＋矿洞副产"这条 AI 能消费的通道）⇒ 通路本身可用；k1/k2/k4 两样都无 ⇒ 石永久 0
- ⇒ **石堆/矿脉/树 对 AI ＝ 看得见、吃不着**；k1 `Stone:prod=0/in=0` 的**主因候选＝该能力缺口**（与「in=0 是否另有采集/入账口径问题」由 §4.6 事件级探针一次闭合）
- **归属**：**设计面新功能缺口**（给 AI 加"确认采集"行动或任务源）⇒ **超本批授权，报裁（不擅动）**，见 §4.7

### 4.4 口径/日志覆盖度（对「我们的日志展现 AI 的选择了吗」的回答）
- **已有**：`[DiagMilitary] Dxx kX census defTotal=26 stageFiltered=… noNeed=… infeasible=… axisFiltered=… top=…`（每日**第一名**＋四类过滤计数）；⑯⑰ **逐行动** `need/feasible/feasibleReachable/score`
- **缺口**：census 只记**第一名**；**逐行动读数只覆盖 ⑯⑰**——③建产能、⑨建墙等**无逐行动行** ⇒ 故「③ 65 天未登顶」**当前无法区分**「被 `stageFiltered/noNeed/infeasible` 拦掉」与「进池但分不够」（D663「禁预设」的落点）
- **已实读旁证**（k1 D30）：⑦招战士 `feasible=False feasibleReachable=True cost 0/0/0/0`（卡**前置建筑**，非资源成本）＋⑰a 建兵营 `cost stone=10` vs k1 `stone=3` ⇒ 与 §4.3 首尾相接

### 4.5 文档↔资产落差（列报，不擅断）
- 「**枯木**」：Assets 全库 grep `deadwood|dead_tree|枯木` ⇒ **零命中**。若文档要求其为独立实体，属**文档↔资产落差**（按 `doc-management` 立账）
- 存档 `tree 0｜farmland 0｜treasure_box 0`：可能**未生成／未入存档／文档命名与资产 id 不一致**（例：树可能并入 `wood_pile`）⇒ 待核

### 4.6 测试方法论修正（**用户批评成立，认账**）
| 问题 | 正确取证件 | 所需窗口 |
|---|---|---|
| AI **有无**采集通道（能力缺口） | **代码级 grep 调用者**（本 §4.3 即纯代码取证）＋**事件级断言**（`Gather` 派发数／`ITaskSource` 注册数／`HarvestCarry` 入账数） | **D1~D3 或纯代码** |
| 门槛机制是否生效（本批 A′） | 夹具／阶段注入 ＋ 逐日 `target` 实读 | D5~D15（夹具≈5 分钟） |
| AI **自主**至军事期（D589 判定线） | 自然长局 | 90~120 日（**唯一必须长跑的**） |
- **认账**：HH.214 把 (B) 资源链与"自主至军事期"**混进同一 90 日窗口**，导致产生"看 `in` 涨不涨"这种别扭的取证件；**能力缺口类问题不该用长跑发现**
- **明日待补探针**（观测域，成本低）：①③⑨ 逐行动读数（接现成机制）②`Gather` 事件计数（派发／注册／入账）③k1 领土内 `stone_pile/ore_vein` 计数（世界实体 × 可达范围对照）

### 4.7 🔴 报裁项（随明日交付报告一并呈）
1. **AI 采集世界一次性资源的能力缺口是否立项**（设计面）——三选：①给 AI 加「确认采集」**行动**（AI 行动表**尾插**，L-28）②给 AI 加**自动任务源**（世界资源点对 AI 自动广告，类比矿洞副产 `MineByproductComponent`）③**暂不改**（把石贫国视为设计取舍）。影响面：AI 决策核（**不在 `AI.Core`**，可直改）＋任务派工面；若触及决策核口径 ⇒ 需走 `sim-sync` 对账
2. **口径分流是否升为项目纪律**（能力缺口→代码/事件级；机制自证→短程/夹具；仅"自主性"→长跑）
3. **「枯木」落差**与 `tree/farmland/treasure_box` 存档 0 的归属（立账／核文档）

---
*执行端 2026-09-11 夜（HH.220）。§一~三 为收工前落盘；§四 为收工后追加（纯代码/存档取证，未跑局、未改业务代码）。明日从 §二-1 起续，§4.6 待补探针与 §4.7 报裁项并入交付报告。*

---

## 五、策划端裁决（D665，2026-09-11，主策划端）

> **报告验收成立**（§四纯代码/存档取证＝高质量硬证据）；**§4.7 三项报裁已裁**；**并给出用户追问的「AI 为何不能与玩家王国底层权力对等」根因认定**。以下结论均经我**独立复核**（非采信转述）。

### 5.1 判据三直读（已过）

- **①设计稿**：`2_17 §3.1`（类别表：经济＝①建住宅②建仓库③建产能④强化采集⑤屯粮）；`2_16 §3.4`（AI 开局 `baseStockpile` 普通＝**木24/石15/粮24**）；`D545`「玩家能做的 AI 都要做」；`D311`（五轴独立线性乘入）；**《全模块覆盖审计_P1文档推导层_2026-09-06》L35**（「采集确认｜一次性资源点交互｜**AI 采集走 TaskScheduler**｜**战略等价——不算缺口**」）。
- **②代码落点（实读）**：`Building.cs:860-866`（`StartGather` 守卫）／`:944`（派发条件 `isConsumable && isBeingGathered`）／**`BuildingPanel.cs:590`＝`StartGather` 全库唯一调用者（玩家 UI）**／**`ResourceRespawnSystem.cs:109`＝`ConfirmTreeGather` 定义·全库 0 调用者**／`TreeGatherSource.cs:12`／`TaskScheduler.cs:554`／`ResourceNodeMapping.cs:12`（`quarry→Mine`）/`:6`（**「木已无产能建筑 2_12，=一次性树」**）／**`UtilityActionConfig.cs:26`（③建产能 `axis=Belligerence`）vs `:24/25/27/28`（①②④⑤ 皆 `Economy`）**／`UtilityScorer.cs:133-142`（`score=need×axis×stageW`）／`KingdomBrain.cs:1264/1283-1289`（⑤三通道 A）。
- **③字段/资产**：`stone_pile/ore_vein/wood_pile` `isConsumable:1`；`tree/farmland/mine` `isResourceNode:1`；存档 **`stone_pile 780｜ore_vein 1188｜wood_pile 644`** vs 全图 `mine 4`。

### 5.2 根因认定：AI 与玩家的权力对等，在「资源第一性」这层就断了

| 层 | 认定 |
|---|---|
| 一·核心 | **世界一次性资源「玩家可采、AI 不可采」**——玩家侧完整（UI 单击／数据格树），**AI 侧零入口/零任务源**（`ITaskSource` 实装清单无面向世界资源点者）⇒ **2612 资源点对 AI 看得见吃不着** |
| 二·替代通道被锁 | **石**＝唯一 AI 产能通道 ③建产能被**错配 `Belligerence` 轴**（和平/经济型 AI 永不建）＋`quarry` 需建**矿洞节点**（全图 `mine 4`）；**木**＝**无产能工具建筑** ⇒ AI 木＝**开局一次性预算 24** |
| 三·元层 | 审计 L35 判「战略等价·不算缺口」**未直读代码**，等价路径**零实装** ⇒ **审计结论失实** |

**⇒ HH.214/HH.216 的 `target/gate` 只是最末端显现；真正的病根在资源地基。**

### 5.3 §4.7 三项裁决（beyond-options）

**① AI 采集能力立项** —— 共同前提「需决定要不要加通道」**不成立**（设计〈审计 L35〉早已裁「AI 走 TaskScheduler」⇒ 本项是**落地题**）。
- **①（给 AI 加"确认采集"行动）＝不采纳**（玩家式单击不适 AI 派工模型；会与建/招抢焦点，重演 ③ 病；背离审计裁定）。
- **②（自动任务源）＝采纳**（＝落地已裁通道；参照 `MineByproductComponent`／`TreeGatherSource` 先例）。
- **③（暂不改）＝不采纳**（＝把权力不对等当设计，撞 `D545`）。
- **裁＝自造 A+（合并）**：**②为主 ＋ ③轴纠正（`Belligerence`→`Economy`）＋ 木产能缺口立账待裁**。

**② 口径分流升为项目纪律＝准**（能力缺口→代码/事件级·短窗或纯代码；机制自证→短程/夹具；仅"自主性"→长跑）——落《设计方法论_生命周期工作流》＋`L-30` 扩维。

**③ 枯木落差 ＋ `tree/farmland/treasure_box` 存档 0＝立账＋核文档**（`doc-management`；`tree/farmland` 若属 **feature** 则不入 building 存档＝非缺失，须核）。

### 5.4 教训入库

- **新立 `L-33`**：「审计/对账结论必须直读代码验证**等价路径已实装**」——"战略等价·不算缺口"型判断，若为"以他路等价"论证，须同时验证**他路在场**（本条＝把"设计应有"当"已有"；L-24/L-25 同族放大）。
- `L-30` 加实例：**证据类型路由**维度。

### 5.5 状态与下一批

- 受理 HH.220 报告；**(B) 资源链根因闭合**至「AI 无采集通道＋③轴错配＋木无产能」。
- **立 HH.221「AI 资源权力对等批」任务书**（D665 签发；含 §0 `L-30` 勾选清单）；新增缺陷台账 **DZ-108~111**。
- **P1 终验收仍挂**（资源地基先补，非再调 `target/gate`）；**HH.220 待续**（负探针补满 90 日＋交付）→ **转 HH.221**。

*裁决：主策划端 2026-09-11（**D665**；0.6 §一百九十四；任务书 `多Agent交接/策划端/HH.221_AI资源权力对等批_任务书.md`）。*
