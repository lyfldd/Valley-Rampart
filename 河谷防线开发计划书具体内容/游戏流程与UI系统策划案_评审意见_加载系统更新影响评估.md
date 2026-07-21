# 评审意见 · 加载系统翻新后状态评估

> 日期：2026-07-21（初次评估） · 2026-07-21 20:50（风险修复后更新）
> 评估对象：[`游戏流程与UI系统策划案_评审意见.md`](./游戏流程与UI系统策划案_评审意见.md)
> 关联文档：[`../修复记录/审查_加载与数据传递链.md`](../修复记录/审查_加载与数据传递链.md)
>
> 评估背景：评审意见写于加载系统翻新前（v1.0 时代）。加载系统现已完成翻新 + 君主系统 4 个 Bug 修复 + 审查报告 6 个风险中的 5 个已修复。本评估反映最新代码状态。

---

## 0. 总评

**原评审意见 16 条问题：13 条已解决，2 条部分解决，1 条未涉及（UI 层面）。**

**审查报告 6 个风险：5 个已修复，1 个（R5 架构重构）有意延后。**

**当前剩余待办只有 3 项**：
1. P1-2 summary 填充 bug（`SaveManager.Save` 没填 summary，存档槽 UI 读到空值）
2. R5 RulerController 从 Prefab 移除（架构重构，当前代码防御够用故延后）
3. P2-2/P2-3 UI 层面问题（LoadingPanel + Paused ESC，待 UI 开发时处理）

**结论：原评审意见已过时，建议保留作为历史快照，不再作为开发依据。本评估文档替代它作为当前状态参考。**

---

## 1. 原评审意见 16 条逐条状态

### P0 · 严重问题（6/6 全部解决）

| 编号 | 问题 | 状态 | 验证位置 |
|------|------|------|----------|
| P0-1 | 任务清单和 v2 架构脱节 | ✅ 已解决 | `SaveableContracts.cs` 定义了 ISaveable/ISaveableSpawner/SavePayload/ModuleSaveEntry |
| P0-2 | ApplySnapshot 已废弃 | ✅ 已解决 | `SaveManager.Load` 实现三阶段：Global → SpawnFromSave → Scene |
| P0-3 | rulerName 没存档 | ✅ 已解决 | `RulerSaveData.rulerName` 字段已加，`RulerController.SaveState/LoadState` 都处理了 |
| P0-4 | mapSeed 没存档 | ✅ 已解决 | `WorldManager : ISaveable`，`WorldSaveData.mapSeed/difficulty/totalDays` |
| P0-5 | difficulty 没存档 | ✅ 已解决 | 用 `WorldManager` 存（评审建议的 GameConfigManager 没采用，WorldManager 担任此角色） |
| P0-6 | 新建游戏没初始存档 | ✅ 已解决 | `GameBootstrap.InitializeScene` 新建后自动 `SaveManager.Save` |

---

### P1 · 中等问题（3 条解决，1 条部分解决 + bug，1 条解决）

| 编号 | 问题 | 状态 | 验证位置 / 说明 |
|------|------|------|----------------|
| P1-1 | 存档设置存储方案 | ✅ 已解决 | 选了方案 B（`settings.json` 全局设置），`SaveManager.LoadSettings/SaveSettings` 已实现 |
| **P1-2** | GetSaveMeta 读取复杂 | ❌ **未解决（隐藏 bug）** | `GameSaveSummary` 结构定义了，`GameSaveRoot.summary` 字段加了，但 **`SaveManager.Save` 没有填充 summary 的逻辑**——存档写盘时 summary 全是默认空值 |
| P1-3 | 暂停保存默认选当前槽位 | ✅ 已解决 | `SaveManager.CurrentSlotId` 字段已加，Save 时自动设置 |
| P1-4 | ApplyConfig vs LoadState 职责 | ✅ 已解决 | `TimeManager` 用 `SetTotalDays`（NewGame）vs `LoadState`（Continue）；`WorldManager.ApplyConfig` 专门给 NewGame 用 |
| P1-5 | GameOver 后存档处理 | ✅ 已解决 | 方案 B：`GameSaveRoot.isFinished` + `SaveManager.MarkCurrentSaveFinished` + `Load` 时拒绝加载 |

#### P1-2 隐藏 bug 详情（当前唯一未修的 P0 级问题）

**位置**：`SaveManager.Save` 第 209-234 行

**现状**：
```csharp
var root = new GameSaveRoot
{
    saveVersion = CurrentSaveVersion,
    saveTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
    slotName = slotId
    // ← 没有填充 summary
};

foreach (var kvp in _saveables)
{
    // 只填充 modules，没有动 summary
    root.modules.Add(new ModuleSaveEntry { ... });
}
```

**后果**：存档槽 UI 调 `GetSaveMeta` 读 `root.summary`，拿到的是 `rulerName=null, currentDay=0, currentSeason=0, difficulty=0`——所有存档槽看起来都是空的。

**修复建议**：Save 时从各 ISaveable 抓取摘要信息填充 summary。推荐直接抓方案（独立游戏规模下耦合可接受）：
```csharp
root.summary = new GameSaveSummary
{
    rulerName = RulerController.Instance.RulerName,
    currentDay = TimeManager.Instance.CurrentDay,
    currentSeason = (int)TimeManager.Instance.CurrentSeason,
    difficulty = WorldManager.Instance.Difficulty
};
```

