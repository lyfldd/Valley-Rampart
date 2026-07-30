using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 栈管理器（3.3.4 批次2）。单例，用栈管理 UI 条目的打开/关闭，先进后出（LIFO）。
/// 有面板 UI（IUIPanel）和无面板建造模式（BuildModeEntry）都作为 IUIStackEntry 入栈。
/// ESC 优先关栈顶；栈空时 ESC 交由 PausePanel 处理暂停。
///
/// 兼容旧接口：Open/CloseCurrent/RefreshCurrent/CurrentPanel/HasPanelOpen 保留。
/// </summary>
public class UIManager : Singleton<UIManager>
{
    private readonly Stack<IUIStackEntry> _stack = new Stack<IUIStackEntry>();

    /// <summary>栈是否非空（有 UI 条目打开）。</summary>
    public bool HasPanelOpen => _stack.Count > 0;

    /// <summary>栈顶条目（null=空栈）。</summary>
    public IUIStackEntry Peek() => _stack.Count > 0 ? _stack.Peek() : null;

    /// <summary>栈顶 IUIPanel（兼容旧代码，null=空栈或栈顶非面板）。</summary>
    public IUIPanel CurrentPanel => _stack.Count > 0 ? _stack.Peek() as IUIPanel : null;

    /// <summary>入栈并激活条目。</summary>
    public void Push(IUIStackEntry entry, Interactor ctx)
    {
        if (entry == null) return;
        _stack.Push(entry);
        entry.Open(ctx);
        Debug.Log($"[UIManager] Push: {entry.GetType().Name}（栈深 {_stack.Count}）");
    }

    /// <summary>出栈并关闭栈顶条目。先出栈再 Close，使 Close 内可安全 Push。</summary>
    public void Pop()
    {
        if (_stack.Count == 0) return;
        var entry = _stack.Pop();
        entry.Close();
        Debug.Log($"[UIManager] Pop: {entry.GetType().Name}（栈深 {_stack.Count}）");
    }

    /// <summary>ESC 处理：栈非空则关栈顶并返回 true（已消费）；栈空返回 false（交暂停面板）。</summary>
    public bool HandleEscape()
    {
        if (_stack.Count > 0) { Pop(); return true; }
        return false;
    }

    // ===== 兼容旧接口 =====

    /// <summary>打开面板（兼容旧接口，等同 Push）。</summary>
    public void Open(IUIPanel panel, Interactor ctx) => Push(panel, ctx);

    /// <summary>关闭栈顶（兼容旧接口，等同 Pop）。</summary>
    public void CloseCurrent() => Pop();

    /// <summary>刷新栈顶面板（若栈顶是 IUIPanel）。</summary>
    public void RefreshCurrent()
    {
        (_stack.Count > 0 ? _stack.Peek() as IUIPanel : null)?.Refresh();
    }
}
