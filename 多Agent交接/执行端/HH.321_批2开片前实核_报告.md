# `HH.321` **段 B · 批 2 开片前实核**（只读评估 · 报选 · 停手待裁）

- **执行端**：TraeCode｜**日期**：2026-09-23｜**依据**：`多Agent交接/策划端/HH.321_散布配平与批2开片前实核_任务书.md` §段 B（`D849`）｜口径真源：`07_伤害层.md` §四/§五/§七 ＋ `03_地图即数据库.md` §7.3/§7.6
- **基线**：`0b373546`（`git log --oneline -3` 实对齐）｜⛔ 未 rebase／未 reset／未 push
- ⛔ **本段 `Assets/**` 一行未写**（只读）；运行期读数经 bridge 脚本（⛔ 不落 Assets）；产物落 `Logs/`（未入版本控制）
- ⛔ **批 2 本体（`M3-C` ＋ `DZ-4`）本段未施工**（授权 ⟸ 待本报告报裁后由策划端放行）

---

## §0 · 一句话结论 ＋ ⚠️ 三处与文档不一致的实读差异

**`M3-A`／`M3-B` 确认已落地**（`M3-A` 为**生产链实测**：cap=1 时每帧判定被压到 ≤1 且队列只增不减、cap=0 时**零判定 ＋ 队列无界增长**、cap=100 一帧清空）；**`M3-C` 未落地**（`Building.cs:1398 Destroy(gameObject)` 仍在 `Die()` 内；⛔ 且 **`07` §五 所指的「建筑实体级 `Remove` 门」在实盘并不存在** —— 现有 `MapGate.RemoveResourceNode` 是**地表资源格**删门）；**`DZ-4` 确认现场成立**（`DamageSystem.cs:280` 对接口类型判 `== null` ⇒ 假 null 漏网；同款面全库 **30 接口 / 87 处**）。

| # | 文档说法 | 实读 | 影响 |
|---|---|---|---|
| 1 | `07` §四 引 `Building.cs:805-811`／`:839-843`「抄两遍」 | 实为 **`:1364-1399`**（`Die()`）；且「抄两遍」**已被 `HH.294` 片4·4-D 合一**为 `ReleaseLayerOwnedState:1336-1356`（唯一实现）⇒ 该差异项**已闭合** | 行号漂移 ＋ 问题已消解（剩余的是"走门/不自 Destroy"） |
| 2 | 任务书 §B3④ 前置＝「底层批 2-D（`Remove` 门）」 | `中层执行计划.md:225` 定前置；`试验底线台账.md:3876` 记「批 2-D **已闭 ✅**」；门本体实读＝**地表级**（`MapGate.RemoveResourceNode:351`）⇒ **前置满足**，但**建筑实体门仍为空位** | ⇒ **未触发停手条件**；形状方案见 §三③ |
| 3 | `LifecycleAudit.cs:136` 列 `BuildingDestroyedEvent` | 该处为 **`string[]` 基线事件名种子**（`:37 WhitelistSeedNames`）＋ `:222` 报告项「基线有·扫描无（缺失/退役）」⇒ **字符串字面量，非类型引用** | ⇒ `M3-B` 判「零引用」**成立**（口径见 §二） |

---

## §一 · `M3-A` 分帧分片（确认已落地）

### 1.1 声明 / 来源 / 可调性（码面实读）

| 项 | 实读 |
|---|---|
| 声明 | `DamageSystem.cs:66  private int MaxAttacksPerFrame => _config.maxAttacksPerFrame;`（**属性 ⇒ 转发到 SO 字段**） |
| 字段源 | `DamageConfig.cs:23  public int maxAttacksPerFrame = 100;`｜资产 `Resources/Config/DamageConfig.asset:17  maxAttacksPerFrame: 100` |
| 可调 | ✅ **SO 可调（⛔ 非 `const`）**：改资产即生效；运行期实读 **100** |
| 用法 | `:260 int count = Mathf.Min(_pendingAttacks.Count, MaxAttacksPerFrame);` → `:261-264` 执行 `count` 次 → `:267-270` 剩余 `RemoveRange(0, count)` 推下帧 |
| 收集 | `:247-254 CollectPendingAttacks()`：**每 tick**（`TickInterval`=0.1s，`:137-141`）把 `Time.time >= nextAttackTime` 的注册入列；`:204` 首发由 `RegisterAttack` **即时** `ExecuteAttack`（⛔ 不经队列 ⇒ **不受 cap 约束**） |

