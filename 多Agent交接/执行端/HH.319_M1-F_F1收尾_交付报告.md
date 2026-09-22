# HH.319 · `M1-F` · `F-1` 收尾批（`D-1` ＋ `D-2` ＋ `U-21`）· 交付报告

- **日期**：2026-09-22 ｜ **端**：执行端（TraeCode）｜ **依据**：`D820` `HH.319_M1-F_D1D2评估_验收裁决.md`（§二 件1~4 ／ §三 判据 ／ §六 裁定）
- **状态**：✅ **判据 1／2／3／4 达成** ＋ ✅ **件1／件2／件3 全落地** ｜ ⚠️ **判据 5 三绿（`M7`／`HH317`／`HH316`）／ `HH315` 未跑 ⇒ 判据 5 未闭合 ⇒ ⛔ 本批不判绿**
- **红线自检**：改动面 **3 生产文件 ＋ 1 探针** ✅ ｜ ⛔ 未动 `NPCBrain.cs`（`4de46407` 现状保持）／`BehaviorExecutor.cs`／`UnitFactory`／`MonsterAI`／`FormationBrain`／`SelectionController`／存档 schema／`O-14`（`GameScene.unity` **未碰、未提交、未丢弃**）／四档账本／美术／`pixel-forge`／`Packages` ✅ ｜ ⛔ 未改 `taskTimeout` 值 ✅ ｜ ⛔ 未新增 SO 字段 ✅ ｜ 行尾**改前已验**（4 文件全 **LF**）✅ ｜ 进局走 `EnterTestRun` 正门 ✅ ｜ 收尾退 Play ✅ ｜ ⭐ **跑局前已归档全部历史日志**（`Logs/archive_preF1shouwei/` · `L-68`；命名带来源前缀可自证 · `L-70`）✅ ｜ 具名 `git add` · ⛔ 未 push · ⛔ 未代提交账本 ✅

---

## 〇 · 开工回执（4 行）

1. **改动面清单**：`KingdomTask.cs`（+`advertiser` 字段）／`Building.cs`（`WaterHaul` 置 `advertiser` ＋ **分支前置**）／`TaskScheduler.cs`（去重键改 `advertiser ?? source` ＋ `AddGatherOverflow` 兜底 ＋ 两处注释勘正）／探针 `Valley_HH319_F1LongRun.cs`（判据 1 两新列 ＋ 判据 2 并发列 ＋ 判据 4 分类型计数）。**净 3 生产文件 ＋ 1 探针**。
2. **行尾核验读数**：`git ls-files --eol` ⇒ `KingdomTask.cs`／`Building.cs`／`TaskScheduler.cs`／探针 **均 `i/lf w/lf`（纯 LF）** ⇒ 全走 `Edit` 直改，零二进制替换。
3. **判据口径确认 ＋ `U-21` 报选**：
   - 判据 1 ＝ **`·farm]` 完整逐秒序列**（⛔ 不得只报峰值）＋ **两新列**（「农场工人 `Working`」＝ `HasWorkerAssigned(农场)` 公开口零反射 ／「`TryConsumeFarmWater` 失败」＝ `!storage.CanTake(Water,2)` **判据式与 `ProducerComponent.cs:124` 同源**、零副作用、⛔ 不依赖日志关键词）；判据 2 ＝ **新增「同农场在途 `WaterHaul` 并发数」列**（按 `HaulWaterArgs.target == 本农场仓` 计）；判据 3 ＝ `采集溢出丢弃：Water` 条数 **3 → 0**；判据 4 ＝ **两侧读数**（派发按类型计数）。
   - ⭐ **`U-21` 报选（三案 · 本端取丙案）**：
     | 案 | 做法 | 论证「不丢」 | 代价／风险 |
     |---|---|---|---|
     | 甲 | `AddGatherOverflow` 的 `default` 补 `case Water ⇒ k.AddResources(...)` | ✅ 不丢（`AddToLedger:217` 的 `default` 分支**本收该资源** ⇒ 入台账 `resources`） | ⚠️ 水进台账但**农场耗水不读台账** ⇒ "存起来但用不上"（语义差） |
     | 乙 | `default` 改走 `WarehouseRegistry.FindNearestAvailable(type, pos, 国)` | ✅ 有接水仓（农场仓）⇒ 回可消耗面；⛔ **无仓时仍丢**（实测溢出发生在 **t38~77**，AI 农场尚未建成 ⇒ 乙**单独不达标**） | 仓满/无仓仍丢 |
     | ⭐ **丙（取）** | **乙 → 甲 逐级兜底**：① 归属国 `Accepts` 有余量的就近仓 ⇒ `Add`；② 余量/无仓 ⇒ 入该国台账 `resources` | ✅ **唯一仍「丢」＝ 国已注销**（上方 `k == null` 分支 · **既有资敌防线语义**，⛔ 保留） | 代码 +18 行；口径演进：**不再打印**「采集溢出丢弃」，改打「溢出转存」 |
