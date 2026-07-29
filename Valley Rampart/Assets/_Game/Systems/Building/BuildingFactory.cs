using UnityEngine;

/// <summary>
/// 地图预置建筑工厂（3.3 批次0）。把 MapData 中的 BuildingPlaceholder 转为运行时 Building 实例。
/// 在 WorldManager.GenerateWorld 末尾调用，解除 3.2.2 地图建筑断裂。
///
/// 流程：遍历 regions → resources（含 CastleCore）+ riftCellX →
/// 查 BuildingMappingTable → 实例化 Building → MarkOccupied → Register → 发事件。
/// </summary>
public static class BuildingFactory
{
    private static BuildingMappingTable _mappingTable;

    static BuildingMappingTable GetMappingTable()
    {
        if (_mappingTable == null)
            _mappingTable = Resources.Load<BuildingMappingTable>("Buildings/BuildingMappingTable");
        return _mappingTable;
    }

    /// <summary>
    /// 把 MapData 的所有 BuildingPlaceholder 转为 Building 实例。
    /// 在 WorldManager.GenerateWorld 末尾调用（3.2.2 第 12.1 节）。
    /// </summary>
    public static int InstantiateFromMap(MapData map)
    {
        if (map == null || map.regions == null) return 0;

        var table = GetMappingTable();
        if (table == null)
        {
            Debug.LogWarning("[BuildingFactory] BuildingMappingTable 未加载（Resources/Buildings/BuildingMappingTable.asset），跳过建筑实例化。地图上将无建筑。");
            return 0;
        }

        int created = 0;
        for (int i = 0; i < map.regions.Count; i++)
        {
            var region = map.regions[i];

            // 1. resources 列表（含 ResourceProducer / ResourcePickup / SpecialPoint / CastleCore）
            if (region.resources != null)
            {
                foreach (var ph in region.resources)
                {
                    if (CreateBuilding(ph, region))
                        created++;
                }
            }

            // 2. 裂隙（不在 resources 列表里，单独处理）
            if (region.riftCellX >= 0)
            {
                var riftPh = new BuildingPlaceholder
                {
                    type = BuildingType.Rift,
                    category = BuildingCategory.Rift,
                    localCellX = region.riftCellX,
                    cellWidth = 1,
                    grade = ResourceGrade.Normal,
                    isConsumable = false
                };
                if (CreateBuilding(riftPh, region))
                    created++;
            }
        }

        Debug.Log($"[BuildingFactory] 地图 mapId={map.mapId} 实例化 {created} 个建筑");
        return created;
    }

    /// <summary>把单个 BuildingPlaceholder 转为 Building 实例。</summary>
    static bool CreateBuilding(BuildingPlaceholder ph, Region region)
    {
        var table = GetMappingTable();
        var def = table.Get(ph.type);
        if (def == null)
        {
            Debug.LogWarning($"[BuildingFactory] BuildingType={ph.type} 未在映射表中配置，跳过");
            return false;
        }

        // 全局小区块坐标
        int globalCellX = region.cellStartX + ph.localCellX;
        var coord = new GridCoord(globalCellX, 0);

        // 世界坐标
        Vector3 worldPos = GridSystem.Instance != null
            ? GridSystem.Instance.CoordToWorld(coord)
            : new Vector3(globalCellX * 32f, 0, 0);

        // 实例化 GameObject（用 def.prefab 或空壳）
        GameObject go;
        if (def.prefab != null)
        {
            go = Object.Instantiate(def.prefab, worldPos, Quaternion.identity);
        }
        else
        {
            // 无 prefab 时创建空壳（有 Collider 可被点击，后续接 prefab 替换）
            go = new GameObject($"Building_{ph.type}_{globalCellX}");
            go.transform.position = worldPos;
        }

        // 确保有 Building 组件
        var b = go.GetComponent<Building>();
        if (b == null) b = go.AddComponent<Building>();

        b.InitFromPlaceholder(def, ph, coord);

        // 确保有 Collider2D（InteractionManager OverlapPoint 需要）
        if (go.GetComponent<Collider2D>() == null)
        {
            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.Config.cellSize : 32f;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cellSize * b.cellWidth, cellSize);
        }

        // 注册占用 + 注册表 + 事件
        GridSystem.Instance?.MarkOccupiedFootprint(coord, b.cellWidth, b);
        BuildingRegistry.Instance?.Register(b);
        EventBus.Publish(new BuildingPlacedEvent(b));

        return true;
    }

    /// <summary>清空所有地图建筑（跨岛切换时由 WorldManager 调）。</summary>
    public static void ClearAllBuildings()
    {
        BuildingRegistry.Instance?.Clear();
    }
}
