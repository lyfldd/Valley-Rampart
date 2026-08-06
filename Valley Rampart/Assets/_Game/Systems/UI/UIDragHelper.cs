using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI 可拖动窗口工具（UI Toolkit / UIDocument）。
/// 将面板(panel)与标题栏(handle)绑定：按住 handle 可拖动整个面板，位置钳制在父容器内。
///
/// <para>特性：</para>
/// <list type="bullet">
/// <item>基于 MouseDown/Move/Up + 屏幕坐标（evt.mousePosition），纯 UI Toolkit，不依赖 UGUI。</item>
/// <item>面板若原本是布局定位（flex 居中），首次拖动时自动转为绝对定位并保持当前视觉位置，避免跳动。</item>
/// <item>拖动目标若命中 Button（含祖先为 Button 的元素，如标题栏内的关闭按钮），则不启动拖动，不破坏点击。</item>
/// <item>不触碰 UIManager 栈 / ESC 关闭逻辑，仅移动面板位置。</item>
/// </list>
///
/// 用法：<c>UIDragHelper.Attach(panelElement, handleElement);</c>
/// 通常在手部绑定阶段（Bind）调用一次即可。
/// </summary>
public static class UIDragHelper
{
    /// <summary>
    /// 为面板绑定拖动能力。panel 为被拖动的根面板元素，handle 为标题栏区域。
    /// </summary>
    public static void Attach(VisualElement panel, VisualElement handle)
    {
        if (panel == null || handle == null) return;

        Vector2 startScreenPos = Vector2.zero; // MouseDown 时的指针屏幕坐标起点
        Vector2 startPanelPos = Vector2.zero;  // 拖动开始时 panel 的 left/top
        bool dragging = false;

        handle.RegisterCallback<MouseDownEvent>(evt =>
        {
            // 若命中按钮（如标题栏内的关闭按钮），不启动拖动，交回按钮点击
            if (IsButtonTarget(evt.target as VisualElement)) return;

            // 面板若为布局定位（flex 居中），先转绝对定位并保持视觉位置，避免跳动
            if (panel.resolvedStyle.position != Position.Absolute)
            {
                var rect = panel.worldBound;
                var parentTopLeft = panel.parent != null ? (Vector2)panel.parent.worldBound.position : Vector2.zero;
                panel.style.position = Position.Absolute;
                panel.style.left = rect.x - parentTopLeft.x;
                panel.style.top = rect.y - parentTopLeft.y;
            }

            startScreenPos = evt.mousePosition;
            startPanelPos = new Vector2(panel.resolvedStyle.left, panel.resolvedStyle.top);
            dragging = true;
            evt.StopPropagation();
        });

        panel.RegisterCallback<MouseMoveEvent>(evt =>
        {
            if (!dragging) return;
            Vector2 delta = evt.mousePosition - startScreenPos;
            SetClamped(panel, startPanelPos.x + delta.x, startPanelPos.y + delta.y);
            evt.StopPropagation();
        });

        panel.RegisterCallback<MouseUpEvent>(evt =>
        {
            if (!dragging) return;
            dragging = false;
            evt.StopPropagation();
        });
    }

    /// <summary>判断事件目标是否为 Button 或其内部结构（命中按钮时不启动拖动）。</summary>
    private static bool IsButtonTarget(VisualElement target)
    {
        VisualElement cur = target;
        while (cur != null)
        {
            if (cur is Button) return true;
            cur = cur.parent;
        }
        return false;
    }

    /// <summary>设置面板位置并钳制在父容器可视范围内。</summary>
    private static void SetClamped(VisualElement panel, float left, float top)
    {
        Rect bounds = panel.parent != null
            ? panel.parent.worldBound
            : (panel.panel != null ? panel.panel.visualTree.worldBound : new Rect(0, 0, 0, 0));
        float w = panel.resolvedStyle.width;
        float h = panel.resolvedStyle.height;
        left = Mathf.Clamp(left, 0f, Mathf.Max(0f, bounds.width - w));
        top = Mathf.Clamp(top, 0f, Mathf.Max(0f, bounds.height - h));
        panel.style.left = left;
        panel.style.top = top;
    }
}