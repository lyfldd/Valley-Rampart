# HH.314 · M1-B（账本 ＋ 读口）交付报告

- 任务号：HH.314（承 HH.313 · M1-A 已验收销号 · D790）
- 归属模块：中层 `09_资源与仓库.md` · 施工片 M1-B
- 状态：**已完成**（1 项待裁决 · 见 §三）
- 编译：Unity 0 error（15 warning 全为存量）
- 冒烟：同局冒烟已跑（正门 `TestHarnessApi.EnterTestRun` · seed=31418 · 15x）
- 日期：2026-09-20

---

## 〇 结论速览（四件）

| 件 | 任务书要求 | 本端结论 | 是否落码 |
|---|---|---|---|
| 件1 | 账本汇总量缓存决策 ＋ 契约注释 | **判不加缓存**（同意任务书建议）＋ 契约语义写进 `IWarehouse`/`StorageComponent` | ✅ 注释落码（无缓存代码） |
| 件2 | 读口调用方分类 | **独立复核与任务书读数逐条相符**；两个「备而未用」项 ⇒ 不预优化、不删 | ✅ 报告（无代码） |
| 件3 | DZ-1 `WarehousePanel` 容量重复计入（「本片修」） | ⚠️ **任务书前提被实读推翻**（逐行无虚增）⇒ 未改码，**报裁** | ⛔ 未落码（见 §三） |
| 件4 | DZ-2 读档后账本与仓容一致性 | clamp **需要**且已在实现内 ＋ 冒烟暴露真缺口 ⇒ **补落码 `RefreshCapacity`** | ✅ 落码（`BuildingFactory:336-341`） |

**改动面**：3 个生产码文件（净 +30/−2）＋ 1 个 Editor 冒烟容器 ＋ 本报告。`WarehousePanel.cs` / `TreasureVault.cs` / 任务书 §二 ⛔ 清单**一行未动**。

---

## 一 件1 · 账本汇总量 —— 缓存决策与依据

### 1(a) 决策：**不加缓存**（A 案与 B 案均不采用）

任务书写「本端建议先不加 …… 若你判需加，选 A/B 之一并说明依据」。本端**判不需要**，依据（均为实读，非推断）：

| 依据 | 实读 |
|---|---|
| 键数上界 | `ResourceCatalog.Table` **13 行**（`ResourceCatalog.AllTypes` 枚举源） |
| 实盘单仓键数 | 冒烟 §B/§E：`Warehouse Contents=木材×40`（**1 键**）；全库 7 栋挂仓**全为专属路径**（`res_material.wood` 等）⇒ 实盘 1 键 |
| 现算成本 | `TotalCount`(:101)／`UsedSpace`(:112) 各＝一次 `_items` 遍历（≤13 次字典查）＋ 体积表查（13 行常量表） |
| 调用频率 | 生产调用面全在**低频点**：面板刷新（`BuildingPanel:185/186/426`）／调度派发（`ScheduleCenterStub:119/132`）／存档（`Building:1044`）／经济结算（`AIEconomySettlement:49`）——无逐帧调用方 |

⇒ 现算成本可忽略，缓存收益 < 失同步维护面。**A 案（仓级缓存）不采用**（其前提"需要缓存"不成立）；B 案（条目级缓存）任务书已列"不推荐"，本端同意（平行派生表必与 `_items` 失同步）。

### 1(b) 契约语义已落码（注释即契约）

- [StorageComponent.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L31-L49)：事件声明上方 19 行，四段——
  **何时发**（只在真实量变的写口发；空转/幂等不发）／**发几次**（每次写口 1 次；部分成功也发；`RestoreContents` N＋1；`Harvest` 整批 1 次；⛔ 无节流）／**载荷**（只带本仓引用、⛔ 不带 diff ⇒ 订阅方必须全量重读）／**读值时效**（现算 ⇒ 回调内即最新值）。
