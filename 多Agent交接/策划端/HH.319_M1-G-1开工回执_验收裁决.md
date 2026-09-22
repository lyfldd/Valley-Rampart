# HH.319 后接批 · `M1-G-1`（A 批）开工回执 · 交付验收裁决

> `D824` · 2026-09-22 · 被验：执行端《开工回执（5 行）》（⛔ 未落码 · `Assets/**` 零写）
> 本端实证 **14 项** ＋ **2 处勘正**（其中 1 处**修正执行端的修法方案**）＋ **3 条裁定**（含 1 处**改形态**）
> ⛔ 本轮**不落码** ⇒ 本裁决 ＝ 裁定 ＋ 施工指令（执行端据此落码）

---

## 一 · 总判

| 项 | 判 |
|---|---|
| `git status` / HEAD | ✅ HEAD ＝ `1748429c`（未推进）⇒ ⛔ **零落码成立** ✓（`Assets` 唯一已跟踪改动 ＝ `O-14` 既有脏点 `GameScene.unity`） |
| `§一` 七批逐条复核 | ✅ **全无异议**（执行端自陈"无异议" ✓ 且本端独立复核一致） |
| 行尾读数 | ✅ `StorageComponent`/`TaskScheduler`/`ScheduleCenterStub`/`BuildingPanel`/探针 ＝ 纯 LF；⚠️ `UnitController.cs` ＝ **mixed** ⇒ 须走 python 二进制按行替换 ✔ |
| `Q1` 依据 | ✅ **完全成立**（决定性：`RequestHaulNow` 落点已备 ＋ `09` §9.8 契约已定 ＋ `ChestEntity` 先例已跑） |
| `Q2` 实读 | ✅ 两条成立 ｜ ⚠️ **但"工人被占用不存在"只覆盖派工面** ⇒ 本端补：**装载面仍被咬** |
| `Q3` 消费面 | ✅ 数字全对（`HarvestTarget` 3 处／`IHarvestable` 4 处） |
| ⚠️ 修法方案 | ⚠️ **`Q2` 方案有 2 处须改**（见 §四） ⇒ 本端**改裁** |

---

## 二 · `Q1` 裁定 —— ⭐ **准"改"，但形态必须换（本端改裁）**

### 2.1 依据成立（本端逐点实证）

| 锚点 | 实读 |
|---|---|
| `TaskScheduler.cs:356-368` `RequestHaulNow(ITaskSource source)` | ✅ **逐字在场**：头注「⭐ `HH.316` 件6（`D798` 裁 ①「链 B 并回链 A」· `09` §9.8 `:390`「玩家手点 ＝ **调用搬运任务的一种形式**」）」＋「⛔ **无独立入账口** —— 到账只经链 A 卸货段」；体 ＝ `if (!_sources.Contains(source)) Register(source); Tick();` |
| `09` §9.8 | ✅ **逐字**：「⭐ **怎么捡 ＝ 上层的事** ｜ **玩家手点 ＝ 调用搬运任务的一种形式**；**AI 走同一套形式** ⇒ 落点归 `05` 交互／`06` 任务」 |
| `ChestEntity.cs:163-168` | ✅ `Interact` ⇒ `TaskScheduler.Instance.RequestHaulNow(this)` **唯一调用者** ✓ |
| `StorageComponent.cs:13` / `:267` | ✅ 头注自陈「玩家手动收取…**`M1-G` 改两段式**」⇒ ⭐ **已立册工作项，⛔ 非新增** |

⇒ ⭐ **结论：`Q1` 的方向准（改走搬运）。**

### 2.2 ⚠️ 但形态不是 (a) 也不是 (b) —— 本端实读发现**第三个缺口**

