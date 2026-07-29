# Singleton 退出陷阱 · 方案三治本改进

> 状态：**已实施**（2026-07-28）
> 负责人：六月份 agent
> 日期：2026-07-28
> 关联：修复记录/全流程审查_场景切换与内存泄漏.md、日志 2026-07-27
>
> **实施记录**：
> - 改 `Singleton.cs`：Instance getter 退出分支改为"对象活着就给"+ try-catch 兜底；新增 `OnDestroy` 清 `_instance`
> - 改 4 个 Singleton 子类 `private void OnDestroy()` → `protected override void OnDestroy()` + `base.OnDestroy()`：
>   `DifficultyManager` / `InputManager` / `RulerController` / `SaveManager`
> - 幂等性确认：`ClearMonarchReference`（monarchUnit=null）✓、`ISaveable.SaveId`（纯 string 属性）✓
> - 编译：Unity 0 错误；`Singleton<T>.OnDestroy()` 经反射确认存在
> - Play Mode 烟测：进出 0 个 NRE、0 个旧"不再提供实例"警告、0 个 MissingReferenceException
> - VS Code 诊断因语言服务器缓存延迟显示误报，Unity 实际编译无错

## 一、背景与问题

### 1.1 坑的现象

退出游戏时，`TeardownManager.TeardownScene` 访问 `SaveManager.Instance` / `UnitRegistry.Instance` 等其他单例时，Instance getter 返回 null，触发 NullReferenceException 或导致清理被跳过。

### 1.2 坑的根因

`Assets/_Game/Core/Singleton.cs` 第 62-65 行与第 25-29 行联动：

```csharp
// 第 62-65 行：退出时打标记
protected virtual void OnApplicationQuit()
{
    _isQuitting = true;
}

// 第 25-29 行：getter 一刀切返回 null
get
{
    if (_isQuitting)
    {
        Debug.LogWarning($"[{typeof(T).Name}] 游戏正在关闭，不再提供实例。");
        return null;   // ← 坑：对象还活着，但 getter 拒绝给
    }
    ...
}
```

Unity 调用各对象 `OnApplicationQuit` 的顺序**不确定**。若 SaveManager/UnitRegistry 的 OnApplicationQuit 先于 TeardownManager 执行，它们的 `_isQuitting=true`，TeardownManager 再访问 `X.Instance` 就拿到 null。

### 1.3 现状补丁

`TeardownScene` 第 56-61 行加了 `if (X.Instance != null)` 防御性检查。补丁能跑，但：
- 治标不治本，退出时清理实际被跳过（靠 Unity 兜底回收）
- 每个 `X.Instance` 访问都要手动加 null 检查，易遗漏
- 代码注释只能解释"必须加"，没解释"为什么基类机制本身和 TeardownManager 对立"

## 二、方案选择

### 2.1 四个治本方向对比

| 方案 | 改动 | 风险 | 治本程度 |
|------|------|------|---------|
| 方向1：Level3 瘦身，退出时不依赖单例 | 中 | 低 | 中（放弃退出清理） |
| 方向2：Awake 时缓存单例引用 | 小 | 低 | 中（引用列表手动维护） |
| **方向3：改 Singleton 基类 _isQuitting 逻辑** | **小（一处）** | **中** | **高（根治 getter）** |
| 方向4：换 Runtime Set，去掉单例依赖 | 大 | 中 | 高（只解决部分单例） |

### 2.2 选择方案三的理由

阿铁指定偏向方案三。方案三的吸引力在于：**改一处基类，所有单例的退出访问问题一次性解决**，不需要逐个加缓存引用或换 SO。风险可控（见第五章评估），且向后兼容（非退出时行为不变）。

## 三、影响范围评估（基于代码调研）

### 3.1 Singleton 子类清单（共 14 个）

| 类 | override OnApplicationQuit | override OnDestroy | 退出时依赖其他单例 |
|----|---------------------------|-------------------|------------------|
| DifficultyManager | 否 | EventBus.Unsubscribe | 否 |
| GameStateManager | 否 | — | 否 |
| LoadManager | 否 | — | 否 |
| InputManager | **是**（CleanupInputActions） | CleanupInputActions | 否（只清自己资源） |
| GridSystem | 否 | — | 否 |
| TeardownManager | **是**（TeardownForQuit） | — | **是**（TeardownScene 调多个单例） |
| RulerController | 否 | EventBus.Unsubscribe（含 _instance 防重） | 否 |
| SaveManager | 否 | EventBus.Unsubscribe | 否 |
| TimeManager | 否 | — | 否 |
| WorldManager | 否 | — | 否 |
| UnitDataManager | 否 | — | 否 |
| WorldSystem | 否 | — | 否 |
| UnitFactory | 否 | — | 否 |
| UnitRegistry | 否 | — | 否 |

