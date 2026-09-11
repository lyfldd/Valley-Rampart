using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

// ============================================================================
//  Smoke_2_23RB 资源 P0 批B 探针容器（派工活权重 R-B1/R-B2，Editor-only）
//  test-harness-first 铁律①⑤：正门 TestHarnessApi.EnterTestRun + ExitTestRun 收尾。
//  L-17：一律走正门（禁裸跑 SmokeApi.EnterGame）。
//  L-21：计数面 0→1 真实翻转（有效优先级「偏向生效数 0→1」）+ 失败路径终止性
//        （无快照/无经济块/无映射 → 不异常、返回死表值）。
//  L-20：本批**不涉建造**（无 TryBuild/竣工两态）⇒ 不适用，如实注记。
//  确定性：run1/run2 双跑日志 P 行序列逐字节比对（时间戳/耗时行不入比对）。
//
//  探针清单（对齐清单 §二 R-B1/R-B2 验收标准 + HH.172 §五 补全口径）：
//    P1  正门进局 + 批A 经济诊断块真值在场（集成前提）
//    P2  出厂等价负探针（无缺口/无快照 → 有效优先级 == 死表值）
//    P3  E-B5 正探针：缺石世界 → 采石（Gather Stone）任务有效优先级↑
//    P4  资源隔离负探针：缺石时木/粮采集任务有效优先级不变
//    P5  S 级保命负探针：Repair(S) 不受缺石影响（== 死表 S）
//    P6  玩家源负探针：kingdomId=0 源 → 权重 1.0（玩家侧零改动）
//    P7  跨档保护负探针：maxWeight 极值 → 非 S 仍 < S（防配置越界架空死表）
//    P8  clamp 上界：bias/缺口满 → 权重 == maxWeight（SO 数据驱动生效）
//    P9  排序确定性：打乱输入序 → 排序结果序列一致（全序）
//    P10 失败路径终止性：缺 economy / 无映射 → 返回死表值不异常
//    P11 L-21 计数面翻转：偏向生效任务数 0（出厂）→ N（缺石）
// ============================================================================
public static class Smoke_2_23RB
{
    private const int SEED = 21140;      // 与 Smoke_2_22P0 同 seed（同局地形，便于横向对照）
    private const string SLOT = "probe_2_23rb";
    private static int _pass, _fail;
    private static readonly System.Text.StringBuilder _log = new System.Text.StringBuilder();
    public static string RunTag = "run1";

