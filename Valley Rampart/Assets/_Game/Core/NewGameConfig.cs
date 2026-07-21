/// <summary>
/// 新建游戏配置。由 CharacterCreation 面板填充，通过 GameSceneEntrance 静态字段传给 GameScene。
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

    /// <summary>起始国家资源（覆盖 RulerData 资产默认值）。</summary>
    public int startGold = 100;
    public int startStone = 100;
    public int startWood = 100;
    public int startFood = 100;

    /// <summary>选中的存档槽 ID（用于新建游戏后的初始存档）。</summary>
    public string selectedSlotId = "slot_1";

    /// <summary>根据难度填充默认资源（Easy/Normal/Hard 三档预设）。</summary>
    public static NewGameConfig CreateWithDifficulty(int difficulty, string rulerName, string slotId)
    {
        var config = new NewGameConfig
        {
            rulerName = rulerName,
            difficulty = difficulty,
            selectedSlotId = slotId,
            mapSeed = UnityEngine.Random.Range(1, int.MaxValue)
        };

        switch (difficulty)
        {
            case 0: // Easy
                config.totalDays = 90;
                config.startGold = 200;
                config.startStone = 200;
                config.startWood = 200;
                config.startFood = 200;
                break;
            case 2: // Hard
                config.totalDays = 30;
                config.startGold = 50;
                config.startStone = 50;
                config.startWood = 50;
                config.startFood = 50;
                break;
            default: // Normal
                config.totalDays = 60;
                config.startGold = 100;
                config.startStone = 100;
                config.startWood = 100;
                config.startFood = 100;
                break;
        }

        return config;
    }
}
