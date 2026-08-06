using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 调试信息接口（3.0.1 附录 A）。
/// NPCBrain 实现此接口，供调试面板实时读取 AI 内部状态。
/// 首版只做个体深查，群体概览留接口。
/// </summary>
public interface IAIDebugInfo
{
    Focus CurrentFocus { get; }
    BehaviorSpectrum CurrentSpectrum { get; }
    ThreatLevel CurrentThreatLevel { get; }
    int NearbyEnemyCount { get; }
    int NearbyAllyCount { get; }
    bool HasProtection { get; }
    bool InSafetyConfirmation { get; }
    bool IsInHitCooldown { get; }
}

/// <summary>
/// AI 调试信息扩展接口（3.0.1_2）。
/// 提供 UI 面板需要的额外数据：位置、血量、切换历史、刺激源排行榜。
/// AIDebugController 检测此接口，有则收集扩展数据。
/// </summary>
public interface IAIDebugInfoExtended : IAIDebugInfo
{
    Vector2 DebugPosition { get; }
    float DebugHPRatio { get; }
    void GetSwitchHistory(List<AISwitchRecord> output, int maxCount);
    void GetTopStimuli(List<StimulusDebugInfo> output, int maxCount);
}

/// <summary>
/// 通用 NPC 大脑（3.0.1_2 三层裁决管线 + 记忆组件群 + 反馈控制环）。
///
/// 架构（§1）：
///   两类东西：纯计算管线(L1/L2/L3 无状态) + 输入侧记忆组件群(有状态需存档)
///   反馈控制环：Executor事件 -> NPCBrain本地分发 -> EventBus -> 记忆组件 -> ctx -> 管线 -> cmd
///
/// tick 管线（先记忆后管线，foreach 钉死秩序）：
///   ⓪ ctx 组装(世界/自身原始状态) -> ① 记忆组件Tick -> ② FillContext+收集刺激源入池
///   -> ③ L1->L2->L3 纯管线 -> ④ 攻击链路(并行) -> ⑤ Executor执行 -> ⑥ 切换历史+缓存
///
/// 替换旧扁平架构：AttentionSystem->ThreatAssessor->TradeoffSystem->直接方法调用。
/// TradeoffSystem 退役，ThreatAssessor 仅保留 CalculateRawFactor。
/// </summary>
[RequireComponent(typeof(UnitController))]
public class NPCBrain : MonoBehaviour, IAIDebugInfoExtended, IExecutorEventReceiver, IAIDebugInfoV3
{
    // ===== 依赖 =====
    private UnitController _controller;
    private IDamageable _self;
    private NpcProfessionDef _profession;
    private AttentionTuningConfig _config;
    private IHomePointProvider _homePointProvider;

    // ===== AI 子系统（纯计算管线复用）=====
    private readonly AttentionSystem _attention = new AttentionSystem();

    // ===== 记忆组件群（§1.1 原则一，有状态需存档）=====
    private IMemoryComponent[] _memoryComponents;
    private ThreatHysteresisComponent _threatHysteresis;
    private ProtectionHysteresisComponent _protectionHysteresis;
    private HitCooldownStateMachine _hitCooldown;

    // ===== 刺激源 Provider（池化复用）=====
    private readonly SafetyStimulusProvider _safetyProvider = new SafetyStimulusProvider();
    private readonly FollowStimulusProvider _followProvider = new FollowStimulusProvider();
    // 3.0.1_4 §6.3 漫游
    private readonly WanderStimulusProvider _wanderProvider = new WanderStimulusProvider();

    // ===== 3.0.1_4 §2.3 受击溯源（聚合 O(1)，不追踪攻击者列表）=====
    private IDamageable _lastAggressor;   // 最近攻击者（1 个，谁打最狠/最近）
    private int _recentHitCount;          // 受击次数（聚合计数）
    private float _lastHitTime;           // 最近受击时间

    // ===== BehaviorExecutor（§13.4）=====
    private BehaviorExecutor _executor;

    // ===== 定时器 =====
    private float _thinkTimer;
    private float _perceptionTimer = 999f;  // 首帧立即触发感知
    /// <summary>活跃区思考频率 10Hz（3.0.1_LOD：半活跃 2Hz / 休眠 0.5Hz 由 LODSystem 覆盖）</summary>
    private const float ThinkInterval = 0.1f;
    /// <summary>当前 LOD 等级的思考间隔（秒，由 LODSystem 每帧刷新）</summary>
    private float _currentThinkInterval = ThinkInterval;
    /// <summary>当前 LOD 等级的感知间隔（秒，= max(感知基础, 思考间隔) 保证输入新鲜）</summary>
    private float _currentPerceptionInterval;
    private float _lastThinkTime;
    /// <summary>LODSystem 引用（所在 region 读思考频率，Init 时查找）</summary>
    private LODSystem _lodSystem;

    // ===== tick 分片平摊（决策12，500 NPC 分 5 帧）=====
    private static int s_globalTickFrame;
    private static int s_registeredCount;
    private int _shardIndex;

    // ===== 运行时状态 =====
    private IDamageable _currentAttackTarget;
    private readonly List<IDamageable> _nearbyEnemies = new List<IDamageable>();
    private readonly List<IDamageable> _nearbyAllies = new List<IDamageable>();
    private float _nearestDist = float.MaxValue;

    // ===== 3.0.1_8 §六 放弃任务因子：追击状态跟踪 =====
    private IUnitHandle _chaseTarget;   // 当前追击目标（焦点威胁源，切换时重置计时；M1 决策核提取改 IUnitHandle）
    private float _chaseStartTime;      // 追击开始时间戳（超时成本用）
    private float _lastChaseDist;       // 上帧追击距离（距离拉大成本用）

    // ===== 管线中间产物缓存 =====
    private float _lastRaw;           // 上一帧 rawFactor（量化器消费）
    private FactorContext _lastCtx;   // 上一帧完整 ctx（调试面板读）
    private BehaviorCommand _lastCmd; // 上一帧 Think 产出的 cmd（Execute 每帧复用）

    // ===== B2 治疗（对齐 sim SimBrain._healTimer）=====
    private float _healTimer;

    /// <summary>
    /// 是否空闲可派任务（3.3.5 资源流转调度中心用）。
    /// 焦点无效 / 焦点是 Wander（漫游）/ Follow（跟随非任务）→ 空闲可派；
    /// 焦点是 TaskStimulus（正在工作）/ ThreatStimulus（战斗中）→ 忙，不派新任务（防任务堆叠）。
    /// </summary>
    public bool IsIdleForTask
    {
        get
        {
            if (!_lastCtx.FocusDecision.IsValid) return true;
            var focus = _lastCtx.FocusDecision.Focus;
            if (focus is TaskStimulus) return false;    // 正在执行任务
            if (focus is ThreatStimulus) return false;  // 战斗中
            return true;                                // Wander/Follow/Safety 均可打断派任务
        }
    }

    // ===== 切换历史（3.0.1_2 AI 调试用）=====
    private readonly AISwitchRecord[] _switchHistory = new AISwitchRecord[10];
    private int _switchHistoryHead;
    private int _switchHistoryCount;
    private Focus _lastRecordedFocus;
    private BehaviorSpectrum _lastRecordedSpectrum;

    // ===== IAIDebugInfo 实现（映射到 _lastCtx 产物）=====
    public Focus CurrentFocus => _attention.CurrentFocus;
    public BehaviorSpectrum CurrentSpectrum => _lastCtx.PostureDecision.Spectrum;
    public ThreatLevel CurrentThreatLevel => _lastCtx.ThreatLevel;
    public int NearbyEnemyCount => _lastCtx.NearbyEnemyCount;
    public int NearbyAllyCount => _lastCtx.NearbyAllyCount;
    public bool HasProtection => _lastCtx.HasProtection;
    public bool InSafetyConfirmation => _lastCtx.IsCaution;  // 旧 TradeoffSystem 概念映射
    public bool IsInHitCooldown => _lastCtx.CurrentState != HitCooldownState.Normal;

    // ===== IAIDebugInfoExtended 实现 =====
    public Vector2 DebugPosition => _controller != null ? (Vector2)_controller.transform.position : Vector2.zero;
    public float DebugHPRatio => _self != null ? (float)_self.CurrentHp / Mathf.Max(1, _self.MaxHp) : 0f;

    public void GetSwitchHistory(List<AISwitchRecord> output, int maxCount)
    {
        output.Clear();
        int count = Mathf.Min(maxCount, _switchHistoryCount);
        for (int i = 0; i < count; i++)
        {
            int idx = (_switchHistoryHead - count + i + _switchHistory.Length) % _switchHistory.Length;
            output.Add(_switchHistory[idx]);
        }
        output.Reverse();
    }

