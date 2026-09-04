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
