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

    protected override void Awake()
    {
        base.Awake();
        _cam = Camera.main;
    }

    private void Update()
    {
        // Build/Dialog 模式下不响应点击交互
        if (InputManager.Instance != null && InputManager.Instance.CurrentMode != InputMode.Normal)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            HandleClick();
        }
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
        }

        // 点空白 → 关闭当前面板
        UIManager.Instance?.CloseCurrent();
    }
}
