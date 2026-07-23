using System;
using UnityEngine;

/// <summary>
/// 世界运行底层逻辑的统一配置（ScriptableObject）。
/// 所有世界规则集中在此，Inspector 可调，数据驱动。
/// 子管理器（TimeManager/DifficultyManager/WorldManager）从这里读自己的配置。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/WorldConfig", fileName = "WorldConfig")]
public class WorldConfig : ScriptableObject
{
    [Header("时间规则")]
    public TimeConfig time;

    [Header("难度规则")]
    public DifficultyConfig difficulty;

    [Header("季节规则")]
    public SeasonConfig season;

    [Header("存档规则")]
    public SaveConfig save;
}

// ===== 子配置 =====

/// <summary>时间规则配置。</summary>
[Serializable]
public struct TimeConfig
{
    [Tooltip("现实多少秒 = 游戏内一天")]
    public float secondsPerDay;       // 默认 480（8分钟/天）

    [Tooltip("游戏开始是第几天")]
    public int startDay;              // 默认 1

    [Tooltip("游戏开始当天的起始时刻 0-24")]
    public float startHour;           // 默认 6（早上6点）

    [Tooltip("一个季节持续多少天")]
    public int daysPerSeason;         // 默认 10

    [Tooltip("黎明过渡时长（小时）")]
    public float dawnDuration;        // 默认 1

    [Tooltip("黄昏过渡时长（小时）")]
    public float duskDuration;        // 默认 1
}

/// <summary>难度规则配置。</summary>
[Serializable]
public struct DifficultyConfig
{
    [Tooltip("三档预设（索引 0/1/2 对应 Easy/Normal/Hard）")]
    public DifficultyPreset[] presets;  // 3 个

    [Tooltip("基础资源（最低难度满额，实际 = 基础 ÷ 难度系数）")]
    public int baseGold;              // 默认 200
    public int baseStone;
    public int baseWood;
    public int baseFood;

    [Tooltip("每季难度系数增加多少")]
    public float factorGrowthPerSeason;  // 默认 0.25

    /// <summary>按档位值获取预设（档位 1/2/3 → 索引 0/1/2）。</summary>
    public DifficultyPreset GetPreset(int difficultyValue)
    {
        if (presets == null || presets.Length == 0)
            return new DifficultyPreset { name = "Default", difficultyValue = 2, secondsPerDay = 480f };
        int index = Mathf.Clamp(difficultyValue - 1, 0, presets.Length - 1);
        return presets[index];
    }

    /// <summary>按档位值算初始资源（基础 ÷ 系数）。</summary>
    public ResourcePack GetInitialResources(int difficultyValue)
    {
        int divisor = Mathf.Max(1, difficultyValue);
        return new ResourcePack
        {
            gold = Mathf.FloorToInt(baseGold / (float)divisor),
            stone = Mathf.FloorToInt(baseStone / (float)divisor),
            wood = Mathf.FloorToInt(baseWood / (float)divisor),
            food = Mathf.FloorToInt(baseFood / (float)divisor)
        };
    }
}

/// <summary>单档难度预设。</summary>
[Serializable]
public struct DifficultyPreset
{
    public string name;              // "Easy" / "Normal" / "Hard"
    public int difficultyValue;      // 1 / 2 / 3（= DifficultyFactor 初始值）
    [Tooltip("现实秒/天。Easy 节奏慢，Hard 节奏紧")]
    public float secondsPerDay;      // Easy 600 / Normal 480 / Hard 360
}

/// <summary>季节规则配置（四季日出日落）。</summary>
[Serializable]
public struct SeasonConfig
{
    [Tooltip("四季的日出日落时刻")]
    public SeasonSunData[] seasons;  // 4 个：春夏秋冬

    public SeasonSunData GetSeason(Season season)
    {
        if (seasons == null || seasons.Length == 0)
            return new SeasonSunData { sunriseHour = 6f, sunsetHour = 18f };
        int index = Mathf.Clamp((int)season, 0, seasons.Length - 1);
        return seasons[index];
    }
}

/// <summary>单个季节的日出日落。</summary>
[Serializable]
public struct SeasonSunData
{
    public Season season;
    public float sunriseHour;        // 春秋6, 夏5, 冬7
    public float sunsetHour;         // 春秋18, 夏20, 冬17
}

/// <summary>存档规则配置。</summary>
[Serializable]
public struct SaveConfig
{
    [Tooltip("自动存档间隔（游戏内天数）")]
    public int autoSaveIntervalDays;  // 默认 3
}

/// <summary>资源包（四资源统一结构）。</summary>
[Serializable]
public struct ResourcePack
{
    public int gold;
    public int stone;
    public int wood;
    public int food;
}
