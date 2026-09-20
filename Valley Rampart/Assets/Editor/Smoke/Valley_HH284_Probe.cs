#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.284「㉕机器实产 · 门判定读数」跑局观测容器（Editor-only · 只读观察 · 业务代码零改动）
//  ---------------------------------------------------------------------------
//  ⚠️ 命名沿革（D732 容器清理）：本文件原为 Valley_HH285_Observe.cs——**它实际是 HH.284 第二步的
//      读数容器**（HH.285 治本批只改 asset·不跑长局），故随 D732 改名 Valley_HH284_Probe（类名/meta/注释/日志路径同步）。
//  依据：HH.284 开工回执 §三（读数方案 R1~R4·L-34 五列）＋ D728 后派工「第二步」。
//  载体取向：**独立容器（D728 裁 1 准 B）**——读数面（㉔/㉕ 三面实读＋R4/R1 存量行）与跑局面**均在本容器内**，
//        ★ 迁入说明：㉔/㉕ 实读原曾以 +27 行尾插形式落在共享诊断文件 Valley_DiagMilitary.cs（违 D728 裁 1，D732 打回）
//        ⇒ 已回退（该文件逐字还原）并**迁入本独立容器**（`DumpMachineReadout`）。
//  档位（HH.284 停手待裁②·建议值放行）：SEED=73621（HH.230 七考正跑档同 seed·与 D684/HH.203 对照）
//        / 新槽 hh284_probe（禁覆盖 p1_run9/p1_run8/HH.214 槽/hh285_obs1）/ **150 日熔断（D733 放行档·A 案治本后复跑：
//        D684 军事期首达 k4@D78/k1@D94 ⇒ 120 日窗不足）**。
//  R5（D733 补）：逐日读 `SiegeProductionSystem.WorkshopLevel()`（各国厂级·全局口径实况）＋与 `GetMachineLimit()`
//        对照 ⇒ 供 `DZ-157`「上限口径跨主体泄漏」定性（现 level 是否恒 1）。
//  只读纪律：本容器不写任何业务状态（相机平移=纯表现层，仅供截图佐证）；证据=Logs/hh284_probe.log
//        （逐行镜像·console 大缓冲教训）+ Logs/hh284_status.log（终局状态）+ Editor.log。
//  命中即停（L-34·HH.284 §3.2 R1 判据）：
//        ① R1 门达标＝top=ProduceMachine 首达 且 该国机器实体 >0（门达标即停）
//        ② R1 空转＝top 首达后实体恒 0 ≥30 日（空转即停·窗口值定案并在报告披露）
//        ③ ANOMALY＝[SiegeProduction]「生成返回 null」退款（HH.282 兜底被触发=prefab 回归·当场判定）
//        ④ D150 熔断。
//  验收两层（D733·不得混判）：层1 评分面＝㉔ 进 top（topBswCount>0 或 topPmCount>0＝HH.285 治本直接效果）；
//        层2 执行面＝feasible/实体可能仍 0（DZ-159 木链未修）⇒ 不得因层2 未过而判层1 未达；零触发亦为证据（分列）。
//  红线：零玩家干预（只观测不建造不训练不输资源）；AI.Core 零触。
// ============================================================================
public static class Valley_HH284_Probe
{
    const int Seed = 73621;
    const string Slot = "hh284_probe";
    const int CircuitBreakDay = 150;
    const int StallStopDays = 30;          // R1 空转窗口：top=ProduceMachine 首达后实体恒 0 ≥N 日
    const int R2SnapshotEvery = 10;        // R2 定期快照间隔（日）

    class KingdomObs
    {
        public int topPmFirstDay = -1; public int topPmCount;      // census top=ProduceMachine（评分面）
        public int topBswFirstDay = -1; public int topBswCount;    // census top=BuildSiegeWorkshop（DZ-105 证据）
        public int stallDays;                                       // top 首达后实体恒 0 连续日
        public int prodOk, prodFail, limitBlock, landed, intercept; // 派发面计数（[SiegeProduction]/[KingdomBrain] 日志）
        public int machines = -1, fac = -1, limit = -1;             // 存量面（每日 tick 只读实读）
        public int wsLevel = -1;                                    // R5（D733）：WorkshopLevel() 实况（全局口径·供 DZ-157 定性）
        public string stage = "?"; public int warrior = -1;         // R3（军事期复证 D684）
    }

