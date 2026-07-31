using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - 记忆组件群接口
//  详见 3.0.1_2_输入输出决定层设计.md §1.1 / §9
//  原则一·两类东西：纯计算管线（L1/L2/L3 无状态）+ 输入侧记忆组件群（有状态、需存档）
//  新增任何有状态的东西（疲劳/士气/信任度…）默认归入记忆组件群，管线永不被污染
// ============================================================================

/// <summary>
/// 记忆组件群统一接口（§9）。
/// 实现类：HitCooldownStateMachine / ThreatHysteresisComponent / ProtectionHysteresisComponent。
/// NPCBrain 每 tick「先记忆后管线」foreach 钉死秩序：
///   ① Tick(更新自身状态) -> ② FillContext(写入 FactorContext) + GetActiveStimuli(注入动态刺激源)
/// </summary>
public interface IMemoryComponent
{
    /// <summary>
    /// 更新自身状态（时长/事件驱动）。
    /// 由 NPCBrain 在管线前调用。量化器读 ctx.LastRaw（上一帧管线产物缓存）。
    /// </summary>
    void Tick(float dt, in FactorContext ctx);

    /// <summary>
    /// 写入 FactorContext 供管线只读。
    /// 由 NPCBrain 在 Tick 后、管线前调用。
    /// </summary>
    void FillContext(ref FactorContext ctx);

    /// <summary>
    /// 返回当前应注入 L1 评分池的动态刺激源（空=不注入）。
    /// 零 GC 落地纪律：实现内部持有长期实例复用、返回缓存列表（不每 tick new）。
    /// Caution 态返回 HoldPositionStimulus，Normal/Probe 返回共享空列表。
    /// </summary>
    IReadOnlyList<IStimulus> GetActiveStimuli();

    /// <summary>重置状态（对象池复用时调）</summary>
    void Reset();
}
