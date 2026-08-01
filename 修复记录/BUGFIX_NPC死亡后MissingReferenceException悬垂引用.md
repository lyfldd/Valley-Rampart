# BUGFIX：NPC 死亡后 MissingReferenceException（IDamageable 悬垂引用）

> 2026-08-02 AI 对战验证时触发：一个 NPC 死亡后，其他 NPC 大脑仍访问已销毁的 UnitController，持续抛 MissingReferenceException。

## 现象

进 Play Mode 让 AI 对战，某 NPC 死亡后报错：

```
MissingReferenceException: The object of type 'UnitController' has been destroyed but you are still trying to access it.
UnitController.GetPosition () (UnitController.cs:52)
L2PostureDecider.DecideRetreatSubtype (FactorContext& ctx, PostureDecision& posture) (L2PostureDecider.cs:113)
L2PostureDecider.Decide (FactorContext& ctx) (L2PostureDecider.cs:36)
NPCBrain.Think () (NPCBrain.cs:376)
NPCBrain.Update () (NPCBrain.cs:261)
```

特征：**某个 NPC 死后报错，之后持续报**（不是单帧偶发）。

## 根因分析

### 直接触发点

`L2PostureDecider.DecideRetreatSubtype` 的战术短撤分支：

```csharp
IDamageable enemy = FindActiveThreatEnemy(in ctx);   // 从 ThreatStimulus.Enemy 取敌人引用
if (enemy != null)                                     // ❌ 这里 != null 是"假通过"
{
    posture.MoveTarget = (ctx.SelfPos - enemy.GetPosition()).normalized;  // 💥 已销毁对象
}
```

`enemy` 是 `IDamageable` **接口引用**（来自 `ThreatStimulus.Enemy`，指向 UnitController）。目标 NPC 死亡销毁后，接口引用仍指向旧对象——`!= null` 判不出来，`GetPosition()` 一调就崩。

### 结构性根因：接口引用不触发 Unity 销毁检测

这是**接口调用错误**，核心是 C# 接口引用与 Unity 对象的生命周期语义断裂：

- `IDamageable` 是纯 C# 接口，`enemy` 编译类型是接口。`enemy != null` 是**普通引用比较**，走 C# 虚表，**不会调用 Unity 的 `Object == null` 重载**（该重载在 `UnityEngine.Object` 类型上，接口变量静态类型不是 Object，不触发）。
- 而 Unity 的 `Destroy(gameObject)` 是**延迟到帧末才真正销毁**，销毁后对象在托管层仍是"非 null 的假对象"，只有 `Object == null` 能识别。
- 结论：**凡是跨帧/跨组件持有 `IDamageable` 引用，目标销毁后必然留下悬垂引用**，任何"判空后访问"都是定时炸弹。

### 为什么"死了之后持续报"：引用生命周期没人管（隔离缺失）

顺着引用持有链追查，发现**不是一处兜底能解决的**，多处缓存引用都没有随目标死亡清理：

| 持有者 | 缓存引用 | 死亡清理 | 状态 |
|--------|----------|----------|------|
| `AttentionSystem._threatStimuli` | `ThreatStimulus.Enemy` | `RemoveThreatStimuli(source)` **已提供** | ❌ **全项目无人调用** |
| `NPCBrain._lastAggressor`（受击溯源） | 攻击者引用 | 无 | ❌ 跨帧缓存，死后悬垂 |
| `NPCBrain._currentAttackTarget` | 攻击目标引用 | 仅 StopAttacking 时清 | ❌ 无死亡回调 |
| `NPCBrain._lastCtx`（调试缓存） | `PostureDecision.TacticalRetreatEnemy` | 无 | ❌ 缓存决策结果含活引用 |
| `DamageSystem._registrations` | target 引用 | `OnUnitDied` 订阅清理 | ✅ 唯一做对了的 |

**关键证据**：`AttentionSystem.RemoveThreatStimuli(object source)` 的注释写着"敌人死亡/离开时调"，接口都设计好了，但 grep 全项目**零调用**——敌人死亡后它的 ThreatStimulus 仍留在其他 NPC 的注意力系统里，焦点评分继续引用它。

这就是"隔离没做好"的实质：**引用失效靠"持有方自觉在用前防御"，而不是靠"死亡事件驱动源头清理"**。设计者预期了清理接口，却没接线到死亡事件，导致清理契约悬空。

## 临时修复（已实施，防御性兜底）

在使用点加销毁防御——先 `as UnityEngine.Object` 再判空，触发 Unity 销毁检测：

```csharp
private static bool IsDestroyed(IDamageable d)
{
    var uo = d as UnityEngine.Object;
    return uo == null;  // UnityEngine.Object==null 触发销毁检测
}
```

已接入的位置：
- `L2PostureDecider.cs`：`DecideRetreatSubtype` 战术短撤判断 + `FindActiveThreatEnemy` 内 `ts.Enemy` 判断
- `NPCBrain.cs`：`UpdatePerception` 感知循环、受击溯源 `_lastAggressor`、攻击注册两条路径

