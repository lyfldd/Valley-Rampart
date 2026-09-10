using System;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  AI 建筑选址打分器配置 SO（2_22 P0 批D / D1，D525 §3.7；§八 SO 清单）。
//  资产路径：Resources/Config/Kingdoms/BuildingPlacementConfig.asset（so-data-driven 禁魔法数）。
//  消费方：PlacementScorer.Pick（D2 候选打分；ExecuteBuildFocus 选址半边升级=D3）。
//
//  结构（D519 模式预留 champion 所有权）：特征清单/关联表/安全栏（半径上限）归策划，
//  权重出厂占位 1.0，未来可归训练（factor_registry 草案登记 S-8「Unity 单侧消费」，
//  本批不进训练=E3 批E 域）。
//
//  三类特征（2_22 §3.7 表）：
//    F1 距威胁边境=军事类建筑（练兵场/战营/射箭场等；熔炉=经济向不配）——消费态势层威胁
//      分布，主威胁方向锚越近分越高；威胁≈0 时该特征置 0（退化通用）。
//    F2 距关联建筑=经济类（per-def 关联表；具体对以 BuildingDef 清单核定：农田↔粮仓、
//      矿类/加工类↔仓库）——越近分越高。
//    F3 距主城+同类密度=通用（含城墙）——紧凑布局防摊大饼+同类过密降分（功能区块自分散）。
//  边界注记（D4）：打分器=Unity 侧执行层零镜像（sim 无空间概念）；玩家建造入口/UI/校验零改动。
// ============================================================================

[CreateAssetMenu(menuName = "ValleyRampart/BuildingPlacementConfig", fileName = "BuildingPlacementConfig")]
public class BuildingPlacementConfig : ScriptableObject
{
    /// <summary>per 建筑类型选址规则（buildingId 精确对齐 BuildingDef.id，大小写敏感）。</summary>
    [Serializable]
    public class PlacementRule
    {
        [Tooltip("建筑 def id（对齐 BuildingDef.id；如 Barracks/farm/Granary）")]
        public string buildingId;

        [Tooltip("F1 距威胁边境权重（军事类=1.0；非军事类=0 关闭）")]
        public float w1ThreatFront = 1f;

        [Tooltip("F2 距关联建筑权重（经济类=1.0；无关联=0 关闭）")]
        public float w2LinkBuilding = 1f;

        [Tooltip("F3 距主城紧凑权重（通用；0=关闭紧凑项）")]
        public float w3CastleCompact = 1f;

        [Tooltip("F2 关联建筑 def id（空=该 def 不启用 F2；如 farm→Granary、mine→Warehouse）")]
        public string linkBuildingId;

        [Tooltip("同类密度惩罚（F3：密度半径内本国同类建筑数×该值扣分；0=不惩罚）")]
        public float sameTypeDensityPenalty = 0.5f;
    }

    [Header("per 建筑类型规则（未配置的 def 走下方出厂默认权重）")]
    public List<PlacementRule> rules = new List<PlacementRule>();

    [Header("出厂默认权重（未配置 def 的占位 1.0=D525；紧凑语义=现状等价）")]
    public float defaultW1 = 1f;
    public float defaultW2 = 1f;
    public float defaultW3 = 1f;

    [Header("特征归一化参数")]
    [Tooltip("F3 同类密度统计半径（格；本国同类 Active 建筑在半径内计数）")]
    public int densityRadiusCells = 5;

    /// <summary>按建筑 id 查规则（线性扫+CompareOrdinal 确定性；未配置 → null=走默认权重）。</summary>
    public PlacementRule Find(string buildingId)
    {
        if (rules == null || string.IsNullOrEmpty(buildingId)) return null;
        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (r != null && !string.IsNullOrEmpty(r.buildingId)
                && string.CompareOrdinal(r.buildingId, buildingId) == 0) return r;
        }
        return null;
    }

    /// <summary>载入选址打分配置（缺 asset 时回退默认占位实例；对齐 SituationConfig.Load 先例）。</summary>
    public static BuildingPlacementConfig Load()
    {
        var cfg = Resources.Load<BuildingPlacementConfig>("Config/Kingdoms/BuildingPlacementConfig");
        return cfg != null ? cfg : ScriptableObject.CreateInstance<BuildingPlacementConfig>();
    }
}