4. **红线确认**：⛔ 未动 `NPCBrain.cs`（让位门／豁免／`ArrivedAtFocus` 冻结 `4de46407` 现状）／`BehaviorExecutor.cs`／`UnitFactory`／`MonsterAI`／`FormationBrain`／`SelectionController`／存档 schema／`O-14`／四档账本／美术／`pixel-forge`／`GameScene`／`Packages`；⛔ 未改 `taskTimeout` 值；⛔ 未新增 SO 字段；⛔ 不 push；⛔ 不代提交账本。

### ⭐ 裁决要求「开工前必须报一句」：`KingdomTask` **无任何序列化引用**（逐面实读）

| 面 | 实读结论 |
|---|---|
| **类型属性** | `KingdomTask` 是**普通 class**（`KingdomTask.cs:24`）—— ⛔ 无 `[System.Serializable]`、⛔ 非 `MonoBehaviour`／`ScriptableObject` |
| **字段/集合持有面** | `grep`：`KingdomTask <name>[;=]` ⇒ **零「字段声明」命中**（唯一命中全是 **Editor 探针的局部变量**）；`List<KingdomTask>` ⇒ 仅 `TaskScheduler.cs:286 var jobs = …` **局部变量** ＋ 探针局部 ⇒ ⭐ **无任何类以字段/集合元素持有 `KingdomTask`** |
| **存档类面** | 存档类共 14 个（`UnitSaveData`／`KingdomSaveData`／`WorldSaveData`／`KingdomBrainSaveData`／`RanchSaveData`／`TerritorySaveData`／`SiegeProductionSaveData`／`PortalSaveData`／`TimeSaveData`／`RulerSaveData`／`ResourceRespawnSaveData`／`PopulationSaveData`／`DifficultySaveData`／`WorldSystemSaveData`）＋ `SituationSnapshot` ⇒ `grep -i task` 于 `SaveData/Save/Serialize` 面 **零命中**（含 `BuildingSaveData` — 其字段为 `storageContents` 等，**无 task**） |
| **`NPCBrain.currentTask`** | ⭐ **该字段不存在**（`grep currentTask` 仅 3 处**注释**：`KingdomTask.cs:22`／`:51`／`TaskState.cs:4`）⇒ 现况由 `TaskScheduler._npcTaskMap` 承载 ⇒ ⚠️ **登记为注释与码不符**（历史描述） |
| **调度器表** | `_npcTaskMap` 是 `TaskScheduler` 的 `private readonly` **运行时字典**；`TaskScheduler` ⛔ 非 `ISaveable` ⇒ ⛔ 不入档 |

⇒ ⭐ **结论：`advertiser` 字段零序列化面 ⇒ 判据 6 的「逐面证明不入档」成立**（实测亦印证：`HH316 §E`／`HH317 §F` 存档往返逐值一致 `=True`）。

---

## 一 · 件 1 `D-1` 甲案（去重键改「广告者＋类型」）· 落地