**⚠️ 这只是止血，不是治本**：引用还在，只是访问前拦住了。系统里所有 `IDamageable` 访问点都要加一遍，漏一处就崩；而且对象泄漏在注意力系统/缓存里，会随对战时间膨胀。

## 彻底解决（按优先级，不是简单兜底）

### 方案一：死亡事件驱动引用清理（治本主方案，推荐先做）

把"使用点防御"改为"源头清理"——所有持有 `IDamageable` 引用的系统**订阅 `UnitDiedEvent`，目标死亡时主动清引用**。DamageSystem 已示范（`OnUnitDied` 清三表），其余照抄：

1. **NPCBrain 订阅 `UnitDiedEvent`**：死亡回调里清理自身缓存
   - `_lastAggressor` 若 == 死者 → 置 null
   - `_currentAttackTarget` 若 == 死者 → 置 null + `DamageSystem.Unregister`
   - 调 `_attention.RemoveThreatStimuli(evt.Unit)`——**把已设计好但没接线的接口接上**
   - `_nearbyEnemies` / `_nearbyAllies` 移除死者（可选，反正每帧重填）
2. **AttentionSystem** 保持 `RemoveThreatStimuli`，由 NPCBrain 死亡回调触发，刺激源在源头消失，焦点自然不再引用死者。

效果：死亡即失效，使用点无需任何 `IsDestroyed` 防御。

### 方案二：统一安全访问封装（契约化，消除"漏一处就崩"）

若方案一无法覆盖全部路径（如历史缓存 `_lastCtx`），提供**唯一**安全访问入口，强制所有跨帧 `IDamageable` 访问走它：

```csharp
/// <summary>IDamageable 安全访问门卫：Unity 对象销毁检测的唯一权威入口。</summary>
public static class DamageableRef
{
    /// <summary>true=引用有效且存活。</summary>
    public static bool IsAlive(IDamageable d)
        => (d as UnityEngine.Object) != null && d.CurrentHp > 0;

    /// <summary>安全取位置（无效返回 false + out 兜底值）。</summary>
    public static bool TryGetPosition(IDamageable d, out Vector2 pos)
    {
        pos = default;
        if ((d as UnityEngine.Object) == null) return false;
        pos = d.GetPosition();
        return true;
    }
}
```

规则：**跨帧持有的 IDamageable 一律经 `DamageableRef` 访问，禁止裸调接口成员**。比散落各处的私有 `IsDestroyed` 更统一、可审计。

### 方案三：架构隔离改进（根治隔离问题）

1. **决策缓存只存值、不存活引用**：`_lastCtx` 是给调试面板读的，把 `PostureDecision.TacticalRetreatEnemy`（活引用）改为存**位置快照**（`Vector2`）或干脆从 `TacticalRetreat` 决策里去掉，需要方向时用 `MoveTarget` 本身（已有）。调试面板不需要敌人引用，只需要方向/位置。
2. **注意力系统契约补全**：`ThreatStimulus` 作为"当帧评分输入"设计是对的（每帧重建），但 `_currentFocus` 跨帧保留时若引用了旧刺激源实例，需确保"实例失效"（`RemoveThreatStimuli` 已覆盖）。把"敌人死亡 → 清刺激源"写入 NPCBrain 的生命周期契约（OnEnable 订阅 / OnDisable 退订）。
3. **IDamageable 接口层根治（可选远期）**：接口加 `bool IsValid { get; }`，实现者（UnitController）返回 `this != null`——把 Unity 销毁检测从实现层暴露给接口层，调用方统一 `if (enemy.IsValid)`。改动面大，收益是接口语义自洽，建议在下次大重构时做。

## 验证

1. 临时修复后：进 Play Mode AI 对战，杀一批 NPC，无 MissingReferenceException，Console 0 error。
2. 方案一后：断点/日志确认 NPC 死亡时 `_lastAggressor`/`_currentAttackTarget` 立即置 null、`RemoveThreatStimuli` 被调用，注意力系统不再含死者刺激源。
3. 方案三后：调试面板正常显示撤退方向（不再依赖活引用）。

## 结论

- **是接口调用错误吗**：是，但不只是"这一处调用没判空"。根因是 `IDamageable` 接口引用的 `!= null` 不触发 Unity 销毁检测这一**语言/引擎语义断裂**，任何跨帧持有该接口的系统都会中招。
- **是没做好隔离吗**：是。引用生命周期管理缺失——`RemoveThreatStimuli` 设计好了却没接死亡事件，`_lastCtx` 把活引用当缓存存，`_lastAggressor`/`_currentAttackTarget` 无死亡回调。
- **兜底 vs 治本**：`IsDestroyed` 兜底只能止血；**方案一（死亡事件驱动清理）** 是正解，**方案二（统一门卫）** 补漏，**方案三（缓存存值不存活引用）** 根治隔离。三者一起做才能彻底摆脱这类崩溃。
