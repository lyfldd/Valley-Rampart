using UnityEngine;

/// <summary>
/// 游戏场景引导器。GameScene 挂这个；MainMenuScene 挂的是 MainMenuController（不是 Bootstrap 类）。
/// 用 DefaultExecutionOrder(-100) 保证在业务系统之后执行。
/// 状态驱动：Booting → Loading → Ready → Playing
///
/// 加载协调已移交 LoadManager：GameBootstrap 只负责"触发阶段1"和"阶段1完成后按模式分叉"。
/// 静态配置加载、新建/读档恢复的时序由 LoadManager 管控。
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameBootstrap : MonoBehaviour
{
    private bool _sceneInitialized;

    private void Awake()
    {
        Debug.Log("[GameBootstrap] 游戏场景启动");

        // R6: 跨场景清理——清除上一场景残留的已销毁引用
        UnitRegistry.Instance.Clear();
        SaveManager.Instance.CleanupDestroyedSaveables();

        // 注册场景对象重建器
        SaveManager.Instance.RegisterSpawner(UnitFactory.Instance);

        // 订阅静态配置加载完成事件（替代旧的 UnitDataLoadedEvent）
        EventBus.Subscribe<ConfigsLoadedEvent>(OnConfigsLoaded);
    }

    private void Start()
    {
        // 触发阶段1：LoadManager 加载静态配置
        GameStateManager.Instance.SetState(GameState.Loading);
        LoadManager.Instance.LoadStaticConfigs();
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<ConfigsLoadedEvent>(OnConfigsLoaded);
    }

    // 阶段1完成回调 → 进阶段2
    private void OnConfigsLoaded(ConfigsLoadedEvent evt)
    {
        if (!evt.IsSuccess)
        {
            Debug.LogError("[GameBootstrap] 静态配置加载失败，游戏无法继续。");
            return;
        }

        if (_sceneInitialized)
        {
            Debug.Log("[GameBootstrap] 场景已初始化，跳过重复调用。");
            return;
        }
        _sceneInitialized = true;

        GameStateManager.Instance.SetState(GameState.Ready);

        // 按 GameSceneEntrance 模式分叉
        if (GameSceneEntrance.CurrentMode == GameSceneEntrance.Mode.ContinueGame)
        {
            ContinueFromSave();
        }
        else
        {
            StartNewGame();
        }

        // 时间系统就绪日志
        var timeManager = TimeManager.Instance;
        if (timeManager != null)
        {
            Debug.Log($"[GameBootstrap] 时间系统就绪: 第{timeManager.CurrentDay}天 "
                + $"{timeManager.CurrentTimeOfDay:0.0}点 {timeManager.CurrentSeason}/{timeManager.CurrentPhase}");
        }

        // 启用输入
        InputManager.Instance.EnableInput();

        Debug.Log("[GameBootstrap] 游戏开始！");

        // 清理跨场景参数
        GameSceneEntrance.Clear();
    }

    private void StartNewGame()
    {
        var config = GameSceneEntrance.NewGameConfig;

        if (config != null)
        {
            // 阶段2a：LoadManager 统一协调世界初始化 + 君主生成 + 进入 Playing
            LoadManager.Instance.InitializeNewGame(config);
            Debug.Log($"[GameBootstrap] 已应用新建游戏配置: "
                + $"ruler={config.rulerName}, seed={config.mapSeed}, difficulty={config.difficulty}");

            // 新建后自动存初始档
            string slotId = config.selectedSlotId;
            if (!string.IsNullOrEmpty(slotId) && !SaveManager.Instance.HasSave(slotId))
            {
                SaveManager.Instance.Save(slotId);
            }
        }
    }

    private void ContinueFromSave()
    {
        string slotId = GameSceneEntrance.LoadSlotId;
        Debug.Log($"[GameBootstrap] 读档: {slotId}");

        // ① 读档前清理场景中所有预置单位（防止读档后双份）
        RulerController.Instance.DestroyAllSceneUnits();

        // ② 阶段2b：LoadManager 读档恢复（SaveManager.Load + 进入 Playing）
        LoadManager.Instance.LoadSave(slotId);

        // ③ 读档后绑定到恢复的君主单位
        RulerController.Instance.BindExistingMonarch();
    }
}