### 1.2 运行期读数（⭐ 生产链 · 产物 `Logs/hh321b_m3a2_20260923_192202.txt` · 末行 `# 封存 19:22:02`）

**构造**：test-run（seed 20321 · 15×）｜隔离场址（12 格无敌）｜1 射手（PlayerCamp Archer · `NPCBrain` 关）＋ 5 靶（AiKingdom Warrior · brain 关）｜生产口 `DamageSystem.RegisterAttack` 连发 5 次｜逐帧 pin（消漂移）。
ℹ️ 口径说明：`_registrations` **按攻方唯一** ⇒ 同射手连发 5 次只留**最后一条**注册（target=第 5 靶）⇒ 队列里只有这 1 个攻方的重复入列（读数因此表现为"单注册积压"）。

| 档位 | 逐帧 `_pendingAttacks.Count` | 靶 HP（5 靶） | 结论 |
|---|---|---|---|
| **cap=1** | `2 → 4 → 6 → 8 → 10 → 12`（**只增不减**） | 6 帧内仅 1 次落伤（t5 `110→106`），余 4 靶全程 110 | ✅ **每帧判定 ≤1**（cap 生效）；⚠️ **积压** |
| **cap=0** | `15 → 18 → 21 → 24 → 27 → 30`（**+3/帧 无界增**） | **5 靶全程 110（零判定）** | ⛔ **停摆**（非"关闭"） |
| **cap=100**（还原） | `0,0,0,0,0,0`（**一帧清空**） | t5 `110→90→86→82→78`（落伤恢复） | ✅ 复原 |

**⇒ 「分片参数置 0」是否等价关闭：⛔ 不等价**（`Mathf.Min(n, 0) = 0` ⇒ 永零执行；且 `:267` `count(0) < Count` ⇒ `RemoveRange(0,0)` ⇒ **不排空**，而 `CollectPendingAttacks` 每 tick 把**仍然到期**的注册**重复入列** ⇒ 队列**无界增长**）。回退路径应为 **`= 100`（或 ≥ 当帧到期数）**，⛔ 不得用 0。
⚠️ 附带读数（不阻断）：分片只压**每帧判定数**、**不解决积压**（cap=1 时队列净增）⇒ 见 §六 待裁 5。

---

## §二 · `M3-B` 旧事件退役（确认已落地）

| 项 | 实读 |
|---|---|
| **类型声明** | `git grep -n "class BuildingDestroyedEvent\|struct BuildingDestroyedEvent" ⇒ **0 命中**` ⇒ ⭐ **类型已删除**（最强证据） |
| `GameEvents.cs` | `:497-498` 注释「**已删**」＋先决证据（`Subscribe<…>`=0 ／ `new …`=0）｜`:125` 注释 |
| 其它生产码 | `Building.cs:24/1360/1389`（注释：改发 `UnitDiedEvent`）｜`BuildingPanel.cs:302`（注释） |
| Editor 面 | `LifecycleAudit.cs:136` ＝ **`string[]` 基线名种子**（`:37 WhitelistSeedNames`；用途＝`CollectEventTypes` 反射扫描 vs 基线**对账报告**，`:222` 输出「基线有·扫描无（缺失/退役）」，**非门禁**）⇒ ⭐ **口径：字符串字面量 ⛔ 不计为引用**｜`R5_SixStage.cs:327`（注释；该验证器已**剔注释再扫** `:330`）｜`Valley_HH294_Slice4Probe.cs:17`（探针文档注释） |

**⇒ 零引用成立**（口径已声明）。

---

## §三 · `M3-C` 建筑致死走 `Remove` 门（⭐ 唯一待做本体 · ⛔ 未落地）

