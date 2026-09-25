using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// M5-C 新协议运行时：王国级分桶、策略读取、生命周期与 M5-B 绑定口编排。
/// 这是并存链，不替换旧 TaskScheduler。
/// </summary>
public sealed class TaskProtocolRuntime
{
    public const string PolicyResourcesPath = "Config/TaskProtocolPolicyConfig";

    private readonly TaskBindingManager _bindingManager;
    private readonly TaskProtocolPolicyConfig _policy;
    private readonly Dictionary<int, List<TaskCard>> _pendingByKingdom =
        new Dictionary<int, List<TaskCard>>();

    public TaskProtocolRuntime()
        : this(new TaskBindingManager())
    {
    }

    public TaskProtocolRuntime(TaskBindingManager bindingManager)
    {
        if (bindingManager == null) throw new ArgumentNullException(nameof(bindingManager));
        _bindingManager = bindingManager;
        _policy = LoadPolicy();
    }

    public TaskProtocolPolicyConfig Policy
    {
        get { return _policy; }
    }

    public TaskBindingManager BindingManager
    {
        get { return _bindingManager; }
    }

    /// <summary>读取唯一配置入口；加载失败或政策字段漂移时立即停止。</summary>
    public static TaskProtocolPolicyConfig LoadPolicy()
    {
        TaskProtocolPolicyConfig config =
            Resources.Load<TaskProtocolPolicyConfig>(PolicyResourcesPath);
        if (config == null)
        {
            throw new InvalidOperationException(
                "TaskProtocolPolicyConfig failed to load from Resources/" + PolicyResourcesPath);
        }

        if (config.defaultRetryMax != 1
            || Math.Abs(config.tickIntervalSeconds - 1f) > 0.0001f
            || config.unreachableCooldownTicks != 5
            || config.domain != TaskProtocolDomain.Kingdom
            || config.statistics != TaskProtocolStatistics.PerTask)
        {
            throw new InvalidOperationException(
                "TaskProtocolPolicyConfig contains values different from the M5-C ruling.");
        }

        return config;
    }

    /// <summary>
    /// 派生有效优先级。M5-C 当前没有额外旧映射，故有效值等于上层给定基值；
    /// 该方法绝不回写卡片。
    /// </summary>
    public static int GetEffectivePriority(TaskCard card)
    {
        if (card == null) throw new ArgumentNullException(nameof(card));
        return card.basePriority;
    }

    /// <summary>提交已经进入 Pending 的卡片，建立 M5-B 预定并放入王国桶。</summary>
    public TaskBindingResult Submit(TaskCard card, bool targetAllowsMultiple, float deadline)
    {
        if (card == null) return TaskBindingResult.StateMismatch;
        if (card.state != TaskLifecycleState.Pending) return TaskBindingResult.StateMismatch;

        TaskBindingResult result =
            _bindingManager.Reserve(card, targetAllowsMultiple, deadline);
        if (result == TaskBindingResult.Ok || result == TaskBindingResult.AlreadyReserved)
        {
            AddPending(card);
        }

        return result;
    }

    public int PendingCount(int kingdomId)
    {
        List<TaskCard> cards;
        return _pendingByKingdom.TryGetValue(kingdomId, out cards) ? cards.Count : 0;
    }

    public IReadOnlyList<TaskCard> PendingForKingdom(int kingdomId)
    {
        List<TaskCard> cards;
        if (!_pendingByKingdom.TryGetValue(kingdomId, out cards))
            return new ReadOnlyCollection<TaskCard>(new List<TaskCard>());
        return cards.AsReadOnly();
    }

    /// <summary>
    /// tick 边界到达即恢复资格：不可达发生在 t，冷却边界为 t+5。
    /// </summary>
    public bool IsEligibleForDispatch(TaskCard card, long currentTick)
    {
        return card != null
            && card.state == TaskLifecycleState.Pending
            && currentTick >= card.unreachableBlockedUntilTick;
    }

    /// <summary>由执行侧在 Unassign(Unreachable) 后调用，写入本卡冷却边界。</summary>
    public void MarkUnreachable(TaskCard card, long currentTick)
    {
        if (card == null) throw new ArgumentNullException(nameof(card));
        card.unreachableBlockedUntilTick =
            currentTick + _policy.unreachableCooldownTicks;
    }

