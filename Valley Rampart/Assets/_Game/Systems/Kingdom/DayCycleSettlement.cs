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
        // ==== 2_17 步骤8：D347 五步权威日 tick 顺序（HH.24 裁决① A 准：Brain 植入②、日结入账前花昨日结存）====

        // 步骤1：SimMode 判定（P0 恒 Fine 占位；SimModeManager.GetMode 恒 Fine，真实判定/休眠唤醒归步骤13）
        // 步骤2：王国脑日 tick（D347 步②，日结入账之前 → 脑看到昨日结存；只循环非玩家王国，玩家无脑 D338）
        TickKingdomBrains();

        // 步骤3：领土变更（2_17 步骤12 批B：AI 推边界日 tick——⑩ ExpandTick；
        //         玩家建造纳土 ClaimAdjacentUnclaimed 归批C；吞并 A 日 tick 归 CampUpgrader 步骤4 前置）
        if (TerritorySystem.Instance != null)
            TerritorySystem.Instance.ExpandTick();
        // 步骤4：营地晋升调度（2_16 已有；顺序归位到③之后 = D347 五步第 4 步）
        CampUpgrader.TickAll();

        // ==== 步骤5：其余日结算 = 现行 1~9 尾巴逐项次序保持不变（增补1：行为保持重构，只做包结构不重排路线）====
        // 饱食→幸福→税收→人口→贸易冷却→AI段日结转账→牧场→营地补员；CampUpgrader 已移步骤4（设计重排非尾巴）。

        // - 饱食结算（先更新个体饱食/幸福，供幸福系统消费）
        if (SatietySystem.Instance != null)
            SatietySystem.Instance.OnNewDay();

        // - 幸福结算（多因素加权，替换 P0 占位常量；用昨日税负 + 今日饱食）
        if (HappinessSystem.Instance != null)
            HappinessSystem.Instance.OnNewDay();

        // - 税收结算（人头税 + 建筑税，幸福系数缩放；写入今日税负供明日幸福）
        if (TaxSystem.Instance != null)
            TaxSystem.Instance.OnNewDay();

        // - 人口生育（数据层先行；AvgHappiness 接真实值）
        if (PopulationSystem.Instance != null)
            PopulationSystem.Instance.OnNewDay();

        // - 贸易额度冷却（商人档位刷新）
        if (KingdomManager.Instance != null)
            KingdomManager.Instance.TickTradeCooldowns();

        // - AI 段 日结转账（2_17 步骤2b 收入侧路由）：把 AI 建筑 Storage 累计产出 → AddResources 入
        //   KingdomState.resources → 清零。只处理 kingdomId>0，玩家(id=0)零回归。
        AIEconomySettlement.Tick();

        // - 牧场养殖每日结算（喂粮/生长）
        if (RanchSystem.Instance != null)
            RanchSystem.Instance.OnNewDay();

        // - 流浪汉营地每日补员（不满补员，刷满停）
        if (VagrantCampSystem.Instance != null)
            VagrantCampSystem.Instance.OnNewDay();

        // P1 占位：研究 / 装备 在此追加

        // 结算全部完成后发 DaySettledEvent（QQQ.3 B8-2 / LC-G5 / D10）
        // SaveManager 自动存档改订阅本事件 ⇒ "结算先、存档后"顺序显式化，不依赖订阅先后。
        if (TimeManager.Instance != null)
            EventBus.Publish(new DaySettledEvent(TimeManager.Instance.CurrentDay));
    }

    /// <summary>
    /// D347 五步②：王国脑日 tick。循环所有非玩家王国（日记入账之前 → 脑花昨日结存，HH.24 裁决① A）。
    /// 只对已建脑的王国驱动（Foundry 创建钩子保证有脑；读档/测试容错跳过无脑王国）。玩家(id=0)无脑跳过（D338）。
    /// </summary>
    private void TickKingdomBrains()
    {
        var reg = KingdomRegistry.Instance;
        var brains = KingdomBrainRegistry.Instance;
        if (reg == null || brains == null) return;

        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            if (k.IsPlayer) continue;   // 玩家无脑（D338）
            var brain = brains.Get(k.id);
            if (brain != null) brain.Tick(day);
        }
    }
}