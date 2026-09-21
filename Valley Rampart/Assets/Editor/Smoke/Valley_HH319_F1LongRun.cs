using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.319 `M1-F` · `F-1` 补证单（`D809`）件 1：**真实长局观察**（生产路径搬水链）
//  用法：菜单「Valley/验证/HH319 F1长局观察_1x」／「…_15x对照」——进 Play 后点（或 MCP 触菜单）。
//  ⛔ 本容器**不干预**搬水链：只做「初始布置 ＋ 观测」（除"井仓水位压制"以维持靶例 · 见下）。
//  ⭐ 件1 核心问题：15× 档下 dt 可达钳制上限 1.0s/帧 ⇒ 寻路/到达判定**跳步**（假设）
//     ⇒ 故 1× 复跑对照；1× 与 15× 读数不同 ⇒ 该假设成立（须明确报出）。
//  收尾：ExitTestRun ＋ QuitSmoke（⛔ 不留 1× 余留世界 · L-32）。
//
// ============================================================================
//  ⭐⭐ `HH.319` · `U-16` 取证探针修复批（`D812` · P1~P3）—— **本片新增，仅 Editor 侧**
//  `D812` 裁决：`U-16` 归因＝甲′「寻路失败 ⇒ `PathFailedEvent` 同步 ⇒ 派发同帧静默 `Abandon`」，
//  但强度仅"唯一自洽假说"⇒ 三处取证缺口：① console 无留档 ② E2 是否真未命中未经落盘
//  ③ 逐帧 state 时序缺。本片补 P1~P3（⛔ `Assets/_Game/**` 一行不动）。
//
//  P1 采样锚定：⛔ 弃用"距井最近者 nearest"当锚（与被派工者零绑定）⇒ 改为**按「被派 npcId」逐帧打点**。
//     被派 npcId 来源两路：① 每帧扫 `_npcTaskMap` 取 WaterHaul 条目 ② 抓 `[TaskScheduler] 派发` 日志
//     （⭐ 后者能抓到"派发帧内即被清"、① 抓不到的那批 npc）⇒ 两路并集＝`_tracked`。
//     E2（"被派工者"的正确锚）**保留**，并额外打印**每帧命中/未命中计数**。
//  P2 直接证甲′：反射只读 `PathFollower._state`(`:19`)/`_consecutiveFails`(`:23`)/`_destination`(`:17`)
//     ＋ `GridSystem.IsSubWalkable(站位微格)` ＋ **A\* 复演**（`PathfindingService.FindPathImmediate`，
//     与生产 `NavigateToSource` 同调用面 · 纯查询无副作用）⇒ **一条读数即可区分甲′（路径失败）vs 乙（被抢占）**。
//  P3 console 全量落盘（第一优先）：`Application.logMessageReceived` 镜像到 `Logs/hh319_u16/hh319_u16_{tag}.log`
//     ＋ `EventBus.Subscribe<PathFailedEvent>` **只读计数**（按 npcId 分桶）。
//     ⚠️ 落盘口径"报选"（见交付报告 §三-D）：**白名单 tag ＋ 关键词（WaterHaul/PathFailed）＋ Error/Warning/
//        Exception/Assert 全量**（先例＝`Valley_P1_Observer.cs:109-119` 同类镜像；字面全量会引入 IO 压力与噪音）。
//
//  ⚠️ **口径演进登记（本片 · 单列）**：① seq 行由「距井最近者」改为「按被派 npcId」逐帧
//     （⛔ 旧口径的 `st=None · 距井 5.25` 读数**不可与新读数直接对拍**）；
//     ② 新增 `[HH319U16·dispatch/seq/trans/pathfail/summary]` 系列行（**只增不改**）；
//     ③ E2/E4/E5/E6 与判据语义**一字未改**；
//     ④ 【二跑补读 · 2026-09-21】`Walkable()` 由「True/False」升级为**带成因鉴别**
//        （`False（占格物阻断：<名>）` ／ `False（地形不可走）`）—— 只增信息、不改判据；
//        ⛔ 与首跑（r1）的 `站位格可走=False` 属**同口径扩展**，r1 存档为 `hh319_u16_15x_r1.log`。
// ============================================================================
public static class Valley_HH319_F1LongRun
{
    private const int SEED = 20319;

    // ---- `U-16` 取证（P1~P3）常量 ----
    private const string U16Tag = "[HH319U16";
    private const int TrackCap = 6;                     // 逐帧打点的 npc 上限（防日志爆炸）
    private static readonly string[] MirrorPrefixes =
    {
        "[TaskScheduler]", "[调度中心]", "[HH319", "[NPCBrain]", "[EventBus]", "[SpawnPosSnapper]"
    };
    private static readonly string[] MirrorKeywords = { "WaterHaul", "PathFailed" };

