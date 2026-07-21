using UnityEngine;

/// <summary>
/// 游戏时间管理器（单例）。
///
/// 核心规则：
///   - 现实 secondsPerDay 秒 = 游戏内一天（24小时）。默认 480 秒 = 8 分钟/天。
///   - CurrentDay 随时间推进递增；季节由天数决定，每 daysPerSeason 天换一季，春夏秋冬循环。
///   - 时段 Night→Dawn→Day→Dusk→Night 由「当天时刻 + 季节昼夜比例」动态计算。
///   - 季节影响日出/日落：夏白天最长(15h)，冬最短(10h)。
///
/// 发布的事件（仅这三种，小时变化不发事件）：
///   - TimePhaseChangedEvent 时段切换时发布
///   - TimeDayChangedEvent   新一天发布
///   - SeasonChangedEvent    季节切换时发布
///
/// 仅在 GameState.Playing 时推进。
/// 难度系统可通过 SetSecondsPerDay / SetTotalDays / SetDaysPerSeason 在游戏开始前配置。
/// 场景中需挂载此脚本（建议挂在空物体 "TimeManager" 上）。
/// </summary>
public class TimeManager : Singleton<TimeManager>
{
    [Header("时间流速")]
    [Tooltip("现实多少秒 = 游戏内一天（24小时）。默认 480 = 8分钟。")]
    [SerializeField] private float secondsPerDay = 480f;

    [Header("起始时间")]
    [Tooltip("游戏开始时是第几天（从1开始）。")]
    [SerializeField] private int startDay = 1;

    [Tooltip("游戏开始当天的起始时刻（0-24）。默认 6 = 早上6点。")]
    [SerializeField] private float startHour = 6f;

    [Header("天数与难度")]
    [Tooltip("游戏总目标天数（可由难度系统设置，作为胜利条件之一）。0 = 无上限。")]
    [SerializeField] private int totalDays = 0;

    [Header("季节")]
    [Tooltip("一个季节持续多少天。默认 10 天。")]
    [SerializeField] private int daysPerSeason = 10;

    // ===== 运行时状态 =====

    private float _dayTimer;  // 当天累计现实秒数

    /// <summary>当前是第几天（从1开始）。</summary>
    public int CurrentDay { get; private set; }

    /// <summary>当前小时 0-23（可直接读取用于显示，但不发变化事件）。</summary>
    public int CurrentHour { get; private set; }

    /// <summary>当天时刻 0~24（含小数，精确到帧）。可直接读取用于显示。</summary>
    public float CurrentTimeOfDay { get; private set; }

    /// <summary>当前时段。</summary>
    public TimePhase CurrentPhase { get; private set; }

    /// <summary>当前季节。</summary>
    public Season CurrentSeason { get; private set; }

    /// <summary>当天进度 0~1。</summary>
    public float DayProgress => _dayTimer / secondsPerDay;

    public float SecondsPerDay => secondsPerDay;
    public int TotalDays => totalDays;
    public int DaysPerSeason => daysPerSeason;

    /// <summary>当前季节的日出时刻。</summary>
    public float SunriseHour => GetSunrise(CurrentSeason);

    /// <summary>当前季节的日落时刻。</summary>
    public float SunsetHour => GetSunset(CurrentSeason);

    private const float DawnDuration = 1f;  // 黎明过渡时长（小时）
    private const float DuskDuration = 1f;  // 黄昏过渡时长（小时）

    protected override void Awake()
    {
        base.Awake();

        CurrentDay = Mathf.Max(1, startDay);
        CurrentSeason = CalculateSeason(CurrentDay);
        CurrentTimeOfDay = Mathf.Clamp(startHour, 0f, 24f);
        CurrentHour = Mathf.Clamp(Mathf.FloorToInt(CurrentTimeOfDay), 0, 23);
        _dayTimer = (CurrentTimeOfDay / 24f) * secondsPerDay;
        CurrentPhase = CalculatePhase(CurrentTimeOfDay, CurrentSeason);

        Debug.Log($"[TimeManager] 初始化: 第{CurrentDay}天 {CurrentTimeOfDay:0.0}点 "
            + $"季节={CurrentSeason} 时段={CurrentPhase} ({secondsPerDay}s/天, {daysPerSeason}天/季)");
    }

