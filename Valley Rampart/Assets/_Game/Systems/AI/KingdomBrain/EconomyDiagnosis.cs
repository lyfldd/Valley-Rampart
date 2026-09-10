using System;
using System.Collections.Generic;

// ============================================================================
//  经济诊断块（2_23 资源 P0 批A / R-A1，D528 统一国情快照 ②经济诊断块 真值）
//  纯 C# 零 UnityEngine 引用（同 AbstractEconomySettler 先例：不用 Mathf 用 System.Math、
//  不用任何 Unity 类型；DTO 全部定义在本文件内）。边界=本层只做纯函数聚合，
//  Unity 侧适配层（KingdomBrain.BuildEconomyBlock）负责取数 → DTO 翻译。
//
//  四件真值（2_23 §2.1「经济诊断块」）：
//    ① 五元资源收支（金/石/木/粮/铁：日入/日出/净流量 per 资源 + 劳力口径）
//    ② 产能盘点（产能建筑×类型×产出率、farm/人口比、采集点饱和度）
//    ③ 断链检测（MustHaveBaseline 基线 vs 现有 → 缺失清单）
//    ④ 储备水位（粮裕日/仓储占用率）
//
//  收支窗口口径（D630 裁 A+，2026-09-11）：
//    日入＝当日资源增量和／日出＝当日资源消耗和／净流量＝入−出；
//    取数＝**入账/扣减时登记**的窗口累计（KingdomState.AddResources/Spend → RegisterFlow），
//    日 tick 重建时读取并清零 ⇒ 无持久态、纯函数聚合（对齐设计稿 §一 八格 L186）。
//    窗口语义注记：快照重建发生在 DayCycleSettlement 步骤②（早于步骤⑤ 日结入账）
//    ⇒ 窗口中累计的是「自上次重建以来」的流量＝脑中「昨日结存」口径（与 UtilityScorer 缺口口径一致）。
//    覆盖面（D632 A′ 收口后）：五经济资源全部增减均经台账 API 登记（AddResources/Spend/Refund
//    三口），生产面已无绕 API 直写点（含牧场买崽/喂粮/产肉、饱食进食扣粮、Refund 退款）。
//
//  粮裕日口径注记（D632 列报2②）：本层用「存活判据同源」口径＝粮存量 / max(1, 人口×每人口日耗)
//    （对齐 KingdomBrainConfig.grainConsumptionPerPop / UtilityScorer.PerPopGrain）；
//    与 AbstractEconomySettler 的 Life/Soldier/Elite 1/2/3 加权耗粮口径**并存但不同源**，
//    后者归抽象期执行面，本层不采用（如需统一由策划端裁）。
//
//  八格（AGENTS.md §〇；批A 附注「与 2_22 批A 快照同一八格口径」）：
//    1 诞生=日 tick 全量重建（KingdomBrain.BuildSituation → BuildEconomyBlock）；触发源唯一=王国脑日 tick
//    2 归属=无独立归属者，作为 SituationSnapshot.Economy 随快照存于 SituationHub（静态槽）
//    3 更新=仅日 tick 重建；Abstract 期照跑（D476 同款分叉：诊断跑、执行分叉）
//    4 挂起/恢复=无挂起态；读档后首个日 tick 全量重建即恢复（窗口由 0 起累计，首日=部分窗）
//    5 正常终结=王国灭亡 → SituationHub.Remove(kingdomId) → EconomyDiagnosis.Remove(kingdomId)
//    6 异常终结=无中途中断路径（纯函数聚合，无协程/无回调）
//    7 清算=收支窗口随 Remove/Clear 清空（无外部引用持有）
//    8 持久化=不入档（D528/D456 同哲学：日 tick 重建的派生态，读档即重建）
// ============================================================================

/// <summary>五元资源枚举（诊断块内部口径；与 ResourceType 解耦=本层零 UnityEngine 依赖）。</summary>
public enum EcoResource : byte
{
    Gold = 0,
    Stone = 1,
    Wood = 2,
    Food = 3,
    Metal = 4
}

/// <summary>收支窗口累计（纯数据）：正数入账记 In、负数（含 Spend 传入的负包）记 Out。</summary>
public struct ResourceFlow
{
    public int goldIn, stoneIn, woodIn, foodIn, metalIn;
    public int goldOut, stoneOut, woodOut, foodOut, metalOut;

