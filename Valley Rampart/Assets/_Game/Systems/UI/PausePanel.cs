using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 暂停面板。挂在 GameScene 的 PauseUI GameObject 上。
/// 订阅 EscapePressedEvent，根据当前状态显示/隐藏并切换 GameState。
/// 暂停时 Time.timeScale = 0，TimeManager 停止推进；恢复时改回 1。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PausePanel : MonoBehaviour
{
    private bool _buttonsBound;

    private void OnEnable()
    {
        // ★ 先订阅事件，确保不会因为 UI 初始化失败而漏掉 ESC
        EventBus.Subscribe<EscapePressedEvent>(OnEscapePressed);
        EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

        // 再绑定 UI 按钮（UIDocument 可能在 OnEnable 时还没完全准备好）
        if (!_buttonsBound) BindButtons();
        SetPanelVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<EscapePressedEvent>(OnEscapePressed);
        EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);

        // UI Toolkit 的 clicked 是 event，需要 -= 退订，否则反复 SetActive 会重复注册
        if (_buttonsBound)
        {
            var doc = GetComponent<UIDocument>();
            if (doc != null && doc.rootVisualElement != null)
            {
                doc.rootVisualElement.Q<Button>("resume-button").clicked -= OnResumeClicked;
                doc.rootVisualElement.Q<Button>("save-button").clicked -= OnSaveClicked;
                doc.rootVisualElement.Q<Button>("settings-button").clicked -= OnSettingsClicked;
                doc.rootVisualElement.Q<Button>("quit-button").clicked -= OnQuitClicked;
            }
            _buttonsBound = false;
        }
    }

    /// <summary>UIDocument 的 rootVisualElement 可能延迟初始化，用 Start 兜底再绑一次。</summary>
    private void Start()
    {
        if (!_buttonsBound) BindButtons();
    }

    private void BindButtons()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null)
        {
            Debug.LogWarning("[PausePanel] UIDocument/rootVisualElement 尚未就绪，延迟绑定按钮。");
            return;
        }

        var root = doc.rootVisualElement;
        root.Q<Button>("resume-button").clicked += OnResumeClicked;
        root.Q<Button>("save-button").clicked += OnSaveClicked;
        root.Q<Button>("settings-button").clicked += OnSettingsClicked;
        root.Q<Button>("quit-button").clicked += OnQuitClicked;
        _buttonsBound = true;
    }

    private void OnEscapePressed(EscapePressedEvent evt)
    {
        // UI 栈非空时优先关栈顶（建造模式/面板），不触发暂停（3.3.4 批次2）
        if (UIManager.Instance != null && UIManager.Instance.HandleEscape())
            return;

        switch (evt.CurrentState)
        {
            case GameState.Playing:
                Pause();
                break;
            case GameState.Paused:
                Resume();
                break;
            // 其他状态不响应
        }
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        // 离开 Playing/Paused 时强制隐藏面板（防止 GameOver 等情况下面板残留）
        if (evt.NewState != GameState.Playing && evt.NewState != GameState.Paused)
        {
            SetPanelVisible(false);
            // ⚠️ 不在此恢复 timeScale：GameOver 冻结（timeScale=0）由 GameOverPanel.Show() 管理，
            // 此处恢复会把 GameOver 的冻结覆盖回 1，导致"结算画面弹出但游戏世界继续跑"。
            // 返回主菜单的恢复由 TeardownManager.TeardownForReturnToMenu 统一处理。
        }
    }

    // ===== 暂停/恢复 =====

    private void Pause()
    {
        SetPanelVisible(true);
        Time.timeScale = 0f;
        GameStateManager.Instance.SetState(GameState.Paused);
    }

    private void Resume()
    {
        SetPanelVisible(false);
        // QQQ.3 B8-6 / LC-G2：用 TimeManager.CurrentTimeScale 恢复（勿硬编码 1f，否则 2x 下暂停再恢复变 1x）
        Time.timeScale = TimeManager.Instance != null ? TimeManager.Instance.CurrentTimeScale : 1f;
        GameStateManager.Instance.SetState(GameState.Playing);
    }

    // ===== 按钮回调 =====

    private void OnResumeClicked() => Resume();

    // D240（2_13 步骤5）：暂停菜单打开设置面板（GameScene 的 SettingsUI GameObject）
    private void OnSettingsClicked()
    {
        var settings = FindObjectOfType<SettingsPanel>();
        if (settings == null)
        {
            Debug.LogWarning("[PausePanel] 未找到 SettingsPanel（GameScene 缺少 SettingsUI GameObject）");
            return;
        }
        settings.Show();
    }

    private void OnSaveClicked()
    {
        string slotId = SaveManager.Instance.CurrentSlotId;
        if (string.IsNullOrEmpty(slotId)) slotId = "slot_1";

        if (SaveManager.Instance.Save(slotId))
        {
            Resume();
        }
    }

    private void OnQuitClicked()
    {
        TeardownManager.Instance.TeardownForReturnToMenu(saveBeforeTeardown: true);
    }

    private void SetPanelVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null)
        {
            return;
        }
        // 用 inline style 直接控制 rootVisualElement 的 display
        // 不用 class——因为 UXML 里的 class 是加在子元素 pause-root 上的，不是 rootVisualElement 上
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
