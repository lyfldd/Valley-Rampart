# HH.319 · `M1-G-1b`（件 1 `#42` ＋ 件 5 `Q1`）· 交付验收裁决（`D826` · 2026-09-22）

> 被验：`5e7a6c6d`（**5 生产文件** · 编译 0 error 自陈 · ⛔ **未跑局**）
> 报请：执行端《开工回执（4 行）》
> 依据：`D824`（`e886d6e7` §一-1 案 (c) ／ §一-2 ／ §一-3）· `D825`（`9dc42308` §六 续批表）
> ⚠️ 红线：本批 ⛔ 零 `AI.Core` 改动（`Q3` 甲案）· ⛔ 未 push · `Assets` 唯一已跟踪改动 ＝ `GameScene.unity`（`O-14` 既有脏点 ✓）

---

## 一 · 总判

⭐ **件 1 ／ 件 5 代码面通过 · 条件通过**（本端实证 **17 项** ＋ **7 项新发现**，其中 **3 项本端已裁为必修**）。

⛔ **件 C（判据 3 收口）／ 件 D（探针）／ 件 E（跑局＋回归）／ 件 F（`_transporting`）未落** ⇒ ⛔ **判据 1/2/3/4/6/7/8/9/10 一条未验 ⇒ ⛔ 本批不判绿**。
✅ 停手理由成立（预算耗于两条行尾异常文件的 python 二进制补丁 ＋ 件 5 的位置敏感插入）｜⚠️ **"不为充数而草写报告"的取舍正确**。

---

## 二 · 实证（17 项 · 逐点已复核）

### 2.1 改动面与行尾（4 项）

| # | 项 | 判 | 依据 |
|---|---|---|---|
| ① | 改动面 ＝ **5 生产文件** | ✅ | `GameEvents.cs`／`TaskScheduler.cs`／`Building.cs`／`BuildingPanel.cs`／`UnitController.cs`（⚠️ `GameEvents.cs` 实际在 **`_Game/Core/`**，⛔ 非 `_Game/Systems/Events/`） |
| ② | `Assets` 已跟踪改动 | ✅ | 仅 `Assets/Scenes/GameScene.unity`（＝ `O-14` 既有脏点 · ⛔ 非本批） |
| ③ | `GameEvents.cs` 行尾 | ✅ | `git ls-files --eol` ⇒ **`i/lf w/crlf`**；实测 `CRLF=682 / LF=682` ⇒ **全 CRLF · 追加行保留 CRLF** ✓（你自陈准确） |
| ④ | `UnitController.cs` 行尾 | ✅ | `i/lf w/mixed` ⇒ **保持 mixed** ✓（⛔ 未破坏原混合面）｜余 3 文件纯 LF ✓ |

### 2.2 件 1 `#42`（4 项）

| # | 项 | 判 | 依据 |
|---|---|---|---|
| ⑤ | 事件结构 | ✅ | `GameEvents.cs:675-682` `public readonly struct UnitOccupationChangedEvent { unit／from／to }` ⇒ ⭐ 与 `EventBus.Publish<T>(T) where T : struct`（`EventBus.cs:76`）相容 ✓ |
| ⑥ | 单点发布 | ✅ | `UnitController.cs:86-92` ⇒ `EventBus.Publish(new UnitOccupationChangedEvent(this, from, occ))` ✓（全库引用 **3 处 ＝ 定义 ×2 ＋ 发布 ×1**） |
| ⑦ | 命名空间可达 | ✅ | 两文件**均无 `namespace` 声明**（全局命名空间）· `EventBus` 在 `_Game/Core/EventBus.cs` · `Occupation` 在 `_Game/Data/UnitData.cs` ⇒ 同一编译单元内直取 ✓（⛔ 无需 `using`） |
| ⑧ | **零订阅方** | ✅ | 全库 `UnitOccupationChangedEvent` 命中 **3 处**（定义 2 ＋ 发布 1）⇒ ⛔ **无 `Subscribe`** ⇒ 备而未用（⚠️ 见 §三-⑦） |

### 2.3 件 5 `Q1` 案 (c)（6 项）

