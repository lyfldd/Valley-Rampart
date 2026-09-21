# HH.315 · `M1-C` · `U-8`「非 Active 态拆除卡死」—— 只读评估 ＋ 报选

- 任务号：`HH.315` · `M1-C` · `U-8`（承 `D799`（`HH.316` `U-2` 验收）立 · ⚠️ **`M1-C` 销号唯一前置**）
- 本轮性质：**只读评估 ＋ 报选**（⛔ `Assets/**` **一行未动**；⛔ 未开工；⛔ 未 push；⛔ 未代提交策划端账本）
- 日期：2026-09-21
- 取证手段：静态实读（Read／Grep）＋ 资产 YAML 直读（`BuildConfig.asset`／`TaskPriorityConfig.asset`）；⛔ 未跑 Unity、⛔ 未进局（运行时验证归施工批）
- 交付面：本报告 1 文件（⛔ 无代码、⛔ 无资产、⛔ 无探针文件）
- 本端立场：**任务书 4 个落点逐条复核 ⇒ 3 条坐实、1 条勘正**（见 §一）；⭐ 另**新增 2 条独立发现**（§八 `E1`／`E2`）＋ 2 条列报（`E3`／`E4`）

---

## 〇 结论速览

| # | 任务书落点 | 本端复核 |
|---|---|---|
| **A1** | 注册守卫 `Building.cs:1445`（`state == Active`）＋ 唯一调用点 `:720` | ✅ **坐实**（逐字实读） |
| **A2** | `TaskScheduler.cs:95` 单例创建补注册同为 `Active` 专属 | ✅ **坐实**（守卫在 `:95`，位于 `:84-98` 块）；⚠️ 但"**必须同批**"⇒ 本端判 **⛔ 不同批**（案甲下无必要，见 §七 `Q2`） |
| **B** | `IsValid` 不含 `Ruined` ⇒ 放宽注册也会被 `Tick:223` 清出 | ⚠️ **勘正**：事实对（确实不含 `Ruined`）、**机制不成立** —— 拆除中 `_demolishing` 已使 `IsValid=true`（任意 state）⇒ ⭐ **`Ruined` 无需补**；真正的 `IsValid` 缺口在「**施工中（料齐）**」，且**只对案乙成立**（见 §一 1.3） |
| **C** | `Demolish()` 未注销工地仓源 | ✅ **坐实**；⚠️ 精确化：「双源同册」**当前仅「升级·投料中拆除」可达**（首次建造／重建的本体根本没在册）⇒ ⭐ **修完 U-8 后它才成为普遍面**（见 §一 1.4） |
| **D** | 拆除任务 Working 工时错配（2s vs 6s） | ✅ **坐实**（`:43` `workDuration=2f` ＋ `:1287-1295` 仅 Gather 走源侧 ＋ `:505-509` 落 else 零动作释放）；⭐ **量化**：现况 ≈**7~8s** 实耗 vs 声称 6s，且 **≥2 轮完整派工**（任务书"≥3 轮"口径见 §一 1.5 勘注） |
| 附带项 | `ExecuteCompletion` 无 `Build` 分支 ⇒ 拆除零动作释放"正确、⛔ 不必改" | ✅ **同意**（拆除由 `Building.Update:594-606` 自推；勿加 `Build` 分支） |
| **修法推荐** | — | ⭐ **案甲**（`Demolish()` 内即时注册）＋ **读档路径补注册**（§六）＋ **C 注销**（§五）；⛔ 不走案乙 |
| **新增列报** | — | `E1` 拆除中被打成废墟 ⇒ `_demolishing` 闩锁 ⇒ 重建后**永久卡死**（静态必现，§八）；`E2` `n`/门控语义（源级计数 + 门控含"派工未到场"期） |

**`Q1`~`Q7` 报选一览**（逐条理由见 §七）：

| 口径 | 本端建议 |
|---|---|
| `Q1` 注册时机 | **案甲**（`Demolish()` 内注册；2~3 行） |
| `Q2` `:95` 同步放宽 | ⛔ **不放宽**（案甲下无必要；放宽只是把两个守卫的语义分裂面扩大） |
| `Q3` `Demolish()` 注销工地仓源 | ✅ **应注销**（用现成 `UnregisterSiteStore()`；⛔ 不清内容 —— 与"保内容"**不冲突**，论证见 §五） |
| `Q4` 拆除工时 | ✅ **改**（照 `GatherTaskArgs.gatherSeconds` 先例 **同形照搬**：源侧填、调度器读）；不改的量化差距 ≈ +17%~33% |
| `Q5` 协作人数 / `k` 语义 | **维持 1 人**；`k` 在拆除侧**形式上不成立**（n 恒 ≤1 ⇒ 死分支；且 `n` 来源会把无关残留任务误计）⇒ 建议随批**加注/收紧**，⛔ 不做多工人 |
| `Q6` 投料态本体在册开销 | 案甲下**为 0**；案乙下 **O(1)/tick/源 可忽略**，真代价是**状态集一致性维护**（3~4 处耦合）⇒ ⛔ 不以此为由走案乙 |
| `Q7` 与 `U-5` 交叉 | ⚠️ **成立（窄面）**：仅「**读档后 · 在投修复（废墟重建）**」⇒ 退款把修复阶段误当升级阶段（有界偏差）；⭐ 且**修完 U-8 才使其可达** |

---

## 一 · 落点逐条复核（逐行实读）

### 1.1 落点 A1 · 注册守卫 —— ✅ 坐实

| 证据 | 实读 |
|---|---|
| 守卫表达式 | `Building.cs:1445`：`if (TaskScheduler.HasInstance && state == BuildingState.Active)` ⇒ `TaskScheduler.Instance.Register(this)`（`:1446`） |
| 方法体 | `Building.cs:1440-1447`（`private void RegisterWithTaskScheduler()`），注释自陈"建筑转 Active 时注册" |
| 唯一调用点 | 全库 grep `RegisterWithTaskScheduler` ⇒ **2 命中 = 定义 `:1440` ＋ 调用 `:720`**（`OnConstructionComplete` 内，`state = Active` 之后 `:717`） |
| 建厂路径（玩家） | `BuildController.cs:284` `b.Init(...)`（`Init` **不注册**，`Building.cs:426-445` 实读）⇒ `:292 b.StartConstructing(...)` ⇒ ⭐ **进入 `Constructing` ⇒ 本体从未注册** |
| 建厂路径（读档） | `BuildingFactory.cs:189`：`if (initialState == BuildingState.Active && TaskScheduler.HasInstance)` ⇒ ⭐ 非 Active 存档态（`Constructing`／`Ruined`）**不注册** |

