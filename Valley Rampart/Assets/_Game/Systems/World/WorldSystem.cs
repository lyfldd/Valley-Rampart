using UnityEngine;

/// <summary>
/// 世界运行底层逻辑管理器（门面）。
/// 职责：持有 WorldConfig + 协调子管理器初始化 + 分发配置。
/// 不接管子管理器的运行时逻辑（TimeManager/DifficultyManager 各自管自己）。
/// </summary>
[DefaultExecutionOrder(-95)]  // 早于 SaveManager(-90) 和各子管理器(默认0) Awake，确保 config 先就绪
public class WorldSystem : Singleton<WorldSystem>, ISaveable
{
    public string SaveId => "WorldSystem";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    [Header("世界配置")]
    [Tooltip("Inspector 拖入，留空则从 Resources/World/ 加载")]
    [SerializeField] private WorldConfig config;

    // ===== 子管理器引用（场景里挂的 Singleton，启动后自动获取）=====

    public TimeManager Time => TimeManager.Instance;
    public DifficultyManager Difficulty => DifficultyManager.Instance;
    public WorldManager World => WorldManager.Instance;

    /// <summary>世界配置（供子管理器读取）。</summary>
    public WorldConfig Config => config;

    protected override void Awake()
    {
        base.Awake();

        // 加载配置
        if (config == null)
        {
            config = Resources.Load<WorldConfig>("World/WorldConfig");
        }
        if (config == null)
        {
            Debug.LogError("[WorldSystem] 未找到 WorldConfig！请创建资产放在 Resources/World/ 下");
        }

        SaveManager.Instance.RegisterSaveable(this);
    }

    // ===== 新建游戏：统一初始化入口 =====

    /// <summary>
    /// 新建游戏时由 GameBootstrap 调用。
    /// 统一协调：世界配置 → 难度初始化 → 时间配置 → 君主资源。
    /// </summary>
    public void InitializeWorld(NewGameConfig newConfig)
    {
        if (config == null)
        {
            Debug.LogError("[WorldSystem] config 为空，无法初始化世界！");
            return;
        }

        // 1. 世界管理器（地图种子 + 难度档位）
        WorldManager.Instance.ApplyConfig(newConfig.mapSeed, newConfig.difficulty);

        // 2. 难度系统初始化（设初始系数 + 同步 TimeManager 的 secondsPerDay）
        DifficultyManager.Instance.Initialize(newConfig.difficulty);

        // 3. 时间系统从 WorldConfig 读规则（秒/天已在 DifficultyManager.Initialize 里按档位设了，
        //    这里补设 daysPerSeason）
        ApplyTimeConfig();

        // 4. 君主名字 + 按难度应用初始资源
        RulerController.Instance.SetRulerName(newConfig.rulerName);
        RulerController.Instance.ApplyInitialResourcesFromDifficulty();

        Debug.Log($"[WorldSystem] 世界初始化完成: 难度={newConfig.difficulty}, 种子={newConfig.mapSeed}");
    }

    /// <summary>把 TimeConfig 应用到 TimeManager。</summary>
    private void ApplyTimeConfig()
    {
        if (config == null) return;
        var tc = config.time;
        TimeManager.Instance.SetDaysPerSeason(tc.daysPerSeason);
        // secondsPerDay 已由 DifficultyManager 按档位设（Easy 慢/Hard 快）
    }

    /// <summary>把 SeasonConfig 应用到 TimeManager（日出日落表）。</summary>
    public SeasonSunData GetSeasonSunData(Season season)
    {
        if (config == null || config.season.seasons == null || config.season.seasons.Length == 0)
        {
            // 兜底默认值（防 config 未就绪时崩溃）
            switch (season)
            {
                case Season.Summer: return new SeasonSunData { sunriseHour = 5f, sunsetHour = 20f };
                case Season.Winter: return new SeasonSunData { sunriseHour = 7f, sunsetHour = 17f };
                default:            return new SeasonSunData { sunriseHour = 6f, sunsetHour = 18f };
            }
        }
        return config.season.GetSeason(season);
    }

    /// <summary>获取自动存档间隔（游戏内天数）。</summary>
    public int GetAutoSaveIntervalDays()
    {
        if (config == null) return 3;
        return Mathf.Max(1, config.save.autoSaveIntervalDays);
    }

    // ===== ISaveable（WorldSystem 存档世界级状态，子管理器各自存自己的）=====

    public SavePayload SaveState()
    {
        var data = new WorldSystemSaveData
        {
            // 目前世界级状态都由子管理器各自存档，WorldSystem 暂无额外状态
            // 未来如果有跨子管理器的状态，存这里
        };
        return new SavePayload
        {
            typeName = typeof(WorldSystemSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(WorldSystemSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<WorldSystemSaveData>(payload.json);

        // 读档时也要应用配置（子管理器的 LoadState 会恢复各自状态，
        // 但 TimeManager 的 daysPerSeason 等配置项需要从 WorldConfig 重新设）
        ApplyTimeConfig();
        DifficultyManager.Instance.SyncConfigFromWorld();

        Debug.Log("[WorldSystem] 读档恢复完成");
    }
}
