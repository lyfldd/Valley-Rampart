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
    // HH.86/DZ-057 件1b：追加 2_20 M7 七职业（Berserker/WolfRider/Musqueteer/Bedrock/Ranger/Windwalker/DeerRider，
    // 枚举实值尾插 28~34 区；HeavyWarrior 保留=枚举位铁律；M9 三机器 Mortar/VineCatapult/Ram 不入=口径对齐 2_20 M7）。
    // 旧清单缺七职业→AI 战士计数虚低→MilitaryTarget 虚高→⑦连轴耗工人（HH.85 审计实锤）。
    public static int AliveWarriorCount(int kingdomId) =>
        CountAliveByKingdom(kingdomId, Occupation.Warrior, Occupation.Archer, Occupation.Mage,
            Occupation.General, Occupation.Crossbowman, Occupation.HeavyWarrior, Occupation.Bishop,
            Occupation.ShieldGuard, Occupation.Archmage, Occupation.Cavalry, Occupation.Healer,
            Occupation.Berserker, Occupation.WolfRider, Occupation.Musqueteer, Occupation.Bedrock,
            Occupation.Ranger, Occupation.Windwalker, Occupation.DeerRider);

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
        // D563② 汇总口径：入册明细静默，人口计数汇入开局/清场汇总行（与 UnitRegistry 口径统一）
        return true;
    }

    /// <summary>实体出册（死亡/离开王国领域）。</summary>
    public void UnregisterEntity(UnitController unit)
    {
        if (unit == null) return;
        if (_entities.Remove(unit))
        {
            // HH.111 补笔A（D579⑤ 裁）：出册明细静默（与 D563② 入册 L160 汇总口径对称——人口计数
            // 对账由 LogCountSnapshot/开局汇总行承载；六考长局每死一单位一条=刷屏面）
        }
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
        UnitRegistry.Instance?.LogCountSnapshot("开局");   // D563② 建局侧汇总行（全单位计数，替代逐条注册日志）
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
            if (u.EffectiveOccupation != Occupation.Resident
                && u.EffectiveOccupation != Occupation.Worker && u.EffectiveOccupation != Occupation.Porter) continue;   // HH.86/DZ-058 件3d：配对池对齐 AI 轨口径（Worker/Porter/Resident——玩家全工人结构下繁殖可发生）
            if (u.LastBirthDay + pairCooldown > currentDay) continue;    // 个体冷却中
            candidates.Add(u);
        }

        // 幸福惩罚：增长因子 < 100% 时按概率折算（幸福低 → 生育概率降低）
        int growthFactor = HappinessSystem.Instance != null
            ? Mathf.RoundToInt(HappinessSystem.Instance.GetPopulationGrowthFactor() * 100f)
            : 100;

        // HH.86/DZ-060 件1f：玩家配对段确定性种子化（旧=Random.value/Random.Range 全局态随机）——
        // 对齐 AI 轨 R4 纪律（OnNewDayPerKingdom L398-407 同族公式）：种子源=map.seed（AI 轨同源，报备），
        // 玩家国 k.id=0 故种子=seed^(day*7919)（AI 公式去 k.id 项特例）；同 seed 同 day 恒复现。
        var wmPlayer = WorldManager.Instance;
        var mapPlayer = wmPlayer != null ? wmPlayer.ActiveMap : null;
        int seedPlayer = mapPlayer != null && mapPlayer.seed != 0 ? mapPlayer.seed : (wmPlayer != null ? wmPlayer.MapSeed : 1);
        int dayPlayer = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
        var pairRng = new System.Random(seedPlayer ^ (dayPlayer * 7919));

        bool tryBirth = candidates.Count >= 2 && growthFactor >= 1
            && (growthFactor >= 100 || pairRng.Next(100) < growthFactor);

        if (tryBirth)
        {
            // 随机抽 2 人（不放回；旧 Unity Random.Range(0,N)/Range(1,N) 语义=System.Random 等价改写）
            int a = pairRng.Next(candidates.Count);
            int b = 1 + pairRng.Next(candidates.Count - 1);
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
            {
                // D467 子女=国族（HH.51 种族1 批A）：Child 生成即抄写所属国族（玩家国=0；helper 挂账 Q10-M2 回填后自动多族）
                var childUc = childGo.GetComponent<UnitController>();
                if (childUc != null) childUc.raceId = KingdomRace.GetKingdomRace(0);
                Debug.Log($"[PopulationSystem] 繁殖：两居民进房表演 → +1 小孩 @ {birthPos}（幸福因子{growthFactor}%；人口 → {PopulationCount}）");
            }
            else
                Debug.LogError("[PopulationSystem] 繁殖失败：Child 单位生成失败（缺 Human_Player_Child 资产/Prefab？）");
        }
        BirthCooldownDays = pairCooldown;
    }

    // ===== HH.78/D540 AI 生育分支（混合双通道 C：AI 轨 per-kingdom，玩家轨 OnNewDay 逐位不动）=====

    /// <summary>AI 王国生育冷却（per-kingdom 独立倒计时；跨轮 ResetState 清空）。</summary>
    private readonly Dictionary<int, int> _aiBirthCooldowns = new Dictionary<int, int>();

    /// <summary>
    /// AI 王国生育分支（HH.78/D540；DayCycleSettlement 人口段对 AI 国逐国调用）。
    /// 条件输入 per-kingdom：幸福=HappinessSystem.GetKingdomHappiness(k.id)/饱食=SatietySystem.GetAverageSatiety(k.id)/
    /// 房屋容量=GetHouseCapacityByKingdom(k.id)；阈值复用玩家同表（birthHappinessThreshold=60/birthSatietyThreshold=50，D540 裁决）。
    /// 配对池=UnitRegistry 按 kingdomId 过滤（Worker/Porter/Resident 均可配对，AI 纯 Worker 结构）——
    /// **先收集快照再遍历，Spawn 在遍历体外**（HH.76 件2 雷区纪律：GetAllUnits 返回内部 List 引用）；
    /// 确定性=npcId 固定序+种子 rng（seed^day^k.id，R4 纪律对齐 VagrantCampSystem.NewDayRng）；
    /// 生成 SpawnUnit(Faction.PlayerCamp, Child, pos, k.id)+raceId=GetKingdomRace(k.id)（Foundry/Siege 现网组合先例，
    /// D467 挂账 per-kingdom 回填）；Child 日常耗粮走 Satiety per-kingdom 国库路由（D453 已通零新增）。
    /// </summary>
    public void OnNewDayPerKingdom(KingdomState k)
    {
        var cfg = LifeConfig();
        if (cfg == null || k == null || k.IsPlayer) return;
        var sat = SatietySystem.Instance;
        var hap = HappinessSystem.Instance;
        if (sat == null || hap == null) return;

        // per-kingdom 冷却倒计时（首见=满冷却）
        int cd;
        if (!_aiBirthCooldowns.TryGetValue(k.id, out cd)) cd = cfg.aiBirthIntervalDays;
        cd--;
        if (cd > 0) { _aiBirthCooldowns[k.id] = cd; return; }

        // 条件输入 per-kingdom（阈值同玩家表）
        float happiness = hap.GetKingdomHappiness(k.id);
        float satiety = sat.GetAverageSatiety(k.id);
        int houseCapacity = hap.GetHouseCapacityByKingdom(k.id);
        int population = k.workerCount + k.warriorCount;
        bool happy = happiness > cfg.birthHappinessThreshold;
        bool fed = satiety > cfg.birthSatietyThreshold;
        bool hasHouse = houseCapacity > population;
        if (!happy || !fed || !hasHouse)
        {
            _aiBirthCooldowns[k.id] = cfg.aiBirthIntervalDays;   // 条件不满足重置冷却（对齐玩家 L273 语义）
            Debug.Log($"[PopulationSystem] AI生育条件未满足 k{k.id}：幸福{happiness:F0}({happy}) 饱食{satiety:F0}({fed}) 房容{houseCapacity} vs 人口{population}({hasHouse}) 工人池={CountEligible(k.id)}");
            return;
        }

        // 配对池：按 kingdomId 过滤（Worker/Porter/Resident）——先收集快照，遍历体内零 Spawn（雷区纪律）
        var candidates = new List<UnitController>();
        var regUnits = UnitRegistry.Instance != null ? UnitRegistry.Instance.GetAllUnits() : null;
        if (regUnits == null) { _aiBirthCooldowns[k.id] = cfg.aiBirthIntervalDays; return; }
        foreach (var u in regUnits)
        {
            if (u == null || !u.IsAlive || u.kingdomId != k.id) continue;
            var occ = u.EffectiveOccupation;
            if (occ != Occupation.Worker && occ != Occupation.Porter && occ != Occupation.Resident) continue;
            candidates.Add(u);
        }
        if (candidates.Count < 2) { _aiBirthCooldowns[k.id] = cfg.aiBirthIntervalDays; return; }
        // 占位引用防未使用告警（候选池规模诊断随条件日志输出）
        int CountEligible(int kid)
        {
            if (UnitRegistry.Instance == null) return 0;
            int n = 0;
            var us = UnitRegistry.Instance.GetAllUnits();
            foreach (var u in us)
            {
                if (u == null || !u.IsAlive || u.kingdomId != kid) continue;
                var o = u.EffectiveOccupation;
                if (o == Occupation.Worker || o == Occupation.Porter || o == Occupation.Resident) n++;
            }
            return n;
        }

        // 确定性配对：npcId 固定序 + 种子 rng（R4：同 (seed, day, k.id) 恒复现）
        candidates.Sort((a, b) => a.npcId.CompareTo(b.npcId));
        var wm = WorldManager.Instance;
        var map = wm != null ? wm.ActiveMap : null;
        int seed = map != null && map.seed != 0 ? map.seed : (wm != null ? wm.MapSeed : 1);
        int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 1;
        var rng = new System.Random(seed ^ (day * 7919) ^ (k.id * 104729));
        int ia = rng.Next(candidates.Count);
        int ib = rng.Next(candidates.Count);
        if (ib == ia) ib = (ia + 1) % candidates.Count;
        var parentA = candidates[ia];
        var parentB = candidates[ib];

        // 出生落点=该国 House 旁（房条件已过=有房在）
        Vector2 birthPos = GetKingdomBirthPosition(k.id);

        // Spawn 在配对池遍历之外（雷区纪律）；归属国 kingdomId+国族 raceId 双写
        GameObject childGo = UnitFactory.Instance != null
            ? UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Child, birthPos, k.id)
            : null;
        if (childGo != null)
        {
            var childUc = childGo.GetComponent<UnitController>();
            if (childUc != null) childUc.raceId = KingdomRace.GetKingdomRace(k.id);
            int childRace = childUc != null ? childUc.raceId : -1;
            Debug.Log($"[PopulationSystem] AI生育：k{k.id} 两口进房 → +1 小孩 @ {birthPos}（幸福{happiness:F0}/饱食{satiety:F0}，raceId={childRace}）");
        }
        else
            Debug.LogError($"[PopulationSystem] AI生育失败：k{k.id} Child 单位生成失败（缺 Child 资产/Prefab？）");
        _aiBirthCooldowns[k.id] = cfg.aiBirthIntervalDays;
    }

    /// <summary>AI 王国出生落点：该国任一活动 House 旁（无房兜底=全局锚点；此路径极少走——无房=条件已挡）。</summary>
    private Vector2 GetKingdomBirthPosition(int kingdomId)
    {
        if (BuildingRegistry.Instance != null)
        {
            var all = BuildingRegistry.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null || b.def == null || !b.IsActive || b.def.id != "House") continue;
                if (b.kingdomId != kingdomId) continue;
                return SpawnPosSnapper.SnapWorld(new Vector2(b.transform.position.x + 1f, b.transform.position.y), "AI繁殖Child");
            }
        }
        return WorldManager.Instance != null
            ? SpawnPosSnapper.SnapWorld(WorldManager.Instance.GetKingdomAnchorWorld(), "AI繁殖Child兜底")
            : Vector2.zero;
    }

    /// <summary>生育落点：第一栋激活房屋旁（进房表演出口）；无房屋回退王国锚点。落点不可走→就近吸附（寻路2/HH.48）。</summary>
    private Vector2 GetBirthPosition()
    {
        if (BuildingRegistry.Instance != null)
        {
            var all = BuildingRegistry.Instance.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b == null || b.def == null || !b.IsActive || b.def.id != "House") continue;
                if (b.kingdomId != 0) continue;   // HH.86/DZ-059 件1e：只落玩家本国 House（对齐 AI 轨 GetKingdomBirthPosition 的 b.kingdomId != kingdomId 过滤）——旧无过滤时全图首栋 House 可能挂 AI 国
                return SpawnPosSnapper.SnapWorld(new Vector2(b.transform.position.x + 1f, b.transform.position.y), "繁殖Child");
            }
        }
        return WorldManager.Instance != null
            ? SpawnPosSnapper.SnapWorld(WorldManager.Instance.GetKingdomAnchorWorld(), "繁殖Child兜底")
            : Vector2.zero;
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
            // HH.78/D540：玩家段只处理玩家 Child（kingdomId=0）——_entities 注册链存在「AI 实体以 kingdomId=0
            // 时序态入册」的既有面（UnitSpawnedEvent 发布早于 kingdomId 写入，HH.79 列报），此守卫防 AI Child
            // 被玩家段转 Resident（实测 D23「小孩长大→居民 Human_Player_Child」），AI Child 归下方 AI 段 Worker 直生。
            if (u.kingdomId > 0) continue;
            u.ChildGrowthDays++;
            if (u.ChildGrowthDays >= need)
            {
                u.SetOccupation(Occupation.Resident);
                u.ChildGrowthDays = 0;
                Debug.Log($"[PopulationSystem] 小孩长大：天数事件累积 {need} 次 → 居民（{u.name}）");
            }
        }

        // ===== HH.78/D540 AI Child 成长段（per-kingdom，件2）：AI Child 成长满 → Worker 直生 =====
        // AI 无 Resident 体系（纯 Worker 结构）→ 直生=绕 ⑥ 不占流浪供给（Gate 面③裁决）；
        // 先收集快照再遍历（GetAllUnits 返回内部 List 引用，雷区纪律——快照防御 SetOccupation 换职业无增删惯例）；
        // 成长耗粮走 Satiety per-kingdom 路由（D453 零新增）。
        if (UnitRegistry.Instance != null && KingdomRegistry.Instance != null)
        {
            var aiChildren = new List<UnitController>();
            var units = UnitRegistry.Instance.GetAllUnits();
            foreach (var u in units)
            {
                if (u == null || !u.IsAlive || u.kingdomId <= 0) continue;   // AI 专属（玩家 Child 走上方 _entities 段）
                if (u.EffectiveOccupation != Occupation.Child) continue;
                aiChildren.Add(u);
            }
            for (int i = 0; i < aiChildren.Count; i++)
            {
                var u = aiChildren[i];
                u.ChildGrowthDays++;
                if (u.ChildGrowthDays >= need)
                {
                    u.SetOccupation(Occupation.Worker);   // AI 直生 Worker（非 Resident）
                    u.ChildGrowthDays = 0;
                    Debug.Log($"[PopulationSystem] AI小孩长大：k{u.kingdomId} Child 天数事件 {need} 次 → Worker 直生（{u.name}）");
                }
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
        _aiBirthCooldowns.Clear();   // HH.78/D540：AI 生育冷却 per-kingdom 清场（跨轮零残留）
        BirthCooldownDays = LifeConfig() != null ? LifeConfig().birthCooldownDefault : 5;
        AvgSatiety = 50f;
        AvgHappiness = 50f;
    }
}
