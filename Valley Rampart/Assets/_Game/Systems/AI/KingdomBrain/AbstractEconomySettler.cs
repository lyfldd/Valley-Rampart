using System;

// ============================================================================
//  抽象结算引擎（2_17 步骤14 批A，D336/D459/D462）
//  纯 C# 零 UnityEngine 引用：自有 DTO（KingdomEconomySnapshot/SettlementDelta/
//  EcoModifiers/AbstractEconomyParams），公式镜像 sim harness/Economy/SimEconomy.cs
//  （QQQ.5 私有副本；15_账本 L49 前向引用已修正=SimEconomy.cs 非同名文件）：
//    采集产出（EconomyTick）· 每日耗粮（DailyFoodNeed + EconomyTick 扣粮/断粮标记）·
//    税收（DailySettle 人头税）
//  边界：本引擎只算"一抽象王国一日"的纯函数结算，不接触 KingdomState/实体/Unity 域；
//        Unity 适配层（AbstractEconomySettlement）负责 KingdomState↔DTO 翻译与增量应用。
//  确定性：建筑遍历序由调用方固定排序（⑤-3 硬性 a 同款纪律），引擎按传入序逐条计算，
//        同 seed 两轮逐字节一致（任务书 P3 探针）。
//  D462（2_20 种族经济修正挂载预留）：EcoModifiers 乘点占位恒 1.0f 零行为差；真值+实体
//        挂载点（TaskScheduler 采集双点/DamagePipeline/Building.Init）归 Q10-M5/M8——
//        本步不创建 RaceDef、不读 KingdomDef.raceId（M1/M2 域）。
// ============================================================================

/// <summary>种族经济修正乘点（2_20 D462 占位：Q10-M5/M8 接入真值前恒 1.0f 零行为差）。
/// 字段对齐 2_20.1 §二 经济乘数挂载点映射表 eco.mineMul/lumberMul/farmMul/buildSpeedMul。</summary>
public struct EcoModifiers
{
    /// <summary>采矿产出%（2_20.1 §二 eco.mineMul；消费点=矿洞产出乘点）。</summary>
    public float mineMul;
    /// <summary>伐木产出%（eco.lumberMul；消费点=伐木场产出乘点）。</summary>
    public float lumberMul;
    /// <summary>粮产%（eco.farmMul；消费点=农田产出乘点）。</summary>
    public float farmMul;
    /// <summary>建造%（eco.buildSpeedMul；建造队列推进不在本步范围，字段占位 Q10 实装）。</summary>
    public float buildSpeedMul;

    /// <summary>默认占位：全 1.0f 零行为差（D462）。</summary>
    public static EcoModifiers Default => new EcoModifiers
    {
        mineMul = 1f, lumberMul = 1f, farmMul = 1f, buildSpeedMul = 1f
    };
}

/// <summary>抽象结算建筑条目（纯 C#；Type=产出资源语义，镜像 sim 建筑类型→资源映射）。</summary>
public struct AbstractBuildingEntry
{
    /// <summary>产出资源语义："Wood"/"Stone"/"Ore"/"Food"/"Metal"。</summary>
    public string Type;
    /// <summary>建筑等级（1-3；sim levelMult = Lv1 1 / Lv2 2 / Lv3 3）。</summary>
    public int Level;
    /// <summary>并发工人槽（def.concurrentWorkers；0=不限→按等级，镜像 sim AddBuilding ConcurrentWorkers=level）。</summary>
    public int ConcurrentCapacity;
}

/// <summary>抽象结算快照（纯 C# 输入：一抽象王国当日全量）。由 Unity 适配层构建。</summary>
public struct KingdomEconomySnapshot
{
    public int KingdomId;
    /// <summary>生活工人数（Worker/Porter/Civilian）——产能分配工人池。</summary>
    public int WorkerCount;
    /// <summary>生活职业数（Resident+Worker+Porter+Child；日耗粮 ×LifeFoodPerDay）。</summary>
    public int LifeCount;
    /// <summary>士兵数（Warrior/Archer/Crossbowman/Cavalry/Mage/Healer；日耗粮 ×SoldierFoodPerDay）。</summary>
    public int SoldierCount;
    /// <summary>高耗数（HeavyWarrior/ShieldGuard/Archmage/Bishop/General；日耗粮 ×EliteFoodPerDay）。</summary>
    public int EliteCount;
    /// <summary>连续断粮日数（D400 流失判定输入；由 Unity 适配层 per-kingdom 维护，确定性）。</summary>
    public int ContinuousUnfedDays;
    /// <summary>建筑清单（调用方已固定排序，⑤-3 硬性 a）。</summary>
    public System.Collections.Generic.List<AbstractBuildingEntry> Buildings;
    /// <summary>国库现状（镜像 sim 资源存量）。</summary>
    public int Food, Gold, Stone, Wood, Metal;
    /// <summary>王国均饱食现状（断粮扣均饱食/D400 流失输入）。</summary>
    public float AvgSatiety;
}

