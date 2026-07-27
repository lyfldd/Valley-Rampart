# 河谷防线 · 游戏流程与UI系统策划案 · 评审意见

> 版本：v1.0 · 2026-07-21
> 评审对象：[`游戏流程与UI系统策划案.md`](./游戏流程与UI系统策划案.md)
> 评审背景：策划案写于 v1 存档文档时代。存档系统已升级到 v2 ISaveable 架构（见 [`存档系统设计文档_v2_ISaveable架构.md`](./存档系统设计文档_v2_ISaveable架构.md)），策划案未同步更新，存在多处脱节和设计漏洞。

---

## 0. 总评

策划案的**流程设计本身没问题**——两场景拆分、GameState 扩展、新建/继续分叉、暂停/GameOver 流程都合理。

问题集中在两类：
1. **和 v2 存档架构脱节**——任务清单和流程图还在用 v1 的 `ApplySnapshot` / `RestoreFromSave` / `GameSaveData` 概念，这些在 v2 里已经被 `ISaveable` / `LoadState` / `GameSaveRoot` 替代。
2. **存档数据覆盖不全**——NewGameConfig 里的 `rulerName` / `mapSeed` / `difficulty` 在存档数据结构里没有对应字段，读档后丢失。

按严重程度分三级。**建议先处理 P0 再开工**，P1 在实施过程中修，P2 可以延后。

---

## 1. P0 · 严重问题（会导致功能跑不通或数据丢失）

### P0-1 · 任务清单和 v2 架构完全脱节

**位置：** 策划案第 19、27 行，任务 #6 和 #9

**现状：**
```
#6  SaveManager 完整实现 | 新文件：SaveManager.cs + GameSaveData / UnitSaveData /
    RulerSaveData / TimeSaveData POCO + JsonSaveSerializer（参照存档系统设计文档）
#9  UnitController 加 RestoreFromSave | 存档恢复方法
```

**问题：**
- `GameSaveData` 是 v1 的"上帝对象"，v2 已拆成 `GameSaveRoot` + 各模块自己的 `XxxSaveData`
- `JsonSaveSerializer` 在 v2 里取消了——每个 `ISaveable` 自己 `JsonUtility.ToJson`
- `RestoreFromSave` 是 v1 的方法名，v2 改成 `ISaveable.LoadState` + `OverrideSaveId`
- 任务 #6 参照的"存档系统设计文档"是 v1，v2 才是最新

**建议：** 任务 #6 和 #9 整体替换为 v2 改造清单（[`存档系统改造清单.md`](./存档系统改造清单.md)）的内容。策划案里的存档相关任务直接引用改造清单，不要重复写。

---

### P0-2 · 流程图里的 `ApplySnapshot` 已废弃

**位置：** 策划案第 202、409-418 行，第 3 节总图和第 4.7 节 Ready 分歧点

**现状（第 412-417 行）：**
```
SaveManager.Load(slotId)
  ├─ 读 JSON → GameSaveData       ← v1 概念，v2 是 GameSaveRoot
  ├─ 版本检查/迁移
  ├─ ApplySnapshot()              ← v1 方法，v2 里不存在
  │   ├─ 清空场景
  │   ├─ 恢复 TimeManager
  │   ├─ 恢复 RulerController 资源
  │   ├─ 遍历重建单位
  │   └─ InputManager.EnableInput()
  └─ → Playing
```

**问题：** v2 的 `SaveManager.Load` 内部已包含三个阶段（Global 恢复 → ISaveableSpawner 重建 → Scene 恢复），外部不需要也不能再调 `ApplySnapshot`。而且 v2 的清空场景逻辑有讲究——单位的 `Destroy` 要触发 `UnregisterSaveable`，不能简单"清空"。

**建议：** 第 4.7 节的 ContinueGame 分支改成：

```
GameSceneEntrance.CurrentMode == ContinueGame
  │
  ▼
SaveManager.Load(slotId)          ← 内部完成全部恢复
  ├─ 读 JSON → GameSaveRoot
  ├─ 版本检查/迁移
  ├─ 阶段1: Global 模块 LoadState（TimeManager/RulerController）
  ├─ 阶段1.5: UnitFactory.SpawnFromSave 重建单位实例
  ├─ 阶段2: Scene 模块 LoadState（每个 UnitController）
  └─ → Playing（由 GameBootstrap 触发）
```

`EnableInput` 不在 `SaveManager.Load` 里，由 `GameBootstrap` 在 Load 完成后统一调。

---

### P0-3 · `rulerName` 没存进存档

**位置：** 策划案第 315 行 `NewGameConfig.rulerName` vs v2 文档 `RulerSaveData`

**现状：**
- `NewGameConfig` 有 `rulerName`，新建游戏时玩家输入
- v2 的 `RulerSaveData` 只有 `gold / stone / wood / food`，**没有 `rulerName`**

