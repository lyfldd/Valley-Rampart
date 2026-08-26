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
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        base.OnDestroy();
        EventBus.Unsubscribe<TimeDayChangedEvent>(OnDayChanged);
    }

    /// <summary>
    /// 灾害触发：2_14 步骤8/10 单轨收拢后，判定权归 PortalDisasterTrigger（发布 PortalDisasterTriggeredEvent），
    /// WaveDirector 订阅该事件生成传送门+波次。本结算不再直连 WaveDirector 旧每晚判定入口（旧轨退役）。
    /// </summary>

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

        // 6. AI 段 日结转账（2_17 步骤2b 收入侧路由）：把 AI 建筑 Storage 累计产出 → AddResources 入
        //    KingdomState.resources → 清零。只处理 kingdomId>0，玩家(id=0)零回归。
        AIEconomySettlement.Tick();

        // 牧场养殖每日结算（喂粮/生长）
        if (RanchSystem.Instance != null)
            RanchSystem.Instance.OnNewDay();

        // 流浪汉营地每日补员（3.5.1 §4.1 E-S7：不满补员，刷满停）
        if (VagrantCampSystem.Instance != null)
            VagrantCampSystem.Instance.OnNewDay();

        // 2_16 步骤11：营地晋升调度（五条件动态立国/吞并出口B；必须在营地补员+存续 tick 之后）
        CampUpgrader.TickAll();

        // P1 占位：研究 / 装备 在此追加

        // 结算全部完成后发 DaySettledEvent（QQQ.3 B8-2 / LC-G5 / D10）
        // SaveManager 自动存档改订阅本事件 ⇒ "结算先、存档后"顺序显式化，不依赖订阅先后。
        if (TimeManager.Instance != null)
            EventBus.Publish(new DaySettledEvent(TimeManager.Instance.CurrentDay));
    }
}