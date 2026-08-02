// ============================================================================
//  M2 Headless 模拟器 - SimExecutor 行为执行器（复刻 BehaviorExecutor）
//  04_模拟器规格.md §三：
//    - 抵达判定：BehaviorExecutor.cs:100 0.3×cellSize（arrivalThreshold × cellSize）
//    - 速度插值：BehaviorExecutor.cs:68 Lerp(dt/0.2)（SpeedLerpTime=0.2）
//    - 战术短撤：Direction+Distance 累积里程（BehaviorExecutor.cs:116-134）
//    - 漫游：HomePoint 周围随机取点（Unity 用 UnityEngine.Random -> sim 用 IRngPort/SimRng）
//  5 模块 + Wander：MoveTowards/RetreatMove/WorkAt/FollowAnchor/Idle/Wander。
//  事件通过 SimBrain 同步回调（对应壳 IExecutorEventReceiver 语义）。
//  注：壳 BehaviorCommandExtensions.IsTacticalRetreatEquivalent 属壳代码，sim 内联同逻辑。
// ============================================================================

/// <summary>
/// 行为执行器（复刻壳 BehaviorExecutor）。
/// 持执行进度态非决策态（幂等续做/到达检测/速度插值）。
/// </summary>
public sealed class SimExecutor
{
    private readonly SimUnit _self;
    private readonly SimBrain _receiver;   // IExecutorEventReceiver 角色
    private readonly SimRng _rng;          // 漫游取点（Unity UnityEngine.Random 替代）
    private readonly TuningSnapshot _config;

    private BehaviorCommand _currentCmd;
    private bool _hasCmd;
    private bool _arrivedAtFocus;
    private float _durationTimer;
    private float _tacticalRetreatTraveled;

    private Vector2X _wanderTarget;
    private bool _wanderHasTarget;
    private bool _wanderStaying;
    private float _wanderStayTimer;

    private float _currentSpeed;
    private const float SpeedLerpTime = 0.2f;

    /// <summary>是否到达焦点目标（反馈到 ctx.ArrivedAtFocus，L2 三维表查表）。</summary>
    public bool ArrivedAtFocus => _arrivedAtFocus;

    public SimExecutor(SimUnit self, SimBrain receiver, SimRng rng, TuningSnapshot config)
    {
        _self = self;
        _receiver = receiver;
        _rng = rng;
        _config = config;
    }

    /// <summary>执行 BehaviorCommand（幂等续做：相同 Module+TargetPos 续做不切）。</summary>
    public void Execute(in BehaviorCommand cmd, float dt, float cellSize)
    {
        if (_self == null || !_self.IsAlive || _self.CurrentHp <= 0) return;

        // 模块切换时重置跨模块状态（漫游随机点/战术短撤里程不跨模块续用）
        if (_hasCmd && cmd.Module != _currentCmd.Module)
        {
            ResetWanderState();
            _tacticalRetreatTraveled = 0f;
        }

        // 速度插值（0.2s lerp，04 §三）
        _currentSpeed = MathfX.Lerp(_currentSpeed, cmd.Speed, dt / SpeedLerpTime);

        switch (cmd.Module)
        {
            case BehaviorModule.MoveTowards:
                ExecuteMoveTowards(in cmd, dt, cellSize);
                break;
            case BehaviorModule.RetreatMove:
                ExecuteRetreatMove(in cmd, dt, cellSize);
                break;
            case BehaviorModule.WorkAt:
                ExecuteWorkAt(in cmd, dt, cellSize);
                break;
            case BehaviorModule.FollowAnchor:
                ExecuteFollowAnchor(in cmd, dt, cellSize);
                break;
            case BehaviorModule.Idle:
                ExecuteIdle(in cmd, dt);
                break;
            case BehaviorModule.Wander:
                ExecuteWander(in cmd, dt, cellSize);
                break;
        }

        _currentCmd = cmd;
        _hasCmd = true;
    }

    private void ExecuteMoveTowards(in BehaviorCommand cmd, float dt, float cellSize)
    {
        Vector2X myPos = _self.Position;
        float dist = Vector2X.Distance(myPos, cmd.TargetPos);
        float arrivalDist = _config.arrivalThreshold * cellSize;   // 0.3 × cellSize

        if (dist <= arrivalDist)
        {
            _arrivedAtFocus = true;
            _self.Stop();
            _receiver?.OnArrived(myPos, BehaviorModule.MoveTowards);
        }
        else
        {
            _arrivedAtFocus = false;
            _self.MoveTowards(cmd.TargetPos, _currentSpeed, dt);
        }
    }

