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
/// 通用 NPC 大脑（3.0.1 第八节·干细胞框架）。
///
/// 所有 NPC 共用此框架，通过 NpcProfessionDef SO 配置分化行为。
/// 集成机制甲（五层注意力）+ 机制乙（三维权衡）+ 威胁评定 + 行为执行。
///
/// 决策流水线：
///   感知（PerceptionSystem 查附近敌人）-> 注意力（选焦点）-> 威胁评定 -> 权衡（算谱系）-> 行为执行
///
/// 替换旧 StubAttacker：攻击改由 NPCBrain 在焦点为敌人且在范围内时驱动 DamageSystem.RegisterAttack。
/// </summary>
[RequireComponent(typeof(UnitController))]
public class NPCBrain : MonoBehaviour, IAIDebugInfoExtended
{
    // ===== 依赖 =====
    private UnitController _controller;
    private IDamageable _self;
    private NpcProfessionDef _profession;
    private AttentionTuningConfig _config;

    // ===== AI 子系统 =====
    private readonly AttentionSystem _attention = new AttentionSystem();
    private readonly ThreatAssessor _threatAssessor = new ThreatAssessor();
    private readonly TradeoffSystem _tradeoff = new TradeoffSystem();

    // ===== 定时器 =====
    private float _thinkTimer;
    private float _perceptionTimer = 999f;  // 首帧立即触发感知
    private const float ThinkInterval = 0.1f;  // 10Hz 思考频率

    // ===== 受击冷却（防止撤退->恢复->送死->撤退无限循环）=====
    private const float HitCooldownDuration = 5f;  // 被击后 5 秒内不追击
    private float _lastHitTime = -999f;
    /// <summary>是否在受击冷却中（被击后 N 秒内不追击敌人）</summary>
    public bool IsInHitCooldown => Time.time - _lastHitTime < HitCooldownDuration;

    // ===== 运行时状态 =====
    private IDamageable _currentAttackTarget;
    private readonly List<IDamageable> _nearbyEnemies = new List<IDamageable>();
    private readonly List<IDamageable> _nearbyAllies = new List<IDamageable>();
    private ThreatAssessmentResult _lastThreatResult;

    // ===== 切换历史（3.0.1_2 AI 调试用）=====
    private readonly AISwitchRecord[] _switchHistory = new AISwitchRecord[10];
    private int _switchHistoryHead;
    private int _switchHistoryCount;
    private Focus _lastRecordedFocus;
    private BehaviorSpectrum _lastRecordedSpectrum;

    // ===== IAIDebugInfo 实现 =====
    public Focus CurrentFocus => _tradeoff.CommittedFocus;
    public BehaviorSpectrum CurrentSpectrum => _tradeoff.CurrentSpectrum;
    public ThreatLevel CurrentThreatLevel => _threatAssessor.CurrentLevel;
    public int NearbyEnemyCount => _lastThreatResult.NearbyEnemyCount;
    public int NearbyAllyCount => _lastThreatResult.NearbyAllyCount;
    public bool HasProtection => _lastThreatResult.HasProtection;
    public bool InSafetyConfirmation => _tradeoff.InSafetyConfirmation;

