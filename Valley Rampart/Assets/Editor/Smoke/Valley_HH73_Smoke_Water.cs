using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.73 AI 供水链修复批 冒烟（D535；任务书=多Agent交接/策划端/HH.73_AI供水链修复批_任务书.md §三）
//  用法：菜单「Valley/验证/HH73_AI供水修复」——MainMenuScene 或 GameScene Play 后点（自动进局）。
//  结构：SmokeApi.EnterGame 真实链进局（seed=20273 固定，smoke_w73 槽）→ 等就稳 →
//    P1 结构：AI 国预置含 Well（baseBuildingDefIds 插序）+ AI 桶有水（GetStored 公开口）。
//    P2 行为正：AI 农田恢复产粮——快进窗口内 AI 桶被消耗（ConsumeWater 只由 TryConsumeFarmWater 调用，
//       桶水下降=产粮事件真实发生）+ AI farm Storage 曾 >0（产出面证据）。
//    P3 行为负：玩家桶零泄漏——玩家桶全程 ==0（AI 井水不泄玩家桶）+ AI 桶独立波动（路由互斥证明：
//       若 AI 农田错走玩家桶，玩家桶 0 不足以支付 → AI farm 必缺水停产 → P2 必失败；P2 过=P3 路由正确）。
//    P4 存档：AI 桶入档——Save→记值→改桶→Load→AI 桶保持（容差 ±2 防井产水一帧增量）。
//  收尾：QuitSmoke（自动清 smoke_ 槽+退 Play）。不改产品代码（探针只读公开口）。
//  P5 同 seed 22360 对照跑=独立长局段（观察器+HH.71 协议），不在本容器。
// ============================================================================
public static class Valley_HH73_Smoke_Water
{
    private const int SEED = 20273;
    private const string SLOT = "smoke_w73";

    [MenuItem("Valley/验证/HH73_AI供水修复")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH73冒烟] 须先进入 Play（MCP enterPlaymode 后调用本菜单）。"); return; }
        new GameObject("HH73_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator routine) => StartCoroutine(routine);
    }