**后果：** 玩家起的名字，存档后读档丢失。存档槽 UI（第 339 行）想显示"统治者名字"也读不到。

**建议：** v2 文档的 `RulerSaveData` 加字段：
```csharp
[Serializable]
public class RulerSaveData
{
    public string rulerName;   // ← 新增
    public int gold, stone, wood, food;
}
```
`RulerController.SaveState` / `LoadState` 同步处理这个字段。

---

### P0-4 · `mapSeed` 没存进存档

**位置：** 策划案第 285、317 行 `NewGameConfig.mapSeed`

**现状：** `NewGameConfig` 有 `mapSeed`，但存档数据结构里完全没存。

**后果：** 如果地图是程序生成的（种子驱动），读档后用默认 seed 重建地图，地形全变了——单位和建筑的位置对不上新地形。

**建议：** 如果本期地图不是程序生成的（手工摆放的固定地图），可以不存，但要在策划案里明确说明。如果是程序生成的，必须存。建议加一个 `WorldSaveData` 或把 seed 存进 `GameSaveRoot` 元数据：

```csharp
public class GameSaveRoot
{
    public int saveVersion;
    public string saveTime;
    public string slotName;
    public int mapSeed;          // ← 新增，世界级元数据
    public List<ModuleSaveEntry> modules;
}
```

**待阿铁确认：** 本期地图是固定的还是程序生成的？

---

### P0-5 · `difficulty` 没存进存档

**位置：** 策划案第 316 行 `NewGameConfig.difficulty`

**现状：** `NewGameConfig` 有 `difficulty`，`TimeSaveData` 存了 `totalDays` 但没存 `difficulty`。

**后果：** 读档后不知道当前游戏难度。难度如果影响敌人刷新率、资源消耗等，读档后逻辑会用默认难度，行为不一致。

**建议：** 策划案第 5 行任务 #5 让 `TimeManager` 加 `ApplyConfig(int totalDays, int difficulty)`——但 `difficulty` 应该是游戏全局属性，不归 `TimeManager` 管。建议：
- 新建一个 `GameConfigManager`（或在 `GameStateManager` 里加字段）管理 `difficulty` / `mapSeed` / `rulerName` 这类全局元数据
- 这个 Manager 实现 `ISaveable`，SaveId = `"GameConfig"`，LoadPhase = Global
- `GameSaveRoot` 不用加 `mapSeed` 字段，走 `ISaveable` 通道

---

### P0-6 · 新建游戏路径没有初始存档

**位置：** 策划案第 396-404 行，NewGame 分支的 `InitializeScene`

**现状：** NewGame 路径创建君主、单位、设资源，但**没有调 `SaveManager.Save`**。

**后果：** 玩家新建游戏后，在第一次手动保存前如果游戏崩溃或退出，进度全丢。而且"继续游戏"路径需要存档槽有内容才能选——新建游戏后存档槽还是空的，玩家无法通过"继续游戏"回到这局。

**建议：** NewGame 路径的 `InitializeScene` 完成后，自动存一个初始存档。槽位由玩家在 CharacterCreation 面板选（或自动分配第一个空槽）：

```
InitializeScene(newGameConfig)
  ├─ ... 创建君主、单位、设资源 ...
  ├─ InputManager.EnableInput()
  ├─ → Playing
  └─ SaveManager.Save(allocatedSlotId)   ← 新增：自动存初始档
```

**待阿铁确认：** CharacterCreation 面板要不要加"选择存档槽"？还是自动分配？

---

## 2. P1 · 中等问题（会导致体验问题或边界 bug）

### P1-1 · 存档设置子面板和 v2 架构冲突

**位置：** 策划案第 354-365 行，4.5.1 存档设置

**现状：** 策划案说"设置值随存档 JSON 一起存（存到 `GameSaveData` 的扩展字段）"+"保存设置时直接覆盖对应槽位的存档 JSON 中的设置字段（不重新抓快照）"。

**问题：**
- v2 没有 `GameSaveData`，是 `GameSaveRoot` + `List<ModuleSaveEntry>`
- "直接覆盖存档 JSON 中的设置字段"——v2 的模块数据是序列化后的 JSON 字符串，不能局部修改字段，要么全量重写要么反序列化-改-再序列化
- 空槽位没有存档文件，设置无处可存——但策划案说"此存档的自动存档"，隐含设置是每槽位独立的

**建议：** 两种方案二选一：
- **(A) 设置走 ISaveable：** 新建 `SaveSettingsManager : ISaveable`，SaveId = `"SaveSettings_{slotId}"`。但这样设置和存档绑定，空槽位还是没设置。
- **(B) 设置独立存储：** 设置不随存档走，单独存一个 `settings.json`（全局，不分槽位）。简单但失去"每槽位独立设置"能力。

