# 河谷防线 · 游戏流程与UI系统策划案

> 版本：v1.1 · 2026-07-21
> 状态：已确认，待实施
>
> **v1.1 修订（2026-07-21）：** 同步 v2 ISaveable 存档架构。任务 #6 #9 更新；CharacterCreation 流程改为先选存档槽；NewGame 分支加 WorldManager + 自动存初始档；ContinueGame 分支改 v2 三阶段；暂停保存默认选当前槽位；退出回菜单前自动存档；GameOver 不删存档；自动存档设置改为全局。响应评审意见 P0/P1。

---

## 📋 任务清单

### 基础设施（按依赖顺序，必须先做）

| # | 任务 | 动什么 | 状态 |
|---|---|---|---|
| 1 | 扩展 GameState 枚举 | `GameStateManager.cs`：加 `Splash` / `MainMenu` / `CharacterCreation` / `SaveSlotSelect` | ⬜ |
| 2 | NewGameConfig + GameSceneEntrance | 两个新文件：`Data/NewGameConfig.cs`（POCO 配置类）、`Core/GameSceneEntrance.cs`（场景间参数传递桥接类） | ⬜ |
| 3 | CoreBootstrap 抽取 | 新文件 `Core/CoreBootstrap.cs`：从 GameBootstrap 拆出 Core 层初始化（Singleton/EventBus），两 Scene 都挂 | ⬜ |
| 4 | RulerController 配置方法 | 加 `SetRulerName(string)`、`ApplyStartResources(int, int, int, int)` | ⬜ |
| 5 | TimeManager 配置方法 | 加 `ApplyConfig(int totalDays, int difficulty)` | ⬜ |
| 6 | SaveManager 完整实现 | 参照 [`存档系统改造清单.md`](./存档系统改造清单.md)——v2 ISaveable 架构，新增 `SaveableContracts.cs` / `SaveRoot.cs` / `SaveManager.cs`，不写 `GameSaveData` 上帝对象 | ⬜ |

### 流程改造

| # | 任务 | 动什么 | 状态 |
|---|---|---|---|
| 7 | GameBootstrap 改造 | Ready 处读 `GameSceneEntrance.CurrentMode` 走分叉；InitializeScene 接收 NewGameConfig 参数 | ⬜ |
| 8 | GameEvents 加事件 | 加 `GameSavedEvent`、`GameLoadedEvent`（v2 文档已有定义） | ⬜ |
| 9 | UnitController 加 ISaveable | 实现 `ISaveable`（`SaveState`/`LoadState`/`OverrideSaveId`），`Initialize` 注册、`Die` 注销（见改造清单 #7） | ⬜ |

### UI 面板（逐个实施）

| # | 任务 | 状态 |
|---|---|---|
| 10 | SplashPanel（过场占位：黑底+Logo+任意键跳过） | ⬜ |
| 11 | MainMenuBootstrap（MainMenuScene 引导脚本，管理菜单流程状态切换） | ⬜ |
| 12 | MainMenuPanel（新建/继续/退出） | ⬜ |
| 13 | CharacterCreationPanel（名字/难度/种子/资源表单） | ⬜ |
| 14 | SaveSlotPanel + SaveSlotItem（3槽位+展开详情） | ⬜ |
| 15 | PausePanel（左侧滑出：继续/保存/退出） | ⬜ |
| 16 | GameOverPanel（结算面板+返回主菜单） | ⬜ |
| 17 | LoadingPanel（进度条+跨场景加载过渡） | ⬜ |

### 联调

| # | 任务 | 状态 |
|---|---|---|
| 18 | MainMenuScene 全流程走通：Splash → MainMenu → CharacterCreation → 加载 GameScene | ⬜ |
| 19 | MainMenuScene → 存档流程走通：Splash → MainMenu → SaveSlotSelect → 加载 GameScene | ⬜ |
| 20 | GameScene 暂停/退出流程走通：Playing ↔ Paused → 退出回主菜单 | ⬜ |
| 21 | GameOver 流程走通：Playing → GameOver → 回主菜单 | ⬜ |

> **状态符号**：⬜ 待做 · 🔲 进行中 · ✅ 完成

