using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 王国模板库（2_16 步骤4，§3.3）：持有 KingdomDef 模板池 + 不重复抽取 API（D315）。
/// 池规则：P0 放宽 = 池 ≥ 档内最大抽取数（6 模板够 Medium 4 档）；P2 补全至 8~10 后恢复"≥最大抽取数 +2 缓冲"。
/// 实例放 Resources/Config/Kingdoms/，由步骤3（PlaceKingdomSpawns 匹配）/步骤5（Foundry 立国）加载。
/// </summary>
[CreateAssetMenu(menuName = "ValleyRampart/Kingdoms/KingdomTemplateLibrary", fileName = "KingdomTemplateLibrary")]
public class KingdomTemplateLibrary : ScriptableObject
{
    /// <summary>模板池（6 占位：雪岩/金穗/铁蹄/密林/河湾/磐石，§3.3）。</summary>
    public KingdomDef[] templates;

    /// <summary>按名取模板（不存在返回 null）。</summary>
    public KingdomDef Get(string templateName)
    {
        if (string.IsNullOrEmpty(templateName) || templates == null) return null;
        for (int i = 0; i < templates.Length; i++)
            if (templates[i] != null && templates[i].templateName == templateName) return templates[i];
        return null;
    }

    /// <summary>
    /// 不重复抽取 count 个模板（Fisher-Yates 取前 count，rng 种子化保证确定性，对齐 2_14 R4）。
    /// 池不足时返回全部可用（调用方应已按 D315 保证池 ≥ 抽取数；若不足打日志兜底）。
    /// <para>【2_20 M3 弃用标注（D430/D506）】世界生成 AI 抽取已改走 <see cref="DrawAiTemplates"/>
    /// （玩家族占保底席+AI 保底其余三族各一+余者随机）。本方法现无调用方，保留作 D315
    /// 纯随机抽取语义的参考实现（2_21 GetUnitsInCell 弃用标注先例），勿在新链路调用。</para>
    /// </summary>
    public List<KingdomDef> DrawWithoutReplacement(System.Random rng, int count)
    {
        var result = new List<KingdomDef>();
        if (templates == null || templates.Length == 0 || count <= 0) return result;

        // 收集非空模板
        List<KingdomDef> pool = new List<KingdomDef>(templates.Length);
        for (int i = 0; i < templates.Length; i++)
            if (templates[i] != null) pool.Add(templates[i]);

        int take = System.Math.Min(count, pool.Count);
        if (take < count)
            Debug.LogWarning($"[KingdomTemplateLibrary] 模板池不足：需抽 {count}，实际 {pool.Count}（D315 池 ≥ 最大抽取数应为先决，请补全模板池）。");

        // Fisher-Yates 洗牌取前 take（原地交换，rng 确定性）
        for (int i = 0; i < take; i++)
        {
            int j = rng.Next(i, pool.Count);
            var tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
            result.Add(pool[i]);
        }
        return result;
    }

    /// <summary>
    /// AI 模板抽取（2_20 M3/D430：玩家种族占保底席+AI 保底其余三族各一+余者随机；D506② 降级口径=min(AI数,3)）。
    /// AI 池=排除玩家族模板（玩家族由玩家占席，AI 不复用——验收口径「三族各一不含玩家族」）；
    /// 保底=AI 池中实际有模板的种族（升序稳定初序）rng 洗牌定序取前 min(count, 族数) 族各 rng 抽 1 模板
    /// （AI 数&lt;保底族数时由 rng 定序决定保哪几族，同 seed 可复现）；
    /// 余者从剩余模板 Fisher-Yates 抽。池不足（玩家族占双模板+高 AI 数边界）并入玩家族模板兜底+警告。
    /// rng 沿用地图生成派生链（R4 确定性，禁 UnityEngine.Random）。
    /// 前置=玩家已注册占 id=0（WorldSystem.InitializeWorld 先 EnsurePlayerRegistered 后 ApplyConfig）。
    /// </summary>
    public List<KingdomDef> DrawAiTemplates(System.Random rng, int count, int playerRaceId)
    {
        var result = new List<KingdomDef>();
        if (templates == null || templates.Length == 0 || count <= 0) return result;

        // 非空模板按 raceId 分桶（桶内保持 templates 声明序=稳定序；桶键升序=跨运行稳定初序）
        var byRace = new SortedDictionary<int, List<KingdomDef>>();
        var all = new List<KingdomDef>();
        for (int i = 0; i < templates.Length; i++)
        {
            var t = templates[i];
            if (t == null) continue;
            all.Add(t);
            if (!byRace.TryGetValue(t.raceId, out var bucket))
                byRace[t.raceId] = bucket = new List<KingdomDef>();
            bucket.Add(t);
        }

        // AI 池=排除玩家族（D430 玩家占保底席）；池不足兜底并入玩家族模板（日志警告可见）
        var aiByRace = new SortedDictionary<int, List<KingdomDef>>();
        foreach (var kv in byRace)
            if (kv.Key != playerRaceId) aiByRace[kv.Key] = kv.Value;
        int aiTotal = 0;
        foreach (var kv in aiByRace) aiTotal += kv.Value.Count;
        if (aiTotal < count)
        {
            Debug.LogWarning($"[KingdomTemplateLibrary] AI 池（除玩家族 {playerRaceId}）仅 {aiTotal} < 抽取数 {count}，" +
                             $"并入玩家族模板兜底（D430 保底席挤占边界，请补全模板池或下调档位 AI 数）。");
            aiByRace = byRace;
        }

        // 保底：种族列表 rng 洗牌定序取前 min(count, 族数) 族各抽 1 模板（D506②；全覆盖时免洗牌省 rng 流）
        var raceKeys = new List<int>(aiByRace.Keys);
        int quota = Mathf.Min(count, raceKeys.Count);
        if (quota < raceKeys.Count)
        {
            for (int i = 0; i < raceKeys.Count; i++)
            {
                int j = rng.Next(i, raceKeys.Count);
                var tmp = raceKeys[i]; raceKeys[i] = raceKeys[j]; raceKeys[j] = tmp;
            }
        }
        for (int k = 0; k < quota; k++)
        {
            var bucket = aiByRace[raceKeys[k]];
            int pick = bucket.Count == 1 ? 0 : rng.Next(bucket.Count);
            result.Add(bucket[pick]);
        }

        // 余者：剩余模板（未保底抽走的）Fisher-Yates 抽 count-quota
        if (result.Count < count)
        {
            var rest = new List<KingdomDef>();
            foreach (var kv in aiByRace)
                foreach (var t in kv.Value)
                    if (!result.Contains(t)) rest.Add(t);
            int take = Mathf.Min(count - result.Count, rest.Count);
            for (int i = 0; i < take; i++)
            {
                int j = rng.Next(i, rest.Count);
                var tmp = rest[i]; rest[i] = rest[j]; rest[j] = tmp;
                result.Add(rest[i]);
            }
        }
        return result;
    }
}