```
TaskScheduler.Tick():290-293
    foreach (var s in _sources) { ... if (!s.TryAdvertiseTask(out var task)) continue; }
```
⇒ ⭐ `RequestHaulNow` 的体是 `Register + Tick` ⇒ **`Tick` 走的是「源的全链广告」** ⇒ 对 `Building` 而言 ＝ 调 `Building.TryAdvertiseTask`
⇒ ⚠️ 而 `Building.TryAdvertiseTask`（`:1494-1581`）顺序 ＝ **② 搬水 → ③ 生产 → ④ 搬运**

⭐⭐ **所以 `RequestHaulNow(building)` 不保证派的是搬运** —— 玩家点"收取"，若该农场**缺水** ⇒ 返回 **② 搬水任务**；若**无工人** ⇒ 返回 **③ 生产任务**。
⇒ ⛔ **(b) 案（传 `Building` ＋ 旁路阈值）不足** —— 它只解决"阈值拦"，**没解决"分支选择"**（执行端未识别此点）。

### 2.3 ⭐ 本端裁定形态 ＝ **案 (c)「一次性强制搬运广告」**

```
① Building 增一次性标记（`private bool _forceTransportOnce;` ＋ 内部 set 口）
② Building.TryAdvertiseTask 入口：读标记并**立即清**（防 `:1501 state!=Active` 提前 return 致残留）
③ 标记为真 ⇒ **先判 ④ 搬运**（跳过 ②/③ 前置）＋ ⭐ **跳过 `transportThreshold`**（玩家显式要求 · `:1567`）
④ TaskScheduler 增重载：
     public bool RequestHaulNow(StorageComponent st)
     {
         var b = st != null ? st.GetComponentInParent<Building>() : null;
         if (b == null || b.state != BuildingState.Active) return false;
         b.RequestTransportOnce();                 // 置一次性标记
         return RequestHaulNow((ITaskSource)b);     // 复用：Register ＋ Tick
     }
```
**理由**：
- ⭐ **复用 `RequestHaulNow` 既有语义**（`Register ＋ Tick`）⇒ 幂等由 `Tick` 的「源＋类型去重／规模派工」保证（头注 `:361` 自陈）✓
- ⛔ 不新增"直接 `Dispatch`"路径（那会绕过 `Tick` 的去重 ⇒ 幂等自担）
- ⭐ `GetComponentInParent<Building>`（⚠️ ⛔ 非 `GetComponent` —— `StorageComponent` 挂建筑上，但 `ChestEntity` 是"箱＝仓"挂本体 ⇒ 用 `InParent` 兼容两者）
- ⚠️ **标记必须"读后即清"**：若 `TryAdvertiseTask` 因 `state != Active` 在 `:1501` 提前 `return false` ⇒ 标记残留 ⇒ 下一 tick 误触非搬运分支

⚠️ **UI 文案**：`BuildingPanel:186` 「收取」⇒「**派搬运**」（`Q1` 落地的必然连带）

---

## 三 · `Q2` 裁定 —— ⭐ **准"先问后拿"，但方案须改 2 处**

### 3.1 执行端两条实读 —— 本端复核

| 条 | 判 | 本端实读 |
|---|---|---|
| ①「背包单资源不可混装」＝ 硬约束 | ✅ **成立** | `WorkerInventory.cs:51 if (!IsEmpty && carriedType != type) return 0;` **逐字** |
| ②「工人被占用不存在」（`IsIdleForTask` 不查背包） | ✅ **成立** | `NPCBrain.cs:131-135`：`if (!_focus.IsValid) return true;` / `focus is TaskStimulus ⇒ false` / `is ThreatStimulus ⇒ false` / 其余 `true` ⇒ ⛔ **确不查背包** |

### 3.2 ⚠️ 但 ②**只覆盖"派工面"** —— 本端补：**装载面仍被咬**