### ① `Building.Die()` 现状全文（`Building.cs:1364-1399`）

```
:1364 public void Die(DeathCause cause = DeathCause.Killed)
:1366   state = BuildingState.Dead;
:1369   EscapeWorkers();                                  // 在册工人逃出存活
:1374   UnregisterSiteStore();  :1375 DropSiteStoreToChest();
:1379   DropStorageToChest();   :1380 DropVaultToChest();  // 掉箱三路（工地/产出/国库）
:1383-84 TrainingSystem.OnBuildingDestroyed(this);
:1387   ReleaseLayerOwnedState(unregisterFromRegistry: true);   // ← 本层注销（唯一实现）
:1390-96 EventBus.Publish(new UnitDiedEvent(this, faction, pos, killer, cause));
:1398   Destroy(gameObject);                              // ⛔ 判据 ⑦ 未达成
```
`ReleaseLayerOwnedState`（`:1336-1356`）＝ `03` §7.3 的**步骤 2/4/5**：`:1339-40 锚点返还(MapGate.ReturnAnchor)` → **`:1345 GridSystem.FreeFootprint(coord, footprint)`** ＋ `:1346-47 isBridge ⇒ SetBridge(false)` → `:1351 BuildingRegistry.Unregister` → `:1353 TaskScheduler.Unregister` → `:1355 SaveManager.UnregisterSaveable`。
**调用面**：`Building.cs:921`（拆除终点：`FinishDemolish → Die(Demolished)`）｜`:1289`（**工事被破 ⇒ `Die(Killed)`**；非工事走 `EnterRuined`）｜Editor 探针 3 档。

### ② `MapGate.RemoveResourceNode`（`MapGate.cs:351-366`）

- **签名**：`public static bool RemoveResourceNode(GridCoord coord)`；**返回**＝是否实际删除。
- **语义**：`03` §7.3 删门的**地表资源格**实现 —— 存在性校验（`IsRemovableResourceFeature`：`Tree/Mine/OreVein/WoodPile/StonePile`）⇒ `SetFeature(coord, Plain)` ＋ `grade` 复位 ＋ `GuardDeploymentSystem.HandleResourceConsumed`（守卫失去）。注释 `:13`「**唯一入口，不区分死因**」。
- **幂等**：✅ 非可删资源格 ⇒ `return false`（"删两次等于删一次"，`:348/§7.4`）。
- **现有调用面**：`MapGate.ConsumeAnchor:422`（锚点消费走删门）｜`ResourceRespawnSystem.HandleCellGathered:519`（采集完成）｜`WorldManager.cs:249-251` **注释**（原 `TryConsumeResourceNode` 已删 ⇒ 唯一入口）。
- ⭐ **实读判定**：该门管 **`map.features` 的地表格**（资源点 ⇒ Plain），**⛔ 不是建筑实体门**（建筑占格在 `_occupants`／走 `GridSystem.FreeFootprint`；建筑所在格 `features` 不变）⇒ **「建筑实体级 `Remove` 门」在实盘不存在**。

### ③ ⭐ 形状方案（⛔ 只报 · 不施工）

**形状 1（推荐 · 本层门 ＋ 高级层收尾分离 · 对齐 `03` §7.3/§7.5/§7.6）**
- 新增**建筑实体级门**（建议落点：`BuildingFactory.RemoveBuilding(Building b, DeathCause cause)` —— 与放置口 `CreateBuildingInstance` 同族，`BuildingFactory.cs:89`）。
- **门内** ＝ `03` §7.3 步骤 2/4/5（锚点返还 → 释放占格 → 注销[注册表/调度器/Saveable]）＝ **即现 `ReleaseLayerOwnedState` 的全部内容** ⇒ **`ReleaseLayerOwnedState` 去留：保留**（由 `private` 改为「门内实现」——由门调用／或上提为门的方法体）⇒ 语义一处不丢、`HH.294` 片4 的"唯一实现"结论不变。
- **高级层收尾**（`03` §7.6 清单）＝ 现 `Die()` 的 `EscapeWorkers ＋ 掉箱三路 ＋ TrainingSystem ＋ UnitDiedEvent` ⇒ 归**门的调用方（`Die()`）**（或门内"前段"，二者取一 — 见 §六 待裁 3）。
- **`Destroy(gameObject)` 由 `Die()` 移出**（门尾执行）⇒ 判据 ⑦「不自 `Destroy`」**结构上达成**（⛔ 非字面绕开）。
- `DeathCause` 保持为**参数**（`03` §7.5「本层不判能不能删；为什么删是调用者的语义」）。

