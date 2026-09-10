using System;
using UnityEngine;

// ============================================================================
//  必须建筑清单 SO（2_23 资源 P0 批A / R-A3，2_23 §3.3「断链自愈的判据」）
//  资产路径：Resources/Config/MustHaveConfig.asset
//  职责：per 锚点（错峰档/人口规模）的必须建筑基线（≥1 farm per 5 人 / ≥1 Warehouse /
//        城墙目标数[⑨既有]）；诊断层日 tick 比对 → 缺失清单进快照 → 断链缺口注入（批C R-C3）。
//  锚点口径注记：运行时无 per-kingdom 错峰档读口（KingdomState 不存 staggerTier；
//        仅 KingdomFoundry 立国时按全局 difficulty 分解消费，实盘复核见 HH.169 列报）
//        ⇒ 本批以**人口规模档**为锚点主轴；错峰档字段留结构待后续批接入。
//  数值禁区：出厂档值=保守占位（断链检测本批零行为消费=R-C3 才注入），实值归 P0 调优轮。
// ============================================================================

[CreateAssetMenu(menuName = "ValleyRampart/MustHaveConfig", fileName = "MustHaveConfig")]
public class MustHaveConfig : ScriptableObject
{
    /// <summary>必须建筑基线档（per 锚点；人口规模档主轴）。</summary>
    [Serializable]
    public class TierRule
    {
        [Tooltip("档名（诊断可读性；不影响判定）")]
        public string tierName = "村落";

        [Tooltip("锚点·人口规模下限（本王国 工人+战士）：取 minPopulation ≤ 人口 的最大档（确定性）")]
        public int minPopulation = 0;

        [Tooltip("每 N 人 ≥1 farm（≥1 farm per 5 人基线）")]
        public int farmPerPeople = 5;

        [Tooltip("必需仓储建筑数下限（≥1 Warehouse）")]
        public int minWarehouse = 1;

        [Tooltip("城墙目标数（⑨修工事既有口径=UtilityActionConfig.needA；本 SO 为诊断层基线，数值待批C 对齐）")]
        public int wallTargetCount = 0;

        [Tooltip("锚点·错峰档下限（staggerTier 0/1/2）：运行时无 per-kingdom 读口，-1=不使用（留结构待后续批）")]
        public int staggerTierMin = -1;
    }

    [Tooltip("基线档表（按 minPopulation 升序配置；命中=minPopulation ≤ 人口 的最大档）")]
    public TierRule[] tiers = new TierRule[]
    {
        new TierRule { tierName = "村庄", minPopulation = 0,  farmPerPeople = 5, minWarehouse = 1, wallTargetCount = 0 },
        new TierRule { tierName = "村落", minPopulation = 6,  farmPerPeople = 5, minWarehouse = 1, wallTargetCount = 2 },
        new TierRule { tierName = "要塞", minPopulation = 12, farmPerPeople = 5, minWarehouse = 1, wallTargetCount = 4 },
    };

    /// <summary>按人口规模解析命中档 index（确定性：取 minPopulation ≤ pop 的最大档；无命中=0）。</summary>
    public int ResolveTierIndex(int population)
    {
        if (tiers == null || tiers.Length == 0) return -1;
        int best = -1;
        int bestMin = int.MinValue;
        for (int i = 0; i < tiers.Length; i++)
        {
            var t = tiers[i];
            if (t == null) continue;
            if (t.minPopulation <= population && t.minPopulation > bestMin)
            {
                bestMin = t.minPopulation;
                best = i;
            }
        }
        return best >= 0 ? best : 0;
    }

    /// <summary>解析为诊断块基线 DTO（farm 需求按人口折算；无档=全 0=不断链）。</summary>
    public MustHaveBaseline ResolveBaseline(int population)
    {
        int idx = ResolveTierIndex(population);
        if (idx < 0 || tiers == null || idx >= tiers.Length || tiers[idx] == null) return default;

        var t = tiers[idx];
        int per = Math.Max(1, t.farmPerPeople);
        return new MustHaveBaseline
        {
            TierIndex = idx,
            FarmRequired = (int)Math.Ceiling(population / (double)per),
            WarehouseRequired = Math.Max(0, t.minWarehouse),
            FortRequired = Math.Max(0, t.wallTargetCount),
            RuleCount = 3
        };
    }

    /// <summary>载入（缺 asset 时回退默认占位实例；对齐 SituationConfig.Load 先例）。</summary>
    public static MustHaveConfig Load()
    {
        var cfg = Resources.Load<MustHaveConfig>("Config/MustHaveConfig");
        return cfg != null ? cfg : CreateInstance<MustHaveConfig>();
    }
}
