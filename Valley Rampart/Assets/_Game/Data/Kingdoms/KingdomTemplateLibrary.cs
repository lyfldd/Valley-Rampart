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
}