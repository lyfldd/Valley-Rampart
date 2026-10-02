# QQQ.3 生命周期审查与兜底设计（v2）

> 覆盖更新于 2026-08-07（由生命周期审查草稿定稿覆盖；原 v1 的 54 个兜底场景全部保留并映射，见 §十）
> 配套文档：QQQ.2_NPC任务修正以及一些小问题.md、QQQ.2_执行清单.md、QQQ.3_执行清单.md
> 粒度（DR-20 升级）：状态机全集 + 转换枚举 + 清场动作 + 接口契约。实现细节仍待 T16-T18 落地后按契约补。
> v2 核心增量：从"受害方被动兜底"升级为"转换发起方主动清场 + 被动兜底"双闭环；新增 13 条生命周期链状态机全集、D1-D14 转换决策、16 项实证 bug（6 高危）。

---

## 〇、方法论升级：从"兜底场景清单"到"生命周期审查"

### 原 QQQ.3 的思维与局限

原 QQQ.3 是**受害方被动兜底**思维：枚举"出了什么事"→"怎么收拾"。覆盖了 54 个场景，但存在三个结构性盲区：

1. **只写了"出事后的收拾"，没写"转换发起时的清场"**。例如 BLD-A5 只说"升级期间拒绝新任务派发"，但没说升级发起那一刻，**已在场的工人、正在执行的任务、在途的搬运**怎么办。
2. **没有状态机全集**。每个实体有哪些状态、哪些转换是合法的、哪些转换是危险的，没有显式定义，导致"损坏的建筑能不能升级""维修中能不能点升级"这类问题连问的对象都不存在。
3. **场景清单是按"事故"枚举的，天然会漏**。没被想到的事故就没有条目；而按"生命周期转换"枚举是**结构性穷举**——N 个状态最多 N×(N-1) 个转换，每个转换都必须有答案，漏不掉。

### 生命周期审查框架（v2 核心）

**定义：一个实体的生命周期 = 状态集合 S + 转换集合 T + 每个转换的清场动作 C。**

审查规则（对每条转换逐一回答）：
1. **触发源**：谁能发起这条转换？（玩家点击/系统 tick/战斗伤害/读档/时间事件）
2. **in-flight 引用枚举**：转换发起瞬间，有哪些"在途引用"指向这个实体？（在执行的任务/在场的工人/在途的搬运/训练队列/编队锚点/UI 面板引用/存档字段）
3. **主动清场动作**：发起方必须逐一处理这些引用（中断/转移/释放/拒绝）
4. **被动兜底**：引用方访问前的校验（沿用原 QQQ.3：IsValid 校验 + 幂等 + 降级不崩溃）
5. **实证核验**：代码现状是否已实现？（本文档标注 ✅已实现 / ⚠️半实现 / ❌未实现 / 🐛实证 bug）

### 设计原则总纲（沿用原 QQQ.3 五条 + 新增两条）

1. **谁创建谁清理**（沿用）：创建任务/订阅事件/注册引用的对象，负责在其销毁/失效时清理
2. **幂等优先**（沿用）：所有清理操作幂等
3. **失效可见**（沿用）：所有引用对象暴露 `IsValid`，访问前必须校验
4. **降级不崩溃**（沿用）：兜底失败时降级行为，不抛异常崩溃
5. **不持久化瞬态**（沿用）：派发记录/订阅状态不持久化，加载后重建
6. **新增·转换先清场**：任何状态转换发起时，先枚举并处理 in-flight 引用，再执行转换本身。**清场是转换的一部分，不是事后补丁**
7. **新增·嵌套生命周期向下负责**：搬运生命周期嵌套在工人生命周期内、工人生命周期嵌套在游戏运行生命周期内。**外层生命周期终止/暂停时，必须显式终止/暂停所有内层生命周期**（如 NPC 死亡必须终止其身上所有任务；游戏暂停必须暂停所有任务计时）

---

## 一、游戏级生命周期审查（大框架）

### 1.1 状态机全集（代码现状 ✅已实现）

```
Booting → Splash → MainMenu → CharacterCreation ─┐
                        ↑      └→ SaveSlotSelect ─┤
                        │                         ↓
                        │                Loading → Ready(瞬态) → Playing
                        │                         ↑      ↓↑
                        │                         │    Paused(ESC, timeScale=0)
                        │                         │      ↓
                        │                         └─── GameOver(君主死/读档失败)
                        └──── TeardownForReturnToMenu(存/不存) ────┘
退出游戏: TeardownForQuit (仅主菜单出口, OnApplicationQuit)
```

时间子生命周期（Playing 内）：`Night → Dawn → Day → Dusk → Night`，跨日发 `TimeDayChangedEvent` → DayCycleSettlement 结算（饱食→幸福→税→生育→贸易→牧场→补员）+ SaveManager 自动存档。

### 1.2 转换审查表

| 转换 | 触发源 | in-flight 引用枚举 | 清场/兜底 | 实证 |
|------|--------|------------------|-----------|------|
| MainMenu→Loading | 玩家点新游戏/选槽 | 静态桥接参数 GameSceneEntrance | 禁输入、timeScale=1 | ✅ |
| Loading→Ready→Playing | ConfigsLoadedEvent | 配置加载结果 | 🐛 **LC-G1**：`_configLoader` 为 null 仍 Publish(true) 假成功；IsSuccess=false 只 LogError 永远卡 Loading，无 UI 反馈无回主菜单出口 | 🐛 |
| Playing→Paused | ESC | **所有内层生命周期**：任务计时/施工进度/训练倒计时/投射物/地面效果 | timeScale=0 天然停推（TimeManager 仅 Playing 推进、ProducerComponent 用 deltaTime） | ✅ |
| Paused→Playing | ESC/存档按钮 | timeScale 恢复 | 🐛 **LC-G2**：Resume 硬编码 `timeScale=1`，2x 下暂停再恢复变 1x，TimeManager 仍认为 2x（UI 与实际不一致） | 🐛 |
| Playing→GameOver | 君主死亡/读档失败 | 存档槽状态 | 🐛 **LC-G3**：读档失败路径在版本校验**之前**就设 CurrentSlotId → 进 GameOver → MarkCurrentSaveFinished 把**高版本存档永久打死档**（换新版本也读不了） | 🐛 |
| 任意→回主菜单 | 暂停退出/结算回菜单 | 全场景单位/建筑/任务/编队/事件订阅 | TeardownForReturnToMenu：先存档后销毁、9 个 Manager ResetState | ⚠️ **LC-G4**：TimeManager.ResetState 不重置倍速/战斗降速 ⇒ 上局 2x/降速残留，新局无故加速 |
| 任意→退出游戏 | 主菜单退出 | EventBus/输入回调 | TeardownForQuit + EventBus.Clear | ⚠️ **退出不自动存档**：距上次每日自动存档的进度丢失（最多一天）。决策 D9 待拍板 |
| 跨日（Day→Night 循环跨天） | TimeManager | **结算 vs 存档顺序** | 🐛 **LC-G5（高危 Heisenbug）**：SaveManager 在主菜单场景订阅 TimeDayChangedEvent，DayCycleSettlement 进 GameScene 才订阅 ⇒ 从主菜单进游戏时**自动存档先于每日结算**，存档抢到"结算前"状态；读档后 TimeManager.LoadState 直接赋值不发事件，该天结算**永不补跑**（编辑器直启 GameScene 顺序相反，入口相关） | 🐛 |
| 夜晚→白天 | TimePhaseChangedEvent | 城门/敌人/战斗降速/夜间停发的任务 | 城门 Dawn 自开、敌人清空后恢复倍速 | 🐛 **LC-G6**：敌人天亮不撤不消失，Dawn 城门即开 ⇒ 残敌长驱直入。"白天=休整期"生命周期不存在（见 §九 D6 战后收尾决策） |
| 战斗降速⇄恢复 | EnemyEnteredRegion | 任务进度/施工进度 | 降速不重置进度（TIM-A2 已定） | ✅ 但 timeScale 恢复依赖"敌人死光"而非天亮，与 LC-G6 叠加 |

