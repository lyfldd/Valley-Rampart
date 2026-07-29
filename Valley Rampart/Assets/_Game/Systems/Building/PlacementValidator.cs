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

        // 5. 边界范围（先查，越界后面没意义）
        int mapCells = grid.MapCellCount;
        if (mapCells > 0 && (origin.x < 0 || origin.x + width > mapCells))
            return false;

        for (int i = 0; i < width; i++)
        {
            var coord = new GridCoord(origin.x + i, origin.y);

            // 1. 区块占用
            if (grid.IsOccupied(coord))
                return false;

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

        // 4. 资源足够
        if (RulerController.Instance != null && !RulerController.Instance.CanAfford(def.cost))
            return false;

        return true;
    }
}
