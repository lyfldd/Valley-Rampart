using UnityEngine;

// ============================================================================
//  王国任务 WorkerTask - 任务 vs 威胁权衡（T-K）/ 打断恢复（T-R）Unity 端落地
//  权威规格：15_训练侧harness与Unity端差距文档.md §7.8（2026-08-07 追加）
//  训练侧参考：harness/Sim/SimTask.cs + SimWorld.cs 的 TickTasks
//  职责：挂在工人上的 MonoBehaviour，驱动王国任务（Gather 采集 / Transport 运输），
//        在 1D 数轴 x 上往返 Source ↔ Dest。任务工人移动由本组件独占（挂起时向
//        HomePoint 逃跑），普通决策核移动在 NPCBrain 中被屏蔽（IsKingdomTaskWorker）。
//  状态机：Assigned→MovingToSource→Working(采集/取货计时 WorkDuration)
//          →(Transport)MovingToDest→Completed/Abandoned
//  ThreatFactor 经 NPCBrain.ThreatFactor 读取（映射训练侧 ThreatFactor）。
// ============================================================================

/// <summary>任务类型：Gather=采集 / Transport=运输（对齐 SimTaskType）。</summary>
public enum WorkerTaskType
{
    Gather,
    Transport,
}

/// <summary>任务推进状态机（对齐 SimTaskState）。</summary>
public enum WorkerTaskState
{
    Assigned,        // 已分配，尚未动身
    MovingToSource,  // 前往取货点 SourceX
    Working,         // 在 SourceX 采集/取货（WorkDuration 计时）
    MovingToDest,    // Transport：前往结算/仓库点 DestX
    Completed,       // 终态：顺利完成
    Abandoned,       // 终态：被打断/工人阵亡放弃
}

[RequireComponent(typeof(UnitController))]
public class WorkerTask : MonoBehaviour
{
    // ===== 静态指标（跨局累计，供调试面板 / Play 验证读数）=====
    // 对齐训练侧 SimWorld TaskCompleted/TaskAbandoned/TaskWorkerTotal/TaskResumed
    public static int TaskCompleted;      // 完成任务数
    public static int TaskAbandoned;      // 放弃任务数
    public static int TaskWorkerTotal;    // 任务工人总数（Assign 时 ++）
    public static int TaskResumed;        // 任务恢复数（T-R：挂起后恢复执行次数）

    // ===== 任务规格（对齐 SimTask）=====
    public WorkerTaskType Type;
    public float SourceX;      // 采集/取货点 x
    public float DestX;        // 结算/仓库点 x
    public float WorkDuration; // Working 阶段需时长（秒）
    public WorkerTaskState State = WorkerTaskState.Assigned;
    public float WorkElapsed;
    public bool Suspended;     // T-R：被打断挂起
    public float SuspendedAt;

    // ===== 搬运携带量（3.5.3 §3.1 / 3.5 前置缺口 §2.2；P1-8）=====
    /// <summary>本次搬运的资源类型（取货点 source 的产出类型）。</summary>
    public ResourceType CarryResource;
    /// <summary>本次搬运一次携带量（资源类型 → 携带量查 ResourceCarryConfig SO，数据驱动）。</summary>
    public int CarryAmount;

    /// <summary>任务是否推进中（非终态）。</summary>
    public bool Active => State != WorkerTaskState.Completed && State != WorkerTaskState.Abandoned;

    private UnitController _unit;
    private NPCBrain _brain;