| 文件 | 改动 |
|---|---|
| `KingdomTask.cs` | +`public ITaskSource advertiser;` —— ⭐ 约定：**默认 `null` ⇒ 视为等于 `source`**（⛔ 其余 6 类任务无需置值 ⇒ 零行为变化）；仅 `WaterHaul` 置 `= this` |
| `Building.cs:1556` 区 | `task.advertiser = this;`（**唯一置值点** · `D820` §二 件1 钉死） |
| `TaskScheduler.cs:1196-1201` | 谓词改 `var adv = kv.Value.advertiser ?? kv.Value.source;` ⇒ 比 `adv` |
| `TaskScheduler.cs:282-289`（Tick 注释） | ⭐ **勘正**：旧注「去重键＝水井+WaterHaul · 同 tick 内同一水井只服务一个农场（串行）」**与码不符** ⇒ 据实改为「键＝**广告者＋类型** ⇒ **同一农场同时只一个在途搬水**」 |

## 二 · 件 2 `D-2` 乙案（`WaterHaul` 前置）· 落地

- `Building.TryAdvertiseTask` **分支次序**改为：**② 搬水（前置）→ ③ 生产 → ④ 搬运**（方法头注同步勘正 ＋ 演进登记）。
- ⭐ 依据（**契约已裁准**）：`ProducerComponent.TryConsumeFarmWater:118-132` **产粮每次耗 2 点水 · 不足即停产 ＋ 冒「缺水」**（`09` §4.3 D 组）⇒ **水是农场生产前提** ⇒ 缺水先补水。

## 三 · 件 3 `U-21` 丙案（`AddGatherOverflow` 逐级兜底）· 落地

- `TaskScheduler.AddGatherOverflow` 的 `default`：**① 归属国接该资源的就近仓 → ② 余量/无仓 ⇒ 入该国台账 `resources`**；`uc == null` 情形亦入台账（⛔ 不再丢）。
- ⚠️ 口径演进：本分支**不再打印**「采集溢出丢弃」（判据 3 ＝ 该条数 → 0），改打「**溢出转存（`U-21`）**」一行（含落点＝仓名／台账）。

---

## 四 · 判据读数（证据：`Logs/hh319_u16/hh319_u16_15x.log` 2026-09-22 13:23）

### 判据 1 ⭐⭐ 靶例 ＋ `·farm]` 完整逐秒序列 ＋ 两新列 —— ✅ **达成**

**`·farm]` 逐秒序列（全部 7 条 · ⛔ 原文照录 · 非峰值）**
```
f382 t22.14 +0.15s 农场仓水=0 峰值=0 首次见水=未见 ⭐农场工人Working=False ⭐缺水产=True ⭐同农场在途WaterHaul=1
f391 t38.49 +1.17s 农场仓水=0 峰值=0 首次见水=未见 ⭐农场工人Working=False ⭐缺水产=True ⭐同农场在途WaterHaul=1
f428 t54.49 +2.18s 农场仓水=0 峰值=0 首次见水=未见 ⭐农场工人Working=False ⭐缺水产=True ⭐同农场在途WaterHaul=1
f462 t69.64 +3.19s 农场仓水=0 峰值=0 首次见水=未见 ⭐农场工人Working=False ⭐缺水产=True ⭐同农场在途WaterHaul=1
f489 t84.85 +4.21s 农场仓水=10 峰值=10 首次见水=4.21s ⭐农场工人Working=False ⭐缺水产=False ⭐同农场在途WaterHaul=1
f521 t100.08 +5.22s 农场仓水=10 峰值=10 首次见水=4.21s ⭐农场工人Working=False ⭐缺水产=False ⭐同农场在途WaterHaul=1
f554 t113.79 +6.24s 农场仓水=10 峰值=10 首次见水=4.21s ⭐农场工人Working=False ⭐缺水产=False ⭐同农场在途WaterHaul=1
```
- ✅ **端到端成立** ＋ ⭐ **到账时刻 4.21s（基线 29.63s ⇒ 提前 ≈86%）**；`E2 被派 npc34 → E3 到达水井(+1.90s 距井 0.28) → E4 装载 → E5 转 MovingToDest → E6 卸货`。
- ⭐ **两新列鉴别力（本列即 `D-2` 的直接证据）**：`⭐农场工人Working=False` **7/7 秒** ⇒ ⚠️ **靶例农场从未有 `Production` 工人在 `Working`** ⇒ 改前 ② 的 `!HasWorkerAssigned(农场)` 子条件**恒真 ⇒ ② 恒 `return true` ⇒ ④ `WaterHaul` 结构性不可达**；改后前置 ⇒ 直接可达 ✔
- ⭐ 旁证：**探针因 `E2~E6` 全命中而提前收口**（t119 汇总 ⇒ 观测窗仅 ~6 真实秒 vs 基线 45s）⇒ 本身即"改后更快"的旁证。