**待阿铁确认：** 自动存档设置是全局的还是每槽位独立的？如果全局，方案 B；如果每槽位，方案 A 但要接受"空槽位用默认设置"。

---

### P1-2 · `GetSaveMeta` 元数据读取复杂

**位置：** 策划案第 352 行

**现状：** 策划案说"从 JSON 读取 `saveTime`、`rulerName`、`time.currentDay` 等元数据"。

**问题：** v2 的 `GameSaveRoot` 顶层只有 `saveTime` / `slotName` / `saveVersion`。`rulerName` 在 `RulerSaveData`（某个 module 的 json 字符串里），`currentDay` 在 `TimeSaveData`（另一个 module 的 json 字符串里）。要读这些，UI 得遍历 `modules` 找对应 `saveId`，再 `JsonUtility.FromJson` 反序列化。

**后果：** 存档槽 UI 代码会很啰嗦，每次刷新都要反序列化多个模块。

**建议：** 在 `GameSaveRoot` 加一个轻量级 `summary` 字段，保存时由 `SaveManager` 从各模块抓取关键信息填充：

```csharp
[Serializable]
public class GameSaveRoot
{
    public int saveVersion;
    public string saveTime;
    public string slotName;
    public GameSaveSummary summary;    // ← 新增：存档列表 UI 用的摘要
    public List<ModuleSaveEntry> modules;
}

[Serializable]
public class GameSaveSummary
{
    public string rulerName;
    public int currentDay;
    public int currentSeason;
    // UI 需要展示的字段都放这
}
```

**但这破坏了"SaveManager 不耦合业务字段"的原则。** 折中：让 `SaveManager` 在 Save 时发一个 `GameSavingEvent`，各 `ISaveable` 把自己的摘要信息塞进一个共享的 `summary` 对象，SaveManager 把 summary 和 modules 一起写盘。这样 SaveManager 还是不知道字段含义，只负责搬运。

**或者更简单：** 存档列表 UI 忍受一下反序列化开销。独立游戏规模下，3 个存档槽各反序列化 2-3 个模块，性能可忽略。

---

### P1-3 · 暂停保存没默认选中当前槽位

**位置：** 策划案第 449-451 行

**现状：** 暂停菜单点"保存游戏"→"弹出存档槽选择（存到哪个槽）"。

**问题：** 如果玩家正在玩的是 slot_2 的存档，保存时还要重新选槽位，体验差。而且如果玩家手滑选了 slot_1，会把另一局游戏覆盖。

**建议：** 记录"当前游戏来自哪个槽位"：
- `GameSceneEntrance.LoadSlotId`（ContinueGame 路径）或 NewGame 时分配的槽位
- 暂停保存时默认选中这个槽位，玩家可直接确认保存
- 如果玩家想另存为新槽位，再手动切换

需要一个 `SaveManager.CurrentSlotId` 字段记录当前活跃槽位。

---

### P1-4 · `ApplyConfig` 和 `LoadState` 职责区分

**位置：** 策划案第 536 行

**现状：** 策划案让 `TimeManager` 加 `ApplyConfig(int totalDays, int difficulty)`——这是 NewGame 路径用的。

**问题：** v2 里 `TimeManager` 还要实现 `ISaveable.LoadState`。两个方法都改 `TimeManager` 的配置字段（`totalDays` / `secondsPerDay` 等），容易混淆。

**建议：** 明确区分：
- `ApplyConfig`：**仅 NewGame 路径**调用，设置初始参数（来自 `NewGameConfig`）
- `LoadState`：**仅 ContinueGame 路径**调用，从存档恢复参数和状态
- 两条路径互斥，不会同时调

策划案第 4.7 节的流程图要标注清楚哪个方法在哪条路径调。

---

### P1-5 · GameOver 后存档处理

**位置：** 策划案第 475-501 行

**现状：** GameOver 后"返回主菜单"，存档还在，玩家可以从存档槽重新加载。

**问题：** 如果 GameOver 是因为君主死亡，存档里记录了君主 HP=0 或君主不在 `units` 列表。读档后君主还是死的，瞬间又 GameOver——死循环。

**建议：** 三选一：
- **(A) GameOver 自动删除存档：** 最简单，但玩家可能想读更早的存档重来
- **(B) GameOver 后存档标记为"已结束"：** 存档槽 UI 显示"已通关/已失败"，不可继续
- **(C) 存档时检查君主存活：** 君主死亡瞬间不让存档，只保留君主存活时的存档

**推荐 (B)：** 在 `GameSaveRoot` 加 `isFinished` 字段，GameOver 时存档标记。存档槽 UI 显示状态，"开始游戏"按钮变灰或改成"查看结算"。

---

## 3. P2 · 轻微问题（设计细节，可延后）

