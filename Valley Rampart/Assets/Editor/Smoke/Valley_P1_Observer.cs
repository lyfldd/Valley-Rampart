using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  P1 总验收本体运行观测器（Editor-only，不入运行时；HH.71 §5.1 签发工装）
//  职责：日志镜像（白名单 tag+Error/Exception 全量）→ Logs/P1/p1_log_*.log
//        日快照 CSV（逐国 worker/warrior/领土/金粮/阶段）→ Logs/P1/p1_snap_*.csv
//        检查点代码化连跑：day%5==0 → Save("p1_dayXXX") + Save("p1_main") 回正
//        （SaveManager L232 Save(slotId) 会切 CurrentSlotId——检查点后必须回存
//        p1_main，否则每日自动存档跟随污染检查点文件。两序皆安全：观察器
//        最后一步 Save("p1_main") 兜底回正。）
//        灭绝监测（AI worker+warrior==0 连续 3 日告警）+ 军事期达标高亮计数。
//  接口纪律（HH.71 裁决）：读状态只走公开口/EventBus——KingdomRegistry.GetAll()
//  /KingdomState 公开属性（workerCount/warriorCount/Territory/resources/
//  scriptPhase/raceId/foundedDay）/TerritorySystem.GetKingdomTerritory/
//  TimeManager.CurrentDay/SaveManager.Save；禁掏私有字段。
//  业务代码零改动；MenuItem 在 Play 中可点。
// ============================================================================

public static class P1Observer
{
    private const string LogDir = "Logs/P1";
    private const int CheckpointIntervalDays = 5;   // 每 5 游戏日一检查点（HH.71 §三）
    private const int ExtinctStreakDays = 3;        // 灭绝监测连续零人口天数

    private static StreamWriter _logWriter;
    private static StreamWriter _csvWriter;
    private static bool _installed;
    private static string _sessionTag;
    private static readonly Dictionary<int, int> _extinctStreak = new Dictionary<int, int>();
    private static readonly HashSet<int> _militaryReached = new HashSet<int>();
    private static int _lastCheckpointDay = int.MinValue;

    // ── 日志镜像白名单（观察清单八项的数据源面；Error/Exception/Assert 全量放行）──
    private static readonly string[] WhitelistTags =
    {
        "[KingdomBrain]", "[TrainingSystem]", "[SiegeProduction]", "[ToastManager]",
        "[KingdomFoundry]", "[TerritorySystem]", "[KingdomRegistry]", "[SaveManager]",
        "[TimeManager]", "[DayCycle", "[KingdomRegistry]", "[PopulationSystem]",
        "[SmokeApi]", "[WorldLifecycle]", "[VagrantCamp]",
        "[WaterNetwork]", "[AIEconomySettlement]"   // HH.73/D535：AI 供水链观测（AI 桶水量+日结入账）
    };

    // ── 单例读取统一守卫（HH.42 教训：非 Play 态 Singleton<T>.Instance 隐式自建进 DDOL 残留——观察器在
    //    编辑器域常驻，任何非 Play 态日志都会触发 OnLog，必须用 isPlaying 守卫，禁触单例）──
    private static int CurrentDaySafe()
    {
        return Application.isPlaying && TimeManager.Instance != null ? TimeManager.Instance.CurrentDay : -1;
    }

