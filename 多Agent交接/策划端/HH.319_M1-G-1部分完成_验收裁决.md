# HH.319 后接批 · `M1-G-1`（A 批）**部分完成**交付验收裁决

> `D825` · 2026-09-22 · 被验：`d9d7dd92`（**9 代码文件 ＋ 报告** · 编译 **0 error** · ⛔ **未跑局**）
> 本端实证 **16 项** ＋ **4 项新问题**（含 1 条执行端未提的**忙等循环**）＋ **2 条裁定**
> ⭐ **本批 ＝ 部分完成 · 条件通过** —— 件 2／3（主体）／4／6 落地**逐点全对**；⛔ **判据一条未验 ⇒ ⛔ 不判绿**

---

## 一 · 总判

| 项 | 判 |
|---|---|
| 改动面 | ✅ **9 代码文件 ＋ 报告**（`StorageComponent`／`TaskScheduler`／`ScheduleCenterStub`／`BuildingPanel`／`WorkerInventory`／`BehaviorExecutor` ＋ Editor 探针 3 处）｜工作区 `Assets` 无额外改动（除 `O-14` 既有脏点 ✓） |
| 编译 | ✅ **0 error**（执行端自陈 ⇒ 本端**未独立复编**，见 §五-3） |
| 件 2 `#41` | ✅ **逐点与 `D824` §一-2 一致**（含 `Take` 修为尽力档 ＋ `Min` 夹取 ＋ 余量竞态退回背包 ⇒ **比裁定更细**） |
| 件 3 `#40` | ✅ 主体落地（`HarvestCarry` ＋ 无参 `GetCarryAmount` ＋ `ExecuteCompletion` 兜底 三删 ＋ 落点统一 `Harvest()` ＋ 守卫语义墓碑注） |
| 件 4 `U-15` 根除 | ✅ 落地（`DispatchTransport` ＋ `IsTransporting` 整段删 ＋ `Update` 调用删 ＋ `BuildingPanel` 改读） |
| 件 6 `Q2` 出口 | ✅ 落地（去 `is ChestEntity` 限定） |
| ⛔ 件 1 `#42`／件 5 `Q1` 接线 | ⛔ **未落**（停手原因成立：预算耗于"删 `HarvestCarry` 致 5 处编译断裂"的调用面修复） |
| ⛔ 探针／跑局／回归 | ⛔ **全部未及** ⇒ ⭐ **判据 1/2/3/4/5/6/9/10 无一有读数** |
| **裁定** | ⭐ **件 2/3/4/6 ＝ 通过（代码面）**；⛔ **本批不判绿**；⭐ **`M1-G-1b` 续批**（件 1 ＋ 件 5 ＋ 探针 ＋ 跑局） |

⭐ **执行端的停手是**正确的 —— 它**如实标注**了"部分完成 · 判据一条未验 · 不判绿"，并把"删 `HarvestCarry` 造成编译断裂"作为停手直接原因**主动申报** ✓

---

## 二 · 逐件实证（16 项）

### 2.1 件 2 `#41`「先问后拿」

| # | 项 | 实读 |
|---|---|---|
| 1 | `UnloadInventory` 改 `bool` | ✅ `private bool UnloadInventory(NPCBrain, KingdomTask)`；`brain == null ⇒ return false`；`inv == null \|\| inv.IsEmpty ⇒ return true`（⚠️ **空背包 ＝ 不算失败** ⇒ 正确） |
| 2 | 先问后拿三步 | ✅ `best == null ⇒ return false`（留背包）／`can = best.CanAccept(type)`；`can <= 0 ⇒ return false`／`moved = inv.Take(type, Mathf.Min(can, inv.carriedAmount))`／`added = best.Add(type, moved)` |
| 3 | ⛔ 两处兜底已删 | ✅ 主支不再调 `AddGatherOverflow`（旧 `:902-903` 溢出 ／ `:908` 无仓 **双删**） |
| 4 | ⭐ **余量竞态防护**（**裁定外增量**） | ✅ `if (added < moved) { inv.TryStore(type, moved - added); Debug.LogWarning(…) }` ⇒ 余量**退回背包**（⛔ 不丢 · ⛔ 不入国库）｜⭐ 本端认可（**比裁定更严密**） |
| 5 | 副产支仍走台账 | ✅ 保留 `AddGatherOverflow(uc, type, taken0)` —— ⭐ **正确**（`Crystal`/`FireOil` 的 AI 落点就是台账；⛔ 非"兜底"）｜且**同步改先问后拿**（`inv.Take(type, inv.carriedAmount)` ⇒ 不再先清空）✓ |
| 6 | `WorkerInventory.Take` 尽力档 | ✅ `if (amt <= 0 \|\| IsEmpty \|\| carriedType != t) return 0;` ⇒ 去掉 `CanTake` 前置 ⇒ 与 `IWarehouse.Take`（`IWarehouse.cs:37`「**尽力档**」）契约一致 ✓ |
| 7 | `AbandonReason` 8 值 | ✅ `DestFull` 新增 ＋ `:68` 注释同改（"日志契约取值域（⭐ `M1-G-1` 起 8 值：新增 `DestFull`）"）✓ |
| 8 | 到达分支 | ✅ `if (!UnloadInventory(brain, task)) { stale.Add((id, AbandonReason.DestFull)); continue; }` ⇒ ⛔ **不 `Complete`**（防"货滞背包 ＋ 假成功"）｜⚠️ **但引入忙等 ⇒ 见 §三-1** |

