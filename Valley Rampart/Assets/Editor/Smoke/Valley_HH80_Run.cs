using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.80 P1 考跑正式容器（Editor-only；HH.71 协议；D585 正门版=test-harness-first 铁律④补接）
//  用法：GameScene Play → 先启动 P1 观测器（Valley/观测/P1_启动观测）→ 本菜单。
//  职责：EnterTestRun 正门进局（D585 全守卫=判负封死+T11 野怪静默+考跑档直通 15x；
//  玩家真实局态挂机=T10 幽灵化 OFF）→独立监控循环
//  （自订阅 logMessageReceived 数「剧本阶段 → 军事」——不掏 P1Observer 私有字段，
//  接口纪律）→ 达标（≥2 AI 军事期）或熔断（day≥120）或灭绝全灭 → SetGameSpeed(0)
//  停速+Save 封盘+ExitTestRun 全量恢复+写状态文件 Logs/P1/hh80_run_status.log（console 大缓冲教训）。
//  每 20 日评审打点（协议）→ 镜像日志+CSV 承担，本容器只做终局三停（达标/熔断/灭绝）。
//  红线：零玩家干预（本容器只开考跑档+Save，不建造不训练不输资源）。
// ============================================================================
public static class Valley_HH80_Run
{
    private const int SEED = 69496;          // HH.122 六考正门重跑 seed（侦察定案 2026-09-09 D585：3 AI[密林 r1/寒晶 r2/战歌 r3]+国距 64.7~175.5 均衡无口袋+营地 2+非历史局；候选 48271[33.3 近]/81203[18.7 近]弃；73311=D45 袭扰段报废，见 Logs/P1/hh80_scout_result.log）
    private const string SLOT = "p1_run6b";  // 六考正门重跑段独立命名（p1_run6=D45 袭扰段检查点 day005~045 原封勿覆盖=D585 袭扰面证据链）
    private const int CIRCUIT_BREAK_DAY = 120;

    private static readonly List<int> _military = new List<int>();
    private static volatile bool _done;

    [MenuItem("Valley/验证/HH80_正式跑")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH80跑] 须先 GameScene 进 Play（且先启动 P1 观测器）。"); return; }
        _military.Clear(); _done = false;
        Application.logMessageReceived += WatchMilitary;
        new GameObject("HH80_RunRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator routine) => StartCoroutine(routine);
    }

    private static void WatchMilitary(string condition, string stackTrace, LogType type)
    {
        if (condition == null || !condition.Contains("剧本阶段 → 军事")) return;
        int idx = condition.IndexOf(" k", System.StringComparison.Ordinal);
        if (idx < 0) return;
        int start = idx + 2; int end = start;
        while (end < condition.Length && char.IsDigit(condition[end])) end++;
        int kid;
        if (int.TryParse(condition.Substring(start, end - start), out kid) && kid > 0 && !_military.Contains(kid))
        {
            _military.Add(kid);
            Debug.LogWarning("[HH80跑] ⚑ 军事期达标 k" + kid + "（累计 " + _military.Count + "）");
        }
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
            if (Time.realtimeSinceStartup - t0 > 120f) { Debug.LogError("[HH80跑] 等世界就绪超时。"); yield break; }
        }
        yield return new WaitForSeconds(1f);

        // 正门进局（test-harness-first 铁律①/D585：EnterTestRun 全守卫=判负封死+T11 野怪静默+考跑档直通
        // [speedOverride=null→读 WorldConfig.time.testSpeedMultiplier SO 缺省 15]；玩家真实局态=T10 OFF）
        yield return TestHarnessApi.EnterTestRun(cfg);
        Debug.LogWarning("[HH80跑] 正门进局 seed=" + SEED + " 槽=" + SLOT + " 考跑守卫全开（D585 判负封死+T11 野怪静默，玩家真实局态挂机；P1 观测器须已在跑：镜像+CSV+检查点）");

        // 终局三停监控：达标（≥2 AI 军事期）/熔断（D120）/灭绝（AI 全灭）
        while (!_done)
        {
            yield return new WaitForSeconds(5f);
            int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            var reg = KingdomRegistry.Instance;
            int aiAlive = 0, aiTotal = 0;
            if (reg != null)
            {
                var all = reg.GetAll();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].IsPlayer) continue;
                    aiTotal++;
                    if (all[i].workerCount + all[i].warriorCount > 0) aiAlive++;
                }
            }
            if (_military.Count >= 2) { Finish("达标收工：≥2 AI 军事期（" + string.Join(",", _military) + "）@D" + day); break; }
            if (day >= CIRCUIT_BREAK_DAY) { Finish("熔断：D" + CIRCUIT_BREAK_DAY + " 无 ≥2 AI 军事期（已达标=" + string.Join(",", _military) + "，AI 存活 " + aiAlive + "/" + aiTotal + "）"); break; }
            if (reg != null && aiTotal > 0 && aiAlive == 0) { Finish("灭绝停跑：AI 全灭 @D" + day + "（已达标=" + string.Join(",", _military) + "）"); break; }
        }
    }

    private static void Finish(string why)
    {
        _done = true;
        Application.logMessageReceived -= WatchMilitary;
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
        TestHarnessApi.ExitTestRun();   // 正门收尾：考跑态/maximumDeltaTime/渲染全量恢复（D585；封盘在后不丢档）
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== HH.80 三考收工 =====");
        sb.AppendLine(why);
        sb.AppendLine("终速=0 存盘 " + SLOT + "=" + saved + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh80_run_status.log"), sb.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[HH80跑] 状态写文件失败: " + e.Message); }
        Debug.LogWarning("[HH80跑] ★ " + why + "——终速 0+封盘 " + SLOT + "=" + saved + "（现场保留：观测器镜像/CSV 在案，待会话侧停观测+收工）");
    }
}
