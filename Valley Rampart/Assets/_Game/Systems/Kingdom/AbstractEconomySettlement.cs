using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 抽象结算适配层（2_17 步骤14 批A，D459 并存+分叉调用；批B 补 D460/D400）。
/// 职责=KingdomState↔DTO 翻译 + 增量应用；公式本体在纯 C# AbstractEconomySettler（零 Unity 引用）。
/// 边界：只处理 simMode==Abstract 的 AI 王国；玩家(id=0)恒 Fine 不进本路径（零回归，探针 P2 负向）。
/// 确定性：建筑遍历固定排序（⑤-3 硬性 a 同款纪律），同 seed 双轮逐字节一致（探针 P3）。
/// 15_账本「一·补二」对账时点标注（D463）：两分支（AIEconomySettlement/本层）统一日结粒度 1 日，
/// 王国脑"花昨日结存"（HH.24 裁决①）两分支一致；sim 瞬时入账差异保留为已知差异留阶段 B。
/// 批B（D460/D400）：断粮扣 per-kingdom 均饱食（镜像 sim EconomyTick）；D400 流失落地=实体转流民
/// +最近营地（计数迁移同世界账）；唤醒拉平=写 KingdomState.lastAbstractAvgSatiety 供 SatietySystem 消费。
/// </summary>
public static class AbstractEconomySettlement
{
    /// <summary>连续断粮日（per-kingdom 运行时，不入档 D456 同哲学；D400 流失判定输入）。</summary>
    private static readonly Dictionary<int, int> _unfedDays = new Dictionary<int, int>();

    /// <summary>地图生成时清空（对齐 SimModeManager._uncoveredDays 生命周期）。</summary>
    public static void OnMapGenerated() => _unfedDays.Clear();

