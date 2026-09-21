# HH.319 · `M1-F` · `F-1` 补证单（`D809`）· 交付报告

- **日期**：2026-09-21 ｜ **执行端** TraeCode ｜ **状态**：⛔ **停手报裁**（件 3 完成；件 1 取读数未达标；件 2 按红线停手）
- **对象批次**：`HH.319 F-1`（水仓化 ＋ 真搬运链 · `64c7d9de`）补证读数
- **红线自检**：⛔ 未加新功能 ✅ ｜ ⛔ 未改水域逻辑（除件 3 零行为删）✅ ｜ ⛔ 未动 `WorkerInventory`/`UnitController`/`DamageSystem`/退款/`DeathCause`/存档 schema ✅ ｜ ⛔ 未碰 `WarehousePanel`/四档账本/美术/pixel-forge/`GameScene`/`Packages`/3.6·3.8 doc ✅ ｜ ⛔ **未自行改生产码使判据变绿** ✅ ｜ 行尾：`KingdomTask.cs` = **LF**（Edit 直改）· 新探针 = LF ✅ ｜ 进局走 `TestHarnessApi.EnterTestRun` 正门 ✅ ｜ 收尾 `ExitTestRun` ＋ 退 Play（`playMode=stopped` 已核）✅ ｜ ⛔ 不 push ✅

---

## 〇 · 一句话

件 3（`need` 删）✅ 全绿。件 1 长局取得**决定性读数**但**未达成判据 1**（1× 下 `WaterHaul` 从未被派出）。件 2 回归**按红线停手未跑**。
⭐ **停手原因（阻塞级）**：取证坐实 —— **本批水仓化后，第二套搬运链（`ScheduleCenterStub`「调度中心」）会把水井仓的水持续搬走，落点 `HarvestCarry → ModifyResource(Water, true)` 而入库口不收 `res_fluid.water` ⇒ 水被无声销毁**。同帧隔离铁证：`井仓 12→2` · `玩家国全库 Water 12→2（差 −10）`。

---

## 一 · 判据逐条

### 判据 1 · 长局：1× 时基下 ≥1 次完整搬水往返（①~⑥）—— ❌ **未达成**

场景（两次长局同场景 · 生产入口布置 · ⛔ 无反射直调搬水）：玩家国 `well@(-6.00,40.96)` ＋ `farm@(6.00,40.96)` ＋ 2 工人；井仓起手 50 水；农场仓 0（缺水）。

| 读数 | **1×**（`speedOverride: 1f`） | **15× 对照**（`speedOverride: 15f`） |
|---|---|---|
| 时基实测 | `δt(帧)=0.161~0.166s` · `timeScale=1` | `timeScale=15`（钳制 1.0s/帧上限） |
| 观测时长 | 180 真实秒（≈180 游戏秒） | 45 真实秒（≈675 游戏秒） |
| **`WaterHaul` 派发行** | ❌ **0 次** | ✅ **4 次**：`[TaskScheduler] 派发 WaterHaul 任务 → npcId 34 @ (-6.00, 40.96)（优先级 B）` |
| ① 农场广告（只读探针采样） | 命中（`adHit`＞0 · 探针只读） | 命中 |
| ② 工人被派（探针捕获） | ❌ | ⚠️ 探针未捕获（**探针缺陷** · 见 §四），派工日志已证 |
| ③ 到达水井 | ❌ | ⚠️ 未捕获 |
| ④ 装载（背包＞0） | ❌ | ✅ 触发（`E4→E6` 有值） |
| ⑤ MovingToDest | ❌ | ✅ 触发 |
| ⑥ 卸货（背包 0 · 农场仓水＞0） | ❌（农场仓水**恒 0**） | ✅ 触发（`E4→E6卸货=5.94s`）· **农场仓水＞0** |
| 完整往返 | ❌ `完整往返=False` | ⚠️ `完整往返=False`（因 ②③ 未捕获；但 ④⑤⑥ 已达成 ⇒ **水实际到了农场**） |

