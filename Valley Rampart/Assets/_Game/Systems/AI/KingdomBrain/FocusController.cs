using UnityEngine;

// ============================================================================
//  国策焦点（2_17 步骤9，D322 完整焦点模型；步骤8 为骨架版）
//  每日 1 个国策焦点 = 行动 id（KingdomState.focus，正 int；0=无国策）。
//  常设底线焦点（⑤屯粮/⑭防御）也是正行动 id，避免负值魔术。
//
//  焦点模型（D322）：
//    1. 常设底线（触发式不评分、优先级最高、即时强制、跳过防抖）：
//       粮储<2日消耗→⑤屯粮(FocusGranary)；本土被攻击→⑭防御姿态(FocusDefense)
//    2. 效用评分：UtilityScorer 对可见候选打分 → LastTop（阶段门控过滤）
//    3. 焦点切换：最高分≠当前 && 当前已持续≥3日(防抖) → 切换；否则维持
//    4. 事件打断（D340）：宣战/灾害/主城被围 → 重规划（源未落地，P1 占位）
//  焦点值全为行动 id：冒烟/诊断统一看 kingdom.focus 与 LastTop。
// ============================================================================

public class FocusController
{
    /// <summary>常设底线焦点：屯粮（= 效用行动⑤ Grain）。</summary>
    public const int FocusGranary = (int)UtilityAction.Grain;     // 5

    /// <summary>常设底线焦点：防御姿态（= 效用行动⑭ Defense）。</summary>
    public const int FocusDefense = (int)UtilityAction.Defense;   // 14

    /// <summary>常设底线焦点：招工人（= 效用行动⑥ RecruitWorker）。人口底线（保增长下限）。</summary>
    public const int FocusRecruitWorker = (int)UtilityAction.RecruitWorker;   // 6

    private readonly int _kingdomId;

    /// <summary>本王国被攻击标记（KingdomAttackedEvent 命中置位，Update 消费）。</summary>
    private bool _attackedFlag;

    /// <summary>防御姿态持续到某日（被攻击后强制防御窗口 = focusMinDurationDays）。</summary>
    private int _defenseEndDay;

    /// <summary>当前焦点设定日（防抖基准：切换需距上次 ≥ focusMinDurationDays）。</summary>
    private int _focusSinceDay = int.MinValue / 2;

    /// <summary>份额式人口底线（策划裁决）：上次进入 popAlarm 的日（窗口起点，供轮替相位取模）。</summary>
    private bool _wasPopAlarm;
    private int _popWindowStartDay = int.MinValue / 2;

    /// <summary>本次评分顶行动（调试/冒烟断言：对比常设底线是否覆盖评分排序，冒烟#4）。</summary>
    public UtilityAction LastTop { get; private set; } = UtilityAction.None;

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

    /// <summary>兼容重载（步骤8 冒烟沿用 3 参签名；内部自动载入效用配置）。</summary>
    public void Update(KingdomState kingdom, KingdomBrainConfig brainCfg, int day)
        => Update(kingdom, brainCfg, UtilityActionConfig.LoadConfig(), day);

