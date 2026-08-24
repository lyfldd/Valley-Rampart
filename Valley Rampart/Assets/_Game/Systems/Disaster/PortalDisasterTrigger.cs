using UnityEngine;

// 2_14 灾害触发判定（实施计划步骤4 / 设计稿 §2.1）
// 确定性纪律（R4）：全程注入 System.Random(seed)，禁用 UnityEngine.Random/Time.deltaTime，供 sim 对拍。
// 触发条件：
//   1. day < minDaysBeforeFirst → 不触发（给玩家发展期）
//   2. 连续未触发天数 ≥ forceTriggerAfterDays → 保底触发（保底计数自 day≥minDaysBeforeFirst 起算）
//   3. 否则按 triggerProbability × 难度系数(D237) 概率触发
//   4. 已有活跃传送门（ActivePortalCount ≥ maxPortalPerNight）→ 不触发（且不计保底失败）
// 触发 → 发布 PortalDisasterTriggeredEvent（UI 预警 / 2_10 渲染 / 步骤5 传送门生成订阅）。
public class PortalDisasterTrigger : MonoBehaviour
{
    [Tooltip("灾害触发生成配置 SO（缺省从 Resources/Config/Disaster 加载）")]
    [SerializeField] private PortalDisasterConfig config;

    /// <summary>灾害触发状态（供存档 2_11）。</summary>
    public DisasterState state = new DisasterState();

    /// <summary>当前活跃传送门数（由传送门调度步骤5起写回；本步骤占位 0）。</summary>
    public int ActivePortalCount { get; set; }

    /// <summary>灾害触发状态，读给存档用。</summary>
    public DisasterState State => state;

    private System.Random _rng = new System.Random();

    private void OnEnable()
    {
        EnsureConfig();
        EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<TimeDayChangedEvent>(OnDayChanged);
    }

    /// <summary>注入确定性种子（世界种子派生），供 sim 对拍。</summary>
    public void InitializeSeeded(int seed, PortalDisasterConfig cfg = null)
    {
        _rng = new System.Random(seed);
        if (cfg != null) config = cfg;
    }

    private void EnsureConfig()
    {
        if (config != null) return;
        config = Resources.Load<PortalDisasterConfig>("Config/Disaster/PortalDisasterConfig");
    }

    private void OnDayChanged(TimeDayChangedEvent evt)
    {
        int difficulty = DifficultyManager.Instance != null ? DifficultyManager.Instance.CurrentDifficulty : 2;
        if (Evaluate(evt.NewDay, difficulty))
        {
            Debug.Log($"[2_14] 灾害触发：day={evt.NewDay}, 累计={state.totalTriggers}, 已连续未触发={state.daysSinceLastTrigger}");
            EventBus.Publish(new PortalDisasterTriggeredEvent(evt.NewDay, Vector2.zero));
        }
    }

    /// <summary>推进灾害状态并返回本夜是否触发（副作用：累计/保底计数）。供实现与验收调用。</summary>
    public bool Evaluate(int day, int difficulty)
    {
        EnsureConfig();
        if (config == null)
        {
            Debug.LogWarning("[2_14] 缺 PortalDisasterConfig 资产，灾害触发停用。");
            return false;
        }
        if (day < config.minDaysBeforeFirst) return false;                    // 前 N 天不触发（发展期）
        if (ActivePortalCount >= config.maxPortalPerNight) return false;      // 同夜已达传送门上限（不计保底失败）

        // 保底：连续未触发天数达标 → 必触发；否则按概率 × 难度倍率
        bool force = state.daysSinceLastTrigger >= config.forceTriggerAfterDays;
        bool triggered = force
            || (_rng.NextDouble() < config.triggerProbability * config.GetTriggerMultiplier(difficulty));

        if (triggered) { state.daysSinceLastTrigger = 0; state.totalTriggers++; }
        else state.daysSinceLastTrigger++;
        return triggered;
    }

    /// <summary>纯判定（不改状态）：供确定性命中测试/单字段对拍。</summary>
    public static bool ShouldTriggerPure(int day, int difficulty, DisasterState st,
        PortalDisasterConfig cfg, System.Random rng)
    {
        if (cfg == null) return false;
        if (day < cfg.minDaysBeforeFirst) return false;
        bool force = st.daysSinceLastTrigger >= cfg.forceTriggerAfterDays;
        bool roll = !force && (rng.NextDouble() < cfg.triggerProbability * cfg.GetTriggerMultiplier(difficulty));
        return force || roll;
    }
}