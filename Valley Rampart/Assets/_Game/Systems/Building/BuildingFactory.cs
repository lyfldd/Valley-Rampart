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
        if (ph == null)
        {
            Debug.LogWarning("[BuildingFactory] 跳过 null placeholder (regionIdx=" + (region != null ? region.regionIndex.ToString() : "?") + ")");
            return false;
        }
        var table = GetMappingTable();
        if (table == null)
        {
            Debug.LogWarning("[BuildingFactory] 映射表缺失，跳过 placeholder=" + ph.type);
            return false;
        }
        var def = table.Get(ph.type);
        if (def == null)
        {
            Debug.LogWarning($"[BuildingFactory] BuildingType={ph.type} 未在映射表中配置（regionIdx={region.regionIndex}, localCellX={ph.localCellX}），跳过");
            return false;
        }

        // 全局小区块坐标
        int globalCellX = region.cellStartX + ph.localCellX;
        var coord = new GridCoord(globalCellX, 0);

        // 世界坐标
        Vector3 worldPos = GridSystem.Instance != null
            ? GridSystem.Instance.CoordToWorld(coord)
            : new Vector3(globalCellX * 32f, 0, 0);

        // 实例化 GameObject（用 def.prefab 或空壳 + 占位视觉）
        GameObject go;
        if (def.prefab != null)
        {
            go = Object.Instantiate(def.prefab, worldPos, Quaternion.identity);
        }
        else
        {
            // 无 prefab 时创建空壳 + 占位彩色方块（3.3.4 问题12）
            go = new GameObject($"Building_{ph.type}_{globalCellX}");
            go.transform.position = worldPos;
            BuildingVisual.ApplyPlaceholder(go, ph.type, def.role);
        }

        // 确保有 Building 组件（注意：Building 没有 [RequireComponent]，因为 Collider2D 是抽象类，Unity 不能自动补）
        var b = go.GetComponent<Building>();
        if (b == null)
        {
            b = go.AddComponent<Building>();
            if (b == null)
            {
                Debug.LogError($"[BuildingFactory] 添加 Building 组件失败！type={ph.type}, go={go.name}");
                Object.DestroyImmediate(go);
                return false;
            }
        }

        // 直接内联初始化 Building 字段（避免 InitFromPlaceholder 内部 NRE 或 getter/回调链上的单例时序问题）
        try
        {
            // 1. 基础字段
            b.def = def;
            b.coord = coord;
            b.isPlayerBuilt = false;           // 地图预置建筑 → false
            b.sourceType = ph.type;
            b.grade = ph.grade;
            int phCellWidth = (ph.cellWidth > 0) ? ph.cellWidth : (def.footprint.x > 0 ? def.footprint.x : 1);
            b.cellWidth = phCellWidth;
            b.level = 1;

            // 2. faction + isObstacle
            b.faction = def.faction;
            b.isObstacle = def.isObstacle;

            // 3. HP：gradeScale 缩放 combat.maxHp；任何异常都给默认 100 不掉链子
            int baseHp = 100;
            try
            {
                if (def.combat.maxHp > 0) baseHp = def.combat.maxHp;
                float scale = def.GetGradeScale(ph.grade);
                baseHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * Mathf.Max(0.1f, scale)));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BuildingFactory] HP计算降级为默认 100（def={def.id}, grade={ph.grade}）: {ex.Message}");
                baseHp = 100;
            }
            b.maxHp = baseHp;
            b.hp = baseHp;

            // 主城初始废弃态（3.3.4 批次7）
            if (ph.type == BuildingType.CastleCore)
                b.state = BuildingState.Abandoned;
        }
        catch (System.Exception ex)
        {
            // 走到这里说明 Building 某个字段赋值访问了非预期东西
            Debug.LogError($"[BuildingFactory] Building基础字段内联初始化失败：type={ph.type}, def={def.id}, regionIdx={region.regionIndex}, err={ex}");
            Object.DestroyImmediate(go);
            return false;
        }

        // 确保有 Collider2D（InteractionManager OverlapPoint 需要）
        if (go.GetComponent<Collider2D>() == null)
        {
            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.Config.cellSize : 32f;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cellSize * Mathf.Max(1, b.cellWidth), cellSize);
        }

        // 注册占用 + 注册表 + 事件（防御性：单例不存在时只打一条 Warning，不抛异常）
        try
        {
            if (GridSystem.Instance != null)
                GridSystem.Instance.MarkOccupiedFootprint(coord, Mathf.Max(1, b.cellWidth), b);
        }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] MarkOccupiedFootprint 失败: " + ex.Message); }

        try
        {
            if (BuildingRegistry.Instance != null)
                BuildingRegistry.Instance.Register(b);
        }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Registry.Register 失败: " + ex.Message); }

        try
        {
            // 按 def 配置挂行为组件（3.3.4 批次4 组件化架构）
            AttachComponents(b, def);
        }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] AttachComponents 失败: " + ex.Message); }

        try
        {
            EventBus.Publish(new BuildingPlacedEvent(b));
        }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Publish BuildingPlacedEvent 失败: " + ex.Message); }

        return true;
    }

    /// <summary>按 BuildingDef 配置挂行为组件（3.3.4 批次4）。Producer/Storage 见批次5。供 BuildingFactory 和 BuildController 共用。</summary>
    public static void AttachComponents(Building b, BuildingDef def)
    {
        if (b == null || def == null) return;
        // Producer + Storage（产能建筑，非资源点；3.3.4 批次5）
        if (def.producer.rate > 0f && def.producer.kind == ProduceKind.Resource && !def.isResourceNode)
        {
            b.gameObject.AddComponent<StorageComponent>()?.Init(b);
            b.gameObject.AddComponent<ProducerComponent>()?.Init(b);
        }
        // Combat（防御建筑，具体逻辑接 3.4/3.5）
        if (def.combat.attack > 0)
            b.gameObject.AddComponent<CombatComponent>()?.Init(b);
        // Pickup（一次性采集：宝箱/木头堆/石头堆）
        if (def.isConsumable)
            b.gameObject.AddComponent<PickupComponent>()?.Init(b);
        // Rift（裂隙，接 3.7 波次）
        if (b.sourceType == BuildingType.Rift)
            b.gameObject.AddComponent<RiftComponent>()?.Init(b);
        // CastleCore（主城，批次7 做最小实现）
        if (b.sourceType == BuildingType.CastleCore)
            b.gameObject.AddComponent<CastleCoreComponent>()?.Init(b);
    }

    /// <summary>清空所有地图建筑（跨岛切换时由 WorldManager 调）。</summary>
    public static void ClearAllBuildings()
    {
        BuildingRegistry.Instance?.Clear();
    }
}
