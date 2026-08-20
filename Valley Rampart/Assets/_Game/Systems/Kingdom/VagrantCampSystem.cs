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
public class VagrantCampSystem : Singleton<VagrantCampSystem>
{
    private const string CAMP_DEF_ID = "VagrantCamp";
    private const float ARRIVE_SCAN_INTERVAL = 0.5f;   // 抵达扫描节流（秒）
    private const float RECRUIT_TASK_EXPIRY = 120f;    // 走回任务刺激有效期（秒）
    private const float RECRUIT_TASK_INTENSITY = 3f;   // 任务刺激强度

    private KingdomConfig _config;
    private float _arriveScanTimer;
    private bool _mapReady;

    protected override void Awake()
    {
        base.Awake();
        _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
    }

    KingdomConfig GetCfg()
    {
        if (_config == null) _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        return _config;
    }

    /// <summary>新游戏地图就绪：每个营地初始生成 campInitialVagrants 名流浪汉（GameBootstrap 调）。</summary>
    public void OnNewGameMapReady()
    {
        var cfg = GetCfg();
        _mapReady = true;
        if (cfg == null) return;

        var camps = FindCamps();
        int spawned = 0;
        for (int i = 0; i < camps.Count; i++)
            for (int v = 0; v < cfg.campInitialVagrants; v++)
                if (SpawnVagrantNear(camps[i])) spawned++;

        Debug.Log($"[VagrantCampSystem] 初始流浪汉生成: {spawned}（营地={camps.Count}，每营地{cfg.campInitialVagrants}）");
    }

    /// <summary>每日补员（DayCycleSettlement 调）：不满营地补 campDailyRefill，刷满 campMaxVagrants 停。</summary>
    public void OnNewDay()
    {
        var cfg = GetCfg();
        if (cfg == null || !_mapReady) return;

        var camps = FindCamps();
        int spawned = 0;
        for (int i = 0; i < camps.Count; i++)
        {
            int count = CountVagrantsNear(camps[i].GetPosition(), cfg.campVagrantRadiusCells);
            if (count >= cfg.campMaxVagrants) continue;
            int refill = Mathf.Min(cfg.campMaxVagrants - count, cfg.campDailyRefill);
            for (int v = 0; v < refill; v++)
                if (SpawnVagrantNear(camps[i])) spawned++;
        }
        if (spawned > 0)
            Debug.Log($"[VagrantCampSystem] 每日补员: +{spawned} 流浪汉");
    }

    /// <summary>当前是否招募得起（粮 ≥ recruitFoodCost）。点击交互 CanTrigger 用（3.5.1 §6.3：无粮回落对话）。</summary>
    public bool CanRecruit()
    {
        var cfg = GetCfg();
        return cfg != null && RulerController.Instance != null
               && RulerController.Instance.Food >= cfg.recruitFoodCost;
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
        if (_arriveScanTimer < ARRIVE_SCAN_INTERVAL) return;
        _arriveScanTimer = 0f;

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

    /// <summary>在营地半径内随机位置生成 1 名流浪汉实体。</summary>
    bool SpawnVagrantNear(Building camp)
    {
        var cfg = GetCfg();
        if (camp == null || cfg == null || UnitFactory.Instance == null) return false;

        Vector2 campPos = camp.GetPosition();
        var grid = GridSystem.Instance;
        float cs = grid != null && grid.Config != null ? grid.Config.cellSize.x : 2.26f;
        float offsetX = Random.Range(-cfg.campVagrantRadiusCells * 0.5f, cfg.campVagrantRadiusCells * 0.5f) * cs;

        var go = UnitFactory.Instance.SpawnUnit(
            Faction.Human_Player, Occupation.Vagrant,
            new Vector2(campPos.x + offsetX, campPos.y));
        if (go == null) return false;

        // QQQ.2 T11 / DR-7：记录出生营地坐标（未招募流浪汉 HomePoint = 本值，在营地游荡不朝王国走）
        var uc = go.GetComponent<UnitController>();
        if (uc != null) uc.BirthCampPos = campPos;
        return true;
    }
}
