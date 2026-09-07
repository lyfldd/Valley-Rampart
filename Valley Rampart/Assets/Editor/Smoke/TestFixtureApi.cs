using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.92 件B/T6~T9：测试夹具 API（Editor-only，D549）。
//  PlaceKingdom(tier, raceId, pos)：参照 KingdomFoundry.FoundFirstGeneration 注册链的
//  「手动参数版」——注册(KingdomDef 链)→定族→性格(种族基准零扰动)→国库→实体(生产链+T5 时序修复)
//  →建筑(生产链 BuildingFactory，M6 禁裸构)→王国脑→领土圈→三源注入(T8)→底数 warm-up(T9)。
//
//  确定性红线（M15）：全链禁 UnityEngine.Random；取点=固定环序；注入定序=注册→实体→建筑→三源。
//  小世界（M13）：fixture 场景由调用方用 WorldSize.Small 建局，绕开 45 日大读档路径。
//
//  ⚠️ T6 档位数值：2026-09-07 策划核准（HH.93 §九，gold=1500 维持）→ 已迁 SO
//  （Resources/Config/TestHarness/TestFixtureTiers.asset，真源=TestFixtureTiersConfig），
//  代码草案表已删（D552 迁 SO 授权；禁硬编码清单值终态达成）。
// ============================================================================
public enum FixtureTier { Opening = 0, Midgame = 1, Military = 2 }

public static class TestFixtureApi
{
    // ===== T6 三档档位表（核准版 SO；缓存读档，null 守卫指路资产路径） =====

    private static TestFixtureTiersConfig _tiers;

    private static TestFixtureTiersConfig LoadTiers()
    {
        if (_tiers != null) return _tiers;
        _tiers = Resources.Load<TestFixtureTiersConfig>("Config/TestHarness/TestFixtureTiers");
        if (_tiers == null)
            Debug.LogError("[TestFixture] 未找到 TestFixtureTiers.asset（Resources/Config/TestHarness/）——T6 三档档位 SO 缺失，PlaceKingdom 拒执行。");
        else if (_tiers.tiers == null || _tiers.tiers.Length < 3)
            Debug.LogError($"[TestFixture] TestFixtureTiers.asset tiers 数量={(_tiers.tiers != null ? _tiers.tiers.Length : 0)}（需 3：开局/中期/军事）——资产配置不完整。");
        return _tiers;
    }

    /// <summary>最近一次 PlaceKingdom 的注入台账（tier→实际落位计数，供探针/报告取证）。</summary>
    public static string LastPlacementLog { get; private set; } = "";

    // ===== T6：放置预设成熟王国 =====

