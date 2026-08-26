using UnityEngine;

// ============================================================================
//  粒度切换（SimMode）判定（2_17 步骤8 骨架；步骤13 才落地真实判定）
//  P0 恒 Fine 占位：王国脑细模拟全量驱动，无无缝栅格切换。
//  步骤13 将在此处落地：活跃带覆盖→Fine（立即）/连续2日未覆盖→Abstract（迟滞）/
//  战斗锁强制 Fine（D333/D344）+ 休眠/唤起（实体常驻，D334）+ 存档记录。
//  SimMode 枚举属本系统，供 KingdomState.simMode 字段引用。
// ============================================================================

/// <summary>演算粒度（D333）。Fine=细模拟（实体驱动）；Abstract=抽象结算（纯 C# 日 tick）。</summary>
public enum SimMode : byte
{
    Fine = 0,      // 细模拟（王国脑 KingdomBrain 实体驱动）
    Abstract = 1   // 抽象结算（AbstractEconomySettler 纯 C#；P1 步骤14）
}

/// <summary>
/// 粒度切换管理器（2_17 步骤8 骨架；P0 恒 Fine）。
/// P0 单一职责：王国脑日 tick 前查询本王国演算粒度挂细模拟。
/// </summary>
public class SimModeManager : Singleton<SimModeManager>
{
    /// <summary>查询某王国日 tick 演算粒度。P0 恒 Fine（步骤13 接真实判定）。</summary>
    public SimMode GetMode(int kingdomId) => SimMode.Fine;
}