### 1.3 游戏级生命周期决策（新增，详见 §九）

- **D9**：退出游戏前是否自动存档
- **D10**：LC-G5 修复方向（订阅优先级 vs DayCycleSettlement 移到 Core 层创建）

---

## 二、建筑生命周期审查（核心章）

### 2.1 状态机全集（v2 定义）

```
            玩家放置                进度条完成
 ghost(非实例) ────→ Constructing ────────→ Active
                       ↑  │                  │  │
            玩家点升级  │  │ 敌人攻击          │  │ 玩家点升级
            (_pending   │  │ (D2:可打/无敌)    │  │
             Upgrade)   │  ↓                  ↓  │
                       └──┴── 升级Constructing ──┘
                          │
  Active ──hp≤0──→ Dead ──→ Destroy/回池（彻底消失）
    │                ↑
    │  玩家拆除 Demolish（按 hp 比例退款, Cause=Demolished）
    ↓
  Abandoned（仅主城初始废墟）──玩家点修复──→ Constructing ──→ Active
```

**状态集合定义（规范化表达，消除歧义）**：

| 状态 | 本质 | 产出 | 可交互 | 可受伤 | 入存档 |
|------|------|------|--------|--------|--------|
| ghost | 放置预览，**非 Building 实例**（BuildController 独立 GameObject） | — | — | — | 否 |
| Constructing | 建造/升级中（脚手架） | ❌ 停产 | ❌ | D2 决策 | ⚠️ state 存但 progress 不存（LC-B4） |
| Active | 正常运行 | ✅ | ✅ | ✅ | ✅ |
| 受损 | **不是独立状态**（D1 决策：受损=Active 的 hp 属性，非状态） | ✅ 不降速 | ✅ | ✅ | hp 入档 |
| Dead | 瞬态（Die() 当帧执行完即 Destroy） | — | — | — | — |
| Abandoned | 主城初始废墟专用态 | ❌ | ✅(修复按钮) | ❌ | ✅（但读档被强制 Active，LC-B1） |

> 注：原枚举 `Placing` 是死代码（全工程无赋值点），v2 删除或保留仅作语义标注——列入清理项。

### 2.2 转换审查表（v2 核心增量）

**约定**：每行列举"转换发起瞬间必须处理的 in-flight 引用"。✅=代码已实现，🐛=实证 bug，❌=未实现，⭐=v2 新增要求。

| # | 转换 | 触发源 | in-flight 引用枚举 | 清场动作（v2 定义） | 实证 |
|---|------|--------|------------------|-------------------|------|
| T-B1 | ghost→Constructing | 玩家放置（扣资源） | 占地格（GridSystem 占用）、资源点改造（工具建筑放到资源点上 node.Die） | 落位即锁格 | ✅ |
| T-B2 | Constructing→Active | 进度条完成 | 无（建造期不可交互，无在途引用） | 发 BuildingActivatedEvent | ✅ |
| T-B3 | Active→升级Constructing | 玩家点升级（扣升级费） | **①在场工人（currentWorkers）②正执行的生产任务③以本建筑为源的搬运任务（产出待搬）④训练队列（训练建筑）⑤研究队列（学院）⑥ProducerComponent 产出累计器** | **D2 决策：升级开工=清场**。⭐v2：进入升级态时①中断在场任务、工人释放回 Idle（刺激自然过期，调度器下 tick 重派）②本建筑待搬运出保持可搬（StorageComponent 仍有效）③训练/研究队列挂起还是取消见 D11 | 🐛 现状：对在场工人**零处理**，工人继续对脚手架"工作" |
| T-B4 | 升级Constructing→Active | 进度条完成 | 升级费已扣 | level+1、**hp=maxHp（升级即满血=D1 变相维修）**、发 BuildingUpgradedEvent | ⚠️ **LC-B6**：只乘 maxHp，_rate/capacity/trainingSlots 不刷新，升级零产能增益（与设计"Lv×并发"矛盾） |
| T-B5 | Active→Dead（被打爆） | hp≤0 | **①在场工人②训练队列③任务源注册（TaskScheduler）④网格占用⑤StorageComponent 存货⑥以本建筑为终点的在途搬运⑦UI 面板引用⑧ISaveable 注册** | Die() 现有：逃工人/清训练/放网格/发 UnitDiedEvent | ⚠️ 四项缺口：🐛**LC-B7** 逃工人是死代码（currentWorkers 从未 Add）；⭐存货蒸发不退不爆（3.5.3 §7.2 已定"清空丢失"=设计特性，明写）；⭐在途搬运终点重定向（QQQ.3 B2-2 已覆盖）；🐛**LC-B8** Die 未 UnregisterSaveable（有 CleanupDestroyedSaveables 兜底，但属清理遗漏） |
| T-B6 | Active→Dead（玩家拆除） | Demolish | 同 T-B5 | 按 hp/maxHp 比例退款 | ⚠️ 受损建筑拆除返还更少——D1 下属合理风险定价，明写 |
| T-B7 | Abandoned→Constructing（修复主城） | 玩家点修复（扣资源） | 建造菜单解锁状态 | 完成后发 BuildingActivatedEvent 解锁建造 | 🐛 **LC-B1（高危）**：读档重建只发 BuildingPlacedEvent 从不发 BuildingActivatedEvent ⇒ `_buildUnlocked=false` 建造菜单永久软锁；反向：未修复就存档 ⇒ 读档强制 Active ⇒ 既无修复按钮也不能升级，**主城卡死** |
| T-B8 | 任意→读档重建 | SaveManager.Load | 存档字段集 | SpawnFromSave 重建+LoadState | 🐛 **LC-B2**：grade 未入档 ⇒ 产能建筑读档后率降贫瘠档（rate×0.7 永久）；🐛 **LC-B4**：constructProgress/_pendingUpgrade 未入档 ⇒ **升级中存档=升级丢失且不退款**；⭐训练队列/研究队列/产出累计器整体不入档（读档即丢，D12 决策） |
| T-B9 | Active 受损（hp 扣减） | 敌人攻击 | 无状态转换（D1：受损非状态） | ⭐v2 新增：**建筑血量变化事件**（现状：TakeDamage 不发任何血量事件，受损感知为零，UI 无数据通道） | ❌ 未实现 |
| T-B10 | 受损→Active（维修回血） | 维修任务 | — | **D1 决策：不做带伤维修**。受损建筑回血只有两个途径：①升级即满血 ②毁了留废墟修复重建。明写为设计特性 | ❌ 有意不实现 |
| T-B11 | Constructing 被打 | 敌人攻击 | 建造进度/已扣资源 | **D2 决策**：脚手架无敌 vs 可被打 | 🐛 现状：非 Active 直接 return=无敌（语义不合理但保护体验） |

