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
                // 3.0.1_3 §4.1 守阵追击 clamp：编队成员 MoveTowards 目标钳制在槽位 ± chaseRange 内
                // 威胁层压制切 MoveTowards 追击敌人时，不离开槽位 chaseRange 限制（占位 2 cell）
                if (ctx.HasFormationSlot)
                {
                    float chaseRangeWorld = ctx.Config.formationChaseRangeCells * cellSize;
                    cmd.TargetPos = ClampToSlotRange(cmd.TargetPos, ctx.FormationSlotWorld, chaseRangeWorld);
                }
                // 远程单位攻击距离保底：敌人进入 attackWorldRange 内就停（不走到脸上，让攻击系统 In-Range 自动开火）
                if (prof != null && prof.isRanged)
                {
                    float distToTarget = Vector2.Distance(ctx.SelfPos, cmd.TargetPos);
                    if (distToTarget < ctx.AttackWorldRange)
                        cmd.TargetPos = ctx.SelfPos;  // 停在原地，攻击系统自动开火
                }
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
                cmd.SlotOffset = posture.Focus.Focus is FollowStimulus fss ? fss.SlotOffset : Vector2Int.zero;
                cmd.KeepDistance = RetreatFormulas.FollowKeepDistance(
                    (int)ctx.ThreatLevel, ctx.Config.baseFollowCells,
                    ctx.Config.followScatterWeight, cellSize);
                cmd.Speed = walkSpeed;
                // 3.0.1_3：槽位化跟随时算 SlotWorld（锚点位置 + SlotOffset × cellSize）
                if (cmd.IsFormationSlot && cmd.Anchor != null)
                {
                    cmd.SlotWorld = (Vector2)cmd.Anchor.transform.position
                        + new Vector2(cmd.SlotOffset.x * cellSize, cmd.SlotOffset.y * cellSize);
                }
                break;

            case BehaviorModule.Idle:
                cmd.Duration = posture.Spectrum == BehaviorSpectrum.Cautious
                    ? RetreatFormulas.CautionDuration(prof.courage, ctx.HpRatio, ctx.Config.baseCautionTime)
                    : 0f;
                break;

            case BehaviorModule.Wander:  // 3.0.1_4 §6.3 漫游：中心=HomePoint，半径/停留由 Executor 管理
                cmd.TargetPos = posture.Focus.TargetPos;  // = HomePoint（漫游中心）
                cmd.Speed = walkSpeed;
                cmd.Duration = ctx.Config.wanderStayTime;
                cmd.WanderRadius = prof != null ? prof.wanderRadiusCells * cellSize : 2f * cellSize;
                break;
        }

        return cmd;
    }

    /// <summary>
    /// 守阵追击 clamp（§4.1）：将目标点钳制在槽位 ± chaseRange 矩形范围内。
    /// 1D 横版只钳 x 轴，y 保持目标原值（地面基线 -3 由 Executor MoveTowards 自行夹取）。
    /// </summary>
    private static Vector2 ClampToSlotRange(Vector2 target, Vector2 slotWorld, float chaseRangeWorld)
    {
        float clampedX = Mathf.Clamp(target.x, slotWorld.x - chaseRangeWorld, slotWorld.x + chaseRangeWorld);
        return new Vector2(clampedX, target.y);
    }
}
