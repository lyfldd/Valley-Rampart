using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 过场动画面板。挂在 SplashUI GameObject 上。
/// 监听任意键或超时 3 秒，通知 MainMenuController 推进到主菜单。
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class SplashPanel : MonoBehaviour
{
    [SerializeField] private float autoSkipSeconds = 3f;

    private MainMenuController _controller;
    private Label _hintLabel;
    private float _elapsed;
    private bool _skipped;

    private void OnEnable()
    {
        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        _controller = FindObjectOfType<MainMenuController>();
        if (_controller == null)
        {
            Debug.LogError("[SplashPanel] 找不到 MainMenuController。");
            return;
        }

        _hintLabel = root.Q<Label>("splash-hint");
        _elapsed = 0f;
        _skipped = false;
    }

    private void Update()
    {
        if (_skipped || _controller == null) return;

        _elapsed += Time.unscaledDeltaTime;

        // 闪烁提示文字
        if (_hintLabel != null)
        {
            _hintLabel.style.opacity = (_elapsed % 1.0f) < 0.5f ? 1.0f : 0.3f;
        }

        // 任意键跳过
        if (Input.anyKeyDown)
        {
            SkipSplash();
            return;
        }

        // 超时跳过
        if (_elapsed >= autoSkipSeconds)
        {
            SkipSplash();
        }
    }

    private void SkipSplash()
    {
        if (_skipped) return;
        _skipped = true;
        Debug.Log("[SplashPanel] 过场结束，切换到主菜单。");
        _controller.OnSplashCompleted();
    }
}
