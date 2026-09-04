using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 训练系统（3.5 实施计划 P0 步骤4，数据结构先行）。
/// 职责：查询训练定义 + 执行转职（无职业 → 工人/搬运工）。
///
/// P0 占位原则：转职 = 改 occupation（数据层），NPC 站桩/训练表演后置到 AI 稳定后
/// （走 IWorkerTaskExecutor 接口，本系统不实现 NPC 行为）。
/// 职业变更写入 UnitController.RuntimeOccupation（不污染共享 UnitData SO）并随 UnitSaveData 持久化。
/// </summary>
public class TrainingSystem : Singleton<TrainingSystem>
{
    private TrainingConfig _config;
    private readonly Dictionary<string, List<TrainingDef>> _byBuilding = new Dictionary<string, List<TrainingDef>>();

    // ===== 3.5 P1-10 训练队列（3.5.1 §4.3 / §12.2）=====
    // 每训练建筑一队列：排队中(inTraining=false) + 训练中(inTraining=true)。
    // 槽位 = BuildingDef.trainingSlots（Lv1=1/Lv2=2/Lv3=3，其他回退 1）。
    // 训练中建筑被摧毁 → 不退款、居民存活回退无职业居民（OnBuildingDestroyed）。
    private readonly Dictionary<Building, TrainingQueue> _queues = new Dictionary<Building, TrainingQueue>();

    // 2_20 M6 战争学院「溃败补充」：战斗单位阵亡 30s 内该兵种补训成本-50%（一次性窗口，2_20.1 §三）。
    // 记录职业 → 最近阵亡世界时间；TryTrain 命中后消费即清除（一次性）。
    private readonly Dictionary<Occupation, float> _recentDeaths = new Dictionary<Occupation, float>();

    /// <summary>单训练建筑队列（排队 + 训练中）。</summary>
    private class TrainingQueue
    {
        public readonly List<TrainingQueueEntry> Entries = new List<TrainingQueueEntry>();
        public int ActiveCount;   // 训练中条目数（占用槽位）
    }

    /// <summary>单条训练请求（排队中或训练中）。</summary>
    private class TrainingQueueEntry
    {
        public UnitController unit;
        public TrainingDef def;
        public int startDay;      // 开始训练的游戏天数（仅训练中条目有效）
        public bool inTraining;   // true=占用槽位训练中；false=排队等待空槽
        public int kingdomId;     // 2_17 步骤5：所属王国（0=玩家，>0=AI）——队列 per-kingdom 记账（D345 招募入口）
        public int effCostDays;   // 2_20 M5/D420：种族训练时长修正后的实际天数（trainSpeedMul；入队时定档为副本，不改 SO——排队期国族不变）
    }

    private void Update()
    {
        // 每日推进训练完成（天数驱动，costDays 天完成转职）
        if (TimeManager.Instance == null) return;
        int day = TimeManager.Instance.CurrentDay;
        if (_queues.Count == 0) return;

        foreach (var kv in _queues)
        {
            var q = kv.Value;
            if (q == null || q.Entries.Count == 0) continue;
            // 从后往前完成训练中条目，避免改列表
            for (int i = q.Entries.Count - 1; i >= 0; i--)
            {
                var e = q.Entries[i];
                if (!e.inTraining || e.unit == null) continue;
                if (day - e.startDay >= e.effCostDays)
                {
                    CompleteTraining(q, e);
                }
            }
            // 排队条目晋升空槽（有槽位则把队首排队条目转训练中）
            if (q.ActiveCount < SlotCount(kv.Key)) TryPromote(q, day);
        }
    }

    /// <summary>把队首排队条目晋升为训练中（若有空槽）。</summary>
    private void TryPromote(TrainingQueue q, int day)
    {
        if (q == null) return;
        for (int i = 0; i < q.Entries.Count; i++)
        {
            var e = q.Entries[i];
            if (e.inTraining) continue;
            e.inTraining = true;
            e.startDay = day;
            q.ActiveCount++;
            return;
        }
    }

    /// <summary>训练完成：改职业 + 出队 + 车牌空槽（由后续 Update 晋升排队条目）。</summary>
    private void CompleteTraining(TrainingQueue q, TrainingQueueEntry e)
    {
        if (e.unit != null && e.unit.Data != null)
        {
            e.unit.SetOccupation(e.def.toOccupation);
            Debug.Log($"[TrainingSystem] 训练完成：{e.def.fromOccupation} → {e.def.toOccupation}（{e.def.buildingId}，耗金{e.def.costGold} 水晶{e.def.costCrystal}）");
        }
        q.ActiveCount = Mathf.Max(0, q.ActiveCount - 1);
        q.Entries.Remove(e);
    }