⇒ ⭐ **真后果不是"工人被占用"，而是"工人对该资源类型之外暂时失效"**：
```
背包留货（A 型） ⇒ 下一趟被派去搬 B 型 ⇒ LoadInventoryFromSource:864 inv.TryStore(B,…)
                  ⇒ :51 carriedType != type ⇒ 返 0 ⇒ :865 if (stored <= 0) return false
                  ⇒ Ticking 侧 ⇒ Complete（"无货可搬"） ⇒ ⭐ 该工人搬不动 B
```
⇒ ⚠️ 所以"出口"仍然必须有 —— 见 §3.4。

### 3.3 ⚠️⚠️ 修订 ①：`inv.Take(type, 可入量)` **会失败**（方案缺陷）

```
IWarehouse.cs:37-38    /// 减（删 · 尽力档 · `09` §7.2）：从本仓拿走 ≤`amt`，返回实际取走量。
                       int Take(ResourceType type, int amt);          ← ⭐ 契约 ＝「尽力档（部分成功）」

WorkerInventory.cs:79  public bool CanTake(ResourceType t, int amt) => !IsEmpty && carriedType == t && carriedAmount >= amt;
WorkerInventory.cs:81-87
                       public int Take(ResourceType t, int amt)
                       {
                           if (!CanTake(t, amt)) return 0;            ← ⚠️ 要求 carriedAmount >= amt（全有或全无）
                           int taken = Mathf.Min(amt, carriedAmount);
                           carriedAmount -= taken;
                           return taken;
                       }
```
⇒ ⚠️ **`WorkerInventory.Take` 与 `IWarehouse.Take` 的契约相悖**（实现是"全有或全无"，契约是"尽力档"）
⇒ ⭐ 执行端方案传「可入量」（**通常 < 背包量**）⇒ `CanTake` 返 `false` ⇒ **`Take` 返 0 ⇒ 一个都不卸**。

⭐ **修法两选** —— 本端**准 (乙)**：
- (甲) 调用侧补 `Mathf.Min(can, inv.carriedAmount)` ⇒ 最小，⛔ 但留着契约违规
- ⭐ **(乙) 把 `WorkerInventory.Take` 改为契约语义**（`if (!IsEmpty && carriedType == t) return 0;` ＋ `return Mathf.Min(amt, carriedAmount)` **无 `CanTake` 前置**）——
  ⭐ **依据（本端实测）**：`WorkerInventory.Take` / `.CanTake` **全库零调用**（`.Take(` 的全部命中均属 `IWarehouse` 其它实现：`vault.Take`／`storage.Take`／`tv.Take`／`warehouses[i].Take`／`RulerController:242`）⇒ **备而未用** ⇒ ⭐ **改它零回归风险**，且**顺带修一条契约违规**

### 3.4 ⭐ 修订 ②：`FindNearestAvailable` **已过滤余量** ⇒ 本端补"出口"裁定

```
WarehouseRegistry.cs:62-69
    if (s == null || KingdomOf(s) != kingdomId) continue;              // 同国
    if (!s.Accepts(type) || s.CanAccept(type) <= 0) continue;          ← ⭐ 已过滤「标签 + 余量」
```
⇒ ⭐ **`best != null` ⇒ 必有可入量（≥1）** ⇒ ⭐ **"满了"只发生在 `best == null`**（无任何可用仓），或被"余量 < 背包量"截断。

⭐ **本端裁定「出口」**：**推广 `LoadInventoryFromSource:847` 的箱源先例到所有源**
```
现状（仅箱源）：  if (task.source is ChestEntity && !inv.IsEmpty) UnloadInventory(brain, task);   // 先就地卸空再取货
裁定：            if (!inv.IsEmpty) UnloadInventory(brain, task);                                // ⭐ 对全部源生效
```
**理由**：① 与 `#41`「满了就满了」**不冲突**（卸不掉就 `break`，货仍留背包）② **有仓可收即自愈**（不是永久失效）③ 与既有先例同形（⛔ 不新增机制）

⚠️ **契约须补一句**（`09`）：`#41` 的「满了就满了」＋「装载前先尝试卸空旧货（自愈）」⇒ 本端同批落 `09`。

