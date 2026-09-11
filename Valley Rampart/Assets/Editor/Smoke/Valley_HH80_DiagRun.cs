using System.Collections;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.190 / D651【T1 观测口补口 · 短程探针容器】建军链诊断跑（Editor-only 观测域增量）
//  用法：GameScene Play → 「Valley/观测/P1_启动观测」→「Valley/诊断/启动建军链诊断」→ 本菜单。
//  与 `Valley_HH80_Run`（七考正式容器）**并存、互不覆盖**：
//    · SEED=48903（七考备选候选·已侦察结构合格；非七考 73621、非历史/冒烟 seed）
//    · SLOT="p1_diag3"（HH.204/D658 收口批新槽；禁覆盖 p1_run6/p1_run6b/p1_run7/p1_diag/2）
//    · 窗口=D60（短程诊断；**不计判定证据**，任务书 §二 附）
//    · 阶段注入取证段＝D51 起每日「注入 Military → 同步 DumpNow → 还原 scriptPhase」（A′ 授权）
//  正门：TestHarnessApi.EnterTestRun（15x 缺省；玩家真实局态挂机；零玩家干预）
//  收尾：真暂停(Time.timeScale=0) → Save(SLOT) → ExitTestRun → 写 Logs/P1/hh80_diag_status.log（独立文件名，不覆盖七考状态档）→ 退 Play（L-32）
//  红线：只进退局+封盘，不建造不训练不输资源；业务代码零改动（本文件 + 只读探针均在 Editor 域）
// ============================================================================
public static class Valley_HH80_DiagRun
{
    private const int SEED = 48903;              // 短程诊断 seed（HH.192 口径三问③；非历史/冒烟/七考）
    private const string SLOT = "p1_diag3";      // 诊断专用槽（HH.204/D658：收口批新槽；禁覆盖 p1_run6/6b/7/p1_diag/2）
    private const int DIAG_DAYS = 60;            // 短程窗口（HH.194 run1 实测 30 日不足：兵营 D28 才竣工⇒⑦可行窗口仅 2 日且金尽；
                                                 // 60 日≈24 现实分钟 @15x。容器级观测配置，非游戏机制参数——D563③ 不适用）
    private const int INJECT_FROM_DAY = 51;      // HH.204/D658 裁 A′【阶段注入取证段】：D51 起每日同步快照一次——
                                                 // 三专属营 minStage=3（军事期）⇒ 军事期前不可见，无注入则 census 恒不可判读。
                                                 // 夹具语义＝「注入→同步 DumpNow→还原」：真实选招读 StageMachine.Stage
                                                 //（KingdomBrain.cs:259-261 每日回写 scriptPhase），故注入**只影响探针同步读**、零行为漂移。
                                                 // 先例＝Smoke_2_22P0.cs:215/220/497（注入后须防日 tick 覆回 ⇒ 本容器用同步窗内还原规避）。

    [MenuItem("Valley/验证/HH80_诊断跑")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[Diag跑] 须先 GameScene 进 Play。"); return; }
        // fail-fast：观测器 + 诊断探针**双在场**（防空跑无证据）
        if (!P1Observer.IsRunning)
        {
            Debug.LogError("[Diag跑] ✗ P1 观测器未启动——请先点「Valley/观测/P1_启动观测」。已中止。");
            return;
        }
        if (!DiagMilitary.IsRunning)
        {
            Debug.LogError("[Diag跑] ✗ 建军链诊断探针未启动——请先点「Valley/诊断/启动建军链诊断」。已中止。");
            return;
        }
        // D656 硬条款2：显式传槽（观测器 MainSlot 已参数化，禁回落硬编码）——进局前设置
        P1Observer.SetMainSlot(SLOT);
        new GameObject("HH80_DiagRunHost").AddComponent<DiagRunHost>().Host(RunCoroutine());
    }

    private class DiagRunHost : MonoBehaviour
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
        yield return TestHarnessApi.EnterTestRun(cfg);   // 正门（L-17）：15x 缺省 + 全守卫 + 玩家真实局态
        Debug.LogWarning("[Diag跑] 正门进局 seed=" + SEED + " 槽=" + SLOT + "（诊断窗口 D" + DIAG_DAYS + "；不计判定证据）");

        int lastSnapDay = -1;
        while (true)
        {
            yield return new WaitForSeconds(5f);
            int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            if (day >= DIAG_DAYS) { Finish("诊断窗口到期 @D" + day); yield break; }
            // HH.204/D658 裁 A′：阶段注入取证段（D51 起每日一次，同步窗内注入→取证→还原）
            if (day >= INJECT_FROM_DAY && day != lastSnapDay) { lastSnapDay = day; SnapshotInjectedStage(day); }
        }
    }

    /// <summary>
    /// HH.204/D658 裁 A′【阶段注入夹具】——三专属营 minStage=3（军事期），无注入则 census 恒不可判读。
    /// 语义＝**同步窗内**：全部 AI 注入 `scriptPhase=Military` → `DiagMilitary.DumpNow(tag)` → **立即还原**。
    /// 零行为漂移依据：真实选招路径读 `StageMachine.Stage`（KingdomBrain.cs:259-261 每日 tick 回写 scriptPhase），
    /// 本注入不触及 StageMachine ⇒ 只改变探针**同步读**到的阶段（先例 Smoke_2_22P0.cs:215/220/497）。
    /// </summary>
    private static void SnapshotInjectedStage(int day)
    {
        var reg = KingdomRegistry.Instance;
        if (reg == null) return;
        var all = reg.GetAll();
        if (all == null) return;

        var ks = new System.Collections.Generic.List<KingdomState>();
        var phases = new System.Collections.Generic.List<ScriptStage?>();
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            if (k == null || k.id == 0) continue;      // 玩家国不评（AI 决策面）
            ks.Add(k);
            phases.Add(k.scriptPhase);
            k.scriptPhase = ScriptStage.Military;      // 夹具注入（同步窗内还原）
        }
        try { DiagMilitary.DumpNow("D" + day + "-注入"); }
        finally
        {
            for (int i = 0; i < ks.Count; i++) ks[i].scriptPhase = phases[i];   // 还原（禁留注入态）
        }
        Debug.LogWarning("[Diag跑] 注入面取证快照 @D" + day + " k=" + ks.Count + "（scriptPhase 已还原；零行为漂移）");
    }

    private static void Finish(string why)
    {
        // L-32 条文3（D657 入库）：禁以 `SetGameSpeed(0f)` 当暂停（SnapToSpeed 吸附 0.5x 非暂停）⇒ 真暂停用 timeScale
        Time.timeScale = 0f;
        bool saved = SaveManager.Instance != null && SaveManager.Instance.Save(SLOT);
        TestHarnessApi.ExitTestRun();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("===== HH.80 诊断跑收工 =====");
        sb.AppendLine(why);
        sb.AppendLine("seed=" + SEED + " 槽=" + SLOT + " 终速=0 存盘=" + saved + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "hh80_diag_status.log"), sb.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[Diag跑] 状态写文件失败: " + e.Message); }
        Debug.LogWarning("[Diag跑] ★ " + why + "——真暂停(TS=0)+封盘 " + SLOT + "=" + saved + "；收尾退 Play（L-32 条文1）");
        // L-32 条文1（D657 入库）：收工禁留「已恢复 1x」余留世界 ⇒ 容器末尾直接退 Play（同 SmokeApi.QuitSmoke 先例）
        EditorApplication.ExitPlaymode();
    }
}
