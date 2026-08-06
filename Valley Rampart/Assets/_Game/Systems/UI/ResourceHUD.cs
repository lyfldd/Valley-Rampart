using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 右上角资源面板（UI Toolkit 版）：显示国库全部资源（金/石/木/粮 + 特殊食物/肉）+ 整体幸福度徽标。
/// 刷新策略：
///   - 资源：事件驱动（RulerResourceChangedEvent）+ Start 时主动读一次。
///   - 幸福度：Start 读一次 + 订阅 TimeDayChangedEvent（换日 HappinessSystem 结算后刷新）
///     + Update 低频兜底（每 1s，防换日事件顺序/其它系统改动漏刷）。
/// 幸福度徽标在主菜单/未进入游戏地图时隐藏（见 RefreshHappiness）。
/// 挂载位置：SampleScene 右上角的 UIDocument GameObject 上。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ResourceHUD : MonoBehaviour
{
    // 资源项：名称由 UXML 静态文本提供，这里只需绑定数值 Label。
    private Label _goldLabel;
    private Label _stoneLabel;
    private Label _woodLabel;
    private Label _foodLabel;
    private Label _specialFoodLabel;
    private Label _meatLabel;

    // 整体幸福度徽标
    private Label _happinessBadge;
    private bool _labelsBound;

    // 幸福度低频兜底刷新计时（事件驱动主要负责正确性，Update 仅为兜底）
    private float _happinessRefreshTimer;
    private const float HappinessRefreshInterval = 1f;

    private void OnEnable()
    {
        if (!_labelsBound) BindLabels();
        EventBus.Subscribe<RulerResourceChangedEvent>(OnResourceChanged);
        EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RulerResourceChangedEvent>(OnResourceChanged);
        EventBus.Unsubscribe<TimeDayChangedEvent>(OnDayChanged);
    }

    private void Start()
    {
        if (!_labelsBound) BindLabels();
        RefreshAll();
        RefreshHappiness();
    }

    private void Update()
    {
        // 低频兜底：每 1s 刷新幸福度，保证换日事件顺序或其它系统改动不遗漏。
        _happinessRefreshTimer += Time.deltaTime;
        if (_happinessRefreshTimer >= HappinessRefreshInterval)
        {
            _happinessRefreshTimer = 0f;
            RefreshHappiness();
        }
    }

    private void BindLabels()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _goldLabel = root.Q<Label>("res-value-gold");
        _stoneLabel = root.Q<Label>("res-value-stone");
        _woodLabel = root.Q<Label>("res-value-wood");
        _foodLabel = root.Q<Label>("res-value-food");
        _specialFoodLabel = root.Q<Label>("res-value-special-food");
        _meatLabel = root.Q<Label>("res-value-meat");
        _happinessBadge = root.Q<Label>("happiness-badge");

        _labelsBound = true;
    }

    private void OnResourceChanged(RulerResourceChangedEvent evt)
    {
        UpdateResourceLabel(evt.Type, evt.NewValue);
    }

    private void OnDayChanged(TimeDayChangedEvent evt)
    {
        // 换日时 HappinessSystem.OnNewDay 已重算整体幸福，这里同步刷新徽标。
        RefreshHappiness();
    }

    private void RefreshAll()
    {
        var ruler = RulerController.Instance;
        if (ruler == null) return;

        UpdateResourceLabel(ResourceType.Gold, ruler.Gold);
        UpdateResourceLabel(ResourceType.Stone, ruler.Stone);
        UpdateResourceLabel(ResourceType.Wood, ruler.Wood);
        UpdateResourceLabel(ResourceType.Food, ruler.Food);
        UpdateResourceLabel(ResourceType.SpecialFood, ruler.SpecialFood);
        UpdateResourceLabel(ResourceType.Meat, ruler.Meat);
    }

    private void UpdateResourceLabel(ResourceType type, int value)
    {
        var target = type switch
        {
            ResourceType.Gold => _goldLabel,
            ResourceType.Stone => _stoneLabel,
            ResourceType.Wood => _woodLabel,
            ResourceType.Food => _foodLabel,
            ResourceType.SpecialFood => _specialFoodLabel,
            ResourceType.Meat => _meatLabel,
            _ => null
        };

        if (target != null)
            target.text = FormatValue(value);
    }

    /// <summary>
    /// 刷新幸福度徽标。HappinessSystem 是 Singleton 会自动创建实例，
    /// 故用 WorldManager 是否已有活动地图判定是否处于实际游戏场景——
    /// 主菜单/未进游戏时隐藏徽标，避免误显示默认值 50。
    /// </summary>
    private void RefreshHappiness()
    {
        if (_happinessBadge == null) return;

        var happiness = HappinessSystem.Instance;
        bool inGame = happiness != null
                      && WorldManager.Instance != null
                      && WorldManager.Instance.ActiveMap != null;
        if (!inGame)
        {
            _happinessBadge.style.display = DisplayStyle.None;
            return;
        }

        _happinessBadge.style.display = DisplayStyle.Flex;
        _happinessBadge.text = $"幸福 {Mathf.RoundToInt(happiness.OverallHappiness)}";
    }

    private static string FormatValue(int value)
    {
        if (value >= 1000000)
            return $"{value / 1000000f:F1}M";
        if (value >= 1000)
            return $"{value / 1000f:F1}K";
        return value.ToString();
    }
}