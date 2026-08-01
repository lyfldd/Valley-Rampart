using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - L2 姿态裁决层
//  详见 3.0.1_2_输入输出决定层设计.md §4
//  谱系 × 焦点类型 × 到达态 三维裁决表 + §4.5 双撤退子裁决
// ============================================================================

/// <summary>
/// L2 姿态裁决层（§4，纯计算无副作用）。
/// 输入：焦点(L1) + 威胁评定 + 保护因子 + 性格因子 + 到达态
/// 输出：谱系等级 -> 行为模块 + 参数来源（PostureDecision）
///
/// 三维裁决表（§4.2，仅谱系 0/2/4）：
///   谱系4 -> RetreatMove + §4.5子裁决
///   谱系0/2 + Anchor -> FollowAnchor
///   谱系0/2 + Position + 未到达 -> MoveTowards
///   谱系0/2 + Position + 已到达 -> Idle
///   谱系0/2 + WorkPosition + 已到达 -> WorkAt
///   谱系0/2 + HomePosition + 已到达 -> Idle
/// </summary>
public static class L2PostureDecider
{
    public static PostureDecision Decide(in FactorContext ctx)
    {
        var posture = new PostureDecision { Focus = ctx.FocusDecision };

        // === 1. 谱系裁决（复用 TradeoffSystem.CalculateSpectrum 逻辑）===
        posture.Spectrum = CalculateSpectrum(in ctx);

        // === 2. 模块选择（§4.2 三维表）===
        if (posture.Spectrum == BehaviorSpectrum.FullRetreat)
        {
            // 谱系 4：RetreatMove + §4.5 双撤退子裁决
            posture.Module = BehaviorModule.RetreatMove;
            DecideRetreatSubtype(in ctx, ref posture);
        }
        else
        {
            // 谱系 0/2：按焦点类型 × 到达态选模块
            posture.Module = SelectModuleByFocus(in ctx);
        }

        return posture;
    }

    /// <summary>
    /// 谱系计算（复用 TradeoffSystem.CalculateSpectrum 公式）。
    /// 威胁 0/1 -> FullPower；威胁 2 有保护 -> Cautious / 无保护 -> FullRetreat；
    /// 威胁 3 -> 按撤退阈值（高阈值职业如士兵扛住 -> Cautious，否则 FullRetreat）。
    /// </summary>
    private static BehaviorSpectrum CalculateSpectrum(in FactorContext ctx)
    {
        var profession = ctx.Profession;
        var config = ctx.Config;
        ThreatLevel threatLevel = ctx.ThreatLevel;

        // 撤退阈值公式（3.0.1 §4.2）：
        // threshold = base + priorityBonus + (courage-50)/50 + (obedience-50)/100 + offset
        float threshold = config.retreatThresholdBase;
        // 注：P0 无任务系统，priorityBonus 暂不加（GetCurrentTaskPriority 返回 null）
        threshold += (profession.courage - 50f) / 50f;       // 勇气加成 -1~+1
        threshold += (profession.obedience - 50f) / 100f;    // 服从度加成 -0.5~+0.5
        threshold += profession.retreatThresholdOffset;       // 职业偏移

        float threatValue = (float)threatLevel;

        // 威胁 0/1 -> 全力执行
        if (threatLevel <= ThreatLevel.Alert)
            return BehaviorSpectrum.FullPower;

        // 威胁 2（危险）
        if (threatLevel == ThreatLevel.Danger)
        {
            if (threatValue > threshold)
                return BehaviorSpectrum.FullRetreat;
            if (ctx.HasProtection)
                return BehaviorSpectrum.Cautious;
            return BehaviorSpectrum.FullRetreat;
        }

        // 威胁 3（致命）
        if (threatValue <= threshold)
            return BehaviorSpectrum.Cautious;  // 高阈值职业（S级军令）即使致命也扛住
        return BehaviorSpectrum.FullRetreat;
    }

