using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_17 步骤10 兵力目标冒烟 #5（D348，路线②验收金线）
//  判据（2_17.md #5）：注入威胁（邻国军队逼近边境）→ 兵力目标上调 → ⑦招战士分数上升。
//  纯逻辑探针，零世界耦合（直接测 D348 纯函数 + ⑦缺口分数），可编辑态菜单运行。
//  用法：菜单「Valley/验证/2_17_步骤10_兵力目标(D348)」——无需 Play。
//  p05_威胁上调: 邻国兵力 ↑ → D348Target 严格不降（威胁→目标单调，分母 max(己方,1)）
//  p07_软帽:      目标 ≤ 2+工人数（软帽 clamp），下限 ≥ floor
//  p09_⑦分数上升: 同己方兵力下，威胁更大 → WarriorGapScore 严格上升（兵力缺口拉大）
//  收口：不改产品代码。
// ============================================================================
public static class Valley2_17_Smoke_5
{
    private const int FLOOR = 2;

    [MenuItem("Valley/验证/2_17_步骤10_兵力目标(D348)")]
    public static void RunFromMenu()
    {
        var cfg = KingdomBrain.LoadConfig();
        bool pass = true;
        var sb = new System.Text.StringBuilder();
        pass &= ThreatRaises( cfg, sb);
        pass &= SoftCapAndFloor(cfg, sb);
        pass &= WarriorGapRises(cfg, sb);
        Debug.Log($"[2_17_5冒烟] {sb}");
        Debug.Log($"[2_17_5冒烟] ===== {(pass ? "ALL PASS" : "HAS FAIL")}（威胁上调→目标升→⑦分数升，D348）=====");
    }

    // ---- ⑤ 邻国兵力威胁 → 兵力目标上调 ----
    private static bool ThreatRaises(KingdomBrainConfig cfg, System.Text.StringBuilder sb)
    {
        bool ok = true;
        string prev = null;
        // 固定己方：战士 4、工人 10、扩张期；威胁兵力 0..60
        for (int nei = 0; nei <= 60; nei += 10)
        {
            int t = UtilityScorer.D348Target(4, 10, nei, cfg.militaryExpandStageFactor, cfg);
            if (prev != null && t < int.Parse(prev)) ok = false;   // 威胁增目标不降（单调）
            prev = t.ToString();
        }
        sb.Append($"威胁上调={(ok ? "OK" : "FAIL")}(4战10工扩张,威胁0→60) ");
        return ok;
    }

    // ---- 软帽 clamp：目标 ≤ 2+工人数，且 ≥ floor ----
    private static bool SoftCapAndFloor(KingdomBrainConfig cfg, System.Text.StringBuilder sb)
    {
        bool ok = true;
        // 大威胁压到软帽：1 战 2 工，威胁 1000 → 被 2+工人数=4 压住
        int cap = UtilityScorer.D348Target(1, 2, 1000f, cfg.militaryStageFactor, cfg);
        if (cap > FLOOR + 2) ok = false;            // 软帽 2+2=4
        // 大工人授权更多兵力：6 工强威胁 → 上限 2+6=8
        int cap6 = UtilityScorer.D348Target(1, 6, 1000f, cfg.militaryStageFactor, cfg);
        if (cap6 > FLOOR + 6) ok = false;
        // 下限：零威胁也 ≥ floor
        int low = UtilityScorer.D348Target(4, 10, 0f, 0, cfg);
        if (low < FLOOR) ok = false;
        sb.Append($"软帽clamp={(ok ? "OK" : "FAIL")}(≤2+工,≥floor) ");
        return ok;
    }

    // ---- ⑦ 战士缺口分数：威胁更大 → 缺口分数严格升（同己方兵力下） ----
    private static bool WarriorGapRises(KingdomBrainConfig cfg, System.Text.StringBuilder sb)
    {
        // worker=20（软帽 22）保证 威胁0→30 全程不触软帽单调升；己方战士 4、扩张期
        bool ok = true;
        float prevScore = -1f;
        for (int nei = 0; nei <= 30; nei += 10)
        {
            int t = UtilityScorer.D348Target(4, 20, nei, cfg.militaryExpandStageFactor, cfg);
            float s = UtilityScorer.WarriorGapScore(4, t);   // 己方 4 战 vs 目标 t
            if (s <= prevScore) ok = false;          // 威胁增（未触软帽）→ 缺口分数不降
            prevScore = s;
        }
        sb.Append($"⑦分数随威胁升={(ok ? "OK" : "FAIL")}(4战20工,威胁0→30) ");
        return ok;
    }
}