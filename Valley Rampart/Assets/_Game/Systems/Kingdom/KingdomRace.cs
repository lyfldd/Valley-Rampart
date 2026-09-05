// ============================================================================
//  种族=人口属性 · 数据层地基（2_20 §十二 D467~D472 / D475，HH.51 种族1 批A；Q10-M2 回填 2026-09-03）
//  RaceIds：种族标识常量。
//    序号对齐 2_13 M10 选族 UI 既有约定（NewGameConfig.raceId：0=人类,1=精灵,2=矮人,3=兽人），
//    RaceDef/KingdomDef.raceId 均按本 int 空间定型（M1 四资产/M2 模板映射同空间）。
//  KingdomRace.GetKingdomRace：国族解析唯一入口（防散落硬编码）——
//    M2 回填：读 KingdomState.raceId 真字段（AI=模板映射 Foundry 写入 / 动态=D471 插旗定族 /
//    玩家=选族暂存 M5 绑定，暂默认 Human）；Registry 缺失/查无王国 → Human 兜底（旧档兼容语义）。
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

/// <summary>种族标识常量（int 空间=2_13 M10 选族索引约定；Q10-M2 定型前唯一真源）。</summary>
public static class RaceIds
{
    public const int Human = 0;   // 人类（兜底默认：旧档缺字段/来源不可考 → Human，D467 兜底口径）
    public const int Elf = 1;     // 精灵
    public const int Dwarf = 2;   // 矮人
    public const int Orc = 3;     // 兽人
}

/// <summary>国族解析统一 helper（D467 国家同族锁定的消费侧唯一入口）。</summary>
public static class KingdomRace
{
    /// <summary>
    /// 解析王国种族（国族）。M2 回填（2026-09-03）：读 KingdomState.raceId 真字段——
    /// AI 第一代=KingdomDef.raceId 模板映射（KingdomFoundry 建国写入）；
    /// 动态立国=D471 插旗定族（FoundFromCamp 显式写入）；玩家=选族暂存（M5 绑定，暂默认 Human）。
    /// 语义=D467"国家种族=其人口种族（构造性不变）"。
    /// Registry 缺失/查无王国 → Human 兜底（旧档/边界兼容，与字段默认一致）。
    /// </summary>
    public static int GetKingdomRace(int kingdomId)
    {
        var registry = KingdomRegistry.Instance;
        var state = registry != null ? registry.Get(kingdomId) : null;
        return state != null ? state.raceId : RaceIds.Human;
    }

    // ===== 2_20 M5 种族乘数消费（Q10 批2，D420 映射权威+D503/D506 真值）=====

    /// <summary>四族 RaceDef 资产缓存（Resources/Config/Races，M5/D420 消费侧统一入口，防散落 Resources.Load）。</summary>
    private static RaceDef[] _raceDefCache;

    /// <summary>
    /// 取王国国族对应的 RaceDef（2_20 M5/D420：种族乘数消费统一入口）。
    /// kingdomId&lt;0（野生自然建筑哨兵）或查无王国 → null（消费侧 mul=1 中性兜底——中立/异常来源不吃修正）；
    /// 怪物（Faction.Monster）由消费侧自行过滤（无国族语义）。
    /// </summary>
    public static RaceDef GetKingdomRaceDef(int kingdomId)
    {
        if (kingdomId < 0) return null;
        if (_raceDefCache == null)
        {
            _raceDefCache = new RaceDef[4];
            var all = Resources.LoadAll<RaceDef>("Config/Races");
            for (int i = 0; i < all.Length; i++)
            {
                var d = all[i];
                if (d != null && d.raceId >= 0 && d.raceId < _raceDefCache.Length)
                    _raceDefCache[d.raceId] = d;
            }
        }
        int race = GetKingdomRace(kingdomId);
        return race >= 0 && race < _raceDefCache.Length ? _raceDefCache[race] : null;
    }

    /// <summary>
    /// 按 raceId 直取 RaceDef（2_20 M10 选族 UI / D431，HH.66 段A）：选族卡渲染数据源。
    /// 与 GetKingdomRaceDef 共享缓存（D420 铁律：UI 侧禁散落 Resources.Load，统一本入口）；
    /// 越界/未建 → null（消费侧兜底）。
    /// </summary>
    public static RaceDef GetRaceDef(int raceId)
    {
        if (raceId < 0 || raceId >= 4) return null;
        if (_raceDefCache == null) GetKingdomRaceDef(RaceIds.Human);   // 惰性建缓存（复用既有加载路径）
        return _raceDefCache[raceId];
    }

