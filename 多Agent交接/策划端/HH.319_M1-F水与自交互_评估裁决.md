# HH.319 · M1-F「水与自交互」· 只读评估交付验收裁决

- 被验：`多Agent交接/执行端/HH.319_M1-F水与自交互_评估报告.md`（370 行 · commit `053225a8` · 1 文件）
- 本轮性质：**评估验收 ＋ 全裁**（⛔ 未开工 · ⛔ 未 push）
- 判据三直读：①契约原文（`09` §4.3 D 组 `:198-208` ＋ §十五 `:531-593` ＋ `09#44`）②代码 `file:line` ③档位/字段
- 日期：2026-09-21 · 策划端（砚）· 代号 `D807`

---

## 〇 本端独立取证（8 项 · ⛔ 不采信转述）

| # | 取证点 | 实读结论 |
|---|---|---|
| 1 | `Building.TryAdvertiseTask:1505-1552` 水井/农场段 | ✅ `:1512 !producer.IsWell`（水井不派生产）／`:1543 producer.OutputResource == Food`／`:1544 GetStored(kingdomId) < waterThreshold`／⭐ `:1546 new KingdomTask(WaterHaul, this)`（**`this` ＝ 农场自身**）／`:1547 destType = WaterNetwork` |
| 2 | `TaskScheduler:459-529` `Working → MovingToDest` | ✅ **只有三分支**：`Transport`（`:478-492`）／`AmmoReload`（`:493-507`）／`Build && HaulToSiteArgs`（`:508-522`）⇒ ⭐ **`else` ⇒ `Complete`**（`:523-527`）⇒ ⭐⭐ **报告"半假搬运"成立** |
| 3 | `TaskScheduler:1008-1029 ResolveDest` ＋ `:1068-1082 ResolveWaterSource` | ✅ `:1021-1022 case KingdomDestType.WaterNetwork ⇒ ResolveWaterSource`；`:1076` 只判 `def.id == "Well"` ＋ `state == Active` ⇒ ⭐ **本端新发现：不过滤国别**（见 §二-1） |
| 4 | `TaskScheduler:657-664` WaterHaul 完成动作 | ✅ `case KingdomTaskType.WaterHaul` ⇒ `AddWater(waterCarryAmount, kingdomId)` ⇒ 与第 2 项合证：**destPos（水井）从不被走到** |
| 5 | `ProducerComponent:63-95 Tick` | ✅ `:68-75 if (_isWell) { TickWaterToNetwork(...); return; }` **早返回**（⛔ 不判工人）vs `:79 if (!HasWorkerAssigned) return;` ⇒ ⭐ 报告"`_isWell` 第二职责"成立 |
| 6 | `ResourceCatalog.cs:32-54 Table` | ✅ **13 项**（`:39-53`）＋ ⭐ `:35` 注释原文「**D 组（水）标签未入本表 —— 其枚举项尚不存在，落码归 `M1-B`／`M1-F`**」⇒ 报告 §四-`Q1` 第 2/3 项成立 |
| 7 | `Well.asset`／`farm.asset` | ✅ `Well.asset:32-33 warehousePaths: [res_fluid.water]` ＋ `:35-38 producer{kind:0, rate:4, capacity:0}`；`farm.asset:32-33 warehousePaths: [res_food.grain]` ＋ `:35-38 {kind:0, rate:2, capacity:100}` ⇒ ⭐ 报告"水井/农场**已有仓**、缺的只是标签与搬运链"成立 |
| 8 | ⭐⭐ `ResourceCarryConfig.cs:4-11, 22-30` 语义 | ⭐ **注释自陈**：「担当文档所称「ResourceCarryDef SO」：**按资源类型区分工人一次搬运量**」＋「`WorkerTask` 与 `StorageComponent` 经 `Resources.Load` 查本表填充**携带量**」⇒ ⭐ **两消费者、语义＝一次搬运量**（**非"背包容量"**）⇒ **决定 §四-6 的裁法** |