### 3.5 `AbandonReason` 7 → 8 —— ⭐ **准**

- 新增 `DestFull`（"落点仓收不下"）
- ⚠️ **须同步注释**（`TaskScheduler.cs:68-71` 自陈"**日志契约取值域（7 值）**" ⇒ 改 8）
- ⚠️ **须在报告里报新值域**（`reason` 是**判据读口** ⇒ 取值域变化属契约面）

---

## 四 · `Q3` 裁定 —— ⭐ **准甲案**（只动 `_Game` ＋ `Inert` 标注）

消费面本端复核 ✅：`HarvestTarget` 3 处（`DecisionStructs.cs:107` 定义 ／ `L3CommandComputer.cs:82` 写 ／ `BehaviorExecutor.cs:172-177` 读）；`IHarvestable` 4 处（`IHarvestable.cs:14` 定义 ／ `DecisionStructs.cs:107` ／ `L3CommandComputer.cs:81` ／ `StorageComponent.cs:16` 唯一实现）。

⛔ **本端不选 (乙)**：本批已是"契约面清理"大改 ⇒ 叠加 `AI.Core` 改动会**扩大回归面**。

⭐ **但须记一笔（`M1-G-2`）**：`Q1` 甲案 ＋ `Q3` 甲案落地后 ⇒ ⭐ **`StorageComponent.Harvest()` ＋ `BehaviorCommand.HarvestTarget` ＋ `IHarvestable` 端口 三样同时成"备而未用"**（`Harvest()` 唯一活调用点 `BuildingPanel:408` 将改走搬运；`BehaviorExecutor:177` 的 `else` 分支属链 B 不可达路径）⇒ ⭐ **应在 `M1-G-2` 一并退役**（⛔ 不本批）。

---

## 五 · 施工指令（§二~§四 裁定后即落码）

| 件 | 落点 | 要点 |
|---|---|---|
| 1 · `#42` | `UnitController.cs:85` | 补一条写入（⭐ **先实读是否已有既有事件**）· 9 处调用面**逐处标"是否需写"**（⚠️ `PopulationSystem:506`/`:532`／`VagrantCampSystem:251` 是"成长/招募"⇒ 单列）· ⚠️ 行尾 **mixed** ⇒ python 二进制按行替换 |
| 2 · `#41` | `TaskScheduler.UnloadInventory:870-910` | 「先问后拿」：`best.CanAccept` ⇒ `inv.Take`（⚠️ 先修 `WorkerInventory.Take` 语义）⇒ `best.Add`；⭐ **删 `:902-903`／`:908` 两处 `AddGatherOverflow`**；`best == null` ⇒ `break`（留背包） |
| 3 · `#40` | `StorageComponent.cs:360-371` ＋ `:332`；`TaskScheduler.cs:716` | 删 `HarvestCarry` ＋ 无参 `GetCarryAmount`；⚠️ `:716` 兜底同删 ＋ **须论证**「非工人不会拿到 `Transport` 任务」；⭐ **`HarvestCarry:363-365` 两道守卫语义须给出迁移落点**（⛔ 不得无声消失） |
| 4 · `U-15` 根除 | `ScheduleCenterStub.cs:89-152` ＋ `:155-158` ＋ `Update` 调用；`BuildingPanel.cs:183-187` | 删链 B；⚠️ UI 改读 `TaskScheduler.HasWorkerAssigned(this)`（`:155` 公开口）；⭐ **须论证链 A（有阈值）覆盖原链 B 场景**（尤其**水井仓**）|
| 5 · `Q1` 落地 | `Building.cs`（标记 ＋ ④ 分支）／`TaskScheduler.cs`（`RequestHaulNow(StorageComponent)` 重载）／`BuildingPanel.cs:401-410` | 按 §2.3 案 (c)；⚠️ 标记**读后即清** |
| 6 · `Q2` 出口 | `TaskScheduler.LoadInventoryFromSource:847` | 去掉 `is ChestEntity` 限定（对全部源先卸空） |
| 7 · 探针 | `Valley_HH319_F1LongRun.cs` | 判据扩展 ＋ ⚠️ **跑前先归档**（`L-68`/`L-70`） |

