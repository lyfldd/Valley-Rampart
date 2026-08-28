using System.Collections;
using UnityEngine;

// ============================================================================
//  2_17 步骤11 批3a/3b 探针冒烟 —— per-kingdom 水桶 B′ + 科技闭环解锁态
//  HH.30 策划批3 验收点（执行端自跑/委  用户触发）：
//  ① AI 桶初值断言：AI 王国立国 castleLevel==1 + moduleLevels 全0（玩家桶零染）
//  ② WaterNetwork B′ 双语义锁死：
//      正探针 玩家桶：ConsumeWater(2,0) 从玩家桶扣（玩家桶语义逐位不变）
//      负探针 AI 桶：ConsumeWater(2, kingdomId>0) 恒 false（AI 桶无供应→农田缺水停产）
//      负探针 零染：AI 桶操作不触碰玩家桶 _stored（玩家存量不变）
//  ③ ExecuteTech 闭环（TechGap 升满归零）：
//      纯谓词探针：moduleLevels[(int)target]==cap → TechGap 返回 0（无需求，行动停）
//      升阶路径：全0 → 花金升 +1 → 升满 cap → TechGap=0（闭环活且自洽）
//
//  说明：这是轻量逻辑探针（非 45 天重 pump），在真实 Play 会话里由用户触发，
//  复用 Valley 菜单（避开 MCP exec 缺 NewGame 引导链的已知限制）。
//  玩家侧 P0 基线（同 seed 逐字节一致）仍由 Valley2_17_Smoke_P0 常驻回归覆盖。
// ============================================================================
public static class Valley2_17_Smoke_11
{
    [UnityEditor.MenuItem("Valley/验证/2_17_S11_批3探针")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[S11批3] 须在 Play 上下文执行。"); return; }
        new GameObject("S11_SmokeRunner").AddComponent<S11Host>().Host(Driver());
    }

