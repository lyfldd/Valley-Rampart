# HH.62 冒烟自动化批任务书（D520 落地）

- **策划端**：签发（2026-09-04）
- **执行端**：TraeCode（接收后开工）
- **裁决源**：D520（0.6 §五十三 · 报告/执行端/冒烟自动化与生命周期API化评估报告_2026-09-04.md §七 裁决表）——四项裁决已用户拍板，本任务书为实施转化
- **状态**：待执行端开工回执（回执后实施，完成后 HH.63 完成报告交策划端验收）
- **规模预估**：约 0.5 人天（薄封装，纯编排现有接口）

---

## 一、范围（三件，蓝本=评估报告 §3.1 编排图）

### 1. WorldLifecycle（运行时门面，放 `Systems/`，约 60 行）

`ResetWorldForNext()`——同场景「清场→重建」编排（**不 LoadScene，留在 GameScene**）：

```
停输入 → TeardownScene（Level 1）
→ 业务 Manager ResetState 全家（复用 Level 2 ⑤ 序：Time/Difficulty/Ruler/KingdomManager/PopulationSystem/RanchSystem/SiegeProductionSystem）
→ GridSystem.ClearAll / BuildingFactory.ClearAllBuildings / ChestManager.ClearAll
→ KingdomRegistry.ResetState / MapRenderService.ClearAllTiles / AttentionSystem.ClearAll
→ UnitRegistry.Clear → WorldManager.ResetState（清 ActiveMap，解锁二次建局）
→ SaveManager.ResetSessionState
```

> Level 2 的 Manager 重置清单**不含** Grid/BuildingFactory/Chest/KingdomRegistry/MapRender 这些散点（靠 LoadScene 兜底）——同场景重建必须显式清，评估报告 §3.1 清单已全列。

### 2. SmokeApi（Editor-only，放 `Assets/Editor/`，约 40 行）

`EnterGame(NewGameConfig)` / `ResetWorldForNext()` / `QuitSmoke()` 薄封装——EnterGame 复用 `InitializeNewGame`（等价用户进局真实链路）。

### 3. 冒烟容器改造

现有 2_20/2_20B 容器开头改调 `SmokeApi.EnterGame`，结尾改调 `SmokeApi.ResetWorldForNext`（每容器约 20 行）。

## 二、策略（D520 定案）

- **四族固定 seed 各建一局（4 轮自动跑）**——点一次菜单自动「建局→探针→清场→重建」多轮
- **周批回归追加换 seed 1~2 轮**（防单 seed 压缩世界缺陷面——寻路1/2 教训：出生口袋吸附为 seed 相关缺陷）
- 自动化与真机并存：真机保留每批最终验收不动

## 三、红线

1. **WorldLifecycle 是运行时代码**（不进 Editor 文件夹）——2_14 传送门跨地图是它的潜在复用方
2. **纯编排现有接口**：不碰业务逻辑、不改现有系统内部（评估报告 §3.3 约束）
3. **接口纪律条款（D520 生效）**：对外暴露走门面方法，禁跨系统掏私有字段
4. **AI.Core/训练仓零触碰**（零 sim 义务，无同步负担）
5. SmokeApi 不得进主流程代码/构建

## 四、复位链时序陷阱清单（⚠️ 同场景重建风险高于 Level 2，逐条对照）

同场景清场重建不走 LoadScene，**所有状态残留都不会被场景重载兜底**。P0 验收批实战沉淀五条：

1. **复位链必须覆盖 GameStateManager**（SetState 关 ThroneAnchor 轮询窗口——Singleton 无 ResetState 易漏；旧态残留=伪 GameOver 风险）
2. **yield return null 一帧=旧组件还能跑一轮 Update**——销毁/禁用类操作要在重建前，勿留空窗
3. **TimeManager.ResetState 只回 day1 不清 `_dayTimer`**——残量会白嫖推进（如无法改内部，编排层补偿）
4. **Save 抓的是前夜态**——多轮建局的存档断言口径注意 ±1 假差
5. **GameStateManager.SetState 有转换日志**（→ GameOver）——探针抓 console 可定位异常触发者

## 五、验收标准（对执行端，D520 已定）

1. 编辑器点一次菜单自动完成「建局(任意族)→探针→清场→重建」多轮，**全程零手动进局**
2. 清场后世界可重建**无卡死**（ActiveMap 清空链路验证）
3. 四族各一局全过+换 seed 轮全过
4. **跨轮污染行为级负探针**：重建后 ActiveMap 为新实例、UnitRegistry 清空、旧轮探针单位/建筑无残留（结构性计数不作通过依据——按路由验收纪律）
5. 编译 0 error；现有冒烟（2_20/2_20B）改接门面后结果与改造前一致

## 六、流程

开工回执（HH.62 回写，含 seed 清单确认与散点清场序列过目）→ 实施 → 多轮冒烟 → 完成报告（HH.63，含四族 4 轮+换 seed 证据+陷阱五条对照结论）→ 策划端验收 → commit 代执 → **批4（M8+M9）解锁**。

---

## 执行端开工回执（TraeCode，2026-09-04）

**状态**：✅ 已回执开工（范围+锚点确认，下随实施）