⇒ 与 `D799` §2.1 逐字一致 ✅。**后果链**（本端复算）：无本体源 ⇒ `TryAdvertiseTask` 无拆除广告（`Building.cs:1367-1373` 进不去）⇒ 无工人派工 ⇒ `HasAssignedWorker()`（`:626-627`，`CountAssignedWorkers>0`）恒 false ⇒ `_demolishProgress` 恒 0（`:598`）⇒ **卡死** ✅。

### 1.2 落点 A2 · 单例创建补注册 —— ✅ 坐实（但"必须同批"⇒ 判不同批）

| 证据 | 实读 |
|---|---|
| 补注册块 | `TaskScheduler.cs:84-98`（`Awake` 内）；守卫在 `:95`：`if (b != null && b.state == BuildingState.Active && !_sources.Contains(b)) Register(b);` |
| 语义 | 只收 `Active`（与 A1 同型）✅ |

⚠️ **本端复核的重要限定**（决定 `Q2` 结论）：

1. `TaskScheduler.HasInstance`（`:35`）＝ `Instance != null`，而 `Instance` 是**自动创建**单例（`:68-99` `Awake`）⇒ **任何一次 `HasInstance` 访问都会当场把单例建出来**；
2. ⇒ "先建后起调度器"要真成立，只有"某次注册动作把单例建起来、而该建筑**当时还不是 Active**"这一窗口 —— 而**该窗口不靠本循环补**（循环只在创建瞬间跑一遍，且只遍历 `BuildingRegistry`）；
3. ⭐ **"读档重建"场景同样不靠它**：读档（`SpawnFromSave`）发生在单例**已存在**之后（或由更早的 `HasInstance` 创建），`:84-98` 只跑一次、不会再跑 ⇒ **放宽 `:95` 对读档路径几乎无用**；读档路径的真缺口在 `LoadState`（§六）。
4. 案甲（`Demolish()` 内注册）落地后，拆除侧**不依赖**该循环 ⇒ **无同批必要**。

⇒ 判定：**坐实（事实）／⛔ 不同批（必要性）**。若策划端最终选案乙，则 `:95` **必须**同步（否则出现"注册语义分裂"），届时再改。

### 1.3 落点 B · `IsValid` 状态集 —— ⚠️ 勘正（方向对、机制不成立）

**逐字实读**（`Building.cs:1349-1350`）：

```1349:1350:Valley Rampart/Assets/_Game/Systems/Building/Building.cs
    public bool IsValid => this != null
        && (state == BuildingState.Active || _awaitingMaterials || _demolishing);
```

`Demolish()` 守卫（`:848-857`）只排除 `Dead`／`Placing`（`:852`）✅ 坐实。

**本端真值表**（`IsValid` ＝ 三项 **OR**）：

| state | `_awaitingMaterials` | `_demolishing` | `IsValid` | 出现场景 |
|---|---|---|---|---|
| Active | — | — | **true** | 常态 / 拆除中 |
| Constructing | true（投料中） | false | **true** | 投料中（在册前提下） |
| Constructing | false（料齐/纯金） | false | **false** | ⭐ 施工中 ⇒ **会被 `Tick:223` 清出** |
| Constructing | any | true | **true** | 拆除中（任意子型） |
| Ruined | false | false | **false** | 废墟（在册前提下） |
| Ruined | — | **true** | ⭐ **true** | **废墟拆除中** |
| Abandoned / Dead / Placing | — | true | true（Dead/Placing 进不来） | 拆除中 |

⇒ ⭐ **勘正结论**：

- **`Ruined` 不需要补进 `IsValid`**：只要"注册发生在 `_demolishing = true` 之后"（案甲天然满足），`Ruined + 拆除中` 的 `IsValid` **已经为 true**，`Tick:223` **不会**清它。任务书 B 的"Ruined 态可拆但 `IsValid=false`"只在**"尚未开拆"**时成立 —— 而那时它本来也不该在册（`EnterRuined:1207` 已显式注销，"不再是 Active 源"）。
- B 的**真缺口**在另一格：**「施工中（料齐/纯金）」⇒ `IsValid=false`**。它惩罚的不是案甲（案甲在拆除瞬间注册，届时 `_demolishing` 已 true），而是**案乙**：若按"进入投料态即注册"，则料齐瞬间 `_awaitingMaterials=false` ⇒ 下一 tick 被 `Tick:223` 清出 ⇒ **升级·施工中拆除照样卡死**。⇒ 案乙必须连 `IsValid` 一起补 `Constructing`，否则其注册是**一次性假象**。
- ⇒ 任务书"完整修法必须『注册守卫』＋『IsValid 状态集』同批改"：**仅对案乙成立**；**案甲不需要动 `IsValid`**。

### 1.4 落点 C · `Demolish()` 不注销工地仓源 —— ✅ 坐实（＋精确化）

| 证据 | 实读 |
|---|---|
| `Demolish()` 全文 | `Building.cs:848-857`：守卫 ×2 ⇒ `_demolishing = true` ⇒ `_demolishProgress = 0` ⇒ `UpdateVisual` ⇒ `Debug.Log`。⛔ **无 `UnregisterSiteStore()`**（现成方法在 `:562-567`） |
| 工地仓 `IsValid` | `ConstructionSiteStore.cs:163`：`_building != null && _building.IsSiteAwaitingMaterials && !IsSatisfied`；`Demolish()` 不清 `_awaitingMaterials`（唯一清点在 `OnSiteMaterialsReady:579`／`Die` 路径）⇒ 投料未齐时**仍 true** ⇒ 搬料广告继续（`:168-198`） |
| 双源面 | 本体（`_demolishing`）广告拆除任务（`Building.cs:1367-1373`）＋ 工地仓广告搬料任务（`ConstructionSiteStore.cs:168-198`）⇒ **同一实体两个有效源** |

⚠️ **精确化（任务书未区分，本端补）**：当前"双源同册"**只在「升级·投料中」拆除时物理可达** ——

- 首次建造／废墟重建的投料中：**本体根本没在册**（1.1）⇒ 只有工地仓一个源；
- 升级·投料中：本体因"此前 Active"**仍在册**（⚠️ 见 §二 表：这是今天**唯一**"非 Active 还能拆成功"的子型）⇒ **双源真实并发**。

⇒ ⭐ 即：**C 的危害面在"U-8 修好之后"才全面打开**（案甲把"任意可拆态拆除"变成"本体必在册"）。修批必须 C／U-8 **同批**，否则修完 U-8 立刻把"工人给正在拆的工地继续送料"从边角变成常态。

