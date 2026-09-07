using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.92 T18 MVP 演示容器 TestHarnessDemo（D549；真实进局链+幽灵化，非合成容器 M14）
//  用法：GameScene Play 后菜单「Valley/验证/TestHarnessDemo_MVP」。跑完自动 QuitSmoke。
//
//  R1/R3（15x）+ R2（10x 对照基线，T4）：同 seed=907 小世界（M13）三轮建拆建（T15 零残留三查）。
//  每轮：EnterTestRun → 玩家幽灵化(T10) → PlaceKingdom(军事期)×2(T6~T9) →
//        第 5 日起 20 日观察窗（§3.4：AI 需 2~5 日消化底数）→ 四断言 → 清场。
//
//  四条 MVP 断言（任务书 §四）：
//    A1 成熟王国演示：观察窗内 fixture 国 新建筑≥1（AI 自主建造决策）
//    A2 扩军：fixture 国 warriorCount 增量≥1（AI 自主军事决策）
//    A3 零 GameOver：全程 GameState=Playing（ThroneAnchor 考跑守卫封死 D111 链）
//    A4 零 NRE：logMessageReceived 异常计数=0
//  T4 对照：R1(15x) 与 R2(10x) 判定列逐项对拍（日级事件，timeScale 只压挂钟）；
//           1x 档 wall-clock 不经济（20 日=2h），列报留 P0 调优批按需跑（结构等价=AdvanceTime 日序同链）。
//  T15 三查（每轮清场后）：实体计数=0 / EventBus 已知事件订阅在场（全量计数口列报）/ 单例静态字典抽查
//           （WaterNetwork 旧桶=0+KingdomBrainRegistry.Count=0=T13 补缺实证）。
// ============================================================================
public static class Valley_TestHarnessDemo
{
    private const int SEED = 907;
    private const int OBS_DAYS = 20;      // 观察窗
    private const int WARMUP_DAYS = 5;    // §3.4：断言从注入后第 5 日算起

    [MenuItem("Valley/验证/TestHarnessDemo_MVP")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[THD] 须先 GameScene 进 Play。"); return; }
        new GameObject("THD_Runner").AddComponent<RunHost>().Host(RunCoroutine());
    }

    private class RunHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    private static int _nreCount;
    private static readonly List<string> _errors = new List<string>();

