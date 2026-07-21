/// <summary>
/// 新建游戏配置。由 CharacterCreation 面板填充，通过静态字段传给 GameScene。
/// 包含玩家输入的名字、难度选择、地图种子等。
/// </summary>
[System.Serializable]
public class NewGameConfig
{
    /// <summary>玩家输入的统治者名字。</summary>
    public string rulerName = "无名君主";

    /// <summary>地图生成种子（0 = 随机生成）。</summary>
    public int mapSeed;

    /// <summary>难度：0=Easy, 1=Normal, 2=Hard。</summary>
    public int difficulty;

    /// <summary>目标总天数（胜利条件之一，0=无上限）。</summary>
    public int totalDays;

    /// <summary>选中的存档槽 ID（用于新建游戏后的初始存档）。</summary>
    public string selectedSlotId = "slot_1";
}
