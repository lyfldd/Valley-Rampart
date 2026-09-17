using System;
using UnityEngine;

/// <summary>
/// 地图生成规则配置（2_1 §5.3，2D 版）。
/// 温度带分布 / 出生点间距 / 资源保障半径 / 连通性阈值。
/// 资产实例放在 Resources/Grid/MapGenRulesConfig.asset
///
/// **HH.272（2_1_R1 §四）**：气候簇形状 / 资源配额（T ＋ 权重表 ＋ 保底 ＋ 难度系数）/ 主城净空区 /
/// 山脉化 全部 SO 化——原「硬编码表 + 死字段」消除（`climateFeatures` 死字段口径随本批作废）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/MapGenRulesConfig", fileName = "MapGenRulesConfig")]
public class MapGenRulesConfig : ScriptableObject
{
    /// <summary>每温度带 × 每资源的权重（w 越大越稀有；`1−w` 参与归一化配额）。位序＝Tree/StonePile/WoodPile/OreVein/Mine。</summary>
    [System.Serializable]
    public struct BandResourceWeights
    {
        [Range(0f, 1f)] public float tree;
        [Range(0f, 1f)] public float stonePile;
        [Range(0f, 1f)] public float woodPile;
        [Range(0f, 1f)] public float oreVein;
        [Range(0f, 1f)] public float mine;
    }

    [Header("温度带分布权重（热带/亚热带/温带/寒带，等概率占位 D1）")]
    [Tooltip("索引 0/1/2/3 = Tropical/Subtropical/Temperate/Cold")]
    public float[] climateWeights = new float[4] { 1f, 1f, 1f, 1f };

    [Header("气候层 · 群系形状（HH.272 件①：种子生长 + 噪声扰动 + 碎片清理）")]
    [Tooltip("气候簇硬底线：4-连通簇小于该值 ⇒ 整簇并入邻接最多的带（碎片清理阈值）")]
    public int clusterSizeMin = 4;
    [Tooltip("典型簇大小区间下限（格）")]
    public int clusterSizeTypicalMin = 96;
    [Tooltip("典型簇大小区间上限（格）")]
    public int clusterSizeTypicalMax = 384;
    [Tooltip("簇上限（格）：任一 4-连通簇超过该值即视为超限（生长期/归并期均受此约束）")]
    public int clusterSizeMax = 384;

    [Header("资源层 · 资源池（`03` §6.7 · `HH.294` 片 5）")]
    [Tooltip("每大区块**池子上限基准**（基准值·上限，非必须填满）。" +
             "实际上限 `cap(band, difficulty) = 本值 × 地带丰度 × 难度系数`（`D773` 裁：难度系数乘在 cap 上）。")]
    public int poolCapBase = 96;
    [Tooltip("开局初始铺量比例：`E_i^open = cap × p_i × 本值`（`03` §6.7「开局初始铺量 40%」，`D762`）。")]
    [Range(0f, 1f)] public float poolOpenRatio = 0.40f;
    [Tooltip("单类上限松弛系数：`capKind_i = cap × p_i × 本值`（`D773` 裁·可调）。")]
    public float kindCapRelax = 1.5f;
    [Tooltip("每温度带 × 每资源的权重表（索引 0/1/2/3 = 热带/亚热带/温带/寒带）。w 越大越稀有 ⇒ `1−w` 越小 ⇒ 占比越低。 **`D774` 落盘 `R-03` 方案 A′ 权重表**（热/亚热/温同表）。")]
    public BandResourceWeights[] resourceWeights = new BandResourceWeights[4]
    {
        // Tropical（`D774` 表）
        new BandResourceWeights { tree = 0.20f, stonePile = 0.50f, woodPile = 0.30f, oreVein = 0.70f, mine = 0.95f },
        // Subtropical（`D774` 表·同温带）
        new BandResourceWeights { tree = 0.20f, stonePile = 0.50f, woodPile = 0.30f, oreVein = 0.70f, mine = 0.95f },
        // Temperate（`D774`：tree 由 HH.291 A2 实盘的 0.25 降到 0.20 ⇒ 抬树占比）
        new BandResourceWeights { tree = 0.20f, stonePile = 0.50f, woodPile = 0.30f, oreVein = 0.70f, mine = 0.95f },
        // Cold：**只保留**「树最少 0.60」与「木堆不生成 1.00」（1−w=0 ⇒ 占比 0），其余同 `D774` 表
        new BandResourceWeights { tree = 0.60f, stonePile = 0.50f, woodPile = 1.00f, oreVein = 0.70f, mine = 0.95f },
    };
    [Tooltip("保底系数：B_i = floor(E_i^open × ratio)（开局目标口径）")]
    [Range(0f, 1f)] public float guaranteeRatio = 0.5f;
    [Tooltip("难度资源系数（索引 0/1/2 = Easy/Normal/Hard），_D773 裁：乘在 `cap` 上_")]
    public float[] difficultyResourceScale = new float[3] { 0.7f, 1.0f, 1.3f };
    [Tooltip("**HH.291 A3（R-01）** 地带资源丰度（索引 0/1/2/3 = 热带/亚热带/温带/寒带），乘在 T 上。" +
             "四带等概率 ⇒ 均值 1.0 ⇒ 全图总量基准不变、仅改变分布。")]
    public float[] bandResourceAbundance = new float[4] { 0.9f, 1.3f, 1.1f, 0.7f };