/// <summary>抽象结算参数（镜像 sim EconomyConfig；.cs 默认值=数值双落之一，SO 载体=批B AbstractEconomyConfig）。</summary>
public struct AbstractEconomyParams
{
    /// <summary>伐木场日产量（sim EconomyConfig.LumberjackDaily=8）。</summary>
    public float LumberjackDaily;
    /// <summary>采石场日产量（sim QuarryDaily=6）。</summary>
    public float QuarryDaily;
    /// <summary>矿洞日产量（sim MineDaily=4；Ore 非经济资源不入 AI 国库，乘点仍按 D462 保留）。</summary>
    public float MineDaily;
    /// <summary>农田日产量（sim FarmDaily=6）。</summary>
    public float FarmDaily;
    /// <summary>铁匠铺日产量（石→Metal 就地加工 D200；sim 无对应建筑，Unity 侧对齐量级）。</summary>
    public float BlacksmithDaily;
    /// <summary>人头税（金/人口/日；sim HeadTaxGold=0.5）。</summary>
    public float HeadTaxGold;
    /// <summary>生活职业日耗粮（sim ×1）。</summary>
    public int LifeFoodPerDay;
    /// <summary>士兵日耗粮（sim ×2）。</summary>
    public int SoldierFoodPerDay;
    /// <summary>高耗日耗粮（sim ×3）。</summary>
    public int EliteFoodPerDay;
    /// <summary>居民无粮连续 N 日 → 流失 -1 转最近营地流民（D400；SO 默认 3）。</summary>
    public int ResidentUnfedDaysToLeave;
    /// <summary>战士断粮（王国无粮）连续 N 日 → 解散转流民（D400；SO 默认 5）。</summary>
    public int WarriorUnfedDaysToDesert;

    /// <summary>默认值（镜像 sim EconomyConfig 占位数值表；批B 由 AbstractEconomyConfig SO 覆盖）。</summary>
    public static AbstractEconomyParams Default => new AbstractEconomyParams
    {
        LumberjackDaily = 8f, QuarryDaily = 6f, MineDaily = 4f, FarmDaily = 6f, BlacksmithDaily = 4f,
        HeadTaxGold = 0.5f, LifeFoodPerDay = 1, SoldierFoodPerDay = 2, EliteFoodPerDay = 3,
        ResidentUnfedDaysToLeave = 3, WarriorUnfedDaysToDesert = 5
    };
}

/// <summary>结算增量（纯 C# 输出；正=入账，负=扣减）。</summary>
public struct SettlementDelta
{
    public int Food, Gold, Stone, Wood, Metal;
    /// <summary>均饱食变化量（断粮扣均饱食，镜像 sim EconomyTick shortfall 扣减；批A 恒 0）。</summary>
    public float AvgSatiety;
    /// <summary>本日断粮（存量+当日产出被吃光）。</summary>
    public bool FoodExhausted;
    /// <summary>断粮缺口（DailyFoodNeed - 可用粮；按此扣均饱食，D460）。</summary>
    public int UnfedShortfall;
    /// <summary>居民流失数（D400：居民无粮连续 N 日 → -1 转最近营地流民；每日 ≤1 镜像 sim 每断粮天死 1 最脆弱）。</summary>
    public int LossResidents;
    /// <summary>战士解散数（D400：战士断粮连续 N 日 → 解散转流民；每日 ≤1）。</summary>
    public int LossSoldiers;
    /// <summary>是否已产生流失（全民==0 → D388 账本层灭亡管线归 2_19，本步仅标记）。</summary>
    public bool HasLoss;
}

