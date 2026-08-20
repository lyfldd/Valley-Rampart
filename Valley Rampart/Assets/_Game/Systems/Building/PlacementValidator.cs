using UnityEngine;

/// <summary>城门朝向（2_2 §3.4）：横墙→横向 2 格；竖墙→纵向 2 格。</summary>
public enum GateOrientation { Horizontal, Vertical }

/// <summary>放置失败原因（2_2 §5.1）。</summary>
public enum PlacementFailReason
{
    None,
    Blocked,        // 占用/阻挡冲突
    Terrain,        // 地形不合法
    Resource,       // 资源不足
    Bounds,         // 越界
    GateCorner,     // 城门放拐角
    WaterMismatch   // 桥不在水上 / 非桥压水
}

/// <summary>放置校验结果（2_2 §5.1）。</summary>
public struct PlacementResult
{
    public bool ok;
    public PlacementFailReason reason;
    public GridCoord snappedOrigin;   // 吸附后的 cell origin（预览用）
}

/// <summary>
/// 放置校验器（2_2 建筑与占格，微格吸附 + footprint 全覆盖 + 桥水域特例）。
/// 静态工具类。旧 1D 的 <see cref="Validate(BuildingDef, GridCoord)"/> 保留为兼容包装。
/// </summary>
public static class PlacementValidator
{
    /// <summary>微格吸附后校验（含桥的水域特例）。subOrigin 为吸附后的微格坐标。</summary>
    public static PlacementResult ValidatePlacement(BuildingDef def, GridCoord subOrigin, GateOrientation orient)
    {
        var result = new PlacementResult { ok = false, reason = PlacementFailReason.Blocked };
        var grid = GridSystem.Instance;
        if (def == null || grid == null) return result;

        int w = def.footprint.x > 0 ? def.footprint.x : 1;
        int h = def.footprint.y > 0 ? def.footprint.y : 1;
        if (def.rotatable && orient == GateOrientation.Vertical) { int t = w; w = h; h = t; }

        var origin = grid.SubToCell(subOrigin);
        result.snappedOrigin = origin;

        // 资源点校验（工具建筑必须建在对应资源点上）
        bool needsNode = ResourceNodeMapping.RequiresResourceNode(def.id);
        BuildingType? requiredNode = needsNode ? ResourceNodeMapping.GetResourceNode(def.id) : null;

        for (int dy = 0; dy < h; dy++)
        {
            for (int dx = 0; dx < w; dx++)
            {
                var coord = new GridCoord(origin.x + dx, origin.y + dy);

                // 边界（doc 1 越界 nullable 语义）
                if (!grid.IsInBounds(coord)) { result.reason = PlacementFailReason.Bounds; return result; }

                var flags = grid.GetWalkFlags(coord);
                bool isWater = (flags & WalkFlags.Water) != 0;

                // 水域特例：桥只能造在水上；非桥不能压水
                if (def.canPlaceOnWater)
                {
                    if (!isWater) { result.reason = PlacementFailReason.WaterMismatch; return result; }
                }
                else
                {
                    if (isWater) { result.reason = PlacementFailReason.WaterMismatch; return result; }
                    if (!grid.IsWalkable(coord)) { result.reason = PlacementFailReason.Blocked; return result; }
                }

                // 区块占用（工具建筑允许建在对应资源点上）
                var occupant = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(coord) : null;
                if (occupant != null)
                {
                    if (needsNode)
                    {
                        if (occupant.sourceType != requiredNode.Value) { result.reason = PlacementFailReason.Blocked; return result; }
                    }
                    else { result.reason = PlacementFailReason.Blocked; return result; }
                }
                else if (needsNode) { result.reason = PlacementFailReason.Blocked; return result; }

                // 地形合法（桥不校验地形，只校验 Water 位）
                if (!def.canPlaceOnWater && def.allowedTerrain != null && def.allowedTerrain.Length > 0)
                {
                    var terrain = grid.GetTerrainAt(coord);
                    bool ok = false;
                    for (int t = 0; t < def.allowedTerrain.Length; t++)
                        if (def.allowedTerrain[t] == terrain) { ok = true; break; }
                    if (!ok) { result.reason = PlacementFailReason.Terrain; return result; }
                }

                // 阻挡冲突（已有 obstacle 建筑重叠）
                if (grid.IsObstacle(coord)) { result.reason = PlacementFailReason.Blocked; return result; }
            }
        }

        // 资源足够
        if (RulerController.Instance != null && !RulerController.Instance.CanAfford(def.cost))
        { result.reason = PlacementFailReason.Resource; return result; }

        result.ok = true;
        result.reason = PlacementFailReason.None;
        return result;
    }

    /// <summary>footprint 清空校验（doc 1 IsFootprintClear 的语义包装）。</summary>
    public static bool ValidateFootprintClear(GridCoord cellOrigin, int w, int h)
    {
        var grid = GridSystem.Instance;
        return grid != null && grid.IsFootprintClear(cellOrigin, w, h);
    }

    /// <summary>旧 1D 签名兼容包装（BuildController 过渡用，步骤 3 切换到 ValidatePlacement）。</summary>
    public static bool Validate(BuildingDef def, GridCoord origin)
    {
        var grid = GridSystem.Instance;
        if (grid == null) return false;
        var sub = grid.CellToSub(origin, 0, 0);
        return ValidatePlacement(def, sub, GateOrientation.Horizontal).ok;
    }
}
