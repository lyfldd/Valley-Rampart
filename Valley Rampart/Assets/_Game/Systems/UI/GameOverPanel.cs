using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// 结算面板。挂在 GameScene 的 GameOverUI GameObject 上。
/// 订阅 GameStateChangedEvent，GameOver 时显示结算信息。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class GameOverPanel : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    private bool _buttonBound;

    private void OnEnable()
    {
        // ★ 先订阅事件
        EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

        if (!_buttonBound) BindButton();
        SetPanelVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);

        // UI Toolkit 的 clicked 是 event，需要 -= 退订，否则反复 SetActive 会重复注册
        if (_buttonBound)
        {
            var doc = GetComponent<UIDocument>();
            if (doc != null && doc.rootVisualElement != null)
            {
                doc.rootVisualElement.Q<Button>("back-to-menu-button").clicked -= OnBackToMenuClicked;
            }
            _buttonBound = false;
        }
    }

    private void Start()
    {
        if (!_buttonBound) BindButton();
    }

    private void BindButton()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;

        doc.rootVisualElement.Q<Button>("back-to-menu-button").clicked += OnBackToMenuClicked;
        _buttonBound = true;
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        if (evt.NewState == GameState.GameOver)
        {
            Show();
        }
        else if (evt.OldState == GameState.GameOver && evt.NewState != GameState.GameOver)
        {
            SetPanelVisible(false);
        }
    }

    private void Show()
    {
        Time.timeScale = 0f;
        SetPanelVisible(true);
        PopulateStats();
        Debug.Log("[GameOverPanel] 显示结算面板");
    }

    private void PopulateStats()
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        var root = doc.rootVisualElement;

        int days = 0;
        if (TimeManager.Instance != null) days = TimeManager.Instance.CurrentDay;
        root.Q<Label>("stat-days").text = $"{days} 天";

        if (RulerController.Instance != null)
        {
            var r = RulerController.Instance;
            root.Q<Label>("stat-resources").text = $"金{r.Gold} 石{r.Stone} 木{r.Wood} 粮{r.Food}";
            root.Q<Label>("stat-ruler").text = r.RulerName;
        }
    }

    private void OnBackToMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetPanelVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
