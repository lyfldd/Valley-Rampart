using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>输入模式（3.3.1 C3）。各系统通过 SetMode 切换，避免冲突输入。</summary>
public enum InputMode
{
    Normal,  // 正常游戏（交互 + 选择）
    Build,   // 建造模式（禁用交互，ghost 跟随光标）
    Dialog   // 对话/菜单模式（禁用交互）
}

// 全局输入管理器（InputManager）
// 职责：封装 Unity Input System 的 Action Map，提供统一的输入读取与事件发布接口。
// 2_13 步骤1 God-view 改造（2026-09-01）：
//   - 旧 WASD 君主移动链退役（PlayerMoveEvent/MoveInput/RunHeld/OnMove/OnFastMove 移除——无君主可操控，
//     且 2_12/HH.17 君主实体已退役）；其遗留 .inputactions move/fastmove 定义保留待清理（漂移标注，报策划裁决）。
//   - 新增鼠标上帝视角输入：leftClick → LeftClickPressedEvent、rightClick → RightClickPressedEvent（含屏幕坐标）。
//   - 中键 pan / 滚轮 zoom 由 CameraRig（2_10）Legacy Input 自理，不重复事件化（漂移标注）；摄像机键盘 WASD 平移
//     属 CameraRig 输入链路，用户红线=保留不动。
//   - IsMovementEnabled → IsInteractionEnabled 改名（Q6 C 面注落地：Build/Dialog 禁交互点击）。语义不变。
//
// 输入响应策略（引导书第 4 节）：
//   - ESC 键始终响应，但行为随当前 GameState 变化——Playing→暂停菜单 / Paused→恢复 / 其他→确认框。
//   - 左键/右键仅 GameState.Playing 且 Normal 模式下发布（Build/Dialog 禁交互，玩家路径零回归）。
//
// 生命周期：
//   - Awake 时创建 GameInput 实例并绑定回调，但默认禁用（Disable）。
//   - 等待 GameBootstrap 完成初始化后调用 EnableInput() 才启用。
//   - OnDestroy / OnApplicationQuit 时解绑回调并释放资源，防止输入泄漏。
public class InputManager : Singleton<InputManager>
{
    // Unity Input System 生成的 Action Map 实例
    private GameInput _inputActions;

    // 当前输入模式（3.3.1 C3）。Build/Dialog 模式下禁用交互输入。
    public InputMode CurrentMode { get; private set; } = InputMode.Normal;
    // 交互是否可用（仅 Normal 模式；2_13 步骤1 改名 IsInteractionEnabled，Q6 C 面落地）
    public bool IsInteractionEnabled => CurrentMode == InputMode.Normal;

    protected override void Awake()
    {
        base.Awake();

        // 防止重复实例执行初始化逻辑（Singleton 基类会 Destroy 多余的 GameObject，
        // 但 InputManager 不应挂在可能被实例化的 Prefab 上，此处做防御性检查）
        if (_instance != this) return;

        _inputActions = new GameInput();

        // 绑定左键点击回调（performed = 按下；2_13 God-view 点选/框选起锚）
        _inputActions.Player.leftClick.performed += OnLeftClick;
        // 绑定右键指令回调（performed = 按下；统一指令入口）
        _inputActions.Player.rightClick.performed += OnRightClick;

        // 绑定 ESC 键回调
        _inputActions.Player.esc.performed += OnEsc;

        // 绑定 B 键（开关建造菜单）回调
        _inputActions.Player.togglebuildmenu.performed += OnToggleBuildMenu;

        // 中键 pan / 滚轮 zoom：CameraRig（2_10）Legacy Input 自理，此处不重复绑定、
        // 不发布 CameraPanEvent/CameraZoomEvent（避免双轨消费，漂移报策划裁决）。
        // CameraRig 键盘 WASD 平移同样由 CameraRig 自理——用户红线：摄像机组 WASD 移动保留。

        // 默认禁用输入，等待 GameBootstrap 在初始化完成后调用 EnableInput()
        // 这样可以避免在 Loading/Ready 阶段误触输入导致异常
        _inputActions.Disable();

        Debug.Log("[InputManager] 初始化完成（God-view 输入：鼠标左/右键事件；输入未启用，等待 GameBootstrap 激活）");
    }

    // 启用全部玩家输入（由 GameBootstrap 在游戏正式开始时调用）
    public void EnableInput()
    {
        if (_inputActions == null) return;
        _inputActions.Enable();
        Debug.Log("[InputManager] 输入已启用");
    }

    // 禁用全部玩家输入并重置（暂停/切场景时调用）
    // null 防御：退出时 OnApplicationQuit 顺序不确定，CleanupInputActions 可能已将 _inputActions 置 null
    public void DisableInput()
    {
        if (_inputActions == null) return;
        _inputActions.Disable();
    }

    /// <summary>切换输入模式（3.3.1 C3）。BuildController 进入建造时调 SetMode(Build)，退出调 SetMode(Normal)。</summary>
    public void SetMode(InputMode mode)
    {
        if (CurrentMode == mode) return;
        CurrentMode = mode;
        Debug.Log($"[InputManager] 输入模式切换: {mode}");
    }

    // 左键点击回调：仅 Playing + Normal（IsInteractionEnabled）模式发布 LeftClickPressedEvent。
    private void OnLeftClick(InputAction.CallbackContext ctx)
    {
        if (ctx.phase != InputActionPhase.Performed) return;
        if (!IsInteractionEnabled) return;                      // Build/Dialog 禁交互点击
        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Playing) return;
        EventBus.Publish(new LeftClickPressedEvent(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero));
    }

    // 右键指令回调：仅 Playing + Normal 模式发布 RightClickPressedEvent。
    private void OnRightClick(InputAction.CallbackContext ctx)
    {
        if (ctx.phase != InputActionPhase.Performed) return;
        if (!IsInteractionEnabled) return;
        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Playing) return;
        EventBus.Publish(new RightClickPressedEvent(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero));
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

    // B 键（开关建造菜单）回调
    // Playing 状态 + Normal/Build 模式下发布 ToggleBuildMenuPressedEvent
    // Paused 或其他状态下忽略
    private void OnToggleBuildMenu(InputAction.CallbackContext ctx)
    {
        if (ctx.phase != InputActionPhase.Performed) return;
        if (GameStateManager.Instance == null) return;
        GameState current = GameStateManager.Instance.CurrentState;
        if (current != GameState.Playing)
        {
            Debug.Log($"[InputManager] B 键忽略：当前状态={current}（仅 Playing 下允许）");
            return;
        }

        Debug.Log("[InputManager] B 键按下：发布 ToggleBuildMenuPressedEvent");
        EventBus.Publish(new ToggleBuildMenuPressedEvent(null)); // null = toggle
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
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

        _inputActions.Player.leftClick.performed -= OnLeftClick;
        _inputActions.Player.rightClick.performed -= OnRightClick;
        _inputActions.Player.esc.performed -= OnEsc;
        _inputActions.Player.togglebuildmenu.performed -= OnToggleBuildMenu;
        _inputActions.Disable();
        _inputActions.Dispose();
        _inputActions = null;
    }
}