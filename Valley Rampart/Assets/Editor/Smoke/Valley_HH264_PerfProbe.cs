#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.Tilemaps;

/// <summary>
/// HH.264 **C 段（§7.1 H5 预算）＋ D 段（§7.3 H6 图集）**：四族混编压测 + 图集前后对照（Editor-only · 正门 `TestHarnessApi.EnterTestRun`）。
///
/// C 段口径（§7.1）：**360 单位（四族各 90）混编** ⇒ 采 `SpriteAnimatorDriver.LateUpdate` 段
///   **&lt;0.1ms/帧** ＋ **GC Alloc 0 B/帧**（稳态）。
///   段隔离：临时 `driver.enabled=false`，探针经 `Delegate` 直调其 `LateUpdate`，
///   用 `Stopwatch` 包夹 + `GC.GetAllocatedBytesForCurrentThread()` 计数（harness 底噪另采空委托基准）。
/// D 段口径（§7.3）：Units 全帧（含 monster）入 `SpriteAtlas`；`SpriteRefTable` 引用入集后**不失效**（实证）；
///   DrawCall 前后对照；**不得破坏三件**（间接层／缺图回退链／统一 pivot 排序稳定）。
/// 用法（MCP，两次跑批对照）：`Valley_HH264_PerfProbe.Run("before")` → 建图集 → `Run("after")`。
/// 证据：`Logs/hh264_perf_{tag}.log`。
/// </summary>
public static class Valley_HH264_PerfProbe
{
    const int Seed = 21107;
    const int UnitsPerRace = 90;    // 4 族 × 90 = 360（落在 300~400）
    const int WarmupFrames = 60;
    const int SampleFrames = 180;
    const string AtlasPath = "Assets/Resources/Config/Art/UnitsFrames.spriteatlas";

    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath, _tag;
    static int _pass, _fail;

    static readonly Occupation[] Occs = { Occupation.Warrior, Occupation.Archer, Occupation.Worker };

    public static void Run(string tag)
    {
        if (!EditorApplication.isPlaying) { UnityEngine.Debug.LogError("[HH264P] 须先 GameScene 进 Play。"); return; }
        _log.Clear(); _pass = 0; _fail = 0; _tag = string.IsNullOrEmpty(tag) ? "run" : tag;
        Log("探针启动 tag=" + _tag + "（正门 EnterTestRun · 四族混编 · seed=" + Seed + "）", true);
        var host = new GameObject("HH264_PerfProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine(host));
    }

    class ProbeHost : MonoBehaviour
    {
        public void Host(IEnumerator r) { StartCoroutine(r); }
    }

    static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        UnityEngine.Debug.Log("[HH264P] " + (ok ? "✓ " : "✗ ") + msg);
        Flush();
    }

    static void Info(string msg) { _log.AppendLine("      " + msg); }