---

## 0. 前置依赖

- 基准文档：[存档系统设计文档 v2 · ISaveable 架构](./存档系统设计文档_v2_ISaveable架构.md) — 定义了 `ISaveable` 契约、`SaveManager`、各模块存档数据结构
- 改造清单：[存档系统改造清单](./存档系统改造清单.md) — 每个系统要改什么的逐文件清单
- 现有代码：`GameStateManager.cs`、`GameBootstrap.cs`、`GameEvents.cs`

---

## 1. GameState 枚举扩展

### 1.1 当前状态（缩小为"游戏内状态子集"）

```
Booting → Loading → Ready → Playing → Paused → GameOver
```

这 6 个状态保留，但全部限定在 **GameScene** 内使用。

### 1.2 新增的菜单流程状态

```csharp
public enum GameState
{
    // ===== 菜单流程（MainMenuScene）=====
    Booting,            // 启动中（两个场景共用，引擎初始化）
    Splash,             // 过场动画（LOGO占位）
    MainMenu,           // 主菜单（新建游戏 / 继续游戏 / 退出）
    CharacterCreation,  // 角色与世界创建（新建游戏路径）
    SaveSlotSelect,     // 存档槽选择（继续游戏路径）

    // ===== 游戏内流程（GameScene）=====
    Loading,            // 加载静态配置 + 场景资源
    Ready,              // 配置就绪，等待初始化（分歧点）
    Playing,            // 游戏运行中
    Paused,             // 暂停（左侧滑出菜单）
    GameOver            // 游戏结束
}
```

### 1.3 状态归属

| 状态 | 所在 Scene | 说明 |
|---|---|---|
| Booting | 两个场景共有 | 引擎层初始化（Singleton、EventBus），两场景各自挂的引导脚本都会经过 |
| Splash | MainMenuScene | 过场动画 |
| MainMenu | MainMenuScene | 主菜单 UI |
| CharacterCreation | MainMenuScene | 创建面板 UI |
| SaveSlotSelect | MainMenuScene | 存档槽 UI |
| Loading | GameScene | 只跨场景加载时触发一次 |
| Ready | GameScene | 初始化前的中间态 |
| Playing | GameScene | 正常运行 |
| Paused | GameScene | 暂停菜单 |
| GameOver | GameScene | 结算界面 |

---

## 2. 场景架构

### 2.1 两个 Scene

```
MainMenuScene
  ├─ 挂 MainMenuBootstrap（轻量引导）
  ├─ 包含：Splash 面板、MainMenu 面板、CharacterCreation 面板、SaveSlotSelect 面板
  └─ 不包含任何游戏系统（无 Unit、无 Tilemap、无 TimeManager）

GameScene
  ├─ 挂 GameBootstrap（现有，需改造）
  ├─ 包含所有游戏系统
  └─ 由 GameSceneEntrance 决定初始化路径
```

### 2.2 场景切换机制

使用 `GameSceneEntrance` 静态桥接类传递参数：

```csharp
public static class GameSceneEntrance
{
    public enum Mode { NewGame, ContinueGame }

    public static Mode CurrentMode;
    public static NewGameConfig NewGameConfig;   // 新建游戏时有效
    public static string LoadSlotId;             // 继续游戏时有效

    public static void SetNewGame(NewGameConfig config)
    {
        CurrentMode = Mode.NewGame;
        NewGameConfig = config;
        LoadSlotId = config.selectedSlotId;  // 新建游戏也要记住选了哪个槽
    }

    public static void SetContinue(string slotId)
    {
        CurrentMode = Mode.ContinueGame;
        NewGameConfig = null;
        LoadSlotId = slotId;
    }

    public static void Clear()
    {
        CurrentMode = Mode.NewGame;
        NewGameConfig = null;
        LoadSlotId = null;
    }
}
```

---

## 3. 完整流程总图

