/// <summary>
/// 新建游戏配置。由 CharacterCreation 面板填充，通过 GameSceneEntrance 静态字段传给 GameScene。
/// 资源/总天数等世界规则已抽到 WorldConfig，这里只保留玩家选择项。
/// </summary>
[System.Serializable]
public class NewGameConfig
{
    /// <summary>王国名（可选，默认"河谷王国"；2_13 取代君主名）。</summary>
    public string kingdomName = "河谷王国";

    /// <summary>
    /// 玩家选族索引（2_13 M10 选族 UI / D431 双挂 UI 侧；0=人类,1=精灵,2=矮人,3=兽人，D421 亡灵退役）。
    /// UI 暂存字段：RaceDef SO 尚未建（2_20 Q10-M1 域，让渡登记）；2_16 kingdomSpawns 激活时定族时消费本值，
    /// 消费前玩家 KingdomDef 维持 Human 现状（2_16 实施批口径）。
    /// </summary>
    public int raceId = 0;

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
