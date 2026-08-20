using UnityEngine;

/// <summary>
/// 王国每日结算统一入口（3.5 实施计划 §四 风险5 建议）。
/// 饱食/税收/人口/贸易全挂昼夜结算，统一在此订阅 TimeDayChangedEvent，
/// 避免各系统各自挂 Update 造成结算点散乱。
///
/// P0 接入：人口生育（PopulationSystem.OnNewDay）+ 贸易额度冷却（KingdomManager.TickTradeCooldowns）。
/// P1 扩展：饱食结算 / 税收 / 幸福 / 装备研究 均在此追加调用。
/// </summary>
public class DayCycleSettlement : Singleton<DayCycleSettlement>
{
    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        EventBus.Subscribe<TimeDayChangedEvent>(OnDayChanged);
        EventBus.Subscribe<TimePhaseChangedEvent>(OnPhaseChanged);
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        base.OnDestroy();
        EventBus.Unsubscribe<TimeDayChangedEvent>(OnDayChanged);
        EventBus.Unsubscribe<TimePhaseChangedEvent>(OnPhaseChanged);
    }

    /// <summary>
    /// 2_8 步骤7/8：入夜到点判断灾害触发（偶发传送门灾害，2_14）。
    /// 判定（概率/天数保底/防长草）在 WaveDirector，本结算只做触发入口 + 派发。
    /// </summary>
    private void OnPhaseChanged(TimePhaseChangedEvent evt)
    {
        if (evt.NewPhase != TimePhase.Night) return;
        if (WaveDirector.Instance != null && WaveDirector.Instance.ShouldTriggerDisasterThisNight())
            WaveDirector.Instance.SpawnDisaster();
    }

    private void OnDayChanged(TimeDayChangedEvent evt)
    {
        // 1. 饱食结算（先更新个体饱食/幸福，供幸福系统消费）
        if (SatietySystem.Instance != null)
            SatietySystem.Instance.OnNewDay();

        // 2. 幸福结算（多因素加权，替换 P0 占位常量；用昨日税负 + 今日饱食）
        if (HappinessSystem.Instance != null)
            HappinessSystem.Instance.OnNewDay();

        // 3. 税收结算（人头税 + 建筑税，幸福系数缩放；写入今日税负供明日幸福）
        if (TaxSystem.Instance != null)
            TaxSystem.Instance.OnNewDay();

        // 4. 人口生育（数据层先行；AvgHappiness 接真实值）
        if (PopulationSystem.Instance != null)
            PopulationSystem.Instance.OnNewDay();

        // 5. 贸易额度冷却（商人档位刷新）
        if (KingdomManager.Instance != null)
            KingdomManager.Instance.TickTradeCooldowns();

        // 牧场养殖每日结算（喂粮/生长）
        if (RanchSystem.Instance != null)
            RanchSystem.Instance.OnNewDay();

        // 流浪汉营地每日补员（3.5.1 §4.1 E-S7：不满补员，刷满停）
        if (VagrantCampSystem.Instance != null)
            VagrantCampSystem.Instance.OnNewDay();

        // P1 占位：研究 / 装备 在此追加

        // 结算全部完成后发 DaySettledEvent（QQQ.3 B8-2 / LC-G5 / D10）
        // SaveManager 自动存档改订阅本事件 ⇒ "结算先、存档后"顺序显式化，不依赖订阅先后。
        if (TimeManager.Instance != null)
            EventBus.Publish(new DaySettledEvent(TimeManager.Instance.CurrentDay));
    }
}