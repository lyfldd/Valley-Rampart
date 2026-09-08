using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  HH.100/P0 件A 诊断跑局容器（D554 获批终版五组+F-T3 gold 消歧，七局批）。
//  每局独立建局（同 seed=907 小世界，M7）：Midgame×2 对角（工10/兵4=五考复刻态）
//  → 变量注入 → 20 游戏日观察（15x 考跑）→ 逐日 CSV → 清场。
//
//  环境事实（判读口径基准，HH.100 §六/报告跑局卷同源）：
//   · T11 MonsterSpawner 考跑守卫使考跑局天然无野怪 → 基线组=「无袭扰条件」本体；
//   · ②组以「袭扰注入」（每游戏日 Publish KingdomAttackedEvent×3 模拟五考日均杀工节奏）
//     实现 differential 的另一半：注入→锁14+兵停=H3 实锤；基线无袭扰→兵招募启动=评分链健康。
//
//  判读产出（A-T1 跑局卷）：A-1 逐行死活 + A-2 四假设 + F-T2 寻路首日定性 + F-T3 gold 消歧。
//  产出后动作：处方呈报（获批前零调参）；DiagBypassDefenseWindow 跑后复位 false。
// ============================================================================
public static class Valley_P0_Diag
{
    private const int SEED = 907;
    private const int OBS_DAYS = 20;
    private const float ATTACKS_PER_DAY = 3f;   // 袭扰注入频率（模拟五考日均杀工 3 节奏）

    [MenuItem("Valley/诊断/P0_Diag_五组跑批")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[P0D] 须先 GameScene 进 Play。"); return; }
        new GameObject("P0D_Runner").AddComponent<DiagHost>().Host(RunAll(null, "七局"));
    }

