using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 主菜单控制器。挂载到 MainMenuScene 的根 GameObject 上。
/// 负责协调主菜单、存档槽面板、角色创建面板的显示/隐藏，
/// 并处理三个入口（继续游戏 / 新建游戏 / 退出）。
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public enum Panel
    {
        MainMenu,        // 主菜单
        SaveSlots,       // 存档槽选择
        CharacterCreation // 角色创建
    }

    [Header("面板根节点（拖入场景里对应的 UIDocument 根）")]
    [SerializeField] private GameObject mainMenuRoot;
    [SerializeField] private GameObject saveSlotsRoot;
    [SerializeField] private GameObject characterCreationRoot;

    private Panel _currentPanel = Panel.MainMenu;

    private void Start()
    {
        ShowPanel(Panel.MainMenu);
    }

    // ===== 面板切换 =====

    public void ShowPanel(Panel panel)
    {
        _currentPanel = panel;
        if (mainMenuRoot != null) mainMenuRoot.SetActive(panel == Panel.MainMenu);
        if (saveSlotsRoot != null) saveSlotsRoot.SetActive(panel == Panel.SaveSlots);
        if (characterCreationRoot != null) characterCreationRoot.SetActive(panel == Panel.CharacterCreation);
    }

    // ===== 主菜单按钮回调（由 UI Button.onClick 绑定）=====

    /// <summary>主菜单 → 继续游戏（跳到存档槽选择）。</summary>
    public void OnContinueClicked()
    {
        ShowPanel(Panel.SaveSlots);
    }

    /// <summary>主菜单 → 新建游戏（跳到角色创建）。</summary>
    public void OnNewGameClicked()
    {
        ShowPanel(Panel.CharacterCreation);
    }

    /// <summary>主菜单 → 退出游戏。</summary>
    public void OnQuitClicked()
    {
        Debug.Log("[MainMenu] 退出游戏");
        Application.Quit();

        // 编辑器里 Application.Quit 不生效，额外打 log
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ===== 存档槽选择回调 =====

    /// <summary>存档槽面板 → 玩家选了一个槽位，开始读档。</summary>
    public void OnSaveSlotSelected(string slotId)
    {
        if (!SaveManager.Instance.HasSave(slotId))
        {
            Debug.LogWarning($"[MainMenu] 槽位 {slotId} 没有存档，忽略。");
            return;
        }

        // 设置跨场景参数
        GameBootstrap.IsLoadingSave = true;
        GameBootstrap.SaveSlotToLoad = slotId;

        Debug.Log($"[MainMenu] 读档: {slotId}");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    /// <summary>存档槽面板 → 返回主菜单。</summary>
    public void OnSaveSlotsBackClicked()
    {
        ShowPanel(Panel.MainMenu);
    }

    // ===== 角色创建回调 =====

    /// <summary>角色创建面板 → 玩家确认创建，开始新游戏。</summary>
    public void OnCharacterCreateConfirmed(NewGameConfig config)
    {
        if (config == null)
        {
            Debug.LogError("[MainMenu] NewGameConfig 为空，无法开始游戏。");
            return;
        }

        // 设置跨场景参数
        GameBootstrap.IsLoadingSave = false;
        GameBootstrap.NewGameConfig = config;

        Debug.Log($"[MainMenu] 新建游戏: ruler={config.rulerName}, slot={config.selectedSlotId}");
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }

    /// <summary>角色创建面板 → 返回主菜单。</summary>
    public void OnCharacterCreationBackClicked()
    {
        ShowPanel(Panel.MainMenu);
    }
}