**形状 2（最小改动 · ⛔ 不推荐）**：仅把 `:1398 Destroy(gameObject)` 移出 `Die()` ⇒ 判据 ⑦ 字面达成，但**未收口**（本层注销仍挂在建筑自己身上）⇒ 违反 `07` §五「由 `Remove(目标, 死因)` 统一处理」的意图。

**形状 3（越层 · ⛔ 否决）**：让 `MapGate` 直接承载建筑实体删除 ⇒ `03` §7.5「本层只有一个 `Remove`」指的是**地表格**；建筑实体属高级层 ⇒ 越层。

**`EnterRuined`（`:1299-1318+`）受影响否：⛔ 不受影响**（实读：只置 `Ruined` ＋ `_demolishing` 复位 ＋ `EscapeWorkers` ＋ `TrainingSystem.OnBuildingDestroyed` ＋ `TaskScheduler.Unregister`；**保持占格**（`D154` 阻挡持续）＋ **不注销 `BuildingRegistry`** ＋ **不发 `UnitDiedEvent`**＋"仓留存"负向判据见 `:1377-1378`）⇒ M3-C 只改 `Die()` 路径，⛔ 不得牵动废墟链。

### ④ 前置核实（「底层批 2-D（`Remove` 门）」）

`中层执行计划.md:225`（M3-C 前置＝底层批 2-D）｜`:130`（`M3` 依赖 `7-C 需批 2-D`）｜`试验底线台账.md:3876`「批 2-D **已闭 ✅**」｜门本体实读见 ②。
⇒ ⭐ **前置已满足**（未触发停手条件）；⚠️ 但**前置交付的是地表格门**，建筑实体门为空位 ⇒ 形状 1 即补此位。

### ⑤ `07` §七 判据 ⑦ 原文 ＋ 达成度

> `07_伤害层.md:128`：**7. 建筑销毁走门**：建筑被打爆 ⇒ 走 `Remove` 门（**不自 Destroy**）

**当前达成度：⛔ 未达成** —— `Building.cs:1398 Destroy(gameObject)` 仍在 `Die()` 内；本层注销虽已**合一**（`ReleaseLayerOwnedState`，`HH.294` 片4），但形态是"建筑自己干"而非"过门"。

---

## §四 · `DZ-4` `DamageSystem` 假 null 守卫（⭐ 高优）

### ① 现场（码面实读）

`DamageSystem.cs:275 private void ExecuteAttack(IDamageable attacker)` ⇒ `:280 if (attacker == null)` ⇒ 分支体 `:282-283 _registrations.Remove(attacker); return;`。
- `attacker` **静态类型 ＝ `IDamageable`（接口）** ⇒ Unity 的 `operator ==` 重载**只对 `UnityEngine.Object` 静态类型生效** ⇒ 对"已销毁 MonoBehaviour 但接口引用非 null"该判定为 **false** ⇒ **守卫生效不了**。
- 后果路径：`:289 animUc.NotifyAttackVisual()` ／ `:305 attacker.GetPosition()` ⇒ ⚠️ `MissingReferenceException`；该函数自己的注释 `:277-279` 已自陈「THD R3 实测 **4586 次/轮**」（⛔ 历史计数，非本端实测）。
- **假 null 面成立性**：`IDamageable` 实现者 **3 个**＝`Building.cs:29`／`Portal.cs:8`／`UnitController.cs:24` ⇒ **全为 `MonoBehaviour`** ✓。

### ② 机械扫描：全库「接口类型上做 `== null` / `!= null`」（原始输出 `Logs/hh321b_iface_null_scan.txt`）

