using System.Collections.Generic;
using UnityEngine;

/// <summary>城门朝向（2_2 §3.4）：横墙->横向 2 格；竖墙->纵向 2 格。</summary>
public enum GateOrientation { Horizontal, Vertical }

/// <summary>放置失败原因（2_2 §5.1）。</summary>
public enum PlacementFailReason
{
    None,
    Blocked,        // 占用/阻挡冲突/断头桥/桥超长
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
/// 放置校验器（2_2 建筑与占格，微格吸附 + footprint 全覆盖 + 桥水域特例 + 城门拐角）。
/// 静态工具类。旧 1D 的 <see cref="Validate(BuildingDef, GridCoord)"/> 保留为兼容包装。
/// </summary>
public static class PlacementValidator
{
    private static BuildConfig _buildConfig;

    /// <summary>建造全局配置（懒加载；缺资产用类默认值兜底）。</summary>
    public static BuildConfig BuildConfig
    {
        get
        {
            if (_buildConfig == null)
                _buildConfig = Resources.Load<BuildConfig>("Config/BuildConfig");
            return _buildConfig;
        }
    }

    /// <summary>微格吸附后校验（含桥的水域特例/接岸校验 + 城门拐角）。subOrigin 为吸附后的微格坐标。</summary>
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

        // 城门拐角（2_2 §3.4）：横竖两侧都有墙 -> 禁放
        if (def.isGate)
        {
            var inferred = InferGateOrientation(origin);
            if (inferred.HasValue == false) { result.reason = PlacementFailReason.GateCorner; return result; }
        }

        // 桥接岸校验（2_2 §3.5）：至少一个邻格可走（陆地或既有桥面）；断头桥拒绝
        if (def.isBridge)
        {
            if (!BridgeTouchesWalkable(grid, origin, w, h)) { result.reason = PlacementFailReason.Blocked; return result; }
            if (BridgeChainLength(origin, w, h) + 1 > MaxBridgeSegments) { result.reason = PlacementFailReason.Blocked; return result; }
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

    // ===== 城门朝向推断（2_2 §3.4）=====

    /// <summary>
    /// 自动推断城门朝向：检测落点两侧相邻格的墙走向。
    /// 返回 null = 拐角（横竖都有墙，禁放）；无墙自由段返回给定 fallback（玩家可 R 旋转）。
    /// </summary>
    public static GateOrientation? InferGateOrientation(GridCoord cellOrigin, GateOrientation fallback = GateOrientation.Horizontal)
    {
        bool horizontalWall = IsWallAt(new GridCoord(cellOrigin.x - 1, cellOrigin.y))
                            || IsWallAt(new GridCoord(cellOrigin.x + 1, cellOrigin.y));
        bool verticalWall = IsWallAt(new GridCoord(cellOrigin.x, cellOrigin.y - 1))
                         || IsWallAt(new GridCoord(cellOrigin.x, cellOrigin.y + 1));
        if (horizontalWall && verticalWall) return null;   // 拐角
        if (horizontalWall) return GateOrientation.Horizontal;
        if (verticalWall) return GateOrientation.Vertical;
        return fallback;
    }

    /// <summary>该格是否有城墙类建筑（role==Wall：墙/门均算，走向判定用）。</summary>
    static bool IsWallAt(GridCoord coord)
    {
        var b = BuildingRegistry.Instance != null ? BuildingRegistry.Instance.GetAt(coord) : null;
        return b != null && b.def != null && b.def.role == BuildingRole.Wall;
    }

    // ===== 桥接岸/桥链（2_2 §3.5）=====

    /// <summary>桥 footprint 是否至少接触一个可走邻格（陆地或既有桥面；桥面 Bridge 位使 IsWalkable 为 true）。</summary>
    static bool BridgeTouchesWalkable(GridSystem grid, GridCoord origin, int w, int h)
    {
        // 沿 footprint 外圈扫描四邻
        for (int dx = -1; dx <= w; dx++)
        {
            var top = new GridCoord(origin.x + dx, origin.y - 1);
            var bottom = new GridCoord(origin.x + dx, origin.y + h);
            if (grid.IsInBounds(top) && grid.IsWalkable(top)) return true;
            if (grid.IsInBounds(bottom) && grid.IsWalkable(bottom)) return true;
        }
        for (int dy = -1; dy <= h; dy++)
        {
            var left = new GridCoord(origin.x - 1, origin.y + dy);
            var right = new GridCoord(origin.x + w, origin.y + dy);
            if (grid.IsInBounds(left) && grid.IsWalkable(left)) return true;
            if (grid.IsInBounds(right) && grid.IsWalkable(right)) return true;
        }
        return false;
    }

    /// <summary>从落点邻接桥段出发 BFS 数既有桥链段数（新段不计，调用方 +1）。</summary>
    static int BridgeChainLength(GridCoord origin, int w, int h)
    {
        var registry = BuildingRegistry.Instance;
        if (registry == null) return 0;

        var visited = new HashSet<Building>();
        var queue = new Queue<GridCoord>();

        // 种子：落点外圈的既有桥段
        for (int dx = -1; dx <= w; dx++)
        {
            queue.Enqueue(new GridCoord(origin.x + dx, origin.y - 1));
            queue.Enqueue(new GridCoord(origin.x + dx, origin.y + h));
        }
        for (int dy = -1; dy <= h; dy++)
        {
            queue.Enqueue(new GridCoord(origin.x - 1, origin.y + dy));
            queue.Enqueue(new GridCoord(origin.x + w, origin.y + dy));
        }

        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            var b = registry.GetAt(c);
            if (b == null || b.def == null || !b.def.isBridge || visited.Contains(b)) continue;
            visited.Add(b);
            // 沿该桥段四邻继续扩散
            int bw = Mathf.Max(1, b.footprint.x), bh = Mathf.Max(1, b.footprint.y);
            for (int dx = -1; dx <= bw; dx++)
            {
                queue.Enqueue(new GridCoord(b.coord.x + dx, b.coord.y - 1));
                queue.Enqueue(new GridCoord(b.coord.x + dx, b.coord.y + bh));
            }
            for (int dy = -1; dy <= bh; dy++)
            {
                queue.Enqueue(new GridCoord(b.coord.x - 1, b.coord.y + dy));
                queue.Enqueue(new GridCoord(b.coord.x + bw, b.coord.y + dy));
            }
        }
        return visited.Count;
    }

    /// <summary>桥链段数上限（BuildConfig.bridgeMaxSegments，缺配置 8）。</summary>
    static int MaxBridgeSegments
        => BuildConfig != null && BuildConfig.bridgeMaxSegments > 0 ? BuildConfig.bridgeMaxSegments : 8;
}