```
┌─────────────────────────────────────────────────────────────┐
│                      MainMenuScene                          │
│                                                             │
│  Booting ──→ Splash ──→ MainMenu                            │
│    │           │            │                                │
│    │      黑屏+Logo      ┌──┴──────────────┐                │
│    │      2-3秒          │                 │                 │
│    │      任意键跳过     ▼                 ▼                 │
│    │              新建游戏             继续游戏               │
│    │                  │                   │                  │
│    │                  ▼                   ▼                  │
│    │          CharacterCreation    SaveSlotSelect            │
│    │          (填参数)             (3个槽位)                  │
│    │                  │                   │                  │
│    │          GameSceneEntrance    GameSceneEntrance         │
│    │          .SetNewGame()        .SetContinue(slotId)      │
│    │                  │                   │                  │
│    │                  └────────┬──────────┘                  │
│    │                           │                             │
│    │                    SceneManager.LoadAsync               │
│    │                    ("GameScene")                        │
└────┼─────────────────────────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────────────────────────────────────┐
│                       GameScene                             │
│                                                             │
│  Loading ──→ Ready ──→ ══ 分歧点 ══                         │
│    │           │            │                                │
│    │      读GameScene-  ┌──┴──────────┐                     │
│    │      Entrance      │             │                      │
│    │      .CurrentMode  ▼             ▼                      │
│    │                NewGame       ContinueGame               │
│    │                    │             │                      │
│    │           InitializeScene   SaveManager.Load            │
│    │           ├─WorldManager    ├─阶段1:Global LoadState    │
│    │           │ .ApplyConfig    │ ├─WorldManager             │
│    │           ├─TimeManager     │ ├─TimeManager              │
│    │           │ .ApplyConfig    │ └─RulerController          │
│    │           ├─SpawnMonarch    ├─阶段1.5:SpawnFromSave     │
│    │           ├─创建默认单位    ├─阶段2:Scene LoadState      │
│    │           ├─EnableInput     └─EnableInput                │
│    │           └─Save(初始档)        │                        │
│    │                    │             │                      │
│    │                    └──────┬─────┘                       │
│    │                           ▼                             │
│    │                        Playing                          │
│    │                           │                             │
│    │                    ┌──────┼──────┐                      │
│    │                    │      │      │                      │
│    │                    ▼      ▼      ▼                      │
│    │                 Paused  ESC   单位死亡/                  │
│    │                    │          条件触发                   │
│    │              ┌─────┼─────┐     │                        │
│    │              ▼     ▼     ▼     ▼                        │
│    │           继续  保存  退出  GameOver                     │
│    │           游戏  游戏  游戏     │                         │
│    │            │     │     │      │                          │
│    │            ▼     ▼     ▼      ▼                          │
│    │         Playing Playing  加载        主菜单              │
│    │                         MainMenuScene                   │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. 各状态详细设计

### 4.1 Booting（启动）

| 项目 | 内容 |
|---|---|
| **Scene** | MainMenuScene 或 GameScene（哪个场景先加载就走哪个） |
| **触发** | 引擎自动（`RuntimeInitializeOnLoadMethod` 或首个场景 Awake） |
| **职责** | 创建 Core 层单例：`Singleton` 基类注册、`EventBus` 初始化、`GameStateManager` 就绪 |
| **加载内容** | 无（纯代码初始化） |
| **UI** | 无 |
| **输出** | 自动进入 Splash（如果在 MainMenuScene）或 Loading（如果在 GameScene） |
| **实现位置** | 现有 `GameBootstrap.Awake` 前半部分，抽取到独立 `CoreBootstrap` |

### 4.2 Splash（过场动画占位）

| 项目 | 内容 |
|---|---|
| **Scene** | MainMenuScene |
| **触发** | Booting 完成 |
| **职责** | 显示黑底+游戏 Logo，展示 2-3 秒或按任意键跳过 |
| **加载内容** | Logo Sprite（预制在 SplashPanel 里） |
| **UI** | `SplashPanel`：居中 Logo 图 + 底部 "按任意键继续" 提示文字闪烁 |
| **输入** | `Input.anyKeyDown` → 跳转 MainMenu |
| **输出** | 进入 MainMenu |

### 4.3 MainMenu（主菜单）

| 项目 | 内容 |
|---|---|
| **Scene** | MainMenuScene |
| **触发** | Splash 结束，或从 GameScene 退出回来 |
| **UI 布局** | 居中竖排菜单 |
| **按钮列表** | |
| | **新建游戏** | 点击 → CharacterCreation |
| | **继续游戏** | 点击 → SaveSlotSelect；无存档时灰色不可点（检测 `SaveManager.HasAnySave()`） |
| | **退出游戏** | 点击 → `Application.Quit()` |
| | （预留）**设置** | 音效/画面设置，本期不做 |
| **背景** | 静态美术图或暗色背景，本期不处理 |
| **输入** | 鼠标点击按钮 |

### 4.4 CharacterCreation（角色与世界创建）

| 项目 | 内容 |
|---|---|
| **Scene** | MainMenuScene |
| **触发** | MainMenu 点击「新建游戏」 |
| **UI 布局** | 居中面板，表单式布局 |

#### 流程（先选槽位再填参数）

```
点击「新建游戏」
  │
  ▼