/// <summary>
/// 抽象结算引擎（2_17 步骤14 批A：镜像 sim 公式的纯 C# 日结算纯函数）。
/// 三次调用同一输入 → 同输出（确定性；无 RNG、无外部状态）。
/// </summary>
public static class AbstractEconomySettler
{
    /// <summary>一抽象王国一日结算：采集产出 → 税收 → 每日耗粮（扣粮/断粮标记）。</summary>
    public static SettlementDelta SettleDaily(KingdomEconomySnapshot s, AbstractEconomyParams p, EcoModifiers eco)
    {
        var delta = new SettlementDelta();

        // 1. 采集产出（镜像 sim EconomyTick：Σ per-building DailyRate×levelMult×assignedWorkers×ecoMul）
        //    工人按固定建筑序逐建筑分配（确定性；sim 为动态分配，抽象态用固定顺序重分配=同构）。
        int remainingWorkers = s.WorkerCount;
        for (int i = 0; i < s.Buildings.Count; i++)
        {
            var b = s.Buildings[i];
            float levelMult = b.Level >= 3 ? 3f : (b.Level >= 2 ? 2f : 1f);
            int capacity = b.ConcurrentCapacity > 0 ? b.ConcurrentCapacity : b.Level;
            int assigned = Math.Min(remainingWorkers, capacity);
            if (assigned <= 0) continue;
            remainingWorkers -= assigned;

            switch (b.Type)
            {
                case "Wood":
                    delta.Wood += (int)(p.LumberjackDaily * levelMult * assigned * eco.lumberMul);
                    break;
                case "Stone":
                    // 2_20.1 §二 无 stone 专属乘数 → 采石场不乘（保持 1.0 语义）。
                    delta.Stone += (int)(p.QuarryDaily * levelMult * assigned);
                    break;
                case "Ore":
                    // 矿洞产 Ore 非经济资源不入 AI 国库（对齐 2b MapToPack 跳过 Ore）；
                    // mineMul 乘点保留（D462：Q10-M5/M8 接真值），本步不落地 Ore 数值。
                    break;
                case "Food":
                    delta.Food += (int)(p.FarmDaily * levelMult * assigned * eco.farmMul);
                    break;
                case "Metal":
                    delta.Metal += (int)(p.BlacksmithDaily * levelMult * assigned);
                    break;
            }
        }

        // 2. 税收（镜像 sim DailySettle：人头税；金=货币不占存储）
        int totalPopulation = s.LifeCount + s.SoldierCount + s.EliteCount;
        delta.Gold += (int)(totalPopulation * p.HeadTaxGold);

        // 3. 每日耗粮（镜像 sim DailyFoodNeed + EconomyTick 扣粮/断粮：有粮先吃，缺口记断粮）
        int dailyFoodNeed = s.LifeCount * p.LifeFoodPerDay
                          + s.SoldierCount * p.SoldierFoodPerDay
                          + s.EliteCount * p.EliteFoodPerDay;
        int available = s.Food + delta.Food;                  // 存量 + 当日产出
        int consume = Math.Min(dailyFoodNeed, available);
        delta.Food -= consume;
        if (consume < dailyFoodNeed)
        {
            // 断粮：Food 已随 consume=available 清 0（s.Food+delta.Food-consume=0）
            delta.FoodExhausted = true;
            delta.UnfedShortfall = dailyFoodNeed - consume;
            // 断粮扣均饱食（镜像 sim EconomyTick L151-163：每缺 1 粮扣 1 点均饱食，0 封底；D460）
            int satietyLoss = Math.Min(delta.UnfedShortfall, Math.Max(0, (int)s.AvgSatiety));
            delta.AvgSatiety = -satietyLoss;
        }

        // 4. D400 抽象态全民流失（2_17 §3.3 追写：居民无粮连续 N 日 -1 转营地 / 战士断粮解散转流民；
        //    镜像 sim ApplyDeathPenalty 精神——均饱食=0 且断粮时每日流失 1 最脆弱，另加 N 日门槛）
        int unfed = s.ContinuousUnfedDays + (delta.FoodExhausted ? 1 : 0);
        if (delta.FoodExhausted && s.AvgSatiety <= 0f)
        {
            if (p.ResidentUnfedDaysToLeave > 0 && unfed >= p.ResidentUnfedDaysToLeave && s.LifeCount > 0)
                delta.LossResidents = 1;
            if (p.WarriorUnfedDaysToDesert > 0 && unfed >= p.WarriorUnfedDaysToDesert && s.SoldierCount + s.EliteCount > 0)
                delta.LossSoldiers = 1;
        }
        delta.HasLoss = delta.LossResidents > 0 || delta.LossSoldiers > 0;
        return delta;
    }
}
