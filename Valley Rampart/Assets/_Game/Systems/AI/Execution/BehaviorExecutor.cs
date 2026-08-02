using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - BehaviorExecutor 行为执行器
//  详见 3.0.1_2_输入输出决定层设计.md §13.4
//  契约：持执行进度态非决策态，事件反馈环闭环
//  可持有：当前cmd幂等续做、到达检测、速度插值、锚点存活检查
//  禁止：自主切模块、决定到达后行为、改cmd参数
// ============================================================================

/// <summary>
/// BehaviorExecutor（§13.4）。
/// 5 模块执行器：MoveTowards/RetreatMove/WorkAt/FollowAnchor/Idle。
/// 到达检测用 arrivalThreshold × cellSize；速度插值 0.2s lerp。
/// 事件通过 IExecutorEventReceiver 同步回调 NPCBrain（本地主），NPCBrain 再 Publish EventBus（辅）。
/// </summary>
public class BehaviorExecutor
{
    private readonly UnitController _controller;
    private readonly IDamageable _self;
    private readonly IExecutorEventReceiver _receiver;
    private readonly AttentionTuningConfig _config;

    private BehaviorCommand _currentCmd;
    private bool _hasCmd;
    private bool _arrivedAtFocus;
    private float _durationTimer;
    private float _tacticalRetreatTraveled;  // 战术短撤已移动距离

    // 3.0.1_4 §6.3 漫游状态（Executor 持有随机点，不依赖每 tick L3 重算）
    private Vector2 _wanderTarget;
    private bool _wanderHasTarget;
    private bool _wanderStaying;
    private float _wanderStayTimer;

    // 速度插值
    private float _currentSpeed;
    private const float SpeedLerpTime = 0.2f;

    /// <summary>是否到达焦点目标（反馈到 ctx.ArrivedAtFocus，供 L2 三维表查表）</summary>
    public bool ArrivedAtFocus => _arrivedAtFocus;

    public BehaviorExecutor(UnitController controller, IDamageable self,
                            IExecutorEventReceiver receiver, AttentionTuningConfig config)
    {
        _controller = controller;
        _self = self;
        _receiver = receiver;
        _config = config;
    }

    /// <summary>
    /// 执行 BehaviorCommand。
    /// 幂等续做：相同 Module + 相同 TargetPos -> 续做不切。
    /// </summary>
    public void Execute(in BehaviorCommand cmd, float dt, float cellSize)
    {
        if (_self == null || _self.CurrentHp <= 0) return;

        // 模块切换时重置跨模块状态（漫游随机点/战术短撤里程不跨模块续用）
        if (_hasCmd && cmd.Module != _currentCmd.Module)
        {
            ResetWanderState();
            _tacticalRetreatTraveled = 0f;
        }

        // 速度插值（0.2s lerp）
        _currentSpeed = Mathf.Lerp(_currentSpeed, cmd.Speed, dt / SpeedLerpTime);

        switch (cmd.Module)
        {
            case BehaviorModule.MoveTowards:
                ExecuteMoveTowards(in cmd, cellSize);
                break;
            case BehaviorModule.RetreatMove:
                ExecuteRetreatMove(in cmd, dt, cellSize);
                break;
            case BehaviorModule.WorkAt:
                ExecuteWorkAt(in cmd, cellSize);
                break;
            case BehaviorModule.FollowAnchor:
                ExecuteFollowAnchor(in cmd, cellSize);
                break;
            case BehaviorModule.Idle:
                ExecuteIdle(in cmd, dt);
                break;
            case BehaviorModule.Wander:  // 3.0.1_4 §6.3
                ExecuteWander(in cmd, dt, cellSize);
                break;
        }

        _currentCmd = cmd;
        _hasCmd = true;
    }