⭐ **时基假设（本端提）的裁定读数**：**既未证实、也未推翻** ——
- 1× 下压根没有 `WaterHaul` 被派出（世界 180 游戏秒推进量太小 · AI 国刚立国），**不构成"1× 能救 3b"的证据**；
- 15× 下反而**成功搬水到农场**，且两次长局**均未复现**历史上 3b 的「卡在 `MovingToSource` 距井 1.29 未达」；
- ⇒ 建议：3b 的"跳步根因"假设**不成立于本场景**，另需定向复现（本轮读数不支持将其归因于时基）。

⭐ **1× 下"无 `WaterHaul`"的直接原因（读数为证）**：工人**聚在水井附近**（seq：最近工人距井 0.1~3.0、距农场 **10~12** 恒定）⇒ **农场长期无工人在场** ⇒ 农场只能命中生产分支（②），而搬水分支（④）**要求农场已有工人在场** ⇒ 条件长期不满足 ⇒ `WaterHaul` 不广告。工人被拉去水井的原因见 §二 缺陷 A/B（第二套搬运链）。

### 判据 2 · `U-14` 读数 A / B

- **读数 A（最终是否到达水井并完成搬水）**
  - 1×：**N**（`WaterHaul` 未被派出 ⇒ 无从谈起）
  - 15×：**部分 Y** —— `WaterHaul` 派出 4 次（**同一工人 npcId 34**）且**卸货已达**（背包归 0 · 农场仓水＞0）⇒ 从"水到农场"看**成立**；但**同工人 4 次重派**⇒ 疑似"逐轮过期重派"（U-14 现象族）
- **读数 B（被拉偏时长 / 是否自愈）**：⚠️ **未取得直接 seq 序列** —— 15× 期间 Console 行被其它日志挤占，`npc34` 序列行未留存（探针缺陷 · §四）。**间接读数**：同工人连续 4 次被派 = **反复重派**；且最终**有水到达农场** ⇒ 判读倾向「**非永久拉走 · 有自愈**」⇒ 依 `D808` 口径可倾向**降为观察项**；⚠️ 但**判法归策划端**，本端只给读数，⛔ 不自行改优先级。

### 判据 3 · 回归 ①~④ —— ⛔ **未跑（停手）**

`HH316 §A~§H` / `HH315 §A~§F` / `HH317` / `2_20B_M7` 六轮**均未执行**。原因：件 1 过程中发现 §二 阻塞级缺陷（真实资源销毁）⇒ 按本单红线「发现阻塞歧义 ⇒ 先停手报裁」＋「出红 ⇒ 停手报裁」**立即停手**。待裁决后补跑（回归成本约 20~30 分钟真实时间）。

### 判据 4 · `need` 删 —— ✅ **全绿**

| 项 | 读数 |
|---|---|
| `HaulToSiteArgs.need` **仍在**（⛔ 不可删 · M1-C 阈值拦截消费方） | ✅ `need field hits: 1` · 命中行 = **:88** |
| `HaulWaterArgs.need` **已无** | ✅ 同上计数（总命中 1 ⇒ 水侧已清零） |
| 注释半句（`need ＝ 缺口量…`） | ✅ 命中数 1（仅 `HaulToSiteArgs` 注释 · `HaulWaterArgs` 注释半句已删） |
| 编译 | ✅ 0 error（10 warning 全既有） |

**块级 diff（`KingdomTask.cs` · `HaulWaterArgs`）**

```diff
 ///   · `target` ＝ 农场仓（卸水落点）；
-///   · `target` ＝ 农场仓（卸水落点）；`need` ＝ 缺口量（装载上限 · ⛔ 不多搬）。
+///   · `target` ＝ 农场仓（卸水落点）。
 /// ⚠️ ⛔ 不复用 `HaulToSiteArgs`：…（类型不兼容 · M1-C 件1 先例形制另立）
+/// ⭐ 本类**不需要** `need`（缺口量）：水搬运与 `Transport` **同构**（按 `StorageComponent.GetCarryAmount(Water)`
+///    决定装载量 · 见 `TaskScheduler.LoadInventoryFromSource`），而 `09` §16.1-2 的「阈值拦截」是
+///    **建造专属契约**（`HaulToSiteArgs.need` 由 `LoadSiteMaterials` 读）⇒ 水不需要（`D809` 补证单件3 · 零行为删除）。
 /// </summary>
 public class HaulWaterArgs
 {
     public StorageComponent target;   // 卸水落点（农场仓 · `M1-F` 件3 的耗水仓）
-    public int need;                  // 缺口量（装载上限 · ⛔ 不多搬）
 }
```

