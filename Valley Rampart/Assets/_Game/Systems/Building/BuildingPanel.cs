using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 建筑面板（3.3 第七节）。挂在 SampleScene 的 BuildingPanel GameObject 上（UIDocument）。
/// 实现 IUIPanel，由 InteractionManager 通过 UIManager 打开。
/// 显示：名称/等级/HP/攻防/产能/描述 + 升级/拆除按钮（按字段条件渲染，无则隐藏）。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class BuildingPanel : MonoBehaviour, IUIPanel
{
    private Building _target;
    private bool _buttonsBound;

    // ===== UI 元素引用 =====
    private Label _nameLabel;
    private Label _levelLabel;
    private VisualElement _hpFill;
    private Label _hpText;
    private VisualElement _descBlock;
    private Label _descLabel;
    private VisualElement _combatRow;
    private Label _attackValue;
    private Label _defenseValue;
    private Label _rangeValue;
    private VisualElement _producerRow;
    private Label _producerRate;
    private Label _producerCap;
    private Label _footprintValue;
    private Label _factionValue;
    private Label _obstacleValue;
    private Button _upgradeButton;
    private Button _demolishButton;
    private Button _closeButton;
    private Button _harvestButton;

    // ===== IUIPanel =====

    public void Open(Interactor ctx)
    {
        if (!_buttonsBound) BindButtons();
        Refresh();
        SetVisible(true);
    }

    public void Close()
    {
        SetVisible(false);
        _target = null;
    }

    public void Refresh()
    {
        if (_target == null || _target.def == null) return;

        var def = _target.def;

        // 标题：废弃态显示"废弃城堡"，修复后(Active)显示 def.displayName（主城）
        if (_nameLabel != null)
            _nameLabel.text = _target.state == BuildingState.Abandoned ? "废弃城堡" : def.displayName;

        // 修复按钮（Abandoned 态，复用升级按钮；3.3.4 批次7）
        if (_target.state == BuildingState.Abandoned)
        {
            if (_upgradeButton != null)
            {
                _upgradeButton.style.display = DisplayStyle.Flex;
                var repairCost = def.cost;
                _upgradeButton.text = $"修复 (金{repairCost.gold} 石{repairCost.stone} 木{repairCost.wood} 粮{repairCost.food})";
                _upgradeButton.SetEnabled(RulerController.Instance != null && RulerController.Instance.CanAfford(repairCost));
            }
            if (_demolishButton != null) _demolishButton.style.display = DisplayStyle.None;
            return;
        }

        if (_levelLabel != null) _levelLabel.text = $"Lv.{_target.level}";

        // HP
        float hpRatio = _target.maxHp > 0 ? (float)_target.hp / _target.maxHp : 0f;
        if (_hpFill != null) _hpFill.style.width = new StyleLength(new Length(Mathf.Clamp01(hpRatio) * 100, LengthUnit.Percent));
        if (_hpText != null) _hpText.text = $"{_target.hp}/{_target.maxHp}";

        // 描述（条件渲染）
        bool hasDesc = !string.IsNullOrEmpty(def.description);
        if (_descBlock != null) _descBlock.style.display = hasDesc ? DisplayStyle.Flex : DisplayStyle.None;
        if (_descLabel != null && hasDesc) _descLabel.text = def.description;

        // 攻防行（条件渲染：有 combat.attack 或 combat.defense 或 combat.range）
        bool hasCombat = def.combat.attack > 0 || def.combat.defense > 0 || def.combat.range > 0f;
        if (_combatRow != null) _combatRow.style.display = hasCombat ? DisplayStyle.Flex : DisplayStyle.None;
        if (_attackValue != null) _attackValue.text = def.combat.attack > 0 ? def.combat.attack.ToString() : "-";
        if (_defenseValue != null) _defenseValue.text = def.combat.defense > 0 ? def.combat.defense.ToString() : "-";
        if (_rangeValue != null) _rangeValue.text = def.combat.range > 0f ? def.combat.range.ToString("F1") : "-";

        // 产能行（条件渲染）
        bool hasProducer = def.producer.rate > 0f || def.producer.capacity > 0;
        if (_producerRow != null) _producerRow.style.display = hasProducer ? DisplayStyle.Flex : DisplayStyle.None;
        if (_producerRate != null) _producerRate.text = def.producer.rate > 0f ? $"{def.producer.rate:F1}/s" : "-";
        if (_producerCap != null) _producerCap.text = def.producer.capacity > 0 ? def.producer.capacity.ToString() : "-";

        // 基础信息
        if (_footprintValue != null) _footprintValue.text = $"{_target.cellWidth}格";
        if (_factionValue != null) _factionValue.text = FactionDisplayName(def.faction);
        if (_obstacleValue != null) _obstacleValue.text = _target.isObstacle ? "是" : "否";

        // 升级按钮（条件渲染：玩家建造 + 有 levels + 未满级）
        bool canUpgrade = _target.isPlayerBuilt
                          && def.levels != null
                          && def.levels.Length > 0
                          && _target.level - 1 < def.levels.Length;
        if (_upgradeButton != null)
        {
            _upgradeButton.style.display = canUpgrade ? DisplayStyle.Flex : DisplayStyle.None;
            if (canUpgrade)
            {
                var lvCost = def.levels[_target.level - 1].upgradeCost;
                _upgradeButton.text = $"升级 (金{lvCost.gold} 石{lvCost.stone} 木{lvCost.wood} 粮{lvCost.food})";
                _upgradeButton.SetEnabled(RulerController.Instance != null && RulerController.Instance.CanAfford(lvCost));
            }
        }

        // 拆除按钮（条件渲染：玩家建造 + 可拆）
        bool canDemolish = _target.isPlayerBuilt && def.isDestructible;
        if (_demolishButton != null)
            _demolishButton.style.display = canDemolish ? DisplayStyle.Flex : DisplayStyle.None;

        // 收取按钮（条件渲染：有 StorageComponent 且可收取，3.3.4 批次5）
        var storage = _target.GetComponent<StorageComponent>();
        if (_harvestButton != null)
        {
            bool canHarvest = storage != null && storage.IsReadyToHarvest();
            _harvestButton.style.display = canHarvest ? DisplayStyle.Flex : DisplayStyle.None;
            if (canHarvest)
                _harvestButton.text = $"收取 {storage.storedAmount}/{storage.capacity}";
        }
    }

    // ===== 对外 API（由 Building.Interact → InteractionManager 调用 SetTarget → Open）=====

    /// <summary>设置面板目标建筑。由 InteractionManager 在打开面板前调用。</summary>
    public void SetTarget(Building b)
    {
        _target = b;
    }

    /// <summary>当前目标（用于调试/扩展）。</summary>
    public Building CurrentTarget => _target;

    // ===== Unity 生命周期 =====

    private void OnEnable()
    {
        EventBus.Subscribe<BuildingUpgradedEvent>(OnBuildingUpgraded);
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
        if (!_buttonsBound) BindButtons();
        SetVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BuildingUpgradedEvent>(OnBuildingUpgraded);
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        UnbindButtons();
    }

    private void Start()
    {
        if (!_buttonsBound) BindButtons();
    }

    // ===== 按钮绑定 =====

    private void BindButtons()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        _nameLabel = root.Q<Label>("building-name");
        _levelLabel = root.Q<Label>("building-level");
        _hpFill = root.Q<VisualElement>("hp-bar-fill");
        _hpText = root.Q<Label>("hp-text");
        _descBlock = root.Q<VisualElement>("desc-block");
        _descLabel = root.Q<Label>("building-desc");
        _combatRow = root.Q<VisualElement>("combat-row");
        _attackValue = root.Q<Label>("attack-value");
        _defenseValue = root.Q<Label>("defense-value");
        _rangeValue = root.Q<Label>("range-value");
        _producerRow = root.Q<VisualElement>("producer-row");
        _producerRate = root.Q<Label>("producer-rate");
        _producerCap = root.Q<Label>("producer-cap");
        _footprintValue = root.Q<Label>("footprint-value");
        _factionValue = root.Q<Label>("faction-value");
        _obstacleValue = root.Q<Label>("obstacle-value");

        _upgradeButton = root.Q<Button>("upgrade-button");
        _demolishButton = root.Q<Button>("demolish-button");
        _closeButton = root.Q<Button>("close-button");

        if (_upgradeButton != null) _upgradeButton.clicked += OnUpgradeClicked;
        if (_demolishButton != null) _demolishButton.clicked += OnDemolishClicked;
        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

        // 动态创建收取按钮（UXML 未预留，3.3.4 批次5）
        if (_harvestButton == null)
        {
            _harvestButton = new Button(OnHarvestClicked) { name = "harvest-button", text = "收取" };
            // 加到拆除按钮的父级（按钮容器），否则回退到 root
            var host = _demolishButton != null ? _demolishButton.parent : root;
            host?.Add(_harvestButton);
            _harvestButton.style.display = DisplayStyle.None;
        }

        _buttonsBound = true;
    }

    private void UnbindButtons()
    {
        if (!_buttonsBound) return;
        if (_upgradeButton != null) _upgradeButton.clicked -= OnUpgradeClicked;
        if (_demolishButton != null) _demolishButton.clicked -= OnDemolishClicked;
        if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
        if (_harvestButton != null) _harvestButton.clicked -= OnHarvestClicked;
        _buttonsBound = false;
    }

    // ===== 事件回调 =====

    private void OnBuildingUpgraded(BuildingUpgradedEvent evt)
    {
        if (evt.Building == _target) Refresh();
    }

    // 3.4：改订阅 UnitDiedEvent（BuildingDestroyedEvent 退役）。
    // 建筑被击杀/拆除都关面板，不过滤 Cause。
    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (evt.Unit as Building == _target) Close();
    }

    private void OnUpgradeClicked()
    {
        if (_target == null || _target.def == null) return;

        // 修复废弃主城（3.3.4 批次7）
        if (_target.state == BuildingState.Abandoned)
        {
            var repairCost = _target.def.cost;
            if (RulerController.Instance == null || !RulerController.Instance.CanAfford(repairCost))
            {
                Debug.Log("[BuildingPanel] 资源不足，无法修复");
                return;
            }
            RulerController.Instance.Spend(repairCost);
            _target.StartConstructing();
            UIManager.Instance?.Pop();
            return;
        }

        if (!_target.isPlayerBuilt || _target.def.levels == null || _target.def.levels.Length == 0) return;
        if (_target.level - 1 >= _target.def.levels.Length) return;

        var lvCost = _target.def.levels[_target.level - 1].upgradeCost;
        if (RulerController.Instance == null || !RulerController.Instance.CanAfford(lvCost))
        {
            Debug.Log("[BuildingPanel] 资源不足，无法升级");
            return;
        }

        RulerController.Instance.Spend(lvCost);
        if (_target.TryUpgrade())
        {
            // 升级走 Constructing 进度，关闭面板（3.3.4 批次3）
            UIManager.Instance?.Pop();
        }
    }

    private void OnDemolishClicked()
    {
        if (_target == null || _target.def == null) return;
        if (!_target.isPlayerBuilt || !_target.def.isDestructible) return;

        // Demolish 内部按 HP 比例返还 + Die（3.3.4 批次3）
        _target.Demolish();
        UIManager.Instance?.Pop();  // 出栈关闭面板
    }

    private void OnHarvestClicked()
    {
        if (_target == null) return;
        var storage = _target.GetComponent<StorageComponent>();
        if (storage == null || !storage.IsReadyToHarvest()) return;
        storage.Harvest();  // 内部调 RulerController.ModifyResource 转入国库
        Refresh();
    }

    private void OnCloseClicked()
    {
        UIManager.Instance?.CloseCurrent();
    }

    // ===== 辅助 =====

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private static string FactionDisplayName(Faction f)
    {
        switch (f)
        {
            case Faction.Human_Player: return "我方";
            case Faction.Undead: return "亡灵";
            case Faction.None: return "中立";
            default: return f.ToString();
        }
    }
}
