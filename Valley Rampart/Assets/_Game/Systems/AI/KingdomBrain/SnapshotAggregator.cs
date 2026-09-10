// ============================================================================
//  兵种表现统计确定性聚合（2_22 P0 批E / E2，S-1 sim 对拍锚）
//  纯 C# 零 UnityEngine 引用（sim 镜像同源对称纪律，SituationSnapshot 同款防假同步模式）。
//  语义=KingdomBrain.BuildSituation 的兵种表现统计段（窗口 TTL 过滤+per 职业死亡计数
//  +OccupationId 升序确定性序），抽为纯函数供 sim 镜像（harness/KingdomBrain/SituationSnapshot.cs）
//  同源对称=同输入同输出（15_账本 S-1 对拍证据载体）。
// ============================================================================

/// <summary>
/// 兵种表现统计确定性聚合。TTL 窗口过滤 + 死亡计数 + 确定性升序——
/// 两侧共用同一实现保证 sim/Unity 行为保真。
/// </summary>
public static class SnapshotAggregator
{
    /// <summary>TTL 过滤损毁流水（day-D>=ttl 移除；ttl<=0 不过滤——对齐 BuildSituation）。</summary>
    public static System.Collections.Generic.List<LossEntry> FilterWindow(
        System.Collections.Generic.IReadOnlyList<LossEntry> losses, int day, int ttl)
    {
        var outList = new System.Collections.Generic.List<LossEntry>(losses != null ? losses.Count : 0);
        if (losses == null) return outList;
        for (int i = 0; i < losses.Count; i++)
        {
            var l = losses[i];
            if (ttl > 0 && day - l.Day >= ttl) continue;
            outList.Add(l);
        }
        return outList;
    }

    /// <summary>窗口死亡计数 → 兵种表现统计（排除建筑损毁；确定性=OccupationId 升序）。</summary>
    public static System.Collections.Generic.List<UnitPerfStat> BuildUnitPerformance(
        System.Collections.Generic.IReadOnlyList<LossEntry> window)
    {
        var outList = new System.Collections.Generic.List<UnitPerfStat>();
        if (window == null || window.Count == 0) return outList;
        var deaths = new System.Collections.Generic.Dictionary<int, int>();
        for (int i = 0; i < window.Count; i++)
        {
            var l = window[i];
            if (l.IsBuilding) continue;   // 建筑损毁不计入兵种死亡（对齐 L128 击杀统计口径）
            deaths.TryGetValue(l.OccupationId, out int n);
            deaths[l.OccupationId] = n + 1;
        }
        foreach (var kv in deaths)
            outList.Add(new UnitPerfStat { OccupationId = kv.Key, Deaths = kv.Value });
        outList.Sort((a, b) => a.OccupationId.CompareTo(b.OccupationId));   // 确定性序
        return outList;
    }
}