| # | 项 | 判 | 依据 |
|---|---|---|---|
| ⑨ | 标记与内部 set 口 | ✅ | `Building.cs:1486 private bool _forceHaulOnce;` ＋ `:1489 public void RequestForceHaulOnce()` ✓ |
| ⑩ | ⭐ **读清在两条早返回之前** | ✅ | `:1497-1498`（`bool forceHaul = _forceHaulOnce; _forceHaulOnce = false;`）**早于** `:1502 _demolishing` 与 `:1512 state != BuildingState.Active` ⇒ ⭐ **裁定钉死项完全落地** ✓ |
| ⑪ | 分支次序 | ✅ | `:1525-1536` forceHaul 分支**在 ②搬水(`:1553`)/③生产(`:1578`)/④搬运(`:1590`) 之前** ⇒ 先判 ④ 语义达成 ✓ |
| ⑫ | 与 ④ 同构 ＋ 跳阈值 | ✅ | `destType = KingdomDestType.NearestWarehouse`／`ScaleTaskArgs{resourceType, totalResourceDemand}`／`PrimaryStoredType()`／`GetAmount` 与 `:1590-1600` **逐一对应**；⛔ **未含** `stored >= capacity * transportThreshold`；✅ 仍受 `capacity > 0 && TotalCount > 0` 前置 ✓ |
| ⑬ | 重载与投递 | ✅ | `TaskScheduler.cs:376-387` `RequestHaulNow(StorageComponent)` ⇒ `GetComponentInParent<Building>()` ＋ `b.RequestForceHaulOnce()` ＋ `RequestHaulNow((ITaskSource)b)` ⇒ 复用 `Tick` 去重／规模派工（`:364-369`）✓ |
| ⑭ | 随动改动 | ✅ | `BuildingPanel.cs:413` 改调新重载 ✓｜`:188` 文案「收取」⇒「**派搬运**」✓ |

### 2.4 ⭐ 过载绑定安全（2 项 · 本端主动核）

| # | 项 | 判 | 依据 |
|---|---|---|---|
| ⑮ | **无静默改绑** | ✅ | `ChestEntity.cs:18 public class ChestEntity : MonoBehaviour, IInteractable, ITaskSource` ⇒ ⭐ **⛔ 非 `StorageComponent` 子类**（持 `:35 private StorageComponent _store`）⇒ `:166 RequestHaulNow(this)` 仍绑 **`ITaskSource`** 重载 ✓（⚠️ 否则点箱子会**退化走 `Harvest()` ⇒ 倒进玩家国库**） |
| ⑯ | `Tick` 可达性 | ✅ | `:291-294 foreach (var s in _sources) { if (s == null \|\| !s.IsValid) continue; if (!s.TryAdvertiseTask(out var task)) continue; …}` ⇒ 注册后**必然被广告** ✓ |

### 2.5 件 F 残留复核（1 项）

| # | 项 | 判 |
|---|---|---|
| ⑰ | `ScheduleCenterStub._transporting` | ✅ 字段仍在（`:41`）· `IsTransporting` 已删（`:87` 墓碑注自陈）⇒ 与报告自陈一致 ⇒ **件 F 待办** ✓ |

---

## 三 · ⭐ 本端新发现（7 项 · 执行端未提）

### ① ⭐⭐ `BuildingPanel` 的**启用判据仍是旧口径** ⇒ 与动作**口径分裂**（须改）

```
:189  _harvestButton.SetEnabled(storage.IsReadyToHarvest());      ← 「有【可入国库】的内容」
:409  if (storage == null || !storage.IsReadyToHarvest()) return; ← 同上
:188  文案 ＝「派搬运」；动作 ＝ RequestHaulNow(storage) → Transport 任务 → 【最近可用仓】
```
⭐ `IsReadyToHarvest()`（`StorageComponent:278-283`）＝ **逐资源问 `CanRulerAccept`（玩家国库收不收）** ⇒ 与"搬进**仓库**"**不是同一口径**。
⚠️ **后果**：**纯水井仓按钮变灰 ⇒ 玩家无法手动派搬运**（水不入国库但**可入仓**）。
⚠️ 且 `:407-409` 的注释自陈「**纯水井仓自动变灰（正确处理 —— 收了会转箱空转）**」—— ⭐ 那是**旧动作（瞬间入国库）的理由**，动作已改 ⇒ **理由失效**。

