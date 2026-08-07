using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 世界管理器。管理程序化地图生成 + 多地图世界状态。
/// 3.2 第 7.6 节 + 3.2.1 第七节落地：5区结构 + 四资源保障 + 邻接约束 + Building占位 + 裂隙放置。
/// 存档策略：seed 复现（WorldSaveData 存 seed+meta，读档用同 seed 重生成）。
/// </summary>
public class WorldManager : Singleton<WorldManager>, ISaveable
{
    public string SaveId => "WorldManager";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    // ===== 兼容旧字段 =====
    public int MapSeed { get; private set; }    // 兼容（= worldSeed）
    public int Difficulty { get; private set; }

    // ===== 多地图世界状态 =====
    private WorldState _world;
    public WorldState World => _world;
    public MapData ActiveMap => _world?.ActiveMap;
    public bool IsCleared => _world?.IsCleared ?? false;

    // ===== SO 配置（Awake 里 Resources.Load）=====
    private GridConfig _gridConfig;
    private MapSizeConfig _mapSizeConfig;
    private MapGenRulesConfig _rulesConfig;
    private ResourceGenConfig _resourceConfig;

    protected override void Awake()
    {
        base.Awake();
        SaveManager.Instance.RegisterSaveable(this);

        // 加载 SO 配置（3.2.2 第 9.1 节，Phase 1 加载）
        _gridConfig = Resources.Load<GridConfig>("Grid/GridConfig");
        _mapSizeConfig = Resources.Load<MapSizeConfig>("Grid/MapSizeConfig");
        _rulesConfig = Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");
        _resourceConfig = Resources.Load<ResourceGenConfig>("Grid/ResourceGenConfig");

        // 注入 MapGenRules 静态工具类（3.2.1 第 2.3 节）
        MapGenRules.SetConfig(_rulesConfig);

        if (_gridConfig == null) Debug.LogError("[WorldManager] GridConfig 未找到！请创建放在 Resources/Grid/ 下");
        if (_mapSizeConfig == null) Debug.LogError("[WorldManager] MapSizeConfig 未找到！");
        if (_rulesConfig == null) Debug.LogError("[WorldManager] MapGenRulesConfig 未找到！");
        if (_resourceConfig == null) Debug.LogError("[WorldManager] ResourceGenConfig 未找到！");
    }