⭐ **附带坐实**：`BuildingDef.cs:58 droppable = true` ＋ `StorageComponent.cs:35 droppable = true` ＋ `Well.asset` **无 `droppable` 行** ⇒ 水井仓**默认可掉** ✓；`GameScene.unity:2065 waterCarryAmount: 10` ✓；`TaskScheduler.cs:46-47` `[Tooltip("WaterHaul 一次搬水量")] public float waterCarryAmount = 10f;` ✓；`TaskPriorityConfig.cs:43 WaterHaul, // 搬水（水井→水网）` ✓；`ResourceBiasConfig.cs:66 WaterHaul ⇒ EcoResource.Food`（`enabled=false`）✓。

---

## 一 报告成立项（复核结论：全部成立）

| 项 | 结论 |
|---|---|
| §二 现状复核（水 6 项 ＋ NPC 自仓 4 项） | ✅ **行号零漂移**，逐条与我独立实读一致 |
| §2.3-① ⭐⭐ **`WaterHaul` 是"半假搬运"** | ✅ **成立**（本端 §〇-2/3/4 三项合证）⇒ ⭐ 本片"行为变更"幅度**从"自动化→人工化"升级为"半假→真"** |
| §2.3-② Editor 补 1 ＋ `R3_SupplyChain:97` 须勘正 | ✅ 成立（`R3` 把水标为"非 `ResourceType` 桶" ⇒ 落枚举后该行**变假**） |
| §2.3-③ 水井/农场仓**既存且已挂** | ✅ 成立（§〇-7 实读坐实） |
| §2.3-④ `_isWell` 第二职责＝免工自产 | ✅ 成立（§〇-5 实读坐实） |
| §2.3-⑤ `Well.droppable` 缺省 `true` | ✅ 成立（§〇 附带坐实） |
| §三-3 **`WorkerInventory` 波及面 30 处**（28 ＋ 补 2） | ✅ 成立；⭐ 其中**本端复核确认"必改"的关键 3 处**：`DamageSystem:640-642`（`carriedType/carriedAmount` 直读）／`:648`（`×1.5` 单资源判据）／`:657`（`inv.carriedAmount = 0` 直写）；另 `UnitController:458-464 GetOrAddInventory`（`:468 SaveState` 亦调用） |
| §三-4 v5 存档代理三案（甲桥／乙弃档／丙改类型） | ✅ **三案分析正确**；⭐ 裁 **甲**（见 §三 `Q8` 补充） |
| §四/§五/§六 | ✅ 建议与代价**逐条可复核**；⚠️ 1 处**改裁**（§四-6） |
| §七 红线自查 | ✅ 报告单文件 commit（`053225a8`）· `Assets/**` 零写 · 未触策划端账本 |

---

## 二 ⭐ 本端补 4 条（任务书/报告未列）

### 1. ⭐⭐ **必做约束：`ResolveWaterSource` 不过滤国别**

```
TaskScheduler.cs:1073-1079
    for (...) { var w = wells[i];
        if (w == null || w.def == null || w.def.id != "Well" || w.state != BuildingState.Active) continue;
        float d = GridMath.DistCells(w.transform.position, task.SourcePos); ... }
```
**⛔ 无国别过滤** —— 与同族解析器 `ResolveWarehouse:1059`（`if (pb != null ? pb.kingdomId != kingdom : kingdom != 0) continue;` **有同国过滤**）**不一致**。

⇒ ⭐ **影响**：现由"半假搬运"（destPos 从不被走到）**掩盖**；`M1-F` 改**真搬运** ⇒ destPos **首次被消费** ⇒ **AI 农场的挑水会解析到玩家水井**（或跨国 AI 井）。
⇒ ⭐ **这是 `HH.86/DZ-044 件2c`（「旧恒入 0 桶＝AI 工人挑水资玩家桶」）的同族缺陷** —— 历史已犯过一次（注释自陈"泄漏面堵法从断供升级为 own 供水"）⇒ **施工必须同批加国别过滤**（并入 `Q6` 的搬水链改造）。
⚠️ **与 `L-45` 同族**：一个被"半假搬运"掩盖的缺陷，在**行为变更**（真搬运）后**立即暴露**。

### 2. ⭐ 注释勘正从 3 处扩到 **5 处**

报告 §四-`Q1` 列 3 处（`KingdomState:50`／`AIDebugSpawnController:430`／`Building:1477`）⇒ 本端实读**补 2 处**：
- ⭐ `TaskPriorityConfig.cs:43` `WaterHaul,  // 搬水（水井→水网）` ⇒ **水网退役后变假** ⇒ 须改「搬水（水井仓 → 农场仓）」
- ⭐ `AIDebugSpawnController.cs:457` `初始水网缺水(<20) → 农场同时派 Production + WaterHaul` ⇒ 同族

