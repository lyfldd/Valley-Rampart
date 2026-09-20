#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.285「军事建造需求口径治本批」行为探针（Editor-only · 独立容器 · D732 重做版）
//  ---------------------------------------------------------------------------
//  唯一改动面=UtilityActionConfig.asset 两字段（id20 need 17→22／id25 need 17→21）。
//  本容器只读取证（铁律：禁自造 UtilityActionDef——D732 §3.1①：两先例 Smoke_2_22P0.cs:495 /
//  HH140_Probe.cs:255 的 need 是硬编码的，验的不是 asset 字段 ⇒ 本容器一律从
//  UtilityActionConfig.LoadConfig() 取资产真 def 再跑 UtilityScorer.NeedScore/Feasible）。
//  三组探针（任务书 §3.1）：
//    ⓐ ㉔ 评分前后对照：改前基线=恒 0.500（ExclusiveGap 占位·D732 收编）；改后=MachineDemand
//      态势驱动——军事期注入（scriptPhase=Military·复原）＋SituationHub/PostureHub 造两档：
//      None(无威胁)档 ⇒ 0 ／ Alert 档 ⇒ >0（wantM=max(1,needA=1)·MachineCount=0 ⇒ 0.8）。
//    ⓑ ㉑ 进池：军事缺口快照（GeneralCount=0<limit2）⇒ MilitaryBuildingGap 缺口 >0；
//      矮人局（raceId=2=LeyForge）⇒ Feasible true（族门禁过）／非矮人 ⇒ false（:574 拦截）。
//    ⓒ 互斥链（Feasible 直调·HH.282 同法）：无厂 ⇒ ㉔ true／㉕ false（:616 厂前置）；
//      建厂后（FindAIBuildSpot+TryBuild·先例 Smoke_2_22P0 P9b）⇒ ㉔ false（:577 cap=1）／㉕ true。
//  真暂停：读数注入阶段 Time.timeScale=0（deltaTime=0 ⇒ 日推进冻结 ⇒ 每日 tick 不覆写注入态）；
//  建厂段临时恢复倍速（施工需推进）→ WaitDays(4) 等 Active（先例同款）。
//  红线：正门 EnterTestRun＋真暂停＋ExitTestRun＋退 Play（L-32）；AI.Core 零触；不跑长局（D732 §3.1④）。
// ============================================================================
public static class Valley_HH285_Probe
{
    const int Seed = 21107;               // HH.282 验收同 seed（结构已侦察；本批只做注入读数·不依赖地图结构）
    const string Slot = "hh285_probe1";   // 新槽（禁覆盖 p1_run9/p1_run8/HH.214 槽/hh282_smoke）
    const int BuildWaitDays = 4;          // TryBuild=施工启动非竣工（L-20·先例 P9b 同款）