⚠️ **`AddGatherOverflow` 逐调用点核**（`:739`/`:743`/`:862`/`:872`/`:877`/`:969`/`:1076`）⇒ 给「调用点 × 是否传玩家 × 该点口径」三列表（`L-71` 实例 2）。

---

## 六 · 判据（本端对 `§三 5 行` 的补充 ＋ 修正）

| # | 判据 | 本端补充／修正 |
|---|---|---|
| 1 | 三侧求和守恒 | ⭐ **修正判据口径**：守恒式 ＝ `背包 ＋ 各仓 ＋ 台账` ⇒ ⚠️ **须含"留背包"的那部分**（⛔ 不得只对仓求和 ⇒ 否则"留背包"会被误读成"丢"） |
| 2 | `[调度中心] 派发搬运任务 → 0` 且 `Building` 的 `Transport 派发 > 0` | ✅ 准 |
| 3 | `HarvestCarry` 全库命中 0 | ⚠️ **加限定**：命中含**注释**（`StorageComponent:269`/`:276`/`:329`/`:344` 四处注释须一并改）⇒ "0" 须以**含注释**为准 |
| 4 | `D811` 止血不回归（改读"井仓水量不被取走"） | ✅ 准 ⇒ ⭐ **补**：须给**井仓水量逐帧序列**（⛔ 不得只给末值 · `L-69`） |
| 5 | `#42` 9 处各 1 条 ＋ 反向列 | ✅ 准 |
| 6 | 四项回归 · 任一红 ⇒ 停手报裁 | ✅ 准 |
| 7 | 存档面不变 | ✅ 准 ＋ ⭐ **补**：`Q2` 出口改的是 `LoadInventoryFromSource` 的**行为**（非存档）⇒ 逐值往返一致即可 |
| 8 | 不含 `U-17`/`U-18`/`U-19`/`DZ-7`/B 批 | ✅ 准 |
| ⭐ 9 | **新增**：`Q1` 接线 —— `BuildingPanel` 点"派搬运" ⇒ ⭐ **必须派出 `Transport` 类型**（⛔ 不得是 `Production`/`WaterHaul`） | 鉴别力：若走 (b) 案（无标记）⇒ 缺水农场会派 `WaterHaul`（**读作语义错**） |
| ⭐ 10 | **新增**：`Q2` 出口 —— 背包有旧货的工人被派新任务 ⇒ **先卸空再装** ＋ 该趟能装上 | 鉴别力：若不加出口 ⇒ `TryStore` 返 0 ⇒ 任务空转 `Complete`（本列须能读出差异） |

---

## 七 · 勘正（2 条）

① ⭐ **执行端引 `Take:81-87`「只减量」不准确** —— `:83` 有 `CanTake` 前置守卫（`carriedAmount >= amt`）⇒ 实为"**全有或全无**"；且与 `IWarehouse.Take` 的契约（「尽力档」）**相悖**。⇒ 立教训 **`L-74`**。
② ⭐ **执行端未识别「`RequestHaulNow(building)` 会走 ②/③ 分支」** ⇒ `Q1` 的 (b) 案不足。⇒ 本端改裁案 (c)。

---

## 八 · 下一步

⭐ **执行端按 §五 落码 ＋ §六 判据（含新增 9／10）**；⚠️ 报告须给：① `Q1` 接线的"**派出类型**"读数 ② `Q2` 的"留背包 → 自愈"时序 ③ `AbandonReason` 新值域 ④ `HarvestCarry` 守卫语义的迁移落点论证。
⛔ 仍不含 `U-17`／`U-18`／`U-19`／`DZ-7`／B 批四项。
