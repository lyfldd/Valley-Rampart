using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 主菜单面板 UI 绑定。挂在 MainMenuUI GameObject 上。
/// 负责把 MainMenu.uxml 里的按钮接到 MainMenuController。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuPanel : MonoBehaviour
{
    private MainMenuController _controller;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        _controller = FindObjectOfType<MainMenuController>();
        if (_controller == null)
        {
            Debug.LogError("[MainMenuPanel] 找不到 MainMenuController。");
            return;
        }

        root.Q<Button>("continue-button").clicked += _controller.OnContinueClicked;
        root.Q<Button>("new-game-button").clicked += _controller.OnNewGameClicked;
        root.Q<Button>("settings-button").clicked += OnSettingsClicked;
        root.Q<Button>("quit-button").clicked += _controller.OnQuitClicked;

        // 如果任何槽位都没有存档，禁用「继续游戏」按钮
        bool hasAnySave = SaveManager.Instance.HasSave("slot_1")
                       || SaveManager.Instance.HasSave("slot_2")
                       || SaveManager.Instance.HasSave("slot_3");
        root.Q<Button>("continue-button").SetEnabled(hasAnySave);
    }

    private void OnSettingsClicked()
    {
        Debug.Log("[MainMenu] 设置面板尚未实现。");
    }
}