### 2.2 件 3 `#40` / 件 4 `U-15` / 件 6 `Q2 出口`

| # | 项 | 实读 |
|---|---|---|
| 9 | `HarvestCarry()` 删除 | ✅ 整段删（原 `:360-371`）＋ ⭐ **墓碑注含两道守卫迁移论证**（`CanRulerAccept` ⇒ `FindNearestAvailable.Accepts(type)`；`TreasuryCanAccept` ⇒ `CanAccept <= 0`；＋ `Harvest()` 自留）✓ |
| 10 | 无参 `GetCarryAmount()` 删除 | ✅ ＋ 墓碑注（"其唯一调用面随链 B 一并删 ⇒ 零调用"）｜⚠️ 有参版保留 ✓ |
| 11 | `ExecuteCompletion` Transport 兜底删除 | ✅ `st` 变量 ＋ `st.HarvestCarry()` 双删；保留 `if (carryInv != null && !carryInv.IsEmpty) UnloadInventory(...)`｜⚠️ **返回值被忽略 ⇒ 见 §三-2** |
| 12 | `BehaviorExecutor` 落点统一 | ✅ `if (cmd.HarvestTarget != null) cmd.HarvestTarget.Harvest();` ＋ ⭐ `Inert` 说明（"生产路径上本块已不可达"）✓ |
| 13 | `ScheduleCenterStub` 整段删 | ✅ `DispatchTransport()`（原 `:89-152`）＋ `IsTransporting()`（`:155-158`）＋ `Update` 调用（`:66`）｜⭐ 迁移论证墓碑注（链 A 有阈值 vs 链 B 无阈值）✓ ｜⚠️ `DispatchCrew`／`AssignFollow` **保留** ✓ |
| 14 | `BuildingPanel` 改读 | ✅ 原 `FindObjectOfType<ScheduleCenterStub>()?.IsTransporting(storage)` ⇒ 改 `TaskScheduler.HasInstance && TaskScheduler.Instance.HasWorkerAssigned(_target)` ✓ |
| 15 | `LoadInventoryFromSource` 去限定 | ✅ `if (!inv.IsEmpty) UnloadInventory(brain, task);`（原 `is ChestEntity &&` 已去）✓ |
| 16 | Editor 探针同步 | ✅ 3 文件（`HH314_M1B`／`HH319_U15Probe`／`HH319_WaterAccount`）调用面已改（⛔ 但**注释/日志字符串仍含旧方法名** ⇒ 见 §四） |

---

## 三 · ⚠️ 本端新发现（4 项 · 执行端未提 1 项）

### 3.1 ⭐⭐ **`Abandon(DestFull)` 引入"忙等循环"**（新立 **`O-17`**）

```
到达分支： UnloadInventory 失败 ⇒ stale.Add(DestFull) ＋ continue ⇒ Abandon ⇒ 工人释放
      ⇒ ⭐ 源侧 Building.TryAdvertiseTask ④ 的判据「stored >= capacity × transportThreshold」**未变**（货没动）
      ⇒ 重新广告 ⇒ 再派 ⇒ 装载（背包可追加同类型）⇒ 又卸不掉 ⇒ 又 Abandon ⇒ ⭐ **循环**
```
⚠️ **注意形态**：不是"死锁"（每 tick 一次尝试），是 **忙等**；且背包会**越积越满**直到 `GetCarryCapacity` ⇒ ⭐ 之后转为 **`Complete` 循环**（`LoadInventoryFromSource:865 if (stored <= 0) return false`）。