    private void ExecuteMoveTowards(in BehaviorCommand cmd, float cellSize)
    {
        Vector2 myPos = _self.GetPosition();
        float dist = Vector2.Distance(myPos, cmd.TargetPos);
        float arrivalDist = _config.arrivalThreshold * cellSize;

        if (dist <= arrivalDist)
        {
            // 到达焦点目标
            _arrivedAtFocus = true;
            _controller.MoveTowards(myPos);  // 停在原地
            _receiver?.OnArrived(myPos, BehaviorModule.MoveTowards);
        }
        else
        {
            _arrivedAtFocus = false;
            _controller.MoveTowards(cmd.TargetPos);
        }
    }

    private void ExecuteRetreatMove(in BehaviorCommand cmd, float dt, float cellSize)
    {
        if (cmd.IsTacticalRetreatEquivalent())
        {
            // 战术短撤：Direction + Distance（撞墙/到达即停）
            _arrivedAtFocus = false;
            Vector2 dir = cmd.Direction;
            if (dir.sqrMagnitude > 0.001f)
            {
                _controller.Move(dir.normalized, run: true);
                // 简化：按速度累积距离，达 Distance 发 MoveComplete
                _tacticalRetreatTraveled += _currentSpeed * dt;
                if (_tacticalRetreatTraveled >= cmd.Distance)
                {
                    _tacticalRetreatTraveled = 0f;
                    _receiver?.OnMoveComplete(_self.GetPosition());
                }
            }
        }
        else
        {
            // 战略撤退：MoveTowards(HomePoint)，到达发 MoveComplete
            Vector2 myPos = _self.GetPosition();
            float dist = Vector2.Distance(myPos, cmd.TargetPos);
            float arrivalDist = _config.arrivalThreshold * cellSize;

            if (dist <= arrivalDist)
            {
                _controller.MoveTowards(myPos);
                _receiver?.OnMoveComplete(myPos);
            }
            else
            {
                _controller.MoveTowards(cmd.TargetPos);
            }
        }
    }

    private void ExecuteWorkAt(in BehaviorCommand cmd, float cellSize)
    {
        Vector2 myPos = _self.GetPosition();
        float dist = Vector2.Distance(myPos, cmd.TargetPos);
        float arrivalDist = _config.arrivalThreshold * cellSize;

        if (dist <= arrivalDist)
        {
            // 边沿触发：仅刚到达（由未到达→到达）时收取一次，防重复 Harvest
            if (!_arrivedAtFocus)
            {
                _arrivedAtFocus = true;
                // 3.3.5 资源流转：搬运任务到达 → Harvest 入国库（原占位"原地待机"扩展）
                if (cmd.HarvestTarget != null)
                    cmd.HarvestTarget.Harvest();
            }
        }
        else
        {
            _arrivedAtFocus = false;
            _controller.MoveTowards(cmd.TargetPos);
        }
    }

    private void ExecuteFollowAnchor(in BehaviorCommand cmd, float cellSize)
    {
        // FollowAnchor 永不到达（持续过程），ArrivedAtFocus 保持 false
        _arrivedAtFocus = false;

        if (cmd.Anchor == null || cmd.Anchor.CurrentHp <= 0)
        {
            _receiver?.OnAnchorLost();
            return;
        }

        // 3.0.1_3：槽位化跟随时目标 = SlotWorld（锚点位置 + SlotOffset × cellSize，L3 已算）
        // 非编队跟随 SlotOffset=zero，SlotWorld=zero，退化为原松散跟随（用 KeepDistance 判定）
        if (cmd.IsFormationSlot)
        {
            Vector2 myPos = _self.GetPosition();
            Vector2 slotWorld = cmd.SlotWorld;
            float dist = Vector2.Distance(myPos, slotWorld);
            float arrivalDist = _config.arrivalThreshold * cellSize;

            if (dist > arrivalDist)
            {
                // 未到槽位 -> 向槽位移动（cell 吸附，1D 横版 y 由 MoveTowards 夹取）
                _controller.MoveTowards(slotWorld);
            }
            else
            {
                // 到槽位 -> 停
                _controller.MoveTowards(myPos);
            }
        }
        else
        {
            // 原松散跟随语义（工人随军等非编队场景）
            Vector2 anchorPos = cmd.Anchor.transform.position;
            Vector2 myPos = _self.GetPosition();
            float dist = Vector2.Distance(myPos, anchorPos);

            if (dist > cmd.KeepDistance + _config.arrivalThreshold * cellSize)
            {
                // 超出保持距离 -> 靠近
                _controller.MoveTowards(anchorPos);
            }
            else
            {
                // 在保持距离内 -> 停
                _controller.MoveTowards(myPos);
            }
        }
    }