    [MenuItem("Valley/观测/P1_启动观测")]
    public static void Start()
    {
        if (_installed)
        {
            Debug.Log("[P1观察] 已在观测中（幂等守卫），如需重置先停止。");
            return;
        }

        string dir = Path.Combine(Directory.GetCurrentDirectory(), LogDir);
        Directory.CreateDirectory(dir);
        _sessionTag = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        _logWriter = new StreamWriter(Path.Combine(dir, "p1_log_" + _sessionTag + ".log"), append: false, Encoding.UTF8);
        _logWriter.AutoFlush = true;
        _logWriter.WriteLine("# P1 观测日志镜像 " + _sessionTag + "（白名单 tag+Error/Exception 全量；Editor.log 兜底全量）");

        bool csvNew = !File.Exists(Path.Combine(dir, "p1_snap.csv"));
        _csvWriter = new StreamWriter(Path.Combine(dir, "p1_snap.csv"), append: true, Encoding.UTF8);
        _csvWriter.AutoFlush = true;
        if (csvNew) _csvWriter.WriteLine("session,day,kid,name,race,worker,warrior,territoryMid,gold,food,stage,foundedDay");

        Application.logMessageReceived += OnLog;
        EventBus.Subscribe<DaySettledEvent>(OnDaySettled);
        _installed = true;
        _extinctStreak.Clear();
        _militaryReached.Clear();
        _lastCheckpointDay = int.MinValue;
        Debug.Log("[P1观察] 启动：日志镜像+日快照+检查点(每" + CheckpointIntervalDays + "日)+灭绝监测 就位 → " + dir);
        if (Application.isPlaying) SnapshotNow("启动快照");
        else Debug.Log("[P1观察] 非 Play 态启动快照跳过（防隐式单例自建，HH.42 教训）。");
    }

    [MenuItem("Valley/观测/P1_停止观测")]
    public static void Stop()
    {
        if (!_installed) { Debug.Log("[P1观察] 未在观测中。"); return; }
        Application.logMessageReceived -= OnLog;
        EventBus.Unsubscribe<DaySettledEvent>(OnDaySettled);
        _installed = false;
        if (_logWriter != null) { _logWriter.WriteLine("# 停止观测 " + DateTime.Now.ToString("HH:mm:ss")); _logWriter.Dispose(); _logWriter = null; }
        if (_csvWriter != null) { _csvWriter.Dispose(); _csvWriter = null; }
        Debug.Log("[P1观察] 停止：军事期已达标国=[" + string.Join(",", new List<int>(_militaryReached)) + "]（无=空）。文件已封存。");
    }

    [MenuItem("Valley/观测/P1_打印当前状态")]
    public static void DumpNow() => SnapshotNow("手动快照");

    // ── 日志镜像 + 军事期达标检测 ──
    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (_logWriter == null) return;
        bool pass = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        if (!pass)
        {
            for (int i = 0; i < WhitelistTags.Length; i++)
            {
                if (!string.IsNullOrEmpty(condition) && condition.StartsWith(WhitelistTags[i], StringComparison.Ordinal)) { pass = true; break; }
            }
        }
        if (!pass) return;

        int day = CurrentDaySafe();
        _logWriter.WriteLine(string.Format("[D{0}][{1}] {2}", day, type, condition));

