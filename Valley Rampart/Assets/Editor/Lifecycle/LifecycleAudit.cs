using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// ============================================================================
//  HH.132 代码化二态 v1 · LifecycleAudit（Editor-only 静态校验器，R1+R2）
//  规格真源：多Agent交接/策划端/HH.132_代码化二态v1_实施计划.md §二/§三（并入 HH.159 件10）
//
//  职责（纯静态校验，零运行时开销 / 零生产代码改动）：
//   R1 事件双向对拍：事件全集=Assembly-CSharp 内 readonly struct `*Event`/`*Command`
//                    → 三类报告（①发布无订阅且无守卫 ②订阅无发布 ③零发零订）
//                    → 与「生命周期登记表 §4」54 事件基线对账（差异列报）
//   R2 存档覆盖对拍：A=Singleton×ISaveable 差集；B=WorldLifecycle.ResetWorldForNext 编排×全库 ResetState 差集
//
//  接口纪律（D561 硬条款）：校验器**只报新违例**；豁免表=代码内静态清单（变更须策划端确认，本类不回写登记表）。
//  反射域纪律（HH.132 排雷 R2）：只扫 Assembly-CSharp（Editor 程序集自身事件不入扫描，防自噪）。
//  幂等：报告主体（lifecycle_audit_report.txt）不含时间戳/随机量，同库重复运行逐字节一致。
// ============================================================================
public static class LifecycleAudit
{
    private const string MenuPath = "Valley/审计/LifecycleAudit（R1+R2）";
    private const string LogDir = "Logs/LifecycleAudit";
    private const string StableReportName = "lifecycle_audit_report.txt";   // 幂等基准（无时间戳）

    private static readonly string[] WhitelistSeedNames =
    {
        "ExecutorMoveCompleteEvent", "ExecutorArrivedEvent", "GameSavedEvent"   // EventBus.WhitelistSeed（噪音静音）
    };

    // ── 豁免表（设计内例外）· 代码内静态清单 ──────────────────────────────
    // 依据 = 生命周期登记表 §4（HH.106，54 事件基线）；初始集来源=HH.132 §二件2。
    // 【D561 硬红线】豁免表变更需策划端确认——本清单只为登记表既有设计内例外，禁自行扩张。
    private static readonly HashSet<string> ExemptEvents = new HashSet<string>
    {
        // ① 登记表 §4 B 组：无守卫零订阅噪音（DZ-077 组，13 项）
        "BuildingPlacedEvent", "GameSavedEvent", "GateStateChangedEvent", "BuildingRuinedEvent",
        "BuildingRepairedEvent", "UnitCommandEvent", "FollowCommand", "GuardDeployCommand",
        "PrioritizeHarvestCommand", "ExecutorArrivedEvent", "ExecutorMoveCompleteEvent",
        "ExecutorAnchorLostEvent", "PortalDestroyedEvent",
        // ② DifficultyChanged（DZ-066 难度死值家族·挂账关联，登记表 §4 C）
        "DifficultyChangedEvent",
        // ③ 守卫式设计内事件（登记表 §4 D：HasSubscribers 守卫 + 预留通道）
        "RegionHeatChangedEvent", "KingdomBrainCreatedEvent",
    };

