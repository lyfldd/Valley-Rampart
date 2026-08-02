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

    // ===== 管线中间产物缓存 =====
    private float _lastRaw;           // 上一帧 rawFactor（量化器消费）
    private FactorContext _lastCtx;   // 上一帧完整 ctx（调试面板读）
    private BehaviorCommand _lastCmd; // 上一帧 Think 产出的 cmd（Execute 每帧复用）

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
    public Vector2 DebugHomePoint => _lastCtx.HomePoint;

    // ===== 初始化 =====
    public void Init(NpcProfessionDef profession)
    {
        _profession = profession;
        _controller = GetComponent<UnitController>();
        _self = GetComponent<IDamageable>();
        _config = Resources.Load<AttentionTuningConfig>("Config/AttentionTuningConfig");
        if (_config == null)
            Debug.LogError("[NPCBrain] 未找到 AttentionTuningConfig！请创建 Resources/Config/AttentionTuningConfig.asset");

        // 初始化记忆组件群
        _threatHysteresis = new ThreatHysteresisComponent(_config);
        _protectionHysteresis = new ProtectionHysteresisComponent(_config);
        _hitCooldown = new HitCooldownStateMachine();
        _memoryComponents = new IMemoryComponent[] { _threatHysteresis, _protectionHysteresis, _hitCooldown };

        // 3.0.1_4：注入全局调参引用（破阵博弈/漫游用）
        _attention.SetConfig(_config);

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

            float dist = Vector2.Distance(myPos, enemy.GetPosition());
            if (dist < _nearestDist) _nearestDist = dist;
            float intensity = Mathf.Max(1f, 100f * (1f - dist / perceptionWorld));

            var stimulus = new ThreatStimulus(
                enemy,
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
                _lastAggressor,
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
                    targetPos: hotspot,                 // 热点即目标位置
                    intensity: 0.6f,                    // > Safety 0.5，< Follow S 级 4.5
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
        ctx.FocusDecision = L1FocusEvaluator.Evaluate(_attention, in ctx);
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
        return new FactorContext
        {
            Self = _self,
            Profession = _profession,
            Config = _config,
            SelfPos = _self.GetPosition(),
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
            HomePoint = _homePointProvider != null ? _homePointProvider.GetHomePoint(this) : Vector2.zero,
            // 3.0.1_3：编队槽位（守阵追击 clamp 用，§4.1）
            HasFormationSlot = _followProvider.IsActive && _followProvider.Stimulus.IsFormationSlot,
            FormationSlotWorld = ResolveFormationSlotWorld(),
            // 3.0.1_LOD §3.2：区块威胁热度（环境型威胁因子；LODSystem 未挂载=0 行为不变）
            RegionHeat = _lodSystem != null ? _lodSystem.GetHeatAt(_self.GetPosition()) : 0f,
            // 3.0.1_8 综合因子（L2 在 ③ 阶段读取，此处用上一帧 rawFactor = _lastRaw，与量化器消费时序一致）：
            //   ThreatFactor = 上一帧 rawFactor（连续 0-1，供 L2 连续仲裁）
            //   FormationFactor = 编队军令强度归一化（FollowStimulus.Intensity / 基准 4.5，有编队≈1 无编队=0）
            ThreatFactor = _lastRaw,
            FormationFactor = _followProvider.IsActive
                ? Mathf.Clamp01(_followProvider.Stimulus.Intensity / FormationController.OrderIntensityBase)
                : 0f,
        };
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
        return (Vector2)stim.Anchor.transform.position
            + new Vector2(stim.SlotOffset.x * cs, stim.SlotOffset.y * cs);
    }

    // ===== 攻击链路保留（搬自旧 ThreatFocusBehavior，不进 Executor）=====

    /// <summary>
    /// 攻击注册：威胁焦点 + 士兵 + 在范围内 -> DamageSystem.RegisterAttack。
    /// 受击冷却防追击的旧 allowChase:false 由 Caution 态 HoldPosition 胜出焦点取代。
    /// </summary>
    private void UpdateCombatRegistration(in FactorContext ctx)
    {
        FocusDecision focus = ctx.FocusDecision;
        bool shouldAttack = false;
        IDamageable targetEnemy = null;

        if (_profession.attack > 0)  // 战斗单位才攻击
        {
            // 路径1：威胁焦点 + 在射程内（原逻辑）
            if (focus.IsValid && focus.Focus is ThreatStimulus ts
                && ts.Enemy != null && !IsDestroyed(ts.Enemy) && ts.Enemy.CurrentHp > 0)
            {
                float dist = Vector2.Distance(ctx.SelfPos, ts.Enemy.GetPosition());
                if (dist <= ctx.AttackWorldRange)
                {
                    shouldAttack = true;
                    targetEnemy = ts.Enemy;
                }
            }
            // 路径2：编队跟随焦点（FollowStimulus）或无威胁焦点时，感知范围内最近敌人在射程内也开火
            // 解决编队优先（威胁0/1级走FollowAnchor）时弓手站槽位看戏的问题
            if (!shouldAttack && _nearbyEnemies.Count > 0)
            {
                IDamageable nearest = null;
                float nearestDist = float.MaxValue;
                for (int i = 0; i < _nearbyEnemies.Count; i++)
                {
                    var e = _nearbyEnemies[i];
                    if (e == null || IsDestroyed(e) || e.CurrentHp <= 0) continue;
                    float d = Vector2.Distance(ctx.SelfPos, e.GetPosition());
                    if (d < nearestDist) { nearestDist = d; nearest = e; }
                }
                if (nearest != null && nearestDist <= ctx.AttackWorldRange)
                {
                    shouldAttack = true;
                    targetEnemy = nearest;
                }
            }
        }

        if (shouldAttack)
        {
            if (!ReferenceEquals(targetEnemy, _currentAttackTarget))
            {
                StopAttacking();
                var profile = new AttackProfile
                {
                    attack = _profession.attack,
                    range = _profession.attackRange,
                    cd = _profession.attackCD,
                    isRanged = _profession.isRanged,
                    projectileSpeed = _profession.projectileSpeed
                };
                if (DamageSystem.Instance != null && DamageSystem.Instance.RegisterAttack(_self, targetEnemy, profile))
                    _currentAttackTarget = targetEnemy;
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
    public void SetFormationSlot(UnitController anchor, TaskPriority priority, float intensity, Vector2Int slotOffset)
    {
        _followProvider.SetFormationSlot(anchor, priority, intensity, slotOffset);
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
