using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 角色创建面板 UI 绑定。挂在 CharacterCreationUI GameObject 上。
/// 收集名字/难度，调 MainMenuController 开始新游戏。
/// 难度档位 1/2/3（Easy/Normal/Hard），资源由 WorldConfig 按难度算，不再在此面板预设。
/// 存档槽自动分配第一个空槽（进入本面板前 MainMenuController 已确保有空槽）。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class CharacterCreationPanel : MonoBehaviour
{
    public static readonly string[] SlotIds = { "slot_1", "slot_2", "slot_3" };

    private MainMenuController _controller;
    private TextField _nameInput;
    private RadioButtonGroup _difficultyGroup;

    private bool _buttonsBound;

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

        // 默认值：难度索引 1 = Normal（档位 2）
        _difficultyGroup.value = 1;

        // 按钮（仅首次绑定时注册，OnDisable 里退订）
        if (!_buttonsBound)
        {
            root.Q<Button>("creation-back-button").clicked += _controller.OnCharacterCreationBackClicked;
            root.Q<Button>("creation-confirm-button").clicked += OnConfirmClicked;
            _buttonsBound = true;
        }
    }

    private void OnDisable()
    {
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

    /// <summary>自动选择第一个未占用的存档槽 ID。无空槽返回 null。</summary>
    private string FindEmptySlot()
    {
        foreach (var id in SlotIds)
        {
            if (!SaveManager.Instance.HasSave(id)) return id;
        }
        return null;
    }

    private void OnConfirmClicked()
    {
        // 自动分配第一个空存档槽（进入本面板前 MainMenuController 已校验有空槽）
        string slotId = FindEmptySlot();
        if (slotId == null)
        {
            Debug.LogWarning("[CharacterCreation] 所有存档槽已占用，无法创建新游戏。请先删除一个存档。");
            return;
        }

        string name = string.IsNullOrEmpty(_nameInput.value) ? "无名君主" : _nameInput.value;
        // RadioButtonGroup.value 是选中索引（0/1/2），转为难度档位 1/2/3
        int difficulty = _difficultyGroup.value + 1;

        var config = new NewGameConfig
        {
            rulerName = name,
            difficulty = difficulty,
            selectedSlotId = slotId,
            mapSeed = UnityEngine.Random.Range(1, int.MaxValue)
        };

        _controller.OnCharacterCreateConfirmed(config);
    }
}