检查存档槽空位（SaveManager.HasSave("slot_1/2/3")）
  │
  ├─ 有空位 → 显示空位列表供选择
  │           （已占用的槽位灰色不可选，或允许覆盖但需二次确认）
  │
  └─ 三个槽位全满 → 提示"存档已满，请先删除一个存档" → 返回 MainMenu
  │
  ▼
选定槽位后（如 "slot_2"）→ 显示参数表单
  │
  ▼
填参数 → 点击「开始游戏」
  │
  ▼
GameSceneEntrance.SetNewGame(config, selectedSlotId)
  │
  ▼
加载 GameScene
```

**关键变化：** `GameSceneEntrance.SetNewGame` 需要接收选定的槽位 ID，进入 GameScene 后自动存初始档到这个槽位（见 4.7 NewGame 分支）。

#### 可配置参数

| 参数 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| 统治者名字 | string | "无名君主" | 输入框，2-8个字符 |
| 难度 | 枚举 | Normal | Easy / Normal / Hard 三个按钮 |
| 地图种子 | int | 随机 | 输入框 + "随机"按钮；0 表示随机 |
| 起始金币 | int | 100 | 滑条或输入，范围 50-500 |
| 起始石料 | int | 100 | 同上 |
| 起始木材 | int | 100 | 同上 |
| 起始粮食 | int | 100 | 同上 |
| 初始天数 | int | 30 | 游戏总天数（赛季长度），30/60/90 |

#### 难度预设（选难度自动填充）

| 难度 | 金币 | 石料 | 木材 | 粮食 | 总天数 |
|---|---|---|---|---|---|
| Easy | 200 | 200 | 200 | 200 | 90 |
| Normal | 100 | 100 | 100 | 100 | 60 |
| Hard | 50 | 50 | 50 | 50 | 30 |

选择难度后自动填充，玩家仍可手动微调。

#### 按钮

| 按钮 | 行为 |
|---|---|
| **开始游戏** | 校验参数 → `GameSceneEntrance.SetNewGame(config)` → 加载 GameScene |
| **返回** | 回到 MainMenu |

#### NewGameConfig 数据结构

```csharp
[Serializable]
public class NewGameConfig
{
    public string rulerName;
    public int difficulty;       // 0=Easy, 1=Normal, 2=Hard
    public int mapSeed;
    public int startGold, startStone, startWood, startFood;
    public int totalDays;
    public string selectedSlotId;  // 新建游戏时玩家选定的存档槽位（如 "slot_2"）
}
```

### 4.5 SaveSlotSelect（存档槽选择）

| 项目 | 内容 |
|---|---|
| **Scene** | MainMenuScene |
| **触发** | MainMenu 点击「继续游戏」 |
| **UI 布局** | 3 个存档槽，垂直排列 |

#### 存档槽 UI（每个槽）

**空槽位：**
- 显示槽位编号（"存档 1 / 存档 2 / 存档 3"）
- 灰色背景，文字 "空"
- 不可点击

**有存档的槽位（默认折叠）：**
- 显示缩略信息：存档时间、第 N 天、统治者名字
- 点击展开下拉详情

**展开后（下拉区域）：**

| 按钮 | 行为 |
|---|---|
| **开始游戏** | `GameSceneEntrance.SetContinue("slot_1")` → 加载 GameScene |
| **删除存档** | 弹确认弹窗 "确定删除此存档？" → 确定则 `SaveManager.Delete("slot_1")` → 刷新槽位列表 |
| **存档设置** | 弹设置子面板（见 4.5.1） |

#### 存档元数据读取

进入 SaveSlotSelect 时，调用 `SaveManager.GetSaveMeta("slot_1")` / `("slot_2")` / `("slot_3")`，从 JSON 读取 `saveTime`、`summary.rulerName`、`summary.currentDay`、`summary.currentSeason`、`summary.difficulty` 等元数据用于显示。**不加载完整存档，只读 `GameSaveRoot.summary` 头部信息。**

> ⚠️ **已知 bug（P1-2 · 待修）**：`GameSaveSummary` 结构已定义、`GameSaveRoot.summary` 字段已加，但 `SaveManager.Save` **没有填充 summary 的逻辑**——存档写盘时 summary 全是默认空值，存档槽 UI 调 `GetSaveMeta` 读到的是 `rulerName=null, currentDay=0, currentSeason=0, difficulty=0`，所有存档槽看起来都是空的。修复方案见第 8 节 #9。

#### 4.5.1 自动存档设置子面板

点击「存档设置」后弹出一个小面板：

| 设置项 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| 自动存档 | bool | ON | 开启/关闭自动存档 |
| 自动存档间隔（天） | int | 3 | 每过 N 天自动存档一次 |

> **自动存档机制（全局设置）：** 自动存档存到 `SaveManager.CurrentSlotId`（当前正在玩的存档槽），不需要每槽位独立设置。
>
> **触发时机：**
> 1. `TimeManager` 的 `TimeDayChangedEvent` 触发时，按间隔天数判断是否自动存档
> 2. 玩家暂停点「退出游戏」回主菜单前，自动存档一次
>
> **存储位置：** 设置值独立存一个 `settings.json`（全局，不分槽位），路径 `Application.persistentDataPath/settings.json`。不随游戏存档走，避免空槽位无存档文件时设置无处可存的矛盾。

### 4.6 Loading（加载中）

| 项目 | 内容 |
|---|---|
| **Scene** | GameScene（异步加载中） |
| **触发** | `SceneManager.LoadAsync("GameScene")` 开始 |
| **职责** | 加载 GameScene；GameBootstrap.Awake 触发静态配置加载（`UnitDataManager.LoadAll` 等） |
| **UI** | `LoadingPanel`：进度条 + 提示文字（"河谷防线 正在准备..."） |
| **超时处理** | 30 秒无响应 → 显示错误提示 + 重试按钮 |
| **输出** | 所有配置就绪 → Ready |
| **Edge Case** | 加载失败（资源缺失）→ 显示错误信息，提供返回主菜单按钮 |

### 4.7 Ready（初始化前分歧点）

| 项目 | 内容 |
|---|---|
| **Scene** | GameScene |
| **触发** | Loading 完成（`UnitDataLoadedEvent.IsSuccess == true`） |
| **职责** | 读取 `GameSceneEntrance.CurrentMode`，进入对应分支 |
| **不显示 UI** | 瞬间决策，无独立 UI |

**两条分支：**

```
Ready
  │
  ├── GameSceneEntrance.CurrentMode == NewGame
  │     │
  │     ▼
  │   InitializeScene(newGameConfig)
  │     ├─ WorldManager.ApplyConfig(seed, difficulty, totalDays)  ← 新增：世界配置 + 地图生成
  │     ├─ TimeManager.ApplyConfig(totalDays, difficulty)          ← 新建时设初始参数
  │     ├─ UnitFactory.PreloadAll()
  │     ├─ RulerController.SetRulerName(config.rulerName)
  │     ├─ RulerController.ApplyStartResources(gold, stone, wood, food)
  │     ├─ RulerController.SpawnMonarch()
  │     ├─ 创建默认单位
  │     ├─ InputManager.EnableInput()
  │     ├─ → Playing
  │     └─ SaveManager.Save(config.selectedSlotId)                 ← 新增：自动存初始档
  │           （SaveManager 内部记 CurrentSlotId = selectedSlotId）
  │
  └── GameSceneEntrance.CurrentMode == ContinueGame
        │
        ▼
      SaveManager.Load(slotId)          ← v2 架构，内部完成全部恢复
        ├─ 读 JSON → GameSaveRoot
        ├─ 版本检查/迁移
        ├─ 阶段1: Global 模块 LoadState
        │   ├─ WorldManager.LoadState   ← 恢复 mapSeed + 重新生成地图
        │   ├─ TimeManager.LoadState    ← 恢复时间
        │   └─ RulerController.LoadState ← 恢复名字 + 资源
        ├─ 阶段1.5: UnitFactory.SpawnFromSave  ← 重建单位实例
        ├─ 阶段2: Scene 模块 LoadState   ← 恢复单位状态
        ├─ SaveManager 记 CurrentSlotId = slotId
        └─ → Playing（由 GameBootstrap 触发）
