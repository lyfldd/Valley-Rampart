using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 世界管理器（改造计划 doc 1：地图生成归 2_1，本片只保留单图 2D 空间契约骨架）。
/// 作用：
///   - 2_1 生成管线装配 MapData（features 唯一功能源）；
///   - 填充网格后调 BuildingFactory.InstantiateFromMap 实例化自然建筑 + 主城（2_2）；
///   - 不再生成 5 区/Region/资源占位（2_1 重写）。
/// 存档策略：seed 复现（WorldSaveData 存 seed+meta，读档用同 seed 重生成）。
/// </summary>
public class WorldManager : Singleton<WorldManager>, ISaveable
{
    public string SaveId => "WorldManager";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    // ===== 兼容旧字段 =====
    public int MapSeed { get; private set; }    // 兼容（= worldSeed）
    public int Difficulty { get; private set; }

    // ===== 单图世界状态 =====
    private WorldState _world;
    public WorldState World => _world;
    public MapData ActiveMap => _world?.ActiveMap;
    public bool IsCleared => _world?.IsCleared ?? false;

    // ===== SO 配置（Awake 里 Resources.Load）=====
    private GridConfig _gridConfig;
    private MapSizeConfig _mapSizeConfig;
    private MapGenRulesConfig _mapGenRulesConfig;

    protected override void Awake()
    {
        base.Awake();
        SaveManager.Instance.RegisterSaveable(this);

        // 加载 SO 配置（doc 1 §4：GridConfig + MapSizeConfig 是空间层唯一真源）
        _gridConfig = Resources.Load<GridConfig>("Grid/GridConfig");
        _mapSizeConfig = Resources.Load<MapSizeConfig>("Grid/MapSizeConfig");
        _mapGenRulesConfig = Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");

        if (_gridConfig == null) Debug.LogError("[WorldManager] GridConfig 未找到！请创建放在 Resources/Grid/ 下");
        if (_mapSizeConfig == null) Debug.LogError("[WorldManager] MapSizeConfig 未找到！");
    }

    // ========================================================================
    //  新建游戏入口
    // ========================================================================

    /// <summary>新建游戏时由 WorldSystem.InitializeWorld 调用。</summary>
    public void ApplyConfig(int worldSeed, WorldSize worldSize, int difficulty)
    {
        EnsureConfigsLoaded();
        MapSeed = worldSeed;   // 兼容旧字段
        Difficulty = difficulty;
        GenerateWorld(worldSeed, worldSize, difficulty);
    }

    /// <summary>确保 SO 配置已加载（Awake 可能早于 SO 创建）。</summary>
    void EnsureConfigsLoaded()
    {
        if (_gridConfig == null) _gridConfig = Resources.Load<GridConfig>("Grid/GridConfig");
        if (_mapSizeConfig == null) _mapSizeConfig = Resources.Load<MapSizeConfig>("Grid/MapSizeConfig");
        if (_mapGenRulesConfig == null) _mapGenRulesConfig = Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");
    }

    /// <summary>旧版兼容签名（worldSize 默认 Medium）。</summary>
    public void ApplyConfig(int mapSeed, int difficulty)
        => ApplyConfig(mapSeed, WorldSize.Medium, difficulty);

    /// <summary>
    /// 仅生成地图数据并设置 ActiveMap（编辑模式预览用）。
    /// 不实例化 Building、不发布 MapGeneratedEvent，避免编辑模式副作用。
    /// 配合 MapVisualizer.Visualize() 在非 Play 模式查看地图。
    /// </summary>
    public MapData GenerateMapForPreview(int seed, WorldSize size, int difficulty)
    {
        EnsureConfigsLoaded();
        _world = new WorldState
        {
            worldSeed = seed,
            worldSize = size,
            difficulty = difficulty,
            activeMapId = 0
        };
        var map = GenerateMap(seed, 0, size, difficulty);
        _world.maps.Add(map);
        return map;
    }

    // ========================================================================
    //  世界生成（doc 1：2D 单图，装配 width/height 的空 MapData）
    // ========================================================================