    private void ExecuteRetreatMove(in BehaviorCommand cmd, float dt, float cellSize)
    {
        // 战术短撤判定内联壳扩展 IsTacticalRetreatEquivalent：Direction 非零且有 Distance
        bool isTactical = cmd.Direction.sqrMagnitude > 0.001f && cmd.Distance > 0f;

        if (isTactical)
        {
            // 战术短撤：Direction + Distance（撞墙/到达即停）
            _arrivedAtFocus = false;
            Vector2X dir = cmd.Direction;
            if (dir.sqrMagnitude > 0.001f)
            {
                // 向 dir 方向移动 _currentSpeed×dt；大步长目标（1D 中 y=0 无影响）
                Vector2X dest = _self.Position + dir * 1e6f;
                _self.MoveTowards(dest, _currentSpeed, dt);
                _tacticalRetreatTraveled += _currentSpeed * dt;
                if (_tacticalRetreatTraveled >= cmd.Distance)
                {
                    _tacticalRetreatTraveled = 0f;
                    _receiver?.OnMoveComplete(_self.Position);
                }
            }
        }
        else
        {
            // 战略撤退：MoveTowards(HomePoint)，到达发 MoveComplete
            Vector2X myPos = _self.Position;
            float dist = Vector2X.Distance(myPos, cmd.TargetPos);
            float arrivalDist = _config.arrivalThreshold * cellSize;

            if (dist <= arrivalDist)
            {
                _self.Stop();
                _receiver?.OnMoveComplete(myPos);
            }
            else
            {
                _self.MoveTowards(cmd.TargetPos, _currentSpeed, dt);
            }
        }
    }

    private void ExecuteWorkAt(in BehaviorCommand cmd, float dt, float cellSize)
    {
        // 战斗场景无 IHarvestable 交互（HarvestTarget 忽略），复刻到达检测
        Vector2X myPos = _self.Position;
        float dist = Vector2X.Distance(myPos, cmd.TargetPos);
        float arrivalDist = _config.arrivalThreshold * cellSize;

        if (dist <= arrivalDist)
        {
            // 边沿触发：仅刚到达时置位
            if (!_arrivedAtFocus)
                _arrivedAtFocus = true;
        }
        else
        {
            _arrivedAtFocus = false;
            _self.MoveTowards(cmd.TargetPos, _currentSpeed, dt);
        }
    }

    private void ExecuteFollowAnchor(in BehaviorCommand cmd, float dt, float cellSize)
    {
        // FollowAnchor 永不到达（持续过程）
        _arrivedAtFocus = false;

        if (cmd.Anchor == null || cmd.Anchor.CurrentHp <= 0)
        {
            _receiver?.OnAnchorLost();
            return;
        }

        if (cmd.IsFormationSlot)
        {
            // 槽位化跟随：目标 = SlotWorld（锚点位置 + SlotOffset × cellSize）
            Vector2X myPos = _self.Position;
            Vector2X slotWorld = cmd.SlotWorld;
            float dist = Vector2X.Distance(myPos, slotWorld);
            float arrivalDist = _config.arrivalThreshold * cellSize;

            if (dist > arrivalDist)
                _self.MoveTowards(slotWorld, _currentSpeed, dt);
            else
                _self.Stop();
        }
        else
        {
            // 松散跟随语义（KeepDistance 判定）
            Vector2X anchorPos = cmd.Anchor.Position;
            Vector2X myPos = _self.Position;
            float dist = Vector2X.Distance(myPos, anchorPos);

            if (dist > cmd.KeepDistance + _config.arrivalThreshold * cellSize)
                _self.MoveTowards(anchorPos, _currentSpeed, dt);
            else
                _self.Stop();
        }
    }

    private void ExecuteIdle(in BehaviorCommand cmd, float dt)
    {
        _arrivedAtFocus = true;
        _self.Stop();

        if (cmd.Duration > 0f)
        {
            _durationTimer += dt;
            if (_durationTimer >= cmd.Duration)
            {
                _durationTimer = 0f;
                _receiver?.OnMoveComplete(_self.Position);
            }
        }
    }

    private void ExecuteWander(in BehaviorCommand cmd, float dt, float cellSize)
    {
        // 漫游是持续过程，永不到达语义
        _arrivedAtFocus = false;

        if (!_wanderHasTarget)
        {
            _wanderTarget = PickWanderPoint(cmd.TargetPos, cmd.WanderRadius);
            _wanderHasTarget = true;
            _wanderStaying = false;
        }

        if (_wanderStaying)
        {
            _self.Stop();
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
            Vector2X myPos = _self.Position;
            float dist = Vector2X.Distance(myPos, _wanderTarget);
            float arrivalDist = _config.arrivalThreshold * cellSize;

            if (dist <= arrivalDist)
            {
                _wanderStaying = true;
                _wanderStayTimer = 0f;
                _self.Stop();
            }
            else
            {
                _self.MoveTowards(_wanderTarget, _currentSpeed, dt);
            }
        }
    }

    /// <summary>1D 横版随机取点：x 轴 ±radius 随机，y 固定漫游中心（复刻 BehaviorExecutor.PickWanderPoint）。</summary>
    private Vector2X PickWanderPoint(Vector2X center, float radius)
    {
        float rx = _rng.Range(-radius, radius);
        return new Vector2X(center.x + rx, center.y);
    }

    private void ResetWanderState()
    {
        _wanderHasTarget = false;
        _wanderStaying = false;
        _wanderStayTimer = 0f;
        _wanderTarget = Vector2X.zero;
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
        Stop();
        _currentSpeed = 0f;
    }
}
