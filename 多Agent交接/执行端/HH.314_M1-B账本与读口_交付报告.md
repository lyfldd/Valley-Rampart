# HH.314 · M1-B 交付报告 —— 账本 ＋ 读口

> 执行端：TraeCode｜依据：`HH.314` 任务书（M1-B 四件）＋ `09_资源与仓库.md` §六／§7.5／§5.2 ＋ `D790` 验收裁决
> 前置：`HH.313` M1-A 补正已闭环（commit `9656cf69` 交付＋`2546e5ff` 回执）
> 复算口径：**本端独立 grep 全库 `Assets/**/*.cs`**（⛔ 未抄任务书读数）
> ⚠️ **本片两处「现状描述」经实读不成立，已按用户裁决「停下报裁·不改码」执行**（见 §三／§四）

---

## 〇、结论速览

| 件 | 结果 |
|---|---|
| 件 1 账本汇总量缓存决策 | **判：不加缓存**（与任务书建议一致）＋ 落码：`OnStorageChanged` 契约语义写进 `StorageComponent`／`IWarehouse` 注释 |
| 件 2 读口调用方分类 | **两列清单已出**；任务书点名的 2 项「备而未用」**复核相符**；⚠️ **新发现第 3 项**（`CanTake` 亦 0 调用方） |
| 件 3 DZ-1 面板容量重复计入 | ⚠️ **前提不成立**（不存在 N 倍虚增；09 §六 明载「行 ＝ 仓库 × 资源」）⇒ **未改码**，报裁 |
| 件 4 DZ-2 读档后账本与仓容一致性 | ⚠️ **前提不成立**（`RestoreContents` 已逐条 clamp，**不需要** `TrimToCapacity`）⇒ **未改码**，报裁；**另查出真缺口** 1 条（容量未随等级刷新 ⇒ 读档过度 clamp 丢存量） |
| 编译读数 | ⛔ **未取到**（见 §六 阻断 1） |
| 同局冒烟读数 | ⛔ **未取到**（见 §六 阻断 2） |
| 红线 | `GridTypes.cs` ⛔未碰／地图四门 ⛔未碰／存档迁移脚本 ⛔未写／双轨开关 ⛔未写／训练仓 ⛔未碰／四档账本 ⛔未碰 |

---

## 一、件 1 · 账本汇总量缓存决策（判：**不加缓存**）

### 1.1 决策与依据

| 依据 | 读数 |
|---|---|
| 键数上界 | `ResourceCatalog.Table` **13 行** ⇒ `_items` 字典键数 ≤ **13** |
| 实盘单仓键数 | 7 栋挂仓建筑**全为专属/分类声明** ⇒ 单仓实盘 **1 键**（`Warehouse`/`Granary`/`Blacksmith`/`farm`/`quarry`/`AdvancedStorage`/`Well`） |
| 现算成本 | `TotalCount`＝O(k)、`UsedSpace`＝O(k)，k ≤ 13 ⇒ 单次 **≤ 13 次字典遍历**（无分配、无装箱） |
| 调用密度 | 生产码 `TotalCount` **6 处**／`UsedSpace` **4 处**（见 §二），均**非每帧路径**（结算/面板/诊断时点） |
| 缓存代价 | A 案需 1 个 `_cachedTotal` ＋ 1 个 `_cachedUsed` ＋ 失效点挂在 6 个写口旁 ⇒ **新增失同步面**，换来的收益在 k ≤ 13 时可忽略 |

⇒ **判：先不加**（属预优化）。`FreeSpace`／`UsedSpace` 保持**现算双出口** ⇒ ⛔ **无分叉风险**（任务书 (c) 的前提是"加了缓存"，本片未加）。

### 1.2 落码（契约注释 · 任务书 (b) 硬要求）