    // ===== IAIDebugInfoExtended 实现（3.0.1_2）=====
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
        // 倒序：最新的在前
        output.Reverse();
    }

    public void GetTopStimuli(List<StimulusDebugInfo> output, int maxCount)
    {
        output.Clear();
        if (_attention == null) return;
        _attention.GetTopStimuliForDebug(output, maxCount);
    }

    // ===== 初始化 =====
    public void Init(NpcProfessionDef profession)
    {
        _profession = profession;
        _controller = GetComponent<UnitController>();
        _self = GetComponent<IDamageable>();
        _config = Resources.Load<AttentionTuningConfig>("Config/AttentionTuningConfig");
        if (_config == null)
            Debug.LogError("[NPCBrain] 未找到 AttentionTuningConfig！请创建 Resources/Config/AttentionTuningConfig.asset");
    }

    private void OnEnable()
    {
        EventBus.Subscribe<UnitDamagedEvent>(OnDamaged);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<UnitDamagedEvent>(OnDamaged);
    }

    /// <summary>自身受击 -> 触发威胁 3（致命）+ 记录受击时间。</summary>
    private void OnDamaged(UnitDamagedEvent evt)
    {
        if (ReferenceEquals(evt.Unit, _self))
        {
            _threatAssessor.OnDamaged(Time.time);
            _lastHitTime = Time.time;
        }
    }

    private void Update()
    {
        if (_self == null || _profession == null || _config == null) return;
        if (_self.CurrentHp <= 0) return;

        // 感知更新（较低频率，默认 0.2s）
        _perceptionTimer += Time.deltaTime;
        if (_perceptionTimer >= _config.perceptionUpdateInterval)
        {
            _perceptionTimer = 0f;
            UpdatePerception();
        }

        // 思考更新（10Hz）
        _thinkTimer += Time.deltaTime;
        if (_thinkTimer >= ThinkInterval)
        {
            _thinkTimer = 0f;
            Think();
        }
    }

    // ===== 感知 =====

    /// <summary>查询附近敌人/友军，更新注意力系统的威胁刺激源。</summary>
    private void UpdatePerception()
    {
        float perceptionWorld = _profession.perceptionRadius * GetCellSize();
        Vector2 myPos = _self.GetPosition();
        Faction myFaction = _self.GetFaction();

        // 空间分区查询
        PerceptionSystem.QueryNearby(myPos, perceptionWorld, myFaction, true, _nearbyEnemies);
        PerceptionSystem.QueryNearby(myPos, perceptionWorld, myFaction, false, _nearbyAllies);

        // 更新威胁刺激源（清空旧威胁，添加新威胁）
        _attention.ClearThreats();
        float currentTime = Time.time;

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            var enemy = _nearbyEnemies[i];
            if (enemy == null || enemy.CurrentHp <= 0) continue;

            float dist = Vector2.Distance(myPos, enemy.GetPosition());
            // 强度：越近越高（100 = 贴脸，0 = 感知边缘）
            float intensity = Mathf.Max(1f, 100f * (1f - dist / perceptionWorld));

            var stimulus = new ThreatStimulus(
                enemy,
                threatLevel: (int)_threatAssessor.CurrentLevel,
                intensity: intensity,
                expiry: currentTime + _config.threatDecayTime
            );
            _attention.AddStimulus(stimulus);
        }
    }

    // ===== 思考（决策流水线）=====

    private void Think()
    {
        float currentTime = Time.time;

        // 1. 注意力系统更新（机制甲）
        _attention.Update(currentTime);

        // 2. 威胁评定
        float perceptionWorld = _profession.perceptionRadius * GetCellSize();
        float attackWorld = _profession.attackRange * GetCellSize();

        float nearestDist = float.MaxValue;
        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            if (_nearbyEnemies[i] == null || _nearbyEnemies[i].CurrentHp <= 0) continue;
            float d = Vector2.Distance(_self.GetPosition(), _nearbyEnemies[i].GetPosition());
            if (d < nearestDist) nearestDist = d;
        }

        float hpRatio = _self.MaxHp > 0 ? (float)_self.CurrentHp / _self.MaxHp : 0f;
        bool isNight = IsNight();

        float rawFactor = ThreatAssessor.CalculateRawFactor(
            nearestDist, _nearbyEnemies.Count, hpRatio, _nearbyAllies.Count,
            isNight, _profession, _config, perceptionWorld, attackWorld
        );

        _lastThreatResult = _threatAssessor.Update(
            rawFactor, _nearbyEnemies.Count, _nearbyAllies.Count,
            hpRatio, isNight, _config, _profession, currentTime
        );

        // 3. 权衡系统更新（机制乙）
        TaskPriority? taskPriority = GetCurrentTaskPriority();
        _tradeoff.Update(
            _attention.CurrentFocus, _attention.FocusChanged,
            _lastThreatResult, taskPriority, _profession, _config, currentTime
        );

        // 3.5 记录切换历史（3.0.1_2 AI 调试用）
        RecordSwitchHistory(currentTime);

        // 4. 行为执行
        ExecuteBehavior();
    }

    /// <summary>记录焦点/谱系切换历史（3.0.1_2）。</summary>
    private void RecordSwitchHistory(float time)
    {
        Focus curFocus = _tradeoff.CommittedFocus;
        BehaviorSpectrum curSpectrum = _tradeoff.CurrentSpectrum;

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

    /// <summary>获取当前任务优先级（P0 无任务系统，返回 null）。</summary>
    private TaskPriority? GetCurrentTaskPriority()
    {
        return null;
    }

    /// <summary>判断当前是否夜晚。</summary>
    private bool IsNight()
    {
        // P0: TimeManager 昼夜因子预留，首版默认白天
        return false;
    }

    // ===== 行为执行 =====

    private void ExecuteBehavior()
    {
        switch (_tradeoff.CurrentSpectrum)
        {
            case BehaviorSpectrum.FullRetreat:
                RetreatBehavior();
                break;
            case BehaviorSpectrum.Cautious:
                // 谨慎：维持当前焦点行为，不追击
                FocusBehavior(allowChase: false);
                break;
            default:
                // 全力执行：受击冷却内不追击（防止撤退->恢复->送死->撤退无限循环）
                FocusBehavior(allowChase: !IsInHitCooldown);
                break;
        }
    }

    /// <summary>焦点驱动行为：根据焦点类型执行移动/攻击。</summary>
    private void FocusBehavior(bool allowChase)
    {
        Focus focus = _tradeoff.CommittedFocus;

        if (!focus.IsValid)
        {
            // 无焦点 -> 待机，停止攻击
            StopAttacking();
            return;
        }

        if (focus.Is(AttentionLayer.Threat))
        {
            // 威胁焦点 -> 追击并攻击
            ThreatFocusBehavior(focus, allowChase);
        }
        else if (focus.Is(AttentionLayer.Task))
        {
            // 任务焦点 -> 移动到任务位置（P0 无任务系统，此分支暂不触发）
            StopAttacking();
            _controller.MoveTowards(focus.Position);
        }
        else
        {
            // 其他层焦点 -> 待机
            StopAttacking();
        }
    }

    /// <summary>威胁焦点行为：靠近敌人 + 攻击注册。</summary>
    private void ThreatFocusBehavior(Focus focus, bool allowChase)
    {
        var enemy = focus.Source as IDamageable;
        if (enemy == null || enemy.CurrentHp <= 0)
        {
            StopAttacking();
            return;
        }

        // 非战斗单位（工人/农民）不攻击，撤退由谱系 4 处理
        if (_profession.attack <= 0)
        {
            StopAttacking();
            return;
        }

        float dist = Vector2.Distance(_self.GetPosition(), enemy.GetPosition());
        float attackRange = _profession.attackRange * GetCellSize();

        if (dist <= attackRange)
        {
            // 在攻击范围内 -> 注册攻击
            if (!ReferenceEquals(enemy, _currentAttackTarget))
            {
                var profile = new AttackProfile
                {
                    attack = _profession.attack,
                    range = _profession.attackRange,
                    cd = _profession.attackCD,
                    isRanged = _profession.isRanged,
                    projectileSpeed = _profession.projectileSpeed
                };
                bool success = DamageSystem.Instance != null
                    && DamageSystem.Instance.RegisterAttack(_self, enemy, profile);
                if (success)
                    _currentAttackTarget = enemy;
            }
        }
        else if (allowChase)
        {
            // 不在范围内且允许追击 -> 移动靠近
            StopAttacking();
            _controller.MoveTowards(enemy.GetPosition());
        }
        // 不允许追击（谨慎态）-> 原地待机，不追
    }

    /// <summary>撤退行为：按方向远离敌人一步，不设固定终点（敌人消失即停）。</summary>
    private void RetreatBehavior()
    {
        StopAttacking();

        if (_nearbyEnemies.Count == 0) return;

        Vector2 myPos = _self.GetPosition();
        Vector2 retreatDir = Vector2.zero;
        int count = 0;

        for (int i = 0; i < _nearbyEnemies.Count; i++)
        {
            if (_nearbyEnemies[i] == null || _nearbyEnemies[i].CurrentHp <= 0) continue;
            // 累加远离每个敌人的方向
            retreatDir += (myPos - _nearbyEnemies[i].GetPosition()).normalized;
            count++;
        }

        if (count == 0) return;

        retreatDir = retreatDir.normalized;
        if (retreatDir == Vector2.zero) retreatDir = Vector2.left;

        // 按方向移动一步（不是追一个移动的目标点）
        _controller.Move(retreatDir, run: true);
    }

    /// <summary>停止攻击并注销注册。</summary>
    private void StopAttacking()
    {
        if (_currentAttackTarget != null)
        {
            DamageSystem.Instance?.Unregister(_self);
            _currentAttackTarget = null;
        }
    }

    // ===== 辅助 =====

    private float GetCellSize()
    {
        return GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize : 2.26f;
    }

    private void OnDestroy()
    {
        if (_self != null && DamageSystem.Instance != null)
            DamageSystem.Instance.Unregister(_self);
    }
}
