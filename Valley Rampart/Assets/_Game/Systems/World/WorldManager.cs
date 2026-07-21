using UnityEngine;

/// <summary>
/// 世界管理器。管理程序化地图种子、难度、总天数等"游戏会话级"配置。
/// 地图是程序化生成的（基于群落模板），读档后必须用同一个 seed 复现地形。
/// </summary>
public class WorldManager : Singleton<WorldManager>, ISaveable
{
    public string SaveId => "WorldManager";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    public int MapSeed { get; private set; }
    public int Difficulty { get; private set; }   // 0=Easy, 1=Normal, 2=Hard
    public int TotalDays { get; private set; }     // 胜利条件之一

    protected override void Awake()
    {
        base.Awake();
        SaveManager.Instance.RegisterSaveable(this);
    }

    /// <summary>新建游戏时由启动流程调用。</summary>
    public void ApplyConfig(int mapSeed, int difficulty, int totalDays)
    {
        MapSeed = mapSeed;
        Difficulty = difficulty;
        TotalDays = totalDays;
        // TODO: 触发地图程序化生成（用 MapSeed 初始化生成器）
        // GenerateWorld(MapSeed);
        Debug.Log($"[WorldManager] 应用配置: seed={mapSeed}, difficulty={difficulty}, totalDays={totalDays}");
    }

    public SavePayload SaveState()
    {
        var data = new WorldSaveData
        {
            mapSeed = MapSeed,
            difficulty = Difficulty,
            totalDays = TotalDays
        };
        return new SavePayload
        {
            typeName = typeof(WorldSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(WorldSaveData).AssemblyQualifiedName) return;

        var data = JsonUtility.FromJson<WorldSaveData>(payload.json);
        MapSeed = data.mapSeed;
        Difficulty = data.difficulty;
        TotalDays = data.totalDays;

        // TODO: 读档时用存档的 seed 重新生成地图（要求生成过程是确定性的：同 seed 同结果）
        // GenerateWorld(MapSeed);
        Debug.Log($"[WorldManager] 从存档恢复: seed={MapSeed}, difficulty={Difficulty}, totalDays={TotalDays}");
    }
}

[System.Serializable]
public class WorldSaveData
{
    public int mapSeed;             // 地图生成种子（读档用同 seed 复现地形）
    public int difficulty;          // 难度（0=Easy, 1=Normal, 2=Hard）
    public int totalDays;           // 总目标天数（胜利条件之一）
}
