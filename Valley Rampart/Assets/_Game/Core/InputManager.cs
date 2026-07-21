using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 全局输入管理器。基于 Input System Action Map。
/// 移动/奔跑仅在 GameState.Playing 时响应；
/// ESC 始终响应，按当前 GameState 决定行为：
///   - Playing  → 发布 EscapePressedEvent（UI 弹暂停面板并切到 Paused）
///   - Paused   → 发布 EscapePressedEvent（UI 关暂停面板并切回 Playing）
///   - 其他     → 发布 EscapePressedEvent（由 UI 决定行为：返回上一级 / 退出确认 等）
/// </summary>
public class InputManager : Singleton<InputManager>
{
    private GameInput _inputActions;

    public Vector2 MoveInput { get; private set; }
    public bool RunHeld { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        // 如果是重复实例被销毁，不创建 GameInput，避免泄漏
        if (_instance != this) return;

        _inputActions = new GameInput();

        _inputActions.Player.move.performed += OnMove;
        _inputActions.Player.move.canceled += OnMove;

        _inputActions.Player.fastmove.performed += OnFastMove;
        _inputActions.Player.fastmove.canceled += OnFastMove;

        _inputActions.Player.esc.performed += OnEsc;

        // 默认不启用，等 GameBootstrap 切到 Playing 再开
        _inputActions.Disable();

        Debug.Log("[InputManager] 初始化完成（输入未启用，等待 GameBootstrap 激活）");
    }

    public void EnableInput()
    {
        _inputActions.Enable();
        Debug.Log("[InputManager] 输入已启用");
    }

    public void DisableInput()
    {
        _inputActions.Disable();
        MoveInput = Vector2.zero;
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        MoveInput = ctx.ReadValue<Vector2>();

        // 只有 Playing 状态才广播移动事件
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing)
        {
            EventBus.Publish(new PlayerMoveEvent(Vector3.zero, MoveInput));
        }
    }

    private void OnFastMove(InputAction.CallbackContext ctx)
    {
        RunHeld = ctx.ReadValueAsButton();
    }

    private void OnEsc(InputAction.CallbackContext ctx)
    {
        if (ctx.phase != InputActionPhase.Performed) return;
        if (GameStateManager.Instance == null) return;

        GameState current = GameStateManager.Instance.CurrentState;
        Debug.Log($"[InputManager] ESC 按下，当前状态: {current}");

        // ESC 始终发布事件，由 UI 决定具体行为（弹暂停面板/返回上一级/退出游戏等）
        EventBus.Publish(new EscapePressedEvent(current));
    }

    private void OnDestroy()
    {
        CleanupInputActions();
    }

    protected override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        CleanupInputActions();
    }

    private void CleanupInputActions()
    {
        if (_inputActions == null) return;

        _inputActions.Player.move.performed -= OnMove;
        _inputActions.Player.move.canceled -= OnMove;
        _inputActions.Player.fastmove.performed -= OnFastMove;
        _inputActions.Player.fastmove.canceled -= OnFastMove;
        _inputActions.Player.esc.performed -= OnEsc;
        _inputActions.Disable();
        _inputActions.Dispose();
        _inputActions = null;
    }
}
