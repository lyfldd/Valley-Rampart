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
    private Button _selectNpcButton;

    // 左上角 AI 状态面板
    private VisualElement _statusPanelRoot;
    private VisualElement _titleBar;
    private Label _panelTitle;
    private Label _npcNameLabel;
    private Label _collapseIcon;
    private VisualElement _contentArea;

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

    // 顶部提示条
    private VisualElement _hintBar;

    // 状态
    private bool _waitingForSelection;  // 是否正在等待用户点击选择 NPC
    private bool _isCollapsed;          // 面板是否折叠
    private bool _modeEntryPushed;       // 调试模式层栈条目是否已压入
    private bool _interactionEntryPushed; // 交互态层栈条目是否已压入

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

    private void BindUIElements()
    {
        // 屏幕中间开发者面板
        _devPanelRoot = _root.Q<VisualElement>("dev-panel-root");
        _selectNpcButton = _root.Q<Button>("select-npc-button");
        if (_selectNpcButton != null)
        {
            _selectNpcButton.clicked += OnSelectNpcButtonClicked;
        }

        // 左上角 AI 状态面板
        _statusPanelRoot = _root.Q<VisualElement>("status-panel-root");
        _titleBar = _root.Q<VisualElement>("title-bar");
        _panelTitle = _root.Q<Label>("panel-title");
        _npcNameLabel = _root.Q<Label>("npc-name");
        _collapseIcon = _root.Q<Label>("collapse-icon");
        _contentArea = _root.Q<VisualElement>("content-area");

        // 标题栏点击折叠/展开
        if (_titleBar != null)
        {
            _titleBar.RegisterCallback<ClickEvent>(OnTitleBarClicked);
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

        // 屏幕中间开发者面板：调试模式开启 + 未选中NPC + 未等待选择
        if (_devPanelRoot != null)
        {
            _devPanelRoot.style.display = (!hasSelection && !_waitingForSelection)
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // 左上角 AI 状态面板：有选中 NPC
        if (_statusPanelRoot != null)
        {
            _statusPanelRoot.style.display = hasSelection
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // 顶部提示条：等待选择中
        if (_hintBar != null)
        {
            _hintBar.style.display = _waitingForSelection
                ? DisplayStyle.Flex : DisplayStyle.None;
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

    // ===== UI 栈集成（两层：调试模式层 + 交互态层）=====

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

    /// <summary>F1 关闭调试模式时清理所有 AI 调试栈条目（从栈顶往下 Pop）。</summary>
    private void ClearDebugStack()
    {
        _waitingForSelection = false;
        _interactionEntryPushed = false;
        _modeEntryPushed = false;
        if (UIManager.Instance == null) return;
        // Pop 栈中所有 AI 调试条目（先清标志，防 Close 回调重复操作）
        while (UIManager.Instance.Peek() is AIDebugModeEntry || UIManager.Instance.Peek() is AIDebugInteractionEntry)
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

    // ===== 渲染快照 =====

    private void RenderSnapshot(AIDebugSnapshot s)
    {
        // 标题
        if (_npcNameLabel != null)
            _npcNameLabel.text = s.NPCName;

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
    }

    private void RenderFocus(Focus focus)
    {
        if (focus.IsValid)
        {
            if (_focusLayerLabel != null)
                _focusLayerLabel.text = AISwitchRecord.LayerName(focus.Layer);
            if (_focusIntensityLabel != null)
                _focusIntensityLabel.text = focus.Intensity.ToString("F1");
            if (_focusPositionLabel != null)
                _focusPositionLabel.text = $"({focus.Position.x:F1}, {focus.Position.y:F1})";
        }
        else
        {
            if (_focusLayerLabel != null) _focusLayerLabel.text = "无";
            if (_focusIntensityLabel != null) _focusIntensityLabel.text = "-";
            if (_focusPositionLabel != null) _focusPositionLabel.text = "-";
        }
    }

    private void RenderSpectrum(BehaviorSpectrum spectrum)
    {
        int idx = (int)spectrum;
        if (_spectrumColor != null && idx >= 0 && idx < SpectrumColors.Length)
        {
            _spectrumColor.style.backgroundColor = SpectrumColors[idx];
        }
        if (_spectrumNameLabel != null && idx >= 0 && idx < SpectrumNames.Length)
        {
            _spectrumNameLabel.text = SpectrumNames[idx];
        }
    }

    private void RenderThreat(ThreatLevel threatLevel, bool hasProtection, bool isInHitCooldown)
    {
        int idx = (int)threatLevel;
        if (_threatColor != null && idx >= 0 && idx < ThreatColors.Length)
        {
            _threatColor.style.backgroundColor = ThreatColors[idx];
        }
        if (_threatLevelLabel != null && idx >= 0 && idx < ThreatNames.Length)
        {
            _threatLevelLabel.text = $"{idx} {ThreatNames[idx]}";
        }
        if (_hasProtectionLabel != null)
        {
            _hasProtectionLabel.text = hasProtection ? "是" : "否";
        }
        if (_hitCooldownLabel != null)
        {
            _hitCooldownLabel.text = isInHitCooldown ? "是" : "否";
        }
    }

    private void RenderStimuli(List<StimulusDebugInfo> stimuli)
    {
        for (int i = 0; i < 5; i++)
        {
            if (_stimLabels[i] == null) continue;

            if (stimuli != null && i < stimuli.Count)
            {
                var stim = stimuli[i];
                string layerName = AISwitchRecord.LayerName(stim.Layer);
                string focusMark = stim.IsFocus ? " ★焦点" : "";
                _stimLabels[i].text = $"{i + 1}. [{layerName}] 强度 {stim.Intensity:F1}{focusMark}";
                _stimLabels[i].style.display = DisplayStyle.Flex;
            }
            else
            {
                _stimLabels[i].text = "-";
                _stimLabels[i].style.display = DisplayStyle.None;
            }
        }
    }

    private void RenderSwitches(List<AISwitchRecord> switches)
    {
        float currentTime = Time.time;
        for (int i = 0; i < 5; i++)
        {
            if (_switchLabels[i] == null) continue;

            if (switches != null && i < switches.Count)
            {
                var record = switches[i];
                float timeAgo = currentTime - record.Timestamp;
                _switchLabels[i].text = $"[{timeAgo:F1}s前] {record.Description}";
                _switchLabels[i].style.display = DisplayStyle.Flex;
            }
            else
            {
                _switchLabels[i].text = "-";
                _switchLabels[i].style.display = DisplayStyle.None;
            }
        }
    }

    private void RenderStats(AIDebugSnapshot s)
    {
        // HP 条
        if (_hpBarFill != null)
        {
            float hpPercent = Mathf.Clamp01(s.HPRatio) * 100f;
            _hpBarFill.style.width = new Length(hpPercent, LengthUnit.Percent);
        }
        if (_hpTextLabel != null)
        {
            _hpTextLabel.text = $"HP: {(s.HPRatio * 100f):F0}%";
        }

        // 位置
        if (_npcPositionLabel != null)
        {
            _npcPositionLabel.text = $"({s.NPCPosition.x:F1}, {s.NPCPosition.y:F1})";
        }

        // 附近单位
        if (_nearbyInfoLabel != null)
        {
            _nearbyInfoLabel.text = $"敌人 {s.NearbyEnemyCount} / 友军 {s.NearbyAllyCount}";
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
        }
    }
}
