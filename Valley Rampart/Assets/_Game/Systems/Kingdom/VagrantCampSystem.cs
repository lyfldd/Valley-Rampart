using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 流浪汉营地系统（3.5.1 §4.1，E-S7）：前期人口补充来源。
/// 营地建筑由 WorldManager.PlaceVagrantCamps 地图生成（BuildingType.VagrantCamp → Building 实例），
/// 本系统负责：开局/每日补员流浪汉实体、招募交互、招募走回抵达入册。
///
/// 无存档设计：流浪汉实体走各自 UnitSaveData（Scene 阶段）；营地建筑靠地图种子确定性复现；
/// 营地↔流浪汉为半径关联（campVagrantRadiusCells），不持久化映射。
/// 抵达入册为自愈式扫描（IsVagrantRecruited 且未入册 + 抵达锚点半径），读档后自动重建，无需 pending 列表。
/// </summary>
public class VagrantCampSystem : Singleton<VagrantCampSystem>, ISaveable
{
    private const string CAMP_DEF_ID = "VagrantCamp";
    private const float ARRIVE_SCAN_INTERVAL = 0.5f;   // 抵达扫描节流（秒）
    private const float RECRUIT_TASK_EXPIRY = 120f;    // 走回任务刺激有效期（秒）
    private const float RECRUIT_TASK_INTENSITY = 3f;   // 任务刺激强度
    private const float CAMP_SCAN_INTERVAL = 3f;       // 营地结营/散营扫描节流（秒，2_16 步骤9）

    /// <summary>结营阈值：营地半径内 ≥3 未招募流浪汉 → 结营（D301 占位；步骤11 统一落 FoundingConfig）。</summary>
    private const int CAMP_ESTABLISH_THRESHOLD = 3;
    /// <summary>散营人数下限：存续成员 &lt;2 → 散营（D387 修订，滞回带 [2,3) 防抖动；杀散才是真正阻止）。</summary>
    private const int CAMP_DISPERSAL_THRESHOLD = 2;

    private KingdomConfig _config;
    private float _arriveScanTimer;
    private float _campScanTimer;
    private bool _mapReady;

    /// <summary>当前活跃营地聚落记录（2_16 步骤9）。</summary>
    private readonly List<Camp> _camps = new List<Camp>();

    /// <summary>读档还原的营地种子（centerCell/persistenceDays），ScanCamps 自愈重建关联（成员/建筑标识不入档）。</summary>
    private List<Camp> _restoredCampSeeds;

    /// <summary>每日补员确定性随机源由世界种子^当日派生（R4，对齐 OnNewGameMapReady 纪律）——
    /// 修复 HH.27 A3 非种子随机漂移根因（原未播种 System.Random 逐轮随当前时间变化 → 同 seed 两轮分叉）。</summary>
    private System.Random NewDayRng()
    {
        var wm = WorldManager.Instance;
        var map = wm != null ? wm.ActiveMap : null;
        int seed = map != null && map.seed != 0 ? map.seed
                 : wm != null ? wm.MapSeed : 1;
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
        return new System.Random(seed ^ (day * 7919));   // 每日不同确定性流；同 (seed, day) 恒复现
    }

    protected override void Awake()
    {
        base.Awake();
        _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        if (_instance != this) return;
        SaveManager.Instance?.RegisterSaveable(this);   // 2_16 步骤9：营地存续计数入档（对齐 Building.Awake 注册惯例）
    }

    KingdomConfig GetCfg()
    {
        if (_config == null) _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        return _config;
    }