```

> **关键：GameSceneEntrance 在 Ready 读取后立即 Clear()**，防止下次从 GameScene 回主菜单又回来时读到脏数据。

### 4.8 Playing（游戏运行中）

| 项目 | 内容 |
|---|---|
| **Scene** | GameScene |
| **触发** | Ready 分支执行完毕 |
| **运行内容** | 所有游戏系统正常 Tick：TimeManager 推进时间、Unit 行为、玩家输入等 |
| **UI** | 游戏 HUD：TopLeftHUD（资源面板）+ ResourceHUD |
| **暂停触发** | ESC 键 → Paused |

### 4.9 Paused（暂停）

| 项目 | 内容 |
|---|---|
| **Scene** | GameScene |
| **触发** | Playing 中按 ESC |
| **行为** | `Time.timeScale = 0`；`GameStateManager.SetState(Paused)` |
| **UI** | 左侧滑出面板，半透明暗色遮罩覆盖游戏画面 |

#### 暂停菜单 UI

```
┌─────────────────────┐
│                     │
│    继续游戏  ▶      │  ← 点击：Time.timeScale = 1；SetState(Playing)
│                     │
│    保存游戏  💾     │  ← 点击：弹出存档槽选择
│                     │             默认选中当前槽位（SaveManager.CurrentSlotId）
│                     │             玩家可直接确认保存，或切换到其他槽位（覆盖需二次确认）
│                     │             → SaveManager.Save(slotId)
│                     │             → 提示"保存成功" → 回到 Playing
│                     │
│    退出游戏  🚪     │  ← 点击：弹确认弹窗
│                     │
└─────────────────────┘
```

#### 退出确认弹窗

```
┌──────────────────────────────┐
│                              │
│    ⚠️ 进度未保存，确定退出？   │
│                              │
│    [取消]      [确定退出]     │
│                              │
└──────────────────────────────┘
```

- 取消 → 关闭弹窗，留在 Paused
- 确定退出 → **先自动存档到当前槽位**（`SaveManager.Save(SaveManager.CurrentSlotId)`）→ `Time.timeScale = 1` → `SceneManager.LoadAsync("MainMenuScene")`
  - MainMenuScene 加载完成后自动进入 MainMenu 状态
  - 自动存档保证玩家进度不丢，下次可从"继续游戏"回到退出前的状态

### 4.10 GameOver（游戏结束）

| 项目 | 内容 |
|---|---|
| **Scene** | GameScene |
| **触发** | 君主死亡、天数耗尽、或满足失败/胜利条件 |
| **UI** | 全屏结算面板 |

#### 结算面板

```
┌──────────────────────────────────┐
│                                  │
│         ⚔️ 河谷失守 ⚔️           │
│                                  │
│     存活天数：42 天               │
│     击杀敌人数：156               │
│     最终资源：金200 石150 木80    │
│     统治评价：平庸的领主           │
│                                  │
│     [返回主菜单]                  │
│                                  │
└──────────────────────────────────┘
```

- 返回主菜单 → 加载 MainMenuScene → 进入 MainMenu 状态
- GameOver 状态下时间停止推进（TimeManager 只有 Playing 下才走）
- **存档不删除**——GameOver 只是游戏内目标达成/失败的逻辑处理，玩家可从存档槽重新读档回到 GameOver 前的检查点继续玩
- **注意：** 如果 GameOver 由君主死亡触发，存档里记录的是君主存活时的状态（自动存档在天更新时触发，君主死亡当天的存档还没生成），读档后君主仍存活，可继续游戏

---

## 5. 状态转换规则表

| 当前状态 | 可转换到 | 触发条件 |
|---|---|---|
| Booting | Splash | 引擎初始化完成（MainMenuScene） |
| Booting | Loading | 引擎初始化完成（GameScene） |
| Splash | MainMenu | 玩家按任意键 或 超时 2-3 秒 |
| MainMenu | CharacterCreation | 点击「新建游戏」 |
| MainMenu | SaveSlotSelect | 点击「继续游戏」 |
| CharacterCreation | MainMenu | 点击「返回」 |
| CharacterCreation | Loading | 点击「开始游戏」→ 加载 GameScene |
| SaveSlotSelect | MainMenu | 点击「返回」 |
| SaveSlotSelect | Loading | 点击存档槽的「开始游戏」→ 加载 GameScene |
| Loading | Ready | 静态配置加载完成 |
| Ready | Playing | InitializeScene 或 ApplySnapshot 完成 |
| Playing | Paused | 按 ESC |
| Playing | GameOver | 君主死亡 / 天数耗尽 / 条件触发 |
| Paused | Playing | 点击「继续游戏」 |
| Paused | MainMenu | 「退出游戏」→ 确认弹窗 → 加载 MainMenuScene |
| GameOver | MainMenu | 点击「返回主菜单」→ 加载 MainMenuScene |

---

## 6. 需要修改的现有代码

| 文件 | 改动 | 说明 |
|---|---|---|
| `GameStateManager.cs` | 扩展 GameState 枚举 | 加 Splash、MainMenu、CharacterCreation、SaveSlotSelect |
| `GameBootstrap.cs` | 拆分逻辑 | 原 Awake 的 Core 初始化提取到 `CoreBootstrap`；原 InitializeScene 改为接收 NewGameConfig 参数；Ready 处根据 `GameSceneEntrance.CurrentMode` 走分支；NewGame 分支末尾自动存初始档 |
| `GameEvents.cs` | 加事件 | `GameSavedEvent`、`GameLoadedEvent`（v2 文档已有定义） |
| `RulerController.cs` | 加方法 + 实现 ISaveable | `SetRulerName(string)`、`ApplyStartResources(int, int, int, int)`；实现 `ISaveable`（含 `rulerName` 存档） |
| `TimeManager.cs` | 加方法 + 实现 ISaveable | `ApplyConfig(int totalDays, int difficulty)` — 仅 NewGame 路径调用；实现 `ISaveable`（LoadState 仅 ContinueGame 路径调用） |
| `WorldManager.cs`（新增） | 新文件 | `Singleton<WorldManager>, ISaveable`——管理 `mapSeed`/`difficulty`/`totalDays`，触发程序化地图生成 |

### 6.1 新增文件

| 文件 | 位置 | 说明 |
|---|---|---|
| `CoreBootstrap.cs` | `_Game/Core/` | 引擎层初始化（从 GameBootstrap 抽取），两场景都挂 |
| `GameSceneEntrance.cs` | `_Game/Core/` | 场景间参数传递桥接类 |
| `NewGameConfig.cs` | `_Game/Data/` | 新建游戏配置 POCO |
| `MainMenuBootstrap.cs` | `_Game/Systems/UI/` | MainMenuScene 的引导脚本，管理菜单流程状态 |
| `SplashPanel.cs` | `_Game/Systems/UI/` | 过场动画 UI |
| `MainMenuPanel.cs` | `_Game/Systems/UI/` | 主菜单 UI |
| `CharacterCreationPanel.cs` | `_Game/Systems/UI/` | 角色创建 UI |
| `SaveSlotPanel.cs` | `_Game/Systems/UI/` | 存档槽选择 UI |
| `SaveSlotItem.cs` | `_Game/Systems/UI/` | 单个存档槽 UI 组件 |
| `PausePanel.cs` | `_Game/Systems/UI/` | 暂停菜单 UI |
| `GameOverPanel.cs` | `_Game/Systems/UI/` | 结算面板 UI |
| `LoadingPanel.cs` | `_Game/Systems/UI/` | 加载过渡 UI |

---

## 7. 实施顺序

按依赖关系排列，数字越小越先做：

```
1. 扩展 GameState 枚举（加 4 个新状态）
     ↓