    public void GetTopStimuli(List<StimulusDebugInfo> output, int maxCount)
    {
        output.Clear();
        if (_attention != null) _attention.GetTopStimuliForDebug(output, maxCount);
    }

    // ===== IAIDebugInfoV3 实现（3.0.1_2 三层中间结果 + 记忆组件状态）=====
    public FocusDecision DebugFocusDecision => _lastCtx.FocusDecision;
    public PostureDecision DebugPostureDecision => _lastCtx.PostureDecision;
    public BehaviorCommand DebugCommand => L3CommandComputer.Compute(in _lastCtx.PostureDecision, in _lastCtx);
    public HitCooldownState DebugHitCooldownState => _lastCtx.CurrentState;
    public int DebugHitCount => _lastCtx.HitCount;
    public float DebugLastRaw => _lastRaw;
    public float DebugSafetyUrge => _safetyProvider.Stimulus.Intensity;
    public Vector2 DebugHomePoint => Vector2XUnity.ToUnity(_lastCtx.HomePoint);

    // ===== 初始化 =====
    public void Init(NpcProfessionDef profession)
    {
        _profession = profession;
        _controller = GetComponent<UnitController>();
        _self = GetComponent<IDamageable>();
        _config = Resources.Load<AttentionTuningConfig>("Config/AttentionTuningConfig");
        if (_config == null)
            Debug.LogError("[NPCBrain] 未找到 AttentionTuningConfig！请创建 Resources/Config/AttentionTuningConfig.asset");

        // D3 清理轮：hv*/惜用阈值注入 UnitController（静态塔无 NPCBrain 用默认值，与 champion 默认一致）
        if (_controller != null)
        {
            _controller.HvKillHpGate = _config.hvKillHpGate;
            _controller.HvDefenseGate = _config.hvDefenseGate;
            _controller.HvCrowdGate = _config.hvCrowdGate;
            _controller.AmmoConserveRatio = _config.ammoConserveRatio;
        }

        // 初始化记忆组件群（M1 决策核提取：组件吃 TuningSnapshot 快照，接缝 4）
        _threatHysteresis = new ThreatHysteresisComponent(_config.ToSnapshot());
        _protectionHysteresis = new ProtectionHysteresisComponent(_config.ToSnapshot());
        _hitCooldown = new HitCooldownStateMachine();
        _memoryComponents = new IMemoryComponent[] { _threatHysteresis, _protectionHysteresis, _hitCooldown };

        // 3.0.1_4：注入全局调参快照（破阵博弈/漫游用）+ 世界查询（接缝 3：GridSystem 单例 -> 注入）
        _attention.SetConfig(_config.ToSnapshot());
        _attention.SetWorldQuery(new UnityWorldQueryAdapter());

        // 初始化 BehaviorExecutor
        _executor = new BehaviorExecutor(_controller, _self, this, _config);

        // tick 分片：分配 shardIndex
        _shardIndex = s_registeredCount++ % Mathf.Max(1, _config.thinkShardCount);

        // 3.0.1_LOD：引用 LODSystem（读所在 region 思考频率；Singleton 隐式创建，未初始化 region 时全活跃行为不变）
        _lodSystem = LODSystem.Instance;
        _currentPerceptionInterval = _config != null ? _config.perceptionUpdateInterval : 0.2f;

        // HomePointProvider 查找（场景内挂 SceneHomePointProvider）
        if (_homePointProvider == null)
        {
            var provider = FindObjectOfType<SceneHomePointProvider>();
            if (provider != null) _homePointProvider = provider;
        }
    }

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDamagedEvent>(OnDamaged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDamagedEvent>(OnDamaged);
    }

    /// <summary>自身受击事件 -> HitCooldownStateMachine.OnDamaged（事件驱动路径b）+ 3.0.1_4 受击溯源聚合更新</summary>
    private void OnDamaged(UnitDamagedEvent evt)
    {
        if (!ReferenceEquals(evt.Unit, _self)) return;
        // 用临时 ctx 触发状态机（受击时刻即转 Caution + hitCount++）
        var ctx = BuildBaseContext();
        _hitCooldown.OnDamaged(in ctx);

        // 3.0.1_4 §2.3 受击溯源（聚合 O(1)）：
        // 敌对攻击者 -> 记录最近攻击者 + 计数 + 时间；环境伤害/误伤 -> 只计数不记攻击者
        if (evt.Source == null) return;
        if (evt.Source.GetFaction() == Faction.None || evt.Source.GetFaction() == _self.GetFaction())
            return;  // 环境伤害/友军误伤不溯源
        _lastAggressor = evt.Source;
        _recentHitCount++;
        _lastHitTime = Time.time;
    }

    // ===== IExecutorEventReceiver 实现（§13.4 双层分发：本地主 + EventBus 辅）=====

    public void OnArrived(Vector2 position, BehaviorModule fromModule)
    {
        // 本地处理（主，可靠不丢）
        // 到达焦点目标 -> 下 tick L2 三维表走"已到达"分支
        // EventBus 辅转发（供调度/调试/音效）
        EventBus.Publish(new ExecutorArrivedEvent(this, position, fromModule));
    }

    public void OnMoveComplete(Vector2 position)
    {
        // 本地处理（主）：撤退完成 -> HitCooldownStateMachine Caution 计时起点（§13.3 关键）
        _hitCooldown.OnMoveComplete();
        EventBus.Publish(new ExecutorMoveCompleteEvent(this, position));
    }

    public void OnAnchorLost()
    {
        // 本地处理（主）：跟随锚点死亡 -> 清除 FollowStimulus
        _followProvider.ClearAnchor();
        EventBus.Publish(new ExecutorAnchorLostEvent(this));
    }

    // ===== Update（感知 + Think，含 tick 分片）=====

    private void Update()
    {
        if (_self == null || _profession == null || _config == null) return;
        if (_self.CurrentHp <= 0) return;

        // 3.0.1_LOD：每帧按所在 region 刷新思考/感知间隔（读 LODSystem；未挂载则活跃区频率）
        RefreshLodIntervals();

        // 感知更新（LOD 动态频率：活跃 0.2s / 半活跃/休眠按思考间隔保证输入新鲜）
        _perceptionTimer += Time.deltaTime;
        if (_perceptionTimer >= _currentPerceptionInterval)
        {
            _perceptionTimer = 0f;
            UpdatePerception();
        }

        // 思考更新（LOD 动态频率，走分片：决策12，500 NPC 分 5 帧平摊）
        // 分片只对 Think（决策）分片，Execute 每帧调用（移动需持续）
        _thinkTimer += Time.deltaTime;
        if (_thinkTimer >= _currentThinkInterval)
        {
            _thinkTimer = 0f;
            s_globalTickFrame++;
            // 仅当前 shardIndex 对应的帧执行 Think（产出新 cmd）
            if (s_globalTickFrame % Mathf.Max(1, _config.thinkShardCount) == _shardIndex)
            {
                Think();
            }
        }

        // Execute 每帧调用（用最近一次 Think 产出的 cmd，持续移动）
        if (_executor != null)
        {
            _executor.Execute(in _lastCmd, Time.deltaTime, GetCellSize());
        }

        // 骑兵冲锋状态机（3.6 §5.3 三态：准备→突进→撞击→第二击；每帧推进）
        TickCharge();
    }

    /// <summary>
    /// 3.0.1_LOD：按所在 region 的 LOD 等级刷新思考/感知间隔。
    /// 活跃 10Hz / 半活跃 2Hz / 休眠 0.5Hz（AttentionTuningConfig 可调）。
    /// 感知间隔 = max(感知基础, min(思考间隔, 0.5s))——降频时感知保底 0.5s，
    /// 防止休眠区 2s 才发现敌人（反应迟钝）。半活跃/休眠区输入收集随思考降频，
    /// 但感知下限保证"敌人出现能及时看见"。
    /// </summary>
    private void RefreshLodIntervals()
    {
        if (_lodSystem == null) return;
        float thinkInterval;
        switch (_lodSystem.GetLevelAt(_self.GetPosition()))
        {
            case LodLevel.SemiActive:
                thinkInterval = 1f / Mathf.Max(0.1f, _config.lodSemiThinkHz);
                break;
            case LodLevel.Sleeping:
                thinkInterval = 1f / Mathf.Max(0.1f, _config.lodSleepThinkHz);
                break;
            default:
                thinkInterval = ThinkInterval;
                break;
        }
        _currentThinkInterval = thinkInterval;
        // 感知保底 0.5s（不超过思考间隔，但休眠区不至于 2s 才发现敌人）
        _currentPerceptionInterval = Mathf.Max(_config.perceptionUpdateInterval,
            Mathf.Min(thinkInterval, 0.5f));
    }

