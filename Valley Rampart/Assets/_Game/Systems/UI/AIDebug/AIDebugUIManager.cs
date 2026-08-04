using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// AI 调试面板 UI 管理器（3.0.1 附录 A / 3.0.1_2）。
///
/// 职责：
///   1. F1 切换调试模式
///   2. 调试模式下显示屏幕中间的开发者面板
///   3. 点击"选择NPC"按钮后，点击场景中的 NPC 锁定深查
///   4. 选中 NPC 后，左上角显示 AI 状态面板，每帧刷新
///   5. ESC 清除选择，再次 F1 退出调试模式
///
/// 挂在 GameScene 的 AIDebugUI GameObject 上（需手动添加 UIDocument 组件）。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class AIDebugUIManager : MonoBehaviour
{
    [Header("UI 引用")]
    private UIDocument _document;
    private VisualElement _root;

    // 屏幕中间开发者面板
    private VisualElement _devPanelRoot;
    private VisualElement _devTitleBar;
    private Button _devCloseButton;
    private Button _selectNpcButton;

    // 开发者面板拖拽状态
    private bool _isDevDragging;
    private Vector2 _devDragOffset;

    // Tab 切换
    private VisualElement _tabAIDebug;
    private VisualElement _tabSpawn;
    private VisualElement _tabFormation;
    private VisualElement _aiDebugContent;
    private VisualElement _spawnContent;
    private VisualElement _spawnButtonsContainer;   // 兼容引用（旧容器，已拆分为阵营两组）
    private VisualElement _allySpawnButtons;        // 己方单位按钮组（固定窗口滚动）
    private VisualElement _enemySpawnButtons;       // 敌方单位按钮组（固定窗口滚动）
    private Label _spawnHintText;
    private VisualElement _formationContent;
    private Button _garrisonButton;      // 守城编队（热键 4）
    private Button _disbandButton;       // 解散将军编队（热键 5）
    private Button _killTestButton;      // 残编测试（热键 7）

    // 放置模式状态
    private bool _isSpawnMode = false;
    private bool _isSpawnTabActive = false;      // 当前是否激活"放置士兵"Tab
    private bool _isFormationTabActive = false;  // 当前是否激活"编队操作"Tab
    private DebugSpawnType? _selectedSpawnType = null;

    // 左上角 AI 状态面板
    private VisualElement _statusPanelRoot;
    private VisualElement _titleBar;
    private Label _panelTitle;
    private Label _npcNameLabel;
    private Label _collapseIcon;
    private Button _closeButton;
    private VisualElement _contentArea;

    // 拖拽状态
    private bool _isDragging;
    private bool _wasDragging;  // 本次按下是否发生了拖拽（用于区分点击和拖拽）
    private Vector2 _dragOffset;
    private Vector2 _dragStartPos;

    // 焦点区域
    private Label _focusLayerLabel;
    private Label _focusIntensityLabel;
    private Label _focusPositionLabel;

    // 谱系区域
    private VisualElement _spectrumColor;
    private Label _spectrumNameLabel;

    // 威胁区域
    private VisualElement _threatColor;
    private Label _threatLevelLabel;
    private Label _hasProtectionLabel;
    private Label _hitCooldownLabel;

    // 刺激源排行
    private Label[] _stimLabels = new Label[5];

    // 切换历史
    private Label[] _switchLabels = new Label[5];

    // 底部统计
    private VisualElement _hpBarFill;
    private Label _hpTextLabel;
    private Label _npcPositionLabel;
    private Label _nearbyInfoLabel;

    // ===== 双列对比（右列：最近敌对 NPC 的同一组字段，name 后缀 -r）=====
    private Label _leftTitle;
    private Label _rightTitle;
    private Label _focusLayerLabelR;
    private Label _focusIntensityLabelR;
    private Label _focusPositionLabelR;
    private VisualElement _spectrumColorR;
    private Label _spectrumNameLabelR;
    private VisualElement _threatColorR;
    private Label _threatLevelLabelR;
    private Label _hasProtectionLabelR;
    private Label _hitCooldownLabelR;
    private Label[] _stimLabelsR = new Label[5];
    private Label[] _switchLabelsR = new Label[5];
    private VisualElement _hpBarFillR;
    private Label _hpTextLabelR;
    private Label _npcPositionLabelR;
    private Label _nearbyInfoLabelR;

    // 顶部提示条
    private VisualElement _hintBar;

    // 状态
    private bool _waitingForSelection;  // 是否正在等待用户点击选择 NPC
    private bool _isCollapsed;          // 面板是否折叠
    private bool _modeEntryPushed;       // 调试模式层栈条目是否已压入
    private bool _interactionEntryPushed; // 交互态层栈条目是否已压入
    private bool _spawnEntryPushed;       // 放置模式栈条目是否已压入

    /// <summary>
    /// 调试模式层栈条目。F1 开启调试模式时压栈，Close 时关闭调试模式。
    /// ESC 关此条目 = 退出调试模式（回到正常游戏，下次 ESC 才触发暂停）。
    /// </summary>
    private class AIDebugModeEntry : IUIStackEntry
    {
        private readonly AIDebugUIManager _owner;
        public AIDebugModeEntry(AIDebugUIManager owner) { _owner = owner; }
        public void Open(Interactor ctx) { }
        public void Close() { _owner.OnModeEntryClosed(); }
    }

    /// <summary>
    /// 交互态层栈条目。进入"等待选择/已选中 NPC"时压栈，Close 时清除交互态。
    /// ESC 关此条目 = 清除选择，回到开发者面板（调试模式仍开）。
    /// </summary>
    private class AIDebugInteractionEntry : IUIStackEntry
    {
        private readonly AIDebugUIManager _owner;
        public AIDebugInteractionEntry(AIDebugUIManager owner) { _owner = owner; }
        public void Open(Interactor ctx) { }
        public void Close() { _owner.OnInteractionEntryClosed(); }
    }

    /// <summary>
    /// 放置模式栈条目。进入放置士兵模式时压栈，ESC/右键退出时弹出。
    /// </summary>
    private class AIDebugSpawnEntry : IUIStackEntry
    {
        private readonly AIDebugUIManager _owner;
        public AIDebugSpawnEntry(AIDebugUIManager owner) { _owner = owner; }
        public void Open(Interactor ctx) { }
        public void Close() { _owner.OnSpawnEntryClosed(); }
    }

    // 谱系颜色表
    private static readonly Color[] SpectrumColors = new Color[]
    {
        new Color(0.298f, 0.686f, 0.314f),  // 0 全力执行 #4CAF50
        new Color(0.545f, 0.765f, 0.290f),  // 1 警惕执行 #8BC34A
        new Color(1.000f, 0.757f, 0.027f),  // 2 谨慎 #FFC107
        new Color(1.000f, 0.596f, 0.000f),  // 3 边撤边做 #FF9800
        new Color(0.957f, 0.263f, 0.212f),  // 4 完全撤退 #F44336
    };

    // 谱系名称表
    private static readonly string[] SpectrumNames = new string[]
    {
        "全力执行", "警惕执行", "谨慎", "边撤边做", "完全撤退"
    };

    // 威胁等级颜色表
    private static readonly Color[] ThreatColors = new Color[]
    {
        new Color(0.620f, 0.620f, 0.620f),  // 0 无威胁 #9E9E9E
        new Color(0.129f, 0.588f, 0.953f),  // 1 警戒 #2196F3
        new Color(1.000f, 0.596f, 0.000f),  // 2 危险 #FF9800
        new Color(0.957f, 0.263f, 0.212f),  // 3 致命 #F44336
    };

    // 威胁等级名称表
    private static readonly string[] ThreatNames = new string[]
    {
        "无威胁", "警戒", "危险", "致命"
    };

    private void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
        {
            Debug.LogError("[AIDebugUIManager] 缺少 UIDocument 组件！");
            return;
        }

        _root = _document.rootVisualElement;
        if (_root == null)
        {
            Debug.LogWarning("[AIDebugUIManager] rootVisualElement 尚未就绪，延迟初始化。");
            return;
        }

        BindUIElements();
        SetAllPanelsVisible(false);
    }

    /// <summary>UIDocument 的 rootVisualElement 可能延迟初始化，用 Start 兜底再绑一次。</summary>
    private void Start()
    {
        if (_root == null)
        {
            _root = _document.rootVisualElement;
            if (_root != null)
            {
                BindUIElements();
                SetAllPanelsVisible(false);
            }
        }
    }

    private bool _devPanelCentered = false;

    private void BindUIElements()
    {
        // 屏幕中间开发者面板
        _devPanelRoot = _root.Q<VisualElement>("dev-panel-root");
        _devTitleBar = _root.Q<VisualElement>("dev-title-bar");
        _devCloseButton = _root.Q<Button>("dev-close-button");
        _selectNpcButton = _root.Q<Button>("select-npc-button");

        // 初始居中定位（延迟到布局完成后）
        if (_devPanelRoot != null && _root != null)
        {
            _devPanelCentered = false;
            _root.RegisterCallback<GeometryChangedEvent>(OnRootGeometryChanged);
        }

        // 标题栏拖拽
        if (_devTitleBar != null)
        {
            _devTitleBar.RegisterCallback<MouseDownEvent>(OnDevTitleBarMouseDown);
            _devTitleBar.RegisterCallback<MouseMoveEvent>(OnDevTitleBarMouseMove);
            _devTitleBar.RegisterCallback<MouseUpEvent>(OnDevTitleBarMouseUp);
        }

        // 关闭按钮
        if (_devCloseButton != null)
        {
            _devCloseButton.clicked += OnDevCloseButtonClicked;
        }

        if (_selectNpcButton != null)
        {
            _selectNpcButton.clicked += OnSelectNpcButtonClicked;
        }

        // Tab 切换
        _tabAIDebug = _root.Q<VisualElement>("tab-ai-debug");
        _tabSpawn = _root.Q<VisualElement>("tab-spawn");
        _tabFormation = _root.Q<VisualElement>("tab-formation");
        _aiDebugContent = _root.Q<VisualElement>("ai-debug-content");
        _spawnContent = _root.Q<VisualElement>("spawn-content");
        _spawnButtonsContainer = _root.Q<VisualElement>("spawn-ally-buttons");   // 兼容引用（旧名已拆，指向己方组）
        _allySpawnButtons = _root.Q<VisualElement>("spawn-ally-buttons");
        _enemySpawnButtons = _root.Q<VisualElement>("spawn-enemy-buttons");
        _spawnHintText = _root.Q<Label>("spawn-hint-text");
        _formationContent = _root.Q<VisualElement>("formation-content");
        _garrisonButton = _root.Q<Button>("formation-garrison-button");
        _disbandButton = _root.Q<Button>("formation-disband-button");
        _killTestButton = _root.Q<Button>("formation-kill-test-button");

        if (_tabAIDebug != null)
            _tabAIDebug.RegisterCallback<ClickEvent>(OnTabAIDebugClicked);
        if (_tabSpawn != null)
            _tabSpawn.RegisterCallback<ClickEvent>(OnTabSpawnClicked);
        if (_tabFormation != null)
            _tabFormation.RegisterCallback<ClickEvent>(OnTabFormationClicked);

        // 编队操作（热键 4/5/7 迁入）
        if (_garrisonButton != null) _garrisonButton.clicked += OnGarrisonClicked;
        if (_disbandButton != null) _disbandButton.clicked += OnDisbandClicked;
        if (_killTestButton != null) _killTestButton.clicked += OnKillTestClicked;

        // 动态生成放置类型按钮
        BuildSpawnButtons();

        // 左上角 AI 状态面板
        _statusPanelRoot = _root.Q<VisualElement>("status-panel-root");
        _titleBar = _root.Q<VisualElement>("title-bar");
        _panelTitle = _root.Q<Label>("panel-title");
        _npcNameLabel = _root.Q<Label>("npc-name");
        _collapseIcon = _root.Q<Label>("collapse-icon");
        _contentArea = _root.Q<VisualElement>("content-area");

        // 标题栏点击折叠/展开 + 拖拽
        if (_titleBar != null)
        {
            _titleBar.RegisterCallback<ClickEvent>(OnTitleBarClicked);
            _titleBar.RegisterCallback<MouseDownEvent>(OnTitleBarMouseDown);
            _titleBar.RegisterCallback<MouseMoveEvent>(OnTitleBarMouseMove);
            _titleBar.RegisterCallback<MouseUpEvent>(OnTitleBarMouseUp);
        }

        // 关闭按钮
        _closeButton = _root.Q<Button>("close-button");
        if (_closeButton != null)
        {
            _closeButton.clicked += OnCloseButtonClicked;
        }

        // 焦点区域
        _focusLayerLabel = _root.Q<Label>("focus-layer");
        _focusIntensityLabel = _root.Q<Label>("focus-intensity");
        _focusPositionLabel = _root.Q<Label>("focus-position");

        // 谱系区域
        _spectrumColor = _root.Q<VisualElement>("spectrum-color");
        _spectrumNameLabel = _root.Q<Label>("spectrum-name");

        // 威胁区域
        _threatColor = _root.Q<VisualElement>("threat-color");
        _threatLevelLabel = _root.Q<Label>("threat-level");
        _hasProtectionLabel = _root.Q<Label>("has-protection");
        _hitCooldownLabel = _root.Q<Label>("hit-cooldown");

        // 刺激源排行
        for (int i = 0; i < 5; i++)
        {
            _stimLabels[i] = _root.Q<Label>($"stim-{i}");
        }

        // 切换历史
        for (int i = 0; i < 5; i++)
        {
            _switchLabels[i] = _root.Q<Label>($"switch-{i}");
        }

        // 底部统计
        _hpBarFill = _root.Q<VisualElement>("hp-bar-fill");
        _hpTextLabel = _root.Q<Label>("hp-text");
        _npcPositionLabel = _root.Q<Label>("npc-position");
        _nearbyInfoLabel = _root.Q<Label>("nearby-info");

        // 双列对比（右列 -r 字段）
        _leftTitle = _root.Q<Label>("left-title");
        _rightTitle = _root.Q<Label>("right-title");
        _focusLayerLabelR = _root.Q<Label>("focus-layer-r");
        _focusIntensityLabelR = _root.Q<Label>("focus-intensity-r");
        _focusPositionLabelR = _root.Q<Label>("focus-position-r");
        _spectrumColorR = _root.Q<VisualElement>("spectrum-color-r");
        _spectrumNameLabelR = _root.Q<Label>("spectrum-name-r");
        _threatColorR = _root.Q<VisualElement>("threat-color-r");
        _threatLevelLabelR = _root.Q<Label>("threat-level-r");
        _hasProtectionLabelR = _root.Q<Label>("has-protection-r");
        _hitCooldownLabelR = _root.Q<Label>("hit-cooldown-r");
        for (int i = 0; i < 5; i++)
        {
            _stimLabelsR[i] = _root.Q<Label>($"stim-{i}-r");
        }
        for (int i = 0; i < 5; i++)
        {
            _switchLabelsR[i] = _root.Q<Label>($"switch-{i}-r");
        }
        _hpBarFillR = _root.Q<VisualElement>("hp-bar-fill-r");
        _hpTextLabelR = _root.Q<Label>("hp-text-r");
        _npcPositionLabelR = _root.Q<Label>("npc-position-r");
        _nearbyInfoLabelR = _root.Q<Label>("nearby-info-r");

        // 顶部提示条
        _hintBar = _root.Q<VisualElement>("hint-bar");
    }

    private void Update()
    {
        if (_root == null) return;

        // F1 切换调试模式
        if (Input.GetKeyDown(KeyCode.F1))
        {
            AIDebugController.Instance.ToggleDebugMode();
            if (AIDebugController.Instance.IsDebugMode)
            {
                // 刚开启：压入调试模式层栈条目
                PushModeEntry();
            }
            else
            {
                // 刚关闭：清理所有 AI 调试栈条目
                ClearDebugStack();
            }
        }

        bool isDebugMode = AIDebugController.Instance.IsDebugMode;

        // 调试模式关闭时，隐藏所有面板
        if (!isDebugMode)
        {
            SetAllPanelsVisible(false);
            return;
        }

        // ===== 放置模式输入处理 =====
        if (_isSpawnMode)
        {
            // 右键退出放置模式
            if (Input.GetMouseButtonDown(1))
            {
                ExitSpawnMode();
                return;
            }

            // 左键连续放置
            if (Input.GetMouseButtonDown(0) && _selectedSpawnType.HasValue)
            {
                Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                var result = AIDebugSpawnController.Instance.Spawn(_selectedSpawnType.Value, worldPos);
                if (result.Success)
                {
                    Debug.Log($"[AIDebugUI] {result.Message}");
                    // 如果是将军，自动编队
                    if (_selectedSpawnType.Value == DebugSpawnType.PlayerGeneral && result.Spawned != null)
                    {
                        AIDebugSpawnController.Instance.BindGeneralFormation(result.Spawned);
                    }
                }
                else
                {
                    Debug.LogWarning($"[AIDebugUI] {result.Message}");
                }
            }

            // ESC 由 UIManager.HandleEscape() 通过栈条目处理，此处不重复
        }
        else if (_isSpawnTabActive || _isFormationTabActive)
        {
            // 放置士兵/编队操作 Tab 激活时，禁用 AI 可视化功能（不处理 NPC 选择）
            // 只处理退出放置模式（右键/ESC 由栈条目处理）
        }
        else
        {
            // AI 可视化 Tab 激活时，正常处理 NPC 选择
            // 确保交互态压栈：无论通过按钮还是直接点击 NPC 进入交互态，
            // 只要处于交互态（等待选择/已选中）且未压栈，就补压一次。
            bool hasInteraction = _waitingForSelection || AIDebugController.Instance.SelectedBrain != null;
            if (hasInteraction && !_interactionEntryPushed)
            {
                PushInteractionEntry();
            }

            // 鼠标左键：等待选择时尝试选中 NPC
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                bool selected = AIDebugController.Instance.TrySelectAtWorldPosition(worldPos);
                if (selected && _waitingForSelection)
                {
                    _waitingForSelection = false;
                }
            }
        }

        // 更新面板可见性
        UpdatePanelVisibility();

        // 有选中 NPC 时，每帧刷新状态面板
        var snapshot = AIDebugController.Instance.GetSnapshot();
        if (snapshot.HasSelection)
        {
            RenderSnapshot(snapshot);
        }
    }

    private void UpdatePanelVisibility()
    {
        bool hasSelection = AIDebugController.Instance.SelectedBrain != null;

        // 屏幕中间开发者面板：调试模式开启 + 未选中NPC + 未等待选择 + 非放置模式
        // 放置模式下隐藏整个开发者面板（用户要求"点击放置后隐藏F1面板"）
        if (_devPanelRoot != null)
        {
            bool showDevPanel = !hasSelection && !_waitingForSelection && !_isSpawnMode;
            _devPanelRoot.style.display = showDevPanel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // 左上角 AI 状态面板：有选中 NPC（放置模式或放置士兵 Tab 激活时隐藏）
        if (_statusPanelRoot != null)
        {
            bool showStatusPanel = hasSelection && !_isSpawnMode && !_isSpawnTabActive && !_isFormationTabActive;
            _statusPanelRoot.style.display = showStatusPanel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // 顶部提示条：等待选择中（放置模式下显示放置提示）
        if (_hintBar != null)
        {
            if (_isSpawnMode)
            {
                _hintBar.style.display = DisplayStyle.Flex;
                if (_spawnHintText != null)
                {
                    _spawnHintText.text = _selectedSpawnType.HasValue
                        ? "左键放置单位，右键/ESC退出"
                        : "请先选择要放置的单位类型";
                }
            }
            else
            {
                _hintBar.style.display = _waitingForSelection ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }

    private void SetAllPanelsVisible(bool visible)
    {
        if (_devPanelRoot != null)
            _devPanelRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_statusPanelRoot != null)
            _statusPanelRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (_hintBar != null)
            _hintBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    // ===== 按钮回调 =====

    private void OnSelectNpcButtonClicked()
    {
        _waitingForSelection = true;
        // 进入交互态时压栈，使 ESC 由 UIManager 统一消费
        PushInteractionEntry();
    }

    // ===== 开发者面板拖拽 =====

    private void OnDevTitleBarMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0) return;
        _isDevDragging = true;
        _devTitleBar.CaptureMouse();
        var pos = _devPanelRoot.layout.position;
        _devDragOffset = evt.mousePosition - new Vector2(pos.x, pos.y);
        evt.StopPropagation();
    }

    private void OnDevTitleBarMouseMove(MouseMoveEvent evt)
    {
        if (!_isDevDragging) return;
        float newX = evt.mousePosition.x - _devDragOffset.x;
        float newY = evt.mousePosition.y - _devDragOffset.y;
        _devPanelRoot.style.left = newX;
        _devPanelRoot.style.top = newY;
    }

    private void OnDevTitleBarMouseUp(MouseUpEvent evt)
    {
        if (!_isDevDragging) return;
        _isDevDragging = false;
        _devTitleBar.ReleaseMouse();
    }

    // ===== 开发者面板关闭按钮 =====

    private void OnDevCloseButtonClicked()
    {
        // 关闭调试模式（等同于 F1 退出）
        if (AIDebugController.Instance != null && AIDebugController.Instance.IsDebugMode)
        {
            AIDebugController.Instance.ToggleDebugMode();
            ClearDebugStack();
        }
    }

    // ===== 初始居中定位（延迟到布局完成） =====

    private void OnRootGeometryChanged(GeometryChangedEvent evt)
    {
        if (_devPanelCentered || _devPanelRoot == null || _root == null) return;

        // 确保根容器已有有效尺寸
        if (_root.layout.width > 0 && _root.layout.height > 0)
        {
            float centerX = (_root.layout.width - 400f) / 2f;
            float centerY = (_root.layout.height - 300f) / 2f;
            _devPanelRoot.style.left = centerX;
            _devPanelRoot.style.top = centerY;
            _devPanelCentered = true;
        }
    }

    // ===== Tab 切换 =====

    private void OnTabAIDebugClicked(ClickEvent evt)
    {
        if (_isSpawnMode) return; // 放置模式下不允许切 Tab
        SwitchTab(0);
    }

    private void OnTabSpawnClicked(ClickEvent evt)
    {
        if (_isSpawnMode) return;
        SwitchTab(1);
    }

    private void OnTabFormationClicked(ClickEvent evt)
    {
        if (_isSpawnMode) return;
        SwitchTab(2);
    }

    /// <summary>三 Tab 切换：0=AI 可视化，1=放置士兵，2=编队操作。</summary>
    private void SwitchTab(int index)
    {
        _isSpawnTabActive = index == 1;
        _isFormationTabActive = index == 2;

        // 内容区显隐
        if (_aiDebugContent != null) _aiDebugContent.style.display = index == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        if (_spawnContent != null) _spawnContent.style.display = index == 1 ? DisplayStyle.Flex : DisplayStyle.None;
        if (_formationContent != null) _formationContent.style.display = index == 2 ? DisplayStyle.Flex : DisplayStyle.None;

        // Tab 高亮（放置模式下非放置 Tab 置灰）
        SetTabActive(_tabAIDebug, index == 0);
        SetTabActive(_tabSpawn, index == 1);
        SetTabActive(_tabFormation, index == 2);
    }

    private void SetTabActive(VisualElement tab, bool active)
    {
        if (tab == null) return;
        if (active)
        {
            tab.AddToClassList("tab-active");
            tab.RemoveFromClassList("tab-disabled");
        }
        else
        {
            tab.RemoveFromClassList("tab-active");
            if (_isSpawnMode) tab.AddToClassList("tab-disabled");
            else tab.RemoveFromClassList("tab-disabled");
        }
    }

    // ===== 编队操作（热键 4/5/7 迁入，原 CombatTestSpawner 逻辑） =====

    /// <summary>守城编队（热键 4）：创建/切换守城编队，绑城墙锚点，按标准配额招募。</summary>
    private void OnGarrisonClicked()
    {
        GameObject wallAnchor = GameObject.Find("WallAnchor_Left");
        if (wallAnchor == null)
        {
            // 原 CombatTestSpawner.CreateWallAnchors 已随测试生成器删除，这里按需创建
            wallAnchor = new GameObject("WallAnchor_Left");
            wallAnchor.transform.position = new Vector2(-12f, -3f);
            Debug.Log("[AIDebugUI] 未找到 WallAnchor_Left，已自动创建（-12,-3）。");
        }
        FormationController garrison = null;
        var all = FindObjectsByType<FormationController>(FindObjectsSortMode.None);
        foreach (var f in all)
        {
            if (f != null && f.isGarrison) { garrison = f; break; }
        }
        if (garrison == null)
        {
            var go = new GameObject("GarrisonController");
            go.transform.position = wallAnchor.transform.position;
            garrison = go.AddComponent<FormationController>();
            garrison.formationTable = Resources.Load<FormationTable>("Formations/FormationTable");
            garrison.InitGarrison(wallAnchor.transform);
        }
        garrison.RecruitStandard();
        Debug.Log("[AIDebugUI] 守城编队组队完成（无将军，城墙锚点）。");
    }

    /// <summary>解散活动将军编队（热键 5）：全体状态清理（ClearFormationState）。</summary>
    private void OnDisbandClicked()
    {
        var general = FindActiveGeneralFormation();
        if (general == null)
        {
            Debug.Log("[AIDebugUI] 无活跃将军编队（将军已阵亡/未生成/编队已解散）。");
            return;
        }
        general.DisbandAll();
        Debug.Log("[AIDebugUI] 将军编队解散，全体状态清理（ClearFormationState）。");
    }

    /// <summary>残编测试（热键 7）：杀活动将军编队第一个近战，触发 1s 防抖重排。</summary>
    private void OnKillTestClicked()
    {
        var general = FindActiveGeneralFormation();
        if (general == null)
        {
            Debug.Log("[AIDebugUI] 无活跃将军编队（将军已阵亡/未生成/编队已解散）。");
            return;
        }
        var brains = FindObjectsByType<NPCBrain>(FindObjectsSortMode.None);
        foreach (var brain in brains)
        {
            if (!brain.HasFormationSlot) continue;
            var unit = brain.GetComponent<UnitController>();
            if (unit == null || unit.Data == null) continue;
            if (unit.Data.occupation != Occupation.Warrior) continue;
            unit.TakeDamage(unit.CurrentHp);
            Debug.Log("[AIDebugUI] 残编测试：杀掉近战士兵，触发 1s 防抖重排。");
            return;
        }
        Debug.Log("[AIDebugUI] 无可杀的近战编队成员。");
    }

    /// <summary>查找当前活跃的将军编队（非守城 + 将军存活 + 有成员）。</summary>
    private FormationController FindActiveGeneralFormation()
    {
        var formations = FindObjectsByType<FormationController>(FindObjectsSortMode.None);
        foreach (var f in formations)
        {
            if (f == null || f.isGarrison) continue;
            if (f.GeneralUnit == null || !f.GeneralUnit.IsAlive) continue;
            if (f.MemberCount <= 0) continue;
            return f;
        }
        return null;
    }

    // ===== 放置按钮构建 =====

    /// <summary>动态生成放置类型按钮（从 AIDebugSpawnController 获取可生成清单）</summary>
    private void BuildSpawnButtons()
    {
        if (_allySpawnButtons != null) _allySpawnButtons.Clear();
        if (_enemySpawnButtons != null) _enemySpawnButtons.Clear();

        var types = AIDebugSpawnController.Instance.GetAvailableTypes();
        foreach (var option in types)
        {
            var btn = new Button();
            btn.text = option.DisplayName;
            btn.AddToClassList("spawn-type-button");
            var capturedType = option.Type;
            btn.clicked += () => OnSpawnTypeButtonClicked(capturedType, btn);
            // 己方/敌方分组（我方一列、敌方一列，固定窗口滚动）
            var target = option.FactionName == "己方" ? _allySpawnButtons : _enemySpawnButtons;
            if (target != null) target.Add(btn);
        }
    }

    /// <summary>清除所有放置按钮的选中样式（两组都清）。</summary>
    private void ClearSpawnSelection()
    {
        ClearSpawnSelectionIn(_allySpawnButtons);
        ClearSpawnSelectionIn(_enemySpawnButtons);
    }

    private void ClearSpawnSelectionIn(VisualElement group)
    {
        if (group == null) return;
        foreach (var child in group.Children())
        {
            child.RemoveFromClassList("spawn-selected");
        }
    }

    private void OnSpawnTypeButtonClicked(DebugSpawnType type, Button clickedBtn)
    {
        _selectedSpawnType = type;

        // 更新所有按钮的选中样式（己方/敌方两组）
        ClearSpawnSelection();
        clickedBtn.AddToClassList("spawn-selected");

        // 进入放置模式（隐藏 F1 面板，压栈）
        EnterSpawnMode();
    }

    // ===== 放置模式进入/退出 =====

    /// <summary>进入放置模式：隐藏 F1 面板，压入放置栈条目。</summary>
    private void EnterSpawnMode()
    {
        if (_isSpawnMode) return;
        _isSpawnMode = true;

        // 隐藏 F1 开发者面板
        if (_devPanelRoot != null)
            _devPanelRoot.style.display = DisplayStyle.None;

        // 压入放置模式栈条目（ESC 可退出）
        PushSpawnEntry();

        Debug.Log($"[AIDebugUI] 进入放置模式：{_selectedSpawnType}，左键放置，右键/ESC退出");
    }

    /// <summary>退出放置模式：恢复 F1 面板，弹出栈条目。</summary>
    private void ExitSpawnMode()
    {
        if (!_isSpawnMode) return;
        _isSpawnMode = false;
        _selectedSpawnType = null;

        // 弹出放置模式栈条目
        PopSpawnEntry();

        // 恢复 F1 面板显示（回到放置 Tab）
        UpdatePanelVisibility();

        Debug.Log("[AIDebugUI] 退出放置模式");
    }

    /// <summary>放置模式栈条目 Close 回调（ESC 触发）。</summary>
    private void OnSpawnEntryClosed()
    {
        if (!_spawnEntryPushed) return;
        _spawnEntryPushed = false;
        _isSpawnMode = false;
        _selectedSpawnType = null;

        // 清除按钮选中样式（己方/敌方两组）
        ClearSpawnSelection();

        // 恢复面板
        UpdatePanelVisibility();

        Debug.Log("[AIDebugUI] 放置模式栈条目关闭（ESC）");
    }

    // ===== UI 栈集成（三层：调试模式层 + 交互态层 + 放置模式层）=====

    /// <summary>F1 开启调试模式时压入调试模式层条目。重复调用安全。</summary>
    private void PushModeEntry()
    {
        if (_modeEntryPushed || UIManager.Instance == null) return;
        UIManager.Instance.Push(new AIDebugModeEntry(this), new Interactor(Faction.Human_Player, Vector3.zero));
        _modeEntryPushed = true;
    }

    /// <summary>进入交互态（等待选择/已选中）时压入交互态层条目。重复调用安全。</summary>
    private void PushInteractionEntry()
    {
        if (_interactionEntryPushed || UIManager.Instance == null) return;
        UIManager.Instance.Push(new AIDebugInteractionEntry(this), new Interactor(Faction.Human_Player, Vector3.zero));
        _interactionEntryPushed = true;
    }

    /// <summary>进入放置模式时压入放置模式层条目。重复调用安全。</summary>
    private void PushSpawnEntry()
    {
        if (_spawnEntryPushed || UIManager.Instance == null) return;
        UIManager.Instance.Push(new AIDebugSpawnEntry(this), new Interactor(Faction.Human_Player, Vector3.zero));
        _spawnEntryPushed = true;
    }

    /// <summary>退出放置模式时弹出放置模式层条目。</summary>
    private void PopSpawnEntry()
    {
        if (!_spawnEntryPushed || UIManager.Instance == null) return;
        // 如果栈顶是放置条目，直接 Pop
        if (UIManager.Instance.Peek() is AIDebugSpawnEntry)
        {
            UIManager.Instance.Pop();
        }
        _spawnEntryPushed = false;
    }

    /// <summary>F1 关闭调试模式时清理所有 AI 调试栈条目（从栈顶往下 Pop）。</summary>
    private void ClearDebugStack()
    {
        _waitingForSelection = false;
        _interactionEntryPushed = false;
        _modeEntryPushed = false;
        _spawnEntryPushed = false;
        _isSpawnMode = false;
        _selectedSpawnType = null;
        if (UIManager.Instance == null) return;
        // Pop 栈中所有 AI 调试条目（先清标志，防 Close 回调重复操作）
        while (UIManager.Instance.Peek() is AIDebugModeEntry ||
               UIManager.Instance.Peek() is AIDebugInteractionEntry ||
               UIManager.Instance.Peek() is AIDebugSpawnEntry)
        {
            UIManager.Instance.Pop();
        }
    }

    /// <summary>交互态层 Close 回调：ESC 关栈顶或主动 Pop 时触发，清掉交互态（调试模式仍开）。</summary>
    private void OnInteractionEntryClosed()
    {
        if (!_interactionEntryPushed) return;
        _interactionEntryPushed = false;
        _waitingForSelection = false;
        if (AIDebugController.Instance != null && AIDebugController.Instance.SelectedBrain != null)
            AIDebugController.Instance.ClearSelection();
    }

    /// <summary>调试模式层 Close 回调：ESC 关栈顶时触发，关闭调试模式。</summary>
    private void OnModeEntryClosed()
    {
        if (!_modeEntryPushed) return;
        _modeEntryPushed = false;
        // 交互态层可能还在栈里（如果 ESC 直接关到调试模式层），同步清理
        _interactionEntryPushed = false;
        _waitingForSelection = false;
        if (AIDebugController.Instance != null && AIDebugController.Instance.SelectedBrain != null)
            AIDebugController.Instance.ClearSelection();
        // 关闭调试模式
        if (AIDebugController.Instance != null && AIDebugController.Instance.IsDebugMode)
            AIDebugController.Instance.ToggleDebugMode();
    }

    private void OnTitleBarClicked(ClickEvent evt)
    {
        if (_wasDragging) { _wasDragging = false; return; }
        _isCollapsed = !_isCollapsed;
        if (_contentArea != null)
        {
            _contentArea.style.display = _isCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
        }
        if (_collapseIcon != null)
        {
            _collapseIcon.text = _isCollapsed ? "▶" : "▼";
        }
    }

    // ===== 拖拽 =====

    private void OnTitleBarMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0) return;
        _isDragging = true;
        _wasDragging = false;
        _dragStartPos = evt.mousePosition;
        _titleBar.CaptureMouse();
        var pos = _statusPanelRoot.layout.position;
        _dragOffset = evt.mousePosition - new Vector2(pos.x, pos.y);
        evt.StopPropagation();
    }

    private void OnTitleBarMouseMove(MouseMoveEvent evt)
    {
        if (!_isDragging) return;
        float dx = evt.mousePosition.x - _dragStartPos.x;
        float dy = evt.mousePosition.y - _dragStartPos.y;
        if (Mathf.Abs(dx) > 3f || Mathf.Abs(dy) > 3f)
            _wasDragging = true;
        float newX = evt.mousePosition.x - _dragOffset.x;
        float newY = evt.mousePosition.y - _dragOffset.y;
        _statusPanelRoot.style.left = newX;
        _statusPanelRoot.style.top = newY;
    }

    private void OnTitleBarMouseUp(MouseUpEvent evt)
    {
        if (!_isDragging) return;
        _isDragging = false;
        _titleBar.ReleaseMouse();
    }

    // ===== 关闭按钮 =====

    private void OnCloseButtonClicked()
    {
        if (AIDebugController.Instance != null && AIDebugController.Instance.SelectedBrain != null)
            AIDebugController.Instance.ClearSelection();
    }

    // ===== 渲染快照 =====

    private void RenderSnapshot(AIDebugSnapshot s)
    {
        // 左列：选中 NPC
        if (_npcNameLabel != null)
            _npcNameLabel.text = s.NPCName;
        if (_leftTitle != null)
            _leftTitle.text = "选中: " + s.NPCName;

        // 焦点
        RenderFocus(s.CurrentFocus);

        // 谱系
        RenderSpectrum(s.CurrentSpectrum);

        // 威胁等级
        RenderThreat(s.CurrentThreatLevel, s.HasProtection, s.IsInHitCooldown);

        // 刺激源排行
        RenderStimuli(s.TopStimuli);

        // 切换历史
        RenderSwitches(s.RecentSwitches);

        // 底部统计
        RenderStats(s);

        // 右列：最近敌对 NPC 对比（固定窗口滚动，滚动补全未展示信息）
        if (AIDebugController.Instance != null)
        {
            var compare = AIDebugController.Instance.GetCompareSnapshot();
            if (compare.HasValue)
            {
                if (_rightTitle != null) _rightTitle.text = "对比: " + compare.Value.NPCName;
                RenderRightColumn(compare.Value);
                return;
            }
        }
        ClearRightColumn();
    }

    /// <summary>右列无对比目标时清空为占位。</summary>
    private void ClearRightColumn()
    {
        if (_rightTitle != null) _rightTitle.text = "对比: 无敌对 NPC";
        RenderFocusTo(default, clear: true);
        RenderSpectrumTo(default, clear: true);
        RenderThreatTo(default, false, false, clear: true);
        RenderStimuliTo(null);
        RenderSwitchesTo(null);
        RenderStatsTo(null);
    }

    /// <summary>渲染右列（对比 NPC）。右列渲染到 -r 字段。</summary>
    private void RenderRightColumn(AIDebugSnapshot s)
    {
        RenderFocusTo(s.CurrentFocus, right: true);
        RenderSpectrumTo(s.CurrentSpectrum, right: true);
        RenderThreatTo(s.CurrentThreatLevel, s.HasProtection, s.IsInHitCooldown, right: true);
        RenderStimuliTo(s.TopStimuli, right: true);
        RenderSwitchesTo(s.RecentSwitches, right: true);
        RenderStatsTo(s, right: true);
    }

    private void RenderFocus(Focus focus)
    {
        RenderFocusTo(focus);
    }

    /// <summary>焦点渲染到指定列（right=false=左列 / true=右列 -r 字段；clear=清空占位）。</summary>
    private void RenderFocusTo(Focus focus, bool right = false, bool clear = false)
    {
        var layer = right ? _focusLayerLabelR : _focusLayerLabel;
        var intensity = right ? _focusIntensityLabelR : _focusIntensityLabel;
        var pos = right ? _focusPositionLabelR : _focusPositionLabel;
        if (clear || !focus.IsValid)
        {
            if (layer != null) layer.text = "无";
            if (intensity != null) intensity.text = "-";
            if (pos != null) pos.text = "-";
            return;
        }
        if (layer != null) layer.text = AISwitchRecord.LayerName(focus.Layer);
        if (intensity != null) intensity.text = focus.Intensity.ToString("F1");
        if (pos != null) pos.text = $"({focus.Position.x:F1}, {focus.Position.y:F1})";
    }

    private void RenderSpectrum(BehaviorSpectrum spectrum)
    {
        RenderSpectrumTo(spectrum);
    }

    /// <summary>谱系渲染到指定列（right=右列 -r；clear=清空占位）。</summary>
    private void RenderSpectrumTo(BehaviorSpectrum spectrum, bool right = false, bool clear = false)
    {
        var color = right ? _spectrumColorR : _spectrumColor;
        var name = right ? _spectrumNameLabelR : _spectrumNameLabel;
        if (clear)
        {
            if (color != null) color.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            if (name != null) name.text = "-";
            return;
        }
        int idx = (int)spectrum;
        if (color != null && idx >= 0 && idx < SpectrumColors.Length)
        {
            color.style.backgroundColor = SpectrumColors[idx];
        }
        if (name != null && idx >= 0 && idx < SpectrumNames.Length)
        {
            name.text = SpectrumNames[idx];
        }
    }

    private void RenderThreat(ThreatLevel threatLevel, bool hasProtection, bool isInHitCooldown)
    {
        RenderThreatTo(threatLevel, hasProtection, isInHitCooldown);
    }

    /// <summary>威胁渲染到指定列（right=右列 -r；clear=清空占位）。</summary>
    private void RenderThreatTo(ThreatLevel threatLevel, bool hasProtection, bool isInHitCooldown,
        bool right = false, bool clear = false)
    {
        var color = right ? _threatColorR : _threatColor;
        var level = right ? _threatLevelLabelR : _threatLevelLabel;
        var prot = right ? _hasProtectionLabelR : _hasProtectionLabel;
        var cool = right ? _hitCooldownLabelR : _hitCooldownLabel;
        if (clear)
        {
            if (color != null) color.style.backgroundColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            if (level != null) level.text = "-";
            if (prot != null) prot.text = "否";
            if (cool != null) cool.text = "否";
            return;
        }
        int idx = (int)threatLevel;
        if (color != null && idx >= 0 && idx < ThreatColors.Length)
        {
            color.style.backgroundColor = ThreatColors[idx];
        }
        if (level != null && idx >= 0 && idx < ThreatNames.Length)
        {
            level.text = $"{idx} {ThreatNames[idx]}";
        }
        if (prot != null) prot.text = hasProtection ? "是" : "否";
        if (cool != null) cool.text = isInHitCooldown ? "是" : "否";
    }

    private void RenderStimuli(List<StimulusDebugInfo> stimuli)
    {
        RenderStimuliTo(stimuli);
    }

    /// <summary>刺激源渲染到指定列（right=右列 -r；null=清空）。</summary>
    private void RenderStimuliTo(List<StimulusDebugInfo> stimuli, bool right = false)
    {
        var labels = right ? _stimLabelsR : _stimLabels;
        for (int i = 0; i < 5; i++)
        {
            if (labels[i] == null) continue;

            if (stimuli != null && i < stimuli.Count)
            {
                var stim = stimuli[i];
                string layerName = AISwitchRecord.LayerName(stim.Layer);
                string focusMark = stim.IsFocus ? " ★焦点" : "";
                labels[i].text = $"{i + 1}. [{layerName}] 强度 {stim.Intensity:F1}{focusMark}";
                labels[i].style.display = DisplayStyle.Flex;
            }
            else
            {
                labels[i].text = "-";
                labels[i].style.display = DisplayStyle.None;
            }
        }
    }

    private void RenderSwitches(List<AISwitchRecord> switches)
    {
        RenderSwitchesTo(switches);
    }

    /// <summary>切换历史渲染到指定列（right=右列 -r；null=清空）。</summary>
    private void RenderSwitchesTo(List<AISwitchRecord> switches, bool right = false)
    {
        var labels = right ? _switchLabelsR : _switchLabels;
        float currentTime = Time.time;
        for (int i = 0; i < 5; i++)
        {
            if (labels[i] == null) continue;

            if (switches != null && i < switches.Count)
            {
                var record = switches[i];
                float timeAgo = currentTime - record.Timestamp;
                labels[i].text = $"[{timeAgo:F1}s前] {record.Description}";
                labels[i].style.display = DisplayStyle.Flex;
            }
            else
            {
                labels[i].text = "-";
                labels[i].style.display = DisplayStyle.None;
            }
        }
    }

    private void RenderStats(AIDebugSnapshot s)
    {
        RenderStatsTo(s);
    }

    /// <summary>底部统计渲染到指定列（right=右列 -r；null=清空）。</summary>
    private void RenderStatsTo(AIDebugSnapshot? s, bool right = false)
    {
        var hpBar = right ? _hpBarFillR : _hpBarFill;
        var hpText = right ? _hpTextLabelR : _hpTextLabel;
        var pos = right ? _npcPositionLabelR : _npcPositionLabel;
        var nearby = right ? _nearbyInfoLabelR : _nearbyInfoLabel;
        if (!s.HasValue)
        {
            if (hpBar != null) hpBar.style.width = new Length(0f, LengthUnit.Percent);
            if (hpText != null) hpText.text = "HP: -";
            if (pos != null) pos.text = "-";
            if (nearby != null) nearby.text = "敌人 - / 友军 -";
            return;
        }
        var snap = s.Value;

        // HP 条
        if (hpBar != null)
        {
            float hpPercent = Mathf.Clamp01(snap.HPRatio) * 100f;
            hpBar.style.width = new Length(hpPercent, LengthUnit.Percent);
        }
        if (hpText != null)
        {
            hpText.text = $"HP: {(snap.HPRatio * 100f):F0}%";
        }

        // 位置
        if (pos != null)
        {
            pos.text = $"({snap.NPCPosition.x:F1}, {snap.NPCPosition.y:F1})";
        }

        // 附近单位
        if (nearby != null)
        {
            nearby.text = $"敌人 {snap.NearbyEnemyCount} / 友军 {snap.NearbyAllyCount}";
        }
    }

    private void OnDisable()
    {
        if (_selectNpcButton != null)
        {
            _selectNpcButton.clicked -= OnSelectNpcButtonClicked;
        }
        if (_titleBar != null)
        {
            _titleBar.UnregisterCallback<ClickEvent>(OnTitleBarClicked);
            _titleBar.UnregisterCallback<MouseDownEvent>(OnTitleBarMouseDown);
            _titleBar.UnregisterCallback<MouseMoveEvent>(OnTitleBarMouseMove);
            _titleBar.UnregisterCallback<MouseUpEvent>(OnTitleBarMouseUp);
        }
        if (_closeButton != null)
        {
            _closeButton.clicked -= OnCloseButtonClicked;
        }
    }
}
