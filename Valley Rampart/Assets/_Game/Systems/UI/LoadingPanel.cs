using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 加载面板。挂在 LoadingUI GameObject 上。
/// 当前实现是占位：跨场景加载是同步的，Loading 状态瞬间完成。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LoadingPanel : MonoBehaviour
{
    private void OnEnable()
    {
        // ★ 先订阅事件
        EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

        SetPanelVisible(false);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
    }

    private void OnGameStateChanged(GameStateChangedEvent evt)
    {
        SetPanelVisible(evt.NewState == GameState.Loading);
    }

    private void SetPanelVisible(bool visible)
    {
        var doc = GetComponent<UIDocument>();
        if (doc == null || doc.rootVisualElement == null) return;
        doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
