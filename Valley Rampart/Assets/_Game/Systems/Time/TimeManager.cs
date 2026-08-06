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
/// 配置来源：WorldSystem.Config.time（TimeConfig）。所有时间规则不再硬编码。
/// secondsPerDay 会被 DifficultyManager.Initialize 按档位覆盖（Easy 慢/Hard 快）。
///
/// 发布的事件（仅这三种，小时变化不发事件）：
///   - TimePhaseChangedEvent 时段切换时发布
///   - TimeDayChangedEvent   新一天发布
///   - SeasonChangedEvent    季节切换时发布
///
/// 仅在 GameState.Playing 时推进。
/// 场景中需挂载此脚本（建议挂在空物体 "TimeManager" 上）。
/// </summary>
public class TimeManager : Singleton<TimeManager>, ISaveable
{
    public string SaveId => "TimeManager";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    // ===== 时间规则（运行时从 WorldConfig.time 读取，不再 SerializeField 硬编码）=====

    private float secondsPerDay = 480f;     // 现实秒/天
    private int startDay = 1;               // 起始天
    private float startHour = 6f;           // 起始时刻
    private int daysPerSeason = 10;         // 每季天数
    private float dawnDuration = 1f;        // 黎明时长（小时）
    private float duskDuration = 1f;        // 黄昏时长（小时）

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
    public float DayProgress => secondsPerDay > 0f ? _dayTimer / secondsPerDay : 0f;

    public float SecondsPerDay => secondsPerDay;
    public int DaysPerSeason => daysPerSeason;

    /// <summary>当前季节的日出时刻。</summary>
    public float SunriseHour => GetSunrise(CurrentSeason);

    /// <summary>当前季节的日落时刻。</summary>
    public float SunsetHour => GetSunset(CurrentSeason);

    // ===== 3.5 P0-6：时间倍速（仅 1x/2x）+ 战斗降速 =====

    /// <summary>当前游戏倍速（仅 1x/2x 两档，受 KingdomConfig.timeScales 约束）。</summary>
    public float CurrentTimeScale { get; private set; } = 1f;
    /// <summary>支持的倍速档位（KingdomConfig.timeScales；未配置回退 {1,2}）。</summary>
    private float[] _allowedScales = { 1f, 2f };
    /// <summary>战斗降速中（有敌人被感知）→ 强制 1x，禁止加速。</summary>
    public bool IsCombatSlowed { get; private set; }
    /// <summary>玩家上次请求的倍速（战斗降速结束后恢复此值，默认 1x）。</summary>
    private float _pendingScale = 1f;

    protected override void Awake()
    {
        base.Awake();

        // 从 WorldConfig 读时间规则（替代硬编码）
        LoadConfigFromWorld();

        CurrentDay = Mathf.Max(1, startDay);
        CurrentSeason = CalculateSeason(CurrentDay);
        CurrentTimeOfDay = Mathf.Clamp(startHour, 0f, 24f);
        CurrentHour = Mathf.Clamp(Mathf.FloorToInt(CurrentTimeOfDay), 0, 23);
        _dayTimer = (CurrentTimeOfDay / 24f) * secondsPerDay;
        CurrentPhase = CalculatePhase(CurrentTimeOfDay, CurrentSeason);

        Debug.Log($"[TimeManager] 初始化: 第{CurrentDay}天 {CurrentTimeOfDay:0.0}点 "
            + $"季节={CurrentSeason} 时段={CurrentPhase} ({secondsPerDay}s/天, {daysPerSeason}天/季)");

        // 3.5 P0-6：加载倍速档位（KingdomConfig.timeScales，仅 1x/2x）
        var kc = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        if (kc != null && kc.timeScales != null && kc.timeScales.Length > 0)
        {
            _allowedScales = new float[kc.timeScales.Length];
            for (int i = 0; i < kc.timeScales.Length; i++)
                _allowedScales[i] = Mathf.Max(1f, kc.timeScales[i]);
        }

        SaveManager.Instance.RegisterSaveable(this);
    }

    private void Start()
    {
        // 3.5 P0-6：敌人跨区块进入 → 战斗降速（强制 1x）
        EventBus.Subscribe<EnemyEnteredRegionEvent>(OnEnemyEnteredRegion);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        EventBus.Unsubscribe<EnemyEnteredRegionEvent>(OnEnemyEnteredRegion);
    }