    /// <summary>
    /// 在 pos 放置一档成熟王国（KingdomFoundry 注册链手动参数版）。
    /// 返回 KingdomState（失败 null）。全链确定性：零 rng、固定环序取点。
    /// </summary>
    public static KingdomState PlaceKingdom(FixtureTier tier, int raceId, Vector2Int pos, string name = null)
    {
        var registry = KingdomRegistry.Instance;
        var wm = WorldManager.Instance;
        var map = wm != null ? wm.ActiveMap : null;
        if (registry == null || map == null)
        { Debug.LogError("[TestFixture] PlaceKingdom 前置缺失（Registry/ActiveMap）——须先经 EnterGame 建局。"); return null; }

        var tiersCfg = LoadTiers();
        var spec = tiersCfg != null && tiersCfg.tiers != null && (int)tier < tiersCfg.tiers.Length
            ? tiersCfg.tiers[(int)tier] : null;
        if (spec == null) return null;   // LoadTiers 已 LogError 指路（资产缺失/配置不完整）
        int foundedDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;

        // 1) 注册（KingdomFoundry L50 同链：RegisterNewKingdom→raceId→personality）
        var state = registry.RegisterNewKingdom(
            name ?? $"Fixture_{spec.label}_{raceId}", new Color(0.55f, 0.35f, 0.2f), foundedDay, templateSourceId: -1);
        state.raceId = raceId;   // D471/D467：国族=人口种族真字段（GetKingdomRace 消费面）
        var raceDef = KingdomRace.GetKingdomRaceDef(state.id);
        state.personality = raceDef != null ? raceDef.GetBaselinePersonalityArray()
                                            : new float[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };   // 种族基准零扰动（确定性）

        // 2) 实体注入（生产链 SpawnUnit；T5 时序修复后 kingdomId 一次到位）
        int workersOk = 0, warriorsOk = 0;
        int unitIdx = 0;
        for (int i = 0; i < spec.workers; i++)
            if (SpawnFixtureUnit(Occupation.Worker, pos, kingdomId: state.id, idx: unitIdx++)) workersOk++;
        for (int i = 0; i < spec.warriors; i++)
            if (SpawnFixtureUnit(Occupation.Warrior, pos, kingdomId: state.id, idx: unitIdx++)) warriorsOk++;

        // 3) 建筑注入（生产链 CreateBuildingInstance，M6）：王座 castle + 清单 + House×N
        var buildingDefs = new List<string>(spec.buildings);
        for (int i = 0; i < spec.houses; i++) buildingDefs.Add("House");
        int buildingsOk = 0, houseOk = 0, buildIdx = 0;
        buildingsOk += PlaceFixtureBuilding("castle", map, pos, state.id, idx: buildIdx++);
        for (int i = 0; i < buildingDefs.Count; i++)
        {
            bool ok = PlaceFixtureBuilding(buildingDefs[i], map, pos, state.id, idx: buildIdx++) > 0;
            if (ok) { buildingsOk++; if (buildingDefs[i] == "House") houseOk++; }
        }

        // 4) 王国脑（2_17 步骤8 同链）+ 领土圈（动态立国先例）
        KingdomBrainFactory.Create(state.id);
        if (TerritorySystem.Instance != null) TerritorySystem.Instance.ClaimInitial(state.id);

        // 5) T8 三源同步注入
        var wn = WaterNetwork.Instance;
        float waterAdded = 0f;
        if (wn != null && !wn.IsBucketFull(state.id))
        {
            float room = wn.capacity - wn.GetStored(state.id);
            wn.AddWater(room, state.id);   // AI 桶注满（不触玩家桶，D545 主体对称纪律）
            waterAdded = wn.GetStored(state.id);
        }
        int warehouseStored = FillFixtureWarehouse(state.id, spec.warehouseFill);

        // 6) T9 底数 warm-up：注入单位饱食拉满（SatietySystem per-kingdom 均值读 unit.Satiety）
        //    幸福=日结按因子重算（饱食/税负/房容），注入饱食+房容后次日语境即过 60/50 阈值。
        int warmed = WarmUpUnitLifeState(state.id);

        LastPlacementLog = $"tier={spec.label} k{state.id} raceId={raceId} 工人={workersOk}/{spec.workers} 战士={warriorsOk}/{spec.warriors} " +
                           $"建筑={buildingsOk}/{1 + buildingDefs.Count}(House {houseOk}/{spec.houses}) 国库(g{spec.gold}/s{spec.stone}/w{spec.wood}/f{spec.food}/m{spec.metal}) " +
                           $"水桶={waterAdded:0}/占位仓={warehouseStored} warmup单位={warmed}";
        Debug.Log($"[TestFixture] PlaceKingdom 完成：{LastPlacementLog}");

        // T9 探针（注入时刻快照，正式判定在次日由演示容器复核）
        var birth = EvaluateBirthConditions(state.id);
        Debug.Log($"[TestFixture] T9 注入时生育条件快照 k{state.id}：幸福{birth.happiness:F0}(需>60) 饱食{birth.satiety:F0}(需>50) " +
                  $"房容{birth.houseCapacity} vs 人口{birth.population} 冷却由 aiBirthIntervalDays 起步");
        return state;
    }

    // ===== T10：无玩家模式（用户拍板默认 ON；幽灵化=清实体+GameOver 链封死，禁硬移除王国注册）=====

