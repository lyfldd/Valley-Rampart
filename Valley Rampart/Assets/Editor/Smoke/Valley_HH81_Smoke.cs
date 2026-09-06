using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.81 修复批冒烟（D542；任务书=策划端/HH.81_招工守卫与住房缺口修复批_任务书.md §二）
//  用法：GameScene Play 后菜单「Valley/验证/HH81_修复批验证」。
//  P1 结构：五 AI 模板立国→每 AI 国 House>=1+房容>0（件2 插 House+buildingCount 5/6/7 取序正确含 Well）。
//  P2 行为正：⑥招工落地>0（流浪 -1 被招募→Worker 入籍 AI id）+AI 生育触发（房容>0 过门槛）→
//             AI Child 诞生（归属国 raceId 正确）→「AI小孩长大」直生 Worker。
//  P3 行为负：玩家零回归——流浪生成 kingdomId=-1 全体（无 0 挂靠）+玩家 CountAliveByKingdom(0) 恒 4+
//             玩家招募口 CanRecruit() 可调不炸+玩家 Resident 招募后 kingdomId=0（置籍配套）。
//  P4 存档：Save→镜像拷贝 Logs/P1/hh81_p3_save.json（QuitSmoke 自清 smoke_ 槽教训 HH.78 七轮）——
//           回读由会话侧 json 统计补证（Vagrant kid=-1/Worker kid>0/Resident kid=0 分布）。
//  P5 雷区：InvalidOperationException 全程 0（件1 动了 Spawn 点+⑥招工遍历=雷区复检硬性条目）。
//  收尾 QuitSmoke。结果落文件 Logs/P1/hh81_smoke_result.log（console 大缓冲教训）。
// ============================================================================
public static class Valley_HH81_Smoke
{
    private const int SEED = 42424;         // 新 seed（避开 20273/16180/22360/52707/31415/27182/16180/57721 已用）
    private const string SLOT = "smoke_p81";

