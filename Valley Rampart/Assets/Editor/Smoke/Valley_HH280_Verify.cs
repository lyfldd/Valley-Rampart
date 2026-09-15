#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HH.280 D724 后收口批 验收探针（Editor-only · 正门 <see cref="TestHarnessApi.EnterTestRun"/>）：
///   件1 LOD 活跃中心集治本——`_focalMidChunk` 是否被 CameraRig 接线喂入、tier 分布对照、可视单位是否冻结；
///   件2 移速重标——非怪物运行时 WalkSpeed（目标≈0.716 世界/秒=1 格/秒）+ 实测净位移速（目标≈1.0 格/秒，HH.278 基线 2.59）。
/// 用法（MCP）：Play 模式内 <c>Valley_HH280_Verify.Run()</c>；证据落 <c>Logs/hh280_verify.log</c>；探针自收尾（ExitTestRun+QuitSmoke+退 Play）。
/// </summary>
public static class Valley_HH280_Verify
{
    const int Seed = 21107;
    const float IsoCellDiag = 0.71554f;   // iso 步长 √((1.28/2)²+(0.64/2)²)
    static readonly StringBuilder _log = new StringBuilder();
    static int _pass, _fail;
    static string _logPath;

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH280] 须先 GameScene 进 Play。"); return; }
        _log.Clear(); _pass = 0; _fail = 0;
        _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh280_verify.log"));
        Log("探针启动（正门 EnterTestRun · seed=" + Seed + " · 1x）", true);
        var host = new GameObject("HH280_ProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine(host));
    }

    /// <summary>桥接 exec_runtime_script 用：返回协程让工具等待执行完毕（fire-and-forget 会被工具退 Play 中断）。</summary>
    public static IEnumerator RunCoroutine()
    {
        _log.Clear(); _pass = 0; _fail = 0;
        _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh280_verify.log"));
        Log("探针启动（正门 EnterTestRun · seed=" + Seed + " · 1x）", true);
        return Coroutine(null);
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.Log("[HH280] " + (ok ? "✓ " : "✗ ") + msg);
        try
        {
            if (_logPath != null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
                System.IO.File.WriteAllText(_logPath, _log.ToString());
            }
        }
        catch { }
    }

    static object ReadField(object target, string name)
    {
        if (target == null) return "<lod-null>";
        var f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        return f != null ? f.GetValue(target) : "<no-field>";
    }

    static IEnumerator Coroutine(ProbeHost host)
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = "hh280_smoke", kingdomName = "HH280验证"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || CameraRig.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(host); yield break; }
        }
        yield return new WaitForSeconds(3f);   // 相机首帧推焦点 + LOD 带渲染 + 单位生成

        // ================= 件1：LOD 活跃中心集 =================
        var lod = LODSystem.Instance;
        object focal = ReadField(lod, "_focalMidChunk");
        Log($"件1 _focalMidChunk={focal}（HH.278 基线=<null>；CameraRig 每帧接线后应非 null）", focal != null && !focal.ToString().Contains("<"));

        int act = SpriteAnimatorDriver.Instance.CountTier(0);
        int semi = SpriteAnimatorDriver.Instance.CountTier(1);
        int dor = SpriteAnimatorDriver.Instance.CountTier(2);
        int total = SpriteAnimatorDriver.Instance.ActiveCount;
        Vector2 camPos = CameraRig.Instance != null ? (Vector2)CameraRig.Instance.transform.position : Vector2.zero;
        // 判据 = 可视区内（相机 12 世界格 ≈ 玩家主城周边）单位全部非 Dormant——「Game 窗口动画正常播放」。
        // 全局 Dormant 占比不作为判据：3 个 AI 王国远处单位按设计（D79 远距冻结）保持 Dormant（HH.278 基线＝
        // 连镜头 2.6 格内的单位都 Dormant，本批使其进入带内 ⇒ 前后对照见下方读数）。
        int nearActive = 0, nearTotal = 0;
        foreach (var u in Object.FindObjectsOfType<UnitController>())
        {
            if (u == null || !u.IsAlive) continue;
            if (Vector2.Distance(u.transform.position, camPos) <= 12f)
            {
                nearTotal++;
                var lv = lod != null ? lod.GetLevelAt(u.transform.position) : LodLevel.Dormant;
                if (lv != LodLevel.Dormant) nearActive++;
            }
        }
        Log($"件1 tier 分布 Active={act} Semi={semi} Dormant={dor} / total={total}（HH.278 基线 0/0/32 全冻结）", total > 0);
        Log($"件1 可视区（相机 12 世界格内）单位非冻结 {nearActive}/{nearTotal}（期望全非 Dormant）",
            nearTotal > 0 && nearActive == nearTotal);

        // 可视单位（相机中心 12 格内）档位：验收「Game 窗口单位动画正常播放」
        var units = Object.FindObjectsOfType<UnitController>();
        UnitController nearest = null; float bestD = float.MaxValue;
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive) continue;
            float d = Vector2.Distance(u.transform.position, camPos);
            if (d < bestD) { bestD = d; nearest = u; }
        }
        if (nearest != null && lod != null)
        {
            var lv = lod.GetLevelAt(nearest.transform.position);
            Log($"件1 最近相机单位（{nearest.EffectiveOccupation}·{bestD:F2} 世界距）档位={lv}（期望 Active/Semi 不冻结）",
                lv == LodLevel.Active || lv == LodLevel.SemiActive);
        }
        else Log("件1 无可视单位样本", false);

        // ================= 件2：移速重标 =================
        float wsSample = -1f; int wsCount = 0; float wsSum = 0f;
        foreach (var u in units)
        {
            if (u == null || u.Data == null || u.Data.faction == Faction.Monster) continue;
            wsSample = u.WalkSpeed; wsSum += u.WalkSpeed; wsCount++;
        }
        float wsAvg = wsCount > 0 ? wsSum / wsCount : -1f;
        Log($"件2 非怪物运行时 WalkSpeed 平均={wsAvg:F3}（n={wsCount}·目标≈0.716 世界/秒=1 格/秒；HH.278 基线 3.000）",
            wsCount > 0 && Mathf.Abs(wsAvg - 0.716f) < 0.02f);

        var pos0 = new Dictionary<UnitController, Vector2>();
        foreach (var u in units) if (u != null && u.IsAlive) pos0[u] = u.transform.position;
        yield return new WaitForSeconds(2.5f);
        var speeds = new List<float>();
        foreach (var kv in pos0)
        {
            var u = kv.Key; if (u == null) continue;
            float cells = Vector2.Distance(kv.Value, u.transform.position) / IsoCellDiag;
            float cps = cells / 2.5f;
            if (cps > 0.15f) speeds.Add(cps);
        }
        speeds.Sort();
        float med = speeds.Count > 0 ? speeds[speeds.Count / 2] : -1f;
        Log($"件2 实测净位移速中位={med:F3} 格/秒（n={speeds.Count}·区间 {(speeds.Count > 0 ? speeds[0] : 0f):F2}~{(speeds.Count > 0 ? speeds[speeds.Count - 1] : 0f):F2}·HH.278 基线 2.59）",
            med > 0.6f && med < 1.6f);

        // 视觉佐证：Game 窗口截图（落 Logs/hh280_live.png）
        string shotPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh280_live.png"));
        ScreenCapture.CaptureScreenshot(shotPath);
        yield return null;   // 等一帧让截图落盘再收尾退 Play
        Log($"件1 视觉佐证：Game 窗口截图 → {shotPath}", System.IO.File.Exists(shotPath));

        Finish(host);
    }

    static void Finish(ProbeHost host)
    {
        Log($"收尾：PASS={_pass} FAIL={_fail} · ExitTestRun + QuitSmoke + 退 Play（L-32）", _fail == 0);
        TestHarnessApi.ExitTestRun();
        try { SmokeApi.QuitSmoke(); } catch { }
        EditorApplication.ExitPlaymode();
        if (host != null) Object.Destroy(host.gameObject);
    }
}
#endif