    // R2A 豁免：无状态 / 运行时态可重算的纯管理器（登记表 §3.2 设计态；显式清单+理由）。
    private static readonly Dictionary<string, string> ExemptSingletons = new Dictionary<string, string>
    {
        { "InputManager", "输入采集器（无持久态）" },
        { "GameStateManager", "状态机（SetState=公开复位口，无跨轮数据）" },
        { "GridSystem", "网格（ClearAll 复位；占格由世界重建派生）" },
        { "BuildingRegistry", "注册表（Clear 复位，态==世界）" },
        { "UnitRegistry", "注册表（Clear 复位，态==世界）" },
        { "MapRenderService", "渲染服务（ClearAllTiles 复位，无业务态）" },
        { "TerritoryOverlay", "渲染层（ClearOverlay 复位，态由 TerritorySystem 派生）" },
        { "ProductionSystem", "每帧产线驱动（态由建筑/单位承载）" },
        { "HappinessSystem", "幸福桶（日结重算=设计态，登记表 §3.2）" },
        { "SatietySystem", "饱食桶（日结重算=设计态）" },
        { "TaxSystem", "税负桶（日结重算=设计态）" },
        { "TradeSystem", "贸易（无跨轮待恢复态=设计态）" },
        { "TaskScheduler", "任务源（Tick 自清无效源=设计态，登记表 §3.2）" },
        { "SimModeManager", "per-kingdom 模式（重建重算=设计态）" },
        { "LODSystem", "LOD 视距（每帧重算）" },
        { "FormationManager", "编队管理（重建重算=设计态）" },
        { "ProjectileManager", "弹道池（跨轮清空=设计态）" },
        { "GroundEffectManager", "地面效果池（跨轮清空）" },
        { "WanderAnchorPool", "游荡锚池（重建重算）" },
        { "SceneHomePointProvider", "场景归位点提供者（场景派生）" },
        { "BuildController", "玩家建造入口（无持久态）" },
        { "InteractionManager", "交互入口（无持久态）" },
        { "SelectionController", "选择器（无持久态）" },
        { "LoadManager", "加载编排（无持久态）" },
        { "TeardownManager", "拆除编排（无持久态）" },
        { "SaveManager", "存档编排（ResetSessionState 复位，登记表 §3.2）" },
        { "UIManager", "UI 栈（无持久态）" },
        { "ToastManager", "提示（无持久态）" },
        { "UnitDataManager", "单位数据表（资产只读）" },
        { "CameraRig", "相机（用户红线 2_10 不入档）" },
        { "DayCycleSettlement", "日结驱动（无跨轮待恢复态）" },
        { "KingdomBrainRegistry", "AI 决策注册（ResetState 编排在位，态由 KingdomRegistry 派生）" },
        { "MachinePlacer", "机器放置入口（无持久态）" },
        { "MachinePanel", "UI 面板（只读展示）" },
        { "OverheadSpeechManager", "气泡表现层（ResetState 编排在位，表现层可丢弃）" },
    };

    // R2B 豁免：编排面外但有合法消费链的 ResetState（登记表 §3.1 注记）。
    private static readonly Dictionary<string, string> ExemptResetState = new Dictionary<string, string>
    {
        // 说明：TimeManager.ResetState 已由 WorldLifecycle 编排（L32）直接调用，
        // 登记表 §3.1 提到的 "LoadState 消费链" 为冗余注记；若未来出现编排面外 ResetState 再入此表。
    };

    // ── 「生命周期登记表 §4」54 事件基线（HH.106）─ 只读对账面 ──────────────
    private static readonly HashSet<string> Baseline54 = new HashSet<string>
    {
        // A 组（33）
        "LeftClickPressedEvent", "LeftClickReleasedEvent", "RightClickPressedEvent", "EscapePressedEvent",
        "ToggleBuildMenuPressedEvent", "ToggleTrainingMenuPressedEvent", "NumberKeyPressedEvent",
        "GameStateChangedEvent", "UnitDiedEvent", "RulerResourceChangedEvent", "UnitDamagedEvent",
        "UnitSpawnedEvent", "UnitHpChangedEvent", "UnitAttributeChangedEvent", "TimeDayChangedEvent",
        "DaySettledEvent", "TimePhaseChangedEvent", "SeasonChangedEvent", "PortalDisasterTriggeredEvent",
        "PortalAttackedEvent", "GameLoadedEvent", "ConfigsLoadedEvent", "MapGeneratedEvent",
        "BuildingUpgradedEvent", "BuildingActivatedEvent", "PathFailedEvent", "TerritoryChangedEvent",
        "KingdomFoundedEvent", "KingdomAttackedEvent", "GuardRegionLostEvent", "EnemyEnteredChunkEvent",
        "FormationSelectedEvent", "FormationDeselectedEvent",
        // B 组（13）
        "BuildingPlacedEvent", "GameSavedEvent", "GateStateChangedEvent", "BuildingRuinedEvent",
        "BuildingRepairedEvent", "UnitCommandEvent", "FollowCommand", "GuardDeployCommand",
        "PrioritizeHarvestCommand", "ExecutorArrivedEvent", "ExecutorMoveCompleteEvent",
        "ExecutorAnchorLostEvent", "PortalDestroyedEvent",
        // C 组（1）
        "DifficultyChangedEvent",
        // D 组（2）
        "RegionHeatChangedEvent", "KingdomBrainCreatedEvent",
        // E 组（4）
        "UnitAttackEvent", "BuildingProductionTickEvent", "UnitDataLoadedEvent", "BuildingDestroyedEvent",
        // F 组（1）
        "EnemyEnteredRegionEvent",
    };