### P2-1 · Booting 状态两场景共用的执行顺序

**位置：** 策划案第 97、232-242 行

`GameStateManager` 是 Singleton，跨场景不销毁。MainMenuScene 进入 Booting → Splash → MainMenu → 加载 GameScene 时，GameScene 的 `CoreBootstrap.Awake` 如果又设 `SetState(Booting)`，会覆盖 MainMenu 设的状态。

**建议：** `CoreBootstrap` 只在 `GameStateManager.Instance` 首次创建时设 Booting，后续场景切换不重置。或者用 `DontDestroyOnLoad` 标记 `CoreBootstrap` 只在第一个场景挂，第二个场景不挂。

---

### P2-2 · Loading 状态的触发时机

**位置：** 策划案第 367-377 行

点"开始游戏"到 `GameScene.Awake` 之间有空窗期，状态可能还是 MainMenu。

**建议：** 点"开始游戏"时立即 `SetState(Loading)`，再 `SceneManager.LoadAsync`。`LoadingPanel` 可以挂在 MainMenuScene 里，覆盖在画面上直到 GameScene 加载完成。

---

### P2-3 · Paused 状态下 ESC 行为

**位置：** 策划案第 433-439、602 行

策划案说"Playing 中按 ESC → Paused"，但没说 Paused 中按 ESC。

**建议：** Paused 中按 ESC → 回到 Playing（和点"继续游戏"等效）。第 602 行的 ESC 统一返回逻辑要覆盖 Paused。

---

### P2-4 · 空槽位的存档设置逻辑矛盾

**位置：** 策划案第 4.5.1 节

策划案说设置是"此存档的自动存档"——每槽位独立。但空槽位没存档文件，设置无处可存。

**建议：** 如果走 P1-1 的方案 B（全局设置），这个问题消失。如果走方案 A（每槽位独立），空槽位不显示设置按钮。

---

### P2-5 · `GameSceneEntrance` 静态字段传参的清理

**位置：** 策划案第 131-159、421 行

静态字段在 Unity 进程重启后会重置，正常流程 OK。但如果玩家从 GameScene 退回主菜单时没 Clear，再新建游戏时可能读到旧数据。

**建议：** 策划案第 421 行已提到"Ready 读取后立即 Clear()"——补充：退出回主菜单时也要 Clear。

---

## 4. 修改建议汇总

按优先级排序的待改事项：

| 优先级 | 编号 | 改什么 | 改哪里 |
|---|---|---|---|
| P0 | 1 | 任务 #6 #9 替换为 v2 改造清单引用 | 策划案任务清单 |
| P0 | 2 | ContinueGame 分支流程图改 v2 三阶段 | 策划案第 4.7 节、第 3 节总图 |
| P0 | 3 | `RulerSaveData` 加 `rulerName` | v2 文档第 6 节、改造清单 |
| P0 | 4 | `mapSeed` 存档（待确认地图是否程序生成） | v2 文档 / 新建 GameConfigManager |
| P0 | 5 | `difficulty` 存档 | 新建 GameConfigManager |
| P0 | 6 | NewGame 路径自动存初始档 | 策划案第 4.7 节 NewGame 分支 |
| P1 | 1 | 存档设置存储方案（待确认全局/每槽位） | 策划案 4.5.1 |
| P1 | 2 | `GameSaveSummary` 或忍受反序列化开销 | v2 文档 / 存档槽 UI |
| P1 | 3 | 暂停保存默认选当前槽位 | 策划案 4.9 + SaveManager |
| P1 | 4 | `ApplyConfig` vs `LoadState` 职责标注 | 策划案 4.7 + 改造清单 |
| P1 | 5 | GameOver 后存档标记 `isFinished` | v2 文档 + 存档槽 UI |
| P2 | 1-5 | 执行顺序、ESC 行为、Clear 时机等 | 策划案对应小节 |

---

## 5. 需要阿铁确认的决策点

评审中发现 4 个设计选择需要你拍板：

1. **地图是否程序生成？** 决定 `mapSeed` 要不要存档（P0-4）
2. **CharacterCreation 要不要选存档槽？** 决定 NewGame 自动存档的槽位分配逻辑（P0-6）
3. **自动存档设置是全局还是每槽位？** 决定存档设置的存储方案（P1-1）
4. **GameOver 后存档怎么处理？** 删除 / 标记 / 禁止存（P1-5）

确认这 4 个后，我把策划案和 v2 文档同步更新。

---

## 6. 一句话总结

策划案的**流程架构没问题**，但**存档相关内容停留在 v1**，而且 NewGameConfig 里的 `rulerName` / `mapSeed` / `difficulty` 在存档数据结构里没对应字段——这三个字段不补，新建游戏存档后读档会丢名字、丢地图、丢难度。先确认上面 4 个决策点，再统一更新文档。
