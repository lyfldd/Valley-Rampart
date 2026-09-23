# `HH.321` **批 2 本体**（`M3-C` ＋ `DZ-4`）施工交付报告

- **执行端**：TraeCode｜**日期**：2026-09-23｜**性质**：施工（`M3-C` 回收门 ＋ `DZ-4` 假 null 守卫 ＋ `cap ≤ 0` 守卫 ＋ 注释勘正）
- **依据**：`多Agent交接/策划端/HH.321_批2开片前实核_报裁裁决.md`（`D851` §三/§五）＋ 批 2 本体施工任务书
- **基线**：`5cddd09e`（`git log --oneline -3` 实对齐：`5cddd09e` ← `9b871186`（`D850`/`D851`）← `97f93fa9`）｜⛔ 未 rebase／未 reset／未 push
- **产物**：`Logs/hh321c_door_20260923_202802…`／`hh321c_door_20260923_202832.txt`（对照臂 · `# 封存 20:28:32`）／`hh321c_fakenull_20260923_202211.txt`（修前 · `20:22:11`）／`hh321c_fakenull_20260923_202604.txt`（修后 · `20:26:04`）
- **编译**：`unity_editor refresh` ⇒ **errors 0**

---

## §0 · 结论 ＋ ⚠️ 停手报裁（件 1）

| 件 | 状态 | 判据 |
|---|---|---|
| **件 1 · `M3-C` 回收门** | ⚠️ **门本体已落地 · 接线按令实施后实测门恒早退 ⇒ 停手报裁（停手条件 1）** | 判据 2/3/4/5 ✅；**判据 1 的 (a) 在"经门"态不成立**（根因见 §一-4） |
| **件 2 · `DZ-4` 假 null 守卫** | ✅ **达成**（修前/修后对照齐备） | 修前：直调抛 `MissingReferenceException` ×1 ＋ 注册残留 ＋ 每 tick 复发（0.4s×5 窗内未捕获异常 **20** 条）；修后：**0 / 残留 False / 0 条** ⇒ 静默早退 |
| **件 3 · `cap ≤ 0` 守卫** | ✅ **达成** | `cap=0` ⇒ `LogError` **1 条** ＋ **判定未停摆**（靶 HP 逐帧下降）＋ `pending` **有界**（`0/3/0/0/0/0`） |
| **件 4 · `ProjectileManager:170` 注释** | ✅ **达成** | 勘正为「视觉格」（旧「格单位」口径已废） |

⛔ **件 1 未按令接线**：`Die()` 现**暂回开片前调用**（`ReleaseLayerOwnedState(unregisterFromRegistry: true)` · 语义零变 · **零回归**），门本体 `BuildingFactory.RemoveBuilding` **在位可用**（探针直调已验证）。**⛔ 本端未对契约细节自行择一**（见 §七 待裁 1）。

---

## §一 · 件 1 · `M3-C` 建筑实体「数据回收门」

### 1. 门本体（落点/形状按 `D851` §3.1/§3.2）

| 项 | 落地 |
|---|---|
| 门 | `BuildingFactory.cs:220 public void RemoveBuilding(Building b, DeathCause cause)`（与放置口 `CreateBuildingInstance` 同族 · ⛔ 未新开类） |
| 门体（幂等校验 ＋ 数据回收） | `Building.cs:1341 internal void ReleaseLayerOwnedState(bool unregisterFromRegistry)`（`private ⇒ internal` **上提** ⇒ ⛔ **无第二份实现**；`HH.294` 片4「唯一实现」结论维持） |
| 门内不做 | ① 实体销毁（`Destroy` 留 `Die()` 尾）② 广播（`03` §7.6）③ 高级层收尾（工人撤出／掉箱三路／训练中断 ⇒ 留 `Die()`） |
| 幂等 | `b == null ⇒ return`；`b.state == BuildingState.Dead ⇒ return`（按令原文） |
| `cause` | 仅作**调用契约**（`03` §7.5「为什么删是调用者的语义」）；死因统计／`Removed` 广播钩子为**未闭合项**（`D851` §四）⇒ 本批 ⛔ 不消费 |

