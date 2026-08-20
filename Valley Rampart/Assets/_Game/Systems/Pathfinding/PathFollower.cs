using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  2_3 步骤 3+4 PathFollower：五态状态机，驱动 UnitController.MoveTowards 走微格 A* 路径。
//  旧链路（直线走+撞墙停）由本组件 + 2_6 A* 取代。挂在需要寻路的单位（NPCBrain 驱动）。
//  状态机：Idle --SetDestination--> Pending --SetPath--> Following
//            --到达终微格--> Idle；--下一点不可走(动态阻挡)--> Repathing --冷却--> Pending
//            --连续 N 次失败--> Failed（发 PathFailedEvent，2_7/2_8 换目标）。
// ============================================================================

/// <summary>PathFollower 五态（2_3 §5.2）。</summary>
public enum PathFollowerState { Idle, Pending, Following, Repathing, Failed }

public class PathFollower : MonoBehaviour
{
    private UnitController _unit;

    private PathFollowerState _state = PathFollowerState.Idle;
    private PathResult _path;
    private int _wpIndex;
    private Vector2 _destination;
    private int _consecutiveFails;
    private float _stateStartTime;   // Pending 超时 / Repathing 冷却

    public PathFollowerState State => _state;

    private void Awake()
    {
        _unit = GetComponent<UnitController>();
    }

    /// <summary>设置目标（世界坐标）。落点吸附微格 + 发同步寻路请求（D73：仅在设置/到达时算）。</summary>
    public void SetDestination(Vector2 worldPos, byte priority = 0)
    {
        _destination = worldPos;
        _stateStartTime = Time.time;
        _state = PathFollowerState.Pending;
        RequestPath();
    }

    /// <summary>外部直接注入路径（2_6 服务化/测试用）。成功置 Following。</summary>
    public void SetPath(PathResult path)
    {
        _path = path;
        _wpIndex = 0;
        if (path == null) { FailOnce(); return; }

        if (path.status == PathStatus.Ready)
        {
            _consecutiveFails = 0;    // 成功重置失败计数
            _state = PathFollowerState.Following;
        }
        else if (path.status == PathStatus.Partial)
        {
            // 截断：走已展开前缀，末尾仍向目标推进（后续 2_6 服务化再 Partial 兜底重搜）
            _consecutiveFails = 0;
            _state = PathFollowerState.Following;
        }
        else // Unreachable / 其他
        {
            FailOnce();
        }
    }

    /// <summary>停止并回到 Idle。</summary>
    public void Stop()
    {
        if (_state != PathFollowerState.Idle)
        {
            _state = PathFollowerState.Idle;
            _path = null;
            _wpIndex = 0;
        }
    }

    private void Update()
    {
        if (_unit == null || !_unit.IsAlive) { Stop(); return; }

        var mc = MovementConfig.Instance;
        switch (_state)
        {
            case PathFollowerState.Pending:
                // 等路径（同步立即回，保留超时兜底防寻路未回呆立，R5）
                float pendingTimeout = mc != null && mc.pendingTimeoutSeconds > 0f ? mc.pendingTimeoutSeconds : 3f;
                if (Time.time - _stateStartTime > pendingTimeout) FailOnce();
                break;

            case PathFollowerState.Repathing:
                float cooldown = mc != null && mc.repathCooldownSeconds > 0f ? mc.repathCooldownSeconds : 1f;
                if (Time.time - _stateStartTime > cooldown)
                {
                    _stateStartTime = Time.time;
                    RequestPath();   // 冷却后重寻
                }
                break;

            case PathFollowerState.Following:
                FollowNext();
                break;
        }
    }

    /// <summary>逐路径点 MoveTowards；下一点失效（动态阻挡 R7）→ Repathing。</summary>
    private void FollowNext()
    {
        if (_path == null || _path.waypoints == null) { FailOnce(); return; }

        // 到达目标（终微格已吸附）：完成
        if (_unit.IsArrived(_destination))
        {
            Complete();
            return;
        }
        if (_wpIndex >= _path.waypoints.Length)
        {
            // 路径走完仍未达目标（通常因落点吸附离散）：尝试直接走向终点
            if (_unit.MoveTowards(_destination)) { Complete(); }
            else { RequestPath(); }   // 偏离路径，重寻
            return;
        }

        GridCoord wp = _path.waypoints[_wpIndex];
        var grid = GridSystem.Instance;
        // 下一路径点失效（动态阻挡：造墙/破门）→ 冷却后重寻（R7）
        if (grid != null && !grid.IsSubWalkable(wp))
        {
            _state = PathFollowerState.Repathing;
            _stateStartTime = Time.time;
            return;
        }

        Vector2 target = grid != null ? grid.SubCoordToWorld(wp) : (Vector2)transform.position;
        bool arrived = _unit.MoveTowards(target);
        if (arrived) _wpIndex++;
    }

    private void Complete()
    {
        _consecutiveFails = 0;
        _path = null;
        _wpIndex = 0;
        _state = PathFollowerState.Idle;
    }

    private void RequestPath()
    {
        _state = PathFollowerState.Pending;
        _stateStartTime = Time.time;
        var res = _unit != null
            ? PathfindingService.FindPathImmediate(_unit.transform.position, _destination)
            : null;
        SetPath(res);
    }

    private void FailOnce()
    {
        _consecutiveFails++;
        var mc = MovementConfig.Instance;
        int maxFails = mc != null && mc.maxConsecutiveFails > 0 ? mc.maxConsecutiveFails : 3;
        if (_consecutiveFails >= maxFails)
        {
            _state = PathFollowerState.Failed;
            if (_unit != null) EventBus.Publish(new PathFailedEvent(_unit, _destination));
            _path = null;
            _wpIndex = 0;
        }
        else
        {
            // 单次失败不封死：允许冷却后重试（由外部 Stop/SetDestination 重置）
            _state = PathFollowerState.Idle;
        }
    }
}