    [Header("山脉化（HH.272 件④：脊线生成 + 沿线扩宽 ⇒ 带状）")]
    [Tooltip("每温度带的山体格数占比（索引 0/1/2/3 = 热带/亚热带/温带/寒带；寒带最多、热带最少）")]
    public float[] mountainCellRatio = new float[4] { 0.05f, 0.08f, 0.12f, 0.16f };
    [Tooltip("山体中「雪山」占比（按该格自身温度带；寒带=1 全雪、热带=0 无雪）")]
    [Range(0f, 1f)] public float[] mountainSnowRatio = new float[4] { 0f, 0.05f, 0.30f, 1f };
    [Tooltip("脊线宽度下限（格，垂直走向方向）")]
    public int mountainRidgeWidthMin = 1;
    [Tooltip("脊线宽度上限（格）")]
    public int mountainRidgeWidthMax = 2;
    [Tooltip("脊线长度下限（格）")]
    public int mountainRidgeLengthMin = 6;
    [Tooltip("脊线长度上限（格）")]
    public int mountainRidgeLengthMax = 20;
    [Tooltip("山脉 4-连通簇最小尺寸（小于该值 ⇒ 碎片回落 Plain）")]
    public int mountainClusterMinSize = 4;

    [Header("主城净空区（HH.272 件③）")]
    [Tooltip("主城 footprint(3×3) 外扩格数 R ⇒ 净空区边长 = 3+2R = 11")]
    public int kingdomClearRadius = 4;

    [Header("出生点间距下限（按地图档位，D41：Small=24/Medium=32/Large=40）")]
    [Tooltip("索引 0/1/2 = Small/Medium/Large")]
    public int[] spawnMinDistanceCells = new int[3] { 24, 32, 40 };

    [Header("连通性")]
    [Tooltip("出生点彼此可达比例阈值，低于则打通走廊（占位 D257）")]
    [Range(0f, 1f)] public float connectivityThreshold = 0.95f;

    [Header("威胁刷点")]
    [Tooltip("每个王国出生点外的威胁刷点数（基础，随难度可放大）")]
    public int threatsPerKingdom = 2;
    [Tooltip("威胁刷点距出生点的最小大区块数（视野外）")]
    public int threatMinChunkDistance = 2;

    [Header("立国选址特征匹配（2_22 P0 批D / D5，D316 悬空转正→DZ-080）")]
    [Tooltip("RiverAdjacent 特征判定半径（格）：半径内存在 River/Lake/Ocean 水格即命中")]
    public int featureScanRadiusCells = 8;
    [Tooltip("ForestDense 命中阈值：候选点所在大区块(16×16)林木(Tree)格占比 ≥ 该值")]
    [Range(0f, 1f)] public float forestDensityThreshold = 0.10f;
    [Tooltip("MineralRich 命中阈值：候选点所在大区块(16×16)矿(Mine)格占比 ≥ 该值。" +
             "D621③ 重标定（件12 矿山簇化后）：原 0.05（≥13 矿格/区块）在簇化分布下命中率降约 18%（196.5→160.8/256）；" +
             "0.039（≥10 矿格/区块）实测 205.8/256，与改前 196.5 最近（+4.7%，优于 0.040 的 -5.6%）。")]
    [Range(0f, 1f)] public float mineralDensityThreshold = 0.039f;
    [Tooltip("BarrenRich 命中阈值：候选点所在大区块开阔地(Plain)格占比 ≥ 该值")]
    [Range(0f, 1f)] public float barrenDensityThreshold = 0.60f;

    // ===== 查表辅助 =====

    /// <summary>按地图档位查出生点间距下限。</summary>
    public int GetSpawnMinDistance(WorldSize size)
    {
        int idx = (int)size;
        if (spawnMinDistanceCells != null && idx >= 0 && idx < spawnMinDistanceCells.Length
            && spawnMinDistanceCells[idx] > 0)
            return spawnMinDistanceCells[idx];
        return 32;
    }

    /// <summary>按温度带查分布权重（缺省 1）。</summary>
    public float GetClimateWeight(ClimateZone zone)
    {
        int idx = (int)zone;
        if (climateWeights != null && idx >= 0 && idx < climateWeights.Length)
            return Mathf.Max(0f, climateWeights[idx]);
        return 1f;
    }

    /// <summary>按温度带查资源权重行（位序＝Tree/StonePile/WoodPile/OreVein/Mine；缺省 0.5）。</summary>
    public float[] GetResourceWeights(ClimateZone zone)
    {
        int idx = (int)zone;
        var dst = new float[5] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
        if (resourceWeights == null || idx < 0 || idx >= resourceWeights.Length) return dst;
        var r = resourceWeights[idx];
        dst[0] = r.tree; dst[1] = r.stonePile; dst[2] = r.woodPile; dst[3] = r.oreVein; dst[4] = r.mine;
        return dst;
    }

    /// <summary>按温度带查资源丰度（**HH.291 A3**；缺省 1）。</summary>
    public float GetBandAbundance(ClimateZone zone)
    {
        int idx = (int)zone;
        if (bandResourceAbundance != null && idx >= 0 && idx < bandResourceAbundance.Length)
            return Mathf.Max(0f, bandResourceAbundance[idx]);
        return 1f;
    }

    /// <summary>按温度带查山体格数占比（缺省 0.1）。</summary>
    public float GetMountainCellRatio(ClimateZone zone)
    {
        int idx = (int)zone;
        if (mountainCellRatio != null && idx >= 0 && idx < mountainCellRatio.Length)
            return Mathf.Max(0f, mountainCellRatio[idx]);
        return 0.1f;
    }

    /// <summary>按温度带查山体中雪山占比（缺省 0.3）。</summary>
    public float GetMountainSnowRatio(ClimateZone zone)
    {
        int idx = (int)zone;
        if (mountainSnowRatio != null && idx >= 0 && idx < mountainSnowRatio.Length)
            return Mathf.Clamp01(mountainSnowRatio[idx]);
        return 0.3f;
    }
}