⇒ ⭐ **本端裁定（件 G-1）**：`:189` 与 `:409` 判据改 **`storage.TotalCount > 0`**（＝"仓里有东西可搬"，与动作同口径）＋ ⛔ **不再用 `IsReadyToHarvest`** ＋ `:407-409` 注释重写。
　⚠️ 「无任何可用仓」的失败由**任务层**承担（`Unreachable`／`DestFull` ⇒ 已有 `Abandon` 出口 ＋ 日志）⇒ ⛔ 本批**不**在 UI 加"可用仓"前置（若探针观测该路径频繁 ⇒ 再议）。

### ② ⭐⭐ 一次性标记**被消费时不保证当次真派发** ⇒ 点击可能**静默丢失**（`O-18`）

```
TaskScheduler.Tick:
  :297  if (RemainingSlots(task) <= 0) continue;            ← Transport 容量已满 ⇒ 任务被丢（标记已清）
  :303  if (jobs.Count == 0 || idle.Count == 0) { UpdateAssignedTasks(); return; }   ← ⭐ 无空闲工人 ⇒ 早退
  :335  if (best < 0) break;                                 ← 无同国空闲工人 ⇒ 该任务等下 tick（标记已清）
```
⭐ 标记在 `TryAdvertiseTask` **入口**即清（`:1497-1498`）⇒ ⚠️ **上述三种情形下标记已消耗、任务未派**。
⚠️ **玩家可见后果**：**仓存量 < `transportThreshold`（80%）时点击可能静默无响应，且不会自愈**（④ 的阈值判据仍不满 ⇒ 不再广告）。⚠️ 仓位 ≥ 80% 时由 ④ 兜底 ⇒ 无害。

⇒ ⭐ **本端裁定**：**新立 `O-18`**（⭐ 归口"标记归属面"）＋ ⛔ **本批不修**（正确修法 ＝ 把标记**上移 `TaskScheduler`** 成 `_forceHaulSources` 集合、**派发成功（`Dispatch`）才清** ⇒ 属**新机制** ⇒ 另批议）｜⭐ **判据须给读数**：① 点击后是否派出 `Transport` ② ⭐ **"无空闲工人场景"下点击的读数**（本条是本批最关键的缺口读数）。

### ③ ⚠️ `st.Harvest()` 兜底 ＝ `#40` 刚删的**国库旁路**＋「箱＝仓」陷阱（须改）

```
TaskScheduler.cs:380-383   var b = st.GetComponentInParent<Building>();
                          if (b == null) { st.Harvest(); return; }   ← ⚠️ 走【写死玩家国库】
```
⚠️ 两问题：① `Harvest()`（`StorageComponent:288`）**写死玩家国库** —— 正是 `#40`（`D824` §一-4）要清的老路径 ⇒ **兜底把旁路又接回来了**；② 注释自称"兼容「箱＝仓」"，⚠️ 但**箱**（`ChestEntity` 自挂 `_store`）`GetComponentInParent<Building>` **返 null** ⇒ ⭐ **会把箱内容直接倒进玩家国库**（比"不做事"更糟）。
✅ **当前不可达**（`BuildingPanel` 路径 `storage = _target.GetComponent<StorageComponent>()` ⇒ 父 `Building` 必非 null）⇒ 属**备而未用的陷阱**。

⇒ ⭐ **本端裁定（件 G-2）**：**改为 `Debug.LogWarning("[TaskScheduler] RequestHaulNow: 无父建筑，忽略") ＋ return`**（⛔ **不落国库**）。

### ④ ⚠️ `TryAdvertiseTask` 的 **doc 归属错位**（须改）

```
:1473-1484  /// <summary> 按建筑类型声明任务 …②搬水／③生产／④有存储且存量 ≥ capacity×transportThreshold → Transport… </summary>
:1485       // ⭐ M1-G-1b 件5 …一次性强制搬运广告标记 ＋ 内部 set 口。
:1486       private bool _forceHaulOnce;          ← ⚠️ 上面那段 doc 现在【挂到本字段】上
:1489       public void RequestForceHaulOnce() { … }
:1491       public bool TryAdvertiseTask(out KingdomTask task)   ← ⚠️ 本节【失去了 doc】
```
⚠️ C# XML doc 注释**附着到下一个声明** ⇒ 段方法的分支次序说明**被挂到字段上**、方法本体失 doc；⚠️ 且该方法 doc 现**未含新的 ⓪ forceHaul 分支**（它已排在 ② 之前）。