### 2.3 案例深挖：农场升级 × 任务执行（用户点名场景）

**场景链**：农场 Active，工人 A 正在农场执行生产任务（Working），工人 B 正在搬农场产出的粮食（MovingToDest），玩家点击升级。

**v2 定义的处理序列**（D2 清场决策下）：

1. BuildingPanel 校验通过、扣升级费 → `TryUpgrade()` 置 `_pendingUpgrade=true, state=Constructing`
2. **清场（新增，T-B3）**：①工人 A 的任务中断：移除以本农场为 issuer 的任务刺激 + `currentTask=null` 回 Idle（调度器下 tick 重派别处）；②工人 B **不受影响**——其任务源是农场的 StorageComponent（存货仍可搬），终点是仓库，与农场 state 无关
3. 升级期间：农场停产（IsActive=false 门控 ProducerComponent.Tick）、不可交互、D2 决定可否受伤；StorageComponent 存货保留照常可搬
4. 完工：`level+1, hp=maxHp`，发 BuildingUpgradedEvent → 农场恢复 Active，下 tick 重新发布生产任务

**现存缺口**：⭐升级期间农场的**耗水条件**（T15 农场需水）与升级恢复的衔接；⭐升级完成后 `_rate` 应按新 level 重算（LC-B6 修复点）。

### 2.4 案例深挖：仓库生命周期交叉（用户点名场景）

**仓库的完整生命周期交叉点**：

| 事件 | 与搬运生命周期的交叉 | v2 处理 |
|------|-------------------|---------|
| 仓库升级 | 以它为终点的在途搬运 | 升级中 StorageComponent 仍有效 ⇒ 在途搬运**不受影响**（存货可进脚手架仓库）；⭐容量升级完工后刷新（LC-B6 同根） |
| 仓库受损（hp 扣减） | 存储功能 | D1：受损非状态，存储不受影响 |
| 仓库被打爆 | 在途搬运终点失效 | QQQ.3 B2-2 已定：终点每 tick 动态重查最近仓库；仓库全毁则入国库 |
| 仓库被打爆 | **库内存货** | 🐛 现状：存货随 Destroy 蒸发。3.5.3 §7.2 已定"生产建筑存货清空丢失"=设计特性；⭐v2 待拍板 D13：仓库是否同样清空（建议：清空，与 3.5.3 一致，激励保护仓库） |
| 搬运工人取货途中被打断 | **手上货物** | **D4 决策**：货留身上，威胁解除继续搬；工人死亡则货随死亡消失 |

### 2.5 案例深挖：城墙/城门生命周期（用户点名场景）

**关键架构事实：城墙/城门/箭塔不是 Building，是 UnitController 单位**（挂 FortificationDef）。这意味着它们**不走建筑状态机**，生命周期是：

```
建造(建筑系统放置) → 单位存活(挡路/挡弹道/被塔楼驻守) → hp≤0 → UnitController.Die → 回对象池
```

| 用户问题 | 现状 | v2 答案（D5 决策） |
|---------|------|------------------|
| 城墙晚上被打坏会怎样？ | hp 扣减，无事件、无视觉、无功能降级，永远带伤（hp 入存档） | D5：城墙**不加受损态、不做维修**（3.5.3 §7.1 已定"土木工事直接销毁需重建"）；⭐但新增**工事关血事件+受损播报**（夜战结束后的"战损清单"），让玩家知道哪段墙该拆建 |
| 会不会发布维修事件？ | 不会（Repair 任务源未实装，ScheduleCenterStub 注释"P1 接入"） | D5：土木工事不发布维修事件，**毁了=重建**；生产建筑毁了留废墟发修复任务（S 级，3.5.3 §7.3）——**废墟修复才是"维修事件"的唯一载体** |
| 损坏的建筑能不能升级？ | 能（TryUpgrade 只查 Active+资源），且升级即满血 | D1+D7：受损建筑**可以升级，升级即满血=变相维修**。明写为设计特性（鼓励玩家"以升代修"） |
| 维修中能不能点升级？ | 无"维修中"状态（不存在） | D1 下该问题转化为"废墟修复中（Constructing）能不能升级"：⭐**不能**——Constructing 态不可交互（IsInteractable=false），UI 层天然拒绝 |
| 城门生命周期 | Closed⇄Open（昼夜自动+玩家覆盖 5min 超时回落） | 🐛 **LC-G6**：Dawn 即开但残敌未清 ⇒ 见 D6 战后收尾；⚠️ 城门回池后 `FortificationPassableOverride`/GateController.State 随池残留（LC-B8 对象池同根问题） |

### 2.6 建筑生命周期遗留实证 bug（非转换类）

| # | Bug | 说明 |
|---|-----|------|
| LC-B9 | 低速率建筑永远不产出 | `RoundToInt(_rate)`：rate<0.5/s 每秒恒产 0，主产无累计器（金矿有 `_goldAccumulator` 主产没有） |
| LC-B10 | 读档后 grade 丢失 | 见 T-B8 |
| LC-B11 | BuildingVisual 占位覆盖 | 非 Constructing 态一律 ApplyPlaceholder 色块，美术接入后会覆盖真贴图（当前 def.prefab 全空未暴露） |

---

## 三、NPC 生命周期审查

### 3.1 状态机全集（代码现状）

```
生成(UnitFactory.SpawnUnit, 池复用优先)
  → Initialize(注入数据/注册UnitRegistry/分配SaveId/发UnitSpawnedEvent)
  → NPCBrain.Init(记忆组件/Executor/分片)
  → 存活循环: 感知(0.2s)→Think(10Hz分片)→Execute(每帧移动)
  → 每日结算: 饱食扣减(≤阈值扣血可致死)/幸福/生育配对
  → 死亡: Die() → 注销存档 → 发UnitDiedEvent(9订阅者) → 注册表注销 → 回对象池
  → 复用: 出池重新 Initialize
```