- [StorageComponent.cs:31-52](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L31-L52)：`OnStorageChanged` 的**何时发／发几次／载荷／读值时效**四段契约。
- [IWarehouse.cs:21-26](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Kingdom/IWarehouse.cs#L21-L26)：`<remarks>` 补「变更订阅不在接口面」＋语义摘要（⛔ **不新增事件成员**）。

**契约条文（落码原文摘要）**：

| 维度 | 条文 |
|---|---|
| **何时发** | 只在**真实量变**的写口发：`Add`／`TakeOut`／`Clear`／`TrimToCapacity`／`Harvest`（量>0）／`RestoreContents`。**空转/幂等不发**（`amount≤0`／标签拒收／容量 0／取到 0） |
| **发几次** | 每次写口调用**各发 1 次**；`Add`/`TakeOut` **部分成功也发**；`RestoreContents` 逐条目经 `Add` ⇒ **N＋1 次**；`Harvest` 整批 1 次。⛔ **无节流** ⇒ 节流**归订阅方自理**（`09` §7.5-⑦） |
| **载荷** | 只带**本仓引用**，⛔ **不带 diff** ⇒ 订阅方**必须全量重读**，⛔ **不得增量累加** |
| **读值时效** | 汇总量**现算** ⇒ 回调内读到的**即最新值**（若将来加缓存：`FreeSpace` 与 `UsedSpace` **同算式双出口必须同走缓存**） |

### 1.3 现状复核（任务书给的发点清单 · 逐行实读）

| 发点 | 任务书 | 本端实读 | 判定 |
|---|---|---|---|
| `Add` | `:177` | `:177` ✔ | 相符 |
| `TakeOut` | `:200` | `:200` ✔ | 相符 |
| `RestoreContents` | `:319` | `:319` ✔ | 相符 |
| `Clear` | `:326` | `:326` ✔ | 相符 |
| `TrimToCapacity` | `:344` | `:344` ✔ | 相符 |
| `Harvest` | `:256`（total>0） | `:256`（`if (total > 0)`）✔ | 相符 |

（另：`TotalCount`／`UsedSpace` 为现算属性、无缓存无脏标记 —— 与任务书一致 ✔）

---

## 二、件 2 · 读口调用方分类（本端独立复核 · 函数级 ＋ 调用面两列）

> 扫描面＝全库 `Assets/**/*.cs`（生产码 `_Game` ＋ Editor 容器）；「调用方」栏只列**生产码**，Editor 命中另注。

### 2.1 `IWarehouse` 接口面（5 方法）

| 函数 | 调用面（生产码 · `file:line`） | 判定 |
|---|---|---|
| `Query()` | `WarehouseHelper.cs:115`（`TryCheckEnough`）／`:131`（`LockTakes`） | **有调用方（2 处）** ✔ 与任务书相符 |
| `CanTake(type,amt)` | **全库 0 命中**（`.CanTake(` 无任何调用） | ⭐ **备而未用**（**任务书未列 · 本端新发现**） |
| `Take(type,amt)` | `WarehouseHelper.cs:148`（`ApplyTakes`）／`AIEconomySettlement.cs:63`／`RulerController.cs:251` | 有调用方（3 处） |
| `Deposit(type,amt)` | `RulerController.cs:251`／`:378` | 有调用方（2 处） |
| `Transform(in,out,amt)` | `BlacksmithBuilding.cs:58` | 有调用方（1 处） |

> ⚠️ `CanTake`（`09` §7.2「先问后扣」的**第一问**）实现齐备但**零调用** —— 现役「先问」由 `WarehouseHelper.TryCheckEnough/LockTakes` **改走 `Query()` 逐条目求和**实现 ⇒ 接口面的 `CanTake` 是**备而未用**。**不预优化、不删**（§2.3）。

### 2.2 `StorageComponent` 面（非接口读口）

| 函数 | 调用面（生产码 · `file:line`） | 判定 |
|---|---|---|
| `TotalCount` | `Building.cs:1044`／`AIEconomySettlement.cs:49`／`ScheduleCenterStub.cs:119,132,150`／`TaskScheduler.cs:709`／内部 `:242` | 有（5 文件） |
| `UsedSpace` | `BuildingPanel.cs:185,186,426`／`KingdomBrain.cs:477`／内部 `:105`（`FreeSpace`） | 有（3 文件） |
| `FreeSpace` | **0 处**（仅 M1-A Editor 探针 `Valley_HH313_M1A_WarehouseProbe.cs:69`） | ⭐ **备而未用** ✔ 与任务书相符 |
| `IsFull` | `BlacksmithBuilding.cs:47,89`／`TaskScheduler.cs:765,894` | 有（2 文件） |
| `IsFullFor(type)` | `Building.cs:1034`／`ProducerComponent.cs:86`／`MineByproductComponent.cs:103`／`SiegeWorkshopBuilding.cs:139` | 有（4 文件） |
| `CanAccept(type)` | `TaskScheduler.cs:822`／`SiegeWorkshopBuilding.cs:145`／内部 `Add`/`IsFullFor`/`Transform` | 有（2 文件＋内部） |
| `Contents` | `Building.cs:720`（存档写口）／`BuildingPanel.cs:426`（UI 文本） | 有（2 文件） |
| `GetAmount(type)` | `Building.cs:1047`／`TaskScheduler.cs:712,774,781`／`MineByproductComponent.cs:108,120,137-139,202`／`AIEconomySettlement.cs:113`／`WarehousePanel.cs:131,133`／`SiegeWorkshopBuilding.cs:161,200`／`RulerController.cs:250,252,310,371` 等 | 有（多文件） |
| `SumByPrefix(prefix)` | **0 处**（仅 M1-A Editor 探针 `:107-111`） | ⭐ **备而未用** ✔ 与任务书相符 |
| `PrimaryStoredType()` | `Building.cs:1046`／`ScheduleCenterStub.cs:150`／`TaskScheduler.cs:710`／内部 `:269`/`:294` | 有（3 文件） |
| `Accepts(type)` | `TaskScheduler.cs:703,822,895`／`WarehousePanel.cs:129`／内部 `:61` | 有（2 文件） |
| `DeclaredPaths` | **0 外部**（仅内部 `Accepts` 用） | 备而未用（内部辅助） |
| `IsReadyToHarvest()` | `BuildingPanel.cs:187,394`／`ScheduleCenterStub.cs:100,118` | 有（2 文件） |
| `GetCarryAmount()`／`(type)` | `ScheduleCenterStub.cs:131`／`TaskScheduler.cs:711`／内部 `HarvestCarry` | 有（2 文件） |
| `HarvestCarry()` | `BehaviorExecutor.cs:175`／`TaskScheduler.cs:594` | 有（2 文件 · ⏭️ `M1-G` 待删） |
| `capacity`（字段直读） | `BuildingPanel.cs:185,186,426`／`WarehousePanel.cs:124,131,133`／`Building.cs:1044`／`MineByproductComponent.cs:108,120` | 有（4 文件） |

### 2.3 「备而未用」项结论（任务书 (b)）

| 项 | 结论 | 理由 |
|---|---|---|
| `SumByPrefix()` | **不预优化、不删** | `09` §7.5-② 的**配套读法**（前缀匹配机制的读侧对偶）；判据 4「前缀可读」的落点 |
| `FreeSpace` | **不预优化、不删** | `09` §7.5-④「件数与占用两口径」的**派生读数**；与 `UsedSpace` 同算式，删它会让调用方各自算（口径分叉源） |
| `CanTake()` | **不预优化、不删** | `09` §7.2「先问后扣」的**第一问**，是跨仓凑单的接口面契约；`M2`/`M5` 转移动作落地时会用到 |

---

## 三、件 3 · ⚠️ DZ-1 前提不成立（本端未改码 · 报裁）

### 3.1 任务书原判

> 成因：容量项 `t.cap + storage.capacity` 在**资源类型循环内**逐次累加，同一仓的 `capacity` 被按接受的资源类型数**重复计入** ⇒ 通用仓 `res` 容量列虚增（接受 N 类即虚增 N 倍）。
> 修法：容量应与资源类型解耦 —— 循环外取一次 `capacity`，循环内只累加 `stored`。

### 3.2 实读（[WarehousePanel.cs:109-159](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/UI/WarehousePanel.cs#L109-L159)）

```csharp
var totals = new Dictionary<ResourceType, (int stored, int cap)>();   // ← 键 ＝ 资源类型
...
foreach (var type in ResourceCatalog.AllTypes)                        // :127
{
    if (!storage.Accepts(type)) continue;                             // :129
    if (totals.TryGetValue(type, out var t))
        totals[type] = (t.stored + storage.GetAmount(type), t.cap + storage.capacity);  // :131
    else
        totals[type] = (storage.GetAmount(type), storage.capacity);   // :133
}
```

### 3.3 为什么不成立（三条）

1. **同一键不可能被累加多次**：`totals` 以 `ResourceType` 为键，而 `type` 是**外层循环变量**、`ResourceCatalog.AllTypes` 内**每型恰出现一次** ⇒ 对任一 `(storage, type)` 对，`:131` 分支**最多命中一次** ⇒ 单行容量 = Σ(能装该型的各仓 capacity)，**不是** N 倍。
2. **"N 倍"只在跨行读数下才出现，而那是设计本意**：通用仓的 capacity 确实落入 **N 个不同资源行**（每行恰一次）；`09` **§六 账本**明载 —— 「**行 ＝ 「仓库 × 资源」**（容量共用 ⇒ 每仓一条容量线 ⇒ **行必须能定位到具体仓**）」⇒ **同仓容量出现在多行是 09 明文要求**，不是虚增。
3. **任务书给的修法数值等价（no-op）**：把 `capacity` 提到循环外（`int cap = storage.capacity;`）后，`:131` 变成 `t.cap + cap` —— 与 `t.cap + storage.capacity` **逐字等价**（循环内无任何写 `capacity` 的路径）⇒ 落码也**不会**改变任何读数。

### 3.4 本端处置（按用户裁决）

**不改码**。⇒ 请策划端裁定：**(A) 认定非缺陷·关 DZ**（附本 §3.3 证据）；或 **(B) 改述缺陷** —— 若你要的是「面板改按 09 §六 的 **行 ＝ 仓库 × 资源** 逐仓成行」，那是**重构**（改显示模型）不是 bugfix，请另立批次并给 UI 验收口径。

### 3.5 附带复核（任务书勘正要点）

「**本缺陷与事件订阅无关** · `SubscribeAll`（`:75-91`）是按**建筑**订阅」⇒ ✔ **实读相符**：`SubscribeAll:79-90` 遍历 `BuildingRegistry.Instance.All`，每栋 `GetComponent<StorageComponent>()` 订阅一次，⛔ 无"按资源订阅 N 次"问题。**本片未动订阅逻辑** ✔。

---

## 四、件 4 · ⚠️ DZ-2 前提不成立 ＋ 真缺口 1 条（本端未改码 · 报裁）

### 4.1 任务书原判

> `RestoreContents`（`StorageComponent.cs:305`）**直接覆盖 `_items`** 并发事件，**不校验 `capacity`、不 Trim**。
> 须判：读档路径是否需要 `TrimToCapacity`（`:333`）兜底。

### 4.2 实读（[StorageComponent.cs:304-320](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Building/StorageComponent.cs#L304-L320)）

```csharp
public void RestoreContents(ResourceList contents)
{
    _items.Clear();
    if (contents.items != null)
        for (int i = 0; i < contents.items.Length; i++)
        {
            var e = contents.items[i];
            if (e.amount <= 0) continue;
            int added = Add(e.type, e.amount);        // ← 走 Add ⇒ 标签校验 ＋ 容量校验 ＋ clamp
            if (added < e.amount)
                Debug.LogWarning($"[StorageComponent] 读档 {e.type} 超容量 clamp {e.amount}→{added}");
        }
    OnStorageChanged?.Invoke(this);
}
```

- `Add`（`:169-179`）：`Accepts` 标签校验 → `CanAccept` 容量校验 → `Mathf.Min(amount, can)` **clamp**。
- 方法自身的文档注释亦自陈：「**clamp 到容量线内**，防旧档/异常值越界」。
- ⇒ **结论：读档路径不需要 `TrimToCapacity` 兜底** —— 逐条目入仓已保证 `UsedSpace ≤ capacity`（体积 0 资源如金按 §4.2 设计**不占容量**，属豁免非漏算）。
- ⇒ **任务书"直接覆盖 `_items`、不校验 capacity"与磁盘实读不符**（覆盖 `_items` 的是 `Clear()`，随后每条都走 `Add`）。

### 4.3 改前对照（⚠️ 本缺口系 **M1-A 引入** · 本端如实自陈）

| 版本 | 读档恢复写法 | clamp? |
|---|---|---|
| 改前（`9656cf69^`） | `storage.storedAmount = Mathf.Max(0, data.storedAmount);`（`BuildingFactory.cs` 原文） | ⛔ **不 clamp**（只挡负数） |
| 本片前（M1-A 后） | `storage.RestoreContents(data.storageContents)` → 逐条 `Add` | ✅ **clamp** |

⇒ **"读档 clamp" 是 M1-A 新增行为**，而 M1-A 交付报告 §五-2「结构强制的行为变化」**未列此项** ⇒ 本端**如实补报**（属报告遗漏，非隐瞒）。

### 4.4 ⭐ 真缺口（本端新查出 · 同一题面 · 方向相反）

**读档时 `capacity` 未随 `level` 刷新 ⇒ 存量被"过度 clamp"丢弃。**

| 证据 | 实读 |
|---|---|
| `BuildingFactory.SpawnFromSave` 顺序 | `:328` `b.grade = data.grade` → `:329` `b.ApplyDef()`（⛔ **不刷新 capacity**）→ `:332` `b.level = data.level` → **`:336` `storage.RestoreContents(...)`** |
| `Building.LoadState` 顺序 | `:757` `level = Mathf.Max(1, data.level)` → `:768` `ApplyDef()` → **`:783` `storage.RestoreContents(...)`** |
| `RefreshCapacity` 全部调用点 | 仅 3 处：`StorageComponent.Init:41`（此时 `level`＝1）／`Building.cs:514`（**升级完成**时）／`TreasureVault.cs:71` |
| 容量随等级 | `RefreshCapacity:72-74` ＝ `def.producer.capacity × b.LevelScale()`；`LevelScale:401-409` 连乘 `levels[].statScale` |
| 实例读数 | `Granary`：`producer.capacity = 60`，`levels[].statScale = 2 / 2` ⇒ `LevelScale(Lv3) = 4` ⇒ **Lv3 容量应 240**；读档时 capacity 仍为 **60**（Lv1 档）⇒ 存档中 > 60 的粮**被 clamp 丢**＋`LogWarning` |

⇒ **可复现路径**：把 Granary 升到 Lv3、存粮 > 60、存档 → 读档 ⇒ 存量回落 60。
⇒ **性质**：`M1-A` 引入的 clamp **＋** 既有的「读档不刷容量」⇒ 两者叠加成**真丢数据**。
⇒ **修法（本端未落 · 待裁）**：落点选 **调用方**（`BuildingFactory.cs:336` 与 `Building.cs:783` 的 `RestoreContents` **之前**补一次 `storage.RefreshCapacity()`）。
　**依据**：①`RefreshCapacity` 依赖 `Building.level`／`grade`，两者均由**调用方**在 restore 前才恢复 ⇒ 放在 `RestoreContents` **内部**会读到**尚未恢复**的 level（仍错），故**必须落在调用方**；②不动 `RestoreContents` 签名与语义 ⇒ 影响面最小。

### 4.5 本端处置（按用户裁决）

**不改码**（避免擅自引入行为变化）⇒ 请策划端裁定：**(A) 本端按 §4.4 修法补修**（行为变化：读档不再丢存量）；或 **(B) 另立 DZ** 归后续批（如 `M1-C` 建造走仓批）；或 **(C) 认定不修**（须给口径：读档超容量该丢还是该保留）。

---

## 五、逐条勘正对照（任务书自述 vs 本端实读）

| # | 任务书自述 | 本端实读 | 判定 |
|---|---|---|---|
| 1 | `Query()` 有真调用方（`WarehouseHelper.cs:115`／`:131`） | `WarehouseHelper.cs:115`（TryCheckEnough）／`:131`（LockTakes） | ✅ **逐字相符** |
| 2 | `SumByPrefix()` 0 外部调用方 | 生产码 0；仅 M1-A Editor 探针 | ✅ 相符 |
| 3 | `FreeSpace` 0 外部调用方 | 生产码 0；仅 M1-A Editor 探针 | ✅ 相符 |
| 4 | 「其余读口均有真调用方」 | ⚠️ `CanTake()`（接口面）与 `DeclaredPaths`（内部辅助）**亦 0 外部调用方** | ⚠️ **不完整**（本端补列） |
| 5 | `TotalCount:83`／`UsedSpace:94` 现算·无缓存无脏标记 | `:83`／`:94` 现算，无缓存字段 | ✅ 相符 |
| 6 | `OnStorageChanged` 全部写口 Invoke（177/200/319/326/344/256） | 六处**逐行相符**（含 `Harvest` 的 `total > 0` 守卫） | ✅ 相符 |
| 7 | 件 1(c) `FreeSpace:105` 与 `UsedSpace` 同算式双出口 | `:105` `capacity - UsedSpace` | ✅ 相符（本片未加缓存 ⇒ 无分叉） |
| 8 | 件 3 位置 `:109-159`／`:127`／`:129`／`:131` | 行号**逐行相符** | ✅ 位置相符（**成因判定不成立**，见 §三） |
| 9 | 件 3「与事件订阅无关 · `SubscribeAll:75-91` 按建筑订阅」 | `:79-90` 遍历建筑、每栋订阅一次 | ✅ 相符 |
| 10 | 件 4 `RestoreContents:305` **直接覆盖 `_items`、不校验 capacity、不 Trim** | 逐条目走 `Add` ⇒ **已校验＋clamp＋LogWarning**；方法注释自陈 clamp | ❌ **不成立**（见 §4.2） |
| 11 | 件 4 落点 `BuildingFactory.cs:336`／`:339` | 行号相符（`:336` storage／`:339` vault） | ✅ 相符 |
| 12 | 件 4 `TrimToCapacity` 在 `:333` | `:333` | ✅ 相符 |
| 13 | 件 4 替换理由「原项不存在 ⇒ 换成 `RestoreContents:305` 的真缺口」 | 该"缺口"**亦不存在**（且 §4.4 另查出真缺口，方向相反） | ❌ **替换理由不成立** |

---

## 六、⛔ 阻断与未完成项（显式列出）

| # | 项 | 状态 | 详情 |
|---|---|---|---|
| 1 | **编译 0 error 读数** | ⛔ **未取到** | ①本会话 **Unity MCP 不可用**（MCP 文件系统仅 `integrated_code_mode` ＋ `Computer_Use`，无 `mcp_unity-bridge`／`mcp_unityMCP`）；②改走**团结引擎 batchmode** 兜底：`Tuanjie.exe -batchmode -nographics -quit -projectPath … -logFile _hh314_compile.log` ⇒ **被授权阻断**（日志原文：`Unable to update licenses … Cannot save ULF license file: Access to the path 'C:\ProgramData\Tuanjie\Tuanjie_lic.ulf' is denied`）⇒ Unity 在包注册后即退出，**未进入脚本编译**；③旁证：`Library/ScriptAssemblies/Assembly-CSharp.dll` 时间戳仍为 **2026-09-19 23:33:53**（M1-A 末次编译），**未更新**。⚠️ **本片唯一代码改动＝两处 XML 文档注释**（`StorageComponent.cs` 事件注释、`IWarehouse.cs` remarks），无签名/无逻辑变化 ⇒ 编译风险极低，但**读数未取到即如实记**。 |
| 2 | **同局冒烟读数** | ⛔ **未取到** | 任务书 §二要求「起局 → 建造挂仓建筑 → 产出/搬运入仓 → 开面板看读数 → 存读档 → 复查读数」。冒烟须走 `HH.92` 正门 `TestHarnessApi.EnterTestRun`（`test-harness-first` 红线）⇒ 需 **Unity 编辑器 Play Mode ＋ 驱动通道**，而本会话 **MCP 不可用、batchmode 被授权阻断** ⇒ 无法进局。**未以 Edit Mode 探针冒充冒烟**（避免 `L-34` 白跑/口径混淆）。 |
| 3 | 件 3 落码 | **未落**（前提不成立） | 按用户裁决「停下报裁·不改码」 |
| 4 | 件 4 落码 | **未落**（前提不成立） | 同上；真缺口见 §4.4，待裁 |
| 5 | 本片未 commit 的读数类产物 | — | 无（本片只改 2 个 `.cs` 注释 ＋ 本报告） |

**解除阻断需要（二选一）**：**(a)** 在本机打开团结引擎编辑器（并启用 Unity MCP）⇒ 本端即可取「编译 0 error ＋ 同局冒烟」两项读数；**(b)** 以管理员权限解决 `C:\ProgramData\Tuanjie\Tuanjie_lic.ulf` 写权限 ⇒ batchmode 可编译（但 Play Mode 冒烟仍需 (a)）。

---

## 七、红线遵守自检

| 红线 | 状态 |
|---|---|
| ⛔ 不碰 `GridTypes.cs` | ✅ 未碰 |
| ⛔ 不碰地图四门 | ✅ 未碰 |
| ⛔ 不写存档迁移脚本 | ✅ 未写（§4.4 的修法**也未落码**） |
| ⛔ 不写双轨开关 | ✅ 无任何 `useXxx` |
| ⛔ 不碰训练仓 | ✅ 未碰 |
| ⛔ 不碰并行会话未提交改动（美术／pixel-forge／GameScene／Packages／3.6·3.8 doc） | ✅ 未碰 |
| ⛔ 不碰四档账本 | ✅ 未碰（`DZ` 入册与否由策划端定） |
| 判据三直读 | ✅ 设计稿（`09` §六／§7.5／§5.2）＋ 代码 `file:line` 实读 ＋ 档位字段（`Granary.asset` `statScale=2/2`）三面齐 |
| 提交纪律 | 具名 `git add`（禁 `-A`/`-u`/`.`）；`-m` 内无反引号；不 push |
| 行尾 | 改动前先验：两文件均 **纯 LF / 无 NUL / 无 BOM** ⇒ 可用 Edit |
