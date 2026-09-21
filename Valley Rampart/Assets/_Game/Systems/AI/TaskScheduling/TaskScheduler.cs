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
///
/// 2_23 资源 P0 批B（R-B1，D529/D634）：排序键升级为三段式＝死表（S/A/B/C 保留不动）×
/// 资源偏向活权重（诊断层缺口信号）→ 距离；权重出自 ResourceBiasConfig SO（出厂 1.0 占位）。
/// 边界注记（R-B3 / 2_23 §八 S-B4 / D525 打分器同型）：**活权重住本类（Unity 侧执行层）
/// 零镜像、不进 AI.Core**——sim 无空间/派工调度概念；权重若归 champion，则由训练仓
/// `15_账本`登记「Unity 单侧消费」形态（**登记面属训练仓，本仓不代提**）。
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
    [Tooltip("WaterHaul 一次搬水量（⛔ `M1-F` 起**不再参与搬水链** —— 装载上限改由 `StorageComponent.GetCarryAmount(Water)` 决定；"
             + "字段保留占位：`GameScene` 序列化稳定面 · 整数化 `D807` Q4）")]
    public int waterCarryAmount = 10;
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
    private ResourceBiasConfig _biasConfig;   // 2_23 资源 P0 批B/R-B1（D529）：资源偏向活权重（SO 可配）

    // ===== 单例 =====

    protected override void Awake()
    {
        base.Awake();   // Singleton：自动创建实例 + DontDestroyOnLoad
        _priorityConfig = Resources.Load<TaskPriorityConfig>("Config/TaskPriorityConfig");
        if (_priorityConfig == null)
            Debug.LogWarning("[TaskScheduler] 未找到 TaskPriorityConfig（Resources/Config/TaskPriorityConfig），优先级回退 B。");

        // 2_23 资源 P0 批B/R-B1（D529/D634）：资源偏向活权重配置（缺 asset → 出厂 1.0 占位实例=零行为差异）
        _biasConfig = ResourceBiasConfig.Load();

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

    /// <summary>⭐ `U-8` 件5（`D800` `Q5` · **形态甲**）：该源当前被派工人总数（**带任务过滤**）。
    /// 例：只计「拆除任务」（`args is DemolishTaskArgs`）⇒ 防「拆除前已在派的 `Production`／`Transport`
    /// 残留任务」被误算成拆除协作工人（虚增 `n` ⇒ 缩短拆除时长）。
    /// ⭐ **调用面 ＝ 1 处**：`Building.DemolishDuration()`（`n` 过滤谓词 `Building.IsDemolishTask`）。
    /// ⚠️ 谓词应为**静态**（⛔ 不捕获 ⇒ Roslyn 缓存委托 ⇒ 无每帧分配）。</summary>
    public int CountAssignedWorkers(ITaskSource source, System.Func<KingdomTask, bool> filter)
    {
        if (source == null) return 0;
        int count = 0;
        foreach (var kv in _npcTaskMap)
        {
            if (!ReferenceEquals(kv.Value.source, source)) continue;
            if (filter != null && !filter(kv.Value)) continue;
            count++;
        }
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
            // HH.86/DZ-064 件1d：Porter 放行（旧白训实锤=可训练不领任务，HH.85 审计）。
            var occ = uc.EffectiveOccupation;
            if (occ != Occupation.Worker && occ != Occupation.Civilian
                && occ != Occupation.Porter) continue;
            if (_npcTaskMap.ContainsKey(uc.npcId)) continue;   // 幂等：已占用不重派
            idle.Add(n);
            idleKingdom.Add(uc.kingdomId);
        }

        // ③ 收集可派任务（QQQ.4 T1：按"源+任务类型"去重，允许同一源并发不同类型任务——
        //    农场可派 Production（耕作）＋搬水任务（⭐ `M1-F` 件4 起源＝**水井** ⇒ 去重键＝「水井+WaterHaul」
        //    ⚠️ 同 tick 内同一水井只服务一个农场（串行）· 完成后再广告下一个）
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

        // ④ 有效优先级排序（2_23 资源 P0 批B/R-B1，D529/D634）：
        //    死表（S/A/B/C 保留不动）× 资源偏向活权重（诊断层缺口信号）→ 距离（下方循环，DR-17）。
        //    有效优先级 = 死表值 × clamp(bias[r]×(1+k×缺口率), minWeight, maxWeight)；
        //    S 级（死表最高档）不乘权重（保命硬红线）+ 非 S 硬上界 < S 值（防配置越界架空死表）。
        //    ⚠ 确定性（补-5，HH.172 §五）：同有效优先级 → 确定性次级键（源坐标 y→x→任务类型）
        //    ＝严格全序，消除 List.Sort 不稳 + _sources(HashSet) 枚举序不可保证的潜在乱序。
        jobs.Sort(CompareByEffectivePriority);

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

    /// <summary>
    /// ⭐ `HH.316` 件6（`D798` 裁 ①「链 B 并回链 A」· `09` §9.8 :390「玩家手点 ＝ **调用搬运任务的一种形式**」）：
    /// 给某任务源**立案一个搬运任务**并**立即**调度一次（源已在册 ⇒ 直接广告；工人在场即来搬；
    /// 无空闲工人则照常每 tick 广告、来日再来）。
    /// ⛔ **无独立入账口** —— 到账只经链 A 卸货段（`UnloadInventory` → `AddGatherOverflow` 分流）。
    /// 幂等：在派工人由「源＋类型去重/规模派工」拦下（⛔ 不重复派工）。
    /// </summary>
    public void RequestHaulNow(ITaskSource source)
    {
        if (source == null) return;
        if (!_sources.Contains(source)) Register(source);
        Tick();   // 立即派发一次（常规 tick 由 Update 照旧驱动，⛔ 不改变其节律）
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
            TaskState st = _npcStateMap.TryGetValue(id, out var cur) ? cur : TaskState.Assigned;
            if (task.source == null || !task.source.IsValid)
            {
                // ⭐ HH.316 件2 例外：**箱源**「已装载在途」（MovingToDest）**不因源失效放弃** ——
                //   箱＝仓 ⇒ 工人搬走**最后一批**后容器即空（IsValid false），若照旧放弃 ⇒ 该批滞留背包、
                //   到账断链（判据1）。装载前各态（Assigned/MovingToSource/Working）照旧放弃。
                if (!(task.source is ChestEntity && st == TaskState.MovingToDest))
                {
                    stale.Add(id);   // 源失效，放弃
                    continue;
                }
            }

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
                                ResolveChestDest(brain, task);   // ⭐ HH.316 件4：箱源第二段落点（装载后即时解析）
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
                        else if (task.type == KingdomTaskType.Build && task.args is HaulToSiteArgs)
                        {
                            // ⭐ M1-C 件1：搬料装载段（`09` §16.1 ②）——从取料仓取料入背包，
                            //   上限＝**工地仓当前缺口量**（⭐ 阈值拦截：够阈值即停 ⇒ ⛔ 不多搬）。
                            if (LoadSiteMaterials(brain, task))
                            {
                                _npcStateMap[id] = TaskState.MovingToDest;
                                InjectCarryStimulus(brain, task);
                            }
                            else
                            {
                                Complete(id, task, brain);   // 取料仓已空 / 阈值已满足 → 完成（等下轮广告）
                                stale.Add(id);
                            }
                        }
                        else if (task.type == KingdomTaskType.WaterHaul && task.args is HaulWaterArgs)
                        {
                            // ⭐ `M1-F` 件5 搬水**装载段**（`09#44` 真搬运 · `09` §4.3 D 组）：从 `task.source`
                            //   （＝**水井**）的 `StorageComponent` 取水入背包 —— 与 Transport 同构 ⇒ 复用
                            //   `LoadInventoryFromSource`（源是建筑仓 ⇒ `GetComponent<StorageComponent>` 直接命中；
                            //   装载上限＝`st.GetCarryAmount(Water)`）。
                            //   ⚠️ 退役前本任务落 `else ⇒ Complete`（半假搬运：水凭空入桶）⇒ 本分支是修复核心。
                            if (LoadInventoryFromSource(brain, task))
                            {
                                _npcStateMap[id] = TaskState.MovingToDest;
                                InjectCarryStimulus(brain, task);
                            }
                            else
                            {
                                Complete(id, task, brain);   // 水井仓已空 → 完成（等下轮广告）
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
                            else if (task.type == KingdomTaskType.Build && task.args is HaulToSiteArgs)
                                DepositToSite(brain, task);          // ⭐ M1-C 件1：卸料进「工地仓」（阈值拦截）
                            else if (task.type == KingdomTaskType.WaterHaul && task.args is HaulWaterArgs wa)
                                DepositWaterToFarm(brain, task, wa); // ⭐ `M1-F` 件5 搬水卸货段：卸水入农场仓
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
            // HH.264 A2/F-02：Working 期＝工作循环动画（attack Loop，不回落；战斗 attack 才是 OnceReturn）
            uc.NotifyWorkVisual();
        }
        OverheadSpeech.Show(brain.transform, "劳作中…", duration: 0.8f);
    }

    /// <summary>放弃任务：清记录 + 复位工人 + 移除刺激（不执行完成动作）。</summary>
    private void Abandon(int npcId, KingdomTask task, NPCBrain brain)
    {
        // QQQ.2 T19 / RES-A2：Gather 中断（工人阵亡/被打断/源失效）→ 资源点解锁可再点击（进度重置不保留）
        // 【HH.294 片 6-2·B-4①】原 `gb.isBeingGathered = false`（Building 采集锁）**随实体退役已删** ——
        //   数据寻址后「解锁」＝源自身失效退出 `_sources`（`WorldGatherSource.IsValid`：完成/放弃即失效），
        //   无锁需要复位；玩家可再次右键同名资源格重新立案（`ConfirmResourceGather` 幂等新建源）。
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
                var carryInv = GetInventory(brain);
                if (st != null && carryInv == null)
                    st.HarvestCarry();
                // DZ-072a（HH.107）：满背包工人取货失败兜底卸货——旧路径背包满→取货失败→Complete 不卸→
                // 重派再失败=死循环；就地 UnloadInventory（就近同国仓/台账兜底）根除循环，资源不丢。
                else if (carryInv != null && !carryInv.IsEmpty)
                    UnloadInventory(brain, task);
                break;

            // ⭐ `M1-F` 件5：原 `case KingdomTaskType.WaterHaul`（完成 ⇒ `AddWater(waterCarryAmount,…)` 凭空入桶）
            //   **整段已删** —— 新链路（件5 装载/卸货两段）下 WaterHaul 在 Working 即转 MovingToDest，
            //   ⛔ 不会落到 ExecuteCompletion；保留会残留"水凭空入桶"路径（`09#44` 禁）。

            case KingdomTaskType.Gather:
                var ga = task.args as GatherTaskArgs;
                if (ga != null)
                {
                    // 2_20 M5/D420：种族采集乘数（Gather 入库侧；与 ProducerComponent.Tick 主产累加
                    // 同源 KingdomRace.GetGatherMul 映射表（D506③）两处同乘防漂移；Max(1) 防低 mul 白干）
                    var guc = brain != null ? brain.GetComponent<UnitController>() : null;
                    float gmul = guc != null ? KingdomRace.GetGatherMul(guc.kingdomId, ga.resourceType) : 1f;
                    int gain = Mathf.Max(1, Mathf.RoundToInt(ga.amount * gmul));
                    // QQQ.4 T10：采集入工人背包（资源生命周期：采集→背包→搬运→仓库）；背包满余量按国分流（HH.86 件2d）
                    var inv = GetInventory(brain);
                    if (inv != null)
                    {
                        int stored = inv.TryStore(ga.resourceType, gain);
                        int overflow = gain - stored;
                        if (overflow > 0) AddGatherOverflow(guc, ga.resourceType, overflow);
                    }
                    else
                    {
                        AddGatherOverflow(guc, ga.resourceType, gain);
                    }
                }
                // 【HH.294 片 6-2·B-4②】原 `if (comp is Building b) b.OnGatherCompleted();`（一次性资源点实体完成）
                //   **随实体退役已删**；收尾批清场后世界资源点采集源只剩 `WorldGatherSource`（覆盖四型全部）。
                // 世界资源点采集源（玩家四型 ＋ AI 四型）完成 → 格翻 Plain ＋ 记重生（数据寻址统一）
                if (task.source is WorldGatherSource wg) wg.OnGatherCompletion();
                break;
        }
    }

    // ===== QQQ.4 T11：搬运两段式辅助（建筑存量→工人背包→仓库/国库）=====

    /// <summary>
    /// HH.86/DZ-045 件2d：采集溢出/无背包入国库按工人国分流——玩家(0)=RulerController 原逻辑逐位；
    /// AI(>0)=本国 KingdomState.AddResources 台账（旧恒入玩家库=资敌实锤）；国已注销=丢弃+日志（防亡国资源入玩家库）。
    /// ⭐ `M1-E`：**金经此口亦入仓**（玩家 ⇒ `ModifyResource(..., 0)` ⇒ 玩家国库仓；AI ⇒ `AddResources` ⇒
    /// `AddToLedger` 金条目 ⇒ `ChangeTreasuryGold` ⇒ 本国国库仓）—— ⛔ 本方法逻辑无需改（落点已透明）。
    /// </summary>
    private void AddGatherOverflow(UnitController uc, ResourceType type, int amount)
    {
        if (amount <= 0) return;
        if (uc == null || uc.kingdomId <= 0)
        {
            if (RulerController.Instance != null)
                RulerController.Instance.ModifyResource(type, true, amount);
            return;
        }
        var k = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(uc.kingdomId) : null;
        if (k == null)
        {
            Debug.Log($"[TaskScheduler] 采集溢出丢弃：国 {uc.kingdomId} 已注销，{type}×{amount} 不入玩家库（资敌防线）");
            return;
        }
        // ⭐ M1-A：五经济资源改走「资源量列表」入账；副产三台账桶（原有独立 int 桶）保持原样
        switch (type)
        {
            case ResourceType.Crystal: k.crystal += amount; return;   // DZ-072a：副产台账桶（HH.107 件2）
            case ResourceType.FireOil: k.fireOil += amount; return;   // DZ-072a：副产台账桶
            case ResourceType.Ore: k.ore += amount; return;           // T1.8（D609/D617）：矿石 AI 独立桶（照副产先例）
            case ResourceType.Gold:
            case ResourceType.Stone:
            case ResourceType.Wood:
            case ResourceType.Food:
            case ResourceType.Metal:
                k.AddResources(ResourceList.Of(new ResourceAmount(type, amount)));
                return;
            default:
                Debug.Log($"[TaskScheduler] 采集溢出丢弃：{type} 非国库五资源/副产桶（AI 台账无此桶），×{amount}");
                return;
        }
    }

    /// <summary>获取工人背包（prefab 未挂组件则经 UnitController.GetOrAddInventory 补挂，QQQ.4 T8）。</summary>
    private WorkerInventory GetInventory(NPCBrain brain)
    {
        if (brain == null) return null;
        var inv = brain.GetComponent<WorkerInventory>();
        if (inv != null) return inv;
        var uc = brain.GetComponent<UnitController>();
        return uc != null ? uc.GetOrAddInventory() : null;
    }

    /// <summary>搬运第一段：建筑 StorageComponent 存量 → 工人背包（一次携带量）。返回是否搬入成功。
    /// DZ-072a：矿洞副产任务（source=MineByproductComponent，本体无 StorageComponent）按 args 资源类型取副产子仓。</summary>
    private bool LoadInventoryFromSource(NPCBrain brain, KingdomTask task)
    {
        if (brain == null) return false;
        var inv = GetInventory(brain);
        if (inv == null) return false;
        // ⭐ HH.316 件2（箱源混装防护 · 照 M1-C `LoadSiteMaterials` 先例）：箱内容物**任意资源**，工人可能
        //   带着上一趟其它资源（背包单资源不可混装 ⇒ `TryStore` 恒拒 ⇒ 装载恒失败 ⇒ Complete ⇒ 重派）。
        //   ⇒ 箱源先就地卸空（走 `UnloadInventory` · 就近同国仓/归属国分流兜底 · 资源不丢），再取货。
        if (task.source is ChestEntity && !inv.IsEmpty) UnloadInventory(brain, task);
        var comp = task.source as Component;
        if (comp == null) return false;
        var st = comp.GetComponent<StorageComponent>();   // ⭐ 箱＝仓：箱容器挂本体 ⇒ 此处直接命中（件1 附益）
        // 副产子仓取货（DZ-072a）：本体仓缺失或类型与 args 不符（Building ③ 广告任务 args 恒等于本体仓类型，不受影响），
        // 且 source 挂有副产组件时 → 按 args.resourceType 取对应副产子仓。
        // ⭐ M1-A：单资源 `resourceType` ⇒ 标签判 `Accepts`（多资源仓下「符不符」＝收不收这个资源）。
        if (task.args is ScaleTaskArgs sa && (st == null || !st.Accepts(sa.resourceType)))
        {
            var byprod = comp.GetComponent<MineByproductComponent>();
            st = byprod != null ? byprod.GetStore(sa.resourceType) : null;
        }
        // ⏭️ 单资源语义假设点（M1-G 收口）：仓内首资源 ＝ 旧单一 `resourceType`（迁移后仓恒单型，读数逐一相同）。
        if (st == null || st.TotalCount <= 0) return false;
        var carried = st.PrimaryStoredType();
        int max = Mathf.Max(1, st.GetCarryAmount(carried));
        int amount = Mathf.Min(st.GetAmount(carried), max);
        int stored = inv.TryStore(carried, amount);
        if (stored <= 0) return false;
        st.TakeOut(carried, stored);   // 扣减存量 + 触发 OnStorageChanged（QQQ.4 T11）
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
        var uc = brain.GetComponent<UnitController>();
        var wkingdom = uc != null ? uc.kingdomId : 0;
        // ⭐ `M1-E`（`09#47` · `D805` 件4）：**金已切仓** —— 金与普通资源同路（`FindNearestAvailable`
        //   会命中本国国库容器 `res_currency` 条目 · 体积 0 ⇒ 恒有余量）⇒ ⛔ 原「金直通」（走
        //   `RulerController` ⇒ `Gold` 字段）**退役**；无可用仓时由 `AddGatherOverflow` 兜底（金 ⇒
        //   `ModifyResource(..., kingdomId)` ⇒ 该国国库仓）。`U-7` 已修 ⇒ 水井不再收金。
        // DZ-072a（HH.107 件2）：副产两资源按归属国路由——玩家(0)卸国库 Vault（TreasureVault.Managed 扩面）；
        // AI(>0) 直走台账 AddGatherOverflow（AI 经济=台账制 2_17 §追记②；AI 主城 Vault 系 CastleCore 无守卫
        // 误挂的玩家国库结构=消费黑洞，AI 消费面读台账不读 Vault，卸进去即黑洞——照 AddWater 桶路由先例语义）。
        if (wkingdom > 0 && (inv.carriedType == ResourceType.Crystal || inv.carriedType == ResourceType.FireOil))
        {
            // ⚠️ 连带修复（`HH.316`）：原此处复调 `inv.UnloadAll()`（顶部已清空 ⇒ 恒返 0 ⇒ 该批**静默丢**）
            //   —— 箱内容物含副产（任意资源）时 AI 工人搬走即丢 ⇒ 与 U-2「内容物到账」硬冲突 ⇒ 改用已取出的 `amount`。
            AddGatherOverflow(uc, inv.carriedType, amount);
            return;
        }
        StorageComponent best = WarehouseRegistry.FindNearestAvailable(inv.carriedType, brain.transform.position, wkingdom);
        if (best != null)
        {
            int added = best.Add(inv.carriedType, amount);
            int overflow = amount - added;
            // DZ-073（HH.107 件2）：溢出兜底改归属国分流——旧硬编码 RulerController=AI 溢出资玩家库；=0 玩家逐位零回归。
            if (overflow > 0)
                AddGatherOverflow(uc, inv.carriedType, overflow);
        }
        else
        {
            // DZ-073：无仓兜底同病同修（旧硬编码玩家国库→归属国分流）。
            AddGatherOverflow(uc, inv.carriedType, amount);
        }
    }

    /// <summary>
    /// ⭐ `HH.316` 件4：**箱源**搬运的第二段落点 —— **装载成功后**按**搬运者国 ＋ 实载资源**即时解析。
    /// 为什么不能照建筑在广告时解析：箱**无主**（`SourceKingdom → -1`）⇒ `ResolveWarehouse` 对 `-1` 全跳过
    /// （回退国库锚点）⇒ 故箱源 `destType=None`，落点在此定（`InjectCarryStimulus` 前）。
    /// 规则（⭐ `M1-E`/`D805` 件4 后**统一**）：最近「**同国 ＋ 收该资源 ＋ 有余量**」仓
    /// （`WarehouseRegistry.FindNearestAvailable`）—— **金亦同路**（命中本国国库容器）；无仓回退国库锚点。
    /// 到账由 `UnloadInventory` 统一收口（就近仓 ＋ `AddGatherOverflow` 兜底 · 资源不丢）。
    /// </summary>
    private void ResolveChestDest(NPCBrain brain, KingdomTask task)
    {
        if (task == null || !(task.source is ChestEntity)) return;
        var inv = GetInventory(brain);
        var uc = brain != null ? brain.GetComponent<UnitController>() : null;
        int kingdom = uc != null ? uc.kingdomId : 0;                       // 谁搬回 ⇒ 归谁国（09 §9.8）
        var type = inv != null ? inv.carriedType : ResourceType.Gold;
        if (brain != null)
        {
            var best = WarehouseRegistry.FindNearestAvailable(type, brain.transform.position, kingdom);
            if (best != null) { task.destPos = best.transform.position; return; }
        }
        task.destPos = ResolveTreasury(task);   // 无可用仓 ⇒ 国库锚点（卸货段分流兜底）
    }

    // ===== ⭐ M1-C 件1：搬料两段式（取料仓 → 工人背包 → 工地仓）=====

    /// <summary>
    /// 搬料第一段（`09` §16.1 ②）：**取料仓 → 工人背包**。
    /// ⭐ **阈值拦截**：装载上限 ＝ 工地仓**当前**缺口量（每次实时重读，⛔ 不用广告时的快照）
    /// ⇒ 「够阈值即停 ⇒ 不多搬」（`09` §16.1-2）。返回是否搬入成功。
    /// </summary>
    private bool LoadSiteMaterials(NPCBrain brain, KingdomTask task)
    {
        if (brain == null || !(task.args is HaulToSiteArgs ha)) return false;
        var inv = GetInventory(brain);
        if (inv == null) return false;
        var st = ha.pickup;
        if (st == null) return false;
        // ⚠️ 背包混装防护（M1-C 冒烟 §B 实测暴露）：工人可能带着上一趟的**其它**资源（采粮/搬运未卸），
        //   此时 `TryStore` 因「单资源背包不可混装」恒返 0 ⇒ 装载恒失败 ⇒ `Complete` ⇒ 下 tick 重派 ⇒
        //   **同一工人死循环**（Build 类型 `ExecuteCompletion` 无兜底）。照 `Transport` 的 `UnloadInventory`
        //   兜底先例：先就地卸空，再继续本趟装载。
        if (!inv.IsEmpty && inv.carriedType != ha.resourceType) UnloadInventory(brain, task);
        int have = st.GetAmount(ha.resourceType);
        if (have <= 0) return false;
        // ⭐ 阈值上限：工地仓当前缺口（工地已销毁/已料齐 ⇒ 缺口 0 ⇒ 不搬）
        int need = ha.site != null ? ha.site.Remaining(ha.resourceType) : ha.need;
        if (need <= 0) return false;
        int max = Mathf.Max(1, st.GetCarryAmount(ha.resourceType));
        int amount = Mathf.Min(have, Mathf.Min(max, need));
        if (amount <= 0) return false;
        int stored = inv.TryStore(ha.resourceType, amount);
        if (stored <= 0) return false;
        st.TakeOut(ha.resourceType, stored);   // 扣减取料仓 + 触发 OnStorageChanged
        return true;
    }

    /// <summary>
    /// 搬料第二段（`09` §16.1 ②）：**工人背包 → 工地仓**。
    /// `ConstructionSiteStore.Deposit` 内部再夹一次阈值（并发搬料下仍不多收）；
    /// 被拒的余量**退回取料仓**（不丢资源）。工地已销毁 ⇒ 余量按国分流兜底。
    /// </summary>
    private void DepositToSite(NPCBrain brain, KingdomTask task)
    {
        if (brain == null || !(task.args is HaulToSiteArgs ha)) return;
        var inv = GetInventory(brain);
        if (inv == null || inv.IsEmpty) return;
        var type = inv.carriedType;
        int amount = inv.UnloadAll();
        if (amount <= 0) return;

        var site = ha.site;
        if (site == null)
        {
            // 工地已销毁（拆了/打毁了）⇒ 不丢资源：退回取料仓，退不进再按国分流
            ReturnOverflow(brain, type, amount, ha.pickup);
            return;
        }
        int accepted = site.Deposit(type, amount);   // ⭐ 阈值拦截在此生效
        int overflow = amount - accepted;
        if (overflow > 0) ReturnOverflow(brain, type, overflow, ha.pickup);
    }

    /// <summary>搬料余量兜底（阈值拦截拒收 / 工地消失）：先退回取料仓，退不进再按归属国分流（不丢资源）。</summary>
    private void ReturnOverflow(NPCBrain brain, ResourceType type, int amount, StorageComponent pickup)
    {
        if (amount <= 0) return;
        int back = pickup != null ? pickup.Add(type, amount) : 0;
        int lost = amount - back;
        if (lost > 0) AddGatherOverflow(brain != null ? brain.GetComponent<UnitController>() : null, type, lost);
    }

    /// <summary>
    /// ⭐ `M1-F` 件5 搬水第二段（`09#44` · `09` §4.3 D 组）：**工人背包（水）→ 农场仓**。
    /// 卸货走农场仓 `Add`（放到满为止 · 部分成功 · `09` §7.1）；落点失效或被拒的余量
    /// **退回水井仓**（`task.source`），退不进再按归属国分流（照 `ReturnOverflow` 形制 · ⛔ 不丢资源）。
    /// </summary>
    private void DepositWaterToFarm(NPCBrain brain, KingdomTask task, HaulWaterArgs wa)
    {
        if (brain == null || wa == null) return;
        var inv = GetInventory(brain);
        if (inv == null || inv.IsEmpty) return;
        var type = inv.carriedType;          // ⛔ 仍单资源背包（多资源化属 `M1-F` 的 `F-2` 批）
        int amount = inv.UnloadAll();
        if (amount <= 0) return;

        var srcComp = task != null ? task.source as Component : null;
        var wellStore = srcComp != null ? srcComp.GetComponent<StorageComponent>() : null;   // 退回落点＝水井仓
        var target = wa.target;
        if (target == null)
        {
            // 农场仓已毁（农场被拆/打毁）⇒ 不丢资源：退回水井仓，退不进再按国分流
            ReturnOverflow(brain, type, amount, wellStore);
            return;
        }
        int accepted = target.Add(type, amount);      // 放到满为止（部分成功）
        int overflow = amount - accepted;
        if (overflow > 0) ReturnOverflow(brain, type, overflow, wellStore);
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
            if (s == null || s.GetAmount(ra.ammoType) <= 0) continue;
            if (IsChestStore(s)) continue;   // ⭐ HH.316：箱容器⛔ 不作弹药来源（见 IsChestStore 注）
            float d = GridMath.DistCells(s.transform.position, brain.transform.position);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best == null) return false;

        int max = Mathf.Max(1, WorkerTask.GetCarryAmount(ra.ammoType));
        int amount = Mathf.Min(best.GetAmount(ra.ammoType), max, ra.amount);   // 适配缺口/装载量/携带量
        if (amount <= 0) return false;
        int stored = inv.TryStore(ra.ammoType, amount);
        if (stored <= 0) return false;
        best.TakeOut(ra.ammoType, stored);   // 扣弹药仓存量（真源扣一次，防双写）
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
            var uc = brain.GetComponent<UnitController>();
            DepositAmmoBack(uc, inv.carriedType, leftover, target.transform.position);
        }
    }

    /// <summary>装填剩余弹药退回最近同类仓库（不丢资源）。owner=弹药来源单位（DZ-073 归属国分流兜底用）。</summary>
    private void DepositAmmoBack(UnitController owner, ResourceType type, int amount, Vector2 nearPos)
    {
        if (amount <= 0) return;
        StorageComponent best = null;
        float bestDist = float.MaxValue;
        var storages = FindObjectsOfType<StorageComponent>();
        for (int i = 0; i < storages.Length; i++)
        {
            var s = storages[i];
            if (s == null || !s.Accepts(type) || s.CanAccept(type) <= 0) continue;
            if (IsChestStore(s)) continue;   // ⭐ HH.316：箱容器⛔ 不作退弹落点（见 IsChestStore 注）
            float d = GridMath.DistCells(s.transform.position, nearPos);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best != null) { best.Add(type, amount); return; }
        // 无同类仓 → 归属国分流兜底（DZ-073，HH.107 件2：旧硬编码 RulerController 玩家国库=AI 退弹资玩家库；
        // 此处无 brain/uc 上下文，改签名带 uc 由调用方传入——=0 玩家原路径逐位，>0 入 AI 台账）。
        AddGatherOverflow(owner, type, amount);
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
            // ⭐ `M1-F` 件5：原 `case KingdomDestType.WaterNetwork ⇒ ResolveWaterSource` **已删** ——
            //   该枚举值退役保占位（`D807` Q6），搬水任务改由广告侧直接设 `destPos`
            //   （`Building.TryAdvertiseTask` ⇒ `SpecificBuilding` ＋ 农场坐标）。
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

    /// <summary>最近可用仓库（StorageComponent 中最近且 capacity&gt;stored），无则回退国库。
    /// DZ-072a/DZ-073（HH.107）：对齐 UnloadInventory.FindNearestAvailable 既有语义——同国+同资源类型过滤。
    /// 旧全场景不过滤=跨国远目的地/异型目的地（副产任务曾解析到跨国仓→工人超时→满背包死循环温床）。</summary>
    private Vector2 ResolveWarehouse(KingdomTask task)
    {
        var storages = FindObjectsOfType<StorageComponent>();
        StorageComponent best = null;
        float bestDist = float.MaxValue;
        int kingdom = SourceKingdom(task);
        ResourceType? want = task != null && task.args is ScaleTaskArgs sa ? sa.resourceType : (ResourceType?)null;
        for (int i = 0; i < storages.Length; i++)
        {
            var s = storages[i];
            if (s == null || s.IsFull) continue;                       // 已满不收
            if (IsChestStore(s)) continue;                             // ⭐ HH.316：箱容器⛔ 不作落点（见 IsChestStore 注）
            if (want.HasValue && !s.Accepts(want.Value)) continue;     // DZ-072a：同型（异型卸入会被 IWarehouse 拒）
            var pb = s.GetComponentInParent<Building>();
            if (pb != null ? pb.kingdomId != kingdom : kingdom != 0) continue;   // DZ-073：同国（防跨国远目的地；无主仓不收）
            float d = GridMath.DistCells(s.transform.position, task.SourcePos);
            if (d < bestDist) { bestDist = d; best = s; }
        }
        if (best != null) return best.transform.position;
        return ResolveTreasury(task);
    }

    // ⭐ `M1-F` 件5：原 `ResolveWaterSource`（= 最近 Active 水井 · ⛔ **不过滤国别**）**整段已删** ——
    //   源在广告时已定（`Building.FindNearestSameKingdomWellWithWater` · **带国别过滤** · `D807` §二-1），
    //   `destPos` 由广告侧直接写入（终点＝农场）⇒ 运行期不再解析水源。

    // ===== 辅助 =====

    /// <summary>
    /// ⭐ `HH.316`：**箱容器**（`ChestEntity` 本体的 `StorageComponent` · 件1「箱＝真仓」）＝
    /// **可搬出但⛔ 不作卸货落点／⛔ 不作通用取货源** —— 与「⛔ 不 `WarehouseRegistry.Register`」同精神
    /// （防成为他人卸货落点 · `D798` 裁 A-1 红线②）。
    /// 用途：三处**遗留全扫**（`ResolveWarehouse`／`LoadAmmoToBackpack`／`DepositAmmoBack` · R1 同族）
    /// 在箱容器入场后会把它当普通仓 ⇒ 会扰动 U-2 **范围外**链路（建筑搬运目的地／装填取货／退弹）
    /// ⇒ 按本判据过滤，保「箱只进搬运链、不进通用仓库面」。
    /// </summary>
    private static bool IsChestStore(StorageComponent s)
        => s != null && s.GetComponent<ChestEntity>() != null;

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

    // ===== 资源偏向活权重（2_23 资源 P0 批B/R-B1，D529/D634）=====

    /// <summary>
    /// 排序比较器：有效优先级降序（死表 × 活权重）→ 确定性次级键（源坐标 y→x→任务类型）。
    /// 严格全序 ⇒ 同 seed 同诊断信号 → 同排序（确定性红线；比 List.Sort 不稳+HSet 枚举序可靠）。
    /// </summary>
    private int CompareByEffectivePriority(KingdomTask a, KingdomTask b)
    {
        float pa = EffectivePriority(a);
        float pb = EffectivePriority(b);
        if (pa > pb) return -1;   // 高优先先派
        if (pa < pb) return 1;

        // 次级键：全序（与注册序/收集序无关；⑤-3 同款纪律）
        Vector2 sa = a.SourcePos, sb = b.SourcePos;
        if (sa.y != sb.y) return sa.y.CompareTo(sb.y);
        if (sa.x != sb.x) return sa.x.CompareTo(sb.x);
        return ((int)a.type).CompareTo((int)b.type);
    }

    /// <summary>
    /// 有效优先级 = 死表值 × 活权重（clamp bias×(1+k×缺口率), min, max）。
    /// 保命硬红线（D634）：S 级不乘权重（原值）；非 S 结果硬上界 = S 值 − ε（防配置越界架空死表）。
    /// 玩家源（无 AI 快照）/无映射/缺参 → 权重 1.0 ⇒ 有效优先级 == 死表值（出厂零差异）。
    /// 纯函数：只读 SO/快照，无随机无时间。
    /// </summary>
    private float EffectivePriority(KingdomTask task)
    {
        float baseP = (float)GetPriority(task.type);

        // S 级修复保命：不参与偏向（红线）
        if (baseP >= (float)TaskPriority.S) return baseP;

        float w = ResolveBiasWeight(task, out _);
        float eff = baseP * w;

        // 非 S 硬上界 < S（防 maxWeight 配置过大导致低档架空死表 S）
        float cap = (float)TaskPriority.S - 0.001f;
        return eff > cap ? cap : eff;
    }

    /// <summary>
    /// 计算任务活权重（纯函数）。缺口信号（D634①）＝批A 经济诊断块 Flow.Net(r) 负值；
    /// 缺口率（补-1）＝clamp01(max(0,−Net(r)) / max(1, Out(r)))；公式（D634② 线性）＝
    /// clamp(bias[r]×(1+k×缺口率), minWeight, maxWeight)。
    /// </summary>
    private float ResolveBiasWeight(KingdomTask task, out int resIdx)
    {
        resIdx = -1;
        if (task == null) return 1f;

        int kingdom = SourceKingdom(task);
        if (kingdom <= 0) return 1f;   // 玩家源：无 AI 快照 ⇒ 零改动
        if (_biasConfig == null) return 1f;

        int r = ResolveTaskBiasResource(task);
        if (r < 0) return 1f;          // 无映射（如建造/搬运等无资源语义）→ 不参与偏向
        resIdx = r;

        float shortage = ShortageRate(kingdom, (EcoResource)r);
        if (shortage <= 0f) return 1f; // 无缺口 → 出厂等价

        float raw = _biasConfig.BiasOf((EcoResource)r) * (1f + _biasConfig.k * shortage);
        float lo = _biasConfig.minWeight, hi = _biasConfig.maxWeight;
        if (hi < lo) hi = lo;
        return raw < lo ? lo : (raw > hi ? hi : raw);
    }

    /// <summary>
    /// 缺口率(r)＝clamp01( max(0, −Net(r)) / max(1, Out(r)) )（补-1，HH.172 §五）。
    /// 数据源＝批A EconomyBlock（SituationHub；无快照/无经济块 → 0=无缺口）。
    /// </summary>
    private static float ShortageRate(int kingdomId, EcoResource r)
    {
        if (!SituationHub.TryGet(kingdomId, out var snap) || snap == null || snap.Economy == null) return 0f;
        var flow = snap.Economy.Flow;
        int net = flow.Net(r);
        if (net >= 0) return 0f;
        int outAmt = flow.Out(r);
        float rate = (-net) / (float)Mathf.Max(1, outAmt);
        return Mathf.Clamp01(rate);
    }

    /// <summary>
    /// 任务→资源解析（D634③：SO 可配表，禁硬编码映射）。分两类：
    ///   ① **采集/生产活**（Gather/Production）＝§2.2 通道B 的「对应资源采集活」——先读 args 实参
    ///      （最精确），缺参再读源建筑产出（须 producer.kind==Resource，防 outputResource 默认值 0
    ///      把无产出建筑误判为产金；口径对齐批A EconomyDiagnosis.MapProduceToEco）。
    ///   ② 其余任务类型＝读 ResourceBiasConfig 兜底表（如 GoldMine→金、Rancher→粮；
    ///      水/弹药等非五元在表中 enabled=false）。
    /// -1 ＝ 不参与偏向（有效优先级退化为死表值＝出厂等价）。
    /// 注：搬运（Transport）**不参与**偏向——§2.2 通道B 语义为「采集活权重↑」，搬运属独立工作类
    /// （且已为 B 档）；是否扩面归策划端（HH.173 §列报）。
    /// </summary>
    private int ResolveTaskBiasResource(KingdomTask task)
    {
        switch (task.type)
        {
            case KingdomTaskType.Gather:
            case KingdomTaskType.Production:
                if (task.args is GatherTaskArgs ga) return MapResourceType(ga.resourceType);
                if (task.source is Building b && b.def != null
                    && b.def.producer.kind == ProduceKind.Resource)
                {
                    if (b.def.isBlacksmith) return (int)EcoResource.Metal;   // 铁匠铺 矿石→Metal（D200/D609）
                    return MapResourceType(b.def.outputResource);
                }
                return -1;

            default:
                return _biasConfig != null ? _biasConfig.ResourceOfTaskType(task.type) : -1;
        }
    }

    /// <summary>ResourceType → 五元 EcoResource（非五元如 Ore/Crystal/Meat/弹药 → -1=不参与偏向）。</summary>
    private static int MapResourceType(ResourceType t)
    {
        switch (t)
        {
            case ResourceType.Gold: return (int)EcoResource.Gold;
            case ResourceType.Stone: return (int)EcoResource.Stone;
            case ResourceType.Wood: return (int)EcoResource.Wood;
            case ResourceType.Food: return (int)EcoResource.Food;
            case ResourceType.Metal: return (int)EcoResource.Metal;
            default: return -1;
        }
    }

    /// <summary>2_17 步骤3 池隔离：任务源归属国（非 Building 源未单列者归玩家 kingdomId=0；
    /// `WorldGatherSource`／`Component` 二者各有专属分支见下，其余（无主源 -1 自然建筑）在路由时降级为先到先得池，任何国可匹配）。
    /// DZ-072a：矿洞副产组件任务源（MineByproductComponent 挂 Building 本体）按父建筑归属国路由——
    /// AI 领土内 mine 副产任务入 AI 池（旧逻辑非 Building 恒归 0=错入玩家池）。
    /// HH.221/D685 A①：世界资源点采集源（`WorldGatherSource`，非 Component）按**per-命令绑定王国**路由——
    /// 否则非 Building 源恒落 0=玩家池 ⇒ AI 工人永不匹配（通道名义落地实则僵死）。
    /// ⭐ HH.316 件4（D798 裁 ③）：**箱源**（`ChestEntity`）⇒ **-1 无主池**（`09` §9.8 :391「任何王国的
    /// 工人都能搬（无主 ⇒ **先到先得**）」· 玩法后果＝AI 会来抢你的战利品）—— 卸货侧维持现码
    /// （`UnloadInventory` 按 `uc.kingdomId` ⇒ **谁搬回归谁国**）⇒ 与 §9.8 自洽。
    /// 非破坏性增支：只多认新类型，既有分支与 return 语义逐位不动（玩家侧零影响）。</summary>
    private int SourceKingdom(KingdomTask task)
    {
        if (task == null) return 0;
        if (task.source is ChestEntity) return -1;   // ⭐ HH.316 件4：箱＝无主源（先到先得池 · 派工循环对 -1 已支持）
        if (task.source is Building b) return b.kingdomId;
        if (task.source is WorldGatherSource wg) return wg.KingdomId;   // HH.221 A①：世界资源点采集源
        if (task.source is Component c)
        {
            var pb = c.GetComponentInParent<Building>();
            if (pb != null) return pb.kingdomId;
        }
        return 0;
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

    /// <summary>任务 Working 时长（QQQ.2 T19/DR-11：Gather 按源侧 `GatherTaskArgs.gatherSeconds`，其余统一 workDuration）。
    /// 【HH.294 片 6-2 收尾】原 `if (task.source is Building …) secs = b.def.gatherSeconds;` **死分支已删** ——
    /// 实体资源点退役 ⇒ Gather 源只剩 `WorldGatherSource`（耗时由源侧按 feature 逐型填入·B-1 逐型原值）。
    /// ⭐ `U-8` 件4（`D800` `Q4`）：**拆除任务工时 ＝ 源侧 `Building.DemolishDuration()`**（同 Gather 形制）。
    ///   **活读**（⛔ 非快照）：`DemolishDuration()` 依赖 `CountAssignedWorkers` ⇒ 每帧可变；且拆除需多轮派工
    ///   ⇒ 每轮都取最新值才与「建筑自推」（`Building.cs:594-606`）一致；`≤0` ⇒ 兜底 `workDuration`（照上分支形制）。
    /// ⚠️ 慢通道副产物（预期行为 · 判据须给读数）：改后多数情况下拆除由 `FinishDemolish` **先收口**
    ///   （进度门控含派工期 ⇒ 比 Working 早 ≈1 tick）⇒ 该任务被 `OnBuildingDied` **放弃**（⛔ 非「完成」）——
    ///   与「建筑自推」口径一致。</summary>
    private float GetTaskDuration(KingdomTask task)
    {
        if (task != null && task.type == KingdomTaskType.Gather && task.args is GatherTaskArgs ga)
        {
            float secs = ga.gatherSeconds;
            return secs > 0f ? secs : workDuration;
        }
        // ⭐ `U-8` 件4：拆除工时活读源侧（`target` ＝ 拆除中的 `Building` 本体）
        if (task != null && task.args is DemolishTaskArgs da && da.target != null)
        {
            float ds = da.target.DemolishDuration();
            return ds > 0f ? ds : workDuration;
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
    /// <summary>采集耗时（秒，数据驱动）。
    /// 【HH.294 片 6-2·6-B 勘正】改前此值由源侧填：实体源＝`BuildingDef.gatherSeconds`（`GetTaskDuration` 处另有
    /// 以 def 为准的覆盖）／树源＝`RespawnConfig.treeGatherSeconds`；**改后一律**由源侧按 feature 填
    /// （`RespawnConfig.GatherSecondsOf`·逐型保原值），调度器**不再读 def**。</summary>
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