### 1.5 落点 D · 拆除任务工时错配 —— ✅ 坐实（量化附勘注）

| 证据 | 实读 |
|---|---|
| `workDuration` | `TaskScheduler.cs:43`：`public float workDuration = 2f;`（默认值；场景无覆盖） |
| 工期出口 | `TaskScheduler.cs:1287-1295`：仅 `Gather` 读 `GatherTaskArgs.gatherSeconds`，**其余一律 `return workDuration`** |
| Working 分支 | `TaskScheduler.cs:458` 用 `GetTaskDuration(task)` 判定；`:490-504` 只识别 `Build + HaulToSiteArgs`；`DemolishTaskArgs` 落 **`:505-509 else`** ⇒ `Complete()` **零动作释放** |
| 零动作确认 | `ExecuteCompletion`（`:615-675`）switch 只有 Production／Transport／WaterHaul／Gather ⇒ **无 Build** ✅（与"拆除由 `Building.Update` 自推"一致） |
| 目标值 | `BuildConfig.asset:18-20`：`constructionBaseSeconds: 5`／`cooperativeBuildK: 0.25`／**`demolishBaseSeconds: 6`** |
| `n` 来源 | `Building.cs:639`：`n = CountAssignedWorkers(this)`（`TaskScheduler.cs:152-159`：**源级计数、不计 state、不计 args 类型**） |

**量化（本端按代码时序推演，非实测）**：

- 拆除进度门控＝`HasAssignedWorker()`（`Building.cs:596`）＝"**源上存在任意在派任务**"（含 `Assigned`／`MovingToSource`／`Working` 全态），**不是**"工人正在 Working"；
- 单轮周期（工人驻场、tick=1s）：`T` 派发（门控 ON）→ `T+1` 转 Working → `T+3` Working 满 2s ⇒ `Complete` 释放（门控 OFF）→ **`T+4` 才可能再派**（`Tick` 内 `②空闲池` 先于 `⑤UpdateAssignedTasks`，本 tick 释放者赶不上本 tick 派发）；
- ⇒ **周期 4s／门控 3s ⇒ duty ≈ 0.75 ⇒ 6s 进度需 ≈8s 墙钟**（含相位抖动约 **7~8s**）；**≥2 轮完整派工**（每轮 2s Working ＋ ≥1 tick 空档 ＋ 一次完整"完成→释放→重派"流程）。
- ⚠️ **勘注（与任务书口径差）**：任务书"≥3 轮"是按"每轮 2s 有效劳作"折算；实际**进度门控把派工/走动段也算入**（每轮 ≈3s 计入）⇒ 本端模型为 **约 2 轮**。若工人不在场（需重新走动）或周围无空闲工人，每轮空档 **≥1 tick 起、可无限拉长**（进度只在有任务时推进 —— 无工人则**冻结**，属设计）。
- 结论：**"声称 6s／实耗 ≈7~8s（+17%~33%）＋ ≥2 轮派工 ＋ 全程进度条抖动"**；且 `DemolishDuration()` 的协作系数分支（`n≥2`）**正常路径不可达**（见 `Q5`／`E2`）。

### 1.6 附带项复核 · `ExecuteCompletion` 无 `Build` 分支 —— ✅ 本端同意"⛔ 不必改"

`ExecuteCompletion`（`:615-675`）无 `Build` case；`Complete()` 对拆除任务＝纯释放（`:557-567`）。拆除收口在 `Building.Update:594-606`（`_demolishProgress >= 1 ⇒ FinishDemolish`）✅。**修法与 D 无关**：⛔ 不要靠"给 `Build` 加分支"绕过 D（那会把"建筑自推"改成"调度器代推"，动契约面）。

---

## 二 · 触发面清点（机械扫全库 · ⛔ 非只答"新建工地"）

**扫描口径**：

1. `state = BuildingState.` 全库 grep ⇒ **9 命中**（`_Game` 内）⇒ `Active`：`Init:444`／`OnConstructionComplete:717`；`Constructing`：`StartConstructing:504`／`StartRebuildFromRuins:519`／`TryUpgrade:823`；`Ruined`：`EnterRuined:1199`；`Dead`：`Die:1250`；**`Placing` 全库零赋值**；`Abandoned` 仅 `BuildingFactory:80`（地图主城 `isPlayerBuilt: false`）＋ `:293-294` 读档重映射。
2. 拆除入口全库 grep ⇒ **仅 2 处**：`BuildingPanel.cs:397`（UI）＋ Editor 冒烟探针（非生产）。`CanDemolish`（`Building.cs:840`）**与 state 无关**（`isPlayerBuilt && def.isDestructible && !def.isResourceNode`）。
3. 注册面全库 grep ⇒ Building 本体只被 `Building.cs:1446`（Active）与 `BuildingFactory.cs:191`（Active）注册；注销面：`EnterRuined:1207`、`ReleaseLayerOwnedState:1237`、`Tick:223`（清源）。

**可变状态集（全量）＋ 拆除结局（现状）**：

| # | state | 子型（`_pending*`／`_awaitingMaterials`） | 本体在 `_sources`？ | `IsValid` | 拆除结局 |
|---|---|---|---|---|---|
| 1 | `Active` | — | ✅（`:720`／`Factory:191`） | true | ✅ 正常（`D799` 已验） |
| 2 | `Constructing` | 首次建造 · **投料中** | ❌ **从未注册** | — | ❌ **卡死（U-8 核心）** |
| 3 | `Constructing` | 首次建造 · **料齐施工中** | ❌ 从未注册 | — | ❌ 卡死 |
| 4 | `Constructing` | 首次建造 · **纯金（无投料）施工中** | ❌ 从未注册 | — | ❌ 卡死（窗口 ≈5s，可点即卡；`farm`/`quarry`/`market.asset` 均 `isDestructible: 1` 已实读） |
| 5 | `Constructing` | 升级 · **投料中** | ✅（Active 遗留，未注销） | true（`_awaitingMaterials`） | ⭐ ✅ **正常**（今天唯一"非 Active 可拆"子型） |
| 6 | `Constructing` | 升级 · **料齐施工中** | ❌ **料齐 tick 后被 `Tick:223` 清出** | false | ❌ 卡死 |
| 7 | `Constructing` | 废墟重建 · **投料中** | ❌（`EnterRuined:1207` 已注销） | — | ❌ 卡死 |
| 8 | `Constructing` | 废墟重建 · **料齐施工中** | ❌ | — | ❌ 卡死 |
| 9 | `Ruined` | — | ❌（`:1207` 注销） | false | ❌ 卡死（⚠️ 可达性见下） |
| 10 | `Abandoned` | — | ❌ | false | ⛔ **不可拆**（`isPlayerBuilt=false` ⇒ `CanDemolish=false`；＋ `castle.asset:55 isDestructible: 0` 双保险） |
| 11 | `Dead`／`Placing` | — | — | — | ⛔ 被 `Demolish:852` 拒绝 |

