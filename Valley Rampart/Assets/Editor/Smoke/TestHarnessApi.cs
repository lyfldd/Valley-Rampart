using System.Collections;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.92 件A/T3：专用测试环境 考跑一体开关（Editor-only，D549）。
//  EnterTestRun = 建局(复用 SmokeApi.EnterGame 真实链路) → 等就绪 → TimeManager.EnableTestHarness(speed)
//    → maximumDeltaTime=1.0（M3：解除 0.33s 钳制，掉帧加速不静默失效）→ 无渲染减负（关阴影/VSync，记原值）。
//  ExitTestRun = 全量恢复（考跑态/timeScale/maximumDeltaTime/QualitySettings）。
//  M4 安全：maximumDeltaTime=1.0 → 15x 单帧游戏跨度 ≤15s < 360s/天，AdvanceTime 跨天 while 单帧不破。
//  玩家侧零回归红线：本类 Editor-only；正式局无任何消费点。
// ============================================================================
public static class TestHarnessApi
{
    static float _origMaxDeltaTime = -1f;
    static ShadowQuality _origShadows;
    static int _origVSync;
    static bool _captured;

    /// <summary>
    /// 协程：建局+等就绪+开考跑。speedOverride 空则读 WorldConfig.time.testSpeedMultiplier（DZ-071 SO 化）。
    /// </summary>
    public static IEnumerator EnterTestRun(NewGameConfig cfg, float? speedOverride = null)
    {
        SmokeApi.EnterGame(cfg);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f)
            { Debug.LogError("[TestHarness] EnterTestRun 等就绪超时。"); yield break; }
        }
        yield return new WaitForSeconds(0.3f);

        float speed = speedOverride ?? ResolveConfiguredSpeed();
        if (TimeManager.Instance != null) TimeManager.Instance.EnableTestHarness(speed);

        if (!_captured)
        {
            _origMaxDeltaTime = Time.maximumDeltaTime;
            _origShadows = QualitySettings.shadows;
            _origVSync = QualitySettings.vSyncCount;
            _captured = true;
        }
        Time.maximumDeltaTime = 1.0f;                        // M3：≥1.0（默认 0.33s 钳制解除）
        QualitySettings.shadows = ShadowQuality.Disable;     // 考跑不看表现（分辨率减负不做，列报）
        QualitySettings.vSyncCount = 0;
        Debug.Log($"[TestHarness] EnterTestRun 完成：speed={speed:0.##}x maximumDeltaTime={Time.maximumDeltaTime:0.##} shadows=Off vSync=0");
    }

    /// <summary>考跑速度真源：WorldConfig.time.testSpeedMultiplier（缺省兜底 15=1 现实秒/1 游戏小时）。</summary>
    public static float ResolveConfiguredSpeed()
    {
        var ws = WorldSystem.Instance;
        if (ws != null && ws.Config != null && ws.Config.time.testSpeedMultiplier > 0f)
            return ws.Config.time.testSpeedMultiplier;
        return 15f;
    }

    /// <summary>全量恢复（考跑态/timeScale/maximumDeltaTime/QualitySettings）。</summary>
    public static void ExitTestRun()
    {
        if (TimeManager.Instance != null) TimeManager.Instance.DisableTestHarness();
        if (_captured)
        {
            Time.maximumDeltaTime = _origMaxDeltaTime;
            QualitySettings.shadows = _origShadows;
            QualitySettings.vSyncCount = _origVSync;
            _captured = false;
        }
        if (Application.isPlaying) Time.timeScale = 1f;
        Debug.Log($"[TestHarness] ExitTestRun：全量恢复完成（maximumDeltaTime={Time.maximumDeltaTime:0.##} shadows={QualitySettings.shadows} vSync={QualitySettings.vSyncCount}）");
    }
}
