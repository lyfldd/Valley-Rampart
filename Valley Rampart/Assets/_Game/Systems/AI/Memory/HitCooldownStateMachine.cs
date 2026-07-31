using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 受击冷却状态机
//  详见 3.0.1_2_输入输出决定层设计.md §13.3
//  三态 Normal/Caution/Probe，双路径更新（时长驱动+事件驱动）
//  Caution 计时自 MoveComplete 起（非受击时刻），否则撤退移动时间吃掉 cautionDuration
// ============================================================================

/// <summary>
/// 受击冷却状态机（§13.3，IMemoryComponent 实现）。
/// 住 NPCBrain（管线外），作 FactorContext 输入，§9 纯计算声明成立。
///
/// 三态：Normal / Caution / Probe（完全恢复 = 回 Normal，无独立 Recovery 态）。
/// 双路径更新：
///   (a) 时长驱动：Caution 满 cautionDuration -> Probe；Probe 满 recoveryDuration -> Normal
///   (b) 事件驱动：受击 -> 任意态->Caution 且 hitCount++；MoveComplete -> Caution 计时起点
///
/// Caution"原地不动"的涌现机制（审计补全）：
///   威胁加成无法实现原地。正确机制：Caution 期间
///   ① 任务类刺激 × stateTaskDiscount（强度打骨折）
///   ② 经 GetActiveStimuli() 注入 HoldPositionStimulus（目标=当前位置，强度中等）
///   -> 驻留焦点胜出 -> 已在目标点 -> 谱系 0 + 位置型已到达 -> Idle。
///   威胁加成只负责"保持警戒姿态"（提权威胁层），不负责原地。
/// </summary>
public class HitCooldownStateMachine : IMemoryComponent
{
    private HitCooldownState _state = HitCooldownState.Normal;
    private int _hitCount;
    private float _stateTimer;

    // 时长缓存（进入态时算一次，避免每 tick 重算）
    private float _cautionDuration;
    private float _recoveryDuration;

    // 池化复用：HoldPositionStimulus 单实例 + 缓存单元素列表（零 GC）
    private readonly HoldPositionStimulus _holdStimulus = new HoldPositionStimulus();
    private readonly List<IStimulus> _cachedHoldList;

    public HitCooldownStateMachine()
    {
        _cachedHoldList = new List<IStimulus>(1) { _holdStimulus };
    }

    public HitCooldownState CurrentState => _state;
    public int HitCount => _hitCount;

    /// <summary>
    /// 时长驱动更新（§13.3 路径 a）。
    /// 注意：Caution 计时起点 = MoveComplete 时刻（OnMoveComplete 重置 _stateTimer），
    /// 非受击时刻--否则撤退移动时间吃掉 cautionDuration。
    /// </summary>
    public void Tick(float dt, in FactorContext ctx)
    {
        // 缓存当前位置/配置供 GetActiveStimuli 用（Tick 在 GetActiveStimuli 前调用）
        CachedSelfPos = ctx.SelfPos;
        CachedConfig = ctx.Config;

        switch (_state)
        {
            case HitCooldownState.Caution:
                _stateTimer += dt;
                if (_stateTimer >= _cautionDuration)
                {
                    // Caution -> Probe
                    _state = HitCooldownState.Probe;
                    _stateTimer = 0f;
                    _recoveryDuration = ComputeRecoveryDuration(ctx);
                }
                break;

            case HitCooldownState.Probe:
                _stateTimer += dt;
                if (_stateTimer >= _recoveryDuration)
                {
                    // Probe -> Normal（完全恢复）
                    _state = HitCooldownState.Normal;
                    _stateTimer = 0f;
                }
                break;
        }
    }

