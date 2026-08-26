using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_17 步骤9 效用评分器冒烟（HH.25 三条验收）
//  用法：菜单「Valley/验证/2_17_步骤9_效用评分器」——须 GameScene Play（先 Play 再点）。
//  p19_可执行：存活期 AI 王国恰有一可行候选（⑥招工人 gap>0 且可行），焦点非空不卡死（#19）
//  p4_底线覆盖:评分焦点(⑥)被粮底线强制翻到 屯粮(⑤) —— 常设底线覆盖评分排序，跳过防抖（#4）
//  p3_性格分化:同王国同局面，好战模板(personality[0])与 经济模板(personality[1]) → 评分 top 不同（#3）
//  自包含：InitializeNewGame 一轮（评分/焦点纯函数，无需两轮确定性重跑）。
// ============================================================================
public static class Valley2_17_Smoke_9
{
    private const int SEED = 20260827;

    [MenuItem("Valley/验证/2_17_步骤9_效用评分器")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_17_9冒烟] 须在 Play 上下文执行。"); return; }
        new GameObject("2_17_9_SmokeRunner").AddComponent<SmokeCoroutineHost>().Host(RunCoroutine());
    }

    public static IEnumerator RunCoroutine()
    {
        var lm = LoadManager.Instance;
        if (lm == null) { Debug.LogError("[2_17_9冒烟] LoadManager 不可用。"); yield break; }
        lm.InitializeNewGame(new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, difficulty = 2,
            worldSize = WorldSize.Medium, kingdomName = "2_17_9冒烟",
            selectedSlotId = "smoke_2_17_9"
        });
        yield return null;

        var c = new List<string>();
        var reg = KingdomRegistry.Instance;
        var ucfg = UtilityActionConfig.LoadConfig();
        var bcfg = KingdomBrain.LoadConfig();
        if (reg == null) { Debug.Log("[2_17_9冒烟] noReg-FAIL"); yield break; }

        KingdomState ai = null;
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && !all[i].IsPlayer) { ai = all[i]; break; }
        if (ai == null) { Debug.Log("[2_17_9冒烟] noAI-FAIL"); yield break; }

        // ---- #19 存活期不卡死：恰有可行候选 → 焦点非空；且⑥(招工人)人口不足时 gap>0 且可行 ----
        int origFood = (int)ai.resources.food;
        ai.resources.food = 99999;   // 粮裕免触发粮底线（测评分层焦点）
        ai.resources.gold = 100;
        var f19 = new FocusController(ai.id);
        f19.Update(ai, bcfg, ucfg, 1);
        bool focusSet = ai.focus != 0;
        bool topNonNone = f19.LastTop != UtilityAction.None;
        bool recruitOpen = ai.workerCount < 10 && ai.resources.gold > 0;   // ⑥ 可招（D345 防卡死关键路径）

        // ---- #4 常设底线覆盖评分（D322 优先级最高、不评分、即时、跳过防抖）----
        ai.focus = (int)UtilityAction.RecruitWorker;   // 人为评分态焦点⑥
        ai.resources.food = 0;                          // 触发粮底线
        var f4 = new FocusController(ai.id);
        f4.Update(ai, bcfg, ucfg, 2);
        bool bottomCovers = ai.focus == FocusController.FocusGranary;   // 被强制翻到 屯粮⑤
        ai.resources.food = origFood;

        // ---- #3 性格分化：同王国同局面，好战 vs 经济 → 评分 top 不同（D311 五轴线性乘入）----
        var origPers = ai.personality != null ? (float[])ai.personality.Clone() : null;
        if (ai.personality == null) ai.personality = new float[5];
        ai.personality[0] = 0.95f; ai.personality[1] = 0.3f;   // 好战模板
        UtilityAction belligTop = UtilityScorer.ScoreTop(ai, ucfg, ScriptStage.Survive);
        ai.personality[0] = 0.3f; ai.personality[1] = 0.95f;   // 经济模板
        UtilityAction econTop = UtilityScorer.ScoreTop(ai, ucfg, ScriptStage.Survive);
        if (origPers != null) ai.personality = origPers;        // 还原，不污染后续
        bool divergence = belligTop != econTop;

        c.Add($"#19焦点非空={(focusSet ? "OK" : "FAIL")}");
        c.Add($"#19评分非空={(topNonNone ? "OK" : "FAIL")}");
        c.Add($"#19⑥可招={(recruitOpen ? "OK" : "FAIL")}");
        c.Add($"#4底线覆盖评分={(bottomCovers ? "OK" : "FAIL")}");
        c.Add($"#3性格分化(好战{belligTop}vs经济{econTop})={(divergence ? "OK" : "FAIL")}");

        bool allPass = !c.Exists(x => x.Contains("FAIL"));
        Debug.Log("[2_17_9冒烟] " + string.Join(" ", c));
        Debug.Log($"[2_17_9冒烟] ===== {(allPass ? "ALL PASS" : "HAS FAIL")}（#19存活期可执行不卡死/#4底线覆盖评分/#3性格分化）=====");
    }

    private class SmokeCoroutineHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}