    /// <summary>HH.101/D556：H3 复现轮（d2 food 保底标准件+袭扰注入 differential）——ffbase(兵4+food保底,无袭扰) vs ffatk(+袭扰3/日)。</summary>
    [MenuItem("Valley/诊断/P0_Diag_H3复现轮")]
    public static void RunH3()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[P0D] 须先 GameScene 进 Play。"); return; }
        new GameObject("P0D_Runner").AddComponent<DiagHost>().Host(RunAll(new[] { "ffatk" }, "H3复现轮"));
    }

    private class DiagHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    private static int _atkCount;
    private static readonly List<string> _pathFails = new List<string>();
    private static bool _pathFailDay1Done;

    private static IEnumerator RunAll(string[] onlyIds, string batchLabel)
    {
        var variants = new (string id, string label, System.Action<int, int> inject)[]
        {
            ("baseline",  "基线",        null),
            ("attack3",   "袭扰注入3每日", InjectNothing),   // 注入在日循环内做（按日切发事件）
            ("war6",      "兵4到6",       (k, pos) => { TestFixtureApi.SpawnFixtureUnitForDiag(Occupation.Warrior, ToVec(pos), k, 100);
                                                          TestFixtureApi.SpawnFixtureUnitForDiag(Occupation.Warrior, ToVec(pos), k, 101); }),
            ("land1",     "领土加1块",    (k, pos) => ClaimOneChunk(k)),
            ("happy5",    "幸福加5",      (k, pos) => AddHappiness(k, 5f)),
            ("unlock14",  "14解锁",       null),            // flag 在局首置位
            ("goldx",     "gold消歧",     null),            // 日切保底 gold>=500
            ("ffbase",    "food保底无袭扰", null),          // D556/d2：粮警解除基线（H3 differential 左半）
            ("ffatk",     "food保底加袭扰", null),          // D556：+袭扰注入3/日（右半；单变量=袭扰）
        };
        Application.logMessageReceived += OnLog;
        EventBus.Subscribe<KingdomAttackedEvent>(OnAtk);
        var summary = new List<string>();
        for (int i = 0; i < variants.Length; i++)
        {
            if (onlyIds != null && System.Array.IndexOf(onlyIds, variants[i].id) < 0) continue;
            yield return RunOne(variants[i].id, variants[i].label, variants[i].inject, summary);
        }
        EventBus.Unsubscribe<KingdomAttackedEvent>(OnAtk);
        Application.logMessageReceived -= OnLog;
        FocusController.DiagBypassDefenseWindow = false;   // M7：跑后复位（裁决区④：跑后摘除）
        Debug.Log($"[P0D] ===== {batchLabel}汇总 =====\n" + string.Join("\n", summary));
        SmokeApi.QuitSmoke();
    }

    private static void InjectNothing(int k, int pos) { }

    /// <summary>找王国首个存活指定职业单位（管线注入用）。</summary>
    private static UnitController FirstAliveUnit(int kid, Occupation occ)
    {
        var reg = UnitRegistry.Instance;
        if (reg == null) return null;
        foreach (var u in new List<UnitController>(reg.GetAllUnits()))
        {
            if (u != null && u.IsAlive && u.kingdomId == kid && u.Data != null && u.Data.occupation == occ)
                return u;
        }
        return null;
    }

    private static Vector2Int ToVec(int pos) => new Vector2Int(pos >> 16, pos & 0xffff);

    private static int PackVec(Vector2Int v) => (v.x << 16) | (v.y & 0xffff);

    // ===== 单局 =====

    private static IEnumerator RunOne(string id, string label, System.Action<int, int> inject, List<string> summary)
    {
        FocusController.DiagBypassDefenseWindow = (id == "unlock14");
        _atkCount = 0; _atkCountA = 0; _pathFails.Clear(); _pathFailDay1Done = false;

        yield return TestHarnessApi.EnterTestRun(new NewGameConfig
        {
            worldSeed = SEED, mapSeed = SEED, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Small, selectedSlotId = "p0diag_" + id, kingdomName = "P0诊断"
        }, 15f);

        var map = WorldManager.Instance.ActiveMap;
        var posA = MapGenRules.NearestWalkable(map, 24, 24);
        var posB = MapGenRules.NearestWalkable(map, map.width - 24, map.height - 24);
        var kA = TestFixtureApi.PlaceKingdom(FixtureTier.Midgame, 0, posA, "诊断甲");
        var kB = TestFixtureApi.PlaceKingdom(FixtureTier.Midgame, 2, posB, "诊断乙");
        int kidA = kA != null ? kA.id : -1;
        int kidB = kB != null ? kB.id : -1;
        _diagKidA = kidA;
        if (kidA < 0 || kidB < 0) { summary.Add($"{label}({id})：PlaceKingdom 失败，局作废"); yield break; }

        if (inject != null) inject(kidA, PackVec(posA));

        // ⑦ def 缓存（NeedScore 消歧用；struct→Nullable）
        UtilityActionDef? def7 = null;
        var utilCfg = UtilityActionConfig.LoadConfig();
        if (utilCfg != null && utilCfg.actions != null)
            foreach (var d in utilCfg.actions) if (d.id == UtilityAction.RecruitWarrior) { def7 = d; break; }

        var tm = TimeManager.Instance;
        int startDay = tm.CurrentDay;
        int targetDay = startDay + OBS_DAYS;
        int lastDay = -1;
        int focus14Days = 0;
        float t0 = Time.realtimeSinceStartup;
        bool gameoverSeen = false;

        while (tm.CurrentDay < targetDay)
        {
            yield return null;
            if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
            { gameoverSeen = true; break; }

            int day = tm.CurrentDay;
            if (day != lastDay)
            {
                // ---- 日切动作 ----
                if (lastDay >= 0)   // 非首日
                {
                    var kKeep = KingdomRegistry.Instance.Get(kidA);
                    if (id == "attack3")
                        for (int i = 0; i < ATTACKS_PER_DAY; i++)
                            EventBus.Publish(new KingdomAttackedEvent(kidA));   // 旁路注入（历史口径：绕过 DamageSystem，不受处方 A 节流）
                    if (id == "ffatk")
                    {
                        // D556/A 验证：走真实受击管线（RegisterAttack→ExecuteAttack→ApplyDamage→PublishDamagedEvent→per-kingdom 节流点）。
                        // ⚠️必须近战（即时命中）：远程=位置驱动弹，移动 worker 必 miss→伤害永不落地（A 验证轮一版实测实收=0）。
                        // cd=360 游戏秒=日均 1 击（≈五考袭扰节奏）；attack=1 保 worker 不死；range=999 过距离检查（对角注入）。
                        var atk = FirstAliveUnit(kidB, Occupation.Warrior);
                        var tgt = FirstAliveUnit(kidA, Occupation.Worker);
                        if (atk != null && tgt != null && DamageSystem.Instance != null)
                            DamageSystem.Instance.RegisterAttack(atk, tgt, new AttackProfile
                            {
                                attack = 1, range = 999f, cd = 360f, isRanged = false
                            });
                        else
                            Debug.LogWarning($"[P0D] ffatk 管线注入缺单位 atk={(atk != null)} tgt={(tgt != null)}");
                    }
                    if (id == "goldx" && kKeep != null)
                    {
                        if (kKeep.resources.gold < 500) kKeep.resources.gold = 500;   // 消歧：永不缺金
                    }
                    // D556/d2 food 保底标准件：粮警解除（grainAlarm 线=grainReserveDaysFloor×pop≈28，保底 200 留余量）
                    if ((id == "ffbase" || id == "ffatk") && kKeep != null && kKeep.resources.food < 200)
                        kKeep.resources.food = 200;
                    if (id == "happy5") AddHappiness(kidA, 5f);   // 每日维持（日结重算后补注）
                }

                // F-T2 首日定性输出（日切进入第 2 日时=首日数据齐）
                if (!_pathFailDay1Done && day >= startDay + 2)
                {
                    _pathFailDay1Done = true;
                    Debug.Log($"[P0D][FT2] {label} 首日寻路不可达={_pathFails.Count} 条；样本（≤5）：\n" +
                              string.Join("\n", _pathFails.GetRange(0, System.Math.Min(5, _pathFails.Count))));
                }

                // 逐日 CSV（两国）
                lastDay = day;
                foreach (var kid in new[] { kidA, kidB })
                {
                    var k = KingdomRegistry.Instance.Get(kid);
                    if (k == null) continue;
                    float n7 = def7 != null ? UtilityScorer.NeedScore(k, def7.Value) : -1f;
                    if (k.focus == (int)UtilityAction.Defense && kid == kidA) focus14Days++;
                    Debug.Log($"[P0D][CSV] var={id},day={day},k={kid},focus={k.focus},workers={k.workerCount},warriors={k.warriorCount}," +
                              $"gold={k.resources.gold:0},food={k.resources.food:0},stage={k.scriptPhase},{(kid == kidA ? $"n7={n7:F2}" : $"atkAll={_atkCount}")}");
                }
                if (id == "attack3" || id == "ffatk")
                    Debug.Log($"[P0D][ATK] var={id} day={day} 全局实收={_atkCount} kidA实收={_atkCountA}");
            }

            if (Time.realtimeSinceStartup - t0 > 900f) { Debug.LogWarning($"[P0D] {label} 15 分钟硬顶break"); break; }
        }

        int obsDays = tm.CurrentDay - startDay;
        var endA = KingdomRegistry.Instance.Get(kidA);
        var endB = KingdomRegistry.Instance.Get(kidB);
        summary.Add($"{label}({id})：观察{obsDays}日 focus14占={focus14Days}日｜甲 终态stage={endA?.scriptPhase} 兵={endA?.warriorCount} 工={endA?.workerCount} gold={endA?.resources.gold:0}｜乙 兵={endB?.warriorCount}｜寻路不可达总数={_pathFails.Count}｜{(gameoverSeen ? "⚠GameOver" : "态Playing")}");

        TestHarnessApi.ExitTestRun();
        SmokeApi.ResetWorldForNext();
        yield return null;   // 清场陷阱：留一帧
    }

    // ===== 注入器 =====

    /// <summary>领土+1 中区块：找己方领土 4 邻接首个无主 mid，ClaimFootprintChunk 注入（生产链公开接口）。</summary>
    private static void ClaimOneChunk(int kid)
    {
        var ts = TerritorySystem.Instance;
        var grid = GridSystem.Instance;
        if (ts == null || grid == null) { Debug.LogError("[P0D] TerritorySystem/Grid 缺失"); return; }
        var k = KingdomRegistry.Instance.Get(kid);
        var all = ts.GetAllTerritory();
        foreach (var mid in k.Territory)
        {
            foreach (var d in new Vector2Int[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                var cand = mid + d;
                if (!all.ContainsKey(cand))
                {
                    ts.ClaimFootprintChunk(kid, new GridCoord(cand.x * 4 + 1, cand.y * 4 + 1, 0));   // mid 内任一格（4 格/中区块，取 (1,1) 偏移）
                    Debug.Log($"[P0D] 领土+1 注入：k{kid} mid={cand} → Territory.Count={k.Territory.Count}");
                    return;
                }
            }
        }
        Debug.LogWarning("[P0D] 领土+1：未找到无主邻接中区块（四面全有主？）");
    }

    /// <summary>幸福注入（反射私有桶；诊断专用，日结会重算覆盖→每日维持注入）。</summary>
    private static void AddHappiness(int kid, float delta)
    {
        var hs = HappinessSystem.Instance;
        if (hs == null) return;
        var f = typeof(HappinessSystem).GetField("_overallHappiness", BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = f?.GetValue(hs) as Dictionary<int, float>;
        if (dict == null) { Debug.LogError("[P0D] _overallHappiness 反射失败"); return; }
        dict[kid] = (dict.TryGetValue(kid, out float cur) ? cur : 50f) + delta;
    }

    // ===== 日志抓取（F-T2 寻路不可达 + KingdomAttackedEvent 计数）=====

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Warning && type != LogType.Error && type != LogType.Exception) return;
        if (condition != null && condition.Contains("寻路不可达") && _pathFails.Count < 200)
            _pathFails.Add(condition);
    }

    private static int _atkCountA;   // 仅 kidA（k4）的事件数——考跑局含随机立国 3 国（k1~k3 互打混入全局计数）
    private static int _diagKidA = -1;

    private static void OnAtk(KingdomAttackedEvent evt)
    {
        _atkCount++;
        if (evt.KingdomId == _diagKidA) _atkCountA++;
    }
}