**连带（同属件 3 · 零行为）**：`Building.cs` 农场派水处删 `need = waterThreshold - waterHave` 赋值（`waterHave` 仍用于缺水判据，无未用变量）；旧探针 `Valley_HH319_M1F_Probe.cs` 两处 `new HaulWaterArgs { … need = 20 }` 同步删。

---

## 二 · ⭐⭐ 阻塞级发现（报裁核心）

### 缺陷 A · 水被无声销毁（**运行时铁证**）

**链路（全为生产码 · 有 file:line）**
1. `ScheduleCenterStub.DispatchTransport()`（`Assets/_Game/Systems/AI/Schedule/ScheduleCenterStub.cs:118`）判据 = `storage.IsReadyToHarvest()`，「无产出不搬」；
2. `StorageComponent.IsReadyToHarvest() => TotalCount > 0`（`…/Building/StorageComponent.cs:270`）⇒ **水井仓只要有水 ⇒ 即被纳入搬运源**；
3. 它以 `TaskStimulus` 派空闲工人到建筑位置（同文件 `:143-150`），工人到场后 `BehaviorExecutor` 调 `sc.HarvestCarry()`（`…/AI/Execution/BehaviorExecutor.cs:175`）；
4. `StorageComponent.HarvestCarry()`（`:320-328`）：`TakeOut(type, amount)` ＋ `RulerController.Instance?.ModifyResource(type, true, taken)`；
5. 玩家国国库 `VaultPaths` **不含 `res_fluid.water`** ⇒ 该资源**不被接收** ⇒ `ModifyResource` 侧丢弃 ⇒ **水消失**。

**证据 1 · 派工日志（1× 长局 · 反复发生）**
```
[调度中心] 派发搬运任务 → 2 工人 @ (-6.00, 40.96)（Water 存量 20，分批2）
[调度中心] 派发搬运任务 → 3 工人 @ (-6.00, 40.96)（Water 存量 28，分批3）
[调度中心] 派发搬运任务 → 3 工人 @ (-6.00, 40.96)（Water 存量 32，分批4）
[调度中心] 派发搬运任务 → 4 工人 @ (-6.00, 40.96)（Water 存量 32，分批4）
```
（`(-6.00, 40.96)` = 本端所建玩家国水井 · ≥4 次 ⇒ 井仓持续被搬）

**证据 2 · 同帧隔离取证（探针 `Valley_HH319_WaterAccount` · 只读 ＋ 复现落点动作）**
```
[HH319取证] 井仓 12 → 2（HarvestCarry 返回 10）
[HH319取证] 玩家国 Water 合计（同帧前后）12 → 2（差 -10）
[HH319取证] ⇒ 搬走=10 · 玩家国全库差=-10 ⇒ ⚠️ 水被销毁（国库不收 res_fluid.water ⇒ ModifyResource 丢弃）
```
（"同帧前后"排除产水增量；"只算玩家国"排除 AI 国产水 ⇒ **水未进入任何收水仓**）

**本批连带性质**：改前水存于 `WaterNetwork`（浮点桶 · 水井仓无内容）⇒ 该链不会碰水；**水仓化后水井仓有 Water ⇒ 才被这套系统纳入搬运**。另注：`HarvestCarry` 自陈为 `09#40`「删 `HarvestCarry` 直通国库」的**待删过渡实现**（归 `M1-G`），**当前先于 `M1-G` 就伤害了水**。

