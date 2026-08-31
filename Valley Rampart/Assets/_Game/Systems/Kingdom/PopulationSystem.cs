using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 人口系统（3.5.1 实体化核心；Singleton + ISaveable, Global）。
///
/// 3.5.1 §3.2 实体化（E-S2）：人口从计数制改为实体制——本系统维护王国领域内 NPC 实体注册表，
/// PopulationCount 为注册表派生值（不再独立维护）。生育/出生/长大/招募/转职/死亡均通过注册表增删实体。
///   - 注册范围：PlayerCamp 的君主/居民/小孩/工人/军事职业（不含机器工事；Vagrant 在王国领域外不计入，招募抵达后才注册）
///   - 自动注册：订阅 UnitSpawnedEvent（合格实体入表）/ UnitDiedEvent（死亡出表）
///   - 存档：实体本体走各自 UnitController 的 UnitSaveData（Scene 阶段），读档时 SpawnFromSave 触发事件自动回表
///
/// 生育事件：每日结算，三层前置（全局幸福/饱食 + 房屋容量 + 个体冷却），见 OnNewDay。
/// </summary>
public class PopulationSystem : Singleton<PopulationSystem>, ISaveable
{
    public string SaveId => "PopulationSystem";
    public SaveLoadPhase LoadPhase => SaveLoadPhase.Global;

    private KingdomConfig _config;

    // ===== 3.5.1 E-S2：实体注册表（王国领域内 NPC 实体）=====
    private readonly List<UnitController> _entities = new List<UnitController>();

    /// <summary>王国人口数 = 实体注册表数量（3.5.1 §3.2 派生值，不再独立维护）。</summary>
    public int PopulationCount => _entities.Count;

    /// <summary>实体注册表只读视图（饱食/幸福/生育/交互共用同一实体集合，§8.2）。</summary>
    public IReadOnlyList<UnitController> Entities => _entities;

    /// <summary>生育冷却倒计时（天，到 0 且满足条件即结算配对）。</summary>
    public int BirthCooldownDays { get; private set; }

    /// <summary>平均饱食（SatietySystem 平均）。</summary>
    public float AvgSatiety { get; private set; } = 50f;

    /// <summary>平均幸福（HappinessSystem 整体幸福）。</summary>
    public float AvgHappiness { get; private set; } = 50f;

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        BirthCooldownDays = LifeConfig().birthCooldownDefault;
        SaveManager.Instance.RegisterSaveable(this);

        // E-S2：出生/死亡事件驱动注册表增删
        EventBus.Subscribe<UnitSpawnedEvent>(OnUnitSpawned);
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    protected override void OnDestroy()
    {
        if (_instance == this)
        {
            EventBus.Unsubscribe<UnitSpawnedEvent>(OnUnitSpawned);
            EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
        }
        base.OnDestroy();
    }

    private KingdomConfig LifeConfig()
    {
        if (_config == null) _config = Resources.Load<KingdomConfig>("Config/KingdomConfig");
        return _config;
    }

    // ===== 实体注册表管理（3.5.1 §3.2）=====