    public TaskTransitionError Assign(TaskCard card, int workerId, float deadline)
    {
        if (card == null) return TaskTransitionError.WorkerUnbound;
        TaskTransitionError transition = card.Assign(workerId, deadline);
        if (transition != TaskTransitionError.None) return transition;

        TaskBindingResult binding = _bindingManager.Pair(card, workerId);
        if (binding != TaskBindingResult.Ok && binding != TaskBindingResult.AlreadyPaired)
        {
            throw new InvalidOperationException(
                "TaskProtocolRuntime.Assign could not establish the M5-B pair: " + binding);
        }

        RemovePending(card);
        return TaskTransitionError.None;
    }

    public TaskTransitionError Unassign(
        TaskCard card,
        TaskUnassignReason reason,
        long currentTick)
    {
        if (card == null) return TaskTransitionError.WorkerUnbound;
        TaskTransitionError transition = card.Unassign(reason);
        if (transition != TaskTransitionError.None) return transition;

        TaskBindingResult binding = _bindingManager.Unpair(card);
        if (binding != TaskBindingResult.Ok && binding != TaskBindingResult.NotPaired)
        {
            throw new InvalidOperationException(
                "TaskProtocolRuntime.Unassign could not release the M5-B pair: " + binding);
        }

        AddPending(card);
        if (reason == TaskUnassignReason.Unreachable)
            MarkUnreachable(card, currentTick);
        return TaskTransitionError.None;
    }

    public TaskTransitionError Complete(TaskCard card)
    {
        if (card == null) return TaskTransitionError.TerminalSource;
        TaskTransitionError transition = card.Complete();
        if (transition != TaskTransitionError.None) return transition;
        ReleaseTerminal(card);
        return TaskTransitionError.None;
    }

    public TaskTransitionError Abort(TaskCard card, TaskAbortReason reason)
    {
        if (card == null) return TaskTransitionError.TerminalSource;
        TaskTransitionError transition = card.Abort(reason);
        if (transition != TaskTransitionError.None) return transition;
        ReleaseTerminal(card);
        return TaskTransitionError.None;
    }

    public TaskTransitionError EnterRestore(TaskCard card)
    {
        if (card == null) return TaskTransitionError.NoRestoreContext;
        TaskTransitionError transition = card.EnterRestore();
        if (transition != TaskTransitionError.None) return transition;

        RemovePending(card);
        TaskBindingResult binding = _bindingManager.EnterRestore(card);
        if (binding != TaskBindingResult.Ok && binding != TaskBindingResult.NotReserved)
        {
            throw new InvalidOperationException(
                "TaskProtocolRuntime.EnterRestore could not update M5-B: " + binding);
        }

        return TaskTransitionError.None;
    }

    public TaskTransitionError RestoreValidated(TaskCard card)
    {
        if (card == null) return TaskTransitionError.NoRestoreContext;
        TaskTransitionError transition = card.RestoreValidated();
        if (transition != TaskTransitionError.None) return transition;

        TaskBindingResult binding = _bindingManager.RestoreValidated(card);
        if (binding != TaskBindingResult.Ok)
        {
            throw new InvalidOperationException(
                "TaskProtocolRuntime.RestoreValidated could not update M5-B: " + binding);
        }

        AddPending(card);
        return TaskTransitionError.None;
    }

    private void ReleaseTerminal(TaskCard card)
    {
        RemovePending(card);
        TaskBindingResult binding = _bindingManager.ReleaseOnTerminal(card);
        if (binding != TaskBindingResult.Ok && binding != TaskBindingResult.NotReserved)
        {
            throw new InvalidOperationException(
                "TaskProtocolRuntime terminal release failed in M5-B: " + binding);
        }
    }

    private void AddPending(TaskCard card)
    {
        List<TaskCard> cards;
        if (!_pendingByKingdom.TryGetValue(card.kingdomId, out cards))
        {
            cards = new List<TaskCard>();
            _pendingByKingdom.Add(card.kingdomId, cards);
        }

        if (!cards.Contains(card)) cards.Add(card);
    }

    private void RemovePending(TaskCard card)
    {
        List<TaskCard> cards;
        if (!_pendingByKingdom.TryGetValue(card.kingdomId, out cards)) return;
        cards.Remove(card);
        if (cards.Count == 0) _pendingByKingdom.Remove(card.kingdomId);
    }
}
