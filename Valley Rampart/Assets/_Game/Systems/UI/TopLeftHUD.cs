using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 左上角 HUD：显示君主血条/战斗属性，以及游戏时间/天数/季节。
///
/// 刷新策略：
///   - 君主血条/属性：事件驱动（UnitHpChangedEvent / UnitAttributeChangedEvent）
///   - 时钟（HH:MM · 时段）：每帧轮询 TimeManager.CurrentTimeOfDay，仅在分钟变化时写 Text
///     （时间每分每秒都在变，不适合事件驱动）
///   - 天数：事件驱动（TimeDayChangedEvent，变化不频繁）
///   - 季节：事件驱动（SeasonChangedEvent / TimeDayChangedEvent）
///
/// 挂载位置：Canvas/TopLeftHUD。
/// Inspector 拖入对应 Image/Text 即可，未拖入的字段不显示。
/// </summary>
public class TopLeftHUD : MonoBehaviour
{
    [Header("血条")]
    [Tooltip("血条填充 Image（Image Type=Filled, Fill Method=Horizontal）。")]
    [SerializeField] private Image hpFillImage;

    [Tooltip("可选：血量文字，格式 '当前/最大'。")]
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("属性显示（可选）")]
    [Tooltip("可选：攻击力文字。")]
    [SerializeField] private TextMeshProUGUI attackText;

    [Tooltip("可选：防御力文字。")]
    [SerializeField] private TextMeshProUGUI defenseText;

    [Header("时间显示（可选）")]
    [Tooltip("可选：时钟文字，格式 'HH:MM · 时段'。每帧轮询刷新。")]
    [SerializeField] private TextMeshProUGUI timeText;

    [Tooltip("可选：天数文字，格式 '第 X 天'。事件驱动刷新。")]
    [SerializeField] private TextMeshProUGUI dayText;

    [Tooltip("可选：季节文字。事件驱动刷新。")]
    [SerializeField] private TextMeshProUGUI seasonText;

    private UnitController _monarch;

    // 时段/季节中文名（顺序对应枚举值：Night=0,Dawn=1,Day=2,Dusk=3 / Spring..Winter）
    private static readonly string[] PhaseNames = { "夜晚", "黎明", "白天", "黄昏" };
    private static readonly string[] SeasonNames = { "春", "夏", "秋", "冬" };

    // 上次显示的分钟数，用于轮询时只在分钟变化才写 Text
    private int _lastMinute = -1;

    private void Awake()
    {
        // 君主事件
        EventBus.Subscribe<UnitSpawnedEvent>(OnUnitSpawned);
        EventBus.Subscribe<UnitHpChangedEvent>(OnHpChanged);
        EventBus.Subscribe<UnitAttributeChangedEvent>(OnAttributeChanged);
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);

        // 天数/季节事件（时钟不走事件，靠 Update 轮询）
        EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);
        EventBus.Subscribe<SeasonChangedEvent>(OnSeasonChanged);
    }

    private void Start()
    {
        // HUD 可能比君主先启动，两种情况都处理
        TryBindMonarch();
        // 天数/季节初始显示（事件是变化时才发，开局需主动读一次）
        RefreshDayAndSeasonDisplay();
    }

    private void Update()
    {
        // 时钟：每帧轮询当前时刻，仅在分钟变化时刷新 Text
        var tm = TimeManager.Instance;
        if (tm == null || timeText == null) return;

        int totalMinutes = Mathf.FloorToInt(tm.CurrentTimeOfDay * 60f);  // 0~1439
        int minute = totalMinutes % 60;

        if (minute != _lastMinute)
        {
            _lastMinute = minute;
            int hour = totalMinutes / 60;
            UpdateTimeText(hour, minute, tm.CurrentPhase);
        }
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<UnitSpawnedEvent>(OnUnitSpawned);
        EventBus.Unsubscribe<UnitHpChangedEvent>(OnHpChanged);
        EventBus.Unsubscribe<UnitAttributeChangedEvent>(OnAttributeChanged);
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);

        EventBus.Unsubscribe<TimeDayChangedEvent>(OnDayChanged);
        EventBus.Unsubscribe<SeasonChangedEvent>(OnSeasonChanged);
    }

    // ===== 君主绑定 =====

    private void TryBindMonarch()
    {
        if (_monarch != null) return;

        var ruler = RulerController.Instance;
        if (ruler == null || ruler.MonarchUnit == null) return;

        _monarch = ruler.MonarchUnit;
        RefreshMonarchDisplay();
        Debug.Log("[TopLeftHUD] 已绑定君主单位，HUD 开始刷新。");
    }

    private void OnUnitSpawned(UnitSpawnedEvent evt)
    {
        if (_monarch != null) return;

        // 只在君主生成时才绑定，过滤掉 NPC/敌人，避免无谓的 RulerController 访问。
        UnitData data = evt.Unit != null ? evt.Unit.Data : null;
        if (data != null
            && data.faction == Faction.Human_Player
            && data.occupation == Occupation.Ruler)
        {
            TryBindMonarch();
        }
    }

    private void OnHpChanged(UnitHpChangedEvent evt)
    {
        if (evt.Unit != _monarch) return;
        UpdateHpBar(evt.NewHp, evt.MaxHp);
    }

    private void OnAttributeChanged(UnitAttributeChangedEvent evt)
    {
        if (evt.Unit != _monarch) return;

        switch (evt.AttributeType)
        {
            case UnitAttributeType.MaxHp:
                UpdateHpBar(_monarch.CurrentHp, _monarch.MaxHp);
                break;
            case UnitAttributeType.Attack:
                if (attackText != null) attackText.text = _monarch.Attack.ToString();
                break;
            case UnitAttributeType.Defense:
                if (defenseText != null) defenseText.text = _monarch.Defense.ToString();
                break;
        }
    }

    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (evt.Unit != _monarch) return;
        UpdateHpBar(0, _monarch != null ? _monarch.MaxHp : 0);
        _monarch = null;
        Debug.Log("[TopLeftHUD] 君主阵亡，HUD 停止刷新。");
    }

    private void RefreshMonarchDisplay()
    {
        if (_monarch == null) return;
        UpdateHpBar(_monarch.CurrentHp, _monarch.MaxHp);
        if (attackText != null) attackText.text = _monarch.Attack.ToString();
        if (defenseText != null) defenseText.text = _monarch.Defense.ToString();
    }

    private void UpdateHpBar(int current, int max)
    {
        if (hpFillImage != null)
        {
            float ratio = max > 0 ? (float)current / max : 0f;
            hpFillImage.fillAmount = Mathf.Clamp01(ratio);
        }
        if (hpText != null)
            hpText.text = $"{current}/{max}";
    }

    // ===== 天数 / 季节（事件驱动）=====

    private void RefreshDayAndSeasonDisplay()
    {
        var tm = TimeManager.Instance;
        if (tm == null) return;
        UpdateDayText(tm.CurrentDay);
        UpdateSeasonText(tm.CurrentSeason);
    }

    private void OnDayChanged(TimeDayChangedEvent evt)
    {
        UpdateDayText(evt.NewDay);
        UpdateSeasonText(evt.Season);
    }

    private void OnSeasonChanged(SeasonChangedEvent evt)
    {
        UpdateSeasonText(evt.NewSeason);
    }

    private void UpdateDayText(int day)
    {
        if (dayText != null)
            dayText.text = $"第 {day} 天";
    }

    private void UpdateSeasonText(Season season)
    {
        if (seasonText != null)
            seasonText.text = SeasonNames[(int)season];
    }

    // ===== 时钟（轮询驱动）=====

    private void UpdateTimeText(int hour, int minute, TimePhase phase)
    {
        if (timeText != null)
            timeText.text = $"{hour:00}:{minute:00} · {PhaseNames[(int)phase]}";
    }
}
