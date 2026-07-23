using UnityEngine;

/// <summary>
/// 场景间参数传递桥接类。MainMenu → GameScene 时填充，GameScene 的 GameBootstrap 读取后清空。
/// 静态字段在 Unity 进程重启后自动重置，跨场景保持。
/// </summary>
public static class GameSceneEntrance
{
    public enum Mode { NewGame, ContinueGame }

    public static Mode CurrentMode = Mode.NewGame;
    public static NewGameConfig NewGameConfig;
    public static string LoadSlotId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        CurrentMode = Mode.NewGame;
        NewGameConfig = null;
        LoadSlotId = null;
    }

    /// <summary>新建游戏路径：由 MainMenu 调用，传入玩家配置。</summary>
    public static void SetNewGame(NewGameConfig config)
    {
        CurrentMode = Mode.NewGame;
        NewGameConfig = config;
        LoadSlotId = config != null ? config.selectedSlotId : null;
    }

    /// <summary>继续游戏路径：由存档槽 UI 调用，传入槽位 ID。</summary>
    public static void SetContinue(string slotId)
    {
        CurrentMode = Mode.ContinueGame;
        NewGameConfig = null;
        LoadSlotId = slotId;
    }

    /// <summary>清空静态字段。GameScene 读取后必须调，防止下次切场景读到脏数据。</summary>
    public static void Clear()
    {
        CurrentMode = Mode.NewGame;
        NewGameConfig = null;
        LoadSlotId = null;
    }
}
