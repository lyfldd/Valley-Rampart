/// <summary>
/// UI 面板接口（3.3.1 P4）。所有交互面板（BuildingPanel/对话框/侦察面板）实现此接口，
/// 由 UIManager 统一管理生命周期（Open/Close/Refresh）。
///
/// 3.3.4 批次2：继承 IUIStackEntry，使面板可作为栈条目入栈。
/// Open/Close 即栈的 OnEnter/OnExit，面板实现无需改动。
/// </summary>
public interface IUIPanel : IUIStackEntry
{
    /// <summary>打开面板。ctx 携带交互发起者信息（阵营/位置）。</summary>
    void Open(Interactor ctx);

    /// <summary>关闭面板。</summary>
    void Close();

    /// <summary>刷新面板数据（升级/拆除后调）。</summary>
    void Refresh();
}
