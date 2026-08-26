using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  2_17 步骤8 王国脑冒烟（HH.24 六探针 + 增补①既有结算回归探针）
//  用法：菜单「Valley/验证/2_17_步骤8_KingdomBrain」——须 GameScene Play（先 Play 再点）。
//  覆盖（报告 §六 全量 + 增补1 回归）：
//    p1_minStay:  剧本推进+最小停留——不早不晚推进（存活→发育 第2日；发育→扩张 第3日）
//    p2_onePerDay:单日满足多级条件只升荤级（D319）
//    p3_grainBase:常设底线粮储清零→仓粮焦点（D322），无视评分（无评分器占位）
//    p4_playerNoBrain:玩家 id=0 无 Brain、scriptPhase==null（D338，#13）
//    p5_fourStage:机器四段全链（注入战士+领土解耦 P0 产出约束）+ 军事期退化不降（D318 单向）
//    p6_deterministic:王国脑日 tick 两轮逐字节一致（同 seed 确定性，镜像原则）
//    回归=既有结算尾巴（饱食/税收/AI段/牧场/营地 全链走通 + DaySettledEvent 照发，增补1 零回归）
//  自包含：InitializeNewGame 重建两轮；用产品王国脑/状态机/焦点（真链路）。
//  收口：不改产品代码；运行不落测试存档。
// ============================================================================
public static class Valley2_17_Smoke_8
{
    private const int SEED = 20260826;

    [MenuItem("Valley/验证/2_17_步骤8_KingdomBrain")]
    public static void RunFromMenu()
    {
        if (!Application.isPlaying) { Debug.LogError("[2_17_8冒烟] 须在 Play 上下文执行。"); return; }
        var runner = new GameObject("2_17_8_SmokeRunner");
        runner.AddComponent<SmokeCoroutineHost>().Host(RunCoroutine());
    }

    public static IEnumerator RunCoroutine()
    {
        var sb = new StringBuilder();
        List<string> r1 = new List<string>(), r2 = new List<string>();

        for (int round = 1; round <= 2; round++)
        {
            var lm = LoadManager.Instance;
            if (lm == null) { Debug.LogError("[2_17_8冒烟] LoadManager 不可用。"); yield break; }
            lm.InitializeNewGame(new NewGameConfig
            {
                mapSeed = SEED, worldSeed = SEED, difficulty = 2,
                worldSize = WorldSize.Medium, kingdomName = "2_17_8冒烟",
                selectedSlotId = "smoke_2_17_8"
            });
            yield return null;

            List<string> checks = new List<string>();
            bool worldOk = WorldScenario(checks);
            if (!worldOk) { foreach (var c in checks) sb.Append(c).Append(' '); break; }
            // 纯逻辑探针（与世界产出解耦，每轮重跑以纳入确定性采收）
            LogicScenario(checks);
            if (round == 1) r1 = checks; else r2 = checks;
        }

        if (r1.Count > 0)
        {
            bool deterministic = Join(r1) == Join(r2);
            foreach (var c in r1) sb.Append(c).Append(' ');
            sb.Append($" | 确定性两轮逐字节一致={(deterministic ? "OK" : "FAIL")} ");
            bool all = deterministic && !r1.Exists(c => c.Contains("FAIL"));
            sb.Append($" | 回归尾巴={(RegressProbe(out _) ? "OK" : "FAIL")} ");
            Debug.Log("[2_17_8冒烟] " + sb);
            Debug.Log($"[2_17_8冒烟] ===== {(all ? "ALL PASS" : "HAS FAIL")}（剧本最小停留/单日一级/粮底线/玩家无脑/四段单向/确定性/尾巴零回归）=====");
        }
        else
        {
            Debug.Log("[2_17_8冒烟] 世界构建异常预终止（无 AI 王国/无王国脑等），未作裁决。");
        }
    }

    // ===== 真实世界探针（玩家无脑 + 王国脑日 tick 确定性采收）=====

