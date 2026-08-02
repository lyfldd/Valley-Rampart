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

    private void Start()
    {
        // 兜底：OnEnable 时 UIDocument.rootVisualElement 可能尚未就绪导致隐藏失败，
        // 全屏 loading-root 若不隐藏会拦截下方所有 UI 点击（挡 GameOver 按钮等）。
        // 此时按当前 GameState 校正显隐。
        var gm = GameStateManager.Instance;
        SetPanelVisible(gm != null && gm.CurrentState == GameState.Loading);
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