    /// <summary>
    /// 每日刷新国策焦点（王国脑日 tick 内，入账前口径）。
    /// 优先级：常设底线强制(⑤屯粮/⑥人口/⑭防御) &gt; 效用评分 &gt; 防抖切换。直接写 kingdom.focus。
    /// </summary>
    public void Update(KingdomState kingdom, KingdomBrainConfig brainCfg, UtilityActionConfig utilCfg, int day)
    {
        int population = kingdom.workerCount + kingdom.warriorCount;

        // 被攻击置位 → 防御窗口延到防抖日数末（姿态稳定窗口）
        if (_attackedFlag)
        {
            _defenseEndDay = Mathf.Max(_defenseEndDay, day + Mathf.Max(0, brainCfg.focusMinDurationDays));
            _attackedFlag = false;
        }

        // ---- 常设底线（D322 优先级最高、不评分、即时强制，跳过防抖；执行序=粮→人口→被攻击）----
        //  底线第一级「保命」：粮储<2日消耗 → 强制屯粮
        bool grainAlarm = population > 0
            && kingdom.GetResourceValue(ResourceType.Food)
               < brainCfg.grainReserveDaysFloor * population * brainCfg.grainConsumptionPerPop;
        if (grainAlarm) { SetFocus(kingdom, FocusGranary, day); return; }
        //  底线第二级「保增长下限」（自造件，D322 决策①判修结构性锁死 + HH.29 仲裁精确化）：工人 < 门槛 → 强制⑥
        //  低扩张国⑥评分被性格轴乘入长期压制（need=0.6 恒定 0.6×expansion 0.25=0.15 败给 BuildHouse），人口卡死→
        //  Develop→Expand 不可达→剧本卡存活期。触发式不评分跳防抖，与粮底线同构。
        //  门槛 = max(popFloor, developToExpand_workersMin)——「保增长下限」的下限=能升 Expand 的工人数，
        //  自动联动 SO 阈值（developToExpand_workersMin 改，此处不用跟着改）。
        //  三档错峰达成：帐篷4<8✓ / 村落6<8✓ / 要塞8<8 不触发（已达标，评分自由）✓。
        //  HH.30 份额式修正（策划裁决-HH.29 二次定性纠偏）：独占⑥会在"招满8人真空窗"饿死建造——
        //  底线本意是"保增长下限"不该吞掉正常经营，与决策①"⑥永不选"是镜像缺陷。故改"独占"为"份额"：
        //  popAlarm 触发→⑥占 popAlarmFocusCapDays 日，第 popAlarmFocusCapDays+1 日让位 1 轮给评分焦点
        //  （含建造），下轮若仍 popAlarm 再回来（相位轮替）。与粮底线 grainReserveDaysFloor 时窗语义同构。
        bool popAlarm = kingdom.workerCount < Mathf.Max(brainCfg.popFloor, brainCfg.developToExpand_workersMin);
        if (popAlarm)
        {
            if (!_wasPopAlarm) { _popWindowStartDay = day; _wasPopAlarm = true; }
            int phase = day - _popWindowStartDay;
            int cap = Mathf.Max(1, brainCfg.popAlarmFocusCapDays);
            bool recruitedTurn = phase % (cap + 1) < cap;
            if (recruitedTurn) { SetFocus(kingdom, FocusRecruitWorker, day); return; }
            // 让位日：⑥聚焦暂停 1 轮，交还评分焦点（含建造），防独占饿死经营；下轮槽位自然轮替回⑥。
            // 评分 + 强制切换（让位日无视防抖，保证建造得以落地）。
            if (day < _defenseEndDay) { SetFocus(kingdom, FocusDefense, day); return; }
            ScriptStage popStage = kingdom.scriptPhase ?? ScriptStage.Survive;
            UtilityAction popTop = UtilityScorer.ScoreTop(kingdom, utilCfg, popStage);
            LastTop = popTop;
            if (popTop != UtilityAction.None) SetFocus(kingdom, (int)popTop, day);
            return;
        }
        _wasPopAlarm = false;
        //  底线第三级「保命」：被攻击 → 强制防御窗口
        if (day < _defenseEndDay) { SetFocus(kingdom, FocusDefense, day); return; }

        // ---- 效用评分（D322 step2；阶段门控过滤 + 四因子打分）----
        ScriptStage stage = kingdom.scriptPhase ?? ScriptStage.Survive;
        UtilityAction top = UtilityScorer.ScoreTop(kingdom, utilCfg, stage);
        LastTop = top;
        if (top == UtilityAction.None) return;   // 无可执行候选 → 维持现状焦点

        // ---- 焦点切换防抖（D322 step3：最高分≠当前 且 当前已持续≥3日才切，否则维持）----
        if (kingdom.focus != (int)top
            && day >= _focusSinceDay + Mathf.Max(1, brainCfg.focusMinDurationDays))
            SetFocus(kingdom, (int)top, day);
        // 否则维持（"已是最优" 或 "防抖中" 均不改）
    }

    private void SetFocus(KingdomState kingdom, int id, int day)
    {
        kingdom.focus = id;
        _focusSinceDay = day;
    }
}