        // 军事期达标检测（唯一升级日志锚点 KingdomBrain.cs L95）
        if (condition != null && condition.Contains("剧本阶段 → 军事"))
        {
            int kid = -1;
            int idx = condition.IndexOf(" k", StringComparison.Ordinal);
            if (idx >= 0)
            {
                int start = idx + 2; int end = start;
                while (end < condition.Length && char.IsDigit(condition[end])) end++;
                int.TryParse(condition.Substring(start, end - start), out kid);
            }
            if (kid > 0 && _militaryReached.Add(kid))
            {
                Debug.LogWarning("[P1观察] ⚑⚑ 军事期达标 k" + kid + "（累计 " + _militaryReached.Count + " 个 AI 国）——「" + condition.Trim() + "」");
            }
        }
    }

    // ── 日快照 + 检查点（代码化连跑）+ 灭绝监测 ──
    private static void OnDaySettled(DaySettledEvent evt)
    {
        try
        {
            SnapshotRow(evt.Day);
            ExtinctWatch(evt.Day);
            Checkpoint(evt.Day);
        }
        catch (Exception e)
        {
            Debug.LogError("[P1观察] DaySettled 处理异常: " + e);
        }
    }

    private static void SnapshotNow(string why)
    {
        int day = CurrentDaySafe();
        Debug.Log("[P1观察] " + why + "（day=" + day + "）\n" + BuildSnapshotText(day));
        if (_csvWriter != null) { SnapshotRow(day); }
    }

    private static string BuildSnapshotText(int day)
    {
        var sb = new StringBuilder();
        var reg = KingdomRegistry.Instance;
        if (reg == null) { sb.AppendLine("  KingdomRegistry 未就绪"); return sb.ToString(); }
        var all = reg.GetAll();
        sb.AppendLine("  立国数=" + reg.Count + " 军事期已达标=" + _militaryReached.Count + " 国" + ListInts(_militaryReached));
        var ts = TerritorySystem.Instance;
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            int terr = k.Territory != null ? k.Territory.Count : -1;
            var stage = k.scriptPhase.HasValue ? k.scriptPhase.Value.ToString() : "无";
            sb.AppendLine(string.Format("  k{0} [{1}] race={2} 工={3} 战={4} 领土mid={5} 金={6} 粮={7} 阶段={8} 成立=D{9}",
                k.id, k.name, k.raceId, k.workerCount, k.warriorCount, terr, k.resources.gold, k.resources.food, stage, k.foundedDay));
        }
        return sb.ToString();
    }

    private static void SnapshotRow(int day)
    {
        var reg = KingdomRegistry.Instance;
        if (reg == null || _csvWriter == null) return;
        var all = reg.GetAll();
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            int terr = k.Territory != null ? k.Territory.Count : -1;
            var stage = k.scriptPhase.HasValue ? k.scriptPhase.Value.ToString() : "无";
            _csvWriter.WriteLine(string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}",
                _sessionTag, day, k.id, Csv(k.name), k.raceId, k.workerCount, k.warriorCount, terr,
                k.resources.gold, k.resources.food, stage, k.foundedDay));
        }
        _csvWriter.WriteLine(string.Format("{0},{1},-1,全局,-,{2},-,-,-,-,-,-", _sessionTag, day, all.Count));
    }

    private static void ExtinctWatch(int day)
    {
        var reg = KingdomRegistry.Instance;
        if (reg == null) return;
        var all = reg.GetAll();
        var alive = new HashSet<int>();
        for (int i = 0; i < all.Count; i++)
        {
            var k = all[i];
            if (!k.IsPlayer) alive.Add(k.id);
            if (k.IsPlayer) continue;
            int pop = k.workerCount + k.warriorCount;
            if (pop > 0) { _extinctStreak[k.id] = 0; continue; }
            int s; _extinctStreak.TryGetValue(k.id, out s);
            _extinctStreak[k.id] = s + 1;
            if (_extinctStreak[k.id] == ExtinctStreakDays)
            {
                Debug.LogError("[P1观察] ☠ 灭绝候选: k" + k.id + " [" + k.name + "] 人口归零连续 " + ExtinctStreakDays + " 日（D" + day + "）——领土mid=" + (k.Territory != null ? k.Territory.Count : -1));
            }
        }
    }

    private static void Checkpoint(int day)
    {
        if (day <= 0) return;
        if (day % CheckpointIntervalDays != 0) return;
        if (day == _lastCheckpointDay) return;   // 同日幂等
        _lastCheckpointDay = day;
        var sm = SaveManager.Instance;
        if (sm == null) { Debug.LogError("[P1观察] 检查点失败: SaveManager 未就绪（D" + day + "）"); return; }
        bool a = sm.Save("p1_day" + day.ToString("000"));
        bool b = sm.Save("p1_main");   // 代码化连跑回正（L232 切槽坑固化，HH.71 裁决纪律①）
        Debug.Log("[P1观察] 检查点 D" + day + ": p1_day" + day.ToString("000") + "=" + a + " 回存p1_main=" + b);
    }

    private static string Csv(string s) => s != null ? s.Replace(",", "，") : "";

    private static string ListInts(HashSet<int> set)
    {
        var sb = new StringBuilder();
        foreach (var v in set) sb.Append(v).Append(",");
        return sb.Length > 0 ? "(" + sb.ToString(0, sb.Length - 1) + ")" : "(空)";
    }
}
