using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 左上角 HUD（UI Toolkit 版）：显示君主血条/战斗属性，以及游戏时间/天数/季节。
/// 刷新策略：
///   - 君主血条/属性：事件驱动（UnitHpChangedEvent / UnitAttributeChangedEvent）
///   - 时钟：每帧轮询 TimeManager.CurrentTimeOfDay，仅在分钟变化时写 Label
///   - 天数/季节：事件驱动（TimeDayChangedEvent / SeasonChangedEvent）
/// 挂载位置：SampleScene 左上角的 UIDocument GameObject 上。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TopLeftHUD : MonoBehaviour
{
    private Label _hpTextLabel;
    private VisualElement _hpBarFill;
    private Label _attackLabel;
    private Label _defenseLabel;
    private Label _timeLabel;
    private Label _dayLabel;
    private Label _seasonLabel;
    private Button _populationButton;
    private Button _warehouseButton;
    private bool _labelsBound;

    private UnitController _monarch;
    private static readonly string[] PhaseNames = { "夜晚", "黎明", "白天", "黄昏" };
    private static readonly string[] SeasonNames = { "春", "夏", "秋", "冬" };
    private int _lastMinute = -1;

    private void OnEnable()
    {
        if (!_labelsBound) BindLabels();
        EventBus.Subscribe<UnitSpawnedEvent>(OnUnitSpawned);
        EventBus.Subscribe<UnitHpChangedEvent>(OnHpChanged);
        EventBus.Subscribe<UnitAttributeChangedEvent>(OnAttributeChanged);
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
        EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);
        EventBus.Subscribe<SeasonChangedEvent>(OnSeasonChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitSpawnedEvent>(OnUnitSpawned);
        EventBus.Unsubscribe<UnitHpChangedEvent>(OnHpChanged);
        EventBus.Unsubscribe<UnitAttributeChangedEvent>(OnAttributeChanged);
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        EventBus.Unsubscribe<TimeDayChangedEvent>(OnDayChanged);
        EventBus.Unsubscribe<SeasonChangedEvent>(OnSeasonChanged);
    }

    private void Start()
    {
        if (!_labelsBound) BindLabels();
        TryBindMonarch();
        RefreshDayAndSeasonDisplay();
    }

    private void Update()
    {
        var tm = TimeManager.Instance;
        if (tm == null || _timeLabel == null) return;

        int totalMinutes = Mathf.FloorToInt(tm.CurrentTimeOfDay * 60f);
        int minute = totalMinutes % 60;

        if (minute != _lastMinute)
        {
            _lastMinute = minute;
            int hour = totalMinutes / 60;
            UpdateTimeText(hour, minute, tm.CurrentPhase);
        }
    }

    private void BindLabels()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _hpTextLabel = root.Q<Label>("hp-text");
        _hpBarFill = root.Q<VisualElement>("hp-bar-fill");
        _attackLabel = root.Q<Label>("atk-value");
        _defenseLabel = root.Q<Label>("def-value");
        _timeLabel = root.Q<Label>("time-text");
        _dayLabel = root.Q<Label>("day-text");
        _seasonLabel = root.Q<Label>("season-text");
        _populationButton = root.Q<Button>("population-button");
        if (_populationButton != null) _populationButton.clicked += OnPopulationClicked;
        _warehouseButton = root.Q<Button>("warehouse-button");
        if (_warehouseButton != null) _warehouseButton.clicked += OnWarehouseClicked;

        _labelsBound = true;
    }

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
        UnitData data = evt.Unit != null ? evt.Unit.Data : null;
        if (data != null && data.faction == Faction.Human_Player && data.occupation == Occupation.Ruler)
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
                if (_attackLabel != null) _attackLabel.text = _monarch.Attack.ToString();
                break;
            case UnitAttributeType.Defense:
                if (_defenseLabel != null) _defenseLabel.text = _monarch.Defense.ToString();
                break;
        }
    }

    private void OnUnitDied(UnitDiedEvent evt)
    {
        // 3.4：evt.Unit 改为 IDamageable，需 as UnitController 判等（君主是 UnitController）
        if (evt.Unit as UnitController != _monarch) return;
        UpdateHpBar(0, _monarch != null ? _monarch.MaxHp : 0);
        _monarch = null;
        Debug.Log("[TopLeftHUD] 君主阵亡，HUD 停止刷新。");
    }

    private void RefreshMonarchDisplay()
    {
        if (_monarch == null) return;
        UpdateHpBar(_monarch.CurrentHp, _monarch.MaxHp);
        if (_attackLabel != null) _attackLabel.text = _monarch.Attack.ToString();
        if (_defenseLabel != null) _defenseLabel.text = _monarch.Defense.ToString();
    }

    private void UpdateHpBar(int current, int max)
    {
        if (_hpBarFill != null)
        {
            float ratio = max > 0 ? (float)current / max : 0f;
            // UI Toolkit 用 style.width 百分比模拟 fillAmount
            _hpBarFill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f);
        }
        if (_hpTextLabel != null)
            _hpTextLabel.text = $"{current}/{max}";
    }

    private void RefreshDayAndSeasonDisplay()
    {
        var tm = TimeManager.Instance;
        if (tm == null) return;
        UpdateDayText(tm.CurrentDay);
        UpdateSeasonText(tm.CurrentSeason);
    }

    /// <summary>「人口」按钮：推送 PopulationPanel 入栈。</summary>
    private void OnPopulationClicked()
    {
        var popPanel = FindObjectOfType<PopulationPanel>();
        if (popPanel == null)
        {
            Debug.LogWarning("[TopLeftHUD] 未找到 PopulationPanel（场景缺少挂载 PopulationPanel + UIDocument 的 GameObject）");
            return;
        }
        UIManager.Instance?.Push(popPanel, new Interactor(Faction.Human_Player, Vector3.zero));
    }

    /// <summary>「仓库」按钮：推送 WarehousePanel 入栈（QQQ.2 §需求7 / DR-15）。</summary>
    private void OnWarehouseClicked()
    {
        var whPanel = FindObjectOfType<WarehousePanel>();
        if (whPanel == null)
        {
            Debug.LogWarning("[TopLeftHUD] 未找到 WarehousePanel（场景缺少挂载 WarehousePanel + UIDocument 的 GameObject）");
            return;
        }
        UIManager.Instance?.Push(whPanel, new Interactor(Faction.Human_Player, Vector3.zero));
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
        if (_dayLabel != null)
            _dayLabel.text = $"第 {day} 天";
    }

    private void UpdateSeasonText(Season season)
    {
        if (_seasonLabel != null)
            _seasonLabel.text = SeasonNames[(int)season];
    }

    private void UpdateTimeText(int hour, int minute, TimePhase phase)
    {
        if (_timeLabel != null)
            _timeLabel.text = $"{hour:00}:{minute:00} · {PhaseNames[(int)phase]}";
    }
}
