using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 主菜单控制器。挂载到 MainMenuScene 的根 GameObject 上。
/// 负责协调 4 个面板（Splash、MainMenu、SaveSlots、CharacterCreation）的显示，
/// 并处理 Booting → Splash → MainMenu 状态推进。
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public enum Panel
    {
        Splash,             // 过场
        MainMenu,           // 主菜单
        SaveSlots,          // 存档槽选择
        CharacterCreation   // 角色创建
    }

    [Header("面板根节点（拖入场景里对应的 UIDocument 根）")]
    [SerializeField] private GameObject splashRoot;
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject saveSlotsRoot;
    [SerializeField] private GameObject characterCreationRoot;

    [Header("游戏场景名（点击开始游戏/读档后切到该场景）")]
    [SerializeField] private string gameSceneName = "SampleScene";

    private Panel _currentPanel = Panel.Splash;

    private void Start()
    {
        // 启动 → Booting → 自动切到 Splash
        GameStateManager.Instance.SetState(GameState.Splash);
        ShowPanel(Panel.Splash);
    }

    // ===== 面板切换 =====

    public void ShowPanel(Panel panel)
    {
        _currentPanel = panel;
        if (splashRoot != null) splashRoot.SetActive(panel == Panel.Splash);
        if (mainMenuRoot != null) mainMenuRoot.SetActive(panel == Panel.MainMenu);
        if (saveSlotsRoot != null) saveSlotsRoot.SetActive(panel == Panel.SaveSlots);
        if (characterCreationRoot != null) characterCreationRoot.SetActive(panel == Panel.CharacterCreation);

        // 同步 GameState
        switch (panel)
        {
            case Panel.Splash: GameStateManager.Instance.SetState(GameState.Splash); break;
            case Panel.MainMenu: GameStateManager.Instance.SetState(GameState.MainMenu); break;
            case Panel.SaveSlots: GameStateManager.Instance.SetState(GameState.SaveSlotSelect); break;
            case Panel.CharacterCreation: GameStateManager.Instance.SetState(GameState.CharacterCreation); break;
        }
    }

    /// <summary>Splash 结束（由 SplashPanel 调用）。</summary>
    public void OnSplashCompleted()
    {
        ShowPanel(Panel.MainMenu);
    }

    // ===== 主菜单按钮回调 =====

    public void OnContinueClicked()
    {
        ShowPanel(Panel.SaveSlots);
    }

    public void OnNewGameClicked()
    {
        // 检查是否有空槽位
        if (SaveManager.Instance.HasAnySave("slot_1", "slot_2", "slot_3")
            && !HasEmptySlot())
        {
            Debug.LogWarning("[MainMenu] 三个槽位都被占用，请先删除一个。");
            return;
        }
        ShowPanel(Panel.CharacterCreation);
    }

    public void OnQuitClicked()
    {
        Debug.Log("[MainMenu] 退出游戏");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ===== 存档槽选择回调 =====

    public void OnSaveSlotSelected(string slotId)
    {
        if (!SaveManager.Instance.HasSave(slotId))
        {
            Debug.LogWarning($"[MainMenu] 槽位 {slotId} 没有存档，忽略。");
            return;
        }

        GameSceneEntrance.SetContinue(slotId);
        Debug.Log($"[MainMenu] 读档: {slotId}");
        LoadGameScene();
    }

    public void OnSaveSlotsBackClicked()
    {
        ShowPanel(Panel.MainMenu);
    }

    // ===== 角色创建回调 =====

    public void OnCharacterCreateConfirmed(NewGameConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[MainMenu] NewGameConfig 为空，无法开始游戏。");
            return;
        }

        GameSceneEntrance.SetNewGame(config);
        Debug.Log($"[MainMenu] 新建游戏: ruler={config.rulerName}, slot={config.selectedSlotId}");
        LoadGameScene();
    }

    public void OnCharacterCreationBackClicked()
    {
        ShowPanel(Panel.MainMenu);
    }

    // ===== 工具 =====

    private void LoadGameScene()
    {
        // 切场景前禁用输入防止遗留
        InputManager.Instance.DisableInput();
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    private bool HasEmptySlot()
    {
        return !SaveManager.Instance.HasSave("slot_1")
            || !SaveManager.Instance.HasSave("slot_2")
            || !SaveManager.Instance.HasSave("slot_3");
    }
}