    private const string OrchestrationFile = "Assets/_Game/Systems/Loading/WorldLifecycle.cs";

    // ========================================================================
    //  入口
    // ========================================================================
    [MenuItem(MenuPath)]
    public static void RunFromMenu() => Run();

    /// <summary>核心入口（也可被 execute_menu_item / MCP 调用）。</summary>
    public static void Run()
    {
        var sb = new StringBuilder();
        int r1a = 0, r1b = 0, r1c = 0, r2a = 0, r2b = 0;

        Assembly asm = typeof(EventBus).Assembly;
        if (asm.GetName().Name != "Assembly-CSharp")
            Debug.LogWarning($"[LifecycleAudit] 反射域=(EventBus).Assembly 名称={asm.GetName().Name}（预期 Assembly-CSharp，HH.132 排雷 R2）。");

        string dataPath = Application.dataPath;

        sb.AppendLine("# LifecycleAudit 报告（HH.132 R1+R2 · 代码化二态 v1）");
        sb.AppendLine("# 反射域=" + asm.GetName().Name + "（Editor 程序集不入扫描，防自噪）");
        sb.AppendLine("# 稳定报告不含时间戳（幂等：同库重复运行逐字节一致）");
        sb.AppendLine();

        // ---------------- R1 事件双向对拍 ----------------
        sb.AppendLine("===== R1 事件双向对拍 =====");
        var eventTypes = CollectEventTypes(asm);
        var usage = ScanEventUsage(dataPath);
        var exemptHits = new SortedSet<string>(StringComparer.Ordinal);

        var list1a = new SortedSet<string>(StringComparer.Ordinal);
        var list1b = new SortedSet<string>(StringComparer.Ordinal);
        var list1c = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var name in eventTypes)
        {
            EventUsage u;
            if (!usage.TryGetValue(name, out u)) u = new EventUsage();
            bool exempt = ExemptEvents.Contains(name);
            bool whitelisted = Array.IndexOf(WhitelistSeedNames, name) >= 0;

            if (u.PublishCount > 0 && u.SubscribeCount == 0 && !u.Guarded && !whitelisted)
            {
                if (exempt) exemptHits.Add(name);
                else list1a.Add(name);
            }
            else if (u.SubscribeCount > 0 && u.PublishCount == 0)
            {
                if (exempt) exemptHits.Add(name);
                else list1b.Add(name);
            }
            else if (u.PublishCount == 0 && u.SubscribeCount == 0)
            {
                if (exempt) exemptHits.Add(name);
                else list1c.Add(name);
            }
        }

        AppendViolationBlock(sb, "① 发布无订阅且无守卫", list1a, usage, "补 HasSubscribers 守卫 / 删发布 / 登记豁免表");
        AppendViolationBlock(sb, "② 订阅无发布（死触发器）", list1b, usage, "补发布端 / 退役订阅");
        AppendViolationBlock(sb, "③ 零发零订（死定义）", list1c, usage, "归资产清册 / 代码清扫批删除");

        r1a = list1a.Count; r1b = list1b.Count; r1c = list1c.Count;

        // 豁免命中明细（只报新违例，此处仅枚举命中面供审计核对）
        sb.AppendLine("-- 豁免命中（设计内例外，不计入违例区；D561 变更须策划端确认） = " + exemptHits.Count + " 项 --");
        foreach (var n in exemptHits) sb.AppendLine("   [豁免] " + n);
        sb.AppendLine();