**裁定**：
- ✅ **准 `Abandon(DestFull)`** —— 语义正确（⛔ 不假成功），且比旧的"塞进国库（无容量上限）"**更符合 `#41`**
- ⭐ **但须列观察项 `O-17`**（"仓长期满 ⇒ 派-装-弃忙等"）＋ ⭐ **判据须给计数**（`DestFull` 条数 **正常应 ≈ 0**；若爆量 ⇒ 说明"仓长期满"是真问题 ⇒ 归 **`O-2`** 家族）
- ⛔ **本批不加"退避"机制**（新机制 ⇒ 扩大改动面）；若 `DestFull` 爆量 ⇒ 另批议
- ⚠️ **本端自认**：`D824` §一-2 我裁"③ 玩家退回背包"时**未预见"任务层反复派发"** ⇒ 立 **`L-75`**

### 3.2 ⚠️ `ExecuteCompletion` 的 Transport 支**返回值被忽略**（口径与到达分支不一致）

```
case Transport 兜底段：  if (carryInv != null && !carryInv.IsEmpty) UnloadInventory(brain, task);   ← 返回值忽略
                          break;
到达分支：               if (!UnloadInventory(...)) ⇒ Abandon(DestFull)
```
⇒ ⚠️ 同一个 `UnloadInventory`，**两处的失败处置不同**（一处 `Abandon`／一处**不管**）。
**裁定**：
- ✅ **可接受**（该支是 `D800`/`DZ-072a` 的"**满背包取货失败兜底卸货**"⇒ 它本身是**末端兜底**，不是"正常搬运的落点"）
- ⭐ **但须在报告/判据里说明该差异**（⛔ 不得沉默）⇒ 列入 §六"须报读数"

### 3.3 ⚠️ 判据 3 的口径冲突 —— ⭐ **本端已裁（收紧执行端的建议）**

**冲突面实测 ＝ 全库 19 处命中**，本端分类（⛔ 不得笼统"注释豁免"）：

| 类 | 处数 | 位置 | 裁定 |
|---|---|---|---|
| **墓碑注**（说明**已删**） | 5 | `TaskScheduler:722`／`ScheduleCenterStub:67`·`:93`／`StorageComponent:349`／`BehaviorExecutor:171` | ✅ **豁免**（其存在目的就是记录删除） |
| ⭐ **旧描述注**（**本批应改未改**） | **5** | ⭐ `StorageComponent:269`·`:276`·`:309`·`:341`（**执行端已如实标注"部分未改"**）＋ `AIDebugSpawnController:432` | ⛔ **不豁免 ⇒ 须改**（旧描述会误导后来读者 · `L-63` 家族） |
| **红线面** | 1 | `NPCBrain:728`（注释） | ⛔ **豁免**（红线：⛔ 不动 `NPCBrain.cs`）⚠️ **须显式声明** |
| **判据说明字符串** | 1 | `Editor/ChainAudit/ChainAuditSpec.cs:61` | ⛔ **豁免**（⛔ 非本批面） |
| **Editor 探针注释/日志串** | 7 | `HH314_M1B:25`／`HH319_U15Probe:92`·`:97`·`:101`／`HH319_WaterAccount:9`·`:84`·`:85` | ⚠️ **按需**（`U15Probe`/`WaterAccount` 是**历史复现档** ⇒ 保留其历史表述**可接受**；但 `HH314_M1B:25` 是**在役探针**的说明 ⇒ ⭐ 须改） |

⭐ **判据 3 新口径（本端裁）**：
> **存活型引用（可执行代码）＝ 0** ✅ 已达成 ＋ **在役源文件的旧描述注 ＝ 0**（⛔ `StorageComponent` 4 处 ＋ `AIDebugSpawnController:432` ＋ `HH314_M1B:25` **须改**）＋ **墓碑注豁免** ＋ **红线面豁免**（须列名）＋ **历史复现档豁免**（须列名）
> ⚠️ **且报告须给"豁免清单"**（逐条列名 ＋ 理由）—— ⛔ 不得以"注释豁免"一句笼统带过