    static readonly Dictionary<int, KingdomObs> _obs = new Dictionary<int, KingdomObs>();
    static readonly StringBuilder _log = new StringBuilder();
    static string _obsPath, _statusPath;
    static bool _installed, _finished;
    static int _lastDay = -1;
    static string _stopReason;             // 非 null ⇒ 请求停止（TickDay 判据命中 或 OnLog ANOMALY）
    static ProbeHost _host;
    static MethodInfo _miFeasible;         // UtilityScorer.Feasible（private static·反射只读）
    static bool _warnedFeasible;

    public static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[HH284] 须先 GameScene 进 Play。"); return; }
        if (!Init()) return;
        _host = new GameObject("HH284_ProbeHost").AddComponent<ProbeHost>();
        _host.Host(Coroutine());
    }

    /// <summary>MCP exec_runtime_script 桥接：返回协程让工具等待执行完毕（fire-and-forget 会被退 Play 中断）。</summary>
    public static IEnumerator RunCoroutine()
    {
        if (!Application.isPlaying) { Debug.LogError("[HH284] 须在 Play 模式内调用。"); yield break; }
        if (!Init()) yield break;
        yield return Coroutine();
    }

    class ProbeHost : MonoBehaviour { public void Host(IEnumerator r) => StartCoroutine(r); }

    static bool Init()
    {
        if (_installed) { Debug.LogWarning("[HH284] 已在观测中（幂等守卫）。"); return false; }
        _log.Clear(); _obs.Clear(); _finished = false; _stopReason = null; _lastDay = -1; _host = null;
        _obsPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh284_probe.log"));
        _statusPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh284_status.log"));
        Log($"观测启动：seed={Seed} slot={Slot} 熔断=D{CircuitBreakDay} 空转窗口={StallStopDays}日（只读·业务代码零改动·AI.Core 零触）");
        Application.logMessageReceived += OnLog;
        _installed = true;
        return true;
    }

    static void Log(string msg)
    {
        _log.AppendLine(msg);
        Debug.Log("[HH284] " + msg);
        try
        {
            if (_obsPath != null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_obsPath));
                System.IO.File.WriteAllText(_obsPath, _log.ToString());
            }
        }
        catch { }
    }

    static IEnumerator Coroutine()
    {
        var cfg = new NewGameConfig
        {
            worldSeed = Seed, mapSeed = Seed, raceId = 0, difficulty = 2,
            worldSize = WorldSize.Medium, selectedSlotId = Slot, kingdomName = "河谷王国"
        };
        yield return TestHarnessApi.EnterTestRun(cfg);   // speedOverride=null → 考跑档直通（与 HH.203 同档）

        float t0 = Time.realtimeSinceStartup;
        while (WorldManager.Instance == null || WorldManager.Instance.ActiveMap == null
               || KingdomRegistry.Instance == null)
        {
            yield return null;
            if (Time.realtimeSinceStartup - t0 > 120f) { Finish("等世界就绪超时"); yield break; }
        }

        // 读数单源启动：DiagMilitary 每日 census（既有公开诊断口；本批未改该文件）
        DiagMilitary.ResetJudges();
        DiagMilitary.Start();

        int r2Next = 1;
        while (true)
        {
            int day = TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
            if (day != _lastDay)
            {
                _lastDay = day;
                TickDay(day);
            }
            if (_stopReason != null) { yield return ScreenshotThen(_stopReason); yield break; }
            if (day >= CircuitBreakDay) { yield return ScreenshotThen($"D{CircuitBreakDay} 熔断（窗口跑满）"); yield break; }
            if (day >= r2Next) { SnapshotR2(day, "定期"); r2Next = day + R2SnapshotEvery; }
            yield return null;
        }
    }

    /// <summary>逐日只读 tick：R3 状态 + ㉔/㉕ 实读（迁入） + R4/R1 存量面 + R1 两判据命中即停。</summary>
    static void TickDay(int day)
    {
        var reg = KingdomRegistry.Instance; if (reg == null) return;
        var all = reg.GetAll(); if (all == null) return;
        var acfg = UtilityActionConfig.LoadConfig();
        var sps = SiegeProductionSystem.Instance;
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i]; if (k == null || k.id == 0) continue;   // 排除项=玩家国（HH.284 §3.2）
            var o = Get(k.id);
            o.stage = k.scriptPhase.HasValue ? k.scriptPhase.Value.ToString() : "null";
            o.warrior = k.warriorCount;
            o.machines = sps != null ? sps.GetPlacedMachineCountByKingdom(k.id) : -1;
            o.limit = sps != null ? sps.GetMachineLimit() : -1;
            o.fac = CountSiegeWorkshops(k.id);
            o.wsLevel = sps != null ? sps.WorkshopLevel() : -1;   // R5（D733·DZ-157 对照：全局口径 level vs per-kingdom limit）
            Log($"D{day} k{k.id} stage={o.stage} warrior={o.warrior} fac={o.fac} wsLv={o.wsLevel} machines={o.machines}/{o.limit} " +
                $"top㉕={o.topPmCount}(首达D{o.topPmFirstDay}) top㉔={o.topBswCount}(首达D{o.topBswFirstDay}) " +
                $"派发 成/败/限={o.prodOk}/{o.prodFail}/{o.limitBlock} 落地={o.landed} 拦截={o.intercept} stall={o.stallDays}");
            // ㉔/㉕ 三面实读（HH.284 读数面·D732 自 DiagMilitary 尾插迁入本独立容器）
            if (acfg != null)
            {
                ScriptStage st = k.scriptPhase ?? ScriptStage.Survive;
                DumpMachineReadout(day, k, acfg, UtilityAction.BuildSiegeWorkshop, "㉔建投掷机厂", st);
                DumpMachineReadout(day, k, acfg, UtilityAction.ProduceMachine, "㉕造机器", st);
            }
            // R1 门达标（HH.284 §3.2）：top 首达 且 实体 >0
            if (o.topPmFirstDay >= 0 && o.machines > 0)
            {
                _stopReason = $"R1 门达标：k{k.id} top㉕首达@D{o.topPmFirstDay} 机器实体={o.machines}(>0) 落地={o.landed} 派发成/败/限={o.prodOk}/{o.prodFail}/{o.limitBlock}";
                return;
            }
            // R1 空转（HH.284 §3.2）：top 首达而实体恒 0 ≥StallStopDays 日
            if (o.topPmFirstDay >= 0 && o.machines <= 0)
            {
                o.stallDays++;
                if (o.stallDays >= StallStopDays)
                {
                    _stopReason = $"R1 空转：k{k.id} top㉕首达@D{o.topPmFirstDay} 后实体恒0≥{StallStopDays}日 top计数={o.topPmCount} " +
                                  $"派发成/败/限={o.prodOk}/{o.prodFail}/{o.limitBlock} 拦截={o.intercept} fac={o.fac} stage={o.stage}";
                    return;
                }
            }
        }
    }

    /// <summary>㉔/㉕ 需求/可行/评分三面实读（自 Valley_DiagMilitary 尾插迁入；格式与 DiagMilitary.DumpAction
    /// 同型以便与既有读数逐项对照·L-31 同源纪律）。口径：need=`UtilityScorer.NeedScore`（公开）；
    /// feasible=`UtilityScorer.Feasible`（private static·反射只读）；score=need×axisWeight×personality[axis]×stageWeight。
    /// 排除项：玩家国（调用方已跳过 id=0）。</summary>
    static void DumpMachineReadout(int day, KingdomState k, UtilityActionConfig acfg, UtilityAction id, string label, ScriptStage st)
    {
        var defOpt = acfg.Find(id);
        if (defOpt == null) { Log($"D{day} k{k.id} {label} def=缺失（配置无条目）"); return; }
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

        Log($"D{day} k{k.id} {label} needKind={d.need}(={(int)d.need}) need={need:F3} feasible={feasible} feasibleReachable={feasibleOk} " +
            $"score={score:F3} minStage={d.minStage} stageGateBlocked={stageGate} buildTargetCap={d.buildTargetCap}");
    }

    static bool InvokeFeasible(KingdomState k, UtilityActionDef d, out bool reachable)
    {
        reachable = true;
        if (_miFeasible == null)
            _miFeasible = typeof(UtilityScorer).GetMethod("Feasible", BindingFlags.NonPublic | BindingFlags.Static);
        if (_miFeasible == null)
        {
            reachable = false;
            if (!_warnedFeasible) { _warnedFeasible = true; Debug.LogWarning("[HH284] 反射不可达：UtilityScorer.Feasible（⇒按不可判读列报，不破零改动）"); }
            return false;
        }
        try { return (bool)_miFeasible.Invoke(null, new object[] { k, d }); }
        catch (System.Exception ex) { reachable = false; Debug.LogWarning("[HH284] Feasible 反射调用异常: " + ex.Message); return false; }
    }

    /// <summary>R2：UnitRegistry 在册单位按 kingdomId 分列的职业计数（专属兵 28~34 vs 通用 Warrior；含机器 35~37+弩炮）。</summary>
    static void SnapshotR2(int day, string tag)
    {
        var reg = UnitRegistry.Instance; if (reg == null) return;
        var excl = new Dictionary<int, int[]>(); var mach = new Dictionary<int, int[]>(); var war = new Dictionary<int, int>();
        foreach (var u in reg.GetAllUnits())
        {
            if (u == null || u.Data == null || u.kingdomId <= 0) continue;   // 排除项=玩家国/怪物（HH.284 §3.2 R2）
            int kid = u.kingdomId;
            if (!excl.ContainsKey(kid)) { excl[kid] = new int[7]; mach[kid] = new int[4]; war[kid] = 0; }
            switch (u.EffectiveOccupation)
            {
                case Occupation.Berserker: excl[kid][0]++; break;
                case Occupation.WolfRider: excl[kid][1]++; break;
                case Occupation.Musqueteer: excl[kid][2]++; break;
                case Occupation.Bedrock: excl[kid][3]++; break;
                case Occupation.Ranger: excl[kid][4]++; break;
                case Occupation.Windwalker: excl[kid][5]++; break;
                case Occupation.DeerRider: excl[kid][6]++; break;
                case Occupation.Mortar: mach[kid][0]++; break;
                case Occupation.VineCatapult: mach[kid][1]++; break;
                case Occupation.Ram: mach[kid][2]++; break;
                case Occupation.Ballista: mach[kid][3]++; break;
                case Occupation.Warrior: war[kid]++; break;
            }
        }
        var ks = new List<int>(excl.Keys); ks.Sort();
        if (ks.Count == 0) { Log($"R2/{tag} D{day} 无 AI 国在册单位（灭绝面）"); return; }
        foreach (var kid in ks)
        {
            var e = excl[kid]; var m = mach[kid];
            Log($"R2/{tag} D{day} k{kid} Warrior={war[kid]} 专属兵 Ber={e[0]} Wolf={e[1]} Mus={e[2]} Bed={e[3]} Ran={e[4]} Win={e[5]} Deer={e[6]}（28~34） " +
                $"机器 Mor={m[0]} Vine={m[1]} Ram={m[2]} Bal={m[3]}");
        }
    }

    // ── 日志流解析（R1 评分/派发面 · DZ-105 证据 · ANOMALY）──────────────────
    static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (_finished || string.IsNullOrEmpty(condition)) return;
        string line = condition;
        if (line.StartsWith("[DiagMilitary]") && line.Contains("census"))
        {
            int day, kid;
            if (TryParseDayKingdom(line, out day, out kid))
            {
                var o = Get(kid);
                if (line.Contains("top=ProduceMachine")) { o.topPmCount++; if (o.topPmFirstDay < 0) o.topPmFirstDay = day; }
                else if (line.Contains("top=BuildSiegeWorkshop")) { o.topBswCount++; if (o.topBswFirstDay < 0) o.topBswFirstDay = day; }
            }
            return;
        }
        if (line.StartsWith("[SiegeProduction]"))
        {
            if (line.Contains("生成返回 null"))
            {
                Log("ANOMALY 命中：" + line);
                _stopReason = "ANOMALY：[SiegeProduction] SpawnUnit 返 null 已退款（HH.282 兜底被触发＝prefab 链回归·当场判定 L-34 §8.1）";
                return;
            }
            int kid = ParseKid(line, "[SiegeProduction]");
            if (kid <= 0) return;   // 玩家侧（无 k 前缀）不属本批判据（排除项=玩家国）
            var o = Get(kid);
            if (line.Contains("生产失败")) { o.prodFail++; Log("证据：" + line); }
            else if (line.Contains("已达上限")) { o.limitBlock++; Log("证据：" + line); }
            else if (line.Contains(" 生产 ")) { o.prodOk++; Log("证据：" + line); }
            return;
        }
        if (line.StartsWith("[KingdomBrain]"))
        {
            int kid = ParseKid(line, "[KingdomBrain]");
            if (line.Contains("㉕造机器落地")) { if (kid > 0) { Get(kid).landed++; Log("证据：" + line); } }
            else if (line.Contains("㉕造机器拦截")) { if (kid > 0) { Get(kid).intercept++; Log("证据：" + line); } }
        }
    }

    static KingdomObs Get(int kid)
    {
        KingdomObs o;
        if (!_obs.TryGetValue(kid, out o)) { o = new KingdomObs(); _obs[kid] = o; }
        return o;
    }

    static bool TryParseDayKingdom(string line, out int day, out int kid)
    {
        day = -1; kid = -1;
        int i = line.IndexOf("] D"); if (i < 0) return false;
        i += 3; int d = 0; bool any = false;
        while (i < line.Length && line[i] >= '0' && line[i] <= '9') { d = d * 10 + (line[i] - '0'); i++; any = true; }
        if (!any || i + 1 >= line.Length || line[i] != ' ' || line[i + 1] != 'k') return false;
        i += 2; int k = 0; any = false;
        while (i < line.Length && line[i] >= '0' && line[i] <= '9') { k = k * 10 + (line[i] - '0'); i++; any = true; }
        if (!any) return false;
        day = d; kid = k; return true;
    }

    static int ParseKid(string line, string prefix)
    {
        int i = line.IndexOf(" k", prefix.Length);
        if (i < 0) return -1;
        i += 2; int n = 0; bool any = false;
        while (i < line.Length && line[i] >= '0' && line[i] <= '9') { n = n * 10 + (line[i] - '0'); i++; any = true; }
        return any ? n : -1;
    }

    /// <summary>本国 Active 投掷机厂数（口径镜像 UtilityScorer.CountActiveDef＝kingdomId+IsActive+def.id；-1=不可达）。</summary>
    static int CountSiegeWorkshops(int kingdomId)
    {
        var reg = BuildingRegistry.Instance;
        if (reg == null || reg.All == null) return -1;
        int n = 0;
        for (int i = 0; i < reg.All.Count; i++)
        {
            var b = reg.All[i];
            if (b != null && b.def != null && b.kingdomId == kingdomId && b.IsActive && b.def.id == "SiegeWorkshop") n++;
        }
        return n;
    }

    static Vector2? FirstMachinePos()
    {
        var reg = UnitRegistry.Instance; if (reg == null) return null;
        foreach (var u in reg.GetAllUnits())
        {
            if (u == null || u.Data == null || u.kingdomId <= 0) continue;
            var occ = u.EffectiveOccupation;
            if (occ == Occupation.Mortar || occ == Occupation.VineCatapult || occ == Occupation.Ram || occ == Occupation.Ballista)
                return u.transform.position;
        }
        return null;
    }

    /// <summary>截图佐证：相机平移到首台机器（纯表现层·不改业务状态）→ Game 窗口截图 → 终局收尾。</summary>
    static IEnumerator ScreenshotThen(string reason)
    {
        Vector2? mpos = FirstMachinePos();
        if (mpos.HasValue && CameraRig.Instance != null)
        {
            var tr = CameraRig.Instance.transform;
            tr.position = new Vector3(mpos.Value.x, mpos.Value.y, tr.position.z);
            Log($"截图前相机平移至首台机器 @ {mpos.Value}（表现层·只读纪律不受影响）");
            yield return null; yield return null;
        }
        string shot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "hh284_live.png"));
        ScreenCapture.CaptureScreenshot(shot);
        yield return null;
        Log($"视觉佐证：Game 窗口截图 → {shot}（exists={System.IO.File.Exists(shot)}）");
        Finish(reason);
    }

    static void Finish(string reason)
    {
        if (_finished) return;
        _finished = true;
        SnapshotR2(_lastDay, "终局");
        Log($"终局：{reason}");
        Log($"收尾：ExitTestRun + DiagMilitary.Stop + QuitSmoke + 退 Play（L-32）· 证据={_obsPath}");
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_statusPath));
            System.IO.File.WriteAllText(_statusPath, _log.ToString() + "\n[FINISH] " + reason + "\n");
        }
        catch { }
        Application.logMessageReceived -= OnLog;
        _installed = false;
        TestHarnessApi.ExitTestRun();
        try { DiagMilitary.Stop(); } catch { }
        try { SmokeApi.QuitSmoke(); } catch { }
        EditorApplication.ExitPlaymode();
        if (_host != null) Object.Destroy(_host.gameObject);
    }
}
#endif
