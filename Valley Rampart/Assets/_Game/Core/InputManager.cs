using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 全局输入管理器。基于 Input System Action Map。
/// 只有 GameState.Playing 时才响应输入。
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
        Debug.Log($"[InputManager] OnMove: {MoveInput} (phase={ctx.phase})");

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
        _inputActions.Disable();
        _inputActions.Dispose();
        _inputActions = null;
    }
}