    /// <summary>写入 FactorContext（§13.3 输出）。</summary>
    public void FillContext(ref FactorContext ctx)
    {
        ctx.CurrentState = _state;
        ctx.HitCount = _hitCount;

        // Caution 态威胁加成 + 任务折扣（让 HoldPosition 胜出，Idle 涌现）
        ctx.StateThreatBias = (_state == HitCooldownState.Caution) ? ctx.Config.stateThreatBias : 0f;
        ctx.StateTaskDiscount = (_state == HitCooldownState.Caution) ? ctx.Config.stateTaskDiscount : 1f;

        // Probe 态敏感度 ×1.5
        ctx.EffectiveSensitivity = (_state == HitCooldownState.Probe)
            ? ctx.Profession.threatSensitivity * ctx.Config.probeSensitivityBoost
            : ctx.Profession.threatSensitivity;
    }

    /// <summary>
    /// 返回当前应注入 L1 评分池的动态刺激源。
    /// Caution 态注入 HoldPositionStimulus（目标=当前 NPC 位置快照），Normal/Probe 返回空列表。
    /// 零 GC：返回缓存单元素列表，元素是池化实例，每 tick 仅更新 Position/Intensity 字段。
    /// </summary>
    public IReadOnlyList<IStimulus> GetActiveStimuli()
    {
        if (_state != HitCooldownState.Caution) return StimulusPool.Empty;

        // 更新池化实例字段（不 new）
        _holdStimulus.Position = CachedSelfPos;
        _holdStimulus.Intensity = CachedConfig != null ? CachedConfig.holdPositionIntensity : 0.6f;
        return _cachedHoldList;
    }

    // FillContext 在 Tick 后调用，但 GetActiveStimuli 需要当前位置/配置。
    // NPCBrain 调用顺序：Tick -> FillContext -> GetActiveStimuli，故缓存 Tick 时的 ctx。
    private Vector2 CachedSelfPos;
    private AttentionTuningConfig CachedConfig;

    // === 事件驱动（§13.3 路径 b）===

    /// <summary>受击事件：任意态 -> Caution 且 hitCount++。</summary>
    public void OnDamaged(in FactorContext ctx)
    {
        _state = HitCooldownState.Caution;
        _hitCount++;
        _stateTimer = 0f;
        _cautionDuration = ComputeCautionDuration(ctx);
        CachedSelfPos = ctx.SelfPos;
        CachedConfig = ctx.Config;
    }

    /// <summary>移动完成事件：Caution 计时起点（§13.3 关键：自 MoveComplete 起算非受击时刻）。</summary>
    public void OnMoveComplete()
    {
        if (_state == HitCooldownState.Caution)
            _stateTimer = 0f;  // 撤退完成才开始计 Caution 时长
    }

    // === 3.0.1_1 §6.4/6.5 公式 ===

    /// <summary>警戒时长（3.0.1_1 §6.4：courage 越低越长 + 血量越低越长）。</summary>
    private float ComputeCautionDuration(in FactorContext ctx)
    {
        // cautionDuration = base * (1 + (100-courage)/100) * (1 + (1-hpRatio)*0.5)
        int courage = ctx.Profession != null ? ctx.Profession.courage : 50;
        return ctx.Config.baseCautionTime
               * (1f + (100 - courage) / 100f)
               * (1f + (1f - ctx.HpRatio) * 0.5f);
    }

    /// <summary>
    /// 恢复时长（3.0.1_1 §6.5，按§5.1"以此为准"的修正方向）。
    /// recoveryDuration = base * (50/courage) * threatSensitivity
    /// sens 在分子：高敏感恢复更慢（符合§5.1勘误②修正方向，勿用 /sens 错误公式）。
    /// </summary>
    private float ComputeRecoveryDuration(in FactorContext ctx)
    {
        int courage = ctx.Profession != null ? ctx.Profession.courage : 50;
        float sens = ctx.Profession != null ? ctx.Profession.threatSensitivity : 1f;
        return ctx.Config.baseRecoveryTime
               * (50f / Mathf.Max(1, courage))
               * sens;
    }

    public void Reset()
    {
        _state = HitCooldownState.Normal;
        _hitCount = 0;
        _stateTimer = 0f;
        _cautionDuration = 0f;
        _recoveryDuration = 0f;
    }
}
