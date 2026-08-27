using UnityEngine;

// ============================================================================
//  效用评分器（2_17 步骤9，D321~D323/D345/D346，纯 C# 无 Unity 引用）
//  对王国可见候选行动按四因子评分（D323）选出胜者 → 国策焦点。
//  纯函数层（无副作用）：输入 王国快照 + 效用配置 SO + 剧本阶段 → 输出 顶行动 id。
//  可单测；同 seed 确定；评分全程不触碰实体（可行性=D346 二值门控，全由王国台账读）。
//
//  评分 = 需求强度 × 性格权重 × 可行性 × 阶段权重
//   - 需求强度：NeedScore 缺口函数单调分 0..1
//   - 性格权重：personality[axis]×action.axisWeight（五轴独立线性乘入，D311 契约）
//   - 可行性：Feasible 二值（D346），不可行→0 直接出局
//   - 阶段权重：action.stageWeight[stage]（D321 阶段只做可见性门控+该权重，不参与连续性）
// ============================================================================

/// <summary>效用候选行动 id（D321 扁平 15 项；P0 实装 ①~⑥+⑬⑭，D345；0=None）。</summary>
public enum UtilityAction : byte
{
    None = 0,
    BuildHouse = 1,        // ①建住宅
    BuildWarehouse = 2,    // ②建仓库
    BuildCapacity = 3,     // ③建产能
    BoostHarvest = 4,      // ④强化采集
    Grain = 5,             // ⑤屯粮（常设底线焦点）
    RecruitWorker = 6,     // ⑥招工人（p 人口增长唯一途径，防卡死存活期）
    RecruitWarrior = 7,    // ⑦招战士（P1）
    Tech = 8,              // ⑧科技升级（P1）
    BuildWall = 9,         // ⑨修工事城墙（P1）
    Expand = 10,           // ⑩推边界（P1 步骤12）
    Expedition = 11,       // ⑪组建出征军（P1，依赖 2_18）
    Reinforce = 12,        // ⑫边境增援（P1）
    Rebuild = 13,          // ⑬重建（D318 全阶段）
    Defense = 14,          // ⑭防御姿态（D318 全阶段 / 常设底线焦点）
    Diplomacy = 15         // ⑮外交姿态（P1，2_18 接管）
}

/// <summary>需求强度缺口函数类型（D323 单调缺口；参数 needA/needB 语义见各 case）。</summary>
public enum NeedKind : byte
{
    HouseGap,        // 住房缺口：needA=住房目标人口（P0 用人口代理无房数）
    WarehouseGap,    // 仓储缺口：needA=仓储容量基线（储量越高越需加仓）
    CapacityGap,     // 产能缺口：needA=产能目标（活跃建筑数不足则补）
    HarvestGap,      // 采集缺口：needB=粮裕日阈值（粮产不足则强化采集）
    GrainGap,        // 屯粮缺口：needA=粮储底线日（低于则屯）
    RecruitWorkerGap,// 工人缺口：needA=工人目标（少于则招募）
    RebuildGap,      // 重建缺口：needA=损毁比阈值（P0 无损毁统计，占位低分）
    DefenseNeed      // 防御缺口：P0 占位 0（被攻击由常设底线 cover；邻国威胁归 P1）
}

/// <summary>效用评分器（纯函数层，2_17 步骤9）。单入口 ScoreTop。</summary>
public static class UtilityScorer
{
    /// <summary>P0 每人口每日粮耗近似（与 KingdomBrainConfig.grainConsumptionPerPop 默认对齐）。</summary>
    private const int PerPopGrain = 1;

    /// <summary>对王国可见候选打分，返回最优行动 id（无可执行 → None）。</summary>
    public static UtilityAction ScoreTop(KingdomState k, UtilityActionConfig cfg, ScriptStage stage)
    {
        if (k == null || cfg == null || cfg.actions == null) return UtilityAction.None;

        float best = -1f;
        UtilityAction top = UtilityAction.None;
        var defs = cfg.actions;
        for (int i = 0; i < defs.Length; i++)
        {
            var def = defs[i];
            if (def.id == UtilityAction.None) continue;
            if (stage < def.minStage) continue;                 // D321 阶段可见性门控

            float need = NeedScore(k, def);
            if (need <= 0.0001f) continue;                      // 无需求 → 不入选（免刷 0 分干扰）
            if (!Feasible(k, def)) continue;                    // D346 二值门控：不可行 → 出局

            float axis = def.axisWeight;
            if (k.personality != null && def.axis >= 0 && def.axis < k.personality.Length)
                axis *= Mathf.Clamp01(k.personality[def.axis]); // 五轴独立线性乘入（D311）
            if (axis <= 0.0001f) continue;

            float stageW = (def.stageWeight != null && (int)stage < def.stageWeight.Length)
                ? Mathf.Max(0f, def.stageWeight[(int)stage]) : 1f;
            if (stageW <= 0f) continue;

            float score = need * axis * stageW;
            if (score > best) { best = score; top = def.id; }
        }
        return top;
    }