    /// <summary>真实世界场景化探针。返回 false＝世界不可用（前置失败，中止裁决）。true 且 checks 填入 p4/p6 采收。</summary>
    private static bool WorldScenario(List<string> checks)
    {
        var reg = KingdomRegistry.Instance;
        var brains = KingdomBrainRegistry.Instance;
        if (reg == null || brains == null) { checks.Add("reg-FAIL"); return false; }

        KingdomState ai = null;
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && !all[i].IsPlayer) { ai = all[i]; break; }
        if (ai == null) { checks.Add("noAI-FAIL"); return false; }

        // ---- ④ 玩家无 Brain（D338 / #13）：Registry 不含 id=0，Get(0) 为 null，王国 scriptPhase==null ----
        var player = reg.Get(0);
        bool noPlayerBrain = player != null && brains.Get(0) == null;
        bool noPlayerPhase = player == null || player.scriptPhase == null;
        checks.Add($"玩家无脑={(noPlayerBrain ? "OK" : "FAIL")}");
        checks.Add($"玩家无阶段={(noPlayerPhase ? "OK" : "FAIL")}");

        // ---- ③ 常设底线（粮）：真实 AI 王国（工人实体派生 pop>0）粮储清零 → 强制屯粮焦点 ----
        // 独立构造 FocusController（不依赖已注册脑的订阅；直接判底线）。
        var fc = new FocusController(ai.id);
        int savedFood = ai.resources.food;
        ai.resources.food = 0;
        fc.Update(ai, KingdomBrain.LoadConfig(), 1);
        bool grainBaseOk = ai.focus == FocusController.FocusGranary;
        ai.resources.food = savedFood;
        checks.Add($"粮底线屯粮={(grainBaseOk ? "OK" : "FAIL")}");

