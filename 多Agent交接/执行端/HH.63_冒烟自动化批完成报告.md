# HH.63 冒烟自动化批完成报告（D520 实施）

> 类型：交付报告（待验收）
> 状态：⏳待策划端验收
> 日期：2026-09-04 · 发起端：执行端 · 关联：HH.62 任务书（D520）· commit 待验收后代执

---

## 一、做了什么（三件套全量交付，编译 0 error）

### 1. WorldLifecycle（运行时门面，[WorldLifecycle.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Loading/WorldLifecycle.cs)）

`ResetWorldForNext()` 同场景「清场→重建」编排（不 LoadScene，留在 GameScene），纯编排现有接口，不碰业务逻辑。序列：

```
① InputManager.DisableInput + Time.timeScale=1
② GameStateManager.SetState(Loading)          ← 陷阱1：从 Playing 拉出，关只在 Playing 跑的 Update
③ TeardownScene()（Level 1：销毁全部单位+清君主引用+CleanupDestroyedSaveables）
④ 业务 Manager ResetState 全家（Time/Difficulty/Ruler/KingdomManager/PopulationSystem/RanchSystem/SiegeProductionSystem）
⑤ 散点清场（GridSystem.ClearAll / BuildingFactory.ClearAllBuildings / ChestManager.ClearAll
   / KingdomRegistry.ResetState / MapRenderService.ClearAllTiles）
⑥ UnitRegistry.Clear → WorldManager.ResetState（清 ActiveMap，解锁二次建局）
⑦ SaveManager.ResetSessionState
```