### 3. `WorkerInventory.GetCarryCapacity` 的 `Resources.Load` 无缓存

`WorkerInventory.cs:34 var cfg = Resources.Load<ResourceCarryConfig>(...)` —— **每次调用都 Load**；而 `StorageComponent.cs:290/303` 用 `private static ResourceCarryConfig _carryConfig` **有 static 缓存**。
⇒ ⭐ 多资源化重写时**顺带统一**（性能 ＋ 一致性）；⚠️ 与 `§四-6` 的裁法同批处理。

### 4. `DamageSystem:648` 的 `×1.5` 单资源判据 ⇒ **升为必做**

`if (inv.carriedType == ResourceType.Gold)` ＋ `pack = ResourceList.Of(new ResourceAmount(Gold, boosted))` ⇒ ⭐ 多资源化后 `pack` 可能含多条目，而此处**整体替换** `pack` ⇒ **会丢掉非金条目**（比报告的"须复核"更硬：**结构上会丢**）。
⇒ 裁：改为**逐条目**（只对 `Gold` 条目乘 1.5，其余原样）⇒ 与 `09` §9.9「✅ 只乘金币」一致。

---

## 三 `Q1`~`Q9` 全裁

| # | 裁决 | 本端补正 |
|---|---|---|
| `Q1` | ✅ **准**（末尾追加第 14 项 ＋ `ResourceCatalog.Table` 加一行 ＋ `:35` 注释勘正）；⭐ **准"8 类代价"** —— 尤其**同意"退役面不拆片"**（拆＝两套水并存＝双真源，与 `M1-A` 红线同族） | ＋ 注释勘正扩到 5 处（§二-2）；＋ `Q1` 落枚举后 `R3_SupplyChain:97` 文字**必须**同批勘正 |
| `Q2` | ✅ **准 1**（⛔ 非 0）—— 三条理由本端**逐条复核成立**；⭐ 其中"`Well.asset producer.capacity=0 ⇒ 仓容 100` 与 `WaterNetwork.capacity=100` **数值巧合相等**"是本片**最关键的论据**（我实读坐实） | ⛔ 否决"体积 0"（会新增第二个特例 ＋ 需另定"井满"判据＝新机制） |
| `Q3` | ✅ **准本片定案**（去 ⏳）＋ `Well.asset` **保持**全标签 ＋ `farm.asset` **加** `- res_fluid.water`（⛔ 不用族前缀） | ⭐ **准"须显式声明口径差异"**：`M1-E` 给国库用**族前缀**（`res_currency`）vs 本片给**全标签** —— 理由＝**国库＝通用容器** vs **农场＝专用工位仓** ⇒ 两者不矛盾，但**必须在 `09` 留注** |
| `Q4` | ✅ **准双双改 `int`**（`waterCarryAmount`／`waterThreshold`）＋ `ProducerConfig.rate` **保 float**（速率≠量；且 40 栋资产共用 ⇒ 改类型＝全资产重序列化） | ⭐ 准"须在 Unity 打开状态下改 `GameScene.unity`"；＋ **补一条**：`StorageComponent.GetCarryAmount` 一族返回 `int` ⇒ 与水口径**已一致**（零连带） |
| `Q5` | ✅ **准 ⛔ 不改 `well.asset` schema** —— 用现成 `rate`（点/秒）；⭐ **准"「每 N 秒产 1 点」是表达方式、⛔ 非新字段要求"**（本端钉死此口径） | ＋ 本片**保持 `rate: 4`**（数值批再调） |
| `Q6` | ✅ **准甲案**（`KingdomDestType.WaterNetwork` **退役但保占位** —— 序列化稳定面，⛔ 不删不重编号）＋ `KingdomTaskType.WaterHaul` **保留**（换 args 语义） | ⭐ **准"⛔ 不提前做能力表"**（`05` §十 是未来态）；＋ **必做**：并入本片加**国别过滤**（§二-1）；＋ 注释勘正（§二-2） |
| `Q7` | ✅ **准"本片只做删＋回调，记账随 `U-11`"** —— 理由三条成立（`U-11` 已裁独立片／`§15.5` 要求零行为风险／现状"系统替吃"本就零记账 ⇒ **不构成回归**） | ⭐ **连带强制**：`U-11` 任务书**必须**补「**第一出口 ＝ 吃**」（本片引入的新消亡口）⇒ 见 §六 落账 |
| `Q8` | ✅ **准甲案**：① 非兽人死亡不掉背包 ⇒ **缺口**（非简化）｜本片**只清不落** ② `ResetForReuse` 补清 ⇒ ⭐ 准 **(b) `GetOrAddInventory().Clear()`**（一石二鸟，顺带兑现 `§十-4`） | ⭐ ① 缺口**挂到 `U-11`**（与 `Q7` **同域** —— 都是"消亡"）；＋ 本端 §一 复核确认 `DamageSystem:648` 是**结构性丢条目** ⇒ 升必做（§二-4） |
| `Q9` | ✅ **准"只做吃 ＋ 自交互框架"** —— ⛔ 不做穿脱装备（`res_equipment` 🔸 待实现 ＋ 装备位＝新容器结构）／⛔ 不做用工具（依赖未落）／⛔ 不做取食任务（`§15.5` 第二步） | ＋ 交付报告**必须声明**"框架就位、动作只落吃" |

