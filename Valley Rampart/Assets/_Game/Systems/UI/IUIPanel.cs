/// <summary>
/// UI 面板接口（3.3.1 P4）。所有交互面板（BuildingPanel/对话框/侦察面板）实现此接口，
/// 由 UIManager 统一管理生命周期（Open/Close/Refresh），同屏互斥。
/// </summary>
public interface IUIPanel
{
    /// <summary>打开面板。ctx 携带交互发起者信息（阵营/位置）。</summary>
    void Open(Interactor ctx);

    /// <summary>关闭面板。</summary>
    void Close();

    /// <summary>刷新面板数据（升级/拆除后调）。</summary>
    void Refresh();
}
