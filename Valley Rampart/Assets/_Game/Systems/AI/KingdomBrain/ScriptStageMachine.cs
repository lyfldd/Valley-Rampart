using System;
using UnityEngine;

// ============================================================================
//  剧本四阶段状态机（2_17 步骤8，D317/D318/D319/D349）
//  存活 → 发育 → 扩张 → 军事，**单向不回退**（D318）；
//  统一起步于存活期、每日最多升荤级（D319）；推进 = 阈值 + 最小停留（D317）。
//  阈值全部落 KingdomBrainConfig（SO，纯规则不训）。
//
//  本机只做"判定 + 阶段标签"，不碰实体派生/国库读写——所有判定所需指标由调用方
//  以 ScriptStageContext 快照喂入（KingdomBrain 从 KingdomState 派生真实值；冒烟可注入）。
//  纯 C# 无 Unity 引用，确定性强、可单测。
// ============================================================================

/// <summary>剧本四阶段（D317）。单向不回退的枚举序即权威序。</summary>
public enum ScriptStage : byte
{
    Survive = 0,   // 存活期（统一起步，D319）
    Develop = 1,   // 发育期
    Expand  = 2,   // 扩张期
    Military = 3   // 军事期
}

/// <summary>
/// 剧本推进判定所需快照（探针集合，不缓存、不读写真源）。
/// 全部字段按"王国脑日 tick 那一刻"采集，保证同 seed 确定性；供 KingdomBrain 派生与冒烟注入共用。
/// </summary>
public struct ScriptStageContext
{
    // 存活→发育（D317：人均有房 + 粮储≥3日消耗 + 无失业，连续2日）
    public bool housedAll;            // 人均有房（P0 房指标未接入，KingdomBrain 按 SO 占位传）
    public bool grainDaysOk;          // 粮储 ≥ 存活阈值日消耗布尔（KingdomBrain 折算好并传入）
    public bool unemployedOk;         // 无失业（P0 任务统计未接入，KingdomBrain 按占位传 true）

    // 发育→扩张（D317：工人≥8 + 产能建筑≥3 + 连续3日净流入正）
    public int workerCount;           // 本王国工人数
    public int capacityCount;         // 本王国产能建筑数（P0 近似：活跃建筑数）
    public bool netInflowPositive;    // 当日资源净流入为正（P0 占位 true）

    // 扩张→军事（D317/D349：战士≥4 + 人口≥12 + 扩张占区≥2 中区块）
    public int warriorCount;          // 本王国战士数（守备线 D348）
    public int populationCount;       // 本王国总人口（工人+战士，P0 口径）
    public int expansionChunks;       // 本王国领土中区块总数（D349 P0 近似）
}

/// <summary>
/// 剧本四阶段状态机（2_17 步骤8）。
/// 纯判定器：持内部连续计数（housedStreak / netInflowStreak）+ 当前阶段 + 阶段内停留天数。
/// Tick 一次最多升荤级（D319，else-if 单支推进）；军事期不降级（D318 单向）。
/// </summary>
public class ScriptStageMachine
{
    /// <summary>当前阶段（Singleton 起步存活，D319）。</summary>
    public ScriptStage Stage { get; private set; } = ScriptStage.Survive;

    /// <summary>当前阶段内已停留天数（升级时清零）。</summary>
    public int StageDayCounter { get; private set; }

    /// <summary>存活阈值三条件连续达标天数（防瞬态抖动，D317 连续2日）。</summary>
    public int HousedStreak { get; private set; }

    /// <summary>资源净流入为正的连续天数（D317 连续3日）。</summary>
    public int NetInflowStreak { get; private set; }

    /// <summary>
    /// 每日推进一档（最多升荤级，D319；单向不回退，D318）。
    /// </summary>
    /// <returns>是否升级（true=当日跨入更高阶段；顶层借此同步 KingdomState.scriptPhase）。</returns>
    public bool Tick(ScriptStageContext ctx, KingdomBrainConfig cfg)
    {
        if (cfg == null) return false;

        // 连续计数每日累计（任何阶段都累计，供各阶段阈值消费；未满自动清零前缀）
        bool housedOk = ctx.housedAll && ctx.grainDaysOk && ctx.unemployedOk;
        HousedStreak = housedOk ? HousedStreak + 1 : 0;
        NetInflowStreak = ctx.netInflowPositive ? NetInflowStreak + 1 : 0;

        StageDayCounter++;

        switch (Stage)
        {
            case ScriptStage.Survive:
                if (StageDayCounter >= cfg.surviveMinDays
                    && HousedStreak >= cfg.surviveToDevelop_streakDays)
                    return Advance(ScriptStage.Develop);
                break;

            case ScriptStage.Develop:
                if (StageDayCounter >= cfg.developMinDays
                    && ctx.workerCount >= cfg.developToExpand_workersMin
                    && ctx.capacityCount >= cfg.developToExpand_capacityMin
                    && NetInflowStreak >= cfg.developToExpand_netInflowStreak)
                    return Advance(ScriptStage.Expand);
                break;

            case ScriptStage.Expand:
                if (StageDayCounter >= cfg.expandMinDays
                    && ctx.warriorCount >= cfg.expandToMilitary_warriorsMin
                    && ctx.populationCount >= cfg.expandToMilitary_populationMin
                    && ctx.expansionChunks >= cfg.expandToMilitary_expansionChunks)
                    return Advance(ScriptStage.Military);
                break;

            case ScriptStage.Military:
                // D318 单向不回退：军事期不再降级
                break;
        }
        return false;
    }

    /// <summary>升级到更高阶段（重置停留计数；单向提升）。</summary>
    private bool Advance(ScriptStage next)
    {
        if (next <= Stage) return false;   // 单向不退
        Stage = next;
        StageDayCounter = 0;
        return true;
    }

    /// <summary>阶段名（日志/冒烟断言用）。</summary>
    public static string Name(ScriptStage s) =>
        s switch
        {
            ScriptStage.Survive => "存活",
            ScriptStage.Develop => "发育",
            ScriptStage.Expand => "扩张",
            ScriptStage.Military => "军事",
            _ => "?"
        };
}