    // ===== 感知 =====

    /// <summary>查询附近敌人/友军，更新注意力系统的威胁刺激源。</summary>
    private void UpdatePerception()
    {
        float perceptionWorld = _profession.perceptionRadius * GetCellSize();
        Vector2 myPos = _self.GetPosition();
        Faction myFaction = _self.GetFaction();

        PerceptionSystem.QueryNearby(myPos, perceptionWorld, myFaction, true, _nearbyEnemies);
        PerceptionSystem.QueryNearby(myPos, perceptionWorld, myFaction, false, _nearbyAllies);

        _attention.ClearThreats();
        float currentTime = Time.time;
        _nearestDist = float.MaxValue;

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            var enemy = _nearbyEnemies[i];
            if (enemy == null || IsDestroyed(enemy) || enemy.CurrentHp <= 0) continue;
            // M1 决策核提取：ThreatStimulus.Enemy 改 IUnitHandle（接缝 1），单位双接口转换
            var enemyHandle = enemy as IUnitHandle;
            if (enemyHandle == null) continue;  // 非单位 IDamageable（感知查询仅返回单位，防御性跳过）

            float dist = Vector2.Distance(myPos, enemy.GetPosition());
            if (dist < _nearestDist) _nearestDist = dist;
            // 刺激强度标定 0-100（贴脸满强度，量纲上限 config 可调；3.0.1_4 §3.4 威胁分层依赖此量纲）
            float intensity = Mathf.Max(1f, _config.threatIntensityMax * (1f - dist / perceptionWorld));

            var stimulus = new ThreatStimulus(
                enemyHandle,
                threatLevel: (int)_threatHysteresis.CurrentLevel,
                intensity: intensity,
                expiry: currentTime + _config.threatDecayTime
            );
            _attention.AddStimulus(stimulus);
        }

        // 3.0.1_4 §2.3 受击溯源（聚合 O(1)）：感知范围外的攻击者也能被溯源到
        // 强度 = min(上限, 基础40 + (次数-1)×10) × 指数衰减(3s)；受击保底威胁 1（警戒）
        // 溯源上限 60 < 贴脸近战下限 90 -> 威胁分层近战>远程（§3.4）
        if (_lastAggressor != null && !IsDestroyed(_lastAggressor) && _lastAggressor.CurrentHp > 0)
        {
            float delta = Mathf.Max(0f, currentTime - _lastHitTime);
            float baseIntensity = Mathf.Min(_config.traceMaxIntensity,
                _config.traceBaseIntensity + (_recentHitCount - 1) * _config.traceStepIntensity);
            float intensity = Mathf.Max(1f, baseIntensity * Mathf.Exp(-delta / _config.traceDecayTime));

            var trace = new ThreatStimulus(
                _lastAggressor as IUnitHandle,
                threatLevel: Mathf.Max((int)_threatHysteresis.CurrentLevel, 1),  // 受击至少警戒
                intensity: intensity,
                expiry: currentTime + _config.traceExpiry
            );
            _attention.AddStimulus(trace);
        }

        // 3.0.1_LOD §3.1 第二层：感知范围外但区块有战斗热点 -> 朝热点移动支援（危险传开的位置载体）
        // 用 TaskStimulus（第 3 层，与 Safety 同层竞争）：引导支援移动、不推高威胁评定（避免误触发撤退谱系）
        // 强度 0.6 > Safety 未到达 ~0.5（朝热点走优先于回城），< Follow 军令 S 级 4.5（编队士兵守阵不乱跑）
        if (_nearbyEnemies.Count == 0 && _lodSystem != null
            && _lodSystem.TryGetCombatHotspot(myPos, _config.traceDecayTime, out var hotspot))
        {
            float dist = Vector2.Distance(myPos, hotspot);
            if (dist > perceptionWorld)  // 热点在感知范围外才需要"传递"；范围内感知已覆盖
            {
                _attention.AddStimulus(new TaskStimulus(
                    TaskPriority.C,                     // 杂务级（支援）
                    targetPos: Vector2XUnity.FromUnity(hotspot),  // 热点即目标位置
                    intensity: _config.hotspotSupportIntensity,  // > Safety 0.5，< Follow S 级 4.5
                    expiry: currentTime + _config.traceDecayTime,
                    issuer: _lodSystem                  // 区块警报来源
                ));
            }
        }