### 判据 2 ⭐ `D-1` 去重生效 —— ✅ **达成**

```
⭐ 判据 2（`D-1`）· 同农场在途 WaterHaul 并发数分布（按秒）：并发1×7秒
⇒ ⭐ `=1` 为主＝去重生效（>1 的秒数即「该拦没拦」残留）；基线此列不存在（恒不生效 ⇒ 应见 2+ 秒数）
```
- ✅ **100% ＝1，零 2+ 秒数** ⇒ 去重生效（改前键恒不匹配 ⇒ 该拦没拦）；⭐ 该列为**本批新增**（基线无 ⇒ 以码面必然性为基线口径）。

### 判据 3 ⭐ `U-21` 丢弃清零 —— ✅ **达成**

```
采集溢出丢弃：Water 条数 = 0      （基线：本档 3 / reason 档 6 / r1 档 13 / yield_r2 档 70）
溢出转存（`U-21`）：Water×4 ⇒ 仓「Building_farm_107_18」入4（全部入仓）
溢出转存（`U-21`）：Water×8 ⇒ 仓「Building_farm_107_18」入8（全部入仓）
溢出转存（`U-21`）：Water×4 ⇒ 仓「Building_farm_107_18」入4（全部入仓）
```
- ✅ **3 → 0**；⭐ 且**第一层就命中**（水入 **AI 农场仓** ⇒ 不仅"不丢"，还**回到可被农场消耗的面** · 强于甲案）。

### 判据 4 ⭐ `D-2` 补水优先级（两侧读数）—— ✅ **同向达成**（⚠️ 口径限制见下）

```
判据 4（`D-2`）· 派发按类型计数：Transport×137 Production×20 WaterHaul×5
```
| 项 | 基线（45s 窗） | 本跑（提前收口 · ~6 真实秒窗） | ⭐ 归一化（按总量折算） |
|---|---|---|---|
| `WaterHaul` | 7 | 5 | ≈ **38** ⇒ ⭐ **↑ ~5.4×** |
| `Production` | 351 | 20 | ≈ **154** ⇒ ⭐ **↓ ~56%** |
| 总派发 | 1221 | 162 | 折算比 ＝ 162/1221 ≈ 13% |

- ⚠️ **口径限制（如实声明）**：本跑因靶例**提前完成**而窗口远短于基线 ⇒ ⛔ **非严格同窗对照**，上表为**总量折算**，仅作**方向判据**（两侧方向与 `D-2` 预期一致）✔。

### 判据 5 ⭐ 回归 —— ⚠️ **三绿 ／ `HH315` 未跑（未闭合）**

