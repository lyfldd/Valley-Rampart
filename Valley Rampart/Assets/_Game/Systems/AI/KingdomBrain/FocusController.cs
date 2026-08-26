using UnityEngine;

// ============================================================================
//  国策焦点 + 常设底线 + 被攻击打断（2_17 步骤8，D322/D340 框架落点）
//  焦点模型（D322）：每日 1 个国策焦点（无固定时长，被替换才结束）；
//  **常设底线不被评分**、优先级最高、即时强制（粮<2日→屯粮；被攻击→防御），
//  无视效用排序、不参与评分。切换有 ≥3 日防抖（focusMinDurationDays）；
//  事件打断框架（D340）预留：宣战/灾害/主城被围 → 立即重规划（源未落地接口占位，
//  2_18/2_14 接入；本步只订阅 KingdomAttackedEvent 写被打标记）。
//
//  焦点值用 int：<0 = 常设底线焦点（FocusGranary/FocusDefense）；0 = 无国策；
//  >0 = 步骤9 UtilityAction id（P0 评分器未落地，正 id 暂不产生）。
// ============================================================================

public class FocusController
{
    /// <summary>国策焦点：屯粮（粮储常设底线强制，D322）。</summary>
    public const int FocusGranary = -1;
    /// <summary>国策焦点：防御姿态（被攻击常设底线强制，D322/D318 姿态落点）。</summary>
    public const int FocusDefense = -2;

    private readonly int _kingdomId;

    /// <summary>本王国被攻击标记（KingdomAttackedEvent 命中置位，Update 消费）。</summary>
    private bool _attackedFlag;

    /// <summary>防御姿态持续到某日（被攻击后强制防御窗口，防抖日数）。</summary>
    private int _defenseEndDay;

    public FocusController(int kingdomId)
    {
        _kingdomId = kingdomId;
    }

    /// <summary>订阅被攻击事件（王国脑生命周期内常驻；Unsubscribe 成对，D337/D340）。</summary>
    public void Subscribe() => EventBus.Subscribe<KingdomAttackedEvent>(OnAttacked);

    /// <summary>退订被攻击事件（灭亡销毁钩子，D340；2_19 接入吊钩）。</summary>
    public void Unsubscribe() => EventBus.Unsubscribe<KingdomAttackedEvent>(OnAttacked);

    private void OnAttacked(KingdomAttackedEvent evt)
    {
        if (evt.KingdomId == _kingdomId) _attackedFlag = true;
    }

    /// <summary>
    /// 每日刷新王国国策焦点（全阶段通用）：
    /// 1) 常设底线即时强制（粮<2日→屯粮；被攻击→防御，跳过防抖）；
    /// 2) 其余时段维持当前焦点 / P0 无评分器故无常规切换（步骤9 UtilityScorer 接入后经此闸输出）。
    /// 直接写 kingdom.focus（台账制，不发布事件）。
    /// </summary>
    public void Update(KingdomState kingdom, KingdomBrainConfig cfg, int day)
    {
        int population = kingdom.workerCount + kingdom.warriorCount;

        // 被攻击置位 → 防御姿态持续到防抖日数末（姿态稳定窗口）
        if (_attackedFlag)
        {
            _defenseEndDay = Mathf.Max(_defenseEndDay, day + Mathf.Max(0, cfg.focusMinDurationDays));
            _attackedFlag = false;
        }

        // 常设底线：粮储 < 粮储日底线 × 人口 × 每人口消耗 → 强制屯粮
        bool grainAlarm = population > 0
            && kingdom.GetResourceValue(ResourceType.Food)
               < cfg.grainReserveDaysFloor * population * cfg.grainConsumptionPerPop;

        if (grainAlarm)
            kingdom.focus = FocusGranary;
        else if (day < _defenseEndDay)
            kingdom.focus = FocusDefense;
        else if (kingdom.focus == FocusGranary || kingdom.focus == FocusDefense)
            kingdom.focus = 0;   // 底线解除 → 回无国策（P0 无评分器常规焦点待步骤9 输出）
    }
}