    /// <summary>训练建筑槽位数（BuildingDef.trainingSlots；≤0 回退 1）。</summary>
    private static int SlotCount(Building building)
    {
        if (building != null && building.def != null && building.def.trainingSlots > 0)
            return building.def.trainingSlots;
        return 1;
    }

    /// <summary>
    /// 训练中断回退（3.5.1 §4.3 / 3.5.4 §8.6；P1-10）。训练建筑被摧毁时由 Building.Die 通知。
    /// 该建筑所有训练中 + 排队居民：已投入资源不退、中断训练、occupation 回退无职业居民（Resident）、不死亡。
    /// </summary>
    public void OnBuildingDestroyed(Building building)
    {
        if (building == null || !_queues.TryGetValue(building, out var q)) return;
        for (int i = 0; i < q.Entries.Count; i++)
        {
            var e = q.Entries[i];
            if (e == null || e.unit == null) continue;
            e.unit.SetOccupation(Occupation.Resident);   // 回退无职业居民（3.5.1 E-S1：Unemployed 已改名 Resident）
            Debug.Log($"[TrainingSystem] 训练中断回退：{e.def.fromOccupation} 目标 {e.def.toOccupation} → 居民（建筑被毁，资源不退，存活）");
        }
        _queues.Remove(building);
    }