| 项 | 结果 |
|---|---|
| `2_20B_M7`（六轮） | ✅ **总 FAIL=0**（人类 1.8s／精灵 1.0s／矮人 1.4s／兽人 1.1s／矮人·换seed 1.1s／兽人·换seed 1.0s · 全 `ALL PASS`；归档 `Logs/hh319_m7/hh319_m7_20260922_133205.log`） |
| `HH317`（`M1-D`） | ✅ **16 PASS ／ 0 FAIL**（2026-09-22 13:34:25） |
| `HH316`（`U-2` 全段） | ✅ **全绿**（13:38:29）：`§B 链成形 箱余={}` · `§B 判据4=True` · `§C 同 coord 未空箱数=0`（金 80→86 到账）· `§E 存档往返逐值一致=True` · `§F 过期消亡（箱数 3→2 不增）` · `§H AI 工人被派（state None→MovingToSource）` |
| `HH315`（`M1-C`） | ⛔ **未跑**（等待被中止）⇒ ⚠️ **判据 5 未闭合** ⇒ ⛔ **本批不判绿**、请续跑 |

- ⚠️ 无任一**红**（故未触发「停手报裁」）；缺口是"**未取**"而非"出红" ⇒ 如实列报。

### 判据 6 存档往返逐值（本批动了 `KingdomTask` 字段）—— ✅ **达成**

- ⭐ **逐面证明不入档**：见 §〇 附表（类型属性／字段持有面／14 存档类／`currentTask` 不存在／调度器表）⇒ `advertiser` **零序列化面**。
- 实测：`HH316 §E 逐值往返一致=True` ＋ `HH317 §F` 判据11（非空箱 12→12 差异 0）＝ `True`。

### 判据 7 `Abandon` reason 分布（⛔ 不变差）—— ✅ **未变差**（⚠️ 窗口不可对拍）

```
判据 7 · Abandon 按 reason：Timeout×39 Unreachable×83
判据 3 · 分桶：Transport/Unreachable×77 Transport/Timeout×29 Production/Timeout×8 Production/Unreachable×6 WaterHaul/Timeout×2
```
- ⚠️ 本跑窗口 ≈ 基线的 13% ⇒ ⛔ **计数不可与基线（269／684）直接对拍**；⭐ **结构未变差**：仍为 `Unreachable`（≠ 恒困死 `npc`）＋ `Timeout` 两类，且 `Transport/*` 主导 ⇒ 与基线同形 ✔

### 判据 8 ⛔ 不含 `U-17`／`U-18`／`U-19` —— ✅ 遵守

---

## 五 · 未闭合项与观察项（如实列报）

1. ⛔ **判据 5 未闭合**：`HH315`（`M1-C`）**未跑**（等待被中止）⇒ ⭐ **请续跑**（该探针 >10 真实分钟 · 建议单独批次）。
2. ⚠️ **判据 4 的窗口不等长**：本跑靶例提前完成（改后更快）⇒ 用「总量折算」给方向判据；若须严格同窗对照 ⇒ 需探针固定观测窗（另批）。
3. ⚠️ **口径演进（须登记）**：① `AddGatherOverflow` 的 `default` **不再打印**「采集溢出丢弃」⇒ 该关键词**不再可用作丢弃计数**（本批判据 3 以「= 0」为达成 ⇒ 后续若复现丢弃将无此日志；⭐ 新日志为「溢出转存」）；② `D-1` 后**同农场并发恒 ≤1** ⇒ `D-1` 的旧口径读数（若历史有）不可与新读数对拍（历史无此列）。
4. ⚠️ **`D-1` 影响已收敛但未消失**：`WaterHaul` 现在**同农场串行**，但**同一口井仍可同时服务多个农场**（键是广告者 ⇒ 井不作键 · 与旧注释"同井串行"**仍不同**）⇒ ⚠️ 若策划端要求"同井串行" ⇒ 需另立项（本批**未做**，⛔ 不擅自扩面）。
5. ⚠️ 登记 **2 处注释与码不符已勘正**（`TaskScheduler.cs` Tick 去重注 · `Building.TryAdvertiseTask` 方法头注）＋ **1 处待勘**：`NPCBrain.currentTask` 在 `KingdomTask.cs:22`／`TaskState.cs:4` 注释中提及但**字段不存在**（属历史描述）。
6. ⚠️ `O-14`（`GameScene.unity` 两处 `m_IsActive: 1→0`）**维持**：本批 ⛔ 未碰、未提交、未丢弃 ✅。