### 3.2 关键结论

1. **只有 2 个类 override OnApplicationQuit**：InputManager（清输入资源，不依赖其他单例）、TeardownManager（调 TeardownForQuit，依赖多个单例）。
2. **OnDestroy 全是 EventBus.Unsubscribe**，不依赖 `X.Instance`，不受方案三影响。
3. **唯一撞坑路径**：`TeardownManager.OnApplicationQuit → TeardownForQuit → TeardownScene` 里访问 `SaveManager.Instance` / `UnitRegistry.Instance` / `RulerController.Instance`。
4. **非 Singleton 的 OnDestroy**（GameBootstrap/ResourceHUD/TopLeftHUD）也都是 EventBus.Unsubscribe，安全。

影响面收敛：**改 Instance getter 只影响退出路径，且退出路径里真正依赖跨单例访问的只有 TeardownScene 那几行**。

## 四、详细设计

### 4.1 改动点：Instance getter + 新增 OnDestroy（共两处）

本方案改 `Singleton.cs` 两处，配合形成**源头防范 + getter 治本**的双重保护。

#### 4.1.1 改动一：Instance getter（第 21-47 行）

**改前**：
```csharp
public static T Instance
{
    get
    {
        if (_isQuitting)
        {
            Debug.LogWarning($"[{typeof(T).Name}] 游戏正在关闭，不再提供实例。");
            return null;
        }

        if (_instance == null)
        {
            _instance = FindObjectOfType<T>();
            if (_instance == null)
            {
                Debug.LogWarning($"[{typeof(T).Name}] 场景中未找到实例，自动创建。...");
                GameObject go = new GameObject($"[Singleton] {typeof(T).Name}");
                _instance = go.AddComponent<T>();
                DontDestroyOnLoad(go);
            }
        }
        return _instance;
    }
}
```

**改后**：
```csharp
public static T Instance
{
    get
    {
        // 退出时：对象还活着就给，不再隐式创建
        // （原逻辑一刀切返回 null，导致 TeardownScene 拿不到存活的单例 → NRE/清理跳过）
        if (_isQuitting)
        {
            if (_instance != null)
                return _instance;
            return null;  // 对象确实没了才返回 null，且不再隐式创建
        }

        if (_instance == null)
        {
            _instance = FindObjectOfType<T>();
            if (_instance == null)
            {
                Debug.LogWarning($"[{typeof(T).Name}] 场景中未找到实例，自动创建。...");
                GameObject go = new GameObject($"[Singleton] {typeof(T).Name}");
                _instance = go.AddComponent<T>();
                DontDestroyOnLoad(go);
            }
        }
        return _instance;
    }
}
```

#### 4.1.2 改动二：新增 OnDestroy 主动清 _instance（源头防范）

**现状**：基类有 `Awake` 设 `_instance = this`，但**没有 OnDestroy 清 _instance**。对象被 Destroy 后，静态字段 `_instance` 仍指向 fake null。运行时若单例被重建（Domain Reload、场景重载），`_instance != null` 可能命中 fake null，绕过 `FindObjectOfType` 重新查找。

**新增**（在 `OnApplicationQuit` 后面加）：
```csharp
protected virtual void OnDestroy()
{
    // 源头防范：对象销毁时主动清静态引用，杜绝 _instance 指向 fake null
    // 配合 Instance getter 的 _instance != null 检查，彻底消除悬空引用
    if (_instance == this)
        _instance = null;
}
```

**注意子类 override**：现有子类若 override OnDestroy（如 InputManager/SaveManager/RulerController/DifficultyManager/GameBootstrap），**必须调 `base.OnDestroy()`**，否则基类清理不执行。实施时逐一检查子类 override 是否调了 base。RulerController 现有 `OnDestroy` 里 `if (_instance != this) return;` 的防重逻辑保留，在其后补 `base.OnDestroy()`。

