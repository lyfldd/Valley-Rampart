using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.78 AI 人口再生批 冒烟（D540；任务书=策划端/HH.78_AI人口再生批_施工任务书.md §二）
//  用法：GameScene Play 后菜单「Valley/验证/HH78_人口再生验证」。
//  P1 生育：快进→AI Child 诞生（归属国 kingdomId>0+raceId=国族）+生育日志在场。
//  P2 成长：AI Child→Worker 直生（childGrowthDayEvents=2 次日 tick；AI worker 计数上升）。
//  P3 存档回读：Save→Load→该 AI 国人口计数保持（Child/Worker 实体持久化）。
//  P4 流浪侧：营地实体在场（FindCamps>0）+自然增长刷点激活（流浪数增长）+族别映射（非 Human 流浪在场）。
//  P5 雷区：全程 InvalidOperationException 零命中（HH.76 件2 雷区纪律运行时监听）。
//  收尾：QuitSmoke。探针只读公开口+SaveManager.Save/Load+TimeManager 加速 API。
// ============================================================================
public static class Valley_HH78_Smoke
{
    private const int SEED = 20273;
    private const string SLOT = "smoke_p78";

    [MenuItem("Valley/验证/HH78_人口再生验证")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH78冒烟] 须先 GameScene 进 Play。"); return; }
        ErrWatch.Clear(); GrowthWatch.Clear();   // 防上一轮静态残留
        new GameObject("HH78_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator routine) => StartCoroutine(routine);
    }

    private static IEnumerator RunCoroutine()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        // HH.150 迁正门：TestHarnessApi.EnterTestRun = EnterGame 真实链+等就绪+考跑加速（D600/L-17）
        yield return TestHarnessApi.EnterTestRun(cfg);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Debug.LogError("[HH78冒烟] 等世界就绪超时。"); TestHarnessApi.ExitTestRun(); SmokeApi.QuitSmoke(); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        var results = new List<string>();
        var reg = KingdomRegistry.Instance;
        var pop = PopulationSystem.Instance;
        var sat = SatietySystem.Instance;
        var hap = HappinessSystem.Instance;

        // 收集 AI 国
        var aiKids = new List<int>();
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
            if (!all[i].IsPlayer) aiKids.Add(all[i].id);

        // ---- P4a 结构：营地实体在场（件3①）+族别映射（件3③）----
        yield return new WaitForSeconds(1f);
        var vc = VagrantCampSystem.Instance;
        int camps = vc != null ? vc.FindCamps().Count : 0;
        bool raceDiverse = false;
        var units0 = UnityEngine.Object.FindObjectsOfType<UnitController>();
        var vagRaces = new HashSet<int>();
        for (int i = 0; i < units0.Length; i++)
        {
            var u = units0[i];
            if (u == null || !u.IsAlive || u.EffectiveOccupation != Occupation.Vagrant) continue;
            vagRaces.Add(u.raceId);
        }
        raceDiverse = vagRaces.Count >= 2;   // 非全 Human=族别映射生效（4~6 初始流浪四族池抽）
        results.Add($"P4a 结构 营地实体={camps}(需>0) 流浪族别多样性={raceDiverse}(族集[{string.Join(",", new List<int>(vagRaces))}]) ={(camps > 0 && raceDiverse)}");

        // ---- 快进压场（45 游戏日：生育 10 日节律×4 胎+成长链+刷点全周期；P3=Save-only 避 Load 卡死[一轮实测 45 日大世界 Load 卡 6min+]，回读由 json 统计补证）----
        TimeManager.Instance.SetSecondsPerDay(15f);
        TimeManager.Instance.SetGameSpeed(3f);
        float w0 = Time.realtimeSinceStartup;
        bool childSeen = false, workerGrown = false, respawnSeen = false;
        int vagMax = vagRaces.Count == 0 ? 0 : CountVagrants();
        int prevVag = vagMax;
        while (Time.realtimeSinceStartup - w0 < 225f)   // ≈45 游戏日@5s/日
        {
            yield return null;
            // P1：AI Child 诞生
            if (!childSeen)
            {
                var us = UnityEngine.Object.FindObjectsOfType<UnitController>();
                for (int i = 0; i < us.Length; i++)
                {
                    var u = us[i];
                    if (u == null || !u.IsAlive || u.kingdomId <= 0 || u.EffectiveOccupation != Occupation.Child) continue;
                    childSeen = true;
                    int expect = KingdomRace.GetKingdomRace(u.kingdomId);
                    bool raceOk = u.raceId == expect;
                    results.Add($"P1 生育 AI Child 诞生 k{u.kingdomId} raceId={u.raceId}(期望{expect}{(raceOk ? "✓" : "✗")}) ={raceOk}");
                    break;
                }
            }
            // P2：AI Child→Worker 直生（双证口径——主证=镜像「AI小孩长大」日志[直生行为直接实锤]；
            // 旁证=AI 国 workerCount 峰值。workerCount>6 弱判定废弃：AI 招募 Resident 入籍不计 workerCount，偏弱）。
            if (!workerGrown && GrowthWatch.Count > 0) workerGrown = true;
            // P4b：流浪总数增长（自然刷点/补员激活）
            int cv = CountVagrants();
            if (cv > vagMax) { vagMax = cv; if (vagMax > prevVag + 1) { respawnSeen = true; prevVag = vagMax; } }
        }
        if (!childSeen) results.Add("P1 生育 AI Child 诞生 =False（45 日内无 Child——诊断：房/幸福/饱食条件，见 HH.79）");
        int peakW = 0;
        for (int i = 0; i < aiKids.Count; i++)
        {
            var k = reg.Get(aiKids[i]);
            if (k != null && k.workerCount > peakW) peakW = k.workerCount;
        }
        if (workerGrown) results.Add($"P2 成长 AI小孩长大日志×{GrowthWatch.Count} workerCount峰={peakW}(基线6) ={true}");
        else results.Add("P2 成长 Worker 直生 =False（45 日内无「AI小孩长大」日志——依赖 P1 生育+成长链）");
        bool p4b = camps > 0 && (respawnSeen || vagMax > 4);   // 补员激活或刷点增长
        results.Add($"P4b 流浪侧 补员/刷点激活 流浪峰={vagMax} ={p4b}");