    /// <summary>
    /// 采集/生产资源 → 种族经济乘数（2_20.1 §二 D420 映射权威 + D506③ 裁决表 2026-09-04）：
    /// Stone/Ore→mineMul、Wood→lumberMul、Food/Meat→farmMul；
    /// Metal/SpecialFood/Crystal/FireOil/Gold/三弹药=加工品/副产/货币不乘（防中间加工重复加成）。
    /// Production（ProducerComponent.Tick 主产累加）与 Gather（TaskScheduler.ExecuteCompletion 入库）
    /// 两侧同源本表（D420 铁律：同 mul 消费点两处同乘防漂移）。
    /// </summary>
    public static float GetGatherMul(int kingdomId, ResourceType resourceType)
    {
        var def = GetKingdomRaceDef(kingdomId);
        if (def == null) return 1f;
        switch (resourceType)
        {
            case ResourceType.Stone:
            case ResourceType.Ore:
            {
                // 2_20 M6 地脉熔炉：采矿产量全局+40%（2_20.1 §三，同 mineMul 消费点叠乘；乘算口径 1.3×1.4=1.82 HH.60 §三.2）
                float leyMul = HasExclusiveBuilding(kingdomId, "LeyForge") ? 1.4f : 1f;
                return def.mineMul * leyMul;
            }
            case ResourceType.Wood:  return def.lumberMul;
            case ResourceType.Food:
            case ResourceType.Meat:  return def.farmMul;
            default:                 return 1f;   // 加工品/副产/货币不乘（D506③）
        }
    }

    /// <summary>
    /// 王国是否已建某专属建筑（2_20 M6：每族限建 1，全局效果/训练入口叠加都按此查重）。
    /// 玩家=kingdomId 0；AI=自身王国。BuildingRegistry 缺失 → false（防御兜底）。
    /// </summary>
    public static bool HasExclusiveBuilding(int kingdomId, string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId) || BuildingRegistry.Instance == null) return false;
        var all = BuildingRegistry.Instance.All;
        for (int i = 0; i < all.Count; i++)
        {
            var b = all[i];
            if (b != null && b.def != null && b.def.id == buildingId && b.kingdomId == kingdomId && b.IsActive)
                return true;
        }
        return false;
    }

    /// <summary>
    /// 成员组种族多数派解析（D471 国族=营族 / D308 按族投放 / D306 修订 异族营判定 共用）：
    /// 按存活成员 raceId 计数取多数派；平票 → 传入 rng 同 seed 确定随机（D471 防御口径）并由 tie=true 交调用方告警；
    /// 空组/注册表不可用 → Human 兜底（tie=false）。
    /// </summary>
    public static int ResolveGroupRace(List<int> memberIds, System.Random rng, out bool tie)
    {
        tie = false;
        if (memberIds == null || memberIds.Count == 0 || UnitRegistry.Instance == null)
            return RaceIds.Human;

        var counts = new Dictionary<int, int>();
        foreach (var uc in UnitRegistry.Instance.GetAllUnits())
        {
            if (uc == null || !uc.IsAlive || !memberIds.Contains(uc.npcId)) continue;
            counts[uc.raceId] = counts.TryGetValue(uc.raceId, out var c) ? c + 1 : 1;
        }
        if (counts.Count == 0) return RaceIds.Human;

        int best = RaceIds.Human, bestN = -1, tieCount = 0;
        foreach (var kv in counts)
        {
            if (kv.Value > bestN) { bestN = kv.Value; best = kv.Key; tieCount = 1; }
            else if (kv.Value == bestN) tieCount++;
        }
        if (tieCount > 1)
        {
            tie = true;
            if (rng != null)
            {
                var tied = new List<int>();
                foreach (var kv in counts) if (kv.Value == bestN) tied.Add(kv.Key);
                best = tied[rng.Next(0, tied.Count)];   // 同 seed 确定随机（rng 由世界/玩法确定性流传入）
            }
            // rng 缺省 → 保持固定遍历序首个最大值（确定性兜底）
        }
        return best;
    }
}