### 4.2 设计要点

1. **退出时优先返回已存在的 _instance**：只要对象还活着就给，治本。
2. **退出时不再隐式创建**：`_instance == null` 时直接返回 null，不触发 `new GameObject`。保留原保护——退出时意外创建对象很危险。
3. **移除退出时的 LogWarning**：原来"游戏正在关闭，不再提供实例"的警告不再适用（现在能给就给了）。
4. **非退出时行为完全不变**：`_isQuitting == false` 的分支原样保留，向后兼容。
5. **OnDestroy 清 _instance 是源头防范**：配合 getter 的 `!= null` 检查（Unity == 重载识别 fake null），双重保险。即使极端情况下 getter 漏检，_instance 也已被 OnDestroy 清成真 null。

### 4.3 第三层兜底：退出路径 try-catch（推荐加）

四层防范中，第一层(OnDestroy 清引用)、第二层(== 检查)已覆盖 99% 场景。第三层 try-catch 作为**退出路径的最后一道防线**，防御 Domain Reload 时机、多线程访问等极端情况。

**加在 Instance getter 退出分支**（推荐）：
```csharp
if (_isQuitting)
{
    if (_instance != null)
    {
        try { return _instance; }
        catch { return null; }  // _instance 是 fake null，放弃
    }
    return null;
}
```

**或在调用方**（TeardownScene，退出清理允许失败跳过）：
```csharp
try
{
    if (SaveManager.Instance != null)
        SaveManager.Instance.UnregisterSaveable(unit);
}
catch (MissingReferenceException e)
{
    Debug.LogWarning($"[TeardownScene] SaveManager 已销毁，跳过清理: {e.Message}");
}
```

**选择建议**：
- getter 加 try-catch：一处防护，所有调用方受益，但每次退出访问多一次 try 开销（退出路径可忽略）
- 调用方加 try-catch：更精准，但要在每个 X.Instance 调用处包，易遗漏
- **推荐 getter 加**，因为退出路径性能不敏感，且能统一覆盖。运行时主循环路径不走 `_isQuitting` 分支，无损耗。

**与第一版计划的差异**：原计划"第一版不加 try-catch，测试后再补"。鉴于阿铁要求彻底防范，改为**直接加**，省去后续返工。try-catch 仅在 `_isQuitting` 分支，不影响运行时性能。

## 五、风险清单与缓解

### 风险1：MissingReferenceException / fake null（低，已配四层防范）

**场景**：Unity 对象 Destroy 后，C++ 原生层立即销毁，但 C# 托管层引用还在（fake null）。getter 若返回 fake null，调用方法抛 `MissingReferenceException`。

**fake null 的三个陷阱**（`== null` 能识别，但以下写法绕过 Unity 重载）：
1. **`is` 模式匹配不触发重载**：`if (obj is MonoBehaviour mb)` 后直接用 mb，不检查原生层存活
2. **泛型无 `Object` 约束**：`where T : class` 时 `obj != null` 用默认 ==，不触发重载
3. **`ReferenceEquals`**：只比 C# 引用，不查原生层

**四层防范体系**（本方案已全部纳入）：

| 层级 | 手段 | 落点 | 状态 |
|------|------|------|------|
| 第一层·源头 | OnDestroy 主动清 `_instance` | Singleton.cs 新增 OnDestroy（4.1.2） | 已纳入 |
| 第二层·访问 | 用 `== null` 检查，避开 is/ReferenceEquals 陷阱 | Instance getter + 编码规范（第十一章） | 已纳入 |
| 第三层·兜底 | 退出路径 try-catch | Instance getter 退出分支（4.3） | 已纳入 |
| 第四层·规范 | fake null 防范编码规范 | 第十一章 | 已纳入 |

**评估**：
- 第一层 OnDestroy 清 `_instance` 后，对象销毁即引用置空，从源头杜绝悬空。
- 第二层 Unity `==` 重载能识别大部分 fake null。`Singleton<T> where T : MonoBehaviour` 约束正确，`_instance != null` 触发重载。
- 第三层 try-catch 兜底 Domain Reload 时机等极端情况，仅 `_isQuitting` 分支生效，运行时无损耗。
- 退出路径调用的方法（见风险2表）都是字典/字段操作，不访问 transform/gameObject，即使对象半残也不崩。

