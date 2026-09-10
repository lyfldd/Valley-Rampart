using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Reflection;

// ============================================================================
//  HH.159 T11.6 AI 弹话族化 进局验证（HH.153 件6；Editor-only 冒烟容器）
//  入口=HH.92 测试环境正门 TestHarnessApi.EnterTestRun（L-17）；收尾 ExitTestRun + QuitSmoke。
//
//  验证口径（HH.153 §五 验收硬条款 + M1/M2/M4）：
//   ① 族池句真的出现（talkRaceChance=0.4 生效：族池句与职业池句双侧在场，非恒职业池/非恒族池）——
//      多句采样取存在性证据（L-14），直调产品口 PickTalkLine()（NPCBrain 空闲弹话与点击对话共用本口子）。
//   ② AI 王国 NPC raceId 抽查（非全兜底 Human；与所属王国种族一致）——D467/Q10-M2 实测确认，大量兜底=列报。
//  补充：四族族池可达性（内存态 raceId 覆盖 0~3，资产零污染），逐族命中计数。
// ============================================================================
public static class Valley_HH153_TalkProbe
{
    private const int SEED = 21153;
    private const string SLOT = "probe_hh153";
    private const string TAG = "[HH153弹话]";
    private const int SAMPLE_PER_UNIT = 200;
    private const int FORCED_SAMPLE = 300;
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();

    [MenuItem("Valley/验证/HH153_AI弹话族化进局验证")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError(TAG + " 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("HH153_TalkHost").AddComponent<ProbeHost>();
        Application.logMessageReceived += host.Catch;
        host.Host(Coroutine());
    }

    private class ProbeHost : MonoBehaviour
    {
        public void Host(IEnumerator r) => StartCoroutine(r);
        public void Catch(string cond, string stack, LogType t)
        {
            if (t == LogType.Exception) Log(TAG + " EXCEPTION: " + cond + "\n" + stack, false);
        }
    }

    private static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.LogWarning(TAG + " " + (ok ? "✓ " : "✗ ") + msg);
    }

    private static MethodInfo _miRacePool;
    private static HashSet<string> RacePoolOf(UnitController u, int race)
    {
        if (_miRacePool == null)
            _miRacePool = typeof(UnitController).GetMethod("GetRaceTalkPool", BindingFlags.NonPublic | BindingFlags.Instance);
        var arr = _miRacePool != null ? _miRacePool.Invoke(u, new object[] { race }) as string[] : null;
        return arr != null ? new HashSet<string>(arr) : null;
    }

    private static bool IsNormal(UnitController u)
        => u != null && u.IsAlive && u.Satiety >= 30 && u.MaxHp > 0 && (float)u.CurrentHp / u.MaxHp >= 0.4f;