    [MenuItem("Valley/验证/Smoke_2_23RB_资源批B探针_run1")]
    public static void Run1() { RunTag = "run1"; Run(); }
    [MenuItem("Valley/验证/Smoke_2_23RB_资源批B探针_run2")]
    public static void Run2() { RunTag = "run2"; Run(); }

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[2_23RB] 须先 GameScene 进 Play。"); return; }
        _pass = 0; _fail = 0; _log.Length = 0;
        var host = new GameObject("Smoke2_23RB_Host").AddComponent<ProbeHost>();
        Application.logMessageReceived += host.Catch;
        host.Host(Coroutine());
    }

    private class ProbeHost : MonoBehaviour
    {
        public void Host(IEnumerator r) => StartCoroutine(r);
        public void Catch(string cond, string stack, LogType t)
        {
            if (t == LogType.Exception) Log("EXCEPTION: " + cond + "\n" + stack, false);
        }
    }

    private static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.LogWarning("[2_23RB] " + (ok ? "✓ " : "✗ ") + msg);
    }

    // ---- 反射句柄（读私有实现，不改产品代码）----
    private static MethodInfo _effMi;        // EffectivePriority(KingdomTask) -> float
    private static MethodInfo _cmpMi;        // CompareByEffectivePriority(a,b) -> int
    private static MethodInfo _prioMi;       // GetPriority(KingdomTaskType) -> TaskPriority
    private static FieldInfo _biasFi;        // TaskScheduler._biasConfig

    private static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            mapSeed = SEED, worldSeed = SEED, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = SLOT, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null || KingdomRegistry.Instance.Count < 4)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(); yield break; }
        }
        yield return new WaitForSeconds(1f);
        Log("P1a 正门进局 seed=" + SEED + " tag=" + RunTag, true);

        var sched = TaskScheduler.Instance;
        var k1 = KingdomRegistry.Instance.Get(1);
        if (sched == null || k1 == null) { Log("P1b TaskScheduler/k1 缺失", false); Finish(); yield break; }

        var tSched = typeof(TaskScheduler);
        _effMi = tSched.GetMethod("EffectivePriority", BindingFlags.NonPublic | BindingFlags.Instance);
        _cmpMi = tSched.GetMethod("CompareByEffectivePriority", BindingFlags.NonPublic | BindingFlags.Instance);
        _prioMi = tSched.GetMethod("GetPriority", BindingFlags.NonPublic | BindingFlags.Instance);
        _biasFi = tSched.GetField("_biasConfig", BindingFlags.NonPublic | BindingFlags.Instance);
        var fLog = _effMi != null;   // 反射在场
        Log("P1b 排序键三段式实现在场（EffectivePriority/Compare/GetPriority/biasConfig）= "
            + (_effMi != null && _cmpMi != null && _prioMi != null && _biasFi != null), fLog);
        var biasCfg = _biasFi != null ? _biasFi.GetValue(sched) as ResourceBiasConfig : null;
        Log("P1c ResourceBiasConfig 加载在场（出厂 1.0 占位）= " + (biasCfg != null),
            biasCfg != null && Mathf.Approximately(biasCfg.BiasOf(EcoResource.Stone), 1f));

        // AI 建筑就绪（立国即建；等 2 日更稳）
        yield return WaitDays(2);

        // P1d 批A 经济诊断块真值在场（集成前提；HH.172 附条① 依赖面）
        bool econOk = SituationHub.TryGet(1, out var realSnap) && realSnap != null && realSnap.Economy != null;
        Log("P1d 批A EconomyBlock 真值在场（SituationHub k1）= " + econOk, econOk);

        var srcs = FindBuildings(1, 6);
        if (srcs.Count == 0) { Log("P1e k1 无 Active 建筑（无法构造任务源）", false); Finish(); yield break; }
        Log("P1e k1 Active 建筑数（探针源）= " + srcs.Count, srcs.Count > 0);

        // ================= 同步断言区（注入快照 → 即刻读，不被日 tick 覆盖） =================
        float baseGather = BasePriority(sched, KingdomTaskType.Gather);
        var stoneSrc = srcs[0];

        // ---- P2 出厂等价负探针：先清快照（无缺口）→ 有效优先级 == 死表值 ----
        SituationHub.Remove(1);
        float stoneNone = Eff(sched, MakeGather(stoneSrc, ResourceType.Stone));
        float woodNone = Eff(sched, MakeGather(stoneSrc, ResourceType.Wood));
        Log("P2 出厂等价负探针（无快照）：采石 eff=" + stoneNone + " == 死表 " + baseGather
            + "；采木 eff=" + woodNone, Mathf.Approximately(stoneNone, baseGather) && Mathf.Approximately(woodNone, baseGather));

        // ---- P10 失败路径终止性：economy==null（空快照）→ 仍返回死表值，不异常 ----
        bool p10ok = false;
        try
        {
            SituationHub.Put(1, new SituationSnapshot { KingdomId = 1, Day = 0, Economy = null });
            float v = Eff(sched, MakeGather(stoneSrc, ResourceType.Stone));
            p10ok = Mathf.Approximately(v, baseGather);
        }
        catch (System.Exception e) { Log("P10 异常: " + e.Message, false); }
        Log("P10 失败路径终止性（economy=null → 死表值、无异常）= " + p10ok, p10ok);

        // ---- P3 E-B5 正探针：注入缺石快照 → 采石任务有效优先级↑ ----
        PutShortage(1, EcoResource.Stone, outAmt: 100);
        float stoneShort = Eff(sched, MakeGather(stoneSrc, ResourceType.Stone));
        Log("P3 E-B5 缺石世界采石 eff=" + stoneShort + " > 无缺口 " + stoneNone
            + "（up=" + (stoneShort > stoneNone) + "）", stoneShort > stoneNone);

        // ---- P4 资源隔离负探针：缺石时木/粮采集 eff 不变 ----
        float woodShort = Eff(sched, MakeGather(stoneSrc, ResourceType.Wood));
        float foodShort = Eff(sched, MakeGather(stoneSrc, ResourceType.Food));
        bool p4 = Mathf.Approximately(woodShort, baseGather) && Mathf.Approximately(foodShort, baseGather);
        Log("P4 资源隔离负探针：缺石时采木 eff=" + woodShort + " 采粮 eff=" + foodShort
            + "（均 == 死表 " + baseGather + "）", p4);

        // ---- P5 S 级保命负探针：Repair(S) 不受缺石影响 ----
        var repair = new KingdomTask(KingdomTaskType.Repair, stoneSrc);
        float repairEff = Eff(sched, repair);
        float sVal = BasePriority(sched, KingdomTaskType.Repair);
        Log("P5 S 级保命负探针：缺石时 Repair eff=" + repairEff + " == 死表 S=" + sVal,
            Mathf.Approximately(repairEff, sVal) && Mathf.Approximately(sVal, 4f));

        // ---- P6 玩家源负探针：kingdomId=0（TreeGatherSource=玩家采集真实载体，非 Building 源）----
        var tg = new TreeGatherSource(new GridCoord(10, 10), new Vector2(1f, 1f), 2f, 5);
        var pTask = new KingdomTask(KingdomTaskType.Gather, tg);
        pTask.args = new GatherTaskArgs { resourceType = ResourceType.Stone, amount = 5, gatherSeconds = 2f };
        float pEff = Eff(sched, pTask);
        Log("P6 玩家源负探针：缺石时玩家采石（TreeGatherSource, kingdomId=0）eff=" + pEff
            + " == 死表 " + baseGather, Mathf.Approximately(pEff, baseGather));

        // ---- P8 clamp 上界：bias/缺口满 → 权重 == maxWeight ----
        float expectedMax = baseGather * biasCfg.maxWeight;   // 缺口率=1.0 时 weight 达上界
        Log("P8 clamp 上界：采石 eff=" + stoneShort + " == 死表×maxWeight(" + biasCfg.maxWeight + ")=" + expectedMax,
            Mathf.Approximately(stoneShort, expectedMax));

        // ---- P7 跨档保护负探针：maxWeight 极值 → 非 S 仍 < S ----
        float origMax = biasCfg.maxWeight;
        float capped = 0f;
        try
        {
            biasCfg.maxWeight = 100f;   // 临时越界配置（模拟错配）；缺口仍满
            capped = Eff(sched, MakeGather(stoneSrc, ResourceType.Stone));
        }
        finally { biasCfg.maxWeight = origMax; }
        float sCap = BasePriority(sched, KingdomTaskType.Repair);   // S=4
        Log("P7 跨档保护负探针：maxWeight=100 时非 S eff=" + capped + " < S=" + sCap
            + "（硬上界生效）", capped < sCap && capped > baseGather);

        // ---- P9 排序确定性：打乱输入序 → 结果序列一致 ----
        var tasks = new List<KingdomTask>();
        for (int i = 0; i < srcs.Count; i++)
        {
            var t = (i % 2 == 0)
                ? MakeGather(srcs[i], (i == 0) ? ResourceType.Stone : ResourceType.Wood)
                : new KingdomTask(KingdomTaskType.Production, srcs[i]);
            tasks.Add(t);
        }
        var cmp = (System.Comparison<KingdomTask>)System.Delegate.CreateDelegate(
            typeof(System.Comparison<KingdomTask>), sched, _cmpMi);
        var a = new List<KingdomTask>(tasks); a.Sort(cmp);
        var b = new List<KingdomTask>(tasks); b.Reverse(); b.Sort(cmp);
        bool p9 = a.Count == b.Count;
        for (int i = 0; p9 && i < a.Count; i++)
            p9 = a[i].type == b[i].type
                 && Mathf.Approximately(a[i].SourcePos.x, b[i].SourcePos.x)
                 && Mathf.Approximately(a[i].SourcePos.y, b[i].SourcePos.y);
        Log("P9 排序确定性：打乱输入序后序列一致（n=" + a.Count + "）= " + p9, p9);

        // ---- P11 L-21 计数面翻转：偏向生效任务数 0（出厂）→ N（缺石）----
        //  任务集含：采石×N + 采木×N + Repair(S) + GoldMine（SO 兜底映射）
        var p11 = new List<KingdomTask>();
        for (int i = 0; i < srcs.Count; i++)
        {
            p11.Add(MakeGather(srcs[i], ResourceType.Stone));
            p11.Add(MakeGather(srcs[i], ResourceType.Wood));
        }
        p11.Add(new KingdomTask(KingdomTaskType.Repair, srcs[0]));
        p11.Add(new KingdomTask(KingdomTaskType.GoldMine, srcs[0]));

        SituationHub.Remove(1);
        int onNone = CountBiased(sched, p11);
        PutShortage(1, EcoResource.Stone, outAmt: 100);
        int onShort = CountBiased(sched, p11);
        Log("P11 L-21 计数面翻转：偏向生效任务数 " + onNone + "（无缺口）→ " + onShort
            + "（缺石；应=采石数 " + srcs.Count + "，采木/Repair/GoldMine 不动）",
            onNone == 0 && onShort == srcs.Count);

        // 清理注入快照（交回真实日 tick 重建；零污染）
        SituationHub.Remove(1);

        Finish();
    }

    // ---- 辅助 ----

    private static float Eff(TaskScheduler s, KingdomTask t) => (float)_effMi.Invoke(s, new object[] { t });

    private static float BasePriority(TaskScheduler s, KingdomTaskType type)
        => (float)(TaskPriority)_prioMi.Invoke(s, new object[] { type });

    private static KingdomTask MakeGather(Building src, ResourceType res)
    {
        var t = new KingdomTask(KingdomTaskType.Gather, src);
        t.args = new GatherTaskArgs { resourceType = res, amount = 5, gatherSeconds = 2f };
        return t;
    }

    /// <summary>注入合成缺口快照（净流量 = −outAmt ⇒ 缺口率 = 1.0）。探针专用，不落盘。</summary>
    private static void PutShortage(int kingdomId, EcoResource r, int outAmt)
    {
        var flow = new ResourceFlow();
        switch (r)
        {
            case EcoResource.Gold: flow.goldOut = outAmt; break;
            case EcoResource.Stone: flow.stoneOut = outAmt; break;
            case EcoResource.Wood: flow.woodOut = outAmt; break;
            case EcoResource.Food: flow.foodOut = outAmt; break;
            default: flow.metalOut = outAmt; break;
        }
        var block = new EconomyBlock { Flow = flow };
        SituationHub.Put(kingdomId, new SituationSnapshot { KingdomId = kingdomId, Day = 0, Economy = block });
    }

    /// <summary>统计一组任务中「有效优先级 &gt; 死表值」的个数（L-21 计数面）。</summary>
    private static int CountBiased(TaskScheduler s, List<KingdomTask> tasks)
    {
        int n = 0;
        for (int i = 0; i < tasks.Count; i++)
            if (Eff(s, tasks[i]) > BasePriority(s, tasks[i].type) + 0.0001f) n++;
        return n;
    }

    private static List<Building> FindBuildings(int kingdomId, int max)
    {
        var list = new List<Building>();
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return list;
        for (int i = 0; i < reg.All.Count && list.Count < max; i++)
        {
            var b = reg.All[i];
            if (b != null && b.IsActive && b.kingdomId == kingdomId) list.Add(b);
        }
        return list;
    }

    private static IEnumerator WaitDays(int days)
    {
        for (int i = 0; i < days; i++)
        {
            int start = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            while (TimeManager.Instance != null && TimeManager.Instance.CurrentDay == start)
                yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }

    private static void Finish()
    {
        _log.AppendLine("===== Smoke_2_23RB 资源批B探针收工 =====");
        _log.AppendLine("PASS=" + _pass + " FAIL=" + _fail + " 时间=" + System.DateTime.Now.ToString("HH:mm:ss"));
        if (TimeManager.Instance != null) TimeManager.Instance.SetGameSpeed(0f);
        TestHarnessApi.ExitTestRun();
        try
        {
            var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Logs/P1");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "smoke_2_23rb_" + RunTag + ".log"), _log.ToString());
        }
        catch (System.Exception e) { Debug.LogError("[2_23RB] 日志写盘失败: " + e.Message); }
        Debug.LogWarning("[2_23RB] ★ 收工 PASS=" + _pass + " FAIL=" + _fail + "（tag=" + RunTag + "）");
        var host = Object.FindObjectOfType<ProbeHost>();
        if (host != null) Application.logMessageReceived -= host.Catch;
    }
}