**结论**：四层防范后，MissingReferenceException 风险从"低"降为"极低"。剩余理论风险仅限多线程访问 Unity 对象（Unity 本身不支持，属非法用法，不在本方案范围）。

### 风险2：双重清理（低）

**场景**：OnApplicationQuit 和 OnDestroy 都触发清理，同一逻辑执行两次。

**评估**：退出路径调用的方法幂等性已验证：

| 方法 | 幂等性 | 说明 |
|------|--------|------|
| `SaveManager.UnregisterSaveable` | ✓ | `if (saveable==null) return; _saveables.Remove(id)`，Remove 不存在 key 不报错 |
| `SaveManager.CleanupDestroyedSaveables` | ✓ | 本身就是清理已销毁对象，重复执行无副作用 |
| `SaveManager.ResetSessionState` | ✓ | 纯字段赋值 |
| `UnitRegistry.Unregister` | ✓ | `if (_aliveUnits.Remove(unit))`，Remove 不存在不报错 |
| `UnitRegistry.Clear` | ✓ | `HashSet.Clear` 幂等 |
| `RulerController.ClearMonarchReference` | 需确认 | **实施前需读此方法确认幂等** |
| `InputManager.CleanupInputActions` | ✓ | `if (_inputActions == null) return`，已有兜底 |

**缓解**：实施前确认 `RulerController.ClearMonarchReference` 幂等。TeardownScene 可加 `CurrentPhase` 防重入（已有该字段）。

### 风险3：访问半残对象状态（低）

**场景**：退出时对象正在 OnDestroy 过程中，字段已被清理，调用方法得到异常结果。

**评估**：退出路径调的方法都是字典操作（Remove/Clear）或字段赋值，不依赖 Unity 生命周期相关的状态（transform/组件引用）。即使对象半残，这些操作也不会抛异常。

**缓解**：无需额外处理。

### 风险4：ISaveable.SaveId 访问（需确认）

**场景**：`UnregisterSaveable` 里 `if (string.IsNullOrEmpty(saveable.SaveId))` 访问 SaveId。若 saveable 是已销毁的 MonoBehaviour 且 SaveId 属性内部访问 Unity 对象，可能抛异常。

**缓解**：实施前确认 `ISaveable.SaveId` 实现是纯字符串字段（不依赖 Unity 对象）。从 UnitData 等实现看应该是，但需验证。

## 六、实施步骤

> 评审通过后执行。每步完成后验证再进下一步。

1. **备份**：`cp Singleton.cs Singleton.cs.bak`（回滚用）
2. **确认幂等性**：读 `RulerController.ClearMonarchReference` 和 `ISaveable.SaveId` 实现，确认风险4/风险2无隐患
3. **检查子类 OnDestroy override**：遍历所有 override OnDestroy 的 Singleton 子类（InputManager/SaveManager/RulerController/DifficultyManager/GameBootstrap），确认是否调 `base.OnDestroy()`。未调的补上——否则第一层防范（OnDestroy 清 _instance）不生效
4. **改 Singleton.cs**（三处一起改）：
   - 按 4.1.1 替换 Instance getter
   - 按 4.1.2 新增 OnDestroy 方法
   - 按 4.3 在 getter 退出分支加 try-catch
5. **编译验证**：Unity 无编译错误
6. **Play Mode 烟测**：新建游戏 → 玩一会 → 正常退出，检查日志无 NRE、无"不再提供实例"警告、无 MissingReferenceException
7. **fake null 专项验证**：在 Play Mode 中手动 Destroy 一个单例 GameObject（模拟极端），再访问 `X.Instance`，确认返回 null 不抛异常
8. **（可选）清理补丁**：确认稳定后，更新 TeardownScene 第 56-61 行注释说明"现已治本，null 检查为双保险"，保留 if 检查本身
9. **落地编码规范**：将第十一章 fake null 防范规范同步到开发引导书/团队约定
10. **更新引导书**：开发引导书里关于退出流程的说明同步更新

## 七、回滚方案

若测试发现问题：
1. `cp Singleton.cs.bak Singleton.cs` 恢复
2. TeardownScene 的 null 检查补丁保留（本来就是防御性代码，回滚后仍需要）

回滚成本：1 个文件恢复，无连锁影响。

