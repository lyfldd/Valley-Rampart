using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.89 统一度量衡批 验证容器（D547；任务书=策划端/HH.89_统一度量衡批_任务书.md 件3）
//  用法：GameScene Play 后菜单「Valley/验证/HH89_度量衡验证」。
//  V1: Easy(档1) 建局 → TimeManager.SecondsPerDay==360（日志断言由 console 外部核）。
//  V2: Hard(档3) 建局（同 seed）→ 同断言（两档一致=难度-时间耦合已除）。
//  V3: 读档回归 → SaveState json=360 → 篡改 999 → LoadState 读回 360（LoadState 链保留实证）。
//  收尾：QuitSmoke。探针只读公开口+ISaveable payload，零业务触碰。
// ============================================================================
public static class Valley_HH89_Smoke_Measure
{
    private const int SEED = 22360;
    private const float EXPECT_SPD = 360f;

    [MenuItem("Valley/验证/HH89_度量衡验证")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH89冒烟] 须先 GameScene 进 Play。"); return; }
        new GameObject("HH89_SmokeRunner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour
    {
        public void Host(IEnumerator routine) => StartCoroutine(routine);
    }

    private static IEnumerator RunCoroutine()
    {
        var results = new List<string>();

        // ---- V1 Easy / V2 Hard 两轮（同 seed 固定序）----
        int[] diffs = { 1, 3 };
        string[] names = { "Easy", "Hard" };
        for (int r = 0; r < diffs.Length; r++)
        {
            // HH.150 迁正门：TestHarnessApi.EnterTestRun = EnterGame 真实链+等就绪+考跑加速（D600/L-17；
            // 考跑只直通 timeScale，不改 SecondsPerDay——V1/V2 度量衡断言语义不受影响）
            yield return TestHarnessApi.EnterTestRun(new NewGameConfig
            {
                worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = diffs[r],
                worldSize = WorldSize.Medium, selectedSlotId = "smoke_h89_" + diffs[r],
                kingdomName = "度量衡" + names[r]
            });

            float t0 = Time.realtimeSinceStartup;
            while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
                   || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
            {
                yield return null;
                if (Time.realtimeSinceStartup - t0 > 120f)
                { Debug.LogError("[HH89冒烟] 等世界就绪超时（难度=" + diffs[r] + "）。"); TestHarnessApi.ExitTestRun(); SmokeApi.QuitSmoke(); yield break; }
            }
            yield return new WaitForSeconds(0.5f);

            var tm = TimeManager.Instance;
            bool spdOk = tm != null && Mathf.Approximately(tm.SecondsPerDay, EXPECT_SPD);
            results.Add($"V{r + 1} {names[r]}(档{diffs[r]}) 建局 SecondsPerDay={tm.SecondsPerDay:0.##}(期望{EXPECT_SPD:0}) ={spdOk}");

            if (r == 0)
            {
                // ---- V3 读档回归（V1 场内做：存 360 → 篡改 → 读回 360）----
                var payload = tm.SaveState();
                var data = JsonUtility.FromJson<TimeSaveData>(payload.json);
                bool saved360 = Mathf.Approximately(data.secondsPerDay, EXPECT_SPD);
                tm.SetSecondsPerDay(999f);              // 模拟漂移，证 LoadState 链真实恢复
                tm.LoadState(payload);
                bool loaded360 = Mathf.Approximately(tm.SecondsPerDay, EXPECT_SPD);
                results.Add($"V3 读档回归 存档json.secondsPerDay={data.secondsPerDay:0.##} 存={saved360} 读回={tm.SecondsPerDay:0.##} 读={loaded360} ={(saved360 && loaded360)}");
            }

            SmokeApi.ResetWorldForNext();
            yield return null;   // 陷阱2：留一帧让 Destroy 落地
        }

        bool allPass = !results.Exists(x => x.EndsWith("=False"));
        Debug.Log("[HH89冒烟] 轮汇总 " + (allPass ? "ALL PASS" : "FAIL") + "\n" + string.Join("\n", results));
        TestHarnessApi.ExitTestRun();   // HH.150 正门收尾：全量恢复考跑态
        SmokeApi.QuitSmoke();
    }
}