    /// <summary>
    /// 玩家幽灵化：①k0 实体清算（致死伤害→内部 Die→回池链，防对象池脏；先快照后遍历=HH.76 雷区纪律）
    /// ②玩家王国注册保留（M12 雷区：kingdomId=0 默认路径[NRE/守卫/路由]禁硬移除）
    /// ③GameOver 链封死=ThroneAnchor 考跑守卫（T0 定性产物：真实停跑机制=D249 工人全灭轮询）。
    /// 须在 TestHarness 开启后调用（守卫以 TestHarnessMode 为开关）。
    /// </summary>
    public static (int cleared, int kingdomCount) EnablePlayerGhostMode()
    {
        int cleared = 0;
        if (UnitRegistry.Instance != null)
        {
            var snapshot = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
            for (int i = 0; i < snapshot.Count; i++)
            {
                var u = snapshot[i];
                if (u == null || !u.IsAlive || u.kingdomId != 0) continue;
                u.TakeDamage((u.CurrentHp > 0 ? u.CurrentHp : 1) + 100000);
                cleared++;
            }
        }
        int kingdoms = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Count : 0;
        Debug.Log($"[TestFixture] 玩家幽灵化 ON：k0 实体清算 {cleared}，王国注册保留 {kingdoms}（GameOver 链已由 ThroneAnchor 考跑守卫封死，T0 定性）");
        return (cleared, kingdoms);
    }

    // ===== T7：建筑注入（生产链）——PlaceKingdom 内部即走此链；farm 派工行为探针归演示容器 =====

    // ===== T8：三源读数断言（演示容器消费）=====

    /// <summary>三源读数快照（国库/占位仓/水桶），供同源断言。</summary>
    public static (int gold, int stone, int wood, int food, int metal, int warehouse, float water) ReadThreeSources(int kingdomId)
    {
        var k = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        int wh = 0;
        var list = WarehouseRegistry.GatherActive(kingdomId);
        for (int i = 0; i < list.Count; i++) wh += list[i].Query().amount;
        float water = WaterNetwork.Instance != null ? WaterNetwork.Instance.GetStored(kingdomId) : 0f;
        if (k == null) return (0, 0, 0, 0, 0, wh, water);
        return (k.resources.gold, k.resources.stone, k.resources.wood, k.resources.food, k.resources.metal, wh, water);
    }

    // ===== T9：生育/招工条件读数（同 PopulationSystem.OnNewDayPerKingdom 口径的公开读数复刻）=====

    public static (float happiness, float satiety, int houseCapacity, int population, bool pass) EvaluateBirthConditions(int kingdomId)
    {
        var hap = HappinessSystem.Instance;
        var sat = SatietySystem.Instance;
        var k = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        if (hap == null || sat == null || k == null) return (0, 0, 0, 0, false);
        float happiness = hap.GetKingdomHappiness(kingdomId);
        float satiety = sat.GetAverageSatiety(kingdomId);
        int houseCapacity = hap.GetHouseCapacityByKingdom(kingdomId);
        int population = k.workerCount + k.warriorCount;
        bool pass = happiness > 60 && satiety > 50 && houseCapacity > population;
        return (happiness, satiety, houseCapacity, population, pass);
    }

    // ===== 内部：确定性取点与生产链封装 =====