⇒ ⭐ **任务书"所有 `Constructing` ＋ `Ruined`"方向正确**，但本端补 3 条精确化：
**(i)** 子型必须拆开：**#5（升级·投料中）今天已能拆** —— 它**不是** U-8 受害者（⛔ 别把它算进修复范围，否则判据会失去鉴别力）；
**(ii)** `Ruined` 在**数据层**可拆，但 **UI 不可达**：`BuildingPanel.cs:79` 对 `Ruined` **显式隐藏拆除按钮**（`Abandoned` 同理 `:94`）⇒ 生产路径点不出来；
**(iii)** ⚠️ **边角可达**：面板**不订阅** `BuildingRuinedEvent`（`BuildingPanel.cs:208-221` 只订阅 `BuildingUpgraded`／`UnitDied`）⇒ "面板开着时建筑被打成废墟"⇒ **陈旧面板仍可点拆除**（`Refresh` 不重跑）⇒ `Ruined + Demolish()` 可达。⇒ 修法**不应依赖"UI 不可达"**。

**生产可达性（`Constructing` 全子型）** —— ✅ 成立，非构造法：

| 环节 | 实读 |
|---|---|
| 拾取 | `InteractionManager.cs:88-98`：`MapGate.PickWorld` → `GetComponentInParent<IInteractable>()` → `Interact`，⛔ **无 state 门** |
| 交互 | `Building.cs:793-797`：只拒非玩家阵营；⛔ 无 state 门（`IsInteractable:290` 全库 **1 命中＝定义本身**，⭐ **零消费者＝死读口**） |
| 面板 | `BuildingPanel.cs:58-96`：只对 `Ruined`／`Abandoned` 特判；`Constructing` 走通用段 ⇒ `:168-173` 拆除按钮按 `CanDemolish` 显示（`Constructing` 为 **true**） |
| 点击 | `:390-399 OnDemolishClicked` → `:397 _target.Demolish()` |

⇒ ⭐ **"玩家点开投料中的工地（脚手架）→ 面板 → 拆除 → 卡死"= 生产路径可复现**（⛔ 不属 `L-51` 构造法样本）。`D799` 的样本虽由构造法取得，**缺陷本身在生产路径成立** ✅。

---

## 三 · 修法对比（案甲／案乙）

### 3.1 案甲：注册时机就地扩展（`Demolish()` 内即时注册）

**形态**：`Demolish()` 在 `_demolishing = true` 之后（`:853` 后）补一句"未注册则注册"（可复用/扩展 `RegisterWithTaskScheduler`，或直接 `TaskScheduler.Instance.Register(this)` —— `Register` 天然幂等，`:118-122`）。

| 维度 | 评估 |
|---|---|
| 正确性 | ✅ **全状态覆盖**：注册发生在 `_demolishing=true` 之后 ⇒ 真值表（§1.3）任意 state 下 `IsValid=true` ⇒ `Tick:223` 不清、`UpdateAssignedTasks:403` 不弃 ⇒ 广告⇒派工⇒进度 链路全通。⭐ **不需要动 `IsValid`／`:95`／`EnterRuined`／任何状态集** |
| 代价 | **1 文件（`Building.cs`）· 2~3 行**（＋§六读档 1 处，合计仍 1 文件） |
| 副作用 | ① ⛔ 无遍历开销（本体只在"开拆→真拆"秒级窗口内在册）；② ⛔ 不漏广告：拆除中走 `:1367-1373` 提前 return，非拆除态根本不在册；③ 存续期：`FinishDemolish → Die → ReleaseLayerOwnedState:1237` 正常注销（⛔ 无残留）；④ 与 AI 池隔离无涉（`SourceKingdom` 按 `kingdomId`，`:1261`） |
| 覆盖盲区 | ⚠️ **读档路径**不覆盖（读档不会调 `Demolish()`）⇒ 必须补 `LoadState` 注册（§六）—— 该盲区**案乙同样有**（`BuildingFactory:189` 只收 Active） |
| 判据风险 | 低（改动面 = 1 个方法 + 1 个读档分支） |

### 3.2 案乙：注册语义整体放宽（`RegisterWithTaskScheduler` ＋ `:95` ＋ `IsValid` 状态集）

**形态（按任务书）**：注册条件改「`Active || 投料态 || 拆除态`」＋ `TaskScheduler:95` 同步 ＋ `IsValid` 补 `Ruined`（＋**必须**另有调用点把"进入投料态"接上，否则守卫再宽也没人调）。

| 维度 | 评估 |
|---|---|
| 正确性（按任务书清单） | ⚠️ **不完整**：① **`EnterRuined:1207` 显式注销** ⇒ `Ruined` **根本不在册** ⇒ 只放宽注册守卫＋`IsValid` 补 `Ruined` **无济于事**（要么连 `EnterRuined` 一起改"废墟仍留册"，要么仍需 `Demolish()` 内注册 ⇒ 退化成案甲＋多余件）；② **「施工中（料齐）」**：`_awaitingMaterials=false` ⇒ `IsValid=false` ⇒ 注册被 `Tick:223` 清 ⇒ **必须把 `Constructing` 整态补进 `IsValid`**（任务书只说补 `Ruined`）；③ **读档路径**：与案甲同缺 |
| 完整性判定 | 要真正达成案甲同等覆盖面，实际需动 **4 处状态集**（注册守卫／`:95`／`IsValid`／`EnterRuined` 或 `Demolish`）＋ 新调用点（`StartConstructing`／`BeginMaterialPhase`）⇒ ⭐ **多点耦合、未来任一处状态集漂移即复现同类缺陷**（正是 U-8 的成因模式） |
| 副作用 · 漏广告复核 | ✅ **不漏**（任务书担心项，本端坐实）：`TryAdvertiseTask:1377` 的 `state != Active ⇒ return false` 是结构性守门 —— 投料/施工态本体提前 return；`Ruined` 同样 return ⇒ **Production/Transport/WaterHaul 不会漏出** ✅。拆除中则走 `:1367-1373` 只出拆除任务 |
| 副作用 · 遍历开销 | **可忽略**（`Q6`）：本体在册期间 `Tick:223` 一次 `IsValid` 读 ＋ `:254 TryAdvertiseTask` 一次早退（≤2 个属性读）vs `tickInterval=1f` ⇒ **O(1)/tick/源**；真正的对比项是**工地仓源的 `FindPickup` 全仓扫描**（`ConstructionSiteStore.cs:208-223`，本就每 tick 跑）⇒ 本体这笔**比它轻数个量级** |
| 副作用 · 抖动 | ⚠️ **有**：投料中在册 → 料齐被清 → 完成后 `:720` 再注册 ⇒ 每个建筑施工全程 **≥2 次入册/清册**（含 `Tick:223` **不回调 `OnUnregister`** 的静默清，`ChestEntity.cs:148` 已记为 R5 同族） |
| 代价 | **2~4 文件**（`Building.cs`／`TaskScheduler.cs`＋视口径动 `ConstructionSiteStore`）· 十余行 ＋ 状态集维护权责扩散 |

