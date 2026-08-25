using System;
using UnityEngine;

// ============================================================================
//  王国立国全局数值配置（2_16 步骤4，§3.4 数值占位表全量落点；占位可调）
//  占位数值全落 SO（so-data-driven 铁律），禁硬编码魔法数。
//  资产路径：Resources/Config/Kingdoms/KingdomFoundingConfig.asset
// ============================================================================

/// <summary>错峰预置档（步骤5 第一代立国按此预置实例化，§3.4/D293/D300/D304/D310）。</summary>
[Serializable]
public class StaggerTier
{
    /// <summary>档名（帐篷/村落/要塞）。</summary>
    public string tierName;
    /// <summary>规模缩放系数（简单 60% 帐篷级 / 普通 100% 村落级 / 困难 160% 要塞级；围墙计入总量锚 D310）。</summary>
    public float scale;
    /// <summary>该档工人数（步骤5 用，替代 baseWorkers×scale 的档内定值）。</summary>
    public int workerCount;
    /// <summary>该档战士数。</summary>
    public int warriorCount;
    /// <summary>该档建筑数（取 KingdomDef.baseBuildingDefIds 前 N 个实例化）。</summary>
    public int buildingCount;
    /// <summary>该档是否含围墙环（困难档最小矩形环+1 城门缺口 D304；遇阻挡格跳段时间自然缺口）。</summary>
    public bool hasWallRing;
    /// <summary>该档起始国库（baseStockpile 过渡账本 D300；2_17 步骤2 WarehouseRegistry 迁移吸收）。</summary>
    public ResourcePack stockpile;
}

/// <summary>
/// 王国立国全局数值配置（2_16 步骤4，§3.4 占位表）。
/// 服务：GetEnemyMapBase 重锚档位（D288）、初始流民预置（D308）、立国阈值/冷却/上限（D294/D312/D314）、
///       扰动幅度（D290/D295）、性格 clamp（§四）、聚集地评分权重（步骤10）。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/Kingdoms/KingdomFoundingConfig", fileName = "KingdomFoundingConfig")]
public class KingdomFoundingConfig : ScriptableObject
{
    [Header("AI 数量档位（D288，替换 MapSizeConfig 1D enemyByDifficulty 语义）")]
    [Tooltip("Small 地图 AI 数区间（档内随机 rng 种子化）")]
    public Vector2Int aiCountSmall = new Vector2Int(2, 3);
    [Tooltip("Medium 地图 AI 数区间")]
    public Vector2Int aiCountMedium = new Vector2Int(3, 4);
    [Tooltip("Large 地图 AI 数区间")]
    public Vector2Int aiCountLarge = new Vector2Int(4, 6);

    [Header("扰动与性格（§四/D290/D295）")]
    [Tooltip("第一代四维数值扰动幅度（±20%，rng）")]
    public float firstGenPerturbation = 0.20f;
    [Tooltip("动态立国性格混合扰动幅度（±10%）")]
    public float dynamicPerturbation = 0.10f;
    [Tooltip("性格五轴 clamp 下限")]
    public float personalityClampMin = 0.05f;
    [Tooltip("性格五轴 clamp 上限")]
    public float personalityClampMax = 0.95f;

    [Header("立国阈值/冷却/上限（D294/D312/D314）")]
    [Tooltip("营地立国所需流浪汉数")]
    public int foundingThresholdVagrants = 12;
    [Tooltip("营地存续所需天数")]
    public int foundingPersistenceDays = 5;
    [Tooltip("全局立国冷却（日，D312，冷却期不插旗、营地继续生长）")]
    public int foundingCooldownDays = 10;
    [Tooltip("全局王国上限（Registry.Count 含玩家，D280/D314）")]
    public int maxKingdomsGlobal = 8;

    [Header("营地结/散营（D301/D387 滞回带 [2,3)）")]
    [Tooltip("结营阈值：营地关联半径内未招募流浪汉 ≥ 此值判定结营")]
    public int campFoundThreshold = 3;
    [Tooltip("散营阈值：成员 < 此值散营（D387 <2；滞回带 2~3 保留不散，杀散才是真阻止）")]
    public int campDisbandThreshold = 2;

    [Header("初始流民预置（D308，地图级口径）")]
    [Tooltip("全图预置流浪汉总数下限")]
    public int initialVagrantTotalMin = 4;
    [Tooltip("全图预置流浪汉总数上限")]
    public int initialVagrantTotalMax = 6;
    [Tooltip("保底同点人数（早期必结营，余散投无主地）")]
    public int baselineGroupSize = 3;

    [Header("错峰三档预置（§3.4/D293/D300/D304/D310，steps4→5）")]
    public StaggerTier[] staggerTiers;

    [Header("聚集地评估权重（步骤10，复用 SafetyScore 思路）")]
    [Tooltip("评分权重 x=无主 y=资源邻近 z=食物邻近（Vector3，步骤10 占位；2_17 前无主恒真）")]
    public Vector3 gatherScoreWeights = new Vector3(1f, 1f, 1f);
    [Tooltip("聚集地候选集刷新间隔（秒，防每帧扫描；对齐 10~20s 锚点刷新惯例）")]
    public float gatherCandidateRefreshSeconds = 15f;
    [Tooltip("聚集地评分影响半径（格）：资源/食物邻近分衰减到 0 的半径上限，距离越近分越高")]
    public float gatherInfluenceRadiusCells = 8f;

    /// <summary>按地图档位取 AI 数量区间（D288；worldSize 转档）。</summary>
    public Vector2Int GetAiCountRange(WorldSize size)
    {
        switch (size)
        {
            case WorldSize.Small: return aiCountSmall;
            case WorldSize.Large: return aiCountLarge;
            default: return aiCountMedium;
        }
    }
}