**⚠ AttentionSystem 修正**（实施中发现）：任务书 §一.1 列的 `AttentionSystem.ClearAll()` **不存在全局 Instance**——AttentionSystem 是每 NPCBrain 私有成员（[NPCBrain.cs L59](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/AI/NPCBrain.cs#L59) `private readonly AttentionSystem _attention = new AttentionSystem()`），随 TeardownScene 销毁全部单位自动清空，无需显式清。已在代码注释说明，未改业务侧。

### 2. SmokeApi（Editor-only，[SmokeApi.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/SmokeApi.cs)）

- `EnterGame(config)`：复现 GameBootstrap.StartNewGame 全链（InitializeNewGame + ResetAutoSaveCounter + SpawnInitialEntities + VagrantCampSystem.OnNewGameMapReady + 初始存档守卫）+ **ActiveMap 幂等守卫**（已存在世界先 ResetWorldForNext，防「二次初始化卡死」）。
- `ResetWorldForNext()`：薄封装 WorldLifecycle。
- `QuitSmoke()`：ResetWorldForNext + `EditorApplication.ExitPlaymode()`（跑完自动退出 Play，全程自动化闭环）。

### 3. 冒烟容器改造

- **[Valley2_20B_Smoke_M7.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_20B_Smoke_M7.cs)**：改为**四族固定 seed 各 1 局（4 轮）+ 换 seed 2 轮 = 6 轮自动跑**。每轮：SmokeApi.EnterGame → 等 ActiveMap → 跨轮污染负探针 → P1~P13（自适应国族）→ 清场负探针 → SmokeApi.ResetWorldForNext → yield（陷阱2）；末尾 QuitSmoke。
- **[Valley2_20_Smoke_Race.cs](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/Editor/Smoke/Valley2_20_Smoke_Race.cs)**：自建逻辑换 SmokeApi.EnterGame（**无条件建局**，矮人 22360）；结尾 ResetWorldForNext + QuitSmoke。

### 4. 交接文档

- HH.62 任务书尾部追加执行端开工回执（seed 清单 + 散点清场序列 + 陷阱五条核对 + 挂账）。

## 二、冒烟证据（零手动进局，全量 ALL PASS）

### 2_20B 六轮自动跑（点一次菜单 → execute_code 触发 Run → 自动完成）

| 轮 | 族 | seed | 结果 | 跨轮负探针（验收4） | 清场负探针 |
|---|---|---|---|---|---|
| R1 | 人类 | 22360 | **ALL PASS**（P1~P13） | —（首轮基准） | PASS（ActiveMap 空+UnitRegistry=0） |
| R2 | 精灵 | 22360 | **ALL PASS** | PASS（ActiveMap 新实例+上轮实体无残留） | PASS |
| R3 | 矮人 | 22360 | **ALL PASS** | PASS | PASS |
| R4 | 兽人 | 22360 | **ALL PASS** | PASS | PASS |
| R5 | 矮人 | 7841（换 seed） | **ALL PASS** | PASS | PASS |
| R6 | 兽人 | 31337（换 seed） | **ALL PASS** | PASS | PASS |

- 每轮世界真实重建：256×256 地图 + 2834 建筑实体 + 4 王国 + 玩家人口（日志实证：`[BuildingFactory] 2D 地图预置建筑实例化完成：2834 个`）。
- 收尾：`[WorldLifecycle] ResetWorldForNext: 清场完成` → `[SmokeApi] QuitSmoke: 退出 Play 模式`。
- 全程**零手动进局**；R2~R6 连续 5 次重建无卡死/无空引用。

### 2_20 单局（改接门面后回归）

`[2_20冒烟] ===== ALL PASS（种族域 D467~D472 行为级探针）=====`——⑤静态 5 项 + ①异族野性正/同族负 + ②压制/反击/射程外/移动焦点 4 项 + ④招募 4 项 + ③D471 定族 + ⑥M3 分布 + ⑦M5 消费链全绿，**与改造前结果一致**。

### 可视化实证（用户关切）

SmokeApi.EnterGame 走 InitializeNewGame 完整链路，**世界是真实渲染的**（非纯日志）：地图 tile（MapRenderService chunk 256×256）+ 2834 建筑实体 + 33 单位 + 4 王国全部真实创建。用户已在 Unity 编辑器亲眼确认世界可视化正常（2026-09-04）。退出 Play 后运行时对象随退出销毁，编辑模式看不到属正常（QuitSmoke 设计）。

## 三、陷阱五条对照结论（任务书 §四）

| # | 陷阱 | 结论 |
|---|------|------|
| 1 | GameStateManager 覆盖 | ✅ 编排层 `SetState(Loading)` 从 Playing 拉出（Singleton 无 ResetState）；每轮重建后 EnterPlaying 正常回 Playing |
| 2 | 一帧空窗 | ✅ 容器每轮 ResetWorldForNext 后 yield 1 帧（Loading 态安全）再进下一轮；6 轮实测无旧组件污染/无卡死 |
| 3 | TimeManager._dayTimer 残量 | ⚠️ **实读当前代码 [TimeManager.ResetState](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Time/TimeManager.cs#L271-L289) 已重置 `_dayTimer`**（L277 按起始时刻重算）——任务书此条基于旧版本假设，当前无需编排补偿；已核对澄清 |
| 4 | Save 前夜态 ±1 | ✅ 探针不断言存档内容；多轮建档 smoke_1~6 正常（首轮建、后续 HasSave 跳过），无 ±1 干扰 |
| 5 | SetState 转换日志 | ✅ 主动 SetState(Loading) 产生正常转换日志，未干扰探针 console 定位（探针全 PASS 无伪触发） |

## 四、验收标准逐条对照（任务书 §五）

| # | 验收标准 | 结果 |
|---|---------|------|
| 1 | 点一次菜单自动「建局→探针→清场→重建」多轮，全程零手动进局 | ✅ execute_code 触发 Run → 6 轮自动完成 → 自动退 Play |
| 2 | 清场后世界可重建无卡死 | ✅ R2~R6 连续 5 次重建，无卡死/无空引用（ActiveMap 清空链路验证） |
| 3 | 四族各一局全过 + 换 seed 轮全过 | ✅ R1~R4 四族 22360 全 ALL PASS；R5/R6 换 seed 7841/31337 全 ALL PASS |
| 4 | 跨轮污染行为级负探针（ActiveMap 新实例/UnitRegistry 清空/旧轮单位无残留） | ✅ 引用对比（ActiveMap != 上轮）+ UnitRegistry count=0 + 上轮探针实体引用 == null 全 PASS；清场后 ActiveMap 空 + UnitRegistry 真空（非结构性计数） |
| 5 | 编译 0 error；现有冒烟改接门面后结果与改造前一致 | ✅ 编译 0 error（WorldLifecycle+SmokeApi+两容器）；2_20B 6 轮 P1~P13 与批3 矮人局一致；2_20 单局 ALL PASS 与改造前一致 |

## 五、git status 全量清单（实测 2026-09-04）

```
M  Valley Rampart/Assets/Editor/Smoke/Valley2_20B_Smoke_M7.cs      （四族循环改造）
M  Valley Rampart/Assets/Editor/Smoke/Valley2_20_Smoke_Race.cs     （门面改造）
M  多Agent交接/策划端/HH.62_冒烟自动化批任务书_D520.md             （回执追加）
?? Valley Rampart/Assets/Editor/Smoke/SmokeApi.cs (+.meta)         （本批新增）
?? Valley Rampart/Assets/_Game/Systems/Loading/WorldLifecycle.cs (+.meta)（本批新增）
?? 图片资源/四族风格锚点/                                          （批3 挂账美术，非本批产物，随批与否请策划裁决）
```

> 注：`多Agent交接/_交接索引.md` 已登记（HH.62 状态更新），git 跟踪正常。

## 六、sim 义务

**零 sim 直改、零同步负担**（红线4）：本批只新增运行编排门面（WorldLifecycle）与 Editor 工具门面（SmokeApi），不触碰 AI.Core 决策核、TuningSnapshot/ProfessionSnapshot、champion、训练仓 harness。AI.Core 目录零改动。

## 七、挂账项 / 待确认

1. **VagrantCampSystem 营地残留**（任务书 §一.1 序列外散点）：VagrantCampSystem 无 Reset/Clear 接口（Level 2 清单未含），多轮间旧营地数据残留。本批无影响：2_20B 六轮探针不依赖营地、2_20 单局无跨轮。**建议后续补 ResetState**（挂 QQQ/后续批）。
2. **2_20 容器语义变更**（回执已声明）：原「ActiveMap 空才自建、否则走用户世界」→ 改接门面后**总是自建**（自动化接管）。真机世界不跑冒烟菜单；真机保留每批最终验收（D520 并存策略）。
3. **QuitSmoke 自动退 Play 行为**：6 轮跑完自动退出 Play（编辑模式看不到运行时世界属预期）。若策划/用户希望「跑完停留在最后一局世界供检查」，可加开关（如 `KeepWorldOnFinish`）——待确认是否要。
4. **陷阱3 澄清**（见 §三）：TimeManager.ResetState 当前已重置 _dayTimer，任务书此条已过时，无需动作。

## 八、验收请求

**请策划端验收本批**。验收通过后：①代执 commit（建议 message：`冒烟自动化批：WorldLifecycle 同场景清场→重建门面+SmokeApi Editor 门面+2_20/2_20B 改接门面（四族固定 seed 4 轮+换 seed 2 轮自动跑，D520）`）；②批4（M8+M9）解锁。期间策划端随时可启动 2_22 P0 清单签发。

---

## 九、策划端验收裁决（2026-09-04，D522）

**结论：✅ 验收成立。批4（M8+M9）解锁。**

**实盘复核（三笔关键声明+两门面全数实锤）**：

| 声明 | 核实 |
|---|---|
| WorldLifecycle 序列①~⑦（GameStateManager 覆盖/散点清场/ActiveMap 解锁） | ✅ 源码逐行核读，与任务书 §一 蓝本吻合；陷阱2 调用约定写入 XML 注释 |
| SmokeApi Editor-only+EnterGame 幂等守卫（ActiveMap 已存在先清再建） | ✅ 源码实读；全链=GameBootstrap.StartNewGame 对应 |
| AttentionSystem 修正（非全局单例，NPCBrain 私有成员随单位销毁清空） | ✅ AI.Core/Decision/AttentionSystem.cs 无 Instance 实锤——**诚实纠正评估报告 §2.2 早前散点误报，嘉奖** |
| 陷阱3「任务书过时」定性（TimeManager.ResetState 已重置 _dayTimer） | ✅ L277 按起始时刻重算实锤（任务书条目基于 2026-08-27 旧版记忆，QQQ.3 批已修）——**实读纠正策划端任务书，正确行为** |
| 跨轮负探针行为级 | ✅ L70~L71 基准引用+L115 ActiveMap != lastMap+L122 旧实体引用全销毁——引用断言非结构性计数，符合验收纪律 |
| 六轮证据（四族 22360+换 seed 7841/31337） | ✅ 全 ALL PASS+2834 建筑真实重建+R2~R6 连续 5 次重建无卡死 |

**验收标准五条逐条过**（§四对照表采信）：零手动进局 6 轮✅/重建无卡死✅/四族+换 seed 全过✅/跨轮污染行为级负探针✅/编译 0 error+2_20 回归一致✅。

**挂账四笔处置**：
1. **VagrantCampSystem 无 Reset 接口**→挂账池登记（轻度：跨轮营地数据残留，本批探针不依赖；后续批补 ResetState）。
2. **2_20 容器语义变更（总是自建）**→采信（与 D520「真机不跑冒烟菜单」并存策略自洽）。
3. **QuitSmoke 自动退 Play**→维持现状不加开关（YAGNI：用户已实证可视化正常；「跑完停留」需求出现再加 KeepWorldOnFinish）。
4. **陷阱3 澄清**→注记即可（任务书条目作废，无需动作）。

**验收意见一笔（非缺陷）**：评估报告 §2.2 的 AttentionSystem.ClearAll 散点条目系当时误报（执行端实施期实读自纠）——后续引用评估报告 §2.2 散点清单时注意此条已作废。

**落档**：0.6 §五十五 D522 · 队列（冒烟自动→完成区，批4 解锁）· 索引 HH.63 行。
