using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 建筑工厂（3.3 批次0 + 3.5 实施计划 P0 步骤3）。
/// 职责拆为两块：
///   1) 地图预置建筑实例化（BuildingPlaceholder → Building，WorldManager.GenerateWorld 调）。
///   2) 存档重建（ISaveableSpawner，前缀 "Building_"，读档时由 SaveManager 调 SpawnFromSave）。
///
/// 3.5 步骤3：static class → Singleton<BuildingFactory>，并实现 ISaveableSpawner。
/// 调用方统一走 BuildingFactory.Instance.X（WorldManager / BuildController 已同步）。
/// </summary>
public class BuildingFactory : Singleton<BuildingFactory>, ISaveableSpawner
{
    public string SaveIdPrefix => "Building_";

    private static BuildingMappingTable _mappingTable;
    private static BuildingDef[] _allDefsCache;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _mappingTable = Resources.Load<BuildingMappingTable>("Buildings/BuildingMappingTable");
    }

    static BuildingMappingTable GetMappingTable()
    {
        if (_mappingTable == null)
            _mappingTable = Resources.Load<BuildingMappingTable>("Buildings/BuildingMappingTable");
        return _mappingTable;
    }

    /// <summary>按 id 查找 BuildingDef（Resources/Buildings 下全部资产，缓存）。存档重建用。</summary>
    public static BuildingDef FindDefById(string defId)
    {
        if (string.IsNullOrEmpty(defId)) return null;
        if (_allDefsCache == null)
            _allDefsCache = Resources.LoadAll<BuildingDef>("Buildings");
        if (_allDefsCache == null) return null;
        for (int i = 0; i < _allDefsCache.Length; i++)
            if (_allDefsCache[i] != null && _allDefsCache[i].id == defId)
                return _allDefsCache[i];
        return null;
    }

    // ===== 地图预置建筑实例化 =====

    /// <summary>把 MapData 的所有 BuildingPlaceholder 转为 Building 实例。</summary>
    public int InstantiateFromMap(MapData map)
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

            if (region.resources != null)
            {
                foreach (var ph in region.resources)
                {
                    if (CreateBuilding(ph, region))
                        created++;
                }
            }

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

    static bool CreateBuilding(BuildingPlaceholder ph, Region region)
    {
        if (ph == null) return false;
        var table = GetMappingTable();
        if (table == null) return false;
        var def = table.Get(ph.type);
        if (def == null)
        {
            Debug.LogWarning($"[BuildingFactory] BuildingType={ph.type} 未在映射表中配置（regionIdx={region.regionIndex}, localCellX={ph.localCellX}），跳过");
            return false;
        }

        int globalCellX = region.cellStartX + ph.localCellX;
        var coord = new GridCoord(globalCellX, 0);
        int phCellWidth = (ph.cellWidth > 0) ? ph.cellWidth : (def.footprint.x > 0 ? def.footprint.x : 1);

        float cs = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize : 2.26f;
        Vector3 worldPos = GridSystem.Instance != null
            ? (Vector3)GridSystem.Instance.CoordToWorld(coord)
            : new Vector3(globalCellX * cs, -3f, 0);
        if (phCellWidth > 1)
            worldPos.x += (phCellWidth - 1) / 2f * cs;

        // 复用通用实例化（含组件挂载/占用/注册/事件）
        return Instance.CreateBuildingInstance(def, ph.type, coord, phCellWidth, worldPos,
                                               isPlayerBuilt: false, ph.grade, ph.isConsumable,
                                               initialState: ph.type == BuildingType.CastleCore ? BuildingState.Abandoned : BuildingState.Active);
    }

    /// <summary>按占用/注册/挂件/发事件创建 Building 实例。供地图与玩家放置共用逻辑（BuildController 保留自身放置路径）。</summary>
    public bool CreateBuildingInstance(BuildingDef def, BuildingType sourceType, GridCoord coord, int cellWidth,
                                       Vector3 worldPos, bool isPlayerBuilt, ResourceGrade grade, bool isConsumable,
                                       BuildingState initialState)
    {
        if (def == null) return false;

        GameObject go;
        if (def.prefab != null)
        {
            go = Object.Instantiate(def.prefab, worldPos, Quaternion.identity);
        }
        else
        {
            go = new GameObject($"Building_{def.id}_{coord.x}");
            go.transform.position = worldPos;
            BuildingVisual.ApplyPlaceholder(go, sourceType, def.role);
        }

        var b = go.GetComponent<Building>();
        if (b == null)
        {
            b = go.AddComponent<Building>();
            if (b == null)
            {
                Debug.LogError($"[BuildingFactory] 添加 Building 组件失败！id={def.id}");
                Object.DestroyImmediate(go);
                return false;
            }
        }

        // 内联初始化
        try
        {
            b.def = def;
            b.coord = coord;
            b.isPlayerBuilt = isPlayerBuilt;
            b.sourceType = sourceType;
            b.grade = grade;
            b.cellWidth = cellWidth;
            b.level = 1;
            b.faction = def.faction;
            b.isObstacle = def.isObstacle;

            int baseHp = 100;
            try
            {
                if (def.combat.maxHp > 0) baseHp = def.combat.maxHp;
                float scale = def.GetGradeScale(grade);
                baseHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * Mathf.Max(0.1f, scale)));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BuildingFactory] HP计算降级为默认 100（def={def.id}, grade={grade}）: {ex.Message}");
                baseHp = 100;
            }
            b.maxHp = baseHp;
            b.hp = baseHp;
            b.state = initialState;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[BuildingFactory] Building字段初始化失败：id={def.id}, err={ex}");
            Object.DestroyImmediate(go);
            return false;
        }

        if (go.GetComponent<Collider2D>() == null)
        {
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }

        try { if (GridSystem.Instance != null) GridSystem.Instance.MarkOccupiedFootprint(coord, Mathf.Max(1, cellWidth), b); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] MarkOccupiedFootprint 失败: " + ex.Message); }

        try { if (BuildingRegistry.Instance != null) BuildingRegistry.Instance.Register(b); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Registry.Register 失败: " + ex.Message); }

        try { AttachComponents(b, def); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] AttachComponents 失败: " + ex.Message); }

        try { EventBus.Publish(new BuildingPlacedEvent(b)); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Publish BuildingPlacedEvent 失败: " + ex.Message); }

        return true;
    }

    /// <summary>按 BuildingDef 配置挂行为组件（Producer/Storage/Combat/Pickup/Rift/CastleCore）。供 BuildingFactory 和 BuildController 共用。</summary>
    public void AttachComponents(Building b, BuildingDef def)
    {
        if (b == null || def == null) return;
        if (def.producer.rate > 0f && def.producer.kind == ProduceKind.Resource && !def.isResourceNode)
        {
            b.gameObject.AddComponent<StorageComponent>()?.Init(b);
            b.gameObject.AddComponent<ProducerComponent>()?.Init(b);
        }
        if (def.combat.attack > 0)
            b.gameObject.AddComponent<CombatComponent>()?.Init(b);
        if (def.isConsumable)
            b.gameObject.AddComponent<PickupComponent>()?.Init(b);
        if (b.sourceType == BuildingType.Rift)
            b.gameObject.AddComponent<RiftComponent>()?.Init(b);
        if (b.sourceType == BuildingType.CastleCore)
            b.gameObject.AddComponent<CastleCoreComponent>()?.Init(b);
        // 3.5 P1-22：科技模块建筑（学院/工坊）挂研究单项目队列组件
        if (def.moduleType == ModuleType.Science)
            b.gameObject.AddComponent<AcademyBuilding>()?.Init(b);
    }

    // ===== ISaveableSpawner（3.5 步骤3：读档重建）=====

    /// <summary>读档重建单栋建筑：按 defId 重建 + 恢复 level/hp/storedAmount + 网格占用。</summary>
    public void SpawnFromSave(ModuleSaveEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.json)) return;
        BuildingSaveData data;
        try { data = JsonUtility.FromJson<BuildingSaveData>(entry.json); }
        catch (System.Exception ex) { Debug.LogError($"[BuildingFactory] BuildingSaveData 反序列化失败: {ex}"); return; }

        var def = FindDefById(data.defId);
        if (def == null)
        {
            Debug.LogWarning($"[BuildingFactory] 读档重建失败：未找到 BuildingDef id={data.defId}，跳过。");
            return;
        }

        var coord = new GridCoord(data.coordX, 0);
        int cellWidth = data.cellWidth > 0 ? data.cellWidth : (def.footprint.x > 0 ? def.footprint.x : 1);
        float cs = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize : 2.26f;
        Vector3 worldPos = GridSystem.Instance != null
            ? (Vector3)GridSystem.Instance.CoordToWorld(coord)
            : new Vector3(coord.x * cs, -3f, 0);
        if (cellWidth > 1)
            worldPos.x += (cellWidth - 1) / 2f * cs;

        BuildingState state = (BuildingState)data.state;
        if (def.sourceType == BuildingType.CastleCore && state == BuildingState.Abandoned)
            state = BuildingState.Active;   // 主城修复后读档不应回到废墟（castoeLevel≥1）

        bool ok = CreateBuildingInstance(def, (BuildingType)data.sourceType, coord, cellWidth, worldPos,
                                         isPlayerBuilt: true, (ResourceGrade)0, false, state);
        if (!ok) return;

        var b = GridSystem.Instance != null ? GridSystem.Instance.GetOccupant(coord) : null;
        if (b == null)
        {
            Debug.LogWarning($"[BuildingFactory] 读档重建后未取到 Building（coord={coord.x}），跳过状态恢复。");
            return;
        }

        // 覆盖 SaveId（否则 SaveManager 找不到该 saveId 分发 LoadState）
        b.OverrideSaveId(entry.saveId);

        // 恢复核心状态（level/hp/maxHp/storedAmount/副产）
        b.level = Mathf.Max(1, data.level);
        b.maxHp = Mathf.Max(1, data.maxHp);
        b.hp = Mathf.Clamp(data.hp, 0, b.maxHp);
        var storage = b.GetComponent<StorageComponent>();
        if (storage != null) storage.storedAmount = Mathf.Max(0, data.storedAmount);
        var producer = b.GetComponent<ProducerComponent>();
        if (producer != null) producer.RestoreByproduct(data.byproductType, data.byproductAmount);
    }

    /// <summary>清空所有地图建筑（跨岛切换时由 WorldManager 调）。</summary>
    public void ClearAllBuildings()
    {
        if (BuildingRegistry.Instance == null) return;
        var all = BuildingRegistry.Instance.All;
        for (int i = all.Count - 1; i >= 0; i--)
        {
            if (all[i] != null && all[i].gameObject != null)
            {
                if (Application.isPlaying) Object.Destroy(all[i].gameObject);
                else Object.DestroyImmediate(all[i].gameObject);
            }
        }
        BuildingRegistry.Instance.Clear();
    }
}