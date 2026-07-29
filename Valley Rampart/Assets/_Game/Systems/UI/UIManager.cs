using UnityEngine;

/// <summary>
/// UI 面板管理器（3.3.1 P4）。单例，管理交互面板的打开/关闭/刷新，同屏互斥。
/// InteractionManager 派发 ShowUI 时调 Open，点空白时调 CloseCurrent。
/// </summary>
public class UIManager : Singleton<UIManager>
{
    private IUIPanel _currentPanel;

    /// <summary>当前打开的面板（null=无）。</summary>
    public IUIPanel CurrentPanel => _currentPanel;

    /// <summary>是否正在显示面板。</summary>
    public bool HasPanelOpen => _currentPanel != null;

    /// <summary>打开面板（同屏互斥：先关旧的）。</summary>
    public void Open(IUIPanel panel, Interactor ctx)
    {
        if (_currentPanel == panel) { _currentPanel.Refresh(); return; }
        CloseCurrent();
        _currentPanel = panel;
        panel.Open(ctx);
        Debug.Log($"[UIManager] 打开面板: {panel.GetType().Name}");
    }

    /// <summary>关闭当前面板。</summary>
    public void CloseCurrent()
    {
        if (_currentPanel == null) return;
        _currentPanel.Close();
        _currentPanel = null;
    }

    /// <summary>刷新当前面板（升级/拆除后调）。</summary>
    public void RefreshCurrent()
    {
        _currentPanel?.Refresh();
    }
}