**扫描法**：枚举生产＋Editor 全部 `interface I*`（**30 个**）⇒ 每文件抽取该类型声明处的标识符 ⇒ 匹配同名标识符的 `== null` / `!= null`（⛔ 声明式扫描，可能有少量误纳，逐条已人工归类）。

| 接口 | 命中 | 实现者 | 假 null 暴露面 |
|---|---|---|---|
| `IDamageable` | **43** | `UnitController`／`Building`／`Portal`（全 MonoBehaviour） | ✅ **暴露** |
| `ITaskSource` | 11 | `Building`(+任务源) | ✅ 暴露（`TaskScheduler` 面） |
| `IUnitHandle` | 8 | `UnitController`（MonoBehaviour） | ✅ 暴露（⚠️ 但其自带 `IsAlive` 伪 null 检测 ⇒ 部分已治） |
| `IGridOccupant` | 5 | `Building`／`Portal` | ✅ 暴露 |
| `IAIDebugInfo` | 4 | `NPCBrain` | ✅ 暴露（调试面） |
| `IHomePointProvider` | 3 | `SceneHomePointProvider`（Singleton） | ✅ 暴露 |
| `ISaveable` | 3 | `Building`／`Portal`／`UnitController`(＋`KingdomBrainSave`) | ✅ 暴露 |
| `IUIPanel` / `IUIStackEntry` | 3 / 1 | UI 面板（MonoBehaviour） | ✅ 暴露 |
| `IAnchorConsumer` | 2 | `Building`（MonoBehaviour） | ✅ 暴露 |
| `IClickInteractable` | 1 | `UnitController` | ✅ 暴露 |
| `IWorldQuery`／`IThreatFormula`／`IPlayerActions` | 1/1/1 | 纯 C# 类（非 Unity 对象） | ⛔ 不暴露（`&&` 短路正常） |

**逐处归类（`IDamageable` 43 处 · 关键面）**：

- **(A) 语义上"依赖假 null 检测"的守卫（应治）**：`DamageSystem.cs:280`（明确）／`:177`（`RegisterAttack` 入口）／`:214`／`:295`／`:372`（`ApplyDamage`）／`:430`／`:590`／`:646`｜`ProjectileManager.cs:91`｜`MonsterAI.cs:95`｜`GroundEffectManager.cs:141/156/171`｜`BehaviorExecutor.cs:61/347`｜`UnitController.cs:846/1065/1088/1168/1187/1194`｜`BuildingComponents.cs:82/83`｜`NPCBrain.cs:158/454/577*/1270*/1861`（`*` ＝ 已配 `IsDestroyed` 兜底 ⇒ **假 null 部分已治**，见 ③）
- **(B) 不依赖假 null（局部变量／未赋值语义）**：`ProjectileManager.cs:253/258/261/278`（`bestTarget`/`throughTarget` 为局部）｜`UnitController.cs:834`（局部 `nearest`）｜`L2PostureDecider.cs:121/144`（`AI.Core` 侧用 `IUnitHandle.IsAlive` 自带伪 null 检测）｜`NPCBrain.cs:1076/1478/1571`｜探针 2 处（`Valley_HH320_DamageProbe.cs:430/910`）
- **非 `IDamageable` 中真正暴露的**：`GridSystem.cs:393/417/420`（`IGridOccupant`）｜`TaskScheduler.cs:138/144/158/172/186/226/366/485/729/1280`（`ITaskSource`）｜`SaveManager.cs:147/159/224`（`ISaveable`）｜`UIManager.cs:27`｜`AIDebugController.cs:89/124/129/190`｜`NPCBrain.cs:258/321/873`（`IHomePointProvider`）
- ⭐ **既有正例（项目内已范用）**：`NPCBrain.cs:1774-1778`
  `private static bool IsDestroyed(IDamageable d) { var uo = d as UnityEngine.Object; return uo == null; }` ⇒ **修法的现成模板**。

### ③ 修法候选（＋性能面）