    static readonly StringBuilder _log = new StringBuilder();
    static string _logPath;
    static int _pass, _fail;
    static MethodInfo _miSpot, _miFeasible;

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH285] 须先 GameScene 进 Play。"); return; }
        _log.Clear(); _pass = 0; _fail = 0;
        _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh285_probe.log"));
        Log("探针启动（正门 EnterTestRun · seed=" + Seed + " · 槽=" + Slot + " · 真暂停注入读数）", true);
        var host = new GameObject("HH285_ProbeHost").AddComponent<ProbeHost>();
        host.Host(Coroutine(host));
    }

    /// <summary>MCP 桥接：返回协程让工具等待执行完毕（fire-and-forget 会被退 Play 中断）。</summary>
    public static IEnumerator RunCoroutine()
    {
        _log.Clear(); _pass = 0; _fail = 0;
        _logPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh285_probe.log"));
        Log("探针启动（正门 EnterTestRun · seed=" + Seed + " · 槽=" + Slot + " · 真暂停注入读数）", true);
        return Coroutine(null);
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    static void Log(string msg, bool ok)
    {
        _log.AppendLine((ok ? "[PASS] " : "[FAIL] ") + msg);
        if (ok) _pass++; else _fail++;
        Debug.Log("[HH285] " + (ok ? "✓ " : "✗ ") + msg);
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

    /// <summary>纯读数输出（不计分：静态直读/遍历证据用）。</summary>
    static void Info(string msg)
    {
        _log.AppendLine("[INFO] " + msg);
        Debug.Log("[HH285] · " + msg);
        try { if (_logPath != null) System.IO.File.WriteAllText(_logPath, _log.ToString()); } catch { }
    }

    static IEnumerator Coroutine(ProbeHost host)
    {
        var cfg = new NewGameConfig
        {
            worldSeed = Seed, mapSeed = Seed, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = Slot, kingdomName = "HH285探针"
        };
        yield return TestHarnessApi.EnterTestRun(cfg, 15f);

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Log("等世界就绪超时", false); Finish(host); yield break; }
        }
        yield return new WaitForSeconds(1.5f);   // 首日快照就绪

        var reg = KingdomRegistry.Instance;
        var k1 = reg.Get(1);
        var acfg = UtilityActionConfig.LoadConfig();
        if (k1 == null || acfg == null) { Log("前置缺失 k1/acfg", false); Finish(host); yield break; }

        // ===== 静态直读（验收线2）：真 def need 实测 + 27 条全遍历 =====
        var defBsw = acfg.Find(UtilityAction.BuildSiegeWorkshop);
        var defLey = acfg.Find(UtilityAction.BuildLeyForge);
        var defPm  = acfg.Find(UtilityAction.ProduceMachine);
        if (!defBsw.HasValue || !defLey.HasValue || !defPm.HasValue)
        { Log("真 def 缺失（LoadConfig/Find 失败）", false); Finish(host); yield break; }
        Info($"静态直读：Find(BuildSiegeWorkshop).need={(int)defBsw.Value.need}（期望 21=MachineDemand）");
        Info($"静态直读：Find(BuildLeyForge).need={(int)defLey.Value.need}（期望 22=MilitaryBuildingGap）");
        Log("静态直读 ㉔ need==21：" + ((int)defBsw.Value.need == 21) + "｜㉑ need==22：" + ((int)defLey.Value.need == 22),
            (int)defBsw.Value.need == 21 && (int)defLey.Value.need == 22);
        Info("27 条全遍历（id|name|need枚举=int|needA|minStage|axis|axisWeight|buildingId|buildTargetCap）——其余 25 条逐条未变以 git diff（恰 2 行）＋本表对照任务书 §2.1 双证：");
        foreach (var a in acfg.actions)
            Info($"  id={(int)a.id,2} {a.name} need={a.need}={(int)a.need} needA={a.needA} minStage={a.minStage} axis={a.axis} axisWeight={a.axisWeight} bld={a.buildingId} cap={a.buildTargetCap}");
        Info($"对照真源：id26(㉕) need={(int)defPm.Value.need}（应为 21=MachineDemand·本批未动）");

        // ===== 真暂停：冻结日推进（deltaTime=0 ⇒ Update/AdvanceTime 停 ⇒ 每日 tick 不覆写注入态）=====
        float savedScale = Time.timeScale;
        Time.timeScale = 0f;
        Info($"真暂停 ON：timeScale {savedScale}→0（日推进冻结·注入态不被每日 tick 覆写）");

        // ===== ⓐ ㉔ 评分前后对照（真 def·注入复原法·先例 P9a/P6d 同款）=====
        var savedPhase1 = k1.scriptPhase;
        var savedSit1 = SituationHub.TryGet(1, out var origSit1) ? origSit1 : null;
        var savedPosture1 = PostureHub.Get(1);
        k1.scriptPhase = ScriptStage.Military;
        SituationHub.Put(1, new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), Day = 800 });
        PostureHub.Put(1, MilitaryPosture.None);
        float bswNone = UtilityScorer.NeedScore(k1, defBsw.Value);
        PostureHub.Put(1, MilitaryPosture.Alert);
        float bswAlert = UtilityScorer.NeedScore(k1, defBsw.Value);
        // 复原
        PostureHub.Put(1, savedPosture1);
        if (savedSit1 != null) SituationHub.Put(1, savedSit1);
        k1.scriptPhase = savedPhase1;
        Info($"ⓐ 前后对照：改前基线（ExclusiveGap 占位·D732 收编）=恒 0.500；改后 None 档={bswNone:F3}（期望 0）／Alert 档={bswAlert:F3}（期望 >0·理论 0.8=1/(1+0)×0.8）");
        Log($"ⓐ ㉔ 态势驱动（真 def·军事期注入）：None档={bswNone:F3}(0)／Alert档={bswAlert:F3}(>0)·改前恒0.500→改后态势驱动",
            bswNone <= 0.0001f && bswAlert > 0.0001f);

        // ===== ⓑ ㉑ 进池（真 def·MilitaryBuildingGap 缺口驱动 + 族门禁双向）=====
        int dwarfId = -1, nonDwarfId = -1;
        for (int i = 1; i <= 4; i++)
        {
            var kk = reg.Get(i); if (kk == null) continue;
            if (KingdomRace.GetKingdomRace(i) == RaceIds.Dwarf) { if (dwarfId < 0) dwarfId = i; }
            else if (nonDwarfId < 0) nonDwarfId = i;
        }
        Info($"ⓑ 族探测：dwarfId={dwarfId} nonDwarfId={nonDwarfId}（k1~k4 实读 KingdomRace）");
        bool bOk;
        string bWhy;
        if (dwarfId < 0)
        {
            // L-30 判据可达性：无矮人局 ⇒ 快照缺口读数仍可验（need>0 与族无关），族门禁双向缺正例 ⇒ 列报不判 PASS
            var kd = reg.Get(1);
            var savedPhaseB = kd.scriptPhase;
            var savedSitB = SituationHub.TryGet(1, out var origSitB) ? origSitB : null;
            var savedPostB = PostureHub.Get(1);
            SituationHub.Put(1, new SituationSnapshot { KingdomId = 1, Threats = new List<ThreatEntry>(), GeneralCount = 0, FormationCount = 0, Day = 801 });
            float leyGap = UtilityScorer.NeedScore(kd, defLey.Value);
            if (savedSitB != null) SituationHub.Put(1, savedSitB);
            kd.scriptPhase = savedPhaseB;
            PostureHub.Put(1, savedPostB);
            bOk = leyGap > 0.0001f;
            bWhy = $"无矮人局（列报）：缺口读数 leyGap={leyGap:F3}(>0)✓ 但族门禁正例缺（Feasible 双向未验）";
            Log("ⓑ " + bWhy, false);
            Log("ⓑ 缺口>0（真 def·GeneralCount=0 注入）：" + leyGap.ToString("F3"), bOk);
        }
        else
        {
            var kd = reg.Get(dwarfId);
            var savedPhaseB = kd.scriptPhase;
            var savedSitB = SituationHub.TryGet(dwarfId, out var origSitB) ? origSitB : null;
            var savedPostB = PostureHub.Get(dwarfId);
            kd.resources = kd.resources.Add(ResourceType.Gold, 3000); kd.resources = kd.resources.Add(ResourceType.Stone, 800); kd.resources = kd.resources.Add(ResourceType.Wood, 800);   // 先例 P9 同款（冒烟槽不入盘）
            SituationHub.Put(dwarfId, new SituationSnapshot { KingdomId = dwarfId, Threats = new List<ThreatEntry>(), GeneralCount = 0, FormationCount = 0, Day = 801 });
            float leyGap = UtilityScorer.NeedScore(kd, defLey.Value);
            bool leyFeas = InvokeFeasible(kd, defLey.Value);
            // 非矮人族门禁负例（有非矮人局时）
            bool gateBlocked = true;
            if (nonDwarfId > 0)
            {
                var kn = reg.Get(nonDwarfId);
                kn.resources = kn.resources.Add(ResourceType.Gold, 3000); kn.resources = kn.resources.Add(ResourceType.Stone, 800); kn.resources = kn.resources.Add(ResourceType.Wood, 800);
                gateBlocked = !InvokeFeasible(kn, defLey.Value);   // :574 raceId 拦截
            }
            // 复原
            if (savedSitB != null) SituationHub.Put(dwarfId, savedSitB);
            kd.scriptPhase = savedPhaseB;
            PostureHub.Put(dwarfId, savedPostB);
            bOk = leyGap > 0.0001f && leyFeas && gateBlocked;
            bWhy = $"k{dwarfId}(矮人) 缺口={leyGap:F3}(>0)·Feasible={leyFeas}（族门禁过·资源已注入）";
            if (nonDwarfId > 0) bWhy += $"·非矮人 k{nonDwarfId} Feasible 拦截={gateBlocked}（:574）";
            Log("ⓑ ㉑ 进池（真 def·MilitaryBuildingGap）：矮人局缺口>0＋Feasible true" + (nonDwarfId > 0 ? "＋非矮人拦截" : ""), bOk);
        }

        // ===== ⓒ-1 互斥链·无厂（Feasible 直调·k1）=====
        k1.resources = k1.resources.Add(ResourceType.Gold, 3000); k1.resources = k1.resources.Add(ResourceType.Stone, 800); k1.resources = k1.resources.Add(ResourceType.Wood, 800);
        var bdefW = BuildingFactory.FindDefById(BuildingIds.SiegeWorkshop);
        bool c1FeasBsw = bdefW != null && InvokeFeasible(k1, defBsw.Value);
        bool c1FeasPm = InvokeFeasible(k1, defPm.Value);
        Log($"ⓒ-1 无厂：Feasible(㉔)={c1FeasBsw}（期望 true·资源已注入）／Feasible(㉕)={c1FeasPm}（期望 false·:616 厂前置）",
            c1FeasBsw && !c1FeasPm);

        // ===== 建厂（恢复倍速→施工→Active）=====
        Time.timeScale = savedScale;   // 恢复（施工需推进）
        Info($"真暂停 OFF：timeScale→{savedScale}（建厂段需时间推进）");
        if (bdefW == null) { Log("bdefW 缺失（BuildingFactory.FindDefById）", false); Finish(host); yield break; }
        if (_miSpot == null) _miSpot = typeof(KingdomBrain).GetMethod("FindAIBuildSpot", BindingFlags.NonPublic | BindingFlags.Static);
        var sW = _miSpot != null ? _miSpot.Invoke(null, new object[] { 1, bdefW, 25 }) as GridCoord? : null;
        var bc = BuildController.Instance;
        bool built = sW.HasValue && bc != null && bc.TryBuild(bdefW, sW.Value, GateOrientation.Horizontal, 1);
        Log($"建厂提交：spot={sW.HasValue} built={built}（FindAIBuildSpot+TryBuild·先例 P9b 同法）", built);
        yield return WaitDays(BuildWaitDays);   // L-20：TryBuild=施工启动非竣工，等 Active
        var regB = BuildingRegistry.Instance;
        bool workshopActive = false;
        if (regB != null && regB.All != null)
            foreach (var b in regB.All)
                if (b != null && b.def != null && b.IsActive && b.kingdomId == 1 && b.def.id == BuildingIds.SiegeWorkshop) { workshopActive = true; break; }
        Log($"厂 Active={workshopActive}（等 {BuildWaitDays} 日）", workshopActive);

        // ===== ⓒ-2 互斥链·建厂后 =====
        bool c2FeasBsw = InvokeFeasible(k1, defBsw.Value);
        bool c2FeasPm = InvokeFeasible(k1, defPm.Value);
        Log($"ⓒ-2 建厂后：Feasible(㉔)={c2FeasBsw}（期望 false·:577 cap=1）／Feasible(㉕)={c2FeasPm}（期望 true·厂前置+prefab 已备 HH.282）",
            !c2FeasBsw && c2FeasPm);

        Finish(host);
    }

    static IEnumerator WaitDays(int days)
    {
        for (int i = 0; i < days; i++)
        {
            int start = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            while (TimeManager.Instance != null && TimeManager.Instance.CurrentDay == start)
                yield return null;
            yield return new WaitForSeconds(0.5f);
        }
    }

    static bool InvokeFeasible(KingdomState k, UtilityActionDef d)
    {
        if (_miFeasible == null)
            _miFeasible = typeof(UtilityScorer).GetMethod("Feasible", BindingFlags.NonPublic | BindingFlags.Static);
        if (_miFeasible == null) { Debug.LogWarning("[HH285] 反射不可达：UtilityScorer.Feasible"); return false; }
        try { return (bool)_miFeasible.Invoke(null, new object[] { k, d }); }
        catch (System.Exception ex) { Debug.LogWarning("[HH285] Feasible 反射异常: " + ex.Message); return false; }
    }

    static void Finish(ProbeHost host)
    {
        Log($"收尾：PASS={_pass} FAIL={_fail} · ExitTestRun + QuitSmoke + 退 Play（L-32）· 不跑长局（D732 §3.1④）", _fail == 0);
        TestHarnessApi.ExitTestRun();   // 内部全量恢复（timeScale=1 等）
        try { SmokeApi.QuitSmoke(); } catch { }
        EditorApplication.ExitPlaymode();
        if (host != null) Object.Destroy(host.gameObject);
    }
}
#endif
