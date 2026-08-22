using UnityEngine;

/// <summary>
/// 2_12 步骤6（HH.10 裁决三）：树的数据格采集源。
/// 树在 A+ 数据化后是 features 数据格（不建实体，防止 1.6 万 GameObject 复辟），但需可被工人砍伐。
/// 本类是一个轻量 ITaskSource 包装（无 MonoBehaviour / 无渲染），代表"一棵被确认采集的树"：
///   懒注册——仅玩家确认砍某棵树时才创建并 Register 进 TaskScheduler（未砍的树不占 _sources）。
/// 生命周期：Confirm → 派 Gather → 工人到树格 Working(gatherSeconds) → 木入背包 →
///          TaskScheduler 回调 TreeGatherSource.OnGatherCompletion → inactive(IsValid=false) 下 tick 被清。
/// SourcePos=树格世界坐标（GridSystem.CoordToWorld），让调度器派工人到格上砍树。
/// </summary>
public class TreeGatherSource : ITaskSource
{
    /// <summary>该树所在格（采集完成后用于翻 feature → Plain 并记录重生）。</summary>
    public GridCoord Cell { get; private set; }

    private readonly Vector2 _pos;
    private bool _active = true;
    private readonly float _gatherSeconds;
    private readonly int _gatherAmount;

    public TreeGatherSource(GridCoord cell, Vector2 worldPos, float gatherSeconds, int gatherAmount)
    {
        Cell = cell;
        _pos = worldPos;
        _gatherSeconds = gatherSeconds;
        _gatherAmount = gatherAmount;
    }

    public bool IsValid => _active;
    public Vector2 SourcePos => _pos;

    public void OnRegister() { }
    public void OnUnregister() { }

    public bool TryAdvertiseTask(out KingdomTask task)
    {
        task = null;
        if (!_active) return false;
        task = new KingdomTask(KingdomTaskType.Gather, this);
        task.destType = KingdomDestType.Treasury;
        task.args = new GatherTaskArgs
        {
            resourceType = ResourceType.Wood,   // 树=木的唯一持续来源（裁决确认 §5.3）
            amount = _gatherAmount,
            gatherSeconds = _gatherSeconds
        };
        // 防多派：登记后同源同类型被 HasAssignedTaskForSourceType 独占去重，只派 1 工人完成采集。
        _active = false;   // 一次性：本 tick 发布后即失效（防未完成前重复派发多工人砍同一棵）。
        return true;
    }

    /// <summary>
    /// 采集完成回调（由 TaskScheduler.ExecuteCompletion 在木入背包后调用）。
    /// 职责：把该树格 feature Tree → Plain + 刷新渲染 + 记重生（数据路径，决不实体化）。
    /// </summary>
    public void OnGatherCompletion()
    {
        if (ResourceRespawnSystem.HasInstance)
            ResourceRespawnSystem.Instance.HandleTreeGathered(Cell);
    }
}