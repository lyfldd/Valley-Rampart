using System;
using UnityEngine;

/// <summary>
/// 地图生成规则配置（2_1 §5.3，2D 版）。
/// 温度带分布 / 出生点间距 / 资源保障半径 / 连通性阈值。
/// 资产实例放在 Resources/Grid/MapGenRulesConfig.asset
///
/// 注：§3.3 特征物密度表是固定设计表，内置于 MapGenRules（保证确定性），
/// 本 SO 承载可调标量（温度带权重/出生点间距/资源半径/连通阈值）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/MapGenRulesConfig", fileName = "MapGenRulesConfig")]
public class MapGenRulesConfig : ScriptableObject
{
    [Header("温度带分布权重（热带/亚热带/温带/寒带，等概率占位 D1）")]
    [Tooltip("索引 0/1/2/3 = Tropical/Subtropical/Temperate/Cold")]
    public float[] climateWeights = new float[4] { 1f, 1f, 1f, 1f };

    [Header("出生点间距下限（按地图档位，D41：Small=24/Medium=32/Large=40）")]
    [Tooltip("索引 0/1/2 = Small/Medium/Large")]
    public int[] spawnMinDistanceCells = new int[3] { 24, 32, 40 };

    [Header("资源保障")]
    [Tooltip("就近补资源半径（大区块数，§5.2 步骤4）")]
    public int resourceGuaranteeRadius = 3;

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
    [Tooltip("MineralRich 命中阈值：候选点所在大区块矿(Mine)格占比 ≥ 该值")]
    [Range(0f, 1f)] public float mineralDensityThreshold = 0.05f;
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
}