    public int In(EcoResource r)
    {
        switch (r)
        {
            case EcoResource.Gold: return goldIn;
            case EcoResource.Stone: return stoneIn;
            case EcoResource.Wood: return woodIn;
            case EcoResource.Food: return foodIn;
            default: return metalIn;
        }
    }

    public int Out(EcoResource r)
    {
        switch (r)
        {
            case EcoResource.Gold: return goldOut;
            case EcoResource.Stone: return stoneOut;
            case EcoResource.Wood: return woodOut;
            case EcoResource.Food: return foodOut;
            default: return metalOut;
        }
    }

    /// <summary>净流量＝入−出。</summary>
    public int Net(EcoResource r) => In(r) - Out(r);

    public int TotalIn => goldIn + stoneIn + woodIn + foodIn + metalIn;
    public int TotalOut => goldOut + stoneOut + woodOut + foodOut + metalOut;
}

/// <summary>产能建筑条目（按产出资源聚合；Type=EcoResource，确定性升序）。</summary>
public struct ProductionEntry
{
    public EcoResource Resource;
    public int Count;        // 本国 Active 产能建筑数（该产出资源）
    public int LevelSum;     // Σ level（等级加权；升级进度观察）
    public float RateSum;    // Σ def.producer.rate（设计产出率，每秒口径=BuildingDef 原值）
    public int WorkersSum;   // Σ def.concurrentWorkers（并发工人槽位）
}

/// <summary>断链缺失条目（MustHave 基线 vs 现状）。</summary>
public struct MustHaveMiss
{
    public int RequiredKind;   // 0=Farm 1=Warehouse 2=Fortification
    public int Required;       // 该档基线要求数
    public int Actual;         // 现有数
}

/// <summary>必须建筑基线（R-A3 MustHaveConfig 解析结果 → 纯 DTO，本层不引 SO）。</summary>
public struct MustHaveBaseline
{
    public int TierIndex;         // 命中档 index（人口规模档主轴）
    public int FarmRequired;      // ≥ceil(pop / farmPerPeople)
    public int WarehouseRequired; // ≥minWarehouse
    public int FortRequired;      // 城墙目标数
    public int RuleCount;         // 基线规则数（断链检测分母）
}

/// <summary>经济诊断输入（Unity 适配层组装；纯数据，无 UnityEngine 类型）。</summary>
public struct EconomyInput
{
    public int KingdomId;
    public int Day;

    // 五元存量（今日 tick 重建时点）
    public int StockGold, StockStone, StockWood, StockFood, StockMetal;

    // 收支窗口（自上次重建以来累计；已 TakeFlow 取出）
    public ResourceFlow Flow;

    // 人口 / 劳力口径
    public int WorkerCount, WarriorCount;

    // 产能 / 仓储 / 工事 / 采集点盘点
    public List<ProductionEntry> Production;   // 确定性升序（EcoResource 升序）
    public int StorageUsed, StorageCapacity;
    public int FortCount;                      // 工事座数（Building.IsFortification）
    public int GatherNodeCount;                // 本国领土内资源节点数（def.isResourceNode）
    public int WarehouseCount;                 // 仓储建筑数（Warehouse/Granary 类 def）
    public bool IsAbstract;                    // Abstract 期口径注记（诊断照跑 D476）

    // 基线与参数（SO → DTO 解析结果）
    public MustHaveBaseline Baseline;
    public int GrainConsumptionPerPop;         // 每人口每日粮耗（对齐 KingdomBrainConfig）
    public float FarmPerPeopleBaseline;        // 每 N 人 ≥1 farm（产能基线）
    public float GatherNodePerPopBaseline;     // 人均资源节点期望（采集点饱和度分母）
}

/// <summary>
/// 经济诊断块（四件真值；落 SituationSnapshot.Economy）。日 tick 全量重建，不入档（D528 八格）。
/// </summary>
public class EconomyBlock
{
    // ===== ① 五元资源收支 =====
    public ResourceFlow Flow;
    public int StockGold, StockStone, StockWood, StockFood, StockMetal;
    public int In(EcoResource r) => Flow.In(r);
    public int Out(EcoResource r) => Flow.Out(r);
    public int Net(EcoResource r) => Flow.Net(r);

