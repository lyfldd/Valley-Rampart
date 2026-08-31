using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 作战面板（3.0.1_5 §六 P2，UI Toolkit）。
/// E 键作战面板：多编队管理（名称/阵营/意图/人数 + 选中下发军令 + 君主令）。
///
/// 挂载：GameScene 的 FormationPanelUI GameObject（UIDocument + 本组件）。
/// 刷新策略：事件驱动（FormationSelected/DeselectedEvent 刷新选中高亮）
///           + 轮询签名（编队增删/意图/人数/君主令变化，秒级轻量比对）。
/// 所属系统：FormationManager（编队注册表，纯后端只消费不改动）/ UIManager（UI 栈）。
///
/// 接入点：
///   - E 键切换开关（UIManager.Push/Pop 入栈，ESC 由 UIManager.HandleEscape 统一关闭）
///   - 选中交互：点击单选 / Ctrl+点击多选或取消 / 单边限制（我方/敌方不混选）
///   - 军令按钮：进攻/防守/撤退 → SetIntentForSelected；君主令 → SetRoyalIntent(intent, 10s)
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class FormationPanel : MonoBehaviour, IUIPanel
{
    /// <summary>君主令时长（秒，期间个体永不弃任务）。</summary>
    private const float RoyalOrderDuration = 10f;

    // ===== UI 元素引用 =====
    private UIDocument _document;
    private VisualElement _root;
    private bool _bound;                 // 防止重复绑定
    private bool _panelVisible;          // 面板是否显示（轮询开关）
    private bool _panelCentered;         // 是否已初始居中（拖拽后不再重置）

    private VisualElement _panelShell;   // 面板壳（absolute，拖拽定位目标）
    private VisualElement _titleBar;     // 标题栏（拖拽手柄）
    private VisualElement _listContainer;// 编队列表容器
    private Label _emptyHint;            // 空列表提示
    private Label _selectionInfo;        // 选中信息
    private Button _closeButton;         // 右上关闭
    private Button _clearSelectionButton;// 清空选择
    private Button _reinforceButton;     // 补充军队（对选中编队 RecruitReinforcement）
    private readonly List<Button> _orderButtons = new List<Button>();  // 普通军令按钮
    private readonly List<Button> _royalButtons = new List<Button>();  // 君主令按钮

    // ===== 标题栏拖拽状态 =====
    private bool _isDragging;
    private Vector2 _dragOffset;

    // ===== 数据缓存（轮询签名刷新） =====
    private int _lastCount = -1;         // 上次编队数
    private int _lastSignature = -1;     // 上次数据签名（意图/人数/君主令）
    private readonly Dictionary<int, VisualElement> _rows = new Dictionary<int, VisualElement>();
    private Faction? _selectionFaction;  // 当前选中集合的阵营（单边限制判断用）

    // ===== IUIPanel =====

    public void Open(Interactor ctx)
    {
        if (!_bound) Bind();
        _panelVisible = true;
        SetPanelVisible(true);
        Refresh();
    }

    public void Close()
    {
        _panelVisible = false;
        SetPanelVisible(false);
        // 关闭面板时清空选中（FormationManager.ClearSelection 注释：关闭面板时）
        if (FormationManager.Instance != null)
            FormationManager.Instance.ClearSelection();
    }

    public void Refresh()
    {
        if (!_bound) return;
        RebuildListIfNeeded();
        UpdateRows();
        RefreshSelectionUI();
    }

    // ===== 生命周期 =====

    private void OnEnable()
    {
        // 先订阅事件，确保不漏掉选中变化
        EventBus.Subscribe<FormationSelectedEvent>(OnFormationSelected);
        EventBus.Subscribe<FormationDeselectedEvent>(OnFormationDeselected);
        if (!_bound) Bind();
        _panelVisible = false;
        SetPanelVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<FormationSelectedEvent>(OnFormationSelected);
        EventBus.Unsubscribe<FormationDeselectedEvent>(OnFormationDeselected);
        Unbind();
    }

    /// <summary>UIDocument 的 rootVisualElement 可能延迟初始化，用 Start 兜底再绑一次。</summary>
    private void Start()
    {
        if (!_bound) Bind();
        SetPanelVisible(false);
    }

    private void Update()
    {
        // E 键切换作战面板（旧输入先例：AIDebugUIManager 的 F1 / CombatTestSpawner 的 1-7）
        if (Input.GetKeyDown(KeyCode.E))
        {
            TogglePanel();
        }

        // 数据变化轮询（仅面板显示时）
        RefreshIfChanged();
    }

    // ===== 面板开关（E 键 / UI 栈） =====

    /// <summary>
    /// E 键切换：栈顶是自己则 Pop 关闭；否则若栈空则 Push 打开。
    /// 其它 UI 在栈顶时（暂停/建造/调试面板）E 不抢焦点。
    /// </summary>
    private void TogglePanel()
    {
        if (UIManager.Instance == null) return;
        var top = UIManager.Instance.Peek();
        if (top == this)
        {
            UIManager.Instance.Pop();
            return;
        }
        if (top != null) return; // 其它 UI 打开中，E 不响应
        UIManager.Instance.Push(this, new Interactor(Faction.PlayerCamp, Vector3.zero));
    }

    private void OnCloseClicked()
    {
        if (UIManager.Instance != null && UIManager.Instance.Peek() == this)
            UIManager.Instance.Pop();
    }

    // ===== 事件回调（选中变化 → 刷新高亮/军令按钮） =====

    private void OnFormationSelected(FormationSelectedEvent evt)
    {
        if (_panelVisible) RefreshSelectionUI();
    }

    private void OnFormationDeselected(FormationDeselectedEvent evt)
    {
        if (_panelVisible) RefreshSelectionUI();
    }

    // ===== 轮询刷新（编队增删/意图/人数/君主令） =====

    private void RefreshIfChanged()
    {
        if (!_panelVisible || !_bound) return;
        var mgr = FormationManager.Instance;
        if (mgr == null) return;

        if (mgr.FormationCount != _lastCount)
        {
            Refresh();
            return;
        }

        int sig = ComputeSignature(mgr);
        if (sig != _lastSignature)
        {
            _lastSignature = sig;
            UpdateRows();
            RefreshSelectionUI();
        }
    }

    /// <summary>轻量签名：编队 id + 意图 + 人数 + 君主令状态（编队数少，开销可忽略）。</summary>
    private int ComputeSignature(FormationManager mgr)
    {
        int sig = 0;
        for (int i = 0; i < mgr.AllFormations.Count; i++)
        {
            var fc = mgr.AllFormations[i];
            if (fc == null) continue;
            sig = sig * 397 ^ fc.GetInstanceID();
            sig = sig * 31 ^ (int)fc.CurrentIntent;
            sig = sig * 31 ^ fc.MemberCount;
            if (fc.IsRoyalCommandActive) sig = sig * 31 ^ 0x7f;
        }
        return sig;
    }

    // ===== 编队列表构建 =====

    /// <summary>编队数变化时全量重建列表（数量少，直接 Clear + 重建）。</summary>
    private void RebuildListIfNeeded()
    {
        var mgr = FormationManager.Instance;
        if (mgr == null || _listContainer == null) return;

        if (mgr.FormationCount == _lastCount) return;
        _lastCount = mgr.FormationCount;

        _listContainer.Clear();
        _rows.Clear();
        for (int i = 0; i < mgr.AllFormations.Count; i++)
        {
            var fc = mgr.AllFormations[i];
            if (fc == null) continue;
            var row = CreateRow(fc);
            _listContainer.Add(row);
            _rows[mgr.FormationId(fc)] = row;
        }

        if (_emptyHint != null)
            _emptyHint.style.display = _lastCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private VisualElement CreateRow(FormationController fc)
    {
        var mgr = FormationManager.Instance;

        var row = new VisualElement();
        row.AddToClassList("formation-row");
        int capturedId = mgr.FormationId(fc);
        row.RegisterCallback<ClickEvent>(evt => OnRowClicked(capturedId, fc, evt));

        // 名称（锚点/将军名）
        var nameLabel = new Label { text = FormationDisplayName(fc) };
        nameLabel.AddToClassList("formation-row-name");
        row.Add(nameLabel);

        // 阵营（敌方红色）
        var factionLabel = new Label { text = FactionDisplayName(fc.faction) };
        factionLabel.AddToClassList("formation-row-faction");
        if (fc.faction == Faction.Monster)
            factionLabel.AddToClassList("formation-row-faction--enemy");
        row.Add(factionLabel);

        // 意图（含君主令标记）
        var intentLabel = new Label { text = IntentDisplayName(fc.CurrentIntent) };
        intentLabel.name = "intent";
        intentLabel.AddToClassList("formation-row-intent");
        row.Add(intentLabel);

        // 人数
        var countLabel = new Label { text = $"{fc.MemberCount} 人" };
        countLabel.name = "count";
        countLabel.AddToClassList("formation-row-count");
        row.Add(countLabel);

        return row;
    }

    /// <summary>刷新每行动态数据（意图/人数，君主令标记）。</summary>
    private void UpdateRows()
    {
        var mgr = FormationManager.Instance;
        if (mgr == null) return;

        for (int i = 0; i < mgr.AllFormations.Count; i++)
        {
            var fc = mgr.AllFormations[i];
            if (fc == null) continue;
            VisualElement row;
            if (!_rows.TryGetValue(mgr.FormationId(fc), out row)) continue;

            var intentLabel = row.Q<Label>("intent");
            if (intentLabel != null)
            {
                string mark = fc.IsRoyalCommandActive ? "⚑ " : "";
                intentLabel.text = mark + IntentDisplayName(fc.CurrentIntent);
            }
            var countLabel = row.Q<Label>("count");
            if (countLabel != null) countLabel.text = $"{fc.MemberCount} 人";
        }
    }

    // ===== 选中交互（点击单选 / Ctrl+点击多选或取消 / 单边限制） =====

    private void OnRowClicked(int id, FormationController fc, ClickEvent evt)
    {
        var mgr = FormationManager.Instance;
        if (mgr == null) return;

        bool multi = evt.ctrlKey || evt.commandKey; // Ctrl（Win）/ Command（Mac）
        if (multi)
        {
            // Ctrl+点击：多选追加 / 取消
            if (mgr.IsSelected(id))
            {
                mgr.Deselect(id);
            }
            else
            {
                // 单边限制：已有选中且阵营不同 → 拒绝混选
                if (_selectionFaction.HasValue && _selectionFaction.Value != fc.faction)
                {
                    Debug.Log($"[FormationPanel] 单边限制：不能混选（当前 {FactionDisplayName(_selectionFaction.Value)} / 点击 {FactionDisplayName(fc.faction)}）。");
                    return;
                }
                mgr.Select(id);
            }
        }
        else
        {
            // 点击单选：清空后选中（天然单边）
            mgr.ClearSelection();
            mgr.Select(id);
        }
    }

    private void OnClearSelectionClicked()
    {
        if (FormationManager.Instance != null)
            FormationManager.Instance.ClearSelection();
    }

    // ===== 补充军队（3.0.1_3 §15.4，原热键 6 功能迁入面板） =====

    /// <summary>对全部选中编队执行补员（RecruitReinforcement 走同初始招募流程，按阵营招空闲兵）。</summary>
    private void OnReinforceClicked()
    {
        var mgr = FormationManager.Instance;
        if (mgr == null || mgr.SelectedCount == 0) return;
        int count = 0;
        foreach (int id in mgr.SelectedIds)
        {
            var fc = mgr.GetById(id);
            if (fc == null) continue;
            fc.RecruitReinforcement();
            count++;
        }
        if (count > 0)
        {
            Debug.Log($"[FormationPanel] 补员下发：{count} 编队");
            Refresh(); // 人数变化立即刷新
        }
    }

    // ===== 军令下发 =====

    private void OnOrderCharge() => IssueOrder(TacticIntent.Charge);
    private void OnOrderDefense() => IssueOrder(TacticIntent.Defense);
    private void OnOrderRetreat() => IssueOrder(TacticIntent.Retreat);

    private void OnRoyalCharge() => IssueRoyalOrder(TacticIntent.Charge);
    private void OnRoyalDefense() => IssueRoyalOrder(TacticIntent.Defense);
    private void OnRoyalRetreat() => IssueRoyalOrder(TacticIntent.Retreat);

    /// <summary>普通军令：对全部选中编队下发意图。</summary>
    private void IssueOrder(TacticIntent intent)
    {
        var mgr = FormationManager.Instance;
        if (mgr == null || mgr.SelectedCount == 0) return;
        mgr.SetIntentForSelected(intent);
        Debug.Log($"[FormationPanel] 军令下发：{IntentDisplayName(intent)}（{mgr.SelectedCount} 编队）");
    }

    /// <summary>君主令：对选中编队逐个 SetRoyalIntent（期间个体永不弃任务）。</summary>
    private void IssueRoyalOrder(TacticIntent intent)
    {
        var mgr = FormationManager.Instance;
        if (mgr == null || mgr.SelectedCount == 0) return;
        int count = 0;
        foreach (int id in mgr.SelectedIds)
        {
            var fc = mgr.GetById(id);
            if (fc == null) continue;
            fc.SetRoyalIntent(intent, RoyalOrderDuration);
            count++;
        }
        if (count > 0)
            Debug.Log($"[FormationPanel] 君主令下发：{IntentDisplayName(intent)} {RoyalOrderDuration}s（{count} 编队，期间个体永不弃任务）");
    }

    // ===== 选中状态 UI =====

    /// <summary>刷新：行高亮 + 选中信息 + 军令按钮可用态 + 单边阵营记录。</summary>
    private void RefreshSelectionUI()
    {
        var mgr = FormationManager.Instance;
        if (mgr == null || _rows == null) return;

        // 记录当前选中集合阵营（单边限制依据）
        _selectionFaction = null;
        foreach (int id in mgr.SelectedIds)
        {
            var fc = mgr.GetById(id);
            if (fc == null) continue;
            if (!_selectionFaction.HasValue) _selectionFaction = fc.faction;
            else if (_selectionFaction.Value != fc.faction) _selectionFaction = null; // 理论不发生（UI 限制）
        }

        // 行高亮
        foreach (var kv in _rows)
        {
            bool selected = mgr.IsSelected(kv.Key);
            if (selected) kv.Value.AddToClassList("formation-row--selected");
            else kv.Value.RemoveFromClassList("formation-row--selected");
        }

        // 选中信息
        if (_selectionInfo != null)
        {
            if (mgr.SelectedCount > 0 && _selectionFaction.HasValue)
                _selectionInfo.text = $"已选 {mgr.SelectedCount} 编队（{FactionDisplayName(_selectionFaction.Value)}）";
            else
                _selectionInfo.text = "未选中编队";
        }

        // 军令按钮可用态（无选中禁用）
        bool hasSelection = mgr.SelectedCount > 0;
        for (int i = 0; i < _orderButtons.Count; i++)
            if (_orderButtons[i] != null) _orderButtons[i].SetEnabled(hasSelection);
        for (int i = 0; i < _royalButtons.Count; i++)
            if (_royalButtons[i] != null) _royalButtons[i].SetEnabled(hasSelection);
        if (_reinforceButton != null) _reinforceButton.SetEnabled(hasSelection);
    }

    // ===== 面板显示 / 绑定 / 解绑 =====

    private void SetPanelVisible(bool visible)
    {
        if (_root != null)
            _root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void Bind()
    {
        if (_bound) return;
        _document = GetComponent<UIDocument>();
        if (_document == null || _document.rootVisualElement == null) return;
        _root = _document.rootVisualElement;

        _panelShell = _root.Q<VisualElement>("formation-panel-root");
        _titleBar = _root.Q<VisualElement>("formation-title-bar");
        _listContainer = _root.Q<VisualElement>("formation-list");
        _emptyHint = _root.Q<Label>("formation-empty-hint");
        _selectionInfo = _root.Q<Label>("selection-info");
        _closeButton = _root.Q<Button>("formation-close-button");
        _clearSelectionButton = _root.Q<Button>("clear-selection-button");
        _reinforceButton = _root.Q<Button>("reinforce-button");

        _orderButtons.Clear();
        _orderButtons.Add(_root.Q<Button>("order-charge-button"));
        _orderButtons.Add(_root.Q<Button>("order-defense-button"));
        _orderButtons.Add(_root.Q<Button>("order-retreat-button"));
        _royalButtons.Clear();
        _royalButtons.Add(_root.Q<Button>("royal-charge-button"));
        _royalButtons.Add(_root.Q<Button>("royal-defense-button"));
        _royalButtons.Add(_root.Q<Button>("royal-retreat-button"));

        // 标题栏拖拽
        if (_titleBar != null)
        {
            _titleBar.RegisterCallback<MouseDownEvent>(OnTitleBarMouseDown);
            _titleBar.RegisterCallback<MouseMoveEvent>(OnTitleBarMouseMove);
            _titleBar.RegisterCallback<MouseUpEvent>(OnTitleBarMouseUp);
        }

        // 初始居中（布局完成后）
        if (_panelShell != null && _root != null)
        {
            _panelCentered = false;
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        // 按钮
        if (_closeButton != null) _closeButton.clicked += OnCloseClicked;
        if (_clearSelectionButton != null) _clearSelectionButton.clicked += OnClearSelectionClicked;
        if (_reinforceButton != null) _reinforceButton.clicked += OnReinforceClicked;

        BindButton(_orderButtons[0], OnOrderCharge);
        BindButton(_orderButtons[1], OnOrderDefense);
        BindButton(_orderButtons[2], OnOrderRetreat);
        BindButton(_royalButtons[0], OnRoyalCharge);
        BindButton(_royalButtons[1], OnRoyalDefense);
        BindButton(_royalButtons[2], OnRoyalRetreat);

        _bound = true;
    }

    private void BindButton(Button btn, System.Action onClick)
    {
        if (btn != null) btn.clicked += onClick;
    }

    private void Unbind()
    {
        if (!_bound) return;
        if (_titleBar != null)
        {
            _titleBar.UnregisterCallback<MouseDownEvent>(OnTitleBarMouseDown);
            _titleBar.UnregisterCallback<MouseMoveEvent>(OnTitleBarMouseMove);
            _titleBar.UnregisterCallback<MouseUpEvent>(OnTitleBarMouseUp);
        }
        if (_root != null)
            _root.UnregisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
        if (_clearSelectionButton != null) _clearSelectionButton.clicked -= OnClearSelectionClicked;
        if (_reinforceButton != null) _reinforceButton.clicked -= OnReinforceClicked;

        for (int i = 0; i < _orderButtons.Count && i < 3; i++)
        {
            UnbindButton(_orderButtons[i], i == 0 ? OnOrderCharge : (i == 1 ? OnOrderDefense : OnOrderRetreat));
        }
        for (int i = 0; i < _royalButtons.Count && i < 3; i++)
        {
            UnbindButton(_royalButtons[i], i == 0 ? OnRoyalCharge : (i == 1 ? OnRoyalDefense : OnRoyalRetreat));
        }

        _orderButtons.Clear();
        _royalButtons.Clear();
        _bound = false;
    }

    private void UnbindButton(Button btn, System.Action onClick)
    {
        if (btn != null) btn.clicked -= onClick;
    }

    // ===== 标题栏拖拽 =====

    private void OnTitleBarMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0) return;
        _isDragging = true;
        _titleBar.CaptureMouse();
        var pos = _panelShell.layout.position;
        _dragOffset = evt.mousePosition - new Vector2(pos.x, pos.y);
        evt.StopPropagation();
    }

    private void OnTitleBarMouseMove(MouseMoveEvent evt)
    {
        if (!_isDragging) return;
        _panelShell.style.left = evt.mousePosition.x - _dragOffset.x;
        _panelShell.style.top = evt.mousePosition.y - _dragOffset.y;
    }

    private void OnTitleBarMouseUp(MouseUpEvent evt)
    {
        if (!_isDragging) return;
        _isDragging = false;
        _titleBar.ReleaseMouse();
    }

    // ===== 初始居中（布局完成后，一次性） =====

    private void OnRootGeometryChanged(GeometryChangedEvent evt)
    {
        if (_panelCentered || _panelShell == null || _root == null) return;
        if (_root.layout.width > 0 && _root.layout.height > 0)
        {
            float centerX = (_root.layout.width - _panelShell.layout.width) / 2f;
            float centerY = (_root.layout.height - _panelShell.layout.height) / 2f;
            _panelShell.style.left = centerX;
            _panelShell.style.top = centerY;
            _panelCentered = true;
        }
    }

    // ===== 显示文案 =====

    /// <summary>编队名称：守城编队/将军编队 + 锚点（将军）名。</summary>
    private static string FormationDisplayName(FormationController fc)
    {
        if (fc == null) return "未知编队";
        string anchorName = fc.Anchor != null ? fc.Anchor.name : "未知锚点";
        return (fc.isGarrison ? "守城·" : "编队·") + anchorName;
    }

    private static string FactionDisplayName(Faction faction)
    {
        switch (faction)
        {
            case Faction.PlayerCamp: return "我方";
            case Faction.Monster: return "敌方";
            default: return "中立";
        }
    }

    private static string IntentDisplayName(TacticIntent intent)
    {
        switch (intent)
        {
            case TacticIntent.Charge: return "进攻";
            case TacticIntent.Defense: return "防守";
            case TacticIntent.Retreat: return "撤退";
            default: return intent.ToString();
        }
    }
}