    private static IEnumerator RunCoroutine()
    {
        var results = new List<string>();
        Application.logMessageReceived += OnLog;
        _nreCount = 0; _errors.Clear();

        var snapshotPrev = new List<UnitController>();
        MapData prevMap = null;
        var roundVerdicts = new List<string>();

        float[] speeds = { 15f, 10f, 15f };   // R1=15x / R2=10x 对照 / R3=15x（第 3 轮建拆建）
        for (int round = 0; round < speeds.Length; round++)
        {
            float spd = speeds[round];
            var roundLog = new List<string>();

            // ---- 进局+考跑一体开关（T3）----
            yield return TestHarnessApi.EnterTestRun(new NewGameConfig
            {
                worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
                worldSize = WorldSize.Small, selectedSlotId = "smoke_thd_" + round,
                kingdomName = "THD演示"
            }, spd);

            // ---- T10 玩家幽灵化（GameOver 链已由 ThroneAnchor 考跑守卫封死）----
            var ghost = TestFixtureApi.EnablePlayerGhostMode();

            // ---- T6~T9：军事期 fixture 王国 ×2（固定对角取点，确定性 M15）----
            var map = WorldManager.Instance.ActiveMap;
            var posA = MapGenRules.NearestWalkable(map, 24, 24);
            var posB = MapGenRules.NearestWalkable(map, map.width - 24, map.height - 24);
            var kA = TestFixtureApi.PlaceKingdom(FixtureTier.Military, raceId: 0, pos: posA, name: "THD甲");
            var kB = TestFixtureApi.PlaceKingdom(FixtureTier.Military, raceId: 2, pos: posB, name: "THD乙");
            int[] fks = { kA != null ? kA.id : -1, kB != null ? kB.id : -1 };
            int baseBldA = CountKingdomBuildings(fks[0]), baseBldB = CountKingdomBuildings(fks[1]);
            int baseWarA = kA != null ? kA.warriorCount : 0, baseWarB = kB != null ? kB.warriorCount : 0;
            roundLog.Add($"ghost清实体={ghost.cleared} 王国注册={ghost.kingdomCount} fixture=[{fks[0]},{fks[1]}] " +
                         $"建筑基线=[{baseBldA},{baseBldB}] 战士基线=[{baseWarA},{baseWarB}]");

            // ---- 观察窗：等 WARMUP+OBS 天（日级事件推进；考跑 timeScale 压挂钟）----
            var tm = TimeManager.Instance;
            int startDay = tm.CurrentDay;
            int targetDay = startDay + WARMUP_DAYS + OBS_DAYS;
            int lastLoggedDay = -1;
            float t0 = Time.realtimeSinceStartup;
            bool gameoverSeen = false;
            while (tm.CurrentDay < targetDay)
            {
                yield return null;
                if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
                { gameoverSeen = true; break; }
                if (tm.CurrentDay != lastLoggedDay && tm.CurrentDay > startDay)
                {
                    lastLoggedDay = tm.CurrentDay;
                    foreach (var kid in fks)
                    {
                        var k = KingdomRegistry.Instance.Get(kid);
                        if (k == null) continue;
                        var s3 = TestFixtureApi.ReadThreeSources(kid);
                        Debug.Log($"[THD][CSV] spd={spd:0.##},day={tm.CurrentDay},k={kid},workers={k.workerCount},warriors={k.warriorCount}," +
                                  $"food={k.resources.food},gold={k.resources.gold},water={s3.water:0}");
                    }
                }
                if (Time.realtimeSinceStartup - t0 > 1500f) break;   // 25 分钟硬顶（防卡死勿硬等）
            }
            int obsDays = tm.CurrentDay - startDay;

            // ---- 四条 MVP 断言（观察窗=第 5 日起的 20 日）----
            int bldA = CountKingdomBuildings(fks[0]), bldB = CountKingdomBuildings(fks[1]);
            int warA = KingdomRegistry.Instance.Get(fks[0]) != null ? KingdomRegistry.Instance.Get(fks[0]).warriorCount : 0;
            int warB = KingdomRegistry.Instance.Get(fks[1]) != null ? KingdomRegistry.Instance.Get(fks[1]).warriorCount : 0;
            bool a1 = (bldA - baseBldA) + (bldB - baseBldB) >= 1;
            bool a2 = (warA - baseWarA) + (warB - baseWarB) >= 1;
            bool a3 = !gameoverSeen && GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Playing;
            bool a4 = _nreCount == 0;
            roundLog.Add($"A1 新建筑≥1 = {a1}（Δ甲={bldA - baseBldA} Δ乙={bldB - baseBldB}）");
            roundLog.Add($"A2 扩军≥1 = {a2}（Δ甲={warA - baseWarA} Δ乙={warB - baseWarB}）");
            roundLog.Add($"A3 零GameOver = {a3}（观察 {obsDays} 日，态={GameStateManager.Instance.CurrentState}）");
            roundLog.Add($"A4 零NRE = {a4}（异常计数={_nreCount}）");
            var birth = TestFixtureApi.EvaluateBirthConditions(fks[0]);
            roundLog.Add($"T9 生育条件（窗末快照 k{fks[0]}）：幸福{birth.happiness:F0}/饱食{birth.satiety:F0}/房容{birth.houseCapacity}/人口{birth.population} pass={birth.pass}");
            roundVerdicts.Add($"R{round + 1}({spd:0.##}x)：" + string.Join("；", roundLog));

            // ---- 清场（T15 三查）----
            snapshotPrev = new List<UnitController>(UnitRegistry.Instance.GetAllUnits());
            prevMap = WorldManager.Instance.ActiveMap;
            int brainCountBefore = KingdomBrainRegistry.Instance != null ? KingdomBrainRegistry.Instance.Count : -1;
            float oldWater = WaterNetwork.Instance != null ? WaterNetwork.Instance.GetStored(fks[0]) : -1f;
            TestHarnessApi.ExitTestRun();
            SmokeApi.ResetWorldForNext();
            yield return null;   // 陷阱2：留一帧

            int unitAfter = CountUnits();
            bool mapNew = WorldManager.Instance.ActiveMap != prevMap;
            int brainAfter = KingdomBrainRegistry.Instance != null ? KingdomBrainRegistry.Instance.Count : -1;
            float waterAfter = WaterNetwork.Instance != null ? WaterNetwork.Instance.GetStored(fks[0]) : -1f;
            bool ebSpot = EventBus.HasSubscribers<TimeDayChangedEvent>() && EventBus.HasSubscribers<UnitDiedEvent>();
            results.Add($"T15 R{round + 1} 零残留三查：实体={unitAfter}(需0) 地图新实例={mapNew} 脑注册 {brainCountBefore}→{brainAfter}(需0=T13) " +
                        $"旧水桶 {oldWater:0}→{waterAfter:0}(需0=T13) EventBus抽查={ebSpot} ={(unitAfter == 0 && mapNew && brainAfter == 0 && waterAfter == 0)}");
        }

        Application.logMessageReceived -= OnLog;

        // HH.92 收口修正：汇总判定必须覆盖 roundVerdicts（A1~A4 明细）——v3 实测 A2=False 因本缺陷
        // 未参与 allPass 且 T4 行 "=False"（无空格）躲过 "= False" 匹配，导致汇总误显示 ALL PASS。
        bool t4ok = roundVerdicts.Count >= 2 && !roundVerdicts[0].Contains("= False") && !roundVerdicts[1].Contains("= False");
        results.Add($"T4 15x↔10x 对照：两轮判定列无 False ={t4ok}（1x 档 wall-clock 不经济列报留 P0 调优批）");
        foreach (var e in _errors) results.Add("异常明细：" + e);

        bool roundAllPass = !roundVerdicts.Exists(x => x.Contains("= False"));
        bool allPass = roundAllPass && !results.Exists(x => x.Contains("= False")) && _errors.Count == 0;
        Debug.Log("[THD] 轮汇总 " + (allPass ? "ALL PASS" : "HAS FAIL") + "\n" +
                  string.Join("\n", results) + "\n" + string.Join("\n", roundVerdicts));
        SmokeApi.QuitSmoke();
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        // 只计项目代码异常（Assets/ 栈）；编辑器内部噪声（VS 包/AssetStoreDownloadManager）不计入 A4。
        if (type != LogType.Exception && type != LogType.Error) return;
        bool projectCode = (stackTrace != null && stackTrace.Contains("Assets/"))
                           || (condition != null && condition.Contains("Assets/"));
        if (!projectCode) return;
        _nreCount++;
        if (_errors.Count < 10) _errors.Add($"{type}:{condition}");
    }

    private static int CountKingdomBuildings(int kingdomId)
    {
        if (BuildingRegistry.Instance == null || kingdomId < 0) return 0;
        int n = 0;
        var all = BuildingRegistry.Instance.All;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].kingdomId == kingdomId) n++;
        return n;
    }

    private static int CountUnits()
    {
        if (UnitRegistry.Instance == null) return 0;
        int n = 0;
        foreach (var u in UnitRegistry.Instance.GetAllUnits()) if (u != null) n++;
        return n;
    }
}
