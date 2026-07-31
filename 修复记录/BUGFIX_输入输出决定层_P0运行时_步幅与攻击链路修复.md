# 输入输出决定层 P0 运行时 Bug 修复记录

> 日期：2026-07-31
> 涉及文件：`NPCBrain.cs`、`AttentionSystem.cs`、`L1FocusEvaluator.cs`
> 触发场景：3.4 验证阶段，测试单位生成完成（2近战+1远程 vs 3敌人含1远程 + 1工人旁观）

---

## 背景

3.0.1_2 输入输出决定层 P0 五阶段实装完成后，进入运行时验证。场景生成 NPC 后观察到两个异常：
1. NPC 移动步幅极小，每秒只动一点点
2. NPC 完全没有攻击逻辑（不注册攻击）

---

## Bug ①：tick 分片导致移动步幅骤减

### 现象

NPC 生成后以极小步幅移动，视觉上几乎原地不动。

### 根因

```
新版 tick 分片（决策12）将 Think 拆为 5 帧 shard
  └─ Think 每 0.5s 才执行一次（s_globalTickFrame % 5 == _shardIndex）
       └─ Execute 在 Think 内部调用
            └─ MoveTowards 用 Time.deltaTime（~0.016s）算位移
                 └─ 每 0.5s 只调一次 MoveTowards，位移 = speed × 0.016
                      └─ 实际移动量 = 预期的 1/30
```

分片本意是平摊决策开销，但 Execute（移动执行）被错误地一起分片了。移动是持续行为，必须每帧调用才能正常位移。

### 修复

**文件**：`NPCBrain.cs` - `Update()` 方法 + `Think()` 方法

分离 Think 和 Execute 的调用频率：
- Think（决策）仍走分片，每 0.5s 产出新 `BehaviorCommand`，缓存到 `_lastCmd`
- Execute（移动）提到 `Update()` 每帧调用，复用最近一次 Think 的 `_lastCmd`

```csharp
// Update() 中
// Think 分片（每 0.5s 一次，产出新 cmd）
if (s_globalTickFrame % Mathf.Max(1, _config.thinkShardCount) == _shardIndex)
{
    Think();  // 内部缓存 _lastCmd = cmd，不再调 Execute
}

// Execute 每帧调用（用最近一次 Think 的 cmd，持续移动）
if (_executor != null)
{
    _executor.Execute(in _lastCmd, Time.deltaTime, GetCellSize());
}
```

```csharp
// Think() 中（原 ⑤ Execute 改为缓存）
_lastCmd = cmd;  // 替代原来的 _executor.Execute(in cmd, ...)
```

新增字段：`private BehaviorCommand _lastCmd;`（管线中间产物缓存区）

---

## Bug ②：攻击链路因 Focus 类型判断失效而中断

### 现象

NPC 生成后不攻击敌人，`UpdateCombatRegistration` 永远不注册攻击。

### 根因

```
L1FocusEvaluator.Evaluate()
  └─ Focus = focus.Source as IStimulus
       └─ Focus.Source 存的是什么？
            └─ AttentionSystem.SelectTopFocus() 构造 Focus 时传的是 top.Source
                 └─ ThreatStimulus.Source = source ?? enemy = IDamageable（业务引用）
                      └─ 不是 ThreatStimulus 实例本身！
                           └─ as IStimulus 永远返回 null
                                └─ focusDecision.Focus = null
                                     └─ NPCBrain.UpdateCombatRegistration()
                                          └─ focus.Focus is ThreatStimulus → false
                                               └─ 永远不注册攻击
```

`Focus.Source` 的语义是"刺激源关联的业务对象"（敌人引用、任务目标等），不是刺激源实例本身。两者类型不同，`as IStimulus` 转换失败。

### 修复

**文件**：`AttentionSystem.cs`

新增 `CurrentStimulus` 属性，在 `SelectTopFocus()` / `SelectTopTaskLayer()` 内记录胜出的 IStimulus 实例：

```csharp
private IStimulus _currentStimulus;
public IStimulus CurrentStimulus => _currentStimulus;

private Focus SelectTopFocus()
{
    _currentStimulus = null;  // 重置
    // 威胁层
    var top = GetTopThreat();
    _currentStimulus = top;  // 记录 stimulus 实例
    // ... 任务层由 SelectTopTaskLayer 设置
    // ... 感知层同理
}

private Focus SelectTopTaskLayer()
{
    IStimulus bestStimulus = null;
    // 各分支胜出时同步更新 bestStimulus
    // TaskStimulus: bestStimulus = ts;  (struct 装箱，P0 接受)
    // SafetyStimulus: bestStimulus = ss;  (class 不装箱)
    // FollowStimulus / HoldPositionStimulus 同理
    _currentStimulus = bestStimulus;
    return best;
}
```

**文件**：`L1FocusEvaluator.cs`

改从 `attention.CurrentStimulus` 取刺激源实例：

```csharp
// 修复前
Focus = focus.Source as IStimulus,  // 永远 null

// 修复后
Focus = attention.CurrentStimulus,  // 直接取 AttentionSystem 记录的实例
```

---

## 影响范围

| Bug | 影响系统 | 严重度 | 修复方式 |
|-----|---------|:---:|---------|
| ① 步幅小 | 全部 NPC 移动 | P0 阻断 | Think/Execute 分离，Execute 提至每帧 |
| ② 不攻击 | 全部 NPC 战斗 | P0 阻断 | AttentionSystem 暴露 CurrentStimulus |

---

## 验证

- Unity 编译零错误
- 待运行时验证：NPC 正常速度移动 + 接敌后注册攻击