    /// <summary>该职业是否属于王国人口实体（机器/工事/领域外流浪汉不计入；君主计入，§3.3 开局 10 含君主）。</summary>
    public static bool IsPopulationOccupation(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.SiegeMachine:
            case Occupation.Ballista:
            case Occupation.Tower:
            case Occupation.ArrowTower:
            case Occupation.CrossbowTower:
            case Occupation.MagicTower:
            case Occupation.Wall:
            case Occupation.Gate:
            case Occupation.Vagrant:   // 王国领域外（营地），招募抵达王国后才转居民入表（§4.1）
                return false;
            default:
                return true;
        }
    }

    // ===== 2_17 步骤4：per-kingdom 派生统计（台账转派生，实体=唯一真源）=====
    // ①真源演进规则（§〇 追记裁决①）：Foundry 不再手写 workerCount/warriorCount 台账，
    // KingdomState.workerCount/warriorCount 改由本系统对实体按 kingdomId 派生——防双真源漂移。
    // 玩家=桶 0，AI=各自桶；流浪汉（领域外，kingdomId 归属营地并非王国）双条件过滤不误计。
    // ③附注：步骤3 的 IsPopulationEntity 守卫（kingdomId>0 排除）仍保障玩家 _entities 桶0 不被 AI 实体污染；
    // 此处派生统计按 kingdomId 明确分桶计数，不与之重复写过滤（派生=读现有注册表/单位，不新增注册）。

    /// <summary>按国派生存活实体计数（排除流浪汉；occs 为空则不限职业）。2_17 步骤4 派生统计核。</summary>
    public static int CountAliveByKingdom(int kingdomId, params Occupation[] occs)
    {
        if (UnitRegistry.Instance == null) return 0;
        int n = 0;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive) continue;
            if (u.kingdomId != kingdomId) continue;
            var occ = u.EffectiveOccupation;
            if (occ == Occupation.Vagrant) continue;                 // 流浪汉双条件过滤不误计
            if (occs != null && occs.Length > 0 && !MatchAny(occs, occ)) continue;
            n++;
        }
        return n;
    }

    /// <summary>按国存活工人数（工人口径=Worker/Porter/Civilian，对齐 ThroneAnchor）。</summary>
    public static int AliveWorkerCount(int kingdomId) =>
        CountAliveByKingdom(kingdomId, Occupation.Worker, Occupation.Porter, Occupation.Civilian);

    /// <summary>按国存活战士数（军事职业）。</summary>
    public static int AliveWarriorCount(int kingdomId) =>
        CountAliveByKingdom(kingdomId, Occupation.Warrior, Occupation.Archer, Occupation.Mage,
            Occupation.General, Occupation.Crossbowman, Occupation.HeavyWarrior, Occupation.Bishop,
            Occupation.ShieldGuard, Occupation.Archmage, Occupation.Cavalry, Occupation.Healer);

    private static bool MatchAny(Occupation[] arr, Occupation o)
    {
        for (int i = 0; i < arr.Length; i++) if (arr[i] == o) return true;
        return false;
    }

    /// <summary>单位是否合格入册（我方 + 存活 + 人口职业）。</summary>
    public static bool IsPopulationEntity(UnitController unit)
    {
        if (unit == null || unit.Data == null) return false;
        if (unit.GetFaction() != Faction.PlayerCamp) return false;
        if (!unit.IsAlive) return false;
        // 2_17 步骤3 双条件过滤（守门员）：kingdomId>0 为 AI 王国工人（含动态立国实体），不得计入玩家人口台账。
        // 判两条件铁律：收编后 GetFaction=AiKingdom 的首条件已把新建 AI 排除，此 kingdomId 双条件保留兼容存量过渡态。
        if (unit.kingdomId > 0) return false;
        return IsPopulationOccupation(unit.EffectiveOccupation);
    }

    /// <summary>是否已注册。</summary>
    public bool IsRegistered(UnitController unit)
    {
        return unit != null && _entities.Contains(unit);
    }

    /// <summary>注册实体入册（幂等；不合格实体拒绝入册）。返回是否入册成功。</summary>
    public bool RegisterEntity(UnitController unit)
    {
        if (!IsPopulationEntity(unit)) return false;
        if (_entities.Contains(unit)) return false;
        _entities.Add(unit);
        Debug.Log($"[PopulationSystem] 实体入册：{unit.EffectiveOccupation} @ {unit.transform.position}，人口 → {_entities.Count}");
        return true;
    }

    /// <summary>实体出册（死亡/离开王国领域）。</summary>
    public void UnregisterEntity(UnitController unit)
    {
        if (unit == null) return;
        if (_entities.Remove(unit))
            Debug.Log($"[PopulationSystem] 实体出册：{unit.EffectiveOccupation}，人口 → {_entities.Count}");
    }

    // ===== 事件驱动增删 =====

    private void OnUnitSpawned(UnitSpawnedEvent evt)
    {
        RegisterEntity(evt.Unit);
    }

    private void OnUnitDied(UnitDiedEvent evt)
    {
        var uc = evt.Unit as UnitController;
        if (uc != null) UnregisterEntity(uc);
    }

    // ===== 开局实体生成（3.5.1 §3.3，E-S3）=====

    /// <summary>
    /// 新建游戏生成开局人口实体（2_12 步骤8.4 / HH.17 决策3：君主实体退役；
    /// 此处生成 4 工人 + 5 居民于城堡两侧，人口目标=9）。实体经 UnitSpawnedEvent 自动入册。
    /// </summary>
    public void SpawnInitialEntities()
    {
        var cfg = LifeConfig();
        if (cfg == null || UnitFactory.Instance == null || WorldManager.Instance == null)
        {
            Debug.LogError("[PopulationSystem] SpawnInitialEntities 前置缺失（config/UnitFactory/WorldManager），跳过开局实体生成！");
            return;
        }

        Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        if (anchor == Vector2.zero)
        {
            Debug.LogError("[PopulationSystem] 王国锚点不可用（地图未就绪），开局实体生成跳过！");
            return;
        }

        float cellSize = GridSystem.Instance != null && GridSystem.Instance.Config != null
            ? GridSystem.Instance.Config.cellSize.x : 2.26f;
        float gap = Mathf.Max(0.5f, cfg.initialSpawnGapCells) * cellSize;

        int idx = 0;
        int ok = 0;
        for (int i = 0; i < cfg.initialWorkerCount; i++)
            if (SpawnAtAnchorSide(Faction.PlayerCamp, Occupation.Worker, idx++, anchor, gap)) ok++;
        for (int i = 0; i < cfg.initialResidentCount; i++)
            if (SpawnAtAnchorSide(Faction.PlayerCamp, Occupation.Resident, idx++, anchor, gap)) ok++;

        BirthCooldownDays = cfg.birthCooldownDefault;
        Debug.Log($"[PopulationSystem] 开局实体生成完成：{ok}/{cfg.initialWorkerCount + cfg.initialResidentCount} " +
                  $"（God-view 无君主；目标人口 {cfg.initialPopulation}；当前注册 {PopulationCount}）");
    }

    /// <summary>城堡两侧交替落位生成单个实体（idx 偶左奇右，逐圈外扩）。</summary>
    private bool SpawnAtAnchorSide(Faction faction, Occupation occ, int idx, Vector2 anchor, float gap)
    {
        float side = (idx % 2 == 0) ? -1f : 1f;
        int rank = idx / 2 + 1;
        Vector2 pos = new Vector2(anchor.x + side * rank * gap, anchor.y);
        GameObject go = UnitFactory.Instance.SpawnUnit(faction, occ, pos);
        if (go == null)
        {
            Debug.LogError($"[PopulationSystem] 开局实体生成失败：{faction}_{occ}（缺资产或 Prefab）");
            return false;
        }
        return true;
    }

    /// <summary>每日结算（DayCycleSettlement 统一入口调用）。
    /// 3.5.1 §4.2 繁殖实体化（E-S5）：三层硬前置 + 随机配对 + 生成 Child 实体。
    ///   ① 全局条件：整体幸福 &gt; 60 且 平均饱食 &gt; 50
    ///   ② 房屋条件：王国房屋剩余容量 &gt; 0（房屋满 = 禁止生育，硬前置）
    ///   ③ 个体条件：从冷却期外的成年居民池随机抽 2 人配对（lastBirthDay + birthPairCooldownDays &lt;= 当前天）
    /// 配对成功 → 两人 lastBirthDay 同步当天 → 进房表演（占位：日志）→ 房屋旁生成 1 个 Child 实体。
    /// 全局节奏：BirthCooldownDays 倒计时间隔一次生育（防多对同日连生）。
    /// </summary>
    public void OnNewDay()
    {
        var cfg = LifeConfig();
        if (cfg == null) return;

        // 接真实值——整体幸福读 HappinessSystem，平均饱食读 SatietySystem
        if (HappinessSystem.Instance != null)
            AvgHappiness = HappinessSystem.Instance.OverallHappiness;
        if (SatietySystem.Instance != null)
            AvgSatiety = SatietySystem.Instance.GetAverageSatiety();

        // E-S6：小孩成长天数事件（先于生育结算，当日新生小孩不计当日）
        TickChildGrowth(cfg);

        int pairCooldown = cfg.birthPairCooldownDays > 0 ? cfg.birthPairCooldownDays : cfg.birthIntervalDays;

        BirthCooldownDays--;
        if (BirthCooldownDays > 0) return;

        // ② 房屋硬前置：王国房屋剩余容量 > 0（房屋满 = 禁止生育）
        bool hasHouse = false;
        if (HappinessSystem.Instance != null)
        {
            int houseCapacity = HappinessSystem.Instance.GetTotalHouseCapacity();
            hasHouse = houseCapacity > PopulationCount;   // 剩余容量 > 0
        }

        // ① 全局条件：幸福>60 且 平均饱食>50（真实值）
        bool happy = AvgHappiness > cfg.birthHappinessThreshold;
        bool fed = AvgSatiety > cfg.birthSatietyThreshold;
        if (!happy || !fed || !hasHouse)
        {
            // 条件不满足则重置冷却，待下轮再评估
            BirthCooldownDays = pairCooldown;
            return;
        }

        // ③ 随机配对：冷却期外的成年居民池抽 2 人
        int currentDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
        var candidates = new List<UnitController>();
        for (int i = 0; i < _entities.Count; i++)
        {
            var u = _entities[i];
            if (u == null || !u.IsAlive) continue;
            if (u.EffectiveOccupation != Occupation.Resident) continue;   // 配对池 = 成年居民
            if (u.LastBirthDay + pairCooldown > currentDay) continue;    // 个体冷却中
            candidates.Add(u);
        }

        // 幸福惩罚：增长因子 < 100% 时按概率折算（幸福低 → 生育概率降低）
        int growthFactor = HappinessSystem.Instance != null
            ? Mathf.RoundToInt(HappinessSystem.Instance.GetPopulationGrowthFactor() * 100f)
            : 100;

        bool tryBirth = candidates.Count >= 2 && growthFactor >= 1
            && (growthFactor >= 100 || Random.value * 100f < growthFactor);

        if (tryBirth)
        {
            // 随机抽 2 人（不放回）
            int a = Random.Range(0, candidates.Count);
            int b = Random.Range(1, candidates.Count);
            if (b == a) b = 0;
            var parentA = candidates[a];
            var parentB = candidates[b];
            parentA.LastBirthDay = currentDay;
            parentB.LastBirthDay = currentDay;

            // 进房表演（占位：日志 + 房屋旁生成）→ 出来两人 + 一小孩
            Vector2 birthPos = GetBirthPosition();
            GameObject childGo = UnitFactory.Instance != null
                ? UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Child, birthPos)
                : null;
            if (childGo != null)
                Debug.Log($"[PopulationSystem] 繁殖：两居民进房表演 → +1 小孩 @ {birthPos}（幸福因子{growthFactor}%；人口 → {PopulationCount}）");
            else
                Debug.LogError("[PopulationSystem] 繁殖失败：Child 单位生成失败（缺 Human_Player_Child 资产/Prefab？）");
        }
        BirthCooldownDays = pairCooldown;
    }

    /// <summary>生育落点：第一栋激活房屋旁（进房表演出口）；无房屋回退王国锚点。</summary>
    private Vector2 GetBirthPosition()
    {
        if (BuildingRegistry.Instance != null)
        {
            var all = BuildingRegistry.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null || b.def == null || !b.IsActive || b.def.id != "House") continue;
                return new Vector2(b.transform.position.x + 1f, b.transform.position.y);
            }
        }
        return WorldManager.Instance != null ? WorldManager.Instance.GetKingdomAnchorWorld() : Vector2.zero;
    }

    /// <summary>
    /// 小孩长大（3.5.1 §4.2/决策16，E-S6）：天数事件（每日结算）累积 childGrowthDayEvents 次（SO，默认 2）
    /// → SetOccupation(Resident) 长成居民（占位可调，非精确时刻）。
    /// </summary>
    private void TickChildGrowth(KingdomConfig cfg)
    {
        int need = Mathf.Max(1, cfg.childGrowthDayEvents);
        for (int i = 0; i < _entities.Count; i++)
        {
            var u = _entities[i];
            if (u == null || !u.IsAlive) continue;
            if (u.EffectiveOccupation != Occupation.Child) continue;
            u.ChildGrowthDays++;
            if (u.ChildGrowthDays >= need)
            {
                u.SetOccupation(Occupation.Resident);
                u.ChildGrowthDays = 0;
                Debug.Log($"[PopulationSystem] 小孩长大：天数事件累积 {need} 次 → 居民（{u.name}）");
            }
        }
    }

    // ===== ISaveable, Global =====
    // 实体本体由各自 UnitController（UnitSaveData，Scene 阶段）持久化；
    // 读档时 UnitFactory.SpawnFromSave → UnitSpawnedEvent → 注册表自动重建。

    public SavePayload SaveState()
    {
        var data = new PopulationSaveData
        {
            saveDataVersion = 2,
            populationCount = PopulationCount,   // 诊断快照（实体制下为派生值）
            birthCooldownDays = BirthCooldownDays,
            avgSatiety = AvgSatiety,
            avgHappiness = AvgHappiness
        };
        return new SavePayload
        {
            typeName = typeof(PopulationSaveData).AssemblyQualifiedName,
            json = JsonUtility.ToJson(data),
            version = data.saveDataVersion
        };
    }

    public void LoadState(SavePayload payload)
    {
        if (payload.typeName != typeof(PopulationSaveData).AssemblyQualifiedName) return;
        var data = JsonUtility.FromJson<PopulationSaveData>(payload.json);
        // 实体制：不再恢复计数（PopulationCount 由注册表派生，读档单位 SpawnFromSave 自动入表）
        BirthCooldownDays = Mathf.Max(0, data.birthCooldownDays);
        AvgSatiety = data.avgSatiety;
        AvgHappiness = data.avgHappiness;
        Debug.Log($"[PopulationSystem] 读档恢复：生育冷却 {BirthCooldownDays} 天（人口实体随单位读档回表）");
    }

    /// <summary>返回主菜单时重置。</summary>
    public void ResetState()
    {
        _entities.Clear();
        BirthCooldownDays = LifeConfig() != null ? LifeConfig().birthCooldownDefault : 5;
        AvgSatiety = 50f;
        AvgHappiness = 50f;
    }
}