### 3.3 结论

| | 案甲 | 案乙 |
|---|---|---|
| 覆盖 U-8 全触发面 | ✅（＋读档补注册） | ✅（但需**超出任务书清单**的 3 项补全） |
| 触碰状态集数 | **0** | **4** |
| 改动文件/行数 | 1／≈4 行 | 2~4／≈15 行＋ |
| 未来漂移风险 | 低（无状态集可漂） | **中高**（4 处须永远同步） |
| 收益差 | — | ⛔ 无增量收益（本体会话内广告与案甲**逐位相同**） |

⇒ ⭐ **推荐案甲**。案乙的"投料态本体在册"**没有任何消费者**（投料由工地仓广告、施工不广告、拆除由案甲覆盖）⇒ 纯成本。

---

## 四 · 碰头面（本体 ＋ 工地仓同册时的派工行为）

**场景**：`Demolish()`（案甲注册后 / 现状的升级·投料中）⇒ 本体（拆除任务）与工地仓（搬料任务）**同时在 `_sources`**。

| 问题 | 实读结论 |
|---|---|
| 谁先被选中 | 两者 `task.type` 同为 `KingdomTaskType.Build` ⇒ 死表同为 **A 级**（`TaskPriorityConfig.asset`：`taskType 1 → priority 3`；`TaskPriorityConfig.cs:35` 注释"Build → A"）⇒ 有效优先级**相同** ⇒ 走确定性次级键：**源坐标 y → x → 类型**（`TaskScheduler.cs:1132-1136`）。⚠️ 键是 `task.SourcePos`＝**源实时读口**：本体＝建筑位置（`Building.cs:1343`）；工地仓＝**取料仓位置**（`ConstructionSiteStore.cs:166 SourcePos => _pickupPos`，广告时解析）⇒ ⭐ **选序由"建筑 vs 取料仓"的坐标决定（无语义优先级）** —— 但两者**并非互斥**，只是同一 tick 内的排序 |
| 会不会重复派工 | ⛔ **同一工人不会**：① 同 tick 内 `used[]` 独占（`:273、:296`）；② 跨 tick，在派工人不在空闲池（`:242 _npcTaskMap.ContainsKey`）⇒ 一工人同时只持一任务 |
| 会否**并发**派两人 | ✅ **会**：两任务**源不同** ⇒ `HasAssignedTaskForSourceType(:1078-1083)` **按源过滤**，互不阻断 ⇒ 拆除 1 人 ＋ 搬料 1 人**可同时在场**（各 1 人：非 Transport 任务 `slots=1`，`:279-280`） |
| 去重维度 | **（source, type）二元组**（`:1078-1083`）；`Transport` 例外按容量（`:255-258、:280`）。拆除任务＝`(本体, Build)` ⇒ 全局**最多 1 个在派**；搬料＝`(工地仓, Build)` ⇒ 最多 1 个 |
| 结论 | 修完 U-8 若**不修 C**：拆除期间**另一名工人会持续向将拆工地送料**（送进去的料在 `FinishDemolish` 掉箱、再由工人搬回 ⇒ **双程浪费**；若恰好在拆除期间投满，见 §五"封口清零窗口"）。⭐ **C 与 U-8 同批 = 从根上消除该碰头面**（本体独占 ⇒ 无人送料） |

**残余面**（案甲＋C 后）：本体在册期间仍可能有**先前在派**的 `Production`／`Transport` 任务（源＝本体，`IsValid` 拆除中为 true ⇒ 不弃）⇒ 见 `E2`（`n`／门控误计），但**不会**再新增搬料任务 ✅。

---

## 五 · 落点 C 的处置（注销源 vs 保内容）

**问**：`Demolish()` 是否应 `UnregisterSiteStore()`？"注销源"与"内容物活到 `FinishDemolish → DropSiteStoreToChest`"是否冲突？

**答：应注销；⛔ 不冲突**（论证如下）：

| # | 论证 | 依据 |
|---|---|---|
| 1 | `UnregisterSiteStore()` **只做"从 `_sources` 移除 ＋ 清 `_siteRegistered`"**，⛔ **不动 `_items`** | `Building.cs:562-567` 实读；`ConstructionSiteStore.Clear()` 是独立方法（`:146`），**唯一内容清点**在 `OnSiteMaterialsReady:578` 与掉箱后 `:1000` |
| 2 | 内容物的**唯一掉箱口**在 `DropSiteStoreToChest()`（`:995-1006`），`FinishDemolish:877` 与 `Die:1259` 双入口 —— 与"是否在册"**正交** | `U-4` 分工注释 `:888-893`；`DropSiteStoreToChest` 只读 `_siteStore.Contents` |
| 3 | 注销后**在派搬料任务**的处置：下一 tick `UpdateAssignedTasks:403-413` 判 `!source.IsValid` ⇒ 放弃；⛔ 背包资源不退不丢（`:531` 注释同族"背包资源保留"）；下轮装载由混装防护 `:844` 先卸空 ⇒ **零丢料** | `TaskScheduler.cs:403-413`／`:844` |
| 4 | 与"保内容"的冲突面 = **无**：结论＝"注销源 ≠ 清内容"；`DropSiteStoreToChest` 保持不动即为唯一出口 ✅ | — |

⚠️ **本端补一处理论窗口（封口清零）** —— 支持"同批注销"＋可选 1 行硬化：

