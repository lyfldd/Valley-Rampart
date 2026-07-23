using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// 暂停面板。挂在 GameScene 的 PauseUI GameObject 上。
/// 订阅 EscapePressedEvent，根据当前状态显示/隐藏并切换 GameState。
/// 暂停时 Time.timeScale = 0，TimeManager 停止推进；恢复时改回 1。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class PausePanel : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

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
        root.Q<Button>("quit-button").clicked += OnQuitClicked;
        _buttonsBound = true;
    }

    private void OnEscapePressed(EscapePressedEvent evt)
    {
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
            Time.timeScale = 1f;
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
        Time.timeScale = 1f;
        GameStateManager.Instance.SetState(GameState.Playing);
    }

    // ===== 按钮回调 =====

    private void OnResumeClicked() => Resume();

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
        string slotId = SaveManager.Instance.CurrentSlotId ?? "slot_1";
        SaveManager.Instance.Save(slotId);
        Time.timeScale = 1f;
        // 清理 DontDestroyOnLoad 的君主单位，防止旧君主被带入下一局
        if (RulerController.Instance != null)
            RulerController.Instance.DestroyMonarchForMenuReturn();
        // 退回主菜单前禁用输入，避免主菜单里残留的输入触发多余事件
        InputManager.Instance.DisableInput();
        SceneManager.LoadScene(mainMenuSceneName);
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
