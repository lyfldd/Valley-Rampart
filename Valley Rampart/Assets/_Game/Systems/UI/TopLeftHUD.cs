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
    private Button _kingdomListButton;                          // 2_13 批D：列国名单入口（四职责承接③）
    private readonly Button[] _speedButtons = new Button[4];   // D241 倍速角落按钮（0.5x/1x/2x/3x）
    private static readonly float[] SpeedValues = { 0.5f, 1f, 2f, 3f };
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
        // 2_12 步骤8.4 / HH.17 决策1A：上帝视角无君主，HUD 君主面改王国信息（王国名/人口/金，删血量条）
        ShowKingdomInfo();
        RefreshDayAndSeasonDisplay();
    }

    /// <summary>上帝视角王国信息面板：王国名 + 人口 + 国库金（郡取代君主血条，HH.17 决策1A）。</summary>
    private void ShowKingdomInfo()
    {
        var ruler = RulerController.Instance;
        if (ruler == null) return;
        // 2_13：王国名优先取 KingdomManager（取代君主名）；君主名兜底兼容旧档
        var km = KingdomManager.Instance;
        string kingdomName = (km != null && !string.IsNullOrEmpty(km.KingdomName))
            ? km.KingdomName
            : (string.IsNullOrEmpty(ruler.RulerName) ? "王国" : ruler.RulerName);
        int gold = ruler.Gold;
        int pop = PopulationSystem.Instance != null ? PopulationSystem.Instance.PopulationCount : -1;
        // 复用原君主血条/攻防标签承载王国信息；血条隐藏（王国无 HP）
        if (_hpBarFill != null) _hpBarFill.style.display = DisplayStyle.None;
        if (_hpTextLabel != null) _hpTextLabel.text = kingdomName;
        if (_attackLabel != null) _attackLabel.text = $"人口 {pop}";
        if (_defenseLabel != null) _defenseLabel.text = $"金币 {gold}";
        if (_hpTextLabel != null) _hpTextLabel.MarkDirtyRepaint();
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

        // 2_13 批D：列国名单入口按钮绑定（四职责承接③；D305"播报点击展开"让渡登记）
        _kingdomListButton = root.Q<Button>("kingdom-list-button");
        if (_kingdomListButton != null) _kingdomListButton.clicked += OnKingdomListClicked;

        // D241 倍速角落按钮绑定（0.5x/1x/2x/3x → TimeManager.SetGameSpeed）
        _speedButtons[0] = root.Q<Button>("speed-05");
        _speedButtons[1] = root.Q<Button>("speed-10");
        _speedButtons[2] = root.Q<Button>("speed-20");
        _speedButtons[3] = root.Q<Button>("speed-30");
        for (int i = 0; i < _speedButtons.Length; i++)
        {
            int idx = i;    // 闭包捕获
            if (_speedButtons[i] != null) _speedButtons[i].clicked += () => OnSpeedClicked(idx);
        }

        _labelsBound = true;
    }

    /// <summary>D241 倍速角落按钮：切 TimeManager.SetGameSpeed 并高亮当前档。</summary>
    private void OnSpeedClicked(int idx)
    {
        if (TimeManager.Instance == null) return;
        TimeManager.Instance.SetGameSpeed(SpeedValues[idx]);
        for (int i = 0; i < _speedButtons.Length; i++)
        {
            if (_speedButtons[i] == null) continue;
            if (i == idx) _speedButtons[i].AddToClassList("speed-button--active");
            else _speedButtons[i].RemoveFromClassList("speed-button--active");
        }
        Debug.Log($"[TopLeftHUD] 倍速切换：{SpeedValues[idx]}x");
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
        if (data != null && data.faction == Faction.PlayerCamp && data.occupation == Occupation.Ruler)
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
        UIManager.Instance?.Push(popPanel, new Interactor(Faction.PlayerCamp, Vector3.zero));
    }

    /// <summary>「列国」按钮：推送 KingdomListPanel 入栈（2_13 批D 四职责承接③）。</summary>
    private void OnKingdomListClicked()
    {
        var listPanel = FindObjectOfType<KingdomListPanel>();
        if (listPanel == null)
        {
            Debug.LogWarning("[TopLeftHUD] 未找到 KingdomListPanel（场景缺少 KingdomListUI 挂载）");
            return;
        }
        UIManager.Instance?.Push(listPanel, new Interactor(Faction.PlayerCamp, Vector3.zero));
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