### 3.4 ⚠️ `ScheduleCenterStub._transporting` 遗留（仅 warning）

✅ **准执行端判断**（下批清理）｜⚠️ 但**须一并**：其 `[Header]`／`<summary>`／`GetPriority` 中若只为链 B 服务者 ⇒ 同批核（⛔ 本批不动，避免与 `DispatchCrew` 面交叉）

---

## 四 · 勘正

① ⚠️ `HH314_M1B:25` 是在役探针的说明注（⛔ 非历史复现档）⇒ **须改**（执行端未列）。
② ⭐ `StorageComponent:341` 的 `GetCarryAmount(ResourceType)` 头注仍写「调用方 `HarvestCarry` 会先问国库能否收」⇒ **调用方已不存在** ⇒ **须改**（与 ① 同族）。

---

## 五 · 说明（3 条）

1. ⛔ **本批 ⛔ 不判绿**：`D824` §三 十条判据**一条未验**（探针未扩展 · 未跑局 · 未取回归）⇒ ⭐ **代码面通过 ≠ 达成**。
2. ⚠️ **编译 0 error 未经本端独立复编**（执行端自陈）⇒ 本端接受其自陈（⛔ 无编译环境）＋ ⭐ **下批跑局时会自然复验**。
3. ⭐ **`D824` §一-3 的 8 值契约**已落地 ⇒ ⚠️ **`D815 §2.4` 曾钉 7 值** ⇒ ⭐ **探针/summary 若按关键词计数须同步**（`DestFull` 纳入 `reason` 分桶）。

---

## 六 · 续批任务书（`M1-G-1b` · 件 1 ＋ 件 5 ＋ 探针 ＋ 跑局）

| 件 | 内容 | 要点 |
|---|---|---|
| **A** · 件 1 `#42` | `UnitController.SetOccupation:85` 补写入 | ✅ 执行端已实读**无既有转职事件**（`GameEvents.cs` 仅 `UnitSpawned/Died/HpChanged/AttributeChanged/Command/DataLoaded`）⇒ ⭐ **准新增 `UnitOccupationChangedEvent`**（struct · `EventBus`）｜⚠️ **须报**：定义落点 ＋ 订阅方清单（⛔ 0 订阅＝备而未用）＋ 是否入档 ＋ **9 处逐处标"是否需写"**（⚠️ `PopulationSystem:506`/`:532`／`VagrantCampSystem:251` 是"成长/招募"⇒ **单列**）｜⚠️ **行尾 `mixed` ⇒ python 二进制按行替换** |
| **B** · 件 5 `Q1` 接线 | 按 `D824` §一-1 案 (c) | `Building` 一次性标记（**入口读后即清**）＋ `RequestHaulNow(StorageComponent)` 重载 ＋ `BuildingPanel` 文案「派搬运」 |
| **C** · 判据 3 收口 | §三-3 | 改 `StorageComponent:269/276/309/341` ＋ `AIDebugSpawnController:432` ＋ `HH314_M1B:25` |
| **D** · 探针扩展 | 判据 1/2/4/5/9/10 | ⭐ 须含：`·farm]` **逐帧**（`L-69`）／`DestFull` 计数（`O-17`）／`#42` 9 处各 1 条 ＋ 反向列／`Q1` **派出类型**／`Q2` **留背包→自愈时序** |
| **E** · 跑局 ＋ 回归 | 判据 1/2/3/4/6/7/8 | ⚠️ **跑前先归档**（`L-68`/`L-70`）｜⚠️ 任一红 ⇒ **停手报裁** |
| **F** · `_transporting` 清理 | §三-4 | 下批或本批尾（⚠️ 须核 `GetPriority` 等辅助面是否只为链 B 服务） |

---

## 七 · 本端自认（1 条）

⚠️ `D824` §一-2 我裁「③ 玩家退回背包」时，**只论证了"不丢"，未论证"上游会不会反复重试"** ⇒ 本轮 `Abandon(DestFull)` 暴露出**忙等循环**。⇒ ⭐ 立 **`L-75`**：**凡新增/改变一个"失败出口"⇒ 必须回查"上游是否会因该出口而重试"**（与 `D816` 通例同族：`D816` 查"收回驱动权 ⇒ 枚举其全部职责段"，`L-75` 查"失败出口 ⇒ 上游重试"）。