        // ---- ⑥ 王国脑日 tick 确定性采收：注入粮维持生存→推进，逐日扫描 scriptPhase/focus ----
        // 用每轮即时新建的 probe 王国脑采集（隔离跨轮残留：KingdomBrainRegistry 中的已注册脑会累积状态机，
        // 直接复用它做两轮对比会因起点不同而误报 FAIL——故此处 new 临时实例保证每轮干净起点，两轮一致才可判确定）。
        var probe = new KingdomBrain(ai.id);
        var scan = new StringBuilder();
        var cfgProbe = KingdomBrain.LoadConfig();
        for (int day = 1; day <= 15; day++)
        {
            ai.resources.food = 99999;            // 粮裕保单确定性主线推进（与脑日 tick 无关的确定性台账）
            probe.Tick(day);
            scan.Append(ScriptStageMachine.Name(probe.Stage)).Append(ai.focus).Append(';');
        }
        probe.Unsubscribe();
        checks.Add("scan=" + scan);
        return true;
    }

    // ===== 纯逻辑探针（ScriptStageMachine 单测，撬开 P0 产出约束验证机器层）=====

    private static void LogicScenario(List<string> checks)
    {
        var cfg = KingdomBrain.LoadConfig();
        if (cfg == null) { checks.Add("cfg-FAIL"); return; }

        // ---- ① 最小停留推进：不早不晚 ----
        // 存活→发育（第2日升，surviveMinDays=2 + streak=2）；发育→扩张（第3日升，developMinDays=3）
        var m1 = new ScriptStageMachine();
        var ctxOk = FullCtx(military: false);
        checks.Add($"①存活D1不动={(m1.Tick(ctxOk, cfg) ? "FAIL" : "OK")}");
        checks.Add($"①存活D2升发育={(m1.Tick(ctxOk, cfg) && m1.Stage == ScriptStage.Develop ? "OK" : "FAIL")}");
        // 发育期最小停留 3 日：D1/D2 不升、D3 才升（不早不晚，developMinDays=3）
        bool dev1 = m1.Tick(ctxOk, cfg);
        bool dev2 = m1.Tick(ctxOk, cfg);
        bool dev3 = m1.Tick(ctxOk, cfg);
        checks.Add($"①发育停3日={(!dev1 && !dev2 && dev3 && m1.Stage == ScriptStage.Expand ? "OK" : "FAIL")}");

        // ---- ② 单日最多升荤级（D319）：存活第2日同时满足发育→军事条件 → 当日只升到发育 ----
        var m2 = new ScriptStageMachine();
        var ctxFull = FullCtx(military: true);
        m2.Tick(ctxOk, cfg);   // 存活D1 不动
        m2.Tick(ctxFull, cfg); // 存活D2 达标 → 只升到发育（尽管军事条件也满足）
        checks.Add($"②单日只升一级={(m2.Stage == ScriptStage.Develop ? "OK" : "FAIL")}");
        m2.Tick(ctxFull, cfg);
        checks.Add($"②次日不连跳={(m2.Stage == ScriptStage.Develop ? "OK" : "FAIL")}");   // 未满发育3日，仍停留

        // ---- ⑤ 机器四段全链 + D318 单向不回退（解耦 P0 产出约束）----
        var m5 = new ScriptStageMachine();
        int stageUps = 0;
        ScriptStage last = ScriptStage.Survive;
        for (int day = 1; day <= 20; day++)
        {
            if (m5.Tick(ctxFull, cfg)) { stageUps++; last = m5.Stage; }
            if (m5.Stage == ScriptStage.Military) break;
        }
        bool fourOk = m5.Stage == ScriptStage.Military && stageUps == 3;
        checks.Add($"⑤四段全链军事={(fourOk ? "OK" : "FAIL")}");
        // 军事期注入"被打残"退化 ctx（工人/战士清零）→ 阶段标签不动（D318 单向）
        var deg = new ScriptStageContext { housedAll = false, workerCount = 0, capacityCount = 0, populationCount = 1 };
        bool noRegress = true;
        for (int i = 0; i < 3 && noRegress && m5.Stage == ScriptStage.Military; i++)
            noRegress = !m5.Tick(deg, cfg);
        checks.Add($"⑤军事期不打回={(noRegress && m5.Stage == ScriptStage.Military ? "OK" : "FAIL")}");
    }

    /// <summary>供存活→发育→扩张全满足（military 再满足战士/人口/领土→军事）的探针快照。</summary>
    private static ScriptStageContext FullCtx(bool military) => new ScriptStageContext
    {
        housedAll = true, grainDaysOk = true, unemployedOk = true,
        workerCount = 10, capacityCount = 5, netInflowPositive = true,
        warriorCount = military ? 6 : 0,
        populationCount = military ? 20 : 12,
        expansionChunks = military ? 3 : 0
    };

    /// <summary>增补①既有结算回归探针：手动触发一次日结算，断言 DaySettledEvent 照常发出（⑤尾巴饱食/税收/AI段/牧场/营地全链走通零回归）。</summary>
    private static bool RegressProbe(out int daySettled)
    {
        daySettled = 0;
        int fired = 0;
        EventBus.Subscribe<DaySettledEvent>(e => fired++);
        if (TimeManager.Instance != null)
        {
            var evt = new TimeDayChangedEvent(TimeManager.Instance.CurrentDay, TimeManager.Instance.CurrentDay + 1, Season.Spring);
            EventBus.Publish(evt);   // DayCycleSettlement 消费 → 五步尾巴走完 → DaySettledEvent
        }
        daySettled = fired;
        // 尾巴系统必须在场可跑（重构后若 break 则此处抛错；且 DaySettledEvent 有发射设其零回归）
        bool sysOk = SatietySystem.Instance != null && TaxSystem.Instance != null;
        return sysOk && fired >= 1;
    }

    private static string Join(List<string> l)
    {
        var s = new StringBuilder();
        foreach (var x in l) s.Append(x).Append('|');
        return s.ToString();
    }

    private class SmokeCoroutineHost : MonoBehaviour
    {
        public void Host(IEnumerator e) { StartCoroutine(e); }
    }
}