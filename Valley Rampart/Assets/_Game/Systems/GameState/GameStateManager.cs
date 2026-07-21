using UnityEngine;

/// <summary>
/// 游戏全局状态。控制启动流程和系统行为。
/// </summary>
public enum GameState
{
    // ===== 菜单流程（MainMenuScene）=====
    Booting,            // 引擎初始化
    Splash,             // 过场动画
    MainMenu,           // 主菜单
    CharacterCreation,  // 角色创建
    SaveSlotSelect,     // 存档槽选择

    // ===== 游戏内流程（GameScene）=====
    Loading,            // 加载静态配置
    Ready,              // 配置就绪，等待初始化
    Playing,            // 游戏运行中
    Paused,             // 暂停
    GameOver            // 游戏结束
}

/// <summary>
/// 全局状态管理器。其他系统通过订阅 GameStateChangedEvent 或查询 CurrentState 响应状态变化。
/// </summary>
public class GameStateManager : Singleton<GameStateManager>
{
    public GameState CurrentState { get; private set; } = GameState.Booting;

    protected override void Awake()
    {
        base.Awake();
        CurrentState = GameState.Booting;
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;

        GameState oldState = CurrentState;
        CurrentState = newState;

        Debug.Log($"[GameStateManager] {oldState} → {newState}");
        EventBus.Publish(new GameStateChangedEvent(oldState, newState));
    }
}
