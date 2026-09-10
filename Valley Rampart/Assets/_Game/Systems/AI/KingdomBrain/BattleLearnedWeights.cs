// ============================================================================
//  局内环自整定权重（2_22 P0 批B / B5，§3.4 双环外环）。
//  纯 C# 零 UnityEngine 引用（E2 sim 对称纪律，SituationSnapshot 同款防假同步模式）。
//
//  更新律（D519涌现范式）：wᵢ ← wᵢ × exp(η × (表现分ᵢ − 均值))，clamp 安全栏 [floor, cap]。
//  表现分ᵢ=1−pᵢ×N（pᵢ=窗口内兵种 i 死亡占比，N=有死亡记录的兵种数）→ 死得比均值多=权重降。
//  · 确定性：全序遍历（Occupation int 升序）+浮点确定性运算 → 同 seed 权重序列逐字节一致（B5 验收）。
//  · 安全栏：clamp 防单兵海坍缩+多样性下限在选招侧惩罚兜底（B6）。
//  · 结算节拍：P0=日 tick（死亡窗口滚动后调用）；战斗结算事件 2_18 接入后切换事件驱动
//    （HH.138 如实列报，接口签名不变）。
//  · 归因日志由调用方（KingdomBrain 日 tick）打印——本类纯计算（「这局为何狂造弓手」可追溯）。
// ============================================================================

/// <summary>某兵种窗口表现条目（调用方从 SituationSnapshot.UnitPerformance 传入）。</summary>
public struct UnitPerfInput
{
    public int OccupationId;
    public int Deaths;
}

/// <summary>权重调整归因条目（返回给调用方打日志）。</summary>
public struct WeightDelta
{
    public int OccupationId;
    public float OldW;
    public float NewW;
    public float PerfScore;   // 表现分（死亡占比反向）
}

public static class BattleLearnedWeights
{
    private static readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<int, float>> _w
        = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.Dictionary<int, float>>();

    // 安全栏（B5 验收：clamp 防坍缩；常量=结构参数非调值面，SO 化归 P0 调优后续轮列报）
    public const float WeightFloor = 0.2f;   // 下限：再差也不归零（保底可招）
    public const float WeightCap = 2.5f;     // 上限：单兵种权重封顶（多样性下限的权重侧硬规则）

    /// <summary>读取某国某兵种当前学习权重（无记录=1.0 中性起步）。</summary>
    public static float Get(int kingdomId, int occupationId)
    {
        return _w.TryGetValue(kingdomId, out var map) && map.TryGetValue(occupationId, out float v)
            ? v : 1f;
    }

    /// <summary>
    /// 日 tick 结算：窗口死亡统计 → 权重更新（η=学习率；调用方传 SituationConfig 域参数或保守常量）。
    /// 确定性：OccupationId 升序遍历。返回归因条目（权重有变化者，升序）。
    /// </summary>
    public static System.Collections.Generic.List<WeightDelta> UpdateFromLosses(
        int kingdomId, UnitPerfInput[] windowStats, float eta)
    {
        var deltas = new System.Collections.Generic.List<WeightDelta>();
        if (windowStats == null || windowStats.Length == 0) return deltas;

        // 死亡总数+有死亡兵种数（确定性单遍）
        int totalDeaths = 0;
        for (int i = 0; i < windowStats.Length; i++) totalDeaths += windowStats[i].Deaths;
        if (totalDeaths <= 0) return deltas;
        int n = windowStats.Length;

        // 均值死亡占比+逐兵种表现分（升序遍历前先复制排序——调用方已按升序传入则免排）
        var sorted = new UnitPerfInput[windowStats.Length];
        for (int i = 0; i < windowStats.Length; i++) sorted[i] = windowStats[i];
        System.Array.Sort(sorted, (a, b) => a.OccupationId.CompareTo(b.OccupationId));

        float meanP = 1f / n;   // 均匀占比=均值
        if (!_w.TryGetValue(kingdomId, out var map))
        {
            map = new System.Collections.Generic.Dictionary<int, float>();
            _w[kingdomId] = map;
        }

        for (int i = 0; i < sorted.Length; i++)
        {
            float p = sorted[i].Deaths / (float)totalDeaths;      // 该兵种死亡占比
            float perf = 1f - p * n;                              // 表现分：死得比均值多 → <1（可负）
            float oldW = map.TryGetValue(sorted[i].OccupationId, out float v) ? v : 1f;
            float newW = oldW * (float)System.Math.Exp(eta * (perf - 1f));   // (perf−均值1)=perf−1 中心化
            newW = newW < WeightFloor ? WeightFloor : (newW > WeightCap ? WeightCap : newW);
            map[sorted[i].OccupationId] = newW;
            if (System.Math.Abs(newW - oldW) > 0.0001f)
                deltas.Add(new WeightDelta { OccupationId = sorted[i].OccupationId, OldW = oldW, NewW = newW, PerfScore = perf });
        }
        return deltas;
    }

    /// <summary>快照某国全部权重（E1 存档面；无记录返回 false=中性 1.0 不用入档）。</summary>
    public static bool TrySnapshot(int kingdomId, out System.Collections.Generic.Dictionary<int, float> map)
    {
        if (_w.TryGetValue(kingdomId, out map))
        {
            // 拷贝快照，防调用方改写枢纽（存档面只读纪律）
            var copy = new System.Collections.Generic.Dictionary<int, float>(map);
            map = copy;
            return true;
        }
        map = null;
        return false;
    }

    /// <summary>写回某国某兵种权重（E1 读档恢复；按安全栏 clamp 防越界=与 UpdateFromLosses 同口径）。</summary>
    public static void SetWeight(int kingdomId, int occupationId, float w)
    {
        if (!_w.TryGetValue(kingdomId, out var map))
        {
            map = new System.Collections.Generic.Dictionary<int, float>();
            _w[kingdomId] = map;
        }
        w = w < WeightFloor ? WeightFloor : (w > WeightCap ? WeightCap : w);
        map[occupationId] = w;
    }

    /// <summary>王国灭亡/退订清槽（对齐 SituationHub.Remove 先例）。</summary>
    public static void Remove(int kingdomId) => _w.Remove(kingdomId);

    /// <summary>全清（harness 两轮间归零）。</summary>
    public static void Clear() => _w.Clear();
}