    private void ExecuteIdle(in BehaviorCommand cmd, float dt)
    {
        _arrivedAtFocus = true;  // Idle = 已在目标点
        _controller.MoveTowards(_self.GetPosition());  // 停

        if (cmd.Duration > 0f)
        {
            _durationTimer += dt;
            if (_durationTimer >= cmd.Duration)
            {
                _durationTimer = 0f;
                _receiver?.OnMoveComplete(_self.GetPosition());
            }
        }
    }

    // ===== 3.0.1_4 §6.3 漫游 =====

    /// <summary>
    /// 漫游执行：HomePoint 周围随机取点 -> 走到 -> 停留 wanderStayTime -> 取新点（走走停停循环）。
    /// 随机点由 Executor 持有（_wanderTarget），不依赖每 tick L3 重算，避免目标抖动。
    /// </summary>
    private void ExecuteWander(in BehaviorCommand cmd, float dt, float cellSize)
    {
        _arrivedAtFocus = false;  // 漫游是持续过程，永不到达语义

        if (!_wanderHasTarget)
        {
            _wanderTarget = PickWanderPoint(cmd.TargetPos, cmd.WanderRadius);
            _wanderHasTarget = true;
            _wanderStaying = false;
        }

        if (_wanderStaying)
        {
            // 到点停留（走走停停），停满后取新点
            _controller.MoveTowards(_self.GetPosition());
            _wanderStayTimer += dt;
            if (_wanderStayTimer >= cmd.Duration)
            {
                _wanderStaying = false;
                _wanderStayTimer = 0f;
                _wanderTarget = PickWanderPoint(cmd.TargetPos, cmd.WanderRadius);
            }
        }
        else
        {
            Vector2 myPos = _self.GetPosition();
            float dist = Vector2.Distance(myPos, _wanderTarget);
            float arrivalDist = _config.arrivalThreshold * cellSize;

            if (dist <= arrivalDist)
            {
                // 到达 -> 开始停留
                _wanderStaying = true;
                _wanderStayTimer = 0f;
                _controller.MoveTowards(myPos);
            }
            else
            {
                _controller.MoveTowards(_wanderTarget);
            }
        }
    }

    /// <summary>1D 横版随机取点：x 轴 ±radius 随机，y 固定漫游中心（地面基线由 MoveTowards 夹取）。</summary>
    private Vector2 PickWanderPoint(Vector2 center, float radius)
    {
        float rx = UnityEngine.Random.Range(-radius, radius);
        return new Vector2(center.x + rx, center.y);
    }

    private void ResetWanderState()
    {
        _wanderHasTarget = false;
        _wanderStaying = false;
        _wanderStayTimer = 0f;
        _wanderTarget = Vector2.zero;
    }

    public void Stop()
    {
        _hasCmd = false;
        _arrivedAtFocus = false;
        _durationTimer = 0f;
        _tacticalRetreatTraveled = 0f;
        ResetWanderState();
    }

    public void Reset()
    {
        _hasCmd = false;
        _arrivedAtFocus = false;
        _durationTimer = 0f;
        _tacticalRetreatTraveled = 0f;
        _currentSpeed = 0f;
        ResetWanderState();
    }
}

/// <summary>BehaviorCommand 扩展：判断是否战术短撤（Direction+Distance 分支）</summary>
public static class BehaviorCommandExtensions
{
    /// <summary>战术短撤判定：Direction 非零且有 Distance</summary>
    public static bool IsTacticalRetreatEquivalent(this in BehaviorCommand cmd)
    {
        return cmd.Direction.sqrMagnitude > 0.001f && cmd.Distance > 0f;
    }
}
