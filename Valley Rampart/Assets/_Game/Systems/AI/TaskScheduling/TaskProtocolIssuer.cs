using System;

/// <summary>
/// L3/L4 的新协议造卡入口。上层持有本类，写入 basePriority 后交给并存运行时。
/// 不持有旧调度器、地图对象或 TaskBindingManager 的第二份实例。
/// </summary>
public sealed class TaskProtocolIssuer
{
    private readonly TaskProtocolRuntime _runtime;
    private readonly TaskIssuerRef _issuer;

    public TaskProtocolIssuer(TaskProtocolRuntime runtime, TaskIssuerRef issuer)
    {
        if (runtime == null) throw new ArgumentNullException(nameof(runtime));
        _runtime = runtime;
        _issuer = issuer;
    }

    public TaskProtocolRuntime Runtime
    {
        get { return _runtime; }
    }

    public TaskIssuerRef Issuer
    {
        get { return _issuer; }
    }

    /// <summary>创建尚未准入的卡片；retryMax 由 M5-C 配置写入。</summary>
    public TaskCard Create(
        long taskId,
        int kingdomId,
        string ability,
        TaskTargetRef targetRef)
    {
        TaskCard card = new TaskCard();
        card.taskId = taskId;
        card.kingdomId = kingdomId;
        card.ability = ability;
        card.targetRef = targetRef;
        card.issuer = _issuer;
        card.retryMax = _runtime.Policy.defaultRetryMax;
        return card;
    }

    /// <summary>
    /// 上层写入 basePriority，随后准入并提交到 M5-B 预定表与王国分桶。
    /// </summary>
    public TaskBindingResult Submit(
        TaskCard card,
        int basePriority,
        bool targetAllowsMultiple = true,
        float deadline = float.PositiveInfinity)
    {
        if (card == null) return TaskBindingResult.StateMismatch;

        card.issuer = _issuer;
        card.basePriority = basePriority;
        card.retryMax = _runtime.Policy.defaultRetryMax;

        TaskTransitionError accepted = card.Accept();
        if (accepted != TaskTransitionError.None)
        {
            throw new InvalidOperationException(
                "TaskProtocolIssuer could not accept a new card: " + accepted);
        }

        return _runtime.Submit(card, targetAllowsMultiple, deadline);
    }

    /// <summary>提交成功时返回同一张卡，便于上层保留卡片实例继续编排生命周期。</summary>
    public TaskCard Issue(
        TaskCard card,
        int basePriority,
        bool targetAllowsMultiple = true,
        float deadline = float.PositiveInfinity)
    {
        TaskBindingResult result = Submit(card, basePriority, targetAllowsMultiple, deadline);
        if (result != TaskBindingResult.Ok && result != TaskBindingResult.AlreadyReserved)
        {
            throw new InvalidOperationException(
                "TaskProtocolIssuer could not reserve the new card: " + result);
        }

        return card;
    }
}
