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
                if (day - e.startDay >= e.def.costDays)
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
    /// 训练请求（P1-10 队列管理）。校验起职 + 资源 → 扣费 → 入队列（有空槽立即开始，否则排队）。
    /// 训练期间居民保持原职业，costDays 天完成才转职（Update 推进）。
    /// 若训练建筑被摧毁，已投入资源不退、居民回退无职业（OnBuildingDestroyed）。
    /// </summary>
    public bool TryTrain(UnitController unit, TrainingDef def)
    {
        return TryTrain(unit, def, FindTrainingBuilding(def.buildingId));
    }

    /// <summary>带训练建筑实例的入队版本（TrainingPanel 传其所属设施，用于槽位管理）。</summary>
    public bool TryTrain(UnitController unit, TrainingDef def, Building building)
    {
        if (unit == null || unit.Data == null) return false;
        Occupation cur = unit.EffectiveOccupation;
        if (cur != def.fromOccupation)
        {
            Debug.Log($"[TrainingSystem] 转职失败：{cur} ≠ 起始职业 {def.fromOccupation}");
            return false;
        }
        if (RulerController.Instance == null || RulerController.Instance.Gold < def.costGold)
        {
            Debug.Log("[TrainingSystem] 转职失败：金币不足");
            return false;
        }
        // P1：魔法训练额外耗水晶（§10 法师/治疗师 水晶1）
        if (def.costCrystal > 0 && RulerController.Instance.GetResource(ResourceType.Crystal) < def.costCrystal)
        {
            Debug.Log("[TrainingSystem] 转职失败：水晶不足");
            return false;
        }
        // 2_12 步骤8 D132：兵种强化耗铁（重装战士/盾卫/骑兵 costMetal）
        if (def.costMetal > 0 && RulerController.Instance.GetResource(ResourceType.Metal) < def.costMetal)
        {
            Debug.Log("[TrainingSystem] 转职失败：铁不足");
            return false;
        }

        // P2：将军训练限量（KingdomConfig.generalLimit，§10 将军限量 2 可配置）
        if (def.toOccupation == Occupation.General && !CanTrainGeneral())
            return false;

        // 扣费（训练中断不退还，故入队即扣）
        RulerController.Instance.ModifyResource(ResourceType.Gold, false, def.costGold);
        if (def.costCrystal > 0)
            RulerController.Instance.ModifyResource(ResourceType.Crystal, false, def.costCrystal);
        if (def.costMetal > 0)
            RulerController.Instance.ModifyResource(ResourceType.Metal, false, def.costMetal);

        // 入队列（P1-10）：有空槽立即开始训练，否则排队
        if (!_queues.TryGetValue(building, out var q))
        {
            q = new TrainingQueue();
            _queues[building] = q;
        }
        var entry = new TrainingQueueEntry { unit = unit, def = def, inTraining = false };
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
                if (unit.Data.faction != Faction.Human_Player) continue;
                if (unit.kingdomId != 0) continue;   // 2_17 步骤4 关账扫描：仅玩家桶0——AI 工人不入玩家转职池(步骤10 退役)
                if (unit.EffectiveOccupation != def.fromOccupation) continue;
                if (!unit.IsAlive) continue;
                pool.Add(unit);
            }
        }
        if (pool.Count == 0) return false;

        var chosen = pool[Random.Range(0, pool.Count)];
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
            if (unit.Data.faction != Faction.Human_Player) continue;
            if (unit.kingdomId != 0) continue;   // 2_17 步骤4 关账扫描：仅玩家桶0——AI 工人不计玩家可训练数(步骤10 退役)
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
        // 回退：该设施可训项的目标职业
        var trainings = building != null && building.def != null ? GetTrainings(building.def.id) : null;
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
                if (unit.Data.faction != Faction.Human_Player) continue;
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