using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  2_16 步骤10 - 聚集地评分 helper（纯 C# 评分，不触单例/世界读写）
//  详见 2_16_AI王国出生与初始条件_实施计划.md 步骤10（§二 老系统改造）
//  背景：未招募流浪汉原在出生营地小半径徘徊，改造为"偏好聚集地"——按评分
//        加权抽锚点，流民自然聚到潜在建国点（后续步骤11 结营/立国的地理前兆）。
//  分项（复用 SafetyScore 思路，§二）：
//    无主（2_17 TerritorySystem 落地前恒真，占位）
//    + 资源邻近（废墟/采集点距离）
//    + 食物邻近（浆果/农田距离）
//  权重落 SO（KingdomFoundingConfig.gatherScoreWeights），影响半径落 gatherInfluenceRadiusCells。
//  评分纯函数：建筑点位由调用方（WanderStimulusProvider）从 BuildingRegistry 传入，
//  本类不含 UnityEngine.Random 之外的随机（游戏玩法随机，R4 世界生成纪律不受影响）。
// ============================================================================

/// <summary>
/// 聚集地评分 helper：对候选点评分 + 按评分加权抽点。
/// 供未招募流浪汉的漫游锚点选择使用（步骤10）。
/// </summary>
public static class VagrantGatherSiteEvaluator
{
    /// <summary>
    /// 综合评分 = 无主×wx + 资源邻近×wy + 食物邻近×wz + 同族聚集×wSame（D468 同族结伙，HH.51 批C）。
    /// owner 恒真（2_17 前）→ 在为候选整体提供恒定基线分；资源/食物/同族聚集为距离衰减 0-1。
    /// sameRaceSites=null 或权重 0 → 同族分数项 0（行为回退步骤10 原口径）。
    /// </summary>
    public static float ScoreSite(Vector2 site, List<Vector2> resourceSites, List<Vector2> foodSites,
        KingdomFoundingConfig cfg, float cellSize, List<Vector2> sameRaceSites = null)
    {
        float wx = cfg.gatherScoreWeights.x;
        float wy = cfg.gatherScoreWeights.y;
        float wz = cfg.gatherScoreWeights.z;
        float wSame = cfg.gatherSameRaceWeight;
        float influence = Max1(cfg.gatherInfluenceRadiusCells) * Max1(cellSize);

        float owner = 1f;   // 无主：2_17 TerritorySystem 落地前恒真（占位，见跨片注记）
        float resource = NearestProximity(site, resourceSites, influence);
        float food = NearestProximity(site, foodSites, influence);
        // D468 同族结伙（2_20 §十二矩阵：无国×同族→结伙）：候选点邻近同族流浪汉加聚集分
        // （异族不加分——野性敌意下异族聚集地不可共处）
        float sameRace = wSame > 0f ? NearestProximity(site, sameRaceSites, influence) : 0f;
        return wx * owner + wy * resource + wz * food + wSame * sameRace;
    }

    /// <summary>距候选最近的资源/食物点邻近分（距离越近越高，≥influence 为 0）。</summary>
    static float NearestProximity(Vector2 site, List<Vector2> sites, float influence)
    {
        if (sites == null || sites.Count == 0) return 0f;
        float inf2 = influence * influence;
        float best = inf2;
        for (int i = 0; i < sites.Count; i++)
        {
            float d2 = (sites[i] - site).sqrMagnitude;
            if (d2 < best) best = d2;
        }
        return Mathf.Clamp01(1f - Mathf.Sqrt(best) / influence);
    }

    /// <summary>按评分加权抽 1 个候选点（权重=评分+0.01 保底，防全零分无法抽）。候选须非空。</summary>
    public static Vector2 PickWeighted(List<Vector2> candidates, List<Vector2> resourceSites,
        List<Vector2> foodSites, KingdomFoundingConfig cfg, float cellSize, List<Vector2> sameRaceSites = null)
    {
        int n = candidates.Count;
        float total = 0f;
        var cum = new float[n];
        for (int i = 0; i < n; i++)
        {
            total += ScoreSite(candidates[i], resourceSites, foodSites, cfg, cellSize, sameRaceSites) + 0.01f;
            cum[i] = total;
        }
        float roll = Random.value * total;
        for (int i = 0; i < n; i++)
            if (roll <= cum[i]) return candidates[i];
        return candidates[n - 1];
    }

    static float Max1(float v) => v > 1f ? v : 1f;
}