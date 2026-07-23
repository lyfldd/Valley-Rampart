using UnityEngine;
using UnityEngine.InputSystem;

// 全局输入管理器（InputManager）
// 职责：封装 Unity Input System 的 Action Map，提供统一的输入读取与事件发布接口。
//
// 输入响应策略（引导书第 4 节）：
//   - 移动/跑步输入仅在 GameState.Playing 时响应，避免暂停/菜单状态下角色移动。
//   - ESC 键始终响应，但行为随当前 GameState 变化——
//     - Playing → 发布 EscapePressedEvent，UI 层暂停游戏并弹出暂停菜单。
//     - Paused  → 发布 EscapePressedEvent，UI 层恢复游戏并关闭暂停菜单。
//     - 其他   → 发布 EscapePressedEvent，UI 层根据上下文弹出确认对话框（下一步/退出等）。
//
// 生命周期：
//   - Awake 时创建 GameInput 实例并绑定回调，但默认禁用（Disable）。
//   - 等待 GameBootstrap 完成初始化后调用 EnableInput() 才启用。
//   - OnDestroy / OnApplicationQuit 时解绑回调并释放资源，防止输入泄漏。
public class InputManager : Singleton<InputManager>
{
    // Unity Input System 生成的 Action Map 实例
    private GameInput _inputActions;

    // 当前移动输入向量（二维，来自 WASD/左摇杆）
    public Vector2 MoveInput { get; private set; }

    // 跑步键是否按住
    public bool RunHeld { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        // 防止重复实例执行初始化逻辑（Singleton 基类会 Destroy 多余的 GameObject，
        // 但 InputManager 不应挂在可能被实例化的 Prefab 上，此处做防御性检查）
        if (_instance != this) return;

        _inputActions = new GameInput();

        // 绑定移动输入回调（performed = 按下/持续，canceled = 松开）
        _inputActions.Player.move.performed += OnMove;
        _inputActions.Player.move.canceled += OnMove;

        // 绑定跑步输入回调
        _inputActions.Player.fastmove.performed += OnFastMove;
        _inputActions.Player.fastmove.canceled += OnFastMove;

        // 绑定 ESC 键回调
        _inputActions.Player.esc.performed += OnEsc;

        // 默认禁用输入，等待 GameBootstrap 在初始化完成后调用 EnableInput()
        // 这样可以避免在 Loading/Ready 阶段误触输入导致异常
        _inputActions.Disable();

        Debug.Log("[InputManager] 初始化完成（输入未启用，等待 GameBootstrap 激活）");
    }

    // 启用全部玩家输入（由 GameBootstrap 在游戏正式开始时调用）
    public void EnableInput()
    {
        _inputActions.Enable();
        Debug.Log("[InputManager] 输入已启用");
    }

    // 禁用全部玩家输入并重置移动向量（暂停/切场景时调用）
    public void DisableInput()
    {
        _inputActions.Disable();
        MoveInput = Vector2.zero;
    }

    // 移动输入回调
    // 仅在 Playing 状态下发布 PlayerMoveEvent，其他状态只更新 MoveInput 属性
    private void OnMove(InputAction.CallbackContext ctx)
    {
        MoveInput = ctx.ReadValue<Vector2>();

        // 只在 Playing 状态才广播移动事件，避免暂停/菜单状态下角色移动
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing)
        {
            EventBus.Publish(new PlayerMoveEvent(Vector3.zero, MoveInput));
        }
    }

    // 跑步输入回调（仅更新 RunHeld 标志，不发布事件——跑步是移动的修饰状态）
    private void OnFastMove(InputAction.CallbackContext ctx)
    {
        RunHeld = ctx.ReadValueAsButton();
    }

    // ESC 键回调
    // 始终发布 EscapePressedEvent，由 UI 层根据当前 GameState 决定行为
    // （暂停/恢复/弹出确认框等，见引导书第 4 节）
    private void OnEsc(InputAction.CallbackContext ctx)
    {
        if (ctx.phase != InputActionPhase.Performed) return;
        if (GameStateManager.Instance == null) return;

        GameState current = GameStateManager.Instance.CurrentState;
        Debug.Log($"[InputManager] ESC 按下，当前状态: {current}");

        // ESC 始终发布事件，UI 层根据状态决定行为（暂停/恢复/下一步/退出游戏等）
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

    // 解绑所有输入回调并释放 GameInput 资源
    // 必须在 OnDestroy/OnApplicationQuit 中调用，否则回调会引用已销毁的对象
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