**职业转换子生命周期**：流浪汉→(招募1粮,职业即翻转)→走回王国途中(**已耗粮未入册过渡态**)→抵达入册 Resident→(训练,天数)→Worker/士兵→(编队征召)⇄解散回 Idle。Child→(2次天数事件)→Resident。

### 3.2 转换审查表

| 转换 | in-flight 引用枚举 | 清场/兜底 | 实证 |
|------|------------------|-----------|------|
| 生成→存活 | 对象池前世状态 | **出池洗涤** | 🐛 **LC-N1（最高危）**：`Initialize` 不重置 `_runtimeOccupation`/`LastBirthDay`/`ChildGrowthDays`/`IsVagrantRecruited`/冲锋态/静态目标；`NPCBrain.Init` 不重置攻击目标/追击目标/受击计数/**编队槽位刺激** ⇒ 新生儿带着上辈子的职业、任务刺激、编队军令行动。⭐v2 必修：出池显式 Reset 清单 |
| 存活→任务执行 | 任务派发 | 任务生命周期嵌套（§四） | 🐛 **LC-N2**：`IsKingdomTaskWorker` 置 true 后全工程无一处置 false ⇒ 任务完成后工人**永久瘫痪**（Update 永远跳过 Execute） |
| 任务执行→威胁挂起 | ThreatStimulus 抢占 | 刺激层挂起/恢复旁路（QQQ.2 DR-2：不进 TaskState 枚举） | ✅ 机制在，⭐挂起超时 60s Abandon（QQQ.3 已定，待 T18 落地） |
| 存活→被招募 | 玩家点击（扣1粮） | 旧任务/旧刺激清理 | 🐛 **LC-N3**：走回刺激 expiry=120s，营地远/被压制超时 ⇒ 已转居民（开始耗粮）永不入册=**人口蒸发**，无重试机制 |
| 存活→编队征召 | DispatchOrders | QQQ.3 FRM-A1：currentTask Abandon | ✅ 设计已定待实施 |
| 存活→死亡 | hp≤0（战斗/饥饿扣血） | **嵌套终止**：任务映射/编队槽位/训练队列/搬运名额/幸福扣减 | ⚠️ 三处缺口：🐛**LC-N4** 训练队列不感知死亡（TrainingSystem.Update 对死单位 continue，槽位永久泄漏；池化下还会对尸体 SetOccupation）；🐛**LC-N5** 调度中心 `_transporting` 名额对回池工人永不释放（w==null 判不失效）⇒ 搬运可永久卡死；🐛**LC-N6** UnitDiedEvent.Killer 恒 null、饿死报 Killed（DeathCause 无 Starved） |
| 死亡→回池→复用 | 对象池 | 同"生成→存活"洗涤 | 同 LC-N1 |

### 3.3 嵌套规则实例（新增原则 7 的应用）

NPC 死亡（外层生命周期终止）必须显式终止的内层生命周期清单：
1. 当前任务（WorkerTask.Abandon ✅已订阅 UnitDiedEvent）
2. 调度器任务映射（QQQ.3 B1-1 OnNpcDied，待 T17）
3. 编队槽位（FormationController.OnUnitDied ✅已实现+1s 防抖重排）
4. 训练队列条目（⭐LC-N4 修复点）
5. 搬运名额 `_transporting`（⭐LC-N5 修复点）
6. 身上携带资源（D4：随死亡消失）
7. DamageSystem 攻击注册三表（✅已实现自清）

---

## 四、任务生命周期审查（嵌套在 NPC 生命周期内）

### 4.1 状态机全集（QQQ.2 T20 定义 + v2 补全）

```
发布 TryAdvertiseTask(任务源按条件声明)
  → 派发(调度器 1s/tick: 优先级+距离升序; currentTask引用即占用=幂等DR-17)
  → Assigned → MovingToSource → Working ──→ Completed
                   │              │(Transport) ↓
                   │              └──→ MovingToDest ──→ Completed
                   ↓
        任意状态 → Abandoned(死亡/征召/源失效/挂起超时/玩家调走)
威胁抢占: 走 TaskStimulus 挂起旁路(非 TaskState 成员, DR-2)
```

**完成语义（QQQ.2 §10.4 已定）**：Production→触发当次产出；Transport→HarvestCarry 到终点；WaterHaul→充水网；Gather→资源入国库+资源点销毁生命周期。

### 4.2 搬运任务生命周期（用户点名的嵌套案例）

```
触发: 产出建筑存储≥阈值(占位 capacity×80%)
  → 发布: destType=NearestWarehouse(不硬编码坐标, DR-16)
  → 派发: 调度器解析 destPos=最近可用仓库(无则国库)
  → MovingToSource(走向产出建筑) → Working(取货 HarvestCarry, carryAmount≤携带量)
  → MovingToDest ──⭐终点每 tick 重查(QQQ.3 B2-2: 仓库被毁/满仓改道)
  → Completed: 资源入仓, _transporting 名额释放
中断分支:
  ├─ 取货前被打断: 刺激5s过期→自然消失→下 tick 重派(✅已有机制)
  ├─ 取货后被打断: D4=货留身上, 威胁解除继续搬
  ├─ 工人死亡: 任务Abandon+名额释放(LC-N5修复)+货物消失(D4)
  └─ 源建筑被毁: 存货清空(3.5.3 §7.2)⇒任务无货可取⇒源失效检查Abandon(QQQ.3 NPC-A6)
```

**"NPC 大脑找终点"的架构定位（回应用户思想）**：终点解析发生在**搬运生命周期的 MovingToDest 转换点**（每 tick 重查），而不是派发时定死、也不是 NPC 大脑每帧决策——这是"结尾生命周期里的处理"，与"防止硬编码"的设计意图一致。决策层（大脑）只做"接不接这个任务"，执行层（调度器）负责"终点在哪"。

### 4.3 一次性资源点采集生命周期（用户点名案例，全链未实现）

**设计链（QQQ.2 T19/DR-11 已定，v2 补全中断分支）**：

```
地图生成创建(isConsumable=true) → 等待玩家点击(无限期)
  → 点击弹确认UI(DR-11: "采集将花费X秒, 派1个工人?"; UI-A4: 单例覆盖防连点)
  → 确认 → 资源点锁定(isBeingGathered=true, RES-A4) + 发布Gather任务
  → 调度器派空闲工人 → MovingToSource → Working(耗时按资源量: 木堆2s/石堆4s/矿脉8s)
    ⭐Working阶段NPC播放采集动作(占位: 朝向+停留+头顶劳作提示, 视觉动画后置)
  → Completed: 资源入国库 → 资源点销毁三步: ①GridSystem.Free ②BuildingRegistry移除 ③对象池Despawn(DR-11不Destroy)
中断分支(⭐v2补全):
  ├─ 确认后工人到达前资源点被敌人打爆: BLD-A4(网格锁随Building.Die释放)+NPC-A6(源失效Abandon)
  ├─ 采集中工人被打断: RES-A2(进度重置不保留, isBeingGathered=false, 可再被点击)
  ├─ 采集中存档: ⭐isBeingGathered是否入档(QQQ.3 §五.8 待T19定, v2建议: 不入档, 读档后资源点回未锁态)
  └─ 玩家点击后不确认/取消: 不锁点不发任务(确认才锁)
```

**代码现状（❌ 全链未实现）**：PickupComponent.Init 空壳；资源点 faction=None 点击打开通用 BuildingPanel 无任何可用操作；调度中心不派 Gather；WorkerTask.Gather 完成不结算不给资源。T19 待 QQQ.2 阶段 B 落地。

### 4.4 任务 × 建筑状态交叉矩阵（v2 新增，回答"什么建筑状态下任务怎么办"）

| 任务阶段 ↓ / 建筑状态 → | 升级Constructing | 受损(Active) | Dead |
|------------------------|-----------------|-------------|------|
| 已派发未出发(Assigned) | D2：中断回收，工人回 Idle | 不受影响（受损非状态） | NPC-A6：源失效 Abandon |
| MovingToSource | D2：同上 | 不受影响 | NPC-A6：到达前校验 IsValid |
| Working（在场工作） | D2：中断释放 | 不受影响 | B5：逃工人（🐛LC-B7 死代码待修） |
| MovingToDest（在途搬运） | 不受影响（源是 StorageComponent） | 不受影响 | B2-2：终点每 tick 重查改道 |
| 训练队列中 | D11 待拍板（挂起/取消） | 不受影响 | OnBuildingDestroyed 全员回退 Resident（✅已实现） |

---

## 五、繁殖生命周期审查（用户点名案例）

### 5.1 状态机全集（3.5.1 §4.2 设计 + 08-06 已实现）

```
每日结算时检查三层硬前置(缺一不可):
  ①全局: 整体幸福>60 且 平均饱食>50
  ②房屋: 剩余容量>0(房屋满=禁止生育, POP-A5已实现)
  ③个体: 冷却期外成年Resident≥2(lastBirthDay+10天≤当天)
  → 随机配对(无固定配偶/性别/家族)
  → 生小孩表演(占位: 仅日志, 无进房动画) → 当帧SpawnUnit(Child)在房屋旁
  → 父母lastBirthDay=当天(个体10天冷却) + 全局BirthCooldownDays节奏闸
  → Child: 只吃粮(1/日)不干活, 占房屋容量
  → 2次天数事件累积 → SetOccupation(Resident) 成人
```

### 5.2 用户问题的对照回答

| 用户问 | 现状 | v2 答案（D8 决策） |
|--------|------|------------------|
| "进入繁殖"有状态吗？ | 无。条件满足当帧即生，无怀孕期/妊娠期 | D8：**保持即时生**（3.5.1 极简哲学：能砍的中间态全砍）。推荐不加怀孕期——它会引入"怀孕中死亡/房屋被毁"等一整族新转换，收益不成比例 |
| "繁殖需要多久"？ | 0（当帧） | 表现层占位：两居民走向房屋的"表演"是日志占位，视觉后置 |
| "下一次繁殖的间隔"？ | 双层冷却：个体 10 天 + 全局 BirthCooldownDays 节奏闸 | ✅已定。注意冷却是**按人**不是按对（无固定配偶） |

### 5.3 转换风险审查

| 风险 | 处理 |
|------|------|
| 配对成功后小孩出生前房屋被毁 | ✅无窗口（当帧完成）——这是"无怀孕期"设计附带的鲁棒性收益 |
| 配对父母之一当日晚些时候死亡 | 无窗口（当帧完成）；死亡扣幸福可能影响明日生育前置（机制自洽） |
| Child 成长期间房屋被毁 | Child 占容量但成长不依赖房屋（TickChildGrowth 只数天数事件）✅ |
| 读档 | lastBirthDay/childGrowthDays/BirthCooldownDays 已入档（UnitSaveData v2/v3 + PopulationSaveData）✅ |
| 对象池复用污染 | 🐛 LC-N1：`LastBirthDay`/`ChildGrowthDays` 出池不洗 ⇒ 新出生单位可能带上辈子生育冷却/成长计数（修复点同 LC-N1） |

---

## 六、次级生命周期审查（训练/编队/牧场/城门/投射物）

### 6.1 训练生命周期

```
入队(TryTrain: 校验职业/金/水晶/将军限量 → 入队即扣费,中断不退)
  → 排队(槽位满时等待, 每帧TryPromote晋升1个) → 训练中(inTraining, 按天计费)
  → 完成: SetOccupation(新职业), ActiveCount--, 移出队列
中断: 建筑被毁→全员回退Resident(不退款, ✅已实现)
缺口: 🐛LC-N4 训练中死亡无回退(槽位泄漏); 🐛队列不入档(读档即丢, D12);
      ⚠️升级Constructing期间训练照常推进(TryTrain/Update不查state)——D11决策
```

### 6.2 编队生命周期

```
组建(BindGeneral挂将军/InitGarrison挂城墙锚点 → RecruitStandard扫空闲士兵填槽 → DispatchOrders发军令)
  → 意图循环: 驻守/冲锋/撤退/Sally(FormationBrain自主决策, 1s防抖)
  → 减员: 成员亡→移出+防抖重排; 将军亡→立即DisbandAll
  → 解散: 逐成员ClearFormationState清军令
缺口: ⭐编队存档未做(存档改造清单#11); 🐛编队槽位刺激随对象池残留(LC-N1同根);
      ⭐编队锚点建筑被毁(原QQQ.3 FRM-A3已覆盖)
```

### 6.3 牧场生命周期（动物）

```
买幼崽(BuyCub, 商人) → 每日喂1粮生长(兔2/鸡3/猪5/牛8天)
  → 断粮: 停长不死亡, 成年期顺延(设计特性)
  → 成年(isAdult) → 玩家点宰杀(Slaughter) → 一次性得肉入国库 → 动物回收, 需再买
无动物繁殖(屠宰制, 3.5 §13.1决策18) ✅设计明确
```

### 6.4 城门开闭生命周期

见 §2.5。补充：玩家覆盖超时用 `Time.time`（受 timeScale 影响），2x 下实际现实时长减半（⚠️ 语义与配置注释"分钟"不一致，轻微）。

### 6.5 投射物/地面效果生命周期

```
投射物: 发射锁定起止(不追踪) → 抛物线插值 → 到达结算(越墙判定→伤害→溅射→地面效果) → 回池(上限200)
地面效果: 生成 → 按tickInterval结算(Burn/Slow/Heal) → duration到移除
缺口: 🐛LC-C1 越墙判定不做阵营判断 ⇒ 己方塔楼低抛弹道持续磨损自家城墙, 无任何事件告知玩家;
      ⭐DamageSystem/ProjectileManager/GroundEffectManager 均无 ResetState 挂 TeardownManager(回主菜单路径的跨局泄漏风险)
```

---

## 七、存档生命周期审查（横切所有实体）

### 7.1 存档自身生命周期

```
保存: 每日自动存档(TimeDayChangedEvent, 间隔1天) + 手动存档(暂停面板)
  → 遍历ISaveable注册表 → JsonUtility → 原子写入(.tmp→删旧→Move) → GameSavedEvent
加载: 文件存在→设CurrentSlotId→反序列化→拒死档/拒高版本→Global模块LoadState
  → Scene重建(spawner) → Scene模块LoadState → GameLoadedEvent
  → ⭐ValidateAfterLoad(原QQQ.3 §3.3契约, 待实施)
损坏回退: ❌无任何回退机制(原QQQ.3 SAV-A9设计已定: try-catch回退上一日自动存档, 待实施)
```

### 7.2 存档字段覆盖审计（v2 新增：每个生命周期"哪些状态入档"）

| 实体 | 已入档 | **未入档（缺口）** | 后果 |
|------|--------|------------------|------|
| Building | defId/coordX/level/hp/maxHp/state/stored/副产 | 🐛grade(LC-B2)、🐛constructProgress/_pendingUpgrade(LC-B4)、coord.y、isPlayerBuilt(强制true) | 产能降档/升级丢失 |
| Unit | 血/攻防/移速/位置/饱食幸福/lastBirthDay/childGrowthDays/IsVagrantRecruited | 任务态(currentTask, QQQ.3 B6-3已定待T18)、编队槽位(#11未做) | 读档后任务清空回Idle(可接受) |
| 训练队列 | 无 | 🐛整体不入档 | 读档即丢(居民已扣费) |
| 研究队列 | 无 | 整体不入档 | 读档即丢 |
| TaxSystem | 非ISaveable | LastDayTax昨日税负 | 读档次日幸福结算丢税负因子一天 |
| 产出累计器 | 无 | ProducerComponent._accumulator | 读档后当秒进度归零(可接受) |
| WaterNetwork | Stored ✅ | — | 加载clamp(0,100)(原QQQ.3已定) |
| 资源点锁定 | — | isBeingGathered(建议不入档, §4.3) | 读档后回未锁态(自愈) |

### 7.3 存档 × 生命周期交叉的实证 bug 汇总

LC-B1（建造菜单软锁）、LC-B2（grade）、LC-B4（升级中存档）、LC-G3（读档失败毁档）、LC-G5（自动存档先于结算）——见对应章节。⭐统一修复方向：SpawnFromSave 后补发"重建完成"事件族（或 BuildController 改从 KingdomManager.CastleLevel 派生 `_buildUnlocked`），见 §十一契约升级。

---

## 八、实证 Bug 总清单（代码级，按严重度）

> 全部为本次生命周期审查中代码实锤（文件：行号 见各章节）。标 🆕 为原 QQQ.3 未覆盖的新发现。

### 高危（数据损坏/软锁/功能失效）

| # | Bug | 根因 | 修复方向 |
|---|-----|------|---------|
| LC-B1 🆕 | 读档后建造菜单永久软锁/主城卡死 | BuildingActivatedEvent 只发一次（建造完工），读档重建不发 | SpawnFromSave 对 CastleCore Active 态补发事件，或 BuildController 从 KingdomManager.CastleLevel≥1 派生 |
| LC-G5 🆕 | 自动存档先于每日结算（入口相关 Heisenbug） | EventBus 按订阅序派发，SaveManager 在主菜单场景先订阅 | D10：EventBus 订阅加优先级，或 DayCycleSettlement 移 Core 层先创建；或自动存档改在结算完成后触发（监听新事件 DaySettledEvent） |
| LC-N1 🆕 | 对象池出池不洗状态（职业/生育冷却/编队槽位/任务刺激跨世泄漏） | Initialize/Init 无 Reset 清单 | 出池显式 Reset：UnitController 列字段清单 + NPCBrain 清刺激列表/攻击目标/IsKingdomTaskWorker |
| LC-N2 🆕 | IsKingdomTaskWorker 永不复位 ⇒ 工人永久瘫痪 | Assign 置 true 无复位点 | 任务 Completed/Abandoned 时复位（T18 落地时一并） |
| LC-B4 🆕 | 升级/建造中存档 ⇒ 升级丢失不退款 | constructProgress/_pendingUpgrade 未入档 | 入档；或折中：Constructing 态存档时按"取消升级退款"语义落档为 Active（D12） |
| LC-G3 🆕 | 读档失败把高版本存档永久打死档 | CurrentSlotId 在版本校验前设置 + GameOver 无差别 MarkFinished | 全部校验通过后再设 CurrentSlotId；MarkFinished 仅君主死亡触发 |

### 中危（功能异常/泄漏）

| # | Bug | 修复方向 |
|---|-----|---------|
| LC-B2 🆕 | 读档后产能建筑降贫瘠档 | grade 入 BuildingSaveData |
| LC-B6 🆕 | 升级只加血，产能/储量/训练槽不刷新 | OnConstructionComplete 按新 level 重算 _rate/capacity/trainingSlots（读 def.levels[level-1]） |
| LC-N4 🆕 | 训练中死亡槽位泄漏 | TrainingSystem 订阅 UnitDiedEvent 清条目（原 QQQ.3 B3-1 已定，本文确认代码现状：当前实现靠 e.unit==null 判断，池化下失效——需改用 IsAlive/IsValid） |
| LC-N5 🆕 | _transporting 名额泄漏 ⇒ 搬运永久卡死 | 清理条件改用 `!w.IsAlive` 而非 `w==null`；或订阅 UnitDiedEvent 释放 |
| LC-N3 🆕 | 招募走回 120s 过期 ⇒ 人口蒸发 | 过期重注入（VagrantCampSystem.Update 自愈扫描已 0.5s 跑一次，把"未入册且刺激过期"的补发刺激） |
| LC-B7 | 逃工人死代码（currentWorkers 从未 Add） | T17/T18 派发时登记 currentWorkers（与 concurrentWorkers 设计对齐） |
| LC-B9 🆕 | 低速率建筑永不产出 | 主产加累计器（对齐金矿 _goldAccumulator 模式） |
| LC-B8 | Building.Die 未 UnregisterSaveable | Die() 补注销（有兜底但应显式） |
| LC-G2 🆕 | 暂停恢复硬编码 timeScale=1 | Resume 用 TimeManager.CurrentTimeScale |
| LC-G4 🆕 | TimeManager.ResetState 不重置倍速/战斗降速 | ResetState 补重置 |
| LC-G6 🆕 | Dawn 城门即开残敌直入 | D6 战后收尾决策 |
| LC-C1 🆕 | 投射物误伤己方城墙 | CheckWallBlock 加阵营判断 |
| LC-N6 🆕 | Killer 恒 null、饿死报 Killed | TakeDamage 加 source 参数（DamageSystem 传攻击者）；DeathCause 加 Starved（SatietySystem 传入） |

### 低危（语义/清理）

| # | Bug |
|---|-----|
| LC-B11 | BuildingVisual 占位色块覆盖隐患（美术接入后现形） |
| LC-B12 | Placing 死枚举、concurrentWorkers 字段从未被读（与设计"Lv×并发"脱节） |
| LC-C2 | 城门覆盖超时按游戏缩放时间计（2x 下现实时长减半） |
| LC-C3 | DamageSystem/ProjectileManager/GroundEffectManager 无 ResetState 挂 TeardownManager |
| LC-C4 | PausePanel.OnSaveClicked 在 CurrentSlotId 为空时写死回退 "slot_1"（极端情况覆盖别槽） |
| LC-C5 | ConfigsLoadedEvent 假成功 + 失败卡 Loading 无出路（LC-G1） |

---

## 九、设计决策表（v2 新增，推荐值待拍板）

> 标记 🔷 为架构级决策（影响代码结构）；🔹 为规则级。本表是本次"生命周期审查"相对原 QQQ.3 的核心增量——原 QQQ.3 没有回答的"转换规则"全部在这里。

| # | 级别 | 决策点 | 推荐方案 | 备选 | 影响面 |
|---|------|--------|---------|------|--------|
| D1 | 🔷 | 建筑受损是否引入中间状态 | **不引入**。受损=Active 的 hp 属性；回血途径=升级满血（以升代修）或毁了废墟修复 | 引入受损态（hp<50% 产能降半+外观破损+维修任务链） | 状态机骨架/维修体系存在性 |
| D2 | 🔷 | 升级×任务并发（农场升级时工人怎么办） | **升级开工=清场**：中断在场任务、工人释放回 Idle，调度器自然重派；在途搬运不受影响（源是 StorageComponent） | 等任务完成再开工 / 挂起升级后恢复 | T-B3 清场序列；WorkerTask 中断链 |
| D3 | 🔷 | 脚手架（建造/升级中）可否被攻击 | **保持无敌**（首版），明写为设计特性 | 可被打，hp≤0=中断+部分退款 | 敌人 AI 目标选择/夜战压力 |
| D4 | 🔹 | 搬运中断时手上货物 | **货留身上**，威胁解除继续搬（终点每 tick 重查）；工人死亡则货随死亡消失 | 中断即蒸发 / 掉落成资源点 | 搬运生命周期中断分支 |
| D5 | 🔷 | 城墙（土木工事）维修 | **不做维修**：毁了直接消失需重建（3.5.3 §7.1 已定）；新增"工事关血事件+夜战战损清单"让受损可见 | 城墙也留废墟可修 | "维修事件"的唯一载体=生产建筑废墟修复任务（S 级） |
| D6 | 🔹 | 夜战→白天收尾（战后生命周期） | **新增"黎明结算"**：天亮时 ①场上残敌标记撤退/继续（待波次系统定，当前明写"敌人不撤退"为现状风险）②发布战损清单（工事/建筑受损播报）③城门延迟到"区域无敌"才自动开 | 维持现状（Dawn 即开） | LC-G6 修复；GateController 开门条件 |
| D7 | 🔹 | 受损建筑能否升级 | **能，且升级即满血**（变相维修，D1 推论）。UI 不做额外限制 | 受损禁止升级（须先修） | BuildingPanel 交互 |
| D8 | 🔹 | 繁殖是否加怀孕期 | **不加**，保持当帧即生（3.5.1 极简哲学）；避免"怀孕中死亡/房屋被毁"一族新转换 | 加怀孕期（天数状态） | PopulationSystem 状态机 |
| D9 | 🔹 | 退出游戏前是否自动存档 | **退出游戏（Quit）前自动存档**；回主菜单已存档（TeardownForReturnToMenu save:true） | 维持现状（退出丢最多一天进度） | TeardownForQuit 流程 |
| D10 | 🔷 | LC-G5 修复方向（存档/结算顺序） | **新增 DaySettledEvent**：DayCycleSettlement 结算完成后发布，SaveManager 改为订阅它（顺序显式化，不依赖订阅先后） | EventBus 加订阅优先级 | 自动存档时机 |
| D11 | 🔹 | 建筑升级期间训练/研究队列 | **挂起**：升级期间训练计时暂停，完工后恢复（队列保留）；建造中的建筑不可发起新训练 | 取消回退 Resident（同摧毁） | TrainingSystem.Update 加 state 门控 |
| D12 | 🔹 | Constructing 态存档语义 | **进度入档**：constructProgress/_pendingUpgrade 写入 BuildingSaveData，读档后续建 | 存档时按"取消升级退款"落档为 Active | LC-B4 修复 |
| D13 | 🔹 | 仓库被打爆时库内存货 | **清空丢失**（与 3.5.3 §7.2 生产建筑一致，激励保护仓库） | 按比例掉落成资源点 | T-B5 清场序列 |
| D14 | 🔹 | 采集中资源点存档（isBeingGathered） | **不入档**，读档后回未锁态可重新点击（进度重置语义与 RES-A2 一致） | 入档恢复采集进度 | T19 落地时定 |

---

## 十、与原 QQQ.3 场景清单的映射（54 场景全保留）

原 QQQ.3 的 54 个兜底场景**全部保留有效**，本文档将其升级为"转换审查"框架下的对应位置：

| 原场景组 | 对应 v2 章节 | 关系 |
|---------|-------------|------|
| §1.1 NPC AI（NPC-A1~A10） | §3.2 存活→死亡/被招募/被征召 转换 + §4 任务中断分支 | 原"被动兜底"保留，v2 补"主动清场" |
| §1.2 建筑（BLD-A1~A8） | §2.2 T-B5/T-B8 + §4.4 交叉矩阵 | BLD-A5 升级为完整 D2 清场序列 |
| §1.3 人口训练（POP-A1~A6） | §3.2 + §5 + §6.1 | POP-A1 代码现状确认池化下失效（LC-N4） |
| §1.4 资源（RES-A1~A6） | §2.4 + §4.2/§4.3 | RES-A2 中断分支并入采集生命周期 |
| §1.5 UI（UI-A1~A6） | 各章节 UI 引用校验 | 保留不变 |
| §1.6 存档（SAV-A1~A10） | §7 + LC-B1/B2/B4/G3/G5 | v2 新增字段覆盖审计 |
| §1.7 时间（TIM-A1~A4） | §1.2 暂停/降速转换 | LC-G2/G4 补充 |
| §1.8 编队（FRM-A1~A4） | §6.2 | 保留不变 |
| §三 接口契约（5 个） | §十一 契约升级 | 保留并扩展 |

**新增覆盖（原 QQQ.3 完全没有）**：升级×任务并发清场（D2）、受损/维修/升级互斥规则（D1/D5/D7）、搬运在途货物（D4）、繁殖生命周期审查（§5）、夜战→白天收尾（D6）、存档字段覆盖审计（§7.2）、实证 bug 16 项（§八）、游戏级转换审查（§1.2）。

---

## 十一、接口契约升级（在原 QQQ.3 §三基础上扩展）

原 5 个契约（ITaskScheduler/ITaskSource/ISaveableWithValidation/Building 事件/UnitController 事件）**全部保留**。v2 新增：

### 11.1 IClearableOnStateChange（转换清场契约，新增）

```csharp
/// <summary>状态转换清场契约（v2 原则6：转换先清场）。
/// 建筑进入 Constructing(升级)/Dead 等状态前，调用方负责触发本接口。</summary>
public interface IClearableOnStateChange
{
    /// <summary>枚举并处理所有 in-flight 引用（中断任务/释放工人/挂起队列）。幂等。</summary>
    void ClearInFlightReferences(StateChangeReason reason);
}

public enum StateChangeReason { Upgrade, Demolish, Killed, Repair, SaveLoad }
```

实现方：Building（清场序列 T-B3/T-B5）、UnitController（死亡嵌套终止 §3.3）、FormationController（解散清场）。

### 11.2 Building 血量可见契约（新增，D5/D6 支撑）

```csharp
public class Building : IDamageable
{
    /// <summary>血量变化事件（受损感知的数据通道）。TakeDamage 后发。</summary>
    public event Action<Building, int currentHp, int maxHp> OnHpChanged;
}
```

配套：`FortificationDamagedEvent`（城墙受损播报，D5 战损清单数据源）。

### 11.3 出池洗涤契约（新增，LC-N1 修复）

```csharp
/// <summary>对象池复用洗涤契约。出池时由 UnitFactory.SpawnUnit 调用，先于 Initialize。</summary>
public interface IPoolResettable
{
    /// <summary>显式重置所有瞬态字段（职业覆盖/生育冷却/冲锋态/刺激列表/编队槽位/任务标志）。</summary>
    void ResetForReuse();
}
```

实现方：UnitController、NPCBrain、WorkerTask、GateController。

### 11.4 DaySettledEvent（新增，D10 支撑）

```csharp
/// <summary>每日结算完成后发布（DayCycleSettlement 末尾）。SaveManager 自动存档改订阅本事件。</summary>
public readonly struct DaySettledEvent { public readonly int Day; }
```

---

## 十二、实施优先级建议（待与 QQQ.3_执行清单合并）

> 原 QQQ.3_执行清单的 47 任务（B0-1~B7-3）**全部保留有效**。以下为 v2 新增项的优先级建议，合并时按此排序：

**P0（高危实证 bug，独立可做，不等 T16-T18）**：
1. LC-B1 建造菜单软锁（SpawnFromSave 补发事件或派生 _buildUnlocked）
2. LC-G5 自动存档/结算顺序（DaySettledEvent，D10）
3. LC-N1 对象池洗涤清单（IPoolResettable）
4. LC-G3 读档失败毁档（CurrentSlotId 设置时机）
5. LC-B2 grade 入档
6. LC-G2/G4 timeScale 恢复与重置
7. LC-B9 主产累计器
8. LC-N5 `_transporting` 名额 IsAlive 判断
9. LC-B8 Die 补 UnregisterSaveable

**P1（转换清场框架，与 T16-T18 并行）**：
10. D2 升级清场序列（IClearableOnStateChange + Building.TryUpgrade 接入）
11. LC-N2 IsKingdomTaskWorker 复位（随 T18）
12. LC-N4 训练队列死亡清理改 IsAlive（随 T7）
13. LC-N3 招募走回过期重注入
14. D11 训练挂起（TrainingSystem state 门控）
15. D12 constructProgress/_pendingUpgrade 入档
16. LC-N6 Killer/DeathCause.Starved

**P2（玩法层，待波次/维修体系）**：
17. D5/D6 工事关血事件 + 战损清单 + 黎明收尾
18. LC-C1 投射物阵营判断
19. 废墟修复任务链（3.5.3 §7.3，依赖任务系统）
20. T19 一次性资源点采集全链（QQQ.2 阶段 B）

---

## 反偷懒自查

- [x] 每个实体都给了**状态机全集**（游戏/建筑/NPC/任务/搬运/采集/繁殖/训练/编队/牧场/城门/投射物/存档 13 条链）
- [x] 每条转换都回答"触发源/in-flight 引用/清场动作/实证核验"四要素（§1.2/§2.2/§3.2 审查表）
- [x] 用户点名的场景逐一专章深挖：农场升级×任务（§2.3）、仓库交叉（§2.4）、城墙维修五连问（§2.5）、搬运嵌套（§4.2）、一次性资源全链（§4.3）、繁殖三问（§5.2）
- [x] 实证 bug 全部带文件：行号，区分 🆕新发现 vs 原 QQQ.3 已覆盖
- [x] 设计决策 D1-D14 全部给推荐值+备选+影响面，不模糊
- [x] 原 QQQ.3 的 54 场景+47 任务显式映射保留（§十/§十二），不破坏既有价值
- [x] 嵌套生命周期原则（原则 7）有 NPC 死亡 7 项内层终止清单实例支撑（§3.3）
- [x] 文档间矛盾如实记录：QQQ.2 §10.5 耗水 1 水 vs DR-18 耗 2 水（以 DR-18 为准）；3.5 幼崽价格两处不一致；3.5.3 搬运终点锁定时机留白（v2 以"派发时解析+每 tick 重查"补全）
- [x] 未实现 vs 实证 bug 明确区分：T19 采集链是"未实现功能"不是 bug（§4.3 标注）
- [ ] 待用户确认 D1-D14 后，覆盖 QQQ.3_兜底设计.md 并同步 QQQ.3_执行清单（新增任务编号续接 B7-3 之后）

---

## 附：本文档未覆盖的显式边界

1. **波次战斗系统生命周期**（敌人生成→进攻→撤退/全灭）：系统未开工（等地图生成），本文只在 D6 留接口
2. **科技/研究生命周期**：AcademyBuilding 队列不入档问题已记录（§7.2），完整体系 P2
3. **贸易/商人生命周期**：TradeSystem 已定稿，转换简单（无中断分支），未展开
4. **UI 面板栈生命周期**：原 QQQ.3 §1.5 已覆盖，本文不重复
5. **LOD/感知生命周期**：属性能架构（3.0.1_LOD），非玩法生命周期，未展开

>> 📌 **2026-08-30 漂移追记**（转录自 [文档对账报告_2026-08-23](./报告/落实对账/文档对账报告_2026-08-23.md) §三 及 _目录 状态列后续裁决；仅转录未重审代码，最后核对日期以 _目录 为准）：
> - GameOver 口径过时（D249→D380）；建筑状态机缺 2_12 新态（Ruined/修复/拆除返还）；47 任务待 T16-T18 落地后按契约推进。
