/// <summary>
/// UI 栈条目接口（3.3.4 批次2 UI栈系统）。
/// 有面板的 UI（IUIPanel）和无面板的建造模式（BuildModeEntry）都实现此接口，
/// 统一由 UIManager 用栈管理。先进后出（LIFO），ESC 优先关栈顶。
/// </summary>
public interface IUIStackEntry
{
    /// <summary>入栈时激活（面板 Open / 建造模式激活）。</summary>
    void Open(Interactor ctx);

    /// <summary>出栈时关闭（面板 Close / 建造模式退出）。</summary>
    void Close();
}