---

### P2 · 轻微问题（3 条解决，1 条部分解决，1 条未涉及）

| 编号 | 问题 | 状态 | 验证位置 / 说明 |
|------|------|------|----------------|
| P2-1 | Booting 状态两场景共用 | ✅ 已解决 | `GameBootstrap.Awake` 注释明确不设 Booting |
| P2-2 | Loading 状态触发时机 | ⚠️ 部分解决 | `GameBootstrap.Start` 设 Loading，但点"开始游戏"到 GameScene.Awake 之间仍有空窗期；LoadingPanel 还没做 |
| P2-3 | Paused 状态下 ESC 行为 | ❓ 未涉及 | 当前代码无暂停菜单实现，纯 UI 层面，待暂停菜单开发时处理 |
| P2-4 | 空槽位存档设置矛盾 | ✅ 已解决 | 选全局设置（settings.json）后矛盾消失 |
| P2-5 | 静态字段传参清理 | ✅ 已解决 | `GameBootstrap.OnDestroy` 清理 `IsLoadingSave/SaveSlotToLoad/NewGameConfig` |

---

## 2. 审查报告 6 个风险修复状态

这些风险是加载系统实现过程中产生的（评审意见写的时候加载系统还没实现，所以没覆盖）。详见 [`../修复记录/审查_加载与数据传递链.md`](../修复记录/审查_加载与数据传递链.md)。

| 编号 | 风险 | 优先级 | 状态 | 验证位置 |
|------|------|--------|------|----------|
| R1 | Singleton.Instance getter 自动创建副作用 | P2 | ✅ 已修 | `Singleton.cs` 第 29 行加 `Debug.LogWarning` 监控 |
| **R2** | 场景预置的非君主单位读档后双份 | **P0** | ✅ 已修 | `DestroyAllSceneMonarchs` → `DestroyAllSceneUnits`，清理所有 `UnitController`；`GameBootstrap.InitializeScene` 读档分支调用 |
| R3 | SpawnFromSave 不检查 SaveId 重复 | P1 | ✅ 已修 | `SaveManager.HasSaveable` 方法 + `UnitFactory.SpawnFromSave` 开头去重检查 |
| R4 | OverrideSaveId 注销-注册非原子 | P2 | ✅ 已修 | `SaveManager.ChangeSaveId` 原子方法 + `UnitController.OverrideSaveId` 改用它 |
| **R5** | RulerController 挂在 Prefab 上导致 Singleton 分离 | **P0** | ⚠️ **有意延后** | Awake 仍是 `Destroy(this)` 方案，注释仍写"挂在 Prefab 上"；但 4 步防御 + `DestroyAllSceneUnits` + `BindExistingMonarch` 已覆盖所有已知场景 |
| R6 | _saveables / _aliveUnits 跨场景不清理 | P1 | ✅ 已修 | `SaveManager.CleanupDestroyedSaveables` 方法 + `GameBootstrap.Awake` 调用 `UnitRegistry.Clear()` + `SaveManager.CleanupDestroyedSaveables()` |

### R5 延后说明

R5 是审查报告里唯一未修的 P0，但**有意延后**是合理的：
- 当前代码防御（4 步 SpawnMonarch + DestroyAllSceneUnits + BindExistingMonarch + Singleton Warning 监控）已覆盖所有已知场景
- RulerController 从 Prefab 移除需要改 Unity Editor 里的 Prefab 结构，是架构级重构
- 风险已通过其他方式缓解，不再是阻塞问题
- 建议在下次 Prefab 结构大改时一并处理

---

## 3. 当前剩余待办清单

按优先级排序，**只剩 4 项**：

| 优先级 | 来源 | 改什么 | 改哪里 | 状态 |
|--------|------|--------|--------|------|
| **P0** | P1-2 隐藏 bug | `SaveManager.Save` 填充 `summary` 字段 | `SaveManager.cs` | ❌ 待修 |
| P2 | R5 架构重构 | RulerController 从 Prefab 移除，挂独立管理器 GameObject | Unity Editor + `RulerController.cs` | ⚠️ 有意延后 |
| P2 | P2-2 | LoadingPanel 覆盖"开始游戏"到 GameScene 之间的空窗期 | MainMenuScene + 策划案 4.7 节 | ❌ 待做 |
| P2 | P2-3 | Paused 状态 ESC 行为（回到 Playing） | 暂停菜单开发时处理 | ❌ 待做 |

---

## 4. 对原评审意见的处置建议

原评审意见 [`游戏流程与UI系统策划案_评审意见.md`](./游戏流程与UI系统策划案_评审意见.md) **建议保留不动**，作为"加载系统翻新前"的历史快照。

理由：
- 16 条问题里 13 条已解决，原文件留着会误导新人去"修复"已经修好的问题
- 但原文件记录了当时的判断和推理过程，有历史价值
- 本评估文档已替代它作为当前状态参考

后续策划案和 v2 文档的更新，以本评估的"当前剩余待办清单"为准。

---

## 5. 一句话总结

评审意见 16 条问题已解决 13 条，加载系统翻新引入的 6 个风险已修 5 个。**当前只剩 1 个 P0 级 bug 待修**（summary 填充），其余都是 P2 级 UI 层问题或有意延后的架构重构。加载系统的稳定性和数据传递链已达到可投产状态。
