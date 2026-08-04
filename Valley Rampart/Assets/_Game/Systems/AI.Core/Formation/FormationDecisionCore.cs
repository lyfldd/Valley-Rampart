// ============================================================================
//  AI.Core Formation - 军队级决策纯函数（M1 决策核提取·步5 降级方案）
//  03_大脑提取与双适配工程.md 迁移 6 步·步5：
//  原 FormationBrain 的 EvaluateTaskValue + 意图自决判定抽成核内可测纯函数；
//  MonoBehaviour 部分（决策节奏/输入采集/控制器副作用 SetIntent/SetAdvanceTarget）留壳。
//  ⚠️ 取舍说明：AssignSlots 槽位分配算法未搬入核（深耦合 FormationDef SO + SlotRole/SlotDef
//     struct + FormationMember 壳类型），留壳，列为 P1 项。
// ============================================================================

/// <summary>
/// 意图自决结果（§4.4）。
/// Valid=false = 维持现状（rule ⑤ 低热度，不频繁切换）。
/// </summary>
public struct FormationIntentDecision
{
    /// <summary>目标意图</summary>
    public TacticIntent Intent;
    /// <summary>rule ② 支援：需同时 SetAdvanceTarget（朝热点推进）</summary>
    public bool ShouldAdvance;
    /// <summary>false = 维持现状（不 SetIntent）</summary>
    public bool Valid;
}

/// <summary>
/// 军队级决策纯函数（3.0.1_5 §4.1/§4.2/§4.4，零引擎依赖）。
/// 壳 FormationBrain.Decide 调用，行为与迁移前一致（秒级决策，SetIntent 自带防抖节流）。
/// </summary>
public static class FormationDecisionCore
{
    /// <summary>
    /// 任务价值动态评估（§4.2）：锚点类型定基础值 + 动态修正。
    /// 基础值：守城编队中（0.5）/ 将军有推进目标=攻城中高（0.8）/ 无目标=巡逻低（0.2）——TuningSnapshot 可调。
    /// 动态：敌压近升价值（战斗紧迫），残编降价值（保命优先）。
    /// </summary>
    public static float EvaluateTaskValue(
        bool isGarrison,
        bool hasAdvanceTarget,
        float heat,
        float survival,
        in TuningSnapshot config,
        float survivalRetreatGate)
    {
        float baseValue;
        if (isGarrison)
            baseValue = config.taskValueGarrison;    // 守城中：固守待敌，被打狠才撤
        else if (hasAdvanceTarget)
            baseValue = config.taskValueAttack;      // 攻城中：带伤推进，个体撤退阈值被编队抵抗抬高
        else
            baseValue = config.taskValuePatrol;      // 巡逻/待命：一触即撤，不恋战

        if (heat > config.taskValueHeatBoostGate) baseValue += config.taskValueHeatBoost;
        if (survival < survivalRetreatGate) baseValue -= config.taskValueSurvivalPenalty;
        return MathfX.Clamp01(baseValue);
    }

    /// <summary>
    /// 意图自决（§4.4 防看戏，规则顺序即优先级）：
    ///   ① 残编 + 被压 → 撤退（先保住有生力量）
    ///   ② 远处战斗热点 + 本地无激战 → 支援（朝热点推进，ShouldAdvance=true）
    ///   ③ 高价值 + 敌压近 → 冲锋压上（军队敢承受代价）
    ///   ③.5 守城 + 城墙健康 + 敌压近且近身 → 出城迎战（3.7 §4.3 Sally，ShouldAdvance=true）
    ///   ④ 敌接近 → 防守
    ///   ⑤ 低热度 → 维持现状（Valid=false，不频繁切换）
    /// </summary>
    public static FormationIntentDecision DecideIntent(
        float heat,
        float survival,
        float value,
        bool hasRemoteHotspot,
        bool isGarrison,
        float wallHpRatio,
        float enemyDist,
        float heatEngage,
        float heatCharge,
        float survivalRetreatGate,
        float chargeValueGate,
        float sallyWallHpGate,
        float sallyEnemyDistGate)
    {
        // ① 残编 + 被压 → 撤退
        if (survival < survivalRetreatGate && heat > heatEngage)
            return new FormationIntentDecision { Intent = TacticIntent.Retreat, Valid = true };

        // ② 远处战斗热点 + 本地无激战 → 支援（编队 B 支援编队 A：朝热点推进）
        if (hasRemoteHotspot && heat < heatEngage)
            return new FormationIntentDecision { Intent = TacticIntent.Charge, ShouldAdvance = true, Valid = true };

        // ③ 高价值 + 敌压近 → 冲锋压上（任务价值高，个体撤退被军令压住）
        if (heat > heatCharge && value > chargeValueGate)
            return new FormationIntentDecision { Intent = TacticIntent.Charge, Valid = true };

        // ③.5 3.7 §4.3 守城出城迎战：城墙健康 + 敌压近且近身 → Sally（出城压上，朝敌推进）
        if (isGarrison && wallHpRatio >= sallyWallHpGate && enemyDist <= sallyEnemyDistGate && heat > heatEngage)
            return new FormationIntentDecision { Intent = TacticIntent.Sally, ShouldAdvance = true, Valid = true };

        // ④ 敌接近 → 防守
        if (heat > heatEngage)
            return new FormationIntentDecision { Intent = TacticIntent.Defense, Valid = true };

        // ⑤ 低热度 → 维持现状（不频繁切换）
        return default;
    }
}
