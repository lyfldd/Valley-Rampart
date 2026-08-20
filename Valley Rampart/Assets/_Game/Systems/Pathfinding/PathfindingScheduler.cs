using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  2_6 P0b 寻路服务化：异步分帧 + 票据。
//  不动 AStarSolver 同步内核——把高频短寻路请求排队，每帧按「数量×时间」预算限流处理，
//  避免单帧内大量同步寻路阻塞主线程。PathFollower 主链仍走同步 FindPathImmediate（稳定），
//  本 Scheduler 提供 RequestPath 异步入口供编辑器模拟/超高单位数场景接入。
// ============================================================================

/// <summary>异步分帧寻路调度器（P0b 服务化，MonoBehaviour 单例）。</summary>
public class PathfindingScheduler : MonoBehaviour
{
    private struct Req
    {
        public int Id;
        public GridCoord From;
        public GridCoord To;
        public byte Priority;
        public Action<PathTicket> Callback;
    }

    private static PathfindingScheduler _instance;
    public static PathfindingScheduler Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("_PathfindingScheduler");
                _instance = go.AddComponent<PathfindingScheduler>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private readonly List<Req> _queue = new List<Req>(64);
    private readonly List<PathTicket> _done = new List<PathTicket>(16);
    private int _nextId = 1;
    private int _available = -1;               // -1=空闲；每次分配 counter 从 0，预算用尽则暂停

    [Tooltip("每帧最多处理的寻路请求数（分帧预算）")]
    public int maxPerFrame = 8;

    [Tooltip("每帧累计寻路时间预算（秒），超时留待下一帧")]
    public float timeBudgetPerFrame = 0.002f;

    [Tooltip("单个请求最大展开数（与 AStarSolver 上限一致）")]
    public int maxExpansions = 4096;

    /// <summary>提交异步寻路请求（世界坐标）。返回票据；完成时经本帧/后续帧回调。</summary>
    public PathTicket RequestPath(Vector2 fromWorld, Vector2 toWorld, out bool enqueued,
        byte priority = 0, Action<PathTicket> callback = null)
    {
        var ticket = new PathTicket { id = _nextId++ };
        var grid = GridSystem.Instance;
        enqueued = false;
        if (grid == null) return ticket;   // 未就绪：空票据（不回调）

        var fromOpt = grid.WorldToSubCoord(fromWorld);
        var toOpt = grid.WorldToSubCoord(toWorld);
        if (!fromOpt.HasValue || !toOpt.HasValue) return ticket;   // 越界：失败票据

        _queue.Add(new Req
        {
            Id = ticket.id, From = fromOpt.Value, To = toOpt.Value,
            Priority = priority, Callback = callback
        });
        enqueued = true;
        return ticket;
    }

    private void Update()
    {
        if (_queue.Count == 0) return;
        // 按优先级稳定排序（高优先在前），确保预算内处理最重要的
        _queue.Sort((a, b) => b.Priority - a.Priority);

        int budgetCount = Mathf.Max(1, maxPerFrame);
        float deadline = Time.time + timeBudgetPerFrame;
        int processed = 0;

        for (int i = 0; i < _queue.Count && processed < budgetCount && Time.time < deadline; i++)
        {
            Req req = _queue[i];
            PathResult res = AStarSolver.Solve(GridSystem.Instance, req.From, req.To, maxExpansions);
            _done.Add(new PathTicket { id = req.Id, HasResult = true, Result = res });
            try { req.Callback?.Invoke(_done[_done.Count - 1]); }
            catch (Exception e) { Debug.LogError($"PathScheduler callback error: {e}"); }
            processed++;
        }

        if (processed > 0)
        {
            _queue.RemoveRange(0, processed);   // 已处理队首
            _done.Clear();
        }
    }
}