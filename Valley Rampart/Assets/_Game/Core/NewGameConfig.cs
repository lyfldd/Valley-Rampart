/// <summary>
/// 新建游戏配置。由 CharacterCreation 面板填充，通过 GameSceneEntrance 静态字段传给 GameScene。
/// 资源/总天数等世界规则已抽到 WorldConfig，这里只保留玩家选择项。
/// </summary>
[System.Serializable]
public class NewGameConfig
{
    /// <summary>王国名（可选，默认"河谷王国"；2_13 取代君主名）。</summary>
    public string kingdomName = "河谷王国";

    /// <summary>地图生成种子（0 = 随机生成）。兼容旧字段，实际由 worldSeed 派生。</summary>
    public int mapSeed;

    /// <summary>难度：1=Easy, 2=Normal, 3=Hard。</summary>
    public int difficulty = 2;

    /// <summary>选中的存档槽 ID（用于新建游戏后的初始存档）。</summary>
    public string selectedSlotId = "slot_1";

    // ===== 新增（3.2 第 2.2 节）=====

    /// <summary>世界种子，0=随机生成（决定多地图布局）。</summary>
    public int worldSeed;

    /// <summary>地图大小：大/中/小。</summary>
    public WorldSize worldSize = WorldSize.Medium;
}