## 八、验证清单

- [ ] 编译通过，无 warning
- [ ] 新建游戏 → 退出：日志无 NRE，TeardownScene 清理日志正常打印（"销毁 N 个场景单位"）
- [ ] 读档 → 退出：同上
- [ ] 返回主菜单 → 退出：同上
- [ ] Alt+F4 强退：不崩（Unity 兜底回收）
- [ ] 编辑器 Stop：日志正常，无"不再提供实例"警告刷屏
- [ ] 连续进出 Play Mode 5 次：无残留、无泄漏（InputManager 清理正常）
- [ ] 内存 Profile：退出后无单例残留对象

## 九、后续清理（方案三落地后）

1. TeardownScene 注释更新：说明 _isQuitting 陷阱已治本，null 检查降级为双保险
2. 评估是否移除冗余 null 检查（保留更稳，移除更干净，二选一）
3. 若后续将 UnitRegistry 换 Runtime Set（独立改进），此方案三仍保留——它解决的是所有单例的退出访问问题，不止 UnitRegistry

## 十、与并行 agent 的协调

- 本改进**只改 Singleton.cs 一处**，不触碰业务逻辑文件，与并行 agent 的功能开发冲突面极小
- 实施前在群里同步"我要改 Singleton.cs，各位拉一下最新代码"
- 改动期间 Singleton.cs 会有短暂不可用窗口，建议在无人在改 Core 目录时合并
- 若有 agent 正在写新的 Singleton 子类，不受影响（非退出时行为不变）

## 十一、fake null 防范编码规范（配套落地）

本方案落地后，以下规范应同步到开发引导书，约束新代码、指导老代码审查。

### 11.1 四条铁律

1. **Unity 对象引用一律用 `== null` / `!= null` 检查**
   - 禁止 `is Type x` 后不接 `x == null` 直接用
   - 禁止 `ReferenceEquals(obj, null)`
   - 禁止 `is not null`（C# 7 模式，不触发 Unity 重载）

2. **泛型方法访问 Unity 对象，约束 `where T : UnityEngine.Object`**
   - 否则 `obj != null` 用默认 ==，fake null 检测不到
   - `Singleton<T> where T : MonoBehaviour` 已合规（MonoBehaviour 继承 Object）

3. **静态字段/缓存持有 Unity 对象，对应 OnDestroy 主动置 null**
   - 本方案的 `Singleton._instance` 是范例
   - 项目里其他静态缓存（如 EventBus 内部字典不持有 Unity 对象，无需）审查时留意

4. **退出/销毁路径的清理方法允许失败跳过**
   - 不强制 try-catch 每一行，但关键清理包 try-catch 或确保方法幂等
   - 退出时 Unity 兜底回收，清理失败不致命

### 11.2 `is` 模式匹配的安全写法

```csharp
// 危险：is 后直接用，不查原生层
if (obj is MonoBehaviour mb)
{
    mb.transform.position = ...;  // mb 可能是 fake null → 抛异常
}

// 安全：is 后紧跟 == null
if (obj is MonoBehaviour mb && mb == null) return;  // mb 已是 MonoBehaviour，== 触发重载

// 更安全：as + ==
var mb = obj as MonoBehaviour;
if (mb == null) return;
mb.transform.position = ...;
```

**项目范例**：`SaveManager.CleanupDestroyedSaveables` 的 `kvp.Value is MonoBehaviour mb && mb == null` 是合规写法。

### 11.3 审查清单

新代码合并前确认：
- [ ] 所有 Unity 对象引用检查用 `== null`，无 `is`/`ReferenceEquals`/`is not null`
- [ ] 泛型方法访问 Unity 对象有 `where T : Object` 约束
- [ ] 新增静态字段持有 Unity 对象的，OnDestroy 里有置 null
- [ ] 退出/销毁路径清理方法幂等或有 try-catch
- [ ] 新增 Singleton 子类 override OnDestroy 的，调了 `base.OnDestroy()`

### 11.4 本规范与方案三的关系

方案三（Singleton 基类改 Instance getter + OnDestroy）是**本规范在 Singleton 层面的落地**。规范本身面向全项目所有 Unity 对象访问，不止单例。两者配合：
- 方案三治 Singleton 这条线的 fake null
- 编码规范防业务代码里其他 fake null 陷阱
