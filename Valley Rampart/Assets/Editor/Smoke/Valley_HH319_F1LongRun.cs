using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.319 `M1-F` · `F-1` 补证单（`D809`）件 1：**真实长局观察**（生产路径搬水链）
//  用法：菜单「Valley/验证/HH319 F1长局观察_1x」／「…_15x对照」——进 Play 后点（或 MCP 触菜单）。
//  ⛔ 本容器**不干预**搬水链：只做「初始布置 ＋ 观测」（除"井仓水位压制"以维持靶例 · 见下）。
//  ⭐ 件1 核心问题：15× 档下 dt 可达钳制上限 1.0s/帧 ⇒ 寻路/到达判定**跳步**（假设）
//     ⇒ 故 1× 复跑对照；1× 与 15× 读数不同 ⇒ 该假设成立（须明确报出）。
//  收尾：ExitTestRun ＋ QuitSmoke（⛔ 不留 1× 余留世界 · L-32）。
// ============================================================================
public static class Valley_HH319_F1LongRun
{
    private const int SEED = 20319;

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
            UnitController nearest = null; float nearestD = float.MaxValue;
            foreach (var uc in PlayerWorkers())
            {
                var st = TaskScheduler.Instance != null ? TaskScheduler.Instance.GetWorkerState(uc.npcId) : TaskState.None;
                var inv = uc.GetComponent<WorkerInventory>();
                int bag = inv != null ? inv.carriedAmount : 0;
                float dw = Vector2.Distance(uc.transform.position, well.transform.position);
                float df = Vector2.Distance(uc.transform.position, farm.transform.position);
                if (dw < nearestD) { nearestD = dw; nearest = uc; }
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
            // ⑦ 距离序列（每 1 真实秒一行 · 为 U-14 定严重度）
            if (nearest != null && rt >= nextDiag)
            {
                nextDiag = rt + 1f;
                var stN = TaskScheduler.Instance != null ? TaskScheduler.Instance.GetWorkerState(nearest.npcId) : TaskState.None;
                var invN = nearest.GetComponent<WorkerInventory>();
                Debug.Log($"[HH319长局·{tag}·seq] +{rt:F1}s 最近工人 npc{nearest.npcId} st={stN} 距井={nearestD:F2} "
                          + $"距农场={Vector2.Distance(nearest.transform.position, farm.transform.position):F2} "
                          + $"背包={(invN != null ? invN.carriedAmount : -1)} 井水={wellStore.GetAmount(ResourceType.Water)} 农场水={fwNow}");
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
        Debug.Log($"[HH319长局·{tag}] ── 收尾（ExitTestRun ＋ 退 Play · ⛔ 不留 1× 余留世界）");
        TestHarnessApi.ExitTestRun();
        SmokeApi.QuitSmoke();
        yield break;
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
        var ts = TaskScheduler.Instance;
        if (ts == null) return d;
        var f = typeof(TaskScheduler).GetField("_npcTaskMap", BindingFlags.NonPublic | BindingFlags.Instance);
        var map = f != null ? f.GetValue(ts) as Dictionary<int, KingdomTask> : null;
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
