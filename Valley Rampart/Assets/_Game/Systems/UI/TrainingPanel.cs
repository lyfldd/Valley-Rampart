using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 训练面板（3.5 训练系统 UI）。挂在 SampleScene 的 TrainingPanel GameObject 上（UIDocument）。
/// 实现 IUIPanel，由 BuildingPanel 的「训练」按钮 SetTarget 后 Push 入栈，关闭 Pop。
///
/// 显示：训练设施名 + 等级；该设施可训练项列表（目标职业 + 消耗金/水晶 + 时长）；
///       每项下列出当前符合条件的单位（当前职业），每个单位一个「训练」按钮。
/// 无训练定义或无人可训时显示空提示。
///
/// 刷新策略：事件驱动（UnitDiedEvent / RulerResourceChangedEvent）+ 训练成功后本地 Refresh。
/// 所属系统：TrainingSystem（转职）/ UnitRegistry（可训单位）/ RulerController（资源）。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class TrainingPanel : MonoBehaviour, IUIPanel
{
    private Building _building;          // 所属训练设施（提供 def.id / 等级）
    private bool _bound;                 // 防止重复绑定
    private bool _visible;               // 面板是否显示（事件回调里判断）

    // ===== UI 元素引用 =====
    private VisualElement _root;
    private Label _titleLabel;
    private VisualElement _trainingList;
    private Label _emptyHint;
    private Button _closeButton;

    // ===== 对外 API（由 BuildingPanel.OnTrainingClicked 调 SetTarget → Push）=====

    /// <summary>设置所属训练设施（提供 def.id / 等级）。打开前调用。</summary>
    public void SetTarget(Building building)
    {
        _building = building;
    }

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
        _building = null;
    }

    public void Refresh()
    {
        if (_building == null || _building.def == null) return;
        if (_titleLabel != null)
            _titleLabel.text = $"{_building.def.displayName}（Lv.{_building.level}）";
        RebuildTrainingList();
    }

    // ===== Unity 生命周期 =====

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
        EventBus.Subscribe<RulerResourceChangedEvent>(OnResourceChanged);
        if (!_bound) Bind();
        SetVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        EventBus.Unsubscribe<RulerResourceChangedEvent>(OnResourceChanged);
        Unbind();
    }

    private void Start()
    {
        if (!_bound) Bind();
    }

    // ===== 事件回调（单位死亡 / 资源变化 → 重建列表）=====

    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (_visible) Refresh();
    }

    private void OnResourceChanged(RulerResourceChangedEvent evt)
    {
        if (_visible) Refresh();
    }

    // ===== 列表构建 =====

    /// <summary>重建训练项列表；无项时显示空提示。</summary>
    private void RebuildTrainingList()
    {
        if (_trainingList == null) return;
        _trainingList.Clear();

        var ts = TrainingSystem.Instance;
        var trainings = ts != null ? ts.GetTrainings(_building.def.id) : null;
        bool any = false;

        if (trainings != null && trainings.Count > 0)
        {
            foreach (var def in trainings)
            {
                var card = CreateTrainingCard(def);
                if (card != null)
                {
                    _trainingList.Add(card);
                    any = true;
                }
            }
        }

        if (_emptyHint != null)
            _emptyHint.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
    }

    /// <summary>创建单个训练项卡片（目标职业 + 消耗 + 可训单位行）。</summary>
    private VisualElement CreateTrainingCard(TrainingDef def)
    {
        var card = new VisualElement();
        card.AddToClassList("training-card");

        // 头部：目标职业 + 时长
        var header = new VisualElement();
        header.AddToClassList("training-card-header");
        var toName = new Label { text = OccName(def.toOccupation) };
        toName.AddToClassList("training-card-title");
        var days = new Label { text = $"{def.costDays} 天" };
        days.AddToClassList("training-card-days");
        header.Add(toName);
        header.Add(days);
        card.Add(header);

        // 消耗
        var cost = new Label { text = CostText(def) };
        cost.AddToClassList("training-card-cost");
        card.Add(cost);

        // 可训单位（无则显示空提示）
        var eligible = GetEligibleUnits(def);
        if (eligible.Count == 0)
        {
            var none = new Label { text = "暂无符合条件的单位" };
            none.AddToClassList("training-card-empty");
            card.Add(none);
            return card;
        }

        var unitsHost = new VisualElement();
        unitsHost.AddToClassList("training-card-units");
        foreach (var unit in eligible)
            unitsHost.Add(CreateUnitRow(unit, def));
        card.Add(unitsHost);
        return card;
    }

    /// <summary>收集某项训练当前符合条件的单位：我方 + 起始职业匹配 + 存活。</summary>
    private List<UnitController> GetEligibleUnits(TrainingDef def)
    {
        var result = new List<UnitController>();
        if (UnitRegistry.Instance == null) return result;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.faction != Faction.Human_Player) continue;
            if (unit.EffectiveOccupation != def.fromOccupation) continue;
            if (!unit.IsAlive) continue;
            result.Add(unit);
        }
        return result;
    }

    /// <summary>创建单个可训单位行：当前职业名 + 「训练」按钮。</summary>
    private VisualElement CreateUnitRow(UnitController unit, TrainingDef def)
    {
        var row = new VisualElement();
        row.AddToClassList("training-unit-row");

        var name = new Label { text = OccName(unit.EffectiveOccupation) };
        name.AddToClassList("training-unit-name");
        row.Add(name);

        var btn = new Button(() => OnTrainClicked(unit, def)) { text = "训练" };
        btn.AddToClassList("training-btn");
        btn.SetEnabled(CanAfford(def));   // 资源不足禁用
        row.Add(btn);
        return row;
    }

    // ===== 训练操作 =====

    private void OnTrainClicked(UnitController unit, TrainingDef def)
    {
        if (TrainingSystem.Instance == null) return;
        if (TrainingSystem.Instance.TryTrain(unit, def))
            Refresh();
        else
            Debug.Log("[TrainingPanel] 训练失败（起始职业不符 / 资源不足 / 将军已达上限）");
    }

    // ===== 按钮绑定 / 解绑 =====

    private void Bind()
    {
        if (_bound) return;
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        _root = doc.rootVisualElement;

        _titleLabel = _root.Q<Label>("training-title");
        _trainingList = _root.Q<VisualElement>("training-list");
        _emptyHint = _root.Q<Label>("training-empty-hint");
        _closeButton = _root.Q<Button>("training-close-button");

        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;

        // 标题栏拖动（可拖动窗口，不破坏关闭按钮点击）
        var panel = _root.Q<VisualElement>("training-panel");
        var handle = _root.Q<VisualElement>("drag-handle");
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

    // ===== 辅助 =====

    /// <summary>训练消耗文本（金 + 可选水晶）。</summary>
    private static string CostText(TrainingDef def)
    {
        string s = $"消耗 金 {def.costGold}";
        if (def.costCrystal > 0) s += $"  水晶 {def.costCrystal}";
        return s;
    }

    /// <summary>当前持有资源是否足够某项训练。</summary>
    private static bool CanAfford(TrainingDef def)
    {
        var ruler = RulerController.Instance;
        if (ruler == null) return false;
        if (ruler.Gold < def.costGold) return false;
        if (def.costCrystal > 0 && ruler.GetResource(ResourceType.Crystal) < def.costCrystal) return false;
        return true;
    }

    /// <summary>职业中文显示名（默认回退 ToString）。</summary>
    private static string OccName(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.Unemployed: return "无职业";
            case Occupation.Worker: return "工人";
            case Occupation.Porter: return "搬运工";
            case Occupation.General: return "将军";
            case Occupation.Archer: return "弓箭手";
            case Occupation.Warrior: return "战士";
            case Occupation.Civilian: return "平民";
            case Occupation.Mage: return "法师";
            case Occupation.Healer: return "治疗师";
            case Occupation.Crossbowman: return "弩手";
            case Occupation.HeavyWarrior: return "重装战士";
            case Occupation.Bishop: return "主教";
            case Occupation.ShieldGuard: return "盾卫";
            case Occupation.Archmage: return "大法师";
            case Occupation.Cavalry: return "骑兵";
            default: return occ.ToString();
        }
    }

    private void SetVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}