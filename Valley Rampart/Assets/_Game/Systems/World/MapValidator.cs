using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图生成后验证器（3.2.1 第十节验证清单）。
/// 在 GenerateMap 返回后调用 Validate，检查硬约束是否满足。
/// 检查项 1-5 为硬约束，6（资源点距离）为 P2 软约束暂不实现，
/// 7（确定性）由设计保证，8（敌方地图）TODO。
/// </summary>
public static class MapValidator
{
    public enum Severity { Error, Warning, Info }

    public struct ValidationIssue
    {
        public Severity severity;
        public string checkName;
        public string message;

        public override string ToString()
            => $"[{severity}] {checkName}: {message}";
    }

    /// <summary>验证整张地图，返回问题列表（空=全部通过）。</summary>
    public static List<ValidationIssue> Validate(MapData map, MapGenRulesConfig rulesConfig = null)
    {
        var issues = new List<ValidationIssue>();
        if (map == null || map.regions == null || map.regions.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                severity = Severity.Error,
                checkName = "地图非空",
                message = "map 或 regions 为空"
            });
            return issues;
        }

        CheckCastlePosition(map, issues);
        CheckResourceCoverage(map, issues, rulesConfig);
        CheckExtremeTerrain(map, issues);
        CheckRifts(map, issues);
        CheckAdjacency(map, issues, rulesConfig);

        return issues;
    }

    // ========================================================================
    //  检查项 1: 废弃城堡位置（3.2.1 第 2.4 节）
    // ========================================================================

    static void CheckCastlePosition(MapData map, List<ValidationIssue> issues)
    {
        int M = map.regions.Count;
        int castleIdx = MapGenRules.GetCastleRegionIndex(M);

        // castleIdx 区域应有 CastleCore
        bool foundCastle = false;
        var region = map.regions[castleIdx];
        if (region.resources != null)
        {
            foreach (var b in region.resources)
            {
                if (b.type == BuildingType.CastleCore)
                {
                    foundCastle = true;
                    break;
                }
            }
        }

        if (!foundCastle)
            issues.Add(new ValidationIssue
            {
                severity = Severity.Error,
                checkName = "废弃城堡位置",
                message = $"区域 {castleIdx} 未找到 CastleCore 占位"
            });

        // 城堡区域应为平原
        if (region.terrain != TerrainType.Plain)
            issues.Add(new ValidationIssue
            {
                severity = Severity.Warning,
                checkName = "废弃城堡位置",
                message = $"区域 {castleIdx} 地形为 {region.terrain}，期望 Plain"
            });
    }

    // ========================================================================
    //  检查项 2: 四资源保障（3.2.1 第四节）
    // ========================================================================

    static void CheckResourceCoverage(MapData map, List<ValidationIssue> issues, MapGenRulesConfig cfg)
    {
        int minForest = cfg != null ? cfg.minForest : 1;
        int minStone = cfg != null ? cfg.minStone : 1;
        int minFertile = cfg != null ? cfg.minFertile : 1;

        int forestCount = 0, quarryCount = 0, fertileCount = 0;
        foreach (var r in map.regions)
        {
            if (r.terrain == TerrainType.Forest) forestCount++;
            if (r.terrain == TerrainType.Quarry) quarryCount++;
            if (r.terrain == TerrainType.Plain && r.plainSubState == PlainSubState.Fertile) fertileCount++;
        }

        if (forestCount < minForest)
            issues.Add(new ValidationIssue
            {
                severity = Severity.Error,
                checkName = "四资源保障",
                message = $"林地数量 {forestCount} < 下限 {minForest}"
            });

        if (quarryCount < minStone)
            issues.Add(new ValidationIssue
            {
                severity = Severity.Error,
                checkName = "四资源保障",
                message = $"矿山数量 {quarryCount} < 下限 {minStone}"
            });

        if (fertileCount < minFertile)
            issues.Add(new ValidationIssue
            {
                severity = Severity.Warning,
                checkName = "四资源保障",
                message = $"肥沃平原数量 {fertileCount} < 下限 {minFertile}"
            });
    }

    // ========================================================================
    //  检查项 3: 出门地形（3.2.1 第三 + 6.10 节）
    // ========================================================================

    static void CheckExtremeTerrain(MapData map, List<ValidationIssue> issues)
    {
        int M = map.regions.Count;
        var leftRegion = map.regions[0];
        var rightRegion = map.regions[M - 1];

        if (map.bigTerrain == BigTerrain.Island)
        {
            // 岛屿：两端应为海岸
            if (leftRegion.terrain != TerrainType.Coast)
                issues.Add(new ValidationIssue
                {
                    severity = Severity.Warning,
                    checkName = "出门地形",
                    message = $"岛屿左端为 {leftRegion.terrain}，期望 Coast"
                });
            if (rightRegion.terrain != TerrainType.Coast)
                issues.Add(new ValidationIssue
                {
                    severity = Severity.Warning,
                    checkName = "出门地形",
                    message = $"岛屿右端为 {rightRegion.terrain}，期望 Coast"
                });
        }
        else
        {
            // 内陆：左端雪山，右端荒地
            if (leftRegion.terrain != TerrainType.Snow)
                issues.Add(new ValidationIssue
                {
                    severity = Severity.Warning,
                    checkName = "出门地形",
                    message = $"内陆左端为 {leftRegion.terrain}，期望 Snow"
                });
            if (rightRegion.terrain != TerrainType.Wasteland)
                issues.Add(new ValidationIssue
                {
                    severity = Severity.Warning,
                    checkName = "出门地形",
                    message = $"内陆右端为 {rightRegion.terrain}，期望 Wasteland"
                });
        }
    }

    // ========================================================================
    //  检查项 4: 出怪口（3.2.1 第 6.10 节）
    // ========================================================================

    static void CheckRifts(MapData map, List<ValidationIssue> issues)
    {
        int M = map.regions.Count;
        var leftRegion = map.regions[0];
        var rightRegion = map.regions[M - 1];

        if (map.bigTerrain == BigTerrain.Island)
        {
            // 岛屿：两端都应有裂隙
            if (leftRegion.riftCellX < 0)
                issues.Add(new ValidationIssue
                {
                    severity = Severity.Error,
                    checkName = "出怪口",
                    message = "岛屿左端无裂隙"
                });
            if (rightRegion.riftCellX < 0)
                issues.Add(new ValidationIssue
                {
                    severity = Severity.Error,
                    checkName = "出怪口",
                    message = "岛屿右端无裂隙"
                });
        }
        else
        {
            // 内陆：仅右端有裂隙，左端无
            if (rightRegion.riftCellX < 0)
                issues.Add(new ValidationIssue
                {
                    severity = Severity.Error,
                    checkName = "出怪口",
                    message = "内陆右端无裂隙"
                });
            if (leftRegion.riftCellX >= 0)
                issues.Add(new ValidationIssue
                {
                    severity = Severity.Warning,
                    checkName = "出怪口",
                    message = "内陆左端不应有裂隙"
                });
        }
    }

    // ========================================================================
    //  检查项 5: 地形过渡（3.2.1 第五节邻接矩阵）
    // ========================================================================

    static void CheckAdjacency(MapData map, List<ValidationIssue> issues, MapGenRulesConfig cfg)
    {
        if (cfg == null) return;

        for (int i = 0; i < map.regions.Count - 1; i++)
        {
            var a = map.regions[i].terrain;
            var b = map.regions[i + 1].terrain;

            // 区交界用严格模式（△ 算违规）
            if (!cfg.CanAdjacency(a, b, strict: true))
                issues.Add(new ValidationIssue
                {
                    severity = Severity.Warning,
                    checkName = "地形过渡",
                    message = $"区域 {i}({a}) 与 {i + 1}({b}) 邻接违规"
                });
        }
    }
}