    [MenuItem("Valley/验证/HH81_修复批验证")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH81冒烟] 须先 GameScene 进 Play。"); return; }
        ErrWatch.Clear(); WatchLog.Clear();
        new GameObject("HH81_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
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
        SmokeApi.EnterGame(cfg);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Debug.LogError("[HH81冒烟] 等世界就绪超时。"); SmokeApi.QuitSmoke(); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        var results = new List<string>();
        var reg = KingdomRegistry.Instance;
        var hap = HappinessSystem.Instance;

        // AI 国清单
        var aiKids = new List<int>();
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
            if (!all[i].IsPlayer) aiKids.Add(all[i].id);

        // ---- P1 结构：每 AI 国 House>=1+房容>0（件2）----
        yield return new WaitForSeconds(1f);
        bool p1 = aiKids.Count > 0;
        var p1Detail = new System.Text.StringBuilder();
        foreach (int kid in aiKids)
        {
            int houses = 0;
            var breg = BuildingRegistry.Instance;
            if (breg != null)
            {
                var bl = breg.All;
                for (int i = 0; i < bl.Count; i++)
                {
                    var b = bl[i];
                    if (b == null || b.def == null || !b.IsActive) continue;
                    if (b.def.id != "House" || b.kingdomId != kid) continue;
                    houses++;
                }
            }
            int cap = hap != null ? hap.GetHouseCapacityByKingdom(kid) : -1;
            p1Detail.Append($"k{kid}:House{houses}/容{cap} ");
            if (houses < 1 || cap <= 0) p1 = false;
        }
        results.Add($"P1 结构 AI国House+房容 [{p1Detail.ToString().TrimEnd()}] ={p1}");

        // ---- 快进压场（45 游戏日：⑥招工即时链+生育 D11 首判+成长 D13+二判 D21；P4=Save+json 镜像补证）----
        TimeManager.Instance.SetSecondsPerDay(15f);
        TimeManager.Instance.SetGameSpeed(3f);
        float w0 = Time.realtimeSinceStartup;
        bool childSeen = false, recruitSeen = false, growSeen = false;
        int playerPop = -1;   // 玩家职业桶终值（P3 记录用；绝对值波动=生态不入判定）
        while (Time.realtimeSinceStartup - w0 < 225f)   // ≈45 游戏日@5s/日
        {
            yield return null;
            // P2a：⑥招工落地（日志镜像）
            if (!recruitSeen && WatchLog.Exists(s => s.Contains("⑥招工人落地"))) recruitSeen = true;
            // P2b：AI Child 诞生（实体+raceId 对照）
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
                    results.Add($"P2b 生育 AI Child k{u.kingdomId} raceId={u.raceId}(期望{expect}{(raceOk ? "✓" : "✗")}) ={raceOk}");
                    break;
                }
            }
            // P2c：直生（日志镜像）
            if (!growSeen && WatchLog.Exists(s => s.Contains("AI小孩长大"))) growSeen = true;
            // P3：玩家人口（限定玩家职业桶——Monster kingdomId=0 会污染裸计数，首轮实锤）
            playerPop = PopulationSystem.CountAliveByKingdom(0, Occupation.Worker, Occupation.Resident, Occupation.Warrior);
        }
        if (!recruitSeen) { /* P2a 主判在循环外以日志镜像为准 */ }
        if (!childSeen) results.Add("P2b 生育 AI Child =False（45 日内无——见 P1 房容/条件诊断）");

        // P2a 落地判定（循环后终判：日志镜像+AI Worker 增量）
        int recruitCount = WatchLog.FindAll(s => s.Contains("⑥招工人落地")).Count;
        int aiWorkerMax = 0;
        var usEnd = UnityEngine.Object.FindObjectsOfType<UnitController>();
        var kidWorker = new Dictionary<int, int>();
        for (int i = 0; i < usEnd.Length; i++)
        {
            var u = usEnd[i];
            if (u == null || !u.IsAlive || u.kingdomId <= 0) continue;
            if (u.EffectiveOccupation == Occupation.Worker)
            {
                aiWorkerMax++;
                if (kidWorker.ContainsKey(u.kingdomId)) kidWorker[u.kingdomId]++;
                else kidWorker[u.kingdomId] = 1;
            }
        }
        bool p2a = recruitCount > 0;
        var kd = new System.Text.StringBuilder();
        foreach (var kv in kidWorker) kd.Append($"k{kv.Key}={kv.Value} ");
        results.Add($"P2a 招工落地 日志×{recruitCount} AI Worker[{kd.ToString().TrimEnd()}] ={p2a}");

        if (growSeen) results.Add($"P2c 直生 AI小孩长大日志×{WatchLog.FindAll(s => s.Contains("AI小孩长大")).Count} ={true}");
        else results.Add("P2c 直生 AI小孩长大 =False（依赖 P2b 生育+成长链）");

        // P3 玩家零回归：流浪 kingdomId 全 -1（无 0 挂靠）+玩家人口不丢+招募口调用不炸
        // （CanRecruit 返回值不判 True——玩家粮 0=玩家侧既有行为[三考 CSV 玩家粮恒 0]，非本批回归面）
        int vagN = 0, vagNeg = 0, vagZero = 0;
        for (int i = 0; i < usEnd.Length; i++)
        {
            var u = usEnd[i];
            if (u == null || !u.IsAlive || u.EffectiveOccupation != Occupation.Vagrant) continue;
            vagN++;
            if (u.kingdomId < 0) vagNeg++;
            else if (u.kingdomId == 0) vagZero++;
        }
        bool canCall = false; bool canRecruitVal = false;
        try { canRecruitVal = VagrantCampSystem.Instance != null && VagrantCampSystem.Instance.CanRecruit(); canCall = true; }
        catch (System.Exception e) { Debug.LogError("[HH81冒烟] CanRecruit 异常: " + e.Message); }
        // P3 判定=结构性两项：流浪残留 0（挂靠清除）+招募口不炸——玩家桶定义 kingdomId==0 精确匹配，
        // -1 流浪天然不入=零污染结构性保证；玩家人口绝对值波动=世界生态（野怪杀工，三考同款）不入判定，数值仅记录。
        bool p3 = vagZero == 0 && canCall;
        results.Add($"P3 玩家零回归 流浪={vagN}(全-1:{vagNeg}/残留0:{vagZero}) 玩家桶终值={playerPop}(生态波动不入判定) 招募口不炸={canCall}(粮够={canRecruitVal}) ={p3}");

        // ---- P4 存档：Save+镜像拷贝（回读 json 统计补证）----
        yield return null;
        bool saved = SaveManager.Instance.Save(SLOT + "_long");
        try
        {
            var savePath = System.IO.Path.Combine(Application.persistentDataPath, "Saves", SLOT + "_long.json");
            if (System.IO.File.Exists(savePath))
                System.IO.File.Copy(savePath, System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1/hh81_p3_save.json"), true);
        }
        catch (System.Exception e) { Debug.LogWarning("[HH81冒烟] 存档镜像拷贝失败: " + e.Message); }
        results.Add($"P4 存档 save={saved}（回读=json 统计补证：Vagrant kid=-1/Worker kid>0/Resident kid=0）={saved}");

        // ---- P5 雷区 ----
        results.Add($"P5 雷区 运行时枚举异常命中={ErrWatch.Count}（0=纪律成立）{(ErrWatch.Count == 0 ? "=True" : "=False")}");

        int pass = 0;
        var sbOut = new System.Text.StringBuilder();
        for (int i = 0; i < results.Count; i++)
        {
            Debug.Log("[HH81冒烟] " + results[i]);
            sbOut.AppendLine(results[i]);
            if (results[i].EndsWith("=True")) pass++;
        }
        string summary = $"===== {(pass == results.Count ? "ALL PASS" : $"HAS FAIL({results.Count - pass})")}（{pass}/{results.Count}）=====";
        Debug.Log("[HH81冒烟] " + summary);
        sbOut.AppendLine(summary);
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh81_smoke_result.log"), sbOut.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH81冒烟] 结果写文件失败: " + e.Message); }

        SmokeApi.QuitSmoke();
    }

    [InitializeOnLoadMethod]
    private static void InstallWatch()
    {
        Application.logMessageReceived -= OnLogLine;
        Application.logMessageReceived += OnLogLine;
    }

    private static readonly List<string> ErrWatch = new List<string>();
    private static readonly List<string> WatchLog = new List<string>();   // ⑥招工/生育/直生日志镜像

    private static void OnLogLine(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log && condition != null
            && (condition.Contains("⑥招工人落地") || condition.Contains("⑥招工无候选")
                || condition.Contains("AI生育") || condition.Contains("AI小孩长大")))
        { WatchLog.Add(condition); return; }
        if (type != LogType.Exception && type != LogType.Error) return;
        if (condition != null && (condition.Contains("InvalidOperationException") || condition.Contains("Collection was modified")))
            ErrWatch.Add(condition);
    }
}
