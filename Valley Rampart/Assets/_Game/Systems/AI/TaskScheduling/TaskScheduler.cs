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
///
/// 2026-08-07 修复（用户报告"工人不工作/采集永远采集中"）：TaskScheduler 原为普通
/// MonoBehaviour 单例，必须场景手动挂载；GameScene 未挂 → HasInstance 恒 false →
/// 建筑不注册、任务永不派发、ProducerComponent.HasWorkerAssigned 恒 false 停产。
/// 改为继承 Singleton&lt;TaskScheduler&gt;（首次访问 Instance 自动创建，DontDestroyOnLoad），
/// 无需场景挂载，任务调度立即可用。
/// </summary>
// QQQ.2 T17 / QQQ.3 B1-7
public class TaskScheduler : Singleton<TaskScheduler>, ITaskScheduler
{
    /// <summary>是否已有实例（访问 Instance 时若未创建会自动创建并返回，调度器始终可用）。</summary>
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
    [Tooltip("规模派工单建筑任务最大同时派工上限（D95，默认不超过 8）。")]
    public int maxWorkersPerTask = 8;

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

    protected override void Awake()
    {
        base.Awake();   // Singleton：自动创建实例 + DontDestroyOnLoad
        _priorityConfig = Resources.Load<TaskPriorityConfig>("Config/TaskPriorityConfig");
        if (_priorityConfig == null)
            Debug.LogWarning("[TaskScheduler] 未找到 TaskPriorityConfig（Resources/Config/TaskPriorityConfig），优先级回退 B。");

        // QQQ.3 B1-1：订阅 NPC 死亡事件清指派
        UnitController.OnUnitDied += OnNpcDied;

        // 2_8 步骤2：寻路失败 → 放弃当前任务（不改单位级），下 tick 换点位（R5）
        EventBus.Subscribe<PathFailedEvent>(OnPathFailed);

        // 2026-08-07 修复：自动创建时补注册——若建筑 OnConstructionComplete 发生在本单例创建前
        // （HasInstance 当时为 false 被跳过），把已 Active 的建筑补纳入任务源，避免"任务永不派发"。
        if (BuildingRegistry.Instance != null && BuildingRegistry.Instance.Count > 0)
        {
            var all = BuildingRegistry.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                // 2_17 步骤3 补丁D收编：不再整块跳过 AI 建筑——收为池隔离主体，AI 建筑也登记为任务源，
                // 但派工按 kingdomId 等路由（工人只领本国任务），玩家调度器天然不匹配 AI 源（见 Tick）。
                // guard 暂留评注：此形式化"过滤器"收编进路由，去留凭步骤3 冒烟取证（裁决⑤-4）。
                if (b != null && b.state == BuildingState.Active && !_sources.Contains(b))
                    Register(b);
            }
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        UnitController.OnUnitDied -= OnNpcDied;
        EventBus.Unsubscribe<PathFailedEvent>(OnPathFailed);
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

    /// <summary>2_8 步骤3（D95）：该源当前被派工人总数（规模派工查询口）。</summary>
    public int CountAssignedWorkers(ITaskSource source)
    {
        if (source == null) return 0;
        int count = 0;
        foreach (var kv in _npcTaskMap)
            if (ReferenceEquals(kv.Value.source, source)) count++;
        return count;
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

    /// <summary>
    /// 2_8 步骤2（R5）：寻路失败事件消费——放弃该工人的当前任务（不改单位级，
    /// 单位自身的移动态由 PathFollower 自理），使下 tick 可换点位/换任务重派，防卡死。
    /// </summary>
    private void OnPathFailed(PathFailedEvent evt)
    {
        if (evt.Unit == null) return;
        int id = evt.Unit.npcId;
        if (id == 0 || !_npcTaskMap.TryGetValue(id, out var task)) return;
        _npcBrainMap.TryGetValue(id, out var brain);
        Abandon(id, task, brain);
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
        var idleKingdom = new List<int>();   // 2_17 步骤3：对齐 idle 池记录每空闲工人归属（池隔离路由用）
        for (int i = 0; i < npcs.Length; i++)
        {
            var n = npcs[i];
            if (n == null || !n.IsAlive || !n.IsIdleForTask) continue;
            var uc = n.GetComponent<UnitController>();
            if (uc == null || uc.npcId == 0) continue;
            // QQQ.4 T3：任务仅派给工人（Worker / Civilian——Civilian 为旧"平民"职业，注释即"从事资源采集/建造"，
            // 可视为工人）——流浪汉/居民/君主/士兵不派任务，修复"流浪汉路过 2 秒抢走玩家采集任务"；
            // Porter 搬运工职业启用时在此追加放行
            var occ = uc.EffectiveOccupation;
            if (occ != Occupation.Worker && occ != Occupation.Civilian) continue;
            if (_npcTaskMap.ContainsKey(uc.npcId)) continue;   // 幂等：已占用不重派
            idle.Add(n);
            idleKingdom.Add(uc.kingdomId);
        }

        // ③ 收集可派任务（QQQ.4 T1：按"源+任务类型"去重，允许同一源并发不同类型任务——
        //    农场可同时派 Production（耕作）+ WaterHaul（挑水），修复"取水+耕作无法同时执行"）
        //    2_8 步骤3（D95）：Transport 去重放宽为按容量（同源可多工人搬运）；其余独占任务按源+类型去重
        var jobs = new List<KingdomTask>();
        foreach (var s in _sources)
        {
            if (s == null || !s.IsValid) continue;
            if (!s.TryAdvertiseTask(out var task)) continue;
            if (task.type == KingdomTaskType.Transport)
            {
                if (RemainingSlots(task) <= 0) continue;   // 规模派工：容量已满不再派
            }
            else if (HasAssignedTaskForSourceType(s, task.type)) continue;
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
            // 2_17 步骤3 池隔离路由：任务源归属 kingdomId——工人只领本国任务（D330），玩家(0)不碰 AI 源、AI 不碰它国源
            int tKingdom = SourceKingdom(task);
            // 规模派工（D95）：运输任务按容量可多派工人；其余独占任务派 1
            int slots = task.type == KingdomTaskType.Transport ? RemainingSlots(task) : 1;
            for (int k = 0; k < slots; k++)
            {
                // 找到距源最近的、同归属国且仍未空闲的 NPC（2_8 步骤1：格单位排序）
                int best = -1;
                float bestDist = float.MaxValue;
                for (int i = 0; i < idle.Count; i++)
                {
                    if (used[i]) continue;
                    // 池隔离：跨归属国不派。例外：无主源(-1，自然建筑/野采) = 先到先得池(D283)，
                    // 任何国空闲工人皆可匹配——玩家回流采集 + AI 野采一并救活（缺陷α）。
                    if (tKingdom >= 0 && idleKingdom[i] != tKingdom) continue;
                    float d = GridMath.DistCells(idle[i].transform.position, task.SourcePos);
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                if (best < 0) break;   // 无对应国空闲工人，剩余任务等待下 tick
                used[best] = true;
                Dispatch(idle[best], task);
            }
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
        InjectStimulus(brain, task);   // TaskStimulus 保留兜底（决策核据此维持工作焦点/威胁挂起）
        NavigateToSource(brain, task); // 2_8 步骤2：PathFollower 直接走向 SourcePos 微格落点
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
    /// 2_8 步骤2：派发后让工人经 PathFollower 直接寻路走向任务源的微格落点
    /// （WorldToSubCoord 吸附 + SubCoordToWorld 中心），提升到达准确/绕障；TaskStimulus 保留兜底。
    /// PathFollower 缺失时自动补挂（与 BehaviorExecutor.EnsurePathFollower 一致）。
    /// </summary>
    private void NavigateToSource(NPCBrain brain, KingdomTask task)
    {
        if (brain == null || task == null) return;
        var uc = brain.GetComponent<UnitController>();
        if (uc == null || GridSystem.Instance == null) return;
        var pf = uc.GetComponent<PathFollower>();
        if (pf == null) pf = uc.gameObject.AddComponent<PathFollower>();
        var subOpt = GridSystem.Instance.WorldToSubCoord(task.SourcePos);
        if (!subOpt.HasValue) return;
        pf.SetDestination(GridSystem.Instance.SubCoordToWorld(subOpt.Value));
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
                        if (task.type == KingdomTaskType.Transport)
                        {
                            // QQQ.4 T11：搬运段——建筑存量入工人背包 → 转 MovingToDest（去仓库/国库卸货）
                            if (LoadInventoryFromSource(brain, task))
                            {
                                _npcStateMap[id] = TaskState.MovingToDest;
                                InjectCarryStimulus(brain, task);
                            }
                            else
                            {
                                Complete(id, task, brain);   // 无货可搬 → 直接完成（ExecuteCompletion 兜底入国库）
                                stale.Add(id);
                            }
                        }
                        else if (task.type == KingdomTaskType.AmmoReload)
                        {
                            // 2_12 步骤9（HH.19 A×4）：装填段——从最近同类弹药仓库取弹入背包 → 转 MovingToDest（回单位弹仓卸货）。
                            // 源=装填目标单位（SourcePos=单位），故取货不走 Building StorageComponent，改走弹药仓库。
                            if (LoadAmmoToBackpack(brain, task))
                            {
                                _npcStateMap[id] = TaskState.MovingToDest;
                                InjectCarryStimulus(brain, task);
                            }
                            else
                            {
                                Complete(id, task, brain);   // 弹药仓空/类型不足 → 完成（等下轮需求）
                                stale.Add(id);
                            }
                        }
                        else
                        {
                            Complete(id, task, brain);
                            stale.Add(id);
                        }
                    }
                    break;

                case TaskState.MovingToDest:
                    // QQQ.4 T11：搬运段——背包资源送往 dest（仓库/国库），到达卸货后完成
                    // 2_12 步骤9：装填段——背包弹药送往单位弹仓（UnitMagazine），到达 FillMagazine 后完成
                    {
                        float arrive = ArrivalThreshold(brain, cellSize);
                        if (Vector2.Distance(brain.transform.position, task.destPos) <= arrive)
                        {
                            if (task.type == KingdomTaskType.AmmoReload)
                                UnloadAmmoToMagazine(brain, task);   // 装填：背包弹药写入单位 A* 弹仓
                            else
                                UnloadInventory(brain, task);        // 搬运：背包资源入仓库/国库
                            Complete(id, task, brain);
                            stale.Add(id);
                        }
                        else if (Time.time - _taskStartTime[id] > taskTimeout)
                        {
                            stale.Add(id);   // 超时未到达卸货点，放弃（背包资源保留，不丢）
                        }
                        else
                        {
                            InjectCarryStimulus(brain, task);   // 未到达续命（目标=destPos）
                        }
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
        ExecuteCompletion(task, brain);
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

    /// <summary>按任务类型执行完成动作（QQQ.2 §10.3；QQQ.4 需求5：Gather/Transport 入工人背包）。</summary>
    private void ExecuteCompletion(KingdomTask task, NPCBrain brain)
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
                // QQQ.4 T11：正常路径已完成（Working→LoadInventoryFromSource→MovingToDest→UnloadInventory）。
                // 此处兜底：无背包组件（非工人）→ 保持旧行为直接入国库，资源不丢。
                var st = comp != null ? comp.GetComponent<StorageComponent>() : null;
                if (st != null && GetInventory(brain) == null)
                    st.HarvestCarry();
                break;

            case KingdomTaskType.WaterHaul:
                if (WaterNetwork.Instance != null)
                    WaterNetwork.Instance.AddWater(waterCarryAmount);
                break;

            case KingdomTaskType.Gather:
                var ga = task.args as GatherTaskArgs;
                if (ga != null)
                {
                    // 2_20 M5/D420：种族采集乘数（Gather 入库侧；与 ProducerComponent.Tick 主产累加
                    // 同源 KingdomRace.GetGatherMul 映射表（D506③）两处同乘防漂移；Max(1) 防低 mul 白干）
                    var guc = brain != null ? brain.GetComponent<UnitController>() : null;
                    float gmul = guc != null ? KingdomRace.GetGatherMul(guc.kingdomId, ga.resourceType) : 1f;
                    int gain = Mathf.Max(1, Mathf.RoundToInt(ga.amount * gmul));
                    // QQQ.4 T10：采集入工人背包（资源生命周期：采集→背包→搬运→仓库）；背包满余量直接入国库兜底
                    var inv = GetInventory(brain);
                    if (inv != null)
                    {
                        int stored = inv.TryStore(ga.resourceType, gain);
                        int overflow = gain - stored;
                        if (overflow > 0 && RulerController.Instance != null)
                            RulerController.Instance.ModifyResource(ga.resourceType, true, overflow);
                    }
                    else if (RulerController.Instance != null)
                    {
                        RulerController.Instance.ModifyResource(ga.resourceType, true, gain);
                    }
                }
                // QQQ.2 T19：采集完成 → 资源点销毁三步（①GridSystem.Free ②BuildingRegistry移除 ③对象池Despawn）
                if (comp is Building b) b.OnGatherCompleted();
                // HH.10 裁决三：数据格树采集源（非实体）完成 → 格翻 Plain + 记重生（TreeGatherSource.OnGatherCompletion 处理）
                else if (task.source is TreeGatherSource tg) tg.OnGatherCompletion();
                break;
        }
    }

    // ===== QQQ.4 T11：搬运两段式辅助（建筑存量→工人背包→仓库/国库）=====

    /// <summary>获取工人背包（prefab 未挂组件则经 UnitController.GetOrAddInventory 补挂，QQQ.4 T8）。</summary>
    private WorkerInventory GetInventory(NPCBrain brain)
    {
        if (brain == null) return null;
        var inv = brain.GetComponent<WorkerInventory>();
        if (inv != null) return inv;
        var uc = brain.GetComponent<UnitController>();
        return uc != null ? uc.GetOrAddInventory() : null;
    }

    /// <summary>搬运第一段：建筑 StorageComponent 存量 → 工人背包（一次携带量）。返回是否搬入成功。</summary>
    private bool LoadInventoryFromSource(NPCBrain brain, KingdomTask task)
    {
        if (brain == null) return false;
        var inv = GetInventory(brain);
        if (inv == null) return false;
        var comp = task.source as Component;
        if (comp == null) return false;
        var st = comp.GetComponent<StorageComponent>();
        if (st == null || st.storedAmount <= 0) return false;
        int max = Mathf.Max(1, st.GetCarryAmount());
        int amount = Mathf.Min(st.storedAmount, max);
        int stored = inv.TryStore(st.resourceType, amount);
        if (stored <= 0) return false;
        st.TakeOut(stored);   // 扣减存量 + 触发 OnStorageChanged（QQQ.4 T11）
        return true;
    }

    /// <summary>搬运第二段：背包 → 最近同类型仓库（StorageComponent.Add；满则入国库兜底），资源不丢。</summary>
    private void UnloadInventory(NPCBrain brain, KingdomTask task)
    {
        if (brain == null) return;
        var inv = GetInventory(brain);
        if (inv == null || inv.IsEmpty) return;
        int amount = inv.UnloadAll();

        // 步骤11：切注册表（WarehouseRegistry.FindNearestAvailable 替 FindObjectsOfType 全场景扫描，D51 就近卸货）
        // 2_17 修复卡γ：第 3 参带工人归属国——玩家工人卸玩家库(0)、AI 工人卸 AI 库，跨王国绝不互卸。
        var wkingdom = brain.GetComponent<UnitController>() != null ? brain.GetComponent<UnitController>().kingdomId : 0;
        StorageComponent best = WarehouseRegistry.FindNearestAvailable(inv.carriedType, brain.transform.position, wkingdom);
        if (best != null)
        {
            int added = best.Add(amount);
            int overflow = amount - added;
            if (overflow > 0 && RulerController.Instance != null)
                RulerController.Instance.ModifyResource(inv.carriedType, true, overflow);
        }
        else if (RulerController.Instance != null)
        {
            RulerController.Instance.ModifyResource(inv.carriedType, true, amount);
        }
    }

    // ===== 2_12 步骤9 装填两段式（D207~D212，HH.19 A×4）：取弹（弹药仓库→背包）→ 卸入单位弹仓 =====

    /// <summary>
    /// 装填取货段：从最近"所需弹种"的 StorageComponent（厂级弹药仓子仓 / 通用仓库）取弹入工人背包。
    /// 源=装填目标单位（非 StorageComponent），故不走 Building 取货，直接扫同类弹药仓。
    /// </summary>
    private bool LoadAmmoToBackpack(NPCBrain brain, KingdomTask task)
    {
        if (brain == null || task == null || !(task.args is ReloadAmmoArgs ra) || ra.ammoType == ResourceType.Gold) return false;
        var inv = GetInventory(brain);
        if (inv == null || inv.IsFull) return false;

        // 找最近且该弹种有余的弹药仓（厂级子仓 / 通用仓库 StorageComponent）
        StorageComponent best = null;
        float bestDist = float.MaxValue;
        var storages = FindObjectsOfType<StorageComponent>();
        for (int i = 0; i < storages.Length; i++)
        {
            var s = storages[i];
            if (s == null || s.resourceType != ra.ammoType || s.storedAmount <= 0) continue;
            float d = GridMath.DistCells(s.transform.position, brain.transform.position);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best == null) return false;

        int max = Mathf.Max(1, WorkerTask.GetCarryAmount(ra.ammoType));
        int amount = Mathf.Min(best.storedAmount, max, ra.amount);   // 适配缺口/装载量/携带量
        if (amount <= 0) return false;
        int stored = inv.TryStore(ra.ammoType, amount);
        if (stored <= 0) return false;
        best.TakeOut(stored);   // 扣弹药仓存量（真源扣一次，防双写）
        return true;
    }

    /// <summary>
    /// 装填卸货段：把背包弹药写入目标单位弹仓（UnitController.FillMagazine）。
    /// 背包剩余（弹仓满）保留留待下轮或入国库兜底不丢。
    /// </summary>
    private void UnloadAmmoToMagazine(NPCBrain brain, KingdomTask task)
    {
        if (brain == null || task == null) return;
        var inv = GetInventory(brain);
        if (inv == null || inv.IsEmpty) return;
        // 源=装填目标单位（塔/机器自身），其 UnitController 即弹仓载体
        var target = task.source as UnitController;
        if (target == null || !target.IsAlive) return;
        int amount = inv.UnloadAll();
        int filled = target.FillMagazine(inv.carriedType, amount);
        // 装满后若仍有剩余（不该发生：装填前按缺口取量），回倒下匣——库存仍归还原地仓库
        if (filled < amount)
        {
            int leftover = amount - filled;
            DepositAmmoBack(inv.carriedType, leftover, target.transform.position);
        }
    }

    /// <summary>装填剩余弹药退回最近同类仓库（不丢资源）。</summary>
    private void DepositAmmoBack(ResourceType type, int amount, Vector2 nearPos)
    {
        if (amount <= 0) return;
        StorageComponent best = null;
        float bestDist = float.MaxValue;
        var storages = FindObjectsOfType<StorageComponent>();
        for (int i = 0; i < storages.Length; i++)
        {
            var s = storages[i];
            if (s == null || s.resourceType != type || s.capacity <= s.storedAmount) continue;
            float d = GridMath.DistCells(s.transform.position, nearPos);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best != null) { best.Add(amount); return; }
        // 无同类仓 → 入国库兜底（弹药不入国库除非改此兜底，此处保守：退回源仓失败才走国库）
        RulerController.Instance?.ModifyResource(type, true, amount);
    }

    /// <summary>搬运段刺激注入：目标 = destPos（仓库/国库），区别于 Working 段的 SourcePos 刺激。</summary>
    private void InjectCarryStimulus(NPCBrain brain, KingdomTask task)
    {
        if (brain == null) return;
        brain.RemoveTaskStimulus(task.source);
        brain.AddTaskStimulus(new TaskStimulus(
            GetPriority(task.type),
            Vector2XUnity.FromUnity(task.destPos),
            task.intensity,
            expiry: Time.time + taskExpiry,
            issuer: task.source));
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
                task.destPos = ResolveWaterSource(task);
                break;
            case KingdomDestType.SpecificBuilding:
            case KingdomDestType.UnitMagazine:   // 2_12 步骤9：终点=单位自身位置（发布时已设 destPos）；此处保持
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
            float d = GridMath.DistCells(s.transform.position, task.SourcePos);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best != null) return best.transform.position;
        return ResolveTreasury(task);
    }

    /// <summary>挑水水源位置 = 最近 Active 水井（QQQ.4 T2：修复挑水目标指向 WaterNetwork.transform 恒 (0,0) 的 bug），无则回退任务源。</summary>
    private Vector2 ResolveWaterSource(KingdomTask task)
    {
        var wells = FindObjectsOfType<Building>();
        Building best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < wells.Length; i++)
        {
            var w = wells[i];
            if (w == null || w.def == null || w.def.id != "Well" || w.state != BuildingState.Active) continue;
            float d = GridMath.DistCells(w.transform.position, task.SourcePos);
            if (d < bestDist) { bestDist = d; best = w; }
        }
        if (best != null) return best.transform.position;
        return task != null ? task.SourcePos : Vector2.zero;
    }

    // ===== 辅助 =====

    /// <summary>该源当前是否已有同类型任务在派（QQQ.4 T1：按源+任务类型去重，允许同一源并发不同类型任务）。</summary>
    private bool HasAssignedTaskForSourceType(ITaskSource source, KingdomTaskType type)
    {
        foreach (var kv in _npcTaskMap)
            if (ReferenceEquals(kv.Value.source, source) && kv.Value.type == type) return true;
        return false;
    }

    /// <summary>该源当前已被派的同类型任务数（规模派工按容量计数）。</summary>
    private int CountAssignedForType(ITaskSource source, KingdomTaskType type)
    {
        if (source == null) return 0;
        int count = 0;
        foreach (var kv in _npcTaskMap)
            if (ReferenceEquals(kv.Value.source, source) && kv.Value.type == type) count++;
        return count;
    }

    /// <summary>2_8 步骤3（D95）：理想工人数 = ceil(资源总量/单次携带量)，clamp 到 [1, maxWorkersPerTask]。</summary>
    private int RequiredWorkers(KingdomTask task)
    {
        if (task == null || !(task.args is ScaleTaskArgs scale)) return 1;
        int total = scale.totalResourceDemand;
        if (total <= 0) return 1;
        int carry = Mathf.Max(1, WorkerTask.GetCarryAmount(scale.resourceType));
        int ideal = Mathf.CeilToInt((float)total / carry);
        return Mathf.Clamp(ideal, 1, Mathf.Max(1, maxWorkersPerTask));
    }

    /// <summary>该任务还缺多少派工名额（理想人数 - 已派同类型工人数，下限 0）。</summary>
    private int RemainingSlots(KingdomTask task)
    {
        int required = RequiredWorkers(task);
        int assigned = CountAssignedForType(task.source, task.type);
        return Mathf.Max(0, required - assigned);
    }

    private TaskPriority GetPriority(KingdomTaskType type)
    {
        return _priorityConfig != null ? _priorityConfig.Get(type) : TaskPriority.B;
    }

    /// <summary>2_17 步骤3 池隔离：任务源归属国（非 Building 源如 TreeGatherSource 归玩家 kingdomId=0；
    /// 无主源 -1（自然建筑）在路由时降级为先到先得池，任何国可匹配）。</summary>
    private int SourceKingdom(KingdomTask task)
    {
        return task != null && task.source is Building b ? b.kingdomId : 0;
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
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;
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

/// <summary>
/// 规模派工参数（2_8 步骤3 / D95）：建筑发布任务时把资源总需求附带进 task.args，
/// 调度器按"理想工人数 = ceil(资源总量/单次携带量)"上限 maxWorkersPerTask 分配多工人。
/// </summary>
public class ScaleTaskArgs
{
    public ResourceType resourceType;
    public int totalResourceDemand;
}