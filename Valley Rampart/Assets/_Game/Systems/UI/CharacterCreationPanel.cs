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
    private DropdownField _raceSelect;
    private TextField _kingdomNameInput;
    private DropdownField _difficultySelect;
    private DropdownField _mapSizeSelect;
    private IntegerField _worldSeedInput;

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

        _raceSelect = root.Q<DropdownField>("race-select");
        _kingdomNameInput = root.Q<TextField>("kingdom-name-input");
        _difficultySelect = root.Q<DropdownField>("difficulty-select");
        _mapSizeSelect = root.Q<DropdownField>("map-size-select");
        _worldSeedInput = root.Q<IntegerField>("world-seed-input");

        // 默认值：索引 1 = "普通"（档位 2）
        if (_difficultySelect != null)
        {
            _difficultySelect.value = "普通";
        }

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

    /// <summary>将难度文字映射为档位数字（1=简单, 2=普通, 3=困难）</summary>
    private int DifficultyTextToValue(string text)
    {
        return text switch
        {
            "简单" => 1,
            "困难" => 3,
            _ => 2  // 默认"普通"
        };
    }

    /// <summary>将地图大小文字映射为枚举</summary>
    private WorldSize MapSizeTextToValue(string text)
    {
        return text switch
        {
            "小" => WorldSize.Small,
            "大" => WorldSize.Large,
            _ => WorldSize.Medium  // 默认"中"
        };
    }

    /// <summary>
    /// 将种族文字映射为 raceId 索引（2_13 M10 / D431 UI 侧；0=人类,1=精灵,2=矮人,3=兽人）。
    /// UI 暂存口径：RaceDef SO 未建（2_20 Q10-M1 让渡），2_16 kingdomSpawns 激活时定族消费。
    /// </summary>
    private int RaceTextToValue(string text)
    {
        return text switch
        {
            "精灵" => 1,
            "矮人" => 2,
            "兽人" => 3,
            _ => 0  // 默认"人类"
        };
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

        string kingdomName = string.IsNullOrEmpty(_kingdomNameInput.value) ? "河谷王国" : _kingdomNameInput.value;
        int difficulty = DifficultyTextToValue(_difficultySelect?.value ?? "普通");
        WorldSize worldSize = MapSizeTextToValue(_mapSizeSelect?.value ?? "中");
        int worldSeed = _worldSeedInput?.value ?? 0;

        var config = new NewGameConfig
        {
            kingdomName = kingdomName,
            raceId = RaceTextToValue(_raceSelect?.value ?? "人类"),   // M10 选族暂存（D431 UI 侧；2_16 激活时定族消费）
            difficulty = difficulty,
            selectedSlotId = slotId,
            worldSeed = worldSeed,
            worldSize = worldSize,
            mapSeed = worldSeed != 0 ? worldSeed : UnityEngine.Random.Range(1, int.MaxValue)
        };

        Debug.Log($"[CharacterCreation] 新建游戏配置：kingdom={kingdomName}, raceId={config.raceId}, difficulty={difficulty}");
        _controller.OnCharacterCreateConfirmed(config);
    }
}