    /// <summary>从 WorldSystem.Config.time 读取时间规则。config 不可用时用默认值兜底。</summary>
    private void LoadConfigFromWorld()
    {
        if (WorldSystem.Instance == null || WorldSystem.Instance.Config == null)
        {
            Debug.LogWarning("[TimeManager] WorldConfig 不可用，使用默认时间规则。");
            return;
        }
        var tc = WorldSystem.Instance.Config.time;
        secondsPerDay = tc.secondsPerDay > 0 ? tc.secondsPerDay : 480f;
        startDay = Mathf.Max(1, tc.startDay);
        startHour = Mathf.Clamp(tc.startHour, 0f, 24f);
        daysPerSeason = Mathf.Max(1, tc.daysPerSeason);
        dawnDuration = Mathf.Max(0f, tc.dawnDuration);
        duskDuration = Mathf.Max(0f, tc.duskDuration);
    }

    private void Update()
    {
        if (GameStateManager.Instance == null) return;
        if (GameStateManager.Instance.CurrentState != GameState.Playing) return;

        // 3.5 P0-6：战斗降速中 → 检测敌人是否全部清除，清除则恢复玩家请求倍速（战斗结束恢复 2x）
        if (IsCombatSlowed && !HasActiveEnemies())
            ExitCombatSlow();

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
        if (timeOfDay < sunrise + dawnDuration)
            return TimePhase.Dawn;                           // 黎明
        if (timeOfDay < sunset - duskDuration)
            return TimePhase.Day;                            // 白天
        if (timeOfDay < sunset)
            return TimePhase.Dusk;                           // 黄昏
        return TimePhase.Night;                              // 后半夜
    }

    // ===== 季节昼夜比例（日出/日落时刻，从 WorldConfig.season 读）=====

    private float GetSunrise(Season season)
    {
        return WorldSystem.Instance != null
            ? WorldSystem.Instance.GetSeasonSunData(season).sunriseHour
            : 6f;
    }

    private float GetSunset(Season season)
    {
        return WorldSystem.Instance != null
            ? WorldSystem.Instance.GetSeasonSunData(season).sunsetHour
            : 18f;
    }

    // ===== 状态重置（由 TeardownManager 返回主菜单时调用）=====

    /// <summary>
    /// 彻底重置运行时状态到初始值。
    /// InitializeWorld 不覆盖 CurrentDay/CurrentSeason/CurrentTimeOfDay 等字段，
    /// Singleton 不重走 Awake，所以必须由 TeardownManager 显式重置。
    /// secondsPerDay / daysPerSeason 不重置（InitializeWorld 会覆盖）。
    /// </summary>
    public void ResetState()
    {
        CurrentDay = Mathf.Max(1, startDay);
        CurrentSeason = CalculateSeason(CurrentDay);
        CurrentTimeOfDay = Mathf.Clamp(startHour, 0f, 24f);
        CurrentHour = Mathf.Clamp(Mathf.FloorToInt(CurrentTimeOfDay), 0, 23);
        _dayTimer = (CurrentTimeOfDay / 24f) * secondsPerDay;
        CurrentPhase = CalculatePhase(CurrentTimeOfDay, CurrentSeason);

        Debug.Log($"[TimeManager] ResetState: 第{CurrentDay}天 {CurrentTimeOfDay:0.0}点 "
            + $"季节={CurrentSeason} 时段={CurrentPhase}");
    }

    // ===== 配置接口（由 DifficultyManager / WorldSystem 调用）=====

    /// <summary>设置现实秒/天。难度越高可缩短（每天更紧张）或延长。</summary>
    public void SetSecondsPerDay(float seconds)
    {
        secondsPerDay = Mathf.Max(1f, seconds);
    }

    /// <summary>设置每季天数。</summary>
    public void SetDaysPerSeason(int days)
    {
        daysPerSeason = Mathf.Max(1, days);
    }

    // ===== 3.5 P0-6：倍速控制 + 战斗降速 =====

