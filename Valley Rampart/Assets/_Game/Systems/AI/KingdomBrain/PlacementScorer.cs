using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  AI 建筑选址打分器（2_22 P0 批D / D2+D3，D525 §3.7）。
//  缺口实锤（§3.7）：原 FindAIBuildSpot=切比雪夫环带扫描取首个合法格——"合法即可"零选址
//  质量。本打分器=ExecuteBuildFocus 选址半边升级，覆盖全部 AI 建造不只军事。
//
//  机制：
//    候选收集：主城锚定环带扫描（r=0..aiBuildRadius 固定序）全量收集合法微格
//              ——PlacementValidator 校验先行全继承（占用/地形/水域/资源点/城门/桥/国库门），
//                不做前 K 截断（环带序前 K 恒为近城格，系统性屏蔽边境候选与 F1 矛盾）。
//    打分：    score(spot) = w1×f1(距威胁锚) + w2×f2(距关联建筑) + w3×f3(距主城紧凑−同类密度)
//    取舍：    argmax 严格大于（首者保留=平局固定环带序，同 seed 确定性红线）。
//    无候选：  null（退回现行失败路径=Bump fail 明日再试）。
//
//  特征归一口径（实现近似，列报申报）：
//    F1 距威胁边境 → 主威胁国主城格作方向锚（快照 Threats 升序首个非零兵国；威胁≈0 置 0 退化通用）。
//    F2 距关联建筑 → 本国最近一座 Active 关联建筑格距（Chebyshev）。
//    F3 距主城+同类密度 → 主城格距紧凑项 − 密度半径内本国同类建筑计数×惩罚。
//  归一化：f1/f2/f3 距离项均以扫描半径为分母线性归一到 [0,1]（越近越高），f3 可为负被 clamp 0。
//
//  边界（D4）：Unity 侧执行层不进 AI.Core（sim 无空间概念零镜像）；玩家建造入口/UI/校验零改动；
//  城墙 per-def 特征集=仅 F3（紧凑行为即现状等价）。
// ============================================================================

public static class PlacementScorer
{
    /// <summary>选址打分结果（探针断言面：选中格+总分+特征分解+候选数）。</summary>
    public struct PickResult
    {
        public GridCoord Sub;       // 选中微格
        public float Score;         // 总分
        public float F1, F2, F3;    // 特征分解（归一后）
        public int Candidates;      // 合法候选总数（全量收集面）
    }

