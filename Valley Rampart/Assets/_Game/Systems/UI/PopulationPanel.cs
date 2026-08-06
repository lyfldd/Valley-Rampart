using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 人口面板（3.5 人口系统 UI）。挂在 SampleScene 的 PopulationPanel GameObject 上（UIDocument）。
/// 实现 IUIPanel，由 TopLeftHUD 的「人口」按钮 Push 入栈，关闭 Pop。
///
/// 显示：人口数 / 平均饱食 / 整体幸福 / 生育冷却天数。
/// 主菜单（PopulationSystem 或 HappinessSystem 为 null）时隐藏面板。
///
/// 刷新策略：事件驱动（TimeDayChangedEvent，换日生育/幸福/饱食结算后刷新）。
/// 所属系统：PopulationSystem（人口/冷却）/ HappinessSystem（整体幸福）/ SatietySystem（平均饱食）。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PopulationPanel : MonoBehaviour, IUIPanel
{
    private bool _bound;                 // 防止重复绑定
    private bool _visible;               // 面板是否显示（事件回调里判断）

    // ===== UI 元素引用 =====
    private Label _populationValue;
    private Label _satietyValue;
    private Label _happinessValue;
    private Label _birthCooldownValue;
    private Button _closeButton;

    // ===== IUIPanel =====

    public void Open(Interactor ctx)
    {
        if (!_bound) Bind();
        _visible = true;
        Refresh();
        SetVisible(true);
    }

    public void Close()
    {
        _visible = false;
        SetVisible(false);
    }

    public void Refresh()
    {
        // 主菜单：PopulationSystem 或 HappinessSystem 缺失时隐藏面板
        if (PopulationSystem.Instance == null || HappinessSystem.Instance == null)
        {
            SetVisible(false);
            return;
        }
        SetVisible(true);

        var pop = PopulationSystem.Instance;
        if (_populationValue != null)
            _populationValue.text = pop.PopulationCount.ToString();
        if (_satietyValue != null)
            _satietyValue.text = SatietySystem.Instance != null ? $"{SatietySystem.Instance.GetAverageSatiety():F0}" : "-";
        if (_happinessValue != null)
            _happinessValue.text = $"{HappinessSystem.Instance.OverallHappiness:F0}";
        if (_birthCooldownValue != null)
            _birthCooldownValue.text = $"{pop.BirthCooldownDays} 天";
    }

    // ===== Unity 生命周期 =====

    private void OnEnable()
    {
        EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);
        if (!_bound) Bind();
        SetVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<TimeDayChangedEvent>(OnDayChanged);
        Unbind();
    }

    private void Start()
    {
        if (!_bound) Bind();
    }

    // ===== 事件回调（换日 → 生育/幸福/饱食已结算，刷新）=====

    private void OnDayChanged(TimeDayChangedEvent evt)
    {
        if (_visible) Refresh();
    }

    // ===== 按钮绑定 / 解绑 =====

    private void Bind()
    {
        if (_bound) return;
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _populationValue = root.Q<Label>("population-value");
        _satietyValue = root.Q<Label>("satiety-value");
        _happinessValue = root.Q<Label>("happiness-value");
        _birthCooldownValue = root.Q<Label>("birth-cooldown-value");
        _closeButton = root.Q<Button>("population-close-button");

        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

        // 标题栏拖动（可拖动窗口，不破坏关闭按钮点击）
        var panel = root.Q<VisualElement>("population-panel");
        var handle = root.Q<VisualElement>("drag-handle");
        if (panel != null && handle != null) UIDragHelper.Attach(panel, handle);

        _bound = true;
    }

    private void Unbind()
    {
        if (!_bound) return;
        if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
        _bound = false;
    }

    private void OnCloseClicked()
    {
        UIManager.Instance?.CloseCurrent();
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}