using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 角色创建面板 UI 绑定。挂在 CharacterCreationUI GameObject 上。
/// 收集名字/难度/天数/槽位，按难度自动填充资源，调 MainMenuController 开始新游戏。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class CharacterCreationPanel : MonoBehaviour
{
    public static readonly string[] SlotIds = { "slot_1", "slot_2", "slot_3" };

    private MainMenuController _controller;
    private TextField _nameInput;
    private RadioButtonGroup _difficultyGroup;
    private RadioButtonGroup _slotGroup;
    private SliderInt _totalDaysSlider;
    private Label _totalDaysLabel;

    private bool _buttonsBound;
    // 保存值变化回调引用，供 OnDisable UnregisterValueChangedCallback 使用
    private EventCallback<ChangeEvent<int>> _totalDaysCallback;
    private EventCallback<ChangeEvent<int>> _difficultyCallback;
    private EventCallback<ChangeEvent<int>> _slotCallback;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        _controller = FindObjectOfType<MainMenuController>();
        if (_controller == null)
        {
            Debug.LogError("[CharacterCreationPanel] 找不到 MainMenuController。");
            return;
        }

        _nameInput = root.Q<TextField>("ruler-name-input");
        _difficultyGroup = root.Q<RadioButtonGroup>("difficulty-group");
        _slotGroup = root.Q<RadioButtonGroup>("slot-group");
        _totalDaysSlider = root.Q<SliderInt>("total-days-slider");
        _totalDaysLabel = root.Q<Label>("total-days-label");

        // 默认值
        _difficultyGroup.value = 1;  // 普通
        _slotGroup.value = 0;
        _totalDaysSlider.value = 60;

        // 同步滑块显示 + 难度预设联动
        UpdateTotalDaysLabel(_totalDaysSlider.value);
        _totalDaysCallback = evt => UpdateTotalDaysLabel(evt.newValue);
        _totalDaysSlider.RegisterValueChangedCallback(_totalDaysCallback);
        _difficultyCallback = OnDifficultyChanged;
        _difficultyGroup.RegisterValueChangedCallback(_difficultyCallback);

        // 占用提示
        _slotCallback = _ => RefreshSlotAvailability();
        _slotGroup.RegisterValueChangedCallback(_slotCallback);

        // 按钮（仅首次绑定时注册，OnDisable 里退订）
        if (!_buttonsBound)
        {
            root.Q<Button>("creation-back-button").clicked += _controller.OnCharacterCreationBackClicked;
            root.Q<Button>("creation-confirm-button").clicked += OnConfirmClicked;
            _buttonsBound = true;
        }

        // 首次刷新难度预设
        OnDifficultyChanged(1);

        RefreshSlotAvailability();
    }

    private void OnDisable()
    {
        // 退订值变化回调，防止反复 SetActive 导致重复注册
        if (_totalDaysSlider != null && _totalDaysCallback != null)
            _totalDaysSlider.UnregisterValueChangedCallback(_totalDaysCallback);
        if (_difficultyGroup != null && _difficultyCallback != null)
            _difficultyGroup.UnregisterValueChangedCallback(_difficultyCallback);
        if (_slotGroup != null && _slotCallback != null)
            _slotGroup.UnregisterValueChangedCallback(_slotCallback);

        // UI Toolkit 的 clicked 是 event，需要 -= 退订
        if (_buttonsBound && _controller != null)
        {
            var doc = GetComponent<UIDocument>();
            if (doc != null && doc.rootVisualElement != null)
            {
                doc.rootVisualElement.Q<Button>("creation-back-button").clicked -= _controller.OnCharacterCreationBackClicked;
                doc.rootVisualElement.Q<Button>("creation-confirm-button").clicked -= OnConfirmClicked;
            }
            _buttonsBound = false;
        }
    }

    private void OnDifficultyChanged(int newValue)
    {
        // 难度预设联动：选难度自动设置目标天数
        switch (newValue)
        {
            case 0: _totalDaysSlider.value = 90; break;  // Easy
            case 2: _totalDaysSlider.value = 30; break;  // Hard
            default: _totalDaysSlider.value = 60; break; // Normal
        }
        UpdateTotalDaysLabel(_totalDaysSlider.value);
    }

    private void OnDifficultyChanged(ChangeEvent<int> evt) => OnDifficultyChanged(evt.newValue);

    private void UpdateTotalDaysLabel(int days)
    {
        if (_totalDaysLabel != null) _totalDaysLabel.text = $"目标天数: {days}";
    }

    private void RefreshSlotAvailability()
    {
        if (_slotGroup == null) return;

        var radios = _slotGroup.Query<RadioButton>().ToList();
        for (int i = 0; i < radios.Count && i < SlotIds.Length; i++)
        {
            bool occupied = SaveManager.Instance.HasSave(SlotIds[i]);
            radios[i].SetEnabled(!occupied);
            if (occupied && _slotGroup.value == i)
            {
                _slotGroup.value = -1;
            }
        }
    }

    private void OnConfirmClicked()
    {
        if (_slotGroup.value < 0)
        {
            Debug.LogWarning("[CharacterCreation] 请选择一个未占用的存档槽。");
            return;
        }

        string name = string.IsNullOrEmpty(_nameInput.value) ? "无名君主" : _nameInput.value;
        string slotId = SlotIds[_slotGroup.value];

        var config = NewGameConfig.CreateWithDifficulty(_difficultyGroup.value, name, slotId);
        // 允许玩家手动覆盖目标天数
        config.totalDays = _totalDaysSlider.value;

        _controller.OnCharacterCreateConfirmed(config);
    }
}