### 2. ⭐ 判据 1（生产链实测）· **两态对照**

**构造**：test-run（seed 20321 · 1× · 隔离场址）｜工事塔（`Buildings/arrow_tower` · `IsFortification=True` · HP 135）｜击毁入口 ＝ `DamageSystem.ApplyDamage(射手, 塔, 999999)` ⇒ `Building.TakeDamage → hp≤0 → IsFortification ⇒ Die(Killed)`（**生产链**）｜订阅方读数 ＝ `UnitDiedEvent` 处理器内即时读（注册表／占格／实体可用性／位置）。

| 态 | 广播时 注册表空 | 广播时 占格空 | 广播时实体可用 | 位对上 | 帧后已销毁 | (a) 回收先于广播 |
|---|---|---|---|---|---|---|
| **态 A · 按令经门**（`BuildingFactory.Instance?.RemoveBuilding(this, cause)`） | **False** | **False** | True | True | True | ⛔ **不成立** |
| **态 B · 回旧路径（对照）** | **True** | **True** | True | True | True | ✅ **成立** |

⇒ ⭐ **(b)(c) 两态全绿**（广播时 `transform.position` 可读 · `Destroy` 于其后生效）；**唯一差异 ＝ 回收是否发生** ⇒ 隔离实验把根因锁定在**门体幂等判据**（`L-89` 双报：变量关＝链路通／变量开＝实际量级）。

### 3. 判据 2/3/4/5 读数

| 判据 | 读数（产物原文） | 判 |
|---|---|---|
| **2 · `EnterRuined` 零扰动回归** | `farm HP 90（ApplyDamage=999999）⇒ state=Ruined ｜ 占格仍在=True ｜ 注册表仍在=True ｜ 实体仍在=True ｜ 新增 UnitDiedEvent=0` | ✅ 全绿（**保持占格／不注销注册表／不发死亡事件／不销毁**） |
| **3 · 门幂等** | 门第 1 次（Active 建筑直调）⇒ `注册表空=True 占格空=True 门后实体仍在=True`（**门体回收生效** ＋ **门不销毁**）｜置 `Dead` 后第 2 次 ⇒ `注册表空=True 占格空=True` ＋ 注册表计数 `23→22→22`（**零副作用**）｜对**已销毁引用**第 3 次 ⇒ 无异常（`b == null` 早退） | ✅ |
| **4 · `Destroy` 仍在 `Die()` 内** | `Building.cs:1405 Destroy(gameObject)`（`Die()` 尾 · ⛔ 未移入门） | ✅ |
| **5 · 注释勘正（`L-63`）** | `Building.cs:1322` 原「工地被打毁掉箱走 `Die:1259`」⇒ 勘正为 **`Die:1364`** | ✅ |
| 门可见性口径 | `ReleaseLayerOwnedState` **非 public（internal）= True** · public 版 = **False**（`Valley_HH294_Slice4Probe.cs:142` 的「非 public」断言**保持成立**） | ✅ |

### 4. ⚠️ 停手报裁 · 根因（契约自相矛盾 · ⛔ 非实现偏差）

- 令中**门体两步**：① 幂等校验 `b == null 或 b.state == BuildingState.Dead ⇒ return` ② 数据回收。
- 令中**`Die()` 顺序（锁死）**：首行即 `state = BuildingState.Dead` ⇒ 随后调门。
- ⇒ **互斥**：进入门时 `state` 恒为 `Dead` ⇒ 步骤 ① 恒命中 ⇒ **步骤 ② 永不执行**（态 A 实测：回收未发生）。
- ⭐ 门体本身**正确**：对 `Active` 建筑直调 ⇒ 回收生效（判据 3）⇒ 该矛盾**仅在"经门"路径暴露**。
- ⛔ 处置：按**停手条件 1**（「发现必须改契约…才能完成」）**报裁**，⛔ 未自行择一；为**免留已知回归**，`Die()` 暂回开片前调用（门体 `ReleaseLayerOwnedState` 语义零变 ⇒ 开片前行为完整保留）。