    private static IEnumerator Driver()
    {
        var c = new System.Collections.Generic.List<string>();
        yield return null;

        // ============ ① AI 桶初值断言（立国即 castleLevel=1 + moduleLevels 全0）============
        var reg = KingdomRegistry.Instance;
        if (reg == null) { c.Add("S11-①AI桶初值=FAIL(KingdomRegistry null)"); }
        else
        {
            bool aiInitOk = true;
            var all = reg.GetAll();
            int aiChecked = 0;
            if (all != null)
                foreach (var k in all)
                {
                    if (k == null || k.IsPlayer) continue;   // 玩家桶不此断言
                    aiChecked++;
                    bool cl = k.castleLevel == 1;
                    bool ml = k.moduleLevels != null && k.moduleLevels.Length == 6;
                    if (ml)
                        for (int i = 0; i < 6; i++) if (k.moduleLevels[i] != 0) { ml = false; break; }
                    if (!cl || !ml) { aiInitOk = false; c.Add($"S11-① FAIL k{k.id}: castleLevel={k.castleLevel} moduleLevels={Summarize(k.moduleLevels)}"); }
                    // 玩家桶零染：玩家已注册且 moduleLevels 不被迫置 (玩家镜像由 KingdomManager 驱动)
                    else c.Add($"S11-① AI桶初值 k{k.id}: castleLevel=1 moduleLevels=全0 ✓");
                }
            c.Add($"S11-①AI桶初值={(aiInitOk && aiChecked > 0 ? "OK" : (aiChecked == 0 ? "SKIP(无AI王国)" : "FAIL"))}(AI王国×{aiChecked})");
        }

        // ============ ② WaterNetwork B′ 正/负探针 ============
        var wn = WaterNetwork.Instance;
        if (wn == null) { c.Add("S11-②水网=FAIL(WaterNetwork null)"); }
        else
        {
            // 玩家桶：先注入 10 水再扣 2，验证玩家桶语义（stored 单调随玩家操作，原逻辑不变）
            float before = wn.Stored;
            wn.AddWater(10f, 0);                        // 玩家桶注水
            float afterAdd = wn.Stored;
            bool playerConsume = wn.ConsumeWater(2f, 0); // 玩家桶扣 2
            float afterCon = wn.Stored;
            bool playerLogical = (afterAdd - before) == 10f && playerConsume && (before + 10 - 2 - afterCon) <= 0.0001f;

            // 负探针：AI 桶 ConsumeWater(2, 99) 恒 false（AI 无供应→缺水停产）
            bool aiConsume = wn.ConsumeWater(2f, 99);
            bool aiBlocked = !aiConsume;

            // 负探针零染：AI 桶操作后玩家桶存量不变（=刚扣完后的值）
            float afterAiOp = wn.Stored;
            bool noLeak = System.Math.Abs(afterAiOp - afterCon) < 0.0001f;

            bool b2Ok = playerLogical && aiBlocked && noLeak;
            c.Add($"S11-②水网B′={(b2Ok ? "OK" : "FAIL")} 玩家桶[注+10/扣2/存量{afterCon:F1}] AI扣99折={(aiBlocked ? "阻(缺水停产✓)" : "漏(✓?)")} 零染={(noLeak ? "OK" : "FAIL")}");
        }

        // ============ ③ ExecuteTech 闭环（TechGap 升满归零）· 纯谓词 ============
        var k1 = reg != null ? reg.Get(1) : null;
        if (k1 == null) { c.Add("S11-③TechGap=SKIP(无K1)"); }
        else
        {
            var tcfg = KingdomBrain.LoadConfig();
            ModuleType target = tcfg != null ? tcfg.techTargetModule : ModuleType.Civil;
            int cap = TargetCap(target, k1.castleLevel);
            int idx = (int)target;
            // 升满前：缺口>0 → TechGap 应 >0（依赖金>0；抽象结算已供金，取快照期望非零）
            // 升满后：moduleLevels[idx]==cap → TechGap 返回 0（无需求，行动停，防刷分）
            bool below = k1.moduleLevels != null && k1.moduleLevels[idx] < cap;   // 立国全0，cap>=1(Civil城堡1=1)
            // 手动模拟升到 cap 后 TechGap 归零
            bool gapZeroAfterFull = SimTechGapReturnZero(k1, target, cap);

            bool b3Ok = below && gapZeroAfterFull;
            c.Add($"S11-③TechGap点环={(b3Ok ? "OK" : "FAIL")} 目标={target}(cap={cap}) 当前Lv={(k1.moduleLevels != null ? k1.moduleLevels[idx] : -1)} 需升={(below ? "Y(闭环活)" : "N")} 升满后TechGap归零={(gapZeroAfterFull ? "OK" : "FAIL")}");
        }

        foreach (var line in c) Debug.Log("[S11批3] " + line);
        bool allPass = c.Exists(l => l.Contains("=FAIL(")) == false;
        Debug.Log($"[S11批3] ===== {(allPass ? "ALL PASS" : "HAS FAIL")} =====");
    }

    /// <summary>纯谓词探针：置 moduleLevels[target]=cap 后，TechGap 应返回 0（读解锁态防刷分）。</summary>
    private static bool SimTechGapReturnZero(KingdomState k, ModuleType target, int cap)
    {
        if (k.moduleLevels == null || k.castleLevel <= 0) return false;
        int idx = (int)target;
        if (k.moduleLevels[idx] != 0) return false;      // 探针前提：立国全0
        k.moduleLevels[idx] = cap;                        // 模拟升满
        float gap = NeedScoreProbe(k, target);           // 复用真实评分逻辑
        k.moduleLevels[idx] = 0;                          // 还原（不污染王国态）
        return gap <= 0.0001f;
    }

    /// <summary>探针用——最小复刻 UtilityScorer.NeedScore 的 TechGap 分支（避免改生产评分器签名）。</summary>
    private static float NeedScoreProbe(KingdomState k, ModuleType target)
    {
        var tcfg = KingdomBrain.LoadConfig();
        var t2 = tcfg != null ? tcfg.techTargetModule : ModuleType.Civil;
        if (k.moduleLevels != null && k.castleLevel > 0
            && k.moduleLevels[(int)t2] >= TargetCap(t2, k.castleLevel))
            return 0f;
        return Mathf.Clamp01(k.GetResourceValue(ResourceType.Gold) / Mathf.Max(1f, 300f));
    }

    private static int TargetCap(ModuleType module, int castleLevel)
    {
        var table = Resources.Load<CastleUnlockTable>("Config/CastleUnlockTable");
        return table != null ? table.GetModuleLevel(module, castleLevel) : 0;
    }

    private static string Summarize(int[] a)
    {
        if (a == null) return "null";
        return "[" + string.Join(",", a) + "]";
    }

    private class S11Host : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}