- 若**不注销**：搬料可持续到"投满阈值"⇒ `Deposit → IsSatisfied ⇒ OnSiteMaterialsReady()`（`ConstructionSiteStore.cs:141`）⇒ **`Clear()` 清空已到料**（`Building.cs:578`）。此时拆除中的退款口径恰会补上：`PaidStageCost` 的 `paid` 翻真 ⇒ 退款含**全额 `def.cost`**（`Building.cs:926`）⇒ **账面无净损**；但"料被 Clear、靠退款补"＝**语义绕行**（读码者/后续改退款公式者极易踩坑）。
- 若**注销**：广告止于 `Demolish()`；残余窗口＝"**同一 tick 内已在途且恰好落在下一次 `UpdateAssignedTasks` 之前到货**"的极小窗口（仍可能触发 `Clear`）⇒ 若要**封死**，加 **1 行**：`Deposit` 首守卫或 `OnSiteMaterialsReady` 首行加 `_demolishing` 判否（`ConstructionSiteStore.cs:135` / `Building.cs:576`）。
- ⭐ 本端建议：**注销（必做）＋ 硬化（建议，二选一，1 行）**；⛔ 不动 `DropSiteStoreToChest`／退款公式（属 `U-1`／`U-4` 域，已销号）。

---

## 六 · 读档路径（`LoadState`）

| 证据 | 实读 |
|---|---|
| 存档侧 | `Building.cs:1053-1058`：`siteNeed`／`siteContents`／`awaitingMaterials`／`demolishing`／`demolishProgress` **均已入档**（尾插零 bump） |
| 读档侧 | `:1112-1113` 恢复 `_demolishing`／`_demolishProgress`；`:1116-1123` 恢复工地仓并 `RegisterSiteStore()` ⇒ ⭐ **工地仓在册 ✅** |
| 缺口 | ⛔ **本体不注册**（`:1076-1149` 无任何 `TaskScheduler` 注册调用）；`BuildingFactory:189` 也只收 `initialState == Active` |
| 结果 | **读档时正在拆除的建筑**：若存档态＝`Active` ⇒ `Factory:191` 已注册 ⇒ 拆到一半读档**能继续** ✅；若存档态＝`Constructing`／`Ruined`（即 §二 表 #2~#8 任一子型在拆）⇒ **本体不在册** ⇒ `Demolish()` 的幂等守卫（`:851 if (_demolishing) return;`）使玩家**再也点不动** ⇒ ⭐ **永久冻结**（比"卡死"更糟：连重试都没有） |

⇒ ⭐ **必须补**：`LoadState` 恢复 `_demolishing` 之后（`:1113` 后）加"**若 `_demolishing` ⇒ 确保注册**"。形态与案甲同源（同一句即可复用）；⛔ **不要**把 `BuildingFactory:189` 的守卫改成"读 `data.demolishing`"（把存档字段语义塞进工厂，职责串层）。

---

## 七 · 须报裁的口径（`Q1`~`Q7` · 逐条建议 ＋ 理由）

### `Q1` 注册时机：案甲 vs 案乙
**建议：案甲**。理由：① 全状态覆盖且**不需要碰任何状态集**（§1.3 真值表证明 `_demolishing` 自足）；② 改动 1 文件 ≈4 行 vs 案乙 2~4 文件 且**必须超出任务书清单补 3 项**（`EnterRuined`／`IsValid` 补 `Constructing`／读档）；③ 无遍历开销与入册抖动；④ 案乙引以为据的"投料态本体在册"**零消费者**（投料由工地仓广告；施工期不广告；拆除由案甲覆盖）⇒ 纯成本；⑤ 状态集多点同步正是 U-8 的**成因模式**，案乙把同类风险从 1 处扩到 4 处。

### `Q2` `TaskScheduler:95` 补注册守卫是否同步放宽
**建议：⛔ 不放宽**（不同批）。理由：① 案甲下该循环与拆除无关；② 该循环只在**单例创建瞬间**跑一次，对"读档重建"（单例早已存在）**本就无效**（§1.2）；③ 若未来走案乙 ⇒ **必须**同步（否则"注册语义分裂"）—— 即：**它不是 U-8 的修复件，而是案乙的附带件**。④ 附注（⛔ 不动）：`:95` 的 `!_sources.Contains(b)` 对幂等 `Register` 冗余。

### `Q3` `Demolish()` 是否注销工地仓源（落点 C）
**建议：✅ 应注销**（`Demolish()` 内显式调 `UnregisterSiteStore()`）。理由：① 语义一致（"拆除中只广告拆除任务"本已是本体侧口径，`:1365-1373` 注释自陈）；② 消除碰头面（§四：工人给将拆工地送料＝送料＋回搬**双程浪费**）；③ **与"保内容"不冲突**（§五 四条论证：注销不动 `_items`，掉箱口正交）；④ 修完 U-8 后该面从"边角"变"普遍"⇒ **同批必做**。⭐ 建议加 1 行硬化（`_demolishing` 判否，封"投满清零"窗口，二选一落点）。

### `Q4` 拆除任务工时：`GetTaskDuration` 是否对 `DemolishTaskArgs` 返回 `DemolishDuration()`
**建议：✅ 改**，且**照 `GatherTaskArgs.gatherSeconds` 先例同形照搬**（源侧填 `args` 字段 → 调度器 `:1289-1293` 同形读，`gatherSeconds` 先例见 `WorldGatherSource.cs:112-116` ＋ `RespawnConfig.cs:54-66`）。**若 ⛔ 不改，量化差距**：声称 6s ⇒ 实耗 **≈7~8s（+17%~33%）**、**≥2 轮完整派工**、每轮 ≥1 tick 进度停摆（§1.5 模型）；协作系数 `n≥2` 分支正常路径不可达（`Q5`）。
**落地形态（二选一，均先例一致）**：**(i) 快照**：`DemolishTaskArgs` 加 `demolishSeconds`，`Building.TryAdvertiseTask:1371` 处填 `DemolishDuration()`；**(ii) 活读**：`GetTaskDuration` 内 `if (task.args is DemolishTaskArgs da && da.target != null) return Mathf.Max(0.01f, da.target.DemolishDuration());`。二者都需保留 `≤0 ⇒ workDuration` 兜底（照 `:1292` 形制）。⚠️ 附注：慢通道副产物 —— 改后拆除**多数情况下由 `FinishDemolish` 先收口**（进度门控含派工期，比 Working 早 ≈1 tick 完成）⇒ 任务被 `OnBuildingDied` 放弃＝**预期行为**（与"建筑自推"口径一致）✅。