    // ========================================================================
    //  新建游戏入口（3.2 第 7.6 节，签名扩展）
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
        if (_rulesConfig == null) _rulesConfig = Resources.Load<MapGenRulesConfig>("Grid/MapGenRulesConfig");
        if (_resourceConfig == null) _resourceConfig = Resources.Load<ResourceGenConfig>("Grid/ResourceGenConfig");
        MapGenRules.SetConfig(_rulesConfig);
    }

    /// <summary>旧版兼容签名（worldSize 默认 Medium）。</summary>
    public void ApplyConfig(int mapSeed, int difficulty)
        => ApplyConfig(mapSeed, WorldSize.Medium, difficulty);
    /// <summary>
    /// 仅生成地图数据并设置 ActiveMap（编辑模式预览用，3.3.4）。
    /// 不实例化 Building、不发布 MapGeneratedEvent，避免编辑模式副作用。
    /// 配合 MapVisualizer.Visualize() 在非 Play 模式查看地图。
    /// </summary>
    public MapData GenerateMapForPreview(int seed, WorldSize size, int difficulty)
    {
        EnsureConfigsLoaded();
        // 先设 _world，因为 GenerateMap 内部用 _world.difficulty（GenerateBuildings）
        _world = new WorldState
        {
            worldSeed = seed,
            worldSize = size,
            difficulty = difficulty,
            activeMapId = 0
        };
        var rng = new System.Random(seed);
        var map = GenerateMap(rng, seed, 0, size, true);
        _world.maps.Add(map);
        return map;
    }

    // ========================================================================
    //  世界生成（3.2 第 5.2 节 + 3.2.1 第七节完整 pipeline）
    // ========================================================================

    /// <summary>
    /// 生成世界。当前只生成玩家初始地图（mapId=0），敌方王国地图 TODO。
    /// </summary>
    void GenerateWorld(int worldSeed, WorldSize size, int difficulty)
    {
        // 清理旧建筑对象（重新生成地图时销毁残留，3.3.4 修复）
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

        var rng = new System.Random(worldSeed);

        // 1. 生成玩家初始地图
        var playerMap = GenerateMap(rng, worldSeed, mapId: 0, size, isPlayerHome: true);
        _world.maps.Add(playerMap);

        // 填充区块 + 发布事件（3.2.2 第 12.1 节）
        if (GridSystem.Instance != null)
            GridSystem.Instance.PopulateFromMap(playerMap);
        EventBus.Publish(new MapGeneratedEvent(0, true));

        // 3.3 批次0: BuildingPlaceholder → Building 实例化（解除 3.2.2 断裂）
        BuildingFactory.Instance.InstantiateFromMap(playerMap);

        Debug.Log($"[WorldManager] 世界生成完成: seed={worldSeed}, size={size}, " +
                  $"difficulty={difficulty}, 地图数={_world.maps.Count}");
    }

    /// <summary>
    /// 生成一张地图（M 个大区块，含玩法约束）。
    /// 3.2.1 第 7.2 节完整版 GenerateMap。
    /// </summary>
    MapData GenerateMap(System.Random rng, int seed, int mapId, WorldSize size,
                        bool isPlayerHome, BigTerrain? forcedBigTerrain = null)
    {
        int cellCount = _gridConfig != null ? _gridConfig.regionCellCount : 16;

        // === Step 1: 确定大地形 ===
        BigTerrain bigTerrain = forcedBigTerrain ?? (
            isPlayerHome
                ? (rng.NextDouble() < 0.5 ? BigTerrain.Island : BigTerrain.Inland)
                : BigTerrain.Island  // 敌方王国固定岛屿（攻城）
        );

        int M = _mapSizeConfig != null ? _mapSizeConfig.GetRegionCount(size) : 15;

        var map = new MapData
        {
            mapId = mapId,
            seed = seed,
            bigTerrain = bigTerrain,
            regions = new List<Region>(M),
            isPlayerHome = isPlayerHome
        };

        // === Step 2: 5 区分配 ===
        var (center, extreme, resource) = MapGenRules.CalcZoneCounts(M);

        // 废弃城堡所在的大区块索引（正中心，单一）
        int abandonedCastleRegionIdx = MapGenRules.GetCastleRegionIndex(M, center, extreme, resource);

        // === Step 3-5: 按区分配地形 ===
        // 先创建全部 Region 骨架（多区结构），再资源保障预占位，最后按区分地形
        for (int i = 0; i < M; i++)
        {
            MapZone zone = MapGenRules.GetZone(i, M, center, extreme, resource);
            var region = new Region
            {
                regionIndex = i,
                terrain = TerrainType.Plain,  // 暂定，后续按区分地形覆盖
                cellStartX = i * cellCount,
                cellCount = cellCount,
                resources = new List<BuildingPlaceholder>(),
                riftCellX = -1,
                isEnemyTerritory = !isPlayerHome,
                zone = zone,
                isInner = (zone == MapZone.LeftResource || zone == MapZone.RightResource)
                          ? MapGenRules.IsResourceInner(i, M, zone) : false
            };
            map.regions.Add(region);
        }

        // 资源保障预占位（QQQ.1 需求1：事前占位替代事后补丁，左右资源区各保障 1 林 + 1 矿）
        if (isPlayerHome) PreReserveResources(rng, map.regions, M, center, extreme, resource);

        // 按区分地形（保障区块直接落 protectedTerrain，不走权重随机）
        for (int i = 0; i < M; i++)
        {
            var region = map.regions[i];
            TerrainType terrain = PickTerrainByZone(rng, region, i, M, bigTerrain);

            // 废弃城堡所在区块强制平原
            if (i == abandonedCastleRegionIdx) terrain = TerrainType.Plain;
            region.terrain = terrain;

            // 平原子状态判定
            if (terrain == TerrainType.Plain)
            {
                region.plainSubState = MapGenRules.IsCenterEdge(i, M, center, extreme, resource)
                    && rng.NextDouble() < _rulesConfig.centerFertileChance
                    ? PlainSubState.Fertile
                    : PlainSubState.Normal;
                // 废弃城堡区块强制普通平原
                if (i == abandonedCastleRegionIdx) region.plainSubState = PlainSubState.Normal;
            }
        }

        // === Step 6-7: 资源保障 + 邻接校验（循环 2 轮）===
        for (int round = 0; round < 2; round++)
        {
            if (isPlayerHome) EnsureResourceCoverage(rng, map.regions, M, abandonedCastleRegionIdx, abandonedCastleRegionIdx);
            EnforceAdjacency(map.regions, M, abandonedCastleRegionIdx, abandonedCastleRegionIdx);
        }

        // === Step 8: 二级约束 - Building 生成 ===
        foreach (var region in map.regions)
        {
            region.resources = GenerateBuildings(rng, region, _world.difficulty, region.isInner);
        }

        // === Step 8.5: 废弃城堡占位（2 格，中心区块）===
        PlaceAbandonedCastle(map.regions, abandonedCastleRegionIdx, cellCount);

        // === Step 8.55: 坐标原点对齐（QQQ.1 需求4：城堡中线 = 世界 x=0）===
        // castleCenterCellGlobal 为城堡中心所在 cell 全局 x（= 城堡两格交界处）
        int castleCenterCellGlobal = abandonedCastleRegionIdx * cellCount + cellCount / 2;
        float originX = castleCenterCellGlobal * (_gridConfig != null ? _gridConfig.cellSize : 1f);
        if (_gridConfig != null) _gridConfig.originX = originX;
        if (GridSystem.Instance != null && GridSystem.Instance.Config != null)
            GridSystem.Instance.Config.originX = originX;

        // === Step 8.6: 流浪汉营地占位（3.5.1 §4.1 E-S7，仅玩家主图）===
        if (isPlayerHome) PlaceVagrantCamps(rng, map, abandonedCastleRegionIdx, cellCount);

        // === Step 9: 出怪口/裂隙放置 ===
        PlaceRifts(map, M, bigTerrain);

        // === Step 9.5: 验证（3.2.1 第十节验证清单）===
        var issues = MapValidator.Validate(map, _rulesConfig);
        foreach (var issue in issues)
        {
            if (issue.severity == MapValidator.Severity.Error)
                Debug.LogError($"[MapValidator] {issue}");
            else
                Debug.LogWarning($"[MapValidator] {issue}");
        }

        Debug.Log($"[WorldManager] 地图生成: mapId={mapId}, seed={seed}, " +
                  $"bigTerrain={bigTerrain}, regions={M}, " +
                  $"裂隙数={map.regions.Count(r => r.riftCellX >= 0)}");

        return map;
    }

    // 5 区结构辅助（CalcZoneCounts/GetZone/GetCastleRegionIndices/IsResourceInner/IsCenterEdge）
    // 已抽至 MapGenRules 静态类（3.2.1 第二节）

    // ========================================================================
    //  地形选择（3.2.1 第三 + 7.2 节）
    // ========================================================================

    /// <summary>按区分地形。资源保障占位区块直接返回保障地形；极端区按大地形固定，资源区分内/外侧风险分层。</summary>
    TerrainType PickTerrainByZone(System.Random rng, Region region, int idx, int M, BigTerrain bigTerrain)
    {
        // QQQ.1 需求1：资源保障占位区块直接返回保障地形，不走权重随机（不被覆盖）
        if (region != null && region.isProtectedResource) return region.protectedTerrain;

        MapZone zone = region != null ? region.zone : MapZone.Center;
        switch (zone)
        {
            case MapZone.Center:
                // 中心区边缘肥沃概率由调用方处理（plainSubState），这里返回平原
                return TerrainType.Plain;

            case MapZone.LeftResource:
            case MapZone.RightResource:
                return MapGenRules.IsResourceInner(idx, M, zone)
                    ? PickWeighted(rng, _rulesConfig.resourceInnerWeights)
                    : PickWeighted(rng, _rulesConfig.resourceOuterWeights);

            case MapZone.LeftExtreme:
            case MapZone.RightExtreme:
                if (bigTerrain == BigTerrain.Island)
                    return TerrainType.Coast;
                else // 内陆
                    return zone == MapZone.LeftExtreme
                        ? TerrainType.Snow      // 内陆左端=雪山（大山屏障）
                        : TerrainType.Wasteland; // 内陆右端=荒地（出怪侧）

            default:
                return TerrainType.Plain;
        }
    }

    /// <summary>按权重随机选地形。</summary>
    TerrainType PickWeighted(System.Random rng, TerrainWeight[] weights)
    {
        if (weights == null || weights.Length == 0) return TerrainType.Plain;

        float total = 0;
        for (int i = 0; i < weights.Length; i++) total += weights[i].weight;

        float r = (float)rng.NextDouble() * total;
        float acc = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i].weight;
            if (r <= acc) return weights[i].terrain;
        }
        return weights[weights.Length - 1].terrain;
    }

    // ========================================================================
    //  四资源保障（3.2.1 第四节）
    // ========================================================================

    /// <summary>
    /// 四资源保障（3.2.1 第四节）。
    /// QQQ.1 需求1重构：保障已前置到分配阶段的 PreReserveResources 事前占位，
    /// 此处不再 ForceReplace 事后补丁（避免与 EnforceAdjacency 打架），只做统计日志兜底观察。
    /// </summary>
    void EnsureResourceCoverage(System.Random rng, List<Region> regions, int M, int castleA, int castleB)
    {
        int forestCount = regions.Count(r => r.terrain == TerrainType.Forest);
        int quarryCount = regions.Count(r => r.terrain == TerrainType.Quarry);
        int fertileCount = regions.Count(r => r.terrain == TerrainType.Plain && r.plainSubState == PlainSubState.Fertile);

        Debug.Log($"[WorldManager] 资源数量: forest={forestCount} quarry={quarryCount} fertile={fertileCount}");
    }

    /// <summary>
    /// 资源保障预占位（QQQ.1 需求1）：左右资源区各预选 1 个区块作保障林地、1 个作保障矿山。
    /// 共 4 个保障区块（左林/左矿/右林/右矿），标记 isProtectedResource + protectedTerrain，
    /// 后续 PickTerrainByZone 直接落保障地形、EnforceAdjacency 不改保障区块。
    /// </summary>
    void PreReserveResources(System.Random rng, List<Region> regions, int M, int center, int extreme, int resource)
    {
        int leftStart = extreme;
        int leftEnd = extreme + resource;
        int rightStart = extreme + resource + center;
        int rightEnd = Mathf.Min(M, rightStart + resource);

        ReserveProtectedInRange(rng, regions, leftStart, leftEnd, TerrainType.Forest);
        ReserveProtectedInRange(rng, regions, leftStart, leftEnd, TerrainType.Quarry);
        ReserveProtectedInRange(rng, regions, rightStart, rightEnd, TerrainType.Forest);
        ReserveProtectedInRange(rng, regions, rightStart, rightEnd, TerrainType.Quarry);
    }

    /// <summary>在 [start, end) 大区块范围内随机选一个未占用的区块设为指定保障地形。</summary>
    void ReserveProtectedInRange(System.Random rng, List<Region> regions, int start, int end, TerrainType type)
    {
        if (end <= start) return;
        var candidates = new List<int>();
        for (int i = start; i < end; i++)
        {
            var r = regions[i];
            if (r == null || r.isProtectedResource) continue;
            candidates.Add(i);
        }
        if (candidates.Count == 0) return;

        int pick = candidates[rng.Next(candidates.Count)];
        var region = regions[pick];
        region.isProtectedResource = true;
        region.protectedTerrain = type;
        Debug.Log($"[WorldManager] 资源保障占位: 区块 {pick}（zone={region.zone}）→ {type}");
    }

    // ========================================================================
    //  邻接校验 + 缓冲插入（3.2.1 第五节）
    // ========================================================================

    /// <summary>扫描相邻大区块对，违规则插入丘陵做缓冲（QQQ.1 需求1：不改资源保障区块）。</summary>
    void EnforceAdjacency(List<Region> regions, int M, int castleA, int castleB)
    {
        for (int i = 0; i < regions.Count - 1; i++)
        {
            var a = regions[i].terrain;
            var b = regions[i + 1].terrain;

            if (!_rulesConfig.CanAdjacency(a, b))
            {
                // QQQ.1 需求1：两个相邻区块都是资源保障区块 → 跳过不处理（保障优先于邻接）
                if (regions[i].isProtectedResource && regions[i + 1].isProtectedResource)
                    continue;

                // 违规：优先改 i+1（靠中心侧），除非 i+1 是主城
                int fixIdx = (i + 1 == castleA || i + 1 == castleB) ? i : i + 1;
                // 出怪口端(0 或 M-1)不改
                if (fixIdx == 0 || fixIdx == M - 1)
                    fixIdx = (fixIdx == i) ? i + 1 : i;

                // QQQ.1 需求1：fixIdx 不能是资源保障区块（改邻居不改保障）
                if (regions[fixIdx].isProtectedResource)
                {
                    int alt = (fixIdx == i) ? i + 1 : i;
                    if (alt < 0 || alt >= regions.Count) continue;
                    if (regions[alt].isProtectedResource) continue;   // 两个都保障 → 跳过
                    if (alt == 0 || alt == M - 1) continue;           // 出怪口不改
                    fixIdx = alt;
                }

                regions[fixIdx].terrain = TerrainType.Hills;
                regions[fixIdx].plainSubState = PlainSubState.Normal;  // 非平原重置子状态
            }
        }
    }

    // ========================================================================
    //  Building 生成（3.2.1 第六节）
    // ========================================================================

    /// <summary>为一个大区块生成所有 Building 占位（持续性 + 一次性 + 特殊点）。</summary>
    List<BuildingPlaceholder> GenerateBuildings(System.Random rng, Region region, int difficulty, bool isInner)
    {
        var buildings = new List<BuildingPlaceholder>();

        // 极端区不放资源点（纯战场/屏障）
        if (region.zone == MapZone.LeftExtreme || region.zone == MapZone.RightExtreme)
            return buildings;

        // 1. 持续性资源
        var producerType = GetProducerType(region);
        if (producerType != BuildingType.None)
        {
            int count = RollCount(rng, region, difficulty, isProducer: true);
            PlaceBuildings(buildings, rng, producerType, count, region.cellCount,
                           difficulty, isInner, BuildingCategory.ResourceProducer, isConsumable: false);
        }

        // 2. 一次性资源（总数受配置控制，在类型间随机分配）
        var pickupTypes = GetPickupTypes(region);
        if (pickupTypes.Length > 0)
        {
            int totalPickup = RollCount(rng, region, difficulty, isProducer: false);
            for (int i = 0; i < totalPickup; i++)
            {
                BuildingType pt = pickupTypes[rng.Next(pickupTypes.Length)];
                PlaceBuildings(buildings, rng, pt, 1, region.cellCount,
                               difficulty, isInner, BuildingCategory.ResourcePickup, isConsumable: true);
            }
        }

        // 3. 特殊点（低概率）
        float spChance = _resourceConfig != null ? _resourceConfig.specialPointChance : 0.15f;
        if (rng.NextDouble() < spChance)
        {
            var spType = RollSpecialPointType(rng);
            PlaceBuildings(buildings, rng, spType, 1, region.cellCount,
                           difficulty, isInner, BuildingCategory.SpecialPoint, isConsumable: false);
        }

        return buildings;
    }

    /// <summary>地形 → 持续性资源类型映射（3.2.1 第 6.2 节）。</summary>
    /// <remarks>
    /// 平原 always 有农田（普通=基础产出，肥沃=高级 buff）。
    /// 丘陵无持续性资源（只提供一次性石头/木头/矿脉）。
    /// 石来源仅靠矿山（矿洞）。
    /// </remarks>
    BuildingType GetProducerType(Region region)
    {
        switch (region.terrain)
        {
            case TerrainType.Forest: return BuildingType.Tree;       // 林地 → 树（木来源）
            case TerrainType.Quarry: return BuildingType.Mine;       // 矿山 → 矿洞（石来源）
            case TerrainType.Plain:  return BuildingType.Farmland;   // 平原 always 有农田
            // 丘陵/荒地/雪山/海岸：无持续性资源
            default: return BuildingType.None;
        }
    }

    /// <summary>地形 → 一次性资源类型列表（3.2.1 第 6.2 节）。</summary>
    BuildingType[] GetPickupTypes(Region region)
    {
        switch (region.terrain)
        {
            case TerrainType.Forest:
                return new[] { BuildingType.WoodPile };              // 林地 → 木头堆
            case TerrainType.Quarry:
                return new[] { BuildingType.OreVein };               // 矿山 → 矿脉
            case TerrainType.Hills:
                // 丘陵复合：一次性石头堆 + 木头堆 + 矿脉（无持续性资源）
                return new[] { BuildingType.StonePile, BuildingType.WoodPile, BuildingType.OreVein };
            case TerrainType.Plain:
                return new[] { BuildingType.StonePile, BuildingType.WoodPile };  // 平原 → 少量石头堆+木头堆
            default:
                return new BuildingType[0];
        }
    }

    /// <summary>按地形+子状态+难度滚动资源点数量（3.2.1 第 6.6 节，区分持续性/一次性）。</summary>
    int RollCount(System.Random rng, Region region, int difficulty, bool isProducer)
    {
        PlainSubState subState = region.terrain == TerrainType.Plain
            ? region.plainSubState : PlainSubState.Normal;

        var (min, max) = _resourceConfig != null
            ? (isProducer
                ? _resourceConfig.GetProducerCount(region.terrain, subState)
                : _resourceConfig.GetPickupCount(region.terrain, subState))
            : (2, 4);

        float density = _resourceConfig != null
            ? _resourceConfig.GetDensity(difficulty)
            : 1.0f;

        int adjMin = Mathf.RoundToInt(min * density);
        int adjMax = Mathf.RoundToInt(max * density);
        if (adjMax < adjMin) adjMax = adjMin;

        // 持续性资源（生产者）至少 1 个：平原 always 有农田，林地 always 有树，矿山 always 有矿洞
        if (isProducer && adjMax < 1) adjMax = 1;
        if (isProducer && adjMin < 1) adjMin = 1;

        return adjMin + (adjMax > adjMin ? rng.Next(adjMax - adjMin + 1) : 0);
    }

    /// <summary>随机滚动等级（贫瘠/普通/富有）。</summary>
    ResourceGrade RollGrade(System.Random rng, int difficulty, bool isInner)
    {
        var prob = _resourceConfig != null
            ? _resourceConfig.GetGradeProb(difficulty, isInner)
            : new GradeProbability { barren = 0.35f, normal = 0.55f, rich = 0.10f };

        float r = (float)rng.NextDouble();
        if (r < prob.barren) return ResourceGrade.Barren;
        if (r < prob.barren + prob.normal) return ResourceGrade.Normal;
        return ResourceGrade.Rich;
    }

    /// <summary>随机滚动特殊点类型。</summary>
    BuildingType RollSpecialPointType(System.Random rng)
    {
        var types = new[] { BuildingType.TreasureBox, BuildingType.Ruins };
        return types[rng.Next(types.Length)];
    }

    /// <summary>在小区块内放置 N 个 Building 占位（避开两端 + 不重复）。</summary>
    void PlaceBuildings(List<BuildingPlaceholder> list, System.Random rng,
                       BuildingType type, int count, int cellCount,
                       int difficulty, bool isInner, BuildingCategory category, bool isConsumable)
    {
        // 候选小区块：1..cellCount-2（避开两端做战场缓冲）
        var candidates = new List<int>();
        for (int x = 1; x <= cellCount - 2; x++)
            if (!list.Any(b => b.localCellX == x))  // 每个小区块最多一个 Building
                candidates.Add(x);

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int pickIdx = rng.Next(candidates.Count);
            list.Add(new BuildingPlaceholder
            {
                type = type,
                category = category,
                localCellX = candidates[pickIdx],
                grade = RollGrade(rng, difficulty, isInner),
                isConsumable = isConsumable
            });
            candidates.RemoveAt(pickIdx);
        }
    }

    // ========================================================================
    //  废弃城堡占位（3.2.1 第 6.2 节）
    // ========================================================================

    /// <summary>在中心区块放置废弃城堡（占 2 个小区格，3.2.1 第 6.2 节）。</summary>
    void PlaceAbandonedCastle(List<Region> regions, int regionIdx, int cellCount)
    {
        if (regionIdx < 0 || regionIdx >= regions.Count) return;
        var region = regions[regionIdx];

        // 占中间 2 格：cellCount/2 - 1 和 cellCount/2
        int cellA = cellCount / 2 - 1;
        int cellB = cellCount / 2;

        // 清理目标格子上已有的占位（GenerateBuildings 可能放了农田/资源堆）
        region.resources.RemoveAll(b => b.localCellX == cellA || b.localCellX == cellB);

        region.resources.Add(new BuildingPlaceholder
        {
            type = BuildingType.CastleCore,
            category = BuildingCategory.CastleCore,
            localCellX = cellA,
            cellWidth = 2,  // 占 2 格
            grade = ResourceGrade.Normal
        });
    }

    // ========================================================================
    //  流浪汉营地占位（3.5.1 §4.1，E-S7）
    // ========================================================================

    private KingdomConfig _kingdomConfig; // 懒加载（营地数值 SO 化，so-data-driven）

    KingdomConfig GetKingdomConfigLazy()
    {
        if (_kingdomConfig == null) _kingdomConfig = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        return _kingdomConfig;
    }

    /// <summary>
    /// 放置流浪汉营地（3.5.1 §4.1 E-S7）：开局 2-3 个（KingdomConfig SO 可调）。
    /// 3.2.1 区块规则：近王国区块必有 1 个（按距城堡排序取最近可落区块）；
    /// 禁落核心区块（城堡区块排除）；其余营地不落与已用区块距离&lt;2 的区块。
    /// 营地占位不与现有 placeholder 重叠，避开区块两端（战场缓冲）。
    /// </summary>
    void PlaceVagrantCamps(System.Random rng, MapData map, int castleIdx, int cellCount)
    {
        var cfg = GetKingdomConfigLazy();
        if (cfg == null || cfg.vagrantCampMax <= 0 || map.regions.Count <= 1) return;

        int want = rng.Next(cfg.vagrantCampMin, cfg.vagrantCampMax + 1);
        want = Mathf.Clamp(want, 1, map.regions.Count - 1);

        // 候选区块：排除城堡核心区块，按与城堡的距离升序（保证第一个落近王国区块）
        var candidates = new List<int>();
        for (int i = 0; i < map.regions.Count; i++)
            if (i != castleIdx) candidates.Add(i);
        candidates.Sort((a, b) => Mathf.Abs(a - castleIdx).CompareTo(Mathf.Abs(b - castleIdx)));

        var usedRegions = new List<int>();
        int placed = 0;

        for (int ci = 0; ci < candidates.Count && placed < want; ci++)
        {
            int idx = candidates[ci];

            // 已落过营地后：跳过与已用区块距离 < 2 的区块（拉开分布）
            if (placed > 0)
            {
                bool tooClose = false;
                for (int u = 0; u < usedRegions.Count; u++)
                    if (Mathf.Abs(usedRegions[u] - idx) < 2) { tooClose = true; break; }
                if (tooClose) continue;
            }

            int slot = FindCampSlot(map.regions[idx], cellCount, cfg.vagrantCampFootprint, rng);
            if (slot < 0) continue;

            map.regions[idx].resources.Add(new BuildingPlaceholder
            {
                type = BuildingType.VagrantCamp,
                category = BuildingCategory.SpecialPoint,
                localCellX = slot,
                cellWidth = cfg.vagrantCampFootprint,
                grade = ResourceGrade.Normal
            });
            usedRegions.Add(idx);
            placed++;
        }

        Debug.Log($"[WorldManager] 流浪汉营地放置: {placed}/{want}（城堡区块={castleIdx}, 区块=[{string.Join(",", usedRegions)}]）");
    }

    /// <summary>在区块内找 footprint 宽的不重叠窗口（避开区块两端与现有占位/裂隙，随机起点保种子确定性）。</summary>
    int FindCampSlot(Region region, int cellCount, int footprint, System.Random rng)
    {
        if (region == null || footprint < 1) return -1;
        if (region.resources == null) region.resources = new List<BuildingPlaceholder>();

        // 与 PlaceBuildings 同规则避开两端（战场缓冲）：合法起点 ∈ [1, cellCount-1-footprint]
        int maxStart = cellCount - 1 - footprint;
        if (maxStart < 1) return -1;

        var validStarts = new List<int>();
        for (int s = 1; s <= maxStart; s++)
        {
            bool ok = true;
            for (int x = s; x < s + footprint && ok; x++)
            {
                if (x == region.riftCellX) { ok = false; break; }
                for (int p = 0; p < region.resources.Count; p++)
                {
                    var b = region.resources[p];
                    if (b == null) continue;
                    int w = Mathf.Max(1, b.cellWidth);
                    if (x >= b.localCellX && x < b.localCellX + w) { ok = false; break; }
                }
            }
            if (ok) validStarts.Add(s);
        }
        if (validStarts.Count == 0) return -1;
        return validStarts[rng.Next(validStarts.Count)];
    }

    // ========================================================================
    //  王国锚点（3.5.1 E-S3：君主出生/开局实体/招募走回 共用）
    // ========================================================================

    /// <summary>
    /// 王国锚点世界坐标 = 废弃城堡中心（地面层 y）。
    /// 地图未就绪返回 Vector2.zero（调用方自行兜底）。
    /// </summary>
    public Vector2 GetKingdomAnchorWorld()
    {
        var map = ActiveMap;
        var grid = GridSystem.Instance;
        if (map == null || grid == null || grid.Config == null) return Vector2.zero;

        int castleIdx = MapGenRules.GetCastleRegionIndex(map.regions.Count);
        if (castleIdx < 0 || castleIdx >= map.regions.Count) return Vector2.zero;

        var region = map.regions[castleIdx];
        if (region.resources == null) return Vector2.zero;

        for (int i = 0; i < region.resources.Count; i++)
        {
            var bp = region.resources[i];
            if (bp == null || bp.category != BuildingCategory.CastleCore) continue;
            float cs = grid.Config.cellSize;
            // 城堡占 2 格（localCellX 起）：中心 x = 两格交界处。
            // QQQ.1 需求6（方案2）：与建筑一致，减 originX 使城堡中心落 x=0（而非原始坐标）。
            float centerX = (region.cellStartX + bp.localCellX + 1f) * cs - grid.Config.originX;
            return new Vector2(centerX, grid.CoordToWorld(new GridCoord(0, 0)).y);
        }
        return Vector2.zero;
    }

    // ========================================================================
    //  裂隙放置（3.2.1 第 6.10 节）
    // ========================================================================

    /// <summary>在极端区端点放裂隙（Rift Building 占位，3.2.1 第 6.10 节）。</summary>
    void PlaceRifts(MapData map, int M, BigTerrain bigTerrain)
    {
        bool isInland = bigTerrain == BigTerrain.Inland;
        int leftRiftRegion = 0;       // 最左大区块
        int rightRiftRegion = M - 1;  // 最右大区块

        if (isInland)
        {
            // 内陆：左端=雪山屏障(无怪)，右端=荒地出怪
            PlaceRiftInRegion(map.regions[rightRiftRegion], isRightEnd: true);
        }
        else
        {
            // 岛屿：两端海岸都出怪
            PlaceRiftInRegion(map.regions[leftRiftRegion], isRightEnd: false);
            PlaceRiftInRegion(map.regions[rightRiftRegion], isRightEnd: true);
        }
    }

    /// <summary>在大区块最外端放裂隙。左端放 cell 0，右端放 cellCount-1。</summary>
    void PlaceRiftInRegion(Region region, bool isRightEnd)
    {
        // 左端放 cell 0（最外端），右端放 cellCount-1（最外端）
        region.riftCellX = isRightEnd ? region.cellCount - 1 : 0;
    }

    // ========================================================================
    //  跨岛远征 + 王国征服（3.2 第 6.4-6.5 节）
    // ========================================================================

    /// <summary>切换到指定地图（跨岛远征）。</summary>
    public void SwitchToMap(int mapId)
    {
        if (_world == null) return;
        _world.activeMapId = mapId;
        Debug.Log($"[WorldManager] 切换到地图 mapId={mapId}");
    }

    /// <summary>占领敌方王国地图。</summary>
    public void ConquerMap(int mapId)
    {
        if (_world == null) return;
        var map = _world.maps.FirstOrDefault(m => m.mapId == mapId);
        if (map != null)
        {
            map.isConquered = true;
            _world.conqueredMapIds.Add(mapId);
            Debug.Log($"[WorldManager] 占领地图 mapId={mapId}, 已征服数={_world.conqueredMapIds.Count}");
        }
    }

    // ========================================================================
    //  ISaveable（3.2 第 7.6 节，version=2 多地图存档）
    // ========================================================================

    public SavePayload SaveState()
    {
        if (_world == null)
        {
            // 兼容旧版（无世界状态时存 mapSeed+difficulty）
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

        // 兼容旧版（version=1 或 worldSeed==0 但 mapSeed!=0）
        int worldSeed = data.worldSeed;
        if (worldSeed == 0 && data.mapSeed != 0) worldSeed = data.mapSeed;

        WorldSize worldSize = data.worldSize > 0 ? (WorldSize)data.worldSize : WorldSize.Medium;
        int difficulty = data.difficulty > 0 ? data.difficulty : 2;

        MapSeed = worldSeed;  // 兼容
        Difficulty = difficulty;

        // 用 seed 重新生成世界（确定性 → 地形/资源完全复现）
        GenerateWorld(worldSeed, worldSize, difficulty);

        // 恢复活跃地图
        if (data.activeMapId >= 0 && _world.maps.Any(m => m.mapId == data.activeMapId))
            _world.activeMapId = data.activeMapId;

        // 恢复征服状态
        if (!string.IsNullOrEmpty(data.conqueredMapIds))
        {
            foreach (var idStr in data.conqueredMapIds.Split(','))
                if (int.TryParse(idStr, out var mid))
                    ConquerMap(mid);
        }

        Debug.Log($"[WorldManager] 从存档恢复: seed={worldSeed}, size={worldSize}, " +
                  $"difficulty={difficulty}, activeMapId={_world.activeMapId}");
    }

    /// <summary>序列化地图列表 meta（只存 mapId/seed/bigTerrain/isConquered，地形靠 seed 复现）。</summary>
    string SerializeMapsMeta(List<MapData> maps)
    {
        var metaList = maps.Select(m => new MapMeta
        {
            mapId = m.mapId,
            seed = m.seed,
            bigTerrain = (int)m.bigTerrain,
            isConquered = m.isConquered ? 1 : 0
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
//  存档数据结构（version=2，兼容旧版 version=1）
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
    public int bigTerrain;
    public int isConquered;
}
