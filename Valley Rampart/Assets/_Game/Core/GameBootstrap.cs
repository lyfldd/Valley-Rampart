using UnityEngine;

// 游戏场景引导器（GameBootstrap）
// 职责：GameScene 的业务初始化入口（MainMenuScene 由 MainMenuController 负责，不挂 Bootstrap 类）。
// 执行顺序：DefaultExecutionOrder(-100) 确保在业务系统之前执行。
//
// 初始化流程（两阶段模式，引导书第 2 节）：
//   阶段1：LoadManager 加载静态配置（UnitData/RulerData 等 ScriptableObject）
//   阶段2：根据 GameSceneEntrance 传入的模式分叉——
//     - 新建游戏 → LoadManager.InitializeNewGame（世界初始化 + 君主生成）
//     - 继续游戏 → LoadManager.LoadSave（存档恢复 + 君主绑定）
//
// 设计要点（引导书第 3 节）：
//   - 阶段化加载由 LoadManager 统一接管，GameBootstrap 只做"触发阶段1"和"阶段1完成后的模式分叉"。
//   - 静态配置加载、新建/存档恢复均由 LoadManager 总控，GameBootstrap 不直接操作资源。
//   - 状态推进：Booting → Loading → Ready → Playing，由 GameStateManager 集中管理。
[DefaultExecutionOrder(-100)]
public class GameBootstrap : MonoBehaviour
{
    // 防止 OnConfigsLoaded 被重复触发（例如配置热重载场景下）
    private bool _sceneInitialized;

    private void Awake()
    {
        Debug.Log("[GameBootstrap] 游戏引导器启动");

        // 场景切换时清理残留数据（引导书 R6：场景加载后第一时间清空运行时注册表）
        UnitRegistry.Instance.Clear();
        SaveManager.Instance.CleanupDestroyedSaveables();

        // 注册场景级生成器到 SaveManager，使 UnitFactory 产出的单位能被存档系统追踪
        SaveManager.Instance.RegisterSpawner(UnitFactory.Instance);

        // 订阅配置加载完成事件（替代已废弃的 UnitDataLoadedEvent，见引导书 3.2 节）
        EventBus.Subscribe<ConfigsLoadedEvent>(OnConfigsLoaded);
    }

    private void Start()
    {
        // 阶段1：将游戏状态推进到 Loading，然后由 LoadManager 加载所有静态配置
        // 静态配置包括：UnitData（单位数据）、RulerData（君主数据）、DifficultyConfig（难度配置）等
        GameStateManager.Instance.SetState(GameState.Loading);
        LoadManager.Instance.LoadStaticConfigs();
    }

    private void OnDestroy()
    {
        // 反订阅，防止事件泄漏
        EventBus.Unsubscribe<ConfigsLoadedEvent>(OnConfigsLoaded);
    }

    // 阶段1 完成回调 → 进入阶段2（模式分叉）
    // 由 LoadManager 在静态配置全部加载后发布 ConfigsLoadedEvent 触发
    private void OnConfigsLoaded(ConfigsLoadedEvent evt)
    {
        if (!evt.IsSuccess)
        {
            Debug.LogError("[GameBootstrap] 静态配置加载失败，游戏无法继续启动。");
            return;
        }

        // 防御：如果场景已初始化过，跳过重复执行
        if (_sceneInitialized)
        {
            Debug.Log("[GameBootstrap] 场景已初始化，跳过重复调用。");
            return;
        }
        _sceneInitialized = true;

        // 配置加载成功，状态推进到 Ready（等待业务初始化完成后再推进到 Playing）
        GameStateManager.Instance.SetState(GameState.Ready);

        // 根据 GameSceneEntrance 传入的模式执行不同的初始化路径
        // GameSceneEntrance 是场景间传递启动参数的桥梁（引导书第 2 节）
        if (GameSceneEntrance.CurrentMode == GameSceneEntrance.Mode.ContinueGame)
        {
            ContinueFromSave();
        }
        else
        {
            StartNewGame();
        }

        // 打印时间系统状态日志，便于调试确认时间系统已正确初始化
        var timeManager = TimeManager.Instance;
        if (timeManager != null)
        {
            Debug.Log($"[GameBootstrap] 时间系统状态: 第{timeManager.CurrentDay}天 "
                + $"{timeManager.CurrentTimeOfDay:0.0}时 {timeManager.CurrentSeason}/{timeManager.CurrentPhase}");
        }

        // 启用玩家输入（之前 InputManager 默认禁用，等待 GameBootstrap 在此激活）
        InputManager.Instance.EnableInput();

        Debug.Log("[GameBootstrap] 游戏开始运行");

        // 清理场景入口参数，防止场景切换后残留旧数据
        GameSceneEntrance.Clear();
    }

    // 新建游戏路径（阶段2a）
    // 由 LoadManager 统一协调：世界初始化 + 君主生成 + 状态推进到 Playing
    private void StartNewGame()
    {
        var config = GameSceneEntrance.NewGameConfig;

        if (config != null)
        {
            // 阶段2a：LoadManager.InitializeNewGame 内部完成——
            //   1. DifficultyManager 按难度初始化
            //   2. WorldSystem 生成地图
            //   3. RulerController.SpawnMonarch 创建君主
            //   4. GameStateManager.SetState(Playing)
            LoadManager.Instance.InitializeNewGame(config);
            Debug.Log($"[GameBootstrap] 应用新建游戏配置: "
                + $"ruler={config.rulerName}, seed={config.mapSeed}, difficulty={config.difficulty}");

            // 新建游戏自动创建初始存档，确保玩家随时可以读档回到初始状态
            string slotId = config.selectedSlotId;
            if (!string.IsNullOrEmpty(slotId) && !SaveManager.Instance.HasSave(slotId))
            {
                SaveManager.Instance.Save(slotId);
            }
        }
    }

    // 继续游戏路径（阶段2b）
    // 由 LoadManager 统一协调：存档恢复 + 君主绑定 + 状态推进到 Playing
    private void ContinueFromSave()
    {
        string slotId = GameSceneEntrance.LoadSlotId;
        Debug.Log($"[GameBootstrap] 读档: {slotId}");

        // 读档前先销毁场景中所有手动放置的单位，避免与存档恢复的单位重复
        // 这是 R2 规则：场景预置单位仅用于编辑期调试，读档时必须清理
        RulerController.Instance.DestroyAllSceneUnits();

        // 阶段2b：LoadManager.LoadSave 内部完成——
        //   1. SaveManager.Load 恢复所有 ISaveable 状态
        //   2. UnitFactory.SpawnFromSave 重建存档中的单位
        //   3. GameStateManager.SetState(Playing)
        bool success = LoadManager.Instance.LoadSave(slotId);
        if (!success)
        {
            Debug.LogError($"[GameBootstrap] 读档失败: {slotId}，游戏无法继续。");
            GameStateManager.Instance.SetState(GameState.GameOver);
            return;
        }

        // 读档完成后，在场景中查找已恢复的君主单位并绑定到 RulerController
        // 新建模式不需要此步骤（SpawnMonarch 中已完成绑定）
        RulerController.Instance.BindExistingMonarch();
    }
}