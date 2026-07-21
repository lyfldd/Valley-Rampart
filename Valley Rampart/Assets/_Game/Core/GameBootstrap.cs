using UnityEngine;

/// <summary>
/// 游戏启动引导器。场景中必须挂载此脚本。
/// 用 DefaultExecutionOrder(-100) 保证最先执行。
/// 状态驱动：Booting → Loading → Ready → Playing
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameBootstrap : MonoBehaviour
{
    private bool _sceneInitialized;

    private void Awake()
    {
        Debug.Log("[GameBootstrap] 游戏启动");

        // 触发所有单例创建（按依赖顺序）
        GameStateManager.Instance.SetState(GameState.Booting);

        // 订阅数据加载完成事件
        EventBus.Subscribe<UnitDataLoadedEvent>(OnDataLoaded);
    }

    private void Start()
    {
        // 启动数据加载
        GameStateManager.Instance.SetState(GameState.Loading);

        // UnitDataManager.Awake 已经自动触发 LoadAll
        // 如果数据是同步加载的，此时已经就绪
        if (UnitDataManager.Instance.IsInitialized)
        {
            OnDataLoaded(new UnitDataLoadedEvent(true, UnitDataManager.Instance.Count));
        }
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<UnitDataLoadedEvent>(OnDataLoaded);
    }

    private void OnDataLoaded(UnitDataLoadedEvent evt)
    {
        if (!evt.IsSuccess)
        {
            Debug.LogError("[GameBootstrap] 数据加载失败，游戏无法继续。");
            return;
        }

        // 防止 UnitDataLoadedEvent 重复触发导致多次初始化
        if (_sceneInitialized)
        {
            Debug.Log("[GameBootstrap] 场景已初始化，跳过重复调用。");
            return;
        }
        _sceneInitialized = true;

        Debug.Log($"[GameBootstrap] 数据就绪（{evt.TotalCount} 个配置），开始初始化场景。");

        GameStateManager.Instance.SetState(GameState.Ready);
        InitializeScene();
    }

    private void InitializeScene()
    {
        // 1. 预加载单位 Prefab
        UnitFactory.Instance.PreloadAll();

        // 2. 创建君主（RulerController 自带位置配置）
        RulerController.Instance.SpawnMonarch();

        // 3. 时间系统就绪
        //    难度系统可在此前调用 TimeManager.SetSecondsPerDay / SetTotalDays / SetDaysPerSeason 配置
        var timeManager = TimeManager.Instance;
        Debug.Log($"[GameBootstrap] 时间系统就绪: 第{timeManager.CurrentDay}天 "
            + $"{timeManager.CurrentTimeOfDay:0.0}点 {timeManager.CurrentSeason}/{timeManager.CurrentPhase}");

        // 4. 启用输入
        InputManager.Instance.EnableInput();

        // 5. 进入游戏（TimeManager 仅在 Playing 后推进时间）
        GameStateManager.Instance.SetState(GameState.Playing);
        Debug.Log("[GameBootstrap] 游戏开始！");
    }
}