---

## §二 · 件 2 · `DZ-4` 接口类型假 null 守卫（修法 B · 范围按 `D851` §3.5）

### 1. 唯一口

`CombatRules.cs:157 public static bool IsUnityNull(IDamageable d) => d is UnityEngine.Object uo && uo == null;`（照 `NPCBrain.IsDestroyed:1774-1778` 上提；`NPCBrain` 该私有方法**改为委托本口**（`NPCBrain.cs:1778`）⇒ 调用面 6 处语义零改）。
- ⭐ 用 `is`（而非裸 `as`）⇒ **纯 C# 实现者不会被误判**为"已销毁"（裸 `as` 版本对非 Unity 实现者返回 null ⇒ 误判 true）；`is`/`as` ＝ 引用类型检查（**无装箱**）⇒ ⛔ 不以性能为由省略；⛔ `ReferenceEquals` 不适用（托管引用对已销毁对象仍非 null ＝ 本 bug 反面）。

### 2. ⭐ 机械重扫（⛔ 未照抄任务书清单 · 逐处解析**变量静态类型**）

扫描法：白名单**变量名声明行解析**（`IDamageable <name>` ／ `var <name> = …` ⇒ 读初始化式类型），再匹配同名标识符的 `== null` / `!= null`。

| # | file:line | 变量 | 静态类型（声明处） | 桶 | 处置 |
|---|---|---|---|---|---|
| 1 | `DamageSystem.cs:199/201` | `attacker`,`target` | `IDamageable` 形参（`RegisterAttack`） | A | ✅ 已改 |
| 2 | `DamageSystem.cs:238` | `newTarget` | `IDamageable` 形参 | A | ✅ |
| 3 | **`DamageSystem.cs:307`** | `attacker` | `IDamageable` 形参（`ExecuteAttack`） | A | ✅ **主现场** |
| 4 | `DamageSystem.cs:322` | `target` | `IDamageable`（`reg.target` · 跨帧字段） | A | ✅ |
| 5 | `DamageSystem.cs:399` | `target` | `IDamageable` 形参（`ApplyDamage`） | A | ✅ |
| 6 | `DamageSystem.cs:617` | `victim` | `IDamageable`（`evt.Unit`） | A | ✅ |
| 7 | **`DamageSystem.cs:464`（点名 ①）** | `c` | ⭐ **`UnitController`**（`QueryUnitsInRadius → List<UnitController>`） | **C** | ⛔ 未改（非接口 ⇒ Unity 重载有效） |
| 8 | **`DamageSystem.cs:500`（点名 ②）** | `unit` | ⭐ **`UnitController`** | **C** | ⛔ 未改 |
| 9 | `DamageSystem.cs:452` | `victim` | `UnitController`（`FindShelterShield` 形参） | C | ⛔ |
| 10 | `DamageSystem.cs:660/668` | `killer`,`victim` | `UnitController`（`is` / `as UnitController`） | C | ⛔ |
| 11 | `ProjectileManager.cs:91-93` | `attacker`,`target` | `IDamageable` 形参（`SpawnProjectile`） | A | ✅ |
| 12 | `ProjectileManager.cs:253/258/261/278` | `bestTarget`,`throughTarget` | 局部 `IDamageable`，赋值源＝**已过滤**的 `UnitController`/`Building` | B | ⛔ 未改（登记） |
| 13 | `MonsterAI.cs:95` | `t` | 局部 `IDamageable`（`PickBuildingTarget`）· 守卫意图＝目标有效性 | A | ✅ |
| 14 | `GroundEffectManager.cs:141/156/171` | `e.source` | 结构体字段 `IDamageable` | A | ✅（3 处 · 同式一并） |
| 15 | `BehaviorExecutor.cs:61/347` | `_self` | 字段 `IDamageable` | A | ✅（2 处） |
| 16 | `UnitController.cs:834/846/1065/1088/1168/1187/1194` | `nearest`/`_staticTarget`×2/`target`/`center`/`fireTarget`/`moveTarget` | 字段 ×2 ＋ 形参 ×2 ＋ 局部 ×3（皆 `IDamageable`） | A/B | ✅（7 处；局部亦改＝假 null 只会更安全） |
| 17 | `BuildingComponents.cs:82/83` | `target` | 局部 `IDamageable` | A | ✅ |
| 18 | `NPCBrain.cs:158/454` | `_self` | 字段 `IDamageable` | A | ✅ |
| 19 | `NPCBrain.cs:553/577/1228/1270/1416/1504` | 各局部/字段 | `IDamageable`（**已配** `IsDestroyed`） | A(已治) | ✅ **仅统一到公共口**（helper 委托 · ⛔ 语义零改） |

