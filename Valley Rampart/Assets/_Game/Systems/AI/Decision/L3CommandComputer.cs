using UnityEngine;

// ============================================================================
//  3.0.1_2 输入输出决定层 - L3 参数计算层
//  详见 3.0.1_2_输入输出决定层设计.md §5
//  复用 ThreatAssessor.CalculateRawFactor + 新公式 -> BehaviorCommand
// ============================================================================

/// <summary>
/// L3 参数计算层（§5，纯计算无副作用）。
/// 只算数值，不做任何选择。公式全部来自 3.0.1_1 + 3.0.1_2 §5.2，集中可查。
///
/// ① rawFactor（复用 ThreatAssessor.CalculateRawFactor）
/// ② 根据 PostureDecision 算 BehaviorCommand 的速度/距离/时长等连续参数
/// </summary>
public static class L3CommandComputer
{
    /// <summary>
    /// 计算 BehaviorCommand。
    /// 调用前 ctx.RawFactor 已由 NPCBrain 算好（含 stateThreatBias 处理）。
    /// </summary>
    public static BehaviorCommand Compute(in PostureDecision posture, in FactorContext ctx)
    {
        var cmd = new BehaviorCommand { Module = posture.Module };
        var prof = ctx.Profession;
        float walkSpeed = prof != null ? prof.walkSpeed : 5f;
        float cellSize = ctx.CellSize;

        switch (posture.Module)
        {
            case BehaviorModule.MoveTowards:
                cmd.TargetPos = posture.Focus.TargetPos;
                cmd.Speed = walkSpeed;
                break;

            case BehaviorModule.RetreatMove:
                if (posture.IsTacticalRetreat)
                {
                    // 战术短撤：Direction + Distance（Executor 走撞墙停语义）
                    cmd.Direction = posture.MoveTarget;  // 已是单位向量
                    cmd.Distance = RetreatFormulas.RetreatDistance(
                        prof.courage, ctx.EffectiveSensitivity, ctx.HitCount,
                        ctx.Config.baseRetreatCells, ctx.Config.stepRetreatCells, cellSize);
                    // TargetPos 仅供调试面板显示预期落点（Executor 不用）
                    cmd.TargetPos = ctx.SelfPos + cmd.Direction * cmd.Distance;
                }
                else
                {
                    // 战略撤退：TargetPos = HomePoint
                    cmd.TargetPos = posture.MoveTarget;  // = HomePoint
                }
                cmd.Speed = RetreatFormulas.RetreatSpeed(prof.courage, walkSpeed);
                break;

            case BehaviorModule.WorkAt:
                cmd.TargetPos = posture.Focus.TargetPos;
                cmd.Speed = walkSpeed;
                cmd.Duration = 0f;  // P0 WorkAt 占位：位移即目的，无工作时长
                break;

            case BehaviorModule.FollowAnchor:
                cmd.Anchor = posture.Focus.Focus is FollowStimulus fs ? fs.Anchor : null;
                cmd.KeepDistance = RetreatFormulas.FollowKeepDistance(
                    (int)ctx.ThreatLevel, ctx.Config.baseFollowCells,
                    ctx.Config.followScatterWeight, cellSize);
                cmd.Speed = walkSpeed;
                break;

            case BehaviorModule.Idle:
                cmd.Duration = posture.Spectrum == BehaviorSpectrum.Cautious
                    ? RetreatFormulas.CautionDuration(prof.courage, ctx.HpRatio, ctx.Config.baseCautionTime)
                    : 0f;
                break;
        }

        return cmd;
    }
}
