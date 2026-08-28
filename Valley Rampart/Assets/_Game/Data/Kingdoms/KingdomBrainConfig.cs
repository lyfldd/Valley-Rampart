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

    [Header("常设底线（D322，触发式不评分；执行序=粮→人口→被攻击 三级底线）")]
    [Tooltip("粮储低于 N 日消耗强制屯粮（粮储 < N × 人口 × perPop 时触发；底线第一级「保命」）")]
    public int grainReserveDaysFloor = 2;
    [Tooltip("每人口每日本人消耗粮估算（占位分母；对人口×日为防线阈值）")]
    public int grainConsumptionPerPop = 1;
    [Tooltip("人口底线（自造件，贴既有哲学）：workerCount < popFloor 强制⑥招工人焦点（底线第二级「保增长下限」；触发式不评分跳防抖）。修复决策①结构性锁死（低扩张国⑥评分被轴乘入压制永不招工）。")]
    public int popFloor = 6;
    [Tooltip("人口底线份额式修正（策划纠偏裁决）：⑥连续占焦点天数。popAlarm 触发后⑥占本值日，第 popAlarmFocusCapDays+1 日让位 1 轮给评分焦点（含建造）再轮替回⑥——防独占⑥在「招满8人真空窗」饿死建造（与决策①⑥永不选为镜像缺陷）。")]
    public int popAlarmFocusCapDays = 2;

    [Header("执行派遣（2_17 完整局批次，D345 执行面）")]
    [Tooltip("AI 招募流浪汉成本（粮/人；⑥招工人通道，镜像玩家 recruitFoodCost 语义）")]
    public int aiRecruitFoodCost = 2;
    [Tooltip("AI 建造选址半径（以本国主城为中心的切比雪夫格半径上限）")]
    public int aiBuildRadius = 8;

    [Header("⑦招战士 兵力目标（D348，P1 步骤10；系数入训项注释见 D324）")]
    [Tooltip("兵力目标公式 clamp(2+⌈威胁×兵力ThreatScale⌉+阶段系数, forceFloor, 2+工人数) 的底数下限")]
    public int militaryTargetFloor = 2;
    [Tooltip("威胁→兵力系数（D348 ×3；k=目标=clamp(2+ceil(threat×scale)+stageFactor, floor, 2+workerCount)）")]
    public float militaryThreatScale = 3f;
    [Tooltip("扩张期阶段系数追加（D348：存活/发育 0、扩张 +1、军事 +militaryStageFactor）")]
    public int militaryExpandStageFactor = 1;
    [Tooltip("军事期阶段系数追加（D348：存活/发育 0、扩张 +militaryExpandStageFactor、军事 +本值）")]
    public int militaryStageFactor = 2;
    [Tooltip("威胁值分母下限（威胁=邻国兵力/己方兵力，分母 max(己方兵力, 1)）")]
    public int militaryThreatDenominatorMin = 1;
    [Tooltip("⑦招战士成本（金/人，worker→warrior 直转通道；so-data-driven 禁魔法数）")]
    public int recruitWarriorCostGold = 20;
    [Tooltip("⑦招战士成本（粮/人，同上）")]
    public int recruitWarriorCostFood = 4;
    [Tooltip("⑧科技升级成本（金/次，per-kingdom 解锁态步骤11 落地的占位执行成本）")]
    public int techUpgradeCostGold = 80;
}