    /// <summary>生成世界。当前装配全 Plain 空地形（2_1 重写生成算法）。</summary>
    void GenerateWorld(int worldSeed, WorldSize size, int difficulty)
    {
        // 清理旧建筑对象（重新生成地图时销毁残留）
        if (BuildingFactory.Instance != null)
            BuildingFactory.Instance.ClearAllBuildings();

        // seed=0 时随机生成一个
        if (worldSeed == 0) worldSeed = Random.Range(1, int.MaxValue);

        _world = new WorldState
        {
            worldSeed = worldSeed,
            worldSize = size,
            difficulty = difficulty,
            activeMapId = 0
        };

        var playerMap = GenerateMap(worldSeed, mapId: 0, size, difficulty);
        _world.maps.Add(playerMap);

        // 填充网格 + 实例化自然建筑/主城（2_2）+ 发布事件（单图初始化）
        if (GridSystem.Instance != null)
            GridSystem.Instance.PopulateFromMap(playerMap);
        if (BuildingFactory.Instance != null)
            BuildingFactory.Instance.InstantiateFromMap(playerMap);
        EventBus.Publish(new MapGeneratedEvent(0, true));

        Debug.Log($"[WorldManager] 世界已装配（2D 骨架）: seed={worldSeed}, size={size}, " +
                  $"difficulty={difficulty}, 网格={playerMap.width}x{playerMap.height}");
    }

    /// <summary>
    /// 生成一张 2D 地图（2_1 §5.2 六步管线，全程 System.Random(seed) 确定性）。
    /// features 唯一功能源；terrain/walkFlags 由 GridSystem.PopulateFromMap 派生。
    /// </summary>
    MapData GenerateMap(int seed, int mapId, WorldSize size, int difficulty)
    {
        int width = _mapSizeConfig != null ? _mapSizeConfig.GetWidth(size) : 256;
        int height = _mapSizeConfig != null ? _mapSizeConfig.GetHeight(size) : 256;
        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);

        var map = new MapData
        {
            mapId = mapId,
            seed = seed,
            width = width,
            height = height,
            features = new FeatureType[width * height],
            climateZones = new ClimateZone[Mathf.Max(1, width / MapGenRules.ChunkSize) * Mathf.Max(1, height / MapGenRules.ChunkSize)],
            kingdomSpawns = new List<Vector2Int>(),
            threatSpawns = new List<SpawnDef>(),
            naturalBuildings = new List<NaturalBuilding>()
        };

        var rng = new System.Random(seed);   // 确定性单源（R4，禁 UnityEngine.Random）

        // §5.2 管线：温度带 → 特征物 → 出生点 → 资源就近补 → 连通性 → 水域 → 复跑连通 → 威胁刷点 → 自然建筑派生
        MapGenRules.FillClimateZones(rng, map, _mapGenRulesConfig);             // 步骤3
        MapGenRules.FillFeatures(rng, map);                                     // 步骤4
        int aiCount = _mapSizeConfig != null ? _mapSizeConfig.GetEnemyMapBase(difficulty) : 2;
        MapGenRules.PlaceKingdomSpawns(rng, map, _mapGenRulesConfig, size, aiCount);   // 步骤6
        MapGenRules.EnsureNearbyResources(rng, map, _mapGenRulesConfig);        // 步骤7
        MapValidator.ValidateConnectivity(map);                                  // 步骤8
        MapGenRules.PlaceWater(rng, map, size);                                  // 步骤9（海洋/湖/河）
        MapValidator.ValidateConnectivity(map);                                  // 水域后复跑连通（审计）
        MapGenRules.PlaceThreatSpawns(rng, map, _mapGenRulesConfig, difficulty); // 步骤10
        MapGenRules.DeriveNaturalBuildings(map);                                 // 步骤11

