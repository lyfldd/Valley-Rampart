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
//     ⑤ 【⭐ `U-16` 案① 批 · `D815`】：
//        a) **件3 落位吸附**：`SpawnWorker` 首行加 `SpawnPosSnapper.SnapWorld(pos, "hh319_worker")`
//           （先例 `TestFixtureApi.cs:250`）⇒ 靶例不再出生即困死（`D813`：`A*复演` 恒 `Unreachable`）。
//        b) **只增列**：`dispatch`/`complete`/`seq`/`abandon` 行补 `Istask`（`IsKingdomTaskWorker`）、
//           `dispatch` 补 `目标位建筑`（判据 1 的 `task.source` 身份）、`seq` 补 `任务/源/dest`。
//        c) **新增行**：`[HH319U16·abandon/flag/inv/threat-inject/yield]`
//           （`abandon`＝判据 7 · `flag`＝判据 3 逐次跃迁 · `inv`＝判据 3 不变量 · `yield`＝判据 4 豁免列）。
//        d) **新增第 3 菜单**「…_豁免列(威胁注入)」⇒ ⛔ 独立档，不污染判据 1/2 的靶例跑。
//        ⚠️ 判据 1~6 原有语义**一字未改**；本条属**只增不改**。
//     ⑥ 【⭐ `U-16b` 件B／件C · `D816` §八】：
//        a) **判据 2 口径修正**：`完成 WaterHaul` 须与 `·trans]` 跃迁**配对** —— `[HH319U16·complete]` 新增
//           `⭐来源分支=`（由 `_lastKnownState` 回看**最近非 None** 的 state）＋ `计入真完成=`；
//           ⭐ **只有来源 ＝ `MovingToDest` 才计入**（`Working` ⇒ 装载失败分支 ⇒ ⛔ 不算达成）。
//        b) **判据 5 口径重写**：只判「来源**非** `MovingToDest` 的异常完成」（计数 `_completeAnomalyN` ⇒ 应为 0），
//           ⛔ 不再与"装载失败 ⇒ 直接完成"混判。
//        c) 新增 `[HH319U16·farm]`（**卸货到账**水位式逐秒：农场仓 `Water` ＋ 峰值 ＋ 首次见水时刻）。
//        d) 新增第 4 菜单「U16b reason覆盖列(构造法)」：定向触发 `External`／`SourceInvalid`／`BrainLost`
//           （⚠️ **构造法** ＋ 单列「生产路径可达性」，`L-51`）；⛔ 独立档。
//        ⭐ `·trans]` **保留**（本轮判据救星）。
//     ⑦ 【⭐ `U-20` 批 · `D817` §六】：
//        a) **件2 补强**：`[HH319U16·complete]` 增 `⭐Working计时已到=`（＝`Time.time - _workStartTime[id]
//           >= GetTaskDuration(task)`，逐帧由 `·seq]` 缓存 ⇒ **仅反射只读**）—— ⛔ **不依赖"回看最近非 None"**；
//           ⛔ **回看列保留**（`⭐来源分支=`）以做**交叉验证**（两列不一致即报）。
//        b) **件2 盲区率**：summary 显式输出「未知 <N> ／ 总 <M> ＝ x%」（上批实测 131/158 ＝ 83%）。
//        c) **件3 分桶**：summary 新增「Abandon 按 `type/reason` 分桶」—— 用于区分「两段位移任务
//           （应受 (f) 影响）」vs「路程短但另有卡死源」。
//        ⛔ 判据 1/2/5/6 既有语义**一字未改**（本项属**只增不改**）。
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

    /// <summary>⭐ `U-16` 案① 批件4（`D815` 判据 4）：**豁免列**档 —— 在册任务期注入威胁，
    /// 验"工人能逃／挂起"（4a）与"`Cautious` 未越阈时继续干活"（4b）。
    /// ⛔ 独立档 ⇒ ⛔ 不污染判据 1/2 的靶例跑（`15x`／`1x`）。</summary>
    [MenuItem("Valley/验证/HH319 F1长局观察_豁免列(威胁注入)")]
    public static void RunYieldColumn()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH319长局] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH319_LongRunHost").AddComponent<RunHost>().Host(RunCoroutine(15f, "yield", threatStage: true));
    }

    /// <summary>⭐ `U-16b` 件C（`D816` §八-件C · 判据 5）：**reason 覆盖列** —— 定向触发 `External`／`SourceInvalid`／`BrainLost`
    /// 三条**未被自然走到**的 `Abandon` 路径。
    /// ⚠️⚠️ **构造法声明（`L-51`）**：本档**直呼 `TaskScheduler` 的 public API**
    ///   （`AbandonTask` `:180` ／ `OnBuildingDied` `:207`）＋ `BrainLost` 走**反射删私有表** ⇒
    ///   ⛔ **均非生产路径触发** ⇒ 每行**同时给出「生产路径可达性」**（见各 `·reason]` 行）。
    /// ⛔ 独立档 ⇒ 不污染判据 1/2 的靶例跑。</summary>
    [MenuItem("Valley/验证/HH319 U16b reason覆盖列(构造法)")]
    public static void RunReasonColumn()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH319长局] 须先进入 Play 后调用本菜单。"); return; }
        new GameObject("HH319_LongRunHost").AddComponent<RunHost>().Host(RunReasonCoroutine());
    }

    private static IEnumerator RunReasonCoroutine()
    {
        const string tag = "reason";
        U16Begin(tag);
        Debug.Log($"[HH319长局·{tag}] ── 起（reason 覆盖列 · ⚠️ 构造法 · L-51 声明见落盘）");
        var cfg = new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "smoke_w319L" + tag, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 15f);
        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 2)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f)
            { Debug.LogError($"[HH319长局·{tag}] 等世界就绪超时。"); yield return Finish(tag, null); yield break; }
        }
        yield return new WaitForSeconds(0.5f);

        Vector2 anchor = WorldManager.Instance.GetKingdomAnchorWorld();
        var well = Place("Buildings/Well", anchor + new Vector2(-6f, 0f), 0);
        var farm = Place("Buildings/farm", anchor + new Vector2(6f, 0f), 0);
        SpawnWorker(anchor + new Vector2(-1f, 1.6f), 0);
        SpawnWorker(anchor + new Vector2(1f, -1.6f), 0);
        TestFixtureApi.AddWaterToKingdomWells(0, 50);
        if (farm != null)
        {
            var fs = farm.GetComponent<StorageComponent>();
            if (fs != null) fs.TakeOut(ResourceType.Water, fs.GetAmount(ResourceType.Water));
        }
        U16Write($"{U16Tag}·reason] {Stamp()} 布置 well={well != null} farm={farm != null}（等世界自行派工 ⇒ 取在册者做靶）");

        float start = Time.realtimeSinceStartup;
        bool d1 = false, d2 = false, d3 = false;
        while (Time.realtimeSinceStartup - start < 50f)
        {
            yield return null;
            float rt = Time.realtimeSinceStartup - start;
            if (!d1)
            {
                int id = FirstAssignedPlayerNpc();
                if (id != 0)
                {
                    d1 = true;
                    U16Write($"{U16Tag}·reason] 构造① **External**：直呼 public `TaskScheduler.AbandonTask({id})`（`TaskScheduler.cs:180`）"
                             + $" ｜生产路径可达性：`VagrantCampSystem` 招募 ／ `Building.RemoveWorkers` 建筑驱离（⛔ 本档走构造法）");
                    TaskScheduler.Instance.AbandonTask(id);
                }
            }
            else if (!d2)
            {
                int id = FirstAssignedPlayerNpc();
                var t = id != 0 ? TaskOf(id) : null;
                if (t != null && t.source != null)
                {
                    d2 = true;
                    var c = t.source as Component;
                    U16Write($"{U16Tag}·reason] 构造② **SourceInvalid**：直呼 public `TaskScheduler.OnBuildingDied(source)`（`TaskScheduler.cs:207`）"
                             + $" 靶 npc={id} 源={(c != null ? c.gameObject.name : "?")}"
                             + $" ｜生产路径可达性：`TaskScheduler.Unregister(source)`（建筑死亡/废弃）调用（⛔ 本档走构造法）");
                    TaskScheduler.Instance.OnBuildingDied(t.source);
                }
            }
            else if (!d3)
            {
                int id = FirstAssignedPlayerNpc();
                if (id != 0)
                {
                    d3 = true;
                    U16Write($"{U16Tag}·reason] 构造③ **BrainLost**：**反射删私有 `_npcBrainMap[{id}]`**（⛔ 非 public API ⇒ 只能构造）"
                             + $" ｜生产路径可达性：`UpdateAssignedTasks` 的 `!TryGetValue || brain == null` 支 —— 需「brain 已 Destroy 而字典未清」，"
                             + $"正常帧序下 `OnNpcDied` 会先清 ⇒ ⚠️ **难以稳定构造** ⇒ 本档以构造法取证并如实标注");
                    RemoveBrainRef(id);
                }
            }
            if (d1 && d2 && d3 && rt > 10f) break;
        }
        U16Write($"{U16Tag}·reason] 结果 d1(External)={d1} d2(SourceInvalid)={d2} d3(BrainLost)={d3}");
        yield return Finish(tag, null);
    }

    /// <summary>取一个「玩家国 ∧ 在册」的 npcId（0＝无）。</summary>
    private static int FirstAssignedPlayerNpc()
    {
        var m = TaskMap();
        if (m == null) return 0;
        foreach (var kv in m)
        {
            var uc = FindUnit(kv.Key);
            if (uc == null || !uc.IsAlive || uc.kingdomId != 0) continue;
            if (uc.EffectiveOccupation == Occupation.Ruler) continue;
            return kv.Key;
        }
        return 0;
    }

    private static KingdomTask TaskOf(int id)
    {
        var m = TaskMap();
        KingdomTask t;
        return (m != null && m.TryGetValue(id, out t)) ? t : null;
    }

    /// <summary>⭐ `U-20` 件2：反射只读 `TaskScheduler._workStartTime[id]`（0 ＝ 无记录）。</summary>
    private static float WorkStartOf(int id)
    {
        var ts = TaskScheduler.Instance;
        if (ts == null) return 0f;
        var f = typeof(TaskScheduler).GetField("_workStartTime", BindingFlags.NonPublic | BindingFlags.Instance);
        var m = f != null ? f.GetValue(ts) as Dictionary<int, float> : null;
        float v;
        return (m != null && m.TryGetValue(id, out v)) ? v : 0f;
    }

    /// <summary>⭐ `U-20` 件2：反射只读 `TaskScheduler.GetTaskDuration(KingdomTask)`（`Working` 段的独立预算）。</summary>
    private static float DurationOf(KingdomTask task)
    {
        if (task == null) return 0f;
        var mi = typeof(TaskScheduler).GetMethod("GetTaskDuration", BindingFlags.NonPublic | BindingFlags.Instance);
        if (mi == null) return 0f;
        try { return (float)mi.Invoke(TaskScheduler.Instance, new object[] { task }); }
        catch { return 0f; }
    }

    /// <summary>⭐ 构造法（`L-51`）：反射删 `_npcBrainMap[id]` ⇒ 触发 `BrainLost` 支（⛔ 非 public API）。</summary>
    private static void RemoveBrainRef(int id)
    {
        var ts = TaskScheduler.Instance;
        if (ts == null) return;
        var f = typeof(TaskScheduler).GetField("_npcBrainMap", BindingFlags.NonPublic | BindingFlags.Instance);
        var m = f != null ? f.GetValue(ts) as Dictionary<int, NPCBrain> : null;
        if (m != null) m.Remove(id);
    }

    private class RunHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    private static UnitController _w;

    private static IEnumerator RunCoroutine(float speed, string tag, bool threatStage = false)
    {
        // ⚠️ `U-16` 案① 批：豁免列档延长观测窗（威胁注入在 +15s ⇒ 需更长的注入后窗口）
        float duration = speed <= 1f ? 180f : (threatStage ? 70f : 45f);    // 1× ⇒ ≥180 真实秒（任务书 §件1）；15× 对照 ⇒ 45s（足够多轮）
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
        float nextInv = 0f;                     // ⭐ 判据 3：每秒一条"在册数 vs 置位数"不变量
        float nextThreatLog = 0f;               // ⭐ 判据 4：威胁注入后逐秒读数
        float nextFarm = 0f;                    // ⭐ `U-16b` 判据 1：农场仓水位逐秒读数（卸货到账）
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

            // ⭐ 判据 3 不变量（每秒一条）：**未在册者恒 false** ⇒ 玩家国「在册任务数」应＝「置位工人数」。
            if (rt >= nextInv) { nextInv = rt + 1f; U16InvariantLine(rt); }
            // ⭐ `U-16b` 判据 1（`D816` §八）：**卸货到账**读数 —— 农场仓水位逐秒（增量 ＝ 真卸货；E6 为事件式补充）。
            if (rt >= nextFarm) { nextFarm = rt + 1f; U16FarmLine(rt, farmStore, farm); }

            // ⭐ 判据 4（豁免列档专有）：在册任务期**注入威胁** ⇒ 工人应"能逃/挂起"，⛔ 不被任务锁死。
            //   ⚠️ 注入口勘正：`AIDebugSpawnController` 的 `Enemy*` 条目走 `Faction.Monster` +
            //   `UnitDataManager.GetData` ⇒ **本局无该条目**（实测 `ok=False 生成失败`）；且
            //   `MonsterSpawner` 有**考跑守卫**（`TimeManager.TestHarnessMode ⇒ 静默 return null`）
            //   ⇒ ⛔ 正门跑局内**无法生成任何怪物**。
            //   ⇒ 改用 ⭐ **AI 国战士**：`UnitFactory.SpawnUnit(PlayerCamp, Warrior, pos, kingdomId:1)`
            //   （`UnitFactory:139-142` 会 `SetFaction(AiKingdom)`）⇒ 对玩家工人**即敌人**
            //   ⇒ `NPCBrain.UpdatePerception:543-571` 注入 `ThreatStimulus` ✔（⛔ 不改 `MonsterAI` 一行）。
            //   两段注入：① 远（~6.5 格 ⇒ 低强度 ⇒ 期望 `Cautious` ⇒ 判据 4b：**继续干活**）
            //             ② 近（~2.0 格 ⇒ 高强度 ⇒ 期望威胁焦点/`FullRetreat` ⇒ 判据 4a：**能逃**）。
            if (threatStage && _tracked.Count > 0)
            {
                int tid = _tracked[0];
                var tuc = FindUnit(tid);
                if (tuc != null && InMap(tid))
                {
                    Vector2 wpos = tuc.transform.position;   // ⚠️ `transform.position` 是 `Vector3` ⇒ 先显式转 `Vector2`
                    if (!_threatInjected && rt >= 15f)
                    {
                        _threatInjected = true; _threatNpcId = tid;
                        InjectAiWarrior(wpos + new Vector2(6.5f, 0f), "4b-远", tid, rt);
                    }
                    // ⚠️ 二跑教训：固定 `rt >= 40f` 会让近敌落在**任务已超时**之后（r2 实测：注入帧 f1800
                    //   ⇒ 下一帧 f1801 即 `Abandon Transport reason=Timeout`）⇒ 4a 窗口错过。
                    //   ⇒ 改为 ⭐ **"等该 npc 的一次新派发"**：派发帧起 30 游戏秒内必在册 ⇒ 4a 窗口充足。
                    else if (_threatInjected && !_threatInjected2)
                    {
                        int dn; _dispatchN.TryGetValue(tid, out dn);
                        if (_stage1DispatchN < 0) _stage1DispatchN = dn;          // 第①段后记基线
                        else if (dn > _stage1DispatchN)                            // ⭐ 新派发 ⇒ 立刻注入近敌
                        {
                            _threatInjected2 = true;
                            InjectAiWarrior(wpos + new Vector2(2.0f, 0f), "4a-近", tid, rt);
                        }
                    }
                }
            }
            if (_threatInjected && rt >= nextThreatLog)
            {
                nextThreatLog = rt + 1f;
                U16ThreatLine(rt);
            }

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
    // ---- `U-16` 案① 批新增（判据 3／6／7／4 读数面）----
    private static readonly Dictionary<int, int> _abandonN = new Dictionary<int, int>();            // Abandon 次数（按 npc）
    private static readonly Dictionary<string, int> _abandonReasonN = new Dictionary<string, int>();// Abandon 次数（按 reason）
    private static int _dispatchFlagTrue, _dispatchFlagFalse;   // 判据 3：派发时刻 IsKingdomTaskWorker 读数
    private static int _completeFlagTrue, _completeFlagFalse;   // 判据 3：完成时刻读数
    private static readonly Dictionary<int, string> _flagLast = new Dictionary<int, string>();     // 判据 3：逐次跃迁
    // ---- `U-16b` 件B（`D816` §八）：判据 2／5 **口径修正**面 ----
    private static readonly Dictionary<int, string> _lastKnownState = new Dictionary<int, string>();  // 每 npc 最近一次**非 None** 的 state
    private static int _completeWaterHaulFromDest;   // ⭐ 判据 2：来源分支 ＝ `MovingToDest` 到达（**唯一计入**）
    private static readonly Dictionary<string, int> _completeSourceN = new Dictionary<string, int>(); // 来源分支分布
    private static int _completeAnomalyN;            // ⭐ 判据 5：来源**非** `MovingToDest` 的 WaterHaul 完成（＝异常分支）
    // ---- `U-20` 件2／件3（`D817` §六）：Working 计时布尔 ＋ 盲区率 ＋ Timeout 按类型分桶 ----
    private static readonly Dictionary<int, float> _workStartLast = new Dictionary<int, float>();   // 每 npc 最近一次 `_workStartTime`
    private static readonly Dictionary<int, float> _workDurLast = new Dictionary<int, float>();     // 每 npc 最近一次 `GetTaskDuration(task)`
    private static readonly Dictionary<string, int> _abandonTypeReason = new Dictionary<string, int>();  // `type/reason` 分桶
    private static readonly Dictionary<int, int> _unreachByNpc = new Dictionary<int, int>();     // ⭐ `U-20` 收口件2：`Unreachable` 按 npc 分桶
    private static int _unknownSrcN, _srcTotalN;     // ⭐ 盲区率：来源＝未知 ／ 完成总数
    private static int _farmWaterPeak2;              // ⭐ 判据 1：农场仓水量增量（卸货到账）
    private static float _farmFirstGainRt = -1f;     // 首次增量时刻
    private static bool _farmGained;
    // ---- ⭐ `F-1` 收尾批（`D820` §三）：判据 1 两新列 ＋ 判据 2 同农场并发数 ＋ 判据 4 两侧派发数 ----
    private static readonly Dictionary<string, int> _dispatchTypeN = new Dictionary<string, int>();  // 判据 4：派发按类型计数
    private static readonly Dictionary<int, int> _farmConcN = new Dictionary<int, int>();            // 判据 2：同农场在途 WaterHaul 并发数分布
    private static int _farmWorkFrames, _farmIdleFrames;     // 判据 1 新列：本秒是否有农场工人 Working
    private static int _farmThirstFrames, _farmOkFrames;     // 判据 1 新列：本秒 TryConsumeFarmWater 是否失败（缺水停产）
    private static int _farmWaterPeakInThirst;               // 缺水期水位峰值（对照）
    private static bool _threatInjected;                        // 判据 4：威胁已注入（第①段 · 远）
    private static bool _threatInjected2;                       // 判据 4：威胁已注入（第②段 · 近）
    private static int _stage1DispatchN = -1;                   // 判据 4a：第①段时的派发计数基线（等新派发窗口）
    private static int _threatNpcId;
    private static readonly List<string> _threatSeq = new List<string>();   // 判据 4：注入后逐秒读数
    private static readonly List<UnitController> _threatEnemies = new List<UnitController>();  // 判据 4：注入的威胁源

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
        // `U-16` 案① 批新增面
        _abandonN.Clear(); _abandonReasonN.Clear(); _flagLast.Clear();
        _dispatchFlagTrue = _dispatchFlagFalse = _completeFlagTrue = _completeFlagFalse = 0;
        _threatInjected = false; _threatInjected2 = false; _threatNpcId = 0; _stage1DispatchN = -1;
        _threatSeq.Clear(); _threatEnemies.Clear();
        // `U-16b` 件B 新增面
        _lastKnownState.Clear(); _completeSourceN.Clear();
        _completeWaterHaulFromDest = 0; _completeAnomalyN = 0;
        // `U-20` 件2／件3 新增面
        _workStartLast.Clear(); _workDurLast.Clear(); _abandonTypeReason.Clear();
        _unreachByNpc.Clear();
        _unknownSrcN = 0; _srcTotalN = 0;
        // ⭐ `F-1` 收尾批新增面
        _dispatchTypeN.Clear(); _farmConcN.Clear();
        _farmWorkFrames = _farmIdleFrames = _farmThirstFrames = _farmOkFrames = 0;
        _farmWaterPeakInThirst = 0;
        _farmWaterPeak2 = 0; _farmGained = false; _farmFirstGainRt = -1f;
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
        else if (condition.StartsWith("[TaskScheduler] Abandon")) CaptureAbandon(condition);
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
        // ⭐ `F-1` 收尾批 判据 4：**派发按类型计数**（缺水期 `WaterHaul` 应 ↑、`Production` 应 ↓ —— 两侧都给读数）。
        int tn; _dispatchTypeN.TryGetValue(type, out tn); _dispatchTypeN[type] = tn + 1;
        var uc = FindUnit(id);
        // ⭐ 判据 3：**派发时刻**的 `IsKingdomTaskWorker` 读数（`D815` §件1 置位后应为 True；
        //   若同帧被 OnPathFailed 清 ⇒ 读 False ⇒ 与 `在册(False)` 同时出现＝甲′-a 铁证）。
        bool flag = FlagOf(id);
        if (flag) _dispatchFlagTrue++; else _dispatchFlagFalse++;
        FlagTrace(id);
        // ⭐ 判据 1：派发目标位（`task.SourcePos`）的**身份**（由 `@ (x,y)` 反查该位建筑名 ⇒ 井/农场可辨）。
        Vector2 dpos = ParsePosAfterAt(line);
        string snap = $"{U16Tag}·dispatch] {Stamp()} type={type} npc={id} 第{_dispatchN[id]}次"
                      + $" 在册(taskMap)={InMap(id)} 在态(stateMap)={InState(id)} Istask={flag}"
                      + $" 目标位=({dpos.x:F2},{dpos.y:F2}) 目标位建筑={BuildingAtName(dpos)}"
                      + $" | {PfDesc(uc, withAstar: true)}";
        U16Write(snap);
        if (!_firstDispatch.ContainsKey(id))
        {
            _firstDispatch[id] = $"npc={id} type={type} 首次派发时 _consecutiveFails={PfFails(uc)}"
                                 + $" pfState={(uc != null ? PfState(uc) : "-")} 在册={InMap(id)} Istask={flag} {Stamp()}";
        }
        if (type == "WaterHaul") Track(id);   // ⭐ 第二路取锚：抓到"派发帧内即被清"的 npc（①路抓不到）
    }

    /// <summary>⭐ 判据 7：`Abandon` 日志捕获（`D815` §四 契约：`[TaskScheduler] Abandon {type} → npcId {id} reason={R}`）。
    /// ⚠️ 本批为**新增日志**：改前 `Abandon` 全程无日志 ⇒ 与之配套的读数须能容"0 条"（＝本支未走）。</summary>
    private static void CaptureAbandon(string line)
    {
        string type = Between(line, "[TaskScheduler] Abandon ", " →");
        int id = ParseNpcId(line);
        string reason = Between(line, "reason=", "\u0000");
        if (reason == "\u0000" || reason.Length == 0) reason = "?";
        int n; _abandonN.TryGetValue(id, out n); _abandonN[id] = n + 1;
        int r; _abandonReasonN.TryGetValue(reason, out r); _abandonReasonN[reason] = r + 1;
        // ⭐ `U-20` 件3：**按 `type/reason` 分桶** —— 用于区分「两段位移任务（应受 (f) 影响）」vs
        //   「路程短但另有卡死源」（如 `Production`）⇒ 单有总数无法定性。
        string tr = type + "/" + reason;
        int trn; _abandonTypeReason.TryGetValue(tr, out trn); _abandonTypeReason[tr] = trn + 1;
        // ⭐ `U-20` 收口件2：`Unreachable` 按 npc 分桶（⇒ 前二名占比 ≥99% 才能自证「`甲′-a` 独占」）。
        if (reason == "Unreachable")
        {
            int un; _unreachByNpc.TryGetValue(id, out un); _unreachByNpc[id] = un + 1;
        }
        bool flag = FlagOf(id);
        FlagTrace(id);
        U16Write($"{U16Tag}·abandon] {Stamp()} type={type} npc={id} reason={reason} 第{_abandonN[id]}次"
                 + $" 在册(taskMap)={InMap(id)} Istask={flag} | {PfDesc(FindUnit(id), withAstar: false)}");
    }

    /// <summary>`NPCBrain.IsKingdomTaskWorker` 读数（**public 字段** ⇒ 零反射）。</summary>
    private static bool FlagOf(int npcId)
    {
        var uc = FindUnit(npcId);
        var b = uc != null ? uc.GetComponent<NPCBrain>() : null;
        return b != null && b.IsKingdomTaskWorker;
    }

    /// <summary>⭐ 判据 3：**逐次跃迁**落一条（⛔ 不得只给末值）—— 派发 True / 完成·放弃 False 的配对轨迹。</summary>
    private static void FlagTrace(int npcId)
    {
        string now = FlagOf(npcId) ? "Istask=True" : "Istask=False";
        string prev;
        if (_flagLast.TryGetValue(npcId, out prev) && prev == now) return;
        _flagLast[npcId] = now;
        U16Write($"{U16Tag}·flag] {Stamp()} npc={npcId} {prev ?? "(首次)"} → {now}");
    }

    /// <summary>解析 `… @ (x.xx, y.yy)…` 的目标位（派发日志的 `task.SourcePos`）。</summary>
    private static Vector2 ParsePosAfterAt(string line)
    {
        int i = line.IndexOf("@ (");
        if (i < 0) return Vector2.zero;
        i += 3;
        int j = line.IndexOf(')', i);
        if (j <= i) return Vector2.zero;
        var parts = line.Substring(i, j - i).Split(',');
        if (parts.Length != 2) return Vector2.zero;
        float x, y;
        if (!float.TryParse(parts[0].Trim(), out x)) return Vector2.zero;
        if (!float.TryParse(parts[1].Trim(), out y)) return Vector2.zero;
        return new Vector2(x, y);
    }

    /// <summary>⭐ 判据 1：某世界坐标处的**建筑身份**（`task.source` 是井还是农场 ⇒ 可辨）。</summary>
    private static string BuildingAtName(Vector2 pos)
    {
        var grid = GridSystem.Instance;
        if (grid == null) return "?";
        var co = grid.WorldToCoord(pos);
        if (!co.HasValue) return "越界";
        var b = grid.GetOccupant(co.Value) as Building;
        if (b == null) return "无";
        return $"{b.def?.id ?? b.name}@({b.transform.position.x:F2},{b.transform.position.y:F2})/k{b.kingdomId}";
    }

    private static void CaptureComplete(string line)
    {
        string type = Between(line, "[TaskScheduler] 完成 ", " 任务");
        int id = ParseNpcId(line);
        if (id == 0) return;
        int n; _completeN.TryGetValue(id, out n); _completeN[id] = n + 1;
        if (type == "WaterHaul") _completeWaterHaul++;
        // ⭐⭐ `U-16b` 件B（`D816` §八 判据 2）：**来源分支配对** —— `Complete` 日志在 `ClearNpc` **之后** 发出
        //   ⇒ `GetWorkerState` 恒 `None` ⇒ 必须回看**最近一次非 None 的 state**（由 `·seq]`／`·trans]` 维护）。
        //   ⭐ **只有来源 ＝ `MovingToDest` 才计入"真完成"**；`Working` ⇒ 装载失败分支（`HH319` 首跑 2 条即此）。
        string src;
        if (!_lastKnownState.TryGetValue(id, out src) || src == null) src = "未知";
        int sn; _completeSourceN.TryGetValue(type + "/" + src, out sn); _completeSourceN[type + "/" + src] = sn + 1;
        _srcTotalN++;
        if (src == "未知") _unknownSrcN++;
        // ⭐ `U-20` 件2：**`Working` 计时是否已到**（`Time.time - _workStartTime[id] >= GetTaskDuration(task)`）
        //   ⇒ 可直接区分 `Working` **正常完工**（已到）vs **装载失败分支**（未到）—— ⛔ 不依赖回看列；
        //   两列**不一致即报**（交叉验证）。
        float ws, dur;
        bool workDone = false, hasWork = false;
        if (_workStartLast.TryGetValue(id, out ws) && _workDurLast.TryGetValue(id, out dur) && dur > 0f)
        { hasWork = true; workDone = (Time.time - ws) >= dur; }
        if (type == "WaterHaul")
        {
            if (src == TaskState.MovingToDest.ToString()) _completeWaterHaulFromDest++;
            else _completeAnomalyN++;
        }
        // ⭐ 判据 3：完成时刻应为**已复位**（`Complete:602` 写 false）⇒ `Istask=False` 即配对成立。
        bool flag = FlagOf(id);   // ⚠️ `Complete` 的日志在 `ClearNpc` 之后 ⇒ 此处读到的就是复位后值
        if (flag) _completeFlagTrue++; else _completeFlagFalse++;
        FlagTrace(id);
        U16Write($"{U16Tag}·complete] {Stamp()} type={type} npc={id} 第{_completeN[id]}次"
                 + $" 在册(taskMap)={InMap(id)} Istask={flag} ⭐来源分支={src}"
                 + $" ⭐Working计时已到={workDone}（有工时戳={hasWork}）"
                 + $" 计入真完成={(type == "WaterHaul" && src == TaskState.MovingToDest.ToString())}");
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
        if (st != TaskState.None.ToString()) _lastKnownState[id] = st;   // ⭐ 判据 2 配对用（只记非 None）
        // ⭐ `U-20` 件2：逐帧缓存 `_workStartTime` ＋ `GetTaskDuration(task)`（**在册时**）——
        //   供 `·complete]` 判「`Working` 计时是否已到」（⛔ 不依赖"回看最近非 None"，两列交叉验证）。
        var tk2 = TaskOf(id);
        if (tk2 != null)
        {
            float ws2 = WorkStartOf(id);
            if (ws2 > 0f) { _workStartLast[id] = ws2; _workDurLast[id] = DurationOf(tk2); }
        }
        U16Write($"{U16Tag}·seq] {Stamp()} +{rt:F2}s npc={id} state={st} 在册={InMap(id)} Istask={FlagOf(id)}"
                 + $" {TaskSrcDesc(id)}"
                 + (uc == null ? " uc=null（已亡/被回收）" : $" | {PfDesc(uc, withAstar: false)}"));
    }

    /// <summary>⭐ 判据 1：在册任务的**源身份**（`task.source` ＝ 水井 还是 农场 —— 直接打印，⛔ 不靠坐标猜）。</summary>
    private static string TaskSrcDesc(int id)
    {
        var m = TaskMap();
        KingdomTask t;
        if (m == null || !m.TryGetValue(id, out t) || t == null) return "在册=-";
        var c = t.source as Component;
        return $"任务={t.type} 源={(c != null ? c.gameObject.name : "?")}"
             + $"@({t.SourcePos.x:F2},{t.SourcePos.y:F2}) dest=({t.destPos.x:F2},{t.destPos.y:F2})";
    }

    /// <summary>⭐ 判据 2：`state` 跃迁瞬间落一条（grep 即得 [Assigned → ?] 轨迹）。</summary>
    private static void U16Transitions(float rt)
    {
        for (int i = 0; i < _tracked.Count; i++)
        {
            int id = _tracked[i];
            string stNow = TaskScheduler.HasInstance ? TaskScheduler.Instance.GetWorkerState(id).ToString() : "?";
            if (TaskScheduler.HasInstance && stNow != TaskState.None.ToString()) _lastKnownState[id] = stNow;   // ⭐ 判据 2 配对用
            string now = stNow + "|在册=" + InMap(id);
            string prev;
            if (_lastState.TryGetValue(id, out prev) && prev == now) continue;
            _lastState[id] = now;
            U16Write($"{U16Tag}·trans] {Stamp()} +{rt:F2}s npc={id} {prev ?? "(首次)"} → {now}");
        }
    }

    /// <summary>⭐ `U-16b` 判据 1（`D816` §八）：**卸货到账**逐秒读数（农场仓 `Water` 水位 ＋ 峰值 ＋ 首次增量时刻）。
    /// 与 `E6` 互补：`E6` 是"背包转空 ∧ 农场水>0"的**事件式**判定；本行是**水位式** ⇒ "增量 > 0"即到账硬证。</summary>
    private static void U16FarmLine(float rt, StorageComponent farmStore, Building farm)
    {
        if (farmStore == null) return;
        int now = farmStore.GetAmount(ResourceType.Water);
        if (now > _farmWaterPeak2) _farmWaterPeak2 = now;
        if (now > 0 && !_farmGained) { _farmGained = true; _farmFirstGainRt = rt; }
        // ⭐ 判据 1 新列①：**本秒是否有农场工人处于 `Working`**（＝ `HasWorkerAssigned(农场)` · **公开口零反射**）。
        //   ⚠️ 这是 `D-2` 修前 ④ `WaterHaul` 可达窗口的**必要条件之一**（另一＝`Food < 0.8×cap`）。
        bool farmWorking = farm != null && TaskScheduler.HasInstance && TaskScheduler.Instance.HasWorkerAssigned(farm);
        // ⭐ 判据 1 新列②：**本秒 `TryConsumeFarmWater` 是否失败** —— 判据式与 `ProducerComponent.cs:124`
        //   **同源**（`!storage.CanTake(Water, 2)`），⛔ **不依赖日志关键词**、⛔ **无副作用**（不真扣水）。
        bool thirst = !farmStore.CanTake(ResourceType.Water, 2);
        if (farmWorking) _farmWorkFrames++; else _farmIdleFrames++;
        if (thirst) { _farmThirstFrames++; if (now > _farmWaterPeakInThirst) _farmWaterPeakInThirst = now; }
        else _farmOkFrames++;
        // ⭐ 判据 2：**同农场在途 `WaterHaul` 并发数**（按 `HaulWaterArgs.target == 本农场仓` 计 —— `D-1` 修后应恒 ≤1）。
        int conc = 0;
        var tm = TaskMap();
        if (tm != null)
            foreach (var kv in tm)
            {
                var t = kv.Value;
                if (t == null || t.type != KingdomTaskType.WaterHaul) continue;
                if (t.args is HaulWaterArgs hw && ReferenceEquals(hw.target, farmStore)) conc++;
            }
        int cn; _farmConcN.TryGetValue(conc, out cn); _farmConcN[conc] = cn + 1;
        U16Write($"{U16Tag}·farm] {Stamp()} +{rt:F2}s 农场仓水={now} 峰值={_farmWaterPeak2}"
                 + $" 首次见水时刻={(_farmGained ? _farmFirstGainRt.ToString("F2") + "s" : "未见")}"
                 + $" ⭐农场工人Working={farmWorking} ⭐缺水产={thirst} ⭐同农场在途WaterHaul={conc}");
    }

    /// <summary>⭐ 判据 3 不变量（每秒一条）：**未在册者恒 false**。
    /// 口径：玩家国工人中「在 `_npcTaskMap` 者数」应 ＝ 「`IsKingdomTaskWorker==true` 者数」
    /// （差 ≠ 0 ⇒ 置位/复位不配对 ⇒ 泄漏）。</summary>
    private static void U16InvariantLine(float rt)
    {
        int inMap = 0, flagged = 0, both = 0;
        foreach (var uc in PlayerWorkers())
        {
            bool m = InMap(uc.npcId);
            bool f = FlagOf(uc.npcId);
            if (m) inMap++;
            if (f) flagged++;
            if (m && f) both++;
        }
        U16Write($"{U16Tag}·inv] {Stamp()} +{rt:F2}s 玩家国：在册={inMap} 置位={flagged} 既在册又置位={both}"
                 + $" 差={inMap - flagged}（应 0）");
    }

    /// <summary>⭐ 判据 4：注入威胁（**AI 国战士** —— ⛔ 不用怪物：`MonsterSpawner` 有考跑守卫）。
    /// `kingdomId:1` ⇒ `UnitFactory:139-142 SetFaction(AiKingdom)` ⇒ 对玩家工人即敌人 ⇒ 感知注入 `ThreatStimulus`。</summary>
    private static void InjectAiWarrior(Vector2 pos, string stage, int targetNpc, float rt)
    {
        var go = UnitFactory.Instance != null
            ? UnitFactory.Instance.SpawnUnit(Faction.PlayerCamp, Occupation.Warrior, pos, 1)
            : null;
        var euc = go != null ? go.GetComponent<UnitController>() : null;
        if (euc != null) _threatEnemies.Add(euc);
        U16Write($"{U16Tag}·threat-inject] {Stamp()} +{rt:F2}s 段={stage} AI战士 @ ({pos.x:F2},{pos.y:F2})"
                 + $" 目标 npc={targetNpc} 在册={InMap(targetNpc)} Istask={FlagOf(targetNpc)}"
                 + $" 生成={(go != null ? go.name : "失败")}"
                 + $" 阵营={(euc != null ? euc.GetFaction().ToString() : "-")}");
    }

    /// <summary>⭐ 判据 4（豁免列）：威胁注入后**逐秒**读数 —— 可辨「能逃（距怪↓却在动/远离任务点）vs 继续干活（距dest↓）」。
    /// 焦点＝`brain.DebugFocusDecision`（`_lastCtx` 现成读口）·谱系＝`DebugPostureDecision.Spectrum`。</summary>
    private static void U16ThreatLine(float rt)
    {
        var uc = FindUnit(_threatNpcId);
        if (uc == null)
        {
            U16Write($"{U16Tag}·yield] {Stamp()} +{rt:F2}s npc={_threatNpcId} uc=null（已亡/被回收）");
            return;
        }
        var brain = uc.GetComponent<NPCBrain>();
        string focus = "?", spec = "?";
        if (brain != null)
        {
            var fd = brain.DebugFocusDecision;
            focus = fd.IsValid && fd.Focus != null ? fd.Focus.GetType().Name : "Invalid";
            spec = brain.DebugPostureDecision.Spectrum.ToString();
        }
        float dEnemy = -1f;
        float best = float.MaxValue;
        for (int i = 0; i < _threatEnemies.Count; i++)
        {
            var e = _threatEnemies[i];
            if (e == null || !e.IsAlive) continue;   // ⚠️ 威胁源＝**注入的 AI 国战士**（⛔ 非 `Faction.Monster`）
            float d = Vector2.Distance(uc.transform.position, e.transform.position);
            if (d < best) best = d;
        }
        if (best < float.MaxValue) dEnemy = best;
        // ⭐ 「继续干活」的判据读数：**距任务目标点（`PathFollower._destination`）** ⇒ ↓＝在朝任务走。
        var pf = uc.GetComponent<PathFollower>();
        Vector2 dest = pf != null ? PfDest(pf) : Vector2.zero;
        float dDest = dest == Vector2.zero ? -1f : Vector2.Distance(uc.transform.position, dest);
        string line = $"{U16Tag}·yield] {Stamp()} +{rt:F2}s npc={_threatNpcId} 焦点={focus} 谱系={spec}"
                      + $" Istask={FlagOf(_threatNpcId)} 在册={InMap(_threatNpcId)}"
                      + $" 站位=({uc.transform.position.x:F2},{uc.transform.position.y:F2})"
                      + $" 距dest={dDest:F2} dest=({dest.x:F2},{dest.y:F2})"
                      + $" 距最近敌={dEnemy:F2}";
        U16Write(line);
        _threatSeq.Add(line);
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
        // ---- `U-16` 案① 批新增（判据 3／4／7）----
        sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 7 · Abandon 次数（按 npc）：{DictLine(_abandonN)}　合计={SumOf(_abandonN)}");
        sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 7 · Abandon 次数（按 reason）：{ReasonLine()}");
        sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 3 · 派发时刻 Istask=True {_dispatchFlagTrue} ／ False {_dispatchFlagFalse}"
                      + $"　完成时刻 Istask=True {_completeFlagTrue} ／ False {_completeFlagFalse}"
                      + $"　⇒ 完成侧全 False={(_completeFlagTrue == 0)}（复位配对）");
        // ---- ⭐⭐ `U-16b` 件B（`D816` §八 判据 1/2/5 新口径）----
        sb.AppendLine($"{U16Tag}·summary] ⭐⭐ 判据 2（`U-16b` 新口径）：`完成 WaterHaul` 总 **{_completeWaterHaul}** 条"
                      + $" ／ ⭐ **来源＝`MovingToDest` 到达 ＝ {_completeWaterHaulFromDest} 条（唯一计入）**"
                      + $" ／ 异常分支 ＝ {_completeAnomalyN} 条（`Working` 装载失败等）");
        sb.AppendLine($"{U16Tag}·summary]     完成来源分支分布：{SrcLine()}");
        // ⭐ `U-20` 件2：**盲区率**（来源＝未知 ／ 完成总数）—— ⛔ 本批（`4de46407`/`4259f2fc`）实测 131/158 ＝ 83%。
        sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 2 补强 · 来源**盲区率**：未知 {_unknownSrcN} ／ 总 {_srcTotalN}"
                      + $" ＝ {(_srcTotalN > 0 ? (_unknownSrcN * 100f / _srcTotalN).ToString("F1") : "0")}%"
                      + $"（盲区＝`ClearNpc` 先于日志发 ⇒ 只能回看；⛔ 由下一行的 `Working计时已到` 布尔补强，两列不一致即报）");
        sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 3 · Abandon 按 `type/reason` 分桶：{TypeReasonLine()}");
        // ⭐ `U-20` 收口件2：`Unreachable` 前二名 npc 占比（期望 ≥99% ⇒ 自证 `甲′-a` 独占 · `L-67`）
        sb.AppendLine($"{U16Tag}·summary] ⭐ 收口件2 · `Unreachable` 前二名 npc 占比：{UnreachTop2Line()}");
        // ⭐⭐ `F-1` 收尾批（`D820` §三）判据 2：**同农场在途 WaterHaul 并发数**分布（`D-1` 修后应 `=1` 为主 · ⛔ 基线无此列）
        {
            var l2 = new List<KeyValuePair<int, int>>(_farmConcN);
            l2.Sort((a, b) => a.Key.CompareTo(b.Key));
            var s2 = new StringBuilder();
            foreach (var kv in l2) s2.Append($"并发{kv.Key}×{kv.Value}秒 ");
            sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 2（`D-1`）· **同农场在途 WaterHaul 并发数**分布（按秒）：{(s2.Length > 0 ? s2.ToString().Trim() : "（无读数）")}"
                          + " ⇒ ⭐ `=1` 为主＝去重生效（>1 的秒数即「该拦没拦」残留）；基线此列**不存在**（恒不生效 ⇒ 应见 2+ 秒数）");
        }
        // ⭐⭐ 判据 1 两新列 ＋ 判据 4 两侧读数
        {
            var l3 = new List<KeyValuePair<string, int>>(_dispatchTypeN);
            l3.Sort((a, b) => b.Value.CompareTo(a.Value));
            var s3 = new StringBuilder();
            foreach (var kv in l3) s3.Append($"{kv.Key}×{kv.Value} ");
            sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 4（`D-2`）· **派发按类型计数**：{(s3.Length > 0 ? s3.ToString().Trim() : "（无）")}"
                          + " ⇒ `WaterHaul` 应 ↑（前置生效）、`Production` 应 ↓（缺水期让位 · 可接受）");
            sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 1 新列汇总 · 农场「有工人 Working」秒数={_farmWorkFrames} ／ 无={_farmIdleFrames}"
                          + $" ｜ 「缺水产」秒数={_farmThirstFrames}（其水位峰值={_farmWaterPeakInThirst}）／ 不缺水={_farmOkFrames}"
                          + " ⇒ ⭐ 缺水秒数应显著下降（基线 34/37 条 `·farm]` 水位=0 —— `D-2` 饥饿）");
        }
        sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 1 · 卸货到账（水位式）：农场仓水峰值＝{_farmWaterPeak2}"
                      + $" 首次见水＝{(_farmGained ? _farmFirstGainRt.ToString("F2") + "s" : "未见")}"
                      + $" ⇒ 到账={(_farmWaterPeak2 > 0)}");
        sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 5（`U-16b` 新口径）：**异常完成**（来源非 `MovingToDest`）＝ **{_completeAnomalyN}**"
                      + " ⇒ 应为 0（⛔ 不得与「装载失败 ⇒ 直接完成」混判）");
        if (_threatInjected)
        {
            sb.AppendLine($"{U16Tag}·summary] ⭐ 判据 4 · 威胁注入后序列（共 {_threatSeq.Count} 条）：");
            for (int i = 0; i < _threatSeq.Count; i++) sb.AppendLine($"{U16Tag}·summary]   {_threatSeq[i]}");
        }
        else sb.AppendLine($"{U16Tag}·summary] 判据 4：（本档未注入威胁 ⇒ 豁免列请跑「…_豁免列(威胁注入)」菜单）");
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

    /// <summary>⭐ `U-20` 收口件2：`Unreachable` **前二名 npc 占比**（npc × 次数 × 占比）。
    /// 口径（`D818` §三 改裁）：`Unreachable` 的唯一来源是 `甲′-a`（恒困死工人 · `U-18` 未修）⇒
    /// **暴露量 ≠ 回归指标**（`L-67`）⇒ 本行用于**持续自证**「`Unreachable` 由极少数恒困死 npc 独占」，
    /// ⛔ 不改任何判据语义。</summary>
    private static string UnreachTop2Line()
    {
        int total = 0; foreach (var kv in _unreachByNpc) total += kv.Value;
        if (total == 0) return "（无）";
        var list = new List<KeyValuePair<int, int>>(_unreachByNpc);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        var sb = new StringBuilder($"总数={total} ｜ ");
        int top2 = 0;
        for (int i = 0; i < list.Count && i < 2; i++)
        {
            top2 += list[i].Value;
            sb.Append($"npc{list[i].Key}×{list[i].Value}（{(list[i].Value * 100f / total):F1}%） ");
        }
        sb.Append($"⇒ 前二名合计={top2} ＝ {(top2 * 100f / total):F1}%（判据 ≥99%）");
        if (list.Count > 2) sb.Append($" ｜ 其余 {list.Count - 2} 个 npc 合计={total - top2}");
        return sb.ToString();
    }

    /// <summary>⭐ `U-20` 件3：Abandon 的 `type/reason` 分桶行（长列表 ⇒ 按计数降序，便于定性两段任务 vs 短程任务）。</summary>
    private static string TypeReasonLine()
    {
        if (_abandonTypeReason.Count == 0) return "（无）";
        var list = new List<KeyValuePair<string, int>>(_abandonTypeReason);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        var sb = new StringBuilder();
        for (int i = 0; i < list.Count; i++) sb.Append($"{list[i].Key}×{list[i].Value} ");
        return sb.ToString().Trim();
    }

    /// <summary>⭐ `U-16b` 判据 2：完成来源分支分布（`type/来源state` ⇒ 计数）。</summary>
    private static string SrcLine()
    {
        if (_completeSourceN.Count == 0) return "（无完成）";
        var sb = new StringBuilder();
        foreach (var kv in _completeSourceN) sb.Append($"{kv.Key}×{kv.Value} ");
        return sb.ToString().Trim();
    }

    /// <summary>⭐ 判据 7：`Abandon` 按 reason 分桶（**日志契约取值域 7 值**；`Inert` ⛔ 不入枚举 ⇒ 不会出现）。</summary>
    private static string ReasonLine()
    {
        if (_abandonReasonN.Count == 0) return "（无 —— ⚠️ 本批新增日志：改前恒 0 条）";
        var sb = new StringBuilder();
        foreach (var kv in _abandonReasonN) sb.Append($"{kv.Key}×{kv.Value} ");
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
        // ⭐ `U-16` 件3（`D815` 裁定 §二 件3）：落位前**可走吸附**（`HF-0`）—— 先例逐字＝`TestFixtureApi.cs:250`
        //   （`SpawnFixtureUnit`：`SpawnPosSnapper.SnapWorld(world, $"fixture_k…")`）。
        //   ⚠️ `UnitFactory.SpawnUnit` **自身不做吸附**（其消费链＝PopulationSystem／KingdomFoundry／
        //   VagrantCampSystem 各自吸附）⇒ `D813` 实测：探针工人落 `站位格可走=False（地形不可走）`
        //   ⇒ `A*复演` 恒 `Unreachable` ⇒ 甲′-a 靶例**恒不可达**（判据可信度前提）。
        //   ⛔ 本批**只改探针侧**；⛔ 不动 `UnitFactory`（`U-18` 的"甲/乙"未裁）。
        pos = SpawnPosSnapper.SnapWorld(pos, "hh319_worker");
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