| 候选 | 形态 | 评价 |
|---|---|---|
| **A** 就地转换 | `if (attacker is UnityEngine.Object uo && uo == null)` | 零新基建；但**逐处手写**（87 处规模） ⇒ 易漏 |
| **B**（⭐ 推荐）统一工具方法 | 照 `NPCBrain.IsDestroyed` 上提为公共口，例：`public static bool IsUnityNull(IDamageable d) => (d as UnityEngine.Object) == null;`（放 `CombatRules` 或小工具类）⇒ 调用 `if (attacker == null \|\| CombatRules.IsUnityNull(attacker))` | ✅ 单一事实源 ＋ 可加断言/日志；`as`/`is` 为**引用类型检查（无装箱）**，`uo == null` 走 Unity `op_Equality`（内部比较实例有效性）⇒ **每次成本远低于一次伤害结算**（`ExecuteAttack` 本身含字典查询/距离/伤害计算）；无 GC |
| **C** `ReferenceEquals` | `ReferenceEquals(attacker, null)` | ⛔ **不适用**（托管引用对已销毁对象仍非 null ⇒ 正是本 bug 反面）；仅用于"同一引用"比较（如 `CombatRules.cs:167` 的 `ignore` 判定 ✓ 该处用法正确） |

### ④ 影响面（修后行为差异）

修后（以 `DamageSystem.ExecuteAttack` 为例）：假 null attacker ⇒ 命中 `:280-284` 分支 ⇒ ① **不再** `NotifyAttackVisual()`／`attacker.GetPosition()`（消 `MissingReferenceException`）② 该攻方注册被清（免悬挂，清 `_pendingAttacks` 的入列源）③ ⚠️ **行为差异**：该攻方**当帧不再攻击**（此前是抛异常/被外层吞 ⇒ 实质同样打不出伤害）⇒ 净效果＝**把异常路径变成静默早退**。
⚠️ 须逐点核的边界：**若某处的语义是"假 null 也要发事件/记日志/归还资源"**（如 `MapGate.ReturnAnchor:441` 的 `IAnchorConsumer`），修后不得变成静默丢弃 ⇒ 建议**分桶治理**（见 §六 待裁 4）。

---

## §五 · 判据表（⭐ `L-88` 读数列 / 能力列分列）

> 规则：**能力列**只接受**生产链实测读数**；⛔ 码面读/静态扫描/推断不得填入。

| # | 项 | 读数列（载体） | 能力列（生产链实测） |
|---|---|---|---|
| B1-a | `M3-A` 上限来源/可调 | `DamageSystem.cs:66` ⇒ `_config.maxAttacksPerFrame`；`DamageConfig.cs:23=100`；资产 `:17=100`；运行期实读 **100**（码面＋运行期读值） | ✅ **cap 生效**：cap=1 ⇒ 逐帧 `pending` `2→12` 只增不减且 6 帧仅 1 次落伤；cap=100 ⇒ 一帧清空＋落伤恢复（`Logs/hh321b_m3a2_…192202.txt`） |
| B1-b | 「置 0」是否等价关闭 | 码面：`Mathf.Min(n,0)=0` ＋ `:267-270` 不排空 | ✅ **不等价（停摆＋无界积压）**：cap=0 ⇒ 5 靶 HP 全程 110 ＋ `pending` `15→30`（+3/帧） |
| B2 | `M3-B` 退役 | 类型声明 **0 命中**；剩余 8 处全为注释/字符串（含 `LifecycleAudit.cs:136` 口径说明）（静态扫描） | ⛔ **本列不填**（"已落地"之断言载体为码面扫描；无生产链读数） |
| B3-① | `Die()`/`ReleaseLayerOwnedState` | `Building.cs:1364-1399`／`:1336-1356`／`:1345 FreeFootprint`；调用面 `:921`／`:1289`（码面） | ⛔ 留空（见 B3-⑤） |
| B3-② | `RemoveResourceNode` | `MapGate.cs:351-366`（地表级 · 幂等）；调用面 `:422`／`ResourceRespawnSystem:519`／`WorldManager:249-251`（码面） | ⛔ 留空 |
| B3-④ | `M3-C` 前置 | `中层执行计划.md:225` ＋ `试验底线台账.md:3876`「批 2-D 已闭 ✅」（文档＋码面） | —— |
| B3-⑤ | 判据 ⑦ | `07:128` 原文（抄录）｜`Building.cs:1398 Destroy(gameObject)` 仍在 `Die()` 内 ＋ 本层注销已合一但**非门**（码面） | ⛔ **本列不填**（"未达成"的载体＝码面读 ⇒ 按 `L-88` 不得入能力列；**生产链实测口** ＝「击毁一座建筑 ⇒ 观测其销毁是否经门」——本段未做，登记为批 2 落地后的验收读数） |
| B4-① | 假 null 守卫现场 | `DamageSystem.cs:275/280/282-283/289/305`；实现者 3 个皆 MonoBehaviour（码面） | ⛔ 留空（"守卫失效"属语义推断；⛔ 上表不填能力列） |
| B4-② | 同款点扫描 | **30 接口 / 87 处**（`IDamageable 43` … ）＋ 逐处归类 ＋ 既有正例 `NPCBrain.cs:1774`（静态扫描） | ⛔ 留空 |