**影响面**：水井产水被持续销毁（1× 长局：产水 ≈4/秒×180 秒 ≈ 720，实际存量 ≈ 30）；且派工**占住工人**（把工人拉离其它任务 ⇒ 与 U-14 同族）。

### 缺陷 B · 三路并行 + 刺激竞争（判据 1 在 1× 下不达成的直接原因）

水现有**三条**流转路径并存：① `TaskScheduler.WaterHaul`（需农场在场工人 · 终到农场）｜② `TaskScheduler.Transport`（井仓 ≥`transportThreshold` 时 · 终到最近仓）｜③ `ScheduleCenterStub` 刺激搬运（`TotalCount>0` 即搬 · 落点 `HarvestCarry` ⇒ **销毁**）。
读数：1× 长局中工人被 ③ 拉在水井旁（距农场 10~12 恒定）⇒ 农场无工人 ⇒ `WaterHaul` 广告条件（需农场已有工人在场）长期不满足。此与 `D808` 验收判据 9「与 Transport 功能重叠」**同族**，但 ③ 是**本批之前未纳入账本**的路径。

### 读数 C · 15× 下通路是通的

`派发 WaterHaul → npcId 34` × 4 ＋ 卸货达成（农场仓水＞0）⇒ **"井 → 农场"在水存在的前提下能走通**；⇒ 缺陷 A 的破坏力恰好在于**把水在中途销毁**，使该通路在长局中经常"无米下锅"。

---

## 三 · 本端未做（留给裁决）

1. ⛔ **未改任何生产码**（除件 3 零行为删）—— 缺陷 A/B 的止血方案请裁决后另批执行。
2. ⛔ 件 2 回归未跑（见判据 3）。
3. 建议方向（**仅供裁决参考 · 本端不裁**）：
   - (a) `HarvestCarry` 或 `DispatchTransport` 侧加「仅搬**可入国库**资源」过滤（Water 不搬）⇒ 最小止血；
   - (b) 或 `IsReadyToHarvest()` 排除 Water（属 `09#40` 待删对象，提前收口）；
   - (c) 若 `M1-G` 原计划删 `HarvestCarry` ⇒ 建议**提前**（它当下正在销毁水）；
   - (d) 是否把「调度中心刺激搬运」正式纳入 `M1-F` 的水流转账本（三路并存的可见性）。

---

## 四 · 探针与取证资产（本轮新增 · 仅 Editor/Smoke）

| 文件 | 用途 |
|---|---|
| `Assets/Editor/Smoke/Valley_HH319_F1LongRun.cs` | 1×／15× 长局观察（生产路径布置 · 逐秒 seq · E2~E6 事件） |
| `Assets/Editor/Smoke/Valley_HH319_WaterAccount.cs` | 水账取证（同帧 · 隔离玩家国 · `HarvestCarry` 落点） |

**自陈缺陷（诚实列报 · 影响判据 2 读数 B）**
1. v1 只采样**单个**工人 ⇒ 误得 `state=None`（v2 已修为遍历全国工人）；
2. `E2` 判定依赖反射读 `_npcTaskMap` ＋ `PlayerWorkers()` 遍历 ⇒ **15× 下未捕获已发生的 `WaterHaul` 派发**（派工日志为证）⇒ 该探针的"事件捕获"不可靠，**结论改以生产日志为准**；
3. 1× 长局为维持靶例做了**井仓水位压制**（`>30 ⇒ 压回 20` · 只减不增）⇒ 该动作本身也销毁水（`TakeOut` 无记账）⇒ **1× 的"水账"读数不可用于计量销毁量**（销毁量以 §二 同帧取证的 10 点为准）。

---

## 五 · 结论

- 件 3：✅ 完成（判据 4 全绿 · 编译 0 error）。
- 件 1：读数已取（1×／15× 双档 ＋ 派工日志 ＋ 水账取证），**判据 1 未达成**；根因**不是时基跳步**，而是 §二 缺陷 A/B。
- 件 2：⛔ 停手未跑。
- ⛔ **不销号**；请裁决缺陷 A/B 的处置（止血批次 / 是否纳入 `M1-G`）与件 2 回归的补跑安排。