**⛔ 本批不治（挂账另批 · `D851` §3.5）**：`ITaskSource` 11／`ISaveable` 3／`IGridOccupant` 5／`IHomePointProvider` 3／`IUIPanel` 3／`IUIStackEntry` 1／`IClickInteractable` 1／`IAIDebugInfo` 4／`IAnchorConsumer` 2／`IWorldQuery`·`IThreatFormula`·`IPlayerActions` 各 1（后三者实现者为**纯 C# 类** ⇒ 不暴露假 null）。

### 3. ⭐ 修前/修后对照（判据 1/2 · 生产链）

**构造**：`IDamageable fake = 单位;` ⇒ `Object.Destroy(单位.gameObject)` ⇒ 引用保留（假 null）。

| 读数列 | 修前（`hh321c_fakenull_…202211.txt`） | 修后（`…202604.txt`） |
|---|---|---|
| `fake == null`（**接口静态类型**） | **False** ⚠️ 病根 | False（构造不变 · 病根仍在） |
| `(fake as UnityEngine.Object) == null` / `IsUnityNull(fake)` | True / True | True / True |
| **直调臂** `RegisterAttack(fake, target)` ⇒ 抛出异常数 | **1**（`MissingReferenceException: The object of type 'UnitController' has been destroyed…`） | **0** ✅ |
| 直调臂 ⇒ 注册残留 | **True**（守卫失效 ⇒ 未清） | **False** ✅（`:307-311` 早退清注册） |
| **tick 臂**（0.4s×5 窗）未捕获异常累计 | **20**（每 tick 复发 · 印证 `:277-279` 注释"4586 次/轮"的形态） | **0** ✅ |
| tick 臂 ⇒ 注册残留 ／ `pending` | True ／ `0→7→14→19`（无界增长） | **False ／ 0** ✅ |
| 目标 HP（两态同） | 110→110（**打不出伤** ⇒ 无功能差异 · 差异只在异常/残留） | 110→110 |

- ⭐ **行为差异（判据 2）**：修后假 null ⇒ 走 `DamageSystem.cs:307-311`（`_registrations.Remove(attacker); return;`）⇒ ① 不再 `NotifyAttackVisual()`／`attacker.GetPosition()`（消异常）② 注册被清（消悬挂 ＋ 消 tick 复发）③ ⚠️ 该攻方当帧不再攻击（**修前亦打不出伤** ⇒ 无功能回退）。
- ⚠️ **`L-51` 生产路径可达性声明**：本臂**直接调** `DamageSystem.RegisterAttack`（战斗链生产入口 · ⛔ 未走发箭链路）；`:307` 守卫位于 `ExecuteAttack` ⇒ 与本臂路径**同源**（`RegisterAttack → ExecuteAttack`）。
- ⚠️ **成本实读（`L-87`）**：N=2,000,000 ⇒ 接口 `== null`（**原无效守卫**）**2.67 ns** ／ `IsUnityNull` **12.09 ns** ／ 组合式 `== null || IsUnityNull` **11.31 ns** ⇒ **增量 ≈ 8.6 ns/判定**（对一次伤害结算可忽略）。

### 4. ⚠️ 边界核查（判据 3 · ⛔ 无触发停手条件）