---

## §六 · §待裁（5 条 · ⛔ 不凑数）

| # | 事项 | 选项/读数 | 执行端建议 |
|---|---|---|---|
| 1 | **`M3-C` 形状选型** | 形状 1（本层门＋高级层收尾分离）／形状 2（只移 `Destroy`）／形状 3（越层 ⇒ 否决） | ⭐ **形状 1**（唯一使"过门"结构成立；且与 `03` §7.3/§7.6 逐条对齐） |
| 2 | **门落点与命名** | ① `BuildingFactory.RemoveBuilding(b, cause)`（与放置口同族）② 新 `BuildingLifecycle` ③ `MapGate` 新口 | ⭐ **①**（同族对称：`CreateBuildingInstance:89` ↔ `RemoveBuilding`） |
| 3 | `Die()` 内「高级层收尾 ＋ 广播」的位置 | ① 留在 `Die()`（门只管本层注销 ＋ 销毁）② 移入门内前段 | ⭐ **①**（`03` §7.6 明列"高级层自己订广播"⇒ 职责线更干净；且 `EnterRuined` 共用面零扰动） |
| 4 | **`DZ-4` 修法与范围** | 修法 A/B/C（⛔ C 不适用）；范围＝① 只修 `DamageSystem`（任务书标的现场）② 同批治「(A) 依赖假 null 守卫」桶（含 `TaskScheduler`/`SaveManager` 等） ③ 全 87 处 | ⭐ **修法 B**（统一工具方法 · 照 `NPCBrain.IsDestroyed` 上提）＋ 范围 **②**（治理"守卫意图明确"的那一桶；局部变量桶与纯 C# 接口桶 ⛔ 不动） |
| 5 | `M3-A` 附带面是否随批 2 处置 | cap=0 ⇒ **停摆 ＋ 队列无界增**（本段实测）；cap=1 ⇒ 积压净增 | ⭐ 建议**随批 2 加守卫**：`maxAttacksPerFrame <= 0 ⇒ LogError`（⛔ 不用 0 作"关闭"）；⚠️ "积压上限/丢弃策略"是否要做 ＝ 请裁（涉及战斗公平性） |

---

## §七 · 红线复核

- ⛔ **`Assets/**` 一行未写**：本段仅只读扫描 ＋ bridge 运行期读数（产物落 `Logs/`，⛔ 未落 Assets）；`git status` 的 `Assets` 改动 ＝ 段 A 的 `DamageConfig.asset` ＋ 探针档，⛔ 与本段无关。
- ⛔ 未动 `AI.Core`／训练仓／`GridMath.DistCells`／`M3` 本体／`GameScene.unity`／`Packages`／`TaskScheduler.cs`（工作区既有改动未动）。
- ⛔ **批 2 本体（`M3-C` ＋ `DZ-4`）未施工**（⛔ 本任务书不含授权）⇒ **停手待裁**。
- 本段**不取新 HH 号**（按任务书）；⛔ 不 push。
