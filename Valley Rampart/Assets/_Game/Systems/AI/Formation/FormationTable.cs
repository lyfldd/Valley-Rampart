using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  3.0.1_3 AI 协作 - 阵型查找表 SO（§3.6 候选表分级查表）
//  详见 3.0.1_3_AI协作.md §3.6 / §14.4
//  设计期烘焙 Top-N 候选（标安全向/进攻向），运行时按威胁因子分档权重候选内挑
//  P0：手配防守/进攻/守城各一条，直接按意图查表（无分档权重挑选）
//  P1：扩为 Top-N 候选 + ThreatWeightTier SO 分档映射
// ============================================================================

/// <summary>
/// 阵型查找表 SO（§3.6）。
/// key = (Intent, BattleLine, meleeCount, archerCount) → FormationDef
/// P0 简化为按意图直查（手配单阵型）；P1 扩为 Top-N 候选 + 分档权重。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/FormationTable", fileName = "FormationTable")]
public class FormationTable : ScriptableObject
{
    [Header("P0 手配阵型（按意图直查，残编自动压队尾）")]
    [Tooltip("防守意图阵型（满编 3 近 2 弓 + 将军）")]
    public FormationDef defenseFormation;
    [Tooltip("进攻意图阵型（将军带头，弓手殿后）")]
    public FormationDef chargeFormation;
    [Tooltip("撤退意图阵型（近战殿后，弓手先走）")]
    public FormationDef retreatFormation;
    [Tooltip("守城阵型（无将军，弓手上墙 + 近战堵口）")]
    public FormationDef garrisonFormation;

    [Header("3.7 合法阵型列表（编队构成自适应：AI 按构成/意图自选）")]
    [Tooltip("全部合法阵型（可多个同意图阵型供 AI 按构成选最优）。留空则回退用上方 P0 手配阵型去重")]
    public List<FormationDef> formationList = new List<FormationDef>();

    /// <summary>
    /// 合法阵型列表（3.7 §4.1：LookupAll，供编队构成自适应按意图+构成自选）。
    /// formationList 非空用列表；空则回退收集 P0 手配阵型（去重），资产无需改造即生效。
    /// </summary>
    public FormationDef[] LookupAll()
    {
        var list = new List<FormationDef>();
        if (formationList != null && formationList.Count > 0)
        {
            list.AddRange(formationList);
        }
        else
        {
            if (defenseFormation != null && !list.Contains(defenseFormation)) list.Add(defenseFormation);
            if (chargeFormation != null && !list.Contains(chargeFormation)) list.Add(chargeFormation);
            if (retreatFormation != null && !list.Contains(retreatFormation)) list.Add(retreatFormation);
            if (garrisonFormation != null && !list.Contains(garrisonFormation)) list.Add(garrisonFormation);
        }
        return list.ToArray();
    }

    /// <summary>
    /// 按意图查阵型（3.7 升级：从合法阵型列表按意图过滤 + 构成匹配度选最优）。
    /// 构成匹配度 MatchScore：容量匹配（成员数 vs 槽数）+ 角色匹配（近战/远程 vs 槽位约束）。
    /// formationList 空时回退 P0 单阵型直查。
    /// 守城编队（无将军）直接返回 garrisonFormation，不按意图查。
    /// </summary>
    public FormationDef Lookup(TacticIntent intent, BattleLine line, int meleeCount, int archerCount)
    {
        var all = LookupAll();
        FormationDef best = null;
        float bestScore = -1f;
        foreach (var f in all)
        {
            if (f == null || f.intent != intent) continue;
            float score = MatchScore(f, meleeCount, archerCount);
            if (score > bestScore)
            {
                bestScore = score;
                best = f;
            }
        }
        if (best != null) return best;

        // 回退：P0 单阵型直查（列表无匹配意图的阵型时）
        switch (intent)
        {
            case TacticIntent.Charge:
            case TacticIntent.Sally: return chargeFormation;   // 3.7 §4.3：出城迎战复用进攻阵型压上
            case TacticIntent.Retreat: return retreatFormation;
            case TacticIntent.Defense:
            default: return defenseFormation;
        }
    }

    /// <summary>守城编队专用查表（无将军，城墙锚点）</summary>
    public FormationDef LookupGarrison()
    {
        return garrisonFormation;
    }

    /// <summary>
    /// 阵型-构成匹配度（0-1，越大越匹配）。
    /// 容量匹配：槽数越接近成员数越高；角色匹配：近战/远程能填进的槽占比越高。
    /// </summary>
    private static float MatchScore(FormationDef f, int meleeCount, int archerCount)
    {
        if (f == null || f.slots == null || f.slots.Length == 0) return 0f;
        int total = meleeCount + archerCount;
        if (total <= 0) total = 1;

        int meleeSlots = 0, rangedSlots = 0;
        foreach (var s in f.slots)
        {
            if (s.role == SlotRole.MeleeOnly || s.role == SlotRole.GeneralOnly) meleeSlots++;
            else if (s.role == SlotRole.RangedOnly) rangedSlots++;
            else { meleeSlots++; rangedSlots++; }   // Any 两可
        }

        float capacityFit = 1f - Mathf.Abs(f.slots.Length - total) / (float)f.slots.Length;
        float roleFit = (Mathf.Min(meleeCount, meleeSlots) + Mathf.Min(archerCount, rangedSlots)) / (float)total;
        return capacityFit * 0.5f + roleFit * 0.5f;
    }
}
