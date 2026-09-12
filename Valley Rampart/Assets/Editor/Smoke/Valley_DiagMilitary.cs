// ============================================================================
//  HH.190 / D651【T1 观测口补口】建军链（⑦⑯⑰）被选中面 · 只读诊断探针
//  ---------------------------------------------------------------------------
//  性质：**Editor-only 观测域增量**（`Assets/Editor/*`）。业务代码零改动：
//        · 只做 **反射只读** 取值（NeedScore 公开调用；Feasible/CountProductionOf/DecideTriage 反射）
//        · **不写任何业务状态**、不改判定/数值/分支、不订阅除 DaySettled 以外的事件
//  口径依据：HH.190 任务书 §二 T1 ＋ D651 §三-1（Editor-only 观测域 ＋ 反射只读；反射不可达⇒列报，禁破零改动）
//  日志 tag（已并入 P1 观测器镜像白名单）：[DiagMilitary] [DiagTriage] [DiagCapacity] [DiagBias]
//                                          [DiagChain] [TreasureVault]
// ============================================================================

using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DiagMilitary
{
    private const BindingFlags PrivStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private static bool _installed;
    private static MethodInfo _miFeasible, _miCountProd, _miDecideTriage;
    private static bool _warnedFeasible, _warnedTriage;

    // ===== HH.222（D666 已裁 / test-harness-first §八 机制 1+2 落地；教训 L-34）=====
    //  在线判据**单源**：探针每日 tick 更新 streak ＋ 状态行打 `verdict=`；跑局容器读快照做「命中即停」。
    //  纪律（§8.1 支撑日志层）：支撑量（stone/in/prod…）一旦异常 ⇒ **当日**打 `verdict=`／`ANOMALY=` 并当场判定，不等跑完。
    public enum JudgeKind { None = 0, Deadlock, StoneCold, TargetGateHit, NoIncome, ChannelAbsent }

    private class JudgeState
    {
        public int prevStone = -1;
        public int noGainDays;      // 石链僵死：stone 未增且石入库=0 的连续日数
        public int belowGateDays;   // 死滞：drive≤0 且 target<门 的连续日数
        public int noIncomeDays;    // 异常：六资源全零入库的连续日数
        public bool gateHit;        // 机制面已证：曾达 target≥门
        public int gateHitDay = -1;
    }

    private static readonly System.Collections.Generic.Dictionary<int, JudgeState> _judge
        = new System.Collections.Generic.Dictionary<int, JudgeState>();

    public const int JudgeDeadlockDays = 5;    // §8.4 示例：负探针 D5~D10 死滞定论
    public const int JudgeStoneColdDays = 10;  // §8.4 示例：stone 连续 ≥10 日不增且入库=0 ⇒ 石链僵死
    public const int JudgeNoIncomeDays = 15;   // 异常即停：六资源全零入库 ≥15 日
    public const int JudgeChannelMinDay = 3;   // 通道未落地（资源对等批判据③）：D≥3 仍 0 源

    /// <summary>清判据状态（跑局容器起跑时调用；防跨局/跨批污染）。</summary>
    public static void ResetJudges() => _judge.Clear();

    private static bool Has(JudgeKind[] arr, JudgeKind j)
    {
        if (arr == null) return false;
        for (int i = 0; i < arr.Length; i++) if (arr[i] == j) return true;
        return false;
    }

    /// <summary>容器早停用：该 AI 是否命中「enabled 内」任一判据。
    /// 优先级＝死滞 ＞ 石链僵死（止损）＞ 零入库 ＞ 通道未落地 ＞ 机制面已证（§8.2 机制 1+3）。</summary>
    public static bool TryJudge(int kId, JudgeKind[] enabled, int day, out JudgeKind kind, out string detail)
    {
        kind = JudgeKind.None; detail = "";
        JudgeState s;
        if (!_judge.TryGetValue(kId, out s)) return false;
        if (Has(enabled, JudgeKind.Deadlock) && s.belowGateDays >= JudgeDeadlockDays)
        { kind = JudgeKind.Deadlock; detail = "drive≤0 且 target<门 连续" + s.belowGateDays + "日"; return true; }
        if (Has(enabled, JudgeKind.StoneCold) && s.noGainDays >= JudgeStoneColdDays)
        { kind = JudgeKind.StoneCold; detail = "stone=" + s.prevStone + " 连续" + s.noGainDays + "日未增且石入库=0（ANOMALY:stoneChain）"; return true; }
        if (Has(enabled, JudgeKind.NoIncome) && s.noIncomeDays >= JudgeNoIncomeDays)
        { kind = JudgeKind.NoIncome; detail = "六资源全零入库连续" + s.noIncomeDays + "日（ANOMALY）"; return true; }
        if (Has(enabled, JudgeKind.ChannelAbsent) && day >= JudgeChannelMinDay && CountWorldResourceSources() <= 0)
        { kind = JudgeKind.ChannelAbsent; detail = "世界资源点任务源注册数=" + CountWorldResourceSources() + "（通道未落地）"; return true; }
        if (Has(enabled, JudgeKind.TargetGateHit) && s.gateHit)
        { kind = JudgeKind.TargetGateHit; detail = "target≥门 首达 @D" + s.gateHitDay; return true; }
        return false;
    }

    /// <summary>世界资源点任务源注册数（HH.221 资源对等批判据③；只读反射 `TaskScheduler._sources`；-1=不可达）。</summary>
    public static int CountWorldResourceSources()
    {
        try
        {
            var ts = TaskScheduler.Instance;
            if (ts == null) return -1;
            var fi = typeof(TaskScheduler).GetField("_sources", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) return -1;
            var col = fi.GetValue(ts) as System.Collections.IEnumerable;
            if (col == null) return -1;
            int n = 0;
            foreach (var o in col)
            {
                if (o == null) continue;
                string tn = o.GetType().Name;
                if (tn.Contains("Gather") || tn.Contains("Byproduct") || tn.Contains("Resource")) n++;
            }
            return n;
        }
        catch { return -1; }
    }

    /// <summary>诊断器是否在跑（与 P1 观测器 IsRunning 同构，供跑局容器断言）。</summary>
    public static bool IsRunning => _installed;

    [MenuItem("Valley/诊断/启动建军链诊断")]
    public static void Start()
    {
        if (_installed) { Debug.Log("[DiagMilitary] 已在诊断中（幂等守卫）。"); return; }
        EventBus.Subscribe<DaySettledEvent>(OnDaySettled);
        _installed = true;
        Debug.LogWarning("[DiagMilitary] 建军链三面诊断已启动（只读反射；业务代码零改动）。");
    }

    [MenuItem("Valley/诊断/停止建军链诊断")]
    public static void Stop()
    {
        if (!_installed) { Debug.Log("[DiagMilitary] 未在诊断中。"); return; }
        EventBus.Unsubscribe<DaySettledEvent>(OnDaySettled);
        _installed = false;
        Debug.LogWarning("[DiagMilitary] 建军链三面诊断已停止。");
    }

    [MenuItem("Valley/诊断/立即取一次建军链三面")]
    public static void DumpNow() => Dump("手动");

    /// <summary>HH.204/D658 A′：带 tag 的**同步**快照入口（供阶段注入夹具在本帧内取证用）。</summary>
    public static void DumpNow(string tag) => Dump(tag);

    private static void OnDaySettled(DaySettledEvent evt)
    {
        try { Dump("D" + (Application.isPlaying && TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1)); }
        catch (Exception ex) { Debug.LogError("[DiagMilitary] DaySettled 处理异常: " + ex); }
    }

    // ── 主入口：逐 AI 王国 + Vault 面 ────────────────────────────────────────
    private static void Dump(string tag)
    {
        if (!Application.isPlaying) return;
        var reg = KingdomRegistry.Instance;
        if (reg == null) return;
        var acfg = UtilityActionConfig.LoadConfig();
        if (acfg == null) { Debug.LogWarning("[DiagMilitary] " + tag + " UtilityActionConfig 不可达。"); return; }

        var all = reg.GetAll();
        if (all == null) return;
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            if (k == null || k.id == 0) continue;    // 玩家国不评（AI 决策面）
            DumpKingdom(tag, k, acfg);
        }

        // [TreasureVault] D569/D579：国库（玩家主城 TreasureVault）五元积压
        var tv = TreasureVault.Instance;
        if (tv != null)
        {
            Debug.LogWarning(string.Format(
                "[TreasureVault] {0} gold={1} stone={2} wood={3} food={4} metal={5}（口径=玩家主城国库；AI 侧无公开 Vault 口⇒列报）",
                tag, tv.GetAmount(ResourceType.Gold), tv.GetAmount(ResourceType.Stone),
                tv.GetAmount(ResourceType.Wood), tv.GetAmount(ResourceType.Food),
                tv.GetAmount(ResourceType.Metal)));
        }
    }

    private static void DumpKingdom(string tag, KingdomState k, UtilityActionConfig acfg)
    {
        var bcfg = KingdomBrain.LoadConfig();
        int target = bcfg != null ? UtilityScorer.MilitaryTarget(k, bcfg) : -1;
        // HH.217 治本批：内源势能读数（只读反射；正/负探针实读通道——weight=0 ⇒ drive≡0）
        float drive = -1f;
        try
        {
            var mi = typeof(UtilityScorer).GetMethod("InternalDrive",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (mi != null) drive = (float)mi.Invoke(null, new object[] { k });
        }
        catch { }
        string stage = k.scriptPhase.ToString();
        ScriptStage st = k.scriptPhase ?? ScriptStage.Survive;   // k.scriptPhase 为可空；未设时按存活期（与 ScoreTop 调用面一致）

        // HH.222（D666 裁 / §8.1+§8.2）：在线判据 streak 更新（支撑量异常 ⇒ 当日打标并当场判定）
        string verdict = UpdateJudges(k, bcfg, target, drive);

        // ── [DiagMilitary] 状态行 + ⑦⑯⑰ 三面 ──────────────────────────────
        var sb = new StringBuilder();
        sb.Append("[DiagMilitary] ").Append(tag).Append(" k").Append(k.id)
          .Append(" stage=").Append(stage)
          .Append(" worker=").Append(k.workerCount).Append(" warrior=").Append(k.warriorCount)
          .Append(" gold=").Append(k.GetResourceValue(ResourceType.Gold))
          .Append(" food=").Append(k.GetResourceValue(ResourceType.Food))
          .Append(" stone=").Append(k.GetResourceValue(ResourceType.Stone))
          .Append(" wood=").Append(k.GetResourceValue(ResourceType.Wood))
          .Append(" militaryTarget=").Append(target)
          .Append(" drive=").Append(drive >= 0f ? drive.ToString("F4") : "n/a")   // HH.217：内源势能实读（正/负探针通道）
          .Append(" verdict=").Append(verdict)                                     // HH.222：在线判据打标（跑局容器命中即停）
          .Append(" chSrc=").Append(CountWorldResourceSources());                  // HH.221 判据③：世界资源点任务源注册数
        Debug.LogWarning(sb.ToString());

        DumpAction(tag, k, acfg, UtilityAction.RecruitWarrior, "⑦招战士", st);
        DumpAction(tag, k, acfg, UtilityAction.BuildBarracks, "⑰a建兵营", st);
        DumpAction(tag, k, acfg, UtilityAction.BuildTrainingCamp, "⑰b建训练营", st);
        DumpAction(tag, k, acfg, UtilityAction.TrainGeneral, "⑯训练将军", st);
        // HH.204/D658（A′ 授权）：三军事向专属营逐条明细（minStage=3 军事期；族门禁在 Feasible）
        DumpAction(tag, k, acfg, UtilityAction.BuildWarAcademy, "⑰c建战争学院", st);
        DumpAction(tag, k, acfg, UtilityAction.BuildWarCamp, "⑰d建兽人战营", st);
        DumpAction(tag, k, acfg, UtilityAction.BuildArcheryRange, "⑰e建精灵射箭场", st);

        // 评分淘汰构成普查（HH.115 件E#6 既有公开口）
        UtilityScorer.ScoreCensus census;
        var top = UtilityScorer.ScoreTop(k, acfg, st, out census);
        Debug.LogWarning(string.Format(
            "[DiagMilitary] {0} k{1} census defTotal={2} stageFiltered={3} noNeed={4} infeasible={5} axisFiltered={6} top={7}",
            tag, k.id, census.defTotal, census.stageFiltered, census.noNeed, census.infeasible, census.axisFiltered, top));

        DumpEconomy(tag, k);
    }

    /// <summary>HH.222（D666 裁 / test-harness-first §八 机制 1+2）：逐日更新在线判据 streak 并返回 `verdict=` 打标串。
    /// 支撑量口径＝ stone（存量）＋ Economy.In(Stone)（入库）＋ 六资源入库（异常面）；判据本身由容器消费（TryJudge）。</summary>
    private static string UpdateJudges(KingdomState k, KingdomBrainConfig bcfg, int target, float drive)
    {
        JudgeState s;
        if (!_judge.TryGetValue(k.id, out s)) { s = new JudgeState(); _judge[k.id] = s; }
        int gate = bcfg != null ? bcfg.expandToMilitary_warriorsMin : 4;

        // ── 支撑日志面（§8.1）：石链（存量+入库）/ 六资源全零入库 ──
        int stoneNow = k.GetResourceValue(ResourceType.Stone);
        int stoneIn = 0; bool ecoOk = false; bool allZero = true;
        if (SituationHub.TryGet(k.id, out var sit) && sit != null && sit.Economy != null)
        {
            ecoOk = true;
            stoneIn = sit.Economy.In(EcoResource.Stone);
            foreach (EcoResource r in Enum.GetValues(typeof(EcoResource)))
                if (sit.Economy.In(r) != 0) { allZero = false; break; }
        }
        if (ecoOk)
        {
            if (s.prevStone >= 0 && stoneNow <= s.prevStone && stoneIn == 0) s.noGainDays++; else s.noGainDays = 0;
            if (allZero) s.noIncomeDays++; else s.noIncomeDays = 0;
        }
        s.prevStone = stoneNow;

        // ── 目标日志面：死滞（负探针）/ 机制面已证 ──
        if (drive <= 0f && target >= 0 && target < gate) s.belowGateDays++; else s.belowGateDays = 0;
        if (target >= gate && !s.gateHit)
        {
            s.gateHit = true;
            s.gateHitDay = Application.isPlaying && TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
        }

        var v = new StringBuilder("ok");
        if (s.gateHit) v.Append("|targetGateHit@D").Append(s.gateHitDay);
        if (s.belowGateDays > 0) v.Append("|deadlock:").Append(s.belowGateDays);
        if (s.noGainDays > 0) v.Append("|stoneCold:").Append(s.noGainDays);
        if (ecoOk && s.noIncomeDays > 0) v.Append("|noIncome:").Append(s.noIncomeDays);
        if (ecoOk && stoneIn == 0 && s.noGainDays >= JudgeStoneColdDays) v.Append("|ANOMALY:stoneChain");
        return v.ToString();
    }

    private static void DumpAction(string tag, KingdomState k, UtilityActionConfig acfg, UtilityAction id, string label, ScriptStage st)
    {
        var defOpt = acfg.Find(id);
        if (defOpt == null) { Debug.LogWarning("[DiagMilitary] " + tag + " k" + k.id + " " + label + " def=缺失（配置无条目）"); return; }
        var d = defOpt.Value;

        float need = UtilityScorer.NeedScore(k, d);
        bool feasible = InvokeFeasible(k, d, out bool feasibleOk);

        float axisRaw = d.axisWeight;
        float personality = 1f;
        if (k.personality != null && d.axis >= 0 && d.axis < k.personality.Length)
        { personality = Mathf.Clamp01(k.personality[d.axis]); axisRaw *= personality; }
        float stageW = (d.stageWeight != null && (int)st < d.stageWeight.Length)
            ? Mathf.Max(0f, d.stageWeight[(int)st]) : 1f;
        float score = need * axisRaw * stageW;
        bool stageGate = st < d.minStage;

        Debug.LogWarning(string.Format(
            "[DiagMilitary] {0} k{1} {2} needKind={3} need={4:F3} feasible={5} feasibleReachable={6} " +
            "axisWeight={7} personalityAxis{8}={9:F2} axis={10:F3} stageW={11:F2} score={12:F3} " +
            "minStage={13} stageGateBlocked={14} cost(g/s/w/f)={15}/{16}/{17}/{18} buildingId={19} buildTargetCap={20}",
            tag, k.id, label, d.need, need, feasible, feasibleOk,
            d.axisWeight, d.axis, personality, axisRaw, stageW, score,
            d.minStage, stageGate, d.costGold, d.costStone, d.costWood, d.costFood,
            d.buildingId, d.buildTargetCap));
    }

    private static bool InvokeFeasible(KingdomState k, UtilityActionDef d, out bool reachable)
    {
        reachable = true;
        if (_miFeasible == null)
            _miFeasible = typeof(UtilityScorer).GetMethod("Feasible", PrivStatic);
        if (_miFeasible == null)
        {
            reachable = false;
            if (!_warnedFeasible) { _warnedFeasible = true; Debug.LogWarning("[DiagMilitary] 反射不可达：UtilityScorer.Feasible（⇒按不可判读列报，不破零改动）"); }
            return false;
        }
        try { return (bool)_miFeasible.Invoke(null, new object[] { k, d }); }
        catch (Exception ex) { reachable = false; Debug.LogWarning("[DiagMilitary] Feasible 反射调用异常: " + ex.Message); return false; }
    }

    // ── [DiagCapacity]/[DiagChain]/[DiagTriage]/[DiagBias]：P0 行为面 ────────
    private static void DumpEconomy(string tag, KingdomState k)
    {
        if (!SituationHub.TryGet(k.id, out var sit) || sit == null || sit.Economy == null)
        {
            Debug.LogWarning("[DiagCapacity] " + tag + " k" + k.id + " 快照缺席（SituationHub 无该王国）⇒ P0 行为面不可判读");
            return;
        }
        var eco = sit.Economy;

        if (_miCountProd == null) _miCountProd = typeof(KingdomBrain).GetMethod("CountProductionOf", PrivStatic);
        var ecap = new StringBuilder();
        ecap.Append("[DiagCapacity] ").Append(tag).Append(" k").Append(k.id);
        foreach (EcoResource r in Enum.GetValues(typeof(EcoResource)))
        {
            int prod = -1;
            if (_miCountProd != null)
            {
                try { prod = (int)_miCountProd.Invoke(null, new object[] { eco, r }); } catch { prod = -1; }
            }
            ecap.Append(' ').Append(r).Append(":prod=").Append(prod)
                .Append("/in=").Append(eco.In(r)).Append("/out=").Append(eco.Out(r));
        }
        ecap.Append(" storageOcc=").Append(eco.StorageOccupancy.ToString("F3"))
            .Append(" grainDays=").Append(eco.GrainReserveDays.ToString("F2"));
        Debug.LogWarning(ecap.ToString());

        var chain = new StringBuilder();
        chain.Append("[DiagChain] ").Append(tag).Append(" k").Append(k.id).Append(" missingMustHave=");
        if (eco.MissingMustHave == null || eco.MissingMustHave.Count == 0) chain.Append("none");
        else
        {
            for (int i = 0; i < eco.MissingMustHave.Count; i++)
            {
                var m = eco.MissingMustHave[i];
                chain.Append("{kind=").Append(m.RequiredKind).Append(" req=").Append(m.Required).Append(" act=").Append(m.Actual).Append('}');
            }
        }
        chain.Append(" productionEntries=").Append(eco.Production != null ? eco.Production.Count : 0);
        Debug.LogWarning(chain.ToString());

        if (_miDecideTriage == null) _miDecideTriage = typeof(KingdomBrain).GetMethod("DecideTriage", PrivStatic);
        var dcfg = KingdomDiagnosisConfig.Load();
        var tri = new StringBuilder();
        tri.Append("[DiagTriage] ").Append(tag).Append(" k").Append(k.id);
        if (_miDecideTriage == null || dcfg == null)
        {
            tri.Append(" 反射/config不可达（⇒列报不可判读）");
            if (!_warnedTriage) { _warnedTriage = true; Debug.LogWarning("[DiagMilitary] 反射不可达：KingdomBrain.DecideTriage 或 KingdomDiagnosisConfig（⇒列报）"); }
        }
        else
        {
            foreach (EcoResource r in Enum.GetValues(typeof(EcoResource)))
            {
                try { tri.Append(' ').Append(r).Append('=').Append(_miDecideTriage.Invoke(null, new object[] { eco, dcfg, r })); }
                catch { tri.Append(' ').Append(r).Append("=ERR"); }
            }
        }
        Debug.LogWarning(tri.ToString());

        // [DiagBias] 派工活权重：ResourceBiasConfig 现值（缺类型则列报不可达）
        var biasT = Type.GetType("ResourceBiasConfig, Assembly-CSharp");
        if (biasT == null)
        {
            Debug.LogWarning("[DiagBias] " + tag + " k" + k.id + " 反射不可达：ResourceBiasConfig 类型未找到（⇒列报不可判读）");
        }
        else
        {
            var load = biasT.GetMethod("Load", BindingFlags.Public | BindingFlags.Static);
            object cfgB = null;
            try { if (load != null) cfgB = load.Invoke(null, null); } catch { }
            Debug.LogWarning("[DiagBias] " + tag + " k" + k.id + " ResourceBiasConfig=" + (cfgB != null ? cfgB.ToString() : "不可达"));
        }
    }
}
