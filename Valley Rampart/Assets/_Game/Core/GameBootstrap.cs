using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏场景引导器。GameScene 挂这个，MainMenuScene 挂 MainMenuBootstrap。
/// 用 DefaultExecutionOrder(-100) 保证在业务系统之后、CoreBootstrap 之前执行。
/// 状态驱动：Booting → Loading → Ready → Playing
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameBootstrap : MonoBehaviour
{
    private bool _sceneInitialized;

    private void Awake()
    {
        Debug.Log("[GameBootstrap] 游戏场景启动");

        // R6: 跨场景清理——清除上一场景残留的已销毁引用
        // （玩家从 GameScene 退回主菜单再进入时，旧场景单位已被 Unity 销毁，
        //   但 _saveables / _aliveUnits 仍持有 C# 引用，会导致内存泄漏与幽灵引用）
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
    }

    private void OnDataLoaded(UnitDataLoadedEvent evt)
    {
        if (!evt.IsSuccess)
        {
            Debug.LogError("[GameBootstrap] 数据加载失败，游戏无法继续。");
            return;
        }

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

        // 2. 读取 GameSceneEntrance，按模式分叉
        if (GameSceneEntrance.CurrentMode == GameSceneEntrance.Mode.ContinueGame)
        {
            ContinueFromSave();
        }
        else
        {
            StartNewGame();
        }

        // 3. 时间系统就绪
        var timeManager = TimeManager.Instance;
        Debug.Log($"[GameBootstrap] 时间系统就绪: 第{timeManager.CurrentDay}天 "
            + $"{timeManager.CurrentTimeOfDay:0.0}点 {timeManager.CurrentSeason}/{timeManager.CurrentPhase}");

        // 4. 启用输入
        InputManager.Instance.EnableInput();

        // 5. 进入游戏
        GameStateManager.Instance.SetState(GameState.Playing);
        Debug.Log("[GameBootstrap] 游戏开始！");

        // 6. 清理跨场景参数
        GameSceneEntrance.Clear();
    }

    private void StartNewGame()
    {
        var config = GameSceneEntrance.NewGameConfig;

        // 1. 应用世界配置（地图种子 + 难度 + 总天数）
        if (config != null)
        {
            WorldManager.Instance.ApplyConfig(config.mapSeed, config.difficulty, config.totalDays);
            RulerController.Instance.SetRulerName(config.rulerName);
            RulerController.Instance.ApplyStartResources(
                config.startGold, config.startStone, config.startWood, config.startFood);
            TimeManager.Instance.ApplyConfig(config.totalDays);

            Debug.Log($"[GameBootstrap] 已应用新建游戏配置: "
                + $"ruler={config.rulerName}, seed={config.mapSeed}, "
                + $"difficulty={config.difficulty}, totalDays={config.totalDays}");
        }

        // 2. 创建君主
        RulerController.Instance.SpawnMonarch();

        // 3. 自动存初始档
        string slotId = config?.selectedSlotId ?? "slot_1";
        if (!SaveManager.Instance.HasSave(slotId))
        {
            SaveManager.Instance.Save(slotId);
        }
    }

    private void ContinueFromSave()
    {
        string slotId = GameSceneEntrance.LoadSlotId;
        Debug.Log($"[GameBootstrap] 读档: {slotId}");

        // ① 读档前清理场景中所有预置单位（防止读档后双份）
        //    场景里手动放的 ruler/单位会被销毁，由 SpawnFromSave 重建
        RulerController.Instance.DestroyAllSceneUnits();

        // ② 从存档恢复唯一的君主（内部三阶段：Global LoadState → SpawnFromSave → Scene LoadState）
        SaveManager.Instance.Load(slotId);

        // ③ 读档后绑定到恢复的君主单位
        RulerController.Instance.BindExistingMonarch();
    }
}
