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
    /// 谱系计算（3.0.1_8 §4 分层仲裁：连续因子仲裁，不查 4 档表）。
    /// 输入：ThreatFactor（连续 0-1）vs FormationFactor（连续 0-1）+ 撤退阈值（职业性格）。
    /// 仲裁逻辑（连续比较 + 末位离散，非合成总分）：
    ///   ① ThreatFactor 低（< 警戒线 0.3）-> FullPower（无威胁全力执行）
    ///   ② 有效威胁 = ThreatFactor × (1 - 协作抵抗)，协作抵抗来自编队军令（服从度越高越扛）
    ///   ③ 有效威胁 > 撤退阈值 -> FullRetreat；否则 Caution（有编队/性格扛住）
    /// 谱系是仲裁的结果而非输入；量化器 4 档保留给攻击链路/调试，不参与谱系判定。
    /// ⚠️ 量纲换算：撤退阈值是旧档位量纲（0-3，threatLevel 档位），ThreatFactor 是连续量纲（0-1），
    ///    统一用 /3 换算到 0-1（3.0.1_8 §2.3 量化器再定位：连续仲裁不再消费档位）。
    /// </summary>
    private static BehaviorSpectrum CalculateSpectrum(in FactorContext ctx)
    {
        var profession = ctx.Profession;
        var config = ctx.Config;
        float threat = ctx.ThreatFactor;        // 连续 0-1（3.0.1_8 威胁因子）
        float formation = ctx.FormationFactor;  // 连续 0-1（3.0.1_8 协作因子）

        // 撤退阈值（职业性格基线）→ 档位量纲换算到连续量纲（/3）
        // threshold = base + (courage-50)/50 + (obedience-50)/100 + offset
        float threshold = config.retreatThresholdBase;
        threshold += (profession.courage - 50f) / 50f;       // 勇气加成 -1~+1
        threshold += (profession.obedience - 50f) / 100f;    // 服从度加成 -0.5~+0.5
        threshold += profession.retreatThresholdOffset;       // 职业偏移
        threshold = Mathf.Max(0.2f, threshold / 3f);          // 连续量纲，保底防除零

        // ① 放弃任务（3.0.1_8 §六）：追击成本 > 收益 → Cautious（放弃追击，回归编队/守位，不追不撤）
        //    放最前：被风筝追不上时威胁因子低（敌人远），低威胁分支会误判 FullPower 继续追
        if (ctx.AbandonTaskFactor > config.abandonThreshold)
            return BehaviorSpectrum.Cautious;

        // ② 低威胁 -> 全力执行（原威胁 0/1）
        if (threat < 0.3f)
            return BehaviorSpectrum.FullPower;

        // ③ 连续仲裁：有效威胁 = 威胁因子 × (1 - 协作抵抗 - 工作抵抗)
        // 协作因子（编队军令）作为"扛住"系数：军令越强、服从度越高，越不易被威胁压垮
        // 工作因子（3.0.1_8 §八）：正在干关键活（任务投入高）→ 抗打断（不会被摸一下就撤）
        // 这是"指挥优先级 vs 战斗本能"的连续权衡，而非 4 档二选一
        float formationResist = formation * (0.5f + profession.obedience / 100f);  // 0~1.5
        float workResist = ctx.WorkFactor * config.workResistScale;                // 0~0.5
        float resistTotal = Mathf.Min(0.95f, formationResist * 0.4f + workResist); // 抗性封顶防负
        float effectiveThreat = threat * (1f - resistTotal);                       // 编队+工作最高抵消 95%

        // ④ 撤退 AND 归巢门控（3.0.1_8 §五）：编队成员需归巢驱力强才真撤（军队承受更多代价）
        //    非编队个体（工人/散兵/敌方）维持原行为：威胁超阈即撤
        bool retreatAllowed = !ctx.HasFormationSlot || ctx.SafetyFactor > config.safetyRetreatGate;
        if (effectiveThreat > threshold && retreatAllowed)
            return BehaviorSpectrum.FullRetreat;
        return BehaviorSpectrum.Cautious;  // 有编队/性格扛住 -> 谨慎（维持工作/守阵）
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