    /// <summary>实体注入：生产链 SpawnUnit（Faction 门面；T5 后 kingdomId 先于事件到位）+ 固定环序取点 + 可走吸附。</summary>
    private static bool SpawnFixtureUnit(Occupation occ, Vector2Int center, int kingdomId, int idx)
    {
        if (UnitFactory.Instance == null) return false;
        var map = WorldManager.Instance.ActiveMap;
        Vector2Int cell = FixtureCell(map, center, idx);
        Vector3 world = WorldOf(cell);
        Vector2 spawnPos = SpawnPosSnapper.SnapWorld(world, $"fixture_k{kingdomId}_{occ}_{idx}");
        return UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, occ, spawnPos, kingdomId) != null;
    }

    /// <summary>建筑注入：生产链 CreateBuildingInstance（isPlayerBuilt=false + Active + kingdomId）。返回 1=成功。</summary>
    private static int PlaceFixtureBuilding(string defId, MapData map, Vector2Int center, int kingdomId, int idx)
    {
        var def = BuildingFactory.FindDefById(defId);
        if (def == null) { Debug.LogWarning($"[TestFixture] 建筑 defId={defId} 未找到，跳过。"); return 0; }
        Vector2Int cell = FixtureCell(map, center, idx, building: true);
        var fp = new Vector2Int(def.footprint.x > 0 ? def.footprint.x : 1, def.footprint.y > 0 ? def.footprint.y : 1);
        var coord = new GridCoord(cell.x, cell.y);
        var grid = GridSystem.Instance;
        Vector3 world = grid != null && grid.Config != null
            ? grid.CoordToWorld(coord) + new Vector2((fp.x - 1) * 0.5f * grid.Config.cellSize.x, (fp.y - 1) * 0.5f * grid.Config.cellSize.y)
            : new Vector3(coord.x, coord.y, 0f);
        return BuildingFactory.Instance != null && BuildingFactory.Instance.CreateBuildingInstance(
            def, def.sourceType, coord, fp, world,
            isPlayerBuilt: false, grade: ResourceGrade.Normal, isConsumable: false,
            initialState: BuildingState.Active, kingdomId: kingdomId) ? 1 : 0;
    }

    /// <summary>占位仓注入：本国 Warehouse 建筑的 StorageComponent 加满（读数断言用；不触他国仓）。</summary>
    private static int FillFixtureWarehouse(int kingdomId, int amount)
    {
        if (BuildingRegistry.Instance == null) return 0;
        var all = BuildingRegistry.Instance.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null || b.def == null || b.kingdomId != kingdomId || b.def.id != "Warehouse") continue;
            var sc = b.GetComponent<StorageComponent>();
            if (sc == null) continue;
            return sc.Add(amount);
        }
        Debug.LogWarning($"[TestFixture] k{kingdomId} 无本国 Warehouse（清单含 Warehouse 才有），占位仓跳过。");
        return 0;
    }

    /// <summary>底数 warm-up：本国 NPC 饱食=100（SatietySystem 均值口径；幸福由日结按因子重算）。</summary>
    private static int WarmUpUnitLifeState(int kingdomId)
    {
        if (UnitRegistry.Instance == null) return 0;
        int n = 0;
        var us = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
        for (int i = 0; i < us.Count; i++)
        {
            var u = us[i];
            if (u == null || !u.IsAlive || u.kingdomId != kingdomId) continue;
            u.Satiety = 100;
            n++;
        }
        return n;
    }

    /// <summary>固定环序取点（Foundry WorkerCell/BuildingCell 同款语义合并版；index0=中心，每环 8 方位外扩；零 rng）。</summary>
    private static Vector2Int FixtureCell(MapData map, Vector2Int center, int index, bool building = false)
    {
        if (index == 0) return MapGenRules.NearestWalkable(map, center.x, center.y);
        int r = building ? 2 + (index - 1) / 4 : 1 + (index - 1) / 8;
        int slot = (index - 1) % (building ? 4 : 8);
        int x = center.x, y = center.y;
        if (building)
        {
            switch (slot % 4)
            {
                case 0: y += r; break;
                case 1: x += r; break;
                case 2: y -= r; break;
                default: x -= r; break;
            }
        }
        else
        {
            switch (slot)
            {
                case 0: x += r; break;
                case 1: x += r; y += r; break;
                case 2: y += r; break;
                case 3: x -= r; y += r; break;
                case 4: x -= r; break;
                case 5: x -= r; y -= r; break;
                case 6: y -= r; break;
                default: x += r; y -= r; break;
            }
        }
        return MapGenRules.NearestWalkable(map, x, y);
    }

    private static Vector3 WorldOf(Vector2Int cell)
    {
        var grid = GridSystem.Instance;
        if (grid != null && grid.Config != null) return (Vector3)grid.CoordToWorld(new GridCoord(cell.x, cell.y));
        return new Vector3(cell.x, cell.y, 0f);
    }
}