- [IWarehouse.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/IWarehouse.cs#L20-L24)：`<remarks>` 追加"变更订阅**不在本接口面**"（⛔ 不新增事件成员）＋ 同上语义摘要 ⇒ 接口消费者不必翻实现即知订阅纪律。

**契约条文的实测验证**（冒烟 §C，真调用计数）：

| 写口 | 期望 | 实测 |
|---|---|---|
| `Deposit` 成功 | 1 | **1** ✅ |
| `Deposit` 标签拒收 | 0 | **0** ✅ |
| `TakeOut` 成功 | 1 | **1** ✅ |
| `TakeOut` amount≤0 | 0 | **0** ✅ |
| `Clear` | 1 | **1** ✅ |
| `RestoreContents`（2 条·全被接受） | 3（N＋1） | **3** ✅ |

### 1(c) 「同算式双出口」约束

`FreeSpace`(:123) ＝ `capacity - UsedSpace`，与 `UsedSpace`(:112) 同算式 ⇒ 已在注释中明示「若将来加缓存，二者必须同走缓存，否则两条读数分叉」。**本片不加缓存，故无落码。**

---

## 二 件2 · 读口调用方分类（独立复核）

复核方法：全库 `Assets/` grep（`.读口` 形式），逐条回原处读上下文。**任务书读数逐条相符**，并补两处。

| # | 读口（函数级 · `StorageComponent.cs` 现行行号） | 生产调用方（调用面 file:line） | 判定 |
|---|---|---|---|
| 1 | `Query()` :170 附近的 `IWarehouse.Query`（接口 :32） | [WarehouseHelper.cs:115](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/WarehouseHelper.cs#L115)（TryCheckEnough）／[:131](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/WarehouseHelper.cs#L131)（LockTakes） | **有调用方**（`IWarehouse` 面唯一外部消费点 ✔ 与任务书相符） |
| 2 | `SumByPrefix(string)` :147 | **生产 0**（仅 Editor 探针 `Valley_HH313_M1A_WarehouseProbe.cs:107-111`） | **备而未用** ✔ |
| 3 | `FreeSpace` :123 | **生产 0**（仅 Editor 探针 `Valley_HH313_M1A_WarehouseProbe.cs:69`、本片冒烟 `Valley_HH314_M1B_Smoke.cs:96`） | **备而未用** ✔ |
| 4 | `TotalCount` :101 | `AIEconomySettlement:49`／`Building:1044`／`WarehouseRegistry:46`／`ScheduleCenterStub:119,132,150`／`TaskScheduler:709` | 有调用方 |
| 5 | `UsedSpace` :112 | `BuildingPanel:185,186,426`／`KingdomBrain:477` | 有调用方 |
| 6 | `IsFull` :126 | `BlacksmithBuilding:47,89`／`TaskScheduler:894`（`:765` 的 `inv.IsFull` 属 `WorkerInventory`） | 有调用方 |
| 7 | `CanAccept(type)` :135 | `WarehouseRegistry:66`／`SiegeWorkshopBuilding:145`／`TaskScheduler:822` | 有调用方 |
| 8 | `Contents` :170 | `Building:720,721`（存档）／`BuildingPanel:426`（UI）；`TreasureVault:85` 为转发 | 有调用方 |
| 9 | `GetAmount(type)` :98 | `AIEconomySettlement:113`／`Building:1047`／`WarehousePanel:131,133`／`MineByproductComponent:108,120,137-139,202`／`SiegeWorkshopBuilding:161,200`／`TaskScheduler:712,774,781`／`RulerController:250,252,310,371`／`TreasureVault:97` | 有调用方（**最广**） |
| 10 | `PrimaryStoredType()` :299 | `Building:1046`／`ScheduleCenterStub:150`／`TaskScheduler:710` | 有调用方（**过渡读口** · 归 `M1-G` 收口） |
| 11 | `Accepts(type)` :79 | `WarehousePanel:129`／`WarehouseRegistry:66`／`TaskScheduler:703,822,895` | 有调用方 |
| 12 | `DeclaredPaths` :76 | 仅自身（`Accepts` :79）；写入用 `SetDeclaredPaths` :70 | 内部 |

### (b) 两个「备而未用」项的显式结论

**不预优化、不删。** 理由：
- `SumByPrefix` ＝ `09` §7.5-⑥「查子树」（`res_material` 一次拿到石＋木＋矿合计）的**契约实现**；`09` §三 把「前缀＝路径」定为唯一机制，子树查询是该机制的必备读口（M1-C 建造走仓、M1-D 掉落箱都要按分类查）。
- `FreeSpace` ＝ `09` §5.2 硬规则 1「一条容量线」的**直接读数出口**（`capacity - UsedSpace`），是 `CanAccept` 之外的**非类型化**容量读数；删除会让"这条容量线还剩多少"必须由调用方自行做减法（复算公式外泄）。
- 两者均无维护成本（各 1~3 行纯读），删除只减接口完整性、不减复杂度。

### (c) 与件1 的联动

件1 判**不加缓存** ⇒ `FreeSpace`/`UsedSpace` 无「同改」义务（该约束已写入注释备用）。

---

## 三 件3 · DZ-1 面板容量重复计入 —— ⚠️ 前提被实读推翻，**未改码，请裁决**

### 3.1 任务书原文（§一 件3）

> 缺陷成因：容量项 `t.cap + storage.capacity` 在**资源类型循环内**逐次累加，同一仓的 `capacity` 被按接受的资源类型数**重复计入** ⇒ 通用仓（`res`）容量列虚增（接受 N 类即虚增 N 倍）。
> 修法：容量应与资源类型解耦——在循环外对每个 storage 取一次 `capacity`，循环内只累加 `stored`。

### 3.2 代码 file:line 实读（现行原文）

[WarehousePanel.cs:127-134](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/UI/WarehousePanel.cs#L127-L134)：

```csharp
foreach (var type in ResourceCatalog.AllTypes)
{
    if (!storage.Accepts(type)) continue;
    if (totals.TryGetValue(type, out var t))
        totals[type] = (t.stored + storage.GetAmount(type), t.cap + storage.capacity);
    else
        totals[type] = (storage.GetAmount(type), storage.capacity);
}
```

**关键事实**：`totals` 的键是 **`ResourceType`**（:115），`storage.capacity` 累加进的是 **`totals[type].cap`** —— 对**同一个 type 行**，本仓 capacity 只加 **1 次**（`else` 分支或 `+=` 各一次）。
⇒ 「接受 N 类即虚增 N 倍」在**逐行**意义上**不成立**（虚增的是**跨行合计**，不是任何一行）。

### 3.3 in-game 读数（冒烟 §D · 反射**真**面板 `RebuildStorageList`）

```
§D 基线（专属声明）行数=4 ⇒ 食物=0/260 金币=0/200 木材=40/120 石材=0/100
§D 转通用：Warehouse 声明＝ [res] ⇒ Accepts(Wood/Stone/Food/Gold)=True/True/True/True
§D 通用后行数=13 ⇒ 食物=0/300 金币=0/240 木材=40/120 石材=0/140 矿石=0/40 Metal=0/40 水晶=0/40 火油=0/40 特殊食物=0/40 肉=0/40 StoneAmmo=0/40 FireballAmmo=0/40 MagicAmmo=0/40
§D ⭐ 容量列 delta（通用后 − 基线）＝ 食物+40 金币+40 木材+0 石材+40 矿石+40 Metal+40 水晶+40 火油+40 特殊食物+40 肉+40 StoneAmmo+40 FireballAmmo+40 MagicAmmo+40
```

读法：把一座 `capacity=40` 的仓从专属（木）改成通用 ⇒ **13 行每行容量恰好 +40，各一次**；若按任务书所述"N 倍虚增"，应出现某行 **+520**。**实测无 +520**。
（木材 +0 因该仓在基线里已按木计过一次 ⇒ 恰好印证"每仓对同一 type 只计一次"。）

### 3.4 任务书「勘正要点」复核 —— ✅ 成立

> ⚠️ 勘正要点：**本缺陷与事件订阅无关**。`SubscribeAll`（`:75-91`）是**按建筑**订阅……

实读 [WarehousePanel.cs:75-91](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/UI/WarehousePanel.cs#L75-L91)：`for` 遍历建筑 → `b.GetComponent<StorageComponent>()` → `if (!_subscribed.Contains(storage)) storage.OnStorageChanged += OnStorageChanged` ⇒ **按仓订阅＋`_subscribed` 去重**，不存在"按资源订阅 N 次"。**勘正要点成立，本端未动订阅逻辑**（一行未动）。

### 3.5 但确实存在一个**不同**的问题（真问题 · 非任务书所述）

**跨行合计虚增 ＋ 共享容量按类型全额展示**：13 行相加 ＝ 40×13 ＝ **520**，而物理容量只有 **40**。
- 面板**不显示合计** ⇒ 当前**不可见**（无可见错误读数）
- 但有**可误导**的读数：通用仓装满木材（40/40）时，"石材 0/40" 会让玩家以为还有 40 石材位 —— 物理上已满
- 可达性：当前 7 栋挂仓全为专属路径（1 仓 1 类型）⇒ **不可达**；`M1-C` 通用仓上线即成真

**注意任务书"修法"会改变语义**：把 `capacity` 移到类型循环外"只取一次"，则通用仓的 40 只能落到**某一行**（其余 12 行容量为 0）或**整体一行** —— 这已不是"修 bug"，而是**重新定义容量列的语义**（共享容量在按类型分行的表里怎么表达）。属 UI 设计决策，越出本片"只改结构"红线。

### 3.6 请裁决（三选一）

- **A（本端倾向 · 本片不动）**：逐行读数正确、缺陷不可达、无可落码之处 ⇒ 保持现状；「跨行合计 / 共享容量如何展示」与 `M1-C` 通用仓上线**同批**裁决（届时应一并决定是否加"总容量"行、通用仓是否单列）。**本片按 A 执行（未改码）。**
- **B（通用仓单列）**：面板对"同一仓被多类型命中"的情况只出一行（如"通用仓 40/40"），不逐类展开 ⇒ 需策划端定 UI 口径（改的是展示语义，不是累加逻辑）。
- **C（按仓去重后分摊/加合计行）**：若将来加"总容量"行，须按**仓**去重（每仓 capacity 计 1 次）⇒ 属新增功能，建议归 `M1-C`。

> 本端不自行选 B/C：两者都需"容量列应当表示什么"的策划口径，且当前不可达、无可见错误 —— 自选即替策划端做设计决策（`09` 没写清的回来问）。

---

## 四 件4 · DZ-2 读档后账本与仓容一致性 —— 结论与落码

### 4.1 结论 1：`TrimToCapacity` 兜底**需要** ⇒ 落点选「`RestoreContents` 内」（现已在）

实读 [StorageComponent.cs:323-338](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L323-L338)：`RestoreContents` 逐条目经 `Add(e.type, e.amount)`，`Add` 内即受容量约束 ⇒ 满则 clamp 并 `Debug.LogWarning("读档 … 超容量 clamp …")`。

**落点依据（二者选一 · 选实现内）**：clamp 是**仓库不变量**（`09` §5.2 硬规则 1：存量 ≤ 容量），**任何**写口都必须维持 ⇒ 不变量归实现；调用方 `BuildingFactory` 只负责"把存档数据搬进仓"，不负责不变量（否则每个调用方都要记得 Trim，漏一个就破不变量）。

### 4.2 结论 2（冒烟发现）：单靠 clamp 会把**高等级仓的存量误删** ⇒ 补落码

**根因**（file:line 直读）：[BuildingFactory.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L332) 读档时 `b.level = Mathf.Max(1, data.level);`（:332）恢复等级，但**未重算容量** ⇒ `Init` 阶段（`StorageComponent.cs:59` 调 `RefreshCapacity()`，此时 `level` 尚为默认 1）算出的 **Lv1 档容量**一直用到 `RestoreContents`（:336）⇒ clamp 按 Lv1 容量截断。

**修复前实测**（冒烟 §E 第 2 跑）：

```
§E 存档前：Granary level=3 capacity=240 Contents=食物×180
§E 读档后：Granary level=3 capacity=60 Contents=食物×60
⇒ 容量未随 level 刷新（缺口坐实）；存量 180→60 被 clamp 净损 120
```

**是否存量缺陷**：`RefreshCapacity`（`Init` 调用）与 `SpawnFromSave` 均在 `M1-A` 之前既有 ⇒ 缺口**非本片引入**；但 `M1-A` 引入 clamp 后，该缺口从"超容量显示（不丢数据）"**升级为"数据损失"** ⇒ 件4 必须一并修，否则 clamp 从"兜底"变成"丢档源"。

### 4.3 落码（调用方落点）

[BuildingFactory.cs:336-341](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L336-L341)：

```csharp
var storage = b.GetComponent<StorageComponent>();
if (storage != null)
{
    // ⭐ M1-B 件4：先按恢复后的等级重算容量，再灌存量（否则高等级仓按 Lv1 容量 clamp：实测 Lv3 粮仓 180→60 净损 120）
    storage.RefreshCapacity();
    storage.RestoreContents(data.storageContents);
}
```

**为何落调用方而非 `RestoreContents` 内**：容量是 `def.producer.capacity × LevelScale(level)` 的**函数**，而 `level` 在 `:332` 才恢复 —— `RestoreContents` 内部无法知道"正确的 level 是多少"（它只知道存档里的条目量）⇒ 必须先由调用方把 level → capacity 对齐，再灌数据。顺序即不变量：**先定容量，后灌存量**。

**修复后实测**（冒烟 §E 第 3 跑）：

```
§E 存档前：Granary level=3 capacity=240 Contents=食物×140
§E 读档后：Granary level=3 capacity=240 Contents=食物×140
⇒ 容量与存档前一致（未复现）
```

⇒ **240/140 → 240/140，零损失。**

### 4.4 行为影响（如实列报）

| 场景 | 影响 |
|---|---|
| Lv1 仓读档 | 容量不变（Lv1 档 = `Init` 默认值）⇒ **零变化** |
| Lv≥2 仓读档 | 容量由"错误回落 Lv1 档"改为正确值 ⇒ 存量不再被误 clamp（**由丢档变不丢档**） |
| 正式局（非读档） | 不经过 `SpawnFromSave` ⇒ **零影响** |

---

## 五 §二 同局冒烟读数

**口径**（`test-harness-first` 铁律 1 / L-32 / L-34）：
- 入口：**正门** `TestHarnessApi.EnterTestRun(cfg)`（守卫全开：判负封死 ＋ 野怪静默 ＋ 加速直通）；⛔ 未裸跑 `GameScene`
- 加速：**15x**（`WorldConfig.time.testSpeedMultiplier`）——测试段倍率；收工段见下
- 收尾：`Time.timeScale = 0f`（真暂停）→ `SaveManager.Save` 封盘 → `ExitTestRun()` → `EditorApplication.ExitPlaymode()`（**禁留 1x 余留世界** · L-32 条文 1/3）
- 在线判据（L-34）：§G 产出观测设**命中即停**
- 落盘：[hh314_m1b_smoke.txt](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Logs/hh314_m1b/hh314_m1b_smoke.txt)（稳定名 ＋ 时间戳副本）
- 容器：[Valley_HH314_M1B_Smoke.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH314_M1B_Smoke.cs)

**全文读数**（第 3 跑 · 件4 修复后）：

```
# HH.314 · M1-B（账本 ＋ 读口）同局冒烟（正门 EnterTestRun · seed=31418 槽=hh314_m1b）
# 跑次：2026-09-20 13:14:54
── §A 正门进局（seed=31418 Small/difficulty=2）
§A 起局：地图=128x128 日=1 建筑数=15 倍率=15
§A 直建：Warehouse=True@20,20 Granary=True@26,20 quarry=True ⇒ 建筑数=18

## §B 入仓读数（09 §5.2 三条硬规则）
§B 仓声明（专属）＝ [res_material.wood] capacity=40（＝ def.producer.capacity × LevelScale）
§B Deposit(Wood,60)=40（期望 40＝放到满为止·部分成功）；Deposit(Stone,10)=0（期望 0＝标签拒收）
§B 读数：TotalCount=40 UsedSpace=40/40 FreeSpace=0 IsFull=True CanAccept(Wood)=0 Contents=木材×40
§B 判据：TotalCount(40) == UsedSpace(40) ⇒ 体积全 1 时两口径相等 ✅

## §C 事件契约计数（M1-B 件1 落码的契约条文）
§C Granary·Add 成功     ⇒ 事件 1 次（契约期望 1）
§C Granary·标签拒收     ⇒ 事件 0 次（契约期望 0）
§C Granary·TakeOut 成功  ⇒ 事件 1 次（契约期望 1）
§C Granary·amount≤0      ⇒ 事件 0 次（契约期望 0）
§C Granary·Clear         ⇒ 事件 1 次（契约期望 1）

## §D 面板读数（反射**真** WarehousePanel.RebuildStorageList）
§D 面板实例=在场
§D 基线（专属声明）行数=4 ⇒ 食物=0/260 金币=0/200 木材=40/120 石材=0/100
§D 转通用：Warehouse 声明＝ [res] ⇒ Accepts(Wood/Stone/Food/Gold)=True/True/True/True
§D 通用后行数=13 ⇒ 食物=0/300 金币=0/240 木材=40/120 石材=0/140 矿石=0/40 Metal=0/40 水晶=0/40 火油=0/40 特殊食物=0/40 肉=0/40 StoneAmmo=0/40 FireballAmmo=0/40 MagicAmmo=0/40
§D ⭐ 容量列 delta（通用后 − 基线）＝ 食物+40 金币+40 木材+0 石材+40 矿石+40 Metal+40 水晶+40 火油+40 特殊食物+40 肉+40 StoneAmmo+40 FireballAmmo+40 MagicAmmo+40
§C 通用仓·RestoreContents(2 条·全被接受) ⇒ 事件 3 次（契约期望 3＝N+1）

## §F 件4 真缺口实证（读档 capacity 未随 level 刷新）
§F Granary level 1→3；capacity 60→240（期望 60×2×2=240；LevelScale 连乘 levels[].statScale）
§F 入粮 200 ⇒ 实际入仓=200 TotalCount=200 UsedSpace=200/240

## §G 产出／搬运观测
§G quarry 本地仓（专属 [res_material.stone]）：D1→D2 ⇒ TotalCount=5（**命中即停 @D2**）
§G HarvestCarry 真搬运口：TotalCount 5→2（搬走 3，≤ 携带量 10）

## §E 存读档复查
§E 存档前：Warehouse Contents=木材×2；Granary level=3 capacity=240 Contents=食物×140
§E Save(hh314_m1b) = True
§E Load = True
§E 读档后：Warehouse=True 声明=[res_material.wood] Contents=木材×2
§E 读档后：Granary=True level=3 capacity=240 Contents=食物×140
§E ⭐ 件4 缺口判据：存档前 Granary level=3 capacity=240 TotalCount=140 ⇒ 读档后 level=3 capacity=240 TotalCount=140 ⇒ 容量与存档前一致（未复现）
```

**读数与件1 决策的一致性核对**：本片判"不加缓存" ⇒ 冒烟全部读数须为**现算值**。核验点：§B `TotalCount=40` 与 `UsedSpace=40/40` 同帧一致（若缓存失同步会出现两值分叉）；§E 读档后 `Contents` 与 `TotalCount` 同帧一致（`RestoreContents` 覆盖 `_items` 后事件回调内读到的即最新值）⇒ **无分叉，与"现算"口径一致 ✅**。

**§G 说明**：`quarry` 在 D1→D2 内本地仓达 5 石（**真产出链**，非注入）⇒ 命中即停；`HarvestCarry` 行作用于**通用仓 Warehouse**（当时 `Contents=木材×2＋石×3`），按 `PrimaryStoredType()`（资源表序首个非空 ⇒ Stone）搬走 3 ⇒ 实测印证 `09#40`「直通国库」过渡语义（归 `M1-G` 删除）。

**冒烟读数与 §一~§四 各件的对应**：§B/§C→件1 契约；§D→件3；§F/§E→件4；§G→搬运/产出读口在役性。

---

## 六 逐条勘正对照（任务书 §一 现状实读 vs 本端实读）

| 任务书原文（§一） | 本端实读 | 判定 |
|---|---|---|
| 件1：`StorageComponent.cs:83 TotalCount` | 现为 **:101**（落码件1 注释 19 行后下移 18） | ✅ 相符（**换算 1:1**） |
| 件1：`:94 UsedSpace` | 现为 **:112**（−18 ⇒ :94） | ✅ 相符 |
| 件1：`OnStorageChanged`（`:32`） | 现为 **:50**（−18 ⇒ :32） | ✅ 相符 |
| 件1：Invoke 点 `Add(:177)/TakeOut(:200)/RestoreContents(:319)/Clear(:326)/TrimToCapacity(:344)/Harvest(:256)` | 现为 **:195/:218/:337/:344/:362/:274**（各 −18 ⇒ 与任务书**逐条相同**） | ✅ 相符（6/6） |
| 件1：`FreeSpace`（`:105`） | 现为 **:123**（−18 ⇒ :105） | ✅ 相符 |
| 件2：`Query()` 有真调用方 ＝ `WarehouseHelper.cs:115`(TryCheckEnough)／`:131`(LockTakes) | 实读**逐字相符**（:115/:131） | ✅ 相符 |
| 件2：`SumByPrefix()` **0 外部调用方** | 生产 0 ✅（仅 Editor 探针引用） | ✅ 相符 |
| 件2：`FreeSpace` **0 外部调用方** | 生产 0 ✅（仅 Editor 探针引用） | ✅ 相符 |
| 件2：其余读口"均有真调用方" | 11 项逐项有生产调用方 ✅ | ✅ 相符 |
| 件3：`WarehousePanel.cs:109-159 RebuildStorageList`／`:127` foreach／`:129` Accepts／`:131` totals | 实读**逐行相符**（109/127/129/131；方法体 :109-159） | ✅ 相符 |
| 件3：`SubscribeAll`（`:75-91`）按建筑订阅 | 实读**逐行相符**（:75-91；`_subscribed` 去重） | ✅ 相符 |
| 件3：**缺陷成因**"容量被按接受类型数重复计入 ⇒ 接受 N 类即虚增 N 倍" | ❌ **不成立**：`totals` 按 `ResourceType` 分键 ⇒ 同一行只加 1 次；in-game delta 每行 +40、**无 +520** | ⚠️ **推断不成立**（见 §三） |
| 件4：`BuildingFactory.cs:336 storage.RestoreContents`／`:339 vault.RestoreContents` | 实读**逐行相符**（:336/:339） | ✅ 相符 |
| 件4：`RestoreContents`（`StorageComponent.cs:305`）不校验 capacity 不 Trim | 现为 **:323**（−18 ⇒ :305）✔ 行号相符；**但**内容已含 clamp（`M1-A` 落码，`Add` 内受容量约束） | ⚠️ 行号相符、**描述已过期**（clamp 已落码） |
| 件4：`TrimToCapacity`（`:333`） | 现为 **:351**（−18 ⇒ :333） | ✅ 相符 |

**行号换算说明**：任务书 `StorageComponent.cs` 行号 = 本片件1 契约注释**落码前**的行号，换算系数 **−18**，**逐条 1:1 相符**（未发现任何偏移异常）；`WarehousePanel.cs`／`BuildingFactory.cs` 本片未动，行号**直接相符**。⇒ **任务书"现状实读"部分（代码位置）全部准确**；唯二不成立的是：件3 的**成因推断**、件4 的**"不 Trim"描述**（`M1-A` 已补 clamp）。

---

## 七 改动清单（file:line · 改动前 → 改动后）

| # | 文件 | 位置 | 改动前 | 改动后 |
|---|---|---|---|---|
| 1 | [StorageComponent.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L31-L49) | `:31-49`（事件声明上方） | 单行注释「存储变化事件（QQQ.2 §需求7 / DR-15…）」 | **契约四段**：何时发／发几次／载荷／读值时效（含"不加缓存"决定与"双出口须同走缓存"约束） |
| 2 | [IWarehouse.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/IWarehouse.cs#L20-L24) | `:20-24`（`<remarks>` 末） | （无） | 追加「变更订阅不在本接口面」＋ 语义摘要（⛔ 不新增事件成员） |
| 3 | [BuildingFactory.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/BuildingFactory.cs#L336-L341) | `:336-341` | `if (storage != null) storage.RestoreContents(data.storageContents);` | 加花括号 ＋ 注释 ＋ **`storage.RefreshCapacity();`**（件4 落码） |
| 4 | [Valley_HH314_M1B_Smoke.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley_HH314_M1B_Smoke.cs)（新建 · ＋`.meta`） | 全文件 347 行 | （无） | 同局冒烟容器（正门进局 · §A~§G · 落盘 `Logs/hh314_m1b/`） |
| 5 | 本报告 | `多Agent交接/执行端/HH.314_M1-B账本与读口_交付报告.md` | （无） | — |

**未改动（显式声明）**：`WarehousePanel.cs`（件3 未落码）、`TreasureVault.cs`、`ResourceCatalog.cs`、`RecipeCatalog.cs`、`GridTypes.cs`、地图四门、任何 `.asset`、任何存档迁移脚本、四档账本。
**行尾核验**：`BuildingFactory.cs` **CRLF 376→381 / LF 0 / NUL 0**（二进制按行替换，保留原行尾）；`StorageComponent.cs` LF 364 / `IWarehouse.cs` LF 48 / 新容器 LF 347（均 0 CRLF / 0 NUL）。

---

## 八 未完成项 / 待裁决

1. **待裁决（阻塞件3 收口）**：§三 3.6 —— 面板"共享容量按类型全额展示"（跨行合计 13×capacity）如何处置：**A 本片不动（本端已按 A 执行）** / B 通用仓单列 / C 按仓去重＋加合计行。⛔ 未改码即等此裁决。
2. **未跑长局**：本片为账本＋读口（结构＋读口面），冒烟已覆盖"起局→建仓→入仓→面板→存读档→复查"全链；**未跑长局考跑**（`L-30` 未列 gap 表）——若需"多日演化下账本一致性"，请指派（须走 `HH.92` 正门）。
3. **`PrimaryStoredType` / `HarvestCarry` 过渡读口**：仍在役（`M1-G` 删除），本片未动其语义。
4. **未 commit 账本**：⛔ 未碰四档账本（`vr-id-ledger` 纪律）。
5. **越界发现（报备 · 不属本片四件）**：冒烟 §E 读档期控制台出现 **4 条既有诊断告警**（见 §十），涉读档建筑身份（saveId）冲突，非本片改动引入、非本片范围 ⇒ 仅报备，请策划端定是否立 DZ。

---

## 十 冒烟期控制台观察（报备 · 越界发现）

冒烟 §E `SaveManager.Load` 期间控制台出现 4 条 `[BuildingFactory] SpawnFromSave 冲突`（原文）：

```
[BuildingFactory] SpawnFromSave 冲突：coord=(75,28) 已有 Building saveId=Building_090e8165082c428caca3da75a4ecc525（疑似路径 A 新随机 GUID+默认 kingdomId），存档侧 saveId=Building_e24f78b8ed374643a3ebeed4369ff947，defId=farm。双路径双份/复合腐坏风险——请核查读档建筑重建路径。
[BuildingFactory] SpawnFromSave 冲突：coord=(71,29) 已有 Building saveId=Building_8cba957b7a9048ccac3e2e190c857095（疑似路径 A 新随机 GUID+默认 kingdomId），存档侧 saveId=Building_226e23e8bebe40ee8494596e04983dad，defId=Warehouse。
[BuildingFactory] SpawnFromSave 冲突：coord=(45,110) 已有 Building saveId=Building_40974bfc3a6344f88cc55c99afa31908（疑似路径 A 新随机 GUID+默认 kingdomId），存档侧 saveId=Building_92e7365e80744d2fa2e956e9e39ab386，defId=farm。
[BuildingFactory] SpawnFromSave 冲突：coord=(42,112) 已有 Building saveId=Building_5551e1fb8f084d8b948aaffe55bcfa59（疑似路径 A 新随机 GUID+默认 kingdomId），存档侧 saveId=Building_768c0dc3c577474ebd2bd406da66140a，defId=Warehouse。
```

**判定（本端口径）**：
- 4 处坐标 **(75,28)/(71,29)/(45,110)/(42,112) 均非本片直建落点**（本片建在 20,20／26,20／32,20）⇒ 属**世界既有建筑**（farm／Warehouse），**非本片构造的测试场景**。
- 成因＝读档时该坐标**已由"路径 A"（世界生成/王国建立，新随机 GUID ＋ 默认 kingdomId）新建**，随后 `SpawnFromSave` 按存档 saveId 再建 ⇒ 撞位。诊断文本自身即写着「请核查读档建筑重建路径」⇒ **既有已知缺陷**，与本片 4 件（缓存／读口／面板容量／仓容一致性）**无交集**。
- 本端改动（`RefreshCapacity` 前置）**不可能**产生 saveId 冲突（该冲突发生在 `BuildingRegistry` 查位阶段，早于容量与存量恢复）⇒ **非本片引入**。
- §E 的账本／仓容读数不受影响（`Granary`／`Warehouse` 读档读数一致）⇒ 本片验收判据不因此动摇。

**处置建议**：仅报备。若策划端要追，建议立 DZ 归「读档重建路径」专项（涉 `BuildingFactory` 双路径 ＋ 王国建档时序），**不并入 M1-B**。

**其余控制台项（均为既有噪音 · 与本片无关）**：`The referenced script on this Behaviour (Game Object 'ruler') is missing!`（场景内 ruler 物件脚本引用缺失 ⇒ 属 `GameScene` 资产面，本片未动该场景资产）／`No Theme Style Sheet set to PanelSettings`（UI Toolkit 配置）／`Some objects were not cleaned up when closing the scene [SpriteAnimatorDriver]`（退场清理）。

---

## 九 红线自检

| 红线 | 状态 |
|---|---|
| ⛔ 不碰 `GridTypes.cs` | ✅ 未动 |
| ⛔ 不碰地图四门 | ✅ 未动 |
| ⛔ 不写存档迁移脚本 | ✅ 未写（`RefreshCapacity` 是读档**重建**路径，非档格式迁移） |
| ⛔ 不写双轨开关 | ✅ 未写 |
| ⛔ 不碰训练仓 | ✅ 未动 |
| ⛔ 只改结构 | ✅ 件1/件2 零行为改动；件4 为**读档容量恢复**修正（§4.4 已列行为影响） |
| ⛔ 不碰并行会话未提交改动（美术／pixel-forge／`GameScene`／`Packages`／3.6·3.8 doc） | ✅ 未 add 未改（`GameScene.unity` 出现在 `git status` 系本端 `manage_scene load` 所致，**不纳入提交**） |
| 判据三直读（设计稿／file:line／档位字段） | ✅ §三 引 `09` §5.2/§7.5 原文 ＋ 代码原文 ＋ 行号；§一 引 `ResourceCatalog` 13 行 |
| 行尾纪律（CRLF 文件禁 Edit/Write） | ✅ `BuildingFactory.cs` 走二进制按行替换＋前后计数核验 |
| 提交纪律（具名 `git add`、禁反引号） | ✅ 具名 6 项：`db864acc`（M1-B 四件 · 6 files +650/−2）／`dcb23e2b`（HH.313 报告漏提补提交 · 1 file +16/−9） |
| `git checkout --` / `restore` 禁用 | ✅ 全程未用；取旧版仅用 `git show` |
| 不 push | ✅ 未 push |