2. 创建 GameSceneEntrance + NewGameConfig（数据结构，无依赖）
     ↓
3. 抽取 CoreBootstrap（从 GameBootstrap 拆出 Core 初始化部分）
     ↓
4. 改造 GameBootstrap（支持新/续两条路径）
     ↓
5. RulerController / TimeManager 加配置方法
     ↓
6. 主菜单 UI 套件（SplashPanel → MainMenuPanel）
     ↓
7. CharacterCreationPanel（新建游戏路径打通）
     ↓
8. SaveManager 实现（存档系统设计文档）
     ↓
9. SaveSlotPanel + SaveSlotItem（继续游戏路径打通）
     ↓
10. PausePanel（暂停菜单）
     ↓
11. GameOverPanel（结算）
     ↓
12. LoadingPanel（加载过渡）
     ↓
13. 联调：全流程走通 → 修边角 → 收工
```

---

## 8. 待确认事项 & 风险点

| # | 事项 | 状态 |
|---|---|---|
| 1 | **两场景拆分**：MainMenuScene 和 GameScene 的创建在 Unity Editor 中手动操作。策划案不涉及 Unity 场景文件的具体操作步骤。 | 待实施 |
| 2 | **自动存档**：UI 留了设置入口，但功能本期不做。`SaveManager.AutoSave` 方法留接口。 | 已知，延后 |
| 3 | **设置面板**（音效/画面）：MainMenu 预留按钮，本期不做。 | 已知，延后 |
| 4 | **主菜单背景**：暂用纯色背景。美术资源不在本期范围。 | 已知 |
| 5 | **GameOver 触发条件**：目前只有君主死亡会触发。胜利条件、天数耗尽等后续补充。 | 已知，延后 |
| 6 | **暂停菜单保存**：暂停时保存需要选槽位（3选1）。如果玩家想覆盖已有存档，需要二次确认。 | 需确认是否需要此功能 |
| 7 | **加载超时处理**：Loading 阶段如果 30 秒没完成，需要有降级方案（显示错误 + 返回主菜单按钮）。 | 已设计，实施时注意 |
| 8 | **ESC 在非 Playing 状态的行为**：在 MainMenu 按 ESC 是否退出游戏？在 CharacterCreation 按 ESC 是返回 MainMenu？需要统一的返回逻辑。 | 建议：CharacterCreation/SaveSlotSelect 按 ESC = 返回上一级；MainMenu 按 ESC = 无事发生或退出确认 |
| 9 | **P1-2 summary 填充 bug（P0 级，待修）**：`GameSaveSummary` 结构已定义、`GameSaveRoot.summary` 字段已加，但 `SaveManager.Save` **没有填充 summary 的逻辑**。存档槽 UI 调 `GetSaveMeta` 读到的全是空值（`rulerName=null, currentDay=0`），所有存档槽显示为空。 | 待修 |

**P1-2 修复方案**：`SaveManager.Save` 中遍历完 modules 后，从各 Manager 抓取关键字段填入 `root.summary`：

```csharp
root.summary = new GameSaveSummary
{
    rulerName     = RulerController.Instance.RulerName,
    currentDay    = TimeManager.Instance.CurrentDay,
    currentSeason = (int)TimeManager.Instance.CurrentSeason,
    difficulty    = WorldManager.Instance.Difficulty
};
```

修复后存档槽 UI 即可正常显示存档摘要（统治者名字、第几天、季节、难度）。

---

*下一步：确认本策划案后，按第 7 节实施顺序开始推进。*
