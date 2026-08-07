using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 任务调度器单例（QQQ.3 B1-7 单例化 + QQQ.2 §10.3 调度流程）。
///
/// 职责（T17，纯派发 + 记录 + 查询）：
///   1. 维护建筑注册表 _sources（避免每帧 FindObjectsOfType）。
///   2. 每 tick（tickInterval 秒）遍历 _sources 调 TryAdvertiseTask 收集可派任务。
///   3. 遍历空闲 NPC（IsIdleForTask && 未被占用），按 优先级(S>A>B>C)+距离升序 分配（DR-17）。
///   4. 派发时动态解析 destPos（destType 驱动），构造 TaskStimulus 注入 NPCBrain（复用刺激机制让 NPC 走向任务点）。
///   5. 对在册 NPC 维护 _npcTaskMap/_npcStateMap 供查询（GetWorkerState/HasWorkerAssigned）。
///   6. 简化状态机：Assigned→MovingToSource→Working→Completed（到达即 Working、计时完成移除）。
///
/// 注意：WorkerTask 已内化为 KingdomTask 工厂（QQQ.2 T18），本类统一驱动任务推进。
/// 本类不设置 brain.IsKingdomTaskWorker=true（靠 TaskStimulus 让 NPC 移动，Executor 消费，
/// 移动独占不再需要——任务态由本类 _npcStateMap 维护），完成/放弃时复位（修复 LC-N2）。
/// </summary>
// QQQ.2 T17 / QQQ.3 B1-7
public class TaskScheduler : MonoBehaviour, ITaskScheduler
{
    public static TaskScheduler Instance { get; private set; }
    public static bool HasInstance => Instance != null;

    [Header("任务调度配置（QQQ.2 §10.3）")]
    [Tooltip("调度 tick 间隔（秒）")]
    public float tickInterval = 1f;
    [Tooltip("任务刺激有效期（秒）：工人被打断没去 → 刺激过期 → 下 tick 重派")]
    public float taskExpiry = 5f;
    [Tooltip("Working 阶段需时长（秒，简化状态机）")]
    public float workDuration = 2f;
    [Tooltip("任务超时（秒）：MovingToSource 迟迟未到达则放弃（防卡死）")]
    public float taskTimeout = 30f;
    [Tooltip("WaterHaul 一次搬水量")]
    public float waterCarryAmount = 10f;
    [Tooltip("Gather 一次采集量")]
    public int gatherAmount = 5;

    // ===== 数据结构 =====
    private readonly HashSet<ITaskSource> _sources = new HashSet<ITaskSource>();
    private readonly Dictionary<int, KingdomTask> _npcTaskMap = new Dictionary<int, KingdomTask>();
    private readonly Dictionary<int, TaskState> _npcStateMap = new Dictionary<int, TaskState>();
    private readonly Dictionary<int, NPCBrain> _npcBrainMap = new Dictionary<int, NPCBrain>();
    private readonly Dictionary<int, float> _suspendStartTime = new Dictionary<int, float>();
    private readonly Dictionary<int, float> _workStartTime = new Dictionary<int, float>();
    private readonly Dictionary<int, float> _taskStartTime = new Dictionary<int, float>();

    private float _tickTimer;
    private TaskPriorityConfig _priorityConfig;