    /// <summary>需求强度缺口函数（0..1 单调；口径=王国昨日结存，与王国脑 tick 于入账前一致）。</summary>
    public static float NeedScore(KingdomState k, UtilityActionDef d)
    {
        int pop = k.workerCount + k.warriorCount;
        float food = k.GetResourceValue(ResourceType.Food);
        float grainDays = pop > 0 ? food / (float)Mathf.Max(1, pop * PerPopGrain) : 0f;

        switch (d.need)
        {
            case NeedKind.HouseGap:      // 人口越多越需住房（P0 用人口代理无房）
                return Mathf.Clamp01(pop / Mathf.Max(1f, d.needA));
            case NeedKind.WarehouseGap:  // 储量越接近容量基线越需加仓
                float maxRes = Mathf.Max(Mathf.Max(food, k.resources.gold), Mathf.Max(k.resources.stone, Mathf.Max(k.resources.wood, 0f)));
                return Mathf.Clamp01(maxRes / Mathf.Max(1f, d.needA));
            case NeedKind.CapacityGap:   // 产能建筑不足
                int cap = CountActiveBuildings(k.id);
                return Mathf.Clamp01((d.needA - cap) / Mathf.Max(1f, d.needA));
            case NeedKind.HarvestGap:    // 粮裕日不足则强化采集（needB=粮裕日阈值）
                return Mathf.Clamp01(1f - grainDays / Mathf.Max(1f, d.needB));
            case NeedKind.GrainGap:      // 粮储日低于底线（needA=底线日）
                return d.needA > 0 ? Mathf.Clamp01((d.needA - grainDays) / d.needA) : 0f;
            case NeedKind.RecruitWorkerGap: // 工人 < 目标（needA=工人目标）
                return Mathf.Clamp01((d.needA - k.workerCount) / Mathf.Max(1f, d.needA));
            case NeedKind.RebuildGap:    // P0 无损毁统计 → 占位低分（损毁配额系统 P1 接入）
                return 0f;
            case NeedKind.DefenseNeed:   // P0 占位 0：被攻击由常设底线⑭强制；邻国威胁归 P1 兵力目标
                return 0f;
            default: return 0f;
        }
    }

    /// <summary>二值可行性门控（D346）。不看需求连续量，硬条件不过 → 0 出局。
    /// 完整局批次口径修正：按 UtilityActionDef 成本镜像判资源（与门面执行同口径双保险）；
    /// ⑥招工人=粮付得起 aiRecruitFoodCost 且未达工人目标（AI 人口增长唯一通道）。</summary>
    private static bool Feasible(KingdomState k, UtilityActionDef d)
    {
        switch (d.id)
        {
            case UtilityAction.RecruitWorker:
            {
                // 招工人：粮 ≥ 招募成本（SO：aiRecruitFoodCost）且未达工人目标
                var bcfg = KingdomBrain.LoadConfig();
                return k.GetResourceValue(ResourceType.Food) >= Mathf.Max(1, bcfg.aiRecruitFoodCost)
                       && k.workerCount < d.needA;
            }
            case UtilityAction.BuildHouse:
            case UtilityAction.BuildWarehouse:
            case UtilityAction.BuildCapacity:
            case UtilityAction.BoostHarvest:
            case UtilityAction.Grain:
            {
                // 建造类：按 def 成本镜像逐项判国库（选址/前置等硬规则归执行门面二次校验）
                return k.GetResourceValue(ResourceType.Gold) >= d.costGold
                    && k.GetResourceValue(ResourceType.Stone) >= d.costStone
                    && k.GetResourceValue(ResourceType.Wood) >= d.costWood
                    && k.GetResourceValue(ResourceType.Food) >= d.costFood;
            }
            case UtilityAction.Rebuild:
            case UtilityAction.Defense:
                // 姿态项全阶段可见、无硬门槛（D318）
                return true;
            default:
                return false;
        }
    }

    /// <summary>某王国活跃建筑数（与 KingdomBrain.BuildContext 同口径；P0 产能下限近似）。</summary>
    private static int CountActiveBuildings(int kingdomId)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return 0;
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.kingdomId == kingdomId && b.IsActive) n++;
        }
        return n;
    }
}