- 逐点核 43 处 `IDamageable` 命中：**全部**是「对象不可用 ⇒ 跳过/停手」语义，**未发现**「假 null 也需**发事件／记账／归还资源**」的落点 ⇒ **停手条件 2 未触发** ✓。
- 任务书点名的两处（`MapGate.cs:439 ReturnAnchor(IAnchorConsumer)`／`:395 IAnchorConsumer`）＝ **`IAnchorConsumer`**（⛔ 本批不治 · 挂账）⇒ 实读结论：① 其真实调用点 `ReleaseLayerOwnedState → MapGate.ReturnAnchor(this)` 位于**回收体内**，而回收发生时建筑实体**仍可用**（`Die` 路径中 `state=Dead` 但对象未销毁 ⇒ 位对上=True 实测）⇒ 该处无假 null 风险；② 若 consumer 已销毁，锚点**结构上无法归还**（`ReturnAnchor` 需读 `ConsumedAnchorCoord`）⇒ 登记（⛔ 不改，另批）。

---

## §三 · 件 3 · `cap ≤ 0` 守卫

**实现**：`DamageSystem.cs:66-88` ⇒ `MaxAttacksPerFrame` 由表达式体改为守卫属性：`cap ≤ 0 ⇒` **`Debug.LogError`（一次性标志 `_maxAttacksCapWarned:67`）** ＋ **兜底按 1 处理**（⛔ 不用 0 作"关闭"；⛔ 未加"积压上限/丢弃策略"＝ `O-27`）。

**判据（生产链 · 1× · 3 注册 × 3 靶）**：

```
[J4·cap=0⛔非法] LogError 累计=1（样本：DamageConfig.maxAttacksPerFrame=0 ≤ 0 非法…按 1 处理）
[J4·cap=0 f0] pending=0   靶HP=110/106/106
[J4·cap=0 f1] pending=3   靶HP=110/102/106
[J4·cap=0 f2] pending=0   靶HP=110/98/94
[J4·cap=0 f3] pending=0   靶HP=106/98/94
[J4·cap=0 f4] pending=0   靶HP=106/98/94
[J4·cap=0 f5] pending=0   靶HP=102/98/94
```

⇒ ① `LogError` **出现且仅 1 条**（⛔ 不刷屏）② **判定未停摆**（三靶 HP 逐帧下降）③ **`pending` 有界**（峰值 3 ⇒ 排空）——与 `D851` 实核的 `cap=0`「**零判定 ＋ +3/帧 无界增长**」形成**同构造前后对照** ✓。

---

## §四 · 件 4 · `ProjectileManager` 命中半径口径注释（`L-63`）

`ProjectileManager.cs:170-172`（原 `:170`）：`float hitRadiusCells = HitRadiusCells; // 格单位（2_5 步骤6…）` ⇒ 勘正为**视觉格**（`DamageConfig.hitRadiusCells` · 默认 0.25）＋ 说明命中判定走 `GridMath.DistVisual`（与 `AttackProfile.range` 同域）⇒ 旧「格单位」口径已废。

---

## §五 · 判据表（⭐ `L-88` 读数列 / 能力列分列）

> 能力列**只接受生产链实测读数**；⛔ 码面读/静态扫描/推断不入该列。

