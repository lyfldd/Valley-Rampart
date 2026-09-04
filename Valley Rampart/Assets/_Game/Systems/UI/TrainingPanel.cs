using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 训练面板（QQQ.2 §需求3 / DR-3：不列出具体 NPC）。挂在 SampleScene 的 TrainingPanel GameObject 上（UIDocument）。
/// 实现 IUIPanel，由 BuildingPanel 的「训练」按钮 SetTarget 后 Push 入栈，关闭 Pop。
///
/// 显示三块信息：
///   1. 可训练人数（王国空闲居民数，匹配该设施起始职业）
///   2. 训练队列清单（职业名 × 数量，含排队 + 训练中）
///   3. 正在训练人数 + 时长（若有训练时长）
/// 点击「训练」弹出可训练职业选择，从居民池自动取一个入队（不列出具体 NPC）。
/// 队满 / 无可训居民时按钮置灰。
///
/// 刷新策略：事件驱动（UnitDiedEvent / RulerResourceChangedEvent）+ 训练成功后本地 Refresh。
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
    private Label _trainableLabel;       // 可训练人数
    private Label _queueLabel;           // 训练队列清单
    private Label _activeLabel;          // 正在训练人数
    private VisualElement _trainButtons; // 训练职业按钮区
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
        RebuildSummary();
        RebuildTrainButtons();
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

    // ===== 三块信息构建 =====

    /// <summary>可训练人数 + 训练队列 + 正在训练。无训练定义时显示空提示。</summary>
    private void RebuildSummary()
    {
        var ts = TrainingSystem.Instance;
        bool hasConfig = ts != null && ts.GetTrainings(_building) != null
                         && ts.GetTrainings(_building).Count > 0;

        if (!hasConfig)
        {
            SetSummaryTexts("0", "暂无训练定义", "0");
            SetSummaryVisible(false);
            if (_emptyHint != null) _emptyHint.style.display = DisplayStyle.Flex;
            return;
        }
        if (_emptyHint != null) _emptyHint.style.display = DisplayStyle.None;
        SetSummaryVisible(true);

        // 1. 可训练人数
        int trainable = ts.GetTrainableCount(_building);
        if (_trainableLabel != null) _trainableLabel.text = $"{trainable}";

        // 2. 训练队列（职业名 × 数量）
        var queue = ts.GetQueueSummary(_building);
        if (_queueLabel != null)
        {
            _queueLabel.text = queue.Count == 0 ? "空" : QueueText(queue);
        }

        // 3. 正在训练人数 + 时长
        int active = ts.GetActiveCount(_building);
        if (_activeLabel != null) _activeLabel.text = active.ToString();
    }

    /// <summary>队列文本：`工人×2 / 士兵×1` 格式。</summary>
    private static string QueueText(List<KeyValuePair<Occupation, int>> queue)
    {
        var parts = new List<string>();
        for (int i = 0; i < queue.Count; i++)
            parts.Add($"{OccName(queue[i].Key)}×{queue[i].Value}");
        return string.Join(" / ", parts);
    }

    private void SetSummaryVisible(bool visible)
    {
        var s = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_trainableLabel != null) _trainableLabel.visible = visible;
        if (_queueLabel != null) _queueLabel.visible = visible;
        if (_activeLabel != null) _activeLabel.visible = visible;
    }

    private void SetSummaryTexts(string trainable, string queue, string active)
    {
        if (_trainableLabel != null) _trainableLabel.text = trainable;
        if (_queueLabel != null) _queueLabel.text = queue;
        if (_activeLabel != null) _activeLabel.text = active;
    }

    // ===== 训练职业按钮区 =====

    /// <summary>重建「可训练职业」按钮列表；点按钮从居民池自动入队。</summary>
    private void RebuildTrainButtons()
    {
        if (_trainButtons == null) return;
        _trainButtons.Clear();

        var ts = TrainingSystem.Instance;
        if (ts == null) return;

        var occs = ts.GetSupportedOccupations(_building);
        int trainable = ts.GetTrainableCount(_building);
        bool queueFull = ts.GetQueueSummary(_building).Count >= _building.def.trainingSlots
                         && ts.GetActiveCount(_building) >= _building.def.trainingSlots;

        if (occs == null || occs.Length == 0)
        {
            var none = new Label { text = "无可训练职业" };
            none.AddToClassList("training-card-empty");
            _trainButtons.Add(none);
            return;
        }

        foreach (var occ in occs)
        {
            var row = new VisualElement();
            row.AddToClassList("training-unit-row");

            float dur = ts.GetTrainDuration(_building, occ);
            var name = new Label { text = $"{OccName(occ)}（{dur:F0} 天）" };
            name.AddToClassList("training-unit-name");
            row.Add(name);

            var btn = new Button(() => OnTrainOccupationClicked(occ)) { text = "训练" };
            btn.AddToClassList("training-btn");
            // 队满 / 无可训居民时置灰
            btn.SetEnabled(trainable > 0 && !queueFull && CanAffordAny());
            row.Add(btn);
            _trainButtons.Add(row);
        }
    }

    /// <summary>点目标职业「训练」：从居民池自动取一个入队。</summary>
    private void OnTrainOccupationClicked(Occupation occ)
    {
        if (TrainingSystem.Instance == null) return;
        if (TrainingSystem.Instance.TryTrainFromPool(_building, occ))
            Refresh();
        else
            Debug.Log("[TrainingPanel] 训练失败（无可训居民 / 队满 / 资源不足）");
    }

    // ===== 按钮绑定 / 解绑 =====

    private void Bind()
    {
        if (_bound) return;
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        _root = doc.rootVisualElement;

        _titleLabel = _root.Q<Label>("training-title");
        _trainableLabel = _root.Q<Label>("training-trainable-value");
        _queueLabel = _root.Q<Label>("training-queue-value");
        _activeLabel = _root.Q<Label>("training-active-value");
        _trainButtons = _root.Q<VisualElement>("training-train-buttons");
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

    /// <summary>当前持有资源是否足够任意训练（粗略：金 > 0 任一训练所需）。</summary>
    private static bool CanAffordAny()
    {
        var ruler = RulerController.Instance;
        return ruler != null && ruler.Gold > 0;
    }

    /// <summary>职业中文显示名（默认回退 ToString）。</summary>
    private static string OccName(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.Resident: return "居民";
            case Occupation.Worker: return "工人";
            case Occupation.Porter: return "搬运工";
            case Occupation.Vagrant: return "流浪汉";
            case Occupation.Child: return "小孩";
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
            // ===== 2_20 M7 专属兵种+机器（D490~D497）=====
            case Occupation.Berserker: return "狂战士";
            case Occupation.WolfRider: return "狼骑兵";
            case Occupation.Musqueteer: return "火枪手";
            case Occupation.Bedrock: return "磐石卫士";
            case Occupation.Ranger: return "游侠";
            case Occupation.Windwalker: return "风行者";
            case Occupation.DeerRider: return "鹿骑";
            case Occupation.Mortar: return "臼炮";
            case Occupation.VineCatapult: return "藤蔓弹射器";
            case Occupation.Ram: return "攻城槌";
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