**一、范围确认（三件套全收）**
1. `WorldLifecycle`（运行时，`Assets/_Game/Systems/Loading/WorldLifecycle.cs`，静态类约 70 行）——`ResetWorldForNext()` 同场景清场→重建编排，不 LoadScene。
2. `SmokeApi`（Editor-only，`Assets/Editor/Smoke/SmokeApi.cs`，约 45 行）——`EnterGame(config)`/`ResetWorldForNext()`/`QuitSmoke()` 薄封装。
3. 冒烟容器改造——2_20B 改四族固定 seed 4 轮+换 seed 2 轮（点一次菜单全自动）；2_20 改接门面（单局矮人 22360）。

**二、seed 清单确认（建议值，实施按此，策划有异议中途拦）**

| 轮 | 族 | raceId | seed | 说明 |
|---|---|---|---|---|
| 1 | 人类 | 0 | 22360 | 主 seed（与 2_20 一致，已知稳定世界） |
| 2 | 精灵 | 1 | 22360 | 主 seed |
| 3 | 矮人 | 2 | 22360 | 主 seed |
| 4 | 兽人 | 3 | 22360 | 主 seed |
| 5 | 矮人 | 2 | 7841 | 换 seed（周批回归防单 seed 缺陷面压缩） |
| 6 | 兽人 | 3 | 31337 | 换 seed（周批回归） |

**三、散点清场序列过目（任务书 §一.1 逐条对齐，含实读核对）**

```
ResetWorldForNext 序列（WorldLifecycle）：
① InputManager.DisableInput + Time.timeScale=1（停输入+恢复缩放，防销毁中误触/暂停残留）
② GameStateManager.SetState(Loading)   ← 陷阱1：从 Playing 拉出，关只在 Playing 跑的 Update（ThroneAnchor 轮询窗等）
③ TeardownScene()（Level 1：销毁全部单位+清君主引用+CleanupDestroyedSaveables）
④ 业务 Manager ResetState 全家（Time/Difficulty/Ruler/KingdomManager/PopulationSystem/RanchSystem/SiegeProductionSystem）
⑤ 散点清场（Level 2 靠 LoadScene 兜底、同场景必须显式清）：
   GridSystem.ClearAll / BuildingFactory.ClearAllBuildings / ChestManager.ClearAll
   / KingdomRegistry.ResetState / MapRenderService.ClearAllTiles / AttentionSystem.ClearAll
⑥ UnitRegistry.Clear → WorldManager.ResetState（清 ActiveMap，解锁二次建局）
⑦ SaveManager.ResetSessionState（槽位/自动存档计数，不清存档文件）
```

**四、陷阱五条逐条核对（实读磁盘当前代码）**

| # | 陷阱 | 核对结论 |
|---|------|---------|
| 1 | GameStateManager 覆盖 | ✅ 确认需覆盖：无 ResetState（Singleton），编排层用 `SetState(Loading)` 从 Playing 拉出 |
| 2 | 一帧空窗 | ✅ 注意：`BuildingFactory.ClearAllBuildings` 用 `Destroy`（延迟帧末）→ 冒烟容器每轮 `ResetWorldForNext` 后 `yield return null` 一帧（Loading 态下安全）再 `EnterGame`，让 Destroy 落地、避免旧 GameObject 与新世界共存 |
| 3 | TimeManager._dayTimer 残量 | ⚠️ 实读 [TimeManager.ResetState](file:///c:/Users/trs/Desktop/Valley%20Rampart/Valley%20Rampart/Assets/_Game/Systems/Time/TimeManager.cs#L271-L289) **当前已重置 `_dayTimer`**（L277 重新按起始时刻算）——无需编排层补偿；若策划端有历史版本顾虑请指明 |
| 4 | Save 前夜态 ±1 | ✅ 本批探针不断言存档内容，无影响；报告注明口径 |
| 5 | SetState 转换日志 | ✅ 编排层主动 SetState(Loading) 会产生正常转换日志（前缀 [GameStateManager]/[WorldLifecycle]），不干扰探针 console 定位 |

**五、设计决策声明（实施按此，属任务书红线内编排）**
1. `SmokeApi.EnterGame` 复现 GameBootstrap.StartNewGame 完整链路：`InitializeNewGame + ResetAutoSaveCounter + SpawnInitialEntities + VagrantCampSystem.OnNewGameMapReady + 初始存档守卫`——真正"等价用户进局"。
2. `EnterGame` 幂等守卫：若 `ActiveMap != null` 先 `ResetWorldForNext` 再建（任何状态进局都得干净世界，防"二次初始化卡死"）。
3. `QuitSmoke`：`ResetWorldForNext` + `EditorApplication.ExitPlaymode()`（跑完自动退出 Play，点一次菜单全程自动化闭环）。
4. **2_20 容器语义变更**：原"ActiveMap 空才自建、否则走用户世界"→ 改接门面后**总是自建**（自动化接管，真机世界不跑冒烟菜单；真机保留每批最终验收=直接 Play+手玩）。
5. **跨轮污染负探针**（验收标准4，行为级）：每轮 EnterGame 后断言「ActiveMap 为新引用（≠上轮）」「UnitRegistry 空」「上轮探针单位/建筑引用 == null（已销毁）」——不依赖结构性计数。

**六、挂账项（本批不处理，随报告申报）**
- `VagrantCampSystem` 无 Reset/Clear 接口（Level 2 清单未含）→ 多轮营地数据残留。本批无影响：2_20B 四族循环不依赖营地、2_20 单局无跨轮。建议后续补 ResetState（挂 QQQ/后续批）。

**七、下一步**：实施三件套 → 编译 0 error → 编辑器实跑多轮（零手动进局）→ HH.63 完成报告交策划端验收。