        // 与 54 事件基线对账
        var scanSet = new SortedSet<string>(eventTypes, StringComparer.Ordinal);
        var extra = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var n in scanSet) if (!Baseline54.Contains(n)) extra.Add(n);
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var n in Baseline54) if (!scanSet.Contains(n)) missing.Add(n);

        sb.AppendLine("-- 与登记表 §4 54 事件基线对账 --");
        sb.AppendLine("   扫描事件数=" + scanSet.Count + "  基线数=54");
        sb.AppendLine("   扫描有·基线无（新增，差异 " + extra.Count + " 项）:" + (extra.Count == 0 ? " 无" : ""));
        foreach (var n in extra) sb.AppendLine("      + " + n);
        sb.AppendLine("   基线有·扫描无（缺失/退役，差异 " + missing.Count + " 项）:" + (missing.Count == 0 ? " 无" : ""));
        foreach (var n in missing) sb.AppendLine("      - " + n);
        sb.AppendLine();

        // ---------------- R2A Singleton × ISaveable ----------------
        sb.AppendLine("===== R2A Singleton × ISaveable 差集 =====");
        var singletons = CollectSingletons(asm);
        var r2aHits = new SortedSet<string>(StringComparer.Ordinal);
        var r2aExempt = new SortedSet<string>(StringComparer.Ordinal);
        int r2aSaveable = 0, r2aSpawner = 0;
        foreach (var t in singletons)
        {
            if (typeof(ISaveable).IsAssignableFrom(t)) { r2aSaveable++; continue; }
            if (typeof(ISaveableSpawner).IsAssignableFrom(t)) { r2aSpawner++; r2aExempt.Add(t.Name + "（ISaveableSpawner 契约：承接实体重建）"); continue; }
            if (ExemptSingletons.ContainsKey(t.Name)) { r2aExempt.Add(t.Name); continue; }
            r2aHits.Add(t.Name);
        }
        sb.AppendLine("   Singleton 全集=" + singletons.Count + "  其中 ISaveable=" + r2aSaveable + "  ISaveableSpawner=" + r2aSpawner);
        AppendSimpleBlock(sb, "  有状态单例未实现 ISaveable（违例）", r2aHits, "实现 ISaveable / 加入豁免表（需策划端确认）");
        sb.AppendLine("  豁免命中（无状态纯管理器 / Spawner 契约） = " + r2aExempt.Count + " 项");
        foreach (var n in r2aExempt) sb.AppendLine("     [豁免] " + n + (ExemptSingletons.ContainsKey(n) ? "：" + ExemptSingletons[n] : ""));
        sb.AppendLine();
        r2a = r2aHits.Count;

        // ---------------- R2B 编排 × ResetState ----------------
        sb.AppendLine("===== R2B WorldLifecycle 编排 × 全库 ResetState 差集 =====");
        var resetTypes = CollectResetStateTypes(asm);
        var orchestrated = ScanOrchestrated(dataPath);
        var r2bHits = new SortedSet<string>(StringComparer.Ordinal);
        var r2bExempt = new SortedSet<string>(StringComparer.Ordinal);
        var orchestratedNames = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var n in resetTypes)
        {
            if (orchestrated.Contains(n)) { orchestratedNames.Add(n); continue; }
            if (ExemptResetState.ContainsKey(n)) { r2bExempt.Add(n); continue; }
            r2bHits.Add(n);
        }
        sb.AppendLine("  全库 ResetState 方法类型=" + resetTypes.Count + "  编排命中=" + orchestratedNames.Count);
        AppendSimpleBlock(sb, "  有 ResetState 未被编排调用（违例）", r2bHits, "补入 WorldLifecycle.ResetWorldForNext / 加入豁免表（需策划端确认）");
        sb.AppendLine("  编排命中明细:" + (orchestratedNames.Count == 0 ? " 无" : ""));
        foreach (var n in orchestratedNames) sb.AppendLine("     [编排] " + n);
        if (r2bExempt.Count > 0)
        {
            sb.AppendLine("  豁免命中 = " + r2bExempt.Count + " 项");
            foreach (var n in r2bExempt) sb.AppendLine("     [豁免] " + n + "：" + ExemptResetState[n]);
        }
        sb.AppendLine();
        r2b = r2bHits.Count;

        // ---------------- 汇总行（L-11：明细为主，汇总仅索引）----------------
        sb.AppendLine("===== 汇总（L-11：仅索引；明细见上）=====");
        sb.AppendLine($"R1①发布无订阅且无守卫={r1a}  R1②订阅无发布={r1b}  R1③零发零订={r1c}  " +
                      $"R2A未存档单例={r2a}  R2B未编排ResetState={r2b}");
        sb.AppendLine($"基线对账：扫描多={extra.Count} 扫描少={missing.Count}");
        sb.AppendLine($"判定：{((r1a + r1b + r1c + r2a + r2b) == 0 ? "零违例" : "有违例 " + (r1a + r1b + r1c + r2a + r2b) + " 条")}");

        string report = sb.ToString();
        foreach (var line in report.Split('\n'))
            if (line.TrimEnd('\r').Length > 0) Debug.Log("[LifecycleAudit] " + line.TrimEnd('\r'));

        WriteReports(report);
    }

    // ========================================================================
    //  落盘
    // ========================================================================
    private static void WriteReports(string report)
    {
        string dir = Path.Combine(Directory.GetCurrentDirectory(), LogDir);
        Directory.CreateDirectory(dir);

        // 稳定报告（幂等基准，无时间戳）
        string stablePath = Path.Combine(dir, StableReportName);
        File.WriteAllText(stablePath, report, new UTF8Encoding(false));

        // 时间戳副本（随交付信=L-02 台账快照口径）
        string stamped = "lifecycle_audit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log";
        File.WriteAllText(Path.Combine(dir, stamped), report, new UTF8Encoding(false));

        Debug.Log($"[LifecycleAudit] 落盘：{stablePath} + {stamped}");
    }

    // ========================================================================
    //  反射收集
    // ========================================================================
    private static List<string> CollectEventTypes(Assembly asm)
    {
        var names = new List<string>();
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types; }
        foreach (var t in types)
        {
            if (t == null || !t.IsValueType || t.IsEnum || t.IsPrimitive) continue;
            string n = t.Name;
            if (!(n.EndsWith("Event", StringComparison.Ordinal) || n.EndsWith("Command", StringComparison.Ordinal))) continue;
            names.Add(n);
        }
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static List<Type> CollectSingletons(Assembly asm)
    {
        var list = new List<Type>();
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types; }
        foreach (var t in types)
        {
            if (t == null || t.IsAbstract || !typeof(MonoBehaviour).IsAssignableFrom(t)) continue;
            if (IsSingletonSubclass(t)) list.Add(t);
        }
        list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return list;
    }

    private static bool IsSingletonSubclass(Type t)
    {
        var b = t.BaseType;
        while (b != null)
        {
            if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(Singleton<>)) return true;
            b = b.BaseType;
        }
        return false;
    }

    private static List<string> CollectResetStateTypes(Assembly asm)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types; }
        foreach (var t in types)
        {
            if (t == null || t.IsAbstract) continue;
            var m = t.GetMethod("ResetState", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (m != null && m.ReturnType == typeof(void)) names.Add(t.Name);
        }
        return new List<string>(names);
    }

    // ========================================================================
    //  源码扫描（发布/订阅面 + 编排面）
    // ========================================================================
    private class EventUsage
    {
        public int PublishCount;
        public int SubscribeCount;
        public bool Guarded;   // 至少一个发布点带 HasSubscribers 守卫
        public readonly List<string> Anchors = new List<string>();
    }

    private static readonly Regex RePubGeneric = new Regex(@"\bPublish\s*<\s*(\w+)\s*>", RegexOptions.Compiled);
    private static readonly Regex RePubNew = new Regex(@"\bPublish\s*\(\s*new\s+(\w+)", RegexOptions.Compiled);
    private static readonly Regex RePubIdent = new Regex(@"\bPublish\s*\(\s*(\w+)\s*\)", RegexOptions.Compiled);
    private static readonly Regex ReSubGeneric = new Regex(@"\bSubscribe\s*<\s*(\w+)\s*>", RegexOptions.Compiled);
    private static readonly Regex ReHasSub = new Regex(@"HasSubscribers\s*<\s*(\w+)\s*>", RegexOptions.Compiled);
    private static readonly Regex ReOrchestrated = new Regex(@"(\w+)\s*\.\s*Instance\s*\.\s*ResetState\s*\(\s*\)", RegexOptions.Compiled);

    private static Dictionary<string, EventUsage> ScanEventUsage(string dataPath)
    {
        var map = new Dictionary<string, EventUsage>(StringComparer.Ordinal);
        string prodRoot = Path.Combine(dataPath, "_Game");   // 仅生产代码面（Editor 冒烟订阅不计入运行时接线）
        if (!Directory.Exists(prodRoot)) return map;

        foreach (var file in Directory.GetFiles(prodRoot, "*.cs", SearchOption.AllDirectories))
        {
            string text;
            try { text = File.ReadAllText(file); } catch { continue; }
            string rel = RelPath(file);

            // 发布面三写法：泛型显式 / Publish(new X…) / Publish(ident) 同文件解析
            foreach (Match m in RePubGeneric.Matches(text))
                AddPublish(map, m.Groups[1].Value, rel, LineOf(text, m.Index), text, m.Index);
            foreach (Match m in RePubNew.Matches(text))
                AddPublish(map, m.Groups[1].Value, rel, LineOf(text, m.Index), text, m.Index);
            foreach (Match m in RePubIdent.Matches(text))
            {
                string ident = m.Groups[1].Value;
                var decl = Regex.Match(text, @"(?:var|\w+)\s+" + Regex.Escape(ident) + @"\s*=\s*new\s+(\w+)");
                if (decl.Success)
                    AddPublish(map, decl.Groups[1].Value, rel, LineOf(text, m.Index), text, m.Index);
            }

            // 订阅面（EventBus.Subscribe<T> 泛型实参，全库均显式）
            foreach (Match m in ReSubGeneric.Matches(text))
            {
                var u = Get(map, m.Groups[1].Value);
                u.SubscribeCount++;
                u.Anchors.Add($"{rel}:{LineOf(text, m.Index)}");
            }
        }
        return map;
    }

    private static void AddPublish(Dictionary<string, EventUsage> map, string name, string rel, int line, string text, int idx)
    {
        var u = Get(map, name);
        u.PublishCount++;
        u.Anchors.Add($"{rel}:{line}");
        // 守卫判定（HH.132：发布点前 400 字符窗口内出现 HasSubscribers 守卫）
        int start = Math.Max(0, idx - 400);
        if (ReHasSub.IsMatch(text.Substring(start, idx - start))) u.Guarded = true;
    }

    private static HashSet<string> ScanOrchestrated(string dataPath)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        string path = Path.Combine(dataPath, OrchestrationFile.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        // OrchestrationFile 常量含 "Assets/" 前缀，dataPath 已是 .../Assets → 去掉前缀
        if (!File.Exists(path))
            path = Path.Combine(dataPath, Path.Combine("_Game", "Systems", "Loading", "WorldLifecycle.cs"));
        if (!File.Exists(path)) return set;
        string text = File.ReadAllText(path);
        foreach (Match m in ReOrchestrated.Matches(text)) set.Add(m.Groups[1].Value);
        return set;
    }

    private static EventUsage Get(Dictionary<string, EventUsage> map, string name)
    {
        EventUsage u;
        if (!map.TryGetValue(name, out u)) { u = new EventUsage(); map[name] = u; }
        return u;
    }

    private static int LineOf(string text, int idx)
    {
        int line = 1;
        for (int i = 0; i < idx && i < text.Length; i++) if (text[i] == '\n') line++;
        return line;
    }

    private static string RelPath(string abs)
    {
        string norm = abs.Replace('\\', '/');
        int i = norm.IndexOf("/Assets/", StringComparison.Ordinal);
        return i >= 0 ? norm.Substring(i + 1) : norm;
    }

    // ========================================================================
    //  输出块辅助
    // ========================================================================
    private static void AppendViolationBlock(StringBuilder sb, string title, SortedSet<string> items,
        Dictionary<string, EventUsage> usage, string advice)
    {
        sb.AppendLine("-- " + title + " = " + items.Count + " 条 --");
        foreach (var n in items)
        {
            EventUsage u;
            usage.TryGetValue(n, out u);
            string anchor = (u != null && u.Anchors.Count > 0) ? u.Anchors[0] : "(无发布/订阅锚点)";
            string counts = u == null ? "pub=0 sub=0 guard=0" : $"pub={u.PublishCount} sub={u.SubscribeCount} guard={(u.Guarded ? 1 : 0)}";
            sb.AppendLine($"   [{title.Substring(0, 1)}] {n} 锚点={anchor} ({counts}) 建议={advice}");
        }
    }

    private static void AppendSimpleBlock(StringBuilder sb, string title, SortedSet<string> items, string advice)
    {
        sb.AppendLine(title + " = " + items.Count + " 条");
        foreach (var n in items) sb.AppendLine($"      {n} 建议={advice}");
    }
}