    private void Awake()
    {
        _unit = GetComponent<UnitController>();
        _brain = GetComponent<NPCBrain>();
    }

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    /// <summary>
    /// 工人阵亡 → 任务标 Abandoned（对齐训练侧 SimWorld.TickTasks「工人不存在/阵亡 → Abandoned」）。
    /// Die 在回池前同步发布 UnitDiedEvent，此处可靠捕捉（比 Update 检查 !IsAlive 更稳，防对象销毁丢失）。
    /// </summary>
    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (evt.Unit == null || _unit == null) return;
        if (!ReferenceEquals(evt.Unit, (IDamageable)_unit)) return;
        Abandon();
    }

    /// <summary>
    /// 领取任务（对齐训练侧 SimWorld 装配 SimTask + 置 worker.Brain.IsKingdomTaskWorker）。
    /// 分配即标记工人为任务工人（移动独占）；TaskWorkerTotal 累计。
    /// 搬运任务经此重载传入 carryResource，携带量由 ResourceCarryConfig SO 查表填充（P1-8）。
    /// </summary>
    public void Assign(WorkerTaskType type, float sourceX, float destX, float workDuration)
        => Assign(type, sourceX, destX, workDuration, ResourceType.Gold);

    /// <summary>带资源类型的领取（P1-8：搬运携带量）。</summary>
    public void Assign(WorkerTaskType type, float sourceX, float destX, float workDuration, ResourceType carryResource)
    {
        Type = type;
        SourceX = sourceX;
        DestX = destX;
        WorkDuration = workDuration;
        CarryResource = carryResource;
        CarryAmount = GetCarryAmount(carryResource);   // 数据驱动：携带量查 SO
        State = WorkerTaskState.Assigned;
        WorkElapsed = 0f;
        Suspended = false;
        SuspendedAt = 0f;
        TaskWorkerTotal++;

        // 任务工人移动独占：NPCBrain 据此跳过普通决策核移动（对齐 SimBrain.IsKingdomTaskWorker）
        if (_brain != null) _brain.IsKingdomTaskWorker = true;
    }

    /// <summary>按资源类型查一次搬运携带量（ResourceCarryConfig SO，P1-8）。</summary>
    private static ResourceCarryConfig _carryConfig;
    private static int GetCarryAmount(ResourceType type)
    {
        if (_carryConfig == null)
            _carryConfig = Resources.Load<ResourceCarryConfig>("Config/ResourceCarryConfig");
        return _carryConfig != null ? _carryConfig.GetCarryAmount(type) : 10;
    }

    /// <summary>把任务标记为失败放弃（工人阵亡等外部原因调用，双保险）。</summary>
    public void Abandon()
    {
        if (State == WorkerTaskState.Completed || State == WorkerTaskState.Abandoned) return;
        State = WorkerTaskState.Abandoned;
        TaskAbandoned++;
    }

    /// <summary>
    /// 每帧推进任务状态机（对齐训练侧 SimWorld.TickTasks）。
    /// 顺序：威胁挂起逃跑 → 恢复判断 → 状态机推进。
    /// </summary>
    private void Update()
    {
        // 工人不存在或已阵亡 → 若任务未终态标 Abandoned（对齐训练侧 TickTasks 双保险）
        if (_unit == null || !IsAlive)
        {
            if (Active)
            {
                State = WorkerTaskState.Abandoned;
                TaskAbandoned++;
            }
            return;
        }
        if (!Active) return;
        if (_brain == null) return;
        var config = _brain.Config;
        if (config == null) return;

        float cellSize = GetCellSize();
        float threat = _brain.ThreatFactor;
        float abandon = config.abandonThreshold;

        // 逃跑/挂起（T-K）：威胁超放弃阈值 → 挂起 + 朝归巢点逃跑
        if (threat > abandon)
        {
            if (!Suspended)
            {
                Suspended = true;
                SuspendedAt = Time.time;
            }
            _unit.MoveTowards(_brain.HomePointWorld, speedOverride: _unit.WalkSpeed);
            return;
        }

        // 恢复（T-R）：挂起且超恢复延迟 + 威胁降到阈值以下 → 解除挂起
        if (Suspended
            && Time.time - SuspendedAt > config.taskResumeDelay
            && threat < config.taskResumeThreshold)
        {
            Suspended = false;
            TaskResumed++;   // T-R 恢复率统计
        }

        // 挂起中不推进任务（未恢复则原地等待）
        if (Suspended) return;

        // 到达判定距离（对齐训练侧 arrivalDist = arrivalThreshold × cellSize）
        float arrival = config.arrivalThreshold * cellSize;

        // 状态机推进（对齐训练侧 TickTasks switch）
        switch (State)
        {
            case WorkerTaskState.Assigned:
                State = WorkerTaskState.MovingToSource;
                goto case WorkerTaskState.MovingToSource;

            case WorkerTaskState.MovingToSource:
                // 前往取货点 SourceX
                _unit.MoveTowards(new Vector2(SourceX, _unit.transform.position.y), speedOverride: _unit.WalkSpeed);
                if (Mathf.Abs(_unit.transform.position.x - SourceX) <= arrival)
                {
                    // 到达取货点 → 进入 Working
                    State = WorkerTaskState.Working;
                    WorkElapsed = 0f;
                }
                break;

            case WorkerTaskState.Working:
                WorkElapsed += Time.deltaTime;
                if (WorkElapsed >= WorkDuration)
                {
                    if (Type == WorkerTaskType.Gather)
                    {
                        // 采集完成直接 Completed
                        State = WorkerTaskState.Completed;
                        TaskCompleted++;
                    }
                    else
                    {
                        // 运输取货完成 → 前往结算点 DestX
                        State = WorkerTaskState.MovingToDest;
                    }
                }
                break;

            case WorkerTaskState.MovingToDest:
                // 运输：前往结算/仓库点 DestX
                _unit.MoveTowards(new Vector2(DestX, _unit.transform.position.y), speedOverride: _unit.WalkSpeed);
                if (Mathf.Abs(_unit.transform.position.x - DestX) <= arrival)
                {
                    // 到达结算点 → Completed
                    State = WorkerTaskState.Completed;
                    TaskCompleted++;
                }
                break;
        }
    }

    private bool IsAlive => _unit != null && _unit.IsAlive;

    /// <summary>网格 cellSize（GridSystem 未就绪时回退对齐 NPCBrain 默认 2.26）。</summary>
    private float GetCellSize()
    {
        return GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize : 2.26f;
    }
}