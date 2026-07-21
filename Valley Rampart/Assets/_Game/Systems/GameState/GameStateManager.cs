using UnityEngine;

/// <summary>
/// 游戏全局状态。控制启动流程和系统行为。
/// </summary>
public enum GameState
{
    Booting,     // 启动中，初始化管理器
    Loading,     // 加载数据中
    Ready,       // 数据就绪，准备初始化场景
    Playing,     // 游戏运行中
    Paused,      // 暂停
    GameOver     // 游戏结束
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