---

## 四 §六 列报 8 条全裁

| # | 项 | 裁决 |
|---|---|---|
| 1 | `§十-4`「单位一律带仓」未落地 | ⭐ **搭本片**（与 `Q8`-②(b) **同向** ⇒ 零边际成本）；⚠️ 落点＝单位**生成/出池**（`GetOrAddInventory` 语义已对，只是调用面窄） |
| 2 | `U-12` 非兽人死亡掉背包 ＝ 缺口 | ⭐ **准**（缺口非简化）⇒ **挂 `U-11`**（同域）；本片只消池化继承 |
| 3 | `Well.droppable` 未声明 | ⭐ **裁「`Well.asset` 显式 `droppable: 0`」** —— 理由：水是**工位缓冲**（水井→农场的中转），⛔ 非战利品；且水是**隐藏资源**（无专门 UI）⇒ 掉箱后靠 `HH.316` 搬运链搬回＝**资源空转**（箱→工人→仓，净位移为零）；⚠️ 与 `§九 :358` 护栏同精神（拦"无意义掉落"） |
| 4 | 水是否需要专门 HUD 行 | ⭐ **准"不需要"** —— `WarehousePanel` 会显示水井仓的水（结构性可见）⇒ 走仓面板即可；⛔ 不新增 HUD |
| 5 | AI 侧水仓读口 | ⭐ **准"本片不接"** —— 现状 AI 不读水；AG 台账读口归 `#52` 非金面（`M1-G`）⇒ **显式声明** |
| 6 | ⭐⭐ `§15.4` 容量落点（甲/乙/丙三案） | ⭐ **改裁：裁「乙案」＝容量基数尾插进 `NpcProfessionDef`（默认 10）**，⛔ **否决报告的甲案** —— 理由见下 |
| 7 | `carryCapMul` 乘数语义 | ✅ 准（同乘数继续作用于新容量线） |
| 8 | `DamageSystem` `×1.5` 与多资源 | ⭐ **升为必做**（§二-4：现实现对多条目是**结构性丢**，非"须复核"） |

### ⭐⭐ §四-6 的改裁理由（本端独立取证）

报告三案：**甲**（`ResourceCarryConfig.asset` 改形按职业）／**乙**（容量进 `NpcProfessionDef`）／**丙**（代码常量）。

⛔ **否决甲案**（报告的首选）—— 实读 `ResourceCarryConfig.cs:4-11` **注释自陈**：

> 「担当文档所称「ResourceCarryDef SO」：**按资源类型区分工人一次搬运量**」
> 「`WorkerTask` 与 `StorageComponent` 经 `Resources.Load` 查本表填充**携带量**」

⇒ ⭐ **该 SO 的真语义 ＝ 「按资源类型的"一次搬运量"」，且它服务两个消费者**：
- `WorkerTask.GetCarryAmount`（`TaskScheduler:940/1121` 派工规模）
- `StorageComponent.GetCarryAmount`（`:297-304` ⇒ `TaskScheduler:773/870` 取货上限）

