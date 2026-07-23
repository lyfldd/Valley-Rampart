using UnityEngine;

/// <summary>
/// 难度系统子管理器（单例）。
/// 职责：维护当前难度档位与难度系数，每过一季系数递增并发布 DifficultyChangedEvent。
/// 配置从 WorldSystem.Config.difficulty 读取，不自己持有 SO。
/// 初始资源公式：基础资源 ÷ 难度档位（Easy 满额，Hard 最少）。
/// </summary>
public class DifficultyManager : Singleton<DifficultyManager>, ISaveable
{
    public string SaveId => "DifficultyManager";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    // 不再自己持有 config，从 WorldSystem 读
    private DifficultyConfig Config
    {
        get
        {
            if (WorldSystem.Instance == null || WorldSystem.Instance.Config == null)
            {
                return default;
            }
            return WorldSystem.Instance.Config.difficulty;
        }
    }

    /// <summary>当前难度档位（1/2/3 = Easy/Normal/Hard）。</summary>
    public int CurrentDifficulty { get; private set; }

    /// <summary>当前难度系数（初始 = 档位值，每季 + factorGrowthPerSeason）。</summary>
    public float CurrentFactor { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        SaveManager.Instance.RegisterSaveable(this);
    }

    private void Start()
    {
        EventBus.Subscribe<SeasonChangedEvent>(OnSeasonChanged);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<SeasonChangedEvent>(OnSeasonChanged);
    }

    // ===== 状态重置（由 TeardownManager 返回主菜单时调用）=====

    /// <summary>重置到默认值。InitializeWorld 也会覆盖，但重置兜底防残留。</summary>
    public void ResetState()
    {
        CurrentDifficulty = 1;
        CurrentFactor = 1f;
        Debug.Log("[DifficultyManager] ResetState: 档位=1, 系数=1");
    }

    /// <summary>新建游戏初始化。</summary>
    public void Initialize(int difficulty)
    {
        CurrentDifficulty = Mathf.Clamp(difficulty, 1, 3);
        CurrentFactor = CurrentDifficulty;

        // 按档位同步 TimeManager 的 secondsPerDay
        DifficultyPreset preset = Config.GetPreset(CurrentDifficulty);
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetSecondsPerDay(preset.secondsPerDay);
        }

        Debug.Log($"[DifficultyManager] 初始化: 档位={CurrentDifficulty}, 系数={CurrentFactor}, 秒/天={preset.secondsPerDay}");
    }

    /// <summary>读档后同步配置（由 WorldSystem.LoadState 调用）。</summary>
    public void SyncConfigFromWorld()
    {
        if (CurrentDifficulty <= 0) return;  // 未初始化则跳过
        DifficultyPreset preset = Config.GetPreset(CurrentDifficulty);
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.SetSecondsPerDay(preset.secondsPerDay);
        }
    }

    /// <summary>按当前难度获取初始国家资源包。</summary>
    public ResourcePack GetInitialResources()
    {
        return Config.GetInitialResources(CurrentDifficulty);
    }

    private void OnSeasonChanged(SeasonChangedEvent evt)
    {
        float oldFactor = CurrentFactor;
        CurrentFactor += Config.factorGrowthPerSeason;
        EventBus.Publish(new DifficultyChangedEvent(oldFactor, CurrentFactor));
        Debug.Log($"[DifficultyManager] 季节切换，难度系数 {oldFactor} → {CurrentFactor}");
    }

    // ===== ISaveable 实现 =====

    public SavePayload SaveState()
    {
        var data = new DifficultySaveData
        {
            currentDifficulty = CurrentDifficulty,
            currentFactor = CurrentFactor
        };
        return new SavePayload
        {
            typeName = typeof(DifficultySaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(DifficultySaveData).AssemblyQualifiedName) return;

        var data = JsonUtility.FromJson<DifficultySaveData>(payload.json);
        CurrentDifficulty = Mathf.Clamp(data.currentDifficulty, 1, 3);
        CurrentFactor = data.currentFactor > 0 ? data.currentFactor : CurrentDifficulty;

        Debug.Log($"[DifficultyManager] 从存档恢复: 档位={CurrentDifficulty}, 系数={CurrentFactor}");
    }
}