    private void Update()
    {
        if (GameStateManager.Instance == null) return;
        if (GameStateManager.Instance.CurrentState != GameState.Playing) return;

        AdvanceTime(Time.deltaTime);
    }

    /// <summary>推进时间。delta 为现实秒。</summary>
    private void AdvanceTime(float delta)
    {
        _dayTimer += delta;

        // 跨天（一帧内可能跨多天）
        while (_dayTimer >= secondsPerDay)
        {
            _dayTimer -= secondsPerDay;
            AdvanceDay();
        }

        CurrentTimeOfDay = (_dayTimer / secondsPerDay) * 24f;

        // 时段变化检测 → 发布 TimePhaseChangedEvent
        TimePhase newPhase = CalculatePhase(CurrentTimeOfDay, CurrentSeason);
        if (newPhase != CurrentPhase)
        {
            TimePhase oldPhase = CurrentPhase;
            CurrentPhase = newPhase;
            EventBus.Publish(new TimePhaseChangedEvent(oldPhase, newPhase));
        }

        // 更新当前小时（仅供 UI 直接读取，不发事件）
        CurrentHour = Mathf.Clamp(Mathf.FloorToInt(CurrentTimeOfDay), 0, 23);
    }

    /// <summary>进入新一天：天数+1，必要时切换季节，发布事件。</summary>
    private void AdvanceDay()
    {
        int oldDay = CurrentDay;
        CurrentDay++;

        Season oldSeason = CurrentSeason;
        Season newSeason = CalculateSeason(CurrentDay);
        if (newSeason != oldSeason)
        {
            CurrentSeason = newSeason;
            EventBus.Publish(new SeasonChangedEvent(oldSeason, newSeason));
            Debug.Log($"[TimeManager] 季节切换: {oldSeason} → {newSeason}");
        }

        EventBus.Publish(new TimeDayChangedEvent(oldDay, CurrentDay, CurrentSeason));

        Debug.Log($"[TimeManager] 新的一天: 第 {CurrentDay} 天，季节: {CurrentSeason}");
    }

    /// <summary>由天数推算季节（春夏秋冬循环）。</summary>
    private Season CalculateSeason(int day)
    {
        int seasonIndex = (((day - 1) / Mathf.Max(1, daysPerSeason)) % 4 + 4) % 4;
        return (Season)seasonIndex;
    }

    /// <summary>
    /// 由当天时刻 + 季节昼夜比例推算时段。
    /// 划分：Night[0,sunrise) → Dawn → Day → Dusk → Night[sunset,24)
    /// </summary>
    private TimePhase CalculatePhase(float timeOfDay, Season season)
    {
        float sunrise = GetSunrise(season);
        float sunset = GetSunset(season);

        if (timeOfDay < sunrise)
            return TimePhase.Night;                          // 前半夜
        if (timeOfDay < sunrise + DawnDuration)
            return TimePhase.Dawn;                           // 黎明
        if (timeOfDay < sunset - DuskDuration)
            return TimePhase.Day;                            // 白天
        if (timeOfDay < sunset)
            return TimePhase.Dusk;                           // 黄昏
        return TimePhase.Night;                              // 后半夜
    }

    // ===== 季节昼夜比例（日出/日落时刻）=====
    // 夏白天最长(15h)，冬最短(10h)，春秋各12h。

    private static float GetSunrise(Season season)
    {
        switch (season)
        {
            case Season.Summer: return 5f;
            case Season.Winter: return 7f;
            default:            return 6f;  // Spring / Autumn
        }
    }

    private static float GetSunset(Season season)
    {
        switch (season)
        {
            case Season.Summer: return 20f;
            case Season.Winter: return 17f;
            default:            return 18f;  // Spring / Autumn
        }
    }

    // ===== 难度系统配置接口（游戏开始前调用）=====

    /// <summary>设置现实秒/天。难度越高可缩短（每天更紧张）或延长。</summary>
    public void SetSecondsPerDay(float seconds)
    {
        secondsPerDay = Mathf.Max(1f, seconds);
    }

    /// <summary>设置总目标天数（0=无上限，作为胜利条件之一）。</summary>
    public void SetTotalDays(int days)
    {
        totalDays = Mathf.Max(0, days);
    }

    /// <summary>设置每季天数。</summary>
    public void SetDaysPerSeason(int days)
    {
        daysPerSeason = Mathf.Max(1, days);
    }
}
