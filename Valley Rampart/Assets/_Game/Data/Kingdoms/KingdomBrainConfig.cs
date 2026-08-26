using System;
using UnityEngine;

// ============================================================================
//  王国脑（KingdomBrain）数值配置（2_17 步骤8，D317~D320/D322/D349 阈值落点；占位可调）
//  剧本四阶段状态机阈值 + 最小停留 + 每日升级上限 + 焦点防抖 + 常设底线 全落 SO
//  （so-data-driven 铁律，禁硬编码魔法数；阈值纯规则不训，D317）。
//  资产路径：Resources/Config/Kingdoms/KingdomBrainConfig.asset
// ============================================================================

/// <summary>
/// 王国脑数值配置（2_17 步骤8）。
/// 服务：ScriptStageMachine 四阶段推进（D317~D320/D349）+ FocusController 防抖与常设底线（D322）。
/// 占位默认值对齐 2_17 实施计划 §三 SO 表 + HH.24 §三 全量落点。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/Kingdoms/KingdomBrainConfig", fileName = "KingdomBrainConfig")]
public class KingdomBrainConfig : ScriptableObject
{
    [Header("最小停留（D317占位）")]
    [Tooltip("存活期最小停留天数，取整日；未满不升发育期")]
    public int surviveMinDays = 2;
    [Tooltip("发育期最小停留天数；未满不升扩张期")]
    public int developMinDays = 3;
    [Tooltip("扩张期最小停留天数；未满不升军事期")]
    public int expandMinDays = 3;

    [Header("存活→发育（D317：人均有房 + 粮储≥3日消耗 + 无失业，连续2日）")]
    [Tooltip("人均有房（P0 无住房统计，占位恒视为达标的真源探针；有指标后接入）")]
    public bool surviveToDevelop_housedAll = true;
    [Tooltip("粮储必须可持续天数（粮储 ≥ 本值 × 人口 × perPop 视为粮裕；与常设底线同口径粮）")]
    public int surviveToDevelop_grainDays = 3;
    [Tooltip("无失业（P0 无任务统计，占位真源探针）")]
    public int surviveToDevelop_unemployedMax = 0;
    [Tooltip("三条件连续达标天数（连续达标才推进，防瞬态抖动）")]
    public int surviveToDevelop_streakDays = 2;

    [Header("发育→扩张（D317：工人≥8 + 产能建筑≥3 + 连续3日净流入正）")]
    [Tooltip("工人数下限")]
    public int developToExpand_workersMin = 8;
    [Tooltip("产能建筑数下限（P0 占位：以本王国城堡+产能建筑估算）")]
    public int developToExpand_capacityMin = 3;
    [Tooltip("资源净流入为正的连续天数")]
    public int developToExpand_netInflowStreak = 3;

    [Header("扩张→军事（D317/D349：战士≥4 + 人口≥12 + 扩张占区≥2中区块）")]
    [Tooltip("战士数下限（守备线 D348）")]
    public int expandToMilitary_warriorsMin = 4;
    [Tooltip("人口数下限")]
    public int expandToMilitary_populationMin = 12;
    [Tooltip("扩张占区≥N 中区块（D349：初始领土外新纳；P0 以领土中区块总数近似）")]
    public int expandToMilitary_expansionChunks = 2;

    [Header("升级节奏（D319）")]
    [Tooltip("单日最多升荤级数（D319：一律1）")]
    public int maxStageUpPerDay = 1;

    [Header("焦点（D322）")]
    [Tooltip("焦点切换防抖最小持续天数（≥3日占位）")]
    public int focusMinDurationDays = 3;

    [Header("常设底线（D322，触发式不评分）")]
    [Tooltip("粮储低于 N 日消耗强制屯粮（粮储 < N × 人口 × perPop 时触发）")]
    public int grainReserveDaysFloor = 2;
    [Tooltip("每人口每日本人消耗粮估算（占位分母；对人口×日为防线阈值）")]
    public int grainConsumptionPerPop = 1;
}