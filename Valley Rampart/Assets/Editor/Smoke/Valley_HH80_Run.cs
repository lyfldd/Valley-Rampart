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
    // P1 七考重验（HH.203/D657；D661 放行）：六考原值 SEED=69496 / SLOT="p1_run6b"、七考原值 SEED=73621 / SLOT="p1_run7"
    // 均已 git 留证（`git log -p -- Assets/Editor/Smoke/Valley_HH80_Run.cs` 可复原；p1_run6/p1_run6b/p1_run7 原封勿覆盖）。
    private const int SEED = 64513;          // HH.203 七考重验 seed（2026-09-11 侦察定案：4 AI[密林 r1 精灵/磐石 r2 矮人/铁蹄 r3 兽人/霜岩 r2 矮人]+国距 42.9/70.7/89.0/92.2/110.5/122.6 均衡无口袋+领土 mid 16~19+营地 2+流浪 6；候选 70403[3 AI·min 41.7 次优]备选、82007[min 25.0 过近]否决；见 Logs/P1/hh80_scout_result.log）
    private const string SLOT = "p1_run8";   // 七考重验段独立命名（禁覆盖 p1_run6=D45 袭扰段 / p1_run6b=六考正门重跑段 / p1_run7=七考段）
    private const int CIRCUIT_BREAK_DAY = 120;

    private static readonly List<int> _military = new List<int>();
    private static volatile bool _done;

    [MenuItem("Valley/验证/HH80_正式跑")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH80跑] 须先 GameScene 进 Play（且先启动 P1 观测器）。"); return; }
        // D647 异议2 fail-fast：观测器未启则中止（防 ≈48 分钟空跑无证据/无检查点）
        if (!P1Observer.IsRunning)
        {
            Debug.LogError("[HH80跑] ✗ 观测器未启动——请先点「Valley/观测/P1_启动观测」再起跑（D647 异议2 fail-fast）。已中止。");
            return;
        }
        // D656 硬条款2：显式传槽（观测器 MainSlot 已参数化，禁回落硬编码）——进局前设置
        P1Observer.SetMainSlot(SLOT);
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
        // HH.150 清残留：删除迁正门时遗留的裸局 SmokeApi.EnterGame+服务性等就绪块
        // （双建局冗余；EnterTestRun 内置等就绪，直接正门进局）
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
        // L-32 条文3（D657 入库·HH.203 §二.7）：禁以 `SetGameSpeed(0f)` 当暂停（SnapToSpeed 吸附 0.5x 非暂停）⇒ 真暂停用 timeScale
        Time.timeScale = 0f;
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
        Debug.LogWarning("[HH80跑] ★ " + why + "——真暂停(TS=0)+封盘 " + SLOT + "=" + saved + "（现场保留：观测器镜像/CSV 在案；收工退 Play，L-32）");
        // L-32 条文1（D657 入库）：收工禁留「已恢复 1x」余留世界 ⇒ 容器末尾直接退 Play（同 SmokeApi.QuitSmoke 先例）
        EditorApplication.ExitPlaymode();
    }
}
