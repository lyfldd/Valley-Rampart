using UnityEngine;

/// <summary>
/// 放置校验器（3.3 第五节）。静态工具类，校验 BuildingDef 能否放在指定坐标。
/// 5 项校验：区块占用 / 地形合法 / 阻挡冲突 / 资源足够 / 边界范围。
/// </summary>
public static class PlacementValidator
{
    /// <summary>校验能否放置。返回 true=可放（绿），false=不可放（红）。</summary>
    public static bool Validate(BuildingDef def, GridCoord origin)
    {
        if (def == null) return false;
        var grid = GridSystem.Instance;
        if (grid == null) return false;

        int width = def.footprint.x > 0 ? def.footprint.x : 1;
        int height = def.footprint.y > 0 ? def.footprint.y : 1;

        // 5. 边界范围（先查，越界后面没意义）
        int mapCells = grid.MapCellCount;
        if (mapCells > 0 && (origin.x < 0 || origin.x + width > mapCells))
            return false;

        // 资源点校验（3.3.4 批次6）：工具建筑必须建在对应资源点上
        bool needsNode = ResourceNodeMapping.RequiresResourceNode(def.id);
        BuildingType? requiredNode = needsNode ? ResourceNodeMapping.GetResourceNode(def.id) : null;

        // 二维遍历（3.3.4 批次8）
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                var coord = new GridCoord(origin.x + dx, origin.y + dy);

                // 1. 区块占用（工具建筑允许建在对应资源点上）
                var occupant = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(coord) : null;
                if (occupant != null)
                {
                    if (needsNode)
                    {
                        // 工具建筑：占用者必须是对应资源点
                        if (occupant.sourceType != requiredNode.Value) return false;
                        // 是对应资源点，允许放置（跳过占用拒绝）
                    }
                    else
                    {
                        return false; // 非工具建筑，占用即拒绝
                    }
                }
                else if (needsNode)
                {
                    return false; // 工具建筑但格上无资源点
                }

                // 2. 地形合法
                if (def.allowedTerrain != null && def.allowedTerrain.Length > 0)
                {
                    var terrain = grid.GetTerrainAt(coord);
                    bool ok = false;
                    for (int t = 0; t < def.allowedTerrain.Length; t++)
                    {
                        if (def.allowedTerrain[t] == terrain) { ok = true; break; }
                    }
                    if (!ok) return false;
                }

                // 3. 阻挡冲突（已有 obstacle 建筑重叠）
                if (grid.IsObstacle(coord))
                    return false;
            }
        }

        // 4. 资源足够
        if (RulerController.Instance != null && !RulerController.Instance.CanAfford(def.cost))
            return false;

        return true;
    }
}
