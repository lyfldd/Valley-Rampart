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