⇒ ⭐ **本端裁定（件 G-4）**：把 `_forceHaulOnce`／`RequestForceHaulOnce` **移到该 `/// <summary>` 之前**（或方法之后）⇒ 恢复 doc 归属；＋ ⭐ 在该 doc 内**补一条 ⓪ 分支**（"玩家手点一次性强制搬运 ⇒ 先判 ④ 并跳阈值"）。

### ⑤ ⚠️ `IsReadyToHarvest` 的 docstring 消费者清单**过时**（须改）

`:273-275` 仍列「消费者（`ScheduleCenterStub:100/:118` 清理与派发 · `BuildingPanel:187/:405` 按钮启用与守卫）」⇒ ⚠️ **`ScheduleCenterStub` 那两支已随件 4 删除**（`:87` 墓碑注自陈）⇒ 须随件 C 同步。

### ⑥ ⚠️ `AIDebugSpawnController` **路径须勘正**（`L-64`）

`D825` §三-3 只写了文件名 ⇒ 实际路径 ＝ **`Assets/_Game/Systems/AI/AIDebugSpawnController.cs:432`**（⚠️ ⛔ 非 `_Game/Systems/AI/Debug/`）⇒ 落账勘正（行号 `:432` 本身**正确** ✓）。

### ⑦ ⭐ **幂等守卫顺带改掉了"钉住"副作用**（须改）

```
原：  public void SetOccupation(Occupation occ) { _runtimeOccupation = (int)occ; }   ← 无条件【钉住】
现：  var from = EffectiveOccupation;
      if (from == occ) return;            ← ⚠️ 早返回【同时跳过】_runtimeOccupation 写入
      _runtimeOccupation = (int)occ;
```
⭐ `_runtimeOccupation`（`UnitController:81` `-1 = 未设置，回退 Data.occupation`）的语义是「**运行时覆盖 ⇒ 不再跟随 `Data`**」。⚠️ 新增的早返回使得：当 `_runtimeOccupation == -1` 且 `occ == Data.occupation` 时 **不再钉住** ⇒ ⚠️ 若 `Data.occupation`（**共享 `UnitData` SO**）此后变化 ⇒ **本次"设置"被静默抹掉**。
⚠️ 严重度**低**（仅该窄条件）但**性质是"改一个副作用时连带改了另一个既有副作用"** ⇒ 与 `L-75`（改出口须回查上游）同族（回归副作用面）。

⇒ ⭐ **本端裁定（件 G-3）**：**写入保持无条件，仅对"事件发布"做幂等**（1 行次序调整）：
```
var from = EffectiveOccupation;
_runtimeOccupation = (int)occ;          // ⭐ 原副作用【保留】⇒ 与原行为逐位一致
if (from == occ) return;                // ⭐ 仅事件幂等（⛔ 不重复发布）
EventBus.Publish(new UnitOccupationChangedEvent(this, from, occ));
```

### ⑧ 说明 · 「9 处调用面」口径（本端复核）

`SetOccupation` 全库 **39 处** ⇒ 其中 **Editor 探针 ~30 处**、**生产面 9 处**（`KingdomBrain:991`/`1019`／`TrainingSystem:100`/`141`／`AbstractEconomySettlement:112`／`KingdomFoundry:396`／`PopulationSystem:506`/`532`／`VagrantCampSystem:251`）⇒ ✅ **`D823` 立册的"9 处"＝ 生产面** ✓｜⭐ **单点发布形态本端准**（"不可能漏"优于逐处接线）⇒ ⚠️ 但见 §四-1。

---

## 四 · 待补项（`D824` 要求但本批未给）

