using System.Collections;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.190 / D651【T1 观测口补口 · 短程探针容器】建军链诊断跑（Editor-only 观测域增量）
//  用法：GameScene Play → 「Valley/观测/P1_启动观测」→「Valley/诊断/启动建军链诊断」→ 本菜单。
//  与 `Valley_HH80_Run`（七考正式容器）**并存、互不覆盖**：
//    · SEED=48903（七考备选候选·已侦察结构合格；非七考 73621、非历史/冒烟 seed）
//    · SLOT="p1_diag"（禁覆盖 p1_run6/p1_run6b/p1_run7）
//    · 窗口=D30（短程诊断；**不计判定证据**，任务书 §二 附）
//  正门：TestHarnessApi.EnterTestRun（15x 缺省；玩家真实局态挂机；零玩家干预）
//  收尾：SetGameSpeed(0) → Save(SLOT) → ExitTestRun → 写 Logs/P1/hh80_diag_status.log（独立文件名，不覆盖七考状态档）
//  红线：只进退局+封盘，不建造不训练不输资源；业务代码零改动（本文件 + 只读探针均在 Editor 域）
// ============================================================================
public static class Valley_HH80_DiagRun
{
    private const int SEED = 48903;              // 短程诊断 seed（HH.192 口径三问③；非历史/冒烟/七考）
    private const string SLOT = "p1_diag2";      // 诊断专用槽（D656：回归探针新槽；禁覆盖 p1_run6/6b/7/p1_diag）
    private const int DIAG_DAYS = 60;            // 短程窗口（HH.194 run1 实测 30 日不足：兵营 D28 才竣工⇒⑦可行窗口仅 2 日且金尽；
                                                 // 60 日≈24 现实分钟 @15x。容器级观测配置，非游戏机制参数——D563③ 不适用）

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

        while (true)
        {
            yield return new WaitForSeconds(5f);
            int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            if (day >= DIAG_DAYS) { Finish("诊断窗口到期 @D" + day); yield break; }
        }
    }

    private static void Finish(string why)
    {
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
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
        Debug.LogWarning("[Diag跑] ★ " + why + "——终速 0+封盘 " + SLOT + "=" + saved);
    }
}