    // 劳力口径（收支的劳力归一：人均日入，便于跨规模比较）
    public int Population;
    public int WorkerCount;
    public int WarriorCount;
    public float IncomePerWorker;

    // ===== ② 产能盘点 =====
    public List<ProductionEntry> Production;
    public int CapacityBuildingCount;   // 产能建筑总数（Σ Count）
    public int FarmCount;               // 产粮产能建筑数（farm/人口比分子）
    public float FarmPerPop;
    public int GatherNodeCount;         // 领土内资源节点数
    public float GatherSaturation;      // 采集点饱和度=节点数 / max(1, 人口×人均期望)，clamp01

    // ===== ③ 断链检测 =====
    public List<MustHaveMiss> MissingMustHave;
    public int MustHaveBaselineCount;

    // ===== ④ 储备水位 =====
    public float GrainReserveDays;      // 粮裕日 = 粮存量 / max(1, 人口×每人口日耗)
    public int StorageUsed;
    public int StorageCapacity;
    public float StorageOccupancy;      // 仓储占用率 = 已用 / 容量

    // ===== 口径标记 =====
    public bool IsAbstract;             // Abstract 期（诊断照跑，经济执行分叉 D476/D459）

    /// <summary>取存量（供消费方按资源遍历；口径=快照重建时点）。</summary>
    public int StockOf(EcoResource r)
    {
        switch (r)
        {
            case EcoResource.Gold: return StockGold;
            case EcoResource.Stone: return StockStone;
            case EcoResource.Wood: return StockWood;
            case EcoResource.Food: return StockFood;
            default: return StockMetal;
        }
    }
}

/// <summary>
/// 经济诊断引擎（R-A1 本体）。纯函数聚合：同输入 → 同输出（确定性红线，无随机无时间）。
/// 唯一有状态面=收支窗口累计（_flow），生命周期随 SituationHub.Clear/Remove 清空（八格 5/7）。
/// </summary>
public static class EconomyDiagnosis
{
    /// <summary>收支窗口累计（per-kingdom；不入档；日 tick 重建读取后清零）。</summary>
    private static readonly Dictionary<int, ResourceFlow> _flow = new Dictionary<int, ResourceFlow>();

    /// <summary>
    /// 入账/扣减登记（KingdomState.AddResources/Spend 调用）：正数记 In、负数记 Out；
    /// 只统计 AI 王国（id&gt;0；玩家走 RulerController/TreasureVault 不在此台账）。
    /// </summary>
    public static void RegisterFlow(int kingdomId, int gold, int stone, int wood, int food, int metal)
    {
        if (kingdomId <= 0) return;

        _flow.TryGetValue(kingdomId, out var f);
        Accum(ref f.goldIn, ref f.goldOut, gold);
        Accum(ref f.stoneIn, ref f.stoneOut, stone);
        Accum(ref f.woodIn, ref f.woodOut, wood);
        Accum(ref f.foodIn, ref f.foodOut, food);
        Accum(ref f.metalIn, ref f.metalOut, metal);
        _flow[kingdomId] = f;
    }

    private static void Accum(ref int inSlot, ref int outSlot, int delta)
    {
        if (delta > 0) inSlot += delta;
        else if (delta < 0) outSlot += -delta;
    }

    /// <summary>读取并清零某国收支窗口（日 tick 重建时调用=纯函数化关键：读后即空）。</summary>
    public static ResourceFlow TakeFlow(int kingdomId)
    {
        if (_flow.TryGetValue(kingdomId, out var f))
        {
            _flow.Remove(kingdomId);
            return f;
        }
        return default;
    }

    /// <summary>王国灭亡/退订时移除其窗口（随 SituationHub.Remove）。</summary>
    public static void Remove(int kingdomId) => _flow.Remove(kingdomId);

    /// <summary>全清（harness 两轮间/新开局；随 SituationHub.Clear）。</summary>
    public static void Clear() => _flow.Clear();