| # | 项 | 读数列（载体） | 能力列（生产链实测） |
|---|---|---|---|
| 件1-a | 门本体/幂等/不销毁 | `BuildingFactory.cs:220`；`Building.cs:1341`（internal · 非 public=True）；探针 J3 逐项读数 | ✅ **门直调 ⇒ 回收生效**（注册表空/占格空=True）＋ **幂等**（`23→22→22`）＋ **不销毁**（实体仍在=True） |
| 件1-b | **判据 1（经门）** | 态 A：广播时 注册表空=**False** · 占格空=**False**（门恒早退）；态 B（对照）：**True**/`True` ＋ 位对上/帧后销毁 两态同 | ⛔ **本列不记"达成"**：**(a) 回收先于广播 在经门态不成立**（实测）⇒ 已按停手条件 1 报裁 |
| 件1-c | 判据 2 `EnterRuined` 回归 | `state=Ruined` / 占格仍在 / 注册表仍在 / 实体仍在 / 新增 `UnitDiedEvent`=0 | ✅ **废墟链零扰动**（生产链实测） |
| 件1-d | 判据 4/5 | `Building.cs:1405 Destroy`（仍在 `Die()` 尾）；`:1322` 注释勘正 `Die:1259 → :1364` | ——（码面/文本面） |
| 件2-a | 假 null **修前** | 直调异常 **1** ＋ 注册残留 True ＋ tick 窗异常 **20** ＋ `pending 0→19` | ✅ **修前确实抛 `MissingReferenceException`**（生产链实测） |
| 件2-b | 假 null **修后** | 直调异常 **0** ＋ 残留 **False** ＋ tick 异常 **0** ＋ `pending 0` | ✅ **不再抛异常 ＋ 静默早退（清注册）**（生产链实测） |
| 件2-c | 成本 | `IsUnityNull` **12.09 ns** ／ 组合式 **11.31 ns** ／ 接口 `==null` 2.67 ns ⇒ 增量 **8.6 ns** | ——（微基准 · 读数列） |
| 件3 | `cap ≤ 0` 守卫 | `LogError` **1** 条 ＋ `pending` 逐帧 `0/3/0/0/0/0` | ✅ **不再零判定 ＋ 有界**（生产链实测） |
| 件4 | 注释勘正 | `ProjectileManager.cs:170-172` | ——（文本面） |
| 全局 | 编译/红线 | `refresh ⇒ errors 0`；⛔ 未动 `AI.Core`／训练仓／`GridMath.DistCells`／`GameScene.unity`／`Packages`／美术／`TaskScheduler.cs` | —— |

---

## §六 · 构造法声明 · 收尾 · 产物

1. **构造法**：探针侧 `NPCBrain` 关脑 ＋ pin（1× · 真实 cadence）；`cap`／`state` 经**运行期字段**改（⛔ 不写资产）；假 null 经 `Object.Destroy` 保留引用构造；击毁入口 `DamageSystem.ApplyDamage`（生产链 · ⛔ 未走发箭链路 ⇒ 已按 `L-51` 声明）。
2. **收尾**：各菜单末 `TestHarnessApi.ExitTestRun()` ＋ 执行端 `unity_editor stop` ⇒ `playMode=stopped`（⛔ 无余留世界）。
3. **产物**：`Logs/hh321c_door_20260923_202008.txt`（态 A · 经门）／`…202832.txt`（态 B · 对照）／`hh321c_fakenull_…202211.txt`（修前）／`…202604.txt`（修后）；均含 `# 封存` 行 ⛔ 未入版本控制（既有约定）。

---

## §七 · §待裁（1 条 · ⛔ 不凑数）

**件 1 门体幂等判据与 `Die()` 首行 `state = Dead` 互斥 ⇒ 请裁形态**（本端 ⛔ 未择一）：

| 候选 | 形态 | 代价/风险 |
|---|---|---|
| **1（本端推荐）** | 门体幂等判据改「**已回收**」语义：`b == null ⇒ return`；否则以**现成可观测**判「已回收」＝ `BuildingRegistry.GetAt(b.coord) == null && GridSystem.GetOccupant(b.coord) == null` ⇒ 与 `03` §7.4「删两次等于删一次」同义 | ⛔ **不新增字段/机制**；⚠️ 需在门体加 2 行只读查询（成本 ≈ 两次表读） |
| 2 | 在 `Building` 上新增 1 个 `bool`（如 `_layerStateRecovered`）作幂等标记 | 最直白；⚠️ 属"加机制"（与停手条件 4 相关）⇒ 须裁 |
| 3 | 调换 `Die()` 内顺序（门先于 `state = Dead`） | ⛔ 与令中**锁死顺序**冲突；⚠️ 须复核 `EscapeWorkers`／掉箱三路／训练中断是否依赖 `state==Dead`（若依赖 ⇒ 连带改面大） |

ℹ️ 现工作区状态：**门在位 · `Die()` 暂回开片前调用**（零回归）⇒ 裁定后仅需改「门体判据（候选 1/2）」或「`Die()` 两行顺序（候选 3）」任一处即可接线，**门本体与探针无需返工**。