    /// <summary>新游戏地图就绪：按 D308 地图级口径预置初始流民（GameBootstrap 调）。
    /// 全图 4~6 人（档内 rng 取值）——保底 baseline=3 人同点（早期必结营）+ 余数散投无主地；
    /// 确定性：rng 由世界种子派生，同 seed 复现（冒烟 #9）。KingdomConfig.campInitialVagrants 退役。</summary>
    public void OnNewGameMapReady()
    {
        _mapReady = true;

        var fc = Resources.Load<KingdomFoundingConfig>("Config/Kingdoms/KingdomFoundingConfig");
        var wm = WorldManager.Instance;
        var map = wm != null ? wm.ActiveMap : null;
        if (map == null)
        {
            Debug.LogWarning("[VagrantCampSystem] D308：无 ActiveMap，跳过初始流民预置。");
            return;
        }

        int seed = map.seed != 0 ? map.seed : (wm != null ? wm.MapSeed : 1);
        var rng = new System.Random(seed);
        int total = fc != null ? rng.Next(fc.initialVagrantTotalMin, fc.initialVagrantTotalMax + 1) : 4;
        int baseline = fc != null ? Mathf.Max(1, fc.baselineGroupSize) : 3;
        int rest = Mathf.Max(0, total - baseline);

        int spawned = 0;
        // D308 修订（D469，HH.51 批B）：初始流民按族群投放——保底 baseline 人同点必须同族（D468 野性敌意下混合群开局互杀）、
        // 余数散投按族成群（按 baseline 大小切块成组，同组同族同锚点落位）。
        // 族别来源挂账：Q10-M2 接真模板映射前全 Human（结构就位，当前世界全默认 Human，D416 前口径）。
        int anchorRace = KingdomRace.GetKingdomRace(0);
        // 保底 baseline 人同点（早期必结营；必须同族）
        var anchor = PickCell(map, rng, 6);
        if (anchor.x >= 0)
        {
            var anchorWorld = CellToWorld(anchor);
            for (int i = 0; i < baseline; i++)
                if (SpawnVagrantAt(anchorWorld, rng, anchorRace)) spawned++;
        }
        // 余数散投按族成群：每 baseline 人切一组，组内同族同锚点
        for (int g = 0; g < rest; g += baseline)
        {
            int groupSize = Mathf.Min(baseline, rest - g);
            var cell = PickCell(map, rng, 8);
            if (cell.x < 0) continue;
            int groupRace = KingdomRace.GetKingdomRace(0);   // 组族别：同上挂账（Q10-M2 后按地图族分布映射）
            var anchorWorld = CellToWorld(cell);
            for (int i = 0; i < groupSize; i++)
                if (SpawnVagrantAt(anchorWorld, rng, groupRace)) spawned++;
        }

        Debug.Log($"[VagrantCampSystem] D308 初始流民预置（按族投放 D469 修订）: {spawned}/{total}（保底同点 {baseline}×raceId={anchorRace} + 散投按族成群 {rest}）seed={seed}");
    }

    /// <summary>每日补员（DayCycleSettlement 调）：不满营地补 campDailyRefill，刷满 campMaxVagrants 停。</summary>
    public void OnNewDay()
    {
        var cfg = GetCfg();
        if (cfg == null || !_mapReady) return;

        var camps = FindCamps();
        int spawned = 0;
        var rng = NewDayRng();
        for (int i = 0; i < camps.Count; i++)
        {
            int count = CountVagrantsNear(camps[i].GetPosition(), cfg.campVagrantRadiusCells);
            if (count >= cfg.campMaxVagrants) continue;
            int refill = Mathf.Min(cfg.campMaxVagrants - count, cfg.campDailyRefill);
            for (int v = 0; v < refill; v++)
                if (SpawnVagrantNear(camps[i], rng)) spawned++;
        }
        if (spawned > 0)
            Debug.Log($"[VagrantCampSystem] 每日补员: +{spawned} 流浪汉");

        TickCampPersistence();   // 2_16 步骤9 D313：营地存续日 +1（驱散/屠杀不清零，干预=拖延）
    }

    /// <summary>当前是否招募得起（粮 ≥ recruitFoodCost）。点击交互 CanTrigger 用（3.5.1 §6.3：无粮回落对话）。</summary>
    public bool CanRecruit()
    {
        var cfg = GetCfg();
        return cfg != null && RulerController.Instance != null
               && RulerController.Instance.GetResource(ResourceType.Food) >= cfg.recruitFoodCost;
    }