1 ⚠️ ⭐ **9 处逐处标"是否需写"（含"成长/招募"3 处单列）** —— `D824` §一-1 明确要求。你用**单点发布**回避了逐处接线（形态本端认可 ✓），⚠️ **但"逐处标注"的实质问题是**：单点 ⇒ **`PopulationSystem:506`（`Child⇒Resident`）／`:532`（`⇒Worker`）／`VagrantCampSystem:251`（`Vagrant⇒Resident`）这三处"成长/招募"也会发同一事件**。
　⇒ ⭐ **裁定**：① ✅ **准单点形态** ② ⭐ **须在事件 docstring 明写"覆盖全部职业变更（含成长/招募）"**（⛔ 不得让订阅方误以为"只含训练转职"）③ ⚠️ **报告须列 9 处清单 ＋ 语义分组**（说明为何三处"成长/招募"也发）。
2 ⚠️ ⭐ **零订阅方 ⇒ 须说明"为何现在发"** —— `D824` §一-1 要求（你自陈"见交付报告 §A-②"，而**报告未落**）⇒ ⛔ **本项缺欠** ⇒ 件 D 报告须补。

---

## 五 · 裁定汇总（已定 · ⛔ 不再报选）

| # | 项 | 裁定 |
|---|---|---|
| 1 | 件 1 `#42` | ✅ **通过**（结构／单点／幂等／命名空间 ＋ §三-⑦ 的守卫位置修正） |
| 2 | 件 5 `Q1` 案 (c) | ✅ **通过**（读清位置／分支次序／同构＋跳阈值／重载／随动 全部落地） |
| 3 | 过载绑定 | ✅ **无回归**（`ChestEntity` 非 `StorageComponent` 子类） |
| 4 | **件 G-1** | ⭐ **必修**：`BuildingPanel:189`／`:409` 判据 ⇒ **`TotalCount > 0`** ＋ `:407-409` 注释重写 |
| 5 | **件 G-2** | ⭐ **必修**：`TaskScheduler:382` 兜底 ⇒ **`LogWarning + return`**（⛔ 不落国库） |
| 6 | **件 G-3** | ⭐ **必修**：`UnitController` ⇒ **写入无条件 · 仅事件幂等** |
| 7 | **件 G-4** | ⭐ **必修**：`Building.cs` ⇒ doc 归属修正 ＋ 补 ⓪ 分支 |
| 8 | **件 C** | 6 处旧描述注 ＋ ⭐ `IsReadyToHarvest` docstring 同步（§三-⑤）｜⛔ 豁免面**须逐条列名** |
| 9 | **件 D** | 探针扩展 ＋ ⭐ **`O-18` 读数**（"无空闲工人场景下点击"）＋ `DestFull` 计数 ＋ `Q1` 派出类型 ＋ `Q2` 时序 ＋ `·farm]` 逐帧 |
| 10 | **件 E** | 跑局 ＋ 四项回归（⚠️ 跑前归档 `L-68`/`L-70`）｜任一红 ⇒ **停手报裁** |
| 11 | **件 F** | `_transporting` 清理（⚠️ 须一并核 `GetPriority` 是否只为链 B 服务） |
| 12 | `O-17`／`O-18` | ⛔ **本批均不加机制** ⇒ 判据给计数／读数即可 |

---

## 六 · 落账

| 件 | 内容 |
|---|---|
| 裁决书 | 本文件（`多Agent交接/策划端/HH.319_M1-G-1b件1件5_验收裁决.md`） |
| 台账 | **§一百二十九** |
| `_编号登记.md` | `D826`（`HH.319` 行尾追加 · 沿用既例） |
| `_任务队列.md` | `M1-G` 行**只增不删** ＋ **新插 `O-18` 行**（NUL **1 守恒**） |
| `_当前快照.md` | `D826` |
| `_策划教训库.md` | 新立 **`L-76`**（「改动边界两查」：⛔ 不过头 · ⛔ 不漏项） |

---

## 七 · 下一步

⭐ **续批 ＝ `M1-G-1c`**：**A** 件 G（G-1~G-4 · 4 项必修）｜**B** 件 C（判据 3 收口）｜**C** 件 D（探针 ＋ `O-18` 读数）｜**D** 件 E（跑局＋回归）｜**E** 件 F（`_transporting`）｜**F** 交付报告（含 §四 两项待补）。
