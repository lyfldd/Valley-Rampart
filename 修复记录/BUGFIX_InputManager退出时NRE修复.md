# BUGFIX · InputManager.DisableInput 退出时 NRE

> 状态：**已修复**（2026-07-28）
> 关联：改进方案_Singleton退出陷阱_方案三_治本改进.md

## 一、错误现象

退出游戏时抛出 NullReferenceException：

```
NullReferenceException: Object reference not set to an instance of an object
  InputManager.DisableInput () (at Assets/_Game/Core/InputManager.cs:67)
  TeardownManager.TeardownForQuit () (at Assets/_Game/Systems/Loading/TeardownManager.cs:153)
  TeardownManager.OnApplicationQuit () (at Assets/_Game/Systems/Loading/TeardownManager.cs:165)
```

## 二、根因分析

### 2.1 调用链

```
TeardownManager.OnApplicationQuit()          ← Unity 退出时调用（顺序不确定）
  ├─ base.OnApplicationQuit()                ← Singleton 基类设 _isQuitting = true
  └─ TeardownForQuit()
       ├─ if (InputManager.Instance != null) ← 方案三修复后：实例活着就返回（不再返回 null）
       └─ InputManager.Instance.DisableInput()
            └─ _inputActions.Disable()        ← NRE！_inputActions 已被清成 null
```

### 2.2 为什么 _inputActions 是 null

Unity 调用各对象 `OnApplicationQuit` 的顺序**不确定**。当 `InputManager.OnApplicationQuit` 先于 `TeardownManager.OnApplicationQuit` 执行时：

```
InputManager.OnApplicationQuit()
  ├─ base.OnApplicationQuit()               ← 设 _isQuitting = true
  └─ CleanupInputActions()
       ├─ _inputActions.Disable()
       ├─ _inputActions.Dispose()
       └─ _inputActions = null               ← 此处置 null
```

之后 `TeardownManager.TeardownForQuit()` 调用 `InputManager.Instance.DisableInput()`，方案三正确返回了存活的 InputManager 实例，但实例内部的 `_inputActions` 已被 `CleanupInputActions()` 置 null，导致 `DisableInput()` 里 `_inputActions.Disable()` 抛 NRE。

### 2.3 与方案三的关系

方案三修复的是 **Singleton Instance getter 在退出时一刀切返回 null** 的问题（`_instance` 还活着但 getter 拒绝给）。修复后 Instance 能正确返回，但这暴露了**下一层问题**：实例虽然活着，内部状态可能已被 `OnApplicationQuit` 清理。

| 问题层 | 原症状 | 修复 |
|--------|--------|------|
| Singleton Instance getter | 退出时 getter 返回 null → TeardownScene 拿不到单例 | 方案三：对象活着就给 |
| InputManager 内部状态 | DisableInput 访问已清理的 _inputActions → NRE | 本修复：null 检查 |

## 三、修复方案

在 `EnableInput()` 和 `DisableInput()` 开头加 null 检查，与 `CleanupInputActions()` 已有的 `if (_inputActions == null) return` 防御一致。

### 3.1 改动

**文件**：`Assets/_Game/Core/InputManager.cs`

```csharp
// 改前（NRE）
public void DisableInput()
{
    _inputActions.Disable();
    MoveInput = Vector2.zero;
}

// 改后（安全）
public void DisableInput()
{
    if (_inputActions == null) return;
    _inputActions.Disable();
    MoveInput = Vector2.zero;
}
```

同时对 `EnableInput()` 也加 null 检查（防御性，当前退出路径不调 EnableInput 但保持一致）。

### 3.2 为什么这样修

1. **退出路径允许失败跳过**：退出时 Unity 自动回收内存，`DisableInput` 清理失败不致命。
2. **与现有模式一致**：`CleanupInputActions()` 已有 `if (_inputActions == null) return` 的先例。
3. **不改变正常流程**：正常运行时 `_inputActions` 在 Awake 创建、在 OnDestroy/Cleanup 时才清 null，null 检查不影响正常路径。
4. **不在调用方加 try-catch**：在 InputManager 自身加防御，所有调用方（TeardownForQuit / PausePanel / GameOverPanel 等）都受益，不需要各自包 try-catch。

## 四、验证

- [x] 编译通过（Unity 0 错误）
- [ ] Play Mode 烟测：退出时无 NRE（待用户验证）

## 五、影响范围

| 文件 | 改动 |
|------|------|
| `InputManager.cs` | `EnableInput()` + `DisableInput()` 各加 `if (_inputActions == null) return` |

不影响其他文件。`TeardownForQuit` 第 152 行已有 `if (InputManager.Instance != null)` 检查，不需要改。

## 六、教训

退出路径的每个方法都应该是**幂等且 null 安全**的：
- Singleton Instance getter：方案三已修（活着就给 + try-catch 兜底）
- 业务方法（DisableInput 等）：内部字段可能被 OnApplicationQuit 先行清理，方法本身需要 null 防御
- 清理方法（CleanupInputActions 等）：已有 null 检查先例，新方法应遵循

退出时 Unity 保证最终回收所有资源，中间步骤失败跳过是安全的。
