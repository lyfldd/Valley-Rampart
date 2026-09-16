#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// HH.294 片1-C：**F-10 序列帧周期性卡顿实测**（Editor-only · 正门 `TestHarnessApi.EnterTestRun` · 只测不改生产代码）。
///
/// 口径（台账 §九 F-10 判据）：
///   ① spike 间隔是否恒定（≈1/fps 逐帧写 ／ ≈动画时长 one-shot 完成 ／ ≈派工节拍）
///   ② 归因（帧耗时 × sprite 写次数 × GC 相关性）
///   ③  同帧 sprite 写入次数分布（双峰＝批量同步成立）
///   ④ 诱发实验：同帧批量触发（one-shot 同起）⇒ 观察「集中写 + 集中回调」
/// 读法：`Time.unscaledDeltaTime`（真实帧时长）；sprite 写＝逐帧比对 SpriteRenderer.sprite 实例变化（脏写跳过等价物）。
/// 用法（MCP）：Play 内 `Valley_HH294_FrameProbe.Run();`；证据 `Logs/hh294_frame_probe.log`。自收尾 ExitTestRun+QuitSmoke。
/// </summary>
public static class Valley_HH294_FrameProbe
{
    const int Seed = 21107;
    const int PhaseAFrames = 900;      // 自然载采样帧数（≈15s@60fps）
    const int PhaseBFrames = 240;      // 诱发载采样帧数
    const int BatchSize = 40;          // 每类诱发电位数（战士=once / 工人=loop）

    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath;

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { UnityEngine.Debug.LogError("[HH294F] 须先 GameScene 进 Play（正门 EnterTestRun）。"); return; }
        _log.Clear();
        var host = new GameObject("HH294_FrameProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine());
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = Seed, worldSeed = Seed, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = "smoke_hh294f", kingdomName = "河谷王国"
        };
        Line("F-10 实测启动（正门 EnterTestRun · seed=" + Seed + " · 1x 真实时序）");
        yield return TestHarnessApi.EnterTestRun(cfg, 1f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null || KingdomRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Line("[FAIL] 等世界就绪超时"); Finish(); yield break; }
        }
        yield return new WaitForSeconds(1.0f);   // 稳态

        var drv = SpriteAnimatorDriver.Instance;
        var probe = new AnimWatcher();
        probe.Rebuild();

        // ================= Phase A：自然载 =================
        Line("");
        Line("【Phase A · 自然载】driver 活跃动画槽=" + (drv != null ? drv.ActiveCount : -1)
             + " | tier Active=" + (drv != null ? drv.CountTier(0) : -1)
             + " Semi=" + (drv != null ? drv.CountTier(1) : -1)
             + " Dormant=" + (drv != null ? drv.CountTier(2) : -1)
             + " | state idle/walk/attack=" + (drv != null ? drv.CountState(SpriteAnimator.StIdle) : -1)
             + "/" + (drv != null ? drv.CountState(SpriteAnimator.StWalk) : -1)
             + "/" + (drv != null ? drv.CountState(SpriteAnimator.StAttack) : -1));

        var a = new Sample();
        a.Begin(PhaseAFrames);
        int frame = 0;
        while (a.n < PhaseAFrames)
        {
            frame++;
            if (frame % 30 == 0) probe.Rebuild();          // 每 30 帧重扫一次（单位池增删）
            yield return null;                              // 帧边界
            a.Push(probe.CountWrites(out long probeMs), probeMs, probe.Count, drv);
        }
        a.End("Phase A 自然载");

        // ================= Phase B：批量诱发 =================
        Line("");
        Line("【Phase B · 批量诱发（同帧批量触发 · one-shot 同起）】");
        // LOD 焦点＝**相机中心**（CameraRig.Update 每帧 SetFocalCenter(transform.position) ⇒ 必须在相机处布点，
        //   否则落 Dormant 档 scale=0 ⇒ 动画冻结、one-shot 永不完成 —— 首跑即踩，见串测记录）
        Vector2 basePos = CameraRig.Instance != null ? (Vector2)CameraRig.Instance.transform.position : PlayerCastlePos();
        Line("  布点中心（相机中心）=" + basePos.ToString("0.00") + " ｜ 城堡兜底点=" + PlayerCastlePos().ToString("0.00"));
        var warriors = new List<SpriteAnimator>();
        var workers = new List<SpriteAnimator>();
        for (int i = 0; i < BatchSize; i++)
        {
            var gw = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Warrior, basePos + new Vector2((i % 10) * 0.22f, (i / 10) * 0.22f), 0);
            if (gw != null) { var br = gw.GetComponent<NPCBrain>(); if (br != null) br.enabled = false; var an = gw.GetComponent<SpriteAnimator>(); an.ResetForReuse(); an.EnsureSet(); warriors.Add(an); }
            var gk = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker, basePos + new Vector2(-1.2f + (i % 10) * 0.22f, -1.2f + (i / 10) * 0.22f), 0);
            if (gk != null) { var bk = gk.GetComponent<NPCBrain>(); if (bk != null) bk.enabled = false; var an = gk.GetComponent<SpriteAnimator>(); an.ResetForReuse(); an.EnsureSet(); workers.Add(an); }
        }
        probe.Rebuild();
        int wActive = 0; for (int i = 0; i < warriors.Count; i++) if (warriors[i].LodTier == 0) wActive++;
        Line("  诱发电位：warrior=" + warriors.Count + "（once 同起） worker=" + workers.Count + "（attack loop）"
             + " | driver 活跃=" + (drv != null ? drv.ActiveCount : -1)
             + " | 探针 warrior Active档=" + wActive + "/" + warriors.Count + "（须=全数，否则动画冻结不可判）");
        yield return new WaitForSeconds(0.5f);

        int cbCount = 0;
        for (int i = 0; i < warriors.Count; i++) warriors[i].SubscribeComplete(() => cbCount++);

        // 同帧批量触发（一帧内全部下发）
        for (int i = 0; i < workers.Count; i++) workers[i].NotifyWork();
        for (int i = 0; i < warriors.Count; i++) warriors[i].NotifyAttack();

        var b = new Sample();
        b.Begin(PhaseBFrames);
        b.Warriors = warriors;
        int frame2 = 0;
        while (b.n < PhaseBFrames)
        {
            frame2++;
            if (frame2 % 30 == 0) probe.Rebuild();
            yield return null;
            int cb = cbCount; cbCount = 0;                  // 本帧回调数
            b.Push(probe.CountWrites(out long pms), pms, probe.Count, drv, cb);
        }
        b.End("Phase B 批量诱发");

        Line("");
        Line("【交叉观察】");
        Line("  Phase B 同帧最大 sprite 写=" + b.maxWrites + "（诱发单位数=" + (warriors.Count + workers.Count) + "）"
             + " | 同帧最大完成回调=" + b.maxCallbacks + "（回调总数=" + b.totalCallbacks + "）");
        Line("  判读：同帧写峰值 ≈ 诱发单位数 ⇒ 「同起同止」批量同步成立（判据③双峰右峰）");

        Finish();
    }

    // ================= 采样器 =================

    class Sample
    {
        public int n;
        public List<float> ms = new List<float>();
        public List<int> writes = new List<int>();
        public List<int> cbs = new List<int>();
        public long probeCostMicro;
        public int maxWrites, maxCallbacks;
        public int totalCallbacks;
        public int activeSlots;

        public void Begin(int cap) { n = 0; _gc0 = System.GC.CollectionCount(0); _gc1 = System.GC.CollectionCount(1); _gc2 = System.GC.CollectionCount(2); }
        public List<SpriteAnimator> Warriors;
        public int maxAttackers, maxExits, exitFrames;
        int _lastAttackers = -1;
        int _gc0, _gc1, _gc2;
        public int gc0Total, gc1Total, gc2Total;
        public int spikeWithGc, spikeTotal;

        public void Push(int w, long probeUs, int active, SpriteAnimatorDriver drv, int cb = -1)
        {
            float f = Time.unscaledDeltaTime;
            float msNow = f * 1000f;
            ms.Add(msNow);
            writes.Add(w);
            cbs.Add(cb);
            probeCostMicro += probeUs;
            if (w > maxWrites) maxWrites = w;
            if (cb > maxCallbacks) maxCallbacks = cb;
            if (cb > 0) totalCallbacks += cb;
            activeSlots = active;

            // GC 增量
            int g0 = System.GC.CollectionCount(0), g1 = System.GC.CollectionCount(1), g2 = System.GC.CollectionCount(2);
            int d0 = g0 - _gc0; _gc0 = g0; gc0Total += d0;
            gc1Total += g1 - _gc1; _gc1 = g1;
            gc2Total += g2 - _gc2; _gc2 = g2;

            // 攻击态在岗数（集中回落观测）
            if (Warriors != null && Warriors.Count > 0)
            {
                int att = 0;
                for (int i = 0; i < Warriors.Count; i++) if (Warriors[i] != null && Warriors[i].CurrentStateId == SpriteAnimator.StAttack) att++;
                if (att > maxAttackers) maxAttackers = att;
                if (_lastAttackers > 0 && att < _lastAttackers)
                {
                    int exited = _lastAttackers - att;
                    if (exited > maxExits) maxExits = exited;
                    exitFrames++;
                }
                _lastAttackers = att;
            }
            n++;
        }

        public void End(string tag)
        {
            var sorted = new List<float>(ms); sorted.Sort();
            float p50 = Pct(sorted, 0.50f), p95 = Pct(sorted, 0.95f), p99 = Pct(sorted, 0.99f);
            float mean = 0f; for (int i = 0; i < ms.Count; i++) mean += ms[i]; mean /= Mathf.Max(1, ms.Count);
            float max = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0f;

            int zero = 0, small = 0, mid = 0, big = 0, huge = 0;
            long sumW = 0;
            for (int i = 0; i < writes.Count; i++)
            {
                int w = writes[i]; sumW += w;
                if (w == 0) zero++; else if (w <= 4) small++; else if (w <= 20) mid++; else if (w <= 100) big++; else huge++;
            }

            // spike 帧（> p50 + 3×(p95−p50)）及其间隔
            float thr = p50 + 3f * Mathf.Max(0.01f, p95 - p50);
            var spikeIdx = new List<int>();
            for (int i = 0; i < ms.Count; i++) if (ms[i] > thr) spikeIdx.Add(i);
            var gaps = new List<int>();
            for (int i = 1; i < spikeIdx.Count; i++) gaps.Add(spikeIdx[i] - spikeIdx[i - 1]);
            string gapStr = gaps.Count == 0 ? "n/a" : ("n=" + gaps.Count + " 中位=" + Median(gaps) + " 帧 范围=" + Min(gaps) + "~" + Max(gaps));

            // 写量与帧耗时相关性（分桶均值）
            float msWhenZero = 0f; int nZero = 0; float msWhenW = 0f; int nW = 0;
            for (int i = 0; i < ms.Count; i++) { if (writes[i] == 0) { msWhenZero += ms[i]; nZero++; } else { msWhenW += ms[i]; nW++; } }

            Line("");
            Line("【" + tag + "】帧数=" + n + " 活跃槽=" + activeSlots
                 + " | 帧耗时 p50=" + p50.ToString("0.00") + "ms p95=" + p95.ToString("0.00")
                 + " p99=" + p99.ToString("0.00") + " max=" + max.ToString("0.00") + " mean=" + mean.ToString("0.00") + "ms");
            Line("  sprite 写/帧：总=" + sumW + " 均值=" + (sumW / (float)Mathf.Max(1, n)).ToString("0.0")
                 + " | 分布 0写=" + zero + " 帧(" + PctI(zero, n) + "%) 1~4=" + small + " 5~20=" + mid
                 + " 21~100=" + big + " >100=" + huge + " | 单帧峰=" + maxWrites);
            Line("  spike（>p50+3σ'）帧数=" + spikeIdx.Count + "（" + PctI(spikeIdx.Count, n) + "%）≥2×p50 帧数=" + CountAbove(ms, p50 * 2f)
                 + " | 间隔：" + gapStr);
            Line("  帧耗时对照：0 写帧 mean=" + (nZero > 0 ? (msWhenZero / nZero).ToString("0.00") : "n/a") + "ms（n=" + nZero + "）"
                 + " 有写帧 mean=" + (nW > 0 ? (msWhenW / nW).ToString("0.00") : "n/a") + "ms（n=" + nW + "）");
            Line("  GC 增量（本窗）：Gen0=" + gc0Total + " Gen1=" + gc1Total + " Gen2=" + gc2Total
                 + "（Gen0 每帧均 " + (gc0Total / (float)Mathf.Max(1, n)).ToString("0.00") + "）");
            if (Warriors != null && Warriors.Count > 0)
                Line("  攻击态观测：峰值在岗=" + maxAttackers + "/" + Warriors.Count + " ｜ 回落事件帧数=" + exitFrames
                     + "（<" + n + "=非同帧集体回落）｜ 单帧最大回落=" + maxExits);
            Line("  探针自身开销：均 " + (probeCostMicro / (float)Mathf.Max(1, n)).ToString("0.0") + "µs/帧（比对 sprite 实例 id 的计数成本，可从帧耗时扣除）");
            _lastP50 = p50; _lastP95 = p95;
        }

        public static float _lastP50, _lastP95;
    }

    static string PctI(int a, int b) => b > 0 ? (100f * a / b).ToString("0.0") : "0";
    static int CountAbove(List<float> v, float t) { int c = 0; for (int i = 0; i < v.Count; i++) if (v[i] > t) c++; return c; }
    static float Pct(List<float> s, float p) { if (s.Count == 0) return 0f; int i = Mathf.Clamp((int)(s.Count * p), 0, s.Count - 1); return s[i]; }
    static int Median(List<int> v) { if (v.Count == 0) return 0; var c = new List<int>(v); c.Sort(); return c[c.Count / 2]; }
    static int Min(List<int> v) { int m = int.MaxValue; for (int i = 0; i < v.Count; i++) if (v[i] < m) m = v[i]; return m; }
    static int Max(List<int> v) { int m = 0; for (int i = 0; i < v.Count; i++) if (v[i] > m) m = v[i]; return m; }

    // ================= sprite 写计数（sprite 实例 id 比对） =================

    class AnimWatcher
    {
        public List<SpriteAnimator> anims = new List<SpriteAnimator>();
        public List<SpriteRenderer> srs = new List<SpriteRenderer>();
        public List<int> lastIds = new List<int>();

        public int Count;
        static FieldInfo _objsField;

        public void Rebuild()
        {
            if (_objsField == null)
                _objsField = typeof(SpriteAnimatorDriver).GetField("_objs", BindingFlags.NonPublic | BindingFlags.Instance);
            var drv = SpriteAnimatorDriver.Instance;
            var objs = drv != null && _objsField != null ? _objsField.GetValue(drv) as List<SpriteAnimator> : null;
            anims.Clear(); srs.Clear(); lastIds.Clear();
            if (objs == null) return;
            for (int i = 0; i < objs.Count; i++)
            {
                var a = objs[i];
                if (a == null) continue;
                var sr = a.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                anims.Add(a); srs.Add(sr);
                lastIds.Add(sr.sprite != null ? sr.sprite.GetInstanceID() : 0);
            }
            Count = anims.Count;
        }

        public int CountWrites(out long probeMicro)
        {
            long t0 = Stopwatch.GetTimestamp();
            int w = 0;
            for (int i = 0; i < srs.Count; i++)
            {
                int id = srs[i] != null && srs[i].sprite != null ? srs[i].sprite.GetInstanceID() : 0;
                if (id != lastIds[i]) { w++; lastIds[i] = id; }
            }
            probeMicro = (Stopwatch.GetTimestamp() - t0) * 1000000L / Stopwatch.Frequency;
            return w;
        }
    }

    static Vector2 PlayerCastlePos()
    {
        var reg = BuildingRegistry.Instance;
        if (reg != null)
            foreach (var b in reg.All)
                if (b != null && b.sourceType == BuildingType.CastleCore && b.kingdomId == 0)
                    return (Vector2)b.transform.position + new Vector2(3f, 0f);
        return Vector2.zero;
    }

    static void Finish()
    {
        Line("");
        Line("==== HH.294 片1-C 收尾（ExitTestRun + QuitSmoke）====");
        Flush();
        try { TestHarnessApi.ExitTestRun(); } catch { }
        try { SmokeApi.QuitSmoke(); } catch { }
    }

    static void Line(string s) { _log.AppendLine(s); UnityEngine.Debug.Log("[HH294F] " + s); Flush(); }
    static void Flush()
    {
        try
        {
            if (_logPath == null)
                _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh294_frame_probe.log"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_logPath));
            System.IO.File.WriteAllText(_logPath, _log.ToString());
        }
        catch { }
    }
}
#endif