using UnityEngine;

/// <summary>
/// 通用交互派发器（3.3.1 P3）。单例，监听鼠标点击 → 射线命中 →
/// 若命中物实现 IInteractable 则调 Interact(ctx) → 按 InteractionResult 派发到 UIManager 或执行动作。
/// 点空白 → 关闭当前面板。
/// </summary>
public class InteractionManager : Singleton<InteractionManager>
{
    [Header("交互设置")]
    [Tooltip("交互用的层级（含建筑/资源点/裂隙等可交互物的 Collider）")]
    public LayerMask interactableMask = ~0;

    private Camera _cam;
    private IInteractable _hovered;        // 当前悬停的可交互物（3.3.4 批次9 悬停高亮）
    private Color _hoveredOrigColor;
    private bool _hoveredHasColor;

    protected override void Awake()
    {
        base.Awake();
        _cam = Camera.main;
    }

    private void Update()
    {
        // 悬停高亮（3.3.4 批次9）
        UpdateHover();

        // Build/Dialog 模式下不响应点击交互
        if (InputManager.Instance != null && InputManager.Instance.CurrentMode != InputMode.Normal)
            return;

        // 面板打开时不响应世界点击，交给 UI Toolkit 处理（问题9临时方案，批次2 UI栈完成后升级）
        if (UIManager.Instance != null && UIManager.Instance.HasPanelOpen)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
    }

    /// <summary>悬停高亮：命中 IInteractable 加亮 SpriteRenderer.color，离开还原（3.3.4 批次9）。</summary>
    private void UpdateHover()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector2 worldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.OverlapPoint(worldPos, interactableMask);
        var interactable = hit != null ? hit.GetComponentInParent<IInteractable>() : null;

        if (interactable != _hovered)
        {
            ClearHover();
            _hovered = interactable;
            if (_hovered is MonoBehaviour mb && mb != null)
            {
                var sr = mb.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    _hoveredOrigColor = sr.color;
                    _hoveredHasColor = true;
                    sr.color = Color.yellow;
                }
            }
        }
    }

    private void ClearHover()
    {
        if (_hovered is MonoBehaviour mb && mb != null && _hoveredHasColor)
        {
            var sr = mb.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = _hoveredOrigColor;
        }
        _hoveredHasColor = false;
        _hovered = null;
    }

    private void HandleClick()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        // 鼠标屏幕坐标 → 世界坐标，用 2D OverlapPoint 检测命中
        Vector2 worldPos = _cam.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.OverlapPoint(worldPos, interactableMask);

        if (hit != null)
        {
            // 沿父级链查找 IInteractable 实现（建筑 Collider 可能在子物体）
            var interactable = hit.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                var ctx = new Interactor(Faction.Human_Player, worldPos);
                var result = interactable.Interact(ctx);

                switch (result.kind)
                {
                    case InteractionKind.ShowUI:
                        UIManager.Instance?.Open(result.panel, ctx);
                        break;
                    case InteractionKind.DoAction:
                        result.action?.Invoke();
                        break;
                    case InteractionKind.OpenSubmenu:
                        // 子菜单暂留，3.3 主体/后期补
                        break;
                }
                return;
            }

            // 3.5.1 §六（E-S8）：NPC 统一点击交互回落（招募/训练/对话，优先级子系统裁决）
            var clickTarget = hit.GetComponentInParent<IClickInteractable>();
            if (clickTarget != null && ClickInteractDispatcher.TryDispatch(clickTarget))
                return;
        }

        // 点空白 → 关闭当前面板
        UIManager.Instance?.CloseCurrent();
    }
}