    // ===== 单例 =====

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TaskScheduler] 已存在实例，销毁重复对象。");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _priorityConfig = Resources.Load<TaskPriorityConfig>("Config/TaskPriorityConfig");
        if (_priorityConfig == null)
            Debug.LogWarning("[TaskScheduler] 未找到 TaskPriorityConfig（Resources/Config/TaskPriorityConfig），优先级回退 B。");

        // QQQ.3 B1-1：订阅 NPC 死亡事件清指派
        UnitController.OnUnitDied += OnNpcDied;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnitController.OnUnitDied -= OnNpcDied;
    }

    private void Update()
    {
        _tickTimer += Time.deltaTime;
        if (_tickTimer < tickInterval) return;
        _tickTimer = 0f;
        Tick();
    }

    // ===== ITaskScheduler =====

    public void Register(ITaskSource source)
    {
        if (source == null) return;
        if (_sources.Add(source)) source.OnRegister();
    }

    public void Unregister(ITaskSource source)
    {
        if (source == null) return;
        if (_sources.Remove(source)) source.OnUnregister();
        // QQQ.3 B8-x：清掉指向该源的在派任务（建筑死亡/废弃释放工人）
        OnBuildingDied(source);
    }

    public TaskState GetWorkerState(int npcId)
    {
        if (_npcStateMap.TryGetValue(npcId, out var st)) return st;
        return TaskState.None;
    }

    public bool HasWorkerAssigned(ITaskSource producer)
    {
        if (producer == null) return false;
        foreach (var kv in _npcTaskMap)
        {
            if (ReferenceEquals(kv.Value.source, producer)
                && _npcStateMap.TryGetValue(kv.Key, out var st)
                && st == TaskState.Working)
                return true;
        }
        return false;
    }

    public void AbandonTask(int npcId)
    {
        if (!_npcTaskMap.TryGetValue(npcId, out var task)) return;
        _npcBrainMap.TryGetValue(npcId, out var brain);
        Abandon(npcId, task, brain);
    }

    public void OnNpcDied(int npcId)
    {
        if (!_npcTaskMap.TryGetValue(npcId, out var task)) return;
        _npcBrainMap.TryGetValue(npcId, out var brain);
        Abandon(npcId, task, brain);
    }

    public void OnBuildingDied(ITaskSource source)
    {
        if (source == null) return;
        var stale = new List<int>();
        foreach (var kv in _npcTaskMap)
        {
            if (ReferenceEquals(kv.Value.source, source)) stale.Add(kv.Key);
        }
        for (int i = 0; i < stale.Count; i++)
        {
            _npcBrainMap.TryGetValue(stale[i], out var brain);
            Abandon(stale[i], _npcTaskMap[stale[i]], brain);
        }
    }

    public void OnThreatSuspended(int npcId)
    {
        _suspendStartTime[npcId] = Time.time;
    }

    public void OnThreatResumed(int npcId)
    {
        _suspendStartTime.Remove(npcId);
    }

    // ===== 内部：调度主循环 =====

    private void Tick()
    {
        // ① 清理无效源（建筑被 Destroy 后引用非 null，靠 IsValid 判）
        if (_sources.Count > 0)
        {
            ITaskSource[] snapshot = new ITaskSource[_sources.Count];
            _sources.CopyTo(snapshot);
            for (int i = 0; i < snapshot.Length; i++)
                if (snapshot[i] == null || !snapshot[i].IsValid) _sources.Remove(snapshot[i]);
        }

        // ② 收集空闲 NPC 候选
        var npcs = FindObjectsOfType<NPCBrain>();
        var idle = new List<NPCBrain>();
        for (int i = 0; i < npcs.Length; i++)
        {
            var n = npcs[i];
            if (n == null || !n.IsAlive || !n.IsIdleForTask) continue;
            var uc = n.GetComponent<UnitController>();
            if (uc == null || uc.npcId == 0) continue;
            if (_npcTaskMap.ContainsKey(uc.npcId)) continue;   // 幂等：已占用不重派
            idle.Add(n);
        }

        // ③ 收集可派任务（跳过已在册源，任务幂等靠 _npcTaskMap 判占用）
        var jobs = new List<KingdomTask>();
        foreach (var s in _sources)
        {
            if (s == null || !s.IsValid) continue;
            if (HasAssignedTaskForSource(s)) continue;
            if (!s.TryAdvertiseTask(out var task)) continue;
            ResolveDest(task);
            jobs.Add(task);
        }
        if (jobs.Count == 0 || idle.Count == 0) { UpdateAssignedTasks(); return; }

        // ④ 优先级（S>A>B>C）排序，高优先先派；同优先按距离升序（DR-17）
        jobs.Sort((a, b) => GetPriority(b.type).CompareTo(GetPriority(a.type)));

        var used = new bool[idle.Count];
        for (int j = 0; j < jobs.Count; j++)
        {
            var task = jobs[j];
            // 找到距源最近的仍空闲 NPC
            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < idle.Count; i++)
            {
                if (used[i]) continue;
                float d = Vector2.Distance(idle[i].transform.position, task.SourcePos);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            if (best < 0) break;   // 无空闲工人，剩余任务等待下 tick
            used[best] = true;
            Dispatch(idle[best], task);
        }

        // ⑤ 推进在册任务态
        UpdateAssignedTasks();
    }

    /// <summary>
    /// 外部派发入口（QQQ.2 T18）：供 WorkerTask 工厂 / AIDebugSpawnController 调试用。
    /// 手动把任务派发给指定 NPC（无双轨：WorkerTask 不再自己驱动，统一走本调度器推进）。
    /// </summary>
    public void DispatchExternal(NPCBrain brain, KingdomTask task)
    {
        if (brain == null || task == null) return;
        var uc = brain.GetComponent<UnitController>();
        if (uc == null || uc.npcId == 0) return;
        Dispatch(brain, task);
    }

    /// <summary>派发任务到指定 NPC：记录 + 注入刺激。</summary>
    private void Dispatch(NPCBrain brain, KingdomTask task)
    {
        var uc = brain.GetComponent<UnitController>();
        if (uc == null) return;
        int id = uc.npcId;
        _npcTaskMap[id] = task;
        _npcStateMap[id] = TaskState.Assigned;
        _npcBrainMap[id] = brain;
        _taskStartTime[id] = Time.time;
        _workStartTime.Remove(id);
        _suspendStartTime.Remove(id);
        InjectStimulus(brain, task);
        Debug.Log($"[TaskScheduler] 派发 {task.type} 任务 → npcId {id} @ {task.SourcePos}（优先级 {GetPriority(task.type)}）");
    }

    /// <summary>注入/续命任务刺激（目标=任务源坐标，复用刺激机制让 NPC 走向任务点）。</summary>
    private void InjectStimulus(NPCBrain brain, KingdomTask task)
    {
        if (brain == null) return;
        brain.RemoveTaskStimulus(task.source);
        brain.AddTaskStimulus(new TaskStimulus(
            GetPriority(task.type),
            Vector2XUnity.FromUnity(task.SourcePos),
            task.intensity,
            expiry: Time.time + taskExpiry,
            issuer: task.source));
    }

    /// <summary>
    /// 推进在册任务态（简化状态机：Assigned→MovingToSource→Working→Completed）。
    /// MovingToSource 到达源附近→Working；Working 计时完成→Complete（执行完成动作并移除）。
    /// </summary>
    private void UpdateAssignedTasks()
    {
        if (_npcTaskMap.Count == 0) return;
        var stale = new List<int>();
        float cellSize = GetCellSize();

        foreach (var kv in new Dictionary<int, KingdomTask>(_npcTaskMap))
        {
            int id = kv.Key;
            var task = kv.Value;
            if (!_npcBrainMap.TryGetValue(id, out var brain) || brain == null)
            {
                stale.Add(id);   // 引用丢失，放弃
                continue;
            }
            if (!brain.IsAlive)
            {
                stale.Add(id);   // 死亡（OnUnitDied 应已清，此处双保险）
                continue;
            }
            if (task.source == null || !task.source.IsValid)
            {
                stale.Add(id);   // 源失效，放弃
                continue;
            }

            TaskState st = _npcStateMap.TryGetValue(id, out var cur) ? cur : TaskState.Assigned;
            switch (st)
            {
                case TaskState.Assigned:
                    _npcStateMap[id] = TaskState.MovingToSource;
                    InjectStimulus(brain, task);   // 续命刺激直至到达
                    break;

                case TaskState.MovingToSource:
                    {
                        float arrive = ArrivalThreshold(brain, cellSize);
                        if (Vector2.Distance(brain.transform.position, task.SourcePos) <= arrive)
                        {
                            _npcStateMap[id] = TaskState.Working;
                            _workStartTime[id] = Time.time;
                        }
                        else if (Time.time - _taskStartTime[id] > taskTimeout)
                        {
                            stale.Add(id);          // 超时未到达，放弃
                        }
                        else
                        {
                            InjectStimulus(brain, task);   // 未到达续命
                        }
                    }
                    break;

                case TaskState.Working:
                    // QQQ.2 T18：Working 占位动作态——面向任务点 + 头顶冒"劳作"提示（占位，视觉动画后置）
                    // T-K 威胁挂起：威胁超放弃阈值 → 冻结工作计时（挂起）；威胁解除恢复（T-R）
                    if (brain.ThreatFactor > GetAbandonThreshold(brain))
                    {
                        if (!_suspendStartTime.ContainsKey(id)) _suspendStartTime[id] = Time.time;
                        InjectStimulus(brain, task);   // 续命刺激：防战斗超 taskExpiry 过期，恢复后仍锁定任务点
                        break;   // 挂起：不推进工作计时，NPC 由注意力切 Threat 战斗
                    }
                    // 恢复：把挂起时长顺延到工作起点（等效暂停计时）
                    if (_suspendStartTime.TryGetValue(id, out float suspendAt))
                    {
                        _workStartTime[id] += Time.time - suspendAt;
                        _suspendStartTime.Remove(id);
                    }
                    FaceAndShowWorking(brain, task);
                    // QQQ.2 T19：Gather 按资源点类型耗时（def.gatherSeconds，DR-11），其余任务用统一 workDuration
                    if (Time.time - _workStartTime[id] >= GetTaskDuration(task))
                    {
                        Complete(id, task, brain);
                        stale.Add(id);
                    }
                    break;

                default:
                    stale.Add(id);
                    break;
            }
        }

        for (int i = 0; i < stale.Count; i++)
        {
            if (_npcTaskMap.TryGetValue(stale[i], out var task))
            {
                _npcBrainMap.TryGetValue(stale[i], out var brain);
                Abandon(stale[i], task, brain);
            }
        }
    }

    /// <summary>完成任务：执行完成动作 + 复位工人 + 移除记录。</summary>
    private void Complete(int npcId, KingdomTask task, NPCBrain brain)
    {
        ExecuteCompletion(task);
        if (brain != null)
        {
            brain.IsKingdomTaskWorker = false;   // 复位（修复 LC-N2）
            brain.RemoveTaskStimulus(task.source);
        }
        ClearNpc(npcId);
        Debug.Log($"[TaskScheduler] 完成 {task.type} 任务 → npcId {npcId}");
    }

    /// <summary>
    /// Working 占位动作态（QQQ.2 T18）：NPC 面向任务点 + 头顶冒"劳作"提示（占位，视觉动画后置）。
    /// 面向：经 MoveTowards(自身位置) 保持原地，移动内核 UpdateFacing 会把朝向翻向目标侧
    /// （step≈0 时 UpdateFacing 用 newPos-current，方向趋于 0，故不依赖朝向，仅作视觉占位）。
    /// 威胁挂起（T-K/T-R）由 Working 分支的 ThreatFactor 判断在调度层处理（见 UpdateAssignedTasks）。
    /// </summary>
    private void FaceAndShowWorking(NPCBrain brain, KingdomTask task)
    {
        if (brain == null) return;
        var uc = brain.GetComponent<UnitController>();
        if (uc != null && task != null)
        {
            // 停在原地（保持到达态，防被 Wander 拉走）；面向任务点由 UpdateFacing 内部处理
            uc.MoveTowards(brain.transform.position);
        }
        OverheadSpeech.Show(brain.transform, "劳作中…", duration: 0.8f);
    }

    /// <summary>放弃任务：清记录 + 复位工人 + 移除刺激（不执行完成动作）。</summary>
    private void Abandon(int npcId, KingdomTask task, NPCBrain brain)
    {
        // QQQ.2 T19 / RES-A2：Gather 中断（工人阵亡/被打断/源失效）→ 资源点解锁可再点击（进度重置不保留）
        if (task != null && task.type == KingdomTaskType.Gather && task.source is Building gb)
            gb.isBeingGathered = false;
        if (brain != null)
        {
            brain.IsKingdomTaskWorker = false;
            if (task != null) brain.RemoveTaskStimulus(task.source);
        }
        ClearNpc(npcId);
    }

    private void ClearNpc(int npcId)
    {
        _npcTaskMap.Remove(npcId);
        _npcStateMap.Remove(npcId);
        _npcBrainMap.Remove(npcId);
        _workStartTime.Remove(npcId);
        _taskStartTime.Remove(npcId);
        _suspendStartTime.Remove(npcId);
    }

    /// <summary>按任务类型执行完成动作（QQQ.2 §10.3）。</summary>
    private void ExecuteCompletion(KingdomTask task)
    {
        if (task == null || task.source == null) return;
        var comp = task.source as Component;
        switch (task.type)
        {
            case KingdomTaskType.Production:
                var prod = comp != null ? comp.GetComponent<ProducerComponent>() : null;
                if (prod != null) prod.Tick();   // 触发当次产出
                break;

            case KingdomTaskType.Transport:
                var st = comp != null ? comp.GetComponent<StorageComponent>() : null;
                if (st != null) st.HarvestCarry();   // 一次携带量入国库（分批搬运）
                break;

            case KingdomTaskType.WaterHaul:
                if (WaterNetwork.Instance != null)
                    WaterNetwork.Instance.AddWater(waterCarryAmount);
                break;

            case KingdomTaskType.Gather:
                var ga = task.args as GatherTaskArgs;
                if (ga != null && RulerController.Instance != null)
                    RulerController.Instance.ModifyResource(ga.resourceType, true, ga.amount);
                // QQQ.2 T19：采集完成 → 资源点销毁三步（①GridSystem.Free ②BuildingRegistry移除 ③对象池Despawn）
                if (comp is Building b) b.OnGatherCompleted();
                break;
        }
    }

    // ===== 终点解析（QQQ.2 §10.3：派发时动态解析 destPos，不硬编码）=====

    private void ResolveDest(KingdomTask task)
    {
        switch (task.destType)
        {
            case KingdomDestType.None:
                task.destPos = task.SourcePos;
                break;
            case KingdomDestType.Treasury:
                task.destPos = ResolveTreasury(task);
                break;
            case KingdomDestType.NearestWarehouse:
                task.destPos = ResolveWarehouse(task);
                break;
            case KingdomDestType.WaterNetwork:
                task.destPos = WaterNetwork.Instance != null
                    ? (Vector2)WaterNetwork.Instance.transform.position
                    : task.SourcePos;
                break;
            case KingdomDestType.SpecificBuilding:
            default:
                break;   // 已由任务带 destPos
        }
    }

    /// <summary>国库位置 = 王国锚点（主城/国库）；无则回退任务源。</summary>
    private Vector2 ResolveTreasury(KingdomTask task)
    {
        if (WorldManager.Instance != null)
        {
            Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
            if (anchor != Vector2.zero) return anchor;
        }
        return task != null ? task.SourcePos : Vector2.zero;
    }

    /// <summary>最近可用仓库（StorageComponent 中最近且 capacity&gt;stored），无则回退国库。</summary>
    private Vector2 ResolveWarehouse(KingdomTask task)
    {
        var storages = FindObjectsOfType<StorageComponent>();
        StorageComponent best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < storages.Length; i++)
        {
            var s = storages[i];
            if (s == null || s.capacity <= s.storedAmount) continue;   // 已满不收
            float d = Vector2.Distance(s.transform.position, task.SourcePos);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best != null) return best.transform.position;
        return ResolveTreasury(task);
    }

    // ===== 辅助 =====

    /// <summary>该源当前是否已有在派任务（幂等去重）。</summary>
    private bool HasAssignedTaskForSource(ITaskSource source)
    {
        foreach (var kv in _npcTaskMap)
            if (ReferenceEquals(kv.Value.source, source)) return true;
        return false;
    }

    private TaskPriority GetPriority(KingdomTaskType type)
    {
        return _priorityConfig != null ? _priorityConfig.Get(type) : TaskPriority.B;
    }

    private float ArrivalThreshold(NPCBrain brain, float cellSize)
    {
        if (brain != null && brain.Config != null)
            return brain.Config.arrivalThreshold * cellSize;
        return 1.5f * cellSize;
    }

    /// <summary>威胁放弃阈值（T-K/T-R：ThreatFactor 超此值工作挂起）。</summary>
    private float GetAbandonThreshold(NPCBrain brain)
    {
        return brain != null && brain.Config != null ? brain.Config.abandonThreshold : 0.8f;
    }

    /// <summary>任务 Working 时长（QQQ.2 T19/DR-11：Gather 按资源点 def.gatherSeconds，其余统一 workDuration）。</summary>
    private float GetTaskDuration(KingdomTask task)
    {
        if (task != null && task.type == KingdomTaskType.Gather && task.args is GatherTaskArgs ga)
        {
            float secs = ga.gatherSeconds;
            if (task.source is Building b && b.def != null && b.def.gatherSeconds > 0f)
                secs = b.def.gatherSeconds;   // 以 def 为准（资源点资产已配 2s/4s/8s）
            return secs > 0f ? secs : workDuration;
        }
        return workDuration;
    }

    private float GetCellSize()
    {
        return GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize : 2.26f;
    }
}

/// <summary>Gather 任务参数（资源类型 + 采集量 + 采集耗时）。</summary>
// QQQ.2 T17 / T19（DR-11：耗时按资源点类型，WoodPile 2s / StonePile 4s / OreVein 8s）
public class GatherTaskArgs
{
    public ResourceType resourceType;
    public int amount;
    /// <summary>采集耗时（秒，取自 BuildingDef.gatherSeconds，数据驱动）。</summary>
    public float gatherSeconds;
}