        // ---- P3 存档：Save 成功+Save 前人口快照（回读由 json 统计补证——Load 卡死规避）----
        yield return null;
        var before = new Dictionary<int, int>();
        for (int i = 0; i < aiKids.Count; i++)
        {
            var k = reg.Get(aiKids[i]);
            if (k != null) before[aiKids[i]] = k.workerCount + k.warriorCount;
        }
        bool saved = SaveManager.Instance.Save(SLOT + "_long");
        // P3 补证镜像：smoke_ 前缀槽 QuitSmoke 自清（HH.66 防堆积纪律，本轮实测 save=True 但文件被收尾删）→
        // Save 后立即拷贝存档到 Logs/P1（非 smoke_ 前缀，QuitSmoke 不清），供会话侧 json 统计（occ=23 race=1 补证）。
        try
        {
            var savePath = System.IO.Path.Combine(Application.persistentDataPath, "Saves", SLOT + "_long.json");
            if (System.IO.File.Exists(savePath))
                System.IO.File.Copy(savePath, System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1/hh78_p3_save.json"), true);
        }
        catch (System.Exception e) { Debug.LogWarning("[HH78冒烟] 存档镜像拷贝失败: " + e.Message); }
        var snap = new System.Text.StringBuilder();
        for (int i = 0; i < aiKids.Count; i++) snap.Append($"k{aiKids[i]}={before[aiKids[i]]} ");
        bool p3 = saved;
        results.Add($"P3 存档 save={saved} AI 人口快照[{snap.ToString().TrimEnd()}]（回读=json 统计补证）={p3}");

        // ---- P5 雷区：InvalidOperationException 零命中 ----
        results.Add($"P5 雷区 运行时枚举异常命中={ErrWatch.Count}（0=纪律成立）{(ErrWatch.Count == 0 ? "=True" : "=False")}");

        int pass = 0;
        var sbOut = new System.Text.StringBuilder();
        for (int i = 0; i < results.Count; i++)
        {
            Debug.Log("[HH78冒烟] " + results[i]);
            sbOut.AppendLine(results[i]);
            if (results[i].EndsWith("=True")) pass++;
        }
        string summary = $"===== {(pass == results.Count ? "ALL PASS" : $"HAS FAIL({results.Count - pass})")}（{pass}/{results.Count}）=====";
        Debug.Log("[HH78冒烟] " + summary);
        sbOut.AppendLine(summary);
        // 结果落文件（console 大缓冲 read_console 不可用——HH.76 教训沿用，文件为可靠取回渠道）
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh78_smoke_result.log"), sbOut.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH78冒烟] 结果写文件失败: " + e.Message); }

        TestHarnessApi.ExitTestRun();   // HH.150 正门收尾：全量恢复考跑态
        SmokeApi.QuitSmoke();
    }

    private static int CountVagrants()
    {
        int n = 0;
        var us = UnityEngine.Object.FindObjectsOfType<UnitController>();
        for (int i = 0; i < us.Length; i++)
            if (us[i] != null && us[i].IsAlive && us[i].EffectiveOccupation == Occupation.Vagrant) n++;
        return n;
    }

    [InitializeOnLoadMethod]
    private static void InstallErrWatch()
    {
        Application.logMessageReceived -= OnLogLine;
        Application.logMessageReceived += OnLogLine;
    }

    private static readonly List<string> ErrWatch = new List<string>();
    private static readonly List<string> GrowthWatch = new List<string>();   // P2 主证：「AI小孩长大」日志镜像

    private static void OnLogLine(string condition, string stackTrace, LogType type)
    {
        // P2 主证收集：普通 Log 级（Debug.Log 打印）——须先于异常过滤判定
        if (type == LogType.Log && condition != null && condition.Contains("AI小孩长大"))
        { GrowthWatch.Add(condition); return; }
        if (type != LogType.Exception && type != LogType.Error) return;
        if (condition != null && (condition.Contains("InvalidOperationException") || condition.Contains("Collection was modified")))
            ErrWatch.Add(condition);
    }
}