        if (_nearbyEnemies.Count == 0) _nearestDist = float.MaxValue;
    }

    // ===== Think（三层裁决管线，先记忆后管线）=====

    private void Think()
    {
        // 3.0.1_LOD §二：记忆组件 dt 改传真实墙钟差值（半活跃 2Hz / 休眠 0.5Hz 下确认时间不漂）
        float currentTime = Time.time;
        float dt = _lastThinkTime > 0f ? Mathf.Max(0f, currentTime - _lastThinkTime) : _currentThinkInterval;
        _lastThinkTime = currentTime;

        // ⓪ 组装 FactorContext（世界/自身原始状态）
        FactorContext ctx = BuildBaseContext();
        ctx.LastRaw = _lastRaw;
        ctx.ArrivedAtFocus = _executor.ArrivedAtFocus;

        // ① 记忆组件 Tick（量化器读 ctx.LastRaw 上一帧缓存）
        for (int i = 0; i < _memoryComponents.Length; i++)
            _memoryComponents[i].Tick(dt, in ctx);

        // ② FillContext（写入 FactorContext）+ 收集动态刺激源入 L1 池
        for (int i = 0; i < _memoryComponents.Length; i++)
            _memoryComponents[i].FillContext(ref ctx);

        // 收集动态刺激源入 L1 评分池（填充式遍历防 GC）
        _attention.ClearDynamicStimuli();
        _attention.AddStimulus(_safetyProvider.GetOrUpdate(in ctx));
        if (_followProvider.IsActive)
        {
            _attention.AddStimulus(_followProvider.Refresh(in ctx));
        }
        // 3.0.1_4 §6.3 漫游兜底（强度 0.05，Safety 到达后压 0 才浮出）
        _attention.AddStimulus(_wanderProvider.GetOrUpdate(in ctx));
        for (int i = 0; i < _memoryComponents.Length; i++)
        {
            var stimuli = _memoryComponents[i].GetActiveStimuli();
            for (int j = 0; j < stimuli.Count; j++)
                _attention.AddDynamicStimulus(stimuli[j]);
        }

        // 设置任务折扣（Caution 态对 TaskStimulus 打折）
        _attention.SetTaskDiscount(ctx.StateTaskDiscount);

        // 3.0.1_4 §4.3 破阵博弈：注入职业参数（courage 高敢脱队，obedience 高难脱队）
        if (_profession != null)
        {
            _attention.SetBreakContext(_profession.courage, _profession.obedience);
        }

        // ③ 纯管线：L1 -> L2 -> L3（无副作用）
        _attention.Update(currentTime, dt);
        // M1 决策核提取：L1 改为纯函数签名（输入=注意力系统当前产物），行为不变
        ctx.FocusDecision = L1FocusEvaluator.Evaluate(_attention.CurrentFocus, _attention.CurrentStimulus, in ctx);
        // 3.0.1_8 §六：放弃任务因子需 L1 焦点判追击状态，故在 L2 前组装
        ctx.AbandonTaskFactor = ComputeAbandonTaskFactor(in ctx);
        // 3.0.1_8 §八：工作因子需 L1 焦点判任务类型（TaskStimulus 按优先级归一化），L2 抗打断
        ctx.WorkFactor = ComputeWorkFactor(in ctx);
        ctx.PostureDecision = L2PostureDecider.Decide(in ctx);

        // rawFactor 计算（复用 CalculateRawFactor）+ stateThreatBias 处理
        ctx.RawFactor = ThreatAssessor.CalculateRawFactor(
            ctx.NearestEnemyDist, ctx.NearbyEnemyCount, ctx.HpRatio, ctx.NearbyAllyCount,
            ctx.IsNight, ctx.Profession, ctx.Config, ctx.PerceptionWorldRadius, ctx.AttackWorldRange,
            ctx.RegionHeat);
        // stateThreatBias 已在 ThreatHysteresisComponent.FillContext 内直接抬等级处理，
        // 此处不叠 rawFactor（避免与量化器滞回打架）
        _lastRaw = ctx.RawFactor;

        var cmd = L3CommandComputer.Compute(in ctx.PostureDecision, in ctx);

        // ④ 攻击链路保留（与 Executor 并行，不进 BehaviorExecutor）
        UpdateCombatRegistration(in ctx);

        // ④b 移速决策（3.6 §六，可训练）：追击中按 speedChaseBoost 提速（平常 walkSpeed / 追击最大 runSpeed）
        if (_chaseTarget != null && cmd.Module == BehaviorModule.MoveTowards)
        {
            cmd.Speed = _profession.walkSpeed
                + (_profession.runSpeed - _profession.walkSpeed) * _config.speedChaseBoost;
        }

        // ④c B4 角色族因子（对齐 sim ApplyProfessionFactors）：死拼/保命/顶住/压上
        ApplyProfessionFactors(ref cmd, in ctx);

        // ⑤ 缓存 cmd（Execute 在 Update 里每帧调用，持续移动）
        _lastCmd = cmd;

        // ⑥ 切换历史记录 + 缓存 _lastCtx（IAIDebugInfo 兼容）
        RecordSwitchHistory(currentTime);
        _lastCtx = ctx;
    }

    /// <summary>组装基础 FactorContext（世界/自身原始状态）</summary>
    private FactorContext BuildBaseContext()
    {
        float hpRatio = _self.MaxHp > 0 ? (float)_self.CurrentHp / _self.MaxHp : 0f;
        bool isNight = IsNight();
        Vector2 homePoint = _homePointProvider != null ? _homePointProvider.GetHomePoint(this) : Vector2.zero;
        // M1 决策核提取：核内吃快照（接缝 4），壳每 tick 从 SO 快照保证滑块实时性
        return new FactorContext
        {
            Self = _self as IUnitHandle,
            Profession = _profession != null ? _profession.ToSnapshot() : ProfessionSnapshot.Default,
            Config = _config != null ? _config.ToSnapshot() : default,
            SelfPos = Vector2XUnity.FromUnity(_self.GetPosition()),
            HpRatio = hpRatio,
            IsNight = isNight,
            NightFactor = GetNightFactor(),
            NearbyEnemyCount = _nearbyEnemies.Count,
            NearbyAllyCount = _nearbyAllies.Count,
            NearestEnemyDist = _nearestDist,
            PerceptionWorldRadius = _profession.perceptionRadius * GetCellSize(),
            AttackWorldRange = _profession.attackRange * GetCellSize(),
            CellSize = GetCellSize(),
            CurrentTime = Time.time,
            HomePoint = Vector2XUnity.FromUnity(homePoint),
            // 3.0.1_3：编队槽位（守阵追击 clamp 用，§4.1）
            HasFormationSlot = _followProvider.IsActive && _followProvider.Stimulus.IsFormationSlot,
            FormationSlotWorld = Vector2XUnity.FromUnity(ResolveFormationSlotWorld()),
            // 3.0.1_LOD §3.2：区块威胁热度（环境型威胁因子；LODSystem 未挂载=0 行为不变）
            RegionHeat = _lodSystem != null ? _lodSystem.GetHeatAt(_self.GetPosition()) : 0f,
            // 3.0.1_8 综合因子（L2 在 ③ 阶段读取，此处用上一帧 rawFactor = _lastRaw，与量化器消费时序一致）：
            //   ThreatFactor = 上一帧 rawFactor（连续 0-1，供 L2 连续仲裁）
            //   FormationFactor = 编队军令强度归一化（FollowStimulus.Intensity / 基准 4.5，有编队≈1 无编队=0；
            //     切阵型瞬时提强度（3.0.1_8 §七）会经 DispatchOrders 写入 FollowStimulus，此处理自动吃到提升值）
            //   SafetyFactor = 归巢因子（离家/夜晚/受伤加权，3.0.1_8 §五）
            ThreatFactor = _lastRaw,
            FormationFactor = _followProvider.IsActive
                ? Mathf.Clamp01(_followProvider.Stimulus.Intensity / _config.formationOrderIntensity)
                : 0f,
            SafetyFactor = ComputeSafetyFactor(hpRatio, homePoint),
            // D1 修复：保护力加权和（3.7 保护矩阵，ProtectionHysteresisComponent 消费；
            // 此前不填充恒 0 → HasProtection 恒 false，保护机制是死代码）
            ProtectPowerSum = SumNearbyProtectPower(),
        };
    }

    /// <summary>3.7 保护力加权和：身边友军 protectPower 之和（真保护判定输入，替代友军数；对齐 sim SumNearbyProtectPower）。</summary>
    private float SumNearbyProtectPower()
    {
        float sum = 0f;
        for (int i = 0; i < _nearbyAllies.Count; i++)
        {
            var a = _nearbyAllies[i] as IUnitHandle;
            if (a != null) sum += a.Profession.protectPower;
        }
        return sum;
    }

    /// <summary>
    /// 归巢因子（3.0.1_8 §五）：离家×w1 + 夜晚×w2 + 受伤×w3 加权合成 0-1。
    /// distFactor = clamp01(离家距离 / (2×感知半径))（2 倍感知半径外=1）。
    /// L2 消费：编队成员撤退需 SafetyFactor > safetyRetreatGate（AND 联合语义，军队承受更多代价）。
    /// </summary>
    private float ComputeSafetyFactor(float hpRatio, Vector2 homePoint)
    {
        float perceptionWorld = Mathf.Max(1f, _profession.perceptionRadius * GetCellSize());
        float distFactor = Mathf.Clamp01(Vector2.Distance(_self.GetPosition(), homePoint) / (perceptionWorld * 2f));
        float wound = 1f - hpRatio;
        return Mathf.Clamp01(
            distFactor * _config.safetyDistWeight
            + GetNightFactor() * _config.safetyNightWeight
            + wound * _config.safetyWoundWeight);
    }

    /// <summary>
    /// 工作因子（3.0.1_8 §八）：当前任务投入强度（0-1）。
    /// 焦点是 TaskStimulus（工作/建造任务）→ 按任务优先级归一化（S=1.0 / A=0.75 / B=0.5 / C=0.25）。
    /// 非任务焦点（威胁/编队/归巢/漫游）= 0（军令执行已由协作因子 FormationFactor 覆盖，不重复计）。
    /// L2 消费：有效威胁削减 = WorkFactor × workResistScale（正在干关键活更抗打断）。
    /// </summary>
    private float ComputeWorkFactor(in FactorContext ctx)
    {
        if (!ctx.FocusDecision.IsValid || !(ctx.FocusDecision.Focus is TaskStimulus ts))
            return 0f;
        return Mathf.Clamp01(_config.GetPriorityWeight(ts.Priority) / Mathf.Max(1f, _config.priorityWeightS));
    }

    /// <summary>
    /// 放弃任务因子（3.0.1_8 §六）：追击成本 vs 收益 仲裁（0-1，越高越想弃）。
    /// 收益（正分）：可击杀（目标残血）/ 军令要求（协作因子，编队要求追则不弃）
    /// 成本（负分）：受伤追击 / 孤军 / 追击超时 / 距离拉大（被风筝追不上）
    /// abandon = clamp01(cost - benefit)，L2 读 > abandonThreshold → Cautious（放弃追击回归编队）。
    /// 非追击中恒 0（不干扰守位/任务执行）。目标切换重置计时。
    /// </summary>
    private float ComputeAbandonTaskFactor(in FactorContext ctx)
    {
        // 仅战斗单位 + 焦点是威胁刺激（追击/交战中）才计算
        IUnitHandle target = null;
        if (_profession != null && _profession.attack > 0
            && ctx.FocusDecision.IsValid && ctx.FocusDecision.Focus is ThreatStimulus ts
            && ts.Enemy != null && ts.Enemy.IsAlive)  // IsAlive 含伪 null 检测（接缝 2）
        {
            target = ts.Enemy;
        }

        if (target == null)
        {
            _chaseTarget = null;
            _lastChaseDist = 0f;
            return 0f;
        }

        // 君主令（3.0.1_8 §6.6）：君主下令不顾一切 → 收益封顶，永不弃任务（覆盖一切成本）
        if (_followProvider.IsActive && _followProvider.Stimulus.IsRoyalCommand)
            return 0f;

        // 目标切换 → 重置追击计时（新的追击对象从零算）
        if (!ReferenceEquals(_chaseTarget, target))
        {
            _chaseTarget = target;
            _chaseStartTime = Time.time;
            _lastChaseDist = 0f;
        }

        float chaseTime = Time.time - _chaseStartTime;
        float distNow = Vector2X.Distance(ctx.SelfPos, target.Position);
        bool distGrow = _lastChaseDist > 0f && distNow > _lastChaseDist * _config.abandonDistGrowRatio;
        _lastChaseDist = distNow;

        float targetHpRatio = target.MaxHp > 0 ? (float)target.CurrentHp / target.MaxHp : 0f;

        // 收益（正分）：放弃 vs 坚持的天平（3.0.1_8 §6.6）
        float benefit = 0f;
        if (targetHpRatio < _config.abandonKillHpGate)
            benefit += _config.abandonBenefitKillable;
        benefit += ctx.FormationFactor * _config.abandonBenefitOrder;

        // 坚持任务因子：装备战力 / 敌情可击败 / 移速追得上（目标 Data 需 as UnitController 读——IDamageable 接口无 Data）
        UnitController targetUnit = target as UnitController;
        NpcProfessionDef targetDef = targetUnit != null ? targetUnit.Data as NpcProfessionDef : null;
        if (_profession.attack >= _config.persistPowerAttackGate)
            benefit += _config.persistBenefitPower;                       // 装备战力：我方攻高打得动
        if (targetDef != null
            && (_profession.attack - targetDef.defense) >= _config.persistDamageMargin)
            benefit += _config.persistBenefitWeakDefense;                 // 敌情：我方打得出有效伤害（相对比较）
        if (targetDef != null && _profession.walkSpeed > targetDef.walkSpeed * _config.persistSpeedRatio)
            benefit += _config.persistBenefitSpeed;                       // 移速：真追得上（>1.1×）才坚持

        // 成本（负分）
        float cost = 0f;
        if (ctx.HpRatio < _config.abandonWoundedHpGate)
            cost += _config.abandonCostWounded;
        if (ctx.NearbyAllyCount <= _config.abandonAloneGate)
            cost += _config.abandonCostAlone;
        if (chaseTime > _config.abandonTimeout)
            cost += _config.abandonCostTimeout;
        if (distGrow)
            cost += _config.abandonCostDistance;

        return Mathf.Clamp01(cost - benefit);
    }

    /// <summary>
    /// 解析当前编队槽位世界坐标（锚点位置 + SlotOffset × cellSize）。
    /// 非编队成员或锚点丢失返回 zero。
    /// </summary>
    private Vector2 ResolveFormationSlotWorld()
    {
        if (!_followProvider.IsActive) return Vector2.zero;
        var stim = _followProvider.Stimulus;
        if (!stim.IsFormationSlot || stim.Anchor == null) return Vector2.zero;
        float cs = GetCellSize();
        // M1 决策核提取：锚点位置经 IUnitHandle.Position（Vector2X）转回壳 Vector2
        return Vector2XUnity.ToUnity(stim.Anchor.Position)
            + new Vector2(stim.SlotOffset.x * cs, stim.SlotOffset.y * cs);
    }

    // ===== 攻击链路保留（搬自旧 ThreatFocusBehavior，不进 Executor）=====

    /// <summary>
    /// 攻击注册：威胁焦点 + 士兵 + 在范围内 -> DamageSystem.RegisterAttack。
    /// 受击冷却防追击的旧 allowChase:false 由 Caution 态 HoldPosition 胜出焦点取代。
    /// </summary>
    private void UpdateCombatRegistration(in FactorContext ctx)
    {
        // B2 治疗（对齐 sim SimBrain.ApplyHealingFactor）：Healer/Bishop 射程内有低血友军（<70%）时
        // 用 attack 值治疗并停火（不注册攻击）。职业名判断为最小方案，B4 角色族重构时替换。
        if (IsHealerRole() && TryHealAlly(ctx))
        {
            StopAttacking();
            return;
        }

        // 骑兵冲锋（3.6 §5.3 三态）：独立于射程攻击，扫感知列表触发（目标在 chargeRange 内即可）
        TryStartChargeFromPerception(ctx.CellSize);

        FocusDecision focus = ctx.FocusDecision;
        bool shouldAttack = false;
        IDamageable targetEnemy = null;

        if (_profession.attack > 0)  // 战斗单位才攻击
        {
            // 改动③「懵」：被骑兵撞飞期间（1.2s）禁攻击（对齐 sim UpdateCombatRegistration：IsKnockedBack -> StopAttacking + return）
            if (SelfUnit != null && SelfUnit.IsKnockedBack)
            {
                StopAttacking();
                return;
            }

            // 路径1：威胁焦点 + 在射程内（原逻辑）
            if (focus.IsValid && focus.Focus is ThreatStimulus ts
                && ts.Enemy != null && ts.Enemy.IsAlive && ts.Enemy.CurrentHp > 0)
            {
                float dist = Vector2X.Distance(ctx.SelfPos, ts.Enemy.Position);
                if (dist <= ctx.AttackWorldRange)
                {
                    shouldAttack = true;
                    targetEnemy = ts.Enemy as IDamageable;  // 单位双接口（UnitController），供 DamageSystem
                }
            }
            // 路径2：编队跟随焦点（FollowStimulus）或无威胁焦点时，感知范围内最近敌人在射程内也开火
            // 解决编队优先（威胁0/1级走FollowAnchor）时弓手站槽位看戏的问题
            // B4 目标选择（对齐 sim UpdateCombatRegistration 配方驱动）：点杀优先残血/脆皮，密度优先人群，否则最近。
            // 职业名驱动为最小方案（对齐 sim BuildDefaultProfiles），B4 角色族重构时替换。
            if (!shouldAttack && _nearbyEnemies.Count > 0)
            {
                bool isSniper = IsSniperRole();
                bool isDensity = IsDensityRole();
                IDamageable sniperTarget = null;
                float sniperBest = float.MaxValue;   // 脆皮优先级分：残血 > 低maxHp
                IDamageable densityTarget = null;
                int densityBest = -1;                // 邻域敌人数越多越优先
                IDamageable nearest = null;
                float nearestDist = float.MaxValue;
                for (int i = 0; i < _nearbyEnemies.Count; i++)
                {
                    var e = _nearbyEnemies[i];
                    if (e == null || IsDestroyed(e) || e.CurrentHp <= 0) continue;
                    float d = Vector2X.Distance(ctx.SelfPos, Vector2XUnity.FromUnity(e.GetPosition()));
                    if (d < nearestDist) { nearestDist = d; nearest = e; }
                    if (d > ctx.AttackWorldRange) continue;
                    if (isSniper)
                    {
                        float hpRatio = e.MaxHp > 0 ? (float)e.CurrentHp / e.MaxHp : 1f;
                        bool isSquishy = e.MaxHp < _profession.maxHp;   // 脆皮（低于自身 maxHp，对齐 SnipeSquishyByMaxHp）
                        bool isLowHp = hpRatio < 0.3f;                   // 残血（sim：弓手 0.3 / 弩手 0.5，最小方案统一 0.3）
                        if (isSquishy || isLowHp)
                        {
                            float score = isLowHp ? hpRatio : (1f + hpRatio);   // 残血权重最高（分数最低优先）
                            if (score < sniperBest) { sniperBest = score; sniperTarget = e; }
                        }
                    }
                    if (isDensity)
                    {
                        int crowd = CountNearbyEnemies(e, ctx.AttackWorldRange * 0.5f);   // 密度邻域（对齐 DensityRadiusCells≈2 格）
                        if (crowd > densityBest) { densityBest = crowd; densityTarget = e; }
                    }
                }
                if (isSniper && sniperTarget != null)
                {
                    shouldAttack = true;
                    targetEnemy = sniperTarget;
                }
                else if (isDensity && densityTarget != null && densityBest >= 1)
                {
                    // 邻域至少 1 个其他敌人（含自身 ≥2）才算密集人群，否则打最近
                    shouldAttack = true;
                    targetEnemy = densityTarget;
                }
                else if (nearest != null && nearestDist <= ctx.AttackWorldRange)
                {
                    shouldAttack = true;
                    targetEnemy = nearest;
                }
            }
        }

        // B1 弹药评估（对齐 sim SimBrain.SelectAmmo）：战争机器耗尽停火 / 惜用省弹 / 昂贵弹只对高价值目标。
        // 非战争机器（ammoMax=0）恒可发射（职业默认弹型）。ammoType 供 AttackProfile 使用。
        var selfUnit = SelfUnit;
        ProjectileType ammoType = _profession.ammo != null ? _profession.ammo.ammoType : ProjectileType.Arrow;
        if (shouldAttack && selfUnit != null && !selfUnit.SelectAmmo(targetEnemy, out ammoType))
        {
            StopAttacking();   // 弹药耗尽/惜用省弹 -> 停火等补给
            shouldAttack = false;
        }

        if (shouldAttack)
        {
            if (!ReferenceEquals(targetEnemy, _currentAttackTarget))
            {
                StopAttacking();
                var ammo = _profession.ammo;
                var profile = new AttackProfile
                {
                    attack = _profession.attack,
                    range = _profession.attackRange,
                    cd = _profession.attackCD,
                    isRanged = _profession.isRanged,
                    projectileSpeed = _profession.projectileSpeed,
                    // 弹药（3.6 §三：AmmoDef 拉平；B1 弹型 = SelectAmmo 选中值）
                    projectileType = ammoType,
                    pierceLevel = ammo != null ? ammo.pierceLevel : 1,
                    aoeRadiusCells = ammo != null ? ammo.aoeRadiusCells : 0f,
                    aoeFalloff = ammo != null ? ammo.aoeFalloff : 0f,
                    ballisticType = ammo != null ? ammo.ballisticType : BallisticType.Lob,
                    arcHeightCells = ammo != null ? ammo.arcHeightCells : 0f,
                    effectType = ammo != null && ammo.effect != null ? ammo.effect.type : GroundEffectType.None,
                    effectRadiusCells = ammo != null && ammo.effect != null ? ammo.effect.radiusCells : 0f,
                    effectDuration = ammo != null && ammo.effect != null ? ammo.effect.duration : 0f,
                    effectTickInterval = ammo != null && ammo.effect != null ? ammo.effect.tickInterval : 0f,
                    effectPower = ammo != null && ammo.effect != null ? ammo.effect.power : 0f,
                    effectMaxTargets = ammo != null && ammo.effect != null ? ammo.effect.maxTargets : 0,
                    // 韧性 + 骑兵冲锋（3.6 §5.3，SO 直读）
                    isCavalry = _profession.isCavalry,
                    chargeDamage = _profession.chargeDamage,
                    chargeRangeCells = _profession.chargeRangeCells,
                    chargePairGap = _profession.chargePairGap,
                    chargeGroupCooldown = _profession.chargeGroupCooldown,
                    chargeDamageReduce = _profession.chargeDamageReduce,
                };
                if (DamageSystem.Instance != null && DamageSystem.Instance.RegisterAttack(_self, targetEnemy, profile))
                {
                    _currentAttackTarget = targetEnemy;
                    if (selfUnit != null) selfUnit.ConsumeAmmo(ammoType);   // B1：发射扣弹（对齐 sim）
                }
            }
        }
        else
        {
            StopAttacking();
        }
    }

    private void StopAttacking()
    {
        if (_currentAttackTarget != null)
        {
            DamageSystem.Instance?.Unregister(_self);
            _currentAttackTarget = null;
        }
    }

    /// <summary>B2 是否治疗职业（B4 角色族：Support 族；资产 roleFamily 真身，构成驱动非职业驱动）。</summary>
    private bool IsHealerRole()
    {
        if (_profession == null || !_profession.isRanged || _profession.attack <= 0) return false;
        return _profession.roleFamily == RoleFamily.Support;
    }

    /// <summary>B2 治疗因子（对齐 sim SimBrain.ApplyHealingFactor）：射程内选最低血友军（hpRatio&lt;healHpGate），
    /// 治疗量 = attack（CD = attackCD），治疗期间停火守位。返回是否在治疗中。D3 清理轮：血线读 _config.healHpGate。</summary>
    private bool TryHealAlly(in FactorContext ctx)
    {
        float healRangeWorld = ctx.AttackWorldRange;   // 治疗范围 = 攻击范围
        float healGate = _config != null ? _config.healHpGate : 0.7f;
        IDamageable bestPatient = null;
        float bestHpRatio = float.MaxValue;
        for (int i = 0; i < _nearbyAllies.Count; i++)
        {
            var a = _nearbyAllies[i];
            if (a == null || a.CurrentHp <= 0) continue;
            if (ReferenceEquals(a, _self)) continue;
            float d = Vector2.Distance(_self.GetPosition(), a.GetPosition());
            if (d > healRangeWorld) continue;
            float hpRatio = a.MaxHp > 0 ? (float)a.CurrentHp / a.MaxHp : 1f;
            if (hpRatio < healGate && hpRatio < bestHpRatio)
            {
                bestHpRatio = hpRatio;
                bestPatient = a;
            }
        }
        if (bestPatient == null) return false;   // 无低血友军 -> 正常攻击

        float healCd = Mathf.Max(0.1f, _profession.attackCD);
        if (Time.time - _healTimer >= healCd)
        {
            _healTimer = Time.time;
            bestPatient.Heal(Mathf.Max(1, _profession.attack));
        }
        return true;   // 治疗中停火守位（对齐 sim：StopAttacking + Idle）
    }

    /// <summary>B4 是否点杀职业（角色族 Sniper 族：弓手/弩手；对齐 sim SnipeEnabled 配方，构成驱动非职业驱动）。</summary>
    private bool IsSniperRole()
    {
        if (_profession == null || !_profession.isRanged) return false;
        return _profession.roleFamily == RoleFamily.Sniper;
    }

    /// <summary>B4 是否密度职业（角色族 Aoe 族：法师/大法师；对齐 sim DensityTargetingEnabled 配方，构成驱动非职业驱动）。</summary>
    private bool IsDensityRole()
    {
        if (_profession == null || !_profession.isRanged) return false;
        return _profession.roleFamily == RoleFamily.Aoe;
    }

    /// <summary>B4 邻域敌数统计（密度目标选择用，对齐 sim DensityRadiusCells 邻域扫描）。</summary>
    private int CountNearbyEnemies(IDamageable center, float radiusWorld)
    {
        int count = 0;
        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            var e = _nearbyEnemies[i];
            if (e == null || IsDestroyed(e) || e.CurrentHp <= 0) continue;
            if (Vector2.Distance(e.GetPosition(), center.GetPosition()) <= radiusWorld) count++;
        }
        return count;
    }

    /// <summary>
    /// B4 角色族因子（对齐 sim SimBrain.ApplyProfessionFactors）：
    /// 死拼（亡灵 Tank 撤退血低反冲）/ 保命（Machine 被贴身保射程）/ 顶住（Tank 残血守位）/ 压上（远程僵持推进）。
    /// 构成驱动（roleFamily）+ 阵营差异，默认阈值对齐 sim 配方（0.4/0.35/2格/6格/20格），参数化随训练需要追加。
    /// </summary>
    private void ApplyProfessionFactors(ref BehaviorCommand cmd, in FactorContext ctx)
    {
        if (_profession == null) return;
        RoleFamily role = _profession.roleFamily;
        float cellSize = ctx.CellSize;

        // 死拼（Berserk）：亡灵 Tank 撤退时血低 <40% 反冲最近敌（对齐 sim BerserkHpRatio 0.4）
        if (role == RoleFamily.Tank && !_profession.isRanged
            && _self.GetFaction() == Faction.Undead
            && cmd.Module == BehaviorModule.RetreatMove && ctx.HpRatio < 0.4f)
        {
            IDamageable nearest = FindNearestEnemyInPerception();
            if (nearest != null)
            {
                cmd = new BehaviorCommand
                {
                    Module = BehaviorModule.MoveTowards,
                    TargetPos = Vector2XUnity.FromUnity(nearest.GetPosition()),
                    Speed = _profession.walkSpeed,
                };
            }
        }
        // 保命（SelfPreserve）：Machine 被近战贴身（<2 格）后撤保射程（对齐 sim SiegeMachine 危险 2 格/后撤 6 格）
        else if (role == RoleFamily.Machine && _profession.isRanged && !_profession.isStatic
            && (cmd.Module == BehaviorModule.MoveTowards || cmd.Module == BehaviorModule.RetreatMove))
        {
            IDamageable nearest = FindNearestEnemyInPerception();
            if (nearest != null)
            {
                float d = Vector2.Distance(_self.GetPosition(), nearest.GetPosition());
                if (d < 2f * cellSize)
                {
                    Vector2 away = (_self.GetPosition() - nearest.GetPosition()).normalized;
                    Vector2 target = _self.GetPosition() + away * (6f * cellSize);
                    cmd = new BehaviorCommand
                    {
                        Module = BehaviorModule.MoveTowards,
                        TargetPos = Vector2XUnity.FromUnity(target),
                        Speed = _profession.walkSpeed,
                    };
                }
            }
        }
        // 顶住（TankHold）：Tank 残血 <35% 且有队友时取消撤退守位（对齐 sim TankHoldHpRatio 0.35）
        else if (role == RoleFamily.Tank && !_profession.isRanged
            && cmd.Module == BehaviorModule.RetreatMove && ctx.HpRatio < 0.35f)
        {
            cmd = new BehaviorCommand { Module = BehaviorModule.Idle, Duration = 0.8f };
        }
        // 压上（PressWhenStalled）：远程散兵感知内无敌但场上有敌且数量不劣势 → 朝敌侧推进（对齐 sim PressWhenStalled，治僵持平局）
        else if ((role == RoleFamily.Sniper || role == RoleFamily.Aoe) && _profession.isRanged
            && !ctx.HasFormationSlot && _nearbyEnemies.Count == 0 && _chaseTarget == null
            && cmd.Module != BehaviorModule.WorkAt)
        {
            int enemyCount = CountAliveByFaction(_self.GetFaction(), isEnemy: true);
            int allyCount = CountAliveByFaction(_self.GetFaction(), isEnemy: false);
            if (enemyCount > 0 && enemyCount - allyCount <= 2)
            {
                Vector2 target = _self.GetPosition() + Vector2.right * (20f * cellSize);   // 人类默认朝 +x 进攻方向
                cmd = new BehaviorCommand
                {
                    Module = BehaviorModule.MoveTowards,
                    TargetPos = Vector2XUnity.FromUnity(target),
                    Speed = _profession.walkSpeed,
                };
            }
        }
    }

    /// <summary>B4 感知范围内最近存活敌（因子用）。</summary>
    private IDamageable FindNearestEnemyInPerception()
    {
        IDamageable nearest = null;
        float best = float.MaxValue;
        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            var e = _nearbyEnemies[i];
            if (e == null || IsDestroyed(e) || e.CurrentHp <= 0) continue;
            float d = Vector2.Distance(_self.GetPosition(), e.GetPosition());
            if (d < best) { best = d; nearest = e; }
        }
        return nearest;
    }

    /// <summary>B4 全场存活单位计数（UnitRegistry，压上因子用）。</summary>
    private int CountAliveByFaction(Faction myFaction, bool isEnemy)
    {
        if (UnitRegistry.Instance == null) return 0;
        var list = isEnemy ? UnitRegistry.Instance.GetEnemies(myFaction)
                           : UnitRegistry.Instance.GetUnitsByFaction(myFaction);
        if (list == null) return 0;
        int count = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var u = list[i];
            if (u != null && u.CurrentHp > 0) count++;
        }
        return count;
    }

    // ===== 骑兵冲锋（3.6 §5.3 五态：0=None 1=准备 2=突进① 3=停顿 4=突进②；免伤 70% 突进中生效）=====

    /// <summary>本单位的 UnitController（冲锋状态挂在它身上）。</summary>
    private UnitController SelfUnit => _self as UnitController;

    // 突进状态（状态 2 期间有效）
    private Vector2 _chargeStart;      // 突进起点（线段扫描基准）
    private Vector2 _chargeDir;        // 突进方向（单位化）
    private Vector2 _chargeEnd;        // 突进终点（起点 + dir × chargeRange）
    private float _chargeTraveled;     // 累计位移
    private readonly HashSet<UnitController> _chargeHit = new HashSet<UnitController>();  // 已击飞（防重复）

    /// <summary>独立触发冲锋：扫感知列表找 chargeRange 内目标（不等近战射程，冲锋 4 格生效）。</summary>
    private void TryStartChargeFromPerception(float cellSize)
    {
        if (_profession == null || !_profession.isCavalry) return;
        if (SelfUnit == null || SelfUnit.ChargeState != 0) return;
        if (Time.time < SelfUnit.ChargeReadyTime) return;

        float rangeWorld = _profession.chargeRangeCells * cellSize;
        UnitController best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            var e = _nearbyEnemies[i];
            if (e == null || e.CurrentHp <= 0) continue;
            if (!(e is UnitController uc)) continue;
            float d = Vector2.Distance(transform.position, uc.transform.position);
            if (d <= rangeWorld && d < bestDist)
            {
                bestDist = d;
                best = uc;
            }
        }
        if (best != null)
            TryStartCharge(best, cellSize);
    }

    /// <summary>触发冲锋：进入准备态（下帧突进）。</summary>
    private void TryStartCharge(UnitController target, float cellSize)
    {
        if (!_profession.isCavalry) return;
        if (SelfUnit == null || SelfUnit.ChargeState != 0) return;
        if (Time.time < SelfUnit.ChargeReadyTime) return;
        if (target == null || !target.IsAlive || target.CurrentHp <= 0) return;

        float rangeWorld = _profession.chargeRangeCells * cellSize;
        if (Vector2.Distance(transform.position, target.transform.position) > rangeWorld) return;

        SelfUnit.ChargeState = 1;
        SelfUnit.ChargeTarget = target;
    }

    /// <summary>
    /// 每帧推进冲锋状态机（Update 调）：
    ///   1 准备 → 2 突进①（高速连续位移 + 扫路径击飞）→ 3 停顿（chargePairGap）→ 4 突进②（同①）→ 0 组冷却。
    /// 3.6 §5.3 穿透冲锋双连击：两段各冲满 chargeRange（4 格），路径上所有敌对单位被击飞（韧性决定距离）。
    /// </summary>
    private void TickCharge()
    {
        if (_profession == null || !_profession.isCavalry) return;
        if (SelfUnit == null) return;

        float cellSize = GetCellSize();
        float now = Time.time;

        // 3 停顿：两段冲锋之间的短暂间隔（chargePairGap 0.3s）→ 续接突进②
        if (SelfUnit.ChargeState == 3)
        {
            if (now >= SelfUnit.ChargeSecondTime)
                BeginChargeSegment2();
            return;
        }

        // 1 准备：锁定方向与终点，下帧开始突进①
        if (SelfUnit.ChargeState == 1)
        {
            var t = SelfUnit.ChargeTarget;
            if (t == null || !t.IsAlive || t.CurrentHp <= 0)
            {
                SelfUnit.ChargeState = 0;
                SelfUnit.ChargeTarget = null;
                return;
            }
            _chargeStart = (Vector2)transform.position;
            _chargeDir = ((Vector2)t.transform.position - _chargeStart).normalized;
            if (_chargeDir == Vector2.zero) _chargeDir = Vector2.right;
            _chargeEnd = _chargeStart + _chargeDir * (_profession.chargeRangeCells * cellSize);
            _chargeTraveled = 0f;
            _chargeHit.Clear();
            SelfUnit.ChargeState = 2;
            return;
        }

        // 2 突进① / 4 突进②：chargeSpeed 高速连续位移（非瞬移），每帧扫上帧→本帧线段击飞
        if (SelfUnit.ChargeState == 2 || SelfUnit.ChargeState == 4)
        {
            bool isSecond = SelfUnit.ChargeState == 4;
            float rangeWorld = _profession.chargeRangeCells * cellSize;
            float step = _profession.chargeSpeed * Time.deltaTime;
            float travelBefore = _chargeTraveled;
            _chargeTraveled += step;
            bool reachEnd = _chargeTraveled >= rangeWorld;
            float travel = reachEnd ? rangeWorld : _chargeTraveled;

            Vector2 newPos = _chargeStart + _chargeDir * travel;
            // 3.7 P1.5：冲锋撞墙即止（城墙阻挡冲锋路径，骑兵不穿墙；拒马=减速带不挡，冲过正常结算）
            if (SelfUnit.IsBlockedByFortification(newPos))
            {
                SelfUnit.ChargeState = 0;
                SelfUnit.ChargeTarget = null;
                SelfUnit.ChargeReadyTime = now + _profession.chargeGroupCooldown;
                return;
            }
            SelfUnit.Teleport(newPos);

            // 扫线段（上帧位置 → 本帧位置）内的敌对单位
            float lastX = _chargeStart.x + _chargeDir.x * travelBefore;
            float curX = _chargeStart.x + _chargeDir.x * travel;
            ChargeSweep(Mathf.Min(lastX, curX), Mathf.Max(lastX, curX), cellSize);

            if (reachEnd)
            {
                if (isSecond)
                {
                    // 突进② 结束 → 组冷却
                    SelfUnit.ChargeState = 0;
                    SelfUnit.ChargeTarget = null;
                    SelfUnit.ChargeReadyTime = now + _profession.chargeGroupCooldown;
                }
                else
                {
                    // 突进① 结束 → 短暂停顿后突进②
                    SelfUnit.ChargeState = 3;
                    SelfUnit.ChargeSecondTime = now + _profession.chargePairGap;
                }
            }
        }
    }

    /// <summary>续接第二段冲锋：从当前位置重新索敌（主目标存活则重瞄，否则续向原方向）。</summary>
    private void BeginChargeSegment2()
    {
        var t = SelfUnit.ChargeTarget;
        _chargeStart = (Vector2)transform.position;
        if (t != null && t.IsAlive && t.CurrentHp > 0)
        {
            _chargeDir = ((Vector2)t.transform.position - _chargeStart).normalized;
            if (_chargeDir == Vector2.zero) _chargeDir = Vector2.right;
        }
        // 目标已死/被击飞出范围 → 续向原方向
        _chargeEnd = _chargeStart + _chargeDir * (_profession.chargeRangeCells * GetCellSize());
        _chargeTraveled = 0f;
        _chargeHit.Clear();   // 第二段重新结算路径（双连击：同路径单位二段再撞飞）
        SelfUnit.ChargeState = 4;
    }

    /// <summary>
    /// 路径击飞（3.6 §5.4）：x1→x2 线段上的敌对单位被击飞（动能+θ 模型，韧性决定距离）。
    /// 3.6 §5.3 穿透冲锋：路径上所有敌对单位都吃冲锋伤害（chargeDamage=80）。工事/机器免疫，击飞打断攻击。
    /// </summary>
    private void ChargeSweep(float x1, float x2, float cellSize)
    {
        var enemies = QueryUnitsInRangeX(x1, x2, cellSize);
        foreach (var uc in enemies)
        {
            if (uc == null || !uc.IsAlive || uc.CurrentHp <= 0) continue;
            if (ReferenceEquals(uc, SelfUnit)) continue;
            if (_chargeHit.Contains(uc)) continue;   // 已撞飞过，不重复结算
            if (uc.GetFaction() == _self.GetFaction() || uc.GetFaction() == Faction.None) continue;
            if (uc.fortification != null) continue;                              // 工事免疫
            if (uc.Data is NpcProfessionDef nd && nd.isStatic) continue;         // 机器免疫

            _chargeHit.Add(uc);

            // 击飞（3.6 §5.4：动能+θ 模型，韧性决定距离）
            CombatRules.ComputeKnockback(_profession.chargeDamage, uc.Toughness,
                out float distWorld, out float dur);
            Vector2 kbDir = _chargeDir;
            if (Random.value > 0.8f) kbDir = -_chargeDir;   // 80% 沿冲击 / 20% 反向
            DamageSystem.Instance?.TryKnockback(uc, kbDir, distWorld, dur);

            // 3.6 §5.3 穿透冲锋：路径上所有敌对单位都吃冲锋伤害（穿透群伤）
            DamageSystem.Instance?.ApplyDamage(_self, uc, (int)_profession.chargeDamage);
        }
    }

    /// <summary>区间查询：x1→x2 范围内格子的单位（穿透冲锋路径扫描）。</summary>
    private List<UnitController> QueryUnitsInRangeX(float x1, float x2, float cellSize)
    {
        var result = new List<UnitController>();
        if (GridSystem.Instance == null) return result;
        int c1 = GridSystem.Instance.WorldToCoord(new Vector2(x1, 0)).x;
        int c2 = GridSystem.Instance.WorldToCoord(new Vector2(x2, 0)).x;
        for (int cx = c1; cx <= c2; cx++)
        {
            for (int y = 0; y <= 1; y++)
                result.AddRange(GridSystem.Instance.GetUnitsInCell(new GridCoord(cx, y)));
        }
        return result;
    }

    // ===== 切换历史 =====

    private void RecordSwitchHistory(float time)
    {
        Focus curFocus = _attention.CurrentFocus;
        BehaviorSpectrum curSpectrum = _lastCtx.PostureDecision.Spectrum;

        bool focusChanged = !AISwitchRecord.FocusEquals(_lastRecordedFocus, curFocus);
        bool spectrumChanged = _lastRecordedSpectrum != curSpectrum;

        if (focusChanged || spectrumChanged)
        {
            _switchHistory[_switchHistoryHead] = new AISwitchRecord(
                time, _lastRecordedFocus, curFocus,
                _lastRecordedSpectrum, curSpectrum
            );
            _switchHistoryHead = (_switchHistoryHead + 1) % _switchHistory.Length;
            _switchHistoryCount = Mathf.Min(_switchHistoryCount + 1, _switchHistory.Length);
            _lastRecordedFocus = curFocus;
            _lastRecordedSpectrum = curSpectrum;
        }
    }

    // ===== 昼夜（实装 TimeManager.CurrentPhase）=====

    /// <summary>
    /// 销毁防御：IDamageable 接口变量的 !=null 是普通引用比较，不触发 Unity 的 Object==null 销毁检查。
    /// 需先 as UnityEngine.Object 再判空，才能拦截已销毁（但引用未清空）的 UnitController 等组件。
    /// </summary>
    private static bool IsDestroyed(IDamageable d)
    {
        var uo = d as UnityEngine.Object;
        return uo == null;  // UnityEngine.Object==null 触发销毁检测
    }

    /// <summary>判断当前是否夜晚（Night/Dusk 视为夜晚，威胁加重 + 归巢放大）</summary>
    private bool IsNight()
    {
        return TimeManager.Instance != null
            && (TimeManager.Instance.CurrentPhase == TimePhase.Night
                || TimeManager.Instance.CurrentPhase == TimePhase.Dusk);
    }

    /// <summary>
    /// 夜晚因子 0-1（Dusk/Dawn 渐变，Night=1，Day=0）。
    /// 用于 SafetyStimulus 放大（nightPullWeight）。
    /// </summary>
    private float GetNightFactor()
    {
        if (TimeManager.Instance == null) return 0f;
        switch (TimeManager.Instance.CurrentPhase)
        {
            case TimePhase.Night: return 1f;
            case TimePhase.Dusk: return 0.6f;   // 黄昏渐入
            case TimePhase.Dawn: return 0.4f;   // 黎明渐出
            default: return 0f;                  // Day
        }
    }

    // ===== 辅助 =====

    private float GetCellSize()
    {
        return GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize : 2.26f;
    }

    /// <summary>设置跟随锚点（调度中心/军令下发时调，§3.2）</summary>
    public void SetFollowAnchor(UnitController anchor, TaskPriority priority, float intensity)
    {
        _followProvider.SetFollowAnchor(anchor, priority, intensity);
    }

    /// <summary>
    /// 设置编队槽位（3.0.1_3 §2.1，FormationController 下发军令时调）。
    /// 槽位化跟随：目标 = 锚点位置 + slotOffset × cellSize（cell 吸附）。
    /// 复用 FollowStimulus 承载（审计 D3），不新建 FormationStimulus 类型。
    /// </summary>
    public void SetFormationSlot(UnitController anchor, TaskPriority priority, float intensity, Vector2Int slotOffset, bool royalCommand = false)
    {
        // 3.0.1_8 §6.6：君主令载体透传（royalCommand=true → 个体永不弃任务）
        _followProvider.SetFormationSlot(anchor, priority, intensity, slotOffset, royalCommand);
    }

    /// <summary>清除跟随锚点（部队解散/任务完成时调）</summary>
    public void ClearFollowAnchor()
    {
        _followProvider.ClearAnchor();
    }

    /// <summary>
    /// 清除编队槽位绑定（3.0.1_3 §15.5 ClearFormationState 调）。
    /// 与 ClearFollowAnchor 等价（SlotOffset 一并清零），独立方法保语义清晰。
    /// </summary>
    public void ClearFormationSlot()
    {
        _followProvider.ClearAnchor();
    }

    /// <summary>是否绑定了编队槽位（FormationController 状态清理用）</summary>
    public bool HasFormationSlot => _followProvider.IsActive && _followProvider.Stimulus.IsFormationSlot;

    /// <summary>注入任务刺激源（调度中心派工时调，如砍树 B 级任务）</summary>
    public void AddTaskStimulus(TaskStimulus stimulus)
    {
        _attention.AddStimulus(stimulus);
    }

    /// <summary>移除指定来源的任务刺激源（任务完成/取消时调）</summary>
    public void RemoveTaskStimulus(object source)
    {
        _attention.RemoveTaskStimuli(source);
    }

    private void OnDestroy()
    {
        if (_self != null && DamageSystem.Instance != null)
            DamageSystem.Instance.Unregister(_self);
    }
}
