using UnityEngine;

/// <summary>
/// 玩家输入驱动器。挂在君主单位 Prefab 上，与 UnitController 配合使用。
/// 读取 InputManager 的输入，翻译成 UnitController 的 Move 调用。
/// 
/// 操作：
///   A/D          → 左右行走 (walkSpeed)
///   Shift + A/D  → 左右奔跑 (runSpeed)
/// </summary>
[RequireComponent(typeof(UnitController))]
public class PlayerInputHandler : MonoBehaviour
{
    private UnitController _unit;

    private void Awake()
    {
        _unit = GetComponent<UnitController>();
    }

    private void Update()
    {
        // 只有 Playing 状态才响应输入
        if (GameStateManager.Instance == null) return;
        if (GameStateManager.Instance.CurrentState != GameState.Playing) return;

        Vector2 moveInput = InputManager.Instance.MoveInput;
        bool run = InputManager.Instance.RunHeld;

        // 有移动输入时驱动 UnitController
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Debug.Log($"[PlayerInputHandler] Move: input={moveInput}, run={run}, unit={_unit != null}, data={_unit?.Data != null}");
            _unit.Move(moveInput, run);
        }
    }
}