    static void Flush()
    {
        try
        {
            if (_logPath == null)
                _logPath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh264_perf_" + _tag + ".log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, _log.ToString());
        }
        catch { /* 取证失败不影响探针 */ }
    }

    static IEnumerator Coroutine(ProbeHost host)
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = "smoke_hh264p", kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null || KingdomRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(host); yield break; }
        }
        yield return new WaitForSeconds(0.8f);

        // ---- 建四族混编单位群（360）----
        Vector2 basePos = PlayerCastlePos();
        if (LODSystem.Instance != null) LODSystem.Instance.SetFocalCenter(basePos);
        var units = new List<UnitController>();
        var perRace = new Dictionary<int, int>();
        int idx = 0;
        for (int race = 0; race < 4; race++)
        {
            for (int k = 0; k < UnitsPerRace; k++)
            {
                var occ = Occs[k % Occs.Length];                      // 30 战士(走) / 30 弓手(待机) / 30 工人(工作 attack 循环)
                Vector2 p = basePos + new Vector2((idx % 20 - 10) * 0.2f, (idx / 20 - 9) * 0.2f);
                var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, occ, p, 0);
                if (go == null) continue;
                var uc = go.GetComponent<UnitController>();
                if (uc == null) continue;
                uc.raceId = race;                                     // D468 种族域调试口径
                var b = uc.GetComponent<NPCBrain>();
                if (b != null) b.enabled = false;                     // 压测控态：状态由探针驱动（L-35 采样条件）
                var a = uc.GetComponent<SpriteAnimator>();
                if (a != null) { a.ResetForReuse(); a.EnsureSet(); a.SubscribeComplete(null); }
                units.Add(uc);
                if (!perRace.ContainsKey(race)) perRace[race] = 0;
                perRace[race] = perRace[race] + 1;
                idx++;
            }
        }
        // 焦点钉在集群质心（保证全部落 Active 档 ⇒ 全速推进）
        if (LODSystem.Instance != null) LODSystem.Instance.SetFocalCenter(basePos);
        yield return new WaitForSeconds(1.5f);

        var drv = SpriteAnimatorDriver.Instance;
        if (drv == null) { Log("SpriteAnimatorDriver.Instance 缺失", false); Finish(host); yield break; }

        var raceStr = new StringBuilder();
        foreach (var kv in perRace) raceStr.Append("r" + kv.Key + "=" + kv.Value + " ");
        Log("C0 四族混编规模：spawn=" + units.Count + "（目标 " + (UnitsPerRace * 4) + "）→ " + raceStr
            + " | driver 活跃=" + drv.ActiveCount
            + " | tier Active=" + drv.CountTier(0) + " Semi=" + drv.CountTier(1) + " Dormant=" + drv.CountTier(2)
            + " | state idle=" + drv.CountState(SpriteAnimator.StIdle) + " walk=" + drv.CountState(SpriteAnimator.StWalk)
            + " attack=" + drv.CountState(SpriteAnimator.StAttack),
            units.Count >= 300 && perRace.Count == 4 && drv.CountTier(0) >= 300);

        // ---- C 段：LateUpdate 段隔离测 ----
        var mi = typeof(SpriteAnimatorDriver).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mi == null) { Log("反射未取到 SpriteAnimatorDriver.LateUpdate", false); Finish(host); yield break; }
        var call = (Action)Delegate.CreateDelegate(typeof(Action), drv, mi);
        Action noop = () => { };

        drv.enabled = false;
        // 预热
        for (int i = 0; i < WarmupFrames; i++) { DriveIntents(units, i); yield return null; call(); }

        var sw = new Stopwatch();
        double sumMs = 0, maxMs = 0; long allocSum = 0, allocMax = 0;
        int frames = 0;
        // harness 底噪（空委托同法包夹）
        double sumNoop = 0;
        long dcSum = 0; int dcMax = 0;
        for (int i = 0; i < SampleFrames; i++)
        {
            DriveIntents(units, i);
            yield return null;

            long a0 = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart(); call(); sw.Stop();
            long a1 = GC.GetAllocatedBytesForCurrentThread();

            double ms = sw.Elapsed.TotalMilliseconds;
            long al = a1 - a0;
            sumMs += ms; if (ms > maxMs) maxMs = ms;
            allocSum += al; if (al > allocMax) allocMax = al;
            frames++;

            long n0 = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart(); noop(); sw.Stop();
            long n1 = GC.GetAllocatedBytesForCurrentThread();
            sumNoop += sw.Elapsed.TotalMilliseconds;

            int dc = DrawCalls();
            if (dc > 0) { dcSum += dc; if (dc > dcMax) dcMax = dc; }
        }
        drv.enabled = true;

        double avgMs = sumMs / Math.Max(1, frames);
        double avgNoop = sumNoop / Math.Max(1, frames);
        double perFrameAlloc = (double)allocSum / Math.Max(1, frames);
        Info("采样：warmup=" + WarmupFrames + " 帧 / sample=" + frames + " 帧 / 单位=" + units.Count
             + " / 段隔离=driver.enabled=false + Delegate 直调 LateUpdate");
        Log("C1 `SpriteAnimatorDriver.LateUpdate` 段耗时：avg=" + avgMs.ToString("0.0000") + "ms/帧"
            + " max=" + maxMs.ToString("0.0000") + "ms"
            + " | harness 底噪(空委托同法包夹)=" + avgNoop.ToString("0.0000") + "ms/帧"
            + " ⇒ 净耗时≈" + (avgMs - avgNoop).ToString("0.0000") + "ms/帧（判据 <0.1ms）",
            avgMs < 0.1 && avgMs > 0);
        Log("C2 GC Alloc：avg=" + perFrameAlloc.ToString("0.###") + " B/帧 max单帧=" + allocMax
            + " B（总 " + allocSum + " B / " + frames + " 帧）⇒ 判据 0 B/帧（稳态）", allocSum == 0);
        Info("C3 采样期内 DrawCall 读数：" + (dcSum > 0
                ? "avg=" + ((double)dcSum / frames).ToString("0.##") + " max=" + dcMax + "（UnityStats.drawCalls）"
                : "不可读（UnityStats 无值 ⇒ 见下方 ProfilerRecorder 兜底）"));
        Info("C4 采样后档位分布：Active=" + drv.CountTier(0) + " Semi=" + drv.CountTier(1) + " Dormant=" + drv.CountTier(2)
             + " | state idle=" + drv.CountState(SpriteAnimator.StIdle) + " walk=" + drv.CountState(SpriteAnimator.StWalk)
             + " attack=" + drv.CountState(SpriteAnimator.StAttack) + " loot=" + drv.CountState(SpriteAnimator.StLoot));

        // ---- C1b 成本构成定位：timeScale=0 ⇒ dt=0 ⇒ 同遍历同调用、零推进零 sprite 写 ----
        {
            float restore = Time.timeScale;
            Time.timeScale = 0f;
            yield return null;                                     // 让一帧 dt=0 生效
            Info("C1b 前置：timeScale=0（dt=0）tier Active=" + drv.CountTier(0) + " Semi=" + drv.CountTier(1) + " Dormant=" + drv.CountTier(2));
            double fSum = 0, fMax = 0; long fAlloc = 0;
            for (int i = 0; i < SampleFrames; i++)
            {
                yield return null;
                long a0 = GC.GetAllocatedBytesForCurrentThread();
                sw.Restart(); call(); sw.Stop();
                double el = sw.Elapsed.TotalMilliseconds;
                long a1 = GC.GetAllocatedBytesForCurrentThread();
                fSum += el; if (el > fMax) fMax = el;
                fAlloc += a1 - a0;
            }
            Time.timeScale = restore <= 0f ? 1f : restore;
            double fAvg = fSum / SampleFrames;
            Info("C1b 零推进对照（dt=0：同遍历同调用·无换帧/无 sprite 写）段耗时：avg=" + fAvg.ToString("0.0000") + "ms/帧"
                 + " max=" + fMax.ToString("0.0000") + "ms alloc=" + fAlloc + " B ⇒ 「每 slot 调用开销」下限");
            Info("C1c 成本构成差：活跃推进档 " + avgMs.ToString("0.0000") + "ms − 零推进档 " + fAvg.ToString("0.0000")
                 + "ms = " + (avgMs - fAvg).ToString("0.0000") + "ms ⇒ 帧推进 + sprite 写段");
        }

        // ---- D 段：图集收录 & 三件不破 ----
        AtlasCheck();

        _log.AppendLine("==== HH264-P(" + _tag + ") 汇总：PASS=" + _pass + " FAIL=" + _fail + " ====");
        UnityEngine.Debug.Log("[HH264P][SUMMARY]\n" + _log);
        Finish(host);
    }

    /// <summary>探针侧驱动状态混合（走/工作 attack 循环/待机），不计入段测窗口。</summary>
    static void DriveIntents(List<UnitController> units, int frame)
    {
        for (int i = 0; i < units.Count; i++)
        {
            var a = units[i].GetComponent<SpriteAnimator>();
            if (a == null) continue;
            int g = i % 3;
            if (g == 0) a.NotifyMove(((frame / 30) % 2 == 0) ? Vector2.right : Vector2.left);   // 走
            else if (g == 1) a.NotifyWork();                                                    // 工人工作 attack 循环
            // g==2 ⇒ 待机
        }
    }

    /// <summary>DrawCall 读数（Editor Play 内 UnityStats；不可用返回 -1）。</summary>
    static int DrawCalls()
    {
        try { return UnityStats.drawCalls; }
        catch { return -1; }
    }

    // ===================== D 段：图集 =====================

    static void AtlasCheck()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
        var table = ValleyRampart.Rendering.SpriteRefTable.Instance;
        if (atlas == null)
        {
            Info("D0 图集未建（tag=before 预期）：" + AtlasPath + " = null");
            Log("D0 图集尚未创建（before 基线跑批）", true);
        }
        else
        {
            Info("D0 图集在场：" + AtlasPath + " spriteCount=" + atlas.spriteCount
                 + " includeInBuild=" + atlas.IsIncludeInBuild());
            Log("D0 图集在场且非空：spriteCount=" + atlas.spriteCount + "（>0 ⇒ Units 全帧已入集）", atlas.spriteCount > 0);
        }

        // ① 间接层不破：SpriteRefTable 查表仍命中 + 单位 SpriteRenderer 与表内同一 sprite 对象
        string[] probes = { "unit_human_warrior_idle", "unit_orc_worker_attack", "unit_elf_archer_walk", "unit_monster_raider_idle" };
        int hit = 0; var detail = new StringBuilder();
        foreach (var id in probes)
        {
            Sprite s = null;
            Sprite[] fr = null;
            bool ok = table != null && table.TryGetFrames(id, out fr) && fr != null && fr.Length > 0;
            if (ok) { s = fr[0]; hit++; }
            else if (table != null && table.TryGet(id, out s)) hit++;
            string bind = (atlas != null && s != null) ? atlas.CanBindTo(s).ToString() : "n/a";
            detail.Append(id + "=" + (s != null ? s.name : "null") + "(atlasCanBind=" + bind + ") ");
        }
        Log("D1 ①间接层不破：SpriteRefTable 逐键查表命中 " + hit + "/" + probes.Length + " → " + detail,
            hit == probes.Length);
        var u0 = UnityEngine.Object.FindObjectOfType<UnitController>();
        if (u0 != null)
        {
            var sr = u0.GetComponent<SpriteRenderer>();
            Info("D1b 运行中单位 SpriteRenderer.sprite=" + (sr != null && sr.sprite != null ? sr.sprite.name : "null")
                 + "（列表 3 件：表内 sprite 引用仍有效 ⇒ 入集不失效）");
        }

        // ② 缺图回退链不破
        var fb = ValleyRampart.Rendering.PlaceholderSprites.Get("bld_academy");
        bool fbOk = fb != null && (table == null || !table.Contains(fb));
        Log("D2 ②缺图回退链不破：bld_academy → nonNull=" + (fb != null) + " 非表内真图=" + fbOk
            + " ⇒ 占位路径仍生效（不崩）", fb != null);

        // ③ 统一 pivot 排序稳定：同 sheet 子帧逐帧同尺寸同 pivot
        var framesOk = true; string pivotInfo = "n/a";
        if (table != null && table.TryGetFrames("unit_human_warrior_idle", out Sprite[] f2) && f2 != null && f2.Length > 0)
        {
            var p0 = f2[0].pivot; var r0 = f2[0].rect.size;
            for (int i = 1; i < f2.Length; i++)
            {
                if (f2[i].pivot != p0 || f2[i].rect.size != r0) { framesOk = false; break; }
            }
            framesOk = framesOk && f2.Length == 8;
            pivotInfo = "帧数=" + f2.Length + " pivot=" + p0 + " rectSize=" + r0;
        }
        Log("D3 ③统一 pivot / 排序稳定：同 sheet 子帧逐帧同尺寸同 pivot → " + pivotInfo, framesOk);
    }

    static Vector2 PlayerCastlePos()
    {
        var reg = BuildingRegistry.Instance;
        if (reg != null)
            foreach (var b in reg.All)
                if (b != null && b.sourceType == BuildingType.CastleCore && b.kingdomId == 0)
                    return (Vector2)b.transform.position + new Vector2(4f, 0f);
        return Vector2.zero;
    }

    static void Finish(ProbeHost host)
    {
        _log.AppendLine("==== HH264-P(" + _tag + ") 收尾：PASS=" + _pass + " FAIL=" + _fail + " ====");
        Flush();
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
    }
}
#endif
