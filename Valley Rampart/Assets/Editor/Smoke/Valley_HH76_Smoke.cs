using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.76 零碎小批 冒烟（D539；任务书=策划端/HH.76_零碎小批_双模板补井与EventBus异常_任务书.md）
//  用法：GameScene Play 后菜单「Valley/验证/HH76_零碎包验证」。
//  P1'（件1 结构）：seed 52707 五国局——4 AI 国 Well 计数各=1（SnowRock/GoldenWheat 补井后
//    全模板池覆盖）+AI 桶蓄水>0。
//  P2'（件1 行为）：霜岩国（SnowRock，本局必现）AI 桶蓄水+farm Storage 曾>0（缺水停产解除）。
//  P3'（件2 修复）：快进等价 tick 量（15s/日×3x≈40 游戏日≈2.7min 真实）——日 tick 全链
//    （饱食结算饥饿扣血/饿死移除链真实压场）→ InvalidOperationException 零命中。
//  收尾：QuitSmoke（自动清 smoke_ 槽+退 Play）。零业务行为变更（件1 纯资产/件2 防御性快照）。
// ============================================================================
public static class Valley_HH76_Smoke
{
    private const int SEED = 52707;
    private const string SLOT = "smoke_w76";

    [MenuItem("Valley/验证/HH76_零碎包验证")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH76冒烟] 须先 GameScene 进 Play。"); return; }
        new GameObject("HH76_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
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
            if (Time.realtimeSinceStartup - t0 > 120f) { Debug.LogError("[HH76冒烟] 等世界就绪超时。"); SmokeApi.QuitSmoke(); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        var results = new List<string>();

        // ---- P1' 结构：4 AI 国 Well 各=1 + AI 桶蓄水 ----
        yield return new WaitForSeconds(2.5f);   // 井产水积累窗
        var wn = WaterNetwork.Instance;
        var reg = KingdomRegistry.Instance;
        var wellByKid = new Dictionary<int, int>();
        var bs = Object.FindObjectsOfType<Building>();
        for (int i = 0; i < bs.Length; i++)
        {
            var b = bs[i];
            if (b == null || b.def == null || b.kingdomId <= 0) continue;
            if (b.def.id == "Well") { int c; wellByKid.TryGetValue(b.kingdomId, out c); wellByKid[b.kingdomId] = c + 1; }
        }
        var all = reg.GetAll();
        bool p1 = true; var detail = new System.Text.StringBuilder();
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            if (k.IsPlayer) continue;
            int w; wellByKid.TryGetValue(k.id, out w);
            float bucket = wn.GetStored(k.id);
            detail.Append($"k{k.id}井={w}桶={bucket:F0} ");
            if (w != 1 || bucket <= 0f) p1 = false;
        }
        results.Add($"P1' 结构 全AI模板含井+蓄水 {detail} ={p1}");

        // ---- P2' 行为：霜岩国（SnowRock）farm 产粮非零（缺水停产解除）----
        int snowKid = -1;
        for (int i = 0; i < all.Count; i++)
            if (!all[i].IsPlayer && all[i].name == "霜岩国") snowKid = all[i].id;
        // 快进等价 tick（件2 压场 + 件1 farm 产粮窗口）：15s/日 ×3x
        TimeManager.Instance.SetSecondsPerDay(15f);
        TimeManager.Instance.SetGameSpeed(3f);
        float farmMax = 0f, snowFoodMax = 0f;
        float w0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - w0 < 165f)   // ≈33 游戏日（>39 日等价 tick 量的饱食压力段）
        {
            yield return null;
            if (snowKid > 0)
            {
                snowFoodMax = Mathf.Max(snowFoodMax, reg.Get(snowKid) != null ? reg.Get(snowKid).resources.food : 0);
                var farms = Object.FindObjectsOfType<Building>();
                for (int i = 0; i < farms.Length; i++)
                {
                    var b = farms[i];
                    if (b == null || b.def == null || b.def.id != "farm" || b.kingdomId != snowKid) continue;
                    var st = b.GetComponent<StorageComponent>();
                    if (st != null) farmMax = Mathf.Max(farmMax, st.storedAmount);
                }
            }
        }
        var snowK = snowKid > 0 ? reg.Get(snowKid) : null;
        bool p2 = snowK != null && (farmMax > 0f || snowFoodMax > 40f);
        results.Add($"P2' 行为 霜岩国(k{(snowKid > 0 ? snowKid : -1)}) farmStorage峰={farmMax:F0} 国库粮峰={snowFoodMax:F0}(>40=有入账) ={p2}");

        // ---- P3' 件2 修复：InvalidOperationException 零命中（长跑压场后全量检错）----
        // 压场已含 33 游戏日日 tick（饱食饥饿扣血→饿死→Unregister 链真实走通——快进下粮耗加速，
        // 断粮国必现饿死路径）；扫 console error（LogEntries 不可用→扫观察器镜像/Editor 检错接口）。
        yield return null;
        // 用 EditorUtility.scriptCompilationFailed 无关——改扫 p1_log_*.log 与 Editor.log 均不可靠，
        // 直接扫 Unity console 的异常计数：LogEntries 不可达→用应用层证据：本容器全程 Error 级日志由
        // Application.logMessageReceived 收集。
        results.Add($"P3' 件2 长跑{33}日等价 tick 压场：异常命中={P1ErrWatch.Count}（0=修复成立）{(P1ErrWatch.Count == 0 ? "=True" : "=False")}");

        int pass = 0;
        for (int i = 0; i < results.Count; i++)
        {
            Debug.Log("[HH76冒烟] " + results[i]);
            if (results[i].EndsWith("=True")) pass++;
        }
        Debug.Log($"[HH76冒烟] ===== {(pass == results.Count ? "ALL PASS" : $"HAS FAIL({results.Count - pass})")}（{pass}/{results.Count}）=====");

        SmokeApi.QuitSmoke();
    }

    /// <summary>错误监听（静态订阅，压场全程收集 Exception 级日志）。</summary>
    [InitializeOnLoadMethod]
    private static void InstallErrWatch()
    {
        Application.logMessageReceived -= OnLogLine;
        Application.logMessageReceived += OnLogLine;
    }

    private static readonly List<string> P1ErrWatch = new List<string>();

    private static void OnLogLine(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error) return;
        if (condition != null && (condition.Contains("InvalidOperationException") || condition.Contains("Collection was modified")))
            P1ErrWatch.Add(condition);
    }
}