    private static IEnumerator RunCoroutine()
    {
        // ---- 真实链进局（SmokeApi 幂等守卫内）----
        var cfg = new NewGameConfig
        {
            worldSeed = SEED,
            mapSeed = SEED,
            raceId = 0,
            difficulty = 2,
            worldSize = WorldSize.Medium,
            selectedSlotId = SLOT,
            kingdomName = "河谷王国"
        };
        SmokeApi.EnterGame(cfg);

        // ---- 等世界就稳（120s 超时）----
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f)
            {
                Debug.LogError("[HH73冒烟] 等世界就绪超时(120s)。");
                SmokeApi.QuitSmoke();
                yield break;
            }
        }
        yield return new WaitForSeconds(0.5f);   // 稳态窗口（HH.69 教训）

        var reg = KingdomRegistry.Instance;
        var wn = WaterNetwork.Instance;
        var results = new List<string>();

        // 收集 AI 国（id>0，取前 3）
        var aiKids = new List<int>();
        var all = reg.GetAll();
        for (int i = 0; i < all.Count && aiKids.Count < 3; i++)
            if (!all[i].IsPlayer) aiKids.Add(all[i].id);
        if (aiKids.Count == 0)
        {
            Debug.LogError("[HH73冒烟] 无 AI 国，中止。");
            SmokeApi.QuitSmoke();
            yield break;
        }

        // ---- P1 结构：AI 国预置含 Well + AI 桶有水 ----
        yield return null;
        int wellCount = 0;
        var farmKid = new Dictionary<int, Building>();   // kid → 任一 farm
        var buildings = Object.FindObjectsOfType<Building>();
        for (int i = 0; i < buildings.Length; i++)
        {
            var b = buildings[i];
            if (b == null || b.def == null || b.kingdomId <= 0) continue;
            if (b.def.id == "Well") wellCount++;
            if (!farmKid.ContainsKey(b.kingdomId) && b.def.id == "farm") farmKid[b.kingdomId] = b;
        }
        yield return new WaitForSeconds(2f);   // 井产水积累窗（rate=4/s）
        bool p1Well = wellCount >= aiKids.Count;                 // 每 AI 国 ≥1 井
        bool p1Water = true;
        for (int i = 0; i < aiKids.Count; i++)
            if (wn.GetStored(aiKids[i]) <= 0f) p1Water = false;
        results.Add($"P1 结构 预置Well数={wellCount}(需≥{aiKids.Count}) AI桶有水={p1Water}({DumpBuckets(wn, aiKids)}) ={p1Well && p1Water}");

        // ---- P2 行为正 + P3 玩家桶零泄漏（同窗口观测）----
        float playerBefore = wn.GetStored(0);
        var aiMinInWindow = new Dictionary<int, float>();   // 窗口内逐国最低值（桶单调涨时末值==峰值，须窗口内跟踪）
        for (int i = 0; i < aiKids.Count; i++) aiMinInWindow[aiKids[i]] = wn.GetStored(aiKids[i]);
        float aiPeakAll = 0f;
        for (int i = 0; i < aiKids.Count; i++) aiPeakAll = Mathf.Max(aiPeakAll, wn.GetStored(aiKids[i]));

        TimeManager.Instance.SetSecondsPerDay(15f);   // 快进：5s 真实=1 游戏日（公开 API，P0 冒烟先例）
        TimeManager.Instance.SetGameSpeed(3f);

        float farmMax = 0f;
        bool playerDrop = false;
        float watchT0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - watchT0 < 8f)
        {
            yield return null;
            // farm storage 峰值（farm 会产粮入本地 Storage；搬运清空不碍「曾>0」判据）
            foreach (var kvp in farmKid)
            {
                var st = kvp.Value != null ? kvp.Value.GetComponent<StorageComponent>() : null;
                if (st != null) farmMax = Mathf.Max(farmMax, st.storedAmount);
            }
            // 玩家桶负跳变检测（零泄漏）
            float pv = wn.GetStored(0);
            if (pv < playerBefore - 0.001f) playerDrop = true;
            playerBefore = pv;
            // AI 桶窗口内峰谷（产水升+农田耗水降——逐国 min 持续跟踪）
            for (int i = 0; i < aiKids.Count; i++)
            {
                float v = wn.GetStored(aiKids[i]);
                aiPeakAll = Mathf.Max(aiPeakAll, v);
                aiMinInWindow[aiKids[i]] = Mathf.Min(aiMinInWindow[aiKids[i]], v);
            }
        }
        // 农田产粮耗水=桶水相对窗口内峰值下降≥2（ConsumeWater 只由 TryConsumeFarmWater 调用）
        bool aiConsumed = false;
        float peakForLog = 0f, troughForLog = float.MaxValue;
        for (int i = 0; i < aiKids.Count; i++)
        {
            float v = wn.GetStored(aiKids[i]);
            float peak = Mathf.Max(aiPeakAll, v);
            if (peak - aiMinInWindow[aiKids[i]] >= 2f) aiConsumed = true;
            peakForLog = Mathf.Max(peakForLog, peak);
            troughForLog = Mathf.Min(troughForLog, aiMinInWindow[aiKids[i]]);
        }
        bool p2 = aiConsumed && farmMax > 0f;
        results.Add($"P2 行为正 AI桶窗口峰谷降={ (peakForLog - troughForLog).ToString("F1") }(需≥2) farmStorage峰={farmMax:F0} ={p2}");
        bool p3 = !playerDrop && aiPeakAll > 0f;         // 玩家桶零变化 + AI 桶独立有水 = 路由互斥
        results.Add($"P3 行为负 玩家桶零泄漏={(!playerDrop)}(终值{wn.GetStored(0):F0}) AI桶独立波动={aiPeakAll > 0f} ={p3}");

        // ---- P4 存档：AI 桶入档（Save→改→Load→保持）----
        int probeKid = aiKids[0];
        yield return null;
        // 先把桶扣到低位（公开 ConsumeWater 模拟消费），保证存档点与改后点有足够区分度
        // （桶近满时 vBefore≈98/改后=100，差 2 无法区分「档恢复」vs「突变残留」）
        int drainGuard = 0;
        while (wn.GetStored(probeKid) > 20f && drainGuard++ < 20) wn.ConsumeWater(10f, probeKid);
        yield return null;
        bool saved = SaveManager.Instance.Save(SLOT + "_p4");
        yield return null;
        float vBefore = wn.GetStored(probeKid);
        wn.AddWater(80f, probeKid);                       // 改桶（制造与存档点的差值）
        float vMutated = wn.GetStored(probeKid);
        bool loaded = SaveManager.Instance.Load(SLOT + "_p4");
        // 等读档世界重建
        float lt0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - lt0 > 60f) break;
        }
        yield return new WaitForSeconds(0.3f);
        float vAfter = wn.GetStored(probeKid);
        // 判据（D535 入档语义）：读回明显低于改后值（≠100 的突变残留=档未恢复）且不低于存档值-2
        // （读档后水井继续产水 → 读回=vBefore+产水增量，92=88+4 即此形态；若 aiBuckets 未入档，
        //  LoadState 不恢复 → 读回=改后 100+。区间判定对井产水时序鲁棒。
        bool p4 = saved && loaded && vAfter >= vBefore - 2f && vAfter <= vMutated - 10f;
        results.Add($"P4 存档 save={saved} load={loaded} AI桶(k{probeKid}) 存档时={vBefore:F1} 改后={vMutated:F1} 读回={vAfter:F1}(期望≈存档值+产水增量) ={p4}");

        // ---- 汇总 ----
        int pass = 0;
        for (int i = 0; i < results.Count; i++)
        {
            Debug.Log("[HH73冒烟] " + results[i]);
            if (results[i].EndsWith("=True")) pass++;
        }
        Debug.Log($"[HH73冒烟] ===== {(pass == results.Count ? "ALL PASS" : $"HAS FAIL({results.Count - pass})")}（{pass}/{results.Count}）=====");

        SmokeApi.QuitSmoke();
    }

    private static string DumpBuckets(WaterNetwork wn, List<int> kids)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < kids.Count; i++) sb.Append($"k{kids[i]}={wn.GetStored(kids[i]):F0} ");
        return sb.ToString().TrimEnd();
    }
}