    /// <summary>
    /// §4.5 双撤退子裁决（三规则，顺序即优先级）。
    /// ① hitCount ≥ maxHitCount -> 战略撤退 HomePoint（被打怕了，无条件回家）
    /// ② 有活跃 ThreatStimulus -> 战术短撤（方向 + retreatDistance，先脱离）
    /// ③ 无 ThreatStimulus 但威胁超阈 -> 战略撤退 HomePoint（纯环境推高如夜晚）
    /// </summary>
    private static void DecideRetreatSubtype(in FactorContext ctx, ref PostureDecision posture)
    {
        int maxHitCount = ctx.Profession != null ? ctx.Profession.maxHitCount : 3;

        // ① hitCount ≥ maxHitCount -> 战略回城
        if (ctx.HitCount >= maxHitCount)
        {
            posture.IsTacticalRetreat = false;
            posture.MoveTarget = ctx.HomePoint;
            posture.TacticalRetreatEnemy = null;
            return;
        }

        // ② 有活跃 ThreatStimulus -> 战术短撤（受击反方向）
        IDamageable enemy = FindActiveThreatEnemy(in ctx);
        if (enemy != null && !IsDestroyed(enemy))
        {
            posture.IsTacticalRetreat = true;
            // 战术方向来源：活跃 ThreatStimulus.enemy 位置反方向（单位向量）
            posture.MoveTarget = (ctx.SelfPos - enemy.GetPosition()).normalized;
            posture.TacticalRetreatEnemy = enemy;
            return;
        }

        // ③ 无 ThreatStimulus 但威胁超阈 -> 战略回城（环境推高如夜晚）
        posture.IsTacticalRetreat = false;
        posture.MoveTarget = ctx.HomePoint;
        posture.TacticalRetreatEnemy = null;
    }

    /// <summary>
    /// 销毁防御：IDamageable 接口变量的 !=null 是普通引用比较，不触发 Unity 的 Object==null 销毁检查。
    /// 需先 as UnityEngine.Object 再判空，才能拦截已销毁（但引用未清空）的 UnitController 等组件。
    /// </summary>
    private static bool IsDestroyed(IDamageable d)
    {
        var uo = d as UnityEngine.Object;
        return uo == null;  // UnityEngine.Object==null 触发销毁检测
    }

    /// <summary>
    /// 查找活跃 ThreatStimulus 的敌人引用（供战术短撤算方向）。
    /// P0：从 ctx 的附近敌人列表取最近的一个（ThreatStimulus 在 L1 评分时已注入）。
    /// </summary>
    private static IDamageable FindActiveThreatEnemy(in FactorContext ctx)
    {
        // L1 焦点是威胁层且有 Enemy 引用（ts.Enemy 的 !=null 同样不触发销毁检测，需 IsDestroyed 防御）
        if (ctx.FocusDecision.IsValid && ctx.FocusDecision.Focus is ThreatStimulus ts
            && ts.Enemy != null && !IsDestroyed(ts.Enemy))
            return ts.Enemy;

        // 焦点非威胁但附近有敌人（威胁层压制下焦点可能是任务层，但威胁源仍存在）
        // P0 简化：通过 Self 查最近敌人（NPCBrain 在 ctx 里已有 NearestEnemyDist，但无 enemy ref）
        // 实际战术短撤场景：刚中箭 -> ThreatStimulus 是焦点 -> 走上面分支
        // 若威胁非焦点（如夜晚环境推高），走 ③ 战略回城，正确
        return null;
    }

    /// <summary>
    /// 谱系 0/2 下按焦点类型 × 到达态选模块（§4.2 三维表）。
    /// </summary>
    private static BehaviorModule SelectModuleByFocus(in FactorContext ctx)
    {
        FocusDecision focus = ctx.FocusDecision;
        if (!focus.IsValid)
            return BehaviorModule.Idle;  // 无焦点兜底

        switch (focus.Type)
        {
            case FocusType.Anchor:
                return BehaviorModule.FollowAnchor;  // 永不到达，持续跟随

            case FocusType.Position:
                return ctx.ArrivedAtFocus ? BehaviorModule.Idle : BehaviorModule.MoveTowards;

            case FocusType.WorkPosition:
                return ctx.ArrivedAtFocus ? BehaviorModule.WorkAt : BehaviorModule.MoveTowards;

            case FocusType.HomePosition:
                return ctx.ArrivedAtFocus ? BehaviorModule.Idle : BehaviorModule.MoveTowards;

            case FocusType.Wander:  // 3.0.1_4 §6.3 漫游：Executor 持续取点循环
                return BehaviorModule.Wander;

            default:
                return BehaviorModule.Idle;
        }
    }
}