    /// <summary>
    /// 设置游戏倍速（仅允许 KingdomConfig.timeScales 档位，如 {1,2}）。
    /// 战斗降速中（IsCombatSlowed）→ 强制 1x，忽略加速请求。
    /// 暂停（Time.timeScale==0）时不覆盖，避免把暂停解冻成 1x。
    /// </summary>
    public void SetTimeScale(float scale)
    {
        float clamped = ClampToAllowedScale(scale);
        CurrentTimeScale = clamped;
        _pendingScale = clamped;   // 记录玩家请求倍速（战斗结束后恢复用）

        // 战斗降速强制 1x；暂停态（0）不覆盖，避免解冻暂停
        if (IsCombatSlowed || Mathf.Approximately(Time.timeScale, 0f)) return;
        Time.timeScale = clamped;
        Debug.Log($"[TimeManager] 倍速 → {clamped}x");
    }

    /// <summary>进入战斗降速（敌人被感知）：强制 1x，后续加速请求被忽略。</summary>
    private void EnterCombatSlow()
    {
        if (IsCombatSlowed) return;
        IsCombatSlowed = true;
        CurrentTimeScale = 1f;
        if (!Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = 1f;
        Debug.Log("[TimeManager] 战斗降速：敌人靠近，强制 1x");
    }

    /// <summary>退出战斗降速（敌人清除）：恢复玩家请求倍速（战斗结束恢复 2x）。</summary>
    private void ExitCombatSlow()
    {
        IsCombatSlowed = false;
        CurrentTimeScale = _pendingScale;
        if (!Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = _pendingScale;
        Debug.Log($"[TimeManager] 战斗结束，恢复 → {_pendingScale}x");
    }

    /// <summary>当前是否仍有存活敌人（Undead 阵营）。供战斗降速结束判定。</summary>
    private bool HasActiveEnemies()
    {
        if (UnitRegistry.Instance == null) return false;
        var all = UnitRegistry.Instance.GetAllUnits();
        for (int i = 0; i < all.Count; i++)
        {
            var u = all[i];
            if (u == null || u.Data == null) continue;
            if (u.Data.faction == Faction.Undead && u.IsAlive) return true;
        }
        return false;
    }

    /// <summary>敌人跨区块进入（威胁升整 region）→ 战斗降速。3.5 P0-6。</summary>
    private void OnEnemyEnteredRegion(EnemyEnteredRegionEvent evt)
    {
        EnterCombatSlow();
    }

    /// <summary>把请求倍速吸附到最近允许档位（最小 1x）。</summary>
    private float ClampToAllowedScale(float scale)
    {
        if (_allowedScales == null || _allowedScales.Length == 0) return Mathf.Max(1f, scale);
        float best = 1f;
        float bestDiff = float.MaxValue;
        for (int i = 0; i < _allowedScales.Length; i++)
        {
            float s = Mathf.Max(1f, _allowedScales[i]);
            float diff = Mathf.Abs(s - scale);
            if (diff < bestDiff) { bestDiff = diff; best = s; }
        }
        return best;
    }

    // ===== ISaveable 实现 =====

    public SavePayload SaveState()
    {
        var data = new TimeSaveData
        {
            currentDay = CurrentDay,
            currentTimeOfDay = CurrentTimeOfDay,
            currentSeason = (int)CurrentSeason,
            currentPhase = (int)CurrentPhase,
            secondsPerDay = SecondsPerDay,
            daysPerSeason = DaysPerSeason
        };
        return new SavePayload
        {
            typeName = typeof(TimeSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(TimeSaveData).AssemblyQualifiedName)
        {
            Debug.LogWarning("[TimeManager] 存档类型不匹配，跳过。");
            return;
        }

        var data = JsonUtility.FromJson<TimeSaveData>(payload.json);

        // 先配置再状态，防止 AdvanceDay 误触发
        if (data.secondsPerDay > 0) SetSecondsPerDay(data.secondsPerDay);
        if (data.daysPerSeason > 0) SetDaysPerSeason(data.daysPerSeason);

        // 直接赋值，不发事件
        CurrentDay = data.currentDay;
        CurrentTimeOfDay = data.currentTimeOfDay;
        CurrentSeason = (Season)data.currentSeason;
        CurrentPhase = (TimePhase)data.currentPhase;
        CurrentHour = Mathf.Clamp(Mathf.FloorToInt(CurrentTimeOfDay), 0, 23);
        _dayTimer = (CurrentTimeOfDay / 24f) * secondsPerDay;
    }
}

[System.Serializable]
public class TimeSaveData
{
    public int currentDay;
    public float currentTimeOfDay;
    public int currentSeason;
    public int currentPhase;
    public float secondsPerDay;
    public int daysPerSeason;
}