    protected override void Awake()
    {
        base.Awake();
        if (_instance != this) return;
        _config = Resources.Load<TrainingConfig>("Config/TrainingConfig");
        BuildLookup();
        // 2_20 M6 战争学院「溃败补充」：订阅阵亡事件记录战斗职业最近阵亡时间（TryTrain 消费一次性窗口）
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    protected override void OnDestroy()
    {
        if (_instance != this) return;
        base.OnDestroy();
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    /// <summary>战争学院「溃败补充」记录：战斗单位被击杀（Cause==Killed）→ 该职业 30s 补训窗口开启。</summary>
    private void OnUnitDied(UnitDiedEvent evt)
    {
        if (evt.Cause != DeathCause.Killed) return;
        if (!(evt.Unit is UnitController uc) || uc == null) return;
        var occ = uc.EffectiveOccupation;
        if (IsCombatOccupation(occ)) _recentDeaths[occ] = Time.time;
    }

    /// <summary>是否战斗职业（溃败补充只认战斗单位；生活/建筑类不触发）。</summary>
    private static bool IsCombatOccupation(Occupation occ)
    {
        switch (occ)
        {
            case Occupation.General:
            case Occupation.Archer:
            case Occupation.Warrior:
            case Occupation.Mage:
            case Occupation.Healer:
            case Occupation.Crossbowman:
            case Occupation.ShieldGuard:
            case Occupation.Archmage:
            case Occupation.Cavalry:
            case Occupation.SiegeMachine:
            case Occupation.Ballista:
            // 2_20 M7 专属兵种+机器（D490~D497：全为战斗单位，阵亡触发溃败补充窗口）
            case Occupation.Berserker:
            case Occupation.WolfRider:
            case Occupation.Musqueteer:
            case Occupation.Bedrock:
            case Occupation.Ranger:
            case Occupation.Windwalker:
            case Occupation.DeerRider:
            case Occupation.Mortar:
            case Occupation.VineCatapult:
            case Occupation.Ram:
                return true;
            default:
                return false;
        }
    }

    private void BuildLookup()
    {
        _byBuilding.Clear();
        if (_config == null || _config.trainings == null) return;
        foreach (var t in _config.trainings)
        {
            if (string.IsNullOrEmpty(t.buildingId)) continue;
            if (!_byBuilding.TryGetValue(t.buildingId, out var list))
            {
                list = new List<TrainingDef>();
                _byBuilding[t.buildingId] = list;
            }
            list.Add(t);
        }
    }

    /// <summary>某训练设施可提供的全部训练项（空 = 无配置）。</summary>
    public IReadOnlyList<TrainingDef> GetTrainings(string buildingId)
    {
        if (buildingId == null || _byBuilding.Count == 0) BuildLookup();
        return _byBuilding.TryGetValue(buildingId ?? "", out var list) ? list : s_empty;
    }
    private static readonly List<TrainingDef> s_empty = new List<TrainingDef>();

    /// <summary>
    /// 某训练建筑可提供的训练项（2_20 M7：按建筑实例过滤专属兵门禁）。
    /// 过滤 = 建筑国族 raceId 匹配（跨族不可训练 D419）+ 建筑等级 ≥ minBuildingLevel（练兵场 Lv2 门槛）。
    /// </summary>
    public IReadOnlyList<TrainingDef> GetTrainings(Building building)
    {
        if (building == null || building.def == null) return s_empty;
        var all = GetTrainings(building.def.id);
        if (all == null || all.Count == 0) return all;
        int race = KingdomRace.GetKingdomRace(building.kingdomId);
        List<TrainingDef> list = null;
        for (int i = 0; i < all.Count; i++)
        {
            var t = all[i];
            if (t.raceId >= 0 && t.raceId != race) continue;
            if (t.minBuildingLevel > 0 && building.level < t.minBuildingLevel) continue;
            if (list == null) list = new List<TrainingDef>();
            list.Add(t);
        }
        return list ?? (IReadOnlyList<TrainingDef>)s_empty;
    }

    /// <summary>
    /// 训练请求（P1-10 队列管理）。校验起职 + 资源 → 扣费 → 入队列（有空槽立即开始，否则排队）。
    /// 训练期间居民保持原职业，costDays 天完成才转职（Update 推进）。
    /// 若训练建筑被摧毁，已投入资源不退、居民回退无职业（OnBuildingDestroyed）。
    /// </summary>
    public bool TryTrain(UnitController unit, TrainingDef def)
    {
        return TryTrain(unit, def, FindTrainingBuilding(def.buildingId));
    }

    /// <summary>
    /// 带训练建筑实例的入队版本（TrainingPanel 传其所属设施，用于槽位管理）。
    /// 2_17 步骤5：加 kingdomId（默认 0）——训练队列 per-kingdom，国库扣费/队列记账按王国分桶；
    /// AI 从自身 KingdomState 国库扣（五经济资源，魔法耗水晶 AI 无库存→不可行），玩家仍走 RulerController。
    /// </summary>
    public bool TryTrain(UnitController unit, TrainingDef def, Building building, int kingdomId = 0)
    {
        if (unit == null || unit.Data == null || building == null) return false;
        // 梯队队列按建筑实例挂桶；建筑领地决定王国归属，招募侧 kingdomId 与之一致（防跨国训练）
        int effKingdom = building.kingdomId;
        if (kingdomId != 0 && kingdomId != effKingdom)
        {
            Debug.Log($"[TrainingSystem] 转职失败：招募国 {kingdomId} ≠ 建筑国 {effKingdom}，跨国训练禁止");
            return false;
        }
        if (unit.kingdomId != effKingdom)
        {
            Debug.Log($"[TrainingSystem] 转职失败：单位国 {unit.kingdomId} ≠ 建筑国 {effKingdom}，仅训练本国单位");
            return false;
        }
        Occupation cur = unit.EffectiveOccupation;
        if (cur != def.fromOccupation)
        {
            Debug.Log($"[TrainingSystem] 转职失败：{cur} ≠ 起始职业 {def.fromOccupation}");
            return false;
        }

        // 2_20 M7 专属兵训练门禁（D419 唯一入口 + D490 共通槽退役）：跨族不可训练 + 建筑等级门槛（练兵场 Lv2）
        if (def.raceId >= 0 && KingdomRace.GetKingdomRace(effKingdom) != def.raceId)
        {
            Debug.Log($"[TrainingSystem] 转职失败：{def.toOccupation} 为种族 {def.raceId} 专属训练，本国族不可训（D419 唯一入口）");
            return false;
        }
        if (def.minBuildingLevel > 0 && building.level < def.minBuildingLevel)
        {
            Debug.Log($"[TrainingSystem] 转职失败：{def.toOccupation} 需 {def.buildingId} Lv{def.minBuildingLevel}，当前 Lv{building.level}");
            return false;
        }

        // 2_20 M5/D420：种族军训成本/时长修正（D503 表 trainCostMul/trainSpeedMul）：
        // 成本=effective 值 ceil 取整（防零成本白嫖）；时长=entry 存 effCostDays 副本（排队期国族不变，不改 SO）。
        // 确定用建筑国族（effKingdom=招募归属，同族镜像语义）。
        // 2_20 M6 战争学院：训练时长全局-25% 在此叠乘（HH.59 疑点④挂账清偿）；溃败补充窗口成本-50%（一次性）。
        var raceDef = KingdomRace.GetKingdomRaceDef(effKingdom);
        float costMul = raceDef != null ? raceDef.trainCostMul : 1f;
        float speedMul = raceDef != null ? raceDef.trainSpeedMul : 1f;
        float academyMul = KingdomRace.HasExclusiveBuilding(effKingdom, "WarAcademy") ? 0.75f : 1f;
        float rallyMul = 1f;   // 战争学院「溃败补充」：本职业 30s 内阵亡 → 本次补训成本-50%（一次性窗口）
        if (_recentDeaths.TryGetValue(def.toOccupation, out float dt) && Time.time - dt <= 30f)
        {
            rallyMul = 0.5f;
            _recentDeaths.Remove(def.toOccupation);   // 一次性窗口已消费
        }
        int effGold = Mathf.CeilToInt(def.costGold * costMul * rallyMul);
        int effCrystal = Mathf.CeilToInt(def.costCrystal * costMul * rallyMul);
        int effMetal = Mathf.CeilToInt(def.costMetal * costMul * rallyMul);
        int effDays = Mathf.Max(1, Mathf.CeilToInt(def.costDays / Mathf.Max(0.01f, speedMul) * academyMul));

        if (!CanPayRecruit(effKingdom, effGold, effCrystal, effMetal)) return false;

        // P2：将军训练限量（KingdomConfig.generalLimit，§10 将军限量 2 可配置）
        if (def.toOccupation == Occupation.General && !CanTrainGeneral())
            return false;

        // 扣费（训练中断不退还，故入队即扣；按王国国库抽象扣）
        PayRecruit(effKingdom, effGold, effCrystal, effMetal);

        // 入队列（P1-10）：有空槽立即开始训练，否则排队
        if (!_queues.TryGetValue(building, out var q))
        {
            q = new TrainingQueue();
            _queues[building] = q;
        }
        var entry = new TrainingQueueEntry { unit = unit, def = def, inTraining = false, kingdomId = effKingdom, effCostDays = effDays };
        q.Entries.Add(entry);
        if (q.ActiveCount < SlotCount(building))
        {
            entry.inTraining = true;
            entry.startDay = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : 0;
            q.ActiveCount++;
            Debug.Log($"[TrainingSystem] 开始训练：{def.fromOccupation} → {def.toOccupation}（{def.buildingId}，耗金{def.costGold} 水晶{def.costCrystal}，{def.costDays}天，{q.ActiveCount}/{SlotCount(building)}槽）");
        }
        else
        {
            Debug.Log($"[TrainingSystem] 训练排队：{def.fromOccupation} → {def.toOccupation}（{def.buildingId}，空槽不足，排队 #{q.Entries.Count}）");
        }
        return true;
    }

    /// <summary>招募国库抽象校验（2_17 步骤5 per-kingdom）：玩家(0)走 RulerController；AI 走自身 KingdomState 五经济资源。
    /// 2_20 M5：金/水晶/铁传种族修正后 effective 值（trainCostMul ceil，TryTrain 计算）。</summary>
    private bool CanPayRecruit(int kingdomId, int gold, int crystal, int metal)
    {
        if (kingdomId <= 0)
        {
            if (RulerController.Instance == null) return false;
            if (RulerController.Instance.Gold < gold) { Debug.Log("[TrainingSystem] 转职失败：金币不足"); return false; }
            if (crystal > 0 && RulerController.Instance.GetResource(ResourceType.Crystal) < crystal) { Debug.Log("[TrainingSystem] 转职失败：水晶不足"); return false; }
            if (metal > 0 && RulerController.Instance.GetResource(ResourceType.Metal) < metal) { Debug.Log("[TrainingSystem] 转职失败：铁不足"); return false; }
            return true;
        }
        var ks = KingdomRegistry.Instance != null ? KingdomRegistry.Instance.Get(kingdomId) : null;
        if (ks == null) return false;
        if (ks.resources.gold < gold) return false;
        if (metal > 0 && ks.resources.metal < metal) return false;
        if (crystal > 0) { Debug.Log("[TrainingSystem] AI 国库仅五经济资源无水晶，魔法训练不可行"); return false; }
        return true;
    }

    /// <summary>招募扣费（2_17 步骤5 per-kingdom）：玩家走 RulerController；AI 走 KingdomState.Spend（台账制，无事件）。
    /// 2_20 M5：传种族修正后 effective 值（与 CanPayRecruit 同一 effective 三元组，防校验/扣费漂移）。</summary>
    private void PayRecruit(int kingdomId, int gold, int crystal, int metal)
    {
        if (kingdomId <= 0)
        {
            RulerController.Instance.ModifyResource(ResourceType.Gold, false, gold);
            if (crystal > 0) RulerController.Instance.ModifyResource(ResourceType.Crystal, false, crystal);
            if (metal > 0) RulerController.Instance.ModifyResource(ResourceType.Metal, false, metal);
        }
        else
        {
            var ks = KingdomRegistry.Instance.Get(kingdomId);
            ks.Spend(new ResourcePack { gold = gold, metal = metal });
        }
    }

    /// <summary>按国在训+排队的条目总数（2_17 步骤5 队列 per-kingdom；D345 招募入口队列视图）。</summary>
    public int GetKingdomQueueCount(int kingdomId)
    {
        int count = 0;
        foreach (var kv in _queues)
        {
            var b = kv.Key;
            if (b == null || b.kingdomId != kingdomId) continue;
            count += kv.Value != null ? kv.Value.Entries.Count : 0;
        }
        return count;
    }

    /// <summary>
    /// 从居民池自动取一个符合起始职业的居民入队（QQQ.2 §需求3 / DR-3：不列出具体 NPC）。
    /// 先按 fromOccupation 匹配可训项，再随机取一个空闲居民。
    /// </summary>
    public bool TryTrainFromPool(Building building, Occupation toOccupation)
    {
        if (building == null || building.def == null) return false;
        var trainings = GetTrainings(building.def.id);
        if (trainings == null || trainings.Count == 0) return false;

        // 找到该目标职业对应的训练定义（起始职业）
        TrainingDef def = default;
        bool found = false;
        for (int i = 0; i < trainings.Count; i++)
        {
            if (trainings[i].toOccupation == toOccupation)
            {
                def = trainings[i];
                found = true;
                break;
            }
        }
        if (!found) return false;

        // 收集全部符合起始职业的空闲居民
        var pool = new List<UnitController>();
        if (UnitRegistry.Instance != null)
        {
            foreach (var unit in UnitRegistry.Instance.GetAllUnits())
            {
                if (unit == null || unit.Data == null) continue;
                if (unit.GetFaction() != Faction.PlayerCamp) continue;
                if (unit.kingdomId != 0) continue;   // 2_17 步骤4 关账扫描：仅玩家桶0——AI 工人不入玩家转职池；收编后 GetFaction=AiKingdom 首条件已排除（双条件保留兼容存量过渡态）
                if (unit.EffectiveOccupation != def.fromOccupation) continue;
                if (!unit.IsAlive) continue;
                pool.Add(unit);
            }
        }
        if (pool.Count == 0) return false;

        // 确定性纪律（HH.27 实锤）：原 Random.Range(0,pool.Count) 未种子化，玩家同操作两次选人不同，
        // 属确定性地雷（已登记独立小卡）。改为 npcId 稳定最小选人——确定性且同操作逐字节一致。
        var chosen = pool[0];
        for (int i = 1; i < pool.Count; i++)
            if (pool[i].npcId < chosen.npcId) chosen = pool[i];
        return TryTrain(chosen, def, building);
    }

    /// <summary>按训练定义 buildingId 找活动训练建筑实例（旧无参调用兼容；无则返回 null=无槽位限制）。</summary>
    private Building FindTrainingBuilding(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId) || BuildingRegistry.Instance == null) return null;
        var all = BuildingRegistry.Instance.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b == null || b.def == null || !b.IsActive) continue;
            if (b.def.id == buildingId) return b;
        }
        return null;
    }

    // ===== QQQ.2 §需求3 / DR-3：训练 UI 数据查询（可训练人数 + 队列 + 正在训练）=====

    /// <summary>某训练建筑当前可训练人数（王国空闲居民数，且起始职业匹配该设施可训项）。</summary>
    public int GetTrainableCount(Building building)
    {
        var trainings = building != null && building.def != null ? GetTrainings(building.def.id) : null;
        if (trainings == null || trainings.Count == 0) return 0;
        // 收集该设施全部起始职业
        var fromSet = new HashSet<Occupation>();
        for (int i = 0; i < trainings.Count; i++) fromSet.Add(trainings[i].fromOccupation);
        int count = 0;
        if (UnitRegistry.Instance == null) return 0;
        foreach (var unit in UnitRegistry.Instance.GetAllUnits())
        {
            if (unit == null || unit.Data == null) continue;
            if (unit.GetFaction() != Faction.PlayerCamp) continue;
            if (unit.kingdomId != 0) continue;   // 2_17 步骤4 关账扫描：仅玩家桶0——AI 工人不计玩家可训练数（同上述收编双条件语义）
            if (!unit.IsAlive) continue;
            if (fromSet.Contains(unit.EffectiveOccupation)) count++;
        }
        return count;
    }

    /// <summary>某训练建筑当前训练队列清单（目标职业 × 数量，含排队 + 训练中）。</summary>
    public List<KeyValuePair<Occupation, int>> GetQueueSummary(Building building)
    {
        var result = new List<KeyValuePair<Occupation, int>>();
        if (building == null || !_queues.TryGetValue(building, out var q) || q.Entries.Count == 0) return result;
        var map = new Dictionary<Occupation, int>();
        for (int i = 0; i < q.Entries.Count; i++)
        {
            var e = q.Entries[i];
            if (e == null) continue;
            map[e.def.toOccupation] = map.TryGetValue(e.def.toOccupation, out var c) ? c + 1 : 1;
        }
        foreach (var kv in map) result.Add(kv);
        return result;
    }

    /// <summary>某训练建筑当前正在训练人数（占用槽位的训练中条目数）。</summary>
    public int GetActiveCount(Building building)
    {
        if (building == null || !_queues.TryGetValue(building, out var q)) return 0;
        return q.ActiveCount;
    }

    /// <summary>某训练建筑支持的职业白名单（TrainingConfig.supportedOccupations；空则回退 GetTrainings 去重目标职业）。</summary>
    public Occupation[] GetSupportedOccupations(Building building)
    {
        if (building != null && building.def != null && _config != null && _config.supportedOccupations != null
            && _config.supportedOccupations.Length > 0)
            return _config.supportedOccupations;
        // 回退：该设施可训项的目标职业（2_20 M7：按建筑实例过滤专属兵门禁）
        var trainings = building != null && building.def != null ? GetTrainings(building) : null;
        if (trainings == null || trainings.Count == 0) return new Occupation[0];
        var set = new List<Occupation>();
        var seen = new HashSet<Occupation>();
        for (int i = 0; i < trainings.Count; i++)
        {
            if (seen.Add(trainings[i].toOccupation)) set.Add(trainings[i].toOccupation);
        }
        return set.ToArray();
    }

    /// <summary>某目标职业的训练时长（TrainingConfig.trainDurationDays 按 supportedOccupations 对齐；0 则回退 DR-12 默认）。</summary>
    public float GetTrainDuration(Building building, Occupation to)
    {
        if (building != null && building.def != null && _config != null && _config.supportedOccupations != null
            && _config.trainDurationDays != null)
        {
            var occs = _config.supportedOccupations;
            for (int i = 0; i < occs.Length && i < _config.trainDurationDays.Length; i++)
            {
                if (occs[i] == to && _config.trainDurationDays[i] > 0f) return _config.trainDurationDays[i];
            }
        }
        // DR-12 默认：居民→工人 1 天 / →士兵 2 天 / →高阶 3 天
        switch (to)
        {
            case Occupation.Worker:
            case Occupation.Porter:
                return 1f;
            case Occupation.Warrior:
            case Occupation.Archer:
            case Occupation.Crossbowman:
                return 2f;
            default:
                return 3f;
        }
    }

    /// <summary>
    /// 将军训练限量校验（3.5 P2，§10 将军限量 2 可配置）。
    /// 统计当前我方将军数（Occupation.General），达到 KingdomConfig.generalLimit 则拒绝。
    /// </summary>
    private bool CanTrainGeneral()
    {
        var cfg = KingdomManager.Instance != null ? KingdomManager.Instance.Config : null;
        int limit = cfg != null && cfg.generalLimit > 0 ? cfg.generalLimit : 2;
        int count = 0;
        if (UnitRegistry.Instance != null)
        {
            foreach (var unit in UnitRegistry.Instance.GetAllUnits())
            {
                if (unit == null || unit.Data == null) continue;
                if (unit.Data.faction != Faction.PlayerCamp) continue;
                if (unit.EffectiveOccupation == Occupation.General) count++;
            }
        }
        if (count >= limit)
        {
            Debug.Log($"[TrainingSystem] 转职失败：将军已达上限 {limit}（当前 {count}）");
            return false;
        }
        return true;
    }
}