using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 角色创建面板 UI 绑定。挂在 CharacterCreationUI GameObject 上。
/// 收集种族/名字/难度，调 MainMenuController 开始新游戏。
/// 难度档位 1/2/3（Easy/Normal/Hard），资源由 WorldConfig 按难度算，不再在此面板预设。
/// 存档槽自动分配第一个空槽（进入本面板前 MainMenuController 已确保有空槽）。
/// 选族（2_20 M10 / D431，HH.66 段A）：静态四张选族卡（UXML 写死），运行时从 RaceDef 四资产
/// 读族名/描述/主色渲染（KingdomRace.GetRaceDef 统一入口，D420 防散落 Resources.Load）；
/// 点击卡=选中态高亮+记录 _selectedRaceId，确认时填 NewGameConfig.raceId 标准桥。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class CharacterCreationPanel : MonoBehaviour
{
    public static readonly string[] SlotIds = { "slot_1", "slot_2", "slot_3" };

    private MainMenuController _controller;
    private TextField _kingdomNameInput;
    private DropdownField _difficultySelect;
    private DropdownField _mapSizeSelect;
    private IntegerField _worldSeedInput;

    private readonly Button[] _raceCards = new Button[4];
    private readonly System.Action[] _raceCardHandlers = new System.Action[4];   // 委托引用（lambda 退订必须同实例）
    private int _selectedRaceId = RaceIds.Human;   // 默认选中人类（RaceIds.Human=0）

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

        _kingdomNameInput = root.Q<TextField>("kingdom-name-input");
        _difficultySelect = root.Q<DropdownField>("difficulty-select");
        _mapSizeSelect = root.Q<DropdownField>("map-size-select");
        _worldSeedInput = root.Q<IntegerField>("world-seed-input");

        // 默认值：索引 1 = "普通"（档位 2）
        if (_difficultySelect != null)
        {
            _difficultySelect.value = "普通";
        }

        // ===== M10 选族卡真数据渲染+点击绑定（RaceDef 四资产 → 卡片）=====
        BindRaceCards(root);

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
                var root = doc.rootVisualElement;
                root.Q<Button>("creation-back-button").clicked -= _controller.OnCharacterCreationBackClicked;
                root.Q<Button>("creation-confirm-button").clicked -= OnConfirmClicked;
                for (int i = 0; i < _raceCards.Length; i++)
                {
                    if (_raceCards[i] != null && _raceCardHandlers[i] != null)
                        _raceCards[i].clicked -= _raceCardHandlers[i];
                }
            }
            _buttonsBound = false;
        }
    }

    /// <summary>
    /// 选族卡绑定：静态四卡（race-card-0~3）读 RaceDef 真数据渲染（族名/描述/主色）+绑点击。
    /// 资产缺失 → 沿用 UXML 静态文本兜底（族名不空，描述空），主色灰占位（不做 null 炸）。
    /// </summary>
    private void BindRaceCards(VisualElement root)
    {
        for (int i = 0; i < _raceCards.Length; i++)
        {
            int raceId = i;   // 闭包捕获（foreach 变量陷阱对齐）
            var card = root.Q<Button>($"race-card-{i}");
            _raceCards[i] = card;
            if (card == null) continue;

            var def = KingdomRace.GetRaceDef(i);
            if (def != null)
            {
                var nameLabel = card.Q<Label>($"race-name-{i}");
                if (nameLabel != null && !string.IsNullOrEmpty(def.raceName)) nameLabel.text = def.raceName;
                var descLabel = card.Q<Label>($"race-desc-{i}");
                if (descLabel != null) descLabel.text = def.raceDescription ?? string.Empty;
                var banner = card.Q<VisualElement>($"race-banner-{i}");
                if (banner != null) banner.style.backgroundColor = def.bannerColor;
            }

            _raceCardHandlers[i] = () => OnRaceCardClicked(raceId);
            card.clicked += _raceCardHandlers[i];
        }
        SelectRaceCard(_selectedRaceId);   // 恢复/初始选中态（默认人类）
    }

    /// <summary>点击选族卡：更新选中态高亮+记录 raceId（NewGameConfig.raceId 标准桥数据源）。</summary>
    private void OnRaceCardClicked(int raceId)
    {
        SelectRaceCard(raceId);
        var def = KingdomRace.GetRaceDef(raceId);
        Debug.Log($"[CharacterCreation] 选族：raceId={raceId}（{(def != null ? def.raceName : raceId.ToString())}）");
    }

    /// <summary>选中态渲染：目标卡加 race-card--selected，其余移除。</summary>
    private void SelectRaceCard(int raceId)
    {
        _selectedRaceId = raceId;
        for (int i = 0; i < _raceCards.Length; i++)
        {
            if (_raceCards[i] == null) continue;
            if (i == raceId) _raceCards[i].AddToClassList("race-card--selected");
            else _raceCards[i].RemoveFromClassList("race-card--selected");
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
            raceId = _selectedRaceId,   // M10 选族卡选中值（2_20 M10 / D431；NewGameConfig 标准桥，D520）
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