    /// <summary>
    /// 选址主入口：全量收集+打分+argmax。无主城/无候选 → null（调用方走现行失败路径）。
    /// 确定性：候选序=环带固定序；威胁锚/关联建筑=升序确定性查询；argmax 严格大于首者保留。
    /// </summary>
    /// <param name="kingdomId">建造王国 id</param>
    /// <param name="def">建筑 def</param>
    /// <param name="maxRadius">主城锚定扫描半径（aiBuildRadius）</param>
    /// <param name="pcfg">选址打分配置（D1；null=默认权重全 1）</param>
    /// <param name="sit">态势快照（F1 威胁锚消费；null=威胁≈0 态，F1 置 0）</param>
    /// <param name="result">打分分解（探针/诊断面）</param>
    public static bool TryPick(int kingdomId, BuildingDef def, int maxRadius,
        BuildingPlacementConfig pcfg, SituationSnapshot sit, out PickResult result)
    {
        result = default;
        var grid = GridSystem.Instance;
        var anchorCell = KingdomBrain.FindCastleCell(kingdomId);
        if (grid == null || anchorCell == null || def == null) return false;
        var anchor = anchorCell.Value;
        int maxR = Mathf.Max(1, maxRadius);

        var rule = pcfg != null ? pcfg.Find(def.id) : null;
        float w1 = rule != null ? rule.w1ThreatFront : (pcfg != null ? pcfg.defaultW1 : 1f);
        float w2 = rule != null ? rule.w2LinkBuilding : (pcfg != null ? pcfg.defaultW2 : 1f);
        float w3 = rule != null ? rule.w3CastleCompact : (pcfg != null ? pcfg.defaultW3 : 1f);
        string linkId = rule != null ? rule.linkBuildingId : null;
        float densityPenalty = rule != null ? rule.sameTypeDensityPenalty : 0f;
        int densityR = pcfg != null ? Mathf.Max(0, pcfg.densityRadiusCells) : 5;

        // r2 修（HH.140 探针 P7 实锤）：特征距离坐标系统一——候选=sub 域（CellToSub），
        // 建筑/主城锚=cell 域（Building.coord）→跨域 Chebyshev 恒巨值→F1/F2/F3 恒 0=打分器退化首格。
        // 锚点统一转 sub 域（CellToSub 线性映射 cell×div+sx）；密度统计反向转 cell 域（SubToCell）语义=格距。
        int div = grid.Config != null && grid.Config.subCellDivisor > 0 ? grid.Config.subCellDivisor : 4;

        // 特征锚点（确定性查询；威胁≈0/无关联建筑 → 对应特征置 0 退化通用）
        GridCoord? threatAnchor = ResolveThreatAnchor(sit);
        if (threatAnchor.HasValue)
        {
            var ta = threatAnchor.Value;
            threatAnchor = new GridCoord(ta.x * div, ta.y * div, ta.layer);
        }
        List<GridCoord> links = linkId != null ? CollectKingdomBuildings(kingdomId, linkId) : null;
        if (links != null)
            for (int i = 0; i < links.Count; i++)
                links[i] = new GridCoord(links[i].x * div, links[i].y * div, links[i].layer);
        var castleSub = new GridCoord(anchor.x * div, anchor.y * div, anchor.layer);

        float best = float.NegativeInfinity;
        bool found = false;

        // 候选全量收集+打分（环带固定序=确定性；argmax 严格大于=平局取环带序先者）
        for (int r = 0; r <= maxR; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;   // 只扫当前环
                var cell = new GridCoord(anchor.x + dx, anchor.y + dy);
                if (!grid.IsInBounds(cell)) continue;
                var sub = grid.CellToSub(cell, 0, 0);
                if (!PlacementValidator.ValidatePlacement(def, sub, GateOrientation.Horizontal, kingdomId).ok)
                    continue;   // 校验先行全继承（九项）；不过=非候选
                result.Candidates++;

                float f1 = ComputeF1(sub, threatAnchor, maxR, div);
                float f2 = ComputeF2(sub, links, maxR, div);
                float f3 = ComputeF3(sub, castleSub, kingdomId, def.id, maxR, densityR, densityPenalty, div);
                float score = w1 * f1 + w2 * f2 + w3 * f3;
                if (!found || score > best)
                {
                    found = true;
                    best = score;
                    result.Sub = sub;
                    result.Score = score;
                    result.F1 = f1;
                    result.F2 = f2;
                    result.F3 = f3;
                }
            }
        }
        return found;
    }

    /// <summary>F1 距威胁锚（主威胁国主城方向，越近越高；威胁≈0 → 0 退化通用）。锚点与 spot 均 sub 域，半径按 cell 格换算 div 倍。</summary>
    private static float ComputeF1(GridCoord spot, GridCoord? threatAnchor, int maxR, int div)
    {
        if (!threatAnchor.HasValue) return 0f;   // 威胁≈0：F1 置 0（§3.7 表口径）
        return 1f - Mathf.Clamp01(Chebyshev(spot, threatAnchor.Value) / (float)(maxR * div));
    }

    /// <summary>F2 距最近关联建筑（本国 Active 同 def；无关联建筑/未配置 → 0）。锚点与 spot 均 sub 域。</summary>
    private static float ComputeF2(GridCoord spot, List<GridCoord> links, int maxR, int div)
    {
        if (links == null || links.Count == 0) return 0f;
        int min = int.MaxValue;
        for (int i = 0; i < links.Count; i++)
        {
            int d = Chebyshev(spot, links[i]);
            if (d < min) min = d;
        }
        return 1f - Mathf.Clamp01(min / (float)(maxR * div));
    }

    /// <summary>F3 距主城紧凑（sub 域）− 同类密度惩罚（cell 域格距语义）；距离项按扫描半径归一，clamp 0 下限防负分搅局。</summary>
    private static float ComputeF3(GridCoord spot, GridCoord castleSub, int kingdomId, string defId,
        int maxR, int densityR, float densityPenalty, int div)
    {
        float compact = 1f - Mathf.Clamp01(Chebyshev(spot, castleSub) / (float)(maxR * div));
        int sameType = densityPenalty > 0f && densityR > 0 ? CountSameTypeNearby(kingdomId, defId, spot, densityR, div) : 0;
        float f3 = compact - densityPenalty * sameType;
        return Mathf.Max(0f, f3);
    }

    /// <summary>半径内本国同类 Active 建筑计数（F3 密度惩罚面；spot 转 cell 域比对=格距语义；固定遍历=确定性）。</summary>
    private static int CountSameTypeNearby(int kingdomId, string defId, GridCoord spotSub, int radius, int div)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return 0;
        var spotCell = new GridCoord(spotSub.x / div, spotSub.y / div, spotSub.layer);
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b == null || b.def == null || !b.IsActive) continue;
            if (b.kingdomId != kingdomId || b.def.id != defId) continue;
            if (Chebyshev(b.coord, spotCell) <= radius) n++;
        }
        return n;
    }

    /// <summary>本国指定 def 的全部 Active 建筑坐标（F2 关联面；固定遍历序=确定性）。</summary>
    private static List<GridCoord> CollectKingdomBuildings(int kingdomId, string defId)
    {
        var list = new List<GridCoord>();
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return list;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.kingdomId == kingdomId && b.IsActive && b.def != null && b.def.id == defId)
                list.Add(b.coord);
        }
        return list;
    }

    /// <summary>主威胁锚：快照威胁表首个非零兵国主城格（表序=id 升序确定性；威胁≈0 → null）。</summary>
    private static GridCoord? ResolveThreatAnchor(SituationSnapshot sit)
    {
        if (sit?.Threats == null) return null;
        for (int i = 0; i < sit.Threats.Count; i++)
            if (sit.Threats[i].WarriorCount > 0)
                return KingdomBrain.FindCastleCell(sit.Threats[i].KingdomId);
        return null;
    }

    /// <summary>切比雪夫距离（格坐标；与环带扫描同度量）。</summary>
    private static int Chebyshev(GridCoord a, GridCoord b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
}