### `Q5` 拆除允许多少工人协作（`n` 上限）＋ 协作系数 `k` 语义
**建议：维持 1 人；`k` 在拆除侧形式上不成立，随批加注（或收紧 `n` 来源）。** 理由：① 现状派工对非 `Transport` 任务 `slots=1`＋按 `(source, type)` 去重（`:279-280、:1078-1083`）⇒ **同时最多 1 个拆除任务** ⇒ `CountAssignedWorkers`（源级）在该源上正常情况下 **n=1** ⇒ `DemolishDuration` 协作分支**恒不触发**（死分支）；② ⚠️ 例外：`n` 会把**同名源的残留任务**（拆除前已在派的 `Production`／`Transport`）计入 ⇒ `n` 可能瞬时 **2~3** ⇒ （a）`DemolishDuration` 被**无关工人**加速、（b）`HasAssignedWorker` 门控把"未在拆的工人"也算"有工人到场"（`E2`）；③ ⭐ **契约依据**：`09` §16.3-3／§16.3.1 只定"要时间与工人／工人侧按进度推进"，⛔ **未定工人数上限，也未定单位工人一次干多久** ⇒ 属**口径空缺**（任务书自陈）；④ ⇒ 本批建议：**明示"拆除＝单人"**（加注即可；若要更干净可选 1 行收紧：`n` 只计 `DemolishTaskArgs` 的任务）＋ **⛔ 不做多工人**（"与建造对称"当前也非多工人对称：建造 `EffectiveDuration` 的 `n` 在施工期同样取不到 >1，`Building.cs:192`）；多工人协作＝**另立小片**（需改派工去重／量纲，属行为变化）。

### `Q6` 投料态本体在册的遍历/广告开销是否可接受（对照 `tickInterval = 1f`）
**建议：开销可接受但⛔ 无必要。** 数据：单源 `Tick` 成本＝①`IsValid`（1 属性）＋③`TryAdvertiseTask`（非 Active 早退，`≤2` 个判断）⇒ **O(1)/tick/源**；工具基准对比：同一 tick 里工地仓的 `FindPickup` **扫 `WarehouseRegistry.GatherActive` 全表**（`ConstructionSiteStore.cs:208-223`）——本体这笔比它**轻数个量级**。⇒ ① 若走案乙：**性能不是否决理由**；② 真正的代价是**状态集一致性维护（4 处）＋ 清册/入册抖动（每建筑 ≥2 次，含 `Tick:223` 静默清不回调 `OnUnregister`）**；③ 案甲下该开销 **≈0**（本体只在秒级拆除窗口在册）。⇒ 维持 `Q1` 结论。

### `Q7` 与 `U-5`（`_pendingUpgrade`／`_pendingRepair` 未入档）的交叉
**建议：交叉成立（窄面）⇒ `U-5` 与 `U-8` 同批（或至少在 U-8 批登记该触发面）。** 推演（读档后拆"投料中的建筑"）：

| 在投阶段 | 读档标志 | `InProgressStageIsBuild()` 兜底 | 退款口径结果 | 判定 |
|---|---|---|---|---|
| 首次建造 | 无需标志（`level=1` ＋ `_siteNeed≈def.cost`） | ✅ 正确判为 build | `def.cost`（料齐后）／`GoldOnlyOf(def.cost)`（投料中） | ✅ 正确 |
| **升级** | `_pendingUpgrade` 丢失 | ✅ 兜底仍判"非 build"（`_siteNeed` ≠ build pack，`:966-968` 自陈 0 碰撞实测） | `def.cost` ＋ `Σ升级` ＋（在投级）`upgradeCost` | ✅ 正确 |
| ⭐ **废墟重建** | `_pendingRepair` 丢失 | ❌ **兜底无法区分"修复"与"升级"**（`:970-976` 只判 build/非 build） | `PaidStageCost`：`isRepair=false` ⇒ **修复费不参与**；`CurrentStageCost`：走 `def.levels[level-1].upgradeCost` ⇒ **按升级价退金**；料齐后 `n++` 同错 | ⚠️ **有界偏差**（金路口径错配；非金修复费缺失/误加） |

⇒ ① **根源在 `U-5`**（`_pending*` 未入档），**不在 U-8**；② ⭐ 但**今天该偏差不可达**（读档后的在投修复建筑 = 卡死 ⇒ 拆不动）⇒ **修完 U-8 才使其可达** ⇒ **必须同批（或同批登记＋判据覆盖）**；③ ⛔ **不在 U-8 内改退款公式**（`PaidStageCost`／`CurrentStageCost` 属 `U-1`／`U-5` 域，`U-1` 已 `D796` 销号；乱动＝重开已收口件）；④ 读档路径的**注册**修复见 §六（与退款偏差是两件事，别混改）。

---

## 八 · 本端新增发现（⛔ 不顺手改 · 逐条列报）

### `E1` ⭐⭐ 拆除中被打成废墟 ⇒ `_demolishing` 闩锁 ⇒ **重建后永久卡死**（静态必现）

| 环节 | 实读 |
|---|---|
| 拆除**不改** `state` | `Demolish():848-857` 只置 `_demolishing`；`Active` 建筑拆除期间**仍是 `Active`** ⇒ `TakeDamage:1180-1191` 照常受伤 |
| 打空 ⇒ 废墟 | `:1184-1189` ⇒ 非工事 `EnterRuined():1197-1212` ⇒ `state=Ruined` ＋ `:1207 Unregister(this)`（⚠️ 源被撤 ⇒ 拆除进度冻结，但 `_demolishing` **仍 true**） |
| 闩锁无解 | `Update:594-606`：`_demolishing` 分支**优先于** `Constructing` 分支；`Demolish():851` 幂等 early-return ⇒ **无任何清 `_demolishing` 的路径** |
| 后果 | 玩家面板（`Ruined` 分支 `:70-81`）点"重建废墟" ⇒ `StartRebuildFromRuins:514-523`（`state=Constructing`,`_awaitingMaterials`…）⇒ ⭐ `Update` **永远走 `_demolishing` 分支**，且 `HasAssignedWorker()` 恒 false（本体不在册）⇒ **重建立即永久冻结**（进度条显示的还是 `_demolishProgress`，`:654-655`） |

**判定**：`U-8` 邻域缺陷（拆除态 × 生命周期），**非本片引入**（件4 落地即存在）；修 U-8 不必然触发它，但"拆除⇒注册⇒有工人推进"会**拉长拆除窗口**（从近乎瞬时变成 ≥6s）⇒ ⚠️ **被打成废墟的机会显著上升** ⇒ 建议**同批处置**（最小修：`EnterRuined` 内或 `Demolish` 的互斥口径二选一，须报裁）。⛔ 本端只列报，**不预设修法**。

### `E2` ⭐ `n` 与门控的语义（源级计数 ⇒ 误计 ＋ 门控含"派工未到场"期）

