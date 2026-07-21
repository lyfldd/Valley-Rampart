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

    // ===== 存档分叉标志 =====
    /// <summary>是否正在读档（场景切换间传参）。</summary>
    public static bool IsLoadingSave = false;
    /// <summary>要加载的存档槽 ID。</summary>
    public static string SaveSlotToLoad;

    /// <summary>新建游戏配置（由 MainMenu 的 CharacterCreation 面板填充后传入）。</summary>
    public static NewGameConfig NewGameConfig;

    private void Awake()
    {
        Debug.Log("[GameBootstrap] 游戏启动");

        // P2-1: 不在此处设 Booting——GameStateManager 首次创建时已自动设为 Booting。
        // 跨场景切换时 GameStateManager 保持之前场景的状态，不应被覆盖。
        // 状态推进在 Start / OnDataLoaded 中完成。

        // R6: 跨场景清理——清除上一场景残留的已销毁引用，防止幽灵引用干扰新场景
        UnitRegistry.Instance.Clear();
        SaveManager.Instance.CleanupDestroyedSaveables();

        // 注册场景对象重建器
        SaveManager.Instance.RegisterSpawner(UnitFactory.Instance);

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

        // P2-5: 退出场景时清理静态字段，防止下次新建游戏时读到旧数据
        IsLoadingSave = false;
        SaveSlotToLoad = null;
        NewGameConfig = null;
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

        bool isNewGame = !IsLoadingSave;

        if (IsLoadingSave)
        {
            // 读档模式：先清理场景中所有手动放置的单位（R2: 不仅限于君主，避免 NPC/敌人重复）
            RulerController.Instance.DestroyAllSceneUnits();

            // SaveManager 通过 UnitFactory.SpawnFromSave 恢复所有单位
            SaveManager.Instance.Load(SaveSlotToLoad);
            IsLoadingSave = false;
            SaveSlotToLoad = null;

            // 读档后 RulerController 需要重新绑定到刚恢复的君主
            // （新建模式在 SpawnMonarch 中完成绑定，读档模式在 Load 后补做）
            RulerController.Instance.BindExistingMonarch();
        }
        else
        {
            // 新建模式：先应用配置，再创建君主
            if (NewGameConfig != null)
            {
                WorldManager.Instance.ApplyConfig(
                    NewGameConfig.mapSeed,
                    NewGameConfig.difficulty,
                    NewGameConfig.totalDays);
                RulerController.Instance.SetRulerName(NewGameConfig.rulerName);
                TimeManager.Instance.SetTotalDays(NewGameConfig.totalDays);

                Debug.Log($"[GameBootstrap] 已应用新建游戏配置: "
                    + $"ruler={NewGameConfig.rulerName}, seed={NewGameConfig.mapSeed}, "
                    + $"difficulty={NewGameConfig.difficulty}, totalDays={NewGameConfig.totalDays}");
            }
            // 2. 创建君主（RulerController 自带位置配置）
            RulerController.Instance.SpawnMonarch();
        }

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

        // 6. 新建游戏后自动存档
        if (isNewGame)
        {
            string slotId = NewGameConfig?.selectedSlotId ?? "slot_1";
            if (!SaveManager.Instance.HasSave(slotId))
            {
                SaveManager.Instance.Save(slotId);
            }
            NewGameConfig = null;
        }
    }
}