    /// <summary>
    /// 招募流浪汉（统一点击交互 E-S8 入口）：花 recruitFoodCost 粮 → 转居民 → TaskStimulus 走回王国锚点。
    /// 粮不足/目标非法返回 false。
    /// </summary>
    public bool RecruitVagrant(UnitController unit)
    {
        var cfg = GetCfg();
        if (unit == null || cfg == null) return false;
        if (!unit.IsAlive || unit.EffectiveOccupation != Occupation.Vagrant) return false;

        // D469 招募限同族（HH.51 批B）：异族流民=永久野人不可招募/不可转化/不可回收——拒绝+日志（玩家侧）。
        // 粮扣除之前拒绝，零资源损耗。
        if (unit.raceId != KingdomRace.GetKingdomRace(0))
        {
            Debug.LogWarning($"[VagrantCampSystem] 招募拒绝：异族流民#{unit.npcId}（raceId={unit.raceId} vs 玩家国族={KingdomRace.GetKingdomRace(0)}）——同族锁定 D469，异族=永久野人。");
            return false;
        }

        var ruler = RulerController.Instance;
        if (ruler == null || ruler.Food < cfg.recruitFoodCost)
        {
            Debug.LogWarning($"[VagrantCampSystem] 招募失败：粮不足（需 {cfg.recruitFoodCost}，有 {(ruler != null ? ruler.Food : 0)}）");
            return false;
        }

        ruler.ModifyResource(ResourceType.Food, false, cfg.recruitFoodCost);
        unit.SetOccupation(Occupation.Resident);
        unit.IsVagrantRecruited = true;

        // QQQ.2 T9 / DR-4：招募走 = 原任务上下文失效——清调度器指派（流浪汉若被派任务，招募后不再执行）
        if (TaskScheduler.HasInstance && unit.npcId != 0)
            TaskScheduler.Instance.AbandonTask(unit.npcId);

        // 军令级任务刺激走回王国锚点；抵达后由 Update 扫描正式入册（人口 +1）
        Vector2 anchor = WorldManager.Instance != null ? WorldManager.Instance.GetKingdomAnchorWorld() : Vector2.zero;
        var brain = unit.GetComponent<NPCBrain>();
        if (brain != null)
        {
            brain.AddTaskStimulus(new TaskStimulus(
                TaskPriority.S, Vector2XUnity.FromUnity(anchor), RECRUIT_TASK_INTENSITY,
                expiry: Time.time + RECRUIT_TASK_EXPIRY, issuer: this));
        }
        Debug.Log($"[VagrantCampSystem] 招募流浪汉（粮-{cfg.recruitFoodCost}）→ 居民，走回王国 @ {anchor}");
        return true;
    }

    // ===== 抵达入册（自愈式：读档后自动重建，无需持久化）=====

    void Update()
    {
        if (!_mapReady) return;
        _arriveScanTimer += Time.deltaTime;
        if (_arriveScanTimer >= ARRIVE_SCAN_INTERVAL)
        {
            _arriveScanTimer = 0f;
            ScanArrive();
        }

        // 2_16 步骤9：营地结营/散营扫描（独立节流；成员表自愈刷新，读档后靠此重建关联）
        _campScanTimer += Time.deltaTime;
        if (_campScanTimer >= CAMP_SCAN_INTERVAL)
        {
            _campScanTimer = 0f;
            ScanCamps();
        }
    }

    void ScanArrive()
    {

        var cfg = GetCfg();
        var pop = PopulationSystem.Instance;
        var grid = GridSystem.Instance;
        if (cfg == null || pop == null || grid == null || grid.Config == null || UnitRegistry.Instance == null) return;

        Vector2 anchor = WorldManager.Instance != null ? WorldManager.Instance.GetKingdomAnchorWorld() : Vector2.zero;
        float arriveRadiusWorld = cfg.recruitArriveRadiusCells * grid.Config.cellSize.x;

        foreach (var uc in UnitRegistry.Instance.GetAllUnits())
        {
            if (uc == null || !uc.IsVagrantRecruited) continue;
            if (pop.IsRegistered(uc)) continue;
            if (Mathf.Abs(uc.transform.position.x - anchor.x) <= arriveRadiusWorld)
                pop.RegisterEntity(uc);   // RegisterEntity 幂等 + 资格校验（居民职业合格）
        }
    }

    // ===== 营地/流浪汉查询 =====

    /// <summary>查找所有营地 Building（BuildingRegistry 中 def.id == VagrantCamp）。</summary>
    public List<Building> FindCamps()
    {
        var result = new List<Building>();
        if (BuildingRegistry.Instance == null) return result;
        foreach (var b in BuildingRegistry.Instance.All)
        {
            if (b != null && b.def != null && b.def.id == CAMP_DEF_ID)
                result.Add(b);
        }
        return result;
    }

    /// <summary>统计指定位置半径内的未招募流浪汉数。</summary>
    int CountVagrantsNear(Vector2 center, float radiusCells)
    {
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null || UnitRegistry.Instance == null) return 0;
        float radiusWorld = radiusCells * grid.Config.cellSize.x;

