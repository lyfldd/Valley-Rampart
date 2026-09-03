// ============================================================================
//  种族=人口属性 · 数据层地基（2_20 §十二 D467~D472 / D475，HH.51 种族1 批A）
//  RaceIds：种族标识常量（当前世界全默认 Human，D416 前口径）。
//    序号对齐 2_13 M10 选族 UI 既有约定（NewGameConfig.raceId：0=人类,1=精灵,2=矮人,3=兽人），
//    防选族索引与种族 id 两套空间错位；Q10-M2 落 RaceDef/KingdomDef.raceId 后统一定型（挂账）。
//  KingdomRace.GetKingdomRace：国族解析唯一入口（防散落硬编码）——
//    KingdomDef.raceId 字段属 Q10-M2 域（2_20 实施清单 M2），本批不加字段；
//    helper 现阶段恒返回 Human 默认+挂账注记，Q10-M2 落字段后在此单点回填
//    （届时 KingdomDef.raceId=模板映射 / 动态立国=营地成员多数派 / 玩家=选族暂存 NewGameConfig.raceId）。
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
    /// 解析王国种族（国族）。当前恒返回 Human 默认（挂账 Q10-M2：KingdomDef.raceId 字段落库后单点回填本方法）。
    /// 语义=D467"国家种族=其人口种族（构造性不变）"——本批人口侧已全 Human，两口径暂等价。
    /// </summary>
    public static int GetKingdomRace(int kingdomId)
    {
        // 挂账注记（Q10-M2 回填点）：
        //  - kingdomId=0 玩家 → 选族暂存 NewGameConfig.raceId（2_13 M10 已暂存）
        //  - kingdomId>0 AI/动态 → KingdomDef.raceId（模板映射，2_20 M2）/ 动态立国=插旗定族（D471）
        return RaceIds.Human;
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