        Debug.Log($"[WorldManager] 地图生成（2_1）: mapId={mapId}, seed={seed}, {width}x{height}, " +
                  $"出生点={map.kingdomSpawns.Count}, 威胁点={map.threatSpawns.Count}, 自然建筑={map.naturalBuildings.Count}");
        return map;
    }

    // ========================================================================
    //  王国锚点（城堡中心世界坐标。城堡占位归 2_1，此处回退地图中心）
    // ========================================================================

    /// <summary>
    /// 王国锚点世界坐标。地图生成尚未放置城堡（归 2_1），返回地图中心格世界坐标兜底。
    /// 地图未就绪返回 Vector2.zero（调用方自行兜底）。
    /// </summary>
    public Vector2 GetKingdomAnchorWorld()
    {
        var map = ActiveMap;
        var grid = GridSystem.Instance;
        if (map == null || grid == null || grid.Config == null) return Vector2.zero;
        var centerCoord = new GridCoord(map.width / 2, map.height / 2);
        return grid.CoordToWorld(centerCoord);
    }

    // ========================================================================
    //  A+（HH.2）：资源节点数据覆盖
    //  树/矿不再创建 Building 实体（装饰持续节点，归 features 数据 + Tilemap 特征层渲染）。
    //  玩家把工具建筑（伐木场/采石场）放在树/矿格上时，覆盖该格 feature → Plain 并刷新渲染。
    // ========================================================================

    /// <summary>
    /// 消耗一格的资源节点（树/矿 feature → Plain），供工具建筑放置覆盖使用。
    /// 若该格是 Tree/Mine feature 则改 Plain + 刷新 GridSystem 地形/可走 + MapRenderService 渲染。
    /// 返回是否覆盖成功（该格本就是资源节点）。
    /// </summary>
    public bool TryConsumeResourceNode(GridCoord coord)
    {
        var map = ActiveMap;
        if (map == null || map.features == null) return false;
        if (coord.x < 0 || coord.y < 0 || coord.x >= map.width || coord.y >= map.height) return false;
        int i = coord.y * map.width + coord.x;
        var f = map.features[i];
        if (f != FeatureType.Tree && f != FeatureType.Mine) return false;   // 非可覆盖资源节点

        map.features[i] = FeatureType.Plain;
        // 网格派生刷新（GridSystem.PopulateFromMap 同源逻辑）
        if (GridSystem.Instance != null)
            GridSystem.Instance.RefreshCellFromFeature(coord, FeatureType.Plain);
        if (MapRenderService.Instance != null)
            MapRenderService.Instance.UpdateCell(new GridCoord(coord.x, coord.y));
        return true;
    }

    /// <summary>该格是否满足资源点放置（Tree/Mine/Farmland；A+ 下由 features 数据判定，非 Building 实体）。</summary>
    public bool IsResourceNodeAvailable(GridCoord coord, BuildingType requiredNode)
    {
        var map = ActiveMap;
        if (map == null || map.features == null) return false;
        if (coord.x < 0 || coord.y < 0 || coord.x >= map.width || coord.y >= map.height) return false;
        var f = map.features[coord.y * map.width + coord.x];
        switch (requiredNode)
        {
            case BuildingType.Tree:      return f == FeatureType.Tree;
            case BuildingType.Mine:      return f == FeatureType.Mine;
            case BuildingType.Farmland:  return f == FeatureType.Plain;   // 农田建在可耕 Plain 上
            default: return false;
        }
    }

    // ========================================================================
    //  跨岛远征 + 王国征服（保留壳；单图征服语义归 2_8）
    // ========================================================================

    /// <summary>切换到指定地图（单图冻结，预留）。</summary>
    public void SwitchToMap(int mapId)
    {
        if (_world == null) return;
        _world.activeMapId = mapId;
        Debug.Log($"[WorldManager] 切换到地图 mapId={mapId}");
    }

    /// <summary>占领敌方王国地图（单图无征服，仅记录 conqueredMapIds）。</summary>
    public void ConquerMap(int mapId)
    {
        if (_world == null) return;
        _world.conqueredMapIds.Add(mapId);
        Debug.Log($"[WorldManager] 记录征服 mapId={mapId}, 已征服数={_world.conqueredMapIds.Count}");
    }

    // ========================================================================
    //  ISaveable（存档 schema 归 2_11；按 seed+worldSize+difficulty 重生成）
    // ========================================================================

    public SavePayload SaveState()
    {
        if (_world == null)
        {
            var oldData = new WorldSaveData { mapSeed = MapSeed, difficulty = Difficulty };
            return new SavePayload
            {
                typeName = typeof(WorldSaveData).AssemblyQualifiedName,
                json = JsonUtility.ToJson(oldData),
                version = 1
            };
        }

        var data = new WorldSaveData
        {
            worldSeed = _world.worldSeed,
            worldSize = (int)_world.worldSize,
            difficulty = _world.difficulty,
            activeMapId = _world.activeMapId,
            mapsMeta = SerializeMapsMeta(_world.maps),
            conqueredMapIds = string.Join(",", _world.conqueredMapIds)
        };
        return new SavePayload
        {
            typeName = typeof(WorldSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 2
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(WorldSaveData).AssemblyQualifiedName) return;

        EnsureConfigsLoaded();

        var data = JsonUtility.FromJson<WorldSaveData>(payload.json);

        int worldSeed = data.worldSeed;
        if (worldSeed == 0 && data.mapSeed != 0) worldSeed = data.mapSeed;

        WorldSize worldSize = data.worldSize > 0 ? (WorldSize)data.worldSize : WorldSize.Medium;
        int difficulty = data.difficulty > 0 ? data.difficulty : 2;

        MapSeed = worldSeed;  // 兼容
        Difficulty = difficulty;

        // 用 seed 重新生成世界（确定性 → 网格复现）
        GenerateWorld(worldSeed, worldSize, difficulty);

        if (data.activeMapId >= 0 && _world.maps.Any(m => m.mapId == data.activeMapId))
            _world.activeMapId = data.activeMapId;

        if (!string.IsNullOrEmpty(data.conqueredMapIds))
        {
            foreach (var idStr in data.conqueredMapIds.Split(','))
                if (int.TryParse(idStr, out var mid))
                    ConquerMap(mid);
        }

        Debug.Log($"[WorldManager] 从存档恢复: seed={worldSeed}, size={worldSize}, " +
                  $"difficulty={difficulty}, activeMapId={_world.activeMapId}");
    }

    /// <summary>序列化地图列表 meta（只存 mapId/seed，网格靠 seed 复现；schema 归 2_11）。</summary>
    string SerializeMapsMeta(List<MapData> maps)
    {
        var metaList = maps.Select(m => new MapMeta
        {
            mapId = m.mapId,
            seed = m.seed
        }).ToArray();
        return JsonUtility.ToJson(new MapMetaList { items = metaList });
    }

    // ========================================================================
    //  状态重置（由 TeardownManager 返回主菜单时调用）
    // ========================================================================

    public void ResetState()
    {
        _world = null;
        MapSeed = 0;
        Difficulty = 0;
        Debug.Log("[WorldManager] ResetState: 世界已清空");
    }
}

// ========================================================================
//  存档数据结构（schema 迁移归 2_11）
// ========================================================================

[System.Serializable]
public class WorldSaveData
{
    // 旧版字段（version=1 兼容）
    public int mapSeed;          // 旧版地图种子（新版用 worldSeed）
    public int difficulty;       // 难度

    // 新增字段（version=2）
    public int worldSeed;         // 世界种子
    public int worldSize;         // (int)WorldSize
    public int activeMapId;       // 当前活跃地图
    public string mapsMeta;       // 地图列表 meta（JSON）
    public string conqueredMapIds;// 逗号分隔的已征服 mapId
}

[System.Serializable]
public class MapMetaList { public MapMeta[] items; }

[System.Serializable]
public class MapMeta
{
    public int mapId;
    public int seed;
    // 删：bigTerrain / isConquered（MapData 2D 契约已去，schema 迁移归 2_11）
}