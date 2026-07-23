/// <summary>
/// 新建游戏配置。由 CharacterCreation 面板填充，通过 GameSceneEntrance 静态字段传给 GameScene。
/// 资源/总天数等世界规则已抽到 WorldConfig，这里只保留玩家选择项。
/// </summary>
[System.Serializable]
public class NewGameConfig
{
    /// <summary>玩家输入的统治者名字。</summary>
    public string rulerName = "无名君主";

    /// <summary>地图生成种子（0 = 随机生成）。</summary>
    public int mapSeed;

    /// <summary>难度：1=Easy, 2=Normal, 3=Hard。</summary>
    public int difficulty = 2;

    /// <summary>选中的存档槽 ID（用于新建游戏后的初始存档）。</summary>
    public string selectedSlotId = "slot_1";
}
