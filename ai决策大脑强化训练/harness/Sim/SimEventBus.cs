// ============================================================================
//  M2 Headless 模拟器 - SimEventBus 事件总线
//  04_模拟器规格.md §八：JSONL 事件（unit_died/spectrum/retreat/formation_intent/tick/attack）。
//  sim 版事件总线 = 发布-订阅（对应 Unity EventBus 的最小子集，仅 sim 内部需要的 6 类事件）。
//  SimDamage 发布受击/死亡 -> SimWorld/编队/指标消费；SimWorld 发布谱系/撤退/军令/tick 采样。
// ============================================================================

using System;

/// <summary>单位死亡事件（DamageSystem.OnUnitDied 链 + 编队减员 + 移除）。</summary>
public sealed class SimUnitDiedEvent
{
    public SimUnit Unit;
    public SimUnit Killer;
}

/// <summary>单位受击事件（节流后发布，NPCBrain.OnDamaged 消费：HitCooldown + 受击溯源）。</summary>
public sealed class SimUnitDamagedEvent
{
    public SimUnit Victim;
    public SimUnit Source;
    public int Damage;
}

/// <summary>谱系切换事件（切换历史，SimWorld 每 tick 检查后发布）。</summary>
public sealed class SimSpectrumEvent
{
    public SimUnit Unit;
    public BehaviorSpectrum From;
    public BehaviorSpectrum To;
}

/// <summary>撤退事件（谱系 4 触发时发布；kind=tactical/strategic，reason=hitCount/heat）。</summary>
public sealed class SimRetreatEvent
{
    public SimUnit Unit;
    public bool IsTactical;
    public string Reason;
}

/// <summary>编队意图事件（剧本 SetIntent / v1 DecideAutoIntent 发布）。</summary>
public sealed class SimFormationIntentEvent
{
    public int Gid;
    public TacticIntent Intent;
    public float Heat;
    public float Value;
}

/// <summary>放弃追击事件（AbandonTaskFactor > threshold 边沿触发，指标统计）。</summary>
public sealed class SimAbandonChaseEvent
{
    public SimUnit Unit;
}

/// <summary>编队解散事件（将军阵亡 -> DisbandAll，M3 D4 破阵指标；AI.Core 零改动，Sim 层发布）。</summary>
public sealed class SimFormationBreakEvent
{
    public int Gid;
    public float Time;
}

/// <summary>
/// sim 事件总线（Unity EventBus 最小子集）。
/// 事件顺序 = 发布顺序（tick 内固定顺序，04 §二 8 步循环），确定性依赖发布方。
/// </summary>
public sealed class SimEventBus
{
    public event Action<SimUnitDiedEvent> UnitDied;
    public event Action<SimUnitDamagedEvent> UnitDamaged;
    public event Action<SimSpectrumEvent> Spectrum;
    public event Action<SimRetreatEvent> Retreat;
    public event Action<SimFormationIntentEvent> FormationIntent;
    public event Action<SimAbandonChaseEvent> AbandonChase;
    public event Action<SimFormationBreakEvent> FormationBreak;

    public void Publish(SimUnitDiedEvent e) => UnitDied?.Invoke(e);
    public void Publish(SimUnitDamagedEvent e) => UnitDamaged?.Invoke(e);
    public void Publish(SimSpectrumEvent e) => Spectrum?.Invoke(e);
    public void Publish(SimRetreatEvent e) => Retreat?.Invoke(e);
    public void Publish(SimFormationIntentEvent e) => FormationIntent?.Invoke(e);
    public void Publish(SimAbandonChaseEvent e) => AbandonChase?.Invoke(e);
    public void Publish(SimFormationBreakEvent e) => FormationBreak?.Invoke(e);
}