⇒ 而 `§15.4` 要的是「**按职业的"背包总容量线"**」⇒ ⭐ **两个不同语义**。单资源时代二者**恰好等价**（背包只装一种、装满即搬走）⇒ 未暴露；多资源化后**必然分家**。
⇒ ⛔ 甲案改形 ⇒ **误伤"一次搬运量"**（`StorageComponent` 侧语义崩）＋ 掩盖真正的概念混用。

⭐ **裁乙案**（理由三条）：
1. `§15.4` 的表**本身就是按职业**的（Porter 20／Worker 10／士兵 10／其他 10）⇒ 容量是**职业属性**；
2. ⭐ 与 `RaceDef.carryCapMul:99`（**种族**修正 · `RaceDef.cs:24` 表格文档化为"携带上限%"）**完全对称** —— **职业基数 × 种族修正**，两个 SO 各管一维 ⇒ 概念清晰；
3. 代价小：**尾插字段零 bump** ⇒ 资产**可不动**（Unity 缺字段用代码默认 10）；仅 `Porter` 一家显式填 20 ⇒ **改 1 个资产**。
⛔ 否决丙案（代码常量）＝ 违 so-data-driven 铁律。

⚠️ **施工前置**：须先报 `NpcProfessionDef` 的字段形状 ＋ `Occupation` 枚举面（确认"其他"桶的取值集）⇒ **本端未独立核**（如实声明）。

---

## 五 施工批件预览（**待本裁决落账后另行签发任务书**）

⭐ 本片面大，建议**切两批**（本端倾向）：

| 批 | 内容 | 理由 |
|---|---|---|
| **`F-1` 水仓化 ＋ 真搬运链** | `Q1`/`Q2`/`Q3`/`Q4`/`Q5`/`Q6` ＋ §二-1 国别过滤 ＋ `WaterNetwork` 退役面 ＋ `Well.droppable: 0` | 自成闭环（水从"半假"变"真"）⇒ 可独立回归 |
| **`F-2` NPC 仓多资源化 ＋ 自交互** | `Q7`/`Q8`/`Q9` ＋ `§十五` 第一步 ＋ `§十-4` ＋ `Q8`-② 清包 ＋ `DamageSystem` 逐条目改 | 动 `WorkerInventory` ＋ 存档 ＋ 30 处调用面 ⇒ 风险集中，独立成批便于定位 |
⚠️ 两批**共用一个前置**（`Q1` 落枚举）⇒ ⛔ 不可并行（会双改 `ResourceType`）。

---

## 六 落账

- 本裁决书新建（本节）
- 台账 **§一百一十一**
- `_编号登记.md`：`HH.319` ⇒ **已评估并裁决 · 待施工**
- `_任务队列.md`：新插 **`M1-F` 主行**；**`U-12` 行**补裁法（甲案 ＋ (b) 清包 ＋ 缺口挂 `U-11`）；**`U-11` 行**补「第一出口 ＝ 吃」
- `_当前快照.md`：`D807`
- `_策划教训库.md`：新立 **`L-57`**
- `09_资源与仓库.md`：`Q3` 定案（去 ⏳）＋ `§15.4` 容量落点（乙案）＋ 水体积 1 ＋ `Well.droppable: 0` 口径 ＋ `M1-E`/本片标签口径差异注

---

## 七 状态

| | |
|---|---|
| `HH.319` `M1-F` 评估 | ✅ **已交付并验收 · 全采** |
| 本端补正 | **4 条**（1 必做约束 ＋ 3 勘正/统一）＋ **1 处改裁**（§四-6 甲⇒乙） |
| `M1-F` | ⭐ **🔵 已评估并裁决 · 待出施工任务书**（建议切 `F-1`/`F-2` 两批） |
| `M1` 完成度 | **5/7**（`A`~`E` 已销号 · `F` 待施工 · `G` 未开） |

**▶️ 下一步**：出 `M1-F` **施工任务书**（`F-1` 批优先 · 含 `Q1`~`Q6` ＋ §二-1 ＋ `Well.droppable`）⇒ ⚠️ 前置：须先报 `NpcProfessionDef` 字段形状（§四-6 改裁的前置）。