- `CountAssignedWorkers`（`:152-159`）＝**源级、无 state/args 过滤** ⇒ ① `DemolishDuration` 的 `n` 可被无关残留任务抬高（`Q5`）；② `HasAssignedWorker`（`Building.cs:626-627`）门控在 **`Assigned`／`MovingToSource` 期就为真** ⇒ "**有工人到场**"实为"**有人接单**"（工人可能还在路上）⇒ 进度在走动途中推进（对照 `:591-593` 注释"有工人到场"）。**建议**：随 U-8 批**加注**；若要精化（门控改 `Working`／`n` 按 `DemolishTaskArgs` 过滤）⇒ **另报裁**（属行为变化，会改拆除手感）。

### `E3` `Ruined` 数据层可拆 vs UI 隐藏（口径分裂）＋ `IsInteractable` 死读口

- `CanDemolish:840` 对 `Ruined` 为 **true**，但 `BuildingPanel:79` **隐藏**拆除按钮（`Abandoned` 同理 `:94`）⇒ 两个"可否拆"口径**不一致**；`Building.CanDemolish` 注释自陈"与 `Demolish()` 守卫同源"，但**未含 UI 态门** ⇒ 建议报裁：要么"数据层→UI 层"统一（例如 `CanDemolish` 加 `state != Ruined`），要么 UI 放开。⚠️ 与 `E1`／边角可达（陈旧面板）叠加时会**实际触发**。
- `IsInteractable`（`:290`）**全库 1 命中＝定义本身** ⇒ **零消费者**（死读口）；交互实际**无 state 门**（§二 生产可达性表）⇒ 它是否该在 `InteractionManager` 侧接入，属独立小口径（⛔ 不属 U-8）。

### `E4` `Tick:223` 清无效源**不回调** `OnUnregister`（既有 `R5` 同族 · 留痕备查）

- `Tick:223` 直接 `_sources.Remove`（无 `OnUnregister`／`OnBuildingDied`）；`ChestEntity.cs:148` 已独立记为 R5 同族并声明"清算走显式钩子"。⚠️ 与本案的交点：**案乙**下"料齐被清"即为本路径（静默）；**案甲**不依赖该路径做清算（拆除走 `Die → ReleaseLayerOwnedState` 显式注销）⇒ 佐证 `Q1`。

---

## 九 · 验证/回归建议（供施工批 · 判据草案 ＋ 并入 `D799` §四三项回归）

| # | 判据（⛔ 生产路径，拒绝构造法 · `L-51`） | 鉴别力声明 |
|---|---|---|
| 1 | **首次建造·投料中**：真 `BuildController` 下单 ⇒ 面板点拆 ⇒ **有工人到场** ⇒ `_demolishProgress` 0→1 ⇒ 真拆（掉箱＋退还） | 改前：进度**恒 0**（本报告 §二 #2） |
| 2 | **首次建造·料齐施工中**：投满 ⇒ 施工中拆除 ⇒ 同上 | 改前恒 0（#3）；同时验 `IsValid` 清源面（案乙相关） |
| 3 | **升级·投料中**（回归 · ⭐ 改前**已通过**）⇒ 拆除正常（防"修复引入回归"） | 改前已过 ⇒ 只作回归，⛔ 不得当作 U-8 判据 |
| 4 | **废墟重建·投料中/施工中** ⇒ 拆除正常 | 改前恒 0（#7/#8） |
| 5 | ⭐ **读档续拆**：拆除中存档（态＝`Constructing`）⇒ 读档 ⇒ 建筑仍在拆且**能继续到 0→1** | 改前：永久冻结（§六）；⭐ 与 `U-5` 交叉件（修复在投）单列读数 |
| 6 | **C 面**：投料中拆除期间 ⇒ 工地仓**不再**广告搬料（无新送料任务）；已到料仍随 `FinishDemolish` 掉箱（`U-4` 判据 8′ 复跑） | 改前：持续送料（§四） |
| 7 | **D 面**：`Demolish()` → 真拆 **≈6s（±1 tick）**、**≤2 轮派工** | 改前：≈7~8s、≥2 轮（§1.5） |
| 8 | **`E1` 面**（若同批处置）：拆除中打空 ⇒ 行为按裁决（⛔ 本端未裁，仅登记） | — |

⭐ 并按 `D799` §四把 **①`HH.315 M1-C` 冒烟（`U-4` 判据 8′）②`2_20B_M7` ③1× 时基跑批**并入本补丁批。

---

## 十 · 红线自证

- ⛔ `Assets/**` **零写入**（本轮全程 Read／Grep＋资产 YAML 直读）；⛔ 未开工；⛔ 未 push；⛔ 未代提交策划端账本。
- 交付面：本报告 1 文件（`多Agent交接/执行端/HH.315_M1-C-U8评估_评估报告.md`）。
- ⚠️ **无阻塞歧义**（`Q1`~`Q7` 均有推荐口径且互不冲突）⇒ 未触发"先停手报裁"；唯 `E1`／`E2` 精化／`E3` **属新增列报，须策划端裁**。

---

## 附 · 关键实读索引（file:line）

| 件 | 落点 |
|---|---|
| 注册守卫 | `Building.cs:1440-1447`（守卫 `:1445`）；唯一调用 `:720` |
| 单例补注册 | `TaskScheduler.cs:84-98`（守卫 `:95`） |
| `IsValid` | `Building.cs:1349-1350`；清理 `TaskScheduler.cs:223` |
| `Demolish()`／`FinishDemolish` | `Building.cs:848-857`／`:867-879` |
| 拆除进度自推 | `Building.cs:589-623`（分支 `:594-606`、`:598`）／`:626-642` |
| 工地仓源 | `ConstructionSiteStore.cs:163`／`:168-198`；注册/注销 `Building.cs:556-567` |
| 内容掉箱 | `Building.cs:995-1006`（`FinishDemolish:877`／`Die:1259`） |
| 工时 | `TaskScheduler.cs:43`／`:1284-1295`；Working 分支 `:441-511`（`:505-509` else） |
| 拆除任务 args | `KingdomTask.cs:89-97`；广告 `Building.cs:1365-1373`；广告守门 `:1377` |
| 去重/派工 | `TaskScheduler.cs:247-262`／`:273-298`／`:1078-1083` |
| 读档 | `Building.cs:1053-1058`（存）／`:1112-1123`（读）；`BuildingFactory.cs:189-193` |
| 面板/交互 | `BuildingPanel.cs:70-81`（Ruined 隐藏 `:79`）／`:85-96`／`:168-173`／`:390-399`；`InteractionManager.cs:88-98`；`Building.cs:290`（死读口）／`:793-797` |
| 配置 | `BuildConfig.asset:18-20`（6s／k=0.25）；`TaskPriorityConfig.asset`（Build→3） |
| 契约 | `09_资源与仓库.md` §16.1（`:585-600`）／§16.2（`:602-607`）／§16.3（`:609-633`） |
