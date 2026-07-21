using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 角色创建面板 UI 绑定。挂在 CharacterCreationUI GameObject 上。
/// 收集名字/难度/天数/槽位，调 MainMenuController 开始新游戏。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class CharacterCreationPanel : MonoBehaviour
{
    private static readonly string[] SlotIds = { "slot_1", "slot_2", "slot_3" };

    private MainMenuController _controller;
    private TextField _nameInput;
    private RadioButtonGroup _difficultyGroup;
    private RadioButtonGroup _slotGroup;
    private SliderInt _totalDaysSlider;
    private Label _totalDaysLabel;

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
        _totalDaysSlider.value = 30;

        // 同步滑块显示
        UpdateTotalDaysLabel(_totalDaysSlider.value);
        _totalDaysSlider.RegisterValueChangedCallback(evt => UpdateTotalDaysLabel(evt.newValue));

        // 占用提示
        _slotGroup.RegisterValueChangedCallback(_ => RefreshSlotAvailability());

        // 按钮
        root.Q<Button>("creation-back-button").clicked += _controller.OnCharacterCreationBackClicked;
        root.Q<Button>("creation-confirm-button").clicked += OnConfirmClicked;

        RefreshSlotAvailability();
    }

    private void UpdateTotalDaysLabel(int days)
    {
        if (_totalDaysLabel != null) _totalDaysLabel.text = $"目标天数: {days}";
    }

    /// <summary>禁用被占用的存档槽。</summary>
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
                _slotGroup.value = -1;  // 取消选中
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

        var config = new NewGameConfig
        {
            rulerName = string.IsNullOrEmpty(_nameInput.value) ? "无名君主" : _nameInput.value,
            mapSeed = Random.Range(0, int.MaxValue),
            difficulty = _difficultyGroup.value,
            totalDays = _totalDaysSlider.value,
            selectedSlotId = SlotIds[_slotGroup.value]
        };

        _controller.OnCharacterCreateConfirmed(config);
    }
}