    /// <summary>
    /// 四件真值聚合（纯函数）。确定性：产出条目按 EcoResource 升序；缺失清单按 RequiredKind 升序。
    /// </summary>
    public static EconomyBlock Build(EconomyInput input)
    {
        var block = new EconomyBlock
        {
            // ① 五元资源收支 + 劳力口径
            Flow = input.Flow,
            StockGold = input.StockGold,
            StockStone = input.StockStone,
            StockWood = input.StockWood,
            StockFood = input.StockFood,
            StockMetal = input.StockMetal,
            WorkerCount = input.WorkerCount,
            WarriorCount = input.WarriorCount,
            Population = input.WorkerCount + input.WarriorCount,
            // ② 产能盘点
            Production = NormalizeProduction(input.Production),
            GatherNodeCount = input.GatherNodeCount,
            // ③ 断链检测
            MustHaveBaselineCount = input.Baseline.RuleCount,
            // ④ 储备水位
            StorageUsed = input.StorageUsed,
            StorageCapacity = input.StorageCapacity,
            IsAbstract = input.IsAbstract
        };

        // ② 派生：产能总数 / farm 数 / farm-人口比 / 采集点饱和度
        int capTotal = 0, farmCount = 0;
        if (block.Production != null)
        {
            for (int i = 0; i < block.Production.Count; i++)
            {
                var e = block.Production[i];
                capTotal += e.Count;
                if (e.Resource == EcoResource.Food) farmCount += e.Count;
            }
        }
        block.CapacityBuildingCount = capTotal;
        block.FarmCount = farmCount;
        block.FarmPerPop = block.Population > 0 ? farmCount / (float)block.Population : 0f;

        float nodeExpect = block.Population * System.Math.Max(0f, input.GatherNodePerPopBaseline);
        block.GatherSaturation = nodeExpect > 0f
            ? Clamp01(input.GatherNodeCount / nodeExpect)
            : (input.GatherNodeCount > 0 ? 1f : 0f);

        // ③ 断链检测（基线 vs 现状：farm 需求按人口折算）
        block.MissingMustHave = BuildMissing(input, farmCount);

        // ④ 粮裕日（口径对齐 KingdomBrainConfig.grainConsumptionPerPop：每人口每日粮耗）
        int perPop = System.Math.Max(1, input.GrainConsumptionPerPop);
        int dailyGrain = System.Math.Max(1, block.Population * perPop);
        block.GrainReserveDays = input.StockFood / (float)dailyGrain;

        // ④ 仓储占用率
        block.StorageOccupancy = input.StorageCapacity > 0
            ? Clamp01(input.StorageUsed / (float)input.StorageCapacity)
            : 0f;

        // ① 劳力口径：人均日入（总入 / 工人数；无工人=0）
        block.IncomePerWorker = block.WorkerCount > 0
            ? block.Flow.TotalIn / (float)block.WorkerCount
            : 0f;

        return block;
    }

    /// <summary>产出条目确定性排序（EcoResource 升序）并做防御性拷贝（禁外部队列引用）。</summary>
    private static List<ProductionEntry> NormalizeProduction(List<ProductionEntry> src)
    {
        var list = new List<ProductionEntry>();
        if (src != null)
        {
            for (int i = 0; i < src.Count; i++) list.Add(src[i]);
        }
        list.Sort((a, b) => ((byte)a.Resource).CompareTo((byte)b.Resource));
        return list;
    }

    /// <summary>断链缺失清单（farm 需求=ceil(人口/farmPerPeople)；按 RequiredKind 升序保确定性）。</summary>
    private static List<MustHaveMiss> BuildMissing(EconomyInput input, int farmCount)
    {
        var misses = new List<MustHaveMiss>();
        int pop = input.WorkerCount + input.WarriorCount;

        int farmRequired = input.Baseline.FarmRequired;
        if (farmRequired <= 0 && input.FarmPerPeopleBaseline > 0f)
        {
            farmRequired = (int)System.Math.Ceiling(pop / input.FarmPerPeopleBaseline);
        }
        AddMiss(misses, 0, farmRequired, farmCount);
        AddMiss(misses, 1, input.Baseline.WarehouseRequired, input.WarehouseCount);
        AddMiss(misses, 2, input.Baseline.FortRequired, input.FortCount);

        misses.Sort((a, b) => a.RequiredKind.CompareTo(b.RequiredKind));
        return misses;
    }

    private static void AddMiss(List<MustHaveMiss> misses, int kind, int required, int actual)
    {
        if (required <= 0 || actual >= required) return;
        misses.Add(new MustHaveMiss { RequiredKind = kind, Required = required, Actual = actual });
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}