    [MenuItem("Valley/验证/HH319 F1长局观察_1x")]
    public static void Run1x()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH319长局] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH319_LongRunHost").AddComponent<RunHost>().Host(RunCoroutine(1f, "1x"));
    }

    [MenuItem("Valley/验证/HH319 F1长局观察_15x对照")]
    public static void Run15x()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH319长局] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH319_LongRunHost").AddComponent<RunHost>().Host(RunCoroutine(15f, "15x"));
    }

    private class RunHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    private static UnitController _w;

    private static IEnumerator RunCoroutine(float speed, string tag)
    {
        float duration = speed <= 1f ? 180f : 45f;    // 1× ⇒ ≥180 真实秒（任务书 §件1）；15× 对照 ⇒ 45s（足够多轮）
        U16Begin(tag);                                // ⭐ P3：先挂钩（⛔ 早于 EnterTestRun，防漏世界创建期日志）
        Debug.Log($"[HH319长局·{tag}] ── 起（speed={speed} · 观测 {duration}s 真实时间 · 生产路径 · ⛔ 不干预搬水链）");
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_w319L" + tag, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, speed);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 2)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f)
            { Debug.LogError($"[HH319长局·{tag}] 等世界就绪超时。"); yield return Finish(tag, null); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        // ---- 初始布置（生产对象 · ⛔ 不走反射直调）----
        Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        var well = Place("Buildings/Well", anchor + new Vector2(-6f, 0f), 0);
        var farm = Place("Buildings/farm", anchor + new Vector2(6f, 0f), 0);
        SpawnWorker(anchor + new Vector2(-1f, 1.6f), 0);
        SpawnWorker(anchor + new Vector2(1f, -1.6f), 0);
        if (well == null || farm == null)
        {
            Debug.LogError($"[HH319长局·{tag}] 布置失败 well={well != null} farm={farm != null} ⇒ 中止。");
            yield return Finish(tag, null); yield break;
        }
        var wellStore = well.GetComponent<StorageComponent>();
        var farmStore = farm.GetComponent<StorageComponent>();
        TestFixtureApi.AddWaterToKingdomWells(0, 50);                      // 井仓给 50 水（< cap×0.8 ⇒ ⛔ 不触发井的 Transport 广告）
        farmStore.TakeOut(ResourceType.Water, farmStore.GetAmount(ResourceType.Water));   // 农场缺水 ⇒ 广告 WaterHaul
        Debug.Log($"[HH319长局·{tag}] 布置：well@{well.transform.position} farm@{farm.transform.position} "
                  + $"井仓={wellStore.GetAmount(ResourceType.Water)}/{wellStore.capacity} 农场仓水={farmStore.GetAmount(ResourceType.Water)} "
                  + $"δt(帧)={Time.smoothDeltaTime:F3}s timeScale={Time.timeScale}");
        U16Write($"{U16Tag}·place] {Stamp()} well@{well.transform.position} farm@{farm.transform.position} "
                 + $"井仓={wellStore.GetAmount(ResourceType.Water)} 农场仓水={farmStore.GetAmount(ResourceType.Water)}");

        // ---- 观测 ----
        var ev = new List<string>();
        bool e2 = false, e3 = false, e4 = false, e5 = false, e6 = false;
        float t2 = 0, t3 = 0, t4 = 0, t5 = 0, t6 = 0;
        int bagPeak = 0, farmWaterPeak = 0, wellMin = wellStore.GetAmount(ResourceType.Water);
        int loadDrop = 0, unloadGain = 0;
        int adHit = 0;
        float nextDiag = 0f, nextSuppress = 0f, nextAdv = 0f;
        bool completed = false;
        float start = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - start < duration)
        {
            yield return null;
            float rt = Time.realtimeSinceStartup - start;

            // ⭐ 靶例维持（只减不增）：井仓 >30 ⇒ 压回 20（**远低于 80% Transport 阈值** ⇒ 保 WaterHaul 靶例）
            //   ⚠️ v1 教训：压回 50 时井产水会把仓顶到 82（>80%）⇒ 井自己广告 Transport 抢走靶例（实测已发生）。
            if (rt >= nextSuppress)
            {
                nextSuppress = rt + 0.25f;   // ⚠️ `U-15` 轮次教训：15× 下 3s 周期压不住（3s≈45 游戏秒·产水 180 ⇒ 井仓常满 ⇒ Transport 抢先）
                int ww2 = wellStore.GetAmount(ResourceType.Water);
                if (ww2 > 30) wellStore.TakeOut(ResourceType.Water, ww2 - 20);
            }
            // ① 广告读数（只读探针 · 每 5 真实秒一次）
            if (rt >= nextAdv)
            {
                nextAdv = rt + 5f;
                if (farm.TryAdvertiseTask(out var adv) && adv != null && adv.type == KingdomTaskType.WaterHaul) adHit++;
            }

            // ⭐ 采样**全部**玩家国工人（v2 修正：v1 只采样一个 ⇒ 派工给了另一个 ⇒ 假"state=None"）
            foreach (var uc in PlayerWorkers())
            {
                var st = TaskScheduler.Instance != null ? TaskScheduler.Instance.GetWorkerState(uc.npcId) : TaskState.None;
                var inv = uc.GetComponent<WorkerInventory>();
                int bag = inv != null ? inv.carriedAmount : 0;
                float dw = Vector2.Distance(uc.transform.position, well.transform.position);
                float df = Vector2.Distance(uc.transform.position, farm.transform.position);
                bool onWater = false; string srcInfo = "-";
                foreach (var kv in ReadTasks())
                {
                    if (kv.Key != uc.npcId || kv.Value.Item1 != KingdomTaskType.WaterHaul) continue;
                    onWater = true; srcInfo = kv.Value.Item2 + "/k" + kv.Value.Item3;
                }
                if (onWater && !e2) { e2 = true; t2 = rt; ev.Add($"E2 工人被派 WaterHaul（生产路径派工 · npc{uc.npcId}）t=+{rt:F2}s 源={srcInfo}"); }
                if (onWater && !e3 && st == TaskState.Working && dw <= 3.5f)
                { e3 = true; t3 = rt; ev.Add($"E3 到达水井（Working · npc{uc.npcId}）t=+{rt:F2}s 距井={dw:F2}"); }
                if (bag > bagPeak) bagPeak = bag;
                if (!e4 && bag > 0)
                { e4 = true; t4 = rt; loadDrop = wellStore.GetAmount(ResourceType.Water); ev.Add($"E4 装载：背包={bag}（npc{uc.npcId}）t=+{rt:F2}s 井仓={loadDrop}"); }
                if (e4 && !e5 && st == TaskState.MovingToDest)
                { e5 = true; t5 = rt; ev.Add($"E5 转 MovingToDest（去农场 · npc{uc.npcId}）t=+{rt:F2}s 距农场={df:F2}"); }
                if (e5 && !e6 && bag == 0 && farmStore.GetAmount(ResourceType.Water) > 0)
                { e6 = true; t6 = rt; unloadGain = farmStore.GetAmount(ResourceType.Water); ev.Add($"E6 卸货：背包=0 · 农场仓水={unloadGain}（npc{uc.npcId}）t=+{rt:F2}s"); }
            }
            int fwNow = farmStore.GetAmount(ResourceType.Water);
            if (fwNow > farmWaterPeak) farmWaterPeak = fwNow;
            completed = e2 && e3 && e4 && e5 && e6;

            // ================================================================
            //  ⭐ `U-16` P1：**按「被派 npcId」逐帧打点**（⛔ 弃用"距井最近者"锚 · 见文件头口径演进登记）
            //  两路取锚：① 每帧扫 `_npcTaskMap` 的 WaterHaul 条目（抓得到"在册"的）
            //            ② `[TaskScheduler] 派发` 日志钩子（抓得到"派发帧内即被清"、① 抓不到的那批）
            // ================================================================
            _u16Frames++;
            bool anyOnWater = false;
            foreach (var kv in ReadTasks())
            {
                if (kv.Value.Item1 != KingdomTaskType.WaterHaul) continue;
                anyOnWater = true;
                Track(kv.Key);   // ⭐ E2 命中 ⇒ 该 npc 确曾活在 `_npcTaskMap`
            }
            if (anyOnWater) _e2HitFrames++; else if (_tracked.Count > 0) _e2MissFrames++;
            for (int ti = 0; ti < _tracked.Count; ti++) U16SeqLine(_tracked[ti], rt);
            U16Transitions(rt);

            // ② 距离序列（旧「距井最近者」口径 —— ⚠️ `U-16` 起**保留但降为旁读**，⛔ 不再作判据/归因锚）
            if (rt >= nextDiag)
            {
                nextDiag = rt + 1f;
                UnitController near = null; float nearD = float.MaxValue;
                foreach (var uc in PlayerWorkers())
                {
                    float d = Vector2.Distance(uc.transform.position, well.transform.position);
                    if (d < nearD) { nearD = d; near = uc; }
                }
                if (near != null)
                {
                    var stN = TaskScheduler.Instance != null ? TaskScheduler.Instance.GetWorkerState(near.npcId) : TaskState.None;
                    var invN = near.GetComponent<WorkerInventory>();
                    U16Write($"{U16Tag}·nearby] {Stamp()} +{rt:F1}s 最近工人 npc{near.npcId} st={stN} 距井={nearD:F2} "
                             + $"距农场={Vector2.Distance(near.transform.position, farm.transform.position):F2} "
                             + $"背包={(invN != null ? invN.carriedAmount : -1)} 井水={wellStore.GetAmount(ResourceType.Water)} 农场水={fwNow}");
                }
            }
            int wmin = wellStore.GetAmount(ResourceType.Water);
            if (wmin < wellMin) wellMin = wmin;

            if (completed && rt > t6 + 3f) break;   // 一次完整往返达成 ⇒ 命中即停（L-34）
        }

        // ---- 汇总 ----
        Debug.Log($"[HH319长局·{tag}] ===== 汇总 =====");
        Debug.Log($"[HH319长局·{tag}] ①广告命中次数={adHit}（每5s采样 · 只读探针）");
        for (int i = 0; i < ev.Count; i++) Debug.Log($"[HH319长局·{tag}] {ev[i]}");
        Debug.Log($"[HH319长局·{tag}] ⭐ 完整往返={completed}（E2~E6 齐全=" + (e2 && e3 && e4 && e5 && e6) + "）"
                  + $"｜耗时 E2→E6={(completed ? (t6 - t2) : -1):F2}s（E2→E3到井={(e3 ? t3 - t2 : -1):F2}s · E4→E6卸货={(e6 ? t6 - t4 : -1):F2}s）");
        Debug.Log($"[HH319长局·{tag}] ⭐ U-14 读数A（最终是否到达水井并完成搬水）={(completed ? "Y" : "N")}"
                  + $"｜读数B（被拉偏时长/是否自愈）=见 seq 序列（WaterHaul 被派后 state 是否长期停滞／下轮是否重派）");
        Debug.Log($"[HH319长局·{tag}] 账：背包峰={bagPeak} 农场仓水峰={farmWaterPeak} 井仓最低={wellMin} 装载时井仓={loadDrop} 卸货时农场仓={unloadGain}");
        yield return Finish(tag, null);
    }

    private static IEnumerator Finish(string tag, object _)
    {
        U16Summary(tag);   // ⭐ P3：判据留档（⛔ 先落盘再收尾，防 ExitTestRun 噪音干扰）
        U16End();
        Debug.Log($"[HH319长局·{tag}] ── 收尾（ExitTestRun ＋ 退 Play · ⛔ 不留 1× 余留世界）");
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
        yield break;
    }

    // ========================================================================
    //  ⭐ `U-16` P1~P3 取证实现（全在 Editor 侧 · 反射**只读**）
    // ========================================================================

    private static StreamWriter _u16w;
    private static string _u16Path;
    private static bool _u16Hooked;
    private static readonly List<int> _tracked = new List<int>();                       // 被派 WaterHaul 的 npcId（首次出现序）
    private static readonly Dictionary<int, int> _dispatchN = new Dictionary<int, int>();   // 派发次数（按 npc）
    private static readonly Dictionary<int, int> _completeN = new Dictionary<int, int>();   // 完成次数（按 npc）
    private static readonly Dictionary<int, int> _pathFailN = new Dictionary<int, int>();   // PathFailedEvent 次数（按 npc）
    private static readonly Dictionary<int, string> _firstDispatch = new Dictionary<int, string>();
    private static readonly Dictionary<int, string> _lastState = new Dictionary<int, string>();
    private static int _e2HitFrames, _e2MissFrames, _u16Frames;
    private static int _completeWaterHaul;    // ⭐ 判据 1 的单一判别式计数

    private static void U16Begin(string tag)
    {
        _u16Path = Path.Combine(Directory.GetCurrentDirectory(), "Logs/hh319_u16", "hh319_u16_" + tag + ".log");
        Directory.CreateDirectory(Path.GetDirectoryName(_u16Path));
        _u16w = new StreamWriter(_u16Path, false, new UTF8Encoding(false)) { AutoFlush = true };
        U16Write($"# HH.319 `M1-F` `U-16` 取证日志（P1~P3）· tag={tag} · 起 {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        U16Write($"# 落盘口径：白名单 tag〔{string.Join(" ", MirrorPrefixes)}〕＋关键词〔{string.Join(" ", MirrorKeywords)}〕＋ Error/Warning/Exception/Assert 全量");
        _tracked.Clear(); _dispatchN.Clear(); _completeN.Clear(); _pathFailN.Clear();
        _firstDispatch.Clear(); _lastState.Clear();
        _e2HitFrames = _e2MissFrames = _u16Frames = 0; _completeWaterHaul = 0;
        Application.logMessageReceived += OnLog;             // P3：console 镜像（⭐ 本批第一优先）
        EventBus.Subscribe<PathFailedEvent>(OnPathFailedEvt); // P3：PathFailedEvent 只读计数（按 npcId 分桶）
        _u16Hooked = true;
    }

    private static void U16End()
    {
        if (_u16Hooked)
        {
            Application.logMessageReceived -= OnLog;
            EventBus.Unsubscribe<PathFailedEvent>(OnPathFailedEvt);
            _u16Hooked = false;
        }
        if (_u16w != null)
        {
            U16Write($"# 封存 {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} ⇒ {_u16Path}");
            _u16w.Flush(); _u16w.Dispose(); _u16w = null;
        }
    }

    private static void U16Write(string line)
    {
        if (_u16w == null) return;
        try { _u16w.WriteLine(line); } catch { /* 落盘失败不阻断跑局 */ }
    }

    private static string Stamp() => $"f{Time.frameCount} t{Time.time:F2}";

    /// <summary>`U-16` P3：console 镜像。⚠️ 本方法**只写文件、不再 `Debug.Log`**（防重入）。</summary>
    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (_u16w == null || condition == null) return;
        bool keep = type == LogType.Error || type == LogType.Warning
                 || type == LogType.Exception || type == LogType.Assert;
        if (!keep)
        {
            for (int i = 0; i < MirrorPrefixes.Length && !keep; i++)
                if (condition.StartsWith(MirrorPrefixes[i])) keep = true;
            for (int i = 0; i < MirrorKeywords.Length && !keep; i++)
                if (condition.Contains(MirrorKeywords[i])) keep = true;
        }
        if (!keep) return;
        U16Write($"[{type}] {Stamp()} {condition}");
        // 结构化抓取（⚠️ 本处不 `Debug.Log`、不写 console ⇒ 无重入）
        if (condition.StartsWith("[TaskScheduler] 派发")) CaptureDispatch(condition);
        else if (condition.StartsWith("[TaskScheduler] 完成")) CaptureComplete(condition);
    }

    /// <summary>⭐ 判据 2/3：派发时刻的**同帧快照** —— `[TaskScheduler] 派发` 日志在 `Dispatch:365`、即
    /// `NavigateToSource(:364)` **之后**发出 ⇒ 此刻读到的 `在册(taskMap)` 若为 `False`，即**直接证明**
    /// 「记录在派发帧内被清」（甲′）。`PathFollower` 读数取 `:365` 时点值（已含本次 `FailOnce`）。</summary>
    private static void CaptureDispatch(string line)
    {
        string type = Between(line, "[TaskScheduler] 派发 ", " 任务");
        int id = ParseNpcId(line);
        if (id == 0) return;
        int n; _dispatchN.TryGetValue(id, out n); _dispatchN[id] = n + 1;
        var uc = FindUnit(id);
        string snap = $"{U16Tag}·dispatch] {Stamp()} type={type} npc={id} 第{_dispatchN[id]}次"
                      + $" 在册(taskMap)={InMap(id)} 在态(stateMap)={InState(id)} | {PfDesc(uc, withAstar: true)}";
        U16Write(snap);
        if (!_firstDispatch.ContainsKey(id))
        {
            _firstDispatch[id] = $"npc={id} type={type} 首次派发时 _consecutiveFails={PfFails(uc)}"
                                 + $" pfState={(uc != null ? PfState(uc) : "-")} 在册={InMap(id)} {Stamp()}";
        }
        if (type == "WaterHaul") Track(id);   // ⭐ 第二路取锚：抓到"派发帧内即被清"的 npc（①路抓不到）
    }

    private static void CaptureComplete(string line)
    {
        string type = Between(line, "[TaskScheduler] 完成 ", " 任务");
        int id = ParseNpcId(line);
        if (id == 0) return;
        int n; _completeN.TryGetValue(id, out n); _completeN[id] = n + 1;
        if (type == "WaterHaul") _completeWaterHaul++;
        U16Write($"{U16Tag}·complete] {Stamp()} type={type} npc={id} 第{_completeN[id]}次 在册(taskMap)={InMap(id)}");
    }

    /// <summary>P3：`PathFailedEvent` 只读计数（⚠️ 只写文件 · 不 `Debug.Log`）。</summary>
    private static void OnPathFailedEvt(PathFailedEvent evt)
    {
        if (_u16w == null) return;
        var uc = evt.Unit;
        int id = uc != null ? uc.npcId : 0;
        int n; _pathFailN.TryGetValue(id, out n); _pathFailN[id] = n + 1;
        U16Write($"{U16Tag}·pathfail] {Stamp()} npc={id} 第{_pathFailN[id]}次 dest=({evt.Destination.x:F2},{evt.Destination.y:F2})"
                 + $" 在册(taskMap)={InMap(id)} 在态(stateMap)={InState(id)} | {PfDesc(uc, withAstar: false)}");
    }

    /// <summary>P1：逐帧打点（一行一 npc）。</summary>
    private static void U16SeqLine(int id, float rt)
    {
        var uc = FindUnit(id);
        string st = TaskScheduler.HasInstance ? TaskScheduler.Instance.GetWorkerState(id).ToString() : "noSched";
        U16Write($"{U16Tag}·seq] {Stamp()} +{rt:F2}s npc={id} state={st} 在册={InMap(id)}"
                 + (uc == null ? " uc=null（已亡/被回收）" : $" | {PfDesc(uc, withAstar: false)}"));
    }

    /// <summary>⭐ 判据 2：`state` 跃迁瞬间落一条（grep 即得 [Assigned → ?] 轨迹）。</summary>
    private static void U16Transitions(float rt)
    {
        for (int i = 0; i < _tracked.Count; i++)
        {
            int id = _tracked[i];
            string now = (TaskScheduler.HasInstance ? TaskScheduler.Instance.GetWorkerState(id).ToString() : "?")
                         + "|在册=" + InMap(id);
            string prev;
            if (_lastState.TryGetValue(id, out prev) && prev == now) continue;
            _lastState[id] = now;
            U16Write($"{U16Tag}·trans] {Stamp()} +{rt:F2}s npc={id} {prev ?? "(首次)"} → {now}");
        }
    }

    /// <summary>P2：`PathFollower` 私有面（`_state` `:19` ／ `_consecutiveFails` `:23` ／ `_destination` `:17`）
    /// ＋ 站位微格可走性 ＋ `A*` 复演（与生产 `TaskScheduler.NavigateToSource` 同调用面 · 纯查询）。</summary>
    private static string PfDesc(UnitController uc, bool withAstar)
    {
        if (uc == null) return "uc=null";
        var pf = uc.GetComponent<PathFollower>();
        var sb = new StringBuilder();
        sb.Append("pfState=").Append(pf != null ? pf.State.ToString() : "无组件");
        sb.Append(" fails=").Append(PfFails(uc));
        Vector2 dest = pf != null ? PfDest(pf) : Vector2.zero;
        Vector2 pos = uc.transform.position;
        sb.Append(" 站位=(").Append(pos.x.ToString("F2")).Append(",").Append(pos.y.ToString("F2")).Append(")");
        sb.Append(" 站位格可走=").Append(Walkable(pos));
        if (dest != Vector2.zero)
        {
            sb.Append(" dest=(").Append(dest.x.ToString("F2")).Append(",").Append(dest.y.ToString("F2"))
              .Append(") dest格可走=").Append(Walkable(dest))
              .Append(" 距dest=").Append(Vector2.Distance(pos, dest).ToString("F2"));
        }
        if (withAstar)
        {
            var grid = GridSystem.Instance;
            if (grid != null && dest != Vector2.zero)
            {
                var subOpt = grid.WorldToSubCoord(dest);
                Vector2 toWorld = subOpt.HasValue ? grid.SubCoordToWorld(subOpt.Value) : dest;
                toWorld = SpawnPosSnapper.SnapWorld(toWorld, null, verbose: false);
                var res = PathfindingService.FindPathImmediate(pos, toWorld);
                sb.Append(" A*复演=").Append(res != null ? res.status.ToString() : "null")
                  .Append("（单位位 → snap(dest微格) · 与 NavigateToSource 同调用面）");
            }
        }
        return sb.ToString();
    }

    private static string PfState(UnitController uc)
    {
        var pf = uc != null ? uc.GetComponent<PathFollower>() : null;
        return pf != null ? pf.State.ToString() : "无组件";
    }

    private static int PfFails(UnitController uc)
    {
        var pf = uc != null ? uc.GetComponent<PathFollower>() : null;
        if (pf == null) return -1;
        var f = typeof(PathFollower).GetField("_consecutiveFails", BindingFlags.NonPublic | BindingFlags.Instance);
        return f != null ? (int)f.GetValue(pf) : -2;
    }

    private static Vector2 PfDest(PathFollower pf)
    {
        var f = typeof(PathFollower).GetField("_destination", BindingFlags.NonPublic | BindingFlags.Instance);
        return f != null ? (Vector2)f.GetValue(pf) : Vector2.zero;
    }

    /// <summary>站位/目标格可走性 ＋ **不可走的成因鉴别**（⭐ 补读 2026-09-21 二跑）：
    /// `IsSubWalkable == false` 的两支 ＝ ① 占格物阻断（`IsObstacleSub`，建筑等）② 地形自身不可走。
    /// ⛔ 两个判据均走 `GridSystem` 公开读口，零反射。</summary>
    private static string Walkable(Vector2 pos)
    {
        var grid = GridSystem.Instance;
        if (grid == null) return "grid=null";
        var sub = grid.WorldToSubCoord(pos);
        if (!sub.HasValue) return "越界";
        if (grid.IsSubWalkable(sub.Value)) return "True";
        if (!grid.IsObstacleSub(sub.Value)) return "False（地形不可走）";
        var o = grid.GetOccupantSub(sub.Value);
        var c = o as Component;
        return "False（占格物阻断：" + (c != null ? c.gameObject.name : o.GetType().Name) + "）";
    }

    private static void Track(int id)
    {
        if (id == 0 || _tracked.Contains(id) || _tracked.Count >= TrackCap) return;
        _tracked.Add(id);
        U16Write($"{U16Tag}·track] {Stamp()} npc={id} 入逐帧跟踪集（第 {_tracked.Count} 个）");
    }

    // ---- 反射只读小工具 ----
    private static Dictionary<int, KingdomTask> TaskMap()
    {
        var ts = TaskScheduler.Instance;
        if (ts == null) return null;
        var f = typeof(TaskScheduler).GetField("_npcTaskMap", BindingFlags.NonPublic | BindingFlags.Instance);
        return f != null ? f.GetValue(ts) as Dictionary<int, KingdomTask> : null;
    }

    private static bool InMap(int id)
    {
        var m = TaskMap();
        return m != null && m.ContainsKey(id);
    }

    private static bool InState(int id)
    {
        var ts = TaskScheduler.Instance;
        if (ts == null) return false;
        var f = typeof(TaskScheduler).GetField("_npcStateMap", BindingFlags.NonPublic | BindingFlags.Instance);
        var m = f != null ? f.GetValue(ts) as Dictionary<int, TaskState> : null;
        return m != null && m.ContainsKey(id);
    }

    private static int ParseNpcId(string line)
    {
        int at = line.IndexOf("npcId ");
        if (at < 0) return 0;
        at += 6;
        int end = at;
        while (end < line.Length && char.IsDigit(line[end])) end++;
        int id;
        return (end > at && int.TryParse(line.Substring(at, end - at), out id)) ? id : 0;
    }

    private static string Between(string s, string a, string b)
    {
        int i = s.IndexOf(a);
        if (i < 0) return "?";
        i += a.Length;
        int j = s.IndexOf(b, i);
        return j > i ? s.Substring(i, j - i) : s.Substring(i);
    }

    private static UnitController FindUnit(int npcId)
    {
        if (UnitRegistry.Instance == null) return null;
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
            if (u != null && u.npcId == npcId) return u;
        return null;
    }

    /// <summary>判据 1/3/5：汇总落盘（⛔ 全部进文件 · 不依赖 `read_console`）。</summary>
    private static void U16Summary(string tag)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{U16Tag}·summary] ===== U-16 汇总（tag={tag}）=====");
        sb.AppendLine($"{U16Tag}·summary] 采样帧数={_u16Frames} ｜ E2 命中帧={_e2HitFrames} ／ E2 未命中帧（有被派 npc 但无一在册）={_e2MissFrames}");
        sb.AppendLine($"{U16Tag}·summary] 跟踪集（被派 npc）=[{string.Join(",", _tracked.ConvertAll(x => x.ToString()).ToArray())}]（上限 {TrackCap}）");
        sb.AppendLine($"{U16Tag}·summary] 派发次数（按 npc）：{DictLine(_dispatchN)}　合计={SumOf(_dispatchN)}");
        sb.AppendLine($"{U16Tag}·summary] 完成次数（按 npc）：{DictLine(_completeN)}　合计={SumOf(_completeN)}");
        sb.AppendLine($"{U16Tag}·summary] ⭐ 单一判别式：`完成 WaterHaul` 条数 = {_completeWaterHaul}"
                      + $" ⇒ {(_completeWaterHaul == 0 ? "0 ⇒ 甲′ 坐实（工人从未走到井）" : ">0 ⇒ 归因改走乙/戊（工人确实到达过）")}");
        sb.AppendLine($"{U16Tag}·summary] PathFailedEvent 次数（按 npc）：{DictLine(_pathFailN)}　合计={SumOf(_pathFailN)}");
        sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 3 · 首次派发时 `_consecutiveFails`：");
        if (_firstDispatch.Count == 0) sb.AppendLine($"{U16Tag}·summary]   （无派发记录）");
        foreach (var kv in _firstDispatch) sb.AppendLine($"{U16Tag}·summary]   {kv.Value}");
        sb.AppendLine($"{U16Tag}·summary] 判据 5 · `_consecutiveFails` 末值：");
        for (int i = 0; i < _tracked.Count; i++)
        {
            var uc = FindUnit(_tracked[i]);
            sb.AppendLine($"{U16Tag}·summary]   npc={_tracked[i]} fails={PfFails(uc)} pfState={PfState(uc)}"
                          + (uc != null ? $" 站位格可走={Walkable(uc.transform.position)} | {PfDesc(uc, withAstar: true)}" : " uc=null"));
        }
        sb.AppendLine($"{U16Tag}·summary] 落盘={_u16Path}");
        if (_u16w != null) _u16w.Write(sb.ToString());
        Debug.Log($"[HH319长局·{tag}] ⭐ U-16 取证已落盘：{_u16Path}（完成 WaterHaul={_completeWaterHaul} · PathFailed={SumOf(_pathFailN)}）");
    }

    private static int SumOf(Dictionary<int, int> d)
    {
        int s = 0; foreach (var kv in d) s += kv.Value; return s;
    }

    private static string DictLine(Dictionary<int, int> d)
    {
        if (d.Count == 0) return "（无）";
        var sb = new StringBuilder();
        foreach (var kv in d) sb.Append($"npc{kv.Key}×{kv.Value} ");
        return sb.ToString().Trim();
    }

    private static Building Place(string resPath, Vector2 wantPos, int kingdomId)
    {
        var def = Resources.Load<BuildingDef>(resPath);
        if (def == null) return null;
        var fp = new Vector2Int(Mathf.Max(1, def.footprint.x), Mathf.Max(1, def.footprint.y));
        for (int ring = 0; ring <= 6; ring++)
        {
            for (int dx = -ring; dx <= ring; dx++)
            {
                for (int dy = -ring; dy <= ring; dy++)
                {
                    if (ring > 0 && Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring) continue;
                    var pos = wantPos + new Vector2(dx * 1.6f, dy * 1.6f);
                    var co = GridSystem.Instance.WorldToCoord(pos);
                    if (!co.HasValue) continue;
                    if (BuildingFactory.Instance.CreateBuildingInstance(def, def.sourceType, co.Value, fp, pos,
                            true, ResourceGrade.Normal, false, BuildingState.Active, kingdomId))
                    {
                        var b = GridSystem.Instance.GetOccupant(co.Value) as Building;
                        if (b != null) return b;
                    }
                }
            }
        }
        return null;
    }

    private static void SpawnWorker(Vector2 pos, int kingdomId)
    {
        var go = UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Worker, pos, kingdomId);
        if (go == null) return;
        if (go.GetComponent<WorkerInventory>() == null) go.AddComponent<WorkerInventory>();
        if (_w == null) _w = go.GetComponent<UnitController>();
    }

    /// <summary>遍历玩家国（k0）全部工人（v2：⛔ 不只看一个 —— v1 只采样一个 ⇒ 派工给了另一个 ⇒ 假 `state=None`）。</summary>
    private static IEnumerable<UnitController> PlayerWorkers()
    {
        foreach (var u in UnitRegistry.Instance.GetAllUnits())
        {
            if (u == null || !u.IsAlive || u.kingdomId != 0) continue;
            if (u.EffectiveOccupation == Occupation.Ruler) continue;
            yield return u;
        }
    }

    /// <summary>反射读 TaskScheduler._npcTaskMap ⇒ npcId → (type, source 名, source 国)。（只读）</summary>
    private static Dictionary<int, (KingdomTaskType, string, int)> ReadTasks()
    {
        var d = new Dictionary<int, (KingdomTaskType, string, int)>();
        var map = TaskMap();
        if (map == null) return d;
        foreach (var kv in map)
        {
            var t = kv.Value;
            if (t == null) continue;
            var srcB = t.source as Building;
            d[kv.Key] = (t.type, t.source is Component c && c != null ? c.gameObject.name : "?", srcB != null ? srcB.kingdomId : -1);
        }
        return d;
    }
}
