using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 右上角资源面板（UI Toolkit 版）：显示国家四种资源（Gold/Stone/Wood/Food）。
/// 刷新策略：事件驱动（RulerResourceChangedEvent）+ Start 时主动读一次。
/// 挂载位置：SampleScene 右上角的 UIDocument GameObject 上。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ResourceHUD : MonoBehaviour
{
    private Label _goldLabel;
    private Label _stoneLabel;
    private Label _woodLabel;
    private Label _foodLabel;
    private bool _labelsBound;

    private void OnEnable()
    {
        if (!_labelsBound) BindLabels();
        EventBus.Subscribe<RulerResourceChangedEvent>(OnResourceChanged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<RulerResourceChangedEvent>(OnResourceChanged);
    }

    private void Start()
    {
        if (!_labelsBound) BindLabels();
        RefreshAll();
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

        _labelsBound = true;
    }

    private void OnResourceChanged(RulerResourceChangedEvent evt)
    {
        UpdateResourceLabel(evt.Type, evt.NewValue);
    }

    private void RefreshAll()
    {
        var ruler = RulerController.Instance;
        if (ruler == null) return;

        UpdateResourceLabel(ResourceType.Gold, ruler.Gold);
        UpdateResourceLabel(ResourceType.Stone, ruler.Stone);
        UpdateResourceLabel(ResourceType.Wood, ruler.Wood);
        UpdateResourceLabel(ResourceType.Food, ruler.Food);
    }

    private void UpdateResourceLabel(ResourceType type, int value)
    {
        var target = type switch
        {
            ResourceType.Gold => _goldLabel,
            ResourceType.Stone => _stoneLabel,
            ResourceType.Wood => _woodLabel,
            ResourceType.Food => _foodLabel,
            _ => null
        };

        if (target != null)
            target.text = FormatValue(value);
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
