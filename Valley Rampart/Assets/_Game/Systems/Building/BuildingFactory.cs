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

    // ===== 地图预置建筑实例化（2_2 步骤5：naturalBuildings 双来源之一）=====

    /// <summary>FeatureType -> BuildingType（naturalBuildings 实例化映射；SnowMountain 无建筑实体，地形已阻挡）。</summary>
    static BuildingType? FeatureToBuildingType(FeatureType f)
    {
        switch (f)
        {
            case FeatureType.Tree: return BuildingType.Tree;
            case FeatureType.Mine: return BuildingType.Mine;
            case FeatureType.OreVein: return BuildingType.OreVein;
            case FeatureType.WoodPile: return BuildingType.WoodPile;   // HH.10 裁决三：扩到三类
            case FeatureType.StonePile: return BuildingType.StonePile;
            default: return null;   // SnowMountain 等纯视觉/地形阻挡特征物跳过
        }
    }

    /// <summary>
    /// 把 MapData 的自然建筑占位（2_1 naturalBuildings）转为 Building 实例（2_2 接管）。
    /// 树/矿洞/矿脉按 BuildingMappingTable 查 BuildingDef 实例化；
    /// 另在玩家出生点放主城（CastleCore）保建造解锁链路（正式主城锚点归 2_12）。
    /// </summary>
    public int InstantiateFromMap(MapData map)
    {
        if (map == null) return 0;
        var table = GetMappingTable();
        if (table == null)
        {
            Debug.LogWarning("[BuildingFactory] BuildingMappingTable 未加载，跳过自然建筑实例化");
            return 0;
        }

        int count = 0;
        if (map.naturalBuildings != null)
        {
            foreach (var nb in map.naturalBuildings)
            {
                if (nb == null) continue;
                var type = FeatureToBuildingType(nb.feature);
                if (!type.HasValue) continue;   // SnowMountain：地形阻挡已就位，无建筑实体
                var def = table.Get(type.Value);
                if (def == null)
                {
                    Debug.LogWarning($"[BuildingFactory] naturalBuildings 类型 {type.Value} 未配置 BuildingDef，跳过");
                    continue;
                }

                var coord = new GridCoord(nb.cellX, nb.cellY);
                var fp = new Vector2Int(nb.w > 0 ? nb.w : 1, nb.h > 0 ? nb.h : 1);
                var worldPos = FootprintCenterWorld(coord, fp);
                if (CreateBuildingInstance(def, type.Value, coord, fp, worldPos,
                        isPlayerBuilt: false, grade: ResourceGrade.Normal,
                        isConsumable: def.isConsumable, initialState: BuildingState.Active,
                        kingdomId: -1))   // 2_16 步骤7 哨兵三分：野生自然建筑=-1（非玩家非 AI，排除集不纳）
                    count++;
            }
        }

        // 玩家出生点放主城（2_2 过渡桥：保建造解锁链路；主城=王座/旗帜锚点归 2_12 重做）
        // 沿用 1D 流程：Abandoned 废墟态放置，玩家经 BuildingPanel 修复 -> CastleLevel=1 解锁建造
        var castleDef = table.Get(BuildingType.CastleCore);
        if (castleDef != null && map.kingdomSpawns != null && map.kingdomSpawns.Count > 0)
        {
            var spawn = map.kingdomSpawns[0];
            var coord = new GridCoord(spawn.x, spawn.y);
            var fp = new Vector2Int(
                castleDef.footprint.x > 0 ? castleDef.footprint.x : 1,
                castleDef.footprint.y > 0 ? castleDef.footprint.y : 1);
            if (CreateBuildingInstance(castleDef, BuildingType.CastleCore, coord, fp,
                    FootprintCenterWorld(coord, fp),
                    isPlayerBuilt: false, grade: ResourceGrade.Normal,
                    isConsumable: false, initialState: BuildingState.Abandoned))
                count++;
        }

        Debug.Log($"[BuildingFactory] 2D 地图预置建筑实例化完成：{count} 个（自然建筑 + 主城）");
        return count;
    }

    /// <summary>
    /// 单体一次性资源实体重建（HH.10 裁决三：实体路径到点重生调）。
    /// 由 ResourceRespawnSystem 在记录格到期时调用，重建一棵被采走的 OreVein/WoodPile/StonePile。
    /// 逻辑镜像 InstantiateFromMap 内循环（feature→type→def→CreateBuildingInstance），不做整图重派生。
    /// </summary>
    public bool ReSpawnNaturalBuilding(GridCoord coord, FeatureType feature)
    {
        var type = FeatureToBuildingType(feature);
        if (!type.HasValue) return false;
        var table = GetMappingTable();
        if (table == null) return false;
        var def = table.Get(type.Value);
        if (def == null)
        {
            Debug.LogWarning($"[BuildingFactory] 资源重生：{feature} 未配置 BuildingDef，跳过");
            return false;
        }
        var fp = new Vector2Int(1, 1);   // 一次性资源均是 1×1
        return CreateBuildingInstance(def, type.Value, coord, fp,
            FootprintCenterWorld(coord, fp),
            isPlayerBuilt: false, grade: ResourceGrade.Normal,
            isConsumable: true, initialState: BuildingState.Active,
            kingdomId: -1);   // 2_16 步骤7 哨兵三分：重生自然建筑仍=-1，排除集不纳
    }

    /// <summary>footprint 中心世界坐标（origin 左上格 + w/h 中心偏移）。</summary>
    static Vector3 FootprintCenterWorld(GridCoord coord, Vector2Int fp)
    {
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null) return Vector3.zero;
        Vector2 origin = grid.CoordToWorld(coord);
        return origin + new Vector2((fp.x - 1) * 0.5f * grid.Config.cellSize.x,
                                     (fp.y - 1) * 0.5f * grid.Config.cellSize.y);
    }

    /// <summary>按占用/注册/挂件/发事件创建 Building 实例。供地图与玩家放置共用逻辑（BuildController 保留自身放置路径）。</summary>
    public bool CreateBuildingInstance(BuildingDef def, BuildingType sourceType, GridCoord coord, Vector2Int footprint,
                                       Vector3 worldPos, bool isPlayerBuilt, ResourceGrade grade, bool isConsumable,
                                       BuildingState initialState, int kingdomId = 0)
    {
        if (def == null) return false;
        var fp = new Vector2Int(
            footprint.x > 0 ? footprint.x : 1,
            footprint.y > 0 ? footprint.y : 1);

        GameObject go;
        if (def.prefab != null)
        {
            go = Object.Instantiate(def.prefab, worldPos, Quaternion.identity);
        }
        else
        {
            go = new GameObject($"Building_{def.id}_{coord.x}_{coord.y}");
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
            b.footprint = fp;
            b.level = 1;
            b.faction = def.faction;
            b.isObstacle = def.isObstacle;
            b.kingdomId = kingdomId;   // 2_16 步骤2：王国归属（默认 0=玩家）

            // HP：统一入口 = def.maxHp（3.5.1 E-S10）× gradeScale
            int baseHp = def.maxHp > 0 ? def.maxHp : 100;
            try
            {
                float scale = def.GetGradeScale(grade);
                baseHp = Mathf.Max(1, Mathf.RoundToInt(baseHp * Mathf.Max(0.1f, scale)));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BuildingFactory] HP计算降级为 def.maxHp 无缩放（def={def.id}, grade={grade}）: {ex.Message}");
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

        try { if (GridSystem.Instance != null) GridSystem.Instance.MarkOccupiedFootprint(coord, fp.x, fp.y, b); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] MarkOccupiedFootprint 失败: " + ex.Message); }

        // 桥：置 Bridge 位（2_2 §3.5）
        if (def.isBridge && GridSystem.Instance != null)
        {
            try { GridSystem.Instance.SetBridge(coord, fp.x, fp.y, true); }
            catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] SetBridge 失败: " + ex.Message); }
        }

        try { if (BuildingRegistry.Instance != null) BuildingRegistry.Instance.Register(b); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Registry.Register 失败: " + ex.Message); }

        try { AttachComponents(b, def); }
        catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] AttachComponents 失败: " + ex.Message); }

        // 城门：挂 GateController（2_2 §3.4）
        if (def.isGate && go.GetComponent<GateController>() == null)
            go.AddComponent<GateController>();

        // QQQ.2 T17：直接以 Active 态创建的建筑（地图预置/读档）注册到任务调度器
        if (initialState == BuildingState.Active && TaskScheduler.HasInstance)
        {
            try { TaskScheduler.Instance.Register(b); }
            catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Register TaskScheduler 失败: " + ex.Message); }
        }

        // 仅玩家建造的 Publish（HH.4 裁决：发布侧剔除，地图自然预置建筑不 Publish——无订阅者时避免全图丢弃刷屏）。
        if (isPlayerBuilt)
        {
            try { EventBus.Publish(new BuildingPlacedEvent(b)); }
            catch (System.Exception ex) { Debug.LogWarning("[BuildingFactory] Publish BuildingPlacedEvent 失败: " + ex.Message); }
        }

        return true;
    }

    /// <summary>按 BuildingDef 配置挂行为组件（Producer/Storage/Combat/Pickup/Rift/CastleCore）。供 BuildingFactory 和 BuildController 共用。</summary>
    public void AttachComponents(Building b, BuildingDef def)
    {
        if (b == null || def == null) return;
        if (def.producer.rate > 0f && def.producer.kind == ProduceKind.Resource && !def.isResourceNode)
        {
            // 投掷机厂（2_12 步骤9 D207~D212 / HH.19 A×4）：挂专属组件产出弹药（厂级 3 子仓），替代通用 ProducerComponent。
            // 弹性逻辑：仍需本地无 StorageComponent（厂仓 3 子弹药不附建筑本体），故跳过通用 StorageComponent 挂载。
            if (def.isSiegeWorkshop)
            {
                b.gameObject.AddComponent<SiegeWorkshopBuilding>()?.Init(b);
            }
            else
            {
                b.gameObject.AddComponent<StorageComponent>()?.Init(b);
                // 铁匠铺（2_12 步骤8 D200）：挂 BlacksmithBuilding 替代通用 ProducerComponent（石→Metal 就地加工）
                if (def.isBlacksmith)
                    b.gameObject.AddComponent<BlacksmithBuilding>()?.Init(b);
                else
                    b.gameObject.AddComponent<ProducerComponent>()?.Init(b);
            }
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

        // 2D 坐标/占地恢复（2_2）：旧档缺字段 -> 兜底 def.footprint
        var coord = new GridCoord(data.coordX, data.coordY);
        int fw = data.footprintW > 0 ? data.footprintW : (def.footprint.x > 0 ? def.footprint.x : 1);
        int fh = data.footprintH > 0 ? data.footprintH : (def.footprint.y > 0 ? def.footprint.y : 1);
        var fp = new Vector2Int(Mathf.Max(1, fw), Mathf.Max(1, fh));
        Vector3 worldPos = GridSystem.Instance != null
            ? (Vector3)GridSystem.Instance.CoordToWorld(coord)
            : new Vector3(coord.x * 2.26f, coord.y * 1.13f, 0);
        if (fp.x > 1 || fp.y > 1)
        {
            float csX = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.x : 2.26f;
            float csY = GridSystem.Instance != null && GridSystem.Instance.Config != null ? GridSystem.Instance.Config.cellSize.y : 1.13f;
            worldPos.x += (fp.x - 1) * 0.5f * csX;
            worldPos.y += (fp.y - 1) * 0.5f * csY;
        }

        BuildingState state = (BuildingState)data.state;
        if (def.sourceType == BuildingType.CastleCore && state == BuildingState.Abandoned)
            state = BuildingState.Active;   // 主城修复后读档不应回到废墟（castoeLevel≥1）

        bool ok = CreateBuildingInstance(def, (BuildingType)data.sourceType, coord, fp, worldPos,
                                         isPlayerBuilt: true, (ResourceGrade)data.grade, false, state,
                                         kingdomId: ReadArchiveKingdomId((BuildingType)data.sourceType, data.kingdomId));
        if (!ok) return;

        var b = GridSystem.Instance != null ? GridSystem.Instance.GetOccupant(coord) as Building : null;
        if (b == null)
        {
            Debug.LogWarning($"[BuildingFactory] 读档重建后未取到 Building（coord=({coord.x},{coord.y})），跳过状态恢复。");
            return;
        }

        // 覆盖 SaveId（否则 SaveManager 找不到该 saveId 分发 LoadState）
        b.OverrideSaveId(entry.saveId);

        // QQQ.3 B8-5 / LC-B2：grade 恢复后按新等级重算属性（修复读档后产能永久降贫瘠档 rate×0.7）
        b.grade = (ResourceGrade)data.grade;
        b.ApplyDef();

        // 恢复核心状态（level/hp/maxHp/storedAmount/副产）
        b.level = Mathf.Max(1, data.level);
        b.maxHp = Mathf.Max(1, data.maxHp);
        b.hp = Mathf.Clamp(data.hp, 0, b.maxHp);
        var storage = b.GetComponent<StorageComponent>();
        if (storage != null) storage.storedAmount = Mathf.Max(0, data.storedAmount);
        var producer = b.GetComponent<ProducerComponent>();
        if (producer != null) producer.RestoreByproduct(data.byproductType, data.byproductAmount);

        // 2_12 步骤7 / D155：累计投入恢复（D155 修复成本基数 / D162 拆除返还基数）。旧档缺字段 → 兜底按 def.cost。
        b.totalInvested = data.totalInvested > 0
            ? data.totalInvested
            : (b.def != null ? b.def.cost.gold + b.def.cost.stone + b.def.cost.wood + b.def.cost.food : 0);
    }

    /// <summary>读档王国归属：自然建筑（OreVein/WoodPile/StonePile 一次性资源点）一律强制 -1（哨兵配套，
    /// 旧档缺 kingdomId 默认 0，不强制则自然建筑全变"玩家王国"污染排除集）；其余取存栏 kingdomId。</summary>
    private static int ReadArchiveKingdomId(BuildingType sourceType, int archivedKingdomId)
    {
        if (sourceType == BuildingType.OreVein || sourceType == BuildingType.WoodPile || sourceType == BuildingType.StonePile)
            return -1;
        return archivedKingdomId;
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

    // ===== 对象池回收（QQQ.2 T19 / DR-11：一次性资源点采集后走池，不直接 Destroy）=====

    /// <summary>
    /// 回收一次性资源点建筑到对象池（由 Building.OnGatherCompleted 调）。
    /// 建筑对象池按 def.id 分桶复用：出池时 CreateBuildingInstance 会重新初始化全字段，状态天然全新。
    /// 采集后资源点应消失不留贴图——若复用于其他资源点，位置/占用在出池时重建。
    /// </summary>
    private readonly Dictionary<string, Stack<Building>> _pool = new Dictionary<string, Stack<Building>>();

    public void ReturnBuildingToPool(Building b)
    {
        if (b == null || b.def == null) return;
        string key = b.def.id;
        if (!_pool.TryGetValue(key, out var stack))
        {
            stack = new Stack<Building>();
            _pool[key] = stack;
        }
        b.gameObject.SetActive(false);
        stack.Push(b);
    }
}