    private static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, raceId = 1, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 15f);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || UnitRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(); yield break; }
        }
        yield return new WaitForSeconds(0.5f);
        Log($"正门进局 seed={SEED}（15x）；talkRaceChance(SO)={ReadTalkRaceChance()}", true);

        // ===== ② AI 王国 NPC raceId 抽查（与所属王国种族一致 / 非恒定）=====
        var reg = KingdomRegistry.Instance;
        int aiUnits = 0, matched = 0, fallbackHuman = 0, nonHumanKingdoms = 0;
        var observedRaces = new HashSet<int>();
        var perKingdom = new List<string>();
        foreach (var k in reg.GetAll())
        {
            if (k == null || k.IsPlayer) continue;
            int kRace = KingdomRace.GetKingdomRace(k.id);
            int n = 0, m = 0, fb = 0;
            foreach (var u in UnitRegistry.Instance.GetAllUnits())
            {
                if (u == null || !u.IsAlive || u.kingdomId != k.id) continue;
                n++;
                observedRaces.Add(u.raceId);
                if (u.raceId == kRace) m++;
                if (u.raceId == RaceIds.Human && kRace != RaceIds.Human) { fb++; fallbackHuman++; }
            }
            if (kRace != RaceIds.Human) nonHumanKingdoms++;
            aiUnits += n; matched += m;
            perKingdom.Add($"k{k.id}(族{kRace}) 单位={n} 匹配={m}" + (fb > 0 ? $" ★Human兜底={fb}" : ""));
        }
        Log($"② AI 王国 raceId 抽查：AI国={perKingdom.Count} 单位={aiUnits} 与国族一致={matched} " +
            $"非人类国={nonHumanKingdoms} Human兜底={fallbackHuman}｜逐国[{string.Join(" | ", perKingdom)}]｜观察到raceIds=[{string.Join(",", observedRaces)}]",
            aiUnits > 0 && fallbackHuman == 0);
        Log($"②b raceId 非恒定值：观察到 {observedRaces.Count} 种" +
            (observedRaces.Count == 1 ? $"（本局单一；非人类国={nonHumanKingdoms}——若均为 Human 则单一属正常，有非人类国仍单一=兜底口）" : ""),
            observedRaces.Count > 1 || nonHumanKingdoms == 0);

        // ===== ① 族池句真的出现（多采样：AI NPC 直调 PickTalkLine）=====
        var samples = new List<UnitController>();
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
            if (u != null && u.IsAlive && u.kingdomId > 0 && u.GetComponent<NPCBrain>() != null && IsNormal(u))
            { samples.Add(u); if (samples.Count >= 5) break; }
        if (samples.Count == 0)   // 兜底：无 NPCBrain 则任取正常态 AI 单位
            foreach (var u in UnitRegistry.Instance.GetAllUnits())
                if (u != null && u.IsAlive && u.kingdomId > 0 && IsNormal(u)) { samples.Add(u); if (samples.Count >= 5) break; }

        int raceHit = 0, occHit = 0, total = 0;
        var perRaceHit = new Dictionary<int, int>();
        var perRaceTot = new Dictionary<int, int>();
        foreach (var u in samples)
        {
            var pool = RacePoolOf(u, u.raceId);
            for (int i = 0; i < SAMPLE_PER_UNIT; i++)
            {
                string line = u.PickTalkLine();
                total++;
                perRaceTot[u.raceId] = perRaceTot.TryGetValue(u.raceId, out var c) ? c + 1 : 1;
                if (pool != null && pool.Contains(line)) { raceHit++; perRaceHit[u.raceId] = perRaceHit.TryGetValue(u.raceId, out var r) ? r + 1 : 1; }
                else occHit++;
            }
        }
        double ratio = total > 0 ? (double)raceHit / total : 0;
        Log($"① 族池/职业池多采样：样本单位={samples.Count} 样本数={total} 族池句命中={raceHit} 职业池句命中={occHit} " +
            $"族池占比={ratio:F3}（期望≈0.4，双侧在场=非恒族池/非恒职业池）",
            total > 0 && raceHit > 0 && occHit > 0 && ratio > 0.2 && ratio < 0.6);

        // ===== 补充：四族族池可达性（内存态 raceId 覆盖 0~3；资产零污染，单位销毁即复原）=====
        var u0 = samples.Count > 0 ? samples[0] : null;
        if (u0 != null)
        {
            int savedRace = u0.raceId;
            var parts = new List<string>();
            bool allReach = true;
            for (int r = 0; r < 4; r++)
            {
                u0.raceId = r;
                var pool = RacePoolOf(u0, r);
                int hit = 0;
                for (int i = 0; i < FORCED_SAMPLE; i++)
                {
                    string line = u0.PickTalkLine();
                    if (pool != null && pool.Contains(line)) hit++;
                }
                parts.Add($"r{r}:命中{hit}/{FORCED_SAMPLE}");
                if (hit == 0) allReach = false;
            }
            u0.raceId = savedRace;
            Log($"补充 四族族池可达性（内存态 raceId 覆盖，单位#{u0.npcId} 已复原）：[{string.Join(" ", parts)}]",
                allReach);
        }
        else Log("补充 四族覆盖：无可用样本单位", false);

        Finish();
    }

    private static float ReadTalkRaceChance()
    {
        var cfg = Resources.Load<AttentionTuningConfig>("Config/AttentionTuningConfig");
        return cfg != null ? cfg.talkRaceChance : -1f;
    }

    private static void Finish()
    {
        _log.AppendLine("===== HH.153 T11.6 弹话进局验证收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh153_talk_probe.log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError(TAG + " 日志写盘失败: " + e.Message); }
        Debug.LogWarning(TAG + " ★ 收工 PASS=" + _pass + " FAIL=" + _fail);
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}