    /// <summary>日结 Abstract 段：把所有 simMode==Abstract 的 AI 王国按镜像公式结算一次。</summary>
    public static void Tick()
    {
        var reg = KingdomRegistry.Instance;
        if (reg == null) return;

        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            if (k.IsPlayer) continue;                              // 玩家恒 Fine（零回归）
            if (k.simMode != SimMode.Abstract) continue;           // Fine 王国走 AIEconomySettlement
            SettleKingdom(k);
        }
    }

    /// <summary>单 Abstract 王国结算：快照 → 纯函数公式 → 增量应用 + 均饱食/流失落地。</summary>
    private static void SettleKingdom(KingdomState k)
    {
        var snapshot = BuildSnapshot(k);
        snapshot.ContinuousUnfedDays = _unfedDays.TryGetValue(k.id, out var u) ? u : 0;
        var delta = AbstractEconomySettler.SettleDaily(snapshot, LoadParams(), EcoModifiers.Default);

        // 更新连续断粮日：断粮 +1，未断粮归 0（D400 判定输入，确定性）
        _unfedDays[k.id] = delta.FoodExhausted ? snapshot.ContinuousUnfedDays + 1 : 0;

        ApplyDelta(k, delta);
        ApplySatietyAndLoss(k, snapshot, delta);

        if (delta.FoodExhausted)
            Debug.Log($"[AbstractEconomySettlement] k{k.id} 断粮：日耗粮缺口 {delta.UnfedShortfall}，均饱食 {snapshot.AvgSatiety}→{snapshot.AvgSatiety + delta.AvgSatiety}（连续 {_unfedDays[k.id]} 日）");
        if (delta.Wood + delta.Stone + delta.Food + delta.Metal + delta.Gold != 0)
            Debug.Log($"[AbstractEconomySettlement] k{k.id} 抽象日结 木+{delta.Wood} 石+{delta.Stone} 粮+{delta.Food} 铁+{delta.Metal} 金+{delta.Gold}（人口 生活{snapshot.LifeCount}/士兵{snapshot.SoldierCount}/高耗{snapshot.EliteCount}）");
    }

    /// <summary>批B：从 AbstractEconomyConfig SO 读参数（数值双落：SO 序列化值；加载失败回退 .cs 默认值）。</summary>
    private static AbstractEconomyParams LoadParams()
    {
        var so = AbstractEconomyConfig.LoadConfig();
        return so != null ? so.ToParams() : AbstractEconomyParams.Default;
    }

    /// <summary>断粮扣均饱食 → 写 avgSatiety 桶 + 唤醒拉平标记；D400 流失落地=实体转流民+最近营地。</summary>
    private static void ApplySatietyAndLoss(KingdomState k, KingdomEconomySnapshot snapshot, SettlementDelta delta)
    {
        // 均饱食：抽象结算维护公式值（断粮扣、未断粮保持），每次结算更新桶+唤醒拉平标记
        float newAvg = Mathf.Clamp(snapshot.AvgSatiety + delta.AvgSatiety, 0f, 100f);
        if (SatietySystem.Instance != null)
            SatietySystem.Instance.SetAverageSatietyCached(k.id, newAvg);
        k.lastAbstractAvgSatiety = newAvg;   // D335/D460 唤醒拉平（切回 Fine 由 SatietySystem 消费）

        // D400 流失：居民/战士转流民 → 最近营地（计数迁移同世界账；每日 ≤1）
        if (delta.LossResidents > 0)
        {
            var u = FindFirstUnit(k.id, Occupation.Resident, Occupation.Worker, Occupation.Porter, Occupation.Civilian);
            if (u != null)
            {
                MigrateToNearestCamp(u);
                Debug.Log($"[AbstractEconomySettlement] k{k.id} D400 居民流失→流民（转最近营地）：{u.name}");
            }
        }
        if (delta.LossSoldiers > 0)
        {
            var u = FindFirstUnit(k.id, Occupation.Warrior, Occupation.Archer, Occupation.Crossbowman, Occupation.Cavalry,
                Occupation.Mage, Occupation.Healer, Occupation.HeavyWarrior, Occupation.ShieldGuard,
                Occupation.Archmage, Occupation.Bishop, Occupation.General);
            if (u != null)
            {
                MigrateToNearestCamp(u);
                Debug.Log($"[AbstractEconomySettlement] k{k.id} D400 战士断粮解散→流民（转最近营地）：{u.name}");
            }
        }
    }

    /// <summary>按注册表固定序找某王国第一个匹配职业的存活 NPC（D335 unit id 序确定性；注册表序=生成序确定）。</summary>
    private static UnitController FindFirstUnit(int kingdomId, params Occupation[] occs)
    {
        if (UnitRegistry.Instance == null) return null;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive || u.kingdomId != kingdomId) continue;
            var occ = u.EffectiveOccupation;
            for (int i = 0; i < occs.Length; i++) if (occs[i] == occ) return u;
        }
        return null;
    }

    /// <summary>实体转流民并迁到最近营地（D400 计数迁移落地：occupation=Vagrant + 位移营地 + BirthCampPos）。</summary>
    private static void MigrateToNearestCamp(UnitController u)
    {
        u.SetOccupation(Occupation.Vagrant);
        Vector2 pos = u.GetPosition();
        var camps = VagrantCampSystem.Instance != null ? VagrantCampSystem.Instance.FindCamps() : null;
        if (camps != null && camps.Count > 0)
        {
            Building nearest = camps[0];
            float best = float.MaxValue;
            for (int i = 0; i < camps.Count; i++)
            {
                float d = Vector2.Distance(camps[i].GetPosition(), pos);
                if (d < best) { best = d; nearest = camps[i]; }
            }
            pos = nearest.GetPosition();
            var t = u.transform;
            if (t != null) t.position = new Vector3(pos.x, pos.y, t.position.z);
        }
        u.BirthCampPos = pos;
    }

    /// <summary>构建王国经济快照（人口派生计数 + 建筑清单固定排序）。</summary>
    private static KingdomEconomySnapshot BuildSnapshot(KingdomState k)
    {
        int kid = k.id;
        return new KingdomEconomySnapshot
        {
            KingdomId = kid,
            // 产能工人池（镜像 sim Population.Worker）
            WorkerCount = PopulationSystem.AliveWorkerCount(kid),
            // 生活职业（1 耗粮/日）：居民/工人/搬运/小孩
            LifeCount = PopulationSystem.CountAliveByKingdom(kid,
                Occupation.Resident, Occupation.Worker, Occupation.Porter, Occupation.Child),
            // 士兵（2 耗粮/日）
            SoldierCount = PopulationSystem.CountAliveByKingdom(kid,
                Occupation.Warrior, Occupation.Archer, Occupation.Crossbowman, Occupation.Cavalry,
                Occupation.Mage, Occupation.Healer),
            // 高耗（3 耗粮/日）
            EliteCount = PopulationSystem.CountAliveByKingdom(kid,
                Occupation.HeavyWarrior, Occupation.ShieldGuard, Occupation.Archmage,
                Occupation.Bishop, Occupation.General),
            Buildings = QueryKingdomBuildings(kid),
            Food = k.resources.food, Gold = k.resources.gold,
            Stone = k.resources.stone, Wood = k.resources.wood, Metal = k.resources.metal,
            AvgSatiety = SatietySystem.Instance != null ? SatietySystem.Instance.GetAverageSatietyCached(kid) : 50f
        };
    }

    /// <summary>
    /// 查询某王国全部产能建筑并转纯 C# 条目。BuildingRegistry（单例真源）；单例未物化时
    /// FindObjectsOfType 兜底。返回前一律固定排序（核心：禁依赖收集序，⑤-3 硬性 a）。
    /// 产出映射：isBlacksmith→Metal；outputResource Wood/Stone/Ore/Food；其余（Gold 退役/Crystal/FireOil/
    /// Well/SiegeWorkshop 弹药）跳过不入 AI 国库（对齐 2b MapToPack 非经济资源跳过）。
    /// </summary>
    private static List<AbstractBuildingEntry> QueryKingdomBuildings(int kingdomId)
    {
        List<Building> buildings = new List<Building>();
        var reg = BuildingRegistry.Instance;
        if (reg != null && reg.All != null)
        {
            var src = reg.All;
            for (int i = 0; i < src.Count; i++)
                if (src[i] != null && src[i].kingdomId == kingdomId && src[i].IsActive)
                    buildings.Add(src[i]);
        }
        else
        {
            foreach (var b in Object.FindObjectsByType<Building>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b != null && b.kingdomId == kingdomId && b.IsActive)
                    buildings.Add(b);
        }

        // 固定排序：主键=坐标（左上格），次键=def.id（String.CompareOrdinal），彻底丢掉收集序/注册序。
        buildings.Sort((a, b) =>
        {
            if (a.coord.y != b.coord.y) return a.coord.y.CompareTo(b.coord.y);
            if (a.coord.x != b.coord.x) return a.coord.x.CompareTo(b.coord.x);
            var ad = a.def != null ? a.def.id : "";
            var bd = b.def != null ? b.def.id : "";
            return string.CompareOrdinal(ad, bd);
        });

        var entries = new List<AbstractBuildingEntry>(buildings.Count);
        for (int i = 0; i < buildings.Count; i++)
        {
            var b = buildings[i];
            var def = b.def;
            if (def == null || def.producer.kind != ProduceKind.Resource) continue;   // 非产资源建筑跳过
            if (def.id == "Well") continue;                        // AI 井恒不产水（D454 守卫），不入国库
            if (def.isSiegeWorkshop) continue;                     // 投掷机厂产弹药，非经济资源不入国库

            string type = null;
            if (def.isBlacksmith) type = "Metal";                  // 铁匠铺石→Metal（D200）
            else
            {
                switch (def.outputResource)
                {
                    case ResourceType.Wood: type = "Wood"; break;
                    case ResourceType.Stone: type = "Stone"; break;
                    case ResourceType.Ore: type = "Ore"; break;
                    case ResourceType.Food: type = "Food"; break;
                    default: type = null; break;                   // Gold（D144 退役）/Crystal/FireOil 跳过
                }
            }
            if (type == null) continue;

            entries.Add(new AbstractBuildingEntry
            {
                Type = type,
                Level = b.level,
                ConcurrentCapacity = def.concurrentWorkers
            });
        }
        return entries;
    }

    /// <summary>增量应用：资源入账（台账制 AddResources）。Food 不会为负（引擎 consume=min(need,available) 已保证）。</summary>
    private static void ApplyDelta(KingdomState k, SettlementDelta d)
    {
        if (d.Food == 0 && d.Gold == 0 && d.Stone == 0 && d.Wood == 0 && d.Metal == 0) return;
        k.AddResources(new ResourcePack
        {
            food = d.Food, gold = d.Gold, stone = d.Stone, wood = d.Wood, metal = d.Metal
        });
    }
}