        int count = 0;
        foreach (var uc in UnitRegistry.Instance.GetAllUnits())
        {
            if (uc == null || !uc.IsAlive) continue;
            if (uc.EffectiveOccupation != Occupation.Vagrant) continue;
            if (uc.IsVagrantRecruited) continue;
            if (Vector2.Distance((Vector2)uc.transform.position, center) <= radiusWorld)
                count++;
        }
        return count;
    }

    /// <summary>在营地半径内随机位置生成 1 名流浪汉实体（每日补员用；rng 传入，R4 禁 UnityEngine.Random）。</summary>
    bool SpawnVagrantNear(Building camp, System.Random rng)
    {
        var cfg = GetCfg();
        if (camp == null || cfg == null || UnitFactory.Instance == null) return false;

        Vector2 campPos = camp.GetPosition();
        var grid = GridSystem.Instance;
        float cs = grid != null && grid.Config != null ? grid.Config.cellSize.x : 2.26f;
        float offsetX = (float)((rng.NextDouble() * 2.0 - 1.0) * cfg.campVagrantRadiusCells * 0.5f) * cs;

        // 寻路2（HH.48）：落点不可走→就近可走吸附（BirthCampPos 仍=营地语义点不变）
        Vector2 spawnPos = SpawnPosSnapper.SnapWorld(new Vector2(campPos.x + offsetX, campPos.y), "流民补员");
        var go = UnitFactory.Instance.SpawnUnit(
            Faction.PlayerCamp, Occupation.Vagrant, spawnPos);
        if (go == null) return false;

        // QQQ.2 T11 / DR-7：记录出生营地坐标（未招募流浪汉 HomePoint = 本值，在营地游荡不朝王国走）
        var uc = go.GetComponent<UnitController>();
        if (uc != null)
        {
            uc.BirthCampPos = campPos;
            // D308/D468 构造性同族营（HH.51 批B）：补员按营族投放（成员 raceId 多数派；异族补员会被野性敌意互攻拆营）。
            // 空营/不可考 → Human 兜底（D467 兜底口径）。
            bool campRaceTie;
            uc.raceId = KingdomRace.ResolveGroupRace(CollectMembers(campPos, cfg), rng, out campRaceTie);
        }
        return true;
    }

    // ===== D308 初始流民（地图级预置，确定性派生自世界种子）=====

    /// <summary>在指定世界点生成 1 名流浪汉（带微小抖动避免完全叠位；滞留该处游荡——BirthCampPos=落点）。
    /// race=所属族群（D308 修订按族投放，HH.51 批B；组内同族保证）。</summary>
    bool SpawnVagrantAt(Vector2 worldPos, System.Random rng, int race)
    {
        if (UnitFactory.Instance == null) return false;
        var grid = GridSystem.Instance;
        float cs = grid != null && grid.Config != null ? grid.Config.cellSize.x : 2.26f;
        float jx = (float)((rng.NextDouble() * 2.0 - 1.0) * 0.3 * cs);
        // 寻路2（HH.48）：落点不可走→就近可走吸附；BirthCampPos=吸附后实际落点（滞留该处游荡语义对齐实体站位）
        Vector2 spawnPos = SpawnPosSnapper.SnapWorld(new Vector2(worldPos.x + jx, worldPos.y), "初始流民");
        var go = UnitFactory.Instance.SpawnUnit(
            Faction.PlayerCamp, Occupation.Vagrant, spawnPos);
        if (go == null) return false;
        var uc = go.GetComponent<UnitController>();
        if (uc != null)
        {
            uc.BirthCampPos = spawnPos;   // 滞留该处游荡（HomePoint=落点，不朝王国走）
            uc.raceId = race;             // D467：流民个体种族=投放族群（终身字段）
        }
        return true;
    }

    /// <summary>取 D308 落点：rng 抽样→就近可走格，且离王国出生点（含玩家）足够远放下（无主地粗口径）。回退地图中心。</summary>
    Vector2Int PickCell(MapData map, System.Random rng, int minDistToSpawn)
    {
        for (int a = 0; a < 32; a++)
        {
            int x = rng.Next(4, Mathf.Max(8, map.width - 4));
            int y = rng.Next(4, Mathf.Max(8, map.height - 4));
            var cell = MapGenRules.NearestWalkable(map, x, y);
            if (cell.x < 0) continue;
            if (FarFromSpawns(cell, map, minDistToSpawn)) return cell;
        }
        return MapGenRules.NearestWalkable(map, map.width / 2, map.height / 2);
    }

    static bool FarFromSpawns(Vector2Int p, MapData map, int minDist)
    {
        if (map.kingdomSpawns == null) return true;
        for (int i = 0; i < map.kingdomSpawns.Count; i++)
        {
            var s = map.kingdomSpawns[i];
            if (Mathf.Abs(s.x - p.x) + Mathf.Abs(s.y - p.y) < minDist) return false;
        }
        return true;
    }

    static Vector2 CellToWorld(Vector2Int cell)
    {
        var grid = GridSystem.Instance;
        if (grid != null && grid.Config != null)
        {
            var v = grid.CoordToWorld(new GridCoord(cell.x, cell.y));
            return new Vector2(v.x, v.y);
        }
        float cs = 2.26f;
        return new Vector2(cell.x * cs, cell.y * cs);
    }

    // ===== 2_16 步骤9：营地聚落运行时数据（D301 结营 / D313 存续不清零 / D387 散营滞回带）=====

    /// <summary>日 tick：活跃营地存续日 +1（D313：驱散不清零，只增不重置）。</summary>
    void TickCampPersistence()
    {
        if (_camps.Count == 0) return;
        for (int i = 0; i < _camps.Count; i++) _camps[i].persistenceDays++;
    }

    /// <summary>
    /// 营地扫描：①未结营建筑半径 ≥3 → 结营建记录；②已结营建筑刷新成员，存续成员 &lt;2 → 散营移除记录（建筑保留）；
    /// ③读档种子经 centerCell 匹配建筑自愈重建（成员/建筑标识不入档，先扫先建、无建筑则弃=散营）。
    /// </summary>
    void ScanCamps()
    {
        var cfg = GetCfg();
        var grid = GridSystem.Instance;
        if (cfg == null || grid == null || grid.Config == null || BuildingRegistry.Instance == null) return;

        var camps = FindCamps();
        foreach (var b in camps)
        {
            if (b == null) continue;
            var cell = grid.WorldToCoord(b.GetPosition());
            if (cell == null) continue;
            int idx = _camps.FindIndex(c => c.centerCell.x == cell.Value.x && c.centerCell.y == cell.Value.y);
            bool established = idx >= 0;
            int count = CountVagrantsNear(b.GetPosition(), cfg.campVagrantRadiusCells);

            if (!established)
            {
                if (count >= CAMP_ESTABLISH_THRESHOLD)
                {
                    var camp = new Camp(cell.Value, b.GetInstanceID())
                    {
                        memberIds = CollectMembers(b.GetPosition(), cfg)
                    };
                    _camps.Add(camp);
                    Debug.Log($"[VagrantCampSystem] 结营 @ ({cell.Value.x},{cell.Value.y})：{count} 名流浪汉，存续从 0 起");
                }
            }
            else
            {
                var c = _camps[idx];
                c.memberIds = CollectMembers(b.GetPosition(), cfg);   // 成员表自愈刷新
                if (c.memberIds.Count < CAMP_DISPERSAL_THRESHOLD)
                {
                    _camps.RemoveAt(idx);
                    Debug.Log($"[VagrantCampSystem] 散营 @ ({cell.Value.x},{cell.Value.y})：存续成员 {c.memberIds.Count} &lt; {CAMP_DISPERSAL_THRESHOLD}，营地建筑保留");
                }
            }
        }

        // 读档种子重建（centerCell 匹配；无匹配建筑 → 丢弃）
        if (_restoredCampSeeds != null && _restoredCampSeeds.Count > 0)
        {
            int n = _restoredCampSeeds.Count;
            for (int i = n - 1; i >= 0; i--)
            {
                var seed = _restoredCampSeeds[i];
                bool matched = false;
                foreach (var b in camps)
                {
                    if (b == null) continue;
                    var cell = grid.WorldToCoord(b.GetPosition());
                    if (cell == null) continue;
                    if (cell.Value.x == seed.centerCell.x && cell.Value.y == seed.centerCell.y)
                    {
                        if (!_camps.Exists(c => c.centerCell.x == cell.Value.x && c.centerCell.y == cell.Value.y))
                        {
                            _camps.Add(new Camp(seed.centerCell, b.GetInstanceID())
                            {
                                persistenceDays = seed.persistenceDays,
                                memberIds = CollectMembers(b.GetPosition(), cfg)
                            });
                            Debug.Log($"[VagrantCampSystem] 读档重建营地 @ ({seed.centerCell.x},{seed.centerCell.y})：存续 {seed.persistenceDays} 日");
                        }
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                    Debug.Log($"[VagrantCampSystem] 读档丢弃营地种子 @ ({seed.centerCell.x},{seed.centerCell.y})：无匹配建筑（散营）");
                _restoredCampSeeds.RemoveAt(i);
            }
        }
    }

    /// <summary>收集营地半径内未招募流浪汉 npcId（成员表，每 tick 扫描刷新，幂等自愈）。</summary>
    List<int> CollectMembers(Vector2 center, KingdomConfig cfg)
    {
        var members = new List<int>();
        var grid = GridSystem.Instance;
        if (grid == null || grid.Config == null || UnitRegistry.Instance == null) return members;
        float rw = cfg.campVagrantRadiusCells * grid.Config.cellSize.x;
        foreach (var uc in UnitRegistry.Instance.GetAllUnits())
        {
            if (uc == null || !uc.IsAlive || uc.EffectiveOccupation != Occupation.Vagrant || uc.IsVagrantRecruited) continue;
            if (Vector2.Distance((Vector2)uc.transform.position, center) <= rw) members.Add(uc.npcId);
        }
        return members;
    }

    /// <summary>当前营地聚落记录数（调试/冒烟查询）。</summary>
    public int CampCount => _camps.Count;

    /// <summary>当前营地聚落记录（只读，2_16 步骤11 CampUpgrader 遍历用）。</summary>
    public IReadOnlyList<Camp> Camps => _camps;

    /// <summary>移除一条营地记录（2_16 步骤11：动态立国/吞并出口B 后移除，营地建筑保留可再结营）。</summary>
    public void RemoveCamp(Camp camp) => _camps.Remove(camp);

    /// <summary>
    /// 跨轮清场（HH.66 段B#3，D522 挂账清偿）：营地记录/读档种子/地图就绪态清空。
    /// 消费方=WorldLifecycle.ResetWorldForNext ⑤ 散点（同场景重建时 _camps 记录的旧世界营地坐标
    /// 与新地图格子不对应，残留会让 ScanCamps/补员行为错乱）；营地建筑实体随 BuildingFactory 清场走。
    /// </summary>
    public void ResetState()
    {
        _camps.Clear();
        _restoredCampSeeds = null;
        _mapReady = false;
    }

    /// <summary>强制立即营地扫描（冒烟验收钩子，确定性地驱动结营/散营，避免依赖 Update 节流时序）。</summary>
    public void ForceCampScan() => ScanCamps();

    /// <summary>营地聚落调试摘要（中心格/存续日/成员数），供冒烟验收取证。</summary>
    public string DumpCamps()
    {
        if (_camps.Count == 0) return "[0 营地]";
        var sb = new System.Text.StringBuilder();
        foreach (var c in _camps)
            sb.Append($"({c.centerCell.x},{c.centerCell.y})pd={c.persistenceDays}mem={c.memberIds.Count} ");
        return $"[{_camps.Count} 营地] {sb.ToString().TrimEnd()}";
    }

    // ===== 2_16 步骤9 ISaveable：只有 persistenceDays/centerCell 入档（设计 §1.1"Camp 存续计数"）=====

    public string SaveId => "VagrantCampSystem";
    /// <summary>Scene：营地建筑按存档在场景恢复后，由 ScanCamps 自愈重建关联。</summary>
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Scene;

    public SavePayload SaveState()
    {
        var data = new CampListSaveData { camps = new List<CampEntrySaveData>(_camps.Count) };
        for (int i = 0; i < _camps.Count; i++)
        {
            var c = _camps[i];
            data.camps.Add(new CampEntrySaveData
            {
                cellX = c.centerCell.x,
                cellY = c.centerCell.y,
                persistenceDays = c.persistenceDays
            });
        }
        return new SavePayload
        {
            typeName = typeof(CampListSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = 1
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(CampListSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<CampListSaveData>(payload.json);
        _restoredCampSeeds = new List<Camp>();
        if (data.camps != null)
        {
            foreach (var e in data.camps)
                _restoredCampSeeds.Add(new Camp(new GridCoord(e.cellX, e.cellY), -1) { persistenceDays = e.persistenceDays });
        }
        // 读档即世界已就绪：恢复营地扫描/补员（对齐 OnNewGameMapReady 置位）
        _mapReady = true;
        Debug.Log($"[VagrantCampSystem] 读档准备恢复营地 {_restoredCampSeeds.Count} 条（ScanCamps 自愈重建关联）");
    }
}

/// <summary>营地存续列表存档（2_16 步骤9；只持久化存续计数与中心格，成员表读档后扫描重建）。</summary>
[System.Serializable]
public struct CampListSaveData
{
    public List<CampEntrySaveData> camps;
}

/// <summary>单营地存档条目。</summary>
[System.Serializable]
public struct CampEntrySaveData
{
    public int cellX;
    public int cellY;
    public int persistenceDays;
}
